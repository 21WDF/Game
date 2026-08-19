using UnityEngine;

/// <summary>
/// 枯萎减防被动（枯萎宝珠）—— 攻击造成伤害后，给目标叠加一条临时减防 buff。
/// buff 为 { statType="Defense", amount=-defenseReduction, turnsRemaining=duration }，可叠加多条；
/// 回合递减/到期移除由 TurnManager.TickTempBuffs 统一处理，生效经 EffectiveDefense 的 GetTempBuffTotal("Defense")。
/// 仅在 AttackPiece 路径触发（普攻/元素溅射）。
/// </summary>
public class WitherPassive : IPassiveEffect
{
    private readonly int _defenseReduction;
    private readonly int _duration;

    public WitherPassive(int defenseReduction = 3, int duration = 2)
    {
        _defenseReduction = defenseReduction;
        _duration = duration;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>造成伤害后：目标叠加临时减防 buff（负数 amount）</summary>
    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null)
    {
        if (target == null) return;
        target.TempBuffs.Add(new PieceModel.TempBuff
        {
            statType = "Defense",
            amount = -_defenseReduction,
            turnsRemaining = _duration
        });
        Debug.Log($"[WitherPassive] {owner.Data.displayName} 枯萎 {target.Data.displayName}（防御 -{_defenseReduction}，持续 {_duration} 回合）");
    }
}
