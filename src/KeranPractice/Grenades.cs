using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using KeranPractice.Core;
using Microsoft.Extensions.Logging;
namespace KeranPractice;
public sealed partial class KeranPracticePlugin
{
    private void Capture(string kind,DynamicHook hook)
    {
        try
        {
            if(_native.ReplayDepth!=0)return;
            var pawnPtr=hook.GetParam<nint>(4);if(pawnPtr==nint.Zero)return;
            var pawn=new CCSPlayerPawn(pawnPtr);if(!pawn.IsValid||pawn.Controller.Value is not {} controller)return;
            var player=new CCSPlayerController(controller.Handle);
            if(!player.IsValid||player.IsBot||!_sessions.TryGetValue(player.SteamID,out var session)||!session.Active||!Current(player,session))return;
            var grenade=new CBaseCSGrenadeProjectile(hook.GetReturn<nint>());if(!grenade.IsValid)return;
            var shot=new ThrowSnapshot(kind,Server.MapName,player.SteamID,player.TeamNum,(ushort)hook.GetParam<int>(5),NativeReplay.Read(hook.GetParam<nint>(0)),NativeReplay.Read(hook.GetParam<nint>(1)),NativeReplay.Read(hook.GetParam<nint>(2)),NativeReplay.Read(hook.GetParam<nint>(3)),Standing(player),Server.TickCount,pawn.EntityHandle.Raw,grenade.GravityScale,grenade.Elasticity);shot=shot with {PlayerVelocity=Vec(pawn.AbsVelocity),OnGround=(pawn.Flags&1)!=0,Crouched=(pawn.Flags&2)!=0};
            if(pawn.WeaponServices?.ActiveWeapon.Value is {IsValid:true} active&&active.DesignerName is "weapon_smokegrenade" or "weapon_flashbang" or "weapon_hegrenade" or "weapon_molotov" or "weapon_incgrenade" or "weapon_decoy")
                shot=shot with {ThrowStrength=new CBaseCSGrenade(active.Handle).ThrowStrength};
            shot.Validate();
            uint raw=grenade.EntityHandle.Raw;
            Server.NextFrame(()=>
            {
                if(!Current(player,session)||player.PlayerPawn.Raw!=shot.OriginalPawn)return;
                var entity=Resolve<CBaseCSGrenadeProjectile>(raw);
                if(entity is not {IsValid:true}||Foreign(entity))return;
                var spin=Angle(entity.AngVelocity);var actual=shot with {AngularVelocity=spin,GravityScale=entity.GravityScale,Elasticity=entity.Elasticity};actual.Validate();
                session.History.Capture(actual,false);long previous=session.Batch;session.Batch++;
                _pawnOwners[shot.OriginalPawn]=session.Owner;
                Track(entity,session.Owner,session.Batch,actual,false);
                ScheduleClear(player,session,previous);
            });
        }
        catch(Exception e){Logger.LogError(e,"Native throw capture rejected safely");}
    }
    private static bool Foreign(CBaseEntity entity)=>!string.IsNullOrEmpty(entity.Globalname)&&!entity.Globalname.StartsWith("keranpractice:v2:",StringComparison.Ordinal);
    private void Replay(CCSPlayerController p,Session session)
    {
        if(_observations.Count>=Config.MaxTrackedEntities)throw new InvalidOperationException("跟踪数量达到上限，请先 !clear");
        if(Server.CurrentTime-session.LastReplayTime<.2)throw new InvalidOperationException("重投过快，请稍等");session.LastReplayTime=Server.CurrentTime;
        var original=session.History.Last??throw new InvalidOperationException("没有可重演的真实投掷物记录；先正常投出一颗道具");
        if(original.Map!=Server.MapName)throw new InvalidOperationException("投掷记录地图不匹配");
        var pawn=Pawn(p);var shot=original with {Team=p.TeamNum,OriginalPawn=p.PlayerPawn.Raw};long previous=session.Batch;
        var grenade=_native.Create(shot,pawn.Handle);grenade.TeamNum=p.TeamNum;grenade.Thrower.Raw=p.PlayerPawn.Raw;grenade.OriginalThrower.Raw=p.PlayerPawn.Raw;grenade.OwnerEntity.Raw=p.PlayerPawn.Raw;
        _pawnOwners[p.PlayerPawn.Raw]=session.Owner;
        Track(grenade,session.Owner,++session.Batch,shot,true);ScheduleClear(p,session,previous);
        Say(p,"已调用原生工厂重演，当前位置不变；正在等待实际引爆/生效事件");
    }
    private void ObserveSpawn(CEntityInstance entity)
    {
        if(entity.DesignerName!="inferno")return;
        uint raw=entity.EntityHandle.Raw;
        Server.NextFrame(()=>
        {
            var inferno=Resolve<CInferno>(raw);if(inferno is not {IsValid:true}||Foreign(inferno))return;
            var ownerRaw=inferno.OwnerEntity.Raw;
            if(_ledger.TryGet(ownerRaw,out var grenade))_ledger.Add(new(raw,grenade.Owner,grenade.Batch,"inferno"));
            else if(_pawnOwners.TryGetValue(ownerRaw,out var owner)&&_sessions.TryGetValue(owner.SteamId,out var session)&&session.Owner==owner)
            {
                // Require an unambiguous native-captured molotov, retaining its exact batch.
                // Never attribute fire by proximity, timing alone, or the most recent unrelated throw.
                var actor=Utilities.GetPlayerFromSteamId(owner.SteamId);
                var candidates=_observations.Values.Where(o=>o.Shot.Kind=="molotov"&&!o.Cancelled&&o.Owner==owner&&o.Shot.OriginalPawn==ownerRaw).ToArray();
                if(actor is {IsValid:true,IsBot:false}&&actor.PlayerPawn.Raw==ownerRaw&&candidates.Length==1)
                    _ledger.Add(new(raw,owner,candidates[0].Batch,"inferno"));
            }
        });
    }
    private void ScheduleClear(CCSPlayerController player,Session session,long through)
    {
        if(!session.AutoClear||through<=0)return;
        session.PendingClear?.Kill();session.PendingClear=AddTimer(session.AutoClearDelay,()=>{session.PendingClear=null;if(Current(player,session)&&session.Active)ClearOwned(session.Owner,through);},TimerFlags.STOP_ON_MAPCHANGE);
    }
    private int ClearOwned(Owner owner,long? through=null)
    {
        int requested=0;
        foreach(var entry in _ledger.Select(owner,through))
        {
            var entity=Resolve<CBaseEntity>(entry.Handle);
            if(entity is {IsValid:true}&&!Foreign(entity)){entity.Remove();requested++;}
            _ledger.Remove(entry.Handle);if(_observations.TryGetValue(entry.Handle,out var obs))obs.Cancelled=true;
        }
        return requested;
    }
    private void ClearAll(CCSPlayerController admin)
    {
        int removed=0,skipped=0;
        foreach(string name in Kinds.Names.Values.Append("inferno"))
        foreach(var e in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(name).ToArray())
        {
            if(!e.IsValid)continue;
            if(Foreign(e)){skipped++;continue;}
            bool proven=_ledger.TryGet(e.EntityHandle.Raw,out _);
            if(!proven)
            {
                var pawn=name=="inferno"?e.OwnerEntity.Value:new CBaseCSGrenadeProjectile(e.Handle).OriginalThrower.Value;
                var player=pawn is {IsValid:true}?Utilities.GetPlayers().FirstOrDefault(p=>!p.IsBot&&p.PlayerPawn.Raw==pawn.EntityHandle.Raw):null;
                proven=player!=null;
            }
            if(!proven){skipped++;continue;}
            if(_observations.TryGetValue(e.EntityHandle.Raw,out var obs))obs.Cancelled=true;
            e.Remove();_ledger.Remove(e.EntityHandle.Raw);removed++;
        }
        Say(admin,$"全局清理已请求 {removed} 个；保护BOT/外部标签/未知归属实体 {skipped} 个（不触碰KeranStrat BOT）");
    }
}
