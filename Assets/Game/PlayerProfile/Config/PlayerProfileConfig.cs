using UnityEngine;

/// <summary>
/// 玩家档案 / 金币经济配置（ScriptableObject，可选资产）。
/// 通过 CreateAssetMenu 创建：Create > Chess > Player Profile Config，放入 Assets/Game/Resources/PlayerProfileConfig.asset
/// 后由 Resources.Load 读取；资产不存在时回退默认实例（数值与设计结论一致）。
///
/// 数值均为 [PLACEHOLDER]：实机验证「解锁节奏」后只需改资产，不动代码。
/// 默认账号（test / test123）为占位测试账号，首次启动自动创建并登录（红线 7：无需任何操作即可开始游戏）。
/// </summary>
[CreateAssetMenu(fileName = "PlayerProfileConfig", menuName = "Chess/Player Profile Config", order = 2)]
public class PlayerProfileConfig : ScriptableObject
{
    [Header("棋池解析（可选）")]
    [Tooltip("棋子注册表（解析已拥有棋子 / 商店列表；为空时回退 PlayerProfileService.Registry → PieceManager.Instance.registry）")]
    public PieceRegistry registry;

    [Header("初始棋子（[PLACEHOLDER] 4）")]
    [Tooltip("初始拥有棋子数（默认取注册表非召唤棋子按 id 升序前 N 个）")]
    public int initialPieceCount = 4;
    [Tooltip("初始棋子 id 白名单；留空 = 按 id 升序取前 initialPieceCount 个非召唤棋子")]
    public int[] initialPieceIds;

    [Header("金币产出（[PLACEHOLDER] 100 / 200）")]
    [Tooltip("每局基础金币（胜负都发）")]
    public int goldPerGame = 100;
    [Tooltip("胜利额外金币（胜利合计 = goldPerGame + goldPerWinBonus = 300）")]
    public int goldPerWinBonus = 200;

    [Header("金币消耗（[PLACEHOLDER] 500）")]
    [Tooltip("商店购买单个棋子价格")]
    public int piecePrice = 500;

    [Header("默认测试账号（[PLACEHOLDER]）")]
    [Tooltip("首次启动自动创建的默认账号；密码仅本地档案归属用（SHA256 + 盐哈希存储）")]
    public string defaultAccount = "test";
    [Tooltip("默认账号密码（占位测试密码）")]
    public string defaultPassword = "test123";
}
