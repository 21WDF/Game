using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 单个棋子数值元素的"引用容器"组件（挂在数值预制体根上）。
///
/// 职责：仅承载 UI 引用与运行时缓存，无任何业务逻辑。
///       布局 / 字号 / 颜色 / 对齐全部在预制体里编辑，改样式无需改代码。
///
/// 由 <see cref="UI_PieceStatsDisplay"/> 的对象池实例化与复用：
///   - 池 Get 时激活 GameObject、调用 <see cref="ResetCache"/> 强制刷新所有字段；
///   - 池 Release 时反激活 GameObject 保留待复用。
///
/// 字段分组（均在预制体中拖入对应子物体）：
///   - 基础数值：attackText / hpText / defText / moveText / rangeText
///   - 能量条：energyBar (+ energyBarFill)
///   - 元素图标：innateElementIcon / affixedElementIcon
///   - 装备图标：equipSlots[3]
///   - 状态图标：statusContainer (+ statusIconPrefab)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UI_StatElement : MonoBehaviour
{
    // ---- 基础数值（现有，保留）----
    [Tooltip("左：攻击力数值文本")]
    public TextMeshProUGUI attackText;
    [Tooltip("右：当前生命值数值文本")]
    public TextMeshProUGUI hpText;

    [Header("新增：属性数值")]
    [Tooltip("防御数值文本")]
    public TextMeshProUGUI defText;
    [Tooltip("移动范围数值文本")]
    public TextMeshProUGUI moveText;
    [Tooltip("攻击范围数值文本")]
    public TextMeshProUGUI rangeText;

    [Header("新增：能量")]
    [Tooltip("能量进度条（无大招棋子隐藏）")]
    public Slider energyBar;
    [Tooltip("能量条 Fill Image（满能变金色）")]
    public Image energyBarFill;

    [Header("新增：元素图标")]
    [Tooltip("所属元素图标")]
    public Image innateElementIcon;
    [Tooltip("当前附着元素图标")]
    public Image affixedElementIcon;
    [Tooltip("附着元素 Gauge 数值文本（显示元素量；无附着时隐藏）")]
    public TextMeshProUGUI affixedElementGaugeText;

    [Header("新增：装备图标")]
    [Tooltip("3 个装备槽位图标（长度=3）")]
    [FormerlySerializedAs("equipIcons")]
    public Image[] equipSlots;

    [Header("新增：状态图标容器")]
    [Tooltip("状态图标容器（HorizontalLayoutGroup，子物体由代码动态创建）")]
    public Transform statusContainer;
    [Tooltip("单个状态图标 Prefab（只含 Image）")]
    public GameObject statusIconPrefab;

    private RectTransform _rect;
    /// <summary>缓存根 RectTransform（池化复用时避免重复 GetComponent）</summary>
    public RectTransform Rect => _rect ??= (RectTransform)transform;

    // ---- 运行时缓存（对象池复用，跨棋子重置；NonSerialized 不进预制体序列化）----
    [System.NonSerialized] public int lastAttack = -1;
    [System.NonSerialized] public int lastHP = -1;
    [System.NonSerialized] public int lastSeenFrame;

    // 新增缓存
    [System.NonSerialized] public int lastDef = -1;
    [System.NonSerialized] public int lastMove = -1;
    [System.NonSerialized] public int lastRange = -1;

    [System.NonSerialized] public int lastEnergy = -1;
    [System.NonSerialized] public bool lastEnergyFull;

    [System.NonSerialized] public ElementType lastInnateElement = (ElementType)(-1);
    [System.NonSerialized] public ElementType lastAffixedElement = (ElementType)(-1);
    [System.NonSerialized] public int lastAffixedGauge = -1;

    [System.NonSerialized] public int lastStatusHash;

    /// <summary>对象池 Get 时调用：重置所有缓存，强制下一帧全量刷新。</summary>
    public void ResetCache()
    {
        lastAttack = -1;
        lastHP = -1;
        lastDef = -1;
        lastMove = -1;
        lastRange = -1;
        lastEnergy = -1;
        lastEnergyFull = false;
        lastInnateElement = (ElementType)(-1);
        lastAffixedElement = (ElementType)(-1);
        lastAffixedGauge = -1;
        lastStatusHash = 0;
    }
}
