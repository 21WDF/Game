using UnityEngine;

/// <summary>
/// 光界之力 —— 反应伤害增强被动（非唯一，可叠加）。
/// 实现 IReactionModifier：发生元素反应时倍率 +percent%（加法叠加——多件依次 Modify 累加）。
/// ReactionType.None（无反应）不加成，避免普攻无反应也被增强。
/// 事件钩子全部走接口默认实现（OnEquip/OnUnequip 空实现），无回合/伤害逻辑。
/// </summary>
public class ReactionBoostPassive : IPassiveEffect, IReactionModifier
{
    private readonly int _percent;

    public ReactionBoostPassive(int percent = 25)
    {
        _percent = percent;
    }

    /// <summary>反应倍率加成：Type != None 时 DamageMultiplier += percent/100（struct 传参即副本，改后返回）</summary>
    public ElementReactionTable.ReactionConfig Modify(ElementReactionTable.ReactionConfig original)
    {
        if (original.Type == ReactionType.None) return original;

        original.DamageMultiplier += _percent / 100f;
        Debug.Log($"[ReactionBoostPassive] 反应({original.Type})倍率 +{_percent}% → {original.DamageMultiplier}");
        return original;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }
}
