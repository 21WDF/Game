using UnityEngine;

/// <summary>地下交易商行物品的结算方式</summary>
public enum UndergroundSettlement
{
    LowPrice,   // 低价金币：象征性低价（欠钱玩家也付得起），走透支扣款
    Debt        // 赊账：购买不花金币，反而把金币扣到更负（加深欠债，金额可配置），复用透支扣款
}

/// <summary>
/// 地下交易商行物品定义（ScriptableObject）—— 贸易之城三期。
/// 通过 CreateAssetMenu 在 Project 窗口创建：Create > Chess > Underground Item。
///
/// 结算方式二选一（不受信誉涨价影响——地下交易是低信誉玩家的补偿通道）：
///   - LowPrice：付 amount 金币（默认配 1~3 的象征性低价）；
///   - Debt：金币扣到更负 amount（加深欠债；不额外扣信誉，之后按二期规则自然触发扣信誉）。
///
/// 装备的「使用代价」（每回合扣血/扣 AP）不在此配置——代价是代价型被动
/// （BloodCostPassive / APCostPassive），配在所指向装备的 passives 里与正面被动共存，
/// jsonParams 由装备 asset 携带。asset 内容由用户后续自行配置；空池可跑通流程。
/// </summary>
[CreateAssetMenu(fileName = "UndergroundItemData", menuName = "Chess/Underground Item", order = 40)]
public class UndergroundItemData : ScriptableObject
{
    [Header("指向物品（商行只卖装备，复用背包）")]
    [Tooltip("商行出售的装备（成交后进入购买方背包）")]
    public EquipmentData equipment;

    [Header("结算方式")]
    [Tooltip("低价金币 = 付象征性低价；赊账 = 金币扣到更负（加深欠债）")]
    public UndergroundSettlement settlement = UndergroundSettlement.LowPrice;

    [Tooltip("金额：LowPrice = 售价（建议 1~3）；Debt = 加深欠债的金额")]
    public int amount = 2;
}
