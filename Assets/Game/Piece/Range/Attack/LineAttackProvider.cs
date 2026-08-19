using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 直线攻击 —— 沿 from→target 方向一条射线，延伸 effectiveRange（length）格。
/// · target=null（高亮阶段）：6 条 hex 射线并集（每个方向各延伸 length 格）
/// · target 非 null（执行阶段）：沿最接近 target 方向的 hex 射线延伸 length 格
/// 无构造参数。length 由 effectiveRange 传入。
/// </summary>
public class LineAttackProvider : IAttackRangeProvider
{
    public List<HexCoord> GetAttackZone(HexCoord from, int effectiveRange, HexCoord? target, ChessBoardModel model)
    {
        var result = new List<HexCoord>();
        if (effectiveRange <= 0) return result;

        if (!target.HasValue)
        {
            // 高亮阶段：6 方向射线并集
            for (int dir = 0; dir < 6; dir++)
                AppendRay(result, from, dir, effectiveRange, model);
        }
        else
        {
            // 执行阶段：沿最接近 target 方向的 hex 射线
            int dir = ClosestDirection(from, target.Value);
            AppendRay(result, from, dir, effectiveRange, model);
        }

        return result;
    }

    /// <summary>沿 dir 方向延伸 maxSteps 格，收集棋盘内的格子（不检查阻挡——攻击可穿过空格打到后方）。
    /// public 供直线型大招复用（如 FlameLanceUltimate 的射线穿透）。</summary>
    public static void AppendRay(List<HexCoord> result, HexCoord from, int dir, int maxSteps, ChessBoardModel model)
    {
        HexCoord current = from;
        for (int step = 0; step < maxSteps; step++)
        {
            current = current.Neighbor(dir);
            if (!model.Contains(current)) break;
            result.Add(current);
        }
    }

    /// <summary>找到最接近 from→to 方向的 hex 方向索引（用点积最大值）。
    /// public 供直线型大招复用（点击格 → 归并到最近 hex 方向）。</summary>
    public static int ClosestDirection(HexCoord from, HexCoord to)
    {
        int dq = to.q - from.q;
        int dr = to.r - from.r;

        int bestDir = 0;
        int bestDot = int.MinValue;
        for (int i = 0; i < 6; i++)
        {
            var d = HexCoord.Directions[i];
            int dot = dq * d.q + dr * d.r;
            if (dot > bestDot)
            {
                bestDot = dot;
                bestDir = i;
            }
        }
        return bestDir;
    }
}
