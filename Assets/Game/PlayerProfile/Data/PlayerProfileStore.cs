using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家档案持久化（PlayerPrefs，多账号结构）。
/// 键名前缀统一 "PlayerProfile."：
///   PlayerProfile.accounts       → 全部账号档案的 JSON（AccountList 包装；JsonUtility 不支持 Dictionary）
///   PlayerProfile.currentAccount → 当前登录账号名
/// 损坏 / 缺失降级：任何解析失败 → 按空数据处理（PlayerProfileService 据此重建默认档案），绝不抛异常（红线 1）。
/// 联机预留（E-1）：档案读写唯一入口集中在此；将来换「服务器档案」时只改本类读写实现，调用方（服务/UI）不动。
/// </summary>
public static class PlayerProfileStore
{
    private const string AccountsKey = "PlayerProfile.accounts";
    private const string CurrentKey = "PlayerProfile.currentAccount";

    /// <summary>JSON 包装：「账号 → 档案」映射用列表承载（JsonUtility 不支持 Dictionary）</summary>
    [System.Serializable]
    public class AccountList
    {
        public List<PlayerProfileData> accounts = new();
    }

    public static void SaveAccounts(List<PlayerProfileData> accounts)
    {
        var wrapper = new AccountList { accounts = accounts ?? new List<PlayerProfileData>() };
        PlayerPrefs.SetString(AccountsKey, JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }

    /// <summary>读取全部档案；数据缺失 / 损坏 → 返回空列表（优雅降级，不抛异常）</summary>
    public static List<PlayerProfileData> LoadAccounts()
    {
        try
        {
            string json = PlayerPrefs.GetString(AccountsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return new List<PlayerProfileData>();
            var wrapper = JsonUtility.FromJson<AccountList>(json);
            return wrapper?.accounts ?? new List<PlayerProfileData>();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerProfileStore] 档案数据损坏，按空档案重建：{e.Message}");
            return new List<PlayerProfileData>();
        }
    }

    public static void SaveCurrentAccount(string account)
    {
        PlayerPrefs.SetString(CurrentKey, account ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static string LoadCurrentAccount()
    {
        try { return PlayerPrefs.GetString(CurrentKey, string.Empty); }
        catch { return string.Empty; }
    }
}
