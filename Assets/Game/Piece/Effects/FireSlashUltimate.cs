using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 烈焰斩大招（伤害型，ultimateConfig.requiresTarget=true）—— 对单个敌方目标造成含元素反应的放大伤害。
/// 构造参数：bonusDamage（附加在 EffectiveAttack 上的额外攻击力，经 DamageCalculator 参与减法与反应倍率）。
///
/// 伤害结算与普通攻击一致：
///   - 走 DamageCalculator.CalculateWithElement，含元素反应倍率/额外伤害（验收：大招伤害走元素反应）。
///   - 附着攻击方先天元素（反应消耗底元素后覆盖为新元素）。
///   - 金币实时到账（硬约束：伤害发生时金币实时结算，无回合末结算）。
/// </summary>
public class FireSlashUltimate : IUltimateEffect, IUltimateAreaProvider
{
    private readonly int _bonusDamage;

    public FireSlashUltimate(int bonusDamage)
    {
        _bonusDamage = bonusDamage;
    }

    /// <summary>范围声明（元素格子统一入口）：单体伤害型 = 目标所在格（无目标 = 无格子）</summary>
    public List<HexCoord> GetUltimateArea(PieceModel caster, PieceModel target, HexCoord? targetCoord)
        => target != null ? new List<HexCoord> { target.Coord } : new List<HexCoord>();

    public void Execute(PieceModel caster, PieceModel target)
    {
        if (caster == null || target == null || caster.Data == null) return;

        var (damage, reaction) = DamageCalculator.CalculateWithElement(
            caster.EffectiveAttack + _bonusDamage,
            target.EffectiveDefense,
            caster.Data.innateElement,
            target.AffixedElement);
        // 统一伤害入口（不走 AttackPiece：不触发元素反应外的被动链/能量/吸血；但触发 OnDamageReceived——打断再生计时/反甲等）
        // 返回 false = 被护盾拦截 → 不给金币（免伤 = 无伤害收益）；元素附着/反应照常
        bool landed = PieceManager.Instance?.ApplyIncomingDamage(target, damage, caster, DamageSource.Ultimate, DamageKind.Physical, caster.Data.innateElement) ?? false;

        // 元素附着与消耗（大招募发量=2）
        // 护盾染色封印（元素城邦二期）：已染色护盾封印同元素附着 → 跳过附着写入（反应照常）
        if (caster.Data.innateElement != ElementType.None)
        {
            if (!target.BlocksElementAttachment(caster.Data.innateElement))
            {
                var (resultElem, resultGauge) = ElementReactionTable.ResolveElementInteraction(
                    target.AffixedElement, target.AffixedElementGauge,
                    caster.Data.innateElement, 2);
                target.AffixedElement = resultElem;
                target.AffixedElementGauge = resultGauge;
                // 染色：附着成功（结果非 None）→ 未染色护盾变为对应元素盾
                target.TryDyeShieldElement(resultElem);
            }
            if (reaction.DefenseReduction > 0)
                target.CurrentDefenseReduction = reaction.DefenseReduction;
        }

        // 金币实时到账（硬约束；护盾拦截时双方都无金币）
        if (landed)
        {
            GoldManager.Instance?.OnDamageDealt(caster, damage);
            GoldManager.Instance?.OnDamageReceived(target, damage);
        }

        string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
        Debug.Log($"[FireSlashUltimate] {caster.Data.displayName} 烈焰斩 {target.Data.displayName}，" +
                  $"造成 {damage} 伤害{reactionStr}（剩余 {target.CurrentHP}）");
    }
}
