using UnityEngine;

/// <summary>跳字形态（View 层）——与颜色正交：
/// Plain=纯色块（物理伤害默认）；Gradient=上下明暗渐变（魔法伤害默认）；Outline=深色描边（真实伤害默认）。
/// 回血/护甲/反应提示等非伤害样式固定 Plain。</summary>
public enum FloatStyleKind
{
    Plain,      // 纯色（TMP 直接着色）
    Gradient,   // 渐变（TMP 顶点渐变：上端提亮 / 下端压暗）
    Outline,    // 描边（TMP UNDERLAY：SDF 膨胀一层深色底，文字本体保持原色）
}

/// <summary>浮动文字样式参数（伤害 / 反应 / 回血 / 护甲 / 暴击 共用）。
/// 本阶段不暴露到 Inspector：由 <see cref="FloatStyles"/> 静态预设库提供实例，后续可收进 GameConfig。</summary>
[System.Serializable]
public struct FloatStyle
{
    /// <summary>文字颜色（元素色或伤害类型色，由调用方经 ElementColorMapper 取得）</summary>
    public Color color;

    /// <summary>上漂 + 淡出总时长（秒）</summary>
    public float duration;

    /// <summary>上漂距离（世界单位）</summary>
    public float floatDistance;

    /// <summary>字号（TMP fontSize）</summary>
    public float fontSize;

    /// <summary>是否带抖动（暴击用）</summary>
    public bool shake;

    /// <summary>跳字形态（伤害类型维度：物理=纯色 / 魔法=渐变 / 真实=描边）；默认纯色</summary>
    public FloatStyleKind kind;

    public FloatStyle(Color color, float duration, float floatDistance, float fontSize, bool shake,
        FloatStyleKind kind = FloatStyleKind.Plain)
    {
        this.color = color;
        this.duration = duration;
        this.floatDistance = floatDistance;
        this.fontSize = fontSize;
        this.shake = shake;
        this.kind = kind;
    }
}
