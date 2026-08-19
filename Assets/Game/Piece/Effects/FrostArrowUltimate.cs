using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 霜华贯矢大招（指定格模式，ultimateConfig.targetMode=Tile，甘雨·蓄力系统）——
/// 远程攻击指定位置，按当前蓄力层数（PieceModel.ChargeStacks，释放时读取，UseUltimateCore 在 Execute 后清零）分三档：
///   - 1 层：仅目标格物理伤害（EffectiveAttack + physicalBonusDamage，走冰元素反应口径同烈焰斩）
///   - 2 层：目标格及其周围 1 格物理伤害（同上口径，每敌独立结算）
///   - 3 层：目标格及其周围 2 格魔法伤害（magicBaseDamage + 攻击×magicAttackPercent%，
///     不吃防御减伤；带冰元素时魔法伤害也吃反应倍率/额外伤害，同雷殛横光强化态口径）
///
/// 全档默认带先天冰元素（gauge=2 同大招募发量；附着 + 反应状态副作用——超导减防/感电DoT/冻结）。
/// 金币实时双边（OnDamageDealt/OnDamageReceived，同烈焰斩口径）。
/// 死亡销毁：范围内死者由本效果统一 DestroyPiece；点击格（targetCoord）上的棋子若死亡
/// 留给 UseUltimateCore 尾部统一销毁（DestroyPiece 无幂等保护，防双重销毁）。
///
/// 构造参数：physicalBonusDamage（1/2 层物理附加攻击力）、
///           magicBaseDamage（3 层魔法基础值）、magicAttackPercent（3 层攻击百分比）。
/// 数值待 playtest 填写（默认 0）。
/// </summary>
public class FrostArrowUltimate : IUltimateEffect
{
    private readonly int _physicalBonusDamage;   // 1/2 层：附加在 EffectiveAttack 上的额外攻击力（物理，走冰元素反应）
    private readonly int _magicBaseDamage;       // 3 层：魔法基础伤害值
    private readonly int _magicAttackPercent;    // 3 层：攻击力百分比（0=不吃攻击加成）

    public FrostArrowUltimate(int physicalBonusDamage, int magicBaseDamage, int magicAttackPercent)
    {
        _physicalBonusDamage = physicalBonusDamage;
        _magicBaseDamage = magicBaseDamage;
        _magicAttackPercent = magicAttackPercent;
    }

    /// <summary>敌人/自身模式入口：本大招为指定格模式，不通过两参入口执行（留空防误用）</summary>
    public void Execute(PieceModel caster, PieceModel target)
    {
        Debug.LogWarning("[FrostArrow] 霜华贯矢为指定格模式大招，请通过三参 Execute(caster, target, targetCoord) 执行");
    }

    /// <summary>指定格模式入口：targetCoord 为玩家点选的目标格（各档范围的中心格）；
    /// target 为该格上的棋子（可为 null，不参与结算——范围内所有敌人统一处理）</summary>
    public void Execute(PieceModel caster, PieceModel target, HexCoord targetCoord)
    {
        if (caster == null || caster.Data == null) return;
        if (ChessBoardController.Instance == null || PieceManager.Instance == null) return;
        var board = ChessBoardController.Instance.Model;
        if (board == null) return;

        int stacks = caster.ChargeStacks;   // 释放时层数（UseUltimateCore 在 Execute 后清零）
        // 档位范围与伤害类型：1 层单点物理 / 2 层周围 1 格物理 / 3 层周围 2 格魔法
        bool magical = stacks >= 3;
        int radius = stacks <= 1 ? 0 : stacks == 2 ? 1 : 2;
        List<HexCoord> zone = GetRadiusCoords(targetCoord, radius, board);

        Debug.Log($"[FrostArrow] {caster.Data.displayName} 释放霜华贯矢：蓄力 {stacks} 层" +
                  $"（{(magical ? $"周围{radius}格·魔法" : radius == 0 ? "单点·物理" : $"周围{radius}格·物理")}），" +
                  $"目标格 {targetCoord}，范围 {zone.Count} 格");

        int hitCount = 0;
        foreach (var coord in zone)
        {
            var enemy = PieceLayoutModel.Instance?.GetPieceAt(coord);
            if (enemy == null || enemy.IsDead || enemy.Owner == caster.Owner) continue;

            ElementType element = caster.Data.innateElement;   // 全档默认带先天冰元素
            int damage;
            ElementReactionTable.ReactionConfig reaction = default;
            if (magical)
            {
                // ---- 3 层：魔法伤害（不吃防御；带元素时吃反应倍率/额外伤害，同雷殛横光强化态口径）----
                float raw = _magicBaseDamage + caster.EffectiveAttack * (_magicAttackPercent / 100f);
                if (element != ElementType.None)
                {
                    reaction = ElementReactionTable.GetReaction(enemy.AffixedElement, element);
                    damage = Mathf.Max(1, Mathf.RoundToInt(raw * reaction.DamageMultiplier) + reaction.ExtraDamage);
                }
                else
                    damage = Mathf.Max(1, Mathf.RoundToInt(raw));

                PieceManager.Instance.ApplyIncomingDamage(enemy, damage, caster,
                    DamageSource.Ultimate, DamageKind.Magical, element);
                PieceManager.Instance.ApplyElementInteraction(enemy, caster, element, 2, reaction);
                GoldManager.Instance?.OnDamageDealt(caster, damage);
                GoldManager.Instance?.OnDamageReceived(enemy, damage);
            }
            else
            {
                // ---- 1/2 层：物理伤害 + 冰元素反应（同烈焰斩口径，每个受击者独立结算）----
                (damage, reaction) = DamageCalculator.CalculateWithElement(
                    caster.EffectiveAttack + _physicalBonusDamage,
                    enemy.EffectiveDefense,
                    element,
                    enemy.AffixedElement);
                PieceManager.Instance.ApplyIncomingDamage(enemy, damage, caster,
                    DamageSource.Ultimate, DamageKind.Physical, element);
                PieceManager.Instance.ApplyElementInteraction(enemy, caster, element, 2, reaction);
                GoldManager.Instance?.OnDamageDealt(caster, damage);
                GoldManager.Instance?.OnDamageReceived(enemy, damage);
            }

            // 致死者销毁（排除点击格棋子——留给 UseUltimateCore 尾部，防双重销毁）
            if (enemy.IsDead && !enemy.Coord.Equals(targetCoord))
                PieceManager.Instance.DestroyPiece(enemy, caster);
            string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
            string kindStr = magical ? "魔法伤害" : "伤害";
            Debug.Log($"[FrostArrow] 命中 {enemy.Data.displayName}，造成 {damage} {kindStr}{reactionStr}（剩余 {enemy.CurrentHP}）");
            hitCount++;
        }

        Debug.Log($"[FrostArrow] 霜华贯矢结算完成：命中 {hitCount} 个敌人，蓄力层数已清零");
    }

    /// <summary>收集 center 及其周围 radius 格内、棋盘内的坐标（圆形，含中心格；radius=0 即单点）</summary>
    private static List<HexCoord> GetRadiusCoords(HexCoord center, int radius, ChessBoardModel board)
    {
        var result = new List<HexCoord>();
        foreach (var offset in HexCoord.AllCoordsInRadius(radius))
        {
            var coord = new HexCoord(center.q + offset.q, center.r + offset.r);
            if (board.Contains(coord)) result.Add(coord);
        }
        return result;
    }
}
