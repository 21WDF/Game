using UnityEngine;

/// <summary>
/// 连击被动（安柏·快速射击）——普攻造成两次物理伤害（一次普攻结算两段）。
///
/// 工作方式：OnDamageDealt 仅在普攻路径（AttackPiece）触发 → 对同一目标立即补一段完整攻击
/// （重入守卫防递归：第二段的 OnDamageDealt 被拦截）。
///   - 第二段为完整伤害管道：伤害计算/元素附着与反应（火元素）/金币/跳字/OnKill 全部正常结算；
///   - 第二段不回能量（skipEnergy=true，一次普攻 = 一次充能）；
///   - 首段致死则不补段（target.IsDead 检查）；
///   - 穿透/溅射命中多个目标时每个目标各补一段（OnDamageDealt 按目标逐次触发）；
///   - 大招/被动溅射/DoT 不触发（不走 AttackPiece 主路径）。
///
/// 构造参数：percent（第二段伤害百分比，默认 100 = 完整一段；通过 overrideAttack 缩放）。
/// 数值待 playtest 填写。
/// </summary>
public class DoubleStrikePassive : IPassiveEffect
{
    private readonly int _percent;      // 第二段伤害百分比（100 = 与首段同攻击力）
    private bool _striking;             // 重入守卫（第二段触发的 OnDamageDealt 直接拦截）

    public DoubleStrikePassive(int percent)
    {
        _percent = Mathf.Max(1, percent);
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null)
    {
        if (_striking || owner == null || target == null || target.IsDead || owner.IsDead) return;
        if (PieceManager.Instance == null) return;

        // 第二段：完整 AttackPiece（元素反应/金币/跳字/击杀全走），不回能量（一次普攻一次充能）；
        // overrideAttack 按百分比缩放（100% = 与首段同 EffectiveAttack，反应计算同步使用）
        _striking = true;
        try
        {
            int attack = _percent >= 100
                ? owner.EffectiveAttack
                : Mathf.Max(1, Mathf.RoundToInt(owner.EffectiveAttack * (_percent / 100f)));
            PieceManager.Instance.AttackPiece(owner, target, skipEnergy: true, overrideAttack: attack);
            Debug.Log($"[DoubleStrike] {owner.Data.displayName} 二段射击命中 {target.Data.displayName}");
        }
        finally
        {
            _striking = false;
        }
    }
}
