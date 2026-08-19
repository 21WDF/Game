using UnityEngine;

/// <summary>免 AP 被动类型（FreeAPPassive 用）</summary>
public enum FreeAPType
{
    Move = 0,    // 移动（探索者护臂）
    Attack = 1,  // 攻击（诸葛连弩）
}

/// <summary>
/// 免 AP 被动（探索者护臂=Move / 诸葛连弩=Attack）—— 冷却完毕后一次移动/攻击不消耗 AP。
/// BattleController 查询 CanUseFreeAP：可用则跳过 AP 检查与消耗并 MarkUsed()。
/// 冷却按回合递减（每次任一方回合开始 -1）；冷却跟装备实例走：卸下暂停、重装继续，装卸无法白嫖。
/// </summary>
public class FreeAPPassive : IPassiveEffect
{
    /// <summary>免 AP 类型（移动/攻击）</summary>
    public FreeAPType FreeType { get; }

    /// <summary>免 AP/连射动作是否获得充能（false=该装备的免 AP 移动/连射攻击不充能；true=照常充能）</summary>
    public bool GainEnergy { get; }

    private readonly int _cooldownTurns;
    private int _cooldown;

    public FreeAPPassive(FreeAPType freeType = FreeAPType.Move, int cooldownTurns = 4, bool gainEnergy = false)
    {
        FreeType = freeType;
        _cooldownTurns = cooldownTurns;
        GainEnergy = gainEnergy;
    }

    /// <summary>免 AP 是否可用（冷却完毕）</summary>
    public bool CanUseFreeAP => _cooldown <= 0;

    /// <summary>使用后进入冷却</summary>
    public void MarkUsed()
    {
        _cooldown = _cooldownTurns;
        Debug.Log($"[FreeAPPassive] 免 AP {(FreeType == FreeAPType.Move ? "移动" : "攻击")} 已使用，进入 {_cooldownTurns} 回合冷却");
    }

    // 装卸不清零冷却：_cooldown 记录在装备实例上（冷却跟装备走）。
    // 卸下后 GetAllPassives 不再遍历到本被动 → OnTurnStart 不触发 → 冷却自然暂停；重装后继续递减。
    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>回合开始：冷却递减（最小 0）</summary>
    public void OnTurnStart(PieceModel owner)
    {
        if (_cooldown > 0) _cooldown--;
    }
}
