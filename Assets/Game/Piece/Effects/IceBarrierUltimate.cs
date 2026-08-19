using UnityEngine;

/// <summary>
/// 冰障大招（自身增益型，ultimateConfig.requiresTarget=false）—— 为释放者提供临时防御加成。
/// Execute 对 caster.TemporaryDefenseBonus 累加 10；该值纳入 PieceModel.EffectiveDefense 计算，
/// 由 TurnManager.TickElementDuration 在下回合开始时重置为 0。
///
/// target 参数被忽略（自身增益；调用方传入 caster）。
/// 与 CurrentDefenseReduction 分离：避免被超导反应覆盖式赋值抹除，语义清晰。
/// </summary>
public class IceBarrierUltimate : IUltimateEffect
{
    public void Execute(PieceModel caster, PieceModel target)
    {
        if (caster == null) return;
        caster.TemporaryDefenseBonus += 10;
        Debug.Log($"[IceBarrierUltimate] {caster.Data.displayName} 施放冰障，临时+10防御" +
                  $"（当前加成 {caster.TemporaryDefenseBonus}，下回合重置）");
    }
}
