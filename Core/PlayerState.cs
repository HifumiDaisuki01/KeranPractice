using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace KeranPractice;

/// <summary>单个玩家的运行时状态。所有字段都由插件独占，不与其他插件共享。</summary>
public sealed class PlayerState
{
    public ulong SteamId { get; init; }
    public int Slot { get; init; }

    // 功能开关
    public bool NoFlash { get; set; }
    public bool God { get; set; }
    public bool NoClip { get; set; }
    public bool IntendedNoClip { get; set; }
    public bool Impacts { get; set; }
    public bool AutoClear { get; set; }
    public bool Practice { get; set; }

    /// <summary>进入 noclip 前的移动类型，退出时恢复。</summary>
    public MoveType_t? PreviousMoveType { get; set; }

    /// <summary>进入 god 前的受伤开关。</summary>
    public bool? PreviousTakesDamage { get; set; }

    /// <summary>最近一次投掷。</summary>
    public GrenadeRecord? LastGrenade { get; set; }

    /// <summary>指令冷却。</summary>
    public DateTime LastCommandAt { get; set; } = DateTime.MinValue;

    /// <summary>本插件创建的 BOT UserId 集合（只管理自己的）。</summary>
    public HashSet<int> OwnedBots { get; } = new();
    // UserId alone may be reused, so also verify the entity handle before mutation.
    public Dictionary<int, ulong> OwnedBotHandles { get; } = new();

    /// <summary>最近一次 !placebot 指向的 BOT UserId。</summary>
    public int? FocusBot { get; set; }

    /// <summary>已冻结的 BOT UserId。</summary>
    public HashSet<int> FrozenBots { get; } = new();

    public void ResetTransient()
    {
        NoClip = false;
        IntendedNoClip = false;
        PreviousMoveType = null;
        Practice = false;
        LastGrenade = null;
        FocusBot = null;
        OwnedBots.Clear();
        OwnedBotHandles.Clear();
        FrozenBots.Clear();
    }
}
