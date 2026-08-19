using UnityEngine;

/// <summary>
/// 状态图标映射表（ScriptableObject）—— 将状态键名（如 DefDown/DefUp/Frozen）映射为图标 Sprite。
/// 通过 Resources.Load&lt;StatusIconMap&gt;("StatusIconMap") 加载（asset 位于 Resources/State/）。
///
/// 供 UI_StatElement 显示棋子状态图标（减防 / 加防 / 冻结等）。
/// 硬约束：状态图标映射必须通过此 ScriptableObject 定义，不在代码中硬编码 Sprite 引用。
/// </summary>
[CreateAssetMenu(fileName = "StatusIconMap", menuName = "Chess/Status Icon Map", order = 21)]
public class StatusIconMap : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("状态键名：DefDown(减防) / DefUp(加防) / Frozen(冻结)")]
        public string statusKey;
        public Sprite icon;
    }

    [Tooltip("状态键 → 图标映射列表")]
    public Entry[] entries;

    /// <summary>获取指定状态键的图标；未配置返回 null</summary>
    public Sprite GetIcon(string key)
    {
        if (entries == null || string.IsNullOrEmpty(key)) return null;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].statusKey == key)
                return entries[i].icon;
        }
        return null;
    }
}
