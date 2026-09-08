using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 季风之城配置（集中数据驱动）：四季 + 昼夜的效果数值与节奏全部由本资产承载，
/// 运行时由 MonsoonManager 读取。改数值 / 换效果走 Inspector 配置，不改代码。
/// 通过 CreateAssetMenu 在 Project 窗口创建：Create > Chess > Monsoon Config。
///
/// 槽位统一抽象为「效果类型 + 数值」（EffectType + value）：
///   回血（回合开始·该方全体回血）/ 攻击（有效攻击力聚合）/ 金币（回合结束·该方 +value）
///   / 扣血（回合结束·该方全体 -value，可致死）/ 移动（移动范围聚合，含下限 clamp）
/// 四季与昼夜共用同一槽位结构；默认 昼=移动+1、夜=移动-1（配合 minMoveRange 下限 1）。
///
/// 气候（二期·简单组已实现：暴雨/暴雪/高温；飓风/雷暴三期）：climates 列表只承载通用字段
///（名称/概率/限制季节/效果类型/独占），**各气候专属数值参数是 MonsoonConfig 的独立字段区块**
///（暴雨 rainAttackRangePenalty / 暴雪 blizzardKnockbackDistance / 高温无数值）——
/// 三期A重构：拆掉 ClimateEntry.value「一字段多用」，简单气候不再背无用参数。
/// </summary>
[CreateAssetMenu(fileName = "MonsoonConfig", menuName = "Chess/Monsoon Config", order = 50)]
public class MonsoonConfig : ScriptableObject
{
    /// <summary>效果类型（可扩展枚举；一期五类 + None）</summary>
    public enum EffectType
    {
        None = 0,   // 无效果
        Heal = 1,   // 回血：该方回合开始，全体存活棋子回 value 血（封顶 MaxHP）
        Attack = 2, // 攻击：有效攻击力聚合 +value（双方全体，季风全局效果）
        Gold = 3,   // 金币：该方回合结束 +value 金币
        Damage = 4, // 扣血：该方回合结束，全体存活棋子 -value 血（走正常扣血结算，可致死）
        Move = 5    // 移动：移动范围聚合 +value（夜为负；最终值 clamp 到 minMoveRange）
    }

    /// <summary>效果槽位：一个效果类型 + 一个数值（四季 / 昼夜 / 未来气候共用）</summary>
    [System.Serializable]
    public class EffectSlot
    {
        [Tooltip("显示名（日志用）")]
        public string slotName;
        [Tooltip("效果类型")]
        public EffectType effectType;
        [Tooltip("数值（回血/攻击/金币/扣血为正数；移动修正昼 +1 / 夜 -1）")]
        public int value;

        /// <summary>无参构造（Unity 序列化必需：List 元素在 Inspector 里「+」添加时用它创建实例）</summary>
        public EffectSlot() { }

        public EffectSlot(string name, EffectType type, int v)
        {
            slotName = name;
            effectType = type;
            value = v;
        }
    }

    [Header("四季（按轮推进：每 roundsPerSeason 轮一季、循环）")]
    [Tooltip("固定 4 个槽位（春/夏/秋/冬）；顺序即循环顺序")]
    public List<EffectSlot> seasons = new List<EffectSlot>
    {
        new EffectSlot("春", EffectType.Heal, 1),
        new EffectSlot("夏", EffectType.Attack, 2),
        new EffectSlot("秋", EffectType.Gold, 10),
        new EffectSlot("冬", EffectType.Damage, 1)
    };

    [Header("昼夜（与季节对齐：每季前 dayRoundsPerSeason 轮为昼、其余为夜）")]
    [Tooltip("昼槽位（默认移动 +1；可换任意效果类型）")]
    public EffectSlot daySlot = new EffectSlot("昼", EffectType.Move, 1);
    [Tooltip("夜槽位（默认移动 -1；最终移动范围 clamp 到 minMoveRange）")]
    public EffectSlot nightSlot = new EffectSlot("夜", EffectType.Move, -1);
    [Tooltip("移动范围下限（夜 -1 后至少可行动 1 格）")]
    public int minMoveRange = 1;

    [Header("节奏（按轮计）")]
    [Tooltip("每多少轮切换一季")]
    public int roundsPerSeason = 6;
    [Tooltip("每季前多少轮为昼（其余为夜）")]
    public int dayRoundsPerSeason = 3;

    [Header("气候（二期·简单组：暴雨/暴雪/高温；三期B 飓风；三期C 雷暴）")]
    [Tooltip("气候条目列表：每轮开始独立判定概率；专属气候受 restrictedSeasonIndex 限制；可叠加气候可同时生效，独占气候（高温）出现时本轮不再判定其他。效果数值不在条目里——各气候专属参数见下方区块（暴雨/暴雪/飓风/雷暴）")]
    public List<ClimateEntry> climates = new List<ClimateEntry>
    {
        new ClimateEntry("暴雨", 0.05f, -1, ClimateEffectKind.Rain, false),
        new ClimateEntry("暴雪", 0.05f, 3, ClimateEffectKind.Blizzard, false),
        new ClimateEntry("高温", 0.05f, 1, ClimateEffectKind.Heat, true),
        new ClimateEntry("飓风", 0.05f, -1, ClimateEffectKind.Hurricane, false),
        new ClimateEntry("雷暴", 0.05f, -1, ClimateEffectKind.Lightning, false)
    };
    [Tooltip("气候持续轮数下限（出现后随机 1~3 轮）")]
    public int climateDurationMinRounds = 1;
    [Tooltip("气候持续轮数上限")]
    public int climateDurationMaxRounds = 3;

    [Header("暴雨参数")]
    [Tooltip("暴雨：上回合受击的棋子本回合攻击距离减量（下限 1 格）")]
    public int rainAttackRangePenalty = 1;

    [Header("暴雪参数")]
    [Tooltip("暴雪：棋子攻击后，被攻击棋子被击退的格数（复用超载击退）")]
    public int blizzardKnockbackDistance = 1;

    // 高温无专属数值参数（语义=「行动后锁定」，本就不需要数值）

    [Header("飓风参数（三期B·幸运方块）")]
    [Tooltip("飓风激活期间，每轮「掉不掉方块」的概率（0~1；掉则该轮 P1/P2 回合开始各掉一批，两批同品质同数量）")]
    [Range(0f, 1f)] public float hurricaneDropChancePerRound = 0.5f;
    [Tooltip("每批掉落数量下限（每轮在 [下限, 上限] 内随机一个 N，该轮 P1/P2 回合各掉 N 个——两批同品质同数量平衡保底）")]
    public int hurricaneDropCountMin = 2;
    [Tooltip("每批掉落数量上限（低于下限时按下限计）")]
    public int hurricaneDropCountMax = 3;
    [Tooltip("幸运方块 3D 实体 prefab（用户在 Editor 准备的正方体等模型；生成到格子世界坐标，复用棋子定位口径）")]
    public GameObject lootBoxPrefab;
    [Tooltip("方块 prefab pivot 到脚底的 Y 距离（脚底 pivot 的模型 = 0；中心 pivot 的模型（如 Unity 内置 Cube）= 高度一半，使方块正好坐在格面上不悬空不下沉）")]
    public float lootBoxYOffset = 0f;
    [Tooltip("幸运方块价值档位列表（按权重随机；每轮所有方块同一档位。可增删档位）")]
    public List<LootBoxTier> lootBoxTiers = new List<LootBoxTier>
    {
        // 设计档位表（三期B_修正）：超高=高级装备+金币100~150（无城邦装备/道具）；低=纯金币1~6（无装备）
        // 档位材质（品质视觉）在 Inspector 拖入各档 Material（代码默认值不带资产引用）
        new LootBoxTier("超高", 0.05f, true, EquipmentTier.Advanced, 50, -1, 100, 150),
        new LootBoxTier("高",   0.15f, true, EquipmentTier.Intermediate, 50, 9999, 20, 60),
        new LootBoxTier("中",   0.30f, true, EquipmentTier.Basic, 0, 49, 10, 20),
        new LootBoxTier("低",   0.50f, false, EquipmentTier.Basic, 0, -1, 1, 6)
    };

    /// <summary>幸运方块价值档位（每字段可配、代码零硬编码；材质只是档位视觉，拾取前看不到内容）
    /// 档位内容规则：非空来源（城邦装备/普通装备/道具/金币）里随机一种，再在来源里随机具体对象：
    /// 城邦装备 = tier=CityState 且 price ∈ [minCityPrice, maxCityPrice]；普通装备 = includeNormalEquipment 为 true 时按 equipmentTier 筛；
    /// 道具 = price ∈ [minCityPrice, maxCityPrice]；金币 = [goldMin, goldMax] 随机。
    /// 「来源为空」表达：城邦/道具用 maxCityPrice &lt; minCityPrice；普通装备用 includeNormalEquipment=false</summary>
    [System.Serializable]
    public class LootBoxTier
    {
        [Tooltip("档位名（日志用）")]
        public string tierName;
        [Tooltip("该档位方块 3D 实体的材质（品质区分视觉，超高/高/中/低各配一档；未拖时保持 prefab 默认材质）")]
        public Material material;
        [Tooltip("档位概率权重（每轮按权重随机一个档位）")]
        public float weight = 0.25f;
        [Tooltip("是否包含普通装备来源（false = 该档位无普通装备，如低档纯金币）")]
        public bool includeNormalEquipment = true;
        [Tooltip("普通装备 tier（Basic/Intermediate/Advanced；CityState 档走 minCityPrice/maxCityPrice 筛选；includeNormalEquipment=false 时忽略）")]
        public EquipmentTier equipmentTier = EquipmentTier.Basic;
        [Tooltip("城邦装备/道具的价格下限（金档 ≥50）")]
        public int minCityPrice = 0;
        [Tooltip("城邦装备/道具的价格上限（maxCityPrice < minCityPrice 表示城邦装备/道具来源为空，如超高档/低档）")]
        public int maxCityPrice = -1;
        [Tooltip("金币下限")]
        public int goldMin = 1;
        [Tooltip("金币上限")]
        public int goldMax = 6;

        public LootBoxTier() { }

        public LootBoxTier(string name, float w, bool includeEq, EquipmentTier tier, int minP, int maxP, int gMin, int gMax)
        {
            tierName = name; weight = w; includeNormalEquipment = includeEq; equipmentTier = tier;
            minCityPrice = minP; maxCityPrice = maxP; goldMin = gMin; goldMax = gMax;
        }
    }

    [Header("雷暴参数（三期C·雷电残留 + 雷劈永久加成）")]
    [Tooltip("雷暴出现时雷劈的格子数量下限")]
    public int lightningStrikeMinCount = 2;
    [Tooltip("雷暴出现时雷劈的格子数量上限")]
    public int lightningStrikeMaxCount = 4;
    [Tooltip("劈中棋子的真实伤害（无视防御/护盾、不附着元素、环境伤害口径）")]
    public int lightningStrikeDamage = 20;
    [Tooltip("雷电残留初始强度（位于/经过该格受当前强度真实伤害）")]
    public int lightningResidueInitialPower = 8;
    [Tooltip("雷电残留格的 3D 材质（替换原半透明高亮：雷劈留残留的格子本体切此材质，归零还原原材质。未配置时无视觉显示，数据层照常生效）")]
    public Material lightningResidueMaterial;
    [Tooltip("残留强度每轮衰减量（按「轮」计；归零消失）")]
    public int lightningResidueDecayPerRound = 2;
    [Tooltip("雷劈强化概率（0~1；默认 1 = 劈中且存活即强化。注意：已存在的 asset 保留旧序列化值，需在 Inspector 手动改）")]
    [Range(0f, 1f)] public float lightningPermanentBonusChance = 1f;
    [Tooltip("残留强化概率（0~1；与雷劈强化互相独立。棋子受残留伤害——停留或经过——且存活时按此概率强化随机一项属性）")]
    [Range(0f, 1f)] public float lightningResiduePermanentBonusChance = 0.4f;
    [Tooltip("雷劈强化属性池（劈中且存活触发；与残留池完全独立）")]
    public List<PermanentBonusEntry> strikePermanentBonusPool = new List<PermanentBonusEntry>
    {
        new PermanentBonusEntry(PermanentBonusType.HP, 2, 1f),
        new PermanentBonusEntry(PermanentBonusType.Attack, 2, 1f),
        new PermanentBonusEntry(PermanentBonusType.Defense, 2, 1f),
        new PermanentBonusEntry(PermanentBonusType.Move, 1, 1f),
        new PermanentBonusEntry(PermanentBonusType.Range, 1, 1f)
    };
    [Tooltip("残留强化属性池（停留/经过残留伤害且存活触发；与雷劈池完全独立）")]
    public List<PermanentBonusEntry> residuePermanentBonusPool = new List<PermanentBonusEntry>
    {
        new PermanentBonusEntry(PermanentBonusType.HP, 2, 1f),
        new PermanentBonusEntry(PermanentBonusType.Attack, 2, 1f),
        new PermanentBonusEntry(PermanentBonusType.Defense, 2, 1f),
        new PermanentBonusEntry(PermanentBonusType.Move, 1, 1f),
        new PermanentBonusEntry(PermanentBonusType.Range, 1, 1f)
    };

    /// <summary>永久加成属性类型（对局内永久、随棋子存续、无回合递减、可叠加累积）</summary>
    public enum PermanentBonusType
    {
        HP = 0,       // MaxHP +N（当前血量不同步涨，只涨上限）
        Attack = 1,   // EffectiveAttack +N
        Defense = 2,  // EffectiveDefense +N
        Move = 3,     // MoveRange +N
        Range = 4     // AttackRange +N
    }

    /// <summary>永久加成池条目：属性类型 + 数值 + 概率权重（代码零硬编码，Inspector 可改可增删）。
    /// 权重非归一化：越大越容易抽中（按池内权重加权随机）；权重 0 = 永不抽中；全 0 = 该池无有效条目不强化</summary>
    [System.Serializable]
    public class PermanentBonusEntry
    {
        [Tooltip("属性类型")]
        public PermanentBonusType type = PermanentBonusType.Attack;
        [Tooltip("加成数值（可叠加累积）")]
        public int value = 2;
        [Tooltip("抽中概率权重（非归一化：越大越容易抽中；0 = 永不抽中）")]
        public float weight = 1f;

        public PermanentBonusEntry() { }

        public PermanentBonusEntry(PermanentBonusType t, int v, float w = 1f)
        {
            type = t; value = v; weight = w;
        }
    }

    /// <summary>气候效果类型（可扩展枚举）</summary>
    public enum ClimateEffectKind
    {
        None = 0,
        Rain = 1,      // 暴雨：上回合受击的棋子本回合攻击距离减 rainAttackRangePenalty（下限 1 格）
        Blizzard = 2,  // 暴雪：棋子攻击后，被攻击棋子被击退 blizzardKnockbackDistance 格（复用超载击退）
        Heat = 3,      // 高温：棋子行动（移动或攻击）一次后本回合锁定，不能再移动/攻击
        Hurricane = 4, // 飓风（三期B）：商店关闭 + 棋盘掉幸运方块，双方可见共同抢；移动经过即拾取开出内容
        Lightning = 5  // 雷暴（三期C）：出现时随机格雷劈（扣血+概率永久加成）+ 留雷电残留（强度每轮衰减的棋盘陷阱）
    }

    /// <summary>气候条目：名称 + 概率 + 限制季节 + 效果类型 + 是否独占（通用字段；
    /// 各气候专属数值参数在 MonsoonConfig 专属区块，不在条目里——避免「简单气候背一堆无用参数」）</summary>
    [System.Serializable]
    public class ClimateEntry
    {
        [Tooltip("气候名（日志用）")]
        public string climateName;
        [Tooltip("每轮开始时的出现概率（0~1；专属气候在非对应季节时概率视为 0 不参与判定）")]
        [Range(0f, 1f)] public float probability;
        [Tooltip("限制季节槽位下标（-1=不限；对应 seasons 列表顺序：0春 1夏 2秋 3冬）")]
        public int restrictedSeasonIndex = -1;
        [Tooltip("效果类型")]
        public ClimateEffectKind effectKind;
        [Tooltip("是否独占（高温：出现时本轮不再判定其他可叠加气候）")]
        public bool exclusive;

        public ClimateEntry() { }

        public ClimateEntry(string name, float prob, int season, ClimateEffectKind kind, bool excl)
        {
            climateName = name;
            probability = prob;
            restrictedSeasonIndex = season;
            effectKind = kind;
            exclusive = excl;
        }
    }
}
