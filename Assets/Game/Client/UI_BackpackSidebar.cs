using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包侧栏（右键 tooltip + 双列版）—— 常驻屏幕一侧，TFT 式「固定空槽位」：
/// 在滚动视图 Content（backpackContainer）下**一次性生成指定数量的槽位**（双列由 LayoutGroup 决定：
/// 左列装备、右列道具），生成后不增删，仅反复填充/清空。填充规则：**偶索引槽位 = 装备（按获得顺序）、
/// 奇索引槽位 = 道具（按获得顺序）**，各自填列；该列无物品的槽保持空槽（父物体空槽背景 + 物品图隐藏）。
/// 超出列容量的溢出部分暂不展示（后续处理）。
///
/// 显隐（CanvasGroup 版）：脚本挂在一个常驻 GameObject 上（本物体即侧栏根，无需 panelRoot 子物体），
/// **不用 SetActive 控制显隐**——避免用户把 panelRoot 拖成本物体时 SetActive(false) 把脚本自身禁用、
/// 订阅断开的坑。显隐一律走本物体自动补挂的 CanvasGroup：
///   隐藏 = alpha 0 + blocksRaycasts false + interactable false（透明、不挡射线、不交互）
///   显示 = alpha 1 + blocksRaycasts true + interactable true（不透明、可交互）
/// 本物体始终保持 active（订阅不断）。
///
/// 穿透阻挡：UIPanelRaycastBlocker 自动补挂到**本物体**；侧栏显示时（blocksRaycasts=true）
/// 鼠标在侧栏上 → InputHandler 检测到标记 → 拦截棋盘点击/悬停；隐藏时不挡。
///
/// 交互（右键 tooltip 版；拖拽是下一步）：
///   - 左键单击图标：单选高亮（父物体 Image 变色，无独立高亮子物体）
///   - 右键图标：显示 tooltip（贴滚动视图右缘、与本格垂直平齐，不跟随鼠标）——装备「名称 + 属性」
///     （BuildStatsText）；道具「名称 + 描述 + AP」（BuildGridItemDesc）；空槽无 tooltip。
///     关闭（两条件同时成立）：鼠标离开滚动视图区域 且 不在 tooltip 面板上（UI_ItemTooltip 负责）
///   - 左键单击空槽：无响应（不高亮不选中，问题4）；左键单击有物品格：单选高亮
///   - 左键双击道具：GridItemManager.UseFromInventory（移出背包→瞄准→命中消耗+扣AP/取消退回；
///     requiresSelectedPiece 未选中己方棋子时由管理器校验拒绝）；双击装备/空槽无动作
///   - 左键拖拽（EventSystem 拖拽阈值天然区分拖拽与单击/双击）：
///     · 装备 → 拖到可施加棋子 = 装备（EquipToPiece：首个空槽，满则替换槽 0）
///     · requiresSelectedPiece 道具（如传送石）→ 拖到可施加棋子 = 选中该棋子 + UseFromInventory
///     · 其余道具不可拖（双击使用）；空槽不可拖
///     · 松手判定：RaycastTile（3D 物理射线，与 UI 解耦）→ GetPieceAt → 统一 CanApplyDragTarget
///       （当前=己方棋子；扩展点：将来按负载类型/数据字段扩出「可对敌方施加」）
///     · 拖拽表现：跟随鼠标的幽灵图标 + 可施加棋子所在格战术层常亮（浅金
///       GameConfig.overlayDragTargetColor；结束/取消即清除；悬停层走现有默认悬停色）
///
/// 常驻红线：本面板【绝不调用】InputHandler.RegisterPanelOpen/RegisterPanelClose（面板开闭计数）。
///
/// Editor 搭建：
///   1. Canvas 侧边创建 BackpackSidebarHost（本物体含背景 Image[空单槽侧栏背景，RaycastTarget=true]、
///      titleText 可选、滚动视图 Content=backpackContainer[GridLayoutGroup Fixed Column Count=2 双列]）
///   2. 子物体 Tooltip（挂 UI_ItemTooltip，titleText/descText；置于最顶层、背景 Image 需
///      RaycastTarget=true——「移进 tooltip 保持/移出关闭」依赖射线命中自身）
///   3. 挂本脚本到 BackpackSidebarHost，拖入 backpackContainer / iconSlotPrefab / tooltip；
///      slotCount 配置槽位数量（默认 10）
/// </summary>
public class UI_BackpackSidebar : MonoBehaviour
{
    [Header("侧栏引用")]
    [SerializeField] private Transform backpackContainer;    // 滚动视图 Content（脚本在此生成指定数量槽位）
    [SerializeField] private UI_BackpackIconSlot iconSlotPrefab;
    [SerializeField] private UI_ItemTooltip tooltip;
    [SerializeField] private TextMeshProUGUI titleText;      // 可选标题
    [Tooltip("滚动视图区域（tooltip 定位与关闭判定的基准；背包系统的 Scroll View 根）")]
    [SerializeField] private RectTransform scrollViewArea;

    private CanvasGroup _canvasGroup;   // 显隐控制（自动补挂；不用 SetActive 禁用自己）
    private GameConfig _config;         // 容量与拖拽高亮色（UI 与购买入口同源）

    private readonly List<UI_BackpackIconSlot> _fixedSlots = new();    // 生成的固定槽（一次创建，反复填充/清空）
    private UI_BackpackIconSlot _selectedIcon;   // 当前单选高亮的格子（单击切换）
    private Image _dragGhost;                    // 拖拽跟随幽灵（临时 Image；raycastTarget=false 不挡射线）
    private RectTransform _dragGhostRect;

    private bool _subscribed;

    // ==========================================
    //  生命周期与订阅
    // ==========================================
    private void OnEnable()
    {
        StartCoroutine(SubscribeNextFrame());
    }

    private System.Collections.IEnumerator SubscribeNextFrame()
    {
        yield return null;   // 等单例就绪（与其他面板同惯例）

        // 显隐基座：本物体自动补挂 CanvasGroup + 穿透阻挡标记（本物体保持 active，订阅不断）
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (GetComponent<UIPanelRaycastBlocker>() == null)
            gameObject.AddComponent<UIPanelRaycastBlocker>();

        SetVisible(false);   // 初始隐藏（战前无背包内容）

        // 配置加载（容量与拖拽高亮色；与购买入口同源——GameConfig 缺失时用字段默认兜底）
        _config = Resources.Load<GameConfig>("GameConfig");

        if (tooltip != null) tooltip.Hide();

        var turnModel = TurnManager.Instance != null ? TurnManager.Instance.Model : null;
        if (turnModel != null)
        {
            turnModel.OnTurnStarted += OnTurnStarted;     // 回合开始（含首个回合=对战开始）→ 显示 + 按活动玩家刷新
            turnModel.OnGameOver += OnGameOver;           // 游戏结束 → 隐藏
        }
        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.OnEquipmentChanged += OnEquipmentChanged;
        if (GridItemManager.Instance != null)
            GridItemManager.Instance.OnInventoryChanged += OnInventoryChanged;
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (!_subscribed) return;
        _subscribed = false;

        var turnModel = TurnManager.Instance != null ? TurnManager.Instance.Model : null;
        if (turnModel != null)
        {
            turnModel.OnTurnStarted -= OnTurnStarted;
            turnModel.OnGameOver -= OnGameOver;
        }
        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.OnEquipmentChanged -= OnEquipmentChanged;
        if (GridItemManager.Instance != null)
            GridItemManager.Instance.OnInventoryChanged -= OnInventoryChanged;
    }

    // ==========================================
    //  显隐（CanvasGroup；本物体永远 active）
    // ==========================================
    /// <summary>显隐切换：显示 = alpha 1 + 挡射线 + 可交互；隐藏 = alpha 0 + 不挡射线 + 不交互</summary>
    private void SetVisible(bool visible)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.blocksRaycasts = visible;
        _canvasGroup.interactable = visible;
    }

    // ==========================================
    //  容量（与购买入口同源：GameConfig 装备/道具容量；缺失时兜底 5——与购买入口口径统一）
    // ==========================================
    /// <summary>装备列容量（GameConfig.backpackEquipmentCapacity；UI 与购买校验同源）</summary>
    private int EquipmentCapacity => _config != null ? _config.backpackEquipmentCapacity : 5;
    /// <summary>道具列容量（GameConfig.backpackItemCapacity；UI 与购买校验同源）</summary>
    private int ItemCapacity => _config != null ? _config.backpackItemCapacity : 5;

    // ==========================================
    //  事件刷新（侧栏可见时才重建）
    // ==========================================
    private void OnTurnStarted(int turn, PlayerSide side)
    {
        SetVisible(true);
        Refresh();
    }

    private void OnGameOver(PlayerSide winner)
    {
        SetVisible(false);
        DestroyGhost();               // 拖拽中途游戏结束 → 清理幽灵
        ClearDragTargetHighlights();  // + 清目标格常亮
        if (tooltip != null) tooltip.Hide();
    }

    private void OnEquipmentChanged(PlayerSide side)
    {
        if (IsVisible) Refresh();
    }

    private void OnInventoryChanged(PlayerSide side)
    {
        if (IsVisible) Refresh();
    }

    private bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0.5f;

    // ==========================================
    //  生成与填充
    // ==========================================
    /// <summary>刷新：确保槽位已生成 → 按顺序填充（装备在前道具在后）→ 剩余空槽</summary>
    private void Refresh()
    {
        if (iconSlotPrefab == null || backpackContainer == null) return;

        EnsureFixedSlots();

        PlayerSide side = TurnManager.Instance != null ? TurnManager.Instance.ActivePlayer : PlayerSide.P1;

        if (titleText != null)
            titleText.text = $"{(side == PlayerSide.P1 ? "玩家1" : "玩家2")} · 背包";

        ClearSelection();   // 重建填充后重置单选（格子不销毁，但内容可能已换）

        // 双列填充（左装备右道具）：偶索引槽位 = 装备列、奇索引槽位 = 道具列（GridLayoutGroup
        // Fixed Column Count=2 时：偶索引落左列、奇索引落右列；各自按获得顺序填列）
        var backpack = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetBackpack(side) : null;
        var inventory = GridItemManager.Instance != null ? GridItemManager.Instance.GetInventory(side) : null;
        int eqCount = backpack != null ? backpack.Count : 0;
        int itemCount = inventory != null ? inventory.Count : 0;

        int eqIndex = 0;    // 装备列已填充数（偶索引消耗）
        int itemIndex = 0;  // 道具列已填充数（奇索引消耗）
        int eqSlots = EquipmentCapacity;    // 装备列容量（GameConfig 同源）
        int itemSlots = ItemCapacity;       // 道具列容量（GameConfig 同源）

        for (int i = 0; i < _fixedSlots.Count; i++)
        {
            var slot = _fixedSlots[i];
            if (i % 2 == 0)
            {
                // 偶索引 = 左列装备槽：按获得顺序填入；超容量溢出暂不展示
                if (eqIndex < eqCount && eqIndex < eqSlots)
                {
                    var eq = backpack[eqIndex];
                    slot.SetIcon(eq != null ? eq.Data.icon : null);
                    slot.TooltipTitle = eq != null ? eq.Data.displayName : "—";
                    slot.TooltipDesc = eq != null ? UI_ShopPanel.BuildStatsText(eq.Data) : "";
                    slot.OnDoubleClick = null;   // 装备无双击（拖拽装备）
                    slot.DragEquipment = eq;     // 拖拽负载：装备（可拖 → 拖到己方棋子=装备）
                    slot.DragItem = null;
                    slot.IsDragEnabled = eq != null;
                    eqIndex++;
                }
                else
                {
                    // 空槽：父物体空槽背景 + 物品图隐藏（不响应单击/右键/拖拽——空槽无 tooltip 内容）
                    slot.SetIcon(null);
                    slot.OnDoubleClick = null;
                    slot.DragEquipment = null;
                    slot.DragItem = null;
                    slot.IsDragEnabled = false;
                }
            }
            else
            {
                // 奇索引 = 右列道具槽：按获得顺序填入；超容量溢出暂不展示
                if (itemIndex < itemCount && itemIndex < itemSlots)
                {
                    var data = inventory[itemIndex];
                    slot.SetIcon(data != null ? data.icon : null);
                    slot.TooltipTitle = data != null ? data.displayName : "—";
                    slot.TooltipDesc = data != null ? $"{UI_ShopPanel.BuildGridItemDesc(data)}\n消耗 {data.apCost} AP" : "";
                    var captured = data;
                    slot.OnDoubleClick = data != null ? () => OnUseItem(side, captured) : null;
                    // 拖拽负载：仅 requiresSelectedPiece 道具（如传送石）可拖 → 拖到己方棋子=选中+使用；
                    // 其余道具不可拖（仍走双击使用）
                    slot.DragEquipment = null;
                    slot.DragItem = data != null && data.requiresSelectedPiece ? data : null;
                    slot.IsDragEnabled = slot.DragItem != null;
                    itemIndex++;
                }
                else
                {
                    // 空槽：父物体空槽背景 + 物品图隐藏（不响应单击/右键/拖拽——空槽无 tooltip 内容）
                    slot.SetIcon(null);
                    slot.OnDoubleClick = null;
                    slot.DragEquipment = null;
                    slot.DragItem = null;
                    slot.IsDragEnabled = false;
                }
            }
        }
    }

    /// <summary>生成固定槽位：首次调用时在 backpackContainer 下一次性 Instantiate
    ///（装备列容量 + 道具列容量，均 GameConfig 同源；此后不增删，仅反复填充/清空；
    /// 双列排布由 Content 上的 GridLayoutGroup[Column Count=2] 决定）</summary>
    private void EnsureFixedSlots()
    {
        if (_fixedSlots.Count > 0) return;
        int count = Mathf.Max(2, EquipmentCapacity + ItemCapacity);
        for (int i = 0; i < count; i++)
        {
            var slot = Instantiate(iconSlotPrefab, backpackContainer);
            _fixedSlots.Add(slot);
            BindCommon(slot);   // tooltip 注入 + 单击单选 + 拖拽回调
            slot.SetIcon(null); // 初始全部空槽（首次 Refresh 会立即填充）
        }
    }

    /// <summary>格子公共绑定：tooltip + 滚动区域基准注入 + 单击单选 + 拖拽回调（幽灵跟随与松手判定都在侧栏）</summary>
    private void BindCommon(UI_BackpackIconSlot slot)
    {
        slot.Tooltip = tooltip;
        slot.TooltipScrollArea = scrollViewArea != null ? scrollViewArea : backpackContainer as RectTransform;
        slot.OnClicked = OnIconClicked;
        slot.OnBeginDragSlot = OnSlotBeginDrag;
        slot.OnDragSlot = OnSlotDrag;
        slot.OnEndDragSlot = OnSlotEndDrag;
    }

    /// <summary>单击单选：取消旧高亮、点亮新格子（父物体变色；空槽不响应——问题4 双保险）</summary>
    private void OnIconClicked(UI_BackpackIconSlot clicked)
    {
        if (clicked == null || !clicked.HasItem) return;
        if (_selectedIcon == clicked) return;
        if (_selectedIcon != null) _selectedIcon.SetSelected(false);
        _selectedIcon = clicked;
        if (_selectedIcon != null) _selectedIcon.SetSelected(true);
    }

    private void ClearSelection()
    {
        if (_selectedIcon != null) _selectedIcon.SetSelected(false);
        _selectedIcon = null;
    }

    // ==========================================
    //  交互（只调用核心管理器，不改装备/瞄准逻辑）
    // ==========================================
    private void OnUseItem(PlayerSide side, GridItemData data)
    {
        if (GridItemManager.Instance == null) return;
        GridItemManager.Instance.UseFromInventory(side, data);
        // 进入瞄准/退回背包均触发 OnInventoryChanged → 自动刷新；
        // 校验失败（未选中己方棋子/AP 不足）不触发使用，Console 有日志提示（不误用）
    }

    // ==========================================
    //  拖拽（负载分工见槽位注释；松手判定 = RaycastTile → GetPieceAt → 统一可施加判定）
    // ==========================================
    /// <summary>统一「可施加目标」判定（拖拽高亮收集与松手判定共用同一口径）。
    /// 当前口径 = 己方存活棋子（null / 已死亡 / 非己方 → false）；
    /// 将来「可对敌方施加的物品」在此按负载类型/数据字段扩展，不散落多处。</summary>
    private bool CanApplyDragTarget(PlayerSide side, PieceModel piece)
    {
        return piece != null && !piece.IsDead && piece.Owner == side;
    }

    /// <summary>拖拽开始：创建跟随幽灵 + 收集「可施加棋子」所在格常亮（战术层，浅金）</summary>
    private void OnSlotBeginDrag(UI_BackpackIconSlot slot)
    {
        DestroyGhost();
        Sprite icon = slot.CurrentIcon;
        if (icon == null) return;

        var go = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
        _dragGhostRect = go.transform as RectTransform;
        _dragGhostRect.SetParent(transform, false);       // 挂侧栏根下（Canvas 同层；置顶 sibling）
        _dragGhostRect.SetAsLastSibling();
        _dragGhostRect.sizeDelta = new Vector2(64f, 64f);
        _dragGhost = go.GetComponent<Image>();
        _dragGhost.sprite = icon;
        _dragGhost.raycastTarget = false;                 // 幽灵不挡射线（不干扰松手判定与棋盘交互）
        _dragGhost.color = new Color(1f, 1f, 1f, 0.85f);  // 略透明区分「拖拽中」
        MoveGhostToMouse();

        ShowDragTargetHighlights(slot);
    }

    /// <summary>拖拽中：幽灵跟随鼠标（屏幕空间坐标；Screen Space Overlay 画布直用屏幕坐标）</summary>
    private void OnSlotDrag(UI_BackpackIconSlot slot)
    {
        MoveGhostToMouse();
    }

    /// <summary>拖拽结束：销毁幽灵 + 清除目标格常亮 → 松手判定 → 可施加棋子执行（装备/选中+使用），否则取消</summary>
    private void OnSlotEndDrag(UI_BackpackIconSlot slot)
    {
        DestroyGhost();
        ClearDragTargetHighlights();

        // 不可拖负载的格子（防御；BeginDrag 已拦）或负载已被刷新清空 → 无动作
        if (!slot.IsDragEnabled || (slot.DragEquipment == null && slot.DragItem == null)) return;

        PlayerSide side = TurnManager.Instance != null ? TurnManager.Instance.ActivePlayer : PlayerSide.P1;

        // 松手判定：3D 物理射线（RaycastTile 打 PieceView/HexTile，与「鼠标是否在 UI 上」解耦）
        var tile = ChessBoardController.Instance != null ? ChessBoardController.Instance.RaycastTile() : null;
        var piece = tile != null ? PieceLayoutModel.Instance.GetPieceAt(tile.Coord) : null;

        // 统一可施加判定（与高亮收集同一口径，问题5）：不可施加 → 取消（无副作用）
        if (!CanApplyDragTarget(side, piece))
        {
            Debug.Log("[BackpackSidebar] 拖拽取消：未命中可施加棋子");
            return;
        }

        if (slot.DragEquipment != null)
        {
            // 装备负载 → 装备到该棋子：首个空槽，满则替换槽 0（沿用侧栏旧口径）
            int equipSlot = 0;
            for (int i = 0; i < piece.EquippedItems.Length; i++)
            {
                if (piece.EquippedItems[i] == null) { equipSlot = i; break; }
            }
            EquipmentManager.Instance?.EquipToPiece(piece, slot.DragEquipment, equipSlot);
            // OnEquipmentChanged → 自动刷新（背包移除 + 被动 OnEquip 已在 EquipToPiece 内）
        }
        else if (slot.DragItem != null)
        {
            // 道具负载（requiresSelectedPiece）→ 先选中该棋子再使用（UseFromInventory 内部校验选中/AP）
            BattleController.Instance?.Model?.SetSelection(piece);
            OnUseItem(side, slot.DragItem);
            // 进入瞄准/退回背包触发 OnInventoryChanged → 自动刷新
        }
    }

    /// <summary>幽灵跟随鼠标（任意 Canvas 渲染模式通用：Overlay 直用屏幕坐标；
    /// Camera/World Space 用 RectTransformUtility.ScreenPointToWorldPointInRectangle 换算）</summary>
    private void MoveGhostToMouse()
    {
        if (_dragGhostRect == null) return;
        Vector2 mouse = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            // Screen Space Camera / World Space：屏幕坐标 → 画布平面世界坐标
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvas.transform as RectTransform, mouse, canvas.worldCamera, out Vector3 worldPos))
            {
                _dragGhostRect.position = worldPos;
                return;
            }
        }
        _dragGhostRect.position = new Vector3(mouse.x, mouse.y, 0f);   // Overlay 兜底
    }

    // ==========================================
    //  拖拽目标格常亮（战术层；颜色 GameConfig.overlayDragTargetColor，问题5）
    // ==========================================
    private readonly List<HexCoord> _dragHighlightCoords = new();

    /// <summary>拖拽开始：把「可施加该负载的棋子」所在格做战术层常亮（统一 CanApplyDragTarget 口径）</summary>
    private void ShowDragTargetHighlights(UI_BackpackIconSlot slot)
    {
        ClearDragTargetHighlights();
        var board = ChessBoardController.Instance;
        if (board == null) return;

        PlayerSide side = TurnManager.Instance != null ? TurnManager.Instance.ActivePlayer : PlayerSide.P1;
        Color color = _config != null ? _config.overlayDragTargetColor : new Color(1f, 0.85f, 0.3f, 0.4f);

        var turnModel = TurnManager.Instance != null ? TurnManager.Instance.Model : null;
        if (turnModel == null) return;
        foreach (var piece in turnModel.GetPieces(side))   // 活动玩家全体棋子
        {
            if (!CanApplyDragTarget(side, piece)) continue;   // 统一口径（null/死亡/非己方过滤；扩展点）
            var tile = board.GetTile(piece.Coord);
            if (tile == null) continue;
            tile.overlay?.Show(color, color.a);   // 战术层常亮（悬停层独立，鼠标经过走默认悬停色）
            _dragHighlightCoords.Add(piece.Coord);
        }
    }

    /// <summary>拖拽结束/取消：清除目标格常亮（只清本次拖拽点亮的格子）</summary>
    private void ClearDragTargetHighlights()
    {
        var board = ChessBoardController.Instance;
        foreach (var coord in _dragHighlightCoords)
        {
            var tile = board != null ? board.GetTile(coord) : null;
            tile?.overlay?.Hide();
        }
        _dragHighlightCoords.Clear();
    }

    /// <summary>销毁拖拽幽灵（结束时/侧栏隐藏时清理）</summary>
    private void DestroyGhost()
    {
        if (_dragGhost != null)
        {
            Destroy(_dragGhost.gameObject);
            _dragGhost = null;
            _dragGhostRect = null;
        }
    }
}
