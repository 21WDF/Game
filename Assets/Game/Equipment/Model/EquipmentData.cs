using UnityEngine;

/// <summary>
/// 装备定义（ScriptableObject）—— 装备数据的唯一来源。
/// 通过 CreateAssetMenu 在 Project 窗口创建：Create > Chess > Equipment Data。
/// 硬约束：装备数据（id、名称、价格、属性加成、被动配置）必须通过 EquipmentData 定义。
///
/// 字段说明：
///   - 属性加成（bonusHP/Attack/Defense/MoveRange/AttackRange）由 PieceModel.Equipment* 属性聚合，
///     自动叠加到 EffectiveAttack/EffectiveDefense/MaxHP/MoveRange/AttackRange。
///   - bonusAPCap 当前版本暂不应用（AP 为每玩家全局，跨模块；仅作数据字段保留，未来扩展）。
///   - passives 通过类名 + JSON 参数描述，由 EquipmentManager 工厂实例化为 IPassiveEffect。
/// </summary>
[CreateAssetMenu(fileName = "EquipmentData", menuName = "Chess/Equipment Data", order = 10)]
public class EquipmentData : ScriptableObject
{
    [Header("标识")]
    public int id;                          // 装备 id
    public string displayName = "Equipment";
    [TextArea] public string description = "";
    public EquipmentTier tier = EquipmentTier.Basic;   // 装备分级（商店 Tab 分类用）

    [Header("经济")]
    public int price = 0;                   // 商店售价

    [Header("显示")]
    [Tooltip("装备图标（供 UI_StatElement 装备槽位显示）")]
    public Sprite icon;                     // 装备图标

    [Header("属性加成")]
    public int bonusHP = 0;
    public int bonusAttack = 0;
    public int bonusDefense = 0;
    public int bonusMoveRange = 0;
    public int bonusAttackRange = 0;
    public int bonusEnergyPerTurn = 0;      // 每回合回能量（蓝量）
    public int bonusAPCap = 0;              // 注：当前版本暂不应用（仅数据字段保留）

    [Header("被动效果")]
    public PassiveConfig[] passives;

    /// <summary>被动效果配置：通过类名 + JSON 参数描述，由 EquipmentManager 工厂实例化</summary>
    [System.Serializable]
    public class PassiveConfig
    {
        [Tooltip("实现 IPassiveEffect 的类名，如 ExtraDamagePassive")]
        public string className = "";
        [Tooltip("构造参数 JSON，如 {\"amount\":3}")]
        public string jsonParams = "";
        [Tooltip("被动名字（纯展示，后续 UI 装备介绍开头显示）")]
        public string passiveName = "";
        [Tooltip("是否唯一被动：同棋子身上同 uniqueId 的唯一被动只生效最后装备的那件（先进先出），先装备的失效")]
        public bool isUnique = false;
        [Tooltip("唯一被动 id：isUnique=true 时必填；不同装备填相同 uniqueId 即互相冲突（约束键是 id，不是装备身份）")]
        public string uniqueId = "";
    }
}
