using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 季风之城管理器（一期：四季 + 昼夜）—— 对标 TradeCityManager / UndergroundTradeManager 的单例管理器。
/// 挂载在场景中的 MonsoonManager GameObject 上（可与其他城邦 Manager 放一起）。
///
/// 职责（内聚过滤：仅当前城邦 == 季风之城时生效，判断不散落到 TurnManager / PieceModel 调用点）：
///   1. 对局级状态：已完成轮数 → 派生当前季节 / 昼夜（切换按「轮」，双方各行动一次 = 1 轮）
///   2. 回合钩子：该方回合开始（回血类效果）/ 该方回合结束（金币 / 扣血类效果）
///   3. 聚合查询：有效攻击力加成（夏）/ 移动范围修正（昼夜 + 季节移动槽位，含下限 clamp）
///
/// 效果数值与节奏全部从 MonsoonConfig 资产读取（数据驱动，代码零硬编码）；
/// 未拖配置资产时回退 Resources/MonsoonConfig，再回退运行时默认实例（字段默认值）。
///
/// 时间单位铁律：季节 / 昼夜切换严格按「轮」（后手方结束行动、一轮完成时），不按「回合」。
/// </summary>
public class MonsoonManager : MonoBehaviour
{
    public static MonsoonManager Instance { get; private set; }

    [Header("配置资产（空则读 Resources/MonsoonConfig，再空则用运行时默认值）")]
    [Tooltip("Create > Chess > Monsoon Config 创建后拖入；改数值/换效果无需改代码")]
    public MonsoonConfig config;

    private MonsoonConfig _runtimeConfig;
    private int _roundsCompleted;      // 已完成轮数（当前轮 = _roundsCompleted + 1）
    private bool _phaseLogged;         // 首次激活时的相位日志只打一次

    // ---- 气候状态（二期·简单组）----
    /// <summary>活跃气候（运行时）：配置条目 + 剩余轮数；可同时存在多个（暴雨/暴雪可叠加）</summary>
    private class ActiveClimate
    {
        public MonsoonConfig.ClimateEntry entry;
        public int roundsRemaining;
    }
    private readonly List<ActiveClimate> _activeClimates = new List<ActiveClimate>();
    private int _lastClimateRollRound = -1;   // 已判定过的轮号（每轮只判定一次；-1 = 尚未判定过任何轮）

    // ---- 飓风·幸运方块（三期B；「格子附着实体」基础，三期C 雷电残留复用本模式）----
    /// <summary>棋盘上的幸运方块（运行时实体）：坐标 + 档位。双方共享可见、共同抢；拾取或飓风结束时消失</summary>
    private class LuckyBox
    {
        public HexCoord coord;
        public int tierIndex;   // 对应 Config.lootBoxTiers 下标
    }
    private readonly List<LuckyBox> _luckyBoxes = new List<LuckyBox>();
    /// <summary>本轮掉落决策（轮号守卫）：null = 本轮尚未决策；-1 = 本轮不掉；≥0 = 本轮掉落的档位下标</summary>
    private int _roundDropTier = int.MinValue;   // int.MinValue = 尚未决策（区别于 -1 不掉）
    private int _lastDropDecisionRound = -1;

    /// <summary>季风机制生效判定（内聚过滤入口）：当前城邦 == 季风之城</summary>
    private static bool MonsoonActive
        => CityStateManager.Instance != null && CityStateManager.Instance.IsActive(CityStateKind.Monsoon);

    /// <summary>生效配置（懒加载：Inspector → Resources → 运行时默认实例）</summary>
    private MonsoonConfig Config
    {
        get
        {
            if (_runtimeConfig == null)
            {
                _runtimeConfig = config != null ? config
                    : (Resources.Load<MonsoonConfig>("MonsoonConfig") ?? ScriptableObject.CreateInstance<MonsoonConfig>());
            }
            return _runtimeConfig;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==========================================
    //  相位派生（轮 → 季节 / 昼夜）
    // ==========================================
    /// <summary>当前季节槽位索引（0=春…3=冬，按 roundsPerSeason 轮一季循环）；配置槽位数异常时安全回退 0</summary>
    private int SeasonIndex
    {
        get
        {
            int perSeason = Mathf.Max(1, Config.roundsPerSeason);
            int count = Config.seasons != null ? Config.seasons.Count : 0;
            if (count == 0) return -1;
            return (_roundsCompleted / perSeason) % count;
        }
    }

    /// <summary>当前是否为昼（每季前 dayRoundsPerSeason 轮）</summary>
    private bool IsDay
    {
        get
        {
            int perSeason = Mathf.Max(1, Config.roundsPerSeason);
            return (_roundsCompleted % perSeason) < Mathf.Max(0, Config.dayRoundsPerSeason);
        }
    }

    /// <summary>当前生效的槽位集合：季节槽位 + 昼（或夜）槽位（效果按类型在各钩子/聚合中结算）</summary>
    private IEnumerable<MonsoonConfig.EffectSlot> ActiveSlots
    {
        get
        {
            int idx = SeasonIndex;
            if (idx >= 0 && Config.seasons != null && idx < Config.seasons.Count && Config.seasons[idx] != null)
                yield return Config.seasons[idx];
            var dn = IsDay ? Config.daySlot : Config.nightSlot;
            if (dn != null) yield return dn;
        }
    }

    /// <summary>当前相位描述（日志用）：如「春·昼」</summary>
    private string PhaseDesc
    {
        get
        {
            int idx = SeasonIndex;
            string season = idx >= 0 && Config.seasons != null && idx < Config.seasons.Count && Config.seasons[idx] != null
                ? Config.seasons[idx].slotName : "?";
            return $"{season}·{(IsDay ? "昼" : "夜")}";
        }
    }

    // ==========================================
    //  轮推进（TurnManager 在一轮完成时调用；严格按「轮」）
    // ==========================================
    /// <summary>轮结束：推进轮数并结算季节 / 昼夜切换（非季风城邦下整套不触发）</summary>
    public void OnRoundEnded()
    {
        if (!MonsoonActive) return;

        string oldPhase = PhaseDesc;
        _roundsCompleted++;
        string newPhase = PhaseDesc;

        if (newPhase != oldPhase)
            Debug.Log($"[MonsoonManager] 相位切换：{oldPhase} → {newPhase}（第 {_roundsCompleted + 1} 轮起生效）");

        // 雷电残留按「轮」结算（三期C）：停留伤害 → 强度衰减/归零移除（轮号守卫每轮一次）
        TickResidues(_roundsCompleted);
    }

    // ==========================================
    //  回合钩子（TurnManager 调用）
    // ==========================================
    /// <summary>该方回合开始：结算回血类效果（春：全体存活棋子回 value 血，封顶 MaxHP）</summary>
    public void OnTurnStarted(PlayerSide side)
    {
        if (!MonsoonActive) return;

        if (!_phaseLogged)
        {
            _phaseLogged = true;
            Debug.Log($"[MonsoonManager] 季风之城生效：第 {_roundsCompleted + 1} 轮 · {PhaseDesc}");
        }

        foreach (var slot in ActiveSlots)
        {
            if (slot.effectType != MonsoonConfig.EffectType.Heal || slot.value <= 0) continue;
            var pieces = GetSidePieces(side);
            if (pieces == null) continue;
            foreach (var piece in pieces)
            {
                if (piece == null || piece.IsDead) continue;
                piece.Heal(slot.value);
            }
            Debug.Log($"[MonsoonManager] {PhaseDesc}·{slot.slotName}：{(side == PlayerSide.P1 ? "玩家1" : "玩家2")} 全体回血 +{slot.value}");
        }

        // 气候判定（二期）：每轮开始时随机判定一次（轮号守卫，P2 回合内同轮不重复判定）
        TickAndRollClimates();

        // 飓风方块掉落（三期B）：决策按轮（轮号守卫）；执行在本回合开始掉一批
        DropLuckyBoxesForTurn(_roundsCompleted + 1);
    }

    /// <summary>该方回合结束：结算金币类（秋：+value 金币）与扣血类（冬：全体 -value 血，可致死）效果</summary>
    public void OnTurnEnded(PlayerSide side)
    {
        if (!MonsoonActive) return;

        foreach (var slot in ActiveSlots)
        {
            if (slot.effectType == MonsoonConfig.EffectType.Gold && slot.value != 0)
            {
                GoldManager.Instance?.AddGold(side, slot.value);
                Debug.Log($"[MonsoonManager] {PhaseDesc}·{slot.slotName}：{(side == PlayerSide.P1 ? "玩家1" : "玩家2")} 金币 +{slot.value}");
            }
            else if (slot.effectType == MonsoonConfig.EffectType.Damage && slot.value > 0)
            {
                ApplySeasonDamage(side, slot);
            }
        }
    }

    /// <summary>扣血类效果结算（冬）：全体存活棋子走统一伤害入口（正常扣血结算，死亡判定照常；
    /// 来源 null / DamageSource.Dot = 环境伤害，不计地下交易统计），致死者循环外销毁（同 DoT 口径）</summary>
    private void ApplySeasonDamage(PlayerSide side, MonsoonConfig.EffectSlot slot)
    {
        var pieces = GetSidePieces(side);
        if (pieces == null) return;

        var killed = new List<PieceModel>();
        int affected = 0;
        // 快照遍历：避免结算过程中集合被修改
        foreach (var piece in new List<PieceModel>(pieces))
        {
            if (piece == null || piece.IsDead) continue;
            PieceManager.Instance?.ApplyIncomingDamage(piece, slot.value, null, DamageSource.Dot, DamageKind.True);
            affected++;
            if (piece.IsDead) killed.Add(piece);
        }
        Debug.Log($"[MonsoonManager] {PhaseDesc}·{slot.slotName}：{(side == PlayerSide.P1 ? "玩家1" : "玩家2")} 全体 {affected} 个棋子扣血 {slot.value}");
        foreach (var dead in killed)
        {
            Debug.Log($"[MonsoonManager] {dead.Data.displayName} 因{slot.slotName}季扣血死亡");
            PieceManager.Instance?.DestroyPiece(dead);
        }
    }

    // ==========================================
    //  聚合查询（PieceModel 调用；非季风城邦返回零修正）
    // ==========================================
    /// <summary>有效攻击力加成（夏：攻击 +value；季节与昼夜槽位中 Attack 类效果求和）。
    /// 非季风城邦返回 0（PieceModel.EffectiveAttack 聚合点调用）。</summary>
    public int GetAttackBonus()
    {
        if (!MonsoonActive) return 0;
        int sum = 0;
        foreach (var slot in ActiveSlots)
            if (slot.effectType == MonsoonConfig.EffectType.Attack) sum += slot.value;
        return sum;
    }

    /// <summary>移动范围修正（昼夜：昼 +1 / 夜 -1，含季节槽位 Move 类效果求和），
    /// 最终值 clamp 到配置下限（默认 1，夜 -1 后至少可行动 1 格）。
    /// 非季风城邦原样返回 baseRange（无修正无 clamp，行为与无机制完全一致）。</summary>
    public int GetMoveRange(int baseRange)
    {
        if (!MonsoonActive) return baseRange;
        int bonus = 0;
        foreach (var slot in ActiveSlots)
            if (slot.effectType == MonsoonConfig.EffectType.Move) bonus += slot.value;
        return Mathf.Max(Config.minMoveRange, baseRange + bonus);
    }

    /// <summary>获取指定方棋子列表（TurnManager 未就绪时返回 null）</summary>
    private static List<PieceModel> GetSidePieces(PlayerSide side)
    {
        var model = TurnManager.Instance != null ? TurnManager.Instance.Model : null;
        return model != null ? model.GetPieces(side) : null;
    }

    // ==========================================
    //  气候（二期·简单组：暴雨/暴雪/高温）
    //  时间单位铁律：判定与持续递减均按「轮」计（轮号守卫，每轮只判定一次）
    // ==========================================
    /// <summary>每轮开始的气候结算：① 存活气候持续轮数 -1、到期移除；② 随机判定新气候。
    /// 由 OnTurnStarted 触发（轮号守卫保证 P1/P2 两个回合内只结算一次 = 「轮」口径）</summary>
    private void TickAndRollClimates()
    {
        int currentRound = _roundsCompleted + 1;
        if (_lastClimateRollRound == currentRound) return;   // 本轮已判定过
        _lastClimateRollRound = currentRound;

        TickClimateDurations();
        RollNewClimates(currentRound);

        // 本轮气候总览日志（晴 = 无任何活跃气候）
        string summary = _activeClimates.Count == 0 ? "晴"
            : string.Join("、", _activeClimates.ConvertAll(c => $"{c.entry.climateName}（剩余 {c.roundsRemaining} 轮）"));
        Debug.Log($"[MonsoonManager] 第 {currentRound} 轮气候：{summary}");
    }

    /// <summary>存活气候持续递减：每轮开始 -1，归零移除（持续期间生效，到期自然消失）。
    /// 飓风到期时同步清除全部未拾取幸运方块（高亮还原、商店恢复）</summary>
    private void TickClimateDurations()
    {
        for (int i = _activeClimates.Count - 1; i >= 0; i--)
        {
            _activeClimates[i].roundsRemaining--;
            if (_activeClimates[i].roundsRemaining <= 0)
            {
                Debug.Log($"[MonsoonManager] 气候到期移除：{_activeClimates[i].entry.climateName}");
                if (_activeClimates[i].entry.effectKind == MonsoonConfig.ClimateEffectKind.Hurricane)
                    ClearAllBoxes("飓风结束");   // 方块保留到被拾取或飓风结束——到期统一清除
                _activeClimates.RemoveAt(i);
            }
        }
    }

    /// <summary>随机判定新气候：各条目独立判定概率（专属气候非对应季节概率视为 0 不参与）；
    /// 可叠加气候（暴雨/暴雪）可同时出现；独占气候（高温）命中时本轮只保留它一个（其余新判定丢弃）。
    /// 持续轮数随机取 [min, max]（默认 1~3 轮）</summary>
    private void RollNewClimates(int currentRound)
    {
        var entries = Config.climates;
        if (entries == null) return;

        var rolled = new List<MonsoonConfig.ClimateEntry>();
        foreach (var entry in entries)
        {
            if (entry == null || entry.effectKind == MonsoonConfig.ClimateEffectKind.None) continue;
            // 专属气候限制：非对应季节 → 概率 0、不参与判定
            if (entry.restrictedSeasonIndex >= 0 && entry.restrictedSeasonIndex != SeasonIndex) continue;
            if (RollProbability(entry.probability))
                rolled.Add(entry);
        }

        // 独占语义：独占气候（高温）命中 → 本轮不再判定其他可叠加气候（丢弃其余新判定）
        var exclusiveHit = rolled.Find(e => e.exclusive);
        if (exclusiveHit != null)
            rolled.RemoveAll(e => e != exclusiveHit);

        foreach (var entry in rolled)
        {
            int duration = Random.Range(
                Mathf.Max(1, Config.climateDurationMinRounds),
                Mathf.Max(1, Config.climateDurationMaxRounds) + 1);
            _activeClimates.Add(new ActiveClimate { entry = entry, roundsRemaining = duration });
            Debug.Log($"[MonsoonManager] 气候出现：{entry.climateName}（第 {currentRound} 轮起，持续 {duration} 轮）");

            // 雷暴出现时一次性雷劈（三期C）：随机格雷劈 + 留雷电残留 + 概率永久加成
            if (entry.effectKind == MonsoonConfig.ClimateEffectKind.Lightning)
                StrikeLightning(currentRound);
        }
    }

    /// <summary>气候概率判定（唯一随机入口；在线 PVP 网络化时替换为同步 seed）</summary>
    private static bool RollProbability(float probability)
        => probability > 0f && Random.value < probability;

    /// <summary>指定效果类型的气候当前是否活跃（含内聚过滤：非季风城邦恒 false）</summary>
    private bool IsClimateActive(MonsoonConfig.ClimateEffectKind kind)
    {
        if (!MonsoonActive) return false;
        foreach (var c in _activeClimates)
            if (c.entry != null && c.entry.effectKind == kind) return true;
        return false;
    }

    /// <summary>暴雨：上回合受击的棋子本回合攻击距离减 rainAttackRangePenalty（下限 1 格，至少可攻击周围 1 格）。
    /// 「上回合受击」= TurnsSinceDamaged ≤ 1（受击归零、每次任一方回合开始 +1；
    /// 值 1 = 上一回合受击，值 0 = 本回合内受击——均已受击）。未受击者原样返回。
    /// 数值来源 = 专属参数区块（多个暴雨条目并存时按条目数累计；三期A前同口径读 entry.value）。
    /// PieceModel.AttackRange 聚合点调用；非季风城邦原样返回（无修正无 clamp，行为不变）</summary>
    public int GetAttackRange(PieceModel piece, int baseRange)
    {
        if (!IsClimateActive(MonsoonConfig.ClimateEffectKind.Rain)) return baseRange;
        if (piece == null || piece.TurnsSinceDamaged > 1) return baseRange;
        int penalty = 0;
        int perEntry = Config != null ? Mathf.Max(0, Config.rainAttackRangePenalty) : 0;
        foreach (var c in _activeClimates)
            if (c.entry != null && c.entry.effectKind == MonsoonConfig.ClimateEffectKind.Rain)
                penalty += perEntry;
        return Mathf.Max(1, baseRange - penalty);
    }

    /// <summary>暴雪：棋子攻击后，被攻击棋子被击退 blizzardKnockbackDistance 格（复用超载击退
    /// KnockbackResolver + TeleportPiece，遇阻/边界由解析器自然处理）。
    /// 数值来源 = 专属参数区块（多个暴雪条目并存时按条目数累计；三期A前同口径读 entry.value）。
    /// PieceManager.AttackPiece 伤害结算后调用（null 安全，纯通知入口）</summary>
    public void OnAttackLanded(PieceModel attacker, PieceModel target)
    {
        if (!IsClimateActive(MonsoonConfig.ClimateEffectKind.Blizzard)) return;
        if (attacker == null || target == null || target.IsDead) return;

        int distance = 0;
        int perEntry = Config != null ? Mathf.Max(1, Config.blizzardKnockbackDistance) : 1;
        foreach (var c in _activeClimates)
            if (c.entry != null && c.entry.effectKind == MonsoonConfig.ClimateEffectKind.Blizzard)
                distance += perEntry;
        if (distance <= 0) return;

        var knockbackTo = KnockbackResolver.Resolve(attacker.Coord, target.Coord, distance);
        if (knockbackTo.HasValue)
        {
            PieceManager.Instance?.TeleportPiece(target, knockbackTo.Value);
            Debug.Log($"[MonsoonManager] 暴雪：{target.Data.displayName} 被击退 {distance} 格 → {knockbackTo.Value}");
        }
    }

    /// <summary>高温：移动入口拦截——已行动（HasAttackedThisTurn 已置位，含本回合攻击过/移动过被锁定）
    /// 的棋子不能再移动。BattleController.HandleMove 入口调用；非季风城邦/无高温恒 false</summary>
    public bool IsMoveBlockedByHeat(PieceModel piece)
        => IsClimateActive(MonsoonConfig.ClimateEffectKind.Heat) && piece != null && piece.HasAttackedThisTurn;

    /// <summary>高温：行动后锁定——棋子完成一次移动后置位 HasAttackedThisTurn（复用「本回合已攻击」标记，
    /// 攻击入口已有该闸门 → 移动后不能再攻击；本方法置位后也不能再移动）。
    /// BattleController.HandleMove 移动完成后调用（null 安全，纯通知入口）</summary>
    public void OnMoveCompleted(PieceModel piece)
    {
        if (!IsClimateActive(MonsoonConfig.ClimateEffectKind.Heat)) return;
        if (piece == null || piece.IsDead) return;
        if (piece.HasAttackedThisTurn) return;   // 已锁定（本回合攻击过），无需重复置位
        piece.HasAttackedThisTurn = true;
        piece.View?.SetActed(true);              // 已行动变灰（与攻击后口径一致，纯视觉）
        Debug.Log($"[MonsoonManager] 高温：{piece.Data.displayName} 移动后本回合锁定（不能再移动/攻击）");
    }

    // ==========================================
    //  飓风·幸运方块（三期B）：商店关闭 + 棋盘掉方块 + 移动经过拾取开出内容
    //  时间单位铁律：掉落决策按「轮」（轮号守卫）；掉落执行在 P1/P2 回合开始各一批（同品质同数量）
    // ==========================================
    /// <summary>飓风激活期间商店是否被关闭（内聚过滤：非季风城邦/无飓风恒 false）。
    /// UI_ShopPanel.Toggle 调用（B 键拦截），不判断城邦</summary>
    public bool IsShopClosedByHurricane
        => IsClimateActive(MonsoonConfig.ClimateEffectKind.Hurricane);

    /// <summary>回合开始时的方块掉落执行（在 OnTurnStarted 内、气候判定之后调用）：
    /// 轮号守卫先做「本轮掉不掉 + 掉哪个档位」的一次性决策（平衡保底：决策结果整轮生效），
    /// 然后本回合（P1 或 P2）掉一批「该档位 × 掉落数量」的方块到随机空格。
    /// 一轮内 P1/P2 两批品质、数量完全一致；不掉则两回合都不掉</summary>
    private void DropLuckyBoxesForTurn(int currentRound)
    {
        if (!IsClimateActive(MonsoonConfig.ClimateEffectKind.Hurricane)) return;
        var tiers = Config.lootBoxTiers;
        if (tiers == null || tiers.Count == 0) return;

        // 一次性决策（轮号守卫）：本轮掉不掉 → 掉哪个档位（决策结果整轮生效，两批复用）
        if (_lastDropDecisionRound != currentRound)
        {
            _lastDropDecisionRound = currentRound;
            if (RollProbability(Config.hurricaneDropChancePerRound))
            {
                _roundDropTier = RollTierIndex(tiers);
                Debug.Log($"[MonsoonManager] 飓风：第 {currentRound} 轮掉落幸运方块（{tiers[_roundDropTier].tierName}档 × {Mathf.Max(1, Config.hurricaneDropCount)} 个/批，P1/P2 回合各一批）");
            }
            else
            {
                _roundDropTier = -1;   // 本轮不掉（两回合都不掉——平衡保底）
            }
        }

        if (_roundDropTier < 0) return;   // 本轮不掉 / 决策已消费后飓风中途结束的情况由清空逻辑兜底

        int count = Mathf.Max(1, Config.hurricaneDropCount);
        int dropped = 0;
        for (int i = 0; i < count; i++)
        {
            var coord = RollRandomEmptyCoord();
            if (coord == null) break;   // 棋盘没有空格了（全被占/全有方块）——后续也不再尝试
            _luckyBoxes.Add(new LuckyBox { coord = coord.Value, tierIndex = _roundDropTier });
            dropped++;
        }
        if (dropped > 0)
        {
            ShowBoxOverlay(coord2 => true);   // 全量刷新方块高亮（简单起见整体重绘）
            Debug.Log($"[MonsoonManager] 飓风：掉落 {dropped} 个幸运方块（{tiers[_roundDropTier].tierName}档）");
        }
    }

    /// <summary>按权重随机一个档位下标（权重全 0 时兜底 0）</summary>
    private int RollTierIndex(List<MonsoonConfig.LootBoxTier> tiers)
    {
        float total = 0f;
        foreach (var t in tiers) if (t != null) total += Mathf.Max(0f, t.weight);
        if (total <= 0f) return 0;
        float roll = Random.value * total;
        for (int i = 0; i < tiers.Count; i++)
        {
            if (tiers[i] == null) continue;
            roll -= Mathf.Max(0f, tiers[i].weight);
            if (roll <= 0f) return i;
        }
        return 0;
    }

    /// <summary>随机一个空格（无棋子且无方块的格子；棋盘无空格返回 null）</summary>
    private HexCoord? RollRandomEmptyCoord()
    {
        var board = ChessBoardController.Instance;
        if (board == null) return null;
        var candidates = new List<HexCoord>();
        foreach (var coord in board.AllCoords)
        {
            if (PieceLayoutModel.Instance != null && PieceLayoutModel.Instance.GetPieceAt(coord) != null) continue;
            if (GetBoxAt(coord) != null) continue;   // 已有方块
            candidates.Add(coord);
        }
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>查询某格上的方块（无 = null）</summary>
    private LuckyBox GetBoxAt(HexCoord coord)
    {
        foreach (var box in _luckyBoxes)
            if (box.coord == coord) return box;
        return null;
    }

    /// <summary>全量刷新方块战术层高亮（方块消失/新增后调用；_luckyBoxes 为空时即全部还原）</summary>
    private void ShowBoxOverlay(System.Func<HexCoord, bool> filter = null)
    {
        var board = ChessBoardController.Instance;
        if (board == null) return;
        var tiers = Config.lootBoxTiers;

        // 先清所有现有方块高亮（重绘语义）
        foreach (var box in _luckyBoxes)
        {
            var tile = board.GetTile(box.coord);
            tile?.overlay?.Hide();
        }

        if (filter != null)
        {
            foreach (var box in _luckyBoxes)
            {
                if (!filter(box.coord)) continue;
                var tile = board.GetTile(box.coord);
                var tier = tiers != null && box.tierIndex >= 0 && box.tierIndex < tiers.Count ? tiers[box.tierIndex] : null;
                if (tile != null && tier != null)
                    tile.overlay?.Show(tier.color, tier.color.a);
            }
        }
    }

    /// <summary>移动经过拾取 + 残留伤害（PieceManager.MovePieceAlongPath 路径遍历点调用，含起终点；落点不必是该格）：
    /// ① 路径经过/到达残留格 → 受当前强度真实伤害（三期C）；
    /// ② 路径经过方块格 → 拾取并按档位开出内容（三期B）。
    /// 内聚过滤：非季风城邦/无飓风时无方块、无雷暴时无残留，本方法即无操作（null 安全，纯通知入口）</summary>
    public void OnPieceMovedAlongPath(PieceModel piece, List<HexCoord> path)
    {
        if (path == null || path.Count == 0) return;
        if (_luckyBoxes.Count == 0 && _residues.Count == 0) return;   // 无实体（含非季风城邦）直接返回
        if (piece == null || piece.IsDead) return;

        // 先结算残留伤害（棋子可能途中死亡 → 后续不再拾取）
        ApplyResiduePathDamage(piece, path);
        if (piece.IsDead) return;

        // 再拾取方块（快照遍历：拾取过程中 _luckyBoxes 会被修改）
        if (_luckyBoxes.Count > 0)
        {
            foreach (var coord in new List<HexCoord>(path))
            {
                var box = GetBoxAt(coord);
                if (box == null) continue;
                PickupBox(piece, box);
            }
        }
    }

    /// <summary>拾取一个方块并开出内容：先在档位的「非空内容来源」里随机一种（城邦装备/普通装备/道具/金币），
    /// 再在来源里随机具体对象。装备走 GrantEquipment、道具走 GrantItem、金币走 AddGold（全部既有入口，
    /// 只调用不改）。开出失败（来源为空/背包满不拦截——Grant 是无偿入口无容量限制）不产生副作用</summary>
    private void PickupBox(PieceModel piece, LuckyBox box)
    {
        var tiers = Config.lootBoxTiers;
        if (tiers == null || box.tierIndex < 0 || box.tierIndex >= tiers.Count || tiers[box.tierIndex] == null) return;
        var tier = tiers[box.tierIndex];
        _luckyBoxes.Remove(box);
        ShowBoxOverlay();   // 移除后重绘（清掉该格高亮）

        string picker = piece.Data != null ? piece.Data.displayName : "棋子";
        Debug.Log($"[MonsoonManager] 飓风：{picker}（{(piece.Owner == PlayerSide.P1 ? "玩家1" : "玩家2")}）拾取幸运方块（{tier.tierName}档）");

        // 收集非空内容来源
        var sources = new List<int>();   // 0=城邦装备 1=普通装备 2=道具 3=金币
        var cityEquipments = FilterCityEquipments(tier);
        if (cityEquipments.Count > 0) sources.Add(0);
        var normalEquipments = FilterNormalEquipments(tier);
        if (normalEquipments.Count > 0) sources.Add(1);
        var items = FilterItems(tier);
        if (items.Count > 0) sources.Add(2);
        if (tier.goldMax > 0 && tier.goldMin <= tier.goldMax) sources.Add(3);
        if (sources.Count == 0)
        {
            Debug.LogWarning($"[MonsoonManager] 飓风：{tier.tierName}档无可用内容来源（装备/道具池全空且金币范围无效），方块空开");
            return;
        }

        int source = sources[Random.Range(0, sources.Count)];
        switch (source)
        {
            case 0:
                var eq = cityEquipments[Random.Range(0, cityEquipments.Count)];
                EquipmentManager.Instance?.GrantEquipment(piece.Owner, eq);
                Debug.Log($"[MonsoonManager] 飓风：开出城邦装备「{eq.displayName}」→ 入装备背包");
                break;
            case 1:
                var neq = normalEquipments[Random.Range(0, normalEquipments.Count)];
                EquipmentManager.Instance?.GrantEquipment(piece.Owner, neq);
                Debug.Log($"[MonsoonManager] 飓风：开出装备「{neq.displayName}」→ 入装备背包");
                break;
            case 2:
                var item = items[Random.Range(0, items.Count)];
                GridItemManager.Instance?.GrantItem(piece.Owner, item);
                Debug.Log($"[MonsoonManager] 飓风：开出道具「{item.displayName}」→ 入道具背包");
                break;
            case 3:
                int gold = Random.Range(Mathf.Max(0, tier.goldMin), Mathf.Max(0, tier.goldMax) + 1);
                GoldManager.Instance?.AddGold(piece.Owner, gold);
                Debug.Log($"[MonsoonManager] 飓风：开出金币 {gold}");
                break;
        }
    }

    /// <summary>城邦装备来源：tier=CityState 且 price ∈ [minCityPrice, maxCityPrice]</summary>
    private List<EquipmentData> FilterCityEquipments(MonsoonConfig.LootBoxTier tier)
    {
        var result = new List<EquipmentData>();
        var shop = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetShopItems() : null;
        if (shop == null) return result;
        foreach (var eq in shop)
        {
            if (eq == null || eq.tier != EquipmentTier.CityState) continue;
            if (eq.price >= tier.minCityPrice && eq.price <= tier.maxCityPrice) result.Add(eq);
        }
        return result;
    }

    /// <summary>普通装备来源：includeNormalEquipment=true 时按指定 tier（Basic/Intermediate/Advanced）筛；
    /// false = 该档位无普通装备（如低档纯金币），直接返回空</summary>
    private List<EquipmentData> FilterNormalEquipments(MonsoonConfig.LootBoxTier tier)
    {
        var result = new List<EquipmentData>();
        if (!tier.includeNormalEquipment) return result;   // 开关关 = 无普通装备来源
        var shop = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetShopItems() : null;
        if (shop == null) return result;
        foreach (var eq in shop)
        {
            if (eq == null || eq.tier == EquipmentTier.CityState) continue;   // 城邦装备走城邦来源
            if (eq.tier == tier.equipmentTier) result.Add(eq);
        }
        return result;
    }

    /// <summary>道具来源：price ∈ [minCityPrice, maxCityPrice]</summary>
    private List<GridItemData> FilterItems(MonsoonConfig.LootBoxTier tier)
    {
        var result = new List<GridItemData>();
        var items = GridItemManager.Instance != null ? GridItemManager.Instance.GetShopGridItems() : null;
        if (items == null) return result;
        foreach (var item in items)
        {
            if (item == null) continue;
            if (item.price >= tier.minCityPrice && item.price <= tier.maxCityPrice) result.Add(item);
        }
        return result;
    }

    // ---- 雷电残留（三期C；复用三期B 格子实体模式：坐标→实体→渲染→生命周期）----
    /// <summary>棋盘上的雷电残留（运行时实体）：坐标 + 当前强度。独立实体，按自身强度衰减，
    /// 不受雷暴本身持续轮数影响；双方棋子经过/停留均受当前强度真实伤害</summary>
    private class LightningResidue
    {
        public HexCoord coord;
        public int power;   // 当前强度（位于/经过该格受 power 真实伤害；每轮 -衰减量，归零消失）
    }
    private readonly List<LightningResidue> _residues = new List<LightningResidue>();
    private int _lastResidueTickRound = -1;   // 残留衰减/停留伤害的轮号守卫（每轮一次）

    /// <summary>查询某格上的残留（无 = null）</summary>
    private LightningResidue GetResidueAt(HexCoord coord)
    {
        foreach (var r in _residues)
            if (r.coord == coord) return r;
        return null;
    }

    /// <summary>雷暴出现时的雷劈（RollNewClimates 内雷暴条目命中时调用，一次性）：
    /// 随机 2~4 个格子 → 每格留雷电残留（初始强度）+ 劈中棋子扣真实伤害（环境伤害口径，
    /// 无视防御/护盾不附着元素）+ 劈中且存活者概率获得永久加成（属性池随机一条，可叠加）。
    /// 随机入口与其他气候判定同源（联机换 seed 只改 RollProbability/Random）</summary>
    private void StrikeLightning(int currentRound)
    {
        int min = Mathf.Max(1, Config.lightningStrikeMinCount);
        int max = Mathf.Max(min, Config.lightningStrikeMaxCount);
        int count = Random.Range(min, max + 1);

        var struckCoords = new List<HexCoord>();
        var gameConfig = Resources.Load<GameConfig>("GameConfig");   // 残留颜色可配（与项目其他 GameConfig 消费方同口径）
        var residueColor = gameConfig != null ? gameConfig.overlayLightningResidueColor
            : new Color(0.75f, 0.4f, 1f, 0.55f);   // GameConfig 缺失兜底（与默认字段一致）
        var board = ChessBoardController.Instance;

        for (int i = 0; i < count; i++)
        {
            var coord = RollRandomCoord();   // 任意格子（可站棋子/可空格，无排除条件）
            if (coord == null) break;
            if (struckCoords.Contains(coord.Value)) continue;   // 同一格不重复劈
            struckCoords.Add(coord.Value);

            // 留下雷电残留（已有残留的格子：取较大强度——重劈覆盖性增强，不叠加重复实体）
            var existing = GetResidueAt(coord.Value);
            int initialPower = Mathf.Max(0, Config.lightningResidueInitialPower);
            if (existing != null)
            {
                existing.power = Mathf.Max(existing.power, initialPower);
            }
            else
            {
                _residues.Add(new LightningResidue { coord = coord.Value, power = initialPower });
            }
            var tile = board != null ? board.GetTile(coord.Value) : null;
            tile?.overlay?.Show(residueColor, residueColor.a);

            // 劈中棋子：扣真实伤害（环境伤害：source=null / Dot / True / 无元素——不计地下交易统计）
            var piece = PieceLayoutModel.Instance != null ? PieceLayoutModel.Instance.GetPieceAt(coord.Value) : null;
            if (piece == null || piece.IsDead) continue;
            PieceManager.Instance?.ApplyIncomingDamage(piece, Mathf.Max(0, Config.lightningStrikeDamage), null, DamageSource.Dot, DamageKind.True, ElementType.None);
            Debug.Log($"[MonsoonManager] 雷暴：{coord.Value} 雷劈命中 {piece.Data.displayName}，真实伤害 {Config.lightningStrikeDamage}");
            if (piece.IsDead) continue;   // 劈死了不给加成

            // 劈中且存活：概率获得永久加成（属性池随机一条）
            if (RollProbability(Config.lightningPermanentBonusChance))
            {
                var pool = Config.permanentBonusPool;
                if (pool != null && pool.Count > 0)
                {
                    var entry = pool[Random.Range(0, pool.Count)];
                    if (entry != null && entry.value != 0)
                    {
                        piece.AddPermanentBonus(entry.type, entry.value);
                        string statName = entry.type == MonsoonConfig.PermanentBonusType.HP ? "生命上限"
                            : entry.type == MonsoonConfig.PermanentBonusType.Attack ? "攻击"
                            : entry.type == MonsoonConfig.PermanentBonusType.Defense ? "防御"
                            : entry.type == MonsoonConfig.PermanentBonusType.Move ? "移动" : "射程";
                        Debug.Log($"[MonsoonManager] 雷暴：{piece.Data.displayName} 获得永久加成（{statName} +{entry.value}，对局内永久可叠加）");
                    }
                }
            }
        }
        if (struckCoords.Count > 0)
            Debug.Log($"[MonsoonManager] 雷暴：第 {currentRound} 轮出现时雷劈 {struckCoords.Count} 格，各留雷电残留（初始强度 {Config.lightningResidueInitialPower}，每轮 -{Config.lightningResidueDecayPerRound}）");
    }

    /// <summary>随机一个格子（无排除条件——雷劈可劈任意格，含棋子格与空格；棋盘无效返回 null）</summary>
    private HexCoord? RollRandomCoord()
    {
        var board = ChessBoardController.Instance;
        if (board == null || board.AllCoords == null || board.AllCoords.Count == 0) return null;
        var coords = board.AllCoords;
        return coords[Random.Range(0, coords.Count)];
    }

    /// <summary>残留按「轮」结算（OnRoundEnded 内调用）：① 停在残留格的棋子受当前强度真实伤害；
    /// ② 强度 -衰减量，归零移除（高亮还原）。轮号守卫每轮一次。
    /// 伤害顺序 = 先结算停留伤害（按当前强度）再衰减——「停留 8→6→4→2」递减口径</summary>
    private void TickResidues(int currentRound)
    {
        if (_residues.Count == 0) return;
        if (_lastResidueTickRound == currentRound) return;   // 本轮已结算
        _lastResidueTickRound = currentRound;

        var board = ChessBoardController.Instance;
        int decay = Mathf.Max(0, Config.lightningResidueDecayPerRound);

        // ① 停留伤害：按结算前当前强度
        var damaged = new List<PieceModel>();
        foreach (var residue in _residues)
        {
            var piece = PieceLayoutModel.Instance != null ? PieceLayoutModel.Instance.GetPieceAt(residue.coord) : null;
            if (piece == null || piece.IsDead) continue;
            PieceManager.Instance?.ApplyIncomingDamage(piece, residue.power, null, DamageSource.Dot, DamageKind.True, ElementType.None);
            damaged.Add(piece);
            Debug.Log($"[MonsoonManager] 雷电残留：{piece.Data.displayName} 停留受 {residue.power} 真实伤害（{residue.coord}）");
        }
        foreach (var piece in damaged)
        {
            if (piece.IsDead)
            {
                Debug.Log($"[MonsoonManager] {piece.Data.displayName} 因雷电残留伤害死亡");
                PieceManager.Instance?.DestroyPiece(piece);
            }
        }

        // ② 衰减：-decay，归零移除（高亮还原）
        for (int i = _residues.Count - 1; i >= 0; i--)
        {
            _residues[i].power -= decay;
            if (_residues[i].power <= 0)
            {
                var tile = board != null ? board.GetTile(_residues[i].coord) : null;
                tile?.overlay?.Hide();
                _residues.RemoveAt(i);
            }
        }
    }

    /// <summary>移动经过/到达残留格 → 受当前强度真实伤害（OnPieceMovedAlongPath 内与方块拾取并行结算）。
    /// 内聚过滤：无残留时零开销直接返回</summary>
    private void ApplyResiduePathDamage(PieceModel piece, List<HexCoord> path)
    {
        if (_residues.Count == 0) return;
        foreach (var coord in path)
        {
            var residue = GetResidueAt(coord);
            if (residue == null) continue;
            if (piece == null || piece.IsDead) return;   // 途中死亡即停止结算
            PieceManager.Instance?.ApplyIncomingDamage(piece, residue.power, null, DamageSource.Dot, DamageKind.True, ElementType.None);
            Debug.Log($"[MonsoonManager] 雷电残留：{piece.Data.displayName} 经过 {coord} 受 {residue.power} 真实伤害");
            if (piece.IsDead)
            {
                Debug.Log($"[MonsoonManager] {piece.Data.displayName} 因雷电残留伤害死亡");
                PieceManager.Instance?.DestroyPiece(piece);
                return;
            }
        }
    }

    private void ClearAllBoxes(string reason)
    {
        if (_luckyBoxes.Count == 0) return;
        int count = _luckyBoxes.Count;
        _luckyBoxes.Clear();
        ShowBoxOverlay();   // 清空后重绘 = 全部还原
        _roundDropTier = int.MinValue;   // 决策状态复位（下次飓风重新决策）
        Debug.Log($"[MonsoonManager] 飓风结束：清除 {count} 个未拾取幸运方块（{reason}）");
    }
}
