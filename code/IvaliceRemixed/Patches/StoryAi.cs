namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's AI for its own story units that hold a post (docs/MECHANICS.md: "ReMixed's AI changes"): a Defensive unit
/// (battle data byte 35, unit +0x199 in the remaster, PS1 +0x167, &amp; 0x08) with byte 38's 0x04 (+0x19C, PS1 +0x16A)
/// that also has Stay Near XY (&amp; 0x20) neither gets the AI mode that keeps it away from its enemies nor runs away
/// when Critical, a Frog or unable to act. Installed by <see cref="Rules"/> with "story-ai".
/// </summary>
/// <remarks>
/// <para>Vanilla's AI setup (PS1 0x8019A1B0; the remaster's 0x316A67 in 0x3167E4) gives a Defensive unit with byte 38's
/// 0x04 mode 0x11, whose turn (0x38C95C) always goes to the run-away routine 0x38D65C; ReMixed skips that for a unit with
/// Stay Near XY too (0x8019A1B8), which goes on to the Focus Unit test as one without 0x04 does. And the turn's step
/// before the mode's (PS1 0x80195E64; the remaster's 0x320B9A in 0x320920) sends a Critical or Frog unit (+0x63 &amp; 3,
/// with an AI flag) or one that can't act (+0x65 &amp; 4) to the same routine; ReMixed skips it for those units
/// (0x80195E6C).</para>
/// <para>Only ReMixed's own battles have such units (side chapter 1-A's second battle, entry 0x1D8: an enemy Thief; its
/// last, 0x1E0: two AI-run units on the player's side; and 0x1E1, which the port doesn't ship); vanilla's four Defensive units with 0x04
/// (Araguay Woods, Zirekile Falls, Zarghidas, Rafa's rescue) have no Stay Near XY and keep vanilla's AI. Each change is a
/// jump (6 bytes) to a stub that tests the two flags first. Not seen in the game yet.</para>
/// </remarks>
internal static unsafe class StoryAi
{
    public const int Flags = 0x199, Byte38 = 0x19C, Defensive = 0x08, StayNearXy = 0x20, PostBit = 0x04;
    public const int StubSize = 96;

    /// <summary>The AI setup's test (0x316A67): mov cl, [r9] (the record's copy of byte 35); test cl, 8; je out; test
    /// byte [rdi+7], 4; je the Focus Unit test; mov byte [r15+r10+0x19AD], 0x11; jmp out; test cl, 0x40.</summary>
    internal const string ModePattern = "41 8A 09 F6 C1 08 74 49 F6 47 07 04 74 0B 43 C6 84 17 AD 19 00 00 11 EB 38 F6 C1 40";
    /// <summary>The turn's step (0x320B8C): rax the acting unit, rdx the AI record; test byte [rax+0x63], 3; je; test
    /// byte [flags], 0x40; jne the step; test byte [rax+0x65], 4; je the mode's turn (0x320973); test [rdx+4], 0x40; je.</summary>
    internal const string StepPattern =
        "48 8B 05 ?? ?? ?? ?? 48 8B 15 ?? ?? ?? ?? F6 40 63 03 74 09 F6 05 ?? ?? ?? ?? 40 75 14 F6 40 65 04 0F 84 ?? ?? ?? ?? " +
        "F6 42 04 40 0F 84";

    /// <summary>For the offline check: where each stub went and the game's addresses they go back to.</summary>
    internal static nint ModeSite { get; private set; }
    internal static nint StepSite { get; private set; }
    internal static nint ModeStub { get; private set; }
    internal static nint StepStub { get; private set; }
    internal static (nint Mode, nint Focus) ModeTargets { get; private set; }
    internal static (nint Step, nint NotCritical, nint Turn) StepTargets { get; private set; }

    public static void Install(Game game)
    {
        var image = game.Image;
        var mode = image.FindUnique(ModePattern);
        var modeAt = mode + 8;            // test byte [rdi+7], 4; je
        var modeSet = mode + 14;          // mov byte [r15+r10+0x19AD], 0x11
        var focus = mode + 25;            // test cl, 0x40
        if (modeAt + 6 + ((sbyte*)modeAt)[5] != focus)
            throw new InvalidDataException("the AI setup's test doesn't go where this DLL expects");
        var step = image.FindUnique(StepPattern);
        var stepAt = step + 14;           // test byte [rax+0x63], 3; je
        var critical = stepAt + 6;        // test byte [flags], 0x40
        var notCritical = stepAt + 6 + ((sbyte*)stepAt)[5];
        var turn = GameImage.Rip(step + 33, 2, 6);  // je the mode's turn
        if (notCritical != step + 29 || GameImage.Rip(step + 43, 2, 6) != turn)
            throw new InvalidDataException("the AI's run-away step isn't laid out as this DLL expects");

        var modeStub = GameImage.AllocateCodeNear(modeAt, StubSize);
        WriteModeStub(modeStub, modeSet, focus);
        var stepStub = GameImage.AllocateCodeNear(stepAt, StubSize);
        WriteStepStub(stepStub, critical, notCritical, turn);
        GameImage.WriteCode(modeAt, Jump(modeAt, (nint)modeStub, 6));
        GameImage.WriteCode(stepAt, Jump(stepAt, (nint)stepStub, 6));
        (ModeSite, StepSite, ModeStub, StepStub) = (modeAt, stepAt, (nint)modeStub, (nint)stepStub);
        ModeTargets = (modeSet, focus);
        StepTargets = (critical, notCritical, turn);
    }

    private static byte[] Jump(nint at, nint to, int length)
    {
        var jump = new byte[length];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)(to - (at + 5)))).CopyTo(jump, 1);
        return jump;
    }

    private static void JumpTo(List<byte> code, nint target)
    {
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);  // jmp [rip+0]
        code.AddRange(BitConverter.GetBytes((long)target));
    }

    /// <summary>
    /// The AI setup's mode test (cl the unit's byte 35, rdi the AI record, its +7 byte 38's copy; reached only for a
    /// Defensive unit): Stay Near XY, or no 0x04 → <paramref name="focus"/>; else → <paramref name="mode"/> (mode 0x11).
    /// Flags only.
    /// </summary>
    internal static void WriteModeStub(byte* stub, nint mode, nint focus)
    {
        var code = new List<byte>
        {
            0xF6, 0xC1, StayNearXy,          // test cl, 0x20
            0x75, 0x14,                      // jnz focus
            0xF6, 0x47, 0x07, PostBit,       // test byte [rdi+7], 4
            0x74, 0x0E,                      // jz focus
        };
        JumpTo(code, mode);                  // (offset 11, 14 bytes)
        JumpTo(code, focus);                 // focus: (offset 25)
        Write(stub, code);
    }

    /// <summary>
    /// The run-away step's first test (rax the acting unit): Defensive, Stay Near XY and byte 38's 0x04 →
    /// <paramref name="turn"/> (past the step); else vanilla's test of +0x63 &amp; 3 → <paramref name="critical"/> when
    /// set, <paramref name="notCritical"/> when not. Flags only.
    /// </summary>
    internal static void WriteStepStub(byte* stub, nint critical, nint notCritical, nint turn)
    {
        var code = new List<byte>
        {
            0xF6, 0x80, (byte)(Flags & 0xFF), (byte)(Flags >> 8), 0x00, 0x00, StayNearXy,  // test byte [rax+0x199], 0x20
            0x74, 0x12,                                                            // jz vanilla
            0xF6, 0x80, (byte)(Flags & 0xFF), (byte)(Flags >> 8), 0x00, 0x00, Defensive,   // test byte [rax+0x199], 8
            0x74, 0x09,                                                            // jz vanilla
            0xF6, 0x80, (byte)(Byte38 & 0xFF), (byte)(Byte38 >> 8), 0x00, 0x00, PostBit,   // test byte [rax+0x19C], 4
            0x75, 0x22,                                                            // jnz skip
            0xF6, 0x40, 0x63, 0x03,                                                // vanilla: test byte [rax+0x63], 3
            0x74, 0x0E,                                                            // jz not critical
        };
        JumpTo(code, critical);              // (offset 33)
        JumpTo(code, notCritical);           // not critical: (offset 47)
        JumpTo(code, turn);                  // skip: (offset 61)
        Write(stub, code);
    }

    private static void Write(byte* stub, List<byte> code)
    {
        if (code.Count > StubSize)
            throw new InvalidOperationException("the story AI's stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
