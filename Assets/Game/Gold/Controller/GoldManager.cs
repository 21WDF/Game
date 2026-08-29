using UnityEngine;

/// <summary>
/// 金币控制器（Gold 模块的 Controller）—— 单例 MonoBehaviour，持有 <see cref="GoldModel"/>。
/// 挂载在场景中的 GoldManager GameObject 上。
///
/// Phase 3 MVC 改造：
///   - 纯数据迁移到 <see cref="GoldModel"/>（无 Unity 依赖）。
///   - 本类负责 Unity 生命周期 + 配置注入 + 伤害收入的入口（接收 PieceModel 参数）。
///   - UI 通过 <see cref="Model"/> 订阅 OnGoldSettled 事件，不再每帧轮询。
///   - 保留旧 API 作为转发。
/// </summary>
public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance { get; private set; }

    // ---- MVC 分层 ----
    private GoldModel _model;
    private GameConfig _config;
    /// <summary>金币数据模型（UI 通过此订阅事件）</summary>
    public GoldModel Model => _model;

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

        // 加载全局配置（金币参数收拢于此）
        _config = Resources.Load<GameConfig>("GameConfig");
        if (_config == null)
        {
            Debug.LogError("[GoldManager] GameConfig 加载失败！请确保 Assets/Game/Resources/GameConfig.asset 存在。", this);
            return;
        }
    }

    private void Start()
    {
        if (_config == null) return; // Awake 已报错

        _model = new GoldModel
        {
            GoldPerDamage = _config.goldPerDamage,
            GoldPerDamageReceived = _config.goldPerDamageReceived,
            StartingGold = _config.startingGold,
            OverdraftFloor = _config.overdraftFloor   // 透支下限（贸易之城·一期，可配置防硬编码）
        };
        _model.InitGold(PlayerSide.P1, _config.startingGold);
        _model.InitGold(PlayerSide.P2, _config.startingGold);
    }

    // ==========================================
    //  收入记录（Phase 2b：参数为 PieceModel）
    // ==========================================
    public void OnDamageDealt(PieceModel dealer, int damage)
    {
        if (dealer == null || _model == null) return;
        int gold = _model.CalcGoldFromDamage(damage, _config.goldPerDamage);
        _model.AddGold(dealer.Owner, gold);
    }

    public void OnDamageReceived(PieceModel receiver, int damage)
    {
        if (receiver == null || _model == null) return;
        int gold = _model.CalcGoldFromDamage(damage, _config.goldPerDamageReceived);
        _model.AddGold(receiver.Owner, gold);
    }

    // ==========================================
    //  兼容旧 API 的转发方法
    // ==========================================
    public int GetGold(PlayerSide side) => _model.GetGold(side);

    public bool TrySpendGold(PlayerSide side, int amount) => _model.TrySpendGold(side, amount);

    /// <summary>透支消费（贸易之城·一期）：允许金币扣到透支下限（可为负）；金币充足时行为与 TrySpendGold 一致</summary>
    public bool TrySpendGoldWithOverdraft(PlayerSide side, int amount)
        => _model != null && _model.TrySpendGoldWithOverdraft(side, amount);

    /// <summary>透支下限（GameConfig 注入；出价支付能力校验等查询用）</summary>
    public int OverdraftFloor => _model != null ? _model.OverdraftFloor : 0;

    /// <summary>增加金币（棋盘道具取消使用时退还等）。UI 通过 OnGoldSettled 自动刷新。</summary>
    public void AddGold(PlayerSide side, int amount) => _model?.AddGold(side, amount);
}
