using UnityEngine;

/// <summary>
/// 再生被动（生命铠甲）—— 回合开始回血，可按"连续未受伤回合数"进化。
/// 进化计时为装备实例独立（_turnsWithoutDamage）：OnTurnStart 递增、任何实际减血归零、装卸清零（重装重新计时）；
/// 达到 upgradeThreshold 后每回合回 upgradedAmount，否则回 baseAmount（Heal 封顶 MaxHP）。
/// </summary>
public class RegenPassive : IPassiveEffect
{
    private readonly int _baseAmount;
    private readonly int _upgradedAmount;
    private readonly int _upgradeThreshold;

    /// <summary>连续未受伤害回合计时（实例独立；受任何实际减血归零，装卸清零）</summary>
    private int _turnsWithoutDamage;

    public RegenPassive(int baseAmount, int upgradedAmount, int upgradeThreshold)
    {
        _baseAmount = baseAmount;
        _upgradedAmount = upgradedAmount;
        _upgradeThreshold = upgradeThreshold;
    }

    /// <summary>装上清零（重装重新计时）</summary>
    public void OnEquip(PieceModel owner) { _turnsWithoutDamage = 0; }

    /// <summary>卸下清零</summary>
    public void OnUnequip(PieceModel owner) { _turnsWithoutDamage = 0; }

    /// <summary>回合开始：计时 +1 后按阈值决定回血量（进化判定）</summary>
    public void OnTurnStart(PieceModel owner)
    {
        _turnsWithoutDamage++;
        bool upgraded = _turnsWithoutDamage >= _upgradeThreshold;
        int heal = upgraded ? _upgradedAmount : _baseAmount;
        if (heal <= 0) return;
        owner.Heal(heal);
        Debug.Log($"[RegenPassive] {owner.Data.displayName} 再生 {heal}（未受伤 {_turnsWithoutDamage} 回合{(upgraded ? "，已进化" : "")}，剩余 {owner.CurrentHP}）");
    }

    /// <summary>受到伤害：计时归零（任何实际减血都打断进化，不区分 DamageSource；统一入口保证通知时必然已减血）</summary>
    public void OnDamageReceived(PieceModel owner, int damage, DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical)
    {
        _turnsWithoutDamage = 0;
    }
}
