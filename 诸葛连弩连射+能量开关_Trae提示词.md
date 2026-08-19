# 诸葛连弩连射 + 免 AP 能量开关 · Trae 提示词

## 任务
1. 诸葛连弩实现「连射」：解除「每回合一次攻击」限制，可多次攻击（每次消耗 1 AP）
2. FreeAPPassive 加 `gainEnergy` 开关：免 AP/连射动作是否获得充能

## 改动

### 1. FreeAPPassive 加 gainEnergy 参数
- 加参数 `gainEnergy`（bool，默认 false），暴露只读 `GainEnergy` 属性
- 语义：`false` = 该装备的免 AP 移动/连射攻击**不获得充能**；`true` = 获得充能

### 2. PieceManager.MovePieceAlongPath 加 skipEnergy 参数
- 加 `bool skipEnergy = false` 参数；`skipEnergy=true` 时跳过移动充能（`energyPerMove`）
- 默认 false 保持现有充能行为

### 3. BattleController.HandleMove（探索者护臂）
- 免 AP 移动命中时：`MovePieceAlongPath(..., skipEnergy: !freeAP.GainEnergy)`（免 AP 移动不充能，除非 gainEnergy=true）
- 消耗 AP 的正常移动：`skipEnergy` 默认 false（照常充能）

### 4. BattleController.HandleAttack（诸葛连弩：连射 + 能量开关）
- **连射**：有 `FreeAPPassive`（`FreeType == Attack`）的棋子，**跳过 `HasAttackedThisTurn` 检查**（可多次攻击）；无诸葛连弩的棋子保持「每回合一次」限制
- 免 AP 判断逻辑不变（免 AP 可用则跳过 HasAP/ConsumeAP + MarkUsed；否则正常消耗 AP）
- **充能开关**：有诸葛连弩的棋子，`AttackPiece(..., skipEnergy: !freeAP.GainEnergy)`——连射攻击（含免 AP 与消耗 AP 的多次攻击）由 gainEnergy 控制是否充能；无诸葛连弩的正常攻击 `skipEnergy` 默认 false（照常充能）

## 约束
1. 只改：FreeAPPassive.cs / PieceManager.cs / BattleController.cs，其他一律不动
2. 攻击的「每回合一次」限制仅对诸葛连弩棋子解除，其余棋子不变
3. 元素反应、金币、伤害结算逻辑不动
4. 完成后自查诊断无报错即可，运行时验证由我方做
