using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 伤害计算器（纯静态工具类，不继承 MonoBehaviour）
/// Phase 1：减法公式 damage = max(1, attack - defense)
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// 计算伤害（基础，无元素反应）
    /// </summary>
    public static int Calculate(int attack, int defense)
    {
        return Mathf.Max(1, attack - defense);
    }

    /// <summary>
    /// 计算伤害（含元素反应 + 棋子修饰器链）
    /// </summary>
    /// <param name="attack">攻击方攻击力</param>
    /// <param name="defense">防御方防御力</param>
    /// <param name="attackerElement">攻击方先天元素（触发元素）</param>
    /// <param name="defenderAffixed">防御方附着元素（底元素）</param>
    /// <param name="pieceModifiers">棋子修饰器链（从攻击方装备收集；可为 null）</param>
    /// <returns>(伤害, 反应配置)；无反应时配置为 None/1.0x</returns>
    public static (int damage, ElementReactionTable.ReactionConfig reaction) CalculateWithElement(
        int attack, int defense, ElementType attackerElement, ElementType defenderAffixed,
        IReadOnlyList<IReactionModifier> pieceModifiers = null)
    {
        int baseDamage = Mathf.Max(1, attack - defense);
        var reaction = ElementReactionTable.GetReaction(defenderAffixed, attackerElement, pieceModifiers);
        int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * reaction.DamageMultiplier) + reaction.ExtraDamage);
        return (damage, reaction);
    }

    /// <summary>
    /// 计算伤害（使用预查询的反应配置，避免二次查询）
    /// 供 PieceManager.AttackPiece 在预应用超导减防后使用：先查一次反应 → 预设减防 → 用同一反应算伤害。
    /// </summary>
    /// <param name="attack">攻击方攻击力</param>
    /// <param name="defense">防御方防御力（可能已含超导减防）</param>
    /// <param name="reaction">预查询的反应配置</param>
    public static int CalculateWithReaction(int attack, int defense, ElementReactionTable.ReactionConfig reaction)
    {
        int baseDamage = Mathf.Max(1, attack - defense);
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * reaction.DamageMultiplier) + reaction.ExtraDamage);
    }
}
