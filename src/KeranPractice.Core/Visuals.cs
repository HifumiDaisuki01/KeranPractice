namespace KeranPractice.Core;
public sealed record VisualMark(uint Raw,int Index,Owner Owner,string Kind,double ExpiresAt);
public sealed class VisualRegistry(int perOwner,int global)
{
    private readonly Dictionary<uint,VisualMark> _marks=[];
    public IReadOnlyCollection<VisualMark> Marks=>_marks.Values;
    public IReadOnlyList<VisualMark> Add(VisualMark mark)
    {
        if(mark.Owner.SteamId==0||mark.Index<0||!double.IsFinite(mark.ExpiresAt))throw new ArgumentException("Invalid visual ownership or expiry");
        var evicted=new List<VisualMark>();
        _marks.Remove(mark.Raw);
        while(_marks.Values.Count(m=>m.Owner==mark.Owner)>=Math.Clamp(perOwner,3,256))
        {var oldest=_marks.Values.Where(m=>m.Owner==mark.Owner).MinBy(m=>m.ExpiresAt)!;_marks.Remove(oldest.Raw);evicted.Add(oldest);}
        while(_marks.Count>=Math.Clamp(global,3,512))
        {var oldest=_marks.Values.MinBy(m=>m.ExpiresAt)!;_marks.Remove(oldest.Raw);evicted.Add(oldest);}
        _marks[mark.Raw]=mark;return evicted;
    }
    public bool Remove(uint raw)=>_marks.Remove(raw);
    public IReadOnlyList<VisualMark> Take(Func<VisualMark,bool> predicate)
    {
        var selected=_marks.Values.Where(predicate).ToArray();foreach(var m in selected)_marks.Remove(m.Raw);return selected;
    }
    public static bool Visible(VisualMark mark,Owner? recipient)=>recipient.HasValue&&recipient.Value==mark.Owner;
}
public static class VisualGeometry
{
    public static IReadOnlyList<(V3 Start,V3 End)> Cross(V3 center,float radius)
    {
        if(!center.Finite||!float.IsFinite(radius)||radius<=0||radius>16)throw new ArgumentException("Invalid marker geometry");
        return [(center with{X=center.X-radius},center with{X=center.X+radius}),
                (center with{Y=center.Y-radius},center with{Y=center.Y+radius}),
                (center with{Z=center.Z-radius},center with{Z=center.Z+radius})];
    }
    public static bool Segment(V3 a,V3 b)=>a.Finite&&b.Finite&&a.Length<=100000&&b.Length<=100000&&a.Distance(b) is >=1 and <4096;
}
