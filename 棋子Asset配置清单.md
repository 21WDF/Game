# 棋子 Asset 配置清单（克洛琳德 · 哥伦比亚 · 全量 Range 资产）

> Range 配置资产已由 `gen_range_configs.py` 脚本**批量重建完成**（17 个，语义化中文命名、不带数字）。
> 你只需在 Inspector 里把棋子的 `moveConfig` / `attackConfig` 拖到对应资产即可。
> 所有数值是占位，待 playtest 调整。

---

## 一、全量 Range 资产清单（已创建，位于 `Assets/Game/Resources/Range/`）

### 移动配置（Move/，8 个）

| 资产名 | providerClassName | baseRange | 说明 |
|--------|-------------------|-----------|------|
| 短距移动 | FreeMoveProvider | 2 | 通用移动·慢速（重装类） |
| 标准移动 | FreeMoveProvider | 3 | 通用移动·默认 |
| 长距移动 | FreeMoveProvider | 4 | 通用移动·快速 |
| 疾行移动 | FreeMoveProvider | 5 | 通用移动·极速（刺客类） |
| 直线移动 | StraightMoveProvider | 4 | 直线遇阻停（旧骑士，预留） |
| 直线穿越 | StraightPassProvider | 4 | 直线穿棋子（**克洛琳德**） |
| 跳格移动 | ParityMoveProvider | 4 | 偶数距离跳格（**哥伦比亚**） |
| 飞行移动 | FlyMoveProvider | 4 | BFS 飞越棋子（**莫娜**） |

### 攻击配置（Attack/，9 个）

| 资产名 | providerClassName | baseRange | 参数 |
|--------|-------------------|-----------|------|
| 近战攻击 | CircleAttackProvider | 1 | — |
| 中距攻击 | CircleAttackProvider | 2 | — |
| 远程攻击 | CircleAttackProvider | 3 | — |
| 超远程攻击 | CircleAttackProvider | 4 | — |
| 直线攻击 | LineAttackProvider | 3 | — |
| 溅射攻击 | AreaAttackProvider | 2 | splashRadius=1（物理，**哥伦比亚**） |
| 范围溅射 | AreaAttackProvider | 1 | splashRadius=2（元素） |
| 远程狙击 | MinMaxAttackProvider | 5 | minRange=2（**甘雨 / 安柏**） |
| 扇形攻击 | ConeAttackProvider | 3 | angle=60 |

---

## 二、9 棋子 → 配置对照（新命名）

| 棋子 | 移动 | 普攻 |
|------|------|------|
| 菲林斯（雷） | 长距移动 | 中距攻击 |
| 爱可菲（冰） | 标准移动 | 近战攻击 |
| 哥伦比亚（水） | 跳格移动 | 溅射攻击 |
| 杜林（火） | 标准移动 | 直线攻击 |
| 克洛琳德（雷） | 直线穿越 | 近战攻击 |
| 甘雨（冰） | 标准移动 | 远程狙击 |
| 雷电将军（雷） | 标准移动 | 近战攻击 |
| 莫娜（水） | 飞行移动 | 近战攻击 |
| 安柏（火） | 标准移动 | 远程狙击 |

> 移动范围具体值（短/标准/长/疾）待 playtest 按手感调，先按上表配，之后只改 `baseRange` 字段即可。

---

## 三、克洛琳德（雷）PieceData

`Create > Chess > Piece Data`，命名 `克洛琳德`：

| 分组 | 字段 | 值 |
|------|------|-----|
| 标识 | id / displayName | 自定唯一 / `克洛琳德` |
| 移动/攻击 | moveConfig | 拖入 `直线穿越` |
| 移动/攻击 | attackConfig | 拖入 `近战攻击` |
| 元素 | innateElement | `Thunder` |
| 内建被动 | builtInPassives[0].className | `VoltPathPassive` |
| 内建被动 | builtInPassives[0].jsonParams | `{"baseDamage":0,"attackPercent":0}` |
| 大招 | targetMode | `Tile` |
| 大招 | tileTargetShape | `Ray`（关键） |
| 大招 | tileTargetRange | `6` |
| 大招 | effectClassName | `ThunderDashUltimate` |
| 大招 | effectJsonParams | `{"healAmount":10}` |

---

## 四、哥伦比亚（水）PieceData

`Create > Chess > Piece Data`，命名 `哥伦比亚`：

| 分组 | 字段 | 值 |
|------|------|-----|
| 标识 | id / displayName | 自定唯一 / `哥伦比亚` |
| 移动/攻击 | moveConfig | 拖入 `跳格移动` |
| 移动/攻击 | attackConfig | 拖入 `溅射攻击` |
| 元素 | innateElement | `Water` |
| 内建被动 | builtInPassives[0].className | `RevivePassive` |
| 内建被动 | builtInPassives[0].jsonParams | `{"reviveHp":10}` |
| 大招 | targetMode | `Self` |
| 大招 | effectClassName | `TideSurgeUltimate` |
| 大招 | effectJsonParams | `{"radius":5,"bonusAttack":0,"duration":4}` |

---

## 五、PieceRegistry 注册

打开 `Assets/Game/Piece/Data/PieceRegistry.asset`，把新棋子拖进 `pieces` 列表（id 唯一）。

---

## ⚠️ 六、杜林、菲林斯 asset 现状问题（务必修正）

### 杜林.asset
- `displayName` 还是默认 `"Piece"` → 改 `杜林`
- `moveConfig` / `attackConfig` 空 → 拖入 `标准移动` / `直线攻击`
- `ultimateConfig` 空（大招没配）→ 补 `FlameLanceUltimate`（targetMode=Tile, tileTargetRange=6）

### 菲林斯.asset
- `builtInPassives[0]` 是 `FlameNovaPassive`（错，杜林的）→ 改 `SparkEmpowerPassive`（jsonParams `{"duration":6}`）
- `ultimateConfig` 是 `FireSlashUltimate`（错，战士的）→ 改 `ThunderSweepUltimate`（targetMode=Tile）
- `moveConfig` / `attackConfig` 的旧 guid 已随 Range 清空而失效 → 重新拖 `长距移动` / `中距攻击`

> 所有旧棋子的 moveConfig/attackConfig 引用都随 Range 清空而失效了，需要全部重新拖。

---

## 附：关键枚举速查

- 元素：None=0 / Fire=1 / Water=2 / Thunder=3 / Ice=4
- 大招目标模式：Enemy=0 / Self=1 / Tile=2
- 大招 Tile 形状：Circle=0 / Ray=1
