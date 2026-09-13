using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 大厅主菜单面板 —— 正式游戏入口。
/// 职责：标题展示 · 三项对局配置（棋盘半径 / 每方棋子数 / 城邦模式）选择 · 开始游戏 · 退出游戏。
///
/// 换皮约定（红线：不引入美术素材，结构预留贴图位）：
///   - 背景 = 全屏 Image（backgroundImage），换皮只替换其 Sprite；
///   - 按钮 = Button + 子节点 TMP（换皮只替换按钮 Image 的 Sprite，不动层级）；
///   - 配色沿用局内 UI 风格（深色面板 + 标准 Button 过渡色）。
///
/// 配置传递：选择结果即时写入 SessionConfig（静态载体），「开始游戏」时一并生效并载入局内场景；
/// 局内三处消费：ChessBoardController（半径）/ UI_UnitSelection（棋子数）/ GameFlowController（城邦模式）。
/// 从局内返回大厅时读取 SessionConfig 回显上次选择。
/// </summary>
public class UI_MainMenu : MonoBehaviour
{
    [Header("面板引用（Inspector 拖入）")]
    [SerializeField] private Image backgroundImage;      // 全屏背景占位（换皮拖 Sprite）
    [SerializeField] private TextMeshProUGUI titleText; // 标题
    [SerializeField] private Button startButton;        // 开始游戏
    [SerializeField] private Button exitButton;         // 退出游戏

    [Header("棋盘大小（半径 4/5/6）")]
    [SerializeField] private TextMeshProUGUI boardSizeLabel;
    [SerializeField] private Button[] boardSizeButtons;

    [Header("每方棋子数（3/5/7）")]
    [SerializeField] private TextMeshProUGUI piecesLabel;
    [SerializeField] private Button[] piecesButtons;

    [Header("城邦模式（随机城邦 / 无城邦）")]
    [SerializeField] private TextMeshProUGUI cityStateLabel;
    [SerializeField] private Button[] cityStateButtons;

    [Header("场景")]
    [Tooltip("局内场景名（须在 Build Settings 中，且至少比大厅靠后）")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("城邦排除（开始游戏前的配置步骤；随机城邦模式时显示）")]
    [SerializeField] private UI_CityStateExclusion cityStateExclusionPanel;

    [Header("玩家档案（登录 / 金币 / 商店）")]
    [SerializeField] private TextMeshProUGUI accountText;    // "账号：test"（未登录显示"未登录"）
    [SerializeField] private TextMeshProUGUI goldText;       // "金币：1200"
    [SerializeField] private Button switchAccountButton;     // 打开登录/注册面板
    [SerializeField] private Button shopButton;              // 打开棋子商店
    [SerializeField] private UI_LoginPanel loginPanel;       // 登录/注册面板（Inspector 拖入）
    [SerializeField] private UI_PieceShopPanel shopPanel;    // 棋子商店面板（Inspector 拖入）
    [SerializeField] private PieceRegistry registry;         // 档案棋池解析源（拖一次；为空时回退 PlayerProfileConfig / 局内 PieceManager）

    // 选项按钮选中态配色（与局内深色 UI 风格协调；换皮只改这两处颜色即可）
    private static readonly Color OptionNormalColor = new Color(0.22f, 0.25f, 0.32f, 1f);
    private static readonly Color OptionSelectedColor = new Color(0.35f, 0.55f, 0.9f, 1f);

    private int _boardRadius = SessionConfig.DefaultBoardRadius;
    private int _piecesPerSide = SessionConfig.DefaultPiecesPerSide;
    private bool _cityStateMode = SessionConfig.DefaultCityStateMode;

    private void Start()
    {
        // 玩家档案：先绑定棋池解析源（拖入的注册表），再初始化（首启自动创建并登录默认测试账号）。
        // 认证/档案/结算均走 PlayerProfileService（联机换远端实现后本 UI 零改动）。
        PlayerProfileService.BindRegistry(registry);
        PlayerProfileService.EnsureInitialized();
        PlayerProfileService.OnProfileChanged += OnProfileChanged;

        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
        if (switchAccountButton != null) switchAccountButton.onClick.AddListener(OnSwitchAccountClicked);
        if (shopButton != null) shopButton.onClick.AddListener(OnShopClicked);

        // 回显：从局内返回大厅时显示上次配置；首次进入显示默认值
        _boardRadius = SessionConfig.BoardRadius;
        // C-3 默认值钳制：记录值若未解锁（删档/新档案等），回退到最大已解锁选项，绝不出现「选了 7 但只有 4 个棋子」
        _piecesPerSide = PlayerProfileService.GetValidPieceCount(SessionConfig.PiecesPerSide);
        _cityStateMode = SessionConfig.CityStateMode;

        // 棋盘大小 / 棋子数：按钮与选项数组按下标一一对应
        BindOptionButtons(boardSizeButtons, SessionConfig.AllowedBoardRadii,
            value => { _boardRadius = value; RefreshAll(); });
        BindOptionButtons(piecesButtons, SessionConfig.AllowedPiecesPerSide,
            value => { _piecesPerSide = value; RefreshAll(); });
        // 城邦模式：按钮顺序固定为 [随机城邦, 无城邦]
        BindToggleButtons(cityStateButtons,
            index =>
            {
                _cityStateMode = index == 0;
                // 切到「随机城邦」→ 开始新一轮城邦排除（重置为 P1 先选）；切到「无城邦」→ 隐藏并清空结算
                if (cityStateExclusionPanel != null)
                {
                    if (_cityStateMode) cityStateExclusionPanel.Begin();
                    else { cityStateExclusionPanel.Hide(); SessionConfig.ResetCityStateSelection(); }
                }
                RefreshAll();
            });

        // 城邦排除面板：随机城邦 → 显示并重置为可重新选择；无城邦 → 隐藏并清空结算状态
        if (cityStateExclusionPanel != null)
        {
            cityStateExclusionPanel.OnBothSubmitted += OnBothSubmitted;
            if (_cityStateMode) cityStateExclusionPanel.Begin();
            else { cityStateExclusionPanel.Hide(); SessionConfig.ResetCityStateSelection(); }
        }

        RefreshAll();
    }

    private void BindOptionButtons(Button[] buttons, int[] values, System.Action<int> onSelect)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length && i < values.Length; i++)
        {
            int value = values[i];
            if (buttons[i] != null) buttons[i].onClick.AddListener(() => onSelect(value));
        }
    }

    private void BindToggleButtons(Button[] buttons, System.Action<int> onSelect)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            if (buttons[i] != null) buttons[i].onClick.AddListener(() => onSelect(index));
        }
    }

    /// <summary>刷新全部选项标签与选中高亮（选择 / 回显共用）；
    /// 随机城邦模式且双方未都提交时「开始游戏」置灰（② 提交状态查询门控；结算在开始点击后才发生）</summary>
    private void RefreshAll()
    {
        if (boardSizeLabel != null) boardSizeLabel.text = $"棋盘大小：半径 {_boardRadius}";

        // 每方棋子数：未解锁选项置灰（interactable=false）+ 标签附加锁定提示（C-2，选「置灰」方案）
        if (piecesLabel != null)
        {
            var locked = new List<string>();
            for (int i = 0; i < SessionConfig.AllowedPiecesPerSide.Length; i++)
                if (!PlayerProfileService.IsOptionUnlocked(SessionConfig.AllowedPiecesPerSide[i]))
                    locked.Add(SessionConfig.AllowedPiecesPerSide[i].ToString());
            piecesLabel.text = locked.Count > 0
                ? $"每方棋子数：{_piecesPerSide}（未解锁：{string.Join("/", locked)}）"
                : $"每方棋子数：{_piecesPerSide}";
        }
        if (cityStateLabel != null) cityStateLabel.text = _cityStateMode ? "城邦模式：随机城邦" : "城邦模式：无城邦";

        // 解锁门控：拥有棋子数 ≥ 选项值才可点（C-2）
        if (piecesButtons != null)
        {
            for (int i = 0; i < piecesButtons.Length && i < SessionConfig.AllowedPiecesPerSide.Length; i++)
                if (piecesButtons[i] != null)
                    piecesButtons[i].interactable = PlayerProfileService.IsOptionUnlocked(SessionConfig.AllowedPiecesPerSide[i]);
        }

        bool cityStateReady = CityStateManager.Instance != null && CityStateManager.Instance.HasBothSubmitted;
        if (startButton != null)
            startButton.interactable = !(_cityStateMode && !cityStateReady);

        HighlightSelected(boardSizeButtons, IndexOf(SessionConfig.AllowedBoardRadii, _boardRadius));
        HighlightSelected(piecesButtons, IndexOf(SessionConfig.AllowedPiecesPerSide, _piecesPerSide));
        HighlightSelected(cityStateButtons, _cityStateMode ? 0 : 1);

        RefreshProfileUI();
    }

    // ==========================================
    //  玩家档案（账号 / 金币 / 登录 / 商店）
    // ==========================================
    /// <summary>档案变化（登录 / 金币 / 购买棋子）→ 刷新账号、金币、解锁状态（购买后 5 可能刚刚可用）</summary>
    private void OnProfileChanged() => RefreshAll();

    /// <summary>刷新账号与金币显示（未登录显示占位）</summary>
    private void RefreshProfileUI()
    {
        if (accountText != null)
            accountText.text = PlayerProfileService.IsLoggedIn
                ? $"账号：{PlayerProfileService.CurrentAccount}"
                : "账号：未登录";
        if (goldText != null)
            goldText.text = PlayerProfileService.IsLoggedIn
                ? $"金币：{PlayerProfileService.Current.gold}"
                : "金币：--";
    }

    private void OnSwitchAccountClicked()
    {
        if (loginPanel != null) loginPanel.Show();
    }

    private void OnShopClicked()
    {
        if (shopPanel != null) shopPanel.Show();
    }

    private void OnDestroy()
    {
        PlayerProfileService.OnProfileChanged -= OnProfileChanged;
    }

    /// <summary>双方提交完成回调（② 提交状态查询自 CityStateManager；联机预留：由网络同步触发，此处零改动）：
    /// 放开「开始游戏」，但不在此结算/揭晓（避免提前剧透）</summary>
    private void OnBothSubmitted()
    {
        RefreshAll();
        Debug.Log("[UI_MainMenu] 双方已完成城邦禁选，可开始游戏");
    }

    private static int IndexOf(int[] values, int value)
    {
        for (int i = 0; i < values.Length; i++)
            if (values[i] == value) return i;
        return 0;
    }

    /// <summary>把选项组里选中下标对应的按钮染成选中色，其余恢复常态色</summary>
    private static void HighlightSelected(Button[] buttons, int selectedIndex)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            if (buttons[i].targetGraphic is Image img)
                img.color = i == selectedIndex ? OptionSelectedColor : OptionNormalColor;
        }
    }

    private void OnStartClicked()
    {
        // 随机城邦模式：「开始游戏」须等双方都完成禁选（② 提交状态查询门控；置灰兜底，防代码路径直调）
        var manager = CityStateManager.Instance;
        if (_cityStateMode && (manager == null || !manager.HasBothSubmitted))
        {
            Debug.Log("[UI_MainMenu] 城邦禁选未完成（双方需各排除 2 个），无法开始游戏");
            return;
        }

        // C-3 默认值钳制兜底：开始前再钳制一次（按钮门控已保证可选项合法，此处防直调/改档后残余非法值）
        _piecesPerSide = PlayerProfileService.GetValidPieceCount(_piecesPerSide);
        SessionConfig.Apply(_boardRadius, _piecesPerSide, _cityStateMode);

        if (_cityStateMode)
        {
            // ③ 结算入口（唯一随机入口）：开始点击后结算 → 写 SessionConfig → 弹提示框 → 自动关闭 → 载入局内。
            // 联机预留：「开始游戏」= 本地玩家的准备/提交信号；结算 seed 由网络权威控制，本地流程不变。
            var kind = manager.ResolvePendingSelections();
            SessionConfig.ApplyCityState(kind);
            if (startButton != null) startButton.interactable = false; // 弹框期间防重复点击
            Debug.Log($"[UI_MainMenu] 城邦已结算：{kind}；提示后载入 {gameSceneName}");
            cityStateExclusionPanel.ShowReveal(kind, () => SceneManager.LoadScene(gameSceneName));
        }
        else
        {
            // 无城邦：跳过城邦区，直接进局内（不弹提示框）
            Debug.Log($"[UI_MainMenu] 开始游戏：半径 {_boardRadius} / 每方 {_piecesPerSide} 个 / 无城邦 → 载入 {gameSceneName}");
            SceneManager.LoadScene(gameSceneName);
        }
    }

    private void OnExitClicked()
    {
        Debug.Log("[UI_MainMenu] 退出游戏");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Editor 内退出 Play 模式
#endif
    }
}
