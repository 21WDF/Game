using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战前流程控制器 —— 单例 MonoBehaviour，管理 选棋子→部署→对战 的完整流程。
/// 挂载在场景中的 GameFlowController GameObject 上。
///
/// 流程：P1选棋子→P1部署→P2选棋子→P2部署→TurnManager.StartFirstTurn
/// 部署阶段为侧边栏模式（UI_Deployment 不 RegisterPanelOpen，不屏蔽棋盘点击），
/// BattleController.HandleTileClick 检查 IsDeploying 并委托 HandleDeployClick。
/// 选棋子/游戏结束面板 RegisterPanelOpen 屏蔽棋盘。
///
/// 半场划分：P1 = r &gt;= 0，P2 = r &lt; 0（若相机朝向相反需交换）。
/// </summary>
public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }

    public enum Phase { None, CityStateSelect, P1Select, P1Deploy, P2Select, P2Deploy, Playing }
    public Phase CurrentPhase { get; private set; } = Phase.None;

    [Header("UI 面板引用（Inspector 拖入）")]
    [SerializeField] private UI_CityStateSelection cityStateSelectionPanel;
    [SerializeField] private UI_UnitSelection unitSelectionPanel;
    [SerializeField] private UI_Deployment deploymentPanel;

    // 城邦选择提交进度（本地驱动层状态：仅用于判断「双方是否都已提交」以驱动 UI 切换，
    // 结算本身由 CityStateManager 的提交管线完成，不依赖顺序）
    private bool _cityP1Submitted;
    private bool _cityP2Submitted;

    // 当前部署方的选中棋子 + 已放置标记（按索引，支持同类型重复选择）
    private readonly List<PieceData> _currentSelection = new();
    private readonly List<bool> _placed = new();
    private int _pendingIndex = -1;

    /// <summary>是否处于部署阶段（BattleController 据此委托点击）</summary>
    public bool IsDeploying => CurrentPhase == Phase.P1Deploy || CurrentPhase == Phase.P2Deploy;

    /// <summary>当前部署方</summary>
    public PlayerSide DeployingPlayer => CurrentPhase == Phase.P1Deploy ? PlayerSide.P1 : PlayerSide.P2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 等棋盘生成完毕后启动战前流程
        Invoke(nameof(BeginPreMatch), 0.3f);
    }

    private void BeginPreMatch()
    {
        // 城邦选择阶段：真实交互 UI（P1 先选、提交后切 P2 = 本地热座驱动方式；
        // 选择/提交/结算逻辑传输无关，联机时换成各自屏幕 + 网络同步即可复用）
        if (CityStateManager.Instance != null && cityStateSelectionPanel != null)
        {
            CurrentPhase = Phase.CityStateSelect;
            _cityP1Submitted = false;
            _cityP2Submitted = false;
            cityStateSelectionPanel.Show(PlayerSide.P1);
            return;
        }

        // 兜底：缺 CityStateManager 或选择面板 → 跳过城邦选择（本局城邦 = None）
        Debug.LogWarning("[GameFlowController] 缺少 CityStateManager 或城邦选择面板，跳过城邦选择（本局城邦 = None）");
        CurrentPhase = Phase.P1Select;
        unitSelectionPanel?.Show(PlayerSide.P1);
    }

    // ==========================================
    //  城邦选择（UI_CityStateSelection 调用）
    // ==========================================
    /// <summary>某方提交心仪城邦（UI 采集后调用；双提交后由 CityStateManager 自动「求交集 + 随机」结算）。
    /// 驱动逻辑：另一方未提交 → 切换面板到另一方；双方都已提交 → 显示揭晓。</summary>
    public void OnCityStateSelectionSubmitted(PlayerSide side, List<CityStateKind> choices)
    {
        var manager = CityStateManager.Instance;
        if (manager == null || CurrentPhase != Phase.CityStateSelect) return;

        manager.SubmitSelection(side, choices);
        if (side == PlayerSide.P1) _cityP1Submitted = true; else _cityP2Submitted = true;
        Debug.Log($"[GameFlowController] {side} 提交心仪城邦 [{string.Join(", ", choices)}]");

        if (_cityP1Submitted && _cityP2Submitted)
        {
            // 双方都已提交：结算已由提交管线完成，城邦已写入状态（无重合时为 None）
            cityStateSelectionPanel?.ShowResult(manager.CurrentCityState);
        }
        else
        {
            // 本地热座驱动：切换到另一方选择（逻辑层不依赖此顺序）
            cityStateSelectionPanel?.Show(side == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1);
        }
    }

    /// <summary>揭晓确认（UI 揭晓视图的继续按钮）→ 进入 P1 选棋子，其后流程顺序不变</summary>
    public void OnCityStateRevealConfirmed()
    {
        if (CurrentPhase != Phase.CityStateSelect) return;
        cityStateSelectionPanel?.Hide();
        CurrentPhase = Phase.P1Select;
        unitSelectionPanel?.Show(PlayerSide.P1);
        Debug.Log("[GameFlowController] 城邦选择完成，进入 P1 选棋子");
    }

    // ==========================================
    //  选棋子确认（UI_UnitSelection 调用）
    // ==========================================
    public void OnSelectionConfirmed(PlayerSide side, List<PieceData> selected)
    {
        _currentSelection.Clear();
        _currentSelection.AddRange(selected);
        _placed.Clear();
        for (int i = 0; i < _currentSelection.Count; i++) _placed.Add(false);
        _pendingIndex = -1;

        CurrentPhase = (side == PlayerSide.P1) ? Phase.P1Deploy : Phase.P2Deploy;
        unitSelectionPanel?.Hide();
        deploymentPanel?.Show(DeployingPlayer, _currentSelection, _placed);
        HighlightDeployZone();
        Debug.Log($"[GameFlowController] {side} 选棋子完成（{selected.Count} 个），进入部署阶段");
    }

    // ==========================================
    //  部署阶段：选中待放置棋子（UI_Deployment 调用）
    // ==========================================
    public void SelectPendingPiece(int index)
    {
        if (!IsDeploying) return;
        if (index < 0 || index >= _currentSelection.Count) return;
        if (_placed[index]) return;
        // 再次点击同一个 → 取消选中
        if (_pendingIndex == index)
        {
            _pendingIndex = -1;
            deploymentPanel?.Refresh(_currentSelection, _placed, _pendingIndex, AllPlaced());
            return;
        }
        _pendingIndex = index;
        deploymentPanel?.Refresh(_currentSelection, _placed, _pendingIndex, AllPlaced());
        HighlightDeployZone();
    }

    // ==========================================
    //  部署阶段：棋盘点击（BattleController 委托）
    // ==========================================
    public void HandleDeployClick(HexTile tile)
    {
        if (!IsDeploying || _pendingIndex < 0 || tile == null) return;
        HexCoord coord = tile.Coord;

        if (!IsInHalf(coord, DeployingPlayer))
        {
            Debug.Log("[GameFlowController] 该格不在己方半场");
            return;
        }
        if (PieceLayoutModel.Instance != null && PieceLayoutModel.Instance.IsOccupied(coord))
        {
            Debug.Log("[GameFlowController] 该格已被占用");
            return;
        }

        // 放置棋子
        var data = _currentSelection[_pendingIndex];
        PieceManager.Instance?.SpawnPieceById(data.id, coord, DeployingPlayer);
        _placed[_pendingIndex] = true;
        Debug.Log($"[GameFlowController] {DeployingPlayer} 部署 {data.displayName} @ {coord}");

        _pendingIndex = -1;
        bool allPlaced = AllPlaced();
        deploymentPanel?.Refresh(_currentSelection, _placed, _pendingIndex, allPlaced);
        if (allPlaced) ClearDeployHighlight();
    }

    // ==========================================
    //  部署确认（UI_Deployment 确认按钮）
    // ==========================================
    public void OnDeploymentConfirmed()
    {
        if (!AllPlaced()) return;
        ClearDeployHighlight();

        if (CurrentPhase == Phase.P1Deploy)
        {
            deploymentPanel?.Hide();
            CurrentPhase = Phase.P2Select;
            unitSelectionPanel?.Show(PlayerSide.P2);
            Debug.Log("[GameFlowController] P1 部署完成，切换 P2 选棋子");
        }
        else // P2Deploy
        {
            deploymentPanel?.Hide();
            CurrentPhase = Phase.Playing;
            Debug.Log("[GameFlowController] 双方部署完成，开始对战");
            TurnManager.Instance?.StartFirstTurn();
        }
    }

    // ==========================================
    //  辅助
    // ==========================================
    private bool AllPlaced()
    {
        foreach (var p in _placed) if (!p) return false;
        return true;
    }

    /// <summary>半场判定：P1 = r &gt;= 0，P2 = r &lt; 0</summary>
    private bool IsInHalf(HexCoord coord, PlayerSide side)
    {
        return side == PlayerSide.P1 ? coord.r >= 0 : coord.r < 0;
    }

    private void HighlightDeployZone()
    {
        var board = ChessBoardController.Instance;
        if (board == null) return;
        var allCoords = board.AllCoords;
        if (allCoords == null) return;

        var highlights = new Dictionary<HexCoord, HighlightType>();
        foreach (var coord in allCoords)
        {
            if (!IsInHalf(coord, DeployingPlayer)) continue;
            if (PieceLayoutModel.Instance != null && PieceLayoutModel.Instance.IsOccupied(coord)) continue;
            highlights[coord] = HighlightType.Move;
        }
        BattleController.Instance?.Model?.SetHighlights(highlights);
    }

    private void ClearDeployHighlight()
    {
        BattleController.Instance?.Model?.ClearHighlights();
    }
}
