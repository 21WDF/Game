using UnityEngine;

/// <summary>元素 / 反应颜色映射器（静态工具类）。
/// 从 GameConfig 读色值，不硬编码。GameConfig 懒加载并缓存。
/// 供 PieceManager / BattleController 在跳字前查询元素伤害色；反应色本阶段预留给未来按反应类型着色。</summary>
public static class ElementColorMapper
{
    private static GameConfig _config;

    /// <summary>缓存的 GameConfig（首次访问懒加载）</summary>
    private static GameConfig Config
    {
        get
        {
            if (_config == null)
                _config = Resources.Load<GameConfig>("GameConfig");
            return _config;
        }
    }

    /// <summary>元素伤害颜色（火/水/雷/冰/物理；其他元素回退物理白色）</summary>
    public static Color GetElementDamageColor(ElementType element)
    {
        var c = Config;
        if (c == null) return Color.white;
        switch (element)
        {
            case ElementType.Fire: return c.floatDamageFire;
            case ElementType.Water: return c.floatDamageWater;
            case ElementType.Thunder: return c.floatDamageThunder;
            case ElementType.Ice: return c.floatDamageIce;
            default: return c.floatDamagePhysical; // None → 物理白
        }
    }

    /// <summary>带 GameConfig 重载（显式传配置；与无参版等价）</summary>
    public static Color GetElementDamageColor(ElementType element, GameConfig config)
    {
        var c = config ?? Config;
        if (c == null) return Color.white;
        switch (element)
        {
            case ElementType.Fire: return c.floatDamageFire;
            case ElementType.Water: return c.floatDamageWater;
            case ElementType.Thunder: return c.floatDamageThunder;
            case ElementType.Ice: return c.floatDamageIce;
            default: return c.floatDamagePhysical; // None → 物理白
        }
    }

    /// <summary>按伤害类型取色（物理白 / 魔法紫 / 真实金）。Config 缺失回退白。</summary>
    public static Color GetDamageKindColor(DamageKind kind)
    {
        var c = Config;
        if (c == null) return Color.white;
        switch (kind)
        {
            case DamageKind.Magical: return c.floatDamageMagical;
            case DamageKind.True: return c.floatDamageTrue;
            default: return c.floatDamagePhysical; // Physical → 物理白
        }
    }

    /// <summary>伤害跳字颜色统一入口（View 层城邦过滤）：
    /// element != None 且元素系统生效（元素城邦）→ 元素色；否则视为无元素 → 伤害类型默认色。
    /// 消费方：普攻路径 / ApplyIncomingDamage / 溅射路径的 ShowDamage 颜色。</summary>
    public static Color GetDamageColor(ElementType element, DamageKind kind)
    {
        if (element != ElementType.None && ElementReactionTable.ElementSystemActive)
            return GetElementDamageColor(element);
        return GetDamageKindColor(kind);
    }

    /// <summary>反应颜色（按反应类型；本阶段预留给未来按反应着色提示文字）。
    /// None 回退 floatReactionDamage 紫。</summary>
    public static Color GetReactionColor(ReactionType type)
    {
        var c = Config;
        if (c == null) return new Color(0.8f, 0.4f, 1f);
        switch (type)
        {
            case ReactionType.Vaporize: return c.floatReactionVaporize;
            case ReactionType.Melt: return c.floatReactionMelt;
            case ReactionType.Overload: return c.floatReactionOverload;
            case ReactionType.ElectroCharged: return c.floatReactionElectro;
            case ReactionType.Frozen: return c.floatReactionFrozen;
            case ReactionType.Superconduct: return c.floatReactionSuperconduct;
            default: return c.floatReactionDamage; // None → 紫兜底
        }
    }

    /// <summary>带 GameConfig 重载</summary>
    public static Color GetReactionColor(ReactionType type, GameConfig config)
    {
        var c = config ?? Config;
        if (c == null) return new Color(0.8f, 0.4f, 1f);
        switch (type)
        {
            case ReactionType.Vaporize: return c.floatReactionVaporize;
            case ReactionType.Melt: return c.floatReactionMelt;
            case ReactionType.Overload: return c.floatReactionOverload;
            case ReactionType.ElectroCharged: return c.floatReactionElectro;
            case ReactionType.Frozen: return c.floatReactionFrozen;
            case ReactionType.Superconduct: return c.floatReactionSuperconduct;
            default: return c.floatReactionDamage;
        }
    }

    /// <summary>反应类型中文名（蒸发/融化/超载/冻结/感电/超导）。None 返回空串。
    /// 供反应提示文本使用，避免输出英文枚举名。名称为固定 UI 文案，不进 GameConfig。</summary>
    public static string GetReactionName(ReactionType type)
    {
        switch (type)
        {
            case ReactionType.Vaporize: return "蒸发";
            case ReactionType.Melt: return "融化";
            case ReactionType.Overload: return "超载";
            case ReactionType.Frozen: return "冻结";
            case ReactionType.ElectroCharged: return "感电";
            case ReactionType.Superconduct: return "超导";
            default: return ""; // None
        }
    }
}
