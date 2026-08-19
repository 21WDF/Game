using UnityEngine;

/// <summary>
/// 击杀加攻被动（死亡之刃）—— owner 击杀棋子时攻击加成 +amount。
/// 叠层累积在本被动实例上（而非 PieceModel）：卸下装备自动移除加成，重新装上叠层保留（叠层跟随装备）。
/// 暴露 Accumulated 只读属性，由 PieceModel.EffectiveAttack 聚合生效。
/// </summary>
public class KillAttackPassive : IPassiveEffect
{
    private readonly int _amount;

    public KillAttackPassive(int amount)
    {
        _amount = amount;
    }

    /// <summary>当前累积的攻击加成（供 EffectiveAttack 聚合；随被动实例存续）</summary>
    public int Accumulated { get; private set; }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>击杀时：叠层 +_amount（经 EffectiveAttack 聚合生效）</summary>
    public void OnKill(PieceModel owner, PieceModel victim)
    {
        Accumulated += _amount;
        Debug.Log($"[KillAttackPassive] {owner.Data.displayName} 击杀 {victim.Data.displayName}，攻击 +{_amount}（叠层 +{Accumulated}）");
    }
}
