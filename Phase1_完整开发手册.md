# Phase 1 完整开发手册 — 单机可玩原型

> **适用版本**：Unity 6 (6000.x LTS)  
> **阶段目标**：实现一个本地双人热座 Hex 战棋原型（走格子 + 攻击 + AP回合 + 金币）。Phase 1 不碰网络、不碰 ScriptableObject、不碰元素/装备/大招。

---

## 目录

- [1. Phase 1 目标与预期成果](#1-phase-1-目标与预期成果)
- [2. 前置准备](#2-前置准备)
- [3. 项目结构搭建（步骤 1）](#3-项目结构搭建步骤-1)
- [4. 核心数据类型（步骤 2）](#4-核心数据类型步骤-2)
- [5. HexTile 预制体（步骤 3）](#5-hextile-预制体步骤-3)
- [6. GridManager 棋盘生成（步骤 4）](#6-gridmanager-棋盘生成步骤-4)
- [7. Unit 棋子预制体（步骤 5）](#7-unit-棋子预制体步骤-5)
- [8. InputHandler 输入管理（步骤 6）](#8-inputhandler-输入管理步骤-6)
- [9. BattleController 战斗控制（步骤 7）](#9-battlecontroller-战斗控制步骤-7)
- [10. DamageCalculator 伤害计算（步骤 8）](#10-damagecalculator-伤害计算步骤-8)
- [11. TurnManager 回合管理（步骤 9）](#11-turnmanager-回合管理步骤-9)
- [12. APManager 行动点管理（步骤 10）](#12-apmanager-行动点管理步骤-10)
- [13. GoldManager 金币管理（步骤 11）](#13-goldmanager-金币管理步骤-11)
- [14. UI 显示层（步骤 12）](#14-ui-显示层步骤-12)
- [15. 场景搭建与连线（步骤 13）](#15-场景搭建与连线步骤-13)
- [16. 脚本依赖关系图](#16-脚本依赖关系图)
- [17. 已创建 Hex 模型集成说明](#17-已创建-hex-模型集成说明)
- [18. 完整验证清单](#18-完整验证清单)
- [19. 常见错误速查](#19-常见错误速查)

---

## 1. Phase 1 目标与预期成果

### 1.1 可运行的核心循环

启动 Unity → 点到 Play 模式后，你应该能看到：

```
61 个平顶六边形棋盘（半径 4）
├── 双方各 1 个棋子（一个红色一个蓝色）
├── 点击己方棋子 → 显示移动范围（蓝色高亮） + 攻击范围（红色高亮）
├── 点击高亮空格 → 棋子移动过去（消耗 1 AP）
├── 点击高亮敌方棋子 → 攻击造成伤害（消耗 1 AP）
├── AP 不足或点击"结束回合" → 轮到对手
└── 一方棋子死亡 → 控制台输出 "Game Over"
```

### 1.2 预期成果清单

| 成果    | 验收标准                          |
| ----- | ----------------------------- |
| 棋盘可见  | 61 个六边形平铺成六边形，能点击             |
| 棋子可见  | 2 个 3D 模型（方块/胶囊均可），颜色不同       |
| 移动可行  | 点击高亮空格 → 棋子瞬移过去 → 高亮消失        |
| 攻击可行  | 点击高亮敌方 → HP 减少 → Console 输出伤害 |
| 伤害正确  | 30 攻打 10 防 = 20 伤害            |
| AP 正确 | 初始 2 点，移动用 1，攻击用 1，回合结束补满     |
| 金币正确  | 造成 20 伤害 = 20 金币，回合结束时结算      |
| 回合切换  | 点"结束回合"→ AP 补满 → 轮到对手         |

### 1.3 Phase 1 刻意不做的事

- 不创建 ScriptableObject（所有数据硬编码在脚本里）
- 不继承 NetworkBehaviour（无网络层）
- 不做 Animator / DOTween 动画
- 不做元素反应、装备、大招、棋盘道具
- 不做棋子选择 / 部署阶段（直接在场景里放 2 个棋子）

---

## 2. 前置准备

### 2.1 Unity 版本与设置

| 项目       | 设置值                               |
| -------- | --------------------------------- |
| Unity 版本 | **6000.x LTS**（Unity 6）           |
| 模板       | **3D (Built-in Render Pipeline)** |
| 输入系统     | **新 Input System**（Package）       |
| .NET     | **Standard 2.1**（默认）              |

**验证**：创建项目后，确认 Scene 视图中有 Main Camera 和 Directional Light。

### 2.2 你已经准备好的资源

- 一个 **平顶六边形（Flat-top Hex）3D 模型**（.fbx 或直接在 Unity 里的 Mesh）

### 2.3 需要创建的新资源

Phase 1 共创建 **15 个 C# 脚本** + **2 个 Prefab** + **1 个 Canvas**。

---

## 3. 项目结构搭建（步骤 1）

### 3.1 创建文件夹

在 Project 窗口的 Assets 下创建以下文件夹：

```
Assets/
  _Game/
    Core/
    Managers/
    Systems/
    Entities/
    Client/
  Prefabs/
  Materials/
```

**操作**：Project 窗口右键 → Create → Folder，依次创建。

**验证**：Project 窗口中出现 \_Game 文件夹，内部有 5 个子文件夹。

### 3.2 设置摄像机

1. 选中 Main Camera
2. Inspector 中设置：
   - **Position**：`(0, 15, -10)`
   - **Rotation**：`(55, 0, 0)`
   - **Projection**：Perspective
   - **Field of View**：`60`

**验证**：Scene 视图从斜上方俯视原点位置，能看到地面（如果创建了 Plane）。

---

## 4. 核心数据类型（步骤 2）

创建以下脚本，保存路径在注释中。

### 4.1 `Assets/_Game/Core/HexCoord.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 六边形立方坐标（Cube Coordinate）
/// 不变式：q + r + s = 0
/// 本游戏使用平顶 Hex（Flat-top），平边朝上下
/// </summary>
[System.Serializable]
public struct HexCoord
{
    public int q; // 列
    public int r; // 行
    public int s; // = -q -r

    public HexCoord(int q, int r)
    {
        this.q = q;
        this.r = r;
        this.s = -q - r;
    }

    // ---- 6 方向邻居（平顶 Hex）----
    // 方向顺序：NE, E, SE, SW, W, NW
    internal static readonly HexCoord[] Directions = {
        new HexCoord( 1,  0),   // NE: 右上
        new HexCoord( 1, -1),   // E:  右
        new HexCoord( 0, -1),   // SE: 右下
        new HexCoord(-1,  0),   // SW: 左下
        new HexCoord(-1,  1),   // W:  左
        new HexCoord( 0,  1),   // NW: 左上
    };

    // ---- 距离（曼哈顿距离的 Hex 等价）----
    public int Distance(HexCoord other)
    {
        return Mathf.Max(
            Mathf.Abs(q - other.q),
            Mathf.Abs(r - other.r),
            Mathf.Abs(s - other.s)
        );
    }

    // ---- 获取某个方向的邻居 ----
    public HexCoord Neighbor(int directionIndex)
    {
        HexCoord dir = Directions[directionIndex];
        return new HexCoord(q + dir.q, r + dir.r);
    }

    // ---- 半径 R 内的所有 Hex 坐标 ----
    public static List<HexCoord> AllCoordsInRadius(int radius)
    {
        var result = new List<HexCoord>();
        for (int qv = -radius; qv <= radius; qv++)
        {
            int rMin = Mathf.Max(-radius, -qv - radius);
            int rMax = Mathf.Min(radius, -qv + radius);
            for (int rv = rMin; rv <= rMax; rv++)
            {
                result.Add(new HexCoord(qv, rv));
            }
        }
        return result;
    }

    // ---- Cube → 世界坐标（平顶 Hex）----
    public Vector3 ToWorld(float hexSize)
    {
        float x = hexSize * (3f / 2f * q);
        float z = hexSize * (Mathf.Sqrt(3f) / 2f * q + Mathf.Sqrt(3f) * r);
        return new Vector3(x, 0f, z);
    }

    // ---- 相等性 ----
    public override bool Equals(object obj) =>
        obj is HexCoord other && q == other.q && r == other.r && s == other.s;

    public override int GetHashCode() => (q, r, s).GetHashCode();

    public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
    public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);

    public override string ToString() => $"({q}, {r}, {s})";
}
```

### 4.2 `Assets/_Game/Core/GameEnums.cs`

```csharp
/// <summary>
/// 玩家阵营
/// </summary>
public enum PlayerSide
{
    P1 = 0,
    P2 = 1
}

/// <summary>
/// 格子高亮类型
/// </summary>
public enum HighlightType
{
    None,       // 默认颜色
    Move,       // 可移动范围（蓝色）
    Attack,     // 可攻击范围（红色）
    Hover       // 鼠标悬停（浅色）
}
```

**验证**：两个脚本保存后，Console 窗口无红色报错，编译通过。

---

## 5. HexTile 预制体（步骤 3）

### 5.1 创建 HexTile.cs 脚本

**保存路径**：`Assets/_Game/Entities/HexTile.cs`

```csharp
using UnityEngine;

/// <summary>
/// 棋盘上单个六边形格子
/// 挂载在 HexTile Prefab 的空根对象上，MeshRenderer 在子对象 Model 上
/// </summary>
public class HexTile : MonoBehaviour
{
    // ---- 核心数据 ----
    public HexCoord Coord { get; private set; }
    public bool IsOccupied => Occupant != null;
    public Unit Occupant { get; set; }

    // ---- 渲染相关 ----
    [Header("材质")]
    public Material defaultMaterial;     // 默认（白色/灰色）
    public Material moveRangeMaterial;   // 移动范围（蓝色）
    public Material attackRangeMaterial; // 攻击范围（红色）
    public Material hoverMaterial;       // 悬停（浅色）

    private MeshRenderer _meshRenderer;

    // ---- 初始化 ----
    private void Awake()
    {
        // MeshRenderer 在子对象 Model 上，用 GetComponentInChildren 查找
        _meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (_meshRenderer == null)
        {
            Debug.LogError($"[HexTile] {name} 的子对象 Model 缺少 MeshRenderer！", this);
            enabled = false;
        }
    }

    public void Initialize(HexCoord coord)
    {
        Coord = coord;
        name = $"Hex_{coord.q}_{coord.r}";
        SetHighlight(HighlightType.None);
    }

    // ---- 高亮控制 ----
    // 模型有 3 个 Material：mats[0]=顶部，mats[1]=底部，mats[2]=两侧
    // 只换顶部材质，保留底部和两侧不变
    public void SetHighlight(HighlightType type)
    {
        if (_meshRenderer == null) return;

        Material targetMat = type switch
        {
            HighlightType.Move   => moveRangeMaterial,
            HighlightType.Attack => attackRangeMaterial,
            HighlightType.Hover  => hoverMaterial,
            _                    => defaultMaterial
        };

        var mats = _meshRenderer.materials;   // 获取当前材质数组
        mats[0] = targetMat;                  // 只换顶部
        _meshRenderer.materials = mats;       // 写回
    }
}
```

### 5.2 创建材质

1. Project 窗口 → `Assets/Materials/` 右键 → Create → Material
2. 创建以下 4 个材质：

| 材质名             | 颜色         | 用途    |
| --------------- | ---------- | ----- |
| Mat_Hex_Default | 白色 #FFFFFF | 普通格子  |
| Mat_Hex_Move    | 蓝色 #4488FF | 可移动范围 |
| Mat_Hex_Attack  | 红色 #FF4444 | 可攻击范围 |
| Mat_Hex_Hover   | 浅黄 #FFFFCC | 鼠标悬停  |

**操作**：创建材质后，在 Inspector 中点击 Albedo 色块，输入对应颜色值。

### 5.3 制作 HexTile Prefab

**Prefab 结构**（最终效果）：

```
HexTile (根，空对象，挂 HexTile 脚本)
  └── Model (子对象，你的 3D 六边形模型，挂 MeshCollider + MeshRenderer)
```

1. **创建空 GameObject**：Hierarchy 右键 → Create Empty，命名为 "HexTile"
2. **添加你的 3D 六边形模型**：
   - 将你的 Hex 3D 模型**拖为 HexTile 的子物体**
   - 该子物体的 Position 设为 `(0, 0, 0)`
   - 命名为 "Model"
3. **给 Model 子对象添加碰撞体**：
   - 选中 Model → **Add Component** → 搜索 `Mesh Collider` → 添加
   - MeshCollider 会自动根据模型的 Mesh 生成精确的碰撞形状，无需手动调整
4. **给 HexTile 根对象添加脚本**：
   - 选中 HexTile 根对象 → **Add Component** → 搜索 `HexTile` → 添加
   - (MeshRenderer 和 MeshCollider 都在子对象上，HexTile 脚本通过 `GetComponentInChildren` 查找)
5. **配置 HexTile 脚本的材质槽**：
   - Default Material → 拖入 `Mat_Hex_Default`
   - Move Range Material → 拖入 `Mat_Hex_Move`
   - Attack Range Material → 拖入 `Mat_Hex_Attack`
   - Hover Material → 拖入 `Mat_Hex_Hover`
6. **拖到 Prefabs 文件夹**：将 Hierarchy 中的 HexTile 拖入 `Assets/Prefabs/`，变为 Prefab
7. **删除 Hierarchy 中的 HexTile**（生成时用代码 Instantiate）

> **注意**：保证你的 3D 六边形模型是平顶（Flat-top），即平边朝上下。尺寸建议外接圆半径 = 1 单位（即 HexTilePrefab 六边形从中心到顶点约 1 单位）。如果你的模型尺寸不同，在步骤 6 的 GridManager 中调整 `hexSize` 参数。

**验证**：Prefabs 文件夹中有 HexTile Prefab，选中后在 Inspector 中看到 HexTile 脚本组件，材质槽都已填满。

---

## 6. GridManager 棋盘生成（步骤 4）

**保存路径**：`Assets/_Game/Managers/GridManager.cs`

```csharp
using UnityEngine;
using UnityEngine.InputSystem;  // 新 Input System 必需
using System.Collections.Generic;

/// <summary>
/// 管理所有 HexTile 的生成、查询、移动范围和路径查找
/// 单例 — 挂载在场景中的 GridManager GameObject 上
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    // ---- 配置参数（Inspector 中调整）----
    [Header("棋盘配置")]
    public GameObject hexTilePrefab;           // HexTile Prefab
    [Tooltip("六边形外接圆半径（单位）")]
    public float hexSize = 1f;
    [Tooltip("初始棋盘半径")]
    public int initialRadius = 4;

    // ---- 运行时数据 ----
    private Dictionary<HexCoord, HexTile> _tiles = new();

    // ---- 单例初始化 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        GenerateBoard(initialRadius);
        Debug.Log($"[GridManager] 棋盘生成完毕，共 {_tiles.Count} 个格子");
    }

    // ==========================================
    //  棋盘生成
    // ==========================================
    private void GenerateBoard(int radius)
    {
        var allCoords = HexCoord.AllCoordsInRadius(radius);
        foreach (var coord in allCoords)
        {
            Vector3 worldPos = coord.ToWorld(hexSize);
            GameObject tileObj = Instantiate(hexTilePrefab, worldPos, Quaternion.identity, transform);
            var tile = tileObj.GetComponent<HexTile>();
            if (tile != null)
            {
                tile.Initialize(coord);
                _tiles[coord] = tile;
            }
            else
            {
                Debug.LogError($"[GridManager] HexTile Prefab 缺少 HexTile 脚本！");
            }
        }
    }

    // ==========================================
    //  查询方法
    // ==========================================
    public HexTile GetTile(HexCoord coord)
    {
        _tiles.TryGetValue(coord, out var tile);
        return tile;
    }

    public HexTile GetTile(int q, int r)
    {
        return GetTile(new HexCoord(q, r));
    }

    /// <summary>
    /// 检测鼠标点击位置是否命中某个 HexTile（新 Input System 版）
    /// </summary>
    public HexTile RaycastTile()
    {
        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            // 如果射线命中了棋子（Unit），直接返回棋子脚下的格子
            var unit = hit.collider.GetComponent<Unit>();
            if (unit == null)
                unit = hit.collider.GetComponentInParent<Unit>();
            if (unit != null)
                return unit.CurrentTile;

            // 否则查找命中对象上的 HexTile
            var tile = hit.collider.GetComponent<HexTile>();
            if (tile == null)
                tile = hit.collider.GetComponentInParent<HexTile>();
            return tile;
        }
        return null;
    }

    // ==========================================
    //  移动范围（BFS — 广度优先）
    // ==========================================
    public List<HexTile> GetWalkableTiles(HexCoord from, int maxSteps)
    {
        var result = new List<HexTile>();
        var visited = new HashSet<HexCoord> { from };
        // 队列元素：(坐标, 已走步数)
        var frontier = new Queue<(HexCoord, int)>();
        frontier.Enqueue((from, 0));

        while (frontier.Count > 0)
        {
            var (current, steps) = frontier.Dequeue();
            if (steps >= maxSteps) continue;

            for (int i = 0; i < 6; i++)
            {
                var neighbor = current.Neighbor(i);

                if (visited.Contains(neighbor)) continue;
                if (!_tiles.TryGetValue(neighbor, out var tile)) continue; // 棋盘边界外
                if (tile.IsOccupied) continue; // 不能走到/穿过有棋子的格子

                visited.Add(neighbor);
                result.Add(tile);
                frontier.Enqueue((neighbor, steps + 1));
            }
        }
        return result;
    }

    // ==========================================
    //  攻击范围（圆形）
    // ==========================================
    public List<HexTile> GetTilesInAttackRange(HexCoord from, int range)
    {
        var result = new List<HexTile>();
        var allOffsets = HexCoord.AllCoordsInRadius(range);
        foreach (var offset in allOffsets)
        {
            var targetCoord = new HexCoord(from.q + offset.q, from.r + offset.r);
            if (_tiles.TryGetValue(targetCoord, out var tile))
                result.Add(tile);
        }
        return result;
    }

    // ==========================================
    //  简单路径查找（BFS）
    // ==========================================
    public List<HexCoord> FindPath(HexCoord from, HexCoord to)
    {
        if (from == to) return new List<HexCoord> { from };

        var cameFrom = new Dictionary<HexCoord, HexCoord>();
        var frontier = new Queue<HexCoord>();
        frontier.Enqueue(from);
        cameFrom[from] = from;

        bool found = false;
        while (frontier.Count > 0 && !found)
        {
            var current = frontier.Dequeue();

            for (int i = 0; i < 6; i++)
            {
                var next = current.Neighbor(i);
                if (cameFrom.ContainsKey(next)) continue;
                if (!_tiles.TryGetValue(next, out var tile)) continue;
                // 终点允许被占据（因为是攻击目标），路径中间不允许
                if (tile.IsOccupied && next != to) continue;

                cameFrom[next] = current;
                frontier.Enqueue(next);

                if (next == to)
                {
                    found = true;
                    break;
                }
            }
        }

        if (!cameFrom.ContainsKey(to)) return null;

        // 回溯路径
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

    // ==========================================
    //  清除所有高亮
    // ==========================================
    public void ClearAllHighlights()
    {
        foreach (var tile in _tiles.Values)
            tile.SetHighlight(HighlightType.None);
    }
}
```

### 6.1 在场景中创建 GridManager GameObject

1. Hierarchy 右键 → Create Empty，命名为 `GridManager`
2. **Add Component** → 搜索 `GridManager` → 添加
3. Inspector 中设置：
   - Hex Tile Prefab → 拖入 `Assets/Prefabs/HexTile`
   - Hex Size → `1`
   - Initial Radius → `4`

**验证**：点击 Play ▶️，Scene 中出现 61 个六边形，Console 输出 `棋盘生成完毕，共 61 个格子`。

**如果看不到格子**：检查 Camera 位置是否正确（步骤 3.2），以及 HexTile Prefab 的尺寸是否合理。

---

## 7. Unit 棋子预制体（步骤 5）

### 7.1 创建 Unit.cs

**保存路径**：`Assets/_Game/Entities/Unit.cs`

```csharp
using UnityEngine;

/// <summary>
/// 棋子运行时状态（Phase 1 硬编码数据）
/// 挂载在 Unit Prefab 上
/// </summary>
public class Unit : MonoBehaviour
{
    // ---- 棋子数据（Phase 1 硬编码）----
    [Header("棋子数据")]
    public string unitName = "TestUnit";
    public int maxHP = 100;
    public int attack = 30;
    public int defense = 10;
    public int moveRange = 3;
    public int attackRange = 2;

    // ---- 运行时状态 ----
    [Header("运行时（只读）")]
    [SerializeField] private int _currentHP;
    public int CurrentHP
    {
        get => _currentHP;
        private set { _currentHP = Mathf.Max(0, value); }
    }

    public HexTile CurrentTile { get; set; }
    public PlayerSide Owner { get; set; } = PlayerSide.P1;

    /// <summary>本回合是否已经移动过</summary>
    public bool HasMovedThisTurn { get; set; }
    /// <summary>本回合是否已经攻击过</summary>
    public bool HasAttackedThisTurn { get; set; }

    // ---- 便捷属性（减法公式用）----
    public int EffectiveAttack => attack;   // Phase 2 加装备后这里会变
    public int EffectiveDefense => defense;

    // ---- 初始化 ----
    private void Start()
    {
        CurrentHP = maxHP;
    }

    // ---- 移动 ----
    public void MoveTo(HexTile targetTile)
    {
        // 离开旧格子
        if (CurrentTile != null)
            CurrentTile.Occupant = null;

        // 进入新格子
        CurrentTile = targetTile;
        targetTile.Occupant = this;

        // 瞬移到位（Phase 1 无动画）
        transform.position = targetTile.transform.position + Vector3.up * 0.5f;

        HasMovedThisTurn = true;
        Debug.Log($"[Unit] {unitName} 移动到 {targetTile.Coord}");
    }

    // ---- 攻击 ----
    public void AttackUnit(Unit target)
    {
        int damage = DamageCalculator.Calculate(EffectiveAttack, target.EffectiveDefense);
        target.TakeDamage(damage, this);
        HasAttackedThisTurn = true;
        Debug.Log($"[Unit] {unitName} 攻击 {target.unitName}，造成 {damage} 点伤害");
    }

    // ---- 受伤 ----
    public void TakeDamage(int damage, Unit source)
    {
        CurrentHP -= damage;

        // 金币系统：造成伤害 / 受到伤害都获得金币
        GoldManager.Instance?.OnDamageDealt(source, damage);
        GoldManager.Instance?.OnDamageReceived(this, damage);

        if (CurrentHP <= 0)
            Die();
    }

    // ---- 死亡 ----
    private void Die()
    {
        if (CurrentTile != null)
            CurrentTile.Occupant = null;

        Debug.Log($"[Unit] {unitName} 死亡！");

        // 从 TurnManager 的列表中移除（如果已注册）
        TurnManager.Instance?.UnregisterUnit(this);

        Destroy(gameObject);
    }
}
```

### 7.2 制作 Unit Prefab

1. Hierarchy 右键 → 3D Object → **Cube**，命名为 "Unit_P1"
2. Inspector 中设置：
   - **Scale**：`(0.8, 0.5, 0.8)` — 压扁一点的方块
3. 给 Cube 换颜色：
   - 创建新材质 `Mat_Unit_P1`，颜色设为红色 #FF4444
   - 拖到 Cube 的 MeshRenderer → Materials[0]
4. **Add Component** → 搜索 `Unit` → 添加
5. 在 Unit 脚本 Inspector 中设置：
   - Unit Name → "战士兵"
   - Max HP → 100
   - Attack → 30
   - Defense → 10
   - Move Range → 3
   - Attack Range → 2
6. 拖到 `Assets/Prefabs/`，命名为 "Unit_P1"
7. 删除 Hierarchy 中的 Unit_P1
8. **重复以上**创建 Unit_P2（蓝色 #4444FF 材质，脚本参数可以一样），也拖到 Prefabs。

**验证**：Prefabs 文件夹中有 Unit_P1 和 Unit_P2，选中后 Inspector 看到 Unit 组件，所有值正确。

---

## 8. InputHandler 输入管理（步骤 6）

**保存路径**：`Assets/_Game/Client/InputHandler.cs`

```csharp
using UnityEngine;
using UnityEngine.InputSystem;  // 新 Input System 必需

/// <summary>
/// 将鼠标左键点击转发为 HexTile 事件
/// 挂载在场景中的 InputHandler GameObject 上
/// </summary>
public class InputHandler : MonoBehaviour
{
    /// <summary>玩家点击了某个六边形格子</summary>
    public System.Action<HexTile> OnTileClicked;

    private void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame) // 新 Input System API
        {
            var tile = GridManager.Instance?.RaycastTile();
            if (tile != null)
            {
                OnTileClicked?.Invoke(tile);
                Debug.Log($"[InputHandler] 点击了 {tile.Coord}");
            }
        }
    }
}
```

### 8.1 场景搭建

1. Hierarchy 右键 → Create Empty，命名为 `InputHandler`
2. Add Component → 搜索 `InputHandler` → 添加

**验证**：点击 Play ▶️，鼠标点击任意格子，Console 输出 `点击了 (x, y, z)`。

---

## 9. BattleController 战斗控制（步骤 7）

这是 Phase 1 最复杂的脚本，负责选棋子、显示范围、执行移动和攻击。

**保存路径**：`Assets/_Game/Managers/BattleController.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 战斗主控制器 — 处理棋子选择、移动、攻击的完整状态机
/// 挂载在场景中的 BattleController GameObject 上
/// </summary>
public class BattleController : MonoBehaviour
{
    public static BattleController Instance { get; private set; }

    // ---- 运行时状态 ----
    private Unit _selectedUnit;
    private List<HexTile> _highlightedTiles = new();

    // ---- 输入引用 ----
    private InputHandler _input;

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _input = FindObjectOfType<InputHandler>();
        if (_input == null)
        {
            Debug.LogError("[BattleController] 场景中缺少 InputHandler！");
            enabled = false;
            return;
        }
        _input.OnTileClicked += HandleTileClick;
    }

    private void OnDestroy()
    {
        if (_input != null)
            _input.OnTileClicked -= HandleTileClick;
    }

    // ==========================================
    //  核心点击处理
    // ==========================================
    private void HandleTileClick(HexTile clickedTile)
    {
        // ---- 情况 1：点击的是己方棋子 → 选中 ----
        if (clickedTile.IsOccupied && clickedTile.Occupant.Owner == TurnManager.Instance.ActivePlayer)
        {
            var unit = clickedTile.Occupant;
            // 如果已经移动过且攻击过，不能选
            if (unit.HasMovedThisTurn && unit.HasAttackedThisTurn) return;
            SelectUnit(unit);
            return;
        }

        // ---- 情况 2：正在选中棋子，点击高亮格子 ----
        if (_selectedUnit != null && _highlightedTiles.Contains(clickedTile))
        {
            // 2a: 空格子 → 移动
            if (!clickedTile.IsOccupied)
            {
                HandleMove(clickedTile);
            }
            // 2b: 敌方棋子 → 攻击
            else if (clickedTile.Occupant.Owner != _selectedUnit.Owner)
            {
                HandleAttack(clickedTile.Occupant);
            }
            return;
        }

        // ---- 情况 3：点击空白/己方格子/已选棋子 → 取消选中 ----
        if (_selectedUnit != null)
        {
            DeselectUnit();
        }
    }

    // ==========================================
    //  选中棋子
    // ==========================================
    private void SelectUnit(Unit unit)
    {
        DeselectUnit(); // 先清除旧高亮
        _selectedUnit = unit;
        Debug.Log($"[BattleController] 选中 {unit.unitName}");

        // 显示移动范围
        if (!unit.HasMovedThisTurn)
        {
            var walkableList = GridManager.Instance.GetWalkableTiles(
                unit.CurrentTile.Coord, unit.moveRange);
            foreach (var tile in walkableList)
            {
                tile.SetHighlight(HighlightType.Move);
                _highlightedTiles.Add(tile);
            }
        }

        // 显示攻击范围（圆形攻击范围内所有敌方棋子）
        if (!unit.HasAttackedThisTurn)
        {
            var attackZone = GridManager.Instance.GetTilesInAttackRange(
                unit.CurrentTile.Coord, unit.attackRange);
            foreach (var tile in attackZone)
            {
                if (tile.IsOccupied && tile.Occupant.Owner != unit.Owner)
                {
                    tile.SetHighlight(HighlightType.Attack);
                    _highlightedTiles.Add(tile);
                }
            }
        }
    }

    // ==========================================
    //  移动处理
    // ==========================================
    private void HandleMove(HexTile targetTile)
    {
        var unit = _selectedUnit;
        if (unit.HasMovedThisTurn) return;

        // 检查 AP
        if (!APManager.Instance.HasAP(unit.Owner, 1))
        {
            Debug.Log("[BattleController] AP 不足，无法移动");
            DeselectUnit();
            return;
        }

        var path = GridManager.Instance.FindPath(unit.CurrentTile.Coord, targetTile.Coord);
        if (path == null)
        {
            Debug.LogWarning("[BattleController] 找不到路径！");
            return;
        }

        // 消耗 AP
        APManager.Instance.ConsumeAP(unit.Owner, 1);
        // 执行移动
        unit.MoveTo(targetTile);

        // 移动后可能改变了攻击范围 → 重新刷新高亮
        RefreshHighlights();
    }

    // ==========================================
    //  攻击处理
    // ==========================================
    private void HandleAttack(Unit target)
    {
        var unit = _selectedUnit;
        if (unit.HasAttackedThisTurn) return;

        // 检查 AP
        if (!APManager.Instance.HasAP(unit.Owner, 1))
        {
            Debug.Log("[BattleController] AP 不足，无法攻击");
            DeselectUnit();
            return;
        }

        // 消耗 AP
        APManager.Instance.ConsumeAP(unit.Owner, 1);
        // 执行攻击
        unit.AttackUnit(target);

        DeselectUnit();
    }

    // ==========================================
    //  刷新高亮
    // ==========================================
    private void RefreshHighlights()
    {
        if (_selectedUnit == null) return;
        GridManager.Instance.ClearAllHighlights();
        _highlightedTiles.Clear();

        var unit = _selectedUnit;

        // 只要没攻击过，就可以点击攻击（即使已经移动过）
        if (!unit.HasAttackedThisTurn)
        {
            var attackZone = GridManager.Instance.GetTilesInAttackRange(
                unit.CurrentTile.Coord, unit.attackRange);
            foreach (var tile in attackZone)
            {
                if (tile.IsOccupied && tile.Occupant.Owner != unit.Owner)
                {
                    tile.SetHighlight(HighlightType.Attack);
                    _highlightedTiles.Add(tile);
                }
            }
        }

        // 如果移动+攻击都完成了，自动取消选中
        if (unit.HasMovedThisTurn && unit.HasAttackedThisTurn)
        {
            DeselectUnit();
        }
    }

    // ==========================================
    //  取消选中
    // ==========================================
    public void DeselectUnit()
    {
        _selectedUnit = null;
        foreach (var tile in _highlightedTiles)
            tile.SetHighlight(HighlightType.None);
        _highlightedTiles.Clear();
    }
}
```

### 9.1 场景搭建

1. Hierarchy 右键 → Create Empty，命名为 `BattleController`
2. Add Component → 搜索 `BattleController` → 添加

**（不需要配置任何 Inspector 参数，全部自动查找）**

---

## 10. DamageCalculator 伤害计算（步骤 8）

**保存路径**：`Assets/_Game/Systems/DamageCalculator.cs`

```csharp
/// <summary>
/// 伤害计算器（纯静态工具类，不继承 MonoBehaviour）
/// Phase 1：减法公式 damage = max(1, attack - defense)
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// 计算伤害
    /// </summary>
    /// <param name="attack">攻击方攻击力</param>
    /// <param name="defense">防御方防御力</param>
    /// <returns>造成的伤害（最少 1 点）</returns>
    public static int Calculate(int attack, int defense)
    {
        return Mathf.Max(1, attack - defense);
    }
}
```

> **说明**：这个类是 `public static`，**不需要挂载到任何 GameObject**。其他脚本直接 `DamageCalculator.Calculate(x, y)` 调用即可。

**验证**：在任意脚本的 Start() 里写 `Debug.Log(DamageCalculator.Calculate(30, 10));`，运行后 Console 输出 `20`。

---

## 11. TurnManager 回合管理（步骤 9）

**保存路径**：`Assets/_Game/Managers/TurnManager.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 回合流转管理
/// 挂载在场景中的 TurnManager GameObject 上
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    // ---- 运行时状态 ----
    public int CurrentTurn { get; private set; } = 0;
    public PlayerSide ActivePlayer { get; private set; } = PlayerSide.P1;

    /// <summary>双方棋子在场景中通过 RegisterUnit 注册到这里</summary>
    public readonly List<Unit> Player1Units = new();
    public readonly List<Unit> Player2Units = new();

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 场景启动后，等待所有棋子注册完毕再开始
        Invoke(nameof(StartFirstTurn), 0.5f);
    }

    private void StartFirstTurn()
    {
        ActivePlayer = PlayerSide.P1;
        StartNewTurn();
    }

    // ==========================================
    //  回合开始
    // ==========================================
    private void StartNewTurn()
    {
        CurrentTurn++;

        // 上回合金币结算（第一回合没有上回合）
        if (CurrentTurn > 1)
            GoldManager.Instance?.SettleTurnGold();

        // 补满当前玩家的 AP
        APManager.Instance?.RefillAP(ActivePlayer);

        // 重置当前玩家所有棋子的行动标记
        var units = ActivePlayer == PlayerSide.P1 ? Player1Units : Player2Units;
        foreach (var unit in units)
        {
            if (unit != null)
            {
                unit.HasMovedThisTurn = false;
                unit.HasAttackedThisTurn = false;
            }
        }

        // 清除所有高亮
        BattleController.Instance?.DeselectUnit();
        GridManager.Instance?.ClearAllHighlights();

        string playerName = ActivePlayer == PlayerSide.P1 ? "玩家1" : "玩家2";
        Debug.Log($"========== 第 {CurrentTurn} 回合 · {playerName} 的回合 ==========");
    }

    // ==========================================
    //  回合结束（由 UI 按钮调用）
    // ==========================================
    public void EndTurn()
    {
        // 切换玩家
        ActivePlayer = ActivePlayer == PlayerSide.P1 ? PlayerSide.P2 : PlayerSide.P1;

        // 检查游戏是否结束
        CheckGameOver();

        StartNewTurn();
    }

    // ==========================================
    //  棋子注册/注销
    // ==========================================
    public void RegisterUnit(Unit unit)
    {
        var list = unit.Owner == PlayerSide.P1 ? Player1Units : Player2Units;
        if (!list.Contains(unit))
            list.Add(unit);
    }

    public void UnregisterUnit(Unit unit)
    {
        var list = unit.Owner == PlayerSide.P1 ? Player1Units : Player2Units;
        list.Remove(unit);
    }

    // ==========================================
    //  胜负判定
    // ==========================================
    private void CheckGameOver()
    {
        Player1Units.RemoveAll(u => u == null);
        Player2Units.RemoveAll(u => u == null);

        if (Player1Units.Count == 0)
        {
            Debug.Log("========== 游戏结束！玩家2 获胜！==========");
        }
        else if (Player2Units.Count == 0)
        {
            Debug.Log("========== 游戏结束！玩家1 获胜！==========");
        }
    }
}
```

### 11.1 场景搭建

1. Hierarchy 右键 → Create Empty，命名为 `TurnManager`
2. Add Component → 搜索 `TurnManager` → 添加

---

## 12. APManager 行动点管理（步骤 10）

**保存路径**：`Assets/_Game/Managers/APManager.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 行动点管理
/// 挂载在场景中的 APManager GameObject 上
/// </summary>
public class APManager : MonoBehaviour
{
    public static APManager Instance { get; private set; }

    // ---- 配置参数（Phase 1 硬编码）----
    [Header("行动点配置")]
    public int baseAPPerTurn = 2;   // 每回合恢复的 AP
    public int maxAPCap = 4;        // AP 上限

    // ---- 运行时数据 ----
    private Dictionary<PlayerSide, int> _currentAP = new();

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _currentAP[PlayerSide.P1] = 0;
        _currentAP[PlayerSide.P2] = 0;
    }

    // ---- 公共方法 ----
    public int GetAP(PlayerSide side) => _currentAP.GetValueOrDefault(side, 0);

    public void RefillAP(PlayerSide side)
    {
        _currentAP[side] = baseAPPerTurn; // 恢复到基础值，不叠加
        Debug.Log($"[APManager] {(side == PlayerSide.P1 ? "P1" : "P2")} AP = {_currentAP[side]}");
    }

    public bool HasAP(PlayerSide side, int amount)
    {
        return _currentAP.GetValueOrDefault(side, 0) >= amount;
    }

    public bool ConsumeAP(PlayerSide side, int amount)
    {
        if (!HasAP(side, amount)) return false;
        _currentAP[side] -= amount;
        Debug.Log($"[APManager] {(side == PlayerSide.P1 ? "P1" : "P2")} 消耗 {amount} AP，剩余 {_currentAP[side]}");
        return true;
    }
}
```

### 12.1 场景搭建

1. Hierarchy 右键 → Create Empty，命名为 `APManager`
2. Add Component → 搜索 `APManager` → 添加
3. Inspector 中检查：Base AP Per Turn = `2`，Max AP Cap = `4`

---

## 13. GoldManager 金币管理（步骤 11）

**保存路径**：`Assets/_Game/Managers/GoldManager.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 金币管理 — 伤害收入在回合结束时结算
/// 挂载在场景中的 GoldManager GameObject 上
/// </summary>
public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance { get; private set; }

    // ---- 配置参数 ----
    [Header("金币配置")]
    public float goldPerDamage = 1f;           // 每造成1点伤害获得的金币
    public float goldPerDamageReceived = 0.3f;  // 每受到1点伤害获得的金币
    public int startingGold = 50;               // 初始金币

    // ---- 运行时数据 ----
    private Dictionary<PlayerSide, int> _pendingGold = new(); // 回合内暂存
    private Dictionary<PlayerSide, int> _totalGold = new();   // 总金币

    // ---- 单例 ----
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _totalGold[PlayerSide.P1] = startingGold;
        _totalGold[PlayerSide.P2] = startingGold;
    }

    // ---- 收入记录（由 Unit.TakeDamage 调用）----
    public void OnDamageDealt(Unit dealer, int damage)
    {
        if (dealer == null) return;
        int gold = Mathf.RoundToInt(damage * goldPerDamage);
        if (!_pendingGold.ContainsKey(dealer.Owner)) _pendingGold[dealer.Owner] = 0;
        _pendingGold[dealer.Owner] += gold;
    }

    public void OnDamageReceived(Unit receiver, int damage)
    {
        if (receiver == null) return;
        int gold = Mathf.RoundToInt(damage * goldPerDamageReceived);
        if (!_pendingGold.ContainsKey(receiver.Owner)) _pendingGold[receiver.Owner] = 0;
        _pendingGold[receiver.Owner] += gold;
    }

    // ---- 回合结算（由 TurnManager.StartNewTurn 调用）----
    public void SettleTurnGold()
    {
        foreach (var kvp in _pendingGold)
        {
            if (!_totalGold.ContainsKey(kvp.Key)) _totalGold[kvp.Key] = 0;
            _totalGold[kvp.Key] += kvp.Value;
            Debug.Log($"[GoldManager] {(kvp.Key == PlayerSide.P1 ? "P1" : "P2")} 结算 +{kvp.Value} 金币，总计 {_totalGold[kvp.Key]}");
        }
        _pendingGold.Clear();
    }

    // ---- 获取金币 ----
    public int GetGold(PlayerSide side) => _totalGold.GetValueOrDefault(side, 0);

    // ---- 消费金币 ----
    public bool TrySpendGold(PlayerSide side, int amount)
    {
        if (!_totalGold.ContainsKey(side) || _totalGold[side] < amount) return false;
        _totalGold[side] -= amount;
        return true;
    }
}
```

### 13.1 场景搭建

1. Hierarchy 右键 → Create Empty，命名为 `GoldManager`
2. Add Component → 搜索 `GoldManager` → 添加
3. Inspector 中检查默认值：Gold Per Damage = `1`，Gold Per Damage Received = `0.3`，Starting Gold = `50`

---

## 14. UI 显示层（步骤 12）

### 14.1 创建 Canvas

1. Hierarchy 右键 → UI → **Canvas**（会自动创建 Canvas + EventSystem）
2. 选中 Canvas，设置：
   - **Render Mode**：Screen Space - Overlay
   - **Canvas Scaler** → UI Scale Mode：Scale With Screen Size → Reference Resolution：`1920 x 1080`

### 14.2 创建 AP 显示文本

**保存路径**：`Assets/_Game/Client/UI_APDisplay.cs`

```csharp
using UnityEngine;
using TMPro;

/// <summary>
/// 在屏幕上显示当前玩家的剩余 AP
/// 挂载在 Canvas 下的 UI GameObject 上
/// </summary>
public class UI_APDisplay : MonoBehaviour
{
    public TextMeshProUGUI apText;  // TMP Text 组件引用

    private void Update()
    {
        if (TurnManager.Instance == null || APManager.Instance == null) return;
        var side = TurnManager.Instance.ActivePlayer;
        int ap = APManager.Instance.GetAP(side);
        apText.text = $"AP: {ap}";
    }
}
```

**搭建**：

1. Canvas 下右键 → UI → **Text - TextMeshPro**（如果没有 TMP，先用 Legacy Text）
2. 命名为 "AP_Text"
3. 位置放在屏幕左上角（Anchor：左上）
4. 字体大小：28
5. 给 AP_Text 添加 UI_APDisplay 脚本
6. 将 AP_Text 拖入脚本的 Ap Text 槽

### 14.3 创建金币显示

**保存路径**：`Assets/_Game/Client/UI_GoldDisplay.cs`

```csharp
using UnityEngine;
using TMPro;

public class UI_GoldDisplay : MonoBehaviour
{
    public TextMeshProUGUI goldText;

    private void Update()
    {
        if (TurnManager.Instance == null || GoldManager.Instance == null) return;
        var side = TurnManager.Instance.ActivePlayer;
        int gold = GoldManager.Instance.GetGold(side);
        goldText.text = $"金币: {gold}";
    }
}
```

搭建同上（AP_Text 旁边，命名 "Gold_Text"）。

### 14.4 创建回合信息显示

**保存路径**：`Assets/_Game/Client/UI_TurnDisplay.cs`

```csharp
using UnityEngine;
using TMPro;

public class UI_TurnDisplay : MonoBehaviour
{
    public TextMeshProUGUI turnText;

    private void Update()
    {
        if (TurnManager.Instance == null) return;
        string playerName = TurnManager.Instance.ActivePlayer == PlayerSide.P1 ? "玩家1" : "玩家2";
        turnText.text = $"{playerName} 的回合 (第 {TurnManager.Instance.CurrentTurn} 回合)";
    }
}
```

搭建：位置放屏幕顶部居中。

### 14.5 创建结束回合按钮

**保存路径**：`Assets/_Game/Client/UI_EndTurnButton.cs`

```csharp
using UnityEngine;

public class UI_EndTurnButton : MonoBehaviour
{
    public void OnEndTurnClicked()
    {
        TurnManager.Instance?.EndTurn();
    }
}
```

**搭建**：

1. Canvas 下右键 → UI → **Button - TextMeshPro**
2. 按钮文字改为 "结束回合"
3. 按钮放在屏幕右下角
4. 给 Button 添加 UI_EndTurnButton 脚本
5. Button 的 OnClick 事件：拖入 Button 自身 → 选择 `UI_EndTurnButton.OnEndTurnClicked`

### 14.6 创建血条（World Space）

**保存路径**：`Assets/_Game/Client/UI_HealthBar.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 挂在棋子头顶的血条（World Space Canvas 的子物体）
/// </summary>
public class UI_HealthBar : MonoBehaviour
{
    public Slider healthSlider;
    public TextMeshProUGUI hpText;
    public Unit trackedUnit;

    private void Update()
    {
        if (trackedUnit == null) return;
        healthSlider.maxValue = trackedUnit.maxHP;
        healthSlider.value = trackedUnit.CurrentHP;
        hpText.text = $"{trackedUnit.CurrentHP} / {trackedUnit.maxHP}";
    }
}
```

**搭建**（可选，Phase 1 可跳过）：

1. 在 Unit_P1 Prefab 下创建子物体 Canvas（Render Mode = World Space）
2. 添加 Slider + Text，挂载 UI_HealthBar
3. 将 trackedUnit 指向父级的 Unit 组件

---

## 15. 场景搭建与连线（步骤 13）

### 15.1 最终场景 Hierarchy 结构

```
Scene
├── Main Camera (Position: 0, 15, -10, Rotation: 55, 0, 0)
├── Directional Light
├── GridManager (GridManager.cs)
├── TurnManager (TurnManager.cs)
├── APManager (APManager.cs)
├── GoldManager (GoldManager.cs)
├── BattleController (BattleController.cs)
├── InputHandler (InputHandler.cs)
├── Canvas (Screen Space - Overlay)
│   ├── AP_Text (UI_APDisplay)
│   ├── Gold_Text (UI_GoldDisplay)
│   ├── Turn_Text (UI_TurnDisplay)
│   └── EndTurn_Button (UI_EndTurnButton)
└── EventSystem (Canvas 自动创建)
```

### 15.2 放置棋子

1. 将 `Unit_P1` Prefab 拖入 Hierarchy（不放在任何 Manager 下）
2. 设置 Position 为 `(0, 0.5, 0)` — 棋盘中心
3. 将 `Unit_P2` Prefab 拖入 Hierarchy
4. 设置 Position 为 `(5, 0.5, 3)` — 棋盘右侧

### 15.3 在 Unit.cs 的 Start() 中添加自动注册

打开 Unit.cs，在末尾加一个方法供场景启动时调用。但更简单的方式是：在 GridManager 生成棋盘后手动放置棋子。

**替换方案**（推荐）：创建一个 `GameSetup.cs` 来放置初始棋子。

**保存路径**：`Assets/_Game/Managers/GameSetup.cs`

```csharp
using UnityEngine;

/// <summary>
/// 游戏初始化 — 在棋盘生成后将两个棋子放到初始位置
/// 挂载在场景中的 GameSetup GameObject 上
/// </summary>
public class GameSetup : MonoBehaviour
{
    public GameObject unitPrefabP1;
    public GameObject unitPrefabP2;

    /// <summary>P1 棋子在棋盘上的初始坐标</summary>
    public Vector2Int p1StartCoord = new Vector2Int(-3, 0);
    /// <summary>P2 棋子在棋盘上的初始坐标</summary>
    public Vector2Int p2StartCoord = new Vector2Int(3, 0);

    private void Start()
    {
        Invoke(nameof(SpawnUnits), 0.2f); // 等 GridManager 先生成棋盘
    }

    private void SpawnUnits()
    {
        var grid = GridManager.Instance;

        // 放置 P1
        var tile1 = grid.GetTile(p1StartCoord.x, p1StartCoord.y);
        if (tile1 != null && unitPrefabP1 != null)
        {
            var unit1 = Instantiate(unitPrefabP1, tile1.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            var unitComp1 = unit1.GetComponent<Unit>();
            unitComp1.Owner = PlayerSide.P1;
            unitComp1.CurrentTile = tile1;
            tile1.Occupant = unitComp1;
            TurnManager.Instance.RegisterUnit(unitComp1);
            Debug.Log($"[GameSetup] P1 棋子放置在 {tile1.Coord}");
        }

        // 放置 P2
        var tile2 = grid.GetTile(p2StartCoord.x, p2StartCoord.y);
        if (tile2 != null && unitPrefabP2 != null)
        {
            var unit2 = Instantiate(unitPrefabP2, tile2.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            var unitComp2 = unit2.GetComponent<Unit>();
            unitComp2.Owner = PlayerSide.P2;
            unitComp2.CurrentTile = tile2;
            tile2.Occupant = unitComp2;
            TurnManager.Instance.RegisterUnit(unitComp2);
            Debug.Log($"[GameSetup] P2 棋子放置在 {tile2.Coord}");
        }
    }
}
```

### 15.4 GameSetup 搭建

1. Hierarchy 右键 → Create Empty，命名为 `GameSetup`
2. Add Component → 搜索 `GameSetup` → 添加
3. Inspector 中：
   - Unit Prefab P1 → 拖入 `Unit_P1`
   - Unit Prefab P2 → 拖入 `Unit_P2`
   - P1 Start Coord → `(-3, 0)`
   - P2 Start Coord → `(3, 0)`

---

## 16. 脚本依赖关系图

```
调用关系（箭头 = "使用/调用"）

GameSetup ──→ GridManager (GetTile)
         ──→ TurnManager (RegisterUnit)
         ──→ Unit (设置 Owner, CurrentTile)

InputHandler ──→ GridManager (RaycastTile)

BattleController ──→ InputHandler (监听 OnTileClicked)
                ──→ GridManager (GetWalkableTiles, GetTilesInAttackRange, FindPath, ClearAllHighlights)
                ──→ APManager (HasAP, ConsumeAP)
                ──→ Unit (MoveTo, AttackUnit)
                ──→ TurnManager (ActivePlayer)

Unit ──→ DamageCalculator (Calculate)
     ──→ GoldManager (OnDamageDealt, OnDamageReceived)
     ──→ TurnManager (UnregisterUnit)

TurnManager ──→ APManager (RefillAP)
           ──→ GoldManager (SettleTurnGold)
           ──→ BattleController (DeselectUnit)
           ──→ GridManager (ClearAllHighlights)

UI_APDisplay ──→ TurnManager, APManager
UI_GoldDisplay ──→ TurnManager, GoldManager
UI_TurnDisplay ──→ TurnManager
UI_EndTurnButton ──→ TurnManager (EndTurn)

=== 无依赖的独立模块 ===
HexCoord（纯数据结构）
GameEnums（纯枚举）
DamageCalculator（纯静态工具）
```

### 执行顺序（Unity 生命周期）

```
场景启动
  ├─ Awake: 所有单例初始化 (GridManager, TurnManager, APManager, GoldManager, BattleController)
  ├─ Start: GridManager.GenerateBoard() → 61个HexTile出现
  ├─ Start: GameSetup.SpawnUnits() (Invoke 延迟 0.2s) → 两个棋子放在棋盘上
  └─ Start: TurnManager.StartFirstTurn() → 第1回合开始
         ├─ APManager.RefillAP(P1)
         └─ 玩家操作
               ├─ InputHandler 检测点击
               ├─ BattleController 处理点击
               │     ├─ SelectUnit → 显示范围高亮
               │     ├─ HandleMove → Unit.MoveTo
               │     └─ HandleAttack → Unit.AttackUnit → DamageCalculator.Calculate
               └─ UI_EndTurnButton → TurnManager.EndTurn()
                     ├─ 切换 ActivePlayer
                     ├─ GoldManager.SettleTurnGold()
                     ├─ APManager.RefillAP()
                     └─ 重置行动标记
```

---

## 17. 已创建 Hex 模型集成说明

你已经创建了 3D 六边形模型。请确认以下事项：

### 17.1 模型朝向验证

| 你的模型应该       | 检查方法                                           |
| ------------ | ---------------------------------------------- |
| 平顶（Flat-top） | 从顶视图看，六边形有两条边是水平的，尖角朝左右                        |
| Y 轴朝上        | 模型的"厚度"方向是 Y 轴                                 |
| 尺寸接近 1 单位    | 从中心到顶点的距离约 1（如果差很多，在 GridManager 中调 `hexSize`） |

> **如果模型是尖顶（Pointy-top）**：请在 Blender/Maya 中将模型绕 Y 轴旋转 30° 后重新导出。

### 17.2 集成到 HexTile Prefab

1. 将你的 Hex 模型 .fbx 文件拖入 `Assets/Prefabs/` 或者拖入场景中
2. 按 [步骤 5.3](#53-制作-hextile-prefab) 制作 HexTile Prefab 时，**将你的模型拖为 HexTile 的子物体**
3. 子物体名为 "Model"，Position 设为 `(0, 0, 0)`
4. 如果模型太大或太小：
   - 调整子物体的 Scale（推荐），不调 GridManager 的 hexSize
   - 或者调整 GridManager 的 hexSize 参数

### 17.3 材质处理

如果模型自带材质，你可能需要替换：

1. 选中 HexTile Prefab → 找到 Model 子物体 → MeshRenderer
2. Materials → 改成 `Mat_Hex_Default`（或留空，让 HexTile 脚本控制）

**注意**：`HexTile.SetHighlight()` 只替换 `materials[0]`（顶部），不改底部和两侧材质。你的模型 3 个 Material 顺序应为：索引 0=顶部，1=底部，2=两侧。

### 17.4 Collider 确认

你在 Model 子对象上添加的是 **Mesh Collider**——它会自动根据模型 Mesh 生成精确碰撞形状，**无需手动调整**。

- 验证：选中 HexTile Prefab → Model 子对象 → Inspector 中看到 MeshCollider 组件，Mesh 字段已自动填入
- 测试：Play 模式，鼠标点击六边形的边缘和中心，Console 都应输出正确的坐标

1. 测试方法：Play 模式，鼠标点击六边形边缘，Console 应输出点击坐标；点击六边形之间的空隙不输出

---

## 18. 完整验证清单

按以下顺序逐项测试。**每步通过后在 `[ ]` 中填 `[x]`。**

### 18.1 基础验证

- [x] **编译通过**：所有脚本保存后，Console 无红色错误
- [x] **棋盘生成**：Play ▶️ 后 Scene 中出现 61 个六边形，Console 输出 "棋盘生成完毕，共 61 个格子"
- [x] **点击检测**：点击任意格子，Console 输出 `点击了 (x, y, z)`
- [x] **棋子放置**：两个棋子（红方块和蓝方块）分别出现在棋盘左右两侧
- [x] **回合开始**：Console 输出 "第 1 回合 · 玩家1 的回合"，AP 显示为 2
- [x] **UI 显示**：屏幕上有 AP、金币、回合信息、结束回合按钮

### 18.2 选中与高亮验证

- [ ] **选中己方棋子**：点击红方块（P1），移动范围和攻击范围高亮显示
- [x] **未选中己方棋子时点击敌人**：不触发任何操作
- [ ] **点击空白取消选中**：高亮全部消失

### 18.3 移动验证

- [ ] **移动成功**：点击蓝色高亮空格 → 棋子瞬移过去 → AP 从 2 变为 1
- [ ] **移动范围正确**：高亮格子数大致为半径 3 的 Hex 范围内空格子数
- [ ] **不能移动到有棋子的格子**：敌方棋子所在格子不显示蓝色高亮
- [ ] **AP 不足不能移动**：AP=0 时点击空格，不移动

### 18.4 攻击验证

- [ ] **攻击成功**：点击红色高亮的敌方棋子 → Console 输出伤害值
- [ ] **伤害计算正确**：30 攻打 10 防 = 20 伤害
- [ ] **HP 减少**：被攻击的棋子 HP 从 100 变为 80
- [ ] **AP 不足不能攻击**：AP=0 时点击敌人，不攻击

### 18.5 回合验证

- [ ] **结束回合**：点击"结束回合"按钮 → 轮到 P2，P2 的 AP 补满
- [ ] **金币结算**：Console 输出 "P1 结算 +20 金币"
- [ ] **双方轮流**：P1 → P2 → P1 → P2，没有跳回

### 18.6 极端情况

- [ ] **棋子死亡**：连续攻击直到 HP ≤ 0 → 棋子消失 → 格子恢复可走
- [ ] **双方棋子都在时**：每个回合只能控制己方棋子
- [ ] **己方棋子全部死亡**：剩下的一方获胜

---

## 19. 常见错误速查

| 错误信息                                           | 原因                               | 解决方法                                                                    |
| ---------------------------------------------- | -------------------------------- | ----------------------------------------------------------------------- |
| `NullReferenceException: GridManager.Instance` | GridManager 未挂载到场景               | 确认场景中有 GridManager GameObject                                           |
| `NullReferenceException: _meshRenderer`        | HexTile Prefab 缺少 MeshRenderer   | 给 Prefab 的根物体或 Model 子物体添加 MeshRenderer                                 |
| 点击格子无反应                                        | InputHandler.OnTileClicked 未绑定   | 检查 BattleController.Start() 中 `_input.OnTileClicked += HandleTileClick` |
| 棋子没有放到格子上                                      | GameSetup Invoke 太早              | 增加 Invoke 延迟到 0.5f                                                      |
| 移动范围为空                                         | moveRange=0 或棋盘未生成               | 检查 Unit 的 moveRange 值，确认 GridManager.Start 执行完                          |
| 攻击后棋子卡住                                        | HasAttackedThisTurn 没有重置         | 检查 TurnManager.StartNewTurn() 中重置逻辑                                     |
| AP 一直为 0                                       | RefillAP 调用了但没生效                 | 确认 RefillAP 在 StartNewTurn 中调用，且 side 参数正确                              |
| 金币不增加                                          | GoldManager 的 OnDamageDealt 未被调用 | Unit.TakeDamage 中是否有 `GoldManager.Instance?.OnDamageDealt`              |

---

## 后续

Phase 1 完成后，你应该得到一个基础可玩的单机原型。Phase 2 将在此基础上添加：

- ScriptableObject 数据层
- 7棋子 + 棋子选择
- 装备系统 + 商店
- 能量/大招
- 元素城邦
- 棋盘扩展道具

> 如果某个步骤运行后报错且上面速查表不能解决，把完整的 **Console 红色错误信息** 发给我。
