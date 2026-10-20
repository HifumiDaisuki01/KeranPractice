namespace KeranPractice;

/// <summary>权限判断：管理员全权限；普通玩家按 PublicCommands 白名单。</summary>
public sealed class PermissionService
{
    private static readonly HashSet<string> HighImpact = new(StringComparer.OrdinalIgnoreCase)
    { "god", "noclip", "respawn", "restart", "bot", "tbot", "ctbot", "placebot", "removebot", "clearbots", "freezebot", "swapbot", "clearall" };
    private readonly HashSet<ulong> _admins = new();
    private readonly HashSet<string> _public = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _adminOnly = new(StringComparer.OrdinalIgnoreCase);
    private readonly PracticeConfig _config;

    public PermissionService(PracticeConfig config)
    {
        _config = config;
        Reload(config);
    }

    public void Reload(PracticeConfig config)
    {
        _admins.Clear();
        _public.Clear();
        _adminOnly.Clear();

        foreach (var s in config.Admins)
            if (ulong.TryParse(s.Trim(), out var id) && id > 0)
                _admins.Add(id);

        foreach (var c in config.PublicCommands)
            if (!string.IsNullOrWhiteSpace(c)) _public.Add(Normalize(c));

        foreach (var c in config.AdminOnlyCommands)
            if (!string.IsNullOrWhiteSpace(c)) _adminOnly.Add(Normalize(c));
    }

    private static string Normalize(string cmd)
    {
        var c = cmd.Trim().TrimStart('!', '/');
        if (c.StartsWith("css_", StringComparison.OrdinalIgnoreCase)) c = c[4..];
        return c;
    }

    public bool IsAdmin(ulong steamId) => steamId != 0 && _admins.Contains(steamId);

    /// <summary>该指令是否被配置为公开可用。</summary>
    public bool IsPublicCommand(string command) => _public.Contains(Normalize(command));

    /// <summary>
    /// 综合判断能否使用某指令。
    /// </summary>
    /// <param name="practiceActive">该玩家当前是否在训练会话中。</param>
    public bool CanUse(ulong steamId, string command, bool practiceActive, out string reason)
    {
        var cmd = Normalize(command);

        if (IsAdmin(steamId))
        {
            reason = "";
            return true;
        }

        if (HighImpact.Contains(cmd) && !_config.AllowPublicPowerCommands)
        {
            reason = "高风险指令仅允许管理员使用（可在配置中显式授权）。";
            return false;
        }

        if (_adminOnly.Contains(cmd))
        {
            reason = "该指令仅限管理员使用。";
            return false;
        }

        if (!_public.Contains(cmd))
        {
            reason = "该指令未对普通玩家开放。";
            return false;
        }

        // 训练会话限制：practice / phelp 自身豁免
        if (_config.RequirePracticeMode &&
            !cmd.Equals("practice", StringComparison.OrdinalIgnoreCase) &&
            !cmd.Equals("phelp", StringComparison.OrdinalIgnoreCase) &&
            !practiceActive)
        {
            reason = "请先输入 !practice 进入训练模式。";
            return false;
        }

        reason = "";
        return true;
    }

    /// <summary>冷却检查。返回 false 表示还在冷却中。</summary>
    public bool CheckCooldown(PlayerState state, out int remainMs)
    {
        remainMs = 0;
        var cooldown = _config.CommandCooldownMs;
        if (cooldown <= 0) return true;

        var elapsed = (DateTime.UtcNow - state.LastCommandAt).TotalMilliseconds;
        if (elapsed < cooldown)
        {
            remainMs = (int)(cooldown - elapsed);
            return false;
        }
        state.LastCommandAt = DateTime.UtcNow;
        return true;
    }

    public bool RespectKeranStrat => _config.RespectKeranStrat;
    public bool EnableBotManagement => _config.EnableBotManagement;
    public bool AllowRoundRestart => _config.AllowRoundRestart;
    public float AutoClearDelaySeconds => _config.AutoClearDelaySeconds;
    public bool AutoClearDefault => _config.AutoClearDefault;
    public bool GodModeDefault => _config.GodModeDefault;
    public int RethrowHoldMs => _config.RethrowHoldMs;
    public bool AutoThrowOnRethrow => _config.AutoThrowOnRethrow;
    public bool ReturnAfterRethrow => _config.ReturnAfterRethrow;
    public int MaxPositionsPerMap => _config.MaxPositionsPerMap;
    public int MaxNadesPerMap => _config.MaxNadesPerMap;
}
