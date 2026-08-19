/// <summary>
/// 能量模型（Piece 模块的子 Model）—— 纯数据 + 事件，无 Unity 依赖。
/// 参照 GoldModel 写法：持有当前/最大能量，提供获取/消耗接口，变化时触发事件。
///
/// 设计要点：
///   1) 仅当棋子 PieceData.ultimateConfig 非空时由 PieceModel 构造；无大招配置的棋子 Energy=null。
///   2) 事件驱动：UI 通过订阅 OnEnergyChanged 替代每帧轮询；获取/消耗均立即触发。
///   3) TryConsume 仅在满能时成功并清零，避免半能消耗。
/// </summary>
public class EnergyModel
{
    // ---- 配置（构造注入）----
    public int MaxEnergy { get; }

    // ---- 运行时数据 ----
    public int CurrentEnergy { get; private set; }
    public bool IsFull => CurrentEnergy >= MaxEnergy;

    // ---- 事件 ----
    /// <summary>能量变化时触发（新当前能量）—— 获取/消耗均触发</summary>
    public event System.Action<int> OnEnergyChanged;

    public EnergyModel(int maxEnergy)
    {
        MaxEnergy = maxEnergy > 0 ? maxEnergy : 100;
        CurrentEnergy = 0;
    }

    // ==========================================
    //  状态变更（由 PieceManager 调用）
    // ==========================================
    /// <summary>获取能量（不超过 MaxEnergy；非正数忽略）</summary>
    public void Gain(int amount)
    {
        if (amount <= 0) return;
        CurrentEnergy = System.Math.Min(CurrentEnergy + amount, MaxEnergy);
        OnEnergyChanged?.Invoke(CurrentEnergy);
    }

    /// <summary>满能时消耗全部能量并返回 true；未满返回 false（不改变能量）</summary>
    public bool TryConsume()
    {
        if (!IsFull) return false;
        CurrentEnergy = 0;
        OnEnergyChanged?.Invoke(CurrentEnergy);
        return true;
    }

    /// <summary>满能时消耗指定数量能量并返回 true（保留剩余）；未满返回 false（不改变能量）。
    /// 部分能量型大招（IPartialEnergyUltimate，如菲林斯强化态）用；释放条件仍是满能。
    /// amount ≥ 当前能量时清零（等同全清）；amount ≤ 0 时不消耗（能量保持满——配置时须 &gt;0 才有意义）。</summary>
    public bool TryConsume(int amount)
    {
        if (!IsFull) return false;
        CurrentEnergy = System.Math.Max(0, CurrentEnergy - System.Math.Max(0, amount));
        OnEnergyChanged?.Invoke(CurrentEnergy);
        return true;
    }
}
