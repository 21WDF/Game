using System.Collections.Generic;

/// <summary>
/// 金币模型（Gold 模块的 Model）—— 纯数据 + 事件，无 Unity 依赖。
/// 职责：持有双方总金币，提供实时收入记录 / 消费接口，并在金币变化时触发事件。
///
/// 设计要点：
///   1) 伤害产生的金币实时到账（直接加到 _totalGold），无回合末结算。
///   2) 事件驱动：UI 通过订阅 OnGoldSettled 替代每帧轮询；实时收入/消费/初始化均立即触发。
/// </summary>
public class GoldModel
{
    // ---- 配置（由 Controller 注入，无默认值；未注入则为 0，便于上层校验暴露）----
    public float GoldPerDamage { get; set; }
    public float GoldPerDamageReceived { get; set; }
    public int StartingGold { get; set; }
    /// <summary>透支下限（负值，贸易之城·一期）：透支消费允许金币扣到此下限（可为负）；由 GoldManager 从 GameConfig 注入</summary>
    public int OverdraftFloor { get; set; }

    // ---- 运行时数据 ----
    private readonly Dictionary<PlayerSide, int> _totalGold = new();

    // ---- 事件 ----
    /// <summary>金币变化时触发（玩家、新总金币）—— 实时收入/消费/初始化均触发</summary>
    public event System.Action<PlayerSide, int> OnGoldSettled;

    // ==========================================
    //  查询
    // ==========================================
    public int GetGold(PlayerSide side) => _totalGold.TryGetValue(side, out var v) ? v : 0;

    // ==========================================
    //  状态变更（由 GoldManager 调用）
    // ==========================================
    /// <summary>初始化双方起始金币</summary>
    public void InitGold(PlayerSide side, int value)
    {
        _totalGold[side] = value;
        OnGoldSettled?.Invoke(side, value);
    }

    /// <summary>记录伤害造成的金币收入（实时到账，立即触发事件）</summary>
    public void AddGold(PlayerSide side, int gold)
    {
        if (!_totalGold.ContainsKey(side)) _totalGold[side] = 0;
        _totalGold[side] += gold;
        OnGoldSettled?.Invoke(side, _totalGold[side]);
    }

    /// <summary>计算伤害对应的金币（四舍五入）</summary>
    public int CalcGoldFromDamage(int damage, float rate)
    {
        // 手动四舍五入，避免依赖 Mathf.RoundToInt
        return (int)System.Math.Round(damage * rate, System.MidpointRounding.AwayFromZero);
    }

    /// <summary>消费金币；不足则返回 false</summary>
    public bool TrySpendGold(PlayerSide side, int amount)
    {
        if (!_totalGold.ContainsKey(side) || _totalGold[side] < amount) return false;
        _totalGold[side] -= amount;
        OnGoldSettled?.Invoke(side, _totalGold[side]);
        return true;
    }

    /// <summary>透支消费（贸易之城·一期）：允许金币扣为负值（欠钱状态），但不得低于透支下限；
    /// 金币充足时行为与 TrySpendGold 完全一致（透支是「放宽」而非「改变」正常扣款）</summary>
    public bool TrySpendGoldWithOverdraft(PlayerSide side, int amount)
    {
        if (!_totalGold.ContainsKey(side)) _totalGold[side] = 0;
        if (_totalGold[side] - amount < OverdraftFloor) return false;
        _totalGold[side] -= amount;
        OnGoldSettled?.Invoke(side, _totalGold[side]);
        return true;
    }

    /// <summary>重置（用于重新开局）</summary>
    public void Reset()
    {
        _totalGold.Clear();
    }
}
