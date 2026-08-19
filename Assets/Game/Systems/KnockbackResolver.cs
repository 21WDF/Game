using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 击退解析器（超载反应）—— 纯静态工具类，参照 DamageCalculator 风格。
///
/// 几何模型（立方坐标 q+r+s=0）：
///   dir = defender - attacker（float 向量）
///   逐格推进：step i 的浮点坐标 = defender + dir * (i / dist)
///   通过 cube_round 舍入到整数格子，检测二义性时生成 1~2 个候选
///
/// 二义性处理：
///   当浮点坐标恰好落在两个六边形边界上时，cube_round 的主候选与某个邻居等距。
///   检测方式：cube_round 得到主候选 → 检查 6 邻居是否有 cube 距离等距者 →
///   有则生成 2 候选 → 过滤合法后随机选一；合法候选 = 0 时该步击退失败。
///
/// 多格击退（k ≥ 2）：逐格推进，每步独立检测合法性，遇阻即停。
/// </summary>
public static class KnockbackResolver
{
    /// <summary>
    /// 计算击退目标坐标。
    /// 返回 null 表示击退完全失败（受击者留在原位）。
    /// 返回非 null 表示受击者应被传送到的坐标。
    /// </summary>
    /// <param name="attacker">攻击者坐标</param>
    /// <param name="defender">受击者坐标</param>
    /// <param name="distance">击退格数（≥1）</param>
    public static HexCoord? Resolve(HexCoord attacker, HexCoord defender, int distance)
    {
        if (distance <= 0) return null;

        int dist = defender.Distance(attacker);
        if (dist == 0)
        {
            Debug.Log("[KnockbackResolver] 攻击者与受击者同格，无法确定击退方向");
            return null;
        }

        // 方向向量（float cube 坐标）
        Vector3 dir = new Vector3(
            defender.q - attacker.q,
            defender.r - attacker.r,
            defender.s - attacker.s
        );
        // 单位方向：每步推进 1 格的浮点增量
        Vector3 unitDir = dir / dist;

        Debug.Log($"[KnockbackResolver] 击退方向 {dir}，距离 {dist}，击退 {distance} 格");

        HexCoord? lastValid = null;

        for (int step = 1; step <= distance; step++)
        {
            // 步骤 i 的浮点坐标 = defender + unitDir * step
            Vector3 floatPos = new Vector3(
                defender.q + unitDir.x * step,
                defender.r + unitDir.y * step,
                defender.s + unitDir.z * step
            );

            // 获取候选格子（1~2 个，二义性时 2 个）
            var candidates = GetKnockbackCandidates(floatPos);

            // 过滤合法候选
            var validCandidates = new List<HexCoord>(2);
            foreach (var c in candidates)
            {
                if (IsValidKnockbackTarget(c))
                    validCandidates.Add(c);
            }

            if (validCandidates.Count == 0)
            {
                Debug.Log($"[KnockbackResolver] 步骤 {step} 被阻挡（候选 {candidates.Count} 个均非法），击退停止");
                break;
            }

            // 随机选择一个合法候选
            int pick = validCandidates.Count > 1 ? Random.Range(0, validCandidates.Count) : 0;
            lastValid = validCandidates[pick];

            string candStr = string.Join(", ", candidates);
            Debug.Log($"[KnockbackResolver] 步骤 {step}：候选 [{candStr}]，合法 {validCandidates.Count} 个，选择 {lastValid}");
        }

        if (lastValid.HasValue)
            Debug.Log($"[KnockbackResolver] 击退完成 → {lastValid}");
        else
            Debug.Log("[KnockbackResolver] 击退完全失败，受击者留在原位");

        return lastValid;
    }

    // ==========================================
    //  内部子方法
    // ==========================================

    /// <summary>
    /// 获取浮点坐标对应的候选格子（1~2 个）。
    /// 先用 cube_round 得到主候选，再检查 6 邻居是否有等距者（二义性）。
    /// </summary>
    private static List<HexCoord> GetKnockbackCandidates(Vector3 f)
    {
        var result = new List<HexCoord>(2);

        HexCoord primary = CubeRound(f);
        result.Add(primary);

        float primaryDist = CubeDistance(primary, f);

        // 检查 6 邻居是否有等距者
        for (int i = 0; i < 6; i++)
        {
            HexCoord neighbor = primary.Neighbor(i);
            float neighborDist = CubeDistance(neighbor, f);

            if (Mathf.Approximately(neighborDist, primaryDist) && neighbor != primary)
            {
                result.Add(neighbor);
            }
        }

        return result;
    }

    /// <summary>检查坐标是否为合法击退目标：在棋盘内 + 未被占据</summary>
    private static bool IsValidKnockbackTarget(HexCoord coord)
    {
        if (ChessBoardController.Instance == null) return false;
        if (!ChessBoardController.Instance.Contains(coord)) return false;
        if (PieceLayoutModel.Instance != null && PieceLayoutModel.Instance.IsOccupied(coord)) return false;
        return true;
    }

    /// <summary>
    /// 标准 cube_round：将浮点立方坐标舍入到最近整数格子。
    /// 优先修正误差最大的坐标，用另外两个坐标反推（维持 q+r+s=0）。
    /// </summary>
    private static HexCoord CubeRound(Vector3 f)
    {
        int rq = Mathf.RoundToInt(f.x);
        int rr = Mathf.RoundToInt(f.y);
        int rs = Mathf.RoundToInt(f.z);

        float qDiff = Mathf.Abs(rq - f.x);
        float rDiff = Mathf.Abs(rr - f.y);
        float sDiff = Mathf.Abs(rs - f.z);

        if (qDiff > rDiff && qDiff > sDiff)
            rq = -rr - rs;
        else if (rDiff > sDiff)
            rr = -rq - rs;
        else
            rs = -rq - rr;

        return new HexCoord(rq, rr);
    }

    /// <summary>整数格子与浮点坐标的 cube 距离（max of abs diffs）</summary>
    private static float CubeDistance(HexCoord h, Vector3 f)
    {
        return Mathf.Max(
            Mathf.Abs(h.q - f.x),
            Mathf.Abs(h.r - f.y),
            Mathf.Abs(h.s - f.z)
        );
    }
}
