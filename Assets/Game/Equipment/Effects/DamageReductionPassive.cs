/// <summary>
/// 减伤被动 —— 受击时最终伤害 -amount（下限 1，由 PieceManager 伤害介入保证）。
/// B 阶段：减伤介入覆盖所有伤害入口（AttackPiece 普攻 + ApplyIncomingDamage 统一入口），
/// filter 筛选生效类型（All=全类型默认；Physical/Magical/True=只减对应类型）。
/// 注意：本被动是「额外减伤」，与「防御减伤（减法公式）」独立——魔法/真实不受防御减伤，但仍受本被动（默认 All）。
/// 不覆写任何事件钩子：效果在伤害计算处通过 Amount/AppliesTo 聚合生效（与 ExtraDamagePassive 同模式）。
/// </summary>
public class DamageReductionPassive : IPassiveEffect
{
    /// <summary>减伤筛选类型（All=全类型；后三项与 DamageKind 一一对应）</summary>
    public enum ReductionFilter { All, Physical, Magical, True }

    private readonly int _amount;
    private readonly ReductionFilter _filter;

    public DamageReductionPassive(int amount, ReductionFilter filter = ReductionFilter.All)
    {
        _amount = amount;
        _filter = filter;
    }

    /// <summary>减伤数值（供 PieceManager 伤害结算读取）</summary>
    public int Amount => _amount;

    /// <summary>该类型的伤害是否受本被动减伤（All=全类型；其余精确匹配 DamageKind）</summary>
    public bool AppliesTo(DamageKind kind)
    {
        switch (_filter)
        {
            case ReductionFilter.Physical: return kind == DamageKind.Physical;
            case ReductionFilter.Magical: return kind == DamageKind.Magical;
            case ReductionFilter.True: return kind == DamageKind.True;
            default: return true; // All
        }
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }
}
