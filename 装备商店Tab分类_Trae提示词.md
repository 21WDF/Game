# 装备商店 Tab 分类 Trae 提示词

```
在 Chaotic Chess 中实现装备商店的 Tab 分类（按等级/来源分 5 个 Tab），
同时解决装备列表溢出问题。

## 需求

商店顶部 5 个 Tab：初级装备 / 中级装备 / 高级装备 / 城邦装备 / 道具。

## 改动 1：EquipmentData 加 tier 字段

在 Assets/Game/Core/GameEnums.cs 加枚举（和其他枚举放一起）：

public enum EquipmentTier { Basic, Intermediate, Advanced, CityState }

在 Assets/Game/Equipment/Model/EquipmentData.cs 加字段：

public EquipmentTier tier = EquipmentTier.Basic;

## 改动 2：17 件装备 asset 的 tier 赋值

在 Resources/Equipment/ 下，给以下装备的 .asset 加 tier 字段（旧 3 件默认 Basic 可不改）：

Intermediate（中级，12 件）：
  生命铠甲、死亡之刃、英灵幡、石像鬼板甲、荆棘刺甲、饮血剑、
  军团圣盾、枯萎宝珠、死亡之蔑、中娅悖论、探索者护臂、诸葛连弩

CityState（城邦，5 件元素装备）：
  光界之力、潮涌之力、灼火之力、陨雷之力、凝冰之力

Basic（初级）：新手剑、橡木盾、迅捷之靴（默认值即可，无需改）
Advanced（高级）：暂无装备

## 改动 3：UI_ShopPanel 加 Tab 切换

- 新增字段：当前选中的 Tab（enum ShopTab { Basic, Intermediate, Advanced, CityState, GridItem }，默认 Basic）
- 顶部 5 个 Tab 按钮，点击切换当前 Tab 并高亮选中项
- RebuildShop 按当前 Tab 过滤：
  - 装备 Tab（Basic/Intermediate/Advanced/CityState）：只 Instantiate 对应 tier 的装备
  - 道具 Tab（GridItem）：只 Instantiate 棋盘道具（现有逻辑）
- Tab 按钮建议复用列表项的动态创建模式（一个 TabButton prefab，代码 Instantiate 5 个），
  或者 Editor 手动放 5 个 Button 绑定 SetTab 方法——两种都行，保持和现有 UI 风格一致即可

## 城邦动态展示（预留，本次不实现）

城邦 Tab 现在显示所有 tier = CityState 的装备。
未来城邦系统上线后，会加"根据本局城邦效果过滤，只显示对应城邦的装备，其余隐藏禁止购买"。
本次只需留一个清晰扩展点：例如一个私有方法
bool ShouldShowCityState(EquipmentData data) { return true; }  // 未来改为按城邦效果判断
城邦 Tab 的过滤调用它即可，不要写死动态展示逻辑。

## 约束

- 不改 EquipmentManager 的商店/背包/装卸逻辑
- 不改 GridItemManager
- 城邦动态展示逻辑本次不实现，只留 ShouldShowCityState 扩展点
- 高级 Tab 本次显示空列表（不是隐藏 Tab，是 Tab 存在但列表为空）

## Editor 操作（用户做）

1. ShopPanel 顶部加 5 个 Tab 按钮（Horizontal Layout Group 横向排列）
2. 每个 Tab 按钮绑定切换方法（或按 Trae 的方案用 prefab 动态创建）
3. Content 容器加 ScrollView（解决列表溢出）：Content 外包 ScrollRect + Viewport + Mask

## 验收

- [ ] 5 个 Tab：初级/中级/高级/城邦/道具
- [ ] 初级 Tab → 3 件旧装备
- [ ] 中级 Tab → 12 件装备
- [ ] 高级 Tab → 空列表
- [ ] 城邦 Tab → 5 件元素装备
- [ ] 道具 Tab → 3 件棋盘道具
- [ ] 列表可滚动，不再溢出面板
```
