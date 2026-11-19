using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Job Level Settings, Enemy Job Settings and random secondary (docs/MECHANICS.md: "Job Level
/// Settings" and "Random secondary skillset"; PS1 SCUS 0x8005B2B4 on, 0x8005B1C4, BATTLE 0x80151A00).
/// Installed by <see cref="Rules"/> with "job-levels".
/// </summary>
/// <remarks>
/// A unit made from battle data gets its job levels, unlocked jobs and secondary skillset in the
/// remaster's 0x27699C (rcx the record, rdx the entry), as in vanilla: the entry's level (+0x09) for its
/// specified job (+0x08) and that job's prerequisites, every job at least 1, then one try at a random
/// secondary for +0x0B = 0xFE. The hook lets it run, then redoes the levels ReMixed's way (the same level
/// also for the current job and, from chapter 4 or by the setting, the secondary's job; Enemy Job
/// Settings for units off the player's team), works the unlocked jobs out again (0x348684), and gives a
/// random secondary three tries. ReMixed's own Bard/Dancer remap of the random job waits for its gendered
/// job slots. The unit setup that learns abilities from the levels (0x2771C8) runs after.
/// <para>ReMixed's Dark Knight is one of the PS1's 20 job slots (17), so its unit setup (SCUS 0x8005BB40) gives it
/// JP by its level and teaches its abilities like any other job's, once its levels meet its unlock row (Knight 8,
/// Black Mage 8). The port's Dark Knight is the remaster's index 20, past the remaster's loop (jobs 0x4A to 0x5D)
/// and never unlocked by its unlocked-jobs routine for a unit made from battle data, so a Dark Knight enemy knew none
/// of Darkness. Here the Dark Knight is unlocked by its row, and after Mime's step it gets the same step: its JP
/// (0x2770BC, by the generic jobs' JP table, as the remaster's has no Dark Knight row) and its abilities (0x2781A8). A
/// primary override of Darkness is known in full, as ReMixed's override of its woman's Darkness (0x4E, a special
/// skillset its menu reads from the override's index 0) is.</para>
/// </remarks>
internal static unsafe class JobLevels
{
    private static Game _game = null!;
    private static delegate* unmanaged<byte*, byte*, void*, void> _setup;                 // the original
    private static delegate* unmanaged<byte*, int, byte*, int, int> _unlocked;           // 0x348684
    private static delegate* unmanaged<byte*, byte> _randomJob;                          // 0x276B8C
    // A job's abilities (0x2781A8: record, job, entry, the abilities it's given and how many, those it keeps unlearned
    // and how many, a byte for "the given ones cost nothing"); the original.
    private static delegate* unmanaged<byte*, int, byte*, ushort*, int, ushort*, int, int, void> _learnJob;
    private static delegate* unmanaged<byte*, int, int, int, void> _jobJp;                // 0x2770BC: record, +6, index, level
    private static byte* _prerequisites; // 12 bytes a job: byte k holds jobs 2k (high nibble) and 2k + 1
    private static byte* _jobs;          // 0x31 bytes a job: +0 its skillset
    private static byte* _roster;        // slot 0: +0 Ramza's sprite set, 1 to 3 by chapter
    private static int _failed;

    internal const int Levels = 0xE4, Unlocked = 0x9F, Primary = 0x12, Secondary = 0x13;
    private const int JobRow = 12, JobSize = UnitTables.JobSize, Generic = 0x4A, GenericJobs = 20;
    /// <summary>
    /// The port's job indexes for ReMixed's gendered slots (tools/build_mod.py, remixed_levels_job): the Dark Knight is
    /// the remaster's index 20 (job 0xA0, its level the high nibble of the levels' 11th byte), and a man's slot 18, the
    /// battle data's 0x92, is the Bard's level (index 17) with the slot's prerequisites, row 18 (ReMixed's one row for
    /// a woman's Dancer and a man's Bard).
    /// </summary>
    internal const int DarkKnight = 20, MansBard = 0x92, Bard = 17, SharedSlot = 18, DarkKnightJob = 0xA0;
    /// <summary>The remaster's last job in its unit setup's loop (Mime), the Dark Knight's skillset and unlocked bit
    /// (0x800000 >> 20), where a record keeps its learned abilities (3 bytes a job index) and the entry's primary
    /// override.</summary>
    internal const int LastLoopJob = 0x5D, DarkKnightSkillset = 0xE0, DarkKnightUnlocked = 0x800000 >> DarkKnight,
        LearnedBits = 0xA2, PrimaryOverride = 0x1D, Gender = 6, Jp = 0xF0, TotalJp = 0x11E; // JP: a halfword a job index
    private const byte RandomSecondary = 0xFE;
    private const int AllJobs = 0xFFFF;

    /// <summary>
    /// The unit setup's job loop (0x2771C8 from 0x2773B5): for each level byte, the even job's JP from its level
    /// (0x2770BC, at +0x1A) and its abilities (0x2781A8, at +0x4B), then the odd job's (+0x9A), while esi &lt; 0x15
    /// (+0xA5): jobs 0x4A to 0x5D. The odd job's JP call is at +0x69.
    /// </summary>
    private const string JobLoop =
        "45 0F B6 65 00 44 8D 46 FF 8A 94 24 ?? ?? ?? ?? 45 8B CC 41 C1 E9 04 48 8B CF E8 ?? ?? ?? ?? " +
        "8A 84 24 ?? ?? ?? ?? 8D 56 49 88 44 24 38 4D 8B CF 8B 84 24 ?? ?? ?? ?? 4C 8B C5 89 44 24 30 48 8B CF " +
        "48 89 5C 24 28 44 89 74 24 20 E8 ?? ?? ?? ?? 83 FE 17 73 ?? 8A 94 24 ?? ?? ?? ?? 41 83 E4 0F 45 8B CC " +
        "44 8B C6 48 8B CF E8 ?? ?? ?? ?? 8A 84 24 ?? ?? ?? ?? 8D 56 4A 88 44 24 38 4D 8B CF 8B 84 24 ?? ?? ?? ?? " +
        "4C 8B C5 89 44 24 30 48 8B CF 48 89 5C 24 28 44 89 74 24 20 E8 ?? ?? ?? ?? 83 C6 02 49 FF C5 83 FE 15 0F 82";

    // For the offline check.
    internal static nint SetupAt { get; private set; }
    internal static nint LearnJobAt { get; private set; }
    internal static nint JobJpAt { get; private set; }
    internal static delegate* unmanaged<byte*, int, byte*, ushort*, int, ushort*, int, int, void> LearnJobFunction
    {
        get => _learnJob;
        set => _learnJob = value;
    }
    internal static delegate* unmanaged<byte*, int, int, int, void> JobJpFunction
    {
        get => _jobJp;
        set => _jobJp = value;
    }
    internal static byte* Roster => _roster;
    internal static byte* Prerequisites
    {
        get => _prerequisites;
        set => _prerequisites = value;
    }
    internal static byte* JobData
    {
        get => _jobs;
        set => _jobs = value;
    }
    internal static delegate* unmanaged<byte*, byte> RandomJobFunction
    {
        get => _randomJob;
        set => _randomJob = value;
    }
    /// <summary>The postgame room stores its unlocked-job switch in var 0x6C's third byte.</summary>
    internal static Func<int, int> ReadVariable = id => Nemesis.GetVariable(id);
    internal static delegate* unmanaged<byte*, int, byte*, int, int> UnlockedFunction => _unlocked;

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // The setup from battle data: the levels at +0xE4 cleared, then (with an entry) the vanilla parts.
        var setup = image.FindUnique(
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 55 41 56 48 83 EC 20 4C 8D 99 E4 00 00 00 33 C0 " +
            "49 83 23 00 49 8B E8 41 83 63 08 00 48 8B FA 48 8B D9 48 85 D2 75 21");
        // Its prerequisite loop: table + 12 × job + byte.
        var table = image.FindUnique("4B 8D 0C 49 48 8D 04 8E 46 8A 84 28 ?? ?? ?? ??");
        // The unlocked jobs from the levels, the flags, +0xA2 and +0x1F4.
        var unlocked = GameImage.Calls(image.FindUnique(
            "44 0F B6 8B F4 01 00 00 4C 8D 83 A2 00 00 00 41 8A D2 49 8B CB E8 ?? ?? ?? ??"),
            "44 0F B6 8B F4 01 00 00 4C 8D 83 A2 00 00 00 41 8A D2 49 8B CB E8 ?? ?? ?? ??", "the unlocked jobs");
        // The random secondary: a random job's skillset (job data + 0x31 × job).
        var secondary = image.FindUnique(
            "8A 47 0B 3C FE 75 44 F6 43 06 20 75 38 48 8B CB E8 ?? ?? ?? ?? 84 C0 74 11 0F B6 C0 48 6B C8 31 " +
            "42 8A 84 29 ?? ?? ?? ??");
        var inSetup = setup + 0x200;
        if (table < setup || table > inSetup || secondary < setup || secondary > inSetup || unlocked.Length != 1)
            throw new InvalidDataException("the job level setup isn't the code this DLL expects");
        _prerequisites = (byte*)(image.ImageStart + *(int*)(table + 12));
        _jobs = game.Units.Jobs;
        if ((byte*)(image.ImageStart + *(int*)(secondary + 36)) != _jobs)
            throw new InvalidDataException("the job level setup reads other job data than this DLL expects");
        _randomJob = (delegate* unmanaged<byte*, byte>)GameImage.Rip(secondary + 16, 1, 5);
        _unlocked = (delegate* unmanaged<byte*, int, byte*, int, int>)unlocked[0];
        _roster = game.Units.Roster;
        var loop = image.FindUnique(JobLoop);
        var learn = GameImage.Rip(loop + 0x4B, 1, 5);
        if (GameImage.Rip(loop + 0x9A, 1, 5) != learn || GameImage.Rip(loop + 0x69, 1, 5) != GameImage.Rip(loop + 0x1A, 1, 5))
            throw new InvalidDataException("the unit setup's job loop isn't the code this DLL expects");
        SetupAt = setup;
        LearnJobAt = learn;
        JobJpAt = GameImage.Rip(loop + 0x1A, 1, 5);
        _jobJp = (delegate* unmanaged<byte*, int, int, int, void>)JobJpAt;
        // Its one caller keeps nothing across it (liveregs) and reloads rcx after.
        _setup = (delegate* unmanaged<byte*, byte*, void*, void>)game.Hooks.Hook(
            setup, (nint)(delegate* unmanaged<byte*, byte*, void*, void>)&Setup, "the job level setup");
        // Its one caller (the loop's two calls) keeps only callee-saved registers and its stack across it.
        _learnJob = (delegate* unmanaged<byte*, int, byte*, ushort*, int, ushort*, int, int, void>)game.Hooks.Hook(
            learn, (nint)(delegate* unmanaged<byte*, int, byte*, ushort*, int, ushort*, int, int, void>)&LearnJob,
            "a job's abilities for a unit made from battle data");
    }

    [UnmanagedCallersOnly]
    private static void LearnJob(byte* record, int job, byte* entry, ushort* given, int givenCount, ushort* kept,
        int keptCount, int givenFree)
    {
        _learnJob(record, job, entry, given, givenCount, kept, keptCount, givenFree);
        if ((ushort)job != LastLoopJob)
            return;
        try
        {
            TeachDarkKnight(record, entry, given, givenCount, kept, keptCount, givenFree);
        }
        catch (Exception e)
        {
            Failed("the Dark Knight's abilities", e);
        }
    }

    /// <summary>
    /// After Mime's step, the Dark Knight's, as ReMixed's loop has it for its slot 17: its JP from its level (at least 1,
    /// as a PS1 slot's is) and its abilities, by the remaster's own step (which wants it unlocked, and for the unit's
    /// current job takes its primary). ReMixed's JP for a level is one table for every job; the remaster's (0x2CC008)
    /// gives each generic job the same, but has no Dark Knight row (index 20 reads 3 to 5, seen in the game), so the
    /// Dark Knight's JP comes from the same step on the Squire's slots (index 0, never skipped for a gender), moved to
    /// its own, the Squire's put back. A primary override of Darkness is then known in full: the remaster marks an
    /// override known in index 0, where its menu reads a special skillset, but reads Darkness from index 20.
    /// </summary>
    internal static void TeachDarkKnight(byte* record, byte* entry, ushort* given, int givenCount, ushort* kept,
        int keptCount, int givenFree)
    {
        var level = record[Levels + (DarkKnight >> 1)] >> 4;
        var jp = (ushort*)(record + Jp);
        var total = (ushort*)(record + TotalJp);
        var (squireJp, squireTotal) = (jp[0], total[0]);
        _jobJp(record, record[Gender], 0, Math.Max(level, 1));
        (jp[DarkKnight], total[DarkKnight]) = (jp[0], total[0]);
        (jp[0], total[0]) = (squireJp, squireTotal);
        _learnJob(record, DarkKnightJob, entry, given, givenCount, kept, keptCount, givenFree);
        if (entry != null && entry[PrimaryOverride] == DarkKnightSkillset)
            for (var k = 0; k < 3; k++)
                record[LearnedBits + 3 * DarkKnight + k] = 0xFF;
    }

    /// <summary>Whether a unit's levels meet a job's unlock row (each of the 20 nibbles at least the row's).</summary>
    internal static bool MeetsRow(byte* levels, int job)
    {
        var row = _prerequisites + job * JobRow;
        for (var b = 0; b < 10; b++)
            if ((levels[b] & 0xF0) < (row[b] & 0xF0) || (levels[b] & 0xF) < (row[b] & 0xF))
                return false;
        return true;
    }

    [UnmanagedCallersOnly]
    private static void Setup(byte* record, byte* entry, void* other)
    {
        _setup(record, entry, other);
        if (entry == null)
            return;
        try
        {
            Apply(record, entry, Options.Current);
        }
        catch (Exception e)
        {
            Failed("the job levels", e);
        }
    }

    /// <summary>After the game's setup: ReMixed's levels, the unlocked jobs again, and its random secondary.</summary>
    internal static void Apply(byte* record, byte* entry, Options options)
    {
        var flags = entry[1];
        var levels = record + Levels;
        var mode = Mode(options);
        var secondaryJob = SecondaryJob(record, entry[0xB]);
        var enemies = LevelScaling.OptionsSkipped() ? Options.EnemyJobRule.Normal : options.EnemyJobs;
        if ((flags & 0x20) == 0)
        {
            new Span<byte>(levels, JobRow).Clear();
            Apply(levels, entry, CurrentJob(record, entry), secondaryJob, mode, enemies);
        }
        var bits = _unlocked(levels, flags, record + 0xA2, record[0x1F4]);
        if ((flags & 0x20) == 0 && MeetsRow(levels, DarkKnight))
            bits |= DarkKnightUnlocked; // ReMixed's slot 17 by its row; the remaster's routine never sets it for these
        record[Unlocked] = (byte)(bits >> 16);
        record[Unlocked + 1] = (byte)(bits >> 8);
        record[Unlocked + 2] = (byte)bits;
        if (entry[0xB] != RandomSecondary || (record[6] & 0x20) != 0)
            return;
        var (skillset, job) = RandomSecondaryPick(record);
        record[Secondary] = skillset == 8 && record[Primary] == 0x9C ? (byte)0 : skillset; // the remaster's own exception
        if (skillset != 0)
            Apply(levels, entry, CurrentJob(record, entry), job - Generic, mode, enemies);
    }

    /// <summary>Which jobs get the level: 0 the specified one, 1 also the current, 2 also the secondary's.</summary>
    internal static int Mode(Options options) => options.JobLevels switch
    {
        Options.JobLevelRule.SpecifiedJob => 0,
        Options.JobLevelRule.SpecifiedAndCurrent => 1,
        Options.JobLevelRule.SpecifiedCurrentAndSecondary => 2,
        _ => Math.Clamp(_roster[0] - 1, 0, 2), // ReMixed reads Ramza's sprite set: 1, 2 (chapters 2 and 3), 3
    };

    /// <summary>The current job's generic index, 0 for a unit in its own sprite set's job, or ≥ 20 for none.</summary>
    internal static int CurrentJob(byte* record, byte* entry) =>
        entry[0xA] == record[0] ? 0 : entry[0xA] == DarkKnightJob ? DarkKnight : (byte)(entry[0xA] - Generic);

    /// <summary>The generic index of the job whose skillset is the secondary (its own job's first), else 0xFF.</summary>
    internal static int SecondaryJob(byte* record, byte skillset)
    {
        if (skillset == RandomSecondary)
            return 0xFF;
        if (record[0] < 0x80 && _jobs[record[0] * JobSize] == skillset)
            return 0;
        for (var job = Generic; job < Generic + GenericJobs - 1; job++)
            if (_jobs[job * JobSize] == skillset)
                return job - Generic;
        return _jobs[DarkKnightJob * JobSize] == skillset ? DarkKnight : 0xFF; // ReMixed's Dark Knight slot's skillset
    }

    /// <summary>
    /// ReMixed's 0x8005B1C4 on the 10 level bytes: the level and prerequisites for the specified job, then for
    /// the current and the secondary's as the mode says; Enemy Job Settings first.
    /// </summary>
    internal static void Apply(byte* levels, byte* entry, int current, int secondary, int mode, Options.EnemyJobRule enemies)
    {
        var level = entry[9] & 0xF;
        int first = entry[8];
        if (enemies != Options.EnemyJobRule.Normal && (entry[0x18] & 0x30) != 0)
        {
            level = 8;
            if (enemies == Options.EnemyJobRule.Exceptional)
                first = AllJobs;
        }
        Span<int> jobs = [first, current & 0xFF, secondary & 0xFF];
        Span<byte> eights = stackalloc byte[10];
        eights.Fill(0x88);
        for (var k = 0; k <= Math.Min(mode, 2); k++)
        {
            var job = jobs[k];
            int at, shift, row = job;
            if (job == AllJobs)
                (at, shift) = (9, 0); // ReMixed's quirk: the level lands on Mime's nibble, then everything is 8
            else if (job < GenericJobs)
                (at, shift) = (job >> 1, (job & 1) != 0 ? 0 : 4);
            else if (job == MansBard)
                (at, shift, row) = (Bard >> 1, 0, SharedSlot);
            else if (job == DarkKnight)
            {
                // Past the PS1's 20 nibbles: no "at least 1" for the Dark Knight's or the Onion Knight's.
                levels[DarkKnight >> 1] = (byte)(Math.Max(levels[DarkKnight >> 1] & 0xF0, level << 4) | (levels[DarkKnight >> 1] & 0xF));
                (at, shift) = (-1, 0);
            }
            else
                continue;
            if (at >= 0)
                levels[at] = Merge(levels[at], (byte)(level << shift));
            for (var b = 0; b < 10; b++)
            {
                var need = job == AllJobs ? eights[b] : row == 0 ? (byte)0 : _prerequisites[row * JobRow + b];
                levels[b] = Merge(levels[b], need);
            }
        }
    }

    /// <summary>Each nibble the higher of the two, at least 1 (ReMixed's 0x8005B4B0).</summary>
    internal static byte Merge(byte a, byte b)
    {
        var low = Math.Max(a & 0xF, b & 0xF);
        var high = Math.Max(a & 0xF0, b & 0xF0);
        return (byte)((high == 0 ? 0x10 : high) | (low == 0 ? 1 : low));
    }

    /// <summary>Up to three random jobs, for a skillset that isn't the primary; (0, 0) if none.</summary>
    internal static (byte Skillset, int Job) RandomSecondaryPick(byte* record)
    {
        for (var tries = 0; tries < 3; tries++)
        {
            var job = RandomJob(record);
            if (job == 0)
                break;
            var skillset = _jobs[job * JobSize];
            if (skillset != record[Primary])
                return (skillset, job);
        }
        return (0, 0);
    }

    /// <summary>
    /// ReMixed's unlocked-jobs helper returns 0xFEFFFF while either of the postgame flags in var 0x6C byte 2 is set
    /// (BATTLE 0x80151A60). Its random-job picker consequently sees every generic job. The remaster's picker reads the
    /// three unlocked bytes already stored in the unit, so lend it the same mask for the call and put the real mask
    /// back afterwards.
    /// </summary>
    private static byte RandomJob(byte* record)
    {
        if ((ReadVariable(0x6C) & 0x30000) == 0)
            return _randomJob(record);
        var a = record[Unlocked];
        var b = record[Unlocked + 1];
        var c = record[Unlocked + 2];
        record[Unlocked] = 0xFE;
        record[Unlocked + 1] = record[Unlocked + 2] = 0xFF;
        try
        {
            return _randomJob(record);
        }
        finally
        {
            record[Unlocked] = a;
            record[Unlocked + 1] = b;
            record[Unlocked + 2] = c;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"job levels: {what} failed: {e}");
    }
}
