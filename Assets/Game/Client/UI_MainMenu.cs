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

    // 选项按钮选中态配色（与局内深色 UI 风格协调；换皮只改这两处颜色即可）
    private static readonly Color OptionNormalColor = new Color(0.22f, 0.25f, 0.32f, 1f);
    private static readonly Color OptionSelectedColor = new Color(0.35f, 0.55f, 0.9f, 1f);

    private int _boardRadius = SessionConfig.DefaultBoardRadius;
    private int _piecesPerSide = SessionConfig.DefaultPiecesPerSide;
    private bool _cityStateMode = SessionConfig.DefaultCityStateMode;

    private void Start()
    {
        // 回显：从局内返回大厅时显示上次配置；首次进入显示默认值
        _boardRadius = SessionConfig.BoardRadius;
        _piecesPerSide = SessionConfig.PiecesPerSide;
        _cityStateMode = SessionConfig.CityStateMode;

        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);

        // 棋盘大小 / 棋子数：按钮与选项数组按下标一一对应
        BindOptionButtons(boardSizeButtons, SessionConfig.AllowedBoardRadii,
            value => { _boardRadius = value; RefreshAll(); });
        BindOptionButtons(piecesButtons, SessionConfig.AllowedPiecesPerSide,
            value => { _piecesPerSide = value; RefreshAll(); });
        // 城邦模式：按钮顺序固定为 [随机城邦, 无城邦]
        BindToggleButtons(cityStateButtons,
            index => { _cityStateMode = index == 0; RefreshAll(); });

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

    /// <summary>刷新全部选项标签与选中高亮（选择 / 回显共用）</summary>
    private void RefreshAll()
    {
        if (boardSizeLabel != null) boardSizeLabel.text = $"棋盘大小：半径 {_boardRadius}";
        if (piecesLabel != null) piecesLabel.text = $"每方棋子数：{_piecesPerSide}";
        if (cityStateLabel != null) cityStateLabel.text = _cityStateMode ? "城邦模式：随机城邦" : "城邦模式：无城邦";

        HighlightSelected(boardSizeButtons, IndexOf(SessionConfig.AllowedBoardRadii, _boardRadius));
        HighlightSelected(piecesButtons, IndexOf(SessionConfig.AllowedPiecesPerSide, _piecesPerSide));
        HighlightSelected(cityStateButtons, _cityStateMode ? 0 : 1);
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
        SessionConfig.Apply(_boardRadius, _piecesPerSide, _cityStateMode);
        Debug.Log($"[UI_MainMenu] 开始游戏：半径 {_boardRadius} / 每方 {_piecesPerSide} 个 / {( _cityStateMode ? "随机城邦" : "无城邦")} → 载入 {gameSceneName}");
        SceneManager.LoadScene(gameSceneName);
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
