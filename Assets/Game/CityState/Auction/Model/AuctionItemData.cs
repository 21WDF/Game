using UnityEngine;

/// <summary>
/// 拍卖物品定义（ScriptableObject）—— 贸易之城拍卖行物品池的条目。
/// 通过 CreateAssetMenu 在 Project 窗口创建：Create > Chess > Auction Item。
///
/// 一期只实际拍卖装备（成交后进买家背包，复用现有背包/装备流程）；
/// gridItem 字段为数据结构预留——道具是「购买即用」消耗品，与延迟成交天然冲突，
/// 交付方式（持有列表/背包 UI/使用入口）是二期独立工作，本期池中会跳过仅配置道具的条目。
/// asset 内容由用户后续自行配置；空池/占位即可跑通流程。
/// </summary>
[CreateAssetMenu(fileName = "AuctionItemData", menuName = "Chess/Auction Item", order = 30)]
public class AuctionItemData : ScriptableObject
{
    [Header("指向物品（一期仅装备生效）")]
    [Tooltip("拍卖的装备（成交后进入买家背包）")]
    public EquipmentData equipment;

    [Tooltip("拍卖的道具（一期不生效：池中校验会跳过仅配置道具的条目，交付方式二期设计）")]
    public GridItemData gridItem;

    [Header("拍卖参数")]
    [Tooltip("底价：首次出价的最低金额")]
    public int startingPrice = 10;

    [Tooltip("最低加价幅度：每次出价必须 ≥ 当前价 + 此值")]
    public int minIncrement = 5;
}
