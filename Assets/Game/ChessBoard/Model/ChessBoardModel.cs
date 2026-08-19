using System;
using System.Collections.Generic;

/// <summary>
/// 棋盘数据模型（纯数据 + 网格算法，不依赖 Unity 渲染）
/// 职责：管理棋盘上所有存在的格子坐标，提供基于坐标的移动范围 / 攻击范围 / 寻路算法。
/// 不持有任何 GameObject / HexTile 引用；格子的占据（阻挡）状态通过 <paramref name="isBlocked"/>
/// 谓词由外部注入，以便 Phase 2 由 PieceLayoutModel 提供权威占据数据。
/// </summary>
public class ChessBoardModel
{
    // ---- 配置 ----
    public int Radius { get; private set; }
    public float HexSize { get; private set; }

    // ---- 网格数据：所有存在的格子坐标 ----
    private readonly HashSet<HexCoord> _coords = new();
    private readonly List<HexCoord> _coordList = new();

    /// <summary>所有格子坐标的只读视图（供 View 实例化时遍历）</summary>
    public IReadOnlyList<HexCoord> Coords => _coordList;

    /// <summary>棋盘初始化完成时触发（View 可订阅以生成格子）</summary>
    public event Action OnBoardInitialized;

    // ==========================================
    //  初始化
    // ==========================================
    /// <summary>初始化棋盘：根据半径生成所有坐标</summary>
    public void Initialize(int radius, float hexSize)
    {
        Radius = radius;
        HexSize = hexSize;

        _coords.Clear();
        _coordList.Clear();

        foreach (var c in HexCoord.AllCoordsInRadius(radius))
        {
            if (_coords.Add(c))
                _coordList.Add(c);
        }

        OnBoardInitialized?.Invoke();
    }

    /// <summary>坐标是否在棋盘内</summary>
    public bool Contains(HexCoord coord) => _coords.Contains(coord);

    // ==========================================
    //  单格增删（棋盘道具系统：扩展石 / 删除石）
    //  注意：_coords 与 _coordList 必须同步维护。
    // ==========================================
    /// <summary>新增一个格子坐标（已存在则忽略）</summary>
    public void AddCoord(HexCoord coord)
    {
        if (_coords.Add(coord)) _coordList.Add(coord);
    }

    /// <summary>移除一个格子坐标（不存在则忽略）</summary>
    public void RemoveCoord(HexCoord coord)
    {
        if (_coords.Remove(coord)) _coordList.Remove(coord);
    }

    /// <summary>当前棋盘格子总数</summary>
    public int CoordCount => _coordList.Count;

    // ==========================================
    //  移动范围（BFS — 广度优先）
    //  返回从 from 出发、最多 maxSteps 步、未被阻挡的可达坐标（不含 from 自身）
    // ==========================================
    public List<HexCoord> GetWalkableCoords(HexCoord from, int maxSteps, Func<HexCoord, bool> isBlocked)
    {
        var result = new List<HexCoord>();
        var visited = new HashSet<HexCoord> { from };
        var frontier = new Queue<(HexCoord coord, int steps)>();
        frontier.Enqueue((from, 0));

        while (frontier.Count > 0)
        {
            var (current, steps) = frontier.Dequeue();
            if (steps >= maxSteps) continue;

            for (int i = 0; i < 6; i++)
            {
                var neighbor = current.Neighbor(i);

                if (visited.Contains(neighbor)) continue;
                if (!Contains(neighbor)) continue;                              // 棋盘边界外
                if (isBlocked != null && isBlocked(neighbor)) continue;        // 不能走到/穿过有棋子的格子

                visited.Add(neighbor);
                result.Add(neighbor);
                frontier.Enqueue((neighbor, steps + 1));
            }
        }
        return result;
    }

    // ==========================================
    //  攻击范围（圆形）
    //  返回 from 周围 range 内的所有坐标（含 from 自身，由调用方过滤目标）
    // ==========================================
    public List<HexCoord> GetCoordsInAttackRange(HexCoord from, int range)
    {
        var result = new List<HexCoord>();
        var offsets = HexCoord.AllCoordsInRadius(range);
        foreach (var offset in offsets)
        {
            var target = new HexCoord(from.q + offset.q, from.r + offset.r);
            if (Contains(target))
                result.Add(target);
        }
        return result;
    }

    // ==========================================
    //  寻路（BFS）
    //  返回从 from 到 to 的坐标路径；不可达返回 null
    // ==========================================
    public List<HexCoord> FindPath(HexCoord from, HexCoord to, Func<HexCoord, bool> isBlocked)
    {
        if (from == to) return new List<HexCoord> { from };

        var cameFrom = new Dictionary<HexCoord, HexCoord>();
        var frontier = new Queue<HexCoord>();
        frontier.Enqueue(from);
        cameFrom[from] = from;

        bool found = false;
        while (frontier.Count > 0 && !found)
        {
            var current = frontier.Dequeue();

            for (int i = 0; i < 6; i++)
            {
                var next = current.Neighbor(i);
                if (cameFrom.ContainsKey(next)) continue;
                if (!Contains(next)) continue;
                // 终点允许被占据（攻击目标），路径中间不允许
                bool blocked = isBlocked != null && isBlocked(next);
                if (blocked && next != to) continue;

                cameFrom[next] = current;
                frontier.Enqueue(next);

                if (next == to)
                {
                    found = true;
                    break;
                }
            }
        }

        if (!cameFrom.ContainsKey(to)) return null;

        // 回溯路径
        var path = new List<HexCoord> { to };
        var cur = to;
        while (cur != from)
        {
            cur = cameFrom[cur];
            path.Add(cur);
        }
        path.Reverse();
        return path;
    }

    // ==========================================
    //  沿途经点拼接寻路（手动路径选择）
    //  路径：from → waypoints[0] → waypoints[1] → ... → waypoints[N-1] → to
    //  每段用 FindPath 计算；拼接时去除重复衔接点（衔接点即途经点本身，不重复计数）。
    //  总步数 = 各段步数之和，不超过 maxSteps，否则返回 null。
    //  任一段不可达返回 null。
    // ==========================================
    public List<HexCoord> FindPathWithWaypoints(HexCoord from, List<HexCoord> waypoints, HexCoord to, int maxSteps, Func<HexCoord, bool> isBlocked)
    {
        // 无途经点 → 退化为普通寻路
        if (waypoints == null || waypoints.Count == 0)
            return FindPath(from, to, isBlocked);

        var full = new List<HexCoord> { from };
        HexCoord current = from;
        int totalSteps = 0;

        // 依次走完每个途经点
        foreach (var wp in waypoints)
        {
            var seg = FindPath(current, wp, isBlocked);
            if (seg == null || seg.Count == 0) return null;   // 该段不可达
            // seg[0] == current，跳过避免衔接点重复
            for (int i = 1; i < seg.Count; i++) full.Add(seg[i]);
            totalSteps += seg.Count - 1;
            current = wp;
        }

        // 末段：最后一个途经点 → to（若 to 即末途经点则跳过）
        if (current != to)
        {
            var seg = FindPath(current, to, isBlocked);
            if (seg == null || seg.Count == 0) return null;
            for (int i = 1; i < seg.Count; i++) full.Add(seg[i]);
            totalSteps += seg.Count - 1;
        }

        // 总步数校验
        if (totalSteps > maxSteps) return null;
        return full;
    }
}
