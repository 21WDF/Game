# 移动模板 + 范围 Provider 基础设施 · Trae 提示词

## 总览

为 9 个新棋子的内容实现铺设第一层基础设施：新增 **3 个移动范围 Provider**（FlyMove / StraightPass / ParityMove）和 **1 个攻击范围 Provider**（RingTangent），并接入 `RangeProviderFactory`。这 4 个 Provider 都是**纯几何计算**，不涉及棋子业务耦合，后续棋子的 `moveConfig` / `attackConfig` 或大招效果类可直接复用。

本阶段**不实现任何棋子的具体技能/被动**，只做范围 Provider 这一层。

---

## 一、现有接口与工具（务必对齐，不要自行臆造签名）

### 移动范围接口（`Assets/Game/Piece/Range/IMoveRangeProvider.cs`）

```
List<HexCoord> GetReachable(HexCoord from, int effectiveRange, Func<HexCoord, bool> isBlocked, ChessBoardModel model)
```

- `effectiveRange`：含装备加成的有效移动范围（由调用方传入，Provider 不固化步数）。
- `isBlocked`：外部注入的阻挡谓词（实为 `PieceLayoutModel.IsOccupied`），true = 该格有棋子。
- `model`：棋盘模型，用 `model.Contains(coord)` 做边界判定。

### 攻击范围接口（`Assets/Game/Piece/Range/IAttackRangeProvider.cs`）

```
List<HexCoord> GetAttackZone(HexCoord from, int effectiveRange, HexCoord? target, ChessBoardModel model)
```

- `target == null`（高亮阶段）：返回所有可攻击的格子。
- `target != null`（执行阶段）：返回实际攻击覆盖的格子。

### 工厂（`Assets/Game/Piece/Range/RangeProviderFactory.cs`）

- `CreateMoveProvider(string className, string jsonParams)`：现有 case 为 `FreeMoveProvider` / `StraightMoveProvider`（均无构造参数）。
- `CreateAttackProvider(string className, string jsonParams)`：现有 case 为 `CircleAttackProvider` / `MinMaxAttackProvider` / `AreaAttackProvider` / `LineAttackProvider` / `ConeAttackProvider`。

### HexCoord 关键方法（`Assets/Game/Core/HexCoord.cs`）

- `int Distance(HexCoord other)`：六边形距离（曼哈顿等价）。
- `HexCoord Neighbor(int directionIndex)`：6 方向邻居，索引 0=NE, 1=E, 2=SE, 3=SW, 4=W, 5=NW。
- `static List<HexCoord> AllCoordsInRadius(int radius)`：返回以原点为中心的半径内**相对坐标**列表（含原点），使用时需 `new HexCoord(from.q + offset.q, from.r + offset.r)` 平移到实际坐标。

---

## 二、新增移动 Provider（3 个）

三者均**无构造参数**（`effectiveRange` 由接口传入，对标 `FreeMoveProvider` / `StraightMoveProvider`）。

### 1. FlyMoveProvider（飞行移动 · 无视阻挡）

- **语义**：BFS 六方向扩散，**可飞越棋子**（`isBlocked` 为 true 的格子不拦截扩散，继续作为中转），但**落点必须是空格**（`isBlocked` 为 false 的格子才加入返回结果）。
- **与 FreeMoveProvider 的区别**：FreeMove 遇到棋子即停止扩散（该格不进入后续搜索）；FlyMove 飞越棋子继续搜索。
- **实现要点**：`isBlocked` 为 true 的格子仍需标记为已访问并继续入队扩散（防止重复访问），只是**不加入返回结果**。
- **边界**：`model.Contains` 为 false 不扩散；`effectiveRange <= 0` 返回空列表。
- **`isBlocked` 为 null 时**：视为无阻挡，所有可达格都加入结果（对标 FreeMoveProvider 的 null 守卫写法）。

### 2. StraightPassProvider（直线穿越移动 · 无视阻挡）

- **语义**：6 方向各发一条射线，**遇棋子不停（穿过）**，遇棋盘边界停止；落点只包含空格。
- **与 StraightMoveProvider 的区别**：StraightMove 遇棋子即 `break` 停止该方向；StraightPass 遇棋子**继续延伸**（该格不加入结果，但不中断射线）。
- **实现要点**：射线每一步，`model.Contains` 为 false → 停止该方向；`isBlocked` 为 true → 跳过该格（不加入结果）但继续下一步；`isBlocked` 为 false → 加入结果。
- **射线长度** = `effectiveRange`。

### 3. ParityMoveProvider（跳格移动 · 仅偶数距离）

- **语义**：返回 `effectiveRange` 内、到 `from` 的六边形距离（`Distance`）为**偶数**的所有空格。
- **跳跃式**：不受中间格阻挡（直接按距离筛选，不做 BFS，不因棋子而绕行）。
- **实现要点**：遍历 `HexCoord.AllCoordsInRadius(effectiveRange)` 平移后，筛出「`Distance(from, coord) % 2 == 0`」「排除 `from` 自身」「`model.Contains` 为真」「`isBlocked` 为 false」的格子。
- **注意**：距离为偶数的判定用 `HexCoord.Distance`（六边形距离），不是路径步数。
- **[PLACEHOLDER]**：跳跃式高机动是否过强，待 playtest 验证；本阶段按「不受阻挡」实现，不做 BFS 绕行。

---

## 三、新增攻击 Provider（1 个）

### RingTangentProvider（环切线 3 格）

- **语义**：以攻击者 `from` 为圆心，「横向 3 格」= 目标格 + 目标格在**同一环**上的左右 2 个邻居。
- **环的定义**：设目标格到 `from` 的距离为 `d`（`d >= 1`）。目标格的 6 个邻居中，到 `from` 距离**仍为 `d`** 的恰好 2 个（即环切线方向的左右邻居）。
- **高亮阶段（`target == null`）**：返回 `effectiveRange` 半径内的所有格子（不含 `from` 自身）——即可选目标位，对标 `AreaAttackProvider` 的高亮行为。
- **执行阶段（`target != null`）**：返回 `target` 本身 + `target` 的 2 个同环邻居（到 `from` 距离不变的那 2 个）。
- **边界处理**：
  - `target == from`（即 `d == 0`）：第 0 环只有中心一格，无同环邻居，执行阶段仅返回 `target` 自身。
  - 同环邻居可能落在棋盘外，须做 `model.Contains` 判定后只保留棋盘内的格子。
- **无构造参数**。
- **用途说明**：主要供后续大招（菲林斯 / 雷电将军的「横向 3 格」）复用几何计算；作为 `IAttackRangeProvider` 实现是为了复用 `RangeProviderFactory` 与 `BattleController` 现有的范围调用链，本阶段无需接入具体棋子。

---

## 四、改动文件

1. **新建** `Assets/Game/Piece/Range/FlyMoveProvider.cs`（实现 `IMoveRangeProvider`）
2. **新建** `Assets/Game/Piece/Range/StraightPassProvider.cs`（实现 `IMoveRangeProvider`）
3. **新建** `Assets/Game/Piece/Range/ParityMoveProvider.cs`（实现 `IMoveRangeProvider`）
4. **新建** `Assets/Game/Piece/Range/RingTangentProvider.cs`（实现 `IAttackRangeProvider`）
5. **修改** `Assets/Game/Piece/Range/RangeProviderFactory.cs`：
   - `CreateMoveProvider` 追加 3 个 case：`FlyMoveProvider` / `StraightPassProvider` / `ParityMoveProvider`。
   - `CreateAttackProvider` 追加 1 个 case：`RingTangentProvider`。

---

## 五、约束（务必遵守）

1. **不改** `IMoveRangeProvider` / `IAttackRangeProvider` 接口签名，不改现有 7 个 Provider（FreeMove / StraightMove / Circle / MinMax / Area / Line / Cone）的任何行为。
2. 4 个新 Provider 均**无构造参数**，工厂 case 直接 `new XxxProvider()`（对标 FreeMoveProvider / StraightMoveProvider 写法）。
3. 所有 Provider 都做 `model.Contains` 边界判定与 `effectiveRange <= 0` 空返回（对标现有 Provider 风格）。
4. FlyMove / StraightPass / ParityMove 的**落点必须空格**：`isBlocked == true` 的格子绝不加入返回结果（FlyMove / StraightPass 允许作为中转穿过，但不作为落点）。
5. RingTangent 的「同环邻居」判定必须用 `HexCoord.Distance`（到 `from` 的距离不变），不是简单相邻关系，也不是到 `target` 的距离。
6. 注释用中文，每个类顶部 `///` 说明「语义 + 与现有模板的区别」，风格对标现有 Provider。
7. 新增 `.cs` 文件配套的 `.meta` 由 Unity 自动生成，**不要手写 meta**。
8. 诊断无报错后收尾。
