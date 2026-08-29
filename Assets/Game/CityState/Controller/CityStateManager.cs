using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 城邦控制器（CityState 模块的 Controller）—— 单例 MonoBehaviour，持有 <see cref="CityStateModel"/>。
/// 挂载在场景中的 CityStateManager GameObject 上（手动创建，对标 GoldManager/TurnManager）。
///
/// 城邦系统一期职责（仅框架，无具体城邦机制）：
///   1) 持有本局生效城邦（存于 Model，不挂任何 UI），变化时触发 OnCityStateChanged 事件；
///   2) 城邦选择流程：双方各提交 4 个心仪城邦 → 纯函数求交集 + roll 定 1 个。
///      随机入口收敛到本类的 <see cref="ResolveCityState"/>（将来网络化只需替换此入口为同步 seed）；
///   3) 商店过滤查询 <see cref="ShouldShowEquipment"/>（供 UI_ShopPanel 城邦 Tab 使用）；
///   4) 持有城邦专属机制实例（默认空实现 <see cref="NullCityStateMechanism"/>，具体机制二期接入）。
/// </summary>
public class CityStateManager : MonoBehaviour
{
    public static CityStateManager Instance { get; private set; }

    // ---- MVC 分层（对标 GoldManager）----
    private CityStateModel _model;
    /// <summary>城邦数据模型（UI 通过此订阅 OnCityStateChanged 事件）</summary>
    public CityStateModel Model => _model;

    // ---- 城邦专属机制骨架（一期：空实现，无任何行为）----
    private ICityStateMechanism _mechanism = new NullCityStateMechanism();
    /// <summary>当前城邦专属机制（默认空实现；二期按城邦替换为具体机制）</summary>
    public ICityStateMechanism CurrentMechanism => _mechanism;

    // ---- 双方已提交的心仪城邦选择 ----
    private readonly Dictionary<PlayerSide, List<CityStateKind>> _selections = new();

    /// <summary>本局生效城邦（None = 未确定 / 管理器未初始化）</summary>
    public CityStateKind CurrentCityState => _model != null ? _model.Current : CityStateKind.None;

    /// <summary>城邦生效查询（各专属机制的统一过滤入口）：当前城邦 == 指定城邦才返回 true。
    /// None 永不匹配（未确定城邦 = 所有专属机制不生效）。机制入口处自查，判断不散落到调用点。</summary>
    public bool IsActive(CityStateKind kind)
        => kind != CityStateKind.None && CurrentCityState == kind;

    /// <summary>城邦变化事件（UI/机制订阅入口；参数 = 旧城邦, 新城邦）</summary>
    public event System.Action<CityStateKind, CityStateKind> OnCityStateChanged
    {
        add { if (_model != null) _model.OnCityStateChanged += value; }
        remove { if (_model != null) _model.OnCityStateChanged -= value; }
    }

    /// <summary>全部可选择的城邦（6 个，不含 None）</summary>
    public static readonly CityStateKind[] AllCityStates =
    {
        CityStateKind.Trade,        // 贸易
        CityStateKind.Merriment,    // 欢愉
        CityStateKind.Monsoon,      // 季风
        CityStateKind.Occult,       // 邪疑
        CityStateKind.War,          // 战争
        CityStateKind.Element       // 元素
    };

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _model = new CityStateModel();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==========================================
    //  城邦选择流程（一期：GameFlowController 临时占位调用；二期：选择 UI 调用，管线不变）
    // ==========================================
    /// <summary>一方提交 4 个心仪城邦；双方都提交后自动「求交集 + 随机」定出本局城邦。
    /// 返回 true = 本局城邦已确定（本次调用触发了结算）。</summary>
    public bool SubmitSelection(PlayerSide side, IReadOnlyList<CityStateKind> choices)
    {
        if (_model == null) return false;
        _selections[side] = choices != null ? new List<CityStateKind>(choices) : new List<CityStateKind>();
        return TryResolveFromSelections();
    }

    /// <summary>从双方已提交的选择结算本局城邦（双方均已提交才执行；重复调用会重新随机）</summary>
    public bool TryResolveFromSelections()
    {
        if (_model == null) return false;
        if (!_selections.TryGetValue(PlayerSide.P1, out var p1) || !_selections.TryGetValue(PlayerSide.P2, out var p2))
            return false;

        CityStateKind resolved = ResolveCityState(p1, p2);
        if (resolved == CityStateKind.None)
        {
            Debug.LogWarning("[CityStateManager] 双方心仪城邦无重合，本局城邦未能确定（保持 None）");
            return false;
        }

        SetCityState(resolved);

        // 机制骨架钩子①：城邦确定时通知（一期空实现，无行为）
        _mechanism?.OnGameStart(resolved);
        return true;
    }

    /// <summary>唯一随机入口：输入双方选择，内部产生随机 roll 并调用纯函数 Resolve。
    /// 将来在线 PVP 只需把此方法内部替换为同步 seed 的确定性取 roll 方式，纯函数与流程零改动。</summary>
    public CityStateKind ResolveCityState(IReadOnlyList<CityStateKind> p1Choices, IReadOnlyList<CityStateKind> p2Choices)
    {
        int roll = Random.Range(int.MinValue, int.MaxValue);
        return CityStateModel.Resolve(p1Choices, p2Choices, roll);
    }

    // ==========================================
    //  城邦写入入口（唯一）
    // ==========================================
    /// <summary>直接设置本局城邦（绕过选择流程的兜底入口；不触发机制 OnGameStart，该钩子仅在选择管线中触发）</summary>
    public void SetCityState(CityStateKind kind)
    {
        if (_model == null) return;
        _model.Set(kind);
        Debug.Log($"[CityStateManager] 本局城邦：{kind}");
    }

    // ==========================================
    //  机制骨架钩子转发（一期空实现；由 TurnManager 回合起止调用，二期机制即插即用）
    // ==========================================
    /// <summary>回合开始：通知城邦机制（null 安全，管理器不在场景时静默跳过）</summary>
    public void NotifyTurnStart(PlayerSide activePlayer) => _mechanism?.OnTurnStart(activePlayer);

    /// <summary>回合结束：通知城邦机制（null 安全；endingPlayer = 刚结束行动的一方）</summary>
    public void NotifyTurnEnd(PlayerSide endingPlayer) => _mechanism?.OnTurnEnd(endingPlayer);

    // ==========================================
    //  商店过滤查询（供 UI_ShopPanel 城邦 Tab 使用）
    // ==========================================
    /// <summary>某件装备在当前城邦下是否应展示：
    /// 非城邦档（tier != CityState）→ 恒显示（通用装备零影响红线）；
    /// 城邦档 + 归属 None → 任何城邦都显示（通用兜底）；
    /// 城邦档 + 归属 == 当前城邦 → 显示；归属其他城邦 → 不显示。</summary>
    public bool ShouldShowEquipment(EquipmentData data)
    {
        if (data == null) return false;
        if (data.tier != EquipmentTier.CityState) return true;
        return data.cityState == CityStateKind.None
            || data.cityState == CurrentCityState;
    }

    // ==========================================
    //  重置（用于重新开局）
    // ==========================================
    /// <summary>清空双方选择并重置本局城邦为 None</summary>
    public void ResetForNewGame()
    {
        _selections.Clear();
        _model?.Reset();
    }
}
