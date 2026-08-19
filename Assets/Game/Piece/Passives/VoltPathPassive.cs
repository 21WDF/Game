using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 雷径惩戒被动（克洛琳德内建被动）—— 移动时，移动路径飞越的敌方棋子受到一段物理伤害（每个敌人一次）。
///
/// 触发口径：PieceManager.MovePieceAlongPath 在移动开始时调用 TriggerPathDamage（普通移动与
/// 大招冲刺共用此入口 → 冲刺同样触发，与「冲锋切入」定位自洽）。
/// 几何重算：路径列表只含空格（StraightPass 的 FindPath 不含被飞越的被占格——动画滑过成果），
/// 被飞越敌格 = 相邻路径节点间的共线缺口（节点距离 &gt; 1 时，两点之间射线上的格子）。
/// 每敌一次：整次移动 HashSet 去重（多段途经点路径也不重复结算）。
///
/// 伤害公式：
///   带元素（elemental=true，默认）：走元素反应口径（同烈焰斩）——
///     CalculateWithElement(baseDamage + 攻击×attackPercent%, 防御, 先天元素, 附着元素)
///     = max(1, round(max(1, 攻击值−防御) × 反应倍率) + 额外伤害)；
///     随后附着先天元素（gauge=1 同普攻口径）并结算反应状态副作用（超导减防/感电DoT/冻结）。
///   不带元素（elemental=false）：旧口径纯公式 max(1, baseDamage + 攻击×% − 防御)，不附着不反应。
/// 走统一伤害入口（DamageSource.Splash / DamageKind.Physical，白色跳字）：含减伤/首伤=1/死亡延迟/
/// 免死/OnDamageReceived；不给能量；致死者 DestroyPiece（killer=owner）。
/// </summary>
public class VoltPathPassive : IPassiveEffect
{
    private readonly int _baseDamage;      // 基础伤害值
    private readonly int _attackPercent;   // 攻击力百分比（0=不吃攻击加成）
    private readonly bool _elemental;      // 是否带先天元素（默认 true；false=旧口径不附着不反应）

    public VoltPathPassive(int baseDamage, int attackPercent, bool elemental)
    {
        _baseDamage = baseDamage;
        _attackPercent = attackPercent;
        _elemental = elemental;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>移动路径伤害（MovePieceAlongPath 移动开始时调用）：
    /// 遍历相邻路径节点，节点间共线缺口（距离 &gt; 1）即被飞越的格子，其上敌方受物理伤害。</summary>
    public void TriggerPathDamage(PieceModel owner, List<HexCoord> path)
    {
        if (owner == null || owner.Data == null || path == null || path.Count < 2) return;
        if (PieceManager.Instance == null) return;

        var damaged = new HashSet<PieceModel>();   // 每个敌方棋子一次（整次移动去重）
        for (int i = 0; i + 1 < path.Count; i++)
        {
            HexCoord a = path[i];
            HexCoord b = path[i + 1];
            int dir = StraightMoveProvider.RayDirection(a, b);
            if (dir < 0) continue;   // 不共线（防御；现有路径生成不会出现）

            // 沿 a→b 射线走，严格介于两点之间的格子 = 被飞越的缺口格
            HexCoord cur = a;
            while (cur != b)
            {
                cur = cur.Neighbor(dir);
                if (cur == b) break;

                var enemy = PieceLayoutModel.Instance?.GetPieceAt(cur);
                if (enemy == null || enemy.IsDead || enemy.Owner == owner.Owner) continue;
                if (!damaged.Add(enemy)) continue;   // 本次移动已结算过

                // 带元素 → 走元素反应口径（同烈焰斩：倍率/额外伤害计入伤害）；
                // 否则旧口径纯公式。元素=None（无元素棋子）时天然回落旧口径。
                ElementType element = _elemental ? owner.Data.innateElement : ElementType.None;
                int damage;
                ElementReactionTable.ReactionConfig reaction = default;
                if (element != ElementType.None)
                {
                    int attackValue = _baseDamage + Mathf.RoundToInt(
                        owner.EffectiveAttack * (_attackPercent / 100f));
                    (damage, reaction) = DamageCalculator.CalculateWithElement(
                        attackValue, enemy.EffectiveDefense, element, enemy.AffixedElement);
                }
                else
                {
                    float raw = _baseDamage + owner.EffectiveAttack * (_attackPercent / 100f) - enemy.EffectiveDefense;
                    damage = Mathf.Max(1, Mathf.RoundToInt(raw));
                }

                PieceManager.Instance.ApplyIncomingDamage(enemy, damage, owner,
                    DamageSource.Splash, DamageKind.Physical, element);
                // 元素附着 + 反应状态副作用（gauge=1 同普攻；超导减防/感电DoT/冻结）
                PieceManager.Instance.ApplyElementInteraction(enemy, owner, element, 1, reaction);

                if (enemy.IsDead)
                    PieceManager.Instance.DestroyPiece(enemy, owner);

                string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
                Debug.Log($"[VoltPath] {owner.Data.displayName} 飞越 {enemy.Data.displayName}，" +
                          $"造成 {damage} 伤害{reactionStr}（剩余 {enemy.CurrentHP}）");
            }
        }
    }
}
