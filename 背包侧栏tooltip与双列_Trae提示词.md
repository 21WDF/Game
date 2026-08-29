# 背包侧栏tooltip与双列_Trae提示词.md

> 铁律：只写「改动方向 + 效果目标 + 红线」，不写实现细节。

## 〇、背景（一句话）

侧栏交互与布局做 4 项调整：①tooltip 改为右键显示（不再悬停）②tooltip 固定物品右侧、移出关闭/移进保持 ③移除 labelText 子物体 ④单列改双列（左装备右道具）。

## 一、现状（框架事实）

1. `UI_BackpackIconSlot`：实现 `IPointerEnterHandler/IPointerExitHandler`（悬停显示/隐藏 tooltip）+ `IPointerClickHandler`（左键单击选中、双击使用）；有 `iconImage` + `labelText` 两个子物体字段；`SetLabel` 设置道具小字。
2. `UI_ItemTooltip`：`Show(title, desc)` / `Hide()`，定位是「每帧跟随鼠标 + 屏幕边界 clamp」。
3. `UI_BackpackSidebar`：填充时 `SetLabel(data.displayName)` 给道具设小字；`BindCommon` 注入 tooltip；单列填充（装备前道具后混排）。
4. 常驻红线（不可破坏）：侧栏不调 `RegisterPanelOpen/Close`、显隐走 CanvasGroup（脚本自己永远 active）、随活动玩家切换、战前隐藏/开战显示/结束隐藏。

## 二、改动方向

### 1. tooltip 改为「右键显示」（不再悬停）

- 移除「悬停显示 tooltip」的 `IPointerEnter/Exit` 交互。
- 改为：**鼠标在物品上右键点击时**显示 tooltip（左键仍用于单击选中、双击使用，互不冲突）。
- 右键判断：`OnPointerClick` 里 `eventData.button == PointerEventData.InputButton.Right`。

### 2. tooltip 定位简化 + 关闭逻辑

- tooltip 不再「每帧跟随鼠标」。改为：**固定显示在物品的右侧**（以物品的 RectTransform 为锚定，定位到其右侧）。
- 关闭逻辑：
  - 鼠标**移出物品**后关闭 tooltip（可给一个极短的延迟，让鼠标有时间移进 tooltip）；
  - 若鼠标**移进 tooltip 显示区域**，则保持显示不关闭；
  - 鼠标**移出 tooltip** 后关闭。
- 简化优先，屏幕边界 clamp 可做可不做（用户表示可简化）。

### 3. 移除 labelText

- `UI_BackpackIconSlot` 移除 `labelText` 字段与 `SetLabel` 方法（道具名由 tooltip 负责显示）。
- 侧栏填充时不再调用 `SetLabel`。槽位子物体只剩「物品图片」（+ 父物体空槽背景）。

### 4. 单列改双列（左装备右道具）

- 槽位布局从单列改为**双列**：**左列 = 装备、右列 = 道具**。
- 槽位总数不变（`slotCount` 默认 10，即左列 5 个装备槽 + 右列 5 个道具槽）。
- 实现方向：容器用双列布局（如 GridLayoutGroup Fixed Column Count=2），代码按索引填充——**偶索引槽位填装备（按获得顺序）、奇索引槽位填道具（按获得顺序）**，使左列全是装备、右列全是道具。
- 装备/道具按各自获得顺序填入各自列；该列无物品的槽保持空槽。

## 三、效果目标（验收标准）

1. 悬停物品不再显示 tooltip；**右键物品**才显示 tooltip。
2. tooltip 固定显示在该物品右侧（不跟随鼠标）。
3. 鼠标移出物品 → tooltip 关闭；移进 tooltip → 保持显示；移出 tooltip → 关闭。
4. 槽位不再有小字（labelText 移除），道具名靠 tooltip 显示。
5. 侧栏双列：左列装备、右列道具；装备/道具按各自获得顺序填列。
6. 左键单击选中（父物体变色）、左键双击道具使用，行为不变。
7. 侧栏仍：不屏蔽棋盘、随活动玩家切换、战前隐藏/开战显示/结束隐藏。

## 四、红线（绝不能破坏）

1. **侧栏仍不得调用 `RegisterPanelOpen/Close`**；显隐仍走 CanvasGroup（脚本自己永远 active）。
2. 装备/道具管理逻辑（BuyEquipment / BuyToInventory / UseFromInventory / EquipToPiece / UnequipFromPiece）零改动。
3. 左键单击选中、左键双击使用的语义不变（右键新增 tooltip 不干扰左键）。
4. 空槽 = 父物体空槽背景 + 物品图隐藏（保持现状）；高亮 = 父物体变色（保持现状）。
5. 商店、拍卖、地下交易、伤害管道、元素、护盾、城邦过滤零改动。

## 五、明确不做

- 拖拽、装备/卸下动作、槽位手动排序、溢出处理。
