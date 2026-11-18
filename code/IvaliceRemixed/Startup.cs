using System.Drawing;
using EventInstructionUpgrade.Interfaces;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace IvaliceRemixed;

/// <summary>Reloaded-II entry point. Applies the patches once, at startup, before the game runs.</summary>
public class Startup : IMod
{
    private const string Prefix = "[Ivalice ReMixed] ";
    private IHooks? _hooks;

    public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)
    {
        var loader = (IModLoader)loaderApi;
        var logger = (ILogger)loader.GetLogger();
        var log = new Log(
            text => logger.WriteLine(Prefix + text),
            text => logger.WriteLine(Prefix + text, Color.Orange),
            text =>
            {
                logger.WriteLine(Prefix + text, Color.Red);
                TestRecorder.Error(text);
            });
        try
        {
            _hooks = Hooks.From(loader);
            Tracer.Sink = TestRecorder.Note;
            // The Event Instruction Upgrade (a mod ReMixed depends on, loaded first) runs ReMixed's events.
            IEventInstructions? events = null;
            loader.GetController<IEventInstructions>()?.TryGetTarget(out events);
            Patches.RemixedEvents.Api = events;
            if (events == null)
                log.Error("the Event Instruction Upgrade mod isn't loaded; ReMixed's events won't run");
            var modDir = loader.GetDirectoryForModId(modConfig.ModId);
            var optionsPath = Path.Combine(loader.GetModConfigDirectory(modConfig.ModId), Options.FileName);
            Options.Current = Options.Read(optionsPath, out var problem);
            Options.Settings = Options.Current.Copy();
            if (problem != null)
                log.Warn($"options: {optionsPath} can't be read ({problem}); using ReMixed's defaults");
            log.Info($"options: {Options.Current}");
            if (Options.Current.TestMode != Options.TestModeRule.Off)
                TestRecorder.Start(loader.GetModConfigDirectory(modConfig.ModId),
                                   Path.Combine(modDir, "Data", TestRecorder.ChecklistFile), log, Options.Current.TestMode);
            var game = new Game(GameImage.Main(), log, _hooks);
            Patcher.Run(game, Path.Combine(modDir, "Data", "patches.json"));
            try
            {
                Patches.SaveOptions.Install(game);
                log.Info("options: each save keeps its own, from the mod's settings the first time");
            }
            catch (Exception e)
            {
                log.Error($"options: each save's own can't be followed ({e.Message}); the mod's settings stay in effect");
            }
            var page = Path.Combine(modDir, Patches.OptionsPage.FileName);
            if (!File.Exists(page))
                log.Info($"options page: no {Patches.OptionsPage.FileName} in the mod's folder; no ReMixed tab in Options");
            else
                try
                {
                    Patches.OptionsPage.Install(game, page);
                }
                catch (Exception e)
                {
                    log.Error($"options page: no ReMixed tab in Options ({e.Message})");
                }
            if (File.Exists(page))
                try
                {
                    Patches.RevealTreasures.Install(game, page);
                }
                catch (Exception e)
                {
                    log.Error($"reveal treasures: not offered ({e.Message})");
                }
            var slots = Path.Combine(modDir, Patches.RemixedText.FileName);
            if (File.Exists(slots))
                try
                {
                    Patches.RemixedText.Install(game, slots);
                }
                catch (Exception e)
                {
                    log.Error($"remixed text: lines with slots show without them ({e.Message})");
                }
            try
            {
                Patches.TallMenus.Install(game);
            }
            catch (Exception e)
            {
                log.Error($"tall menus: menus past three rows show past their box ({e.Message})");
            }
            try
            {
                Patches.FastForwardToggle.Install(game);
            }
            catch (Exception e)
            {
                log.Error($"fast-forward: no toggle, held as the game has it ({e.Message})");
            }
            TestRecorder.Install(game);
#if DEVELOPER_MENU
            // Private builds only: the explicit MSBuild property supplies this symbol, and a marker gates it again.
            DeveloperMenu.Install(log, modDir, Path.GetDirectoryName(optionsPath)!);
#endif
        }
        catch (Exception e)
        {
            log.Error($"stopped: {e}");
        }
    }

    // The patches change the game's own data and code, so the mod can't be suspended or unloaded.
    public void Suspend() { }
    public void Resume() { }
    public void Unload() { }
    public bool CanUnload() => false;
    public bool CanSuspend() => false;
    public Action Disposing => () => { };
}
