using UnityEngine;

/// <summary>
/// 雷罚恶曜之眼被动（挂在雷电将军 PieceData.builtInPassives 上）——
/// 全局回能：己方其他棋子每次释放大招成功，雷电将军回复 energyGain（10）点能量。
///
/// 触发点：OnAllyUltimateCast（UseUltimateCore 在 Execute 完成后广播给释放者阵营全体存活棋子——
/// 雷电将军死亡时不在广播名单内，自然失效，无需额外判断）。
///
/// 口径（已确认）：不含自己——雷电将军本人释放大招不给自己回能（对齐原作设定；
/// 广播仍会到达她，此处在 caster == owner 时直接返回）。
///
/// 构造参数（雷电将军资产 jsonParams）：energyGain（每次队友开大回能量，默认 10）。
/// </summary>
public class StormEyePassive : IPassiveEffect
{
    private readonly int _energyGain;   // 队友开大时回能量值

    public string UniqueId => null;

    public StormEyePassive(int energyGain)
    {
        _energyGain = Mathf.Max(0, energyGain);
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>队友释放大招成功 → 雷电将军回能（不含自己；死亡时不被广播）</summary>
    public void OnAllyUltimateCast(PieceModel owner, PieceModel caster)
    {
        if (owner == null || owner.IsDead || owner.Energy == null) return;
        if (caster == null || ReferenceEquals(caster, owner)) return;   // 不含自己
        if (_energyGain <= 0) return;

        owner.Energy.Gain(_energyGain);
        Debug.Log($"[StormEye] 队友 {caster.Data?.displayName} 释放大招，{owner.Data.displayName} 回复 {_energyGain} 能量" +
                  $"（{owner.Energy.CurrentEnergy}/{owner.Energy.MaxEnergy}）");
    }

    // ---- 其余钩子：回能被动不消费 ----
    public void OnTurnStart(PieceModel owner) { }
    public void OnAttacked(PieceModel owner, PieceModel attacker, DamageSource source = DamageSource.Attack) { }
    public void OnDamageReceived(PieceModel owner, int damage,
        DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical) { }
    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null) { }
    public void OnKill(PieceModel owner, PieceModel victim) { }
    public void OnAllyDeath(PieceModel owner, PieceModel ally) { }
    public void OnAllyAttackHit(PieceModel owner, PieceModel ally, PieceModel target, int damage, DamageSource source) { }
}
