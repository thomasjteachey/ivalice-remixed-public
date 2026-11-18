using System.Text.Json;
using IvaliceRemixed.Patches;

namespace IvaliceRemixed;

/// <summary>
/// Applies the patches listed in Data/patches.json, which tools/build_mod.py
/// generates from ReMixed's data. Each game build (exe name) has its own section,
/// mapping a patch name to its data.
/// </summary>
/// <remarks>
/// For what the FFT mod loader can't do. Anything it has a table for goes
/// through the loader instead: it merges changes from several mods and keeps
/// the original values, which a write from here, done before the loader reads
/// them, would replace.
/// </remarks>
internal static class Patcher
{
    /// <summary>Patch name -> what applies it.</summary>
    private static readonly Dictionary<string, Action<Game, JsonElement>> Patches = new()
    {
        ["formulas"] = Formulas.Install,
        ["supports"] = Supports.Install,
        ["rules"] = Rules.Install, // after "formulas": its formula stubs call ReMixed's formulas
    };

    /// <summary>Apply the patches in <paramref name="path"/> (Data/patches.json) to the game.</summary>
    public static void Run(Game game, string path)
    {
        var image = game.Image;
        var log = game.Log;
        if (image.Delta == 0)  // the game, not the offline check's mapped copy
        {
            try
            {
                CrashLog.Install(game);
            }
            catch (Exception e)
            {
                log.Error($"crash log: not installed: {e.Message}");
            }
            try
            {
                Autopilot.Install(game);
            }
            catch (Exception e)
            {
                log.Error($"autopilot: not installed: {e.Message}");
            }
        }
        if (!File.Exists(path))
        {
            log.Warn($"no {path}; nothing to patch");
            return;
        }
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        if (!doc.RootElement.GetProperty("builds").TryGetProperty(image.ExeName, out var build))
        {
            log.Info($"no code patches for {image.ExeName} yet");
            return;
        }
        foreach (var patch in build.EnumerateObject())
        {
            if (!Patches.TryGetValue(patch.Name, out var apply))
            {
                log.Warn($"{patch.Name}: this DLL doesn't know that patch; rebuild the mod");
                continue;
            }
            try
            {
                apply(game, patch.Value);
            }
            catch (Exception e)
            {
                log.Error($"{patch.Name}: not applied: {e.Message}{(e.InnerException is { } inner ? $" ({inner.Message})" : "")}");
            }
        }
    }
}
