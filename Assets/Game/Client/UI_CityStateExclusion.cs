using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大厅城邦排除面板 —— 「开始游戏」之前的配置步骤（由局内 UI_CityStateSelection 迁移而来）。
///
/// 交互：P1 → P2 轮流（热座同机；联机时每台设备只有本地一方），各划掉（排除）2 个不想要的城邦；
/// 恰好排除 2 个才可确认。提交时在 UI/流程层做「排除 → 心仪」转换：心仪 = 全部城邦 − 已排除
/// （数学等价，6 中选 4 心仪 ⇔ 6 中排除 2），CityStateModel.Resolve 算法原样复用，不修改。
///
/// 结算与揭晓：**不在提交时结算**。双方都提交后仅解锁「开始游戏」（事件 OnBothSubmitted）；
/// 「开始游戏」点击后由 UI_MainMenu 调 CityStateManager.ResolvePendingSelections 结算（单一随机入口），
/// 再调本组件的 <see cref="ShowReveal"/> 弹提示框 → 自动关闭 → 载入局内（无城邦模式不弹，直接进局内）。
///
/// 联机预留（接入网络时 UI 零改动）：
///   ① 提交走 <see cref="CityStateManager.SubmitSelection"/>（单一本地提交入口）；
///   ② 双方提交状态查询 <see cref="CityStateManager.HasBothSubmitted"/>（本地实现；联机由网络层实现）；
///   ③ 本组件不持有 _p1Submitted/_p2Submitted 之类本地布尔，不持有对局状态。
///
/// Editor 搭建（沿用大厅既有手搭方式；复用 ShopItem 预制体作城邦卡片，**不改预制体**）：
///   - itemPrefab = ShopItem（UI_ShopItemRefs）：iconImage = 图像框（无素材时占位色块，换皮拖 Sprite），
///     nameText = 城邦名；价格/描述/按钮等商店元素运行时隐藏（卡片 = 图像 + 名，保持干净）；
///   - contentContainer 挂 GridLayoutGroup：Fixed Column Count = 3（3列×2行，6 城邦同屏）；
///   - tooltip = UI_ItemTooltip 实例（Editor 把 Tooltip.prefab 拖入场景并拖到本字段；悬停显示城邦描述）。
/// </summary>
public class UI_CityStateExclusion : MonoBehaviour
{
    [Header("面板引用（Inspector 拖入）")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform contentContainer;
    [SerializeField] private UI_ShopItemRefs itemPrefab;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Button confirmButton;

    [Header("揭晓视图（「开始游戏」点击后显示，自动关闭后载入局内）")]
    [SerializeField] private GameObject revealRoot;
    [SerializeField] private TextMeshProUGUI revealText;
    [Tooltip("揭晓提示框显示时长（秒）；[PLACEHOLDER] 实机确认快慢后调整")]
    [SerializeField] private float revealDuration = 2f;

    [Header("描述悬浮提示（复用 UI_ItemTooltip；Editor 把 Tooltip.prefab 实例拖入）")]
    [SerializeField] private UI_ItemTooltip tooltip;

    /// <summary>双方都已完成禁选（提交状态查询自 CityStateManager，本组件不持有本地布尔）。
    /// UI_MainMenu 订阅以解锁「开始游戏」。</summary>
    public event System.Action OnBothSubmitted;

    private readonly List<UI_ShopItemRefs> _cards = new();
    private readonly Dictionary<CityStateKind, UI_ShopItemRefs> _cardMap = new();

    /// <summary>当前方本次的禁选（UI 本地收集的**本地玩家**选择；切方清空 = 热座私密性）</summary>
    private readonly HashSet<CityStateKind> _excluded = new();

    /// <summary>当前选择方（本地热座：P1 先选，提交后切 P2；联机时固定为本地玩家侧，不切换）</summary>
    private PlayerSide _currentSide = PlayerSide.P1;

    private Coroutine _revealRoutine;

    /// <summary>每方需排除数量 = 城邦总数 − 心仪数（当前 6 − 4 = 2；动态计算不写死）</summary>
    public static int ExcludesRequired => CityStateManager.AllCityStates.Length - (CityStateManager.AllCityStates.Length / 2 + 1);

    // ---- 禁选态视觉（与未禁选形成强对比：白卡 → 暗红卡 + 灰字 + 图标压暗）----
    private static readonly Color ExcludedCardColor = new Color(0.42f, 0.2f, 0.22f, 0.95f);
    private static readonly Color ExcludedTextColor = new Color(0.55f, 0.55f, 0.6f, 1f);
    private static readonly Color ExcludedIconColor = new Color(0.32f, 0.28f, 0.3f, 1f);

    private void Start()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        ResetInternalState();
        // 面板显隐与流程由 UI_MainMenu 控制（Begin / Hide），本组件不自行弹出
    }

    // ==========================================
    //  流程入口（UI_MainMenu 调用）
    // ==========================================
    /// <summary>开始新一轮城邦排除（大厅切到「随机城邦」时调用）：重置为 P1 先选、清掉大厅管理器里
    /// 上一轮的双方选择（联机预留：改为向网络层发起新对局/清空同步状态），显示面板。</summary>
    public void Begin()
    {
        CityStateManager.Instance?.ResetForNewGame();
        ResetInternalState();
        if (panelRoot != null) panelRoot.SetActive(true);
        if (revealRoot != null) revealRoot.SetActive(false);
        if (titleText != null) titleText.text = $"玩家1 · 排除 {ExcludesRequired} 个不想要的城邦";
        RebuildCards();
        UpdateCount();
    }

    /// <summary>隐藏面板（大厅切到「无城邦」时调用）</summary>
    public void Hide()
    {
        if (_revealRoutine != null) { StopCoroutine(_revealRoutine); _revealRoutine = null; }
        if (panelRoot != null) panelRoot.SetActive(false);
        if (revealRoot != null) revealRoot.SetActive(false);
        HideCardTooltip();
    }

    /// <summary>重置内部状态（不清面板显隐；Begin 时整体重来）</summary>
    private void ResetInternalState()
    {
        _currentSide = PlayerSide.P1;
        _excluded.Clear();
        if (confirmButton != null) confirmButton.interactable = false;
    }

    // ==========================================
    //  卡片构建、简化布局与排除交互
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
            SetupCardVisual(card);
            if (card.nameText != null) card.nameText.text = GetDisplayName(kind);

            // 交互（事件转发组件，不在卡片上持任何对局状态）：整卡点击切换禁选；悬停显示描述 Tooltip
            var interact = card.gameObject.AddComponent<UI_CityStateCard>();
            var captured = kind;
            interact.OnClicked = () => OnToggleCard(captured);
            interact.OnHoverStart = () => ShowCardTooltip(captured);
            interact.OnHoverEnd = HideCardTooltip;

            _cards.Add(card);
            _cardMap[kind] = card;
            RefreshCardVisual(card, excluded: false);
        }
    }

    /// <summary>卡片运行时简化布局（不改 ShopItem 预制体）：整卡 = 图像框（icon 占位色块）+ 城邦名；
    /// 价格/描述/属性/购买按钮等商店元素隐藏；根节点 Image 作为唯一射线目标 → 整卡点击/悬停生效。</summary>
    private void SetupCardVisual(UI_ShopItemRefs card)
    {
        if (card.priceText != null) card.priceText.gameObject.SetActive(false);
        if (card.descText != null) card.descText.gameObject.SetActive(false);
        if (card.statsText != null) card.statsText.gameObject.SetActive(false);
        if (card.buyButton != null) card.buyButton.gameObject.SetActive(false); // 含子物体 buttonText

        // 子节点图形不拦截射线（根 Image 是唯一射线目标；根自身保持 raycastTarget）
        var rootImage = card.GetComponent<Image>();
        foreach (var img in card.GetComponentsInChildren<Image>(true))
            if (img != rootImage) img.raycastTarget = false;

        // 装饰用棕色底图（预制体「Image」）隐藏，只留 iconImage 作图像框
        var decor = card.transform.Find("Image");
        if (decor != null) decor.gameObject.SetActive(false);

        // 图像框（iconImage）：顶部居中放大；当前无素材 → 清 Sprite 用占位色块（换皮时在 Inspector 拖 Sprite）
        if (card.iconImage != null)
        {
            card.iconImage.sprite = null;
            var rt = card.iconImage.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f);
            rt.sizeDelta = new Vector2(110f, 110f);
        }

        // 城邦名：底部居中
        if (card.nameText != null)
        {
            card.nameText.alignment = TextAlignmentOptions.Center;
            card.nameText.fontSize = 18;
            var rt = card.nameText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 6f);
            rt.sizeDelta = new Vector2(130f, 24f);
        }
    }

    /// <summary>点击卡片：切换排除/取消排除（排除满 ExcludesRequired 后只能先取消再加）</summary>
    private void OnToggleCard(CityStateKind kind)
    {
        if (!_cardMap.TryGetValue(kind, out var card)) return;

        if (_excluded.Contains(kind))
        {
            _excluded.Remove(kind);
            RefreshCardVisual(card, excluded: false);
        }
        else
        {
            if (_excluded.Count >= ExcludesRequired) return;
            _excluded.Add(kind);
            RefreshCardVisual(card, excluded: true);
        }
        UpdateCount();
    }

    /// <summary>卡片视觉刷新：已排除 = 整卡暗红 + 名字/图标压暗（强对比）；未排除 = 白卡常态</summary>
    private void RefreshCardVisual(UI_ShopItemRefs card, bool excluded)
    {
        if (card == null) return;

        var bg = card.GetComponent<Image>();
        if (bg != null) bg.color = excluded ? ExcludedCardColor : Color.white;
        if (card.nameText != null) card.nameText.color = excluded ? ExcludedTextColor : Color.white;
        if (card.iconImage != null) card.iconImage.color = excluded ? ExcludedIconColor : GetIconPlaceholderColor(kindOf(card));
    }

    /// <summary>由卡片反查城邦（视觉刷新用）</summary>
    private CityStateKind kindOf(UI_ShopItemRefs card)
    {
        foreach (var kv in _cardMap)
            if (kv.Value == card) return kv.Key;
        return CityStateKind.None;
    }

    private void UpdateCount()
    {
        if (countText != null) countText.text = $"已排除 {_excluded.Count}/{ExcludesRequired}";
        if (confirmButton != null) confirmButton.interactable = (_excluded.Count == ExcludesRequired);
    }

    // ==========================================
    //  提交（排除 → 心仪转换；只记录，不结算）
    // ==========================================
    private void OnConfirm()
    {
        if (_excluded.Count != ExcludesRequired) return;

        // 「排除 → 心仪」转换点：心仪 4 个 = 全部 6 个 − 已排除 2 个。
        // 数学等价（候选 = 双方都没排除的城邦），故 CityStateModel.Resolve 无需任何改动。
        var desired = new List<CityStateKind>();
        foreach (var kind in CityStateManager.AllCityStates)
            if (!_excluded.Contains(kind)) desired.Add(kind);

        var manager = CityStateManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[UI_CityStateExclusion] 场景缺 CityStateManager，无法提交城邦选择");
            return;
        }

        // ① 本地提交入口（单一；联机预留：网络层同步本地选择，此处零改动）
        manager.SubmitSelection(_currentSide, desired);
        Debug.Log($"[UI_CityStateExclusion] {(_currentSide == PlayerSide.P1 ? "玩家1" : "玩家2")} 排除 {_excluded.Count} 个 → 提交心仪 [{string.Join(", ", desired)}]");

        // ② 双方提交状态查询（统一查询；不再持有 _p1Submitted/_p2Submitted 本地布尔）
        if (manager.HasBothSubmitted)
        {
            // 选择阶段不显示任何结算结果（避免提前剧透）；只解锁「开始游戏」
            if (confirmButton != null) confirmButton.interactable = false;
            SetCardsInteractive(false);
            if (titleText != null) titleText.text = "双方已完成禁选，点击「开始游戏」";
            OnBothSubmitted?.Invoke();
        }
        else
        {
            NextSide();
        }
    }

    /// <summary>切到 P2（清空当前方排除记录，不向 P2 展示 P1 的选择 = 热座私密性）</summary>
    private void NextSide()
    {
        _currentSide = PlayerSide.P2;
        _excluded.Clear();
        RebuildCards();
        if (titleText != null) titleText.text = $"玩家2 · 排除 {ExcludesRequired} 个不想要的城邦";
        UpdateCount();
    }

    /// <summary>锁定/解锁卡片交互（双方提交完成后锁定，防止改动已定选择）</summary>
    private void SetCardsInteractive(bool interactive)
    {
        foreach (var card in _cards)
        {
            if (card == null) continue;
            var interact = card.GetComponent<UI_CityStateCard>();
            if (interact != null) interact.enabled = interactive;
        }
    }

    // ==========================================
    //  揭晓（「开始游戏」点击后由 UI_MainMenu 调用；自动关闭）
    // ==========================================
    /// <summary>弹提示框显示本局城邦 → 自动关闭 → 回调（载入局内）。
    /// 联机预留：提示内容由网络权威结算结果填充，本方法纯显示，接入时零改动。</summary>
    public void ShowReveal(CityStateKind kind, System.Action onClosed)
    {
        if (revealRoot != null) revealRoot.SetActive(true);
        if (revealText != null)
            revealText.text = kind == CityStateKind.None
                ? "本局无城邦"
                : $"本局城邦\n「{GetDisplayName(kind)}」";
        if (confirmButton != null) confirmButton.interactable = false;
        if (_revealRoutine != null) StopCoroutine(_revealRoutine);
        _revealRoutine = StartCoroutine(RevealRoutine(onClosed));
    }

    private IEnumerator RevealRoutine(System.Action onClosed)
    {
        yield return new WaitForSecondsRealtime(revealDuration); // 自动关闭；[PLACEHOLDER] 时长实机调整
        if (revealRoot != null) revealRoot.SetActive(false);
        _revealRoutine = null;
        onClosed?.Invoke();
    }

    // ==========================================
    //  描述悬浮提示（复用 UI_ItemTooltip）
    // ==========================================
    private void ShowCardTooltip(CityStateKind kind)
    {
        if (tooltip == null) return;
        if (!_cardMap.TryGetValue(kind, out var card)) return;
        // 以卡片 RectTransform 为基准：贴卡片右缘 + 顶对齐；内容 = 城邦名 + 描述（卡片本身保持干净）
        tooltip.Show(GetDisplayName(kind), GetDescription(kind), (RectTransform)card.transform, (RectTransform)card.transform);
    }

    private void HideCardTooltip()
    {
        if (tooltip != null) tooltip.Hide();
    }

    // ==========================================
    //  城邦文案与图片占位（View 层静态表；与枚举一一对应，换皮只换色/Sprite）
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

    /// <summary>图片位纯色占位（当前无美术素材；换皮时在 Inspector 替换 iconImage 的 Sprite 即可，层级不动）</summary>
    internal static Color GetIconPlaceholderColor(CityStateKind kind) => kind switch
    {
        CityStateKind.Trade => new Color(0.83f, 0.69f, 0.36f, 1f),    // 金色
        CityStateKind.Merriment => new Color(0.85f, 0.45f, 0.62f, 1f), // 粉紫
        CityStateKind.Monsoon => new Color(0.36f, 0.62f, 0.83f, 1f),   // 雨蓝
        CityStateKind.Occult => new Color(0.55f, 0.4f, 0.72f, 1f),     // 紫
        CityStateKind.War => new Color(0.78f, 0.3f, 0.28f, 1f),        // 血红
        CityStateKind.Element => new Color(0.35f, 0.78f, 0.55f, 1f),   // 翠绿
        _ => new Color(0.5f, 0.5f, 0.5f, 1f)
    };
}
