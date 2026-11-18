namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Pass (movement 0x1FC, vanilla Silent Walk's slot; docs/MECHANICS.md: "Pass"; PS1 0x801749A4–0x801749D8
/// through 0x8015A2D0 and 0x80152860): a unit with Pass moves through enemies. Installed by <see cref="Rules"/> with
/// "pass".
/// </summary>
/// <remarks>
/// <para>The movement setup (PS1 0x80174430, the remaster's 0x27ADB8) marks each other unit's tile: 0x20 "can't stop
/// here", 0x40 "can't pass through", the second for an enemy (the team bits, +0x05 &amp; 0x30, differ). ReMixed's
/// rewrite clears 0x40 on every unit's tile when the mover has Pass (PS1 +0x95 &amp; 2, here +0x9E &amp; 2). The
/// remaster keeps the mover's byte at [0x1882610] (stored at 0x27AFE7, from +0x9E at 0x27AE11; the setup only clears
/// its 0x04) and picks the enemy's flags with a cmove at 0x27B1D7: those 8 bytes jump to a stub that does the same
/// and then clears 0x40 for Pass. The AI's moves come from the same setup, so AI units with Pass go through enemies too,
/// as on the PS1.</para>
/// <para>The same rewrite's riding rules (an enemy's free chocobo can't be boarded, except with Pass and Train) are
/// <see cref="RidingBoarding"/>'s, whose stub sits just before this one and reads <see cref="Mover"/>.</para>
/// </remarks>
internal static unsafe class Pass
{
    public const int StubSize = 64, PassBit = 0x02, CantPass = 0x40;

    // The tile's flags for another unit: r8d the base, the team test, 0x40 for an enemy (cmove keeps the base for an
    // ally), the unit's x into the tile record, then its y and the flags.
    private const string TeamTest =
        "44 0F B6 C5 41 8A D7 43 32 54 0A 05 41 8A C0 0C 40 0F B6 C8 80 E2 30 43 8A 44 0A 4F 41 0F 44 C8 41 88 43 FE " +
        "43 8A 44 0A 50 41 88 4B 01";
    private const int Site = 28, SiteLength = 8;

    /// <summary>The mover's movement byte (+0x9E), as the setup keeps it.</summary>
    internal static byte* Mover { get; private set; }
    internal static nint SiteAt { get; private set; }
    internal static nint Stub { get; private set; }

    public static void Install(Game game)
    {
        var image = game.Image;
        var test = image.FindUnique(TeamTest);
        // The setup's store of the mover's byte: test bpl, 2; mov [mover], bl; mov r8d, edx.
        var store = image.FindUnique("40 F6 C5 02 88 1D ?? ?? ?? ?? 44 8B C2 48 8D 05");
        // bl is the mover's +0x9E, loaded early in the same setup.
        var load = image.FindUnique("8A 9F 9E 00 00 00");
        if ((ulong)(test - store) > 0x400 || (ulong)(store - load) > 0x400)
            throw new InvalidDataException("the movement setup isn't laid out as this DLL expects");
        Mover = (byte*)GameImage.Rip(store + 4, 2, 6);
        var site = test + Site;
        var stub = GameImage.AllocateCodeNear(site, StubSize);
        WriteStub(stub, (nint)Mover, site + SiteLength);
        byte[] jump = [0xE9, 0, 0, 0, 0, 0x90, 0x90, 0x90];
        BitConverter.GetBytes(checked((int)((nint)stub - (site + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(site, jump);
        SiteAt = site;
        Stub = (nint)stub;
    }

    /// <summary>
    /// The stub: the game's two instructions, then 0x40 cleared from the flags (cl) when the mover has Pass; back at
    /// <paramref name="back"/>. Flags aren't read after it before they're set again; push and pop keep rax.
    /// </summary>
    internal static void WriteStub(byte* stub, nint mover, nint back)
    {
        var code = new List<byte>
        {
            0x41, 0x0F, 0x44, 0xC8,             // cmove ecx, r8d
            0x41, 0x88, 0x43, 0xFE,             // mov [r11-2], al
            0x50,                               // push rax
            0x48, 0xB8,                         // mov rax, mover
        };
        code.AddRange(BitConverter.GetBytes((long)mover));
        code.AddRange([0xF6, 0x00, PassBit]);   // test byte [rax], 2
        code.Add(0x58);                         // pop rax
        code.AddRange([0x74, 0x03]);            // jz +3
        code.AddRange([0x80, 0xE1, 0xFF & ~CantPass]); // and cl, 0xBF
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]); // jmp [rip+0]
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > StubSize)
            throw new InvalidOperationException("Pass's stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
