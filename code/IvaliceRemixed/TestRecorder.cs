using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IvaliceRemixed.Patches;

namespace IvaliceRemixed;

/// <summary>
/// The test mode (the "Test mode" setting): records which of the mod's mechanics fire, battle by battle, from the trace
/// lines they already write (a topic before the colon: "riding: …", "nemesis: …"), with any errors, and writes them to
/// TestResults.json and a report page, TestReport.html, in the mod's config folder after every battle. With
/// <see cref="Options.TestModeRule.RecordAndAutoBattle"/> the AI plays the player's units too, so a battle runs on its own.
/// </summary>
/// <remarks>
/// A battle starts at the battle setup (<see cref="BattleEvents"/>) and is won at the battle end's write-back
/// (<see cref="BattleEnd"/>); one that never gets there was lost, retried or quit, which the next battle (or closing the
/// game) records. What happens outside battles (the party menu's unlearning, dismissals, job changes) goes in a record of
/// its own for the session. The hand checks come from the to-do's Testing list (Data/test-checklist.json, written by the
/// build); the page keeps their ticks in the browser.
/// </remarks>
internal static class TestRecorder
{
    public const string ResultsFile = "TestResults.json", ReportFile = "TestReport.html", ChecklistFile = "test-checklist.json";
    private const int Examples = 6, KeptRuns = 60;

    private static readonly object Lock = new();
    private static string _folder = "";
    private static Log? _log;
    private static Game? _game;
    private static Results _results = new();
    private static Run? _battle;
    private static Run? _outside;
    private static List<string> _checklist = [];
    private static readonly HashSet<int> Handed = [];

    public static bool Enabled { get; private set; }
    public static Options.TestModeRule Mode { get; private set; }

    /// <summary>What a known topic is, and how to make it fire. Others show by their own name.</summary>
    internal static readonly Dictionary<string, (string Name, string How)> Catalogue = new()
    {
        ["formulas"] = ("ReMixed's formulas", "Any ability whose formula ReMixed changed (most attacks and spells)."),
        ["supports"] = ("Supports (Adeptness, Vehemence…)", "A unit with Adeptness or Vehemence attacks."),
        ["gains"] = ("EXP and JP rates", "Any action that gives EXP or JP."),
        ["rules"] = ("Battle rules (team-only, auras…)", "Attack an ally with an enemy-only ability, or stand next to an aura."),
        ["reactions"] = ("Reactions", "A unit with a reaction ability is hit."),
        ["brave"] = ("Brave and damage", "Attack a target with Brave under 50."),
        ["knockback"] = ("Knockback", "A strong physical hit knocks a unit back."),
        ["landing"] = ("Landing in lava or water", "A unit is knocked onto lava or into deep water."),
        ["death counter"] = ("The death counter", "A unit stays dead for three of its turns."),
        ["movement"] = ("Movement abilities (First Move, Move-Cleanse…)", "A unit with a ReMixed movement ability moves."),
        ["passives"] = ("Learned passives (Move-Find Item, 0x1D0)", "A unit with Move-Find Item or 0x1D0 joins a battle."),
        ["two spells"] = ("Two Spells", "A Calculator (0x1D0) casts a cheap charged spell, then another in the same turn."),
        ["dragon tech"] = ("Dragon Tech", "A Lancer who has learned Lancet jumps."),
        ["riding"] = ("Riding", "A unit rides a chocobo and uses Choco Cure from its menu."),
        ["gun skills"] = ("Gun skills and Charge", "A unit uses a gun skill or Charge."),
        ["charge lock"] = ("A charge locks the move", "A unit charges a weapon-range or area spell before moving."),
        ["cone area"] = ("Cone areas", "A Breath or Elemental Wave is aimed."),
        ["katana break"] = ("Draw Out's katana break", "A unit uses Draw Out."),
        ["banditry"] = ("Banditry", "A unit with Banditry steals or attacks from behind."),
        ["monster skill"] = ("Monster Skill", "A monster uses a Monster Skill ability."),
        ["learning"] = ("Learning from the secondary", "A unit is hit by an ability its secondary skillset can learn."),
        ["weapons"] = ("Weapon requirements", "A unit without the weapon an ability needs looks at its list."),
        ["level options"] = ("Level options", "Turn on Fixed or minimum levels or Enemy level boost and start a battle."),
        ["monster reactions"] = ("Monster reactions", "A battle with monsters."),
        ["ability validation"] = ("Enemy ability validation", "A battle with generic enemies."),
        ["random equipment"] = ("Random equipment", "A battle with generic enemies."),
        ["growth"] = ("Two-job stat growth", "A Bard or Dancer (or a unit with 0x1E5) and a secondary skillset levels up."),
        ["guest control"] = ("Guest control", "Set Guest control to Always and start a battle with a guest."),
        ["hitting bodies"] = ("Hitting bodies", "An archer aims Raise or a Phoenix Down at a body."),
        ["move-find"] = ("Move-Find", "A unit with Move-Find Item stops on a hidden-item panel."),
        ["taken equipment"] = ("Stolen equipment comes back", "An enemy Thief steals from your unit, and you win."),
        ["victory bonus"] = ("Bonus EXP & JP on Victory", "Turn the setting on and win a battle."),
        ["nemesis"] = ("The Nemesis System", "Leave a generic enemy downed and win; a few battles later it comes back."),
        ["unlearn"] = ("Unlearning for JP", "On the learn screen, choose a learned ability."),
        ["dismissal"] = ("Dismissing a monster", "Dismiss a monster with a Secret Hunt user in the party."),
        ["job change"] = ("Job change rules", "Change a unit's job in the party menu."),
        ["test mode"] = ("The test mode itself", "Start a battle with Test mode on."),
    };

    /// <summary>Turn the recorder on (from <see cref="Startup"/>, before the patches), reading earlier results.</summary>
    public static void Start(string folder, string checklist, Log log, Options.TestModeRule mode)
    {
        _folder = folder;
        _log = log;
        Mode = mode;
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, ResultsFile);
            if (File.Exists(path))
                _results = JsonSerializer.Deserialize<Results>(File.ReadAllText(path), Json) ?? new();
        }
        catch (Exception e)
        {
            log.Warn($"test mode: earlier results can't be read ({e.Message}); starting new ones");
            _results = new();
        }
        try
        {
            if (File.Exists(checklist))
                _checklist = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(checklist)) ?? [];
        }
        catch (Exception e)
        {
            log.Warn($"test mode: the hand checks can't be read ({e.Message})");
        }
        _results.Sessions++;
        _outside = new Run { Kind = "outside", Started = DateTime.Now };
        Enabled = true;
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Finish("the game was closed during the battle");
            Write(); // what happened in the menus since the last battle
        };
        log.Info($"test mode: {mode}; results go to {Path.Combine(folder, ReportFile)}");
    }

    /// <summary>The battle hooks (from <see cref="Startup"/>, after the patches).</summary>
    public static void Install(Game game)
    {
        if (!Enabled)
            return;
        _game = game;
        BattleEvents.OnStart(game, "the test recorder", BattleStarted);
        BattleEnd.OnAfter(game, "the test recorder", BattleWon);
        try
        {
            Patches.EventLog.Install(game); // for the story port: which event scripts load
        }
        catch (Exception e)
        {
            game.Log.Warn($"test mode: the event script log isn't installed ({e.Message})");
        }
        Write();
    }

    /// <summary>The rules installed this session, for the report's header.</summary>
    public static void Installed(IEnumerable<string> rules)
    {
        if (Enabled)
            lock (Lock)
                _results.Installed = rules.ToList();
    }

    /// <summary>A trace line (from <see cref="Tracer"/>, before its dedupe).</summary>
    public static void Note(string line)
    {
        if (!Enabled)
            return;
        var topic = Topic(line);
        lock (Lock)
        {
            var run = _battle ?? _outside!;
            if (!run.Topics.TryGetValue(topic, out var seen))
                run.Topics[topic] = seen = new Seen();
            seen.Count++;
            if (seen.Examples.Count < Examples && !seen.Examples.Contains(line))
                seen.Examples.Add(line);
        }
    }

    /// <summary>An error line from the mod's log.</summary>
    public static void Error(string text)
    {
        if (!Enabled)
            return;
        lock (Lock)
        {
            var run = _battle ?? _outside!;
            if (run.Errors.Count < 20)
                run.Errors.Add(text);
        }
    }

    /// <summary>The topic of a trace line: the words before its first colon ("formula 0x…" lines are "formulas").</summary>
    internal static string Topic(string line)
    {
        if (line.StartsWith("formula ", StringComparison.Ordinal))
            return "formulas";
        var colon = line.IndexOf(':');
        return (colon > 0 ? line[..colon] : line).Trim().ToLowerInvariant();
    }

    internal static void BattleStarted()
    {
        Patches.EventLog.Drain("before this battle");
        Finish("it didn't reach the battle end (lost, retried or quit)");
        lock (Lock)
        {
            Handed.Clear();
            _battle = new Run { Kind = "battle", Started = DateTime.Now, Map = Map() };
        }
        Note($"test mode: a battle starts{(Mode == Options.TestModeRule.RecordAndAutoBattle ? "; the AI plays your units" : "")}");
    }

    internal static void BattleWon()
    {
        Patches.EventLog.Drain("in this battle");
        Finish("won");
    }

    /// <summary>The current battle ends with <paramref name="outcome"/>, and the results are written.</summary>
    internal static void Finish(string outcome)
    {
        if (!Enabled)
            return;
        lock (Lock)
        {
            if (_battle == null)
                return;
            _battle.Ended = DateTime.Now;
            _battle.Outcome = outcome;
            _results.Runs.Add(_battle);
            if (_results.Runs.Count > KeptRuns)
                _results.Runs.RemoveRange(0, _results.Runs.Count - KeptRuns);
            _battle = null;
        }
        Write();
    }

    /// <summary>
    /// With the AI playing the player's units: every unit of the player's team (+5 &amp; 0x30 clear) loses its control
    /// bit, 0x08 in its current team (+5) and in its base team (+0x1EE): the remaster keeps it in both (seen with Invite,
    /// <see cref="Patches.InviteControl"/>), and clearing +5 alone left the units to the player (seen on the PC).
    /// Called from the turn loop's countdown, which runs before the first turn.
    /// </summary>
    internal static unsafe void AutoBattle(byte* units)
    {
        if (Enabled)
        {
            Patches.EventLog.Drain("up to this turn"); // so a battle needn't be finished to see them
            Patches.EventLog.CheckMarker();
        }
        if (!Enabled || Mode != Options.TestModeRule.RecordAndAutoBattle || units == null)
            return;
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var unit = units + k * Battle.UnitSize;
            if (unit[1] == 0xFF || (unit[5] & 0x30) != 0 || ((unit[5] | unit[Battle.UnitTeam]) & 0x08) == 0)
                continue;
            unit[5] &= unchecked((byte)~0x08);
            unit[Battle.UnitTeam] &= unchecked((byte)~0x08);
            if (Handed.Add(k))
                Note($"test mode: unit {k} (job 0x{unit[3]:X2}) is the AI's to play");
        }
    }

    private static unsafe int Map()
    {
        try
        {
            if (MoveFind.Variable != null)
                return MoveFind.Variable(MoveFind.MapVar);
        }
        catch (Exception)
        {
        }
        return -1;
    }

    /// <summary>TestResults.json and TestReport.html, from the runs so far (and this session's menus).</summary>
    internal static void Write()
    {
        try
        {
            string json, html;
            lock (Lock)
            {
                _results.Outside = _outside;
                _results.Written = DateTime.Now;
                json = JsonSerializer.Serialize(_results, Json);
                html = Report(_results, _checklist);
            }
            File.WriteAllText(Path.Combine(_folder, ResultsFile), json);
            File.WriteAllText(Path.Combine(_folder, ReportFile), html);
        }
        catch (Exception e)
        {
            _log?.Warn($"test mode: the results can't be written ({e.Message})");
        }
    }

    /// <summary>Forget the results (for the offline check).</summary>
    internal static void Reset(string folder, Options.TestModeRule mode, List<string> checklist)
    {
        _folder = folder;
        Mode = mode;
        _results = new Results { Sessions = 1 };
        _battle = null;
        _outside = new Run { Kind = "outside", Started = DateTime.Now };
        _checklist = checklist;
        Handed.Clear();
        Enabled = true;
    }

    internal static void Stop() => Enabled = false;

    internal static Results Current => _results;

    // ---------------------------------------------------------------- the report

    internal static string Report(Results results, List<string> checklist)
    {
        var battles = results.Runs.Where(r => r.Kind == "battle").ToList();
        var all = new List<Run>(battles);
        if (results.Outside is { } outside && (outside.Topics.Count > 0 || outside.Errors.Count > 0))
            all.Add(outside);
        var topics = all.SelectMany(r => r.Topics.Keys).Distinct().OrderBy(t => Name(t)).ToList();
        var html = new StringBuilder();
        html.Append("""
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Ivalice ReMixed tests</title><style>
:root{--bg:#f7f7f5;--card:#fff;--text:#1d1d1b;--muted:#6b6b66;--line:#e2e1dc;--good:#1f7a3f;--bad:#b3261e;--warn:#9a6700;--chip:#efeee9}
@media (prefers-color-scheme:dark){:root{--bg:#161615;--card:#1f1f1d;--text:#ecebe6;--muted:#a3a29b;--line:#34332f;--good:#5cc184;--bad:#f2867c;--warn:#e3b341;--chip:#2a2a27}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif}
main{max-width:980px;margin:0 auto;padding:24px 16px 64px}h1{font-size:24px;margin:0 0 4px}h2{font-size:18px;margin:32px 0 8px}
.muted{color:var(--muted)}.card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:14px 16px;margin:10px 0}
table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:7px 8px;border-bottom:1px solid var(--line);vertical-align:top}
th{font-weight:600;color:var(--muted);font-size:13px}.num{text-align:right;white-space:nowrap}.ok{color:var(--good)}.bad{color:var(--bad)}.warn{color:var(--warn)}
code,.ex{font:12.5px/1.45 ui-monospace,Consolas,monospace}.ex{color:var(--muted);display:block;white-space:pre-wrap;overflow-wrap:anywhere}
.chip{display:inline-block;background:var(--chip);border-radius:999px;padding:1px 9px;margin:2px 4px 2px 0;font-size:12.5px}
details summary{cursor:pointer}label{display:flex;gap:10px;align-items:flex-start;padding:6px 0;border-bottom:1px solid var(--line)}
input[type=checkbox]{margin-top:4px;flex:none}.wrap{overflow-x:auto}
</style></head><body><main>
""");
        html.Append("<h1>Ivalice ReMixed: test results</h1>");
        var won = battles.Count(b => b.Outcome == "won");
        var errors = all.Sum(r => r.Errors.Count);
        html.Append($"<p class=\"muted\">Written {E(results.Written.ToString("yyyy-MM-dd HH:mm"))} · {battles.Count} battle{(battles.Count == 1 ? "" : "s")} recorded ({won} won) · {results.Sessions} game session{(results.Sessions == 1 ? "" : "s")} · {results.Installed.Count} rules installed · ");
        html.Append(errors == 0 ? "<span class=\"ok\">no errors</span>" : $"<span class=\"bad\">{errors} error{(errors == 1 ? "" : "s")}</span>");
        html.Append("</p><p class=\"muted\">Play another battle to add to these results; delete TestResults.json in this folder to start over.</p>");

        if (errors > 0)
        {
            html.Append("<h2 class=\"bad\">Errors</h2><div class=\"card\">");
            foreach (var r in all.Where(r => r.Errors.Count > 0))
                foreach (var e in r.Errors)
                    html.Append($"<span class=\"ex\">{E(Title(r))}: {E(e)}</span>");
            html.Append("</div>");
        }

        html.Append("<h2>Mechanics seen</h2><div class=\"card wrap\"><table><tr><th>Mechanic</th><th class=\"num\">Battles</th><th class=\"num\">Times</th><th>Examples</th></tr>");
        foreach (var t in topics)
        {
            var inBattles = battles.Count(b => b.Topics.ContainsKey(t));
            var times = all.Sum(r => r.Topics.TryGetValue(t, out var s) ? s.Count : 0);
            var examples = all.SelectMany(r => r.Topics.TryGetValue(t, out var s) ? s.Examples : new List<string>()).Distinct().Take(3);
            html.Append($"<tr><td><b>{E(Name(t))}</b><br><span class=\"muted\">{E(t)}</span></td><td class=\"num\">{inBattles}</td><td class=\"num\">{times}</td><td>");
            foreach (var x in examples)
                html.Append($"<span class=\"ex\">{E(x)}</span>");
            html.Append("</td></tr>");
        }
        if (topics.Count == 0)
            html.Append("<tr><td colspan=\"4\" class=\"muted\">Nothing yet: start a battle with Test mode on.</td></tr>");
        html.Append("</table></div>");

        var unseen = Catalogue.Keys.Where(k => !topics.Contains(k)).OrderBy(k => Catalogue[k].Name).ToList();
        if (unseen.Count > 0)
        {
            html.Append($"<h2>Not seen yet <span class=\"muted\">({unseen.Count})</span></h2><div class=\"card wrap\"><table><tr><th>Mechanic</th><th>How to make it happen</th></tr>");
            foreach (var k in unseen)
                html.Append($"<tr><td>{E(Catalogue[k].Name)}</td><td class=\"muted\">{E(Catalogue[k].How)}</td></tr>");
            html.Append("</table></div>");
        }

        html.Append("<h2>Battles</h2>");
        foreach (var b in Enumerable.Reverse(all))
        {
            var cls = b.Kind == "outside" ? "muted" : b.Outcome == "won" ? "ok" : "warn";
            html.Append($"<details class=\"card\"{(b == all.LastOrDefault() ? " open" : "")}><summary><b>{E(Title(b))}</b> <span class=\"{cls}\">{E(b.Kind == "outside" ? "menus this session" : b.Outcome ?? "")}</span>");
            html.Append($" <span class=\"muted\">· {b.Topics.Count} mechanic{(b.Topics.Count == 1 ? "" : "s")}{(b.Errors.Count > 0 ? $" · {b.Errors.Count} error{(b.Errors.Count == 1 ? "" : "s")}" : "")}</span></summary><p>");
            foreach (var (t, s) in b.Topics.OrderByDescending(p => p.Value.Count))
                html.Append($"<span class=\"chip\">{E(Name(t))} × {s.Count}</span>");
            html.Append("</p>");
            foreach (var (t, s) in b.Topics.OrderBy(p => Name(p.Key)))
                foreach (var x in s.Examples)
                    html.Append($"<span class=\"ex\">{E(x)}</span>");
            html.Append("</details>");
        }
        if (all.Count == 0)
            html.Append("<p class=\"muted\">No battles yet.</p>");

        if (checklist.Count > 0)
        {
            html.Append("<h2>Hand checks</h2><p class=\"muted\">What a recording can't tell (menus, what's shown). Ticks are kept in this browser.</p><div class=\"card\">");
            for (var k = 0; k < checklist.Count; k++)
                html.Append($"<label><input type=\"checkbox\" data-key=\"{E(Key(checklist[k]))}\"><span>{E(checklist[k])}</span></label>");
            html.Append("</div>");
        }
        html.Append("""
<script>
document.querySelectorAll('input[data-key]').forEach(function (box) {
  var key = 'ivalice-remixed-check:' + box.dataset.key;
  try { box.checked = localStorage.getItem(key) === '1'; } catch (e) {}
  box.addEventListener('change', function () { try { localStorage.setItem(key, box.checked ? '1' : '0'); } catch (e) {} });
});
</script></main></body></html>
""");
        return html.ToString();
    }

    private static string Name(string topic) => Catalogue.TryGetValue(topic, out var c) ? c.Name : topic;

    private static string Title(Run r) => r.Kind == "outside"
        ? $"Outside battles, {r.Started:yyyy-MM-dd HH:mm}"
        : $"Battle {r.Started:yyyy-MM-dd HH:mm}{(r.Map >= 0 ? $", map {r.Map}" : "")}{(r.Ended is { } end ? $", {(int)(end - r.Started).TotalMinutes} min" : "")}";

    private static string Key(string text) => Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(text)))[..12];

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    internal sealed class Results
    {
        public int Sessions { get; set; }
        public DateTime Written { get; set; }
        public List<string> Installed { get; set; } = [];
        public List<Run> Runs { get; set; } = [];
        public Run? Outside { get; set; }
    }

    internal sealed class Run
    {
        public string Kind { get; set; } = "battle";
        public DateTime Started { get; set; }
        public DateTime? Ended { get; set; }
        public string? Outcome { get; set; }
        public int Map { get; set; } = -1;
        public Dictionary<string, Seen> Topics { get; set; } = [];
        public List<string> Errors { get; set; } = [];
    }

    internal sealed class Seen
    {
        public int Count { get; set; }
        public List<string> Examples { get; set; } = [];
    }
}
