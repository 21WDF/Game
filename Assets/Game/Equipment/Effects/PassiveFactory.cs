using UnityEngine;

/// <summary>
/// 被动效果静态工厂 —— 按「类名 + JSON 参数」创建 IPassiveEffect 实例。
/// 装备被动（EquipmentManager.InstantiatePassives）与棋子内建被动（PieceManager.SpawnPieceById）共用。
/// 新增被动在此追加 case（对标 PieceManager.CreateUltimate / RangeProviderFactory 的写法）。
/// </summary>
public static class PassiveFactory
{
    /// <summary>按类名创建被动实例；uniqueId 为唯一被动 id（C2 框架参数，现有 case 均不消费，
    /// 唯一被动类如 LegionAegisPassive 在 C3 实现并覆写 UniqueId）。
    /// 未知类名返回 null（调用方自行告警）。</summary>
    public static IPassiveEffect Create(string className, string jsonParams, string uniqueId = null)
    {
        switch (className)
        {
            case "ExtraDamagePassive":
                var p = JsonUtility.FromJson<ExtraDamageParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams);
                return new ExtraDamagePassive(p.amount, ParseDamageKind(p.kind));
            case "LifestealPassive":
                var ls = ParseParams<LifestealParams>(jsonParams);
                return new LifestealPassive(ls.baseHeal, ls.percent);
            case "DamageReductionPassive":
                var dr = ParseParams<DamageReductionParams>(jsonParams);
                return new DamageReductionPassive(dr.amount, ParseReductionFilter(dr.filter));
            case "RegenPassive":
                var rg = ParseParams<RegenParams>(jsonParams);
                return new RegenPassive(rg.baseAmount, rg.upgradedAmount, rg.upgradeThreshold);
            case "ThornsPassive":
                var th = ParseParams<ThornsParams>(jsonParams);
                return new ThornsPassive(th.amount, th.percent, th.affectedByDefense, th.reflectRadius, th.alwaysReflectAttacker);
            case "KillAttackPassive":
                return new KillAttackPassive(ParseParams<KillAttackParams>(jsonParams).amount);
            case "StoneSkinPassive":
                var ss = ParseParams<StoneSkinParams>(jsonParams);
                return new StoneSkinPassive(ss.defenseBonus, ss.timerTurns, ss.triggerOnNoDamage);
            case "WitherPassive":
                var wi = ParseParams<WitherParams>(jsonParams);
                return new WitherPassive(wi.defenseReduction, wi.duration);
            case "SpiritBannerPassive":
                var sb = ParseParams<SpiritBannerParams>(jsonParams);
                return new SpiritBannerPassive(sb.instantEnergy, sb.regenEnergyPerTurn, sb.durationTurns);
            case "FreeAPPassive":
                var fap = ParseParams<FreeAPParams>(jsonParams);
                return new FreeAPPassive(ParseFreeAPType(fap.freeType), fap.cooldownTurns, fap.gainEnergy);
            case "DeathDancePassive":
                var dd = ParseParams<DeathDanceParams>(jsonParams);
                return new DeathDancePassive(dd.delayPercent, dd.delayTurns, dd.realTimeDamage);
            case "FirstDamageToOnePassive":
                return new FirstDamageToOnePassive(ParseParams<FirstDamageToOneParams>(jsonParams).cooldownTurns);
            case "DeathDefyPassive":
                return new DeathDefyPassive(ParseParams<DeathDefyParams>(jsonParams).cooldownTurns);
            case "LegionAegisPassive":
                var lg = ParseParams<LegionAegisParams>(jsonParams);
                return new LegionAegisPassive(uniqueId,
                    ParseAuraDirection(lg.affectDirection), lg.affectRange,
                    ParseAuraStat(lg.bonusStat), lg.bonusAmount,
                    lg.evolveTurns,
                    ParseAuraDirection(lg.evolvedDirection), lg.evolvedRange,
                    ParseAuraStat(lg.evolvedStat), lg.evolvedAmount,
                    lg.evolveOverrides);
            case "ReactionBoostPassive":
                return new ReactionBoostPassive(ParseParams<ReactionBoostParams>(jsonParams).percent);
            case "FlameNovaPassive":
                var fn = ParseParams<FlameNovaParams>(jsonParams);
                return new FlameNovaPassive(fn.baseDamage, fn.attackPercent, fn.splashRadius, fn.goldPercent, fn.elemental);
            case "SparkEmpowerPassive":
                return new SparkEmpowerPassive(ParseParams<SparkEmpowerParams>(jsonParams).duration);
            case "RevivePassive":
                return new RevivePassive(ParseParams<ReviveParams>(jsonParams).reviveHp);
            case "VoltPathPassive":
                var vp = ParseParams<VoltPathParams>(jsonParams);
                return new VoltPathPassive(vp.baseDamage, vp.attackPercent, vp.elemental);
            case "DoubleStrikePassive":
                return new DoubleStrikePassive(ParseParams<DoubleStrikeParams>(jsonParams).percent);
            case "PuppetPassive":
                var pp = ParseParams<PuppetParams>(jsonParams);
                return new PuppetPassive(pp.hitsToDetonate, pp.maxTurns, pp.tauntRange,
                    pp.explosionRadius, pp.baseDamage, pp.attackPercent);
            case "DomainPassive":
                var dm = ParseParams<DomainParams>(jsonParams);
                return new DomainPassive(dm.radius, dm.baseDamage, dm.attackPercent);
            case "FrostBreakPassive":
                var fb = ParseParams<FrostBreakParams>(jsonParams);
                return new FrostBreakPassive(fb.amount, fb.duration);
            case "BlizzardPassive":
                var bl = ParseParams<BlizzardParams>(jsonParams);
                return new BlizzardPassive(bl.radius, bl.baseDamage, bl.attackPercent);
            case "StormEyePassive":
                return new StormEyePassive(ParseParams<StormEyeParams>(jsonParams).energyGain);
            case "CoordinatedStrikePassive":
                var cs = ParseParams<CoordinatedStrikeParams>(jsonParams);
                return new CoordinatedStrikePassive(cs.baseDamage, cs.attackPercent, cs.energyPerAttack);
            case "HolyShieldPassive":
                var hs = ParseParams<HolyShieldParams>(jsonParams);
                return new HolyShieldPassive(hs.stacks, ParseShieldElement(hs.element));
            case "BloodCostPassive":
                // 代价型被动①（地下交易装备）：每回合开始扣 X HP（jsonParams {"hpPerTurn":2}）
                return new BloodCostPassive(ParseParams<BloodCostParams>(jsonParams).hpPerTurn);
            case "APCostPassive":
                // 代价型被动②（地下交易装备）：装备者自己回合开始扣 N AP（jsonParams {"apPerTurn":1}）
                return new APCostPassive(ParseParams<APCostParams>(jsonParams).apPerTurn);
            case "ElementalCorePassive":
                var ec = ParseParams<ElementalCoreParams>(jsonParams);
                return new ElementalCorePassive(ParseElement(ec.element), uniqueId,
                    ec.multiplierPercent, ec.electroChargeTurns, ec.freezeTurns,
                    ec.superconductDefense, ec.superconductTurns,
                    ec.extraGauge, ec.explosionRadius, ec.explosionBase, ec.explosionDivisor);
            default:
                return null;
        }
    }

    /// <summary>JSON 参数反序列化（null/空串回退 "{}" 取字段默认值）。
    /// 参数类必须标 [System.Serializable]，否则 JsonUtility 读不到字段（ExtraDamageParams 踩过的坑）。</summary>
    private static T ParseParams<T>(string json)
        => JsonUtility.FromJson<T>(string.IsNullOrEmpty(json) ? "{}" : json);

    /// <summary>ExtraDamagePassive 的构造参数</summary>
    [System.Serializable]
    private class ExtraDamageParams
    {
        public int amount = 0;          // 附加伤害数值
        public string kind = "True";    // 伤害类型：Physical/Magical/True（string 承接 + 手动解析，默认真实）
    }

    /// <summary>LifestealPassive 的构造参数</summary>
    [System.Serializable]
    private class LifestealParams
    {
        public int baseHeal = 0;    // 固定吸血量
        public int percent = 0;     // 伤害百分比吸血
    }

    /// <summary>DamageReductionPassive 的构造参数</summary>
    [System.Serializable]
    private class DamageReductionParams
    {
        public int amount = 0;          // 减伤数值
        public string filter = "All";   // 筛选类型：All/Physical/Magical/True（string 承接 + 手动解析）
    }

    /// <summary>RegenPassive 的构造参数</summary>
    [System.Serializable]
    private class RegenParams
    {
        public int baseAmount = 0;      // 基础每回合回血
        public int upgradedAmount = 0;  // 进化后每回合回血
        public int upgradeThreshold = 0;// 连续未受伤回合数阈值
    }

    /// <summary>ThornsPassive 的构造参数</summary>
    [System.Serializable]
    private class ThornsParams
    {
        public int amount = 0;                  // 基础反伤值
        public int percent = 0;                 // 按受击伤害比例反伤
        public bool affectedByDefense = false;  // true=反伤被目标防御减伤；false=真实伤害
        public int reflectRadius = 0;           // 反伤范围半径（0=只反攻击者）
        public bool alwaysReflectAttacker = false; // true=攻击者必反；false=只反半径内敌人
    }

    /// <summary>KillAttackPassive 的构造参数</summary>
    [System.Serializable]
    private class KillAttackParams { public int amount = 0; }

    /// <summary>StoneSkinPassive 的构造参数</summary>
    [System.Serializable]
    private class StoneSkinParams
    {
        public int defenseBonus = 5;        // 每次叠加防御
        public int timerTurns = 4;          // 计时器回合
        public bool triggerOnNoDamage = true; // true=无论是否减血都叠层（OnAttacked）；false=仅减血叠层
    }

    /// <summary>WitherPassive 的构造参数</summary>
    [System.Serializable]
    private class WitherParams
    {
        public int defenseReduction = 3;    // 减防值
        public int duration = 2;            // 持续回合
    }

    /// <summary>SpiritBannerPassive 的构造参数</summary>
    [System.Serializable]
    private class SpiritBannerParams
    {
        public int instantEnergy = 20;      // 死亡立刻回蓝
        public int regenEnergyPerTurn = 5;  // 之后每回合持续回蓝
        public int durationTurns = 3;       // 持续回合（多次死亡刷新不叠加）
    }

    /// <summary>FreeAPPassive 的构造参数。
    /// freeType 用 string 承接：JsonUtility 枚举字段只认数值、不认名字（"Attack" 会静默落回默认值），
    /// 故统一按名字解析（兼容 "0"/"1" 数值字符串）。</summary>
    [System.Serializable]
    private class FreeAPParams
    {
        public string freeType = "Move";   // "Move" / "Attack"（也接受 "0"/"1"）
        public int cooldownTurns = 4;
        public bool gainEnergy = false;    // 免 AP/连射动作是否获得充能（false=不充能）
    }

    /// <summary>DeathDancePassive（死亡之蔑）的构造参数</summary>
    [System.Serializable]
    private class DeathDanceParams
    {
        public int delayPercent = 50;    // 延迟比例（1~99，构造时 clamp）
        public int delayTurns = 2;       // 延迟回合数（≥1，构造时 clamp）
        public bool realTimeDamage = false;  // 实时伤害开关（false=固定；true=随防御变化微调）
    }

    /// <summary>FirstDamageToOnePassive（中娅悖论·首伤=1）的构造参数</summary>
    [System.Serializable]
    private class FirstDamageToOneParams { public int cooldownTurns = 4; }

    /// <summary>DeathDefyPassive（中娅悖论·免死）的构造参数</summary>
    [System.Serializable]
    private class DeathDefyParams { public int cooldownTurns = 16; }

    /// <summary>LegionAegisPassive（军团圣盾）的构造参数。
    /// 方向/属性用 string 承接 + 手动解析（JsonUtility 枚举只认数值，同 FreeAPType 的坑）。</summary>
    [System.Serializable]
    private class LegionAegisParams
    {
        public string affectDirection = "Ally";     // Ally=己方 / Enemy=敌方
        public int affectRange = 2;
        public string bonusStat = "Defense";        // Attack / Defense
        public int bonusAmount = 5;
        public int evolveTurns = 4;                 // -1=不进化；0=立即进化；>0=连续未受伤回合数
        public string evolvedDirection = "Ally";
        public int evolvedRange = 3;
        public string evolvedStat = "Defense";
        public int evolvedAmount = 10;
        public bool evolveOverrides = true;         // true=覆盖旧光环；false=新旧叠加
    }

    /// <summary>ReactionBoostPassive（光界之力）的构造参数</summary>
    [System.Serializable]
    private class ReactionBoostParams { public int percent = 25; }

    /// <summary>FlameNovaPassive（炎星溅射）的构造参数</summary>
    [System.Serializable]
    private class FlameNovaParams
    {
        public int baseDamage = 0;       // 基础伤害值
        public int attackPercent = 0;    // 攻击力百分比（0=不吃攻击加成）
        public int splashRadius = 2;     // 溅射半径（以自身为圆心）
        public float goldPercent = 0f;   // 金币比例（0=不产金币）
        public bool elemental = true;    // 是否带先天元素（false=旧口径纯公式不附着不反应）
    }

    /// <summary>SparkEmpowerPassive（雷蚀强化）的构造参数</summary>
    [System.Serializable]
    private class SparkEmpowerParams
    {
        public int duration = 6;         // 强化持续回合（任一方回合开始递减）
    }

    /// <summary>RevivePassive（生命之息）的构造参数</summary>
    [System.Serializable]
    private class ReviveParams
    {
        public int reviveHp = 10;        // 复活后的生命值
    }

    /// <summary>VoltPathPassive（雷径惩戒）的构造参数</summary>
    [System.Serializable]
    private class VoltPathParams
    {
        public int baseDamage = 0;       // 基础伤害值
        public int attackPercent = 0;    // 攻击力百分比（0=不吃攻击加成）
        public bool elemental = true;    // 是否带先天元素（false=旧口径纯公式不附着不反应）
    }

    /// <summary>DoubleStrikePassive（快速射击·二段普攻）的构造参数</summary>
    [System.Serializable]
    private class DoubleStrikeParams
    {
        public int percent = 100;        // 第二段伤害百分比（100 = 完整一段）
    }

    /// <summary>PuppetPassive（傀儡·兔兔伯爵）的构造参数</summary>
    [System.Serializable]
    private class PuppetParams
    {
        public int hitsToDetonate = 3;     // 受击引爆阈值
        public int maxTurns = 6;           // 持续回合数（每次任一方回合开始 +1）
        public int tauntRange = 2;         // 嘲讽半径（攻击时距傀儡 ≤ 此距离的敌方被锁定）
        public int explosionRadius = 1;    // 引爆范围半径
        public int baseDamage = 0;         // 引爆基础伤害
        public int attackPercent = 100;    // 引爆攻击力百分比（主人 EffectiveAttack）
    }

    /// <summary>DomainPassive（领域·莫娜）的构造参数</summary>
    [System.Serializable]
    private class DomainParams
    {
        public int radius = 2;             // 领域半径（普攻目标格为中心）
        public int baseDamage = 0;         // 每 tick 基础伤害
        public int attackPercent = 50;     // 每 tick 攻击力百分比（主人 EffectiveAttack）
    }

    /// <summary>FrostBreakPassive（冰锋蚀甲·爱可菲）的构造参数</summary>
    [System.Serializable]
    private class FrostBreakParams
    {
        public int amount = 4;             // 减防固定值（不可叠加，重复攻击只刷新回合）
        public int duration = 4;           // 持续回合
    }

    /// <summary>BlizzardPassive（凛冬风暴结算器·爱可菲）的构造参数</summary>
    [System.Serializable]
    private class BlizzardParams
    {
        public int radius = 3;             // 风暴半径（以爱可菲当前位置为中心，每回合动态跟随）
        public int baseDamage = 0;         // 每 tick 魔法基础伤害
        public int attackPercent = 30;     // 每 tick 攻击力百分比（爱可菲 EffectiveAttack）
    }

    /// <summary>StormEyePassive（雷罚恶曜之眼·雷电将军）的构造参数</summary>
    [System.Serializable]
    private class StormEyeParams
    {
        public int energyGain = 10;        // 队友开大时雷电将军回能量（不含自己开大）
    }

    /// <summary>CoordinatedStrikePassive（协同状态·雷电将军）的构造参数</summary>
    [System.Serializable]
    private class CoordinatedStrikeParams
    {
        public int baseDamage = 0;         // 协同攻击基础伤害
        public int attackPercent = 50;     // 协同攻击力百分比（雷电将军 EffectiveAttack；playtest 调参用）
        public int energyPerAttack = 5;    // 雷电将军普攻时全体己方回能量
    }

    /// <summary>HolyShieldPassive（圣盾·护盾一期测试被动）的构造参数</summary>
    [System.Serializable]
    private class HolyShieldParams
    {
        public int stacks = 3;             // 护盾层数（上限 3；施加时 Clamp）
        public string element = "None";    // 护盾元素标签（字符串手动解析：None/Fire/Water/Thunder/Ice）
    }

    [System.Serializable]
    private class BloodCostParams
    {
        public int hpPerTurn = 2;          // 代价型被动①：每回合开始扣血量（任一方回合，同 DoT 口径）
    }

    [System.Serializable]
    private class APCostParams
    {
        public int apPerTurn = 1;          // 代价型被动②：装备者自己回合开始扣 AP（refill 之后）
    }

    /// <summary>解析护盾元素标签：不区分大小写；无法识别回退 None（普通盾）。
    /// 与 ParseElement（元素之力，Water 兜底）分开——护盾的默认语义是普通盾。</summary>
    private static ElementType ParseShieldElement(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Equals("None", System.StringComparison.OrdinalIgnoreCase))
            return ElementType.None;
        if (value.Equals("Fire", System.StringComparison.OrdinalIgnoreCase)) return ElementType.Fire;
        if (value.Equals("Water", System.StringComparison.OrdinalIgnoreCase)) return ElementType.Water;
        if (value.Equals("Thunder", System.StringComparison.OrdinalIgnoreCase)) return ElementType.Thunder;
        if (value.Equals("Ice", System.StringComparison.OrdinalIgnoreCase)) return ElementType.Ice;
        return ElementType.None;
    }

    /// <summary>ElementalCorePassive（四元素之力）的构造参数。
    /// element 用 string 承接 + 手动解析（JsonUtility 枚举只认数值，同 FreeAPType 的坑）。</summary>
    [System.Serializable]
    private class ElementalCoreParams
    {
        public string element = "Water";           // Water/Fire/Thunder/Ice
        public int multiplierPercent = 20;         // 倍率加成百分比（+= percent/100f，即 +0.2）
        public int electroChargeTurns = 2;         // 感电 DoT 延长回合
        public int freezeTurns = 1;                // 冻结延长回合
        public int superconductDefense = 5;        // 超导减防加成值
        public int superconductTurns = 2;          // 超导减防持续延长回合
        public int extraGauge = 1;                 // 元素量加成：对应元素普攻额外元素量
        public int explosionRadius = 1;            // 超载爆炸范围（距离 ≤ 该值）
        public int explosionBase = 1;              // 爆炸基础伤害
        public int explosionDivisor = 5;           // 爆炸伤害除数（每 divisor 点超载伤害 +1）
    }

    /// <summary>解析附加伤害类型：不区分大小写；无法识别回退 True（对标 ParseReductionFilter 写法）</summary>
    private static DamageKind ParseDamageKind(string value)
    {
        if (value == null) return DamageKind.True;
        if (value.Equals("Physical", System.StringComparison.OrdinalIgnoreCase)) return DamageKind.Physical;
        if (value.Equals("Magical", System.StringComparison.OrdinalIgnoreCase)) return DamageKind.Magical;
        return DamageKind.True;
    }

    /// <summary>解析减伤筛选类型：不区分大小写；无法识别回退 All（对标 ParseElement 写法）</summary>
    private static DamageReductionPassive.ReductionFilter ParseReductionFilter(string value)
    {
        if (value == null) return DamageReductionPassive.ReductionFilter.All;
        if (value.Equals("Physical", System.StringComparison.OrdinalIgnoreCase)) return DamageReductionPassive.ReductionFilter.Physical;
        if (value.Equals("Magical", System.StringComparison.OrdinalIgnoreCase)) return DamageReductionPassive.ReductionFilter.Magical;
        if (value.Equals("True", System.StringComparison.OrdinalIgnoreCase)) return DamageReductionPassive.ReductionFilter.True;
        return DamageReductionPassive.ReductionFilter.All;
    }

    /// <summary>解析元素类型：不区分大小写；无法识别回退 Water（对标 ParseAuraDirection/ParseFreeAPType）</summary>
    private static ElementType ParseElement(string value)
    {
        if (value == null) return ElementType.Water;
        if (value.Equals("Fire", System.StringComparison.OrdinalIgnoreCase)) return ElementType.Fire;
        if (value.Equals("Thunder", System.StringComparison.OrdinalIgnoreCase)) return ElementType.Thunder;
        if (value.Equals("Ice", System.StringComparison.OrdinalIgnoreCase)) return ElementType.Ice;
        return ElementType.Water;
    }

    /// <summary>解析光环方向：不区分大小写；无法识别回退 Ally</summary>
    private static AuraDirection ParseAuraDirection(string value)
        => value != null && value.Equals("Enemy", System.StringComparison.OrdinalIgnoreCase)
            ? AuraDirection.Enemy : AuraDirection.Ally;

    /// <summary>解析光环属性：不区分大小写；无法识别回退 Defense</summary>
    private static AuraStat ParseAuraStat(string value)
        => value != null && value.Equals("Attack", System.StringComparison.OrdinalIgnoreCase)
            ? AuraStat.Attack : AuraStat.Defense;

    /// <summary>解析 freeType：名字（不区分大小写）或数值字符串 → FreeAPType；无法识别按 Move</summary>
    private static FreeAPType ParseFreeAPType(string value)
    {
        if (string.IsNullOrEmpty(value)) return FreeAPType.Move;
        if (int.TryParse(value, out int n))
            return n == 1 ? FreeAPType.Attack : FreeAPType.Move;
        return value.Equals("Attack", System.StringComparison.OrdinalIgnoreCase)
            ? FreeAPType.Attack : FreeAPType.Move;
    }
}
