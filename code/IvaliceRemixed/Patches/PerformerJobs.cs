using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's performers keep their own job open: a unit named Bryn (name id 0x69) or Lorne (0x6A) has job slot 0x5C
/// unlocked whatever its job levels (PS1 SCUS 0x8005DC14, ReMixed's unlocked-jobs helper: from 0x8005DCFC, after the
/// gender rules, bit 0x20 for those two names, which each caller passes in t7: the setup from battle data, the
/// roster's and the job screen's recomputes, a job level gained in battle). Side chapter 1-A's 0x0ED makes them from
/// battle data at job level 1 everywhere, so in ReMixed they join with the Squire, the Chemist and that slot open (read
/// in the PS1's RAM after the scene, 2026-11-18). ReMixed's slot 0x5C is a man's Bard and a woman's Dancer; the port's
/// man's Bard is the remaster's Bard (0x5B, BARD in tools/build_mod.py), so Lorne's bit is the Bard's and Bryn's the
/// Dancer's. Without the rule they had only the Squire and the Chemist, so a job change locked them out of their own.
/// Installed with ReMixed's events (<see cref="RemixedEvents"/>).
/// </summary>
/// <remarks>
/// The remaster works unlocked jobs out in 0x348684 (rcx the job levels, dl the gender, r8 the learned abilities, r9b a
/// last byte; eax the jobs, 0x800000 the Squire, 0x800000 >> (job − 0x4A) a generic job) for a roster record (levels
/// at +0x74, name id +0x230: 0x275B84, 0x275EF4, 0x370054) or a battle unit (levels +0xE4, name id +0x1FC: the setup
/// 0x27699C, a job level gained in battle 0x283664, the menus' units 0x284A08 and 0x360048, 0x39231C). The hook adds
/// the bit for a record in the roster or the battle units. The menus' units are made from a battle unit that can be
/// anywhere (the party menu builds one on the stack from the roster, 0x284F6C, then 0x284A08 works the menu unit's jobs
/// out from it into the menu unit's +0x78, high byte first), so hooks on 0x284A08 and 0x360048 add the bit there too.
/// Their callers, and 0x348684's, keep nothing but the result across them (ExeTool liveregs).
/// </remarks>
internal static unsafe class PerformerJobs
{
    internal const int BrynName = 0x69, LorneName = 0x6A;
    /// <summary>The unlocked-jobs bits of the Dancer (0x5C) and the Bard (0x5B).</summary>
    internal const int DancerBit = 0x800000 >> (0x5C - 0x4A), BardBit = 0x800000 >> (0x5B - 0x4A);
    private const int RosterLevels = 0x74, RosterName = 0x230, RosterGender = 4;
    private const int UnitLevels = 0xE4, UnitName = 0x1FC, UnitGender = 6, Monster = 0x20;
    private const int MenuUnlocked = 0x78; // three bytes, the high first

    /// <summary>The unlocked jobs (0x348684), its call in the job level setup (as JobLevels finds it).</summary>
    internal const string UnlockedCallPattern =
        "44 0F B6 8B F4 01 00 00 4C 8D 83 A2 00 00 00 41 8A D2 49 8B CB E8 ?? ?? ?? ??";
    /// <summary>The party menu's unit from a battle unit (0x284A08: rcx the unit, rdx the menu unit, r8 a roster record):
    /// its prologue and the clearing of the menu unit's 0x280 bytes.</summary>
    internal const string PartyMenuUnitPattern =
        "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 54 41 56 41 57 48 83 EC 20 48 8B DA 49 8B F8 " +
        "48 8B F1 33 D2 48 8B CB 41 B8 80 02 00 00 E8";
    /// <summary>The battle menus' unit from a battle unit (0x360048: rcx the unit, rdx the menu unit, r8d an index).</summary>
    internal const string BattleMenuUnitPattern =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 81 EC 80 00 00 00 48 8B DA " +
        "48 8B F9 48 8B CB 33 D2 41 B8 80 02 00 00 E8";

    private static delegate* unmanaged<byte*, int, byte*, int, int> _unlocked;
    private static delegate* unmanaged<byte*, byte*, nint, void> _partyMenuUnit, _battleMenuUnit;
    private static byte* _roster, _units;
    private static Log _log = null!;
    private static int _failed;

    // For the offline check.
    internal static nint UnlockedAt { get; private set; }
    internal static nint PartyMenuUnitAt { get; private set; }
    internal static nint BattleMenuUnitAt { get; private set; }

    public static void Install(Game game, byte* units)
    {
        _log = game.Log;
        var image = game.Image;
        var unlocked = GameImage.Calls(image.FindUnique(UnlockedCallPattern), UnlockedCallPattern, "the unlocked jobs")[0];
        var party = image.FindUnique(PartyMenuUnitPattern);
        var battle = image.FindUnique(BattleMenuUnitPattern);
        // Each works its menu unit's jobs out from its battle unit (and dl, 0xC0; call 0x348684) within 0x500 bytes.
        foreach (var (at, what) in new[] { (party, "the party menu's unit"), (battle, "the battle menus' unit") })
            if (!CallsWithin(at, 0x500, unlocked))
                throw new InvalidDataException($"{what} doesn't work its unlocked jobs out as this DLL expects");
        _roster = game.Units.Roster;
        _units = units;
        UnlockedAt = unlocked;
        PartyMenuUnitAt = party;
        BattleMenuUnitAt = battle;
        _unlocked = (delegate* unmanaged<byte*, int, byte*, int, int>)game.Hooks.Hook(
            unlocked, (nint)(delegate* unmanaged<byte*, int, byte*, int, int>)&Unlocked, "the unlocked jobs");
        _partyMenuUnit = (delegate* unmanaged<byte*, byte*, nint, void>)game.Hooks.Hook(
            party, (nint)(delegate* unmanaged<byte*, byte*, nint, void>)&PartyMenuUnit, "the party menu's unit");
        _battleMenuUnit = (delegate* unmanaged<byte*, byte*, nint, void>)game.Hooks.Hook(
            battle, (nint)(delegate* unmanaged<byte*, byte*, nint, void>)&BattleMenuUnit, "the battle menus' unit");
    }

    /// <summary>Whether the code from <paramref name="start"/> masks the gender and calls <paramref name="target"/>, as
    /// the menu builders' own call of the unlocked jobs does.</summary>
    private static bool CallsWithin(nint start, int length, nint target)
    {
        var code = new ReadOnlySpan<byte>((void*)start, length);
        for (var k = 0; k + 5 <= code.Length; k++)
            if (code[k] == 0xE8 && start + k + 5 + BitConverter.ToInt32(code.Slice(k + 1, 4)) == target
                && k >= 3 && code[k - 3] == 0x80 && code[k - 2] == 0xE2 && code[k - 1] == 0xC0) // and dl, 0xC0
                return true;
        return false;
    }

    /// <summary>ReMixed's rule: the job a performer named so keeps open (0 for anyone else).</summary>
    internal static int Bit(int name) => name switch
    {
        BrynName => DancerBit,
        LorneName => BardBit,
        _ => 0,
    };

    /// <summary>The bit for the record whose job levels are at <paramref name="levels"/>, if it's a roster record or a
    /// battle unit (0 otherwise, or for a monster).</summary>
    internal static int BitFor(byte* levels)
    {
        var record = levels - RosterLevels;
        if (_roster != null && InArray(record, _roster, UnitTables.RosterSlots, UnitTables.RosterSize))
            return (record[RosterGender] & Monster) != 0 ? 0 : Bit(*(ushort*)(record + RosterName));
        var unit = levels - UnitLevels;
        if (_units != null && InArray(unit, _units, Battle.UnitCount, Battle.UnitSize))
            return UnitBit(unit);
        return 0;
    }

    /// <summary>The bit for a battle unit (battle layout, wherever it is).</summary>
    internal static int UnitBit(byte* unit) =>
        (unit[UnitGender] & Monster) != 0 ? 0 : Bit(*(ushort*)(unit + UnitName));

    /// <summary>Whether <paramref name="record"/> is one of the <paramref name="count"/> records from
    /// <paramref name="first"/>.</summary>
    private static bool InArray(byte* record, byte* first, int count, int size)
    {
        var offset = (long)record - (long)first;
        return offset >= 0 && offset < (long)count * size && offset % size == 0;
    }

    /// <summary>For the offline check: the records it reads.</summary>
    internal static void Point(byte* roster, byte* units)
    {
        _roster = roster;
        _units = units;
    }

    [UnmanagedCallersOnly]
    private static int Unlocked(byte* levels, int gender, byte* learned, int last)
    {
        var bits = _unlocked(levels, gender, learned, last);
        try
        {
            bits |= BitFor(levels);
        }
        catch (Exception e)
        {
            Failed(e);
        }
        return bits;
    }

    [UnmanagedCallersOnly]
    private static void PartyMenuUnit(byte* unit, byte* menu, nint record)
    {
        _partyMenuUnit(unit, menu, record);
        AddToMenu(unit, menu);
    }

    [UnmanagedCallersOnly]
    private static void BattleMenuUnit(byte* unit, byte* menu, nint index)
    {
        _battleMenuUnit(unit, menu, index);
        AddToMenu(unit, menu);
    }

    /// <summary>A menu unit's jobs (+0x78, from its battle unit) with the performer's job open.</summary>
    internal static void AddToMenu(byte* unit, byte* menu)
    {
        try
        {
            var bit = UnitBit(unit);
            if (bit != 0)
            {
                menu[MenuUnlocked] |= (byte)(bit >> 16);
                menu[MenuUnlocked + 1] |= (byte)(bit >> 8);
                menu[MenuUnlocked + 2] |= (byte)bit;
            }
        }
        catch (Exception e)
        {
            Failed(e);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _log.Error($"performer jobs: {e}");
    }
}
