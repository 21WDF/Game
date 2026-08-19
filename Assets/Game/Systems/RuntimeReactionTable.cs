using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时反应表（懒加载单例，纯 C# 类）—— 管理反应配置的运行时可变状态。
///
/// 两层可变状态：
///   1. _overrides：道具临时覆盖某对元素的反应配置（指定持续回合，回合结束自动递减/清除）
///   2. _globalModifiers：全局修饰器链（供未来扩展：天气/地图效果等；装备修饰器不走此链）
///
/// 解析流程 Resolve(base, trigger, pieceModifiers)：
///   1. 先查 _overrides → 命中直接返回（不经过修饰器链，覆盖优先级最高）
///   2. 未命中查 _database 基础值 → 无匹配返回 None/1.0x
///   3. 依次过 _globalModifiers 全局修饰器链
///   4. 依次过 pieceModifiers 棋子修饰器链（从攻击方装备收集）
///   5. 返回最终 ReactionConfig
///
/// 装备被动实现 IReactionModifier 时不注册全局链（用户确认：仅棋子修饰器），
/// 由 PieceManager.AttackPiece 从攻击方 EquippedItems 收集后作为 pieceModifiers 传入。
/// </summary>
public class RuntimeReactionTable
{
    private static RuntimeReactionTable _instance;

    /// <summary>懒加载单例：首次访问时从 Resources 加载 ReactionDatabase</summary>
    public static RuntimeReactionTable Instance
    {
        get
        {
            if (_instance == null)
                _instance = new RuntimeReactionTable();
            return _instance;
        }
    }

    private ReactionDatabaseSO _database;

    // ---- 临时覆盖（道具用）----
    private readonly Dictionary<(ElementType, ElementType), OverrideEntry> _overrides = new();

    /// <summary>覆盖条目：配置 + 剩余回合</summary>
    private struct OverrideEntry
    {
        public ElementReactionTable.ReactionConfig config;
        public int durationTurns;
    }

    // ---- 全局修饰器链（未来扩展用；装备修饰器不走此链）----
    private readonly List<IReactionModifier> _globalModifiers = new();

    private RuntimeReactionTable()
    {
        _database = Resources.Load<ReactionDatabaseSO>("ReactionDatabase");
        if (_database == null)
            Debug.LogError("[RuntimeReactionTable] ReactionDatabase 加载失败！请在 Resources 文件夹中创建 ReactionDatabase.asset（Create > Chess > Reaction Database）");
    }

    // ==========================================
    //  核心：解析最终反应配置
    // ==========================================
    /// <summary>
    /// 解析最终反应配置：override → 数据库基础值 → 全局修饰器链 → 棋子修饰器链
    /// </summary>
    /// <param name="baseElement">底元素（防御方附着元素）</param>
    /// <param name="trigger">触发元素（攻击方先天元素）</param>
    /// <param name="pieceModifiers">棋子修饰器链（从攻击方装备收集；可为 null）</param>
    public ElementReactionTable.ReactionConfig Resolve(
        ElementType baseElement, ElementType trigger,
        IReadOnlyList<IReactionModifier> pieceModifiers)
    {
        // 1. 临时覆盖优先（不经过修饰器链）
        if (_overrides.TryGetValue((baseElement, trigger), out var overrideEntry))
            return overrideEntry.config;

        // 2. 查数据库基础值
        ElementReactionTable.ReactionConfig config;
        if (_database != null && _database.TryGetEntry(baseElement, trigger, out var entry))
        {
            config = entry.config;
        }
        else
        {
            config = new ElementReactionTable.ReactionConfig
            {
                Type = ReactionType.None,
                DamageMultiplier = 1.0f
            };
        }

        // 3. 全局修饰器链
        foreach (var mod in _globalModifiers)
            config = mod.Modify(config);

        // 4. 棋子修饰器链
        if (pieceModifiers != null)
        {
            for (int i = 0; i < pieceModifiers.Count; i++)
            {
                if (pieceModifiers[i] != null)
                    config = pieceModifiers[i].Modify(config);
            }
        }

        return config;
    }

    // ==========================================
    //  临时覆盖管理（道具用）
    // ==========================================
    /// <summary>设置临时覆盖：指定元素对的反应配置在 durationTurns 回合内被覆盖</summary>
    public void SetOverride(ElementType baseElement, ElementType trigger,
        ElementReactionTable.ReactionConfig config, int durationTurns)
    {
        _overrides[(baseElement, trigger)] = new OverrideEntry
        {
            config = config,
            durationTurns = durationTurns
        };
    }

    /// <summary>回合结束调用：递减所有覆盖的剩余回合，清除过期项</summary>
    public void TickOverrides()
    {
        if (_overrides.Count == 0) return;

        var expired = new List<(ElementType, ElementType)>();
        foreach (var kvp in _overrides)
        {
            var entry = kvp.Value;
            entry.durationTurns--;
            if (entry.durationTurns <= 0)
                expired.Add(kvp.Key);
            else
                _overrides[kvp.Key] = entry;
        }
        foreach (var key in expired)
            _overrides.Remove(key);
    }

    // ==========================================
    //  全局修饰器管理（未来扩展用）
    // ==========================================
    public void AddGlobalModifier(IReactionModifier modifier)
    {
        if (modifier != null && !_globalModifiers.Contains(modifier))
            _globalModifiers.Add(modifier);
    }

    public void RemoveGlobalModifier(IReactionModifier modifier)
    {
        _globalModifiers.Remove(modifier);
    }
}
