# 诸葛连弩连射 · UI 交互层放行修复（Trae 提示词）

## 背景

上次实现连射时，只改了 `BattleController.HandleAttack` 内部的放行逻辑（`repeater == null` 判断），但 **UI 交互层在到达 HandleAttack 之前还有三道 `!HasAttackedThisTurn` 拦截**，导致已攻击过一次的连弩棋子虽然逻辑上允许再攻击，实际上玩家根本无法发起第二次攻击。

## Bug 根源（三道拦截，全部用 `HasAttackedThisTurn` 一刀切）

`Assets/Game/Battle/Controller/BattleController.cs`：

1. **点击敌方棋子时的攻击判定（约 152-153 行）**：
   ```
   bool canAttack = APManager.Instance.HasAP(selected.Owner, 1)
                    && !selected.HasAttackedThisTurn;
   ```
   已攻击的连弩棋子 `HasAttackedThisTurn == true` → `canAttack == false` → 点击敌人直接 return，根本走不到 HandleAttack。

2. **移动模式下显示攻击范围（约 238 行）**：`if (hasAP && !piece.HasAttackedThisTurn)` —— 已攻击的连弩棋子不显示可攻击的敌人标记。

3. **攻击模式下显示攻击范围（约 255 行）**：`if (hasAP && !piece.HasAttackedThisTurn)` —— 已攻击的连弩棋子攻击模式下不显示攻击范围。

## 修复方案

### 改动 1（核心）：新增辅助方法 `CanAttack`

在 `FindFreeAP`（约 376 行）附近新增一个 static 辅助方法：

- 语义：判断棋子**当前能否发起攻击**。
- 连弩持有者（`FindFreeAP(piece, FreeAPType.Attack) != null`）→ 恒返回 true（连射，解除"每回合一次"限制）。
- 非连弩 → 返回 `!piece.HasAttackedThisTurn`（保持原语义不变）。

### 改动 2：三处拦截点改用 `CanAttack`

把上面三处 `!selected.HasAttackedThisTurn` / `!piece.HasAttackedThisTurn` 全部替换为 `CanAttack(selected)` / `CanAttack(piece)`：

1. 约 153 行：`bool canAttack = APManager.Instance.HasAP(selected.Owner, 1) && CanAttack(selected);`
2. 约 238 行：`if (hasAP && CanAttack(piece))`
3. 约 255 行：`if (hasAP && CanAttack(piece))`

### 改动 3（配套 UX）：连弩持有者攻击后不变灰

`Assets/Game/Piece/Controller/PieceManager.cs` 约 284 行：
```
attacker.View?.SetActed(true);
```

改为：**若 attacker 持有连弩（`FreeAPPassive` 且 `FreeType == Attack`），跳过 `SetActed(true)`**，让玩家直观看到连弩棋子攻击后仍可继续连射（不变灰）。

- 判断方式复用 `attacker.GetAllPassives()` 遍历，`is FreeAPPassive fp && fp.FreeType == FreeAPType.Attack` 即持有连弩。
- 注意：`FreeAPType` 枚举定义在 `FreeAPPassive.cs`，需确认 PieceManager 能访问到（同程序集应可直接引用，若命名空间不同则补 using）。

## 约束（务必遵守）

1. **只改这两个文件**：`BattleController.cs`（3 处拦截 + 1 个辅助方法）、`PieceManager.cs`（1 处 SetActed）。其他文件一律不动。
2. **不要动 `HasAttackedThisTurn` 字段的底层语义**：它仍然在 `AttackPiece` 里被设为 true、在每回合开始时被重置。本次修复只在 UI 呈现层对连弩持有者做豁免，不要改字段的设置/重置逻辑。
3. **AP 判断保持现状**：`hasAP`（AP≥1）的判断不变。连弩"AP=0 但免 AP 攻击冷却好"这一边界场景本次不处理（`HandleAttack` 内部已正确处理，只是 UI 高亮不会提前标出，属可接受的呈现差异）。
4. **不要改 `HandleAttack` 内部已正确的连射逻辑**（`repeater` / `freeAttack` 那一段）。
5. 诊断无报错后收尾。
