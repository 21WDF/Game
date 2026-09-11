using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 元素格子管理器（统一棋盘格子实体基座）—— 对标 MonsoonManager/WarCityManager 的单例管理器。
/// 挂载在场景中的 ElementTileManager GameObject 上（可与其他城邦 Manager 放一起）。
///
/// 基座职责（两类格子实体共享）：
///   1. 逐格实体存储：棋盘上同一格最多一份格子实体（元素格 / 雷电残留互斥，后者者覆盖前者，材质先还原再替换）
///   2. 视觉：格子本体材质替换 / 还原（复用 HexTile.SetTileMaterial / RestoreTileMaterial 幂等能力；
///      材质未配置时无视觉但数据层照常生效——警告一次，不刷屏）
///   3. 双时机结算：元素格按「回合」（TurnManager 回合结束 → OnTurnEnded）；
///      雷电残留按「轮」（MonsoonManager.OnRoundEnded → TickResidues 主动驱动基座实体）
///      ——两种时机各自独立，不折算（红线：不得改变雷暴手感）
///   4. 移动经过通知：PieceManager.MovePieceAlongPath 统一入口（与雷暴经过伤害同一条链路，互不干扰）
///
/// 元素格行为（全城邦通用）：
///   - 停留结算（回合结束，格上所有棋子不分敌我）：元素附着（复用 PieceManager.ApplyElementInteraction
///     既有链路——含护盾染色封印守卫与染色入口；非元素城邦下附着被既有城邦过滤天然跳过）+
///     持续伤害（环境真实伤害：不带元素、不触发反应、不附着；火/雷默认 2 点可配）
///   - 水/冰减益：站在格上实时生效（射程/移动聚合点查询），离开或格子消失立即恢复（查询式，无状态残留）
///   - 经过附着：路径上每格附着一次（只附着——不结算伤害不给减益）；触发超载/冻结反应时打断移动
///   - 到期/清除：材质还原，实体移除
///
/// 雷电残留（季风·雷暴变体）：实体（坐标 + 强度）与材质由本基座承载，
/// 生成 / 停留伤害 / 衰减 / 永久加成 / 日志等全部逻辑保留在 MonsoonManager（纯结构迁移，行为零变化）。
/// </summary>
public class ElementTileManager : MonoBehaviour
{
    public static ElementTileManager Instance { get; private set; }

    [Header("配置资产（空则读 Resources/ElementTileConfig，再空则用运行时默认值）")]
    [Tooltip("Create > Chess > Element Tile Config 创建后拖入；改数值无需改代码")]
    public ElementTileConfig config;

    private ElementTileConfig _runtimeConfig;
    private bool _materialMissingWarned;   // 材质缺失警告只打一次（照雷暴残留口径，不刷屏）

    /// <summary>生效配置（懒加载：Inspector → Resources → 运行时默认实例）</summary>
    private ElementTileConfig Config
    {
        get
        {
            if (_runtimeConfig == null)
            {
                _runtimeConfig = config != null ? config
                    : (Resources.Load<ElementTileConfig>("ElementTileConfig") ?? ScriptableObject.CreateInstance<ElementTileConfig>());
            }
            return _runtimeConfig;
        }
    }

    // ==========================================
    //  格子实体（统一存储：同一格最多一份；元素格与雷电残留互斥）
    // ==========================================
    /// <summary>格子实体：元素格（element + 剩余回合）或雷电残留（strength；结算逻辑在 MonsoonManager）</summary>
    public class TileEntity
    {
        public HexCoord coord;
        public bool isLightning;          // true = 雷电残留变体（季风雷暴）
        // ---- 元素格字段 ----
        public ElementType element;
        public int turnsRemaining;
        // ---- 雷电残留字段 ----
        public int strength;              // 当前强度（伤害值；由 MonsoonManager 衰减）
    }

    private readonly Dictionary<HexCoord, TileEntity> _tiles = new Dictionary<HexCoord, TileEntity>();

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
        // 泄漏兜底：管理器销毁时还原全部格子本体材质（格子可能已随场景销毁 → GetTile null 安全跳过）
        RestoreAllTileMaterials();
    }

    /// <summary>当前元素格数量（雷电残留不计；调试/日志用）</summary>
    public int ElementTileCount
    {
        get
        {
            int n = 0;
            foreach (var t in _tiles.Values) if (!t.isLightning) n++;
            return n;
        }
    }

    /// <summary>当前雷电残留数量（季风雷暴；调试/零开销快路径守卫用）</summary>
    public int LightningResidueCount
    {
        get
        {
            int n = 0;
            foreach (var t in _tiles.Values) if (t.isLightning) n++;
            return n;
        }
    }

    // ==========================================
    //  元素格：生成 / 覆盖（大招统一入口调用）
    // ==========================================
    /// <summary>把一组格子替换为指定元素格（大招释放统一入口；全城邦通用，无城邦过滤）。
    /// 覆盖规则：同元素 → 刷新持续回合；异元素 / 雷电残留 → 覆盖为新元素并重置时长（先还原旧材质再替换）。
    /// durationTurns ≤ 0 时回退配置默认值。材质未配置 → 无视觉，数据层照常（警告一次）</summary>
    public void ApplyElementTiles(IEnumerable<HexCoord> coords, ElementType element, int durationTurns)
    {
        if (coords == null || element == ElementType.None) return;
        var board = ChessBoardController.Instance;
        var model = board != null ? board.Model : null;
        if (model == null) return;

        var entry = Config.GetEntry(element);
        if (entry == null) return;
        if (entry.material == null && !_materialMissingWarned)
        {
            _materialMissingWarned = true;
            Debug.LogWarning($"[ElementTileManager] ElementTileConfig 未配置 {element} 元素格材质，该元素格子无材质显示（数据层照常生效，仅无显示）");
        }

        int duration = durationTurns > 0 ? durationTurns : Mathf.Max(1, Config.defaultDurationTurns);
        int applied = 0;
        foreach (var coord in coords)
        {
            if (!model.Contains(coord)) continue;
            _tiles.TryGetValue(coord, out var existing);
            if (existing != null && !existing.isLightning && existing.element == element)
            {
                existing.turnsRemaining = duration;   // 同元素：刷新持续回合
            }
            else
            {
                if (existing != null) RestoreTileMaterial(coord);   // 异元素/残留：先还原旧材质再替换
                _tiles[coord] = new TileEntity { coord = coord, element = element, turnsRemaining = duration };
                SetTileMaterial(coord, entry.material);
            }
            applied++;
        }
        if (applied > 0)
            Debug.Log($"[ElementTileManager] 元素格：{applied} 格替换为 {element} 元素格（持续 {duration} 回合）");
    }

    // ==========================================
    //  元素格：回合结算（TurnManager 回合结束调用；任一方回合结束都结算）
    // ==========================================
    /// <summary>回合结束结算：① 停留效果（格上所有棋子不分敌我：元素附着 + 持续伤害）；
    /// ② 时长递减，到期移除并还原材质。
    /// 附着走 PieceManager.ApplyElementInteraction 既有链路（含染色封印守卫；非元素城邦下天然不附着）；
    /// 持续伤害为环境真实伤害（source=null / Dot / True / 无元素——不触发元素反应、不附着、不计地下交易统计）</summary>
    public void OnTurnEnded()
    {
        if (_tiles.Count == 0) return;
        var layout = PieceLayoutModel.Instance;

        var damaged = new List<PieceModel>();
        // ① 停留结算（快照遍历：结算中实体可能被覆盖/移除）
        foreach (var tile in new List<TileEntity>(_tiles.Values))
        {
            if (tile.isLightning) continue;   // 雷电残留按「轮」由 MonsoonManager 结算
            var piece = layout != null ? layout.GetPieceAt(tile.coord) : null;
            if (piece == null || piece.IsDead) continue;

            // 附着（复用既有链路；非元素城邦下 GetReaction 返回无反应、附着写入被既有城邦过滤跳过）
            var reaction = ElementReactionTable.GetReaction(piece.AffixedElement, tile.element);
            PieceManager.Instance?.ApplyElementInteraction(piece, null, tile.element,
                Mathf.Max(1, Config.defaultAttachGauge), reaction);

            // 持续伤害（环境真实伤害；水/冰默认 0）
            var entry = Config.GetEntry(tile.element);
            int dot = entry != null ? Mathf.Max(0, entry.damagePerTurn) : 0;
            if (dot > 0)
            {
                PieceManager.Instance?.ApplyIncomingDamage(piece, dot, null, DamageSource.Dot, DamageKind.True, ElementType.None);
                Debug.Log($"[ElementTileManager] 元素格：{piece.Data?.displayName} 停留 {tile.coord}（{tile.element}）受 {dot} 环境伤害");
                damaged.Add(piece);
            }
        }
        foreach (var piece in damaged)
        {
            if (piece.IsDead)
            {
                Debug.Log($"[ElementTileManager] {piece.Data?.displayName} 因元素格持续伤害死亡");
                PieceManager.Instance?.DestroyPiece(piece);
            }
        }

        // ② 时长递减：到期移除 + 材质还原
        var expired = new List<HexCoord>();
        foreach (var tile in _tiles.Values)
        {
            if (tile.isLightning) continue;
            tile.turnsRemaining--;
            if (tile.turnsRemaining <= 0) expired.Add(tile.coord);
        }
        foreach (var coord in expired)
        {
            if (!_tiles.TryGetValue(coord, out var tile) || tile.isLightning) continue;
            RestoreTileMaterial(coord);
            _tiles.Remove(coord);
        }
        if (expired.Count > 0)
            Debug.Log($"[ElementTileManager] 元素格：{expired.Count} 格到期移除（材质已还原）");
    }

    // ==========================================
    //  元素格：移动经过附着（MovePieceAlongPath 内与雷暴经过伤害同一条链路）
    // ==========================================
    /// <summary>移动经过元素格 → 只结算附着（+元素量、走完整反应流程、仅元素城邦生效）；
    /// 不结算持续伤害、不给减益（减益是「站在格子上」的实时效果，经过不算停留）。
    /// 路径每格只附着一次（本方法由移动管道对整条路径调用一次）。
    /// 返回打断坐标：某次附着触发超载/冻结反应时返回该格——调用方应把移动截断到该格
    ///（棋子停在触发的格子上，不继续前往原定终点；其余反应不打断）。
    /// 非元素城邦下 GetReaction 恒返回无反应 → 永不打断（附着也被既有过滤跳过）</summary>
    public HexCoord? NotifyPiecePathPassed(PieceModel piece, List<HexCoord> path)
    {
        if (piece == null || piece.IsDead || path == null || _tiles.Count == 0) return null;
        if (!ElementReactionTable.ElementSystemActive) return null;   // 仅元素城邦附着（非元素城邦不附着、不打断、无日志）
        int gauge = Mathf.Max(1, Config.defaultAttachGauge);

        foreach (var coord in path)
        {
            if (!_tiles.TryGetValue(coord, out var tile) || tile.isLightning) continue;
            var reaction = ElementReactionTable.GetReaction(piece.AffixedElement, tile.element);
            PieceManager.Instance?.ApplyElementInteraction(piece, null, tile.element, gauge, reaction);
            Debug.Log($"[ElementTileManager] 元素格：{piece.Data?.displayName} 经过 {coord}（{tile.element}）附着 +{gauge}");
            if (reaction.Type == ReactionType.Overload || reaction.Type == ReactionType.Frozen)
            {
                Debug.Log($"[ElementTileManager] 元素格：经过附着触发{reaction.Type}，{piece.Data?.displayName} 移动被打断（停在 {coord}）");
                return coord;   // 打断：棋子停在触发的格子
            }
        }
        return null;
    }

    // ==========================================
    //  元素格：水/冰减益查询（聚合点实时读取；无状态、离开即恢复）
    // ==========================================
    /// <summary>棋子站在水元素格时的攻击距离减益值（非水格/无格返回 0）。
    /// 消费方：PieceModel.AttackRange 聚合点（保底 1）</summary>
    public int GetAttackRangeDebuff(PieceModel piece)
    {
        if (piece == null) return 0;
        if (!_tiles.TryGetValue(piece.Coord, out var tile) || tile.isLightning) return 0;
        var entry = Config.GetEntry(tile.element);
        return entry != null ? Mathf.Max(0, entry.attackRangeDebuff) : 0;
    }

    /// <summary>棋子站在冰元素格时的移动距离减益值（非冰格/无格返回 0）。
    /// 消费方：PieceModel.MoveRange 聚合点（保底 1）</summary>
    public int GetMoveRangeDebuff(PieceModel piece)
    {
        if (piece == null) return 0;
        if (!_tiles.TryGetValue(piece.Coord, out var tile) || tile.isLightning) return 0;
        var entry = Config.GetEntry(tile.element);
        return entry != null ? Mathf.Max(0, entry.moveRangeDebuff) : 0;
    }

    // ==========================================
    //  雷电残留变体（季风·雷暴）：实体 + 材质由基座承载，结算逻辑在 MonsoonManager
    // ==========================================
    /// <summary>全部雷电残留快照（MonsoonManager.TickResidues 遍历用；含坐标与强度）</summary>
    public List<TileEntity> GetLightningResidues() 
    {
        var list = new List<TileEntity>();
        foreach (var tile in _tiles.Values)
            if (tile.isLightning) list.Add(tile);
        return list;
    }

    /// <summary>查询某格雷电残留（无 = null）</summary>
    public TileEntity GetLightningResidueAt(HexCoord coord)
        => _tiles.TryGetValue(coord, out var tile) && tile.isLightning ? tile : null;

    /// <summary>设置雷电残留（雷劈入口）：已有残留 → 取较大强度（重劈覆盖性增强，不叠加实体）；
    /// 无（或该格是元素格）→ 新建实体并替换材质。强度衰减 / 移除 / 停留伤害由 MonsoonManager 驱动。
    /// material 为空时无视觉（调用方 MonsoonManager 负责缺失警告）</summary>
    public void SetLightningResidue(HexCoord coord, int strength, Material material)
    {
        _tiles.TryGetValue(coord, out var existing);
        if (existing != null && existing.isLightning)
        {
            existing.strength = Mathf.Max(existing.strength, strength);   // 重劈：取较大强度
            return;
        }
        if (existing != null) RestoreTileMaterial(coord);   // 覆盖元素格：先还原旧材质
        _tiles[coord] = new TileEntity { coord = coord, isLightning = true, strength = strength };
        SetTileMaterial(coord, material);
    }

    /// <summary>移除一格雷电残留并还原材质（强度归零时由 MonsoonManager 调用；幂等）</summary>
    public void RemoveLightningResidue(HexCoord coord)
    {
        if (!_tiles.TryGetValue(coord, out var tile) || !tile.isLightning) return;
        RestoreTileMaterial(coord);
        _tiles.Remove(coord);
    }

    /// <summary>还原全部雷电残留格材质（MonsoonManager 销毁 / 对局重置兜底用）</summary>
    public void RestoreAllLightningMaterials()
    {
        var coords = new List<HexCoord>();
        foreach (var tile in _tiles.Values)
            if (tile.isLightning) coords.Add(tile.coord);
        foreach (var coord in coords)
        {
            RestoreTileMaterial(coord);
            _tiles.Remove(coord);
        }
    }

    /// <summary>清除全部元素格并还原材质（对局重置 / 泄漏兜底）</summary>
    public void ClearAllElementTiles()
    {
        var coords = new List<HexCoord>();
        foreach (var tile in _tiles.Values)
            if (!tile.isLightning) coords.Add(tile.coord);
        foreach (var coord in coords)
        {
            RestoreTileMaterial(coord);
            _tiles.Remove(coord);
        }
    }

    // ==========================================
    //  材质替换 / 还原（复用 HexTile 既有幂等能力；tile 缺失安全跳过）
    // ==========================================
    private static void SetTileMaterial(HexCoord coord, Material material)
    {
        if (material == null) return;   // 无材质：数据层照常，仅无视觉
        ChessBoardController.Instance?.GetTile(coord)?.SetTileMaterial(material);
    }

    private static void RestoreTileMaterial(HexCoord coord)
        => ChessBoardController.Instance?.GetTile(coord)?.RestoreTileMaterial();

    /// <summary>还原全部格子实体材质并清空（泄漏兜底：OnDestroy 调用）</summary>
    private void RestoreAllTileMaterials()
    {
        var board = ChessBoardController.Instance;
        if (board == null) return;
        foreach (var tile in _tiles.Values)
            board.GetTile(tile.coord)?.RestoreTileMaterial();
        _tiles.Clear();
    }
}
