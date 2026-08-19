using UnityEngine;

/// <summary>
/// 删除石效果 —— 移除 target 位置的棋盘格子。
/// target 语义：待删除的现有格子坐标。
///
/// 约束：
///   - target 必须在棋盘内
///   - target 上无棋子占据（不能删有棋子的格子）
///   - 删除后棋盘格子数必须 &gt;= minTilesAfter（防软锁；删除前格子数须 &gt; minTilesAfter）
/// minTilesAfter 由 GridItemData.minTilesAfter 通过工厂构造注入。
/// </summary>
public class RemoveTileEffect : IGridItemEffect
{
    private readonly int _minTilesAfter;

    public RemoveTileEffect(int minTilesAfter)
    {
        _minTilesAfter = minTilesAfter;
    }

    public bool CanExecute(HexCoord target, PlayerSide user)
    {
        var board = ChessBoardController.Instance;
        if (board == null) return false;
        // target 必须在棋盘内
        if (!board.Contains(target)) return false;
        // target 上无棋子占据
        if (PieceLayoutModel.Instance != null && PieceLayoutModel.Instance.IsOccupied(target)) return false;
        // 防软锁：删除前格子数须 > minTilesAfter（删除后 >= minTilesAfter）
        if (board.CoordCount <= _minTilesAfter) return false;
        return true;
    }

    public void Execute(HexCoord target, PlayerSide user)
    {
        var board = ChessBoardController.Instance;
        if (board == null) return;
        if (!board.RemoveTile(target))
        {
            Debug.LogWarning($"[RemoveTileEffect] 删除格子失败 {target}（不存在或被占据）");
        }
    }
}
