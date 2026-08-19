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

    [Tooltip("每受到 1 点伤害获得的金币")]
    public float goldPerDamageReceived = 0.3f;

    [Tooltip("双方初始金币")]
    public int startingGold = 50;

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
