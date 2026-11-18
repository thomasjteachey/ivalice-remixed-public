using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Brave rule for attacks (docs/MECHANICS.md: "Brave lowers damage taken"; PS1 0x80186254,
/// rewritten in place). Installed by <see cref="Rules"/> with "brave-damage".
/// </summary>
/// <remarks>
/// The attacker's status step of the attack modifiers (the remaster's 0x305D08, from nine formula
/// paths) makes XA half again with Berserk and 1 with Frog. ReMixed first takes (50 − Brave)% off XA
/// when the target's Brave is under 50, so a Brave 30 target takes a fifth less, and Brave 0 half.
/// Its code skips that with flag 0xDF, which no event sets, so it always applies.
/// </remarks>
internal static unsafe class BraveDamage
{
    private static Game _game = null!;
    private static delegate* unmanaged<void> _attackerStatus; // the original
    private static int _failed;

    private const int Threshold = 50;

    // For the offline check.
    internal static nint StepAt { get; private set; }

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // The attacker's +0x63: & 8 Berserk, XA × 3 / 2; & 2 Frog, XA = 1.
        var step = image.FindUnique(
            "48 8B 0D ?? ?? ?? ?? 8A 41 63 A8 08 74 ?? 0F BF 05 ?? ?? ?? ?? 8D 04 40 99 2B C2 D1 F8 66 89 05 ?? ?? ?? ?? " +
            "8A 41 63 A8 02 74 ?? B8 01 00 00 00 66 89 05 ?? ?? ?? ?? C3");
        var battle = game.Battle;
        if ((byte**)GameImage.Rip(step, 3, 7) != battle.Attacker || (short*)GameImage.Rip(step + 14, 3, 7) != battle.XA
            || (short*)GameImage.Rip(step + 29, 3, 7) != battle.XA)
            throw new InvalidDataException("the attacker's status step isn't the code this DLL expects");
        _ = game.Routines.Target;
        StepAt = step;
        // Its callers keep nothing across it (liveregs).
        _attackerStatus = (delegate* unmanaged<void>)game.Hooks.Hook(
            step, (nint)(delegate* unmanaged<void>)&AttackerStatus, "the attacker's status step");
    }

    [UnmanagedCallersOnly]
    private static void AttackerStatus()
    {
        try
        {
            var target = *_game.Routines.Target;
            if (target != null && Options.Current.Bravery == Options.BraveryRule.LowersWithBrave) // a save's own option
            {
                var xa = _game.Battle.XA;
                var before = *xa;
                var after = Reduced(before, target[Battle.UnitBrave]);
                if (after != before)
                {
                    *xa = (short)after;
                    Tracer.Line(_game.Log, $"brave: a Brave {target[Battle.UnitBrave]} target, XA {before} becomes {after}");
                }
            }
        }
        catch (Exception e)
        {
            Failed("the Brave rule", e);
        }
        _attackerStatus();
    }

    /// <summary>XA less (50 − Brave)% of itself for a target with Brave under 50, rounding as ReMixed does.</summary>
    internal static int Reduced(int xa, int brave) => brave < Threshold ? xa - xa * (Threshold - brave) / 100 : xa;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"brave: {what} failed: {e}");
    }
}
