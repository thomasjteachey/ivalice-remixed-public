namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's changes to the evade checks' status rules (docs/MECHANICS.md: "Evasion and statuses"). Installed by
/// <see cref="Rules"/> with "evade-checks".
/// </summary>
/// <remarks>
/// <para>
/// The target's statuses (PS1 0x801853F4; the remaster's 0x30573C, shared by the physical evade check 0x306C90, the
/// magic hit check 0x306D70 and formula 0x05's check): a target that's asleep, stopped, confused, charging or
/// performing (or Don't Act) loses its evasion, and Defending halves the hit chance. ReMixed adds Dead, and lets a
/// Transparent target keep its evasion while disabled, except against the physical check, which passes a0 = 1 for
/// it. The routine's start jumps to a stub's physical entry; the magic hit check's call goes to its other entry.
/// </para>
/// <para>
/// The attacker's statuses, in the physical check only (PS1 0x801852E4 to 0x80185328, the remaster's inline code at
/// 0x306CA8): vanilla zeroes the evasion for Concentrate and for a Transparent attacker and halves the hit chance for
/// a blind or confused attacker, which doubles the target's evasion. ReMixed: after the zeroing (Concentrate, a
/// Transparent attacker, or an ability its table marks), a Transparent target facing a visible attacker gets an
/// accessory evasion of 100 (0x8015D500), and a blind attacker adds 50 to the accessory evasion, 25 if the evasion
/// was zeroed, a confused one 25 (0x80185328). The hit chance isn't halved.
/// </para>
/// </remarks>
internal static unsafe class EvadeChecks
{
    private const int StatusStubSize = 96, AttackerStubSize = 128;
    private const int Concentrate = Battle.UnitSupports + 1; // +0x99 & 1 (0x1D5)
    private const int Statuses = Battle.UnitStatuses;        // +0x61..: PS1 +0x58..

    /// <summary>
    /// Set by the unevadable rule's evade setup hook for each evade check: 1 when the attack can't be evaded (a
    /// Transparent attacker or an ability ReMixed's table marks), so a blind attacker adds 25 rather than 50.
    /// </summary>
    internal static byte* Unevadable { get; private set; }

    // For the offline check.
    internal static nint StatusRoutine { get; private set; }
    internal static nint PhysicalEntry { get; private set; }
    internal static nint MagicEntry { get; private set; }
    internal static nint MagicCall { get; private set; }
    internal static nint AttackerAt { get; private set; }
    internal static nint AttackerStub { get; private set; }

    public static List<string> Install(Game game)
    {
        var applied = new List<string>();
        try
        {
            InstallStatuses(game);
            applied.Add("ReMixed's target statuses for evasion (Dead loses it, Transparent keeps it against magic)");
        }
        catch (Exception e)
        {
            game.Log.Error($"rules: the evade checks' target statuses not applied: {e.Message}");
        }
        try
        {
            InstallAttacker(game);
            applied.Add("ReMixed's blind, confused and Transparent rules for physical evasion");
        }
        catch (Exception e)
        {
            game.Log.Error($"rules: the evade check's attacker statuses not applied: {e.Message}");
        }
        return applied;
    }

    // ---------------------------------------------------------------- the target's statuses

    private static void InstallStatuses(Game game)
    {
        var image = game.Image;
        // mov rdx, [target]; the four disabling tests; and dword [evades], 0; Defending: shr byte [hit chance], 1; ret
        var routine = image.FindUnique(
            "48 8B 15 ?? ?? ?? ?? F6 42 65 14 0F 95 C0 F6 42 64 02 0F B6 C8 B8 01 00 00 00 0F 45 C8 F6 42 62 10 " +
            "44 0F B6 C1 44 0F 45 C0 8A 42 61 A8 09 75 05 45 84 C0 74 0A 83 25 ?? ?? ?? ?? 00 8A 42 61 A8 02 74 06 " +
            "D0 2D ?? ?? ?? ?? C3");
        var target = (byte**)GameImage.Rip(routine, 3, 7);
        var evades = (byte*)GameImage.Rip(routine + 53, 2, 7);
        var hit = (byte*)GameImage.Rip(routine + 67, 2, 6);
        if (target != game.Routines.Target || evades != (byte*)game.RuleCode.Evades || hit != evades - 2)
            throw new InvalidDataException("the status routine's target, evades or hit chance aren't the ones this DLL expects");
        // The magic hit check: a Transparent attacker zeroes the evades, then Abandon, the statuses and the roll.
        var magic = image.FindUnique(
            "48 8B 05 ?? ?? ?? ?? F6 40 63 10 74 06 89 1D ?? ?? ?? ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 0D");
        var call = magic + 24;
        if (GameImage.Rip(call, 1, 5) != routine || (byte*)GameImage.Rip(magic + 13, 2, 6) != evades)
            throw new InvalidDataException("the magic hit check doesn't call the status routine where this DLL expects");

        var stub = GameImage.AllocateCodeNear(routine, StatusStubSize);
        var physical = WriteStatusStub(stub, target, evades);
        // The routine's start jumps to the physical entry (the physical check's and formula 0x05's calls).
        byte[] jump = [0xE9, 0, 0, 0, 0, 0x90, 0x90];
        BitConverter.GetBytes(checked((int)((nint)physical - (routine + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(routine, jump);
        byte[] magicCall = [0xE8, 0, 0, 0, 0];
        BitConverter.GetBytes(checked((int)((nint)stub - (call + 5)))).CopyTo(magicCall, 1);
        GameImage.WriteCode(call, magicCall);
        StatusRoutine = routine;
        MagicEntry = (nint)stub;
        PhysicalEntry = (nint)physical;
        MagicCall = call;
    }

    /// <summary>
    /// The statuses stub: the magic entry at its start, the physical entry returned. Both work like the routine they
    /// replace (no arguments; change rax, rcx, rdx, r8 and the flags) and return.
    /// </summary>
    internal static byte* WriteStatusStub(byte* stub, byte** target, byte* evades)
    {
        var code = new List<byte>
        {
            0x41, 0xB0, 0x01,                         // magic: mov r8b, 1 (a Transparent target keeps its evasion)
            0xEB, 0x03,                               // jmp common
            0x45, 0x31, 0xC0,                         // physical: xor r8d, r8d
            0x48, 0xBA,                               // common: mov rdx, the target's slot
        };
        code.AddRange(BitConverter.GetBytes((ulong)(nint)target));
        code.AddRange([0x48, 0x8B, 0x12]);            // mov rdx, [rdx]
        code.Add(0x48); code.Add(0xB9);               // mov rcx, the evades
        code.AddRange(BitConverter.GetBytes((ulong)(nint)evades));
        var disabled = new List<int>();
        code.AddRange([0x8A, 0x42, Statuses]);        // mov al, [rdx+0x61]
        code.AddRange([0xA8, 0x29, 0x75, 0x00]);      // test al, Dead | Charging | Performing; jnz disabled
        disabled.Add(code.Count - 1);
        code.AddRange([0xF6, 0x42, Statuses + 1, 0x10, 0x75, 0x00]); // Confusion
        disabled.Add(code.Count - 1);
        code.AddRange([0xF6, 0x42, Statuses + 3, 0x02, 0x75, 0x00]); // Stop
        disabled.Add(code.Count - 1);
        code.AddRange([0xF6, 0x42, Statuses + 4, 0x14, 0x74, 0x00]); // Sleep, Don't Act; jz defending
        var toDefending = code.Count - 1;
        foreach (var k in disabled)
            code[k] = (byte)(code.Count - (k + 1));
        code.AddRange([0x45, 0x84, 0xC0, 0x74, 0x06]); // disabled: test r8b, r8b; jz zero
        code.AddRange([0xF6, 0x42, Statuses + 2, 0x10, 0x75, 0x06]); // Transparent (magic only): keep; jnz defending
        code.AddRange([0xC7, 0x01, 0x00, 0x00, 0x00, 0x00]);         // zero: mov dword [rcx], 0
        code[toDefending] = (byte)(code.Count - (toDefending + 1));
        code.AddRange([0xA8, 0x02, 0x74, 0x03]);      // defending: test al, Defending; jz done
        code.AddRange([0xD0, 0x69, 0xFE]);            // shr byte [rcx-2], 1 (the hit chance)
        code.Add(0xC3);                               // done: ret
        if (code.Count > StatusStubSize)
            throw new InvalidOperationException("the evade statuses stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, StatusStubSize));
        return stub + 5;
    }

    // ---------------------------------------------------------------- the attacker's statuses

    private static void InstallAttacker(Game game)
    {
        // The physical check: the evade setup; mov rax, [attacker]; xor ebx, ebx; Concentrate zeroes the evades;
        // Darkness or Confusion halves the hit chance; a Transparent attacker zeroes the evades; then Abandon.
        var check = game.Image.FindUnique(
            "E8 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 33 DB F6 80 99 00 00 00 01 74 06 89 1D ?? ?? ?? ?? F6 40 62 30 74 06 " +
            "D0 2D ?? ?? ?? ?? F6 40 63 10 74 06 89 1D ?? ?? ?? ?? E8");
        var evades = (byte*)GameImage.Rip(check + 23, 2, 6);
        if ((byte**)GameImage.Rip(check + 5, 3, 7) != game.Battle.Attacker || evades != (byte*)game.RuleCode.Evades
            || (byte*)GameImage.Rip(check + 47, 2, 6) != evades || (byte*)GameImage.Rip(check + 35, 2, 6) != evades - 2)
            throw new InvalidDataException("the physical evade check's globals aren't the ones this DLL expects");
        var at = check + 14;
        var back = check + 53;
        Unevadable = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
        var stub = GameImage.AllocateCodeNear(at, AttackerStubSize);
        WriteAttackerStub(stub, back, Unevadable, evades, game.Routines.Target);
        var patch = Enumerable.Repeat((byte)0x90, (int)(back - at)).ToArray();
        patch[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(patch, 1);
        GameImage.WriteCode(at, patch);
        AttackerAt = at;
        AttackerStub = (nint)stub;
    }

    /// <summary>
    /// The attacker stub: rax the attacker (ebx stays 0, as the check needs); changes rcx, rdx, r8, r9 and the flags,
    /// then jumps to <paramref name="back"/>.
    /// </summary>
    internal static void WriteAttackerStub(byte* stub, nint back, byte* unevadable, byte* evades, byte** target)
    {
        var code = new List<byte> { 0x48, 0xBA };     // mov rdx, the unevadable flag
        code.AddRange(BitConverter.GetBytes((ulong)(nint)unevadable));
        code.AddRange([0x8A, 0x0A]);                  // mov cl, [rdx]
        code.AddRange([0xF6, 0x80]);                  // test byte [rax+Concentrate], 1
        code.AddRange(BitConverter.GetBytes(Concentrate));
        code.AddRange([0x01, 0x74, 0x02, 0xB1, 0x01]); // jz; mov cl, 1
        code.AddRange([0xF6, 0x40, Statuses + 2, 0x10, 0x74, 0x02, 0xB1, 0x01]); // a Transparent attacker: mov cl, 1
        code.AddRange([0x48, 0xBA]);                  // mov rdx, the evades
        code.AddRange(BitConverter.GetBytes((ulong)(nint)evades));
        code.AddRange([0x84, 0xC9, 0x74, 0x06]);      // test cl, cl; jz
        code.AddRange([0xC7, 0x02, 0x00, 0x00, 0x00, 0x00]); // mov dword [rdx], 0: no evasion
        // A Transparent target facing a visible attacker: accessory evasion 100 (ReMixed's 0x8015D500).
        code.AddRange([0xF6, 0x40, Statuses + 2, 0x10, 0x75, 0x00]); // test byte [rax+0x63], 0x10; jnz dark
        var toDark = code.Count - 1;
        code.AddRange([0x49, 0xB8]);                  // mov r8, the target's slot
        code.AddRange(BitConverter.GetBytes((ulong)(nint)target));
        code.AddRange([0x4D, 0x8B, 0x00]);            // mov r8, [r8]
        code.AddRange([0x41, 0xF6, 0x40, Statuses + 2, 0x10, 0x74, 0x03]); // test byte [r8+0x63], 0x10; jz dark
        code.AddRange([0xC6, 0x02, 0x64]);            // mov byte [rdx], 100
        code[toDark] = (byte)(code.Count - (toDark + 1));
        // Darkness: +50, or +25 when the evasion was zeroed; Confusion alone: +25 (ReMixed's 0x80185328).
        code.AddRange([0x44, 0x8A, 0x40, Statuses + 1]); // dark: mov r8b, [rax+0x62]
        code.AddRange([0x41, 0xF6, 0xC0, 0x20, 0x74, 0x00]); // test r8b, Darkness; jz confused
        var toConfused = code.Count - 1;
        code.AddRange([0x41, 0xB1, 50, 0x84, 0xC9, 0x74, 0x00]); // mov r9b, 50; test cl, cl; jz add
        var toAdd = code.Count - 1;
        code.AddRange([0x41, 0xB1, 25, 0xEB, 0x00]);  // mov r9b, 25; jmp add
        var jumpToAdd = code.Count - 1;
        code[toConfused] = (byte)(code.Count - (toConfused + 1));
        code.AddRange([0x41, 0xF6, 0xC0, 0x10, 0x74, 0x06]); // confused: test r8b, Confusion; jz done
        code.AddRange([0x41, 0xB1, 25]);              // mov r9b, 25
        code[toAdd] = (byte)(code.Count - (toAdd + 1));
        code[jumpToAdd] = (byte)(code.Count - (jumpToAdd + 1));
        code.AddRange([0x44, 0x00, 0x0A]);            // add: add [rdx], r9b
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);      // done: jmp [rip+0]: back
        code.AddRange(BitConverter.GetBytes((ulong)back));
        if (code.Count > AttackerStubSize)
            throw new InvalidOperationException("the evade check's attacker stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, AttackerStubSize));
    }
}
