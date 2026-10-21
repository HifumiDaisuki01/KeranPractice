using KeranPractice.Core;
using Xunit;
namespace KeranPractice.Tests;
public sealed class IsolationTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"kpv2-"+Guid.NewGuid().ToString("N"));
    private string FileName=>Path.Combine(_dir,"archive.json");
    private static readonly Standing A=new("de_dust2",new(1,2,3),new(10,20,0));
    private static ThrowSnapshot Shot(ulong id=1)=>new("smoke","de_dust2",id,2,45,new(1,2,3),new(0,0,0),new(100,2,50),new(300,0,0),A,1,123);
    [Fact] public void ReplayDoesNotOverwriteOriginal(){var h=new History(2);var a=Shot();h.Capture(a,false);h.Capture(a with{Velocity=new(1,0,0)},true);Assert.Same(a,h.Last);Assert.Single(h.Entries);}
    [Fact] public void HistoryIsBounded(){var h=new History(2);for(int i=0;i<10;i++)h.Capture(Shot() with{Tick=i},false);Assert.Equal(2,h.Entries.Count);Assert.Equal(9,h.Last!.Tick);}
    [Fact] public void HistoriesAreIndependent(){var a=new History(10);var b=new History(10);a.Capture(Shot(),false);Assert.Null(b.Last);}
    [Theory][InlineData("../escape")][InlineData("x;y")][InlineData("x\ny")][InlineData("a\\b")][InlineData("")]
    public void NamesRejectInjection(string name)=>Assert.Throws<ArgumentException>(()=>Inputs.Name(name));
    [Fact] public void PrivateSameNamesAreIsolated(){var db=new PrivateStore(FileName);db.Save(new(1,"a",A),4);db.Save(new(2,"a",A with{Origin=new(8,9,10)}),4);db.Delete(1,"de_dust2","a");Assert.Single(db.Positions(2,"de_dust2"));Assert.Throws<InvalidOperationException>(()=>db.Get(1,"de_dust2","a"));}
    [Fact] public void RestartPreservesPositions(){var db=new PrivateStore(FileName);db.Save(new(1,"a",A),4);Assert.Equal(A,new PrivateStore(FileName).Get(1,"de_dust2","a").Position);}
    [Fact] public void MapIsolation(){var db=new PrivateStore(FileName);db.Save(new(1,"a",A),4);Assert.Empty(db.Positions(1,"de_inferno"));}
    [Fact] public void OverwriteAllowedAtLimit(){var db=new PrivateStore(FileName);db.Save(new(1,"a",A),1);db.Save(new(1,"A",A with{Origin=new(8,0,0)}),1);Assert.Equal(8,db.Get(1,"de_dust2","a").Position.Origin.X);Assert.Throws<InvalidOperationException>(()=>db.Save(new(1,"b",A),1));}
    [Fact] public void CorruptDataIsNotOverwritten(){Directory.CreateDirectory(_dir);File.WriteAllText(FileName,"broken");Assert.Throws<System.Text.Json.JsonException>(()=>new PrivateStore(FileName));Assert.Equal("broken",File.ReadAllText(FileName));}
    [Fact] public void LegacyPositionsImportedOnceWithoutDestroyingSource(){Directory.CreateDirectory(_dir);var legacy=Path.Combine(_dir,"v1.json");File.WriteAllText(legacy,"{\"version\":1,\"positions\":[{\"steamId\":1,\"name\":\"a\",\"map\":\"de_dust2\",\"x\":1,\"y\":2,\"z\":3,\"pitch\":10,\"yaw\":20}]}");var db=new PrivateStore(FileName);Assert.Equal(1,db.ImportLegacy(legacy));Assert.Equal(0,db.ImportLegacy(legacy));Assert.Equal(A,db.Get(1,"de_dust2","a").Position);Assert.True(File.Exists(legacy));}
    [Fact] public void InvalidSnapshotRejected()=>Assert.Throws<InvalidDataException>(()=>Shot() with{Velocity=new(float.NaN,0,0)} is var s ? Validate(s):null);
    private static object? Validate(ThrowSnapshot shot){shot.Validate();return null;}
    [Fact] public void ReconnectAndMapEpochProtectOwnership(){var l=new OwnershipLedger();l.Add(new(123,new Owner(1,1,1),1,"smoke"));Assert.Empty(l.Select(new(1,2,1)));Assert.Empty(l.Select(new(1,1,2)));Assert.Empty(l.Select(new(2,1,1)));Assert.Single(l.Select(new(1,1,1)));}
    [Fact] public void AutoClearDoesNotSelectNewestBatch(){var l=new OwnershipLedger();var o=new Owner(1,1,1);l.Add(new(1,o,1,"smoke"));l.Add(new(2,o,2,"molotov"));Assert.Equal(1u,Assert.Single(l.Select(o,1)).Handle);}
    [Fact] public void SerialReusedIndexIsNotSameEntity(){var l=new OwnershipLedger();l.Add(new(0x10001,new(1,1,1),1,"smoke"));Assert.False(l.TryGet(0x20001,out _));}
    [Theory][InlineData("god")][InlineData("noclip")][InlineData("clearall")][InlineData("bot")]
    public void PublicConfigurationCannotGrantHighRiskCommands(string command){var p=new AccessPolicy{TrainingEnvironmentApproved=true,Public=[command]};Assert.False(p.Allows(2,command,true));}
    [Fact] public void PracticeSwitchCannotAuthorizeEnvironment(){var p=new AccessPolicy{Public=["rethrow"]};Assert.False(p.Allows(2,"rethrow",true));}
    [Fact] public void PublicRequiresApprovedEnvironmentAndSession(){var p=new AccessPolicy{TrainingEnvironmentApproved=true};Assert.False(p.Allows(2,"rethrow",false));Assert.True(p.Allows(2,"rethrow",true));}
    [Fact] public void BackActuallyToggles(){var b=new TeleportToggle();var other=A with{Origin=new(3,4,5)};b.Remember(A);Assert.Equal(A,b.Swap(other));Assert.Equal(other,b.Swap(A));}
    [Fact] public void BackRejectsCrossMap(){var b=new TeleportToggle();b.Remember(A);Assert.Throws<InvalidOperationException>(()=>b.Swap(A with{Map="de_inferno"}));}
    public void Dispose(){if(Directory.Exists(_dir))Directory.Delete(_dir,true);}
}
