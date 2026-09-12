using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 贯日炎枪大招（指定格模式，ultimateConfig.targetMode=Tile）—— 玩家点选目标格（可为空格）定方向，
/// 沿 caster→目标格 方向归并到最近 hex 方向，对射线上 length 格内所有敌方造成物理伤害（直线穿透，可多目标）。
/// 构造参数：length（射线长度，需与 ultimateConfig.tileTargetRange 保持一致）、bonusDamage（附加攻击力）。
///
/// 伤害结算与烈焰斩（FireSlashUltimate）同口径，每个受击者独立结算：
///   - DamageCalculator.CalculateWithElement：含火元素反应倍率/额外伤害（受防御减伤）
///   - 统一伤害入口 ApplyIncomingDamage（DamageSource.Ultimate / DamageKind.Physical）
///   - 附着攻击方先天元素（大招募发量=2）+ 反应减防
///   - 金币实时到账（双边 OnDamageDealt / OnDamageReceived）
/// 死亡销毁：射线上死者由本效果统一 DestroyPiece；点击格（targetCoord）上的棋子若死亡
/// 留给 UseUltimateCore 尾部统一销毁（DestroyPiece 无幂等保护，防双重销毁）。
/// </summary>
public class FlameLanceUltimate : IUltimateEffect, IUltimateAreaProvider
{
    private readonly int _length;         // 射线长度（格）
    private readonly int _bonusDamage;    // 附加在 EffectiveAttack 上的额外攻击力

    public FlameLanceUltimate(int length, int bonusDamage)
    {
        _length = Mathf.Max(1, length);
        _bonusDamage = bonusDamage;
    }

    /// <summary>范围声明（元素格子统一入口）：直线穿透型 = 整条射线上的所有格
    ///（与 Execute 内同源同参数：ClosestDirection 归并方向 + AppendRay 延伸；含棋盘边界校验）</summary>
    public List<HexCoord> GetUltimateArea(PieceModel caster, PieceModel target, HexCoord? targetCoord)
    {
        var area = new List<HexCoord>();
        if (caster == null || caster.Data == null || !targetCoord.HasValue) return area;
        var board = ChessBoardController.Instance != null ? ChessBoardController.Instance.Model : null;
        if (board == null) return area;
        int dir = LineAttackProvider.ClosestDirection(caster.Coord, targetCoord.Value);
        LineAttackProvider.AppendRay(area, caster.Coord, dir, _length, board);
        return area;
    }

    /// <summary>敌人/自身模式入口：本大招为指定格模式，不通过两参入口执行（留空防误用）</summary>
    public void Execute(PieceModel caster, PieceModel target)
    {
        Debug.LogWarning("[FlameLance] 贯日炎枪为指定格模式大招，请通过三参 Execute(caster, target, targetCoord) 执行");
    }

    /// <summary>指定格模式入口：targetCoord 为玩家点选的目标格（定方向用）；
    /// target 为该格上的棋子（可为 null，不参与结算——射线上所有敌人统一处理）</summary>
    public void Execute(PieceModel caster, PieceModel target, HexCoord targetCoord)
    {
        if (caster == null || caster.Data == null) return;
        if (ChessBoardController.Instance == null || PieceManager.Instance == null) return;
        var board = ChessBoardController.Instance.Model;
        if (board == null) return;

        // 复用 LineAttackProvider 执行阶段语义：沿最接近 targetCoord 方向的 hex 射线延伸 _length 格
        //（含棋盘边界校验；斜向点击归并到最近 hex 方向，点击格本身不一定在射线上）
        var rayCoords = new List<HexCoord>();
        int dir = LineAttackProvider.ClosestDirection(caster.Coord, targetCoord);
        LineAttackProvider.AppendRay(rayCoords, caster.Coord, dir, _length, board);

        int hitCount = 0;
        foreach (var coord in rayCoords)
        {
            var enemy = PieceLayoutModel.Instance?.GetPieceAt(coord);
            if (enemy == null || enemy.IsDead || enemy.Owner == caster.Owner) continue;

            // 伤害计算（含元素反应倍率；减防由 CalculateWithElement 前置查询的反应结果带出，同烈焰斩）
            var (damage, reaction) = DamageCalculator.CalculateWithElement(
                caster.EffectiveAttack + _bonusDamage,
                enemy.EffectiveDefense,
                caster.Data.innateElement,
                enemy.AffixedElement);

            // 统一伤害入口（触发 OnDamageReceived——打断再生计时/反甲等；不走 AttackPiece）
            // 返回 false = 被护盾拦截 → 不给金币；元素附着/反应照常
            bool landed = PieceManager.Instance.ApplyIncomingDamage(enemy, damage, caster, DamageSource.Ultimate, DamageKind.Physical, caster.Data.innateElement);

            // 元素附着与消耗（大招募发量=2，同烈焰斩）
            // 护盾染色封印（元素城邦二期）：已染色护盾封印同元素附着 → 跳过附着写入（反应照常）
            if (caster.Data.innateElement != ElementType.None)
            {
                if (!enemy.BlocksElementAttachment(caster.Data.innateElement))
                {
                    var (resultElem, resultGauge) = ElementReactionTable.ResolveElementInteraction(
                        enemy.AffixedElement, enemy.AffixedElementGauge,
                        caster.Data.innateElement, 2);
                    enemy.AffixedElement = resultElem;
                    enemy.AffixedElementGauge = resultGauge;
                    // 染色：附着成功（结果非 None）→ 未染色护盾变为对应元素盾
                    enemy.TryDyeShieldElement(resultElem);
                }
                if (reaction.DefenseReduction > 0)
                    enemy.CurrentDefenseReduction = reaction.DefenseReduction;
            }

            // 反应视觉脉冲（纯表现，不参与任何结算）：触发元素反应时目标光环脉冲
            if (reaction.Type != ReactionType.None)
                enemy.View?.PulseAura();

            // 金币实时到账（硬约束，同烈焰斩双边口径；护盾拦截时双方都无金币）
            if (landed)
            {
                GoldManager.Instance?.OnDamageDealt(caster, damage);
                GoldManager.Instance?.OnDamageReceived(enemy, damage);
            }

            // 死亡销毁：射线上死者统一处理；点击格棋子留给 UseUltimateCore 尾部（防双重销毁）
            if (enemy.IsDead && !enemy.Coord.Equals(targetCoord))
                PieceManager.Instance.DestroyPiece(enemy, caster);

            string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
            Debug.Log($"[FlameLance] {caster.Data.displayName} 炎枪命中 {enemy.Data.displayName}，" +
                      $"造成 {damage} 伤害{reactionStr}（剩余 {enemy.CurrentHP}）");
            hitCount++;
        }

        Debug.Log($"[FlameLance] {caster.Data.displayName} 沿方向 {dir} 释放贯日炎枪，命中 {hitCount} 个敌人");
    }
}
