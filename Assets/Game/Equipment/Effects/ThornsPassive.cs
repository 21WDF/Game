using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 荆棘反伤被动（荆棘刺甲）—— 受到伤害后反伤。
/// 反伤值 = amount + floor(受击伤害 × percent / 100)；
/// 对每个反伤目标：affectedByDefense=true 时最终反伤 = max(1, 反伤值 - 目标EffectiveDefense)，否则为真实伤害。
/// 反伤目标集合：
///   reflectRadius = 0：只反攻击者（单体，不受 alwaysReflectAttacker 影响）；
///   reflectRadius ≥ 1 且 alwaysReflectAttacker=true：受击者周围 radius 内所有敌方 + 半径外的攻击者也必反（攻击者在半径内只反一次）；
///   reflectRadius ≥ 1 且 alwaysReflectAttacker=false：只反受击者周围 radius 内所有敌方（攻击者超出半径不反）。
/// 反伤走 ApplyIncomingDamage(DamageSource.Reflect, DamageKind.Magical)：保留直接扣血语义（不触发元素反应/金币/能量/吸血），
/// 仅新增"受伤害通知"（打断再生计时等）；反出的伤害是 Magical，天然不会触发反甲（防递归自动成立）；致死逐个 DestroyPiece(target, owner)。
/// B 阶段：只反物理伤害（kind == Physical 才反伤，魔法/真实/反伤均不反）。
/// 先快照目标列表再结算（反伤致死会修改回合棋子列表，避免遍历中改集合）。
/// owner 即使本次被打死仍正常反伤（同归于尽成立）。
/// </summary>
public class ThornsPassive : IPassiveEffect
{
    private readonly int _amount;
    private readonly int _percent;
    private readonly bool _affectedByDefense;
    private readonly int _reflectRadius;
    private readonly bool _alwaysReflectAttacker;

    public ThornsPassive(int amount, int percent, bool affectedByDefense,
        int reflectRadius, bool alwaysReflectAttacker)
    {
        _amount = amount;
        _percent = percent;
        _affectedByDefense = affectedByDefense;
        _reflectRadius = reflectRadius;
        _alwaysReflectAttacker = alwaysReflectAttacker;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>受到伤害后：按参数对攻击者/周围敌方反伤。
    /// 只反物理伤害（kind == Physical）；反伤自身为 Magical，天然不会反出反甲（防递归自动成立）。</summary>
    public void OnDamageReceived(PieceModel owner, int damage, DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical)
    {
        if (kind != DamageKind.Physical) return;               // 只反物理（魔法/真实/反伤不反）
        var attacker = owner.LastDamageSource;
        int reflectValue = _amount + Mathf.FloorToInt(damage * _percent / 100f);

        // ---- 收集反伤目标（快照：先全部收集，再逐个结算，避免致死销毁在遍历中修改棋子列表）----
        var targets = new List<PieceModel>();
        bool attackerInRange = attacker != null && !attacker.IsDead
            && owner.Coord.Distance(attacker.Coord) <= _reflectRadius;

        // 半径 ≥1：受击者周围 radius 内所有敌方（含半径内的攻击者）
        if (_reflectRadius >= 1)
        {
            var enemySide = owner.Owner == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;
            var enemies = TurnManager.Instance?.Model?.GetPieces(enemySide);
            if (enemies != null)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    if (owner.Coord.Distance(enemy.Coord) > _reflectRadius) continue;
                    targets.Add(enemy);
                }
            }
        }

        // 攻击者单独判定：radius=0 单体必反；radius≥1 时仅 alwaysReflectAttacker 且不在半径内才补反
        //（在半径内的攻击者已被上面的循环收过，天然去重）
        if (attacker != null && !attacker.IsDead
            && (_reflectRadius == 0 || (_alwaysReflectAttacker && !attackerInRange)))
        {
            targets.Add(attacker);
        }

        if (targets.Count == 0) return;

        // ---- 逐个结算反伤（快照之上遍历，安全）----
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            int final = _affectedByDefense
                ? Mathf.Max(1, reflectValue - target.EffectiveDefense)
                : reflectValue;
            if (final <= 0) continue;

            // 统一伤害入口（DamageSource.Reflect：触发受伤害通知，但反甲对 Reflect 位免疫、无吸血/元素反应/金币）
            PieceManager.Instance?.ApplyIncomingDamage(target, final, owner, DamageSource.Reflect, DamageKind.Magical);
            if (sb.Length > 0) sb.Append("，");
            sb.Append($"{target.Data.displayName}:{final}");

            if (target.IsDead)
                PieceManager.Instance?.DestroyPiece(target, owner);
        }
        Debug.Log($"[ThornsPassive] {owner.Data.displayName} 受击 {damage}，反伤 {targets.Count} 个目标（反伤值 {reflectValue}{(_affectedByDefense ? "，减防后" : "，真实伤害")}）：{sb}");
    }
}
