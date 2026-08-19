using System.Collections.Generic;

/// <summary>
/// 圆形攻击范围 —— 以 from 为圆心、effectiveRange 为半径的所有格子（不含 from 自身）。
/// 单目标攻击：执行阶段（target 非 null）仅返回主目标，不产生溅射。
/// 无构造参数。
/// </summary>
public class CircleAttackProvider : IAttackRangeProvider
{
    public List<HexCoord> GetAttackZone(HexCoord from, int effectiveRange, HexCoord? target, ChessBoardModel model)
    {
        var result = new List<HexCoord>();

        // 执行阶段（target 非 null）：单目标攻击，仅返回主目标（不溅射）
        if (target.HasValue)
        {
            if (model.Contains(target.Value))
                result.Add(target.Value);
            return result;
        }

        // 高亮阶段（target null）：圆形范围内所有格子（不含 from 自身）
        foreach (var offset in HexCoord.AllCoordsInRadius(effectiveRange))
        {
            if (offset.q == 0 && offset.r == 0) continue;  // 排除自身
            var coord = new HexCoord(from.q + offset.q, from.r + offset.r);
            if (model.Contains(coord)) result.Add(coord);
        }
        return result;
    }
}
