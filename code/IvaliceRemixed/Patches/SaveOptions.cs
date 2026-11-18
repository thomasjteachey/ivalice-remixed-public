using static IvaliceRemixed.Options;

namespace IvaliceRemixed.Patches;

/// <summary>
/// Each save's own options (asked 2026-11-11), kept where ReMixed keeps its Extra Options (docs/MECHANICS.md, "Where the
/// options live"), so the save carries them: event flags 0xD6–0xF9, the bytes of vars 0x14 and 0x16, and var 0x40's
/// bit 0x100000 (the Nemesis System off). The remaster and vanilla's events never touch them (checked 2026-11-11). The
/// first time the mod meets a save without them (var 0x14's bit 0x10000000 clear: a new game, or a save from before),
/// the mod's settings (Config.json, <see cref="Options.Settings"/>) go in as its own, as ReMixed's options event sets
/// its defaults the first time it runs; from then on <see cref="Options.Current"/> follows the save in play, checked
/// on every world-map frame and at each battle's start. The extra options menu changes them in the save. The settings
/// with no place in a save (the test mode, separate saves, the Argath campaign) stay the mod's.
/// </summary>
internal static unsafe class SaveOptions
{
    internal const int Rates = 0x14, Powers = 0x16, NemesisWord = 0x40;
    internal const int Marker = 0x10000000, NemesisOff = 0x100000;
    internal const int RefundNone = 0xD6, RefundHalf = 0xD7, BodiesCrystal = 0xDA, BodiesTreasure = 0xDB,
        EntireRoster = 0xDC, Revive = 0xDD, GuestsAlways = 0xDE, BraveryVanilla = 0xDF, Zodiac = 0xE2,
        FixedScale = 0xE4, PoachVanilla = 0xE5, RangeOf3 = 0xE7, JobLevel = 0xE8, TwoJobs = 0xEA, BackVanilla = 0xEB,
        BackEveryone = 0xEC, KnockbackVanilla = 0xED, PoisonMp = 0xF2, Renew = 0xF3, StartTen = 0xF4, Instant = 0xF5,
        Rare = 0xF6, Sexual = 0xF8;
    /// <summary>The flags, each with the next one for the two-flag settings (Zodiac, job levels, sexuality).</summary>
    internal static readonly int[] Flags =
    [
        RefundNone, RefundHalf, BodiesCrystal, BodiesTreasure, EntireRoster, Revive, GuestsAlways, BraveryVanilla, Zodiac,
        Zodiac + 1, FixedScale, PoachVanilla, RangeOf3, JobLevel, JobLevel + 1, TwoJobs, BackVanilla, BackEveryone,
        KnockbackVanilla, PoisonMp, Renew, StartTen, Instant, Rare, Sexual, Sexual + 1,
    ];
    /// <summary>ReMixed's power mode presets in var 0x16 (byte 0 the player's team, 1 the others, 2 and 3 the same for
    /// Math Skill); 0 is Knight.</summary>
    internal const uint Squire = 0x4B7D4B7D, Tactician = 0x7D327D4B, Chemist = 0x32323232;

    private static Log _log = null!;
    private static int** _words;
    private static (int Rates, int Powers, int Nemesis, long Flags) _seen = (-1, -1, -1, -1);
    private static int _failed;

    public static void Install(Game game)
    {
        _log = game.Log;
        if (Nemesis.GetVariable == null)
            Nemesis.FindVariables(game.Image);
        _words = (int**)GameImage.Rip((nint)Nemesis.SetVariable + 0x14, 3, 7); // mov rax, [0x7830D8]
        BattleEvents.OnStart(game, "each save's options", Sync);
    }

    /// <summary>A variable's value: the 32-bit word for one under 0x80 (read straight from the remaster's array, as
    /// its setter cuts a negative one to a byte), else the flag through the game's getter.</summary>
    internal static int Read(int id) => id < 0x80 ? (*_words)[id] : Nemesis.GetVariable(id);

    internal static void Write(int id, int value)
    {
        if (id < 0x80)
            (*_words)[id] = value;
        else
            Nemesis.SetVariable(id, value, 0);
    }

    /// <summary>Whether a save's variables can be read (a game is loaded).</summary>
    internal static bool Ready => _words != null && *_words != null;

    /// <summary>The variable block's layout, as the game's address helper (0x242984) has it: vars 0–0x7F a word each
    /// from +0, flags 0x80–0x35F a bit each from <see cref="FlagWords"/> (32 to a word), nibbles 0x360–0x3FF four bits
    /// each from <see cref="NibbleWords"/> (8 to a word); <see cref="BlockBytes"/> in all.</summary>
    internal const int FirstFlag = 0x80, FlagWords = 0x200, FirstNibble = 0x360, NibbleWords = 0x25C, Ids = 0x400,
        BlockBytes = 0x400;

    /// <summary>Where a variable is in the block: its word's offset, and the shift and mask of its bits in it (a var's
    /// whole word: mask 0).</summary>
    internal static (int At, int Shift, uint Mask) Place(int id) => id switch
    {
        < 0 or >= Ids => throw new ArgumentOutOfRangeException(nameof(id), $"no variable 0x{id:X}"),
        < FirstFlag => (id * 4, 0, 0u),
        < FirstNibble => (FlagWords + (id - FirstFlag) / 32 * 4, (id - FirstFlag) % 32, 1u),
        _ => (NibbleWords + (id - FirstNibble) / 8 * 4, (id - FirstNibble) % 8 * 4, 0xFu),
    };

    /// <summary>Writes a variable into a copy of the variable block laid out as the game's (the formation keeps one,
    /// <see cref="RevealTreasures"/>): the word of a var, or the bit of a flag or the four bits of a nibble, which the
    /// game's store (0x242700) cuts the value to.</summary>
    internal static void WriteIn(nint block, int id, int value)
    {
        var (at, shift, mask) = Place(id);
        var word = (uint*)(block + at);
        *word = mask == 0 ? (uint)value : (*word & ~(mask << shift)) | ((uint)value & mask) << shift;
    }

    /// <summary>A variable from a copy of the variable block (<see cref="WriteIn"/>).</summary>
    internal static int ReadIn(nint block, int id)
    {
        var (at, shift, mask) = Place(id);
        var word = *(uint*)(block + at);
        return mask == 0 ? (int)word : (int)((word >> shift) & mask);
    }

    /// <summary>The save in play's options in effect: its own, given the mod's settings first if it has none. On the
    /// world map's frames and at each battle's start; cheap when nothing changed.</summary>
    internal static void Sync()
    {
        try
        {
            if (_words == null || *_words == null)
                return;
            if ((Read(Rates) & Marker) == 0)
            {
                Encode(Options.Settings, Read, Write);
                _log.Info("options: the save in play had none of its own; it takes the mod's settings");
            }
            long flags = 0;
            for (var k = 0; k < Flags.Length; k++)
                flags |= (long)(Read(Flags[k]) != 0 ? 1 : 0) << k;
            var seen = (Read(Rates), Read(Powers), Read(NemesisWord) & NemesisOff, flags);
            if (seen == _seen)
                return;
            _seen = seen;
            if (Options.Use(Decode(Read, Options.Settings)))
                _log.Info($"options: the save's: {Options.Current}");
        }
        catch (Exception e)
        {
            if (Interlocked.Increment(ref _failed) <= 3)
                _log.Error($"options: reading the save's failed: {e}");
        }
    }

    /// <summary>Puts <paramref name="options"/> into the save in play (the Extra Options menu) and in effect.</summary>
    internal static void Save(Options options)
    {
        if (_words == null || *_words == null)
        {
            Options.Use(options);
            return;
        }
        Encode(options, Read, Write);
        _seen = (-1, -1, -1, -1);
        Sync();
    }

    /// <summary>The options a save keeps (<paramref name="read"/>: a variable's word, or a flag), with the ones it has no
    /// place for from <paramref name="settings"/>.</summary>
    internal static Options Decode(Func<int, int> read, Options settings)
    {
        bool On(int flag) => read(flag) != 0;
        int Two(int low) => (On(low) ? 1 : 0) | (On(low + 1) ? 2 : 0);
        var rates = read(Rates);
        var o = settings.Copy();
        o.PlayerDeaths = On(Revive) ? PlayerDeathRule.ReviveAfterBattle : PlayerDeathRule.DiePermanently;
        o.Bodies = On(BodiesCrystal) ? BodyRule.Crystal : On(BodiesTreasure) ? BodyRule.TreasureBox : BodyRule.TransformRandomly;
        o.JpGain = rates & 0xFF;
        o.ExpGain = (rates >> 8) & 0xFF;
        o.SpilloverJp = (rates >> 16) & 0xF;
        o.VictoryBonus = (VictoryBonusRule)((rates >> 20) & 3);
        o.EnemyLevelBoost = Defined((LevelBoostRule)((rates >> 24) & 3));
        o.EnemyJobs = Defined((EnemyJobRule)((rates >> 26) & 3));
        o.JpRefund = On(RefundNone) ? JpRefundRule.None : On(RefundHalf) ? JpRefundRule.Half : JpRefundRule.Full;
        o.LevelScaling = On(EntireRoster) ? LevelScalingRule.EntireRoster : LevelScalingRule.DeployedUnits;
        o.GuestControl = On(GuestsAlways) ? GuestControlRule.Always : GuestControlRule.Deployed;
        o.Bravery = On(BraveryVanilla) ? BraveryRule.Vanilla : BraveryRule.LowersWithBrave;
        o.ZodiacStrength = (ZodiacRule)Two(Zodiac);
        o.FixedLevels = On(FixedScale) ? FixedLevelRule.IfPartyLevelIsHigher : FixedLevelRule.Never;
        o.PoachingOdds = On(PoachVanilla) ? PoachRule.Vanilla : PoachRule.ByBrave;
        o.WeaponRange = On(RangeOf3) ? WeaponRangeRule.MinimumOf3 : WeaponRangeRule.NoMinimum;
        o.JobLevels = (JobLevelRule)Two(JobLevel);
        o.JobStats = On(TwoJobs) ? JobStatRule.JobAndSecondary : JobStatRule.JobOnly;
        o.BackAttacks = On(BackEveryone) ? BackAttackRule.Everyone : On(BackVanilla) ? BackAttackRule.Vanilla
            : BackAttackRule.BanditryAndMug;
        o.Knockback = On(KnockbackVanilla) ? KnockbackRule.Vanilla : KnockbackRule.Everyone;
        o.PoisonAndRegen = On(PoisonMp) ? PoisonRegenRule.HpAndMp : PoisonRegenRule.HpOnly;
        o.AutoRenew = On(Renew);
        o.StartingMp = On(StartTen) ? StartingMpRule.Ten : StartingMpRule.Full;
        o.EveryoneNonCharge = On(Instant);
        o.RareEquipment = On(Rare) ? RareEquipmentRule.Allowed : RareEquipmentRule.LeftOut;
        o.Sexuality = (SexualityRule)Two(Sexual);
        var powers = (uint)read(Powers);
        int Percent(int shift)
        {
            var p = (int)((powers >> shift) & 0xFF);
            return p != 0 ? p : 100; // 0 leaves the damage as it is
        }
        o.PowerMode = powers switch
        {
            0 => PowerModeRule.Knight,
            Squire => PowerModeRule.Squire,
            Tactician => PowerModeRule.Tactician,
            Chemist => PowerModeRule.Chemist,
            _ => PowerModeRule.Custom,
        };
        if (o.PowerMode == PowerModeRule.Custom)
            (o.AllyPower, o.EnemyPower, o.AllyMathPower, o.EnemyMathPower) = (Percent(0), Percent(8), Percent(16), Percent(24));
        o.NemesisSystem = (read(NemesisWord) & NemesisOff) == 0;
        return o;
    }

    private static T Defined<T>(T value) where T : struct, Enum => Enum.IsDefined(value) ? value : default;

    /// <summary>The options into a save's places (<paramref name="write"/>: a word, or a flag as 0 or 1), marking
    /// them as its own; var 0x40's other bits (the Nemesis itself) stay.</summary>
    internal static void Encode(Options o, Func<int, int> read, Action<int, int> write)
    {
        void Flag(int flag, bool on) => write(flag, on ? 1 : 0);
        void Two(int low, int index)
        {
            Flag(low, (index & 1) != 0);
            Flag(low + 1, (index & 2) != 0);
        }
        Flag(Revive, o.PlayerDeaths == PlayerDeathRule.ReviveAfterBattle);
        Flag(BodiesCrystal, o.Bodies == BodyRule.Crystal);
        Flag(BodiesTreasure, o.Bodies == BodyRule.TreasureBox);
        write(Rates, Marker | ((int)o.EnemyJobs & 3) << 26 | ((int)o.EnemyLevelBoost & 3) << 24
                     | ((int)o.VictoryBonus & 3) << 20 | (o.SpilloverJp & 0xF) << 16 | (o.ExpGain & 0xFF) << 8
                     | (o.JpGain & 0xFF));
        Flag(RefundNone, o.JpRefund == JpRefundRule.None);
        Flag(RefundHalf, o.JpRefund == JpRefundRule.Half);
        Flag(EntireRoster, o.LevelScaling == LevelScalingRule.EntireRoster);
        Flag(GuestsAlways, o.GuestControl == GuestControlRule.Always);
        Flag(BraveryVanilla, o.Bravery == BraveryRule.Vanilla);
        Two(Zodiac, (int)o.ZodiacStrength);
        Flag(FixedScale, o.FixedLevels == FixedLevelRule.IfPartyLevelIsHigher);
        Flag(PoachVanilla, o.PoachingOdds == PoachRule.Vanilla);
        Flag(RangeOf3, o.WeaponRange == WeaponRangeRule.MinimumOf3);
        Two(JobLevel, (int)o.JobLevels);
        Flag(TwoJobs, o.JobStats == JobStatRule.JobAndSecondary);
        Flag(BackVanilla, o.BackAttacks == BackAttackRule.Vanilla);
        Flag(BackEveryone, o.BackAttacks == BackAttackRule.Everyone);
        Flag(KnockbackVanilla, o.Knockback == KnockbackRule.Vanilla);
        Flag(PoisonMp, o.PoisonAndRegen == PoisonRegenRule.HpAndMp);
        Flag(Renew, o.AutoRenew);
        Flag(StartTen, o.StartingMp == StartingMpRule.Ten);
        Flag(Instant, o.EveryoneNonCharge);
        Flag(Rare, o.RareEquipment == RareEquipmentRule.Allowed);
        Two(Sexual, (int)o.Sexuality);
        write(Powers, (int)Power(o));
        var nemesis = read(NemesisWord);
        write(NemesisWord, o.NemesisSystem ? nemesis & ~NemesisOff : nemesis | NemesisOff);
    }

    /// <summary>var 0x16 for the options' power mode: a preset's, or the four percentages (Custom).</summary>
    internal static uint Power(Options o) => o.PowerMode switch
    {
        PowerModeRule.Squire => Squire,
        PowerModeRule.Tactician => Tactician,
        PowerModeRule.Chemist => Chemist,
        PowerModeRule.Custom => (uint)(Math.Clamp(o.AllyPower, 1, MaxRate) | Math.Clamp(o.EnemyPower, 1, MaxRate) << 8
                                       | Math.Clamp(o.AllyMathPower, 1, MaxRate) << 16
                                       | Math.Clamp(o.EnemyMathPower, 1, MaxRate) << 24),
        _ => 0,
    };
}
