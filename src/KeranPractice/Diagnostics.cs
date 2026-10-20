using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using KeranPractice.Core;
using Microsoft.Extensions.Logging;
namespace KeranPractice;
public sealed class Observation(uint raw,int index,Owner owner,ThrowSnapshot shot,bool replay,long batch)
{
    public long Batch {get;}=batch;
    public uint Raw {get;}=raw;
    public int Index {get;}=index;
    public Owner Owner {get;}=owner;
    public ThrowSnapshot Shot {get;}=shot;
    public bool Replay {get;}=replay;
    public List<V3> Path {get;}=[];
    public bool Ended {get;set;}
    public bool Cancelled {get;set;}
    public V3? Effect {get;set;}
    public float Started {get;}=Server.CurrentTime;
}
public sealed partial class KeranPracticePlugin
{
    private readonly Dictionary<uint,Observation> _observations=[];
    private readonly Dictionary<(Owner,Guid),V3> _originalLandings=[];
    private void Track(CBaseCSGrenadeProjectile entity,Owner owner,long batch,ThrowSnapshot shot,bool replay)
    {
        if(_observations.Count>=Config.MaxTrackedEntities)throw new InvalidOperationException("记录实体数量达到上限；停止接受新跟踪");
        _ledger.Add(new(entity.EntityHandle.Raw,owner,batch,shot.Kind));
        _observations[entity.EntityHandle.Raw]=new(entity.EntityHandle.Raw,(int)entity.Index,owner,shot,replay,batch);
        _trajectoryTimer??=AddTimer(.05f,Sample,TimerFlags.REPEAT);
    }
    private void Sample()
    {
        foreach(var (raw,obs) in _observations.ToArray())
        {
            if(!obs.Ended&&!obs.Cancelled)
            {
                var e=Resolve<CBaseEntity>(raw);
                if(e is {IsValid:true}&&e.AbsOrigin is {} origin&&obs.Path.Count<400)obs.Path.Add(Vec(origin));
            }
            if(obs.Cancelled||obs.Ended||Server.CurrentTime-obs.Started>22)
            {
                if(!obs.Ended&&!obs.Cancelled)Logger.LogWarning("Projectile {Kind} {Raw} did not produce a matched terminal effect; not reported successful",obs.Shot.Kind,raw);
                WriteObservation(obs);_observations.Remove(raw);
            }
        }
        ProbeTick();
        if(_observations.Count==0&&!_probeRunning){_trajectoryTimer?.Kill();_trajectoryTimer=null;}
    }
    private void WriteObservation(Observation obs)
    {
        var dir=Path.Combine(ModuleDirectory,"data/diagnostics");Directory.CreateDirectory(dir);
        // Bound diagnostic disk usage as well as in-memory trajectory data.
        var files=Directory.GetFiles(dir,"*.json").OrderBy(File.GetLastWriteTimeUtc).ToArray();foreach(var old in files.Take(Math.Max(0,files.Length-127)))File.Delete(old);
        var key=(obs.Owner,obs.Shot.Id);float? difference=null;
        if(obs.Effect is {} end)
        {
            if(!obs.Replay)_originalLandings[key]=end;
            else if(_originalLandings.TryGetValue(key,out var original))difference=original.Distance(end);
        }
        File.WriteAllText(Path.Combine(dir,$"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{obs.Raw}.json"),JsonSerializer.Serialize(new{obs.Shot,obs.Owner,obs.Replay,obs.Ended,obs.Cancelled,obs.Effect,LandingDifference=difference,Trajectory=obs.Path,HumanClientVerified=false},new JsonSerializerOptions{WriteIndented=true}));
    }
    private void Terminal(string kind,int index,V3 point)
    {
        var candidates=_observations.Values.Where(o=>o.Index==index&&o.Shot.Kind==kind&&!o.Ended&&!o.Cancelled).ToArray();
        if(candidates.Length!=1)return;var obs=candidates[0];obs.Ended=true;obs.Effect=point;
        Logger.LogInformation("Engine effect observed: {Kind} entity={Index} replay={Replay} position={Point}",kind,index,obs.Replay,point);
        if(obs.Replay&&Utilities.GetPlayerFromSteamId(obs.Owner.SteamId) is {IsValid:true,IsBot:false} player&&_sessions.TryGetValue(player.SteamID,out var s)&&s.Owner==obs.Owner)
            Say(player,"重投已收到引爆/生效事件："+kind+"；详细轨迹已记录");
        ProbeEffect(kind,index,point);
    }
    private void RegisterEffects()
    {
        RegisterEventHandler<EventSmokegrenadeDetonate>((e,_)=>{Terminal("smoke",e.Entityid,new(e.X,e.Y,e.Z));return HookResult.Continue;});
        RegisterEventHandler<EventFlashbangDetonate>((e,_)=>{Terminal("flash",e.Entityid,new(e.X,e.Y,e.Z));return HookResult.Continue;});
        RegisterEventHandler<EventHegrenadeDetonate>((e,_)=>{Terminal("he",e.Entityid,new(e.X,e.Y,e.Z));return HookResult.Continue;});
        RegisterEventHandler<EventDecoyStarted>((e,_)=>{Terminal("decoy",e.Entityid,new(e.X,e.Y,e.Z));return HookResult.Continue;});
        RegisterEventHandler<EventInfernoStartburn>((e,_)=>{uint raw=Utilities.GetEntityFromIndex<CInferno>(e.Entityid)?.EntityHandle.Raw??uint.MaxValue;
            Server.NextFrame(()=>{var fire=Resolve<CInferno>(raw);if(fire is not {IsValid:true}||fire.FireCount<=0)return;
                var candidates=_observations.Values.Where(o=>o.Shot.Kind=="molotov"&&!o.Ended&&!o.Cancelled&&o.Shot.OriginalPawn==fire.OwnerEntity.Raw).ToArray();
                if(candidates.Length==1)Terminal("molotov",candidates[0].Index,Vec(fire.OriginalSpawnLocation));});return HookResult.Continue;});
    }
    private void ClearObservations(){_observations.Clear();_originalLandings.Clear();_trajectoryTimer?.Kill();_trajectoryTimer=null;_probeRunning=false;}
}
