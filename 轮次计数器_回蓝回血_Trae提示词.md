# 轮次计数器 + 棋子基础回蓝回血 Trae 提示词

```
在 Chaotic Chess 中做三个改动：轮次计数器、棋子基础回蓝/回血属性、回蓝回血逻辑。

注意：之前有一个"回蓝时机修复"（回蓝从 OnTurnStarted 移到 EndTurn）的提示词。
如果那个修复已做，本次在 EndTurn 里扩展回蓝逻辑即可；如果未做，本次一并做。
以当前实际代码为准，最终保证回蓝/回血都在 EndTurn（回合结束）触发。

## 改动 1：轮次计数器（CurrentRound）

### TurnModel.cs 加字段
public int CurrentRound { get; private set; }  // 轮次 = 双方各行动一次

### 轮次递增时机
- StartFirstTurn()：CurrentRound = 1
- EndTurn 里，SwitchActivePlayer 之后，如果新 ActivePlayer == P1（切回 P1，一轮完成），CurrentRound++

### 显示
TurnManager.OnTurnStarted 的 Debug.Log 可带上轮次（如"第 N 轮"），
UI_TurnDisplay 的"第 N 回合"保持现状即可（不强制改）。

## 改动 2：PieceData 加 baseEnergyRegen / baseHPRegen

### PieceData.cs 加字段（默认 0）
public int baseEnergyRegen = 0;  // 每回合回蓝（棋子基础值）
public int baseHPRegen = 0;      // 每回合回血（棋子基础值）

### 现有 9 个棋子 asset 默认 0，不用改（除非要设非 0 值）

## 改动 3：回蓝/回血逻辑（回合结束触发）

### 回蓝
在 EndTurn（回合结束，_model.SwitchActivePlayer() 之前）：
- 遍历当前 ActivePlayer 的棋子（此时还是"刚结束的一方"）
- 回蓝量 = piece.Data.baseEnergyRegen + 该棋子所有装备 bonusEnergyPerTurn 之和
- 回蓝量 > 0 才调 piece.Energy?.Gain(regen)（Energy 为 null 跳过，Gain 自带上限）

### 回血
在 EndTurn（同上位置，和回蓝并列）：
- 遍历当前 ActivePlayer 的棋子
- 回血量 = piece.Data.baseHPRegen（装备回血阶段②再加）
- 回血量 > 0 才回血：piece.CurrentHP = Mathf.Min(piece.MaxHP, piece.CurrentHP + regen)
- 死亡棋子跳过
- 如果 PieceModel 没有回血方法，加一个 Heal(int amount) 或直接改 CurrentHP

## 约束

- 不改装备的 bonusEnergyPerTurn 逻辑（装备回蓝保留）
- 回血只做"棋子基础回血"（baseHPRegen），装备回血（生命铠甲被动）阶段②再做
- 不改 OnTurnStarted 的其他逻辑（攻击标记重置、元素递减、清高亮）
- CurrentRound 只新增，不改变 CurrentTurn 现有语义（持续效果仍用 CurrentTurn 计数）
- 回蓝/回血时机统一在 EndTurn（回合结束），不在 OnTurnStarted

## 验收

- [ ] TurnModel 有 CurrentRound 字段
- [ ] 第一轮 CurrentRound=1，P2 结束切回 P1 后 CurrentRound=2
- [ ] PieceData 有 baseEnergyRegen / baseHPRegen 字段（默认 0）
- [ ] 某棋子设 baseEnergyRegen=2 后，该方回合结束回 2 蓝（叠加装备回蓝）
- [ ] 某棋子设 baseHPRegen=3 后，该方回合结束回 3 血，不超过 MaxHP
- [ ] 回蓝/回血在"结束回合"时触发（不是"开始回合"）
```
