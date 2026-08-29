using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 元素反应查找表（元素城邦系统）—— 纯静态工具类，不继承 MonoBehaviour。
/// 参照 DamageCalculator 的写法：public static，无 Unity 依赖。
///
/// 反应规则（底元素 base + 触发元素 trigger → ReactionConfig）：
///   蒸发：火+水=1.5x / 水+火=2.0x
///   融化：火+冰=2.0x / 冰+火=1.5x
///   超载：火+雷=1.0x+10额外 / 雷+火同上
///   感电：水+雷=0.5x+DoT5×2 / 雷+水同上
///   冻结：水+冰=1.0x+冻结1回合 / 冰+水同上
///   超导：雷+冰=0.8x+减防5 / 冰+雷同上
///
/// 数据源改造：原硬编码 _table 字典已删除，改为由 ReactionDatabaseSO（ScriptableObject）
/// 提供数据，运行时由 RuntimeReactionTable 管理可变状态（覆盖/修饰器）。
/// 新增反应只需在 Inspector 的 ReactionDatabase.asset 中加行，不改代码。
///
/// 副作用结算（PieceManager.AttackPiece 触发，TurnManager 结算）：
///   超导减防：攻击前预设 CurrentDefenseReduction，当次伤害即享受减防
///   感电 DoT：触发时设置 DotDamagePerTurn/DotTurnsRemaining，每回合开始由 TickElementDuration 扣血
///   冻结：触发时设置 FreezeTurnsRemaining，冻结棋子不可选中（BattleController 拦截），回合末递减
/// </summary>
public static class ElementReactionTable
{
    /// <summary>元素系统生效判定（元素系统本体的城邦过滤，内聚在核心入口）：
    /// 仅当前城邦 == 元素城邦时元素系统生效；其他城邦下不反应、不附着（退回「无元素」基线）。
    /// 消费方：GetReaction / ResolveElementInteraction / ElementColorMapper / UI 元素图标。</summary>
    public static bool ElementSystemActive
        => CityStateManager.Instance != null && CityStateManager.Instance.IsActive(CityStateKind.Element);

    /// <summary>反应配置（描述一次元素反应的数值效果）</summary>
    [System.Serializable]
    public struct ReactionConfig
    {
        public ReactionType Type;              // 反应类型
        public float DamageMultiplier;         // 伤害倍率
        public bool ClearBase;                 // 是否清除底元素（本阶段不单独使用，覆盖即消耗）
        public int ExtraDamage;                // 额外固定伤害
        public int DotDamagePerTurn;           // DoT 每回合伤害（感电；TurnManager.TickElementDuration 结算）
        public int DotTurns;                   // DoT 持续回合（感电）
        public int DefenseReduction;           // 防御降低值（超导；AttackPiece 伤害计算前预设）
        public int DefenseReductionTurns;      // 减防持续回合数（超导；0=当次生效后下回合开始重置≈旧行为；TickElementDuration 按剩余回合递减）
        public int FreezeTurns;                // 冻结回合（冻结；BattleController 拦截选中，EndTurn 递减）
        public int KnockbackDistance;          // 击退格数（超载；AttackPiece 伤害结算后由 KnockbackResolver 处理）
    }

    /// <summary>无反应配置（Type=None、倍率 1、无任何副作用）——非元素城邦下的统一返回</summary>
    private static ReactionConfig NoReaction => new ReactionConfig
    {
        Type = ReactionType.None,
        DamageMultiplier = 1.0f
    };

    /// <summary>查询反应配置（向后兼容：无修饰器）。
    /// 城邦过滤：非元素城邦下恒返回「无反应」（倍率 1、无副作用）——蒸发/融化/超载/感电/冻结/超导全部不触发。</summary>
    public static ReactionConfig GetReaction(ElementType baseElement, ElementType trigger)
    {
        if (!ElementSystemActive) return NoReaction;
        return RuntimeReactionTable.Instance.Resolve(baseElement, trigger, null);
    }

    /// <summary>查询反应配置（带棋子修饰器链：装备被动用）。
    /// 城邦过滤：非元素城邦下恒返回「无反应」（修饰器链/数据库均不经过，数据源零改动）。</summary>
    public static ReactionConfig GetReaction(ElementType baseElement, ElementType trigger,
        IReadOnlyList<IReactionModifier> pieceModifiers)
    {
        if (!ElementSystemActive) return NoReaction;
        return RuntimeReactionTable.Instance.Resolve(baseElement, trigger, pieceModifiers);
    }

    // ==========================================
    //  元素克制查询
    // ==========================================
    /// <summary>
    /// 元素克制查询：attacker 是否克制 defender。
    /// 克制表（单向）：水 → 克 火；火 → 克 冰。雷不参与克制。
    /// 克制仅影响反应消耗量（克制方有效值 ×2，实际消耗 ÷2），不影响伤害倍率。
    /// </summary>
    public static bool IsCountered(ElementType attacker, ElementType defender)
    {
        return (attacker == ElementType.Water && defender == ElementType.Fire)
            || (attacker == ElementType.Fire && defender == ElementType.Ice);
    }

    // ==========================================
    //  元素交互结算（Gauge 消耗算法）
    // ==========================================
    /// <summary>
    /// 元素交互结算：计算反应消耗后的最终附着元素和 Gauge。
    ///
    /// 三种情况：
    ///   1. 无底元素（baseGauge ≤ 0）→ 直接附着触发元素，Gauge = triggerGauge
    ///   2. 同种元素 → 叠加，Gauge = min(baseGauge + triggerGauge, MaxElementGauge)
    ///   3. 不同元素 → 反应消耗算法（纯整数，无浮点）：
    ///      a. baseEff = baseGauge; trigEff = triggerGauge
    ///      b. 触发方克底方 → trigEff *= 2；底方克触发方 → baseEff *= 2
    ///      c. consume = min(baseEff, trigEff)
    ///      d. 底方克触发方 → baseConsumed = consume/2, trigConsumed = consume
    ///         触发方克底方 → baseConsumed = consume, trigConsumed = consume/2
    ///         无克制       → baseConsumed = consume, trigConsumed = consume
    ///      e. baseGauge -= baseConsumed; triggerGauge -= trigConsumed
    ///      f. baseGauge > 0 → 保留底元素；triggerGauge > 0 → 附着变为触发元素；都 ≤0 → 清除
    /// </summary>
    /// <param name="baseElement">目标当前附着元素（底元素）</param>
    /// <param name="baseGauge">目标当前元素量</param>
    /// <param name="triggerElement">攻击方元素（触发元素）</param>
    /// <param name="triggerGauge">本次攻击的触发元素量（普攻=1，大招=2）</param>
    /// <returns>(最终附着元素, 最终元素量)；元素量 ≤0 时元素为 None</returns>
    public static (ElementType element, int gauge) ResolveElementInteraction(
        ElementType baseElement, int baseGauge,
        ElementType triggerElement, int triggerGauge)
    {
        // 城邦过滤：非元素城邦下不附着、不消耗（无元素基线）——所有附着写入点据此写 None/0
        if (!ElementSystemActive)
            return (ElementType.None, 0);

        // 无触发元素 → 不变
        if (triggerElement == ElementType.None)
            return (baseElement, baseGauge);

        // 无底元素 → 直接附着触发元素
        if (baseGauge <= 0 || baseElement == ElementType.None)
            return (triggerElement, triggerGauge);

        // 同种元素 → 叠加（带上限）
        if (baseElement == triggerElement)
            return (baseElement, Mathf.Min(baseGauge + triggerGauge, PieceModel.MaxElementGauge));

        // ---- 不同元素 → 反应消耗算法 ----

        // 步骤 2：有效值（克制方 ×2）
        int baseEff = baseGauge;
        int trigEff = triggerGauge;
        if (IsCountered(triggerElement, baseElement)) trigEff *= 2;  // 触发方克底方
        if (IsCountered(baseElement, triggerElement)) baseEff *= 2;  // 底方克触发方

        // 步骤 3：有效消耗量
        int consume = Mathf.Min(baseEff, trigEff);

        // 步骤 4：实际消耗（克制方消耗减半，整数除法向下取整）
        int baseConsumed, trigConsumed;
        if (IsCountered(baseElement, triggerElement))
        {
            // 底方克触发方：底方消耗减半，保底至少 1（反应发生时底方必须有消耗）
            baseConsumed = Mathf.Max(consume / 2, 1);
            trigConsumed = consume;
        }
        else if (IsCountered(triggerElement, baseElement))
        {
            // 触发方克底方：触发方消耗减半
            baseConsumed = consume;
            trigConsumed = consume / 2;
        }
        else
        {
            // 无克制：双方等量消耗
            baseConsumed = consume;
            trigConsumed = consume;
        }

        // 步骤 5：扣除 Gauge
        baseGauge -= baseConsumed;
        triggerGauge -= trigConsumed;

        // 步骤 6：剩余方成为新附着（数学上不可能双方都 >0，见算法分析）
        if (baseGauge > 0)
            return (baseElement, baseGauge);
        if (triggerGauge > 0)
            return (triggerElement, triggerGauge);
        return (ElementType.None, 0);
    }
}
