using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏结束面板 —— 订阅 TurnModel.OnGameOver 事件，显示获胜方，"再来一局"按钮重载场景。
/// 挂在 Canvas 下的 UI GameObject 上，panelRoot 指向面板根。
/// 面板打开时 RegisterPanelOpen 屏蔽棋盘交互。
///
/// Editor 搭建：panelRoot / resultText / restartButton 由 Inspector 拖入。
/// </summary>
public class UI_GameOver : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button restartButton;

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestart);
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
}
