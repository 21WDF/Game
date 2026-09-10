using UnityEngine;

/// <summary>
/// 贸易之城经济控制器（利息 + 信誉，二期）—— 单例 MonoBehaviour，持有 <see cref="TradeCityModel"/>。
/// 挂载在场景中的 TradeCityManager GameObject 上（手动创建，对标 GoldManager/AuctionManager）。
///
/// 结算调度（时间单位铁律）：
///   - OnTurnEnded（每次任一方回合结束，由 TurnManager.EndTurn 调用）：
///     欠钱方连续欠钱计数 +1；满 debtCreditTickTurns（默认 8）回合扣一次信誉 floor(欠钱数/interestGoldInterval)，
///     扣完计数归零续计（再满 8 回合再扣）；金币 ≥ 0 的玩家不计数。
///   - OnRoundEnded（后手方结束行动、一轮完成时，由 TurnManager.EndTurn 在行动方为 P2 时调用）：
///     ① 正利息：金币 > 0 的玩家获得 floor(金币 / interestGoldInterval)（金币为负无利息、不扣款）；
///     ② 信誉恢复：金币 ≥ 0 的玩家信誉 +1（上限 10）。
///   - 订阅 GoldModel.OnGoldSettled：金币回到 ≥ 0 的瞬间重置该方欠钱计数（精确兑现「中途还清则计数重置」，
///     覆盖回合中途因伤害收入短暂还清的情况）。
///
/// 查询接口（消费方）：
///   - GetAdjustedPrice：商店涨价后售价 = floor(原价 × (1 + (10-信誉) × percent%))；信誉 10 = 原价。
///     仅 EquipmentManager.BuyEquipment / GridItemManager.BuyToInventory 使用；拍卖不涨。
///   - CanBidAuction：信誉 ≥ auctionReputationThreshold（默认 6）才可出价；商店不受此限制。
///
/// 二期不判断城邦（同一期口径：机制常驻跑通，城邦联动后续统一接）；管理器不在场景时
/// 所有消费方回退一期行为（原价、不设拍卖门槛）。地下交易是三期，本期不碰。
/// </summary>
public class TradeCityManager : MonoBehaviour
{
    public static TradeCityManager Instance { get; private set; }

    // ---- MVC 分层 ----
    private TradeCityModel _model;
    /// <summary>经济数据模型（UI 通过此订阅 OnReputationChanged 事件）</summary>
    public TradeCityModel Model => _model;

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

    /// <summary>贸易机制生效判定（内聚过滤，不散落到调用点）：仅当前城邦 == 贸易之城时生效；
    /// 其他城邦下整套路径不触发、查询接口退回基线（原价 / 无拍卖门槛 / 满信誉）</summary>
    private static bool TradeActive
        => CityStateManager.Instance != null && CityStateManager.Instance.IsActive(CityStateKind.Trade);

    /// <summary>查询某方当前信誉（0 ~ 10）</summary>
    public int GetReputation(PlayerSide side) => _model != null ? _model.GetReputation(side) : TradeCityModel.MaxReputation;

    /// <summary>信誉事件转发（UI 订阅入口）</summary>
    public event System.Action<PlayerSide, int> OnReputationChanged
    {
        add { if (_model != null) _model.OnReputationChanged += value; }
        remove { if (_model != null) _model.OnReputationChanged -= value; }
    }

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _model = new TradeCityModel();
    }

    private void Start()
    {
        // 金币回到 ≥ 0 的瞬间重置欠钱计数（「中途还清则计数重置」的事件驱动实现）
        if (GoldManager.Instance != null && GoldManager.Instance.Model != null)
            GoldManager.Instance.Model.OnGoldSettled += OnGoldSettled;
    }

    private void OnDestroy()
    {
        if (GoldManager.Instance != null && GoldManager.Instance.Model != null)
            GoldManager.Instance.Model.OnGoldSettled -= OnGoldSettled;
        if (Instance == this) Instance = null;
    }

    /// <summary>金币变化回调：回到 ≥ 0 → 重置该方连续欠钱计数（非贸易城邦下信誉机制整体不生效）</summary>
    private void OnGoldSettled(PlayerSide side, int gold)
    {
        if (!TradeActive) return;
        if (gold >= 0)
            _model?.ResetDebtTurns(side);
    }

    // ==========================================
    //  回合结束结算（欠钱扣信誉计数按「回合」；由 TurnManager.EndTurn 每次调用）
    // ==========================================
    public void OnTurnEnded()
    {
        if (_model == null || GoldManager.Instance == null) return;
        if (!TradeActive) return;   // 城邦过滤：非贸易城邦下信誉机制整套不触发

        foreach (PlayerSide side in System.Enum.GetValues(typeof(PlayerSide)))
        {
            int gold = GoldManager.Instance.GetGold(side);
            if (gold >= 0)
            {
                _model.ResetDebtTurns(side);   // 双保险（事件驱动已重置；回合末再核一次）
                continue;
            }

            _model.IncrementDebtTurns(side);
            int tick = Config.debtCreditTickTurns;
            if (_model.GetDebtTurns(side) < tick) continue;

            // 满 N 回合扣一次：floor(欠钱数 / 利息结算间隔)；欠 <10 扣 0（计数仍归零续计）
            int interval = Config.interestGoldInterval;
            int deduction = -gold / interval;   // gold < 0，向下取整用整数除法（欠 11 → 1，欠 39 → 3）
            _model.ResetDebtTurns(side);        // 扣完计数归零；继续欠钱再满 N 回合会再扣一次
            if (deduction > 0)
            {
                _model.AddReputation(side, -deduction);
                Debug.Log($"[TradeCityManager] {side} 持续欠钱 {-gold} 金币满 {tick} 回合，信誉 -{deduction}（当前 {_model.GetReputation(side)}）");
            }
        }
    }

    // ==========================================
    //  轮结束结算（利息 + 信誉恢复按「轮」；由 TurnManager.EndTurn 在行动方为 P2 时调用）
    // ==========================================
    public void OnRoundEnded()
    {
        if (_model == null || GoldManager.Instance == null) return;
        if (!TradeActive) return;   // 城邦过滤：非贸易城邦下利息/信誉恢复整套不触发

        int interval = Config.interestGoldInterval;
        if (interval <= 0) interval = 10;

        foreach (PlayerSide side in System.Enum.GetValues(typeof(PlayerSide)))
        {
            int gold = GoldManager.Instance.GetGold(side);

            // ① 正利息：金币 > 0 每拥有 interval 金币 +1（向下取整）；金币为负无利息（欠钱不会自己滚大）
            if (gold > 0)
            {
                int interest = gold / interval;
                if (interest > 0)
                {
                    GoldManager.Instance.AddGold(side, interest);
                    Debug.Log($"[TradeCityManager] {side} 结算利息 +{interest}（持有 {gold}，每 {interval} 金币 +1）");
                }
            }

            // ② 信誉恢复：脱离欠钱（金币 ≥ 0）每轮 +1，上限 10
            if (gold >= 0 && _model.GetReputation(side) < TradeCityModel.MaxReputation)
            {
                _model.AddReputation(side, 1);
                Debug.Log($"[TradeCityManager] {side} 脱离欠钱，信誉恢复 +1（当前 {_model.GetReputation(side)}）");
            }
        }
    }

    // ==========================================
    //  查询接口（消费方）
    // ==========================================
    /// <summary>商店涨价后售价 = floor(原价 × (1 + (10-信誉) × percent%))；信誉 10 = 原价。
    /// 仅用于商店购买（买装备/买道具）；拍卖成交价、底价、出价一律不涨。
    /// 非贸易城邦下返回原价（信誉机制整体不生效）。</summary>
    public int GetAdjustedPrice(PlayerSide side, int basePrice)
    {
        if (_model == null || !TradeActive) return basePrice;
        int percent = Config.shopPricePercentPerReputation;
        int lost = TradeCityModel.MaxReputation - _model.GetReputation(side);
        if (lost <= 0 || percent <= 0) return basePrice;
        return basePrice * (100 + lost * percent) / 100;   // 整数运算天然向下取整（13×1.2 → 15）
    }

    /// <summary>涨价百分比（UI 展示用；信誉 10 → 0；非贸易城邦 → 0）</summary>
    public int GetPricePercent(PlayerSide side)
    {
        if (_model == null || !TradeActive) return 0;
        int percent = Config.shopPricePercentPerReputation;
        return (TradeCityModel.MaxReputation - _model.GetReputation(side)) * percent;
    }

    /// <summary>拍卖出价资格：信誉 ≥ 门槛（默认 6）才可出价；商店购买不受此限制。
    /// 非贸易城邦下无门槛（信誉机制整体不生效）。</summary>
    public bool CanBidAuction(PlayerSide side)
    {
        if (!TradeActive) return true;
        int threshold = Config.auctionReputationThreshold;
        return GetReputation(side) >= threshold;
    }
}
