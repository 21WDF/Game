using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 城邦控制器（CityState 模块的 Controller）—— 单例 MonoBehaviour，持有 <see cref="CityStateModel"/>。
/// 挂载在场景中的 CityStateManager GameObject 上（手动创建，对标 GoldManager/TurnManager）。
///
/// 城邦系统一期职责（仅框架，无具体城邦机制）：
///   1) 持有本局生效城邦（存于 Model，不挂任何 UI），变化时触发 OnCityStateChanged 事件；
///   2) 城邦选择流程：大厅 UI_CityStateExclusion 记录双方心仪城邦（本地热座轮流；联机由网络同步），
///      「开始游戏」点击后经 <see cref="ResolvePendingSelections"/> 结算。随机入口收敛到
///      <see cref="ResolveCityState"/>（将来网络化只需替换此入口为同步 seed）；
///   3) 商店过滤查询 <see cref="ShouldShowEquipment"/>（供 UI_ShopPanel 城邦 Tab 使用）；
///   4) 持有城邦专属机制实例（默认空实现 <see cref="NullCityStateMechanism"/>，具体机制二期接入）。
/// 联机预留接入点（UI 只依赖这三个入口，接入网络时 UI 零改动）：
///   ① 本地提交入口 <see cref="SubmitSelection"/>；② 双方提交状态查询 <see cref="HasBothSubmitted"/>；
///   ③ 结算入口 <see cref="ResolvePendingSelections"/>（内部唯一随机点 <see cref="ResolveCityState"/>）。
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
    //  城邦选择流程（一期：大厅 UI_CityStateExclusion 调用；管线预留联机接入点 ①②③）
    // ==========================================
    /// <summary>① 本地提交入口（UI 唯一调用）：记录一方的心仪城邦选择，**不结算**（结算后移至「开始游戏」点击后）。
    /// 联机预留：改为把本地选择上报网络层（对方提交状态由网络同步），本方法签名与调用方零改动。</summary>
    public void SubmitSelection(PlayerSide side, IReadOnlyList<CityStateKind> choices)
    {
        if (_model == null) return;
        _selections[side] = choices != null ? new List<CityStateKind>(choices) : new List<CityStateKind>();
        Debug.Log($"[CityStateManager] {side} 已提交城邦选择（{_selections[side].Count} 个）");
    }

    /// <summary>② 双方提交状态查询（统一查询；本地实现 = 该方选择已记录）。
    /// 联机预留：实现改为网络同步的对方提交状态——UI 不感知对方是否在同一台机器。</summary>
    public bool HasSideSubmitted(PlayerSide side) => _selections.ContainsKey(side);

    /// <summary>双方是否都已提交（「开始游戏」的可用条件；UI 只查不存）</summary>
    public bool HasBothSubmitted => HasSideSubmitted(PlayerSide.P1) && HasSideSubmitted(PlayerSide.P2);

    /// <summary>③ 结算入口（唯一随机入口的对外封装）：读取双方已存选择 → <see cref="ResolveCityState"/> → 返回本局城邦。
    /// 由 UI_MainMenu 在「开始游戏」点击后调用；不在本方法之外产生任何随机。
    /// 联机预留：ResolveCityState 内部换网络权威 seed 的确定性 roll，本方法与调用方零改动。</summary>
    public CityStateKind ResolvePendingSelections()
    {
        if (_model == null) return CityStateKind.None;
        if (!_selections.TryGetValue(PlayerSide.P1, out var p1) || !_selections.TryGetValue(PlayerSide.P2, out var p2))
            return CityStateKind.None;
        return ResolveCityState(p1, p2);
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
