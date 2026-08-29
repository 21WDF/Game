# 背包拖拽装备道具_Trae提示词.md

> 铁律：只写「改动方向 + 效果目标 + 红线」，不写实现细节。

## 〇、背景（一句话）

背包侧栏的最后一个交互：**拖拽**——把装备图标拖到己方棋子身上装备；把需指定棋子的道具拖到棋子身上使用。这是 UI→3D 棋盘的跨空间拖放，项目目前零拖拽代码，从零搭。

## 一、现状（框架事实）

1. 槽位组件 `UI_BackpackIconSlot` 当前实现 `IPointerClickHandler/IPointerEnterHandler/IPointerExitHandler`（左键单击选中、双击使用、右键 tooltip、悬停通知 tooltip）。**没有拖拽接口**。
2. **棋子有 Collider（PieceView）**，`ChessBoardController.RaycastTile()` 已处理「射线命中棋子 PieceView → 反查 Model.Coord → 返回该格子」。拖拽松手可复用：`RaycastTile()` → `tile.Coord` → `PieceLayoutModel.GetPieceAt(coord)` 取棋子（空格返回 null）。
3. `RaycastTile` 用 `Camera.main.ScreenPointToRay` + `Physics.Raycast`（3D 物理射线，不打 UGUI），所以拖拽松手判定与「鼠标是否在 UI 上」解耦。
4. 装备：`EquipmentManager.EquipToPiece(piece, equipment, slot)` 已就绪（从背包移除 + 替换旧装备 + OnEquip）。装备到首个空槽、满则替换槽 0 的逻辑可复用侧栏现有做法。
5. 道具：`GridItemManager.UseFromInventory(side, data)` 已就绪；`requiresSelectedPiece`（传送石）的道具要求「已选中己方棋子」（读 `BattleController.Model.SelectedPiece`）。选中接口 `BattleModel.SetSelection(piece)` 是 public。
6. 侧栏填充时已给槽位设置：装备 → icon + tooltip + `OnDoubleClick=null`；道具 → icon + tooltip + `OnDoubleClick=使用`。但**槽位当前没有记录「拖拽负载」（拖的是哪件装备/哪个道具）**。
7. 常驻红线（不可破坏）：侧栏不调 `RegisterPanelOpen/Close`、显隐走 CanvasGroup、随活动玩家切换、战前隐藏/开战显示/结束隐藏。

## 二、改动方向

### 1. 槽位支持拖拽

- 槽位组件增加拖拽接口（如 `IBeginDragHandler` / `IDragHandler` / `IEndDragHandler`）。
- 槽位增加「拖拽负载」：记录当前格子上「是装备还是道具、具体是哪一件」（侧栏填充时设置；空槽无负载不可拖）。

### 2. 拖拽的负载分工（谁可拖）

- **装备**：可拖拽，拖到己方棋子 = 装备。
- **道具（requiresSelectedPiece，如传送石）**：可拖拽，拖到己方棋子 = 选中该棋子 + 使用。
- **道具（非 requiresSelectedPiece，如扩展石/删除石）**：不拖拽，仍走双击使用。
- **空槽**：不可拖。

### 3. 拖拽松手判定（核心）

- 拖拽结束时，用 `RaycastTile()` + `GetPieceAt(coord)` 判定松手位置命中哪个棋子。
- 命中**己方**棋子：
  - 装备负载 → `EquipToPiece`（首个空槽，满则替换）。
  - 道具负载（requiresSelectedPiece）→ 先 `SetSelection(该棋子)` 再 `UseFromInventory`。
- 命中**敌方**棋子 / 空格 / 未命中 → 取消拖拽（无动作）。
- 拖拽过程中可给一个「跟随鼠标的图标」和「命中有效棋子时高亮」的反馈（可选，体验优化；不影响核心拖拽结果）。

### 4. 保留既有交互

- 左键单击选中、左键双击使用道具、右键 tooltip、悬停 tooltip 保持逻辑——全部不变。
- 拖拽与单击/双击要能正确区分（拖拽由 BeginDrag 接管，与点击事件自然区分）。

## 三、效果目标（验收标准）

1. 拖装备图标到己方棋子身上 → 装备成功（进棋子槽位、背包移除、装备被动生效）。
2. 拖装备到敌方棋子 / 空格 → 无动作（取消）。
3. 拖「需指定棋子的道具」（传送石）到己方棋子 → 选中该棋子并进入瞄准（传送落点）。
4. 非 requiresSelectedPiece 道具不可拖，双击照常使用。
5. 空槽不可拖。
6. 单击选中、双击使用、右键 tooltip、悬停 tooltip 行为与改动前一致。
7. 侧栏仍：不屏蔽棋盘（拖拽时除外，拖拽结束恢复）、随活动玩家切换、战前隐藏/开战显示/结束隐藏。

## 四、红线（绝不能破坏）

1. **只能拖到己方棋子**（装备/道具都不得拖到敌方棋子）。
2. **`EquipToPiece` / `UseFromInventory` 零改动**——拖拽只调用它们，不改装备/道具核心逻辑。
3. **侧栏仍不得调用 `RegisterPanelOpen/Close`**；显隐仍走 CanvasGroup。
4. 左键单击选中、双击使用、右键 tooltip 的语义不变。
5. 商店、拍卖、地下交易、伤害管道、元素、护盾、城邦过滤零改动。
6. 拖拽失败/取消不得产生副作用（不凭空装备、不凭空消耗道具）。

## 五、明确不做

- 拖拽排序/整理槽位。
- 装备/道具从棋子身上拖回侧栏（卸下仍归后续）。
- 溢出处理、存档。
