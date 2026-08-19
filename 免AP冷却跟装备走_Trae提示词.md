# 免 AP 冷却跟装备走（探索者护臂/诸葛连弩）· Trae 提示词

## 问题
`FreeAPPassive` 的 `OnEquip` / `OnUnequip` 都执行 `_cooldown = 0`，导致：
1. 卸下再装备后冷却被刷新，玩家可反复装卸「白嫖」免 AP
2. 冷却不跟装备走——冷却期间卸下转给其他棋子后，对方能立即白嫖

## 修复
去掉 `OnEquip` / `OnUnequip` 里的 `_cooldown = 0`，改为空实现（接口要求必须实现这两个方法，但方法体留空）。

**原理**：冷却 `_cooldown` 记录在装备实例上（被动对象随装备存续）；卸下后被动不再被棋子的 `GetAllPassives` 遍历，`OnTurnStart` 不再触发 → 冷却**自然暂停**；重新装备后 `OnTurnStart` 恢复触发 → 冷却**继续递减**。新装备构造时 `_cooldown` 默认 0，无需 `OnEquip` 清零。

修复后行为：冷却跟装备走、卸下暂停、重装继续；冷却期间转给任何棋子都无法白嫖。

## 约束
只改 `FreeAPPassive.cs`，其他一律不动。
