using UnityEngine;

/// <summary>
/// 元素之城配置（ScriptableObject）—— 元素城邦机制参数（数据驱动，代码零硬编码）。
/// 对标 MonsoonConfig/WarConfig 范式：Create > Chess > Element City Config 创建资产（默认名 ElementCityConfig），
/// 放到 Assets/Game/Resources/ 下即可被 Resources.Load 回退加载；也可直接拖入 ElementCityManager Inspector。
/// 未拖入且 Resources 无资产时回退运行时默认实例（字段默认值 = 原 GameConfig 中的取值，行为等价）。
/// 修改数值无需改代码，直接在资产 Inspector 调整。
/// </summary>
[CreateAssetMenu(fileName = "ElementCityConfig", menuName = "Chess/Element City Config", order = 70)]
public class ElementCityConfig : ScriptableObject
{
    [Header("护盾染色")]
    [Tooltip("护盾元素染色开关（master 总闸）。开启时：有护盾且未染色的棋子被元素附着 → 护盾永久变成对应元素盾（染色）；染色后该元素无法再附着到该棋子（封印），其他元素照常附着；元素反应照常（封印的是附着，不是反应）。关闭时一切等同护盾基座（无染色）。")]
    public bool shieldElementDyeingEnabled = true;
}
