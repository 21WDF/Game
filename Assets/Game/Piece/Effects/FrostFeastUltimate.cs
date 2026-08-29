using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 极寒飨宴大招（自身增益/复合型，ultimateConfig.targetMode=Self，爱可菲）——
/// 以自身为中心的三段复合效果：
///   1. 即时段：周围 radius（1）格内敌方受物理伤害（CalculateWithElement 含冰元素反应倍率；
///      附着 gauge 2 同大招募发量；金币双边；致死销毁）
///   2. 即时段：周围 radius（1）格内友方回复生命（PieceModel.Heal 封顶 MaxHP）；
///      爱可菲自身回复翻倍（中心格 = 自身，按 healAmount × 2 结算）
///   3. 持续段：激活凛冬风暴（BlizzardPassive.Activate）——此后 stormTurns（4）回合内
///      每回合对周围 3 格内最近/最远各 1 个敌方造成魔法伤害（选取/去重/递减逻辑见被动类）
///
/// target 参数被忽略（自身中心效果；调用方传入 caster）。
/// 重复释放 = 风暴刷新剩余回合（不叠加，见 BlizzardPassive.Activate）。
///
/// 构造参数（爱可菲资产 effectJsonParams）：radius（即时段半径，默认 1）、
/// attackPercent（即时段物理攻击力百分比，默认 100）、healAmount（友方回血量，默认 10；
/// 自身 ×2）、stormTurns（风暴持续回合，默认 4；风暴半径/伤害参数在 BlizzardPassive 侧 jsonParams）。
/// </summary>
public class FrostFeastUltimate : IUltimateEffect
{
    private readonly int _radius;           // 即时段作用半径（以自身为中心）
    private readonly int _attackPercent;    // 即时段物理攻击力百分比
    private readonly int _healAmount;       // 友方回血量（自身翻倍）
    private readonly int _stormTurns;       // 风暴持续回合

    public FrostFeastUltimate(int radius, int attackPercent, int healAmount, int stormTurns)
    {
        _radius = Mathf.Max(1, radius);
        _attackPercent = attackPercent;
        _healAmount = Mathf.Max(0, healAmount);
        _stormTurns = Mathf.Max(1, stormTurns);
    }

    /// <summary>自身模式入口：target 被忽略（自身中心复合效果）</summary>
    public void Execute(PieceModel caster, PieceModel target)
    {
        if (caster == null || caster.Data == null) return;
        if (PieceManager.Instance == null) return;
        var board = ChessBoardController.Instance?.Model;
        if (board == null) return;

        ElementType element = caster.Data.innateElement;
        int attackValue = Mathf.RoundToInt(caster.EffectiveAttack * (_attackPercent / 100f));

        // ---- 段 1 + 段 2：遍历自身周围 radius 格（含中心格）——敌方物理伤害 / 友方治疗 ----
        int hitCount = 0, healedCount = 0;
        foreach (var offset in HexCoord.AllCoordsInRadius(_radius))
        {
            var coord = new HexCoord(caster.Coord.q + offset.q, caster.Coord.r + offset.r);
            if (!board.Contains(coord)) continue;
            var occupant = PieceLayoutModel.Instance?.GetPieceAt(coord);
            if (occupant == null || occupant.IsDead || occupant.IsDestroyed) continue;

            if (occupant.Owner != caster.Owner)
            {
                // ---- 段 1：物理 + 冰元素（CalculateWithElement 含反应倍率/额外伤害）----
                int damage;
                ElementReactionTable.ReactionConfig reaction = default;
                if (element != ElementType.None)
                {
                    (damage, reaction) = DamageCalculator.CalculateWithElement(
                        attackValue, occupant.EffectiveDefense, element, occupant.AffixedElement);
                }
                else
                {
                    damage = Mathf.Max(1, attackValue - occupant.EffectiveDefense);
                }

                // 返回 false = 被护盾拦截 → 不给金币；元素附着/反应照常
                bool landed = PieceManager.Instance.ApplyIncomingDamage(occupant, damage, caster,
                    DamageSource.Ultimate, DamageKind.Physical, element);
                PieceManager.Instance.ApplyElementInteraction(occupant, caster, element, 2, reaction);
                if (landed)
                {
                    GoldManager.Instance?.OnDamageDealt(caster, damage);
                    GoldManager.Instance?.OnDamageReceived(occupant, damage);
                }

                if (occupant.IsDead)
                    PieceManager.Instance.DestroyPiece(occupant, caster);

                string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
                Debug.Log($"[FrostFeast] 即时段命中 {occupant.Data.displayName}，造成 {damage} 伤害{reactionStr}（剩余 {occupant.CurrentHP}）");
                hitCount++;
            }
            else
            {
                // ---- 段 2：友方治疗（自身翻倍；Heal 封顶 MaxHP）----
                int heal = ReferenceEquals(occupant, caster) ? _healAmount * 2 : _healAmount;
                if (heal > 0)
                {
                    occupant.Heal(heal);
                    string selfMark = ReferenceEquals(occupant, caster) ? "（自身翻倍）" : "";
                    Debug.Log($"[FrostFeast] {occupant.Data.displayName} 回复 {heal} 生命{selfMark}（剩余 {occupant.CurrentHP}/{occupant.MaxHP}）");
                    healedCount++;
                }
            }
        }

        // ---- 段 3：激活凛冬风暴（持续伤害由 BlizzardPassive.OnTurnStart 逐回合结算）----
        foreach (var passive in caster.GetAllPassives())
        {
            if (passive is BlizzardPassive storm)
            {
                storm.Activate(_stormTurns);
                Debug.Log($"[FrostFeast] 凛冬风暴激活：持续 {_stormTurns} 回合，每回合对周围 3 格内最近/最远敌方造成魔法伤害");
                break;
            }
        }

        Debug.Log($"[FrostFeast] {caster.Data.displayName} 释放极寒飨宴：即时段命中 {hitCount} 个敌人、" +
            $"治疗 {healedCount} 个友方（含自身翻倍），风暴已激活");
    }
}
