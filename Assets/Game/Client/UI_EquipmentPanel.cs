using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 装备面板 —— 显示选中棋子的 3 个装备槽位 + 当前活动玩家背包。
/// 订阅 InputHandler.OnEquipmentToggle（E 键）开启/关闭。
/// 订阅 BattleModel.OnSelectionChanged + EquipmentManager.OnEquipmentChanged，面板打开时自动刷新。
///
/// 交互：
///   - 背包项 "装备" 按钮 → 装备到首个空槽位（无空槽则替换槽位0）
///   - 槽位 "卸下" 按钮 → 卸下回背包
///   - 未选中棋子时，槽位显示 "—"，背包 "装备" 按钮禁用
///
/// 挂载：脚本挂在 Canvas 下常驻 active 的 GameObject 上，panelRoot 拖入装备面板 UI 根（可显隐）。
/// Editor 搭建：
///   1. Canvas 下创建 EquipmentPanel（含背景、titleText、Slots 容器、Backpack 容器）
///   2. 常驻 GameObject 挂本脚本，panelRoot 指向 EquipmentPanel
///   3. 复用 ShopItem 预制体或新建装备项预制体（挂 UI_EquipmentItemRefs，配齐引用）
///   4. Inspector 拖入：panelRoot、slotsContainer、backpackContainer、itemPrefab、titleText
/// </summary>
public class UI_EquipmentPanel : MonoBehaviour
{
    [Header("面板引用")]
    [SerializeField] private GameObject panelRoot;            // 装备面板根（显隐控制）
    [SerializeField] private Transform slotsContainer;       // 3 个槽位容器
    [SerializeField] private Transform backpackContainer;    // 背包列表容器
    [SerializeField] private UI_EquipmentItemRefs itemPrefab;
    [SerializeField] private TextMeshProUGUI titleText;

    private InputHandler _input;
    private readonly List<UI_EquipmentItemRefs> _slotItems = new();
    private readonly List<UI_EquipmentItemRefs> _backpackItems = new();

    private void OnEnable()
    {
        StartCoroutine(SubscribeNextFrame());
    }

    private System.Collections.IEnumerator SubscribeNextFrame()
    {
        yield return null;
        _input = FindFirstObjectByType<InputHandler>();
        if (_input != null)
            _input.OnEquipmentToggle += Toggle;
        if (BattleController.Instance != null && BattleController.Instance.Model != null)
            BattleController.Instance.Model.OnSelectionChanged += OnSelectionChanged;
        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.OnEquipmentChanged += OnEquipmentChanged;
        if (panelRoot != null) panelRoot.SetActive(false);  // 初始隐藏（打开时由 Toggle 刷新）
    }

    private void OnDisable()
    {
        if (_input != null)
            _input.OnEquipmentToggle -= Toggle;
        if (BattleController.Instance != null && BattleController.Instance.Model != null)
            BattleController.Instance.Model.OnSelectionChanged -= OnSelectionChanged;
        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.OnEquipmentChanged -= OnEquipmentChanged;
    }

    // ==========================================
    //  显隐
    // ==========================================
    private void Toggle()
    {
        if (panelRoot == null) return;
        bool show = !panelRoot.activeSelf;
        panelRoot.SetActive(show);
        if (show) InputHandler.RegisterPanelOpen(); else InputHandler.RegisterPanelClose();
        if (show) Refresh();  // 打开时刷新一次，显示最新选中棋子装备
    }

    private void OnSelectionChanged(PieceModel piece)
    {
        if (panelRoot != null && panelRoot.activeSelf) Refresh();
    }

    private void OnEquipmentChanged(PlayerSide side)
    {
        if (panelRoot != null && panelRoot.activeSelf) Refresh();
    }

    // ==========================================
    //  重建
    // ==========================================
    private void Refresh()
    {
        if (itemPrefab == null) return;

        var selected = BattleController.Instance != null && BattleController.Instance.Model != null
            ? BattleController.Instance.Model.SelectedPiece : null;
        PlayerSide side = TurnManager.Instance != null ? TurnManager.Instance.ActivePlayer : PlayerSide.P1;

        // 标题
        if (titleText != null)
            titleText.text = selected != null ? $"{selected.Data.displayName} 的装备" : "装备管理（未选中棋子）";

        // ---- 3 个槽位 ----
        ClearList(_slotItems);
        if (slotsContainer != null)
        {
            for (int i = 0; i < 3; i++)
            {
                var item = Instantiate(itemPrefab, slotsContainer);
                _slotItems.Add(item);
                var equipped = selected != null ? selected.EquippedItems[i] : null;
                if (equipped != null)
                {
                    if (item.nameText != null) item.nameText.text = $"槽{i}: {equipped.Data.displayName}";
                    if (item.statsText != null) item.statsText.text = UI_ShopPanel.BuildStatsText(equipped.Data);
                    if (item.iconImage != null)
                    {
                        item.iconImage.sprite = equipped.Data.icon;
                        item.iconImage.gameObject.SetActive(equipped.Data.icon != null);
                    }
                    if (item.buttonText != null) item.buttonText.text = "卸下";
                    if (item.actionButton != null && selected != null)
                    {
                        item.actionButton.interactable = true;
                        int slot = i;
                        var piece = selected;
                        item.actionButton.onClick.AddListener(() => OnUnequip(piece, slot));
                    }
                }
                else
                {
                    if (item.nameText != null) item.nameText.text = $"槽{i}: 空";
                    if (item.statsText != null) item.statsText.text = "";
                    if (item.buttonText != null) item.buttonText.text = "—";
                    if (item.iconImage != null)
                        item.iconImage.gameObject.SetActive(false);
                    if (item.actionButton != null) item.actionButton.interactable = false;
                }
            }
        }

        // ---- 背包 ----
        ClearList(_backpackItems);
        if (backpackContainer != null && EquipmentManager.Instance != null)
        {
            var backpack = EquipmentManager.Instance.GetBackpack(side);
            foreach (var eq in backpack)
            {
                if (eq == null) continue;
                var item = Instantiate(itemPrefab, backpackContainer);
                _backpackItems.Add(item);
                if (item.nameText != null) item.nameText.text = eq.Data.displayName;
                if (item.statsText != null) item.statsText.text = UI_ShopPanel.BuildStatsText(eq.Data);
                if (item.iconImage != null)
                {
                    item.iconImage.sprite = eq.Data.icon;
                    item.iconImage.gameObject.SetActive(eq.Data.icon != null);
                }
                if (item.buttonText != null) item.buttonText.text = "装备";
                if (item.actionButton != null)
                {
                    item.actionButton.interactable = selected != null;
                    var captured = eq;
                    var piece = selected;
                    item.actionButton.onClick.AddListener(() => OnEquip(piece, captured));
                }
            }
        }
    }

    // ==========================================
    //  交互
    // ==========================================
    private void OnEquip(PieceModel piece, EquipmentModel equipment)
    {
        if (piece == null || equipment == null || EquipmentManager.Instance == null) return;
        int slot = FirstEmptySlot(piece);
        if (slot < 0) slot = 0;  // 无空槽则替换槽位0
        EquipmentManager.Instance.EquipToPiece(piece, equipment, slot);
    }

    private void OnUnequip(PieceModel piece, int slot)
    {
        if (piece == null || EquipmentManager.Instance == null) return;
        EquipmentManager.Instance.UnequipFromPiece(piece, slot);
    }

    private static int FirstEmptySlot(PieceModel piece)
    {
        if (piece == null) return -1;
        for (int i = 0; i < piece.EquippedItems.Length; i++)
            if (piece.EquippedItems[i] == null) return i;
        return -1;
    }

    private static void ClearList(List<UI_EquipmentItemRefs> list)
    {
        foreach (var item in list)
            if (item != null) Destroy(item.gameObject);
        list.Clear();
    }
}
