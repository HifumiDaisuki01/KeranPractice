using System.Buffers.Binary;
using KeranPractice.Core;
using Xunit;
namespace KeranPractice.Tests;
public sealed class NativeAuditTests
{
    private static (byte[] File,string Signature,string Guard) Fixture()
    {
        var b=Enumerable.Repeat((byte)0xcc,1200).ToArray();Array.Clear(b,0,128);new byte[]{127,69,76,70,2,1}.CopyTo(b,0);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(16),3);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(18),62);BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),64);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(54),56);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(56),1);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(64),1);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(68),5);BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(72),128);BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(80),4096);BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(96),1072);
        Array.Fill(b,(byte)0x90,256,256);new byte[]{0x55,0x48,0x89,0xe5,0x41,0x57,0x41,0x56}.CopyTo(b,256);
        foreach(int o in new[]{32,64}){b[256+o]=0xe8;BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(256+o+1),20);}
        var g=b.AsSpan(256,256).ToArray().Select(v=>v.ToString("X2")).ToArray();foreach(int o in new[]{32,64})for(int n=1;n<=4;n++)g[o+n]="?";
        return(b,"55 48 89 E5 41 57 41 56",string.Join(' ',g));
    }
    [Fact]public void RecognizedCachedCssDetourCanSurviveHotReload(){byte[] d=Enumerable.Repeat((byte)0xcc,192).ToArray();byte[] l=(byte[])d.Clone();l[0]=0xe9;for(int n=1;n<5;n++)l[n]=0;l[5]=l[6]=0x90;Assert.Equal(7,NativeAudit.CssDetourPrefix(d,l,[1,4,7,9],true));Assert.Equal(-1,NativeAudit.CssDetourPrefix(d,l,[1,4,7,9],false));l[40]^=1;Assert.Equal(-1,NativeAudit.CssDetourPrefix(d,l,[1,4,7,9],true));}
    [Fact]public void UnrelatedUpdateDoesNotDisableFunction(){var(b,s,g)=Fixture();b[1100]^=1;Assert.Equal(256,NativeAudit.Verify(b,s,g,[32,64]).Offset);}
    [Fact]public void ShiftedFunctionIsAcceptedAfterRevalidation(){var(b,s,g)=Fixture();b.AsSpan(256,256).CopyTo(b.AsSpan(600));Array.Fill(b,(byte)0xcc,256,256);Assert.Equal(600,NativeAudit.Verify(b,s,g,[32,64]).Offset);}
    [Fact]public void RelativeCallRelocationAcceptedWithinExecutableCode(){var(b,s,g)=Fixture();BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(289),80);Assert.Equal(256,NativeAudit.Verify(b,s,g,[32,64]).Offset);}
    [Fact]public void ArgumentCodeChangeFailsClosed(){var(b,s,g)=Fixture();b[270]=0x49;Assert.Throws<InvalidDataException>(()=>NativeAudit.Verify(b,s,g,[32,64]));}
    [Fact]public void DuplicateSignatureFailsClosed(){var(b,s,g)=Fixture();b.AsSpan(256,256).CopyTo(b.AsSpan(600));Assert.Throws<InvalidDataException>(()=>NativeAudit.Verify(b,s,g,[32,64]));}
    [Fact]public void NonExecutableSegmentIsRejected(){var(b,s,g)=Fixture();BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(68),4);Assert.Throws<InvalidDataException>(()=>NativeAudit.Verify(b,s,g,[32,64]));}
    [Fact]public void CallOutsideCodeIsRejected(){var(b,s,g)=Fixture();BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(289),int.MaxValue);Assert.Throws<InvalidDataException>(()=>NativeAudit.Verify(b,s,g,[32,64]));}
    [Fact]public void CorruptElfBoundsAreRejected(){var(b,s,g)=Fixture();BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),ulong.MaxValue);Assert.Throws<InvalidDataException>(()=>NativeAudit.Verify(b,s,g,[32,64]));}
    [Fact]public void MissingAbiGuardDoesNotBypassAudit(){var(b,s,_)=Fixture();Assert.Throws<InvalidDataException>(()=>NativeAudit.Verify(b,s,"",[32,64]));}
    [Fact]public void OneBadFunctionDoesNotInvalidateAnother(){var(b,s,g)=Fixture();Assert.Throws<InvalidDataException>(()=>NativeAudit.Verify(b,"55 48 89 E5 41 57 41 55",g,[32,64]));Assert.Equal(256,NativeAudit.Verify(b,s,g,[32,64]).Offset);}
}
