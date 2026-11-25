using EventInstructionUpgrade.Interfaces;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's events on the Event Instruction Upgrade (its own mod, code/EventInstructionUpgrade, which ReMixed depends
/// on; docs/analysis/remixed-event-opcodes.md). The upgrade runs the instructions; this gives it what ReMixed's events
/// need beyond them: ReMixed's native routines (Routine, 0xCF), its battle event conditions (LoadConditionals, 0xCE),
/// the dispatch tavern's variables and PS1 places, the skillset types, units keeping the looks events give them, its
/// unit presets, and the scenario and text steps it takes as each instruction runs.
/// </summary>
internal static unsafe class RemixedEvents
{
    /// <summary>The upgrade, from Reloaded-II (<see cref="Startup"/>; the offline check makes its own); null while it
    /// isn't loaded.</summary>
    internal static IEventInstructions? Api;

    /// <summary>ReMixed's battle event conditions per event number (from its disc), for LoadConditionals.</summary>
    internal const string PresetsFileName = "remixed-event-presets.bin";
    internal const int PresetBytes = 0x220, PresetOffsets = 0x20;

    /// <summary>The per-event map-settings words (<see cref="EventTables.MapSettings"/>), for ReMixed's scenarios.</summary>
    internal static ushort* MapSettings;

    private static Log _log = null!;

    public static void Install(Game game)
    {
        _log = game.Log;
        MapSettings = EventTables.MapSettings(game.Image);
        if (Api is not { } api)
            throw new InvalidOperationException("the Event Instruction Upgrade mod isn't loaded");
        if (!api.Installed)
            throw new InvalidOperationException("the Event Instruction Upgrade didn't install (its log says why)");
        api.OnMessage(text => TestRecorder.Note("remixed events: " + text), text => TestRecorder.Error("remixed events: " + text));
        try
        {
            TavernStorage.Install(game);
        }
        catch (Exception e)
        {
            _log.Error($"dispatch tavern: its groups' days don't pass ({e.Message})");
        }
        try
        {
            EventUnits.Install(game.Image);
        }
        catch (Exception e)
        {
            _log.Error($"remixed events: a unit number past the groups (the Treasure Wheel's chests) names no unit ({e.Message})");
        }
        // The dispatch tavern's groups and mission states, in the remaster's errand storage (TavernStorage); its states'
        // bytes override the variables' map (the upgrade's, made first), as they're kept apart from the remaster's own
        // nibbles.
        TavernStorage.Find(game.Image);
        api.AddVariables(TavernStorage.Holds, TavernStorage.Get, TavernStorage.Set);
        api.MapMemory(TavernStorage.Ps1Count, 1, (nint)TavernStorage.Count);
        api.MapMemory(TavernStorage.Ps1Records, TavernStorage.Records * TavernStorage.RecordBytes, (nint)TavernStorage.RecordsAt);
        api.MapMemory(TavernStorage.Ps1States, TavernStorage.StatesBytes, (nint)TavernStorage.States);
        // ReMixed's location battles read each enemy's skillsets' types.
        api.MapMemory(Ps1Memory.SkillsetTypeTable, Ps1Memory.SkillsetTypeBytes, (nint)SkillsetTypes.Find(game.Image));
        MapUnitPlaces(api.MapMemory, game.TurnCode.Units);
        MapRosterJobLevels(api.MapMemory, game.Units.Roster);
        MapTavernJobData(api.MapMemory, game.Units.Jobs);
        try
        {
            // A unit keeps the graphic an event gives it, where the remaster would work it out again from its sprite
            // set when a box shows its face.
            UnitLooks.Install(game, game.TurnCode.Units);
            api.OnGraphicWritten(UnitLooks.Mark);
        }
        catch (Exception e)
        {
            _log.Error($"remixed events: units don't keep the looks events give them: {e.Message}");
        }
        try
        {
            // Bryn and Lorne keep their Dancer and Bard open, as ReMixed's unlocked-jobs rule gives them by name.
            PerformerJobs.Install(game, game.TurnCode.Units);
        }
        catch (Exception e)
        {
            _log.Error($"remixed events: Bryn's and Lorne's own jobs aren't kept open: {e.Message}");
        }
        try
        {
            // DismissUnit by name id where no sprite set matches (Bryn, Lorne and Elias's locked looks, 1-A's end).
            DismissByName.Install(game);
        }
        catch (Exception e)
        {
            _log.Error($"remixed events: DismissUnit doesn't find units by name: {e.Message}");
        }
        try
        {
            GuestDeployment.Install(game);
        }
        catch (Exception e)
        {
            _log.Error($"remixed events: guest formation availability couldn't be set: {e.Message}");
        }
        api.OnUnitPreset(preset => UnitPresets.Pending = preset);
        api.AddStepObserver(at =>
        {
            RemixedScenarios.RevealContinued();
            RemixedScenarios.MoveMessage((byte*)at);
            RemixedText.Alternate((byte*)at);
            MirrorBaseTeamWrite((byte*)at, api.GetVariable, game.TurnCode.Units);
        });
        var presets = Path.Combine(Path.GetDirectoryName(typeof(RemixedEvents).Assembly.Location) ?? "", PresetsFileName);
        if (File.Exists(presets))
            api.SetConditionals(File.ReadAllBytes(presets));
        else
            _log.Info($"remixed events: no {PresetsFileName} beside the DLL; LoadConditionals (0xCE) does nothing");
        api.AddRoutine(LevelScalingRerun, () => LevelScaling.FromEvent(api.GetVariable(0x70), Note));
        var battleUnits = game.TurnCode.Units;
        var roster = game.Units.Roster;
        api.AddRoutine(LevelChange, () => Note(ChangeLevel(battleUnits, (uint)api.GetVariable(0), api.GetVariable(1) & 0xFF,
                                                           (api.GetVariable(0x18) & 0xFF) != 0)));
        api.AddRoutine(RosterSwap, () =>
        {
            if (SwapRoster(roster, (uint)api.GetVariable(0), (uint)api.GetVariable(1)) is { } slots)
                Note($"roster slots {slots.First} and {slots.Second} swapped");
        });
        api.AddRoutine(MirrorCopy, () => Note(CopyParty(battleUnits, (uint)api.GetVariable(1), (uint)api.GetVariable(MirrorLast),
                                                        k => api.GetVariable(MirrorSources + k))));
        var applyEventStatus = FindEventStatus(game.Image);
        api.AddRoutine(ApplyExtendedStatus, () =>
        {
            var actor = api.GetVariable(ExtendedStatusActor);
            var mode = api.GetVariable(ExtendedStatusMode);
            if (DecodeExtendedStatus(mode) is { } status)
                applyEventStatus(RemixedScenarios.EventUnitId(battleUnits, actor), status.Bit, status.Add ? 1 : 0);
        });
        BattleEnd.Install(game);
        api.AddRoutine(MidsceneJoinReturn, BattleEnd.RestoreMidsceneField);
        api.AddRoutine(LiveBattleWriteBack, () =>
        {
            // What it returns is the joiners it queued for a join screen, which this call doesn't show: none join.
            var joiners = BattleEnd.WriteBackNow();
            if (joiners > 0)
                _log.Warn($"remixed events: the live battle write-back queued {joiners} joiner(s) with no join screen; "
                          + "they don't join (a recruitment needs display state 7)");
            else
                Note("live battle write-back done");
        });
        _log.Info("remixed events: ReMixed's routines, conditions and tavern given to the Event Instruction Upgrade");
    }

    /// <summary>
    /// Places in and about a battle unit that ReMixed's events write, its Nemesis's return (0x11D) among them: each unit's
    /// preset (PS1 +0x169, ReMixed's loader's copy of its battle data's byte 37; <see cref="UnitPresets.Slots"/>, worked
    /// into its stats at its next recalculation), and the snapshot of the units the PS1 takes as a battle's event starts
    /// and puts back on them as it ends (BATTLE.BIN 0x80148580 and 0x801488A8, at [0x80173C78], which ReMixed's events
    /// name as 0x8019F9B4; <see cref="SnapshotPlaces"/>). The remaster keeps no such copy, so each of its tables goes to
    /// the unit's own field, where the putting back takes it: what an event writes there is the unit's after the event,
    /// as on the PS1 (0x11D's Reraise, Protect, Shell and Haste, and Immortal, +5's 4). Its two lists of statuses whose
    /// sprite effects the putting back turns off and on (+0x1A4 and +0x13B, five bytes a unit) aren't statuses, and stay
    /// in the upgrade's store; so does its copy of HP (+0x3C6).
    /// </summary>
    internal static void MapUnitPlaces(Action<uint, int, nint> map, byte* units)
    {
        for (var k = 0; k < Ps1Memory.UnitCount; k++)
        {
            map(Ps1Memory.Units + (uint)k * Ps1Memory.UnitSize + UnitPresetAt, 1, (nint)(UnitPresets.Slots + k));
            var unit = units + k * Battle.UnitSize;
            foreach (var (table, stride, field, length) in SnapshotPlaces)
                map(Snapshot + (uint)(table + stride * k), length, (nint)(unit + Ps1Memory.UnitField(field)));
        }
    }

    /// <summary>
    /// Mirrors an event's SaveAddress to a battle unit's PS1 base-team byte (+0x1BA) into the remaster's live
    /// base-team byte (+0x1EE). <see cref="Ps1Memory"/> deliberately keeps that field read-only because random-battle
    /// setup event 0x191 clears every enemy team's bits there, which makes the remaster declare victory immediately;
    /// its other events' deliberate team and control changes must still reach the live field. Called before the
    /// instruction runs, after AlterParameter has patched its operands.
    /// </summary>
    internal static bool MirrorBaseTeamWrite(byte* instruction, Func<int, int> variable, byte* units)
    {
        const int saveAddress = 0xC4, baseTeam = 0x1BA, randomBattleSetup = 0x191;
        if (instruction == null || instruction[0] != saveAddress || units == null
            || variable(0x27) == randomBattleSetup || instruction[5] is not (0 or 1))
            return false;
        var source = instruction[1] & 0x7F;
        var offset = instruction[2] | instruction[3] << 8;
        var address = unchecked((uint)(variable(instruction[4] & 0x7F) + offset));
        var relative = address - Ps1Memory.Units;
        if (relative >= Ps1Memory.UnitCount * Ps1Memory.UnitSize || relative % Ps1Memory.UnitSize != baseTeam)
            return false;
        var slot = (int)(relative / Ps1Memory.UnitSize);
        units[slot * Battle.UnitSize + Battle.UnitTeam] = (byte)variable(source);
        return true;
    }

    /// <summary>
    /// A roster unit's job levels (PS1 +0x64-0x6D, a nibble a job, the even index high, Squire to Mime), which the
    /// upgrade's map sends into the learned abilities: the remaster keeps them at +0x74 (its unit build, 0x276758, copies
    /// them from there to the battle unit's +0xE4), in the same order and packing for those 20 jobs. ReMixed's tavern
    /// reads them for its contests (a squad's job levels in the contest's job) and its missions' preferences.
    /// </summary>
    internal static void MapRosterJobLevels(Action<uint, int, nint> map, byte* roster)
    {
        for (var k = 0; k < Ps1Memory.RosterSlots; k++)
            map(Ps1Memory.Roster + (uint)k * Ps1Memory.RosterSize + RosterJobLevelsAt, JobLevelBytes,
                (nint)(roster + k * UnitTables.RosterSize + RemasterJobLevelsAt));
    }

    /// <summary>
    /// The job records the tavern's Training and Retraining rewards read (PS1 0x800610B8, 0x30 bytes a job). The
    /// remaster keeps the same fields in 0x31-byte records: bytes 0–12 are in place, then its extra byte at +13 moves
    /// the growths, multipliers and the rest of the PS1 record one byte on. Map the fields to the live table after the
    /// loader has applied ReMixed's JobData edits, so event 0x0E9 compares and copies real growths instead of the
    /// upgrade's zero-filled private store.
    ///
    /// ReMixed's man-only Bard row 0x38 was relocated to the remaster's Bard (0x5B), and its Dark Knight row 0x5B to
    /// the remaster's Dark Knight (0xA0), by the table build. Those aliases make the event's original job numbers see
    /// the relocated records. The second Dark Knight id 0x37 uses that same remaster record.
    /// </summary>
    internal static void MapTavernJobData(Action<uint, int, nint> map, byte* jobs)
    {
        for (var theirs = 0; theirs < Ps1JobCount; theirs++)
        {
            var ours = theirs switch
            {
                0x37 or 0x5B => 0xA0,
                0x38 => 0x5B,
                _ => theirs,
            };
            var ps1 = Ps1JobData + (uint)(theirs * Ps1JobSize);
            var remaster = jobs + ours * UnitTables.JobSize;
            map(ps1, Ps1JobExtraAt, (nint)remaster);
            map(ps1 + Ps1JobExtraAt, Ps1JobSize - Ps1JobExtraAt, (nint)(remaster + Ps1JobExtraAt + 1));
        }
    }

    internal const int RosterJobLevelsAt = 0x64, RemasterJobLevelsAt = 0x74, JobLevelBytes = 10;
    internal const uint Ps1JobData = 0x800610B8;
    internal const int Ps1JobSize = 0x30, Ps1JobCount = 160, Ps1JobExtraAt = 13;
    internal const int UnitPresetAt = 0x169;
    /// <summary>The units' snapshot as ReMixed's events name it (vanilla's [0x80173C78]).</summary>
    internal const uint Snapshot = 0x8019F9B4;
    /// <summary>The snapshot's tables (by the unit's index: the table + stride × index) and the PS1 unit fields the
    /// putting back copies them to: innate statuses, current statuses, the second copy of the statuses (+0x1BB), the
    /// statuses' counters, +0x15D, +7, and ReMixed's addition, +5 (team, control, 4 Immortal; 0x8014869C, 0x80148A80).</summary>
    internal static readonly (int Table, int Stride, int Field, int Length)[] SnapshotPlaces =
    [
        (0x000, 5, 0x4E, 5), (0x069, 5, 0x58, 5), (0x0D2, 5, 0x1BB, 5), (0x20D, 16, 0x5D, 16),
        (0x35D, 1, 0x15D, 1), (0x372, 1, 0x07, 1), (0x387, 1, 0x05, 1),
    ];

    /// <summary>The upgrade's words (its variables 0xB00–0xB7F and the text values), or null without it.</summary>
    internal static int* Words => Api == null ? null : (int*)Api.Words;

    /// <summary>An event variable's word (an id below 0x80), or 0 without the upgrade.</summary>
    internal static int Word(int id) => Api != null && id is >= 0 and < 0x80 ? Api.GetVariable(id) : 0;

    /// <summary>For the test harness: the running event's code, its last instruction, and tracing them.</summary>
    internal static nint RunningCode => Api?.RunningCode ?? 0;
    internal static int LastPc => Api?.LastOffset ?? -1;
    internal static int LastOp => Api?.LastOpcode ?? -1;

    internal static bool TraceSteps
    {
        get => Api?.TraceSteps ?? false;
        set
        {
            if (Api != null)
                Api.TraceSteps = value;
        }
    }

    /// <summary>ReMixed's Improved Level Scaling re-run (BATTLE.BIN 0x80151000, called by most battle events): the
    /// port's level scaling does it at the first countdown (<see cref="LevelScaling.FromEvent"/>).</summary>
    internal const uint LevelScalingRerun = 0x80151000;
    /// <summary>ReMixed's roster swap (BATTLE.BIN 0x80160600): the two roster units at the PS1 addresses in vars 0 and 1
    /// trade places, each slot keeping its byte +1 (and the PS1's last 0x28 bytes, which the remaster's records don't
    /// hold apart). Six events call it (the side chapter's start among them).</summary>
    internal const uint RosterSwap = 0x80160600;
    /// <summary>ReMixed's level change (BATTLE.BIN 0x80150440: 0x80157E00 with var 1's low byte, var 0x18's and var 0): the
    /// battle unit at var 0's PS1 address levels down (var 0x18's low byte not 0) or up that many times, between 1 and
    /// 99, each level's growth applied (vanilla's 0x8005DA10), then its stats recalculated. The Algus campaign's 0x008
    /// and 0x07B call it. Here the unit is rebuilt at its new level as the level scaling rebuilds one.</summary>
    internal const uint LevelChange = 0x80150440;

    internal static string ChangeLevel(byte* units, uint address, int count, bool down)
    {
        if (address - Ps1Memory.Units >= Ps1Memory.UnitCount * Ps1Memory.UnitSize)
            return $"0x80150440: 0x{address:X8} isn't a battle unit; nothing done";
        var index = (address - Ps1Memory.Units) / Ps1Memory.UnitSize;
        var unit = units + index * Battle.UnitSize;
        int before = unit[Battle.UnitLv], after = Math.Clamp(before + (down ? -count : count), 1, 99);
        if (after == before)
            return $"0x80150440: battle unit {index} stays at level {before}";
        if (!LevelScaling.CanRebuild)
            return $"0x80150440: the level scaling's rebuild isn't found; battle unit {index} stays at level {before}";
        LevelScaling.Rebuild(unit, after);
        return $"0x80150440: battle unit {index}'s level {before} -> {after}";
    }

    /// <summary>Mirror Match's copy of the party (ReMixed's 0x0C4, mission-list entry 17), a routine of the mod's own: its
    /// event copies each party unit over one of Gogo's units 0-4 by raw PS1 offsets, a byte at a time, which the
    /// remaster's wider units don't hold as the PS1's (its equipment halfwords, 22 jobs' learned abilities and JP, its
    /// name as UTF-8), so the build has the event call this in place of the loop (its EVENT_CODE_PATCHES). No PS1 address
    /// is that small, so none of ReMixed's routines can be taken for it. The battle units from var 1's PS1 address through
    /// var 0x74's (<see cref="MirrorLast"/>) each become a copy of the unit whose event id is var 0x7A, 0x7B... (<see
    /// cref="MirrorSources"/>), as the PS1's loop goes (<see cref="CopyUnit"/>).</summary>
    internal const uint MirrorCopy = 0x000000C4;
    /// <summary>
    /// A mod-owned native routine address unused by ReMixed. AFTER_BATTLE_RETURN calls the hooked roster write-back
    /// here without the remaster's display-state-7 teardown, then resumes its post-battle scene on the same map.
    /// </summary>
    internal const uint LiveBattleWriteBack = 0x000000C5;
    internal const uint ApplyExtendedStatus = 0x000000C6;
    internal const uint MidsceneJoinReturn = 0x000000C7;
    internal const int ExtendedStatusActor = 0xB7E, ExtendedStatusMode = 0xB7F;

    /// <summary>ReMixed's extended status mode: bit 7 selects it, bit 6 removes the status, low six bits choose it.</summary>
    internal static (int Bit, bool Add)? DecodeExtendedStatus(int mode) =>
        mode is >= 0x80 and <= 0xFF && (mode & 0x3F) < 40 ? (mode & 0x3F, (mode & 0x40) == 0) : null;

    // The stock event's status setter (0x241BD0) queues the selected status's bit and starts its battle action.
    internal static delegate* unmanaged<int, int, int, void> FindEventStatus(GameImage image) =>
        (delegate* unmanaged<int, int, int, void>)image.FindUnique(
            "48 89 5C 24 08 57 48 83 EC 30 41 8B D8 8B FA E8 ?? ?? ?? ?? 41 B8 D0 07 00 00");
    internal const int MirrorLast = 0x74, MirrorSources = 0x7A;

    /// <summary><see cref="MirrorCopy"/>: the units from PS1 address <paramref name="first"/> through
    /// <paramref name="last"/> copied from the units whose event ids <paramref name="sourceId"/> gives (the first's, the
    /// second's...), found as UNIT_ADDRESS (0xC2) finds them. A source not found leaves its copy as it is (the PS1 read
    /// from address 0). Returns a line for the trace.</summary>
    internal static string CopyParty(byte* units, uint first, uint last, Func<int, int> sourceId)
    {
        bool IsUnit(uint address) => address - Ps1Memory.Units < Ps1Memory.UnitCount * Ps1Memory.UnitSize
                                     && (address - Ps1Memory.Units) % Ps1Memory.UnitSize == 0;
        if (!IsUnit(first) || !IsUnit(last) || last < first)
            return $"Mirror Match's copy: 0x{first:X8} through 0x{last:X8} aren't battle units; nothing copied";
        var memory = new Ps1Memory(units, null, _ => { });
        var done = new List<string>();
        for (uint k = 0, slot = (first - Ps1Memory.Units) / Ps1Memory.UnitSize; slot <= (last - Ps1Memory.Units) / Ps1Memory.UnitSize; k++, slot++)
        {
            var id = sourceId((int)k) & 0xFF;
            var source = memory.FindUnit(id);
            if (source == 0)
            {
                done.Add($"no unit 0x{id:X2} for unit {slot}");
                continue;
            }
            var from = (source - Ps1Memory.Units) / Ps1Memory.UnitSize;
            CopyUnit(units + from * Battle.UnitSize, units + slot * Battle.UnitSize);
            UnitLooks.Mark(slot); // its graphic written, as the PS1's loop writes +0x15F
            done.Add($"unit {slot} copied from unit {from} (0x{id:X2})");
        }
        return "Mirror Match's copy: " + string.Join(", ", done);
    }

    /// <summary>One unit copied over another as 0x0C4's loop does, in the remaster's layout (<see cref="Ps1Memory.UnitField"/>):
    /// the sprite set (+0), the job (+3) and +6's gender bits (0xE0, the rest cleared), all that the PS1's +0x09-0x160
    /// holds (the remaster's +0x09-0x190: equipment, stats, statuses, equipped and learned abilities, unlocked jobs, job
    /// levels and JP, the name, the look), with +0x91's bit 4 set after it, then the job's skillset (+0x162), the name id
    /// (+0x16C-0x16E: the remaster's dword), +0x184-0x185 and the second status copy (+0x1BB-0x1BE). The rest stays the
    /// copy's own: its index (+1), team (+5, and +0x1BA's base team), battle id (+0x161) and what the battle keeps.</summary>
    internal static void CopyUnit(byte* from, byte* to)
    {
        to[0] = from[0];
        to[3] = from[3];
        to[6] = (byte)(from[6] & 0xE0);
        CopyFields(from, to, 0x09, 0x160);
        to[Ps1Memory.UnitField(0x91)] |= 4;
        CopyFields(from, to, 0x162, 0x162);
        new Span<byte>(from + NameIdAt, 4).CopyTo(new Span<byte>(to + NameIdAt, 4));
        CopyFields(from, to, 0x184, 0x185);
        CopyFields(from, to, 0x1BB, 0x1BE);
    }

    /// <summary>The remaster's battle unit's name id, a dword (PS1 +0x16C, a halfword, and +0x16E).</summary>
    private const int NameIdAt = 0x1FC;

    /// <summary>The remaster's bytes for PS1 unit offsets <paramref name="first"/> through <paramref name="last"/>, a
    /// run the map lays out without a gap.</summary>
    private static void CopyFields(byte* from, byte* to, int first, int last)
    {
        int start = Ps1Memory.UnitField(first), end = Ps1Memory.UnitField(last);
        new Span<byte>(from + start, end - start + 1).CopyTo(new Span<byte>(to + start, end - start + 1));
    }

    private static void Note(string text) => Tracer.Line(_log, "remixed events: " + text);

    /// <summary><see cref="RosterSwap"/>: the remaster's records for the two PS1 roster addresses trade places, but
    /// for byte +1. Addresses outside the roster do nothing, as on the PS1. Returns the two slots, if swapped.</summary>
    internal static (uint First, uint Second)? SwapRoster(byte* roster, uint first, uint second)
    {
        const uint size = Ps1Memory.RosterSize;
        if (first < Ps1Memory.Roster || second < Ps1Memory.Roster)
            return null;
        var (a, b) = ((first - Ps1Memory.Roster) / size, (second - Ps1Memory.Roster) / size);
        if (a >= Ps1Memory.RosterSlots || b >= Ps1Memory.RosterSlots || a == b)
            return null;
        var x = roster + a * UnitTables.RosterSize;
        var y = roster + b * UnitTables.RosterSize;
        for (var k = 0; k < UnitTables.RosterSize; k++)
            if (k != 1)
                (x[k], y[k]) = (y[k], x[k]);
        return (a, b);
    }
}
