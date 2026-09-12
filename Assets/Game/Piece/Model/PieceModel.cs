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
    // 季风之城（夏·攻击聚合）：查询式聚合，生效判断内聚在 MonsoonManager（非季风城邦返回 0）
    // 永久加成（季风·雷暴三期C）：对局内永久、可叠加累积、无回合递减；只新增求和项，不改现有计算逻辑
    // 战争之城·主将加成：主将四维加成 + 装备实际提供项的额外加成（判断内聚 WarCityManager，非战争城邦/非主将返回 0）
    public int EffectiveAttack => Data != null ? Data.attack + EquipmentAttack + GetTotalKillAttackBonus() + GetTempBuffTotal("Attack") + GetAuraBonus("Attack") + (MonsoonManager.Instance != null ? MonsoonManager.Instance.GetAttackBonus() : 0) + PermanentAttackBonus + (WarCityManager.Instance != null ? WarCityManager.Instance.GetGeneralAttackBonus(this) + WarCityManager.Instance.GetGeneralEquipBonus(this, EquipmentAttack) : 0) : 0;
    public int EffectiveDefense => Data != null ? Data.defense + EquipmentDefense - CurrentDefenseReduction - PassiveDefenseReduction + TemporaryDefenseBonus + GetTotalStoneSkinDefense() + GetTempBuffTotal("Defense") + GetAuraBonus("Defense") + PermanentDefenseBonus + (WarCityManager.Instance != null ? WarCityManager.Instance.GetGeneralDefenseBonus(this) + WarCityManager.Instance.GetGeneralEquipBonus(this, EquipmentDefense) : 0) : 0;
    public int MaxHP => Data != null ? Data.maxHP + EquipmentHP + PermanentHPBonus + (WarCityManager.Instance != null ? WarCityManager.Instance.GetGeneralHPBonus(this) + WarCityManager.Instance.GetGeneralEquipBonus(this, EquipmentHP) : 0) : 0;

    /// <summary>有效移动范围（内联 moveConfig.baseRange + 装备加成）。
    /// 季风之城（昼夜修正）：昼 +1 / 夜 -1，最终 clamp 下限 1（夜至少可行动 1 格）；
    /// 生效判断与 clamp 内聚在 MonsoonManager（非季风城邦原样返回，行为不变）。
    /// 永久加成（雷暴三期C）：+PermanentMoveBonus 求和项。
    /// 战争之城·主将：+主将移动加成 + 装备实际提供移动时的额外加成（内聚 WarCityManager）。
    /// 冰元素格减益：站在冰元素格上移动距离 -N（全城邦通用；查询式实时生效——离开/格子消失立即恢复，
    /// 减在最终聚合结果上，保底 1）</summary>
    public int MoveRange
    {
        get
        {
            if (Data == null) return 0;
            int range = MonsoonManager.Instance != null
                ? MonsoonManager.Instance.GetMoveRange(Data.moveConfig.baseRange + EquipmentMoveRange + PermanentMoveBonus + (WarCityManager.Instance != null ? WarCityManager.Instance.GetGeneralMoveBonus(this) + WarCityManager.Instance.GetGeneralEquipBonus(this, EquipmentMoveRange) : 0))
                : Data.moveConfig.baseRange + EquipmentMoveRange + PermanentMoveBonus + (WarCityManager.Instance != null ? WarCityManager.Instance.GetGeneralMoveBonus(this) + WarCityManager.Instance.GetGeneralEquipBonus(this, EquipmentMoveRange) : 0);
            int debuff = ElementTileManager.Instance != null ? ElementTileManager.Instance.GetMoveRangeDebuff(this) : 0;
            return debuff > 0 ? Mathf.Max(1, range - debuff) : range;
        }
    }

    /// <summary>有效攻击范围（内联 attackConfig.baseRange + 装备加成）。
    /// 季风之城（暴雨）：上回合受击的棋子 -N（下限 1）；生效判断与 clamp 内聚在 MonsoonManager
    ///（非季风城邦/未受击原样返回，行为不变）。永久加成（雷暴三期C）：+PermanentAttackRangeBonus 求和项。
    /// 战争之城·主将：+装备实际提供射程时的额外加成（主将基础四维无射程；内聚 WarCityManager）。
    /// 水元素格减益：站在水元素格上攻击距离 -N（全城邦通用；查询式实时生效——离开/格子消失立即恢复，
    /// 减在最终聚合结果上，保底 1）</summary>
    public int AttackRange
    {
        get
        {
            if (Data == null) return 0;
            int range = MonsoonManager.Instance != null
                ? MonsoonManager.Instance.GetAttackRange(this, Data.attackConfig.baseRange + EquipmentAttackRange + PermanentAttackRangeBonus + (WarCityManager.Instance != null ? WarCityManager.Instance.GetGeneralEquipBonus(this, EquipmentAttackRange) : 0))
                : Data.attackConfig.baseRange + EquipmentAttackRange + PermanentAttackRangeBonus + (WarCityManager.Instance != null ? WarCityManager.Instance.GetGeneralEquipBonus(this, EquipmentAttackRange) : 0);
            int debuff = ElementTileManager.Instance != null ? ElementTileManager.Instance.GetAttackRangeDebuff(this) : 0;
            return debuff > 0 ? Mathf.Max(1, range - debuff) : range;
        }
    }

    // ---- 对局内永久属性加成（季风·雷暴三期C：雷劈概率获得；随棋子存续、无回合递减、可叠加累积）----
    public int PermanentHPBonus { get; set; }
    public int PermanentAttackBonus { get; set; }
    public int PermanentDefenseBonus { get; set; }
    public int PermanentMoveBonus { get; set; }
    public int PermanentAttackRangeBonus { get; set; }

    /// <summary>施加一条永久加成（雷暴雷劈入口调用；累加式，多次雷劈可叠加）</summary>
    public void AddPermanentBonus(MonsoonConfig.PermanentBonusType type, int value)
    {
        if (value == 0) return;
        switch (type)
        {
            case MonsoonConfig.PermanentBonusType.HP: PermanentHPBonus += value; break;
            case MonsoonConfig.PermanentBonusType.Attack: PermanentAttackBonus += value; break;
            case MonsoonConfig.PermanentBonusType.Defense: PermanentDefenseBonus += value; break;
            case MonsoonConfig.PermanentBonusType.Move: PermanentMoveBonus += value; break;
            case MonsoonConfig.PermanentBonusType.Range: PermanentAttackRangeBonus += value; break;
        }
    }

    // ---- 元素状态（元素城邦系统）----
    /// <summary>同种元素叠加时的 Gauge 上限（每回合 -1 自然消耗；5 足够容纳合理叠加场景）</summary>
    public const int MaxElementGauge = 5;
    /// <summary>当前附着元素（底元素；反应消耗后可能被新元素覆盖或清除）。
    /// setter 在「值变化」时触发 <see cref="OnElementChanged"/>（纯视觉通知，不参与任何结算；
    /// 光环/UI 图标等订阅显示）。同元素 Gauge 增减不触发（值未变）。</summary>
    public ElementType AffixedElement
    {
        get => _affixedElement;
        set
        {
            if (_affixedElement == value) return;
            _affixedElement = value;
            OnElementChanged?.Invoke(value);
        }
    }
    private ElementType _affixedElement;
    /// <summary>附着元素变化事件（纯通知，不参与任何结算逻辑）：元素光环显示/隐藏/换色订阅。
    /// 参数 = 最新元素（None = 附着被消耗/清除）。</summary>
    public event System.Action<ElementType> OnElementChanged;
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

    // ---- 护盾系统（基座）----
    /// <summary>护盾层数（0 = 无盾；上限 3，每层免一次伤害。由 PieceManager.TryAbsorbByShield 拦截消耗；
    /// 圣盾等被动施加时刷新到上限不叠加。护盾只拦「伤害数值」——元素附着/反应/冻结/感电/减防照常发生）。
    /// 修改一律走 <see cref="SetShield"/>（触发 UI 事件），不要直接赋值</summary>
    public int ShieldStacks { get; private set; }
    /// <summary>护盾元素标签（None = 普通盾；元素盾对同元素伤害免疫【免伤不扣层】，
    /// 真实伤害无视元素免疫仍扣层。二期「元素城邦染色」将写入此字段）</summary>
    public ElementType ShieldElement { get; private set; }
    /// <summary>是否有护盾（派生）</summary>
    public bool HasShield => ShieldStacks > 0;

    /// <summary>护盾层数变化事件（对标 EnergyModel.OnEnergyChanged）：获得/消耗/破碎/刷新时触发，
    /// 参数 = 最新层数 + 元素标签（元素为二期染色预留）。UI 订阅显示层数；层数 0 时 UI 隐藏。
    /// 纯通知，不参与任何结算逻辑。</summary>
    public event System.Action<int, ElementType> OnShieldChanged;

    /// <summary>护盾统一修改入口（施加/扣层/破碎/刷新都走这里）：赋值 + 触发 OnShieldChanged。
    /// stacks &lt; 0 按 0 处理；层数归 0 时元素标签一并清空（盾碎语义）。</summary>
    public void SetShield(int stacks, ElementType element)
    {
        if (stacks <= 0)
        {
            stacks = 0;
            element = ElementType.None;
        }
        ShieldStacks = stacks;
        ShieldElement = element;
        OnShieldChanged?.Invoke(stacks, element);
    }

    // ---- 护盾元素染色（元素城邦二期；生效条件 = master 总闸开 且 当前城邦 == 元素城邦）----
    /// <summary>染色机制是否生效（判定收敛到 ElementCityManager 统一入口：总闸开 且 当前城邦 == 元素城邦，
    /// 两个条件语义不变；总闸取值改读 ElementCityConfig——非元素城邦下不染色、不封印（护盾退回普通层数护盾）。
    /// 护盾基座（层数/拦截/吸血金币拦截/UI）不受此开关影响。</summary>
    private static bool DyeingActive => ElementCityManager.DyeingActive;

    /// <summary>封印判定（查询，不改状态）：染色生效时，已染色护盾对「同元素」附着封印——
    /// 该元素无法再附着到本棋子（其他元素照常）。染色未生效/未染色/无盾 → 永不封印。
    /// 由各元素附着调用点在写入 AffixedElement 前查询；元素反应照常发生（封印的是附着，不是反应）。</summary>
    public bool BlocksElementAttachment(ElementType element)
    {
        if (element == ElementType.None) return false;
        if (!DyeingActive) return false;
        return HasShield && ShieldElement == element;
    }

    /// <summary>染色入口（附着结算后调用）：染色生效 + 有盾 + 未染色 + 本次附着结果非 None
    /// → 护盾永久变为该元素盾。走 SetShield 触发 UI 事件；重复染色（已有元素标签）不生效。
    /// 返回 true = 发生了染色。</summary>
    public bool TryDyeShieldElement(ElementType attachedElement)
    {
        if (attachedElement == ElementType.None) return false;
        if (!DyeingActive) return false;
        if (!HasShield || ShieldElement != ElementType.None) return false;
        SetShield(ShieldStacks, attachedElement);
        Debug.Log($"[PieceModel] {Data.displayName} 的护盾被染色为 {attachedElement}元素盾");
        return true;
    }

    /// <summary>最近一次受击是否被护盾拦截（AttackPiece 每次结算后更新；
    /// LifestealPassive 据此跳过吸血——护盾免伤 = 攻击方无伤害收益。
    /// 仅供伤害收益类被动读取，不参与数值结算）</summary>
    public bool LastHitAbsorbedByShield { get; set; }

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
