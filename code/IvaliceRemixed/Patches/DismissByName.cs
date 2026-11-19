namespace IvaliceRemixed.Patches;

/// <summary>
/// DismissUnit (event instruction 0x7A) finds the unit by its name id when no sprite set matches, as ReMixed's does: its
/// events dismiss by name the units whose sprite set isn't their id. Side chapter 1-A's river camp (0x0F1, its after-
/// battle run 0x113, and 0x0F2) dismisses Bryn and Lorne as 0x69 and 0x6A, whose locked looks are sprite sets 0xB5 and
/// 0xB4; 0x118 dismisses Elias as 0x6B (his 0x9C); 1-A's end (0x0FC) dismisses 0x78, 0x79 and 0x6C; the Argath campaign
/// (0x102, 0x106) the units it renames 0x6C. Seen on the PS1 (2026-11-18): 0x0F2 empties Bryn's and Lorne's roster
/// slots. The remaster's DismissUnit compares the sprite set alone, so in the port they stayed (a tester on 0.9.17).
/// Installed with ReMixed's events (<see cref="RemixedEvents"/>).
/// </summary>
/// <remarks>
/// The remaster's DismissUnit is inline in the event runner (0x23A868, from 0x23D529): from roster slot 1 to 0x35 (r8d,
/// and r9 the same as an index; r11 the roster, rdx a record, r10b 0xFF, r12w the id) a record in use (+1 not 0xFF)
/// whose sprite set (+0) is the id; then (0x23D56F) none found goes on (je 0x23B60D), or the record's equipment goes
/// back to the inventory and +1 becomes 0xFF. The 10 bytes of that test become a jump to <see cref="StubCode"/>, which
/// looks again by the name id (+0x230) when the sprite set found none, from slot 1 as the remaster does, and goes on to
/// the game's own dismissal with r8d, r9 and rdx set as its loop leaves them. It touches rax and the flags, which the
/// code after sets before reading.
/// </remarks>
internal static unsafe class DismissByName
{
    internal const string LoopPattern =
        "4C 8D 1D ?? ?? ?? ?? 41 83 F8 36 72 05 49 8B D5 EB 0D 49 63 C0 48 69 D0 58 02 00 00 49 03 D3 44 38 52 01 " +
        "74 09 0F B6 0A 66 41 3B CC 74 0C 45 03 C6 4D 03 CE 41 83 F8 36 7C C6 41 83 F8 36 0F 84 ?? ?? ?? ?? " +
        "BE 02 00 00 00";
    internal const int TestAt = 0x3A, TestBytes = 10, RosterName = 0x230, Slots = UnitTables.RosterSlots;

    // For the offline check.
    internal static nint SiteAt { get; private set; }
    internal static nint Probe { get; private set; }

    public static void Install(Game game)
    {
        var image = game.Image;
        var loop = image.FindUnique(LoopPattern);
        if ((byte*)GameImage.Rip(loop, 3, 7) != game.Units.Roster)
            throw new InvalidDataException("DismissUnit's loop doesn't read the roster this DLL knows");
        var site = loop + TestAt;
        var notFound = site + TestBytes + *(int*)(site + 6);
        var code = StubCode(site + TestBytes, notFound);
        var stub = GameImage.AllocateCodeNear(site, code.Length);
        code.CopyTo(new Span<byte>(stub, code.Length));
        var jump = new byte[TestBytes];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9; // jmp rel32 stub
        BitConverter.GetBytes(checked((int)((nint)stub - (site + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(site, jump);
        SiteAt = site;
        var probe = ProbeCode();
        var probeStub = GameImage.AllocateCode(probe.Length);
        probe.CopyTo(new Span<byte>(probeStub, probe.Length));
        Probe = (nint)probeStub;
    }

    /// <summary>The stub: the sprite set's find as it was, else a find by the name id, then on to
    /// <paramref name="found"/> (the game's dismissal) or <paramref name="notFound"/>.</summary>
    internal static byte[] StubCode(nint found, nint notFound)
    {
        var a = new Code();
        Body(a);
        a.Label("none");
        a.Emit(0xFF, 0x25, 0, 0, 0, 0);                       // jmp [rip+0] → the game's "none found"
        a.Emit(BitConverter.GetBytes((long)notFound));
        a.Label("found");
        a.Emit(0xFF, 0x25, 0, 0, 0, 0);                       // jmp [rip+0] → the game's dismissal
        a.Emit(BitConverter.GetBytes((long)found));
        return a.Build();
    }

    /// <summary>The stub's test and search, ending at "found" (r8d, r9 and rdx the record) or "none".</summary>
    private static void Body(Code a)
    {
        a.Emit(0x41, 0x83, 0xF8, 0x36);                       // cmp r8d, 0x36: the sprite set found none?
        a.Jump(0x75, "found");                                // jne
        a.Emit(0x41, 0xB8, 0x01, 0, 0, 0);                    // mov r8d, 1: slot 1 on, as the remaster's loop
        a.Label("loop");
        a.Emit(0x49, 0x63, 0xC0);                             // movsxd rax, r8d
        a.Emit(0x48, 0x69, 0xD0, 0x58, 0x02, 0, 0);           // imul rdx, rax, 0x258
        a.Emit(0x49, 0x03, 0xD3);                             // add rdx, r11: the record
        a.Emit(0x44, 0x38, 0x52, 0x01);                       // cmp [rdx+1], r10b: empty?
        a.Jump(0x74, "next");                                 // je
        a.Emit(0x66, 0x44, 0x39, 0xA2);                       // cmp word [rdx+0x230], r12w: the name id
        a.Emit(BitConverter.GetBytes(RosterName));
        a.Jump(0x74, "named");                                // je
        a.Label("next");
        a.Emit(0x41, 0xFF, 0xC0);                             // inc r8d
        a.Emit(0x41, 0x83, 0xF8, 0x36);                       // cmp r8d, 0x36
        a.Jump(0x7C, "loop");                                 // jl
        a.Jump(0xEB, "none");                                 // jmp
        a.Label("named");
        a.Emit(0x4D, 0x63, 0xC8);                             // movsxd r9, r8d: the loop's index too
        a.Jump(0xEB, "found");
    }

    /// <summary>For the offline check: (rcx the roster, edx the id, r8d the sprite set's find: 0x36 for none) → the slot
    /// dismissed, −1 for none; r9 is checked to match.</summary>
    internal static byte[] ProbeCode()
    {
        var a = new Code();
        a.Emit(0x41, 0x54, 0x41, 0x55);                       // push r12; push r13
        a.Emit(0x4C, 0x8B, 0xD9);                             // mov r11, rcx
        a.Emit(0x44, 0x0F, 0xB7, 0xE2);                       // movzx r12d, dx
        a.Emit(0x41, 0xBA, 0xFF, 0, 0, 0);                    // mov r10d, 0xFF
        a.Emit(0x45, 0x33, 0xED);                             // xor r13d, r13d
        a.Emit(0x4D, 0x63, 0xC8);                             // movsxd r9, r8d
        Body(a);
        a.Label("none");
        a.Emit(0xB8, 0xFF, 0xFF, 0xFF, 0xFF);                 // mov eax, -1
        a.Jump(0xEB, "end");
        a.Label("found");
        a.Emit(0x44, 0x89, 0xC0);                             // mov eax, r8d
        a.Emit(0x4D, 0x39, 0xC1);                             // cmp r9, r8
        a.Jump(0x74, "end");                                  // je
        a.Emit(0xB8, 0xFE, 0xFF, 0xFF, 0xFF);                 // mov eax, -2: r9 disagrees
        a.Label("end");
        a.Emit(0x41, 0x5D, 0x41, 0x5C, 0xC3);                 // pop r13; pop r12; ret
        return a.Build();
    }

    /// <summary>A few bytes of x64 with short jumps to labels.</summary>
    private sealed class Code
    {
        private readonly List<byte> _code = [];
        private readonly List<(int At, string Label)> _fixups = [];
        private readonly Dictionary<string, int> _labels = [];

        public void Emit(params byte[] bytes) => _code.AddRange(bytes);

        public void Jump(byte op, string label)
        {
            Emit(op, 0);
            _fixups.Add((_code.Count - 1, label));
        }

        public void Label(string name) => _labels[name] = _code.Count;

        public byte[] Build()
        {
            foreach (var (at, label) in _fixups)
                _code[at] = (byte)checked((sbyte)(_labels[label] - (at + 1)));
            return [.. _code];
        }
    }
}
