using UnityEngine;

/// <summary>
/// 石像鬼叠防被动（石像鬼板甲）—— 被不同敌人攻击时叠防御，计时内未再被新敌人攻击则清零。
/// triggerOnNoDamage=true（默认）：任何被攻击都叠层（OnAttacked，含未减血场景）；
/// false：仅实际减血叠层（OnDamageReceived）。
/// 叠层防御经 PieceModel.EffectiveDefense 的 GetTotalStoneSkinDefense 聚合生效。
/// </summary>
public class StoneSkinPassive : IPassiveEffect
{
    private readonly int _defenseBonus;
    private readonly int _timerTurns;
    private readonly bool _triggerOnNoDamage;

    private int _stacks;
    private int _timer;
    private PieceModel _lastAttacker;

    public StoneSkinPassive(int defenseBonus = 5, int timerTurns = 4, bool triggerOnNoDamage = true)
    {
        _defenseBonus = defenseBonus;
        _timerTurns = timerTurns;
        _triggerOnNoDamage = triggerOnNoDamage;
    }

    /// <summary>当前叠层提供的防御加成（供 EffectiveDefense 聚合）</summary>
    public int DefenseBonus => _stacks * _defenseBonus;

    public void OnEquip(PieceModel owner) => Reset();
    public void OnUnequip(PieceModel owner) => Reset();

    /// <summary>被攻击时：无论是否减血都叠层（默认模式）</summary>
    public void OnAttacked(PieceModel owner, PieceModel attacker, DamageSource source = DamageSource.Attack)
    {
        if (!_triggerOnNoDamage) return;
        TryStack(owner, attacker);
    }

    /// <summary>受到伤害时：仅减血叠层（triggerOnNoDamage=false 模式；来源取 LastDamageSource）</summary>
    public void OnDamageReceived(PieceModel owner, int damage, DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical)
    {
        if (_triggerOnNoDamage) return;
        TryStack(owner, owner.LastDamageSource);
    }

    /// <summary>回合开始：有叠层时计时递减，归零清空（口径同生命铠甲——每次任一方回合开始都递减）</summary>
    public void OnTurnStart(PieceModel owner)
    {
        if (_lastAttacker == null) return;
        _timer--;
        if (_timer <= 0)
        {
            Debug.Log($"[StoneSkinPassive] {owner.Data.displayName} 叠防计时归零，清空 {_stacks} 层（+{DefenseBonus} → +0）");
            Reset();
        }
    }

    /// <summary>被敌方攻击时：首次只开启计时器（不叠防）；计时内被「不同敌人」攻击才叠层并刷新计时（同一敌人连打不触发）</summary>
    private void TryStack(PieceModel owner, PieceModel attacker)
    {
        if (attacker == null || attacker == _lastAttacker) return;

        if (_lastAttacker == null)
        {
            // 首次被攻击：只开启计时器，不叠防
            _timer = _timerTurns;
            _lastAttacker = attacker;
            Debug.Log($"[StoneSkinPassive] {owner.Data.displayName} 首次被 {attacker.Data.displayName} 攻击，开启 {_timerTurns} 回合计时（不叠防）");
            return;
        }

        // 计时内被不同敌人攻击：叠一层并刷新计时
        _stacks++;
        _timer = _timerTurns;
        _lastAttacker = attacker;
        Debug.Log($"[StoneSkinPassive] {owner.Data.displayName} 被新敌人攻击，叠层 {_stacks}（防御 +{DefenseBonus}，{_timerTurns} 回合内有效）");
    }

    private void Reset()
    {
        _stacks = 0;
        _timer = 0;
        _lastAttacker = null;
    }
}
