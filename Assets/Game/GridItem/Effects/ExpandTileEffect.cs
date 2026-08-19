using UnityEngine;

/// <summary>
/// 扩展石效果（幽灵格方案）—— 在 target 位置新增一个棋盘格子。
/// target 语义：待新增的格子坐标（棋盘外、邻接棋盘）。
///
/// 瞄准流程：GridItemManager 进入瞄准模式时，遍历棋盘所有格子的 6 方向邻居，
/// 筛选出"不在棋盘内"的坐标作为幽灵格位置（即 CanExecute 为 true 的位置），
/// 玩家点击幽灵格即触发本效果。方向由玩家点击位置决定，directionIndex 参数废弃。
/// </summary>
public class ExpandTileEffect : IGridItemEffect
{
    public bool CanExecute(HexCoord target, PlayerSide user)
    {
        var board = ChessBoardController.Instance;
        if (board == null) return false;
        // target 必须不在棋盘内（新格子位置）
        if (board.Contains(target)) return false;
        // target 的某个邻居必须在棋盘内（邻接棋盘，扩展后仍连通）
        for (int i = 0; i < 6; i++)
        {
            if (board.Contains(target.Neighbor(i))) return true;
        }
        return false;
    }

    public void Execute(HexCoord target, PlayerSide user)
    {
        var board = ChessBoardController.Instance;
        if (board == null) return;
        if (!board.AddTile(target))
        {
            Debug.LogWarning($"[ExpandTileEffect] 新增格子失败 {target}（已存在或棋盘未就绪）");
        }
    }
}
