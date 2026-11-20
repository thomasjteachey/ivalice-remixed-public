namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Mighty Sword, formula 0x2E (docs/MECHANICS.md: "Mighty Sword always deals damage"; PS1 0x80189B94,
/// rewritten): past the evade check its strike always deals its damage (XA = PA, YA = WP), and breaks the item unless
/// there's nothing to break or the target has Maintenance. Installed by <see cref="Formulas"/> for 0x2E; its table
/// entry stays the game's. Meliadoul's Shellbust Stab, Blastar Punch, Hellcry Punch and Icewolf Bite (0x0A0–0x0A3);
/// the Knight's Breaks are formula 0x25 (Formulas.cs).
/// </summary>
/// <remarks>
/// <para>Vanilla PS1 missed with nothing to break and with Maintenance. The remaster's 0x2E (0x308538) halves the
/// damage with nothing to break (a second slot pick, then XA × 50 / 100) and misses in its Maintenance step (0x30647C)
/// with Maintenance, and also, clearing the slot and item, for the items 0x101–0x104. Two changes: the halving's je
/// becomes a jmp, and the step's call jumps to a stub. For a target with Maintenance or one of those items the stub
/// clears the result's slot (+0x1B) and item (+4) and goes on to the damage without the break mark (+0x12 = 4), as
/// ReMixed does with Maintenance; otherwise the game's step runs as before (it then does nothing).</para>
/// <para>ReMixed's 0x2E ends in its common tail past the Y test (0x8018BAF8), which clears the action's status bytes
/// (0x80193860): its data puts the slot (1 weapon, 2 shield, 3 head, 4 body) in the Breaks' status byte. The remaster
/// picks the slot by the ability's id (0x3065C4) but takes the byte as the action's status set, shown as added
/// effects and inflicted by a Rend's plain attack (a player's report, 2026-11-19), so the build keeps vanilla's byte
/// for these (tools/build_mod.py SLOT_STATUS_BYTES).</para>
/// </remarks>
internal static unsafe class Breaks
{
    public const int StubSize = 128;
    private const int Maintenance = Battle.UnitSupports + 2, MaintenanceBit = 0x04; // +0x9A & 4 (0x1DB)
    private const int ResultSlot = 0x1B, ResultItem = 0x04;

    // Formula 0x2E, from its entry to the damage path's XA = PA: the evade check and its miss, the slot pick, the
    // nothing-to-break path (XA = PA, YA = WP, the second pick, the halving), the Maintenance step and the break mark.
    private const string Formula =
        "48 83 EC 28 E8 ?? ?? ?? ?? 85 C0 74 2B 48 8B 0D ?? ?? ?? ?? 80 39 00 0F 85 ?? ?? ?? ?? 83 3D ?? ?? ?? ?? 00 " +
        "0F 85 ?? ?? ?? ?? B8 00 20 00 00 66 09 41 12 E9 ?? ?? ?? ?? " +
        "E8 ?? ?? ?? ?? 85 C0 74 4D 48 8B 05 ?? ?? ?? ?? 0F B6 48 3E 0F B6 05 ?? ?? ?? ?? 66 89 05 ?? ?? ?? ?? " +
        "66 89 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 85 C0 74 75 0F BF 05 ?? ?? ?? ?? 6B C8 32 B8 1F 85 EB 51 F7 E9 " +
        "C1 FA 05 8B C2 C1 E8 1F 03 D0 66 89 15 ?? ?? ?? ?? EB 51 " +
        "E8 ?? ?? ?? ?? 4C 8B 15 ?? ?? ?? ?? 41 80 3A 00 75 15 83 3D ?? ?? ?? ?? 00 75 7D B8 00 20 00 00 " +
        "66 41 09 42 12 EB 71 B8 04 00 00 00 66 41 89 42 12 48 8B 05 ?? ?? ?? ?? 0F B6 48 3E";
    private const int HalvingJump = 0x68, StepCall = 0x8E, AfterStep = 0x93, DamagePath = 0xBF;

    // The Maintenance step: the target's Maintenance (+0x9A & 4), the result's item against 0x101–0x104.
    private const string Step =
        "48 8B 05 ?? ?? ?? ?? 45 33 C9 48 8B 15 ?? ?? ?? ?? 45 0F B6 C1 F6 80 9A 00 00 00 04 41 8D 41 01 " +
        "0F B7 4A 04 44 0F 45 C0 81 E9 01 01 00 00 74 13 2B C8 74 0F 2B C8 74 0B 3B C8 74 07";

    /// <summary>The halving's jump and the step's call in the game's 0x2E, and the stub; for the offline check.</summary>
    internal static nint HalvingAt { get; private set; }
    internal static nint StepCallAt { get; private set; }
    internal static nint Stub { get; private set; }
    internal static nint StepRoutine { get; private set; }

    public static void Install(nint formula, Routines r, Battle battle)
    {
        var calls = GameImage.Calls(formula, Formula, "formula 0x2E");
        // evade, the miss's jump to the epilogue, the slot pick twice, the Maintenance step
        if (calls[0] != (nint)r.Evade || calls[2] != calls[3])
            throw new InvalidDataException("formula 0x2E's evade check or slot pick isn't the one this DLL expects");
        if ((byte**)GameImage.Rip(formula + 13, 3, 7) != battle.TargetResult
            || (byte**)GameImage.Rip(formula + AfterStep, 3, 7) != battle.TargetResult)
            throw new InvalidDataException("formula 0x2E's result isn't the one this DLL expects");
        var step = calls[4];
        GameImage.Calls(step, Step, "the Maintenance step");
        if ((byte**)GameImage.Rip(step, 3, 7) != r.Target || (byte**)GameImage.Rip(step + 10, 3, 7) != battle.TargetResult)
            throw new InvalidDataException("the Maintenance step's target or result isn't the one this DLL expects");

        var stub = GameImage.AllocateCodeNear(formula, StubSize);
        WriteStub(stub, (nint)r.Target, (nint)battle.TargetResult, step, formula + AfterStep, formula + DamagePath);
        var site = formula + StepCall;
        byte[] jump = [0xE9, 0, 0, 0, 0];
        BitConverter.GetBytes(checked((int)((nint)stub - (site + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(site, jump);
        GameImage.WriteCode(formula + HalvingJump, [0xEB]); // je → jmp: nothing to break deals the full damage
        HalvingAt = formula + HalvingJump;
        StepCallAt = site;
        Stub = (nint)stub;
        StepRoutine = step;
    }

    /// <summary>
    /// The step's stub: Maintenance or an item the step spares → clear the slot and item, then <paramref name="damage"/>;
    /// otherwise call <paramref name="step"/> and go on at <paramref name="back"/>. Entered by a jmp from the call site,
    /// so the call keeps the formula's stack as the game's did. Written again against landing pads by the offline check.
    /// </summary>
    internal static void WriteStub(byte* stub, nint target, nint result, nint step, nint back, nint damage)
    {
        var code = new List<byte>();
        void Imm(nint value) => code.AddRange(BitConverter.GetBytes((long)value));
        void Load(nint global)
        {
            code.AddRange([0x48, 0xB8]); Imm(global);                 // mov rax, &global
            code.AddRange([0x48, 0x8B, 0x00]);                        // mov rax, [rax]
        }
        void Jump(nint to)
        {
            code.AddRange([0xFF, 0x25, 0, 0, 0, 0]); Imm(to);          // jmp [rip+0]
        }
        Load(target);
        code.AddRange([0xF6, 0x80, Maintenance, 0, 0, 0, MaintenanceBit]); // test byte [rax+0x9A], 4
        code.AddRange([0x75, 0]);                                      // jnz spare
        var toSpare1 = code.Count;
        Load(result);
        code.AddRange([0x0F, 0xB7, 0x48, ResultItem]);                 // movzx ecx, word [rax+4]
        code.AddRange([0x81, 0xE9, 0x01, 0x01, 0, 0]);                 // sub ecx, 0x101
        code.AddRange([0x83, 0xF9, 0x03]);                             // cmp ecx, 3
        code.AddRange([0x76, 0]);                                      // jbe spare
        var toSpare2 = code.Count;
        code.AddRange([0x48, 0xB8]); Imm(step);                        // mov rax, step
        code.AddRange([0xFF, 0xD0]);                                   // call rax
        Jump(back);
        var spare = code.Count;
        Load(result);
        code.AddRange([0xC6, 0x40, ResultSlot, 0]);                    // mov byte [rax+0x1B], 0
        code.AddRange([0x66, 0xC7, 0x40, ResultItem, 0, 0]);           // mov word [rax+4], 0
        Jump(damage);
        code[toSpare1 - 1] = checked((byte)(spare - toSpare1));
        code[toSpare2 - 1] = checked((byte)(spare - toSpare2));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the Breaks' stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
