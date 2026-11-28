using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// Guards on the remaster's writes of battle units into the roster.
/// <para>The write-back (0x274EB8: the unit's fields into the record its byte +2 names). A unit ReMixed's data lists as
/// "taken from the roster" (its battle-data flag 0x08) whose character isn't in the roster is left empty by the
/// remaster (sprite set 0, job 0) with +2 still 0, so the end of the scene (0x2C60E4) wrote it over roster slot 0,
/// Ramza's: seen on the PC after Ashley's scene (ReMixed's 0x040, which lists Ashley that way; the game then crashed on
/// the blank record's missing name, see <see cref="NameGuard"/>). An empty unit is never written back now: the write
/// answers 0xFFFFFFFF ("not written"), which all five of its callers handle.</para>
/// <para>The join now (0x227458, the event instruction 0x43 1, vanilla's CallFunction 1): it writes each unit with the
/// "joins after the event" flag (+6 0x10) into the roster at once but leaves the flag set, and at the scene's end
/// 0x2C60E4 writes each unit still flagged again; it skips one whose sprite set the roster has, but not a generic's
/// (0x80, 0x81). The remaster's own events call it twice (its opening and event 467); ReMixed's 0x0ED makes its two
/// recruits join this way (generics with names of their own, Bryn and Lorne) and then gives them their sprite sets, and
/// each was in the roster twice after the scene (seen on the PC). A unit this join writes has its flag cleared now. It
/// shows no join screen: that's display state 7's (<see cref="BattleEnd.JoinScreenPending"/>), whose writes of the
/// units it shows are logged here.</para>
/// <para>A story character who'd join a second time (<see cref="KeepOutRepeats"/>): before either join, a unit made
/// afresh from its battle data (not loaded from the roster, +6 &amp; 0x08) that would join (+6 &amp; 0x10) under a story
/// name of its own (a name id under 0x100; a generated one is 0x100 and up) the roster already has, doesn't. The
/// remaster's own skip by sprite set misses the Lionsguard knights (generics), and a joiner that saves its formation (+6
/// &amp; 1, the chapter-2 guest Agrias) is written in with no check at all, so after side chapter 1-A the remaster's
/// chapter-2 opening (event 117, battle entry 0x11B) made a second Agrias, Alicia and Lavian, each with her own gear (a
/// player, 2026-11-27). ReMixed's own opening (0x01B) clears the join flag of each one the roster has by name
/// (HAS_CHARACTER), which the remaster's script can't do, so the rule does it for every join, theirs and ReMixed's.</para>
/// Installed with ReMixed's scenarios (<see cref="RemixedScenarios"/>).
/// </summary>
internal static unsafe class RosterGuard
{
    // mov [rsp+0x10], rbx; mov [rsp+0x18], rbp; push rsi, rdi, r12, r14, r15; sub rsp, 0x20; mov bl, [rcx+6];
    // xor r12d, r12d; mov al, [rcx] (0x274EB8).
    internal const string Pattern =
        "48 89 5C 24 10 48 89 6C 24 18 56 57 41 54 41 56 41 57 48 83 EC 20 8A 59 06 45 33 E4 8A 01";
    // mov [rsp+8], rbx; mov [rsp+0x18], rbp; push rsi, rdi, r14; sub rsp, 0x20; mov r14b, cl; lea rbp, [the battle
    // units' +0x191]; xor ebx, ebx; cmp ebx, 0x14 (0x227458).
    internal const string JoinNowPattern =
        "48 89 5C 24 08 48 89 6C 24 18 56 57 41 56 48 83 EC 20 44 8A F1 48 8D 2D ?? ?? ?? ?? 33 DB 83 FB 14";
    internal const uint NotWritten = 0xFFFFFFFF;
    internal const byte JoinsFlag = 0x10, LoadsFormation = 0x08, SavesFormation = 0x01;
    /// <summary>A battle unit's name id (+0x1FC) and a roster record's (+0x230). A story name is under 0xFF: the battle
    /// data's 0xFF asks for a random one, and a generated name is 0x100 and up.</summary>
    internal const int UnitName = 0x1FC, RosterName = 0x230, RandomName = 0xFF;

    internal static nint At, JoinNowAt;
    private static delegate* unmanaged<byte*, int, uint> _game;
    private static delegate* unmanaged<byte, void> _joinNow;
    private static Log _log = null!;
    private static int _noted, _joinsNoted, _repeatsNoted;
    private static bool _joining;
    private static byte* _units, _roster;

    public static void Install(Game game)
    {
        _log = game.Log;
        _units = game.TurnCode.Units;
        _roster = OldGuests.FindRoster(game.Image);
        At = game.Image.FindUnique(Pattern);
        _game = (delegate* unmanaged<byte*, int, uint>)game.Hooks.Hook(
            At, (nint)(delegate* unmanaged<byte*, int, uint>)&WriteBack, "the roster write-back");
        JoinNowAt = game.Image.FindUnique(JoinNowPattern);
        _joinNow = (delegate* unmanaged<byte, void>)game.Hooks.Hook(
            JoinNowAt, (nint)(delegate* unmanaged<byte, void>)&JoinNow, "the event's join now");
    }

    /// <summary>Whether a battle unit is one the remaster left empty: no sprite set and no job.</summary>
    internal static bool Empty(byte* unit) => unit != null && unit[0] == 0 && unit[3] == 0;

    /// <summary>A unit the join now has written: its "joins after the event" flag goes, so the scene's end doesn't write
    /// it again. Whether it had the flag.</summary>
    internal static bool Joined(byte* unit)
    {
        if ((unit[6] & JoinsFlag) == 0)
            return false;
        unit[6] &= unchecked((byte)~JoinsFlag);
        return true;
    }

    /// <summary>The character a name id is: the remaster names its guests Agrias and Rafa by the ids of their second
    /// sprite sets (0x34, 0x19; CharaName "Agrias", "Rapha") and the party's by their own (0x1E, 0x29), and each guest is
    /// sent away before the party's joins (events 158 and 281), so the pairs are one character. 1-A's Agrias is 0x1E.</summary>
    internal static int Character(int name) => name switch
    {
        OldGuests.OldAgrias => OldGuests.Agrias,
        OldGuests.OldRafa => OldGuests.Rafa,
        _ => name,
    };

    /// <summary>The battle units that would join as a story character the roster already has, kept out of the join:
    /// their join and save flags cleared. Returns how many. Called before the join now and the battle end's
    /// write-back.</summary>
    internal static int KeepOutRepeats(byte* units, byte* roster, Action<string>? note)
    {
        if (units == null || roster == null)
            return 0;
        var kept = 0;
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var unit = units + k * Battle.UnitSize;
            if (unit[1] == 0xFF || (unit[6] & JoinsFlag) == 0 || (unit[6] & LoadsFormation) != 0)
                continue;
            var name = *(int*)(unit + UnitName);
            if (name is <= 0 or >= RandomName)
                continue;
            for (var slot = 0; slot < UnitTables.RosterSlots; slot++)
            {
                var record = roster + slot * UnitTables.RosterSize;
                if (record[1] == 0xFF || Character(*(int*)(record + RosterName)) != Character(name))
                    continue;
                unit[6] &= unchecked((byte)~(JoinsFlag | SavesFormation));
                kept++;
                note?.Invoke($"battle unit {k} (battle id 0x{unit[0x191]:X2}, sprite set 0x{unit[0]:X2}, name id 0x{name:X}) "
                             + $"won't join: roster slot {slot} has the character already");
                break;
            }
        }
        return kept;
    }

    /// <summary><see cref="KeepOutRepeats"/> on the game's battle units and roster.</summary>
    internal static void KeepOutRepeats() =>
        KeepOutRepeats(_units, _roster, text =>
        {
            if (_repeatsNoted++ < 16)
                _log.Info($"roster guard: {text}");
        });

    [UnmanagedCallersOnly]
    private static void JoinNow(byte opening)
    {
        KeepOutRepeats();
        _joining = true;
        try
        {
            _joinNow(opening);
        }
        finally
        {
            _joining = false;
        }
    }

    [UnmanagedCallersOnly]
    private static uint WriteBack(byte* unit, int saving)
    {
        if (Empty(unit))
        {
            if (_noted++ < 8)
                _log.Info($"roster guard: an empty battle unit (battle id 0x{unit[0x191]:X2}, roster slot {unit[2]}) not "
                          + "written back into the roster");
            return NotWritten;
        }
        var slot = _game(unit, saving);
        if (_joining && slot != NotWritten && Joined(unit) && _joinsNoted++ < 16)
            _log.Info($"roster guard: battle id 0x{unit[0x191]:X2} (sprite set 0x{unit[0]:X2}) joined at once, into roster "
                      + $"slot {slot}; it won't join again at the scene's end");
        else if (!_joining && slot != NotWritten && BattleEnd.JoinScreenPending > 0)
        {
            // After the write-back has queued its joiners, the next writes are the join screen's, one for each Accept.
            BattleEnd.JoinScreenPending--;
            _log.Info($"join screen: battle id 0x{unit[0x191]:X2} (sprite set 0x{unit[0]:X2}, name id "
                      + $"0x{*(int*)(unit + 0x1FC):X}) joined, into roster slot {slot}");
        }
        return slot;
    }
}
