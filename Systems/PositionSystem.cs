using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace KeranPractice;

/// <summary>位置保存、传送、出生点。</summary>
public sealed class PositionSystem
{
    private readonly KeranPracticePlugin _plugin;
    private readonly List<SavedPosition> _positions = new();
    private readonly string _file;

    public PositionSystem(KeranPracticePlugin plugin, string dataDir)
    {
        _plugin = plugin;
        _file = Path.Combine(dataDir, "positions.json");
        Reload();
    }

    public void Reload()
    {
        _positions.Clear();
        var f = JsonStore.Load<PositionFile>(_file);
        if (f.Positions != null) _positions.AddRange(f.Positions);
    }

    private bool Persist() => JsonStore.Save(_file, new PositionFile { Positions = _positions });

    private static string CurrentMap() => Server.MapName ?? "unknown";

    public IReadOnlyList<SavedPosition> ForPlayer(ulong steamId, string? map = null)
    {
        var m = map ?? CurrentMap();
        return _positions.Where(p => p.SteamId == steamId && p.Map.Equals(m, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // ---------- 指令实现 ----------

    public void SavePosition(CCSPlayerController player, PlayerState st, string? name)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) { Reply(player, "角色无效，无法保存。"); return; }

        var origin = pawn.AbsOrigin;
        if (origin == null) { Reply(player, "读取坐标失败。"); return; }

        var map = CurrentMap();
        var key = string.IsNullOrWhiteSpace(name) ? "default" : name.Trim();

        if (key.Length > 32) { Reply(player, "名称过长（最多 32 字符）。"); return; }

        var list = ForPlayer(st.SteamId, map);

        if (list.Count >= _plugin.Permissions.MaxPositionsPerMap && list.All(p => !p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)))
        {
            Reply(player, $"本图位置已达上限（{_plugin.Permissions.MaxPositionsPerMap} 个），请先 !delpos 删除。");
            return;
        }

        var existing = list.FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (existing != null) _positions.Remove(existing);

        _positions.Add(new SavedPosition
        {
            Name = key,
            Map = map,
            SteamId = st.SteamId,
            X = origin.X,
            Y = origin.Y,
            Z = origin.Z,
            Pitch = pawn.EyeAngles.X,
            Yaw = pawn.EyeAngles.Y,
            SavedAt = DateTime.UtcNow,
        });

        if (!Persist()) { Reply(player, "位置保存失败，请检查服务端日志。"); return; }
        Reply(player, existing != null
            ? $"已覆盖位置 [{key}]（{map}）。"
            : $"已保存位置 [{key}]（{map}）。");
    }

    public void Teleport(CCSPlayerController player, PlayerState st, string? name)
    {
        if (!CanTeleport(player)) { Reply(player, "当前状态无法传送（请先复活并等待出生）。"); return; }

        var key = string.IsNullOrWhiteSpace(name) ? "default" : name.Trim();
        var map = CurrentMap();
        var pos = _positions.FirstOrDefault(p =>
            p.SteamId == st.SteamId &&
            p.Map.Equals(map, StringComparison.OrdinalIgnoreCase) &&
            p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (pos == null)
        {
            var others = ForPlayer(st.SteamId, map);
            Reply(player, others.Count == 0
                ? $"本图还没有保存的位置。用 !savepos {key} 先存一个。"
                : $"没有名为 [{key}] 的位置。已有：{string.Join(", ", others.Select(p => p.Name))}");
            return;
        }

        DoTeleport(player, new Vector(pos.X, pos.Y, pos.Z), new QAngle(pos.Pitch, pos.Yaw, 0f));
        Reply(player, $"已传送到 [{pos.Name}]。");
    }

    public void TeleportLast(CCSPlayerController player, PlayerState st)
    {
        var rec = st.LastGrenade;
        if (rec == null) { Reply(player, "还没有投掷记录。先扔一颗道具。"); return; }
        if (!rec.Map.Equals(CurrentMap(), StringComparison.OrdinalIgnoreCase))
        {
            Reply(player, $"上颗道具是在 {rec.Map} 投的，当前地图是 {CurrentMap()}，无法传送。");
            return;
        }
        if (!CanTeleport(player)) { Reply(player, "当前状态无法传送。"); return; }

        DoTeleport(player, new Vector(rec.X, rec.Y, rec.Z), new QAngle(rec.Pitch, rec.Yaw, 0f));
        Reply(player, "已回到上一颗道具的投掷位置。");
    }

    public void ListPositions(CCSPlayerController player, PlayerState st)
    {
        var list = ForPlayer(st.SteamId);
        if (list.Count == 0) { Reply(player, $"本图（{CurrentMap()}）没有保存的位置。"); return; }

        Reply(player, $"本图已保存 {list.Count} 个位置：");
        foreach (var p in list.OrderBy(p => p.Name))
            player.PrintToChat($"  {ChatColors.Yellow}{p.Name}{ChatColors.Default}  ({p.X:F0}, {p.Y:F0}, {p.Z:F0})");
    }

    public void DeletePosition(CCSPlayerController player, PlayerState st, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) { Reply(player, "用法：!delpos <名称>"); return; }

        var map = CurrentMap();
        var pos = _positions.FirstOrDefault(p =>
            p.SteamId == st.SteamId &&
            p.Map.Equals(map, StringComparison.OrdinalIgnoreCase) &&
            p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

        if (pos == null) { Reply(player, $"没有名为 [{name}] 的位置。"); return; }

        _positions.Remove(pos);
        if (!Persist()) { Reply(player, "删除后写盘失败，请检查服务端日志。"); return; }
        Reply(player, $"已删除位置 [{pos.Name}]。");
    }

    // ---------- 出生点 ----------

    public void TeleportSpawn(CCSPlayerController player, string team, int index)
    {
        if (!CanTeleport(player)) { Reply(player, "当前状态无法传送。"); return; }

        var designer = team.Equals("t", StringComparison.OrdinalIgnoreCase)
            ? "info_player_terrorist"
            : "info_player_counterterrorist";

        var ents = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(designer)
            .Where(e => e != null && e.IsValid && e.AbsOrigin != null)
            .OrderBy(e => e.EntityHandle.Index)
            .ToList();

        if (ents.Count == 0) { Reply(player, $"未找到 {team.ToUpperInvariant()} 出生点。"); return; }

        var idx = index <= 0 ? 0 : Math.Min(index - 1, ents.Count - 1);
        var target = ents[idx];
        var o = target.AbsOrigin!;
        var a = target.AbsRotation;

        DoTeleport(player,
            new Vector(o.X, o.Y, o.Z + 2f),
            a != null ? new QAngle(0f, a.Y, 0f) : null);

        Reply(player, $"已传送到 {team.ToUpperInvariant()} 出生点 #{idx + 1}（共 {ents.Count} 个）。");
    }

    // ---------- 内部 ----------

    private static bool CanTeleport(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return false;
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return false;
        if (pawn.Health <= 0) return false;   // 死亡/未出生
        return true;
    }

    private static void DoTeleport(CCSPlayerController player, Vector pos, QAngle? angle)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null) return;

        // 第三个参数清零速度，避免惯性滑动
        pawn.Teleport(pos, angle, new Vector(0f, 0f, 0f));
    }

    public static void Reply(CCSPlayerController player, string msg)
        => player.PrintToChat($" {ChatColors.Green}[训练]{ChatColors.Default} {msg}");
}
