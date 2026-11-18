namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's bow path (docs/analysis/battle-in-place-review.md, 0x8008DC50's row): the Arc solver no longer steers a
/// shot around the shooter's allies, and Jumping units count as obstacles. Installed by <see cref="Rules"/> with
/// "arc-obstacles".
/// </summary>
/// <remarks>
/// <para>The obstacle list (PS1 0x801AF7E4; the remaster's 0x3124E8) skipped units with Jump (+0x61 &amp; 4); the test
/// at 0x312559 goes, so a Jumping unit is listed with its height like any other.</para>
/// <para>The Arc solver (0x3129E0) tries two arcs and gets the unit each hits first (−1 for none). Vanilla takes the
/// first unless it hits one of the shooter's team, then the second unless that does, then falls back. ReMixed (0x801B02AC
/// → 0x80160900, and 0x801B0330's test gone) takes the first when both hit the same unit or the second hits none, and
/// the second otherwise, teams aside. Here: the first test (0x312C5F, the team xor at 0x312C73) jumps to a stub that
/// makes ReMixed's choice; the second (0x312CBC) goes, so a hit on the second arc is always taken.</para>
/// </remarks>
internal static unsafe class ArcObstacles
{
    public const int StubSize = 48;

    // For the offline check.
    internal static nint JumpSkip { get; private set; }
    internal static nint FirstTest { get; private set; }
    internal static nint SecondTest { get; private set; }
    internal static nint Stub { get; private set; }

    public static void Install(Game game)
    {
        var image = game.Image;
        // The obstacle list: skip the shooter, the excluded unit and empty slots (cmp cl, 0xFF; je), then Jump.
        var jump = image.FindUnique("80 F9 FF 74 4B F6 00 04 75 46 F6 02 40 75 41") + 5;
        // The first arc's test: hit1 (edi) not -1, its +0x1EE team xor the shooter's +5, & 0x30: different → take it.
        var first = image.FindUnique(
            "41 B8 EE 01 00 00 41 3B FA 74 49 83 FF 14 77 0C 48 8B CF 48 C1 E1 09 49 03 CB EB 03 49 8B C8 8A 09 32 08 " +
            "F6 C1 30 74 2C 8B 4D 40 8B C7") + 11;
        var takeFirst = first + 29;   // mov ecx, [rbp+0x40]: the first arc's result
        var tryNext = takeFirst + (sbyte)((byte*)first)[28];   // the je's target (rel8): the second arc's test
        // The second arc's test: mov al, [rax]; xor al, [r8]; test al, 0x30; je (the fallback); then take the second.
        var second = image.FindUnique("8A 00 41 32 00 A8 30 74 0A 8B F2 8B 45 50");
        if (tryNext != second - 0x14 || second - first is < 0 or > 0x100)
            throw new InvalidDataException("the Arc solver's choice isn't laid out as this DLL expects");

        var stub = GameImage.AllocateCodeNear(first, StubSize);
        WriteStub(stub, takeFirst, tryNext);
        GameImage.WriteCode(jump, [0x0F, 0x1F, 0x44, 0x00, 0x00]);
        var code = new byte[29];
        Array.Fill(code, (byte)0x90);
        code[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (first + 5)))).CopyTo(code, 1);
        GameImage.WriteCode(first, code);
        GameImage.WriteCode(second, [0x66, 0x0F, 0x1F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00]);
        JumpSkip = jump;
        FirstTest = first;
        SecondTest = second;
        Stub = (nint)stub;
    }

    /// <summary>
    /// ReMixed's choice (PS1 0x80160900), with edi the first arc's hit, edx the second's and r10d −1: the first arc
    /// when the second hits none or both hit the same unit, else on to the second arc's test.
    /// </summary>
    internal static void WriteStub(byte* stub, nint takeFirst, nint tryNext)
    {
        var code = new List<byte> { 0x41, 0x3B, 0xD2, 0x74, 0x12 };   // cmp edx, r10d; je first
        code.AddRange([0x3B, 0xFA, 0x74, 0x0E]);                       // cmp edi, edx; je first
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);                       // jmp [rip+0] → the second arc's test
        code.AddRange(BitConverter.GetBytes((long)tryNext));
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);                       // first: jmp [rip+0] → take the first arc
        code.AddRange(BitConverter.GetBytes((long)takeFirst));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the arc choice stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
