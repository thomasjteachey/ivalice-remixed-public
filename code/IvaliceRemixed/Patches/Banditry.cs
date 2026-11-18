using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Banditry (0x1CF, the Thief's innate; vanilla Gained JP Up; docs/MECHANICS.md:
/// "Banditry"). Installed by <see cref="Supports"/>.
/// </summary>
/// <remarks>
/// A basic attack (formulas below 7) from behind by a unit with Banditry, or Mug's (formula 0x27)
/// by anyone, is a critical hit: +50% XA with a knife, no weapon, a crossbow or a bow, +25%
/// otherwise, without the knockback (PS1 0x80151840, 0x80160440, in ReMixed's rewrite of the
/// critical step 0x801864F8). ReMixed's Back Attacks option can make that vanilla's 4% or every
/// unit's (<see cref="Options.BackAttacks"/>; the default is the above). And an AI unit with Banditry uses Mug (0x133) where its list has Gil Taking
/// (0x6C; PS1 0x80150E20, from the AI's ability list at 0x8018199C).
/// <para>The same step holds Momentum's knockback (0x1F8, 0x80151840's flag 4): a basic attack (or Mug's) with a melee
/// weapon by a unit with Momentum that moved this turn is a critical hit with a knockback, without extra damage.
/// ReMixed reads its moved bit (+0x187 bit 7, which its gun skills also set when they lock the move), and skips the
/// after-action reactions (the battle loop's counter 2), so a counter by a unit that moved earlier doesn't count. Here
/// the attacker has to be the unit whose turn it is (+0x1B8) and to have moved (+0x1B9 &amp; 1).</para>
/// </remarks>
internal static unsafe class Banditry
{
    private static Game _game = null!;
    private static delegate* unmanaged<void> _critical; // the original
    private static delegate* unmanaged<int> _random;
    /// <summary>The critical step's knockback (the game's). The offline check swaps it.</summary>
    internal static delegate* unmanaged<void> GameKnockback;
    private static delegate* unmanaged<int, int, int> _skillsetAbility; // (skillset, slot) → ability (0x275860)
    private static byte* _facing; // the attack's angle on the target: 0 front, 1 side, 2 back (0x7B0785)
    private static int _failed;

    private const int Id = 0x1CF, Bit = 0x40; // support byte +0x99
    private const int GilTaking = 0x6C, Mug = 0x133, MugFormula = 0x27;

    // For the offline check.
    internal static nint CriticalAt { get; private set; }
    internal static nint AiStub { get; private set; }
    internal static nint[] AiCalls { get; private set; } = [];
    internal static byte* Facing => _facing;

    private static bool _banditry, _momentum;

    /// <summary>Hooks what <paramref name="ids"/> asks for and takes those ids out of it. Returns what it enabled.</summary>
    public static List<string> Install(Game game, HashSet<int> ids)
    {
        _banditry = ids.Remove(Id);
        _momentum = Supports.Momentum;
        if (!_banditry && !_momentum)
            return [];
        _game = game;
        var image = game.Image;
        // The critical step (PS1 0x801864F8): a 4% roll, then XA + rand × XA / 32768 and a knockback.
        var critical = image.FindUnique(
            "48 83 EC 28 E8 ?? ?? ?? ?? 6B C8 64 81 F9 00 00 02 00 7D ?? 48 8B 05 ?? ?? ?? ?? C6 40 01 01 E8 ?? ?? ?? ?? " +
            "44 0F BF 05 ?? ?? ?? ?? 41 0F AF C0 99 81 E2 FF 7F 00 00 03 C2 C1 F8 0F 66 44 03 C0 66 44 89 05 ?? ?? ?? ?? " +
            "E8 ?? ?? ?? ?? 48 83 C4 28 C3");
        var calls = GameImage.Calls(critical, "48 83 EC 28 E8 ?? ?? ?? ??", "the critical step");
        if (GameImage.Rip(critical + 20, 3, 7) != (nint)game.Battle.TargetResult || GameImage.Rip(critical + 36, 4, 8) != (nint)game.Battle.XA
            || GameImage.Rip(critical + 31, 1, 5) != calls[0])
            throw new InvalidDataException("the critical step isn't the code this DLL expects");
        _random = (delegate* unmanaged<int>)calls[0];
        GameKnockback = (delegate* unmanaged<void>)GameImage.Rip(critical + 72, 1, 5);
        // Where the evade setup stores the attack's angle (PS1 0x801938E2), and clears the evades it skips.
        var facing = image.FindUnique("88 0D ?? ?? ?? ?? 84 C9 74 ?? C6 05 ?? ?? ?? ?? 00 80 F9 02 75 ?? 66 83 25 ?? ?? ?? ?? 00 C3");
        _facing = (byte*)GameImage.Rip(facing, 2, 6);
        // The AI's two ability lists ask the skillset for each slot's ability; each keeps the unit's
        // battle index in its [rsp+0x28] (stored once, at the start).
        var ai1 = image.FindUnique("0F B6 4C 24 21 8B D5 E8 ?? ?? ?? ?? 48 0F BF F0");
        var ai2 = image.FindUnique("41 8B CF 41 8B D6 E8 ?? ?? ?? ?? 48 0F BF F8");
        var ai1Index = image.FindUnique("8B C1 89 4C 24 28 49 8B F1 48 8B 8C 24 80 01 00 00");
        var ai2Index = image.FindUnique("44 0F B6 FA 89 4C 24 28 89 7C 24 24 E8");
        var getter = GameImage.Rip(ai1 + 7, 1, 5);
        if (GameImage.Rip(ai2 + 6, 1, 5) != getter || (ulong)(ai1 - ai1Index) > 0x200 || (ulong)(ai2 - ai2Index) > 0x200)
            throw new InvalidDataException("the AI's ability lists aren't the code this DLL expects");
        _skillsetAbility = (delegate* unmanaged<int, int, int>)getter;
        // Each call goes to a stub near the code instead, which passes the caller's [rsp+0x28] in r8d
        // and keeps r10, r11 and xmm0–5: the second list keeps r11 across the call (the getter never
        // touches it).
        var stub = Stub.KeepingRegisters((nint)(delegate* unmanaged<int, int, int, int>)&AiAbility, 0x28, image.ImageStart);
        CriticalAt = critical;
        AiStub = stub;
        AiCalls = [ai1 + 7, ai2 + 6];

        _critical = (delegate* unmanaged<void>)game.Hooks.Hook(critical, (nint)(delegate* unmanaged<void>)&Critical, "the critical step");
        var enabled = new List<string>();
        if (_momentum)
            enabled.Add("0x1F8 Momentum's knockback after moving");
        if (!_banditry)
            return enabled;
        enabled.Add("0x1CF Banditry");
        foreach (var call in AiCalls)
        {
            var rel = (long)stub - (call + 5);
            if (rel is < int.MinValue or > int.MaxValue)
                throw new InvalidOperationException("the AI stub is out of reach");
            var patch = new byte[5];
            patch[0] = 0xE8;
            BitConverter.TryWriteBytes(patch.AsSpan(1), (int)rel);
            GameImage.WriteCode(call, patch);
        }
        return enabled;
    }

    /// <summary>
    /// ReMixed's critical step: the game's own when the attack isn't a Banditry back attack. For one,
    /// the 4% roll still adds its random part and the knockback, and the back attack adds its bonus
    /// and makes it a critical hit either way.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void Critical()
    {
        var bonus = 0;
        var momentum = false;
        try
        {
            bonus = BackAttackBonus();
            momentum = MomentumKnockback();
        }
        catch (Exception e)
        {
            Failed("the back attack", e);
        }
        if (bonus <= 0 && !momentum)
        {
            _critical();
            return;
        }
        var rolled = _random() * 100 < 0x20000; // the game's 4%
        var xa = *_game.Battle.XA;
        var variance = rolled ? _random() * xa / 0x8000 : 0;
        *_game.Battle.XA = (short)(xa + Math.Max(bonus, 0) + variance);
        (*_game.Battle.TargetResult)[1] = 1; // a critical hit
        if (rolled || momentum)
            GameKnockback();
        Tracer.Line(_game.Log, $"banditry: {(bonus > 0 ? "a back attack" : "Momentum after moving")}, XA {xa} + {Math.Max(bonus, 0)}" +
                               $"{(rolled ? $" + {variance}" : "")}{(rolled || momentum ? ", and a knockback" : "")}");
    }

    /// <summary>
    /// Momentum's knockback (PS1 0x80151840's flag 4): a basic attack (effective formula below 7) or Mug's, with no weapon
    /// or a melee one (types below 10 and 15–18: not a gun, crossbow, bow, instrument or book), by a unit with 0x1F8
    /// (+0x9E &amp; 0x20) whose turn it is and that has moved.
    /// </summary>
    internal static bool MomentumKnockback()
    {
        if (!_momentum)
            return false;
        var formula = *_game.Battle.EffectiveFormula;
        var attacker = *_game.Battle.Attacker;
        if (attacker == null || (formula >= 7 && formula != MugFormula) || (attacker[Battle.UnitMovement + 2] & 0x20) == 0
            || attacker[Battle.UnitTurn] == 0 || (attacker[Battle.UnitMoved] & 1) == 0)
            return false;
        var type = WeaponType();
        return type < 10 || type is >= 15 and <= 18;
    }

    /// <summary>
    /// The XA a back attack adds (PS1 0x80151840's flag 2, used at 0x80160470), or 0: for a basic
    /// attack (effective formula below 7) or Mug's formula, from behind, by a unit with Banditry or
    /// with Mug; half the XA with no weapon, a knife, a crossbow or a bow, a quarter otherwise.
    /// </summary>
    internal static int BackAttackBonus()
    {
        var rule = Options.Current.BackAttacks;
        var formula = *_game.Battle.EffectiveFormula;
        var attacker = *_game.Battle.Attacker;
        if (!_banditry || rule == Options.BackAttackRule.Vanilla || attacker == null || (formula >= 7 && formula != MugFormula)
            || (*_facing & 2) == 0)
            return 0;
        if (rule == Options.BackAttackRule.BanditryAndMug && (attacker[Battle.UnitSupports + 1] & Bit) == 0 && formula != MugFormula)
            return 0;
        var xa = *_game.Battle.XA;
        if (xa <= 0)
            return 0;
        var type = WeaponType();
        return type is 0 or 1 or 11 or 12 ? xa / 2 : xa / 4;
    }

    private static int WeaponType()
    {
        var item = _game.Routines.GetItem(*_game.Routines.WeaponItemId);
        return item == null ? 0 : item[5];
    }

    /// <summary>An AI ability list's call for a slot's ability (from the stub): Mug for Gil Taking on a Banditry unit.</summary>
    [UnmanagedCallersOnly]
    private static int AiAbility(int skillset, int slot, int index)
    {
        var ability = _skillsetAbility(skillset, slot);
        try
        {
            return ForAi(ability, index);
        }
        catch (Exception e)
        {
            Failed("the AI's Mug", e);
            return ability;
        }
    }

    internal static int ForAi(int ability, int index)
    {
        if ((short)ability != GilTaking || (uint)index >= Battle.UnitCount)
            return ability;
        var unit = _game.TurnCode.Units + index * Battle.UnitSize;
        return (unit[Battle.UnitSupports + 1] & Bit) != 0 ? Mug : ability;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"banditry: {what} failed: {e}");
    }
}
