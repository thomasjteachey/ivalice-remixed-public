using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's two-job stat growth (0x1E5, the Bard and Dancer innate; docs/MECHANICS.md:
/// "Two-job stat growth"; PS1 SCUS 0x8002D200). Installed by <see cref="Supports"/>.
/// </summary>
/// <remarks>
/// When a job's growth and multiplier bytes are copied into a battle unit, a unit with 0x1E5
/// (innate or equipped) and a secondary skillset gets, for each of HP, MP, Speed, PA and MA,
/// the better of its job's and the secondary skillset's job's: the lower growth constant (it
/// divides, so lower grows faster) and the higher multiplier. ReMixed's Job Stats option (flag 0xEA)
/// gives every unit this (<see cref="Options.JobStats"/>; off by default).
/// </remarks>
internal static unsafe class Growth
{
    private static Game _game = null!;
    private static delegate* unmanaged<nint, nint, nint> _afterGrowthCopy; // the original
    private static byte* _jobGrowths; // job records (0x31 bytes) from their growth bytes (.data 0x785E3E)
    private static int _failed;

    private const int TwoJobs = 0x1E5, JobRecord = 0x31, UnitGrowths = 0x8A; // 5 × (growth, multiplier), PS1 +0x81

    /// <summary>Hooks what <paramref name="ids"/> asks for and takes those ids out of it. Returns what it enabled.</summary>
    public static List<string> Install(Game game, HashSet<int> ids)
    {
        if (!ids.Remove(TwoJobs))
            return [];
        _game = game;
        var image = game.Image;
        // Building a unit: the job's 10 growth bytes into the unit (+0x8A), then the step that reads them.
        var copy = image.FindUnique(
            "0F B6 C1 BA 0A 00 00 00 4C 6B C0 31 48 8D 05 ?? ?? ?? ?? 44 8B CA 4C 03 C0 48 8D 8B 8A 00 00 00 " +
            "E8 ?? ?? ?? ?? 48 8B D5 48 8B CB E8 ?? ?? ?? ??");
        var calls = GameImage.Calls(copy, "0F B6 C1 BA 0A 00 00 00 4C 6B C0 31 48 8D 05 ?? ?? ?? ?? 44 8B CA 4C 03 C0 " +
                                    "48 8D 8B 8A 00 00 00 E8 ?? ?? ?? ?? 48 8B D5 48 8B CB E8 ?? ?? ?? ??", "the unit build's growth copy");
        _jobGrowths = (byte*)GameImage.Rip(copy + 12, 3, 7);
        // The job-to-unit copy does the same, and calls the same step right after.
        var jobCopy = image.FindUnique("8D 56 05 44 8B CA 4C 8D 47 0E 48 8D 8B 8A 00 00 00 E8 ?? ?? ?? ?? 48 8B D5 48 8B CB E8 ?? ?? ?? ??");
        if (GameImage.Calls(jobCopy, "8D 56 05 44 8B CA 4C 8D 47 0E 48 8D 8B 8A 00 00 00 E8 ?? ?? ?? ?? 48 8B D5 48 8B CB E8 ?? ?? ?? ??",
                            "the job-to-unit growth copy")[1] != calls[1])
            throw new InvalidDataException("the unit build and the job-to-unit copy don't call the same step after the growth copy");
        _afterGrowthCopy = (delegate* unmanaged<nint, nint, nint>)game.Hooks.Hook(
            calls[1], (nint)(delegate* unmanaged<nint, nint, nint>)&AfterGrowthCopy, "the step after a growth copy");
        return ["0x1E5 two-job stat growth"];
    }

    /// <summary>
    /// Right after either growth copy (the only two callers), where ReMixed's routine replaces
    /// the copy: the unit's growths become the best of both jobs, then the game goes on to use them.
    /// </summary>
    [UnmanagedCallersOnly]
    private static nint AfterGrowthCopy(nint unit, nint other)
    {
        try
        {
            if (unit != 0)
                Combine((byte*)unit);
        }
        catch (Exception e)
        {
            Failed("two-job stat growth", e);
        }
        return _afterGrowthCopy(unit, other);
    }

    /// <summary>ReMixed's rule (PS1 0x8002D200) on a unit whose growths were just copied from its job.</summary>
    internal static bool Combine(byte* unit)
    {
        var job = SecondJob(unit);
        if (job < 0)
            return false;
        var other = _jobGrowths + job * JobRecord;
        var growths = unit + UnitGrowths;
        for (var k = 0; k < 10; k += 2)
        {
            growths[k] = Math.Min(growths[k], other[k]);
            growths[k + 1] = Math.Max(growths[k + 1], other[k + 1]);
        }
        Tracer.Line(_game.Log, $"growth: unit job 0x{unit[3]:X2} also grows like job 0x{job:X2}");
        return true;
    }

    /// <summary>
    /// The job whose growths a unit also gets, or -1: it needs 0x1E5 as its equipped support
    /// (+0x16) or an innate (+0x0A..+0x10), a secondary skillset (+0x13), and not sprite 0x82.
    /// Skillsets 5–23 belong to jobs 0x4A–0x5C and 0xE0 to the Dark Knight (0xA0; ReMixed's 0x4E to its
    /// 0x37 and 0x4D to its 0x38, which the port moves to the remaster's Dark Knight and Bard, see
    /// tools/build_mod.py's DARK_KNIGHT); for anything else a special character's own sprite id is its job.
    /// </summary>
    internal const int DarkKnight = 0xA0, DarkKnightSkillset = 0xE0;

    internal static int SecondJob(byte* unit)
    {
        var secondary = unit[0x13];
        if (secondary == 0 || !HasTwoJobs(unit) || unit[0] == 0x82)
            return -1;
        if ((uint)(secondary - 5) < 19)
            return secondary + 0x45;
        return secondary switch
        {
            DarkKnightSkillset => DarkKnight,
            _ => unit[0] < 128 ? unit[0] : -1,
        };
    }

    private static bool HasTwoJobs(byte* unit) =>
        Options.Current.JobStats == Options.JobStatRule.JobAndSecondary // ReMixed's flag 0xEA
        || *(ushort*)(unit + 0x16) == TwoJobs || *(ushort*)(unit + 0x0A) == TwoJobs || *(ushort*)(unit + 0x0C) == TwoJobs
        || *(ushort*)(unit + 0x0E) == TwoJobs || *(ushort*)(unit + 0x10) == TwoJobs;

    /// <summary>For the offline check: a job's growth bytes.</summary>
    internal static byte* JobGrowths(int job) => _jobGrowths + job * JobRecord;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"growth: {what} failed: {e}");
    }
}
