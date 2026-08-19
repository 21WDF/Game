using UnityEngine;

/// <summary>
/// 行动点控制器（ActionPoint 模块的 Controller）—— 单例 MonoBehaviour，持有 <see cref="APModel"/>。
/// 挂载在场景中的 APManager GameObject 上。
///
/// Phase 3 MVC 改造：
///   - 纯数据迁移到 <see cref="APModel"/>（无 Unity 依赖）。
///   - 本类负责 Unity 生命周期 + 配置注入（从 Inspector 读取配置写入 Model）。
///   - UI 通过 <see cref="Model"/> 订阅 OnAPChanged 事件，不再每帧轮询。
///   - 保留旧 API（GetAP/HasAP/RefillAP/ConsumeAP）作为转发。
/// </summary>
public class APManager : MonoBehaviour
{
    public static APManager Instance { get; private set; }

    // ---- MVC 分层 ----
    private APModel _model;
    private GameConfig _config;
    /// <summary>行动点数据模型（UI 通过此订阅事件）</summary>
    public APModel Model => _model;

    // ---- 单例 ----
    // GameConfig 在 Awake 加载；Model 构造放在 Start（确保单例就绪顺序）。
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 加载全局配置（AP 参数收拢于此）
        _config = Resources.Load<GameConfig>("GameConfig");
        if (_config == null)
        {
            Debug.LogError("[APManager] GameConfig 加载失败！请确保 Assets/Game/Resources/GameConfig.asset 存在。", this);
            return;
        }
    }

    private void Start()
    {
        if (_config == null) return; // Awake 已报错

        _model = new APModel
        {
            BaseAPPerTurn = _config.baseAPPerTurn,
            MaxAPCap = _config.maxAPCap
        };
        _model.InitAP(PlayerSide.P1, 0);
        _model.InitAP(PlayerSide.P2, 0);
    }

    // ==========================================
    //  兼容旧 API 的转发方法
    // ==========================================
    public int GetAP(PlayerSide side) => _model.GetAP(side);

    public void RefillAP(PlayerSide side) => _model.RefillAP(side);

    public bool HasAP(PlayerSide side, int amount) => _model.HasAP(side, amount);

    public bool ConsumeAP(PlayerSide side, int amount)
    {
        bool ok = _model.TryConsumeAP(side, amount);
        if (ok)
            Debug.Log($"[APManager] {(side == PlayerSide.P1 ? "P1" : "P2")} 消耗 {amount} AP，剩余 {_model.GetAP(side)}");
        return ok;
    }
}
