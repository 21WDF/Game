# 免死响应延迟伤害致死 · 修复提示词

## 问题

`DeathDancePassive.OnTurnStart` 里延迟伤害结算致死后，直接 `DestroyPiece(owner)`，**绕过了免死（DeathDefyPassive）介入**。导致同时装备「死亡之蔑 + 中娅悖论」时，延迟伤害的致命结算会把棋子直接打死，免死不触发。

而其他致死路径（普攻/大招/DoT/溅射/反伤）都走 `ApplyIncomingDamage` 或 `AttackPiece`，其内部已调用 `TryDefyDeath`，唯独延迟伤害漏了。

## 修复方案

### 改动 1：`PieceManager.cs` — `TryDefyDeath` 改为 public

当前定义（约 356 行）：

```csharp
private static void TryDefyDeath(PieceModel target)
```

改为：

```csharp
public static void TryDefyDeath(PieceModel target)
```

（方法体不变，仅把访问级别从 private 提升到 public，供 DeathDancePassive 调用。它是 static，静态调用 `PieceManager.TryDefyDeath(owner)` 即可，不依赖 Instance。）

### 改动 2：`DeathDancePassive.cs` — 致死前先尝试免死

当前 `OnTurnStart` 末尾（约 82-83 行）：

```csharp
if (owner.IsDead)
    PieceManager.Instance?.DestroyPiece(owner);
```

改为：

```csharp
if (owner.IsDead)
{
    PieceManager.TryDefyDeath(owner);   // 免死响应一切致死（含延迟伤害）
    if (owner.IsDead)                    // 免死成功则不再死亡，跳过销毁
        PieceManager.Instance?.DestroyPiece(owner);
}
```

## 约束（务必遵守）

1. **只改这两个文件**：`PieceManager.cs`（TryDefyDeath 访问级别）、`DeathDancePassive.cs`（致死前先免死）。其他文件一律不动。
2. **不改免死（DeathDefyPassive）本身的逻辑**，不改介入顺序，不改其他致死路径。
3. 免死触发后棋子回到 1 HP，本回合延迟伤害的结算已完成（延迟池照常 `_pendingDamage -= settle` 递减），这是正确语义——免死只把"致命"拉回 1 HP，不影响延迟伤害后续回合继续结算。
4. 诊断无报错后收尾。
