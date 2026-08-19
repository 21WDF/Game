using UnityEngine;

/// <summary>
/// 英灵幡被动 —— 同阵营棋子死亡时：立刻回蓝 instantEnergy，
/// 之后每回合回蓝 regenEnergyPerTurn、持续 durationTurns 回合。
/// 叠加规则：立刻回蓝每次死亡都触发；持续回蓝每次死亡刷新为完整时长（不叠加）。装卸清零。
/// </summary>
public class SpiritBannerPassive : IPassiveEffect
{
    private readonly int _instantEnergy;
    private readonly int _regenEnergyPerTurn;
    private readonly int _durationTurns;

    /// <summary>剩余持续回蓝回合</summary>
    private int _regenTurnsRemaining;

    public SpiritBannerPassive(int instantEnergy = 20, int regenEnergyPerTurn = 5, int durationTurns = 3)
    {
        _instantEnergy = instantEnergy;
        _regenEnergyPerTurn = regenEnergyPerTurn;
        _durationTurns = durationTurns;
    }

    public void OnEquip(PieceModel owner) { _regenTurnsRemaining = 0; }
    public void OnUnequip(PieceModel owner) { _regenTurnsRemaining = 0; }

    /// <summary>己方棋子死亡：立刻回蓝（每次死亡都触发），持续回蓝刷新为完整时长（不叠加）</summary>
    public void OnAllyDeath(PieceModel owner, PieceModel ally)
    {
        if (owner.Energy == null) return;
        owner.Energy.Gain(_instantEnergy);
        _regenTurnsRemaining = _durationTurns;
        Debug.Log($"[SpiritBannerPassive] {owner.Data.displayName} 因 {ally.Data.displayName} 阵亡立刻回蓝 {_instantEnergy}，随后每回合回蓝 {_regenEnergyPerTurn}（{_durationTurns} 回合）");
    }

    /// <summary>回合开始：持续期内每回合回蓝并递减（Energy 判空；递减不受 Energy 判空影响）</summary>
    public void OnTurnStart(PieceModel owner)
    {
        if (_regenTurnsRemaining <= 0) return;
        owner.Energy?.Gain(_regenEnergyPerTurn);
        _regenTurnsRemaining--;
        Debug.Log($"[SpiritBannerPassive] {owner.Data.displayName} 持续回蓝 {_regenEnergyPerTurn}（剩余 {_regenTurnsRemaining} 回合）");
    }
}
