using UnityEngine;

/// <summary>浮动文字样式预设库（静态）。
/// 动画参数（duration / floatDistance / fontSize）从 GameConfig 读取；color 由预设或调用方传入。
/// 每个样式提供两个重载：无参版内部 Resources.Load 兜底（供零散调用方），GameConfig 版由已缓存配置的池直传（省去重复加载）。
/// shake 仅暴击启用，暂不进 config。</summary>
public static class FloatStyles
{
    // ---- 颜色常量（硬编码；颜色映射见 ElementColorMapper，不在本类调参）----
    private static readonly Color ReactionColor = new Color(0.8f, 0.4f, 1f);
    private static readonly Color HealColor = new Color(0.3f, 1f, 0.3f);
    private static readonly Color ArmorColor = new Color(0.3f, 0.5f, 1f);
    private static readonly Color CritColor = new Color(1f, 0.85f, 0.2f);

    // ---- 兜底默认值（config 为 null 时使用；与 GameConfig 字段默认值保持一致）----
    private const float DefaultPhysicalDuration = 0.8f;
    private const float DefaultPhysicalDistance = 1.5f;
    private const float DefaultPhysicalFontSize = 10f;

    private const float DefaultReactionDuration = 1.0f;
    private const float DefaultReactionDistance = 2.0f;
    private const float DefaultReactionFontSize = 12f;

    private const float DefaultHealDuration = 0.6f;
    private const float DefaultHealDistance = 0.8f;
    private const float DefaultHealFontSize = 9f;

    private const float DefaultArmorDuration = 0.8f;
    private const float DefaultArmorDistance = 0.8f;
    private const float DefaultArmorFontSize = 8f;

    private const float DefaultCritDuration = 1.0f;
    private const float DefaultCritDistance = 2.5f;
    private const float DefaultCritFontSize = 16f;

    /// <summary>懒加载 GameConfig（无参重载兜底用；Unity 对 Resources.Load 有缓存，重复调用开销极低）</summary>
    private static GameConfig LoadConfig() => Resources.Load<GameConfig>("GameConfig");

    // ==========================================
    //  伤害（按伤害类型选形态：物理=纯色 / 魔法=渐变 / 真实=描边；颜色由调用方传入，与形态正交）
    // ==========================================
    /// <summary>伤害跳字样式。无参版 Resources.Load 兜底。</summary>
    public static FloatStyle Damage(Color color, DamageKind kind)
        => Damage(color, kind, LoadConfig());

    /// <summary>伤害跳字样式（显式传 config；池已缓存时用此重载省去重复加载）。
    /// 动画参数（时长/上漂/字号）复用物理伤害档；形态按伤害类型分流：
    /// Physical→纯色块 / Magical→上下明暗渐变 / True→深色描边。</summary>
    public static FloatStyle Damage(Color color, DamageKind kind, GameConfig config)
    {
        float dur = config != null ? config.floatDurationPhysical : DefaultPhysicalDuration;
        float dist = config != null ? config.floatDistancePhysical : DefaultPhysicalDistance;
        float font = config != null ? config.floatFontSizePhysical : DefaultPhysicalFontSize;
        FloatStyleKind styleKind = kind == DamageKind.Magical ? FloatStyleKind.Gradient
            : kind == DamageKind.True ? FloatStyleKind.Outline
            : FloatStyleKind.Plain;
        return new FloatStyle(color, dur, dist, font, false, styleKind);
    }

    // ==========================================
    //  物理 / 元素伤害（旧入口，等价 Damage(color, Physical)；保留供既有调用方）
    // ==========================================
    /// <summary>物理 / 元素伤害跳字（接收元素颜色参数）。无参版 Resources.Load 兜底。</summary>
    public static FloatStyle PhysicalDamage(Color elementColor)
        => PhysicalDamage(elementColor, LoadConfig());

    /// <summary>物理 / 元素伤害跳字（显式传 config；池已缓存时用此重载省去重复加载）</summary>
    public static FloatStyle PhysicalDamage(Color elementColor, GameConfig config)
        => Damage(elementColor, DamageKind.Physical, config);

    // ==========================================
    //  反应伤害
    // ==========================================
    /// <summary>反应伤害跳字（紫色）。无参版 Resources.Load 兜底。</summary>
    public static FloatStyle ReactionDamage()
        => ReactionDamage(LoadConfig());

    /// <summary>反应伤害跳字（显式传 config）</summary>
    public static FloatStyle ReactionDamage(GameConfig config)
    {
        float dur = config != null ? config.floatDurationReaction : DefaultReactionDuration;
        float dist = config != null ? config.floatDistanceReaction : DefaultReactionDistance;
        float font = config != null ? config.floatFontSizeReaction : DefaultReactionFontSize;
        return new FloatStyle(ReactionColor, dur, dist, font, false);
    }

    // ==========================================
    //  回血
    // ==========================================
    /// <summary>回血跳字（绿色）。无参版 Resources.Load 兜底。</summary>
    public static FloatStyle Heal()
        => Heal(LoadConfig());

    /// <summary>回血跳字（显式传 config）</summary>
    public static FloatStyle Heal(GameConfig config)
    {
        float dur = config != null ? config.floatDurationHeal : DefaultHealDuration;
        float dist = config != null ? config.floatDistanceHeal : DefaultHealDistance;
        float font = config != null ? config.floatFontSizeHeal : DefaultHealFontSize;
        return new FloatStyle(HealColor, dur, dist, font, false);
    }

    // ==========================================
    //  护甲
    // ==========================================
    /// <summary>护甲跳字（蓝色）。无参版 Resources.Load 兜底。</summary>
    public static FloatStyle Armor()
        => Armor(LoadConfig());

    /// <summary>护甲跳字（显式传 config）</summary>
    public static FloatStyle Armor(GameConfig config)
    {
        float dur = config != null ? config.floatDurationArmor : DefaultArmorDuration;
        float dist = config != null ? config.floatDistanceArmor : DefaultArmorDistance;
        float font = config != null ? config.floatFontSizeArmor : DefaultArmorFontSize;
        return new FloatStyle(ArmorColor, dur, dist, font, false);
    }

    // ==========================================
    //  暴击
    // ==========================================
    /// <summary>暴击跳字（金色，抖动）。无参版 Resources.Load 兜底。</summary>
    public static FloatStyle Crit()
        => Crit(LoadConfig());

    /// <summary>暴击跳字（显式传 config）</summary>
    public static FloatStyle Crit(GameConfig config)
    {
        float dur = config != null ? config.floatDurationCrit : DefaultCritDuration;
        float dist = config != null ? config.floatDistanceCrit : DefaultCritDistance;
        float font = config != null ? config.floatFontSizeCrit : DefaultCritFontSize;
        return new FloatStyle(CritColor, dur, dist, font, true);
    }
}
