using Iced.Intel;

namespace ExeTool;

internal static class Disasm
{
    private static readonly Formatter Format = new IntelFormatter(new FormatterOptions
    {
        HexPrefix = "0x", HexSuffix = "", UppercaseHex = true, FirstOperandCharIndex = 8,
        RipRelativeAddresses = false, ShowBranchSize = false,
    });

    /// <summary>Decode instructions starting at rva. IPs are RVAs.</summary>
    public static IEnumerable<Instruction> Decode(PeImage image, uint rva, uint length)
    {
        length = Math.Min(length, (uint)image.Bytes.Length - rva);
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(image.Bytes, (int)rva, (int)length), rva);
        while (decoder.IP < rva + length)
        {
            decoder.Decode(out var instr);
            yield return instr;
        }
    }

    public static string Text(Instruction instr)
    {
        var output = new StringOutput();
        Format.Format(instr, output);
        return output.ToStringAndReset();
    }

    public static string Line(PeImage image, Instruction instr)
    {
        var bytes = string.Join(" ", image.Bytes.Skip((int)instr.IP).Take(instr.Length).Select(b => b.ToString("X2")));
        return $"{instr.IP:X8}  {bytes,-30} {Text(instr)}";
    }

    public static IEnumerable<string> Lines(PeImage image, uint rva, int count) =>
        Decode(image, rva, (uint)(count * 15)).Take(count).Select(i => Line(image, i));

    /// <summary>One function, using .pdata for its end when known.</summary>
    public static IEnumerable<string> Function(PeImage image, uint rva)
    {
        var start = image.FunctionStart(rva);
        var i = Array.BinarySearch(image.Functions, start);
        var end = i >= 0 && i + 1 < image.Functions.Length ? image.Functions[i + 1] : rva + 0x400;
        if (start == 0) (start, end) = (rva, rva + 0x400);
        foreach (var instr in Decode(image, start, end - start))
            yield return Line(image, instr);
    }
}
