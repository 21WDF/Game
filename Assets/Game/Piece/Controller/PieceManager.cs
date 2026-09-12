using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 棋子管理器（生命周期 + 行为）—— 棋子渲染与生命周期的统一管理者（Piece 模块的 Controller）。
/// 职责：按 id 生成棋子（从 PieceRegistry 取定义、实例化 prefab、创建 PieceModel、注册到布局 / 回合）；
///       驱动移动 / 攻击 / 死亡。
/// 挂载在场景中的 PieceManager GameObject 上（单例）。
/// </summary>
public class PieceManager : MonoBehaviour
{
    public static PieceManager Instance { get; private set; }

    [Header("棋子定义注册表")]
    public PieceRegistry registry;

    // ---- 反应修饰器收集缓冲（复用，避免每次攻击分配）----
    private readonly List<IReactionModifier> _reactionModifierBuffer = new();
    private GameConfig _config;
    private bool _elementTileAdapterMissingWarned;   // 大招未实现 IUltimateAreaProvider 但勾选元素格子的告警只打一次

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _config = Resources.Load<GameConfig>("GameConfig");
    }

    // ==========================================
    //  生成
    // ==========================================
    /// <summary>按棋子 id 在指定坐标生成一个属于 owner 的棋子，返回其运行时模型</summary>
    public PieceModel SpawnPieceById(int pieceId, HexCoord coord, PlayerSide owner)
    {
        if (registry == null)
        {
            Debug.LogError("[PieceManager] 未配置 PieceRegistry，无法生成棋子！");
            return null;
        }

        var data = registry.GetByID(pieceId);
        if (data == null)
        {
            Debug.LogError($"[PieceManager] 注册表中找不到 id={pieceId} 的棋子定义！");
            return null;
        }
        if (data.prefab == null)
        {
            Debug.LogError($"[PieceManager] PieceData(id={pieceId}) 未指定 prefab！请在 Inspector 中赋值。");
            return null;
        }

        Vector3 worldPos = ChessBoardController.Instance != null
            ? ChessBoardController.Instance.GetCellWorldPosition(coord)
            : Vector3.zero;
        GameObject obj = Instantiate(data.prefab, worldPos + Vector3.up * (_config != null ? _config.pieceYOffset : 0f), Quaternion.identity);

        var view = obj.GetComponent<PieceView>();
        if (view == null) view = obj.GetComponentInChildren<PieceView>();
        if (view == null)
        {
            Debug.LogError($"[PieceManager] prefab({data.prefab.name}) 缺少 PieceView 脚本！");
            Destroy(obj);
            return null;
        }

        var model = new PieceModel(data, owner, coord, view);
        view.BindModel(model);

        // 召唤出现表现：仅召唤物（isSummon，如兔兔伯爵/傀儡）播放出现动作；开局部署的棋子不播（避免喧闹）
        if (data.isSummon)
            view.PlaySummonAppearance();

        // 内建被动实例化：Data.builtInPassives → model.BuiltInPassives（工厂与装备被动共用）
        // OnEquip 与装备路径（EquipmentManager）同口径：此前内建被动从未触发 OnEquip，
        // 傀儡（PuppetPassive）等依赖宿主注入/注册的被动会失效，此处补齐。
        if (data.builtInPassives != null)
        {
            foreach (var cfg in data.builtInPassives)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.className)) continue;
                var passive = PassiveFactory.Create(cfg.className, cfg.jsonParams);
                if (passive != null)
                {
                    model.BuiltInPassives.Add(passive);
                    passive.OnEquip(model);
                }
                else
                    Debug.LogWarning($"[PieceManager] 棋子 {data.displayName} 的内建被动未知类名: {cfg.className}");
            }
        }

        // 注册到占据权威与回合管理
        PieceLayoutModel.Instance?.Place(coord, model);
        TurnManager.Instance?.RegisterPiece(model);

        Debug.Log($"[PieceManager] 生成 {data.displayName}(id={pieceId}) @ {coord}，归属 {owner}");
        return model;
    }

    // ==========================================
    //  移动
    // ==========================================
    /// <summary>传送棋子到 toCoord（瞬移，不走寻路；传送石用）。
    /// 更新占据表 + Model.Coord + View 位置（Snap，无路径动画）。</summary>
    public void TeleportPiece(PieceModel model, HexCoord toCoord)
    {
        if (model == null || model.IsDestroyed) return;  // 已销毁（如傀儡已引爆）不再移位，防幽灵占据
        HexCoord from = model.Coord;
        if (from == toCoord) return;
        PieceLayoutModel.Instance?.MovePiece(from, toCoord);
        model.SetCoord(toCoord);
        if (model.View != null && ChessBoardController.Instance != null)
        {
            Vector3 pos = ChessBoardController.Instance.GetCellWorldPosition(toCoord);
            model.View.SnapToPosition(pos + Vector3.up * (_config != null ? _config.pieceYOffset : 0f));
        }
        Debug.Log($"[PieceManager] {model.Data.displayName} 传送 {from} -> {toCoord}");
    }

    /// <summary>双子调换位置（战争之城·主将调换用；瞬移语义：不走寻路、无动画、不触发任何
    /// 路径经过类效果——路径被动/季风经过伤害等均在 MovePieceAlongPath 内，本方法不经过）。
    /// 占据表先同时移除两坐标再各自放置，避免连续 TeleportPiece 的占据覆盖冲突（MovePiece(to) 会覆盖目标格）</summary>
    public void SwapPieces(PieceModel a, PieceModel b)
    {
        if (a == null || b == null || a == b || a.IsDestroyed || b.IsDestroyed) return;
        HexCoord coordA = a.Coord, coordB = b.Coord;
        if (coordA == coordB) return;

        PieceLayoutModel.Instance?.Remove(coordA);
        PieceLayoutModel.Instance?.Remove(coordB);
        a.SetCoord(coordB);
        b.SetCoord(coordA);
        PieceLayoutModel.Instance?.Place(coordB, a);
        PieceLayoutModel.Instance?.Place(coordA, b);

        if (ChessBoardController.Instance != null)
        {
            Vector3 yOffset = Vector3.up * (_config != null ? _config.pieceYOffset : 0f);
            if (a.View != null) a.View.SnapToPosition(ChessBoardController.Instance.GetCellWorldPosition(coordB) + yOffset);
            if (b.View != null) b.View.SnapToPosition(ChessBoardController.Instance.GetCellWorldPosition(coordA) + yOffset);
        }
        Debug.Log($"[PieceManager] 调换 {a.Data.displayName}({coordA}) ↔ {b.Data.displayName}({coordB})");
    }

    /// <summary>沿完整路径移动棋子（逐格动画）。供 BattleController 途经点移动使用。
    /// 逐格动画走一格 → 逻辑立即跟进一格（SetCoord + 占据表）；occupancy 逐格更新。
    /// 大招能量整次移动只获取一次（动画前结算）。ForceInstantMove=true 时瞬移无动画。</summary>
    public void MovePieceAlongPath(PieceModel model, List<HexCoord> path, bool skipEnergy = false)
    {
        if (model == null || path == null || path.Count == 0) return;
        HexCoord from = model.Coord;
        HexCoord to = path[path.Count - 1];
        if (from == to) return;

        // 路径伤害被动（克洛琳德·雷径惩戒）：移动开始时对飞越的敌方棋子结算物理伤害。
        // 普通移动与大招冲刺（ThunderDashUltimate 复用本入口）都会经过此处 → 冲刺同样触发。
        foreach (var passive in model.GetAllPassives())
            if (passive is VoltPathPassive vpp)
                vpp.TriggerPathDamage(model, path);

        // 飓风幸运方块（季风三期B）：移动经过即拾取（路径所有节点含终点；落点不必是该格）。
        // 判断内聚 MonsoonManager（无方块时零开销直接返回）。
        MonsoonManager.Instance?.OnPieceMovedAlongPath(model, path);

        // 元素格子·经过附着：与雷暴经过伤害同一条「移动经过路径」通知链路（新增一路通知，互不干扰——
        // 上面的雷暴结算已按原路径完成，本通知不改变其触发时机/结算顺序/数值）。
        // 只结算附着（+元素量、走完整反应流程、仅元素城邦生效）；路径上每格只附着一次。
        // 返回打断坐标 = 经过附着触发超载/冻结 → 移动立即中断：截断路径，棋子停在触发的那一格
        //（不继续前往原定终点；AP/能量已按整次移动结算，不受打断影响）。
        HexCoord? interruptCoord = ElementTileManager.Instance?.NotifyPiecePathPassed(model, path);
        if (interruptCoord.HasValue && path.Count > 1)
        {
            int stopIndex = path.LastIndexOf(interruptCoord.Value);
            if (stopIndex >= 0 && stopIndex < path.Count - 1)
                path = path.GetRange(0, stopIndex + 1);   // 截断：动画与占据表只走到打断格
        }
        to = path[path.Count - 1];

        // 构建世界坐标路径
        var worldPath = new List<Vector3>(path.Count);
        if (ChessBoardController.Instance != null)
        {
            foreach (var coord in path)
            {
                Vector3 pos = ChessBoardController.Instance.GetCellWorldPosition(coord);
                worldPath.Add(pos + Vector3.up * (_config != null ? _config.pieceYOffset : 0f));
            }
        }

        // 设动画锁 + 能量结算（动画前；整次移动只获取一次；skipEnergy=true 跳过充能——免 AP 移动用）
        SetPieceAnimating(true);
        if (!skipEnergy)
            model.Energy?.Gain(model.Data.ultimateConfig?.energyPerMove ?? 0);

        // 动画时长
        float dt = model.ForceInstantMove ? 0f : (_config != null ? _config.moveDurationPerTile : 0.4f);

        if (model.View != null)
        {
            // Walk/Run：步数 ≤ walkRunThreshold 走，否则跑
            int steps = path.Count - 1;
            bool isRunning = steps > (_config != null ? _config.walkRunThreshold : 2);
            // 逐格 SetCoord / 占据表更新由 AnimateMove 回调内部处理
            model.View.AnimateMove(worldPath, path, dt, isRunning, null, OnMoveAnimationComplete);
        }
        else
        {
            // 无 View → 瞬移完成逻辑（occupancy 仅 from→终点）
            model.SetCoord(to);
            PieceLayoutModel.Instance?.MovePiece(from, to);
            OnMoveAnimationComplete();
        }

        Debug.Log($"[PieceManager] {model.Data.displayName} 沿路径移动 {from} -> {to}（{path.Count} 节点）");
    }

    /// <summary>设置棋子动画锁（true=屏蔽输入）</summary>
    private static void SetPieceAnimating(bool value)
    {
        if (BattleController.Instance != null && BattleController.Instance.Model != null)
            BattleController.Instance.Model.IsPieceAnimating = value;
    }

    /// <summary>移动动画完成：解锁输入 + 基于新位置重算高亮</summary>
    private static void OnMoveAnimationComplete()
    {
        if (BattleController.Instance == null || BattleController.Instance.Model == null) return;
        BattleController.Instance.Model.IsPieceAnimating = false;
        BattleController.Instance.RefreshSelectionHighlights();
    }

    // ==========================================
    //  攻击
    // ==========================================
    public void AttackPiece(PieceModel attacker, PieceModel target, bool skipGold = false, bool skipEnergy = false, int? overrideAttack = null, DamageSource damageSource = DamageSource.Attack)
    {
        if (attacker == null || target == null) return;

        ElementType attackerElem = attacker.Data.innateElement;

        // 收集攻击方装备中的反应修饰器（按槽位顺序：0→1→2）
        CollectReactionModifiers(attacker, _reactionModifierBuffer);

        // 只查一次反应配置（含修饰器链 → override → 数据库 → 全局链 → 棋子链）
        var reaction = ElementReactionTable.GetReaction(
            target.AffixedElement, attackerElem, _reactionModifierBuffer);

        // 超导减防预应用：在伤害计算前生效（当次攻击即享受减防）；同时写入持续回合（③.1，0=旧行为）
        if (reaction.DefenseReduction > 0)
        {
            target.CurrentDefenseReduction = reaction.DefenseReduction;
            target.DefenseReductionTurnsRemaining = reaction.DefenseReductionTurns;
        }

        // 计算伤害（此时 EffectiveDefense 已包含超导减防；用预查询的 reaction 避免二次查询）
        // overrideAttack 非 null 时用其替代 attacker.EffectiveAttack（如溅射用公式算的有效攻击力）
        int effectiveAttack = overrideAttack ?? attacker.EffectiveAttack;
        int damage = DamageCalculator.CalculateWithReaction(
            effectiveAttack, target.EffectiveDefense, reaction);

        // 护盾拦截（基座）：拦在减伤之前（拦截原始伤害）。被拦截时跳过整条数值链
        // （减伤/易伤/首伤=1/延迟拆分/扣血/免死/受伤通知/伤害跳字）；
        // 元素附着/反应/冻结/感电/超载爆炸/击退/能量/OnAttacked/OnDamageDealt 照常（护盾只拦伤害数值）——
        // 金币是例外：拦截时攻击方/受击方都不给金币（免伤 = 无伤害收益，见下方金币段）
        bool absorbed = TryAbsorbByShield(target, damage, DamageKind.Physical, attackerElem);
        // 拦截标志：供 OnDamageDealt 消费者中的「伤害收益类」被动（吸血）读取——被拦截则不吸血。
        // 二段/减防/强化/领域/溅射等「命中类」消费者不读此标志，照常触发
        target.LastHitAbsorbedByShield = absorbed;

        if (!absorbed)
        {
            // 被动伤害介入（B 阶段）：基础段 = max(1, 基础伤害 - 受击方物理减伤被动)。
            // 附加伤害已独立成段（基础段扣血结算后按类型分三份另行结算，见 ApplyExtraDamageSegments）
            damage = Mathf.Max(1, damage - GetTotalDamageReduction(target, DamageKind.Physical));
            // 易伤结算（莫娜大招·星异）：减伤之后、首伤=1 之前，伤害 ×(1+percent%)
            ApplyVulnerability(target, ref damage);

            // 伤害介入（C1，严格顺序）：首伤=1（中娅悖论）→ 死亡之蔑拆分（立即 + 延迟）
            ApplyFirstDamageProtection(target, ref damage);
            ApplyDeathDelay(target, ref damage);
            target.TakeDamage(damage);
            // 死亡介入（C1）：免死（中娅悖论）——将死则回 1 HP（后续 IsDead 判定/击退/销毁自然使用新状态）
            TryDefyDeath(target);

            // 被动状态维护：未受伤害计数归零 + 记录上次受伤害来源（石像鬼板甲等 2.2 用）
            // （护盾免伤 = 没有减血：不打断「连续未受伤」计时、不更新伤害来源）
            target.TurnsSinceDamaged = 0;
            target.LastDamageSource = attacker;

            // 地下交易·累计伤害统计（三期，只读观察）：护盾未拦截的实际伤害，按攻击方累计
            UndergroundTradeManager.Instance?.RecordDamage(attacker.Owner, damage, damageSource);
        }
        attacker.HasAttackedThisTurn = true;

        // 被动事件通知（纯通知，不改主流程数值）：被攻击（无论是否减血）→ 造成伤害（含目标）→ 受到伤害（仅实际减血）
        // 普攻路径伤害类型 = Physical（受防御减伤、触发反甲）
        foreach (var passive in target.GetAllPassives()) passive.OnAttacked(target, attacker, damageSource);
        foreach (var passive in attacker.GetAllPassives()) passive.OnDamageDealt(attacker, damage, target);
        if (!absorbed)
            foreach (var passive in target.GetAllPassives()) passive.OnDamageReceived(target, damage, damageSource, DamageKind.Physical);

        // ---- 浮动跳字（View 层，不参与逻辑；池/View 未就绪时静默跳过）----
        // 在副作用（元素附着/冻结/击退）前取位置，确保用击退前的原始坐标
        if (FloatingTextPool.Instance != null && target.View != null)
        {
            Vector3 pos = target.View.transform.position;
            Color elementColor = ElementColorMapper.GetDamageColor(attackerElem, DamageKind.Physical);
            bool isReaction = reaction.Type != ReactionType.None;
            // 普攻固定物理形态（纯色块）；伤害数字颜色不受「是否反应」影响（反应提示由下方独立跳字承担）
            // （护盾格挡时跳过伤害数字——「格挡」提示由 TryAbsorbByShield 显示）
            if (!absorbed)
                FloatingTextPool.Instance.ShowDamage(damage, pos, elementColor, DamageKind.Physical);
            if (isReaction)
            {
                // 智能格式：倍率≠1 显 ×倍率；倍率=1 且有额外伤害显 +额外；否则仅反应名
                string prompt;
                string rname = ElementColorMapper.GetReactionName(reaction.Type);
                if (reaction.DamageMultiplier != 1f)
                    prompt = $"{rname}！×{reaction.DamageMultiplier:0.0}";
                else if (reaction.ExtraDamage > 0)
                    prompt = $"{rname}！+{reaction.ExtraDamage}";
                else
                    prompt = $"{rname}！";
                FloatingTextPool.Instance.Get()?.Show(prompt, pos + Vector3.up * 1.5f, FloatStyles.ReactionDamage());
            }
        }

        // 元素附着与消耗：攻击方有元素才触发交互（普攻触发量=1 + 元素之力加成）
        if (attackerElem != ElementType.None)
        {
            // 护盾染色封印（元素城邦二期）：已染色护盾封印同元素附着 → 跳过附着写入（反应已照常结算）
            if (!target.BlocksElementAttachment(attackerElem))
            {
                int triggerGauge = 1 + GetExtraElementGauge(attacker, attackerElem);
                var (resultElem, resultGauge) = ElementReactionTable.ResolveElementInteraction(
                    target.AffixedElement, target.AffixedElementGauge,
                    attackerElem, triggerGauge);
                target.AffixedElement = resultElem;
                target.AffixedElementGauge = resultGauge;
                // 染色：附着成功（结果非 None）→ 未染色护盾变为对应元素盾
                target.TryDyeShieldElement(resultElem);
            }
        }

        // 反应视觉脉冲（纯表现，不参与任何结算）：命中触发元素反应时，目标脚下光环做一次强调脉冲
        if (reaction.Type != ReactionType.None)
            target.View?.PulseAura();

        // 冻结反应：设置冻结回合（IsFrozen 由 FreezeTurnsRemaining 派生）
        if (reaction.FreezeTurns > 0)
        {
            target.FreezeTurnsRemaining = reaction.FreezeTurns;
            Debug.Log($"[PieceManager] {target.Data.displayName} 被冻结 {reaction.FreezeTurns} 回合");
        }

        // 感电 DoT：设置每回合伤害和持续回合
        if (reaction.DotTurns > 0 && reaction.DotDamagePerTurn > 0)
        {
            target.DotDamagePerTurn = reaction.DotDamagePerTurn;
            target.DotTurnsRemaining = reaction.DotTurns;
            target.DotSource = attacker;  // 记录 DoT 施加者（回合结算时作为伤害来源）
            Debug.Log($"[PieceManager] {target.Data.displayName} 感电，每回合 {reaction.DotDamagePerTurn} 伤害，持续 {reaction.DotTurns} 回合");
        }

        // 超载爆炸 AOE（③.3）：灼火/陨雷之力，对目标周围敌方额外真实伤害（排除 target 本身）。
        // 在击退前执行——爆炸以「击退前的目标坐标」为中心（原地扩散，目标被炸飞）
        if (reaction.Type == ReactionType.Overload && !target.IsDead)
            ApplyOverloadExplosion(attacker, target, damage);

        // 超载击退：伤害结算后、死亡检查前触发（击退不致死；已死亡的目标不击退）
        if (reaction.KnockbackDistance > 0 && !target.IsDead)
        {
            var knockbackTo = KnockbackResolver.Resolve(attacker.Coord, target.Coord, reaction.KnockbackDistance);
            if (knockbackTo.HasValue)
                TeleportPiece(target, knockbackTo.Value);
        }

        // 季风之城·暴雪（二期）：攻击后被攻击棋子被击退 N 格（null 安全纯通知；生效判断内聚在管理器，
        // 复用超载击退链路，遇阻/边界由解析器自然处理）
        if (!target.IsDead)
            MonsoonManager.Instance?.OnAttackLanded(attacker, target);

        // 金币系统：造成 / 受到伤害实时到账（skipGold=true 时跳过，如溅射想用自定义金币比例；
        // absorbed=true 跳过——护盾免伤 = 攻击方/受击方都无金币收益，非拦截场景数值不变）
        if (!skipGold && !absorbed)
        {
            GoldManager.Instance?.OnDamageDealt(attacker, damage);
            GoldManager.Instance?.OnDamageReceived(target, damage);
        }

        // 附加伤害独立段：基础段未致死时，按「物理→魔法→真实」三份独立结算（基础段已死则跳过）
        if (!target.IsDead)
            ApplyExtraDamageSegments(attacker, target);

        // 已行动视觉标记：攻击结算完成后攻击方变灰（被攻击方不变灰）；
        // 诸葛连弩持有者豁免——连射可再攻击，保持原色提示玩家可继续操作
        bool hasRepeater = false;
        foreach (var passive in attacker.GetAllPassives())
            if (passive is FreeAPPassive fp && fp.FreeType == FreeAPType.Attack) { hasRepeater = true; break; }
        if (!hasRepeater)
            attacker.View?.SetActed(true);

        string reactionStr = reaction.Type != ReactionType.None ? $"，触发{reaction.Type}" : "";
        Debug.Log($"[PieceManager] {attacker.Data.displayName} 攻击 {target.Data.displayName}，造成 {damage} 伤害{reactionStr}（剩余 {target.CurrentHP}）");

        // 动作表现（纯异步，不锁输入、不改逻辑；受击分级内聚在 PlayHitReaction）：
        // 攻击者朝目标方向前冲；受击者按伤害来源播完整/轻量受击（目标已死则不播——死亡动作由
        // DestroyPiece 承接，避免两动作在视觉子节点上争抢）
        if (attacker.View != null && !attacker.IsDestroyed)
        {
            Vector3 targetPos = target.View != null
                ? target.View.transform.position
                : (ChessBoardController.Instance != null
                    ? ChessBoardController.Instance.GetCellWorldPosition(target.Coord)
                    : attacker.View.transform.position);
            attacker.View.PlayAttack(targetPos);
        }
        PlayHitReaction(target, damageSource, attacker);

        if (target.IsDead)
            DestroyPiece(target, attacker);

        // 大招能量：攻击后获取（skipEnergy=true 时跳过，如溅射不应充能）
        if (!skipEnergy)
        {
            attacker.Energy?.Gain(attacker.Data.ultimateConfig?.energyPerAttack ?? 0);
        }

        // 己方攻击命中广播（雷电将军·协同攻击）：普攻路径尾部（含二段/溅射，按 damageSource 传递；
        // target 可能已死亡——协同消费方按需筛选；协同攻击自身由 CoordinatedStrikePassive 重入守卫阻断）
        NotifyAllyAttackHit(attacker, target, damage, damageSource);
    }

    /// <summary>统一伤害应用入口（2.2 伤害通知统一）：
    /// TakeDamage + 状态维护（未受伤害计数归零 / 记录伤害来源）+ 触发受击方全部被动 OnAttacked / OnDamageReceived。
    /// 只触发"受伤害"通知——不触发 OnDamageDealt（吸血保持只在 AttackPiece 生效）、不触发元素反应/金币/能量。
    /// 返回值：true = 实际造成伤害（未被护盾拦截）；false = 未造成伤害（目标无效 / damage≤0 / 被护盾拦截）。
    /// 调用方（各效果类）据此决定是否给金币——护盾免伤 = 攻击方与受击方都无金币收益。
    /// 目标已死直接返回 false；damage≤0 仍触发 OnAttacked（被攻击通知，无论是否减血）但不触发 OnDamageReceived（不减血不通知/不打断计时）。
    /// damageType 标记伤害来源类型（DamageSource：反甲等被动据此筛选/防递归）；
    /// damageKind 标记伤害类型三分类（DamageKind：A 阶段仅打标签，B 阶段接入减伤/反甲筛选）。
    /// element 仅影响跳字颜色（View 层）：带元素伤害（element != None）跳元素色（火红/水蓝/雷紫/冰白），
    /// 无元素伤害（反伤/DoT/超载爆炸/非元素溅射，不传保持 None）跳伤害类型色（物理白/魔法紫/真实金）——不影响任何结算逻辑。
    /// 普攻路径（AttackPiece）不走此方法——主流程保持内联的同款通知。</summary>
    public bool ApplyIncomingDamage(PieceModel target, int damage, PieceModel source,
        DamageSource damageType = DamageSource.Attack, DamageKind damageKind = DamageKind.Physical,
        ElementType element = ElementType.None)
    {
        if (target == null || target.IsDead) return false;

        // 被攻击通知：无论是否减血都触发（与 OnDamageReceived 的"仅减血"语义区分；石像鬼板甲用）
        foreach (var passive in target.GetAllPassives())
            passive.OnAttacked(target, source, damageType);

        if (damage <= 0) return false;

        // 护盾拦截（基座）：拦在减伤之前（拦截原始伤害）。被拦截 = 本次伤害完全免掉：
        // 跳过数值链（减伤/易伤/首伤=1/延迟拆分/扣血/免死）+ 受伤通知 + 打断计时 + 伤害跳字
        //（「格挡/免疫」提示由 TryAbsorbByShield 显示）。大招路径（Ultimate 来源）仍广播命中——
        // 与普攻路径拦截时协同照常的口径一致（攻击命中了护盾，雷电将军协同补刀）
        if (TryAbsorbByShield(target, damage, damageKind, element))
        {
            if (damageType == DamageSource.Ultimate)
                NotifyAllyAttackHit(source, target, damage, damageType);
            // 护盾格挡的命中同样播受击（打击视觉上命中了，只是数值被盾拦）
            PlayHitReaction(target, damageType, source);
            return false;
        }

        // 减伤介入（B 阶段，与普攻路径同序：先于首伤=1/延迟拆分）：统一入口伤害也按 damageKind 筛选减伤
        //（魔法/真实不受防御减伤，但受减伤被动——filter 默认 All 全类型；下限 1 与普攻路径一致）
        damage = Mathf.Max(1, damage - GetTotalDamageReduction(target, damageKind));
        // 易伤结算（莫娜大招·星异）：减伤之后、首伤=1 之前，伤害 ×(1+percent%)（与普攻路径同序）
        ApplyVulnerability(target, ref damage);

        // 伤害介入（C1，与 AttackPiece 同序）：首伤=1 → 死亡之蔑拆分（立即 + 延迟）
        ApplyFirstDamageProtection(target, ref damage);
        ApplyDeathDelay(target, ref damage);
        target.TakeDamage(damage);
        // 死亡介入（C1）：免死（中娅悖论）
        TryDefyDeath(target);

        // 跳字（View 层反馈，不参与逻辑；池/View 未就绪静默跳过；用击退前的原坐标）
        // 颜色统一入口 GetDamageColor：带元素伤害（element != None 且元素城邦生效）→ 元素色；
        // 无元素伤害（或非元素城邦，元素维度视为无）→ 伤害类型色（物理白/魔法紫/真实金）。
        // 样式按 damageKind 自动分流（物理纯色/魔法渐变/真实描边）
        if (FloatingTextPool.Instance != null && target.View != null)
        {
            Color floatColor = ElementColorMapper.GetDamageColor(element, damageKind);
            FloatingTextPool.Instance.ShowDamage(damage, target.View.transform.position, floatColor, damageKind);
        }
        target.TurnsSinceDamaged = 0;
        target.LastDamageSource = source;

        // 地下交易·累计伤害统计（三期，只读观察）：护盾未拦截的实际伤害，按攻击方累计
        //（Dot/Reflect 由 RecordDamage 内部过滤不计；DoT 的 source 可能为 null，防御性跳过）
        if (source != null)
            UndergroundTradeManager.Instance?.RecordDamage(source.Owner, damage, damageType);

        foreach (var passive in target.GetAllPassives())
            passive.OnDamageReceived(target, damage, damageType, damageKind);

        // 受击表现（纯异步；分级内聚在 PlayHitReaction——本方法为「统一伤害入口」，
        // 大招/溅射/DoT/环境/反伤全部经由此处，一次覆盖）
        PlayHitReaction(target, damageType, source);

        // 己方攻击命中广播（雷电将军·协同攻击）：大招路径尾部（Ultimate 来源——大招直伤/持续段
        // 如凛冬风暴每命中一次广播一次；Splash/Dot/Reflect 来源不广播，保持"攻击"语义）
        if (damageType == DamageSource.Ultimate)
            NotifyAllyAttackHit(source, target, damage, damageType);
        return true;
    }

    /// <summary>受击表现分级（判定内聚一处）：按伤害来源类型区分完整/轻量受击。
    /// 完整（位移 + 抖动）：直接伤害来源——普攻 Attack / 溅射 Splash / 大招 Ultimate；
    /// 轻量（仅抖动，无位移）：持续与环境伤害——DoT / 环境（Dot）与反伤（Reflect，已与需求确认）。
    /// 目标已死/已销毁不播（死亡动作由 DestroyPiece 承接，避免两动作在视觉子节点上争抢）。
    /// sourcePiece 提供受击反方向（轻量无位移不依赖）；来源不可得时完整受击退化为仅抖动。
    /// 两个调用点：AttackPiece（普攻族内联路径）与 ApplyIncomingDamage（大招/溅射/DoT/环境/反伤统一入口）。</summary>
    private void PlayHitReaction(PieceModel target, DamageSource source, PieceModel sourcePiece)
    {
        if (target == null || target.View == null || target.IsDead || target.IsDestroyed) return;
        bool full = (source & (DamageSource.Attack | DamageSource.Splash | DamageSource.Ultimate)) != 0;
        if (full)
        {
            Vector3? from = sourcePiece != null && sourcePiece.View != null
                ? (Vector3?)sourcePiece.View.transform.position
                : null;
            target.View.TakeHit(from);
        }
        else
        {
            target.View.TakeLightHit();
        }
    }

    /// <summary>己方攻击命中广播：通知攻击方阵营全体存活棋子的全部被动 OnAllyAttackHit。
    /// 两个调用点：AttackPiece 尾部（普攻族 Attack/Splash，含二段/溅射）与
    /// ApplyIncomingDamage 尾部（仅 Ultimate 来源）。DoT/反伤不广播（非"攻击"）。</summary>
    private static void NotifyAllyAttackHit(PieceModel attacker, PieceModel target, int damage, DamageSource source)
    {
        if (attacker == null || target == null) return;
        var allies = TurnManager.Instance?.Model?.GetPieces(attacker.Owner);
        if (allies == null) return;
        foreach (var ally in allies)
        {
            if (ally == null || ally.IsDead || ally.IsDestroyed) continue;
            foreach (var passive in ally.GetAllPassives())
                passive.OnAllyAttackHit(ally, attacker, target, damage, source);
        }
    }

    /// <summary>己方大招释放广播：通知释放者阵营全体存活棋子的全部被动 OnAllyUltimateCast。
    /// UseUltimateCore 在 Execute 完成后调用（能量已消耗、效果已结算）。</summary>
    private static void NotifyAllyUltimateCast(PieceModel caster)
    {
        if (caster == null) return;
        var allies = TurnManager.Instance?.Model?.GetPieces(caster.Owner);
        if (allies == null) return;
        foreach (var ally in allies)
        {
            if (ally == null || ally.IsDead || ally.IsDestroyed) continue;
            foreach (var passive in ally.GetAllPassives())
                passive.OnAllyUltimateCast(ally, caster);
        }
    }

    /// <summary>元素附着与反应状态副作用结算（带元素直接伤害共用入口，VoltPath/FlameNova/ThunderSweep 等）：
    /// ResolveElementInteraction 消耗/附着触发元素，并结算反应状态效果——
    /// 超导减防（含持续回合）、感电 DoT（记录施加者）、冻结。
    /// 伤害倍率/额外伤害由调用方经 DamageCalculator 计入伤害数值（本方法不改伤害）。
    /// 超载爆炸/击退为普攻 AttackPiece 特有，此处不复刻（同 FireSlash/ThunderSweep 既有口径）。
    /// triggerElement=None 时无操作（无元素棋子的 elemental 开关不产生附着）。</summary>
    public void ApplyElementInteraction(PieceModel target, PieceModel source,
        ElementType triggerElement, int triggerGauge, ElementReactionTable.ReactionConfig reaction)
    {
        if (target == null || triggerElement == ElementType.None) return;

        // 护盾染色封印（元素城邦二期）：已染色护盾封印同元素附着 → 只跳过附着写入；
        // 下方反应副作用（超导减防/感电DoT/冻结）照常结算——封印拦「附着」不拦「反应」
        if (!target.BlocksElementAttachment(triggerElement))
        {
            // 元素附着与消耗（gauge：被动=1 同普攻口径；大招=2 同大招募发量）
            var (resultElem, resultGauge) = ElementReactionTable.ResolveElementInteraction(
                target.AffixedElement, target.AffixedElementGauge, triggerElement, triggerGauge);
            target.AffixedElement = resultElem;
            target.AffixedElementGauge = resultGauge;
            // 染色：附着成功（结果非 None）→ 未染色护盾变为对应元素盾
            target.TryDyeShieldElement(resultElem);
        }

        // 反应视觉脉冲（纯表现，不参与任何结算）：触发元素反应时目标光环脉冲
        //（本方法是普攻以外全部元素交互的共用入口——被动/元素格/各大招，一次覆盖）
        if (reaction.Type != ReactionType.None)
            target.View?.PulseAura();

        // ---- 反应状态副作用（与 AttackPiece 同字段同口径）----
        if (reaction.DefenseReduction > 0)
        {
            target.CurrentDefenseReduction = reaction.DefenseReduction;
            target.DefenseReductionTurnsRemaining = reaction.DefenseReductionTurns;
        }
        if (reaction.FreezeTurns > 0)
        {
            target.FreezeTurnsRemaining = reaction.FreezeTurns;
            Debug.Log($"[PieceManager] {target.Data?.displayName} 被冻结 {reaction.FreezeTurns} 回合");
        }
        if (reaction.DotTurns > 0 && reaction.DotDamagePerTurn > 0)
        {
            target.DotDamagePerTurn = reaction.DotDamagePerTurn;
            target.DotTurnsRemaining = reaction.DotTurns;
            target.DotSource = source;  // 记录 DoT 施加者（回合结算时作为伤害来源）
            Debug.Log($"[PieceManager] {target.Data?.displayName} 感电，每回合 {reaction.DotDamagePerTurn} 伤害，持续 {reaction.DotTurns} 回合");
        }
    }

    /// <summary>伤害介入（C1）：遍历 target 全部被动的首伤=1（中娅悖论），冷却完毕且伤害>1 时降为 1</summary>
    private static void ApplyFirstDamageProtection(PieceModel target, ref int damage)
    {
        foreach (var passive in target.GetAllPassives())
            if (passive is FirstDamageToOnePassive fdp)
                fdp.TryReduceFirstDamage(ref damage);
    }

    /// <summary>伤害介入（C1）：遍历 target 全部被动的死亡之蔑，把伤害按比例拆进延迟池（ref 留立即部分）</summary>
    private static void ApplyDeathDelay(PieceModel target, ref int damage)
    {
        foreach (var passive in target.GetAllPassives())
            if (passive is DeathDancePassive ddp)
                ddp.SplitIncomingDamage(ref damage, target.EffectiveDefense);
    }

    /// <summary>护盾拦截（基座）：伤害命中护盾时按规则判定，返回 true = 本次伤害被完全免掉
    /// （调用方跳过「减伤→易伤→首伤=1→延迟拆分→扣血→免死→受伤通知」整条数值链；
    /// 元素附着/反应/冻结/感电/减防/金币/能量等由调用方照常处理，护盾不碰）。
    /// 拦截规则：
    ///   无盾 / damage≤0 → false（不拦截，照常结算）
    ///   真实（True）伤害 → 免掉 + 扣 1 层（无视元素免疫）
    ///   元素盾 · 伤害元素 == 盾元素 → 免疫（免掉且不扣层）
    ///   其余（普通盾 / 元素盾但伤害元素不同或无元素）→ 免掉 + 扣 1 层
    /// 扣到 0 层护盾消失（清元素标签）。「格挡 / 免疫」提示跳字（View 层，池未就绪静默跳过）。
    /// public static：DeathDancePassive 延迟伤害等非 PieceManager 内部路径也需调用。</summary>
    public static bool TryAbsorbByShield(PieceModel target, int damage, DamageKind kind, ElementType element)
    {
        if (target == null || target.ShieldStacks <= 0 || damage <= 0) return false;

        // 元素免疫：元素盾 + 同元素伤害 + 非 True → 免掉且不扣层
        if (kind != DamageKind.True
            && target.ShieldElement != ElementType.None
            && element == target.ShieldElement)
        {
            Debug.Log($"[PieceManager] {target.Data.displayName} 的{element}元素盾免疫伤害（盾 {target.ShieldStacks} 层，不扣层）");
            if (FloatingTextPool.Instance != null && target.View != null)
                FloatingTextPool.Instance.Get()?.Show("免疫",
                    target.View.transform.position + Vector3.up * 1.5f, FloatStyles.Armor());
            return true;
        }

        // 普通格挡：免掉 + 扣 1 层（True 伤害无视元素免疫也走这里）。
        // 扣层走 SetShield（触发 OnShieldChanged UI 事件；层数归 0 自动清元素标签）
        string elemMark = target.ShieldElement != ElementType.None ? $"{target.ShieldElement}" : "";
        int stacksAfter = target.ShieldStacks - 1;
        bool shattered = stacksAfter <= 0;
        target.SetShield(stacksAfter, target.ShieldElement);
        Debug.Log($"[PieceManager] {target.Data.displayName} 的{elemMark}护盾格挡 {damage} 伤害" +
                  (shattered ? "，护盾破碎" : $"（剩 {target.ShieldStacks} 层）"));
        if (FloatingTextPool.Instance != null && target.View != null)
            FloatingTextPool.Instance.Get()?.Show(shattered ? "盾碎" : "格挡",
                target.View.transform.position + Vector3.up * 1.5f, FloatStyles.Armor());
        return true;
    }

    /// <summary>死亡介入（C1）：遍历 target 全部被动的免死（中娅悖论），已死且冷却完毕时回 1 HP。
    /// public：死亡之蔑延迟伤害等回合内致死路径也需调用（免死响应一切致死）。</summary>
    public static void TryDefyDeath(PieceModel target)
    {
        foreach (var passive in target.GetAllPassives())
            if (passive is DeathDefyPassive defy)
                defy.TryDefy(target);
    }

    /// <summary>收集攻击方装备中实现了 IReactionModifier 的被动效果（按槽位顺序 0→1→2）</summary>
    private void CollectReactionModifiers(PieceModel attacker, List<IReactionModifier> buffer)
    {
        buffer.Clear();
        if (attacker?.EquippedItems == null) return;
        for (int i = 0; i < attacker.EquippedItems.Length; i++)
        {
            var equip = attacker.EquippedItems[i];
            if (equip == null) continue;
            foreach (var passive in equip.ActivePassives)
            {
                if (passive is IReactionModifier mod)
                    buffer.Add(mod);
            }
        }
    }

    /// <summary>元素之力元素量加成（③.3）：攻击方被动中 Element == attackerElem 的 ElementalCorePassive 的 ExtraGauge 总和</summary>
    private static int GetExtraElementGauge(PieceModel attacker, ElementType attackerElem)
    {
        int total = 0;
        foreach (var passive in attacker.GetAllPassives())
            if (passive is ElementalCorePassive ecp && ecp.Element == attackerElem)
                total += ecp.ExtraGauge;
        return total;
    }

    /// <summary>超载爆炸 AOE（③.3）：灼火/陨雷之力，超载时对目标周围敌方造成真实伤害。
    /// 爆炸伤害 = base + floor(超载伤害/divisor)；排除 target 本身（已受超载伤害）；
    /// 受害者先快照再结算（爆炸致死会改棋子列表）；致死 DestroyPiece(victim, attacker)（OnKill 生效）。</summary>
    private static void ApplyOverloadExplosion(PieceModel attacker, PieceModel target, int overloadDamage)
    {
        // 找第一个可爆炸的元素之力（唯一被动框架下同 id 只剩一件生效）
        ElementalCorePassive exploder = null;
        foreach (var passive in attacker.GetAllPassives())
            if (passive is ElementalCorePassive ecp && ecp.CanExplode) { exploder = ecp; break; }
        if (exploder == null) return;

        int explosionDamage = exploder.GetExplosionDamage(overloadDamage);
        if (explosionDamage <= 0) return;

        // 快照收集受害者：target 周围爆炸半径内的存活敌方（排除 target 本身）
        var enemySide = attacker.Owner == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;
        var enemies = TurnManager.Instance?.Model?.GetPieces(enemySide);
        var victims = new List<PieceModel>();
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead || enemy == target) continue;
                if (target.Coord.Distance(enemy.Coord) <= exploder.ExplosionRadius)
                    victims.Add(enemy);
            }
        }

        if (victims.Count == 0) return;
        Debug.Log($"[PieceManager] 超载爆炸：{target.Data.displayName} 周围 {victims.Count} 个敌方受 {explosionDamage} 魔法伤害（{exploder.Element}之力）");

        foreach (var victim in victims)
        {
            Instance.ApplyIncomingDamage(victim, explosionDamage, attacker, DamageSource.Splash, DamageKind.Magical);
            if (victim.IsDead)
                Instance.DestroyPiece(victim, attacker);
        }
    }

    /// <summary>附加伤害独立段：按「物理→魔法→真实」三份各自结算（PieceManager.AttackPiece 基础段之后调用）。
    /// 每份流程：物理段先吃防御减伤（max(1, extra - EffectiveDefense)，对标基础减法公式）→ 吃对应类型减伤被动 →
    /// 扣血 → 免死 → 触发「受到伤害」通知（携带正确伤害类型）。
    /// 不触发：被攻击通知 / 造成伤害通知（吸血）/ 元素反应与附着 / 冻结 / 感电 / 超载爆炸 / 击退 / 金币 / 能量 / 跳字。
    /// 首伤=1 与死亡之蔑延迟拆分只作用于基础段，此处不参与。致死不单独销毁——末尾 AttackPiece 统一销毁（击杀者=攻击者）。</summary>
    private static void ApplyExtraDamageSegments(PieceModel attacker, PieceModel target)
    {
        // DamageKind 枚举顺序 Physical=0 / Magical=1 / True=2，按此固定顺序逐份结算
        for (int k = 0; k < 3; k++)
        {
            if (target.IsDead) return; // 前一份致死且免死未救回 → 后续份不再结算
            var kind = (DamageKind)k;
            int extra = GetExtraDamage(attacker, kind);
            if (extra <= 0) continue;

            // 护盾拦截（基座）：拦在防御/减伤之前；附加段不带元素（None）→ 元素盾也按「元素不同」扣层，
            // True 附加无视元素免疫仍扣层。被拦截时跳过本段全部结算（扣血/免死/通知/跳字）
            if (TryAbsorbByShield(target, extra, kind, ElementType.None)) continue;

            int amount = kind == DamageKind.Physical
                ? Mathf.Max(1, extra - target.EffectiveDefense)   // 物理附加：先吃防御减伤（下限 1）
                : extra;                                          // 魔法/真实附加：不吃防御减伤
            amount = Mathf.Max(1, amount - GetTotalDamageReduction(target, kind));
            // 易伤结算（莫娜大招·星异）：附加段同样受增伤（口径：受到的所有伤害提高）
            ApplyVulnerability(target, ref amount);

            target.TakeDamage(amount);
            TryDefyDeath(target);

            // 地下交易·累计伤害统计（三期，只读观察）：附加段实际伤害（护盾未拦截），固定 Attack 来源
            UndergroundTradeManager.Instance?.RecordDamage(attacker.Owner, amount, DamageSource.Attack);

            foreach (var passive in target.GetAllPassives())
                passive.OnDamageReceived(target, amount, DamageSource.Attack, kind);
            // 跳字（按伤害类型取色+选形态；池/View 未就绪静默跳过）
            if (FloatingTextPool.Instance != null && target.View != null)
                FloatingTextPool.Instance.ShowDamage(amount, target.View.transform.position,
                    ElementColorMapper.GetDamageKindColor(kind), kind);
            Debug.Log($"[PieceManager] 附加伤害（{kind}）：{target.Data.displayName} 受 {amount}（剩余 {target.CurrentHP}）");
        }
    }

    /// <summary>累加 piece 全部被动中 kind 类型的附加伤害（ExtraDamagePassive；内建+装备）</summary>
    private static int GetExtraDamage(PieceModel piece, DamageKind kind)
    {
        if (piece == null) return 0;
        int total = 0;
        foreach (var passive in piece.GetAllPassives())
            if (passive is ExtraDamagePassive p && p.Kind == kind)
                total += p.Amount;
        return total;
    }

    /// <summary>累加 piece 全部被动中对 kind 类型生效的减伤（DamageReductionPassive；内建+装备，按 filter 筛选）</summary>
    private static int GetTotalDamageReduction(PieceModel piece, DamageKind kind)
    {
        if (piece == null) return 0;
        int total = 0;
        foreach (var passive in piece.GetAllPassives())
            if (passive is DamageReductionPassive p && p.AppliesTo(kind))
                total += p.Amount;
        return total;
    }

    /// <summary>易伤结算（莫娜大招·星异）：VulnerableTurnsRemaining&gt;0 时伤害 ×(1+VulnerablePercent%)，
    /// 下限 1。三个伤害管道（普攻基础段 / 附加伤害段 / 统一入口 ApplyIncomingDamage）在减伤之后、
    /// 首伤=1/死亡之蔑之前共用本方法——口径：受到的所有伤害提高。</summary>
    private static void ApplyVulnerability(PieceModel target, ref int damage)
    {
        if (damage <= 0 || target == null) return;
        if (target.VulnerableTurnsRemaining <= 0 || target.VulnerablePercent <= 0) return;
        damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f + target.VulnerablePercent / 100f)));
    }

    // ==========================================
    //  死亡 / 销毁
    // ==========================================
    /// <summary>销毁死亡棋子。killer 非空时通知其击杀被动（OnKill），并通知同阵营存活棋子的 OnAllyDeath。
    /// 可选参数保持既有调用方兼容（DoT 致死等无击杀者场景不传）。</summary>
    public void DestroyPiece(PieceModel model, PieceModel killer = null)
    {
        if (model == null) return;
        // 幂等保护：傀儡等可能在伤害链内被引爆销毁（OnDamageReceived → Detonate → DestroyPiece），
        // 外层流程（AttackPiece 尾部 / 击退等）重入时直接跳过，防 OnKill 误触发与重复销毁
        if (model.IsDestroyed) return;

        // 复活介入（哥伦比亚·生命之息）：在任何销毁状态变更（占据移除/回合注销/视图销毁）之前询问。
        // 复活成功 → 取消销毁整条流程（棋子原地满状态续命，不触发 OnKill/OnAllyDeath——击杀未成立，
        // 与中娅悖论免死后不触发击杀通知的口径一致）。
        if (TryReviveAlly(model)) return;

        model.IsDestroyed = true;
        PieceLayoutModel.Instance?.Remove(model.Coord);
        TurnManager.Instance?.UnregisterPiece(model);

        // 被动事件通知（纯通知）：击杀者 OnKill
        if (killer != null)
            foreach (var passive in killer.GetAllPassives()) passive.OnKill(killer, model);

        // 同阵营存活棋子 OnAllyDeath（victim 已注销，列表不含自身）。
        // 召唤物（傀儡等 isSummon）不算"队友死亡"——不触发战旗等 OnAllyDeath 消费者
        if (!model.Data.isSummon)
        {
            var allies = TurnManager.Instance?.Model?.GetPieces(model.Owner);
            if (allies != null)
            {
                foreach (var ally in allies)
                {
                    if (ally == null || ally.IsDead) continue;
                    foreach (var passive in ally.GetAllPassives()) passive.OnAllyDeath(ally, model);
                }
            }
        }

        // 视图延后销毁：逻辑层死亡处理（占据移除/回合注销/被动通知/胜负判定）已在上方立即完成，
        // 这里只延后 GameObject 的销毁——先播死亡动作，动作播完再 Destroy（不延迟逻辑，纯表现延后）
        if (model.View != null)
        {
            GameObject viewGo = model.View.gameObject;
            model.View.PlayDeath(() => { if (viewGo != null) Destroy(viewGo); });
        }

        // 战争之城·主将双判负（斩首 / 光杆司令）：销毁完成后通知（null 安全纯通知，
        // 判断内聚 WarCityManager——非战争城邦内部直接返回，不影响现有销毁流程）
        WarCityManager.Instance?.OnPieceDestroyed(model);

        Debug.Log($"[PieceManager] {model.Data.displayName} 死亡销毁");
    }

    /// <summary>复活询问（哥伦比亚·生命之息）：遍历 dying 同阵营存活棋子（不含自身）的全部被动，
    /// 找到未消耗的 RevivePassive 则复活 dying 并返回 true（DestroyPiece 据此取消销毁）。
    /// dying 此刻尚未注销（拦截发生在状态变更前），GetPieces 列表仍含 dying，用 ReferenceEquals 排除。
    /// AOE 多死顺序处理：首个死亡消耗复活次数，后续死亡正常销毁（一局一次的自然结果）。</summary>
    private bool TryReviveAlly(PieceModel dying)
    {
        var allies = TurnManager.Instance?.Model?.GetPieces(dying.Owner);
        if (allies == null) return false;
        foreach (var ally in allies)
        {
            if (ally == null || ally.IsDead || ReferenceEquals(ally, dying)) continue;
            foreach (var passive in ally.GetAllPassives())
                if (passive is RevivePassive rp && rp.TryRevive(dying))
                    return true;
        }
        return false;
    }

    // ==========================================
    //  大招
    // ==========================================
    /// <summary>释放大招（敌人 / 自身模式入口）：消耗满能量 + 1 AP，工厂创建 IUltimateEffect 并执行两参 Execute。
    /// target 对自身增益型大招（targetMode=Self / 旧 requiresTarget=false）会被忽略（调用方传 caster）。
    /// 注意：先校验 AP 再消耗能量，避免 AP 不足时能量被吞（与需求伪代码顺序略有调整，更安全）。</summary>
    public void UseUltimate(PieceModel caster, PieceModel target) => UseUltimateCore(caster, target, null);

    /// <summary>释放大招（指定格模式入口，targetMode=Tile）：效果走三参 Execute 拿目标格坐标。
    /// target 为目标格上的棋子（空格时为 null），由效果自行决定如何使用。</summary>
    public void UseUltimate(PieceModel caster, PieceModel target, HexCoord targetCoord)
        => UseUltimateCore(caster, target, targetCoord);

    /// <summary>大招执行核心：AP / 能量（或蓄力）校验消耗 → 工厂创建效果 → 按模式分发 Execute → 目标死亡销毁。
    /// 能量消耗：部分能量型（IPartialEnergyUltimate）按效果自报量扣（满能校验不变，保留剩余）；
    /// 其余大招满能全清（TryConsume 原行为，零变化）。
    /// 蓄力型（usesChargeSystem=true，甘雨）：不走能量——校验蓄力层数 ≥1，Execute 后清零（效果读层数分档）。</summary>
    private void UseUltimateCore(PieceModel caster, PieceModel target, HexCoord? targetCoord)
    {
        if (caster == null || caster.Data?.ultimateConfig == null) return;
        bool chargeMode = caster.IsChargeSystem;
        if (!chargeMode && caster.Energy == null) return;   // 非蓄力型必须有大招能量配置

        Debug.Log($"[大招诊断] UseUltimateCore：{caster.Data.displayName}，" +
                  (chargeMode ? $"蓄力 {caster.ChargeStacks}/{caster.MaxChargeStacks}"
                    : $"能量 {caster.Energy.CurrentEnergy}/{caster.Energy.MaxEnergy}") +
                  $"，AP够={APManager.Instance != null && APManager.Instance.HasAP(caster.Owner, 1)}，" +
                  $"效果类={caster.Data.ultimateConfig.effectClassName}");

        // 先校验 AP，再消耗能量/蓄力（避免能量被吞）
        if (APManager.Instance == null || !APManager.Instance.HasAP(caster.Owner, 1))
        {
            Debug.Log($"[PieceManager] {caster.Data.displayName} AP 不足，无法释放大招");
            return;
        }

        // 效果先于能量消耗创建（部分能量型需先询问 GetEnergyCost；未知类名时能量与 AP 均未消耗）
        var cfg = caster.Data.ultimateConfig;
        var effect = CreateUltimate(cfg.effectClassName, cfg.effectJsonParams);
        if (effect == null)
        {
            Debug.LogWarning($"[PieceManager] 未知大招类名: {cfg.effectClassName}");
            return;
        }

        // 能量/蓄力校验消耗
        if (chargeMode)
        {
            // 蓄力型：层数 ≥1 才可释放（清零延迟到 Execute 后——效果读当前层数分档）
            if (caster.ChargeStacks < 1)
            {
                Debug.Log($"[PieceManager] {caster.Data.displayName} 蓄力层数不足，无法释放大招");
                return;
            }
        }
        else if (effect is IPartialEnergyUltimate partial)
        {
            if (!caster.Energy.TryConsume(partial.GetEnergyCost(caster)))
            {
                Debug.Log($"[PieceManager] {caster.Data.displayName} 能量未满，无法释放大招");
                return;
            }
        }
        else if (!caster.Energy.TryConsume())
        {
            Debug.Log($"[PieceManager] {caster.Data.displayName} 能量未满，无法释放大招");
            return;
        }
        APManager.Instance.ConsumeAP(caster.Owner, 1);

        // 元素格子（大招统一入口）：先声明范围——必须在 Execute 之前快照
        //（雷驰突进等效果会移动释放者，Execute 后再取坐标范围就错了）。
        // 只声明不替换：替换统一由 ElementTileManager 完成（各大招零替换逻辑）。
        List<HexCoord> elementTileArea = null;
        if (cfg.enableElementTiles)
        {
            if (effect is IUltimateAreaProvider areaProvider)
                elementTileArea = areaProvider.GetUltimateArea(caster, target, targetCoord);
            else if (!_elementTileAdapterMissingWarned)
            {
                _elementTileAdapterMissingWarned = true;
                Debug.LogWarning($"[PieceManager] {cfg.effectClassName} 勾选了元素格子但未实现 IUltimateAreaProvider，" +
                                 $"本次释放不产生格子替换（请为大招类补充范围声明）");
            }
        }

        if (targetCoord.HasValue)
            effect.Execute(caster, target, targetCoord.Value);   // 指定格模式：三参（默认委托两参，现有大招零改动）
        else
            effect.Execute(caster, target);

        // 大招扩散光环（纯表现，不影响任何结算/能量/回合流转）：释放者脚下圆环扩散一圈淡出。
        // 在 Execute 之后触发——锚定大招结算后释放者的最终逻辑格（雷驰突进等会移动释放者）
        caster.View?.PlayUltimateSpread();

        // 蓄力型：释放后层数清零（在 Execute 之后——效果已按释放时层数结算）
        if (chargeMode) caster.ChargeStacks = 0;

        // 元素格子替换：勾选机制 + 释放者有先天元素 → 声明的范围整体替换为对应元素格
        //（在 Execute 之后落格——本次大招结算不受新格子影响；ApplyElementTiles 内含同格覆盖/刷新规则）
        if (elementTileArea != null && elementTileArea.Count > 0 && caster.Data.innateElement != ElementType.None)
            ElementTileManager.Instance?.ApplyElementTiles(
                elementTileArea, caster.Data.innateElement, cfg.elementTileDurationTurns);

        // 己方大招释放广播（雷电将军·雷罚恶曜之眼）：Execute 完成后通知释放者阵营全体存活棋子
        //（能量已消耗、效果已结算——回能类被动拿到的是消耗后的能量状态）
        NotifyAllyUltimateCast(caster);

        Debug.Log($"[PieceManager] {caster.Data.displayName} 释放大招「{cfg.ultimateName}」" +
                  $"{(cfg.IsSelfCast ? "（自身增益）" : targetCoord.HasValue ? $"，目标格 {targetCoord.Value}" : $"，目标 {target?.Data.displayName}")}");

        // 大招致目标死亡则销毁（killer=caster，通知 OnKill / OnAllyDeath；自身增益型 target==caster 不会进入此分支）
        if (target != null && target.IsDead && !ReferenceEquals(caster, target))
            DestroyPiece(target, caster);
    }

    // ==========================================
    //  大招效果工厂
    // ==========================================
    /// <summary>简单工厂：按类名创建大招效果实例。新增大招在此追加 case。（参照 EquipmentManager.CreatePassive）</summary>
    private IUltimateEffect CreateUltimate(string className, string jsonParams)
    {
        switch (className)
        {
            case "FireSlashUltimate":
                var p = JsonUtility.FromJson<FireSlashParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams);
                return new FireSlashUltimate(p.bonusDamage);
            case "IceBarrierUltimate":
                return new IceBarrierUltimate();
            case "FlameLanceUltimate":
                var fl = JsonUtility.FromJson<FlameLanceParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams);
                return new FlameLanceUltimate(fl.length, fl.bonusDamage);
            case "ThunderSweepUltimate":
                var ts = JsonUtility.FromJson<ThunderSweepParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams);
                return new ThunderSweepUltimate(ts.normalBonusDamage, ts.empoweredBaseDamage,
                    ts.empoweredAttackPercent, ts.empoweredEnergyCost, ts.empoweredElemental);
            case "FrostArrowUltimate":
                var fa = JsonUtility.FromJson<FrostArrowParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams);
                return new FrostArrowUltimate(fa.physicalBonusDamage, fa.magicBaseDamage, fa.magicAttackPercent);
            case "BaronBunnyUltimate":
                var bb = JsonUtility.FromJson<BaronBunnyParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams);
                return new BaronBunnyUltimate(bb.puppetPieceId);
            case "OmenUltimate":
                var om = JsonUtility.FromJson<OmenParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams);
                return new OmenUltimate(om.radius, om.duration, om.percent);
            case "FrostFeastUltimate":
                var ff = JsonUtility.FromJson<FrostFeastParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams);
                return new FrostFeastUltimate(ff.radius, ff.attackPercent, ff.healAmount, ff.stormTurns);
            case "MusouStrikeUltimate":
                var ms = JsonUtility.FromJson<MusouStrikeParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams);
                return new MusouStrikeUltimate(ms.attackPercent, ms.synergyTurns);
            case "TideSurgeUltimate":
                var td = JsonUtility.FromJson<TideSurgeParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams);
                return new TideSurgeUltimate(td.radius, td.bonusAttack, td.duration);
            case "ThunderDashUltimate":
                return new ThunderDashUltimate(JsonUtility.FromJson<ThunderDashParams>(
                    string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams).healAmount);
            default:
                return null;
        }
    }

    // JsonUtility 反序列化用的参数包装类
    [System.Serializable]
    private class FireSlashParams { public int bonusDamage = 0; }

    // JsonUtility 反序列化用的参数包装类
    [System.Serializable]
    private class FlameLanceParams
    {
        public int length = 6;        // 射线长度（需与 ultimateConfig.tileTargetRange 保持一致）
        public int bonusDamage = 0;   // 附加攻击力
    }

    // JsonUtility 反序列化用的参数包装类
    [System.Serializable]
    private class BaronBunnyParams
    {
        public int puppetPieceId = 0;       // 傀儡棋子 id（PieceRegistry 查找键；傀儡资产需 isSummon=true + PuppetPassive）
    }

    // JsonUtility 反序列化用的参数包装类
    [System.Serializable]
    private class OmenParams
    {
        public int radius = 2;      // 易伤作用半径（目标格为中心，含中心格）
        public int duration = 4;    // 持续回合（每次任一方回合开始递减）
        public int percent = 50;    // 增伤百分比（50 = 受到伤害 +50%）
    }

    // JsonUtility 反序列化用的参数包装类
    [System.Serializable]
    private class FrostFeastParams
    {
        public int radius = 1;          // 即时段作用半径（以自身为中心）
        public int attackPercent = 100; // 即时段物理攻击力百分比
        public int healAmount = 10;     // 友方回血量（自身翻倍；Heal 封顶 MaxHP）
        public int stormTurns = 4;      // 凛冬风暴持续回合（风暴半径/伤害在 BlizzardPassive 侧 jsonParams）
    }

    // JsonUtility 反序列化用的参数包装类
    [System.Serializable]
    private class MusouStrikeParams
    {
        public int attackPercent = 100; // 横向 3 格段攻击力百分比
        public int synergyTurns = 6;    // 协同状态持续回合（协同伤害/回能在 CoordinatedStrikePassive 侧 jsonParams）
    }

    // JsonUtility 反序列化用的参数包装类
    [System.Serializable]
    private class FrostArrowParams
    {
        public int physicalBonusDamage = 0;   // 1/2 层：附加攻击力（物理，走冰元素反应）
        public int magicBaseDamage = 0;       // 3 层：魔法基础伤害值
        public int magicAttackPercent = 0;    // 3 层：攻击力百分比（0=不吃攻击加成）
    }

    // JsonUtility 反序列化用的参数包装类
    [System.Serializable]
    private class ThunderSweepParams
    {
        public int normalBonusDamage = 0;      // 普通态：附加攻击力（物理，走元素反应）
        public int empoweredBaseDamage = 0;    // 强化态：魔法基础伤害值
        public int empoweredAttackPercent = 0; // 强化态：攻击力百分比（0=不吃攻击加成）
        public int empoweredEnergyCost = 50;   // 强化态：能量消耗量（保留剩余；普通态全清不受此参数影响）
        public bool empoweredElemental = true; // 强化态：是否带先天元素（false=旧纯魔法段口径不附着不反应）
    }

    // JsonUtility 反序列化用的参数包装类
    [System.Serializable]
    private class TideSurgeParams
    {
        public int radius = 5;        // 增益半径（以自身为圆心，含中心格=含自身）
        public int bonusAttack = 0;   // 攻击力加成值
        public int duration = 4;      // 持续回合
    }

    // JsonUtility 反序列化用的参数包装类
    [System.Serializable]
    private class ThunderDashParams
    {
        public int healAmount = 10;   // 冲刺后回血量
    }
}
