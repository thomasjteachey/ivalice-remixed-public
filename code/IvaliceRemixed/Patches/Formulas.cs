using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's damage formulas (docs/MECHANICS.md, docs/analysis/battle-80150000.md §1,
/// battle-80158000.md §15). Each one replaces an entry of the remaster's formula
/// table. Data: a list of formula ids to install.
/// </summary>
/// <remarks>
/// The remaster rebuilt the PS1's formulas as native functions with the same
/// structure, so ReMixed's formulas are built from the remaster's own building
/// blocks (<see cref="Routines"/>) in ReMixed's order, plus ReMixed's own
/// arithmetic. Formulas take no arguments; they work on the battle globals.
/// </remarks>
internal static unsafe class Formulas
{
    private enum Slot
    {
        /// <summary>Empty in vanilla (and on the remaster): the table entry must be the shared stub.</summary>
        Empty,
        /// <summary>A vanilla formula ReMixed changes: the table entry must be a real routine, kept for fallback.</summary>
        Vanilla,
    }

    private static Battle _battle = null!;
    private static RuleCode _code = null!;
    private static Routines _r = null!;
    private static Geomancy? _geomancy;
    private static Log _log = null!;
    private static readonly nint[] Original = new nint[0x6B];
    private static int _failed;

    private static readonly Dictionary<int, (nint Routine, Slot Slot)> Implemented = new()
    {
        [0x05] = ((nint)(delegate* unmanaged<void>)&GunShot, Slot.Vanilla),
        [0x11] = ((nint)(delegate* unmanaged<void>)&StatusStrike, Slot.Empty),
        [0x13] = ((nint)(delegate* unmanaged<void>)&SacrificeStrike, Slot.Empty),
        [0x15] = ((nint)(delegate* unmanaged<void>)&ReturnTwo, Slot.Vanilla),
        [0x18] = ((nint)(delegate* unmanaged<void>)&WindWithKnockback, Slot.Empty),
        [0x1A] = ((nint)(delegate* unmanaged<void>)&Ruin, Slot.Vanilla),
        [0x1E] = ((nint)(delegate* unmanaged<void>)&SameTeamHeal, Slot.Vanilla),
        [0x21] = ((nint)(delegate* unmanaged<void>)&BizenBoat, Slot.Vanilla),
        [0x24] = ((nint)(delegate* unmanaged<void>)&GeomancyByTerrain, Slot.Vanilla),
        [0x25] = ((nint)(delegate* unmanaged<void>)&MomentumBreak, Slot.Vanilla), // the Knight's Breaks, rewritten in place
        [0x27] = ((nint)(delegate* unmanaged<void>)&GilTaking, Slot.Vanilla),
        [0x2B] = ((nint)(delegate* unmanaged<void>)&StatBreak, Slot.Vanilla),
        [0x2C] = ((nint)(delegate* unmanaged<void>)&MagicBreak, Slot.Vanilla),
        [0x2D] = ((nint)(delegate* unmanaged<void>)&MagicSword, Slot.Vanilla),
        [0x32] = ((nint)(delegate* unmanaged<void>)&RepeatingFist, Slot.Vanilla), // rewritten in place
        [0x39] = ((nint)(delegate* unmanaged<void>)&FalloffStrike, Slot.Vanilla), // vanilla's Yell; ReMixed moves it to 0x3B
        [0x3B] = ((nint)(delegate* unmanaged<void>)&StatsFromData, Slot.Vanilla), // vanilla's Scream, rewritten in place
        [0x3C] = ((nint)(delegate* unmanaged<void>)&SacrificeHeal, Slot.Vanilla), // vanilla's Wish, rewritten in place
        [0x40] = ((nint)(delegate* unmanaged<void>)&SealEvil, Slot.Vanilla), // rewritten in place
        [0x42] = ((nint)(delegate* unmanaged<void>)&WorkerSkill, Slot.Vanilla),
        [0x46] = ((nint)(delegate* unmanaged<void>)&WeaponSkill, Slot.Empty),
        [0x55] = ((nint)(delegate* unmanaged<void>)&Beaking, Slot.Vanilla),
        [0x58] = ((nint)(delegate* unmanaged<void>)&HpMpSwap, Slot.Vanilla),
    };

    /// <summary>Whether this patch put ReMixed's version in a formula's table entry.</summary>
    internal static bool Replaced(int id) => id < Original.Length && Original[id] != 0;

    /// <summary>For the offline check: runs this DLL's version of a formula directly.</summary>
    internal static void Run(int id) => ((delegate* unmanaged<void>)Implemented[id].Routine)();

    /// <summary>For the offline check: the game's own routine a wrapping formula calls, swapped. Returns the one it had.</summary>
    internal static nint SwapOriginal(int id, nint routine)
    {
        var had = Original[id];
        Original[id] = routine;
        return had;
    }

    /// <summary>
    /// The status-type hit (PS1 0x801889CC; 0x306FCC): XA = MA, YA = X, the magic steps, the zodiac, the status
    /// chance and roll; non-zero for a miss. Found in formula 0x15; the offline check swaps it.
    /// </summary>
    internal static delegate* unmanaged<int> StatusHit;

    /// <summary>
    /// The other status-type hit (PS1 0x80188A84; 0x307088): like <see cref="StatusHit"/> with the after-magic-XA step
    /// in place of Faith; non-zero for a miss. Found in formula 0x55; the offline check swaps it.
    /// </summary>
    internal static delegate* unmanaged<int> StatHit;

    public static void Install(Game game, JsonElement data)
    {
        var image = game.Image;
        var log = game.Log;
        _log = log;
        _battle = game.Battle;
        _r = game.Routines;
        _code = game.RuleCode;
        var table = _battle.FormulaTable;
        nint Entry(int id) => image.Pointer(table[id - 1]);
        var stub = Entry(0x11); // vanilla leaves 0x11, 0x13, 0x18, 0x19 and 0x46 empty; they share one stub

        // Data: {"install": [formula ids], "geomancy": [12 x [status set, element, effect]]}.
        if (data.TryGetProperty("geomancy", out var geomancy))
            _geomancy = Geomancy.Find(image, geomancy);

        var installed = new List<string>();
        foreach (var item in data.GetProperty("install").EnumerateArray())
        {
            var id = item.GetInt32();
            if (id == 0x24 && _geomancy is null)
            {
                log.Warn("formulas: 0x24 needs ReMixed's Geomancy table; rebuild the mod");
                continue;
            }
            if (id is 0x1C or 0x1D)
            {
                // Only their effects are ReMixed's: the formulas' table entries stay the game's.
                try
                {
                    if (id == 0x1C)
                        SingAt = InstallPerformance(image, dance: false);
                    else
                        DanceAt = InstallPerformance(image, dance: true);
                    installed.Add($"0x{id:X2}'s effect");
                }
                catch (Exception e)
                {
                    log.Error($"formulas: 0x{id:X2}'s effect not applied: {e.Message}");
                }
                continue;
            }
            if (id == 0x2A)
            {
                // Only its effect is ReMixed's: the formula's table entry stays the game's.
                try
                {
                    InstallTalkEffect(image);
                    installed.Add("0x2A's effect");
                }
                catch (Exception e)
                {
                    log.Error($"formulas: 0x2A's effect not applied: {e.Message}");
                }
                continue;
            }
            if (id == 0x2E)
            {
                // Two changes in the game's own routine (Breaks.cs): its table entry stays the game's.
                try
                {
                    Breaks.Install(Entry(0x2E), _r, _battle);
                    installed.Add("0x2E's breaks");
                }
                catch (Exception e)
                {
                    log.Error($"formulas: 0x2E left alone: {e.Message}");
                }
                continue;
            }
            if (!Implemented.TryGetValue(id, out var formula))
            {
                log.Warn($"formulas: 0x{id:X2} isn't implemented in this DLL; rebuild the mod");
                continue;
            }
            var empty = Entry(id) == stub;
            if (empty != (formula.Slot == Slot.Empty))
            {
                log.Warn($"formulas: 0x{id:X2} is {(empty ? "" : "not ")}empty in this game version; left alone");
                continue;
            }
            if (id == 0x55)
            {
                try
                {
                    // Beaking: the magic hit check, the status-type hit, then PA lowered by Y and flags 1.
                    var calls = GameImage.Calls(Entry(0x55),
                        "48 83 EC 28 E8 ?? ?? ?? ?? 85 C0 75 ?? E8 ?? ?? ?? ?? 85 C0 75 ?? 8A 0D ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? " +
                        "80 E1 7F 88 48 16 C6 40 27 01 48 83 C4 28 C3", "formula 0x55");
                    if (calls[0] != (nint)_r.MagicHit || (byte*)GameImage.Rip(Entry(0x55) + 22, 2, 6) != _battle.Ability + 10)
                        throw new InvalidDataException("formula 0x55's hit check or Y isn't the one this DLL expects");
                    StatHit = (delegate* unmanaged<int>)calls[1];
                }
                catch (Exception e)
                {
                    log.Error($"formulas: 0x55 left alone: {e.Message}");
                    continue;
                }
            }
            if (id == 0x15)
            {
                try
                {
                    // Return 2: the magic hit check, the status-type hit, then the target's CT change and flags.
                    var calls = GameImage.Calls(Entry(0x15),
                        "48 83 EC 28 E8 ?? ?? ?? ?? 85 C0 75 ?? E8 ?? ?? ?? ?? 85 C0 75 ?? 48 8B 05 ?? ?? ?? ?? " +
                        "C6 40 15 7F C6 40 27 01 48 83 C4 28 C3", "formula 0x15");
                    if (calls[0] != (nint)_r.MagicHit || (byte**)GameImage.Rip(Entry(0x15) + 22, 3, 7) != _battle.TargetResult)
                        throw new InvalidDataException("formula 0x15's hit check or result isn't the one this DLL expects");
                    StatusHit = (delegate* unmanaged<int>)calls[1];
                }
                catch (Exception e)
                {
                    log.Error($"formulas: 0x15 left alone: {e.Message}");
                    continue;
                }
            }
            if (id == 0x32)
            {
                try
                {
                    FindFistSteps(Entry(0x32));
                }
                catch (Exception e)
                {
                    log.Error($"formulas: 0x32 left alone: {e.Message}");
                    continue;
                }
            }
            if (id == 0x3C)
                PhoenixDown = (delegate* unmanaged<void>)Entry(0x4B); // the game's own, before the rules' stubs
            Original[id] = Entry(id);
            GameImage.Unprotected((nint)(table + id - 1), sizeof(nint), () => table[id - 1] = formula.Routine);
            installed.Add($"0x{id:X2}");
        }
        log.Info($"formulas: installed {string.Join(", ", installed)} (table at 0x{(nint)table:X})");
    }

    /// <summary>
    /// 0x46, ReMixed's weapon skill (PS1 0x80153020): a weapon strike with the
    /// ability's element, Two Hands applied, and HP damage × X / 16. Braver,
    /// Cross-slash, Omnislash, Poison Nail and the new 0x165–0x16E.
    /// </summary>
    /// <remarks>
    /// Not ported: ReMixed sends magic guns (weapon formula 4) through formula
    /// 4's spell with Y = 14; here they strike like any weapon.
    /// </remarks>
    [UnmanagedCallersOnly]
    private static void WeaponSkill()
    {
        try
        {
            var ability = _battle.Ability;
            var weapon = _battle.Weapon;
            if (ability[7] != 0)
                weapon[6] = ability[7]; // the ability's element replaces the weapon's
            ApplyTwoHands();
            _r.WeaponAttack();
            var before = Result(Battle.HpDamage);
            ScaleHpDamage(ability[9]);
            Trace(0x46, $"X {ability[9]}, element 0x{weapon[6]:X2}, Two Hands {*_battle.TwoHands}, "
                        + $"hit {**_battle.TargetResult}, HP damage {before} -> {Result(Battle.HpDamage)}");
        }
        catch (Exception e)
        {
            Failed(nameof(WeaponSkill), e);
        }
    }

    /// <summary>
    /// 0x2D, ReMixed's version (PS1 0x8015D200). Abilities flagged as magical
    /// attacks (AI flag MagicalAttack: common byte 6 bit 0x02; Meteorain, Cherry
    /// Blossom) become magic swords: magic hit check, XA = MA, YA = Y + WP, the
    /// magic modifiers, HP damage × X / 16, then the weapon's element and the
    /// ability's statuses. Everything else on 0x2D (Holy Sword) is vanilla's.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void MagicSword()
    {
        try
        {
            if ((_battle.AbilityCommon[*_battle.AbilityId * 8 + 6] & 0x02) == 0)
            {
                ((delegate* unmanaged<void>)Original[0x2D])();
                return;
            }
            if (_r.MagicHit() != 0)
                return;
            var ability = _battle.Ability;
            var weapon = _battle.Weapon;
            *_battle.XA = (*_battle.Attacker)[Battle.UnitMa];
            *_battle.YA = (short)(ability[10] + weapon[4]);
            _r.AfterWeaponXa();
            _r.MagicMod1();
            _r.MagicMod2();
            _r.MagicMod3();
            _r.MagicMod4();
            _r.HpDamage();
            var before = Result(Battle.HpDamage);
            ScaleHpDamage(ability[9]);
            _r.Element(weapon[6]);
            var hit = **_battle.TargetResult;
            if (hit != 0)
            {
                _r.ApplyHit();
                _r.Statuses();
            }
            Trace(0x2D, $"magic sword, XA {*_battle.XA}, YA {*_battle.YA}, X {ability[9]}, hit {hit}, "
                        + $"HP damage {before} -> {Result(Battle.HpDamage)}");
        }
        catch (Exception e)
        {
            Failed(nameof(MagicSword), e);
        }
    }

    /// <summary>
    /// 0x1A, ReMixed's Ruins (PS1 0x80153500). With a "status" byte (Alter Speed, Power and Mind) it's vanilla's routine
    /// as ReMixed changed it, <see cref="AlterStats"/>. Without one (Magic Ruin) it builds magic healing (XA = MA,
    /// YA = Y; damage instead if the target is Undead), gives the full amount to MP and half (rounded up) to HP.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void Ruin()
    {
        try
        {
            var ability = _battle.Ability;
            if (ability[11] != 0)
            {
                AlterStats(ability);
                return;
            }
            *_battle.XA = (*_battle.Attacker)[Battle.UnitMa];
            *_battle.YA = ability[10];
            _r.AfterMagicXa();
            _r.MagicMod1();
            _r.MagicMod4();
            _r.HpDamage();
            _r.Faith();
            _r.HealUnlessUndead();
            var result = *_battle.TargetResult;
            var hp = (ushort*)(result + Battle.HpDamage); // HP damage, HP heal, MP damage, MP heal
            hp[2] = hp[0];
            hp[3] = hp[1];
            hp[0] = (ushort)((hp[0] >> 1) + (hp[0] & 1));
            hp[1] = (ushort)((hp[1] >> 1) + (hp[1] & 1));
            result[Battle.ResultFlags] |= (byte)(result[Battle.ResultFlags] >> 2); // HP flags -> MP flags
            Trace(0x1A, $"Magic Ruin, XA {*_battle.XA}, YA {*_battle.YA}, HP -{hp[0]} +{hp[1]}, MP -{hp[2]} +{hp[3]}, "
                        + $"flags 0x{result[Battle.ResultFlags]:X2}");
        }
        catch (Exception e)
        {
            Failed(nameof(Ruin), e);
        }
    }

    /// <summary>
    /// 0x11, ReMixed's stat strike (PS1 0x80152DC0, entered from the formula table): a
    /// weapon strike that adds the ability's element to the weapon's (built with the
    /// ability's Y set aside), then on a hit lowers the target's stats by Y: the ability's
    /// "status" byte is a mask over the result's stat changes (ReMixed's 0x80186CA4, called
    /// with amount Y and no raise bit). Maintenance on the target makes it a miss. Ability
    /// 0x0FA, ReMixed's "Slap": mask 0x02, Y 40, so CT −40.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void StatusStrike()
    {
        try
        {
            if (_r.Evade() != 0)
                return;
            var ability = _battle.Ability;
            var weapon = _battle.Weapon;
            weapon[6] |= ability[7];
            var chance = ability[10];
            ability[10] = 0;
            _r.WeaponXa();
            _r.AfterWeaponXa();
            _r.AttackMods();
            _r.Critical();
            _r.HpDamage();
            _r.Element(weapon[6]);
            ability[10] = chance;
            var result = *_battle.TargetResult;
            if (result[0] == 0)
                return;
            _r.ApplyHit();
            string status;
            if (HasMaintenance())
            {
                BlockedByMaintenance(result);
                status = "blocked by Maintenance";
            }
            else
            {
                StatChange(result, ability[11], chance);
                status = $"stats 0x{ability[11]:X2} down by {chance}";
            }
            result[Battle.ResultFlags] |= (byte)(Result(Battle.HpDamage) != 0 ? 0x80 : 0x40);
            Trace(0x11, $"HP damage {Result(Battle.HpDamage)}, {status}");
        }
        catch (Exception e)
        {
            Failed(nameof(StatusStrike), e);
        }
    }

    /// <summary>
    /// 0x1A with a "status" byte (PS1 0x801892AC, vanilla's routine with ReMixed's changes): the magic hit check, XA = MA
    /// and YA = X (0x80185D80; vanilla took Y, and ReMixed's data swaps the two), the magic steps, chance, Faith and roll
    /// as the remaster's own 0x1A has them; a miss clears the result's bit 2 as vanilla does, a hit is
    /// <see cref="StatsByTeam"/>.
    /// </summary>
    private static void AlterStats(byte* ability)
    {
        if (_r.MagicHit() != 0)
            return;
        *_battle.XA = (*_battle.Attacker)[Battle.UnitMa];
        *_battle.YA = ability[9];
        _r.AfterMagicXa();
        _r.MagicMod1();
        _r.MagicMod2();
        _r.MagicMod3();
        _r.MagicMod4();
        _r.StatusChance();
        _r.Faith();
        _r.StatusRoll();
        var result = *_battle.TargetResult;
        if (result[0] == 0)
        {
            *(ushort*)(result + Battle.ResultFlags2) &= 0xFFFB;
            return;
        }
        var effect = StatsByTeam(result, ability);
        Trace(0x1A, $"ability 0x{*_battle.AbilityId:X3}, XA {*_battle.XA}, YA {*_battle.YA}, {effect}");
    }

    /// <summary>
    /// ReMixed's effect for formulas 0x1A (Alter), 0x1C (Sing) and 0x1D (Dance) (PS1 0x801869EC, rewritten from vanilla's
    /// song switch): with no Y, the ability's status set. Otherwise it raises when the ability's flags byte 1 (+4) has
    /// bit 7 (the songs), lowers for bit 6 (the dances), and with neither raises for the caster's own team and lowers
    /// for others. Then each stat in the "status" byte changes by Y (0x80186CA4; a lowering misses on a target with
    /// Maintenance), or, with no stat bits, HP (bit 7, also for a zero byte) and MP (bit 6) take or get (XA × YA) / 8, at
    /// least 1.
    /// </summary>
    internal static string StatsByTeam(byte* result, byte* ability)
    {
        int amount = ability[10], kind = ability[11];
        if (amount == 0)
        {
            _r.StatusSet();
            return $"status set 0x{kind:X2}";
        }
        var direction = ability[4] & 0xC0;
        var raise = direction == 0x80
                    || (direction == 0 && (((*_battle.Attacker)[Battle.UnitTeam] ^ (*_r.Target)[Battle.UnitTeam]) & 0x30) == 0);
        if (kind == 0)
            kind = 0x80;
        if ((kind & 0x3F) != 0)
        {
            if (!raise && HasMaintenance())
            {
                BlockedByMaintenance(result);
                return "blocked by Maintenance";
            }
            StatChange(result, kind & 0x3F, amount | (raise ? 0x80 : 0));
            return $"stats 0x{kind & 0x3F:X2} {(raise ? "up" : "down")} by {amount}";
        }
        _r.HpDamage();
        var hp = (ushort*)(result + Battle.HpDamage); // HP damage, HP heal, MP damage, MP heal
        var share = Math.Max(1, hp[0] >> 3);
        hp[0] = 0;
        var bits = (kind & 0x80) | ((kind & 0x40) >> 1); // 0x80 HP damage, 0x20 MP damage
        var at = hp;
        if (raise)
        {
            bits >>= 1;                                   // 0x40 HP healing, 0x10 MP healing
            at = hp + 1;
        }
        if ((bits & 0x30) != 0)
            at[2] = (ushort)share;
        if ((bits & 0xC0) != 0)
            at[0] = (ushort)share;
        result[Battle.ResultFlags] = (byte)bits;
        return $"{((bits & 0xC0) != 0 ? "HP " : "")}{((bits & 0x30) != 0 ? "MP " : "")}{(raise ? "+" : "-")}{share}";
    }

    // For the offline check: the songs' and dances' effect sites and stubs.
    internal static (nint At, nint Stub) SingAt { get; private set; }
    internal static (nint At, nint Stub) DanceAt { get; private set; }

    /// <summary>
    /// Formulas 0x1C and 0x1D (Sing and Dance) keep the remaster's target check, and then their effects become ReMixed's
    /// <see cref="StatsByTeam"/>: Sing's switch on ability ids (after XA = MA and YA = Y, as ReMixed has them) and
    /// Dance's weapon XA with its switch (ReMixed: XA = PA, YA = Y, 0x80185E04) each jump to a stub that calls C# and
    /// goes on to the formula's epilogue.
    /// </summary>
    private static (nint, nint) InstallPerformance(GameImage image, bool dance)
    {
        // The target check (0x304D5C), its jne to the epilogue, then Sing's XA and YA and its switch from Angel Song
        // (0x56), or Dance's weapon XA (0x305A48) and its switch from Witch Hunt (0x5D).
        var start = image.FindUnique(dance
            ? "E8 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 0D ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 83 E9 5D 74"
            : "E8 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 0F B7 0D ?? ?? ?? ?? 0F B6 15 ?? ?? ?? ?? " +
              "44 0F B6 40 3F 48 8B 05 ?? ?? ?? ?? 66 44 89 05 ?? ?? ?? ?? 66 89 15 ?? ?? ?? ?? 83 E9 56 74");
        var at = start + (dance ? 13 : 0x3D);
        var epilogue = GameImage.Rip(start + 5 + 2, 2, 6);
        if (dance && GameImage.Rip(at, 1, 5) != (nint)_r.WeaponXa)
            throw new InvalidDataException("the dances' weapon XA isn't the step this DLL expects");
        var managed = dance
            ? (nint)(delegate* unmanaged<void>)&DanceEffect
            : (nint)(delegate* unmanaged<void>)&SingEffect;
        var stub = GameImage.AllocateCodeNear(at, 32);
        var code = new List<byte> { 0x48, 0xB8 };            // mov rax, the effect
        code.AddRange(BitConverter.GetBytes((ulong)managed));
        code.AddRange([0xFF, 0xD0, 0xFF, 0x25, 0, 0, 0, 0]); // call rax (the formula's frame has the shadow space); jmp [rip+0]
        code.AddRange(BitConverter.GetBytes((ulong)epilogue));
        code.ToArray().CopyTo(new Span<byte>(stub, 32));
        byte[] jump = [0xE9, 0, 0, 0, 0];
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(at, jump);
        return (at, (nint)stub);
    }

    [UnmanagedCallersOnly]
    private static void SingEffect()
    {
        try
        {
            var effect = StatsByTeam(*_battle.TargetResult, _battle.Ability);
            Trace(0x1C, $"ability 0x{*_battle.AbilityId:X3}, XA {*_battle.XA}, YA {*_battle.YA}, {effect}");
        }
        catch (Exception e)
        {
            Failed(nameof(SingEffect), e);
        }
    }

    [UnmanagedCallersOnly]
    private static void DanceEffect()
    {
        try
        {
            var ability = _battle.Ability;
            *_battle.XA = (*_battle.Attacker)[Battle.UnitPa];
            *_battle.YA = ability[10];
            var effect = StatsByTeam(*_battle.TargetResult, ability);
            Trace(0x1D, $"ability 0x{*_battle.AbilityId:X3}, XA {*_battle.XA}, YA {*_battle.YA}, {effect}");
        }
        catch (Exception e)
        {
            Failed(nameof(DanceEffect), e);
        }
    }

    /// <summary>
    /// 0x15, Return 2 (ability 0x0E9; PS1 0x801891AC, changed in place): the game's own steps (the magic hit check,
    /// the status-type hit, then the target's CT lowered by 127, which takes it to 0), and then ReMixed's addition, a
    /// miss on a target with Maintenance (vanilla's 0x80187638, as its Breaks have).
    /// </summary>
    [UnmanagedCallersOnly]
    private static void ReturnTwo()
    {
        try
        {
            if (_r.MagicHit() != 0 || StatusHit() != 0)
                return;
            var result = *_battle.TargetResult;
            result[ResultCt] = 0x7F;
            result[Battle.ResultFlags] = 1;
            if (!HasMaintenance())
                return;
            result[0] = 0;
            result[2] = 7;
            *(ushort*)(result + Battle.ResultStatusChance) = 0;
            Trace(0x15, "blocked by Maintenance");
        }
        catch (Exception e)
        {
            Failed(nameof(ReturnTwo), e);
        }
    }

    /// <summary>The result's CT change (+0x15, PS1 +0x13): bit 7 raises, otherwise it lowers.</summary>
    private const int ResultCt = 0x15;

    /// <summary>The result's Speed, PA and MA changes (+0x14, +0x16, +0x17; PS1 +0x12, +0x14, +0x15).</summary>
    private const int ResultSpeed = 0x14, ResultPa = 0x16, ResultMa = 0x17;

    /// <summary>
    /// 0x55, Beaking (ability 0x135; PS1 0x8018A698, changed in place): the physical evade check in place of the magic
    /// hit check, then the status-type hit, and on a hit Speed, PA and MA each lowered by Y (vanilla: PA only).
    /// </summary>
    [UnmanagedCallersOnly]
    private static void Beaking()
    {
        try
        {
            if (_r.Evade() != 0 || StatHit() != 0)
                return;
            var result = *_battle.TargetResult;
            var y = (byte)(_battle.Ability[10] & 0x7F);
            result[ResultSpeed] = y;
            result[ResultPa] = y;
            result[ResultMa] = y;
            result[Battle.ResultFlags] = 1;
            Trace(0x55, $"Speed, PA and MA down by {y}");
        }
        catch (Exception e)
        {
            Failed(nameof(Beaking), e);
        }
    }

    /// <summary>
    /// 0x42, Worker 8's Destroy, Compress, Dispose and Crush (PS1 0x8018A17C, changed in place): XA = PA, YA = Y, the
    /// zodiac, then ReMixed's change: the game's HP damage step (vanilla multiplies inline, skipping the step and the
    /// rules this mod hooks there), the attacker takes the final damage / X, and the ability's statuses apply
    /// (vanilla has no status step; ReMixed gives Compress a set). There's no evade check, in either game.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void WorkerSkill()
    {
        try
        {
            var ability = _battle.Ability;
            *_battle.XA = (*_battle.Attacker)[Battle.UnitPa];
            *_battle.YA = ability[10];
            _r.MagicMod4();
            _r.HpDamage();
            var result = *_battle.TargetResult;
            var x = ability[9];
            var recoil = x == 0 ? 0 : (short)Result(Battle.HpDamage) / x; // ReMixed's X is 4 to 8
            Recoil((ushort)recoil);
            result[Battle.ResultFlags] = 0x80;
            _r.Statuses();
            Trace(0x42, $"XA {*_battle.XA}, YA {*_battle.YA}, HP damage {Result(Battle.HpDamage)}, recoil {recoil}");
        }
        catch (Exception e)
        {
            Failed(nameof(WorkerSkill), e);
        }
    }

    /// <summary>The target has Maintenance (0x1DB), which keeps ReMixed's stat changes from lowering its stats.</summary>
    private static bool HasMaintenance() => ((*_r.Target)[Battle.UnitSupports + 2] & 0x04) != 0;

    /// <summary>ReMixed's miss for a stat change Maintenance blocks (PS1 0x80186CC0).</summary>
    private static void BlockedByMaintenance(byte* result)
    {
        result[0] = 0;
        result[2] = 7;
        result[Battle.ResultFlags] = 0;
        *(ushort*)(result + Battle.ResultStatusChance) = 0;
    }

    /// <summary>
    /// 0x2B, ReMixed's (PS1 0x80189A90, changed in place): the Breaks, and Secret Fist, Eye Gouge and Beak. XA is PA and
    /// YA the ability's X (0x80185DAC; vanilla took Y, and ReMixed's data swaps the two), then the game's attack
    /// modifiers, chance and roll, as its own 0x2B has them; on a hit, <see cref="StatOrStatus"/>.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void StatBreak()
    {
        try
        {
            if (_r.Evade() != 0)
                return;
            var ability = _battle.Ability;
            *_battle.XA = (*_battle.Attacker)[Battle.UnitPa];
            *_battle.YA = ability[9];
            _r.AttackMods();
            _r.StatusChance();
            _r.StatusRoll();
            var result = *_battle.TargetResult;
            if (result[0] == 0)
                return;
            var effect = StatOrStatus(result, ability);
            Trace(0x2B, $"ability 0x{*_battle.AbilityId:X3}, XA {*_battle.XA}, YA {*_battle.YA}, {effect}");
        }
        catch (Exception e)
        {
            Failed(nameof(StatBreak), e);
        }
    }

    /// <summary>
    /// ReMixed's effect for formulas 0x2A and 0x2B (PS1 0x80186C04, rewritten from vanilla's switch on Talk Skill ids):
    /// with a Y, each stat in the ability's "status" byte (bit 0 Speed to 5 Faith) changes by Y, raised for bit 7,
    /// lowered otherwise and then a miss on a target with Maintenance (0x80186CA4); with no Y, the byte is the
    /// ability's status set, through the game's status step; with neither, the gil steal (vanilla's Negotiate).
    /// </summary>
    internal static string StatOrStatus(byte* result, byte* ability)
    {
        int amount = ability[10], kind = ability[11];
        if (amount != 0)
        {
            var raise = kind & 0x80;
            if (raise == 0 && HasMaintenance())
            {
                BlockedByMaintenance(result);
                return "blocked by Maintenance";
            }
            StatChange(result, kind & 0x3F, amount | raise);
            return $"stats 0x{kind & 0x3F:X2} {(raise != 0 ? "up" : "down")} by {amount}";
        }
        if (kind != 0)
        {
            _r.StatusSet();
            return $"status set 0x{kind:X2}";
        }
        _r.StealGil();
        result[Battle.ResultFlags] = 0x01;
        return "gil";
    }

    /// <summary>
    /// 0x3B, ReMixed's (PS1 0x80186D58, vanilla's Scream rewritten in place): <see cref="MaskedStats"/>. The remaster's
    /// (0x306074) is vanilla's: Brave up by X and PA, MA and Speed up by Y whatever the ability's data, and ReMixed moved
    /// Focus, Tailwind, Steel and Gather Power onto it, so with it Tailwind raised Brave by 0 and PA, MA and Speed by 1
    /// (seen in the game, 2026-11-07).
    /// </summary>
    [UnmanagedCallersOnly]
    private static void StatsFromData()
    {
        try
        {
            var effect = MaskedStats(*_battle.TargetResult, _battle.Ability);
            Trace(0x3B, $"ability 0x{*_battle.AbilityId:X3}, {effect}");
        }
        catch (Exception e)
        {
            Failed(nameof(StatsFromData), e);
        }
    }

    /// <summary>
    /// ReMixed's formula 0x3B's effect: each stat in the ability's "status" byte (bit 0 Speed to 5 Faith, 6 the byte
    /// after) changes, Speed, PA, MA and the byte after by Y and CT, Brave and Faith by X (0x8015E7D0), raised for the
    /// byte's bit 7 and lowered otherwise, a lowering missing on a target with Maintenance. ReMixed's data: Focus and
    /// Gather Power PA (0x84), Tailwind Speed (0x81), Steel Brave by X 5 (0x90), Shout Speed, PA, MA and Brave (0x9D).
    /// </summary>
    internal static string MaskedStats(byte* result, byte* ability)
    {
        int x = ability[9], y = ability[10], kind = ability[11];
        var raise = kind & 0x80;
        if (raise == 0 && HasMaintenance())
        {
            BlockedByMaintenance(result);
            return "blocked by Maintenance";
        }
        result[0] = 1;
        result[Battle.ResultFlags] = 0x01;
        for (var k = 0; k < 7; k++)
            if ((kind & (1 << k)) != 0)
                result[Battle.ResultStatChanges + k] = (byte)((((1 << k) & 0x32) != 0 ? x : y) | raise);
        return $"stats 0x{kind & 0x7F:X2} {(raise != 0 ? "up" : "down")} by X {x} or Y {y}";
    }

    // For the offline check: the Talk Skills' effect switch and its stub.
    internal static nint TalkEffectAt { get; private set; }
    internal static nint TalkEffectStub { get; private set; }

    /// <summary>
    /// Formula 0x2A's effect (the remaster's switch on Talk Skill ids at the end of 0x308284, on a hit) becomes
    /// ReMixed's <see cref="StatOrStatus"/>: a jump to a stub that calls <see cref="TalkEffect"/> and goes on to the
    /// formula's epilogue.
    /// </summary>
    private static void InstallTalkEffect(GameImage image)
    {
        // cmp [rdx], sil; je epilogue; movzx ecx, word [ability id]; sub ecx, 0x75 (Persuade); … the statuses' call;
        // jmp; Negotiate's gil steal.
        var start = image.FindUnique(
            "40 38 32 74 ?? 0F B7 0D ?? ?? ?? ?? 83 E9 75 74 ?? 83 E9 01 74 ?? 83 E9 01 74 ?? 83 E9 01 74 ?? " +
            "83 E9 01 74 ?? 83 F9 02 74 ?? E8 ?? ?? ?? ?? EB ?? E8 ?? ?? ?? ??");
        var at = start + 5;
        var epilogue = start + 5 + (sbyte)((byte*)start)[4];
        if (GameImage.Rip(start + 0x2A, 1, 5) != (nint)_r.StatusSet || GameImage.Rip(start + 0x31, 1, 5) != (nint)_r.StealGil)
            throw new InvalidDataException("the Talk Skills' effects aren't the steps this DLL expects");
        var stub = GameImage.AllocateCodeNear(at, 32);
        var code = new List<byte> { 0x48, 0xB8 };                    // mov rax, TalkEffect
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<void>)&TalkEffect));
        code.AddRange([0xFF, 0xD0, 0xFF, 0x25, 0, 0, 0, 0]);         // call rax (the formula's frame has the shadow space); jmp [rip+0]
        code.AddRange(BitConverter.GetBytes((ulong)epilogue));
        code.ToArray().CopyTo(new Span<byte>(stub, 32));
        byte[] jump = [0xE9, 0, 0, 0, 0, 0x90, 0x90];
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(at, jump);
        TalkEffectAt = at;
        TalkEffectStub = (nint)stub;
    }

    [UnmanagedCallersOnly]
    private static void TalkEffect()
    {
        try
        {
            var effect = StatOrStatus(*_battle.TargetResult, _battle.Ability);
            Trace(0x2A, $"ability 0x{*_battle.AbilityId:X3}, {effect}");
        }
        catch (Exception e)
        {
            Failed(nameof(TalkEffect), e);
        }
    }

    /// <summary>
    /// ReMixed's stat change (PS1 0x80186CA4, past its Maintenance check): the result becomes a
    /// stat change (flags 0x01), and each stat in <paramref name="mask"/> (bit 0 Speed, 1 CT,
    /// 2 PA, 3 MA, 4 Brave, 5 Faith, 6 the byte after) changes by <paramref name="amount"/>.
    /// </summary>
    internal static void StatChange(byte* result, int mask, int amount)
    {
        result[Battle.ResultFlags] = 0x01;
        for (var k = 0; k < 7; k++)
            if ((mask & (1 << k)) != 0)
                result[Battle.ResultStatChanges + k] = (byte)amount;
    }

    /// <summary>
    /// 0x18, ReMixed's (PS1 0x80152A30): vanilla formula 0x4E, then a knockback
    /// attempt, which the remaster only makes for single-target actions. Wind Soul.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void WindWithKnockback()
    {
        try
        {
            _r.Formula4E();
            _r.Knockback();
            Trace(0x18, $"hit {**_battle.TargetResult}, HP damage {Result(Battle.HpDamage)}");
        }
        catch (Exception e)
        {
            Failed(nameof(WindWithKnockback), e);
        }
    }

    /// <summary>
    /// 0x27, ReMixed's Gil Taking (PS1 0x80151480). For an ability flagged as a
    /// weapon strike (ability flags byte 1 & 0x04: "Shine Lover", 0x133) it's a
    /// weapon attack that also steals gil from an enemy: no evasion for guns, the
    /// steal if attacker and target are on different teams, then the weapon chain
    /// and the statuses, and both results flagged as a gil change. Otherwise
    /// (Gil Taking, 0x06C) it checks evasion, then ReMixed's steal setup (PS1
    /// 0x80189828, rewritten in place from vanilla's Gil Taking): XA = Speed, YA = X,
    /// doubled for a steal made as a reaction (Counter Steal), plus 10 with Banditry
    /// (0x1CF); then the hit roll and the steal.
    /// </summary>
    /// <remarks>
    /// Left out: ReMixed's weapon path first works out whether the attack comes
    /// from the front, side or back (PS1 0x80185588). Only evasion uses that, and
    /// only guns skip the evasion check that does it anyway.
    /// </remarks>
    [UnmanagedCallersOnly]
    private static void GilTaking()
    {
        try
        {
            var ability = _battle.Ability;
            var attacker = *_battle.Attacker;
            if ((ability[3] & 0x04) == 0)
            {
                var steals = 0;
                if (_r.Evade() == 0)
                {
                    // ReMixed's steal setup (0x80189828; formulas 0x26's and 0x28's too, FormulaSteps): X doubled
                    // in the battle loop's after-action phase (0x80096214 = 2: Counter Steal), here for a stealer
                    // that isn't the unit whose turn it is; then 10 more with Banditry.
                    *_battle.XA = attacker[Battle.UnitSpeed];
                    var x = ability[9] * (attacker[Battle.UnitTurn] == 0 ? 2 : 1);
                    *_battle.YA = (short)(x + ((attacker[Battle.UnitSupports + 1] & 0x40) != 0 ? 10 : 0));
                    if (_r.GilRoll() == 0)
                    {
                        _r.StealGil();
                        steals++;
                    }
                }
                var result = *_battle.TargetResult;
                if (result[0] == 0 && *_r.Simulating == 0)
                    *(ushort*)(result + Battle.ResultFlags2) |= 0x2000; // the remaster's miss marker
                Trace(0x27, $"Gil Taking, XA {*_battle.XA}, YA {*_battle.YA}, steals {steals}, hit {result[0]}");
                return;
            }
            if (_r.GetItem(*_r.WeaponItemId)[5] != 0x0A && _r.Evade() != 0) // guns aren't evaded
                return;
            var enemy = (((*_r.Target)[Battle.UnitTeam] ^ attacker[Battle.UnitTeam]) & 0x30) != 0;
            if (enemy)
                _r.StealGil();
            if (_r.WeaponChain() == 0)
                _r.Statuses();
            (*_battle.TargetResult)[Battle.ResultFlags] |= 0x01; // gil changed
            (*_r.AttackerResult)[Battle.ResultFlags] |= 0x01;
            Trace(0x27, $"weapon strike, stole from an enemy {enemy}, HP damage {Result(Battle.HpDamage)}");
        }
        catch (Exception e)
        {
            Failed(nameof(GilTaking), e);
        }
    }

    /// <summary>
    /// ReMixed's formula 0x05 (PS1 0x80188C9C), for the gun skills and Charge (<see cref="GunSkills"/>, which sends
    /// 0x190–0x19D here): the weapon's own formula (0x8015E780: 1 for a weapon with none, and 1 for one that casts its
    /// spell when the skill has a status set), through its table entry, then Hook Shot's pull after a hit (0x80161080),
    /// then the skill's status set through the status step.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void GunShot()
    {
        var ability = *_battle.AbilityId;
        var status = GunSkills.StatusSet(ability);
        int formula = _battle.Weapon[2];
        if (formula == 0 || formula == 5 || (status > 0 && formula == 2))
            formula = 1;
        Trace(0x05, $"ability 0x{ability:X3}: the weapon's formula 0x{formula:X2}{(status > 0 ? $", then status set 0x{status:X2}" : "")}");
        ((delegate* unmanaged<void>)_battle.FormulaTable[formula - 1])();
        try
        {
            if (ability == GunSkills.HookShot && (*_battle.TargetResult)[0] != 0 && Knockback.Pull())
                Trace(0x05, "Hook Shot pulls the target");
        }
        catch (Exception e)
        {
            Failed(nameof(GunShot), e);
        }
        if (status <= 0)
            return;
        try
        {
            *_code.StatusId = (byte)status;
            new ReadOnlySpan<byte>(_code.StatusSets + 6 * status, 6).CopyTo(new Span<byte>(_code.StatusCopy, 6));
        }
        catch (Exception e)
        {
            Failed(nameof(GunShot), e);
            return;
        }
        _r.Statuses();
    }

    /// <summary>
    /// 0x21 as ReMixed changed it (Bizen Boat; PS1 0x801895F4): vanilla's MA × Y as MP damage, but through the HP damage
    /// step first (0x80186568), so the damage rules apply to it, then moved to MP. ReMixed also takes the katana break
    /// roll out of the formula for its action-stage check; the game's roll stays here, with <see cref="KatanaBreak"/>'s
    /// conditions. ReMixed's version stores the damage step's return value with 0x20 as the result's flags (the
    /// damage's low bits get in); this keeps vanilla's 0x20, MP damage.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void BizenBoat()
    {
        try
        {
            var original = Original[0x21];
            ((delegate* unmanaged<void>)GameImage.Rip(original + 4, 1, 5))(); // the katana break roll (0x30655C)
            *_battle.XA = (*_battle.Attacker)[Battle.UnitMa];
            *_battle.YA = _battle.Ability[10];
            _r.MagicMod1();
            _r.MagicMod2();
            _r.MagicMod3();
            _r.MagicMod4();
            _r.HpDamage();
            var result = *_battle.TargetResult;
            *(ushort*)(result + Battle.MpDamage) = *(ushort*)(result + Battle.HpDamage);
            *(ushort*)(result + Battle.HpDamage) = 0;
            result[Battle.ResultFlags] = 0x20;
            Trace(0x21, $"XA {*_battle.XA}, YA {*_battle.YA}, MP -{Result(Battle.MpDamage)}");
        }
        catch (Exception e)
        {
            Failed(nameof(BizenBoat), e);
        }
    }

    /// <summary>
    /// 0x1E as ReMixed changed it (PS1 0x80189434, now ending in 0x80152B90): vanilla's magic
    /// damage with the elemental steps, then, for a target on the attacker's team (+0x1EE),
    /// the HP damage heals instead and no status lands. Heaven Thunder, Asura, Diamond Sword,
    /// Hydragon Pit, Space Storage, Sky Demon and Holy Bracelet.
    /// </summary>
    /// <remarks>
    /// For other targets ReMixed's tail runs the status step once more, hit or not (through
    /// 0x801895AC), as here.
    /// </remarks>
    [UnmanagedCallersOnly]
    private static void SameTeamHeal()
    {
        ((delegate* unmanaged<void>)Original[0x1E])();
        try
        {
            var attacker = *_battle.Attacker;
            var target = *_r.Target;
            if (((attacker[Battle.UnitTeam] ^ target[Battle.UnitTeam]) & 0x30) != 0)
            {
                _r.Statuses();
                return;
            }
            var damage = Result(Battle.HpDamage);
            HealInstead(*_battle.TargetResult);
            Trace(0x1E, $"same team: HP damage {damage} heals instead");
        }
        catch (Exception e)
        {
            Failed(nameof(SameTeamHeal), e);
        }
    }

    /// <summary>A result's HP damage becomes HP healing, and its statuses to add are dropped (flags: no damage, no status change, healing).</summary>
    internal static void HealInstead(byte* result)
    {
        *(ushort*)(result + Battle.HpHeal) = *(ushort*)(result + Battle.HpDamage);
        *(ushort*)(result + Battle.HpDamage) = 0;
        for (var i = 0; i < 5; i++)
            result[Battle.ResultStatusAdded + i] = 0;
        result[Battle.ResultFlags] = (byte)((result[Battle.ResultFlags] & 0x77) | 0x40);
    }

    /// <summary>
    /// 0x58 as ReMixed rewrote it (PS1 0x8018A824, 0x8015D300): a magic hit
    /// check first; then with Y = 0 vanilla's formula (Moldball Virus), and
    /// otherwise the usual status chance and roll, then the target's HP and MP
    /// swap places (HP damage and MP healing of the difference, or the other way
    /// round). Death Sentence's slot.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void HpMpSwap()
    {
        try
        {
            if (_r.MagicHit() != 0)
                return;
            var ability = _battle.Ability;
            if (ability[10] == 0)
            {
                _r.Formula58();
                return;
            }
            *_battle.XA = (*_battle.Attacker)[Battle.UnitMa];
            *_battle.YA = ability[9];
            _r.AfterMagicXa();
            _r.MagicMod1();
            _r.MagicMod4();
            _r.StatusChance();
            _r.StatusRoll();
            var target = *_r.Target;
            var result = *_battle.TargetResult;
            var fields = (ushort*)(result + Battle.HpDamage); // HP damage, HP heal, MP damage, MP heal
            var difference = *(ushort*)(target + Battle.UnitHp) - *(ushort*)(target + Battle.UnitMp);
            if (difference > 0)
            {
                fields[0] = (ushort)difference;
                fields[3] = (ushort)difference;
                result[Battle.ResultFlags] = 0x90;
            }
            else
            {
                fields[1] = (ushort)-difference;
                fields[2] = (ushort)-difference;
                result[Battle.ResultFlags] = 0x60;
            }
            Trace(0x58, $"hit {result[0]}, HP - MP = {difference}");
        }
        catch (Exception e)
        {
            Failed(nameof(HpMpSwap), e);
        }
    }

    /// <summary>
    /// 0x24, Elemental with ReMixed's Geomancy by terrain (PS1 0x801896B4 with the
    /// call at 0x801896CC): magic hit check, then the terrain picks the ability's
    /// status set, element and visual effect (<see cref="Geomancy"/>), then
    /// vanilla's XA = MA, YA = (PA + Y) / 2 and the rest of the formula.
    /// ReMixed's four Geomancy abilities, and Blizzard.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void GeomancyByTerrain()
    {
        try
        {
            if (_r.MagicHit() != 0)
                return;
            var terrain = _geomancy!.Apply(_battle);
            var unit = *_battle.Attacker;
            *_battle.XA = unit[Battle.UnitMa];
            *_battle.YA = (short)((unit[Battle.UnitPa] + _battle.Ability[10]) >> 1);
            _r.ElementalRest();
            Trace(0x24, $"{terrain}; XA {*_battle.XA}, YA {*_battle.YA}, hit {**_battle.TargetResult}, "
                        + $"HP damage {Result(Battle.HpDamage)}");
        }
        catch (Exception e)
        {
            Failed(nameof(GeomancyByTerrain), e);
        }
    }

    /// <summary>
    /// 0x2C as ReMixed rewrote it (PS1 0x80189AD8, via 0x801529C0 and 0x8018AD58):
    /// the attacker makes a plain weapon attack (ability 0) inside this action,
    /// and what it does to HP goes to MP instead. Magic Break.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void MagicBreak()
    {
        try
        {
            SubAttack();
            var result = *_battle.TargetResult;
            var fields = (ushort*)(result + Battle.HpDamage); // HP damage, HP heal, MP damage, MP heal
            fields[2] = fields[0];
            fields[3] = fields[1];
            fields[0] = 0;
            fields[1] = 0;
            var flags = result[Battle.ResultFlags];
            result[Battle.ResultFlags] = (byte)(((flags & 0xC0) >> 2) | (flags & 0x3F)); // HP flags -> MP flags
            Trace(0x2C, $"hit {result[0]}, MP damage {fields[2]}, MP heal {fields[3]}, flags 0x{result[Battle.ResultFlags]:X2}");
        }
        catch (Exception e)
        {
            Failed(nameof(MagicBreak), e);
        }
    }

    /// <summary>
    /// The attacker's plain attack as a sub-action of the current one (PS1
    /// 0x8018AD58), as the remaster's formula 0x25 does it: queue ability 0 on
    /// the attacker's action record, compute that action, then put the record
    /// and the shared globals back. It replaces the ability and weapon globals
    /// with the attack's.
    /// </summary>
    private static void SubAttack()
    {
        var unit = *_battle.Attacker;
        var saved = *_r.SubActionSaved;
        var type = unit[Battle.UnitAction + 1];
        var ability = *(ushort*)(unit + Battle.UnitAction + 2);
        unit[Battle.UnitAction + 1] = 1;
        *(ushort*)(unit + Battle.UnitAction + 2) = 0;
        _r.ActionRoutine(unit + Battle.UnitAction, *_r.SubActionParam);
        unit = *_battle.Attacker;
        *_r.SubActionSaved = saved;
        *(ushort*)(unit + Battle.UnitAction + 2) = ability;
        unit[Battle.UnitAction + 1] = type;
        *_r.SubActionCleared = 0;
    }

    // ---------------------------------------------------------------- formulas rewritten in place (read 2026-11-12)

    /// <summary>
    /// 0x25, the Knight's Breaks (Head, Armor, Shield and Weapon Break; PS1 0x801896EC, rewritten in place): the
    /// game's own (the remaster's 0x307E74), which picks the slot, breaks the item or attacks plainly when there's
    /// nothing to break, as ReMixed's does. ReMixed adds one thing: a break that Momentum helped (its +10 XA with a melee
    /// weapon, <see cref="Supports"/>' attack modifiers; PS1 0x80151940) knocks the target back (0x801843EC).
    /// </summary>
    /// <remarks>
    /// ReMixed also reads the slot from the ability's "status" byte (its data: 1 weapon, 2 shield, 3 head, 4 body, 5
    /// accessory) where vanilla and the remaster go by the ability's id; for the four Breaks the two agree. It then
    /// clears the action's status bytes (its common tail, 0x8018BAF8), as neither game's 0x25 runs a status step.
    /// </remarks>
    [UnmanagedCallersOnly]
    private static void MomentumBreak()
    {
        Supports.MomentumBreak = false;
        ((delegate* unmanaged<void>)Original[0x25])();
        try
        {
            var result = *_battle.TargetResult;
            if (!Supports.MomentumBreak || result[0] == 0 || *(ushort*)(result + Battle.ResultFlags2) != 4)
                return;
            _r.Knockback();
            Trace(0x25, $"a break with Momentum's bonus knocks back: flags 0x{*(ushort*)(result + Battle.ResultFlags2):X4}");
        }
        catch (Exception e)
        {
            Failed(nameof(MomentumBreak), e);
        }
    }

    /// <summary>
    /// Formula 0x32's steps between its XA and YA and its zodiac, from the game's own 0x32: the attack's support step
    /// (Attack UP and Martial Arts; PS1 0x8018614C), the attacker's statuses (0x80186254, with Brave's rule), the
    /// target's Defense UP (0x801862CC) and the target's statuses (0x8018636C). Swapped by the offline check.
    /// </summary>
    internal static delegate* unmanaged<void>[] FistSteps = new delegate* unmanaged<void>[4];

    /// <summary>The count of strikes Repeating Fist shows (PS1 0x801938EA). (0x7B078D)</summary>
    internal static byte* FistHits { get; private set; }

    /// <summary>Formula 0x4B's own routine (Phoenix Down's), for 0x3C on a dead target.</summary>
    internal static delegate* unmanaged<void> PhoenixDown;

    private static void FindFistSteps(nint formula)
    {
        // Evade; XA = PA, YA = (PA + Y) / 2; four steps; the zodiac, the critical hit, one strike (XA + YA);
        // a random number × X for the strikes, into the count.
        var calls = GameImage.Calls(formula,
            "48 83 EC 28 E8 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 0F B6 41 3E 66 89 05 ?? ?? ?? ?? " +
            "0F B6 51 3E 0F B6 05 ?? ?? ?? ?? 03 D0 D1 EA 66 89 15 ?? ?? ?? ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? " +
            "E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? " +
            "0F B6 0D ?? ?? ?? ?? 0F AF C1 99 81 E2 FF 7F 00 00 03 C2 48 8B 15 ?? ?? ?? ?? C1 F8 0F FE C0 0F B6 C8 " +
            "88 0D ?? ?? ?? ??",
            "formula 0x32");
        if (calls[0] != (nint)_r.Evade || calls[5] != (nint)_r.MagicMod4 || calls[6] != (nint)_r.Critical
            || calls[7] != (nint)_r.StatusChance || calls[8] != (nint)_r.Random)
            throw new InvalidDataException("formula 0x32's steps aren't the ones this DLL expects");
        if ((byte**)GameImage.Rip(formula + 17, 3, 7) != _battle.Attacker || (short*)GameImage.Rip(formula + 28, 3, 7) != _battle.XA
            || (byte*)GameImage.Rip(formula + 39, 3, 7) != _battle.Ability + 10
            || (short*)GameImage.Rip(formula + 50, 3, 7) != _battle.YA
            || (byte*)GameImage.Rip(formula + 97, 3, 7) != _battle.Ability + 9
            || (byte**)GameImage.Rip(formula + 116, 3, 7) != _battle.TargetResult)
            throw new InvalidDataException("formula 0x32's globals aren't the ones this DLL expects");
        for (var k = 0; k < 4; k++)
            FistSteps[k] = (delegate* unmanaged<void>)calls[1 + k];
        FistHits = (byte*)GameImage.Rip(formula + 131, 2, 6);
    }

    /// <summary>
    /// 0x32, Repeating Fist (PS1 0x80189D74, rewritten in place): as vanilla, one strike is XA + YA (XA = PA, YA =
    /// (PA + Y) / 2 through the attack's steps, the zodiac and the critical hit) and the strikes 1 to X; ReMixed then
    /// takes the total through the HP damage step (XA the strikes, YA one strike), so its damage rules (Power Modes,
    /// Adeptness, Vehemence) apply, where vanilla and the remaster multiply it themselves.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void RepeatingFist()
    {
        try
        {
            if (_r.Evade() != 0)
                return;
            var unit = *_battle.Attacker;
            var ability = _battle.Ability;
            *_battle.XA = unit[Battle.UnitPa];
            *_battle.YA = (short)((unit[Battle.UnitPa] + ability[10]) >> 1);
            foreach (var step in FistSteps)
                step();
            _r.MagicMod4();
            _r.Critical();
            _r.StatusChance(); // one strike: XA + YA, the zodiac on YA
            var strike = (short)Result(Battle.HpDamage);
            var hits = (byte)((_r.Random() * ability[9] >> 15) + 1);
            *FistHits = hits;
            *_battle.XA = hits;
            *_battle.YA = strike;
            _r.HpDamage();
            Trace(0x32, $"{hits} strikes of {strike}, HP damage {Result(Battle.HpDamage)}");
        }
        catch (Exception e)
        {
            Failed(nameof(RepeatingFist), e);
        }
    }

    /// <summary>
    /// 0x3C, ReMixed's (PS1 0x80186DBC, vanilla's Wish rewritten in place; the remaster's 0x30609C is vanilla's): on a
    /// dead target it runs Phoenix Down's formula (0x4B), whose hit check misses unless the ability's statuses revive.
    /// Otherwise, with Y 0 (Wish, Energy) the attacker gives up Max HP / 5 (its own result) and the target gets back
    /// 2/5 of the attacker's Max HP; with a Y (Lava Ball, which ReMixed made a self-heal: Y 1) the target gets back the
    /// attacker's Max HP / 5 for nothing. Then the ability's statuses (Lava Ball's cure several). Vanilla had no dead
    /// check, the fixed cost and no statuses.
    /// </summary>
    /// <remarks>
    /// The fifths are ReMixed's: Max HP × 4/5 (a multiply by 0xCCCCCCCD), then a quarter or a half of that. ReMixed
    /// stores the heal as a byte (0x80186E08), a slip that would wrap past 255 HP; this stores all of it.
    /// </remarks>
    [UnmanagedCallersOnly]
    private static void SacrificeHeal()
    {
        try
        {
            var target = *_r.Target;
            if ((target[Battle.UnitStatuses] & 0x20) != 0) // Dead
            {
                PhoenixDown();
                Trace(0x3C, $"a dead target: Phoenix Down's formula, hit {**_battle.TargetResult}");
                return;
            }
            var attacker = *_battle.Attacker;
            var fourFifths = (uint)((ulong)*(ushort*)(attacker + Battle.UnitMaxHp) * 0xCCCCCCCD >> 32);
            var heal = fourFifths >> 2;
            var cost = 0u;
            if (_battle.Ability[10] == 0)
            {
                cost = heal;
                Recoil((ushort)cost);
                heal = fourFifths >> 1;
            }
            var result = *_battle.TargetResult;
            *(ushort*)(result + Battle.HpHeal) = (ushort)heal;
            result[Battle.ResultFlags] = 0x40;
            _r.Statuses();
            Trace(0x3C, $"heals {heal}, costs the attacker {cost}, statuses 0x{_battle.Ability[11]:X2}");
        }
        catch (Exception e)
        {
            Failed(nameof(SacrificeHeal), e);
        }
    }

    /// <summary>
    /// 0x40, Seal Evil (PS1 0x8018A088, rewritten in place): as vanilla, XA = Speed and YA = X through the attack
    /// modifiers, a status-type chance and roll, then on a hit its statuses (Petrify); ReMixed lets it reach a target
    /// with any status of the set its Y names (Y 6: Undead or Blood Suck; ReMixed's 0x80155A40) where vanilla and the
    /// remaster want Undead. A target with none is a miss.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void SealEvil()
    {
        try
        {
            var ability = _battle.Ability;
            *_battle.XA = (*_battle.Attacker)[Battle.UnitSpeed];
            *_battle.YA = ability[9];
            _r.AttackMods();
            _r.StatusChance();
            _r.StatusRoll();
            var result = *_battle.TargetResult;
            if (result[0] == 0)
                return;
            var set = _code.StatusSets + 6 * (ability[10] & 0x0F) + 1;
            var statuses = *_r.Target + Battle.UnitStatuses;
            var reached = false;
            for (var k = 0; k < 5; k++)
                reached |= (set[k] & statuses[k]) != 0;
            if (reached)
                _r.StatusSet();
            else
            {
                result[0] = 0;
                result[2] = 7;
                *(ushort*)(result + Battle.ResultStatusChance) = 0;
            }
            Trace(0x40, $"XA {*_battle.XA}, YA {*_battle.YA}, set 0x{ability[10] & 0x0F:X} {(reached ? "reached" : "not on the target")}, "
                        + $"hit {result[0]}");
        }
        catch (Exception e)
        {
            Failed(nameof(SealEvil), e);
        }
    }

    /// <summary>
    /// 0x13, ReMixed's sacrifice strike (PS1 0x80155B08): XA = PA, YA = MA,
    /// adjusted by weapon type, then the attack modifiers and the magic finish.
    /// On a hit the target takes another Max HP / 3 of the attacker's, and the
    /// attacker takes Max HP / 3 itself. Ability 0x0DB.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void SacrificeStrike()
    {
        try
        {
            if (_r.Evade() != 0)
                return;
            var unit = *_battle.Attacker;
            *_battle.XA = unit[Battle.UnitPa];
            *_battle.YA = unit[Battle.UnitMa];
            AdjustForWeaponType();
            _r.AfterMagicXa();
            _r.AttackMods();
            _r.HpDamage();
            _r.MagicFinish();
            _r.MagicElement();
            var result = *_battle.TargetResult;
            if (result[0] == 0)
                return;
            var before = Result(Battle.HpDamage);
            var share = (ushort)(*(ushort*)(unit + Battle.UnitMaxHp) / 3);
            *(ushort*)(result + Battle.HpDamage) = (ushort)Math.Min(before + share, 0x7FFF);
            Recoil(share);
            _r.ApplyHit();
            _r.Statuses();
            Trace(0x13, $"XA {*_battle.XA}, YA {*_battle.YA}, HP damage {before} + {share}, recoil {share}");
        }
        catch (Exception e)
        {
            Failed(nameof(SacrificeStrike), e);
        }
    }

    /// <summary>
    /// 0x39, ReMixed's ranged strike (PS1 0x80155BCC): XA = PA, YA = Y + WP,
    /// adjusted by weapon type, YA falling off with distance, then the weapon
    /// chain. On a hit the attacker takes Max HP / 5. Ability 0x0B8.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void FalloffStrike()
    {
        try
        {
            if (_r.Evade() != 0)
                return;
            var unit = *_battle.Attacker;
            var ability = _battle.Ability;
            var weapon = _battle.Weapon;
            *_battle.XA = unit[Battle.UnitPa];
            *_battle.YA = (short)(ability[10] + weapon[4]);
            AdjustForWeaponType();
            var full = *_battle.YA;
            Falloff(ability[9]);
            _r.AfterWeaponXa();
            _r.AttackMods();
            _r.Critical();
            _r.HpDamage();
            _r.Element(weapon[6]);
            if (**_battle.TargetResult == 0)
                return;
            var recoil = (ushort)Math.Max(*(short*)(unit + Battle.UnitMaxHp) / 5, 0);
            Recoil(recoil);
            _r.ApplyHit();
            _r.Statuses();
            Trace(0x39, $"XA {*_battle.XA}, YA {full} -> {*_battle.YA} (X {ability[9]}), "
                        + $"HP damage {Result(Battle.HpDamage)}, recoil {recoil}");
        }
        catch (Exception e)
        {
            Failed(nameof(FalloffStrike), e);
        }
    }

    /// <summary>
    /// ReMixed's weapon-type adjustment (PS1 0x80155D20): with a Knight Sword or
    /// Katana XA × Brave / 100, with an Axe XA × (100 − Faith) / 100.
    /// </summary>
    private static void AdjustForWeaponType()
    {
        var type = _r.GetItem(*_r.WeaponItemId)[5];
        var xa = (byte)*_battle.XA; // ReMixed reads XA as a byte
        if (type is 4 or 5)
            *_battle.XA = (short)(xa * (*_battle.Attacker)[Battle.UnitBrave] / 100);
        else if (type == 6)
            *_battle.XA = (short)(xa * (100 - *_r.AttackerFaith) / 100);
    }

    /// <summary>
    /// ReMixed's distance falloff (PS1 0x80155DA0): YA × (16 − (X + 1) × (d − 1)) / 16,
    /// d = the larger of the X and Y distances. Below zero it stops at 0 (ReMixed's
    /// unsigned shift would wrap; ability 0x0B8's range keeps it above).
    /// </summary>
    private static void Falloff(byte x)
    {
        var a = *_battle.Attacker;
        var t = *_r.Target;
        var d = Math.Max(Math.Abs(a[Battle.UnitX] - t[Battle.UnitX]), Math.Abs(a[Battle.UnitY] - t[Battle.UnitY]));
        if (d <= 1)
            return;
        int ya = *_battle.YA;
        *_battle.YA = (short)Math.Max((ya * 16 - ya * (x + 1) * (d - 1)) >> 4, 0);
    }

    /// <summary>The attacker takes <paramref name="damage"/>, through its own result struct (as Chant does).</summary>
    private static void Recoil(ushort damage)
    {
        var own = *_r.AttackerResult;
        own[0] = 1;
        *(ushort*)(own + Battle.HpDamage) = damage;
        own[Battle.ResultFlags] = 0x80;
    }

    private static ushort Result(int field) => *(ushort*)(*_battle.TargetResult + field);

    private static void Trace(int formula, string details) =>
        Tracer.Line(_log, $"formula 0x{formula:X2}, ability 0x{*_battle.AbilityId:X3}: {details}");

    /// <summary>HP damage × X / 16 (ReMixed 0x8015241C); X = 0 leaves it alone.</summary>
    private static void ScaleHpDamage(byte x)
    {
        if (x == 0)
            return;
        var damage = (ushort*)(*_battle.TargetResult + Battle.HpDamage);
        *damage = (ushort)(*damage * x >> 4);
    }

    /// <summary>
    /// The remaster's own Two Hands rule, from its weapon-attack setup (0x309ED1):
    /// the weapon allows two hands, the unit has Two Hands, and one hand is free.
    /// The setup only runs it for plain attacks, so skills set it themselves.
    /// </summary>
    private static void ApplyTwoHands()
    {
        if ((_battle.Weapon[1] & 0x04) == 0)
            return;
        var unit = *_battle.Attacker;
        if ((unit[Battle.UnitSupports + 2] & 0x02) == 0) // Two Hands
            return;
        var hands = (ushort*)(unit + 0x20);
        static bool Free(ushort item) => item == 0xFF || item >= 0x105;
        if ((Free(hands[0]) && Free(hands[1])) || (Free(hands[2]) && Free(hands[3])))
            *_battle.TwoHands = 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string formula, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _log.Error($"formulas: {formula} failed: {e}");
    }
}
