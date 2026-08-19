using UnityEngine;

/// <summary>
/// 中娅悖论 · 首伤=1（FirstDamageToOnePassive）—— 伤害介入型被动。
/// 受击扣血前（PieceManager.ApplyFirstDamageProtection 调用）：冷却完毕且伤害 >1 时，
/// 伤害降为 1 并进入冷却。冷却记在被动实例上（跟装备走）：卸下暂停、重装继续递减。
/// </summary>
public class FirstDamageToOnePassive : IPassiveEffect
{
    private readonly int _cooldownTurns;
    private int _cooldown;

    public FirstDamageToOnePassive(int cooldownTurns = 4)
    {
        _cooldownTurns = cooldownTurns;
    }

    /// <summary>伤害介入（扣血前）：冷却完毕且 damage>1 → 伤害降为 1，进入冷却</summary>
    public void TryReduceFirstDamage(ref int damage)
    {
        if (_cooldown > 0 || damage <= 1) return;

        damage = 1;
        _cooldown = _cooldownTurns;
        Debug.Log($"[FirstDamageToOnePassive] 首伤=1 生效，伤害降为 1，进入 {_cooldownTurns} 回合冷却");
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }   // 冷却跟装备走：卸下暂停、重装继续

    /// <summary>回合开始：冷却递减（最小 0）</summary>
    public void OnTurnStart(PieceModel owner)
    {
        if (_cooldown > 0) _cooldown--;
    }
}
