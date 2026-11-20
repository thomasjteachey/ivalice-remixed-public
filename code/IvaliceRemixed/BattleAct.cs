
using System.Runtime.InteropServices;

namespace IvaliceRemixed;

/// <summary>
/// For test runs (<see cref="Harness"/>' <c>act</c>; installed with it): the unit whose turn it is uses an ability, chosen
/// without driving the battle menus. The battle menu's action record is filled as its target selection leaves it, and
/// the menu goes to its confirm state, where the game calls the player's action (0x206CE8) as for a confirmed target:
/// the turn's action marked (0x310548, through Two Spells' hook), the action queued (0x281434), and the menu on to what
/// comes next. Only the menus are skipped, so everything after them is the game's own.
/// </summary>
/// <remarks>
/// The battle menu runs as one loop (0x210AF0, entered once a battle), each state's case ending in the battle's frame
/// (0x1EA110, its one caller); that is hooked, and a queued request is taken there, on the game's thread, before the
/// next frame's dispatch, when no menu window is open: as the command menu is about to open, at a party unit's
/// turn start (state 6, which opens it through 0x20327C or 0x203450) or after an action that left the turn going
/// (state 3, through 0x203620), or at a move selection (0x0D). So a turn's actions are queued before it starts. A
/// window left open would hold the menu at 0x25 for good, which waits for the command menu's result ([0x2FD35A0]) to
/// say it closed (seen in the game, 2026-11-18: an act from the open command menu, state 4). The menu's state is [0xC6B1CC] (the dispatch at 0x211B23); the confirm, 0x1C, calls
/// 0x206CE8 once the screen is idle, and 0x206CE8 sets the next state itself. The battle menu's object comes from
/// 0x2607C0; its +0x148 is the acting unit and +0x178 the 20-byte action record: the unit's index, the skillset, the
/// ability (16 bits), and from +0xA the target: type 5 (a panel), then the panel's x, level and y as 16-bit values at
/// +0xC, +0xE and +0x10, as 0x206F90 writes them from the target cursor. An item (a skillset of type 1, Items) also
/// names the item at +8, which the queue (0x281488) checks and makes the ability from (the item + 0x80). A move
/// selection's range stays drawn until the next one: looks only.
/// </remarks>
internal static unsafe class BattleAct
{
    private const int MoveSelection = 0x0D, CommandMenuOpens = 3, TurnStarts = 6, CommandMenu = 4, Confirm = 0x1C;
    private const int UiUnit = 0x148, UiRecord = 0x178, RecordSize = 20, PanelTarget = 5;
    private const int ItemType = 1, ItemAt = 8, ItemAbilities = 0x80; // an Items skillset's record: the item at +8
    private const int UnitX = 0x4F, UnitY = 0x50, UnitLevelBit = 0x51, UnitIndex = 0x1BC;

    private sealed class Request
    {
        public int Skillset, Ability;
        public string Target = "";
        public bool Moved;
        public int Actor = -1;
        public string Where = "";
    }

    private static Game _game = null!;
    private static int* _state;
    private static delegate* unmanaged<byte*> _battleUi;
    private static delegate* unmanaged<nint, nint> _menuFrame;
    private static readonly System.Collections.Concurrent.ConcurrentQueue<Request> Requests = new();
    private static Request? _current; // the game thread's: set, until the confirm has run
    private static int _seenState = -1;

    public static void Install(Game game)
    {
        _game = game;
        // The menu's dispatch: mov ecx, [state]; cmp ecx, 0x30; jg; inside the menu's loop (0x210AF0).
        var dispatch = game.Image.FindUnique("8B 0D ?? ?? ?? ?? 83 F9 30 0F 8F");
        GameImage.Calls(dispatch - 0x1033, "48 8B C4 48 89 58 08 48 89 70 10 48 89 78 18 55 41 54 41 55 41 56 41 57 48 8D 68 A1",
                        "the battle menu's loop");
        // Where every state's case ends, the battle's frame (0x1EA110, GlobalWork in rcx; its one caller): mov rcx,
        // [GlobalWork]; call; mov rdx, [..]; test rdx, rdx; je; cmp [rdx+0x68], r13b.
        var end = game.Image.FindUnique("48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 15 ?? ?? ?? ?? 48 85 D2 0F 84 ?? ?? ?? ?? 44 38 6A 68");
        if ((ulong)(end - dispatch) > 0x3000)
            throw new InvalidDataException("the battle's frame isn't called from the menu's loop");
        var frame = GameImage.Rip(end + 7, 1, 5);
        // 0x2607C0, as the player's action calls it (0x206D1E): call; or r9d, -1; xor r8d, r8d; xor edx, edx; mov rbx, rax.
        var call = game.Image.FindUnique("E8 ?? ?? ?? ?? 41 83 C9 FF 45 33 C0 33 D2 48 8B D8 E8");
        var getter = GameImage.Rip(call, 1, 5);
        // ... a walk of the UI's objects: mov rcx, [the list]; jmp; movzx eax, byte [rcx+8]; cmp eax, [the battle menu's id].
        GameImage.Calls(getter, "48 8B 0D ?? ?? ?? ?? EB 0F 0F B6 41 08 3B 05", "the battle menu's object");
        _state = (int*)GameImage.Rip(dispatch, 2, 6);
        _battleUi = (delegate* unmanaged<byte*>)getter;
        _menuFrame = (delegate* unmanaged<nint, nint>)game.Hooks.Hook(
            frame, (nint)(delegate* unmanaged<nint, nint>)&MenuFrame, "the battle's frame (the harness's act)");
    }

    /// <summary>
    /// <c>act SKILLSET ABILITY TARGET [TIMES] [moved]</c> (hex skillset and ability): TARGET is <c>foe</c> (the nearest living unit of
    /// another team), <c>self</c> (the acting unit's panel), a battle unit's index (its panel), or <c>X,Y[,LEVEL]</c>; an
    /// item's ability (Items, 0x06: the item + 0x80, a Potion 0x170) names its item too; TIMES queues it that often (a turn's two
    /// actions in one go, as the second's menu comes moments after the first); <c>moved</c> marks the unit as having moved
    /// (+0x1B9 bit 1) just before the confirm, as a move first would, so that the action's mark ends the turn there and then
    /// (a Two Spells second, a rider's mount skill). Queued, and answered at once: the next party
    /// unit's menu between actions takes it (so a turn's actions are queued before it starts), and the log says
    /// "harness: unit N used ..." once its confirm has run. <c>act clear</c> drops what's queued. <c>act menu</c> logs the
    /// menu's state and its action record (unit, skillset, ability, target type, x, level, y), as the menus filled it.
    /// </summary>
    public static string Run(string[] parts)
    {
        if (_state == null)
            return "error: act isn't installed";
        if (parts.Length == 2 && parts[1].Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            Requests.Clear();
            return "act: nothing queued";
        }
        if (parts.Length == 2 && parts[1].Equals("menu", StringComparison.OrdinalIgnoreCase))
        {
            var menu = _battleUi();
            var line = menu == null
                ? $"harness: the menu's state 0x{*_state:X}, no battle menu"
                : $"harness: the menu's state 0x{*_state:X}, its record unit {menu[UiRecord]}, skillset 0x{menu[UiRecord + 1]:X2}, "
                  + $"ability 0x{*(ushort*)(menu + UiRecord + 2):X3}, target type {menu[UiRecord + 0xA]} at "
                  + $"({*(ushort*)(menu + UiRecord + 0xC)}, {*(ushort*)(menu + UiRecord + 0x10)}, level {*(ushort*)(menu + UiRecord + 0xE)})";
            _game.Log.Info(line);
            return line;
        }
        if (parts.Length < 4)
            return "error: act SKILLSET ABILITY foe|self|INDEX|X,Y[,LEVEL] [TIMES] [moved], or act clear";
        var extra = parts.Skip(4).ToList();
        var moved = extra.RemoveAll(p => p.Equals("moved", StringComparison.OrdinalIgnoreCase)) > 0;
        var times = extra.Count > 0 ? int.Parse(extra[0]) : 1;
        for (var k = 0; k < times; k++)
            Requests.Enqueue(new Request
            {
                Skillset = Convert.ToInt32(parts[1].Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16),
                Ability = Convert.ToInt32(parts[2].Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16),
                Target = parts[3],
                Moved = moved,
            });
        // A party unit's turn already at its first command menu (state 4) has no window-less moment left: there the
        // menu's cursor is on Move, and F opens the move selection, which takes the request.
        var pressed = "";
        var ui = *_state == CommandMenu ? _battleUi() : null;
        var actor = ui == null ? null : *(byte**)(ui + UiUnit);
        if (actor != null && actor[Battle.UnitTurn] != 0 && (actor[Battle.UnitCurrentTeam] & 0x08) != 0
            && actor[Battle.UnitMoved] == 0 && actor[Battle.UnitActed] == 0)
        {
            Autopilot.Step("key F");
            pressed = "; F pressed on Move for the move selection";
        }
        return $"act: {Requests.Count} queued for the party's next turn menus (the menu's state 0x{*_state:X}){pressed}";
    }

    [UnmanagedCallersOnly]
    private static nint MenuFrame(nint globalWork)
    {
        try
        {
            Step();
        }
        catch (Exception e)
        {
            _current = null;
            _game.Log.Error($"harness: act failed: {e.Message}");
        }
        return _menuFrame(globalWork);
    }

    private static void Step()
    {
        var state = *_state;
        var units = _game.TurnCode.Units;
        if (_current is { } done)
        {
            if (state == Confirm)
                return;
            var unit = units + done.Actor * Battle.UnitSize;
            _current = null;
            _game.Log.Info($"harness: unit {done.Actor} used 0x{done.Ability:X3} (skillset 0x{done.Skillset:X2}) at {done.Where}; "
                           + $"acted {unit[Battle.UnitActed]}, its action 0x{*(ushort*)(unit + 0x1A2):X3} charging "
                           + $"{unit[0x18D]:X2}, the menu's state now 0x{state:X}");
            return;
        }
        if (Requests.IsEmpty)
            return;
        var ui = _battleUi();
        var actor = ui == null ? null : *(byte**)(ui + UiUnit);
        if (state != _seenState)
        {
            // While acts wait: each state the menu passes through, and its unit, to see why none is taken.
            _seenState = state;
            _game.Log.Info($"harness: act waits ({Requests.Count} queued): the menu's state 0x{state:X}, "
                           + (actor == null ? "no unit" : $"unit {actor[UnitIndex]} (turn {actor[Battle.UnitTurn]}, +5 0x{actor[Battle.UnitCurrentTeam]:X2})"));
        }
        if (state is not (TurnStarts or CommandMenuOpens or MoveSelection))
            return;
        // A party unit (+5 & 8, the player's to command) whose turn it is.
        if (actor == null || actor[Battle.UnitTurn] == 0 || (actor[Battle.UnitCurrentTeam] & 0x08) == 0
            || !Requests.TryDequeue(out var request))
            return;

        int x, y, level;
        if (request.Target.Contains(','))
        {
            var xyz = request.Target.Split(',').Select(int.Parse).ToArray();
            (x, y, level) = (xyz[0], xyz[1], xyz.Length > 2 ? xyz[2] : 0);
            request.Where = $"panel ({x}, {y}, level {level})";
        }
        else
        {
            var index = request.Target.Equals("foe", StringComparison.OrdinalIgnoreCase) ? Nearest(units, actor)
                : request.Target.Equals("self", StringComparison.OrdinalIgnoreCase) ? actor[UnitIndex]
                : int.Parse(request.Target);
            if ((uint)index >= Battle.UnitCount)
            {
                _game.Log.Error("harness: act: no foe on the field");
                return;
            }
            var t = units + index * Battle.UnitSize;
            (x, y, level) = (t[UnitX], t[UnitY], t[UnitLevelBit] >> 7);
            request.Where = $"unit {index}'s panel ({x}, {y}, level {level})";
        }

        var record = ui + UiRecord;
        new Span<byte>(record, RecordSize).Clear();
        record[0] = actor[UnitIndex];
        record[1] = (byte)request.Skillset;
        *(ushort*)(record + 2) = (ushort)request.Ability;
        if (Patches.TwoSpells.TypeTable != null && (Patches.TwoSpells.TypeTable[(byte)request.Skillset] & 0x0F) == ItemType)
            *(ushort*)(record + ItemAt) = (ushort)(request.Ability - ItemAbilities);
        record[0xA] = PanelTarget;
        *(ushort*)(record + 0xC) = (ushort)x;
        *(ushort*)(record + 0xE) = (ushort)level;
        *(ushort*)(record + 0x10) = (ushort)y;
        request.Actor = actor[UnitIndex];
        if (request.Moved)
            actor[Battle.UnitMoved] |= 1;
        _current = request;
        *_state = Confirm;
        _game.Log.Info($"harness: unit {request.Actor} uses 0x{request.Ability:X3} (skillset 0x{request.Skillset:X2}) at "
                       + $"{request.Where}{(request.Moved ? ", as having moved" : "")}, from the menu's state 0x{state:X}");
    }

    /// <summary>The nearest living unit on the field of another team than <paramref name="actor"/>'s (+0x1EE &amp; 0x30).</summary>
    private static int Nearest(byte* units, byte* actor)
    {
        var best = -1;
        var distance = int.MaxValue;
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var u = units + k * Battle.UnitSize;
            if (u[1] == 0xFF || u == actor || *(ushort*)(u + Battle.UnitHp) == 0
                || (u[Battle.UnitTeam] & 0x30) == (actor[Battle.UnitTeam] & 0x30))
                continue;
            var d = Math.Abs(u[UnitX] - actor[UnitX]) + Math.Abs(u[UnitY] - actor[UnitY]);
            if (d < distance)
                (best, distance) = (k, d);
        }
        return best;
    }
}
