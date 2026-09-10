using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城邦选择面板（战前流程最前段）—— 双方各选 floor(城邦总数/2)+1 个心仪城邦，
/// 双方都提交后由 CityStateManager「求交集 + 随机」定出本局城邦并揭晓。
///
/// 传输无关原则（联机预留）：本面板只做三件事——展示可选城邦、采集「当前指定一方」的选择、
/// 调 GameFlowController 的提交接口。不假设「对方在同屏」「先后顺序」「对方可见我的选择」；
/// 「谁在选择」由战前流程控制器（本地热座驱动）指定，将来联机换成各自屏幕 + 网络同步，
/// 提交/结算逻辑零改动。
///
/// Editor 搭建（对标 UI_UnitSelection 惯例）：
///   - 复用 ShopItem 预制体（UI_ShopItemRefs）作为城邦卡片；
///   - contentContainer 挂 GridLayoutGroup：Constraint = Fixed Column Count = 3
///     （每行 3 张、纵向铺开，当前 6 城邦 = 3 列 × 2 行；将来增减城邦自动重排，代码无感知）；
///   - panelRoot / contentContainer / itemPrefab / titleText / countText / confirmButton
///     / revealRoot / revealText / revealConfirmButton 由 Inspector 拖入。
/// </summary>
public class UI_CityStateSelection : MonoBehaviour
{
    [Header("面板引用")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform contentContainer;
    [SerializeField] private UI_ShopItemRefs itemPrefab;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Button confirmButton;

    [Header("揭晓视图（双方提交后显示）")]
    [SerializeField] private GameObject revealRoot;
    [SerializeField] private TextMeshProUGUI revealText;
    [SerializeField] private Button revealConfirmButton;

    private readonly HashSet<CityStateKind> _selected = new();
    private readonly List<UI_ShopItemRefs> _cards = new();
    private readonly Dictionary<CityStateKind, UI_ShopItemRefs> _cardMap = new();
    private PlayerSide _currentSide;

    /// <summary>每位玩家可选数量 = floor(城邦总数/2)+1（当前 6 城邦 = 4），由城邦总数动态计算，不写死</summary>
    public static int PicksRequired => CityStateManager.AllCityStates.Length / 2 + 1;

    private void Start()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirm);
            confirmButton.interactable = false;
        }
        if (revealConfirmButton != null)
            revealConfirmButton.onClick.AddListener(OnRevealConfirmed);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>开始某方的选择（本地热座驱动：流程控制器指定当前选择方；逻辑层不依赖顺序）</summary>
    public void Show(PlayerSide side)
    {
        _currentSide = side;
        _selected.Clear();
        if (titleText != null)
            titleText.text = $"{(side == PlayerSide.P1 ? "玩家1" : "玩家2")} · 选择 {PicksRequired} 个心仪城邦";
        if (revealRoot != null) revealRoot.SetActive(false);
        RebuildCards();
        // 面板开闭登记只在「关闭 → 打开」时发生一次；已打开时的内容切换（P1 切 P2）不重复登记
        bool wasOpen = panelRoot != null && panelRoot.activeSelf;
        if (panelRoot != null) panelRoot.SetActive(true);
        if (!wasOpen) InputHandler.RegisterPanelOpen();
        UpdateCount();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        // 揭晓遮罩与面板一起隐藏（揭晓后残留会全屏挡住后续界面）
        if (revealRoot != null) revealRoot.SetActive(false);
        InputHandler.RegisterPanelClose();
    }

    // ==========================================
    //  卡片构建与多选交互
    // ==========================================
    private void RebuildCards()
    {
        foreach (var card in _cards)
            if (card != null) Destroy(card.gameObject);
        _cards.Clear();
        _cardMap.Clear();
        if (itemPrefab == null || contentContainer == null) return;

        foreach (var kind in CityStateManager.AllCityStates)
        {
            var card = Instantiate(itemPrefab, contentContainer);
            if (card.nameText != null) card.nameText.text = GetDisplayName(kind);
            if (card.priceText != null) card.priceText.text = string.Empty;
            if (card.descText != null) card.descText.text = GetDescription(kind);
            if (card.statsText != null) card.statsText.text = string.Empty;
            if (card.buttonText != null) card.buttonText.text = "选择";
            var captured = kind;
            var capturedCard = card;
            if (card.buyButton != null)
                card.buyButton.onClick.AddListener(() => OnToggleCard(captured, capturedCard));
            _cards.Add(card);
            _cardMap[kind] = card;
        }
    }

    /// <summary>点击卡片切换选中/取消（多选，选满 PicksRequired 后不可再加选，只能先取消）</summary>
    private void OnToggleCard(CityStateKind kind, UI_ShopItemRefs card)
    {
        if (_selected.Contains(kind))
        {
            _selected.Remove(kind);
            if (card.buttonText != null) card.buttonText.text = "选择";
        }
        else
        {
            if (_selected.Count >= PicksRequired) return;
            _selected.Add(kind);
            if (card.buttonText != null) card.buttonText.text = "已选 ✓";
        }
        UpdateCount();
    }

    private void UpdateCount()
    {
        if (countText != null) countText.text = $"已选 {_selected.Count}/{PicksRequired}";
        if (confirmButton != null) confirmButton.interactable = (_selected.Count == PicksRequired);
    }

    // ==========================================
    //  提交（只调流程控制器的提交接口；结算全在后端管线）
    // ==========================================
    private void OnConfirm()
    {
        if (_selected.Count != PicksRequired) return;
        var choices = new List<CityStateKind>(_selected);
        GameFlowController.Instance?.OnCityStateSelectionSubmitted(_currentSide, choices);
    }

    // ==========================================
    //  揭晓视图（双方都已提交；本局城邦已由后端写入状态）
    // ==========================================
    /// <summary>显示本局城邦揭晓（kind == None 表示双方心仪无重合）</summary>
    public void ShowResult(CityStateKind kind)
    {
        if (revealRoot != null) revealRoot.SetActive(true);
        if (revealText != null)
            revealText.text = kind == CityStateKind.None
                ? "双方心仪城邦无重合\n本局无城邦"
                : $"本局城邦\n「{GetDisplayName(kind)}」";
        if (confirmButton != null) confirmButton.interactable = false;
        Debug.Log($"[UI_CityStateSelection] 城邦揭晓：{kind}");
    }

    private void OnRevealConfirmed()
    {
        GameFlowController.Instance?.OnCityStateRevealConfirmed();
    }

    // ==========================================
    //  城邦文案（View 层静态表；与枚举一一对应）
    // ==========================================
    internal static string GetDisplayName(CityStateKind kind) => kind switch
    {
        CityStateKind.Trade => "贸易之城",
        CityStateKind.Merriment => "欢愉之都",
        CityStateKind.Monsoon => "季风城邦",
        CityStateKind.Occult => "邪疑之城",
        CityStateKind.War => "战争之城",
        CityStateKind.Element => "元素之城",
        _ => "无城邦"
    };

    internal static string GetDescription(CityStateKind kind) => kind switch
    {
        CityStateKind.Trade => "拍卖行 · 透支 · 利息与信誉 · 地下交易",
        CityStateKind.Merriment => "欢愉城邦（机制待接入）",
        CityStateKind.Monsoon => "季风城邦（机制待接入）",
        CityStateKind.Occult => "邪疑城邦（机制待接入）",
        CityStateKind.War => "战争城邦（机制待接入）",
        CityStateKind.Element => "元素附着 · 六系元素反应 · 护盾染色",
        _ => string.Empty
    };
}
