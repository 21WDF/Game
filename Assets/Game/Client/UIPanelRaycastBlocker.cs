using UnityEngine;

/// <summary>
/// 标记一个 UI 面板为"会阻挡棋盘点击/悬停"的面板。挂在面板根 GameObject 上。
///
/// 背景：InputHandler.IsPointerOverUI 用 GraphicRaycaster 检测鼠标命中的 UI，只有命中带本标记的
/// 面板（含其子物体）时才拦截棋盘交互。这样血量/回合/金币等纯显示 UI（无本标记）即使
/// RaycastTarget=true 也不会遮挡鼠标。
///
/// 用法：给商店面板根（ShopPanel）、装备面板根（EquipmentPanel）挂本组件。
/// 注意：面板背景 Image 仍需保留 RaycastTarget=true，RaycastAll 才能命中面板进而检测到标记。
/// </summary>
public class UIPanelRaycastBlocker : MonoBehaviour
{
    // 空标记组件，无需字段或逻辑
}
