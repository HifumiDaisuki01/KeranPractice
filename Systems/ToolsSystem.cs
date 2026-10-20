using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace KeranPractice;

/// <summary>noclip / god / impacts / respawn / restart / practice / help。</summary>
public sealed class ToolsSystem
{
    private readonly KeranPracticePlugin _plugin;

    // 命中点输出限流：每人每秒最多 5 条
    private readonly Dictionary<ulong, (DateTime At, int Count)> _impactBudget = new();

    public ToolsSystem(KeranPracticePlugin plugin)
    {
        _plugin = plugin;
    }

    public void Reset()
    {
        _impactBudget.Clear();
    }

    // ---------- noclip ----------

    public void ToggleNoClip(CCSPlayerController player, PlayerState st, bool? force)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) { PositionSystem.Reply(player, "角色无效。"); return; }

        var next = force ?? !st.NoClip;

        if (next)
        {
            if (!st.NoClip) st.PreviousMoveType = pawn.MoveType;
            pawn.MoveType = MoveType_t.MOVETYPE_NOCLIP;
            st.NoClip = true;
            PositionSystem.Reply(player, "自由飞行已开启。");
        }
        else
        {
            pawn.MoveType = st.PreviousMoveType ?? MoveType_t.MOVETYPE_WALK;
            st.NoClip = false;
            st.PreviousMoveType = null;
            PositionSystem.Reply(player, "自由飞行已关闭。");
        }
    }

    // ---------- god ----------

    public void ToggleGod(CCSPlayerController player, PlayerState st, bool? force)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) { PositionSystem.Reply(player, "角色无效。"); return; }

        var next = force ?? !st.God;
        if (next && !st.God) st.PreviousTakesDamage = pawn.TakesDamage;
        st.God = next;
        pawn.TakesDamage = next ? false : (st.PreviousTakesDamage ?? true);
        if (!next) st.PreviousTakesDamage = null;
        PositionSystem.Reply(player, next ? "无敌模式已开启。" : "无敌模式已关闭。");
    }

    // ---------- impacts ----------

    public void ToggleImpacts(CCSPlayerController player, PlayerState st, bool? force)
    {
        st.Impacts = force ?? !st.Impacts;
        PositionSystem.Reply(player, st.Impacts
            ? "弹着点提示已开启（控制台输出，受 5 条/秒限流）。"
            : "弹着点提示已关闭。");
    }

    public HookResult OnBulletImpact(EventBulletImpact e, GameEventInfo info)
    {
        var player = e.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;
        if (!_plugin.TryGetState(player, out var st) || !st.Impacts) return HookResult.Continue;

        // 限流
        var now = DateTime.UtcNow;
        if (_impactBudget.TryGetValue(st.SteamId, out var b))
        {
            if ((now - b.At).TotalSeconds >= 1.0)
                _impactBudget[st.SteamId] = (now, 1);
            else if (b.Count >= 5)
                return HookResult.Continue;
            else
                _impactBudget[st.SteamId] = (b.At, b.Count + 1);
        }
        else
        {
            _impactBudget[st.SteamId] = (now, 1);
        }

        var x = e.X; var y = e.Y; var z = e.Z;
        player.PrintToConsole($"[弹着点] X={x:F1} Y={y:F1} Z={z:F1}");
        return HookResult.Continue;
    }

    // ---------- respawn / restart ----------

    public void Respawn(CCSPlayerController player, PlayerState st)
    {
        if (player == null || !player.IsValid) return;
        player.Respawn();
        PositionSystem.Reply(player, "已重新复活。");
    }

    public void RestartRound(CCSPlayerController player)
    {
        if (!_plugin.Permissions.AllowRoundRestart)
        {
            PositionSystem.Reply(player, "本服已禁用回合重置（会中断 KeranStrat 录制）。");
            return;
        }

        if (_plugin.Permissions.RespectKeranStrat)
        {
            PositionSystem.Reply(player, "本服开启了 KeranStrat 兼容保护：回合重置可能中断其录制/回放，已阻止。");
            return;
        }

        Server.ExecuteCommand("mp_restartgame 1");
        PositionSystem.Reply(player, "已请求回合重置。");
    }

    // ---------- practice 会话 ----------

    public void TogglePractice(CCSPlayerController player, PlayerState st, bool? force)
    {
        var next = force ?? !st.Practice;
        st.Practice = next;

        if (!next)
        {
            // 关闭会话：恢复被本插件改过的玩家状态
            var pawn = player.PlayerPawn.Value;
            if (pawn != null && pawn.IsValid)
            {
                if (st.NoClip)
                {
                    pawn.MoveType = st.PreviousMoveType ?? MoveType_t.MOVETYPE_WALK;
                    st.NoClip = false;
                    st.PreviousMoveType = null;
                }
                if (st.God)
                {
                    st.God = false;
                    pawn.TakesDamage = st.PreviousTakesDamage ?? true;
                    st.PreviousTakesDamage = null;
                }
            }
            st.Impacts = false;
            st.NoFlash = false;
            st.AutoClear = false;
            _plugin.Flash.StopIfUnused();
            PositionSystem.Reply(player, "训练会话已结束：免闪、自动清理及临时状态已恢复。");
        }
        else
        {
            PositionSystem.Reply(player, "训练会话已开启。输入 !phelp 查看指令。");
        }
    }

    // ---------- help ----------

    public void PrintHelp(CCSPlayerController player, PlayerState st)
    {
        var isAdmin = _plugin.Permissions.IsAdmin(st.SteamId);
        player.PrintToChat($" {ChatColors.Green}══════ KeranPractice 训练指令 ══════");
        player.PrintToChat($" {ChatColors.Yellow}!practice{ChatColors.Default}  开启/关闭训练会话");
        player.PrintToChat($" {ChatColors.Yellow}!noflash [on|off]{ChatColors.Default}  闪光免疫（个人）");
        player.PrintToChat($" {ChatColors.Yellow}!savepos [名称]{ChatColors.Default}  保存当前位置");
        player.PrintToChat($" {ChatColors.Yellow}!tp [名称]{ChatColors.Default}  传送到保存的位置");
        player.PrintToChat($" {ChatColors.Yellow}!poslist{ChatColors.Default}  查看本图位置");
        player.PrintToChat($" {ChatColors.Yellow}!delpos <名称>{ChatColors.Default}  删除位置");
        player.PrintToChat($" {ChatColors.Yellow}!last{ChatColors.Default}  回到上一颗道具投掷点");
        player.PrintToChat($" {ChatColors.Yellow}!rethrow{ChatColors.Default}  重投上一颗道具");
        player.PrintToChat($" {ChatColors.Yellow}!clear{ChatColors.Default}  清除场上投掷物");
        player.PrintToChat($" {ChatColors.Yellow}!autoclear{ChatColors.Default}  自动清理开关");
        player.PrintToChat($" {ChatColors.Yellow}!savenade <名称>{ChatColors.Default}  保存道具点位");
        player.PrintToChat($" {ChatColors.Yellow}!nades{ChatColors.Default}  列出本图道具");
        player.PrintToChat($" {ChatColors.Yellow}!loadnade <名称/编号>{ChatColors.Default}  加载道具点位");
        player.PrintToChat($" {ChatColors.Yellow}!noclip{ChatColors.Default}  自由飞行");
        player.PrintToChat($" {ChatColors.Yellow}!god{ChatColors.Default}  无敌");
        player.PrintToChat($" {ChatColors.Yellow}!impacts{ChatColors.Default}  弹着点提示");
        player.PrintToChat($" {ChatColors.Yellow}!respawn{ChatColors.Default}  复活");
        player.PrintToChat($" {ChatColors.Yellow}!spawn / !tspawn [n] / !ctspawn [n]{ChatColors.Default}  回出生点");
        player.PrintToChat($" {ChatColors.Yellow}!bot / !tbot / !ctbot{ChatColors.Default}  添加 BOT");
        player.PrintToChat($" {ChatColors.Yellow}!placebot / !removebot / !clearbots / !freezebot / !swapbot{ChatColors.Default}  BOT 管理");

        if (isAdmin)
        {
            player.PrintToChat($" {ChatColors.Red}[管理员]{ChatColors.Default} !restart 回合重置");
        }
    }
}
