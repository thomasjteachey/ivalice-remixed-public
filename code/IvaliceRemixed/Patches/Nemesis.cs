using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Nemesis System (docs/MECHANICS.md, mapped from event 0x008 and SCUS 0x8005C9A0): a generic enemy downed
/// but not finished off is remembered, and a few battles later its name comes back on an enemy of the same gender,
/// built like any other for the current chapter. Installed by <see cref="Rules"/> with "nemesis" when the setting is on.
/// </summary>
/// <remarks>
/// <para>The Nemesis lives in event variable 0x40, saved with the game, laid out as ReMixed has it: the name id in bits
/// 0–15, the gender in byte 2 (0x80 a man, 0x40 a woman), and a countdown of battles in the top nibble. Vanilla's events
/// don't use the variable; it goes through the remaster's own getter and setter (0x2332D8, 0x233368).</para>
/// <para>After a battle (event 0x008's step, here before the battle end's write-back, <see cref="BattleEnd"/>): with a
/// Nemesis stored, its countdown drops by one, or once it's 0 is rolled again (3 to 5); with none, an enemy unit (team
/// +0x1EE &amp; 0x30) that's down (Dead, +0x61 &amp; 0x20, or Petrified, +0x62 &amp; 0x80) with a generated name (+0x1FC
/// &amp; 0xF00) is remembered, a man's or a woman's (both: a woman two times in three, as ReMixed's roll has it), with a
/// countdown of 3 to 5. ReMixed's event also checks three values it reads with an opcode (0xC2) that isn't decoded; here
/// any battle that reaches the battle end can remember one.</para>
/// <para>In a battle: the remaster's name picker (0x277CF0; a unit outside the roster, +2 = 0xFF, with no name from its
/// battle data gets a random one by gender, 0x100 a man's, 0x200 a woman's, 0x300 a monster's, at +0x1FC) is hooked. With
/// the countdown at 0, a generic enemy (sprite &amp; 0x80) of the Nemesis's gender that just got a random name takes the
/// Nemesis's instead, unless a unit in the battle already has it (SCUS 0x8005CAC4 → 0x80027270). ReMixed also leaves out
/// units with a preset (+0x169), which only its story battles set.</para>
/// </remarks>
internal static unsafe class Nemesis
{
    public const int Variable = 0x40, NameId = 0x1FC, Team = Battle.UnitTeam;

    private static Game _game = null!;
    private static delegate* unmanaged<byte*, byte*, void> _picker; // the original
    private static int _failed;

    /// <summary>The game's variable getter and setter; the offline check swaps them.</summary>
    internal static delegate* unmanaged<int, int> GetVariable;
    internal static delegate* unmanaged<int, int, int, void> SetVariable;
    /// <summary>The roll for a new countdown and for a man's or a woman's name (0 ≤ result &lt; range); the offline check swaps it.</summary>
    internal static Func<int, int> Roll = range => Random.Shared.Next(range);

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        var picker = image.FindUnique(
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 20 0F B6 41 02 48 8D B1 4C 01 00 00");
        FindVariables(image);
        _picker = (delegate* unmanaged<byte*, byte*, void>)game.Hooks.Hook(
            picker, (nint)(delegate* unmanaged<byte*, byte*, void>)&Picker, "the generic name picker");
        BattleEnd.OnBefore(game, "the Nemesis System", AfterBattle);
    }

    /// <summary>The event variable getter (0x2332D8) and setter (0x233368), for the Nemesis and ReMixed's events.</summary>
    internal static void FindVariables(GameImage image)
    {
        var (get, set) = EventVariables.Find(image);
        GetVariable = (delegate* unmanaged<int, int>)get;
        SetVariable = (delegate* unmanaged<int, int, int, void>)set;
    }

    [UnmanagedCallersOnly]
    private static void Picker(byte* unit, byte* record)
    {
        var random = false;
        try
        {
            random = unit != null && unit[2] == 0xFF && (record == null || *(int*)(record + 0xC) == 0)
                     && (*(ushort*)(unit + NameId) & 0xFF) == 0xFF;
        }
        catch (Exception e)
        {
            Failed("the name picker", e);
        }
        _picker(unit, record);
        try
        {
            if (random && Options.Current.NemesisSystem) // a save's own option; its off bit isn't the Nemesis's
                Give(unit, GetVariable(Variable) & ~SaveOptions.NemesisOff);
        }
        catch (Exception e)
        {
            Failed("the name picker", e);
        }
    }

    /// <summary>A generic enemy that just got a random name takes the Nemesis's when it's due. Returns whether it did.</summary>
    internal static bool Give(byte* unit, int nemesis)
    {
        if (nemesis == 0 || (uint)nemesis >> 28 != 0)
            return false;
        int name = nemesis & 0xFFFF, gender = (nemesis >> 16) & 0xFF;
        if ((unit[0] & 0x80) == 0 || (unit[6] & 0xE0) != gender || (unit[Team] & 0x30) == 0)
            return false;
        var units = _game.TurnCode.Units;
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var other = units + k * Battle.UnitSize;
            if (other != unit && other[1] != 0xFF && *(int*)(other + NameId) == name)
                return false;
        }
        *(int*)(unit + NameId) = name;
        Tracer.Line(_game.Log, $"nemesis: name 0x{name:X3} comes back on a {(gender == 0x80 ? "man" : "woman")} (sprite 0x{unit[0]:X2})");
        return true;
    }

    private static void AfterBattle()
    {
        if (!Options.Current.NemesisSystem) // off for the save in play (SaveOptions): nothing is remembered
            return;
        var before = GetVariable(Variable);
        var after = Next(before & ~SaveOptions.NemesisOff, _game.TurnCode.Units) | (before & SaveOptions.NemesisOff);
        if (after != before)
            SetVariable(Variable, after, 0);
    }

    /// <summary>
    /// The variable after a battle (event 0x008's step): a stored Nemesis's countdown drops, or is rolled again at 0;
    /// with none, a downed generic enemy is remembered.
    /// </summary>
    internal static int Next(int nemesis, byte* units)
    {
        if (nemesis != 0)
        {
            var top = (nemesis >> 24) & 0xFF;
            var countdown = top & 0xF0;
            countdown = countdown != 0 ? countdown - 0x10 : Countdown();
            return (nemesis & 0x00FFFFFF) | ((top & 0x0F) | countdown) << 24;
        }
        int man = 0, woman = 0;
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var unit = units + k * Battle.UnitSize;
            if (unit[1] == 0xFF || (unit[Team] & 0x30) == 0
                || ((unit[Battle.UnitStatuses] & 0x20) == 0 && (unit[Battle.UnitStatuses + 1] & 0x80) == 0))
                continue;
            var name = *(int*)(unit + NameId);
            if ((name & 0xF00) == 0)
                continue;
            switch (unit[6] & 0xC0)
            {
                case 0x80: man = name; break;
                case 0x40: woman = name; break;
            }
        }
        if (man == 0 && woman == 0)
            return 0;
        var gender = man != 0 && woman != 0 ? ((Roll(3) & 2) != 0 ? 0x80 : 0x40) : man != 0 ? 0x80 : 0x40;
        var chosen = gender == 0x80 ? man : woman;
        Tracer.Line(_game.Log, $"nemesis: name 0x{chosen:X3} ({(gender == 0x80 ? "a man" : "a woman")}) is remembered");
        return (chosen & 0xFFFF) | gender << 16 | Countdown() << 24;
    }

    /// <summary>ReMixed's new countdown: (a roll below 40, + 0x30) &amp; 0xF0, so 3 to 5 battles.</summary>
    private static int Countdown() => (Roll(40) + 0x30) & 0xF0;

    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"nemesis: {what} failed: {e}");
    }
}
