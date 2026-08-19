# 四元素之力数值参数化 · 修复提示词

## 问题

`ElementalCorePassive.Modify` 里的数值全部硬编码（倍率 +0.2f、DotTurns +2、FreezeTurns +1、DefenseReduction +5、DefenseReductionTurns +2），无法通过 jsonParams 统一修改。

## 修复：把写死数值全部参数化

### 改动 1：`ElementalCorePassive.cs` — 构造加 5 个数值参数，Modify 改用字段

构造签名从 `(ElementType element, string uniqueId)` 扩展为：

```
(ElementType element, string uniqueId,
 int multiplierPercent, int electroChargeTurns, int freezeTurns,
 int superconductDefense, int superconductTurns)
```

5 个数值字段存为 `readonly int`，`Modify` 里把写死的常量替换为字段：

| element | Modify 逻辑（用字段替换写死值） |
|---|---|
| Water | Vaporize → `DamageMultiplier += _multiplierPercent / 100f`；ElectroCharged → `DotTurns += _electroChargeTurns`；Frozen → `FreezeTurns += _freezeTurns` |
| Fire | Vaporize / Melt → `DamageMultiplier += _multiplierPercent / 100f` |
| Thunder | Superconduct → `DefenseReduction += _superconductDefense` 且 `DefenseReductionTurns += _superconductTurns`；ElectroCharged → `DotTurns += _electroChargeTurns` |
| Ice | Superconduct → `DefenseReduction += _superconductDefense` 且 `DefenseReductionTurns += _superconductTurns`；Melt → `DamageMultiplier += _multiplierPercent / 100f`；Frozen → `FreezeTurns += _freezeTurns` |

倍率用 int 百分比（`_multiplierPercent / 100f`），与光界之力 `percent/100f` 口径一致。其余逻辑（`Type == None` 返回、`changed` 标记、`UniqueId` 覆写、空实现钩子）不变。

### 改动 2：`PassiveFactory.cs` — 参数类 + case

- `ElementalCoreParams` 加 5 个字段（全默认保持当前行为）：

```csharp
[System.Serializable]
private class ElementalCoreParams
{
    public string element = "Water";
    public int multiplierPercent = 20;   // 倍率加成百分比（+= percent/100f，即 +0.2）
    public int electroChargeTurns = 2;   // 感电延长回合
    public int freezeTurns = 1;          // 冻结延长回合
    public int superconductDefense = 5;  // 超导减防加成
    public int superconductTurns = 2;    // 超导减防持续延长回合
}
```

- `case "ElementalCorePassive"` 改为读这 6 个字段并传入构造（`ParseElement(ec.element)` + 5 个数值）。

## 约束（务必遵守）

1. **只改这两个文件**：`ElementalCorePassive.cs`（构造 + Modify 用字段）、`PassiveFactory.cs`（参数类 + case）。其他文件一律不动。
2. 默认值保持当前行为（20/2/1/5/2），四件装备只填 `element` 即与现在完全一致。
3. 不要动 `Type == None` 判断、`changed` 标记、`UniqueId` 覆写、唯一被动逻辑。
4. 诊断无报错后收尾。
