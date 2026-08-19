using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 领域被动（挂在莫娜 PieceData.builtInPassives 上）——
/// 普攻命中后在目标位置留下水域（「区域实体」：位置 + 半径 + 每回合生效）。
///
/// 红线约束：领域不是棋子——不占据格子、不可选中/攻击/移动、不参与胜负判定。
/// 状态全部存于被动实例（同 LegionAegisPassive 口径），非棋子实体：
///   - 建立/移动：OnDamageDealt 仅由普攻路径触发（PieceManager.AttackPiece 独有钩子，
///     大招/溅射/DoT 不经过）→ 领域中心 = 本次普攻目标格；同一莫娜场上只有一个领域。
///   - 结算：OnTurnStart 每次任一方回合开始（同感电 DoT/强化/超导口径）对领域半径内
///     存活敌方造成物理 + 主人先天元素（水）伤害：CalculateWithElement 含反应倍率/额外伤害
///     （蒸发/感电/冻结均可触发），ApplyElementInteraction 附着 gauge=1（普攻口径）+ 反应副作用，
///     ApplyIncomingDamage 统一入口（减伤/易伤/反甲/OnDamageReceived 全走）；金币双边。
///   - 失效：莫娜死亡后回合通知不再到达（NotifyPassivesTurnStartSide 跳过死亡棋子），领域自然失效。
///
/// 构造参数（莫娜资产 jsonParams）：radius（领域半径，默认 2）、baseDamage（默认 0）、
/// attackPercent（每 tick 攻击力百分比，默认 50）——伤害 = baseDamage + 攻击×percent% − 目标防御（经反应倍率）。
/// </summary>
public class DomainPassive : IPassiveEffect
{
    private readonly int _radius;           // 领域半径
    private readonly int _baseDamage;       // tick 基础伤害
    private readonly int _attackPercent;    // tick 攻击力百分比（主人的 EffectiveAttack）

    // ---- 运行时状态（被动实例随莫娜生成创建，随场景重开重建）----
    private PieceModel _owner;              // 莫娜（OnEquip 注入）
    private HexCoord? _center;              // 领域中心（null = 尚未建立）

    public string UniqueId => null;

    public DomainPassive(int radius, int baseDamage, int attackPercent)
    {
        _radius = Mathf.Max(1, radius);
        _baseDamage = baseDamage;
        _attackPercent = attackPercent;
    }

    /// <summary>领域中心（null = 无领域；调试/后续 UI 可用）</summary>
    public HexCoord? Center => _center;

    // ---- 生命周期 ----

    public void OnEquip(PieceModel owner) => _owner = owner;

    public void OnUnequip(PieceModel owner)
    {
        _owner = null;
        _center = null;
    }

    // ---- 普攻命中：建立/移动领域 ----

    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null)
    {
        if (owner == null || target == null) return;
        bool moved = _center.HasValue && _center.Value != target.Coord;
        _center = target.Coord;
        Debug.Log(moved
            ? $"[Domain] {owner.Data.displayName} 普攻命中，领域迁移至 {target.Coord}（半径 {_radius}，旧领域消失）"
            : $"[Domain] {owner.Data.displayName} 普攻命中，领域落于 {target.Coord}（半径 {_radius}，下回合开始结算）");
    }

    // ---- 回合开始：领域结算 ----

    public void OnTurnStart(PieceModel owner)
    {
        if (_center == null || owner == null || owner.IsDead) return;
        var board = ChessBoardController.Instance?.Model;
        if (board == null || PieceManager.Instance == null) return;

        // 快照收集受害者：领域半径内存活敌方（结算可能致死修改棋子列表）
        var enemySide = owner.Owner == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;
        var enemies = TurnManager.Instance?.Model?.GetPieces(enemySide);
        var victims = new List<PieceModel>();
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead || enemy.IsDestroyed) continue;
                if (_center.Value.Distance(enemy.Coord) <= _radius)
                    victims.Add(enemy);
            }
        }
        if (victims.Count == 0) return;

        int attackValue = _baseDamage + Mathf.RoundToInt(owner.EffectiveAttack * (_attackPercent / 100f));
        ElementType element = owner.Data != null ? owner.Data.innateElement : ElementType.None;
        int hitCount = 0;

        foreach (var victim in victims)
        {
            // 物理 + 主人先天元素（水）：CalculateWithElement 含反应倍率/额外伤害（蒸发/感电/冻结）
            int damage;
            ElementReactionTable.ReactionConfig reaction = default;
            if (element != ElementType.None)
            {
                (damage, reaction) = DamageCalculator.CalculateWithElement(
                    attackValue, victim.EffectiveDefense, element, victim.AffixedElement);
            }
            else
            {
                damage = Mathf.Max(1, attackValue - victim.EffectiveDefense);
            }

            // 统一入口（减伤/易伤/首伤=1/死亡之蔑/反甲/OnDamageReceived 全走）+ 元素附着（gauge=1 普攻口径）+ 金币双边
            PieceManager.Instance.ApplyIncomingDamage(victim, damage, owner, DamageSource.Dot, DamageKind.Physical, element);
            PieceManager.Instance.ApplyElementInteraction(victim, owner, element, 1, reaction);
            GoldManager.Instance?.OnDamageDealt(owner, damage);
            GoldManager.Instance?.OnDamageReceived(victim, damage);

            if (victim.IsDead)
                PieceManager.Instance.DestroyPiece(victim, owner);

            string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
            Debug.Log($"[Domain] 领域（{_center.Value} 半径 {_radius}）命中 {victim.Data.displayName}，" +
                $"造成 {damage} 伤害{reactionStr}（剩余 {victim.CurrentHP}）");
            hitCount++;
        }

        Debug.Log($"[Domain] 领域结算完成：命中 {hitCount} 个敌人（中心 {_center.Value}）");
    }

    // ---- 其余钩子：领域不消费 ----
    public void OnDamageReceived(PieceModel owner, int damage,
        DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical) { }
    public void OnKill(PieceModel owner, PieceModel victim) { }
    public void OnAllyDeath(PieceModel owner, PieceModel ally) { }
}
