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

public sealed record FactorySpec(string Signature, long FileOffset);
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
    public bool Ready => _six.Count==4 && _smoke!=null;
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
            if(hash!=manifest.ServerSHA256)throw new InvalidDataException("Server SHA256 differs from audited build");
            foreach(var kind in Kinds.Names.Keys)
            {
                var spec=manifest.Factories[kind];int[] pattern=spec.Signature.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(t=>t=="?"?-1:Convert.ToInt32(t,16)).ToArray();int matches=0;long offset=-1;
                for(int i=0;i<=bytes.Length-pattern.Length;i++) {if(bytes[i]!=pattern[0])continue;bool match=true;for(int j=1;j<pattern.Length;j++)if(pattern[j]>=0&&bytes[i+j]!=pattern[j]){match=false;break;}if(match){matches++;offset=i;}}
                if(matches!=1||offset!=spec.FileOffset)throw new InvalidDataException($"{kind}: non-unique or shifted signature");
            }
            foreach(var kind in Kinds.Names.Keys)
            {
                BaseMemoryFunction f;
                if(kind=="smoke")f=_smoke=new Fn7(manifest.Factories[kind].Signature);
                else {var six=new Fn6(manifest.Factories[kind].Signature);_six[kind]=six;f=six;}
                if(f.Handle==nint.Zero)throw new InvalidOperationException($"{kind}: dynamic function unresolved");
                Func<DynamicHook,HookResult> callback=ctx=>{Created?.Invoke(kind,ctx);return HookResult.Continue;};
                f.Hook(callback,HookMode.Post);_hooks.Add((f,callback));
            }
            Status="ready; audited Linux build; five native factories";
        }
        catch(Exception e){Dispose();Status="disabled: "+e.Message;}
    }
    public CBaseCSGrenadeProjectile Create(ThrowSnapshot shot, nint pawn)
    {
        if(!Ready)throw new InvalidOperationException(Status);shot.Validate();
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
