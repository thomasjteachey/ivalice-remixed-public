using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// One hook on the battle setup's turn reset (<see cref="TurnCode.BattleStart"/>, called once
/// per battle before the unit slots are cleared), shared by the patches that need a new
/// battle: they register what to do, and it runs before the game's own reset.
/// </summary>
internal static unsafe class BattleEvents
{
    private static readonly List<(string Name, Action Run)> Starting = [];
    private static delegate* unmanaged<void> _battleStart; // the original
    private static Log _log = null!;

    /// <summary>Run <paramref name="run"/> at the start of every battle.</summary>
    public static void OnStart(Game game, string name, Action run)
    {
        if (_battleStart == null)
        {
            _log = game.Log;
            _battleStart = (delegate* unmanaged<void>)game.Hooks.Hook(
                (nint)game.TurnCode.BattleStart, (nint)(delegate* unmanaged<void>)&BattleStart, "the battle setup's turn reset");
        }
        Starting.Add((name, run));
    }

    [UnmanagedCallersOnly]
    private static void BattleStart()
    {
        foreach (var (name, run) in Starting)
        {
            try
            {
                run();
            }
            catch (Exception e)
            {
                _log.Error($"battle start: {name} failed: {e}");
            }
        }
        _battleStart();
    }
}
