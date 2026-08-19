/// <summary>
/// 棋盘道具效果接口 —— 道具的行为契约。
/// 由 GridItemManager 工厂根据 GridItemData.effectClassName 实例化，在瞄准命中目标后调用 Execute。
///
/// target 语义因效果而异（由各实现自定，调用方用 CanExecute 过滤合法目标）：
///   - ExpandTileEffect：target = 待新增的格子坐标（棋盘外、邻接棋盘）。
///   - RemoveTileEffect：target = 待删除的现有格子坐标。
///   - TeleportUnitEffect：target = 传送落点坐标；被传送棋子取自 BattleController.Model.SelectedPiece。
/// </summary>
public interface IGridItemEffect
{
    /// <summary>该目标是否可执行此效果（瞄准前用于过滤可点目标）</summary>
    bool CanExecute(HexCoord target, PlayerSide user);

    /// <summary>执行效果（调用方已通过 CanExecute 校验）</summary>
    void Execute(HexCoord target, PlayerSide user);
}
