using System;
using System.Collections.Generic;

/// <summary>
/// 跳格移动（仅偶数距离）—— 返回 effectiveRange 内到 from 六边形距离为偶数的所有空格。
/// 跳跃式：不受中间格阻挡（直接按 HexCoord.Distance 筛选，不做 BFS、不因棋子绕行）；
/// 距离判定用六边形距离（Distance），不是路径步数。
/// [PLACEHOLDER] 跳跃式高机动是否过强，待 playtest 验证；当前按「不受阻挡」实现。
/// 无构造参数（effectiveRange 由接口传入）。
/// </summary>
public class ParityMoveProvider : IMoveRangeProvider
{
    public List<HexCoord> GetReachable(HexCoord from, int effectiveRange, Func<HexCoord, bool> isBlocked, ChessBoardModel model)
    {
        var result = new List<HexCoord>();
        if (effectiveRange <= 0) return result;

        foreach (var offset in HexCoord.AllCoordsInRadius(effectiveRange))
        {
            if (offset.q == 0 && offset.r == 0) continue; // 排除 from 自身
            var coord = new HexCoord(from.q + offset.q, from.r + offset.r);
            if (!model.Contains(coord)) continue;
            if (coord.Distance(from) % 2 != 0) continue;          // 仅偶数六边形距离
            if (isBlocked != null && isBlocked(coord)) continue;  // 落点必须空格
            result.Add(coord);
        }
        return result;
    }

    /// <summary>路径 = 起点→终点直达（跳跃无中间路径，与高亮的"按距离筛选不绕行"一致）。
    /// 落点为空由 Move 高亮层保证。</summary>
    public List<HexCoord> FindPath(HexCoord from, HexCoord to, Func<HexCoord, bool> isBlocked, ChessBoardModel model)
    {
        if (from == to) return new List<HexCoord> { from };
        if (!model.Contains(to)) return null;
        return new List<HexCoord> { from, to };
    }
}
