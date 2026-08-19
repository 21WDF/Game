using System.Collections.Generic;

/// <summary>
/// 环切线攻击（横向 3 格）—— 以攻击者 from 为圆心，命中目标格 + 目标格在同一环上的左右 2 个邻居。
/// 「同环邻居」= target 的 6 个邻居中，到 from 距离仍为 d（= Distance(from, target)）的恰好 2 个
/// （环切线方向；判定必须用 HexCoord.Distance，不是简单相邻关系，也不是到 target 的距离）。
/// · target=null（高亮阶段）：返回 effectiveRange 半径内的格子（不含 from）——即可选目标位（对标 AreaAttackProvider）
/// · target 非 null（执行阶段）：返回 target + 2 个同环邻居（棋盘外的邻居剔除；d==0 时仅 target 自身）
/// 主要供后续大招（菲林斯/雷电将军「横向 3 格」）复用几何计算。
/// 无构造参数（effectiveRange 由接口传入）。
/// </summary>
public class RingTangentProvider : IAttackRangeProvider
{
    public List<HexCoord> GetAttackZone(HexCoord from, int effectiveRange, HexCoord? target, ChessBoardModel model)
    {
        var result = new List<HexCoord>();
        if (effectiveRange <= 0) return result;

        if (!target.HasValue)
        {
            // 高亮阶段：effectiveRange 内的可选目标位（不含 from 自身）
            foreach (var offset in HexCoord.AllCoordsInRadius(effectiveRange))
            {
                if (offset.q == 0 && offset.r == 0) continue;
                var coord = new HexCoord(from.q + offset.q, from.r + offset.r);
                if (model.Contains(coord)) result.Add(coord);
            }
            return result;
        }

        // 执行阶段：target 本身 + 同环邻居（到 from 距离不变的那 2 个）
        var t = target.Value;
        if (model.Contains(t)) result.Add(t);
        int d = from.Distance(t);
        if (d <= 0) return result; // 第 0 环只有中心一格，无同环邻居

        for (int i = 0; i < 6; i++)
        {
            var neighbor = t.Neighbor(i);
            if (from.Distance(neighbor) != d) continue; // 只收同环（切线方向）邻居
            if (!model.Contains(neighbor)) continue;    // 棋盘外的邻居剔除
            if (neighbor.Equals(t) || result.Contains(neighbor)) continue; // 防重
            result.Add(neighbor);
        }
        return result;
    }
}
