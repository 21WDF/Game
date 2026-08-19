---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: c1f24d71dc555efa347b566d163f258b_502e8bce918411f1a102525400826444
    ReservedCode1: YH5R6g8BHL5F8a2obUt3pu+xv7rcnbdyNh+9I83FRAy6b3CO3AjxEhs82wuJZMC5OudwNRN7MTCmOfhjMlPIe129tsJNG+m2nKJ8FNYHJ9daGR6k6sXf2nk++hbrNdrz0LSwXYyiK126uRMG4JVc62MsruWsmXyKCd1D3ppCkHCT74a2QlJ0OmAB6I4=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: c1f24d71dc555efa347b566d163f258b_502e8bce918411f1a102525400826444
    ReservedCode2: YH5R6g8BHL5F8a2obUt3pu+xv7rcnbdyNh+9I83FRAy6b3CO3AjxEhs82wuJZMC5OudwNRN7MTCmOfhjMlPIe129tsJNG+m2nKJ8FNYHJ9daGR6k6sXf2nk++hbrNdrz0LSwXYyiK126uRMG4JVc62MsruWsmXyKCd1D3ppCkHCT74a2QlJ0OmAB6I4=
---

# Chaotic Chess — 项目全景分析报告

> **生成日期**: 2026-08-06  
> **基于**: 项目实际文件结构与代码内容分析

---

## 一、项目基本信息

| 项目 | 详情 |
|------|------|
| **项目名称** | Chaotic Chess（混沌棋局） |
| **Unity 版本** | 6000.3.19f1（Unity 6 LTS） |
| **模板** | com.unity.template.urp-blank@17.1.0 |
| **渲染管线** | Universal Render Pipeline (URP) 17.3.0 |
| **色彩空间** | Linear |
| **运行时** | .NET Standard 2.1 (apiCompatibilityLevel: 6) |
| **版本号** | bundleVersion: 0.1.0 |
| **公司** | DefaultCompany |
| **产品名** | Chaotic Chess |
| **目标平台** | PC（同时配置了 Mobile 渲染器备用） |
| **项目路径** | `D:\unity\Project\Chaotic Chess` |
| **开发阶段** | 初始开发阶段（Phase 1 完整，Phase 2 部分完成） |
| **默认场景** | `Assets/Scenes/SampleScene.unity` |

---

## 二、技术栈与依赖

### 2.1 核心 Package 依赖

| Package | 版本 | 用途 |
|---------|------|------|
| com.unity.render-pipelines.universal | 17.3.0 | URP 渲染管线 |
| com.unity.inputsystem | 1.19.0 | 新输入系统（鼠标点击检测） |
| com.unity.ai.navigation | 2.0.13 | AI 导航（预留） |
| com.unity.formats.fbx | 5.1.3 | FBX 模型导入 |
| com.unity.timeline | 1.8.12 | Timeline（预留） |
| com.unity.ugui | 2.0.0 | Unity UI 系统 |
| com.unity.visualscripting | 1.9.11 | 可视化编程（预留） |

### 2.2 第三方/自定义资源

| 资源 | 说明 |
|------|------|
| Editor/Pmx2Fbx | MMD PMX 模型 → FBX 转换工具（编辑器扩展） |
| MyToonShader | 自定义卡通渲染 Shader（用于角色模型） |
| TTF/OPPOSans-R | OPPO Sans 字体（SDF） |
| TTF/YSHaoShenTi-2 | 优设好身体字体（SDF） |
| TTF/微软雅黑 | 微软雅黑字体（SDF） |
| Models/菲林斯.fbx | MMD 风格动漫角色模型（8.15MB） |
| OPPOSans-R-2 SDF.asset | 已生成的 SDF 字体资源 |
| YSHaoShenTi-2 SDF.asset | 已生成的 SDF 字体资源 |
| 微软雅黑 SDF.asset | 已生成的 SDF 字体资源 |

### 2.3 URP 配置

- 同时配置 PC 和 Mobile 两种渲染器
- 使用 `SampleSceneProfile` 场景配置文件
- `DefaultVolumeProfile` 后处理配置

---

## 三、目录结构

```
Chaotic Chess/
├── Assets/
│   ├── Editor/
│   │   └── Pmx2Fbx/               # PMX→FBX 编辑器工具
│   │       ├── FbxPackageAutoInstaller/
│   │       ├── Scripts/
│   │       │   ├── PMXData.cs
│   │       │   ├── PMXHumanoidMapper.cs
│   │       │   └── PMXParser.cs
│   │       ├── FBXWriter.cs
│   │       ├── PMX2FBXEditor.cs
│   │       └── PMX2FBXEditor.FbxBlendShape.cs
│   │
│   ├── Game/                       # ★ 核心游戏代码（MVC 架构）
│   │   ├── Core/                   # 核心基础设施
│   │   │   ├── GameConfig.cs       #   ScriptableObject 全局配置
│   │   │   ├── GameEnums.cs        #   枚举定义（PlayerSide / HighlightType / ElementType / ReactionType）
│   │   │   ├── GameFlowController.cs  # 战前流程控制（选棋子→部署→对战）
│   │   │   ├── GameSetup.cs        #   [废弃] 早期场景初始化
│   │   │   └── HexCoord.cs         #   立方坐标系统（q/r/s）与世界坐标转换
│   │   │
│   │   ├── ChessBoard/             # 棋盘模块（MVC）
│   │   │   ├── Controller/
│   │   │   │   └── ChessBoardController.cs  # 棋盘协调器（Raycast/寻路/添加/删除格子/GhostTile）
│   │   │   ├── Model/
│   │   │   │   └── ChessBoardModel.cs       # 棋盘数据（格子字典/BFS 寻路/IsBlocked 委托）
│   │   │   └── View/
│   │   │       ├── ChessBoardView.cs         # 棋盘渲染（GenerateView/CreateTile/DestroyTile/高亮）
│   │   │       └── HexTile.cs               # 单体格子 MonoBehaviour（坐标/占据/材质高亮切换）
│   │   │
│   │   ├── Piece/                  # 棋子模块（MVC + Data）
│   │   │   ├── Controller/
│   │   │   │   └── PieceManager.cs           # 棋子生命周期管理（生成/移动/传送/攻击/死亡/大招）
│   │   │   ├── Model/
│   │   │   │   ├── PieceModel.cs             # 运行时数据模型（HP/装备/元素/能量/冻结）
│   │   │   │   └── EnergyModel.cs            # 能量模型（Gain/TryConsume/满能检测）
│   │   │   ├── Data/
│   │   │   │   ├── PieceData.cs              # ScriptableObject 棋子定义（id/属性/大招配置/元素）
│   │   │   │   ├── PieceRegistry.cs          # ScriptableObject 注册表（id→PieceData）
│   │   │   │   └── *.asset                   # 9 个棋子数据资产
│   │   │   ├── Effects/                      # 大招效果策略
│   │   │   │   ├── IUltimateEffect.cs
│   │   │   │   ├── FireSlashUltimate.cs       # 炎爆斩（伤害型+元素反应）
│   │   │   │   └── IceBarrierUltimate.cs      # 冰障（自身+10防御）
│   │   │   └── View/
│   │   │       └── PieceView.cs               # 棋子视图（BindModel/SnapToPosition）
│   │   │
│   │   ├── Battle/                 # 战斗模块（MVC）
│   │   │   ├── Controller/
│   │   │   │   └── BattleController.cs       # 战斗主控制器（选棋子/高低亮/移动/攻击/部署委托/大招瞄准/道具瞄准）
│   │   │   ├── Model/
│   │   │   │   └── BattleModel.cs            # 战斗数据（SelectedPiece/Highlights 字典/事件驱动）
│   │   │   └── View/
│   │   │       └── BattleView.cs             # 战斗渲染（订阅 BattleModel 事件渲染高亮）
│   │   │
│   │   ├── Turn/                   # 回合模块（MVC）
│   │   │   ├── Controller/
│   │   │   │   └── TurnManager.cs            # 回合流转（StartFirstTurn/EndTurn/跨系统协调/元素tick/胜负判定）
│   │   │   └── Model/
│   │   │       └── TurnModel.cs              # 回合数据（CurrentTurn/ActivePlayer/双方棋子列表/事件驱动）
│   │   │
│   │   ├── ActionPoint/            # 行动点模块（MVC）
│   │   │   ├── Controller/
│   │   │   │   └── APManager.cs              # AP 单例管理器（配置注入 GameConfig）
│   │   │   └── Model/
│   │   │       └── APModel.cs                # AP 数据模型（OnAPChanged 事件）
│   │   │
│   │   ├── Gold/                   # 金币模块（MVC）
│   │   │   ├── Controller/
│   │   │   │   └── GoldManager.cs            # 金币单例管理器（伤害收入/消费/结算）
│   │   │   └── Model/
│   │   │       └── GoldModel.cs              # 金币数据模型（OnGoldSettled 事件）
│   │   │
│   │   ├── Equipment/              # 装备模块（MVC + Effects）
│   │   │   ├── Controller/
│   │   │   │   └── EquipmentManager.cs       # 装备管理器（商店/背包/3槽装备/卸载/被动工厂）
│   │   │   ├── Model/
│   │   │   │   ├── EquipmentData.cs           # ScriptableObject 装备定义（属性加成/被动配置）
│   │   │   │   └── EquipmentModel.cs          # 运行时装备模型
│   │   │   └── Effects/
│   │   │       ├── IPassiveEffect.cs          # 被动效果接口
│   │   │       └── ExtraDamagePassive.cs      # 示例被动（额外伤害，未接入结算）
│   │   │
│   │   ├── GridItem/               # 棋盘道具模块（MVC + Effects）
│   │   │   ├── Controller/
│   │   │   │   └── GridItemManager.cs         # 道具管理器（商店/购买即用/瞄准模式/效果工厂）
│   │   │   ├── Model/
│   │   │   │   └── GridItemData.cs            # ScriptableObject 道具定义（price/apCost/effectClassName）
│   │   │   └── Effects/
│   │   │       ├── IGridItemEffect.cs
│   │   │       ├── ExpandTileEffect.cs        # 扩展棋盘格子
│   │   │       ├── RemoveTileEffect.cs        # 删除棋盘格子
│   │   │       └── TeleportUnitEffect.cs      # 传送棋子
│   │   │
│   │   ├── PieceLayout/            # 棋子占据管理
│   │   │   └── Model/
│   │   │       └── PieceLayoutModel.cs        # 懒加载单例（坐标→棋子占据权威来源）
│   │   │
│   │   ├── Systems/                # 通用系统工具
│   │   │   ├── DamageCalculator.cs           # 伤害计算器（减法公式 + 元素反应）
│   │   │   └── ElementReactionTable.cs        # 元素反应查找表（6种反应/倍率/效果，部分未实现）
│   │   │
│   │   ├── Client/                 # UI 与输入
│   │   │   ├── InputHandler.cs               # 鼠标点击检测与转发（B/E/U 快捷键/面板穿透防护）
│   │   │   ├── UI_APDisplay.cs               # AP 显示 HUD
│   │   │   ├── UI_GoldDisplay.cs             # 金币显示 HUD
│   │   │   ├── UI_TurnDisplay.cs             # 回合信息 HUD
│   │   │   ├── UI_EndTurnButton.cs           # 结束回合按钮
│   │   │   ├── UI_EnergyBar.cs               # 能量条 HUD
│   │   │   ├── UI_PieceStatsDisplay.cs       # 屏幕空间数值面板（对象池+WorldToScreenPoint/N属性/元素图标/装备/状态）
│   │   │   ├── UI_StatElement.cs             # 数值元素引用容器
│   │   │   ├── UI_UnitSelection.cs           # 棋子选择界面
│   │   │   ├── UI_Deployment.cs              # 部署界面（侧边栏）
│   │   │   ├── UI_ShopPanel.cs               # 商店面板（B键，装备+道具分区）
│   │   │   ├── UI_ShopItemRefs.cs            # 商店列表项引用
│   │   │   ├── UI_EquipmentPanel.cs          # 装备面板（E键，3槽+背包）
│   │   │   ├── UI_EquipmentItemRefs.cs       # 装备列表项引用
│   │   │   ├── UI_GameOver.cs                # 游戏结束面板（订阅 OnGameOver 重载场景）
│   │   │   └── UIPanelRaycastBlocker.cs      # 面板遮挡标记（防止穿透点击）
│   │   │
│   │   └── Resources/              # 运行时 SO 资源
│   │       ├── GameConfig.asset              # 全局游戏配置（hexSize/initialRadius/baseAPPerTurn 等）
│   │       ├── Element/
│   │       │   ├── ElementIconMap.asset      # 元素图标映射表
│   │       │   └── ElementIconMap.cs
│   │       ├── State/
│   │       │   ├── StatusIconMap.asset       # 状态图标映射表
│   │       │   └── StatusIconMap.cs
│   │       ├── Equipment/
│   │       │   ├── 新手剑.asset                 # 装备资产
│   │       │   ├── 橡木盾.asset
│   │       │   ├── 火焰护符.asset
│   │       │   └── 迅捷之靴.asset
│   │       └── GridItem/
│   │           ├── 扩展石.asset                 # 棋盘道具资产
│   │           ├── 删除石.asset
│   │           └── 传送石.asset
│   │
│   ├── Models/                     # 3D 模型资源
│   │   ├── 菲林斯.fbx               # MMD 角色模型（8.15MB）
│   │   └── 菲林斯.prefab             # 角色预制体
│   │
│   ├── Prefabs/                    # 预制体
│   │   ├── HexTile.prefab          # 棋盘格子
│   │   ├── Unit_P1.prefab          # 玩家1棋子（红色）
│   │   ├── Unit_P2.prefab          # 玩家2棋子（蓝色）
│   │   ├── Unit_P3.prefab          # 第三棋子（备用）
│   │   ├── StatElement.prefab      # 棋子数值面板
│   │   ├── StatusIconPrefab.prefab # 状态图标
│   │   ├── ShopItem.prefab         # 商店物品项
│   │   ├── ShopItemRefs.prefab     # 商店物品引用
│   │   ├── ShopItemRefs_Deployment.prefab
│   │   └── backpackItem.prefab     # 背包物品项
│   │
│   ├── Scenes/
│   │   ├── SampleScene.unity       # 主场景
│   │   └── New Scene.unity         # 备用场景
│   │
│   ├── Settings/                   # URP 设置
│   │   ├── PC_RPAsset.asset
│   │   ├── PC_Renderer.asset
│   │   ├── Mobile_RPAsset.asset
│   │   ├── Mobile_Renderer.asset
│   │   ├── SampleSceneProfile.asset
│   │   ├── DefaultVolumeProfile.asset
│   │   └── UniversalRenderPipelineGlobalSettings.asset
│   │
│   └── md/                         # 设计文档
│       ├── 设计分析报告.md            # 游戏设计分析（Fun Hypothesis/Design Pillars/经济系统/架构建议）
│       ├── Phase1_完整开发手册.md     # Phase 1 逐步构建指南
│       ├── Phase1_实现方案.md        # Phase 1 实现规格
│       ├── Phase2_实现方案.md        # Phase 2 实现规格（含系统清单/优先级/Sprint划分）
│       └── P0-1_修补方案.md         # UI_StatElement 扩展方案
│
├── Packages/
│   └── manifest.json               # Package 依赖清单
├── ProjectSettings/                # Unity 项目设置
└── UserSettings/                   # 用户本地设置
```

---

## 四、核心架构与模块说明

### 4.1 架构概览

项目采用 **MVC（Model-View-Controller）** 分层架构，辅以 **ScriptableObject** 作为数据定义层。每个游戏子系统（棋盘、棋子、战斗、回合、AP、金币、装备、道具）都有独立的 Controller / Model / View 三件套，通过单例模式（`Instance` 静态属性）实现跨模块访问。

```
┌─────────────────────────────────────────────┐
│                GameFlowController             │  ← 顶层流程（选棋子→部署→对战）
├─────────────────────────────────────────────┤
│  BattleController  │  TurnManager            │  ← 战斗逻辑 + 回合流转
├─────────────────────────────────────────────┤
│  ChessBoardController  │  PieceManager      │  ← 棋盘 + 棋子生命周期
├─────────────────────────────────────────────┤
│  APManager  │  GoldManager  │  EquipmentMgr │  ← 资源管理
│  GridItemMgr│                               │
├─────────────────────────────────────────────┤
│  DamageCalculator  │  ElementReactionTable   │  ← 静态工具
├─────────────────────────────────────────────┤
│  ScriptableObject Data Layer                 │  ← 数据层（独立于场景）
│  PieceData / EquipmentData / GridItemData    │
│  GameConfig / PieceRegistry                  │
└─────────────────────────────────────────────┘
```

### 4.2 模块详细说明

#### 4.2.1 Core — 核心基础设施

| 文件 | 职责 |
|------|------|
| `HexCoord.cs` | 平顶六边形立方坐标（q/r/s），含邻居、距离、半径范围生成、世界坐标转换 |
| `GameEnums.cs` | 全局枚举：`PlayerSide`(P1/P2)、`HighlightType`(Flags: Move\|Attack)、`ElementType`(None/Fire/Water/Thunder/Ice)、`ReactionType`(6种反应) |
| `GameConfig.cs` | ScriptableObject 全局配置中心（hexSize=1.25, initialRadius=4, baseAPPerTurn=2, maxAPCap=4, goldPerDamage=1, startingGold=50） |
| `GameFlowController.cs` | 战前流程状态机：P1选棋子→P1部署→P2选棋子→P2部署→对战开始。半场划分：P1=r>=0, P2=r<0 |
| `GameSetup.cs` | 废弃，早期自动生成逻辑已由 PieceManager 取代 |

#### 4.2.2 ChessBoard — 棋盘系统

```
ChessBoardController (单例) 
  ├── ChessBoardModel  (格子字典 / BFS寻路 / BFS移动范围 / BFS攻击范围 / IsBlocked委托)
  └── ChessBoardView   (GenerateView / CreateTile / DestroyTile / 高亮)
       └── HexTile     (MonoBehaviour: Coord / Occupant / SetHighlight / 材质替换)
```

- **核心功能**：生成平顶六边形棋盘（初始 61 格）、射线检测格子点击、BFS 移动范围/攻击范围/寻路、动态添加/删除格子、幽灵格预览（扩展石道具）
- **高亮系统**：使用 Flags 枚举支持复合高亮（Move | Attack），HexTile 内部替换 `materials[0]`（顶部材质）

#### 4.2.3 Piece — 棋子系统

```
PieceManager (单例)
  ├── PieceData      (ScriptableObject: id/displayName/maxHP/attack/defense/moveRange/attackRange/
  │                     innateElement/UltimateConfig/prefab/EnergyConfig)
  ├── PieceRegistry  (ScriptableObject: id→PieceData 映射)
  ├── PieceModel     (运行时纯数据: HP/装备聚合/元素状态/EnergyModel/冻结/便捷属性 Effective*)
  ├── PieceView      (轻量视图: BindModel/SnapToPosition)
  ├── EnergyModel    (纯数据: Gain/TryConsume/IsFull/OnEnergyChanged)
  │
  └── IUltimateEffect  (策略接口)
       ├── FireSlashUltimate  (伤害+元素反应)
       └── IceBarrierUltimate (自身防御+10)
```

- **设计特点**：棋子用 `PieceData` ScriptableObject 定义，`PieceRegistry` 统一管理。`PieceModel` 是运行时纯数据（不继承 MonoBehaviour），通过 `PieceView` 与场景中的 GameObject 绑定。装备加成、元素状态、能量均在 Model 层聚合。

**已有棋子（9个）**：

| id | 名称 | 推测类型 |
|----|------|----------|
| ? | WarriorPieceData | 战士 |
| ? | KnightPieceData | 骑士 |
| ? | ArcherPieceData | 弓手 |
| ? | MagePieceData | 法师 |
| ? | AssassinPieceData | 刺客 |
| ? | HeavyPieceData | 重装 |
| ? | PriestPieceData | 牧师 |
| ? | RangerPieceData | 游侠 |
| ? | WarlockPieceData | 术士 |

#### 4.2.4 Battle — 战斗系统

```
BattleController (单例)
  ├── BattleModel  (SelectedPiece / Highlights 字典 / OnSelectionChanged / OnHighlightsChanged)
  └── BattleView   (订阅 BattleModel 渲染高亮)
```

- **核心流程**：
  1. 点击己方棋子 → `SelectUnit` → 显示移动范围（蓝色）+ 攻击范围（敌方红/普通红）
  2. 点击高亮空格 → `HandleMove` → 消耗 1AP → `PieceManager.MovePiece`
  3. 点击高亮敌方 → `HandleAttack` → 消耗 1AP → `PieceManager.AttackPiece`
  4. 移动后可刷新攻击范围高亮
  5. **部署模式**：检测 `GameFlowController.IsDeploying` → 委托 `HandleDeployClick` 放置棋子
  6. **大招瞄准模式**：U 键 → 进入瞄准 → 点击任意目标格执行
  7. **道具瞄准模式**：`GridItemManager.IsTargeting` → 委托点击

#### 4.2.5 Turn — 回合系统

```
TurnManager (单例)
  └── TurnModel  (CurrentTurn / ActivePlayer / 双方棋子列表 / CheckGameOver / 事件驱动)
```

- **回合流程**：`StartFirstTurn` → 每回合 `StartNewTurn`：
  1. 结算上回合金币
  2. 补 AP（当前行动方）
  3. 重置棋子行动标记
  4. 清除高亮
  5. 元素 tick（递减附着元素/重置减防/重置临时防御/解冻）
  6. 检查胜负

#### 4.2.6 ActionPoint — 行动点系统

- `APManager` 单例：持有 `APModel`（按 PlayerSide 存储），配置从 `GameConfig` 注入
- 每回合恢复 baseAPPerTurn（2），上限 maxAPCap（4），溢出浪费，不滚存

#### 4.2.7 Gold — 金币系统

- `GoldManager` 单例：持有 `GoldModel`
- **收入**：造成伤害 × 1.0 + 受到伤害 × 0.3（回合结束时结算）
- **消费**：购买装备、棋盘道具（实时扣费）
- **初始**：50 金币

#### 4.2.8 Equipment — 装备系统

```
EquipmentManager (单例)
  ├── EquipmentData  (ScriptableObject: bonusHP/Attack/Defense/MoveRange/AttackRange + price + passives)
  ├── EquipmentModel (运行时，持有 Data + 被动实例列表)
  └── IPassiveEffect (接口)
       └── ExtraDamagePassive (示例，未接入结算)
```

- **3 槽位**：每个棋子 `EquippedItems[3]`，装备/卸下时触发被动 OnEquip/OnUnequip
- **商店**：B 键打开 `UI_ShopPanel`，分类展示装备+道具
- **已配置装备**：新手剑、橡木盾、火焰护符、迅捷之靴

#### 4.2.9 GridItem — 棋盘道具系统

```
GridItemManager (单例)
  ├── GridItemData  (ScriptableObject: price/apCost/effectClassName/requiresSelectedPiece)
  └── IGridItemEffect (接口)
       ├── ExpandTileEffect  (扩展格子 + 幽灵格预览)
       ├── RemoveTileEffect  (删除格子 + 红色高亮预览)
       └── TeleportUnitEffect (传送棋子 + 需选中棋子 + 蓝色落点预览)
```

- **购买即用流程**：扣金币 → 进入瞄准模式 → 命中目标扣 AP + 执行效果 → 取消退还金币（不扣 AP）
- **已配置道具**：扩展石、删除石、传送石

#### 4.2.10 Systems — 通用计算

| 文件 | 职责 |
|------|------|
| `DamageCalculator.cs` | 减法公式 `Mathf.Max(1, attack - defense)` + 元素反应参数处理 |
| `ElementReactionTable.cs` | 6 种反应的静态查找表（蒸发 1.5x/2.0x、融化 2.0x/1.5x、超载 1.0x+额外、感电 0.5x+DoT、冻结 0.0x+控制、超导 0.8x+减防），Dot/减防字段部分未实现 |

#### 4.2.11 Client — UI 与输入

- `InputHandler`：鼠标左键检测 → `OnTileClicked` 事件转发；B(商店)/E(装备)/U(大招) 快捷键；`IsAnyPanelOpen` 预判防止面板穿透
- HUD 系列：`UI_APDisplay`、`UI_GoldDisplay`、`UI_TurnDisplay`、`UI_EndTurnButton`、`UI_EnergyBar`
- `UI_PieceStatsDisplay`：屏幕空间数值跟随面板（对象池 + WorldToScreenPoint），显示攻/血/防/移/射/能量/元素/装备/状态
- `UI_UnitSelection` / `UI_Deployment`：战前选择与部署
- `UI_ShopPanel` / `UI_EquipmentPanel`：商店与装备面板
- `UI_GameOver`：订阅 OnGameOver 事件

---

## 五、游戏玩法设计

### 5.1 核心概念

Chaotic Chess 是一款 **本地双人热座（Hot-Seat）Hex 战棋游戏**。双方在六边形棋盘上操控多个棋子进行回合制对决，核心乐趣在于"有限 AP 资源下的多棋子激活取舍 + 动态棋盘塑造 + 元素反应差异化对局"。

### 5.2 Design Pillars（5条）

1. **每回合都是新的资源分配难题** — 7 个棋子分 2 点 AP，激活谁、牺牲谁
2. **战场由玩家共同塑造** — 棋盘不是给定的，是博弈出来的（扩展/删除格子）
3. **城邦效果让每一局都不像上一局** — 相同棋子在不同城邦下有完全不同的打法
4. **装备 = 战术扩展，不是数值堆砌** — 被动效果改变行为模式
5. **信息完全透明，决策深度来自排列组合** — 无战争迷雾，胜负取决于资源调度

### 5.3 核心循环

```
[回合开始]
  → 结算上回合金币
  → 补充 AP（当前行动方，+2，上限 4）
  → [玩家行动阶段]
      ├─ 移动棋子（-1 AP）
      ├─ 攻击（-1 AP）
      ├─ 购买装备（不耗 AP）
      ├─ 装备棋子（不耗 AP，限己方回合）
      ├─ 使用道具（不耗 AP，限己方回合，命中时 -1 AP）
      └─ 释放大招（-1 AP，需满能）
  → AP 归零 或 主动结束回合
  → [金币结算]（伤害收入 + 受击收入）
  → [回合结束] → 切换行动方
```

### 5.4 游戏流程（完整对战）

```
游戏开始
  → P1 选棋子（从棋子池选 7 个）
  → P1 部署（在己方半场 r>=0 放置棋子）
  → P2 选棋子
  → P2 部署（在敌方半场 r<0 放置棋子）
  → 第 1 回合开始（P1 先手）
  → 轮流对战...
  → 一方棋子全灭 → 另一方获胜
```

### 5.5 核心数值体系

| 参数 | 值 |
|------|-----|
| 棋盘 | 六边形，初始半径 4（61 格） |
| 棋子数 | 每方可选 7 个 |
| AP 恢复 | 每回合 +2 |
| AP 上限 | 4（溢出浪费） |
| 伤害公式 | atk - def（保底 1 点）— 减法公式 |
| 元素反应 | 6 种（蒸发/融化/超载/感电/冻结/超导） |
| 金币收入 | 造成伤害 × 1.0 + 受到伤害 × 0.3 |
| 初始金币 | 50 |
| 装备槽位 | 3 个/棋子 |
| 棋盘道具 | 3 种（扩展石/删除石/传送石） |

### 5.6 元素系统

```
元素类型：Fire / Water / Thunder / Ice

反应规则（底元素 × 触发元素）：
  火底：[水→蒸发1.5x] [雷→超载1.0x+额外] [冰→融化2.0x]
  水底：[火→蒸发2.0x] [雷→感电0.5x+DoT] [冰→冻结0.0x+控制]
  雷底：[火→超载1.0x] [水→感电0.5x]   [冰→超导0.8x+减防]
  冰底：[火→融化1.5x] [水→冻结0.0x]   [雷→超导0.8x]
```

### 5.7 经济系统

- **Sources**：造成伤害（1:1）、受到伤害（1:0.3）、城邦效果（预留）、初始 50
- **Sinks**：购买装备、购买棋盘道具
- **通胀风险**：装备属性加成控制在 +1 ~ +5，防止伤害与金币收入线性增长崩盘

---

## 六、代码规范与约定

### 6.1 命名约定

| 类型 | 规范 | 示例 |
|------|------|------|
| 枚举 | PascalCase | `PlayerSide.P1`、`ElementType.Fire` |
| 类 | PascalCase | `PieceManager`、`BattleController` |
| 公共方法 | PascalCase | `SpawnPieceById()`、`MovePiece()` |
| 私有字段 | _camelCase | `_targeting`、`_selectedUnit` |
| 公共属性 | PascalCase | `IsTargeting`、`ActivePlayer` |
| 常量 | PascalCase | `MaxEquipmentSlots` |
| UI 脚本 | `UI_` 前缀 | `UI_ShopPanel`、`UI_APDisplay` |
| ScriptableObject asset | 中文名 | `新手剑.asset`、`扩展石.asset` |
| 设计文档 | 中文名 | `Phase1_完整开发手册.md` |

### 6.2 架构约定

1. **单例模式**：所有 Controller 使用 `public static ... Instance` + Awake 重复检查实现单例
2. **MVC 分层**：Controller（逻辑协调）/ Model（纯数据+事件）/ View（渲染订阅）
3. **ScriptableObject 数据**：棋子、装备、道具均用 SO 定义，`PieceRegistry` 统一索引
4. **事件驱动**：Model 层通过 `System.Action` 事件通知 View 刷新（如 `OnEquipmentChanged`、`OnGoldSettled`）
5. **策略模式**：大招（`IUltimateEffect`）、道具（`IGridItemEffect`）、被动（`IPassiveEffect`）均使用接口+工厂
6. **Resources 加载**：`GameConfig`、`ElementIconMap`、`StatusIconMap` 等全局 SO 通过 `Resources.Load` 加载

### 6.3 文件组织

- 每个模块独立文件夹：`Assets/Game/<Module>/Controller|Model|View|Data|Effects/`
- 设计文档放在 `Assets/md/`（非标准做法，建议后续移到项目根目录或 Docs 文件夹）
- Scene 单独放在 `Assets/Scenes/`
- 预制体集中在 `Assets/Prefabs/`

---

## 七、当前开发进度评估

### 7.1 已实现功能（Phase 1 + Phase 2 部分）

| 功能模块 | 状态 | 完成度 |
|----------|------|--------|
| 六边形棋盘生成 | ✅ 完成 | 100% |
| 立方坐标系统 | ✅ 完成 | 100% |
| BFS 移动/攻击范围/寻路 | ✅ 完成 | 100% |
| 棋子生成与生命周期 | ✅ 完成 | 100% |
| 移动/攻击核心循环 | ✅ 完成 | 100% |
| 伤害计算（减法公式） | ✅ 完成 | 100% |
| AP 系统 | ✅ 完成 | 100% |
| 金币系统 | ✅ 完成 | 100% |
| 回合流转 | ✅ 完成 | 100% |
| 棋子选择与部署 | ✅ 完成 | 100% |
| 9 个棋子数据 | ✅ 完成 | 100% |
| HUD（AP/金币/回合/结束按钮） | ✅ 完成 | 100% |
| 装备系统（3槽+背包+商店） | ✅ 完成 | 100% |
| 棋盘道具（扩展/删除/传送） | ✅ 完成 | 100% |
| 元素系统（附着/反应/查找表） | ✅ 部分完成 | ~80% |
| 大招系统（能量+效果） | ✅ 完成 | 90% |
| UI_PieceStatsDisplay（多属性面板） | ✅ 完成 | 90% |
| 游戏结束判定 | ✅ 完成 | 100% |
| 鼠标输入/面板穿透防护 | ✅ 完成 | 100% |
| 快捷键（B/E/U） | ✅ 完成 | 100% |

### 7.2 未完成/待完善项

| 项目 | 优先级 | 说明 |
|------|--------|------|
| 元素反应副作用（DoT/超载击退/冻结回合限制/超导减防持续） | P0 | `ElementReactionTable` 中已定义字段但未接入伤害结算管道 |
| 被动效果接入伤害结算 | P1 | `IPassiveEffect` 接口已有，`ExtraDamagePassive` 示例存在但未与 `DamageCalculator` 集成 |
| 城邦系统（CityConfig） | P2 | 设计文档提及但未实现 |
| 网络对战 | P2 | 设计文档提及但 Phase 1 刻意不做 |
| 动画系统（移动/攻击/死亡） | P2 | 当前所有移动为瞬移（SnapToPosition），无 DOTween/Animator |
| AI 对手 | P3 | 仅预留 AI Navigation package |
| 音效系统 | P3 | 未配置 |
| 棋子模型替换 | P2 | 当前使用方块（Cube）占位，仅 菲林斯 为完整角色模型 |
| P0-1 修补方案实施 | P0 | UI_StatElement 扩展方案已设计但需在 Editor 中实施 |

### 7.3 风险与债务

1. **技术债务**：部分早期代码（如 `GameSetup.cs`）已废弃但未删除
2. **元素反应**：查找表已定义但副作用（DoT/控制/击退）未全部实现
3. **被动系统**：接口存在但工厂+事件总线未完善
4. **棋子模型**：仅 1 个完整角色模型，其余 8 种棋子使用 Unity Cube 占位
5. **中文资源名**：asset 文件使用中文命名（新手剑、扩展石等），在某些 Unity 版本可能引发路径问题
6. **设计文档位置**：设计文档放置在 `Assets/md/`，非标准做法，建议外移

### 7.4 总体评估

项目处于 **Phase 2 中后期**，核心战棋玩法循环已完整可运行（选棋子→部署→移动→攻击→回合流转→胜负判定），装备系统、棋盘道具系统、大招系统均已具备基本功能。元素反应系统已定义完整的数据表但副作用执行管道还需完善。项目架构清晰，MVC 分层严格，代码质量较高。

**预计达到可试玩的内部 Alpha 版本**：完成 P0/P1 待完善项后约 1-2 周。

---

*本报告基于对 `D:\unity\Project\Chaotic Chess` 项目全部可读文件的穷尽分析生成。*
*（内容由AI生成，仅供参考）*
