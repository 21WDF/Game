using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 棋盘控制器（顶层协调 + 单例入口）
/// 职责：协调 <see cref="ChessBoardModel"/>（数据/算法）与 <see cref="ChessBoardView"/>（渲染），
///       对外暴露与旧 GridManager 兼容的查询 API，供 BattleController / InputHandler / GameSetup 等调用。
/// 挂载在场景中的 ChessBoardController GameObject 上（由旧 GridManager 重命名而来，GUID 保持不变）。
/// </summary>
public class ChessBoardController : MonoBehaviour
{
    public static ChessBoardController Instance { get; private set; }

    // ---- 配置参数（hexSize/initialRadius 已收拢到 GameConfig；hexTilePrefab 仍由 Inspector 配置）----
    [Header("棋盘配置")]
    public GameObject hexTilePrefab;           // HexTile Prefab

    // ---- MVC 分层 ----
    private ChessBoardModel _model;
    private ChessBoardView _view;
    private GameConfig _config;

    /// <summary>棋盘数据模型（供大招效果等外部类复用 RangeProvider 计算，如 FlameLanceUltimate）</summary>
    public ChessBoardModel Model => _model;

    // ---- 单例初始化 ----
    // Awake 仅设置单例 + 加载 GameConfig；_view 在 Start 中创建（确保各 Manager 单例就绪后再生成棋盘；
    // GameSetup 延迟 0.2s 布子，时序安全）。hexTilePrefab 在 Awake 已序列化可用。
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _model = new ChessBoardModel();

        // 加载全局配置（hexSize/initialRadius 收拢于此）
        _config = Resources.Load<GameConfig>("GameConfig");
        if (_config == null)
        {
            Debug.LogError("[ChessBoardController] GameConfig 加载失败！请确保 Assets/Game/Resources/GameConfig.asset 存在。", this);
            return;
        }
    }

    private void Start()
    {
        // 配置校验：GameConfig 应在 Awake 加载成功；hexTilePrefab 仍由 Inspector 配置
        if (_config == null) return; // Awake 已报错
        if (hexTilePrefab == null)
        {
            Debug.LogError("[ChessBoardController] hexTilePrefab 未配置！请在 Inspector 中拖入 HexTile Prefab。", this);
            return;
        }

        float hexSize = _config.hexSize;
        int initialRadius = _config.initialRadius;
        _view = new ChessBoardView(hexTilePrefab, hexSize, transform);
        _model.Initialize(initialRadius, hexSize);
        _view.GenerateView(_model.Coords);
        Debug.Log($"[ChessBoardController] 棋盘生成完毕，共 {_model.Coords.Count} 个格子");
    }

    // ==========================================
    //  坐标 → 格子（委托 View）
    // ==========================================
    public HexTile GetTile(HexCoord coord) => _view != null ? _view.GetTile(coord) : null;

    public HexTile GetTile(int q, int r) => _view != null ? _view.GetTile(new HexCoord(q, r)) : null;

    /// <summary>坐标 → 世界坐标（供棋子生成等模块定位使用）</summary>
    public Vector3 GetCellWorldPosition(HexCoord coord) => _view != null ? _view.GetCellWorldPosition(coord) : Vector3.zero;

    // ==========================================
    //  射线检测（输入/IO 层）
    //  Phase 2b：命中棋子（PieceView）时，通过其 Model.Coord 反查格子；
    //            占据权威在 PieceLayoutModel，本方法仅做"点击 → 格子"映射。
    // ==========================================
    public HexTile RaycastTile()
    {
        if (_view == null) return null;
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            // 如果射线命中了棋子（PieceView），通过其绑定的 Model.Coord 反查格子
            var pieceView = hit.collider.GetComponent<PieceView>();
            if (pieceView == null)
                pieceView = hit.collider.GetComponentInParent<PieceView>();
            if (pieceView != null && pieceView.Model != null)
                return _view.GetTile(pieceView.Model.Coord);

            // 否则查找命中对象上的 HexTile
            var tile = hit.collider.GetComponent<HexTile>();
            if (tile == null)
                tile = hit.collider.GetComponentInParent<HexTile>();
            return tile;
        }
        return null;
    }

    // ==========================================
    //  移动范围（委托 Model，坐标 → 格子映射）
    // ==========================================
    public List<HexTile> GetWalkableTiles(HexCoord from, int maxSteps)
    {
        if (_view == null) return new List<HexTile>();
        var coords = _model.GetWalkableCoords(from, maxSteps, IsBlocked);
        var result = new List<HexTile>(coords.Count);
        foreach (var c in coords)
        {
            var tile = _view.GetTile(c);
            if (tile != null) result.Add(tile);
        }
        return result;
    }

    // ==========================================
    //  攻击范围（委托 Model，坐标 → 格子映射）
    // ==========================================
    public List<HexTile> GetTilesInAttackRange(HexCoord from, int range)
    {
        if (_view == null) return new List<HexTile>();
        var coords = _model.GetCoordsInAttackRange(from, range);
        var result = new List<HexTile>(coords.Count);
        foreach (var c in coords)
        {
            var tile = _view.GetTile(c);
            if (tile != null) result.Add(tile);
        }
        return result;
    }

    // ==========================================
    //  可插拔 Provider 范围计算（坐标返回，供 BattleController 直接判断占据/高亮）
    //  · 配置内联于 PieceData（moveConfig / attackConfig：provider 类名 + JSON 参数 + baseRange）
    //  · effectiveRange 由调用方传入（含装备加成，即 PieceModel.MoveRange / AttackRange）
    // ==========================================

    /// <summary>通过 moveConfig 的 provider 计算可达坐标（不含 from 自身）。未知类名返回空列表。</summary>
    public List<HexCoord> GetReachableCoords(HexCoord from, PieceData.RangeConfig config, int effectiveRange)
    {
        if (_model == null || config == null) return new List<HexCoord>();
        IMoveRangeProvider provider = config.CreateMoveProvider();
        if (provider == null) return new List<HexCoord>();
        return provider.GetReachable(from, effectiveRange, IsBlocked, _model);
    }

    /// <summary>通过 attackConfig 的 provider 计算攻击覆盖坐标。
    /// target=null（高亮阶段）返回完整范围；target 非 null（执行阶段）返回实际攻击覆盖（含溅射/射线/扇形）。
    /// 未知类名返回空列表。</summary>
    public List<HexCoord> GetAttackZoneCoords(HexCoord from, PieceData.RangeConfig config, int effectiveRange, HexCoord? target)
    {
        if (_model == null || config == null) return new List<HexCoord>();
        IAttackRangeProvider provider = config.CreateAttackProvider();
        if (provider == null) return new List<HexCoord>();
        return provider.GetAttackZone(from, effectiveRange, target, _model);
    }

    // ==========================================
    //  寻路（委托 Model）
    // ==========================================
    public List<HexCoord> FindPath(HexCoord from, HexCoord to)
    {
        return _model.FindPath(from, to, IsBlocked);
    }

    /// <summary>沿途径点拼接寻路（手动路径选择）。委托 Model，占据判定同 FindPath。
    /// 总步数不超过 maxSteps，任一段不可达或超限返回 null。</summary>
    public List<HexCoord> FindPathWithWaypoints(HexCoord from, List<HexCoord> waypoints, HexCoord to, int maxSteps)
    {
        if (_model == null) return null;
        return _model.FindPathWithWaypoints(from, waypoints, to, maxSteps, IsBlocked);
    }

    // ==========================================
    //  按移动规则寻路（预览线与实际移动共用，保证路径一致）
    //  · 走 moveConfig 的 provider.FindPath（直线=射线 / 穿越=穿棋子射线 / 飞行=无视阻挡 BFS / 跳跃=直达）
    // ==========================================

    /// <summary>按棋子移动规则寻路：from→to 单段路径。不可达返回 null。
    /// 工厂失败（未知类名）回退 FreeMoveProvider（普通 BFS，与 FindPath 行为一致）。</summary>
    public List<HexCoord> FindPathByMoveRule(PieceData.RangeConfig config, HexCoord from, HexCoord to)
    {
        if (_model == null) return null;
        IMoveRangeProvider provider = config != null ? config.CreateMoveProvider() : null;
        if (provider == null) provider = new FreeMoveProvider();
        return provider.FindPath(from, to, IsBlocked, _model);
    }

    /// <summary>按棋子移动规则沿途经点拼接寻路：每段走 provider 路径；
    /// 总步数 = 各段步数之和，不超过 maxSteps；任一段不可达返回 null（拼接规则同 FindPathWithWaypoints）。</summary>
    public List<HexCoord> FindPathWithWaypointsByMoveRule(PieceData.RangeConfig config, HexCoord from, List<HexCoord> waypoints, HexCoord to, int maxSteps)
    {
        if (waypoints == null || waypoints.Count == 0)
            return FindPathByMoveRule(config, from, to);

        var full = new List<HexCoord> { from };
        HexCoord current = from;
        int totalSteps = 0;

        foreach (var wp in waypoints)
        {
            var seg = FindPathByMoveRule(config, current, wp);
            if (seg == null || seg.Count == 0) return null;
            for (int i = 1; i < seg.Count; i++) full.Add(seg[i]);
            totalSteps += seg.Count - 1;
            current = wp;
        }

        if (current != to)
        {
            var seg = FindPathByMoveRule(config, current, to);
            if (seg == null || seg.Count == 0) return null;
            for (int i = 1; i < seg.Count; i++) full.Add(seg[i]);
            totalSteps += seg.Count - 1;
        }

        if (totalSteps > maxSteps) return null;
        return full;
    }

    // ==========================================
    //  清除所有高亮（委托 View）
    // ==========================================
    public void ClearAllHighlights()
    {
        if (_view != null) _view.ClearAllHighlights();
    }

    // ==========================================
    //  路径预览（委托 View；InputHandler 调用，避免输入层持有 LineRenderer）
    // ==========================================
    /// <summary>显示路径预览线</summary>
    public void ShowPathPreview(List<HexCoord> path)
    {
        _view?.ShowPathPreview(path);
    }

    /// <summary>隐藏路径预览线</summary>
    public void HidePathPreview()
    {
        _view?.HidePathPreview();
    }

    // ==========================================
    //  棋盘增删（棋盘道具系统：扩展石 / 删除石）
    //  同步操作 Model（坐标集合）与 View（格子视图），参照 GenerateBoard 风格。
    // ==========================================
    /// <summary>坐标是否在棋盘内</summary>
    public bool Contains(HexCoord coord) => _model?.Contains(coord) ?? false;

    /// <summary>当前棋盘格子总数</summary>
    public int CoordCount => _model?.CoordCount ?? 0;

    /// <summary>棋盘所有格子坐标（只读视图；供 GridItemManager 遍历生成幽灵格/删除目标高亮）</summary>
    public IReadOnlyList<HexCoord> AllCoords => _model?.Coords;

    /// <summary>新增一个格子（扩展石）。已存在则返回 false。</summary>
    public bool AddTile(HexCoord coord)
    {
        if (_model == null || _view == null) return false;
        if (_model.Contains(coord)) return false;
        _model.AddCoord(coord);
        _view.CreateTile(coord);
        Debug.Log($"[ChessBoardController] 新增格子 {coord}（共 {_model.CoordCount} 个）");
        return true;
    }

    /// <summary>移除一个格子（删除石）。不存在或有棋子占据则返回 false。</summary>
    public bool RemoveTile(HexCoord coord)
    {
        if (_model == null || _view == null) return false;
        if (!_model.Contains(coord)) return false;
        if (PieceLayoutModel.Instance != null && PieceLayoutModel.Instance.IsOccupied(coord)) return false;
        _view.DestroyTile(coord);
        _model.RemoveCoord(coord);
        Debug.Log($"[ChessBoardController] 删除格子 {coord}（剩 {_model.CoordCount} 个）");
        return true;
    }

    // ==========================================
    //  幽灵格（棋盘道具系统：扩展石瞄准模式）
    //  仅操作 View（不入/出 Model）：在棋盘外可扩展位置生成临时 HexTile，
    //  供玩家点击选择扩展落点。正式化时由 AddTile 把坐标写入 Model（View 中已存在该实例，CreateTile 返回原实例）。
    //  取消时由 DestroyGhostTile 销毁；DestroyGhostTile 会保护已正式加入 Model 的格子不被误删。
    // ==========================================
    /// <summary>创建仅 View 的幽灵格（不入 Model，供扩展石瞄准用）。
    /// 若该坐标在 View 中已有格子则返回原实例。调用方负责后续高亮与销毁。</summary>
    public HexTile CreateGhostTile(HexCoord coord)
    {
        return _view != null ? _view.CreateTile(coord) : null;
    }

    /// <summary>销毁仅 View 的幽灵格（供扩展石取消/命中后清理未用的幽灵格）。
    /// 安全保护：若该坐标已正式加入 Model（被 AddTile 正式化），则不销毁。</summary>
    public void DestroyGhostTile(HexCoord coord)
    {
        if (_view == null) return;
        if (_model != null && _model.Contains(coord)) return; // 已正式化，保护
        _view.DestroyTile(coord);
    }

    // ==========================================
    //  占据判定（委托 PieceLayoutModel —— Phase 2a 起的占据权威）
    // ==========================================
    private bool IsBlocked(HexCoord coord)
    {
        return PieceLayoutModel.Instance?.IsOccupied(coord) ?? false;
    }
}
