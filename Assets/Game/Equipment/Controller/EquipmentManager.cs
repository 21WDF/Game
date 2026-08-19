using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备控制器（Equipment 模块的 Controller）—— 单例 MonoBehaviour。
/// 职责：管理商店装备列表、双方背包、装备槽位（3 槽）的装备/卸下，并通过工厂实例化被动效果。
/// 挂载在场景中的 EquipmentManager GameObject 上（参照 GoldManager 写法）。
///
/// 设计要点：
///   1) availableEquipment 由 Inspector 拖入（商店装备列表）。
///   2) 背包按 PlayerSide 分组，Start 中初始化为空列表。
///   3) 购买：GoldManager.TrySpendGold 扣费 → 创建 EquipmentModel → 工厂实例化被动 → 加入背包。
///   4) 装备/卸下：操作 PieceModel.EquippedItems[slot]，调用被动 OnEquip/OnUnequip；
///      卸下时若 CurrentHP 超过新 MaxHP 则截断（bonusHP=CurrentHP不变的决策）。
///   5) 事件 OnEquipmentChanged 通知 UI 刷新。
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    [Header("商店装备列表（Inspector 拖入）")]
    [SerializeField] private EquipmentData[] availableEquipment;

    // ---- 运行时数据 ----
    private readonly Dictionary<PlayerSide, List<EquipmentModel>> _backpack = new();
    private int _equipOrderCounter;   // 装备时间戳计数器（唯一被动「先进先出」先后判断用）

    // ---- 事件 ----
    /// <summary>某方装备/背包变化时触发（商店购买、装备、卸下均触发）</summary>
    public event Action<PlayerSide> OnEquipmentChanged;

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _backpack[PlayerSide.P1] = new List<EquipmentModel>();
        _backpack[PlayerSide.P2] = new List<EquipmentModel>();
    }

    // ==========================================
    //  商店 / 背包查询
    // ==========================================
    public EquipmentData[] GetShopItems() => availableEquipment;

    public List<EquipmentModel> GetBackpack(PlayerSide side)
        => _backpack.TryGetValue(side, out var list) ? list : new List<EquipmentModel>();

    // ==========================================
    //  购买
    // ==========================================
    /// <summary>为 side 购买指定装备。金币不足或装备无效则返回 false。</summary>
    public bool BuyEquipment(PlayerSide side, EquipmentData data)
    {
        if (data == null) return false;

        if (GoldManager.Instance == null || !GoldManager.Instance.TrySpendGold(side, data.price))
        {
            Debug.Log($"[EquipmentManager] {side} 金币不足，无法购买 {data.displayName}");
            return false;
        }

        var model = new EquipmentModel(data);
        InstantiatePassives(model);
        GetBackpack(side).Add(model);
        OnEquipmentChanged?.Invoke(side);
        Debug.Log($"[EquipmentManager] {side} 购买 {data.displayName}（花费 {data.price}），背包+1");
        return true;
    }

    // ==========================================
    //  装备 / 卸下
    // ==========================================
    /// <summary>将 equipment 装备到 piece 的指定槽位（0~2）。
    /// 流程：从背包移除待装备项 → 若槽位已有旧装备则卸下回背包 → 装新装备 → 调用 OnEquip。</summary>
    public void EquipToPiece(PieceModel piece, EquipmentModel equipment, int slot)
    {
        if (piece == null || equipment == null) return;
        if (slot < 0 || slot >= piece.EquippedItems.Length)
        {
            Debug.LogWarning($"[EquipmentManager] 无效槽位 {slot}");
            return;
        }

        // 从背包移除待装备项
        GetBackpack(piece.Owner).Remove(equipment);

        // 卸下旧装备（回背包，不单独触发事件，末尾统一触发）
        if (piece.EquippedItems[slot] != null)
        {
            UnequipInternal(piece, slot, returnToBackpack: true, fireEvent: false);
        }

        // 装新装备（EquipOrder 时间戳：唯一被动「同 id 只生效最后装备」判断用）
        equipment.EquipOrder = ++_equipOrderCounter;
        piece.EquippedItems[slot] = equipment;
        foreach (var passive in equipment.ActivePassives)
            passive?.OnEquip(piece);

        OnEquipmentChanged?.Invoke(piece.Owner);
        Debug.Log($"[EquipmentManager] {piece.Data.displayName} 槽位{slot} 装备 {equipment.Data.displayName}");
    }

    /// <summary>卸下 piece 指定槽位的装备，放回背包，调用 OnUnequip。</summary>
    public void UnequipFromPiece(PieceModel piece, int slot)
    {
        if (piece == null) return;
        if (slot < 0 || slot >= piece.EquippedItems.Length) return;

        UnequipInternal(piece, slot, returnToBackpack: true, fireEvent: true);
    }

    private void UnequipInternal(PieceModel piece, int slot, bool returnToBackpack, bool fireEvent)
    {
        var equipped = piece.EquippedItems[slot];
        if (equipped == null) return;

        // 死亡之蔑：有未结算的延迟伤害期间禁止卸下（延迟池记在装备实例上，卸下会丢失结算）
        foreach (var passive in equipped.ActivePassives)
        {
            if (passive is DeathDancePassive dd && dd.HasPendingDamage)
            {
                Debug.Log($"[EquipmentManager] {equipped.Data.displayName} 有未结算的延迟伤害，禁止卸下/替换");
                return;
            }
        }

        equipped.EquipOrder = 0;   // 卸下清零时间戳（重新装备会拿到新时间戳 = 最后装备）

        foreach (var passive in equipped.ActivePassives)
            passive?.OnUnequip(piece);

        piece.EquippedItems[slot] = null;

        if (returnToBackpack)
            GetBackpack(piece.Owner).Add(equipped);

        // 卸下后若 CurrentHP 超过新 MaxHP，截断（bonusHP 决策：CurrentHP 不变，仅截断溢出）
        if (piece.CurrentHP > piece.MaxHP)
            piece.SetHP(piece.MaxHP);

        if (fireEvent)
            OnEquipmentChanged?.Invoke(piece.Owner);

        Debug.Log($"[EquipmentManager] {piece.Data.displayName} 槽位{slot} 卸下 {equipped.Data.displayName}");
    }

    // ==========================================
    //  被动效果实例化
    // ==========================================
    /// <summary>根据 EquipmentData.passives 的类名 + JSON 参数实例化被动效果，注入 model.ActivePassives。
    /// 工厂逻辑统一在 PassiveFactory（与棋子内建被动共用）。</summary>
    private void InstantiatePassives(EquipmentModel model)
    {
        if (model.Data.passives == null) return;
        foreach (var cfg in model.Data.passives)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.className)) continue;
            var passive = PassiveFactory.Create(cfg.className, cfg.jsonParams, cfg.uniqueId);
            if (passive != null)
                model.ActivePassives.Add(passive);
            else
                Debug.LogWarning($"[EquipmentManager] 未知被动类名: {cfg.className}");
        }
    }
}
