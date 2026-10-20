using System.Text.Json.Serialization;

namespace KeranPractice;

/// <summary>
/// 插件配置。写入 configs/plugins/KeranPractice/KeranPractice.json
/// </summary>
public sealed class PracticeConfig
{
    [JsonPropertyName("ConfigVersion")]
    public int ConfigVersion { get; set; } = 1;

    /// <summary>管理员 SteamID64，拥有全部权限。</summary>
    [JsonPropertyName("Admins")]
    public List<string> Admins { get; set; } = new();

    /// <summary>普通玩家可用的指令（不含 css_ 前缀）。</summary>
    [JsonPropertyName("PublicCommands")]
    public List<string> PublicCommands { get; set; } = new()
    {
        "noflash", "savepos", "tp", "poslist", "delpos", "last",
        "clear", "rethrow", "practice", "phelp",
        "spawn", "tspawn", "ctspawn", "autoclear",
        "savenade", "nades", "loadnade"
    };

    /// <summary>仅管理员/白名单可用的指令。</summary>
    [JsonPropertyName("AdminOnlyCommands")]
    public List<string> AdminOnlyCommands { get; set; } = new()
    {
        "restart", "clearall", "give", "admin"
    };

    /// <summary>危险功能即便写进旧 PublicCommands 也需单独显式授权，迁移时安全默认拒绝。</summary>
    [JsonPropertyName("AllowPublicPowerCommands")]
    public bool AllowPublicPowerCommands { get; set; } = false;

    /// <summary>只有独立测试环境才显式开启 BOT 管理；KeranStrat 共存默认关闭。</summary>
    [JsonPropertyName("EnableBotManagement")]
    public bool EnableBotManagement { get; set; } = false;

    /// <summary>是否要求先在训练会话中（!practice）才能使用功能类指令。</summary>
    [JsonPropertyName("RequirePracticeMode")]
    public bool RequirePracticeMode { get; set; } = true;

    /// <summary>普通玩家进入训练会话是否也受 RequirePracticeMode 限制。</summary>
    [JsonPropertyName("AutoStartPracticeForWhitelist")]
    public bool AutoStartPracticeForWhitelist { get; set; } = true;

    /// <summary>同一玩家两条指令的最小间隔（毫秒），防刷。</summary>
    [JsonPropertyName("CommandCooldownMs")]
    public int CommandCooldownMs { get; set; } = 300;

    /// <summary>!autoclear 的清理延迟（秒）。</summary>
    [JsonPropertyName("AutoClearDelaySeconds")]
    public float AutoClearDelaySeconds { get; set; } = 3.0f;

    /// <summary>!autoclear 的初始状态。</summary>
    [JsonPropertyName("AutoClearDefault")]
    public bool AutoClearDefault { get; set; } = false;

    /// <summary>是否允许 !restart 执行回合重置（会中断 KeranStrat 录制，谨慎开启）。</summary>
    [JsonPropertyName("AllowRoundRestart")]
    public bool AllowRoundRestart { get; set; } = true;

    /// <summary>!rethrow 时是否把玩家传送回投掷原点（还原轨迹最准）。</summary>
    [JsonPropertyName("TeleportOnRethrow")]
    public bool TeleportOnRethrow { get; set; } = true;

    /// <summary>模拟投掷时按住按键的时长（毫秒），影响投掷力度。</summary>
    [JsonPropertyName("RethrowHoldMs")]
    public int RethrowHoldMs { get; set; } = 120;

    /// <summary>!god 初始状态。</summary>
    [JsonPropertyName("GodModeDefault")]
    public bool GodModeDefault { get; set; } = false;

    /// <summary>!impacts 命中点标记存活秒数。</summary>
    [JsonPropertyName("ImpactMarkerSeconds")]
    public float ImpactMarkerSeconds { get; set; } = 8.0f;

    /// <summary>!impacts 同时存在的最大标记数，防实体堆积。</summary>
    [JsonPropertyName("MaxImpactMarkers")]
    public int MaxImpactMarkers { get; set; } = 80;

    /// <summary>每个玩家每张图最多保存多少个位置。</summary>
    [JsonPropertyName("MaxPositionsPerMap")]
    public int MaxPositionsPerMap { get; set; } = 64;

    /// <summary>每张图最多保存多少个道具点位。</summary>
    [JsonPropertyName("MaxNadesPerMap")]
    public int MaxNadesPerMap { get; set; } = 200;

    /// <summary>备用命令名：key=原命令，value=别名列表。用于冲突规避。</summary>
    [JsonPropertyName("CommandAliases")]
    public Dictionary<string, List<string>> CommandAliases { get; set; } = new();

    /// <summary>与 KeranStrat 共存时，检测到录制中则拒绝 !restart。</summary>
    [JsonPropertyName("RespectKeranStrat")]
    public bool RespectKeranStrat { get; set; } = true;

    /// <summary>服务器控制台执行指令时使用的 SteamID（一般留空）。</summary>
    [JsonPropertyName("ConsoleSteamId")]
    public string ConsoleSteamId { get; set; } = "";
}
