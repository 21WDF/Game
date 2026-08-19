# Phase 2 Sprint 审查报告 — 2026-08-04

> 已完成：S2.1（元素城邦）+ S2.2（装备系统）
> 待执行：S2.3（大招系统）、S2.4（棋盘道具）、S2.5（7棋子对战）

---

## 已完成系统的关键实现细节（影响后续 Sprint）

### S2.1 元素城邦 — 已完工

| 组件 | 路径 | 关键内容 |
|------|------|---------|
| ElementType / ReactionType | GameEnums.cs | 4 元素 + 6 反应 |
| ElementReactionTable | Systems/ElementReactionTable.cs | 12 条规则，ReactionConfig struct |
| DamageCalculator.CalculateWithElement | 修改 | 返回 (int, ReactionConfig)，含 DefenseReduction |
| PieceData.innateElement | 修改 | ElementType 字段 |
| PieceModel 元素状态 | 修改 | AffixedElement / AffixedElementDuration / CurrentDefenseReduction |
| PieceManager.AttackPiece | 修改 | 调用 CalculateWithElement，附着逻辑：攻击方有元素才附着，无元素不改变目标 |
| TurnManager.TickElementDuration | 修改 | 每回合递减 Duration，重置 CurrentDefenseReduction |

**影响后续**：大招伤害计算应该也走 `CalculateWithElement`（如果你允许大招带元素类型）。

### S2.2 装备系统 — 已完工

| 组件 | 路径 | 关键内容 |
|------|------|---------|
| EquipmentData | Equipment/Model/ | SO，含 PassiveConfig（className + jsonParams） |
| EquipmentModel | Equipment/Model/ | 纯 C#，持 EquipmentData + ActivePassives |
| EquipmentManager | Equipment/Controller/ | 单例，BuyEquipment / EquipToPiece / UnequipFromPiece |
| IPassiveEffect | Equipment/Effects/ | OnEquip / OnUnequip 接口 |
| ExtraDamagePassive | Equipment/Effects/ | 示例被动 |
| PieceModel 装备 | 修改 | EquippedItems[3]，Equipment* 计算属性 |
| EffectiveAttack/Defense/MoveRange/AttackRange | 修改 | **现在全部含装备加成** |
| UI_ShopPanel | Client/ | B 键开关，Prefab 驱动列表 |
| UI_EquipmentPanel | Client/ | **常驻显示**（无开关） |
| UI_ShopItemRefs | Client/ | 引用容器 |
| UI_EquipmentItemRefs | Client/ | 引用容器 |
| InputHandler | 修改 | **新增 IsAnyPanelOpen 静态字段**，B 键触发 OnShopToggle |
| 4 件装备 .asset | Equipment/Data/ | 新手剑/橡木盾/火焰护符/迅捷之靴 |

**影响后续的关键点**：

1. `PieceModel.EffectiveAttack` 现在含装备加成——**S2.3 大招伤害计算应使用 EffectiveAttack，不是 Data.attack**
2. `InputHandler.IsAnyPanelOpen` 是新增的 UI 穿透防护——**S2.3/2.4/2.5 所有新 UI 面板都必须设这个标记**
3. `UI_EquipmentPanel` 目前常驻显示，没有开关——**S2.3 做大招 UI 时需要用同样模式，或给装备面板也加开关**
4. 被动效果目前只是演示（OnEquip/OnUnequip 为空），实际属性通过 Effective* 属性计算

---

## Sprint 2.3：大招系统 — 重新审查

### 与原始规格的偏差

| 原始规格 | 修正 |
|---------|------|
| `caster.EffectiveAttack + bonusDamage` | ✅ 正确——EffectiveAttack 现在已含装备加成 |
| `caster.EffectiveDefense` | ✅ 正确——EffectiveDefense 已含装备 - 元素减防 |
| U 键触发大招 | 需在 InputHandler 和 BattleController 中新增 |
| 能量条 UI | 需新增 `UI_EnergyBar`，模式参照 `UI_APDisplay`（订阅事件不轮询） |
| `PieceData.UltimateConfig` | PieceData 当前没有这个字段，需追加 |

### 更新后的 Sprint 2.3 提示词

```
在 Chaotic Chess 项目中实现大招/能量系统。项目当前已拥有元素城邦和装备系统。

## 现状确认
- PieceModel.EffectiveAttack 已含装备加成（Data.attack + EquipmentAttack）
- PieceModel.EffectiveDefense 已含装备+元素减防（Data.defense + EquipmentDefense - CurrentDefenseReduction）
- InputHandler 有 public static bool IsAnyPanelOpen（UI面板打开时阻止棋盘点击）
- 事件驱动 UI 模式参照 UI_APDisplay（订阅 Model 事件，不 Update 轮询）

## 1. 修改 PieceData.cs
追加内部类 UltimateConfig：
- string ultimateName
- int energyRequired = 100
- int energyPerMove = 5
- int energyPerAttack = 15
- string effectClassName（如 "FireSlashUltimate"）
- string effectJsonParams（如 {"bonusDamage":50}）
在 PieceData 末尾加：public UltimateConfig ultimateConfig;

## 2. 新建 EnergyModel.cs（路径：Assets/Game/Piece/Model/）
纯 C# 类，参照 GoldModel 写法：
- int MaxEnergy（构造参数），int CurrentEnergy
- bool IsFull => CurrentEnergy >= MaxEnergy
- Gain(int amount)：不超过 MaxEnergy
- TryConsume()：满能时消耗，返回 bool
- event Action<int> OnEnergyChanged

## 3. 修改 PieceModel.cs
构造函数中：Energy = new EnergyModel(Data.ultimateConfig?.energyRequired ?? 100);
追加属性：EnergyModel Energy，bool CanUseUltimate => Energy != null && Energy.IsFull

## 4. 修改 PieceManager.cs
MovePiece 末尾：model.Energy?.Gain(model.Data.ultimateConfig?.energyPerMove ?? 0);
AttackPiece 末尾：attacker.Energy?.Gain(attacker.Data.ultimateConfig?.energyPerAttack ?? 0);
新增方法 UseUltimate(PieceModel caster, PieceModel target)：
- if (caster.Energy == null || !caster.Energy.TryConsume()) return;
- if (!APManager.Instance.HasAP(caster.Owner, 1)) return;
- APManager.Instance.ConsumeAP(caster.Owner, 1);
- 反射/工厂创建 IUltimateEffect 并 Execute
- Debug 输出大招释放日志

## 5. 新建 IUltimateEffect.cs（路径：Assets/Game/Piece/Effects/）
接口：void Execute(PieceModel caster, PieceModel target);

## 6. 新建 FireSlashUltimate.cs（同上目录）
构造函数接收 int bonusDamage
Execute：调用 DamageCalculator.CalculateWithElement(caster.EffectiveAttack + bonusDamage, target.EffectiveDefense, caster.Data.innateElement, target.AffixedElement)
然后 target.TakeDamage(伤害)

## 7. 新建 IceBarrierUltimate.cs（同上目录）
Execute：caster.CurrentDefenseReduction -= 10（临时增加10防御，下回合重置）

## 8. 修改 BattleController.cs
在选中棋子后增加 U 键检测：
if (Keyboard.current.uKey.wasPressedThisFrame && selectedPiece.CanUseUltimate)
    HandleUltimate(selectedPiece, 需要指定目标：如果攻击范围内恰好一个敌人则自动选，否则提示选目标)
大招消耗 AP 后刷新选择状态

## 9. 新建 UI_EnergyBar.cs（路径：Assets/Game/Client/）
订阅 BattleModel.OnSelectionChanged → 切换监听目标
监听 PieceModel.Energy.OnEnergyChanged → 更新 UI
显示格式："能量: 75/100"
默认隐藏，选中棋子时显示
注意：当 IsAnyPanelOpen 为 true 时（商店打开），不显示能量条

## 10. 更新 WarriorPieceData.asset 和 MagePieceData.asset
Warrior：加 ultimateConfig（energyRequired=100, effectClassName="FireSlashUltimate", jsonParams={"bonusDamage":50}）
Mage：加 ultimateConfig（energyRequired=100, effectClassName="IceBarrierUltimate", jsonParams={}）

## 验收
- [ ] 战士移动后能量+5，攻击后+15
- [ ] 满100能量后选中棋子时显示"可释放"
- [ ] 按U释放大火招，能量清零+AP-1
- [ ] 大招伤害走元素反应（如果攻击方有元素）
- [ ] 冰障大招增加防御，下回合重置
- [ ] 商店打开时能量条隐藏
```

---

## Sprint 2.4：棋盘道具 — 重新审查

### 与原始规格的偏差

| 原始规格 | 修正 |
|---------|------|
| 棋盘道具也用 EquipmentManager 的商店 | 道具和装备是不同的商店——建议单独 GridItemManager 或加入 EquipmentManager 的 availableEquipment 列表 |
| ExpandTile 在 ChessBoardController | ✅ 正确，但 ChessBoardModel._allCoords 需要确认是 HashSet 还是其他类型 |
| RemoveTile 检查 PieceLayoutModel | ✅ IsOccupied 已存在 |

### 更新后的 Sprint 2.4 提示词

```
在 Chaotic Chess 项目中实现棋盘道具系统。

## 现状确认
- ChessBoardController 管理棋盘生成和查询
- ChessBoardModel 有 _allCoords（HashSet<HexCoord>）——需确认
- ChessBoardView 有 CreateTile 和 DestroyTile（或等效方法）——需确认
- PieceLayoutModel.IsOccupied(coord) 可查询格子占用

## 1. 新建 GridItemData.cs（路径：Assets/Game/GridItem/Model/）
ScriptableObject，[CreateAssetMenu(menuName = "Chess/Grid Item")]
字段：int id, string displayName, int price, string effectClassName, string effectJsonParams

## 2. 新建 IGridItemEffect.cs（路径：Assets/Game/GridItem/Effects/）
接口：bool CanExecute(HexCoord target, PlayerSide user); void Execute(HexCoord target, PlayerSide user);

## 3. 新建 ExpandTileEffect.cs
CanExecute：target 在棋盘内，某方向邻居不在棋盘内
Execute：调用 ChessBoardController 新增的 ExpandTile 方法
jsonParams：{"directionIndex":0}（方向通过 Editor 配置，或玩家点击时选择）

## 4. 新建 RemoveTileEffect.cs
CanExecute：target 存在、无棋子、非最后 N 个格子（防止棋盘被删光）
Execute：调用 ChessBoardController 新增的 RemoveTile 方法

## 5. 新建 TeleportUnitEffect.cs
CanExecute：target 存在、为空格、在 from 坐标 maxRange 内
Execute：将选中棋子传送到 target

## 6. 修改 ChessBoardModel.cs
确认 _allCoords 字段类型，追加：
public void AddCoord(HexCoord coord) => _allCoords.Add(coord);
public void RemoveCoord(HexCoord coord) => _allCoords.Remove(coord);
public int CoordCount => _allCoords.Count;

## 7. 修改 ChessBoardView.cs
追加或确认方法：
public HexTile CreateTile(HexCoord coord)（Instantiate 新的 HexTile 并注册到 _coordsToTiles）
public void DestroyTile(HexCoord coord)（从 _coordsToTiles 移除并 Destroy GameObject）

## 8. 修改 ChessBoardController.cs
追加方法：
public bool ExpandTile(HexCoord at, int directionIndex)
public bool RemoveTile(HexCoord coord)
参照现有 GenerateBoard 的风格，同步操作 Model + View

## 9. 道具商店集成
棋盘道具可以加入 EquipmentManager 的 availableEquipment 列表一起卖（在 Inspector 中拖入），或者在 UI_ShopPanel 旁加单独的道具面板。先最小实现：直接加到 EquipmentManager.availableEquipment 中。

## 10. 创建 3 个 GridItemData.asset
扩展石：20金币，ExpandTileEffect
删除石：15金币，RemoveTileEffect  
传送石：30金币，TeleportUnitEffect，jsonParams={"maxRange":5}

## 验收
- [ ] 买扩展石→点击空格相邻方向→新格子出现且可走
- [ ] 有棋子的格子不能用删除石
- [ ] 传送棋子到范围空格
- [ ] 棋盘不能删到只剩边界（防止软锁）
```

---

## Sprint 2.5：7 棋子对战 — 重新审查

### 与原始规格的偏差

| 原始规格 | 修正 |
|---------|------|
| GameSetup 硬编码 3 棋子 | 当前 P1 2 棋子 + P2 1 棋子，需重构为可变数量 |
| 棋子池只有 Warrior 和 Mage | 至少需要 7 种才能凑够 7 选阵容 |
| TurnModel.CheckGameOver | 当前检查 Player1Pieces/Player2Pieces.Count==0，支持全灭，**无需改** |
| 战前流程（选择→部署→对战） | 需新增 UI_UnitSelection + UI_Deployment |

### 当前 GameSetup 的硬编码问题

必须确认 GameSetup 当前代码结构——它可能在 Inspector 里暴露多个槽位，也可能用数组。如果是数组，重构量小；如果是单个字段，需要改成数组。

### 更新后的 Sprint 2.5 提示词

```
在 Chaotic Chess 项目中实现 7 棋子完整对战。

## 现状确认
- GameSetup 当前可能硬编码了 2-3 个棋子的 SpawnPieceById 调用（需读取确认）
- PieceRegistry 现有 Warrior + Mage 两种棋子
- TurnModel.CheckGameOver 已检查双方棋子全灭，无需修改
- BattleController 选中棋子逻辑已支持，无需修改
- 所有 UI 面板必须设 InputHandler.IsAnyPanelOpen = true 防止穿透

## 1. 扩建棋子池
创建以下 PieceData.asset（在 Editor 中右键 Create→Chess→Piece Data）：

| id | 名称 | HP | 攻 | 防 | 移 | 射 | 元素 | 能量 | 大招类名 |
|----|------|----|----|----|----|----|------|------|---------|
| 1 | 战士兵 | 100 | 30 | 10 | 4 | 2 | Fire | 100 | FireSlashUltimate |
| 2 | 法师 | 80 | 35 | 10 | 3 | 2 | Ice | 100 | IceBarrierUltimate |
| 3 | 弓手 | 70 | 25 | 5 | 3 | 4 | Thunder | 100 | 空 |
| 4 | 骑士 | 120 | 20 | 15 | 3 | 1 | Water | 100 | 空 |
| 5 | 刺客 | 60 | 40 | 3 | 5 | 1 | Fire | 100 | 空 |
| 6 | 牧师 | 75 | 15 | 8 | 3 | 3 | Water | 80 | 空 |
| 7 | 重装兵 | 150 | 18 | 20 | 2 | 1 | Ice | 120 | 空 |
| 8 | 术士 | 65 | 30 | 5 | 3 | 3 | Thunder | 100 | 空 |
| 9 | 游侠 | 85 | 22 | 8 | 4 | 3 | Fire | 100 | 空 |

大招类名为空的不放大招（PieceData.ultimateConfig 留空），先让系统兼容"无大招棋子"。

## 2. 修改 GameSetup.cs
将硬编码的单个棋子字段改为数组或列表：
- public PieceData[] p1Pieces（拖 7 个 PieceData.asset）
- public HexCoord[] p1Coords（7 个起始坐标）
- 同理 p2Pieces / p2Coords
- Start 中循环 SpawnPieceById

## 3. 新建 UI_UnitSelection.cs（路径：Assets/Game/Client/）
- 从 PieceRegistry 读取全棋子列表
- 显示棋子名称+属性+元素图标
- 点击选中/取消（最多 7 个）
- 确认按钮→切换到部署阶段
- 面板显示时设 InputHandler.IsAnyPanelOpen = true，关闭时恢复

## 4. 新建 UI_Deployment.cs
- 显示己方选中的 7 个棋子缩略图
- 点击棋子→棋盘高亮可部署区域（己方半场空格）
- 点击空格→放置棋子
- 7 个全放完→确认部署→切换对手部署（P2 选棋子→P2 部署）
- 双方完成→关闭 UI →开始对战

## 5. 新建 UI_GameOver.cs
- 订阅 TurnModel.OnGameOver 事件
- 显示 "玩家X 获胜！"
- "再来一局" 按钮→SceneManager.LoadScene 重新加载

## 6. 战前完整流程
P1选棋子→P1部署→P2选棋子→P2部署→TurnManager.StartFirstTurn

## 验收
- [ ] 棋子选择界面可用，从 9 个中选 7 个
- [ ] 部署阶段在己方半场放 7 个棋子
- [ ] 全部棋子可独立移动/攻击
- [ ] 7 棋子全灭→游戏结束界面
- [ ] 无大招棋子不报错
```

---

## Sprint 间的依赖已解决的

| 依赖 | 状态 |
|------|------|
| S2.3 依赖 EffectiveAttack 含装备 | ✅ 已满足 |
| S2.3 依赖元素反应 | ✅ 已满足 |
| S2.4 依赖 ChessBoardController | ✅ 需确认 View 的 CreateTile/DestroyTile |
| S2.5 依赖 TurnModel.CheckGameOver | ✅ 已满足 |
| S2.5 依赖全棋子池 | ⚠️ S2.3/2.4 不依赖棋子池，可单独做；但 S2.5 需要 9 枚棋子 |
