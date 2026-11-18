namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's one-handing (docs/MECHANICS.md: "One-handing two-handed weapons"; WORLD 0x801498C0 on, EQUIP.OUT 0x801C84A4,
/// SCUS 0x8005C034): with support 0x1C7 (Equip Shield) or 0x1CC (the unit's support bits +0x8F &amp; 0x42), a weapon that
/// forces two hands (its data +1 &amp; 1: axes, bows) counts as one-handed, so it can go with a shield or another
/// weapon. Installed by <see cref="Rules"/> with "one-handing".
/// </summary>
/// <remarks>
/// <para>The remaster reads the weapon's bit in five places that matter for play; each gets a stub that lets the bit
/// count only without the supports (the built unit's +0x98, which the party menu's record copies to +0x16C):</para>
/// <list type="bullet">
/// <item>the equip-slot check 0x288788 (the party menu and the formation screen): the new item's bit at 0x288830 and
/// the other hand's at 0x28887E (the record in r12 there; the index in rbp);</item>
/// <item>the two optimize routines' left-hand step, which skips the left hand behind a forced weapon: 0x287966 (the
/// index at [rsp+0x36]) and 0x288111 (the index in r14);</item>
/// <item>random gear's left hand (0x27771E in the unit build, rbx the unit), which gives no left-hand item behind a
/// forced weapon: ReMixed keeps one with the supports (SCUS 0x8005C034).</item>
/// </list>
/// <para>Display left as it is: the hand labels, the record's two-handing flag (0x284998) and the status windows. The
/// battle needs nothing: the Two Hands doubling doesn't read the bit.</para>
/// </remarks>
internal static unsafe class OneHanding
{
    public const int Supports = 0x42, RecordSupports = 0x16C, UnitSupports = 0x98, Forced = 0x01;
    public const int StubSize = 80;

    /// <summary>For the offline check: each site and its stub, in the order above (slot check ×2, optimize ×2, gear).</summary>
    internal static List<(string Name, nint At, nint Stub)> Sites { get; } = [];
    internal static nint* Records { get; private set; }

    public static void Install(Game game)
    {
        var image = game.Image;
        // The slot check: the records' table, the new item's bits, the other hand's bits.
        var records = image.FindUnique("48 8D 05 ?? ?? ?? ?? 0F B7 CB 48 8B 04 F8 48 8B EF 0F B7 50 24 4C 8D 40 73 E8");
        var newItem = image.FindUnique("44 0F B6 58 01 41 8B FB 41 23 FC 41 83 E3 08 66 F7 DE") + 8;
        var otherHand = image.FindUnique("0F B6 48 01 8B D1 83 E2 01 83 E1 08 45 85 DB") + 6;
        // The optimize routines: the record's Two Hands test, the weapon's bit 4, then its forced bit.
        var optimizeA = image.FindUnique(
            "0F B7 0E E8 ?? ?? ?? ?? 41 B8 01 00 00 00 44 84 40 01 0F 85 ?? ?? ?? ?? 33 D2 41 0F B7 CC") + 14;
        var optimizeB = image.FindUnique(
            "0F B7 0F E8 ?? ?? ?? ?? 41 B8 01 00 00 00 44 84 40 01 0F 85 ?? ?? ?? ?? 44 8B 54 24 38") + 14;
        // Random gear: the weapon's forced bit, else Two Hands (+0x9A & 2), either way no left-hand item.
        var gear = image.FindUnique("E8 ?? ?? ?? ?? F6 40 01 01 75 09 F6 83 9A 00 00 00 02 74 05 44 8A DF EB") + 5;
        Records = (nint*)GameImage.Rip(records, 3, 7);

        var stubs = new[]
        {
            ("the equip-slot check, the new item", newItem, 7, (Action<nint>)(s => WriteNewItemStub((byte*)s, Records, newItem + 7))),
            ("the equip-slot check, the other hand", otherHand, 6, s => WriteOtherHandStub((byte*)s, otherHand + 6)),
            ("optimize, the left hand", optimizeA, 10, s => WriteOptimizeStub((byte*)s, Records, false,
                GameImage.Rip(optimizeA + 4, 2, 6), optimizeA + 10)),
            ("optimize, the left hand (the other routine)", optimizeB, 10, s => WriteOptimizeStub((byte*)s, Records, true,
                GameImage.Rip(optimizeB + 4, 2, 6), optimizeB + 10)),
            ("random gear's left hand", gear, 6, s => WriteGearStub((byte*)s, gear + 6, gear + 6 + 9)),
        };
        // Every stub first, then the jumps, so a failure leaves the game's code as it was.
        var built = new List<(string Name, nint At, int Length, nint Stub)>();
        foreach (var (name, at, length, write) in stubs)
        {
            var stub = (nint)GameImage.AllocateCodeNear(at, StubSize);
            write(stub);
            built.Add((name, at, length, stub));
        }
        foreach (var (name, at, length, stub) in built)
        {
            var jump = new byte[length];
            Array.Fill(jump, (byte)0x90);
            jump[0] = 0xE9;
            BitConverter.GetBytes(checked((int)(stub - (at + 5)))).CopyTo(jump, 1);
            GameImage.WriteCode(at, jump);
            Sites.Add((name, at, stub));
        }
    }

    private static void Finish(List<byte> code, byte* stub, string what)
    {
        if (code.Count > StubSize)
            throw new InvalidOperationException($"one-handing's {what} stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    private static void JumpTo(List<byte> code, nint target)
    {
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);
        code.AddRange(BitConverter.GetBytes((long)target));
    }

    /// <summary>The new item's bits (r11d the weapon flags, edi its forced bit): edi = 0 when the record has the supports.</summary>
    internal static void WriteNewItemStub(byte* stub, nint* records, nint back)
    {
        var code = new List<byte>
        {
            0x83, 0xE7, Forced,                                      // and edi, 1
            0x41, 0x83, 0xE3, 0x08,                                  // and r11d, 8
            0x48, 0xB8,                                              // mov rax, records
        };
        code.AddRange(BitConverter.GetBytes((long)records));
        code.AddRange([0x48, 0x8B, 0x04, 0xE8]);                     // mov rax, [rax+rbp*8]
        code.AddRange([0xF6, 0x80, 0x6C, 0x01, 0x00, 0x00, Supports]); // test byte [rax+0x16C], 0x42
        code.AddRange([0x74, 0x02]);                                 // jz back
        code.AddRange([0x33, 0xFF]);                                 // xor edi, edi
        JumpTo(code, back);
        Finish(code, stub, "new item");
    }

    /// <summary>The other hand's bits (ecx the flags, edx its forced bit; r12 the record): edx = 0 with the supports.</summary>
    internal static void WriteOtherHandStub(byte* stub, nint back)
    {
        var code = new List<byte>
        {
            0x83, 0xE2, Forced,                                      // and edx, 1
            0x83, 0xE1, 0x08,                                        // and ecx, 8
            0x41, 0xF6, 0x84, 0x24, 0x6C, 0x01, 0x00, 0x00, Supports, // test byte [r12+0x16C], 0x42
            0x74, 0x02,                                              // jz back
            0x33, 0xD2,                                              // xor edx, edx
        };
        JumpTo(code, back);
        Finish(code, stub, "other hand");
    }

    /// <summary>
    /// An optimize routine's forced test (rax the weapon's data): forced and without the supports → <paramref name="skip"/>
    /// (no left hand), else <paramref name="cont"/>. The index is at [rsp+0x36], or in r14 for the other routine. rax is
    /// kept for the skip path.
    /// </summary>
    internal static void WriteOptimizeStub(byte* stub, nint* records, bool indexInR14, nint skip, nint cont)
    {
        var code = new List<byte> { 0xF6, 0x40, 0x01, Forced, 0x74, 0 };  // test byte [rax+1], 1; jz cont
        var toCont1 = code.Count;
        code.Add(0x50);                                                     // push rax
        if (!indexInR14)
            code.AddRange([0x48, 0x0F, 0xBF, 0x44, 0x24, 0x3E]);           // movsx rax, word [rsp+0x36+8]
        code.AddRange([0x48, 0xB9]);                                        // mov rcx, records
        code.AddRange(BitConverter.GetBytes((long)records));
        code.AddRange(indexInR14 ? [0x4A, 0x8B, 0x04, 0xF1] : [0x48, 0x8B, 0x04, 0xC1]); // mov rax, [rcx+r14*8 | rax*8]
        code.AddRange([0xF6, 0x80, 0x6C, 0x01, 0x00, 0x00, Supports]);     // test byte [rax+0x16C], 0x42
        code.Add(0x58);                                                     // pop rax
        code.AddRange([0x75, 0]);                                           // jnz cont
        var toCont2 = code.Count;
        JumpTo(code, skip);
        var at = code.Count;
        JumpTo(code, cont);
        code[toCont1 - 1] = checked((byte)(at - toCont1));
        code[toCont2 - 1] = checked((byte)(at - toCont2));
        Finish(code, stub, "optimize");
    }

    /// <summary>
    /// Random gear's forced test (rax the weapon's data, rbx the unit): forced and without the supports → <paramref name="noLeft"/>,
    /// else <paramref name="cont"/> (the Two Hands test).
    /// </summary>
    internal static void WriteGearStub(byte* stub, nint cont, nint noLeft)
    {
        var code = new List<byte>
        {
            0xF6, 0x40, 0x01, Forced,                                // test byte [rax+1], 1
            0x74, 0x17,                                              // jz cont
            0xF6, 0x83, UnitSupports, 0x00, 0x00, 0x00, Supports,     // test byte [rbx+0x98], 0x42
            0x75, 0x0E,                                              // jnz cont
        };
        JumpTo(code, noLeft);
        JumpTo(code, cont);
        Finish(code, stub, "random gear");
    }
}
