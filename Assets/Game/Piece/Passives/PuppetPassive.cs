using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 傀儡被动（挂在傀儡 PieceData.builtInPassives 上，安柏·兔兔伯爵）——
/// 傀儡的行为核心：受击计数、持续回合计数、嘲讽光环、引爆。
///
/// 工作方式：
///   - 傀儡是大招投放的临时召唤物（PieceData.isSummon=true，高 HP 防伤害致死），
///     经 PieceManager.SpawnPieceById 生成并注册到回合系统（OnTurnStart 通知依赖注册）。
///   - 本被动挂在傀儡的 builtInPassives，由 BaronBunnyUltimate 在召唤后 Init(caster) 注入主人。
///   - 受击计数：OnDamageReceived 每段伤害 +1（普攻/溅射/大招/DoT 各算一次，不看伤害值），
///     满 hitsToDetonate（3）→ 引爆。
///   - 持续回合：OnTurnStart 每次 +1（每次任一方回合开始，同强化/超导口径），
///     满 maxTurns（6）→ 引爆。
///   - 主动关闭：玩家选中安柏再按 U（BattleController.HandleUltimate 前置分支）→ FindByCaster → 引爆。
///   - 嘲讽：攻击时距傀儡 ≤ tauntRange（2）格的敌方棋子只能攻击傀儡
///     （BattleController 高亮过滤 + HandleAttack 校验，走 FindTaunting 查询）。
///
/// 引爆（对傀儡周围 explosionRadius 格内敌方，物理 + 主人先天元素）：
///   max(1, round(max(1, baseDamage + 主人 EffectiveAttack × attackPercent% − 防御) × 反应倍率) + 额外伤害)
///   （CalculateWithElement 同烈焰斩口径）；ApplyIncomingDamage（Ultimate/Physical 白字跳字）+
///   ApplyElementInteraction（gauge=2 同大招募发量；超导减防/感电DoT/冻结）+ 金币双边；
///   致死者 DestroyPiece（killer=主人）；引爆后傀儡自毁销毁（无 killer，不触发 OnKill/OnAllyDeath——
///   OnAllyDeath 另有 isSummon 过滤双保险）。
///
/// 构造参数（傀儡资产 jsonParams）：hitsToDetonate（受击引爆阈值，默认 3）、
///   maxTurns（持续回合，默认 6）、tauntRange（嘲讽半径，默认 2）、explosionRadius（爆炸半径，默认 1）、
///   baseDamage / attackPercent（引爆伤害数值，默认 0/100，待 playtest）。
/// </summary>
public class PuppetPassive : IPassiveEffect
{
    private readonly int _hitsToDetonate;     // 受击引爆阈值
    private readonly int _maxTurns;           // 持续回合数
    private readonly int _tauntRange;         // 嘲讽半径（距傀儡 ≤ 此距离的敌方攻击被锁定）
    private readonly int _explosionRadius;    // 引爆范围半径
    private readonly int _baseDamage;         // 引爆基础伤害
    private readonly int _attackPercent;      // 引爆攻击力百分比（主人的 EffectiveAttack）

    // ---- 运行时状态（被动实例随傀儡生成创建，随场景重开重建）----
    private PieceModel _puppet;               // 宿主傀儡（OnEquip 注入）
    private PieceModel _caster;               // 主人（召唤者；引爆伤害与击杀归属）
    private int _hitsTaken;                   // 已受击次数
    private int _turnsTaken;                  // 已持续回合数
    private bool _detonated;                  // 引爆幂等标志

    // ---- 活跃傀儡注册表（嘲讽查询 + toggle 查询；查询时惰性清理已销毁项）----
    private static readonly List<PuppetPassive> _active = new();

    public string UniqueId => null;

    public PuppetPassive(int hitsToDetonate, int maxTurns, int tauntRange,
        int explosionRadius, int baseDamage, int attackPercent)
    {
        _hitsToDetonate = Mathf.Max(1, hitsToDetonate);
        _maxTurns = Mathf.Max(1, maxTurns);
        _tauntRange = Mathf.Max(0, tauntRange);
        _explosionRadius = Mathf.Max(1, explosionRadius);
        _baseDamage = baseDamage;
        _attackPercent = attackPercent;
    }

    /// <summary>召唤后由 BaronBunnyUltimate 注入主人（引爆伤害/击杀归属/嘲讽阵营判断）</summary>
    public void Init(PieceModel caster) => _caster = caster;

    /// <summary>宿主傀儡（嘲讽锁定目标；null = 已销毁）</summary>
    public PieceModel Puppet => _puppet != null && !_puppet.IsDestroyed ? _puppet : null;

    /// <summary>查询 attacker 是否被嘲讽（攻击时距某敌方傀儡 ≤ 其 tauntRange）。
    /// 返回嘲讽它的傀儡被动；null = 未被嘲讽，攻击目标不受限。</summary>
    public static PuppetPassive FindTaunting(PieceModel attacker)
    {
        if (attacker == null) return null;
        PruneActive();
        foreach (var p in _active)
        {
            // 只嘲讽敌方（傀儡与攻击者异阵营才生效）
            if (p._puppet.Owner == attacker.Owner) continue;
            if (p._puppet.Coord.Distance(attacker.Coord) <= p._tauntRange)
                return p;
        }
        return null;
    }

    /// <summary>查询 caster 当前是否有活跃傀儡（主动引爆 toggle 用）</summary>
    public static PuppetPassive FindByCaster(PieceModel caster)
    {
        if (caster == null) return null;
        PruneActive();
        foreach (var p in _active)
            if (ReferenceEquals(p._caster, caster)) return p;
        return null;
    }

    /// <summary>清理已销毁/已引爆的注册项</summary>
    private static void PruneActive()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            if (_active[i]._detonated || _active[i]._puppet == null || _active[i]._puppet.IsDestroyed)
                _active.RemoveAt(i);
    }

    // ---- 事件钩子 ----

    public void OnEquip(PieceModel owner)
    {
        _puppet = owner;
        if (!_active.Contains(this)) _active.Add(this);
    }

    public void OnUnequip(PieceModel owner) => _active.Remove(this);

    public void OnDamageReceived(PieceModel owner, int damage,
        DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical)
    {
        if (_detonated || owner == null) return;
        _hitsTaken++;
        Debug.Log($"[Puppet] 傀儡受击计数 {_hitsTaken}/{_hitsToDetonate}（{source}/{kind}，{damage} 伤害不看值）");
        if (_hitsTaken >= _hitsToDetonate)
        {
            Debug.Log("[Puppet] 受击次数达到阈值，引爆！");
            Detonate();
        }
    }

    public void OnTurnStart(PieceModel owner)
    {
        if (_detonated || owner == null) return;
        _turnsTaken++;
        Debug.Log($"[Puppet] 傀儡持续回合 {_turnsTaken}/{_maxTurns}");
        if (_turnsTaken >= _maxTurns)
        {
            Debug.Log("[Puppet] 持续回合到期，引爆！");
            Detonate();
        }
    }

    // ---- 引爆 ----

    /// <summary>引爆傀儡：对周围 explosionRadius 格内敌方造成物理 + 主人先天元素伤害，随后傀儡自毁。
    /// 幂等（多次触发只结算一次）；public 供主动关闭（toggle）调用。</summary>
    public void Detonate()
    {
        if (_detonated || _puppet == null || _puppet.IsDestroyed) return;
        _detonated = true;

        var board = ChessBoardController.Instance?.Model;
        if (board == null || PieceManager.Instance == null)
        {
            PieceManager.Instance?.DestroyPiece(_puppet);
            return;
        }

        // 爆炸范围：傀儡周围 explosionRadius 格（含傀儡自身格——已被傀儡占据，对敌不生效）
        ElementType element = _caster != null && _caster.Data != null
            ? _caster.Data.innateElement : ElementType.None;
        int hitCount = 0;
        foreach (var offset in HexCoord.AllCoordsInRadius(_explosionRadius))
        {
            var coord = new HexCoord(_puppet.Coord.q + offset.q, _puppet.Coord.r + offset.r);
            if (!board.Contains(coord)) continue;
            var enemy = PieceLayoutModel.Instance?.GetPieceAt(coord);
            if (enemy == null || enemy.IsDead || enemy.IsDestroyed) continue;
            if (_puppet.Owner == enemy.Owner) continue;   // 只打敌方（傀儡与主人同阵营）

            // 物理 + 主人先天元素（统一元素规则：CalculateWithElement 含反应倍率/额外伤害）
            int attackValue = _baseDamage + (_caster != null
                ? Mathf.RoundToInt(_caster.EffectiveAttack * (_attackPercent / 100f)) : 0);
            int damage;
            ElementReactionTable.ReactionConfig reaction = default;
            if (element != ElementType.None)
            {
                (damage, reaction) = DamageCalculator.CalculateWithElement(
                    attackValue, enemy.EffectiveDefense, element, enemy.AffixedElement);
            }
            else
            {
                float raw = attackValue - enemy.EffectiveDefense;
                damage = Mathf.Max(1, Mathf.RoundToInt(raw));
            }

            // 返回 false = 被护盾拦截 → 不给金币；元素附着/反应照常
            bool landed = PieceManager.Instance.ApplyIncomingDamage(enemy, damage, _caster,
                DamageSource.Ultimate, DamageKind.Physical, element);
            PieceManager.Instance.ApplyElementInteraction(enemy, _caster, element, 2, reaction);
            if (landed)
            {
                if (_caster != null) GoldManager.Instance?.OnDamageDealt(_caster, damage);
                GoldManager.Instance?.OnDamageReceived(enemy, damage);
            }

            if (enemy.IsDead)
                PieceManager.Instance.DestroyPiece(enemy, _caster);

            string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
            Debug.Log($"[Puppet] 引爆命中 {enemy.Data.displayName}，造成 {damage} 伤害{reactionStr}（剩余 {enemy.CurrentHP}）");
            hitCount++;
        }

        Debug.Log($"[Puppet] 傀儡引爆完成：命中 {hitCount} 个敌人，傀儡销毁");
        PieceManager.Instance.DestroyPiece(_puppet);   // 自毁（无 killer：不触发 OnKill；OnAllyDeath 被 isSummon 过滤）
    }

    // ---- 其余钩子：傀儡不消费 ----
    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null) { }
    public void OnKill(PieceModel owner, PieceModel victim) { }
    public void OnAllyDeath(PieceModel owner, PieceModel ally) { }
}
