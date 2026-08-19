using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 格子叠加层组件 —— 独立于地形材质的半透明叠加，用于高亮/悬停显示。
/// 挂在 HexTile 根对象或子物体上（运行时自动创建六边形 Mesh + 材质，无需 Editor 手动配置）。
///
/// 双层叠加设计：
///   - 战术层（Show/Hide）：由 BattleView 管理，用于移动/攻击范围高亮
///   - 悬停层（ShowHover/HideHover）：由 InputHandler 管理，用于鼠标悬停
///   两层同时激活时：显示悬停色，Alpha = 战术Alpha + 0.1（堆叠效果，不互相覆盖）
///
/// 形状：程序化平顶六边形 Mesh（与 HexCoord 平顶 Hex 一致），顶点距离中心 1（宽 2、高 √3）。
///       尺寸自适应：Awake 读取同父 Model 的 MeshFilter.sharedMesh.bounds，
///       OverlayHex.localScale = max(extents.x, extents.z)（Model 外接半径），
///       localPosition.y = bounds.max.y + 0.01（略高于 Model 顶面，避免 Z-fighting）。
///       因 OverlayHex 与 Model 同为根节点子物体、缩放一致 → 本地空间对齐即世界空间对齐，
///       兼容未来 hexSize / 根缩放调整。Mesh 跨实例共享（静态缓存）。
/// 材质：Sprites/Default（支持 _Color + Alpha 混合；Unlit/Transparent 无 _Color、Unlit/Color 不透明，均不适用）。
///       每实例 new Material + sharedMaterial 直接引用（.material getter 会克隆导致 Apply 改色不生效）。
///       Render Queue = Transparent+1（3001），保证渲染在地形层之上。
/// 默认状态：全透明（Alpha = 0，等同不存在）。
/// </summary>
public class TileOverlay : MonoBehaviour
{
    private Material _mat;
    private MeshRenderer _renderer;

    // ---- 战术层（BattleView 管理）----
    private Color _tacticalColor = Color.white;
    private float _tacticalAlpha = 0f;
    private bool _tacticalActive = false;

    // ---- 悬停层（InputHandler 管理）----
    private bool _hoverActive = false;
    private Color _hoverColor = Color.white;
    private float _hoverAlpha = 0f;

    // ---- 共享六边形 Mesh（所有 TileOverlay 实例复用，避免每格一份 Mesh）----
    private static Mesh _sharedHexMesh;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer == null)
        {
            // 读取同父 Model 的 Mesh 尺寸，用于把 Overlay 对齐到 Model 顶面（大小 + 高度）
            float radius = 1f;
            float topLocalY = 0.01f;
            var mf = GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var b = mf.sharedMesh.bounds;
                // 平顶六边形：extents.x = 外接半径 R，extents.z = R*√3/2；取 max 得外接半径
                radius = Mathf.Max(b.extents.x, b.extents.z);
                // Model 子物体位于本地原点（scale=1、无旋转）→ mesh bounds 即根本地空间 bounds
                topLocalY = b.max.y + 0.01f;   // 略高于 Model 顶面，避免 Z-fighting / 被模型遮挡
            }

            // 运行时自动创建六边形 Mesh 子物体
            var hexGo = new GameObject("OverlayHex");
            hexGo.transform.SetParent(transform, false);
            hexGo.transform.localPosition = new Vector3(0, topLocalY, 0);               // 略高于 Model 顶面
            hexGo.transform.localRotation = Quaternion.identity;                        // Mesh 已在 XZ 平面，无需旋转
            hexGo.transform.localScale = new Vector3(radius, radius, radius);           // 与 Model 外接半径对齐

            var meshFilter = hexGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetSharedHexMesh();

            _renderer = hexGo.AddComponent<MeshRenderer>();
        }

        // 创建材质：Sprites/Default 支持 _Color(RGBA) + Alpha 混合
        // （Unlit/Transparent 仅 _MainTex、无 _Color，设 color 无效；Unlit/Color 不支持透明，均不适用）
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent"); // 兜底（不应触发）
        _mat = new Material(shader);
        _mat.mainTexture = Texture2D.whiteTexture;   // tex * color = color（纯色叠加）
        _mat.color = new Color(1, 1, 1, 0);           // 初始全透明
        _mat.renderQueue = 3001;                       // Transparent + 1，渲染在地形之上
        // 用 sharedMaterial 直接引用 _mat（_mat 每实例独立），避免 .material getter 克隆导致 Apply 改色不生效
        _renderer.sharedMaterial = _mat;
    }

    private void OnDestroy()
    {
        // 运行时 new 的 Material 需手动释放（sharedMaterial 不会被 Renderer 自动销毁，否则内存泄漏）
        if (_mat == null) return;
        if (_renderer != null) _renderer.sharedMaterial = null;
        Destroy(_mat);
        _mat = null;
    }

    // ==========================================
    //  共享六边形 Mesh（平顶 Hex：顶点在 0°,60°,120°,180°,240°,300°，与 HexCoord 一致）
    //  顶点距离中心 1 → 宽 2（点左右）、高 √3（平边上下），正好贴合 Model 外轮廓。
    //  绕序：从 +Y 俯视顺时针，法线朝上（+Y）。
    // ==========================================
    private static Mesh GetSharedHexMesh()
    {
        if (_sharedHexMesh != null) return _sharedHexMesh;

        _sharedHexMesh = new Mesh { name = "HexOverlay" };

        var vertices = new List<Vector3> { Vector3.zero };  // 中心顶点
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i);  // 0°, 60°, 120°, ...
            vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
        }

        // 中心 + 相邻两外顶点组成 6 个三角形：(0, next, i) 为 CW（+Y 俯视）→ 法线 +Y
        var triangles = new List<int>(18);
        for (int i = 1; i <= 6; i++)
        {
            int next = (i % 6) + 1;  // i=1→2, ..., i=5→6, i=6→1
            triangles.Add(0);
            triangles.Add(next);
            triangles.Add(i);
        }

        _sharedHexMesh.SetVertices(vertices);
        _sharedHexMesh.SetTriangles(triangles, 0);
        _sharedHexMesh.RecalculateNormals();
        return _sharedHexMesh;
    }

    // ==========================================
    //  战术层（BattleView 调用）
    // ==========================================

    /// <summary>显示战术高亮（指定颜色 + Alpha）</summary>
    public void Show(Color color, float alpha)
    {
        _tacticalColor = color;
        _tacticalAlpha = alpha;
        _tacticalActive = true;
        Apply();
    }

    /// <summary>隐藏战术高亮（恢复全透明）</summary>
    public void Hide()
    {
        _tacticalActive = false;
        Apply();
    }

    // ==========================================
    //  悬停层（InputHandler 调用）
    // ==========================================

    /// <summary>
    /// 显示悬停叠加（在战术高亮之上堆叠，不覆盖战术色）。
    /// 有战术高亮时：显示悬停色，Alpha = 战术Alpha + 0.1。
    /// 无战术高亮时：显示悬停色，Alpha = 指定 Alpha。
    /// </summary>
    public void ShowHover(Color color, float alpha)
    {
        _hoverActive = true;
        _hoverColor = color;
        _hoverAlpha = alpha;
        Apply();
    }

    /// <summary>隐藏悬停叠加（恢复战术高亮颜色和 Alpha）</summary>
    public void HideHover()
    {
        _hoverActive = false;
        Apply();
    }

    // ==========================================
    //  内部：合成最终颜色
    // ==========================================

    private void Apply()
    {
        if (_mat == null) return;
        if (_tacticalActive && _hoverActive)
        {
            // 战术 + 悬停：用悬停色，Alpha 叠加 +0.1（简单做法，需求确认）
            _mat.color = new Color(_hoverColor.r, _hoverColor.g, _hoverColor.b, _tacticalAlpha + 0.1f);
        }
        else if (_tacticalActive)
        {
            _mat.color = new Color(_tacticalColor.r, _tacticalColor.g, _tacticalColor.b, _tacticalAlpha);
        }
        else if (_hoverActive)
        {
            _mat.color = new Color(_hoverColor.r, _hoverColor.g, _hoverColor.b, _hoverAlpha);
        }
        else
        {
            _mat.color = new Color(1, 1, 1, 0); // 全透明
        }
    }
}
