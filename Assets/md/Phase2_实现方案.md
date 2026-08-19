# Phase 2 完整单机版本 - 实现规格

## 系统清单与优先级

| #  | 系统        | 预估脚本数 | 依赖            |
| -- | --------- | ----- | ------------- |
| S1 | 元素城邦      | 4 个   | 伤害计算（Phase 1） |
| S2 | 装备系统 + 商店 | 8 个   | 经济系统（Phase 1） |
| S3 | 能量/大招系统   | 5 个   | 元素系统 + 伤害系统   |
| S4 | 棋盘道具系统    | 5 个   | 棋盘系统（Phase 1） |
| S5 | 7棋子完整对战   | 6 个   | 以上全部          |

---

## S1：元素城邦系统

### 数据定义

```csharp
// ElementType.cs — 元素枚举
public enum ElementType { None = 0, Fire = 1, Water = 2, Thunder = 3, Ice = 4 }

// ElementReaction.cs — 反应类型枚举
public enum ReactionType
{
    None,
    Vaporize,      // 蒸发：火+水
    Melt,          // 融化：火+冰
    Overload,      // 超载：火+雷
    Frozen,        // 冻结：水+冰
    ElectroCharged,// 感电：水+雷
    Superconduct   // 超导：雷+冰
}
```

### 反应查找表（单例静态类）

```csharp
public static class ElementReactionTable
{
    // (底元素, 触发元素) → (反应类型, 倍率, 是否清除底元素)
    static Dictionary<(ElementType, ElementType), ReactionConfig> _table = new()
    {
        // 火底反应
        [(Fire, Water)]   = new(Vaporize,      1.5f, clearBase: true),
        [(Fire, Thunder)] = new(Overload,      1.0f, clearBase: true),  // +额外伤害+击退
        [(Fire, Ice)]     = new(Melt,          2.0f, clearBase: true),
        
        // 水底反应
        [(Water, Fire)]    = new(Vaporize,      2.0f, clearBase: true),
        [(Water, Thunder)] = new(ElectroCharged, 0.5f, clearBase: true), // +DoT
        [(Water, Ice)]     = new(Frozen,        0.0f, clearBase: true), // +控制
        
        // 雷底反应
        [(Thunder, Fire)]    = new(Overload,      1.0f, clearBase: true),
        [(Thunder, Water)]   = new(ElectroCharged, 0.5f, clearBase: true),
        [(Thunder, Ice)]     = new(Superconduct,  0.8f, clearBase: true),
        
        // 冰底反应
        [(Ice, Fire)]    = new(Melt,          1.5f, clearBase: true),
        [(Ice, Water)]   = new(Frozen,        0.0f, clearBase: true),
        [(Ice, Thunder)] = new(Superconduct,  0.8f, clearBase: true),
    };

    public static ReactionConfig GetReaction(ElementType baseElem, ElementType triggerElem)
    {
        if (_table.TryGetValue((baseElem, triggerElem), out var config))
            return config;
        return new ReactionConfig(ReactionType.None, 1.0f, false);
    }
}

public record ReactionConfig(ReactionType Type, float DamageMultiplier, bool ClearBase);
```

### 元素附着逻辑

```csharp
// Unit.cs 新增字段
public ElementType CurrentElement { get; set; }       // 棋子当前元素（可被道具改）
public ElementType AffixedElement { get; set; }       // 身上挂的元素（来自敌人攻击）
public int AffixedElementDuration { get; set; }       // 剩余回合数

// HexTile.cs 新增字段  
public ElementType TerrainElement { get; set; }
public int TerrainElementDuration { get; set; }

// 攻击时挂元素
public void ApplyElement(Unit target, ElementType element, int duration = 2)
{
    target.AffixedElement = element;
    target.AffixedElementDuration = duration;
}

// 大招改变地形
public void ApplyTerrainElement(HexTile tile, ElementType element, int duration = 3)
{
    tile.TerrainElement = element;
    tile.TerrainElementDuration = duration;
}
```

### 伤害计算修改

```csharp
// DamageCalculator 中新增元素反应处理
public static DamageResult CalculateWithElement(
    int attack, int defense,
    ElementType attackerElement, ElementType defenderAffixedElement,
    ElementType terrainElement)
{
    // Step 1: 基础伤害（减法公式：atk - def，保底1点）
    int raw = Mathf.Max(1, attack - defense);
    
    // Step 2: 检查地形元素（踩上去时触发反应）
    ElementType baseElem = defenderAffixedElement != ElementType.None 
        ? defenderAffixedElement 
        : terrainElement;
    
    if (baseElem != ElementType.None)
    {
        var reaction = ElementReactionTable.GetReaction(baseElem, attackerElement);
        raw *= reaction.DamageMultiplier;
        
        if (reaction.ClearBase)
            defenderAffixedElement = ElementType.None; // 客户端不回传，仅概念
        
        return new DamageResult(raw, attackerElement, reaction.Type);
    }
    
    // Step 3: 给目标挂上当前攻击的元素
    // (由 Unit.ApplyElement 在外部执行，此处只返回元素信息)
    return new DamageResult(raw, attackerElement, ReactionType.None);
}
```

### 新建文件清单

```
Assets/_Game/
  Data/
    ElementType.cs              # 枚举定义
    ReactionConfig.cs           # 反应结果记录
    ElementReactionTable.cs     # 查找表
  Server/Systems/
    ElementReactionResolver.cs  # 反应解析器（纯逻辑）
```

---

## S2：装备系统 + 商店UI

### ScriptableObject 更新

```csharp
// EquipmentData.cs（之前在 Phase 1 已有骨架，现在补全）
[CreateAssetMenu(fileName = "Eq_", menuName = "Game/Equipment")]
public class EquipmentData : ScriptableObject
{
    public string equipId;
    public string equipName;
    public string description;
    public int price;
    
    // 基础属性加成
    public int bonusHP;
    public int bonusAttack;
    public int bonusDefense;
    public int bonusMoveRange;
    public int bonusAPCap;
    
    // 被动效果配置（数据描述，运行时实例化）
    public List<PassiveEffectConfig> passives;
}

[System.Serializable]
public class PassiveEffectConfig
{
    public string effectClassName;  // 如 "ExtraFireDamage"
    public string jsonParams;       // 如 {"bonusDamage": 10}
    // 运行时通过反射或工厂方法创建 IPassiveEffect 实例
}
```

### 核心接口

```csharp
// IPassiveEffect.cs
public interface IPassiveEffect
{
    System.Type[] InterestedEvents { get; }  // 关心哪些事件
    void OnEvent(object eventData, Unit owner);
}

// PassiveEffectFactory.cs
public static class PassiveEffectFactory
{
    // 从 PassiveEffectConfig 创建实例
    public static IPassiveEffect Create(PassiveEffectConfig config)
    {
        // 反射或 switch 创建对应类的实例
        // 使用 JsonUtility 反序列化参数
    }
}
```

### Unit.cs 装备相关更新

```csharp
public class Unit : MonoBehaviour
{
    // === 装备系统 ===
    public const int MaxEquipmentSlots = 3;
    
    // 已装备的（槽位固定3个，可能为空）
    public EquipmentData[] EquippedItems = new EquipmentData[MaxEquipmentSlots];
    
    // 运行时被动效果实例（装备装上时创建，卸下时移除）
    List<IPassiveEffect> _activePassives = new();
    
    public bool EquipItem(EquipmentData item, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxEquipmentSlots) return false;
        
        // 如果该槽位已有装备，先卸下来回背包
        if (EquippedItems[slotIndex] != null)
            UnequipItem(slotIndex);
        
        EquippedItems[slotIndex] = item;
        
        // 创建被动效果实例并注册到 EventBus
        foreach (var passive in item.passives)
        {
            var instance = PassiveEffectFactory.Create(passive);
            _activePassives.Add(instance);
            RegisterPassiveToEvents(instance);
        }
        return true;
    }
    
    public EquipmentData UnequipItem(int slotIndex)
    {
        var item = EquippedItems[slotIndex];
        // 注销该装备的所有被动效果
        // 把装备还给背包
        EquippedItems[slotIndex] = null;
        return item;
    }
    
    // 装备提供的属性总和
    public int EquipmentAttack => EquippedItems.Where(e => e != null).Sum(e => e.bonusAttack);
    public int EquipmentDefense => EquippedItems.Where(e => e != null).Sum(e => e.bonusDefense);
    // ... 其他属性同理
    
    // 修正后的 EffectiveAttack
    public int EffectiveAttack => Data.baseAttack + EquipmentAttack;
}
```

### 局内背包

```csharp
// 挂在 PlayerState 或独立 Manager 上
public class EquipmentInventory : MonoBehaviour
{
    // PlayerSide → 拥有的装备列表
    Dictionary<PlayerSide, List<EquipmentData>> _backpack = new();
    
    public void BuyEquipment(PlayerSide player, EquipmentData item)
    {
        if (GoldManager.Instance.TrySpendGold(player, item.price))
        {
            _backpack[player].Add(item);
            EventBus.Publish(new EquipmentPurchasedEvent { player = player, item = item });
        }
    }
    
    public List<EquipmentData> GetBackpack(PlayerSide player) => _backpack[player];
}
```

### 商店与UI

```csharp
// 商店数据（也是 ScriptableObject）
[CreateAssetMenu(fileName = "ShopConfig", menuName = "Game/Shop Config")]
public class ShopConfig : ScriptableObject
{
    public List<EquipmentData> availableEquipment;  // 所有可购买的装备
}
```

UI 部分简要说明（等你实际实现时再展开）：

- `UI_Shop.cs`：遍历 ShopConfig.availableEquipment，显示列表，点击购买
- `UI_EquipmentPanel.cs`：显示当前选中棋子的3个槽位 + 背包装备列表，拖拽/点击装备
- 购买不消耗AP，任何时候可打开

### 新建文件清单

```
Assets/_Game/
  Data/
    EquipmentData.cs (更新)
    ShopConfig.asset
  Server/Effects/EquipmentPassives/
    IPassiveEffect.cs
    PassiveEffectFactory.cs
    ExtraFireDamage.cs          # 示例被动
    HealOnKill.cs               # 示例被动
    FreeMovePerTurn.cs          # 示例被动
    BonusAPOnEnergyFull.cs      # 示例被动
  Server/Managers/
    EquipmentInventory.cs
  Client/UI/
    UI_Shop.cs
    UI_EquipmentPanel.cs
```

---

## S3：能量/大招系统

### 数据定义

```csharp
// UnitData.cs 新增
[System.Serializable]
public class UltimateConfig
{
    public string ultimateName;
    public string ultimateDescription;
    public int energyRequired = 100;       // 需要多少能量释放
    public UltimateEffectType effectType;  // 枚举：Damage / Heal / Buff / Debuff / AOE
    public string effectParamsJson;        // 具体参数JSON
    public int cooldownTurns;              // 冷却回合（0=无冷却）
}

[System.Serializable]
public class EnergyConfig
{
    public int energyPerMove = 5;    // 移动获得能量
    public int energyPerAttack = 15; // 攻击获得能量
    public int energyPerDamageTaken = 3; // 受击获得能量
}
```

### Unit.cs 能量相关更新

```csharp
public class Unit : MonoBehaviour
{
    public int CurrentEnergy { get; private set; }
    public int LastUltimateTurn { get; private set; } // 用于冷却检测
    
    public bool CanUseUltimate 
    {
        get
        {
            if (CurrentEnergy < Data.ultimateConfig.energyRequired) return false;
            if (Data.ultimateConfig.cooldownTurns > 0 
                && TurnManager.Instance.CurrentTurn - LastUltimateTurn < Data.ultimateConfig.cooldownTurns) 
                return false;
            if (!APManager.Instance.HasAP(Owner, 1)) return false;
            return true;
        }
    }
    
    public void GainEnergy(int amount)
    {
        CurrentEnergy = Mathf.Min(CurrentEnergy + amount, Data.ultimateConfig.energyRequired);
        // 注意：达到上限后不再增加（默认浪费）
        EventBus.Publish(new EnergyGainedEvent { unit = this, amount = amount });
        
        // 预留：将来某些被动可以监听溢出事件
        if (CurrentEnergy >= Data.ultimateConfig.energyRequired && amount > 0)
            EventBus.Publish(new EnergyFullEvent { unit = this });
    }
    
    public void UseUltimate(Unit target = null)
    {
        if (!CanUseUltimate) return;
        
        APManager.Instance.ConsumeAP(Owner, 1);
        CurrentEnergy = 0;
        LastUltimateTurn = TurnManager.Instance.CurrentTurn;
        
        // 由 EffectResolver 解析并执行大招效果
        EffectResolver.ExecuteUltimate(this, target);
        EventBus.Publish(new UltimateUsedEvent { unit = this, target = target });
    }
}
```

### 大招效果执行（策略模式）

```csharp
public interface IUltimateEffect
{
    void Execute(Unit caster, Unit target);
}

// 示例：炎爆斩
public class FireSlashUltimate : IUltimateEffect
{
    public int baseDamage = 50;
    
    public void Execute(Unit caster, Unit target)
    {
        var damage = DamageCalculator.CalculateWithElement(
            caster.EffectiveAttack + baseDamage,
            target.EffectiveDefense,
            ElementType.Fire,
            target.AffixedElement,
            target.CurrentTile?.TerrainElement ?? ElementType.None
        );
        target.TakeDamage(damage.rawDamage, damage.element);
    }
}
```

### 新建文件清单

```
Assets/_Game/
  Server/Systems/
    UltimateEffectResolver.cs
  Server/Effects/UltimateEffects/
    IUltimateEffect.cs
    FireSlashUltimate.cs       # 示例
    ThunderStormUltimate.cs    # 示例（AOE）
    IceBarrierUltimate.cs      # 示例（护盾/控制）
```

---

## S4：棋盘道具系统

### 数据定义

```csharp
[CreateAssetMenu(fileName = "GridItem_", menuName = "Game/Grid Item")]
public class GridItemData : ScriptableObject
{
    public string itemId;
    public string itemName;
    public int price;
    public IGridItemEffect effect;  // 等你有网络层后这里改成可序列化的配置
}
```

### 道具效果接口

```csharp
public interface IGridItemEffect
{
    bool CanUse(HexCoord target, PlayerSide user, GridManager grid);
    void Execute(HexCoord target, PlayerSide user, GridManager grid);
}

// 扩展棋盘
public class ExpandTileEffect : IGridItemEffect
{
    public HexDirection direction;  // 哪个方向扩展
    public ElementType terrainElement; // 可选：附带地形元素
    
    public bool CanUse(HexCoord target, PlayerSide user, GridManager grid) { ... }
    public void Execute(HexCoord target, PlayerSide user, GridManager grid)
    {
        var newTile = grid.ExpandTile(target, direction);
        if (terrainElement != ElementType.None)
            newTile.TerrainElement = terrainElement;
    }
}

// 删除棋盘
public class RemoveTileEffect : IGridItemEffect
{
    public bool CanUse(HexCoord target, PlayerSide user, GridManager grid) { ... }
    public void Execute(HexCoord target, PlayerSide user, GridManager grid)
    {
        grid.RemoveTile(target);
    }
}

// 传送棋子（棋盘道具的另一种）
public class TeleportUnitEffect : IGridItemEffect
{
    public int maxRange = 5;
    // ...
}
```

### 新建文件清单

```
Assets/_Game/
  Data/
    GridItemData.cs
    GridItems/
      GridItem_Expand.asset
      GridItem_Remove.asset
      GridItem_Teleport.asset
      GridItem_Rotate.asset
  Server/Effects/GridItemEffects/
    IGridItemEffect.cs
    ExpandTileEffect.cs
    RemoveTileEffect.cs
    TeleportUnitEffect.cs
```

---

## S5：7棋子完整对战

### 战前流程

```
[游戏开始]
  → 选择城邦（弹窗选择 CityConfig）
  → 双方选棋子（从棋子池选7个）
  → 部署阶段（在己方半场放置棋子）
  → 第1回合开始
```

### 对局结束条件

- 一方所有棋子被消灭 → 另一方胜利
- 任意一方主动认输
- （可选）回合上限达到后按剩余HP总和判定

### TurnManager 完整状态机

```csharp
public class TurnManager : MonoBehaviour
{
    public enum Phase { Deployment, PlayerTurn, Resolution, GameOver }
    public Phase CurrentPhase { get; private set; }
    
    List<Unit> _player1Units = new();
    List<Unit> _player2Units = new();
    
    void StartBattle()
    {
        CurrentPhase = Phase.Deployment;
        // 让双方部署棋子
    }
    
    void StartPlayerTurn(PlayerSide side)
    {
        CurrentPhase = Phase.PlayerTurn;
        ActivePlayer = side;
        APManager.Instance.RefillAP(side);
        EventBus.Publish(new TurnStartEvent { turn = CurrentTurn, player = side });
    }
    
    public void EndPlayerTurn()
    {
        EventBus.Publish(new TurnEndEvent { turn = CurrentTurn });
        GoldManager.Instance.SettleTurn();
        CheckElementDurations();  // 减少元素残留回合
        CheckGameOver();
        SwitchPlayer();
    }
    
    void CheckGameOver()
    {
        if (_player1Units.All(u => u.CurrentHP <= 0))
            DeclareWinner(PlayerSide.P2);
        else if (_player2Units.All(u => u.CurrentHP <= 0))
            DeclareWinner(PlayerSide.P1);
    }
}
```

### 棋子选择UI（Phase 2仅需基础版本）

- 显示可用棋子列表（从 UnitData 池中）
- 双方各选7个（本地热座模式下先后选）
- 部署阶段：点击棋子 → 点击棋盘空白格放置

### 新建文件清单

```
Assets/_Game/
  Server/Managers/
    TurnManager.cs（重写）
    BattleSetupManager.cs   # 战前流程
  Client/UI/
    UI_UnitSelection.cs     # 棋子选择界面
    UI_Deployment.cs        # 部署界面
    UI_GameOver.cs          # 结果界面
```

---

## Phase 2 开发顺序（推荐的 sprint 划分）

### Sprint 2.1：元素城邦（最底层，先做）

- [ ] ElementType / ReactionConfig / ElementReactionTable
- [ ] Unit.ApplyElement / HexTile.TerrainElement
- [ ] 修改 DamageCalculator 加入元素反应
- [ ] 写2个棋子的 UnitData（1火1水）验证反应正确触发

### Sprint 2.2：装备系统

- [ ] EquipmentData ScriptableObject（创建5-8件装备）
- [ ] IPassiveEffect + 3个示例实现
- [ ] Unit 装备槽位 + 装备/卸载逻辑
- [ ] EquipmentInventory 背包管理
- [ ] UI_Shop + UI_EquipmentPanel

### Sprint 2.3：能量大招

- [ ] UltimateConfig + EnergyConfig
- [ ] Unit.GainEnergy / UseUltimate
- [ ] 能量条UI
- [ ] 2个示例大招效果

### Sprint 2.4：棋盘道具

- [ ] GridItemData + IGridItemEffect
- [ ] 扩展/删除/传送道具实现
- [ ] 道具商店/背包

### Sprint 2.5：完整对战集成

- [ ] 棋子选择 + 部署阶段
- [ ] 7棋子阵容的完整对局流程
- [ ] 对局结束判定
- [ ] 整套系统的集成测试

---

## Phase 2 风险清单

| 风险                   | 严重度 | 对策                                 |
| -------------------- | --- | ---------------------------------- |
| 元素反应 + 地形残留的计算复杂度    | 中   | 最多4元素，查找表O(1)，不是问题                 |
| 被动效果组合爆炸（装备A×装备B的交互） | 高   | 限制每个棋子槽位3个，减少组合；被动系统开log记录所有触发便于调试 |
| 装备池膨胀导致商店UI混乱        | 低   | 固定商店，分类标签即可                        |
| 棋盘道具过多导致策略稀释         | 低   | Phase 2 限制5种道具                     |
| 7棋子全部激活导致回合过长        | 中   | AP上限2自然限制了每回合操作量                   |

---

## 测试策略

### Phase 2 集成测试 checklist

1. **元素反应测试**：火棋子攻击水底棋子 → 验证蒸发2.0x伤害 → 验证底元素被清除
2. **装备加成测试**：装备+10攻击 → 验证 EffectiveAttack = baseAttack + 10
3. **被动触发测试**：装备"攻击回血" → 攻击后验证HP确实增加了
4. **装备切换测试**：卸下 → 回背包 → 换给别人 → 验证属性正确转移
5. **大招释放测试**：满能→释放→能量清零→冷却开始→冷却结束可再次释放
6. **棋盘扩展测试**：用扩展道具→新格子出现→可以走上去→再用删除道具→空格子消失
7. **7棋子完整对局**：选7个→部署→打满一局→验证胜负判定正确
