# 石像鬼板甲「首次被攻击」叠层修复 · Trae 提示词

## 问题
`StoneSkinPassive.TryStack` 里，**第一次**被敌方棋子攻击（`_lastAttacker` 从 `null` 变为该棋子）时也执行了 `_stacks++`，导致首次被攻击就 +5 防。

## 正确规格（用户原始规则）
1. 第一次被敌方棋子 A 攻击 → **只开启计时器**（不叠防），记录 `_lastAttacker = A`
2. 计时器内被「不同敌人」B 攻击 → 才 `_stacks++`（+5 防），刷新计时器

## 修复
`TryStack` 里区分「首次被攻击」与「后续不同敌人」：
- `_lastAttacker == null`（首次）：只 `_timer = timerTurns`、`_lastAttacker = attacker`，**不叠防**
- `_lastAttacker != null && attacker != _lastAttacker`：`_stacks++`、`_timer = timerTurns`、`_lastAttacker = attacker`

## 约束
只改 `StoneSkinPassive.cs` 的 `TryStack` 方法，其他一律不动。
