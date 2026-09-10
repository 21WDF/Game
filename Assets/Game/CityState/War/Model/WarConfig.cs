using UnityEngine;

/// <summary>
/// 战争之城配置（ScriptableObject）—— 主将机制全部数值（数据驱动，代码零硬编码）。
/// 对标 MonsoonConfig：Create > Chess > War Config 创建资产（默认名 WarConfig，无空格——
/// 与 WarCityManager 的 Resources.Load&lt;WarConfig&gt;("WarConfig") 回退路径匹配；
/// 资产放 Assets/Game/Resources/ 下即可被回退加载）后拖入 WarCityManager；
/// 未拖入时回退 Resources/WarConfig，再回退运行时默认实例（字段默认值）。
/// 修改数值无需改代码，直接在资产 Inspector 调整。
/// </summary>
[CreateAssetMenu(fileName = "WarConfig", menuName = "Chess/War Config", order = 55)]
public class WarConfig : ScriptableObject
{
    [Header("主将加成（部署阶段每方第一个放置的棋子成为主将；对局内持续有效、随棋子存续）")]
    [Tooltip("主将生命上限加成（登记时同步补满等量当前血量）")]
    public int generalHPBonus = 20;

    [Tooltip("主将防御加成")]
    public int generalDefenseBonus = 3;

    [Tooltip("主将攻击加成")]
    public int generalAttackBonus = 6;

    [Tooltip("主将移动距离加成（不含攻击距离——主将基础加成四维无射程）")]
    public int generalMoveBonus = 1;

    [Header("主将装备额外加成")]
    [Tooltip("主将的装备每实际提供一项属性（攻/防/生命/移动/射程），该项额外加该值（仅装备实际提供的项享受；未提供该项不享受）")]
    public int generalEquipStatBonus = 2;

    [Header("棋盘扩展")]
    [Tooltip("本局城邦为战争之城时，棋盘向外扩展的环数（城邦揭晓确认后、进入选棋子前一次性扩展；0 = 不扩展）")]
    public int boardExpansionRings = 2;
}
