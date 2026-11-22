using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's riding rework, steps 2 and 3 (docs/analysis/riding.md, pieces 3–8 and 10): in each of its rider's turns a mount gives one
/// extra action, its own skillset, instant, with the mount as the attacker. A rider that uses its own action first may
/// then use the mount's skill (its list then holds only that). Using the mount's skill first leaves the rider's own action.
/// Installed by <see cref="Rules"/> with "riding-actions".
/// </summary>
/// <remarks>
/// <para>It goes in through Two Spells' hooks (<see cref="TwoSpells"/>, installed with support 0x1D0), which already sit
/// on the same four points: the command list (PS1 0x801813C0 → 0x8015A200, 0x8015A100, 0x8015A140, 0x8015E260; here
/// <see cref="Commands"/>), the turn's action mark (the action end 0x8015F100; here <see cref="Acts"/>), the queue
/// (0x8015E370, the charge time; here <see cref="BeforeQueue"/> and <see cref="AfterQueue"/>) and the end of turn. The
/// attacker swap (0x80160F00) is in the formula wrapper (<see cref="Rules"/>' dispatch, through
/// <see cref="SwapAttacker"/>). ReMixed keeps its state in the units' action byte (+0x188 bits 0x04 and 0x08); here
/// it's in arrays, as Two Spells' is.</para>
/// <para>A rider: linked (+0x1B4 &amp; 0x80) to a mount that's linked back, not a monster itself (+6 &amp; 0x20), and
/// the mount's primary skillset is a monster's (0xA8 or more). The rider's MP pays, as in ReMixed. AI riders don't use
/// the mount's skills, as in ReMixed (the AI doesn't list them).</para>
/// <para>Step 3 (pieces 7, 8 and 10): a charging rider's pending charge is parked while its mount's skill runs and
/// given back after it (<see cref="Parked"/>); the EXP and JP go to the mount (<see cref="ActingMount"/>, used by
/// <see cref="Gains"/>); a dead rider's CT goes to its mount when it's placed off it (the place-unit routine, hooked
/// here). The rider stays Charging through both of the game's action state resets that follow the instant skill, as
/// ReMixed's 0x8015F400 sits on both: the queue's after an instant action (0x281476, PS1 0x80183DE8; here
/// <see cref="AtQueueReset"/>, through <see cref="Performances"/>' stub) and the action's start (0x2828E4, PS1
/// 0x8017DA64; Two Spells' hook). ReMixed gives the charge back as the action's display ends (0x8015DD00); here it's
/// the end of the rider's turn (<see cref="TurnEnded"/>), before any countdown. A rider that has moved has its turn
/// ended by the mark of its mount's skill (0x310548), before that skill is queued and the charge parked; the game runs
/// the end of turn again once the action is over (seen on the PC, 2026-11-19), which gives it back then, and the
/// battle's step before the turn loop (<see cref="GiveBack"/>, Performances' other stub) gives back any still parked.</para>
/// </remarks>
internal static unsafe class RidingActions
{
    public const int Link = 0x1B4, Primary = 0x12, MonsterSkillsets = 0xA8, UnitRecord = 0x1A0, UnitActed = 0x1BA;
    public const int JumpType = 9, MaxCommands = 6, Ct = 0x41, Dead = 0x20;

    private static Game _game = null!;
    private static bool _on;
    private static readonly byte[] RiderActed = new byte[Battle.UnitCount];
    private static readonly byte[] MountUsed = new byte[Battle.UnitCount];
    private static readonly byte[] Pending = new byte[Battle.UnitCount];
    // A charging rider's pending charge, parked while its mount's skill runs: the record and the CT (0xFF: none).
    private const int RecordSize = 20, UnitCharge = 0x18D, Charging = 0x08, Empty = 0xFF;
    private static readonly byte[] ParkedRecords = new byte[Battle.UnitCount * RecordSize];
    private static readonly byte[] ParkedCt = Enumerable.Repeat((byte)Empty, Battle.UnitCount).ToArray();
    private static readonly byte[] SnapRecord = new byte[RecordSize];
    private static byte _snapCt = Empty;
    private static byte* _ct;
    private static byte _ctBefore;
    private static int _failed;

    internal static bool On => _on;
    private static delegate* unmanaged<int, int, int, int, int, int> _place;

    public static void Install(Game game)
    {
        if (!TwoSpells.Turns || TwoSpells.Builder == 0)
            throw new InvalidOperationException("needs Two Spells' hooks (support 0x1D0 in the build)");
        _game = game;
        // The queue's reset after an instant action and the battle's step, shared with persistent performances.
        Performances.HookAfterAction(game);
        // At the committed walk's turn-over check, rcx is the rider's unit and rbx its sprite.
        var walkCheck = game.Image.FindUnique("80 89 B9 01 00 00 01 E8 ?? ?? ?? ?? 85 C0 74 07 8B CF E8") + 7;
        if (GameImage.Rip(walkCheck, 1, 5) != (nint)game.TurnCode.TurnOver)
            throw new InvalidDataException("the completed walk doesn't call the turn-over check");
        var walkStub = GameImage.AllocateCodeNear(walkCheck, 32);
        var code = new List<byte> { 0x48, 0x8B, 0xD3 }; // mov rdx,rbx
        TwoSpells.AbsoluteJump(code, (nint)(delegate* unmanaged<byte*, byte*, int>)&WalkTurnOver);
        code.ToArray().CopyTo(new Span<byte>(walkStub, 32));
        TwoSpells.WriteCall(walkCheck, (nint)walkStub);
        // The fifth argument is on the stack (read at 0x271A23), outside KeepingRegisters' four-argument ABI.
        var place = game.Image.FindUnique(
            "48 89 5C 24 08 44 88 44 24 18 88 54 24 10 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 20 45 0F B6 F1");
        _place = (delegate* unmanaged<int, int, int, int, int, int>)game.Hooks.Hook(
            place, (nint)(delegate* unmanaged<int, int, int, int, int, int>)&Place, "placing a unit");
        _on = true;
    }

    [UnmanagedCallersOnly]
    private static int Place(int index, int x, int y, int level, int facing)
    {
        try
        {
            if ((uint)index < Battle.UnitCount)
                DeadRiderCt(_game.TurnCode.Units + index * Battle.UnitSize);
        }
        catch (Exception e)
        {
            Failed("placing a unit", e);
        }
        return _place(index, x, y, level, facing);
    }

    [UnmanagedCallersOnly]
    private static int WalkTurnOver(byte* unit, byte* sprite)
    {
        try
        {
            FinishWalk(unit, sprite);
        }
        catch (Exception e)
        {
            Failed("finishing a walk", e);
        }
        return _game.TurnCode.TurnOver(unit);
    }

    // The sprite backs up its riding role in bits 2–3 at move selection (0x203E01); bits 0–1
    // hold the role after the walk (0 on foot, 1 rider, 2 mount). Preview and undo skip this call.
    private static void FinishWalk(byte* unit, byte* sprite)
    {
        var index = unit[Battle.UnitIndex];
        if (index >= Battle.UnitCount || unit[Battle.UnitTurn] == 0 || *_game.Routines.Simulating != 0)
            return;
        var roles = sprite[0x12C] & 0x0F;
        if (roles == 1 && MountSkillset(unit) >= 0)
        {
            // ReMixed 0x8015F300: boarding leaves a mounted Move. If the human already
            // acted on foot, only the newly boarded chocobo's action becomes available.
            unit[0x1B9] &= 0xFC;
            sprite[0x153] &= 0xFE;
            if (unit[UnitActed] != 0)
            {
                RiderActed[index] = 1;
                unit[UnitActed] = 0;
            }
        }
        else if (roles == 4 && RiderActed[index] != 0 && (unit[Link] & 0x80) == 0)
        {
            // The rider can no longer spend the mount-only action after dismounting.
            unit[UnitActed] = 1;
        }
    }

    /// <summary>
    /// A dead rider being placed (which takes it off its mount): the mount gets its CT (PS1 0x8015DF20, from the
    /// placement's unlink), so the freed mount acts where its rider would have. Returns whether it did.
    /// </summary>
    internal static bool DeadRiderCt(byte* rider)
    {
        if ((rider[Link] & 0x80) == 0 || (rider[Battle.UnitStatuses] & Dead) == 0)
            return false;
        var mount = MountOf(rider);
        if (mount == null)
            return false;
        mount[Ct] = rider[Ct];
        Tracer.Line(_game.Log, $"riding: unit {rider[Battle.UnitIndex]} falls; its mount, unit {mount[Battle.UnitIndex]}, takes its CT {rider[Ct]}");
        return true;
    }

    /// <summary>
    /// The mount whose skill a unit's action is (its action record's skillset is its mount's), or null: the mount gets
    /// the action's EXP and JP, the EXP at its own level (PS1 0x8015E150 in the award, 0x8018D620 in the EXP).
    /// </summary>
    internal static byte* ActingMount(byte* unit)
    {
        var skillset = MountSkillset(unit);
        return skillset >= 0 && unit[UnitRecord + 1] == skillset ? MountOf(unit) : null;
    }

    /// <summary>A rider's mount, linked both ways, or null.</summary>
    internal static byte* MountOf(byte* rider)
    {
        var link = rider[Link];
        if ((link & 0x80) == 0)
            return null;
        if ((link & 0x1F) >= Battle.UnitCount)
            return null;
        var mount = _game.TurnCode.Units + (link & 0x1F) * Battle.UnitSize;
        return mount[1] != 0xFF && mount[Link] == (0x40 | rider[Battle.UnitIndex]) ? mount : null;
    }

    /// <summary>The mount's skillset a rider can use, or -1 (no mount, a monster rider, or not a monster skillset).</summary>
    internal static int MountSkillset(byte* rider)
    {
        if (!_on || rider == null || (rider[6] & 0x20) != 0)
            return -1;
        var mount = MountOf(rider);
        return mount != null && mount[Primary] >= MonsterSkillsets ? mount[Primary] : -1;
    }

    /// <summary>
    /// The command list (from <see cref="TwoSpells.Insert"/>): the mount's skillset before Defend and Equip Change while
    /// the mount's skill is unused; after the rider's own action, only that. Returns whether it changed the list.
    /// </summary>
    internal static bool Commands(int index, byte* unit, List<int> entries)
    {
        if ((uint)index >= Battle.UnitCount || MountUsed[index] != 0)
            return false;
        var skillset = MountSkillset(unit);
        if (skillset < 0)
            return false;
        if (RiderActed[index] != 0)
        {
            entries.Clear();
            entries.Add(skillset);
            return true;
        }
        if (entries.Contains(skillset))
            return false;
        var at = entries.FindIndex(e => e is 2 or 3);
        entries.Insert(at < 0 ? entries.Count : at, skillset);
        return true;
    }

    /// <summary>
    /// The turn's action mark for the player's action (from <see cref="TwoSpells.Acts"/>, after its own rule): the
    /// first of the pair leaves the other's action. A Jump leaves the mount. Returns the acted flag to mark.
    /// </summary>
    internal static int Acts(int index, int acted, byte* record)
    {
        if (!_on || acted == 0 || (uint)index >= Battle.UnitCount || record == null)
            return acted;
        var unit = _game.TurnCode.Units + index * Battle.UnitSize;
        var skillset = MountSkillset(unit);
        if (skillset < 0)
            return acted;
        if (record[1] == skillset)
        {
            var first = MountUsed[index] == 0 && RiderActed[index] == 0;
            MountUsed[index] = 1;
            Tracer.Line(_game.Log, $"riding: unit {index} uses its mount's skill 0x{*(ushort*)(record + 2):X3}");
            if (!first)
                return acted;
        }
        else
        {
            if (RiderActed[index] != 0 || MountUsed[index] != 0 || (TwoSpells.TypeTable[record[1]] & 0x0F) == JumpType)
                return acted;
            RiderActed[index] = 1;
            Tracer.Line(_game.Log, $"riding: unit {index} has acted; its mount's skill is left");
        }
        Pending[index] = 1;
        return 0;
    }

    /// <summary>Whether a queued record is a rider's use of its mount's skill.</summary>
    internal static bool IsMountSkill(byte* record)
    {
        if (!_on || record == null || record[0] >= Battle.UnitCount)
            return false;
        var skillset = MountSkillset(_game.TurnCode.Units + record[0] * Battle.UnitSize);
        return skillset >= 0 && record[1] == skillset;
    }

    /// <summary>Whether a unit's pending charge is parked (the action's start then keeps it Charging; PS1 0x8015F400).</summary>
    internal static bool Parked(int index) => _on && (uint)index < Battle.UnitCount && ParkedCt[index] != Empty;

    /// <summary>
    /// Before the queue: a rider's mount skill charges in 0 (PS1 0x8015E370), shown and committed; at a real commit, a
    /// charge the rider is still waiting on is noted, to park it (PS1 0x8015DE00).
    /// </summary>
    internal static void BeforeQueue(byte* record, byte* queued, int commit)
    {
        _ct = null;
        _snapCt = Empty;
        if (!IsMountSkill(record))
            return;
        var unit = _game.TurnCode.Units + record[0] * Battle.UnitSize;
        if (TwoSpells.Queued(record, queued, commit) >= 0 && unit[UnitCharge] is > 0 and < Empty
            && (unit[Battle.UnitStatuses] & Charging) != 0)
        {
            new Span<byte>(queued, RecordSize).CopyTo(SnapRecord);
            _snapCt = unit[UnitCharge];
        }
        var ability = _game.RuleCode.GetAbilityAction(*(ushort*)(record + 2));
        if (ability == null || (ability[0xC] & 0x7F) == 0)
            return;
        _ct = ability + 0xC;
        _ctBefore = *_ct;
        *_ct = (byte)(_ctBefore & 0x80);
    }

    /// <summary>
    /// After the queue: the charge time put back, a charge the mount's skill replaced parked (PS1 0x8015DE40), and a
    /// first action of the pair given back in the rider's "acted" byte.
    /// </summary>
    internal static void AfterQueue(byte* record, byte* queued, int commit)
    {
        if (_ct != null)
        {
            *_ct = _ctBefore;
            _ct = null;
        }
        if (_snapCt != Empty)
        {
            int index = record[0];
            SnapRecord.CopyTo(ParkedRecords.AsSpan(index * RecordSize, RecordSize));
            ParkedCt[index] = _snapCt;
            Note($"riding: unit {index}'s charge 0x{SnapRecord[2] | (SnapRecord[3] << 8):X3} (CT {_snapCt}) waits while its mount acts");
            _snapCt = Empty;
        }
        if (!_on)
            return;
        var committed = TwoSpells.Queued(record, queued, commit);
        if (committed >= 0 && Pending[committed] != 0)
        {
            (queued - UnitRecord)[UnitActed] = 0;
            Pending[committed] = 0;
        }
    }

    /// <summary>
    /// The queue's action state reset after an instant action (0x279370 with 0, from <see cref="Performances.QueueReset"/>):
    /// a rider whose charge is parked for its mount's skill keeps Charging (PS1 0x8015F400, on 0x80183DE8); only the
    /// instant skill's countdown (+0x18D, 0 from the queue) is emptied, so nothing fires from its record. Returns whether
    /// it did, in place of the game's reset.
    /// </summary>
    internal static bool AtQueueReset(byte* unit)
    {
        if (!_on || unit == null)
            return false;
        var offset = unit - _game.TurnCode.Units;
        if (offset < 0 || offset >= Battle.UnitCount * Battle.UnitSize || offset % Battle.UnitSize != 0)
            return false;
        var index = (int)(offset / Battle.UnitSize);
        if (ParkedCt[index] == Empty || !IsMountSkill(unit + UnitRecord))
            return false;
        unit[UnitCharge] = Empty;
        Note($"riding: unit {index} stays Charging through its mount's skill 0x{*(ushort*)(unit + UnitRecord + 2):X3}");
        return true;
    }

    /// <summary>
    /// The end of a unit's turn: its rider state is over, and a parked charge goes back to its record (PS1 0x8015DD00,
    /// after the action's display there), unless the unit stopped Charging or is gone.
    /// </summary>
    internal static void TurnEnded(int index)
    {
        if ((uint)index >= Battle.UnitCount)
            return;
        if (_on && MountUsed[index] != 0)
            Note($"riding: unit {index}'s turn ends with its mount's skill used{(ParkedCt[index] == Empty ? ", nothing parked yet" : "")}");
        RiderActed[index] = MountUsed[index] = Pending[index] = 0;
        if (_on && ParkedCt[index] != Empty)
            Unpark(index, "as its turn ends");
    }

    /// <summary>
    /// The battle's step before the turn loop (from <see cref="Performances"/>' stub on 0x2029E8, once the turn and its
    /// action are over): a charge still parked goes back. A rider that had moved has its turn ended by the mark of its
    /// mount's skill, before the skill is queued and the charge parked, so that <see cref="TurnEnded"/> finds nothing;
    /// in the game a second end of turn after the action gives it back first, and this is the fallback.
    /// </summary>
    internal static void GiveBack()
    {
        if (!_on)
            return;
        for (var index = 0; index < Battle.UnitCount; index++)
        {
            if (ParkedCt[index] != Empty)
                Unpark(index, "after its turn's action");
        }
    }

    /// <summary>A parked charge back in its unit's record with its CT, unless the unit stopped Charging or is gone.</summary>
    private static void Unpark(int index, string when)
    {
        var unit = _game.TurnCode.Units + index * Battle.UnitSize;
        if (unit[1] != Empty && (unit[Battle.UnitStatuses] & Charging) != 0)
        {
            ParkedRecords.AsSpan(index * RecordSize, RecordSize).CopyTo(new Span<byte>(unit + UnitRecord, RecordSize));
            unit[UnitCharge] = ParkedCt[index];
            Note($"riding: unit {index} goes back to charging 0x{*(ushort*)(unit + UnitRecord + 2):X3}, CT {ParkedCt[index]}, {when}");
        }
        else
            Note($"riding: unit {index}'s parked charge is dropped {when} (no longer charging)");
        ParkedCt[index] = Empty;
    }

    internal static void Reset()
    {
        Array.Clear(RiderActed);
        Array.Clear(MountUsed);
        Array.Clear(Pending);
        Array.Fill(ParkedCt, (byte)Empty);
        _snapCt = Empty;
    }

    /// <summary>
    /// A parked charge's line for the log every time (the tracer stops after 128 distinct lines a session, which a long
    /// battle reaches) and for the test recorder: a few lines for each use of a mount's skill by a charging rider.
    /// </summary>
    private static void Note(string line)
    {
        Tracer.Sink?.Invoke(line);
        _game.Log.Info("trace " + line);
    }

    /// <summary>The state of a swap, to put back after the formula.</summary>
    internal struct Swap
    {
        public byte* Attacker, Result;
        public ulong Weapon;
    }

    /// <summary>
    /// The formula wrapper (PS1 0x80160F00): for a rider whose action is its mount's skillset, the mount becomes the
    /// attacker (and its result), and the rider's weapon (formula, WP, element and status) is cleared. Returns whether
    /// it swapped; <see cref="Restore"/> puts everything back.
    /// </summary>
    internal static bool SwapAttacker(out Swap swap)
    {
        swap = default;
        if (!_on)
            return false;
        var battle = _game.Battle;
        var attacker = *battle.Attacker;
        if (attacker == null)
            return false;
        var skillset = MountSkillset(attacker);
        if (skillset < 0 || attacker[UnitRecord + 1] != skillset)
            return false;
        var mount = MountOf(attacker);
        var result = *_game.Routines.AttackerResult;
        swap.Attacker = attacker;
        swap.Result = result;
        swap.Weapon = *(ulong*)battle.Weapon;
        *battle.Attacker = mount;
        *_game.Routines.AttackerResult = mount + Battle.UnitResult;
        new Span<byte>(battle.Weapon + 2, 6).Clear();
        Tracer.Line(_game.Log, $"riding: unit {mount[Battle.UnitIndex]} attacks for its rider, unit {attacker[Battle.UnitIndex]}");
        return true;
    }

    internal static void Restore(in Swap swap)
    {
        *_game.Battle.Attacker = swap.Attacker;
        *_game.Routines.AttackerResult = swap.Result;
        *(ulong*)_game.Battle.Weapon = swap.Weapon;
    }

    internal static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"riding: {what} failed: {e}");
    }
}
