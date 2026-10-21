using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using KeranPractice.Core;
using Fn6 = CounterStrikeSharp.API.Modules.Memory.DynamicFunctions.MemoryFunctionWithReturn<nint,nint,nint,nint,nint,int,nint>;
using Fn7 = CounterStrikeSharp.API.Modules.Memory.DynamicFunctions.MemoryFunctionWithReturn<nint,nint,nint,nint,nint,int,int,nint>;
namespace KeranPractice;

public sealed record FactorySpec(string Signature,long FileOffset,string AbiMask="",int[]? RelativeCalls=null,int[]? EntryBoundaries=null);
public sealed class NativeManifest
{
    public string ServerSHA256 { get; set; } = "";
    public Dictionary<string,FactorySpec> Factories { get; set; } = [];
}
// Independently checked SysV ABI: position*, angles*, linear velocity*, angular velocity*, pawn*, item index;
// smoke additionally accepts team on the stack. Never use an entity-API or client-input fallback.
public sealed class NativeReplay : IDisposable
{
    private readonly Dictionary<string,Fn6> _six = [];
    private Fn7? _smoke;
    private readonly List<(BaseMemoryFunction Function,Func<DynamicHook,HookResult> Callback)> _hooks=[];
    public string Status { get; private set; } = "not initialized";
    public bool Ready => _six.Count>0 || _smoke!=null;
    public bool Supports(string kind)=>kind=="smoke"?_smoke!=null:_six.ContainsKey(kind);
    private readonly Dictionary<string,string> _disabled=[];
    public int ReplayDepth { get; private set; }
    public event Action<string,DynamicHook>? Created;
    public void Initialize(string manifestFile)
    {
        try
        {
            if(!OperatingSystem.IsLinux())throw new InvalidOperationException("Windows ABI not verified; native replay disabled");
            var manifest=JsonSerializer.Deserialize<NativeManifest>(File.ReadAllText(manifestFile))??throw new InvalidDataException("Empty native manifest");
            var path=File.ReadLines("/proc/self/maps").Select(l=>l.Split(' ',StringSplitOptions.RemoveEmptyEntries).Last()).FirstOrDefault(p=>p.EndsWith("/csgo/bin/linuxsteamrt64/libserver.so",StringComparison.Ordinal))??throw new FileNotFoundException("Loaded server module not found");
            var bytes=File.ReadAllBytes(path);var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
            var failures=new List<string>();
            foreach(var kind in Kinds.Names.Keys)
            {
                try
                {
                    var spec=manifest.Factories[kind];var audit=NativeAudit.Verify(bytes,spec.Signature,spec.AbiMask,spec.RelativeCalls??[]);
                    // Check the actual running mapping too; an updated file on disk is not proof of loaded code.
                    var maps=File.ReadLines("/proc/self/maps").Select(l=>l.Split(' ',StringSplitOptions.RemoveEmptyEntries)).Where(p=>p.Length>=6&&p[^1]==path).ToArray();
                    var mapping=maps.FirstOrDefault(p=>Convert.ToInt64(p[2],16)<=audit.Offset&&audit.Offset<Convert.ToInt64(p[2],16)+Convert.ToInt64(p[0].Split('-')[1],16)-Convert.ToInt64(p[0].Split('-')[0],16))??throw new InvalidDataException("Loaded mapping missing");
                    long address=Convert.ToInt64(mapping[0].Split('-')[0],16)+audit.Offset-Convert.ToInt64(mapping[2],16);
                    // Detour libraries may split a module into page mappings: validate coverage, not one giant region.
                    long cursor=address,endAddress=address+audit.GuardLength;
                    while(cursor<endAddress)
                    {
                        var region=maps.FirstOrDefault(p=>p[1].Contains('r')&&p[1].Contains('x')&&Convert.ToInt64(p[0].Split('-')[0],16)<=cursor&&cursor<Convert.ToInt64(p[0].Split('-')[1],16))??throw new InvalidDataException("Function crosses unreadable/non-executable mapping");
                        cursor=Math.Min(endAddress,Convert.ToInt64(region[0].Split('-')[1],16));
                    }
                    var live=new byte[audit.GuardLength];Marshal.Copy((nint)address,live,0,live.Length);
                    var cache=typeof(BaseMemoryFunction).GetField("_createdFunctions",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as System.Collections.IDictionary;
                    bool cached=cache?.Contains(spec.Signature)==true&&cache[spec.Signature] is nint cachedHandle&&cachedHandle!=nint.Zero;
                    int prefix=NativeAudit.CssDetourPrefix(bytes.AsSpan((int)audit.Offset,live.Length),live,spec.EntryBoundaries??[],cached);
                    if(prefix<0)throw new InvalidDataException("Loaded function changed outside a cached CSS detour");
                    if(prefix>0)
                    {
                        long target=address+5+BitConverter.ToInt32(live,1);
                        bool executable=File.ReadLines("/proc/self/maps").Select(l=>l.Split(' ',StringSplitOptions.RemoveEmptyEntries)).Any(p=>p[1].Contains('x')&&Convert.ToInt64(p[0].Split('-')[0],16)<=target&&target<Convert.ToInt64(p[0].Split('-')[1],16));
                        if(!executable)throw new InvalidDataException("Cached CSS detour target is not executable");
                    }
                    BaseMemoryFunction f;Fn6? six=null;Fn7? smoke=null;
                    if(kind=="smoke")f=smoke=new Fn7(spec.Signature,path);else f=six=new Fn6(spec.Signature,path);
                    if(f.Handle==nint.Zero)throw new InvalidOperationException("Dynamic function unresolved");
                    Func<DynamicHook,HookResult> callback=ctx=>{Created?.Invoke(kind,ctx);return HookResult.Continue;};
                    f.Hook(callback,HookMode.Post);_hooks.Add((f,callback));
                    if(smoke!=null)_smoke=smoke;else _six[kind]=six!;
                }
                catch(Exception e){_disabled[kind]=e.Message;failures.Add(kind+": "+e.Message);}
            }
            int available=_six.Count+(_smoke!=null?1:0);
            Status=$"{(available>0?"ready":"disabled")}; validated factories={available}/5; SHA256={hash[..12]} (informational)"+(failures.Count>0?"; disabled: "+string.Join(" | ",failures):"");
        }
        catch(Exception e){Dispose();Status="disabled: "+e.Message;}
    }
    public CBaseCSGrenadeProjectile Create(ThrowSnapshot shot, nint pawn)
    {
        if(!Supports(shot.Kind))throw new InvalidOperationException(shot.Kind+": "+_disabled.GetValueOrDefault(shot.Kind,Status));shot.Validate();
        var pos=new Vector(shot.Origin.X,shot.Origin.Y,shot.Origin.Z);var angles=new QAngle(shot.Angles.X,shot.Angles.Y,shot.Angles.Z);
        var vel=new Vector(shot.Velocity.X,shot.Velocity.Y,shot.Velocity.Z);var spin=new Vector(shot.AngularVelocity.X,shot.AngularVelocity.Y,shot.AngularVelocity.Z);
        ReplayDepth++;
        try
        {
            nint ptr=shot.Kind=="smoke"?_smoke!.Invoke(pos.Handle,angles.Handle,vel.Handle,spin.Handle,pawn,shot.ItemIndex,shot.Team):_six[shot.Kind].Invoke(pos.Handle,angles.Handle,vel.Handle,spin.Handle,pawn,shot.ItemIndex);
            if(ptr==nint.Zero)throw new InvalidOperationException("Engine factory returned null");
            var grenade=new CBaseCSGrenadeProjectile(ptr);if(!grenade.IsValid||grenade.DesignerName!=Kinds.Names[shot.Kind])throw new InvalidOperationException("Engine returned unexpected entity");
            grenade.Globalname="keranpractice:v2:replay";
            // Native factories initialize fuse/detonation and physics. Do not DispatchSpawn a second time.
            grenade.AngVelocity.X=shot.AngularVelocity.X;grenade.AngVelocity.Y=shot.AngularVelocity.Y;grenade.AngVelocity.Z=shot.AngularVelocity.Z;
            grenade.Elasticity=shot.Elasticity;grenade.GravityScale=shot.GravityScale;
            grenade.Teleport(pos,angles,vel);
            return grenade;
        }
        finally {ReplayDepth--;GC.KeepAlive(pos);GC.KeepAlive(angles);GC.KeepAlive(vel);GC.KeepAlive(spin);}
    }
    public void Dispose(){foreach(var (f,c) in _hooks)f.Unhook(c,HookMode.Post);_hooks.Clear();_six.Clear();_smoke=null;}
    public static V3 Read(nint pointer)
    {
        if(pointer==nint.Zero)throw new InvalidDataException("Null vector in native call");
        var data=new float[3];Marshal.Copy(pointer,data,0,3);return new(data[0],data[1],data[2]);
    }
}
