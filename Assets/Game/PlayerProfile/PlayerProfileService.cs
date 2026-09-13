using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家档案服务 —— 局外成长（登录 / 金币 / 棋池 / 商店）的统一入口（非 UI 组件）。
/// 首次使用惰性初始化：加载上次登录账号；无任何档案 → 自动创建并登录默认测试账号（红线 7：首启即可直接开始游戏）。
/// 金币 / 拥有棋子变动即时写盘（异常退出不丢，红线 B-3）。
///
/// 联机预留（E 项）：
///   E-1 档案读写：集中于 PlayerProfileStore（换「服务器档案」只改该类）；
///   E-2 认证：走 IAuthProvider 接口（本地实现可替换为远端实现，UI 不改）；
///   E-3 结算：唯一发币点 = <see cref="SettleGameOutcome"/>，按「本机是否获胜」参数化，
///       热座调用方传 winner==P1，联机传 winner==本机阵营 —— 本方法与 UI 零改动；
///   E-4 棋池：GetOwnedPieces / IsOptionUnlocked 为统一查询入口，联机按各自档案分别判断。
/// </summary>
public static class PlayerProfileService
{
    // ================= 事件 =================
    /// <summary>档案状态变化（登录 / 注册 / 退出 / 金币 / 拥有棋子变动）→ 大厅 UI 统一刷新</summary>
    public static event System.Action OnProfileChanged;

    // ================= 依赖注入 =================
    private static PieceRegistry _registry;
    /// <summary>棋池解析源（大厅 UI_MainMenu 拖入后 BindRegistry；局内回退 PieceManager.Instance.registry）</summary>
    public static PieceRegistry Registry => _registry;

    private static PlayerProfileConfig _config;
    public static PlayerProfileConfig Config
    {
        get
        {
            if (_config == null)
                _config = Resources.Load<PlayerProfileConfig>("PlayerProfileConfig")
                          ?? ScriptableObject.CreateInstance<PlayerProfileConfig>();
            return _config;
        }
    }

    /// <summary>棋池解析优先级：BindRegistry 绑定 → 配置资产 → 局内 PieceManager</summary>
    public static PieceRegistry ResolveRegistry()
    {
        if (_registry != null) return _registry;
        if (Config != null && Config.registry != null) return Config.registry;
        if (PieceManager.Instance != null && PieceManager.Instance.registry != null)
            return PieceManager.Instance.registry;
        return null;
    }

    /// <summary>绑定棋池解析源（大厅拖入注册表后调用；只绑定一次）</summary>
    public static void BindRegistry(PieceRegistry registry)
    {
        if (registry == null || _registry != null) return;
        _registry = registry;
    }

    // ================= 运行时状态 =================
    private static IAuthProvider _auth;
    public static IAuthProvider Auth => _auth ??= new LocalAuthProvider();

    private static PlayerProfileData _current;
    public static PlayerProfileData Current => _current;
    public static bool IsLoggedIn => _current != null;
    public static string CurrentAccount => _current?.account ?? string.Empty;

    /// <summary>本局是否已结算（幂等标志：同局只发一次币；BeginNewGame 在新局开始时重置）</summary>
    private static bool _currentGameSettled;

    private static List<PlayerProfileData> _accounts = new();
    private static bool _initialized;

    // ================= 初始化 =================
    /// <summary>首次使用惰性初始化（幂等）：加载档案 → 自动登录上次账号；无档案则创建默认测试账号</summary>
    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _accounts = PlayerProfileStore.LoadAccounts();
        // 红线 1 降级：过滤损坏项（空账号记录），仍无有效档案 → 重建默认档案，绝不崩溃
        _accounts.RemoveAll(p => p == null || string.IsNullOrEmpty(p.account));
        if (_accounts.Count == 0)
        {
            var profile = CreateProfile(Config.defaultAccount, Config.defaultPassword);
            _current = profile;
            PlayerProfileStore.SaveCurrentAccount(profile.account);
            Debug.Log($"[PlayerProfile] 首次启动：已创建并登录默认账号 {profile.account}");
        }
        else
        {
            string last = PlayerProfileStore.LoadCurrentAccount();
            _current = Find(_accounts, last) ?? _accounts[0];
            Debug.Log($"[PlayerProfile] 恢复登录账号：{_current.account}");
        }
    }

    // ================= 认证（转发 IAuthProvider；联机替换 Auth 实现后此处零改动） =================
    public static bool Login(string account, string password)
    {
        EnsureInitialized();
        if (!Auth.Login(account, password)) return false;
        _current = GetProfile(account);
        RaiseChanged();
        return true;
    }

    public static bool Register(string account, string password)
    {
        EnsureInitialized();
        if (!Auth.Register(account, password)) return false;
        _current = GetProfile(account);
        RaiseChanged();
        return true;
    }

    public static void Logout()
    {
        EnsureInitialized();
        Auth.Logout();
        _current = null;
        RaiseChanged();
    }

    // ================= 档案访问 =================
    /// <summary>按账号查找档案；不存在返回 null</summary>
    public static PlayerProfileData GetProfile(string account)
    {
        EnsureInitialized();
        return Find(_accounts, account);
    }

    /// <summary>创建新档案（哈希 + 初始棋子），加入缓存并写盘。供 LocalAuthProvider.Register / 首启默认账号调用。</summary>
    public static PlayerProfileData CreateProfile(string account, string password)
    {
        var profile = new PlayerProfileData
        {
            account = account,
            passwordSalt = LocalAuthProvider.NewSalt(),
            ownedPieceIds = ResolveInitialPieceIds(),
        };
        profile.passwordHash = LocalAuthProvider.HashPassword(password, profile.passwordSalt);
        _accounts.Add(profile);
        SaveAccounts();
        Debug.Log($"[PlayerProfile] 创建档案：{account}（初始拥有 {profile.ownedPieceIds.Count} 个棋子）");
        return profile;
    }

    // ================= 棋池查询（E-4 统一入口；联机按各自档案分别判断，此处实现不变） =================
    /// <summary>当前账号拥有的棋子（非召唤，按注册表解析为 PieceData；注册表缺失返回空并告警）</summary>
    public static List<PieceData> GetOwnedPieces()
    {
        EnsureInitialized();
        var result = new List<PieceData>();
        if (_current == null) return result;
        var registry = ResolveRegistry();
        if (registry == null)
        {
            Debug.LogWarning("[PlayerProfile] 未配置棋子注册表（大厅拖入或 PlayerProfileConfig.registry），棋池为空");
            return result;
        }
        foreach (var id in _current.ownedPieceIds)
        {
            var data = registry.GetByID(id);
            if (data != null && !data.isSummon) result.Add(data);
        }
        return result;
    }

    public static bool IsOwned(int pieceId)
    {
        EnsureInitialized();
        return _current != null && _current.ownedPieceIds != null && _current.ownedPieceIds.Contains(pieceId);
    }

    /// <summary>当前账号拥有棋子数</summary>
    public static int OwnedCount
    {
        get
        {
            EnsureInitialized();
            return _current?.ownedPieceIds?.Count ?? 0;
        }
    }

    /// <summary>解锁判定（设计结论）：拥有数 ≥ 每方棋子数 即解锁</summary>
    public static bool IsOptionUnlocked(int piecesPerSide) => OwnedCount >= piecesPerSide;

    /// <summary>把请求值收敛到「已解锁」的合法选项（C-3 默认值钳制）：
    /// 已解锁 → 原值；否则取 ≤ requested 的最大已解锁项；再否则最小已解锁项；全未解锁返回选项集最小值。</summary>
    public static int GetValidPieceCount(int requested)
    {
        int[] options = SessionConfig.AllowedPiecesPerSide;
        if (IsOptionUnlocked(requested)) return requested;
        int best = 0;
        foreach (var opt in options)
            if (opt <= requested && opt > best && IsOptionUnlocked(opt)) best = opt;
        if (best > 0) return best;
        foreach (var opt in options)
            if (IsOptionUnlocked(opt)) return opt;
        return options[0];
    }

    // ================= 商店购买（金币 sink） =================
    /// <summary>购买棋子：金币足够 → 扣款 + 解锁 + 即时写盘，返回 true；不足 / 已拥有 → false 并给出原因（不扣款）</summary>
    public static bool TryBuyPiece(int pieceId, out string failReason)
    {
        EnsureInitialized();
        failReason = string.Empty;
        if (_current == null) { failReason = "未登录"; return false; }
        var data = ResolveRegistry()?.GetByID(pieceId);
        if (data == null) { failReason = "棋子不存在"; return false; }
        if (_current.ownedPieceIds.Contains(pieceId)) { failReason = "已拥有该棋子"; return false; }

        int price = Config.piecePrice;
        if (_current.gold < price) { failReason = $"金币不足（需 {price}，当前 {_current.gold}）"; return false; }

        _current.gold -= price;
        _current.ownedPieceIds.Add(pieceId);
        SaveAccounts();
        RaiseChanged();
        Debug.Log($"[PlayerProfile] 购买棋子 {data.displayName}（-{price}），余额 {_current.gold}");
        return true;
    }

    // ================= 结算（唯一发币点 E-3；幂等；参数化"本机是否获胜"） =================
    /// <summary>新一局开始（GameFlowController.Start 调用）：重置本局结算标志。重开一局 = 重载场景 → Start 再跑 → 标志自然重置，新局可正常结算。</summary>
    public static void BeginNewGame()
    {
        EnsureInitialized();
        _currentGameSettled = false;
        Debug.Log("[PlayerProfile] 新局开始：结算幂等标志已重置");
    }

    /// <summary>
    /// 本局结算（唯一发币点）：同局只结算一次（_currentGameSettled 保护；UI_GameOver 重复触发 / 重开均不重复发币）。
    /// 参数化 localPlayerWon —— 调用方决定「本机是否获胜」：
    ///   热座：winner == PlayerSide.P1（本机视角固定 P1）；
    ///   联机：winner == 本机阵营。
    /// 两种模式下本方法与 UI 一行都不用改。
    /// 返回本局获得金币；已结算 / 未登录返回 -1（不发币）。
    /// </summary>
    public static int SettleGameOutcome(bool localPlayerWon)
    {
        EnsureInitialized();
        if (_currentGameSettled)
        {
            Debug.LogWarning("[PlayerProfile] 本局已结算，跳过重复发币（幂等保护）");
            return -1;
        }
        if (_current == null) return -1;
        _currentGameSettled = true;

        int gold = Config.goldPerGame + (localPlayerWon ? Config.goldPerWinBonus : 0);
        _current.gold += gold;
        if (localPlayerWon) _current.wins++; else _current.losses++;
        SaveAccounts();
        RaiseChanged();
        Debug.Log($"[PlayerProfile] 本局结算：{(localPlayerWon ? "胜" : "负")} +{gold} 金币，余额 {_current.gold}");
        return gold;
    }

    // ================= 初始棋子解析 =================
    /// <summary>初始拥有棋子 id：配置白名单优先；否则注册表非召唤棋子按 id 升序取前 initialPieceCount 个</summary>
    private static List<int> ResolveInitialPieceIds()
    {
        var cfg = Config;
        if (cfg.initialPieceIds != null && cfg.initialPieceIds.Length > 0)
            return new List<int>(cfg.initialPieceIds);

        var registry = ResolveRegistry();
        if (registry == null || registry.pieces == null)
        {
            Debug.LogWarning("[PlayerProfile] 无法解析初始棋子：未配置注册表（UI_MainMenu 拖入或 PlayerProfileConfig.registry），初始拥有棋子为空");
            return new List<int>();
        }

        var sorted = new List<PieceData>();
        foreach (var p in registry.pieces)
            if (p != null && !p.isSummon) sorted.Add(p);
        sorted.Sort((a, b) => a.id.CompareTo(b.id));

        var ids = new List<int>();
        for (int i = 0; i < sorted.Count && i < cfg.initialPieceCount; i++)
            ids.Add(sorted[i].id);
        return ids;
    }

    // ================= 辅助 =================
    private static PlayerProfileData Find(List<PlayerProfileData> list, string account)
    {
        if (list == null || string.IsNullOrEmpty(account)) return null;
        foreach (var p in list)
            if (p != null && p.account == account) return p;
        return null;
    }

    private static void SaveAccounts() => PlayerProfileStore.SaveAccounts(_accounts);

    private static void RaiseChanged() => OnProfileChanged?.Invoke();
}
