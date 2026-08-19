using System.Collections.Generic;

/// <summary>
/// 棋子布局模型 —— 棋盘上「坐标 → 棋子」占据关系的唯一权威数据源。
/// 职责：记录哪个格子上放着哪个棋子，提供占据查询 / 放置 / 移除 / 移动接口。
/// 所有模块（ChessBoardController 的寻路阻挡判定、BattleController 的选中/攻击判定等）
/// 都应通过本模型查询占据状态，而不再读取 HexTile.Occupant 或 Unit.CurrentTile。
///
/// Phase 2b：占据实体由 Unit（fat object）迁移为 PieceModel（纯数据），实现渲染与数据的彻底解耦。
/// </summary>
public class PieceLayoutModel
{
    // 懒加载单例：纯数据模型，无需挂载到 GameObject。
    public static PieceLayoutModel Instance { get; } = new PieceLayoutModel();

    private readonly Dictionary<HexCoord, PieceModel> _occupancy = new();

    /// <summary>该坐标上是否有棋子</summary>
    public bool IsOccupied(HexCoord coord) => _occupancy.ContainsKey(coord);

    /// <summary>获取该坐标上的棋子模型；无则返回 null</summary>
    public PieceModel GetPieceAt(HexCoord coord)
    {
        _occupancy.TryGetValue(coord, out var piece);
        return piece;
    }

    /// <summary>在坐标上放置棋子（覆盖已有）</summary>
    public void Place(HexCoord coord, PieceModel piece)
    {
        _occupancy[coord] = piece;
    }

    /// <summary>移除坐标上的棋子（若存在）</summary>
    public void Remove(HexCoord coord)
    {
        _occupancy.Remove(coord);
    }

    /// <summary>将棋子从 from 移动到 to（更新占据表）</summary>
    public void MovePiece(HexCoord from, HexCoord to)
    {
        if (from == to) return;
        if (_occupancy.TryGetValue(from, out var piece))
        {
            _occupancy.Remove(from);
            _occupancy[to] = piece;
        }
    }

    /// <summary>清空所有占据（用于场景重载 / 重新开局）</summary>
    public void Clear() => _occupancy.Clear();
}
