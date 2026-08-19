using System;
using System.Collections.Generic;

/// <summary>
/// 战斗模型（Battle 模块的 Model）—— 纯数据 + 状态 + 事件，无 Unity 依赖。
/// 职责：持有战斗状态机数据（当前选中棋子、高亮坐标集合），提供状态变更方法，并在变更时触发事件。
///
/// Phase 4 MVC 改造：
///   - 选中状态、高亮集合从 BattleController 的私有字段迁移到本 Model。
///   - BattleController 只修改 Model，不再直接调用 HexTile.SetHighlight。
///   - BattleView 订阅 OnHighlightsChanged 事件，负责实际材质渲染。
///
/// 事件设计：
///   - OnHighlightsChanged(oldCoords, newHighlights)：oldCoords 用于让 View 清除旧高亮，
///     newHighlights 用于让 View 应用新高亮。避免 View 自行跟踪已应用状态。
/// </summary>
public class BattleModel
{
    // ---- 运行时状态 ----
    public PieceModel SelectedPiece { get; private set; }

    /// <summary>当前高亮显示模式（移动/攻击）。选中棋子后默认 Move，R 键切换。</summary>
    public HighlightMode CurrentHighlightMode { get; private set; } = HighlightMode.Move;

    /// <summary>棋子是否正在移动动画中（true 时屏蔽点击/悬停输入）</summary>
    public bool IsPieceAnimating { get; set; }

    private Dictionary<HexCoord, HighlightType> _highlights = new();
    /// <summary>当前高亮集合（只读视图）</summary>
    public IReadOnlyDictionary<HexCoord, HighlightType> Highlights => _highlights;

    // ---- 事件 ----
    /// <summary>选中棋子变化时触发（null 表示取消选中）</summary>
    public event Action<PieceModel> OnSelectionChanged;

    /// <summary>高亮变化时触发（旧坐标集合用于清除，新高亮字典用于应用）</summary>
    public event Action<IReadOnlyList<HexCoord>, IReadOnlyDictionary<HexCoord, HighlightType>> OnHighlightsChanged;

    /// <summary>高亮模式切换时触发（BattleController 据此全量重算行动范围高亮）</summary>
    public event Action<HighlightMode> OnHighlightModeChanged;

    // ==========================================
    //  选中状态
    // ==========================================
    public void SetSelection(PieceModel piece)
    {
        SelectedPiece = piece;
        OnSelectionChanged?.Invoke(piece);
    }

    public void ClearSelection()
    {
        SelectedPiece = null;
        ResetHighlightMode();   // 取消选中 → 回到默认移动模式
        ClearHighlights();
        OnSelectionChanged?.Invoke(null);
    }

    // ==========================================
    //  高亮状态
    // ==========================================
    /// <summary>设置新的高亮集合（替换旧集合，触发事件）</summary>
    public void SetHighlights(Dictionary<HexCoord, HighlightType> newHighlights)
    {
        // 保存旧坐标用于 View 清除
        var oldCoords = new List<HexCoord>(_highlights.Keys);
        _highlights = newHighlights ?? new Dictionary<HexCoord, HighlightType>();
        OnHighlightsChanged?.Invoke(oldCoords, _highlights);
    }

    /// <summary>清除所有高亮（触发事件）</summary>
    public void ClearHighlights()
    {
        if (_highlights.Count == 0) return;
        var oldCoords = new List<HexCoord>(_highlights.Keys);
        _highlights = new Dictionary<HexCoord, HighlightType>();
        OnHighlightsChanged?.Invoke(oldCoords, _highlights);
    }

    /// <summary>指定坐标是否在高亮集合中</summary>
    public bool IsHighlighted(HexCoord coord) => _highlights.ContainsKey(coord);

    // ==========================================
    //  高亮模式（R 键切换）
    // ==========================================
    /// <summary>切换高亮模式（Move↔Attack），并触发事件通知 BattleController 全量重算高亮</summary>
    public void ToggleHighlightMode()
    {
        CurrentHighlightMode = CurrentHighlightMode == HighlightMode.Move
            ? HighlightMode.Attack
            : HighlightMode.Move;
        OnHighlightModeChanged?.Invoke(CurrentHighlightMode);
    }

    /// <summary>重置为移动模式（静默，不触发事件）。
    /// 在取消选中 / 选中新棋子 / 按下其他操作键（U）时调用，确保新交互从移动模式开始。
    /// 调用方负责后续通过 SetHighlights/ShowActionHighlights 同步视图。</summary>
    public void ResetHighlightMode()
    {
        CurrentHighlightMode = HighlightMode.Move;
    }

    /// <summary>重置（用于重新开局）</summary>
    public void Reset()
    {
        SelectedPiece = null;
        ResetHighlightMode();
        ClearHighlights();
    }
}
