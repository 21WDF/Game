using UnityEngine;

/// <summary>
/// 元素格子配置（ScriptableObject，全城邦通用棋盘机制）—— 数据驱动，代码零硬编码。
/// 对标 MonsoonConfig/WarConfig 范式：Create > Chess > Element Tile Config 创建资产（默认名 ElementTileConfig），
/// 放到 Assets/Game/Resources/ 下即可被 Resources.Load 回退加载；也可直接拖入 ElementTileManager Inspector。
/// 未拖入且 Resources 无资产时回退运行时默认实例（字段默认值，行为等价）。
///
/// 元素格子：坐标 + 元素类型 + 剩余持续回合；按「回合」（任一方回合结束）结算停留效果并递减时长。
/// 生成 / 视觉 / 持续伤害 / 减益全城邦通用；仅「给棋子附着元素」受元素城邦过滤（复用既有附着链路）。
/// 修改数值无需改代码，直接在资产 Inspector 调整。
/// </summary>
[CreateAssetMenu(fileName = "ElementTileConfig", menuName = "Chess/Element Tile Config", order = 80)]
public class ElementTileConfig : ScriptableObject
{
    [Header("通用参数")]
    [Tooltip("默认持续回合数（任一方回合结束递减；大招配置里持续回合数 ≤0 时回退用本值）")]
    public int defaultDurationTurns = 4;

    [Tooltip("每次附着的元素量（停留结算与移动经过共用同一默认量）")]
    public int defaultAttachGauge = 1;

    [Header("火元素格")]
    public ElementTileEntry fire = new ElementTileEntry { damagePerTurn = 2 };

    [Header("雷元素格")]
    public ElementTileEntry thunder = new ElementTileEntry { damagePerTurn = 2 };

    [Header("水元素格")]
    [Tooltip("默认：攻击距离 -1（站在格上实时生效，离开/格子消失立即恢复，保底 1）")]
    public ElementTileEntry water = new ElementTileEntry { attackRangeDebuff = 1 };

    [Header("冰元素格")]
    [Tooltip("默认：移动距离 -1（站在格上实时生效，离开/格子消失立即恢复，保底 1）")]
    public ElementTileEntry ice = new ElementTileEntry { moveRangeDebuff = 1 };

    /// <summary>单元素格参数组（持续伤害 / 射程减益 / 移动减益 / 格子本体材质）。
    /// 减益数值 0 = 该元素无此项减益；材质为空时无视觉但数据层照常生效（警告一次，不刷屏）</summary>
    [System.Serializable]
    public class ElementTileEntry
    {
        [Tooltip("每回合对停留棋子的持续伤害（环境真实伤害口径：不带元素、不触发元素反应、不附着；0 = 无伤害）")]
        public int damagePerTurn = 0;

        [Tooltip("站在该格上的棋子攻击距离减益值（实时生效，聚合点保底 1；0 = 无减益）")]
        public int attackRangeDebuff = 0;

        [Tooltip("站在该格上的棋子移动距离减益值（实时生效，聚合点保底 1；0 = 无减益）")]
        public int moveRangeDebuff = 0;

        [Tooltip("该元素格子的本体材质（用户在 Editor 准备并拖入；空 = 无视觉仅数据层生效）")]
        public Material material;
    }

    /// <summary>按元素类型取参数组（None / 未知元素返回 null）</summary>
    public ElementTileEntry GetEntry(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire: return fire;
            case ElementType.Thunder: return thunder;
            case ElementType.Water: return water;
            case ElementType.Ice: return ice;
            default: return null;
        }
    }
}
