using UnityEngine;

/// <summary>
/// 雷蚀强化被动（菲林斯内建被动）—— 普攻命中后进入「强化」状态，持续 duration 回合（默认 6）。
///
/// 触发口径：覆写 OnDamageDealt（仅 AttackPiece 路径触发 = 普攻命中通知）；
/// 普攻直线穿透命中 N 个敌人 → N 次通知同帧到达，重复设置同一字段天然幂等（无需去重）。
/// 强化期间再次普攻命中 → 刷新为 duration（重置时长）。
///
/// 递减口径：TurnManager.TickElementDuration 每次任一方回合开始 -1（同超导减防/元素附着惯例），≤0 自然结束。
/// 强化状态本身无属性加成——只改变大招形态（见 ThunderSweepUltimate：普通态横向 3 格物理 / 强化态周围 1 格魔法）。
/// </summary>
public class SparkEmpowerPassive : IPassiveEffect
{
    private readonly int _duration;    // 强化持续回合

    public SparkEmpowerPassive(int duration)
    {
        _duration = Mathf.Max(1, duration);
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>普攻命中后：进入/刷新强化状态（剩余回合重置为 duration）</summary>
    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null)
    {
        if (owner == null || owner.IsDead) return;

        bool wasEmpowered = owner.IsEmpowered;
        owner.EmpoweredTurnsRemaining = _duration;
        if (!wasEmpowered)
            Debug.Log($"[SparkEmpower] {owner.Data?.displayName} 进入强化状态，持续 {_duration} 回合");
        else
            Debug.Log($"[SparkEmpower] {owner.Data?.displayName} 强化状态刷新为 {_duration} 回合");
    }
}
