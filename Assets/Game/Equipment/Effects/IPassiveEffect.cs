/// <summary>伤害来源类型（位掩码）—— 标记伤害从哪条路径来（通知链传递；DamageSource 与 DamageKind 两维度并存）。
/// 常用组合：7 = Attack|Splash|Ultimate（普攻+溅射+大招）；15 = 全响应（含 DoT）。</summary>
[System.Flags]
public enum DamageSource
{
    Attack = 1,     // 普攻
    Splash = 2,     // 元素 + 非元素溅射
    Ultimate = 4,   // 大招
    Dot = 8,        // 感电 DoT
    Reflect = 16,   // 反伤
}

/// <summary>伤害类型三分类 —— 与 DamageSource（来源维度）独立并存，统一约束"受不受防御减伤、反甲反不反"。
/// Physical=物理（受防御减伤、触发反甲：普攻/大招/溅射）；Magical=魔法（不受减伤、不触发反甲：感电DoT/反伤/超载爆炸）；
/// True=真实（不受减伤、不触发反甲，未来无视护盾）。B 阶段在此分类上接入减伤/反甲筛选逻辑。</summary>
public enum DamageKind
{
    Physical,   // 物理
    Magical,    // 魔法
    True,       // 真实
}

/// <summary>
/// 被动效果接口 —— 装备附带 / 棋子内建的效果契约。
/// 生命周期：装备到棋子时 OnEquip，卸下时 OnUnequip。
/// 事件钩子（阶段②事件驱动框架）：C# 默认接口方法空实现，具体被动按需覆写，现有实现类（如 ExtraDamagePassive）无需改动；
/// 钩子只做"通知"，不改变主流程的伤害值或返回值。
/// 属性加成（bonusAttack 等）不在此处处理，而是通过 PieceModel.Equipment* 属性聚合计算。
/// </summary>
public interface IPassiveEffect
{
    /// <summary>装备到 owner 时触发</summary>
    void OnEquip(PieceModel owner);

    /// <summary>从 owner 卸下时触发</summary>
    void OnUnequip(PieceModel owner);

    /// <summary>唯一被动 id（默认接口属性）：null/空 = 非唯一被动，不参与去重。
    /// 唯一被动框架（C2）：同棋子同 uniqueId 仅保留「最后装备」装备上的那件（GetAllPassives 去重）；
    /// 不同棋子各持相同 uniqueId 互不影响（可同时生效）。</summary>
    string UniqueId => null;

    // ---- 事件钩子（默认空实现；触发点见 TurnManager / PieceManager）----
    /// <summary>回合开始（双方棋子在每次回合开始都触发，"每回合"语义）</summary>
    void OnTurnStart(PieceModel owner) { }

    /// <summary>被攻击时（owner 为受击方；无论是否造成减血都触发，attacker 为攻击方）。
    /// 与 OnDamageReceived（仅实际减血触发）区分；反甲只覆写 OnDamageReceived，
    /// 故反伤触发的本钩子不会反出反甲（防递归天然成立）。</summary>
    void OnAttacked(PieceModel owner, PieceModel attacker, DamageSource source = DamageSource.Attack) { }

    /// <summary>受到伤害后（owner 为受击方）。source 标记伤害来源类型（DamageSource；反甲等被动据此筛选/防递归）；
    /// kind 标记伤害类型三分类（DamageKind；默认 Physical 向后兼容）。
    /// 所有实际减血路径（普攻/大招/DoT/溅射/反伤）统一触发；damage≤0 或目标已死不通知。</summary>
    void OnDamageReceived(PieceModel owner, int damage, DamageSource source = DamageSource.Attack, DamageKind kind = DamageKind.Physical) { }

    /// <summary>造成伤害后（owner 为攻击方）。target 为受击方（枯萎宝珠等目标类被动用；仅 AttackPiece 路径触发）</summary>
    void OnDamageDealt(PieceModel owner, int damage, PieceModel target = null) { }

    /// <summary>击杀时（owner 为击杀者）</summary>
    void OnKill(PieceModel owner, PieceModel victim) { }

    /// <summary>同阵营棋子死亡时（owner 为存活的己方棋子，victim 已死亡）</summary>
    void OnAllyDeath(PieceModel owner, PieceModel ally) { }

    /// <summary>同阵营棋子释放大招成功后（owner 为被动持有者，caster 为释放者；含/不含 caster 本人由具体被动覆写筛选）。
    /// 由 UseUltimateCore 在 Execute 完成后广播给释放者阵营全体存活棋子（能量已消耗、效果已结算）。
    /// 用途：雷电将军·雷罚恶曜之眼（队友开大回能）等跨棋子大招监听。</summary>
    void OnAllyUltimateCast(PieceModel owner, PieceModel caster) { }

    /// <summary>同阵营棋子攻击命中后（owner 为被动持有者，ally 为攻击者，target 为受击者——可能已死亡）。
    /// source 标记命中来源：普攻族（Attack/Splash，AttackPiece 路径含二段/溅射）或大招（Ultimate，大招直伤/持续段）；
    /// DoT/Reflect 不广播（非"攻击"语义）。由 PieceManager 在两条路径结算尾部广播给攻击方阵营全体存活棋子。
    /// 用途：雷电将军·协同攻击等"己方攻击时响应"类被动。重入风险由消费被动自防（协同攻击期间不再响应）。</summary>
    void OnAllyAttackHit(PieceModel owner, PieceModel ally, PieceModel target, int damage, DamageSource source) { }
}
