# 元素附着量（Gauge）系统 — Trae 需求提示词

## 背景

当前元素附着是简单的二值标记（挂元素→2回合倒计时→消失），改为量化的元素量系统。

## 核心约束

### 1. 元素量定义

- `PieceModel.AffixedElement` 保留（ElementType 枚举），新增 `AffixedElementGauge`（int，元素量）。
- `AffixedElementGauge <= 0` 等价于无附着元素，此时 `AffixedElement = None`。
- 普通攻击挂载 **1 点**元素量，大招挂载 **2 点**元素量。
- 每回合开始时，所有棋子 `AffixedElementGauge -= 1`（TickElementDuration 中处理）。
- UI 中附着元素图标的显示条件改为 `AffixedElementGauge > 0`（原来是 `AffixedElementDuration > 0`），不再需要 `AffixedElementDuration` 字段，**删掉它**。
- 没有 `AffixedElementDuration` 之后，棋子不会再"几回合后自动失去元素"——全靠每回合 -1 消耗归零。

### 2. 元素反应消耗量

- 反应触发时，双方消耗相同的**有效消耗量**：`min(底元素有效值, 触发元素有效值)`。
- 有效值计算：如果某方被对方克制，则该方的有效值不变；**克制方的有效值 × 2**。
- 克制方的实际消耗 = 有效消耗量 ÷ 2（整数除法，向下取整）。被克制方 / 无克制关系方 = 有效消耗量。
- 消耗后，哪方的实际 Gauge 有剩余，该方元素成为目标的新附着。双方都归零 → 目标无附着。
- 具体算法（纯整数，无浮点）：
  ```
  1. baseEff = baseGauge;   trigEff = triggerGauge;
  2. 如果触发方克底方 → trigEff *= 2；如果底方克触发方 → baseEff *= 2
  3. consume = min(baseEff, trigEff)
  4. 如果底方克触发方 → baseConsumed = consume / 2, trigConsumed = consume
     如果触发方克底方 → baseConsumed = consume, trigConsumed = consume / 2
     无克制关系        → baseConsumed = consume, trigConsumed = consume
  5. baseGauge -= baseConsumed; triggerGauge -= trigConsumed
  6. 若 baseGauge > 0 → 目标保留原底元素，Gauge = baseGauge
     若 triggerGauge > 0 → 目标附着变为攻击方元素，Gauge = triggerGauge
     若都 <= 0 → 清除附着
  ```

### 3. 元素克制（新增）

- **水克火**：水 ×2，火不变。
- **火克冰**：火 ×2，冰不变。
- 克制关系不涉及雷元素（雷与火/水/冰互不克制，有效值不调整）。
- 克制表：水 → 克 火；火 → 克 冰。单向（火不克水，冰不克火）。
- 克制仅影响反应消耗量，不影响伤害倍率。

### 4. 举例验证

| 场景 | 底 | 触发 | 反应 | 有效值 | consume | 消耗 | 结果 |
|------|----|------|------|--------|---------|------|------|
| 普+普 | 火×2 | 雷×1 | 超载 | 都不克，火2雷1 | min(2,1)=1 | 火1雷1 | 火剩×1 |
| 普+普 | 火×1 | 雷×2 | 超载 | 都不克，火1雷2 | min(1,2)=1 | 火1雷1 | 雷剩×1 |
| 大+大 | 火×2 | 水×2 | 蒸发 | 水克火→水×2=4 | min(2,4)=2 | 火2水2/2=1 | 水剩×1 |
| 普+普 | 冰×1 | 火×1 | 融化 | 火克冰→火×2=2 | min(1,2)=1 | 冰1火1/2=0 | 火剩×1 |
| 大+普 | 冰×2 | 火×1 | 融化 | 火克冰→火×2=2 | min(2,2)=2 | 冰2火2/2=1 | 火1-1=0→无 |
| 普+大 | 火×1 | 冰×2 | 融化 | 火克冰→火×2=2 | min(2,2)=2 | 火2冰2 | 火1-2无法→火0冰0→无 |

### 5. 改动范围约束

- **改 PieceModel**：删 `AffixedElementDuration`，加 `AffixedElementGauge`（int）。
- **改 PieceManager.AttackPiece**：攻击挂元素时 `target.AffixedElementGauge = normalAttack ? 1 : 2`。反应消耗逻辑按上述规则计算并重新设置。
- **改 PieceManager.UseUltimate**：大招攻击时传 `isUltimate=true` 给附着逻辑（或直接在 UseUltimate 中设 Gauge=2）。无害大招（冰障自身增益）不涉及此逻辑。
- **改 TurnManager.TickElementDuration**：将原来的 `AffixedElementDuration--` 改为 `AffixedElementGauge -= 1`，`<=0` 时清除。删掉原来基于 Duration 的倒计时逻辑。
- **改 ElementReactionTable 或 DamageCalculator**：加克制查询方法 `static bool IsCountered(ElementType attacker, ElementType defender)` → 水克火、火克冰。
- **改 UI_PieceStatsDisplay / UI_StatElement**：附着元素图标显隐条件从 `AffixedElementDuration > 0` 改为 `AffixedElementGauge > 0`。可选：在附着元素图标旁边显示 Gauge 数字（如 "2"）。

### 6. 不要动的

- 元素反应类型（蒸发/融化/超载/感电/冻结/超导）不变。
- 伤害倍率不变。
- DoT（感电）和冻结逻辑不变——它们和 Gauge 系统是独立的。
- `CurrentDefenseReduction`、`TemporaryDefenseBonus` 不变。
- ElementIconMap / StatusIconMap 不变。
- Equipment 系统不变。
