using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reloaded.Mod.Interfaces;

namespace IvaliceRemixed;

/// <summary>
/// ReMixed's Extra Options that the code mod supports, as Reloaded-II mod settings (docs/MECHANICS.md:
/// "Extra Options"). The defaults are ReMixed's, as a player meets them once its story has begun.
/// Read once at startup from Config.json in the mod's config folder; a change needs a restart.
/// </summary>
/// <remarks>
/// No game code here: Reloaded-II's launcher loads this class (through <see cref="Configurator"/>)
/// to show the settings.
/// </remarks>
public sealed class Options : IConfigurable
{
    /// <summary>Permadeath → Player Deaths (ReMixed's flag 0xDD).</summary>
    public enum PlayerDeathRule
    {
        /// <summary>The crystal or treasure, as in vanilla (ReMixed's default: its story sets flag 0xD8).</summary>
        DiePermanently,
        /// <summary>The unit freezes as a corpse and is back after the battle (flag 0xDD).</summary>
        ReviveAfterBattle,
    }

    /// <summary>Permadeath → General Deaths (ReMixed's flags 0xDA and 0xDB).</summary>
    public enum BodyRule
    {
        /// <summary>A crystal or a treasure box at random, as in vanilla.</summary>
        TransformRandomly,
        /// <summary>Always a crystal (flag 0xDA).</summary>
        Crystal,
        /// <summary>Always a treasure box (flag 0xDB).</summary>
        TreasureBox,
    }

    /// <summary>Combat Adjustments → Back Attacks (ReMixed's flags 0xEB and 0xEC, bits 0x08 and 0x10 of 0x80057929).</summary>
    public enum BackAttackRule
    {
        /// <summary>A basic attack from behind by a unit with Banditry, and Mug by anyone, is a critical hit.</summary>
        BanditryAndMug,
        /// <summary>Only vanilla's 4% critical hits.</summary>
        Vanilla,
        /// <summary>Every unit's basic attacks from behind, and Mug, are critical hits.</summary>
        Everyone,
    }

    /// <summary>Unit Levels, Stats &amp; Equipment → Job Stats (ReMixed's flag 0xEA).</summary>
    public enum JobStatRule
    {
        /// <summary>The unit's job only, unless it has Bard and Dancer's innate (0x1E5).</summary>
        JobOnly,
        /// <summary>The best of its job and its secondary skillset's job, for everyone.</summary>
        JobAndSecondary,
    }

    /// <summary>Levels → Improved Level Scaling (ReMixed's flag 0xDC).</summary>
    public enum LevelScalingRule
    {
        /// <summary>Scaling enemies follow the highest level among the deployed units (ReMixed's default).</summary>
        DeployedUnits,
        /// <summary>They follow the highest level in the roster, units away on errands left out (vanilla's, nearly).</summary>
        EntireRoster,
    }

    /// <summary>Levels → Fixed or Minimum Levels (ReMixed's flag 0xE4).</summary>
    public enum FixedLevelRule
    {
        /// <summary>A fixed level stays.</summary>
        Never,
        /// <summary>A fixed level below the party level less an eighth scales with the party instead.</summary>
        IfPartyLevelIsHigher,
    }

    /// <summary>Levels → Enemy Level Boost (ReMixed's power mode, var 0x14 byte 3 &amp; 3).</summary>
    public enum LevelBoostRule
    {
        /// <summary>No boost.</summary>
        Normal,
        /// <summary>Fixed-level enemies +49, and the party level at least 57.</summary>
        Nightmare,
        /// <summary>Fixed-level enemies +98 (so 99), and the party level at least 114.</summary>
        NoFuture,
    }

    /// <summary>Combat Adjustments → Bravery/Wariness (ReMixed's flag 0xDF).</summary>
    public enum BraveryRule
    {
        /// <summary>Under 50 Brave, a unit takes (50 − Brave)% less from physical attacks.</summary>
        LowersWithBrave,
        /// <summary>Brave doesn't change the damage a unit takes (flag 0xDF).</summary>
        Vanilla,
    }

    /// <summary>Combat Adjustments → Knockback (ReMixed's flag 0xED).</summary>
    public enum KnockbackRule
    {
        /// <summary>Immortal units and the party leader can be knocked back too, and a knockback can land in water.</summary>
        Everyone,
        /// <summary>They can't, and no knockback lands in water (flag 0xED).</summary>
        Vanilla,
    }

    /// <summary>Combat Adjustments → Minimum Weapon Range (ReMixed's flag 0xE7).</summary>
    public enum WeaponRangeRule
    {
        /// <summary>Ranged weapons can hit any tile in their range, the nearest ones too.</summary>
        NoMinimum,
        /// <summary>Vanilla: nothing within 2 tiles (flag 0xE7).</summary>
        MinimumOf3,
    }

    /// <summary>JP and EXP → JP Refund (ReMixed's flags 0xD6 and 0xD7).</summary>
    public enum JpRefundRule
    {
        /// <summary>Choosing a learned ability on the learn screen unlearns it and gives its JP cost back.</summary>
        Full,
        /// <summary>Half of the cost back (flag 0xD7).</summary>
        Half,
        /// <summary>Vanilla: a learned ability stays learned (flag 0xD6).</summary>
        None,
    }

    /// <summary>The mod's test mode (not ReMixed's).</summary>
    public enum TestModeRule
    {
        /// <summary>No recording (the default).</summary>
        Off,
        /// <summary>Record which mechanics fire, battle by battle, into a report.</summary>
        Record,
        /// <summary>Record, and let the AI play the player's units, so a battle runs on its own.</summary>
        RecordAndAutoBattle,
    }

    /// <summary>Bonus EXP &amp; JP on Victory (ReMixed's var 0x14, byte 2's high nibble).</summary>
    public enum VictoryBonusRule
    {
        /// <summary>No bonus (ReMixed's default).</summary>
        None,
        /// <summary>Ten times an action's JP base in each unit's job.</summary>
        Jp,
        /// <summary>A level for each unit.</summary>
        Exp,
        /// <summary>Both.</summary>
        Both,
    }

    /// <summary>Combat Adjustments → Guest Control (ReMixed's flag 0xDE).</summary>
    public enum GuestControlRule
    {
        /// <summary>Guests the player deploys by hand are the player's to control (the remaster has none of those yet).</summary>
        Deployed,
        /// <summary>Every guest on the player's team is the player's to control (flag 0xDE).</summary>
        Always,
    }

    /// <summary>Combat Adjustments → Poaching Odds (ReMixed's flag 0xE5).</summary>
    public enum PoachRule
    {
        /// <summary>A rare poach gets likelier as the poacher's Brave falls: (231 − 2 × Brave) in 256.</summary>
        ByBrave,
        /// <summary>Always 31 in 256 (flag 0xE5).</summary>
        Vanilla,
    }

    /// <summary>Combat Adjustments → Poison &amp; Regen (ReMixed's flag 0xF2).</summary>
    public enum PoisonRegenRule
    {
        /// <summary>Poison and Regen take or give HP only.</summary>
        HpOnly,
        /// <summary>They take or give an eighth of Max MP too (flag 0xF2).</summary>
        HpAndMp,
    }

    /// <summary>Spellcasting → Starting MP (ReMixed's flag 0xF4).</summary>
    public enum StartingMpRule
    {
        /// <summary>Units start a battle with full MP.</summary>
        Full,
        /// <summary>With 10 MP, or their Max MP if that's lower (flag 0xF4).</summary>
        Ten,
    }

    /// <summary>Combat Adjustments → Zodiac Compatibility (ReMixed's flags 0xE2, 0xE3).</summary>
    public enum ZodiacRule
    {
        /// <summary>Good and Bad ±25%, Best and Worst ±50%, as in vanilla.</summary>
        Normal,
        /// <summary>No effect (flag 0xE2).</summary>
        Off,
        /// <summary>Half as strong: ±12.5% and ±25% (flag 0xE3).</summary>
        Half,
        /// <summary>Half again as strong: ±37.5% and ±75% (both).</summary>
        OneAndAHalf,
    }

    /// <summary>Combat Adjustments → Sexuality (ReMixed's flags 0xF8, 0xF9), for zodiac compatibility.</summary>
    public enum SexualityRule
    {
        /// <summary>Opposite signs: Best for a man and a woman, Worst for two of the same gender, as on the PS1.</summary>
        Straight,
        /// <summary>The other way round.</summary>
        Gay,
        /// <summary>Best either way.</summary>
        Bisexual,
        /// <summary>Best either way, as the remaster has it (ReMixed's menu says it differs from Bisexual in Charm; its code doesn't).</summary>
        Tic,
    }

    /// <summary>Combat Adjustments → Power Modes (ReMixed's var 0x16 presets).</summary>
    public enum PowerModeRule
    {
        /// <summary>Damage unchanged (var 0x16 = 0).</summary>
        Knight,
        /// <summary>Allies 125%, enemies 75% (0x4B7D4B7D).</summary>
        Squire,
        /// <summary>Allies 75% (their Math Skill 50%), enemies 125% (0x7D327D4B).</summary>
        Tactician,
        /// <summary>Everyone 50% (0x32323232).</summary>
        Chemist,
        /// <summary>The four percentages below.</summary>
        Custom,
    }

    /// <summary>Job Levels → Job Level Settings (ReMixed's flags 0xE8, 0xE9).</summary>
    public enum JobLevelRule
    {
        /// <summary>More jobs in later chapters: chapter 1 the specified job, 2 and 3 also the current, 4 also the secondary's.</summary>
        ByChapter,
        /// <summary>Just the specified job, as in vanilla.</summary>
        SpecifiedJob,
        /// <summary>The specified and the current job.</summary>
        SpecifiedAndCurrent,
        /// <summary>The specified, the current and the secondary skillset's job.</summary>
        SpecifiedCurrentAndSecondary,
    }

    /// <summary>Job Levels → Enemy Job Settings (ReMixed's var 0x14 byte 3 &amp; 0x0C).</summary>
    public enum EnemyJobRule
    {
        /// <summary>Enemies follow the Job Level Settings.</summary>
        Normal,
        /// <summary>Their preset job level is 8.</summary>
        Expert,
        /// <summary>Every job is level 8.</summary>
        Exceptional,
    }

    /// <summary>Unit Levels, Stats &amp; Equipment → Rare Equipment (ReMixed's flag 0xF6).</summary>
    public enum RareEquipmentRule
    {
        /// <summary>Units with random equipment never get rare items, as in vanilla.</summary>
        LeftOut,
        /// <summary>They can; then a rare item can't be stolen and invited humans don't join.</summary>
        Allowed,
    }

    public const int MaxRate = 255; // ReMixed keeps each rate in a byte

    [Category("Permadeath"), DisplayName("Player deaths")]
    [Description("When a player unit's death counter runs out, they die permanently (ReMixed's default) or " +
                 "revive after the battle. Reviving counts Ramza too.")]
    [DefaultValue(PlayerDeathRule.DiePermanently)]
    public PlayerDeathRule PlayerDeaths { get; set; } = PlayerDeathRule.DiePermanently;

    [Category("Permadeath"), DisplayName("General deaths")]
    [Description("When a unit permanently dies, its body transforms randomly (ReMixed's default), becomes a crystal, " +
                 "or becomes a treasure box.")]
    [DefaultValue(BodyRule.TransformRandomly)]
    public BodyRule Bodies { get; set; } = BodyRule.TransformRandomly;

    [Category("JP and EXP"), DisplayName("JP gain (%)")]
    [Description("JP earned from actions, as a percentage. ReMixed's default is 125. At most 255.")]
    [DefaultValue(125)]
    public int JpGain { get; set; } = 125;

    [Category("JP and EXP"), DisplayName("Spillover JP (1/N)")]
    [Description("The share of an action's JP (at the JP gain) that each of the actor's teammates gets in the same " +
                 "job: 1/N, at least 1 (ReMixed's default is 4, a quarter). 0 shares none. At most 15.")]
    [DefaultValue(4)]
    public int SpilloverJp { get; set; } = 4;

    [Category("JP and EXP"), DisplayName("JP refund")]
    [Description("On the learn screen, choosing an ability the unit has already learned unlearns it and gives back its " +
                 "JP cost (ReMixed's default), half of it, or does nothing (vanilla). An equipped ability, or Equip " +
                 "Sword or Equip Shield while Two Spells is equipped, stays learned.")]
    [DefaultValue(JpRefundRule.Full)]
    public JpRefundRule JpRefund { get; set; } = JpRefundRule.Full;

    [Category("JP and EXP"), DisplayName("EXP gain (%)")]
    [Description("EXP earned from actions, as a percentage. ReMixed's default is 100. At most 255.")]
    [DefaultValue(100)]
    public int ExpGain { get; set; } = 100;

    [Category("JP and EXP"), DisplayName("Bonus EXP & JP on victory")]
    [Description("After a won battle, every unit on your side gets a level (EXP), and/or ten times an action's JP base " +
                 "(8 + level / 4 + 2 × job level) in its current job (JP), each JP total at most 9999. Off by default, " +
                 "as in ReMixed.")]
    [DefaultValue(VictoryBonusRule.None)]
    public VictoryBonusRule VictoryBonus { get; set; } = VictoryBonusRule.None;

    [Category("Testing"), DisplayName("Test mode")]
    [Description("Records which of the mod's mechanics fire in each battle, with any errors, into TestReport.html in " +
                 "this mod's config folder (Reloaded-II: right-click the mod, Open Folder → User config), rewritten " +
                 "after every battle. RecordAndAutoBattle also lets the AI play your units, so a battle plays itself: " +
                 "start it and watch, or speed the game up; and Start anywhere on the world map then starts the first " +
                 "of ReMixed's missions. Off by default; not part of ReMixed.")]
    [DefaultValue(TestModeRule.Off)]
    public TestModeRule TestMode { get; set; } = TestModeRule.Off;

    [Category("Saves"), DisplayName("Separate saves")]
    [Description("Keeps the modded game's saves in a folder of their own (Documents/My Games/FINAL FANTASY TACTICS - " +
                 "Ivalice ReMixed), so they don't mix with the plain game's. Steam Cloud doesn't sync that folder. Off: " +
                 "the modded game uses the plain game's saves. Not part of ReMixed; takes effect the next time the game starts.")]
    [DefaultValue(true)]
    public bool SeparateSaves { get; set; } = true;

    [Category("Story"), DisplayName("Argath campaign")]
    [Description("ReMixed's secret campaign, played as Argath: its own retelling of chapter 1, the Order of the " +
                 "Northern Sky's side of the story and a final battle of its own. ReMixed starts it when Ramza is " +
                 "named Algus or Argath, which the remaster doesn't allow; with this on, a new game's opening (the " +
                 "chapel at Orbonne) goes on to it instead of the first battle. New games only: turn it off again " +
                 "for a normal one. Ported to its end (after its ending you're back on the world map), but played " +
                 "in the game only as far as its battle in the woods so far.")]
    [DefaultValue(false)]
    public bool ArgathCampaign { get; set; }

    [Category("Combat adjustments"), DisplayName("Nemesis System")]
    [Description("Now and then, a generic enemy that was downed but not finished off comes back by name in a later " +
                 "battle, as strong as the chapter makes it (ReMixed's default). It's kept in the save's event variable " +
                 "0x40. Off: no Nemesis is remembered or comes back.")]
    [DefaultValue(true)]
    public bool NemesisSystem { get; set; } = true;

    [Category("Combat adjustments"), DisplayName("Back attacks")]
    [Description("Which attacks from behind are critical hits: a basic attack by a unit with Banditry and Mug by anyone " +
                 "(ReMixed's default), none beyond vanilla's 4%, or every unit's basic attacks and Mug.")]
    [DefaultValue(BackAttackRule.BanditryAndMug)]
    public BackAttackRule BackAttacks { get; set; } = BackAttackRule.BanditryAndMug;

    [Category("Combat adjustments"), DisplayName("Bravery/Wariness")]
    [Description("Under 50 Brave, the damage a unit takes from physical attacks lowers as its Brave does, by " +
                 "(50 − Brave)% (ReMixed's default), or stays the same (vanilla).")]
    [DefaultValue(BraveryRule.LowersWithBrave)]
    public BraveryRule Bravery { get; set; } = BraveryRule.LowersWithBrave;

    [Category("Combat adjustments"), DisplayName("Knockback")]
    [Description("Immortal units and the party leader can be knocked back, and a knockback can land in water " +
                 "(ReMixed's default), or neither (vanilla). Either way only an attack's last strike knocks back, and " +
                 "a knockback can land on lava.")]
    [DefaultValue(KnockbackRule.Everyone)]
    public KnockbackRule Knockback { get; set; } = KnockbackRule.Everyone;

    [Category("Combat adjustments"), DisplayName("Minimum weapon range")]
    [Description("Bows, crossbows and guns have no minimum range (ReMixed's default), or vanilla's 3, so they can't hit " +
                 "anything within 2 tiles.")]
    [DefaultValue(WeaponRangeRule.NoMinimum)]
    public WeaponRangeRule WeaponRange { get; set; } = WeaponRangeRule.NoMinimum;

    [Category("Combat adjustments"), DisplayName("Guest control")]
    [Description("Guests the player deploys by hand are the player's to control (ReMixed's default; on the remaster " +
                 "guests can't be deployed by hand yet, so this changes nothing), or every guest on the player's " +
                 "side is, the ones the story places too.")]
    [DefaultValue(GuestControlRule.Deployed)]
    public GuestControlRule GuestControl { get; set; } = GuestControlRule.Deployed;

    [Category("Combat adjustments"), DisplayName("Poaching odds")]
    [Description("The chance of a monster's rare poach goes up as the poacher's Brave goes down: (231 − 2 × Brave) in " +
                 "256, so 37/256 at Brave 97 and 131/256 at Brave 50 (ReMixed's default), or is always 31/256 (vanilla).")]
    [DefaultValue(PoachRule.ByBrave)]
    public PoachRule PoachingOdds { get; set; } = PoachRule.ByBrave;

    [Category("Combat adjustments"), DisplayName("Poison and Regen")]
    [Description("Poison and Regen affect HP only (ReMixed's default), or MP too, by an eighth of Max MP.")]
    [DefaultValue(PoisonRegenRule.HpOnly)]
    public PoisonRegenRule PoisonAndRegen { get; set; } = PoisonRegenRule.HpOnly;

    [Category("Combat adjustments"), DisplayName("Zodiac compatibility")]
    [Description("How strong zodiac compatibility is: Normal (ReMixed's default) gives ±25% for Good and Bad and ±50% for " +
                 "Best and Worst; Off, Half (12.5% and 25%) or OneAndAHalf (37.5% and 75%) change that.")]
    [DefaultValue(ZodiacRule.Normal)]
    public ZodiacRule ZodiacStrength { get; set; } = ZodiacRule.Normal;

    [Category("Combat adjustments"), DisplayName("Sexuality")]
    [Description("Zodiac compatibility between opposite signs: Straight (ReMixed's default, as on the PS1) makes a man " +
                 "and a woman Best and two of the same gender Worst; Gay the other way round; Bisexual and Tic (the " +
                 "remaster's own rule) always Best. Charm follows through the compatibility's effect on status " +
                 "chances, as in ReMixed, whose code has no other Charm rule (its menu's Bisexual and TIC differ in text only).")]
    [DefaultValue(SexualityRule.Straight)]
    public SexualityRule Sexuality { get; set; } = SexualityRule.Straight;

    [Category("Combat adjustments"), DisplayName("Power mode")]
    [Description("ReMixed's Power Modes: Knight (the default) leaves damage alone; Squire gives allies 125% and enemies " +
                 "75%; Tactician allies 75% (their Math Skill 50%) and enemies 125%; Chemist everyone 50%; Custom uses " +
                 "the four percentages below.")]
    [DefaultValue(PowerModeRule.Knight)]
    public PowerModeRule PowerMode { get; set; } = PowerModeRule.Knight;

    [Category("Combat adjustments"), DisplayName("Ally power (%)")]
    [Description("With the Custom power mode: HP damage dealt by the player's units, as a percentage (ReMixed's var " +
                 "0x16). 100 is unchanged.")]
    [DefaultValue(100)]
    public int AllyPower { get; set; } = 100;

    [Category("Combat adjustments"), DisplayName("Enemy power (%)")]
    [Description("With the Custom power mode: HP damage dealt by other teams' units, as a percentage. 100 is unchanged.")]
    [DefaultValue(100)]
    public int EnemyPower { get; set; } = 100;

    [Category("Combat adjustments"), DisplayName("Ally Math power (%)")]
    [Description("With the Custom power mode: HP damage dealt through Math Skill by the player's units, in place of " +
                 "Ally power. 100 is unchanged.")]
    [DefaultValue(100)]
    public int AllyMathPower { get; set; } = 100;

    [Category("Combat adjustments"), DisplayName("Enemy Math power (%)")]
    [Description("With the Custom power mode: HP damage dealt through Math Skill by other teams' units, in place of " +
                 "Enemy power. 100 is unchanged.")]
    [DefaultValue(100)]
    public int EnemyMathPower { get; set; } = 100;

    [Category("Spellcasting"), DisplayName("Auto Renew")]
    [Description("At the end of every turn, a unit without Poison or Regen gets back an eighth of its Max MP (at " +
                 "least 1). Off by default.")]
    [DefaultValue(false)]
    public bool AutoRenew { get; set; }

    [Category("Spellcasting"), DisplayName("Starting MP")]
    [Description("Units start a battle with full MP (ReMixed's default), or with 10 MP (their Max MP if that's lower).")]
    [DefaultValue(StartingMpRule.Full)]
    public StartingMpRule StartingMp { get; set; } = StartingMpRule.Full;

    [Category("Spellcasting"), DisplayName("Cast instantly")]
    [Description("ReMixed's Cast Time option: abilities with a charge time cast at once, as if every unit had the " +
                 "Non-charge support (flag 0xF5). Off by default.")]
    [DefaultValue(false)]
    public bool EveryoneNonCharge { get; set; }

    [Category("Levels"), DisplayName("Improved level scaling")]
    [Description("Which units an enemy's scaling level follows: the highest level among the units you deploy " +
                 "(ReMixed's default), or the highest in the whole roster (vanilla's; units away on errands left out " +
                 "either way). The enemies are redone for the deployed units once the formation's done.")]
    [DefaultValue(LevelScalingRule.DeployedUnits)]
    public LevelScalingRule LevelScaling { get; set; } = LevelScalingRule.DeployedUnits;

    [Category("Levels"), DisplayName("Fixed or minimum levels")]
    [Description("Whether an enemy with a fixed level scales with the party instead when the party level is higher " +
                 "(by more than an eighth). ReMixed's default is never.")]
    [DefaultValue(FixedLevelRule.Never)]
    public FixedLevelRule FixedLevels { get; set; } = FixedLevelRule.Never;

    [Category("Levels"), DisplayName("Enemy level boost")]
    [Description("Normal (ReMixed's default); Nightmare: fixed-level enemies +49 levels and enemies that scale with the " +
                 "party at least level 57; No Future: every enemy level 99.")]
    [DefaultValue(LevelBoostRule.Normal)]
    public LevelBoostRule EnemyLevelBoost { get; set; } = LevelBoostRule.Normal;

    [Category("Units"), DisplayName("Job level settings")]
    [Description("Which of a new unit's jobs get the job level its battle data sets: more in later chapters (ReMixed's " +
                 "default: chapter 1 the specified job, chapters 2 and 3 also its current job, chapter 4 also its " +
                 "secondary skillset's), just the specified job (vanilla), the specified and current, or all three.")]
    [DefaultValue(JobLevelRule.ByChapter)]
    public JobLevelRule JobLevels { get; set; } = JobLevelRule.ByChapter;

    [Category("Units"), DisplayName("Enemy job settings")]
    [Description("Normal (ReMixed's default) follows the job level settings; Expert sets enemies' preset job level to " +
                 "8; Exceptional sets every job of theirs to 8. With either, an enemy's empty reaction, support and " +
                 "movement are picked at random.")]
    [DefaultValue(EnemyJobRule.Normal)]
    public EnemyJobRule EnemyJobs { get; set; } = EnemyJobRule.Normal;

    [Category("Units"), DisplayName("Rare equipment")]
    [Description("Whether units with random equipment can get rare items. ReMixed's default leaves them out, as " +
                 "vanilla does. Allowing them also makes rare items impossible to steal (they can still be broken) " +
                 "and stops invited humans from joining after the battle, as in ReMixed.")]
    [DefaultValue(RareEquipmentRule.LeftOut)]
    public RareEquipmentRule RareEquipment { get; set; } = RareEquipmentRule.LeftOut;

    [Category("Units"), DisplayName("Job stats")]
    [Description("A unit's stat growths come from its job only (ReMixed's default; Bard and Dancer's innate still " +
                 "adds the secondary's), or from the best of its job and its secondary skillset's job, for everyone.")]
    [DefaultValue(JobStatRule.JobOnly)]
    public JobStatRule JobStats { get; set; } = JobStatRule.JobOnly;

    /// <summary>The options in effect: the save's own once one is in play (<see cref="Patches.SaveOptions"/>), the mod's
    /// settings until then.</summary>
    public static Options Current { get; set; } = new();

    /// <summary>The mod's settings (Config.json, read at startup): a save's options until it has its own, and the
    /// settings a save has no place for (the test mode, separate saves, the Argath campaign).</summary>
    public static Options Settings { get; set; } = new();

    /// <summary>Raised when <see cref="Use"/> puts other options in effect, for the rules that keep a copy.</summary>
    internal static event Action? Changed;

    /// <summary>Puts <paramref name="next"/> in effect; whether it differed (then <see cref="Changed"/> is raised).</summary>
    internal static bool Use(Options next)
    {
        if (Same(Current, next))
            return false;
        Current = next;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Whether two sets of options are the same, setting by setting.</summary>
    internal static bool Same(Options a, Options b) =>
        JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);

    /// <summary>A copy of these options.</summary>
    internal Options Copy() => (Options)MemberwiseClone();

    public const string FileName = "Config.json";

    /// <summary>Where <see cref="Save"/> writes; set by whoever read it.</summary>
    [JsonIgnore, Browsable(false)]
    public string? FilePath { get; set; }

    [JsonIgnore, Browsable(false)]
    public string ConfigName => "Ivalice ReMixed";

    [JsonIgnore, Browsable(false)]
    public Action Save => () =>
    {
        if (FilePath != null)
            Write(FilePath);
    };

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The options in <paramref name="path"/>, or the defaults if it's missing or unreadable (with why in <paramref name="problem"/>).</summary>
    public static Options Read(string path, out string? problem)
    {
        problem = null;
        Options options;
        try
        {
            options = File.Exists(path) ? Parse(File.ReadAllText(path)) : new();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            problem = e.Message;
            options = new();
        }
        options.JpGain = Math.Clamp(options.JpGain, 0, MaxRate);
        options.ExpGain = Math.Clamp(options.ExpGain, 0, MaxRate);
        options.SpilloverJp = Math.Clamp(options.SpilloverJp, 0, 15);
        if (!Enum.IsDefined(options.PowerMode)) options.PowerMode = default;
        options.AllyPower = Math.Clamp(options.AllyPower, 1, MaxRate);
        options.EnemyPower = Math.Clamp(options.EnemyPower, 1, MaxRate);
        options.AllyMathPower = Math.Clamp(options.AllyMathPower, 1, MaxRate);
        options.EnemyMathPower = Math.Clamp(options.EnemyMathPower, 1, MaxRate);
        if (!Enum.IsDefined(options.PlayerDeaths)) options.PlayerDeaths = default;
        if (!Enum.IsDefined(options.Bodies)) options.Bodies = default;
        if (!Enum.IsDefined(options.BackAttacks)) options.BackAttacks = default;
        if (!Enum.IsDefined(options.Bravery)) options.Bravery = default;
        if (!Enum.IsDefined(options.Knockback)) options.Knockback = default;
        if (!Enum.IsDefined(options.WeaponRange)) options.WeaponRange = default;
        if (!Enum.IsDefined(options.PoachingOdds)) options.PoachingOdds = default;
        if (!Enum.IsDefined(options.JpRefund)) options.JpRefund = default;
        if (!Enum.IsDefined(options.GuestControl)) options.GuestControl = default;
        if (!Enum.IsDefined(options.PoisonAndRegen)) options.PoisonAndRegen = default;
        if (!Enum.IsDefined(options.StartingMp)) options.StartingMp = default;
        if (!Enum.IsDefined(options.ZodiacStrength)) options.ZodiacStrength = default;
        if (!Enum.IsDefined(options.Sexuality)) options.Sexuality = default;
        if (!Enum.IsDefined(options.JobLevels)) options.JobLevels = default;
        if (!Enum.IsDefined(options.EnemyJobs)) options.EnemyJobs = default;
        if (!Enum.IsDefined(options.RareEquipment)) options.RareEquipment = default;
        if (!Enum.IsDefined(options.JobStats)) options.JobStats = default;
        if (!Enum.IsDefined(options.FixedLevels)) options.FixedLevels = default;
        if (!Enum.IsDefined(options.LevelScaling)) options.LevelScaling = default;
        if (!Enum.IsDefined(options.EnemyLevelBoost)) options.EnemyLevelBoost = default;
        if (!Enum.IsDefined(options.VictoryBonus)) options.VictoryBonus = default;
        if (!Enum.IsDefined(options.TestMode)) options.TestMode = default;
        options.FilePath = path;
        return options;
    }

    /// <summary>
    /// The options in a Config.json's text. One written before the power mode existed, with percentages
    /// that aren't all 100, keeps them as the Custom mode.
    /// </summary>
    internal static Options Parse(string json)
    {
        var options = JsonSerializer.Deserialize<Options>(json, Json) ?? new();
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && !document.RootElement.TryGetProperty(nameof(PowerMode), out _)
            && (options.AllyPower, options.EnemyPower, options.AllyMathPower, options.EnemyMathPower) != (100, 100, 100, 100))
            options.PowerMode = PowerModeRule.Custom;
        return options;
    }

    public void Write(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }

    public override string ToString() =>
        $"player deaths {PlayerDeaths}, bodies {Bodies}, JP {JpGain}%, spillover 1/{SpilloverJp}, JP refund {JpRefund}, EXP {ExpGain}%, victory bonus {VictoryBonus}, nemesis {NemesisSystem}, test mode {TestMode}, separate saves {SeparateSaves}, argath campaign {ArgathCampaign}, " +
        $"back attacks {BackAttacks}, " +
        $"bravery {Bravery}, knockback {Knockback}, weapon range {WeaponRange}, poaching {PoachingOdds}, guests {GuestControl}, zodiac {ZodiacStrength}, sexuality {Sexuality}, " +
        $"job stats {JobStats}, job levels {JobLevels}, enemy jobs {EnemyJobs}, rare equipment {RareEquipment}, " +
        $"level scaling {LevelScaling}, fixed levels {FixedLevels}, " +
        $"enemy level boost {EnemyLevelBoost}, " +
        $"power {PowerMode}: ally {Percentages.Ally}% enemy {Percentages.Enemy}% ally Math {Percentages.AllyMath}% " +
        $"enemy Math {Percentages.EnemyMath}%, " +
        $"cast instantly {EveryoneNonCharge}, Poison and Regen {PoisonAndRegen}, Auto Renew {AutoRenew}, " +
        $"starting MP {StartingMp}";

    /// <summary>Whether any power setting changes damage.</summary>
    [JsonIgnore, Browsable(false)]
    public bool ChangesPower => Percentages != (100, 100, 100, 100);

    /// <summary>The damage percentages in effect: the power mode's preset, or the four settings with Custom.</summary>
    [JsonIgnore, Browsable(false)]
    public (int Ally, int Enemy, int AllyMath, int EnemyMath) Percentages => PowerMode switch
    {
        PowerModeRule.Squire => (125, 75, 125, 75),
        PowerModeRule.Tactician => (75, 125, 50, 125),
        PowerModeRule.Chemist => (50, 50, 50, 50),
        PowerModeRule.Custom => (AllyPower, EnemyPower, AllyMathPower, EnemyMathPower),
        _ => (100, 100, 100, 100),
    };
}

/// <summary>
/// Reloaded-II's settings entry point for this mod: the launcher finds it in the DLL and shows
/// <see cref="Options"/> in its "Configure Mod" window.
/// </summary>
public sealed class Configurator : IConfiguratorV3
{
    private string? _modDirectory, _configDirectory;
    private Options? _options;

    public void SetModDirectory(string modDirectory) => _modDirectory = modDirectory;

    public void SetConfigDirectory(string configDirectory) => _configDirectory = configDirectory;

    public void SetContext(in ConfiguratorContext context) { }

    public void Migrate(string oldDirectory, string newDirectory)
    {
        var from = Path.Combine(oldDirectory, Options.FileName);
        var to = Path.Combine(newDirectory, Options.FileName);
        if (File.Exists(from) && !File.Exists(to))
        {
            Directory.CreateDirectory(newDirectory);
            File.Move(from, to);
        }
    }

    public IConfigurable[] GetConfigurations()
    {
        var directory = _configDirectory ?? _modDirectory ?? AppContext.BaseDirectory;
        _options ??= Options.Read(Path.Combine(directory, Options.FileName), out _);
#if DEVELOPER_MENU
        if (DeveloperMenu.Enabled(_modDirectory))
            return [_options, DeveloperMenu.Configuration(directory)];
#endif
        return [_options];
    }

    public bool TryRunCustomConfiguration() => false;
}
