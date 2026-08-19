# 英灵幡完整被动修复 · Trae 提示词

## 问题
当前 `SpiritBannerPassive` 只实现了「己方棋子死亡立刻回蓝」，缺少「之后每回合持续回蓝」。

## 完整规格
携带方棋子死亡时：
1. **立刻**回蓝 `instantEnergy`（默认 20）
2. 之后**每个回合**回蓝 `regenEnergyPerTurn`（默认 5），持续 `durationTurns` 回合（默认 3）

三个参数：`instantEnergy` / `regenEnergyPerTurn` / `durationTurns`

叠加规则：立刻回蓝每次死亡都累加；持续回蓝每次死亡**刷新**为完整 `durationTurns`（不叠加）。

## 改动

### 1. SpiritBannerPassive 重写
- 参数改为 `instantEnergy = 20`、`regenEnergyPerTurn = 5`、`durationTurns = 3`
- 实例字段：`_regenTurnsRemaining`（剩余持续回合）
- `OnAllyDeath(owner, ally)`：`owner.Energy` 判空；立刻 `Gain(instantEnergy)`；`_regenTurnsRemaining = durationTurns`（刷新）
- `OnTurnStart(owner)`：`_regenTurnsRemaining > 0` 时 `owner.Energy?.Gain(regenEnergyPerTurn)` 并 `_regenTurnsRemaining--`（回合回蓝 + 递减；Energy 判空）
- `OnEquip` / `OnUnequip`：`_regenTurnsRemaining = 0`

### 2. PassiveFactory.SpiritBannerParams 同步
参数类改为 `instantEnergy` / `regenEnergyPerTurn` / `durationTurns` 三字段（默认 20/5/3，保持 `[System.Serializable]`），`case "SpiritBannerPassive"` 构造调用同步。

## 约束
只改 `SpiritBannerPassive.cs` + `PassiveFactory.cs`，其他一律不动。
