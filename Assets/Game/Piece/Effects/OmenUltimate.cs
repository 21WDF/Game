using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 星异大招（指定格模式，ultimateConfig.targetMode=Tile，莫娜·易伤）——
/// 对目标位置及周围 radius（2）格内的敌方棋子施加「易伤」：受到的伤害提高 percent%（默认 50），
/// 持续 duration（4）回合（TurnManager.TickElementDuration 每次任一方回合开始递减，≤0 清除）。
///
/// 纯 debuff：本大招不造成伤害、不附着元素——易伤只影响「之后受到的伤害」
/// （结算接入见 PieceManager.ApplyVulnerability：普攻基础段/附加段/统一入口三管道共用），
/// 不干扰本次释放的结算顺序。重复施加 = 刷新持续回合（百分比不叠加）。
///
/// 构造参数（莫娜资产 effectJsonParams）：radius（默认 2）、duration（默认 4）、percent（默认 50）。
/// </summary>
public class OmenUltimate : IUltimateEffect
{
    private readonly int _radius;       // 易伤作用半径（目标格为中心，含中心格）
    private readonly int _duration;     // 持续回合
    private readonly int _percent;      // 增伤百分比（50 = +50%）

    public OmenUltimate(int radius, int duration, int percent)
    {
        _radius = Mathf.Max(0, radius);
        _duration = Mathf.Max(1, duration);
        _percent = Mathf.Max(0, percent);
    }

    /// <summary>敌人/自身模式入口：本大招为指定格模式，不通过两参入口执行（留空防误用）</summary>
    public void Execute(PieceModel caster, PieceModel target)
    {
        Debug.LogWarning("[Omen] 星异为指定格模式大招，请通过三参 Execute(caster, target, targetCoord) 执行");
    }

    /// <summary>指定格模式入口：targetCoord 为玩家点选的目标格；target 为该格上的棋子（可空，不影响效果）</summary>
    public void Execute(PieceModel caster, PieceModel target, HexCoord targetCoord)
    {
        if (caster == null || caster.Data == null) return;

        // 快照收集受害者：目标格周围 radius 内存活敌方（含中心格上的敌方）
        var enemySide = caster.Owner == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;
        var enemies = TurnManager.Instance?.Model?.GetPieces(enemySide);
        var victims = new List<PieceModel>();
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead || enemy.IsDestroyed) continue;
                if (targetCoord.Distance(enemy.Coord) <= _radius)
                    victims.Add(enemy);
            }
        }

        foreach (var victim in victims)
        {
            victim.VulnerableTurnsRemaining = _duration;
            victim.VulnerablePercent = _percent;
            Debug.Log($"[Omen] {victim.Data.displayName} 进入易伤：受到的伤害 +{_percent}%，持续 {_duration} 回合");
        }

        Debug.Log($"[Omen] {caster.Data.displayName} 星异降临 {targetCoord}：周围 {_radius} 格内 {victims.Count} 个敌方进入易伤");
    }
}
