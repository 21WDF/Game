using UnityEngine;

/// <summary>
/// 吸血被动（饮血剑）—— 攻击造成伤害后回血。
/// 吸血量 = baseHeal + floor(伤害 × percent / 100)，Heal 封顶 MaxHP。
/// baseHeal=0 即纯百分比；percent=0 即纯固定；两者皆有即混合。
/// damage 为 AttackPiece 结算后的最终伤害（已含附加/减伤修正）。
/// </summary>
public class LifestealPassive : IPassiveEffect
{
    private readonly int _baseHeal;
    private readonly int _percent;

    public LifestealPassive(int baseHeal, int percent)
    {
        _baseHeal = baseHeal;
        _percent = percent;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>造成伤害后：固定 + 百分比吸血（向下取整；Heal 封顶 MaxHP）。
    /// 护盾拦截（基座修复）：伤害被目标护盾免掉时不吸血（固定 + 百分比都不吸）——
    /// 免伤 = 攻击方无伤害收益；非拦截场景吸血数值与原逻辑完全一致</summary>
    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null)
    {
        if (target != null && target.LastHitAbsorbedByShield)
        {
            Debug.Log($"[LifestealPassive] {owner.Data.displayName} 的伤害被护盾格挡，不吸血");
            return;
        }
        int heal = _baseHeal + Mathf.FloorToInt(damage * _percent / 100f);
        if (heal <= 0) return;
        owner.Heal(heal);
        Debug.Log($"[LifestealPassive] {owner.Data.displayName} 吸血 {heal}（固定 {_baseHeal} + {damage}×{_percent}%，剩余 {owner.CurrentHP}）");
    }
}
