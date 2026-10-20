using System.Text.Json;
namespace KeranPractice.Core;
public sealed record PositionEntry(ulong SteamId, string Name, Standing Position);
public sealed record LineupEntry(ulong SteamId, string Name, ThrowSnapshot Throw);
public sealed class Archive
{
    public int Version { get; set; } = 2;
    public List<PositionEntry> Positions { get; set; } = [];
    public List<LineupEntry> Lineups { get; set; } = [];
}
public sealed class PrivateStore
{
    private readonly string _file;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented=true,PropertyNameCaseInsensitive=true };
    public Archive Data { get; private set; }
    public PrivateStore(string file)
    {
        _file=file;Data=File.Exists(file)?JsonSerializer.Deserialize<Archive>(File.ReadAllText(file),Options)??throw new InvalidDataException("Empty archive"):new();
        if (Data.Version!=2) throw new InvalidDataException("Unsupported data version; original file preserved");
        foreach(var e in Data.Positions) Validate(e);
    }
    private static void Validate(PositionEntry e)
    {
        Inputs.Name(e.Name);
        if(e.SteamId==0||!e.Position.Origin.Finite||!e.Position.Angles.Finite||string.IsNullOrWhiteSpace(e.Position.Map))throw new InvalidDataException("Invalid position data");
    }
    public IReadOnlyList<PositionEntry> Positions(ulong id,string map)=>Data.Positions.Where(p=>p.SteamId==id&&p.Position.Map==map).ToArray();
    public PositionEntry Get(ulong id,string map,string name)=>Positions(id,map).FirstOrDefault(p=>p.Name.Equals(Inputs.Name(name),StringComparison.OrdinalIgnoreCase))??throw new InvalidOperationException("没有该私人位置");
    public void Save(PositionEntry entry,int limit)
    {
        Validate(entry);var list=Data.Positions.ToList();var old=list.FirstOrDefault(p=>p.SteamId==entry.SteamId&&p.Position.Map==entry.Position.Map&&p.Name.Equals(entry.Name,StringComparison.OrdinalIgnoreCase));
        if(old==null&&Positions(entry.SteamId,entry.Position.Map).Count>=limit)throw new InvalidOperationException("位置数量达到上限");
        if(old!=null)list.Remove(old);list.Add(entry);Commit(new Archive{Positions=list,Lineups=Data.Lineups});
    }
    public void Delete(ulong id,string map,string name)
    {
        var entry=Get(id,map,name);var list=Data.Positions.ToList();list.Remove(entry);Commit(new Archive{Positions=list,Lineups=Data.Lineups});
    }
    private void Commit(Archive next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_file))!);var temp=_file+".tmp-"+Guid.NewGuid().ToString("N");
        try { using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,next,Options);stream.Flush(true);} File.Move(temp,_file,true);Data=next; }
        finally { if(File.Exists(temp))File.Delete(temp); }
    }
    public int ImportLegacy(string file)
    {
        if(!File.Exists(file)||File.Exists(_file+".legacy-imported"))return 0;
        using var document=JsonDocument.Parse(File.ReadAllText(file).TrimStart('\uFEFF'));var list=Data.Positions.ToList();int added=0;
        foreach(var e in document.RootElement.GetProperty("positions").EnumerateArray())
        {
            var p=new PositionEntry(e.GetProperty("steamId").GetUInt64(),Inputs.Name(e.GetProperty("name").GetString()!),new Standing(e.GetProperty("map").GetString()!,new(e.GetProperty("x").GetSingle(),e.GetProperty("y").GetSingle(),e.GetProperty("z").GetSingle()),new(e.GetProperty("pitch").GetSingle(),e.GetProperty("yaw").GetSingle(),0)));Validate(p);
            if(list.Any(x=>x.SteamId==p.SteamId&&x.Position.Map==p.Position.Map&&x.Name.Equals(p.Name,StringComparison.OrdinalIgnoreCase)))continue;
            list.Add(p);added++;
        }
        Commit(new Archive{Positions=list,Lineups=Data.Lineups});File.WriteAllText(_file+".legacy-imported","Original source preserved; imported positions only");return added;
    }
}
