# Phase 2 Trae 执行手册 — Chaotic Chess (MVC)

> **给 Trae 的指令格式。每个 Sprint 是独立的对话，直接复制粘贴给 Trae。**
> **项目路径：D:/unity/Project/Chaotic Chess**

---

## Sprint 2.1：元素城邦

复制以下内容给 Trae：

```
我有一份 Unity 6 的 Hex 战棋项目，MVC 架构，路径 D:/unity/Project/Chaotic Chess。

请在动手前先读取以下文件的完整内容，理解项目的 MVC 模式和数据流：

Assets/Game/Core/GameEnums.cs
Assets/Game/Systems/DamageCalculator.cs
Assets/Game/Piece/Model/PieceData.cs
Assets/Game/Piece/Model/PieceModel.cs
Assets/Game/Piece/Controller/PieceManager.cs
Assets/Game/Turn/Controller/TurnManager.cs
Assets/Game/Piece/Data/WarriorPieceData.asset
Assets/Game/Piece/Data/MagePieceData.asset
Assets/Game/Core/GameConfig.cs       (参考 ScriptableObject 写法)

================================

## 任务：实现元素城邦（四元素反应系统）

在现有 MVC 架构上叠加元素系统。以下是每个文件需要改/建的具体内容。

### 1. 修改 Assets/Game/Core/GameEnums.cs

追加两个枚举：

```csharp
public enum ElementType { None = 0, Fire = 1, Water = 2, Thunder = 3, Ice = 4 }

public enum ReactionType
{
    None,
    Vaporize, Melt, Overload, Frozen, ElectroCharged, Superconduct
}
```

### 2. 新建 Assets/Game/Systems/ElementReactionTable.cs

纯静态查找表类，以下是完整代码：

```csharp
/// 元素反应静态查找表。无 UnityEngine 依赖，符合现有 Model 层规范。
public static class ElementReactionTable
{
    public struct ReactionConfig
    {
        public ReactionType Type;
        public float DamageMultiplier;
        public bool ClearBase;
        public int ExtraDamage;
        public int DotDamagePerTurn;
        public int DotTurns;
        public int DefenseReduction;

        public ReactionConfig(ReactionType type, float multiplier, bool clearBase,
            int extraDmg = 0, int dotDmg = 0, int dotTurns = 0, int defRed = 0)
        {
            Type = type; DamageMultiplier = multiplier; ClearBase = clearBase;
            ExtraDamage = extraDmg; DotDamagePerTurn = dotDmg;
            DotTurns = dotTurns; DefenseReduction = defRed;
        }
    }

    static Dictionary<(ElementType, ElementType), ReactionConfig> _table = new()
    {
        [(ElementType.Fire, ElementType.Water)]   = new(ReactionType.Vaporize,     1.5f, true),
        [(ElementType.Water, ElementType.Fire)]    = new(ReactionType.Vaporize,     2.0f, true),
        [(ElementType.Fire, ElementType.Ice)]      = new(ReactionType.Melt,         2.0f, true),
        [(ElementType.Ice, ElementType.Fire)]      = new(ReactionType.Melt,         1.5f, true),
        [(ElementType.Fire, ElementType.Thunder)]  = new(ReactionType.Overload,     1.0f, true, extraDmg: 10),
        [(ElementType.Thunder, ElementType.Fire)]  = new(ReactionType.Overload,     1.0f, true, extraDmg: 10),
        [(ElementType.Water, ElementType.Thunder)] = new(ReactionType.ElectroCharged, 0.5f, true, dotDmg: 5, dotTurns: 2),
        [(ElementType.Thunder, ElementType.Water)] = new(ReactionType.ElectroCharged, 0.5f, true, dotDmg: 5, dotTurns: 2),
        [(ElementType.Water, ElementType.Ice)]     = new(ReactionType.Frozen,       0.0f, true),
        [(ElementType.Ice, ElementType.Water)]     = new(ReactionType.Frozen,       0.0f, true),
        [(ElementType.Thunder, ElementType.Ice)]   = new(ReactionType.Superconduct, 0.8f, true, defRed: 5),
        [(ElementType.Ice, ElementType.Thunder)]   = new(ReactionType.Superconduct, 0.8f, true, defRed: 5),
    };

    public static ReactionConfig GetReaction(ElementType baseElem, ElementType triggerElem)
    {
        if (_table.TryGetValue((baseElem, triggerElem), out var config))
            return config;
        return new ReactionConfig(ReactionType.None, 1.0f, false);
    }
}
```

### 3. 修改 Assets/Game/Piece/Model/PieceData.cs

在类的末尾（`prefab` 字段之后）追加：

```csharp
public ElementType innateElement = ElementType.None;
```

### 4. 修改 Assets/Game/Piece/Model/PieceModel.cs

追加以下字段和方法。放在现有字段之后，`EffectiveDefense` 之前：

```csharp
// 元素附着
public ElementType AffixedElement { get; set; }
public int AffixedElementDuration { get; set; }

// 减防
public int CurrentDefenseReduction { get; set; }

// 修正 EffectiveDefense（如果已有这个属性则修改，否则新增）
public int EffectiveDefense => Data.defense - CurrentDefenseReduction;
```

### 5. 修改 Assets/Game/Systems/DamageCalculator.cs

在现有的 `Calculate` 方法之后追加一个新方法：

```csharp
public static (int damage, ReactionType reaction) CalculateWithElement(
    int attack, int defense, ElementType attackerElement, ElementType defenderAffixed)
{
    int raw = Mathf.Max(1, attack - defense);

    if (defenderAffixed != ElementType.None)
    {
        var reaction = ElementReactionTable.GetReaction(defenderAffixed, attackerElement);
        int total = Mathf.RoundToInt(raw * reaction.DamageMultiplier) + reaction.ExtraDamage;
        return (Mathf.Max(1, total), reaction.Type);
    }

    return (raw, ReactionType.None);
}
```

### 6. 修改 Assets/Game/Piece/Controller/PieceManager.cs

找到 `AttackPiece` 方法（负责造成伤害的那个）。将现有的伤害计算逻辑替换为：

```csharp
// 旧逻辑（删除）：
// int damage = DamageCalculator.Calculate(attacker.EffectiveAttack, target.EffectiveDefense);

// 新逻辑：
var (damage, reaction) = DamageCalculator.CalculateWithElement(
    attacker.EffectiveAttack,
    target.EffectiveDefense,
    attacker.Data.innateElement,
    target.AffixedElement
);

target.TakeDamage(damage);

// 给目标挂上攻击方的元素
target.AffixedElement = attacker.Data.innateElement;
target.AffixedElementDuration = 2;

// 处理超导减防
if (reaction != ReactionType.None)
{
    var cfg = ElementReactionTable.GetReaction(target.AffixedElement, attacker.Data.innateElement);
    if (cfg.DefenseReduction > 0)
        target.CurrentDefenseReduction = cfg.DefenseReduction;
}
```

保留原有的金币记录逻辑（GoldManager.OnDamageDealt 等）。

### 7. 修改 Assets/Game/Turn/Controller/TurnManager.cs

找到 `OnTurnStarted` 方法（或回合开始时重置棋子状态的代码位置），追加以下元素清理逻辑：

```csharp
// 在现有的棋子重置循环中追加
foreach (var piece in allPieces)
{
    // 原有重置逻辑保留...
    
    // 新增：元素状态衰减
    if (piece.AffixedElementDuration > 0)
    {
        piece.AffixedElementDuration--;
        if (piece.AffixedElementDuration <= 0)
            piece.AffixedElement = ElementType.None;
    }
    piece.CurrentDefenseReduction = 0;
}
```

### 8. 修改 ScriptableObject 数据

打开 WarriorPieceData.asset：innateElement 设为 Fire
打开 MagePieceData.asset：innateElement 设为 Ice

### 验证
- 运行项目，战士攻击法师 → 战士造成伤害，法师被挂上 Fire 元素
- 法师被挂火后攻击战士（战士无底元素）→ 法师给战士挂 Ice，无反应
- 法师先打战士（挂 Ice），战士再打法师 → Melt 反应，伤害 ×1.5 或 ×2
- Console 输出元素反应类型

不要改动 ChessBoard、Battle、Gold、AP、Client 模块的任何文件。
```

---

## Sprint 2.2：装备系统

复制以下内容给 Trae：

```
我有一份 Unity 6 的 Hex 战棋项目，MVC 架构，路径 D:/unity/Project/Chaotic Chess。
已完成 Sprint 2.1（元素城邦）。

请在动手前先读取以下文件的完整内容：

Assets/Game/Piece/Model/PieceData.cs      (参考 SO 格式)
Assets/Game/Piece/Model/PieceModel.cs      (看现有属性和 EffectiveAttack 怎么写的)
Assets/Game/Piece/Controller/PieceManager.cs
Assets/Game/Gold/Model/GoldModel.cs        (看实时金币怎么花的)
Assets/Game/Gold/Controller/GoldManager.cs
Assets/Game/Client/UI_APDisplay.cs         (参考 UI 怎么写)
Assets/Game/Core/GameConfig.cs             (参考 SO 怎么写)

================================

## 任务：实现装备系统

按照项目现有的 MVC 模式（SO 数据模板 → 纯 C# Model → MonoBehaviour Controller → 事件驱动 UI）实现装备系统。

### 1. 新建 Assets/Game/Equipment/Model/EquipmentData.cs

ScriptableObject，参考 PieceData.cs 的写法：

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "Chess/Equipment Data")]
public class EquipmentData : ScriptableObject
{
    public int id;
    public string displayName;
    public string description;
    public int price;

    [Header("属性加成")]
    public int bonusHP;
    public int bonusAttack;
    public int bonusDefense;
    public int bonusMoveRange;
    public int bonusAttackRange;

    [System.Serializable]
    public class PassiveConfig
    {
        public string className;  // 如 "ExtraDamagePassive"
        public string jsonParams; // 如 "{\"amount\":5}"
    }
    public PassiveConfig[] passives;
}
```

### 2. 新建 Assets/Game/Equipment/Model/EquipmentModel.cs

纯 C# 类，无 UnityEngine 依赖。参考 PieceModel.cs 的写法：

```csharp
using System.Collections.Generic;

public class EquipmentModel
{
    public EquipmentData Data { get; }

    public EquipmentModel(EquipmentData data)
    {
        Data = data;
    }
}
```

### 3. 新建 Assets/Game/Equipment/Controller/EquipmentManager.cs

Singleton MonoBehaviour，参考 GoldManager.cs 的写法：

```csharp
using UnityEngine;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    [SerializeField] private EquipmentData[] availableEquipment;  // 固定商店列表

    private Dictionary<PlayerSide, List<EquipmentModel>> _backpack = new();
    public event System.Action<PlayerSide> OnEquipmentChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _backpack[PlayerSide.P1] = new();
        _backpack[PlayerSide.P2] = new();
    }

    public EquipmentData[] GetShopItems() => availableEquipment;

    public bool BuyEquipment(PlayerSide side, EquipmentData data)
    {
        if (!GoldManager.Instance.TrySpendGold(side, data.price)) return false;
        var model = new EquipmentModel(data);
        _backpack[side].Add(model);
        OnEquipmentChanged?.Invoke(side);
        Debug.Log($"[Equipment] {(side == PlayerSide.P1 ? "P1" : "P2")} 购买了 {data.displayName}");
        return true;
    }

    public List<EquipmentModel> GetBackpack(PlayerSide side) => _backpack.GetValueOrDefault(side, new());

    // 装备到棋子槽位
    public void EquipToPiece(PieceModel piece, EquipmentModel equip, int slotIndex)
    {
        if (piece == null || equip == null) return;
        if (slotIndex < 0 || slotIndex >= 3) return;

        // 卸旧
        if (piece.EquippedItems[slotIndex] != null)
            UnequipFromPiece(piece, slotIndex);

        piece.EquippedItems[slotIndex] = equip;
        _backpack[piece.Owner].Remove(equip);
        OnEquipmentChanged?.Invoke(piece.Owner);
    }

    public void UnequipFromPiece(PieceModel piece, int slotIndex)
    {
        var equip = piece.EquippedItems[slotIndex];
        if (equip == null) return;
        _backpack[piece.Owner].Add(equip);
        piece.EquippedItems[slotIndex] = null;
        OnEquipmentChanged?.Invoke(piece.Owner);
    }
}
```

### 4. 修改 Assets/Game/Piece/Model/PieceModel.cs

追加装备相关属性和修正逻辑。在现有 `EffectiveDefense` 附近追加/修改：

```csharp
// 装备槽位（3 个）
public EquipmentModel[] EquippedItems { get; } = new EquipmentModel[3];

// 装备提供的属性之和
private int EquipmentAttack => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusAttack);
private int EquipmentDefense => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusDefense);
private int EquipmentHP => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusHP);
public int EquipmentMoveRange => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusMoveRange);

// 修改 EffectiveAttack 和 EffectiveDefense（覆盖或修改现有定义）
public int EffectiveAttack => Data.attack + EquipmentAttack;
// EffectiveDefense 保持不变（你已经改过，加上 EquipmentDefense）
public int EffectiveDefense => Data.defense + EquipmentDefense - CurrentDefenseReduction;
public int EffectiveMoveRange => Data.moveRange + EquipmentMoveRange;
```

### 5. 创建场景中的 EquipmentManager GameObject

在现有场景中添加一个空 GameObject，命名为 "EquipmentManager"，挂载 EquipmentManager 脚本。Inspector 中先不填 AvailableEquipment（后面创建了 SO 再填）。

### 6. 创建装备 SO（至少 6 件）

在 Assets/Game/Equipment/Data/ 下创建：

Equipment_NoviceSword.asset:
  id=101, name="新手剑", price=30, bonusAttack=5

Equipment_OakShield.asset:
  id=102, name="橡木盾", price=25, bonusDefense=3

Equipment_SwiftBoots.asset:
  id=103, name="迅捷之靴", price=35, bonusMoveRange=1

Equipment_LifeGem.asset:
  id=104, name="生命宝石", price=45, bonusHP=15

Equipment_FireAmulet.asset:
  id=105, name="火焰护符", price=40, bonusAttack=2

Equipment_ThunderRing.asset:
  id=106, name="雷霆戒指", price=50, bonusAttack=3

然后用这些 asset 的引用填入 EquipmentManager 的 AvailableEquipment 数组。

### 验证
- 运行项目，选中 P1 棋子。在代码中调用 EquipmentManager.Instance.BuyEquipment(PlayerSide.P1, swordData)
- P1 金币减少 30，背包中多一件装备
- EquipmentManager.Instance.EquipToPiece(piece, sword, 0)
- piece.EffectiveAttack 从 30 变为 35
- 攻击造成伤害从 20 变为 25

不要改动 Battle、Turn、AP、Gold、ChessBoard 模块已有逻辑。
```

---

## Sprint 2.3：大招系统

复制以下内容给 Trae：

```
我有一份 Unity 6 的 Hex 战棋项目，MVC 架构，路径 D:/unity/Project/Chaotic Chess。
已完成 Sprint 2.1（元素）和 2.2（装备）。

请在动手前先读取以下文件：

Assets/Game/Piece/Model/PieceData.cs
Assets/Game/Piece/Model/PieceModel.cs      (含 EnergyModel 相关)
Assets/Game/Piece/Controller/PieceManager.cs
Assets/Game/Battle/Controller/BattleController.cs
Assets/Game/ActionPoint/Model/APModel.cs   (参考纯 C# Model 写法)

================================

## 任务：实现能量/大招系统

### 1. 新建 Assets/Game/Piece/Model/EnergyModel.cs

纯 C# 类，参考 APModel.cs 的写法：

```csharp
public class EnergyModel
{
    public int MaxEnergy { get; }
    public int CurrentEnergy { get; private set; }
    public bool IsFull => CurrentEnergy >= MaxEnergy;
    public event System.Action<int> OnEnergyChanged;

    public EnergyModel(int maxEnergy)
    {
        MaxEnergy = maxEnergy;
    }

    public void Gain(int amount)
    {
        if (amount <= 0) return;
        CurrentEnergy = Mathf.Min(CurrentEnergy + amount, MaxEnergy);
        OnEnergyChanged?.Invoke(CurrentEnergy);
    }

    public bool TryConsume()
    {
        if (!IsFull) return false;
        CurrentEnergy = 0;
        OnEnergyChanged?.Invoke(0);
        return true;
    }
}
```

### 2. 修改 Assets/Game/Piece/Model/PieceData.cs

在 innateElement 字段之后追加：

```csharp
[System.Serializable]
public class UltimateConfig
{
    public string ultimateName;
    public int energyRequired = 100;
    public int energyPerMove = 5;
    public int energyPerAttack = 15;
}
public UltimateConfig ultimateConfig;
```

### 3. 修改 Assets/Game/Piece/Model/PieceModel.cs

在构造函数末尾或字段区追加：

```csharp
public EnergyModel Energy { get; private set; }

// 在构造函数中（new PieceModel 时）
if (Data.ultimateConfig != null)
    Energy = new EnergyModel(Data.ultimateConfig.energyRequired);

public bool CanUseUltimate => Energy != null && Energy.IsFull;
```

### 4. 修改 Assets/Game/Piece/Controller/PieceManager.cs

在 MovePiece 方法的末尾（移动完成后）追加：

```csharp
// 移动获得能量
model.Energy?.Gain(model.Data.ultimateConfig?.energyPerMove ?? 0);
```

在 AttackPiece 方法的末尾（攻击完成后、金币记录之后）追加：

```csharp
// 攻击获得能量
attacker.Energy?.Gain(attacker.Data.ultimateConfig?.energyPerAttack ?? 0);
```

新增一个 UseUltimate 方法：

```csharp
public void UseUltimate(PieceModel caster, PieceModel target)
{
    if (caster == null || caster.Energy == null) return;
    if (!caster.Energy.TryConsume()) return;

    // Phase 2 简易版：大招造成双倍攻击力伤害
    int ultimateDamage = DamageCalculator.Calculate(
        caster.EffectiveAttack * 2, target.EffectiveDefense);
    target.TakeDamage(ultimateDamage);

    GoldManager.Instance?.OnDamageDealt(caster, ultimateDamage);
    GoldManager.Instance?.OnDamageReceived(target, ultimateDamage);

    Debug.Log($"[Ultimate] {caster.Data.displayName} 释放大招！造成 {ultimateDamage} 伤害");
}
```

### 5. 修改 Assets/Game/Battle/Controller/BattleController.cs

在键盘输入处理区（或 InputHandler）追加大招触发。最简单方式：空格键检测到满能时触发大招。

在 HandleTileClick 或新增快捷键处理中追加（代码位置由你根据现有结构决定）：

```csharp
// 如果选中棋子且能量满，按 U 键释放大招（对选中的敌方棋子）
if (Input.GetKeyDown(KeyCode.U) && _selectedPiece != null && _selectedPiece.CanUseUltimate)
{
    // 找到攻击范围内的第一个敌方棋子
    // ... (由你根据现有 BattleModel.Highlights 字典实现)
    if (enemyTarget != null)
    {
        APManager.Instance.ConsumeAP(_selectedPiece.Owner, 1);
        PieceManager.Instance.UseUltimate(_selectedPiece, enemyTarget);
        DeselectPiece();
    }
}
```

### 验证
- 战士移动后能量 +5（Console 验证或用 Debug 输出）
- 战士攻击后能量 +15
- 能量满 100 后按 U 键 → 大招释放，伤害 = 攻击力×2 - 防御（保底1）
- 大招消费 1 AP，能量清零
```

---

## Sprint 2.4：棋盘道具

复制以下内容给 Trae：

```
我有一份 Unity 6 的 Hex 战棋项目，MVC 架构，路径 D:/unity/Project/Chaotic Chess。
已完成 Sprint 2.1-2.3。

请在动手前先读取以下文件：

Assets/Game/ChessBoard/Controller/ChessBoardController.cs
Assets/Game/ChessBoard/Model/ChessBoardModel.cs
Assets/Game/ChessBoard/View/ChessBoardView.cs
Assets/Game/ChessBoard/View/HexTile.cs
Assets/Game/PieceLayout/Model/PieceLayoutModel.cs
Assets/Game/Piece/Model/PieceData.cs      (参考 SO 格式)
Assets/Game/Core/HexCoord.cs

================================

## 任务：实现棋盘道具系统（扩展/删除/传送）

### 1. 新建 Assets/Game/GridItem/Model/GridItemData.cs

ScriptableObject：

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "Chess/Grid Item")]
public class GridItemData : ScriptableObject
{
    public int id;
    public string displayName;
    public string description;
    public int price;
    public GridItemType itemType;  // Expand, Remove, Teleport

    public int teleportRange = 5;  // 传送用
}

public enum GridItemType { Expand, Remove, Teleport }
```

### 2. 修改 Assets/Game/ChessBoard/Model/ChessBoardModel.cs

追加两个方法：

```csharp
public void AddCoord(HexCoord coord) { _allCoords.Add(coord); }
public void RemoveCoord(HexCoord coord) { _allCoords.Remove(coord); }
```

### 3. 修改 Assets/Game/ChessBoard/View/ChessBoardView.cs

追加两个方法：

```csharp
public HexTile CreateTile(HexCoord coord)
{
    Vector3 worldPos = coord.ToWorld(hexSize);
    GameObject tileObj = Instantiate(hexTilePrefab, worldPos, Quaternion.identity, transform);
    var tile = tileObj.GetComponent<HexTile>();
    tile.Initialize(coord);
    _coordsToTiles[coord] = tile;
    return tile;
}

public void DestroyTile(HexCoord coord)
{
    if (_coordsToTiles.TryGetValue(coord, out var tile))
    {
        Destroy(tile.gameObject);
        _coordsToTiles.Remove(coord);
    }
}
```

### 4. 修改 Assets/Game/ChessBoard/Controller/ChessBoardController.cs

追加两个公共方法：

```csharp
public bool ExpandTile(HexCoord at, int directionIndex)
{
    if (!_model.Contains(at)) return false;
    var newCoord = at.Neighbor(directionIndex);
    if (_model.Contains(newCoord)) return false;
    if (_view.GetTile(newCoord) != null) return false;

    _model.AddCoord(newCoord);
    var tile = _view.CreateTile(newCoord);
    Debug.Log($"[ChessBoard] 扩展格子 {newCoord}");
    return tile != null;
}

public bool RemoveTile(HexCoord coord)
{
    if (!_model.Contains(coord)) return false;
    if (PieceLayoutModel.Instance.IsOccupied(coord)) return false;

    _model.RemoveCoord(coord);
    _view.DestroyTile(coord);
    Debug.Log($"[ChessBoard] 删除格子 {coord}");
    return true;
}

public List<HexCoord> GetExpandableNeighbors(HexCoord at)
{
    var result = new List<HexCoord>();
    if (!_model.Contains(at)) return result;
    for (int i = 0; i < 6; i++)
    {
        var neighbor = at.Neighbor(i);
        if (!_model.Contains(neighbor))
            result.Add(neighbor);
    }
    return result;
}
```

### 5. 新建 3 个 GridItemData asset

在 Assets/Game/GridItem/Data/ 下：

GridItem_Expand.asset:
  id=201, name="扩展石", price=20, itemType=Expand

GridItem_Remove.asset:
  id=202, name="删除石", price=15, itemType=Remove

GridItem_Teleport.asset:
  id=203, name="传送石", price=30, itemType=Teleport, teleportRange=5

### 验证
- 调用 ChessBoardController.Instance.GetExpandableNeighbors(某个坐标) 返回相邻空坐标
- 调用 ExpandTile → Scene 中出现新格子
- 新格子可点击、可走上去
- 调用 RemoveTile(空格子) → 格子消失。有棋子的格子删除失败返回 false
```

---

## Sprint 2.5：7 棋子完整对战

复制以下内容给 Trae：

```
我有一份 Unity 6 的 Hex 战棋项目，MVC 架构，路径 D:/unity/Project/Chaotic Chess。
已完成 Sprint 2.1-2.4。

请在动手前先读取以下文件：

Assets/Game/Core/GameSetup.cs
Assets/Game/Turn/Model/TurnModel.cs
Assets/Game/Turn/Controller/TurnManager.cs
Assets/Game/Piece/Model/PieceRegistry.cs
Assets/Game/Piece/Controller/PieceManager.cs
Assets/Game/Battle/Controller/BattleController.cs
Assets/Game/Client/UI_TurnDisplay.cs       (参考 UI 写法)
Assets/Game/Piece/Data/ (所有 .asset 文件)

================================

## 任务：7 棋子完整对战

### 1. 创建 7 个棋子的 PieceData.asset

确保 Assets/Game/Piece/Data/ 下至少有 7 个不同的棋子。如果不够，基于 Warrior 和 Mage 复制创建：

| id | 文件名 | displayName | HP | 攻 | 防 | 移 | 射程 | 元素 |
|----|--------|-------------|----|----|----|----|------|------|
| 1 | WarriorPieceData | 战士兵 | 100 | 30 | 10 | 4 | 2 | Fire |
| 2 | MagePieceData | 法师 | 80 | 35 | 10 | 3 | 2 | Ice |
| 3 | ArcherPieceData | 弓手 | 70 | 25 | 5 | 3 | 4 | Thunder |
| 4 | KnightPieceData | 骑士 | 120 | 20 | 15 | 3 | 1 | Water |
| 5 | AssassinPieceData | 刺客 | 60 | 40 | 3 | 5 | 1 | Fire |
| 6 | PriestPieceData | 牧师 | 75 | 15 | 8 | 3 | 3 | Water |
| 7 | HeavySoldierPieceData | 重装兵 | 150 | 18 | 20 | 2 | 1 | Ice |

每个都需要指定 prefab（可以暂时共用同一个 Cube Prefab，后续替换模型）。

更新 PieceRegistry.asset 的 pieces 列表，加入所有 7 个棋子。

### 2. 修改 Assets/Game/Core/GameSetup.cs

重构 SpawnPieces 逻辑，支持从 int[] 棋子 ID 数组生成：

```csharp
// GameSetup.cs 重构后
[Header("P1 阵容")]
public int[] p1PieceIds = new int[7];  // 7 个棋子 ID
public Vector2Int[] p1Coords = new Vector2Int[7]; // 对应坐标

[Header("P2 阵容")]
public int[] p2PieceIds = new int[7];
public Vector2Int[] p2Coords = new Vector2Int[7];

private void SpawnPieces()
{
    for (int i = 0; i < 7; i++)
    {
        if (p1PieceIds[i] > 0)
            PieceManager.Instance.SpawnPieceById(p1PieceIds[i],
                new HexCoord(p1Coords[i].x, p1Coords[i].y), PlayerSide.P1);
        if (p2PieceIds[i] > 0)
            PieceManager.Instance.SpawnPieceById(p2PieceIds[i],
                new HexCoord(p2Coords[i].x, p2Coords[i].y), PlayerSide.P2);
    }
}
```

在 Inspector 中手动填好双方 7 个棋子的 ID 和初始坐标。

### 3. 确认 TurnModel.CheckGameOver 逻辑

查看 TurnModel.cs，确认胜负判定是检查**任一方棋子数为 0**。如果是检查"当前玩家棋子数"则需要改成检查双方（你现在的实现应该已经是对的）。

### 4. 新建 Assets/Game/Client/UI_GameOver.cs

游戏结束显示 UI：

```csharp
using UnityEngine;
using TMPro;

public class UI_GameOver : MonoBehaviour
{
    public GameObject gameOverPanel;
    public TextMeshProUGUI winnerText;

    void OnEnable()
    {
        if (TurnManager.Instance?.Model != null)
            TurnManager.Instance.Model.OnGameOver += ShowGameOver;
    }

    void OnDisable()
    {
        if (TurnManager.Instance?.Model != null)
            TurnManager.Instance.Model.OnGameOver -= ShowGameOver;
    }

    void ShowGameOver(PlayerSide winner)
    {
        gameOverPanel.SetActive(true);
        string name = winner == PlayerSide.P1 ? "玩家1" : "玩家2";
        winnerText.text = $"游戏结束！{name} 获胜！";
    }
}
```

在 Canvas 下创建一个 Panel 命名为 GameOverPanel，默认不激活。挂载 UI_GameOver 脚本。

### 验证
- 双方各 7 个棋子在棋盘上可见
- 每个棋子可独立选中、移动、攻击
- AP=2，每回合最多激活 2 个棋子
- 一方棋子全灭后 GameOverPanel 弹出
- 所有 7 个棋子的元素反应正确触发
- 装备可正常购买和装备到任意棋子
```

---

## 全部 Sprint 完成后的验证

打一局完整的 7v7 对战，检查：

- [ ] 双方各选 7 棋子，部署在棋盘上
- [ ] 元素反应正确：火→冰 = Melt，雷→水 = 感电
- [ ] 装备商店：购买 → 背包 → 装备到棋子 → 属性变化 → 伤害变化
- [ ] 大招：能量满 → 释放 → 双倍伤害 → 能量清零
- [ ] 棋盘道具：扩展石头 → 新格子 → 可走；删除石 → 空格子消失
- [ ] 回合流转正常：P1 → P2 → 回合增加
- [ ] 金币实时结算：攻击瞬间到账
- [ ] 一方全灭 → GameOver 面板弹出
