using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's persistent performances (The Lion War's hack; docs/MECHANICS.md, "Persistent performances";
/// docs/analysis/battle-80150000.md §17): a unit that's performing (a Bard's song, a Dancer's dance) and takes an
/// instant action (an item, Attack, Throw, an ability that doesn't charge) goes on performing: the song's record and
/// countdown are kept through the action and given back after it. Math Skill ends the performance, and an action that
/// charges (another song, a spell, a Jump) replaces it, as in vanilla. The remaster's turn-start notice for a performer
/// ("The unit is performing. Using another ability will cancel the performance.") goes, as ReMixed took its own out
/// (BATTLE 0x80140BE0). Installed by <see cref="Rules"/> with "persistent-performances".
/// </summary>
/// <remarks>
/// <para>ReMixed saves the unit's record and countdown (PS1 +0x15D) at each queue commit (0x80151200, at 0x8017C494),
/// keeps Performing through the action-state reset after an instant action (SCUS 0x8005E804, mask 0xE), and after the
/// action's last strike (0x80151220, at 0x80077854) copies the saved record and countdown back over an instant action's,
/// or clears Performing for Math Skill (skillset 0x15). Its save slot serves every unit; here each unit has its own, as
/// <see cref="RidingActions"/> parks a rider's charge.</para>
/// <para>In the remaster the queue (0x281434) calls the inner queue (0x281488, hooked by <see cref="Rules"/>: here
/// <see cref="BeforeQueue"/> and <see cref="AfterQueue"/>), which copies the new record over the unit's (+0x1A0); for an
/// instant action (it returns 1) the queue then resets the action state (0x279370 with 0), which clears Performing and
/// the countdown (+0x18D = 0xFF). That call goes to <see cref="QueueReset"/>, which for a kept song leaves Performing
/// and only empties the countdown, so nothing fires from the instant action's record (Performing, Charging, Jump and
/// Defending exclude each other, so there's nothing else for the reset to clear). The action's start (0x2828E4, through
/// <see cref="TwoSpells.StartAction"/>) reloads a Performing unit's countdown from the record it runs, which for an item
/// has no ability data; for a kept song it's skipped. The song goes back when the battle's step next runs the turn loop
/// (0x30F030, from 0x2029DC: once the turn and its action are over, before any countdown), with its countdown (+0x18D)
/// and its full charge (+0x1BD, which ReMixed works out again with 0x8005E778) as they were, unless the unit stopped
/// Performing meanwhile (a status; Defend and Equip Change reset the state themselves), died or left. ReMixed gives it
/// back as the action's display ends, so here a unit that acts and then moves shows its song again only as its turn
/// ends; nothing ticks in between.</para>
/// <para>Nothing runs while the game simulates (the AI's planning) or for a reaction (0x186AF9C), which keeps the unit's
/// record itself. Not ported: ReMixed's Equip Change keeps the status with the countdown emptied (its reset is the
/// same state 0), so its song never lands again; here Equip Change ends the performance, as in vanilla.</para>
/// </remarks>
internal static unsafe class Performances
{
    public const int Performing = 0x01, Dead = 0x20, MathSkill = 0x15; // +0x61
    private const int RecordSize = 20, UnitRecord = 0x1A0, UnitCharge = 0x18D, UnitFull = 0x1BD, Empty = 0xFF;
    private const int Instant = 1; // the inner queue's result for an action that runs at once
    private const int TurnLoopSize = 0x96C;

    private static Game _game = null!;
    private static bool _on;
    // A unit's kept song: its record, its countdown (0xFF: none) and its full charge.
    private static readonly byte[] Records = new byte[Battle.UnitCount * RecordSize];
    private static readonly byte[] Ct = Enumerable.Repeat((byte)Empty, Battle.UnitCount).ToArray();
    private static readonly byte[] Full = new byte[Battle.UnitCount];
    private static readonly byte[] Back = new byte[Battle.UnitCount]; // given back: its next run is logged
    private static readonly byte[] Snap = new byte[RecordSize];
    private static int _snapIndex = -1;
    private static byte _snapCt, _snapFull;
    private static int _keep = -1; // the unit whose song the queue's reset, next, keeps
    private static delegate* unmanaged<int, uint> _turnLoop;
    private static int _failed;

    internal static bool On => _on;
    // For the offline check.
    internal static nint ResetAt { get; private set; }
    internal static nint StepAt { get; private set; }
    internal static nint[] NoticesAt { get; private set; } = [];

    public static void Install(Game game)
    {
        if (!TwoSpells.Turns || TwoSpells.GameActionState == null)
            throw new InvalidOperationException("needs Two Spells' action start (support 0x1D0 in the build)");
        _game = game;
        var image = game.Image;
        // The queue (0x281434): the inner queue (rdx the unit's record, r8d 1); then for an instant action (1), unless
        // a reaction runs ([0x186AF9C]), the action state reset: xor edx, edx; mov rcx, rbx; call 0x279370.
        var queue = image.FindUnique("E8 ?? ?? ?? ?? 8B F8 83 F8 01 75 13 83 3D ?? ?? ?? ?? 00 75 0A 33 D2 48 8B CB E8");
        if (GameImage.Rip(queue, 1, 5) != (nint)game.RuleCode.QueueAction)
            throw new InvalidDataException("the queue doesn't call the inner queue this DLL hooks");
        var reset = queue + 26;
        if (GameImage.Rip(reset, 1, 5) != (nint)TwoSpells.GameActionState)
            throw new InvalidDataException("the queue's reset isn't the action state Two Spells found");
        // The battle's step (0x2029DC): xor ecx, ecx; call the turn loop; mov ecx, 0xFF00; movzx edi, al; mov r8d, eax.
        var step = image.FindUnique("33 C9 E8 ?? ?? ?? ?? B9 00 FF 00 00 0F B6 F8 44 8B C0 44 23 C1") + 2;
        var turnLoop = GameImage.Rip(step, 1, 5);
        if ((ulong)(TwoSpells.TickAt - turnLoop) >= TurnLoopSize)
            throw new InvalidDataException("the battle's step doesn't call the turn loop whose countdown Two Spells patched");
        // The turn-start notices (0x237494 and its copy 0x2EB408): a Performing unit (test al, 1; je past) gets 0x8002.
        var notices = new[]
        {
            image.FindUnique("A8 01 0F 84 ?? ?? ?? ?? BB 02 80 00 00") + 2,
            image.FindUnique("A8 01 0F 84 ?? ?? ?? ?? BE 02 80 00 00") + 2,
        };

        _turnLoop = (delegate* unmanaged<int, uint>)turnLoop;
        var resetStub = GameImage.AllocateCodeNear(reset, 32);
        var code = new List<byte>();
        TwoSpells.AbsoluteJump(code, (nint)(delegate* unmanaged<byte*, int, nint>)&QueueResetEntry);
        code.ToArray().CopyTo(new Span<byte>(resetStub, 32));
        TwoSpells.WriteCall(reset, (nint)resetStub);
        var stepStub = GameImage.AllocateCodeNear(step, 32);
        code = [];
        TwoSpells.AbsoluteJump(code, (nint)(delegate* unmanaged<int, uint>)&TurnStep);
        code.ToArray().CopyTo(new Span<byte>(stepStub, 32));
        TwoSpells.WriteCall(step, (nint)stepStub);
        // je → nop; jmp, to the same place: no notice.
        foreach (var at in notices)
            GameImage.WriteCode(at, [0x90, 0xE9]);
        BattleEvents.OnStart(game, "persistent performances", Reset);
        ResetAt = reset;
        StepAt = step;
        NoticesAt = notices;
        _on = true;
    }

    private static bool Active => *_game.Routines.Simulating == 0;

    /// <summary>Whether a unit's song is kept for it now.</summary>
    internal static bool Kept(int index) => _on && (uint)index < Battle.UnitCount && Ct[index] != Empty;

    /// <summary>Before the queue's commit: a performer's song (its record, countdown and full charge) is noted.</summary>
    internal static void BeforeQueue(byte* record, byte* queued, int commit)
    {
        _snapIndex = -1;
        if (!_on)
            return;
        var index = TwoSpells.Queued(record, queued, commit);
        if (index < 0)
            return;
        var unit = queued - UnitRecord;
        if ((unit[Battle.UnitStatuses] & Performing) == 0 || unit[UnitCharge] == Empty)
            return;
        new Span<byte>(queued, RecordSize).CopyTo(Snap);
        _snapCt = unit[UnitCharge];
        _snapFull = unit[UnitFull];
        _snapIndex = index;
    }

    /// <summary>
    /// After the queue's commit: an instant action (but Math Skill) keeps the song, which waits until the action is
    /// over; anything else that went in (a charge, a song, a Jump) or Math Skill drops it.
    /// </summary>
    internal static void AfterQueue(byte* record, byte* queued, int commit, int result)
    {
        _keep = -1;
        if (!_on)
            return;
        var index = TwoSpells.Queued(record, queued, commit);
        if (index < 0 || result < 0)
        {
            _snapIndex = -1;
            return;
        }
        var unit = queued - UnitRecord;
        var ability = *(ushort*)(record + 2);
        var keeps = result == Instant && record[1] != MathSkill && (unit[Battle.UnitStatuses] & Performing) != 0;
        if (keeps && _snapIndex == index)
        {
            Snap.CopyTo(Records.AsSpan(index * RecordSize, RecordSize));
            Ct[index] = _snapCt;
            Full[index] = _snapFull;
            Note($"performances: unit {index} keeps performing 0x{Snap[2] | (Snap[3] << 8):X3} (countdown {_snapCt}) through 0x{ability:X3}");
        }
        else if (!keeps && (_snapIndex == index || Ct[index] != Empty))
        {
            var song = _snapIndex == index ? Snap[2] | (Snap[3] << 8) : Records[index * RecordSize + 2] | (Records[index * RecordSize + 3] << 8);
            Ct[index] = Empty;
            Note(record[1] == MathSkill && result == Instant
                ? $"performances: unit {index}'s Math Skill 0x{ability:X3} ends its performance of 0x{song:X3}"
                : $"performances: unit {index}'s 0x{ability:X3} replaces its performance of 0x{song:X3}");
        }
        if (keeps && Ct[index] != Empty)
            _keep = index;
        _snapIndex = -1;
    }

    [UnmanagedCallersOnly]
    private static nint QueueResetEntry(byte* unit, int state) => QueueReset(unit, state);

    /// <summary>
    /// The queue's action state reset after an instant action (0x279370 with 0): for a kept song only the countdown is
    /// emptied, and Performing stays.
    /// </summary>
    internal static nint QueueReset(byte* unit, int state)
    {
        var keep = _keep;
        _keep = -1;
        try
        {
            if (keep >= 0 && (byte)state == 0 && Active && unit == _game.TurnCode.Units + keep * Battle.UnitSize)
            {
                unit[UnitCharge] = Empty;
                return 0;
            }
        }
        catch (Exception e)
        {
            Failed("the queue's reset", e);
        }
        return TwoSpells.GameActionState(unit, state);
    }

    /// <summary>
    /// The action's start for a Performing unit (from <see cref="TwoSpells.StartAction"/>, 0x279370 with 0xFF): skipped
    /// (true) while its song is kept, so the instant action's record doesn't reload its countdown. A song given back
    /// running again is logged.
    /// </summary>
    internal static bool AtActionStart(int index, byte* unit)
    {
        if (!_on || (uint)index >= Battle.UnitCount || (unit[Battle.UnitStatuses] & Performing) == 0)
            return false;
        if (Ct[index] != Empty)
        {
            Note($"performances: unit {index} goes on performing as 0x{*(ushort*)(unit + UnitRecord + 2):X3} starts");
            return true;
        }
        if (Back[index] != 0)
        {
            Back[index] = 0;
            Note($"performances: unit {index}'s song 0x{*(ushort*)(unit + UnitRecord + 2):X3} goes on after its instant action");
        }
        return false;
    }

    [UnmanagedCallersOnly]
    private static uint TurnStep(int start)
    {
        try
        {
            if (start == 0 && Active)
                GiveBack();
        }
        catch (Exception e)
        {
            Failed("giving a song back", e);
        }
        return _turnLoop(start);
    }

    /// <summary>
    /// The battle's step before the turn loop (the action is over): each kept song goes back to its unit's record with
    /// its countdown and full charge, unless the unit stopped Performing, died or left.
    /// </summary>
    internal static void GiveBack()
    {
        if (!_on)
            return;
        for (var index = 0; index < Battle.UnitCount; index++)
        {
            if (Ct[index] == Empty)
                continue;
            var unit = _game.TurnCode.Units + index * Battle.UnitSize;
            var song = Records[index * RecordSize + 2] | (Records[index * RecordSize + 3] << 8);
            if (unit[1] != Empty && (unit[Battle.UnitStatuses] & (Performing | Dead)) == Performing)
            {
                Records.AsSpan(index * RecordSize, RecordSize).CopyTo(new Span<byte>(unit + UnitRecord, RecordSize));
                unit[UnitCharge] = Ct[index];
                unit[UnitFull] = Full[index];
                Back[index] = 1;
                Note($"performances: unit {index}'s song 0x{song:X3} is back, countdown {Ct[index]}");
            }
            else
                Note($"performances: unit {index}'s song 0x{song:X3} is dropped (no longer performing)");
            Ct[index] = Empty;
        }
    }

    /// <summary>A new battle (and the offline check): no songs kept.</summary>
    internal static void Reset()
    {
        Array.Fill(Ct, (byte)Empty);
        Array.Clear(Back);
        _snapIndex = -1;
        _keep = -1;
    }

    /// <summary>
    /// A line for the log every time (the tracer stops after 128 distinct lines a session, which a long battle reaches),
    /// and for the test recorder. It's one or two lines for each instant action of a performer.
    /// </summary>
    private static void Note(string line)
    {
        Tracer.Sink?.Invoke(line);
        _game.Log.Info("trace " + line);
    }

    internal static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"performances: {what} failed: {e}");
    }
}
