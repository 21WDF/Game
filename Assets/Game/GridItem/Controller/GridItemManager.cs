using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 棋盘道具控制器（GridItem 模块的 Controller）—— 单例 MonoBehaviour。
/// 职责：管理商店道具列表、道具库存（按玩家分组的持有列表）、
///       购买进背包流程（背包系统一期：扣金币→入背包，不再购买即用）、
///       从背包使用流程（移出背包→瞄准→命中消耗+扣AP/取消退回背包）、
///       工厂实例化 IGridItemEffect、瞄准模式视觉（扩展石幽灵格 / 删除石红色高亮 / 传送石蓝色落点）。
/// 挂载在场景中的 GridItemManager GameObject 上（对标 EquipmentManager）。
///
/// 设计要点：
///   1) availableGridItems 由 Inspector 拖入（商店固定资产，不随购买减库存；玩家的持有在 _inventories）。
///   2) 道具库存（一期）：每件道具独立占一格、不堆叠、不持久化；
///      BuyToInventory 扣金币（透支版+信誉涨价）→ 入背包；
///      UseFromInventory 移出背包 → 瞄准（复用现有瞄准/命中/执行链路）→ 命中扣 AP + 消耗；
///      取消（点非目标/无合法目标）→ 道具退回背包，不扣 AP（等价旧「取消退金币」语义）。
///   3) 瞄准模式与 BattleController 集成：BattleController.HandleTileClick 最前面检查 IsTargeting 并委托；
///      道具瞄准进行中忽略 U 键（HandleUltimate 屏蔽）。
///   4) AP 在命中时扣减（取消不扣 AP）。
///   5) requiresSelectedPiece 道具（传送石）：UseFromInventory 校验 SelectedPiece，侧栏「使用」按钮也据此置灰（双保险）。
///   6) 幽灵格正式化时机：玩家点中幽灵格→Execute 调 AddTile（Model.AddCoord + View.CreateTile 返回已存在实例）
///      →坐标入 Model 正式化；ClearTargetingVisuals 清理其余幽灵格时，DestroyGhostTile 保护已正式化的跳过。
/// </summary>
public class GridItemManager : MonoBehaviour
{
    public static GridItemManager Instance { get; private set; }

    [Header("商店棋盘道具列表（Inspector 拖入）")]
    [SerializeField] private GridItemData[] availableGridItems;

    // ---- 瞄准状态 ----
    private bool _targeting;
    private GridItemData _activeData;
    private IGridItemEffect _activeEffect;
    private PlayerSide _activeUser;
    private bool _activeFromInventory;  // 当前瞄准道具来源背包（取消退回背包；命中已消耗）
    private int _activeApCost;        // 命中时扣减的 AP
    private readonly HashSet<HexCoord> _ghostCoords = new();   // 扩展石幽灵格坐标
    private readonly HashSet<HexCoord> _targetCoords = new();  // 删除石/传送石高亮目标坐标

    // ---- 道具库存（背包系统一期：按玩家分组，每件独立占一格）----
    private readonly Dictionary<PlayerSide, List<GridItemData>> _inventories = new()
    {
        [PlayerSide.P1] = new List<GridItemData>(),
        [PlayerSide.P2] = new List<GridItemData>()
    };

    /// <summary>道具持有变化（进背包/移出使用/取消退回）时触发（侧栏刷新）</summary>
    public event Action<PlayerSide> OnInventoryChanged;

    // 渲染参数（幽灵格颜色）收拢到 GameConfig，与 BattleView 移动范围同色
    private GameConfig _config;   // 已有缓存（容量校验同用；缺失时兜底 5——与背包侧栏口径一致）

    /// <summary>是否处于道具瞄准模式（BattleController 据此委托点击）</summary>
    public bool IsTargeting => _targeting;

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _config = Resources.Load<GameConfig>("GameConfig");
        if (_config == null)
            Debug.LogError("[GridItemManager] GameConfig 加载失败！请确保 Assets/Game/Resources/GameConfig.asset 存在。", this);
    }

    // ==========================================
    //  商店查询与道具库存
    // ==========================================
    public GridItemData[] GetShopGridItems() => availableGridItems;

    /// <summary>查询某方当前持有的道具列表（侧栏展示用；每件独立占一格，不堆叠）</summary>
    public List<GridItemData> GetInventory(PlayerSide side)
        => _inventories.TryGetValue(side, out var list) ? list : null;

    /// <summary>道具入背包（唯一入口：购买/取消退回都走这里，统一触发 OnInventoryChanged）</summary>
    private void AddToInventory(PlayerSide side, GridItemData data)
    {
        if (data == null || !_inventories.TryGetValue(side, out var list)) return;
        list.Add(data);
        OnInventoryChanged?.Invoke(side);
    }

    // ==========================================
    //  购买进背包（背包系统一期：扣金币 → 入背包，不进入瞄准）
    // ==========================================
    /// <summary>道具背包容量查询（复用 _config 缓存；缺失时兜底 5——与背包侧栏兜底口径一致）</summary>
    private int GetItemCapacity()
    {
        return _config != null ? _config.backpackItemCapacity : 5;
    }

    /// <summary>无偿获得道具（幸运方块/拍卖交付等；对标 EquipmentManager.GrantEquipment）。
    /// 入背包触发 OnInventoryChanged；不扣金币、不受容量拦截（无偿入口）</summary>
    public void GrantItem(PlayerSide side, GridItemData data)
    {
        if (data == null) return;
        AddToInventory(side, data);
    }

    /// <summary>商店购买道具：道具背包容量已满 / 售价含信誉涨价（贸易之城·二期）→ 透支扣款 → 入背包。
    /// 容量 = GameConfig.backpackItemCapacity（与背包侧栏列容量同源）。
    /// 商店列表是固定资产不随购买减库存；使用走 UseFromInventory（侧栏「使用」按钮）</summary>
    public bool BuyToInventory(PlayerSide side, GridItemData data)
    {
        if (data == null) return false;

        // 容量校验（问题3：道具列已满拒绝购买，不扣金币不入背包）
        int capacity = GetItemCapacity();
        if (GetInventory(side).Count >= capacity)
        {
            Debug.Log($"[GridItemManager] {side} 道具背包已满（{capacity}），无法购买 {data.displayName}");
            return false;
        }

        int price = TradeCityManager.Instance != null
            ? TradeCityManager.Instance.GetAdjustedPrice(side, data.price)
            : data.price;

        // 扣金币（透支版，贸易之城·一期：金币充足时行为与原 TrySpendGold 一致）
        if (GoldManager.Instance == null || !GoldManager.Instance.TrySpendGoldWithOverdraft(side, price))
        {
            Debug.Log($"[GridItemManager] {side} 金币不足（含透支额度），无法购买 {data.displayName}");
            return false;
        }

        AddToInventory(side, data);
        Debug.Log($"[GridItemManager] {side} 购买 {data.displayName}（花费 {price}），进入背包");
        return true;
    }

    // ==========================================
    //  从背包使用（移出背包 → 瞄准 → 命中消耗 / 取消退回）
    // ==========================================
    /// <summary>使用背包道具：前置校验 → 从背包移除 → 进入瞄准模式（复用现有瞄准/命中/执行链路）。
    /// 命中：执行效果 + 扣 AP（道具已消耗）；取消/无合法目标：道具退回背包，不扣 AP。
    /// 前置校验失败（未选中己方棋子/效果类名未知/AP 不足）时道具不动、返回 false</summary>
    public bool UseFromInventory(PlayerSide side, GridItemData data)
    {
        if (data == null) return false;

        // 已在瞄准模式：先取消当前（退回背包），再开始新的
        if (_targeting) CancelTargeting();

        // 前置校验：需要选中棋子的道具（传送石）
        if (data.requiresSelectedPiece)
        {
            var sel = BattleController.Instance?.Model?.SelectedPiece;
            if (sel == null || sel.Owner != side)
            {
                Debug.Log($"[GridItemManager] {side} 使用 {data.displayName} 需先选中己方棋子");
                return false;
            }
        }

        // 创建效果
        var effect = CreateEffect(data);
        if (effect == null)
        {
            Debug.LogWarning($"[GridItemManager] 未知效果类名: {data.effectClassName}");
            return false;
        }

        // AP 预校验（命中时才扣，此处仅检查是否足够）
        if (APManager.Instance == null || !APManager.Instance.HasAP(side, data.apCost))
        {
            Debug.Log($"[GridItemManager] {side} AP 不足，无法使用 {data.displayName}");
            return false;
        }

        // 从背包移除（命中即消耗；取消由 CancelTargeting 退回）
        if (!_inventories.TryGetValue(side, out var list) || !list.Remove(data)) return false;
        OnInventoryChanged?.Invoke(side);

        // 进入瞄准模式
        _targeting = true;
        _activeData = data;
        _activeEffect = effect;
        _activeUser = side;
        _activeFromInventory = true;
        _activeApCost = data.apCost;
        _ghostCoords.Clear();
        _targetCoords.Clear();

        // 生成瞄准视觉
        BuildTargetingVisuals(effect, side);

        // 无合法目标 → 立即取消（退回背包）
        if (_ghostCoords.Count == 0 && _targetCoords.Count == 0)
        {
            Debug.Log($"[GridItemManager] {data.displayName} 无合法目标，退回背包");
            CancelTargeting();
            return false;
        }

        Debug.Log($"[GridItemManager] {side} 使用背包道具 {data.displayName}，进入瞄准模式");
        return true;
    }

    // ==========================================
    //  瞄准视觉生成
    // ==========================================
    private void BuildTargetingVisuals(IGridItemEffect effect, PlayerSide user)
    {
        var board = ChessBoardController.Instance;
        if (board == null) return;

        var allCoordsList = board.AllCoords;
        if (allCoordsList == null) return;
        // 复制坐标列表，避免遍历中集合被修改（理论上此处不修改，但防御性复制）
        var allCoords = new List<HexCoord>(allCoordsList);

        if (effect is ExpandTileEffect)
        {
            // 扩展石：幽灵格。清除选中棋子的行动高亮（幽灵格自身高亮直接操作 HexTile，不走 BattleModel）
            BattleController.Instance?.Model?.ClearHighlights();

            // 收集棋盘外、邻接棋盘的候选坐标
            var candidates = new HashSet<HexCoord>();
            foreach (var c in allCoords)
            {
                for (int i = 0; i < 6; i++)
                {
                    var nb = c.Neighbor(i);
                    if (!board.Contains(nb)) candidates.Add(nb);
                }
            }
            foreach (var cand in candidates)
            {
                if (!effect.CanExecute(cand, user)) continue;
                var tile = board.CreateGhostTile(cand);
                if (tile != null)
                {
                    // 蓝色表示可扩展落点；颜色取自 GameConfig.overlayMoveColor（与移动范围同色，便于统一调参）
                    var c = _config != null ? _config.overlayMoveColor : new Color(0.2f, 0.4f, 1.0f, 0.35f);
                    tile.overlay?.Show(c, c.a);
                    _ghostCoords.Add(cand);
                }
            }
        }
        else
        {
            // 删除石 / 传送石：高亮现有格子中 CanExecute 为 true 的（SetHighlights 替换选中高亮）
            var highlights = new Dictionary<HexCoord, HighlightType>();
            foreach (var c in allCoords)
            {
                if (!effect.CanExecute(c, user)) continue;
                _targetCoords.Add(c);
                // 传送石落点用 Move（蓝），删除石待删格用 Attack（红）
                highlights[c] = (effect is TeleportUnitEffect)
                    ? HighlightType.Move
                    : HighlightType.Attack;
            }
            BattleController.Instance?.Model?.SetHighlights(highlights);
        }
    }

    // ==========================================
    //  瞄准点击处理（由 BattleController.HandleTileClick 委托）
    // ==========================================
    public void HandleTargetClick(HexTile clickedTile)
    {
        if (!_targeting || _activeEffect == null) return;

        HexCoord coord = clickedTile != null ? clickedTile.Coord : default;
        bool isGhost = clickedTile != null && _ghostCoords.Contains(coord);
        bool isTarget = _targetCoords.Contains(coord);

        // 命中合法目标（幽灵格或高亮目标格，且通过 CanExecute 二次校验）
        if ((isGhost || isTarget) && _activeEffect.CanExecute(coord, _activeUser))
        {
            ExecuteHit(coord);
            return;
        }

        // 点击非目标 → 取消（退回背包，不扣 AP）
        Debug.Log($"[GridItemManager] 取消 {_activeData?.displayName} 使用，退回背包");
        CancelTargeting();
    }

    /// <summary>命中目标：执行效果（先于清理，使被点幽灵格正式化受保护）+ 扣 AP + 清理 + 恢复选中高亮。
    /// 背包道具在进入瞄准时已从背包移除 → 命中即消耗，无需再动</summary>
    private void ExecuteHit(HexCoord coord)
    {
        var effect = _activeEffect;
        var user = _activeUser;
        var data = _activeData;
        int apCost = _activeApCost;

        // 先执行效果：
        //   - 扩展石：AddTile 把被点幽灵格坐标写入 Model 正式化（View 中已存在该实例）
        //   - 删除石：RemoveTile 销毁目标格
        //   - 传送石：TeleportPiece 移动选中棋子
        effect.Execute(coord, user);

        // 扣 AP（命中才扣）
        if (APManager.Instance != null) APManager.Instance.ConsumeAP(user, apCost);

        // 清理瞄准视觉：销毁未用的幽灵格（已正式化的受 DestroyGhostTile 保护跳过）+ 清除目标高亮
        ClearTargetingVisuals();

        Debug.Log($"[GridItemManager] {user} 使用 {data?.displayName} 命中 {coord}（消耗 {apCost} AP）");

        // 退出瞄准
        _targeting = false;
        _activeData = null;
        _activeEffect = null;
        _activeFromInventory = false;
        _ghostCoords.Clear();
        _targetCoords.Clear();

        // 恢复选中棋子的行动高亮（含位置变化重算与无高亮时自动取消选中）
        BattleController.Instance?.RefreshSelectionHighlights();
    }

    /// <summary>取消瞄准：背包道具退回背包 + 清理视觉 + 恢复选中高亮（不扣 AP）</summary>
    public void CancelTargeting()
    {
        if (!_targeting) return;

        // 退还：从背包使用的道具退回背包（不消耗）；旧「扣金币购买」来源已随购买即用流程移除
        if (_activeFromInventory && _activeData != null)
            AddToInventory(_activeUser, _activeData);

        ClearTargetingVisuals();

        _targeting = false;
        _activeData = null;
        _activeEffect = null;
        _activeFromInventory = false;
        _ghostCoords.Clear();
        _targetCoords.Clear();

        // 恢复选中棋子的行动高亮
        BattleController.Instance?.RefreshSelectionHighlights();
    }

    /// <summary>清理瞄准视觉：销毁幽灵格（已正式化的保护跳过）+ 清除目标高亮</summary>
    private void ClearTargetingVisuals()
    {
        var board = ChessBoardController.Instance;
        if (board != null)
        {
            // 销毁幽灵格（已正式加入 Model 的会被 DestroyGhostTile 保护跳过）
            foreach (var c in _ghostCoords)
                board.DestroyGhostTile(c);
        }

        // 清除目标高亮（删除石/传送石的高亮在 BattleModel 中）
        BattleController.Instance?.Model?.ClearHighlights();
    }

    // ==========================================
    //  效果工厂（对标 EquipmentManager.CreatePassive / PieceManager.CreateUltimate）
    // ==========================================
    private IGridItemEffect CreateEffect(GridItemData data)
    {
        if (data == null || string.IsNullOrEmpty(data.effectClassName)) return null;
        string json = string.IsNullOrEmpty(data.effectJsonParams) ? "{}" : data.effectJsonParams;
        switch (data.effectClassName)
        {
            case "ExpandTileEffect":
                return new ExpandTileEffect();
            case "RemoveTileEffect":
                return new RemoveTileEffect(data.minTilesAfter);
            case "TeleportUnitEffect":
                var p = JsonUtility.FromJson<TeleportParams>(json);
                return new TeleportUnitEffect(p.maxRange > 0 ? p.maxRange : 5);
            default:
                return null;
        }
    }

    [Serializable]
    private class TeleportParams { public int maxRange = 5; }
}
