using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's species reactions (docs/MECHANICS.md: "Monster reactions by family"; SCUS 0x8002D680,
/// from the random R/S/M pick at 0x8005D10C). Installed by <see cref="Rules"/> with
/// "monster-reactions". Data: the reaction for each monster job 0x5E–0x8D.
/// </summary>
/// <remarks>
/// Building a unit from its battle data (0x2771C8) asks the pick (0x278358: unit, id, kind, record)
/// for its reaction (kind 2), support (4) and movement (8). An id from 0x1FE is random, and vanilla
/// gives a monster (+6 &amp; 0x20) none. ReMixed gives a monster's random reaction its family's:
/// Chocobos Counter, Goblins Counter Tackle, Behemoths Hamedo and so on. Its random support and
/// movement stay none.
/// </remarks>
internal static unsafe class MonsterReactions
{
    private static Game _game = null!;
    private static delegate* unmanaged<byte*, int, int, byte*, int> _pick; // the original
    private static delegate* unmanaged<int, int, ushort> _skillsetAbility;  // 0x275860
    private static byte* _jobs;                                              // +0 a job's skillset
    private static readonly ushort[] Reactions = new ushort[MonsterJobs];
    private static int _failed;

    private const int MonsterJobs = 48, FirstMonsterJob = 0x5E; // 16 families of 3
    private const int Random = 0x1FE, ReactionKind = 2, MonsterFlag = 0x20; // +6
    private const int UnitJob = 3, UnitGender = 6;

    // For the offline check.
    internal static nint PickAt { get; private set; }

    /// <summary>Hooks the pick. Returns how many monster jobs get a reaction.</summary>
    public static int Install(Game game, JsonElement data)
    {
        _game = game;
        var count = 0;
        foreach (var (id, k) in data.EnumerateArray().Select((e, k) => (e.GetInt32(), k)))
            if (k < MonsterJobs && id is >= 0x1A6 and <= 0x1C5)
            {
                Reactions[k] = (ushort)id;
                count++;
            }
        var image = game.Image;
        // The pick: an id under 0x1FE is kept; a monster gets nothing; then the random pick.
        var pick = image.FindUnique(
            "4C 89 4C 24 20 44 89 44 24 18 48 89 4C 24 08 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC 08 04 00 00 " +
            "33 FF B8 FE 01 00 00 40 88 BC 24 58 04 00 00 49 8B D9 4C 8B F1 44 8A D7 66 3B D0 73 08 0F B7 C2 " +
            "E9 ?? ?? ?? ?? F6 41 06 20 0F 85");
        // Building a unit: the reaction from its record's +0xC, kind 2, into the unit's +0x14.
        var reaction = image.FindUnique("0F B7 55 0C 4C 8B CD 41 B8 02 00 00 00 48 8B CF E8 ?? ?? ?? ?? 66 89 47 14");
        if (GameImage.Rip(reaction + 16, 1, 5) != pick)
            throw new InvalidDataException("the unit builder's reaction pick isn't the code this DLL expects");
        PickAt = pick;
        _skillsetAbility = game.Units.SkillsetAbility;
        _jobs = game.Units.Jobs;
        // Its one caller (three calls) keeps nothing but the result across it (liveregs).
        _pick = (delegate* unmanaged<byte*, int, int, byte*, int>)game.Hooks.Hook(
            pick, (nint)(delegate* unmanaged<byte*, int, int, byte*, int>)&Pick, "the R/S/M pick");
        return count;
    }

    [UnmanagedCallersOnly]
    private static int Pick(byte* unit, int id, int kind, byte* entry)
    {
        try
        {
            if (MarkLearned(unit, id, entry) is { } learned)
                Tracer.Line(_game.Log, $"ability validation: 0x{(ushort)id:X3} learned in job index {learned.Job}, slot {learned.Slot}");
            var checkedId = Validate(id, kind, entry, Options.Current.EnemyJobs);
            if (checkedId != id)
            {
                Tracer.Line(_game.Log, $"ability validation: kind {kind} id 0x{id:X3} becomes a random pick");
                id = checkedId;
            }
            var reaction = ForMonster(unit, id, kind);
            if (reaction != 0)
            {
                Tracer.Line(_game.Log, $"monster reactions: job 0x{unit[UnitJob]:X2} gets reaction 0x{reaction:X3}");
                return reaction;
            }
        }
        catch (Exception e)
        {
            Failed("the pick", e);
        }
        return _pick(unit, id, kind, entry);
    }

    /// <summary>For the offline check: the tables the learned marking reads.</summary>
    internal static (nint SkillsetAbility, nint Jobs) LearnedTables
    {
        get => ((nint)_skillsetAbility, (nint)_jobs);
        set
        {
            _skillsetAbility = (delegate* unmanaged<int, int, ushort>)value.SkillsetAbility;
            _jobs = (byte*)value.Jobs;
        }
    }

    private const int LearnedBits = 0xA2, SearchedSlots = 21; // ReMixed searches 16 actions and 5 R/S/M slots

    /// <summary>
    /// ReMixed's marking of a battle unit's fixed ability learned (SCUS 0x8002D400 → 0x8002D500): in its own
    /// sprite set's job (stored in job index 0's bits) if that job's skillset has it, else in the first of the
    /// generic jobs 0x4B–0x5C whose skillset does (not for a monster's entry, +1 &amp; 0x20). The bits are the
    /// record's 3 bytes a job at +0xA2, slot s at bit 0x80 >> (s &amp; 7) of byte s / 8. ReMixed's Bard and
    /// Dancer slots look at its gendered jobs 0x37 and 0x38 instead; the port keeps the remaster's own slots (MECHANICS.md). Returns where
    /// it was marked, or null.
    /// </summary>
    internal static (int Job, int Slot)? MarkLearned(byte* record, int id, byte* entry)
    {
        id = (ushort)id;
        if (record == null || id == 0 || id >= Random || _skillsetAbility == null)
            return null;
        int? Slot(int job)
        {
            var skillset = _jobs[job * UnitTables.JobSize];
            for (var s = 0; s < SearchedSlots; s++)
                if (_skillsetAbility(skillset, s) == id)
                    return s;
            return null;
        }
        (int, int)? Mark(int index, int slot)
        {
            record[LearnedBits + 3 * index + (slot >> 3)] |= (byte)(0x80 >> (slot & 7));
            return (index, slot);
        }
        if (record[0] < 0x80 && Slot(record[0]) is { } own)
            return Mark(0, own);
        if (entry != null && (entry[1] & 0x20) != 0)
            return null;
        for (var job = 0x4B; job < 0x5D; job++)
            if (Slot(job) is { } slot)
                return Mark(job - 0x4A, slot);
        return null;
    }

    /// <summary>
    /// ReMixed's check of a battle unit's fixed ability (SCUS 0x8002D400): an id that isn't of the slot's kind
    /// (reaction 0x1A6, support 0x1C6, movement 0x1E6, 32 each) becomes a random pick, and with Enemy Job
    /// Settings Expert or Exceptional so does an empty slot of a unit off the player's team (the entry's
    /// +0x18 &amp; 0x30). Before that, <see cref="MarkLearned"/>.
    /// </summary>
    internal static int Validate(int id, int kind, byte* entry, Options.EnemyJobRule enemies)
    {
        id = (ushort)id;
        if (id >= Random)
            return id;
        var randomEmpty = entry != null && enemies != Options.EnemyJobRule.Normal && (entry[0x18] & 0x30) != 0;
        if (id == 0 && !randomEmpty)
            return id;
        var first = kind switch { ReactionKind => 0x1A6, 4 => 0x1C6, 8 => 0x1E6, _ => -1 };
        if (first < 0)
            return id;
        return (uint)(id - first) < 32 ? id : Random;
    }

    /// <summary>The family reaction for a monster's random reaction, or 0 where the game's pick stands.</summary>
    internal static int ForMonster(byte* unit, int id, int kind)
    {
        if (unit == null || (ushort)id < Random || kind != ReactionKind || (unit[UnitGender] & MonsterFlag) == 0)
            return 0;
        var k = unit[UnitJob] - FirstMonsterJob;
        return (uint)k < MonsterJobs ? Reactions[k] : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"monster reactions: {what} failed: {e}");
    }
}
