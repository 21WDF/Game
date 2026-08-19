using UnityEngine;

/// <summary>
/// 四元素之力（潮涌/灼火/陨雷/凝冰）—— 元素反应修饰核心（③.2 只做 Modify 部分）。
/// 四件装备共用本类，element 参数区分元素；uniqueId 统一为 "ElementalCore"
/// （唯一被动，走 C2 框架：同棋子后装的元素之力覆盖先装的）。
///
/// Modify 按 element + 反应类型改字段（倍率加法 += 0.2f，与光界之力口径一致）：
///   Water（潮涌）  ：Vaporize 倍率+0.2；ElectroCharged DoT+2 回合；Frozen 冻结+1 回合
///   Fire（灼火）   ：Vaporize / Melt 倍率+0.2
///   Thunder（陨雷）：Superconduct 减防+5 且持续+2 回合；ElectroCharged DoT+2 回合
///   Ice（凝冰）    ：Superconduct 减防+5 且持续+2 回合；Melt 倍率+0.2；Frozen 冻结+1 回合
///
/// CollectReactionModifiers 从攻击方收集 → 天然限定「携带者作为触发方」才生效。
/// ③.3 扩展：元素量加成（对应元素普攻额外元素量，PieceManager 元素附着处介入）、
/// 超载爆炸 AOE（仅 Fire/Thunder，CanExplode；对目标周围敌方造成真实伤害）。
/// </summary>
public class ElementalCorePassive : IPassiveEffect, IReactionModifier
{
    private readonly ElementType _element;
    private readonly string _uniqueId;
    private readonly int _multiplierPercent;      // 倍率加成百分比（+= percent/100f）
    private readonly int _electroChargeTurns;     // 感电 DoT 延长回合
    private readonly int _freezeTurns;            // 冻结延长回合
    private readonly int _superconductDefense;    // 超导减防加成值
    private readonly int _superconductTurns;      // 超导减防持续延长回合
    private readonly int _extraGauge;             // 元素量加成：对应元素普攻额外元素量
    private readonly int _explosionRadius;        // 超载爆炸范围（HexCoord 距离 ≤ 该值）
    private readonly int _explosionBase;          // 爆炸基础伤害
    private readonly int _explosionDivisor;       // 爆炸伤害除数（每 divisor 点超载伤害 +1）

    public ElementalCorePassive(ElementType element, string uniqueId,
        int multiplierPercent, int electroChargeTurns, int freezeTurns,
        int superconductDefense, int superconductTurns,
        int extraGauge, int explosionRadius, int explosionBase, int explosionDivisor)
    {
        _element = element;
        _uniqueId = uniqueId;
        _multiplierPercent = multiplierPercent;
        _electroChargeTurns = electroChargeTurns;
        _freezeTurns = freezeTurns;
        _superconductDefense = superconductDefense;
        _superconductTurns = superconductTurns;
        _extraGauge = extraGauge;
        _explosionRadius = explosionRadius;
        _explosionBase = explosionBase;
        _explosionDivisor = explosionDivisor;
    }

    /// <summary>唯一被动 id（C2 框架；四件元素之力配同一 "ElementalCore"，后装覆盖先装）</summary>
    public string UniqueId => _uniqueId;

    // ---- ③.3 新机制暴露（PieceManager 介入点调用）----
    /// <summary>元素类型（元素量加成判定：携带者元素 == 攻击方先天元素才生效）</summary>
    public ElementType Element => _element;
    /// <summary>元素量加成：对应元素棋子普攻的额外元素量</summary>
    public int ExtraGauge => _extraGauge;
    /// <summary>是否超载爆炸（仅 Fire/Thunder 之力）</summary>
    public bool CanExplode => _element == ElementType.Fire || _element == ElementType.Thunder;
    /// <summary>超载爆炸范围（HexCoord 距离 ≤ 该值）</summary>
    public int ExplosionRadius => _explosionRadius;
    /// <summary>爆炸伤害 = base + floor(超载伤害 / divisor)（整数除法即 floor）</summary>
    public int GetExplosionDamage(int damage) => _explosionBase + damage / _explosionDivisor;

    /// <summary>按 element + 反应类型修改配置；None 不处理（无反应不改）</summary>
    public ElementReactionTable.ReactionConfig Modify(ElementReactionTable.ReactionConfig original)
    {
        if (original.Type == ReactionType.None) return original;

        bool changed = false;
        switch (_element)
        {
            case ElementType.Water:
                if (original.Type == ReactionType.Vaporize) { original.DamageMultiplier += _multiplierPercent / 100f; changed = true; }
                else if (original.Type == ReactionType.ElectroCharged) { original.DotTurns += _electroChargeTurns; changed = true; }
                else if (original.Type == ReactionType.Frozen) { original.FreezeTurns += _freezeTurns; changed = true; }
                break;

            case ElementType.Fire:
                if (original.Type == ReactionType.Vaporize || original.Type == ReactionType.Melt)
                { original.DamageMultiplier += _multiplierPercent / 100f; changed = true; }
                break;

            case ElementType.Thunder:
                if (original.Type == ReactionType.Superconduct)
                { original.DefenseReduction += _superconductDefense; original.DefenseReductionTurns += _superconductTurns; changed = true; }
                else if (original.Type == ReactionType.ElectroCharged) { original.DotTurns += _electroChargeTurns; changed = true; }
                break;

            case ElementType.Ice:
                if (original.Type == ReactionType.Superconduct)
                { original.DefenseReduction += _superconductDefense; original.DefenseReductionTurns += _superconductTurns; changed = true; }
                else if (original.Type == ReactionType.Melt) { original.DamageMultiplier += _multiplierPercent / 100f; changed = true; }
                else if (original.Type == ReactionType.Frozen) { original.FreezeTurns += _freezeTurns; changed = true; }
                break;
        }

        if (changed)
            Debug.Log($"[ElementalCorePassive] {_element}之力增强反应 {original.Type}（倍率 {original.DamageMultiplier}，DoT {original.DotTurns} 回合，减防 {original.DefenseReduction}/{original.DefenseReductionTurns} 回合，冻结 {original.FreezeTurns} 回合）");
        return original;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }
}
