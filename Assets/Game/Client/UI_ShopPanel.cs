using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店面板 —— 挂在 Canvas 下的 UI GameObject 上。
/// 订阅 InputHandler.OnShopToggle（B 键）开启/关闭商店。
/// 装备分区：遍历 EquipmentManager.GetShopItems() 显示装备列表；购买按钮调用 EquipmentManager.BuyEquipment。
/// 道具分区：遍历 GridItemManager.GetShopGridItems() 显示棋盘道具列表；购买按钮调用 GridItemManager.BuyAndUse
///           （购买即用：成功后关闭商店进入瞄准模式）。传送石（requiresSelectedPiece）按钮在未选中己方棋子时置灰。
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
    [Tooltip("顶部 Tab 按钮，按顺序拖入 5 个：初级/中级/高级/城邦/道具")]
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

        // Tab 按钮：按下标绑定切换（顺序：初级/中级/高级/城邦/道具）
        if (tabButtons != null)
        {
            for (int i = 0; i < tabButtons.Length && i < 5; i++)
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
        bool show = !panelRoot.activeSelf;
        panelRoot.SetActive(show);
        if (show) InputHandler.RegisterPanelOpen(); else InputHandler.RegisterPanelClose();
        if (show) RebuildShop();
    }

    // ==========================================
    //  Tab 分类
    // ==========================================
    /// <summary>切换商店 Tab（由 tabButtons 按下标绑定）</summary>
    private void SetTab(ShopTab tab)
    {
        if (_currentTab == tab) return;
        _currentTab = tab;
        UpdateTabHighlight();
        if (panelRoot != null && panelRoot.activeSelf) RebuildShop();
    }

    /// <summary>选中 Tab 置灰不可点，其余恢复可点（高亮选中项）</summary>
    private void UpdateTabHighlight()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
            if (tabButtons[i] != null)
                tabButtons[i].interactable = i != (int)_currentTab;
    }

    /// <summary>城邦 Tab 动态展示扩展点：未来改为按本局城邦效果过滤（当前全部显示）</summary>
    private bool ShouldShowCityState(EquipmentData data)
    {
        return true;
    }

    private void RebuildShop()
    {
        // 清理旧项
        foreach (var item in _items)
            if (item != null) Destroy(item.gameObject);
        _items.Clear();

        if (itemPrefab == null || contentContainer == null) return;

        // ---- 装备分区（4 个装备 Tab：按 tier 过滤；道具 Tab 跳过）----
        if (_currentTab != ShopTab.GridItem && EquipmentManager.Instance != null)
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
                    if (item.priceText != null) item.priceText.text = $"{data.price} 金币";
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
                    if (item.priceText != null) item.priceText.text = $"{data.price} 金币";
                    if (item.descText != null) item.descText.text = BuildGridItemDesc(data);
                    if (item.statsText != null) item.statsText.text = $"消耗 {data.apCost} AP";
                    if (item.iconImage != null)
                        item.iconImage.gameObject.SetActive(false);
                    var captured = data;
                    // 传送石（requiresSelectedPiece）按钮置灰逻辑：未选中己方棋子时不可点击
                    bool canBuy = !data.requiresSelectedPiece || IsOwnPieceSelected();
                    if (item.buyButton != null)
                    {
                        item.buyButton.interactable = canBuy;
                        item.buyButton.onClick.AddListener(() => OnBuyGridItem(captured));
                    }
                    if (item.buttonText != null)
                        item.buttonText.text = canBuy ? "购买" : "请先选中己方棋子";
                    _items.Add(item);
                }
            }
        }

        RefreshGold();
    }

    private void OnBuy(EquipmentData data)
    {
        if (EquipmentManager.Instance == null || TurnManager.Instance == null) return;
        EquipmentManager.Instance.BuyEquipment(TurnManager.Instance.ActivePlayer, data);
        RebuildShop();  // 刷新金币显示
    }

    /// <summary>购买棋盘道具（购买即用）：成功后关闭商店进入瞄准模式；失败则刷新列表。</summary>
    private void OnBuyGridItem(GridItemData data)
    {
        if (GridItemManager.Instance == null || TurnManager.Instance == null) return;
        bool ok = GridItemManager.Instance.BuyAndUse(TurnManager.Instance.ActivePlayer, data);
        if (ok)
        {
            // 购买成功 → 关闭商店，让玩家在棋盘上瞄准
            if (panelRoot != null) panelRoot.SetActive(false);
            InputHandler.RegisterPanelClose();
        }
        else
        {
            RebuildShop();  // 金币不足/无目标等 → 刷新金币显示与按钮状态
        }
    }

    /// <summary>当前活动玩家是否选中了己方棋子（传送石按钮可用性判定）</summary>
    private bool IsOwnPieceSelected()
    {
        var sel = BattleController.Instance?.Model?.SelectedPiece;
        return sel != null && sel.Owner == TurnManager.Instance.ActivePlayer;
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
        goldText.text = $"金币: {GoldManager.Instance.GetGold(TurnManager.Instance.ActivePlayer)}";
    }
}
