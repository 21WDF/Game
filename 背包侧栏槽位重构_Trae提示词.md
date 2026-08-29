# 背包侧栏槽位重构_Trae提示词.md

> 铁律：只写「改动方向 + 效果目标 + 红线」，不写实现细节。

## 〇、背景（一句话）

槽位预制体结构要简化 + 槽位由脚本在滚动视图里「生成指定数量（单列）」。核心：移除高亮子物体（高亮改父物体变色）、代码 Instantiate 指定数量、填充改物品图片子物体。

## 一、现状（框架事实）

1. 槽位预制体挂 `UI_BackpackIconSlot`，当前有三个子物体：①物品图片（`iconImage`，无装备显示空）②高亮空槽精灵图（`highlight`，GameObject）③字提示（`labelText`）。**预制体的父物体 Image 本身就是「空单槽精灵图」**。
2. 当前 `UI_BackpackIconSlot` 字段：`iconImage`（Image）、`highlight`（GameObject）、`labelText`（TMP）。`SetSelected` 用 `highlight.SetActive`，`SetIcon(sprite, fallbackColor)` 用 sprite+color 占位。
3. 当前 `UI_BackpackSidebar` 字段：`panelRoot` / `slotsContainer`（已装备 3 槽）/ `backpackContainer` / `iconSlotPrefab` / `tooltip` / `titleText` / `fallbackColor` / `emptySlotColor`。
4. 常驻红线（不可破坏）：侧栏不调用 `RegisterPanelOpen/Close`，穿透靠 `UIPanelRaycastBlocker`；随活动玩家切换；战前隐藏、开战显示、结束隐藏。

## 二、改动方向

### 1. 移除高亮子物体，高亮改为「改父物体颜色」

- 槽位预制体**不再有「高亮空槽精灵图」子物体**（子物体②移除）。
- 选中高亮改为**直接改变父物体 Image 的颜色**（选中变高亮色，取消恢复默认色）。父物体 Image 的引用方式由你定（组件引用或 GetComponent），但不要再有独立的高亮子物体。

### 2. 槽位由脚本在滚动视图里「生成指定数量（单列）」

- `UI_BackpackSidebar` 加一个「槽位数量」可配置字段（用户指定，默认 10）。
- 脚本在 `backpackContainer`（滚动视图的 Content）下，一次性 Instantiate 出「指定数量」的槽位预制体。
- 单列竖排由 Content 上的 LayoutGroup 决定（代码只 Instantiate 到 Content，不关心排列方向）。
- 生成后不增删，仅反复填充/清空（空槽保持父物体空槽背景）。

### 3. 填充 = 改物品图片子物体

- 有物品：把子物体①（物品图片）的 sprite 换成物品 icon，显示。
- 空槽：子物体①显示空（隐藏或透明，由你定），父物体保持空槽背景。
- 装备：物品图 + tooltip（名称+属性，复用 BuildStatsText）+ 无双击。
- 道具：物品图 + 字提示（labelText 显示道具名）+ tooltip（名称+描述+AP，复用 BuildGridItemDesc）+ 双击使用（UseFromInventory）。
- 装备在前、道具在后，按获得顺序填入前 N 槽；剩余槽保持空槽。

### 4. 移除多余字段与逻辑

- 移除 `slotsContainer`（已装备 3 槽区）及其展示逻辑——棋子头顶的数值面板已有装备图标，侧栏不重复。
- 移除 `fallbackColor` / `emptySlotColor` 颜色占位逻辑（空槽用父物体空槽背景，不用颜色占位）。
- 字段收敛为：`panelRoot`、`backpackContainer`、`iconSlotPrefab`、`slotCount`（槽位数量）、`tooltip`、`titleText`（可选）。

### 5. 为后续拖拽预留（本次不做拖拽）

- 槽位组件的结构改动不要写死「阻碍后续拖拽」的东西（拖拽是下一步：装备拖到棋子、道具拖到棋子）。本次只需保证槽位组件结构清晰、图标/选中态可独立控制即可。

## 三、效果目标（验收标准）

1. 槽位预制体没有「高亮子物体」了，选中高亮表现为父物体 Image 变色。
2. 侧栏脚本在滚动视图 Content 里生成「指定数量」的槽位（单列），数量可配置。
3. 空槽显示父物体的空槽背景（物品图片子物体显示空）。
4. 购买装备/道具后，物品图片子物体换成对应 icon；装备在前、道具在后。
5. 单击选中（父物体变色）、悬停 tooltip、双击道具使用，行为正确。
6. 侧栏不再有「已装备 3 槽区」；仍不屏蔽棋盘、随活动玩家切换、战前隐藏/开战显示/结束隐藏。

## 四、红线（绝不能破坏）

1. **高亮 = 改父物体颜色，不再有独立高亮子物体。**
2. **空槽 = 父物体空槽背景 + 物品图片子物体显示空；不用颜色占位、不额外造空槽图。**
3. **侧栏仍不得调用 `RegisterPanelOpen/Close`。**
4. 装备/道具管理逻辑（BuyEquipment / BuyToInventory / UseFromInventory / EquipToPiece / UnequipFromPiece）零改动——只改展示层。
5. `UI_BackpackIconSlot` 的单击/双击/悬停接口语义不变（可改 SetIcon/SetSelected 内部实现）。
6. 商店、拍卖、地下交易、伤害管道、元素、护盾、城邦过滤零改动。

## 五、明确不做

- 拖拽、装备/卸下动作、槽位手动排序、溢出处理（超出槽位数的物品暂不展示）。
