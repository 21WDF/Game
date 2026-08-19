/// <summary>
/// 元素反应修饰器接口 —— 装备被动/道具可实现此接口，在反应结算时修改反应参数。
///
/// 调用链（RuntimeReactionTable.Resolve）：
///   数据库基础值 → 全局修饰器链 → 棋子修饰器链 → 最终值
///
/// 实现方应返回新副本（值类型 struct 直接复制即新副本），不修改传入的 original。
/// 多个修饰器按顺序执行：前一个的输出是后一个的输入。
/// </summary>
public interface IReactionModifier
{
    /// <summary>修改反应配置，返回新值（不修改 original）</summary>
    ElementReactionTable.ReactionConfig Modify(ElementReactionTable.ReactionConfig original);
}
