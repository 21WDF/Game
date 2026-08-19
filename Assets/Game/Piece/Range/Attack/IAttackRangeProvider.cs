using System.Collections.Generic;

/// <summary>
/// 攻击范围提供者接口 —— 可插拔的攻击范围计算契约。
/// 由 PieceData.RangeConfig（内联配置）经 RangeProviderFactory 实例化，在 BattleController 中调用。
///
/// 设计参照 IUltimateEffect / IPassiveEffect 模式（className + JSON 参数 → 工厂创建实例）。
/// effectiveRange 参数由调用方传入（含装备加成），provider 不固化主范围——装备 bonusAttackRange 运行时生效。
///
/// target 语义：
///   - null（高亮阶段）：返回所有可被攻击的格子（方向无关型返回完整范围；方向相关型返回 6 方向并集）
///   - 非 null（执行阶段）：返回实际攻击覆盖的格子（含溅射/射线/扇形扩展）
/// </summary>
public interface IAttackRangeProvider
{
    /// <summary>返回攻击覆盖的坐标列表。
    /// effectiveRange 为含装备加成的有效范围；target 为 null 时返回高亮范围，非 null 时返回实际攻击范围。</summary>
    List<HexCoord> GetAttackZone(HexCoord from, int effectiveRange, HexCoord? target, ChessBoardModel model);
}
