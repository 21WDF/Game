using UnityEngine;

/// <summary>
/// 贸易之城配置（ScriptableObject）—— 贸易机制全部数值（数据驱动，代码零硬编码）。
/// 对标 MonsoonConfig/WarConfig 范式：Create > Chess > Trade Config 创建资产（默认名 TradeConfig），
/// 放到 Assets/Game/Resources/ 下即可被 Resources.Load 回退加载；也可直接拖入各贸易管理器 Inspector。
/// 未拖入且 Resources 无资产时回退运行时默认实例（字段默认值 = 原 GameConfig 中的取值，行为等价）。
/// 修改数值无需改代码，直接在资产 Inspector 调整。
/// </summary>
[CreateAssetMenu(fileName = "TradeConfig", menuName = "Chess/Trade Config", order = 60)]
public class TradeConfig : ScriptableObject
{
    [Header("透支")]
    [Tooltip("金币透支下限（贸易之城·一期）：所有消费（买装备/买道具/拍卖成交）允许透支到该下限，扣款后金币可为负（欠钱状态）")]
    public int overdraftFloor = -50;

    [Header("拍卖")]
    [Tooltip("拍卖成交倒计时（回合）：出价后每次任一方回合结束倒计时 -1，归零成交。严格按回合计、不按轮计（先手/后手公平性铁律）")]
    public int auctionDealCountdownTurns = 6;

    [Tooltip("拍卖行刷新间隔（回合）：每 N 个回合从物品池补充 1 件新拍品")]
    public int auctionRefreshIntervalTurns = 5;

    [Header("利息与信誉（二期）")]
    [Tooltip("利息结算间隔（金币）：每轮结束每拥有 N 金币获得 1 金币利息（向下取整；金币为负时无利息）")]
    public int interestGoldInterval = 10;

    [Tooltip("欠钱扣信誉计次（回合）：欠钱状态下每持续欠钱满 N 个回合扣一次信誉（每次任一方回合结束计数 +1），扣 floor(欠钱数/利息结算间隔) 点；中途还清则计数重置")]
    public int debtCreditTickTurns = 8;

    [Tooltip("拍卖信誉门槛：信誉低于该值时无法在拍卖行出价（商店购买不受影响）")]
    public int auctionReputationThreshold = 6;

    [Tooltip("商店涨价：每失去 1 点信誉商店购买价格上涨的百分比（信誉 10 = 原价；涨价后价格向下取整；拍卖不受影响）")]
    public int shopPricePercentPerReputation = 10;

    [Header("地下交易（三期）")]
    [Tooltip("地下交易激活阈值（累计直接伤害）：某方累计直接伤害 ≥ 该值且信誉 < undergroundReputationThreshold 时事件激活")]
    public int undergroundDamageThreshold = 60;

    [Tooltip("地下交易商行触发基础概率（%）：事件激活后每轮结束掷骰（阈值伤害时 = 该值）")]
    public int undergroundProbBasePercent = 10;

    [Tooltip("地下交易概率步进伤害：累计伤害每超过阈值该值，概率 +undergroundProbStepPercent")]
    public int undergroundProbStepDamage = 30;

    [Tooltip("地下交易概率步进（%）：每步增加的概率（封顶 100%）")]
    public int undergroundProbStepPercent = 20;

    [Tooltip("地下交易信誉门槛：信誉低于该值才可激活/维持地下交易（与拍卖门槛语义独立，可分别调参）")]
    public int undergroundReputationThreshold = 6;
}
