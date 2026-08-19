# Phase 2 框架优化清单（2026-08-15 最终版）

> 当前状态：83 个 .cs 文件，64 个 .asset 文件，18 个 .prefab，全部编译通过。
> **P0 / P1 全部完成，Phase 2 框架优化闭环。** 以下 P2/P3 为后续内容扩展方向。

---

## ✅ 已完成的优化

| # | 类别 | 项目 | 状态 | 备注 |
|---|------|------|------|------|
| 1 | 棋子 UI | UI_StatElement 扩展（防/移/射/能量/元素图标/附着元素量） | ✅ | 装备图标仍缺失 |
| 2 | 操作反馈 | 路径预览线（悬停空格显示到达路线） | ✅ | PathPreview + 颜色收进 GameConfig |
| 3 | 操作反馈 | R 键高亮模式切换（Move/Attack 分显） | ✅ | BattleModel.HighlightMode + InputHandler R 键 |
| 4 | 操作反馈 | 途经点系统（右键多段路径规划） | ✅ | BattleController._waypoints + FindPathWithWaypoints |
| 5 | 架构 | Range Provider 系统（MoveRangeConfig/AttackRangeConfig SO 驱动） | ✅ | 接口+工厂+13个asset，12种provider |
| 6 | 架构 | AreaAttackProvider 可配置溅射（5个独立参数） | ✅ | splashRadius/filterByAttackRange/splashIsElemental/splashGoldPercent/formula |
| 7 | 架构 | 元素附着量系统（AffixedElementGauge 替代 Duration） | ✅ | 普攻+1/大招+2/回合-1/克制×2/消耗取min |
| 8 | 渲染 | TileOverlay 六边形 Mesh（替换正方形 Quad） | ✅ | 静态共享 Mesh + Sprites/Default Shader |
| 9 | 渲染 | Overlay 尺寸自动适配 Model bounds | ✅ | GetComponentInChildren<MeshFilter>.sharedMesh.bounds |
| 10 | 调参 | Overlay 颜色收进 GameConfig | ✅ | 4组颜色 + 路径色/宽度，Inspector 一键调 |
| 11 | Bug修复 | HandleTileClick 高亮类型检查 | ✅ | Move 格只能移动、Attack 格只能攻击 |
| 12 | 操作反馈 | 伤害跳字（FloatingText 对象池系统） | ✅ | 伤害数字+反应提示，共用池，上漂+淡出 |
| 13 | 操作反馈 | 元素反应视觉提示（中文反应名） | ✅ | 蒸发/融化/超载等，自适应格式 |
| 14 | 棋子 UI | 棋子选中发光（MaterialPropertyBlock） | ✅ | URP/Lit _EmissionColor，零克隆零泄漏 |
| 15 | 棋子 UI | 装备图标（UI_StatElement + 商店 + 背包） | ✅ | equipSlots[3] + ShopItemRefs + EquipmentRefs |
| 16 | 调参 | FloatStyle 动画参数收进 GameConfig | ✅ | 5类的duration/floatDistance/fontSize + alphaFadeStart |
| 17 | 渲染 | 浮动文字面朝摄像机 | ✅ | FloatingText.Animate 中每帧跟踪 Camera.main |
| 18 | 渲染 | 浮动文字前半保持不透明、后半淡出 | ✅ | floatAlphaFadeStart 滑动条控制分界点 |
| 19 | 调试 | DebugManager（跳过选棋子/部署直接对战） | ✅ | Inspector 实时调参，删文件夹即移除 |

---

## ❌ 仍需优化的项目

### P1 — 已完成

| # | 项目 | 当前状态 |
|---|------|---------|
| 20 | 棋子移动动画（DOTween 逐格滑动 + Animator Walk/Run/Idle） | ✅ DOTween DOMove + 逐格 DORotateQuaternion + 输入锁 + 走跑切换 |
| 21 | 棋子已行动标记（变灰） | ✅ MPB _BaseColor 去饱和，复用 _mpb 与选中发光叠加，回合重置 |

### P2 — 后续做

| # | 项目 | 当前状态 |
|---|------|---------|
| 22 | 攻击命中 SFX | ❌ 项目零 AudioSource |
| 23 | UI 点击 SFX | ❌ |
| 24 | 攻击/受击/死亡/大招动画 | ❌ 需要 Animator 或程序化动画 |
| 25 | 元素反应 VFX（着火/冰冻/电击粒子） | ❌ 需要粒子系统或 Shader |
| 26 | 敌方棋子信息查看（能看不让动） | ❌ |
| 27 | 快捷键提示面板 | ❌ |
| 28 | Tab 切换棋子 | ❌ InputHandler 无 Tab 键 |
| 29 | 部署半透明预览 | ❌ |

### P3 — 锦上添花

| # | 项目 | 当前状态 |
|---|------|---------|
| 30 | BGM（战斗/选棋子/菜单） | ❌ |
| 31 | 金币飞入动画 | ❌ |
| 32 | 棋子死亡特效 | ❌ |
| 33 | 回合开始大字提示 | ❌ |

---

## 优先级速览

```
P1（2 项）：棋子移动动画 → 已行动标记
P2（8 项）：SFX → 动画/VFX → 敌方查看 → 快捷键 → Tab → 部署预览
P3（4 项）：BGM → 飞金 → 死亡特效 → 回合大字
```

---

## 当前最值得做的：棋子移动动画

P1 两项中，棋子移动动画（DOTween 0.3s 沿路径滑动）对观感提升最大。
当前 `SnapToPosition` 瞬移造成棋子像"瞬移棋子"，
加 0.3 秒滑动后整个棋盘会有物理存在感。
改动仅限 PieceView.SnapToPosition 方法体内——把
`transform.position = worldPos` 替换为 DOTween 插值。
