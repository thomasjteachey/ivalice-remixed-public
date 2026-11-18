using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's random equipment and its Rare Equipment option (docs/MECHANICS.md: "Random battle equipment" and
/// flag 0xF6; PS1 SCUS 0x8005CC98, rewritten, and 0x8005CE4C; BATTLE 0x80160E80; REQUIRE.OUT 0x801C4950).
/// Installed by <see cref="Rules"/> with "random-equipment".
/// </summary>
/// <remarks>
/// A unit with random equipment gets each item from the remaster's item pick (0x277F10: unit, slot category,
/// weapon kind, item type), vanilla's: of the items in the category's id range that the unit may equip, whose
/// level isn't above its own and that aren't rare (+3 &amp; 2), roughly the highest-level ones. ReMixed keeps one
/// candidate per item type, the best its level allows, and every accessory on its own, so a weapon is the best
/// of a random type; the option lets rare items in. <see cref="Pick"/> replaces the pick and draws one number
/// from the game's generator, as the game's does. A Two Swords unit's random left hand is the Koga Knife when
/// its right holds the Iga Knife and the other way round (a stub on that call, 0x27774B). Taking a target's
/// equipment (0x3065C4, for steals and breaks) fails up front for an item with +3 &amp; 1; with the option, a
/// steal (formula 0x26) fails for a rare item, and invited humans don't join after the battle (a stub in the
/// after-battle loop, 0x2C63C2).
/// </remarks>
internal static unsafe class RandomEquipment
{
    private static Game _game = null!;
    private static delegate* unmanaged<byte*, int, int, int, int> _pick; // the originals, as fallbacks
    private static delegate* unmanaged<int> _takeSlot;
    private static int _failed;

    internal const int Weapon = 0x80, Shield = 0x40, Head = 0x20, Body = 0x10, Accessory = 0x08; // the pick's categories
    internal const int IgaKnife = 0x11, KogaKnife = 0x12;
    private const int UnitRightHand = 0x20;                         // u16
    private const int NotTakenFlag = 0x01, RareFlag = 0x02;         // item +3
    private const int StealFormula = 0x26;                          // the Thief's steals; 0x25 and 0x2E break
    private const int ResultSlots = 0x1B, ResultItem = 4;           // the slot bit taken and the item (u16)
    private const int StubSize = 64;

    // Settable for the offline check.
    /// <summary>An item's 12 bytes, +2 level, +3 flags, +5 type (0x2B8C44).</summary>
    internal static delegate* unmanaged<int, byte*> ItemData { get; set; }
    /// <summary>An item's weapon or shield data, +1 the kinds the pick can ask for (0x2B8C74).</summary>
    internal static delegate* unmanaged<int, byte*> WeaponData { get; set; }
    /// <summary>The game's xorshift128: x, y, z, w (0x8E5FC8), as its rolls and the item pick inline it.</summary>
    internal static uint* RandomState { get; set; }
    internal static nint PickAt { get; private set; }
    internal static nint PairAt { get; private set; }
    internal static nint PairStub { get; private set; }
    internal static nint TakeSlotAt { get; private set; }
    internal static nint JoinAt { get; private set; }
    internal static byte* JoinSwitch { get; private set; }
    internal const int JoinStubSize = 96;

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // The item pick: the category's id range (8: 0xD0–0xEF … 0x80: 1–0x7F, else all 256), then each item.
        var pick = image.FindUnique(
            "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 54 41 56 41 57 48 81 EC A0 00 00 00 " +
            "44 0F B6 D2 45 8A F1 45 8A F8 4C 8B E1 41 83 EA 08 74 ?? 41 83 EA 08 74 ?? 41 83 EA 10 74 ?? 41 83 EA 20");
        // Its calls of the item and weapon lookups, and its draw: mov eax, [y]; mov edx, [x].
        var p = (byte*)pick;
        if (p[0x93] != 0xE8 || p[0xD2] != 0xE8 || p[0x121] != 0x8B || p[0x122] != 0x05 || p[0x127] != 0x8B || p[0x128] != 0x15)
            throw new InvalidDataException("the item pick isn't the code this DLL expects");
        var state = (uint*)GameImage.Rip(pick + 0x127, 2, 6);
        var roll = (nint)game.TurnCode.Roll; // mov r8d, [w] first
        if ((uint*)GameImage.Rip(pick + 0x121, 2, 6) != state + 1 || (uint*)GameImage.Rip(roll, 3, 7) != state + 3)
            throw new InvalidDataException("the item pick's random numbers aren't the rolls' generator");
        // The Two Swords unit's left hand: kind 8, any type, after the right hand is stored at +0x20.
        var pair = image.FindUnique("F6 83 9A 00 00 00 01 74 ?? 44 8A CF 41 B0 08 B2 80 48 8B CB E8 ?? ?? ?? ?? 44 8A D8") + 20;
        if (GameImage.Rip(pair, 1, 5) != pick)
            throw new InvalidDataException("the left hand's pick isn't the item pick");
        // Taking equipment: the target's slot by the ability (result +0x1B = 0xFE first; a monster has none).
        var take = image.FindUnique(
            "40 53 48 83 EC 20 4C 8B 05 ?? ?? ?? ?? 4C 8B 0D ?? ?? ?? ?? 41 C6 40 1B FE 41 F6 41 06 20 75 ?? " +
            "0F B7 0D ?? ?? ?? ?? B8 A0 00 00 00");

        ItemData = (delegate* unmanaged<int, byte*>)GameImage.Rip(pick + 0x93, 1, 5);
        WeaponData = (delegate* unmanaged<int, byte*>)GameImage.Rip(pick + 0xD2, 1, 5);
        RandomState = state;
        // Liveregs: every caller of the pick and of the taking keeps only the result.
        _pick = (delegate* unmanaged<byte*, int, int, int, int>)game.Hooks.Hook(
            pick, (nint)(delegate* unmanaged<byte*, int, int, int, int>)&Pick, "the item pick");
        _takeSlot = (delegate* unmanaged<int>)game.Hooks.Hook(
            take, (nint)(delegate* unmanaged<int>)&TakeSlot, "taking equipment");
        PickAt = pick;
        TakeSlotAt = take;

        var stub = GameImage.AllocateCodeNear(pair, StubSize);
        WritePairStub(stub, pick);
        byte[] call = [0xE8, 0, 0, 0, 0];
        BitConverter.GetBytes(checked((int)((nint)stub - (pair + 5)))).CopyTo(call, 1);
        GameImage.WriteCode(pair, call);
        PairAt = pair;
        PairStub = (nint)stub;

        // Whatever the option, as a save's own can change it: with rare items left out, the check is the game's own.
        try
        {
            InstallJoinRule(game);
        }
        catch (Exception e)
        {
            game.Log.Error($"random equipment: invited humans still join, the after-battle check isn't found: {e.Message}");
        }
    }

    /// <summary>
    /// With rare items allowed, an invited human doesn't join (ReMixed's REQUIRE.OUT 0x801C4950): after the battle,
    /// a unit that isn't one of the event's joiners (+6 &amp; 0x10) joins only if it's a monster and invited.
    /// </summary>
    /// <summary>The join check's stub, rbx the unit: with <paramref name="on"/> set (rare items allowed) a human, or a
    /// monster not invited, goes to <paramref name="skip"/>; with it off, as the game: an invited unit joins.</summary>
    internal static void WriteJoinStub(byte* stub, nint join, nint skip, byte* on)
    {
        var code = new List<byte>();
        Switches.Test(code, on);                 // ZF: rare items left out
        code.AddRange([0x74, 0x1A]);             // jz: the game's own check
        code.AddRange([0xF6, 0x43, 0x06, 0x20, 0x74, 0x28]); // test byte [rbx+6], 0x20; jz skip: a human
        code.AddRange([0xF6, 0x43, 0x62, 0x40, 0x74, 0x22]); // test byte [rbx+0x62], 0x40; jz skip: not invited
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]); // jmp [rip+0]: join
        code.AddRange(BitConverter.GetBytes((ulong)join));
        code.AddRange([0xF6, 0x43, 0x62, 0x40, 0x74, 0x0E]); // the game's own: test byte [rbx+0x62], 0x40; jz skip
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]); // jmp [rip+0]: join
        code.AddRange(BitConverter.GetBytes((ulong)join));
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]); // skip: jmp [rip+0]
        code.AddRange(BitConverter.GetBytes((ulong)skip));
        if (code.Count > JoinStubSize)
            throw new InvalidOperationException("the join rule's stub outgrew its space");
        code.ToArray().CopyTo(new Span<byte>(stub, JoinStubSize));
    }

    internal static void InstallJoinRule(Game game)
    {
        // mov r8b, [rbx+6]; test r8b, 0x10; jne join; test byte [rbx+0x62], 0x40 (Invite); je skip; join:
        var at = game.Image.FindUnique("44 8A 43 06 41 F6 C0 10 75 0A F6 43 62 40 0F 84 ?? ?? ?? ??") + 10;
        var join = at + 10;
        var skip = GameImage.Rip(at + 4, 2, 6);
        var stub = GameImage.AllocateCodeNear(at, JoinStubSize);
        JoinSwitch = Switches.Of(o => o.RareEquipment == Options.RareEquipmentRule.Allowed);
        WriteJoinStub(stub, join, skip, JoinSwitch);
        byte[] jump = [0xE9, 0, 0, 0, 0, 0x90, 0x90, 0x90, 0x90, 0x90];
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(at, jump);
        JoinAt = at;
    }

    /// <summary>
    /// The left hand's pick: the Koga Knife for an Iga Knife in the right hand (+0x20) and the other way round,
    /// else on to the item pick. Called with the unit in rcx; changes only rax and the flags before that.
    /// </summary>
    internal static void WritePairStub(byte* stub, nint pick)
    {
        var code = new List<byte>
        {
            0x0F, 0xB7, 0x41, UnitRightHand, // movzx eax, word [rcx+0x20]
            0x83, 0xF8, IgaKnife, 0x74, 0x13, // cmp eax, 0x11; je koga
            0x83, 0xF8, KogaKnife, 0x74, 0x14, // cmp eax, 0x12; je iga
            0xFF, 0x25, 0, 0, 0, 0,           // jmp [rip+0]: the pick
        };
        code.AddRange(BitConverter.GetBytes((ulong)pick));
        code.AddRange([0xB8, KogaKnife, 0, 0, 0, 0xC3]); // koga: mov eax, 0x12; ret
        code.AddRange([0xB8, IgaKnife, 0, 0, 0, 0xC3]);  // iga: mov eax, 0x11; ret
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }

    [UnmanagedCallersOnly]
    private static int Pick(byte* unit, int slot, int kind, int type)
    {
        try
        {
            return Choose(unit, (byte)slot, (byte)kind, (byte)type,
                          Options.Current.RareEquipment == Options.RareEquipmentRule.Allowed);
        }
        catch (Exception e)
        {
            Failed("the item pick", e);
        }
        return _pick(unit, slot, kind, type);
    }

    /// <summary>ReMixed's pick: <see cref="Candidates"/>, then one at random, or 0xFE (none).</summary>
    internal static int Choose(byte* unit, int slot, int kind, int type, bool rare)
    {
        Span<byte> list = stackalloc byte[256];
        var count = Candidates(unit, slot, kind, type, rare, list);
        if (count == 0)
            return 0xFE;
        return list[(int)(((uint)count * NextRandom(RandomState)) >> 15)];
    }

    /// <summary>
    /// ReMixed's candidates (SCUS 0x8005CD0C on), in id order through the category's range: an item the unit may
    /// equip (+0x52, a bit per type), of the asked type (0xFF any) and kind (0 any; its weapon data +1), not rare
    /// unless <paramref name="rare"/>, and not above the unit's level (+0x29). Items of one type in a row make one
    /// candidate, the last that's at least as high as the first; an accessory type (26–31) doesn't group.
    /// </summary>
    internal static int Candidates(byte* unit, int slot, int kind, int type, bool rare, Span<byte> list)
    {
        var (first, end) = slot switch
        {
            Weapon => (1, 0x80), Shield => (0x80, 0x90), Head => (0x90, 0xAC), Body => (0xAC, 0xD0), Accessory => (0xD0, 0xF0),
            _ => (0, 0), // ReMixed: none (vanilla tries every item)
        };
        int count = 0, groupType = 0, groupLevel = 0;
        for (var id = first; id < end; id++)
        {
            var item = ItemData(id);
            int itemType = item[5], level = item[2];
            if ((!rare && (item[3] & RareFlag) != 0)
                || (type != 0xFF && itemType != type)
                || (unit[Battle.UnitEquipTypes + (itemType >> 3)] & (0x80 >> (itemType & 7))) == 0
                || (kind != 0 && (WeaponData(id)[1] & kind) == 0)
                || unit[Battle.UnitLv] < level)
                continue;
            if ((uint)(groupType - 26) < 6)
                groupType = 0;
            if (itemType != groupType)
                groupLevel = 0;
            if (level < groupLevel)
                continue;
            if (itemType == groupType && count > 0)
                list[count - 1] = (byte)id;
            else
            {
                groupType = itemType;
                groupLevel = level;
                list[count++] = (byte)id;
            }
        }
        return count;
    }

    /// <summary>The game's generator, one step (as 0x278E68 inlines it): 15 bits.</summary>
    internal static uint NextRandom(uint* s)
    {
        var t = s[0] ^ (s[0] << 11);
        s[0] = s[1];
        s[1] = s[2];
        var w = s[3];
        s[2] = w;
        s[3] = w ^ (w >> 19) ^ t ^ (t >> 8);
        return s[3] & 0x7FFF;
    }

    [UnmanagedCallersOnly]
    private static int TakeSlot()
    {
        var result = *_game.Battle.TargetResult;
        var before = result != null ? *(ushort*)(result + ResultItem) : (ushort)0;
        var picked = _takeSlot();
        try
        {
            if (picked == 0 && result != null)
            {
                int id = *(ushort*)(result + ResultItem);
                int formula = *_game.Battle.EffectiveFormula;
                if (!CanTake(id, formula, Options.Current.RareEquipment == Options.RareEquipmentRule.Allowed))
                {
                    result[ResultSlots] = 0;
                    *(ushort*)(result + ResultItem) = before;
                    Tracer.Line(_game.Log, $"random equipment: item 0x{id:X3} can't be taken (formula 0x{formula:X2})");
                    return -1;
                }
            }
        }
        catch (Exception e)
        {
            Failed("taking equipment", e);
        }
        return picked;
    }

    /// <summary>
    /// Whether a steal or break may take the item (BATTLE 0x80160E80): never one with +3 &amp; 1, and with rare items
    /// allowed, a steal (formula 0x26) never a rare one of level 1–99.
    /// </summary>
    internal static bool CanTake(int id, int formula, bool rare)
    {
        var item = ItemData(id);
        if ((item[3] & NotTakenFlag) != 0)
            return false;
        return !(rare && formula == StealFormula && (item[3] & RareFlag) != 0 && (uint)(item[2] - 1) < 99);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"random equipment: {what} failed: {e}");
    }
}
