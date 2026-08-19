# 死亡之蔑延迟伤害跳字颜色修正

## 需求
死亡之蔑（DeathDancePassive）延迟伤害结算的跳字，颜色从「魔法紫」改成「物理白」——延迟伤害是物理直伤（继承受击的物理类型），不是魔法伤害。

## 改动方向
把 DeathDancePassive 延迟伤害结算处跳字的颜色参数，从 `DamageKind.Magical` 改为 `DamageKind.Physical`。

## 约束
- 只改这一处跳字的颜色参数，其余（扣血、受伤通知、免死、销毁等）一律不动。
- 只改 DeathDancePassive 一个文件，其余不碰。
