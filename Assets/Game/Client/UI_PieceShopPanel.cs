using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 棋子商店（大厅，金币 sink）—— 列出全部棋子：已拥有置灰显示「已拥有」，未拥有显示价格 + 购买按钮。
/// 购买校验在 PlayerProfileService.TryBuyPiece（金币足够 → 扣款+解锁+即时写盘；不足 → 拒绝并提示，不扣款）。
/// 购买 / 结算等金币变动经 OnProfileChanged 驱动本面板与大厅解锁状态自动刷新（购买后棋子数选项立即更新）。
/// 复用现有 UI_ShopItemRefs 预制体（与局内装备商店同构；棋子无图标 → 隐藏 iconImage，无空引用）。
///
/// Editor 搭建：挂在 PieceShopPanel 根节点上；
///   panelRoot / contentContainer / itemPrefab（现有 ShopItem 预制体）/ goldText / messageText / closeButton 由 Inspector 拖入。
/// </summary>
public class UI_PieceShopPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform contentContainer;
    [SerializeField] private UI_ShopItemRefs itemPrefab;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button closeButton;

    private readonly List<UI_ShopItemRefs> _items = new();

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        PlayerProfileService.OnProfileChanged += OnProfileChanged;
    }

    private void OnDestroy()
    {
        PlayerProfileService.OnProfileChanged -= OnProfileChanged;
    }

    public void Show()
    {
        SetMessage(string.Empty);
        RebuildShop();
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>档案变化（金币 / 拥有棋子变动）→ 打开时刷新金币与列表（购买后「已拥有」状态立即更新）</summary>
    private void OnProfileChanged()
    {
        RefreshGold();
        if (panelRoot != null && panelRoot.activeSelf) RebuildShop();
    }

    private void RebuildShop()
    {
        foreach (var item in _items)
            if (item != null) Destroy(item.gameObject);
        _items.Clear();
        RefreshGold();
        if (itemPrefab == null || contentContainer == null) return;

        var registry = PlayerProfileService.ResolveRegistry();
        if (registry == null || registry.pieces == null)
        {
            SetMessage("未配置棋子注册表（请在大厅 UI_MainMenu 拖入或在 PlayerProfileConfig 中指定）");
            return;
        }

        int price = PlayerProfileService.Config.piecePrice;
        foreach (var data in registry.pieces)
        {
            if (data == null || data.isSummon) continue; // 召唤物不可购买（与选人池口径一致）
            bool owned = PlayerProfileService.IsOwned(data.id);

            var item = Instantiate(itemPrefab, contentContainer);
            if (item.nameText != null) item.nameText.text = data.displayName;
            if (item.priceText != null)
                item.priceText.text = owned ? $"HP{data.maxHP} 攻{data.attack} 防{data.defense}" : $"{price} 金币";
            if (item.descText != null)
            {
                string elemText = ElementReactionTable.ElementSystemActive ? $" 元素{data.innateElement}" : string.Empty;
                item.descText.text = $"移{data.moveConfig.baseRange} 射{data.attackConfig.baseRange}{elemText}";
            }
            if (item.statsText != null)
                item.statsText.text = data.ultimateConfig != null ? $"大招:{data.ultimateConfig.ultimateName}" : "无大招";
            if (item.iconImage != null) item.iconImage.gameObject.SetActive(false); // 棋子无图标位（预留换皮拖 Sprite）

            var captured = data;
            if (item.buyButton != null)
            {
                item.buyButton.interactable = !owned;
                if (item.buttonText != null) item.buttonText.text = owned ? "已拥有" : "购买";
                if (!owned) item.buyButton.onClick.AddListener(() => OnBuy(captured));
            }
            _items.Add(item);
        }
    }

    private void OnBuy(PieceData data)
    {
        if (PlayerProfileService.TryBuyPiece(data.id, out string failReason))
        {
            SetMessage($"已购买 {data.displayName}");
        }
        else
        {
            SetMessage(failReason); // 金币不足等：拒绝且不扣款
        }
        RebuildShop(); // 立即刷新（成功→已拥有置灰；失败→金币显示同步）
    }

    private void RefreshGold()
    {
        if (goldText != null)
            goldText.text = PlayerProfileService.IsLoggedIn
                ? $"金币：{PlayerProfileService.Current.gold}"
                : "金币：--";
    }

    private void SetMessage(string text)
    {
        if (messageText != null) messageText.text = text;
    }
}
