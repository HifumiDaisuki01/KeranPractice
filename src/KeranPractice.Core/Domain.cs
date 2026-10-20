namespace KeranPractice.Core;

public readonly record struct V3(float X, float Y, float Z)
{
    public bool Finite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
    public float Distance(V3 other) => new V3(X-other.X,Y-other.Y,Z-other.Z).Length;
}
public sealed record Standing(string Map, V3 Origin, V3 Angles);
public sealed record ThrowSnapshot(string Kind, string Map, ulong SteamId, int Team, ushort ItemIndex,
    V3 Origin, V3 Angles, V3 Velocity, V3 AngularVelocity, Standing Standing, int Tick, uint OriginalPawn,
    float GravityScale = 1, float Elasticity = .45f)
{
    public Guid Id {get;init;}=Guid.NewGuid();
    public float? ThrowStrength {get;init;}
    public V3 PlayerVelocity {get;init;}
    public bool OnGround {get;init;}
    public bool Crouched {get;init;}
    public void Validate()
    {
        if (!Kinds.Names.ContainsKey(Kind) || !Origin.Finite || !Angles.Finite || !Velocity.Finite ||
            !AngularVelocity.Finite || !Standing.Origin.Finite || !Standing.Angles.Finite ||
            Velocity.Length > 10000 || Origin.Length > 100000 || Map != Standing.Map || SteamId == 0 ||
            !float.IsFinite(GravityScale) || !float.IsFinite(Elasticity))
            throw new InvalidDataException("Invalid projectile snapshot");
    }
}
public static class Kinds
{
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    { ["smoke"]="smokegrenade_projectile", ["flash"]="flashbang_projectile", ["he"]="hegrenade_projectile", ["molotov"]="molotov_projectile", ["decoy"]="decoy_projectile" };
}
public sealed class History(int limit)
{
    private readonly List<ThrowSnapshot> _entries = [];
    public IReadOnlyList<ThrowSnapshot> Entries => _entries;
    public ThrowSnapshot? Last => _entries.LastOrDefault();
    public void Capture(ThrowSnapshot snapshot, bool replay)
    {
        snapshot.Validate();
        if (replay) return;
        _entries.Add(snapshot);
        if (_entries.Count > Math.Clamp(limit,1,256)) _entries.RemoveAt(0);
    }
}
public sealed class TeleportToggle
{
    public Standing? Return { get; private set; }
    public void Remember(Standing current) => Return = current;
    public Standing Swap(Standing current)
    {
        var target = Return ?? throw new InvalidOperationException("没有可返回的位置");
        if (target.Map != current.Map) throw new InvalidOperationException("返回点不属于当前地图");
        Return = current;
        return target;
    }
}
public readonly record struct Owner(ulong SteamId, long Connection, long MapEpoch);
public sealed record OwnedEntity(uint Handle, Owner Owner, long Batch, string Kind);
public sealed class OwnershipLedger
{
    private readonly Dictionary<uint,OwnedEntity> _entities = [];
    public void Add(OwnedEntity entity) => _entities[entity.Handle] = entity;
    public bool TryGet(uint handle, out OwnedEntity entity) => _entities.TryGetValue(handle,out entity!);
    public void Remove(uint handle) => _entities.Remove(handle);
    public IReadOnlyList<OwnedEntity> Select(Owner owner, long? throughBatch = null) =>
        _entities.Values.Where(e=>e.Owner==owner && (throughBatch==null || e.Batch<=throughBatch)).ToArray();
    public void Clear() => _entities.Clear();
}
public static class Inputs
{
    public static string Name(string value)
    {
        value = value.Trim();
        if (value.Length is <1 or >32 || value.Any(char.IsControl) || value.IndexOfAny([';','"','\\','/'])>=0)
            throw new ArgumentException("名称限1–32字符，不能含控制字符或路径/命令分隔符");
        return value;
    }
}
public sealed class AccessPolicy
{
    public HashSet<ulong> Admins { get; init; } = [];
    public HashSet<string> Public { get; init; } = ["noflash","savepos","tp","poslist","delpos","last","back","rethrow","clear","autoclear","nadesupply","savenade","loadnade","nades","delnade","respawn","spawn","tspawn","ctspawn","impacts","trajectory"];
    public bool TrainingEnvironmentApproved { get; init; }
    public static readonly HashSet<string> AlwaysAdmin = ["autosupply","clearall","noclip","god","bot","tbot","ctbot","placebot","removebot","clearbots","freezebot","swapbot","restart"];
    public bool Allows(ulong id,string command,bool session) => Admins.Contains(id) ||
        (TrainingEnvironmentApproved && session && !AlwaysAdmin.Contains(command) && Public.Contains(command));
}
