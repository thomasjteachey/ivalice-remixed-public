namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's in-place changes to four steps the formulas and reactions share (docs/MECHANICS.md: "Magic damage and
/// statuses", "Phoenix Down and vampires", "Composure", "Banditry's steal bonus"). Installed by <see cref="Rules"/>
/// with "formula-steps"; each part on its own, so one that doesn't match leaves the others.
/// </summary>
internal static unsafe class FormulaSteps
{
    private const int St = Battle.UnitStatuses;               // +0x61..: PS1 +0x58..
    private const int Banditry = Battle.UnitSupports + 1;      // +0x99 & 0x40 (0x1CF)
    public const int Composure = 0x1C0;                        // vanilla Finger Guard

    // For the offline check.
    internal static nint MagicModAt { get; private set; }
    internal static nint MagicModStub { get; private set; }
    internal static nint PhoenixAt { get; private set; }
    internal static nint PhoenixStub { get; private set; }
    internal static nint ComposureAt { get; private set; }
    internal static nint ComposureStub { get; private set; }
    internal static short* ReactionId { get; private set; }
    internal static nint StealAt { get; private set; }
    internal static nint StealStub { get; private set; }
    internal static nint StealExpAt { get; private set; }
    internal static nint StealExpStub { get; private set; }

    public static List<string> Install(Game game)
    {
        var applied = new List<string>();
        void Part(string name, Action install)
        {
            try
            {
                install();
                applied.Add(name);
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: {name} not applied: {e.Message}");
            }
        }
        Part("magic damage: a Transparent target counts as Shelled, and Defending takes a third off", () => InstallMagicMod(game));
        Part("Phoenix Down treats a Blood-Sucked unit like an Undead one, and neither when it's an Immortal story unit", () => InstallPhoenix(game));
        Part("Composure (was Finger Guard) gives 30 CT when it triggers", () => InstallComposure(game));
        Part("an item or EXP steal's chance: X doubled for a reaction (Counter Steal), 10 more with Banditry", () => InstallSteal(game));
        return applied;
    }

    private static void JumpTo(nint at, byte* stub, int length)
    {
        var patch = Enumerable.Repeat((byte)0x90, length).ToArray();
        patch[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(patch, 1);
        GameImage.WriteCode(at, patch);
    }

    private static void AddJump(List<byte> code, nint to)
    {
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);   // jmp [rip+0]
        code.AddRange(BitConverter.GetBytes((ulong)to));
    }

    private static void Copy(List<byte> code, byte* stub, int size, string what)
    {
        if (code.Count > size)
            throw new InvalidOperationException($"the {what} stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, size));
    }

    // ---------------------------------------------------------------- the magic modifier

    /// <summary>
    /// The target's statuses against magic (PS1 0x80186460; the remaster's 0x305E64, the formulas' third magic
    /// step): Shell takes a third off XA and Chicken or Frog adds half. ReMixed: Shell or Transparent takes the third,
    /// then Defending takes another third, then Chicken or Frog. The routine's start jumps to a stub that does all of
    /// it, a leaf like the routine (changes rax, rcx, rdx, r8, r9 and the flags).
    /// </summary>
    private static void InstallMagicMod(Game game)
    {
        var routine = (nint)game.Routines.MagicMod3;
        GameImage.Calls(routine,
            "4C 8B 05 ?? ?? ?? ?? 41 F6 40 64 10 74 20 0F BF 0D ?? ?? ?? ?? B8 56 55 55 55 03 C9 F7 E9 8B C2 C1 E8 1F " +
            "03 D0 66 89 15 ?? ?? ?? ?? EB 07 0F B7 15 ?? ?? ?? ?? 41 F6 40 63 06 74 12", "the third magic step");
        if ((byte**)GameImage.Rip(routine, 3, 7) != game.Routines.Target || (short*)GameImage.Rip(routine + 14, 3, 7) != game.Battle.XA)
            throw new InvalidDataException("the third magic step's target or XA isn't the one this DLL expects");
        var stub = GameImage.AllocateCodeNear(routine, 128);
        WriteMagicModStub(stub, game.Routines.Target, game.Battle.XA);
        JumpTo(routine, stub, 7);
        MagicModAt = routine;
        MagicModStub = (nint)stub;
    }

    internal static void WriteMagicModStub(byte* stub, byte** target, short* xa)
    {
        var code = new List<byte> { 0x49, 0xB8 };                    // mov r8, the target's slot
        code.AddRange(BitConverter.GetBytes((ulong)(nint)target));
        code.AddRange([0x4D, 0x8B, 0x00]);                           // mov r8, [r8]
        code.Add(0x48); code.Add(0xB9);                              // mov rcx, XA's address
        code.AddRange(BitConverter.GetBytes((ulong)(nint)xa));
        code.AddRange([0x0F, 0xBF, 0x01]);                           // movsx eax, word [rcx]
        byte[] third =                                               // eax = eax × 2 / 3 (toward zero), into XA
        [
            0x44, 0x8D, 0x0C, 0x00,                                  // lea r9d, [rax+rax]
            0xB8, 0x56, 0x55, 0x55, 0x55,                            // mov eax, 0x55555556
            0x41, 0xF7, 0xE9,                                        // imul r9d
            0x89, 0xD0,                                              // mov eax, edx
            0xC1, 0xE8, 0x1F,                                        // shr eax, 31
            0x01, 0xD0,                                              // add eax, edx
            0x66, 0x89, 0x01,                                        // mov [rcx], ax
        ];
        code.AddRange([0x41, 0xF6, 0x40, St + 3, 0x10, 0x75, 0x07]); // Shell: jnz shell
        code.AddRange([0x41, 0xF6, 0x40, St + 2, 0x10, 0x74, (byte)third.Length]); // Transparent; jz defending
        code.AddRange(third);                                         // shell:
        code.AddRange([0x41, 0xF6, 0x40, St, 0x02, 0x74, (byte)third.Length]); // defending: Defending; jz chicken
        code.AddRange(third);
        byte[] half =                                                // eax = eax × 3 / 2 (toward zero), into XA
        [
            0x8D, 0x04, 0x40,                                        // lea eax, [rax+rax*2]
            0x99,                                                    // cdq
            0x29, 0xD0,                                              // sub eax, edx
            0xD1, 0xF8,                                              // sar eax, 1
            0x66, 0x89, 0x01,                                        // mov [rcx], ax
        ];
        code.AddRange([0x41, 0xF6, 0x40, St + 2, 0x06, 0x74, (byte)half.Length]); // chicken: Chicken or Frog; jz done
        code.AddRange(half);
        code.Add(0xC3);                                              // done: ret
        Copy(code, stub, 128, "third magic step");
    }

    // ---------------------------------------------------------------- Phoenix Down

    /// <summary>
    /// Phoenix Down's formula (0x4B; PS1 0x8018A2EC, rewritten in place; the remaster's 0x308C98): vanilla takes an
    /// Undead target's HP as damage and revives anyone else with random HP. ReMixed takes the HP of an Undead or
    /// Blood-Sucked target unless it's an Immortal story unit (+5 &amp; 5 is 5), which is revived. The heal step after
    /// it (0x306378) only keeps the damage for the Undead, so a vampire that isn't Undead gets that much back as HP,
    /// in ReMixed too.
    /// </summary>
    private static void InstallPhoenix(Game game)
    {
        // The item hit check; rax = [target]; test byte [rax+0x61], 0x10 (Undead); je revive; HP (+0x30) into damage.
        var start = game.Image.FindUnique(
            "48 83 EC 28 E8 ?? ?? ?? ?? 85 C0 74 ?? 48 8B 05 ?? ?? ?? ?? F6 40 61 10 74 11 0F B7 48 30 48 8B 05 ?? ?? ?? ?? 66 89 48 06 EB");
        if ((byte**)GameImage.Rip(start + 13, 3, 7) != game.Routines.Target)
            throw new InvalidDataException("Phoenix Down's target isn't the one this DLL expects");
        var at = start + 20;
        var stub = GameImage.AllocateCodeNear(at, 64);
        WritePhoenixStub(stub, kill: at + 6, revive: at + 6 + 0x11);
        JumpTo(at, stub, 6);
        PhoenixAt = at;
        PhoenixStub = (nint)stub;
    }

    /// <summary>The stub: rax the target; changes cl and the flags.</summary>
    internal static void WritePhoenixStub(byte* stub, nint kill, nint revive)
    {
        var code = new List<byte>
        {
            0xF6, 0x40, St, 0x10, 0x75, 0x06,           // Undead: jnz vampire
            0xF6, 0x40, St + 1, 0x04, 0x74, 0x00,       // Blood Suck; jz revive
        };
        var toRevive = code.Count - 1;
        code.AddRange([0x8A, 0x48, 0x05, 0x80, 0xE1, 0x05, 0x80, 0xF9, 0x05, 0x74, 0x00]); // vampire: cl = +5 & 5; cmp 5; je revive
        var toRevive2 = code.Count - 1;
        AddJump(code, kill);
        code[toRevive] = (byte)(code.Count - (toRevive + 1));
        code[toRevive2] = (byte)(code.Count - (toRevive2 + 1));
        AddJump(code, revive);                          // revive:
        Copy(code, stub, 64, "Phoenix Down");
    }

    // ---------------------------------------------------------------- Composure

    /// <summary>
    /// The reaction dispatcher (PS1 0x8018AF2C; the remaster's 0x3095E4) has no case for 0x1C0, vanilla's Finger Guard,
    /// whose block only shows. ReMixed's Composure (0x80188360, from the switch's default) raises the reactor's CT by
    /// 30 and finishes like the other cases. The remaster's default (0x309795) jumps to a stub with that case.
    /// </summary>
    private static void InstallComposure(Game game)
    {
        var image = game.Image;
        // The reaction id, then the compare chain's start (0x1AE, then 0x1A6 on).
        var chain = image.FindUnique("0F BF 0D ?? ?? ?? ?? 3B C8 0F 8F ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 81 E9 A6 01 00 00");
        // The chain's last case (0x1BE) and the default: the result's flags from sil, -1, to the end.
        var last = image.FindUnique("83 F9 02 74 13 48 8B 05 ?? ?? ?? ?? 40 88 70 27 83 C8 FF E9");
        // The common tail: mov cl, 1; mov [rbx+0x27], dil; call apply; mov rcx, [result]; cmp [rcx+0x12], si.
        var tail = image.FindUnique("B1 01 40 88 7B 27 E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 66 39 71 12");
        var at = last + 5;
        var end = GameImage.Rip(at + 14, 1, 5);
        if ((ulong)(at - chain) > 0x200 || (ulong)(tail - at) > 0x200 || (ulong)(end - tail) > 0x100
            || (byte**)GameImage.Rip(at, 3, 7) != game.Battle.TargetResult)
            throw new InvalidDataException("the reaction dispatcher isn't laid out as this DLL expects");
        ReactionId = (short*)GameImage.Rip(chain, 3, 7);
        var stub = GameImage.AllocateCodeNear(at, 96);
        WriteComposureStub(stub, ReactionId, game.Battle.TargetResult, tail, end);
        JumpTo(at, stub, 5);
        ComposureAt = at;
        ComposureStub = (nint)stub;
    }

    /// <summary>The stub: esi 0 and dil 1 as the dispatcher has them; changes rax, rcx, rbx (saved by it) and the flags.</summary>
    internal static void WriteComposureStub(byte* stub, short* reaction, byte** result, nint tail, nint end)
    {
        var code = new List<byte> { 0x48, 0xB9 };      // mov rcx, the reaction id's address
        code.AddRange(BitConverter.GetBytes((ulong)(nint)reaction));
        code.AddRange([0x0F, 0xBF, 0x09]);             // movsx ecx, word [rcx]
        code.AddRange([0x81, 0xF9]);                   // cmp ecx, 0x1C0
        code.AddRange(BitConverter.GetBytes(Composure));
        code.AddRange([0x75, 0x00]);                   // jne default
        var toDefault = code.Count - 1;
        code.AddRange([0x48, 0xBB]);                   // mov rbx, the result's slot
        code.AddRange(BitConverter.GetBytes((ulong)(nint)result));
        code.AddRange([0x48, 0x8B, 0x1B]);             // mov rbx, [rbx]
        code.AddRange([0xC6, 0x43, 0x15, 0x80 | 30]);  // the CT change: raise by 30
        AddJump(code, tail);
        code[toDefault] = (byte)(code.Count - (toDefault + 1));
        code.AddRange([0x48, 0xB8]);                   // default: mov rax, the result's slot
        code.AddRange(BitConverter.GetBytes((ulong)(nint)result));
        code.AddRange([0x48, 0x8B, 0x00]);             // mov rax, [rax]
        code.AddRange([0x40, 0x88, 0x70, 0x27]);       // mov [rax+0x27], sil
        code.AddRange([0x83, 0xC8, 0xFF]);             // or eax, -1
        AddJump(code, end);
        Copy(code, stub, 96, "Composure");
    }

    // ---------------------------------------------------------------- Banditry and steals

    public const int StealStubSize = 64;

    /// <summary>
    /// The item and EXP steals' hit chance (formulas 0x26 and 0x28; PS1 0x801897D8 and 0x80189888 now call ReMixed's
    /// 0x80189828, which formula 0x27's Gil Taking uses too): XA = Speed, YA = X, with X doubled for a steal made as a
    /// reaction (ReMixed's test is the battle loop's after-action phase, 0x80096214 = 2: Counter Steal, "at an improved
    /// hit rate"; here the stealer isn't the unit whose turn it is, +0x1B8), then plus 10 with Banditry (0x1CF, the
    /// Thief's innate). Each formula's YA load (the remaster's 0x308044 and 0x30813D) jumps to a stub.
    /// </summary>
    private static void InstallSteal(Game game)
    {
        // Formula 0x26: the slot pick; on none, the null step and the miss marker; then the evade check, and XA, YA.
        var items = game.Image.FindUnique(
            "48 83 EC 28 E8 ?? ?? ?? ?? 85 C0 74 37 E8 ?? ?? ?? ?? 4C 8B 05 ?? ?? ?? ?? 41 80 38 00 41 C6 40 02 07");
        // Formula 0x28 (Steal Exp): the evade check and its miss marker, then XA = Speed, YA = X.
        var exp = game.Image.FindUnique(
            "40 53 48 83 EC 20 E8 ?? ?? ?? ?? 33 DB 85 C0 74 29 48 8B 0D ?? ?? ?? ?? 38 19 0F 85 ?? ?? ?? ?? " +
            "39 1D ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? B8 00 20 00 00 66 09 41 12 E9 ?? ?? ?? ?? " +
            "48 8B 05 ?? ?? ?? ?? 0F B6 48 40 0F B6 05 ?? ?? ?? ?? 66 89 05");
        if (GameImage.Rip(exp + 6, 1, 5) != (nint)game.Routines.Evade)
            throw new InvalidDataException("Steal Exp's evade check isn't the one this DLL expects");
        (StealAt, StealStub) = PatchSteal(game, items + 0x78, "the item steal's XA and YA");
        (StealExpAt, StealExpStub) = PatchSteal(game, exp + 0x45, "Steal Exp's XA and YA");
    }

    private static (nint At, nint Stub) PatchSteal(Game game, nint at, string what)
    {
        GameImage.Calls(at - 11, "48 8B 05 ?? ?? ?? ?? 0F B6 48 40 0F B6 05 ?? ?? ?? ?? 66 89 05", what);
        var x = (byte*)GameImage.Rip(at, 3, 7);
        if ((byte**)GameImage.Rip(at - 11, 3, 7) != game.Battle.Attacker || x != game.Battle.Ability + 9)
            throw new InvalidDataException($"{what}: the attacker or X isn't the one this DLL expects");
        var stub = GameImage.AllocateCodeNear(at, StealStubSize);
        WriteStealStub(stub, x, at + 7);
        JumpTo(at, stub, 7);
        return (at, (nint)stub);
    }

    /// <summary>
    /// The stub: rax the attacker; eax = X, doubled when the attacker isn't the unit whose turn it is (a reaction), then
    /// plus 10 with Banditry; changes rdx and the flags.
    /// </summary>
    internal static void WriteStealStub(byte* stub, byte* x, nint back)
    {
        var code = new List<byte> { 0x48, 0xBA };      // mov rdx, X's address
        code.AddRange(BitConverter.GetBytes((ulong)(nint)x));
        code.AddRange([0x0F, 0xB6, 0x12]);             // movzx edx, byte [rdx]
        code.AddRange([0x80, 0xB8]);                   // cmp byte [rax+0x1B8], 0 (the unit's turn)
        code.AddRange(BitConverter.GetBytes(Battle.UnitTurn));
        code.Add(0x00);
        code.AddRange([0x75, 0x02, 0x01, 0xD2]);       // jne; add edx, edx (a reaction: X doubled)
        code.AddRange([0xF6, 0x80]);                   // test byte [rax+0x99], 0x40 (Banditry)
        code.AddRange(BitConverter.GetBytes(Banditry));
        code.Add(0x40);
        code.AddRange([0x74, 0x03, 0x83, 0xC2, 10]);   // jz; add edx, 10
        code.AddRange([0x89, 0xD0]);                   // mov eax, edx
        AddJump(code, back);
        Copy(code, stub, StealStubSize, "steal");
    }
}
