using System;
using System.Collections.Generic;

/// <summary>
/// 直线移动 —— 6 方向各发一条射线，遇到阻挡或达到 effectiveRange 停止。
/// 射线上的空格汇总为可达结果集。每次移动只能选一个方向（直线行进，不能拐弯）。
/// 无构造参数（effectiveRange 由接口传入，作为射线最大长度）。
/// </summary>
public class StraightMoveProvider : IMoveRangeProvider
{
    public List<HexCoord> GetReachable(HexCoord from, int effectiveRange, Func<HexCoord, bool> isBlocked, ChessBoardModel model)
    {
        var result = new List<HexCoord>();
        if (effectiveRange <= 0) return result;

        for (int dir = 0; dir < 6; dir++)
        {
            HexCoord current = from;
            for (int step = 0; step < effectiveRange; step++)
            {
                current = current.Neighbor(dir);
                if (!model.Contains(current)) break;          // 走出棋盘
                if (isBlocked != null && isBlocked(current)) break;  // 遇到阻挡
                result.Add(current);
            }
        }
        return result;
    }

    /// <summary>路径 = 一条直线（与高亮射线一致）。to 必须在 from 的 6 向射线上且中间无棋子（本规则遇阻不可达），
    /// 否则返回 null（不绕行——直线移动不允许 BFS 弧线）。</summary>
    public List<HexCoord> FindPath(HexCoord from, HexCoord to, Func<HexCoord, bool> isBlocked, ChessBoardModel model)
    {
        if (from == to) return new List<HexCoord> { from };
        int dir = RayDirection(from, to);
        if (dir < 0) return null; // 不在同一直线上 → 本规则不可达

        var path = new List<HexCoord> { from };
        HexCoord cur = from;
        while (cur != to)
        {
            cur = cur.Neighbor(dir);
            if (!model.Contains(cur)) return null;
            if (cur != to && isBlocked != null && isBlocked(cur)) return null; // 直线规则：遇棋子不可过
            path.Add(cur);
        }
        return path;
    }

    /// <summary>返回 from→to 的直线方向索引（0~5）；不在同一直线上返回 -1</summary>
    internal static int RayDirection(HexCoord from, HexCoord to)
    {
        int dq = to.q - from.q, dr = to.r - from.r;
        for (int i = 0; i < 6; i++)
        {
            var d = HexCoord.Directions[i];
            int k = d.q != 0 ? dq / d.q : (dq == 0 ? dr / d.r : 0);
            if (k <= 0) continue;
            if (dq == k * d.q && dr == k * d.r) return i;
        }
        return -1;
    }
}
