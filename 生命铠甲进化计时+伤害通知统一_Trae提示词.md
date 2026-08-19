# 生命铠甲进化计时 + 伤害通知统一 · Trae 提示词（完整版，替代之前《生命铠甲_进化计时修复》）

## 目标
1. 生命铠甲进化计时改为装备实例独立（修 bug）
2. "未受伤害"判定精确化：**所有敌方造成的实际减血都打断计时；只发动攻击但没减血不打断**

## 一、RegenPassive 实例计时（生命铠甲）
- 加实例字段 `_turnsWithoutDamage`（连续未受伤害回合计时）
- `OnTurnStart`：`_turnsWithoutDamage++` 后判断 `>= upgradeThreshold` 决定回 `upgradedAmount` 还是 `baseAmount`
- `OnDamageReceived`：`_turnsWithoutDamage = 0`（任何实际减血都归零）
- `OnEquip` / `OnUnequip`：`_turnsWithoutDamage = 0`（装上/卸下清零，重装重新计时）
- 不再引用 `owner.TurnsSinceDamaged`

## 二、伤害通知统一（让所有减血路径都触发 OnDamageReceived）

### 1. IPassiveEffect.OnDamageReceived 加参数
签名改为：`void OnDamageReceived(PieceModel owner, int damage, bool fromReflect = false)`
- `fromReflect=true` 表示这是反伤伤害（供反甲防递归判断）

### 2. PieceManager 新增统一伤害应用方法
`ApplyIncomingDamage(PieceModel target, int damage, PieceModel source, bool fromReflect = false)`：
- `damage <= 0` 或 `target` 已死 → 直接 return（没减血，不通知、不打断）
- `target.TakeDamage(damage)`
- `target.TurnsSinceDamaged = 0`；`target.LastDamageSource = source`
- 触发 `target` 全部被动的 `OnDamageReceived(target, damage, fromReflect)`
- 注意：**只触发"受伤害"通知，不触发"造成伤害"（吸血 OnDamageDealt）**——吸血保持只在 AttackPiece 生效

### 3. 各减血路径改调 ApplyIncomingDamage（替换直接 TakeDamage）
- 大招 FireSlashUltimate.Execute → `ApplyIncomingDamage(target, damage, caster)`
- DoT（TurnManager.TickElementDuration）→ `ApplyIncomingDamage(piece, dotDmg, piece.DotSource)`
- 非元素溅射（BattleController.ApplySplashDamage 纯公式分支）→ `ApplyIncomingDamage(occupant, damage, attacker)`
- 反伤（ThornsPassive）→ `ApplyIncomingDamage(target, final, owner, fromReflect: true)`

### 4. DoT 来源记录
- PieceModel 加 `DotSource` 字段（感电 DoT 的施加者）
- AttackPiece 施加感电处（设置 DotDamagePerTurn/DotTurnsRemaining 的地方）同时记 `target.DotSource = attacker`

### 5. 反甲防递归
- ThornsPassive.OnDamageReceived 加 `fromReflect` 参数；`fromReflect=true` 时直接 return（反伤不触发反甲，防递归）
- `fromReflect=false` 时按现有逻辑反伤

## 约束（必须遵守）
1. 只改：IPassiveEffect / PieceManager / PieceModel / FireSlashUltimate / TurnManager / BattleController / ThornsPassive / RegenPassive，其他不动
2. AttackPiece 主路径的伤害计算 / 元素反应 / 金币逻辑不动（它已正确触发通知，接口加默认参数后自动兼容）
3. 反伤仍不触发元素反应 / 金币 / 能量（保持直接扣血语义，仅新增"受伤害通知"）
4. 吸血（OnDamageDealt）生效范围不变
5. 完成后自查诊断无报错即可，运行时验证由我方做
