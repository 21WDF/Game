using TMPro;
using UnityEngine;

/// <summary>
/// 显示当前回合与活动玩家（Phase 3：事件驱动，取代每帧轮询）
/// 挂载在 Canvas 下的 UI GameObject 上
/// </summary>
public class UI_TurnDisplay : MonoBehaviour
{
    public TextMeshProUGUI turnText;

    private void OnEnable()
    {
        // 延迟一帧订阅，确保 Manager 已完成 Awake 初始化
        // （UI 与 Manager 都在场景中时，Awake 顺序不保证）
        StartCoroutine(SubscribeNextFrame());
    }

    private System.Collections.IEnumerator SubscribeNextFrame()
    {
        yield return null; // 等一帧
        if (TurnManager.Instance != null && TurnManager.Instance.Model != null)
        {
            TurnManager.Instance.Model.OnTurnStarted += HandleTurnStarted;
            TurnManager.Instance.Model.OnGameOver += HandleGameOver;
            RefreshDisplay();
        }
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.Model != null)
        {
            TurnManager.Instance.Model.OnTurnStarted -= HandleTurnStarted;
            TurnManager.Instance.Model.OnGameOver -= HandleGameOver;
        }
    }

    private void HandleTurnStarted(int turn, PlayerSide active)
    {
        string playerName = active == PlayerSide.P1 ? "玩家1" : "玩家2";
        // 轮次（双方各行动一次 = 1 轮）取代回合数显示；事件签名不变，轮次从 Model 读取
        int round = TurnManager.Instance != null && TurnManager.Instance.Model != null
            ? TurnManager.Instance.Model.CurrentRound
            : 0;
        if (turnText != null)
            turnText.text = $"第 {round} 轮 · {playerName} 的回合";
    }

    private void HandleGameOver(PlayerSide winner)
    {
        string winnerName = winner == PlayerSide.P1 ? "玩家1" : "玩家2";
        if (turnText != null)
            turnText.text = $"游戏结束！{winnerName} 获胜！";
    }

    private void RefreshDisplay()
    {
        if (TurnManager.Instance == null) return;
        if (TurnManager.Instance.IsGameOver)
        {
            // 无法从 Model 直接获取获胜方，用通用文案
            if (turnText != null) turnText.text = "游戏结束！";
            return;
        }
        HandleTurnStarted(TurnManager.Instance.CurrentTurn, TurnManager.Instance.ActivePlayer);
    }
}
