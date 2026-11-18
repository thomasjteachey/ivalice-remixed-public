namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Invite (docs/MECHANICS.md: "Invite hands over control"; PS1 0x8018E4D0 on): a unit the player's team
/// invites is the player's to control for the rest of the battle. Installed by <see cref="Rules"/> with
/// "invite-control".
/// </summary>
/// <remarks>
/// Applying Invite (status 9) moves the unit to the inviter's team, in its current team (+5) and its base team
/// (+0x1EE). The remaster clears the control bit 0x08 in +5 and leaves it out of +0x1EE, so the unit is the AI's.
/// ReMixed sets 0x08 in both when the inviter's team is the player's (team bits 0). The remaster's two stores
/// (0x30CC71) jump to a stub that does that and keeps the remaster's clearing for any other team.
/// </remarks>
internal static unsafe class InviteControl
{
    private const int StubSize = 48;

    // For the offline check.
    internal static nint At { get; private set; }
    internal static nint StubAt { get; private set; }

    public static void Install(Game game)
    {
        // Invite only: al = the inviter's team (+5 & 0x30), cl = (base team & 0xC7) | al; then +5 &= ~8 and +0x1EE = cl.
        var start = game.Image.FindUnique(
            "83 FB 09 75 ?? 41 8A 40 05 41 8A 8A EE 01 00 00 24 30 66 44 89 4A 10 80 E1 C7 " +
            "66 41 83 A2 EC 01 00 00 00 0A C8 41 80 62 05 F7 41 88 8A EE 01 00 00");
        var at = start + 37;
        var back = at + 12;
        var stub = GameImage.AllocateCodeNear(at, StubSize);
        WriteStub(stub, back);
        byte[] patch = [0xE9, 0, 0, 0, 0, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90];
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(patch, 1);
        GameImage.WriteCode(at, patch);
        At = at;
        StubAt = (nint)stub;
    }

    /// <summary>The stub: r10 the unit, al the inviter's team, cl its new base team; changes cl and the flags.</summary>
    internal static void WriteStub(byte* stub, nint back)
    {
        var code = new List<byte>
        {
            0x84, 0xC0, 0x75, 0x0A,                   // test al, al; jnz another team
            0x80, 0xC9, 0x08,                         // the player's: or cl, 8
            0x41, 0x80, 0x4A, 0x05, 0x08,             // or byte [r10+5], 8
            0xEB, 0x05,                               // jmp store
            0x41, 0x80, 0x62, 0x05, 0xF7,             // another team: and byte [r10+5], 0xF7 (the remaster's)
            0x41, 0x88, 0x8A, 0xEE, 0x01, 0x00, 0x00, // store: mov [r10+0x1EE], cl
            0xFF, 0x25, 0, 0, 0, 0,                   // jmp [rip+0]: back
        };
        code.AddRange(BitConverter.GetBytes((ulong)back));
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }
}
