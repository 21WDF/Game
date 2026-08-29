/// <summary>
/// 城邦专属机制接口（Pillar 3 城邦系统）—— 一期只建骨架，不实现任何具体城邦机制
/// （拍卖/卡牌/四季/卧底/将帅均为二期）。具体机制由 CityStateManager 持有并通知。
///
/// 硬约束：钩子为「纯通知」语义，不得修改回合主流程的任何数值或返回值（对标 IPassiveEffect 事件钩子约定）。
/// </summary>
public interface ICityStateMechanism
{
    /// <summary>钩子①：本局城邦确定时触发（双方选择求交集随机完成后）</summary>
    void OnGameStart(CityStateKind cityState);

    /// <summary>钩子②：每个回合开始时触发（任一方回合开始）</summary>
    void OnTurnStart(PlayerSide activePlayer);

    /// <summary>钩子③：每个回合结束时触发（endingPlayer = 刚结束行动的一方）</summary>
    void OnTurnEnd(PlayerSide endingPlayer);
}

/// <summary>无城邦机制（一期默认实现）：全部钩子为空 —— 城邦系统存在但无任何机制，游戏行为与之前完全一致。</summary>
public class NullCityStateMechanism : ICityStateMechanism
{
    public void OnGameStart(CityStateKind cityState) { }
    public void OnTurnStart(PlayerSide activePlayer) { }
    public void OnTurnEnd(PlayerSide endingPlayer) { }
}
