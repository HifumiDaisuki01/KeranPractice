using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;

namespace KeranPractice;

/// <summary>
/// 闪光免疫。
/// 实现：EventPlayerBlind 事件触发时立即清零 FlashDuration；
/// 同时用一个 0.1 秒的低频定时器兜底（只在有人开启免闪时运行）。
/// 不修改闪光弹实体，不影响其他人。
/// </summary>
public sealed class FlashSystem
{
    private readonly KeranPracticePlugin _plugin;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _timer;

    public FlashSystem(KeranPracticePlugin plugin)
    {
        _plugin = plugin;
    }

    public void Start()
    {
        if (_timer != null) return;
        _timer = _plugin.AddTimer(0.1f, Tick, TimerFlags.REPEAT);
    }

    public void StopIfUnused()
    {
        if (!_plugin.States.Values.Any(s => s.NoFlash)) Stop();
    }

    public void Stop()
    {
        _timer?.Kill();
        _timer = null;
    }

    private void Tick()
    {
        // 没有任何人开启免闪时，保持定时器空转成本极低（早退）
        var any = false;
        foreach (var st in _plugin.States.Values)
        {
            if (!st.NoFlash) continue;
            any = true;

            var player = Utilities.GetPlayerFromSteamId(st.SteamId);
            if (player == null || !player.IsValid) continue;
            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid) continue;

            if (pawn.FlashDuration > 0f) pawn.FlashDuration = 0f;
        }

        // 长时间没人用就停掉定时器，避免无意义占用
        if (!any) Stop();
    }

    /// <summary>切换/设置免闪状态。返回切换后的值。</summary>
    public bool Toggle(CCSPlayerController player, PlayerState state, bool? force)
    {
        var next = force ?? !state.NoFlash;
        state.NoFlash = next;

        if (next)
        {
            ClearFlashNow(player);
            Start();
        }
        else StopIfUnused();

        return next;
    }

    public void ClearFlashNow(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return;
        pawn.FlashDuration = 0f;
    }
}
