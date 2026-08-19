using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 回合控制器（Turn 模块的 Controller）—— 单例 MonoBehaviour，协调 <see cref="TurnModel"/> 与其他系统。
/// 挂载在场景中的 TurnManager GameObject 上。
///
/// Phase 3 MVC 改造：
///   - 纯数据/状态迁移到 <see cref="TurnModel"/>（无 Unity 依赖，可单元测试）。
///   - 本类只负责 Unity 生命周期 + 跨系统协调（补 AP / 清高亮 / 结算金币 / 重置攻击标记）。
///   - UI 通过 <see cref="Model"/> 订阅事件，不再每帧轮询。
///   - 保留旧 API（ActivePlayer/CurrentTurn/IsGameOver/RegisterPiece/UnregisterPiece）作为转发，
///     避免调用方大规模改动；新代码推荐直接访问 Model。
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    // ---- MVC 分层 ----
    private TurnModel _model;
    /// <summary>回合数据模型（UI 通过此订阅事件）</summary>
    public TurnModel Model => _model;

    // ---- 兼容旧 API 的转发属性 ----
    public int CurrentTurn => _model.CurrentTurn;
    public PlayerSide ActivePlayer => _model.ActivePlayer;
    public bool IsGameOver => _model.IsGameOver;

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _model = new TurnModel();
    }

    private void Start()
    {
        // 战前流程由 GameFlowController 接管（选棋子→部署→对战）。
        // StartFirstTurn 在双方部署完成后由 GameFlowController 调用，不再自动启动。
    }

    /// <summary>开始第一回合（public 供 GameFlowController 在部署完成后调用）</summary>
    public void StartFirstTurn()
    {
        _model.StartFirstTurn();
        OnTurnStarted();
    }

    // ==========================================
    //  回合开始（跨系统协调）
    // ==========================================
    private void OnTurnStarted()
    {
        // 补满当前玩家的 AP
        APManager.Instance?.RefillAP(_model.ActivePlayer);

        // 重置当前玩家所有棋子的攻击标记（每回合每棋子仅可攻击一次；移动为纯 AP 限制无需重置）
        var pieces = _model.GetPieces(_model.ActivePlayer);
        foreach (var piece in pieces)
        {
            if (piece != null) piece.HasAttackedThisTurn = false;
        }

        // 元素城邦：遍历双方棋子，递减附着元素持续、重置减防
        TickElementDuration(_model.Player1Pieces);
        TickElementDuration(_model.Player2Pieces);

        // 被动事件通知（阶段②框架）：双方所有棋子的全部被动 OnTurnStart（"每回合"语义 = 每次任一方回合开始都触发）
        NotifyPassivesTurnStart();
        // 临时 buff 回合递减（2.2B）：>0 递减、≤0 移除、<0 永久不递减
        TickTempBuffs();

        // 清除所有高亮
        BattleController.Instance?.DeselectPiece();
        ChessBoardController.Instance?.ClearAllHighlights();

        string playerName = _model.ActivePlayer == PlayerSide.P1 ? "玩家1" : "玩家2";
        Debug.Log($"========== 第 {_model.CurrentRound} 轮 · 第 {_model.CurrentTurn} 回合 · {playerName} 的回合 ==========");
    }

    /// <summary>每回合开始：结算 DoT、递减附着元素持续回合（&lt;=0 清除）、重置防御降低值</summary>
    private void TickElementDuration(IEnumerable<PieceModel> pieces)
    {
        var dotKilled = new List<PieceModel>();
        foreach (var piece in pieces)
        {
            if (piece == null) continue;
            // 超导减防按持续回合递减（③.1）：>0 递减（减防继续生效），≤0 才清除（其余条目 turns=0 时≈旧行为）
            if (piece.DefenseReductionTurnsRemaining > 0)
                piece.DefenseReductionTurnsRemaining--;
            else
                piece.CurrentDefenseReduction = 0;
            // 被动减防递减（爱可菲·冰锋蚀甲）：与超导独立并行，每次任一方回合开始 -1，≤0 清除
            if (piece.PassiveDefenseReductionTurnsRemaining > 0)
            {
                piece.PassiveDefenseReductionTurnsRemaining--;
                if (piece.PassiveDefenseReductionTurnsRemaining <= 0)
                {
                    piece.PassiveDefenseReduction = 0;
                    Debug.Log($"[TurnManager] {piece.Data.displayName} 被动减防结束");
                }
            }
            // 临时防御加成每回合重置（冰障大招等；纳入 EffectiveDefense）
            piece.TemporaryDefenseBonus = 0;
            // 强化状态递减（菲林斯被动）：每次任一方回合开始 -1（同超导减防/元素附着口径），≤0 自然结束
            if (piece.EmpoweredTurnsRemaining > 0)
            {
                piece.EmpoweredTurnsRemaining--;
                if (piece.EmpoweredTurnsRemaining <= 0)
                    Debug.Log($"[TurnManager] {piece.Data.displayName} 强化状态结束");
            }
            // 易伤递减（莫娜大招·星异）：同强化/超导口径，每次任一方回合开始 -1，≤0 清除增伤
            if (piece.VulnerableTurnsRemaining > 0)
            {
                piece.VulnerableTurnsRemaining--;
                if (piece.VulnerableTurnsRemaining <= 0)
                {
                    piece.VulnerablePercent = 0;
                    Debug.Log($"[TurnManager] {piece.Data.displayName} 易伤结束");
                }
            }
            // 已行动变灰标记重置：回合开始双方棋子恢复原色（纯视觉）
            piece.View?.SetActed(false);

            // 感电 DoT 结算：每回合扣血并递减持续
            if (piece.DotTurnsRemaining > 0)
            {
                int dotDmg = piece.DotDamagePerTurn;
                // 统一伤害入口：DoT 也触发 OnDamageReceived（打断再生计时/反甲等；来源=施加者，可能为 null）
                PieceManager.Instance?.ApplyIncomingDamage(piece, dotDmg, piece.DotSource, DamageSource.Dot, DamageKind.Magical);
                piece.DotTurnsRemaining--;
                if (piece.DotTurnsRemaining <= 0)
                    piece.DotDamagePerTurn = 0;
                Debug.Log($"[TurnManager] {piece.Data.displayName} 感电 DoT 造成 {dotDmg} 伤害（剩余 {piece.CurrentHP}，剩余 {Mathf.Max(0, piece.DotTurnsRemaining)} 回合）");
                if (piece.IsDead)
                    dotKilled.Add(piece);
            }

            // 附着元素量递减（每回合 -1，归零清除）
            if (piece.AffixedElement != ElementType.None)
            {
                piece.AffixedElementGauge -= 1;
                if (piece.AffixedElementGauge <= 0)
                    piece.AffixedElement = ElementType.None;
            }
        }
        // DoT 致死的棋子在循环外销毁（避免修改正在迭代的集合）
        foreach (var dead in dotKilled)
        {
            Debug.Log($"[TurnManager] {dead.Data.displayName} 因感电 DoT 死亡");
            PieceManager.Instance?.DestroyPiece(dead);
        }
    }

    /// <summary>回合开始：遍历双方所有棋子，维护未受伤害回合计数（+1）并通知其全部被动 OnTurnStart。
    /// 死亡棋子跳过（DoT 致死者在 TickElementDuration 中已注销）。</summary>
    private void NotifyPassivesTurnStart()
    {
        int notified = NotifyPassivesTurnStartSide(_model.Player1Pieces)
                     + NotifyPassivesTurnStartSide(_model.Player2Pieces);
        if (notified > 0)
            Debug.Log($"[TurnManager] OnTurnStart 通知双方共 {notified} 个被动");
    }

    /// <summary>单方棋子的回合开始被动通知；返回被调用的被动数量（用于验证日志）</summary>
    private int NotifyPassivesTurnStartSide(IEnumerable<PieceModel> pieces)
    {
        int count = 0;
        // 快照遍历：回合开始被动（如死亡之蔑延迟伤害）可能致死并触发 DestroyPiece 修改列表
        foreach (var piece in new List<PieceModel>(pieces))
        {
            if (piece == null || piece.IsDead) continue;
            piece.TurnsSinceDamaged++;  // 未受伤害连续回合计数（受伤害时在 AttackPiece 归零）
            var passives = piece.GetAllPassives();
            for (int i = 0; i < passives.Count; i++)
            {
                passives[i].OnTurnStart(piece);
                count++;
            }
        }
        return count;
    }

    /// <summary>回合结束：递减当前玩家冻结棋子的剩余回合（冻结棋子本回合已无法行动，回合末递减）</summary>
    private void DecrementFreeze(IEnumerable<PieceModel> pieces)
    {
        foreach (var piece in pieces)
        {
            if (piece == null) continue;
            if (piece.FreezeTurnsRemaining > 0)
            {
                piece.FreezeTurnsRemaining--;
                if (piece.FreezeTurnsRemaining <= 0)
                    Debug.Log($"[TurnManager] {piece.Data.displayName} 解冻");
            }
        }
    }

    /// <summary>临时 buff 回合递减（2.2B）：遍历双方棋子，turnsRemaining>0 的 -1、≤0 移除；&lt;0 视为永久不递减</summary>
    private void TickTempBuffs()
    {
        if (_model == null) return;
        TickTempBuffsSide(_model.Player1Pieces);
        TickTempBuffsSide(_model.Player2Pieces);
    }

    private static void TickTempBuffsSide(IEnumerable<PieceModel> pieces)
    {
        foreach (var piece in pieces)
        {
            if (piece == null) continue;
            var buffs = piece.TempBuffs;
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                var buff = buffs[i];
                if (buff.turnsRemaining < 0) continue;   // <0 = 永久 buff
                buff.turnsRemaining--;
                if (buff.turnsRemaining <= 0)
                    buffs.RemoveAt(i);
            }
        }
    }

    /// <summary>回合结束结算：当前行动方棋子回蓝（基础 + 装备 bonusEnergyPerTurn）与回血（基础 baseHPRegen）。
    /// 死亡棋子跳过；Energy 为 null 跳过（Gain 自带 MaxEnergy 上限，Heal 封顶 MaxHP）。</summary>
    private void RegeneratePieces(IEnumerable<PieceModel> pieces)
    {
        foreach (var piece in pieces)
        {
            if (piece == null || piece.IsDead) continue;

            // 回蓝：基础 + 装备
            int energyRegen = piece.Data.baseEnergyRegen;
            foreach (var eq in piece.EquippedItems)
            {
                if (eq != null && eq.Data != null)
                    energyRegen += eq.Data.bonusEnergyPerTurn;
            }
            if (energyRegen > 0)
                piece.Energy?.Gain(energyRegen);

            // 回血：基础（装备回血阶段②）
            int hpRegen = piece.Data.baseHPRegen;
            if (hpRegen > 0)
                piece.Heal(hpRegen);
        }
    }

    // ==========================================
    //  回合结束（由 UI 按钮调用）
    // ==========================================
    public void EndTurn()
    {
        if (_model.IsGameOver) return;

        // 冻结回合递减：当前玩家的冻结棋子在本回合已无法行动，回合末递减剩余回合
        DecrementFreeze(_model.GetPieces(_model.ActivePlayer));

        // 元素反应临时覆盖递减：道具设置的反应覆盖按回合过期
        RuntimeReactionTable.Instance?.TickOverrides();

        // 回蓝/回血（"每回合"语义 = 每次一方行动结束都触发）：双方所有棋子都结算
        //（回蓝 = 基础 baseEnergyRegen + 装备 bonusEnergyPerTurn 之和；回血 = 基础 baseHPRegen，装备回血阶段②）
        // 注："每轮"（双方各行动一次才触发）的回复未来用 CurrentRound 计数，本次不做
        RegeneratePieces(_model.Player1Pieces);
        RegeneratePieces(_model.Player2Pieces);

        // 金币已改为伤害发生时实时到账，无需回合末结算
        // 切换玩家
        _model.SwitchActivePlayer();

        // 检查游戏是否结束
        if (_model.CheckGameOver())
        {
            BattleController.Instance?.DeselectPiece();
            ChessBoardController.Instance?.ClearAllHighlights();
            return;
        }

        // 开始新回合
        _model.AdvanceTurn();
        OnTurnStarted();
    }

    // ==========================================
    //  棋子注册/注销（转发到 Model）
    // ==========================================
    public void RegisterPiece(PieceModel piece) => _model.RegisterPiece(piece);
    public void UnregisterPiece(PieceModel piece) => _model.UnregisterPiece(piece);
}
