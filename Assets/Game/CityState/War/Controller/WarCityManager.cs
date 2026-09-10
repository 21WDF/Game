using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战争之城管理器（主将机制）—— 对标 MonsoonManager 的单例管理器。
/// 挂载在场景中的 WarCityManager GameObject 上（可与其他城邦 Manager 放一起）。
///
/// 职责（内聚过滤：仅当前城邦 == 战争之城时生效，判断不散落到调用点）：
///   1. 主将确定：部署阶段每方第一个放置的棋子自动成为主将（GameFlowController 放置后通知）
///   2. 主将加成：生命/防御/攻击/移动四维可配置加成 + 装备实际提供项的额外加成
///      （PieceModel 五个聚合点调用查询；非战争城邦/非主将一律返回 0）
///   3. 调换位置：选中主将时点击移动距离内己方棋子 → 二者互换（瞬移，不走路径、
///      不触发任何路径经过类效果）；消耗 1 AP 并锁定主将本回合不能再移动/调换
///   4. 双判负：主将死亡（斩首）→ 即时判负；除主将外真实棋子全灭（光杆司令）→ 即时判负
///      （PieceManager.DestroyPiece 后通知；复用 TurnManager.Forfeit → OnGameOver 结束流程）
///
/// 配置数值全部从 WarConfig 资产读取；未拖资产时回退 Resources/WarConfig，再回退运行时默认实例。
/// </summary>
public class WarCityManager : MonoBehaviour
{
    public static WarCityManager Instance { get; private set; }

    [Header("配置资产（空则读 Resources/WarConfig，再空则用运行时默认值）")]
    [Tooltip("Create > Chess > War Config 创建后拖入；改数值无需改代码")]
    public WarConfig config;

    private WarConfig _runtimeConfig;

    // ---- 主将状态 ----
    /// <summary>各方主将（部署阶段首子登记；对局内持续，随棋子存续）</summary>
    private readonly Dictionary<PlayerSide, PieceModel> _generals = new Dictionary<PlayerSide, PieceModel>();
    /// <summary>本回合已调换的主将（锁移动：调换后本回合不能再移动/调换；该方下次回合开始时清除）</summary>
    private readonly HashSet<PieceModel> _swapLocked = new HashSet<PieceModel>();

    /// <summary>战争城邦机制生效判定（内聚过滤入口）：当前城邦 == 战争之城</summary>
    private static bool WarActive
        => CityStateManager.Instance != null && CityStateManager.Instance.IsActive(CityStateKind.War);

    /// <summary>生效配置（懒加载：Inspector → Resources → 运行时默认实例）</summary>
    private WarConfig Config
    {
        get
        {
            if (_runtimeConfig == null)
            {
                _runtimeConfig = config != null ? config
                    : (Resources.Load<WarConfig>("WarConfig") ?? ScriptableObject.CreateInstance<WarConfig>());
            }
            return _runtimeConfig;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==========================================
    //  主将确定（GameFlowController 部署放置后通知）
    // ==========================================
    /// <summary>部署放置通知：该方第一个放置的棋子自动成为主将。
    /// 返回 true = 本次放置的棋子成为主将（调用方据此显示提示）；非战争城邦 / 已有主将返回 false</summary>
    public bool OnPieceDeployed(PlayerSide side, PieceModel piece)
    {
        if (!WarActive) return false;
        if (piece == null || piece.Data == null || _generals.ContainsKey(side)) return false;

        _generals[side] = piece;

        // 生命加成同步补满等量当前血量（此时 IsGeneral 已生效 → MaxHP 已含加成，
        // Heal 恰好封顶到新上限；否则「只涨上限」会让主将开局相对不满血）
        int hpBonus = Config.generalHPBonus;
        if (hpBonus > 0) piece.Heal(hpBonus);

        Debug.Log($"[WarCityManager] {(side == PlayerSide.P1 ? "玩家1" : "玩家2")} 主将确定：{piece.Data.displayName}（首个部署的棋子自动成为主将）");
        return true;
    }

    /// <summary>查询某棋子是否主将（非战争城邦恒 false——UI 标记等消费者的统一过滤口径）</summary>
    public bool IsGeneral(PieceModel piece)
        => WarActive && piece != null && _generals.TryGetValue(piece.Owner, out var g) && g == piece;

    /// <summary>查询某方主将（未登记 / 非战争城邦返回 null）</summary>
    public PieceModel GetGeneral(PlayerSide side)
        => _generals.TryGetValue(side, out var g) ? g : null;

    // ==========================================
    //  棋盘扩展（战争之城开局：城邦揭晓确认后、进入选棋子前一次性向外扩 N 环）
    // ==========================================
    /// <summary>本局是否已执行过棋盘扩展尝试（幂等标志：无论结果如何只尝试一次，
    /// 重复走揭晓流程 / 重复调用不会二次扩展）</summary>
    private bool _boardExpanded;

    /// <summary>战争之城·棋盘向外扩展 boardExpansionRings 环（幂等）。
    /// 以现有棋盘为中心补齐外圈，形成半径 +N 的完整六边形；逐格走 ChessBoardController.AddTile
    ///（与扩展石完全同链路：Model 坐标集合与 View 格子视图同步维护，已存在格自动忽略）。
    /// 非战争城邦 / 已扩展过 / 环数 ≤ 0 / 棋盘未就绪时安全跳过（无副作用）</summary>
    public void ExpandBoard()
    {
        if (_boardExpanded) return;
        _boardExpanded = true;   // 先置标志再判定：本局只尝试一次（幂等铁律），后续重复调用零开销返回

        if (!WarActive) return;
        int rings = Config.boardExpansionRings;
        var board = ChessBoardController.Instance;
        if (rings <= 0 || board == null || board.Model == null) return;

        int oldRadius = board.Model.Radius;
        int oldCount = board.CoordCount;
        int newRadius = oldRadius + rings;

        // 棋盘以原点为中心 → AllCoordsInRadius 的偏移即绝对坐标；
        // 内圈已存在格由 AddTile 的 Contains 守卫自动忽略，只新增外两环
        foreach (var coord in HexCoord.AllCoordsInRadius(newRadius))
            board.AddTile(coord);

        // 半径记录同步（外部目前无 Radius 读取方，防御性保持口径一致）
        board.Model.SetRadius(newRadius);
        Debug.Log($"[WarCityManager] 战争之城·棋盘扩展：半径 {oldRadius} → {newRadius}，格子 {oldCount} → {board.CoordCount}");
    }

    // ==========================================
    //  主将加成查询（PieceModel 聚合点调用；非战争城邦/非主将返回 0）
    // ==========================================
    public int GetGeneralHPBonus(PieceModel piece) => IsGeneral(piece) ? Config.generalHPBonus : 0;
    public int GetGeneralDefenseBonus(PieceModel piece) => IsGeneral(piece) ? Config.generalDefenseBonus : 0;
    public int GetGeneralAttackBonus(PieceModel piece) => IsGeneral(piece) ? Config.generalAttackBonus : 0;
    public int GetGeneralMoveBonus(PieceModel piece) => IsGeneral(piece) ? Config.generalMoveBonus : 0;

    /// <summary>主将装备额外加成：主将的装备在某一属性维度的合计值 &gt; 0（装备实际提供了该项）
    /// → 该项额外加 generalEquipStatBonus；未提供该项（合计 0）不享受。攻/防/生命/移动/射程五维同口径</summary>
    public int GetGeneralEquipBonus(PieceModel piece, int equipTotal)
        => equipTotal > 0 && IsGeneral(piece) ? Config.generalEquipStatBonus : 0;

    // ==========================================
    //  调换位置（BattleController 点击拦截调用）
    // ==========================================
    /// <summary>尝试主将调换：选中棋子是主将、目标是己方非召唤棋子、且在主将移动距离内
    ///（六边形距离 ≤ MoveRange——瞬移语义，目标格被棋子占据寻路不可达，故按距离判定）
    /// → 二者互换位置（瞬移，不触发任何路径经过类效果）。成功消耗 1 AP 并锁定主将本回合
    /// 不能再移动/调换（该方下次回合开始解除）。返回 false = 不满足条件（调用方走原有选中/无反应逻辑）</summary>
    public bool TrySwapPosition(PieceModel selected, PieceModel target)
    {
        if (!IsGeneral(selected)) return false;                     // 只有主将能发起调换
        if (target == null || target == selected) return false;
        if (target.Owner != selected.Owner) return false;           // 只与己方棋子调换
        if (target.Data == null || target.Data.isSummon) return false;   // 召唤物（傀儡等）不可调换
        if (_swapLocked.Contains(selected)) return false;           // 本回合已调换过
        if (APManager.Instance == null || !APManager.Instance.HasAP(selected.Owner, 1)) return false;
        if (selected.Coord.Distance(target.Coord) > selected.MoveRange) return false;   // 超出主将移动距离

        APManager.Instance.ConsumeAP(selected.Owner, 1);
        _swapLocked.Add(selected);
        PieceManager.Instance?.SwapPieces(selected, target);
        Debug.Log($"[WarCityManager] 主将调换：{selected.Data.displayName} ↔ {target.Data.displayName}（瞬移不触发路径效果；消耗 1 AP，主将本回合不能再移动）");
        return true;
    }

    /// <summary>棋子移动锁定查询（BattleController.HandleMove 检查）：主将本回合已调换 → 不能再移动</summary>
    public bool IsMoveLocked(PieceModel piece)
        => WarActive && piece != null && _swapLocked.Contains(piece);

    // ==========================================
    //  回合钩子（TurnManager 挂载）
    // ==========================================
    /// <summary>回合开始：解除该方主将的调换锁定（WarActive 判断内聚；非战争城邦也清，防残留）</summary>
    public void OnTurnStarted(PlayerSide activePlayer)
    {
        if (_generals.TryGetValue(activePlayer, out var g) && g != null)
            _swapLocked.Remove(g);
    }

    // ==========================================
    //  双判负（PieceManager.DestroyPiece 后通知）
    // ==========================================
    /// <summary>棋子销毁通知：主将死亡 → 斩首即时判负；非主将真实棋子死亡后该方只剩主将 → 光杆司令即时判负。
    /// 非战争城邦 / 召唤物死亡不触发；Forfeit 幂等（对局已结束直接跳过）</summary>
    public void OnPieceDestroyed(PieceModel piece)
    {
        if (!WarActive || piece == null || piece.Data == null) return;

        // 斩首：主将阵亡 → 该方即时判负（即使还有其他棋子存活）
        if (IsGeneral(piece))
        {
            TurnManager.Instance?.Forfeit(piece.Owner, $"主将 {piece.Data.displayName} 阵亡（斩首）");
            return;
        }

        // 光杆司令：该方真实存活棋子（非召唤）只剩主将 → 即时判负
        if (piece.Data.isSummon) return;
        var pieces = TurnManager.Instance?.Model?.GetPieces(piece.Owner);
        if (pieces == null) return;
        bool hasOtherReal = false;
        foreach (var p in pieces)
        {
            if (p == null || p.IsDead || p.Data == null || p.Data.isSummon) continue;
            hasOtherReal = true;
            break;
        }
        if (!hasOtherReal && _generals.TryGetValue(piece.Owner, out var g) && g != null && !g.IsDead)
            TurnManager.Instance?.Forfeit(piece.Owner, $"主将 {g.Data.displayName} 成为光杆司令（除主将外全灭）");
    }
}
