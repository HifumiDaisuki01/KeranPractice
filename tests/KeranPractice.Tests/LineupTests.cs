using System.Text.Json;
using KeranPractice.Core;
using Xunit;
namespace KeranPractice.Tests;
public sealed class LineupTests:IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"kp-lineups-"+Guid.NewGuid().ToString("N"));
    private string FileName=>Path.Combine(_dir,"archive.json");
    private static ThrowSnapshot Shot(ulong id=1,string map="de_dust2")=>new("smoke",map,id,2,45,new(10,20,30),new(2,90,0),new(100,200,300),new(400,0,0),new(map,new(1,2,3),new(4,5,6)),123,999){ThrowStrength=.5f,PlayerVelocity=new(100,0,0),OnGround=false,Crouched=true};
    [Fact]public void CompletePhysicsSurvivesRestart(){var db=new PrivateStore(FileName);var shot=Shot();db.SaveLineup(new(1,"A",shot),64);Assert.Equal(shot,new PrivateStore(FileName).GetLineup(1,"de_dust2","a").Throw);}
    [Fact]public void PlayersCannotReadOrDeleteOthers(){var db=new PrivateStore(FileName);db.SaveLineup(new(1,"A",Shot()),64);Assert.Empty(db.Lineups(2,"de_dust2"));Assert.Throws<InvalidOperationException>(()=>db.DeleteLineup(2,"de_dust2","A"));Assert.Single(db.Lineups(1,"de_dust2"));}
    [Fact]public void MapsAreIsolated(){var db=new PrivateStore(FileName);db.SaveLineup(new(1,"A",Shot()),64);db.SaveLineup(new(1,"A",Shot(map:"de_mirage")),64);db.DeleteLineup(1,"de_mirage","A");Assert.Single(db.Lineups(1,"de_dust2"));}
    [Fact]public void OverwriteAtLimitPreservesPositions(){var db=new PrivateStore(FileName);db.Save(new(1,"pos",Shot().Standing),64);db.SaveLineup(new(1,"A",Shot()),1);db.SaveLineup(new(1,"a",Shot() with{Velocity=new(1,1,1)}),1);Assert.Single(db.Lineups(1,"de_dust2"));Assert.Equal(new V3(1,1,1),db.GetLineup(1,"de_dust2","A").Throw.Velocity);Assert.Throws<InvalidOperationException>(()=>db.SaveLineup(new(1,"B",Shot()),1));Assert.Single(db.Positions(1,"de_dust2"));}
    [Fact]public void OwnershipMismatchRejected(){var db=new PrivateStore(FileName);Assert.Throws<InvalidDataException>(()=>db.SaveLineup(new(2,"A",Shot()),64));Assert.False(File.Exists(FileName));}
    [Fact]public void NonfinitePhysicsRejected(){var db=new PrivateStore(FileName);Assert.Throws<InvalidDataException>(()=>db.SaveLineup(new(1,"A",Shot() with{Velocity=new(float.NaN,0,0)}),64));}
    [Fact]public void OldV2ArchiveNeedsNoDestructiveMigration(){Directory.CreateDirectory(_dir);File.WriteAllText(FileName,"{\"Version\":2,\"Positions\":[]}");var db=new PrivateStore(FileName);Assert.Empty(db.Lineups(1,"de_dust2"));db.SaveLineup(new(1,"A",Shot()),64);Assert.Single(new PrivateStore(FileName).Lineups(1,"de_dust2"));}
    [Fact]public void InvalidPersistedLineupPreservesOriginal(){Directory.CreateDirectory(_dir);var json=JsonSerializer.Serialize(new Archive{Lineups=[new(2,"A",Shot())]});File.WriteAllText(FileName,json);Assert.Throws<InvalidDataException>(()=>new PrivateStore(FileName));Assert.Equal(json,File.ReadAllText(FileName));}
    [Fact]public void DeleteKeepsOtherPlayerSameName(){var db=new PrivateStore(FileName);db.SaveLineup(new(1,"A",Shot()),64);db.SaveLineup(new(2,"A",Shot(2)),64);db.DeleteLineup(1,"de_dust2","a");Assert.Single(new PrivateStore(FileName).Lineups(2,"de_dust2"));}
    public void Dispose(){if(Directory.Exists(_dir))Directory.Delete(_dir,true);}
}
