using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [调试工具模块 —— 完全独立可删除]
/// 跳过选棋子 / 部署阶段，直接进入 7v7 对战，并在 Inspector 提供运行时测试参数。
///
/// ★删除零影响★：本文件可随时删除 / 移出项目，不影响任何现有功能。
///   - enableDebugMode = false 时，Start / Update 立即 return，等同脚本不存在。
///   - 删除文件后（场景中残留 Missing Script 引用，非致命），GameFlowController 走正常 选棋子→部署→对战 流程。
///   - 本模块不修改、不继承、不反射任何现有脚本；仅通过 Singleton.Instance + public API + Model 公共成员操作。
///
/// 实现要点（不改现有脚本约束下的方案）：
///   跳过流程：禁用 GameFlowController 组件 + CancelInvoke，阻止其 0.3s 后弹选棋子面板；
///             直接 PieceManager.SpawnPieceById 摆 7v7（内部已 Place + RegisterPiece，无需再调 Place），
///             再 TurnManager.StartFirstTurn()。BattleController 只按 IsDeploying 门控（不检查 CurrentPhase==Playing），
///             故 phase 停在 None 不影响对战。
///   testGold：GoldManager.Model.InitGold(side, value)（Inspector 值变化时调用，非持续锁定，允许正常消费）。
///   infiniteAP：每帧 APManager.Model.InitAP(side, 999)（持续锁 999）。
///   godMode：每帧对己方棋子 PieceModel.SetHP(MaxHP)（SetHP public，clamp 到 [0,MaxHP]）。
///            ⚠局限：单次致命伤害（damage ≥ MaxHP）仍会致死——死亡判定在 PieceManager.AttackPiece 内即时触发，
///            先于本帧 Update 回血；真正"免疫致死"需改 TakeDamage/AttackPiece（被约束禁止）。
///   alwaysFullEnergy：每帧对棋子 EnergyModel.Gain(MaxEnergy)（Gain 封顶满；仅未满时调，避免事件刷屏）。
///   skipElementCheck：已移除——元素反应在 PieceManager.AttackPiece 内由 ElementReactionTable.GetReaction 纯函数计算，
///            无 public 钩子，不改现有脚本无法实现（用户确认去掉）。
/// </summary>
public class DebugManager : MonoBehaviour
{
    public static DebugManager Instance { get; private set; }

    [Header("总开关")]
    [Tooltip("关闭则完全跳过调试逻辑，等同本脚本不存在（走正常 选棋子→部署→对战 流程）")]
    public bool enableDebugMode = true;

    [Header("预设对阵（长度 7；为空或不足 7 自动用 PieceRegistry 前 N 个 id 补全；多于 7 截断）")]
    public int[] p1PieceIds = new int[] { 1, 10, 11, 12, 13, 15, 16 };
    public int[] p2PieceIds = new int[] { 1, 10, 11, 12, 13, 15, 16 };

    [Header("测试参数（运行时修改立即生效）")]
    [Tooltip("P1 金币（值变化时设为此值，非持续锁定）")]
    public int testGoldP1 = 9999;
    [Tooltip("P2 金币（值变化时设为此值，非持续锁定）")]
    public int testGoldP2 = 9999;
    [Tooltip("每帧强制双方 AP = 999")]
    public bool infiniteAP = true;
    [Tooltip("P1 棋子每帧回满血（单次致命伤害仍致死，见类注释局限）")]
    public bool godModeP1 = false;
    [Tooltip("P2 棋子每帧回满血（单次致命伤害仍致死，见类注释局限）")]
    public bool godModeP2 = false;
    [Tooltip("所有棋子每帧回满能量（大招随时放）")]
    public bool alwaysFullEnergy = false;

    // ---- 内部状态 ----
    private bool _setupDone;
    private int _lastGoldP1 = int.MinValue;
    private int _lastGoldP2 = int.MinValue;

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
        if (!enableDebugMode) return;
        // 0.1s 后执行：确保棋盘（ChessBoardController.Start）已生成、各 Manager 已就绪；
        // 且早于 GameFlowController 的 BeginPreMatch(0.3s)，可 CancelInvoke 阻止弹面板。
        Invoke(nameof(SetupDebugMatch), 0.1f);
    }

    private void Update()
    {
        if (!enableDebugMode || !_setupDone) return;

        // ---- 金币：Inspector 值变化时设（非持续锁定，允许正常消费）----
        if (testGoldP1 != _lastGoldP1) { ApplyGold(PlayerSide.P1, testGoldP1); _lastGoldP1 = testGoldP1; }
        if (testGoldP2 != _lastGoldP2) { ApplyGold(PlayerSide.P2, testGoldP2); _lastGoldP2 = testGoldP2; }

        // ---- 无限 AP：持续锁 999 ----
        if (infiniteAP)
        {
            var ap = APManager.Instance != null ? APManager.Instance.Model : null;
            if (ap != null)
            {
                if (ap.GetAP(PlayerSide.P1) != 999) ap.InitAP(PlayerSide.P1, 999);
                if (ap.GetAP(PlayerSide.P2) != 999) ap.InitAP(PlayerSide.P2, 999);
            }
        }

        // ---- 无敌 / 满能：遍历双方棋子 ----
        var turn = TurnManager.Instance != null ? TurnManager.Instance.Model : null;
        if (turn == null) return;

        if (godModeP1) ForceFullHP(turn.GetPieces(PlayerSide.P1));
        if (godModeP2) ForceFullHP(turn.GetPieces(PlayerSide.P2));
        if (alwaysFullEnergy)
        {
            ForceFullEnergy(turn.GetPieces(PlayerSide.P1));
            ForceFullEnergy(turn.GetPieces(PlayerSide.P2));
        }
    }

    // ==========================================
    //  调试对战初始化
    // ==========================================
    private void SetupDebugMatch()
    {
        if (!enableDebugMode || _setupDone) return;
        _setupDone = true;

        // 1) 压制 GameFlowController 自动流程：取消其挂起的 BeginPreMatch(0.3s) 调用并停用组件
        var gfc = GameFlowController.Instance;
        if (gfc != null)
        {
            gfc.CancelInvoke();   // 取消 Invoke(nameof(BeginPreMatch), 0.3f)，阻止弹选棋子面板
            gfc.enabled = false;  // 停用其生命周期（无 Update，主要防止后续逻辑触发）
        }

        // 2) 摆棋：P1 半场 r>=0，P2 半场 r<0
        if (!SpawnSides())
        {
            Debug.LogError("[DebugManager] 摆棋失败，放弃启动对战（请检查 PieceManager.registry / 棋盘是否就绪）");
            return;
        }

        // 3) 应用初始测试金币
        ApplyGold(PlayerSide.P1, testGoldP1);
        ApplyGold(PlayerSide.P2, testGoldP2);
        _lastGoldP1 = testGoldP1;
        _lastGoldP2 = testGoldP2;

        // 4) 启动第一回合（内部补 AP、重置攻击标记、清高亮）
        TurnManager.Instance?.StartFirstTurn();
        Debug.Log("[DebugManager] 调试对战已启动：7v7 已摆好，进入第一回合");
    }

    // ==========================================
    //  摆棋
    // ==========================================
    private bool SpawnSides()
    {
        var pm = PieceManager.Instance;
        var board = ChessBoardController.Instance;
        if (pm == null || board == null) return false;

        var p1Ids = ResolveIds(p1PieceIds);
        var p2Ids = ResolveIds(p2PieceIds);
        if (p1Ids.Count == 0 || p2Ids.Count == 0) return false;

        var p1Coords = CollectHalfCoords(board, p1: true, p1Ids.Count);
        var p2Coords = CollectHalfCoords(board, p1: false, p2Ids.Count);

        for (int i = 0; i < p1Ids.Count && i < p1Coords.Count; i++)
            pm.SpawnPieceById(p1Ids[i], p1Coords[i], PlayerSide.P1);
        for (int i = 0; i < p2Ids.Count && i < p2Coords.Count; i++)
            pm.SpawnPieceById(p2Ids[i], p2Coords[i], PlayerSide.P2);

        return true;
    }

    /// <summary>解析预设 id 数组：不足 7 用 PieceRegistry 前 N 个 id 循环补全；多于 7 截断到 7</summary>
    private List<int> ResolveIds(int[] raw)
    {
        var result = new List<int>();
        if (raw != null)
            foreach (var id in raw) result.Add(id);

        if (result.Count < 7)
        {
            var reg = PieceManager.Instance != null ? PieceManager.Instance.registry : null;
            if (reg != null && reg.pieces != null && reg.pieces.Count > 0)
            {
                int idx = 0, guard = 0;
                while (result.Count < 7 && guard < 100)
                {
                    var pd = reg.pieces[idx % reg.pieces.Count];
                    if (pd != null) result.Add(pd.id);
                    idx++;
                    guard++;
                }
            }
        }

        if (result.Count > 7) result.RemoveRange(7, result.Count - 7);
        return result;
    }

    /// <summary>收集指定半场（P1: r≥0 / P2: r&lt;0）前 count 个未占用坐标</summary>
    private static List<HexCoord> CollectHalfCoords(ChessBoardController board, bool p1, int count)
    {
        var result = new List<HexCoord>();
        var all = board.AllCoords;
        if (all == null) return result;
        foreach (var c in all)
        {
            bool inHalf = p1 ? c.r >= 0 : c.r < 0;
            if (!inHalf) continue;
            if (PieceLayoutModel.Instance != null && PieceLayoutModel.Instance.IsOccupied(c)) continue;
            result.Add(c);
            if (result.Count >= count) break;
        }
        return result;
    }

    // ==========================================
    //  测试参数应用
    // ==========================================
    private static void ApplyGold(PlayerSide side, int value)
    {
        var gm = GoldManager.Instance != null ? GoldManager.Instance.Model : null;
        if (gm != null) gm.InitGold(side, value);
    }

    private static void ForceFullHP(List<PieceModel> pieces)
    {
        if (pieces == null) return;
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p == null || p.IsDead) continue;
            if (p.CurrentHP != p.MaxHP) p.SetHP(p.MaxHP);
        }
    }

    private static void ForceFullEnergy(List<PieceModel> pieces)
    {
        if (pieces == null) return;
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p == null || p.Energy == null) continue;
            if (!p.Energy.IsFull) p.Energy.Gain(p.Energy.MaxEnergy);
        }
    }
}
