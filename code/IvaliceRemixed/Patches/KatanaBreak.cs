using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's katana break for Draw Out (docs/MECHANICS.md: "Draw Out's katana break"; PS1 0x80187910, rewritten, and
/// its call at the action stage, 0x8017D120): a katana can only break for a Draw Out skillset (type 7), never for a
/// Mime or a Samurai, never with Maintenance or Adeptness, and it gets one roll per target until one breaks, none
/// without a target. Installed by <see cref="Rules"/> with "katana-break".
/// </summary>
/// <remarks>
/// <para>ReMixed takes the roll out of all four Draw Out formulas (0x20–0x23) and rolls at the action stage, up to the
/// count of targets. The remaster takes the katana out of the inventory when a player's Draw Out is confirmed (the
/// action record's step 0x281488, at 0x2817CB) and gives it back after the action unless it broke: the result's
/// application (0x30A488, at 0x30A6D4) returns it (0x30CEA4 adds one) when the attacker's own result has +0x12 &amp; 8.
/// The roll is in each formula (0x30655C), once per target: nothing for a Mime; otherwise the own result gets the katana
/// (+4) and type 8 (+2), and +0x12 = 8 ("keep") when 0x278E68(100, the weapon block's +4) passes, 0x1000 ("broken")
/// when it fails. The per-target routine (0x3099B0) clears the own result before each target (0x309C10), so after the
/// targets the action setup (0x281CE8, at 0x281FB9) sees only the last target's mark: with no target, or with 8, or
/// with neither, it writes 8 (0x281FF4, the katana comes back); with 0x1000 it leaves it (0x28200D, the katana stays
/// gone).</para>
/// <para>Two changes: a hook runs the game's roll and, for an attacker ReMixed spares, turns a "broken" back into
/// "keep", and otherwise remembers the break; and the setup's decision (0x281FD8) jumps to a stub that breaks the
/// katana (0x1000, then 0x28200D) when any target's roll broke it, as ReMixed's rolls until one breaks do, and
/// otherwise keeps it as the remaster does (0x281FF4), with no target too.</para>
/// <para>With the roll at the action stage, Murasame (formula 0x23) can break too in ReMixed, so the game's own 0x23,
/// with its roll, stays. Not ported: ReMixed's break moves the katana to a second count table (probably the Poachers'
/// Den's stock), which waits for the stolen-equipment rule. ReMixed's Bizen Boat change is in Formulas.cs.</para>
/// </remarks>
internal static unsafe class KatanaBreak
{
    private const int Samurai = 0x58, DrawOutType = 7;
    private const int Maintenance = Battle.UnitSupports + 2, MaintenanceBit = 0x04; // +0x9A & 4
    private const int Adeptness = Battle.UnitSupports + 3, AdeptnessBit = 0x02;     // +0x9B & 2 (0x1E4)
    /// <summary>The own result's +0x12 after the roll: 8 the katana comes back, 0x1000 it broke.</summary>
    internal const int Keep = 8, Broken = 0x1000;

    public const int DecisionStubSize = 96;

    private static Game _game = null!;
    private static delegate* unmanaged<void> _roll;
    private static byte* _types; // the skillset type table (.rdata 0x67E010)
    private static int _failed;

    // The decision in the action setup: the Draw Out test's tail, the "keep" path, and the no-target test after it.
    private const string Decision =
        "42 0F B6 84 1E ?? ?? ?? ?? 42 80 BC 18 ?? ?? ?? ?? 07 75 3B 42 80 BC 1E ?? ?? ?? ?? 5D 74 30 45 84 C0 74 17 " +
        "44 84 35 ?? ?? ?? ?? 75 0E B8 00 10 00 00 66 85 05 ?? ?? ?? ?? 75 19 66 44 89 35 ?? ?? ?? ?? 88 1D ?? ?? ?? ?? " +
        "88 1D ?? ?? ?? ?? 45 84 C0 74 63";
    private const int DecisionSite = 0x1F, DecisionLength = 28, KeepPath = 0x3B, BrokenPath = 0x54;

    /// <summary>
    /// The attacker (its offset from the battle's units, + 1) whose roll broke the katana in this action, or 0; the
    /// setup's stub reads and clears it. Native memory, so the stub can read it.
    /// </summary>
    internal static long* Broke { get; private set; }

    // For the offline check.
    internal static nint RollAt { get; private set; }
    internal static nint DecisionAt { get; private set; }
    internal static nint DecisionStub { get; private set; }

    public static void Install(Game game)
    {
        _game = game;
        // The roll: the attacker's job against the Mime (0x5D), the own result, the type 8 and the katana.
        var roll = game.Image.FindUnique(
            "40 53 48 83 EC 20 48 8B 05 ?? ?? ?? ?? 80 78 03 5D 74 ?? 4C 8B 1D ?? ?? ?? ?? BB 08 00 00 00 41 C6 03 01");
        if ((byte**)GameImage.Rip(roll + 6, 3, 7) != game.Battle.Attacker
            || (byte**)GameImage.Rip(roll + 19, 3, 7) != game.Routines.AttackerResult)
            throw new InvalidDataException("the katana break roll isn't the code this DLL expects");
        // The skillset type table, from the action queue's switch on it (as SkillsetTypes finds it).
        var types = game.Image.FindUnique("4C 8D 0D ?? ?? ?? ?? BB 01 00 00 00 42 0F B6 8C 08 ?? ?? ?? ?? 8D 53 06 3B CA 0F 87");
        _types = (byte*)(game.Image.ImageStart + *(int*)(types + 17));
        if ((_types[0x13] & 0x0F) != DrawOutType)
            throw new InvalidDataException("Draw Out (0x13) isn't type 7 in the skillset type table");
        // The setup's decision: its own result's mark must be the one the roll writes.
        var decision = game.Image.FindUnique(Decision);
        var mark = GameImage.Rip(decision + 0x24, 3, 7);
        if (GameImage.Rip(decision + 0x32, 3, 7) != mark || GameImage.Rip(decision + KeepPath, 4, 8) != mark
            || (byte*)(game.Image.ImageStart + *(int*)(decision + 13)) != _types)
            throw new InvalidDataException("the action setup's katana decision isn't the code this DLL expects");
        Broke = (long*)NativeMemory.AllocZeroed(8);
        var site = decision + DecisionSite;
        var stub = GameImage.AllocateCodeNear(site, DecisionStubSize);
        WriteDecisionStub(stub, (nint)Broke, mark, decision + KeepPath, decision + BrokenPath);
        var jump = new byte[DecisionLength];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (site + 5)))).CopyTo(jump, 1);
        _roll = (delegate* unmanaged<void>)game.Hooks.Hook(roll, (nint)(delegate* unmanaged<void>)&Roll, "the katana break roll");
        GameImage.WriteCode(site, jump);
        (RollAt, DecisionAt, DecisionStub) = (roll, site, (nint)stub);
    }

    /// <summary>
    /// The decision's stub, for a Draw Out by a unit that isn't a Mime (the game's tests before it): when
    /// <see cref="Broke"/> holds this unit (rsi, its index × 0x200) and there are targets (r8b, the count), the own
    /// result's mark (<paramref name="mark"/>) becomes 0x1000 and on to <paramref name="brokenPath"/>, where the katana
    /// stays out of the inventory; otherwise to <paramref name="keepPath"/>, which marks it 8 for the result to give it
    /// back. Either way the flag is cleared.
    /// </summary>
    internal static void WriteDecisionStub(byte* stub, nint broke, nint mark, nint keepPath, nint brokenPath)
    {
        var code = new List<byte>
        {
            0x50,                                   // push rax
            0x51,                                   // push rcx
            0x48, 0xB8,                             // mov rax, broke
        };
        code.AddRange(BitConverter.GetBytes((long)broke));
        code.AddRange([0x48, 0x8B, 0x08]);          // mov rcx, [rax]
        code.AddRange([0x48, 0xC7, 0x00, 0, 0, 0, 0]); // mov qword [rax], 0
        code.AddRange([0x45, 0x84, 0xC0]);          // test r8b, r8b
        code.AddRange([0x74, 0]);                   // jz keep
        var toKeep1 = code.Count;
        code.AddRange([0x48, 0x8D, 0x46, 0x01]);    // lea rax, [rsi+1]
        code.AddRange([0x48, 0x39, 0xC1]);          // cmp rcx, rax
        code.AddRange([0x75, 0]);                   // jne keep
        var toKeep2 = code.Count;
        code.Add(0x48);                             // mov rax, mark
        code.Add(0xB8);
        code.AddRange(BitConverter.GetBytes((long)mark));
        code.AddRange([0x66, 0xC7, 0x00]);          // mov word [rax], 0x1000
        code.AddRange(BitConverter.GetBytes((ushort)Broken));
        code.AddRange([0x59, 0x58]);                // pop rcx; pop rax
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);    // jmp [rip+0] → the katana stays gone
        code.AddRange(BitConverter.GetBytes((long)brokenPath));
        code[toKeep1 - 1] = checked((byte)(code.Count - toKeep1));
        code[toKeep2 - 1] = checked((byte)(code.Count - toKeep2));
        code.AddRange([0x59, 0x58]);                // keep: pop rcx; pop rax
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);    // jmp [rip+0] → the katana comes back
        code.AddRange(BitConverter.GetBytes((long)keepPath));
        if (code.Count > DecisionStubSize)
            throw new InvalidOperationException("the katana decision stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    [UnmanagedCallersOnly]
    private static void Roll()
    {
        _roll();
        try
        {
            var attacker = *_game.Battle.Attacker;
            var own = *_game.Routines.AttackerResult;
            if (attacker == null || own == null || *(ushort*)(own + 0x12) != Broken)
                return;
            if (!Spared(attacker))
            {
                *Broke = attacker - _game.TurnCode.Units + 1; // the setup's decision breaks it for this action
                Tracer.Line(_game.Log, $"katana break: the roll broke it (job 0x{attacker[3]:X2}, skillset 0x{attacker[0x1A1]:X2})");
                return;
            }
            *(ushort*)(own + 0x12) = Keep;
            Tracer.Line(_game.Log, $"katana break: none for this attacker (job 0x{attacker[3]:X2}, skillset 0x{attacker[0x1A1]:X2})");
        }
        catch (Exception e)
        {
            Failed(e);
        }
    }

    /// <summary>ReMixed spares the katana: not a Draw Out skillset, a Samurai, or Maintenance or Adeptness.</summary>
    internal static bool Spared(byte* attacker)
    {
        var skillset = attacker[0x1A1]; // the queued action's skillset
        return (_types[skillset] & 0x0F) != DrawOutType || attacker[3] == Samurai
            || (attacker[Maintenance] & MaintenanceBit) != 0 || (attacker[Adeptness] & AdeptnessBit) != 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"katana break: the roll failed: {e}");
    }
}
