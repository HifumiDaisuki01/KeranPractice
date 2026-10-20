using System.Reflection;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Plugin.Host;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using KeranPractice.Core;
using Microsoft.Extensions.Logging;
using Timer=CounterStrikeSharp.API.Modules.Timers.Timer;
namespace KeranPractice;
public sealed partial class KeranPracticePlugin
{
    // Read-only, version-bounded compatibility adapter. Unknown KS layout disables supply.
    private bool DefenseBlocksTools(bool automatic=false)
    {
        try
        {
            var query=typeof(Application).GetField("_pluginContextQueryHandler",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(Application.Instance) as IPluginContextQueryHandler;
            if(query==null)return true;
            var ks=query.FindPluginByModuleName("KeranStrat")?.Plugin;
            if(ks==null)return false;
            var type=ks.GetType();
            var mode=type.GetField("_modeDefense",BindingFlags.Instance|BindingFlags.NonPublic);
            var lease=type.GetProperty("DefenseLease",BindingFlags.Instance|BindingFlags.NonPublic);
            if(mode==null||lease==null)return true;
            var training=type.GetField("_trainingMode",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(ks) as string;
            var versus=type.GetField("_modeVersus",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(ks) as bool?;
            var changing=type.GetField("_modeChanging",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(ks) as bool?;
            return SupplyRules.Blocked(mode.GetValue(ks) as bool?,lease.GetValue(ks) as bool?,training,versus,changing,automatic);
        }
        catch(Exception e){Logger.LogWarning("KS isolation check unavailable: {Error}",e.Message);return true;}
    }
    private static void CancelSupply(Session s){foreach(var timer in s.SupplyTasks)timer.Kill();s.SupplyTasks.Clear();}
    private void ScheduleSupply(CCSPlayerController p,Session s,ThrowSnapshot shot)
    {
        if(!s.SupplyEnabled||DefenseBlocksTools(true))return;
        if(s.SupplyTasks.Count>=8)return;
        var pawn=p.PlayerPawn.Raw;var team=p.TeamNum;
        Timer? timer=null;
        timer=AddTimer(.65f,()=>
        {
            if(timer!=null)s.SupplyTasks.Remove(timer);
            if(!Current(p,s)||!s.Active||!s.SupplyEnabled||p.PlayerPawn.Raw!=pawn||p.TeamNum!=team||!p.PawnIsAlive||DefenseBlocksTools(true))return;
            try{EnsureGrenade(p,SupplyRules.Weapon(shot.Kind,team),shot.Kind=="flash"?2:1);}
            catch(Exception e){Say(p,"道具补充失败，请用 !nadesupply 重试");Logger.LogWarning(e,"Grenade supply failed");}
        },TimerFlags.STOP_ON_MAPCHANGE);
        s.SupplyTasks.Add(timer);
    }
    private static void EnsureGrenade(CCSPlayerController p,string name,int count)
    {
        var pawn=Pawn(p);var services=pawn.WeaponServices??throw new InvalidOperationException("武器服务不可用");
        var weapon=services.MyWeapons.Select(h=>h.Value).FirstOrDefault(w=>w is {IsValid:true}&&w.DesignerName==name);
        weapon??=p.GiveNamedItem<CBasePlayerWeapon>(name);
        if(weapon is not {IsValid:true})throw new InvalidOperationException("引擎未发放 "+name);
        var slot=weapon.VData?.PrimaryAmmoType??byte.MaxValue;
        if(slot>=services.Ammo.Length)throw new InvalidOperationException("无法安全识别道具弹药槽");
        // Only the verified grenade's ammo slot; never gun magazines/reserves or global convars.
        services.Ammo[slot]=(ushort)Math.Max(services.Ammo[slot],count);
        Utilities.SetStateChanged(pawn,"CBasePlayerPawn","m_pWeaponServices");
    }
    private void SupplyCommand(CCSPlayerController p,Session s,CommandInfo cmd)
    {
        var arg=cmd.ArgCount>1?cmd.GetArg(1):"fill";
        if(arg=="off"){s.SupplyEnabled=false;CancelSupply(s);Say(p,"个人道具自动补充关闭");return;}
        if(arg is not ("on" or "fill"))throw new ArgumentException("用法 !nadesupply [on/off]");
        if(DefenseBlocksTools())throw new InvalidOperationException("KSD 防守模式或隔离状态未知，禁止补充道具");
        Pawn(p);s.SupplyEnabled=true;
        foreach(var kind in Kinds.Names.Keys)EnsureGrenade(p,SupplyRules.Weapon(kind,p.TeamNum),kind=="flash"?2:1);
        Say(p,DefenseBlocksTools(true)?"道具已手动补齐（双闪）；KS/KSV 不自动补充":"道具已补齐（双闪）；自动补充开启，枪械弹药保持正常消耗");
    }
}
