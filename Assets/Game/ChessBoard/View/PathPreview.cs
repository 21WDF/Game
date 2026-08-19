using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 路径预览线 —— 独立 LineRenderer，显示棋子移动路径。
/// 由 ChessBoardView 持有与懒加载创建（挂在棋盘根下的独立 GameObject 上）；
/// InputHandler 仅通过 ChessBoardController.ShowPathPreview / HidePathPreview 转发调用，不直接持有。
///
/// 参数取自 GameConfig：pathColor（默认黄 (1,0.9,0.2) α0.6）、pathWidth（默认 0.15，最小兜底 0.08）。
/// 渲染：LineAlignment.TransformZ + transform 旋转 90°（法线朝 +Y，平铺在棋盘上，不随摄像机姿态变化，支持自由摄像机），
///       端头与拐角圆角（numCapVertices/numCornerVertices = 2），useWorldSpace = true。
///       位置点 Y 取首格 Model 顶面 + 偏移（抬高到 3D 模型之上避免被格子遮挡；所有点共用首格 Y 保证共面）。
///       低视角下平铺线会变薄，靠最小宽度兜底（Mathf.Max(pathWidth, 0.08f)）保证可见。
/// 材质：Sprites/Default（支持 _Color + 顶点色 + Alpha；Unlit/Transparent 无 _Color、不读顶点色 → 渲染成白色）。
///       颜色经 startColor/endColor（顶点色）传入，材质 _Color 保持白色不干扰。Render Queue = Transparent+2（在 Overlay 之上）。
/// </summary>
public class PathPreview : MonoBehaviour
{
    private LineRenderer _lineRenderer;
    private Material _mat;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
            _lineRenderer = gameObject.AddComponent<LineRenderer>();

        // 颜色/宽度取自 GameConfig（便于配合不同主题切换，如冬季棋盘改白色路径）；
        // 配置缺失时回退到默认黄线，保证仍可渲染。
        var config = Resources.Load<GameConfig>("GameConfig");
        Color color = config != null ? config.pathColor : new Color(1.0f, 0.9f, 0.2f, 0.6f);
        float width = config != null ? config.pathWidth : 0.15f;
        // 最小宽度兜底：平铺模式下低视角线会变薄，过窄时几乎不可见
        width = Mathf.Max(width, 0.08f);

        Debug.Log($"[PathPreview] 路径颜色: {color}，宽度: {width}");

        // ---- LineRenderer 参数 ----
        // 平铺在棋盘上：TransformZ 对齐 + 把 transform 旋转 90° 使其局部 Z 轴指向 +Y（世界向上），
        //               条带法线朝上 → 线平铺在 XZ 平面，不随摄像机姿态变化（支持自由摄像机）。
        //               useWorldSpace = true 时点坐标不受 transform 平移/旋转影响，旋转仅决定 TransformZ 对齐方向。
        transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        _lineRenderer.useWorldSpace = true;                  // 路径点为世界坐标（不被 transform 平移/旋转）
        _lineRenderer.loop = false;
        _lineRenderer.alignment = LineAlignment.TransformZ;  // 条带法线 = transform 的 Z 轴（已旋至 +Y → 平铺朝上）
        _lineRenderer.widthMultiplier = 1.0f;                // 宽度由 startWidth/endWidth 控制
        _lineRenderer.startWidth = width;
        _lineRenderer.endWidth = width;
        _lineRenderer.numCapVertices = 2;                    // 端头圆角，端点完整不消失
        _lineRenderer.numCornerVertices = 2;                 // 拐角圆滑，防止折角处断裂

        // 颜色：必须同时设 startColor + endColor，否则默认渐变会过渡到白色；
        // 不另外设 colorGradient（Gradient 会覆盖 startColor/endColor）
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;

        // ---- 材质：Sprites/Default 支持 _Color + 顶点色 + Alpha ----
        // （Unlit/Transparent 无 _Color 且固定管线 combine texture 不读顶点色 → startColor/_Color 均失效，渲染成白色）
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent"); // 兜底（不应触发）
        _mat = new Material(shader);
        _mat.mainTexture = Texture2D.whiteTexture;          // tex * vertexColor * _Color(white) = 顶点色
        _mat.renderQueue = 3002;                             // Transparent + 2，渲染在 Overlay 之上
        _lineRenderer.sharedMaterial = _mat;                 // sharedMaterial 避免 .material 克隆

        // 初始隐藏
        _lineRenderer.positionCount = 0;
    }

    private void OnDestroy()
    {
        // 运行时 new 的 Material 需手动释放（sharedMaterial 不会被 Renderer 自动销毁，否则内存泄漏）
        if (_mat == null) return;
        if (_lineRenderer != null) _lineRenderer.sharedMaterial = null;
        Destroy(_mat);
        _mat = null;
    }

    /// <summary>显示路径（将 HexCoord 列表转换为 LineRenderer 位置点）</summary>
    public void ShowPath(List<HexCoord> path)
    {
        if (path == null || path.Count == 0)
        {
            HidePath();
            return;
        }

        var board = ChessBoardController.Instance;
        if (board == null)
        {
            HidePath();
            return;
        }

        // 路径线高度：取首格 Model 顶面世界 Y + 偏移，抬高到 3D 模型之上避免被格子遮挡。
        // 所有格子同 Prefab、同高度 → 首格顶面即整条路径的共面高度
        // （平铺模式下各点 Y 决定条带高度，必须一致，否则条带分段出现阶梯）。
        float lineY = GetTileTopY(board.GetTile(path[0]));

        var positions = new Vector3[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            var tile = board.GetTile(path[i]);
            if (tile != null)
            {
                var pos = tile.transform.position;
                positions[i] = new Vector3(pos.x, lineY, pos.z);
            }
            else
            {
                // 格子不存在（不应发生在正常路径上），用同高占位
                positions[i] = new Vector3(0f, lineY, 0f);
            }
        }

        _lineRenderer.positionCount = positions.Length;
        _lineRenderer.SetPositions(positions);
    }

    /// <summary>取格子 Model 顶面世界 Y + 偏移（路径线抬高到 3D 模型之上，避免被格子遮挡）。
    /// 优先读 Model 子对象 Renderer.bounds.max.y（世界 AABB 顶面，自动适配模型高度/缩放，不硬编码）；
    /// TileOverlay 运行时创建的 OverlayHex 也是 Renderer，用 transform.Find("Model") 精确定位排除。
    /// 偏移 0.05 同时越过 OverlayHex（位于顶面 +0.01），保证路径线在格子模型与叠加层之上。</summary>
    private static float GetTileTopY(HexTile tile)
    {
        const float offset = 0.05f;
        if (tile == null) return offset;
        var model = tile.transform.Find("Model");
        if (model != null)
        {
            var rend = model.GetComponent<Renderer>();
            if (rend != null)
                return rend.bounds.max.y + offset;
        }
        // 兜底：第一个子 Renderer 的世界 bounds 顶面
        var r = tile.GetComponentInChildren<Renderer>();
        return (r != null ? r.bounds.max.y : tile.transform.position.y) + offset;
    }

    /// <summary>隐藏路径</summary>
    public void HidePath()
    {
        if (_lineRenderer != null)
            _lineRenderer.positionCount = 0;
    }
}
