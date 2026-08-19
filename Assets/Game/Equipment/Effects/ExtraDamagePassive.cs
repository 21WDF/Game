/// <summary>
/// 附加伤害被动 —— 攻击命中后追加一段独立伤害（不混入基础物理伤害公式）。
/// 构造函数接收 amount（附加伤害量）与 kind（伤害类型，默认 True 真实）。
/// 结算（PieceManager.ApplyExtraDamageSegments）：按类型分「物理/魔法/真实」三份独立扣血——
///   物理：max(1, extra - 目标EffectiveDefense) 再吃物理减伤被动（吃防御减伤）；
///   魔法/真实：不吃防御减伤，只吃对应类型减伤被动；
///   每份独立走免死与「受到伤害」通知；不触发吸血/元素反应/附着/冻结/击退/金币/能量/跳字。
/// OnEquip/OnUnequip 为空。
/// </summary>
public class ExtraDamagePassive : IPassiveEffect
{
    private readonly int _amount;
    private readonly DamageKind _kind;

    public ExtraDamagePassive(int amount, DamageKind kind = DamageKind.True)
    {
        _amount = amount;
        _kind = kind;
    }

    /// <summary>额外伤害量（供攻击结算读取）</summary>
    public int Amount => _amount;

    /// <summary>附加伤害类型（物理/魔法/真实；供分段结算读取）</summary>
    public DamageKind Kind => _kind;

    public void OnEquip(PieceModel owner) { }

    public void OnUnequip(PieceModel owner) { }
}
