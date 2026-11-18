using Iced.Intel;

namespace ExeTool;

/// <summary>
/// Which scratch registers a function's callers keep across a call to it: after each direct call,
/// the code that runs next (fall-through and branches, up to the next call, return or indirect
/// jump) is searched for a volatile register read before it's written. A C# detour clobbers all of
/// them, so a hooked function with such a caller needs a stub that keeps them.
/// </summary>
/// <remarks>
/// Writes to a 32- or 64-bit register count as a full write; 8- and 16-bit writes keep the rest
/// live. rax is reported too (callers of a void function could keep it), as is xmm0–5.
/// </remarks>
internal static class LiveRegs
{
    private static readonly Register[] Volatile =
    [
        Register.RAX, Register.RCX, Register.RDX, Register.R8, Register.R9, Register.R10, Register.R11,
        Register.XMM0, Register.XMM1, Register.XMM2, Register.XMM3, Register.XMM4, Register.XMM5,
    ];

    public static IEnumerable<string> Check(PeImage image, uint function, int depth = 0)
    {
        var indent = new string(' ', 2 + 4 * depth);
        foreach (var (site, _, _) in Analysis.XRefs(image, [function]))
        {
            var call = Disasm.Decode(image, site, 15).First();
            if (call.Mnemonic != Mnemonic.Call && call.Mnemonic != Mnemonic.Jmp)
                continue;
            if (call.Mnemonic == Mnemonic.Jmp)
            {
                var from = image.FunctionStart(site);
                yield return $"{indent}0x{site:X8} tail jump from 0x{from:X8}, whose callers:";
                if (depth < 2 && from != 0)
                    foreach (var line in Check(image, from, depth + 1))
                        yield return line;
                continue;
            }
            var live = new SortedSet<string>();
            Walk(image, (uint)call.NextIP, new HashSet<Register>(), new HashSet<uint>(), live, 0);
            yield return $"{indent}0x{site:X8} [{image.FunctionStart(site):X8}]: {(live.Count == 0 ? "nothing kept" : "keeps " + string.Join(", ", live))}";
        }
    }

    private static void Walk(PeImage image, uint rva, HashSet<Register> written, HashSet<uint> seen, SortedSet<string> live, int depth)
    {
        var factory = new InstructionInfoFactory();
        for (var steps = 0; steps < 80 && depth < 6; steps++)
        {
            if (!seen.Add(rva))
                return;
            var instr = Disasm.Decode(image, rva, 15).First();
            var info = factory.GetInfo(instr);
            var writes = new List<Register>();
            foreach (var used in info.GetUsedRegisters())
            {
                var full = used.Register.GetFullRegister();
                if (Array.IndexOf(Volatile, full) < 0 && !(full >= Register.ZMM0 && full <= Register.ZMM5))
                    continue;
                if (full >= Register.ZMM0 && full <= Register.ZMM5)
                    full = Register.XMM0 + (full - Register.ZMM0);
                var reads = used.Access is OpAccess.Read or OpAccess.ReadWrite or OpAccess.CondRead or OpAccess.ReadCondWrite;
                if (reads && !written.Contains(full) && !written.Contains(used.Register)
                    && !(instr.Mnemonic == Mnemonic.Xor && instr.Op0Register == instr.Op1Register))
                    live.Add($"{full.ToString().ToLowerInvariant()} (0x{rva:X} {Disasm.Text(instr)})");
                if (used.Access is OpAccess.Write or OpAccess.ReadWrite)
                {
                    var size = used.Register.GetSize();
                    // A 32- or 64-bit write replaces the register; a smaller one only that part of it.
                    writes.Add(size >= 4 || full >= Register.XMM0 ? full : used.Register);
                }
            }
            foreach (var w in writes)
                written.Add(w);
            switch (instr.FlowControl)
            {
                case FlowControl.Return:
                case FlowControl.Call:
                case FlowControl.IndirectBranch:
                case FlowControl.IndirectCall:
                case FlowControl.Interrupt:
                    return;
                case FlowControl.UnconditionalBranch:
                    rva = (uint)instr.NearBranchTarget;
                    continue;
                case FlowControl.ConditionalBranch:
                    Walk(image, (uint)instr.NearBranchTarget, new HashSet<Register>(written), seen, live, depth + 1);
                    break;
            }
            rva = (uint)instr.NextIP;
        }
    }
}
