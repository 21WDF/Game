# FlyMove 路径含被占格修复 · Trae 提示词

## 问题

`FlyMoveProvider`（飞行移动）的路径生成（`FindPath`）会返回**包含被飞越的敌方棋子格**的路径。逐格移动时，棋子的占据表更新会覆盖这些敌方格，导致占据数据不一致（表现为两个棋子重叠、敌方棋子消失）。

## 根源

FlyMove 的 `FindPath` 在做 BFS 时没有排除被飞越的棋子格，回溯出的路径把它们也包含了进去。这与 `StraightPassProvider` 的处理不一致——StraightPass 已经做到「被占中间格不入路径」。

## 修复目标

FlyMove 的移动路径应该**只包含空格**（起点、落点、经过的空格），被飞越的敌方棋子格不出现在路径里；移动表现是「飞越」这些格（直线滑过，不逐格占据）。保证飞行移动不会覆盖任何敌方棋子的占据记录。

## 涉及文件

- `Assets/Game/Piece/Range/FlyMoveProvider.cs`（`FindPath` 方法）

## 不要破坏

1. FlyMove 的可达范围计算（`GetReachable`，高亮是对的，不要动）
2. 飞行「可穿过棋子」的语义（不能因此改成遇棋子即停止）
3. 其他 Provider（StraightMove / StraightPass / ParityMove / FreeMove）的路径行为

## 验收要点

- 飞行路径不含任何被占据的格子
- 飞行穿过敌方棋子时，占据表不发生变化（敌方棋子原地不动）
- 落点正常到达、正常占据
- 与 `StraightPassProvider` 的「被占中间格不入路径」口径一致
