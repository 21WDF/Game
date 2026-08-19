using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 范围溅射攻击（可配置溅射系统）—— 先在 effectiveRange 内找可攻击目标位，选定目标后以目标为中心溅射。
/// · target=null（高亮阶段）：返回 effectiveRange 半径内的格子（不含 from）——即可选目标位
/// · target 非 null（执行阶段）：返回主目标格 + splashRadius 半径内的格子（未过滤，由 BattleController
///   按 filterByAttackRange 过滤并结算溅射伤害）
///
/// 溅射参数（从 jsonParams 解析，见 <see cref="AreaSplashParams"/>）：
///   splashRadius        — 溅射半径（0=无溅射，等同 CircleAttackProvider）
///   filterByAttackRange — true=溅射格须在攻击方有效攻击范围内（effectiveRange）；false=无视射程全溅射
///   splashIsElemental   — true=溅射走 AttackPiece（元素附着+反应+伤害；skipGold/skipEnergy 跳过内部金币与能量，金币改由 splashGoldPercent 手动结算）；
///                         false=纯公式伤害+TakeDamage（不附着元素、不触发反应、不给能量）
///   splashGoldPercent   — 溅射伤害转金币比例（0.0~1.0）。两分支均手动 AddGold(基础溅射伤害×splashGoldPercent)：
///                         元素分支 skipGold=true 跳过 AttackPiece 内部金币后手动加；非元素分支公式算 dmg 后手动加。0 时不产生金币调用。
///   baseDamage / attackPercent — 溅射伤害公式：max(1, baseDamage + EffectiveAttack×attackPercent - 目标 EffectiveDefense)
/// </summary>
public class AreaAttackProvider : IAttackRangeProvider
{
    /// <summary>溅射参数（[Serializable] 供 JsonUtility 解析；字段默认值匹配 spec 默认）。
    /// 使用 class（引用类型）以便 JsonUtility.FromJsonOverwrite 原地覆盖、保留未指定字段的默认值。</summary>
    [System.Serializable]
    public class AreaSplashParams
    {
        public int splashRadius = 1;
        public bool filterByAttackRange = true;
        public bool splashIsElemental = false;
        public float splashGoldPercent = 0f;
        public int baseDamage = 0;
        public float attackPercent = 0f;
    }

    private readonly int _splashRadius;
    private readonly bool _filterByAttackRange;
    private readonly bool _splashIsElemental;
    private readonly float _splashGoldPercent;
    private readonly int _baseDamage;
    private readonly float _attackPercent;

    public AreaAttackProvider(AreaSplashParams p)
    {
        _splashRadius = p != null ? p.splashRadius : 0;
        _filterByAttackRange = p != null ? p.filterByAttackRange : true;
        _splashIsElemental = p != null ? p.splashIsElemental : false;
        _splashGoldPercent = p != null ? p.splashGoldPercent : 0f;
        _baseDamage = p != null ? p.baseDamage : 0;
        _attackPercent = p != null ? p.attackPercent : 0f;
    }

    /// <summary>是否有溅射（splashRadius &gt; 0）。false 时 GetAttackZone 执行阶段仅返回主目标格。</summary>
    public bool IsSplash => _splashRadius > 0;
    public int SplashRadius => _splashRadius;
    public bool FilterByAttackRange => _filterByAttackRange;
    public bool SplashIsElemental => _splashIsElemental;
    public float SplashGoldPercent => _splashGoldPercent;
    public int BaseDamage => _baseDamage;
    public float AttackPercent => _attackPercent;

    public List<HexCoord> GetAttackZone(HexCoord from, int effectiveRange, HexCoord? target, ChessBoardModel model)
    {
        var result = new List<HexCoord>();

        if (!target.HasValue)
        {
            // 高亮阶段：显示 effectiveRange 内的可选目标位（不含 from 自身）
            foreach (var offset in HexCoord.AllCoordsInRadius(effectiveRange))
            {
                if (offset.q == 0 && offset.r == 0) continue;
                var coord = new HexCoord(from.q + offset.q, from.r + offset.r);
                if (model.Contains(coord)) result.Add(coord);
            }
        }
        else
        {
            // 执行阶段：主目标格 + splashRadius 半径内的格子（未过滤，由 BattleController 按 filterByAttackRange 过滤）
            // splashRadius=0 时 AllCoordsInRadius(0) 仅含中心 → 只返回主目标格（等同 CircleAttackProvider，无溅射）
            var t = target.Value;
            foreach (var offset in HexCoord.AllCoordsInRadius(_splashRadius))
            {
                var coord = new HexCoord(t.q + offset.q, t.r + offset.r);
                if (model.Contains(coord)) result.Add(coord);
            }
        }

        return result;
    }
}
