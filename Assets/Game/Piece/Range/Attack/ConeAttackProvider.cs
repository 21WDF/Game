using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 扇形攻击 —— 面向 target 方向，angle/2 度开角的扇形，深度 effectiveRange（length）格。
/// · target=null（高亮阶段）：6 方向扇形并集（每个 hex 方向各一个 angle/2 扇形）
/// · target 非 null（执行阶段）：面向 target 方向的扇形
/// 构造参数：angle（度数，从 jsonParams 解析）。length 由 effectiveRange 传入。
///
/// 角度计算基于平顶 Hex 世界坐标（hexSize 不影响角度，消去）：
///   worldX = 1.5 * q, worldZ = sqrt(3)/2 * q + sqrt(3) * r
/// 6 个 hex 方向的世界角度：NE=30°, E=330°(-30°), SE=270°(-90°), SW=210°, W=150°, NW=90°
/// </summary>
public class ConeAttackProvider : IAttackRangeProvider
{
    private readonly int _angle;

    // 6 个 hex 方向的世界角度（与 HexCoord.Directions 顺序一致：NE, E, SE, SW, W, NW）
    private static readonly float[] HexDirAngles = { 30f, 330f, 270f, 210f, 150f, 90f };

    public ConeAttackProvider(int angle)
    {
        _angle = angle;
    }

    public List<HexCoord> GetAttackZone(HexCoord from, int effectiveRange, HexCoord? target, ChessBoardModel model)
    {
        var result = new List<HexCoord>();
        if (effectiveRange <= 0) return result;

        float halfAngle = _angle / 2f;

        if (!target.HasValue)
        {
            // 高亮阶段：6 方向扇形并集——格子角度落在任一 hex 方向的 ±halfAngle 内即纳入
            foreach (var offset in HexCoord.AllCoordsInRadius(effectiveRange))
            {
                if (offset.q == 0 && offset.r == 0) continue;  // 排除自身
                var coord = new HexCoord(from.q + offset.q, from.r + offset.r);
                if (!model.Contains(coord)) continue;

                float tileAngle = HexAngle(offset);
                for (int i = 0; i < 6; i++)
                {
                    if (AngleDiff(tileAngle, HexDirAngles[i]) <= halfAngle)
                    {
                        result.Add(coord);
                        break;  // 已纳入，无需检查其他方向
                    }
                }
            }
        }
        else
        {
            // 执行阶段：面向 target 方向的扇形
            float targetAngle = HexAngle(new HexCoord(
                target.Value.q - from.q, target.Value.r - from.r));

            foreach (var offset in HexCoord.AllCoordsInRadius(effectiveRange))
            {
                if (offset.q == 0 && offset.r == 0) continue;  // 排除自身
                var coord = new HexCoord(from.q + offset.q, from.r + offset.r);
                if (!model.Contains(coord)) continue;

                float tileAngle = HexAngle(offset);
                if (AngleDiff(tileAngle, targetAngle) <= halfAngle)
                    result.Add(coord);
            }
        }

        return result;
    }

    /// <summary>计算 hex 偏移量在世界空间中的角度（度）。hexSize 消去，直接用相对偏移。</summary>
    private static float HexAngle(HexCoord offset)
    {
        float dx = 1.5f * offset.q;
        float dz = Mathf.Sqrt(3f) / 2f * offset.q + Mathf.Sqrt(3f) * offset.r;
        return Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
    }

    /// <summary>两个角度的最小差值（度，考虑 360° 环绕）</summary>
    private static float AngleDiff(float a, float b)
    {
        float diff = Mathf.Abs(a - b) % 360f;
        if (diff > 180f) diff = 360f - diff;
        return diff;
    }
}
