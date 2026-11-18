using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's monster dismissal (docs/MECHANICS.md: "Dismissing a monster gives its poach item"; WORLD 0x8013DC80 and
/// 0x8013DD40): dismissing a monster while a party member has Secret Hunt (Poach, 0x1D7) poaches it, as a battle
/// poach would. Installed by <see cref="Rules"/> with "dismissal-poach".
/// </summary>
/// <remarks>
/// <para>ReMixed (from the party menu's Dismiss, 0x80120634 and, on confirming, 0x80120704): for a monster (job
/// 0x5E–0x8D) it looks through the party for the first member with Secret Hunt (the menu record's +0x126 & 0x40, the
/// support bits built from the equipped support and the innates) who isn't away on an errand (+0x11F & 0xB, a copy of
/// the roster's +0xD0). With one, the confirmation becomes another (dialog type 0x16), and on confirming it rolls
/// 0x8005E0CC(256, chance) with the chance of a battle poach (31/256, or 231 − 2 × that member's Brave with the
/// Poaching Odds setting): the rare item on a hit, the common one otherwise (0x80066064 + 2 × (job − 0x5E)), into the
/// Poachers' Den's stock (0x80059494 + item, unless it's 0xFF), then a message of its own, and the dismissal.</para>
/// <para>Here a hook on the remaster's dismissal (0x284830: the menu record [0x1800F50 + 8 × [0x1811428]]; a unit whose
/// +0x72 is 1–3 or whose roster slot, +0x2C, is 0x32 or more isn't dismissed) does the same before the game dismisses:
/// the poacher from the roster (+0x0A the support, +0x02 the job and its innates, +0x1E Brave), the roll through the
/// game's (0x278E68), the monster's two items through the NXD table the battle poach reads (0x3D325C(nxd, 0x2E),
/// 0x3D3AE8(table, job), 0x3D2FD8; +0x24 common, +0x28 rare), and the item into the stock the battle poach fills
/// (0x0D6FB0, the counts at 0x11A7A1A; at 99 nothing, where a battle pays gil). The errand test reads the roster's
/// +0x11E, the remaster's errand byte (its menu record's +0x167 is a copy, as the PS1's +0x11F is of +0xD0;
/// analysis/world-in-place-review.md).</para>
/// <para>The message isn't ported (parked 2026-11-05): the remaster shows "&lt;name&gt; dismissed." (UIDialog 55, UI
/// text 523) and ReMixed's own confirmation isn't there either. What's known for whoever picks it up: the game builds
/// that dialog with the question, 0xEBF10(menu, 1, type, the record's name +0x126) → 0x11545C → 0x121144 (UIDialog 55–57
/// at +0x228, the question 1–3 at +0x70, through 0x29C6E0, which keeps only the text's ID; the name as lstr0 through
/// 0x29C9A8), and the code says the text comes by ID through 0x3DBE88 and the formatter 0x3F0B10. But on the Deck
/// neither saw texts 520 or 523, repointing row 523 in both copies of the UI text in memory changed nothing, and a read
/// trap on those texts' pages saw no read from the question to 20 s after the result: the dialog draws from a copy
/// made earlier, likely not UTF-8 (a UTF-16 one would have been missed by the memory search, which stopped at 24 hits).
/// The Den's items are the PoachItem table's rows (NXD 236), not the Item table's.</para>
/// </remarks>
internal static unsafe class Dismissal
{
    public const int FirstMonster = 0x5E, Monsters = 48, SecretHunt = 0x1D7, PoachTable = 0x2E;
    private const int Job = 0x24, Slot = 0x2C, Kind = 0x72, MaxSlot = 0x32, RecordCount = 0x37;
    private const int RosterJob = 0x02, RosterSupport = 0x0A, RosterBrave = 0x1E, Full = 99, StockItems = 0x60;
    /// <summary>The roster's errand byte (PS1 +0xD0) and the bits that keep a unit from poaching (ReMixed's & 0xB).</summary>
    internal const int RosterErrand = 0x11E, Away = 0x0B;
    private const int Common = 0x24, Rare = 0x28;

    private static Game _game = null!;
    private static nint* _records;
    private static short* _current;
    private static nint* _cache, _nxd, _manager;
    private static byte* _defaultRow, _stock;
    private static delegate* unmanaged<nint, int, nint> _table;
    private static delegate* unmanaged<nint, int, long*> _row;
    private static delegate* unmanaged<long*, byte*> _rowData;
    private static delegate* unmanaged<nint, int, int, void> _store;
    private static int _failed;
    /// <summary>The game's dismissal (0x284830's original). The offline check swaps it.</summary>
    internal static delegate* unmanaged<void> GameDismiss;
    /// <summary>The monster job's two poach items: (job, common out, rare out), non-zero when found. The offline check swaps it.</summary>
    internal static delegate* unmanaged<int, int*, int*, int> PoachItems;
    /// <summary>Into the stock: (item). The offline check swaps it.</summary>
    internal static delegate* unmanaged<int, void> Store;
    /// <summary>The roll, (256, chance): 0 when it hits. The offline check swaps it.</summary>
    internal static delegate* unmanaged<int, int, int> Roll;
    internal static nint DismissAt { get; private set; }
    internal static nint* Records => _records;
    internal static short* Current => _current;

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        _ = game.Units.Jobs; // found now, so a missing pattern stops this rule here
        // The dismissal: the current record, Ramza's sprite sets (+0x72 1–3) and the roster slot (+0x2C) refused.
        var start = image.FindUnique(
            "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 56 48 83 EC 60 48 0F BF 3D ?? ?? ?? ?? " +
            "4C 8D 35 ?? ?? ?? ?? 49 8B 84 FE ?? ?? ?? ?? 8A 48 72 FE C9 80 F9 02 0F 86 ?? ?? ?? ?? 66 83 78 2C 32");
        if (GameImage.Rip(start + 33, 3, 7) != image.ImageStart)
            throw new InvalidDataException("the dismissal isn't the code this DLL expects");
        _current = (short*)GameImage.Rip(start + 25, 4, 8);
        _records = (nint*)(image.ImageStart + *(int*)(start + 44));
        if (JobChange.Records != null && JobChange.Records != _records)
            throw new InvalidDataException("the dismissal reads other menu records than the job change");

        // The battle poach (in the apply step, after its roll): the monster job's row of the poach table.
        var lookup = image.FindUnique(
            "85 C0 48 8D 35 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 0F B6 78 03 48 8B 05 ?? ?? ?? ?? 74 ?? 48 85 C0 75 ?? " +
            "48 8B 0D ?? ?? ?? ?? 48 85 C9 0F 84 ?? ?? ?? ?? 8D 53 2E E8 ?? ?? ?? ?? 48 89 05 ?? ?? ?? ?? 48 85 C0 " +
            "0F 84 ?? ?? ?? ?? 8B D7 48 8B C8 E8 ?? ?? ?? ?? 4C 8B C0 48 85 C0 0F 84 ?? ?? ?? ?? 48 39 18 7C ?? " +
            "48 8B C8 E8 ?? ?? ?? ?? 48 85 C0 74 ?? 49 39 18 7C ?? 49 8B C8 E8 ?? ?? ?? ?? EB ?? 48 8B C6 8B 78 24");
        // ... the rare item (+0x28), then the stock: the manager, items up to 0x60 counted at 0x11A7A1A, 99 the most.
        var stock = image.FindUnique(
            "48 8B C6 8B 78 28 EB 02 8B FB 48 8B 0D ?? ?? ?? ?? BD 01 00 00 00 89 A9 88 41 00 00 89 B9 8C 41 00 00 " +
            "83 FF 60 0F 87 ?? ?? ?? ?? 48 63 C7 48 8D 15 ?? ?? ?? ?? 80 3C 10 63");
        var store = image.FindUnique("44 8B C5 48 89 A9 94 43 00 00 8B D7 E8");
        if ((ulong)(stock - lookup) > 0x100 || (ulong)(store - stock) > 0x100)
            throw new InvalidDataException("the battle poach isn't the code this DLL expects");
        _defaultRow = (byte*)GameImage.Rip(lookup + 2, 3, 7);
        _cache = (nint*)GameImage.Rip(lookup + 20, 3, 7);
        _nxd = (nint*)GameImage.Rip(lookup + 34, 3, 7);
        _table = (delegate* unmanaged<nint, int, nint>)GameImage.Rip(lookup + 53, 1, 5);
        _row = (delegate* unmanaged<nint, int, long*>)GameImage.Rip(lookup + 79, 1, 5);
        _rowData = (delegate* unmanaged<long*, byte*>)GameImage.Rip(lookup + 104, 1, 5);
        _manager = (nint*)GameImage.Rip(stock + 10, 3, 7);
        _stock = (byte*)GameImage.Rip(stock + 46, 3, 7);
        _store = (delegate* unmanaged<nint, int, int, void>)GameImage.Rip(store + 12, 1, 5);

        Roll = game.TurnCode.Roll;
        PoachItems = &GamePoachItems;
        Store = &GameStore;
        GameDismiss = (delegate* unmanaged<void>)game.Hooks.Hook(
            start, (nint)(delegate* unmanaged<void>)&Dismiss, "the party menu's dismissal");
        DismissAt = start;

    }

    [UnmanagedCallersOnly]
    private static void Dismiss()
    {
        try
        {
            Poach();
        }
        catch (Exception e)
        {
            Failed(e);
        }
        GameDismiss();
    }

    /// <summary>ReMixed's poach for the record about to be dismissed. Returns the item given, or -1.</summary>
    internal static int Poach()
    {
        var item = RollPoach();
        if (item >= 0)
            Store(item);
        return item;
    }

    /// <summary>ReMixed's roll for the record about to be dismissed, nothing stored. Returns the item, or -1.</summary>
    private static int RollPoach()
    {
        var index = *_current;
        if ((uint)index >= RecordCount)
            return -1;
        var record = (byte*)_records[index];
        if (record == null || (byte)(record[Kind] - 1) <= 2 || *(ushort*)(record + Slot) >= MaxSlot)
            return -1; // the game won't dismiss it
        int job = *(ushort*)(record + Job);
        if ((uint)(job - FirstMonster) >= Monsters)
            return -1;
        var poacher = FindPoacher();
        if (poacher == null)
            return -1;
        var chance = Options.Current.PoachingOdds == Options.PoachRule.ByBrave ? PoachOdds.Chance(poacher[RosterBrave]) : 31;
        var rare = Roll(256, chance) == 0;
        int common, rareItem;
        if (PoachItems(job, &common, &rareItem) == 0)
            return -1;
        var item = rare ? rareItem : common;
        Tracer.Line(_game.Log, $"dismissal: job 0x{job:X2} poached for item {item} ({(rare ? "rare" : "common")}, "
                               + $"{chance}/256 with Brave {poacher[RosterBrave]})");
        return item;
    }

    /// <summary>The first roster unit with Secret Hunt, equipped or innate, that isn't away (ReMixed's first party member with it).</summary>
    internal static byte* FindPoacher()
    {
        var units = _game.Units;
        for (var slot = 0; slot < UnitTables.RosterSlots; slot++)
        {
            var unit = units.Roster + slot * UnitTables.RosterSize;
            if (unit[1] == 0xFF || (unit[RosterErrand] & Away) != 0)
                continue;
            if (*(ushort*)(unit + RosterSupport) == SecretHunt)
                return unit;
            var job = unit[RosterJob];
            if (job >= UnitTables.JobCount)
                continue;
            var data = units.Jobs + job * UnitTables.JobSize;
            for (var k = 0; k < 4; k++)
                if (*(ushort*)(data + 1 + 2 * k) == SecretHunt)
                    return unit;
        }
        return null;
    }

    /// <summary>The job's row of the poach table, read as the battle poach reads it.</summary>
    [UnmanagedCallersOnly]
    private static int GamePoachItems(int job, int* common, int* rare)
    {
        var table = *_cache;
        if (table == 0)
        {
            if (*_nxd == 0)
                return 0;
            table = _table(*_nxd, PoachTable);
            *_cache = table;
            if (table == 0)
                return 0;
        }
        var handle = _row(table, job);
        if (handle == null)
            return 0;
        byte* row;
        if (*handle >= 0)
        {
            row = _rowData(handle);
            if (row == null)
                return 0;
        }
        else
            row = _defaultRow;
        *common = *(int*)(row + Common);
        *rare = *(int*)(row + Rare);
        return 1;
    }

    /// <summary>One more in the stock, as the battle poach adds it; nothing once there are 99.</summary>
    [UnmanagedCallersOnly]
    private static void GameStore(int item)
    {
        if ((uint)item <= StockItems && _stock[item] >= Full)
            return;
        _store(*_manager, item, 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"dismissal: the poach failed: {e}");
    }
}
