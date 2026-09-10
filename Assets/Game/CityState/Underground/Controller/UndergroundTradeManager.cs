using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地下交易控制器（贸易之城·三期：累计伤害 + 事件状态机 + 商行）—— 单例 MonoBehaviour，
/// 持有 <see cref="UndergroundTradeModel"/>。挂载在场景中的 UndergroundTradeManager GameObject 上（手动创建）。
///
/// 一期口径：不判断城邦（机制常驻跑通，城邦联动后续统一接）；管理器不在场景时
/// 伤害统计静默跳过、商店无地下交易 Tab，行为与二期完全一致。
///
/// 职责：
///   1) 累计伤害统计（只读观察）：RecordDamage 由 PieceManager 的 3 个扣血点调用
///      （AttackPiece 主段 / ApplyIncomingDamage / ApplyExtraDamageSegments）；
///      只计 Attack/Splash/Ultimate（超载爆炸走 Splash 计入），Dot/Reflect 不计；被护盾挡的不计（调用点在拦截判定之后）。
///      累计 ≥ undergroundDamageThreshold（默认 60）且信誉 < undergroundReputationThreshold（默认 6）→ 事件激活。
///   2) 概率触发（按「轮」，后手方结束行动时由 TurnManager 调用 OnRoundEnded）：
///      事件激活且商行未开 → 每轮掷骰；概率 = min(100, 基础 10% + 每超阈值 30 伤害 +20%)。
///      随机入口收敛到 <see cref="RollOpenChance"/> 单一方法（在线 PVP 预留：网络化时替换为同步 seed）。
///   3) 关闭检查（按「回合」，每次任一方回合结束时由 TurnManager 调用 OnTurnEnded）：
///      事件激活的某方信誉 ≥ 6 → 全重置（清伤害/概率/关商行/关事件，重新累计）；< 6 → 商行保持开启。
///   4) 商行购买：只卖装备（复用 EquipmentManager.GrantEquipment 入背包）；
///      结算二选一——低价金币（透支扣款）/ 赊账（金币扣到更负）；不受信誉涨价影响；每件每次开启限购 1 件。
/// </summary>
public class UndergroundTradeManager : MonoBehaviour
{
    public static UndergroundTradeManager Instance { get; private set; }

    [Header("地下交易商行物品池（Inspector 拖入 UndergroundItemData；允许为空跑通流程）")]
    [SerializeField] private UndergroundItemData[] pool;

    // ---- MVC 分层 ----
    private UndergroundTradeModel _model;
    /// <summary>地下交易数据模型（UI 通过此订阅 OnStateChanged 事件）</summary>
    public UndergroundTradeModel Model => _model;

    [Header("配置资产（空则读 Resources/TradeConfig，再空则用运行时默认值）")]
    [Tooltip("Create > Chess > Trade Config 创建后拖入；改数值无需改代码")]
    public TradeConfig config;

    private TradeConfig _runtimeConfig;

    /// <summary>生效配置（懒加载：Inspector → Resources → 运行时默认实例）</summary>
    private TradeConfig Config
    {
        get
        {
            if (_runtimeConfig == null)
            {
                _runtimeConfig = config != null ? config
                    : (Resources.Load<TradeConfig>("TradeConfig") ?? ScriptableObject.CreateInstance<TradeConfig>());
            }
            return _runtimeConfig;
        }
    }

    /// <summary>地下交易生效判定（内聚过滤，含高频伤害统计入口）：仅当前城邦 == 贸易之城时生效；
    /// 非贸易城邦下不累计伤害、不激活、不掷骰、不结算、不可购买（整套路径不触发，无日志）</summary>
    private static bool TradeActive
        => CityStateManager.Instance != null && CityStateManager.Instance.IsActive(CityStateKind.Trade);

    // ---- 事件转发 ----
    /// <summary>某方地下交易状态变化（激活/开商行/关闭/购买）时触发</summary>
    public event System.Action<PlayerSide> OnStateChanged
    {
        add { if (_model != null) _model.OnStateChanged += value; }
        remove { if (_model != null) _model.OnStateChanged -= value; }
    }

    /// <summary>商行物品池（UI 遍历展示用）</summary>
    public IReadOnlyList<UndergroundItemData> Pool => pool;

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _model = new UndergroundTradeModel();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==========================================
    //  累计伤害统计（只读观察，由 PieceManager 扣血点调用；不得改变伤害结算）
    // ==========================================
    /// <summary>记录一次实际造成的直接伤害（护盾未拦截的那份）。
    /// 口径：只计 Attack/Splash/Ultimate（超载爆炸 = Splash，计入）；Dot/Reflect 不计。</summary>
    public void RecordDamage(PlayerSide attackerSide, int damage, DamageSource source)
    {
        if (_model == null || damage <= 0) return;
        if (!TradeActive) return;   // 城邦过滤：非贸易城邦下伤害统计入口直接跳过（高频路径，最轻判断）
        if (source != DamageSource.Attack && source != DamageSource.Splash && source != DamageSource.Ultimate) return;

        var state = _model.GetSide(attackerSide);
        state.AccumulatedDamage += damage;

        // 激活检查：累计 ≥ 阈值 且 信誉 < 门槛（每次伤害后即时检查，覆盖「先跌破信誉再打满伤害」路径）
        TryActivate(attackerSide);
    }

    /// <summary>激活判定（唯一入口，条件不变：累计直接伤害 ≥ 阈值 且 信誉 < 门槛）。
    /// 调用时点：①每次记录伤害后（覆盖「先跌破信誉、再打满伤害」）；
    /// ②每回合结束结算（覆盖「先打满伤害、之后才欠钱跌破信誉」——两个条件在不同时点变化，
    /// 任一条件下后满足时，最迟在下一个回合结束激活）。已激活玩家直接跳过。</summary>
    private void TryActivate(PlayerSide side)
    {
        var state = _model.GetSide(side);
        if (state.EventActive) return;

        int threshold = Config.undergroundDamageThreshold;
        if (state.AccumulatedDamage >= threshold && IsLowReputation(side))
        {
            state.EventActive = true;
            Debug.Log($"[UndergroundTradeManager] {side} 累计直接伤害达 {state.AccumulatedDamage} 且信誉 < {Config.undergroundReputationThreshold}，地下交易事件激活");
            _model.RaiseStateChanged(side);
        }
    }

    /// <summary>某方信誉是否低于地下交易门槛（只读查询 TradeCityManager；缺失视为不满足）</summary>
    private bool IsLowReputation(PlayerSide side)
    {
        if (TradeCityManager.Instance == null) return false;
        int threshold = Config.undergroundReputationThreshold;
        return TradeCityManager.Instance.GetReputation(side) < threshold;
    }

    // ==========================================
    //  概率触发（按「轮」；由 TurnManager.EndTurn 在行动方为 P2 时调用）
    // ==========================================
    public void OnRoundEnded()
    {
        if (_model == null) return;
        if (!TradeActive) return;   // 城邦过滤：非贸易城邦下概率掷骰整套不触发

        foreach (PlayerSide side in System.Enum.GetValues(typeof(PlayerSide)))
        {
            var state = _model.GetSide(side);
            if (!state.EventActive || state.ShopOpen) continue;

            int chance = GetOpenChancePercent(side);
            if (RollOpenChance(chance))
            {
                state.ShopOpen = true;
                state.Purchased.Clear();   // 每次开启恢复限购（重置关闭后再开也恢复）
                Debug.Log($"[UndergroundTradeManager] {side} 地下交易商行开启（概率 {chance}% 命中，累计伤害 {state.AccumulatedDamage}）");
                _model.RaiseStateChanged(side);
            }
            else
            {
                Debug.Log($"[UndergroundTradeManager] {side} 地下交易商行未开启（概率 {chance}% 未命中）");
            }
        }
    }

    /// <summary>当前开商行概率（%）：min(100, 基础 10% + 每超阈值 30 伤害 +20%）。60→10%，90→30%，120→50%…</summary>
    public int GetOpenChancePercent(PlayerSide side)
    {
        int threshold = Config.undergroundDamageThreshold;
        int stepDamage = Config.undergroundProbStepDamage;
        int stepPercent = Config.undergroundProbStepPercent;
        int basePercent = Config.undergroundProbBasePercent;

        int damage = _model.GetSide(side).AccumulatedDamage;
        int steps = 0;
        if (stepDamage > 0 && damage > threshold)
            steps = (damage - threshold) / stepDamage;   // 整数除法：90-60=30 → 1 步
        return Mathf.Min(100, basePercent + steps * stepPercent);
    }

    /// <summary>唯一随机入口：掷骰判定概率是否命中（将来在线 PVP 只需替换此方法为同步 seed 判定）</summary>
    private bool RollOpenChance(int chancePercent)
    {
        return UnityEngine.Random.Range(0, 100) < chancePercent;
    }

    // ==========================================
    //  关闭检查（按「回合」；由 TurnManager.EndTurn 每次调用）
    // ==========================================
    public void OnTurnEnded()
    {
        if (_model == null) return;
        if (!TradeActive) return;   // 城邦过滤：非贸易城邦下激活补判/关闭检查整套不触发

        foreach (PlayerSide side in System.Enum.GetValues(typeof(PlayerSide)))
        {
            var state = _model.GetSide(side);

            // 时序修复：对未激活玩家补一次激活判定（覆盖「先打满伤害、之后才欠钱跌破信誉」——
            // 信誉在回合结束结算里下降，那时点没有伤害记录触发判定；条件不变，只是补查）
            if (!state.EventActive)
            {
                TryActivate(side);
                continue;
            }

            // 信誉 ≥ 门槛 → 全重置（清累计伤害与概率、关商行与事件，回到初始状态重新累计）
            if (!IsLowReputation(side))
            {
                Debug.Log($"[UndergroundTradeManager] {side} 信誉回升 ≥ {Config.undergroundReputationThreshold}，地下交易关闭重置（累计伤害清零）");
                _model.ResetSide(side);
                _model.RaiseStateChanged(side);
            }
            // 信誉 < 门槛 → 商行保持开启（继续可买），事件保持激活
        }
    }

    // ==========================================
    //  商行查询与购买
    // ==========================================
    /// <summary>某方商行是否开启（UI 据此显示/隐藏地下交易 Tab）</summary>
    public bool IsShopOpen(PlayerSide side) => _model != null && _model.GetSide(side).ShopOpen;

    /// <summary>某方是否已购过某件商行物品（每次开启期间每件限购 1 件）</summary>
    public bool IsPurchased(PlayerSide side, UndergroundItemData item)
        => _model != null && _model.GetSide(side).Purchased.Contains(item);

    /// <summary>购买商行物品（不受信誉涨价影响——补偿通道定位）：
    /// LowPrice = 透支扣款 amount；Debt = 金币扣到更负 amount（加深欠债，不额外扣信誉）。
    /// 成功后装备入购买方背包（GrantEquipment）并标记已购。返回 false = 商行未开/已购/付不起。</summary>
    public bool BuyItem(PlayerSide side, UndergroundItemData item)
    {
        if (_model == null || item == null || item.equipment == null) return false;
        if (!TradeActive) return false;   // 城邦过滤：非贸易城邦下商行不可购买（Tab 已隐藏，双保险）

        var state = _model.GetSide(side);
        if (!state.ShopOpen)
        {
            Debug.Log($"[UndergroundTradeManager] {side} 商行未开启，无法购买");
            return false;
        }
        if (state.Purchased.Contains(item))
        {
            Debug.Log($"[UndergroundTradeManager] {item.name} 本次开启期间已购过（每件限购 1 件）");
            return false;
        }

        // 结算（原价，不走信誉涨价）：低价金币 或 赊账加深欠债，都复用透支扣款
        if (GoldManager.Instance == null || !GoldManager.Instance.TrySpendGoldWithOverdraft(side, item.amount))
        {
            Debug.Log($"[UndergroundTradeManager] {side} 无法结算 {item.name}（金额 {item.amount}，超出透支下限）");
            return false;
        }

        EquipmentManager.Instance?.GrantEquipment(side, item.equipment);
        state.Purchased.Add(item);

        string settle = item.settlement == UndergroundSettlement.LowPrice ? $"低价 {item.amount} 金币" : $"赊账（欠债 +{item.amount}）";
        Debug.Log($"[UndergroundTradeManager] {side} 购得 {item.equipment.displayName}（{settle}），装备入背包");
        _model.RaiseStateChanged(side);
        return true;
    }
}
