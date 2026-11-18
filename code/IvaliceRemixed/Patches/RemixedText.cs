using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's text made while its events run: its dispatch tavern's menus, prices and reports
/// (docs/analysis/dispatch-missions-plan.md). ReMixed's text codes that take the next of its list words
/// (<see cref="RemixedEvents.Words"/>; in a line the k-th of them takes word k): 0xEB shows that word + 1 as one of the
/// event's own messages (a name; a menu's rows with their line breaks; a colour; a plural's "s"), 0xE4 shows it as a
/// number; and 0xE6 xx shows word xx as a number. The build keeps them as tokens in <see cref="FileName"/>: the lines
/// with slots by their Ids, and each message as a slot shows it. This fills them in as the game reads a line: the
/// event line lookup (0x1DB0FC: rcx the text table, edx the line's Id; it returns the line's UTF-8 text) returns the
/// filled-in line instead, from a buffer kept per line.
/// </summary>
/// <remarks>
/// A menu's rows are its lines between &lt;cursor&gt; and &lt;cursorend&gt;; a slot that shows nothing makes no row, as
/// in ReMixed, where a slot's message brings its own line break. Elsewhere a line break a slot brings is a space, as the
/// remaster flows its lines. The lookup's callers keep only rax across it (ExeTool's liveregs).
/// </remarks>
internal static unsafe class RemixedText
{
    internal const string FileName = "remixed-slots.json";
    internal const string Message = "{eb}", Number = "{n}", WordStart = "{w:";
    // The event line lookup (0x1DB0FC): sub rsp, 0x28; mov rcx, [rcx+0x28]; test rcx, rcx; je; cmp qword [rcx+0x20], 0;
    // je; add rcx, 0x18; call (the record by Id); mov r10, rax; test rax, rax; je; movsxd rax, [rax+4]; add rax, r10.
    internal const string LinePattern =
        "48 83 EC 28 48 8B 49 28 48 85 C9 74 21 48 83 79 20 00 74 1A 48 83 C1 18 E8 ?? ?? ?? ?? 4C 8B D0 48 85 C0 74 09 48 63 40 04 49 03 C2";

    /// <summary>The lines with slots, by Id: the event (the mod's number) and the line with its tokens.</summary>
    internal static readonly Dictionary<int, (int Event, string Template)> Lines = [];
    /// <summary>The lines with slots no one speaks (a unit's card, a mission's needs), by Id: laid out as ReMixed has
    /// them, their line breaks and spacing kept, where a speaker's line flows.</summary>
    internal static readonly HashSet<int> LaidOut = [];
    /// <summary>The lines ReMixed shows in a frameless line (a MESSAGE's place 8: its Treasure Wheel's top and bottom
    /// lines), by Id: on the PS1 the event goes on past them at once, so their boxes go on without a press
    /// (<see cref="TallMenus"/>).</summary>
    internal static readonly HashSet<int> PlainLines = [];
    /// <summary>Each message of those events as a slot shows it, by (event, message number).</summary>
    internal static readonly Dictionary<(int Event, int Message), string> Parts = [];
    /// <summary>Each line with slots and its twin (the build's copy of it), both ways: the game looks a line's text up
    /// once while its box shows it, so <see cref="Alternate"/> shows the two by turns and each showing is filled in
    /// afresh (a unit's card as the picker moves).</summary>
    internal static readonly Dictionary<int, int> Twins = [];
    private static readonly HashSet<int> TwinNext = []; // pairs (by their lower Id) whose twin shows next
    internal const int MessageAt = 3, ChangeAt = 2;

    /// <summary>A ReMixed instruction about to run (after <see cref="RemixedScenarios.MoveMessage"/>): a MESSAGE (0x10,
    /// its line's Id at +3) or 0x51 (at +2) that shows a line with slots shows it or its twin, the other of the two
    /// from last time.</summary>
    internal static void Alternate(byte* instruction)
    {
        var at = instruction[0] switch { 0x10 => MessageAt, 0x51 => ChangeAt, _ => 0 };
        if (at == 0)
            return;
        var id = (int)*(uint*)(instruction + at);
        if (!Twins.TryGetValue(id, out var twin))
            return;
        var pair = Math.Min(id, twin);
        var useTwin = TwinNext.Remove(pair);
        if (!useTwin)
            TwinNext.Add(pair);
        *(uint*)(instruction + at) = (uint)(useTwin ? Math.Max(id, twin) : pair);
    }
    /// <summary>Text the harness's "line" command puts in place of a line's own, by Id.</summary>
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> Overrides = new();
    private static readonly Dictionary<int, (nint Buffer, int Size)> Buffers = [];
    private static delegate* unmanaged<nint, int, nint> _line;
    private static Log _log = null!;
    private static int _failed, _filled;

    public static void Install(Game game, string path)
    {
        _log = game.Log;
        _roster = (nint)game.Units.Roster;
        _battleUnits = (nint)game.TurnCode.Units;
        Read(File.ReadAllText(path));
        if (Lines.Count == 0)
        {
            _log.Info($"remixed text: no lines with slots in {FileName}");
            return;
        }
        _line = (delegate* unmanaged<nint, int, nint>)game.Hooks.Hook(
            game.Image.FindUnique(LinePattern), (nint)(delegate* unmanaged<nint, int, nint>)&OnLine, "the event line lookup");
        _log.Info($"remixed text: {Lines.Count} line(s) with slots in {Parts.Select(p => p.Key.Event).Distinct().Count()} event(s), " +
                  "filled in as they show");
    }

    /// <summary>The build's file: {"events": [{"event", "first" (message 1's line Id), "lines" and "parts" by message
    /// number}]}.</summary>
    internal static void Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var e in document.RootElement.GetProperty("events").EnumerateArray())
        {
            int number = e.GetProperty("event").GetInt32(), first = e.GetProperty("first").GetInt32();
            Events.Add(number);
            foreach (var line in e.GetProperty("lines").EnumerateObject())
                Lines[first + int.Parse(line.Name) - 1] = (number, line.Value.GetString() ?? "");
            if (e.TryGetProperty("layout", out var layout))
                foreach (var n in layout.EnumerateArray())
                    LaidOut.Add(first + n.GetInt32() - 1);
            if (e.TryGetProperty("plain", out var plain))
                foreach (var n in plain.EnumerateArray())
                    PlainLines.Add(first + n.GetInt32() - 1);
            if (e.TryGetProperty("twins", out var twins))
                foreach (var twin in twins.EnumerateObject())
                {
                    int one = first + int.Parse(twin.Name) - 1, other = first + twin.Value.GetInt32() - 1;
                    Lines[other] = Lines[one];
                    if (LaidOut.Contains(one))
                        LaidOut.Add(other);
                    if (PlainLines.Contains(one))
                        PlainLines.Add(other);
                    Twins[one] = other;
                    Twins[other] = one;
                }
            foreach (var part in e.GetProperty("parts").EnumerateObject())
                Parts[(number, int.Parse(part.Name))] = part.Value.GetString() ?? "";
        }
    }

    /// <summary>What a 0xEB slot's word names from 0x1000 up (seen in ReMixed's tavern on the PS1: a unit's card held
    /// 0x5801 for roster slot 1's name and 0x304A for Squire; its Treasure Wheel's prize 0x3803 for item 3, "Blind
    /// Knife"; its Nemesis's return, 0x11D, names its speaker 0x40xx, the battle unit in slot xx, +0x18A): 0x30xx a job,
    /// 0x38xx an item, 0x40xx a battle unit's name, 0x58xx a roster unit's name, 0x7xxx an ability, 0xBxxx a skillset,
    /// each the remaster's (its tables' names at +8 of a row, an item's at +4, an offset from there; the units' from the
    /// game's name lookup); else nothing.</summary>
    internal static string Reference(int word)
    {
        const int jobs = 46, items = 45, abilities = 39, skillsets = 41, nameField = 8, itemNameField = 4;
        return (word >> 12) switch
        {
            0x3 when (word & 0x800) != 0 => TableText(items, word & 0x7FF, itemNameField),
            0x3 => TableText(jobs, word & 0xFFF, nameField),
            0x4 when (word & 0xF00) == 0 => BattleUnitName(word & 0xFF),
            0x5 when (word & 0xF00) == 0x800 => UnitName(word & 0xFF),
            0x7 => TableText(abilities, word & 0xFFF, nameField),
            0xB => TableText(skillsets, word & 0xFFF, nameField),
            _ => "",
        };
    }

    private static string TableText(int table, int key, int field)
    {
        var row = OptionsPage.Row(table, key);
        return row == 0 ? "" : Marshal.PtrToStringUTF8(row + field + *(int*)(row + field)) ?? "";
    }

    /// <summary>A roster unit's name: its record's own text (+0xDC), else its name id's (+0x230) through the game's
    /// lookup (<see cref="NameGuard"/>).</summary>
    private static string UnitName(int slot)
    {
        if (_roster == 0 || slot >= RosterSlots)
            return "";
        var own = RemixedScenarios.RosterName(slot);
        if (own.Length > 0 || NameGuard.At == 0)
            return own;
        var record = _roster + slot * RosterBytes;
        var name = ((delegate* unmanaged<int, nint, nint>)NameGuard.At)(*(ushort*)(record + 0x230), record + 0xDC);
        return name == 0 ? "" : Marshal.PtrToStringUTF8(name) ?? "";
    }

    private const int RosterSlots = 54, RosterBytes = 0x258;
    private static nint _roster;

    /// <summary>A battle unit's name, as the battle shows it: its name id's (+0x1FC, the PS1's +0x16C, a generic's
    /// random name) through the game's lookup, with its own text (+0x14C) for a blank row (<see cref="NameGuard"/>);
    /// nothing for an empty slot.</summary>
    private static string BattleUnitName(int slot)
    {
        if (_battleUnits == 0 || slot >= Battle.UnitCount)
            return "";
        var unit = _battleUnits + slot * Battle.UnitSize;
        if (*(byte*)(unit + 1) == 0xFF)
            return "";
        if (NameGuard.At == 0)
            return Marshal.PtrToStringUTF8(unit + UnitNameText) ?? "";
        var name = ((delegate* unmanaged<int, nint, nint>)NameGuard.At)(*(ushort*)(unit + UnitNameId), unit + UnitNameText);
        return name == 0 ? "" : Marshal.PtrToStringUTF8(name) ?? "";
    }

    private const int UnitNameId = 0x1FC, UnitNameText = 0x14C;
    private static nint _battleUnits;

    private static nint _table;

    /// <summary>An event line's text as the game shows it (filled in, for a line with slots); null before the game has
    /// looked a line up (the text table not known yet).</summary>
    internal static string? TextOf(int id)
    {
        if (Buffers.TryGetValue(id, out var kept) && (Lines.ContainsKey(id) || Overrides.ContainsKey(id)))
            return Marshal.PtrToStringUTF8(kept.Buffer);
        if (_table == 0 || _line == null)
            return null;
        var text = _line(_table, id);
        return text == 0 ? null : Marshal.PtrToStringUTF8(text);
    }

    /// <summary>An event line's text as the game shows it, as UTF-8 the game can read; 0 before the game has looked a
    /// line up.</summary>
    internal static nint TextPointer(int id)
    {
        if (Buffers.TryGetValue(id, out var kept) && (Lines.ContainsKey(id) || Overrides.ContainsKey(id)))
            return kept.Buffer;
        return _table == 0 || _line == null ? 0 : _line(_table, id);
    }

    [UnmanagedCallersOnly]
    private static nint OnLine(nint table, int id)
    {
        _table = table;
        var text = _line(table, id);
        try
        {
            if (Overrides.TryGetValue(id, out var given))
                return Keep(id, given);
            if (Lines.TryGetValue(id, out var line))
            {
                var words = RemixedEvents.Words;
                var filled = Fill(line.Template, k => words == null ? 0 : words[k & 0x7F],
                                  w => w < 0x1000 ? Parts.GetValueOrDefault((line.Event, w + 1)) : Reference(w),
                                  LaidOut.Contains(id));
                if (Interlocked.Increment(ref _filled) <= 5)
                    _log.Info($"remixed text: line {id} (event 0x{line.Event:X}) shows \"{filled}\"");
                return Keep(id, filled);
            }
        }
        catch (Exception e)
        {
            if (Interlocked.Increment(ref _failed) <= 3)
                _log.Error($"remixed text: line {id}: {e.Message}");
        }
        return text;
    }

    /// <summary>A line with its slots filled in: each 0xEB slot what its word names (<paramref name="slot"/>: below
    /// 0x1000 the event's message word + 1, whose own slots go on from the same word; above, <see cref="Reference"/>),
    /// each 0xE4 slot its word as a number, each 0xE6 slot the word it names; then tidied (<see cref="Tidy"/>; a line
    /// <paramref name="laidOut"/> keeps its line breaks and spacing).</summary>
    internal static string Fill(string template, Func<int, int> word, Func<int, string?> slot, bool laidOut = false)
    {
        var k = 0;
        return Tidy(Expand(template, word, slot, ref k, 0), laidOut);
    }

    private static string Expand(string text, Func<int, int> word, Func<int, string?> part, ref int k, int depth)
    {
        var out_ = new StringBuilder(text.Length + 32);
        for (var i = 0; i < text.Length;)
        {
            if (string.CompareOrdinal(text, i, Message, 0, Message.Length) == 0)
            {
                var inserted = part(word(k++)) ?? "";
                out_.Append(depth < 2 ? Expand(inserted, word, part, ref k, depth + 1) : inserted);
                i += Message.Length;
            }
            else if (string.CompareOrdinal(text, i, Number, 0, Number.Length) == 0)
            {
                out_.Append(word(k++));
                i += Number.Length;
            }
            else if (i + 4 <= text.Length && text[i] == '{' && text[i + 1] == 'n' && text[i + 2] is >= '1' and <= '9'
                     && text[i + 3] == '}')
            {
                // ReMixed's E8: the number right-aligned in a field of that many characters, as its boxes lay them out
                var number = word(k++).ToString(System.Globalization.CultureInfo.InvariantCulture);
                for (var pad = text[i + 2] - '0' - number.Length; pad > 0; pad--)
                    out_.Append(Space);
                out_.Append(number);
                i += 4;
            }
            else if (i + 6 <= text.Length && string.CompareOrdinal(text, i, WordStart, 0, WordStart.Length) == 0
                     && text[i + 5] == '}' && int.TryParse(text.AsSpan(i + 3, 2), System.Globalization.NumberStyles.HexNumber,
                                                            null, out var index))
            {
                out_.Append(word(index));
                i += 6;
            }
            else
                out_.Append(text[i++]);
        }
        return out_.ToString();
    }

    /// <summary>A menu's rows (between &lt;cursor&gt; and &lt;cursorend&gt;: each trimmed, the empty ones left out) and
    /// the rest flowed (a line break a slot brought is a space; spaces run together), keeping the line break the build
    /// puts between a menu's question and its rows; a line <paramref name="laidOut"/> keeps its line breaks and spacing
    /// (<see cref="Lay"/>). Its colours paired up (<see cref="Balance"/>).</summary>
    internal static string Tidy(string text, bool laidOut = false)
    {
        const string open = "<cursor>", close = "<cursorend>";
        var start = text.IndexOf(open, StringComparison.Ordinal);
        var end = start < 0 ? -1 : text.IndexOf(close, start, StringComparison.Ordinal);
        if (end < 0)
            return laidOut ? Lay(text) : Flow(Balance(text));
        var lead = text[..start];
        var rows = Balance(text[(start + open.Length)..end]).Split('\n').Select(Flow).Where(Shows);
        var leadText = laidOut ? Lay(lead) : Flow(Balance(lead));
        return (leadText.Length > 0 && (laidOut || lead.EndsWith('\n')) ? leadText + "\n" : leadText) + open
               + string.Join("\n", rows) + text[end..];
    }

    private static string Flow(string text) => string.Join(' ', text.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries));

    /// <summary>A line as ReMixed lays it out: its line breaks and spacing kept, each line's end trimmed, no empty lines
    /// at its start or end.</summary>
    private static string Lay(string text) => string.Join('\n', Balance(text).Trim('\n').Split('\n').Select(l => l.TrimEnd()));

    /// <summary>Whether one of the events with slots (the tavern's) is running.</summary>
    internal static bool OwnEventRunning =>
        RemixedScenarios.RunningEvent != null && Events.Contains(*RemixedScenarios.RunningEvent);
    private static readonly HashSet<int> Events = [];

    /// <summary>A space the game keeps where it is (its text's &lt;nbsp&gt;).</summary>
    private const string Space = "<nbsp>";

    private const string ColourOpen = "<color=", ColourClose = "</color>";

    /// <summary>Whether a menu row shows anything (its colour tags aside).</summary>
    private static bool Shows(string row) =>
        System.Text.RegularExpressions.Regex.Replace(row, "</?color[^>]*>", "").Trim().Length > 0;

    /// <summary>ReMixed's colours are switches (a colour on until the next one, or 00 for the text's own; a slot may be
    /// only a colour): as the remaster's tags, paired up. A colour opened while another is on closes it first; one still
    /// on at a line's end closes there and opens again on the next line; a close with nothing open is dropped, as is a
    /// pair with nothing between.</summary>
    internal static string Balance(string text)
    {
        var out_ = new StringBuilder(text.Length + 16);
        string? on = null;
        for (var i = 0; i < text.Length;)
        {
            if (string.CompareOrdinal(text, i, ColourOpen, 0, ColourOpen.Length) == 0 && text.IndexOf('>', i) is var end and > 0)
            {
                if (on != null)
                    out_.Append(ColourClose);
                on = text[i..(end + 1)];
                out_.Append(on);
                i = end + 1;
            }
            else if (string.CompareOrdinal(text, i, ColourClose, 0, ColourClose.Length) == 0)
            {
                if (on != null)
                    out_.Append(ColourClose);
                on = null;
                i += ColourClose.Length;
            }
            else if (text[i] == '\n' && on != null)
            {
                out_.Append(ColourClose).Append('\n').Append(on);
                i++;
            }
            else
                out_.Append(text[i++]);
        }
        if (on != null)
            out_.Append(ColourClose);
        var balanced = out_.ToString();
        for (var at = balanced.IndexOf(ColourOpen, StringComparison.Ordinal); at >= 0;
             at = balanced.IndexOf(ColourOpen, at + 1, StringComparison.Ordinal))
        {
            var end = balanced.IndexOf('>', at);
            if (end > 0 && string.CompareOrdinal(balanced, end + 1, ColourClose, 0, ColourClose.Length) == 0)
            {
                balanced = balanced.Remove(at, end + 1 - at + ColourClose.Length);
                at = Math.Max(-1, at - 1);
            }
        }
        return balanced;
    }

    /// <summary>The filled-in line as UTF-8 in a buffer kept for its Id (the game reads it after the lookup returns).</summary>
    private static nint Keep(int id, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (!Buffers.TryGetValue(id, out var kept) || kept.Size < bytes.Length + 1)
        {
            if (kept.Buffer != 0)
                NativeMemory.Free((void*)kept.Buffer);
            var size = Math.Max(256, bytes.Length + 1);
            kept = ((nint)NativeMemory.AllocZeroed((nuint)size), size);
            Buffers[id] = kept;
        }
        var span = new Span<byte>((void*)kept.Buffer, kept.Size);
        bytes.CopyTo(span);
        span[bytes.Length] = 0;
        return kept.Buffer;
    }
}
