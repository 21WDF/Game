using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店列表项 UI 引用 —— 挂在商店列表项预制体上。
/// 由 UI_ShopPanel 实例化后填充内容、绑定购买按钮。
///
/// 预制体结构（Editor 搭建）：
///   [root] (挂 UI_ShopItemRefs)
///     ├─ iconImage     (Image，装备图标；无图标时隐藏)
///     ├─ nameText      (TextMeshProUGUI)
///     ├─ priceText     (TextMeshProUGUI)
///     ├─ descText      (TextMeshProUGUI)
///     ├─ statsText     (TextMeshProUGUI)
///     └─ buyButton     (Button，子物体含 buttonText TMP)
/// </summary>
public class UI_ShopItemRefs : MonoBehaviour
{
    public Image iconImage;                  // 装备图标（无图标时隐藏）
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI statsText;
    public Button buyButton;
    public TextMeshProUGUI buttonText;   // 按钮上的文字（如 "购买"）
}
