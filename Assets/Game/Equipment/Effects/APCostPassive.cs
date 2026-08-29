using UnityEngine;

/// <summary>
/// 代价型被动②：扣 AP（地下交易装备的使用代价）—— 装备后每回合开始扣 N AP。
/// 触发粒度：仅装备者自己一方回合开始（AP 是玩家资源、在自己回合开始时 refill，
/// 对方回合扣会被 refill 无感覆盖，无意义）。扣款发生在 refill 之后（OnTurnStarted 中
/// RefillAP 先于被动通知），净效果 = 每回合可用 AP 减少 N；不足则扣到 0 为止。
/// jsonParams：{"apPerTurn":1}。
/// </summary>
public class APCostPassive : IPassiveEffect
{
    private readonly int _apPerTurn;

    public APCostPassive(int apPerTurn)
    {
        _apPerTurn = apPerTurn;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>每回合开始（仅装备者自己一方）：扣 AP（在回合开始 refill 之后触发，净效果 = 每回合少 N AP）</summary>
    public void OnTurnStart(PieceModel owner)
    {
        if (owner == null || owner.IsDead || _apPerTurn <= 0) return;
        if (TurnManager.Instance == null || TurnManager.Instance.ActivePlayer != owner.Owner) return;

        APManager.Instance?.ConsumeAP(owner.Owner, _apPerTurn);
        Debug.Log($"[APCostPassive] {owner.Data.displayName} 支付代价：扣 {_apPerTurn} AP（当前 {APManager.Instance?.GetAP(owner.Owner)}）");
    }
}
