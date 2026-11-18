using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Guest Control (docs/MECHANICS.md: "Guest Control"; its Preparation Menu, event 0x192): guests on the
/// player's team are the player's to control. Installed by <see cref="Rules"/> with "guest-control", for the Guest
/// control setting's Always.
/// </summary>
/// <remarks>
/// ReMixed's event sets the control bit (+0x05 | 8) on every player-team unit (+0x05 & 0x30 zero) in the player's
/// deployment slots from 16, the guests deployed by hand, and with flag 0xDE in every slot from 0, so the story's
/// guests too. The remaster deploys no guests by hand, so only Always does anything: a hook on the unit setup from
/// battle data (0x27612C, which copies the record's +0x18 into +0x05 and +0x1EE; one caller, which keeps only the
/// result) sets the bit for a player-team unit after the game has built it.
/// </remarks>
internal static unsafe class GuestControl
{
    public const int Team = 0x05, TeamBits = 0x30, ControlBit = 0x08;

    private static Game _game = null!;
    private static int _failed;

    /// <summary>The game's unit setup (0x27612C's original). The offline check swaps it.</summary>
    internal static delegate* unmanaged<byte*, byte*, int> GameSetup;
    internal static nint SetupAt { get; private set; }

    public static void Install(Game game)
    {
        _game = game;
        var setup = game.Image.FindUnique(
            "48 89 5C 24 18 48 89 4C 24 08 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 20 8A 42 17 4C 8D 05 ?? ?? ?? ?? " +
            "88 41 04 33 ED 8A 42 18 48 8B DA 88 41 05");
        GameSetup = (delegate* unmanaged<byte*, byte*, int>)game.Hooks.Hook(
            setup, (nint)(delegate* unmanaged<byte*, byte*, int>)&Setup, "the unit setup from battle data");
        SetupAt = setup;
    }

    [UnmanagedCallersOnly]
    private static int Setup(byte* unit, byte* record)
    {
        var result = GameSetup(unit, record);
        try
        {
            if (Options.Current.GuestControl == Options.GuestControlRule.Always) // a save's own option
                Control(unit);
        }
        catch (Exception e)
        {
            Failed(e);
        }
        return result;
    }

    /// <summary>A player-team unit the player doesn't control yet becomes the player's.</summary>
    internal static bool Control(byte* unit)
    {
        if (unit == null || (unit[Team] & (TeamBits | ControlBit)) != 0)
            return false;
        unit[Team] |= ControlBit;
        Tracer.Line(_game.Log, $"guest control: a guest (job 0x{unit[3]:X2}) is the player's to control");
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"guest control: failed: {e}");
    }
}
