using UnityEngine;

/// <summary>
/// 元素之城管理器 —— 对标 MonsoonManager/WarCityManager 的单例管理器。
/// 挂载在场景中的 ElementCityManager GameObject 上（可与其他城邦 Manager 放一起）。
///
/// 职责：
///   1. 元素城邦统一生效判定入口（收敛原 PieceModel.DyeingActive 与
///      ElementReactionTable.ElementSystemActive 两处重复的城邦判断——判定只在此处写一次）：
///       · ElementSystemActive：元素系统本体（反应 / 附着 / 元素图标）
///       · DyeingActive：护盾染色（= 元素城邦生效 且 染色总闸开）
///   2. 承载元素城邦配置资产（ElementCityConfig：护盾染色开关及未来元素机制参数）。
///
/// 降级安全（元素是核心玩法，静默失效比报错更糟）：
///   · 生效判定为纯静态查询（只依赖 CityStateManager），管理器未挂到场景时判定照常工作、不失效；
///   · 配置读取：管理器在场景 → 三级回退（Inspector → Resources/ElementCityConfig → 运行时默认值）；
///     管理器不在场景 → 静态回退（Resources/ElementCityConfig → 运行时默认值，缓存避免高频查询反复 Load）。
/// </summary>
public class ElementCityManager : MonoBehaviour
{
    public static ElementCityManager Instance { get; private set; }

    [Header("配置资产（空则读 Resources/ElementCityConfig，再空则用运行时默认值）")]
    [Tooltip("Create > Chess > Element City Config 创建后拖入；改数值无需改代码")]
    public ElementCityConfig config;

    private ElementCityConfig _runtimeConfig;
    /// <summary>管理器未挂载时的静态回退配置（缓存一次，修复原 DyeingActive 每次查询都 Resources.Load 的性能问题）</summary>
    private static ElementCityConfig _fallbackConfig;

    /// <summary>生效配置（懒加载：Inspector → Resources → 运行时默认实例）</summary>
    private ElementCityConfig Config
    {
        get
        {
            if (_runtimeConfig == null)
            {
                _runtimeConfig = config != null ? config
                    : (Resources.Load<ElementCityConfig>("ElementCityConfig") ?? ScriptableObject.CreateInstance<ElementCityConfig>());
            }
            return _runtimeConfig;
        }
    }

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==========================================
    //  元素城邦生效判定（统一入口；纯静态，不依赖管理器实例）
    // ==========================================
    /// <summary>元素系统生效判定（元素城邦本体的城邦过滤）：仅当前城邦 == 元素之城时元素系统生效；
    /// 其他城邦下不反应、不附着（退回「无元素」基线）。
    /// 消费方：ElementReactionTable.ElementSystemActive（转发）及 UI 元素图标等。</summary>
    public static bool ElementSystemActive
        => CityStateManager.Instance != null && CityStateManager.Instance.IsActive(CityStateKind.Element);

    /// <summary>护盾染色总闸（缓存读取）：管理器在场景 → 其三级回退配置；
    /// 管理器不在场景 → Resources/ElementCityConfig → 运行时默认值（= 原取值）</summary>
    private static bool ShieldDyeingEnabled
    {
        get
        {
            if (Instance != null) return Instance.Config.shieldElementDyeingEnabled;
            if (_fallbackConfig == null)
            {
                _fallbackConfig = Resources.Load<ElementCityConfig>("ElementCityConfig")
                                  ?? ScriptableObject.CreateInstance<ElementCityConfig>();
            }
            return _fallbackConfig.shieldElementDyeingEnabled;
        }
    }

    /// <summary>染色机制生效判定（统一入口）：染色总闸开 且 当前城邦 == 元素之城。
    /// 语义与原 PieceModel.DyeingActive 完全一致（两个条件不变，只是取值来源与判定收敛到本管理器）。
    /// 消费方：PieceModel.DyeingActive（转发；BlocksElementAttachment / TryDyeShieldElement 内部使用）。</summary>
    public static bool DyeingActive => ElementSystemActive && ShieldDyeingEnabled;
}
