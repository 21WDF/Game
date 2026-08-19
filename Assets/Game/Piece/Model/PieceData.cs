using UnityEngine;

/// <summary>
/// 棋子定义（ScriptableObject）—— 棋子数据的唯一来源。
/// 通过 CreateAssetMenu 在 Project 窗口创建：Create > Chess > Piece Data。
/// 硬约束：棋子数据（id、名称、prefab、属性）必须通过 PieceData 定义，并由 PieceRegistry 统一管理。
/// </summary>
[CreateAssetMenu(fileName = "PieceData", menuName = "Chess/Piece Data", order = 0)]
public class PieceData : ScriptableObject
{
    [Header("标识")]
    public int id;                          // 棋子类型 id（注册表查找键）
    public string displayName = "Piece";

    [Header("战斗属性")]
    public int maxHP = 100;
    public int attack = 10;
    public int defense = 5;
    [Tooltip("每回合回蓝（棋子基础值；本方回合结束结算，与装备 bonusEnergyPerTurn 叠加）")]
    public int baseEnergyRegen = 0;
    [Tooltip("每回合回血（棋子基础值；本方回合结束结算，装备回血阶段②再做）")]
    public int baseHPRegen = 0;

    [Header("移动 / 攻击范围（内联可插拔 Provider）")]
    [Tooltip("移动范围内联配置：provider 类型 + 构造参数 JSON + 基础范围（装备加成在 baseRange 上叠加）")]
    public RangeConfig moveConfig = new RangeConfig { providerClassName = "FreeMoveProvider", baseRange = 3 };
    [Tooltip("攻击范围内联配置：provider 类型 + 构造参数 JSON + 基础范围（装备加成在 baseRange 上叠加）")]
    public RangeConfig attackConfig = new RangeConfig { providerClassName = "CircleAttackProvider", baseRange = 1 };

    [Header("渲染")]
    public GameObject prefab;               // 棋子预制体（PieceView 所在 prefab）

    [Header("召唤物")]
    [Tooltip("是否为临时召唤物（如安柏的傀儡）：不计入胜负判定、不可选中/操作、不出现在选人界面；由大招投放、被动驱动（计数/回合/引爆）")]
    public bool isSummon = false;

    [Header("元素")]
    [Tooltip("先天元素（None=无元素）；攻击时作为触发元素参与反应")]
    public ElementType innateElement = ElementType.None;

    [Header("内建被动")]
    [Tooltip("棋子自带被动（创建时由 PassiveFactory 实例化存入 PieceModel.BuiltInPassives）；结构与装备被动一致")]
    public EquipmentData.PassiveConfig[] builtInPassives;

    [Header("大招 / 能量")]
    [Tooltip("大招配置；为空（null）则该棋子无能量系统与大招，Energy=null、不响应 U 键")]
    public UltimateConfig ultimateConfig;

    [Header("蓄力系统（替代能量）")]
    [Tooltip("是否用蓄力系统替代能量（甘雨）：true=无能量条（Energy=null，UI 能量条自动隐藏），普攻不造成伤害改为蓄力+1，大招按蓄力层数释放（≥1 层可放，放完清零）")]
    public bool usesChargeSystem = false;
    [Tooltip("蓄力层数上限（usesChargeSystem=true 时生效；普攻+1 封顶，大招释放清零，不随回合衰减）")]
    public int maxChargeStacks = 3;

    /// <summary>范围配置（内联于 PieceData，非 ScriptableObject）——「provider 类型 + 参数 JSON + 基础范围」三项。
    /// 由 RangeProviderFactory 按 providerClassName 实例化（providerJsonParams 填充非主范围参数）；
    /// baseRange 为基础范围，装备加成（EquipmentMoveRange/AttackRange）在其上叠加后作为 effectiveRange 传入 provider。</summary>
    [System.Serializable]
    public class RangeConfig
    {
        [Tooltip("Provider 类名（移动：FreeMoveProvider/StraightMoveProvider/FlyMoveProvider/StraightPassProvider/ParityMoveProvider；攻击：CircleAttackProvider/MinMaxAttackProvider/AreaAttackProvider/LineAttackProvider/ConeAttackProvider/RingTangentProvider）")]
        public string providerClassName = "";
        [Tooltip("Provider 构造参数 JSON（非主范围参数，如 {\"minRange\":2} / {\"splashRadius\":1} / {\"angle\":60}；无则留空）")]
        [Multiline(6)]
        public string providerJsonParams = "";
        [Tooltip("基础范围（装备加成在此基础上叠加；作为 effectiveRange 传入 provider）")]
        public int baseRange = 1;

        /// <summary>创建移动范围 provider 实例（委托工厂；未知类名返回 null）</summary>
        public IMoveRangeProvider CreateMoveProvider()
        {
            return RangeProviderFactory.CreateMoveProvider(providerClassName, providerJsonParams);
        }

        /// <summary>创建攻击范围 provider 实例（委托工厂；未知类名返回 null）</summary>
        public IAttackRangeProvider CreateAttackProvider()
        {
            return RangeProviderFactory.CreateAttackProvider(providerClassName, providerJsonParams);
        }
    }

    /// <summary>大招配置（通过类名 + JSON 参数描述效果，由 PieceManager 工厂实例化为 IUltimateEffect）</summary>
    [System.Serializable]
    public class UltimateConfig
    {
        [Tooltip("大招名称（用于日志/UI）")]
        public string ultimateName = "";
        [Tooltip("满能所需能量值")]
        public int energyRequired = 100;
        [Tooltip("每次移动获取的能量")]
        public int energyPerMove = 5;
        [Tooltip("每次攻击获取的能量")]
        public int energyPerAttack = 15;
        [Tooltip("是否需要指定敌方目标（true=伤害型走瞄准流程；false=自身增益型直接对自己释放）")]
        public bool requiresTarget = true;
        [Tooltip("目标模式：Enemy=敌方棋子（默认，走 requiresTarget 旧语义）；Self=自身增益；Tile=指定格子（可点空格，效果拿目标格坐标）")]
        public UltimateTargetMode targetMode = UltimateTargetMode.Enemy;
        [Tooltip("Tile 模式瞄准范围：>0 = 以自身为圆心的圆形半径；≤0 = 沿用攻击范围（attackConfig provider 高亮范围）")]
        public int tileTargetRange = 0;
        [Tooltip("Tile 模式高亮形状：Circle=圆形含被占格（默认）；Ray=6 方向射线上的空格（直线冲刺/落点类，被占格不可选）")]
        public UltimateTileShape tileTargetShape = UltimateTileShape.Circle;
        [Tooltip("实现 IUltimateEffect 的类名，如 FireSlashUltimate")]
        public string effectClassName = "";
        [Tooltip("构造参数 JSON，如 {\"bonusDamage\":50}")]
        public string effectJsonParams = "";

        /// <summary>是否指定格模式（Tile）：瞄准时可点空格，效果走三参 Execute 拿目标格坐标</summary>
        public bool IsTileTargeting => targetMode == UltimateTargetMode.Tile;

        /// <summary>是否自身增益型：显式 Self 或旧资产 requiresTarget=false（Enemy 默认值时回落旧语义）</summary>
        public bool IsSelfCast => !IsTileTargeting
            && (targetMode == UltimateTargetMode.Self || !requiresTarget);
    }
}
