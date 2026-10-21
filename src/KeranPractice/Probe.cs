using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using KeranPractice.Core;
using Microsoft.Extensions.Logging;
namespace KeranPractice;
public sealed partial class KeranPracticePlugin
{
    private bool _probeRunning;
    private Queue<string> _probeQueue=[];
    private readonly List<object> _probeResults=[];
    private readonly Dictionary<string,V3> _probeFirst=[];
    private string _probeKind="";
    private int _probeIndex=-1,_probeHurt;
    private bool _probeBlind,_probeEffectSeen;
    private float _probeStarted;
    private V3? _probePoint;
    private void RegisterProbe()
    {
        AddCommand("kpv2_probe","Isolated instance integration fixture only",(p,cmd)=>
        {
            if(p!=null||Environment.GetEnvironmentVariable("KERANPRACTICE_ISOLATED")!="1"){cmd.ReplyToCommand("仅限隔离实例本地控制台");return;}
            if(_probeRunning){cmd.ReplyToCommand("Probe already running");return;}
            _probeQueue=new Queue<string>(Kinds.Names.Keys.SelectMany(k=>new[]{k,k}));_probeResults.Clear();_probeFirst.Clear();_probeRunning=true;
            _trajectoryTimer??=AddTimer(.05f,Sample,TimerFlags.REPEAT);ProbeNext();cmd.ReplyToCommand("Started native-factory fixtures; these are NOT human throw tests");
        });
        RegisterEventHandler<EventPlayerHurt>((e,_)=>{if(_probeRunning)_probeHurt+=e.DmgHealth;return HookResult.Continue;});
        RegisterEventHandler<EventPlayerBlind>((_,_)=>{if(_probeRunning)_probeBlind=true;return HookResult.Continue;});
    }
    private void ProbeNext()
    {
        foreach(var name in Kinds.Names.Values.Append("inferno"))foreach(var e in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(name).ToArray())if(e.IsValid)e.Remove();
        if(_probeQueue.Count==0)
        {
            _probeRunning=false;var file=Path.Combine(ModuleDirectory,"data/native-fixture-results.json");File.WriteAllText(file,JsonSerializer.Serialize(new{SyntheticFactoryFixtures=true,RealHumanThrows=false,Results=_probeResults},new JsonSerializerOptions{WriteIndented=true}));Logger.LogInformation("Native integration fixture finished: {File}",file);return;
        }
        var bot=Utilities.GetPlayers().FirstOrDefault(p=>p.IsBot&&p.IsValid);
        if(bot==null){_probeRunning=false;Logger.LogError("Fixture requires a bot only inside isolated instance");return;}
        if(!bot.PawnIsAlive)bot.Respawn();var pawn=bot.PlayerPawn.Value;
        if(pawn?.AbsOrigin is not {} origin){_probeRunning=false;return;}
        pawn.Health=1000;Utilities.SetStateChanged(pawn,"CBaseEntity","m_iHealth");
        var kind=_probeQueue.Dequeue();ushort item=kind switch{"smoke"=>45,"flash"=>43,"he"=>44,"molotov"=>46,_=>47};
        var spawn=Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("info_player_counterterrorist").FirstOrDefault()?.AbsOrigin??origin;
        var ground=Vec(spawn);pawn.Teleport(new Vector(spawn.X,spawn.Y,spawn.Z+4),new QAngle(0,180,0),new Vector(0,0,0));
        var snapshot=new ThrowSnapshot(kind,Server.MapName,1,2,item,new(ground.X+60,ground.Y,ground.Z+60),new(0,0,0),new(-20,0,30),new(300,0,0),new(Server.MapName,ground,new(0,0,0)),Server.TickCount,pawn.EntityHandle.Raw);
        _probeKind=kind;_probeHurt=0;_probeBlind=false;_probeEffectSeen=false;_probePoint=null;_probeStarted=Server.CurrentTime;
        try {var grenade=_native.Create(snapshot,pawn.Handle);grenade.TeamNum=2;grenade.Thrower.Raw=pawn.EntityHandle.Raw;grenade.OriginalThrower.Raw=pawn.EntityHandle.Raw;grenade.OwnerEntity.Raw=pawn.EntityHandle.Raw;_probeIndex=(int)grenade.Index;Track(grenade,new Owner(1,0,_epoch),0,snapshot,_probeFirst.ContainsKey(kind));}
        catch(Exception e){_probeRunning=false;Logger.LogError(e,"Native fixture failed; not a passing test");}
    }
    private void ProbeEffect(string kind,int index,V3 point){if(_probeRunning&&kind==_probeKind&&index==_probeIndex){_probeEffectSeen=true;_probePoint=point;}}
    private void ProbeTick()
    {
        if(!_probeRunning)return;float elapsed=Server.CurrentTime-_probeStarted;
        if((!_probeEffectSeen||elapsed<3)&&elapsed<12)return;
        var entity=Utilities.GetEntityFromIndex<CBaseCSGrenadeProjectile>(_probeIndex);
        bool smokeVolume=_probeKind=="smoke"&&entity is {IsValid:true}&&new CSmokeGrenadeProjectile(entity.Handle).DidSmokeEffect;
        int burning=Utilities.FindAllEntitiesByDesignerName<CInferno>("inferno").Where(e=>e.IsValid&&e.FireCount>0).Sum(e=>e.FireCount);
        float? difference=null;if(_probePoint is {} point){if(_probeFirst.TryGetValue(_probeKind,out var first))difference=first.Distance(point);else _probeFirst[_probeKind]=point;}
        _probeResults.Add(new{Kind=_probeKind,EffectEvent=_probeEffectSeen,Landing=_probePoint,RepeatLandingDifference=difference,SmokeVolume=smokeVolume,BurningFireCount=burning,HealthDamage=_probeHurt,PlayerBlindEvent=_probeBlind,Elapsed=elapsed,HumanClientValidated=false});ProbeNext();
    }
}
