using System;
using System.Collections.Generic;

/// <summary>
/// 飞行移动（BFS 无视阻挡）—— 可飞越棋子，落点必须空格。
/// 与 FreeMoveProvider 的区别：FreeMove 遇棋子即停止扩散（该格不进入后续搜索）；
/// FlyMove 把被占格作为中转继续扩散（isBlocked 格仍标记已访问并入队，只是不加入返回结果）。
/// 边界：effectiveRange &lt;= 0 返回空；isBlocked 为 null 视为无阻挡（所有可达格都加入结果）。
/// 无构造参数（effectiveRange 由接口传入）。
/// </summary>
public class FlyMoveProvider : IMoveRangeProvider
{
    public List<HexCoord> GetReachable(HexCoord from, int effectiveRange, Func<HexCoord, bool> isBlocked, ChessBoardModel model)
    {
        var result = new List<HexCoord>();
        if (effectiveRange <= 0) return result;

        var visited = new HashSet<HexCoord> { from };
        var frontier = new Queue<(HexCoord coord, int steps)>();
        frontier.Enqueue((from, 0));

        while (frontier.Count > 0)
        {
            var (current, steps) = frontier.Dequeue();
            if (steps >= effectiveRange) continue;

            for (int i = 0; i < 6; i++)
            {
                var neighbor = current.Neighbor(i);
                if (visited.Contains(neighbor)) continue;
                if (!model.Contains(neighbor)) continue;

                visited.Add(neighbor); // 无论是否阻挡都标记已访问，防止重复访问

                bool blocked = isBlocked != null && isBlocked(neighbor);
                if (!blocked)
                    result.Add(neighbor);   // 落点只收空格
                frontier.Enqueue((neighbor, steps + 1)); // 被占格作为中转继续扩散（飞越棋子）
            }
        }
        return result;
    }

    /// <summary>路径 = BFS 最短路径且无视中间棋子（可飞越，与高亮扩散规则一致）。
    /// 被飞越的被占格不进入路径（与 StraightPassProvider 口径一致）——动画直线滑过、
    /// 占据表只更新空格落点，不覆盖任何敌方棋子的占据记录；落点为空由 Move 高亮层保证。</summary>
    public List<HexCoord> FindPath(HexCoord from, HexCoord to, Func<HexCoord, bool> isBlocked, ChessBoardModel model)
    {
        if (from == to) return new List<HexCoord> { from };
        if (!model.Contains(to)) return null;

        var cameFrom = new Dictionary<HexCoord, HexCoord> { [from] = from };
        var frontier = new Queue<HexCoord>();
        frontier.Enqueue(from);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            for (int i = 0; i < 6; i++)
            {
                var next = current.Neighbor(i);
                if (cameFrom.ContainsKey(next)) continue;
                if (!model.Contains(next)) continue;

                cameFrom[next] = current;   // 不检查 isBlocked —— 飞越棋子
                frontier.Enqueue(next);
                if (next == to) { frontier.Clear(); break; }
            }
        }

        if (!cameFrom.ContainsKey(to)) return null;

        // 回溯路径：被占中间格不入路径（飞越滑过）；终点保留（允许被占，与默认 BFS 口径一致）
        var path = new List<HexCoord>();
        var cur = to;
        while (cur != from)
        {
            if (cur.Equals(to) || isBlocked == null || !isBlocked(cur))
                path.Add(cur);
            cur = cameFrom[cur];
        }
        path.Add(from);
        path.Reverse();
        return path;
    }
}
