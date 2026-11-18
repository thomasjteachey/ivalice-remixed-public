#if DEVELOPER_MENU
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reloaded.Mod.Interfaces;

namespace IvaliceRemixed;

/// <summary>Private one-shot actions shown as a second Reloaded-II Configure Mod page.</summary>
public sealed class DeveloperOptions : IConfigurable
{
    [Category("DEV — story"), DisplayName("Story progress")]
    [Description("Set event variable 0x6E on the next world-map frame. -1 leaves it alone.")]
    [DefaultValue(-1)]
    public int StoryProgress { get; set; } = -1;

    [Category("DEV — story"), DisplayName("Event variable or flag")]
    [Description("Event variable/flag id to set (0 through 0x3FF). -1 leaves it alone.")]
    [DefaultValue(-1)]
    public int Variable { get; set; } = -1;

    [Category("DEV — story"), DisplayName("Variable value")]
    [Description("Value written to the event variable/flag above. Use 0 or 1 for a flag.")]
    [DefaultValue(0)]
    public int VariableValue { get; set; }

    [Category("DEV — travel"), DisplayName("World-map location")]
    [Description("Warp the party to this location on the next world-map frame. -1 leaves it alone.")]
    [DefaultValue(-1)]
    public int Location { get; set; } = -1;

    [Category("DEV — travel"), DisplayName("Scenario")]
    [Description("Start this scenario after the other DEV actions on the next world-map frame. -1 starts none.")]
    [DefaultValue(-1)]
    public int Scenario { get; set; } = -1;

    [JsonIgnore, Browsable(false)]
    public string ConfigName => "DEV ONLY — one-shot test actions";
    [JsonIgnore, Browsable(false)]
    public string? FilePath { get; set; }
    [JsonIgnore, Browsable(false)]
    public Action Save => Write;
    internal bool Any => StoryProgress >= 0 || Variable >= 0 || Location >= 0 || Scenario >= 0;

    internal static DeveloperOptions Read(string path)
    {
        DeveloperOptions result;
        try
        {
            result = File.Exists(path) ? JsonSerializer.Deserialize<DeveloperOptions>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            result = new();
        }
        result.FilePath = path;
        return result;
    }

    internal void Write()
    {
        if (FilePath == null)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
}

/// <summary>
/// Compiled only when EnableDeveloperMenu=true, and gated again by a DEV-MENU marker beside the installed DLL.
/// Actions wait for the first usable world-map frame and reset immediately after applying.
/// </summary>
internal static unsafe class DeveloperMenu
{
    internal const string MarkerFile = "DEV-MENU", ConfigFile = "Developer.json";
    private const int StoryProgress = 0x6E;
    private static DeveloperOptions? _pending;
    private static Log _log = null!;

    internal static bool Enabled(string? modDirectory) => modDirectory != null && File.Exists(Path.Combine(modDirectory, MarkerFile));
    internal static DeveloperOptions Configuration(string configDirectory) => DeveloperOptions.Read(Path.Combine(configDirectory, ConfigFile));

    internal static void Install(Log log, string modDirectory, string configDirectory)
    {
        if (!Enabled(modDirectory))
            return;
        _log = log;
        _pending = Configuration(configDirectory);
        log.Warn($"DEV ONLY: developer actions enabled by {Path.Combine(modDirectory, MarkerFile)}"
                 + (_pending.Any ? "; queued for the next world-map frame" : "; no action queued"));
    }

    /// <summary>Called from the world-map frame on the game thread. Returns a scenario to start, or -1.</summary>
    internal static int ApplyPendingWorldMap(int* partyLocation)
    {
        var pending = Interlocked.Exchange(ref _pending, null);
        if (pending == null || !pending.Any)
            return -1;
        var changed = new List<string>();
        if (pending.StoryProgress >= 0)
        {
            Patches.Nemesis.SetVariable(StoryProgress, pending.StoryProgress, 0);
            changed.Add($"story 0x{pending.StoryProgress:X}");
        }
        if (pending.Variable >= 0)
        {
            if (pending.Variable > 0x3FF)
                _log.Error($"DEV ONLY: variable 0x{pending.Variable:X} is outside 0..0x3FF; ignored");
            else
            {
                Patches.Nemesis.SetVariable(pending.Variable, pending.VariableValue, 0);
                changed.Add($"var 0x{pending.Variable:X}=0x{pending.VariableValue:X}");
            }
        }
        if (pending.Location >= 0)
        {
            *partyLocation = pending.Location;
            changed.Add($"location {pending.Location}");
        }
        var scenario = pending.Scenario;
        if (scenario >= 0)
            changed.Add($"scenario 0x{scenario:X}");
        var path = pending.FilePath;
        new DeveloperOptions { FilePath = path }.Write();
        _log.Warn($"DEV ONLY: applied {string.Join(", ", changed)}; {ConfigFile} reset");
        return scenario;
    }
}
#endif
