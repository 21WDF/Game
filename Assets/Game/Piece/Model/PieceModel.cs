using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 棋子运行时模型（纯数据 + 状态）—— 每个棋子实例一份。
/// 职责：持有运行时状态（当前血量、归属、本回合攻击标记、坐标）与对定义数据 / 视图的引用。
/// 不执行任何渲染或输入；行为（移动 / 攻击 / 死亡）由 PieceManager 驱动。
/// </summary>
public class PieceModel
{
    // ---- 定义数据（只读来源）----
    public PieceData Data { get; }

    // ---- 运行时状态 ----
    public int CurrentHP { get; private set; }
    public PlayerSide Owner { get; }
    public bool HasAttackedThisTurn { get; set; }
    /// <summary>强制瞬移（不走动画；传送石等场景设为 true）</summary>
    public bool ForceInstantMove { get; set; }
    public HexCoord Coord { get; private set; }

    // ---- 视图引用（PieceManager 同步两者）----
    public PieceView View { get; private set; }

    // ---- 装备槽位（3 槽；由 EquipmentManager 装备/卸下）----
    public EquipmentModel[] EquippedItems = new EquipmentModel[3];

    // ---- 装备加成聚合（只读；null 槽位跳过）----
    public int EquipmentAttack => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusAttack);
    public int EquipmentDefense => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusDefense);
    public int EquipmentMoveRange => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusMoveRange);
    public int EquipmentAttackRange => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusAttackRange);
    public int EquipmentHP => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusHP);

    // ---- 便捷属性（含装备加成；减法公式用）----
    public int EffectiveAttack => Data != null ? Data.attack + EquipmentAttack + GetTotalKillAttackBonus() + GetTempBuffTotal("Attack") + GetAuraBonus("Attack") : 0;
    public int EffectiveDefense => Data != null ? Data.defense + EquipmentDefense - CurrentDefenseReduction - PassiveDefenseReduction + TemporaryDefenseBonus + GetTotalStoneSkinDefense() + GetTempBuffTotal("Defense") + GetAuraBonus("Defense") : 0;
    public int MaxHP => Data != null ? Data.maxHP + EquipmentHP : 0;

    /// <summary>有效移动范围（内联 moveConfig.baseRange + 装备加成）</summary>
    public int MoveRange => Data != null ? Data.moveConfig.baseRange + EquipmentMoveRange : 0;

    /// <summary>有效攻击范围（内联 attackConfig.baseRange + 装备加成）</summary>
    public int AttackRange => Data != null ? Data.attackConfig.baseRange + EquipmentAttackRange : 0;

    // ---- 元素状态（元素城邦系统）----
    /// <summary>同种元素叠加时的 Gauge 上限（每回合 -1 自然消耗；5 足够容纳合理叠加场景）</summary>
    public const int MaxElementGauge = 5;
    /// <summary>当前附着元素（底元素；反应消耗后可能被新元素覆盖或清除）</summary>
    public ElementType AffixedElement { get; set; }
    /// <summary>附着元素量（&gt;0 表示有附着；每回合开始 -1，归零清除元素。同种元素攻击叠加，上限 MaxElementGauge）</summary>
    public int AffixedElementGauge { get; set; }
    /// <summary>当前防御降低值（超导反应触发；按 DefenseReductionTurnsRemaining 持续，耗尽后回合开始清零）</summary>
    public int CurrentDefenseReduction { get; set; }
    /// <summary>超导减防剩余持续回合（AttackPiece 施加时设为反应配置值；TurnManager 每回合开始递减，≤0 时清除减防）</summary>
    public int DefenseReductionTurnsRemaining { get; set; }

    /// <summary>被动来源防御降低值（爱可菲·冰锋蚀甲；与超导 CurrentDefenseReduction 独立并行、
    /// 各自按各自的持续回合递减，互不覆盖——同冰障 TemporaryDefenseBonus 分离先例）</summary>
    public int PassiveDefenseReduction { get; set; }
    /// <summary>被动减防剩余持续回合（FrostBreakPassive 施加时设置；TurnManager 每回合开始递减，≤0 清零）</summary>
    public int PassiveDefenseReductionTurnsRemaining { get; set; }

    // ---- 大招 / 能量 ----
    /// <summary>能量模型（仅当 Data.ultimateConfig 非空且非蓄力系统时构造；否则为 null。
    /// 蓄力型棋子（usesChargeSystem=true，甘雨）无能量条，用 ChargeStacks 驱动大招）</summary>
    public EnergyModel Energy { get; }
    /// <summary>是否可释放大招：蓄力型 = 蓄力层数 ≥1；能量型 = Energy 非空且满能</summary>
    public bool CanUseUltimate => IsChargeSystem ? ChargeStacks >= 1 : Energy != null && Energy.IsFull;
    /// <summary>是否已销毁（DestroyPiece 幂等标志：重入调用直接跳过，
    /// 防止傀儡等在伤害链内被引爆销毁后又被外层流程二次销毁/移位）</summary>
    public bool IsDestroyed { get; set; }

    /// <summary>临时防御加成（冰障大招等；纳入 EffectiveDefense，每回合由 TurnManager 重置为 0）</summary>
    public int TemporaryDefenseBonus { get; set; }

    /// <summary>是否蓄力系统型棋子（Data.usesChargeSystem，甘雨）：无能量条，普攻蓄力，大招按层数释放</summary>
    public bool IsChargeSystem => Data != null && Data.usesChargeSystem;
    /// <summary>蓄力层数上限（Data.maxChargeStacks；非蓄力型为 0）</summary>
    public int MaxChargeStacks => Data != null ? Data.maxChargeStacks : 0;
    private int _chargeStacks;
    /// <summary>当前蓄力层数（0..MaxChargeStacks，setter 自动钳制；普攻+1、大招释放清零、不随回合衰减）</summary>
    public int ChargeStacks
    {
        get => _chargeStacks;
        set => _chargeStacks = Mathf.Clamp(value, 0, Mathf.Max(0, MaxChargeStacks));
    }

    /// <summary>冻结剩余回合（冻结反应触发时设置；当前玩家回合结束时递减，归零即解冻）</summary>
    public int FreezeTurnsRemaining { get; set; }
    /// <summary>是否冻结（由 FreezeTurnsRemaining 派生；冻结状态下回合内无法做任何事）</summary>
    public bool IsFrozen => FreezeTurnsRemaining > 0;

    /// <summary>感电 DoT 每回合伤害（感电反应触发时设置；归零时清除）</summary>
    public int DotDamagePerTurn { get; set; }
    /// <summary>感电 DoT 剩余回合（每回合开始时结算扣血并递减）</summary>
    public int DotTurnsRemaining { get; set; }

    /// <summary>强化状态剩余回合（菲林斯被动：普攻命中后进入/刷新，持续 duration 回合；
    /// TurnManager.TickElementDuration 每次任一方回合开始递减，≤0 结束）。
    /// 强化状态改变大招效果形态（见 ThunderSweepUltimate）。</summary>
    public int EmpoweredTurnsRemaining { get; set; }
    /// <summary>是否处于强化状态（由 EmpoweredTurnsRemaining 派生）</summary>
    public bool IsEmpowered => EmpoweredTurnsRemaining > 0;

    /// <summary>易伤剩余回合（莫娜大招·星异：受到的伤害提高 VulnerablePercent%；
    /// TurnManager.TickElementDuration 每次任一方回合开始递减，≤0 清除；重复施加=刷新时长不叠加）</summary>
    public int VulnerableTurnsRemaining { get; set; }
    /// <summary>易伤增伤百分比（50 = 受到伤害 +50%；仅 VulnerableTurnsRemaining&gt;0 时生效，回合耗尽时清零）</summary>
    public int VulnerablePercent { get; set; }

    // ---- 被动系统（阶段②框架；具体被动效果 2.2 使用）----
    /// <summary>临时属性 buff 列表（如"攻击+10 持续10回合"）；生效/递减逻辑 2.2 实现</summary>
    public readonly List<TempBuff> TempBuffs = new();
    /// <summary>未受伤害连续回合计数（受伤害归零、回合开始 +1 的维护逻辑 2.2 接入）</summary>
    public int TurnsSinceDamaged { get; set; }
    /// <summary>上次受伤害来源（石像鬼板甲等反制型被动用；受伤害时由 AttackPiece / ApplyIncomingDamage 更新）</summary>
    public PieceModel LastDamageSource { get; set; }
    /// <summary>感电 DoT 的施加者（AttackPiece 施加感电时记录；DoT 回合结算时作为伤害来源传给 ApplyIncomingDamage）</summary>
    public PieceModel DotSource { get; set; }
    /// <summary>延迟伤害池（死亡之蔑等延迟结算被动用）</summary>
    public int PendingDamage { get; set; }
    /// <summary>冷却计时器（按效果名记录剩余回合；中娅悖论/探索者护臂/诸葛连弩用）</summary>
    public readonly Dictionary<string, int> Cooldowns = new();
    /// <summary>内建被动实例（SpawnPieceById 时由 PassiveFactory 从 Data.builtInPassives 创建）</summary>
    public List<IPassiveEffect> BuiltInPassives { get; } = new();

    /// <summary>统计 statType 匹配的临时 buff 总量（"Attack"/"Defense"；正=加成，负=削弱）</summary>
    public int GetTempBuffTotal(string statType)
    {
        int total = 0;
        for (int i = 0; i < TempBuffs.Count; i++)
            if (TempBuffs[i].statType == statType)
                total += TempBuffs[i].amount;
        return total;
    }

    /// <summary>聚合全部被动中 StoneSkinPassive 的叠层防御（内建+装备，逐项遍历不分配临时列表；对标 GetTotalKillAttackBonus）</summary>
    private int GetTotalStoneSkinDefense()
    {
        int total = 0;
        foreach (var passive in BuiltInPassives)
            if (passive is StoneSkinPassive s)
                total += s.DefenseBonus;
        for (int i = 0; i < EquippedItems.Length; i++)
        {
            var eq = EquippedItems[i];
            if (eq == null) continue;
            foreach (var passive in eq.ActivePassives)
                if (passive is StoneSkinPassive s)
                    total += s.DefenseBonus;
        }
        return total;
    }

    /// <summary>汇总该棋子的全部被动（内建 + 已装备装备的 ActivePassives），供事件分发遍历。
    /// 唯一被动去重（C2）：同 uniqueId 仅保留「最后装备」（EquipOrder 最大）装备上的那件，
    /// 先装备的被压制（不加入遍历结果，但装备仍在槽位、可正常卸下）；
    /// UniqueId 为 null/空的普通被动不参与去重。不同棋子互不影响。</summary>
    public List<IPassiveEffect> GetAllPassives()
    {
        var all = new List<IPassiveEffect>(BuiltInPassives);

        // 第一遍：记录每个 uniqueId → 最大 EquipOrder（该 id 最后装备的装备时间戳）
        Dictionary<string, int> latestOrderByUniqueId = null;
        for (int i = 0; i < EquippedItems.Length; i++)
        {
            var eq = EquippedItems[i];
            if (eq == null) continue;
            foreach (var passive in eq.ActivePassives)
            {
                if (passive == null) continue;
                string uid = passive.UniqueId;
                if (string.IsNullOrEmpty(uid)) continue;
                latestOrderByUniqueId ??= new Dictionary<string, int>();
                if (!latestOrderByUniqueId.TryGetValue(uid, out int latest) || eq.EquipOrder > latest)
                    latestOrderByUniqueId[uid] = eq.EquipOrder;
            }
        }

        // 第二遍：普通被动全部加入；唯一被动仅当所在装备为该 uniqueId 的最后装备时加入
        for (int i = 0; i < EquippedItems.Length; i++)
        {
            var eq = EquippedItems[i];
            if (eq == null) continue;
            foreach (var passive in eq.ActivePassives)
            {
                if (passive == null) continue;
                string uid = passive.UniqueId;
                if (!string.IsNullOrEmpty(uid) && latestOrderByUniqueId != null
                    && latestOrderByUniqueId.TryGetValue(uid, out int latest)
                    && eq.EquipOrder != latest)
                    continue;   // 被压制的唯一被动（同 id 有更晚装备的）
                all.Add(passive);
            }
        }
        return all;
    }

    /// <summary>查询型光环加成（C3）：遍历全场棋子的军团圣盾（经 GetAllPassives 尊重唯一被动去重），
    /// 累加对本棋子 statType 属性的光环加成。空安全（TurnManager/Model 未就绪返回 0）。
    /// 无递归风险：光环判定只用坐标/阵营，不查询 Effective* 属性。</summary>
    private int GetAuraBonus(string statType)
    {
        var model = TurnManager.Instance?.Model;
        if (model == null) return 0;

        int total = 0;
        CollectAura(model.Player1Pieces, statType, ref total);
        CollectAura(model.Player2Pieces, statType, ref total);
        return total;
    }

    /// <summary>收集一方棋子身上的光环对 (Coord, Owner, statType) 的加成</summary>
    private void CollectAura(List<PieceModel> pieces, string statType, ref int total)
    {
        foreach (var piece in pieces)
        {
            if (piece == null || piece.IsDead) continue;
            foreach (var passive in piece.GetAllPassives())
                if (passive is LegionAegisPassive aura)
                    total += aura.GetAuraAmount(statType, Coord, Owner);
        }
    }

    /// <summary>聚合全部被动中 KillAttackPassive 的攻击叠层（内建+装备，逐项遍历不分配临时列表）。
    /// 叠层跟随被动实例（=装备）：卸下自动移除、重装保留。</summary>
    private int GetTotalKillAttackBonus()
    {
        int total = 0;
        foreach (var passive in BuiltInPassives)
            if (passive is KillAttackPassive k)
                total += k.Accumulated;
        for (int i = 0; i < EquippedItems.Length; i++)
        {
            var eq = EquippedItems[i];
            if (eq == null) continue;
            foreach (var passive in eq.ActivePassives)
                if (passive is KillAttackPassive k)
                    total += k.Accumulated;
        }
        return total;
    }

    /// <summary>临时属性 buff（属性类型 + 数值 + 剩余回合）；statType 取值集合 2.2 与具体被动一起定。
    /// source 标记 buff 来源效果类名（如 TideSurgeUltimate；null=未标记）——同来源刷新式 buff 用它查重，不叠数值。</summary>
    [System.Serializable]
    public class TempBuff
    {
        public string statType = "Attack";
        public int amount;              // 数值（正=加成，负=削弱）
        public int turnsRemaining;      // 剩余回合
        public string source;           // 来源效果类名（可选；刷新式 buff 查重用）
    }

    public PieceModel(PieceData data, PlayerSide owner, HexCoord coord, PieceView view)
    {
        Data = data;
        Owner = owner;
        Coord = coord;
        View = view;
        CurrentHP = data != null ? data.maxHP : 0;
        HasAttackedThisTurn = false;
        // 能量系统：仅当配置了大招且非蓄力系统时创建 EnergyModel
        // （无配置或蓄力型（usesChargeSystem=true，甘雨）→ Energy=null：不响应能量，UI 能量条自动隐藏）
        Energy = (data != null && data.ultimateConfig != null && !data.usesChargeSystem)
            ? new EnergyModel(data.ultimateConfig.energyRequired)
            : null;
    }

    // ---- 状态变更（由 PieceManager 调用）----
    public void SetCoord(HexCoord coord) => Coord = coord;
    public void BindView(PieceView view) => View = view;

    public void TakeDamage(int damage)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - damage);
    }

    /// <summary>直接设置当前血量（clamp 到 [0, MaxHP]）；供装备系统卸下时截断使用</summary>
    public void SetHP(int value)
    {
        CurrentHP = Mathf.Clamp(value, 0, MaxHP);
    }

    /// <summary>回血（封顶 MaxHP）；回合结束基础回血等使用</summary>
    public void Heal(int amount)
    {
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }

    public bool IsDead => CurrentHP <= 0;
}
