using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// One hook on the battle end's write-back of the units to the roster (0x2C60E4, called first by 0x2C8818, game mode 7
/// of 0x2CB9E8), shared by the patches that act after a battle: what they register runs before or after the game's
/// write-back, once per battle (a battle start arms it; the write-back uses it up).
/// </summary>
/// <remarks>
/// The write-back also recruits Invited units and monsters and handles units lost for good, so it belongs to a won
/// battle; a lost one ends in a game over. That isn't checked in the game yet; arming it at the battle start keeps a
/// write-back that no battle preceded from running anything.
/// </remarks>
internal static unsafe class BattleEnd
{
    private static readonly List<(string Name, Action Run)> Before = [], After = [];
    private static delegate* unmanaged<int> _writeBack; // the original
    private static Log _log = null!;

    /// <summary>Whether a battle has started since the last write-back (the offline check sets it).</summary>
    internal static bool Armed { get; set; }

    /// <summary>The write-back's entry, for the offline check.</summary>
    internal static nint WriteBackAt { get; private set; }

    /// <summary>Install the write-back hook even when no optional after-battle rule is enabled.</summary>
    internal static void Install(Game game) => Hook(game);

    /// <summary>
    /// Write the completed battle into the roster without entering display state 7. ReMixed's post-battle returns
    /// continue their event on the live battlefield, while the remaster's display state also tears that map down.
    /// </summary>
    internal static int WriteBackNow()
    {
        if (_writeBack == null)
            throw new InvalidOperationException("the battle-end write-back isn't installed");
        var armed = Armed;
        Armed = false;
        if (armed)
            Run(Before, "before");
        var result = _writeBack();
        if (armed)
            Run(After, "after");
        return result;
    }

    /// <summary>Run <paramref name="run"/> before the write-back of a battle's units.</summary>
    public static void OnBefore(Game game, string name, Action run)
    {
        Hook(game);
        Before.Add((name, run));
    }

    /// <summary>Run <paramref name="run"/> after the write-back of a battle's units.</summary>
    public static void OnAfter(Game game, string name, Action run)
    {
        Hook(game);
        After.Add((name, run));
    }

    private static void Hook(Game game)
    {
        if (_writeBack != null)
            return;
        _log = game.Log;
        var writeBack = game.Image.FindUnique(
            "48 89 5C 24 18 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 20 33 F6 4C 8D 3D ?? ?? ?? ?? 89 35 ?? ?? ?? ?? " +
            "8B EE 89 74 24 68 41 B9 FF 00 00 00");
        WriteBackAt = writeBack;
        _writeBack = (delegate* unmanaged<int>)game.Hooks.Hook(
            writeBack, (nint)(delegate* unmanaged<int>)&WriteBack, "the battle end's write-back to the roster");
        BattleEvents.OnStart(game, "the battle end", () => Armed = true);
    }

    [UnmanagedCallersOnly]
    private static int WriteBack() => WriteBackNow();

    private static void Run(List<(string Name, Action Run)> steps, string when)
    {
        foreach (var (name, run) in steps)
        {
            try
            {
                run();
            }
            catch (Exception e)
            {
                _log.Error($"battle end ({when} the write-back): {name} failed: {e}");
            }
        }
    }
}
