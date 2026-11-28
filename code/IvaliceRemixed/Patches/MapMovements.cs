using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// The redrawn maps' moving parts (a gate, a door, a drawbridge) in ReMixed's scenes.
/// <list type="bullet">
/// <item>The PS1's field objects (event instructions 0x55 and 0x57) move a part of the classic map, which the remaster
/// still keeps under its redrawn one. As a field object starts (0x24F5A0 with 0x83, from the event's frame code
/// 0x23A0B4) the remaster asks two lists compiled into its code what the redrawn map does, by the scene's number
/// (var 0x27) and the object: 0xC729C starts or stops the redrawn map's animations (the map view's 0x20 entries,
/// [[0x3CC96B8] + 0x24280] + 0x20 + 0x17BDC × n: a kind from the map's RefinedBgAnim rows, a state 1 or 2, a clock)
/// and 0xC9538 sets and clears marks of the other view ([[0x3CC96B8] + 0x24270]). Eagrose Castle's gate is scene 434
/// (0x1B2), object 1: animations 1 and 2.</item>
/// <item>The port runs ReMixed's scenes under numbers of its own (tools/build_mod.py REMIXED_SCENARIOS), so the lists
/// knew none of them: at Eagrose, ReMixed's arrival (its 0x06E, the port's 0x36A) used the switch and the classic gate
/// rose under a redrawn one that stood still, and Ramza walked in through it (a tester on 0.9.39, 2026-11-26). The
/// two calls now go through the mod, which gives the lists the remaster's scene each of ReMixed's stands in for (the
/// build's MAP_MOVEMENT_SCENES), or 0, a scene on neither list, for one of ReMixed's that stands in for none: some of
/// the port's numbers are the remaster's own (0x1B2 is ReMixed's Depths of Murond), and those mustn't move another
/// map by the scene that had the number.</item>
/// <item>The remaster's own instruction for the same animations, 0xCD (u32 animation, u32 mode: state 1 for mode 1,
/// else 2; 0x23E826), is the upgrade's PostEvent in one of ReMixed's events, so the build has ReMixed's event call
/// a routine of the mod's instead (<see cref="Routine"/>, the animation and the mode in two of the upgrade's
/// words) where the remaster's script for the same scene has it (the build's MAP_ANIMATION_CALLS).</item>
/// </list>
/// Data: {"scenes": [[the port's scenario, the remaster's scene or 0], ...], "listed": [[a scene of the remaster's
/// lists, [its objects]], ...] (for the offline check)}. Installed by <see cref="Rules"/> with "map-movements".
/// </summary>
internal static unsafe class MapMovements
{
    // mov edx, eax (var 0x27); mov r8d, r15d (the object); mov rcx, [rcx+0x24270]; call 0xC9538.
    internal const string MarksCallPattern = "8B D0 45 8B C7 48 8B 89 70 42 02 00 E8";
    internal const int MarksCallAt = 12;
    // mov rcx, [the screens]; lea r8d, [rdi-5] (the object's kind less 5); mov edx, eax (var 0x27); mov [rsp+0x20], ebx;
    // mov r9d, r15d (the object); mov rcx, [rcx+0x24280]; call 0xC729C.
    internal const string AnimationsCallPattern =
        "48 8B 0D ?? ?? ?? ?? 44 8D 47 FB 8B D0 89 5C 24 20 45 8B CF 48 8B 89 80 42 02 00 E8";
    internal const int AnimationsCallAt = 27, ViewAt = 0x24280;
    /// <summary>The map view: its map (-1 with none loaded), its animations (a kind, a state, then 0x24 bytes of
    /// clocks) and the kinds its map's rows gave them (8 bytes an animation, the kind first).</summary>
    internal const int ViewMap = 0x18, Animations = 0x20, AnimationBytes = 0x17BDC, AnimationCount = 0x20,
                       AnimationKinds = 0x2F7BA0, ClockBytes = 0x24;
    /// <summary>The frames of the view's animations ([view + 0x10]: 0x7ED8 bytes an animation, 0xE18 a part, a
    /// part's frame count at its +0xE10; 0 for a part the animation doesn't have), read by the frame code 0xC7664.</summary>
    internal const int ViewFrames = 0x10, FrameBytes = 0x7ED8, PartBytes = 0xE18, PartFrames = 0xE10, Parts = 9;
    /// <summary>The routine's mode for an animation left on its last frame (0 and 1 are the remaster's 0xCD's).</summary>
    internal const int AtEnd = 2;
    internal const int StubSize = 12;

    /// <summary>The mod's routine for one of ReMixed's events (0xCF with this address): the remaster's 0xCD, with the
    /// animation in <see cref="AnimationWord"/> and the mode in <see cref="ModeWord"/> (the words the extended
    /// statuses' routine borrows too: each is written just before its call).</summary>
    internal const uint Routine = 0x000000C8;
    internal const int AnimationWord = 0xB7E, ModeWord = 0xB7F;

    /// <summary>The port's scenario -> the remaster's scene whose map movements it takes (0: none).</summary>
    internal static readonly Dictionary<int, int> Scenes = [];

    /// <summary>For the offline check: the remaster's own lists as the build has them (its REMASTER_MAP_MOVEMENTS),
    /// the scene -> the field objects that move something there.</summary>
    internal static readonly Dictionary<int, int[]> Listed = [];

    /// <summary>For the offline check: the two calls, the functions they called, their stubs, and the screens' pointer.</summary>
    internal static nint MarksCall { get; private set; }
    internal static nint AnimationsCall { get; private set; }
    internal static nint MarksStub { get; private set; }
    internal static nint AnimationsStub { get; private set; }
    internal static nint* Screens { get; private set; }
    internal static delegate* unmanaged<nint, int, int, void> GameMarks { get; private set; }
    internal static delegate* unmanaged<nint, int, int, int, int, void> GameAnimations { get; private set; }

    private static Log? _log;
    private static readonly HashSet<long> Noted = [];

    public static string Install(Game game, JsonElement data)
    {
        _log = game.Log;
        Scenes.Clear();
        foreach (var row in data.GetProperty("scenes").EnumerateArray())
            Scenes[row[0].GetInt32()] = row[1].GetInt32();
        Listed.Clear();
        if (data.TryGetProperty("listed", out var listed))
            foreach (var row in listed.EnumerateArray())
                Listed[row[0].GetInt32()] = [.. row[1].EnumerateArray().Select(o => o.GetInt32())];
        var marks = game.Image.FindUnique(MarksCallPattern) + MarksCallAt;
        var site = game.Image.FindUnique(AnimationsCallPattern);
        var animations = site + AnimationsCallAt;
        Screens = (nint*)GameImage.Rip(site, 3, 7);
        GameMarks = (delegate* unmanaged<nint, int, int, void>)(marks + 5 + *(int*)(marks + 1));
        GameAnimations = (delegate* unmanaged<nint, int, int, int, int, void>)(animations + 5 + *(int*)(animations + 1));
        // Each of the two functions is called from here alone, so the calls are turned, not the functions hooked:
        // both start with a compare and a conditional jump, too short a start to move.
        MarksStub = Turn(marks, (nint)(delegate* unmanaged<nint, int, int, void>)&Marks);
        AnimationsStub = Turn(animations, (nint)(delegate* unmanaged<nint, int, int, int, int, void>)&Animate);
        (MarksCall, AnimationsCall) = (marks, animations);
        var stood = Scenes.Count(p => p.Value != 0);
        return $"the redrawn maps' moving parts in ReMixed's scenes: {stood} of its scenes move a map as the remaster's "
               + $"scene each stands in for, the other {Scenes.Count - stood} as none";
    }

    /// <summary>Gives ReMixed's events the remaster's 0xCD as a routine (<see cref="Routine"/>).</summary>
    internal static void AddRoutine(EventInstructionUpgrade.Interfaces.IEventInstructions api) =>
        api.AddRoutine(Routine, () => Note(Start(View, api.GetVariable(AnimationWord), api.GetVariable(ModeWord))));

    /// <summary>The scene whose map movements a scenario takes: the remaster's own number for a scenario of its
    /// own, the scene one of ReMixed's stands in for, or 0 (on neither list) for one of ReMixed's that has none.</summary>
    internal static int Scene(int scenario) => Scenes.TryGetValue(scenario, out var scene) ? scene : scenario;

    /// <summary>The map view ([[the screens] + 0x24280]), or 0 before the game has one.</summary>
    internal static nint View => Screens == null || *Screens == 0 ? 0 : *(nint*)(*Screens + ViewAt);

    /// <summary>The remaster's 0xCD on a map view: animation <paramref name="animation"/> takes its kind from the
    /// map's rows and state 1 (<paramref name="mode"/> 1) or 2, its clocks cleared: it stands on its first frame, or
    /// plays from it. Mode 2 is the mod's own (<see cref="AtEnd"/>): the animation stands on its last frame, as one
    /// that plays once is left when it has played (the game's frame code 0xC7664: each of an animation's nine parts
    /// has a clock, a float, stepped while the state is 2; when one comes to its frame count it is put on its last
    /// frame and the state goes back to 1). A scene uses it for a part the redrawn map draws moved from the start
    /// where ReMixed's scene moves it later: Fort Zeakden's door, which the remaster's own scene opens at once.
    /// Returns a line for the trace.</summary>
    internal static string Start(nint view, int animation, int mode)
    {
        if (view == 0 || *(int*)(view + ViewMap) == -1)
            return $"map animation {animation}: no redrawn map is loaded; nothing started";
        if (animation is < 0 or >= AnimationCount)
            return $"map animation {animation}: past the view's {AnimationCount}; nothing started";
        var entry = (int*)(view + Animations + (nint)AnimationBytes * animation);
        entry[0] = *(int*)(view + AnimationKinds + 8 * animation);
        entry[1] = mode is 1 or AtEnd ? 1 : 2;
        new Span<byte>(entry + 2, ClockBytes).Clear();
        if (mode != AtEnd)
            return $"map animation {animation} (kind {entry[0]}) set to state {entry[1]}";
        // The frame code's own steps, a frame at a time, until a part comes to its last frame: there the game stops
        // an animation that plays once (the parts after it in that step still take the step; the others stay).
        var frames = *(nint*)(view + ViewFrames);
        var clocks = (float*)(entry + 2);
        var ended = false;
        for (var step = 0; frames != 0 && step < 0x10000 && !ended; step++)
        {
            var any = false;
            for (var part = 0; part < Parts; part++)
            {
                var count = *(int*)(frames + (nint)FrameBytes * animation + PartBytes * part + PartFrames);
                if (count == 0)
                    continue;
                any = true;
                clocks[part] += 1f;
                if (count > (int)clocks[part])
                    continue;
                clocks[part] = count - 1;
                ended = true;
            }
            if (!any)
                break;
        }
        return $"map animation {animation} (kind {entry[0]}) set on its last frame{(ended ? "" : " (it has no frames)")}";
    }

    [UnmanagedCallersOnly]
    private static void Marks(nint view, int scenario, int item) => GameMarks(view, Scene(scenario), item);

    [UnmanagedCallersOnly]
    private static void Animate(nint view, int scenario, int part, int item, int direction)
    {
        var scene = Scene(scenario);
        if (scene != scenario && scene != 0 && _log != null)
        {
            bool first;
            lock (Noted)
                first = Noted.Count < 64 && Noted.Add(((long)scenario << 32) | (uint)item);
            if (first)
                _log.Info($"map movements: scenario 0x{scenario:X}'s field object {item} moves the redrawn map as the "
                          + $"remaster's scene {scene}'s");
        }
        GameAnimations(view, scene, part, item, direction);
    }

    /// <summary>Turns a call to a stub near it that jumps to <paramref name="detour"/> (mov rax, the detour; jmp rax).
    /// Returns the stub.</summary>
    private static nint Turn(nint call, nint detour)
    {
        if (*(byte*)call != 0xE8)
            throw new InvalidOperationException("a call of the map's movement lists isn't where this DLL expects it");
        var stub = (nint)GameImage.AllocateCodeNear(call, StubSize);
        var code = StubCode(detour);
        code.CopyTo(new Span<byte>((void*)stub, code.Length));
        var patch = new byte[5];
        patch[0] = 0xE8;
        BitConverter.TryWriteBytes(patch.AsSpan(1), checked((int)(stub - (call + 5))));
        GameImage.WriteCode(call, patch);
        return stub;
    }

    internal static byte[] StubCode(nint detour)
    {
        var code = new byte[StubSize];
        code[0] = 0x48; code[1] = 0xB8;              // mov rax, detour
        BitConverter.TryWriteBytes(code.AsSpan(2), (long)detour);
        code[10] = 0xFF; code[11] = 0xE0;            // jmp rax
        return code;
    }

    /// <summary>A line of the routine's, each time: its events call it once or twice a scene (the tracer keeps only its
    /// first 128 distinct lines of a run).</summary>
    private static void Note(string text) => _log?.Info("map movements: " + text);
}
