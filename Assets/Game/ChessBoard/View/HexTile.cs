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
}
