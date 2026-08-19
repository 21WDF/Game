using UnityEngine;

/// <summary>
/// 冰锋蚀甲被动（挂在爱可菲 PieceData.builtInPassives 上）——
/// 受到爱可菲普攻攻击的敌方棋子防御值降低（固定值），持续 duration（4）回合。
///
/// 触发点：OnDamageDealt 仅由普攻路径（PieceManager.AttackPiece）触发——大招/溅射路径外的
/// 间接伤害不经过；溅射本身也是 AttackPiece 调用，命中即施加（"受到爱可菲攻击"的自然语义）。
///
/// 与超导减防的关系（独立并行，互不覆盖）：
///   - 本被动写 PieceModel.PassiveDefenseReduction / PassiveDefenseReductionTurnsRemaining；
///   - 超导反应写 CurrentDefenseReduction / DefenseReductionTurnsRemaining；
///   - 两者在 EffectiveDefense 中同时扣除、在 TurnManager.TickElementDuration 中各自独立递减
///     （同冰障 TemporaryDefenseBonus 的分离先例）。
///
/// 不可叠加：重复攻击同一目标 = 覆盖式赋值（值不变、剩余回合刷新为 duration），不累加减防值。
///
/// 构造参数（爱可菲资产 jsonParams）：amount（减防固定值，默认 4）、duration（持续回合，默认 4）。
/// </summary>
public class FrostBreakPassive : IPassiveEffect
{
    private readonly int _amount;       // 减防固定值
    private readonly int _duration;     // 持续回合

    public string UniqueId => null;

    public FrostBreakPassive(int amount, int duration)
    {
        _amount = Mathf.Max(0, amount);
        _duration = Mathf.Max(1, duration);
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>普攻命中：目标被动减防（不可叠加 = 值覆盖 + 回合刷新）</summary>
    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null)
    {
        if (target == null || target.IsDead) return;
        bool refreshed = target.PassiveDefenseReductionTurnsRemaining > 0;
        target.PassiveDefenseReduction = _amount;
        target.PassiveDefenseReductionTurnsRemaining = _duration;
        Debug.Log(refreshed
            ? $"[FrostBreak] {target.Data.displayName} 冰锋蚀甲刷新：防御 -{_amount}，剩余 {_duration} 回合（不叠加）"
            : $"[FrostBreak] {target.Data.displayName} 防御被冰锋蚀甲降低 {_amount} 点，持续 {_duration} 回合");
    }

    // ---- 其余钩子：减防被动不消费 ----
    public void OnTurnStart(PieceModel owner) { }
    public void OnDamageReceived(PieceModel owner, int damage,
        DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical) { }
    public void OnKill(PieceModel owner, PieceModel victim) { }
    public void OnAllyDeath(PieceModel owner, PieceModel ally) { }
}
