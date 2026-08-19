# 装备系统 阶段① Trae 提示词

```
在 Chaotic Chess 中实现装备系统第一阶段：数据层 + 纯属性装备 + 每回合回蓝。
本阶段只做三件事，不碰被动框架、不碰元素反应修饰。

## 改动 1：EquipmentData 加回蓝字段

Assets/Game/Equipment/Model/EquipmentData.cs 加一个字段（放在 bonusAttackRange 附近）：

public int bonusEnergyPerTurn = 0;  // 每回合回能量（蓝量）

不改其他任何字段。

## 改动 2：实现每回合回蓝机制

在 TurnManager 的回合开始方法（TickElementDuration 附近，遍历棋子的循环里）加回蓝：

- 遍历当前行动方的所有棋子
- 对每个棋子，累加其 EquippedItems 中所有装备的 bonusEnergyPerTurn 之和
- 如果和 > 0，调 piece.Energy.Gain(和)（Energy 为 null 时跳过）
- 回蓝时机 = 该棋子所属方回合开始时

参考现有元素残留递减（TickElementDuration）的遍历位置和写法，回蓝逻辑并进去。
Energy.Gain 已有 MaxEnergy 上限保护，直接调用即可。

## 改动 3：创建 17 件装备 .asset（只填属性，passives 留空）

在 Assets/Game/Resources/Equipment/ 下创建 17 个 .asset，
复用现有 EquipmentData 的 asset 格式（参考新手剑.asset）。
只填 id / displayName / price / 属性字段，passives 数组留空（阶段②③再填）。

清单（id / 名称 / 价格 / 属性）：

5  生命铠甲   120   bonusHP=20
6  死亡之刃   132   bonusAttack=15
7  英灵幡      95   bonusEnergyPerTurn=5
8  石像鬼板甲 125   bonusDefense=20
9  荆棘刺甲   118   bonusDefense=10, bonusAttack=5
10 饮血剑     113   bonusAttack=10
11 军团圣盾   107   bonusDefense=8
12 枯萎宝珠   103   bonusEnergyPerTurn=1
13 死亡之蔑   113   bonusAttack=10
14 中娅悖论   113   bonusEnergyPerTurn=2
15 探索者护臂  96   bonusDefense=5, bonusAttack=5
16 诸葛连弩    92   bonusAttack=6
17 光界之力   100   bonusAttack=3
18 潮涌之力   100   bonusEnergyPerTurn=2
19 灼火之力   100   bonusAttack=1
20 陨雷之力   100   bonusAttack=1
21 凝冰之力   100   bonusAttack=1

所有 17 件的 passives 数组保持为空（不填 className）。
id 用上面的 5~21（旧装备已占 1/2/4，火焰护符 id=3 已移除）。

## 约束（绝对不碰）

- 不改 IPassiveEffect 接口（被动框架阶段②再做）
- 不扩展事件钩子（OnTurnStart/OnDamageReceived 等）
- 不改元素系统 / IReactionModifier / ElementReactionTable
- 不改 PieceModel 的被动相关逻辑
- 不改槽位 / 商店 / 背包 / 装卸机制
- 17 件装备的 passives 字段留空，不填任何 className
- 不改已有的 3 件旧装备（新手剑/橡木盾/迅捷之靴）

## 验收

- [ ] EquipmentData 有 bonusEnergyPerTurn 字段
- [ ] 装备英灵幡（回5蓝）后，该棋子每回合开始 +5 能量
- [ ] 17 件装备 asset 都在 Resources/Equipment/ 下，Inspector 里属性正确
- [ ] 17 件装备的 passives 全为空
- [ ] 旧 3 件装备不受影响
```
