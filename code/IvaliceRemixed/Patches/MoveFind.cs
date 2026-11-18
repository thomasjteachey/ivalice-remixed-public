using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Move-Find rework, the finding (docs/analysis/battle-80158000.md §7): a hidden item's panel isn't used up
/// for good. Its first find goes by the Brave roll as in vanilla; after that, in every later battle, it gives its common
/// item once. Installed by <see cref="Rules"/> with "move-find".
/// </summary>
/// <remarks>
/// <para>Vanilla keeps one bit per hidden panel in the save (PS1 0x80059414 + map / 2, 0x80 &gt;&gt; ((map &amp; 1) × 4 +
/// panel); the remaster's 0x11A7680), set by the pick-up (PS1 0x80180278), and a found panel never gives anything
/// again. The roll (100, 100 − Brave) gives the common item (byte 3; the remaster's +4) on a hit, else the rare one
/// (byte 2; +2).</para>
/// <para>ReMixed (0x8015C100 at the map load, 0x8015C200 at the pick-up, 0x801804F0 as the check) keeps two bits per
/// panel for the battle: the rare one taken (loaded from the save's bit, so a panel found before has its rare taken)
/// and the common one taken (cleared each battle). A panel has something while either is left; the pick-up gives the
/// rolled one, or the other when that's taken, and marks it; the save's bit is set as before.</para>
/// <para>Here: the item check (0x271D80, PS1 0x80180380; the battle's copy of the map's four panels at 0xD8DC90, 6 bytes
/// each: x &lt;&lt; 4 | y, flags (0x80 the level, 0x20 no item), the rare item, the common one) is hooked to answer by
/// those bits instead of the save's, and the pick-up (0x271CA8) to give the other item when the rolled one is taken.
/// The battle's bits are read from the save at the battle's first check. ReMixed rolls before its check, and a panel
/// whose common item was taken this battle gives nothing on a roll for it; here the roll comes at the pick-up, so it
/// gives the rare one if that's left (only in the battle of a panel's first find). Not here: the highlighted panels
/// (display).</para>
/// <para>ReMixed's story lock (0x8015C100 at the map load): with the story progress (var 0x6E) below the map's point in
/// its table (0x8015C000, a byte per map; the build's remixed-move-find-lock.bin), each panel's rare item is its common
/// one for the battle and every rare counts as taken, so a find before that point gives the common item, and the save's
/// bit it sets uses the rare one up for good. Here at the battle's first use of the panels (<see cref="Map"/>). Not seen
/// in the game yet.</para>
/// </remarks>
internal static unsafe class MoveFind
{
    public const int Panels = 4, PanelSize = 6, NoItem = 0x20, MapVar = 0x33, StoryVar = 0x6E;
    internal const string LockFileName = "remixed-move-find-lock.bin";
    /// <summary>ReMixed's story point per map (empty without the build's file: no lock).</summary>
    internal static byte[] Thresholds = [];

    private static Game _game = null!;
    private static delegate* unmanaged<int, int, int, byte*> _check;  // the originals
    private static delegate* unmanaged<byte*, byte*> _pickUp;
    private static int _failed;
    private static int _map = -1;
    private static byte _rareTaken, _commonTaken;

    /// <summary>The battle's four panels, the save's found bits, and the event variable getter (swapped offline).</summary>
    internal static byte* Records { get; private set; }
    internal static byte* Found { get; private set; }
    internal static delegate* unmanaged<int, int> Variable;
    /// <summary>The check's result (flags at +0: 1 an item, 2 a trap; +1 the panel; +2 and +4 the items) and the pick-up's (+4 the item given).</summary>
    internal static byte* CheckResult { get; private set; }
    internal static byte* PickUpResult { get; private set; }

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        var check = image.FindUnique(
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 20 33 FF 44 0F B6 E1 40 8A DF");
        var pickUp = image.FindUnique(
            "48 89 5C 24 10 48 89 6C 24 18 48 89 74 24 20 57 48 83 EC 20 44 8A 41 51 48 8B F9 8A 51 50 8A 49 4F 41 C0 E8 07 E8");
        if (GameImage.Rip(pickUp + 0x25, 1, 5) != check)
            throw new InvalidDataException("the pick-up doesn't call the item check");
        // The check's mov [result], bl at +0x29; its records: mov cl, [r13 + rbp*2 + the records] at +0x74 (r13 the image).
        CheckResult = (byte*)GameImage.Rip(check + 0x29, 2, 6);
        if (((byte*)check)[0x74] != 0x41 || ((byte*)check)[0x75] != 0x8A || ((byte*)check)[0x76] != 0x8C)
            throw new InvalidDataException("the item check's records aren't where this DLL expects");
        Records = (byte*)(image.ImageStart + *(int*)(check + 0x78));
        // The pick-up: mov ecx, 0x33; call the variable getter; ... lea r9, [the found bits]; ... lea rax, [the result].
        var getVar = pickUp + 0x7B;
        // (mov ecx, 0x33; mov [the item], ax; call the getter)
        if (*(int*)(getVar + 1) != MapVar || ((byte*)getVar)[0xC] != 0xE8)
            throw new InvalidDataException("the pick-up doesn't read the map from var 0x33");
        Variable = (delegate* unmanaged<int, int>)GameImage.Rip(getVar + 0xC, 1, 5);
        Found = (byte*)GameImage.Rip(pickUp + 0x90, 3, 7);
        PickUpResult = (byte*)GameImage.Rip(pickUp + 0xC4, 3, 7);
        if (CheckResult - PickUpResult != 8)
            throw new InvalidDataException("the pick-up's and the check's results aren't laid out as this DLL expects");

        _check = (delegate* unmanaged<int, int, int, byte*>)game.Hooks.Hook(
            check, (nint)(delegate* unmanaged<int, int, int, byte*>)&Check, "the Move-Find item check");
        _pickUp = (delegate* unmanaged<byte*, byte*>)game.Hooks.Hook(
            pickUp, (nint)(delegate* unmanaged<byte*, byte*>)&PickUp, "the Move-Find pick-up");
        BattleEvents.OnStart(game, "the Move-Find panels", Reset);
        var file = Path.Combine(Path.GetDirectoryName(typeof(MoveFind).Assembly.Location) ?? "", LockFileName);
        if (File.Exists(file))
        {
            Thresholds = File.ReadAllBytes(file);
            game.Log.Info($"move-find: ReMixed's story lock on rare items, {Thresholds.Count(t => t != 0)} map(s)");
        }
        else
            game.Log.Info($"move-find: no {LockFileName}, so no story lock on rare items");
    }

    internal static void Reset() => _map = -1;

    /// <summary>The battle's bits for the current map, read from the save at its first use; before the map's story
    /// point (<see cref="Locked"/>) every rare item is the common one and taken, as ReMixed's map load has it.</summary>
    internal static int Map()
    {
        var map = Variable(MapVar);
        if (map != _map)
        {
            _map = map;
            _rareTaken = (byte)(SavedBits(map) & 0xF);
            _commonTaken = 0;
            if (Locked(map))
            {
                for (var k = 0; k < Panels; k++)
                    *(ushort*)(Records + k * PanelSize + 2) = *(ushort*)(Records + k * PanelSize + 4);
                _rareTaken = 0xF;
                Tracer.Line(_game.Log, $"move-find: map {map}'s rare items locked until story progress {Thresholds[map]}");
            }
        }
        return map;
    }

    /// <summary>Whether the map's rare items wait on a later story point (var 0x6E below its byte in the table).</summary>
    internal static bool Locked(int map) =>
        map >= 0 && map < Thresholds.Length && Thresholds[map] != 0 && Variable(StoryVar) < Thresholds[map];

    /// <summary>The map's four found bits from the save, panel 0 the 8s bit.</summary>
    internal static int SavedBits(int map)
    {
        if (map < 0)
            return 0;
        var b = Found[map / 2];
        return (map & 1) != 0 ? b & 0xF : b >> 4;
    }

    private static int Bit(int panel) => 8 >> panel;

    /// <summary>Whether a panel's rare item is taken (this battle, or found in an earlier one).</summary>
    internal static bool RareTaken(int panel) => (_rareTaken & Bit(panel)) != 0;

    /// <summary>Whether a panel has an item left this battle.</summary>
    internal static bool Left(int panel) => (_rareTaken & _commonTaken & Bit(panel)) == 0;

    /// <summary>The panel at a tile, or -1.</summary>
    internal static int PanelAt(int x, int y, int level)
    {
        for (var k = 0; k < Panels; k++)
        {
            var r = Records + k * PanelSize;
            if (r[0] >> 4 == x && (r[0] & 0xF) == y && r[1] >> 7 == level)
                return k;
        }
        return -1;
    }

    [UnmanagedCallersOnly]
    private static byte* Check(int x, int y, int level)
    {
        var result = _check(x, y, level);
        try
        {
            AfterCheck(x & 0xFF, y & 0xFF, level & 0xFF, result);
        }
        catch (Exception e)
        {
            Failed("the item check", e);
        }
        return result;
    }

    /// <summary>After the game's check: the item flag (and the two items) by this battle's bits instead of the save's.</summary>
    internal static void AfterCheck(int x, int y, int level, byte* result)
    {
        var panel = PanelAt(x, y, level);
        if (panel < 0 || (Records[panel * PanelSize + 1] & NoItem) != 0)
            return;
        Map();
        if (Left(panel))
        {
            result[0] |= 1;
            *(ushort*)(result + 2) = *(ushort*)(Records + panel * PanelSize + 2);
            *(ushort*)(result + 4) = *(ushort*)(Records + panel * PanelSize + 4);
        }
        else
            result[0] &= unchecked((byte)~1);
    }

    [UnmanagedCallersOnly]
    private static byte* PickUp(byte* unit)
    {
        int x = unit[0x4F], y = unit[0x50], level = unit[Battle.UnitLevel] >> 7;
        int panel = -1, map = -1;
        try
        {
            panel = PanelAt(x, y, level);
            map = Map();
        }
        catch (Exception e)
        {
            Failed("the pick-up", e);
        }
        var result = _pickUp(unit);
        try
        {
            if (panel >= 0 && map >= 0)
                Give(panel, (ushort*)(result + 4));
        }
        catch (Exception e)
        {
            Failed("the pick-up", e);
        }
        return result;
    }

    /// <summary>
    /// The pick-up's item (the roll's: the common one at +4 on a hit, else the rare one at +2) swapped for the other when
    /// it's taken this battle, then marked taken. Returns whether it gave the common one.
    /// </summary>
    internal static bool Give(int panel, ushort* item)
    {
        var record = Records + panel * PanelSize;
        ushort rare = *(ushort*)(record + 2), common = *(ushort*)(record + 4);
        var bit = Bit(panel);
        var wantsCommon = *item == common;
        var givesCommon = wantsCommon ? (_commonTaken & bit) == 0 : (_rareTaken & bit) != 0;
        *item = givesCommon ? common : rare;
        if (givesCommon)
            _commonTaken |= (byte)bit;
        else
            _rareTaken |= (byte)bit;
        Tracer.Line(_game.Log, $"move-find: panel {panel} gives its {(givesCommon ? "common" : "rare")} item 0x{*item:X3}" +
                               (givesCommon != wantsCommon ? " (the rolled one is taken)" : ""));
        return givesCommon;
    }

    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"move-find: {what} failed: {e}");
    }
}
