using System.Collections.Generic;

/// <summary>
/// 行动点模型（ActionPoint 模块的 Model）—— 纯数据 + 事件，无 Unity 依赖。
/// 职责：持有双方 AP 值，提供查询 / 恢复 / 消耗接口，并在 AP 变化时触发事件。
///
/// 设计要点：
///   1) AP 为累积制（每回合 +baseAPPerTurn，受 maxAPCap 上限），溢出浪费。
///   2) 事件驱动：UI 通过订阅 OnAPChanged 替代每帧轮询。
/// </summary>
public class APModel
{
    // ---- 配置（由 Controller 注入，无默认值；未注入则为 0，便于上层校验暴露）----
    public int BaseAPPerTurn { get; set; }
    public int MaxAPCap { get; set; }

    // ---- 运行时数据 ----
    private readonly Dictionary<PlayerSide, int> _ap = new();

    // ---- 事件 ----
    /// <summary>某方 AP 变化时触发（玩家、新 AP 值）</summary>
    public event System.Action<PlayerSide, int> OnAPChanged;

    // ==========================================
    //  查询
    // ==========================================
    public int GetAP(PlayerSide side) => _ap.TryGetValue(side, out var v) ? v : 0;

    public bool HasAP(PlayerSide side, int amount) => GetAP(side) >= amount;

    // ==========================================
    //  状态变更（由 APManager 调用）
    // ==========================================
    /// <summary>初始化指定玩家的 AP（通常为 0）</summary>
    public void InitAP(PlayerSide side, int value)
    {
        _ap[side] = value;
        OnAPChanged?.Invoke(side, value);
    }

    /// <summary>回合恢复 AP（+baseAPPerTurn，受 maxAPCap 限制）</summary>
    public void RefillAP(PlayerSide side)
    {
        int current = GetAP(side);
        // 使用 System.Math 避免 UnityEngine 依赖
        int newVal = current + BaseAPPerTurn;
        if (newVal > MaxAPCap) newVal = MaxAPCap;
        _ap[side] = newVal;
        OnAPChanged?.Invoke(side, newVal);
    }

    /// <summary>消耗 AP；不足则返回 false</summary>
    public bool TryConsumeAP(PlayerSide side, int amount)
    {
        if (!HasAP(side, amount)) return false;
        _ap[side] -= amount;
        OnAPChanged?.Invoke(side, _ap[side]);
        return true;
    }

    /// <summary>重置（用于重新开局）</summary>
    public void Reset()
    {
        _ap.Clear();
    }
}
