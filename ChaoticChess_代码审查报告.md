# Chaotic Chess — Phase 1 代码审查报告

> 审查日期：2026-08-03
> 项目路径：`D:/unity/Project/Chaotic Chess`
> 脚本数量：30 个 .cs + 3 个 .asset
> 架构模式：MVC（Model-View-Controller）

---

## 一、总体评价

**这是一份超过预期的 Phase 1 实现。** MVC 分层干净、数据流可控、事件驱动替代了每帧轮询——这些决策让项目骨架比原始 Phase 1 方案更健壮。你不仅仅"实现了功能"，而是在架构层面做了深思熟虑的改进。

按维度评分：

| 维度 | 评分 | 说明 |
|------|------|------|
| MVC 分层纯度 | A | Model 层纯 C# 无 UnityEngine 依赖，View 层只做渲染 |
| 数据驱动程度 | B+ | PieceData SO 做得好，但部分配置仍散落在 Inspector 中 |
| 事件驱动 | A- | UI 全部事件订阅，无 Update() 轮询浪费 |
| 单一职责 | B+ | 大部分类职责清晰，少数 Controller 略重 |
| Phase 1 功能覆盖 | B | 核心循环完整，缺 HealthBar 和攻击范围可视化细节 |

---

## 二、架构亮点（做对的 8 件事）

### 2.1 Model 层纯 C# — 可单元测试的基础

```
ChessBoardModel   → 无 UnityEngine 引用，纯算法
PieceModel        → 无 MonoBehaviour 继承，纯数据
TurnModel         → 纯数据 + 事件
GoldModel         → 纯数据 + 事件
APModel           → 纯数据 + 事件
BattleModel       → 纯数据 + 事件
PieceLayoutModel  → 懒加载单例，纯数据
```

这是你做得最对的一件事。这些 Model 类可以直接在 NUnit 测试里 new 出来跑，不需要 Unity Editor。

### 2.2 PieceLayoutModel — 占据关系的单一真相源

```csharp
// 所有模块通过它查询"某个坐标上有没有棋子"
ChessBoardController.IsBlocked → PieceLayoutModel.Instance.IsOccupied
BattleController 攻击检测      → PieceLayoutModel.Instance.GetPieceAt
PieceManager.MovePiece         → PieceLayoutModel.Instance.MovePiece
```

没有第二个 Dictionary 存"谁在哪"——这就消除了数据不一致的风险。

### 2.3 事件驱动的 UI 更新

```
UI_APDisplay    订阅 APModel.OnAPChanged + TurnModel.OnTurnStarted
UI_GoldDisplay  订阅 GoldModel.OnGoldSettled + TurnModel.OnTurnStarted
UI_TurnDisplay  订阅 TurnModel.OnTurnStarted + OnGameOver
BattleView      订阅 BattleModel.OnHighlightsChanged
```

**没有 Update() 里每帧读 TurnManager.ActivePlayer。** 只在事件发生时更新一次。这对性能不重要（Phase 1 完全不需要担心），但它培养了一个好习惯：UI 与数据同步靠事件，不靠轮询。

### 2.4 HexTile 的三材质处理

```csharp
// materials[0] = 顶部, [1] = 底部, [2] = 两侧
// 只换顶部，保留底部和两侧不变
var mats = _meshRenderer.materials;
mats[0] = targetMat;
_meshRenderer.materials = mats;
```

你的悬停 + 战术高亮组合逻辑（`ApplyMaterial()` 方法）也处理得干净——用 if-else 优先级链而非复杂状态机。

### 2.5 HighlightType 的 Flags 枚举

```csharp
[System.Flags]
public enum HighlightType { None=0, Move=1, Attack=2, AttackEnemy=4 }
// 支持：Move | Attack 表示重合格
// 支持：type.HasFlag(HighlightType.Move) 判断
```

用 Flags 而非独立 boolean 或 switch-case 穷举组合——简洁且可扩展。

### 2.6 GameSetup 的"未配置"哨兵值

```csharp
if (pieceDataP1.id == int.MinValue) return;  // 未配置哨兵
```

用 `int.MinValue` 而非 0 区分"未配置"和"配置了 id=0 的数据"。0 在游戏里是完全合法的棋子 ID。

### 2.7 GoldManager 的金币校验用 -1 哨兵

```csharp
// 0 是合法值，"起始金币 0"和"未配置"要能区分
if (startingGold == -1) { Debug.LogError("未配置"); return; }
```

同样的问题（0 是合法值），同样的正确解法——用 -1 做"未配置"哨兵。

### 2.8 PieceView 替代了旧 fat-object Unit

你把原来的 `Unit`（既管数据又管行为又管渲染的 200 行大杂烩）拆成了：
- `PieceData`（SO 模板数据）
- `PieceModel`（纯 C# 运行时数据）
- `PieceView`（MonoBehaviour，只管渲染）
- `PieceManager`（Controller，管行为）

拆完之后每个类能一眼看到底。这是好架构的直观指标。

---

## 三、与原始 Phase 1 方案的差异（需注意的点）

### 3.1 去除了 HasMovedThisTurn —— 这是设计级改动

**原始设计**：每个棋子每回合最多移动 1 次 + 攻击 1 次（各消耗 1 AP）。
**你的实现**：只有 `HasAttackedThisTurn`，没有移动次数限制。只要有 AP，一个棋子可以在一回合内移动多次。

这意味着：
- 棋子可以移动→攻击→再移动（Hit & Run）
- 棋子可以连续移动 2 次（Dash）

**评价**：这不是 bug，这是一个有效的设计选择。但是它改变了战术深度。Hit & Run 会让远程棋子过强，Dash 会让高移动力棋子变成"全场跑"。我建议 **playtest 后看手感再决定保留还是加回移动限制**。如果保留，至少标记为 [PLACEHOLDER · 需验证这个设计对游戏节奏的影响]。

### 3.2 棋子数值偏差

| 属性 | 原始设计 | 你的实现 |
|------|---------|---------|
| 战士 moveRange | 3 | **4** |
| 法师 moveRange | 3 | **3** (一致) |
| 战士 attackRange | 2 | **2** (一致) |
| 法师 attackRange | 2 | **2** (一致) |

战士 moveRange=4 意味着战士能覆盖更多地面。配合上面的"无移动次数限制"，战士可以在一回合内跑 8 格。这是一个可调参数，但需要注意其影响。

### 3.3 ChessBoardView 的坐标映射重复

```csharp
// ChessBoardModel 有 _allCoords (HashSet)
// ChessBoardView 有 _coordsToTiles (Dictionary)
```

这两个数据结构本质上映射同一个东西（坐标→存在性 vs 坐标→GameObject）。严格 MVC 下这是正确的——Model 不应该知道 GameObject 的存在。但要注意：如果将来棋盘有动态增删（Phase 2），需要保证两边同步更新。

### 3.4 没有世界空间血条

你没有做 World Space 的棋子头顶血条，而是用 `UI_PieceStatsDisplay`（屏幕空间叠加层）替代。用对象池管理 `UI_StatElement` 是个巧妙的优化，但玩家体验上需要视线在棋子和屏幕角落之间来回跳。

**建议**：Phase 2 加一个 World Space Canvas 血条作为可选方案，或者至少让 `UI_PieceStatsDisplay` 的偏移量更大更可见。

---

## 四、可改进的地方

### 4.1 BattleModel 缺失 HasMovedThisTurn 追踪

```csharp
// 当前 BattleModel 只有
public PieceModel SelectedPiece { get; private set; }

// 建议加入（如果要恢复移动限制）
public Dictionary<PieceModel, bool> HasMovedThisTurn; // 由 TurnManager.OnTurnStarted 重置
```

### 4.2 TurnManager.EndTurn 的结算顺序有细微问题

当前流程：
```
EndTurn → Gold.SettleTurnGold → SwitchActivePlayer → CheckGameOver → AdvanceTurn
```

问题：`Gold.SettleTurnGold` 结算的是"上一回合的金币"，但此时 `ActivePlayer` 还是上一回合的玩家。金币是在结算时归属正确的（`_pendingGold` 已经按 side 记录了），但切换玩家后才 `CheckGameOver`，这意味着在游戏结束前不会显示"新的当前玩家的金币"。

**实际影响**：没有实质性 bug，纯粹是逻辑可读性问题。建议注释说明。

### 4.3 HighlightType 缺少"已行动过"的可视化反馈

当棋子已攻击过（`HasAttackedThisTurn = true`），它应该有一个视觉差异让玩家知道"这个棋子不能再攻击了"。目前没有这个反馈。

**建议**：给已行动过的棋子加一个暗色叠加层或半透明效果。

### 4.4 棋子数据硬编码 vs SO 的混合状态

`PieceData` 用了 ScriptableObject（好），但 `GoldModel` 的金币倍率和 `APModel` 的 AP 值仍在 Inspector 中手动配。这种混合使得"全局数值表"的查找分散在两个地方。

**建议**：Phase 2 做一个 `GameConfig.asset`（ScriptableObject），把 `goldPerDamage`、`baseAPPerTurn` 等全局参数收进去。GoldManager/APManager 从 GameConfig 读取而非从 Inspector。

### 4.5 ChessBoardController 的配置校验可以统一

```csharp
// ChessBoardController.Awake 中
if (hexTilePrefab == null) { Debug.LogError(...); return; }
if (Mathf.Approximately(hexSize, 0f)) { Debug.LogError(...); return; }
```

这是好的。但 GoldManager 和 APManager 也有自己的校验——各模块校验风格不统一（有的用 -1 哨兵，有的用 0，有的用 null）。建议统一为：每个 Controller 在 Awake 里调用一次 `ValidateConfig()` 方法并自禁用。

---

## 五、Phase 1 验收清单对照

| # | 验收项 | 状态 | 备注 |
|---|--------|------|------|
| 1 | 61 个六边形棋盘可见可点击 | ✅ | ChessBoardController + HexTile |
| 2 | 棋子可见（战士+法师） | ✅ | PieceData SO + PieceView Prefab |
| 3 | 点击己方棋子显示移动/攻击范围 | ✅ | BattleController + Flags 枚举 |
| 4 | 点击空格移动棋子 | ✅ | BFS 寻路 + PieceManager.MovePiece |
| 5 | 点击敌方攻击 | ✅ | DamageCalculator + PieceManager.AttackPiece |
| 6 | 伤害 = atk - def（保底 1） | ✅ | DamageCalculator.Calculate |
| 7 | AP 扣除 + 回合恢复 | ✅ | APModel 累积制 |
| 8 | 金币结算 | ✅ | GoldModel pending + settle |
| 9 | 回合切换 | ✅ | TurnManager.EndTurn |
| 10 | 棋子死亡 + 胜负判定 | ✅ | TurnModel.CheckGameOver |
| 11 | 棋子头顶血条 | ⚠️ | 改用屏幕空间叠加层（可接受） |
| 12 | 攻击范围可视化 | ⚠️ | 有高亮但缺少"攻击范围预览" |
| 13 | Space 键结束回合 | ✅ | InputHandler 中实现 |

**Phase 1 核心功能覆盖率：约 90%。**

---

## 六、Phase 2 建议——基于你当前架构的自然演化

你的 MVC 架构已经为 Phase 2 铺好了路：

| Phase 2 系统 | 需要加的 | 已有基础 |
|-------------|---------|---------|
| 7 棋子阵容 | PieceLayoutModel 扩容 + 部署 UI | GameSetup 的 SpawnPieceById 流程可复用 |
| 装备系统 | EquipmentData SO + EquipmentModel + EquipmentManager | PieceData + PieceModel 的 SO-Model-View 模式可照搬 |
| 元素城邦 | ElementType 枚举 + ElementReactionTable + ElementalEffectManager | DamageCalculator 可扩展，APModel 的事件机制可复用 |
| 大招系统 | UltimateData SO + EnergyModel + EnergyManager | APModel 的累积制可照搬到能量 |
| 棋盘道具 | GridItemData SO + GridItemEffect + GridItemManager | ChessBoardController 的扩删接口预留 |

**你的 MVC 模式意味着每个新系统 = Model + Controller + (可选的 View)，复制模板即可。**

---

## 七、总结

**Phase 1 的 MVC 实现质量高于原方案中的简易单体架构。** 你做了四个正确的架构决策：

1. Model 层纯 C# → 可测试
2. PieceLayoutModel 单一真相源 → 数据一致
3. 事件驱动 UI → 低耦合
4. PieceData ScriptableObject → 数据可配置

**接下来最该做的三件事**：
1. 用现有代码打一局完整的 1v1，感受"无移动次数限制"是否好玩——这个设计决定必须用 playtest 验证
2. 把 GoldManager/APManager 的全局参数收进一个 `GameConfig.asset`，统一数值管理入口
3. 确认 UI_PieceStatsDisplay 的位置在屏幕上的可读性——如果体验不好，换 World Space 血条
