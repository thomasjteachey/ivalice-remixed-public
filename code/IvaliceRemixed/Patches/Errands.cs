namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's errands (docs/MECHANICS.md: "Errands"; WLDCORE 0x80073D9C, 0x80075834, 0x80075908, 0x80091588): special
/// characters and guests can be sent on errands, and while one is away the event condition "character X in the party"
/// doesn't count it. Installed by <see cref="Rules"/> with "errands".
/// </summary>
/// <remarks>
/// <para>Who can go: vanilla's candidates are generic units (sprite set 0x4A or more) in slots 0–15; ReMixed's are anyone
/// but Ramza (sprite set 4 or more) in slots 0–19, the guests' included. The remaster's candidate check (0x2EE37C: at
/// home, not empty, not +4 &amp; 0x20, sprite set 0x4A or more and not its own 0xA2 or 0xA3) gets 4 as its bound
/// (0x2EE3B1), and its two callers (0x2EDEA5, 0x1A32F4) loop to 0x36 instead of 0x32, the remaster's guests being
/// slots 0x32–0x35.</para>
/// <para>The condition (opcode 4; the remaster's 0x344134): a unit matched by its name id is skipped while away, but one
/// matched by its sprite set (0x344189) isn't. A stub there skips a unit whose errand byte (+0x11E) is set first.</para>
/// <para>Not here: ReMixed also zeroes its reads of the errands-under-way count (0x800715C4, 0x80074C48, 0x80081888),
/// which hides two town menus' errand entries (the reports); that isn't ported, as no other way ReMixed reports errands is found.</para>
/// </remarks>
internal static unsafe class Errands
{
    public const int StubSize = 64, Errand = 0x11E;

    /// <summary>For the offline check: the candidate check, the byte changes, and the condition's site and stub.</summary>
    internal static nint Candidate { get; private set; }
    internal static List<(string Name, nint At, byte Value)> Bytes { get; } = [];
    internal static nint ConditionAt { get; private set; }
    internal static nint ConditionStub { get; private set; }

    public static void Install(Game game)
    {
        var image = game.Image;
        var candidate = image.FindUnique(
            "83 F9 36 72 04 33 C0 EB 14 48 63 C1 48 8D 0D ?? ?? ?? ?? 48 69 C0 58 02 00 00 48 03 C1 80 B8 1E 01 00 00 00");
        var bound = image.FindUnique("0F B6 10 83 FA 4A 72 0D 81 EA A2 00 00 00") + 5;
        var loopA = image.FindUnique("48 85 C0 75 13 45 03 C6 41 83 F8 32 72 EA") + 11;
        var loopB = image.FindUnique("41 FF C1 44 8B C1 41 83 F9 32 72 E1") + 9;
        if ((ulong)(bound - candidate) > 0x40)
            throw new InvalidDataException("the errand candidate check isn't laid out as this DLL expects");
        // The condition: an empty slot to the next; the sprite set against the id; then the name-id paths.
        var condition = image.FindUnique("80 79 01 FF 74 65 0F B6 01 41 3B C2 74 68 4D 85 DB") + 6;
        var next = condition + *(sbyte*)(condition - 1);        // je rel8 at condition − 2
        var found = condition + 8 + *(sbyte*)(condition + 7);   // je rel8 at condition + 6
        var stub = GameImage.AllocateCodeNear(condition, StubSize);
        WriteConditionStub(stub, condition + 8, next, found);

        foreach (var (name, at, value) in new[]
                 {
                     ("the candidates' sprite bound", bound, (byte)0x04),
                     ("the first candidate loop's slots", loopA, (byte)0x36),
                     ("the candidate count's slots", loopB, (byte)0x36),
                 })
        {
            GameImage.WriteCode(at, [value]);
            Bytes.Add((name, at, value));
        }
        var jump = new byte[8];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (condition + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(condition, jump);
        (Candidate, ConditionAt, ConditionStub) = (candidate, condition, (nint)stub);
    }

    /// <summary>
    /// The condition's stub (rcx the roster unit, r10d the character's id): away → <paramref name="next"/>; the sprite set
    /// matching → <paramref name="found"/>; else <paramref name="cont"/> (the name-id paths). eax holds the sprite set.
    /// </summary>
    internal static void WriteConditionStub(byte* stub, nint cont, nint next, nint found)
    {
        var code = new List<byte>
        {
            0x0F, 0xB6, 0x01,                               // movzx eax, byte [rcx]
            0x80, 0xB9, 0x1E, 0x01, 0x00, 0x00, 0x00,       // cmp byte [rcx+0x11E], 0
            0x75, 0x13,                                     // jne away
            0x41, 0x3B, 0xC2,                               // cmp eax, r10d
            0x74, 0x1C,                                     // je found
            0xFF, 0x25, 0, 0, 0, 0,                         // jmp [rip+0] → cont
        };
        code.AddRange(BitConverter.GetBytes((long)cont));
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);             // away: jmp [rip+0] → next
        code.AddRange(BitConverter.GetBytes((long)next));
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);             // found: jmp [rip+0] → found
        code.AddRange(BitConverter.GetBytes((long)found));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the party condition's stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
