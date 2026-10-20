using KeranPractice.Core;
using Xunit;
namespace KeranPractice.Tests;
public sealed class VisualTests
{
    private static readonly Owner A=new(1,1,1),B=new(2,1,1);
    private static VisualMark Mark(uint raw,Owner owner,double expiry=10,string kind="trajectory")=>new(raw,(int)(raw&0x7fff),owner,kind,expiry);
    [Fact]public void OnlyExactConnectionAndMapCanSee(){var m=Mark(1,A);Assert.True(VisualRegistry.Visible(m,A));Assert.False(VisualRegistry.Visible(m,B));Assert.False(VisualRegistry.Visible(m,new Owner(1,2,1)));Assert.False(VisualRegistry.Visible(m,new Owner(1,1,2)));Assert.False(VisualRegistry.Visible(m,null));}
    [Fact]public void PerPlayerLimitEvictsOwnOldestOnly(){var db=new VisualRegistry(3,10);db.Add(Mark(1,A,1));db.Add(Mark(2,A,2));db.Add(Mark(3,A,3));db.Add(Mark(4,B,0));var evicted=db.Add(Mark(5,A,4));Assert.Equal(1u,Assert.Single(evicted).Raw);Assert.Equal(4,db.Marks.Count);Assert.Contains(db.Marks,m=>m.Owner==B);}
    [Fact]public void GlobalLimitIsHardBound(){var db=new VisualRegistry(10,3);for(uint n=1;n<=100;n++)db.Add(Mark(n,n%2==0?A:B,n));Assert.Equal(3,db.Marks.Count);Assert.All(db.Marks,m=>Assert.True(m.Raw>=98));}
    [Fact]public void ToggleOffClearsOnlyOneCategoryAndPlayer(){var db=new VisualRegistry(10,10);db.Add(Mark(1,A,10,"impact"));db.Add(Mark(2,A,10));db.Add(Mark(3,B,10,"impact"));Assert.Single(db.Take(m=>m.Owner==A&&m.Kind=="impact"));Assert.Equal(2,db.Marks.Count);Assert.Contains(db.Marks,m=>m.Owner==B);}
    [Fact]public void ExpiryKeepsLiveMarkers(){var db=new VisualRegistry(10,10);db.Add(Mark(1,A,1));db.Add(Mark(2,A,5));Assert.Single(db.Take(m=>m.ExpiresAt<=2));Assert.Equal(2u,Assert.Single(db.Marks).Raw);}
    [Fact]public void StaleSerialCannotDeleteNewEntity(){var db=new VisualRegistry(10,10);db.Add(Mark(0x10001,A));Assert.False(db.Remove(0x20001));Assert.Single(db.Marks);Assert.True(db.Remove(0x10001));Assert.Empty(db.Marks);}
    [Fact]public void DisconnectCanRemoveAllOwnMarkers(){var db=new VisualRegistry(10,10);db.Add(Mark(1,A));db.Add(Mark(2,B));db.Take(m=>m.Owner==A);Assert.Equal(B,Assert.Single(db.Marks).Owner);}
    [Fact]public void InvalidVisualMetadataRejected(){var db=new VisualRegistry(10,10);Assert.Throws<ArgumentException>(()=>db.Add(Mark(1,new Owner(0,0,0))));Assert.Throws<ArgumentException>(()=>db.Add(Mark(1,A,double.NaN)));}
    [Fact]public void CrossHasThreeCenteredAxes(){var c=new V3(10,20,30);var lines=VisualGeometry.Cross(c,2);Assert.Equal(3,lines.Count);foreach(var(a,b)in lines){Assert.Equal(4,a.Distance(b));Assert.Equal(c,new V3((a.X+b.X)/2,(a.Y+b.Y)/2,(a.Z+b.Z)/2));}}
    [Fact]public void InvalidGeometryIsRejected(){Assert.False(VisualGeometry.Segment(new(float.NaN,0,0),new(0,0,0)));Assert.False(VisualGeometry.Segment(new(0,0,0),new(0,0,0)));Assert.False(VisualGeometry.Segment(new(0,0,0),new(5000,0,0)));Assert.Throws<ArgumentException>(()=>VisualGeometry.Cross(new(1,2,3),float.NaN));}
    [Fact]public void WholeMapCleanupReturnsOnlyRegisteredMarks(){var db=new VisualRegistry(10,10);db.Add(Mark(1,A));db.Add(Mark(2,B));Assert.Equal(2,db.Take(_=>true).Count);Assert.Empty(db.Marks);}
}
