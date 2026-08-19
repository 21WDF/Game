using UnityEngine;

/// <summary>
/// 中娅悖论 · 免死（DeathDefyPassive）—— 死亡介入型被动。
/// 扣血后、死亡判定前（PieceManager.TryDefyDeath 调用）：目标已死且冷却完毕时，
/// SetHP(1) 免死一次并进入冷却。冷却记在被动实例上（跟装备走）：卸下暂停、重装继续递减。
/// </summary>
public class DeathDefyPassive : IPassiveEffect
{
    private readonly int _cooldownTurns;
    private int _cooldown;

    public DeathDefyPassive(int cooldownTurns = 16)
    {
        _cooldownTurns = cooldownTurns;
    }

    /// <summary>死亡介入（扣血后、死亡判定前）：目标已死且冷却完毕 → 回 1 HP 免死，进入冷却</summary>
    public bool TryDefy(PieceModel target)
    {
        if (_cooldown > 0 || target == null || !target.IsDead) return false;

        target.SetHP(1);
        _cooldown = _cooldownTurns;
        Debug.Log($"[DeathDefyPassive] 免死触发，{target.Data?.displayName} 回到 1 HP，进入 {_cooldownTurns} 回合冷却");
        return true;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }   // 冷却跟装备走：卸下暂停、重装继续

    /// <summary>回合开始：冷却递减（最小 0）</summary>
    public void OnTurnStart(PieceModel owner)
    {
        if (_cooldown > 0) _cooldown--;
    }
}
