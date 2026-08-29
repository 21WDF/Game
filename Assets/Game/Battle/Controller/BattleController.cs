using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗控制器（Battle 模块的 Controller）—— 单例 MonoBehaviour，协调 <see cref="BattleModel"/> 与其他系统。
/// 挂载在场景中的 BattleController GameObject 上。
///
/// Phase 4 MVC 改造：
///   - 选中状态、高亮集合迁移到 <see cref="BattleModel"/>。
///   - 本类只负责「输入 → 状态判定 → 修改 Model / 调用 PieceManager」，不再直接操作 HexTile 材质。
///   - 高亮渲染由 <see cref="BattleView"/> 订阅 Model 事件完成。
///   - 保留旧 API（DeselectPiece）供 TurnManager 调用。
/// </summary>
public class BattleController : MonoBehaviour
{
    public static BattleController Instance { get; private set; }

    // ---- MVC 分层 ----
    private BattleModel _model;
    /// <summary>战斗数据模型（BattleView 通过此订阅事件）</summary>
    public BattleModel Model => _model;

    // ---- 输入引用 ----
    private InputHandler _input;

    // ---- 大招瞄准状态 ----
    private bool _ultimateTargeting;
    private bool _ultimateTileMode;      // true=指定格瞄准（可点空格）；false=敌方棋子瞄准
    private PieceModel _ultimateCaster;
    private HashSet<HexCoord> _ultimateTargetCoords;

    // ---- 途经点（手动路径选择）----
    // 空列表 = 无途经点，行为与现有系统完全一致（零退化）。
    // 选中棋子后右键蓝色空格追加；右键链中途经点截断；取消选中/执行移动/攻击时清空。
    private readonly List<HexCoord> _waypoints = new List<HexCoord>();

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _model = new BattleModel();
    }

    private void Start()
    {
        _input = FindFirstObjectByType<InputHandler>();
        if (_input == null)
        {
            Debug.LogError("[BattleController] 场景中缺少 InputHandler！");
            enabled = false;
            return;
        }
        _input.OnTileClicked += HandleTileClick;
        _input.OnUltimateToggle += HandleUltimate;
        _input.OnToggleHighlightMode += HandleToggleHighlightMode;
        _input.OnWaypointSet += HandleWaypointSet;
        // 模式切换 → 全量重算行动范围高亮
        _model.OnHighlightModeChanged += HandleHighlightModeChanged;
    }

    private void OnDestroy()
    {
        if (_input != null)
        {
            _input.OnTileClicked -= HandleTileClick;
            _input.OnUltimateToggle -= HandleUltimate;
            _input.OnToggleHighlightMode -= HandleToggleHighlightMode;
            _input.OnWaypointSet -= HandleWaypointSet;
        }
        if (_model != null)
        {
            _model.OnHighlightModeChanged -= HandleHighlightModeChanged;
        }
    }

    // ==========================================
    //  核心点击处理
    // ==========================================
    private void HandleTileClick(HexTile clickedTile)
    {
        if (_model == null) return;
        if (_model.IsPieceAnimating) return;  // 动画期间屏蔽点击

        // 部署阶段（战前流程）：点击委托给 GameFlowController 放置棋子
        if (GameFlowController.Instance != null && GameFlowController.Instance.IsDeploying)
        {
            GameFlowController.Instance.HandleDeployClick(clickedTile);
            return;
        }

        // 棋盘道具瞄准模式：点击优先作为道具目标处理（最高优先级，先于大招瞄准与选中逻辑）
        if (GridItemManager.Instance != null && GridItemManager.Instance.IsTargeting)
        {
            GridItemManager.Instance.HandleTargetClick(clickedTile);
            return;
        }

        // 大招瞄准模式：点击优先作为大招目标处理
        if (_ultimateTargeting)
        {
            HandleUltimateTargetClick(clickedTile);
            return;
        }

        // 占据权威在 PieceLayoutModel
        var piece = PieceLayoutModel.Instance?.GetPieceAt(clickedTile.Coord);

        // ---- 情况 1：点击的是己方棋子 → 选中（召唤物如傀儡不可选中/操作，落入后续分支取消）----
        if (piece != null && piece.Owner == TurnManager.Instance.ActivePlayer
            && !(piece.Data != null && piece.Data.isSummon))
        {
            // 没有 AP 则无法行动，不选中（移动为纯 AP 限制；攻击每回合每棋子一次）
            if (!APManager.Instance.HasAP(piece.Owner, 1)) return;
            // 冻结状态无法行动（冻结反应触发；本回合无法移动/攻击/大招）
            if (piece.IsFrozen)
            {
                Debug.Log($"[BattleController] {piece.Data.displayName} 已冻结，本回合无法行动");
                return;
            }
            SelectPiece(piece);
            return;
        }

        // ---- 情况 2：正在选中棋子，按当前高亮模式处理点击 ----
        if (_model.SelectedPiece != null)
        {
            var selected = _model.SelectedPiece;

            if (_model.CurrentHighlightMode == HighlightMode.Move)
            {
                // 移动模式
                if (piece == null)
                {
                    // 空格：在移动范围内 → 移动；否则落入情况 3 取消选中
                    if (_model.IsHighlighted(clickedTile.Coord))
                    {
                        HandleMove(clickedTile);
                        return;
                    }
                }
                else if (piece.Owner != selected.Owner)
                {
                    // 敌方棋子：在攻击范围内且可攻击 → 攻击；否则无反应（保持选中）
                    // 攻击范围判定用高亮集合中的 AttackEnemy 标记（由 ShowActionHighlights 通过
                    // attackConfig provider 计算）—— 适配所有 provider（Circle/MinMax/Line/Cone/Area），
                    // 比纯距离判定更准确（如 MinMax 近身不可攻击、Line 仅射线方向可攻击）。
                    bool inAttackRange = _model.Highlights.TryGetValue(piece.Coord, out var atkType)
                                         && (atkType & HighlightType.AttackEnemy) != 0;
                    bool canAttack = APManager.Instance.HasAP(selected.Owner, 1)
                                     && CanAttack(selected);
                    if (inAttackRange && canAttack)
                    {
                        HandleAttack(piece);
                        return;
                    }
                    // 不在范围内 / 本回合已攻击 / AP 不足 → 无反应
                    return;
                }
                // 己方棋子已在情况 1 处理
            }
            else // HighlightMode.Attack
            {
                // 攻击模式：仅高亮格可交互
                if (_model.IsHighlighted(clickedTile.Coord))
                {
                    if (piece != null && piece.Owner != selected.Owner)
                    {
                        HandleAttack(piece);
                        return;
                    }
                    // 红色空格 → 无反应（保持选中）
                    return;
                }
                // 非高亮格 → 落入情况 3 取消选中
            }
        }

        // ---- 情况 3：点击空白/非高亮格 → 取消选中 ----
        if (_model.SelectedPiece != null)
        {
            DeselectPiece();
        }
    }

    // ==========================================
    //  选中棋子
    // ==========================================
    private void SelectPiece(PieceModel piece)
    {
        DeselectPiece(); // 先清除旧高亮
        _model.SetSelection(piece);
        Debug.Log($"[BattleController] 选中 {piece.Data.displayName}");

        ShowActionHighlights(piece);
    }

    // ==========================================
    //  计算并显示当前可行动范围（按高亮模式只显示一种范围）
    //  · 移动模式：蓝色移动范围（空格）+ 攻击范围内可攻击敌人浅红标记（不显示攻击范围空格圈）
    //  · 攻击模式：只显示红色攻击范围（敌方格浅红），不显示移动范围
    //  · 移动范围圆心/半径：无途经点→棋子位置+moveRange；有途经点→末途经点+剩余步数
    //  · 途经点（绿）在两种模式下都显示（玩家的路线规划）
    //  · 攻击范围始终以棋子当前位置为圆心（未移动前可攻击的敌人）
    //  · 移动：纯 AP 限制，有 AP 即可多次移动（每步 1 AP）
    //  · 攻击：每回合每棋子仅一次，且需要 AP
    // ==========================================
    private void ShowActionHighlights(PieceModel piece)
    {
        var newHighlights = new Dictionary<HexCoord, HighlightType>();

        bool hasAP = APManager.Instance.HasAP(piece.Owner, 1);

        // 嘲讽查询（安柏·傀儡）：被嘲讽（距敌方傀儡 ≤ 其嘲讽半径）→ 攻击目标高亮只标傀儡。
        // 攻击模式下的红色范围圈仍照常显示（范围可见），但可攻击目标仅傀儡（点击由 HandleAttack 嘲讽校验兜底）
        var taunting = PuppetPassive.FindTaunting(piece);

        // 移动范围圆心与半径：无途经点→棋子位置+moveRange；有途经点→末途经点+剩余步数
        HexCoord moveOrigin = GetMoveRangeOrigin(piece);
        int moveRange = GetRemainingMoveRange(piece);

        if (_model.CurrentHighlightMode == HighlightMode.Move)
        {
            // 蓝色移动范围（通过 moveConfig provider 计算；config 为空时 FreeMove 兜底，行为同旧 BFS）
            if (hasAP && moveRange > 0)
            {
                var reachable = ChessBoardController.Instance.GetReachableCoords(
                    moveOrigin, piece.Data.moveConfig, moveRange);
                foreach (var coord in reachable)
                {
                    // 途经点显示绿色，不作为移动目标（排除避免 Move|Waypoint 冲突）
                    if (_waypoints.Contains(coord)) continue;
                    newHighlights[coord] = HighlightType.Move;
                }
            }

            // 标记攻击范围内可攻击的敌方棋子（浅红），让玩家可见哪些敌人可被攻击。
            // 通过 attackConfig provider 计算（target=null 高亮范围；适配 Circle/MinMax/Line/Cone/Area）。
            // 仅标记敌人脚下，不标记空格 → 不显示红色攻击圈（保持"移动模式不显示红色"）。
            if (hasAP && CanAttack(piece))
            {
                var attackZone = ChessBoardController.Instance.GetAttackZoneCoords(
                    piece.Coord, piece.Data.attackConfig, piece.AttackRange, null);
                foreach (var coord in attackZone)
                {
                    if (coord.Equals(piece.Coord)) continue;
                    var occupant = PieceLayoutModel.Instance?.GetPieceAt(coord);
                    if (occupant != null && occupant.Owner != piece.Owner)
                    {
                        // 嘲讽：只标傀儡格为可攻击目标（其他敌人被锁定不可选）
                        if (taunting != null && !ReferenceEquals(occupant, taunting.Puppet)) continue;
                        newHighlights[coord] = HighlightType.AttackEnemy;
                    }
                }
            }
        }
        else // HighlightMode.Attack
        {
            // 攻击模式：只显示攻击范围（红色 + 敌方格浅红）
            // 通过 attackConfig provider 计算（target=null 高亮范围）
            if (hasAP && CanAttack(piece))
            {
                var attackZone = ChessBoardController.Instance.GetAttackZoneCoords(
                    piece.Coord, piece.Data.attackConfig, piece.AttackRange, null);
                foreach (var coord in attackZone)
                {
                    // 排除棋子自身格（攻击范围不含自身，避免选中棋子脚下变红）
                    if (coord.Equals(piece.Coord)) continue;
                    var occupant = PieceLayoutModel.Instance?.GetPieceAt(coord);
                    if (occupant != null && occupant.Owner != piece.Owner)
                    {
                        // 嘲讽：敌方棋子格只标傀儡（其他敌人浅红取消，红色范围圈不受影响）
                        if (taunting != null && !ReferenceEquals(occupant, taunting.Puppet))
                            newHighlights[coord] = HighlightType.Attack;
                        else
                            newHighlights[coord] = HighlightType.AttackEnemy;   // 敌方棋子格（浅红）
                    }
                    else
                        newHighlights[coord] = HighlightType.Attack;            // 攻击范围普通格（红）
                }
            }
        }

        // 途经点标记（绿色）—— 两种模式下都显示（玩家的路线规划）。
        // 途经点为空格且已从 walkableList 排除，故不会与 Move/Attack 冲突。
        foreach (var wp in _waypoints)
            newHighlights[wp] = HighlightType.Waypoint;

        // 通过 Model 设置高亮（BattleView 订阅事件自动渲染）
        _model.SetHighlights(newHighlights);
    }

    // ==========================================
    //  途经点（手动路径选择）
    //  · 右键蓝色空格 → 追加途经点；右键链中途经点 → 截断
    //  · 移动范围圆心/半径随末途经点动态变化
    //  · 路径预览：起点→途经点1→...→途经点N→悬停格（分段拼接，统一颜色）
    // ==========================================

    /// <summary>移动范围圆心：无途经点→棋子位置；有途经点→最后一个途经点</summary>
    private HexCoord GetMoveRangeOrigin(PieceModel piece) =>
        _waypoints.Count > 0 ? _waypoints[_waypoints.Count - 1] : piece.Coord;

    /// <summary>剩余移动步数：无途经点→piece.MoveRange；有途经点→moveRange 减去起点到末途经点的实际步数。
    /// 步数按棋子移动规则寻路求解（穿越/飞行不因棋子绕行），而非统一 BFS，确保与实际路径一致。</summary>
    private int GetRemainingMoveRange(PieceModel piece)
    {
        if (_waypoints.Count == 0) return piece.MoveRange;

        // 累加起点→各途经点的实际步数（每段按移动规则寻路）
        int usedSteps = 0;
        HexCoord current = piece.Coord;
        foreach (var wp in _waypoints)
        {
            var seg = ChessBoardController.Instance.FindPathByMoveRule(piece.Data.moveConfig, current, wp);
            if (seg == null || seg.Count < 2) return 0;   // 不可达 → 剩余 0
            usedSteps += seg.Count - 1;
            current = wp;
        }
        return Mathf.Max(0, piece.MoveRange - usedSteps);
    }

    /// <summary>获取有效途经点列表（若目标格本身是途经点，截断到该点之前）。
    /// 用于路径计算：左键点击途经点时，只走该点之前的途经点，避免路径环回。</summary>
    private List<HexCoord> GetEffectiveWaypoints(HexCoord target)
    {
        int idx = _waypoints.IndexOf(target);
        if (idx < 0) return _waypoints;                       // 目标不是途经点 → 全部
        if (idx == 0) return new List<HexCoord>();            // 目标是第一个途经点 → 无途经点
        return _waypoints.GetRange(0, idx);                   // 目标是第 idx 个 → 用前 idx 个（0..idx-1）
    }

    /// <summary>右键格子时的处理（由 InputHandler.OnWaypointSet 触发）：
    /// · 已是途经点 → 从该点起截断（该点及之后全部删除）
    /// · 蓝色高亮空格 → 追加为途经点
    /// · 其他情况 → 无反应</summary>
    private void HandleWaypointSet(HexTile tile)
    {
        if (tile == null) return;
        if (_model.SelectedPiece == null) return;
        if (_model.CurrentHighlightMode != HighlightMode.Move) return;
        if (_ultimateTargeting) return;
        if (GridItemManager.Instance != null && GridItemManager.Instance.IsTargeting) return;

        var coord = tile.Coord;

        // 已是途经点 → 截断（该点及之后全部删除）
        int idx = _waypoints.IndexOf(coord);
        if (idx >= 0)
        {
            _waypoints.RemoveRange(idx, _waypoints.Count - idx);
            ShowActionHighlights(_model.SelectedPiece);
            return;
        }

        // 蓝色高亮空格 → 追加为途经点
        // （途经点已在上面处理；AttackEnemy 格有敌人会被空格校验拦截）
        if (_model.Highlights.TryGetValue(coord, out var type)
            && (type & HighlightType.Move) != 0
            && PieceLayoutModel.Instance?.GetPieceAt(coord) == null)
        {
            _waypoints.Add(coord);
            ShowActionHighlights(_model.SelectedPiece);
        }
    }

    /// <summary>计算预览路径（供 InputHandler 路径预览线使用）。
    /// 按棋子移动规则寻路（直线=射线/穿越=穿棋子/飞行=无视阻挡/普通=BFS），与实际移动共用同一入口，保证预览与执行一致。
    /// 无途经点→单段寻路；有途经点→沿途经点拼接。目标格本身是途经点时截断到该点。返回 null 表示不可达或超限。</summary>
    public List<HexCoord> ComputePreviewPath(HexTile hoveredTile)
    {
        if (hoveredTile == null) return null;
        var piece = _model?.SelectedPiece;
        if (piece == null || piece.IsDead) return null;
        if (_model.CurrentHighlightMode != HighlightMode.Move) return null;
        if (!_model.IsHighlighted(hoveredTile.Coord)) return null;

        var effectiveWaypoints = GetEffectiveWaypoints(hoveredTile.Coord);
        if (effectiveWaypoints.Count > 0)
            return ChessBoardController.Instance.FindPathWithWaypointsByMoveRule(
                piece.Data.moveConfig, piece.Coord, effectiveWaypoints, hoveredTile.Coord, piece.MoveRange);

        return ChessBoardController.Instance.FindPathByMoveRule(piece.Data.moveConfig, piece.Coord, hoveredTile.Coord);
    }

    // ==========================================
    //  移动处理
    // ==========================================
    /// <summary>查找棋子身上指定类型的免 AP 被动（不校验冷却；诸葛连弩"持有"判断/充能开关用）</summary>
    private static FreeAPPassive FindFreeAP(PieceModel piece, FreeAPType type)
    {
        foreach (var passive in piece.GetAllPassives())
            if (passive is FreeAPPassive fp && fp.FreeType == type)
                return fp;
        return null;
    }

    /// <summary>棋子当前能否发起攻击：诸葛连弩持有者恒 true（连射，解除"每回合一次"限制）；
    /// 其余棋子 = 未攻击过（原语义）。UI 交互层（点击判定/攻击范围显示）统一走此判断，
    /// 与 HandleAttack 的放行口径一致。</summary>
    private static bool CanAttack(PieceModel piece)
        => FindFreeAP(piece, FreeAPType.Attack) != null || !piece.HasAttackedThisTurn;

    /// <summary>查找棋子身上指定类型且可用的免 AP 被动（FreeAPPassive；未装备/冷却中/类型不符返回 null）</summary>
    private static FreeAPPassive FindUsableFreeAP(PieceModel piece, FreeAPType type)
    {
        foreach (var passive in piece.GetAllPassives())
            if (passive is FreeAPPassive fp && fp.FreeType == type && fp.CanUseFreeAP)
                return fp;
        return null;
    }

    private void HandleMove(HexTile targetTile)
    {
        var piece = _model.SelectedPiece;

        // 季风之城·高温（二期）：已行动（本回合攻击过/移动过被锁定）的棋子不能再移动
        //（null 安全；生效判断内聚在 MonsoonManager，非季风城邦/无高温恒 false）
        if (MonsoonManager.Instance != null && MonsoonManager.Instance.IsMoveBlockedByHeat(piece))
        {
            Debug.Log("[BattleController] 高温锁定：该棋子本回合已行动，不能再移动");
            DeselectPiece();
            return;
        }

        // 探索者护臂：免 AP 移动可用 → 跳过 AP 检查（下方用 MarkUsed 代替 ConsumeAP）
        var freeMove = FindUsableFreeAP(piece, FreeAPType.Move);
        if (freeMove == null)
        {
            // 检查 AP（移动为纯 AP 限制，无每回合次数上限）
            if (!APManager.Instance.HasAP(piece.Owner, 1))
            {
                Debug.Log("[BattleController] AP 不足，无法移动");
                DeselectPiece();
                return;
            }
        }

        // 计算路径：按棋子移动规则寻路（与预览线同一入口，保证一致）；
        // 有途经点时沿途经点拼接；目标本身是途经点时截断到该点
        var effectiveWaypoints = GetEffectiveWaypoints(targetTile.Coord);
        List<HexCoord> path = effectiveWaypoints.Count > 0
            ? ChessBoardController.Instance.FindPathWithWaypointsByMoveRule(
                  piece.Data.moveConfig, piece.Coord, effectiveWaypoints, targetTile.Coord, piece.MoveRange)
            : ChessBoardController.Instance.FindPathByMoveRule(piece.Data.moveConfig, piece.Coord, targetTile.Coord);

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("[BattleController] 找不到路径！");
            return;
        }

        // 验证所有途经点仍为空格（执行时再次校验，防止中途被棋子占据）
        foreach (var wp in effectiveWaypoints)
        {
            if (PieceLayoutModel.Instance?.GetPieceAt(wp) != null)
            {
                Debug.LogWarning("[BattleController] 途经点被占据，无法移动！");
                return;
            }
        }

        // 消耗 AP（或标记护臂冷却）
        if (freeMove != null) freeMove.MarkUsed();
        else APManager.Instance.ConsumeAP(piece.Owner, 1);
        // 执行移动（委托 PieceManager 沿完整路径移动，同步 Model/View/Layout）
        PieceManager.Instance?.MovePieceAlongPath(piece, path, skipEnergy: freeMove != null && !freeMove.GainEnergy);
        // 季风之城·高温（二期）：移动完成后锁定该棋子本回合不能再行动（null 安全纯通知，判断内聚在管理器）
        MonsoonManager.Instance?.OnMoveCompleted(piece);
        // 移动后位置变化，途经点已失效 → 清空
        _waypoints.Clear();

        // 移动后位置变化，重算可行动范围；若已无可行动格子则自动取消选中
        RefreshAfterAction();
    }

    // ==========================================
    //  攻击处理
    // ==========================================
    private void HandleAttack(PieceModel target)
    {
        var piece = _model.SelectedPiece;
        // 嘲讽强制锁定（安柏·傀儡）：攻击时被嘲讽（距敌方傀儡 ≤ 其嘲讽半径）→ 只能攻击傀儡。
        // 放在 AP/次数检查之前：嘲讽拒绝不消耗任何资源（走位离开嘲讽范围即可解除）
        var taunt = PuppetPassive.FindTaunting(piece);
        if (taunt != null && !ReferenceEquals(target, taunt.Puppet))
        {
            Debug.Log($"[BattleController] {piece.Data.displayName} 被傀儡嘲讽，本回合只能攻击 {taunt.Puppet.Data.displayName}");
            return;
        }
        // 攻击每回合每棋子仅一次（诸葛连弩持有者解除限制——连射：可多次攻击，每次消耗 1 AP 或免 AP）
        var repeater = FindFreeAP(piece, FreeAPType.Attack);
        if (piece.HasAttackedThisTurn && repeater == null) return;

        // 诸葛连弩：免 AP 攻击可用 → 跳过 AP 检查（连射次数无限制，AP/免 AP 冷却自限）
        var freeAttack = FindUsableFreeAP(piece, FreeAPType.Attack);
        if (freeAttack == null)
        {
            // 检查 AP
            if (!APManager.Instance.HasAP(piece.Owner, 1))
            {
                Debug.Log("[BattleController] AP 不足，无法攻击");
                DeselectPiece();
                return;
            }
        }

        // 消耗 AP（或标记连弩冷却）
        if (freeAttack != null) freeAttack.MarkUsed();
        else APManager.Instance.ConsumeAP(piece.Owner, 1);

        // ---- 蓄力型普攻（usesChargeSystem=true，甘雨）：不造成伤害，改为蓄力 +1 ----
        // 一次普攻 = 一次 AP（或连弩免 AP）= 一层蓄力（上限 Data.maxChargeStacks）；
        // 不走 AttackPiece / ApplySplashDamage（无伤害/无元素/无伤害类被动事件/无溅射）；
        // 算「已行动」：HasAttackedThisTurn 置位 + 视图变灰（同普通普攻口径）
        if (piece.IsChargeSystem)
        {
            piece.HasAttackedThisTurn = true;
            piece.ChargeStacks++;
            piece.View?.SetActed(true);
            // 蓄力跳字反馈（View/池未就绪时静默跳过）
            if (piece.View != null && FloatingTextPool.Instance != null)
                FloatingTextPool.Instance.Get()?.Show($"蓄力 {piece.ChargeStacks}/{piece.MaxChargeStacks}",
                    piece.View.transform.position + Vector3.up * 1.5f, FloatStyles.ReactionDamage());
            Debug.Log($"[BattleController] {piece.Data.displayName} 普攻蓄力：层数 {piece.ChargeStacks}/{piece.MaxChargeStacks}");
            RefreshAfterAction();
            return;
        }

        // 主目标攻击（委托 PieceManager，含伤害结算/元素反应/金币/死亡处理；
        // 连弩持有者的所有攻击（含免 AP 与耗 AP 连射）由 gainEnergy 控制充能，无连弩默认照常充能）
        PieceManager.Instance?.AttackPiece(piece, target, skipEnergy: repeater != null && !repeater.GainEnergy);

        // 溅射伤害（Area 可配置溅射 / Cone·Line 多目标 provider）：
        // AreaAttackProvider 走可配置溅射（splashRadius/filterByAttackRange/splashIsElemental/splashGoldPercent/公式）；
        // Cone/Line 等其它多目标 provider 保持原逻辑（遍历 attackZone 调 AttackPiece）。
        // 单目标 provider（Circle/MinMax）执行阶段仅返回 [主目标]，故无额外溅射。
        ApplySplashDamage(piece, target);

        // 攻击后若仍有 AP，仍可继续移动；重算范围，无可行动格子则自动取消选中
        RefreshAfterAction();
    }

    /// <summary>范围攻击溅射：
    /// · AreaAttackProvider（且 IsSplash）→ 可配置溅射：取主目标 + splashRadius 格子，按 filterByAttackRange 过滤，
    ///   splashIsElemental=true 走 AttackPiece（元素附着+反应+伤害+死亡；skipEnergy=true 不充能；skipGold=true 跳过内部金币，
    ///   改由手动 AddGold(基础溅射伤害×splashGoldPercent)；有效攻击力=baseDamage+atk×attackPercent 经 overrideAttack 传入，
    ///   使元素溅射也走溅射公式而非裸 EffectiveAttack）；
    ///   splashIsElemental=false 走纯公式 max(1, baseDamage + EffectiveAttack×attackPercent - EffectiveDefense) +
    ///   TakeDamage（不附着元素/反应/能量；splashGoldPercent>0 时手动 AddGold(dmg×splashGoldPercent)；致死时调 DestroyPiece）。
    /// · 其它多目标 provider（Cone/Line）→ 保持原逻辑：遍历 attackZone 调 AttackPiece（不传 skip，默认金币+能量全开）。
    /// · 单目标 provider（Circle/MinMax）执行阶段仅返回主目标，故无溅射。
    /// 主目标已在 HandleAttack 中结算，此处全程跳过。</summary>
    private void ApplySplashDamage(PieceModel attacker, PieceModel mainTarget)
    {
        if (attacker == null || mainTarget == null) return;
        if (ChessBoardController.Instance == null) return;
        if (PieceLayoutModel.Instance == null) return;

        var config = attacker.Data?.attackConfig;
        int effectiveRange = attacker.AttackRange;

        // 创建 provider 判断是否为可配置溅射（AreaAttackProvider）；config 为 null 时 provider=null
        IAttackRangeProvider provider = config != null ? config.CreateAttackProvider() : null;

        if (provider is AreaAttackProvider area && area.IsSplash)
        {
            // ---- Area 可配置溅射 ----
            // 执行阶段 GetAttackZone 返回主目标格 + splashRadius 半径内格子（未过滤）
            var zone = ChessBoardController.Instance.GetAttackZoneCoords(
                attacker.Coord, config, effectiveRange, mainTarget.Coord);

            foreach (var coord in zone)
            {
                if (coord.Equals(mainTarget.Coord)) continue;      // 主目标已结算
                if (coord.Equals(attacker.Coord)) continue;         // 不误伤自己
                // filterByAttackRange=true：溅射格须在攻击方有效攻击范围内（effectiveRange）
                if (area.FilterByAttackRange && attacker.Coord.Distance(coord) > effectiveRange) continue;

                var occupant = PieceLayoutModel.Instance.GetPieceAt(coord);
                if (occupant == null || occupant.Owner == attacker.Owner || occupant.IsDead) continue;

                if (area.SplashIsElemental)
                {
                    // 元素溅射：走 AttackPiece（元素附着+反应+伤害+死亡处理）
                    // skipEnergy=true：溅射始终不充能（仅主目标攻击充能）
                    // skipGold=true：跳过 AttackPiece 内部 OnDamageDealt（其按 goldPerDamage 而非 splashGoldPercent）
                    //   溅射金币改由下方手动按 splashGoldPercent 结算（与溅射伤害同源，数值不再丢失）
                    // overrideAttack：用溅射公式算有效攻击力覆盖 AttackPiece 内部的 attacker.EffectiveAttack
                    //   有效攻击力 = baseDamage + EffectiveAttack × attackPercent（与 splashIsElemental=false 公式同源，减防/反应由 AttackPiece 内部处理）
                    int overrideAttack = Mathf.RoundToInt(area.BaseDamage + attacker.EffectiveAttack * area.AttackPercent);
                    // AttackPiece 前捕获位置 + HP（AttackPiece 可能致死并销毁 View，事后取不到坐标）
                    var splashView = occupant.View;
                    Vector3 splashPos = splashView != null ? splashView.transform.position : Vector3.zero;
                    int hpBefore = occupant.CurrentHP;
                    PieceManager.Instance?.AttackPiece(attacker, occupant, skipGold: true, skipEnergy: true, overrideAttack: overrideAttack, damageSource: DamageSource.Splash);
                    // 浮动跳字（溅射固定物理形态纯色块）；伤害用 HP 前后差（含反应倍率的实际值）
                    int splashDmg = hpBefore - occupant.CurrentHP;
                    if (splashDmg > 0 && splashView != null && FloatingTextPool.Instance != null)
                    {
                        Color ec = ElementColorMapper.GetDamageColor(attacker.Data.innateElement, DamageKind.Physical);
                        FloatingTextPool.Instance.ShowDamage(splashDmg, splashPos, ec, DamageKind.Physical);
                    }
                    // 手动金币结算：用基础溅射伤害（overrideAttack - 目标 EffectiveDefense，不含反应倍率）× splashGoldPercent
                    //   splashGoldPercent=0 时不产生金币调用（与 splashIsElemental=false 分支口径一致）；
                    //   splashDmg>0 = 实际掉血（护盾拦截时 HP 无变化 → splashDmg=0，不给金币；数值不变，仅加拦截判断）
                    if (area.SplashGoldPercent > 0f && splashDmg > 0)
                    {
                        int damage = Mathf.Max(1, overrideAttack - occupant.EffectiveDefense);
                        int gold = Mathf.RoundToInt(damage * area.SplashGoldPercent);
                        GoldManager.Instance?.AddGold(attacker.Owner, gold);
                    }
                }
                else
                {
                    // 纯公式 + TakeDamage（不附着元素、不触发反应、不给能量）
                    // 伤害 = max(1, baseDamage + EffectiveAttack×attackPercent - 目标 EffectiveDefense)
                    // attackPercent=0 即 0%（不贡献攻击力）；按用户确认，非「0 视为完整」
                    float raw = area.BaseDamage + attacker.EffectiveAttack * area.AttackPercent - occupant.EffectiveDefense;
                    int damage = Mathf.Max(1, Mathf.RoundToInt(raw));
                    // 统一伤害入口：非元素溅射也触发 OnDamageReceived（打断再生计时/反甲等）
                    // 返回 false = 被护盾拦截 → 跳过金币（免伤 = 无伤害收益）
                    bool landed = PieceManager.Instance?.ApplyIncomingDamage(occupant, damage, attacker, DamageSource.Splash, DamageKind.Physical) ?? false;

                    // 金币结算（伤害结算后，仅实际造成伤害时）：splashGoldPercent>0 时手动 AddGold（0 时不产生任何金币调用；数值不变，仅加拦截判断）
                    if (landed && area.SplashGoldPercent > 0f)
                    {
                        int gold = Mathf.RoundToInt(damage * area.SplashGoldPercent);
                        GoldManager.Instance?.AddGold(attacker.Owner, gold);
                    }
                    // 浮动跳字（溅射固定物理形态纯色块）；View 此时仍存活（DestroyPiece 在下方死亡检查才调）
                    if (FloatingTextPool.Instance != null && occupant.View != null)
                    {
                        Color ec = ElementColorMapper.GetDamageColor(attacker.Data.innateElement, DamageKind.Physical);
                        FloatingTextPool.Instance.ShowDamage(damage, occupant.View.transform.position, ec, DamageKind.Physical);
                    }
                    Debug.Log($"[BattleController] 溅射：{attacker.Data.displayName} 对 {occupant.Data.displayName} 造成 {damage} 伤害（剩余 {occupant.CurrentHP}）");
                    if (occupant.IsDead)
                        PieceManager.Instance?.DestroyPiece(occupant, attacker);  // 溅射击杀：killer=attacker
                }
            }
        }
        else
        {
            // ---- 非 Area 可配置溅射（Cone/Line 等多目标 provider）：保持原逻辑 ----
            // 单目标 provider（Circle/MinMax）执行阶段仅返回 [主目标]，故无溅射
            var zone = ChessBoardController.Instance.GetAttackZoneCoords(
                attacker.Coord, config, effectiveRange, mainTarget.Coord);

            foreach (var coord in zone)
            {
                if (coord.Equals(mainTarget.Coord)) continue;      // 主目标已结算
                if (coord.Equals(attacker.Coord)) continue;         // 不误伤自己
                var occupant = PieceLayoutModel.Instance.GetPieceAt(coord);
                if (occupant != null && occupant.Owner != attacker.Owner && !occupant.IsDead)
                {
                    PieceManager.Instance?.AttackPiece(attacker, occupant);
                }
            }
        }
    }

    // ==========================================
    //  行动后刷新高亮
    // ==========================================
    private void RefreshAfterAction()
    {
        if (_model.SelectedPiece == null) return;

        // 选中棋子已死亡（攻击触发的反击/连击等场景）→ 直接取消
        if (_model.SelectedPiece.IsDead)
        {
            DeselectPiece();
            return;
        }

        ShowActionHighlights(_model.SelectedPiece);

        // 已无可行动格子（AP 耗尽，或已攻击且无路可走）→ 自动取消选中
        if (_model.Highlights.Count == 0)
        {
            DeselectPiece();
        }
    }

    /// <summary>刷新当前选中棋子的行动范围高亮（供 GridItemManager 道具命中/取消瞄准后恢复显示）。
    /// 复用 RefreshAfterAction 逻辑：含位置变化重算与无高亮时自动取消选中。</summary>
    public void RefreshSelectionHighlights() => RefreshAfterAction();

    // ==========================================
    //  高亮模式切换（R 键）
    // ==========================================
    /// <summary>R 键按下处理：在移动/攻击模式间切换。
    /// 棋盘道具瞄准 / 大招瞄准进行中或无选中棋子时忽略。</summary>
    private void HandleToggleHighlightMode()
    {
        if (GridItemManager.Instance != null && GridItemManager.Instance.IsTargeting) return;
        if (_ultimateTargeting) return;
        if (_model.SelectedPiece == null) return;

        _model.ToggleHighlightMode();
    }

    /// <summary>高亮模式变化时（由 BattleModel.ToggleHighlightMode 触发）：
    /// Attack 模式下隐藏路径预览线，并全量重算当前选中棋子的行动范围高亮。</summary>
    private void HandleHighlightModeChanged(HighlightMode mode)
    {
        // 攻击模式下不显示路径预览线（仅移动模式才显示）
        if (mode == HighlightMode.Attack)
            ChessBoardController.Instance?.HidePathPreview();

        // 全量重算行动范围高亮（非 Show/Hide，是按当前模式重新计算）
        if (_model.SelectedPiece != null && !_ultimateTargeting)
            ShowActionHighlights(_model.SelectedPiece);
    }

    // ==========================================
    //  大招（U 键触发）
    // ==========================================
    private void HandleUltimate()
    {
        // 棋盘道具瞄准模式进行中：忽略大招键，避免干扰（道具取消靠点击非目标格）
        if (GridItemManager.Instance != null && GridItemManager.Instance.IsTargeting) return;

        // 已在瞄准模式时，忽略重复触发（用点击取消或释放）
        if (_ultimateTargeting) return;

        var piece = _model.SelectedPiece;
        if (piece == null)
        {
            Debug.Log("[大招诊断] 按下U时未选中任何棋子");
            return;
        }

        // ---- 傀儡主动引爆（安柏·兔兔伯爵）：选中棋子有活跃傀儡时，再按一次大招键 = 引爆关闭 ----
        // 免费动作：不耗 AP/能量、不需满能（傀儡在场即可关闭）；引爆后傀儡销毁，可重新召唤
        var ownPuppet = PuppetPassive.FindByCaster(piece);
        if (ownPuppet != null)
        {
            ownPuppet.Detonate();
            RefreshAfterAction();   // 傀儡销毁改变占据/局面 → 刷新高亮
            return;
        }

        if (!piece.CanUseUltimate)
        {
            // 静默失败点①：能量未满/蓄力不足（或无大招配置）——此前零反馈，玩家以为大招已进入瞄准
            Debug.Log($"[大招诊断] {piece.Data?.displayName} 无法进入大招瞄准："
                + (piece.IsChargeSystem ? $"蓄力 {piece.ChargeStacks}/{piece.MaxChargeStacks}（需 ≥1）"
                    : piece.Energy == null ? "无大招配置（Energy=null）"
                    : $"能量 {piece.Energy.CurrentEnergy}/{piece.Energy.MaxEnergy} 未满"));
            return;
        }
        if (APManager.Instance == null || !APManager.Instance.HasAP(piece.Owner, 1))
        {
            Debug.Log("[BattleController] AP 不足，无法释放大招");
            return;
        }

        var cfg = piece.Data?.ultimateConfig;
        if (cfg == null)
        {
            Debug.Log("[大招诊断] 选中棋子无 ultimateConfig");
            return;
        }

        // 按下其他操作键(U) → 重置为移动模式（后续 RefreshAfterAction/ShowActionHighlights 将以移动模式重算）
        _model.ResetHighlightMode();

        // 自身增益型大招（targetMode=Self 或旧 requiresTarget=false）：无需目标，直接对自己释放
        if (cfg.IsSelfCast)
        {
            PieceManager.Instance?.UseUltimate(piece, piece);
            RefreshAfterAction();
            return;
        }

        // 指定格模式（targetMode=Tile）：高亮可选格子（含空格），进入格子瞄准
        if (cfg.IsTileTargeting)
        {
            var tiles = GetUltimateTileTargets(piece, cfg);
            if (tiles.Count == 0)
            {
                Debug.Log("[BattleController] 大招瞄准范围内无格子，无法释放");
                ShowActionHighlights(piece);   // 已重置为移动模式 → 刷新为蓝色移动范围
                return;
            }
            EnterUltimateTileTargeting(piece, tiles);
            return;
        }

        // 伤害型大招：在攻击范围内找敌方目标
        var enemies = GetEnemiesInAttackRange(piece);
        if (enemies.Count == 0)
        {
            Debug.Log("[BattleController] 攻击范围内无敌人，无法释放大招");
            ShowActionHighlights(piece);   // 已重置为移动模式 → 刷新为蓝色移动范围
            return;
        }
        if (enemies.Count == 1)
        {
            PieceManager.Instance?.UseUltimate(piece, enemies[0]);
            RefreshAfterAction();
            return;
        }

        // 多敌人：进入瞄准模式（高亮可命中敌人，下一次点击释放/取消）
        EnterUltimateTargeting(piece, enemies);
    }

    /// <summary>获取 piece 攻击范围内的所有敌方棋子（通过 attackConfig provider 计算，target=null 高亮范围）</summary>
    private List<PieceModel> GetEnemiesInAttackRange(PieceModel piece)
    {
        var result = new List<PieceModel>();
        if (ChessBoardController.Instance == null || PieceLayoutModel.Instance == null) return result;
        var zone = ChessBoardController.Instance.GetAttackZoneCoords(
            piece.Coord, piece.Data?.attackConfig, piece.AttackRange, null);
        foreach (var coord in zone)
        {
            if (coord.Equals(piece.Coord)) continue;
            var occupant = PieceLayoutModel.Instance.GetPieceAt(coord);
            if (occupant != null && occupant.Owner != piece.Owner)
                result.Add(occupant);
        }
        return result;
    }

    /// <summary>进入大招瞄准模式：高亮可命中敌人，等待玩家点击</summary>
    private void EnterUltimateTargeting(PieceModel caster, List<PieceModel> enemies)
    {
        _ultimateTargeting = true;
        _ultimateTileMode = false;
        _ultimateCaster = caster;
        _ultimateTargetCoords = new HashSet<HexCoord>();
        var highlights = new Dictionary<HexCoord, HighlightType>();
        foreach (var e in enemies)
        {
            _ultimateTargetCoords.Add(e.Coord);
            highlights[e.Coord] = HighlightType.AttackEnemy;
        }
        _model.SetHighlights(highlights);
        Debug.Log("[BattleController] 大招瞄准模式：点击敌方棋子释放，点击其他位置取消");
    }

    /// <summary>计算 Tile 模式大招的可选目标格（空格与被占据格均可选，由各大招效果自行决定合法性）：
    /// tileTargetRange &gt; 0 → 以自身为圆心的圆形半径（GetTilesInAttackRange 语义，含自身格）；
    /// ≤0 → 沿用攻击范围（attackConfig provider 高亮范围，provider 语义不含自身格）。</summary>
    private List<HexCoord> GetUltimateTileTargets(PieceModel piece, PieceData.UltimateConfig cfg)
    {
        var result = new List<HexCoord>();
        if (ChessBoardController.Instance == null) return result;

        // Ray 形状（直线冲刺/落点类，如雷驰突进）：6 方向射线上的空格（tileTargetRange=射程）。
        // 复用 StraightPassProvider；被占格不可选 → 点击被占格自然落入「非目标格 → 取消」分支，无需额外校验。
        if (cfg.tileTargetShape == UltimateTileShape.Ray)
        {
            System.Func<HexCoord, bool> isBlocked = c => PieceLayoutModel.Instance?.GetPieceAt(c) != null;
            return new StraightPassProvider().GetReachable(
                piece.Coord, cfg.tileTargetRange, isBlocked, ChessBoardController.Instance.Model);
        }

        if (cfg.tileTargetRange > 0)
        {
            foreach (var tile in ChessBoardController.Instance.GetTilesInAttackRange(
                piece.Coord, cfg.tileTargetRange))
                result.Add(tile.Coord);
        }
        else
        {
            result = ChessBoardController.Instance.GetAttackZoneCoords(
                piece.Coord, piece.Data?.attackConfig, piece.AttackRange, null);
        }
        return result;
    }

    /// <summary>进入大招格子瞄准模式（targetMode=Tile）：高亮可选格子（含空格），等待玩家点击。
    /// 敌方棋子格浅红标记（AttackEnemy），其余可选格红色（Attack）。</summary>
    private void EnterUltimateTileTargeting(PieceModel caster, List<HexCoord> tiles)
    {
        _ultimateTargeting = true;
        _ultimateTileMode = true;
        _ultimateCaster = caster;
        _ultimateTargetCoords = new HashSet<HexCoord>(tiles);
        var highlights = new Dictionary<HexCoord, HighlightType>();
        foreach (var coord in tiles)
        {
            var occupant = PieceLayoutModel.Instance?.GetPieceAt(coord);
            highlights[coord] = (occupant != null && occupant.Owner != caster.Owner)
                ? HighlightType.AttackEnemy   // 敌方棋子格（浅红）
                : HighlightType.Attack;        // 可选格子（红，含空格 / 己方格 / 自身格）
        }
        _model.SetHighlights(highlights);
        Debug.Log("[BattleController] 大招格子瞄准模式：点击高亮格子释放（可点空格），点击其他位置取消");
    }

    /// <summary>瞄准模式下处理点击：命中目标则释放，否则取消</summary>
    private void HandleUltimateTargetClick(HexTile tile)
    {
        var caster = _ultimateCaster;
        var targetCoords = _ultimateTargetCoords;
        bool tileMode = _ultimateTileMode;
        ExitUltimateTargeting();

        // 诊断：点击进入时的完整状态（定位点击链路断点）
        Debug.Log($"[大招诊断] 瞄准点击：tile={tile?.Coord}，tileMode={tileMode}，" +
                  $"caster={(caster == null ? "null" : caster.Data?.displayName)}，" +
                  $"能量={(caster?.Energy == null ? "?" : $"{caster.Energy.CurrentEnergy}/{caster.Energy.MaxEnergy}")}，" +
                  $"在目标集内={(targetCoords != null && tile != null && targetCoords.Contains(tile.Coord))}");

        if (tile == null || caster == null || !caster.CanUseUltimate)
        {
            if (caster != null) ShowActionHighlights(caster);  // 恢复行动高亮
            return;
        }

        if (targetCoords != null && targetCoords.Contains(tile.Coord))
        {
            var target = PieceLayoutModel.Instance?.GetPieceAt(tile.Coord);
            if (tileMode)
            {
                // 指定格模式：目标可为空格（target=null），坐标传给效果三参 Execute
                PieceManager.Instance?.UseUltimate(caster, target, tile.Coord);
                RefreshAfterAction();
                return;
            }
            if (target != null)
            {
                PieceManager.Instance?.UseUltimate(caster, target);
                RefreshAfterAction();
                return;
            }
        }

        // 点击非目标格 → 取消
        Debug.Log("[BattleController] 取消大招释放");
        ShowActionHighlights(caster);
    }

    private void ExitUltimateTargeting()
    {
        _ultimateTargeting = false;
        _ultimateTileMode = false;
        _ultimateCaster = null;
        _ultimateTargetCoords = null;
    }

    // ==========================================
    //  取消选中（供 TurnManager / 自身调用）
    // ==========================================
    public void DeselectPiece()
    {
        ExitUltimateTargeting();
        _waypoints.Clear();  // 取消选中 → 清空途经点（路线规划随选中状态一同清除）
        _model?.ClearSelection();
    }
}
