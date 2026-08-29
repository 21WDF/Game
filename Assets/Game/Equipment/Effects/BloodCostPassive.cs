using UnityEngine;

/// <summary>
/// 代价型被动①：持续扣血（地下交易装备的使用代价）—— 装备后每回合开始扣 X HP（越强扣越多）。
/// 触发粒度：每次任一方回合开始（同 DoT/强化/超导的项目「每回合」口径）。
/// 致死处理：走 DoT 同款路径——ApplyIncomingDamage（Dot 来源、真实类型，免死被动照常介入），
/// 死亡由销毁流程统一处理。jsonParams：{"hpPerTurn":2}。
/// </summary>
public class BloodCostPassive : IPassiveEffect
{
    private readonly int _hpPerTurn;

    public BloodCostPassive(int hpPerTurn)
    {
        _hpPerTurn = hpPerTurn;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>每回合开始（任一方）：扣血。走统一伤害入口（免死介入；Dot 来源不计入直接伤害统计）</summary>
    public void OnTurnStart(PieceModel owner)
    {
        if (owner == null || owner.IsDead || _hpPerTurn <= 0) return;
        PieceManager.Instance?.ApplyIncomingDamage(owner, _hpPerTurn, null, DamageSource.Dot, DamageKind.True);
        Debug.Log($"[BloodCostPassive] {owner.Data.displayName} 支付代价：扣血 {_hpPerTurn}（剩余 {owner.CurrentHP}）");
        if (owner.IsDead)
        {
            Debug.Log($"[TurnManager] {owner.Data.displayName} 因装备代价失血死亡");
            PieceManager.Instance?.DestroyPiece(owner);
        }
    }
}
