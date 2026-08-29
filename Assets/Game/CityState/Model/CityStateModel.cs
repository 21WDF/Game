using System;
using System.Collections.Generic;

/// <summary>
/// 城邦数据模型（CityState 模块的 Model）—— 纯数据 + 事件，无 Unity 依赖。
/// 职责：
///   1) 持有本局生效城邦，变化时触发 <see cref="OnCityStateChanged"/>（订阅风格对标 GoldModel.OnGoldSettled）；
///   2) 提供静态纯函数 <see cref="Resolve"/>：输入双方各 4 个心仪城邦 + 一个随机 roll，
///      求重合部分并确定性选出 1 个 —— 不依赖任何 UI、不自己产生随机数
///      （随机 roll 由 Controller 的单一随机入口传入，便于将来在线 PVP 同步 seed）。
///
/// 硬约束：本局城邦状态存在城邦管理器（Model/Manager），不挂在任何 UI 组件上。
/// </summary>
public class CityStateModel
{
    // ---- 运行时数据 ----
    private CityStateKind _current = CityStateKind.None;

    // ---- 事件 ----
    /// <summary>当前城邦变化时触发（旧值、新值）</summary>
    public event Action<CityStateKind, CityStateKind> OnCityStateChanged;

    /// <summary>本局生效城邦（None = 未确定）</summary>
    public CityStateKind Current => _current;

    // ==========================================
    //  状态变更（由 CityStateManager 调用）
    // ==========================================
    /// <summary>唯一写入口：设置本局城邦并触发事件（相同值不重复触发）</summary>
    public void Set(CityStateKind kind)
    {
        if (_current == kind) return;
        var old = _current;
        _current = kind;
        OnCityStateChanged?.Invoke(old, kind);
    }

    /// <summary>重置（用于重新开局）</summary>
    public void Reset() => Set(CityStateKind.None);

    // ==========================================
    //  城邦选择纯函数（可脱离 UI 调用，可单元测试）
    // ==========================================
    /// <summary>求双方心仪城邦的重合部分，并按 roll 确定性选定 1 个：intersection[roll % count]。
    /// 规则：去重（每方选择内部去重）、排除 None（非法选择）、交集按枚举值排序保证确定性；
    /// roll 由调用方传入（随机入口收敛在 CityStateManager）；空交集返回 None。</summary>
    public static CityStateKind Resolve(IReadOnlyList<CityStateKind> p1Choices, IReadOnlyList<CityStateKind> p2Choices, int roll)
    {
        if (p1Choices == null || p2Choices == null || p1Choices.Count == 0 || p2Choices.Count == 0)
            return CityStateKind.None;

        // 求交集（P2 选择建哈希集 → 遍历 P1 选择去重收录）
        var p2Set = new HashSet<CityStateKind>(p2Choices);
        var intersection = new List<CityStateKind>();
        foreach (var choice in p1Choices)
        {
            if (choice == CityStateKind.None) continue;            // None 不是合法选择
            if (!p2Set.Contains(choice)) continue;
            if (intersection.Contains(choice)) continue;           // 去重
            intersection.Add(choice);
        }
        if (intersection.Count == 0) return CityStateKind.None;

        // 按枚举值排序，保证相同输入 + 相同 roll 结果确定（网络同步 seed 时行为一致）
        intersection.Sort();
        int index = roll % intersection.Count;
        if (index < 0) index += intersection.Count;                 // 防御负数 roll
        return intersection[index];
    }
}
