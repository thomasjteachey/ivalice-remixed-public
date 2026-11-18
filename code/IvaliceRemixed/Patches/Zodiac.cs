using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's zodiac compatibility (docs/MECHANICS.md: "Zodiac compatibility by gender"; PS1 0x80184964,
/// rewritten in place). Installed by <see cref="Rules"/> with "zodiac".
/// </summary>
/// <remarks>
/// Opposite signs are Best or Worst by gender on the PS1, but the remaster dropped the gender test, so
/// any two humans are Best there (ReMixed calls that TIC mode). ReMixed's default, Straight, is the PS1's
/// rule. This replaces the remaster's damage step (0x305178, XA) and its compatibility for the screen
/// (0x305264, into 0x186AFFC) with ReMixed's: the Sexuality setting picks the rule and the Zodiac
/// Compatibility setting scales the 25%. As in ReMixed, the step reads XA's low byte back before it adds
/// or takes the bonus, so an XA over 255 wraps.
/// </remarks>
internal static unsafe class Zodiac
{
    private static Game _game = null!;
    private static byte* _table;   // the compatibility by sign difference: 1 Bad, 2 Good, 3 opposite (0x682F20)
    private static int* _shown;    // the compatibility the screen shows (0x186AFFC)
    private static Options.SexualityRule _sexuality;
    private static Options.ZodiacRule _strength;
    private static int _failed;

    public const int Bad = 1, Good = 2, Opposite = 3, Worst = 4, Best = 5;
    private const int Serpentarius = 12;

    // For the offline check.
    internal static nint StepAt { get; private set; }
    internal static nint ShownAt { get; private set; }
    internal static int* Shown => _shown;

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // The damage step: the signs, Serpentarius, the table, opposite signs (a monster Bad, else Best),
        // then XA += (percentage − 100) × XA / 100, at least 1.
        var step = image.FindUnique(
            "48 83 EC 28 4C 8B 15 ?? ?? ?? ?? 4C 8B 0D ?? ?? ?? ?? 45 0F B7 42 08 41 0F B7 51 08 66 41 C1 E8 0C " +
            "66 C1 EA 0C 66 41 83 F8 0C 0F 84 ?? ?? ?? ?? 66 83 FA 0C 0F 84 ?? ?? ?? ?? 0F B7 CA 41 0F B7 C0 " +
            "66 41 2B C8 66 2B C2 66 44 3B C2 41 BB 01 00 00 00 66 0F 46 C1 48 8D 0D ?? ?? ?? ?? 0F B7 C0 8A 0C 08 " +
            "80 F9 03 75 13 41 F6 42 06 20 75 09 41 F6 41 06 20 B1 05 74 03 41 8A CB 66 0F 6F 05 ?? ?? ?? ?? " +
            "C7 44 24 10 32 00 00 00 C7 44 24 14 96 00 00 00 F3 0F 7F 04 24 80 F9 06 73 08 0F B6 C1 8B 0C 84 " +
            "EB 05 B9 64 00 00 00 44 0F BF 05 ?? ?? ?? ?? 83 C1 9C 41 0F AF C8 B8 1F 85 EB 51 F7 E9 C1 FA 05 " +
            "8B C2 C1 E8 1F 03 D0 66 44 03 C2 66 44 89 05 ?? ?? ?? ?? 66 45 3B C3 7D 08 66 44 89 1D ?? ?? ?? ?? " +
            "48 83 C4 28 C3");
        // The same for two units, into the compatibility the screen shows.
        var shown = image.FindUnique(
            "33 C0 4C 8B D1 48 85 C9 74 ?? 48 85 D2 74 ?? 44 0F B7 49 08 44 0F B7 42 08 66 41 C1 E9 0C " +
            "66 41 C1 E8 0C 66 41 83 F9 0C 74 ?? 66 41 83 F8 0C 74 ?? 41 0F B7 C8 41 0F B7 C1 66 41 2B C0 " +
            "66 41 2B C9 66 45 3B C8 66 0F 46 C1 48 8D 0D ?? ?? ?? ?? 0F B7 C0 8A 04 08 3C 03 75 11 " +
            "41 F6 42 06 20 75 08 F6 42 06 20 B0 05 74 02 B0 01 0F B6 C0 89 05 ?? ?? ?? ?? C3");
        _table = (byte*)GameImage.Rip(step + 0x56, 3, 7);
        _shown = (int*)GameImage.Rip(shown + 0x6E, 2, 6);
        if ((byte**)GameImage.Rip(step + 4, 3, 7) != game.Battle.Attacker
            || (byte**)GameImage.Rip(step + 0xB, 3, 7) != game.Routines.Target
            || (short*)GameImage.Rip(step + 0xAA, 4, 8) != game.Battle.XA
            || (byte*)GameImage.Rip(shown + 0x49, 3, 7) != _table
            || _table[6] != Opposite || _table[4] != Good || _table[3] != Bad || _table[0] != 0)
            throw new InvalidDataException("the zodiac code isn't what this DLL expects");
        _sexuality = Options.Current.Sexuality;
        _strength = Options.Current.ZodiacStrength;
        Options.Changed += () => (_sexuality, _strength) = (Options.Current.Sexuality, Options.Current.ZodiacStrength);
        StepAt = step;
        ShownAt = shown;
        // Their callers keep nothing across them (liveregs); the originals never run.
        game.Hooks.Hook(step, (nint)(delegate* unmanaged<void>)&Step, "the zodiac step");
        game.Hooks.Hook(shown, (nint)(delegate* unmanaged<byte*, byte*, int>)&Compatibility, "the shown compatibility");
    }

    /// <summary>For the offline check: the settings the hooks follow.</summary>
    internal static (Options.SexualityRule Sexuality, Options.ZodiacRule Strength) Settings
    {
        get => (_sexuality, _strength);
        set => (_sexuality, _strength) = value;
    }

    [UnmanagedCallersOnly]
    private static void Step()
    {
        try
        {
            Apply(_game.Battle.XA, *_game.Battle.Attacker, *_game.Routines.Target, _sexuality, _strength);
        }
        catch (Exception e)
        {
            Failed("the zodiac step", e);
        }
    }

    [UnmanagedCallersOnly]
    private static int Compatibility(byte* a, byte* b)
    {
        var c = 0;
        try
        {
            c = a == null || b == null ? 0 : Of(a, b, _sexuality);
        }
        catch (Exception e)
        {
            Failed("the shown compatibility", e);
        }
        *_shown = c;
        return c;
    }

    private static int Sign(byte* unit) => *(ushort*)(unit + 8) >> 12;

    /// <summary>
    /// Two units' compatibility, in the game's codes: 0 none, Bad, Good, Worst or Best. Opposite signs are
    /// Bad with a monster, otherwise Best or Worst by gender as <paramref name="sexuality"/> says.
    /// </summary>
    internal static int Of(byte* a, byte* b, Options.SexualityRule sexuality)
    {
        int sa = Sign(a), sb = Sign(b);
        if (sa >= Serpentarius || sb >= Serpentarius)
            return 0;
        int c = _table[Math.Abs(sa - sb)];
        if (c != Opposite)
            return c;
        if (((a[6] | b[6]) & 0x20) != 0)
            return Bad;
        var same = (a[6] & 0xC0) == (b[6] & 0xC0);
        return sexuality switch
        {
            Options.SexualityRule.Straight => same ? Worst : Best,
            Options.SexualityRule.Gay => same ? Best : Worst,
            _ => Best,
        };
    }

    /// <summary>ReMixed's damage step on XA (PS1 0x80184964): the bonus is 25% of XA scaled by <paramref name="strength"/>, twice that for Best and Worst.</summary>
    internal static void Apply(short* xa, byte* attacker, byte* target, Options.SexualityRule sexuality,
                               Options.ZodiacRule strength)
    {
        if (Sign(attacker) == Serpentarius || Sign(target) == Serpentarius)
            return;
        var c = Of(attacker, target, sexuality);
        if (strength == Options.ZodiacRule.Off)
            return;
        var full = 25 * *xa;
        var part = strength switch
        {
            Options.ZodiacRule.Half => (int)((uint)full >> 1),
            Options.ZodiacRule.OneAndAHalf => (int)((uint)full >> 1) + full,
            _ => full,
        } & 0xFFFF;
        var low = *xa & 0xFF; // ReMixed reads XA back as a byte here
        int? changed = c switch
        {
            Good => low + part / 100,
            Bad => low - part / 100,
            Best => low + part / 50,
            Worst => low - part / 50,
            _ => null,
        };
        if (changed is { } value)
            *xa = (short)value;
        if (*xa <= 0)
            *xa = 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"zodiac: {what} failed: {e}");
    }
}
