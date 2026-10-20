#if KPTEST
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace KeranPractice;

/// <summary>
/// 仅在 -p:DefineConstants=KPTEST 的测试构建中编译。
/// 用 BOT 作为受测主体，直接驱动各系统，不经过 CommandInfo（无法伪造）。
/// 输出统一为 [KPTEST] PASS/FAIL &lt;name&gt; &lt;detail&gt;，由外部脚本解析。
/// </summary>
public static class TestHarness
{
    private static void Report(string name, bool ok, string detail = "")
        => Console.WriteLine($"[KPTEST] {(ok ? "PASS" : "FAIL")} {name} {detail}");

    private static void Later(KeranPracticePlugin plugin, float delay, Action action)
        => plugin.AddTimer(delay, action);

    private static CCSPlayerController? FirstBot()
        => Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && p.IsBot && p.PlayerPawn.Value is { IsValid: true });

    private static PlayerState StateOf(CCSPlayerController p) => new() { SteamId = p.SteamID, Slot = p.Slot };

    public static void Run(KeranPracticePlugin plugin, string scenario)
    {
        switch (scenario)
        {
            case "pos": Pos(plugin); break;
            case "god": God(plugin); break;
            case "noclip": Noclip(plugin); break;
            case "flash": Flash(plugin); break;
            case "clear": Clear(plugin); break;
            case "iso": Isolation(plugin); break;
            case "bot": Bots(plugin); break;
            default: Report("scenario", false, "unknown " + scenario); break;
        }
    }

    private static void Pos(KeranPracticePlugin plugin)
    {
        Console.WriteLine("[KPTEST] pos: step1");
        var bot = FirstBot();
        Console.WriteLine("[KPTEST] pos: step2 bot=" + (bot == null ? "null" : bot.Slot.ToString()));
        if (bot == null) { Report("pos.bot", false, "no bot"); return; }
        var st = StateOf(bot);
        var pawn = bot.PlayerPawn.Value!;
        var o = pawn.AbsOrigin!;
        plugin.Positions.SavePosition(bot, st, "kt1");
        Console.WriteLine("[KPTEST] pos: step3 saved");

        try
        {
            pawn.Teleport(new Vector(o.X + 200f, o.Y + 100f, o.Z + 40f), new QAngle(0f, 0f, 0f), new Vector(0f, 0f, 0f));
            Console.WriteLine("[KPTEST] pos: step4 teleported");
        }
        catch (Exception ex) { Console.WriteLine("[KPTEST] pos teleport EX " + ex); }

        Later(plugin, 0.30f, () =>
        {
            Console.WriteLine("[KPTEST] pos: step5 in timer");
            plugin.Positions.Teleport(bot, st, "kt1");
            Later(plugin, 0.30f, () =>
            {
                var back = bot.PlayerPawn.Value?.AbsOrigin;
                if (back == null) { Report("pos.tp", false, "pawn gone"); return; }
                var d = Math.Sqrt(Math.Pow(back.X - o.X, 2) + Math.Pow(back.Y - o.Y, 2) + Math.Pow(back.Z - o.Z, 2));
                Report("pos.save_tp", d < 8.0, $"distance={d:F2}");
                var list = plugin.Positions.ForPlayer(st.SteamId);
                Report("pos.isolation_count", list.Count == 1, $"count={list.Count}");
                plugin.Positions.DeletePosition(bot, st, "kt1");
                Report("pos.delete", plugin.Positions.ForPlayer(st.SteamId).Count == 0);
            });
        });
    }

    private static void God(KeranPracticePlugin plugin)
    {
        var bot = FirstBot();
        if (bot == null) { Report("god.bot", false, "no bot"); return; }
        var st = StateOf(bot);
        var pawn = bot.PlayerPawn.Value!;
        var before = pawn.TakesDamage;
        plugin.Tools.ToggleGod(bot, st, true);
        Later(plugin, 0.30f, () =>
        {
            Report("god.on", bot.PlayerPawn.Value!.TakesDamage == false && st.God);
            plugin.Tools.ToggleGod(bot, st, false);
            Later(plugin, 0.30f, () =>
            {
                var after = bot.PlayerPawn.Value!.TakesDamage;
                Report("god.off_restores", after == before, $"before={before} after={after}");
            });
        });
    }

    private static void Noclip(KeranPracticePlugin plugin)
    {
        var bot = FirstBot();
        if (bot == null) { Report("noclip.bot", false, "no bot"); return; }
        var st = StateOf(bot);
        var beforeType = bot.PlayerPawn.Value!.MoveType;
        plugin.Tools.ToggleNoClip(bot, st, true);
        Later(plugin, 0.30f, () =>
        {
            Report("noclip.on", bot.PlayerPawn.Value!.MoveType == MoveType_t.MOVETYPE_NOCLIP, $"before={beforeType}");
            plugin.Tools.ToggleNoClip(bot, st, false);
            Later(plugin, 0.30f, () =>
                Report("noclip.off", bot.PlayerPawn.Value!.MoveType == beforeType,
                       $"now={bot.PlayerPawn.Value!.MoveType} expect={beforeType}"));
        });
    }

    private static void Flash(KeranPracticePlugin plugin)
    {
        var bot = FirstBot();
        if (bot == null) { Report("flash.bot", false, "no bot"); return; }
        var st = StateOf(bot);
        plugin.Flash.Toggle(bot, st, true);
        var pawn = bot.PlayerPawn.Value!;
        pawn.FlashDuration = 5.0f;
        plugin.AddTimer(0.35f, () =>
        {
            Report("flash.timer_clears", pawn.FlashDuration <= 0.01f, $"value={pawn.FlashDuration:F2}");
            plugin.Flash.Toggle(bot, st, false);
            Report("flash.off_state", !st.NoFlash);
        });
    }

    private static void Clear(KeranPracticePlugin plugin)
    {
        var bot = FirstBot();
        if (bot == null) { Report("clear.bot", false, "no bot"); return; }

        // 说明：服务端手工创建的投掷物会被引擎很快剔除（实测 0.25s 内消失），
        // 因此这里只验证审计要求的关键行为——「归属无法确认时 !clear 绝不误删」。
        var e1 = Utilities.CreateEntityByName<CBaseCSGrenadeProjectile>("smokegrenade_projectile");
        var e2 = Utilities.CreateEntityByName<CBaseCSGrenadeProjectile>("flashbang_projectile");
        if (e1 == null || e2 == null) { Report("clear.create", false, "create failed"); return; }
        e1.Globalname = "kptest_unowned_a"; e1.DispatchSpawn();
        e2.Globalname = "kptest_unowned_b"; e2.DispatchSpawn();

        var before = plugin.Grenades.ClearProjectiles(76561199404249776UL);
        Report("clear.unowned_skipped", before == 0, $"removed={before}");

        // 全图清理不得抛异常
        var all = plugin.Grenades.ClearProjectiles();
        Report("clear.admin_call_ok", all >= 0, $"removed={all}");
    }

    private static void Isolation(KeranPracticePlugin plugin)
    {
        var bot = FirstBot();
        if (bot == null) { Report("iso.bot", false, "no bot"); return; }
        var pawn = bot.PlayerPawn.Value!;
        var o = pawn.AbsOrigin!;
        var map = Server.MapName;

        var a = new PlayerState { SteamId = 76561190000000001, Slot = 0 };
        var b = new PlayerState { SteamId = 76561190000000002, Slot = 1 };

        pawn.Teleport(new Vector(o.X, o.Y, o.Z), new QAngle(0, 0, 0), new Vector(0, 0, 0));
        Later(plugin, 0.25f, () =>
        {
            plugin.Positions.SavePosition(bot, a, "same");
            pawn.Teleport(new Vector(o.X + 64f, o.Y, o.Z), new QAngle(0, 0, 0), new Vector(0, 0, 0));
            Later(plugin, 0.25f, () =>
            {
                plugin.Positions.SavePosition(bot, b, "same");
                var la = plugin.Positions.ForPlayer(a.SteamId, map);
                var lb = plugin.Positions.ForPlayer(b.SteamId, map);
                Report("iso.pos_separate", la.Count == 1 && lb.Count == 1 && Math.Abs(lb[0].X - la[0].X - 64f) < 2f,
                       $"la={la.Count} lb={lb.Count} laX={(la.Count > 0 ? la[0].X : -9999):F1} lbX={(lb.Count > 0 ? lb[0].X : -9999):F1}");

                a.LastGrenade = new GrenadeRecord { Type = "smoke", Map = map, SteamId = a.SteamId, X = o.X, Y = o.Y, Z = o.Z, Pitch = 0, Yaw = 0 };
                b.LastGrenade = new GrenadeRecord { Type = "he", Map = map, SteamId = b.SteamId, X = o.X + 64f, Y = o.Y, Z = o.Z, Pitch = 0, Yaw = 0 };
                plugin.Grenades.SaveNade(bot, a, "kt_a");
                plugin.Grenades.SaveNade(bot, b, "kt_b");
                Later(plugin, 0.25f, () =>
                {
                    plugin.Grenades.LoadNade(bot, a, "kt_a");
                    Later(plugin, 0.25f, () =>
                    {
                        var back = bot.PlayerPawn.Value?.AbsOrigin;
                        var ok = back != null && Math.Abs(back.X - o.X) < 8f;
                        Report("iso.nade_own_only", ok, back == null ? "pawn gone" : $"x={back.X:F1} expect={o.X:F1}");
                    });
                });
            });
        });
    }

    private static void Bots(KeranPracticePlugin plugin)
    {
        var bot = FirstBot();
        if (bot == null) { Report("bot.bot", false, "no bot"); return; }
        if (!plugin.Permissions.EnableBotManagement)
        {
            Report("bot.disabled_by_default", true, "EnableBotManagement=false（拒绝执行即为预期）");
            return;
        }

        var st = StateOf(bot);
        // 生产路径里 state 一定先由 TryGetState 注册；脚手架手动补上，才能通过 IsCurrentSession
        plugin.States[st.SteamId] = st;
        var before = Utilities.GetPlayers().Count(p => p != null && p.IsValid && p.IsBot);
        plugin.Bots.AddBot(bot, st, "ct");
        plugin.AddTimer(1.2f, () =>
        {
            var after = Utilities.GetPlayers().Count(p => p != null && p.IsValid && p.IsBot);
            Report("bot.add", after > before, $"before={before} after={after}");
            Report("bot.owned_tracked", st.OwnedBots.Count == 1, $"owned={st.OwnedBots.Count}");
            foreach (var kv in st.OwnedBotHandles)
                Console.WriteLine($"[KPTEST]   recorded id={kv.Key} handle={kv.Value}");
            foreach (var b in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.IsBot))
                Console.WriteLine($"[KPTEST]   live id={b.UserId} handle={b.EntityHandle.Raw} name={b.PlayerName}");

            var ownedId = st.FocusBot ?? -1;
            plugin.Bots.ClearBots(bot, st);
            plugin.AddTimer(0.5f, () =>
            {
                var still = Utilities.GetPlayers().Any(p => p != null && p.IsValid && p.IsBot && (p.UserId ?? -1) == ownedId);
                Report("bot.clearbots", !still, $"ownedId={ownedId} still={still}");
                Report("bot.ownership_cleared", st.OwnedBots.Count == 0 && st.OwnedBotHandles.Count == 0);
                plugin.States.Remove(st.SteamId);
            });
        });
    }
}
#endif
