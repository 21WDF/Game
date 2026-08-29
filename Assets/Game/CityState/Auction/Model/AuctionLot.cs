/// <summary>
/// 拍卖拍品运行时状态 —— 纯数据类（由 AuctionManager 创建与修改）。
/// 字段：当前最高出价 / 出价者 / 成交倒计时剩余回合数。
///
/// 出价规则：首次出价 ≥ 底价；后续出价 ≥ 当前价 + 最低加价幅度（MinNextBid = 一键出最低可行价）。
/// 倒计时口径（时间单位铁律）：出价时重置为配置回合数（默认 6），每次任一方回合结束 -1
/// （含出价所在回合的结束），归零成交 —— 严格按「回合」（CurrentTurn）计，绝不按「轮」（CurrentRound）计。
/// </summary>
public class AuctionLot
{
    public AuctionItemData Data { get; }
    public int CurrentBid { get; set; }             // 当前最高出价（HasBid=false 时无意义）
    public PlayerSide? Bidder { get; set; }         // 当前出价者（null = 无人出价）
    public int CountdownRemaining { get; set; }     // 成交倒计时剩余回合（HasBid 后有效）

    public bool HasBid => Bidder.HasValue;

    /// <summary>下一次出价的最低可行金额：无人出价 = 底价；有人出价 = 当前价 + 最低加价幅度</summary>
    public int MinNextBid => HasBid ? CurrentBid + Data.minIncrement : Data.startingPrice;

    public AuctionLot(AuctionItemData data)
    {
        Data = data;
    }
}
