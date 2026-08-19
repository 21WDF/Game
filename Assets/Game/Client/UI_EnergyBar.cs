using TMPro;
using UnityEngine;
using System.Collections;

/// <summary>
/// 选中棋子的能量条（事件驱动，参照 UI_APDisplay）。
/// 挂载在 Canvas 下，energyText 指向含 TextMeshProUGUI 的 GameObject（用其显隐控制整条能量条）。
///
/// 订阅三个事件：
///   1. BattleModel.OnSelectionChanged —— 切换监听目标（取消订阅旧棋子 Energy，订阅新棋子 Energy）。
///   2. PieceModel.Energy.OnEnergyChanged —— 能量变化时刷新数值。
///   3. InputHandler.OnAnyPanelOpenChanged —— 面板打开时隐藏，关闭时恢复（仅当选中棋子有 Energy 时）。
///
/// 显示格式："能量: 75/100"；满能时显示"可释放"。
/// 无大招配置的棋子（Energy=null）不显示能量条。
/// </summary>
public class UI_EnergyBar : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI energyText;

    private PieceModel _current;

    private void OnEnable()
    {
        StartCoroutine(SubscribeNextFrame());
    }

    private IEnumerator SubscribeNextFrame()
    {
        yield return null;
        if (BattleController.Instance != null && BattleController.Instance.Model != null)
            BattleController.Instance.Model.OnSelectionChanged += OnSelectionChanged;
        InputHandler.OnAnyPanelOpenChanged += OnPanelOpenChanged;
        SetVisible(false);  // 初始隐藏（无选中棋子）
    }

    private void OnDisable()
    {
        if (BattleController.Instance != null && BattleController.Instance.Model != null)
            BattleController.Instance.Model.OnSelectionChanged -= OnSelectionChanged;
        InputHandler.OnAnyPanelOpenChanged -= OnPanelOpenChanged;
        UnsubscribeEnergy();
    }

    // ==========================================
    //  选中目标切换
    // ==========================================
    private void OnSelectionChanged(PieceModel piece)
    {
        UnsubscribeEnergy();
        _current = piece;

        if (piece != null && piece.Energy != null)
        {
            piece.Energy.OnEnergyChanged += OnEnergyChanged;
            UpdateText(piece.Energy.CurrentEnergy, piece.Energy.MaxEnergy, piece.CanUseUltimate);
            // 面板打开时不显示
            SetVisible(!InputHandler.IsAnyPanelOpen);
        }
        else
        {
            // 无大招配置的棋子或取消选中 → 隐藏
            SetVisible(false);
        }
    }

    private void OnEnergyChanged(int newEnergy)
    {
        if (_current != null && _current.Energy != null)
            UpdateText(newEnergy, _current.Energy.MaxEnergy, _current.CanUseUltimate);
    }

    private void OnPanelOpenChanged(bool open)
    {
        if (_current != null && _current.Energy != null)
            SetVisible(!open);
    }

    // ==========================================
    //  渲染
    // ==========================================
    private void UpdateText(int current, int max, bool canUse)
    {
        if (energyText == null) return;
        energyText.text = canUse ? "可释放" : $"能量: {current}/{max}";
    }

    private void SetVisible(bool visible)
    {
        if (energyText != null) energyText.gameObject.SetActive(visible);
    }

    private void UnsubscribeEnergy()
    {
        if (_current != null && _current.Energy != null)
            _current.Energy.OnEnergyChanged -= OnEnergyChanged;
        _current = null;
    }
}
