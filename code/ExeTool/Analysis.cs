using Iced.Intel;

namespace ExeTool;

internal static class Analysis
{
    /// <summary>Every decoded instruction in the code sections (linear sweep).</summary>
    public static IEnumerable<Instruction> AllCode(PeImage image) =>
        image.Code.SelectMany(s => Disasm.Decode(image, s.Rva, s.Size));

    public static IEnumerable<(uint Rva, string Kind, string Text)> XRefs(PeImage image, HashSet<uint> targets)
    {
        foreach (var instr in AllCode(image))
        {
            string? kind = null;
            if (instr.FlowControl is FlowControl.Call or FlowControl.ConditionalBranch or FlowControl.UnconditionalBranch
                && instr.Op0Kind is OpKind.NearBranch64 && targets.Contains((uint)instr.NearBranch64))
                kind = instr.FlowControl == FlowControl.Call ? "call" : "jump";
            else if (instr.IsIPRelativeMemoryOperand && targets.Contains((uint)instr.IPRelativeMemoryAddress))
                kind = "rip";
            else if (instr.MemoryDisplacement64 != 0 && !instr.IsIPRelativeMemoryOperand
                     && instr.MemoryBase != Register.None && targets.Contains((uint)instr.MemoryDisplacement64))
                kind = "base+rva";
            else
            {
                for (var k = 0; k < instr.OpCount && kind is null; k++)
                    if (instr.GetOpKind(k) is OpKind.Immediate64 or OpKind.Immediate32 or OpKind.Immediate32to64
                        && targets.Contains((uint)(instr.GetImmediate(k) - image.ImageBase)))
                        kind = "imm-va";
            }
            if (kind is not null)
                yield return ((uint)instr.IP, kind, Disasm.Text(instr));
        }
        // Absolute pointers in data (vtables, function-pointer tables).
        foreach (var s in image.Sections.Where(s => !s.Executable))
            for (var at = s.Rva; at + 8 <= s.Rva + s.Size; at += 8)
            {
                var v = BitConverter.ToUInt64(image.Bytes, (int)at);
                if (v > image.ImageBase && targets.Contains((uint)(v - image.ImageBase)))
                    yield return (at, "data-ptr", $"{s.Name} pointer to 0x{v - image.ImageBase:X}");
            }
    }

    /// <summary>MSVC x64 switches: mov r32,[base+idx*4+RVA]; add r64,base; jmp r64, bounded by cmp/ja.</summary>
    public static IEnumerable<string> Switches(PeImage image, int minCases)
    {
        var window = new Queue<Instruction>();
        foreach (var instr in AllCode(image))
        {
            window.Enqueue(instr);
            if (window.Count > 14) window.Dequeue();
            if (instr.Mnemonic != Mnemonic.Jmp || instr.Op0Kind != OpKind.Register) continue;
            var list = window.ToArray();
            var load = list.LastOrDefault(i => i.Mnemonic == Mnemonic.Mov && i.Op1Kind == OpKind.Memory
                                               && i.MemoryIndexScale == 4 && i.MemoryDisplacement64 != 0
                                               && !i.IsIPRelativeMemoryOperand);
            if (load.Mnemonic == Mnemonic.INVALID) continue;
            var cmp = list.LastOrDefault(i => i.Mnemonic == Mnemonic.Cmp && i.Op1Kind is OpKind.Immediate8 or OpKind.Immediate32
                                                or OpKind.Immediate8to32 or OpKind.Immediate8to64 or OpKind.Immediate32to64);
            var cases = cmp.Mnemonic == Mnemonic.INVALID ? -1 : (int)cmp.GetImmediate(1) + 1;
            var byteIndex = list.LastOrDefault(i => i.Mnemonic == Mnemonic.Movzx && i.Op1Kind == OpKind.Memory
                                                    && i.MemoryIndexScale == 1 && i.MemoryDisplacement64 != 0);
            if (cases < minCases) continue;
            var table = (uint)load.MemoryDisplacement64;
            string targets;
            if (byteIndex.Mnemonic != Mnemonic.INVALID)
            {
                var idx = (uint)byteIndex.MemoryDisplacement64;
                var n = Enumerable.Range(0, cases).Select(k => image.Bytes[idx + k]).Max() + 1;
                targets = $"two-level: index 0x{idx:X} ({cases} cases) -> {n} targets at 0x{table:X}";
            }
            else
            {
                var distinct = Enumerable.Range(0, cases).Select(k => BitConverter.ToUInt32(image.Bytes, (int)(table + 4 * k))).Distinct().Count();
                targets = $"{cases} cases, {distinct} distinct targets, table 0x{table:X}";
            }
            yield return $"jmp at 0x{instr.IP:X8} in func 0x{image.FunctionStart((uint)instr.IP):X8}: {targets} (cmp at 0x{cmp.IP:X})";
        }
    }

    /// <summary>Runs of 8-byte pointers into code, in non-code sections.</summary>
    public static IEnumerable<string> PointerTables(PeImage image, int minLength)
    {
        var code = image.Code.ToArray();
        bool IsCode(ulong v) => v > image.ImageBase && code.Any(c => c.Contains((uint)(v - image.ImageBase)));
        foreach (var s in image.Sections.Where(s => !s.Executable))
        {
            uint runStart = 0;
            var run = 0;
            for (var at = s.Rva; at + 8 <= s.Rva + s.Size; at += 8)
            {
                if (IsCode(BitConverter.ToUInt64(image.Bytes, (int)at)))
                {
                    if (run++ == 0) runStart = at;
                    continue;
                }
                if (run >= minLength)
                {
                    var distinct = Enumerable.Range(0, run).Select(k => BitConverter.ToUInt64(image.Bytes, (int)(runStart + 8 * k))).Distinct().Count();
                    yield return $"{s.Name} 0x{runStart:X8}: {run} pointers, {distinct} distinct";
                }
                run = 0;
            }
        }
    }
}
