using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 雷驰突进大招（指定格模式，ultimateConfig.targetMode=Tile + tileTargetShape=Ray，克洛琳德）——
/// 向指定位置直线冲刺（移动过去），并回复 healAmount 点生命值。
///
/// 瞄准：Ray 形状高亮 = 6 方向射线上的空格（tileTargetRange 为射程；被占格不可选，
/// 点击被占格自然落入「非目标格 → 取消」分支，不消耗能量/AP）。
/// 冲刺 = 一次移动：复用 PieceManager.MovePieceAlongPath（含逐格动画/占据表/动画锁，
/// skipEnergy=true 不重复充能）——因此自然触发雷径惩戒被动（飞越的敌人受路径伤害）。
/// 路径由 StraightPassProvider.FindPath 生成（与她的移动规则同源：直线穿越，被飞越敌格不入路径）。
/// target 为点击格上的棋子（Ray 瞄准保证为 null 空格；防御性校验被占则不执行）。
/// 构造参数：healAmount（冲刺后回血量）。
/// </summary>
public class ThunderDashUltimate : IUltimateEffect, IUltimateAreaProvider
{
    private readonly int _healAmount;    // 冲刺后回血量

    public ThunderDashUltimate(int healAmount)
    {
        _healAmount = Mathf.Max(0, healAmount);
    }

    /// <summary>范围声明（元素格子统一入口）：直线冲刺型 = 起点到落点整条直线上的所有格
    ///（与 Execute 同源的直线几何；不带阻挡过滤——元素格范围是完整几何，不因飞越敌人而缺格）</summary>
    public List<HexCoord> GetUltimateArea(PieceModel caster, PieceModel target, HexCoord? targetCoord)
    {
        var area = new List<HexCoord>();
        if (caster == null || caster.Data == null || !targetCoord.HasValue) return area;
        var board = ChessBoardController.Instance != null ? ChessBoardController.Instance.Model : null;
        if (board == null) return area;
        return new StraightPassProvider().FindPath(caster.Coord, targetCoord.Value, null, board)
               ?? new List<HexCoord>();
    }

    /// <summary>敌人/自身模式入口：本大招为指定格模式，不通过两参入口执行（留空防误用）</summary>
    public void Execute(PieceModel caster, PieceModel target)
    {
        Debug.LogWarning("[ThunderDash] 雷驰突进为指定格模式大招，请通过三参 Execute(caster, target, targetCoord) 执行");
    }

    /// <summary>指定格模式入口：targetCoord 为冲刺落点（Ray 瞄准保证为直线上的空格）</summary>
    public void Execute(PieceModel caster, PieceModel target, HexCoord targetCoord)
    {
        if (caster == null || caster.Data == null) return;
        if (PieceManager.Instance == null || ChessBoardController.Instance == null) return;

        // 防御校验：落点必须空格（Ray 瞄准已过滤；此处兜底防配置错用 Circle 形状）
        if (!ReferenceEquals(target, null))
        {
            Debug.LogWarning("[ThunderDash] 冲刺落点被占据，取消突进（请将 tileTargetShape 配置为 Ray）");
            return;
        }

        // 路径：与她的移动规则同源（直线穿越；被飞越敌格不入路径，由被动按缺口几何重算）
        HexCoord from = caster.Coord;
        Func<HexCoord, bool> isBlocked = c => PieceLayoutModel.Instance?.GetPieceAt(c) != null;
        List<HexCoord> path = new StraightPassProvider().FindPath(
            from, targetCoord, isBlocked, ChessBoardController.Instance.Model);
        if (path == null || path.Count < 2)
        {
            Debug.LogWarning("[ThunderDash] 冲刺落点不在直线上，取消突进");
            return;
        }

        // 回血（先回血后冲刺；封顶 MaxHP）
        if (_healAmount > 0)
            caster.Heal(_healAmount);

        // 冲刺 = 一次移动：复用移动管道（逐格动画 + 占据表 + 动画锁；skipEnergy=true 不重复充能）。
        // MovePieceAlongPath 内部触发雷径惩戒被动 → 飞越的敌人受路径伤害。
        PieceManager.Instance.MovePieceAlongPath(caster, path, skipEnergy: true);

        Debug.Log($"[ThunderDash] {caster.Data.displayName} 雷驰突进 {from} -> {targetCoord}" +
                  $"{(_healAmount > 0 ? $"，回复 {_healAmount} 生命（剩余 {caster.CurrentHP}）" : "")}");
    }
}
