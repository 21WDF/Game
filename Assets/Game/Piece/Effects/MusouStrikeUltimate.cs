using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无想一刀大招（指定格模式，ultimateConfig.targetMode=Tile，雷电将军）——
///   1. 即时段：对指定位置的「横向 3 格」（环切线：目标格 + 同环左右 2 邻居，
///      复用 RingTangentProvider.GetAttackZone——同菲林斯雷殛横光普通态几何）内敌方
///      造成物理伤害（攻击×attackPercent%，走雷元素反应口径：CalculateWithElement 含倍率/额外伤害 +
///      附着 gauge 2 + 金币双边）
///   2. 持续段：激活协同状态（CoordinatedStrikePassive.Activate(synergyTurns)）——
///      此后 synergyTurns（6）回合内：雷电将军普攻全体己方回能、己方攻击命中触发协同攻击
///      （详见被动类；激活放在伤害段之后——本次释放的命中不触发自己的协同）
///
/// 死亡销毁：范围内死者由本效果统一 DestroyPiece；点击格（targetCoord）上的棋子若死亡
/// 留给 UseUltimateCore 尾部统一销毁（防双重销毁，同 ThunderSweep 口径）。
/// 构造参数（雷电将军资产 effectJsonParams）：attackPercent（横向段攻击百分比，默认 100）、
/// synergyTurns（协同状态持续回合，默认 6；协同伤害/回能参数在 CoordinatedStrikePassive 侧 jsonParams）。
/// </summary>
public class MusouStrikeUltimate : IUltimateEffect
{
    private readonly int _attackPercent;    // 横向段攻击力百分比
    private readonly int _synergyTurns;     // 协同状态持续回合

    public MusouStrikeUltimate(int attackPercent, int synergyTurns)
    {
        _attackPercent = attackPercent;
        _synergyTurns = Mathf.Max(1, synergyTurns);
    }

    /// <summary>敌人/自身模式入口：本大招为指定格模式，不通过两参入口执行（留空防误用）</summary>
    public void Execute(PieceModel caster, PieceModel target)
    {
        Debug.LogWarning("[MusouStrike] 无想一刀为指定格模式大招，请通过三参 Execute(caster, target, targetCoord) 执行");
    }

    /// <summary>指定格模式入口：targetCoord 为玩家点选的目标格（横向 3 格的中心格）；
    /// target 为该格上的棋子（可为 null，不参与结算——范围内所有敌人统一处理）</summary>
    public void Execute(PieceModel caster, PieceModel target, HexCoord targetCoord)
    {
        if (caster == null || caster.Data == null) return;
        if (PieceManager.Instance == null) return;
        var board = ChessBoardController.Instance?.Model;
        if (board == null) return;

        // 横向 3 格：环切线（目标格 + 同环左右 2 邻居）——与菲林斯普通态同几何
        List<HexCoord> zone = new RingTangentProvider().GetAttackZone(caster.Coord, 1, targetCoord, board);

        int attackValue = Mathf.RoundToInt(caster.EffectiveAttack * (_attackPercent / 100f));
        ElementType element = caster.Data.innateElement;
        int hitCount = 0;

        foreach (var coord in zone)
        {
            var enemy = PieceLayoutModel.Instance?.GetPieceAt(coord);
            if (enemy == null || enemy.IsDead || enemy.Owner == caster.Owner) continue;

            // 物理 + 雷元素反应口径（同菲林斯普通态）
            var (damage, reaction) = DamageCalculator.CalculateWithElement(
                attackValue, enemy.EffectiveDefense, element, enemy.AffixedElement);
            PieceManager.Instance.ApplyIncomingDamage(enemy, damage, caster,
                DamageSource.Ultimate, DamageKind.Physical, element);
            PieceManager.Instance.ApplyElementInteraction(enemy, caster, element, 2, reaction);
            GoldManager.Instance?.OnDamageDealt(caster, damage);
            GoldManager.Instance?.OnDamageReceived(enemy, damage);

            if (enemy.IsDead && !enemy.Coord.Equals(targetCoord))
                PieceManager.Instance.DestroyPiece(enemy, caster);
            string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
            Debug.Log($"[MusouStrike] 命中 {enemy.Data.displayName}，造成 {damage} 伤害{reactionStr}（剩余 {enemy.CurrentHP}）");
            hitCount++;
        }

        // 激活协同状态（伤害段之后——本次大招命中不触发自己的协同）
        foreach (var passive in caster.GetAllPassives())
        {
            if (passive is CoordinatedStrikePassive synergy)
            {
                synergy.Activate(_synergyTurns);
                Debug.Log($"[MusouStrike] 协同状态激活：持续 {_synergyTurns} 回合" +
                          $"（雷电将军普攻全体己方回能；己方攻击命中触发协同攻击）");
                break;
            }
        }

        Debug.Log($"[MusouStrike] {caster.Data.displayName} 释放无想一刀（{targetCoord} 横向 3 格），命中 {hitCount} 个敌人");
    }
}
