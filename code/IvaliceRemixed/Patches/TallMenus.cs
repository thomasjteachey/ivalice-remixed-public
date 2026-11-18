using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// Event boxes with more than three lines, framed to fit them, and menus with the cursor beside every row (ReMixed's
/// dispatch tavern's menus run to seven rows: docs/analysis/dispatch-missions-plan.md, "The tavern's look"). The
/// remaster's speech windows are drawn for three lines: a box's frame is a nine-grid ("NineGrid00"-"02" in its
/// "Normal", "Round" and "Spike" frames) that repeats its texture past that (mode 2, no top or bottom border), and a
/// menu's cursor goes to two anchors only ("ButtonCollision00" and "01" in its text view, a row apart), so rows past
/// the third showed under the box with no cursor beside them. As a box is laid out, a text with more line breaks
/// stretches its window's frames instead (mode 0, a border of 100 at the top and bottom so the corners keep their
/// shape) to fit them, and they go back for the next box of three lines or fewer; for a row from the third on, the
/// second anchor moves to that row and takes the cursor. A menu with no unit (a portrait-less box the remaster puts in
/// the screen's top left corner) goes under the top window's box.
/// </summary>
/// <remarks>
/// The layout (0x2D9490: rcx the box's record, rdx the message, r9d the line's Id; its one caller keeps r11 across it):
/// the record's floats [0] and [1] the box's place, [0xB] its window as an int (0 the top, 1 the bottom, 2 the
/// narration layer); the message's +0x20 its kind bits (4 a menu). A speech window is placed from its unit and clamped
/// to x 192 and y 75.6 at least: both for a box with no unit. The cursor (0x1365AC: the message window object, the
/// window, the side, the row) goes to the side's anchor for rows 0 and 1 only: the side's record (0x13AAC8) +0x88 and
/// +0x90 (+0x78 and +0x80 in Japanese), each a button (+0x1E0 its collision node, the mouse's), the instance (+0x130) of
/// the node of that name in the text view's layout (the record's +0x30; +0x28 in Japanese), which places it. While a
/// row is under the mouse the menu takes that row every frame, so a moved anchor's collision is off till it's put back.
/// The message window object's +0x40 is the windows' manager: +0x1D0 and +0x1D8 the
/// top window's root node and layout (+0x78), the bottom one's (+0x288). A UI node: +8 its name, +0x18 its parent,
/// +0x34 its type (1 a group, its first child at +0xD0; 12 an instance, its layout at +0x130), +0x38 its position,
/// +0x40 its size (0x3EF684 and 0x3EF6B4 set them); a nine-grid's +0xE8 and +0xF0 its top and bottom borders, +0xF4
/// its mode. 0x3F0794 finds a node by name under a group or a layout's +0x110 (its name argument {char*, id 0}). Seen
/// on the PC (2026-11-12).
/// </remarks>
internal static unsafe class TallMenus
{
    // 0x1365AC: mov [rsp+8], rbx; mov [rsp+0x10], rbp; mov [rsp+0x18], rsi; push rdi; sub rsp, 0x20; mov rax, [rcx];
    // mov esi, r8d; movsxd rbx, r9d; mov ebp, edx; mov rdi, rcx; call [rax+0x10]; test al, al; je; mov rcx, [rdi+0x40].
    internal const string CursorPattern =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 20 48 8B 01 41 8B F0 49 63 D9 8B EA 48 8B F9 FF 50 10 84 C0 74 67 48 8B 4F 40 48 85 C9";
    // 0x2D9490: mov rax, rsp; mov [rax+0x18], rbx; push rbp, rsi, rdi, r12-r15; lea rbp, [rax-0x58]; sub rsp, 0x120;
    // mov rsi, rcx; movaps [rax-0x48], xmm6; movaps [rax-0x58], xmm7.
    internal const string LayoutPattern =
        "48 8B C4 48 89 58 18 55 56 57 41 54 41 55 41 56 41 57 48 8D 68 A8 48 81 EC 20 01 00 00 48 8B F1 0F 29 70 B8 0F 29 78 A8";
    // 0x221C00, the same for battle events' boxes (the tavern's): mov rax, rsp; mov [rax+0x10], rbx; push rbp, rsi, rdi,
    // r12-r15; lea rbp, [rax-0x5F]; sub rsp, 0x90; mov rsi, rcx; movaps [rax-0x48], xmm6; movaps [rax-0x58], xmm7.
    internal const string BattleLayoutPattern =
        "48 8B C4 48 89 58 10 55 56 57 41 54 41 55 41 56 41 57 48 8D 68 A1 48 81 EC 90 00 00 00 48 8B F1 0F 29 70 B8 0F 29 78 A8";
    // 0x135D18, the message window's answer to its buttons (a virtual; rcx the window object, rdx the button, r8d what
    // happened: 0 the cursor on it, 1 picked; r9d its index, a row from the menu's first): mov [rsp+8], rbx; push rdi;
    // sub rsp, 0x20; mov rbx, rcx; mov edi, r9d; mov rcx, [rcx+0x40]; test rcx, rcx; je; mov eax, [rdx+0x20];
    // cmp [rcx+0x20], eax; jne; test r8d, r8d.
    internal const string ButtonPattern =
        "48 89 5C 24 08 57 48 83 EC 20 48 8B D9 41 8B F9 48 8B 49 40 48 85 C9 74 66 8B 42 20 39 41 20 75 5E 45 85 C0";
    // 0x136A7C, a text's width in a box (rcx the message window object, rdx the text, r8d the window, r9d the side, a
    // byte its +0xB8): push rbx, rbp, rsi, rdi; sub rsp, 0x208; mov rax, [the cookie]; xor rax, rsp; ...;
    // mov rbp, [rcx+0x40]; mov ebx, r8d; mov rsi, rdx.
    internal const string WidthPattern =
        "40 53 55 56 57 48 81 EC 08 02 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 F0 01 00 00 48 8B 69 40 41 8B D8 48 8B F2";
    internal const string SetPositionPattern =
        "39 51 38 75 06 44 39 41 3C 74 22 F6 81 8C 00 00 00 03 89 51 38 44 89 41 3C C6 81 95 00 00 00 01";
    internal const string SetSizePattern =
        "39 51 40 75 06 44 39 41 44 74 43 F6 81 8C 00 00 00 0C 89 51 40 44 89 41 44 C6 81 95 00 00 00 01";
    internal const string FindPattern =
        "48 89 5C 24 08 57 48 83 EC 20 48 8B 99 D0 00 00 00 48 8B FA 48 85 DB 74 61 83 7B 10 00 74 0C 8B 47 08 85 C0 74 05 39 43 10 EB 1F";
    // 0x221994, the text of a box's line (rcx the box's record, its window at +0x2C; r8d the line's Id), for a box as it
    // opens (0x221C00, 0x2D9490) and for one kept open as it takes its next line (0x22284C, 0x2DA018; all four of its
    // callers keep r11 across it): sub rsp, 0x28; movsxd rax, [rcx+0x2C]; xor cl, cl; imul rdx, rax, 0x298;
    // mov rax, [rip]; cmp [rdx+rax+0x2CA4], cl; je; test byte [rdx+rax+0x2CA0], 0x80.
    internal const string LineTextPattern =
        "48 83 EC 28 48 63 41 2C 32 C9 48 69 D0 98 02 00 00 48 8B 05 ?? ?? ?? ?? 38 8C 02 A4 2C 00 00 74 0C F6 84 02 A0 2C 00 00 80";
    // Whether the player pressed on in a box (ecx its window; the action checks 0xFC598 of confirm, 0x1A, and the
    // others its window's +0x2F30 bits leave): 0x22169C for battle events' boxes, its copy 0x2D9394 for the world's. A
    // box typing finishes its text at a press, one done goes on (closed, or kept open by its style's 0x80 for its
    // window's next line). mov rax, rsp; mov [rax+8], rbx; ... push r12, r14, r15; sub rsp, 0x20; mov r14, [rip];
    // xor ebx, ebx; mov r15, [rip].
    internal const string PressedPattern =
        "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 54 41 56 41 57 48 83 EC 20 4C 8B 35 ?? ?? ?? ?? 33 DB 4C 8B 3D ?? ?? ?? ??";
    // In the cursor's code: the side's record (call at +0x37), the language (+0x41: mov rcx, [rip]), the UI's root
    // (+0x62: mov rax, [rip]) and the cursor's move to an anchor (call at +0x84).
    internal const int RecordCall = 0x37, LanguageAt = 0x41, UiAt = 0x62, FocusCall = 0x84;

    internal const int Manager = 0x40, Roots = 0x1D0, Layouts = 0x1D8, TopWindow = 0x78, BottomWindow = 0x288, LayoutNodes = 0x110;
    internal const int Kind = 0x20, MenuKind = 4, RecordWindow = 0xB, Row = 0x84, FirstRow = 0x90;
    internal const int Name = 8, Parent = 0x18, Type = 0x34, Position = 0x38, Size = 0x40, Instance = 0x130;
    internal const int Top = 0xE8, Bottom = 0xF0, Mode = 0xF4, Stretch = 0, Border = 100, AnchorCollision = 0x1E0;
    // A node anchored to its parent (a frame's nine-grid to the frame, sideways only: its +0x8C flags 5): +0x74-0x80
    // its margins from the parent's left, top, right and bottom (floats; the nine-grids' -7, -23, -10, -23 and -6, -24,
    // -9, -22), its width the parent's less its side margins, its height its own. The size setter makes both sizes its
    // own (the margins marked to be worked out again from them), so after it the right margin goes back.
    internal const int RightMargin = 0x7C, BottomMargin = 0x80;
    internal const int Lines = 3, RowHeight = 54; // the boxes' three lines; the anchors' step (−4 and 50)
    internal const float LeftLimit = 192f, TopLimit = 75.6f, BottomLimit = 1004.4f;
    internal const int UnderTopY = 60, BoxHeight = 210; // a box of three lines, as its root is
    // Where a tavern menu's middle goes across the screen (1920 wide): the PS1 centres each at x 124 of its 256 (a 4:3
    // picture 1440 wide in the middle of the remaster's).
    internal const int MenuMiddle = 940;
    internal const int Slack = 80; // about three characters
    internal static readonly string[] Frames = ["Normal", "Round", "Spike"], Grids = ["NineGrid00", "NineGrid01", "NineGrid02"];
    internal static readonly string[] Anchors = ["ButtonCollision00", "ButtonCollision01"];
    // A side's record: +0x30 its text view's layout (+0x28 in Japanese), where the anchors' nodes are; the box's record
    // [0xC] its side (0 none, 1 the portrait on the left, 2 on the right) and [2] its width, the side's padding
    // (the manager's +0x6C8 with a portrait, +0x6CC without) and the text's (0x136A7C: at least the manager's +0x6D0,
    // at most its +0x6D4).
    internal const int TextLayout = 0x30, JapaneseTextLayout = 0x28, RecordSide = 0xC, RecordWidth = 2;
    // The record's [0x13]: the box's frame as the window opens (the manager's +0x280 and +0x490 keep it: 0 "Normal",
    // the speech boxes', 1 "Round", the remaster's menus', 2 "Spike"); [0x14] its tail (0 none, 1 above, 2 below);
    // [0x16] the tail's x from the box's left (in a battle event: towards its unit, the screen's left edge with none).
    internal const int RecordFrame = 0x13, NormalFrame = 0, RecordTail = 0x14, RecordTailX = 0x16;
    internal const int PortraitPadding = 0x6C8, NoPortraitPadding = 0x6CC, LeastWidth = 0x6D0;
    internal const int Centring = 0xAF; // the message window object's: a MESSAGE's centring byte, for one layout

    private static delegate* unmanaged<nint, int, int, int, void> _cursor;
    private static delegate* unmanaged<nint, nint, int, int, void> _button;
    private static delegate* unmanaged<nint, nint, int, int, byte, int> _width;
    private static delegate* unmanaged<nint, nint, nint, int, void> _layout, _battleLayout;
    private static delegate* unmanaged<nint, int, int, nint> _record;
    private static delegate* unmanaged<nint, void> _focus;
    private static delegate* unmanaged<nint, int, int, void> _setPosition, _setSize;
    private static delegate* unmanaged<nint, nint, nint> _find;
    private static delegate* unmanaged<nint, nint, int, nint> _lineText;
    private static delegate* unmanaged<int, byte> _pressedBattle, _pressedWorld;
    // Whether each speech window (0 the top, 1 the bottom) shows one of ReMixed's frameless lines now.
    private static readonly bool[] Plain = new bool[2];
    private static nint* _language;
    private static nint* _ui;
    private static Log _log = null!;
    private static readonly Dictionary<string, nint> Names = [];
    private static readonly Dictionary<nint, (int Window, int Height, float Right, float Margin, int Top, int Bottom, int Mode)> Stretched = [];
    private static (nint Node, int Y, nint Collision, int Width, int Height) _moved;
    private static int _movedRow = -1;
    private static int _noted, _fitted;

    /// <summary>For the offline check: where its hooks and calls go.</summary>
    internal static nint CursorAt, LayoutAt, BattleLayoutAt, ButtonAt, RecordAt, FocusAt, LineTextAt;
    internal static IReadOnlyList<nint> PressedAt = [];

    public static void Install(Game game)
    {
        _log = game.Log;
        var image = game.Image;
        CursorAt = image.FindUnique(CursorPattern);
        LayoutAt = image.FindUnique(LayoutPattern);
        BattleLayoutAt = image.FindUnique(BattleLayoutPattern);
        ButtonAt = image.FindUnique(ButtonPattern);
        RecordAt = GameImage.Rip(CursorAt + RecordCall, 1, 5);
        FocusAt = GameImage.Rip(CursorAt + FocusCall, 1, 5);
        _record = (delegate* unmanaged<nint, int, int, nint>)RecordAt;
        _focus = (delegate* unmanaged<nint, void>)FocusAt;
        _language = (nint*)GameImage.Rip(CursorAt + LanguageAt, 3, 7);
        _ui = (nint*)GameImage.Rip(CursorAt + UiAt, 3, 7);
        _setPosition = (delegate* unmanaged<nint, int, int, void>)image.FindUnique(SetPositionPattern);
        _setSize = (delegate* unmanaged<nint, int, int, void>)image.FindUnique(SetSizePattern);
        _find = (delegate* unmanaged<nint, nint, nint>)image.FindUnique(FindPattern);
        _width = (delegate* unmanaged<nint, nint, int, int, byte, int>)image.FindUnique(WidthPattern);
        foreach (var name in Frames.Concat(Grids).Concat(Anchors))
            Names[name] = Marshal.StringToCoTaskMemUTF8(name);
        _cursor = (delegate* unmanaged<nint, int, int, int, void>)game.Hooks.Hook(
            CursorAt, (nint)(delegate* unmanaged<nint, int, int, int, void>)&OnCursor, "an event menu's cursor");
        _button = (delegate* unmanaged<nint, nint, int, int, void>)game.Hooks.Hook(
            ButtonAt, (nint)(delegate* unmanaged<nint, nint, int, int, void>)&OnButton, "an event menu's buttons");
        _layout = (delegate* unmanaged<nint, nint, nint, int, void>)game.Hooks.Hook(
            LayoutAt, Stub.KeepingRegisters((nint)(delegate* unmanaged<nint, nint, nint, int, void>)&OnLayout, near: LayoutAt),
            "an event box's layout");
        _battleLayout = (delegate* unmanaged<nint, nint, nint, int, void>)game.Hooks.Hook(
            BattleLayoutAt,
            Stub.KeepingRegisters((nint)(delegate* unmanaged<nint, nint, nint, int, void>)&OnBattleLayout, near: BattleLayoutAt),
            "a battle event box's layout");
        LineTextAt = image.FindUnique(LineTextPattern);
        _lineText = (delegate* unmanaged<nint, nint, int, nint>)game.Hooks.Hook(
            LineTextAt, Stub.KeepingRegisters((nint)(delegate* unmanaged<nint, nint, int, nint>)&OnLineText, near: LineTextAt),
            "an event box's line");
        PressedAt = image.FindExactly(PressedPattern, 2);
        _pressedBattle = (delegate* unmanaged<int, byte>)game.Hooks.Hook(
            PressedAt[0], (nint)(delegate* unmanaged<int, byte>)&OnPressedBattle, "a battle event box's press on");
        _pressedWorld = (delegate* unmanaged<int, byte>)game.Hooks.Hook(
            PressedAt[1], (nint)(delegate* unmanaged<int, byte>)&OnPressedWorld, "a world event box's press on");
        _log.Info("tall menus: event boxes past three lines framed to fit, a menu's cursor beside each row, ReMixed's " +
                  "frameless lines on without a press");
    }

    /// <summary>A box's line looked up as it opens or takes its next line: whether its window now shows one of
    /// ReMixed's frameless lines (<see cref="RemixedText.PlainLines"/>).</summary>
    [UnmanagedCallersOnly]
    private static nint OnLineText(nint record, nint unused, int line)
    {
        var text = _lineText(record, unused, line);
        var window = *(int*)(record + RecordWindow * 4);
        if (window is 0 or 1)
            Plain[window] = RemixedText.PlainLines.Contains(line);
        return text;
    }

    /// <summary>A frameless line's box goes on as if pressed: its text shows whole and it's done at once, so the event's
    /// wait for it ends as on the PS1 (its style keeps it open: the Treasure Wheel's top and bottom lines).</summary>
    [UnmanagedCallersOnly]
    private static byte OnPressedBattle(int window) => window is 0 or 1 && Plain[window] ? (byte)1 : _pressedBattle(window);

    [UnmanagedCallersOnly]
    private static byte OnPressedWorld(int window) => window is 0 or 1 && Plain[window] ? (byte)1 : _pressedWorld(window);

    [UnmanagedCallersOnly]
    private static void OnLayout(nint box, nint message, nint data, int line)
    {
        _layout(box, message, data, line);
        LaidOut(box, message, line);
    }

    [UnmanagedCallersOnly]
    private static void OnBattleLayout(nint box, nint message, nint data, int line)
    {
        var centred = CentredNext();
        _battleLayout(box, message, data, line);
        LaidOut(box, message, line, centred && RemixedText.OwnEventRunning);
        DropStrayTail(box);
    }

    /// <summary>Whether the box about to be laid out is one the game centres (a MESSAGE's centring byte: the message
    /// window object's +0xAF, for one layout).</summary>
    private static bool CentredNext()
    {
        try
        {
            if (*_ui == 0)
                return false;
            var messages = *(nint*)(*(nint*)(*_ui + 0x10) + 0x68);
            return messages != 0 && *(byte*)(messages + Centring) != 0;
        }
        catch (Exception e)
        {
            Note($"a box's centring failed: {e.Message}");
            return false;
        }
    }

    /// <summary>A speech box of ReMixed's own events (its Treasure Wheel's) whose tail points outside it has no tail: a
    /// box with no unit that the game centred (a MESSAGE's centring byte) points it at the screen's left edge, where
    /// ReMixed's boxes there have none. A menu keeps one whatever its style says; the others' styles drop it.</summary>
    private static void DropStrayTail(nint box)
    {
        try
        {
            var place = (float*)box;
            if (*(int*)(box + RecordWindow * 4) is (0 or 1) && RemixedText.OwnEventRunning
                && (place[RecordTailX] < 0 || place[RecordTailX] > place[RecordWidth]))
                *(int*)(box + RecordTail * 4) = 0;
        }
        catch (Exception e)
        {
            Note($"a box's tail failed: {e.Message}");
        }
    }

    /// <summary>A box just laid out (its record, its message, its line's Id; <paramref name="fitted"/> for one of
    /// ReMixed's own events that the game centred): its window's frames fitted to its lines, a menu with no unit placed
    /// under the top window's box.</summary>
    private static void LaidOut(nint box, nint message, int line, bool fitted = false)
    {
        try
        {
            var window = *(int*)(box + RecordWindow * 4);
            var text = RemixedText.TextOf(line);
            var lines = text == null ? 1 : text.Count(c => c == '\n') + 1;
            if (window is not (0 or 1))
                return;
            var ui = UiWindows();
            if (ui == 0)
                return;
            // (its height the window root's, [3]: three lines, which the frames are drawn for)
            fitted &= text != null && ((float*)box)[3] >= BoxHeight - 1;
            Fit(ui, window, lines, fitted);
            PutAnchorBack();
            var menu = (*(ushort*)(message + Kind) & MenuKind) != 0;
            var place = (float*)box;
            if (fitted)
            {
                FitToText(ui, box, window, line, lines, menu);
                return;
            }
            // ReMixed's boxes of its own lines (its tavern's) take wider text in as they stay open (a number growing a
            // digit as a unit is placed): the game sizes a box once, by the text it opens with, and a wider line
            // breaks, so they open a little wider.
            if (!menu && text != null && text.Contains('\n') && RemixedText.OwnEventRunning)
                place[RecordWidth] += Slack;
            if (menu && window == 1 && place[0] == LeftLimit && place[1] == TopLimit)
            {
                // No portrait, and as wide as its own rows: the remaster measured the widest line of the box's group,
                // which takes in other boxes' lines swapped in after it.
                *(int*)(box + RecordSide * 4) = 0;
                *(int*)(box + RecordFrame * 4) = NormalFrame; // the speech boxes' corners, not a menu's round ends
                *(int*)(box + RecordTail * 4) = 0;
                var messages = *(nint*)(*(nint*)(*_ui + 0x10) + 0x68);
                var shown = RemixedText.TextPointer(line);
                if (shown != 0 && messages != 0)
                {
                    // The width the game measures is at least a speech box's (the manager's +0x6D0), which a menu of
                    // short rows would sit in with half of it empty.
                    var least = *(int*)(ui + LeastWidth);
                    *(int*)(ui + LeastWidth) = 0;
                    try
                    {
                        place[RecordWidth] = _width(messages, shown, window, 0, *(byte*)(messages + 0xB8))
                                             + *(int*)(ui + NoPortraitPadding);
                    }
                    finally
                    {
                        *(int*)(ui + LeastWidth) = least;
                    }
                }
                PlaceUnderTop(ui, place, lines);
            }
        }
        catch (Exception e)
        {
            Note($"laying out a box failed: {e.Message}");
        }
    }

    /// <summary>A box of ReMixed's own events that the game centred (no unit: its Treasure Wheel's) fitted to its text
    /// as ReMixed's boxes are on the PS1: no room for a portrait on its left (the remaster keeps it with no unit), as
    /// wide as its widest line (a speech box's least width lifted while it's measured) and still centred, a menu in the
    /// speech boxes' frame, no tail. Its frames are as high as its lines (<see cref="Fit"/>); one along the screen's
    /// bottom keeps its bottom there, the others their top.</summary>
    private static void FitToText(nint manager, nint box, int window, int line, int lines, bool menu)
    {
        var place = (float*)box;
        *(int*)(box + RecordSide * 4) = 0;
        *(int*)(box + RecordTail * 4) = 0;
        if (menu)
            *(int*)(box + RecordFrame * 4) = NormalFrame;
        var messages = *(nint*)(*(nint*)(*_ui + 0x10) + 0x68);
        var shown = RemixedText.TextPointer(line);
        if (shown != 0 && messages != 0)
        {
            var least = *(int*)(manager + LeastWidth);
            *(int*)(manager + LeastWidth) = 0;
            try
            {
                var width = _width(messages, shown, window, 0, *(byte*)(messages + 0xB8)) + *(int*)(manager + NoPortraitPadding);
                place[0] += (place[RecordWidth] - width) / 2;
                place[RecordWidth] = width;
            }
            finally
            {
                *(int*)(manager + LeastWidth) = least;
            }
        }
        if (lines < Lines && Math.Abs(place[1] + place[3] - BottomLimit) < 1)
            place[1] += (Lines - lines) * RowHeight;
        if (Interlocked.Increment(ref _fitted) <= 8)
            _log.Info($"tall menus: line {line} fitted to its text: {lines} lines, {place[RecordWidth]} wide at "
                      + $"{place[0]}, {place[1]}");
    }

    /// <summary>A button's news (the cursor on it, or picked): the moved second anchor stands for the row it was moved
    /// to, not row 1.</summary>
    [UnmanagedCallersOnly]
    private static void OnButton(nint menu, nint button, int happened, int index)
    {
        if (happened is 0 or 1 && index == 1 && _movedRow >= 1)
            index = _movedRow - *(int*)(menu + FirstRow);
        _button(menu, button, happened, index);
    }

    /// <summary>The line of the box the cursor goes to. The anchors stand at the box's first two lines, the remaster's
    /// own menus having no other lines; its cursor is asked for the menu's row (+0x84) on one path (0x136DB8) and for
    /// one counted from its first choice (+0x90) on another. ReMixed's tavern asks with lines of text above its choices
    /// ("Mission cost: 300 gil. / You have: ... / Squad isn't full. Are you sure? / No / Yes": first choice 3), and the
    /// hand stood at the first line for "No" and the second for "Yes" (seen on the PC, 2026-11-12; on the PS1 it's
    /// beside "No"), so a row that isn't the menu's own counts from its first choice.</summary>
    internal static int CursorLine(int row, int first, int current) => row == current ? row : row + Math.Max(0, first);

    [UnmanagedCallersOnly]
    private static void OnCursor(nint menu, int window, int side, int row)
    {
        try
        {
            var first = *(int*)(menu + FirstRow);
            var line = CursorLine(row, first, *(int*)(menu + Row));
            if ((line >= 2 || first > 0) && Point(menu, window, side, line))
                return;
            PutAnchorBack();
        }
        catch (Exception e)
        {
            Note($"a cursor past the second row failed: {e.Message}");
        }
        _cursor(menu, window, side, row);
    }

    /// <summary>The windows' manager (the message window object's +0x40), or 0.</summary>
    private static nint UiWindows()
    {
        if (*_ui == 0)
            return 0;
        var messages = *(nint*)(*(nint*)(*_ui + 0x10) + 0x68);
        return messages == 0 ? 0 : *(nint*)(messages + Manager);
    }

    /// <summary>A window's frames stretched to a box of <paramref name="lines"/> lines, or as they were for three or
    /// fewer; <paramref name="fitted"/>, shortened to one of fewer (its corners' borders less than half its height).</summary>
    private static void Fit(nint manager, int window, int lines, bool fitted = false)
    {
        var layout = *(nint*)(manager + Layouts + (window == 1 ? BottomWindow : TopWindow));
        if (layout == 0)
            return;
        var extra = (fitted ? Math.Max(1, lines) - Lines : Math.Max(0, lines - Lines)) * RowHeight;
        for (var k = 0; k < Frames.Length; k++)
        {
            var frame = Find(layout + LayoutNodes, Frames[k]);
            if (frame == 0 || *(int*)(frame + Type) != 12 || *(nint*)(frame + Instance) == 0)
                continue;
            var grid = Find(*(nint*)(frame + Instance) + LayoutNodes, Grids[k]);
            if (grid == 0)
                continue;
            if (!Stretched.TryGetValue(grid, out var was))
            {
                if (extra == 0)
                    continue;
                was = (window, *(int*)(grid + Size + 4), *(float*)(grid + RightMargin), *(float*)(grid + BottomMargin),
                       *(int*)(grid + Top), *(int*)(grid + Bottom), *(int*)(grid + Mode));
                Stretched[grid] = was;
            }
            if (extra == 0)
            {
                *(int*)(grid + Mode) = was.Mode;
                *(int*)(grid + Top) = was.Top;
                *(int*)(grid + Bottom) = was.Bottom;
                Resize(grid, was.Height, was.Right);
                *(float*)(grid + BottomMargin) = was.Margin;
                Stretched.Remove(grid);
                continue;
            }
            var border = Math.Min(Border, (was.Height + extra) / 2 - 4);
            *(int*)(grid + Mode) = Stretch;
            *(int*)(grid + Top) = border;
            *(int*)(grid + Bottom) = border;
            Resize(grid, was.Height + extra, was.Right);
        }
        if (extra > 0)
            Note($"a box of {lines} lines in window {window}: its frames {extra} higher");
    }

    /// <summary>A frame's height set (the setter makes both its sizes its own), its right margin put back so that its
    /// width goes on following the box's.</summary>
    private static void Resize(nint grid, int height, float right)
    {
        _setSize(grid, *(int*)(grid + Size), height);
        *(float*)(grid + RightMargin) = right;
    }

    /// <summary>A menu with no unit under the top window's box, its middle where ReMixed's tavern centres its menus on
    /// the PS1 (each at the same x whatever its width, a little left of the screen's middle: work/psx/run/shots
    /// dmf-01800, dmj-00300), kept on the screen.</summary>
    private static void PlaceUnderTop(nint manager, float* place, int lines)
    {
        var top = *(nint*)(manager + Roots + TopWindow);
        if (top == 0)
            return;
        var height = BoxHeight + Math.Max(0, lines - Lines) * RowHeight;
        place[0] = Math.Max(LeftLimit, MenuMiddle - place[RecordWidth] / 2);
        place[1] = Math.Min(*(int*)(top + Position + 4) + *(int*)(top + Size + 4) + UnderTopY, BottomLimit - height);
    }

    /// <summary>The cursor beside a row from the third on: the side's second anchor moved to it and given the cursor,
    /// its collision (the mouse's) off meanwhile. False when the anchors aren't there (the game's own cursor then, which
    /// stays where it was).</summary>
    private static bool Point(nint menu, int window, int side, int row)
    {
        var manager = *(nint*)(menu + Manager);
        if (manager == 0)
            return false;
        var record = _record(manager, window, side);
        if (record == 0)
            return false;
        var japanese = *_language != 0 && *(int*)(*_language + 8) == 0;
        var second = *(nint*)(record + (japanese ? 0x80 : 0x90));
        var layout = *(nint*)(record + (japanese ? JapaneseTextLayout : TextLayout));
        if (second == 0 || layout == 0)
            return false;
        var firstNode = Find(layout + LayoutNodes, Anchors[0]);
        var secondNode = Find(layout + LayoutNodes, Anchors[1]);
        if (firstNode == 0 || secondNode == 0)
            return false;
        if (_moved.Node != secondNode)
        {
            PutAnchorBack();
            var collision = *(nint*)(second + AnchorCollision);
            _moved = (secondNode, *(int*)(secondNode + Position + 4), collision,
                      collision == 0 ? 0 : *(int*)(collision + Size), collision == 0 ? 0 : *(int*)(collision + Size + 4));
            if (collision != 0)
                _setSize(collision, 0, 0);
        }
        var step = _moved.Y - *(int*)(firstNode + Position + 4);
        if (step <= 0)
            step = RowHeight;
        _setPosition(secondNode, *(int*)(secondNode + Position), *(int*)(firstNode + Position + 4) + row * step);
        _movedRow = row;
        var cursor = *(nint*)(*(nint*)(*_ui + 0x10) + 8);
        if (cursor != 0)
            *(byte*)(cursor + 0x84) = 0;
        _focus(second);
        *(byte*)(second + 0x22D) = 0;
        return true;
    }

    private static void PutAnchorBack()
    {
        if (_moved.Node == 0)
            return;
        _setPosition(_moved.Node, *(int*)(_moved.Node + Position), _moved.Y);
        if (_moved.Collision != 0)
            _setSize(_moved.Collision, _moved.Width, _moved.Height);
        _moved = default;
        _movedRow = -1;
    }

    private static nint Find(nint under, string name)
    {
        var key = stackalloc nint[2];
        key[0] = Names[name];
        key[1] = 0;
        return _find(under, (nint)key);
    }

    private static void Note(string text)
    {
        if (Interlocked.Increment(ref _noted) <= 20)
            _log.Info($"tall menus: {text}");
    }
}
