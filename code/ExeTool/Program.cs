using System.Globalization;
using ExeTool;
using Iced.Intel;

if (args.Length < 2)
{
    Console.WriteLine("""
        ExeTool <exe> <command> [args]   (addresses are RVAs, hex)

          info                      sections and image base
          dis <rva> [count]         disassemble count instructions (default 40)
          func <rva>                disassemble to the first ret/jmp-out after a run of int3
          xref <rva> [rva...]       instructions that reference an address (branch, RIP-relative,
                                    image-base-relative displacement, or absolute pointer in data)
          liveregs <rva> [rva...]   volatile registers each direct caller keeps across a call (for hooks)
          switches [min_cases]      switch jump tables with at least min_cases cases (default 64)
          ptrtables [min_len]       runs of pointers into .text in data sections (default 64)
          find <hex bytes, ?? ok>   byte pattern; prints each match's RVA and section
          u8|u16|u32 <rva> [count]  dump values
        """);
    return 1;
}

var image = PeImage.Load(args[0]);
var cmd = args[1];
var rest = args.Skip(2).ToArray();
ulong Hex(string s) => ulong.Parse(s.Replace("0x", "").Replace("0X", ""), NumberStyles.HexNumber);

switch (cmd)
{
    case "info":
        Console.WriteLine($"image base 0x{image.ImageBase:X}, size 0x{image.Bytes.Length:X}");
        foreach (var s in image.Sections)
            Console.WriteLine($"  {s.Name,-8} rva 0x{s.Rva:X8} size 0x{s.Size:X8} file 0x{s.FileOffset:X8} {(s.Executable ? "code" : "")}");
        break;

    case "dis":
        foreach (var line in Disasm.Lines(image, (uint)Hex(rest[0]), rest.Length > 1 ? int.Parse(rest[1]) : 40))
            Console.WriteLine(line);
        break;

    case "func":
        foreach (var line in Disasm.Function(image, (uint)Hex(rest[0])))
            Console.WriteLine(line);
        break;

    case "xref":
        {
            var targets = rest.Select(r => (uint)Hex(r)).ToHashSet();
            foreach (var (rva, kind, text) in Analysis.XRefs(image, targets))
                Console.WriteLine($"0x{rva:X8}  {kind,-8} {text}   [{image.FunctionStart(rva):X8}]");
            break;
        }

    case "liveregs":
        foreach (var rva in rest.Select(r => (uint)Hex(r)))
        {
            Console.WriteLine($"0x{rva:X8}:");
            foreach (var line in LiveRegs.Check(image, rva))
                Console.WriteLine(line);
        }
        break;

    case "switches":
        foreach (var sw in Analysis.Switches(image, rest.Length > 0 ? int.Parse(rest[0]) : 64))
            Console.WriteLine(sw);
        break;

    case "ptrtables":
        foreach (var t in Analysis.PointerTables(image, rest.Length > 0 ? int.Parse(rest[0]) : 64))
            Console.WriteLine(t);
        break;

    case "find":
        {
            var pattern = rest.Select(b => b == "??" ? -1 : Convert.ToInt32(b, 16)).ToArray();
            foreach (var rva in image.Find(pattern))
                Console.WriteLine($"0x{rva:X8} {image.SectionOf(rva)?.Name}");
            break;
        }

    case "u8" or "u16" or "u32":
        {
            var rva = (uint)Hex(rest[0]);
            var count = rest.Length > 1 ? int.Parse(rest[1]) : 16;
            var size = cmd == "u8" ? 1 : cmd == "u16" ? 2 : 4;
            for (var i = 0; i < count; i++)
            {
                var at = rva + (uint)(i * size);
                ulong v = size switch { 1 => image.Bytes[at], 2 => BitConverter.ToUInt16(image.Bytes, (int)at), _ => BitConverter.ToUInt32(image.Bytes, (int)at) };
                Console.Write($"{v.ToString(size == 1 ? "X2" : size == 2 ? "X4" : "X8")}{((i + 1) % 16 == 0 ? "\n" : " ")}");
            }
            Console.WriteLine();
            break;
        }

    default:
        Console.Error.WriteLine($"unknown command {cmd}");
        return 1;
}
return 0;
