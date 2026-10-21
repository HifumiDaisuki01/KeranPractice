using System.Buffers.Binary;
namespace KeranPractice.Core;
public sealed record ExecutableSegment(long Offset,long Address,long Length);
public sealed record AuditedFunction(long Offset,long Address,int GuardLength);
public static class NativeAudit
{
    public static int CssDetourPrefix(ReadOnlySpan<byte> disk,ReadOnlySpan<byte> live,IReadOnlyList<int> boundaries,bool cached)
    {
        if(disk.SequenceEqual(live))return 0;
        if(!cached||live.Length!=disk.Length||live.Length<16||live[0]!=0xe9)return -1;
        foreach(int n in boundaries.Where(n=>n>=5&&n<=16))
        {
            bool nops=true;for(int j=5;j<n;j++)if(live[j]!=0x90)nops=false;
            if(nops&&live[n..].SequenceEqual(disk[n..]))return n;
        }
        return -1;
    }
    public static int[] Pattern(string pattern)=>pattern.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(s=>s is "?" or "??"?-1:Convert.ToInt32(s,16)).ToArray();
    public static bool Match(ReadOnlySpan<byte> bytes,int[] pattern)
    {
        if(bytes.Length<pattern.Length)return false;
        for(int i=0;i<pattern.Length;i++)if(pattern[i]>=0&&bytes[i]!=pattern[i])return false;
        return true;
    }
    public static IReadOnlyList<ExecutableSegment> ExecutableSegments(byte[] file)
    {
        if(file.Length<64||!file.AsSpan(0,4).SequenceEqual(new byte[]{127,69,76,70})||file[4]!=2||file[5]!=1||BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(18))!=62||BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(16))!=3)throw new InvalidDataException("Expected Linux x86-64 little-endian shared ELF");
        ulong table=BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(32));int entry=BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(54)),count=BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(56));
        if(entry<56||count<1||table>(ulong)file.Length||(ulong)entry*(ulong)count>(ulong)file.Length-table)throw new InvalidDataException("Invalid ELF program headers");
        var result=new List<ExecutableSegment>();
        for(int n=0;n<count;n++)
        {
            int at=checked((int)table+n*entry);var p=file.AsSpan(at,56);
            if(BinaryPrimitives.ReadUInt32LittleEndian(p)!=1||(BinaryPrimitives.ReadUInt32LittleEndian(p[4..])&1)==0)continue;
            ulong offset=BinaryPrimitives.ReadUInt64LittleEndian(p[8..]),address=BinaryPrimitives.ReadUInt64LittleEndian(p[16..]),length=BinaryPrimitives.ReadUInt64LittleEndian(p[32..]);
            if(offset>(ulong)file.Length||length>(ulong)file.Length-offset||address>long.MaxValue||length==0)throw new InvalidDataException("Invalid executable ELF bounds");
            result.Add(new((long)offset,(long)address,(long)length));
        }
        if(result.Count==0)throw new InvalidDataException("ELF has no executable load segment");return result;
    }
    public static AuditedFunction Verify(byte[] file,string signature,string abiMask,IReadOnlyList<int> relativeCalls)
    {
        var segments=ExecutableSegments(file);var pattern=Pattern(signature);var guard=Pattern(abiMask);
        if(pattern.Length<8||pattern[0]<0||guard.Length is <192 or >1024||guard.Count(b=>b>=0)<128||relativeCalls.Count<2)throw new InvalidDataException("Incomplete per-function ABI audit");
        var prefix=pattern.TakeWhile(b=>b>=0).Select(b=>(byte)b).ToArray();int cursor=0,match=-1;
        while(cursor<=file.Length-pattern.Length)
        {
            int next=file.AsSpan(cursor).IndexOf(prefix);if(next<0)break;int at=cursor+next;
            if(Match(file.AsSpan(at),pattern)){if(match>=0)throw new InvalidDataException("Signature is not unique");match=at;}cursor=at+1;
        }
        if(match<0)throw new InvalidDataException("Signature not found");
        var segment=segments.FirstOrDefault(s=>match>=s.Offset&&match+guard.Length<=s.Offset+s.Length)??throw new InvalidDataException("Signature/ABI guard outside executable segment");
        if(!Match(file.AsSpan(match),guard))throw new InvalidDataException("Function ABI/code guard changed; re-audit required");
        long address=segment.Address+match-segment.Offset;
        foreach(int call in relativeCalls)
        {
            if(call<0||call+5>guard.Length||file[match+call]!=0xe8)throw new InvalidDataException("Audited direct call instruction changed");
            long target=address+call+5+BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(match+call+1));
            if(!segments.Any(s=>target>=s.Address&&target<s.Address+s.Length))throw new InvalidDataException("Audited call target outside executable segments");
        }
        return new(match,address,guard.Length);
    }
}
