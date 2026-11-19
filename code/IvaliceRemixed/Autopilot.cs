using System.Runtime.InteropServices;

namespace IvaliceRemixed;

/// <summary>
/// For headless test runs only (tools/deck/headless.sh): with <see cref="FileName"/> beside the DLL, the game's controller
/// polls (XInputGetState) answer from that script instead of a pad, so a run can get from the title to where a test
/// needs it with no one at the Deck. Without the file nothing is hooked.
/// </summary>
/// <remarks>
/// The script has a command a line: <c>wait 5</c> (seconds), <c>press A</c> (a button held for a few polls, then let
/// go: A B X Y START BACK UP DOWN LEFT RIGHT LB RB), <c>hold START 2</c> (seconds), <c>key RETURN</c> (a key pressed:
/// sent to the game's windows as key messages and seen by GetAsyncKeyState and GetKeyState; RETURN ESCAPE SPACE
/// BACK TAB UP DOWN LEFT RIGHT, a letter or digit, F1-F12, or a number), <c>scenario 1EC</c> (started from the world
/// map's next frame, as Start does it), <c>action 1A</c> (the game's action test, 0x0FC598, says yes to that action
/// for a moment: its menus and the PS1 code check buttons through it), <c>pad 0x40</c> (PS1 button bits ORed into the
/// pad word the test is given), <c>ui1 1A</c> (one of the UI's three action checks, 0x0F0D00, 0x0F0D4C and 0x0F0D98 for
/// ui0, ui1 and ui2, says yes to that action for about a frame: the remaster's own screens read their keys there),
/// <c>actions on</c>/<c>actions off</c> (log each new (button, action) pair the game asks
/// about, to learn a screen's), <c>log text</c>, <c>quit</c> (ends the game).
/// Lines starting with # are left out. The game takes its pad through Steam Input when Steam runs, so keys are what
/// reach it. The game drops its input while its window isn't the active one (0x1F9D8 asks GetActiveWindow), which on a
/// hidden desktop it never is, so GetActiveWindow answers with the game's window; SetCursorPos and ClipCursor, which
/// the game calls for its active window, do nothing (they'd move or hold the real mouse). <see cref="Harness"/> takes
/// the same steps through a pipe while the game runs.
/// </remarks>
internal static unsafe class Autopilot
{
    public const string FileName = "autopilot.txt";
    // XInput's button bits, and the triggers (LT, RT) as two more: held, the state's trigger byte is full.
    private static readonly Dictionary<string, uint> Buttons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UP"] = 0x1, ["DOWN"] = 0x2, ["LEFT"] = 0x4, ["RIGHT"] = 0x8, ["START"] = 0x10, ["BACK"] = 0x20,
        ["LB"] = 0x100, ["RB"] = 0x200, ["A"] = 0x1000, ["B"] = 0x2000, ["X"] = 0x4000, ["Y"] = 0x8000,
        ["LT"] = 0x10000, ["RT"] = 0x20000,
    };
    private static volatile uint _held;
    private static uint _packet;
    private static int _polls;
    internal static int Polls => _polls;
    private static volatile bool _done;
    internal static bool Done => _done;
    private static delegate* unmanaged<nint> _gameActiveWindow;
    private static nint _mainWindow;
    private static Log _log = null!;
    private static Game _game = null!;
    private static delegate* unmanaged<uint, byte*, uint> _gameGetState;
    private static delegate* unmanaged<int, short> _gameAsyncKey, _gameKey;
    private static volatile int _key; // the virtual key held, or 0
    private static volatile int _action = -1; // the action the test says yes to, or -1
    private static volatile uint _pad; // PS1 button bits ORed into the test's pad word
    // The battle loop's frame (0x37FB7C) builds the PS1 button words from the first pad's struct ([0x2E942E8]: +2 the
    // high byte, +3 the low, a bit clear when held). A native wrapper ANDs _padMasks (high, low) into them for that call
    // and puts them back after, so a "pad" step reaches the PS1 code (the formation screen among it).
    private static byte* _padMasks;
    // sub rsp, 0x28; mov rax, [second pad]; mov rcx, [first pad]; mov edx, [held]; mov [held before], edx (0x37FB7C).
    internal const string BattleFramePattern =
        "48 83 EC 28 48 8B 05 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 8B 15 ?? ?? ?? ?? 89 15 ?? ?? ?? ?? 44 0F B6 40 02";
    private static volatile bool _listen;
    private static readonly HashSet<(int, long)> AskedActions = [];
    private static delegate* unmanaged<uint, int, ulong, byte, ulong> _gameActionTest;
    /// <summary>Where the action test and the first pad's record pointer are, found before this hooks them (their
    /// patterns start where the hooks go), for <see cref="Patches.Ps1Pad"/>, which installs after; 0 when not here.</summary>
    internal static nint ActionTestAt, FirstPadAt;
    // The UI's action check is a leaf the whole-program-optimized callers keep registers across (a managed hook there
    // crashed a twin's caller), so its hook is native: yes when the action is *_ui; each action asked about is set in
    // the bitmap _asked (0x400 bits); else on to the original.
    // Three twins read the action's state at +0x552, +0x554 and +0x556 (likely held, pressed and released); each gets
    // its own stub, scripted action (ui0, ui1, ui2) and bitmap.
    private static readonly int*[] _ui = new int*[3];
    private static readonly uint*[] _asked = new uint*[3];
    private static readonly HashSet<int> AskedUi = [];
    // The UI's action checks (0x0F0D00, 0x0F0D4C, 0x0F0D98): mov eax, edx; lea rdx, [rax+rax*2]; shl rdx, 4; add rdx,
    // rcx; xor ecx, ecx; lea rax, [rdx+0x552 (0x554, 0x556)]; cmp byte [rax], 0.
    internal const string UiActionPattern = "8B C2 48 8D 14 40 48 C1 E2 04 48 03 D1 33 C9 48 8D 82 {0:X2} 05 00 00 80 38 00";
    // The action test (0x0FC598): push rbx; sub rsp, 0x20; mov r10, [the input state]; mov r11d, ecx; mov ebx, r8d;
    // movzx eax, byte [r10+0x17]; cmp byte [rax+r10+0x14], 2.
    internal const string ActionTestPattern =
        "40 53 48 83 EC 20 4C 8B 15 ?? ?? ?? ?? 44 8B D9 41 8B D8 41 0F B6 42 17 42 80 7C 10 14 02";

    private static readonly Dictionary<string, int> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RETURN"] = 0x0D, ["ENTER"] = 0x0D, ["ESCAPE"] = 0x1B, ["SPACE"] = 0x20, ["BACK"] = 0x08, ["TAB"] = 0x09,
        ["LEFT"] = 0x25, ["UP"] = 0x26, ["RIGHT"] = 0x27, ["DOWN"] = 0x28, ["SHIFT"] = 0x10, ["CONTROL"] = 0x11,
    };

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(delegate* unmanaged<nint, nint, int> callback, nint parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")]
    private static extern bool PostMessageW(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyW(uint code, uint mapType);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out Rect rect);
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadLibraryW(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern nint GetProcAddress(nint module, string name);

    public static void Install(Game game)
    {
        var file = Path.Combine(Path.GetDirectoryName(typeof(Autopilot).Assembly.Location) ?? "", FileName);
        if (!File.Exists(file))
            return;
        _log = game.Log;
        _game = game;
        _log.Info($"autopilot: the game's sound muted on {Mute()} output device(s)");
        var hooked = new List<string>();
        foreach (var dll in (string[])["xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll"])
        {
            var module = GetModuleHandleW(dll);
            if (module == 0 && dll == "xinput1_4.dll")
                module = LoadLibraryW(dll);
            var get = module == 0 ? 0 : GetProcAddress(module, "XInputGetState");
            if (get == 0)
                continue;
            var original = game.Hooks.Hook(get, (nint)(delegate* unmanaged<uint, byte*, uint>)&GetState, $"{dll}'s XInputGetState");
            if (_gameGetState == null)
                _gameGetState = (delegate* unmanaged<uint, byte*, uint>)original;
            hooked.Add(dll);
        }
        var user32 = GetModuleHandleW("user32.dll");
        _gameAsyncKey = (delegate* unmanaged<int, short>)game.Hooks.Hook(GetProcAddress(user32, "GetAsyncKeyState"),
            (nint)(delegate* unmanaged<int, short>)&AsyncKey, "GetAsyncKeyState");
        _gameKey = (delegate* unmanaged<int, short>)game.Hooks.Hook(GetProcAddress(user32, "GetKeyState"),
            (nint)(delegate* unmanaged<int, short>)&Key, "GetKeyState");
        _gameActiveWindow = (delegate* unmanaged<nint>)game.Hooks.Hook(GetProcAddress(user32, "GetActiveWindow"),
            (nint)(delegate* unmanaged<nint>)&ActiveWindow, "GetActiveWindow");
        game.Hooks.Hook(GetProcAddress(user32, "SetCursorPos"), (nint)(delegate* unmanaged<int, int, int>)&NoCursorPos, "SetCursorPos");
        game.Hooks.Hook(GetProcAddress(user32, "ClipCursor"), (nint)(delegate* unmanaged<nint, int>)&NoClipCursor, "ClipCursor");
        try
        {
            ActionTestAt = game.Image.FindUnique(ActionTestPattern);
            _gameActionTest = (delegate* unmanaged<uint, int, ulong, byte, ulong>)game.Hooks.Hook(
                ActionTestAt, (nint)(delegate* unmanaged<uint, int, ulong, byte, ulong>)&ActionTest, "the action test");
            _padMasks = (byte*)NativeMemory.AllocZeroed(8);
            _padMasks[0] = _padMasks[1] = 0xFF;
            var frame = game.Image.FindUnique(BattleFramePattern);
            var firstPad = GameImage.Rip(frame + 11, 3, 7);
            FirstPadAt = firstPad;
            var wrapper = GameImage.AllocateCode(128);
            var wrapperCode = PadWrapper((nint*)firstPad, _padMasks, (nint)(wrapper + 120));
            wrapperCode.CopyTo(new Span<byte>(wrapper, wrapperCode.Length));
            *(nint*)(wrapper + 120) = game.Hooks.Hook(frame, (nint)wrapper, "the battle loop's frame");
            for (var v = 0; v < 3; v++)
            {
                _ui[v] = (int*)NativeMemory.AllocZeroed(8);
                *_ui[v] = -1;
                _asked[v] = (uint*)NativeMemory.AllocZeroed(0x400 / 8);
                var stub = GameImage.AllocateCode(96);
                var code = UiStub(_ui[v], _asked[v], (nint)(stub + 88));
                code.CopyTo(new Span<byte>(stub, code.Length));
                var at = game.Image.FindUnique(string.Format(UiActionPattern, 0x52 + 2 * v));
                var original = game.Hooks.Hook(at, (nint)stub, $"the UI's action check {v}");
                *(nint*)(stub + 88) = original;
            }
        }
        catch (Exception e)
        {
            _log.Error($"autopilot: the action test not hooked: {e.Message}");
        }
        var lines = File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToArray();
        _log.Info($"autopilot: {lines.Length} step(s) from {FileName}; XInputGetState hooked in {string.Join(", ", hooked)}");
        _done = false;
        new Thread(() => Run(lines)) { IsBackground = true, Name = "autopilot" }.Start();
        Harness.Install(game);
    }

    private static void Run(string[] lines)
    {
        try
        {
            foreach (var line in lines)
            {
                var result = Step(line);
                if (result.StartsWith("error", StringComparison.Ordinal))
                    _log.Error($"autopilot: '{line}': {result}");
            }
        }
        finally
        {
            _done = true;
            _log.Info("autopilot: the script is done");
        }
    }

    /// <summary>One step of a script (or from the harness); what it did, or "error: ...".</summary>
    internal static string Step(string line)
    {
        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var arg = parts.Length > 1 ? parts[1] : "";
        try
        {
            switch (parts[0].ToLowerInvariant())
            {
                case "wait":
                    Thread.Sleep(TimeSpan.FromSeconds(double.Parse(arg)));
                    break;
                case "press":
                    Hold(arg, 0.15);
                    Thread.Sleep(250);
                    break;
                case "hold":
                {
                    var hold = arg.Split(' ');
                    Hold(hold[0], double.Parse(hold[1]));
                    break;
                }
                case "key":
                    PressKey(arg);
                    Thread.Sleep(250);
                    break;
                case "scenario":
                {
                    // "scenario 79 noted": its scene counted as seen, so a scene-first battle starts at the battle.
                    // "scenario 95 event 95": the scenario's map and units with that event (hex), for the remaster's
                    // own scenes, whose events a requested start doesn't load.
                    var words = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var scenario = Convert.ToInt32(words[0], 16);
                    for (var k = 1; k < words.Length; k++)
                        if (words[k].Equals("noted", StringComparison.OrdinalIgnoreCase))
                            Patches.RemixedScenarios.RequestedNote = scenario;
                        else if (words[k].Equals("event", StringComparison.OrdinalIgnoreCase) && k + 1 < words.Length)
                            Patches.RemixedScenarios.RequestedEvent = Convert.ToInt32(words[++k], 16);
                    Patches.RemixedScenarios.Requested = scenario;
                    _log.Info($"autopilot: asks for scenario 0x{scenario:X}"
                              + $"{(words.Length > 1 ? " " + string.Join(' ', words.Skip(1)) : "")} "
                              + "on the world map's next frame");
                    break;
                }
                case "action":
                    _log.Info($"autopilot: action 0x{arg} (polls so far {_polls})");
                    _action = Convert.ToInt32(arg, 16);
                    Thread.Sleep(120);
                    _action = -1;
                    Thread.Sleep(250);
                    break;
                case "ui0" or "ui1" or "ui2":
                {
                    var v = parts[0][2] - '0';
                    _log.Info($"autopilot: UI check {v}, action 0x{arg} (polls so far {_polls})");
                    Volatile.Write(ref *_ui[v], Convert.ToInt32(arg, 16));
                    Thread.Sleep(40);
                    Volatile.Write(ref *_ui[v], -1);
                    Thread.Sleep(300);
                    break;
                }
                case "pad":
                {
                    var bits = Convert.ToUInt32(arg, 16);
                    _log.Info($"autopilot: pad 0x{bits:X} (polls so far {_polls})");
                    _pad = bits;
                    Volatile.Write(ref _padMasks[0], (byte)~(bits >> 8));
                    Volatile.Write(ref _padMasks[1], (byte)~bits);
                    Thread.Sleep(120);
                    Volatile.Write(ref _padMasks[0], 0xFF);
                    Volatile.Write(ref _padMasks[1], 0xFF);
                    _pad = 0;
                    Thread.Sleep(250);
                    break;
                }
                case "actions":
                    if (!arg.Equals("on", StringComparison.OrdinalIgnoreCase))
                        LogAskedUi();
                    for (var v = 0; v < 3; v++)
                        new Span<byte>(_asked[v], 0x400 / 8).Clear();
                    _listen = arg.Equals("on", StringComparison.OrdinalIgnoreCase);
                    lock (AskedActions)
                    {
                        AskedActions.Clear();
                        AskedUi.Clear();
                    }
                    break;
                case "until-world":
                {
                    // Enter every second until the world map's frames run, for up to the given seconds. Title and
                    // Continue transitions ignore input while busy, so this removes most cold-boot idle time.
                    var deadline = DateTime.UtcNow.AddSeconds(double.Parse(arg));
                    var before = Patches.RemixedScenarios.WorldFrames;
                    while (DateTime.UtcNow < deadline)
                    {
                        Thread.Sleep(1000);
                        var now = Patches.RemixedScenarios.WorldFrames;
                        if (now != before)
                            break;
                        before = now;
                        PressKey("RETURN");
                    }
                    Thread.Sleep(3000);
                    _log.Info($"autopilot: on the world map ({Patches.RemixedScenarios.WorldFrames} frames seen)");
                    break;
                }
                case "units":
                    return LogUnits();
                case "log":
                    _log.Info($"autopilot: {arg} (the game has polled the pad {_polls} times)");
                    break;
                case "quit":
                    _log.Info("autopilot: quitting");
                    Environment.Exit(0);
                    break;
                default:
                    return $"error: unknown step '{line}'";
            }
            return "ok";
        }
        catch (Exception e)
        {
            return $"error: {e.Message}";
        }
    }

    private static readonly List<nint> Windows = [];

    [UnmanagedCallersOnly]
    private static int CollectWindow(nint window, nint parameter)
    {
        GetWindowThreadProcessId(window, out var process);
        if (process == (uint)Environment.ProcessId)
            Windows.Add(window);
        return 1;
    }

    private static void PressKey(string name)
    {
        var key = Keys.TryGetValue(name, out var known) ? known
            : name.Length == 1 && char.IsLetterOrDigit(name[0]) ? char.ToUpperInvariant(name[0])
            : name.StartsWith('F') && int.TryParse(name[1..], out var f) ? 0x6F + f
            : Convert.ToInt32(name, 0);
        nint[] windows;
        lock (Windows)
        {
            Windows.Clear();
            EnumWindows(&CollectWindow, 0);
            windows = [.. Windows];
        }
        var scan = (nint)MapVirtualKeyW((uint)key, 0) << 16;
        _log.Info($"autopilot: key {name} (0x{key:X2}) to {windows.Length} window(s) (polls so far {_polls})");
        _key = key;
        foreach (var window in windows)
            PostMessageW(window, 0x100, key, 1 | scan);                 // WM_KEYDOWN
        Thread.Sleep(150);
        _key = 0;
        foreach (var window in windows)
            PostMessageW(window, 0x101, key, 1 | scan | unchecked((nint)0xC0000000)); // WM_KEYUP
    }

    /// <summary>The game's action test (pad word, the action's default button, the action, a mode): yes for the
    /// script's action, and with the script's pad bits in the word.</summary>
    [UnmanagedCallersOnly]
    private static ulong ActionTest(uint pad, int button, ulong action, byte mode)
    {
        if (_listen)
        {
            lock (AskedActions)
            {
                if (AskedActions.Count < 64 && AskedActions.Add((button, (long)(action & 0xFFFFFFFF))))
                    _log.Info($"autopilot: the game asks for button 0x{button:X} action 0x{action & 0xFFFFFFFF:X} (mode {mode})");
            }
        }
        if (_action >= 0 && (long)(action & 0xFFFFFFFF) == _action)
            return 1;
        return _gameActionTest(pad | _pad, button, action, mode);
    }

    /// <summary>The battle loop's frame, wrapped: the first pad's struct (*<paramref name="firstPad"/>) +2/+3 ANDed with
    /// <paramref name="masks"/>, the original called (its address in the slot <paramref name="original"/>), the two bytes
    /// put back. The frame takes no arguments and clobbers rax, rcx, rdx and r8 itself; so does this.</summary>
    internal static byte[] PadWrapper(nint* firstPad, byte* masks, nint original)
    {
        var c = new List<byte>();
        c.AddRange([0x48, 0x83, 0xEC, 0x38]);                                       // sub rsp, 0x38
        c.AddRange([0x48, 0xB8]); c.AddRange(BitConverter.GetBytes((long)firstPad)); // mov rax, &first pad
        c.AddRange([0x48, 0x8B, 0x00]);                                             // mov rax, [rax]
        c.AddRange([0x48, 0x89, 0x44, 0x24, 0x28]);                                 // mov [rsp+0x28], rax
        c.AddRange([0x0F, 0xB7, 0x48, 0x02]);                                       // movzx ecx, word [rax+2]
        c.AddRange([0x66, 0x89, 0x4C, 0x24, 0x20]);                                 // mov [rsp+0x20], cx
        c.AddRange([0x48, 0xBA]); c.AddRange(BitConverter.GetBytes((long)masks));    // mov rdx, masks
        c.AddRange([0x44, 0x8A, 0x02]);                                             // mov r8b, [rdx]
        c.AddRange([0x44, 0x20, 0x40, 0x02]);                                       // and [rax+2], r8b
        c.AddRange([0x44, 0x8A, 0x42, 0x01]);                                       // mov r8b, [rdx+1]
        c.AddRange([0x44, 0x20, 0x40, 0x03]);                                       // and [rax+3], r8b
        c.AddRange([0x48, 0xB8]); c.AddRange(BitConverter.GetBytes((long)original)); // mov rax, &slot
        c.AddRange([0xFF, 0x10]);                                                   // call [rax]
        c.AddRange([0x48, 0x8B, 0x4C, 0x24, 0x28]);                                 // mov rcx, [rsp+0x28]
        c.AddRange([0x0F, 0xB7, 0x54, 0x24, 0x20]);                                 // movzx edx, word [rsp+0x20]
        c.AddRange([0x66, 0x89, 0x51, 0x02]);                                       // mov [rcx+2], dx
        c.AddRange([0x48, 0x83, 0xC4, 0x38]);                                       // add rsp, 0x38
        c.AddRange([0xC3]);                                                         // ret
        if (c.Count > 120)
            throw new InvalidOperationException("the pad wrapper is too big");
        return [.. c];
    }

    /// <summary>Each battle unit in use: its sprite set (+0), battle data id (+1), job (+3), team (+5), primary and
    /// secondary skillsets (+0x12, +0x13), level (+0x29), the graphic and its palette (+0x18F, +0x190), the battle data's
    /// unit id (+0x191) and its name id (+0x1FC).</summary>
    private static string LogUnits()
    {
        var text = new List<string>();
        void Say(string line)
        {
            _log.Info("autopilot: " + line);
            text.Add(line);
        }
        var units = _game.TurnCode.Units;
        Say($"the battle units at 0x{(nint)units:X}");
        try
        {
            // The loaded battle-data file (0x28416C: lea rax, [rcx+rcx*4]; shl rax, 7; add rax, [buffer]; ret) and var 0x32.
            var at = _game.Image.FindUnique("48 8D 04 89 48 C1 E0 07 48 03 05 ?? ?? ?? ?? C3");
            var buffer = *(byte**)GameImage.Rip(at + 8, 3, 7);
            var entry = Patches.Nemesis.GetVariable(0x32);
            var index = entry < 0x200 ? entry & 0x7F : entry - 0x200;
            var data = buffer + index * 0x280;
            Say($"var 0x32 = 0x{entry:X}; its loaded data (buffer 0x{(nint)buffer:X}): "
                      + string.Join(" ", Enumerable.Range(0, 16).Where(u => data[u * 40] != 0)
                          .Select(u => $"{data[u * 40]:X2}/{data[u * 40 + 10]:X2}")));
        }
        catch (Exception e)
        {
            Say($"the battle data: {e.Message}");
        }
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var u = units + k * Battle.UnitSize;
            if (u[0] == 0 && u[3] == 0)
                continue;
            Say($"unit {k}: sprites 0x{u[0]:X2} id 0x{u[1]:X2} job 0x{u[3]:X2} team 0x{u[5]:X2} skillsets "
                + $"0x{u[0x12]:X2} 0x{u[0x13]:X2} level {u[0x29]} graphic 0x{u[0x18F]:X2} palette 0x{u[0x190]:X2} "
                + $"battle id 0x{u[0x191]:X2} name 0x{*(ushort*)(u + 0x1FC):X}");
        }
        return string.Join("\n", text);
    }

    private static void LogAskedUi()
    {
        for (var v = 0; v < 3; v++)
        {
            var asked = _asked[v];
            var ids = Enumerable.Range(0, 0x400).Where(k => (asked[k >> 5] & (1u << (k & 31))) != 0).ToList();
            _log.Info($"autopilot: the UI's check {v} asked for actions {string.Join(" ", ids.Select(k => $"0x{k:X}"))}");
        }
    }

    /// <summary>edx = the action: set its bit in <paramref name="asked"/> (under 0x400); yes (al = 1) when it's *ui;
    /// else jmp [<paramref name="original"/>]. Only rax and the flags change.</summary>
    internal static byte[] UiStub(int* ui, uint* asked, nint original)
    {
        var c = new List<byte>();
        c.AddRange([0x81, 0xFA, 0x00, 0x04, 0x00, 0x00]);                  // cmp edx, 0x400
        c.AddRange([0x73, 0x0D]);                                          // jae over the bts (13 bytes)
        c.AddRange([0x48, 0xB8]); c.AddRange(BitConverter.GetBytes((long)asked)); // mov rax, asked
        c.AddRange([0x0F, 0xAB, 0x10]);                                    // bts [rax], edx
        c.AddRange([0x48, 0xB8]); c.AddRange(BitConverter.GetBytes((long)ui));    // mov rax, ui
        c.AddRange([0x3B, 0x10]);                                          // cmp edx, [rax]
        c.AddRange([0x75, 0x03]);                                          // jne original
        c.AddRange([0xB0, 0x01, 0xC3]);                                    // mov al, 1; ret
        c.AddRange([0xFF, 0x25]); c.AddRange(BitConverter.GetBytes((int)((byte*)original - 0)));  // placeholder
        return Fix(c, original);
    }

    private static byte[] Fix(List<byte> c, nint original)
    {
        // jmp [rip+disp]: the slot at original (the stub's +88), relative to the end of this instruction.
        var at = c.Count - 6;
        c.RemoveRange(at, 6);
        c.AddRange([0xFF, 0x25, 0, 0, 0, 0]);
        if (c.Count > 88)
            throw new InvalidOperationException("the UI stub is too big");
        var disp = 88 - c.Count;
        var bytes = c.ToArray();
        BitConverter.TryWriteBytes(bytes.AsSpan(at + 2), disp);
        return bytes;
    }

    /// <summary>GetActiveWindow: the game's main window (its biggest visible one), once it has one.</summary>
    [UnmanagedCallersOnly]
    private static nint ActiveWindow()
    {
        if (_mainWindow == 0 || !IsWindow(_mainWindow))
        {
            _mainWindow = 0;
            lock (Windows)
            {
                Windows.Clear();
                EnumWindows(&CollectWindow, 0);
                var best = 0L;
                foreach (var window in Windows)
                {
                    if (!IsWindowVisible(window) || !GetWindowRect(window, out var r))
                        continue;
                    var area = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
                    if (area > best)
                    {
                        best = area;
                        _mainWindow = window;
                    }
                }
            }
            if (_mainWindow != 0)
                _log.Info($"autopilot: the game's window 0x{_mainWindow:X} answers GetActiveWindow");
        }
        return _mainWindow != 0 ? _mainWindow : _gameActiveWindow();
    }

    [UnmanagedCallersOnly]
    private static int NoCursorPos(int x, int y) => 1;

    [UnmanagedCallersOnly]
    private static int NoClipCursor(nint rect) => 1;

    [UnmanagedCallersOnly]
    private static short AsyncKey(int key) => key == _key && key != 0 ? unchecked((short)0x8001) : _gameAsyncKey(key);

    [UnmanagedCallersOnly]
    private static short Key(int key) => key == _key && key != 0 ? unchecked((short)0x8000) : _gameKey(key);

    private static void Hold(string name, double seconds)
    {
        if (!Buttons.TryGetValue(name, out var button))
            throw new ArgumentException($"no button {name}");
        _log.Info($"autopilot: {name} for {seconds} s (polls so far {_polls})");
        _held = button;
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        _held = 0;
    }

    /// <summary>Mutes this process's audio session on every active output device (Windows' per-app mute, which holds for
    /// the streams the game opens later), so a test run makes no sound. The number of devices muted.</summary>
    private static int Mute()
    {
        var muted = 0;
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            enumerator.EnumAudioEndpoints(0, 1, out var devices); // eRender, DEVICE_STATE_ACTIVE
            devices.GetCount(out var count);
            var manager = typeof(IAudioSessionManager).GUID;
            for (uint k = 0; k < count; k++)
            {
                devices.Item(k, out var device);
                device.Activate(ref manager, 1, 0, out var activated); // CLSCTX_INPROC_SERVER
                ((IAudioSessionManager)activated).GetSimpleAudioVolume(0, 0, out var volume);
                var context = Guid.Empty;
                volume.SetMute(1, ref context);
                muted++;
            }
        }
        catch (Exception e)
        {
            _log.Error($"autopilot: muting the game failed: {e.Message}");
        }
        return muted;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int context, nint parameters, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
    }

    [ComImport, Guid("BFA971F1-4D5E-40BB-935E-967039BFBEE4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager
    {
        [PreserveSig] int GetAudioSessionControl(nint session, int flags, out nint control);
        [PreserveSig] int GetSimpleAudioVolume(nint session, int crossProcess, out ISimpleAudioVolume volume);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute(int mute, ref Guid context);
    }

    /// <summary>XInputGetState(user, state): pad 0 is the script's, connected; the others as the game has them.</summary>
    [UnmanagedCallersOnly]
    private static uint GetState(uint user, byte* state)
    {
        if (user != 0)
            return _gameGetState != null ? _gameGetState(user, state) : 1167; // ERROR_DEVICE_NOT_CONNECTED
        Interlocked.Increment(ref _polls);
        new Span<byte>(state, 16).Clear();
        var held = _held;
        if (held != 0)
            _packet++;
        *(uint*)state = _packet;
        *(ushort*)(state + 4) = (ushort)held;
        state[6] = (held & 0x10000) != 0 ? (byte)255 : (byte)0; // bLeftTrigger
        state[7] = (held & 0x20000) != 0 ? (byte)255 : (byte)0; // bRightTrigger
        return 0;
    }
}
