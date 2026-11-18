using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Monster Skill and Train range (docs/MECHANICS.md: "Monster Skill and Train range";
/// PS1 0x80182430, rewritten in place, and 0x80151680). Installed by <see cref="Supports"/> for
/// Monster Skill (0x1DE).
/// </summary>
/// <remarks>
/// <para>
/// The game's check (0x30ED60) decides whether a monster skillset's list has its 4th ability. It's
/// called for monster skillsets only: by the battle's list (0x30EC48, skillset type 6) and by the
/// menus that show one (skillsets 0xB0–0xDF). Vanilla counts the unit's four neighbours that are on
/// its current team, can act (not while a reaction is applied, nothing in status group 4), have Monster Skill
/// and stand within 5 of its height.
/// </para>
/// <para>
/// ReMixed's asks the whole battlefield instead, for Monster Skill or Train (0x1D6), with the same
/// team and can-act tests, and gives the monster Move+1 while it holds: it sets the Move+1 bit
/// (+0x9C &amp; 0x80) and adds 1 to Move, once, and takes both back when no one's left. A monster
/// with Move+1 of its own loses it while no one's there, as in ReMixed. The game recomputes Move
/// whenever it rebuilds the ability bits, so the bonus can't pile up.
/// </para>
/// </remarks>
internal static unsafe class MonsterSkill
{
    private static Game _game = null!;
    private static delegate* unmanaged<byte*, int> _check; // the original
    private static int* _reacting; // set while a reaction is applied (0x186AF9C, PS1 0x8018F5F0)
    private static int _failed;

    private const int Id = 0x1DE;
    // Support bytes: Train (0x1D6) is +0x9A bit 7, Monster Skill (0x1DE) +0x9B bit 7.
    private const int TrainByte = 0x9A, MonsterSkillByte = 0x9B, SupportBit = 0x80;
    private const int MovePlusOne = 0x80; // movement byte +0x9C: 0x1E6
    private const int CanActGroup = 4;

    // For the offline check.
    internal static nint CheckAt { get; private set; }
    internal static int* Reacting => _reacting;

    /// <summary>Hooks the check if <paramref name="ids"/> has Monster Skill, and takes it out. Returns what it enabled.</summary>
    public static List<string> Install(Game game, HashSet<int> ids)
    {
        if (!ids.Remove(Id))
            return [];
        _game = game;
        var image = game.Image;
        // The check: 0 for an empty slot; the unit's tile; then the neighbour check at x − 1 (and
        // x + 1, y − 1, y + 1), with the current team (+5) and the tile's height.
        var check = image.FindUnique(
            "48 89 5C 24 10 48 89 6C 24 18 56 57 41 56 48 83 EC 20 80 79 01 FF 48 8B D9 75 04 33 C0 EB ?? " +
            "0F B6 91 BC 01 00 00 48 8D 4C 24 40 E8 ?? ?? ?? ?? 0F B6 73 4F 44 8A 73 05 0F B6 5B 50 41 8A CE " +
            "40 8A 68 06 44 8B C3 44 8A CD 8D 56 FF E8 ?? ?? ?? ??");
        var neighbour = GameImage.Rip(check + 76, 1, 5);
        // The neighbour check's test of each unit: there, on (x, y), no reaction being applied,
        // nothing in group 4, Monster Skill (+0x9B, from rbx = unit + 0x4F), the same current team.
        var test = image.FindUnique(
            "48 8D 1D ?? ?? ?? ?? 80 7B B2 FF 74 ?? 0F B6 03 3B C6 75 ?? 0F B6 43 01 3B C7 75 ?? 83 3D ?? ?? ?? ?? 00 75 ?? " +
            "48 8D 4B B1 BA 04 00 00 00 E8 ?? ?? ?? ?? 85 C0 75 ?? 38 43 4C 7D ?? 40 8A C5 32 43 B6 A8 30");
        var turn = game.TurnCode;
        if (test != neighbour + 81 || (byte*)GameImage.Rip(test, 3, 7) != turn.Units + Battle.UnitX
            || GameImage.Rip(test + 46, 1, 5) != (nint)turn.HasStatusIn)
            throw new InvalidDataException("Monster Skill's check isn't the code this DLL expects");
        _reacting = (int*)GameImage.Rip(test + 28, 2, 7);
        CheckAt = check;
        // Its callers keep nothing but the result across it (liveregs).
        _check = (delegate* unmanaged<byte*, int>)game.Hooks.Hook(
            check, (nint)(delegate* unmanaged<byte*, int>)&Check, "Monster Skill's check");
        return ["0x1DE Monster Skill and Train from anywhere, with Move+1"];
    }

    [UnmanagedCallersOnly]
    private static int Check(byte* unit)
    {
        try
        {
            return Holds(unit) ? 1 : 0;
        }
        catch (Exception e)
        {
            Failed("the check", e);
            return _check(unit);
        }
    }

    /// <summary>
    /// ReMixed's check (PS1 0x80182430): whether an ally anywhere has Monster Skill or Train and can
    /// act, with the Move+1 that comes with it put on or taken off <paramref name="unit"/>.
    /// </summary>
    internal static bool Holds(byte* unit)
    {
        if (unit[1] == 0xFF)
            return false;
        var turn = _game.TurnCode;
        var team = unit[Battle.UnitCurrentTeam] & 0x30;
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var ally = turn.Units + k * Battle.UnitSize;
            if (ally[1] == 0xFF || (ally[Battle.UnitCurrentTeam] & 0x30) != team
                || ((ally[TrainByte] | ally[MonsterSkillByte]) & SupportBit) == 0
                || *_reacting != 0 || turn.HasStatusIn(ally, CanActGroup) != 0)
                continue;
            if ((unit[Battle.UnitMovement] & MovePlusOne) == 0)
            {
                unit[Battle.UnitMovement] |= MovePlusOne;
                unit[Battle.UnitMove]++;
                Tracer.Line(_game.Log, $"monster skill: unit {unit[Battle.UnitIndex]} gets Move+1 from unit {ally[Battle.UnitIndex]}");
            }
            return true;
        }
        if ((unit[Battle.UnitMovement] & MovePlusOne) != 0)
        {
            unit[Battle.UnitMovement] &= unchecked((byte)~MovePlusOne);
            unit[Battle.UnitMove]--;
            Tracer.Line(_game.Log, $"monster skill: unit {unit[Battle.UnitIndex]} loses Move+1");
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"monster skill: {what} failed: {e}");
    }
}
