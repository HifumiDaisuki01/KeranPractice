using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using KeranPractice.Core;
using Microsoft.Extensions.Logging;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
namespace KeranPractice;
public sealed class PracticeConfig : BasePluginConfig
{
    public override int Version {get;set;}=2;
    public List<string> Admins {get;set;}=[];
    public bool TrainingEnvironmentApproved {get;set;}
    public List<string> PublicPersonalCommands {get;set;}=["noflash","savepos","tp","poslist","delpos","last","back","rethrow","clear","autoclear","nadesupply","savenade","loadnade","nades","delnade","respawn","spawn","tspawn","ctspawn","impacts","trajectory"];
    public int MaxPositionsPerMap {get;set;}=64;
    public int MaxLineupsPerMap {get;set;}=64;
    public float AutoClearDelaySeconds {get;set;}=3;
    public bool EnableNativeReplay {get;set;}=true;
    public int MaxTrackedEntities {get;set;}=256;
    public int MaxVisualsPerPlayer {get;set;}=192;
    public int MaxVisualsGlobal {get;set;}=512;
    public float ImpactSeconds {get;set;}=3;
    public float TrajectorySeconds {get;set;}=12;
}
public sealed class Session(Owner owner,uint controller)
{
    public Owner Owner {get;}=owner;
    public uint Controller {get;}=controller;
    public bool Active {get;set;}
    public bool Impacts {get;set;}
    public bool Trajectory {get;set;}
    public long VisualGeneration {get;set;}
    public int ImpactTick {get;set;}=-1;
    public int ImpactsThisTick {get;set;}
    public bool NoFlash {get;set;}
    public bool SupplyEnabled {get;set;}=true;
    public bool CrossModeSupply {get;set;}
    public List<Timer> SupplyTasks {get;}=[];
    public bool AutoClear {get;set;}
    public float AutoClearDelay {get;set;}=3;
    public double LastReplayTime {get;set;}=double.NegativeInfinity;
    public long Batch {get;set;}
    public History History {get;}=new(64);
    public ThrowSnapshot? SelectedLineup {get;set;}
    public PlayerToolState Tools {get;}=new();
    public TeleportToggle Back {get;}=new();
    public Timer? PendingClear {get;set;}
}
[MinimumApiVersion(376)]
public sealed partial class KeranPracticePlugin : BasePlugin,IPluginConfig<PracticeConfig>
{
    public override string ModuleName=>"KeranPractice";
    public override string ModuleVersion=>"2.0.0-phase3-review";
    public override string ModuleAuthor=>"Keran Technology © 2026";
    public override string ModuleDescription=>"Independent practice rewrite; native physics replay";
    public PracticeConfig Config {get;set;}=new();
    private readonly Dictionary<ulong,Session> _sessions=[];
    private readonly Dictionary<int,ulong> _slots=[];
    private long _connection,_epoch;
    private AccessPolicy _policy=new();
    private PrivateStore _store=null!;
    private readonly NativeReplay _native=new();
    private readonly OwnershipLedger _ledger=new();
    private readonly Dictionary<uint,Owner> _pawnOwners=[];
    private Timer? _flashTimer;
    private Timer? _trajectoryTimer;
    public void OnConfigParsed(PracticeConfig config)
    {
        Config=config;_policy=new AccessPolicy {Admins=config.Admins.Select(s=>ulong.TryParse(s,out var v)?v:0).Where(v=>v!=0).ToHashSet(),TrainingEnvironmentApproved=config.Version>=2&&config.TrainingEnvironmentApproved,Public=config.Version>=2?config.PublicPersonalCommands.ToHashSet(StringComparer.Ordinal):[]};
        Config.AutoClearDelaySeconds=float.IsFinite(Config.AutoClearDelaySeconds)?Math.Clamp(Config.AutoClearDelaySeconds,.1f,120):3;Config.MaxTrackedEntities=Math.Clamp(Config.MaxTrackedEntities,16,512);Config.MaxPositionsPerMap=Math.Clamp(Config.MaxPositionsPerMap,1,256);Config.MaxLineupsPerMap=Math.Clamp(Config.MaxLineupsPerMap,1,256);
        Config.MaxVisualsPerPlayer=Math.Clamp(Config.MaxVisualsPerPlayer,12,256);Config.MaxVisualsGlobal=Math.Clamp(Config.MaxVisualsGlobal,12,512);
        Config.ImpactSeconds=float.IsFinite(Config.ImpactSeconds)?Math.Clamp(Config.ImpactSeconds,.5f,10):3;
        Config.TrajectorySeconds=float.IsFinite(Config.TrajectorySeconds)?Math.Clamp(Config.TrajectorySeconds,1,20):12;
    }
    public override void Load(bool hotReload)
    {
        Directory.CreateDirectory(Path.Combine(ModuleDirectory,"data"));_store=new PrivateStore(Path.Combine(ModuleDirectory,"data/archive-v2.json"));
        var legacy=Path.Combine(ModuleDirectory,"data/positions.json");var imported=_store.ImportLegacy(legacy);if(imported>0)Logger.LogInformation("Imported {Count} legacy positions; source retained",imported);
        _native.Created+=Capture;
        if(Config.EnableNativeReplay)_native.Initialize(Path.Combine(ModuleDirectory,"gamedata/keranpractice-v2.json"));
        foreach(var cmd in new[]{"practice","phelp","noflash","savepos","tp","poslist","delpos","last","back","rethrow","clear","clearall","autoclear","nadesupply","autosupply","savenade","loadnade","nades","delnade","noclip","god","respawn","spawn","tspawn","ctspawn","impacts","trajectory"})
        {var name=cmd;AddCommand("css_"+name,"KeranPractice v2: "+name,(player,args)=>Execute(name,player,args));}
        AddCommand("kpv2_status","Read-only diagnostics",(_,cmd)=>cmd.ReplyToCommand($"KeranPractice 2.0.0-phase3-review; native={_native.Status}; sessions={_sessions.Count}; bots=disabled; dataVersion=2"));
        RegisterEventHandler<EventPlayerBlind>((e,_)=>{if(e.Userid is {} p&&_sessions.TryGetValue(p.SteamID,out var s)&&s.NoFlash)ClearFlash(p);return HookResult.Continue;},HookMode.Pre);
        RegisterListener<Listeners.OnClientDisconnect>(slot=>{if(_slots.Remove(slot,out var id))Exit(id);});
        RegisterListener<Listeners.OnMapStart>(_=>ResetMap());
        RegisterListener<Listeners.OnMapEnd>(ResetMap);
        RegisterEventHandler<EventRoundEnd>((_,_)=>{CancelRoundWork();return HookResult.Continue;});
        RegisterListener<Listeners.OnEntitySpawned>(ObserveSpawn);
        RegisterListener<Listeners.OnEntityDeleted>(e=>_ledger.Remove(e.EntityHandle.Raw));
        AddCommand("kpv2_supply_status","Read-only supply isolation diagnostics",(_,cmd)=>cmd.ReplyToCommand($"supply manualBlocked={DefenseBlocksTools()}; autoBlocked={DefenseBlocksTools(true)}; default KS/KSV manual-only, KSD blocked; explicitOverrides={_sessions.Values.Count(s=>s.CrossModeSupply&&s.SupplyEnabled)}"));
        RegisterToolsLifecycle();RegisterEffects();RegisterProbe();RegisterVisuals();
        Logger.LogInformation("KeranPractice v2 loaded; {Native}; old architecture not loaded",_native.Status);
    }
    private Session SessionFor(CCSPlayerController p)
    {
        if(!_sessions.TryGetValue(p.SteamID,out var session)||session.Controller!=p.EntityHandle.Raw)
        {Exit(p.SteamID);session=new Session(new Owner(p.SteamID,++_connection,_epoch),p.EntityHandle.Raw){AutoClearDelay=Config.AutoClearDelaySeconds};_sessions[p.SteamID]=session;}
        _slots[p.Slot]=p.SteamID;return session;
    }
    private static T? Resolve<T>(uint raw) where T:NativeEntity {var handle=new CHandle<T>(raw);return handle.IsValid?handle.Value:null;}
    private bool Current(CCSPlayerController p,Session s)=>p.IsValid&&p.EntityHandle.Raw==s.Controller&&_epoch==s.Owner.MapEpoch&&_sessions.GetValueOrDefault(p.SteamID)==s;
    private static CCSPlayerPawn Pawn(CCSPlayerController p)=>p.PlayerPawn.Value is {IsValid:true,LifeState:0} pawn?pawn:throw new InvalidOperationException("请先进入T/CT并复活");
    internal static V3 Vec(Vector v)=>new(v.X,v.Y,v.Z);
    internal static V3 Angle(QAngle a)=>new(a.X,a.Y,a.Z);
    private static Standing Standing(CCSPlayerController p){var pawn=Pawn(p);return new(Server.MapName,Vec(pawn.AbsOrigin??throw new InvalidOperationException("角色坐标不可读")),Angle(pawn.EyeAngles));}
    private static void Teleport(CCSPlayerController p,Standing pos)
    {
        if(pos.Map!=Server.MapName)throw new InvalidOperationException("地图不匹配");
        Pawn(p).Teleport(new Vector(pos.Origin.X,pos.Origin.Y,pos.Origin.Z),new QAngle(pos.Angles.X,pos.Angles.Y,pos.Angles.Z),new Vector(0,0,0));
    }
    private static void Say(CCSPlayerController p,string text)=>p.PrintToChat("[KeranPractice] "+text);
    private void Execute(string command,CCSPlayerController? p,CommandInfo cmd)
    {
        if(p==null||!p.IsValid||p.IsBot||p.SteamID==0){cmd.ReplyToCommand("此命令需要真实玩家");return;}
        try
        {
            var s=SessionFor(p);
            if(command=="phelp")
            {
                Say(p,"训练：!practice on/off | !noflash [on/off] | !nadesupply [on/off]（默认KS/KSV仅手动）；管理员 !autosupply on/off 跨模式补充");
                Say(p,"位置：!savepos 名 / !tp 名 / !poslist / !delpos 名；!last 返回上次真实投掷站位；!back 往返传送");
                Say(p,"道具：!rethrow 重投已选道具或最新投掷；!clear；!autoclear on/off [秒]；管理员 !clearall");
                Say(p,"私有道具库：!savenade 名 / !loadnade 名（选中并返回站位）/ !nades / !delnade 名；新投掷取消选中");
                Say(p,"工具：!respawn / !spawn [序号] / !tspawn [序号] / !ctspawn [序号]；管理员 !noclip / !god [on/off]；!impacts / !trajectory [on/off] 个人可视化。BOT 未开放。");return;
            }
            if(command=="autosupply")
            {
                if(!_policy.Admins.Contains(p.SteamID))throw new InvalidOperationException("跨模式自动补充仅管理员可用");
                AutoSupplyCommand(p,s,cmd);return;
            }
            if(command=="practice")
            {
                if(!_policy.Admins.Contains(p.SteamID)&&!_policy.TrainingEnvironmentApproved)throw new InvalidOperationException("服务器尚未授权公共训练环境，请联系管理员");
                var arg=cmd.ArgCount>1?cmd.GetArg(1):s.Active?"off":"on";if(arg is not ("on" or "off"))throw new ArgumentException("用法 !practice on/off");
                if(arg=="off"){Exit(p.SteamID);Say(p,"已退出训练，已恢复个人临时状态并取消任务；未更改全局游戏模式");return;}s.Active=true;Say(p,"已进入个人训练会话");return;
            }
            if(!_policy.Allows(p.SteamID,command,s.Active))throw new InvalidOperationException("无权限，或尚未进入服务器已授权的训练会话");
            if(command=="nadesupply"&&!_policy.Admins.Contains(p.SteamID)&&!_policy.TrainingEnvironmentApproved)throw new InvalidOperationException("未授权训练环境");
            if(!s.Active)throw new InvalidOperationException("请先 !practice on");
            var name=cmd.ArgCount>1?Inputs.Name(cmd.GetArg(1)):"default";
            switch(command)
            {
                case "savenade":case "loadnade":case "nades":case "delnade":LineupCommand(command,p,s,cmd);break;
                case "noclip":case "god":case "respawn":case "spawn":case "tspawn":case "ctspawn":ToolCommand(command,p,s,cmd);break;
                case "impacts":case "trajectory":VisualCommand(command,p,s,cmd);break;
                case "nadesupply":SupplyCommand(p,s,cmd);break;
                case "noflash":if(cmd.ArgCount>1&&cmd.GetArg(1) is not ("on" or "off"))throw new ArgumentException("用法 !noflash [on/off]");s.NoFlash=cmd.ArgCount>1?cmd.GetArg(1)=="on":!s.NoFlash;if(s.NoFlash){ClearFlash(p);EnsureFlashTimer();}Say(p,s.NoFlash?"个人免闪开启":"个人免闪关闭");break;
                case "savepos":_store.Save(new(p.SteamID,name,Standing(p)),Config.MaxPositionsPerMap);Say(p,"已保存私人位置 "+name);break;
                case "tp":var before=Standing(p);var pos=_store.Get(p.SteamID,Server.MapName,name).Position;Teleport(p,pos);s.Back.Remember(before);Say(p,"已传送 "+name);break;
                case "back":var current=Standing(p);Teleport(p,s.Back.Swap(current));Say(p,"已返回；再次 !back 可往返");break;
                case "poslist":Say(p,"本图私人位置："+string.Join("、",_store.Positions(p.SteamID,Server.MapName).Select(e=>e.Name)));break;
                case "delpos":if(cmd.ArgCount<2)throw new ArgumentException("用法 !delpos 名称");_store.Delete(p.SteamID,Server.MapName,name);Say(p,"已删除 "+name);break;
                case "last":var shot=s.History.Last??throw new InvalidOperationException("没有本次连接的投掷记录");var old=Standing(p);Teleport(p,shot.Standing);s.Back.Remember(old);Say(p,"已返回上次投掷站位");break;
                case "rethrow":Replay(p,s);break;
                case "clear":var result=ClearOwned(s.Owner);Say(p,$"已请求清理个人道具 {result} 个；未知归属和其他插件道具不动");break;
                case "clearall":ClearAll(p);break;
                case "autoclear":if(cmd.ArgCount>1&&cmd.GetArg(1) is not ("on" or "off"))throw new ArgumentException("用法 !autoclear on/off");if(cmd.ArgCount>2){if(!float.TryParse(cmd.GetArg(2),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var delay)||!float.IsFinite(delay)||delay<.1f||delay>120)throw new ArgumentException("清理延迟必须为0.1–120秒");s.AutoClearDelay=delay;}s.AutoClear=cmd.ArgCount>1?cmd.GetArg(1)=="on":!s.AutoClear;if(!s.AutoClear){s.PendingClear?.Kill();s.PendingClear=null;}Say(p,s.AutoClear?$"自动清理开启，延迟 {s.AutoClearDelay} 秒，仅清理本人的上一批":"自动清理关闭");break;
            }
        }
        catch(Exception e) when(e is InvalidOperationException or ArgumentException or IOException or System.Text.Json.JsonException){Say(p,e.Message);Logger.LogWarning("Command {Command}: {Error}",command,e.Message);}
    }
    private static void ClearFlash(CCSPlayerController p){if(p.PlayerPawn.Value is {IsValid:true} pawn)pawn.FlashDuration=0;}
    private void EnsureFlashTimer()
    {
        _flashTimer??=AddTimer(.1f,()=>{foreach(var (id,s) in _sessions.ToArray())if(s.NoFlash&&Utilities.GetPlayerFromSteamId(id) is {} p&&Current(p,s))ClearFlash(p);if(!_sessions.Values.Any(s=>s.NoFlash)){_flashTimer?.Kill();_flashTimer=null;}},TimerFlags.REPEAT);
    }
    private void Exit(ulong id){foreach(var slot in _slots.Where(e=>e.Value==id).Select(e=>e.Key).ToArray())_slots.Remove(slot);if(_sessions.Remove(id,out var s)){ClearVisuals(s.Owner);RestoreTools(s);s.PendingClear?.Kill();CancelSupply(s);ClearOwned(s.Owner);s.NoFlash=false;foreach(var key in _pawnOwners.Where(e=>e.Value==s.Owner).Select(e=>e.Key).ToArray())_pawnOwners.Remove(key);}}
    private void CancelRoundWork(){ClearVisuals();foreach(var s in _sessions.Values){RestoreTools(s);s.PendingClear?.Kill();s.PendingClear=null;CancelSupply(s);}_ledger.Clear();_pawnOwners.Clear();ClearObservations();}
    private void ResetMap(){foreach(var s in _sessions.Values){RestoreTools(s);CancelSupply(s);}_epoch++;CancelRoundWork();_sessions.Clear();_slots.Clear();_flashTimer?.Kill();_flashTimer=null;}
    public override void Unload(bool hotReload){ResetMap();_native.Dispose();_trajectoryTimer?.Kill();_toolTimer?.Kill();}
}
