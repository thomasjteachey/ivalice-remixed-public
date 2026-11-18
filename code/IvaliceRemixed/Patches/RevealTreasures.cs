using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// Reveal Treasures (asked 2026-11-11), from ReMixed's Preparation Menu (event 0x192). As a battle's formation screen
/// opens on a map whose hidden items still hold a rare one and that hasn't been paid for, the screen asks "Reveal item
/// locations for N gil? Current gil: N." in the box it asks "Commence battle?" in. Yes takes the gil and stands
/// ReMixed's arrows over the map's hidden-item panels that still have something (<see cref="TreasureArrows"/>), taken
/// off as the battle begins; a panel without one (no sprite or event slot free) is marked as a yellow tile instead,
/// which the game clears as the battle begins. The price is ReMixed's: a quarter of the
/// prices of the rare and common items of the panels whose rare item is left, at most gil × those panels / 40 (a tenth
/// of the gil at most). A map paid for once (flag 0x100 + the map, where ReMixed keeps it) is revealed for free from
/// then on, as is one whose rare items are all found.
/// </summary>
/// <remarks>
/// The formation's frame (0x1F16F4, rcx the formation; its caller runs the screen's input when it returns 1, and doesn't
/// call it during the opening banner) asks "Commence battle?" through the formation screen's confirm box (the
/// formation UI [[[0x3CD9DA8] + 0x10] + 0x90] + 0x78, the box its +0x7A0, a gadget set up with dialog 16): 0x1489BC(ui,
/// 1, 0) shows it, the gadget's vtable +0x38 (gadget, the answer: −1, then 0 the first choice or 1 the second; the
/// pad's pressed and held words) polls it, and 0x1489BC(ui, 0, 0) hides it. The hook asks the mod's dialog (a UIDialog
/// row the build adds, like dialog 16) the same way: 0x29C6E0 sets the gadget's dialog, 0x29C9A8 its numbers (entries
/// of 0x108 bytes, the value at +0 and the type, 1, at +0x100). While it's up the hook returns 0 (no input for the
/// formation); then it puts dialog 16 back. The marks are panel kind 4, a yellow tile (0xD9C44(controller, kind, on,
/// tile), the controller the GameUIManager's +0x170, [0x3CD9DC8]). The game draws kinds 11 and 12 as the formation's
/// green and blue tiles over any other, so a hidden item on a deployment tile has those taken off it: it shows yellow
/// and stays a tile to deploy on (the game's logic reads only kinds 0, 1 and 13; seen on the PC: Ramza placed on one).
/// The game clears every kind as the battle begins.
/// <para>The payment is written twice. The formation's setup (0x1EE9B4) puts the event variables aside in the
/// formation (the whole 0x400-byte block, vars, flags and nibbles, at its +0x68), and the battle's start after
/// "Commence battle?" Yes (0x1F014C, battle state 0x54) copies them back, so a variable written while the formation
/// is up doesn't last: the gil and the paid flag were back as they were (seen on the PC, 2026-11-12: a second Chocobo
/// Defense after the first was won asked again, with the gil from before paying). ReMixed's Preparation Menu pays
/// before its formation opens. So the payment goes into the block in play (for the formation) and into the
/// formation's copy (for the battle and after).</para>
/// </remarks>
internal static unsafe class RevealTreasures
{
    // 0x1F16F4: the formation's frame (rcx the formation); returns whether the screen's input runs.
    internal const string FormationFrame =
        "48 8B C4 48 89 58 10 48 89 70 18 48 89 78 20 55 41 54 41 55 41 56 41 57 48 8B EC 48 81 EC 80 00 00 00 0F 29 70 C8 " +
        "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 E0 4C 8B 99 98 07 00 00 48 8B F9 4C 89 99 A0 07 00 00";
    // 0x1F1D06: the formation UI, as the frame finds it to poll "Commence battle?" ([the UI manager] +0x10 +0x90 +0x78).
    internal const string FormationUi =
        "48 8B 05 ?? ?? ?? ?? 41 8B D6 89 55 D0 44 89 75 D4 44 88 65 D8 48 8B 48 10 48 8B 81 90 00 00 00 48 8B 48 78";
    // 0x1F1D35: the poll's input (the held word, the pressed one) and the box at the UI's +0x7A0, its vtable +0x38.
    internal const string AnswerPoll =
        "44 0F B7 0D ?? ?? ?? ?? 48 8D 55 D0 44 8B 05 ?? ?? ?? ?? 48 81 C1 A0 07 00 00 48 8B 01 FF 50 38";
    // 0x1489BC: shows (dl 1) or hides (dl 0) the formation UI's confirm box.
    internal const string ShowBox =
        "48 8B C4 48 89 58 08 48 89 70 10 48 89 78 18 4C 89 70 20 55 48 8B EC 48 83 EC 40 48 83 79 50 00 41 8A F0 40 8A FA 48 8B D9";
    // 0x29C6E0: a box's dialog (rcx the box, edx the UIDialog row, r8b, r9b).
    internal const string SetDialog =
        "48 89 5C 24 18 55 56 57 48 81 EC 70 01 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 60 01 00 00 40 32 F6 41 8A E8 48 8B D9 39 91 78 01 00 00";
    // 0x29C9A8: a box's numbers and strings (rcx the box, rdx a vector: +8 the first entry, +0x10 past the last).
    internal const string SetNumbers =
        "40 53 48 83 EC 20 48 8B 81 A8 00 00 00 48 8B D9 48 89 81 B0 00 00 00 48 81 C1 A0 00 00 00";
    // 0xD9C44: a panel kind on a tile (rcx the controller, edx the kind, r8b on, r9d the tile).
    internal const string SetKind =
        "45 8A D8 4C 8B D1 45 85 C9 78 56 41 81 F9 00 02 00 00 73 4D 48 63 D2 8A CA 49 63 C1 48 83 C0 02";
    // 0x255D8C: clears every panel kind through the GameUIManager ([0x3CD9DC8]) +0x170.
    internal const string ClearPanels =
        "48 83 EC 28 33 C9 E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 81 C1 70 01 00 00 48 83 C4 28 E9";
    // 0x1EE9D3, in the formation's setup (0x1EE9B4, rcx the formation): the event variables put aside in it, 8 × 0x80
    // bytes from the block to the formation's +0x68: mov edx, 8; lea rax, [rcx+0x68]; mov r14, rcx; lea rcx, [the
    // block]; lea r8d, [rdx+0x78].
    internal const string KeepVariables = "BA 08 00 00 00 48 8D 41 ?? 4C 8B F1 48 8D 0D ?? ?? ?? ?? 44 8D 42 78";
    // 0x1F0447, in the battle's start after the formation (0x1F014C, rbx the formation): the copy put back: lea rax,
    // [rbx+0x68]; mov [..], esi; lea rcx, [the block]; mov [..], esi; mov [rbx+0x20], r13d; then the copy's first 16.
    internal const string PutBackVariables =
        "48 8D 43 ?? 89 35 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 89 35 ?? ?? ?? ?? 44 89 6B 20 0F 10 00 0F 11 01";

    internal const int CommenceDialog = 16, MarkKind = 4, Tiles = 0x200, Group2Kind = 11, Group1Kind = 12;
    internal const int Mode = 0x75C, Confirming = 0x768, Box = 0x7A0, BoxNode = 0x50, Controller = 0x170;
    internal const int GilVar = 0x2C, PaidFlags = 0x100, NumberBytes = 0x108, NumberType = 0x100, Price = 8;
    private const int WaitFrames = 20; // the box's own wait before the formation polls it is longer (0x1F)

    private static Game _game = null!;
    private static Log _log = null!;
    private static int _dialog;
    private static delegate* unmanaged<nint, byte> _frame;
    private static delegate* unmanaged<nint, byte, byte, void> _showBox;
    private static delegate* unmanaged<nint, int, byte, byte, void> _setDialog;
    private static delegate* unmanaged<nint, nint, void> _setNumbers;
    private static delegate* unmanaged<nint, int, byte, int, void> _setKind;
    private static nint* _uiManager, _gameUi;
    private static uint* _pressed;
    private static ushort* _held;
    private static nint _numbers; // the vector (+8 the first entry, +0x10 past the last) and its two entries after it
    private static bool _due, _asking;
    private static int _frames, _price, _map, _failed;
    /// <summary>Where the formation keeps its copy of the event variables (+0x68), read from its setup's code.</summary>
    internal static int Kept { get; private set; }

    /// <summary>The formation's copy of the event variables: its offset in the formation, from the setup's code (<see
    /// cref="KeepVariables"/>) and the put-back's (<see cref="PutBackVariables"/>), which must name the same offset and
    /// the same block.</summary>
    internal static int FindKept(GameImage image)
    {
        var keep = image.FindUnique(KeepVariables);
        var putBack = image.FindUnique(PutBackVariables);
        var kept = *(byte*)(keep + 8);
        if (*(byte*)(putBack + 3) != kept || GameImage.Rip(keep + 12, 3, 7) != GameImage.Rip(putBack + 10, 3, 7))
            throw new InvalidDataException("the formation doesn't keep the event variables as this DLL expects");
        return kept;
    }

    public static void Install(Game game, string listPath)
    {
        _game = game;
        _log = game.Log;
        using (var document = JsonDocument.Parse(File.ReadAllText(listPath)))
            _dialog = document.RootElement.GetProperty("reveal").GetProperty("dialog").GetInt32();
        if (MoveFind.Records == null)
            throw new InvalidOperationException("the Move-Find rework isn't installed (rule move-find), whose panels it reads");
        var image = game.Image;
        var ui = image.FindUnique(FormationUi);
        _uiManager = (nint*)GameImage.Rip(ui, 3, 7);
        var poll = image.FindUnique(AnswerPoll);
        _held = (ushort*)GameImage.Rip(poll, 4, 8);
        _pressed = (uint*)GameImage.Rip(poll + 12, 3, 7);
        _gameUi = (nint*)GameImage.Rip(image.FindUnique(ClearPanels) + 11, 3, 7);
        _showBox = (delegate* unmanaged<nint, byte, byte, void>)image.FindUnique(ShowBox);
        _setDialog = (delegate* unmanaged<nint, int, byte, byte, void>)image.FindUnique(SetDialog);
        _setNumbers = (delegate* unmanaged<nint, nint, void>)image.FindUnique(SetNumbers);
        _setKind = (delegate* unmanaged<nint, int, byte, int, void>)image.FindUnique(SetKind);
        Kept = FindKept(image);
        _numbers = Marshal.AllocHGlobal(0x20 + 2 * NumberBytes);
        _frame = (delegate* unmanaged<nint, byte>)game.Hooks.Hook(
            image.FindUnique(FormationFrame), (nint)(delegate* unmanaged<nint, byte>)&OnFrame, "the formation's frame");
        BattleEvents.OnStart(game, "reveal treasures", () => (_due, _asking) = (true, false));
        TreasureArrows.Install(game, MarkYellow);
        _log.Info($"reveal treasures: asked as a formation opens (dialog {_dialog}), shown by ReMixed's arrows");
    }

    [UnmanagedCallersOnly]
    private static byte OnFrame(nint formation)
    {
        var result = _frame(formation);
        try
        {
            if (TreasureArrows.Busy)
                TreasureArrows.Step();
            else
                TreasureArrows.Keep(formation);
            if (_asking)
            {
                Poll(formation);
                return 0;
            }
            if (_due && *(int*)(formation + Mode) == 0 && *(int*)(formation + Confirming) == 0)
            {
                _due = false;
                if (Ask())
                    return 0;
            }
            if (TreasureArrows.Busy)
                return 0; // no input for the formation while the arrows are made
        }
        catch (Exception e)
        {
            _asking = false;
            if (Interlocked.Increment(ref _failed) <= 3)
                _log.Error($"reveal treasures: {e.Message}");
        }
        return result;
    }

    /// <summary>The formation UI, or 0.</summary>
    private static nint Ui()
    {
        var manager = *_uiManager;
        if (manager == 0 || *(nint*)(manager + 0x10) == 0)
            return 0;
        var screens = *(nint*)(*(nint*)(manager + 0x10) + 0x90);
        return screens == 0 ? 0 : *(nint*)(screens + 0x78);
    }

    /// <summary>The map's panels with an item (not "no item", flag 0x20).</summary>
    internal static List<int> Panels()
    {
        var panels = new List<int>();
        for (var k = 0; k < MoveFind.Panels; k++)
            if ((MoveFind.Records[k * MoveFind.PanelSize + 1] & MoveFind.NoItem) == 0)
                panels.Add(k);
        return panels;
    }

    /// <summary>ReMixed's price: a quarter of the prices of the panels' rare and common items, at most gil × the panels
    /// / 40.</summary>
    internal static int Cost(IReadOnlyCollection<(int Rare, int Common)> panels, int gil, Func<int, int> price)
    {
        long sum = panels.Sum(p => (long)price(p.Rare) + price(p.Common));
        return (int)Math.Max(0, Math.Min(sum / 4, (long)Math.Max(0, gil) * panels.Count / 40));
    }

    private static int ItemPrice(int item) => *(ushort*)(_game.Routines.GetItem((uint)item) + Price);

    /// <summary>Asks, or reveals for free (a map paid for before, or with its rare items found). Whether it asks.</summary>
    private static bool Ask()
    {
        if (!SaveOptions.Ready)
            return false;
        var map = MoveFind.Map();
        var panels = Panels();
        if (map < 0 || !panels.Any(MoveFind.Left))
            return false;
        var unfound = panels.Where(p => !MoveFind.RareTaken(p)).ToList();
        if (unfound.Count == 0 || SaveOptions.Read(PaidFlags + map) != 0)
        {
            var marked = Mark();
            _log.Info($"reveal treasures: map {map}'s {marked} hidden item(s) shown, for free " +
                      (unfound.Count == 0 ? "(its rare items are found)" : "(paid for before)"));
            return false;
        }
        var gil = SaveOptions.Read(GilVar);
        var items = unfound.Select(p => ((int)*(ushort*)(MoveFind.Records + p * MoveFind.PanelSize + 2),
                                         (int)*(ushort*)(MoveFind.Records + p * MoveFind.PanelSize + 4))).ToList();
        var price = Cost(items, gil, ItemPrice);
        var ui = Ui();
        if (ui == 0 || *(nint*)(ui + BoxNode) == 0)
            return false;
        var box = ui + Box;
        _setDialog(box, _dialog, 1, 0);
        Numbers(_numbers, price, gil);
        _setNumbers(box, _numbers);
        _showBox(ui, 1, 0);
        (_asking, _frames, _price, _map) = (true, 0, price, map);
        _log.Info($"reveal treasures: map {map}, {unfound.Count} panel(s) with their rare item: asked {price} gil of {gil}");
        return true;
    }

    /// <summary>The box's two numbers (the price, the gil) as the game passes them: a vector (+8 the first entry, +0x10
    /// past the last, +0x18 the end) and its entries after it, the value at +0 and the type 1 at +0x100.</summary>
    internal static void Numbers(nint vector, int first, int second)
    {
        new Span<byte>((void*)vector, 0x20 + 2 * NumberBytes).Clear();
        var entries = vector + 0x20;
        *(int*)entries = first;
        *(int*)(entries + NumberType) = 1;
        *(int*)(entries + NumberBytes) = second;
        *(int*)(entries + NumberBytes + NumberType) = 1;
        *(nint*)(vector + 8) = entries;
        *(nint*)(vector + 0x10) = entries + 2 * NumberBytes;
        *(nint*)(vector + 0x18) = entries + 2 * NumberBytes;
    }

    /// <summary>Polls the box; on an answer hides it, puts "Commence battle?" back, and on Yes pays (in the block in play
    /// and in the formation's copy, which the battle's start puts back: the remarks) and marks.</summary>
    private static void Poll(nint formation)
    {
        var ui = Ui();
        if (ui == 0)
        {
            _asking = false;
            return;
        }
        if (++_frames < WaitFrames)
            return;
        var box = ui + Box;
        var answer = stackalloc long[2];
        answer[0] = -1;
        answer[1] = 0;
        var poll = (delegate* unmanaged<nint, long*, uint, uint, void>)(*(nint**)box)[0x38 / 8];
        poll(box, answer, *_pressed, *_held);
        var choice = *(int*)answer;
        if (choice == -1)
            return;
        _asking = false;
        _showBox(ui, 0, 0);
        _setDialog(box, CommenceDialog, 1, 0);
        if (choice != 0)
        {
            _log.Info("reveal treasures: declined");
            return;
        }
        var gil = SaveOptions.Read(GilVar);
        if (gil < _price)
        {
            _log.Info($"reveal treasures: {_price} gil asked, {gil} held");
            return;
        }
        SaveOptions.Write(GilVar, gil - _price);
        SaveOptions.Write(PaidFlags + _map, 1);
        var kept = formation + Kept;
        SaveOptions.WriteIn(kept, GilVar, gil - _price);
        SaveOptions.WriteIn(kept, PaidFlags + _map, 1);
        var marked = Mark();
        _log.Info($"reveal treasures: paid {_price} gil (left {gil - _price}, {SaveOptions.ReadIn(kept, GilVar)} in the " +
                  $"formation's copy, its flag 0x{PaidFlags + _map:X} {SaveOptions.ReadIn(kept, PaidFlags + _map)}); " +
                  $"map {_map}'s {marked} hidden item(s) shown, and free on this map from now on");
    }

    /// <summary>The panels with something left, shown by ReMixed's arrows (marked yellow if they can't be had). How
    /// many.</summary>
    private static int Mark()
    {
        var arrows = Panels().Where(MoveFind.Left).Select(p =>
        {
            var r = MoveFind.Records + p * MoveFind.PanelSize;
            return new TreasureArrows.Arrow(p, r[0] >> 4, r[0] & 0xF, r[1] >> 7, TreasureArrows.PaletteFor(p, MoveFind.RareTaken(p)));
        }).ToList();
        if (!TreasureArrows.Begin(arrows))
            foreach (var arrow in arrows)
                MarkYellow(arrow.Panel);
        return arrows.Count;
    }

    /// <summary>A panel's tile marked yellow for the formation.</summary>
    private static void MarkYellow(int panel)
    {
        var manager = *_gameUi;
        if (manager == 0)
            return;
        var r = MoveFind.Records + panel * MoveFind.PanelSize;
        var tile = (r[1] >> 7) * 256 + (r[0] & 0xF) * _game.Map.Width + (r[0] >> 4);
        if (tile is < 0 or >= Tiles)
            return;
        _setKind(manager + Controller, MarkKind, 1, tile);
        _setKind(manager + Controller, Group1Kind, 0, tile); // a deployment tile's colour would hide the mark
        _setKind(manager + Controller, Group2Kind, 0, tile);
    }
}
