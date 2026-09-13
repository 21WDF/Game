using System.Collections.Generic;

/// <summary>
/// 玩家档案数据（局外成长载体）—— 纯数据类，可被 JsonUtility 序列化存入本地持久化。
/// 字段：账号 / 密码哈希+盐 / 金币 / 已拥有棋子 id 列表 / 胜败场次（展示用）。
/// 密码绝不存明文：passwordHash = SHA256(salt + password) 的 Base64，passwordSalt 为每账号随机盐。
/// 序列化注意：JsonUtility 不支持 Dictionary，多账号组织由 PlayerProfileStore 的 AccountList 包装承载。
/// </summary>
[System.Serializable]
public class PlayerProfileData
{
    public string account = "";
    public string passwordHash = "";
    public string passwordSalt = "";
    public int gold;
    public List<int> ownedPieceIds = new();
    public int wins;
    public int losses;
}
