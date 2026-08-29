using UnityEngine;

/// <summary>
/// 炎星溅射被动（杜林内建被动）—— 普攻命中后，对自身周围 splashRadius 格内敌方造成一段物理伤害。
///
/// 触发口径：覆写 OnDamageDealt（仅 AttackPiece 路径触发 = 普攻命中通知）；
/// 普攻直线穿透命中 N 个敌人 → N 次通知同帧同步到达，用 Time.frameCount 同帧去重，
/// 一次普攻只触发一段溅射（诸葛连弩同帧连射视为同一次，也只触发一段）。
/// 防递归：本被动的溅射伤害走 ApplyIncomingDamage（不触发 OnDamageDealt），天然不会再触发自身。
///
/// 伤害公式：
///   带元素（elemental=true，默认）：走元素反应口径（同烈焰斩）——
///     CalculateWithElement(baseDamage + 攻击方攻击×attackPercent%, 防御, 先天元素, 附着元素)
///     = max(1, round(max(1, 攻击值−防御) × 反应倍率) + 额外伤害)；
///     随后附着先天元素（gauge=1 同普攻口径）并结算反应状态副作用（超导减防/感电DoT/冻结）。
///   不带元素（elemental=false）：旧口径纯公式 max(1, baseDamage + 攻击×% − 防御)，不附着不反应。
/// 走统一伤害入口（DamageSource.Splash / DamageKind.Physical，白色跳字）：
/// 含减伤被动/首伤=1/死亡延迟/免死/OnDamageReceived 通知；不给能量。
/// 金币：goldPercent &gt; 0 时按溅射伤害比例实时结算（默认 0 不产金币，同 Area 溅射口径）。
/// </summary>
public class FlameNovaPassive : IPassiveEffect
{
    private readonly int _baseDamage;      // 基础伤害值
    private readonly int _attackPercent;   // 攻击力百分比（0=不吃攻击加成）
    private readonly int _splashRadius;    // 溅射半径（以自身为圆心）
    private readonly float _goldPercent;   // 金币比例（0=不产金币）
    private readonly bool _elemental;      // 是否带先天元素（默认 true；false=旧口径不附着不反应）

    private int _lastTriggerFrame = -1;    // 同帧去重：一次普攻只触发一段

    public FlameNovaPassive(int baseDamage, int attackPercent, int splashRadius, float goldPercent, bool elemental)
    {
        _baseDamage = baseDamage;
        _attackPercent = attackPercent;
        _splashRadius = Mathf.Max(1, splashRadius);
        _goldPercent = Mathf.Max(0f, goldPercent);
        _elemental = elemental;
    }

    public void OnEquip(PieceModel owner) { }
    public void OnUnequip(PieceModel owner) { }

    /// <summary>普攻命中后：对自身周围 splashRadius 格内敌方造成一段物理伤害（同帧去重，一次普攻一段）</summary>
    public void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null)
    {
        if (owner == null || owner.IsDead || owner.Data == null) return;
        if (ChessBoardController.Instance == null || PieceManager.Instance == null) return;

        // 同帧去重：穿透多目标/连弩连射的多次通知同帧到达，只放行第一段
        if (_lastTriggerFrame == Time.frameCount) return;
        _lastTriggerFrame = Time.frameCount;

        // 周围 splashRadius 格（圆形，含棋盘边界校验）；排除自身格
        var tiles = ChessBoardController.Instance.GetTilesInAttackRange(owner.Coord, _splashRadius);
        foreach (var tile in tiles)
        {
            if (tile.Coord.Equals(owner.Coord)) continue;   // 不打自己
            var enemy = PieceLayoutModel.Instance?.GetPieceAt(tile.Coord);
            if (enemy == null || enemy.IsDead || enemy.Owner == owner.Owner) continue;

            // 带元素 → 走元素反应口径（同烈焰斩：倍率/额外伤害计入伤害）；
            // 否则旧口径纯公式（同 Area 非元素溅射）。元素=None（无元素棋子）时天然回落旧口径。
            ElementType element = _elemental ? owner.Data.innateElement : ElementType.None;
            int splash;
            ElementReactionTable.ReactionConfig reaction = default;
            if (element != ElementType.None)
            {
                int attackValue = _baseDamage + Mathf.RoundToInt(
                    owner.EffectiveAttack * (_attackPercent / 100f));
                (splash, reaction) = DamageCalculator.CalculateWithElement(
                    attackValue, enemy.EffectiveDefense, element, enemy.AffixedElement);
            }
            else
            {
                float raw = _baseDamage + owner.EffectiveAttack * (_attackPercent / 100f) - enemy.EffectiveDefense;
                splash = Mathf.Max(1, Mathf.RoundToInt(raw));
            }

            // 统一伤害入口：减伤/首伤=1/死亡延迟/免死/跳字/OnDamageReceived；
            // 不触发 OnDamageDealt（防递归）、不给能量。
            // 返回 false = 被护盾拦截 → 跳过金币（免伤 = 无伤害收益）
            bool landed = PieceManager.Instance.ApplyIncomingDamage(enemy, splash, owner, DamageSource.Splash, DamageKind.Physical, element);

            // 金币实时结算（goldPercent>0 且实际造成伤害时；比例口径同 Area 溅射，基于反应后最终伤害——数值不变，仅加拦截判断）
            if (landed && _goldPercent > 0f)
                GoldManager.Instance?.AddGold(owner.Owner, Mathf.RoundToInt(splash * _goldPercent));

            // 元素附着 + 反应状态副作用（gauge=1 同普攻；超导减防/感电DoT/冻结）
            PieceManager.Instance.ApplyElementInteraction(enemy, owner, element, 1, reaction);

            // 致死者销毁（killer=owner，通知 OnKill / OnAllyDeath；ApplyIncomingDamage 不处理死亡）
            if (enemy.IsDead)
                PieceManager.Instance.DestroyPiece(enemy, owner);

            string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
            Debug.Log($"[FlameNova] {owner.Data.displayName} 溅射命中 {enemy.Data.displayName}，" +
                      $"造成 {splash} 伤害{reactionStr}（剩余 {enemy.CurrentHP}）");
        }
    }
}
