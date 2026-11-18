namespace ExeTool;

/// <summary>A PE file laid out as it is in memory (sections at their RVAs).</summary>
internal sealed class PeImage
{
    public sealed record Section(string Name, uint Rva, uint Size, uint FileOffset, bool Executable)
    {
        public bool Contains(uint rva) => rva >= Rva && rva < Rva + Size;
    }

    public ulong ImageBase { get; private init; }
    public byte[] Bytes { get; private init; } = [];
    public List<Section> Sections { get; } = [];
    /// <summary>Function starts from the exception directory (.pdata), sorted.</summary>
    public uint[] Functions { get; private set; } = [];

    public static PeImage Load(string path)
    {
        var file = File.ReadAllBytes(path);
        var nt = BitConverter.ToInt32(file, 0x3C);
        var count = BitConverter.ToUInt16(file, nt + 6);
        var optional = nt + 24;
        var imageBase = BitConverter.ToUInt64(file, optional + 24);
        var sizeOfImage = BitConverter.ToUInt32(file, optional + 56);
        var image = new PeImage { ImageBase = imageBase, Bytes = new byte[sizeOfImage] };
        Array.Copy(file, image.Bytes, BitConverter.ToUInt32(file, optional + 60)); // headers
        var table = optional + BitConverter.ToUInt16(file, nt + 20);
        for (var i = 0; i < count; i++)
        {
            var s = table + 40 * i;
            var name = System.Text.Encoding.ASCII.GetString(file, s, 8).TrimEnd('\0');
            var vsize = BitConverter.ToUInt32(file, s + 8);
            var rva = BitConverter.ToUInt32(file, s + 12);
            var rawSize = BitConverter.ToUInt32(file, s + 16);
            var raw = BitConverter.ToUInt32(file, s + 20);
            var flags = BitConverter.ToUInt32(file, s + 36);
            Array.Copy(file, raw, image.Bytes, rva, Math.Min(rawSize, vsize));
            image.Sections.Add(new Section(name, rva, vsize, raw, (flags & 0x20000000) != 0));
        }
        // Exception directory (data directory 3): RUNTIME_FUNCTION { begin, end, unwind } entries.
        var pdataRva = BitConverter.ToUInt32(file, optional + 112 + 3 * 8);
        var pdataSize = BitConverter.ToUInt32(file, optional + 112 + 3 * 8 + 4);
        var starts = new List<uint>();
        for (var at = pdataRva; at + 12 <= pdataRva + pdataSize; at += 12)
            starts.Add(BitConverter.ToUInt32(image.Bytes, (int)at));
        starts.Sort();
        image.Functions = starts.ToArray();
        return image;
    }

    public Section? SectionOf(uint rva) => Sections.FirstOrDefault(s => s.Contains(rva));

    public IEnumerable<Section> Code => Sections.Where(s => s.Executable);

    /// <summary>Start of the function containing rva, from .pdata (0 if unknown).</summary>
    public uint FunctionStart(uint rva)
    {
        var i = Array.BinarySearch(Functions, rva);
        if (i >= 0) return Functions[i];
        i = ~i - 1;
        return i >= 0 ? Functions[i] : 0;
    }

    public IEnumerable<uint> Find(int[] pattern)
    {
        foreach (var s in Sections)
        {
            var end = s.Rva + s.Size - (uint)pattern.Length;
            for (var at = s.Rva; at <= end; at++)
            {
                var ok = true;
                for (var k = 0; k < pattern.Length && ok; k++)
                    ok = pattern[k] < 0 || Bytes[at + k] == pattern[k];
                if (ok) yield return at;
            }
        }
    }
}
