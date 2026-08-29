using UnityEngine;

/// <summary>
/// 玩家阵营
/// </summary>
public enum PlayerSide
{
    P1 = 0,
    P2 = 1
}

/// <summary>格子高亮类型（Flags：一个格子可同时属于移动范围与攻击范围，如 Move|Attack 重合格）</summary>
[System.Flags]
public enum HighlightType
{
    None = 0,
    Move = 1,           // 可移动范围（蓝色）
    Attack = 2,         // 可攻击范围普通格（红色）
    AttackEnemy = 4,    // 可攻击范围内敌方棋子所在格（浅红）
    Waypoint = 8,       // 途经点（绿）—— 手动路径选择的锁定节点
    // 注：悬停不再作为战术类型，改为 HexTile 内部的 bool 叠加层（与战术类型组合出材质）
}

/// <summary>高亮显示模式（R 键切换）：决定选中棋子后显示移动范围还是攻击范围。
/// 选中棋子默认 Move，按 R 切换；取消选中/按下其他操作键时重置为 Move。</summary>
public enum HighlightMode
{
    Move,   // 移动模式（默认）：显示蓝色移动范围
    Attack  // 攻击模式：显示红色攻击范围（敌方格浅红），不显示移动范围
}

/// <summary>元素类型（元素城邦系统）</summary>
public enum ElementType
{
    None = 0,
    Fire = 1,
    Water = 2,
    Thunder = 3,
    Ice = 4
}

/// <summary>元素反应类型（元素城邦系统）</summary>
public enum ReactionType
{
    None,
    Vaporize,           // 蒸发
    Melt,               // 融化
    Overload,           // 载
    Frozen,             // 冻结
    ElectroCharged,     // 感电
    Superconduct        // 超导
}

/// <summary>城邦类型（Pillar 3 城邦系统）：本局生效城邦由战前双方各选 4 个心仪城邦求交集随机确定</summary>
public enum CityStateKind
{
    None = 0,           // 不归属任何城邦（通用装备兜底 / 城邦未定）
    Trade = 1,          // 贸易
    Merriment = 2,      // 欢愉
    Monsoon = 3,        // 季风
    Occult = 4,         // 邪疑
    War = 5,            // 战争
    Element = 6         // 元素
}

/// <summary>装备分级（商店 Tab 分类用）</summary>
public enum EquipmentTier
{
    Basic = 0,          // 初级
    Intermediate = 1,   // 中级
    Advanced = 2,       // 高级（暂无装备，Tab 保留显示空列表）
    CityState = 3       // 城邦（元素之力系列）
}

/// <summary>商店 Tab（前 4 个与 EquipmentTier 一一对应，GridItem 为道具页，Auction 为拍卖行，Underground 为地下交易）</summary>
public enum ShopTab
{
    Basic = 0,          // 初级装备
    Intermediate = 1,   // 中级装备
    Advanced = 2,       // 高级装备
    CityState = 3,      // 城邦装备
    GridItem = 4,       // 棋盘道具
    Auction = 5,        // 拍卖行（贸易之城·一期）
    Underground = 6     // 地下交易（贸易之城·三期；仅当前活动玩家商行开启时可见）
}