using UnityEngine;

/// <summary>
/// 圣盾被动（护盾机制一期测试装备被动）——
/// 持有者装备时获得 stacks 层护盾（上限 3），护盾元素标签由 element 参数配置
/// （None=普通盾；Fire/Water/Thunder/Ice=元素盾，对同元素伤害免疫不扣层）。
///
/// 行为语义见 PieceManager.TryAbsorbByShield（拦截规则）与 PieceModel.ShieldStacks（字段）：
///   - 护盾只拦「伤害数值」，元素附着/反应/冻结/感电/减防照常发生；
///   - 被免掉的伤害不减血 → 不触发受伤通知、不打断「连续未受伤」计时；
///   - 扣到 0 层护盾消失；重复获得 = 刷新到上限（重置满层，不叠加）。
///
/// 构造参数（装备 JSON）：
///   {"stacks": 3, "element": "None"}   —— element 为字符串手动解析（枚举直写 JSON 会静默回退默认值）；
///   可配 "Fire"/"Water"/"Thunder"/"Ice"/"None"（大小写不敏感），无法识别回退 None。
/// 数值为占位，playtest 再调。
/// </summary>
public class HolyShieldPassive : IPassiveEffect
{
    /// <summary>护盾层数上限（基座常量；刷新到上限语义 = 装备时重置为满层）</summary>
    public const int MaxStacks = 3;

    private readonly int _stacks;           // 施加层数（1~3）
    private readonly ElementType _element;  // 护盾元素标签

    public string UniqueId => null;

    public HolyShieldPassive(int stacks, ElementType element)
    {
        _stacks = Mathf.Clamp(stacks, 1, MaxStacks);
        _element = element;
    }

    /// <summary>装备时获得护盾：走 SetShield 统一入口（触发 OnShieldChanged UI 事件；
    /// 重复获得 = 刷新到满层，不叠加）</summary>
    public void OnEquip(PieceModel owner)
    {
        if (owner == null) return;
        bool refreshed = owner.HasShield;
        owner.SetShield(_stacks, _element);
        string elemMark = _element != ElementType.None ? $"{_element}元素盾" : "普通盾";
        Debug.Log(refreshed
            ? $"[HolyShield] {owner.Data.displayName} 的护盾刷新为 {elemMark} {_stacks} 层（不叠加）"
            : $"[HolyShield] {owner.Data.displayName} 获得{elemMark} {_stacks} 层");
    }

    /// <summary>卸下时不清除护盾（护盾已施加到棋子身上，随层数自然消耗；一期从简口径）</summary>
    public void OnUnequip(PieceModel owner) { }

    // ---- 事件钩子：圣盾是一次性施加型被动，不消费事件 ----
    public void OnTurnStart(PieceModel owner) { }
    public void OnAttacked(PieceModel owner, PieceModel attacker, DamageSource source = DamageSource.Attack) { }
    public void OnDamageReceived(PieceModel owner, int damage,
        DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical) { }
    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null) { }
    public void OnKill(PieceModel owner, PieceModel victim) { }
    public void OnAllyDeath(PieceModel owner, PieceModel ally) { }
    public void OnAllyUltimateCast(PieceModel owner, PieceModel caster) { }
    public void OnAllyAttackHit(PieceModel owner, PieceModel ally, PieceModel target, int damage, DamageSource source) { }
}
