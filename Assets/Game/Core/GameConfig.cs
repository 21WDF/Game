using UnityEngine;

/// <summary>
/// 全局游戏配置（ScriptableObject）—— 收拢散落在各 Controller Inspector 的调参字段。
/// 加载方式：Resources.Load&lt;GameConfig&gt;("GameConfig")；asset 实例位于 Assets/Game/Resources/GameConfig.asset。
/// 修改配置后无需改代码，直接在 asset 的 Inspector 调整即可。
/// </summary>
[CreateAssetMenu(menuName = "Chess/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("棋盘")]
    [Tooltip("六边形外接圆半径（单位）")]
    public float hexSize = 1.25f;

    [Tooltip("初始棋盘半径（中心到边的六边形圈数）")]
    public int initialRadius = 4;

    [Header("行动点")]
    [Tooltip("每回合恢复的 AP")]
    public int baseAPPerTurn = 2;

    [Tooltip("AP 上限")]
    public int maxAPCap = 4;

    [Header("移动动画")]
    [Tooltip("每格动画时长（秒；0=瞬移无动画）")]
    public float moveDurationPerTile = 0.4f;

    [Tooltip("步数≤此值走 Walk，>此值跑 Run")]
    public int walkRunThreshold = 2;

    [Tooltip("Walk 动画播放速度")]
    public float walkAnimSpeed = 1.0f;

    [Tooltip("Run 动画播放速度")]
    public float runAnimSpeed = 1.0f;

    [Header("棋子定位")]
    [Tooltip("棋子模型 pivot 到脚底的 Y 距离（脚底 pivot 的模型=0，中心 pivot 的模型=高度/2）")]
    public float pieceYOffset = 0f;

    [Header("棋子状态视觉")]
    [Tooltip("已行动棋子的去饱和色（变灰）")]
    public Color actedPieceColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("金币")]
    [Tooltip("每造成 1 点伤害获得的金币")]
    public float goldPerDamage = 1f;

    [Header("护盾染色（元素城邦二期）")]
    [Tooltip("护盾元素染色开关（元素城邦二期占位；未来城邦系统接管此开关）。开启时：有护盾且未染色的棋子被元素附着 → 护盾永久变成对应元素盾（染色）；染色后该元素无法再附着到该棋子（封印），其他元素照常附着；元素反应照常（封印的是附着，不是反应）。关闭时一切等同护盾基座（无染色）。")]
    public bool shieldElementDyeingEnabled = true;

    [Tooltip("每受到 1 点伤害获得的金币")]
    public float goldPerDamageReceived = 0.3f;

    [Tooltip("双方初始金币")]
    public int startingGold = 50;

    [Tooltip("金币透支下限（贸易之城·一期）：所有消费（买装备/买道具/拍卖成交）允许透支到该下限，扣款后金币可为负（欠钱状态）")]
    public int overdraftFloor = -50;

    [Header("贸易之城·拍卖")]
    [Tooltip("拍卖成交倒计时（回合）：出价后每次任一方回合结束倒计时 -1，归零成交。严格按回合计、不按轮计（先手/后手公平性铁律）")]
    public int auctionDealCountdownTurns = 6;

    [Tooltip("拍卖行刷新间隔（回合）：每 N 个回合从物品池补充 1 件新拍品")]
    public int auctionRefreshIntervalTurns = 5;

    [Header("贸易之城·利息与信誉（二期）")]
    [Tooltip("利息结算间隔（金币）：每轮结束每拥有 N 金币获得 1 金币利息（向下取整；金币为负时无利息）")]
    public int interestGoldInterval = 10;

    [Tooltip("欠钱扣信誉计次（回合）：欠钱状态下每持续欠钱满 N 个回合扣一次信誉（每次任一方回合结束计数 +1），扣 floor(欠钱数/利息结算间隔) 点；中途还清则计数重置")]
    public int debtCreditTickTurns = 8;

    [Tooltip("拍卖信誉门槛：信誉低于该值时无法在拍卖行出价（商店购买不受影响）")]
    public int auctionReputationThreshold = 6;

    [Tooltip("商店涨价：每失去 1 点信誉商店购买价格上涨的百分比（信誉 10 = 原价；涨价后价格向下取整；拍卖不受影响）")]
    public int shopPricePercentPerReputation = 10;

    [Header("贸易之城·地下交易（三期）")]
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

    [Header("背包容量")]
    [Tooltip("装备背包容量（列）：商店购买装备前校验，已装备数量达该值后拒绝购买（不扣金币）")]
    public int backpackEquipmentCapacity = 5;

    [Tooltip("道具背包容量（列）：商店购买道具前校验，持有数量达该值后拒绝购买（不扣金币）")]
    public int backpackItemCapacity = 5;

    [Header("战术叠加层颜色")]
    [Tooltip("移动范围高亮（蓝）。Alpha 已含在颜色里，Inspector 直接调透明度即可。")]
    public Color overlayMoveColor = new Color(0.2f, 0.4f, 1f, 0.35f);

    [Tooltip("攻击范围高亮（红）")]
    public Color overlayAttackColor = new Color(1f, 0.2f, 0.2f, 0.25f);

    [Tooltip("敌方棋子所在格高亮（浅红，优先级高于 Attack）")]
    public Color overlayAttackEnemyColor = new Color(1f, 0.4f, 0.4f, 0.35f);

    [Tooltip("途经点高亮（绿）—— 手动路径选择的锁定节点，区别于蓝色移动范围")]
    public Color overlayWaypointColor = new Color(0.2f, 1f, 0.3f, 0.5f);

    [Tooltip("鼠标悬停高亮（浅紫）")]
    public Color overlayHoverColor = new Color(0.7f, 0.5f, 1f, 0.25f);

    [Tooltip("背包拖拽目标格常亮（浅金）—— 拖装备/需指定棋子道具时「可施加棋子」所在格的战术层高亮")]
    public Color overlayDragTargetColor = new Color(1f, 0.85f, 0.3f, 0.4f);

    [Tooltip("雷电残留格常亮（紫电色，季风·雷暴）—— 残留所在格战术层高亮，与方块/移动/攻击高亮可区分")]
    public Color overlayLightningResidueColor = new Color(0.75f, 0.4f, 1f, 0.55f);

    [Header("路径预览")]
    [Tooltip("路径线颜色（默认黄）。配合不同主题可改白/橙等。")]
    public Color pathColor = new Color(1f, 0.9f, 0.2f, 0.6f);

    [Tooltip("路径线宽度（世界单位）")]
    public float pathWidth = 0.15f;

    [Header("浮动文字颜色")]
    [Tooltip("火伤（橙红）")]
    public Color floatDamageFire = new Color(1f, 0.4f, 0.2f);

    [Tooltip("水伤（蓝）")]
    public Color floatDamageWater = new Color(0.2f, 0.5f, 1f);

    [Tooltip("雷伤（黄）")]
    public Color floatDamageThunder = new Color(1f, 0.8f, 0f);

    [Tooltip("冰伤（浅蓝）")]
    public Color floatDamageIce = new Color(0.5f, 0.8f, 1f);

    [Tooltip("物理伤（白）")]
    public Color floatDamagePhysical = Color.white;

    [Tooltip("魔法伤（紫）—— DoT/反伤/超载爆炸等魔法类型跳字")]
    public Color floatDamageMagical = new Color(0.7f, 0.4f, 1f);

    [Tooltip("真实伤（金）—— 真实类型附加伤害等跳字")]
    public Color floatDamageTrue = new Color(1f, 0.85f, 0.3f);

    [Tooltip("反应伤害默认色（紫）")]
    public Color floatReactionDamage = new Color(0.8f, 0.4f, 1f);

    [Tooltip("蒸发（橙）")]
    public Color floatReactionVaporize = new Color(1f, 0.4f, 0.2f);

    [Tooltip("融化（红）")]
    public Color floatReactionMelt = new Color(1f, 0.3f, 0.3f);

    [Tooltip("超载（黄）")]
    public Color floatReactionOverload = new Color(1f, 0.8f, 0f);

    [Tooltip("感电（紫）")]
    public Color floatReactionElectro = new Color(0.6f, 0.4f, 1f);

    [Tooltip("冻结（冰蓝）")]
    public Color floatReactionFrozen = new Color(0.5f, 0.8f, 1f);

    [Tooltip("超导（青）")]
    public Color floatReactionSuperconduct = new Color(0.4f, 0.8f, 1f);

    [Header("浮动文字动画参数")]
    [Tooltip("物理 / 元素伤害动画时长（秒）")]
    public float floatDurationPhysical = 0.8f;

    [Tooltip("物理 / 元素伤害上漂距离（世界单位）")]
    public float floatDistancePhysical = 1.5f;

    [Tooltip("物理 / 元素伤害字号")]
    public int floatFontSizePhysical = 10;

    [Tooltip("反应提示动画时长（秒）")]
    public float floatDurationReaction = 1.0f;

    [Tooltip("反应提示上漂距离（世界单位）")]
    public float floatDistanceReaction = 2.0f;

    [Tooltip("反应提示字号")]
    public int floatFontSizeReaction = 12;

    [Tooltip("回血跳字动画时长（秒）")]
    public float floatDurationHeal = 0.6f;

    [Tooltip("回血跳字上漂距离（世界单位）")]
    public float floatDistanceHeal = 0.8f;

    [Tooltip("回血跳字字号")]
    public int floatFontSizeHeal = 9;

    [Tooltip("护甲跳字动画时长（秒）")]
    public float floatDurationArmor = 0.8f;

    [Tooltip("护甲跳字上漂距离（世界单位）")]
    public float floatDistanceArmor = 0.8f;

    [Tooltip("护甲跳字字号")]
    public int floatFontSizeArmor = 8;

    [Tooltip("暴击跳字动画时长（秒）")]
    public float floatDurationCrit = 1.0f;

    [Tooltip("暴击跳字上漂距离（世界单位）")]
    public float floatDistanceCrit = 2.5f;

    [Tooltip("暴击跳字字号")]
    public int floatFontSizeCrit = 16;

    [Range(0f, 1f)]
    [Tooltip("Alpha 开始衰减的时间比例。0=立即衰减，0.5=前半保持不透明后半淡出，1=全程不衰减")]
    public float floatAlphaFadeStart = 0.5f;
}
