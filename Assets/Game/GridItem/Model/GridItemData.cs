using UnityEngine;

/// <summary>
/// 棋盘道具定义（ScriptableObject）—— 棋盘道具数据的唯一来源。
/// 通过 CreateAssetMenu 在 Project 窗口创建：Create > Chess > Grid Item。
///
/// 道具为消耗品，采用「背包库存」流程（背包系统一期）：
///   - 购买时扣 price 金币（含信誉涨价，透支版）→ 入背包（不立即使用）。
///   - 从背包使用：移出背包 → 瞄准 → 命中合法目标执行效果 + 扣 apCost AP + 消耗。
///   - 取消或无合法目标：道具退回背包（不扣 AP）。
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
    [Tooltip("道具图标（背包侧栏图标格子显示；asset 由用户配置，缺省时显示占位）")]
    public Sprite icon;

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
    [Tooltip("是否需要先选中己方棋子才能使用（传送石=true，扩展石/删除石=false）。背包侧栏「使用」时校验（未选中己方棋子则拒绝使用）；为 true 时该道具可从侧栏拖拽到己方棋子上（拖到=选中该棋子+使用），false 的道具仅双击使用、不可拖拽")]
    public bool requiresSelectedPiece = false;

    [Header("删除石专用")]
    [Tooltip("防软锁下限：棋盘格子数 > minTilesAfter 才允许删除（仅 RemoveTileEffect 使用）")]
    public int minTilesAfter = 7;
}
