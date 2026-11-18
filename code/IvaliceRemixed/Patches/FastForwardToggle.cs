using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// Fast-forward as a toggle (asked 2026-11-18; the mod's, not ReMixed's): with the Fast-forward setting at Toggle, the
/// game's fast-forward button turns fast-forward on, and it stays on after the button is let go until the button turns
/// it off again. The faster speed's button switches to that speed, and turns it off the same way. A press counts once
/// the game takes it as one: the pad's fast-forward buttons only after being held about a second, as the game has them.
/// Installed at startup when the setting is Toggle.
/// </summary>
/// <remarks>
/// The remaster asks each frame whether its two fast-forward actions are held: 0x59 (fast-forward; the pad's button 35,
/// Ctrl or Left Alt on the keyboard, in UIKeyDefine and UIKeyAssign) and 0x5A (the faster speed; the pad's 36), through
/// the held check 0x0F0D4C, which reads the held byte of each of the action's three bindings (the input state's +0x554,
/// +0x564 and +0x574, from the action × 0x30; the pressed bytes, 0x0F0D00's, are 2 before each). The battle's frame
/// (0x1EA110) sets the speed level (GlobalWork [0x3CD9DD0] +0x58) to 2 while 0x5A is held, 1 while 0x59 is, else 0; an
/// event's (0x1DB6C4) to the same once one of them has been pressed and held a moment; 0x15C0DC, in mode 3, to 1 while
/// 0x59 is. 0x1E8A9C gives the mode (GlobalWork +0x60: 1 a battle, 2 an event, 3 set by 0x33EC0C), or 0 where the game's
/// own Fast-forward option turns it off (the config's +0x37: 1 off, 2 off in events); 0x1E8AC4 turns the level into the
/// game's speed (the table at 0x672018: 1, 3, 5, so 0x59 is ×3 and 0x5A ×5). The pad's bindings carry flag 0x10000,
/// which makes the action pressed and held only once its button has been held for a second (0x0EFF18), so a short press
/// stays the button's other uses'.
/// <para>The input's frame (0x0F17D4, rcx the input manager [0x3CD9EE8], whose +8 is the input state) fills those bytes
/// from the pad and keyboard (0x0EFB50, while its +0x359F is set). The hook runs it, then, where fast-forward can be on
/// (the mode isn't 0): a press of either action turns its speed on, or off when it's the one on; the held bytes say the
/// speed on is held and the other isn't; and in an event, which starts fast-forward only after a press (the event's code
/// is the only reader of these two actions' presses), the speed on is pressed too. Elsewhere the bytes are left as the
/// game has them. The game works out each frame's bytes from the devices and its own +0x550, +0x551, +0x555 and +0x560,
/// never from the pressed or held bytes, so what's written here doesn't carry into the next frame.</para>
/// </remarks>
internal static unsafe class FastForwardToggle
{
    // 0x0F17D4: the input's frame (rcx the input manager, rdx a record it fills): mov [rsp+8], rbx; mov [rsp+0x10], rbp;
    // mov [rsp+0x18], rsi; push rdi; sub rsp, 0x40; xor ebp, ebp; mov rdi, rdx; mov rbx, rcx; cmp [rcx+0x359F], bpl; je.
    internal const string InputFrame =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 40 33 ED 48 8B FA 48 8B D9 40 38 A9 9F 35 00 00 0F 84";
    // 0x1E8A9C: fast-forward's mode (rcx GlobalWork): its +0x60, or 0 where the config's +0x37 turns fast-forward off.
    internal const string Mode =
        "48 8B 05 ?? ?? ?? ?? 48 85 C0 74 17 0F B6 50 37 83 EA 01 74 0B 83 FA 01 75 09 83 79 60 02 75 03 33 C0 C3 8B 41 60 C3";
    // 0x1EA154, in the battle's frame: mov r8, [GlobalWork]; mov rcx, r8; call the mode; cmp eax, r13d (1); jne; cmp
    // [..], 0; jne; mov r9, [the input manager]; lea edx, [r13+0x59] (0x5A); lea rcx, [r9+8]; call the held check.
    internal const string BattleSpeed =
        "4C 8B 05 ?? ?? ?? ?? 49 8B C8 E8 ?? ?? ?? ?? 41 3B C5 75 ?? 83 3D ?? ?? ?? ?? 00 75 ?? 4C 8B 0D ?? ?? ?? ?? 41 8D 55 59 49 8D 49 08 E8";

    internal const int Slow = 0x59, Fast = 0x5A; // fast-forward's two actions: its speed and the faster one
    internal const int Event = 2;                // the mode of an event, which starts fast-forward on a press
    internal const int State = 8, Filled = 0x359F, Actions = 0x548, ActionBytes = 0x30, Bindings = 3, BindingBytes = 0x10,
        Pressed = 0xA, Held = 0xC;

    private static Log _log = null!;
    private static delegate* unmanaged<nint, nint, void> _frame;
    private static delegate* unmanaged<nint, int> _mode;
    private static nint* _globalWork;
    private static int _failed;

    /// <summary>The action whose speed is on (<see cref="Slow"/> or <see cref="Fast"/>), or 0.</summary>
    internal static int On { get; private set; }

    /// <summary>Where the input's frame, the mode and the GlobalWork pointer are, for the offline check.</summary>
    internal static nint FrameAt, ModeAt, GlobalWorkAt, InputManagerAt;

    public static void Install(Game game)
    {
        _log = game.Log;
        var image = game.Image;
        var speed = image.FindUnique(BattleSpeed);
        ModeAt = image.FindUnique(Mode);
        if (GameImage.Rip(speed + 10, 1, 5) != ModeAt)
            throw new InvalidDataException("the battle's speed doesn't ask fast-forward's mode where this DLL expects");
        GlobalWorkAt = GameImage.Rip(speed, 3, 7);
        InputManagerAt = GameImage.Rip(speed + 29, 3, 7);
        _globalWork = (nint*)GlobalWorkAt;
        _mode = (delegate* unmanaged<nint, int>)ModeAt;
        FrameAt = image.FindUnique(InputFrame);
        _frame = (delegate* unmanaged<nint, nint, void>)game.Hooks.Hook(
            FrameAt, (nint)(delegate* unmanaged<nint, nint, void>)&OnFrame, "the input's frame");
        _log.Info("fast-forward: a toggle (its button turns it on, and again off)");
    }

    [UnmanagedCallersOnly]
    private static void OnFrame(nint input, nint record)
    {
        var filled = *(byte*)(input + Filled) != 0;
        _frame(input, record);
        try
        {
            var work = *_globalWork;
            var mode = work == 0 ? 0 : _mode(work);
            var next = Step(input + State, filled, mode, On);
            if (next != On)
                _log.Info(next == 0 ? "fast-forward: off" : $"fast-forward: on (×{(next == Fast ? 5 : 3)})");
            On = next;
        }
        catch (Exception e)
        {
            if (Interlocked.Increment(ref _failed) <= 3)
                _log.Error($"fast-forward: the toggle failed: {e.Message}");
        }
    }

    /// <summary>One frame of the toggle on the input <paramref name="state"/> the game has just filled (or left, when
    /// not <paramref name="filled"/>), in fast-forward's <paramref name="mode"/>, with <paramref name="on"/>'s speed on:
    /// the speed on after it, its bytes written into the state.</summary>
    internal static int Step(nint state, bool filled, int mode, int on)
    {
        if (mode == 0)
            return on; // no fast-forward here: the game's bytes as they are
        if (filled)
        {
            var pressed = IsPressed(state, Fast) ? Fast : IsPressed(state, Slow) ? Slow : 0;
            if (pressed != 0)
                on = on == pressed ? 0 : pressed;
        }
        foreach (var action in (ReadOnlySpan<int>)[Slow, Fast])
        {
            var record = state + Actions + action * ActionBytes;
            for (var k = 0; k < Bindings; k++)
                *(byte*)(record + k * BindingBytes + Held) = (byte)(k == 0 && action == on ? 1 : 0);
        }
        if (on != 0 && mode == Event)
            *(byte*)(state + Actions + on * ActionBytes + Pressed) = 1;
        return on;
    }

    /// <summary>Whether the game took <paramref name="action"/> as pressed this frame, by any of its bindings.</summary>
    internal static bool IsPressed(nint state, int action)
    {
        var record = state + Actions + action * ActionBytes;
        for (var k = 0; k < Bindings; k++)
            if (*(byte*)(record + k * BindingBytes + Pressed) != 0)
                return true;
        return false;
    }
}
