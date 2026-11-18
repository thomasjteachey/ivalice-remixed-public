using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// Guests saved on the sprite sets the build moved the remaster's units off (tools/build_mod.py REMASTER_OLD_GUESTS):
/// the remaster keeps a guest (Agrias in chapter 2, Rafa at Riovanes) in the roster's guest slots (50-53) and finds it
/// there by sprite set (0x2766C8, for a battle unit whose battle data loads its formation, flag 0x08). ReMixed's
/// characters took those guests' second sprite sets (Ashley on Agrias's 0x34), so the mod's battle data has the
/// guests on their first ones (Agrias 0x1E, Rafa 0x29), and a save made with an earlier build may hold one on the old
/// sprite set, which the battle data would no longer find. As a battle's units are made (0x27128C, before any is
/// looked up) a guest slot holding one of them (its old sprite set and the remaster's name id for it) moves to the new
/// sprite set, and its job with it where it's the old slot's own, while the story (var 0x6E) is before the point the
/// remaster's own event takes the guest out of the party; later records on the old sprite set are ReMixed's own
/// characters' (Ashley's, from progress 0x25, with a name id of her own) and stay as they are. Installed by
/// <see cref="Rules"/> with "old-guests".
/// <para>The same hook bridges the Argath campaign's first battle to the roster. ReMixed's opening swaps Algus into
/// roster slot 0, and its 0x006 battle loads him from that slot. The remaster interprets the same entry flag as
/// "search guest slots 50–53 by sprite set" only. For the one player-team Algus record, slot 0 is copied temporarily
/// into guest slot 50 while the game's unit maker runs; the resulting battle unit is then relinked from 50 to 0 and
/// the guest slot restored. Thus his menu, earned EXP/JP, and battle-end write-back belong to the roster leader.</para>
/// <para>An old or constructed save can also reach Araguay Woods without the chapter-two Agrias and Gaffgarion
/// guest records. ReMixed's 0x01D entry asks to load both from those slots; the remaster otherwise leaves empty
/// actors, so their sprites and speech portraits become generic stand-ins. At story progress 0x10 only, a missing
/// 0x17 or 0x1E guest is made from its complete entry data. A normal chapter-two save still uses its roster guest.</para>
/// Data: [[old sprite set, new sprite set, new job, name id, progress from which it's left], ...].
/// </summary>
internal static unsafe class OldGuests
{
    // The battle units' making (0x27128C, from the setup 0x30428C): mov rax, rsp; mov [rax+0x20], r9;
    // mov [rax+0x18], r8; mov [rax+0x10], rdx; mov [rax+8], rcx; push rbx, rbp, rsi, rdi, r12-r15; sub rsp, 0x88;
    // xor r15d, r15d; lea r11, [the image's start]; mov edx, r15d; mov [0xD405A3], r15b.
    internal const string MakeUnitsPattern =
        "48 8B C4 4C 89 48 20 4C 89 40 18 48 89 50 10 48 89 48 08 53 55 56 57 41 54 41 55 41 56 41 57 " +
        "48 81 EC 88 00 00 00 45 33 FF 4C 8D 1D ?? ?? ?? ?? 41 8B D7 44 88 3D";
    internal const int FirstGuestSlot = 0x32, GuestSlots = 4, RosterJob = 2, RosterErrand = 0x11E,
                       RosterName = 0x230;
    internal const int EntryUnits = 16, EntryUnitSize = 40, EntryFlags = 1, EntryTeam = 0x18;
    internal const byte Algus = 0x07, Gaffgarion = 0x17, Agrias = 0x1E, LoadsRoster = 0x08, PlayerControl = 0x08;
    internal const byte OldAgrias = 0x34, Ps1Bard = 0x38, RemasterBard = 0x5B, SideChapterParty = 0x10;
    /// <summary>Araguay's chocobo: a monster (sprite set 0x82) named Boco, whose record asks for him from the roster.</summary>
    internal const byte Monster = 0x82, BocoName = 0x76;
    internal const int EntryName = 2;
    internal const int Lorne = 0x6A, Whitney = 0x6C, Lavian = 0x78, Alicia = 0x79;
    internal const int StoryProgress = 0x6E, AraguayProgress = 0x10;
    // The guest lookup (0x2766C8): test byte [rdx+1], 8; mov rbx, rdx; movzx ecx, byte [rdx]; je; lea eax, [r9+0x33];
    // lea r8, [the first guest slot] (roster slot 0x32). Read here rather than through UnitTables.Find, whose
    // roster walk the level scaling rewrites.
    internal const string GuestLookupPattern = "F6 42 01 08 48 8B DA 0F B6 0A 74 2A 41 8D 41 33 4C 8D 05 ?? ?? ?? ??";

    internal readonly record struct Guest(byte Old, byte Sprites, byte Job, int Name, int Until);

    internal static nint At;
    internal static Guest[] Guests = [];
    private static byte* _roster;
    private static byte* _units;
    private static delegate* unmanaged<int, int> _getVariable;
    private static delegate* unmanaged<nint, nint, nint, nint, long> _make;
    private static Log _log = null!;
    private static int _noted;

    public static string Install(Game game, JsonElement data)
    {
        _log = game.Log;
        Guests = data.EnumerateArray().Select(row => new Guest((byte)row[0].GetInt32(), (byte)row[1].GetInt32(),
            (byte)row[2].GetInt32(), row[3].GetInt32(), row[4].GetInt32())).ToArray();
        _roster = FindRoster(game.Image);
        RepairLoadedSideChapterRoster();
        _units = game.TurnCode.Units;
        _getVariable = (delegate* unmanaged<int, int>)EventVariables.Find(game.Image).Get;
        At = game.Image.FindUnique(MakeUnitsPattern);
        _make = (delegate* unmanaged<nint, nint, nint, nint, long>)game.Hooks.Hook(
            At, (nint)(delegate* unmanaged<nint, nint, nint, nint, long>)&Make, "the battle units' making");
        return "guests saved on the sprite sets ReMixed gave its own characters moved to the remaster's other ones ("
               + string.Join(", ", Guests.Select(g => $"0x{g.Old:X2} to 0x{g.Sprites:X2}")) + ")";
    }

    /// <summary>The roster (.data 0x11A7D10), from the guest lookup's first guest slot.</summary>
    internal static byte* FindRoster(GameImage image)
    {
        var at = image.FindUnique(GuestLookupPattern);
        return (byte*)GameImage.Rip(at + 16, 3, 7) - FirstGuestSlot * UnitTables.RosterSize;
    }

    [UnmanagedCallersOnly]
    private static long Make(nint records, nint a, nint b, nint c)
    {
        Span<byte> savedGuest = stackalloc byte[UnitTables.RosterSize];
        Span<byte> madeFromData = stackalloc byte[EntryUnits];
        madeFromData.Clear();
        var guestCopied = false;
        var leader = LeaderRecord((byte*)records) >= 0 && _roster[0] == Algus && _roster[1] != 0xFF;
        try
        {
            var progress = _getVariable(StoryProgress);
            foreach (var (slot, guest) in Move(_roster, progress, Guests))
                if (_noted++ < 8)
                    _log.Info($"old guests: roster slot {slot} (name id 0x{guest.Name:X}) moved from sprite set "
                              + $"0x{guest.Old:X2} to 0x{guest.Sprites:X2} at story progress 0x{progress:X}");
            var missing = MissingAraguayGuests((byte*)records, _roster, progress, madeFromData);
            if (missing != 0 && _noted++ < 8)
                _log.Info($"old guests: Araguay made {missing} missing story guest(s) from their entry data");
            if (leader)
            {
                var guest = _roster + FirstGuestSlot * UnitTables.RosterSize;
                new Span<byte>(guest, UnitTables.RosterSize).CopyTo(savedGuest);
                new Span<byte>(_roster, UnitTables.RosterSize).CopyTo(new Span<byte>(guest, UnitTables.RosterSize));
                guestCopied = true;
            }
        }
        catch (Exception e)
        {
            if (_noted++ < 8)
                _log.Error($"old guests: {e.Message}");
        }
        long result;
        try
        {
            result = _make(records, a, b, c);
        }
        finally
        {
            RestoreLoads((byte*)records, madeFromData);
            if (guestCopied)
                savedGuest.CopyTo(new Span<byte>(_roster + FirstGuestSlot * UnitTables.RosterSize, UnitTables.RosterSize));
        }
        if (leader && RelinkLeader(_units, FirstGuestSlot) && _noted++ < 8)
            _log.Info("old guests: the Argath campaign's Algus loaded from roster slot 0 as its player leader");
        return result;
    }

    /// <summary>At Araguay, make a missing Agrias, Gaffgarion or Boco from its entry instead of an absent guest slot.
    /// Boco is never there: the remaster looks for a record's roster unit only in the guest slots (0x2766C8), and ReMixed's
    /// chocobo is no guest, so its battle left an empty unit holding his battle id (0x80) and Max HP 0. Its HP conditions
    /// read that as a dead chocobo (0.9.7's early end), and the HP display divided by it (exe+0x38A844, seen in the hidden
    /// PC game, 2026-11-18). On the PS1 the record's own data makes him, as here.</summary>
        internal static int MissingAraguayGuests(byte* records, byte* roster, int progress, Span<byte> changed)
    {
        changed.Clear();
        if (records == null || roster == null || progress != AraguayProgress || changed.Length < EntryUnits)
            return 0;
        var count = 0;
        for (var k = 0; k < EntryUnits; k++)
        {
            var record = records + k * EntryUnitSize;
            var boco = record[0] == Monster && record[EntryName] == BocoName;
            if (record[0] is not (Gaffgarion or Agrias) && !boco || (record[EntryFlags] & LoadsRoster) == 0)
                continue;
            var found = false;
            for (var slot = FirstGuestSlot; slot < FirstGuestSlot + GuestSlots; slot++)
            {
                var guest = roster + slot * UnitTables.RosterSize;
                if (guest[1] != 0xFF && guest[0] == record[0])
                {
                    found = true;
                    break;
                }
            }
            if (found)
                continue;
            record[EntryFlags] &= unchecked((byte)~LoadsRoster);
            changed[k] = 1;
            count++;
        }
        return count;
    }

    /// <summary>Put back entry load bits temporarily suppressed by <see cref="MissingAraguayGuests"/>.</summary>
    internal static void RestoreLoads(byte* records, ReadOnlySpan<byte> changed)
    {
        if (records == null)
            return;
        for (var k = 0; k < Math.Min(EntryUnits, changed.Length); k++)
            if (changed[k] != 0)
                records[k * EntryUnitSize + EntryFlags] |= LoadsRoster;
    }

    /// <summary>The index of the one entry record that asks for player-team Algus from the roster; -1 otherwise.</summary>
    internal static int LeaderRecord(byte* records)
    {
        if (records == null)
            return -1;
        for (var k = 0; k < EntryUnits; k++)
        {
            var record = records + k * EntryUnitSize;
            if (record[0] == Algus && (record[EntryFlags] & LoadsRoster) != 0
                                    && (record[EntryTeam] & PlayerControl) != 0)
                return k;
        }
        return -1;
    }

    /// <summary>The made player Algus linked through the temporary guest slot is linked to roster slot 0 instead.</summary>
    internal static bool RelinkLeader(byte* units, int temporarySlot)
    {
        if (units == null)
            return false;
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var unit = units + k * Battle.UnitSize;
            if (unit[1] == 0xFF || unit[0] != Algus || unit[2] != temporarySlot
                                || (unit[Battle.UnitTeam] & 0x38) != PlayerControl)
                continue;
            unit[2] = 0;
            return true;
        }
        return false;
    }

    /// <summary>Repair records already saved by builds before 0.9.5: Lorne's PS1 Bard job, the three Lionsguard
    /// recruits' 1-A party-state bit (which the remaster displays as an errand), and Agrias on the sprite set now used
    /// by Ashley. Ashley's distinct name id (0x42) keeps her record out of the Agrias conversion.</summary>
    internal static int RepairSideChapterRoster(byte* roster)
    {
        if (roster == null)
            return 0;
        var repaired = 0;
        for (var slot = 0; slot < UnitTables.RosterSlots; slot++)
        {
            var record = roster + slot * UnitTables.RosterSize;
            if (record[1] == 0xFF)
                continue;
            var name = *(int*)(record + RosterName);
            var changed = false;
            if (name == Lorne && record[RosterJob] == Ps1Bard)
            {
                record[RosterJob] = RemasterBard;
                changed = true;
            }
            if (name is Lavian or Alicia or Whitney && (record[RosterErrand] & SideChapterParty) != 0)
            {
                record[RosterErrand] &= unchecked((byte)~SideChapterParty);
                changed = true;
            }
            if (record[0] == OldAgrias && name is Agrias or OldAgrias)
            {
                record[0] = Agrias;
                if (record[RosterJob] == OldAgrias)
                    record[RosterJob] = Agrias;
                changed = true;
            }
            repaired += changed ? 1 : 0;
        }
        return repaired;
    }

    /// <summary>Repair the roster after a save has actually reached the world map. At install time the roster still
    /// belongs to the title screen, so doing this only from <see cref="Install"/> misses every loaded save.</summary>
    internal static void RepairLoadedSideChapterRoster()
    {
        var repaired = RepairSideChapterRoster(_roster);
        if (repaired != 0)
            _log.Info($"old guests: repaired {repaired} side chapter 1-A roster record(s) from an earlier build");
    }

    /// <summary>The guest slots of <paramref name="roster"/> that hold one of <paramref name="guests"/> on its old
    /// sprite set while the story is before its point, moved (sprite set, and job where it's the old slot's own).
    /// Returns the slots moved.</summary>
    internal static List<(int Slot, Guest Guest)> Move(byte* roster, int progress, Guest[] guests)
    {
        var moved = new List<(int, Guest)>();
        for (var slot = FirstGuestSlot; slot < FirstGuestSlot + GuestSlots; slot++)
        {
            var record = roster + slot * UnitTables.RosterSize;
            if (record[1] == 0xFF)
                continue;
            foreach (var guest in guests)
            {
                if (record[0] != guest.Old || *(int*)(record + RosterName) != guest.Name || progress >= guest.Until)
                    continue;
                record[0] = guest.Sprites;
                if (record[RosterJob] == guest.Old)
                    record[RosterJob] = guest.Job;
                moved.Add((slot, guest));
                break;
            }
        }
        return moved;
    }
}
