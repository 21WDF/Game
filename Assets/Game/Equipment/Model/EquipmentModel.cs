using System.Collections.Generic;

/// <summary>
/// 装备运行时模型（纯 C# 类）—— 每件装备实例一份。
/// 职责：持有装备定义数据 + 已实例化的被动效果列表。
/// 不执行任何渲染或 Unity 生命周期逻辑；装备/卸下行为由 EquipmentManager 驱动。
/// 参照 PieceModel 的写法（纯数据 + 状态，无 Unity 渲染依赖）。
/// </summary>
public class EquipmentModel
{
    // ---- 定义数据（只读来源）----
    public EquipmentData Data { get; }

    // ---- 运行时被动效果实例 ----
    /// <summary>已实例化的被动效果列表（由 EquipmentManager 工厂创建并注入）</summary>
    public List<IPassiveEffect> ActivePassives { get; } = new List<IPassiveEffect>();

    /// <summary>装备时间戳（EquipmentManager 装备时 ++counter 赋值，卸下清零）。
    /// 仅用于唯一被动「先进先出」的先后判断，不是唯一性标识（唯一性键是 uniqueId）。</summary>
    public int EquipOrder;

    public EquipmentModel(EquipmentData data)
    {
        Data = data;
    }
}
