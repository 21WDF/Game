# 范围配置内联重构 · Trae 提示词

## 目标

当前移动 / 攻击范围通过独立的 ScriptableObject 资产（`MoveRangeConfig` / `AttackRangeConfig`）配置，`PieceData` 里用引用字段指向它们。这导致每个「provider 类型 × 范围参数」组合都要建一个资产，资产数量随内容膨胀。

改为：**把移动 / 攻击范围配置直接内联进 `PieceData`**，删掉这两个 SO 类型。让 `PieceData` 成为棋子的唯一自描述来源——标识、属性、移动、攻击、被动、大招全在一个文件里，不再跳转到别的资产。

## 需要实现

1. `PieceData` 增加移动、攻击的内联配置（各自承载「provider 类型 + 参数 JSON + 范围值」三项，组织成一个内联配置块，而不是散落多个字段）
2. 删除 `MoveRangeConfig`、`AttackRangeConfig` 两个 SO 类型
3. 所有读取 `moveConfig` / `attackConfig` 的地方，改为读内联配置（移动范围计算、攻击范围计算、路径生成、选人界面的移动/攻击数值展示）
4. 清理废弃的 `moveRange` / `attackRange` 字段及其「为空时回退」的兜底逻辑（内联后配置字段就是唯一来源，不再需要回退链）

## 涉及范围（帮助定位，具体改法由你决定）

- `PieceData`（字段结构）
- `PieceModel`（`MoveRange` / `AttackRange` 的读取）
- `ChessBoardController`（移动可达坐标、攻击覆盖坐标、路径生成几个方法当前接收 SO 参数）
- `BattleController`（移动/攻击高亮、路径预览、溅射结算、大招瞄准等多处引用）
- `UI_UnitSelection`（选人界面展示移动/攻击数值）
- 删除 `MoveRangeConfig.cs`、`AttackRangeConfig.cs`

## 不要破坏（红线）

1. `RangeProviderFactory` 按类名创建 provider 的核心逻辑不变（内联后仍由「类名 + 参数 JSON」驱动工厂）
2. 所有 provider 类及其几何算法不变（FreeMove / StraightMove / FlyMove / StraightPass / ParityMove / Circle / Line / Area / MinMax / Cone / RingTangent）
3. 路径生成行为不变（直线遇阻停 / 直线穿越 / 飞行飞越 / 跳格直达，各按各自规则）
4. 大招框架、伤害管道、被动框架、元素反应系统不受影响

## 验收要点

- 删除两个 SO 类型后编译通过，无残留引用
- 各棋子的移动 / 攻击范围行为不变（通用移动、直线穿越、跳格、溅射等）
- `PieceData` 里能直接配置每个棋子的移动 / 攻击 provider 类型、参数、范围值
- 废弃的 `moveRange` / `attackRange` 字段及其回退逻辑被彻底清除
