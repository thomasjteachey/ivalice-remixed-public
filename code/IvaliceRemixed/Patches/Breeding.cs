namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's daily monster step for units away (WORLD 0x80125AA0 in 0x80125A04, from WLDCORE's day change): a roster
/// unit whose errand byte is set is skipped, so a monster away on an errand doesn't breed, an egg away doesn't hatch, and
/// units away don't count towards a hatchling's level. Installed by <see cref="Rules"/> with "breeding-errands".
/// </summary>
/// <remarks>
/// The remaster's step is 0x289900; its loop over the 54 roster slots (rbx the unit) skips an empty one at 0x2899C1
/// (cmp byte [rbx+1], 0xFF; je to the next slot). Those 10 bytes jump to a stub that also skips a unit whose errand byte
/// (+0x11E, the PS1's +0xD0; analysis/world-in-place-review.md) is non-zero.
/// </remarks>
internal static unsafe class Breeding
{
    public const int Errand = 0x11E, StubSize = 48;

    /// <summary>For the offline check: the site, the slot's next step and the stub.</summary>
    internal static nint At { get; private set; }
    internal static nint Next { get; private set; }
    internal static nint Stub { get; private set; }

    public static void Install(Game game)
    {
        // movsxd rax, r14d; imul rbx, rax, 0x258; add rbx, r9; je; the empty test; test byte [rbx+4], 4 (an egg).
        var match = game.Image.FindUnique(
            "49 63 C6 48 69 D8 58 02 00 00 49 03 D9 0F 84 ?? ?? ?? ?? 80 7B 01 FF 0F 84 ?? ?? ?? ?? F6 43 04 04 0F 84");
        var site = match + 19;
        var skip = GameImage.Rip(site + 4, 2, 6);
        var stub = GameImage.AllocateCodeNear(site, StubSize);
        WriteStub(stub, site + 10, skip);
        var jump = new byte[10];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (site + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(site, jump);
        (At, Next, Stub) = (site, skip, (nint)stub);
    }

    /// <summary>The stub: an empty slot or a unit away goes to <paramref name="skip"/>, the rest to <paramref name="back"/>.</summary>
    internal static void WriteStub(byte* stub, nint back, nint skip)
    {
        var code = new List<byte>
        {
            0x80, 0x7B, 0x01, 0xFF,                   // cmp byte [rbx+1], 0xFF
            0x74, 0x17,                               // je away
            0x80, 0xBB, 0x1E, 0x01, 0x00, 0x00, 0x00, // cmp byte [rbx+0x11E], 0
            0x75, 0x0E,                               // jne away
            0xFF, 0x25, 0, 0, 0, 0,                   // jmp [rip+0] → back
        };
        code.AddRange(BitConverter.GetBytes((long)back));
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);       // away: jmp [rip+0] → skip
        code.AddRange(BitConverter.GetBytes((long)skip));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the breeding stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
