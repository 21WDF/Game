using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// 本地认证实现（纯离线校验）—— 密码 SHA256 + 每账号随机盐哈希存储，绝不明文（红线 1）。
/// 校验流程：
///   注册：生成随机盐 salt（16 字节 Base64）→ 存 passwordSalt；passwordHash = SHA256(salt + password) 的 Base64。
///   登录：用档案内 salt 重算哈希，与 passwordHash 比对（恒定时长差异此处从简，本地场景）。
/// 联机预留（E-2）：此实现可被远端实现替换（IAuthProvider），替换后 UI / 服务层零改动。
/// </summary>
public class LocalAuthProvider : IAuthProvider
{
    public string CurrentAccount { get; private set; } = string.Empty;

    public bool Login(string account, string password)
    {
        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password)) return false;
        var profile = PlayerProfileService.GetProfile(account);
        if (profile == null) return false;                       // 账号不存在
        if (string.IsNullOrEmpty(profile.passwordSalt)) return false;
        if (HashPassword(password, profile.passwordSalt) != profile.passwordHash) return false; // 密码错误
        CurrentAccount = account;
        PlayerProfileStore.SaveCurrentAccount(account);
        return true;
    }

    public bool Register(string account, string password)
    {
        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password)) return false;
        if (PlayerProfileService.GetProfile(account) != null) return false; // 账号已存在
        PlayerProfileService.CreateProfile(account, password);
        CurrentAccount = account;
        PlayerProfileStore.SaveCurrentAccount(account);
        return true;
    }

    public void Logout()
    {
        CurrentAccount = string.Empty;
        PlayerProfileStore.SaveCurrentAccount(string.Empty);
    }

    // ---- 哈希（静态，供服务层创建档案时复用）----

    /// <summary>SHA256(salt + password)，返回 Base64</summary>
    public static string HashPassword(string password, string saltBase64)
    {
        byte[] salt = Convert.FromBase64String(saltBase64);
        byte[] pw = Encoding.UTF8.GetBytes(password);
        byte[] combined = new byte[salt.Length + pw.Length];
        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
        Buffer.BlockCopy(pw, 0, combined, salt.Length, pw.Length);
        using (var sha = SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(combined));
    }

    /// <summary>生成随机盐（16 字节，Base64）</summary>
    public static string NewSalt()
    {
        byte[] salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(salt);
        return Convert.ToBase64String(salt);
    }
}
