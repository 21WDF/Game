using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 物品 tooltip 浮层（滚动视图定位版）—— **右键点击物品**时显示。
///
/// 定位：贴 **Scroll View 滚动视图的右侧边缘外侧**（水平基准 = 滚动视图 RectTransform），
/// **垂直方向与对应物品格子对齐（平齐）**（垂直基准 = 物品格 RectTransform）。
/// 不跟随鼠标；不做屏幕边界 clamp（简化口径）。
///
/// 关闭条件（两条件都成立才关）：
///   - 鼠标**离开滚动视图区域**（由物品格/滚动区域侧通知 NotifyPointerLeftScrollArea）；
///   - **且** 鼠标不在 tooltip 面板上（tooltip 自身 IPointerEnter/Exit 跟踪）。
/// 即：鼠标仍在滚动视图内、或仍在 tooltip 上 → 保持显示。
///
/// 本组件不参与面板计数/棋盘交互（纯显示浮层）。
///
/// Editor 搭建：
///   [root] (挂 UI_ItemTooltip；背景 Image 需 RaycastTarget=true 以支持「在 tooltip 上保持」)
///     ├─ titleText   (TextMeshProUGUI，名称行)
///     └─ descText    (TextMeshProUGUI，描述/属性行)
/// </summary>
public class UI_ItemTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descText;

    [Tooltip("相对滚动视图右边缘的间距（像素）")]
    [SerializeField] private float xOffset = 8f;
    [Tooltip("移出滚动视图后延迟关闭的秒数（给鼠标移进 tooltip 留时间）")]
    [SerializeField] private float closeDelay = 0.25f;

    private RectTransform _rect;
    private RectTransform _itemRect;       // 垂直对齐基准（当前物品格）
    private RectTransform _scrollViewRect; // 水平定位基准（滚动视图）
    private bool _pointerInside;           // 鼠标在 tooltip 区域内（保持显示）
    private bool _pointerInScrollArea;     // 鼠标在滚动视图区域内（保持显示）
    private float _closeAt;                // 延迟关闭时间戳（-1 = 无待关闭）
    private bool _showing;

    private void Awake()
    {
        _rect = transform as RectTransform;
        Hide();
    }

    private void Update()
    {
        // 延迟关闭：鼠标离开滚动视图且不在 tooltip 上后倒计时；期间任一条件恢复（移回滚动视图/移进 tooltip）取消
        if (_showing && !_pointerInside && !_pointerInScrollArea && _closeAt >= 0f && Time.unscaledTime >= _closeAt)
        {
            _closeAt = -1f;
            Hide();
        }
    }

    /// <summary>显示 tooltip：内容 + 定位（水平贴滚动视图右缘、垂直与物品格平齐）。
    /// scrollArea = 滚动视图 RectTransform（水平基准）；item = 物品格 RectTransform（垂直基准）</summary>
    public void Show(string title, string desc, RectTransform item, RectTransform scrollArea)
    {
        if (titleText != null) titleText.text = title;
        if (descText != null)
        {
            descText.text = desc;
            descText.gameObject.SetActive(!string.IsNullOrEmpty(desc));
        }
        _itemRect = item;
        _scrollViewRect = scrollArea != null ? scrollArea : item;
        gameObject.SetActive(true);
        _showing = true;
        _pointerInside = false;
        _pointerInScrollArea = true;   // 右键点物品时鼠标必在滚动区域内
        _closeAt = -1f;
        PlaceBesideScrollView();
    }

    /// <summary>外部立即关闭（拖拽开始/格子销毁/侧栏隐藏时调用）</summary>
    public void Hide()
    {
        _showing = false;
        _pointerInside = false;
        _pointerInScrollArea = false;
        _closeAt = -1f;
        _itemRect = null;
        _scrollViewRect = null;
        gameObject.SetActive(false);
    }

    /// <summary>物品格/滚动区域侧通知：鼠标已离开滚动视图区域（可能延迟关闭——若也不在 tooltip 上）</summary>
    public void NotifyPointerLeftScrollArea()
    {
        if (!_showing) return;
        _pointerInScrollArea = false;
        if (!_pointerInside)
            _closeAt = Time.unscaledTime + closeDelay;
    }

    /// <summary>物品格/滚动区域侧通知：鼠标在滚动视图区域内（取消待关闭，保持显示）</summary>
    public void NotifyPointerInScrollArea()
    {
        if (!_showing) return;
        _pointerInScrollArea = true;
        _closeAt = -1f;
    }

    // ---- tooltip 自身指针事件（在 tooltip 上 = 保持；离开 tooltip = 若也不在滚动区域则关）----
    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
        _closeAt = -1f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
        if (!_pointerInScrollArea)
            _closeAt = Time.unscaledTime;   // 离开 tooltip 且不在滚动区域 → 立即关闭（下一帧生效）
    }

    /// <summary>定位：tooltip 左边缘贴滚动视图右边缘 + xOffset；顶边与物品格顶边对齐（垂直平齐）</summary>
    private void PlaceBesideScrollView()
    {
        if (_rect == null || _scrollViewRect == null || _itemRect == null) return;

        // 世界坐标锚定（Canvas 任意模式通用）
        Vector3[] scrollCorners = new Vector3[4];
        _scrollViewRect.GetWorldCorners(scrollCorners);
        Vector3 scrollTopRight = scrollCorners[3];   // 滚动视图右上角

        Vector3[] itemCorners = new Vector3[4];
        _itemRect.GetWorldCorners(itemCorners);
        Vector3 itemTop = itemCorners[3];            // 物品格顶边（与 tooltip 顶边对齐）

        // tooltip pivot 默认 (0,1)（左上）：左边缘贴滚动视图右缘、顶边对齐物品格顶边
        Vector3 worldPos = new Vector3(scrollTopRight.x + xOffset, itemTop.y, 0f);
        if (_rect.pivot != new Vector2(0f, 1f))
        {
            // pivot 非 (0,1) 时按 rect 尺寸修正
            Vector2 size = _rect.rect.size;
            worldPos += new Vector3(size.x * _rect.pivot.x, -size.y * (1f - _rect.pivot.y), 0f);
        }
        _rect.position = worldPos;
    }
}
