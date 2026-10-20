using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using KeranPractice.Core;
namespace KeranPractice;
public sealed partial class KeranPracticePlugin
{
    private void LineupCommand(string command,CCSPlayerController p,Session s,CommandInfo cmd)
    {
        if(command=="nades")
        {
            var entries=_store.Lineups(p.SteamID,Server.MapName);
            Say(p,$"本图私人道具 {entries.Count}/{Config.MaxLineupsPerMap}：");
            foreach(var chunk in entries.Chunk(4))Say(p,string.Join("；",chunk.Select(e=>$"{e.Name} [{e.Throw.Kind}]")));
            return;
        }
        if(cmd.ArgCount!=2)throw new ArgumentException($"用法 !{command} 名称（含空格请加引号）");
        var name=Inputs.Name(cmd.GetArg(1));
        if(command=="savenade")
        {
            var shot=s.SelectedLineup??s.History.Last??throw new InvalidOperationException("请先真实投出道具，或加载已有道具");
            if(shot.Map!=Server.MapName||shot.SteamId!=p.SteamID)throw new InvalidOperationException("道具地图或归属不匹配");
            _store.SaveLineup(new(p.SteamID,name,shot),Config.MaxLineupsPerMap);Say(p,$"已保存私人道具 {name} [{shot.Kind}]，包含完整投掷状态");
        }
        else if(command=="loadnade")
        {
            var saved=_store.GetLineup(p.SteamID,Server.MapName,name);var before=Standing(p);
            Teleport(p,saved.Throw.Standing);s.Back.Remember(before);s.SelectedLineup=saved.Throw;
            Say(p,$"已选中 {saved.Name} 并返回站位；!rethrow 重投，!back 返回。亲手投新道具会取消选中。");
        }
        else{_store.DeleteLineup(p.SteamID,Server.MapName,name);Say(p,"已删除私人道具 "+name);}
    }
}
