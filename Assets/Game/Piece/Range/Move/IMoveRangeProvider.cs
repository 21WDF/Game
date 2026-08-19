using System;
using System.Collections.Generic;

/// <summary>
/// 移动范围提供者接口 —— 可插拔的移动范围计算契约。
/// 由 PieceData.RangeConfig（内联配置）经 RangeProviderFactory 实例化，在 PieceModel.GetReachableCoords / BattleController 中调用。
///
/// 设计参照 IUltimateEffect / IPassiveEffect 模式（className + JSON 参数 → 工厂创建实例）。
/// effectiveRange 参数由调用方传入（含装备加成），provider 不固化步数——装备 bonusMoveRange 运行时生效。
/// </summary>
public interface IMoveRangeProvider
{
    /// <summary>返回从 from 出发、effectiveRange 步内可达的坐标列表（不含 from 自身）。
    /// isBlocked 谓词由外部注入（PieceLayoutModel.IsOccupied）；model 提供棋盘边界判定。</summary>
    List<HexCoord> GetReachable(HexCoord from, int effectiveRange, Func<HexCoord, bool> isBlocked, ChessBoardModel model);

    /// <summary>按本移动规则生成 from→to 路径（预览线与实际移动共用同一入口，保证两者一致）。
    /// 默认实现 = BFS 最短路径（棋子为阻挡、终点允许被占），即普通移动的现状行为；
    /// 直线/穿越/飞行/跳跃等规则按需覆写（如穿越不含被占中间格——不逐格占据敌方格子，直接到达终点）。
    /// 不可达返回 null。</summary>
    List<HexCoord> FindPath(HexCoord from, HexCoord to, Func<HexCoord, bool> isBlocked, ChessBoardModel model)
        => model.FindPath(from, to, isBlocked);
}
