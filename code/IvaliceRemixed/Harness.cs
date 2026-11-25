using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace IvaliceRemixed;

/// <summary>
/// For test runs only (with <see cref="Autopilot"/>'s file beside the DLL): a named pipe, <c>\\.\pipe\ivalice-remixed</c>
/// (this user only), that takes a command per connection and answers, so a test can drive the running game and look
/// into it without restarting it. tools/harness.py is its client. A command is one of the autopilot's steps (key,
/// scenario, units, ...) or one of these:
/// <list type="bullet">
/// <item><c>state</c>: the world-map frames, the pad polls, the running event and var 0x27;</item>
/// <item><c>peek ADDR [N]</c>: N bytes (default 64) as hex; ADDR is <c>0x...</c> (absolute) or <c>+0x...</c> (an RVA in
/// the exe), and <c>[ADDR]</c> reads the pointer there first;</item>
/// <item><c>poke ADDR HEX...</c>: writes the bytes;</item>
/// <item><c>var ID</c>, <c>setvar ID VALUE</c>: an event variable;</item>
/// <item><c>event HEX PHASE</c>: in a running ReMixed battle, run that event phase through the ordinary battle
/// condition walker (for example <c>event 34D 2</c>);</item>
/// <item><c>dispatch KIND [STORY] [MONTH]</c>: from the stable Tavern menu, open the native Dispatch view directly,
/// optionally restricted to propositions, jobs, training or contests; <c>dispatchinfo</c> lists its virtual rows;</item>
/// <item><c>worldpad BITS [FRAMES]</c>: buttons held in the pad words the world map's buttons read, as a controller would
/// (0x08000800 Start; default 4 frames);</item>
/// <item><c>find text TEXT</c> / <c>find hex HEX...</c>: where the bytes are in the process's readable memory (text as
/// UTF-8 and UTF-16), up to 16 places;</item>
/// <item><c>call ADDR [ARG...]</c>: a game function called on the pipe's thread with up to four integer arguments (an
/// argument may be an address: <c>[+0x3CD9DB8]</c>), its result (and the text there, if any); for lookups only.</item>
/// <item><c>watch ADDR [LEN]</c>: a hardware write watch on the address (up to four; <see cref="Watch"/>), <c>watch</c>
/// the writes caught (each with the exe addresses on its stack), <c>watch off</c>.</item>
/// <item><c>testmode record</c> / <c>testmode auto</c>: the test mode's Record or RecordAndAutoBattle for the rest of
/// the run, so a test can play the party's own battle menus.</item>
/// <item><c>act SKILLSET ABILITY foe|INDEX|X,Y[,LEVEL]</c>: queued for the party's next turn menus, where a unit uses that
/// ability through the game's own confirm (<see cref="BattleAct"/>), without driving the menus; <c>act clear</c>.</item>
/// </list>
/// </summary>
internal static unsafe class Harness
{
    public const string PipeName = "ivalice-remixed";
    private static Log _log = null!;
    private static Game _game = null!;
    private static (nint Start, byte[] Bytes) _snap;

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public nint BaseAddress, AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nint RegionSize;
        public uint State, Protect, Type;
    }

    [DllImport("kernel32.dll")]
    private static extern nint VirtualQuery(nint address, out MemoryBasicInformation info, nint length);
    [DllImport("kernel32.dll")]
    private static extern bool VirtualProtect(nint address, nint size, uint protect, out uint old);

    public static void Install(Game game)
    {
        _log = game.Log;
        _game = game;
        try
        {
            BattleAct.Install(game);
        }
        catch (Exception e)
        {
            _log.Error($"harness: act isn't available: {e.Message}");
        }
        new Thread(Serve) { IsBackground = true, Name = "harness" }.Start();
        // The image base too: tools/harness.py `crashes` turns a crash report's address into an RVA with it.
        _log.Info($@"harness: listening on \\.\pipe\{PipeName} (the exe at 0x{game.Image.ImageStart:X})");
    }

    private static void Serve()
    {
        while (true)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                                                           PipeOptions.CurrentUserOnly);
                pipe.WaitForConnection();
                var line = ReadLine(pipe);
                string reply;
                try
                {
                    reply = Execute(line);
                }
                catch (Exception e)
                {
                    reply = $"error: {e.Message}";
                }
                var bytes = Encoding.UTF8.GetBytes(reply.TrimEnd('\n') + "\n.\n");
                pipe.Write(bytes);
                pipe.Flush();
                if (OperatingSystem.IsWindows())
                    pipe.WaitForPipeDrain();
            }
            catch (Exception e)
            {
                _log.Error($"harness: {e.Message}");
                Thread.Sleep(500);
            }
        }
    }

    private static string ReadLine(Stream stream)
    {
        var bytes = new List<byte>();
        int b;
        while ((b = stream.ReadByte()) >= 0 && b != '\n')
            bytes.Add((byte)b);
        return Encoding.UTF8.GetString([.. bytes]).Trim();
    }

    internal static string Execute(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "error: no command";
        switch (parts[0].ToLowerInvariant())
        {
            case "state":
            {
                var running = Patches.RemixedScenarios.RunningEvent;
                var depth = Patches.RemixedScenarios.WorldDepth == null ? -1 : *Patches.RemixedScenarios.WorldDepth;
                var top = depth > 0 && Patches.RemixedScenarios.WorldStates != null
                    ? Patches.RemixedScenarios.WorldStates[depth - 1] : -1;
                return $"world frames {Patches.RemixedScenarios.WorldFrames}, pad polls {Autopilot.Polls}, running event "
                       + $"{(running == null ? "?" : (*running).ToString(CultureInfo.InvariantCulture))}, var 0x1 = "
                       + $"{(Patches.Nemesis.GetVariable == null ? "?" : $"0x{Patches.Nemesis.GetVariable(1):X}")}, var 0x27 = "
                       + $"{(Patches.Nemesis.GetVariable == null ? "?" : $"0x{Patches.Nemesis.GetVariable(0x27):X}")}, ReMixed "
                       + $"event at 0x{Patches.RemixedEvents.LastPc:X} (op 0x{Patches.RemixedEvents.LastOp:X2}), "
                       + $"world depth {depth}, top state {top}, party location "
                       + (Patches.RemixedScenarios.PartyLocation == null ? "?" : (*Patches.RemixedScenarios.PartyLocation).ToString())
                       + $", autopilot done {Autopilot.Done.ToString().ToLowerInvariant()}, dispatch open "
                       + Patches.TavernStorage.DispatchView.ToString().ToLowerInvariant();
            }
            case "peek":
            {
                var at = Address(parts[1]);
                var count = parts.Length > 2 ? Number(parts[2]) : 64;
                return Dump(at, Math.Clamp(count, 1, 0x4000));
            }
            case "poke":
            {
                var at = Address(parts[1]);
                var data = Convert.FromHexString(string.Concat(parts.Skip(2)));
                if (!Readable(at, data.Length))
                    return $"error: 0x{at:X} isn't mapped";
                VirtualProtect(at, data.Length, 0x40, out var old); // PAGE_EXECUTE_READWRITE
                data.CopyTo(new Span<byte>((void*)at, data.Length));
                VirtualProtect(at, data.Length, old, out _);
                return $"wrote {data.Length} byte(s) at 0x{at:X}";
            }
            case "var":
                return $"var 0x{Number(parts[1]):X} = 0x{Patches.Nemesis.GetVariable(Number(parts[1])):X}";
            case "setvar":
                Patches.Nemesis.SetVariable(Number(parts[1]), Number(parts[2]), 0);
                return $"var 0x{Number(parts[1]):X} = 0x{Patches.Nemesis.GetVariable(Number(parts[1])):X}";
            case "event":
            {
                if (parts.Length != 3)
                    return "error: event HEX PHASE";
                var number = Convert.ToInt32(parts[1].Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16);
                return Patches.RemixedScenarios.RequestEventPhase(number, Number(parts[2]));
            }
            case "entries":
                return LogEntries();
            case "dispatch":
                return Patches.RemixedScenarios.RequestDispatchForHarness(parts.Length > 1 ? parts[1] : "all",
                    parts.Length > 2 ? Number(parts[2]) : 16, parts.Length > 3 ? Number(parts[3]) : 1);
            case "dispatchinfo":
                return Patches.TavernStorage.HarnessSummary();
            case "bar":
            {
                // The Tavern screen the mod last saw and its state (+0x98), for watching its menu.
                var bar = Patches.RemixedScenarios.BarScreen;
                return bar == 0 ? "no Tavern screen seen" : $"tavern screen 0x{bar:X}, state {*(int*)(bar + 0x98)}";
            }
            case "worldpad":
                // Buttons held in the PS1-style pad words the world map's button function reads (its world word's bits:
                // 0x08000800 Start), for a few frames: Steam Input fills them only from a controller, which a test run
                // has none of, and keys don't reach them.
                Patches.RemixedScenarios.InjectFrames = parts.Length > 2 ? Number(parts[2]) : 4;
                Patches.RemixedScenarios.InjectHeld = (uint)Number(parts[1]);
                return $"world pad 0x{Number(parts[1]):X} for {Patches.RemixedScenarios.InjectFrames} frame(s)";
            case "find":
            {
                var rest = line[(line.IndexOf(parts[1], StringComparison.Ordinal) + parts[1].Length)..].Trim();
                var patterns = parts[1].Equals("hex", StringComparison.OrdinalIgnoreCase)
                    ? [Convert.FromHexString(rest.Replace(" ", ""))]
                    : new[] { Encoding.UTF8.GetBytes(rest), Encoding.Unicode.GetBytes(rest) };
                var found = new List<string>();
                foreach (var pattern in patterns)
                    foreach (var at in Find(pattern, 16 - found.Count))
                        found.Add($"0x{at:X}{Where(at)}");
                return found.Count == 0 ? "not found" : string.Join("\n", found);
            }
            case "call":
            {
                // A game function by address with up to four integer arguments, on this thread: for read-only lookups.
                var at = Address(parts[1]);
                var args = parts.Skip(2).Select(a => a.StartsWith('+') || a.StartsWith('[') ? Address(a) : (nint)Convert.ToInt64(
                    a.Replace("0x", "", StringComparison.OrdinalIgnoreCase), a.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 16 : 10)).ToArray();
                Array.Resize(ref args, 4);
                var result = ((delegate* unmanaged<nint, nint, nint, nint, nint>)at)(args[0], args[1], args[2], args[3]);
                var text = Readable(result, 1) ? $"; as text: \"{Marshal.PtrToStringUTF8(result, Math.Min(200, Length(result)))}\"" : "";
                return $"0x{result:X}{Where(result)}{text}";
            }
            case "watch":
            {
                // watch ADDR [LEN]: a hardware write watch; watch off; watch (the hits so far).
                var start = _game.Image.ImageStart;
                var end = start + *(int*)(start + *(int*)(start + 0x3C) + 0x50);
                if (parts.Length == 1)
                    return Watch.Report(start, end);
                if (parts[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                    return Watch.Clear();
                return Watch.Add(Address(parts[1]), parts.Length > 2 ? Number(parts[2]) : 8);
            }
            case "code":
                return $"running event code at 0x{Patches.RemixedEvents.RunningCode:X}, at offset 0x{Patches.RemixedEvents.LastPc:X}";
            case "line":
            {
                // line ID [TEXT]: show TEXT ("\n" a line break) for the event line ID from its next look-up, or with no
                // TEXT, the line's own again: for trying a box's markup in the game without a build.
                var id = Number(parts[1]);
                if (parts.Length < 3)
                    return Patches.RemixedText.Overrides.TryRemove(id, out _) ? $"line {id}: its own text again" : $"line {id}: no override";
                var text = line[(line.IndexOf(parts[2], line.IndexOf(parts[1], StringComparison.Ordinal) + parts[1].Length,
                                              StringComparison.Ordinal))..].Replace("\\n", "\n");
                Patches.RemixedText.Overrides[id] = text;
                return $"line {id} shows \"{text}\"";
            }
            case "trace":
                Patches.RemixedEvents.TraceSteps = parts.Length > 1 && parts[1] == "on";
                return $"tracing ReMixed events' instructions: {Patches.RemixedEvents.TraceSteps}";
            case "act":
                return BattleAct.Run(parts);
            case "speed":
            {
                // speed 5|3|off: the game's fast-forward on (×5 or ×3) wherever it can be, whatever the setting, for a
                // case that plays a long scene; off gives the setting back.
                var speed = parts.Length > 1 ? parts[1] : "";
                Patches.FastForwardToggle.Forced = speed switch
                {
                    "5" => Patches.FastForwardToggle.Fast,
                    "3" => Patches.FastForwardToggle.Slow,
                    "off" => 0,
                    _ => -1,
                };
                if (Patches.FastForwardToggle.Forced < 0)
                {
                    Patches.FastForwardToggle.Forced = 0;
                    return "error: speed 5|3|off";
                }
                return speed == "off" ? "fast-forward: the setting's again" : $"fast-forward: ×{speed} wherever it can be";
            }
            case "testmode":
                // testmode record: the party's units stay the player's (a test that drives the battle menus); testmode
                // auto: the AI plays them again. For this run only; the settings stay as they are.
                return parts.Length > 1 && parts[1].Equals("record", StringComparison.OrdinalIgnoreCase)
                    ? TestRecorder.SetMode(Options.TestModeRule.Record)
                    : parts.Length > 1 && parts[1].Equals("auto", StringComparison.OrdinalIgnoreCase)
                        ? TestRecorder.SetMode(Options.TestModeRule.RecordAndAutoBattle)
                        : "error: testmode record|auto";
            case "battlemark":
                // battlemark off: ReMixed's battles keep no mark (var 0x4E) from now on, as before 0.9.34, so a
                // mid-battle save made in this run is an older build's; battlemark on: kept again.
                if (parts.Length < 2 || (parts[1] != "off" && parts[1] != "on"))
                    return "error: battlemark off|on";
                Patches.RemixedScenarios.BattleMarksOff = parts[1] == "off";
                return $"battle marks: {(Patches.RemixedScenarios.BattleMarksOff ? "not kept (an older build's saves)" : "kept")}";
            case "stage":
            {
                // stage [QUEST]: the remaster's own count of the story (quest 0 the main story), which its world map
                // greys roads and words the objective by; stage QUEST VALUE: set it, as an event of its own would.
                var quest = parts.Length > 1 ? Number(parts[1]) : 0;
                if (parts.Length > 2 && !Patches.WorldRoads.Poke(quest, Number(parts[2])))
                    return "error: the game's story stages aren't there yet";
                return $"stage of quest {quest} = {Patches.WorldRoads.Stage(quest)}";
            }
            case "roads":
                // roads off: the world map's roads as the remaster colours them (grey by its stage) even in a story of
                // ReMixed's own, as before Patches/WorldRoads.cs; roads on: no road greyed there again.
                if (parts.Length < 2 || (parts[1] != "off" && parts[1] != "on"))
                    return "error: roads off|on";
                Patches.WorldRoads.Off = parts[1] == "off";
                return $"world roads: {(Patches.WorldRoads.Off ? "the remaster's colours (a road greyed by its stage)" : "none greyed in ReMixed's own stories")}";
            case "snap":
            {
                // A copy of a range, for "diff" to compare with later.
                var at = Address(parts[1]);
                var count = Number(parts[2]);
                if (!Readable(at, count))
                    return $"error: 0x{at:X} (+0x{count:X}) isn't all mapped";
                _snap = (at, new ReadOnlySpan<byte>((void*)at, count).ToArray());
                return $"kept 0x{count:X} bytes from 0x{at:X}{Where(at)}";
            }
            case "diff":
            {
                // The aligned dwords that changed since "snap" (with "diff OLD NEW", only those that went from OLD to NEW).
                if (_snap.Bytes == null)
                    return "error: no snap";
                var (start, before) = _snap;
                int? from = parts.Length > 2 ? Number(parts[1]) : null, to = parts.Length > 2 ? Number(parts[2]) : null;
                var found = new List<string>();
                var total = 0;
                for (var k = 0; k + 4 <= before.Length; k += 4)
                {
                    var old = BitConverter.ToInt32(before, k);
                    var now = *(int*)(start + k);
                    if (old == now || (from != null && (old != from || now != to)))
                        continue;
                    total++;
                    if (found.Count < 64)
                        found.Add($"0x{start + k:X}{Where(start + k)}: 0x{old:X} -> 0x{now:X}");
                }
                return $"{total} changed\n" + string.Join("\n", found);
            }
            case "help":
                return "state | peek ADDR [N] | poke ADDR HEX | var ID | setvar ID VALUE | event HEX PHASE | "
                       + "entries | dispatch KIND [STORY] [MONTH] | dispatchinfo | find text T | find hex H | call ADDR ARGS | "
                       + "snap ADDR N | diff [OLD NEW] | worldpad BITS [FRAMES] | code | line ID [TEXT] | testmode record|auto | "
                       + "speed 5|3|off | battlemark off|on | stage [QUEST [VALUE]] | roads off|on | "
                       + "act SKILLSET ABILITY foe|INDEX|X,Y[,LEVEL] [TIMES] | act clear | "
                       + "an autopilot step (key K, press B, scenario S, units, action A, ui0-2 A, pad BITS, log T, quit)";
            default:
                return Autopilot.Step(line);
        }
    }

    private static int Number(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt32(text[2..], 16) : int.Parse(text, CultureInfo.InvariantCulture);

    /// <summary>0x... absolute, +0x... (or +...) an RVA in the exe, [..] the pointer stored there.</summary>
    internal static nint Address(string text)
    {
        // [X] is the pointer kept at X, and a +N after the bracket adds to it, so a chain of the game's objects can be
        // walked: [[[+0x3CD9DA8]+0x10]+0xA0]+0x40 is the world module's map screen pointer's place.
        if (text.StartsWith('['))
        {
            var depth = 0;
            var close = -1;
            for (var k = 0; k < text.Length && close < 0; k++)
            {
                depth += text[k] == '[' ? 1 : text[k] == ']' ? -1 : 0;
                if (depth == 0)
                    close = k;
            }
            if (close < 0)
                throw new InvalidOperationException($"{text}: a bracket isn't closed");
            var at = Address(text[1..close]);
            if (!Readable(at, 8))
                throw new InvalidOperationException($"0x{at:X} isn't mapped");
            var rest = text[(close + 1)..].TrimStart('+');
            return *(nint*)at
                   + (rest.Length == 0 ? 0 : (nint)Convert.ToInt64(rest.Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16));
        }
        if (text.StartsWith('+'))
            return _game.Image.ImageStart + (nint)Convert.ToInt64(text[1..].Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16);
        return (nint)Convert.ToInt64(text.Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16);
    }

    private static int Length(nint text)
    {
        var n = 0;
        while (n < 200 && Readable(text + n, 1) && *(byte*)(text + n) != 0)
            n++;
        return n;
    }

    private static string Where(nint at)
    {
        var start = _game.Image.ImageStart;
        return at >= start && at < start + 0x4000000 ? $" (exe+0x{at - start:X})" : "";
    }

    private static string Dump(nint at, int count)
    {
        if (!Readable(at, count))
            return $"error: 0x{at:X} (+{count}) isn't all mapped";
        var text = new StringBuilder($"0x{at:X}{Where(at)}:\n");
        for (var row = 0; row < count; row += 16)
        {
            var bytes = new ReadOnlySpan<byte>((void*)(at + row), Math.Min(16, count - row));
            text.Append($"{row:X4}  {Convert.ToHexString(bytes).ToLowerInvariant().Chunk(2).Select(c => new string(c)).Aggregate((a, b) => a + " " + b),-47}  ");
            foreach (var b in bytes)
                text.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            text.Append('\n');
        }
        return text.ToString();
    }

    /// <summary>The current scenario's sixteen raw 40-byte battle-entry records.</summary>
    private static string LogEntries()
    {
        var scenario = Patches.Nemesis.GetVariable(0x27);
        byte* data;
        string source;
        if (Patches.RemixedScenarios.OwnEntries.TryGetValue(scenario, out var own))
        {
            data = (byte*)own;
            source = "the mod's private entry";
        }
        else
        {
            var at = _game.Image.FindUnique(Patches.UnitPresets.EntryDataPattern);
            var buffer = *(byte**)GameImage.Rip(at + 8, 3, 7);
            var entry = Patches.Nemesis.GetVariable(0x32);
            var index = entry < 0x200 ? entry & 0x7F : entry - 0x200;
            data = buffer + index * Patches.RemixedScenarios.EntryBytes;
            source = $"loaded entry 0x{entry:X}";
        }
        var text = new List<string> { $"scenario 0x{scenario:X}, {source} at 0x{(nint)data:X}" };
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var u = data + k * CreateUnit.RecordSize;
            if (u[0] == 0)
                continue;
            text.Add($"entry {k}: sprites 0x{u[0]:X2} flags 0x{u[1]:X2} name 0x{u[2]:X2} job 0x{u[3]:X2} "
                     + $"secondary 0x{u[0x0B]:X2} team 0x{u[0x18]:X2} x 0x{u[0x19]:X2} y 0x{u[0x1A]:X2} "
                     + $"battle id 0x{u[0x20]:X2}");
        }
        return string.Join("\n", text);
    }

    private static bool IsReadable(in MemoryBasicInformation m) =>
        m.State == 0x1000 && (m.Protect & 0x100) == 0 && (m.Protect & 0xEE) != 0; // committed, not guard, a readable kind

    internal static bool Readable(nint at, int count)
    {
        var end = at + count;
        while (at < end)
        {
            if (VirtualQuery(at, out var m, sizeof(MemoryBasicInformation)) == 0 || !IsReadable(m))
                return false;
            at = m.BaseAddress + m.RegionSize;
        }
        return true;
    }

    private static List<nint> Find(byte[] pattern, int most)
    {
        var found = new List<nint>();
        if (pattern.Length == 0 || most <= 0)
            return found;
        nint at = 0x10000;
        while (found.Count < most && VirtualQuery(at, out var m, sizeof(MemoryBasicInformation)) != 0)
        {
            var next = m.BaseAddress + m.RegionSize;
            if (IsReadable(m) && m.RegionSize < 0x40000000)
            {
                var span = new ReadOnlySpan<byte>((void*)m.BaseAddress, (int)m.RegionSize);
                var offset = 0;
                while (found.Count < most)
                {
                    var k = span[offset..].IndexOf(pattern);
                    if (k < 0)
                        break;
                    found.Add(m.BaseAddress + offset + k);
                    offset += k + 1;
                }
            }
            if (next <= at)
                break;
            at = next;
        }
        return found;
    }
}
