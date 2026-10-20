namespace KeranPractice.Core;
public static class SupplyRules
{
    public static bool Blocked(bool? defense,bool? lease,string? training,bool? versus,bool? changing,bool automatic)=>
        defense is not false||lease is not false||changing is not false||training is not ("Practice" or "Deathmatch")||versus==null||
        (automatic&&(training=="Practice"||versus==true));
    public static string Weapon(string kind,int team)=>kind switch
    {
        "smoke"=>"weapon_smokegrenade","flash"=>"weapon_flashbang","he"=>"weapon_hegrenade",
        "decoy"=>"weapon_decoy","molotov"=>team==3?"weapon_incgrenade":"weapon_molotov",
        _=>throw new ArgumentException("Unknown grenade kind")
    };
}
