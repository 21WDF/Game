# 背包侧栏显隐修复_Trae提示词.md

> 铁律：只写「改动方向 + 效果目标 + 红线」，不写实现细节。

## 〇、背景（一句话）

侧栏战前隐藏用了 `panelRoot.SetActive(false)`，而用户把 `panelRoot` 拖成了「脚本自己挂的物体」，导致 `SetActive(false)` 把脚本自己也禁用了、订阅断开，侧栏开战后起不来。修复：**不再用 SetActive 控制显隐，改用 CanvasGroup（透明 + 挡射线开关），脚本永远 active**。

## 一、现状（框架事实）

1. `UI_BackpackSidebar` 当前字段：`panelRoot` / `backpackContainer` / `iconSlotPrefab` / `slotCount` / `tooltip` / `titleText`。
2. 显隐：`panelRoot.SetActive(false)`（战前隐藏）+ `panelRoot.SetActive(true)`（开战显示）。
3. 问题：`panelRoot` 被拖成脚本自己挂的物体时，`SetActive(false)` 会禁用脚本自身、断开订阅，且 OnDisable 里 `_subscribed` 逻辑无法在协程中途正确退订，导致侧栏「运行时被隐藏、开战后不恢复」。
4. 穿透阻挡：脚本自动给 panelRoot 补挂 `UIPanelRaycastBlocker`；InputHandler 靠它 + 背景 Image 的 RaycastTarget 判断「鼠标是否在面板上」。

## 二、改动方向

### 1. 显隐改用 CanvasGroup，不再 SetActive 自己

- 移除 `panelRoot` 字段（不再需要「常驻父物体 + panelRoot 子物体」两层结构）。
- 脚本操作**自己挂载的 GameObject**：自动获取或补挂一个 `CanvasGroup`，用它控制显隐。
- **隐藏**：CanvasGroup `alpha = 0`（透明）+ `blocksRaycasts = false`（不挡射线）+ `interactable = false`（不响应交互）。
- **显示**：CanvasGroup `alpha = 1` + `blocksRaycasts = true` + `interactable = true`。
- 脚本自身的 GameObject **始终保持 active**（不用 SetActive 禁用自己），订阅不断。

### 2. 穿透阻挡挂到脚本自己的 GameObject

- `UIPanelRaycastBlocker` 改为自动补挂到**脚本自己的 GameObject**（不再挂 panelRoot）。
- 侧栏显示时（blocksRaycasts=true），鼠标在侧栏上 → InputHandler 检测到 UIPanelRaycastBlocker → 拦截棋盘点击/悬停；侧栏隐藏时（blocksRaycasts=false）不挡。

### 3. 战前隐藏 / 开战显示 / 结束隐藏

- 订阅就绪后：初始隐藏（CanvasGroup 隐藏态）。
- 首个回合开始（OnTurnStarted）→ 显示（CanvasGroup 显示态）+ 刷新。
- 游戏结束（OnGameOver）→ 隐藏。

### 4. 字段收敛

- 移除 `panelRoot`。剩余字段：`backpackContainer` / `iconSlotPrefab` / `slotCount` / `tooltip` / `titleText`（可选）。

## 三、效果目标（验收标准）

1. 脚本挂 `BackpackSidebarHost`（无 panelRoot 子物体），运行时 **BackpackSidebarHost 不再被 SetActive 隐藏**。
2. 战前侧栏透明不可见、不挡射线；开战首个回合显示（不透明、可交互）；游戏结束隐藏。
3. 侧栏显示时，鼠标在侧栏上棋盘无响应；移开后正常（大招键可用）。
4. 侧栏隐藏时不挡棋盘（鼠标在侧栏原本位置也能点棋盘）。
5. 单击选中、悬停 tooltip、双击道具使用、随活动玩家切换等既有行为不变。
6. 不再需要「常驻父物体 + panelRoot 子物体」两层结构，脚本挂一个物体即可。

## 四、红线（绝不能破坏）

1. **不再用 `SetActive` 禁用脚本自己的 GameObject**（显隐一律走 CanvasGroup）。
2. **侧栏仍不得调用 `RegisterPanelOpen/Close`**。
3. 装备/道具管理逻辑（BuyEquipment / BuyToInventory / UseFromInventory / EquipToPiece / UnequipFromPiece）零改动。
4. `UI_BackpackIconSlot` / `UI_ItemTooltip` 的接口语义不变（可不动这两个文件）。
5. 商店、拍卖、地下交易、伤害管道、元素、护盾、城邦过滤零改动。

## 五、明确不做

- 拖拽、装备/卸下动作、槽位手动排序、溢出处理。
