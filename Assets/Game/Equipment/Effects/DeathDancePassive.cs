using UnityEngine;

/// <summary>
/// 死亡之蔑 —— 延迟伤害被动。
/// 受击时把伤害拆成「立即结算 + 延迟结算」：立即部分正常扣血（伤害介入，扣血前由 PieceManager 调
/// SplitIncomingDamage），延迟部分进入实例 _pendingDamage，在之后 delayTurns 个回合开始时平分结算。
/// 延迟结算 = TakeDamage + TurnsSinceDamaged=0 + OnDamageReceived(Dot)（打断计时），
/// 不触发 OnAttacked / OnDamageDealt / 元素反应 / 金币 / 能量；结算值不享受防御减伤（物理直伤）。
/// realTimeDamage=true 时按防御变化微调（防御上涨→伤害下降，反之亦然）。
/// 有未结算延迟伤害期间禁止卸下（EquipmentManager 检查 HasPendingDamage）。
/// </summary>
public class DeathDancePassive : IPassiveEffect
{
    private readonly int _delayPercent;     // 延迟比例（构造时 clamp 到 1~99）
    private readonly int _delayTurns;       // 延迟回合数（构造时 clamp 到 ≥1）
    private readonly bool _realTimeDamage;  // 实时伤害开关

    private int _pendingDamage;             // 待结算延迟伤害池
    private int _pendingTurns;              // 剩余结算回合
    private int _baseDefense;               // 受击时的有效防御（实时开关基准）

    /// <summary>是否有未结算的延迟伤害（禁止卸下判断用）</summary>
    public bool HasPendingDamage => _pendingTurns > 0;

    public DeathDancePassive(int delayPercent = 50, int delayTurns = 2, bool realTimeDamage = false)
    {
        _delayPercent = Mathf.Clamp(delayPercent, 1, 99);
        _delayTurns = Mathf.Max(1, delayTurns);
        _realTimeDamage = realTimeDamage;
    }

    /// <summary>受击拆分（伤害介入，扣血前）：delayPercent% 拆入延迟池，ref damage 留立即部分</summary>
    public void SplitIncomingDamage(ref int damage, int currentDefense)
    {
        if (damage <= 0) return;

        int delayed = damage * _delayPercent / 100;   // floor（int 数学）
        if (delayed <= 0) return;                     // 伤害过小（如 1×50%）→ 无延迟部分

        damage -= delayed;
        _pendingDamage += delayed;
        _pendingTurns = _delayTurns;                  // 新受击刷新结算窗口
        _baseDefense = currentDefense;
        Debug.Log($"[DeathDancePassive] 拆分伤害：立即 {damage}，延迟 +{delayed}（池 {_pendingDamage}，{_pendingTurns} 回合内结算）");
    }

    /// <summary>回合开始：结算一笔延迟伤害（平分；realTimeDamage 按防御变化微调）</summary>
    public void OnTurnStart(PieceModel owner)
    {
        if (_pendingTurns <= 0 || _pendingDamage <= 0 || owner == null || owner.IsDead) return;

        int baseDmg = _pendingDamage / _pendingTurns;
        int settle = baseDmg;

        if (_realTimeDamage)
        {
            // 系数 k = floor(delayPercent / delayTurns)；防御变化量 Δ = 当前有效防御 - 受击时防御
            // 修正量 n = floor(|Δ| × k / 100)；结算 = 基础 - sign(Δ) × n（防御上涨→伤害下降）
            int k = _delayPercent / _delayTurns;
            int delta = owner.EffectiveDefense - _baseDefense;
            int sign = delta > 0 ? 1 : (delta < 0 ? -1 : 0);
            int n = Mathf.Abs(delta) * k / 100;
            settle = baseDmg - sign * n;
        }

        settle = Mathf.Max(1, settle);   // 结算伤害最低 1（池未清空则至少扣 1）

        // 扣血 + 打断计时 + 受伤通知（Dot 语义；不触发 OnAttacked/OnDamageDealt/元素/金币/能量）
        owner.TakeDamage(settle);
        owner.TurnsSinceDamaged = 0;
        foreach (var passive in owner.GetAllPassives())
            passive.OnDamageReceived(owner, settle, DamageSource.Dot);
        // 跳字（View 层反馈；池/View 未就绪静默跳过；延迟伤害是物理直伤——继承受击的物理类型，用物理白纯色块）
        if (FloatingTextPool.Instance != null && owner.View != null)
            FloatingTextPool.Instance.ShowDamage(settle, owner.View.transform.position,
                ElementColorMapper.GetDamageKindColor(DamageKind.Physical), DamageKind.Physical);

        _pendingDamage -= settle;
        _pendingTurns--;
        if (_pendingTurns <= 0)
            _pendingDamage = 0;

        Debug.Log($"[DeathDancePassive] {owner.Data?.displayName} 延迟伤害结算 {settle}（池剩 {_pendingDamage}，剩 {_pendingTurns} 回合，HP {owner.CurrentHP}）");

        // 延迟伤害致死：先尝试免死（中娅悖论响应一切致死），免死成功则不再销毁；
        // 无击杀者销毁（对标 DoT 致死语义）
        if (owner.IsDead)
        {
            PieceManager.TryDefyDeath(owner);
            if (owner.IsDead)
                PieceManager.Instance?.DestroyPiece(owner);
        }
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }   // 延迟池记在装备实例上；有池期间卸下已被 EquipmentManager 拦截
}
