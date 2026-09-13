using UnityEngine;

/// <summary>
/// 会话对局配置（运行时静态载体）—— 大厅选择的对局参数在场景切换间传递的唯一入口。
///
/// 设计约束（红线）：
///   1) 独立于 GameConfig（ScriptableObject 资产）：不新增/不改写 GameConfig 字段，
///      避免污染全局平衡参数、避免 Editor 把运行期修改持久化写回资产。
///   2) 默认值 = 现状（半径 4 / 每方 7 / 随机城邦）：大厅不改配置直接开始，
///      或开发者直接打开局内场景 Play（IsConfigured == false）时，行为与现状逐帧一致。
///   3) 静态字段跨场景存活（大厅 → 局内 → 大厅 → 局内），是「对局配置传入局内」的载体；
///      局内只读、不写；重复进入大厅时 UI_MainMenu 读取以回显上次选择。
///
/// 写入入口：UI_MainMenu「开始游戏」调用 <see cref="Apply"/>；局内只允许读取。
/// </summary>
public static class SessionConfig
{
    // ---- 默认值（与现状一一对应，不得改动）----
    public const int DefaultBoardRadius = 4;        // 对应 GameConfig.initialRadius 现值（=4）
    public const int DefaultPiecesPerSide = 7;      // 对应 UI_UnitSelection 硬编码值（=7）
    public const bool DefaultCityStateMode = true;  // true=随机城邦（现行流程），false=无城邦（跳过选择）

    // ---- 可选取值（大厅 UI 的选项集；局内读取方不必感知，只读当前值）----
    public static readonly int[] AllowedBoardRadii = { 4, 5, 6 };
    public static readonly int[] AllowedPiecesPerSide = { 3, 5, 7 };

    // ---- 当前值（未被大厅配置过时 = 默认值，即开发直进场景的行为基线）----
    public static int BoardRadius { get; private set; } = DefaultBoardRadius;
    public static int PiecesPerSide { get; private set; } = DefaultPiecesPerSide;
    public static bool CityStateMode { get; private set; } = DefaultCityStateMode;

    /// <summary>是否已被大厅显式配置（false = 开发直进局内场景，半径应回退 GameConfig 现值）</summary>
    public static bool IsConfigured { get; private set; }

    /// <summary>大厅「开始游戏」写入对局参数（唯一写入入口；局内只读）。越界值收敛到合法选项。</summary>
    public static void Apply(int boardRadius, int piecesPerSide, bool cityStateMode)
    {
        BoardRadius = ClampToOption(AllowedBoardRadii, boardRadius, DefaultBoardRadius);
        PiecesPerSide = ClampToOption(AllowedPiecesPerSide, piecesPerSide, DefaultPiecesPerSide);
        CityStateMode = cityStateMode;
        IsConfigured = true;
        Debug.Log($"[SessionConfig] 对局配置已写入：半径 {BoardRadius} / 每方 {PiecesPerSide} 个 / 城邦模式 {(CityStateMode ? "随机城邦" : "无城邦")}");
    }

    /// <summary>取合法选项集中最近的值（防开发期误传畸形参数导致对局异常）</summary>
    private static int ClampToOption(int[] options, int value, int fallback)
    {
        foreach (var opt in options)
            if (opt == value) return opt;
        int nearest = fallback, best = int.MaxValue;
        foreach (var opt in options)
        {
            int d = System.Math.Abs(opt - value);
            if (d < best) { best = d; nearest = opt; }
        }
        return nearest;
    }
}
