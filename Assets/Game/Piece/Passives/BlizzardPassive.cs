using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 凛冬风暴结算器被动（挂在爱可菲 PieceData.builtInPassives 上，由极寒飨宴大招激活）——
/// 大招释放后的持续段：每回合（任一方回合开始，同领域/DoT 口径）对爱可菲周围 radius（3）格内
/// 「距离最近」和「距离最远」的各 1 个敌方棋子造成魔法伤害；同距离内随机选取；
/// 最近与最远指向同一目标时只结算一次（去重）。
///
/// 为什么是被动而不是大招实例：大招效果实例每次释放由工厂重建（UseUltimateCore → CreateUltimate），
/// 跨回合状态必须挂在持久对象上——本被动随爱可菲生成创建（PassiveFactory + OnEquip），
/// FrostFeastUltimate.Execute 通过 Activate(turns) 激活/刷新（参照 BaronBunnyUltimate → PuppetPassive 模式）。
///
/// 伤害口径（魔法 + 冰元素 gauge 2，同菲林斯强化态）：raw = baseDamage + 攻击×attackPercent%，
/// 免防御减伤；有元素时 damage = max(1, round(raw × 反应倍率) + 额外伤害)；
/// ApplyIncomingDamage(Ultimate/Magical) + ApplyElementInteraction（附着 gauge 2 + 反应副作用）+ 金币双边。
///
/// 失效：爱可菲死亡后回合通知不再到达（NotifyPassivesTurnStartSide 跳过死亡棋子），风暴自然终止。
///
/// 构造参数（爱可菲资产 jsonParams）：radius（风暴半径，默认 3）、baseDamage（默认 0）、
/// attackPercent（每 tick 攻击力百分比，默认 30）。持续回合由大招 Activate 传入（大招侧参数）。
/// </summary>
public class BlizzardPassive : IPassiveEffect
{
    private readonly int _radius;           // 风暴半径（以爱可菲当前位置为中心，每回合动态跟随）
    private readonly int _baseDamage;       // 每 tick 魔法基础伤害
    private readonly int _attackPercent;    // 每 tick 攻击力百分比（爱可菲的 EffectiveAttack）

    // ---- 运行时状态（被动实例随爱可菲生成创建，随场景重开重建）----
    private PieceModel _owner;              // 爱可菲（OnEquip 注入）
    private int _turnsRemaining;            // 风暴剩余回合（0 = 未激活）

    public string UniqueId => null;

    public BlizzardPassive(int radius, int baseDamage, int attackPercent)
    {
        _radius = Mathf.Max(1, radius);
        _baseDamage = baseDamage;
        _attackPercent = attackPercent;
    }

    public void OnEquip(PieceModel owner) => _owner = owner;
    public void OnUnequip(PieceModel owner) => _turnsRemaining = 0;

    /// <summary>激活/刷新风暴（由 FrostFeastUltimate.Execute 调用；重复释放 = 刷新剩余回合）</summary>
    public void Activate(int turns) => _turnsRemaining = Mathf.Max(1, turns);

    /// <summary>风暴是否激活中（诊断/UI 可用）</summary>
    public bool IsActive => _turnsRemaining > 0;

    // ---- 回合开始：风暴结算 ----

    public void OnTurnStart(PieceModel owner)
    {
        if (_turnsRemaining <= 0 || owner == null || owner.IsDead) return;
        if (PieceManager.Instance == null) return;

        // 快照收集风暴半径内存活敌方 + 距离（结算可能致死修改棋子列表）
        var enemySide = owner.Owner == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;
        var enemies = TurnManager.Instance?.Model?.GetPieces(enemySide);
        var inRange = new List<(PieceModel piece, int dist)>();
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead || enemy.IsDestroyed) continue;
                int dist = owner.Coord.Distance(enemy.Coord);
                if (dist <= _radius) inRange.Add((enemy, dist));
            }
        }

        // 最近/最远各选 1（同距随机；去重——同一目标只结算一次）
        var targets = SelectNearestAndFarthest(inRange);

        ElementType element = owner.Data != null ? owner.Data.innateElement : ElementType.None;
        foreach (var victim in targets)
        {
            // 魔法伤害（免防御）+ 冰元素反应倍率/额外伤害（同菲林斯强化态公式）
            float raw = _baseDamage + owner.EffectiveAttack * (_attackPercent / 100f);
            int damage;
            ElementReactionTable.ReactionConfig reaction = default;
            if (element != ElementType.None)
            {
                reaction = ElementReactionTable.GetReaction(victim.AffixedElement, element);
                damage = Mathf.Max(1, Mathf.RoundToInt(raw * reaction.DamageMultiplier) + reaction.ExtraDamage);
            }
            else
                damage = Mathf.Max(1, Mathf.RoundToInt(raw));

            // 返回 false = 被护盾拦截 → 不给金币；元素附着/反应照常
            bool landed = PieceManager.Instance.ApplyIncomingDamage(victim, damage, owner,
                DamageSource.Ultimate, DamageKind.Magical, element);
            PieceManager.Instance.ApplyElementInteraction(victim, owner, element, 2, reaction);
            if (landed)
            {
                GoldManager.Instance?.OnDamageDealt(owner, damage);
                GoldManager.Instance?.OnDamageReceived(victim, damage);
            }

            if (victim.IsDead)
                PieceManager.Instance.DestroyPiece(victim, owner);

            string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
            Debug.Log($"[Blizzard] 凛冬风暴命中 {victim.Data.displayName}，造成 {damage} 魔法伤害{reactionStr}（剩余 {victim.CurrentHP}）");
        }

        _turnsRemaining--;
        if (_turnsRemaining <= 0)
            Debug.Log("[Blizzard] 凛冬风暴结束");
        else
            Debug.Log($"[Blizzard] 凛冬风暴剩余 {_turnsRemaining} 回合（本次命中 {targets.Count} 个目标）");
    }

    /// <summary>从范围内敌方中选「最近 1 个 + 最远 1 个」（同距离组内随机；两个选择指向同一目标时去重只取一次；
    /// 范围内 0 个返回空，1 个只返回它）</summary>
    private static List<PieceModel> SelectNearestAndFarthest(List<(PieceModel piece, int dist)> inRange)
    {
        var result = new List<PieceModel>();
        if (inRange.Count == 0) return result;
        if (inRange.Count == 1) { result.Add(inRange[0].piece); return result; }

        int minDist = int.MaxValue, maxDist = int.MinValue;
        foreach (var (_, dist) in inRange)
        {
            if (dist < minDist) minDist = dist;
            if (dist > maxDist) maxDist = dist;
        }

        var nearestGroup = new List<PieceModel>();
        var farthestGroup = new List<PieceModel>();
        foreach (var (piece, dist) in inRange)
        {
            if (dist == minDist) nearestGroup.Add(piece);
            if (dist == maxDist) farthestGroup.Add(piece);
        }

        var nearest = nearestGroup[Random.Range(0, nearestGroup.Count)];
        var farthest = farthestGroup[Random.Range(0, farthestGroup.Count)];

        result.Add(nearest);
        if (!ReferenceEquals(nearest, farthest)) result.Add(farthest);   // 去重：同一目标只打一次
        return result;
    }

    // ---- 其余钩子：风暴不消费 ----
    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null) { }
    public void OnDamageReceived(PieceModel owner, int damage,
        DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical) { }
    public void OnKill(PieceModel owner, PieceModel victim) { }
    public void OnAllyDeath(PieceModel owner, PieceModel ally) { }
}
