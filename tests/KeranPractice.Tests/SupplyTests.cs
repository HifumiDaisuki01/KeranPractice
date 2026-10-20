using KeranPractice.Core;
using Xunit;
namespace KeranPractice.Tests;
public class SupplyTests
{
    [Theory][InlineData(2,"weapon_molotov")][InlineData(3,"weapon_incgrenade")]
    public void FireGrenadeFollowsCurrentTeam(int team,string name)=>Assert.Equal(name,SupplyRules.Weapon("molotov",team));
    [Theory][InlineData("smoke")][InlineData("flash")][InlineData("he")][InlineData("decoy")]
    public void OnlyGrenadesAreSupplied(string kind){Assert.StartsWith("weapon_",SupplyRules.Weapon(kind,2));Assert.Equal(SupplyRules.Weapon(kind,2),SupplyRules.Weapon(kind,3));}
    [Fact]public void OrdinaryPracticeAutoSupplies()=>Assert.False(SupplyRules.Blocked(false,false,"Deathmatch",false,false,true));
    [Theory][InlineData(false)][InlineData(true)]public void KsAndKsvManualOnly(bool versus){Assert.True(SupplyRules.Blocked(false,false,"Practice",versus,false,true));Assert.False(SupplyRules.Blocked(false,false,"Practice",versus,false,false));}
    [Theory][InlineData(true)][InlineData(false)]public void DefenseNeverSupplies(bool automatic)=>Assert.True(SupplyRules.Blocked(true,true,"Practice",false,false,automatic));
    [Fact]public void UnknownKsStateFailsClosed()=>Assert.True(SupplyRules.Blocked(null,false,null,null,null,false));
    [Fact]public void GunsCannotBeSupplied()=>Assert.Throws<ArgumentException>(()=>SupplyRules.Weapon("ak47",2));
}
