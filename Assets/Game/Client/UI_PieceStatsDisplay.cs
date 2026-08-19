using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

/// <summary>
/// 棋子状态数值的集中式屏幕空间显示。
///
/// 设计思路：在单一屏幕空间 Canvas 下，为每个存活棋子维护一个数值元素（左：攻击力 / 右：生命值），
/// 每帧用 Camera.WorldToScreenPoint 把棋子世界坐标投影到屏幕，再定位元素。
/// 文字始终 2D 正立，免疫棋子 3D 旋转与摄像机角度/透视/移动变化；屏幕空间 UI 绘制在最上层，
/// 不会被 3D 几何遮挡。
///
/// 取代旧的 per-piece World Space Canvas + UI_HealthBar 方案。
///
/// ===== 关键实现要点 =====
/// 1) 预制体驱动布局：数值元素由 elementPrefab 实例化，字号 / 颜色 / 排版全在预制体里编辑，
///    改样式无需改代码；脚本只负责数据绑定与屏幕定位。预制体根挂 <see cref="UI_StatElement"/>。
/// 2) 对象池复用（UnityEngine.Pool.ObjectPool）：棋子生成/死亡时 Get（激活）/ Release（反激活），
///    避免频繁 Instantiate/Destroy；池化的是预制体实例，无 AddComponent 运行时开销。
/// 3) Canvas 局部偏移（screenOffset）：先 WorldToScreenPoint 转 Canvas 局部，再加 Canvas 局部偏移。
///    偏移随 Canvas 统一缩放，分辨率无关；避免屏幕像素偏移在 Scale With Screen Size + Match 下被非均匀缩放放大。
///
/// 挂载：放在场景 UI Canvas 下的全屏 RectTransform 子物体上。Inspector 指定 elementPrefab 与 targetCamera。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UI_PieceStatsDisplay : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("数值元素的父容器（屏幕空间 Canvas 下的 RectTransform，建议铺满屏幕）。留空则用本物体自身。")]
    public RectTransform container;
    [Tooltip("渲染棋子的 3D 摄像机，用于 WorldToScreenPoint。留空则用 Camera.main。")]
    public Camera targetCamera;
    [Tooltip("数值元素预制体：根挂 UI_StatElement，含 Attack/HP 两个 TMP 子物体。布局/字号/颜色在预制体内编辑。")]
    public GameObject elementPrefab;

    [Header("显示样式")]
    [Tooltip("数值相对棋子投影点的偏移（Canvas 局部单位 = 参考分辨率像素，随 Canvas 统一缩放，分辨率无关）。")]
    public Vector2 screenOffset = new Vector2(0f, -30f);

    [Header("对象池")]
    [Tooltip("对象池预分配容量（首次 Get 时按需创建，最多缓存到 maxSize）。")]
    public int defaultCapacity = 8;
    [Tooltip("对象池最大容量，超出部分 Release 时直接销毁而非缓存。棋子总数有限，64 足够。")]
    public int maxSize = 64;

    [Header("图标映射")]
    [Tooltip("元素图标映射表；留空则从 Resources 加载 ElementIconMap")]
    [SerializeField] private ElementIconMap elementIconMap;
    [Tooltip("状态图标映射表；留空则从 Resources 加载 StatusIconMap")]
    [SerializeField] private StatusIconMap statusIconMap;

    private Canvas _canvas;
    private Camera _cam;
    private readonly Dictionary<PieceModel, UI_StatElement> _elements = new();
    private readonly List<PieceModel> _removalBuffer = new();
    private ObjectPool<UI_StatElement> _pool;

    private void Awake()
    {
        if (container == null) container = (RectTransform)transform;
        _canvas = container.GetComponentInParent<Canvas>();

        // 启动诊断：暴露常见配置错误，避免“不显示”却不知原因
        if (_canvas == null)
            Debug.LogWarning("[UI_PieceStatsDisplay] 未找到上层 Canvas！请将本物体挂在屏幕空间 Canvas 下。", this);
        if (elementPrefab == null)
            Debug.LogWarning("[UI_PieceStatsDisplay] elementPrefab 未赋值！请在 Inspector 拖入数值元素预制体。", this);

        // 图标映射：Inspector 未指定则从 Resources 加载（注意路径含子目录）
        if (elementIconMap == null)
            elementIconMap = Resources.Load<ElementIconMap>("Element/ElementIconMap");
        if (elementIconMap == null)
            Debug.LogWarning("[UI_PieceStatsDisplay] ElementIconMap 加载失败！路径 Resources/Element/ElementIconMap 不存在或脚本缺失。元素图标将不显示。", this);
        if (statusIconMap == null)
            statusIconMap = Resources.Load<StatusIconMap>("State/StatusIconMap");
        if (statusIconMap == null)
            Debug.LogWarning("[UI_PieceStatsDisplay] StatusIconMap 加载失败！路径 Resources/State/StatusIconMap 不存在或脚本缺失。状态图标将不显示。", this);

        _pool = new ObjectPool<UI_StatElement>(
            createFunc: () =>
            {
                // 实例化预制体作为 container 子物体；布局/样式全在预制体内
                var go = Instantiate(elementPrefab, container);
                go.SetActive(false);
                var el = go.GetComponent<UI_StatElement>();
                if (el == null)
                    Debug.LogError("[UI_PieceStatsDisplay] elementPrefab 根未挂 UI_StatElement 组件！请检查预制体。", this);
                return el;
            },
            actionOnGet: el =>
            {
                // 取出：激活、置顶渲染、重置缓存强制刷新所有字段
                el.gameObject.SetActive(true);
                el.transform.SetAsLastSibling();
                el.ResetCache();
            },
            actionOnRelease: el =>
            {
                // 归还：仅反激活，GameObject 与子物体保留待复用
                if (el != null) el.gameObject.SetActive(false);
            },
            actionOnDestroy: el =>
            {
                if (el != null) Destroy(el.gameObject);
            },
            collectionCheck: true,   // 防止同一元素被重复 Release
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    private void LateUpdate()
    {
        if (TurnManager.Instance == null || TurnManager.Instance.Model == null) return;
        if (elementPrefab == null) return;
        _cam = targetCamera != null ? targetCamera : Camera.main;
        if (_cam == null) return;

        var model = TurnManager.Instance.Model;
        int frame = Time.frameCount;

        // 同步存活棋子 → 复用/创建数值元素
        SyncPieces(model.Player1Pieces, frame);
        SyncPieces(model.Player2Pieces, frame);

        // 移除本帧未见到的元素（棋子已死亡/注销）→ 归还对象池
        if (_elements.Count > 0)
        {
            _removalBuffer.Clear();
            foreach (var kvp in _elements)
                if (kvp.Value.lastSeenFrame != frame)
                    _removalBuffer.Add(kvp.Key);
            foreach (var piece in _removalBuffer)
            {
                if (_elements.TryGetValue(piece, out var el))
                    _pool.Release(el);
                _elements.Remove(piece);
            }
        }
    }

    private void SyncPieces(List<PieceModel> pieces, int frame)
    {
        if (pieces == null) return;
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || piece.View == null) continue;

            if (!_elements.TryGetValue(piece, out var el))
            {
                el = _pool.Get();
                el.gameObject.name = $"Stats_{piece.Data?.displayName ?? "?"}_{piece.Owner}";
                _elements[piece] = el;
            }
            el.lastSeenFrame = frame;
            UpdateElement(el, piece);
        }
    }

    private void UpdateElement(UI_StatElement el, PieceModel piece)
    {
        // 世界坐标 → 屏幕（像素，左下原点）
        Vector3 screenPos = _cam.WorldToScreenPoint(piece.View.transform.position);

        // 在摄像机后方 → 隐藏（不归还池，仅反激活；下一帧仍由 SyncPieces 维护）
        if (screenPos.z <= 0f)
        {
            if (el.gameObject.activeSelf) el.gameObject.SetActive(false);
            return;
        }
        if (!el.gameObject.activeSelf) el.gameObject.SetActive(true);

        // 屏幕 → 容器局部坐标（overlay 传 null，screen-camera 传 canvas.worldCamera）
        // 注意：screenPos 必须是“无偏移”的棋子投影点。偏移在 Canvas 局部空间加（见下），
        // 不能加在屏幕像素上 —— 否则在 Scale With Screen Size + Match 0.5 下，屏幕像素偏移
        // 会经非均匀缩放被 s/sH 放大/缩小，导致拉高/拉低分辨率时 UI 垂直偏移剧变。
        var canvasCam = _canvas != null ? _canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screenPos, canvasCam, out Vector2 localPos);

        // Canvas 局部空间偏移（参考分辨率单位，随 Canvas 统一缩放 → 分辨率无关，无垂直放大）
        localPos += screenOffset;
        el.Rect.anchoredPosition = localPos;

        // ===== 属性数值（仅在变化时更新，避免每帧字符串分配）=====

        // 攻击力（现有）
        int atk = piece.EffectiveAttack;
        if (el.lastAttack != atk)
        {
            el.attackText.text = atk.ToString();
            el.lastAttack = atk;
        }

        // 生命值（现有）
        int hp = piece.CurrentHP;
        if (el.lastHP != hp)
        {
            el.hpText.text = hp.ToString();
            el.lastHP = hp;
        }

        // 防御（新增）
        if (el.defText != null)
        {
            int def = piece.EffectiveDefense;
            if (el.lastDef != def)
            {
                el.defText.text = def.ToString();
                el.lastDef = def;
            }
        }

        // 移动范围（新增）
        if (el.moveText != null)
        {
            int mv = piece.MoveRange;
            if (el.lastMove != mv)
            {
                el.moveText.text = mv.ToString();
                el.lastMove = mv;
            }
        }

        // 攻击范围（新增）
        if (el.rangeText != null)
        {
            int range = piece.AttackRange;
            if (el.lastRange != range)
            {
                el.rangeText.text = range.ToString();
                el.lastRange = range;
            }
        }

        // ===== 能量条（新增；无大招棋子 Energy=null → 隐藏）=====
        if (el.energyBar != null && piece.Energy != null)
        {
            if (!el.energyBar.gameObject.activeSelf)
                el.energyBar.gameObject.SetActive(true);

            int cur = piece.Energy.CurrentEnergy;
            int max = piece.Energy.MaxEnergy;
            if (el.lastEnergy != cur)
            {
                el.energyBar.maxValue = max;
                el.energyBar.value = cur;
                el.lastEnergy = cur;
            }
            bool full = piece.Energy.IsFull;
            if (el.lastEnergyFull != full)
            {
                if (el.energyBarFill != null)
                    el.energyBarFill.color = full
                        ? new Color(1f, 0.84f, 0f)    // 满能金色
                        : new Color(0.27f, 0.53f, 1f); // 未满蓝色
                el.lastEnergyFull = full;
            }
        }
        else if (el.energyBar != null)
        {
            if (el.energyBar.gameObject.activeSelf)
                el.energyBar.gameObject.SetActive(false);
        }

        // ===== 元素图标（新增）=====
        if (el.innateElementIcon != null && elementIconMap != null)
        {
            var elem = piece.Data != null ? piece.Data.innateElement : ElementType.None;
            if (el.lastInnateElement != elem)
            {
                var spr = elementIconMap.GetIcon(elem);
                el.innateElementIcon.sprite = spr;
                el.innateElementIcon.gameObject.SetActive(spr != null);
                el.lastInnateElement = elem;
            }
        }

        if (el.affixedElementIcon != null && elementIconMap != null)
        {
            bool has = piece.AffixedElement != ElementType.None && piece.AffixedElementGauge > 0;
            var aff = has ? piece.AffixedElement : ElementType.None;
            if (el.lastAffixedElement != aff)
            {
                var spr = has ? elementIconMap.GetIcon(aff) : null;
                el.affixedElementIcon.sprite = spr;
                el.affixedElementIcon.gameObject.SetActive(spr != null);
                el.lastAffixedElement = aff;
            }
            // 附着元素 Gauge 数字（有附着时显示元素量）
            if (el.affixedElementGaugeText != null)
            {
                int gauge = has ? piece.AffixedElementGauge : 0;
                if (el.lastAffixedGauge != gauge)
                {
                    el.affixedElementGaugeText.text = gauge > 0 ? gauge.ToString() : "";
                    el.affixedElementGaugeText.gameObject.SetActive(gauge > 0);
                    el.lastAffixedGauge = gauge;
                }
            }
        }

        // ===== 装备图标（3 槽；每帧赋值，无缓存比对——3 个 Image 性能可忽略）=====
        if (el.equipSlots != null && el.equipSlots.Length == 3)
        {
            for (int i = 0; i < 3; i++)
            {
                bool hasEquip = i < piece.EquippedItems.Length && piece.EquippedItems[i] != null;
                if (el.equipSlots[i] == null) continue;

                if (hasEquip)
                {
                    var icon = piece.EquippedItems[i].Data != null ? piece.EquippedItems[i].Data.icon : null;
                    el.equipSlots[i].sprite = icon;
                    el.equipSlots[i].color = Color.white;
                }
                else
                {
                    el.equipSlots[i].sprite = null;
                    el.equipSlots[i].color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                }
            }
        }

        // ===== 状态图标（新增；仅在状态组合变化时重建）=====
        if (el.statusContainer != null && el.statusIconPrefab != null && statusIconMap != null)
        {
            int hash = HashStatus(piece);
            if (el.lastStatusHash != hash)
            {
                // 清除旧图标
                for (int i = el.statusContainer.childCount - 1; i >= 0; i--)
                    Destroy(el.statusContainer.GetChild(i).gameObject);

                // 按条件创建新图标
                ShowStatusIf(el, "DefDown", piece.CurrentDefenseReduction > 0);
                ShowStatusIf(el, "DefUp", piece.TemporaryDefenseBonus > 0);
                ShowStatusIf(el, "Frozen", piece.IsFrozen);
                el.lastStatusHash = hash;
            }
        }
    }

    // ==========================================
    //  状态图标辅助
    // ==========================================
    private void ShowStatusIf(UI_StatElement el, string key, bool condition)
    {
        if (!condition) return;
        var spr = statusIconMap != null ? statusIconMap.GetIcon(key) : null;
        if (spr == null) return;
        var go = Instantiate(el.statusIconPrefab, el.statusContainer);
        var img = go.GetComponent<Image>();
        if (img != null) img.sprite = spr;
        go.SetActive(true);
    }

    private static int HashStatus(PieceModel p)
    {
        int h = 0;
        if (p.CurrentDefenseReduction > 0) h |= 1;
        if (p.TemporaryDefenseBonus > 0) h |= 2;
        if (p.IsFrozen) h |= 4;
        return h;
    }

    private void OnDestroy()
    {
        // 清空索引；池中空闲实例由 Clear() 统一销毁；in-use 元素的 GameObject 随场景销毁自动回收
        _elements.Clear();
        _pool?.Clear();
    }
}
