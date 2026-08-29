# 地下交易代价被动参数修复 · Trae 提示词

## 问题

`Assets/Game/Equipment/Effects/PassiveFactory.cs` 里，三期为地下交易新增的两个参数类**缺 `[System.Serializable]` 标记**：

- `BloodCostParams`（约 L346）
- `APCostParams`（约 L351）

后果：`JsonUtility.FromJson` 对非 `[Serializable]` 的类读不到字段，导致：
- `BloodCostPassive` 的 `hpPerTurn` 恒读默认值 2，用户配置 `{"hpPerTurn":5}` 不生效；
- `APCostPassive` 的 `apPerTurn` 恒读默认值 1，用户配置 `{"apPerTurn":3}` 不生效。

这个坑项目里早有明确经验——`PassiveFactory` 自己的注释（`ParseParams` 方法上方）写着「参数类必须标 [System.Serializable]，否则 JsonUtility 读不到字段（ExtraDamageParams 踩过的坑）」。同文件里所有其他参数类（`ExtraDamageParams`/`LifestealParams` 等）都标了，就这两个漏了。

## 改动方向

给 `BloodCostParams` 和 `APCostParams` 两个参数类补上 `[System.Serializable]` 标记，对标同文件其他参数类的写法。

## 红线

- 只补标记，不改任何其他逻辑（被动类、工厂 case、伤害挂点、状态机、UI 一律不动）。
- 不要顺手改默认值（`hpPerTurn=2`、`apPerTurn=1` 保持原样）。
