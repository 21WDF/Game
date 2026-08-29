# 背包侧栏手动摆槽位_Trae提示词.md

> 铁律：只写「改动方向 + 效果目标 + 红线」，不写实现细节。

## 〇、背景（一句话）

当前侧栏是「代码运行时 Instantiate 10 个槽位 + 拖 iconSlotPrefab + 拖已装备 3 槽容器」。这不符合用户搭法——用户已经有槽位预制体（自带空槽精灵图）、已经搭了 ScrollView，他要的是「**在 Unity 里手动摆好 10 个空槽，代码只负责填充**」。本次把侧栏改成「找已有槽位填充」，砍掉 Instantiate 和多余容器。

## 一、现状（框架事实）

1. `UI_BackpackSidebar` 当前字段：`panelRoot` / `slotsContainer`（已装备 3 槽）/ `backpackContainer` / `iconSlotPrefab` / `tooltip` / `titleText` / `fallbackColor` / `emptySlotColor`。
2. 当前 `EnsureFixedSlots()` 是「运行时 Instantiate 10 个 `iconSlotPrefab` 到 `backpackContainer`」，`Refresh` 里反复填充。
3. 槽位组件 `UI_BackpackIconSlot` 有 `SetIcon(sprite, fallbackColor)`（sprite 空时 `iconImage.sprite=null` + `color` 占位）、`SetLabel`、`SetSelected`、单击/双击/悬停 tooltip。
4. 已装备 3 槽（`slotsContainer`）在侧栏里是重复展示（棋子头顶的数值面板 UI_StatElement 已有「3 装备槽位图标」），本次移除。
5. 常驻红线（不可破坏）：侧栏不调用 `RegisterPanelOpen/Close`，穿透靠 `UIPanelRaycastBlocker`；随活动玩家切换；战前隐藏、开战显示、结束隐藏。

## 二、改动方向

### 1. 槽位改为「用户手动摆，代码找已有槽位填充」

- 不再运行时 Instantiate 槽位。改为：**在 `backpackContainer`（用户在 Unity 里摆好槽位的容器，即 ScrollView 的 Content）下，按 Hierarchy 顺序找到已有的 `UI_BackpackIconSlot` 槽位**（如 `GetComponentsInChildren`），作为固定槽位列表。
- 槽位数量 = 容器下实际摆的槽位数量（用户摆几个就是几个，不再由代码常量写死 10）。
- `Refresh` 时对这组已有槽位做「填充/清空」，**不增删、不销毁**。

### 2. 砍掉多余字段与逻辑

- 移除 `iconSlotPrefab`（不再 Instantiate）。
- 移除 `slotsContainer` 及「已装备 3 槽区」的整套展示逻辑（棋子头顶已有装备图标，侧栏不再重复）。
- 保留字段收敛为：`panelRoot`、`backpackContainer`、`tooltip`、`titleText`（可选）。
- 移除 `fallbackColor` / `emptySlotColor` 的颜色占位逻辑（见下：空槽改用预制体自带的空槽精灵图）。

### 3. 空槽显示 = 预制体自带的空槽精灵图

- 用户的槽位预制体 iconImage 自带「空槽精灵图」。**空槽时保持/恢复这个默认精灵图，不用颜色占位、不清空 sprite。**
- 填充物品时换成物品 icon；清空回空槽时恢复预制体的空槽精灵图。
- 具体做法由你定（例如槽位组件记录初始 sprite、或加一个空槽 sprite 引用），但要保证「空槽显示的是预制体自带的空槽图」。

### 4. 填充逻辑（延续现状，不改）

- 装备列表（获得顺序）在前 + 道具列表（获得顺序）在后，按顺序填入前 N 个槽；剩余槽保持空槽。
- 装备：icon + tooltip（名称+属性，复用 BuildStatsText）+ 无双击。
- 道具：icon + 可选小字 + tooltip（名称+描述+AP，复用 BuildGridItemDesc）+ 双击使用（UseFromInventory）。
- 单击单选高亮、悬停 tooltip 延续。

## 三、效果目标（验收标准）

1. 侧栏不再需要拖 `iconSlotPrefab`，也不再有「已装备 3 槽区」。
2. 用户在 ScrollView 的 Content 下摆几个槽位，侧栏就用几个槽位（不写死、不 Instantiate）。
3. 空槽显示**预制体自带的空槽精灵图**（不是颜色色块）。
4. 购买装备/道具后，图标填充到已有槽位；装备在前、道具在后。
5. 单击/悬停/双击行为与之前一致。
6. 侧栏仍：不屏蔽棋盘、随活动玩家切换、战前隐藏/开战显示/结束隐藏。

## 四、红线（绝不能破坏）

1. **槽位是用户手动摆的，代码只找已有槽位填充，不 Instantiate、不销毁。**
2. **空槽用预制体自带的空槽精灵图，不用颜色占位、不清空 sprite。**
3. **侧栏仍不得调用 `RegisterPanelOpen/Close`**。
4. 装备/道具管理逻辑（BuyEquipment / BuyToInventory / UseFromInventory / EquipToPiece / UnequipFromPiece）零改动——只改展示层。
5. `UI_BackpackIconSlot` 的单击/双击/悬停接口语义不变（可改 SetIcon 内部实现来支持「恢复空槽精灵图」）。
6. 商店、拍卖、地下交易、伤害管道、元素、护盾、城邦过滤零改动。

## 五、明确不做

- 拖拽、装备/卸下动作、槽位手动排序、溢出处理（超出槽位数的物品暂不展示）。
