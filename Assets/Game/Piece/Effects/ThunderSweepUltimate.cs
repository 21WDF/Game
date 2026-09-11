using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 雷殛横光大招（指定格模式，ultimateConfig.targetMode=Tile，菲林斯）—— 对指定位置释放，
/// 按强化状态（PieceModel.IsEmpowered，普攻命中进入/刷新）切换形态：
///   - 普通态：对目标格的「横向 3 格」内敌方造成物理伤害（复用 RingTangentProvider 执行阶段几何：
///     目标格 + 同一环上的左右 2 个邻居），伤害走雷元素反应口径（同烈焰斩：CalculateWithElement +
///     附着 gauge 2 + 金币实时双边）
///   - 强化态：对指定位置及其周围 1 格内敌方造成魔法伤害（max(1, empoweredBaseDamage +
///     EffectiveAttack × empoweredAttackPercent%)，不吃防御减伤；DamageKind.Magical 紫色跳字）。
///     带元素（empoweredElemental=true，默认）：魔法伤害也吃反应倍率/额外伤害
///     （伤害 = max(1, round(魔法基础 × 倍率) + 额外)，仅免防御），随后附着先天雷元素
///     （gauge=2 同大招募发量）并结算反应状态副作用（超导减防/感电DoT/冻结）；
///     false 时保持旧口径——纯魔法段不附着不反应（同感电 DoT/超载爆炸）。
///
/// 构造参数：normalBonusDamage（普通态物理附加攻击力）、
///           empoweredBaseDamage（强化态魔法基础值）、empoweredAttackPercent（强化态攻击百分比）、
///           empoweredEnergyCost（强化态能量消耗量，保留剩余；普通态全清）、
///           empoweredElemental（强化态是否带元素，默认 true）。
/// 能量机制（IPartialEnergyUltimate）：强化态只消耗 empoweredEnergyCost 并保留剩余；
/// 普通态返回 MaxEnergy 全清——释放条件两态均为满能。
/// 死亡销毁：范围内死者由本效果统一 DestroyPiece；点击格（targetCoord）上的棋子若死亡
/// 留给 UseUltimateCore 尾部统一销毁（DestroyPiece 无幂等保护，防双重销毁）。
/// </summary>
public class ThunderSweepUltimate : IUltimateEffect, IPartialEnergyUltimate, IUltimateAreaProvider
{
    private readonly int _normalBonusDamage;       // 普通态：附加在 EffectiveAttack 上的额外攻击力
    private readonly int _empoweredBaseDamage;     // 强化态：魔法基础伤害值
    private readonly int _empoweredAttackPercent;  // 强化态：攻击力百分比（0=不吃攻击加成）
    private readonly int _empoweredEnergyCost;     // 强化态：能量消耗量（保留剩余）
    private readonly bool _empoweredElemental;     // 强化态：是否带先天元素（默认 true；false=旧纯魔法段口径）

    public ThunderSweepUltimate(int normalBonusDamage, int empoweredBaseDamage,
        int empoweredAttackPercent, int empoweredEnergyCost, bool empoweredElemental)
    {
        _normalBonusDamage = normalBonusDamage;
        _empoweredBaseDamage = empoweredBaseDamage;
        _empoweredAttackPercent = empoweredAttackPercent;
        _empoweredEnergyCost = empoweredEnergyCost;
        _empoweredElemental = empoweredElemental;
    }

    /// <summary>范围声明（元素格子统一入口）：指定格 AOE 型，与 Execute 同源分形态——
    /// 普通态 = RingTangent 横向 3 格；强化态 = 指定格及周围 1 格（圆形含中心）。
    /// 声明先于 Execute 调用，IsEmpowered 形态判断与结算一致</summary>
    public List<HexCoord> GetUltimateArea(PieceModel caster, PieceModel target, HexCoord? targetCoord)
    {
        var area = new List<HexCoord>();
        if (caster == null || caster.Data == null || !targetCoord.HasValue) return area;
        var board = ChessBoardController.Instance != null ? ChessBoardController.Instance.Model : null;
        if (board == null) return area;
        return caster.IsEmpowered
            ? GetRadiusCoords(targetCoord.Value, 1, board)
            : new RingTangentProvider().GetAttackZone(caster.Coord, 1, targetCoord.Value, board);
    }

    /// <summary>本次释放的能量消耗量：强化态 = empoweredEnergyCost（保留剩余）；普通态 = 全清。
    /// 与 Execute 同一调用栈被询问，形态判断一致。</summary>
    public int GetEnergyCost(PieceModel caster)
    {
        if (caster == null || caster.Energy == null) return 0;
        return caster.IsEmpowered
            ? System.Math.Max(0, _empoweredEnergyCost)
            : caster.Energy.MaxEnergy;   // 普通态：全清
    }

    /// <summary>敌人/自身模式入口：本大招为指定格模式，不通过两参入口执行（留空防误用）</summary>
    public void Execute(PieceModel caster, PieceModel target)
    {
        Debug.LogWarning("[ThunderSweep] 雷殛横光为指定格模式大招，请通过三参 Execute(caster, target, targetCoord) 执行");
    }

    /// <summary>指定格模式入口：targetCoord 为玩家点选的目标格（形态范围的中心格）；
    /// target 为该格上的棋子（可为 null，不参与结算——范围内所有敌人统一处理）</summary>
    public void Execute(PieceModel caster, PieceModel target, HexCoord targetCoord)
    {
        Debug.Log($"[ThunderSweep] Execute 入口：caster={(caster == null ? "null" : caster.Data?.displayName)}，targetCoord={targetCoord}");
        if (caster == null || caster.Data == null) return;
        if (ChessBoardController.Instance == null || PieceManager.Instance == null) return;
        var board = ChessBoardController.Instance.Model;
        if (board == null) return;

        bool empowered = caster.IsEmpowered;
        // 形态范围：普通态 = RingTangent 执行阶段（目标格 + 同环左右 2 邻居，横向 3 格）；
        //           强化态 = 指定格及其周围 1 格（圆形半径 1，含中心格）
        List<HexCoord> zone = empowered
            ? GetRadiusCoords(targetCoord, 1, board)
            : new RingTangentProvider().GetAttackZone(caster.Coord, 1, targetCoord, board);

        // 诊断日志（范围失效定位用）：形态 + 释放者/目标格坐标 + 范围坐标列表 + 每格占据查询结果
        var diag = new System.Text.StringBuilder();
        foreach (var c in zone)
        {
            var occ = PieceLayoutModel.Instance?.GetPieceAt(c);
            diag.Append(c).Append('=')
                .Append(occ == null ? "空格" : occ.Owner == caster.Owner ? "己方" : $"敌方{occ.Data?.displayName}")
                .Append("; ");
        }
        Debug.Log($"[ThunderSweep] 形态={(empowered ? $"强化（剩余{caster.EmpoweredTurnsRemaining}回合）" : "普通")}，" +
                  $"释放者={caster.Coord}，目标格={targetCoord}，范围{zone.Count}格：{diag}");

        int hitCount = 0;
        foreach (var coord in zone)
        {
            var enemy = PieceLayoutModel.Instance?.GetPieceAt(coord);
            if (enemy == null || enemy.IsDead || enemy.Owner == caster.Owner) continue;

            if (empowered)
            {
                // ---- 强化态：魔法伤害（攻击加成不吃防；带元素时魔法伤害也吃反应倍率/额外伤害）----
                float raw = _empoweredBaseDamage + caster.EffectiveAttack * (_empoweredAttackPercent / 100f);
                ElementType element = _empoweredElemental ? caster.Data.innateElement : ElementType.None;
                int damage;
                ElementReactionTable.ReactionConfig reaction = default;
                if (element != ElementType.None)
                {
                    reaction = ElementReactionTable.GetReaction(enemy.AffixedElement, element);
                    damage = Mathf.Max(1, Mathf.RoundToInt(raw * reaction.DamageMultiplier) + reaction.ExtraDamage);
                }
                else
                    damage = Mathf.Max(1, Mathf.RoundToInt(raw));

                // 返回 false = 被护盾拦截 → 不给金币（免伤 = 无伤害收益）；元素附着/反应照常
                bool landed = PieceManager.Instance.ApplyIncomingDamage(enemy, damage, caster,
                    DamageSource.Ultimate, DamageKind.Magical, element);
                // 元素附着 + 反应状态副作用（gauge=2 同大招募发量；超导减防/感电DoT/冻结）
                PieceManager.Instance.ApplyElementInteraction(enemy, caster, element, 2, reaction);
                if (landed)
                {
                    GoldManager.Instance?.OnDamageDealt(caster, damage);
                    GoldManager.Instance?.OnDamageReceived(enemy, damage);
                }

                if (enemy.IsDead && !enemy.Coord.Equals(targetCoord))
                    PieceManager.Instance.DestroyPiece(enemy, caster);
                string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
                Debug.Log($"[ThunderSweep] 强化态命中 {enemy.Data.displayName}，造成 {damage} 魔法伤害{reactionStr}（剩余 {enemy.CurrentHP}）");
            }
            else
            {
                // ---- 普通态：物理伤害 + 雷元素反应（同烈焰斩口径，每个受击者独立结算）----
                var (damage, reaction) = DamageCalculator.CalculateWithElement(
                    caster.EffectiveAttack + _normalBonusDamage,
                    enemy.EffectiveDefense,
                    caster.Data.innateElement,
                    enemy.AffixedElement);
                // 返回 false = 被护盾拦截 → 不给金币；元素附着/反应照常
                bool landed = PieceManager.Instance.ApplyIncomingDamage(enemy, damage, caster,
                    DamageSource.Ultimate, DamageKind.Physical, caster.Data.innateElement);

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

                if (landed)
                {
                    GoldManager.Instance?.OnDamageDealt(caster, damage);
                    GoldManager.Instance?.OnDamageReceived(enemy, damage);
                }

                if (enemy.IsDead && !enemy.Coord.Equals(targetCoord))
                    PieceManager.Instance.DestroyPiece(enemy, caster);
                string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
                Debug.Log($"[ThunderSweep] 普通态命中 {enemy.Data.displayName}，造成 {damage} 伤害{reactionStr}（剩余 {enemy.CurrentHP}）");
            }
            hitCount++;
        }

        Debug.Log($"[ThunderSweep] {caster.Data.displayName} 释放雷殛横光（{(empowered ? "强化态·周围1格魔法" : "普通态·横向3格物理")}），" +
                  $"命中 {hitCount} 个敌人，能量剩余 {caster.Energy.CurrentEnergy}/{caster.Energy.MaxEnergy}");
    }

    /// <summary>收集 center 及其周围 radius 格内、棋盘内的坐标（圆形，含中心格）</summary>
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
