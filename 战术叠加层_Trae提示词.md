所有依赖已确认。关键发现：

- BattleModel.SelectedPiece 公开可读 → InputHandler 可获取选中棋子
- ChessBoardController.FindPath(from, to) → List<HexCoord> 可直接调用
- ChessBoardView.ClearAllHighlights() 调用了 tile.SetHighlight() → 必须同步修改
- HexTile 有 7 个 Material 字段（不是 6 个，需求文中列了 7 个名字）  
  无矛盾点，但有一个设计决策需要说明：

关于悬停堆叠 ：需求中 Show/Hide 用于战术高亮，但悬停不能覆盖战术色。我将 TileOverlay 设计为 双层 ： Show/Hide （战术层，BattleView 调用）+ ShowHover/HideHover （悬停层，InputHandler 调用）。两层同时激活时，显示悬停色 + 战术Alpha + 0.1 （符合需求"简单做法"）。

关于 Prefab 和材质 ：TileOverlay 和 PathPreview 都支持 运行时自动创建 （Quad + Unlit/Transparent 材质），因此 Editor 手动操作是可选的。用户也可在 Inspector 中拖入预制体以获得更精确的控制。

## 实现计划（7 步）

步骤 文件 改动 1 TileOverlay.cs （新建） 双层叠加：Show/Hide（战术）+ ShowHover/HideHover（悬停）；Awake 自动创建 Quad + 材质 2 PathPreview.cs （新建） LineRenderer 封装：ShowPath(List<HexCoord>) / HidePath()；Awake 自动配置参数 3 HexTile.cs 删 7 个 Material 字段 + ApplyMaterial/SetHighlight/SetHover；加 overlay 引用（Awake 自动创建） 4 BattleView.cs HandleHighlightsChanged：SetHighlight → overlay.Show/Hide + 颜色映射表 5 ChessBoardView.cs ClearAllHighlights：SetHighlight(None) → overlay.Hide() 6 InputHandler.cs SetHover → ShowHover/HideHover；UpdateHover 加路径预览逻辑 7 编译验证 MSBuild 0 错误 0 警告

颜色映射：

- Move → 蓝(0.2, 0.4, 1.0) α=0.35
- Attack → 红(1.0, 0.2, 0.2) α=0.25
- AttackEnemy → 浅红(1.0, 0.4, 0.4) α=0.35
- Hover → 浅紫(0.7, 0.5, 1.0) α=0.25（无战术时）/ α=战术+0.1（有战术时堆叠）  
  确认后开始执行。
