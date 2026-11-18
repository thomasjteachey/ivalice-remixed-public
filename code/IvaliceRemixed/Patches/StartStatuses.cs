namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's equipment start statuses (docs/MECHANICS.md: "Equipment start statuses"; PS1 0x8005C76C → 0x800271D0 in
/// the equipment bonus routine 0x8005C5C8): an item's start statuses go into the unit's current statuses as
/// (current | start) &amp; ~immune | innate, a byte at a time, with the immunities gathered so far. So a start status the
/// unit is immune to doesn't take (the Stone Gun's Petrify with a Ribbon), and innate statuses are current too. Vanilla
/// ORs the start statuses in. Installed by <see cref="Rules"/> with "start-statuses".
/// </summary>
/// <remarks>
/// The remaster's recalculation (0x2779EC, from the unit build 0x276108 and the rebuilds 0x2787CF and 0x278A19) runs per
/// item a loop over the unit's 15 status bytes (innate +0x57, immune +0x5C, current +0x61) ORing the item's 15 (its
/// bonus record +5): rcx the unit's byte, r9 the item's minus rcx, r8 the count. The loop (0x277B3F, 15 bytes) becomes a
/// jump to a stub that ORs the first 10 as before and works out the last 5 as ReMixed does, then goes on at 0x277B4E,
/// which sets rcx, r8 and r9 afresh. r10 is free there (each item reloads it before use).
/// </remarks>
internal static unsafe class StartStatuses
{
    public const int StubSize = 80;

    /// <summary>For the offline check: the loop's site and the stub.</summary>
    internal static nint At { get; private set; }
    internal static nint Stub { get; private set; }

    public static void Install(Game game)
    {
        // rcx = unit + 0x57; cmova eax, r13d; r9 -= rcx; [rbx+0x43] = al; the loop; then the next loop's lea rcx, [rbx+0x76].
        var site = game.Image.FindUnique(
            "48 8D 4B 57 41 0F 47 C5 4C 2B C9 88 43 43 41 8A 04 09 08 01 48 FF C1 49 83 E8 01 75 F1 48 8D 4B 76") + 14;
        var stub = GameImage.AllocateCodeNear(site, StubSize);
        WriteStub(stub, site + 15);
        var jump = new byte[15];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (site + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(site, jump);
        (At, Stub) = (site, (nint)stub);
    }

    /// <summary>
    /// The stub: rcx the unit's first status byte, r9 the item's minus rcx. ORs bytes 0–9 (innate and immune), then for
    /// the 5 current bytes: (current | start) &amp; ~immune (5 bytes back) | innate (10 back). Then <paramref name="back"/>.
    /// </summary>
    internal static void WriteStub(byte* stub, nint back)
    {
        var code = new List<byte>
        {
            0x41, 0xB8, 0x0A, 0, 0, 0, // mov r8d, 10
            0x41, 0x8A, 0x04, 0x09,    // first: mov al, [r9+rcx]
            0x08, 0x01,                // or [rcx], al
            0x48, 0xFF, 0xC1,          // inc rcx
            0x49, 0x83, 0xE8, 0x01,    // sub r8, 1
            0x75, 0xF1,                // jnz first
            0x41, 0xB8, 0x05, 0, 0, 0, // mov r8d, 5
            0x41, 0x8A, 0x04, 0x09,    // current: mov al, [r9+rcx] (the item's start)
            0x0A, 0x01,                // or al, [rcx] (the current)
            0x44, 0x8A, 0x51, 0xFB,    // mov r10b, [rcx-5] (immune)
            0x41, 0xF6, 0xD2,          // not r10b
            0x41, 0x22, 0xC2,          // and al, r10b
            0x0A, 0x41, 0xF6,          // or al, [rcx-10] (innate)
            0x88, 0x01,                // mov [rcx], al
            0x48, 0xFF, 0xC1,          // inc rcx
            0x49, 0x83, 0xE8, 0x01,    // sub r8, 1
            0x75, 0xE2,                // jnz current
            0xFF, 0x25, 0, 0, 0, 0,    // jmp [rip+0] → back
        };
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the start statuses' stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
