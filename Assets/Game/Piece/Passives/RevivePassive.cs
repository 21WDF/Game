using UnityEngine;

/// <summary>
/// 生命之息被动（哥伦比亚内建被动）—— 己方棋子（不含自身）死亡时，立即原地复活并获得 reviveHp 点生命值。
/// 一局游戏仅触发一次：首次触发后本局内后续己方棋子死亡不再复活（consumed 实例标记；
/// 被动实例随棋子生成创建、随场景重开重建，天然按局重置）。
///
/// 触发机制：不走事件钩子——由 PieceManager.DestroyPiece 在任何销毁状态变更（占据移除/回合注销/视图销毁）之前
/// 调用 TryReviveAlly 主动询问（框架级"取消死亡"拦截，避免棋子进入半死亡状态）。
/// 哥伦比亚自身死亡不触发（拦截只询问存活己方；她死了被动自然失效）。
/// 与中娅悖论（TryDefyDeath）互不冲突：免死在伤害路径内回 1 血、死亡判定前生效；
/// 复活在销毁 funnel 顶部生效、覆盖所有致死路径（普攻/大招/DoT/溅射/反伤/延迟伤害）。
///
/// 复活口径：SetHP(reviveHp) + 清理异常状态（感电 DoT / 冻结 / 附着元素 / 延迟伤害池 / 超导减防），
/// 原地不动（占据/注册/视图从未被动过）。
/// </summary>
public class RevivePassive : IPassiveEffect
{
    private readonly int _reviveHp;   // 复活后的生命值
    private bool _consumed;           // 一局一次标记（触发过即失效）

    public RevivePassive(int reviveHp)
    {
        _reviveHp = Mathf.Max(1, reviveHp);
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>尝试复活 dying 棋子（PieceManager.TryReviveAlly 调用）。
    /// 未触发过 → 消耗次数、回血 + 清异常状态、返回 true（调用方取消销毁）；已触发过 → false。</summary>
    public bool TryRevive(PieceModel dying)
    {
        if (_consumed || dying == null) return false;
        _consumed = true;

        dying.SetHP(_reviveHp);

        // 清理异常状态（净化复活）：DoT / 冻结 / 附着元素 / 延迟伤害池 / 超导减防
        dying.DotTurnsRemaining = 0;
        dying.DotDamagePerTurn = 0;
        dying.DotSource = null;
        dying.FreezeTurnsRemaining = 0;
        dying.AffixedElement = ElementType.None;
        dying.AffixedElementGauge = 0;
        dying.PendingDamage = 0;
        dying.CurrentDefenseReduction = 0;
        dying.DefenseReductionTurnsRemaining = 0;
        dying.PassiveDefenseReduction = 0;
        dying.PassiveDefenseReductionTurnsRemaining = 0;
        dying.TurnsSinceDamaged = 0;

        Debug.Log($"[RevivePassive] {dying.Data?.displayName} 被「生命之息」原地复活（{_reviveHp} 血，异常状态已净化）");
        return true;
    }
}
