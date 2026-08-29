using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 将鼠标左键点击转发为 HexTile 事件
/// 挂载在场景中的 InputHandler GameObject 上
///
/// UI 穿透处理：
///   HexTile 命中用的是 3D Physics.Raycast（打 Collider），而 UGUI 的 Image 只能挡
///   GraphicRaycaster 射线，挡不住 Physics 射线。因此必须由本类主动检测鼠标是否在"面板"UI 上。
///   但不能用 IsPointerOverGameObject()——它会命中所有 RaycastTarget=true 的 UGUI 元素
///   （TMP 文字默认 RaycastTarget=true），导致血量/回合等纯显示 UI 也遮挡棋盘。
///   本类改用 GraphicRaycaster.RaycastAll，只当命中的 UI 属于带 UIPanelRaycastBlocker 标记的
///   面板（商店/装备）时才拦截棋盘交互。其余显示 UI 不影响。
///   要求：面板根挂 UIPanelRaycastBlocker，面板背景 Image 保留 RaycastTarget=true。
///
/// 面板开闭计数（IsAnyPanelOpen）：
///   有任意面板打开时整体屏蔽棋盘点击/悬停/大招键（U）；B/E 键仍响应以便关闭面板。
///   UI_EnergyBar 订阅 OnAnyPanelOpenChanged 控制显隐。
/// </summary>
public class InputHandler : MonoBehaviour
{
    /// <summary>玩家点击了某个六边形格子</summary>
    public System.Action<HexTile> OnTileClicked;

    /// <summary>按下 B 键时触发（开启/关闭商店面板）</summary>
    public System.Action OnShopToggle;

    /// <summary>按下 E 键时触发（开启/关闭装备面板）</summary>
    public System.Action OnEquipmentToggle;

    /// <summary>按下 U 键时触发（释放大招；BattleController 订阅处理选中/瞄准）</summary>
    public System.Action OnUltimateToggle;

    /// <summary>按下 R 键时触发（切换高亮显示模式：移动↔攻击；BattleController 订阅处理）</summary>
    public System.Action OnToggleHighlightMode;

    /// <summary>右键点击格子时触发（BattleController 订阅：设置/截断途经点）</summary>
    public System.Action<HexTile> OnWaypointSet;

    // ---- 面板开闭计数（支持商店/装备同时开闭，避免互相覆盖）----
    private static int _openPanelCount;
    /// <summary>是否有任意面板（商店/装备）打开——打开时屏蔽棋盘点击/悬停/大招键，并隐藏能量条</summary>
    public static bool IsAnyPanelOpen => _openPanelCount > 0;
    /// <summary>面板打开/关闭状态翻转时触发（true=有面板打开，false=全部关闭）—— UI_EnergyBar 订阅以控制显隐</summary>
    public static event System.Action<bool> OnAnyPanelOpenChanged;
    /// <summary>面板打开时调用（UI_ShopPanel.Toggle 内 show=true 时；常驻侧栏不调用）</summary>
    public static void RegisterPanelOpen()
    {
        bool wasOpen = _openPanelCount > 0;
        _openPanelCount++;
        if (!wasOpen) OnAnyPanelOpenChanged?.Invoke(true);
    }
    /// <summary>面板关闭时调用（show=false 时）；计数不低于 0</summary>
    public static void RegisterPanelClose()
    {
        bool wasOpen = _openPanelCount > 0;
        _openPanelCount = System.Math.Max(0, _openPanelCount - 1);
        if (wasOpen && _openPanelCount == 0) OnAnyPanelOpenChanged?.Invoke(false);
    }

    private HexTile _currentHoverTile;
    private GameConfig _config;

    // ---- 生命周期 ----
    // GameConfig 在 Awake 加载（悬停色等渲染参数收拢于此，调参无需改代码）
    private void Awake()
    {
        _config = Resources.Load<GameConfig>("GameConfig");
        if (_config == null)
            Debug.LogError("[InputHandler] GameConfig 加载失败！请确保 Assets/Game/Resources/GameConfig.asset 存在。", this);
    }

    // 面板关闭时（IsAnyPanelOpen 转 false）手动触发一次 UpdateHover，
    // 让当前鼠标下的格子立即高亮，玩家无需晃鼠标即可看到悬停反馈。
    private void OnEnable()
    {
        OnAnyPanelOpenChanged += HandleAnyPanelOpenChanged;
    }

    private void OnDisable()
    {
        OnAnyPanelOpenChanged -= HandleAnyPanelOpenChanged;
    }

    private void HandleAnyPanelOpenChanged(bool isOpen)
    {
        if (!isOpen) UpdateHover();
    }

    private void Update()
    {
        // B/E/Space 始终响应（面板打开时仍可用于关闭面板 / 结束回合）
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            TurnManager.Instance?.EndTurn();
        }
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            OnShopToggle?.Invoke();
        }
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            OnEquipmentToggle?.Invoke();
        }

        // 面板打开时屏蔽棋盘点击 / 悬停 / 大招键（B/E 已在上面处理，可关闭面板）
        if (IsAnyPanelOpen)
        {
            if (_currentHoverTile != null)
                _currentHoverTile.overlay?.HideHover();
            _currentHoverTile = null;
            HidePathPreview();
            return;
        }

        // 动画期间屏蔽点击/悬停（不更新悬停、不触发点击）
        if (BattleController.Instance?.Model != null &&
            BattleController.Instance.Model.IsPieceAnimating)
        {
            if (_currentHoverTile != null)
                _currentHoverTile.overlay?.HideHover();
            _currentHoverTile = null;
            HidePathPreview();
            return;
        }

        // 点击（鼠标在 UI 上时不触发棋盘点击，避免商店/装备面板穿透）
        if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
        {
            var tile = ChessBoardController.Instance?.RaycastTile();
            if (tile != null)
            {
                OnTileClicked?.Invoke(tile);
                Debug.Log($"[InputHandler] 点击了 {tile.Coord}");
            }
        }

        // 右键设置/截断途经点（BattleController 订阅 OnWaypointSet 处理）
        if (Mouse.current.rightButton.wasPressedThisFrame && !IsPointerOverUI())
        {
            var tile = ChessBoardController.Instance?.RaycastTile();
            if (tile != null)
            {
                OnWaypointSet?.Invoke(tile);
                // 途经点变化后刷新路径预览（高亮已由 BattleController 更新，预览线需同步重算）
                if (_currentHoverTile != null)
                    UpdatePathPreview(_currentHoverTile);
            }
        }

        // U 键释放大招（BattleController 订阅 OnUltimateToggle 处理选中/瞄准）
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
        {
            OnUltimateToggle?.Invoke();
        }

        // R 键切换高亮显示模式（移动↔攻击）；位于 IsAnyPanelOpen 守卫之后，面板打开时不响应
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            OnToggleHighlightMode?.Invoke();
        }

        // 悬停高亮
        UpdateHover();
    }

    private void UpdateHover()
    {
        // 鼠标在 UI 上时清除棋盘悬停（避免面板下方格子被误高亮）
        if (IsPointerOverUI())
        {
            if (_currentHoverTile != null)
                _currentHoverTile.overlay?.HideHover();
            _currentHoverTile = null;
            HidePathPreview();
            return;
        }

        var tile = ChessBoardController.Instance?.RaycastTile();
        if (tile == _currentHoverTile) return;

        if (_currentHoverTile != null)
            _currentHoverTile.overlay?.HideHover();
        _currentHoverTile = tile;
        if (_currentHoverTile != null)
        {
            // 悬停色取自 GameConfig，Alpha 烘焙在颜色的 .a 里
            if (_config != null)
                _currentHoverTile.overlay?.ShowHover(_config.overlayHoverColor, _config.overlayHoverColor.a);
            UpdatePathPreview(_currentHoverTile);
        }
        else
        {
            HidePathPreview();
        }
    }

    // ==========================================
    //  路径预览
    // ==========================================

    /// <summary>悬停在移动范围内时显示路径预览线（委托 ChessBoardController → View 层渲染）</summary>
    private void UpdatePathPreview(HexTile hoveredTile)
    {
        // 检查是否有选中的棋子
        var battle = BattleController.Instance;
        var selectedPiece = battle?.Model?.SelectedPiece;
        if (selectedPiece == null || selectedPiece.IsDead)
        {
            HidePathPreview();
            return;
        }

        // 攻击模式下不显示路径预览线（仅移动模式才显示）
        if (battle.Model.CurrentHighlightMode != HighlightMode.Move)
        {
            HidePathPreview();
            return;
        }

        // 检查悬停的格子是否在高亮范围内
        if (!battle.Model.IsHighlighted(hoveredTile.Coord))
        {
            HidePathPreview();
            return;
        }

        // 通过 BattleController 计算预览路径（含途经点拼接，统一由 Controller 层处理）
        var path = battle.ComputePreviewPath(hoveredTile);
        if (path == null || path.Count == 0)
        {
            HidePathPreview();
            return;
        }

        // 路径预览的 LineRenderer 由 View 层（ChessBoardView）持有，输入层只负责调用
        ChessBoardController.Instance?.ShowPathPreview(path);
    }

    /// <summary>隐藏路径预览（委托 ChessBoardController → View 层）</summary>
    private void HidePathPreview()
    {
        ChessBoardController.Instance?.HidePathPreview();
    }

    /// <summary>鼠标是否悬停在带 UIPanelRaycastBlocker 标记的面板上（阻挡棋盘点击/悬停穿透）。
    /// 只检测面板类 UI，纯显示 UI（血量/回合等，无标记）不遮挡。</summary>
    private static bool IsPointerOverUI()
    {
        var es = EventSystem.current;
        if (es == null || Mouse.current == null) return false;

        if (_pointerEventData == null)
            _pointerEventData = new PointerEventData(es);
        _pointerEventData.position = Mouse.current.position.ReadValue();

        _raycastResults.Clear();
        es.RaycastAll(_pointerEventData, _raycastResults);
        foreach (var r in _raycastResults)
        {
            // 命中 UI 属于带标记的面板（含子物体）才拦截
            if (r.gameObject != null && r.gameObject.GetComponentInParent<UIPanelRaycastBlocker>() != null)
                return true;
        }
        return false;
    }

    // 缓存避免每帧 GC（IsPointerOverUI 在点击与悬停路径均可能每帧调用）
    private static PointerEventData _pointerEventData;
    private static readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
}
