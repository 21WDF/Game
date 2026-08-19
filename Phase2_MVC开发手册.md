# Phase 2 完整开发手册 — 基于 MVC 架构

> **项目基础**：D:/unity/Project/Chaotic Chess（MVC 架构，31 个 .cs 文件，GameConfig.asset 全局配置）
> **目标**：在现有 MVC 骨架上叠加元素城邦、装备系统、大招系统、棋盘道具、7 棋子完整对战

---

## Phase 2 系统清单与开发顺序

| # | 系统 | 新增文件 | 修改文件 | 依赖 |
|---|------|---------|---------|------|
| S1 | 元素城邦 | 4 | 5 | DamageCalculator, GameEnums, PieceData, PieceModel |
| S2 | 装备系统 + 商店 | 10 | 5 | PieceModel, PieceManager, GoldModel |
| S3 | 能量/大招系统 | 5 | 4 | PieceData, PieceModel, PieceManager |
| S4 | 棋盘道具系统 | 6 | 3 | ChessBoardController, GoldModel |
| S5 | 7 棋子完整对战 | 4 | 3 | GameSetup, TurnModel, BattleController |

---

## S1：元素城邦系统

### 设计目标

增加一层"元素克制"的策略维度。四种元素（火水雷冰）两两组合产生 6 种反应，每种反应有不同的增伤/持续/控制效果。只做**非城邦模式**——即棋子自带元素，无城邦选择 UI，元素反应直接激活。

### 新增文件

```
Assets/Game/Core/
  ElementType.cs              # 枚举（或追加到 GameEnums.cs）
Assets/Game/Systems/
  ElementReactionTable.cs     # 静态查找表
Assets/Game/Piece/Data/
  FireSlashUltimate.cs        # 示例大招（先行占位，S3 完善）
```

### 修改文件

```
Assets/Game/Core/GameEnums.cs           # 追加 ElementType 枚举
Assets/Game/Systems/DamageCalculator.cs # 追加 CalculateWithElement
Assets/Game/Piece/Model/PieceData.cs    # 追加 innateElement 字段
Assets/Game/Piece/Model/PieceModel.cs   # 追加 AffixedElement 字段
Assets/Game/Piece/Controller/PieceManager.cs # 修改 AttackPiece 使用新伤害计算
```

### S1.1 数据定义

```csharp
// 追加到 GameEnums.cs
public enum ElementType { None = 0, Fire = 1, Water = 2, Thunder = 3, Ice = 4 }

// GameEnums.cs 追加
public enum ReactionType
{
    None,
    Vaporize,       // 蒸发：火+水
    Melt,           // 融化：火+冰
    Overload,       // 超载：火+雷
    Frozen,         // 冻结：水+冰
    ElectroCharged, // 感电：水+雷
    Superconduct    // 超导：雷+冰
}
```

### S1.2 反应查找表

**新建：Assets/Game/Systems/ElementReactionTable.cs**

```csharp
/// 静态查找表，纯数据，无 MonoBehaviour 依赖
public static class ElementReactionTable
{
    public struct ReactionConfig
    {
        public ReactionType Type;
        public float DamageMultiplier;
        public bool ClearBase;          // 是否清除底元素
        public int ExtraDamage;         // 额外固定伤害（超载/超导用）
        public int DotDamagePerTurn;    // DoT 每回合伤害（感电用）
        public int DotTurns;            // DoT 持续回合
        public int DefenseReduction;    // 减防值（超导用）
        public int FreezeTurns;         // 冻结回合（0=无限直到被打破）

        public ReactionConfig(ReactionType type, float multiplier, bool clearBase,
            int extraDmg = 0, int dotDmg = 0, int dotTurns = 0, int defRed = 0, int freezeT = 0)
        {
            Type = type; DamageMultiplier = multiplier; ClearBase = clearBase;
            ExtraDamage = extraDmg; DotDamagePerTurn = dotDmg;
            DotTurns = dotTurns; DefenseReduction = defRed; FreezeTurns = freezeT;
        }
    }

    static Dictionary<(ElementType, ElementType), ReactionConfig> _table = new()
    {
        // (底元素, 触发元素) -> 反应
        [(ElementType.Fire, ElementType.Water)]   = new(ReactionType.Vaporize,     1.5f, true),
        [(ElementType.Water, ElementType.Fire)]    = new(ReactionType.Vaporize,     2.0f, true),
        [(ElementType.Fire, ElementType.Ice)]      = new(ReactionType.Melt,         2.0f, true),
        [(ElementType.Ice, ElementType.Fire)]      = new(ReactionType.Melt,         1.5f, true),
        [(ElementType.Fire, ElementType.Thunder)]  = new(ReactionType.Overload,     1.0f, true, extraDmg: 10),
        [(ElementType.Thunder, ElementType.Fire)]  = new(ReactionType.Overload,     1.0f, true, extraDmg: 10),
        [(ElementType.Water, ElementType.Thunder)] = new(ReactionType.ElectroCharged, 0.5f, true, dotDmg: 5, dotTurns: 2),
        [(ElementType.Thunder, ElementType.Water)] = new(ReactionType.ElectroCharged, 0.5f, true, dotDmg: 5, dotTurns: 2),
        [(ElementType.Water, ElementType.Ice)]     = new(ReactionType.Frozen,       0.0f, true, freezeT: 1),
        [(ElementType.Ice, ElementType.Water)]     = new(ReactionType.Frozen,       0.0f, true, freezeT: 1),
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

### S1.3 修改 PieceData

```csharp
// PieceData.cs 追加字段
public ElementType innateElement = ElementType.None;
```

### S1.4 修改 PieceModel

```csharp
// PieceModel.cs 追加字段
public ElementType AffixedElement { get; set; }        // 身上挂的元素（谁打的）
public int AffixedElementDuration { get; set; }        // 剩余回合

// 防御力受减益影响
public int CurrentDefenseReduction { get; set; }
public int EffectiveDefense => Data.defense - CurrentDefenseReduction;
```

### S1.5 修改 DamageCalculator

```csharp
// DamageCalculator.cs 追加方法
public static (int damage, ReactionType reaction) CalculateWithElement(
    int attack, int defense, ElementType attackerElement, ElementType defenderAffixed)
{
    int raw = Mathf.Max(1, attack - defense);  // 基础减法公式不变

    if (defenderAffixed != ElementType.None)
    {
        var reaction = ElementReactionTable.GetReaction(defenderAffixed, attackerElement);
        float multiplier = reaction.DamageMultiplier;
        int total = Mathf.RoundToInt(raw * multiplier) + reaction.ExtraDamage;
        return (Mathf.Max(1, total), reaction.Type);
    }

    return (raw, ReactionType.None);
}
```

### S1.6 修改 PieceManager.AttackPiece

```csharp
// PieceManager.cs AttackPiece 方法中，伤害计算改为：
var (damage, reaction) = DamageCalculator.CalculateWithElement(
    attacker.EffectiveAttack,
    target.EffectiveDefense,
    attacker.Data.innateElement,           // 攻击方元素
    target.AffixedElement                  // 受击方身上的底元素
);

target.TakeDamage(damage);
target.AffixedElement = attacker.Data.innateElement;  // 挂上攻击方的元素
target.AffixedElementDuration = 2;                     // 持续 2 回合

// 处理额外效果
var reactionCfg = ElementReactionTable.GetReaction(target.AffixedElement, attacker.Data.innateElement);
if (reactionCfg.DefenseReduction > 0)
    target.CurrentDefenseReduction = reactionCfg.DefenseReduction;
```

### S1.7 回合清理

```csharp
// TurnManager.OnTurnStarted 中追加
foreach (var piece in allPieces)
{
    piece.AffixedElementDuration--;
    if (piece.AffixedElementDuration <= 0)
        piece.AffixedElement = ElementType.None;
    
    piece.CurrentDefenseReduction = 0; // 减防仅持续一回合
}
```

### S1.8 创建 Warrior 和 Mage 的 PieceData.asset 更新

```
WarriorPieceData: innateElement = Fire
MagePieceData:    innateElement = Ice
```

---

## S2：装备系统 + 商店

### 设计目标

固定商店（所有装备明码标价），每个棋子 3 个装备槽位，装备可随时购买和更换。装备提供属性加成和被动效果。复用 MVC 模式：EquipmentData (SO) → EquipmentModel (运行时) → EquipmentInventory (仓库)。

### 新增文件

```
Assets/Game/Equipment/
  Model/
    EquipmentData.cs           # ScriptableObject
    EquipmentModel.cs          # 纯 C# 运行时
  Controller/
    EquipmentManager.cs        # Singleton
  Effects/
    IPassiveEffect.cs          # 被动效果接口
    ExtraDamagePassive.cs      # 示例：攻击+额外伤害
    HealOnKillPassive.cs       # 示例：击杀回血
  Data/
    Equipment_NoviceSword.asset
    Equipment_OakShield.asset
    Equipment_FireAmulet.asset
    Equipment_ThunderRing.asset

Assets/Game/Client/
  UI_ShopPanel.cs              # 商店 UI
  UI_EquipmentPanel.cs         # 装备面板 UI
```

### 修改文件

```
Assets/Game/Piece/Model/PieceModel.cs      # 追加 EquippedItems / 被动效果
Assets/Game/Piece/Controller/PieceManager.cs # 追加 Equip/Unequip 方法
Assets/GameClient/InputHandler.cs           # 追加商店/装备面板快捷键
```

### S2.1 EquipmentData (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "Chess/Equipment Data")]
public class EquipmentData : ScriptableObject
{
    public int id;
    public string displayName;
    public string description;
    public int price;

    // 属性加成
    public int bonusHP;
    public int bonusAttack;
    public int bonusDefense;
    public int bonusMoveRange;
    public int bonusAttackRange;
    public int bonusAPCap;

    // 被动效果配置
    [System.Serializable]
    public class PassiveConfig
    {
        public string className;  // 如 "ExtraDamagePassive"
        public string jsonParams; // 如 {"amount":5}
    }
    public PassiveConfig[] passives;
}
```

### S2.2 IPassiveEffect 接口

```csharp
/// 所有装备被动的接口
public interface IPassiveEffect
{
    void OnEquip(PieceModel owner);
    void OnUnequip(PieceModel owner);
}
```

### S2.3 EquipmentModel（纯 C# 运行时）

```csharp
public class EquipmentModel
{
    public EquipmentData Data { get; }
    public List<IPassiveEffect> ActivePassives { get; } = new();

    public EquipmentModel(EquipmentData data)
    {
        Data = data;
        // 根据 Data.passives 反射创建 IPassiveEffect 实例
        foreach (var cfg in data.passives)
        {
            var instance = PassiveFactory.Create(cfg);
            if (instance != null) ActivePassives.Add(instance);
        }
    }
}
```

### S2.4 EquipmentManager（Controller）

```csharp
public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }
    
    [SerializeField] private EquipmentData[] availableEquipment; // 固定商店列表

    private Dictionary<PlayerSide, List<EquipmentModel>> _backpack = new();
    private EquipmentModel _selectedEquipment;

    void Awake() => Instance = this;

    void Start()
    {
        _backpack[PlayerSide.P1] = new();
        _backpack[PlayerSide.P2] = new();
    }

    public EquipmentData[] GetShopItems() => availableEquipment;

    public bool BuyEquipment(PlayerSide side, EquipmentData data)
    {
        if (!GoldModel 实例.TrySpendGold(side, data.price)) return false;
        
        var model = new EquipmentModel(data);
        _backpack[side].Add(model);
        OnEquipmentChanged?.Invoke(side);
        return true;
    }

    public List<EquipmentModel> GetBackpack(PlayerSide side) => _backpack[side];

    // 装备到棋子
    public bool EquipToPiece(PieceModel piece, EquipmentModel equip, int slot)
    {
        if (slot < 0 || slot >= 3) return false;
        
        // 卸下旧装备
        if (piece.EquippedItems[slot] != null)
            UnequipFromPiece(piece, slot);
        
        piece.EquippedItems[slot] = equip;
        foreach (var passive in equip.ActivePassives)
            passive.OnEquip(piece);
        
        _backpack[piece.Owner].Remove(equip);
        return true;
    }

    public void UnequipFromPiece(PieceModel piece, int slot)
    {
        var equip = piece.EquippedItems[slot];
        if (equip == null) return;
        
        foreach (var passive in equip.ActivePassives)
            passive.OnUnequip(piece);
        
        _backpack[piece.Owner].Add(equip);
        piece.EquippedItems[slot] = null;
    }

    public event System.Action<PlayerSide> OnEquipmentChanged;
}
```

### S2.5 修改 PieceModel

```csharp
// PieceModel.cs 追加
public EquipmentModel[] EquippedItems { get; } = new EquipmentModel[3]; // 3 槽位

// 属性修正
public int EquipmentAttack => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusAttack);
public int EffectiveAttack => Data.attack + EquipmentAttack; // 重写
public int EquipmentDefense => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusDefense);
public int EffectiveDefense => Data.defense + EquipmentDefense - CurrentDefenseReduction;
public int EquipmentMoveRange => EquippedItems.Where(e => e != null).Sum(e => e.Data.bonusMoveRange);
public int EffectiveMoveRange => Data.moveRange + EquipmentMoveRange;
```

### S2.6 Shop UI 要点

```
UI_ShopPanel.cs 挂在 Canvas 上
- 遍历 EquipmentManager.GetShopItems()
- 为每件装备创建按钮（名称 + 价格 + 描述）
- 购买按钮调用 EquipmentManager.BuyEquipment
- 装备面板：显示棋子 3 槽位 + 背包装备列表
- 快捷键：B 打开商店，E 打开装备面板（通过 InputHandler）
```

### S2.7 示例被动

```csharp
public class ExtraDamagePassive : IPassiveEffect
{
    private int _amount;
    
    public ExtraDamagePassive(int amount) { _amount = amount; }

    public void OnEquip(PieceModel owner)
    {
        // 直接加成到攻击力（已在 EffectiveAttack 中计算）
    }

    public void OnUnequip(PieceModel owner) { }
}
```

### S2.8 示例装备数据

| 装备 | 价格 | 属性 | 被动 |
|------|------|------|------|
| 新手剑 | 30 | +5 攻击 | 无 |
| 橡木盾 | 25 | +3 防御 | 无 |
| 火焰护符 | 40 | +2 攻击 | 攻击变为火元素 |
| 迅捷之靴 | 35 | +1 移动范围 | 无 |
| 生命宝石 | 45 | +15 HP | 无 |
| 雷霆戒指 | 50 | +3 攻击 | 攻击额外 +3 伤害 |
| 吸血之刃 | 60 | +5 攻击 | 击杀回复 5 HP |

---

## S3：能量/大招系统

### 设计目标

棋子移动和攻击获得能量，能量满后可以消耗 1 AP 释放大招。大招效果数据定义。

### 新增文件

```
Assets/Game/Piece/
  Model/
    EnergyModel.cs             # 纯 C# 能量模型
  Effects/
    IUltimateEffect.cs         # 大招效果接口
    FireSlashUltimate.cs       # 示例大招
    IceBarrierUltimate.cs      # 示例大招
```

### 修改文件

```
Assets/Game/Piece/Model/PieceData.cs     # 追加 UltimateConfig
Assets/Game/Piece/Model/PieceModel.cs    # 追加 CurrentEnergy / CanUseUltimate
Assets/Game/Piece/Controller/PieceManager.cs # 追加 UseUltimate
Assets/Game/Battle/Controller/BattleController.cs # 追加大招按钮/触发
Assets/Game/Client/                       # 追加 UI_EnergyBar
```

### S3.1 PieceData 新增配置

```csharp
// PieceData.cs 追加
[System.Serializable]
public class UltimateConfig
{
    public string ultimateName;
    public int energyRequired = 100;
    public int energyPerMove = 5;
    public int energyPerAttack = 15;
    public string effectClassName;  // "FireSlashUltimate"
    public string effectJsonParams; // {"damage":50}
}
public UltimateConfig ultimateConfig;
```

### S3.2 EnergyModel（纯 C#）

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
        CurrentEnergy = 0;
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

### S3.3 PieceModel 追加

```csharp
// PieceModel.cs 追加
public EnergyModel Energy { get; private set; }

// 构造函数中初始化
Energy = new EnergyModel(Data.ultimateConfig?.energyRequired ?? 100);

public bool CanUseUltimate => Energy.IsFull; // AP 消耗在 BattleController 检查
```

### S3.4 PieceManager 移动和攻击时加能量

```csharp
// PieceManager.MovePiece 末尾
model.Energy.Gain(model.Data.ultimateConfig?.energyPerMove ?? 0);

// PieceManager.AttackPiece 开头
attacker.Energy.Gain(attacker.Data.ultimateConfig?.energyPerAttack ?? 0);
```

### S3.5 IUltimateEffect 接口

```csharp
public interface IUltimateEffect
{
    void Execute(PieceModel caster, PieceModel target);
}
```

### S3.6 示例大招

```csharp
public class FireSlashUltimate : IUltimateEffect
{
    private int _bonusDamage;
    
    public FireSlashUltimate(int bonusDamage) { _bonusDamage = bonusDamage; }
    
    public void Execute(PieceModel caster, PieceModel target)
    {
        int damage = DamageCalculator.Calculate(
            caster.EffectiveAttack + _bonusDamage, 
            target.EffectiveDefense);
        target.TakeDamage(damage);
    }
}
```

### S3.7 棋子的 UltimateConfig 示例

| 棋子 | 大招名 | 能量需求 | 效果 |
|------|--------|---------|------|
| 战士 | 炎爆斩 | 100 | 造成 50 额外伤害 |
| 法师 | 冰霜屏障 | 100 | 自身 +10 防御 2 回合 |

---

## S4：棋盘道具系统

### 设计目标

通过金币购买道具来扩展/删除/传送棋盘格子。每个道具实现 IGridItemEffect 接口。

### 新增文件

```
Assets/Game/GridItem/
  Model/
    GridItemData.cs             # ScriptableObject
  Effects/
    IGridItemEffect.cs          # 道具效果接口
    ExpandTileEffect.cs
    RemoveTileEffect.cs
    TeleportUnitEffect.cs
  Data/
    GridItem_Expand.asset
    GridItem_Remove.asset
    GridItem_Teleport.asset
```

### 修改文件

```
Assets/Game/ChessBoard/Controller/ChessBoardController.cs  # 追加 ExpandTile / RemoveTile
Assets/Game/ChessBoard/Model/ChessBoardModel.cs            # 追加动态增删坐标
Assets/Game/PieceLayout/Model/PieceLayoutModel.cs          # 校验删除时格子上无棋子
```

### S4.1 GridItemData

```csharp
[CreateAssetMenu(menuName = "Chess/Grid Item")]
public class GridItemData : ScriptableObject
{
    public int id;
    public string displayName;
    public int price;
    public string effectClassName;  // ExpandTileEffect
    public string effectJsonParams; // {}
}
```

### S4.2 IGridItemEffect

```csharp
public interface IGridItemEffect
{
    bool CanExecute(HexCoord target, PlayerSide user);
    void Execute(HexCoord target, PlayerSide user);
}
```

### S4.3 ChessBoardController 新增方法

```csharp
// ChessBoardController.cs 追加

public bool ExpandTile(HexCoord at, int directionIndex)
{
    if (!_model.Contains(at)) return false;
    var newCoord = at.Neighbor(directionIndex);
    if (_model.Contains(newCoord)) return false; // 已有格子
    if (_view.GetTile(newCoord) != null) return false;

    _model.AddCoord(newCoord);
    var tile = _view.CreateTile(newCoord);
    return tile != null;
}

public bool RemoveTile(HexCoord coord)
{
    if (!_model.Contains(coord)) return false;
    if (PieceLayoutModel.Instance.IsOccupied(coord)) return false; // 有棋子不能删

    _model.RemoveCoord(coord);
    _view.DestroyTile(coord);
    return true;
}
```

### S4.4 ChessBoardModel 追加方法

```csharp
// ChessBoardModel.cs 追加
public void AddCoord(HexCoord coord) => _allCoords.Add(coord);
public void RemoveCoord(HexCoord coord) => _allCoords.Remove(coord);
```

### S4.5 道具数据

| 道具 | 价格 | 效果 |
|------|------|------|
| 扩展石 | 20 | 在相邻空位创建一个格子 |
| 删除石 | 15 | 删除一个空格子 |
| 传送石 | 30 | 将一个己方棋子传送到 5 格内空格 |

---

## S5：7 棋子完整对战

### 设计目标

从棋子池选 7 个上场，进入部署阶段，然后开始对战。回合结束条件改为一方 7 棋子全灭。

### 新增文件

```
Assets/Game/Client/
  UI_UnitSelection.cs          # 棋子选择界面
  UI_Deployment.cs             # 部署界面
  UI_GameOver.cs               # 胜负界面
Assets/Game/Battle/Model/
  TeamConfig.cs                # 可选：用于记录选定的 7 棋子 id 列表
```

### 修改文件

```
Assets/Game/Core/GameSetup.cs              # 重构：支持 7 棋子部署
Assets/Game/Turn/Model/TurnModel.cs        # CheckGameOver 已支持全灭（无需改）
Assets/Game/Battle/Controller/BattleController.cs # 支持多棋子选中
Assets/Game/Piece/Controller/PieceManager.cs      # 支持批量生成
```

### S5.1 战前流程

```
[玩家1 选棋子] → [玩家2 选棋子] → [玩家1 部署] → [玩家2 部署] → 对战开始
```

### S5.2 棋子池配置

在 PieceRegistry 中已经注册了所有可用棋子。Phase 2 至少需要 7+ 种棋子供选择。

**Phase 2 最小棋子池（建议 9 种）**：

| id | 名称 | HP | 攻 | 防 | 移 | 射程 | 元素 | 大招 |
|----|------|----|----|----|----|------|------|------|
| 1 | 战士兵 | 100 | 30 | 10 | 4 | 2 | 火 | 炎爆斩 |
| 2 | 法师 | 80 | 35 | 10 | 3 | 2 | 冰 | 冰霜屏障 |
| 3 | 弓手 | 70 | 25 | 5 | 3 | 4 | 雷 | 雷霆万钧 |
| 4 | 骑士 | 120 | 20 | 15 | 3 | 1 | 水 | 圣盾术 |
| 5 | 刺客 | 60 | 40 | 3 | 5 | 1 | 火 | 暗杀 |
| 6 | 牧师 | 75 | 15 | 8 | 3 | 3 | 水 | 治愈祷言 |
| 7 | 重装兵 | 150 | 18 | 20 | 2 | 1 | 冰 | 冰墙 |
| 8 | 术士 | 65 | 30 | 5 | 3 | 3 | 雷 | 雷暴 |
| 9 | 游侠 | 85 | 22 | 8 | 4 | 3 | 火 | 多重射击 |

### S5.3 GameSetup 重构（概念代码）

```csharp
// GameSetup.SpawnPieces 改为接收 int[] pieceIds
public void SpawnPieces(PlayerSide side, int[] pieceIds, HexCoord[] coords)
{
    for (int i = 0; i < pieceIds.Length; i++)
    {
        PieceManager.Instance.SpawnPieceById(pieceIds[i], coords[i], side);
    }
}
```

### S5.4 部署阶段 UI

```
UI_Deployment.cs
- 显示己方 7 个棋子缩略图
- 点击棋子 → 棋盘高亮可部署区域（己方半场空格）
- 点击空格 → 放置棋子
- 放置完成后点"确认部署"
```

### S5.5 UI_UnitSelection

```
- 显示棋子池（从 PieceRegistry 读取）
- 玩家点击棋子选中/取消
- 最多选 7 个
- 确认后切换到部署阶段
```

---

## Phase 2 项目目录终态

```
Assets/Game/
  Core/
    GameConfig.cs / GameConfig.asset   (已有)
    GameEnums.cs (追加 ElementType, ReactionType)
    GameSetup.cs (重构)
    HexCoord.cs (已有)
  Systems/
    DamageCalculator.cs (追加 CalculateWithElement)
    ElementReactionTable.cs (新增)
  Equipment/
    Model/
      EquipmentData.cs (新增)
      EquipmentModel.cs (新增)
    Controller/
      EquipmentManager.cs (新增)
    Effects/
      IPassiveEffect.cs (新增)
      ExtraDamagePassive.cs (新增)
      HealOnKillPassive.cs (新增)
    Data/
      Equipment_*.asset (新增 10+)
  GridItem/
    Model/
      GridItemData.cs (新增)
    Effects/
      IGridItemEffect.cs (新增)
      ExpandTileEffect.cs (新增)
      RemoveTileEffect.cs (新增)
      TeleportUnitEffect.cs (新增)
    Data/
      GridItem_*.asset (新增 3+)
  Piece/
    Model/
      PieceData.cs (追加 UltimateConfig, ElementType)
      PieceModel.cs (追加 Energy, Equipment, Element)
      EnergyModel.cs (新增)
      PieceRegistry.cs (已有，扩充)
    Controller/
      PieceManager.cs (追加能量/大招/装备逻辑)
    Effects/
      IUltimateEffect.cs (新增)
      FireSlashUltimate.cs (新增)
    Data/
      PieceData_*.asset (新增 7+)
  ChessBoard/
    Controller/
      ChessBoardController.cs (追加 ExpandTile/RemoveTile)
    Model/
      ChessBoardModel.cs (追加 AddCoord/RemoveCoord)
  Turn/
    Model/
      TurnModel.cs (追加元素回合清理)
  Battle/
    Controller/
      BattleController.cs (追加伤害类型显示/大招按钮)
  Client/
    UI_ShopPanel.cs (新增)
    UI_EquipmentPanel.cs (新增)
    UI_UnitSelection.cs (新增)
    UI_Deployment.cs (新增)
    UI_GameOver.cs (新增)
    UI_EnergyBar.cs (新增)
    InputHandler.cs (追加快捷键)
```

共计：**新增约 30 个文件，修改约 15 个文件。**

---

## 开发顺序建议

| Sprint | 内容 | 预估新增脚本 |
|--------|------|------------|
| 2.1 | S1 元素城邦 | 3 新增 + 5 修改 |
| 2.2 | S2 装备系统 | 10 新增 + 3 修改 |
| 2.3 | S3 大招系统 | 5 新增 + 4 修改 |
| 2.4 | S4 棋盘道具 | 6 新增 + 3 修改 |
| 2.5 | S5 7 棋子对战 | 4 新增 + 3 修改 |

**严格按顺序做**——元素城邦影响伤害计算（所有攻击的基础），必须先落地。装备系统次之（影响属性），大招再次（依赖装备和元素），道具最后（独立系统），7 棋子作为集成。

---

## 每个 Sprint 的验收 checkpoints

### S1 验收
- [ ] 火棋子攻击冰底棋子：伤害 ×2，Console 输出 "Melt"
- [ ] 冰棋子被火攻击后，AffixedElement = Fire，持续 2 回合然后消失
- [ ] 减防效果生效：攻击方打超导目标时伤害增加

### S2 验收
- [ ] 按 B 打开商店，显示装备列表和价格
- [ ] 购买一把剑（-30 金币），背包中多一件装备
- [ ] 装备到棋子上，EffectiveAttack 从 30 变为 35
- [ ] 造成伤害从 20 变为 25
- [ ] 卸下装备 → 回背包 → 给别人 → 属性正确转移

### S3 验收
- [ ] 战士移动后能量 +5，攻击后 +15
- [ ] 能量满 100 后可以按 U（或其他键）释放大招
- [ ] 大招消耗 1 AP，能量清零
- [ ] 不在己方回合不能放大招

### S4 验收
- [ ] 购买"扩展石" → 点击相邻空格 → 新格子出现
- [ ] 新格子可走上去
- [ ] 不能在有棋子的格子上使用删除石
- [ ] 传送棋子到 5 格内空格

### S5 验收
- [ ] 棋子选择界面可用，最多选 7 个
- [ ] 部署阶段在己方半场放置 7 个棋子
- [ ] 全部棋子可以在战场上移动和攻击
- [ ] 一方 7 棋子全灭 → 显示 "玩家X 获胜" 界面
