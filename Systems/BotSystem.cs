using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.ValveConstants.Protobuf;

namespace KeranPractice;

/// <summary>
/// BOT 管理。只操作本插件自己创建的 BOT（按 UserId 归属），
/// 不触碰其他插件或服务器原生的 BOT。
/// </summary>
public sealed class BotSystem
{
    private readonly KeranPracticePlugin _plugin;

    private bool Available(CCSPlayerController p)
    {
        if (_plugin.Permissions.EnableBotManagement) return true;
        PositionSystem.Reply(p, "BOT 管理已安全禁用；仅在独立测试服显式开启 EnableBotManagement。");
        return false;
    }

    private bool Owned(CCSPlayerController bot, PlayerState state)
        => bot.IsValid && bot.IsBot && bot.UserId is int id && state.OwnedBotHandles.TryGetValue(id, out var handle)
           && handle == bot.EntityHandle.Raw;

    public void CleanupOwned(PlayerState st)
    {
        // Never disconnect bots on unload or player disconnect: those bots may now be managed by KeranStrat.
        st.OwnedBots.Clear(); st.OwnedBotHandles.Clear(); st.FrozenBots.Clear(); st.FocusBot = null;
    }

    public BotSystem(KeranPracticePlugin plugin)
    {
        _plugin = plugin;
    }

    public void AddBot(CCSPlayerController player, PlayerState st, string team)
    {
        if (!Available(player)) return;
        // Never infer ownership from all bots that appeared during an asynchronous window.
        var before = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && p.IsBot)
            .Select(p => p.UserId ?? 0)
            .ToHashSet();

        var cmd = team switch
        {
            "t" => "bot_add_t",
            "ct" => "bot_add_ct",
            _ => "bot_add",
        };

        Server.ExecuteCommand(cmd);

        // 延迟确认新 BOT，记录归属
        var map = Server.MapName;
        _plugin.AddTimer(0.5f, () =>
        {
            var after = Utilities.GetPlayers()
                .Where(p => p != null && p.IsValid && p.IsBot)
                .ToList();

            var added = after.Where(p => !before.Contains(p.UserId ?? 0)).ToList();
            if (!_plugin.IsCurrentSession(st, map) || player == null || !player.IsValid) return;
            if (added.Count != 1)
            {
                PositionSystem.Reply(player, "无法唯一识别本次创建的 BOT，拒绝认领；请在独立训练服重试。");
                return;
            }
            foreach (var b in added)
            {
                var id = b.UserId ?? 0;
                if (id == 0) continue;
                st.OwnedBots.Add(id);
                st.OwnedBotHandles[id] = b.EntityHandle.Raw;
                st.FocusBot = id;
            }

            if (added.Count == 0)
            {
                PositionSystem.Reply(player, "BOT 添加失败（可能已达上限或服务器未启用 BOT）。");
            }
            else
            {
                PositionSystem.Reply(player, $"已添加 {added.Count} 个 BOT（{(team == "t" ? "T" : team == "ct" ? "CT" : "自动")}），当前指向最后一个。");
            }
        });
    }

    private CCSPlayerController? ResolveOwnedBot(CCSPlayerController player, PlayerState st, out string error)
    {
        error = "";

        if (st.FocusBot.HasValue)
        {
            var b = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && p.IsBot && (p.UserId ?? 0) == st.FocusBot.Value);
            if (b != null && Owned(b, st)) return b;
        }

        // 退而求其次：取自己创建的、仍存活的任意一个
        var any = Utilities.GetPlayers().FirstOrDefault(p =>
            p != null && p.IsValid && p.IsBot && Owned(p, st));
        if (any != null) return any;

        error = "没有可用的训练 BOT。先 !bot 添加，或用 !placebot 指向一个。";
        return null;
    }

    public void PlaceBot(CCSPlayerController player, PlayerState st)
    {
        if (!Available(player)) return;
        var bot = ResolveOwnedBot(player, st, out var err);
        if (bot == null) { PositionSystem.Reply(player, err); return; }

        var myPawn = player.PlayerPawn.Value;
        var botPawn = bot.PlayerPawn.Value;
        if (myPawn?.AbsOrigin == null || botPawn == null || !botPawn.IsValid)
        {
            PositionSystem.Reply(player, "位置读取失败。");
            return;
        }

        var o = myPawn.AbsOrigin;
        botPawn.Teleport(new Vector(o.X, o.Y, o.Z),
                         new QAngle(0f, myPawn.EyeAngles.Y, 0f),
                         new Vector(0f, 0f, 0f));

        PositionSystem.Reply(player, $"已把 BOT 放到你的位置（UserId {bot.UserId}）。");
    }

    public void RemoveBot(CCSPlayerController player, PlayerState st)
    {
        if (!Available(player)) return;
        var bot = ResolveOwnedBot(player, st, out var err);
        if (bot == null) { PositionSystem.Reply(player, err); return; }

        var id = bot.UserId ?? 0;
        st.OwnedBots.Remove(id);
        st.OwnedBotHandles.Remove(id);
        if (st.FocusBot == id) st.FocusBot = null;

        bot.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
        PositionSystem.Reply(player, $"已移除 BOT（UserId {id}）。");
    }

    public void ClearBots(CCSPlayerController player, PlayerState st)
    {
        if (!Available(player)) return;
        var all = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && p.IsBot && Owned(p, st))
            .ToList();

        foreach (var b in all)
        {
            try { b.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED); } catch { }
        }

        var n = all.Count;
        st.OwnedBots.Clear();
        st.OwnedBotHandles.Clear();
        st.FrozenBots.Clear();
        st.FocusBot = null;

        PositionSystem.Reply(player, n > 0 ? $"已清理 {n} 个本插件创建的 BOT。" : "没有属于你的训练 BOT。");
    }

    public void FreezeBot(CCSPlayerController player, PlayerState st)
    {
        if (!Available(player)) return;
        var bot = ResolveOwnedBot(player, st, out var err);
        if (bot == null) { PositionSystem.Reply(player, err); return; }

        var id = bot.UserId ?? 0;
        var pawn = bot.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) { PositionSystem.Reply(player, "BOT 角色无效。"); return; }

        if (st.FrozenBots.Contains(id))
        {
            pawn.MoveType = MoveType_t.MOVETYPE_WALK;
            st.FrozenBots.Remove(id);
            PositionSystem.Reply(player, "BOT 已解冻。");
        }
        else
        {
            pawn.MoveType = MoveType_t.MOVETYPE_NONE;
            st.FrozenBots.Add(id);
            PositionSystem.Reply(player, "BOT 已冻结。");
        }
    }

    public void SwapBot(CCSPlayerController player, PlayerState st)
    {
        if (!Available(player)) return;
        var bot = ResolveOwnedBot(player, st, out var err);
        if (bot == null) { PositionSystem.Reply(player, err); return; }

        var myPawn = player.PlayerPawn.Value;
        var botPawn = bot.PlayerPawn.Value;
        if (myPawn?.AbsOrigin == null || botPawn?.AbsOrigin == null)
        {
            PositionSystem.Reply(player, "位置读取失败。");
            return;
        }

        var mine = myPawn.AbsOrigin;
        var theirs = botPawn.AbsOrigin;

        var myAngle = new QAngle(myPawn.EyeAngles.X, myPawn.EyeAngles.Y, 0f);

        myPawn.Teleport(new Vector(theirs.X, theirs.Y, theirs.Z), null, new Vector(0f, 0f, 0f));
        botPawn.Teleport(new Vector(mine.X, mine.Y, mine.Z), null, new Vector(0f, 0f, 0f));

        PositionSystem.Reply(player, "已与 BOT 交换位置。");
    }
}
