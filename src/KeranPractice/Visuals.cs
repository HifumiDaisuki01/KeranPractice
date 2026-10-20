using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using KeranPractice.Core;
using Microsoft.Extensions.Logging;
using Timer=CounterStrikeSharp.API.Modules.Timers.Timer;
namespace KeranPractice;
public sealed partial class KeranPracticePlugin
{
    private VisualRegistry _visuals=new(192,512);
    private VisualMark[] _visualSnapshot=[];
    private Timer? _visualTimer;
    private bool _visualsReady;
    private long _visualCreates,_visualFiltered;
    private void RegisterVisuals()
    {
        _visuals=new(Config.MaxVisualsPerPlayer,Config.MaxVisualsGlobal);
        // CSS owns the listener ABI/gamedata. Do not install new native signatures or touch foreign entities.
        try{RegisterListener<Listeners.CheckTransmit>(VisualTransmit);_visualsReady=true;}
        catch(Exception e){Logger.LogError(e,"Private visual transmission unavailable; visuals disabled");}
        RegisterEventHandler<EventBulletImpact>((e,_)=>
        {
            var p=e.Userid;
            if(_visualsReady&&p is {IsValid:true,IsBot:false}&&_sessions.TryGetValue(p.SteamID,out var s)&&Current(p,s)&&s.Active&&s.Impacts)
            {
                // Bound high-rate pellets/penetrations independently of registry entity eviction.
                if(s.ImpactTick!=Server.TickCount){s.ImpactTick=Server.TickCount;s.ImpactsThisTick=0;}
                if(++s.ImpactsThisTick<=8)DrawCross(s.Owner,"impact",new(e.X,e.Y,e.Z),2.5f,Color.DeepSkyBlue,Config.ImpactSeconds);
            }
            return HookResult.Continue;
        });
        RegisterListener<Listeners.OnEntityDeleted>(e=>{if(_visuals.Remove(e.EntityHandle.Raw))RefreshVisualSnapshot();});
        AddCommand("kpv2_visual_status","Read-only visual diagnostics",(_,cmd)=>cmd.ReplyToCommand($"visuals ready={_visualsReady}; active={_visualSnapshot.Length}; created={_visualCreates}; filterRemovals={_visualFiltered}; perPlayer={Config.MaxVisualsPerPlayer}; global={Config.MaxVisualsGlobal}"));
        AddCommand("kpv2_visual_probe","Console-only hidden beam geometry/expiry integration check",(p,cmd)=>
        {
            if(p!=null){cmd.ReplyToCommand("Console only");return;}
            if(!_visualsReady){cmd.ReplyToCommand("Private visuals disabled");return;}
            var owner=new Owner(ulong.MaxValue,0,_epoch);
            uint raw=DrawBeam(owner,"probe",new(0,0,-4096),new(10,20,-4076),Color.White,.25f);
            var beam=Resolve<CBeam>(raw);
            bool geometry=beam is {IsValid:true}&&beam.EndPos.X==10&&beam.EndPos.Y==20&&beam.EndPos.Z==-4076;
            cmd.ReplyToCommand($"Hidden beam raw={raw}; geometryReadback={geometry}; expiryPending=true (not a client render test)");
            AddTimer(.8f,()=>Logger.LogInformation("Visual probe raw={Raw}; geometryReadback={Geometry}; expired={Expired}; clientRenderVerified=false",raw,geometry,Resolve<CBeam>(raw) is not {IsValid:true}),TimerFlags.STOP_ON_MAPCHANGE);
        });
    }
    private void VisualCommand(string command,CCSPlayerController p,Session s,CommandInfo cmd)
    {
        if(cmd.ArgCount>2||cmd.ArgCount>1&&cmd.GetArg(1) is not ("on" or "off"))throw new ArgumentException($"用法 !{command} [on/off]");
        if(!_visualsReady)throw new InvalidOperationException("个人可视化暂不可用，请查看服务端日志");
        if(command=="trajectory"&&!_native.Ready)throw new InvalidOperationException("无法跟踪真实道具："+_native.Status);
        bool enabled=command=="impacts"?s.Impacts:s.Trajectory;
        bool on=cmd.ArgCount>1?cmd.GetArg(1)=="on":!enabled;
        if(command=="impacts")s.Impacts=on;else{if(s.Trajectory!=on)s.VisualGeneration++;s.Trajectory=on;}
        if(!on)ClearVisuals(s.Owner,command=="impacts"?"impact":"trajectory");
        Say(p,command=="impacts"?$"个人弹着点已{(on?"开启":"关闭")}（蓝色小十字，{Config.ImpactSeconds:0.#}秒）":$"个人道具轨迹已{(on?"开启":"关闭")}（原投绿、重投橙，{Config.TrajectorySeconds:0.#}秒；仅跟踪启用后的真实投掷）");
    }
    private void RefreshVisualSnapshot()=>_visualSnapshot=_visuals.Marks.ToArray();
    private void VisualTransmit(CCheckTransmitInfoList list)
    {
        var snapshot=_visualSnapshot;if(snapshot.Length==0)return;
        foreach(var (info,player) in list)
        {
            Owner? recipient=null;
            if(player is {IsValid:true,IsBot:false}&&_sessions.TryGetValue(player.SteamID,out var s)&&Current(player,s)&&s.Active)recipient=s.Owner;
            foreach(var mark in snapshot)
            {
                var beam=Resolve<CBeam>(mark.Raw);
                if(beam is not {IsValid:true}||beam.Index!=mark.Index)continue;
                if(VisualRegistry.Visible(mark,recipient))info.TransmitEntities.Add(mark.Index);
                else
                {
                    info.TransmitEntities.Remove(mark.Index);
                    if(!info.TransmitAlways.Equals(default(CFixedBitVecBase)))info.TransmitAlways.Remove(mark.Index);
                    _visualFiltered++;
                }
            }
        }
    }
    private uint DrawBeam(Owner owner,string kind,V3 start,V3 end,Color color,float seconds)
    {
        if(!_visualsReady||!VisualGeometry.Segment(start,end))return uint.MaxValue;
        CBeam? beam=null;
        try
        {
            beam=Utilities.CreateEntityByName<CBeam>("beam");
            if(beam is not {IsValid:true})throw new InvalidOperationException("Engine did not create beam");
            if(beam.Index>=Utilities.MaxEdicts)throw new InvalidOperationException("Beam index outside networked entity range");
            uint raw=beam.EntityHandle.Raw;
            // Register owner before DispatchSpawn, so the first transmit pass is already private.
            foreach(var old in _visuals.Add(new(raw,(int)beam.Index,owner,kind,Server.CurrentTime+seconds)))RemoveVisual(old);
            RefreshVisualSnapshot();
            beam.Globalname=$"keranpractice:v2:visual:{owner.Connection}:{kind}";
            beam.LifeState=1;beam.Damage=0;beam.Width=.7f;beam.EndWidth=.7f;beam.Render=color;
            beam.EndPos.X=end.X;beam.EndPos.Y=end.Y;beam.EndPos.Z=end.Z;
            beam.Teleport(new Vector(start.X,start.Y,start.Z),new QAngle(0,0,0),new Vector(0,0,0));
            beam.DispatchSpawn();Utilities.SetStateChanged(beam,"CBeam","m_vecEndPos");
            _visualCreates++;
            _visualTimer??=AddTimer(.25f,ExpireVisuals,TimerFlags.REPEAT);
            return raw;
        }
        catch(Exception e)
        {
            if(beam is {IsValid:true}){_visuals.Remove(beam.EntityHandle.Raw);beam.Remove();RefreshVisualSnapshot();}
            Logger.LogError(e,"Visual beam failed; renderer disabled safely");DisableVisuals();return uint.MaxValue;
        }
    }
    private void DrawCross(Owner owner,string kind,V3 point,float radius,Color color,float seconds)
    {
        if(!point.Finite||point.Length>100000)return;
        foreach(var (a,b) in VisualGeometry.Cross(point,radius))DrawBeam(owner,kind,a,b,color,seconds);
    }
    private void RemoveVisual(VisualMark mark)
    {
        var beam=Resolve<CBeam>(mark.Raw);
        if(beam is {IsValid:true}&&beam.Globalname.StartsWith("keranpractice:v2:visual:",StringComparison.Ordinal))beam.Remove();
    }
    private void ClearVisuals(Owner? owner=null,string? kind=null)
    {
        foreach(var mark in _visuals.Take(m=>(owner==null||m.Owner==owner)&&(kind==null||m.Kind==kind)))RemoveVisual(mark);
        RefreshVisualSnapshot();
        if(_visualSnapshot.Length==0){_visualTimer?.Kill();_visualTimer=null;}
    }
    private void ExpireVisuals()
    {
        foreach(var mark in _visuals.Take(m=>m.ExpiresAt<=Server.CurrentTime))RemoveVisual(mark);
        RefreshVisualSnapshot();
        if(_visualSnapshot.Length==0){_visualTimer?.Kill();_visualTimer=null;}
    }
    private void DisableVisuals()
    {
        _visualsReady=false;ClearVisuals();
        foreach(var s in _sessions.Values){s.Impacts=false;s.Trajectory=false;}
    }
    private void VisualSample(Observation obs,V3 point,bool terminal=false)
    {
        if(!_visualsReady||!_sessions.TryGetValue(obs.Owner.SteamId,out var s)||s.Owner!=obs.Owner||!s.Active||!s.Trajectory||obs.VisualGeneration!=s.VisualGeneration)return;
        if(Utilities.GetPlayerFromSteamId(s.Owner.SteamId) is not {IsValid:true} p||!Current(p,s))return;
        if(obs.VisualSegments>=96||(!terminal&&Server.CurrentTime-obs.LastVisualTime<.1f))return;
        var start=obs.LastVisualPoint??obs.Shot.Origin;
        if(!VisualGeometry.Segment(start,point))return;
        DrawBeam(obs.Owner,"trajectory",start,point,obs.Replay?Color.Orange:Color.LimeGreen,Config.TrajectorySeconds);
        obs.LastVisualPoint=point;obs.LastVisualTime=Server.CurrentTime;obs.VisualSegments++;
        if(terminal)DrawCross(obs.Owner,"trajectory",point,4,obs.Replay?Color.Orange:Color.LimeGreen,Config.TrajectorySeconds);
    }
}
