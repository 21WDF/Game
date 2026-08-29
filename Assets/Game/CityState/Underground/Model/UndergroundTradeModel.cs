using System;
using System.Collections.Generic;

/// <summary>
/// 地下交易数据模型（贸易之城·三期）—— 纯数据 + 事件，无 Unity 依赖。
/// P1/P2 各自独立一套状态（累计伤害 / 事件激活 / 商行开启 / 已购物品），互不共享。
///
/// 时间单位铁律（调度与口径由 UndergroundTradeManager 负责，本模型仅存状态）：
///   - 概率触发按「轮」（后手方结束行动、一轮完成时掷骰）；
///   - 关闭检查按「回合」（每次任一方回合结束查信誉）。
///
/// 状态机（每方独立）：
///   初始（伤害 0，事件未激活）→ 累计直接伤害 ≥ 60 且信誉 < 6 → 事件激活 →
///   每轮按概率开商行 → 商行开启期间可购买（每件限购 1 件）→
///   信誉 ≥ 6 时全重置（清伤害/关商行/关事件，重新累计）。
/// </summary>
public class UndergroundTradeModel
{
    /// <summary>单方地下交易运行时状态</summary>
    public class SideState
    {
        public int AccumulatedDamage;                  // 累计直接伤害（只计 Attack/Splash/Ultimate，护盾挡的不计）
        public bool EventActive;                       // 事件是否激活（≥阈值且信誉<6 时激活）
        public bool ShopOpen;                          // 商行是否开启（激活后每轮按概率开）
        public readonly HashSet<UndergroundItemData> Purchased = new();   // 本次开启期间已购物品（限购 1 件）
    }

    // ---- 运行时数据（每方独立）----
    private readonly Dictionary<PlayerSide, SideState> _sides = new();

    // ---- 事件 ----
    /// <summary>某方地下交易状态变化时触发（伤害累计不触发——高频；激活/开商行/关闭/购买触发）</summary>
    public event Action<PlayerSide> OnStateChanged;

    /// <summary>获取某方状态（懒初始化；返回引用供管理器读写，UI 只读）</summary>
    public SideState GetSide(PlayerSide side)
    {
        if (!_sides.TryGetValue(side, out var state))
        {
            state = new SideState();
            _sides[side] = state;
        }
        return state;
    }

    /// <summary>状态变化通知（由管理器在关键变更后调用）</summary>
    public void RaiseStateChanged(PlayerSide side) => OnStateChanged?.Invoke(side);

    /// <summary>某方全重置（信誉 ≥ 6 时：清伤害/关商行/关事件/恢复限购）</summary>
    public void ResetSide(PlayerSide side)
    {
        var s = GetSide(side);
        s.AccumulatedDamage = 0;
        s.EventActive = false;
        s.ShopOpen = false;
        s.Purchased.Clear();
    }

    /// <summary>整体重置（重新开局备用；场景重载时管理器自然重建）</summary>
    public void Reset()
    {
        _sides.Clear();
    }
}
