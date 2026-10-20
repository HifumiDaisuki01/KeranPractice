using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace KeranPractice;

/// <summary>
/// 投掷物：记录、重投、清理、保存点位。
///
/// 重投（v1.0.1 实测定稿）：CS2 服务端无法用 CreateEntityByName 造出「会引爆」的投掷物。
/// 实测证明手工实体只受物理模拟、永远不会 detonate（1.5s 引信、2s、AcceptInput("Detonate")
/// 全部无效），因此改为「引擎真实投掷回放」：把玩家瞬移回投掷点、恢复当时速度与视角、
/// 补发同一颗道具、由客户端真实按下 attack。成功与否以 grenade_thrown 事件为准，绝不假报。
/// </summary>
public sealed class GrenadeSystem
{
    private readonly KeranPracticePlugin _plugin;
    private readonly List<SavedNade> _nades = new();
    private readonly string _file;

    private static readonly string[] ProjectileDesigners =
    {
        "smokegrenade_projectile",
        "molotov_projectile",
        "flashbang_projectile",
        "hegrenade_projectile",
        "decoy_projectile",
        "inferno",
    };

    public GrenadeSystem(KeranPracticePlugin plugin, string dataDir)
    {
        _plugin = plugin;
        _file = Path.Combine(dataDir, "nades.json");
        Reload();
    }

    public void Reload()
    {
        _nades.Clear();
        var f = JsonStore.Load<NadeFile>(_file);
        if (f.Nades != null) _nades.AddRange(f.Nades);
    }

    private bool Persist() => JsonStore.Save(_file, new NadeFile { Nades = _nades });

    private static string CurrentMap() => Server.MapName ?? "unknown";

    /// <summary>把 CS2 的 weapon_xxx 归一化成内部类型。</summary>
    public static string NormalizeType(string weapon)
    {
        var w = weapon.ToLowerInvariant();
        if (w.Contains("hegrenade")) return "he";
        if (w.Contains("flashbang")) return "flash";
        if (w.Contains("smokegrenade")) return "smoke";
        if (w.Contains("incgrenade") || w.Contains("molotov")) return "molotov";
        if (w.Contains("decoy")) return "decoy";
        return w.Replace("weapon_", "");
    }

    public static string ToWeaponName(string type) => type switch
    {
        "he" => "weapon_hegrenade",
        "flash" => "weapon_flashbang",
        "smoke" => "weapon_smokegrenade",
        "molotov" => "weapon_molotov",
        "decoy" => "weapon_decoy",
        _ => "weapon_" + type,
    };

    public static string DisplayName(string type) => type switch
    {
        "he" => "高爆手雷",
        "flash" => "闪光弹",
        "smoke" => "烟雾弹",
        "molotov" => "燃烧瓶",
        "decoy" => "诱饵弹",
        _ => type,
    };

    // ---------- 事件：记录投掷 ----------

    public HookResult OnGrenadeThrown(EventGrenadeThrown e, GameEventInfo info)
    {
        var player = e.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;
        if (!_plugin.TryGetState(player, out var st)) return HookResult.Continue;

        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return HookResult.Continue;

        var type = NormalizeType(e.Weapon ?? "");
        var o = pawn.AbsOrigin;
        var v = pawn.AbsVelocity;

        // 上一次 !rethrow 的模拟投掷是否真的落地（引擎事件为准）
        if (st.RethrowPending)
        {
            st.RethrowPending = false;
            if (type == st.RethrowExpectedType &&
                (DateTime.UtcNow - st.RethrowRequestedAt).TotalSeconds < 3.0)
            {
                PositionSystem.Reply(player, $"重投成功：{DisplayName(type)} 已由引擎真实投出。");
            }
        }

        st.LastGrenade = new GrenadeRecord
        {
            Type = type,
            Map = CurrentMap(),
            SteamId = st.SteamId,
            X = o.X, Y = o.Y, Z = o.Z,
            Pitch = pawn.EyeAngles.X,
            Yaw = pawn.EyeAngles.Y,
            VelX = v?.X ?? 0f, VelY = v?.Y ?? 0f, VelZ = v?.Z ?? 0f,
            ThrownAt = DateTime.UtcNow,
        };

        if (st.AutoClear)
        {
            var delay = Math.Max(0.1f, _plugin.Permissions.AutoClearDelaySeconds);
            var owner = st.SteamId;
            var map = CurrentMap();
            _plugin.AddTimer(delay, () =>
            {
                if (!_plugin.IsCurrentSession(st, map) || !st.AutoClear) return;
                var n = ClearProjectiles(owner);
                if (n > 0 && player.IsValid) PositionSystem.Reply(player, $"自动清理了自己 {n} 个投掷物。");
            });
        }

        return HookResult.Continue;
    }

    // ---------- 重投 ----------

    public void Rethrow(CCSPlayerController player, PlayerState st)
    {
        var rec = st.LastGrenade;
        if (rec == null || !rec.Map.Equals(CurrentMap(), StringComparison.OrdinalIgnoreCase))
        {
            PositionSystem.Reply(player, "当前地图没有可重投的道具。");
            return;
        }

        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0)
        {
            PositionSystem.Reply(player, "需要存活的角色才能重投。");
            return;
        }

        var weapon = ToWeaponName(rec.Type);
        if (st.RethrowPending && (DateTime.UtcNow - st.RethrowRequestedAt).TotalSeconds < 2.0)
        {
            PositionSystem.Reply(player, "上一次重投还在进行，请稍等。");
            return;
        }

        var savedOrigin = pawn.AbsOrigin;
        var savedVel = pawn.AbsVelocity;
        var savedAngles = pawn.EyeAngles;
        if (savedOrigin == null) { PositionSystem.Reply(player, "读取当前位置失败。"); return; }

        var origin = new Vector(rec.X, rec.Y, rec.Z);
        var angles = new QAngle(rec.Pitch, rec.Yaw, 0f);
        var velocity = new Vector(rec.VelX, rec.VelY, rec.VelZ);

        st.RethrowPending = true;
        st.RethrowExpectedType = rec.Type;
        st.RethrowRequestedAt = DateTime.UtcNow;

        // 1) 回到投掷点，恢复当时的移动速度（跳投/跑投的惯性是轨迹的一部分）
        pawn.Teleport(origin, angles, velocity);
        // 2) 补发同一颗道具并切换
        player.GiveNamedItem(weapon);

        var holdSeconds = Math.Clamp(_plugin.Permissions.RethrowHoldMs, 20, 1000) / 1000f;
        var auto = _plugin.Permissions.AutoThrowOnRethrow;
        var returnAfter = _plugin.Permissions.ReturnAfterRethrow;

        _plugin.AddTimer(0.10f, () =>
        {
            if (!player.IsValid) return;
            player.ExecuteClientCommand($"use {weapon}");
        });

        if (auto)
        {
            _plugin.AddTimer(0.18f, () =>
            {
                if (!player.IsValid) return;
                player.ExecuteClientCommand("+attack");

                _plugin.AddTimer(holdSeconds, () =>
                {
                    if (player.IsValid) player.ExecuteClientCommand("-attack");
                });
            });
        }

        if (returnAfter)
        {
            _plugin.AddTimer(0.62f + holdSeconds, () =>
            {
                if (!player.IsValid || savedOrigin == null) return;
                var p = player.PlayerPawn.Value;
                if (p == null || !p.IsValid) return;
                p.Teleport(new Vector(savedOrigin.X, savedOrigin.Y, savedOrigin.Z),
                           new QAngle(savedAngles.X, savedAngles.Y, 0f),
                           new Vector(savedVel?.X ?? 0f, savedVel?.Y ?? 0f, savedVel?.Z ?? 0f));
            });
        }

        // 3) 1.2 秒后如果引擎没有抛出 grenade_thrown，如实报失败
        _plugin.AddTimer(1.2f, () =>
        {
            if (!st.RethrowPending) return;
            st.RethrowPending = false;
            if (player.IsValid)
            {
                PositionSystem.Reply(player, auto
                    ? "自动投掷未生效（客户端拒绝模拟按键）。已把你放回投掷点并补发道具，请手动投掷。"
                    : "已就位并补发道具，请手动投掷。");
            }
        });

        PositionSystem.Reply(player, auto
            ? $"正在按原始站位/视角/速度重投 {DisplayName(rec.Type)}…"
            : $"已回到投掷点并补发 {DisplayName(rec.Type)}，请手动投掷。");
    }

    // ---------- 清理 ----------

    /// <summary>
    /// 删除投掷物与火焰。ownerSteamId 为空 = 管理员全图清理；
    /// 非空 = 只删「能确认归属于该玩家」的实体，无法确认的一律跳过（fail closed）。
    /// </summary>
    public int ClearProjectiles(ulong? ownerSteamId = null)
    {
        var removed = 0;
        foreach (var designer in ProjectileDesigners)
        {
            List<CBaseEntity> ents;
            try
            {
                ents = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(designer).ToList();
            }
            catch { continue; }

            foreach (var ent in ents)
            {
                if (ent == null || !ent.IsValid) continue;
                if (ownerSteamId.HasValue)
                {
                    if (!TryGetOwnerSteamId(ent, out var owner)) continue;   // 源头不明 → 不动
                    if (owner != ownerSteamId.Value) continue;
                }
                try { ent.Remove(); removed++; } catch { }
            }
        }
        return removed;
    }

    private static bool TryGetOwnerSteamId(CBaseEntity ent, out ulong steamId)
    {
        steamId = 0;
        try
        {
            if (ent is CBaseGrenade grenade)
            {
                var thrower = grenade.Thrower.Value?.Controller.Value;
                if (thrower != null && thrower.IsValid)
                {
                    steamId = thrower.SteamID;
                    return steamId != 0;
                }
            }

            var owner = ent.OwnerEntity.Value;
            switch (owner)
            {
                case CCSPlayerController c when c.IsValid:
                    steamId = c.SteamID;
                    break;
                case CCSPlayerPawn p when p.Controller.Value is CCSPlayerController pc && pc.IsValid:
                    steamId = pc.SteamID;
                    break;
                default:
                    break;
            }
            return steamId != 0;
        }
        catch
        {
            steamId = 0;
            return false;
        }
    }

    public void Clear(CCSPlayerController player)
    {
        var n = ClearProjectiles(player.SteamID);
        PositionSystem.Reply(player, n > 0
            ? $"已清除自己 {n} 个投掷物。"
            : "场上没有能确认属于你的投掷物（他人道具不会被动）。");
    }

    public void ClearAll(CCSPlayerController player)
    {
        var n = ClearProjectiles();
        PositionSystem.Reply(player, $"管理员已清理全图 {n} 个道具实体。");
    }

    public void ToggleAutoClear(CCSPlayerController player, PlayerState st, bool? force)
    {
        st.AutoClear = force ?? !st.AutoClear;
        PositionSystem.Reply(player, st.AutoClear
            ? $"自动清理已开启（投掷后 {_plugin.Permissions.AutoClearDelaySeconds:F1} 秒清理自己的投掷物）。"
            : "自动清理已关闭。");
    }

    // ---------- 保存点位 ----------

    public void SaveNade(CCSPlayerController player, PlayerState st, string name)
    {
        var rec = st.LastGrenade;
        if (rec == null) { PositionSystem.Reply(player, "还没有投掷记录，先扔一颗道具再保存。"); return; }
        if (string.IsNullOrWhiteSpace(name)) { PositionSystem.Reply(player, "用法：!savenade <名称>"); return; }
        if (name.Length > 32) { PositionSystem.Reply(player, "名称过长（最多 32 字符）。"); return; }

        var map = CurrentMap();
        if (!rec.Map.Equals(map, StringComparison.OrdinalIgnoreCase))
        {
            PositionSystem.Reply(player, "上一颗道具不在当前地图。");
            return;
        }

        var mine = _nades.Where(n => n.Map.Equals(map, StringComparison.OrdinalIgnoreCase) && n.SteamId == st.SteamId).ToList();
        var key = name.Trim();
        var existing = mine.FirstOrDefault(n => n.Name.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (existing == null && mine.Count >= _plugin.Permissions.MaxNadesPerMap)
        {
            PositionSystem.Reply(player, $"本图道具点位已达上限（{_plugin.Permissions.MaxNadesPerMap} 个）。");
            return;
        }

        if (existing != null) _nades.Remove(existing);

        _nades.Add(new SavedNade
        {
            Name = key,
            Map = map,
            SteamId = st.SteamId,
            Type = rec.Type,
            X = rec.X, Y = rec.Y, Z = rec.Z,
            Pitch = rec.Pitch, Yaw = rec.Yaw,
            SavedAt = DateTime.UtcNow,
        });

        if (!Persist()) { PositionSystem.Reply(player, "道具存档写入失败，请检查日志。"); return; }
        PositionSystem.Reply(player, existing != null
            ? $"已覆盖道具点位 [{key}]（{DisplayName(rec.Type)}，{map}）。"
            : $"已保存道具点位 [{key}]（{DisplayName(rec.Type)}，{map}）。");
    }

    public void ListNades(CCSPlayerController player, PlayerState st)
    {
        var map = CurrentMap();
        var mine = _nades.Where(n => n.Map.Equals(map, StringComparison.OrdinalIgnoreCase) && n.SteamId == st.SteamId)
                         .OrderBy(n => n.Name).ToList();

        if (mine.Count == 0) { PositionSystem.Reply(player, $"本图（{map}）还没有保存的道具。"); return; }

        PositionSystem.Reply(player, $"本图已保存 {mine.Count} 个道具点位：");
        var i = 1;
        foreach (var n in mine)
        {
            player.PrintToChat($"  {ChatColors.Yellow}#{i}{ChatColors.Default} {n.Name}  [{DisplayName(n.Type)}]");
            i++;
        }
    }

    public void LoadNade(CCSPlayerController player, PlayerState st, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) { PositionSystem.Reply(player, "用法：!loadnade <名称或编号>"); return; }

        var map = CurrentMap();
        var all = _nades.Where(n => n.Map.Equals(map, StringComparison.OrdinalIgnoreCase) && n.SteamId == st.SteamId)
                        .OrderBy(n => n.Name).ToList();
        if (all.Count == 0) { PositionSystem.Reply(player, "本图还没有保存的道具点位。"); return; }

        SavedNade? target = null;

        if (int.TryParse(key, out var idx) && idx >= 1 && idx <= all.Count)
            target = all[idx - 1];
        else
            target = all.FirstOrDefault(n => n.Name.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase));

        if (target == null) { PositionSystem.Reply(player, $"没有找到 [{key}]。用 !nades 查看列表。"); return; }

        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0)
        {
            PositionSystem.Reply(player, "当前状态无法传送（请先复活）。");
            return;
        }

        pawn.Teleport(new Vector(target.X, target.Y, target.Z),
                      new QAngle(target.Pitch, target.Yaw, 0f),
                      new Vector(0f, 0f, 0f));

        player.GiveNamedItem(ToWeaponName(target.Type));
        _plugin.AddTimer(0.1f, () =>
        {
            if (player.IsValid) player.ExecuteClientCommand($"use {ToWeaponName(target.Type)}");
        });

        PositionSystem.Reply(player, $"已就位到 [{target.Name}]，并发放 {DisplayName(target.Type)}。");
    }
}
