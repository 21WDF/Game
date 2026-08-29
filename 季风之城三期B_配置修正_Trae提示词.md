# 季风之城三期B_配置修正_Trae提示词.md

> 铁律：只写「改动方向 + 效果目标 + 红线」，不写实现细节。

## 〇、背景（一句话）

三期B 幸运方块主体已实现、核心逻辑正确，但 `LootBoxTier` 的**默认档位配置**与设计档位表不符（超高档多开出了城邦装备/道具、低档多开出了初级装备），且结构缺「无普通装备」的表达。本次只修配置默认值 + 补一个开关，不动任何逻辑。

## 一、现状（问题定位）

1. `LootBoxTier` 现有字段：`tierName / color / weight / equipmentTier / minCityPrice / maxCityPrice / goldMin / goldMax`。
2. **问题①**：`equipmentTier` 无「不含普通装备」的表达——`FilterNormalEquipments` 用「`eq.tier == tier.equipmentTier`」筛选，任何档位都必然开出对应 tier 装备，无法表达「低档无装备」。
3. **问题②**：默认 4 档的数值与设计档位表不符——
   - 超高（红）`minCityPrice=50 / maxCityPrice=9999` → 错误地开出了「城邦装备≥50 + 道具≥50」，设计里超高只含「高级装备 + 金币」；
   - 低（灰）`equipmentTier=Basic` → 错误地开出了「初级装备」，设计里低档只含金币。
4. 「城邦装备/道具」共用一个价格范围（`minCityPrice/maxCityPrice`），「空」用 `maxCityPrice < minCityPrice` 表达（现状低档 `0/-1` 已是此约定，`FilterCityEquipments`/`FilterItems` 对空范围自然筛空）。

## 二、改动方向

### 1. `LootBoxTier` 补「是否含普通装备」开关

- 新增字段 `includeNormalEquipment`（bool，默认 true）。
- `FilterNormalEquipments` 在该字段为 false 时**直接返回空**（不再按 `equipmentTier` 筛选）——低档等「无装备」档位用此表达。

### 2. 修正默认 4 档配置（对齐设计档位表）

设计档位表（默认值仅作字段初始值）：

| 档位 | includeNormalEquipment | equipmentTier | 城邦/道具价格范围 | 金币范围 |
|------|----------------------|--------------|------------------|---------|
| 超高(红) | true | Advanced | 空 | 100~150 |
| 高(金) | true | Intermediate | ≥50 | 20~60 |
| 中(蓝) | true | Basic | <50 | 10~20 |
| 低(灰) | false | 忽略 | 空 | 1~6 |

- 超高：城邦/道具价格范围改为「空」（`maxCityPrice < minCityPrice`，如 `minCityPrice=50 / maxCityPrice=-1`）。
- 低：`includeNormalEquipment=false`（不再开出初级装备）。

## 三、效果目标（验收标准）

1. 超高档开出内容只含「高级装备（tier=Advanced）」与「金币 100~150」，无城邦装备、无道具。
2. 低档开出内容只含「金币 1~6」，无任何装备。
3. 高档/中档内容不变（高=Intermediate + 城邦/道具≥50 + 金币20~60；中=Basic + 城邦/道具<50 + 金币10~20）。
4. 档位字段仍全部 Inspector 可配，代码零硬编码。

## 四、红线（绝不能破坏）

1. **只改配置默认值 + 加开关，不动任何逻辑**：掉落决策、平衡保底、路径拾取、开出随机、商店关闭、内聚过滤——全部零改动。
2. `FilterCityEquipments` / `FilterItems` / `PickupBox` / 掉落/拾取流程零改动（只在 `FilterNormalEquipments` 加开关拦截 + 改配置默认值）。
3. 数据驱动：新开关与修正后的默认值仍是字段，代码零硬编码。

## 五、明确不做

- 不改「城邦装备/道具共用一个价格范围」的结构（设计里二者同步，够用）。
- 不改雷暴（三期C）任何内容。
