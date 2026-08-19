# 伤害类型系统 · A 阶段（DamageKind 枚举 + 来源打标签）· Trae 提示词

## 需求

引入伤害类型三分类（`DamageKind`），统一约束"受不受防御减伤、反甲反不反"两件事。本阶段只做**基础**：加枚举 + 各伤害来源打标签 + 通知链传类型。

三种类型：
- **Physical（物理）**：受防御减伤，触发反甲。普攻、大招、溅射。
- **Magical（魔法）**：不受防御减伤，不触发反甲。感电 DoT、反甲反伤、超载爆炸。
- **True（真实）**：不受防御减伤，不触发反甲，未来无视护盾。

## 改动方向

1. **加 `DamageKind` 枚举**（Physical / Magical / True），放在与 `DamageSource` 相同的位置（核心枚举处）。
2. **伤害通知链加 `damageKind` 参数**：`OnDamageReceived` 和 `ApplyIncomingDamage` 增加伤害类型参数（默认 Physical，向后兼容）。
3. **各伤害来源打标签**：
   - 普攻（AttackPiece）→ Physical
   - 大招（FireSlashUltimate）→ Physical
   - 元素溅射 / 非元素溅射 → Physical
   - 感电 DoT（TurnManager）→ Magical
   - 反甲反伤（ThornsPassive）→ Magical
   - 超载爆炸（ApplyOverloadExplosion）→ Magical

## 约束（务必遵守）

1. **本阶段只加标签和传参**，不要改伤害计算公式、不要改反甲/减伤逻辑（那些是 B 阶段）。
2. `DamageSource`（现有来源枚举）本阶段**保留不动**，`DamageKind` 是新增的独立维度，两者并存。
3. 附加伤害、死亡之蔑延迟伤害的"真实/继承"类型，本阶段**不处理**，留后续。
4. 参数默认值保持向后兼容（新参数默认 Physical，不影响现有调用）。
5. 诊断无报错后收尾。
