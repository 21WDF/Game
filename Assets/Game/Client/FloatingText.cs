using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>浮动文字组件 —— 挂在 World Space Canvas 的 TMP 预制体上。
/// 职责：按 <see cref="FloatStyle"/>（含 <see cref="FloatStyleKind"/> 形态）播放上漂 + 淡出动画，结束后回池。
/// 纯 View 层，不参与任何游戏逻辑。由 <see cref="FloatingTextPool"/> 驱动。</summary>
public class FloatingText : MonoBehaviour
{
    /// <summary>预制体子物体上的 TMP 文本组件（Inspector 拖入）</summary>
    public TextMeshProUGUI text;

    // ---- 渐变形态运行时缓存（淡出时按 alpha 重建渐变四角色）----
    private bool _isGradient;
    private Color _gradientTop;     // 上端提亮色
    private Color _gradientBottom;  // 下端压暗色

    // ---- 描边形态运行时缓存 ----
    private bool _isOutlineActive;  // 当前是否处于描边态（池复位/淡出判断用）
    private Material _outlineMat;   // text.fontMaterial 实例（首用描边时创建；池复用期间共享，改它不影响其他跳字）

    /// <summary>描边底色（深色；alpha 随文字同步淡出，避免文字消失后残留深色底影）</summary>
    private static readonly Color OutlineBaseColor = new Color(0.05f, 0.05f, 0.05f, 1f);

    /// <summary>显示一条浮动文字。
    /// 每次先 StopAllCoroutines 防止上一轮动画干扰；位置 = worldPos + 上偏 2.5 单位。</summary>
    public void Show(string msg, Vector3 worldPos, FloatStyle style)
    {
        if (text == null) return;
        StopAllCoroutines();
        text.text = msg;
        text.fontSize = style.fontSize;
        ApplyStyle(style);
        transform.position = worldPos + Vector3.up * 2.5f;
        gameObject.SetActive(true);
        StartCoroutine(Animate(style));
    }

    /// <summary>按样式形态配置 TMP。
    /// 对象池复用防护：先全量复位（关渐变/关描边关键字），再按形态开启——
    /// 防止上一次的渐变/描边残留串到下一次纯色跳字。</summary>
    private void ApplyStyle(FloatStyle style)
    {
        // ---- 复位为纯色基线 ----
        text.enableVertexGradient = false;
        _isGradient = false;
        text.color = style.color;

        // 描边复位：仅上一次开过描边才需要关（fontMaterial 实例级关键字，不影响其他跳字）
        if (_isOutlineActive && _outlineMat != null)
        {
            _outlineMat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            _isOutlineActive = false;
        }

        switch (style.kind)
        {
            case FloatStyleKind.Gradient:
                // 上下明暗渐变：跨度 ±45%（上端向白 / 下端向黑），保证 0.8s 跳字期内可分辨
                _gradientTop = Color.Lerp(style.color, Color.white, 0.45f);
                _gradientBottom = Color.Lerp(style.color, Color.black, 0.45f);
                text.enableVertexGradient = true;
                text.colorGradient = new VertexGradient(_gradientTop, _gradientTop, _gradientBottom, _gradientBottom);
                _isGradient = true;
                break;

            case FloatStyleKind.Outline:
                // 深色描边 = TMP UNDERLAY：SDF 膨胀一层深色底，文字本体保持原色
                if (_outlineMat == null)
                    _outlineMat = text.fontMaterial;   // getter 首次访问时实例化（每跳字对象一份）
                if (_outlineMat != null)
                {
                    _outlineMat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    _outlineMat.SetColor(ShaderUtilities.ID_UnderlayColor, OutlineBaseColor);
                    _outlineMat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.25f);   // 描边宽度（SDF 膨胀量）
                    _outlineMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);    // 硬边（不羽化）
                    _outlineMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                    _outlineMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
                    _isOutlineActive = true;
                }
                break;
        }
    }

    /// <summary>上漂 floatDistance 单位、duration 秒、alpha→0；shake=true 时叠加随机位移。
    /// 渐变态：alpha 写入渐变四角色（vertexGradient 开启时 text.color 不参与顶点着色，直接改 color 会失效）；
    /// 描边态：UNDERLAY 底色 alpha 与文字同步淡出（防"漏光"残影）。</summary>
    private IEnumerator Animate(FloatStyle style)
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * style.floatDistance;
        Color startColor = text.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        // Alpha 衰减起点（可配置）。config 为 null 时回退 0.5（与字段默认值一致），
        // 避免 NRE 中断协程导致对象池泄漏。
        var config = Resources.Load<GameConfig>("GameConfig");
        float fadeStartRatio = config != null ? config.floatAlphaFadeStart : 0.5f;
        float fadeStart = style.duration * fadeStartRatio;

        float elapsed = 0f;
        while (elapsed < style.duration)
        {
            float t = elapsed / style.duration;
            Vector3 p = Vector3.Lerp(startPos, endPos, t);
            if (style.shake)
                p += (Vector3)(Random.insideUnitCircle * 0.05f);
            transform.position = p;

            // Alpha 曲线：fadeStart 前保持不透明，之后线性淡出至 0。
            // 公式比较项 fadeStart 为秒值（duration×ratio），故用 elapsed（秒）而非归一化 t。
            float alpha = elapsed < fadeStart
                ? 1f
                : 1f - (elapsed - fadeStart) / (style.duration - fadeStart);
            alpha = Mathf.Clamp01(alpha);

            if (_isGradient)
            {
                // 渐变态：按 alpha 重建四角渐变色（上×2 / 下×2）
                var top = new Color(_gradientTop.r, _gradientTop.g, _gradientTop.b, alpha);
                var bottom = new Color(_gradientBottom.r, _gradientBottom.g, _gradientBottom.b, alpha);
                text.colorGradient = new VertexGradient(top, top, bottom, bottom);
            }
            else
            {
                Color c = startColor;
                c.a = alpha;
                text.color = c;
            }

            // 描边态：底色 alpha 同步淡出（文字与描边同速消失，不残留深色底影）
            if (_isOutlineActive && _outlineMat != null)
            {
                var oc = OutlineBaseColor;
                oc.a = alpha;
                _outlineMat.SetColor(ShaderUtilities.ID_UnderlayColor, oc);
            }

            // 面朝摄像机（billboard）
            transform.rotation = Camera.main.transform.rotation;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        // 终态归零（回池前的卫生处理：下一次 Show 的 ApplyStyle 会全量复位，此处兜底）
        if (_isGradient)
        {
            var top = new Color(_gradientTop.r, _gradientTop.g, _gradientTop.b, 0f);
            var bottom = new Color(_gradientBottom.r, _gradientBottom.g, _gradientBottom.b, 0f);
            text.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }
        else
        {
            text.color = endColor;
        }
        ReturnToPool();
    }

    /// <summary>归还对象池（池不存在时直接隐藏）</summary>
    private void ReturnToPool()
    {
        if (FloatingTextPool.Instance != null)
            FloatingTextPool.Instance.Return(this);
        else if (gameObject != null)
            gameObject.SetActive(false);
    }
}
