using UnityEngine;

/// <summary>
/// 元素反应数据库（ScriptableObject）—— 反应配置的可编辑数据源。
/// 通过 CreateAssetMenu 在 Project 窗口创建：Create > Chess > Reaction Database。
/// asset 放在 Resources 文件夹，由 RuntimeReactionTable 通过 Resources.Load 加载。
///
/// 硬约束：元素反应数据必须通过此 ScriptableObject 定义，不在代码中硬编码。
/// 新增反应只需在 Inspector 的 entries 数组中添加一条，无需改代码。
/// </summary>
[CreateAssetMenu(fileName = "ReactionDatabase", menuName = "Chess/Reaction Database", order = 30)]
public class ReactionDatabaseSO : ScriptableObject
{
    /// <summary>单条反应配置（可序列化；供 Inspector 编辑）</summary>
    [System.Serializable]
    public class ReactionEntry
    {
        [Tooltip("底元素（被攻击方附着元素）")]
        public ElementType baseElement;
        [Tooltip("触发元素（攻击方先天元素）")]
        public ElementType triggerElement;
        [Tooltip("反应配置")]
        public ElementReactionTable.ReactionConfig config;
    }

    [Tooltip("元素反应条目列表；每条定义一对 (底元素, 触发元素) → 反应配置")]
    public ReactionEntry[] entries;

    /// <summary>查询指定元素对的反应配置；未匹配返回 false</summary>
    public bool TryGetEntry(ElementType baseElement, ElementType trigger, out ReactionEntry entry)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e != null && e.baseElement == baseElement && e.triggerElement == trigger)
                {
                    entry = e;
                    return true;
                }
            }
        }
        entry = null;
        return false;
    }
}
