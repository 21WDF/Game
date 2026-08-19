using System;
using System.Collections.Generic;

/// <summary>
/// 自由移动（BFS）—— 现有移动范围的默认行为。
/// 从 from 出发广度优先搜索 effectiveRange 步内所有未被阻挡的格子。
/// 逻辑与 ChessBoardModel.GetWalkableCoords 一致（BFS 搬运至此封装）。
/// 无构造参数（effectiveRange 由接口传入）。
/// </summary>
public class FreeMoveProvider : IMoveRangeProvider
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
                if (isBlocked != null && isBlocked(neighbor)) continue;

                visited.Add(neighbor);
                result.Add(neighbor);
                frontier.Enqueue((neighbor, steps + 1));
            }
        }
        return result;
    }
}
