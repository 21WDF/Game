# Chaotic Chess 项目长期记忆

## 项目身份
- **Chaotic Chess（混沌棋局）**：本地双人热座 Hex 战棋
- Unity 6 LTS (6000.3.19f1) + URP + MVC + ScriptableObject
- Phase 2 中后期，核心循环完整可玩

## 用户约定
- 中文命名习惯（设计文档、SO 资产）
- 分析报告放 `.workbuddy/artifacts/` 或项目根目录，不污染 Assets
- Trae 提示词放在项目根目录，用于 AI 辅助开发迭代
- **Trae 提示词风格（重要）**：只给「改动方向 + 效果目标 + 不要破坏什么（红线）」，**不写实现细节**（不加方法名建议、不写算法步骤、不给伪代码）。分工：我出方向 → Trae 实现 → 我最后核对。禁止把「怎么做」写进提示词（否则 Trae 变成打字机，我出错它必然跟着错）
- 提示词核对范围由我判断：简单改动读源码逐行核对，复杂改动（涉及 BattleController 状态机/伤害管道/回合流转）升级到追踪调用链 + 交叉验证
- **配置分工（8/19 更新）**：asset 配置（PieceData 字段填写、className/jsonParams 引用）由**用户自己做**，Trae 只写代码；Excel 配置表**保留**（用于后期数值平衡，不是中间产物）

## 唯一被动语义（8/19 明确）
- 唯一被动（UniqueId 去重）**仅作用于装备被动**（EquippedItems），角色被动（builtInPassives）不参与去重
- 合理性：角色被动是出厂静态配置（一个棋子配一次），不会重复，去重对其无意义；复用装备被动模板（IPassiveEffect + PassiveFactory）是合理 DRY，不拆两套
- 未来缺口（当前不存在）：若某棋子角色被动与装备被动同名需互斥，需扩展去重逻辑（当前 9 棋子角色被动都是专属效果，不与装备同名，暂不处理）

## 目录结构（8/19 重构完成）
- `Piece/Range/Move/`：7 个移动模板（IMoveRangeProvider + Free/Straight/Fly/StraightPass/ParityMove + MoveRangeConfig）
- `Piece/Range/Attack/`：8 个攻击模板（IAttackRangeProvider + Circle/Line/Area/MinMax/Cone/RingTangent + AttackRangeConfig）
- `Piece/Range/`：RangeProviderFactory.cs（统一工厂，跨 Move/Attack）
- `Piece/Passives/`：角色被动（FlameNovaPassive，杜林；后续棋子角色被动都放这）
- `Piece/Effects/`：大招（IUltimateEffect + FireSlash/IceBarrier/FlameLance）
- `Equipment/Effects/`：被动框架（IPassiveEffect + PassiveFactory）+ 14 装备被动 + Elemental/（元素装备被动）
- 重构方式：Bash mv 成对移动 .cs + .meta（GUID 不变，引用不断）；C# 类引用按类名不按路径，编译不受影响

## 开发进度（2026-08-18）
- 被动系统：17 种被动效果，事件驱动框架完整（8 钩子 + 唯一被动去重 + 伤害介入链）
- 元素系统：4 元素 6 反应 + Gauge 消耗算法 + ReactionDatabaseSO 数据驱动
- 伤害系统：DamageSource 位掩码 + DamageKind 三分类 + 附加伤害独立段
- 装备：20 件（Basic 3 + Intermediate 12 + CityState 5）
- 棋子：9 种，可插拔 Range Provider（6 种 + 12 资产）
- 跳字：FloatingTextPool + 元素颜色映射

## 关键风险
1. ~~无 git~~（已解决 8-20：git init + 多次提交）
2. 城邦系统实现中（Pillar 3，8-24 启动框架）
3. 8/9 棋子用 Cube 占位
4. 无音效
5. GetAllPassives 每次分配新 List（GC 风险）
6. 两个第三方插件（Pmx2Fbx/GenshinToonShader）内部嵌套 .git，主仓库未追踪其内容（待用户拍板）

## Playtest 失败信号
- 单局 >40min
- 金币囤积 >120
- 某棋子/装备组合胜率 >70%

## 明确未完成项（8/18 确认）
- 7 棋子大招未做（仅战士烈焰斩/法师冰障；弓手箭雨/骑士冲锋/刺客背刺/牧师治疗/重装眩晕/术士召唤/游侠传送已规划未实现）
- 3 移动模板未落地（FlyMove/BlinkMove/JumpMove，当前仅 Free/Straight）
- 9 棋子 builtInPassives 均空（模板已规划战士反击/法师减伤等）
- 5 大招模板未实现（治疗/召唤/控制/位移/AOE，缺 IsStunned/IsSilenced 字段）

## 设计哲学
- "模板化"渐进扩展：每个系统都有「现有模板 + 待新增模板」规范文档
- 历史档案在 `C:\Users\MECHREVO\WorkBuddy\2026-07-27-23-05-25`（22 天开发档案）
- 无移动次数限制（可 Hit&Run），待 playtest 验证
- 配置工作流：gen_passive_config.py 生成 Excel 配置生成器（表单驱动替代手写 JSON）

## 9 棋子内容规格（2026-08-18 定稿，原神角色名，替换旧 Warrior/Knight 等）
- 菲林斯(雷)·蓄力爆发 / 爱可菲(冰)·控场持续 / 哥伦比亚(水)·团队辅助 / 杜林(火)·直线穿透 / 克洛琳德(雷)·冲锋 / 甘雨(冰)·蓄力狙击 / 雷电将军(雷)·能量引擎 / 莫娜(水)·领域控制 / 安柏(火)·召唤嘲讽

## 4 个关键设计决策（用户拍板，长期有效）
1. 全局被动 = 在场即生效（哥伦比亚复活、雷电将军回能，死亡后失效）
2. 甘雨蓄力完全替代能量（无能量条，蓄力层数 0-3 驱动大招）
3. "横向3格" = 环切线3格（目标格 + 同环左右 2 邻居）→ 新 Provider `RingTangentProvider`
4. 嘲讽 = 强制锁定（安柏傀儡在场，敌方本回合只能攻击傀儡）
5. **范围配置内联 PieceData（8/19 拍板）**：砍掉 MoveRangeConfig/AttackRangeConfig 两个 SO 资产层，移动/攻击范围配置（provider 类型+参数+范围）直接内联进 PieceData，避免资产爆炸；RangeProviderFactory 保留（按类名创建 provider）

## 伤害类型与元素附着定义（8/19 用户澄清，长期有效）

**伤害类型三分类（DamageKind）与防御/护盾的关系**：
- 物理（Physical）：受**防御 + 护盾**影响
- 魔法（Magical）：**仅受护盾**影响（不受防御减伤）
- 真实（True）：**无视防御 + 护盾**

**元素附着默认规则**：角色造成的**任何直接伤害**（普攻/大招/被动）默认带元素（附着角色 innateElement、触发元素反应），**除非有特别说明**（反伤/DoT/超载爆炸等非"角色主动直接伤害"可能不附着）。

**关键：伤害类型 与 元素附着 是正交维度**——物理伤害也可以带元素（如克洛琳德路径伤害 = 物理类型 + 雷元素）。不要混淆"物理"和"不附着元素"。

**当前实现状态**：防御维度已对（物理受防御、魔法/真实不受，见 GetTotalDamageReduction 按 kind 筛选）；护盾维度**尚未实现**（DamageKind 注释"未来无视护盾"，无 Shield 系统）。护盾是实现后的未来扩展，当前护盾=0 不影响。

**落实方案（8/19 用户拍板）**：护盾**暂不实现**（只定规则）；直接伤害**一次性全改**为默认带元素，并为每个直接伤害效果类加一个「是否带元素」的配置参数（默认 true，false 保持不附着）。待改：克洛琳德路径伤害(VoltPath=物理+雷)、杜林溅射(FlameNova=物理+火)、菲林斯强化态魔法段(ThunderSweep=魔法+雷)。保持不附着（特例）：反伤/感电DoT/超载爆炸。AreaAttackProvider 的 splashIsElemental 已是开关，无需改。

## 跳字视觉规范（8/20 用户拍板，长期有效）
伤害数字跳字**两个正交维度分开表达**：
- **颜色 = 元素**（火/水/雷/冰；无元素城邦用默认：物理红 / 魔法紫 / 真实白，值在 GameConfig floatDamagePhysical/Magical/True）
- **样式 = 伤害类型**：物理 = 纯色块；魔法 = 颜色渐变；真实 = 深色边框

跳字组件是 TextMeshPro（FloatingText.text = TextMeshProUGUI），渐变用 colorGradient、边框用 Outline 组件。反应名提示（「蒸发！×2」）独立紫色跳字保持现状，伤害数字不再被「是否反应」染色（isReaction 参数语义调整）。已出提示词 `伤害类型跳字样式_Trae提示词.md`。

## 城邦系统设计（8-24 定稿，Pillar 3）
- **6 城邦**：贸易(拍卖/金币)、欢愉(卡牌/手牌)、季风(四季/buff)、邪疑(卧底/棋子归属)、战争(将帅/主将死亡即败)、元素(染色/护盾)。元素城邦是第 6 个，染色是它的专属机制（其他城邦无染色）。
- **城邦选择规则（8-25 细化）**：双方各选 k 个心仪城邦 → 取重合部分 → 随机定 1 个。k = floor(城邦数/2)+1（6 城邦 = 4 个），鸽笼原理保证交集非空（2k > N）。
- **暗选/联机预留（8-25 拍板）**：选择流程按「双方各自独立提交、双方提交后自动结算」建模（天然即联机「同时选择、提交后确定」模型）。「暗选」是伪问题——遮不遮罩只进 UI 渲染层，不进逻辑层。本地热座「不遮罩」仅渲染策略；UI 选择/提交/结算逻辑必须传输无关，将来联机只需把「谁在选择」的驱动从本地轮流换成网络同步，逻辑复用。后端 SubmitSelection 的各自提交模型早已为此预留。
- **机制按城邦过滤（8-25 拍板）**：城邦确定后，只有当前城邦的专属机制生效。现状是骨架期「机制常驻跑通」遗留——染色靠 GameConfig.shieldElementDyeingEnabled 全局开关、贸易 3 Manager（拍卖/利息信誉/地下交易）无条件常驻。要改成：元素染色 = 「当前城邦==Element 且 master 开关」；贸易机制 = 「当前城邦==Trade」。其余 4 城邦（欢愉/季风/邪疑/战争）机制未实现，选中后暂无专属机制（只有商店城邦装备池变化）。
- **每城邦 = 专属装备池（tier=CityState）+ 专属机制**（统一走 ICityStateMechanic 钩子）。
- **在线 PVP 是最终目标**（用户强调）：框架须与传输分离——选择流程判定（交集+随机）是纯函数、随机入口收敛到 Manager 单一方法（将来换网络 seed）、城邦状态是"对局级"数据放 Manager 非 UI。
- **复杂度分档**：一档(贸易+季风，复用现有系统) → 二档(欢愉，全新手牌系统) → 三档(邪疑+战争，改归属/胜负，规则歧义最多，须单独评审)。
- **已有伏笔（架构早预留）**：商店 ShouldShowCityState 扩展点、EquipmentTier.CityState、GameConfig.shieldElementDyeingEnabled（等城邦接管）。
- 护盾系统已全部完成（基座+收益拦截+UI+染色），见 2026-08-20 日志。

## 待实现新机制（约 16 个）
- 移动模板：FlyMove（BFS无视阻挡）/ StraightPass（直线无视阻挡）/ ParityMove（偶数距离）
- 范围 Provider：RingTangentProvider（环切线3格）
- 大招机制：AOE / 直线 / 治疗 / 光环 buff / 易伤 debuff / 位移冲刺 / 召唤傀儡
- 被动机制：蓄力 / 领域实体 / 路径伤害(onTileTraversed) / 协同攻击 / 全局监听 / 攻击锁定(嘲讽)
