/// <summary>
/// 认证接口 —— 档案归属校验的抽象层。
/// 本地实现 <see cref="LocalAuthProvider"/>（纯离线校验：SHA256 + 盐哈希比对）；
/// 联机预留（E-2）：接入服务器时新增远端实现（如 RemoteAuthProvider 走网络认证），替换注入即可，UI / 服务层不改。
/// </summary>
public interface IAuthProvider
{
    /// <summary>当前登录账号（未登录为空字符串）</summary>
    string CurrentAccount { get; }

    /// <summary>登录：账号 + 密码校验通过 → 置为当前登录账号并返回 true；失败返回 false（不修改登录态）</summary>
    bool Login(string account, string password);

    /// <summary>注册：账号不存在 → 创建档案并登录返回 true；已存在返回 false</summary>
    bool Register(string account, string password);

    /// <summary>退出登录（清空当前登录态）</summary>
    void Logout();
}
