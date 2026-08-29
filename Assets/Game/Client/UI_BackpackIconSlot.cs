using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 背包图标格子（拖拽版）—— TFT 式固定槽位格：
/// 左键单击选中高亮（父物体 Image 变色）、左键双击使用道具、右键显示 tooltip、**左键拖拽**。
/// 挂在槽位预制体（父物体 = 空单槽精灵图 Image）上。
///
/// 结构（子物体只剩一个）：
///   [root] (挂 UI_BackpackIconSlot + Image[空单槽精灵图，即父物体背景]）
///     └─ iconImage   (Image，物品图片：有物品=物品 icon；空槽=隐藏)
///
/// 按键语义（左键功能不变；拖拽与单击/双击由 EventSystem 拖拽阈值天然区分——拖动超过阈值
/// 只触发拖拽链，不触发 click）：
///   - 左键单击（clickCount=1）：单选高亮（通知侧栏）
///   - 左键双击（clickCount==2，恰好第 2 击）：触发 OnDoubleClick（道具=使用；装备/空槽=无动作）；
///     三连击的第 3 击回到单击语义（防误触二次使用）
///   - 右键（button==Right）：显示 tooltip（滚动视图右缘 + 本格垂直平齐；定位与关闭逻辑在 UI_ItemTooltip）
///   - 左键拖拽：负载可拖（DragEquipment/DragItem + IsDragEnabled 由侧栏填充时设置）→
///     通知侧栏（OnBeginDragSlot/OnDragSlot/OnEndDragSlot），松手判定与动作全部在侧栏
///
/// 拖拽负载（侧栏填充时设置；空槽无负载不可拖）：
///   - 装备：可拖 → 拖到己方棋子 = 装备（EquipToPiece）
///   - 道具（requiresSelectedPiece，如传送石）：可拖 → 拖到己方棋子 = 选中该棋子 + 使用
///   - 道具（非 requiresSelectedPiece）：不可拖（仍走双击使用）
///   - 空槽：不可拖
///
/// tooltip 关闭（配合 UI_ItemTooltip）：移出任何格子（含空槽）都通知「可能离开滚动区域」
/// （延迟关——鼠标仍在滚动视图内会被其他格子 Enter 取消）；拖拽开始强制关 tooltip。
///
/// 不参与面板开闭计数（延续侧栏红线：常驻不锁棋盘）。
/// </summary>
public class UI_BackpackIconSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("子物体引用")]
    [SerializeField] private Image iconImage;             // 物品图片子物体（空槽时隐藏）

    [Header("选中高亮（父物体变色）")]
    [Tooltip("选中时父物体 Image 的颜色")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f, 1f);

    private Color _originalColor;   // 预制体 Image 的原始颜色（取消选中时还原它——空槽背景色正确还原）
    private Image _slotImage;       // 父物体背景（空槽精灵图；高亮=改它的颜色）
    private RectTransform _rect;
    private bool _selected;

    /// <summary>tooltip 引用（侧栏注入；右键时显示 title/desc 于滚动视图右缘、与本格垂直平齐）</summary>
    [System.NonSerialized] public UI_ItemTooltip Tooltip;

    /// <summary>tooltip 定位的滚动视图基准（侧栏注入：Show 的水平锚 + 关闭判定的「滚动区域」）</summary>
    [System.NonSerialized] public RectTransform TooltipScrollArea;

    /// <summary>tooltip 内容（侧栏填充：装备=名称+属性；道具=名称+描述+AP消耗）</summary>
    [System.NonSerialized] public string TooltipTitle = "";
    [System.NonSerialized] public string TooltipDesc = "";

    /// <summary>双击回调（道具=使用；装备/空槽不注册=无动作）</summary>
    [System.NonSerialized] public System.Action OnDoubleClick;

    /// <summary>单击回调（侧栏单选高亮管理）</summary>
    [System.NonSerialized] public System.Action<UI_BackpackIconSlot> OnClicked;

    // ---- 拖拽负载与回调（侧栏填充/侧栏处理）----
    /// <summary>拖拽负载：装备（可拖 → 拖到己方棋子=装备）；非装备格为 null</summary>
    [System.NonSerialized] public EquipmentModel DragEquipment;
    /// <summary>拖拽负载：道具（仅 requiresSelectedPiece 的可拖）；其余为 null</summary>
    [System.NonSerialized] public GridItemData DragItem;
    /// <summary>本格是否可拖（侧栏填充：装备=true；requiresSelectedPiece 道具=true；其余 false）</summary>
    [System.NonSerialized] public bool IsDragEnabled;

    /// <summary>拖拽开始（侧栏：关 tooltip + 创建跟随幽灵）</summary>
    [System.NonSerialized] public System.Action<UI_BackpackIconSlot> OnBeginDragSlot;
    /// <summary>拖拽中（侧栏：幽灵跟随鼠标）</summary>
    [System.NonSerialized] public System.Action<UI_BackpackIconSlot> OnDragSlot;
    /// <summary>拖拽结束（侧栏：销毁幽灵 + RaycastTile 松手判定 → 装备/使用/取消）</summary>
    [System.NonSerialized] public System.Action<UI_BackpackIconSlot> OnEndDragSlot;

    /// <summary>当前格子显示的 icon（拖拽幽灵用；空槽为 null）</summary>
    public Sprite CurrentIcon => iconImage != null ? iconImage.sprite : null;

    private void Awake()
    {
        _slotImage = GetComponent<Image>();
        _rect = transform as RectTransform;
        // 缓存预制体 Image 原始色（取消选中时还原——空槽背景色正确还原；选中色仍走 Inspector 的 selectedColor）
        if (_slotImage != null) _originalColor = _slotImage.color;
    }

    // ==========================================
    //  填充（侧栏调用）
    // ==========================================
    /// <summary>设置物品图片：sprite 非 null → 显示物品 icon；null → 隐藏（空槽=父物体背景）</summary>
    public void SetIcon(Sprite sprite)
    {
        if (iconImage == null) return;
        iconImage.sprite = sprite;
        iconImage.gameObject.SetActive(sprite != null);
    }

    /// <summary>选中高亮：改父物体 Image 颜色（选中=高亮色 / 取消=还原预制体原始色）</summary>
    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (_slotImage != null)
            _slotImage.color = selected ? selectedColor : _originalColor;
    }

    /// <summary>本格当前是否有物品（空槽不响应单击选中/不可拖）</summary>
    public bool HasItem => iconImage != null && iconImage.gameObject.activeSelf;

    // ==========================================
    //  指针事件（点击/悬停）
    // ==========================================
    public void OnPointerClick(PointerEventData eventData)
    {
        // 空槽：无 tooltip 内容、不响应单击选中（问题4：无物品的槽单击无任何变化）
        if (!HasItem) return;

        // 右键：显示 tooltip（滚动视图右缘 + 本格垂直平齐；不干扰左键选中/双击）
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (Tooltip != null) Tooltip.Show(TooltipTitle, TooltipDesc, _rect, TooltipScrollArea);
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (eventData.clickCount == 2)
        {
            // 左键双击（恰好第 2 次点击才触发）：道具=使用；装备/空槽=未注册回调，无动作。
            // 第 3 次及以后（三连击）回到单击语义，不产生第二次使用（防止「使用后立即取消退回」）
            if (OnDoubleClick != null) OnDoubleClick.Invoke();
            return;
        }
        // 左键单击（含三连击的第 3 击）：单选高亮（侧栏管理；未注册时本地 toggle）
        if (OnClicked != null) OnClicked.Invoke(this);
        else SetSelected(!_selected);
    }

    /// <summary>鼠标在格子上（= 在滚动区域内）：tooltip 保持（取消待关闭）</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Tooltip != null) Tooltip.NotifyPointerInScrollArea();
    }

    /// <summary>鼠标移出格子（含空槽）：通知「可能离开滚动区域」→ tooltip 延迟关闭判定
    /// （鼠标仍在滚动视图内时，会被鼠标当前所在的其他格子/区域 Enter 取消——物品间空隙穿过即属此类；
    /// 真正离开滚动区域且不在 tooltip 上时才关闭。空槽的「无响应」只作用于单击/右键，
    /// 不影响 tooltip 关闭通知——否则 tooltip 的「在滚动区域内」状态会卡住永不消失）</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (Tooltip != null) Tooltip.NotifyPointerLeftScrollArea();
    }

    // ==========================================
    //  拖拽（仅左键 + 可拖负载；EventSystem 拖拽阈值天然区分拖拽与单击/双击）
    // ==========================================
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!IsDragEnabled || !HasItem) return;   // 不可拖负载/空槽：不启动拖拽表现
        if (Tooltip != null) Tooltip.Hide();   // 拖拽开始关闭 tooltip
        if (OnBeginDragSlot != null) OnBeginDragSlot.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!IsDragEnabled) return;
        if (OnDragSlot != null) OnDragSlot.Invoke(this);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 结束统一走侧栏（不可拖负载时侧栏内无幽灵/无动作，安全）
        if (OnEndDragSlot != null) OnEndDragSlot.Invoke(this);
    }

    private void OnDisable()
    {
        // 格子被销毁/隐藏时确保 tooltip 不残留
        if (Tooltip != null && Tooltip.isActiveAndEnabled) Tooltip.Hide();
    }
}
