using UnityEngine;

/// <summary>
/// 潮涌战意大招（自身增益型，ultimateConfig.targetMode=Self / requiresTarget=false，哥伦比亚）——
/// 提升以自身为中心周围 radius 格（含中心格=含自身）内己方棋子的攻击力，持续 duration 回合。
///
/// buff 走现有 TempBuffs 机制（PieceModel.GetTempBuffTotal("Attack") 已接入 EffectiveAttack，
/// TurnManager.TickTempBuffs 每次任一方回合开始递减，≤0 移除）——不另造一套。
/// 刷新不叠加：TempBuff.source 标记来源（本类名），再次释放时已有同来源 Attack buff 只重置剩余回合，数值不叠。
/// target 参数被忽略（自身增益；调用方传入 caster）。
/// 构造参数：radius（增益半径，含自身）、bonusAttack（攻击加成值）、duration（持续回合）。
/// </summary>
public class TideSurgeUltimate : IUltimateEffect
{
    private const string SourceMark = "TideSurgeUltimate";   // TempBuff 来源标记（刷新式查重）

    private readonly int _radius;        // 增益半径（以自身为圆心，含中心格）
    private readonly int _bonusAttack;   // 攻击力加成值
    private readonly int _duration;      // 持续回合

    public TideSurgeUltimate(int radius, int bonusAttack, int duration)
    {
        _radius = Mathf.Max(1, radius);
        _bonusAttack = bonusAttack;
        _duration = Mathf.Max(1, duration);
    }

    public void Execute(PieceModel caster, PieceModel target)
    {
        if (caster == null || caster.Data == null) return;
        if (ChessBoardController.Instance == null) return;

        // 以自身为中心 radius 格圆形（GetTilesInAttackRange 语义含中心格 → 含哥伦比亚自身）
        int buffed = 0, refreshed = 0;
        foreach (var tile in ChessBoardController.Instance.GetTilesInAttackRange(caster.Coord, _radius))
        {
            var ally = PieceLayoutModel.Instance?.GetPieceAt(tile.Coord);
            if (ally == null || ally.IsDead || ally.Owner != caster.Owner) continue;

            // 刷新不叠加：已有本来源的 Attack buff → 只重置剩余回合；否则新增一条
            PieceModel.TempBuff existing = null;
            foreach (var b in ally.TempBuffs)
                if (b.statType == "Attack" && b.source == SourceMark) { existing = b; break; }

            if (existing != null)
            {
                existing.amount = _bonusAttack;      // 数值以最新配置为准（防 playtest 中途改参数残留旧值）
                existing.turnsRemaining = _duration;
                refreshed++;
            }
            else
            {
                ally.TempBuffs.Add(new PieceModel.TempBuff
                {
                    statType = "Attack",
                    amount = _bonusAttack,
                    turnsRemaining = _duration,
                    source = SourceMark,
                });
                buffed++;
            }
        }

        Debug.Log($"[TideSurge] {caster.Data.displayName} 释放潮涌战意：范围 {_radius} 格，" +
                  $"新增 {buffed} / 刷新 {refreshed} 个己方攻击 +{_bonusAttack}（{_duration} 回合）");
    }
}
