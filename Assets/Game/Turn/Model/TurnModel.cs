using System.Collections.Generic;

/// <summary>
/// 回合模型（Turn 模块的 Model）—— 纯数据 + 状态 + 事件，无 Unity 依赖。
/// 职责：持有回合流转的纯数据（当前回合数、当前活动玩家、游戏结束标志、双方棋子列表），
///       提供状态变更方法，并在关键变更时触发事件供 View/Controller 订阅。
///
/// 设计要点：
///   1) 不依赖 UnityEngine，可独立单元测试。
///   2) 跨系统协调（补 AP、清高亮、结算金币）由 TurnManager（Controller）负责，Model 只管自身数据。
///   3) 事件驱动：UI 通过订阅 OnTurnStarted / OnGameOver 替代每帧轮询。
/// </summary>
public class TurnModel
{
    // ---- 运行时状态 ----
    public int CurrentTurn { get; private set; }
    /// <summary>轮次 = 双方各行动一次（P1+P2 各一回合 = 1 轮）；首轮 = 1</summary>
    public int CurrentRound { get; private set; }
    public PlayerSide ActivePlayer { get; private set; } = PlayerSide.P1;
    public bool IsGameOver { get; private set; }

    public readonly List<PieceModel> Player1Pieces = new();
    public readonly List<PieceModel> Player2Pieces = new();

    // ---- 事件 ----
    /// <summary>新回合开始时触发（回合数、活动玩家）</summary>
    public event System.Action<int, PlayerSide> OnTurnStarted;

    /// <summary>游戏结束时触发（获胜方）</summary>
    public event System.Action<PlayerSide> OnGameOver;

    // ==========================================
    //  状态变更（由 TurnManager 调用）
    // ==========================================
    /// <summary>开始第一回合（活动玩家 = P1）</summary>
    public void StartFirstTurn()
    {
        ActivePlayer = PlayerSide.P1;
        CurrentRound = 1;   // 首轮从 1 开始
        AdvanceTurn();
    }

    /// <summary>推进到下一回合（回合数+1，触发 OnTurnStarted）</summary>
    public void AdvanceTurn()
    {
        CurrentTurn++;
        OnTurnStarted?.Invoke(CurrentTurn, ActivePlayer);
    }

    /// <summary>切换活动玩家；切回 P1 表示双方各行动一次，一轮完成，轮次 +1</summary>
    public void SwitchActivePlayer()
    {
        ActivePlayer = ActivePlayer == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;
        if (ActivePlayer == PlayerSide.P1)
            CurrentRound++;
    }

    // ==========================================
    //  棋子注册/注销
    // ==========================================
    public void RegisterPiece(PieceModel piece)
    {
        if (piece == null) return;
        var list = piece.Owner == PlayerSide.P1 ? Player1Pieces : Player2Pieces;
        if (!list.Contains(piece))
            list.Add(piece);
    }

    public void UnregisterPiece(PieceModel piece)
    {
        if (piece == null) return;
        var list = piece.Owner == PlayerSide.P1 ? Player1Pieces : Player2Pieces;
        list.Remove(piece);
    }

    /// <summary>获取指定玩家的棋子列表</summary>
    public List<PieceModel> GetPieces(PlayerSide side)
    {
        return side == PlayerSide.P1 ? Player1Pieces : Player2Pieces;
    }

    // ==========================================
    //  胜负判定
    // ==========================================
    /// <summary>检测游戏是否结束；若结束则设置标志并触发 OnGameOver，返回是否结束</summary>
    public bool CheckGameOver()
    {
        Player1Pieces.RemoveAll(p => p == null);
        Player2Pieces.RemoveAll(p => p == null);

        if (IsGameOver) return true;

        // 胜负只看真实棋子：召唤物（傀儡等 Data.isSummon）不算存活战力，
        // 防止"本方全灭但傀儡未引爆"时对局无法结束
        if (!HasRealPieces(Player1Pieces))
        {
            IsGameOver = true;
            OnGameOver?.Invoke(PlayerSide.P2);
            return true;
        }
        if (!HasRealPieces(Player2Pieces))
        {
            IsGameOver = true;
            OnGameOver?.Invoke(PlayerSide.P1);
            return true;
        }
        return false;
    }

    /// <summary>强制判负入口（战争之城·主将双判负等即时结束场景）：指定方判负、对方获胜。
    /// 幂等（已结束直接跳过）；与 CheckGameOver 共用 IsGameOver/OnGameOver 结束管线</summary>
    public void Forfeit(PlayerSide loser)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        OnGameOver?.Invoke(loser == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1);
    }

    /// <summary>该方是否存在非召唤物的存活棋子（胜负判定口径）</summary>
    private static bool HasRealPieces(List<PieceModel> pieces)
    {
        foreach (var p in pieces)
            if (p != null && p.Data != null && !p.Data.isSummon) return true;
        return false;
    }

    /// <summary>重置模型（用于重新开局）</summary>
    public void Reset()
    {
        CurrentTurn = 0;
        CurrentRound = 0;
        ActivePlayer = PlayerSide.P1;
        IsGameOver = false;
        Player1Pieces.Clear();
        Player2Pieces.Clear();
    }
}
