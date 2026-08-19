using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗视图（Battle 模块的 View）—— 订阅 <see cref="BattleModel"/> 事件，负责高亮的实际渲染。
/// 挂载在场景中的 BattleView GameObject 上。
///
/// Phase 4 MVC 改造：
///   - 高亮渲染从 BattleController 迁移到本 View。
///   - 通过订阅 Model.OnHighlightsChanged 实现事件驱动，Controller 不再直接操作 HexTile 材质。
///   - 旧坐标清除 + 新坐标应用，避免全量扫描所有格子。
/// </summary>
public class BattleView : MonoBehaviour
{
    private BattleModel _model;
    private GameConfig _config;
    private PieceModel _lastSelected;  // 跟踪上一个选中棋子，用于取消高亮

    // GameConfig 在 Awake 加载（颜色等渲染参数收拢于此，调参无需改代码）
    private void Awake()
    {
        _config = Resources.Load<GameConfig>("GameConfig");
        if (_config == null)
            Debug.LogError("[BattleView] GameConfig 加载失败！请确保 Assets/Game/Resources/GameConfig.asset 存在。", this);
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeNextFrame());
    }

    private System.Collections.IEnumerator SubscribeNextFrame()
    {
        yield return null;
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (BattleController.Instance == null) return;
        _model = BattleController.Instance.Model;
        if (_model == null) return;
        _model.OnHighlightsChanged += HandleHighlightsChanged;
        _model.OnSelectionChanged += HandleSelectionChanged;
    }

    private void OnDisable()
    {
        if (_model != null)
        {
            _model.OnHighlightsChanged -= HandleHighlightsChanged;
            _model.OnSelectionChanged -= HandleSelectionChanged;
        }
    }

    // ==========================================
    //  事件处理：棋子选中高亮
    // ==========================================
    private void HandleSelectionChanged(PieceModel newSelected)
    {
        // 取消上一个选中棋子的高亮
        if (_lastSelected != null && _lastSelected.View != null)
            _lastSelected.View.SetSelected(false);

        // 设置新选中棋子的高亮
        if (newSelected != null && newSelected.View != null)
            newSelected.View.SetSelected(true);

        _lastSelected = newSelected;
    }

    // ==========================================
    //  事件处理：清除旧高亮 + 应用新高亮
    // ==========================================
    private void HandleHighlightsChanged(IReadOnlyList<HexCoord> oldCoords, IReadOnlyDictionary<HexCoord, HighlightType> newHighlights)
    {
        if (_config == null) return;  // 配置未加载，跳过高亮渲染（Awake 已报错）
        var board = ChessBoardController.Instance;
        if (board == null) return;

        // 1. 清除旧高亮
        foreach (var coord in oldCoords)
        {
            // 新集合中仍存在的坐标稍后会重新应用，跳过避免闪烁
            if (newHighlights.ContainsKey(coord)) continue;
            var tile = board.GetTile(coord);
            if (tile != null) tile.overlay?.Hide();
        }

        // 2. 应用新高亮
        foreach (var kvp in newHighlights)
        {
            var tile = board.GetTile(kvp.Key);
            if (tile != null) ApplyHighlight(tile, kvp.Value);
        }
    }

    /// <summary>根据高亮类型应用叠加层颜色（优先级：AttackEnemy > Attack > Waypoint > Move > None）。
    /// 颜色取自 GameConfig，Alpha 烘焙在颜色的 .a 里，调用时取 .a 传给 TileOverlay。</summary>
    private void ApplyHighlight(HexTile tile, HighlightType type)
    {
        // 一个格子可同时具有多个标志（如 Move|Attack 重合格），按优先级取最高的显示
        if ((type & HighlightType.AttackEnemy) != 0)
            tile.overlay?.Show(_config.overlayAttackEnemyColor, _config.overlayAttackEnemyColor.a);  // 浅红
        else if ((type & HighlightType.Attack) != 0)
            tile.overlay?.Show(_config.overlayAttackColor, _config.overlayAttackColor.a);            // 红色
        else if ((type & HighlightType.Waypoint) != 0)
            tile.overlay?.Show(_config.overlayWaypointColor, _config.overlayWaypointColor.a);        // 途经点（绿）
        else if ((type & HighlightType.Move) != 0)
            tile.overlay?.Show(_config.overlayMoveColor, _config.overlayMoveColor.a);                // 蓝色
        else
            tile.overlay?.Hide();
    }
}
