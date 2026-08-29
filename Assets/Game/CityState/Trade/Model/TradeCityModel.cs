using System;
using System.Collections.Generic;

/// <summary>
/// 贸易之城经济模型（利息 + 信誉，二期）—— 纯数据 + 事件，无 Unity 依赖。
/// 持有：双方信誉值（默认满 10，下限 0）+ 双方连续欠钱回合计数器。
///
/// 时间单位铁律（本模型不做结算调度，仅存状态；调度与口径由 TradeCityManager 负责）：
///   - 利息、信誉恢复按「轮」（后手方结束行动、一轮完成时结算）；
///   - 欠钱扣信誉的计数按「回合」（每次任一方回合结束计数 +1）。
///
/// 信誉惩罚只有两条（三期前）：商店涨价（每点信誉 +shopPricePercentPerReputation%，拍卖不涨）
/// 和信誉低于门槛禁拍（商店不受影响）。信誉满 10 时一切与一期一致。
/// </summary>
public class TradeCityModel
{
    /// <summary>信誉上限（结构常量：涨价公式 (10 - 信誉) × 10% 与上限绑定，不进配置）</summary>
    public const int MaxReputation = 10;

    // ---- 运行时数据 ----
    private readonly Dictionary<PlayerSide, int> _reputation = new();
    private readonly Dictionary<PlayerSide, int> _debtTurns = new();   // 连续欠钱回合计数（每次任一方回合结束 +1；还清归零）

    // ---- 事件 ----
    /// <summary>某方信誉变化时触发（玩家、新信誉值）—— UI 据此刷新信誉展示与价格</summary>
    public event Action<PlayerSide, int> OnReputationChanged;

    /// <summary>查询某方当前信誉（0 ~ 10）</summary>
    public int GetReputation(PlayerSide side)
        => _reputation.TryGetValue(side, out var v) ? v : MaxReputation;

    /// <summary>查询某方连续欠钱回合计数（欠钱计次用，UI 一般不展示）</summary>
    public int GetDebtTurns(PlayerSide side)
        => _debtTurns.TryGetValue(side, out var v) ? v : 0;

    /// <summary>设置信誉（clamp 0~10；变化时触发事件；相同值不触发）</summary>
    public void SetReputation(PlayerSide side, int value)
    {
        value = Math.Clamp(value, 0, MaxReputation);
        if (GetReputation(side) == value) return;
        _reputation[side] = value;
        OnReputationChanged?.Invoke(side, value);
    }

    /// <summary>信誉增减（delta 可正可负；clamp 0~10 由 SetReputation 保证）</summary>
    public void AddReputation(PlayerSide side, int delta) => SetReputation(side, GetReputation(side) + delta);

    /// <summary>欠钱计数 +1（每次任一方回合结束时，欠钱状态下调用）</summary>
    public void IncrementDebtTurns(PlayerSide side) => _debtTurns[side] = GetDebtTurns(side) + 1;

    /// <summary>重置某方欠钱计数（金币回到 ≥ 0 瞬间调用——「中途还清则计数重置」）</summary>
    public void ResetDebtTurns(PlayerSide side) => _debtTurns[side] = 0;

    /// <summary>重置（重新开局；场景重载时管理器自然重建，此方法备用）</summary>
    public void Reset()
    {
        _reputation.Clear();
        _debtTurns.Clear();
    }
}
