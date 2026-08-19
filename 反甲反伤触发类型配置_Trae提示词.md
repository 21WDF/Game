# 反甲反伤触发类型可配置 · Trae 提示词

## 目标
反甲（ThornsPassive）目前响应所有非反伤伤害（普攻/大招/溅射/DoT），无法区分。改为：加一个可配置的「反伤触发类型」位掩码，设计者可在 .asset 里自定义反甲响应哪些伤害来源。

## 改动

### 1. 定义 DamageSource 枚举（[System.Flags]，放 IPassiveEffect.cs 顶部）
```
[System.Flags] enum DamageSource { Attack = 1, Splash = 2, Ultimate = 4, Dot = 8, Reflect = 16 }
```
- Attack = 普攻；Splash = 元素 + 非元素溅射；Ultimate = 大招；Dot = 感电 DoT；Reflect = 反伤

### 2. OnDamageReceived 签名改
`bool fromReflect` → `DamageSource source`（默认 `DamageSource.Attack`）

### 3. ApplyIncomingDamage 签名改
`bool fromReflect` → `DamageSource damageType`（默认 `DamageSource.Attack`）

### 4. AttackPiece 加参数区分溅射
- 加 `DamageSource damageSource = DamageSource.Attack` 参数
- 元素溅射调用处（BattleController.ApplySplashDamage）传 `damageSource: DamageSource.Splash`
- AttackPiece 内部触发 OnDamageReceived 时传 `damageSource`

### 5. 各伤害路径传对应类型
- 普攻（AttackPiece 主目标）：Attack（默认）
- 元素溅射：Splash
- 非元素溅射：`ApplyIncomingDamage(..., DamageSource.Splash)`
- 大招：`ApplyIncomingDamage(..., DamageSource.Ultimate)`
- DoT：`ApplyIncomingDamage(..., DamageSource.Dot)`
- 反伤：`ApplyIncomingDamage(..., DamageSource.Reflect)`

### 6. ThornsPassive 加 reflectTriggers 参数
- 加参数 `reflectTriggers`（int，DamageSource 位掩码，默认 7 = Attack|Splash|Ultimate，即默认不响应 DoT）
- OnDamageReceived 里：
  - `(source & DamageSource.Reflect) != 0` → return（反伤防递归）
  - `(reflectTriggers & source) == 0` → return（未勾选该伤害类型）

### 7. RegenPassive.OnDamageReceived 签名改
加 `DamageSource source` 参数（方法体忽略其值，任何实际减血都归零计时）

### 8. PassiveFactory.ThornsParams 加 reflectTriggers 字段
加 `public int reflectTriggers = 7;`（保持 [System.Serializable]）

## 约束（必须遵守）
1. 只改：DamageSource 枚举 + IPassiveEffect / PieceManager / BattleController / FireSlashUltimate / TurnManager / ThornsPassive / RegenPassive / PassiveFactory，其他不动
2. 反伤仍不触发元素反应 / 金币 / 能量 / 吸血（ApplyIncomingDamage 只通知受伤害，语义不变）
3. 默认 reflectTriggers = 7（普攻 + 溅射 + 大招，不响应 DoT）；DoT 反伤需显式配 15（全响应）
4. 完成后自查诊断无报错即可，运行时验证由我方做
