using TMPro;
using UnityEngine;

/// <summary>
/// 显示当前活动玩家的金币（Phase 3：事件驱动，取代每帧轮询）
/// 挂载在 Canvas 下的 UI GameObject 上
///
/// 订阅两个事件：
///   1. GoldModel.OnGoldSettled —— 金币变化时实时刷新（若变化方为当前活动玩家）
///   2. TurnModel.OnTurnStarted —— 回合切换时刷新（显示新活动玩家的金币）
/// </summary>
public class UI_GoldDisplay : MonoBehaviour
{
    public TextMeshProUGUI goldText;

    private void OnEnable()
    {
        StartCoroutine(SubscribeNextFrame());
    }

    private System.Collections.IEnumerator SubscribeNextFrame()
    {
        yield return null;
        if (GoldManager.Instance != null && GoldManager.Instance.Model != null)
            GoldManager.Instance.Model.OnGoldSettled += HandleGoldChanged;
        if (TurnManager.Instance != null && TurnManager.Instance.Model != null)
            TurnManager.Instance.Model.OnTurnStarted += HandleTurnStarted;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (GoldManager.Instance != null && GoldManager.Instance.Model != null)
            GoldManager.Instance.Model.OnGoldSettled -= HandleGoldChanged;
        if (TurnManager.Instance != null && TurnManager.Instance.Model != null)
            TurnManager.Instance.Model.OnTurnStarted -= HandleTurnStarted;
    }

    private void HandleGoldChanged(PlayerSide side, int newGold)
    {
        if (TurnManager.Instance != null && side == TurnManager.Instance.ActivePlayer)
            UpdateText(newGold);
    }

    private void HandleTurnStarted(int turn, PlayerSide active)
    {
        if (GoldManager.Instance != null)
            UpdateText(GoldManager.Instance.GetGold(active));
    }

    private void UpdateText(int gold)
    {
        if (goldText != null)
            goldText.text = $"金币: {gold}";
    }

    private void RefreshDisplay()
    {
        if (TurnManager.Instance == null || GoldManager.Instance == null) return;
        UpdateText(GoldManager.Instance.GetGold(TurnManager.Instance.ActivePlayer));
    }
}
