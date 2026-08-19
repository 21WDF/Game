using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备槽位/背包装备项 UI 引用 —— 挂在装备项预制体上。
/// 复用于「槽位」（已装备/空）与「背包列表项」。由 UI_EquipmentPanel 实例化后填充。
///
/// 预制体结构（Editor 搭建）：
///   [root] (挂 UI_EquipmentItemRefs)
///     ├─ iconImage     (Image，装备图标；空槽位/无图标时隐藏)
///     ├─ nameText      (TextMeshProUGUI)
///     ├─ statsText     (TextMeshProUGUI)
///     └─ actionButton  (Button，子物体含 buttonText TMP)
/// </summary>
public class UI_EquipmentItemRefs : MonoBehaviour
{
    public Image iconImage;                  // 装备图标（空槽位/无图标时隐藏）
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statsText;
    public Button actionButton;
    public TextMeshProUGUI buttonText;   // "装备" / "卸下" / "—"
}
