using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
namespace KeranPractice;
public sealed class PlayerToolState
{
    public uint Pawn;
    public MoveType_t? PreviousMove,PreviousActual;
    public bool? PreviousDamage;
    public void Clear(){Pawn=0;PreviousMove=null;PreviousActual=null;PreviousDamage=null;}
}
public sealed partial class KeranPracticePlugin
{
    private CounterStrikeSharp.API.Modules.Timers.Timer? _toolTimer;
    private void EnsureToolWatcher()
    {
        _toolTimer??=AddTimer(.2f,()=>
        {
            if(DefenseBlocksTools())foreach(var s in _sessions.Values)RestoreTools(s);
            if(!_sessions.Values.Any(s=>s.Tools.PreviousMove!=null||s.Tools.PreviousDamage!=null)){_toolTimer?.Kill();_toolTimer=null;}
        },CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
    }
    private void RestoreTools(Session s)
    {
        var t=s.Tools;
        var pawn=Resolve<CCSPlayerPawn>(t.Pawn);
        if(pawn is {IsValid:true}&&pawn.Controller.Raw==s.Controller)
        {
            if(t.PreviousMove is {} move&&pawn.MoveType==MoveType_t.MOVETYPE_NOCLIP)
            {
                pawn.MoveType=move;
                if(pawn.ActualMoveType==MoveType_t.MOVETYPE_NOCLIP)pawn.ActualMoveType=t.PreviousActual??move;
                Utilities.SetStateChanged(pawn,"CBaseEntity","m_MoveType");
            }
            if(t.PreviousDamage is {} damage&&!pawn.TakesDamage)pawn.TakesDamage=damage;
        }
        t.Clear();
    }
    private void RegisterToolsLifecycle()
    {
        void Clear(CCSPlayerController? p)
        {
            if(p is {IsValid:true}&&_sessions.TryGetValue(p.SteamID,out var s)&&Current(p,s))
            {RestoreTools(s);CancelSupply(s);}
        }
        RegisterEventHandler<EventPlayerDeath>((e,_)=>{Clear(e.Userid);return HookResult.Continue;},HookMode.Pre);
        RegisterEventHandler<EventPlayerTeam>((e,_)=>{Clear(e.Userid);return HookResult.Continue;},HookMode.Pre);
        RegisterEventHandler<EventPlayerSpawn>((e,_)=>{Clear(e.Userid);return HookResult.Continue;});
    }
    private void ToolCommand(string command,CCSPlayerController p,Session s,CommandInfo cmd)
    {
        if(DefenseBlocksTools())throw new InvalidOperationException("KSD 防守会话或隔离状态未知，禁止工具干预");
        if(command=="respawn")
        {
            if(p.TeamNum is not (2 or 3))throw new InvalidOperationException("请先加入 T/CT");
            if(p.PawnIsAlive)throw new InvalidOperationException("你已经存活");
            p.Respawn();Say(p,"已请求复活");return;
        }
        var pawn=Pawn(p);var t=s.Tools;
        if(command is "noclip" or "god")
        {
            if(cmd.ArgCount>2||cmd.ArgCount>1&&cmd.GetArg(1) is not ("on" or "off"))throw new ArgumentException($"用法 !{command} [on/off]");
            if(t.Pawn!=pawn.EntityHandle.Raw){RestoreTools(s);t.Pawn=pawn.EntityHandle.Raw;}
            bool enabled=command=="noclip"?t.PreviousMove!=null:t.PreviousDamage!=null;
            bool on=cmd.ArgCount>1?cmd.GetArg(1)=="on":!enabled;
            if(on==enabled){Say(p,on?"已开启":"已关闭");return;}
            if(command=="noclip")
            {
                if(on){t.PreviousMove=pawn.MoveType;t.PreviousActual=pawn.ActualMoveType;pawn.MoveType=MoveType_t.MOVETYPE_NOCLIP;pawn.ActualMoveType=MoveType_t.MOVETYPE_NOCLIP;}
                else{if(pawn.MoveType==MoveType_t.MOVETYPE_NOCLIP)pawn.MoveType=t.PreviousMove!.Value;if(pawn.ActualMoveType==MoveType_t.MOVETYPE_NOCLIP)pawn.ActualMoveType=t.PreviousActual!.Value;t.PreviousMove=null;t.PreviousActual=null;}
                Utilities.SetStateChanged(pawn,"CBaseEntity","m_MoveType");
            }
            else
            {
                if(on){t.PreviousDamage=pawn.TakesDamage;pawn.TakesDamage=false;}
                else{if(!pawn.TakesDamage)pawn.TakesDamage=t.PreviousDamage!.Value;t.PreviousDamage=null;}
            }
            if(on)EnsureToolWatcher();
            Say(p,$"{(command=="noclip"?"自由飞行":"无敌")}已{(on?"开启":"关闭")}；退出训练、死亡、切队或回合结束时恢复");return;
        }
        int team=command=="tspawn"?2:command=="ctspawn"?3:p.TeamNum;
        if(team is not (2 or 3))throw new InvalidOperationException("请先加入 T/CT");
        var spots=Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(team==2?"info_player_terrorist":"info_player_counterterrorist").Where(e=>e.IsValid&&e.AbsOrigin!=null).OrderBy(e=>e.Index).ToArray();
        int index=1;
        if(cmd.ArgCount>2||cmd.ArgCount==2&&(!int.TryParse(cmd.GetArg(1),out index)||index<1))throw new ArgumentException($"用法 !{command} [出生点序号，从1开始]");
        if(index>spots.Length)throw new InvalidOperationException($"本图该阵营共有 {spots.Length} 个出生点");
        var spot=spots[index-1];var before=Standing(p);var origin=Vec(spot.AbsOrigin!);
        Teleport(p,new(Server.MapName,origin with{Z=origin.Z+2},Angle(spot.AbsRotation??new QAngle(0,0,0))));s.Back.Remember(before);
        Say(p,$"已传送到 {(team==2?"T":"CT")} 出生点 {index}/{spots.Length}，阵营不变；!back 可返回");
    }
}
