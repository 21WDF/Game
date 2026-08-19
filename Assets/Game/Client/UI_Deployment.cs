using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 部署面板（战前流程侧边栏）—— 显示己方选中的 7 个棋子，点击选中后到棋盘己方半场放置。
/// 侧边栏模式：不 RegisterPanelOpen（不屏蔽棋盘点击），BattleController 委托点击给 GameFlowController。
/// 7 个全放完后确认按钮可用 → GameFlowController.OnDeploymentConfirmed。
///
/// Editor 搭建：复用 ShopItem 预制体（UI_ShopItemRefs）；
///   panelRoot / contentContainer / itemPrefab / titleText / countText / confirmButton 由 Inspector 拖入。
/// </summary>
public class UI_Deployment : MonoBehaviour
{
    [Header("面板引用")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform contentContainer;
    [SerializeField] private UI_ShopItemRefs itemPrefab;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Button confirmButton;

    private readonly List<UI_ShopItemRefs> _items = new();

    private void Start()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
            confirmButton.interactable = false;
        }
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Show(PlayerSide side, List<PieceData> selection, List<bool> placed)
    {
        if (titleText != null)
            titleText.text = $"{(side == PlayerSide.P1 ? "玩家1" : "玩家2")} · 部署棋子（点击棋盘己方半场放置）";
        if (panelRoot != null) panelRoot.SetActive(true);
        // 侧边栏不 RegisterPanelOpen —— 部署需要点击棋盘
        Rebuild(selection, placed, -1, false);
        UpdateCount(selection, placed);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Refresh(List<PieceData> selection, List<bool> placed, int pendingIndex, bool allPlaced)
    {
        Rebuild(selection, placed, pendingIndex, allPlaced);
        UpdateCount(selection, placed);
    }

    private void Rebuild(List<PieceData> selection, List<bool> placed, int pendingIndex, bool allPlaced)
    {
        foreach (var item in _items)
            if (item != null) Destroy(item.gameObject);
        _items.Clear();
        if (itemPrefab == null || contentContainer == null) return;

        for (int i = 0; i < selection.Count; i++)
        {
            var data = selection[i];
            if (data == null) continue;
            var item = Instantiate(itemPrefab, contentContainer);
            if (item.nameText != null) item.nameText.text = data.displayName;
            if (item.priceText != null) item.priceText.text = $"HP{data.maxHP} 攻{data.attack} 防{data.defense}";
            if (item.descText != null) item.descText.text = placed[i] ? "已部署 ✓" : "待部署";
            if (item.statsText != null) item.statsText.text = (i == pendingIndex) ? "← 已选中" : "";

            if (item.buttonText != null)
                item.buttonText.text = placed[i] ? "已部署" : (i == pendingIndex ? "取消" : "选中");
            if (item.buyButton != null)
            {
                item.buyButton.interactable = !placed[i];
                int captured = i;
                item.buyButton.onClick.AddListener(() => OnSelectPiece(captured));
            }
            _items.Add(item);
        }

        if (confirmButton != null)
            confirmButton.interactable = allPlaced;
    }

    private void OnSelectPiece(int index)
    {
        GameFlowController.Instance?.SelectPendingPiece(index);
    }

    private void OnConfirmClicked()
    {
        GameFlowController.Instance?.OnDeploymentConfirmed();
    }

    private void UpdateCount(List<PieceData> selection, List<bool> placed)
    {
        if (countText == null) return;
        int placedCount = 0;
        foreach (var p in placed) if (p) placedCount++;
        countText.text = $"已部署 {placedCount}/{selection.Count}";
    }
}
