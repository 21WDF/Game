# 免 AP 被动合并（FreeMoveAPPassive + FreeAttackAPPassive → FreeAPPassive）· Trae 提示词

## 任务
把两个几乎相同的免 AP 被动合并为一个 `FreeAPPassive`，用枚举 `freeType` 区分移动/攻击。

## 改动

### 1. 新建 FreeAPPassive.cs（删除 FreeMoveAPPassive.cs 和 FreeAttackAPPassive.cs 两个文件）
- 顶部定义枚举：
  `public enum FreeAPType { Move = 0, Attack = 1 }`
- `FreeAPPassive` 参数：`freeType`（FreeAPType，默认 Move）、`cooldownTurns`（int，默认 4）
- 实例字段 `_cooldown`；`CanUseFreeAP => _cooldown <= 0`；`MarkUsed() => _cooldown = cooldownTurns`
- `OnTurnStart` 递减（最小 0）；`OnEquip`/`OnUnequip` 清零
- 暴露只读 `FreeType` 属性

### 2. PassiveFactory 同步
- 删除 `FreeMoveAPPassive` / `FreeAttackAPPassive` 两个 case 和 `FreeMoveAPParams` / `FreeAttackAPParams` 两个参数类
- 新增 `case "FreeAPPassive"` + `FreeAPParams`（`public FreeAPType freeType = FreeAPType.Move; public int cooldownTurns = 4;`，保持 `[System.Serializable]`）
- JSON 里 `freeType` 用枚举名：`{"freeType":"Move","cooldownTurns":4}` 或 `{"freeType":"Attack",...}`

### 3. BattleController 合并 helper
- 删除 `FindUsableFreeMove` / `FindUsableFreeAttack`，合并为 `FindUsableFreeAP(PieceModel piece, FreeAPType type)`：遍历被动找 `FreeAPPassive` 且 `FreeType == type` 且 `CanUseFreeAP`
- `HandleMove` 改用 `FindUsableFreeAP(piece, FreeAPType.Move)`
- `HandleAttack` 改用 `FindUsableFreeAP(piece, FreeAPType.Attack)`

## 约束
1. 只改：新建 FreeAPPassive.cs + 删除两个旧被动文件 + PassiveFactory.cs + BattleController.cs，其他一律不动
2. 免 AP 语义不变（完全免 AP、跳过 HasAP/ConsumeAP、4 回合冷却、装卸清零）
3. 完成后自查诊断无报错即可，运行时验证由我方做
