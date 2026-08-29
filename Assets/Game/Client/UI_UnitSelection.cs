using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 棋子选择面板（战前流程）—— 从 PieceRegistry 读取全部棋子，玩家选 7 个后确认。
/// 挂在 Canvas 下的 UI GameObject 上，panelRoot 指向面板根。
/// 面板打开时 RegisterPanelOpen 屏蔽棋盘点击；关闭时 RegisterPanelClose。
/// 确认时调用 GameFlowController.OnSelectionConfirmed。
///
/// Editor 搭建：复用 ShopItem 预制体（UI_ShopItemRefs）作为列表项；
///   panelRoot / contentContainer / itemPrefab / titleText / countText / confirmButton 由 Inspector 拖入。
/// </summary>
public class UI_UnitSelection : MonoBehaviour
{
    [Header("面板引用")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform contentContainer;
    [SerializeField] private UI_ShopItemRefs itemPrefab;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Button confirmButton;

    private readonly List<PieceData> _allPieces = new();
    private readonly HashSet<int> _selectedIds = new();
    private readonly List<UI_ShopItemRefs> _items = new();
    private PlayerSide _currentSide;

    private void Start()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirm);
            confirmButton.interactable = false;
        }
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Show(PlayerSide side)
    {
        _currentSide = side;
        _selectedIds.Clear();
        if (titleText != null)
            titleText.text = $"{(side == PlayerSide.P1 ? "玩家1" : "玩家2")} · 选择 7 个棋子";
        LoadPieces();
        RebuildList();
        if (panelRoot != null) panelRoot.SetActive(true);
        InputHandler.RegisterPanelOpen();
        UpdateCount();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        InputHandler.RegisterPanelClose();
    }

    private void LoadPieces()
    {
        _allPieces.Clear();
        var registry = PieceManager.Instance?.registry;
        if (registry != null && registry.pieces != null)
        {
            // 召唤物（傀儡等）不进入选人池：只能由大招投放
            foreach (var p in registry.pieces)
                if (p != null && !p.isSummon) _allPieces.Add(p);
        }
    }

    private void RebuildList()
    {
        foreach (var item in _items)
            if (item != null) Destroy(item.gameObject);
        _items.Clear();
        if (itemPrefab == null || contentContainer == null) return;

        foreach (var data in _allPieces)
        {
            if (data == null) continue;
            var item = Instantiate(itemPrefab, contentContainer);
            if (item.nameText != null) item.nameText.text = data.displayName;
            if (item.priceText != null) item.priceText.text = $"HP{data.maxHP} 攻{data.attack} 防{data.defense}";
            // 显示范围：内联配置 baseRange（与 PieceModel.MoveRange/AttackRange 口径一致）
            int dispMove = data.moveConfig.baseRange;
            int dispAttack = data.attackConfig.baseRange;
            // 城邦过滤：非元素城邦下元素系统不生效，不显示先天元素
            string elemText = ElementReactionTable.ElementSystemActive ? $" 元素{data.innateElement}" : string.Empty;
            if (item.descText != null) item.descText.text = $"移{dispMove} 射{dispAttack}{elemText}";
            if (item.statsText != null)
                item.statsText.text = data.ultimateConfig != null ? $"大招:{data.ultimateConfig.ultimateName}" : "无大招";
            if (item.buttonText != null) item.buttonText.text = "选择";
            var captured = data;
            var capturedItem = item;
            if (item.buyButton != null)
                item.buyButton.onClick.AddListener(() => OnToggleSelect(captured, capturedItem));
            _items.Add(item);
        }
    }

    private void OnToggleSelect(PieceData data, UI_ShopItemRefs item)
    {
        if (_selectedIds.Contains(data.id))
        {
            _selectedIds.Remove(data.id);
            if (item.buttonText != null) item.buttonText.text = "选择";
        }
        else
        {
            if (_selectedIds.Count >= 7) return;
            _selectedIds.Add(data.id);
            if (item.buttonText != null) item.buttonText.text = "已选 ✓";
        }
        UpdateCount();
    }

    private void UpdateCount()
    {
        if (countText != null) countText.text = $"已选 {_selectedIds.Count}/7";
        if (confirmButton != null) confirmButton.interactable = (_selectedIds.Count == 7);
    }

    private void OnConfirm()
    {
        if (_selectedIds.Count != 7) return;
        var selected = new List<PieceData>();
        foreach (var p in _allPieces)
            if (p != null && _selectedIds.Contains(p.id)) selected.Add(p);
        GameFlowController.Instance?.OnSelectionConfirmed(_currentSide, selected);
    }
}
