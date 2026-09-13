using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏结束面板 —— 订阅 TurnModel.OnGameOver 事件，显示获胜方，
/// "再来一局"按钮重载当前局内场景，"返回大厅"按钮载入大厅场景。
/// 挂在 Canvas 下的 UI GameObject 上，panelRoot 指向面板根。
/// 面板打开时 RegisterPanelOpen 屏蔽棋盘交互。
///
/// Editor 搭建：panelRoot / resultText / restartButton / menuButton 由 Inspector 拖入；
/// menuButton 为空时仅隐藏返回大厅入口（不报错）。
/// </summary>
public class UI_GameOver : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;   // 返回大厅（Inspector 拖入；未接线则无此入口）

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestart);
        if (menuButton != null)
            menuButton.onClick.AddListener(OnReturnToMenu);
        StartCoroutine(SubscribeNextFrame());
    }

    private System.Collections.IEnumerator SubscribeNextFrame()
    {
        yield return null;
        if (TurnManager.Instance != null && TurnManager.Instance.Model != null)
            TurnManager.Instance.Model.OnGameOver += OnGameOver;
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.Model != null)
            TurnManager.Instance.Model.OnGameOver -= OnGameOver;
    }

    private void OnGameOver(PlayerSide winner)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        if (resultText != null)
            resultText.text = $"{(winner == PlayerSide.P1 ? "玩家1" : "玩家2")} 获胜！";
        InputHandler.RegisterPanelOpen();
        Debug.Log($"[UI_GameOver] 游戏结束，{winner} 获胜");
    }

    private void OnRestart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnReturnToMenu()
    {
        // 大厅须在 Build Settings 中，否则 LoadScene 抛异常；先做存在性检查，给出可操作提示
        const string menuSceneName = "MainMenu";
        if (SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainMenu.unity") < 0)
        {
            Debug.LogError($"[UI_GameOver] 找不到大厅场景 {menuSceneName}，请先在 Build Settings 加入 Assets/Scenes/MainMenu.unity。");
            return;
        }
        Debug.Log($"[UI_GameOver] 返回大厅 {menuSceneName}");
        SceneManager.LoadScene(menuSceneName);
    }
}
