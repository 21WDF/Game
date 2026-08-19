using UnityEngine;

/// <summary>
/// 兔兔伯爵大招（指定格模式，ultimateConfig.targetMode=Tile，安柏·召唤嘲讽）——
/// 在指定位置（须为空格）投放傀儡棋子（临时召唤物，占据该格，非玩家可操作）。
///
/// 傀儡行为全部由傀儡资产的 PuppetPassive（builtInPassives）驱动：
///   - 嘲讽：攻击时距傀儡 ≤ tauntRange（2）格的敌方只能攻击傀儡（BattleController 高亮过滤 + 校验）
///   - 引爆三条件（任一满足）：受击 hitsToDetonate（3）次 / 持续 maxTurns（6）回合 /
///     主动关闭（选中安柏再按 U → BattleController.HandleUltimate 前置 toggle 分支，免费）
///   - 引爆：周围 explosionRadius（1）格敌方物理 + 火元素伤害（数值与阈值都在傀儡资产 jsonParams）
///
/// 本类只负责「召唤」：校验目标格空 → 替换旧傀儡（若有，先引爆）→ SpawnPieceById 生成 →
/// 注入主人引用（PuppetPassive.Init）。
///
/// 构造参数：puppetPieceId（傀儡在 PieceRegistry 中的 id；傀儡资产需配置 isSummon=true + PuppetPassive）。
/// </summary>
public class BaronBunnyUltimate : IUltimateEffect
{
    private readonly int _puppetPieceId;     // 傀儡棋子 id（注册表查找键）

    public BaronBunnyUltimate(int puppetPieceId)
    {
        _puppetPieceId = puppetPieceId;
    }

    /// <summary>敌人/自身模式入口：本大招为指定格模式，不通过两参入口执行（留空防误用）</summary>
    public void Execute(PieceModel caster, PieceModel target)
    {
        Debug.LogWarning("[BaronBunny] 兔兔伯爵为指定格模式大招，请通过三参 Execute(caster, target, targetCoord) 执行");
    }

    /// <summary>指定格模式入口：targetCoord 为投放位置（须为空格）；
    /// target 为该格上的棋子（非 null 即占据 → 校验失败，不投放）</summary>
    public void Execute(PieceModel caster, PieceModel target, HexCoord targetCoord)
    {
        if (caster == null || caster.Data == null) return;
        if (PieceManager.Instance == null) return;

        // 校验：投放格必须为空（傀儡要占据该格；Tile 瞄准高亮含被占格，玩家须点空格）
        if (target != null || PieceLayoutModel.Instance?.GetPieceAt(targetCoord) != null)
        {
            Debug.LogWarning($"[BaronBunny] 目标格 {targetCoord} 被占据，无法投放傀儡（请点空格）");
            return;
        }

        // 同一主人只保留一个傀儡：已有则先引爆旧的（正常路径 U 键 toggle 已拦截，此处防御替换）
        PuppetPassive.FindByCaster(caster)?.Detonate();

        // 生成傀儡（真实 PieceModel：占据格子、可被攻击、注册回合系统供 OnTurnStart 计数）
        var puppet = PieceManager.Instance.SpawnPieceById(_puppetPieceId, targetCoord, caster.Owner);
        if (puppet == null)
        {
            Debug.LogError($"[BaronBunny] 傀儡生成失败：请确认 PieceRegistry 已注册 id={_puppetPieceId} 的傀儡（isSummon=true + PuppetPassive + prefab）");
            return;
        }

        // 注入主人（引爆伤害/击杀归属/嘲讽阵营判断）
        foreach (var passive in puppet.BuiltInPassives)
        {
            if (passive is PuppetPassive p)
            {
                p.Init(caster);
                break;
            }
        }

        Debug.Log($"[BaronBunny] {caster.Data.displayName} 在 {targetCoord} 投放傀儡（嘲讽范围内敌方只能攻击它；3 次受击 / 6 回合 / 再按大招键引爆）");
    }
}
