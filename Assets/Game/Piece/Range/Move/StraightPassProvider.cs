using System;
using System.Collections.Generic;

/// <summary>
/// 直线穿越移动（射线无视阻挡）—— 6 方向各发一条射线，遇棋子穿过不停，遇棋盘边界停止；落点只含空格。
/// 与 StraightMoveProvider 的区别：StraightMove 遇棋子即 break 停止该方向；
/// StraightPass 遇棋子跳过该格（不加入结果）但射线继续延伸。
/// 射线长度 = effectiveRange。无构造参数（effectiveRange 由接口传入）。
/// </summary>
public class StraightPassProvider : IMoveRangeProvider
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
                if (!model.Contains(current)) break;                    // 走出棋盘 → 停止该方向
                if (isBlocked != null && isBlocked(current)) continue;  // 穿过棋子：不加入结果，射线继续
                result.Add(current);
            }
        }
        return result;
    }

    /// <summary>路径 = 沿直线穿过棋子（与高亮射线一致）。被占中间格不进入路径
    /// （不逐格占据敌方格子，动画直接滑过、占据表只更新空格落点），射线继续延伸；
    /// to 不在 from 的 6 向射线上返回 null（不绕行）。</summary>
    public List<HexCoord> FindPath(HexCoord from, HexCoord to, Func<HexCoord, bool> isBlocked, ChessBoardModel model)
    {
        if (from == to) return new List<HexCoord> { from };
        int dir = StraightMoveProvider.RayDirection(from, to);
        if (dir < 0) return null; // 不在同一直线上 → 本规则不可达

        var path = new List<HexCoord> { from };
        HexCoord cur = from;
        while (cur != to)
        {
            cur = cur.Neighbor(dir);
            if (!model.Contains(cur)) return null;
            if (cur != to && isBlocked != null && isBlocked(cur)) continue; // 穿过棋子：不入路径，继续延伸
            path.Add(cur);
        }
        return path;
    }
}
