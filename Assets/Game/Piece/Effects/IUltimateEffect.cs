/// <summary>大招目标模式。
/// Enemy = 敌方棋子（默认；走 requiresTarget 旧语义，向后兼容既有资产）；
/// Self = 自身增益（等同旧 requiresTarget=false）；
/// Tile = 指定格子（可点空格；瞄准范围由 tileTargetRange / 攻击范围决定，效果走三参 Execute 拿坐标）。</summary>
public enum UltimateTargetMode { Enemy, Self, Tile }

/// <summary>Tile 模式高亮形状。
/// Circle = 以自身为圆心的圆形（默认，含被占格——效果自行决定合法性）；
/// Ray = 6 方向射线上的空格（直线冲刺/落点类；被占格不可选，点击被占格自然取消）。
/// 射线长度 = tileTargetRange（Ray 需 &gt;0，≤0 时无可选格无法释放）。</summary>
public enum UltimateTileShape { Circle, Ray }

/// <summary>部分能量消耗型大招 —— 释放条件仍是「满能」，但消耗量由效果按自身状态决定（如菲林斯：
/// 强化态只消耗 empoweredEnergyCost 并保留剩余，普通态全清）。
/// PieceManager.UseUltimateCore 在能量校验阶段创建效果后询问 GetEnergyCost；
/// 未实现此接口的大招走 TryConsume() 满能全清（原行为，零影响）。</summary>
public interface IPartialEnergyUltimate
{
    /// <summary>本次释放的能量消耗量（在满能前提下扣除；≥ 当前能量则清零）。
    /// 与 Execute 在同一调用栈内被询问，两者读到的形态状态一致。</summary>
    int GetEnergyCost(PieceModel caster);
}

/// <summary>
/// 大招效果接口 —— 大招的行为契约。
/// 由 PieceManager 工厂根据 PieceData.ultimateConfig.effectClassName 实例化，
/// 在 <see cref="PieceManager.UseUltimate"/> 中调用 Execute。
///
/// 目标约定：
///   - caster 永远为释放者。
///   - target 对自身增益型大招（targetMode=Self / requiresTarget=false）会被忽略，
///     调用方传入 caster 即可。
///   - 指定格模式（targetMode=Tile）走三参 <see cref="Execute(PieceModel, PieceModel, HexCoord)"/>：
///     targetCoord 为玩家点选的目标格坐标（可为空格），target 为该格上的棋子（空格时为 null）。
/// </summary>
public interface IUltimateEffect
{
    /// <summary>执行大招效果（敌人 / 自身模式入口）</summary>
    void Execute(PieceModel caster, PieceModel target);

    /// <summary>执行大招效果（指定格模式入口）。默认委托两参版本——现有大招零改动；
    /// 格子型大招（召唤物 / 领域 / 指定位 AOE）覆写此方法消费 targetCoord。</summary>
    void Execute(PieceModel caster, PieceModel target, HexCoord targetCoord) => Execute(caster, target);
}
