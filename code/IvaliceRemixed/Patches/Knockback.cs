using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's knockback rules (docs/MECHANICS.md: "Knockback rules"; PS1 0x801843EC, rewritten in
/// place). Installed by <see cref="Rules"/> with "knockback".
/// </summary>
/// <remarks>
/// The remaster's knockback (0x304DC0, from the critical step and one formula) is vanilla's. This
/// replaces it with ReMixed's: Immortal units and the party leader can be knocked back unless the
/// Knockback setting is vanilla, only an attack's last strike knocks back, and the landing tile may
/// be water (not with the vanilla setting, and not on rows 16–31, ReMixed's quirk) or lava (not for
/// a target with +0x06 &amp; 4 and neither Waterwalking nor Levitate). The rest is the game's: the
/// direction, the landing level (0x304F44, which also refuses a tile with a unit on it), the fall
/// type and where it's stored.
/// </remarks>
internal static unsafe class Knockback
{
    private static Game _game = null!;
    private static byte* _single;      // 1 for a single-target action (PS1 0x801938EC)
    private static byte* _fall;        // the fall type: 0x81, or 0x82 for a fall that hurts (PS1 0x801938ED)
    private static byte* _landing;     // x, y, level (PS1 0x801938CA)
    private static byte* _strikes;     // the attack's strike count (PS1 0x801938C2)
    private static byte* _strike;      // the strike being worked out, from 0 (PS1 0x801938C3)
    private static delegate* unmanaged<int, int, int, int> _landingLevel; // (direction, x, y) → level, or −1
    private static delegate* unmanaged<byte*, byte> _fallCheck;           // bit 7: the drop can hurt (PS1 0x8017F020)
    private static bool _vanilla;
    private static int _failed;

    // For the offline check.
    internal static nint RoutineAt { get; private set; }

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // The single-target test, the target's +5 tests (Immortal; the party leader), attacker and
        // link tests, then the direction away from the attacker.
        var at = image.FindUnique(
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 20 BD 01 00 00 00 40 38 2D ?? ?? ?? ?? " +
            "0F 85 ?? ?? ?? ?? 48 8B 35 ?? ?? ?? ?? 8A 46 05 A8 04 0F 85 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? " +
            "48 3B F1 0F 84 ?? ?? ?? ?? 80 BE B4 01 00 00 00 0F 85 ?? ?? ?? ?? 44 8D 4D 02 41 22 C1 41 3A C1 " +
            "0F 84 ?? ?? ?? ?? 0F B6 41 4F 0F B6 7E 4F 0F B6 5E 50 44 8B C7 44 2B C0 44 8B DB 0F B6 41 50 " +
            "44 2B D8 41 8B C0 99 44 8B D0 41 8B C3 44 33 D2 44 2B D2 99 33 C2 2B C2 42 8D 0C 10 85 C9 " +
            "0F 84 ?? ?? ?? ?? 8B CF 44 3B D0 7E 16 45 85 C0 8B FD 44 0F 4E CD 83 C8 FF 45 85 C0 0F 4E F8 " +
            "03 F9 EB 14 45 85 DB 7E 0A 03 DD 41 B9 02 00 00 00 EB 05 2B DD 45 33 C9");
        // The landing: its level, the tile checks (blocked, any depth, a slope over 2, lava, 0x1C),
        // the fall type, then the stores and the result's knocked-back flag.
        var tail = at + 0xD3;
        var calls = GameImage.Calls(tail,
            "44 8B C3 8B D7 41 8B C9 E8 ?? ?? ?? ?? 8B E8 85 C0 0F 88 ?? ?? ?? ?? 0F B6 0D ?? ?? ?? ?? " +
            "48 8D 15 ?? ?? ?? ?? 0F AF CB C1 E0 08 03 CF 03 C1 48 98 8A 0C C2 80 E1 3F F6 44 C2 06 02 75 ?? " +
            "8A 44 C2 03 3C 20 73 ?? 24 1F 3C 02 77 ?? 80 F9 12 74 ?? 80 F9 1C 74 ?? 48 8B CE E8 ?? ?? ?? ?? " +
            "84 C0 79 ?? F6 46 63 06 C6 05 ?? ?? ?? ?? 82 74 ?? C6 05 ?? ?? ?? ?? 81 48 8B 05 ?? ?? ?? ?? " +
            "B9 00 40 00 00 40 88 3D ?? ?? ?? ?? 88 1D ?? ?? ?? ?? 40 88 2D ?? ?? ?? ?? 66 09 48 12",
            "the knockback's landing");
        // The action routine's hand for a strike: the count (above 2, the right hand), else the index.
        var hand = image.FindUnique("80 3D ?? ?? ?? ?? 02 88 05 ?? ?? ?? ?? 76 0A 44 0F B7 0D ?? ?? ?? ?? EB 18 " +
                                    "44 38 3D ?? ?? ?? ?? 44 0F B7 0D");
        var battle = game.Battle;
        var map = game.Map;
        _single = (byte*)GameImage.Rip(at + 0x19, 3, 7);
        _fall = (byte*)GameImage.Rip(tail + 0x66, 2, 7);
        _landing = (byte*)GameImage.Rip(tail + 0x82, 3, 7);
        _strikes = (byte*)GameImage.Rip(hand, 2, 7);
        _strike = (byte*)GameImage.Rip(hand + 0x19, 3, 7);
        if ((byte**)GameImage.Rip(at + 0x26, 3, 7) != game.Routines.Target
            || (byte**)GameImage.Rip(at + 0x38, 3, 7) != battle.Attacker
            || (byte**)GameImage.Rip(tail + 0x76, 3, 7) != battle.TargetResult
            || GameImage.Rip(tail + 0x17, 3, 7) != map.WidthAddress || GameImage.Rip(tail + 0x1E, 3, 7) != map.TilesAddress
            || (byte*)GameImage.Rip(tail + 0x6F, 2, 7) != _fall || _fall != _single + 1
            || (byte*)GameImage.Rip(tail + 0x89, 2, 6) != _landing + 1 || (byte*)GameImage.Rip(tail + 0x8F, 3, 7) != _landing + 2
            || _strike != _strikes + 1)
            throw new InvalidDataException("the knockback's globals aren't the ones this DLL expects");
        _landingLevel = (delegate* unmanaged<int, int, int, int>)calls[0];
        _fallCheck = (delegate* unmanaged<byte*, byte>)calls[1];
        _vanilla = Options.Current.Knockback == Options.KnockbackRule.Vanilla;
        Options.Changed += () => _vanilla = Options.Current.Knockback == Options.KnockbackRule.Vanilla; // a save's own
        RoutineAt = at;
        // Its callers keep nothing across it (liveregs); the original never runs.
        game.Hooks.Hook(at, (nint)(delegate* unmanaged<void>)&Routine, "the knockback");
    }

    /// <summary>For the offline check: the Knockback setting the routine follows.</summary>
    internal static bool Vanilla
    {
        get => _vanilla;
        set => _vanilla = value;
    }

    /// <summary>For the offline check: the globals the routine reads and writes.</summary>
    internal static byte* SingleTarget => _single;
    internal static byte* FallType => _fall;
    internal static byte* LandingAt => _landing;
    internal static byte* Strikes => _strikes;

    [UnmanagedCallersOnly]
    private static void Routine()
    {
        try
        {
            Knock();
        }
        catch (Exception e)
        {
            Failed("the knockback", e);
        }
    }

    private static void Knock()
    {
        if (*_single != 1)
            return;
        var target = *_game.Routines.Target;
        var attacker = *_game.Battle.Attacker;
        if (!Spares(target, attacker) && LastStrike(*_strikes, *_strike))
            Move(target, attacker, false);
    }

    /// <summary>
    /// Hook Shot's pull (ReMixed's knockback routine with a0 = 1, from 0x80161080): one tile toward the attacker along
    /// the axis it's further on (Y on a tie), for a single-target action, with the knockback's rules for who's spared
    /// and where it can land (an adjacent target has the attacker on that tile, so it stays), but on any strike. Returns
    /// whether it moved the target.
    /// </summary>
    internal static bool Pull()
    {
        if (_single == null || *_single != 1)
            return false;
        var target = *_game.Routines.Target;
        var attacker = *_game.Battle.Attacker;
        return target != null && attacker != null && !Spares(target, attacker) && Move(target, attacker, true);
    }

    private static bool Move(byte* target, byte* attacker, bool pull)
    {
        int x = target[Battle.UnitX], y = target[Battle.UnitY];
        int dx = x - attacker[Battle.UnitX], dy = y - attacker[Battle.UnitY];
        if (dx == 0 && dy == 0)
            return false;
        var away = pull ? -1 : 1;
        int direction;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            var step = (dx > 0 ? 1 : -1) * away;
            direction = step > 0 ? 3 : 1;
            x += step;
        }
        else
        {
            var step = (dy > 0 ? 1 : -1) * away;
            direction = step > 0 ? 2 : 0;
            y += step;
        }
        if (x == attacker[Battle.UnitX] && y == attacker[Battle.UnitY])
            return false;
        var level = _landingLevel(direction, x, y);
        if (level < 0)
            return false;
        var tile = _game.Map.TileAt(x, y, level);
        if (tile == null || !CanLand(target, tile, y, _vanilla))
            return false;
        *_fall = (_fallCheck(target) & 0x80) != 0 && (target[Battle.UnitStatuses + 2] & 6) == 0 ? (byte)0x82 : (byte)0x81;
        _landing[0] = (byte)x;
        _landing[1] = (byte)y;
        _landing[2] = (byte)level;
        *(ushort*)(*_game.Battle.TargetResult + Battle.ResultFlags2) |= 0x4000;
        Tracer.Line(_game.Log, $"knockback: {(pull ? "pulled" : "pushed")} to ({x}, {y}) level {level}, surface 0x{tile[0] & 0x3F:X2}, depth {tile[3] >> 5}");
        return true;
    }

    /// <summary>
    /// Whether nothing happens to this target: it's the attacker or linked (a rider or its mount), or,
    /// with the vanilla setting, Immortal (+0x05 &amp; 4) or the party leader (+0x05 &amp; 3 is 3).
    /// </summary>
    private static bool Spares(byte* target, byte* attacker)
    {
        if (target == attacker || target[Battle.UnitLinked] != 0)
            return true;
        var flags = target[Battle.UnitCurrentTeam];
        return _vanilla && ((flags & 4) != 0 || (flags & 3) == 3);
    }

    /// <summary>ReMixed knocks back only on an attack's last strike, such as a dual-wielder's second.</summary>
    internal static bool LastStrike(int strikes, int strike) => strikes == strike + 1;

    /// <summary>
    /// Whether a knockback can land on <paramref name="tile"/> (row <paramref name="y"/>): ReMixed's checks
    /// in place of vanilla's no water and no lava.
    /// </summary>
    internal static bool CanLand(byte* target, byte* tile, int y, bool vanilla)
    {
        if ((tile[6] & 2) != 0)
            return false;
        // Water: ReMixed tests bit 4 of a register holding the row there, so rows 16–31 refuse it.
        if ((tile[3] & 0xE0) != 0 && (vanilla || (y & 0x10) != 0))
            return false;
        if ((tile[3] & 0x1F) > 2)
            return false;
        var surface = tile[0] & 0x3F;
        if (surface == 0x1C)
            return false;
        // Lava: refused only for a target with +0x06 & 4 (a frozen body) that can't stand on it.
        return surface != 0x12 || (target[Battle.UnitMovement + 2] & 0x88) != 0 || (target[6] & 4) == 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"knockback: {what} failed: {e}");
    }
}
