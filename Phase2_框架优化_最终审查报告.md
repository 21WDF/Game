# Phase 2 框架优化 — 最终审查报告

> 审查时间：2026-08-15
> 项目路径：`D:/unity/Project/Chaotic Chess`
> 架构：Unity 6 + Hex 战棋 + MVC

---

## 一、项目规模

| 维度 | 数量 |
|------|------|
| `.cs` 源文件 | **83 个** |
| `.asset`（ScriptableObject） | **64 个** |
| `.prefab` | **18 个** |
| MVC 模块目录 | 14+ 个 |

模块划分完整：ActionPoint / Battle / ChessBoard / Client / Core / Debug / Equipment / Gold / GridItem / Piece / PieceLayout / Systems / Turn / Resources。

---

## 二、总体评分：9.2 / 10

| 维度 | 评分 | 说明 |
|------|:----:|------|
| 架构一致性 | 9.5 | 三个工厂系统（大招/被动/Range）同一套接口+SO 模板 |
| 代码质量 | 9.0 | 零 TODO、零空 catch、零材质泄漏、null 守卫风格统一 |
| 可扩展性 | 9.5 | 位标志高亮、可插拔 provider、参数化 FloatStyle、逐格回调 |
| 耦合度/内聚度 | 8.5 | 横向依赖存在（PieceManager→BattleController），是唯一扣分项 |
| 资源管理 | 10 | MPB 零克隆、TileOverlay/PathPreview 手动释放、Animator null 守卫 |

---

## 三、Phase 2 框架优化完成度

**P0 / P1 全部闭环**，19 项优化全部落地：

- 棋子信息面板（防/移/射/能量/元素/附着/装备图标）
- 伤害跳字 + 元素反应中文提示（对象池）
- 棋子选中发光 + 已行动变灰（MaterialPropertyBlock，两属性叠加）
- 移动动画（DOTween 逐格滑动 + Animator Walk/Run/Idle + 逐格转向）
- 路径预览线 + 途经点系统 + R 键高亮模式切换
- Range Provider 系统（接口+工厂+13 asset+7 provider）
- 元素附着量系统（Gauge + 克制）
- TileOverlay 六边形 Mesh + 尺寸自适应 + 颜色收 GameConfig
- FloatStyle 参数化 + 面朝摄像机 + Alpha 分段淡出
- DebugManager 零侵入调试工具

---

## 四、四个需要后续处理的非阻塞项

按优先级排序：

### 1. 死代码 `PieceManager.MovePiece`（建议删除）

`MovePiece.cs:85-123` 目前**全项目无调用点**——唯一移动入口是 `MovePieceAlongPath`（`BattleController.cs:414`）。`MovePiece` 与 `MovePieceAlongPath` 重复了 5 段逻辑（路径构建/设锁/能量/时长/View null 分支），保留它只是养着两套同步维护的负担。

**建议**：删除，或改为一行 `MovePieceAlongPath(new[]{from, to}, ...)` 的单步封装。

### 2. `GameConfig.cs:46` Header 命名错误

第 46 行 `[Header("战术叠加层颜色")]` 下面其实是金币字段（`goldPerDamage` 等），且第 56 行又出现一次同名 Header。Inspector 里金币字段会挂在错误的标题下。

**建议**：第 46 行 Header 改为 `"金币"`，一行改动。

### 3. `isRunning` 判定写法不一致

`MovePiece`（单步）用硬编码 `bool isRunning = 1 > walkRunThreshold`，`MovePieceAlongPath` 用规范写法 `steps = path.Count - 1; steps > walkRunThreshold`。语义都对，但前者可读性差——若将来 `walkRunThreshold` 调成 0，单步会变 Run。

**建议**：随第 1 项一起处理（删掉 MovePiece 后此问题自然消失）。

### 4. 高频 Debug.Log 噪音（99 条）

Error/Warning 22+17 条全部合理，保留。但普通 `Debug.Log` 约 60 条，其中热点路径是噪音：

- `InputHandler.cs:137` —— **每次点击都 Log**（最高频）
- `PieceManager.cs:122/139/186/299` —— 每次移动/传送/攻击都 Log
- `KnockbackResolver.cs:49/75/84/88` —— 击退循环内每步 Log

**建议**：正式版对这三处高频日志做 `#if UNITY_EDITOR` 条件编译，其余低频日志（生成/回合切换/游戏结束）保留。

---

## 五、架构评价（GameDesigner 视角）

### 做得最好的一件事

**BattleController 经历了元素 → 装备 → 大招 → 道具 → 7 棋子 → 途经点 → 高亮模式 → 移动动画八层叠加后，每次系统注入只改 3-10 行。** 这次审查确认了四个系统（动画锁 `IsPieceAnimating` / 高亮模式 `HighlightMode` / 途经点 `_waypoints` / 溅射 `ApplySplashDamage`）在同一文件里共存且零冲突——这是架构正确的直观标志。好架构不体现在第一版代码多漂亮，体现在第八版改动多克制。

### 一个值得长期留意的横向依赖

`PieceManager` 通过静态方法直接访问 `BattleController.Instance.Model` 设动画锁。这是 Controller→Controller 的横向依赖，违反了严格 MVC。但这是项目既有惯例（PieceManager 也直接调 GoldManager/TurnManager），不算本次引入的问题。

**长期建议**：未来引入 EventBus 时，把"动画完成"这类跨系统通知从直接调用改为事件发布，横向依赖会自然收敛。

### 一个设计层面的信号

所有数值仍是 `[PLACEHOLDER]`，但**调参入口已经全部就位**——GameConfig.asset 现在一页能调：棋盘大小、AP、金币倍率、Overlay 4 组颜色、路径颜色/宽度、浮动文字 12 色 + 5 类动画参数、移动动画时长、走跑阈值、棋子定位偏移、变灰色。这是 Phase 2 框架优化最重要的产出：**不是"能玩"，是"能调"**。

---

## 六、下一步建议

框架优化到此收官，**正式转入 playtest 数值调优**：

1. 处理上述 4 个非阻塞项（半天工作量）
2. 用 DebugManager 打 20 局单机，记录：哪个棋子过强/过弱、装备性价比、金币倍率、元素反应触发频率
3. 数值调稳后，再进入 Phase 3（局域网联机）——**别用锁链锁住一辆还没调好刹车的车**

---

## 七、结论

**Phase 2 框架优化验收通过。** 从"棋子瞬移"到"角色有腿会跑、落地贴地、选中发光、已行动变灰、伤害跳字、元素反应提示"，视觉层和交互层全面闭环。核心系统（移动动画、变灰标记、Range Provider、浮动文字、溅射、途经点、元素附着量）全部落地且架构一致。剩 4 个非阻塞项，都是清理性工作，不影响 playtest。
