namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Dragon Tech command (docs/MECHANICS.md: "Dragon Tech"): Lancet (0x144) and Ice, Fire and Thunder Breath
/// (0x151–0x153) sit in Jump's slots 8–11 (skillset 0x12), where the Lancer learns them, and battle shows them under
/// Dragon Tech, skillset 0x64, with the same four in the same slots, an ordinary skillset (type 0). Installed by
/// <see cref="Rules"/> with "dragon-tech", after "jump-rework".
/// </summary>
/// <remarks>
/// <para>The command list (PS1 0x8015E260, from 0x801814B0 and 0x801814C0): Dragon Tech right after Jump, as primary
/// or secondary. Here it goes in through Two Spells' hook on the remaster's list (0x30E010, <see cref="TwoSpells.Insert"/>),
/// which works the types and usable flags out again; with nothing learned the list is empty, so the command is
/// greyed, as the PS1's usable test also asks the list builder.</para>
/// <para>The learned test (PS1 0x801818E0 in the list builder 0x801817C0): skillset 0x64 reads skillset 0x12's learned
/// bits, the Lancer's. The remaster's list builder (0x30E368) takes a job skillset's bits from +0xA2 + 3 × (skillset −
/// 5) for skillsets 5–23 and otherwise, for a skillset that isn't the unit's own, counts every ability as learned
/// (0x30E466). Its 14 bytes at 0x30E43B are rewritten in place so 0x64 gives index 13 (0x12 − 5).</para>
/// <para>ReMixed's Jump skillset in the menu (PS1 0x80155F6C, from 0x80181AAC): skillset 0x12 lists no ordinary
/// ability, so Jump's list holds Jump alone. Here the menu's list builder asks <see cref="Shows"/> through its weapon
/// stub (<see cref="WeaponRequirements"/>), which has the list's skillset. Whichever of the list the battle UI takes
/// for its Jump, it's never Lancet; the Jump range only adds up 0x18A–0x18F (<see cref="JumpRework"/>).</para>
/// <para>The AI (PS1 0x8015E8DC, from 0x8019A998 in the per-skillset candidates 0x8019A5F8): in a type 9 skillset an
/// ability below 0x170 becomes its own candidate with skillset 0x64, where Jump abilities merge into the one Jump
/// entry; ReMixed builds no Dragon Tech list of its own. The remaster's candidates (0x316D24) store each entry, then
/// at 0x317007 test for type 9 and the Jump bound (14 bytes, which <see cref="JumpRework"/> patched to 0x18B); those
/// jump to a stub that does ReMixed's. ReMixed also drops the bare Jump the list gets when no Jump ability is learned
/// (PS1 0x8019A9F4–0x8019AA14, now nops); the remaster writes that one over the last entry stored (0x3170A3), which
/// could be a Dragon Tech candidate, and its test's jne (0x317097) becomes a jmp.</para>
/// </remarks>
internal static unsafe class DragonTech
{
    public const int Jump = 0x12, Skillset = 0x64, JumpType = 9, FirstSpecial = 0x170;
    private const int JumpJob = Jump - 5;
    internal const int AiStubSize = 69;

    /// <summary>The command list puts Dragon Tech after Jump.</summary>
    internal static bool Commands { get; private set; }
    /// <summary>The menu's Jump list holds no ordinary ability.</summary>
    internal static bool JumpListOnly { get; private set; }

    // For the offline check.
    internal static nint LearnedAt { get; private set; }
    internal static nint AiAt { get; private set; }
    internal static int AiBound { get; private set; }
    internal static nint AiOther { get; private set; }
    internal static nint AiSkip { get; private set; }
    internal static nint AiNext { get; private set; }
    internal static nint BareAt { get; private set; }

    public static void Install(Game game)
    {
        var image = game.Image;
        // The list builder's learned bits: a monster has all; skillsets 5–23 a job's (index skillset − 5); 0xE0 job
        // 0xA0's; then (0x30E449) the unit's own skillset its own job's, anything else all.
        var learned = image.FindUnique(
            "F6 43 06 20 88 4C 24 22 75 ?? 41 8D 47 FB 3C 12 77 06 41 8D 47 FB EB 14 41 80 FF E0 0F 85") + 10;
        // The AI's candidates per skillset: the entry stored (skillset, ability), then type 9's Jump bound.
        var ai = image.FindUnique(
            "41 88 76 02 66 41 89 2E 41 80 FC 09 75 ?? 81 FD ?? 01 00 00 73 ?? 83 BC 24 98 00 00 00 01") + 8;
        var bound = *(int*)(ai + 8);
        if (bound is not (0x18B or 0x18F))
            throw new InvalidDataException($"the AI's Jump bound is 0x{bound:X}, not one this DLL expects");
        var other = ai + 6 + *(sbyte*)(ai + 5);
        var skip = ai + 14 + *(sbyte*)(ai + 13);
        // After the loop: type 9 with no Jump entry gets a bare one, skillset 0x12, written at [r14].
        var bare = image.FindUnique("41 80 FC 09 75 17 83 BC 24 98 00 00 00 00 75 0D 66 C1 E3 0A 66 41 89 1E 41 C6 46 02 12 8B C7");
        if ((ulong)(bare - ai) > 0x100)
            throw new InvalidDataException("the AI's bare Jump isn't in the candidates");

        if (TwoSpells.Builder == 0)
            TwoSpells.InstallCommands(game);

        GameImage.WriteCode(learned, LearnedCode);

        var stub = GameImage.AllocateCode(AiStubSize);
        WriteAiStub(stub, bound, other, skip, ai + 14);
        var jump = new byte[14];
        AbsoluteJump(jump, 0, (nint)stub);
        GameImage.WriteCode(ai, jump);
        GameImage.WriteCode(bare + 4, [0xEB]); // jne → jmp: no bare Jump

        BareAt = bare + 4;
        LearnedAt = learned;
        AiAt = ai;
        AiBound = bound;
        AiOther = other;
        AiSkip = skip;
        AiNext = ai + 14;
        Commands = true;
        JumpListOnly = true;
    }

    /// <summary>
    /// The learned test's 14 bytes: eax = skillset − 5; for 0x64 (0x5F) Jump's index instead; then a job index
    /// (0–0x12) goes on to the bits (the original's jmp target, 0x14 on), anything else falls through to 0xE0's test.
    /// </summary>
    internal static readonly byte[] LearnedCode =
    [
        0x41, 0x8D, 0x47, 0xFB,          // lea eax, [r15-5]
        0x3C, Skillset - 5,              // cmp al, 0x5F
        0x75, 0x02,                      // jne +2
        0xB0, JumpJob,                   // mov al, 13 (eax's upper bytes are 0 here)
        0x3C, 0x12,                      // cmp al, 0x12
        0x76, 0x14,                      // jbe: the bits
    ];

    /// <summary>
    /// The AI's stub: not type 9 goes on as before (<paramref name="other"/>); in type 9 an ability below 0x170
    /// becomes skillset 0x64's and goes on the same way; a Jump ability at or past <paramref name="bound"/> is left
    /// out (<paramref name="skip"/>); the rest goes on to the one-entry merge (<paramref name="next"/>). Only the
    /// flags change.
    /// </summary>
    internal static void WriteAiStub(byte* stub, int bound, nint other, nint skip, nint next)
    {
        var code = new byte[AiStubSize];
        byte[] head =
        [
            0x41, 0x80, 0xFC, JumpType,               //  0: cmp r12b, 9
            0x75, 0x0D,                               //  4: jne 19
            0x81, 0xFD, 0x70, 0x01, 0x00, 0x00,       //  6: cmp ebp, 0x170
            0x73, 0x13,                               // 12: jae 33
            0x41, 0xC6, 0x46, 0x02, Skillset,         // 14: mov byte [r14+2], 0x64
        ];
        head.CopyTo(code, 0);
        AbsoluteJump(code, 19, other);                // 19: jmp other
        code[33] = 0x81;                              // 33: cmp ebp, bound
        code[34] = 0xFD;
        BitConverter.TryWriteBytes(code.AsSpan(35), bound);
        code[39] = 0x73;                              // 39: jae 55
        code[40] = 0x0E;
        AbsoluteJump(code, 41, next);                 // 41: jmp next
        AbsoluteJump(code, 55, skip);                 // 55: jmp skip
        code.CopyTo(new Span<byte>(stub, AiStubSize));
    }

    /// <summary>jmp [rip+0] with the target after it: 14 bytes that reach anywhere.</summary>
    private static void AbsoluteJump(byte[] code, int at, nint target)
    {
        code[at] = 0xFF;
        code[at + 1] = 0x25;
        BitConverter.TryWriteBytes(code.AsSpan(at + 2), 0);
        BitConverter.TryWriteBytes(code.AsSpan(at + 6), (ulong)target);
    }

    /// <summary>Dragon Tech after each Jump in a command list (the game's entries, before the cut to six).</summary>
    internal static void AddCommands(List<int> entries)
    {
        for (var k = 0; k < entries.Count; k++)
            if (entries[k] == Jump && (k + 1 == entries.Count || entries[k + 1] != Skillset))
                entries.Insert(k + 1, Skillset);
    }

    /// <summary>Whether the menu's list builder keeps an ability: Jump's list holds no ordinary ability.</summary>
    internal static bool Shows(int skillset, int ability) =>
        !JumpListOnly || skillset != Jump || ability >= FirstSpecial;
}
