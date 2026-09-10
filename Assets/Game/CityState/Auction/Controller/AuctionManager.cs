using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 拍卖行控制器（贸易之城·一期：透支 + 拍卖本体）—— 单例 MonoBehaviour，对标 GoldManager 写法。
/// 挂载在场景中的 AuctionManager GameObject 上（手动创建）。
///
/// 一期范围：
///   1) 物品池 pool 由 Inspector 拖入 AuctionItemData（asset 用户自配；空池可跑通流程）；
///   2) 出价：仅当前回合玩家可出价；一键出「最低可行价」（首次 ≥ 底价，后续 ≥ 当前价 + 最低加价幅度）；
///      出价时校验支付能力（当前金币 - 出价额 ≥ 透支下限，防恶意抬价后付不起）；
///   3) 成交：出价后倒计时（默认 6 回合）内无人加价 → 归零成交，扣出价金额（可透支），装备进买家背包；
///   4) 刷新：开局放 1 件；之后每 5 回合从池中随机补 1 件「未售且未在拍」的物品；已售集合防重复；
///   5) 时间单位铁律：倒计时与刷新严格按「回合」（CurrentTurn，每次任一方回合结束）计，绝不按「轮」计
///      —— 用轮会让后手报价者白赚半轮优势。
///
/// 利息/信誉/地下交易是二三期，本期不碰。本期不判断城邦（先把机制本体跑通，
/// 「仅贸易之城生效」的城邦联动后续再接，CityStateManager 保持原样零改动）。
/// </summary>
public class AuctionManager : MonoBehaviour
{
    public static AuctionManager Instance { get; private set; }

    [Header("拍卖物品池（Inspector 拖入 AuctionItemData；允许为空跑通流程）")]
    [SerializeField] private AuctionItemData[] pool;

    // ---- 运行时状态 ----
    private readonly List<AuctionLot> _lots = new();
    private readonly HashSet<AuctionItemData> _sold = new();   // 已售物品集合（刷新时不重复添加）
    private int _turnsSinceRefresh;                            // 距上次刷新经过的回合数
    private bool _initialSpawnDone;                            // 初始拍品是否已投放（等城邦确定为贸易后投）

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

    /// <summary>拍卖机制生效判定（内聚过滤）：仅当前城邦 == 贸易之城时生效；
    /// 非贸易城邦下不出价、不倒计时、不刷新、不投放初始拍品（无任何日志）</summary>
    private static bool TradeActive
        => CityStateManager.Instance != null && CityStateManager.Instance.IsActive(CityStateKind.Trade);

    // ---- 事件 ----
    /// <summary>拍卖行状态变化时触发（出价/成交/刷新），UI 据此刷新拍卖 Tab</summary>
    public event Action OnAuctionChanged;

    /// <summary>当前在拍物品（只读视图）</summary>
    public IReadOnlyList<AuctionLot> ActiveLots => _lots;

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 城邦过滤：初始拍品只在「本局城邦确定为贸易之城」时投放（城邦选择晚于场景 Start，
        // 订阅城邦变化事件；若订阅时已确定则立即投放）
        if (CityStateManager.Instance != null)
        {
            CityStateManager.Instance.OnCityStateChanged += OnCityStateChangedForInitialSpawn;
            TryInitialSpawn();
        }
    }

    private void OnDestroy()
    {
        if (CityStateManager.Instance != null)
            CityStateManager.Instance.OnCityStateChanged -= OnCityStateChangedForInitialSpawn;
        if (Instance == this) Instance = null;
    }

    /// <summary>城邦确定/变化回调：成为贸易之城 → 投放初始拍品（仅一次）</summary>
    private void OnCityStateChangedForInitialSpawn(CityStateKind oldKind, CityStateKind newKind)
    {
        TryInitialSpawn();
    }

    /// <summary>投放初始拍品（城邦 == 贸易 且 未投过；池空/无有效条目也标记已投，避免重复尝试）</summary>
    private void TryInitialSpawn()
    {
        if (_initialSpawnDone || !TradeActive) return;
        _initialSpawnDone = true;
        AddNextFromPool();
    }

    // ==========================================
    //  出价
    // ==========================================
    /// <summary>当前回合玩家对拍品出价（一键出最低可行价）。
    /// 规则：仅当前回合玩家可出价；金额 = MinNextBid（自动满足「≥ 当前价 + 最低加价幅度」，首次 ≥ 底价）；
    /// 出价时校验支付能力（当前金币 - 出价额 ≥ 透支下限；金币后续变动导致的付不起由成交时流拍兜底）；
    /// 成功后重置成交倒计时。玩家可同时参与多个物品的拍卖（各拍品状态独立互不干扰）。</summary>
    public bool PlaceBid(AuctionLot lot, PlayerSide side)
    {
        if (lot == null || !_lots.Contains(lot)) return false;
        if (!TradeActive) return false;   // 城邦过滤：非贸易城邦下拍卖行不生效（Tab 已隐藏，双保险）
        if (TurnManager.Instance == null || side != TurnManager.Instance.ActivePlayer)
        {
            Debug.Log($"[AuctionManager] 仅当前回合玩家可出价（{side} 不是行动方）");
            return false;
        }

        // 信誉门槛（贸易之城·二期）：信誉低于门槛（默认 6）无法在拍卖行出价；商店购买不受此限制
        if (TradeCityManager.Instance != null && !TradeCityManager.Instance.CanBidAuction(side))
        {
            Debug.Log($"[AuctionManager] {side} 信誉不足（当前 {TradeCityManager.Instance.GetReputation(side)}，门槛 6），无法出价");
            return false;
        }

        int amount = lot.MinNextBid;

        // 支付能力校验：当前金币 - 出价额 ≥ 透支下限
        if (GoldManager.Instance == null) return false;
        if (GoldManager.Instance.GetGold(side) - amount < GoldManager.Instance.OverdraftFloor)
        {
            Debug.Log($"[AuctionManager] {side} 出价 {amount} 超出支付能力（当前金币 {GoldManager.Instance.GetGold(side)}，透支下限 {GoldManager.Instance.OverdraftFloor}）");
            return false;
        }

        lot.CurrentBid = amount;
        lot.Bidder = side;
        lot.CountdownRemaining = Config.auctionDealCountdownTurns;
        Debug.Log($"[AuctionManager] {side} 对 {GetDisplayName(lot.Data)} 出价 {amount}（倒计时 {lot.CountdownRemaining} 回合）");
        OnAuctionChanged?.Invoke();
        return true;
    }

    // ==========================================
    //  回合结束结算（由 TurnManager.EndTurn 调用；每次任一方回合结束 = 1 个回合）
    // ==========================================
    public void OnTurnEnded()
    {
        if (!TradeActive) return;   // 城邦过滤：非贸易城邦下倒计时/成交/刷新整套不触发

        bool changed = false;

        // 倒计时递减 + 归零成交（仅有出价者的拍品；倒计时严格按回合递减）
        for (int i = _lots.Count - 1; i >= 0; i--)
        {
            var lot = _lots[i];
            if (!lot.HasBid) continue;
            lot.CountdownRemaining--;
            changed = true;
            if (lot.CountdownRemaining <= 0)
                SettleLot(lot);
        }

        // 刷新：每 N 回合补充 1 件新拍品（未售且未在拍）
        int interval = Config.auctionRefreshIntervalTurns;
        if (interval > 0 && ++_turnsSinceRefresh >= interval)
        {
            _turnsSinceRefresh = 0;
            changed |= AddNextFromPool();
        }

        if (changed) OnAuctionChanged?.Invoke();
    }

    /// <summary>成交结算：扣出价金额（可透支）→ 装备进买家背包 → 移出拍卖行并记入已售集合。
    /// 支付失败（超透支下限）则流拍移除：不计入已售集合（未来刷新可再现）。</summary>
    private void SettleLot(AuctionLot lot)
    {
        _lots.Remove(lot);
        var winner = lot.Bidder.Value;

        if (GoldManager.Instance == null || !GoldManager.Instance.TrySpendGoldWithOverdraft(winner, lot.CurrentBid))
        {
            Debug.LogWarning($"[AuctionManager] {winner} 无力支付 {GetDisplayName(lot.Data)} 的成交价 {lot.CurrentBid}（超透支下限），流拍移除");
            return;
        }

        // 一期只交付装备（道具交付方式二期设计；池准入已过滤，此处为双保险）
        if (lot.Data.equipment != null && EquipmentManager.Instance != null)
            EquipmentManager.Instance.GrantEquipment(winner, lot.Data.equipment);

        _sold.Add(lot.Data);
        Debug.Log($"[AuctionManager] {GetDisplayName(lot.Data)} 成交：{winner} 以 {lot.CurrentBid} 金币购得（已扣款，装备入背包）");
    }

    // ==========================================
    //  物品池补充
    // ==========================================
    /// <summary>从池中随机挑 1 件「未售、未在拍、指向装备」的物品加入拍卖行；无可用条目返回 false</summary>
    private bool AddNextFromPool()
    {
        if (pool == null || pool.Length == 0) return false;

        var candidates = new List<AuctionItemData>();
        foreach (var data in pool)
        {
            if (data == null) continue;
            if (_sold.Contains(data)) continue;              // 已售不重复
            if (HasActiveLot(data)) continue;                // 在拍不重复
            if (data.equipment == null)
            {
                Debug.LogWarning($"[AuctionManager] 拍卖物品 {data.name} 未配置装备引用（一期只拍卖装备），跳过");
                continue;
            }
            candidates.Add(data);
        }
        if (candidates.Count == 0) return false;

        var picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        _lots.Add(new AuctionLot(picked));
        Debug.Log($"[AuctionManager] 拍卖行补充新拍品：{GetDisplayName(picked)}（底价 {picked.startingPrice}，加价幅度 {picked.minIncrement}）");
        OnAuctionChanged?.Invoke();
        return true;
    }

    private bool HasActiveLot(AuctionItemData data)
    {
        foreach (var lot in _lots)
            if (lot.Data == data) return true;
        return false;
    }

    /// <summary>拍品显示名（装备名；未配置装备则回退 asset 名）</summary>
    internal static string GetDisplayName(AuctionItemData data)
        => data.equipment != null ? data.equipment.displayName : data.name;
}
