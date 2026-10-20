using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace KeranPractice;

public sealed class KeranPracticePlugin : BasePlugin
{
    public override string ModuleName => "KeranPractice";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "Keran Technology";
    public override string ModuleDescription => "CS2 社区训练插件：道具、站位、闪光免疫、BOT 训练";

    // ---------- 运行时 ----------
    public Dictionary<ulong, PlayerState> States { get; } = new();

    public PracticeConfig Config { get; private set; } = new();
    public PermissionService Permissions { get; private set; } = null!;
    public FlashSystem Flash { get; private set; } = null!;
    public PositionSystem Positions { get; private set; } = null!;
    public GrenadeSystem Grenades { get; private set; } = null!;
    public ToolsSystem Tools { get; private set; } = null!;
    public BotSystem Bots { get; private set; } = null!;

    private string _configPath = "";
    private string _dataDir = "";
    private bool _unloading;

    // ---------- 生命周期 ----------

    public override void Load(bool hotReload)
    {
        _unloading = false;
        _configPath = Path.GetFullPath(Path.Combine(
            ModuleDirectory, "..", "..", "configs", "plugins", "KeranPractice", "KeranPractice.json"));
        // Data belongs outside plugin install folder so updating the DLL does not delete saves.
        _dataDir = Path.GetFullPath(Path.Combine(ModuleDirectory, "..", "..", "..", "..", "data", "KeranPractice"));

        Config = JsonStore.Load<PracticeConfig>(_configPath);
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);

        // 首次运行写默认配置
        if (!File.Exists(_configPath)) JsonStore.Save(_configPath, Config);

        MigrateLegacyData(_dataDir);

        Permissions = new PermissionService(Config);
        Flash = new FlashSystem(this);
        Positions = new PositionSystem(this, _dataDir);
        Grenades = new GrenadeSystem(this, _dataDir);
        Tools = new ToolsSystem(this);
        Bots = new BotSystem(this);

        RegisterEventHandler<EventPlayerBlind>(OnPlayerBlind);
        RegisterEventHandler<EventGrenadeThrown>(Grenades.OnGrenadeThrown);
        RegisterEventHandler<EventBulletImpact>(Tools.OnBulletImpact);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        Console.WriteLine($"[KeranPractice] 已加载 v{ModuleVersion}｜配置: {_configPath}｜数据: {_dataDir}");
    }

    public override void Unload(bool hotReload)
    {
        _unloading = true;
        try { Flash.Stop(); } catch { }

        // 恢复被本插件改过的玩家状态，避免热重载后残留
        foreach (var st in States.Values)
        {
            var p = Utilities.GetPlayerFromSteamId(st.SteamId);
            var pawn = p?.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid) continue;
            try
            {
                if (st.NoClip) pawn.MoveType = st.PreviousMoveType ?? MoveType_t.MOVETYPE_WALK;
                if (st.God) pawn.TakesDamage = st.PreviousTakesDamage ?? true;
            }
            catch { }
        }

        foreach (var st in States.Values) Bots.CleanupOwned(st);
        States.Clear();
        Tools.Reset();
        Console.WriteLine("[KeranPractice] 已卸载");
    }

    /// <summary>
    /// v1.0.0 把存档放在插件目录内（升级/重装会丢）。这里把旧文件复制到新目录，
    /// 原文件保留不删，方便回滚；新目录已有同名文件时不覆盖。
    /// </summary>
    private void MigrateLegacyData(string newDir)
    {
        var legacyDir = Path.Combine(ModuleDirectory, "data");
        if (!Directory.Exists(legacyDir)) return;

        foreach (var name in new[] { "positions.json", "nades.json" })
        {
            var src = Path.Combine(legacyDir, name);
            var dst = Path.Combine(newDir, name);
            if (!File.Exists(src) || File.Exists(dst)) continue;
            try
            {
                File.Copy(src, dst, overwrite: false);
                Console.WriteLine($"[KeranPractice] 已迁移旧存档 {name} → {dst}（原文件保留）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeranPractice] 迁移旧存档 {name} 失败: {ex.Message}");
            }
        }
    }

    // ---------- 状态 ----------

    public bool TryGetState(CCSPlayerController player, out PlayerState state)
    {
        state = null!;
        if (player == null || !player.IsValid) return false;

        var id = player.SteamID;
        if (id == 0) return false;

        if (States.TryGetValue(id, out var existing))
        {
            state = existing;
            return true;
        }

        state = new PlayerState { SteamId = id, Slot = player.Slot };
        States[id] = state;
        return true;
    }

    private void CleanupPlayer(ulong steamId)
    {
        if (States.TryGetValue(steamId, out var st))
        {
            Bots.CleanupOwned(st);
            // 玩家断开时只清瞬态，保留 noflash 等偏好由玩家自行决定（这里统一清掉，避免残留）
            st.ResetTransient();
            States.Remove(steamId);
            Flash.StopIfUnused();
        }
    }

    public bool IsCurrentSession(PlayerState state, string map)
        => !_unloading && Server.MapName.Equals(map, StringComparison.OrdinalIgnoreCase)
           && States.TryGetValue(state.SteamId, out var current) && ReferenceEquals(state, current);

    // ---------- 事件 ----------

    private HookResult OnPlayerBlind(EventPlayerBlind e, GameEventInfo info)
    {
        var player = e.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;
        if (!TryGetState(player, out var st) || !st.NoFlash) return HookResult.Continue;

        Flash.ClearFlashNow(player);
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn e, GameEventInfo info)
    {
        var player = e.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;
        if (!TryGetState(player, out var st)) return HookResult.Continue;

        // 出生会重置移动类型/受伤开关，重新应用
        AddTimer(0.2f, () =>
        {
            if (player == null || !player.IsValid) return;
            if (!IsCurrentSession(st, Server.MapName)) return;   // 期间断线/重连过，别用旧状态覆盖新角色
            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid) return;

            try
            {
                if (st.NoClip) pawn.MoveType = MoveType_t.MOVETYPE_NOCLIP;
                if (st.God) pawn.TakesDamage = false;
                if (st.NoFlash && pawn.FlashDuration > 0f) pawn.FlashDuration = 0f;
            }
            catch { }
        });

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect e, GameEventInfo info)
    {
        var player = e.Userid;
        if (player == null) return HookResult.Continue;
        var id = player.SteamID;
        if (id != 0) CleanupPlayer(id);
        return HookResult.Continue;
    }

    // ================= 指令 =================

    /// <summary>统一入口：有效性 → 状态 → 权限 → 冷却 → 执行。</summary>
    private void Run(CCSPlayerController? player, string cmd, Action<CCSPlayerController, PlayerState> action)
    {
        if (player == null || !player.IsValid)
        {
            Console.WriteLine($"[KeranPractice] {cmd} 需要玩家身份执行");
            return;
        }

        if (!TryGetState(player, out var st)) return;

        if (!Permissions.CanUse(st.SteamId, cmd, st.Practice, out var reason))
        {
            PositionSystem.Reply(player, reason);
            return;
        }

        if (!Permissions.CheckCooldown(st, out var remain))
        {
            if (remain > 150) PositionSystem.Reply(player, $"操作太快，请 {remain} ms 后再试。");
            return;
        }

        try { action(player, st); }
        catch (Exception ex)
        {
            Console.WriteLine($"[KeranPractice] {cmd} 异常: {ex.Message}");
            PositionSystem.Reply(player, "指令执行出错，已记录日志。");
        }
    }

    private static string Arg(CommandInfo c, int i)
        => c.ArgCount > i ? (c.GetArg(i) ?? "").Trim() : "";

    private static bool? ParseOnOff(string s)
        => s.ToLowerInvariant() switch
        {
            "on" or "1" or "true" or "开" => true,
            "off" or "0" or "false" or "关" => false,
            _ => null,
        };

    // ---------- 测试脚手架（仅 KPTEST 构建） ----------

#if KPTEST
    [ConsoleCommand("css_kptest", "测试脚手架：用 BOT 驱动功能验证")]
    [CommandHelper(minArgs: 1, usage: "<pos|god|noclip|flash|clear|iso|bot>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnTest(CCSPlayerController? player, CommandInfo command)
    {
        var scenario = Arg(command, 1);
        Console.WriteLine($"[KPTEST] begin {scenario}");
        try { TestHarness.Run(this, scenario); }
        catch (Exception ex) { Console.WriteLine($"[KPTEST] EX {ex}"); }
    }
#endif

    // ---------- P0 ----------

    [ConsoleCommand("css_noflash", "开关闪光免疫")]
    [CommandHelper(minArgs: 0, usage: "[on|off]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnNoFlash(CCSPlayerController? player, CommandInfo command)
        => Run(player, "noflash", (p, st) =>
        {
            var v = Flash.Toggle(p, st, ParseOnOff(Arg(command, 1)));
            PositionSystem.Reply(p, v ? "闪光免疫已开启（只影响你自己）。" : "闪光免疫已关闭。");
        });

    [ConsoleCommand("css_savepos", "保存当前位置")]
    [CommandHelper(minArgs: 0, usage: "[名称]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnSavePos(CCSPlayerController? player, CommandInfo command)
        => Run(player, "savepos", (p, st) => Positions.SavePosition(p, st, Arg(command, 1)));

    [ConsoleCommand("css_tp", "传送到保存的位置")]
    [CommandHelper(minArgs: 0, usage: "[名称]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnTp(CCSPlayerController? player, CommandInfo command)
        => Run(player, "tp", (p, st) => Positions.Teleport(p, st, Arg(command, 1)));

    [ConsoleCommand("css_poslist", "查看本图保存的位置")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnPosList(CCSPlayerController? player, CommandInfo command)
        => Run(player, "poslist", (p, st) => Positions.ListPositions(p, st));

    [ConsoleCommand("css_delpos", "删除位置")]
    [CommandHelper(minArgs: 1, usage: "<名称>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnDelPos(CCSPlayerController? player, CommandInfo command)
        => Run(player, "delpos", (p, st) => Positions.DeletePosition(p, st, Arg(command, 1)));

    [ConsoleCommand("css_last", "回到上一颗道具投掷点")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnLast(CCSPlayerController? player, CommandInfo command)
        => Run(player, "last", (p, st) => Positions.TeleportLast(p, st));

    [ConsoleCommand("css_clear", "清除场上投掷物")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnClear(CCSPlayerController? player, CommandInfo command)
        => Run(player, "clear", (p, st) => Grenades.Clear(p));

    [ConsoleCommand("css_clearall", "管理员：清理全图道具")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnClearAll(CCSPlayerController? player, CommandInfo command)
        => Run(player, "clearall", (p, st) => Grenades.ClearAll(p));

    [ConsoleCommand("css_rethrow", "重投上一颗道具")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRethrow(CCSPlayerController? player, CommandInfo command)
        => Run(player, "rethrow", (p, st) => Grenades.Rethrow(p, st));

    // ---------- P1 ----------

    [ConsoleCommand("css_autoclear", "自动清理投掷物")]
    [CommandHelper(minArgs: 0, usage: "[on|off]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnAutoClear(CCSPlayerController? player, CommandInfo command)
        => Run(player, "autoclear", (p, st) => Grenades.ToggleAutoClear(p, st, ParseOnOff(Arg(command, 1))));

    [ConsoleCommand("css_savenade", "保存道具点位")]
    [CommandHelper(minArgs: 1, usage: "<名称>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnSaveNade(CCSPlayerController? player, CommandInfo command)
        => Run(player, "savenade", (p, st) => Grenades.SaveNade(p, st, Arg(command, 1)));

    [ConsoleCommand("css_nades", "列出本图道具点位")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnNades(CCSPlayerController? player, CommandInfo command)
        => Run(player, "nades", (p, st) => Grenades.ListNades(p, st));

    [ConsoleCommand("css_loadnade", "加载道具点位")]
    [CommandHelper(minArgs: 1, usage: "<名称或编号>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnLoadNade(CCSPlayerController? player, CommandInfo command)
        => Run(player, "loadnade", (p, st) => Grenades.LoadNade(p, st, Arg(command, 1)));

    [ConsoleCommand("css_noclip", "自由飞行")]
    [CommandHelper(minArgs: 0, usage: "[on|off]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnNoClip(CCSPlayerController? player, CommandInfo command)
        => Run(player, "noclip", (p, st) => Tools.ToggleNoClip(p, st, ParseOnOff(Arg(command, 1))));

    [ConsoleCommand("css_god", "无敌模式")]
    [CommandHelper(minArgs: 0, usage: "[on|off]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnGod(CCSPlayerController? player, CommandInfo command)
        => Run(player, "god", (p, st) => Tools.ToggleGod(p, st, ParseOnOff(Arg(command, 1))));

    [ConsoleCommand("css_impacts", "弹着点提示")]
    [CommandHelper(minArgs: 0, usage: "[on|off]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnImpacts(CCSPlayerController? player, CommandInfo command)
        => Run(player, "impacts", (p, st) => Tools.ToggleImpacts(p, st, ParseOnOff(Arg(command, 1))));

    [ConsoleCommand("css_respawn", "重新复活")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRespawn(CCSPlayerController? player, CommandInfo command)
        => Run(player, "respawn", (p, st) => Tools.Respawn(p, st));

    [ConsoleCommand("css_restart", "重新开始回合")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRestart(CCSPlayerController? player, CommandInfo command)
        => Run(player, "restart", (p, st) => Tools.RestartRound(p));

    [ConsoleCommand("css_practice", "开启/关闭训练会话")]
    [CommandHelper(minArgs: 0, usage: "[on|off]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnPractice(CCSPlayerController? player, CommandInfo command)
        => Run(player, "practice", (p, st) => Tools.TogglePractice(p, st, ParseOnOff(Arg(command, 1))));

    [ConsoleCommand("css_phelp", "训练指令帮助")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnPHelp(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;
        if (!TryGetState(player, out var st)) return;
        if (!Permissions.CanUse(st.SteamId, "phelp", st.Practice, out var reason)) { PositionSystem.Reply(player, reason); return; }
        Tools.PrintHelp(player, st);
    }

    // ---------- P2 ----------

    [ConsoleCommand("css_spawn", "回到本方出生点")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnSpawn(CCSPlayerController? player, CommandInfo command)
        => Run(player, "spawn", (p, st) =>
        {
            var team = p.TeamNum == 2 ? "t" : "ct";
            Positions.TeleportSpawn(p, team, 0);
        });

    [ConsoleCommand("css_tspawn", "传送到 T 出生点")]
    [CommandHelper(minArgs: 0, usage: "[编号]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnTSpawn(CCSPlayerController? player, CommandInfo command)
        => Run(player, "tspawn", (p, st) =>
        {
            int.TryParse(Arg(command, 1), out var idx);
            Positions.TeleportSpawn(p, "t", idx);
        });

    [ConsoleCommand("css_ctspawn", "传送到 CT 出生点")]
    [CommandHelper(minArgs: 0, usage: "[编号]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnCTSpawn(CCSPlayerController? player, CommandInfo command)
        => Run(player, "ctspawn", (p, st) =>
        {
            int.TryParse(Arg(command, 1), out var idx);
            Positions.TeleportSpawn(p, "ct", idx);
        });

    [ConsoleCommand("css_bot", "添加 BOT")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnBot(CCSPlayerController? player, CommandInfo command)
        => Run(player, "bot", (p, st) => Bots.AddBot(p, st, "any"));

    [ConsoleCommand("css_tbot", "添加 T BOT")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnTBot(CCSPlayerController? player, CommandInfo command)
        => Run(player, "tbot", (p, st) => Bots.AddBot(p, st, "t"));

    [ConsoleCommand("css_ctbot", "添加 CT BOT")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnCTBot(CCSPlayerController? player, CommandInfo command)
        => Run(player, "ctbot", (p, st) => Bots.AddBot(p, st, "ct"));

    [ConsoleCommand("css_placebot", "把 BOT 放到你的位置")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnPlaceBot(CCSPlayerController? player, CommandInfo command)
        => Run(player, "placebot", (p, st) => Bots.PlaceBot(p, st));

    [ConsoleCommand("css_removebot", "移除 BOT")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRemoveBot(CCSPlayerController? player, CommandInfo command)
        => Run(player, "removebot", (p, st) => Bots.RemoveBot(p, st));

    [ConsoleCommand("css_clearbots", "清理训练 BOT")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnClearBots(CCSPlayerController? player, CommandInfo command)
        => Run(player, "clearbots", (p, st) => Bots.ClearBots(p, st));

    [ConsoleCommand("css_freezebot", "冻结/解冻 BOT")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnFreezeBot(CCSPlayerController? player, CommandInfo command)
        => Run(player, "freezebot", (p, st) => Bots.FreezeBot(p, st));

    [ConsoleCommand("css_swapbot", "与 BOT 交换位置")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnSwapBot(CCSPlayerController? player, CommandInfo command)
        => Run(player, "swapbot", (p, st) => Bots.SwapBot(p, st));
}
