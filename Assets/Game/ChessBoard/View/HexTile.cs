using UnityEngine;

/// <summary>
/// 棋盘上单个六边形格子
/// 挂载在 HexTile Prefab 的空根对象上，MeshRenderer 在子对象 Model 上
///
/// 高亮系统已改为 TileOverlay 叠加层（独立于地形材质）：
///   - 战术高亮由 BattleView 通过 overlay.Show/Hide 管理
///   - 悬停高亮由 InputHandler 通过 overlay.ShowHover/HideHover 管理
///   - 路径预览由独立 PathPreview（LineRenderer）管理，不挂在 HexTile 上
/// </summary>
public class HexTile : MonoBehaviour
{
    // ---- 核心数据 ----
    public HexCoord Coord { get; private set; }
    // Phase 2a：占据数据（Occupant）已迁移到 PieceLayoutModel，HexTile 仅保留渲染职责。

    // ---- 叠加层 ----
    [Tooltip("格子叠加层（高亮/悬停）。留空则 Awake 时自动创建。")]
    public TileOverlay overlay;

    // ---- 初始化 ----
    private void Awake()
    {
        // 确认 Model 子对象有 MeshRenderer
        var meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogError($"[HexTile] {name} 的子对象 Model 缺少 MeshRenderer！", this);
            enabled = false;
            return;
        }

        // 获取或创建 TileOverlay
        if (overlay == null)
            overlay = GetComponentInChildren<TileOverlay>();
        if (overlay == null)
            overlay = gameObject.AddComponent<TileOverlay>();
    }

    public void Initialize(HexCoord coord)
    {
        Coord = coord;
        name = $"Hex_{coord.q}_{coord.r}";
    }

    // ==========================================
    //  格子材质覆盖（雷电残留·三期C 材质化：格子本体材质临时切换，归零还原）
    //  与 TileOverlay 叠加层完全独立：覆盖期间战术/悬停高亮照常叠加显示，互不干扰
    // ==========================================
    private MeshRenderer _modelRenderer;        // 惰性缓存：Model 子物体的地形 renderer
    private bool _modelLookupFailed;            // Model 查找失败标志（只告警一次 + 不再重试，防刷屏）
    private Material _originalMaterial;         // 覆盖前的原材质（首次覆盖时保存）
    private bool _materialOverridden;           // 覆盖中标志（幂等：重复 Set 不覆盖已保存的原材质）

    /// <summary>Model 地形 renderer（惰性查找：按固定子物体名「Model」定位，prefab 结构稳定）。
    /// 注意不能用「排除 overlay 子树」定位——TileOverlay 挂在 HexTile 根上，根的子物体
    ///（含 Model）全部 IsChildOf(根)，会全部被误排除导致定位为空（材质化静默失效的根因）</summary>
    private MeshRenderer ModelRenderer
    {
        get
        {
            if (_modelRenderer == null && !_modelLookupFailed)
            {
                var model = transform.Find("Model");
                _modelRenderer = model != null ? model.GetComponent<MeshRenderer>() : null;
                if (_modelRenderer == null)
                {
                    _modelLookupFailed = true;
                    Debug.LogError($"[HexTile] {name} 未找到 Model 子物体的 MeshRenderer，格子材质覆盖不可用！", this);
                }
            }
            return _modelRenderer;
        }
    }

    /// <summary>临时覆盖格子本体材质（雷电残留等持续状态展示）。幂等：覆盖期间重复调用只换材质、
    /// 不覆盖已保存的原材质（重劈同格安全）。材质为 null 或找不到 Model renderer 时不做任何事</summary>
    public void SetTileMaterial(Material material)
    {
        var renderer = ModelRenderer;
        if (renderer == null || material == null) return;
        if (!_materialOverridden)
        {
            _materialOverridden = true;
            _originalMaterial = renderer.sharedMaterial;
        }
        renderer.sharedMaterial = material;
    }

    /// <summary>还原格子本体材质（残留归零时调用）。幂等：未覆盖时不做任何事；
    /// sharedMaterial 引用切换不产生材质实例副本，无泄漏</summary>
    public void RestoreTileMaterial()
    {
        if (!_materialOverridden) return;
        var renderer = ModelRenderer;
        if (renderer != null && _originalMaterial != null)
            renderer.sharedMaterial = _originalMaterial;
        _materialOverridden = false;
        _originalMaterial = null;
    }
}
