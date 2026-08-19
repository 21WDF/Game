# 生命铠甲进化计时归属修复 · Trae 提示词

## 问题
RegenPassive 的进化判断目前读 `owner.TurnsSinceDamaged`（棋子级全局字段），导致三个错误：
1. 一个棋子装多件生命铠甲时，共用同一个计时，一件触发进化、其他件也一起进化（应每件独立计时独立进化）
2. 计时从棋子出生就开始累积，装上装备前就已经在计时（应只在装备期间计时）
3. 卸下后计时不清除（应清除，重装后重新计时）

## 修复
改为 RegenPassive 自己维护实例级计时字段，与 KillAttackPassive.Accumulated 同模式（叠层/计时跟随装备实例）：

- 加实例字段 `_turnsWithoutDamage`（连续未受伤害回合计时）
- 覆写 `OnTurnStart`：`_turnsWithoutDamage++` 后判断 `>= upgradeThreshold`，决定回 `upgradedAmount` 还是 `baseAmount`
- 覆写 `OnDamageReceived`：`_turnsWithoutDamage = 0`（受击清零）
- 覆写 `OnEquip` / `OnUnequip`：`_turnsWithoutDamage = 0`（装上/卸下都清零，重新计时）
- 不再引用 `owner.TurnsSinceDamaged`

## 约束
只改 `RegenPassive.cs` 一个文件，其他一律不动。
