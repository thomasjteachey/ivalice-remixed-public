using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's repurposed support and movement abilities (docs/MECHANICS.md:
/// "Damage multipliers", "Repurposed movement and reaction abilities"), as hooks
/// on the remaster's shared formula steps. Data: the ability ids to enable.
/// </summary>
internal static unsafe class Supports
{
    private static Game _game = null!;
    private static delegate* unmanaged<void> _hpDamage, _attackMods; // the originals
    private static bool _adeptness, _vehemence, _momentum, _power;

    /// <summary>0x1F8 Momentum is listed (this part takes it out of the set; <see cref="Banditry"/> asks here).</summary>
    internal static bool Momentum => _momentum;

    /// <summary>
    /// Set when Momentum's +10 XA went into a Break (formula 0x25): ReMixed then knocks the target back after the break
    /// (PS1 0x80189778). <see cref="Formulas"/>' 0x25 clears it before the game's formula and reads it after.
    /// </summary>
    internal static bool MomentumBreak;
    private static byte* _skillset; // the action's skillset (0x7B0776; PS1 0x801938D4)
    private const int MathSkill = 0x15;
    private static int _failed;

    /// <summary>
    /// Data: the ability ids to enable. Damage supports and Momentum are here; the turn
    /// and move abilities are in <see cref="Movement"/>, and reactions in <see cref="Reactions"/>.
    /// Each part installs on its own, so one that can't find its code doesn't stop the rest.
    /// </summary>
    public static void Install(Game game, JsonElement data)
    {
        _game = game;
        var ids = data.EnumerateArray().Select(e => e.GetInt32()).ToHashSet();
        var enabled = new List<string>();
        var twoSpells = ids.Contains(TwoSpells.Id); // Passives' part takes the id out of the set too
        Part("damage supports", () =>
        {
            _adeptness = ids.Remove(0x1E4);
            _vehemence = ids.Remove(0x1D9);
            _momentum = ids.Remove(0x1F8);
            var r = game.Routines;
            _ = game.Battle.EffectiveFormula; // found now, so a missing pattern stops this part here
            // The action routine keeps the action's skillset, then reads its type from the table.
            _skillset = (byte*)GameImage.Rip(game.Image.FindUnique("40 88 2D ?? ?? ?? ?? 42 8A AC 25 ?? ?? ?? ??"), 3, 7);
            _power = Options.Current.ChangesPower;
            Options.Changed += () => _power = Options.Current.ChangesPower; // a save's power mode
            // Hooked whatever the options (a save's can set a power mode): with nothing on, the damage stays.
            _hpDamage = (delegate* unmanaged<void>)game.Hooks.Hook(
                    (nint)r.HpDamage, (nint)(delegate* unmanaged<void>)&HpDamage, "HP damage (XA × YA)");
            if (_momentum)
                _attackMods = (delegate* unmanaged<void>)game.Hooks.Hook(
                    (nint)r.AttackMods, (nint)(delegate* unmanaged<void>)&AttackMods, "the attack modifiers");
            enabled.AddRange(new[] { (_adeptness, "0x1E4 Adeptness"), (_vehemence, "0x1D9 Vehemence"), (_momentum, "0x1F8 Momentum"),
                                     (_power, "the power settings") }
                .Where(e => e.Item1).Select(e => e.Item2));
        });
        Part("movement abilities", () => enabled.AddRange(Movement.Install(game, ids)));
        Part("reactions", () => enabled.AddRange(Reactions.Install(game, ids)));
        Part("growth", () => enabled.AddRange(Growth.Install(game, ids)));
        Part("banditry", () => enabled.AddRange(Banditry.Install(game, ids)));
        Part("learned passives", () => enabled.AddRange(Passives.Install(game, ids)));
        // 0x1D0 is ReMixed's Two Spells only once the build lists it; until then it's vanilla's Gained EXP Up.
        Part("two spells", () =>
        {
            if (twoSpells)
                enabled.AddRange(TwoSpells.Install(game));
        });
        Part("monster skill", () => enabled.AddRange(MonsterSkill.Install(game, ids)));
        foreach (var id in ids)
            game.Log.Warn($"supports: 0x{id:X3} isn't implemented in this DLL; rebuild the mod");
        game.Log.Info($"supports: {(enabled.Count == 0 ? "none" : string.Join(", ", enabled))}");

        void Part(string what, Action install)
        {
            try
            {
                install();
            }
            catch (Exception e)
            {
                game.Log.Error($"supports: {what} not applied: {e.Message}");
            }
        }
    }

    /// <summary>
    /// The shared HP damage step (PS1 0x80186568, ReMixed 0x80158300): after
    /// XA × YA, Adeptness (0x1E4) adds 20%, and Vehemence (0x1D9) adds 50% for an
    /// attacker that has it and 50% for a target that has it.
    /// </summary>
    /// <remarks>
    /// ReMixed's same step also doubles damage for Two Spells' second spell, and
    /// scales it by the Power Modes option; those come with those mechanics.
    /// </remarks>
    [UnmanagedCallersOnly]
    private static void HpDamage()
    {
        _hpDamage();
        try
        {
            var battle = _game.Battle;
            var attacker = *battle.Attacker;
            var target = *_game.Routines.Target;
            var percent = 100;
            if (_adeptness && (attacker[Battle.UnitSupports + 3] & 0x02) != 0)
                percent += 20;
            if (_vehemence && (attacker[Battle.UnitSupports + 2] & 0x10) != 0)
                percent += 50;
            if (_vehemence && target != null && (target[Battle.UnitSupports + 2] & 0x10) != 0)
                percent += 50;
            var power = _power ? Power(attacker, *_skillset, Options.Current) : 100;
            if (percent == 100 && power == 100)
                return;
            var damage = (ushort*)(*battle.TargetResult + Battle.HpDamage);
            var before = *damage;
            *damage = (ushort)Scaled(before, percent, power);
            Tracer.Line(_game.Log, $"supports: ability 0x{*battle.AbilityId:X3}, HP damage {before} × {percent}% × {power}% = {*damage}");
        }
        catch (Exception e)
        {
            Failed("HP damage", e);
        }
    }

    /// <summary>
    /// ReMixed's Power Modes (var 0x16; PS1 0x80160E00, right after the multiply): the percentage for
    /// the attacker's team (+0x1EE &amp; 0x30), or Math Skill's (skillset 0x15) pair.
    /// </summary>
    internal static int Power(byte* attacker, int skillset, Options options)
    {
        var enemy = attacker != null && (attacker[Battle.UnitTeam] & 0x30) != 0;
        var p = options.Percentages;
        return skillset == MathSkill ? (enemy ? p.EnemyMath : p.AllyMath) : (enemy ? p.Enemy : p.Ally);
    }

    /// <summary>
    /// The supports' percentage, then the power's, each rounding down as in ReMixed; a hit that does
    /// damage still does at least 1 after the power.
    /// </summary>
    internal static int Scaled(int damage, int percent, int power)
    {
        damage = Math.Min(damage * percent / 100, 0x7FFF);
        if (power != 100 && damage != 0)
            damage = Math.Clamp(damage * power / 100, 1, 0x7FFF);
        return damage;
    }

    /// <summary>
    /// The attack modifiers (PS1 0x801885F8). For the Breaks (formula 0x25), whose
    /// chance builds XA from PA, ReMixed's Momentum (0x1F8) adds 10 to XA with a
    /// melee weapon, or none: item types below 10 and 15–18 (PS1 0x80151940).
    /// </summary>
    /// <summary>Hooks the attack modifiers once: Momentum's bonus and the gun skills' XA rules share it.</summary>
    internal static void HookAttackMods(Game game)
    {
        if (_attackMods != null)
            return;
        _game = game;
        _attackMods = (delegate* unmanaged<void>)game.Hooks.Hook(
            (nint)game.Routines.AttackMods, (nint)(delegate* unmanaged<void>)&AttackMods, "the attack modifiers");
    }

    [UnmanagedCallersOnly]
    private static void AttackMods()
    {
        try
        {
            GunSkills.AdjustXa();
        }
        catch (Exception e)
        {
            Failed("the gun skills' XA", e);
        }
        try
        {
            var battle = _game.Battle;
            if (_momentum && *battle.EffectiveFormula == 0x25 && ((*battle.Attacker)[Battle.UnitMovement + 2] & 0x20) != 0)
            {
                var type = _game.Routines.GetItem(*_game.Routines.WeaponItemId)[5];
                if (type < 10 || (uint)(type - 15) < 4)
                {
                    *battle.XA += 10;
                    MomentumBreak = true;
                    Tracer.Line(_game.Log, $"supports: Momentum, ability 0x{*battle.AbilityId:X3}, item type {type}, XA {*battle.XA}");
                }
            }
        }
        catch (Exception e)
        {
            Failed("Momentum", e);
        }
        _attackMods();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"supports: {what} failed: {e}");
    }
}
