using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's "equipment taken from your own units isn't lost", the stolen half: an item stolen from one of your units
/// comes back to the inventory after the battle. With the mod's Broken equipment setting at BackAfterBattle (not
/// ReMixed's: its broken items go on sale at the Poachers' Den, which the remaster's den has no room for) a broken one
/// comes back the same way, and so does a katana Draw Out breaks (<see cref="KatanaBreak"/>). Installed by
/// <see cref="Rules"/> with "taken-equipment".
/// </summary>
/// <remarks>
/// <para>ReMixed's taking step (PS1 0x8018D3F8, rewritten in place to 0x8018D4D4): for each slot it empties, unless the
/// battle is only simulated ([0x8018F5FC]), when the owner is a roster unit (+2 isn't 0xFF) or a guest on the player's
/// team (+6 &amp; 0x11 clear, +5 &amp; 0x30 clear, +5 &amp; 0x08 set), a steal (the result's flag 0x10) counts the item
/// in a pool (0x80150BA0 + item) and a break counts it in the Poachers' Den's stock (0x80059494 + item). At the battle's
/// end (REQUIRE.OUT 0x801C7354 → 0x80150CB8) the pool goes into the inventory, each item up to 99 owned and
/// equipped.</para>
/// <para>Here: the taking step (0x30BF04; the owner at [0x186AF68], the result at [0x186AF70], the slots u16 at +0x1A)
/// is hooked to note what a steal took; after the battle end's write-back of the units to the roster (0x2C60E4,
/// <see cref="BattleEnd"/>) the pool is added to the inventory (0x11A7C00 + item), capped by the game's own count of
/// owned and equipped (0x278FEC). The pool is cleared at every battle's start. Broken items aren't ported: the
/// remaster's den only stocks what its own list (NXD table 0xEC) has a row for, which ordinary equipment hasn't (as
/// with Draw Out's katana break).</para>
/// </remarks>
internal static unsafe class TakenEquipment
{
    /// <summary>The result's +0x12: <see cref="Steal"/> hands the item to the thief, <see cref="Break"/> only empties the
    /// slot (the taking step tests both, 0x14).</summary>
    public const int Slots = 0x1A, SlotCount = 7, Empty = 0xFF, Items = 0x105, Steal = 0x10, Break = 0x04, MaxOwned = 99;

    private static Game _game = null!;
    private static delegate* unmanaged<void*> _taking;      // the original: returns whether anything was taken
    private static byte** _owner, _result;
    private static int _failed;
    private static readonly byte[] Pool = new byte[Items];

    /// <summary>The inventory (an owned count per item) and the game's owned-and-equipped count. The offline check uses both.</summary>
    internal static byte* Inventory { get; private set; }
    internal static delegate* unmanaged<int, int> Owned { get; private set; }

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        var taking = image.FindUnique(
            "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 55 41 56 41 57 48 83 EC 20 " +
            "4C 8B 05 ?? ?? ?? ?? 33 C0 0F 57 C0 48 89 05 ?? ?? ?? ?? 0F 11 05 ?? ?? ?? ?? 89 05 ?? ?? ?? ?? 41 F6 40 12 14");
        _result = (byte**)GameImage.Rip(taking + 0x1D, 3, 7);
        _owner = (byte**)GameImage.Rip(taking + 0x5C, 3, 7);
        // The owner's slots: lea esi, [rax+0x1A]; then movzx ebx, word [rsi+r9].
        if (((byte*)taking)[0x5C] != 0x4C || ((byte*)taking)[0x63] != 0x8D || ((byte*)taking)[0x65] != Slots)
            throw new InvalidDataException("the taking step isn't laid out as this DLL expects");
        var (inventory, owned) = FindCount(image);
        Inventory = (byte*)inventory;
        Owned = (delegate* unmanaged<int, int>)owned;

        _taking = (delegate* unmanaged<void*>)game.Hooks.Hook(
            taking, (nint)(delegate* unmanaged<void*>)&Taking, "the taking of equipment");
        BattleEnd.OnAfter(game, "the stolen equipment's return", Flush);
        BattleEvents.OnStart(game, "the stolen equipment's pool", Clear);
    }

    /// <summary>The inventory (0x11A7C00, an owned count per item) and the game's count of an item owned and equipped
    /// (0x278FEC: the inventory, the roster's units not in the battle and the player's battle units).</summary>
    internal static (nint Inventory, nint Owned) FindCount(GameImage image) => IvaliceCore.Inventory.FindCount(image);

    [UnmanagedCallersOnly]
    private static void* Taking()
    {
        var before = stackalloc ushort[SlotCount];
        byte* owner = null;
        try
        {
            owner = *_owner;
            if (owner != null)
                new ReadOnlySpan<ushort>(owner + Slots, SlotCount).CopyTo(new Span<ushort>(before, SlotCount));
        }
        catch (Exception e)
        {
            Failed("the taking", e);
            owner = null;
        }
        var r = _taking();
        try
        {
            if (owner != null && *_game.Routines.Simulating == 0)
                Note(owner, *_result, before);
        }
        catch (Exception e)
        {
            Failed("the taking", e);
        }
        return r;
    }

    /// <summary>
    /// After the taking: each slot a steal emptied on one of the player's units goes in the pool, and each one a break
    /// emptied with the Broken equipment setting at BackAfterBattle. Returns how many.
    /// </summary>
    internal static int Note(byte* owner, byte* result, ushort* before)
    {
        if (result == null || !Players(owner))
            return 0;
        var stolen = (result[0x12] & Steal) != 0;
        if (!stolen && ((result[0x12] & Break) == 0 || !BrokenComesBack))
            return 0;
        var noted = 0;
        var slots = (ushort*)(owner + Slots);
        for (var k = 0; k < SlotCount; k++)
        {
            int item = before[k];
            if (item is 0 or Empty || item >= Items || slots[k] != Empty || Pool[item] == 0xFF)
                continue;
            Pool[item]++;
            noted++;
            _game.Log.Info($"taken equipment: item 0x{item:X3} {(stolen ? "stolen from" : "broken on")} unit {owner[Battle.UnitIndex]} comes back after the battle");
        }
        return noted;
    }

    /// <summary>Whether broken equipment comes back after the battle (the mod's Broken equipment setting).</summary>
    internal static bool BrokenComesBack => Options.Current.BrokenEquipment == Options.BrokenEquipmentRule.BackAfterBattle;

    /// <summary>A broken item of the player's (Draw Out's katana) back after the battle, with the setting on; whether
    /// it was pooled.</summary>
    internal static bool Broken(int item)
    {
        if (!BrokenComesBack || item is 0 or Empty || item >= Items || Pool[item] == 0xFF)
            return false;
        Pool[item]++;
        _game?.Log.Info($"taken equipment: item 0x{item:X3} broken (Draw Out) comes back after the battle");
        return true;
    }

    /// <summary>One of the player's units: in the roster (+2), or a guest on the player's team that the player controls.</summary>
    internal static bool Players(byte* unit) =>
        unit[2] != Empty || ((unit[6] & 0x11) == 0 && (unit[5] & 0x30) == 0 && (unit[5] & 0x08) != 0);

    /// <summary>The pool into the inventory (PS1 0x80150CB8): each item up to 99 owned and equipped, then cleared.</summary>
    internal static void Flush()
    {
        for (var item = 1; item < Items; item++)
        {
            int pool = Pool[item];
            if (pool == 0)
                continue;
            Pool[item] = 0;
            int have = Inventory[item];
            var equipped = Math.Max(Owned(item) - have, 0);
            var room = MaxOwned - equipped - have;
            var back = Math.Clamp(pool, 0, Math.Max(room, 0));
            Inventory[item] = (byte)(have + back);
            _game.Log.Info($"taken equipment: {back} of item 0x{item:X3} back in the inventory ({pool} taken)");
        }
    }

    internal static void Clear() => Array.Clear(Pool);

    internal static int Pooled(int item) => Pool[item];

    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"taken equipment: {what} failed: {e}");
    }
}
