using UnityEngine;

/// <summary>
/// 元素图标映射表（ScriptableObject）—— 将 <see cref="ElementType"/> 映射为图标 Sprite。
/// 通过 Resources.Load&lt;ElementIconMap&gt;("ElementIconMap") 加载（asset 位于 Resources/Element/）。
///
/// 供 UI_StatElement 显示棋子所属元素 / 附着元素图标。
/// 硬约束：元素图标映射必须通过此 ScriptableObject 定义，不在代码中硬编码 Sprite 引用。
/// </summary>
[CreateAssetMenu(fileName = "ElementIconMap", menuName = "Chess/Element Icon Map", order = 20)]
public class ElementIconMap : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public ElementType element;
        public Sprite icon;
    }

    [Tooltip("元素 → 图标映射列表；覆盖所有 ElementType")]
    public Entry[] entries;

    /// <summary>获取指定元素的图标；未配置返回 null</summary>
    public Sprite GetIcon(ElementType element)
    {
        if (entries == null) return null;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].element == element)
                return entries[i].icon;
        }
        return null;
    }
}
