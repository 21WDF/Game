using UnityEngine;

/// <summary>光环作用方向（军团圣盾等查询型光环用）</summary>
public enum AuraDirection
{
    Ally,   // 己方（同阵营）
    Enemy   // 敌方（敌阵营）
}

/// <summary>光环加成属性</summary>
public enum AuraStat
{
    Attack,
    Defense
}

/// <summary>
/// 军团圣盾 —— 查询型光环 + 进化 + 唯一被动（C2 框架第一个使用者）。
/// 光环：EffectiveAttack/EffectiveDefense 被查询时由 PieceModel.GetAuraBonus 动态遍历全场现算
/// （不引入"进出范围"事件监听）；携带者自己吃自己的 Ally 光环，Enemy 光环天然排除自己。
/// 进化：连续 evolveTurns 回合未受伤后永久进化（一次性，不退化）；evolveTurns=-1 不进化、0 立即进化；
/// 计时/状态记在被动实例上（跟装备走，卸下重装重新计时）。
/// 唯一被动：覆写 UniqueId，同棋子同 id 只生效最后装备的那件（C2 去重）。
/// </summary>
public class LegionAegisPassive : IPassiveEffect
{
    private readonly string _uniqueId;

    // 基础光环参数
    private readonly AuraDirection _affectDirection;
    private readonly int _affectRange;
    private readonly AuraStat _bonusStat;
    private readonly int _bonusAmount;

    // 进化参数
    private readonly int _evolveTurns;          // -1=不进化；0=装备立即进化；>0=连续未受伤回合数
    private readonly AuraDirection _evolvedDirection;
    private readonly int _evolvedRange;
    private readonly AuraStat _evolvedStat;
    private readonly int _evolvedAmount;
    private readonly bool _evolveOverrides;     // true=进化后只用新一套；false=新旧叠加

    // 运行时状态（装备实例上）
    private PieceModel _owner;
    private int _turnsWithoutDamage;
    private bool _isEvolved;

    public LegionAegisPassive(string uniqueId,
        AuraDirection affectDirection, int affectRange, AuraStat bonusStat, int bonusAmount,
        int evolveTurns,
        AuraDirection evolvedDirection, int evolvedRange, AuraStat evolvedStat, int evolvedAmount,
        bool evolveOverrides)
    {
        _uniqueId = uniqueId;
        _affectDirection = affectDirection;
        _affectRange = affectRange;
        _bonusStat = bonusStat;
        _bonusAmount = bonusAmount;
        _evolveTurns = evolveTurns;
        _evolvedDirection = evolvedDirection;
        _evolvedRange = evolvedRange;
        _evolvedStat = evolvedStat;
        _evolvedAmount = evolvedAmount;
        _evolveOverrides = evolveOverrides;
    }

    /// <summary>唯一被动 id（C2 框架；由 PassiveConfig.uniqueId 经工厂透传）</summary>
    public string UniqueId => _uniqueId;

    /// <summary>查询该光环对「目标坐标/归属 的 statType 属性」的加成；0 = 不在范围/方向/属性不匹配。
    /// 未进化用基础参数；已进化且 evolveOverrides=true 只用进化参数；已进化且 false 新旧叠加。</summary>
    public int GetAuraAmount(string statType, HexCoord targetCoord, PlayerSide targetOwner)
    {
        if (_owner == null) return 0;

        if (!_isEvolved)
            return Calc(_affectDirection, _affectRange, _bonusStat, _bonusAmount, statType, targetCoord, targetOwner);

        if (_evolveOverrides)
            return Calc(_evolvedDirection, _evolvedRange, _evolvedStat, _evolvedAmount, statType, targetCoord, targetOwner);

        return Calc(_affectDirection, _affectRange, _bonusStat, _bonusAmount, statType, targetCoord, targetOwner)
             + Calc(_evolvedDirection, _evolvedRange, _evolvedStat, _evolvedAmount, statType, targetCoord, targetOwner);
    }

    /// <summary>单套光环参数的目标判定：距离 ≤ range + 阵营匹配 + 属性匹配 → 加成值</summary>
    private int Calc(AuraDirection dir, int range, AuraStat stat, int amount,
        string statType, HexCoord targetCoord, PlayerSide targetOwner)
    {
        if (amount == 0 || stat.ToString() != statType) return 0;
        if (_owner.Coord.Distance(targetCoord) > range) return 0;

        bool isAlly = _owner.Owner == targetOwner;
        if (dir == AuraDirection.Ally && !isAlly) return 0;
        if (dir == AuraDirection.Enemy && isAlly) return 0;
        return amount;
    }

    // ---- 生命周期与事件 ----

    public void OnEquip(PieceModel owner)
    {
        _owner = owner;
        _turnsWithoutDamage = 0;
        _isEvolved = _evolveTurns == 0;   // evolveTurns=0 装备立即进化
    }

    public void OnUnequip(PieceModel owner)
    {
        _owner = null;   // 卸下光环消失（GetAllPassives 不再遍历到，双保险清引用）
    }

    /// <summary>回合开始：连续未受伤计时，达标进化（一次性，进化后不再退化）</summary>
    public void OnTurnStart(PieceModel owner)
    {
        if (_isEvolved || _evolveTurns <= 0) return;   // 已进化 / -1 不进化 / 0 装备时已进化

        _turnsWithoutDamage++;
        if (_turnsWithoutDamage >= _evolveTurns)
        {
            _isEvolved = true;
            Debug.Log($"[LegionAegisPassive] {owner?.Data?.displayName} 的圣盾光环连续 {_turnsWithoutDamage} 回合未受伤，进化！" +
                      $"（{_evolvedDirection} 范围{_evolvedRange}，{_evolvedStat} {_evolvedAmount}{(_evolveOverrides ? "，覆盖旧光环" : "，新旧叠加")}）");
        }
    }

    /// <summary>受击打断进化计时（进化后归零无影响）</summary>
    public void OnDamageReceived(PieceModel owner, int damage, DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical)
    {
        _turnsWithoutDamage = 0;
    }
}
