using System.Collections.Generic;

/// <summary>
/// 最小-最大攻击范围（环形）—— 外圈减内圈：AllCoordsInRadius(maxRange) 减去 AllCoordsInRadius(minRange-1)。
/// 用于弓手等近身无法攻击的单位（minRange=2, maxRange=5）。
/// 单目标攻击：执行阶段（target 非 null）仅返回主目标，不产生溅射。
/// 构造参数：minRange（内圈半径，不含；从 jsonParams 解析）。maxRange 由 effectiveRange 传入。
/// </summary>
public class MinMaxAttackProvider : IAttackRangeProvider
{
    private readonly int _minRange;

    public MinMaxAttackProvider(int minRange)
    {
        _minRange = minRange;
    }

    public List<HexCoord> GetAttackZone(HexCoord from, int effectiveRange, HexCoord? target, ChessBoardModel model)
    {
        // 执行阶段（target 非 null）：单目标攻击，仅返回主目标（不溅射）
        if (target.HasValue)
        {
            if (model.Contains(target.Value))
                return new List<HexCoord> { target.Value };
            return new List<HexCoord>();
        }

        // 高亮阶段：环形范围（外圈减内圈）
        var outer = new HashSet<HexCoord>();
        foreach (var offset in HexCoord.AllCoordsInRadius(effectiveRange))
        {
            var coord = new HexCoord(from.q + offset.q, from.r + offset.r);
            if (model.Contains(coord)) outer.Add(coord);
        }

        // 内圈：minRange-1 半径内所有格子（含 from 自身）
        if (_minRange > 0)
        {
            foreach (var offset in HexCoord.AllCoordsInRadius(_minRange - 1))
            {
                var coord = new HexCoord(from.q + offset.q, from.r + offset.r);
                outer.Remove(coord);  // 直接从外圈移除（差集）
            }
        }

        return new List<HexCoord>(outer);
    }
}
