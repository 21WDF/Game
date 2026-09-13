using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 大厅城邦卡片交互 —— 运行时由 UI_CityStateExclusion 附加到卡片根节点（不改 ShopItem 预制体）。
/// 整卡可点：点击切换禁选；悬停显示描述 Tooltip（复用 UI_ItemTooltip）。
///
/// 只转发事件，不含任何对局状态 / 流程逻辑 —— 联机预留：UI 不持有「双方提交」等对局状态，
/// 状态一律查询 CityStateManager（见其 ② 双方提交状态查询）。
/// </summary>
public class UI_CityStateCard : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>点击卡片（切换禁选）</summary>
    public System.Action OnClicked;

    /// <summary>鼠标悬停进入（显示描述 Tooltip）</summary>
    public System.Action OnHoverStart;

    /// <summary>鼠标悬停离开（隐藏描述 Tooltip）</summary>
    public System.Action OnHoverEnd;

    public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke();

    public void OnPointerEnter(PointerEventData eventData) => OnHoverStart?.Invoke();

    public void OnPointerExit(PointerEventData eventData) => OnHoverEnd?.Invoke();
}
