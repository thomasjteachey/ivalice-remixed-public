using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's learn-on-hit from the secondary skillset (docs/MECHANICS.md: "Learn on hit from
/// secondary"; PS1 0x8015A004, 0x8015A068). Installed by <see cref="Rules"/> as "learn-secondary".
/// </summary>
/// <remarks>
/// Vanilla: when a learnable ability lands, each unit that can learn and has it unlearned in
/// its own job's skillset rolls the ability's learn rate. ReMixed: a unit that doesn't learn it
/// there tries its secondary skillset next, with the learned bits of that skillset's job.
/// </remarks>
internal static unsafe class Learning
{
    private static Game _game = null!;
    private static delegate* unmanaged<void> _learn; // the original learn step
    private static int* _noLearning;        // non-zero: nobody learns this action (0x186AFF8)
    private static short* _ability;         // the ability being learned from (0x186AF5C)
    private static ushort* _learner;        // the scan's result, which the learn step reads (0x2FCE7C8)
    private static delegate* unmanaged<int, int> _jobIndex;         // job -> index of its learned bits (0x2B8F18)
    private static delegate* unmanaged<int, int, int> _abilityAt;   // (skillset, slot) -> ability (0x275860)
    // By battle index: the job and skillset a unit's secondary pass found the ability in (job 0 = none).
    private static readonly byte[] SecondaryJobOf = new byte[Battle.UnitCount], SecondarySkillsetOf = new byte[Battle.UnitCount];
    private static int _failed;

    /// <summary>For the offline check: the scan's entry (hooked), the ability being learned from, and the skillset lookup.</summary>
    internal static nint ScanEntry { get; private set; }
    internal static short* Ability => _ability;
    internal static int AbilityAt(int skillset, int slot) => _abilityAt(skillset, slot);

    private const int Slots = 24, LearnedBits = 0xA2; // 3 bytes per job from +0xA2 (PS1 +0x99)
    private const int CanLearn = 0x1BB;             // 2 = can learn this action, 4 = done (PS1 +0x189)

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // The scan: nobody learns if 0x186AFF8 is set; the ability; its learn-on-hit flag (common +3 & 0x20).
        var scan = image.FindUnique(
            "48 89 5C 24 08 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 20 45 33 DB 4C 8B E2 44 39 1D ?? ?? ?? ?? " +
            "0F 85 ?? ?? ?? ?? 48 0F BF 35 ?? ?? ?? ?? 66 85 F6 0F 84 ?? ?? ?? ?? 48 8D 05 ?? ?? ?? ?? 4C 8B EE F6 44 F0 03 20");
        // ... the skillset's ability at a slot, compared with the one used.
        var slot = image.FindUnique("41 8B CF 8B D7 E8 ?? ?? ?? ?? 44 0F B7 D8 0F BF C6 44 3B D8 75 ?? 48 8D 05 ?? ?? ?? ?? B9 64 00 00 00");
        // ... and its remaster exception and job index: skillset 0x1A never teaches 0x9A.
        image.Expect("44 0F B6 BB 57 FE FF FF 41 80 FF 1A 75 ?? B8 9A 00 00 00 66 3B F0 0F 84 ?? ?? ?? ?? 0F B6 8B 48 FE FF FF E8",
                     "the learn scan's skillset 0x1A exception");
        // The learn step: the learner by index, its job's learned bits, its own skillset.
        var learn = image.FindUnique(
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 40 48 8B 05 ?? ?? ?? ?? 48 33 C4 " +
            "48 89 44 24 38 0F B7 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 98 48 01 00 00 0F B6 4B 03 44 0F B6 7B 12 E8 ?? ?? ?? ??");
        // The caller: the scan's result into the learner.
        var caller = image.FindUnique("E8 ?? ?? ?? ?? 89 05 ?? ?? ?? ?? 83 F8 FF 74 ?? 0F B7 C8 E8");

        _noLearning = (int*)GameImage.Rip(scan + 26, 3, 7);
        _ability = (short*)GameImage.Rip(scan + 39, 4, 8);
        _abilityAt = (delegate* unmanaged<int, int, int>)GameImage.Rip(slot + 5, 1, 5);
        _learner = (ushort*)GameImage.Rip(learn + 39, 3, 7);
        _jobIndex = (delegate* unmanaged<int, int>)GameImage.Rip(learn + 67, 1, 5);
        if ((byte*)GameImage.Rip(scan + 56, 3, 7) != game.Battle.AbilityCommon)
            throw new InvalidDataException("the learn scan and the action setup disagree on the abilities' common records");
        if (GameImage.Rip(caller, 1, 5) != scan || (ushort*)GameImage.Rip(caller + 5, 2, 6) != _learner)
            throw new InvalidDataException("the learn scan's caller doesn't store its result where the learn step reads it");
        _ = game.TurnCode.Units;

        ScanEntry = scan;
        game.Hooks.Hook(scan, (nint)(delegate* unmanaged<int, ushort*, int>)&Scan, "the learn scan (replaced)");
        _learn = (delegate* unmanaged<void>)game.Hooks.Hook(learn, (nint)(delegate* unmanaged<void>)&Learn, "the learn step");
    }

    /// <summary>
    /// The learn scan (PS1 0x8018E6xx; 0x30CD08), rewritten with ReMixed's second pass: the
    /// first unit (not the actor) that can learn and learns the ability, by battle index, or -1.
    /// It writes the ability to <paramref name="learned"/>. The game calls it until it returns
    /// -1; a scanned unit is marked done (+0x1BB = 4) either way.
    /// </summary>
    [UnmanagedCallersOnly]
    private static int Scan(int actor, ushort* learned)
    {
        try
        {
            if (*_noLearning != 0)
                return -1;
            var ability = *_ability;
            if (ability == 0 || (_game.Battle.AbilityCommon[ability * 8 + 3] & 0x20) == 0)
                return -1;
            var turn = _game.TurnCode;
            for (var i = 0; i < Battle.UnitCount; i++)
            {
                var unit = turn.Units + i * Battle.UnitSize;
                if (i == actor || (unit[CanLearn] & 2) == 0)
                    continue;
                unit[CanLearn] = 4;
                SecondaryJobOf[i] = 0;
                if ((unit[6] & 0x20) != 0 || turn.HasStatusIn(unit, 4) != 0)
                    continue;
                if (Learns(unit, unit[3], unit[0x12], ability))
                {
                    *learned = (ushort)*_ability;
                    return i;
                }
                // ReMixed (0x8015A004): the secondary skillset next, with its job's learned bits.
                var secondary = unit[0x13];
                if (secondary != 0 && Learns(unit, (byte)SecondaryJob(secondary), secondary, ability))
                {
                    SecondaryJobOf[i] = (byte)SecondaryJob(secondary);
                    SecondarySkillsetOf[i] = secondary;
                    Tracer.Line(_game.Log, $"learning: unit {i} learns 0x{ability:X3} through its secondary skillset 0x{secondary:X2}");
                    *learned = (ushort)*_ability;
                    return i;
                }
            }
        }
        catch (Exception e)
        {
            Failed("the learn scan", e);
        }
        return -1;
    }

    /// <summary>
    /// One pass of the scan over a skillset: an unlearned slot holding the ability, then a roll
    /// of its learn rate (common +2). The remaster's own exception: skillset 0x1A never teaches 0x9A.
    /// </summary>
    private static bool Learns(byte* unit, byte job, byte skillset, int ability)
    {
        if (skillset == 0x1A && ability == 0x9A)
            return false;
        var bits = unit + LearnedBits + 3 * _jobIndex(job);
        var known = stackalloc byte[3];
        for (var k = 0; k < 3; k++)
            known[k] = bits[k];
        for (var slot = 0; slot < Slots; slot++)
        {
            if ((known[slot >> 3] & (0x80 >> (slot & 7))) != 0)
                continue;
            if ((ushort)_abilityAt(skillset, slot) != (ushort)*_ability)
                continue;
            if (_game.TurnCode.Roll(100, _game.Battle.AbilityCommon[ability * 8 + 2]) == 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The job whose learned bits a secondary skillset uses (PS1 0x8015A01C): the generic
    /// skillsets (below 25) belong to job skillset + 0x45 (Basic Skill to Squire…), and the Dark Knight's
    /// (0xE0) to the remaster's Dark Knight (0xA0), where the port puts ReMixed's (its 0x4D went to its Dancer
    /// slot and 0x4E to its Bard slot, tools/build_mod.py's DARK_KNIGHT); anything else to job 0x12, which uses
    /// Squire's bits.
    /// </summary>
    internal static int SecondaryJob(int skillset) => skillset switch
    {
        Growth.DarkKnightSkillset => Growth.DarkKnight,
        < 25 => skillset + 0x45,
        _ => 0x12,
    };

    /// <summary>
    /// The learn step (PS1 0x8018E8AC; 0x20D190), which marks the ability learned in the learner's
    /// own job. ReMixed (0x8015A068) gives it the job and skillset the scan found it in, so for a
    /// secondary learn the bit is set here; the game's step then finds nothing in the primary and
    /// posts its notification.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void Learn()
    {
        try
        {
            var i = *_learner;
            if (i < Battle.UnitCount && SecondaryJobOf[i] != 0)
            {
                MarkLearned(_game.TurnCode.Units + i * Battle.UnitSize, SecondaryJobOf[i], SecondarySkillsetOf[i]);
                SecondaryJobOf[i] = 0;
            }
        }
        catch (Exception e)
        {
            Failed("the learn step", e);
        }
        _learn();
    }

    // ---------------------------------------------------------------- the can-learn bit

    /// <summary>For the offline check: where the hit mark jumps to its stub, and the stub.</summary>
    internal static nint MarkAt { get; private set; }
    internal static nint MarkStub { get; private set; }

    /// <summary>
    /// ReMixed's hit mark (PS1 0x8018BEC8; installed with "learn-flag"): each target the action reaches gets +0x1BB
    /// = 1 (unless 0x186AF9C, PS1 0x8018F5F0, is set), and a target the action changed gets 2 later (0x30AA68),
    /// which the learn scan wants. Vanilla's 1 overwrites the byte, so a unit reached again later in the same action
    /// (a second strike that misses, say) loses its 2 and learns nothing. ReMixed keeps the 2: +0x1BB = (+0x1BB
    /// &amp; 2) | 1.
    /// </summary>
    public static void InstallMarkFix(Game game)
    {
        // cmp [0x186AF9C], ebx (0); jne; mov [rdx+0x1BB], r13b (1); then the reactions step and the 0x1C2 check.
        var check = game.Image.FindUnique("39 1D ?? ?? ?? ?? 75 07 44 88 AA BB 01 00 00 E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? B8 C2 01 00 00");
        var at = check + 8;
        var stub = GameImage.AllocateCodeNear(at, 32);
        WriteMarkStub(stub, at + 7);
        byte[] patch = [0xE9, 0, 0, 0, 0, 0x90, 0x90];
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(patch, 1);
        GameImage.WriteCode(at, patch);
        MarkAt = at;
        MarkStub = (nint)stub;
    }

    /// <summary>The stub: rdx the unit; changes the flags, then jumps to <paramref name="back"/>.</summary>
    internal static void WriteMarkStub(byte* stub, nint back)
    {
        var code = new List<byte>
        {
            0x80, 0xA2, CanLearn & 0xFF, CanLearn >> 8, 0x00, 0x00, 0x02, // and byte [rdx+0x1BB], 2
            0x80, 0x8A, CanLearn & 0xFF, CanLearn >> 8, 0x00, 0x00, 0x01, // or byte [rdx+0x1BB], 1
            0xFF, 0x25, 0, 0, 0, 0,                                     // jmp [rip+0]: back
        };
        code.AddRange(BitConverter.GetBytes((ulong)back));
        code.ToArray().CopyTo(new Span<byte>(stub, 32));
    }

    /// <summary>For the offline check: the job and skillset a unit's secondary pass found the ability in (0, 0 if none).</summary>
    internal static (int Job, int Skillset) SecondaryFound(int unit) => (SecondaryJobOf[unit], SecondarySkillsetOf[unit]);

    internal static void MarkLearned(byte* unit, byte job, byte skillset)
    {
        var bits = unit + LearnedBits + 3 * _jobIndex(job);
        for (var slot = 0; slot < Slots; slot++)
            if ((ushort)_abilityAt(skillset, slot) == (ushort)*_ability)
                bits[slot >> 3] |= (byte)(0x80 >> (slot & 7));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"learning: {what} failed: {e}");
    }
}
