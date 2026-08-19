using TMPro;
using UnityEngine;
using System.Collections;

/// <summary>
/// 显示当前活动玩家的剩余 AP（Phase 3：事件驱动，取代每帧轮询）
/// 挂载在 Canvas 下的 UI GameObject 上
///
/// 订阅两个事件：
///   1. APModel.OnAPChanged —— AP 变化时刷新（若变化方为当前活动玩家）
///   2. TurnModel.OnTurnStarted —— 回合切换时刷新（显示新活动玩家的 AP）
/// </summary>
public class UI_APDisplay : MonoBehaviour
{
    public TextMeshProUGUI apText;  // UI Text 组件引用

    private void OnEnable()
    {
        StartCoroutine(SubscribeNextFrame());
    }

    private IEnumerator SubscribeNextFrame()
    {
        yield return null;
        if (APManager.Instance != null && APManager.Instance.Model != null)
            APManager.Instance.Model.OnAPChanged += HandleAPChanged;
        if (TurnManager.Instance != null && TurnManager.Instance.Model != null)
            TurnManager.Instance.Model.OnTurnStarted += HandleTurnStarted;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (APManager.Instance != null && APManager.Instance.Model != null)
            APManager.Instance.Model.OnAPChanged -= HandleAPChanged;
        if (TurnManager.Instance != null && TurnManager.Instance.Model != null)
            TurnManager.Instance.Model.OnTurnStarted -= HandleTurnStarted;
    }

    private void HandleAPChanged(PlayerSide side, int newAP)
    {
        // 只在当前活动玩家的 AP 变化时刷新
        if (TurnManager.Instance != null && side == TurnManager.Instance.ActivePlayer)
            UpdateText(newAP);
    }

    private void HandleTurnStarted(int turn, PlayerSide active)
    {
        // 回合切换，显示新活动玩家的 AP
        if (APManager.Instance != null)
            UpdateText(APManager.Instance.GetAP(active));
    }

    private void UpdateText(int ap)
    {
        if (apText != null)
            apText.text = $"AP: {ap}";
    }

    private void RefreshDisplay()
    {
        if (TurnManager.Instance == null || APManager.Instance == null) return;
        UpdateText(APManager.Instance.GetAP(TurnManager.Instance.ActivePlayer));
    }
}
