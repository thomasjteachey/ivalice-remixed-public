using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's battle rules that aren't one ability's (docs/MECHANICS.md): unevadable attacks,
/// team-only targeting, Defense and Warding Aura, Limit's faster charge when hurt, and
/// status chance tiers, weapon requirements per ability, EXP and JP rates, the post-move rewrite.
/// Data: {"enable": [rule names], "unevadable": [ability ids], "tiers": [[status set, tier bits]],
/// "weapons": [[ability, mask]], "rates": {"jp", "exp", "spillover"}}.
/// </summary>
internal static unsafe class Rules
{
    private static Game _game = null!;
    private static RuleCode _code = null!;
    private static delegate* unmanaged<void> _evadeSetup, _defenseUp, _magicDefenseUp; // the originals
    private static delegate* unmanaged<byte*, byte*, int, int> _queueAction;
    private static delegate* unmanaged<void> _statusStep;
    private static delegate* unmanaged<int> _hitStatusRoll;
    private static readonly byte[] Tiers = new byte[0x100]; // by status set id: ReMixed's chance tier bits
    private static bool _teamTargeting;
    private static int _vanillaStatusRoll; // the formula's id while formula 0x02 or 0x05 runs
    private static int _category;          // ReMixed's status category for this target (0x8015C514): 19 after 0x02's roll

    /// <summary>
    /// The chance each status of a Separate set lands with, which the status step's per-status roll reads (vanilla's
    /// 24%; ReMixed's tier chance, 19 after formula 0x02's roll, 100 without either). Set by the status step's hook.
    /// </summary>
    internal static byte* SeparateChance { get; private set; }
    internal static nint SeparateAt { get; private set; }
    internal static nint SeparateStub { get; private set; }

    /// <summary>Misses from a failed chance tier, for the offline check.</summary>
    internal static int TierMisses;
    private static readonly HashSet<int> UnevadableAbilities = [];
    private static nint[] _formulas = []; // by formula id: what its table entry called before the team check
    private static int _failed;

    /// <summary>Cloud's Limit skillset, the one whose charge ReMixed shortens (PS1 0x8015D8B0).</summary>
    public const int LimitSkillset = 0x29;
    /// <summary>The support byte (+0x99, PS1 +0x90) with Defense UP (8) and Magic Defense UP (2).</summary>
    private const int AuraSupports = Battle.UnitSupports + 1;
    private const int StubSize = 32;

    public static void Install(Game game, JsonElement data)
    {
        _game = game;
        var code = _code = game.RuleCode;
        var hooks = game.Hooks;
        var rules = data.GetProperty("enable").EnumerateArray().Select(e => e.GetString()!).ToHashSet();
        var enabled = new List<string>();
        if (rules.Remove("unevadable"))
        {
            foreach (var id in data.GetProperty("unevadable").EnumerateArray())
                UnevadableAbilities.Add(id.GetInt32());
            _evadeSetup = (delegate* unmanaged<void>)hooks.Hook(
                (nint)code.EvadeSetup, (nint)(delegate* unmanaged<void>)&EvadeSetup, "the evade setup");
            enabled.Add($"unevadable attacks (Transparent attackers, {UnevadableAbilities.Count} {(UnevadableAbilities.Count == 1 ? "ability" : "abilities")})");
        }
        if (rules.Remove("team-targeting"))
        {
            _teamTargeting = true;
            enabled.Add($"team-only targeting ({InstallFormulaStubs(game)} formulas)");
        }
        if (rules.Remove("auras"))
        {
            _ = game.TurnCode.Units; // found now, so a missing pattern stops this rule here
            _defenseUp = (delegate* unmanaged<void>)hooks.Hook(
                (nint)code.DefenseUp, (nint)(delegate* unmanaged<void>)&DefenseUp, "Defense UP");
            _magicDefenseUp = (delegate* unmanaged<void>)hooks.Hook(
                (nint)code.MagicDefenseUp, (nint)(delegate* unmanaged<void>)&MagicDefenseUp, "Magic Defense UP");
            enabled.Add("Defense and Warding Aura");
        }
        if (rules.Remove("limit-charge"))
        {
            HookQueue(game);
            _limitCharge = true;
            enabled.Add("Limit's faster charge");
            try
            {
                LimitPreview.Install(game);
                enabled.Add("Limit's charge in the ability lists");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: Limit's charge in the ability lists not applied: {e.Message}");
            }
        }
        if (rules.Remove("status-tiers"))
        {
            var sets = 0;
            foreach (var pair in data.GetProperty("tiers").EnumerateArray())
            {
                Tiers[pair[0].GetInt32()] = (byte)(pair[1].GetInt32() & 0x0F);
                sets++;
            }
            if (_formulas.Length == 0)
                InstallFormulaStubs(game); // they tell the hit's status roll which formula is running
            _statusStep = (delegate* unmanaged<void>)hooks.Hook(
                (nint)code.StatusStep, (nint)(delegate* unmanaged<void>)&StatusStep, "the status step");
            _hitStatusRoll = (delegate* unmanaged<int>)hooks.Hook(
                (nint)code.HitStatusRoll, (nint)(delegate* unmanaged<int>)&HitStatusRoll, "the hit's status roll");
            enabled.Add($"status chance tiers ({sets} sets)");
            try
            {
                InstallSeparateChance(game);
                enabled.Add("a Separate set's statuses roll at its tier's chance");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the Separate sets' chance not applied: {e.Message}");
            }
        }
        if (rules.Remove("learn-secondary"))
        {
            Learning.Install(game);
            enabled.Add("learning on hit through the secondary skillset");
        }
        var gainRates = rules.Remove("gain-rates");
        var postMove = rules.Remove("post-move");
        if (gainRates || postMove)
            Gains.SetRates(game, data.GetProperty("rates"));
        if (gainRates)
        {
            try
            {
                Gains.Install(game);
                enabled.Add($"EXP and JP rates (EXP {Gains.ExpRate}%, JP {Gains.JpRate}%)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: EXP and JP rates not applied: {e.Message}");
            }
        }
        if (postMove)
        {
            try
            {
                Movement.UseRewrite(game);
                enabled.Add("every post-move ability applies");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the post-move rewrite not applied: {e.Message}");
            }
        }
        if (rules.Remove("landing"))
        {
            try
            {
                Landing.Install(game);
                enabled.Add("knockback into lava or water");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the landings not applied: {e.Message}");
            }
            try
            {
                Landing.InstallBodies(game);
                enabled.Add("a body on lava or deep water moves to a free tile");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the deadly tiles not applied: {e.Message}");
            }
        }
        if (rules.Remove("equipped-reaction"))
        {
            try
            {
                Passives.InstallReactionRule(game);
                enabled.Add("an equipped reaction wins over an innate one");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the reaction rule not applied: {e.Message}");
            }
        }
        if (rules.Remove("weapon-requirements"))
        {
            try
            {
                enabled.Add($"weapon requirements ({WeaponRequirements.Install(game, data.GetProperty("weapons"))} abilities)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: weapon requirements not applied: {e.Message}");
            }
        }
        if (rules.Remove("monster-reactions"))
        {
            try
            {
                enabled.Add($"monster reactions by family ({MonsterReactions.Install(game, data.GetProperty("monster-reactions"))} jobs)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: monster reactions not applied: {e.Message}");
            }
        }
        if (rules.Remove("jump-rework"))
        {
            try
            {
                JumpRework.Install(game, data.GetProperty("jump"));
                enabled.Add("ReMixed's Jump (range 3, plus one per Jump Range +1)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the Jump rework not applied: {e.Message}");
            }
        }
        if (rules.Remove("dragon-tech")) // after "jump-rework", whose AI bound it takes over
        {
            try
            {
                DragonTech.Install(game);
                enabled.Add("Dragon Tech after Jump (Lancet and the Breaths, with Jump's learned bits)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: Dragon Tech not applied: {e.Message}");
            }
        }
        if (rules.Remove("death-counter"))
        {
            try
            {
                DeathCounter.Install(game);
                enabled.Add($"the death counter (player deaths: {Options.Current.PlayerDeaths}, bodies: {Options.Current.Bodies})");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the death counter not applied: {e.Message}");
            }
        }
        if (rules.Remove("level-options"))
        {
            try
            {
                LevelScaling.Install(game);
                enabled.Add($"level options (fixed levels: {Options.Current.FixedLevels}, enemy level boost: {Options.Current.EnemyLevelBoost})");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the level options not applied: {e.Message}");
            }
        }
        if (rules.Remove("brave-damage")) // whatever the option: a save's own can change it
        {
            try
            {
                BraveDamage.Install(game);
                enabled.Add("a target with Brave under 50 takes less from attacks");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the Brave rule not applied: {e.Message}");
            }
        }
        if (rules.Remove("knockback"))
        {
            try
            {
                Knockback.Install(game);
                enabled.Add($"ReMixed's knockback ({(Knockback.Vanilla ? "vanilla targets" : "Immortal units and the leader too")}, "
                            + "last strike only, into water and onto lava)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the knockback rules not applied: {e.Message}");
            }
        }
        if (rules.Remove("weapon-range"))
        {
            try
            {
                WeaponRange.Install(game);
                enabled.Add(WeaponRange.Patched ? "no minimum weapon range" : "vanilla's minimum weapon range of 3");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the minimum weapon range not applied: {e.Message}");
            }
        }
        if (rules.Remove("poach-odds"))
        {
            try
            {
                PoachOdds.Install(game);
                enabled.Add(Options.Current.PoachingOdds == Options.PoachRule.ByBrave
                    ? "rare poaches by the poacher's Brave" : "vanilla's 31/256 rare poaches");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the poaching odds not applied: {e.Message}");
            }
        }
        if (rules.Remove("status-ticks"))
        {
            try
            {
                StatusTick.Install(game);
                if (StatusTick.Patched)
                    enabled.Add("end-of-turn ticks (" + string.Join(", ", new[]
                    {
                        Options.Current.PoisonAndRegen == Options.PoisonRegenRule.HpAndMp ? "Poison and Regen on MP too" : null,
                        Options.Current.AutoRenew ? "Auto Renew" : null,
                    }.Where(x => x != null)) + ")");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the Poison and Regen options not applied: {e.Message}");
            }
        }
        if (rules.Remove("starting-mp"))
        {
            try
            {
                StartingMp.Install(game);
                if (StartingMp.Patched)
                    enabled.Add($"units start a battle with {StartingMp.Limit} MP");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: Starting MP not applied: {e.Message}");
            }
        }
        if (rules.Remove("zodiac"))
        {
            try
            {
                Zodiac.Install(game);
                enabled.Add($"zodiac compatibility ({Options.Current.Sexuality}, {Options.Current.ZodiacStrength})");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the zodiac rules not applied: {e.Message}");
            }
        }
        if (rules.Remove("equip-supports"))
        {
            try
            {
                enabled.Add($"equip permissions from supports ({EquipSupports.Install(game, data.GetProperty("equip-supports"))} supports)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the equip supports not applied: {e.Message}");
            }
        }
        if (rules.Remove("weapon-pick"))
        {
            try
            {
                WeaponPick.Install(game);
                enabled.Add("ReMixed's random weapon pick");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the random weapon pick not applied: {e.Message}");
            }
        }
        if (rules.Remove("job-levels"))
        {
            try
            {
                JobLevels.Install(game);
                enabled.Add($"ReMixed's job levels ({Options.Current.JobLevels}, enemies {Options.Current.EnemyJobs}) " +
                            "and random secondary");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the job levels not applied: {e.Message}");
            }
        }
        if (rules.Remove("arrow-guard"))
        {
            try
            {
                ArrowGuard.Install(game);
                enabled.Add("Arrow Guard against guns too");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: Arrow Guard against guns not applied: {e.Message}");
            }
        }
        if (rules.Remove("hide-unlearnable"))
        {
            try
            {
                HideUnlearnable.Install(game);
                enabled.Add("abilities a unit can't learn left off the ability lists (no \"???\")");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: hiding the abilities a unit can't learn not applied: {e.Message}");
            }
        }
        if (rules.Remove("brave-reactions"))
        {
            try
            {
                enabled.Add($"reactions at least half the time with Move-Find Item ({BraveReactions.Install(game)} places)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the reactions' Brave chance not applied: {e.Message}");
            }
        }
        if (rules.Remove("random-equipment"))
        {
            try
            {
                RandomEquipment.Install(game);
                enabled.Add($"ReMixed's random equipment (rare items {Options.Current.RareEquipment}), Iga and Koga Knives " +
                            "in pairs, and what can be stolen or broken");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the random equipment not applied: {e.Message}");
            }
        }
        if (rules.Remove("skillset-types"))
        {
            try
            {
                SkillsetTypes.Install(game);
                enabled.Add("Elemental as an ordinary skillset (ReMixed's Geomancy picks its own abilities)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the skillset types not applied: {e.Message}");
            }
        }
        if (rules.Remove("in-place"))
            enabled.AddRange(InPlace.Install(game));
        if (rules.Remove("side-evasion"))
        {
            try
            {
                SideEvasion.Install(game);
                enabled.Add("ReMixed's evasion from the side (Weapon Guard keeps the hands', bare hands the class's)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the side evasion not applied: {e.Message}");
            }
        }
        if (rules.Remove("invite-control"))
        {
            try
            {
                InviteControl.Install(game);
                enabled.Add("a unit the player invites is the player's to control");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: Invite's control not applied: {e.Message}");
            }
        }
        if (rules.Remove("evade-checks"))
            enabled.AddRange(EvadeChecks.Install(game));
        if (rules.Remove("formula-steps"))
            enabled.AddRange(FormulaSteps.Install(game));
        if (rules.Remove("gun-skills"))
        {
            try
            {
                enabled.AddRange(GunSkills.Install(game, data.GetProperty("gun-skills")));
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the gun skills not applied: {e.Message}");
            }
        }
        if (rules.Remove("job-change-secondary"))
        {
            try
            {
                JobChange.Install(game);
                enabled.Add("a job change keeps a usable secondary");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the job change's secondary not applied: {e.Message}");
            }
        }
        if (rules.Remove("job-change-innates"))
        {
            try
            {
                JobChange.InstallInnates(game);
                enabled.Add("a job change takes off an equipped reaction, support or movement the new job has innately");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the job change's innates not applied: {e.Message}");
            }
        }
        if (rules.Remove("guest-control")) // whatever the option: a save's own can change it
        {
            try
            {
                GuestControl.Install(game);
                enabled.Add("every guest on the player's side is the player's to control");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: guest control not applied: {e.Message}");
            }
        }
        if (rules.Remove("pass"))
        {
            try
            {
                Pass.Install(game);
                enabled.Add("Pass (0x1FC) moves through enemies");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: Pass not applied: {e.Message}");
            }
        }
        if (rules.Remove("riding-actions"))
        {
            try
            {
                RidingActions.Install(game);
                enabled.Add("riding: a rider's mount gives one extra action a turn, its own skillset, as the attacker, for its own EXP and JP; a charging rider keeps its charge, and a dead rider's mount takes its CT");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: riding's mount actions not applied: {e.Message}");
            }
        }
        if (rules.Remove("persistent-performances"))
        {
            try
            {
                Performances.Install(game);
                enabled.Add("persistent performances: a performer's song goes on through an instant action (Math Skill ends it), and the turn-start \"performing\" notice is gone");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: persistent performances not applied: {e.Message}");
            }
        }
        if (rules.Remove("riding-boarding"))
        {
            try
            {
                RidingBoarding.Install(game);
                enabled.Add("riding: the White Chocobo can be ridden, another team's chocobo only with Pass and Train, and Train wins the mount");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: riding's boarding rules not applied: {e.Message}");
            }
        }
        if (rules.Remove("movement-mode"))
        {
            try
            {
                MovementMode.Install(game);
                enabled.Add("ReMixed's movement modes (Waterbreathing and riders move in water; Chicken and Frog lose Teleport)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: movement modes not applied: {e.Message}");
            }
        }
        if (rules.Remove("cone-area"))
        {
            try
            {
                ConeArea.Install(game);
                enabled.Add("cone areas for abilities with both Linear and 3 Directions (the Breaths, Elemental Wave), " +
                            "with ReMixed's targeting off the diagonals and targets in rings");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: cone areas not applied: {e.Message}");
            }
        }
        if (rules.Remove("errands"))
        {
            try
            {
                Errands.Install(game);
                enabled.Add("special characters and guests can go on errands, and don't count as in the party while away");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: errands for special characters and guests not applied: {e.Message}");
            }
        }
        if (rules.Remove("one-handing"))
        {
            try
            {
                OneHanding.Install(game);
                enabled.Add("one-handing: with Equip Shield or 0x1CC, axes and bows go with a shield or another weapon");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: one-handing not applied: {e.Message}");
            }
        }
        if (rules.Remove("errand-locks"))
        {
            try
            {
                ErrandLocks.Install(game);
                enabled.Add("the job and equipment locks by the errand byte (a unit away can re-equip, not change job)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the errand locks not applied: {e.Message}");
            }
        }
        if (rules.Remove("hitting-bodies"))
        {
            try
            {
                HittingBodies.Install(game);
                enabled.Add("a strike or a bow shot aimed at a body reaches it (bodies block nothing)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: hitting bodies not applied: {e.Message}");
            }
        }
        if (rules.Remove("arc-obstacles"))
        {
            try
            {
                ArcObstacles.Install(game);
                enabled.Add("ReMixed's bow path (allies aren't steered around, Jumping units are obstacles)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the bow path's obstacles not applied: {e.Message}");
            }
        }
        if (rules.Remove("taken-equipment"))
        {
            try
            {
                TakenEquipment.Install(game);
                enabled.Add("equipment stolen from your units comes back after the battle");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: stolen equipment's return not applied: {e.Message}");
            }
        }
        if (rules.Remove("victory-bonus")) // whatever the option: a save's own can change it
        {
            try
            {
                VictoryBonus.Install(game, Options.Current.VictoryBonus);
                enabled.Add($"Bonus EXP & JP on Victory ({Options.Current.VictoryBonus})");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: Bonus EXP & JP on Victory not applied: {e.Message}");
            }
        }
        if (rules.Remove("move-find"))
        {
            try
            {
                MoveFind.Install(game);
                enabled.Add("Move-Find panels give their common item again in later battles (the rare only on a first find)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the Move-Find rework not applied: {e.Message}");
            }
        }
        if (rules.Remove("nemesis")) // whatever the option: a save's own can change it
        {
            try
            {
                Nemesis.Install(game);
                enabled.Add("the Nemesis System (a downed generic enemy comes back by name)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the Nemesis System not applied: {e.Message}");
            }
        }
        if (rules.Remove("remixed-events"))
        {
            try
            {
                RemixedEvents.Install(game);
                enabled.Add("ReMixed's event scripts (the ones the mod ships) run with ReMixed's own instructions");
                enabled.Add(RemixedEventGraphics.Install(game));
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: ReMixed's event instructions not applied: {e.Message}");
            }
        }
        if (rules.Remove("pack-files"))
        {
            try
            {
                PackFiles.Install(game);
                enabled.Add("the mod's fftpack files (the battle data) given to the game by number");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the mod's fftpack files not given: {e.Message}");
            }
        }
        if (rules.Remove("remixed-sprites"))
        {
            try
            {
                enabled.Add(RemixedSprites.Install(game, data.GetProperty("sprites")));
                if (data.TryGetProperty("sprite-aliases", out var aliases))
                    enabled.Add(RemixedSprites.InstallAliases(game, aliases));
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: ReMixed's unit art not given: {e.Message}");
            }
        }
        if (rules.Remove("remixed-faces"))
        {
            try
            {
                enabled.Add(RemixedFaces.Install(game, data.GetProperty("faces")));
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: ReMixed's characters' faces not given: {e.Message}");
            }
        }
        if (rules.Remove("old-guests"))
        {
            try
            {
                enabled.Add(OldGuests.Install(game, data.GetProperty("old-guests")));
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: guests saved on the old sprite sets not moved: {e.Message}");
            }
        }
        if (rules.Remove("remixed-scenarios"))
        {
            try
            {
                RemixedScenarios.Install(game);
                enabled.Add("ReMixed's own scenarios (its missions, side stories and campaigns) in the remaster's tables");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: ReMixed's scenarios not added: {e.Message}");
            }
        }
        if (rules.Remove("unit-presets"))
        {
            try
            {
                UnitPresets.Install(game);
                enabled.Add("ReMixed's unit presets: its story and mission units' statuses, abilities, elements and stats");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: ReMixed's unit presets not applied: {e.Message}");
            }
        }
        if (rules.Remove("story-ai"))
        {
            try
            {
                StoryAi.Install(game);
                enabled.Add("ReMixed's story units that hold a post (Defensive, Stay Near XY) don't run away");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: ReMixed's story AI not applied: {e.Message}");
            }
        }
        if (rules.Remove("separate-saves") && Options.Current.SeparateSaves)
        {
            try
            {
                SaveFolder.Install(game);
                enabled.Add($"saves in their own folder (My Games/{SaveFolder.Folder})");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: separate saves not applied: {e.Message}");
            }
        }
        if (rules.Remove("formation-split"))
        {
            try
            {
                FormationStart.Install(game);
                enabled.Add("a split formation's second group can be left empty");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the split formation's empty group not applied: {e.Message}");
            }
        }
        if (rules.Remove("six-places"))
        {
            try
            {
                SixPlaces.Install(game);
                enabled.Add($"up to {SixPlaces.Places} of the player's units in a battle with two groups");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: six places not applied: {e.Message}");
            }
        }
        if (rules.Remove("breeding-errands"))
        {
            try
            {
                Breeding.Install(game);
                enabled.Add("units away on an errand sit out the daily monster step (no breeding or hatching)");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: breeding without units away not applied: {e.Message}");
            }
        }
        if (rules.Remove("movement-bonus"))
        {
            try
            {
                MovementBonus.Install(game);
                enabled.Add("Athlete (0x1F7) adds 1 to Jump and Pass (0x1FC) 1 to Move");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: Athlete's and Pass's Move and Jump not applied: {e.Message}");
            }
        }
        if (rules.Remove("start-statuses"))
        {
            try
            {
                StartStatuses.Install(game);
                enabled.Add("an item's start statuses skip what the unit is immune to, and innate statuses are current");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: equipment start statuses not applied: {e.Message}");
            }
        }
        if (rules.Remove("charge-lock"))
        {
            try
            {
                ChargeLock.Install(game);
                enabled.Add("a charge aimed from where the unit stands (weapon range, an area round it, a gun skill) uses up its move");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the charge lock not applied: {e.Message}");
            }
        }
        if (rules.Remove("unlearn"))
        {
            try
            {
                Unlearn.Install(game);
                if (Unlearn.BranchAt != 0)
                    enabled.Add($"choosing a learned ability unlearns it for its JP back ({Options.Current.JpRefund})");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: unlearning for JP not applied: {e.Message}");
            }
        }
        if (rules.Remove("dismissal-poach"))
        {
            try
            {
                Dismissal.Install(game);
                enabled.Add("dismissing a monster poaches it when a party member has Secret Hunt");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the dismissal's poach not applied: {e.Message}");
            }
        }
        if (rules.Remove("katana-break"))
        {
            try
            {
                KatanaBreak.Install(game);
                enabled.Add("a katana breaks only for Draw Out, never for a Samurai or with Maintenance or Adeptness");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the katana break not applied: {e.Message}");
            }
        }
        if (rules.Remove("reflect-reaction"))
        {
            try
            {
                ReflectReaction.Install(game);
                enabled.Add("the Reflect reaction reflects spells like the status");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the Reflect reaction not applied: {e.Message}");
            }
            try
            {
                ReflectReaction.InstallBounce(game);
                enabled.Add("a reflected spell bounces back onto its caster's panel");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the reflect bounce not applied: {e.Message}");
            }
        }
        if (rules.Remove("learn-flag"))
        {
            try
            {
                Learning.InstallMarkFix(game);
                enabled.Add("a unit reached again keeps its chance to learn");
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: the learn flag fix not applied: {e.Message}");
            }
        }
        foreach (var rule in rules)
            game.Log.Warn($"rules: \"{rule}\" isn't implemented in this DLL; rebuild the mod");
        game.Log.Info($"rules: {(enabled.Count == 0 ? "none" : string.Join(", ", enabled))}");
        TestRecorder.Installed(enabled);
    }

    // ---------------------------------------------------------------- unevadable attacks

    /// <summary>
    /// After the target's evades are set up (ReMixed's 0x80155E1C and 0x80155E40, from the
    /// Concentrate check at 0x801852EC): they're zeroed if the attacker is Transparent, or the
    /// ability is one ReMixed's table marks (0x8015E690, bit 0x80). Concentrate stays the
    /// game's own check, right after.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void EvadeSetup()
    {
        _evadeSetup();
        try
        {
            var attacker = *_game.Battle.Attacker;
            var unevadable = attacker != null && IsUnevadable(attacker, *_game.Battle.AbilityId);
            if (EvadeChecks.Unevadable != null)
                *EvadeChecks.Unevadable = (byte)(unevadable ? 1 : 0); // a blind attacker's +25 rather than +50
            if (unevadable)
            {
                *_code.Evades = 0;
                Tracer.Line(_game.Log, $"rules: ability 0x{*_game.Battle.AbilityId:X3} can't be evaded");
            }
        }
        catch (Exception e)
        {
            Failed("unevadable attacks", e);
        }
    }

    /// <summary>The attacker is Transparent (status byte 2, 0x10), or the ability is marked unevadable.</summary>
    internal static bool IsUnevadable(byte* attacker, int ability) =>
        (attacker[Battle.UnitStatuses + 2] & 0x10) != 0 || UnevadableAbilities.Contains(ability);

    // ---------------------------------------------------------------- team-only targeting

    /// <summary>
    /// Puts a stub in every formula table entry, so each formula call goes through
    /// <see cref="Dispatch"/> with its id. Returns how many. ReMixed's check sits on the
    /// formula call (PS1 0x8018B974, jumping to 0x80160F00); the remaster's call reads the
    /// table directly, and the weapon-formula dispatcher reads it too, so the table is where
    /// both can be caught. Install after the formulas patch, so ReMixed's formulas are what
    /// the stubs call.
    /// </summary>
    private static int InstallFormulaStubs(Game game)
    {
        var image = game.Image;
        var table = game.Battle.FormulaTable;
        var count = 0;
        while (count < 0x100 && (image.IsCode(image.StoredCode(table[count])) || Formulas.Replaced(count + 1)))
            count++;
        if (count < 0x64)
            throw new InvalidDataException($"the formula table has only {count} entries");
        _formulas = new nint[count + 1];
        var stubs = GameImage.AllocateCode(count * StubSize);
        var dispatch = (ulong)(nint)(delegate* unmanaged<int, void>)&Dispatch;
        for (var id = 1; id <= count; id++)
        {
            _formulas[id] = image.StoredCode(table[id - 1]);
            var stub = stubs + (id - 1) * StubSize;
            new Span<byte>(stub, StubSize).Fill(0xCC);
            stub[0] = 0xB9; // mov ecx, id
            *(int*)(stub + 1) = id;
            stub[5] = 0x48; // mov rax, Dispatch
            stub[6] = 0xB8;
            *(ulong*)(stub + 7) = dispatch;
            stub[15] = 0xFF; // jmp rax
            stub[16] = 0xE0;
        }
        GameImage.Unprotected((nint)table, count * sizeof(nint), () =>
        {
            for (var id = 1; id <= count; id++)
                table[id - 1] = (nint)(stubs + (id - 1) * StubSize);
        });
        return count;
    }

    /// <summary>For the offline check: what a formula's stub calls, swapped for another routine. Returns the one it had.</summary>
    internal static nint SwapFormula(int id, nint routine)
    {
        var had = _formulas[id];
        _formulas[id] = routine;
        return had;
    }

    /// <summary>A formula call: the team check, then the formula (or a miss instead of it).</summary>
    [UnmanagedCallersOnly]
    private static void Dispatch(int formula)
    {
        // A rider's mount skill: the mount is the attacker from here (ReMixed swaps before its team check).
        var swapped = false;
        RidingActions.Swap swap = default;
        try
        {
            swapped = RidingActions.SwapAttacker(out swap);
        }
        catch (Exception e)
        {
            RidingActions.Failed("the attacker swap", e);
        }
        try
        {
            if (_teamTargeting && TeamMisses())
            {
                if (swapped)
                    RidingActions.Restore(swap);
                Miss("misses a target on the wrong team");
                return;
            }
        }
        catch (Exception e)
        {
            Failed("team-only targeting", e);
        }
        var outer = _vanillaStatusRoll;
        _vanillaStatusRoll = formula is 0x02 or 0x05 ? formula : 0;
        _category = 0; // ReMixed resets it before each formula call (0x8018B928)
        // ReMixed clears the attack's angle at the action's start (0x8018B384), so a strike that skips the evade
        // check (a gun's Mug, say) can't take an earlier attack's "from behind" for Banditry.
        if (Banditry.Facing != null)
            *Banditry.Facing = 0;
        ((delegate* unmanaged<void>)_formulas[formula])();
        _vanillaStatusRoll = outer;
        if (swapped)
            RidingActions.Restore(swap);
    }

    /// <summary>
    /// ReMixed's team check (PS1 0x80160F94): the ability's attribute byte 4 has 0x80 for
    /// abilities that only help allies (Moogle, Fairy, the songs) and 0x40 for ones that only
    /// hit enemies (Shiva, Ramuh…), which vanilla only used for targeting. With 0x80 a target
    /// on another team is missed, with 0x40 one on the attacker's team (+0x1EE, PS1 +0x1BA).
    /// Abilities without attributes (above 0x16F) aren't checked.
    /// </summary>
    internal static bool TeamMisses()
    {
        var record = _code.GetAbilityAction(*_game.Battle.AbilityId);
        if (record == null)
            return false;
        var only = record[4] & 0xC0;
        var attacker = *_game.Battle.Attacker;
        var target = *_game.Routines.Target;
        if (only == 0 || attacker == null || target == null)
            return false;
        var sameTeam = ((attacker[Battle.UnitTeam] ^ target[Battle.UnitTeam]) & 0x30) == 0;
        return ((only & 0x80) != 0 && !sameTeam) || ((only & 0x40) != 0 && sameTeam);
    }

    /// <summary>A miss (PS1 0x8018430C, inlined all over the remaster): no hit, result type 7, no status chance.</summary>
    private static void Miss(string why)
    {
        var result = *_game.Battle.TargetResult;
        result[0] = 0;
        result[2] = 7;
        *(ushort*)(result + Battle.ResultStatusChance) = 0;
        Tracer.Line(_game.Log, $"rules: ability 0x{*_game.Battle.AbilityId:X3} {why}");
    }

    // ---------------------------------------------------------------- status chance tiers

    /// <summary>
    /// ReMixed's chance tiers (0x8015C700, at the status step's start, PS1 0x80187F3C): the low
    /// nibble of the set's flags, unused in vanilla, picks a chance (8: 75%, 4: 50%, 2: 25%,
    /// 1: 10%), or the action's category does: 19 after formula 0x02's 19% roll, whatever the tier.
    /// Unless the AI is simulating or the set is Separate, a failed roll is a miss and the step is
    /// skipped. Through the outer step (most formulas' tails) the miss is undone and only the
    /// statuses fail; status-only formulas call this step directly, and miss. A Separate set rolls
    /// each status at the same chance instead (vanilla 24%; 100 with neither a tier nor a category;
    /// PS1 0x801880EC), through <see cref="SeparateChance"/>.
    /// </summary>
    /// <remarks>
    /// The mod loader's status sets can't hold the tier bits, so they come from ReMixed's data
    /// by set id: the one the action setup copied, while the copy is still that set. ReMixed
    /// also keeps the tier as a category for a tag in the battle info window; not ported.
    /// </remarks>
    [UnmanagedCallersOnly]
    private static void StatusStep()
    {
        try
        {
            var copy = _code.StatusCopy;
            var tier = TierOf(*_code.StatusId, copy);
            var chance = ChanceFor(tier, _category);
            if (SeparateChance != null)
                *SeparateChance = (byte)chance;
            if ((tier != 0 || _category == 19) && *_game.Routines.Simulating == 0 && (copy[0] & 0x20) == 0
                && _game.TurnCode.Roll(100, chance) != 0)
            {
                Miss($"misses: its {chance}% status chance failed");
                TierMisses++;
                return;
            }
        }
        catch (Exception e)
        {
            Failed("status chance tiers", e);
        }
        _statusStep();
    }

    /// <summary>The tier of the set in the action's copy, if the copy is still the set <paramref name="id"/> (else none).</summary>
    internal static int TierOf(int id, byte* copy)
    {
        var tier = Tiers[id];
        if (tier == 0)
            return 0;
        var set = _code.StatusSets + id * 6;
        if ((set[0] & 0xF0) != (copy[0] & 0xF0) || *(uint*)(set + 1) != *(uint*)(copy + 1) || set[5] != copy[5])
            return 0;
        return tier;
    }

    internal static int TierChance(int tier) => (tier & 8) != 0 ? 75 : (tier & 4) != 0 ? 50 : (tier & 2) != 0 ? 25 : 10;

    /// <summary>The status step's chance (ReMixed's s4): category 19 first, then the tier's, else 100.</summary>
    internal static int ChanceFor(int tier, int category) => category == 19 ? 19 : tier != 0 ? TierChance(tier) : 100;

    /// <summary>For the offline check: set the category, as formula 0x02's roll does.</summary>
    internal static void SetCategory(int category) => _category = category;

    /// <summary>
    /// A Separate set's per-status roll in the status step (PS1 0x801880E8: vanilla rolls 24, ReMixed its s4; the
    /// remaster's 0x306AEE, `mov edx, 0x18; lea ecx, [rdx+0x4C]` before the roll) jumps to a stub that loads the
    /// chance from <see cref="SeparateChance"/>.
    /// </summary>
    private static void InstallSeparateChance(Game game)
    {
        // mov edx, 24; lea ecx, [rdx+0x4C] (100); call roll; test eax, eax; jne clear; cmp [simulating], 2; jne; xor [bit]
        var at = game.Image.FindUnique("BA 18 00 00 00 8D 4A 4C E8 ?? ?? ?? ?? 85 C0 75 09 83 3D ?? ?? ?? ?? 02 75 05 44 30 7C 3E 51");
        if (GameImage.Rip(at + 8, 1, 5) != (nint)_game.TurnCode.Roll || (int*)GameImage.Rip(at + 17, 2, 7) != _game.Routines.Simulating)
            throw new InvalidDataException("the Separate sets' roll isn't the one this DLL expects");
        if ((ulong)(at - (nint)_code.StatusStep) > 0x200)
            throw new InvalidDataException("the Separate sets' roll isn't in the status step");
        SeparateChance = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
        *SeparateChance = 24;
        var stub = GameImage.AllocateCodeNear(at, 32);
        WriteSeparateStub(stub, SeparateChance, at + 8);
        byte[] patch = [0xE9, 0, 0, 0, 0, 0x90, 0x90, 0x90];
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(patch, 1);
        GameImage.WriteCode(at, patch);
        SeparateAt = at;
        SeparateStub = (nint)stub;
    }

    /// <summary>The stub: edx = the chance, ecx = 100, then on to the roll's call; changes nothing else.</summary>
    internal static void WriteSeparateStub(byte* stub, byte* chance, nint back)
    {
        var code = new List<byte> { 0x48, 0xBA };  // mov rdx, the chance's address
        code.AddRange(BitConverter.GetBytes((ulong)(nint)chance));
        code.AddRange([0x0F, 0xB6, 0x12]);         // movzx edx, byte [rdx]
        code.AddRange([0xB9, 100, 0, 0, 0]);       // mov ecx, 100
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);   // jmp [rip+0]: back
        code.AddRange(BitConverter.GetBytes((ulong)back));
        code.ToArray().CopyTo(new Span<byte>(stub, 32));
    }

    /// <summary>For the offline check: give a status set a tier.</summary>
    internal static void SetTier(int set, int tier) => Tiers[set] = (byte)tier;

    /// <summary>
    /// The hit's 19% status roll. ReMixed took it out of every place vanilla rolls it before a
    /// hit's statuses (0x8018868C, 0x8018872C, 0x801887D4, 0x80189D4C become `or v0, zero, zero`),
    /// so those statuses land and the set's own tier decides. Formula 0x02 (a weapon casting its
    /// spell) keeps a 19% roll for the spell (ReMixed adds 0x801884C0 at 0x80188BC4), and formula
    /// 0x05, which ReMixed rewrote around its gun skills, keeps vanilla's until that's ported.
    /// </summary>
    [UnmanagedCallersOnly]
    private static int HitStatusRoll()
    {
        if (_vanillaStatusRoll == 0)
            return 0;
        if (_vanillaStatusRoll == 0x02)
            _category = 19; // ReMixed's 0x801884C0 marks the 19% category, then rolls as vanilla does
        return _hitStatusRoll();
    }

    /// <summary>For the offline check: run the hit's status roll as formula <paramref name="formula"/> would.</summary>
    internal static int HitStatusRollIn(int formula, delegate* unmanaged<int> roll)
    {
        var outer = _vanillaStatusRoll;
        var category = _category;
        _vanillaStatusRoll = formula is 0x02 or 0x05 ? formula : 0;
        var result = roll();
        _vanillaStatusRoll = outer;
        _category = category;
        return result;
    }

    // ---------------------------------------------------------------- Defense and Warding Aura

    /// <summary>Defense UP's step (PS1 0x801862CC, ReMixed's version calls 0x80151580): Defense Aura.</summary>
    [UnmanagedCallersOnly]
    private static void DefenseUp() => WithAura(0x08, _defenseUp, "Defense Aura");

    /// <summary>Magic Defense UP's step (PS1 0x8018631C): Warding Aura.</summary>
    [UnmanagedCallersOnly]
    private static void MagicDefenseUp() => WithAura(0x02, _magicDefenseUp, "Warding Aura");

    /// <summary>
    /// The target gets its support's XA × 2/3 if it has the support or a unit next to it does
    /// (once, not twice). The support bit is set on the target for the step's call and cleared
    /// after, so the reduction happens where and how the game does it.
    /// </summary>
    private static void WithAura(byte support, delegate* unmanaged<void> step, string name)
    {
        byte* lent = null;
        try
        {
            var target = *_game.Routines.Target;
            if (target != null && (target[AuraSupports] & support) == 0 && NeighbourHas(target, support))
            {
                lent = target;
                target[AuraSupports] |= support;
            }
        }
        catch (Exception e)
        {
            Failed(name, e);
        }
        step();
        if (lent != null)
        {
            lent[AuraSupports] &= (byte)~support;
            Tracer.Line(_game.Log, $"rules: {name} from a neighbour, ability 0x{*_game.Battle.AbilityId:X3}, XA {*_game.Battle.XA}");
        }
    }

    /// <summary>
    /// ReMixed's 0x80151580: one of the four units next to the target (left, right, up, down)
    /// has the support, using its generalized neighbour check (vanilla 0x80182508, Monster
    /// Skill's): on the target's current team, not in status group 1, and within 5 of its height.
    /// </summary>
    internal static bool NeighbourHas(byte* target, byte support)
    {
        if (target[1] == 0xFF)
            return false;
        byte* info = stackalloc byte[16];
        int height = _code.TileInfo(info, target[Battle.UnitIndex])[6];
        int x = target[Battle.UnitX], y = target[Battle.UnitY];
        var team = target[Battle.UnitCurrentTeam];
        return Neighbour(team, x - 1, y, height, support, info) || Neighbour(team, x + 1, y, height, support, info)
            || Neighbour(team, x, y - 1, height, support, info) || Neighbour(team, x, y + 1, height, support, info);
    }

    private static bool Neighbour(byte team, int x, int y, int height, byte support, byte* info)
    {
        var map = _game.Map;
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
            return false;
        var turn = _game.TurnCode;
        for (var i = 0; i < Battle.UnitCount; i++)
        {
            var unit = turn.Units + i * Battle.UnitSize;
            if (unit[1] == 0xFF || unit[Battle.UnitX] != x || unit[Battle.UnitY] != y
                || turn.HasStatusIn(unit, 1) != 0 || (unit[AuraSupports] & support) == 0
                || ((unit[Battle.UnitCurrentTeam] ^ team) & 0x30) != 0)
                continue;
            if ((uint)(height - _code.TileInfo(info, unit[Battle.UnitIndex])[6] + 5) < 11)
                return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- Limit's faster charge

    private static bool _limitCharge;

    /// <summary>Hooks the action queue once: Limit's charge and Two Spells' slot (installed with the supports, first) share it.</summary>
    internal static void HookQueue(Game game)
    {
        if (_queueAction != null)
            return;
        _game = game;
        _code = game.RuleCode;
        _queueAction = (delegate* unmanaged<byte*, byte*, int, int>)game.Hooks.Hook(
            (nint)_code.QueueAction, (nint)(delegate* unmanaged<byte*, byte*, int, int>)&QueueAction, "the action queue");
    }

    /// <summary>
    /// Queueing an action (PS1 0x8017C45C, ReMixed's 0x8015D8B0 at 0x8017C868): for the Limit
    /// skillset, the charge time is cut before Short Charge. The game reads the ability's CT
    /// from its live attribute record once, so the record holds the cut CT for the call.
    /// </summary>
    [UnmanagedCallersOnly]
    private static int QueueAction(byte* record, byte* queued, int commit)
    {
        byte* ct = null;
        byte before = 0;
        try
        {
            if (_limitCharge && record != null && record[1] == LimitSkillset && record[0] < Battle.UnitCount)
            {
                var unit = _game.TurnCode.Units + record[0] * Battle.UnitSize;
                var ability = _code.GetAbilityAction(*(ushort*)(record + 2));
                if (ability != null && unit[1] != 0xFF)
                {
                    var full = ability[0xC] & 0x7F;
                    var cut = LimitCharge(full, *(ushort*)(unit + Battle.UnitHp), *(ushort*)(unit + Battle.UnitMaxHp));
                    if (cut != full)
                    {
                        ct = ability + 0xC;
                        before = *ct;
                        *ct = (byte)((before & 0x80) | cut);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Failed("Limit's charge", e);
        }
        TwoSpells.BeforeQueue(record, queued, commit);
        try
        {
            RidingActions.BeforeQueue(record, queued, commit);
        }
        catch (Exception e)
        {
            RidingActions.Failed("the queue", e);
        }
        try
        {
            Performances.BeforeQueue(record, queued, commit);
        }
        catch (Exception e)
        {
            Performances.Failed("the queue", e);
        }
        var queuedOk = _queueAction(record, queued, commit);
        if (ct != null)
        {
            Tracer.Line(_game.Log, $"rules: Limit 0x{*(ushort*)(record + 2):X3} charges in {*ct & 0x7F} instead of {before & 0x7F}");
            *ct = before;
        }
        TwoSpells.AfterQueue(record, queued, commit);
        try
        {
            RidingActions.AfterQueue(record, queued, commit);
        }
        catch (Exception e)
        {
            RidingActions.Failed("the queue", e);
        }
        try
        {
            Performances.AfterQueue(record, queued, commit, queuedOk);
        }
        catch (Exception e)
        {
            Performances.Failed("the queue", e);
        }
        try
        {
            ChargeLock.AfterQueue(record, queued, commit, queuedOk);
        }
        catch (Exception e)
        {
            Failed("the charge lock", e);
        }
        return queuedOk;
    }

    /// <summary>
    /// ReMixed's cut (PS1 0x8015D8B0): q = HP / (Max HP / 4), at least 1, and 3 − q cuts of a
    /// quarter of the original CT, each leaving at least 1. Full to 75% HP: none; 50–75%: CT − CT/4;
    /// below 50%: CT − 2 × CT/4 (the quarter is worked out once, so it's about half, not 0.5625).
    /// </summary>
    internal static int LimitCharge(int ct, int hp, int maxHp)
    {
        var q = hp / Math.Max(maxHp >> 2, 1);
        if (q == 0)
            q = 1;
        var quarter = ct >> 2;
        for (var cuts = 3 - q; cuts > 0; cuts--)
        {
            ct -= quarter;
            if (ct <= 0)
                ct = 1;
        }
        return ct;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"rules: {what} failed: {e}");
    }
}
