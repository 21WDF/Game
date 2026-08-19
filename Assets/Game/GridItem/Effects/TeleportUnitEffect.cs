using UnityEngine;

/// <summary>
/// 传送石效果 —— 将当前选中棋子传送到 target 位置（瞬移，不走寻路）。
/// target 语义：传送落点坐标（棋盘内、空格、在选中棋子 maxRange 内）。
///
/// 被传送棋子取自 BattleController.Model.SelectedPiece（由玩家购买前选中）。
/// maxRange 由 GridItemData.effectJsonParams {"maxRange":5} 通过工厂构造注入。
/// 注意：传送不消耗 HasAttackedThisTurn / HasMoved 标记（独立于普通移动/攻击），
///       仅消耗道具本身的 apCost（由 GridItemManager 在命中时扣减）。
/// </summary>
public class TeleportUnitEffect : IGridItemEffect
{
    private readonly int _maxRange;

    public TeleportUnitEffect(int maxRange)
    {
        _maxRange = maxRange;
    }

    public bool CanExecute(HexCoord target, PlayerSide user)
    {
        var board = ChessBoardController.Instance;
        if (board == null) return false;
        // target 必须在棋盘内
        if (!board.Contains(target)) return false;
        // target 必须为空格
        if (PieceLayoutModel.Instance != null && PieceLayoutModel.Instance.IsOccupied(target)) return false;
        // 必须有选中的己方棋子
        var selected = BattleController.Instance?.Model?.SelectedPiece;
        if (selected == null || selected.Owner != user) return false;
        // target 不能是选中棋子自身位置
        if (selected.Coord == target) return false;
        // target 必须在 maxRange 内
        if (selected.Coord.Distance(target) > _maxRange) return false;
        return true;
    }

    public void Execute(HexCoord target, PlayerSide user)
    {
        var selected = BattleController.Instance?.Model?.SelectedPiece;
        if (selected == null)
        {
            Debug.LogWarning("[TeleportUnitEffect] 无选中棋子，无法传送");
            return;
        }
        PieceManager.Instance?.TeleportPiece(selected, target);
    }
}
