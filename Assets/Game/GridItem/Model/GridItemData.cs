using UnityEngine;

/// <summary>
/// 棋盘道具定义（ScriptableObject）—— 棋盘道具数据的唯一来源。
/// 通过 CreateAssetMenu 在 Project 窗口创建：Create > Chess > Grid Item。
///
/// 道具为消耗品，采用"购买即用"流程（无库存）：
///   - 购买时扣 price 金币 → 关闭商店 → 进入瞄准模式。
///   - 命中合法目标：执行效果 + 扣 apCost AP + 消耗。
///   - 取消或无合法目标：退还金币（不扣 AP）。
///
/// 效果通过 effectClassName + effectJsonParams 描述，由 GridItemManager 工厂实例化为 IGridItemEffect。
/// minTilesAfter 仅 RemoveTileEffect 使用（防软锁下限）。
/// </summary>
[CreateAssetMenu(fileName = "GridItemData", menuName = "Chess/Grid Item", order = 20)]
public class GridItemData : ScriptableObject
{
    [Header("标识")]
    public int id;                          // 道具 id
    public string displayName = "Grid Item";

    [Header("经济 / 行动")]
    public int price = 0;                   // 商店售价
    [Tooltip("使用时消耗的 AP（默认 1）")]
    public int apCost = 1;

    [Header("效果")]
    [Tooltip("实现 IGridItemEffect 的类名，如 ExpandTileEffect")]
    public string effectClassName = "";
    [Tooltip("构造参数 JSON，如 {\"maxRange\":5}")]
    public string effectJsonParams = "";

    [Header("使用前置")]
    [Tooltip("是否需要先选中己方棋子才能使用（传送石=true，扩展石/删除石=false）。UI 据此决定购买按钮是否置灰。")]
    public bool requiresSelectedPiece = false;

    [Header("删除石专用")]
    [Tooltip("防软锁下限：棋盘格子数 > minTilesAfter 才允许删除（仅 RemoveTileEffect 使用）")]
    public int minTilesAfter = 7;
}
