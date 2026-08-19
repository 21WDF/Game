using UnityEngine;

/// <summary>
/// 协同状态结算器被动（挂在雷电将军 PieceData.builtInPassives 上，由无想一刀大招激活）——
/// 持续 turns 回合的「协同状态」，两个效果：
///   1. 雷电将军每次普攻命中 → 全体己方棋子回复 energyPerAttack（5）点能量
///      （触发口径：OnAllyAttackHit 且 ally == owner 且 source == Attack——真普攻主段；
///       溅射/二段不重复回能；协同攻击自身被重入守卫拦截不回能）
///   2. 己方任意棋子攻击（普攻族 Attack/Splash + 大招 Ultimate，含雷电将军本人）命中敌方 →
///      雷电将军立即对该目标发起一次远程协同攻击（射程无限；伤害 = baseDamage + 攻击×attackPercent%，
///       完整 AttackPiece 管道：元素反应/金币/跳字/击杀全走；不耗 AP/能量）
///
/// 防递归（红线）：静态 _striking 重入守卫——协同攻击经过 AttackPiece 会再次广播 OnAllyAttackHit，
/// 守卫期间本被动不再响应（不二次协同、不回能）；守卫在 finally 中复位。
///
/// 状态生命周期：跨回合状态存于本被动持久实例（大招效果每次释放由工厂重建，不能存状态——
/// 参照 BaronBunny → PuppetPassive 激活模式）；回合递减在 OnTurnStart（每次任一方回合开始，
/// 同领域/风暴口径）；雷电将军死亡后不被广播（NotifyAllyAttackHit 只通知存活棋子），状态自然失效。
///
/// 构造参数（雷电将军资产 jsonParams）：baseDamage（协同基础伤害，默认 0）、
/// attackPercent（协同攻击力百分比，默认 50）、energyPerAttack（普攻回能量，默认 5）。
/// 持续回合由大招 Activate 传入（大招侧参数 synergyTurns）。
/// </summary>
public class CoordinatedStrikePassive : IPassiveEffect
{
    private readonly int _baseDamage;        // 协同攻击基础伤害
    private readonly int _attackPercent;     // 协同攻击力百分比（雷电将军 EffectiveAttack）
    private readonly int _energyPerAttack;   // 雷电将军普攻时全体己方回能量

    // ---- 运行时状态 ----
    private int _turnsRemaining;             // 协同状态剩余回合（0 = 未激活）

    /// <summary>重入守卫（static：防协同攻击再触发协同/回能——AttackPiece 广播会重入本被动）</summary>
    private static bool _striking;

    public string UniqueId => null;

    public CoordinatedStrikePassive(int baseDamage, int attackPercent, int energyPerAttack)
    {
        _baseDamage = baseDamage;
        _attackPercent = attackPercent;
        _energyPerAttack = energyPerAttack;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) => _turnsRemaining = 0;

    /// <summary>激活/刷新协同状态（由 MusouStrikeUltimate.Execute 调用；重复释放 = 刷新剩余回合）</summary>
    public void Activate(int turns) => _turnsRemaining = Mathf.Max(1, turns);

    /// <summary>协同状态是否激活中（诊断/UI 可用）</summary>
    public bool IsActive => _turnsRemaining > 0;

    // ---- 回合开始：协同状态递减 ----

    public void OnTurnStart(PieceModel owner)
    {
        if (_turnsRemaining <= 0) return;
        _turnsRemaining--;
        if (_turnsRemaining <= 0)
            Debug.Log("[CoordinatedStrike] 协同状态结束");
    }

    // ---- 己方攻击命中广播：回能 + 协同攻击 ----

    public void OnAllyAttackHit(PieceModel owner, PieceModel ally, PieceModel target, int damage, DamageSource source)
    {
        if (_turnsRemaining <= 0 || _striking) return;
        if (owner == null || owner.IsDead || owner.Energy == null) return;

        // ---- 效果 1：雷电将军自己普攻命中 → 全体己方回能（仅真普攻主段；含自己普攻）----
        if (ReferenceEquals(ally, owner) && source == DamageSource.Attack && _energyPerAttack > 0)
        {
            var allies = TurnManager.Instance?.Model?.GetPieces(owner.Owner);
            if (allies != null)
            {
                foreach (var mate in allies)
                {
                    if (mate == null || mate.IsDead || mate.IsDestroyed) continue;
                    mate.Energy?.Gain(_energyPerAttack);
                }
                Debug.Log($"[CoordinatedStrike] {owner.Data.displayName} 普攻，全体己方回复 {_energyPerAttack} 能量");
            }
        }

        // ---- 效果 2：己方攻击（普攻族/大招，含自己）命中存活敌方 → 协同攻击 ----
        if (source != DamageSource.Attack && source != DamageSource.Splash && source != DamageSource.Ultimate) return;
        if (target == null || target.IsDead || target.IsDestroyed) return;
        if (target.Owner == owner.Owner) return;   // 只对敌方目标协同（广播防御）

        int attack = _baseDamage + Mathf.RoundToInt(owner.EffectiveAttack * (_attackPercent / 100f));
        _striking = true;
        try
        {
            PieceManager.Instance.AttackPiece(owner, target, skipEnergy: true, overrideAttack: attack);
            Debug.Log($"[CoordinatedStrike] {ally.Data?.displayName} 命中 {target.Data.displayName}，" +
                      $"{owner.Data.displayName} 发起协同攻击（剩余 {_turnsRemaining} 回合）");
        }
        finally
        {
            _striking = false;
        }
    }

    // ---- 其余钩子：协同状态不消费 ----
    public void OnAttacked(PieceModel owner, PieceModel attacker, DamageSource source = DamageSource.Attack) { }
    public void OnDamageReceived(PieceModel owner, int damage,
        DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical) { }
    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null) { }
    public void OnKill(PieceModel owner, PieceModel victim) { }
    public void OnAllyDeath(PieceModel owner, PieceModel ally) { }
    public void OnAllyUltimateCast(PieceModel owner, PieceModel caster) { }
}
