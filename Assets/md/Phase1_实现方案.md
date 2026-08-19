# Phase 1 单机可玩原型 - 完整实现规格

## 设计原则（Phase 1 专用）

1. **不做网络层**：所有 Manager 直接 MonoBehavior 不继承 NetworkBehaviour
2. **不做 ScriptableObject**：数据先用常量或简单 class 硬编码，Phase 2 再迁移
3. **不做 UI 框架**：用最简 Text/Button 验证逻辑，Phase 2 换正式 UI
4. **不做元素/装备/大招**：Phase 1 只管走格子 + 攻击 + AP + 金币

---

## 任务 #1：Hex 网格渲染 + 点击检测

### 1.1 HexPrefab 制作

在 Unity Editor 中：

1. 创建一个 3D 六边形模型（或用内置的 Cylinder 设为 6 边 + 压扁）
2. 或者用代码：创建一个 `HexTile.prefab`，包含：
   - `MeshFilter` + `MeshRenderer`（一个程序生成的六边形 mesh）
   - `BoxCollider` 或 `MeshCollider`（点击检测用）
   - `HexTile.cs` 脚本

### 1.2 HexCoord 结构体

```csharp
// Core/HexCoord.cs
[System.Serializable]
public struct HexCoord : IEquatable<HexCoord>
{
    public int q, r, s;

    public HexCoord(int q, int r)
    {
        this.q = q;
        this.r = r;
        this.s = -q - r;
    }

    // 6个方向（平顶Hex）
    public static readonly HexCoord[] Directions = new[]
    {
        new HexCoord( 1,  0), // NE
        new HexCoord( 1, -1), // E
        new HexCoord( 0, -1), // SE
        new HexCoord(-1,  0), // SW
        new HexCoord(-1,  1), // W
        new HexCoord( 0,  1), // NW
    };

    // 距离
    public int Distance(HexCoord other)
    {
        return Mathf.Max(
            Mathf.Abs(q - other.q),
            Mathf.Abs(r - other.r),
            Mathf.Abs(s - other.s)
        );
    }

    // 邻居
    public HexCoord Neighbor(int directionIndex)
    {
        var dir = Directions[directionIndex];
        return new HexCoord(q + dir.q, r + dir.r);
    }

    // 半径R内所有坐标
    public static List<HexCoord> HexesInRadius(int radius)
    {
        var result = new List<HexCoord>();
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                result.Add(new HexCoord(q, r));
            }
        }
        return result;
    }

    // Cube → 世界坐标（平顶Hex，size = 六边形外接圆半径）
    public Vector3 ToWorld(float size)
    {
        float x = size * (3f / 2f * q);
        float z = size * (Mathf.Sqrt(3f) / 2f * q + Mathf.Sqrt(3f) * r);
        return new Vector3(x, 0, z);
    }

    public bool Equals(HexCoord other) => q == other.q && r == other.r && s == other.s;
    public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(q, r, s);
    public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
    public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
}
```

### 1.3 HexTile MonoBehaviour

```csharp
// Entities/HexTile.cs
public class HexTile : MonoBehaviour
{
    public HexCoord Coord { get; private set; }
    public bool IsOccupied => Occupant != null;
    public Unit Occupant { get; set; }

    [Header("Visual")]
    public MeshRenderer tileRenderer;
    public Material defaultMat;
    public Material highlightMat;   // 移动范围高亮
    public Material attackMat;      // 攻击范围高亮
    public Material hoverMat;       // 鼠标悬停

    public void Initialize(HexCoord coord)
    {
        Coord = coord;
        name = $"Hex_{coord.q}_{coord.r}";
        tileRenderer.material = defaultMat;
    }

    public void SetHighlight(HighlightType type)
    {
        tileRenderer.material = type switch
        {
            HighlightType.None => defaultMat,
            HighlightType.Move => highlightMat,
            HighlightType.Attack => attackMat,
            HighlightType.Hover => hoverMat,
            _ => defaultMat
        };
    }
}

public enum HighlightType { None, Move, Attack, Hover }
```

### 1.4 GridManager（棋盘生成 + 管理）

```csharp
// Managers/GridManager.cs
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Config")]
    public GameObject hexTilePrefab;
    public float hexSize = 1f;          // 每个六边形的大小
    public int initialRadius = 4;       // 初始半径

    [Header("Runtime")]
    public Dictionary<HexCoord, HexTile> Tiles = new();

    void Awake() => Instance = this;

    void Start()
    {
        GenerateBoard(initialRadius);
    }

    void GenerateBoard(int radius)
    {
        var coords = HexCoord.HexesInRadius(radius);
        foreach (var coord in coords)
        {
            var worldPos = coord.ToWorld(hexSize);
            var tileObj = Instantiate(hexTilePrefab, worldPos, Quaternion.identity, transform);
            var tile = tileObj.GetComponent<HexTile>();
            tile.Initialize(coord);
            Tiles[coord] = tile;
        }
    }

    public HexTile GetTile(HexCoord coord)
    {
        Tiles.TryGetValue(coord, out var tile);
        return tile;
    }

    public HexTile GetTileAtWorldPosition(Vector3 worldPos)
    {
        // 将世界坐标转换为 Hex 坐标
        // 精确转换比较复杂，Phase 1 用射线检测
        RaycastHit hit;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 100f))
        {
            return hit.collider.GetComponentInParent<HexTile>();
        }
        return null;
    }

    // 获取移动范围内的所有空格子
    public List<HexTile> GetWalkableTilesInRange(HexCoord from, int range)
    {
        var result = new List<HexTile>();
        var visited = new HashSet<HexCoord> { from };
        var frontier = new Queue<(HexCoord, int)>();
        frontier.Enqueue((from, 0));

        while (frontier.Count > 0)
        {
            var (current, dist) = frontier.Dequeue();
            if (dist >= range) continue;

            for (int i = 0; i < 6; i++)
            {
                var neighbor = current.Neighbor(i);
                if (visited.Contains(neighbor)) continue;
                if (!Tiles.TryGetValue(neighbor, out var tile)) continue;
                if (tile.IsOccupied) continue; // 不能穿过棋子

                visited.Add(neighbor);
                result.Add(tile);
                frontier.Enqueue((neighbor, dist + 1));
            }
        }
        return result;
    }
}
```

### 1.5 点击检测（InputHandler）

```csharp
// Client/InputHandler.cs
public class InputHandler : MonoBehaviour
{
    public TileClickEvent OnTileClicked;

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // 左键点击
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var tile = hit.collider.GetComponentInParent<HexTile>();
                if (tile != null)
                {
                    OnTileClicked?.Invoke(tile);
                }
            }
        }
    }
}

public delegate void TileClickEvent(HexTile tile);
```

---

## 任务 #2：寻路 + 移动范围显示

### 2.1 BFS 移动范围（不需要 A*，Phase 1 用 BFS 就够了）

上面 `GridManager.GetWalkableTilesInRange()` 已经是 BFS。A* 用于实际移动路径，BFS 用于显示范围。

```csharp
// 在 GridManager.cs 中追加
public List<HexCoord> FindPath(HexCoord from, HexCoord to)
{
    var cameFrom = new Dictionary<HexCoord, HexCoord>();
    var frontier = new Queue<HexCoord>();
    frontier.Enqueue(from);
    cameFrom[from] = from;

    while (frontier.Count > 0)
    {
        var current = frontier.Dequeue();
        if (current == to) break;

        for (int i = 0; i < 6; i++)
        {
            var next = current.Neighbor(i);
            if (cameFrom.ContainsKey(next)) continue;
            if (!Tiles.TryGetValue(next, out var tile)) continue;
            if (tile.IsOccupied && next != to) continue; // 允许终点被占据（攻击目标）

            cameFrom[next] = current;
            frontier.Enqueue(next);
        }
    }

    // 回溯路径
    if (!cameFrom.ContainsKey(to)) return null;
    
    var path = new List<HexCoord> { to };
    var cur = to;
    while (cur != from)
    {
        cur = cameFrom[cur];
        path.Add(cur);
    }
    path.Reverse();
    return path;
}
```

---

## 任务 #3：单棋子移动 + 攻击

### 3.1 简单的 Unit.cs（Phase 1 极简版）

```csharp
// Entities/Unit.cs (Phase 1 版)
public class Unit : MonoBehaviour
{
    [Header("Data (hardcoded for now)")]
    public string unitName = "TestUnit";
    public int maxHP = 100;
    public int attack = 30;
    public int defense = 10;
    public int moveRange = 3;
    public int attackRange = 2;

    [Header("Runtime State")]
    public int currentHP;
    public HexTile currentTile;
    public PlayerSide owner = PlayerSide.P1;
    public bool hasMovedThisTurn;
    public bool hasAttackedThisTurn;

    void Start()
    {
        currentHP = maxHP;
    }

    public void MoveTo(HexTile targetTile, List<HexCoord> path)
    {
        // Phase 1: 直接瞬移（后续加动画）
        if (currentTile != null)
            currentTile.Occupant = null;
        
        currentTile = targetTile;
        targetTile.Occupant = this;
        transform.position = targetTile.transform.position + Vector3.up * 0.5f;
        hasMovedThisTurn = true;
    }

    public void Attack(Unit target)
    {
        int damage = DamageCalculator.Calculate(attack, target.defense);
        target.TakeDamage(damage, this);
        hasAttackedThisTurn = true;
    }

    public void TakeDamage(int damage, Unit source)
    {
        currentHP -= damage;
        currentHP = Mathf.Max(0, currentHP);
        
        // Phase 1 金币系统：造成伤害/受到伤害获得金币
        GoldManager.Instance.OnDamageDealt(source, damage);
        GoldManager.Instance.OnDamageReceived(this, damage);

        if (currentHP <= 0)
            Die();
    }

    public void Die()
    {
        if (currentTile != null)
            currentTile.Occupant = null;
        Destroy(gameObject);
    }
}
```

### 3.2 攻击范围计算

```csharp
// 在 GridManager.cs 中追加
public List<HexTile> GetTilesInAttackRange(HexCoord from, int range)
{
    var result = new List<HexTile>();
    var coords = HexCoord.HexesInRadius(range);
    foreach (var offset in coords)
    {
        var target = new HexCoord(from.q + offset.q, from.r + offset.r);
        if (Tiles.TryGetValue(target, out var tile))
        {
            result.Add(tile);
        }
    }
    return result;
}
```

### 3.3 BattleController（游戏主控制器）

```csharp
// Managers/BattleController.cs
public class BattleController : MonoBehaviour
{
    public static BattleController Instance { get; private set; }

    public Unit selectedUnit;
    public List<HexTile> highlightedTiles;
    public enum BattleState { Idle, UnitSelected, Moving, Attacking }
    public BattleState state = BattleState.Idle;

    void Awake() => Instance = this;

    void OnEnable()
    {
        InputHandler input = FindObjectOfType<InputHandler>();
        input.OnTileClicked += HandleTileClick;
    }

    void HandleTileClick(HexTile tile)
    {
        switch (state)
        {
            case BattleState.Idle:
                TrySelectUnit(tile);
                break;

            case BattleState.UnitSelected:
                if (tile.IsOccupied && tile.Occupant == selectedUnit)
                {
                    DeselectUnit();
                }
                else if (highlightedTiles.Contains(tile) && !tile.IsOccupied)
                {
                    MoveUnit(tile);
                }
                else if (highlightedTiles.Contains(tile) && tile.IsOccupied 
                    && tile.Occupant.owner != selectedUnit.owner)
                {
                    AttackUnit(tile.Occupant);
                }
                break;
        }
    }

    void TrySelectUnit(HexTile tile)
    {
        if (!tile.IsOccupied) return;
        var unit = tile.Occupant;
        if (unit.owner != TurnManager.Instance.ActivePlayer) return;
        
        // 检查是否已经行动过了
        if (unit.hasMovedThisTurn && unit.hasAttackedThisTurn) return;

        SelectUnit(unit);
    }

    void SelectUnit(Unit unit)
    {
        DeselectUnit();
        selectedUnit = unit;
        state = BattleState.UnitSelected;

        // 显示移动范围
        if (!unit.hasMovedThisTurn)
        {
            var moveTiles = GridManager.Instance.GetWalkableTilesInRange(
                unit.currentTile.Coord, unit.moveRange);
            foreach (var t in moveTiles)
            {
                t.SetHighlight(HighlightType.Move);
                highlightedTiles.Add(t);
            }
        }

        // 显示攻击范围
        if (!unit.hasAttackedThisTurn)
        {
            var attackTiles = GridManager.Instance.GetTilesInAttackRange(
                unit.currentTile.Coord, unit.attackRange);
            foreach (var t in attackTiles)
            {
                if (t.IsOccupied && t.Occupant.owner != unit.owner)
                {
                    t.SetHighlight(HighlightType.Attack);
                    highlightedTiles.Add(t);
                }
            }
        }
    }

    void MoveUnit(HexTile targetTile)
    {
        var path = GridManager.Instance.FindPath(
            selectedUnit.currentTile.Coord, targetTile.Coord);
        if (path == null) return;

        APManager.Instance.ConsumeAP(selectedUnit.owner, 1);
        selectedUnit.MoveTo(targetTile, path);
        
        // 移动后刷新攻击范围高亮
        RefreshHighlights();
    }

    void AttackUnit(Unit target)
    {
        APManager.Instance.ConsumeAP(selectedUnit.owner, 1);
        selectedUnit.Attack(target);
        DeselectUnit();
    }

    void DeselectUnit()
    {
        if (selectedUnit != null)
        {
            foreach (var t in highlightedTiles)
                t.SetHighlight(HighlightType.None);
            highlightedTiles.Clear();
            selectedUnit = null;
            state = BattleState.Idle;
        }
    }
}
```

---

## 任务 #4：伤害计算 + HP 系统

### 4.1 伤害计算器

```csharp
// Systems/DamageCalculator.cs
public static class DamageCalculator
{
    // Phase 1 减法公式：atk - def，保底 1 点伤害
    // 玩家心智模型：30攻击打10防御 = 20伤害，直觉可算
    // 注意：装备属性加成应控制在 +1 ~ +5，否则平衡极易崩
    // Phase 2 会加入元素反应参数
    public static int Calculate(int attack, int defense)
    {
        return Mathf.Max(1, attack - defense);
    }
}
```

### 4.2 简单血条 UI

```csharp
// Client/UI_HealthBar.cs
public class UI_HealthBar : MonoBehaviour
{
    public Unit trackedUnit;
    public Slider healthSlider;
    public TextMeshProUGUI hpText;

    void Update()
    {
        if (trackedUnit == null) return;
        healthSlider.value = (float)trackedUnit.currentHP / trackedUnit.maxHP;
        hpText.text = $"{trackedUnit.currentHP}/{trackedUnit.maxHP}";
    }
}
```

---

## 任务 #5：回合循环 + AP + 基础经济

### 5.1 TurnManager（Phase 1 版）

```csharp
// Managers/TurnManager.cs
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public int CurrentTurn { get; private set; } = 0;
    public PlayerSide ActivePlayer { get; private set; } = PlayerSide.P1;
    public List<Unit> Player1Units = new();
    public List<Unit> Player2Units = new();

    void Awake() => Instance = this;

    void Start() => StartNewTurn();

    void StartNewTurn()
    {
        CurrentTurn++;
        ActivePlayer = (CurrentTurn % 2 == 1) ? PlayerSide.P1 : PlayerSide.P2;
        
        // 结算金币
        if (CurrentTurn > 1)
            GoldManager.Instance.SettleTurnGold();

        // 刷新AP
        APManager.Instance.RefillAP(ActivePlayer);

        // 重置棋子的行动标记
        var units = ActivePlayer == PlayerSide.P1 ? Player1Units : Player2Units;
        foreach (var unit in units)
        {
            unit.hasMovedThisTurn = false;
            unit.hasAttackedThisTurn = false;
        }

        Debug.Log($"Turn {CurrentTurn} �� Player {(ActivePlayer == PlayerSide.P1 ? "1" : "2")}'s turn");
    }

    public void EndTurn()
    {
        BattleController.Instance.DeselectUnit();
        
        // 检查胜负
        if (Player2Units.Count == 0) { GameOver(PlayerSide.P1); return; }
        if (Player1Units.Count == 0) { GameOver(PlayerSide.P2); return; }

        StartNewTurn();
    }

    void GameOver(PlayerSide winner)
    {
        Debug.Log($"Game Over! Winner: {winner}");
        // Phase 1: 简单日志，Phase 2 加UI
    }
}
```

### 5.2 APManager

```csharp
// Managers/APManager.cs
public class APManager : MonoBehaviour
{
    public static APManager Instance { get; private set; }

    // Phase 1 硬编码
    public int baseAPPerTurn = 2;
    public int maxAP = 4;
    
    Dictionary<PlayerSide, int> _currentAP = new();

    void Awake() => Instance = this;

    public int GetAP(PlayerSide side) => _currentAP.GetValueOrDefault(side, 0);

    public void RefillAP(PlayerSide side)
    {
        _currentAP[side] = Mathf.Min(baseAPPerTurn, maxAP);
    }

    public bool HasAP(PlayerSide side, int amount = 1)
    {
        return _currentAP.GetValueOrDefault(side, 0) >= amount;
    }

    public bool ConsumeAP(PlayerSide side, int amount = 1)
    {
        if (!HasAP(side, amount)) return false;
        _currentAP[side] -= amount;
        return true;
    }
}
```

### 5.3 GoldManager（Phase 1 版）

```csharp
// Managers/GoldManager.cs
public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance { get; private set; }

    [Header("Config")]
    public float goldPerDamage = 1f;
    public float goldPerDamageReceived = 0.3f;
    public int startingGold = 50;

    Dictionary<PlayerSide, int> _pendingGold = new(); // 回合内暂存
    Dictionary<PlayerSide, int> _totalGold = new();

    void Awake() => Instance = this;

    void Start()
    {
        _totalGold[PlayerSide.P1] = startingGold;
        _totalGold[PlayerSide.P2] = startingGold;
    }

    public void OnDamageDealt(Unit dealer, int damage)
    {
        int gold = Mathf.RoundToInt(damage * goldPerDamage);
        _pendingGold[dealer.owner] = _pendingGold.GetValueOrDefault(dealer.owner, 0) + gold;
    }

    public void OnDamageReceived(Unit receiver, int damage)
    {
        int gold = Mathf.RoundToInt(damage * goldPerDamageReceived);
        _pendingGold[receiver.owner] = _pendingGold.GetValueOrDefault(receiver.owner, 0) + gold;
    }

    public void SettleTurnGold()
    {
        foreach (var kvp in _pendingGold)
        {
            _totalGold[kvp.Key] = _totalGold.GetValueOrDefault(kvp.Key, 0) + kvp.Value;
        }
        _pendingGold.Clear();
    }

    public bool TrySpendGold(PlayerSide side, int amount)
    {
        if (_totalGold.GetValueOrDefault(side, 0) < amount) return false;
        _totalGold[side] -= amount;
        return true;
    }

    public int GetGold(PlayerSide side) => _totalGold.GetValueOrDefault(side, 0);
}
```

### 5.4 回合结束按钮

```csharp
// Client/UI_EndTurnButton.cs
public class UI_EndTurnButton : MonoBehaviour
{
    public void OnEndTurnClicked()
    {
        TurnManager.Instance.EndTurn();
    }
}
```

---

## Phase 1 项目结构

```
Assets/_Game/
  Core/
    HexCoord.cs
    PlayerSide.cs          # enum PlayerSide { P1, P2 }
    GameEnums.cs           # HighlightType 等枚举
  Managers/
    GridManager.cs
    TurnManager.cs
    APManager.cs
    GoldManager.cs
    BattleController.cs
  Systems/
    DamageCalculator.cs
  Entities/
    HexTile.cs
    Unit.cs
  Client/
    InputHandler.cs
    UI_HealthBar.cs
    UI_EndTurnButton.cs
    UI_GoldDisplay.cs      # 显示当前金币
    UI_APDisplay.cs         # 显示当前AP
```

共 15 个脚本文件。

---

## Phase 1 测试 checkpoints

每完成一个任务，用这些标准验收：

### T1 验收：Hex 网格

- [ ] 启动游戏能看到 61 个六边形排列成六边形
- [ ] 点击任意六边形，控制台输出该格子的 HexCoord
- [ ] 鼠标悬停时格子变色

### T2 验收：寻路

- [ ] 选中一个棋子，它移动范围内的格子高亮
- [ ] 高亮格子数量 = 正确的 Hex 距离范围内的空格子数
- [ ] 棋子无法穿过被另一个棋子占据的格子

### T3 验收：移动+攻击

- [ ] 点击高亮空格 → 棋子移动过去
- [ ] 点击高亮敌方棋子 → 棋子对敌方造成伤害
- [ ] 移动消耗1AP，攻击消耗1AP
- [ ] 同一个棋子可以先移动再攻击（消耗2AP）

### T4 验收：伤害+HP

- [ ] 攻击后目标HP正确减少（验证伤害公式）
- [ ] HP归零后棋子消失
- [ ] 棋子消失后其所在格子恢复可用

### T5 验收：回合+AP+经济

- [ ] 点结束回合 → AP清零 → 轮到对手 → 对手AP补满
- [ ] 造成伤害后金币增加（回合结束时结算）
- [ ] 受到伤害也获得少量金币
- [ ] 初始金币=50
- [ ] 一局可以打到一方棋子全灭
