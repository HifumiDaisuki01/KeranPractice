using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace KeranPractice;

/// <summary>
/// 投掷物：记录、重投、清理、保存点位。
/// 重投策略：传送回原点 + 恢复视角 + 给道具 + 模拟按键投掷（走引擎真实物理，
/// 因此轨迹与原始投掷在静止状态下一致；移动中投掷会有速度差）。
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

    // ---------- 实体生命周期：记录真实投掷物参数 ----------
    public void OnEntitySpawned(CEntityInstance entity)
    {
        if (entity == null || entity.Entity == null || !ProjectileDesigners.Contains(entity.Entity.DesignerName)) return;
        uint index = entity.Index;
        string designer = entity.Entity.DesignerName;
        if (designer == "inferno") return;
        Server.NextFrame(() =>
        {
            var projectile = Utilities.GetEntityFromIndex<CBaseCSGrenadeProjectile>((int)index);
            if (projectile == null || !projectile.IsValid || projectile.DesignerName != designer || projectile.Globalname == "keranpractice_rethrow") return;
            if (projectile.Thrower.Value?.Controller.Value is not CCSPlayerController player || !player.IsValid) return;
            if (!_plugin.TryGetState(player, out var st) || projectile.AbsOrigin == null || projectile.AbsRotation == null) return;
            var velocity = projectile.InitialVelocity;
            if (velocity == null) return;
            var rec = st.LastGrenade;
            if (rec == null || rec.Map != CurrentMap()) return;
            rec.ProjectileX = projectile.AbsOrigin.X; rec.ProjectileY = projectile.AbsOrigin.Y; rec.ProjectileZ = projectile.AbsOrigin.Z;
            rec.VelocityX = velocity.X; rec.VelocityY = velocity.Y; rec.VelocityZ = velocity.Z;
            rec.AngleX = projectile.AbsRotation.X; rec.AngleY = projectile.AbsRotation.Y; rec.AngleZ = projectile.AbsRotation.Z;
            rec.AngularVelocityX = projectile.AngVelocity.X; rec.AngularVelocityY = projectile.AngVelocity.Y; rec.AngularVelocityZ = projectile.AngVelocity.Z;
            rec.ItemIndex = projectile.ItemIndex;
            rec.HasProjectileSnapshot = true;
        });
    }

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

        st.LastGrenade = new GrenadeRecord
        {
            Type = type,
            Map = CurrentMap(),
            SteamId = st.SteamId,
            X = o.X, Y = o.Y, Z = o.Z,
            Pitch = pawn.EyeAngles.X,
            Yaw = pawn.EyeAngles.Y,
            HoldMs = _plugin.Permissions.RethrowHoldMs,
            ThrownAt = DateTime.UtcNow,
        };

        // 自动清理：延迟后清掉场上的烟雾/火焰（由本次投掷触发）
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
        if (rec == null || rec.Map != CurrentMap()) { PositionSystem.Reply(player, "当前地图没有可重投的道具。"); return; }
        if (!rec.HasProjectileSnapshot) { PositionSystem.Reply(player, "没有捕获真实投掷物速度，拒绝不准确的按键模拟重投。"); return; }
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0) { PositionSystem.Reply(player, "需要存活的角色才能重投。"); return; }
        var designer = rec.Type switch
        {
            "smoke" => "smokegrenade_projectile", "flash" => "flashbang_projectile",
            "he" => "hegrenade_projectile", "molotov" => "molotov_projectile",
            "decoy" => "decoy_projectile", _ => ""
        };
        if (designer == "") { PositionSystem.Reply(player, "不支持的投掷物类型。"); return; }
        // No simulated +attack. This reproduces the projectile physical state independently of the observer.
        // Some CS2 builds require a native factory to initialize smoke/inferno effects; verify in game.
        var entity = Utilities.CreateEntityByName<CBaseCSGrenadeProjectile>(designer);
        if (entity == null || !entity.IsValid) { PositionSystem.Reply(player, "无法创建投掷物实体。"); return; }
        entity.Globalname = "keranpractice_rethrow";
        entity.DispatchSpawn();
        entity.ItemIndex = rec.ItemIndex;
        var pos = new Vector(rec.ProjectileX, rec.ProjectileY, rec.ProjectileZ);
        var vel = new Vector(rec.VelocityX, rec.VelocityY, rec.VelocityZ);
        entity.InitialPosition.X = pos.X; entity.InitialPosition.Y = pos.Y; entity.InitialPosition.Z = pos.Z;
        entity.InitialVelocity.X = vel.X; entity.InitialVelocity.Y = vel.Y; entity.InitialVelocity.Z = vel.Z;
        entity.AngVelocity.X = rec.AngularVelocityX;
        entity.AngVelocity.Y = rec.AngularVelocityY;
        entity.AngVelocity.Z = rec.AngularVelocityZ;
        entity.Thrower.Raw = player.PlayerPawn.Raw;
        entity.OriginalThrower.Raw = player.PlayerPawn.Raw;
        entity.OwnerEntity.Raw = player.PlayerPawn.Raw;
        entity.TeamNum = player.TeamNum;
        entity.Teleport(pos, new QAngle(rec.AngleX, rec.AngleY, rec.AngleZ), vel);
        PositionSystem.Reply(player, "已重建道具实体（真实速度）；请实测落点及烟火效果。");
    }

    // ---------- 清理 ----------

    /// <summary>删除场上所有投掷物与火焰，返回删除数量。</summary>
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
                // Fail closed. Only projectiles with a verified thrower are eligible for personal cleanup.
                if (ownerSteamId.HasValue)
                {
                    if (ent is not CBaseGrenade grenade || grenade.Thrower.Value?.Controller.Value is not CCSPlayerController thrower || thrower.SteamID != ownerSteamId.Value) continue;
                }
                try { ent.Remove(); removed++; } catch { }
            }
        }
        return removed;
    }

    public void Clear(CCSPlayerController player)
    {
        var n = ClearProjectiles(player.SteamID);
        PositionSystem.Reply(player, n > 0 ? $"已清除自己 {n} 个投掷物。"  : "场上没有可清理的投掷物。");
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
            ? $"自动清理已开启（投掷后 {_plugin.Permissions.AutoClearDelaySeconds:F1} 秒清理）。"
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
        if (rec.Map != map) { PositionSystem.Reply(player, "上一颗道具不在当前地图。"); return; }
        var mine = _nades.Where(n => n.Map.Equals(map, StringComparison.OrdinalIgnoreCase) && n.SteamId == st.SteamId).ToList();

        if (mine.Count >= _plugin.Permissions.MaxNadesPerMap && !mine.Any(n => n.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            PositionSystem.Reply(player, $"本图道具点位已达上限（{_plugin.Permissions.MaxNadesPerMap} 个）。");
            return;
        }

        var existing = mine.FirstOrDefault(n =>
            n.SteamId == st.SteamId && n.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing != null) _nades.Remove(existing);

        _nades.Add(new SavedNade
        {
            Name = name.Trim(),
            Map = map,
            SteamId = st.SteamId,
            Type = rec.Type,
            X = rec.X, Y = rec.Y, Z = rec.Z,
            Pitch = rec.Pitch, Yaw = rec.Yaw,
            HoldMs = rec.HoldMs,
            SavedAt = DateTime.UtcNow,
        });

        if (!Persist()) { PositionSystem.Reply(player, "道具存档写入失败，请检查日志。"); return; }
        PositionSystem.Reply(player, $"已保存道具点位 [{name.Trim()}]（{DisplayName(rec.Type)}，{map}）。");
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
            var tag = n.SteamId == st.SteamId ? "我的" : "他人";
            player.PrintToChat($"  {ChatColors.Yellow}#{i}{ChatColors.Default} {n.Name}  [{DisplayName(n.Type)}] {ChatColors.Grey}{tag}{ChatColors.Default}");
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

        // 先按编号
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
            if (player.IsValid) player.ExecuteClientCommandFromServer($"use {ToWeaponName(target.Type)}");
        });

        PositionSystem.Reply(player, $"已就位到 [{target.Name}]，并发放 {DisplayName(target.Type)}。");
    }
}
