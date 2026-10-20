using System.Text.Json.Serialization;

namespace KeranPractice;

/// <summary>保存的一个站位。</summary>
public sealed class SavedPosition
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("map")] public string Map { get; set; } = "";
    [JsonPropertyName("steamId")] public ulong SteamId { get; set; }
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("z")] public float Z { get; set; }
    [JsonPropertyName("pitch")] public float Pitch { get; set; }
    [JsonPropertyName("yaw")] public float Yaw { get; set; }
    [JsonPropertyName("savedAt")] public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>一次投掷的快照，用于 !rethrow / !last / !savenade。</summary>
public sealed class GrenadeRecord
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";      // he / flash / smoke / molotov / decoy
    [JsonPropertyName("map")] public string Map { get; set; } = "";
    [JsonPropertyName("steamId")] public ulong SteamId { get; set; }
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("z")] public float Z { get; set; }
    [JsonPropertyName("pitch")] public float Pitch { get; set; }
    [JsonPropertyName("yaw")] public float Yaw { get; set; }
    [JsonPropertyName("holdMs")] public int HoldMs { get; set; } = 120;
    [JsonPropertyName("thrownAt")] public DateTime ThrownAt { get; set; } = DateTime.UtcNow;
}

/// <summary>保存的道具点位（可长期复用）。</summary>
public sealed class SavedNade
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("map")] public string Map { get; set; } = "";
    [JsonPropertyName("steamId")] public ulong SteamId { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("z")] public float Z { get; set; }
    [JsonPropertyName("pitch")] public float Pitch { get; set; }
    [JsonPropertyName("yaw")] public float Yaw { get; set; }
    [JsonPropertyName("holdMs")] public int HoldMs { get; set; } = 120;
    [JsonPropertyName("savedAt")] public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>磁盘存储结构（positions.json / nades.json）。</summary>
public sealed class PositionFile
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("positions")] public List<SavedPosition> Positions { get; set; } = new();
}

public sealed class NadeFile
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("nades")] public List<SavedNade> Nades { get; set; } = new();
}
