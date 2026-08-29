using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店面板 —— 挂在 Canvas 下的 UI GameObject 上。
/// 订阅 InputHandler.OnShopToggle（B 键）开启/关闭商店。
/// 装备分区：遍历 EquipmentManager.GetShopItems() 显示装备列表；购买按钮调用 EquipmentManager.BuyEquipment。
/// 道具分区：遍历 GridItemManager.GetShopGridItems() 显示棋盘道具列表；购买按钮调用 GridItemManager.BuyToInventory
///           （背包系统一期：买进背包，不立即进入瞄准；使用走 UI_BackpackSidebar 侧栏「使用」按钮）。
///
/// Editor 搭建：
///   1. Canvas 下创建 ShopPanel（含背景、标题、金币显示、Content 容器、关闭按钮）
///      —— 本脚本挂在 ShopPanel 的父级或同级常驻 GameObject 上，panelRoot 指向 ShopPanel。
///   2. 创建 ShopItem 预制体（挂 UI_ShopItemRefs，配齐 5 个引用）
///   3. Inspector 拖入：panelRoot、contentContainer、itemPrefab、goldText(可选)
/// </summary>
public class UI_ShopPanel : MonoBehaviour
{
    [Header("面板引用")]
    [SerializeField] private GameObject panelRoot;          // 商店面板根（显隐控制）
    [SerializeField] private Transform contentContainer;   // 列表项容器
    [SerializeField] private UI_ShopItemRefs itemPrefab;   // 列表项预制体
    [SerializeField] private TextMeshProUGUI goldText;     // 当前活动玩家金币显示（可选）

    [Header("Tab 分类")]
    [Tooltip("顶部 Tab 按钮，按顺序拖入 7 个：初级/中级/高级/城邦/道具/拍卖/地下交易（第 7 个仅当前活动玩家商行开启时显示；未加时地下交易 Tab 不可达）")]
    [SerializeField] private Button[] tabButtons;

    private ShopTab _currentTab = ShopTab.Basic;  // 当前选中 Tab（默认初级）

    private InputHandler _input;
    private readonly List<UI_ShopItemRefs> _items = new();

    private void OnEnable()
    {
        StartCoroutine(SubscribeNextFrame());
    }

    private System.Collections.IEnumerator SubscribeNextFrame()
    {
        yield return null;
        _input = FindFirstObjectByType<InputHandler>();
        if (_input != null)
            _input.OnShopToggle += Toggle;
        if (GoldManager.Instance != null && GoldManager.Instance.Model != null)
            GoldManager.Instance.Model.OnGoldSettled += OnGoldChanged;
        if (AuctionManager.Instance != null)
            AuctionManager.Instance.OnAuctionChanged += OnAuctionChanged;
        if (TradeCityManager.Instance != null)
            TradeCityManager.Instance.OnReputationChanged += OnReputationChanged;
        if (UndergroundTradeManager.Instance != null)
            UndergroundTradeManager.Instance.OnStateChanged += OnUndergroundStateChanged;

        // Tab 按钮：按下标绑定切换（顺序：初级/中级/高级/城邦/道具/拍卖/地下交易）
        if (tabButtons != null)
        {
            for (int i = 0; i < tabButtons.Length && i < 7; i++)
            {
                if (tabButtons[i] == null) continue;
                int idx = i;
                tabButtons[i].onClick.AddListener(() => SetTab((ShopTab)idx));
            }
        }
        UpdateTabHighlight();

        if (panelRoot != null) panelRoot.SetActive(false);  // 初始隐藏
        RefreshGold();
    }

    private void OnDisable()
    {
        if (_input != null)
            _input.OnShopToggle -= Toggle;
        if (GoldManager.Instance != null && GoldManager.Instance.Model != null)
            GoldManager.Instance.Model.OnGoldSettled -= OnGoldChanged;
        if (AuctionManager.Instance != null)
            AuctionManager.Instance.OnAuctionChanged -= OnAuctionChanged;
        if (TradeCityManager.Instance != null)
            TradeCityManager.Instance.OnReputationChanged -= OnReputationChanged;
        if (UndergroundTradeManager.Instance != null)
            UndergroundTradeManager.Instance.OnStateChanged -= OnUndergroundStateChanged;
        // 移除 Tab 按钮运行时监听（仅非持久化监听，防重复 OnEnable 叠加）
        if (tabButtons != null)
            foreach (var btn in tabButtons)
                if (btn != null) btn.onClick.RemoveAllListeners();
    }

    // ==========================================
    //  显隐 / 列表重建
    // ==========================================
    private void Toggle()
    {
        if (panelRoot == null) return;
        // 飓风关闭（季风三期B）：激活期间商店不可打开（已打开时仍可正常关闭）。
        // 判断内聚 MonsoonManager（非季风城邦/无飓风恒 false，商店行为不变）
        if (!panelRoot.activeSelf && MonsoonManager.Instance != null && MonsoonManager.Instance.IsShopClosedByHurricane)
        {
            Debug.Log("[UI_ShopPanel] 飓风期间商店关闭，无法打开");
            return;
        }
        bool show = !panelRoot.activeSelf;
        panelRoot.SetActive(show);
        if (show) InputHandler.RegisterPanelOpen(); else InputHandler.RegisterPanelClose();
        if (show) RebuildShop();
    }

    // ==========================================
    //  Tab 分类
    // ==========================================
    /// <summary>切换商店 Tab（由 tabButtons 按下标绑定）；拍卖 Tab 仅贸易城邦可进入，
    /// 地下交易 Tab 仅贸易城邦且当前活动玩家商行开启时可进入</summary>
    private void SetTab(ShopTab tab)
    {
        if (tab == ShopTab.Auction && !IsTradeCityActive()) return;
        if (tab == ShopTab.Underground && !IsUndergroundOpenForActive()) return;
        if (_currentTab == tab) return;
        _currentTab = tab;
        UpdateTabHighlight();
        if (panelRoot != null && panelRoot.activeSelf) RebuildShop();
    }

    /// <summary>本局是否贸易之城（拍卖/地下交易页签与信誉行的显示前提）</summary>
    private bool IsTradeCityActive()
        => CityStateManager.Instance != null && CityStateManager.Instance.IsActive(CityStateKind.Trade);

    /// <summary>当前活动玩家的地下交易商行是否开启（贸易城邦 + 商行开启；管理器缺失 = 未开启）</summary>
    private bool IsUndergroundOpenForActive()
    {
        if (!IsTradeCityActive()) return false;
        if (UndergroundTradeManager.Instance == null || TurnManager.Instance == null) return false;
        return UndergroundTradeManager.Instance.IsShopOpen(TurnManager.Instance.ActivePlayer);
    }

    /// <summary>选中 Tab 置灰不可点，其余恢复可点（高亮选中项）；
    /// 拍卖 Tab 按钮（下标 5）仅贸易城邦显示；地下交易 Tab 按钮（下标 6）仅贸易城邦且商行开启时显示</summary>
    private void UpdateTabHighlight()
    {
        if (tabButtons == null) return;
        bool tradeActive = IsTradeCityActive();
        bool undergroundVisible = tradeActive && IsUndergroundOpenForActive();
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            if (i == (int)ShopTab.Underground)
                tabButtons[i].gameObject.SetActive(undergroundVisible);
            else if (i == (int)ShopTab.Auction)
                tabButtons[i].gameObject.SetActive(tradeActive);
            else
                tabButtons[i].interactable = i != (int)_currentTab;
        }
    }

    /// <summary>城邦 Tab 动态展示：调用 CityStateManager 过滤查询 ——
    /// 归属 None → 任何城邦都显示（通用兜底）；归属 == 当前城邦 → 显示；归属其他城邦 → 不显示。
    /// 管理器不在场景时回退为全部显示（保持改造前行为，避免城邦页整个变空）。</summary>
    private bool ShouldShowCityState(EquipmentData data)
    {
        var manager = CityStateManager.Instance;
        if (manager == null) return true;
        return manager.ShouldShowEquipment(data);
    }

    private void RebuildShop()
    {
        // 清理旧项
        foreach (var item in _items)
            if (item != null) Destroy(item.gameObject);
        _items.Clear();

        if (itemPrefab == null || contentContainer == null) return;

        // ---- 装备分区（4 个装备 Tab：按 tier 过滤；道具/拍卖/地下交易 Tab 跳过）----
        if (_currentTab != ShopTab.GridItem && _currentTab != ShopTab.Auction && _currentTab != ShopTab.Underground && EquipmentManager.Instance != null)
        {
            var shopItems = EquipmentManager.Instance.GetShopItems();
            if (shopItems != null)
            {
                foreach (var data in shopItems)
                {
                    if (data == null) continue;
                    if (data.tier != (EquipmentTier)_currentTab) continue;                        // Tab ↔ tier 过滤
                    if (_currentTab == ShopTab.CityState && !ShouldShowCityState(data)) continue;  // 城邦动态展示扩展点
                    var item = Instantiate(itemPrefab, contentContainer);
                    if (item.nameText != null) item.nameText.text = data.displayName;
                    if (item.priceText != null) item.priceText.text = BuildPriceText(data.price);
                    if (item.descText != null) item.descText.text = data.description;
                    if (item.statsText != null) item.statsText.text = BuildStatsText(data);
                    if (item.buttonText != null) item.buttonText.text = "购买";
                    if (item.iconImage != null)
                    {
                        item.iconImage.sprite = data.icon;
                        item.iconImage.gameObject.SetActive(data.icon != null);
                    }
                    var captured = data;
                    if (item.buyButton != null)
                    {
                        item.buyButton.interactable = true;
                        item.buyButton.onClick.AddListener(() => OnBuy(captured));
                    }
                    _items.Add(item);
                }
            }
        }

        // ---- 道具分区（仅道具 Tab）----
        if (_currentTab == ShopTab.GridItem && GridItemManager.Instance != null)
        {
            var gridItems = GridItemManager.Instance.GetShopGridItems();
            if (gridItems != null)
            {
                foreach (var data in gridItems)
                {
                    if (data == null) continue;
                    var item = Instantiate(itemPrefab, contentContainer);
                    if (item.nameText != null) item.nameText.text = $"[道具] {data.displayName}";
                    if (item.priceText != null) item.priceText.text = BuildPriceText(data.price);
                    if (item.descText != null) item.descText.text = BuildGridItemDesc(data);
                    if (item.statsText != null) item.statsText.text = $"消耗 {data.apCost} AP";
                    if (item.iconImage != null)
                        item.iconImage.gameObject.SetActive(false);
                    var captured = data;
                    // 背包系统一期：购买进背包（无需选中棋子；requiresSelectedPiece 的校验移到侧栏「使用」时）
                    if (item.buyButton != null)
                    {
                        item.buyButton.interactable = true;
                        item.buyButton.onClick.AddListener(() => OnBuyGridItem(captured));
                    }
                    if (item.buttonText != null)
                        item.buttonText.text = "购买";
                    _items.Add(item);
                }
            }
        }

        // ---- 拍卖分区（仅拍卖 Tab；贸易之城·一期）----
        if (_currentTab == ShopTab.Auction && AuctionManager.Instance != null)
        {
            var lots = AuctionManager.Instance.ActiveLots;
            foreach (var lot in lots)
            {
                var data = lot.Data;
                var item = Instantiate(itemPrefab, contentContainer);
                string bidderName = lot.Bidder == PlayerSide.P1 ? "玩家1" : "玩家2";
                if (item.nameText != null) item.nameText.text = $"[拍卖] {AuctionManager.GetDisplayName(data)}";
                if (item.priceText != null)
                    item.priceText.text = lot.HasBid ? $"当前价 {lot.CurrentBid} 金币" : $"底价 {data.startingPrice} 金币";
                if (item.descText != null && data.equipment != null)
                    item.descText.text = data.equipment.description;
                if (item.statsText != null)
                    item.statsText.text = lot.HasBid
                        ? $"倒计时 {lot.CountdownRemaining} 回合 · 出价者 {bidderName}"
                        : "等待出价";
                if (item.buttonText != null)
                    item.buttonText.text = $"出价 {lot.MinNextBid} 金币";
                if (item.iconImage != null)
                {
                    item.iconImage.sprite = data.equipment != null ? data.equipment.icon : null;
                    item.iconImage.gameObject.SetActive(item.iconImage.sprite != null);
                }
                var captured = lot;
                // 信誉门槛（贸易之城·二期）：当前活动玩家信誉 < 6 时无法出价（拍卖底价/成交价不受涨价影响）
                bool canBid = TradeCityManager.Instance == null
                    || TurnManager.Instance == null
                    || TradeCityManager.Instance.CanBidAuction(TurnManager.Instance.ActivePlayer);
                if (item.buttonText != null)
                    item.buttonText.text = canBid ? $"出价 {lot.MinNextBid} 金币" : "信誉不足（<6）";
                if (item.buyButton != null)
                {
                    item.buyButton.interactable = canBid;
                    if (canBid)
                        item.buyButton.onClick.AddListener(() => OnBid(captured));
                }
                _items.Add(item);
            }
        }

        // ---- 地下交易分区（仅地下交易 Tab；贸易之城·三期。当前活动玩家商行开启才可见该 Tab）----
        if (_currentTab == ShopTab.Underground && UndergroundTradeManager.Instance != null && IsUndergroundOpenForActive())
        {
            var side = TurnManager.Instance.ActivePlayer;
            foreach (var data in UndergroundTradeManager.Instance.Pool)
            {
                if (data == null || data.equipment == null) continue;
                var item = Instantiate(itemPrefab, contentContainer);
                if (item.nameText != null) item.nameText.text = $"[黑市] {data.equipment.displayName}";
                if (item.priceText != null)
                    item.priceText.text = data.settlement == UndergroundSettlement.LowPrice
                        ? $"低价 {data.amount} 金币"
                        : $"赊账（欠债 +{data.amount}）";
                if (item.descText != null) item.descText.text = data.equipment.description;
                if (item.statsText != null)
                    item.statsText.text = BuildUndergroundCostText(data.equipment);
                if (item.buttonText != null)
                    item.buttonText.text = UndergroundTradeManager.Instance.IsPurchased(side, data) ? "已购" : "购买";
                if (item.iconImage != null)
                {
                    item.iconImage.sprite = data.equipment.icon;
                    item.iconImage.gameObject.SetActive(item.iconImage.sprite != null);
                }
                var captured = data;
                bool purchased = UndergroundTradeManager.Instance.IsPurchased(side, data);
                if (item.buyButton != null)
                {
                    item.buyButton.interactable = !purchased;
                    if (!purchased)
                        item.buyButton.onClick.AddListener(() => OnBuyUnderground(captured));
                }
                _items.Add(item);
            }
        }

        RefreshGold();
    }

    /// <summary>购买地下交易商行物品（结算与限购校验在 UndergroundTradeManager）</summary>
    private void OnBuyUnderground(UndergroundItemData data)
    {
        if (UndergroundTradeManager.Instance == null || TurnManager.Instance == null) return;
        UndergroundTradeManager.Instance.BuyItem(TurnManager.Instance.ActivePlayer, data);
        RebuildShop();  // 刷新已购置灰与金币显示
    }

    /// <summary>解析装备 passives 中的代价型被动，生成使用代价文案（单一事实来源 = 装备 asset 的 jsonParams）</summary>
    internal static string BuildUndergroundCostText(EquipmentData equipment)
    {
        var parts = new List<string>();
        if (equipment.passives != null)
        {
            foreach (var cfg in equipment.passives)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.className)) continue;
                string json = string.IsNullOrEmpty(cfg.jsonParams) ? "{}" : cfg.jsonParams;
                if (cfg.className == "BloodCostPassive")
                {
                    var p = JsonUtility.FromJson<BloodCostDisplayParams>(json);
                    parts.Add($"每回合扣血 {p.hpPerTurn}");
                }
                else if (cfg.className == "APCostPassive")
                {
                    var p = JsonUtility.FromJson<APCostDisplayParams>(json);
                    parts.Add($"自己回合扣 AP {p.apPerTurn}");
                }
            }
        }
        return parts.Count > 0 ? $"代价：{string.Join(" · ", parts)}" : "无代价";
    }

    [System.Serializable]
    private class BloodCostDisplayParams { public int hpPerTurn = 2; }

    [System.Serializable]
    private class APCostDisplayParams { public int apPerTurn = 1; }

    /// <summary>当前活动玩家的商店涨价后售价（信誉 10 / 管理器缺失时 = 原价）</summary>
    private int GetAdjustedPriceForActive(int basePrice)
    {
        if (TradeCityManager.Instance == null || TurnManager.Instance == null) return basePrice;
        return TradeCityManager.Instance.GetAdjustedPrice(TurnManager.Instance.ActivePlayer, basePrice);
    }

    /// <summary>商店价格展示文案：涨价时标注涨幅百分比（信誉 10 = 原价无标注）</summary>
    private string BuildPriceText(int basePrice)
    {
        int adjusted = GetAdjustedPriceForActive(basePrice);
        if (adjusted == basePrice) return $"{basePrice} 金币";
        int percent = TradeCityManager.Instance != null && TurnManager.Instance != null
            ? TradeCityManager.Instance.GetPricePercent(TurnManager.Instance.ActivePlayer)
            : 0;
        return $"{adjusted} 金币（信誉涨价 +{percent}%）";
    }

    private void OnBuy(EquipmentData data)
    {
        if (EquipmentManager.Instance == null || TurnManager.Instance == null) return;
        EquipmentManager.Instance.BuyEquipment(TurnManager.Instance.ActivePlayer, data);
        RebuildShop();  // 刷新金币显示
    }

    /// <summary>购买棋盘道具（背包系统一期：买进背包，不进入瞄准；使用走背包侧栏「使用」按钮）。
    /// 购买后商店保持打开（可继续购买），道具持有由侧栏显示</summary>
    private void OnBuyGridItem(GridItemData data)
    {
        if (GridItemManager.Instance == null || TurnManager.Instance == null) return;
        GridItemManager.Instance.BuyToInventory(TurnManager.Instance.ActivePlayer, data);
        RebuildShop();  // 刷新金币显示（成功入背包/金币不足都刷新）
    }

    /// <summary>拍卖出价（当前回合玩家，一键出最低可行价；支付能力校验在 AuctionManager）</summary>
    private void OnBid(AuctionLot lot)
    {
        if (AuctionManager.Instance == null || TurnManager.Instance == null) return;
        AuctionManager.Instance.PlaceBid(lot, TurnManager.Instance.ActivePlayer);
        RebuildShop();  // 刷新当前价/倒计时/出价者显示
    }

    /// <summary>拍卖行状态变化（出价/成交/刷新）→ 拍卖 Tab 打开时刷新列表（金币显示由 OnGoldSettled 单独驱动）</summary>
    private void OnAuctionChanged()
    {
        if (panelRoot != null && panelRoot.activeSelf && _currentTab == ShopTab.Auction)
            RebuildShop();
    }

    /// <summary>信誉变化（扣信誉/恢复）→ 商店打开时刷新（价格/拍卖门槛/信誉行都可能变化）</summary>
    private void OnReputationChanged(PlayerSide side, int reputation)
    {
        if (panelRoot != null && panelRoot.activeSelf)
            RebuildShop();
    }

    /// <summary>地下交易状态变化（激活/开商行/关闭/购买）→ 刷新 Tab 可见性；
    /// 商行关闭时若正浏览地下交易 Tab 自动跳回初级</summary>
    private void OnUndergroundStateChanged(PlayerSide side)
    {
        bool open = IsUndergroundOpenForActive();
        if (!open && _currentTab == ShopTab.Underground)
            _currentTab = ShopTab.Basic;

        UpdateTabHighlight();
        if (panelRoot != null && panelRoot.activeSelf)
            RebuildShop();
    }

    // ==========================================
    //  辅助
    // ==========================================
    internal static string BuildStatsText(EquipmentData d)
    {
        var parts = new List<string>();
        if (d.bonusHP != 0) parts.Add($"HP+{d.bonusHP}");
        if (d.bonusAttack != 0) parts.Add($"攻击+{d.bonusAttack}");
        if (d.bonusDefense != 0) parts.Add($"防御+{d.bonusDefense}");
        if (d.bonusMoveRange != 0) parts.Add($"移动+{d.bonusMoveRange}");
        if (d.bonusAttackRange != 0) parts.Add($"射程+{d.bonusAttackRange}");
        if (d.bonusAPCap != 0) parts.Add($"AP上限+{d.bonusAPCap}");
        if (d.bonusEnergyPerTurn != 0) parts.Add($"回蓝+{d.bonusEnergyPerTurn}/回合");
        return parts.Count > 0 ? string.Join("  ", parts) : "无加成";
    }

    /// <summary>棋盘道具效果说明（按 effectClassName 生成中文描述）</summary>
    internal static string BuildGridItemDesc(GridItemData d)
    {
        switch (d.effectClassName)
        {
            case "ExpandTileEffect": return "在棋盘外邻接位置新增一个格子（点击蓝色幽灵格选择落点）";
            case "RemoveTileEffect": return $"移除一个无棋子的格子（棋盘至少保留 {d.minTilesAfter} 格）";
            case "TeleportUnitEffect": return "将选中棋子传送到范围内空格";
            default: return d.effectClassName;
        }
    }

    private void OnGoldChanged(PlayerSide side, int gold) => RefreshGold();

    private void RefreshGold()
    {
        if (goldText == null || TurnManager.Instance == null || GoldManager.Instance == null) return;
        var side = TurnManager.Instance.ActivePlayer;
        // 信誉行（贸易之城·二期）：仅贸易城邦下展示当前活动玩家信誉，让玩家感知涨价/禁拍来源；
        // 非贸易城邦信誉机制整体不生效，不展示
        if (TradeCityManager.Instance != null && IsTradeCityActive())
            goldText.text = $"金币: {GoldManager.Instance.GetGold(side)} · 信誉: {TradeCityManager.Instance.GetReputation(side)}/10";
        else
            goldText.text = $"金币: {GoldManager.Instance.GetGold(side)}";
    }
}
