using IvaliceRemixed;
using IvaliceRemixed.Patches;
using EventInstructionUpgrade;
using Weapons = IvaliceRemixed.Patches.WeaponRequirements;
using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("usage: IvaliceRemixed.Check <FFT_enhanced.exe> <Data/patches.json> [live-hooks]");
    return 2;
}

var problems = 0;
var log = new Log(
    text => Console.WriteLine($"  {text}"),
    text => { problems++; Console.WriteLine($"  warning: {text}"); },
    text => { problems++; Console.WriteLine($"  error: {text}"); });
try
{
    var image = GameImage.FromFile(args[0]);
    var live = args.Length == 3 && args[2] == "live-hooks";
    IHooks hooks = live ? new LiveHooks(log) : new ReportedHooks(log);
    var game = new Game(image, log, hooks);
    // The Event Instruction Upgrade, its own mod in the game (loaded before ReMixed, which depends on it).
    try
    {
        EventHooks.Install(game);
    }
    catch (Exception e)
    {
        log.Error($"the Event Instruction Upgrade: not installed: {e.Message}");
    }
    RemixedEvents.Api = new Api();
    Tracer.Sink = TestRecorder.Note; // as ReMixed's startup has it
    Patcher.Run(game, args[1]);
    LevelScaling.BattleEntry = () => 0; // the game's variables aren't there to read
    if (live)
    {
        problems += SelfTest.HpDamage(game, log);
        problems += SelfTest.FirstMove(game, log);
        problems += SelfTest.MoveCleanse(game, log);
        problems += SelfTest.TeamTargeting(game, log);
        problems += SelfTest.Auras(game, log);
        problems += SelfTest.RuleArithmetic(game, log);
        problems += SelfTest.StatusTiers(game, log);
        problems += SelfTest.LearnSecondary(game, log);
        problems += SelfTest.TwoJobGrowth(game, log);
        problems += SelfTest.CounterSteal(game, log);
        problems += SelfTest.WeaponTypes(game, log);
        problems += SelfTest.GainRates(game, log);
        problems += SelfTest.BackAttacks(game, log);
        problems += SelfTest.StatStrike(log);
        problems += SelfTest.LearnedPassives(game, log);
        problems += SelfTest.KeptRegisters(game, log);
        problems += SelfTest.Landings(game, log);
        problems += SelfTest.DeadlyTiles(game, log);
        problems += SelfTest.MonsterSkills(game, log);
        problems += SelfTest.LimitPreviews(game, log);
        problems += SelfTest.MonsterReactionPicks(game, log);
        problems += SelfTest.JumpRanges(game, log);
        problems += SelfTest.DeathCounters(game, log);
        problems += SelfTest.OptionSettings(log);
        problems += SelfTest.LevelOptions(game, log);
        problems += SelfTest.BraveRule(game, log);
        problems += SelfTest.KnockbackRules(game, log);
        problems += SelfTest.WeaponRanges(game, log);
        problems += SelfTest.PoachingOdds(game, log);
        problems += SelfTest.SpellcastingOptions(game, log);
        problems += SelfTest.EachSavesOptions(log);
        problems += SelfTest.RevealTreasureRules(game, log);
        problems += SelfTest.RemixedTextRules(game, log);
        problems += SelfTest.TavernStorageRules(game, log);
        problems += SelfTest.OptionsPageList(game, Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(args[1])))!, log);
        problems += SelfTest.ZodiacRules(game, log);
        problems += SelfTest.EquipPermissions(game, log);
        problems += SelfTest.WeaponPicks(game, log);
        problems += SelfTest.JobLevelRules(game, log);
        problems += SelfTest.AbilityValidation(game, log);
        problems += SelfTest.ArrowGuardTypes(game, log);
        problems += SelfTest.HideUnlearnableSite(game, log);
        problems += SelfTest.SpilloverJp(game, log);
        problems += SelfTest.BraveReactionRules(game, log);
        problems += SelfTest.RandomEquipmentRules(game, log);
        problems += SelfTest.ElementalType(game, log);
        problems += SelfTest.InPlaceChanges(game, log);
        problems += SelfTest.StatOrStatusEffects(game, log);
        problems += SelfTest.AngleAndInvite(game, log);
        problems += SelfTest.EvasionAndOthers(game, log);
        problems += SelfTest.FormulaStepRules(game, log);
        problems += SelfTest.RewrittenFormulas(game, log);
        problems += SelfTest.AngleClear(game, log);
        problems += SelfTest.TwoSpellsCommands(game, log);
        problems += SelfTest.TwoSpellsTurn(game, log);
        problems += SelfTest.KatanaBreaks(game, log);
        problems += SelfTest.BreakFormula(game, log);
        problems += SelfTest.PassRule(game, log);
        problems += SelfTest.MovementModes(game, log);
        problems += SelfTest.ConeAreas(game, log);
        problems += SelfTest.ConeTargeting(game, log);
        problems += SelfTest.StartStatusRules(game, log);
        problems += SelfTest.UnitPresetRules(game, log);
        problems += SelfTest.StoryAiRules(game, log);
        problems += SelfTest.MovementBonusRules(game, log);
        problems += SelfTest.ErrandAndListRules(game, log);
        problems += SelfTest.MonsterWriteBackRule(game, log);
        problems += SelfTest.FormationStartRule(game, log);
        problems += SelfTest.SixPlacesRule(game, log);
        problems += SelfTest.ErrandLockRules(game, log);
        problems += SelfTest.OneHandingRules(game, log);
        problems += SelfTest.ErrandRules(game, log);
        problems += SelfTest.RidingBoardingRules(game, log);
        problems += SelfTest.RidingActionRules(game, log);
        problems += SelfTest.HittingBodyRules(game, log);
        problems += SelfTest.ArcObstacleRules(game, log);
        problems += SelfTest.TakenEquipmentRules(game, log);
        problems += SelfTest.VictoryBonusRules(game, log);
        problems += SelfTest.MoveFindRules(game, log);
        problems += SelfTest.NemesisRules(game, log);
        problems += SelfTest.TestModeRules(game, log);
        problems += SelfTest.UnlearnInMenu(game, log);
        problems += SelfTest.SeparateSaves(game, log);
        problems += SelfTest.FastForwardToggleRules(game, log);
        problems += SelfTest.DeployedLevels(game, log);
        problems += SelfTest.EventLogStub(game, log);
        problems += SelfTest.RemixedEventScripts(game, log);
        problems += SelfTest.UnitLooksRule(game, log);
        problems += SelfTest.PerformerJobRules(game, log);
        problems += SelfTest.DismissByNameRule(game, log);
        problems += SelfTest.SideChapterRepair(log);
        problems += SelfTest.EventUnitNumbers(game, log);
        problems += SelfTest.RemixedScenarioTables(game, log);
        problems += SelfTest.PackFileGiving(log);
        problems += SelfTest.UnitSprites(game, log);
        problems += SelfTest.EventGraphicTextures(game, log);
        problems += SelfTest.FaceFold(log);
        problems += SelfTest.FacePortraits(game, log);
        problems += SelfTest.OldGuestMoves(game, log);
        problems += SelfTest.RemixedMessageMoving(log);
        problems += SelfTest.RemixedConditionTypes(log);
        problems += SelfTest.RemixedWalkOrder(log);
        problems += SelfTest.MenuBattleGroups(log);
        problems += SelfTest.RemixedPhaseBlocks(log);
        problems += SelfTest.RemixedRosterSwap(log);
        problems += SelfTest.RemixedMirrorCopy(log);
        problems += SelfTest.RemixedCreateUnit(game, log);
        problems += SelfTest.RemixedAlgusPieces(log);
        problems += SelfTest.CrashLogFilter(log);
        problems += SelfTest.WatchHandler(log);
        problems += SelfTest.AutopilotUiStub(log);
        problems += SelfTest.AutopilotPadWrapper(log);
        problems += SelfTest.JobChangeSecondary(game, log);
        problems += SelfTest.JobChangeInnates(game, log);
        problems += SelfTest.DismissalPoach(game, log);
        problems += SelfTest.Unlearning(game, log);
        problems += SelfTest.ChargeLocks(game, log);
        problems += SelfTest.TwoShotsLists(game, log);
        problems += SelfTest.HarnessAct(game, log);
        problems += SelfTest.GuestControls(game, log);
        problems += SelfTest.ReflectBounces(game, log);
        problems += SelfTest.GunSkillRules(game, log);
        problems += SelfTest.DragonTechRules(game, log);
    }
}
catch (Exception e)
{
    problems++;
    Console.WriteLine($"  error: {e}");
}
return problems == 0 ? 0 : 1;

/// <summary>Installs the real hooks on the mapped copy, then checks each entry now jumps away.</summary>
internal sealed unsafe class LiveHooks(Log log) : IHooks
{
    private readonly List<IHook<NativeAction>> _active = [];

    public nint Hook(nint function, nint detour, string name)
    {
        var hook = ReloadedHooks.Instance.CreateHook<NativeAction>((void*)detour, function).Activate();
        _active.Add(hook);
        var entry = (byte*)function;
        // jmp rel32, jmp [rip+disp32], or jmp [abs32] (Reloaded.Hooks' pointer in low memory)
        var jumps = entry[0] == 0xE9 || (entry[0] == 0xFF && (entry[1] == 0x25 || (entry[1] == 0x24 && entry[2] == 0x25)));
        if (!jumps)
            throw new InvalidOperationException($"hooking {name} didn't put a jump at its entry: " +
                string.Join(" ", Enumerable.Range(0, 16).Select(k => entry[k].ToString("X2"))));
        log.Info($"hooks: hooked {name} at 0x{function:X}; the original runs from 0x{hook.OriginalFunctionAddress:X}");
        return hook.OriginalFunctionAddress;
    }
}

[Function(CallingConventions.Microsoft)]
internal delegate void NativeAction();

/// <summary>
/// Runs hooked game code on the check's copy of the exe with made-up battle
/// state, and compares the results with ReMixed's arithmetic. Only leaf steps
/// that touch nothing but their own globals can run this way.
/// </summary>
internal static unsafe class SelfTest
{
    /// <summary>The HP damage step (XA × YA) through the supports hook: Adeptness and Vehemence.</summary>
    public static int HpDamage(Game game, Log log)
    {
        var b = game.Battle;
        var r = game.Routines;
        var attacker = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        *b.Attacker = attacker;
        *r.Target = target;
        *b.TargetResult = result;
        *b.EffectiveFormula = 1;
        var failures = 0;
        void Case(string name, byte attackerAdept, byte attackerVehement, byte targetVehement, int expected)
        {
            attacker[Battle.UnitSupports + 3] = attackerAdept;
            attacker[Battle.UnitSupports + 2] = attackerVehement;
            target[Battle.UnitSupports + 2] = targetVehement;
            *b.XA = 10;
            *b.YA = 20;
            *(ushort*)(result + Battle.HpDamage) = 0;
            r.HpDamage();
            var got = *(ushort*)(result + Battle.HpDamage);
            var ok = got == expected && result[Battle.ResultFlags] == 0x80;
            if (!ok) failures++;
            log.Info($"self-test: HP damage 10 × 20, {name}: {got} (expected {expected}){(ok ? "" : ", WRONG")}");
        }
        Case("no supports", 0, 0, 0, 200);
        Case("attacker has Adeptness", 0x02, 0, 0, 240);
        Case("attacker has Vehemence", 0, 0x10, 0, 300);
        Case("target has Vehemence", 0, 0, 0x10, 300);
        Case("all three", 0x02, 0x10, 0x10, 440);
        // The power settings: by the attacker's team, Math Skill's own pair, after the supports, at least 1.
        var power = new Options
        {
            PowerMode = Options.PowerModeRule.Custom, AllyPower = 150, EnemyPower = 50, AllyMathPower = 200, EnemyMathPower = 10,
        };
        attacker[Battle.UnitTeam] = 0;
        var ally = (Supports.Power(attacker, 0x07, power), Supports.Power(attacker, 0x15, power));
        attacker[Battle.UnitTeam] = 0x10;
        var enemy = (Supports.Power(attacker, 0x07, power), Supports.Power(attacker, 0x15, power));
        var powerOk = ally == (150, 200) && enemy == (50, 10) && Supports.Scaled(200, 100, 150) == 300
                      && Supports.Scaled(200, 120, 50) == 120 && Supports.Scaled(3, 100, 10) == 1 && Supports.Scaled(0, 100, 150) == 0;
        if (!powerOk) failures++;
        log.Info($"self-test: HP damage, the power settings: ally {ally}, enemy {enemy} (normal, Math); 200 at 150% 300, " +
                 $"at 120% then 50% 120, 3 at 10% 1, 0 stays 0{(powerOk ? "" : ", WRONG")}");
        Check(!new Options().ChangesPower && power.ChangesPower);
        void Check(bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: HP damage, the defaults change no damage{(ok ? "" : ", WRONG")}");
        }
        // The presets, as ReMixed's menu writes var 0x16, and the custom percentages only with Custom.
        var presets = new[] { Options.PowerModeRule.Squire, Options.PowerModeRule.Tactician, Options.PowerModeRule.Chemist }
            .Select(m => new Options { PowerMode = m, AllyPower = 200 }.Percentages).ToArray();
        var presetsOk = presets[0] == (125, 75, 125, 75) && presets[1] == (75, 125, 50, 125) && presets[2] == (50, 50, 50, 50)
                        && !new Options { AllyPower = 200 }.ChangesPower
                        && Options.Parse("{\"AllyPower\": 150}").PowerMode == Options.PowerModeRule.Custom
                        && Options.Parse("{\"AllyPower\": 150, \"PowerMode\": \"Knight\"}").PowerMode == Options.PowerModeRule.Knight
                        && Options.Parse("{}").PowerMode == Options.PowerModeRule.Knight;
        if (!presetsOk) failures++;
        log.Info($"self-test: HP damage, the power modes: Squire {presets[0]}, Tactician {presets[1]}, Chemist {presets[2]}; " +
                 $"an older config with percentages reads as Custom{(presetsOk ? "" : ", WRONG")}");
        *b.Attacker = null;
        *r.Target = null;
        *b.TargetResult = null;
        return failures;
    }

    /// <summary>
    /// First Move's turn count, through the end-of-turn hook (the game's routine is a leaf),
    /// and when its Move × 2 applies. The movement setup itself reads settings through
    /// pointers that only the running game has, so it isn't run here.
    /// </summary>
    public static int FirstMove(Game game, Log log)
    {
        var code = game.TurnCode;
        var simulating = game.Routines.Simulating;
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: First Move, {name}{(ok ? "" : ": WRONG")}");
        }
        for (var i = 0; i < Battle.UnitCount; i++)
            code.Units[i * Battle.UnitSize + 1] = 0xFF;
        const int slot = 3;
        var unit = code.Units + slot * Battle.UnitSize;
        unit[1] = slot;
        unit[Battle.UnitMovement + 1] = 0x02;
        Movement.ResetTurns();
        Check("applies before the unit's first turn ends", Movement.FirstMoveApplies(slot));
        unit[Battle.UnitTurn] = 1;
        *simulating = 1;
        code.EndOfTurn(slot);
        Check("an AI simulation of the end of turn doesn't count", Movement.TurnsEndedBy(slot) == 0);
        unit[Battle.UnitTurn] = 1;
        *simulating = 0;
        var returned = code.EndOfTurn(slot);
        Check($"the end of turn counts the turn (returned {returned}, turn flag now {unit[Battle.UnitTurn]})",
              Movement.TurnsEndedBy(slot) == 1 && returned == 1 && unit[Battle.UnitTurn] == 0);
        Check("doesn't apply after it", !Movement.FirstMoveApplies(slot));
        code.EndOfTurn(slot);
        Check("an end of turn when it isn't the unit's turn doesn't count", Movement.TurnsEndedBy(slot) == 1);
        Movement.ResetTurns();
        Check("applies again in the next battle", Movement.FirstMoveApplies(slot));
        unit[Battle.UnitMovement + 1] = 0;
        Check("needs the ability", !Movement.FirstMoveApplies(slot));
        System.Runtime.InteropServices.NativeMemory.Clear(code.Units, Battle.UnitCount * Battle.UnitSize);
        return failures;
    }

    /// <summary>
    /// Move-Cleanse through the post-move ability's hook. For a unit whose only movement
    /// ability is Move-Cleanse, the game's routine only clears the unit's result and checks
    /// its statuses, so it runs here; then the hook adds the cleanse.
    /// </summary>
    public static int MoveCleanse(Game game, Log log)
    {
        var code = game.TurnCode;
        var unit = code.Units + 2 * Battle.UnitSize;
        unit[1] = 2;
        unit[Battle.UnitMovement + 1] = 0x01;
        var failures = 0;
        void Case(string name, byte[] statuses, byte[] removed)
        {
            for (var i = 0; i < 5; i++)
                unit[Battle.UnitStatusCopy + i] = statuses[i];
            var returned = code.PostMove(unit);
            var result = unit + Battle.UnitResult;
            var got = Enumerable.Range(0, 5).Select(i => result[Battle.ResultStatusRemoved + i]).ToArray();
            var flagged = (result[Battle.ResultFlags] & 0x08) != 0;
            var ok = returned == 0 && got.SequenceEqual(removed) && flagged == removed.Any(b => b != 0);
            if (!ok) failures++;
            log.Info($"self-test: Move-Cleanse, {name}: removes {Convert.ToHexString(got)}, status change {flagged}, " +
                     $"returned {returned} (expected {Convert.ToHexString(removed)}){(ok ? "" : ", WRONG")}");
        }
        // The bad statuses, with Dead, Treasure, Float, Stop and Faith, which it leaves.
        Case("bad statuses and others", [0x30, 0x3D, 0xCA, 0x86, 0xE5], [0x10, 0x3C, 0x8A, 0x84, 0x65]);
        Case("only others", [0x20, 0x01, 0x40, 0x02, 0x80], [0, 0, 0, 0, 0]);

        // The turn-over check after the unit has moved (+0x1B9 = 3) but not acted, with Don't Act.
        // The game's check and its can't-act test are leaves.
        void Turn(string name, byte movement, byte linked, byte pendingRemoval, int expected)
        {
            System.Runtime.InteropServices.NativeMemory.Clear(unit, Battle.UnitSize);
            unit[1] = 2;
            unit[Battle.UnitIndex] = 2;
            unit[Battle.UnitMoved] = 3;
            unit[Battle.UnitStatuses + 4] = 0x04;
            unit[Battle.UnitMovement + 1] = movement;
            unit[Battle.UnitLinked] = linked;
            unit[Battle.UnitResult + Battle.ResultStatusRemoved + 4] = pendingRemoval;
            var over = code.TurnOver(unit);
            var ok = over == expected && unit[Battle.UnitStatuses + 4] == 0x04;
            if (!ok) failures++;
            log.Info($"self-test: turn over after moving with Don't Act, {name}: {over} (expected {expected}), " +
                     $"Don't Act kept: {unit[Battle.UnitStatuses + 4] == 0x04}{(ok ? "" : ", WRONG")}");
        }
        Turn("Move-Cleanse can still act", 0x01, 0, 0, 0);
        Turn("without it", 0, 0, 0, 1);
        Turn("without it, but the unit's result removes Don't Act", 0, 0, 0x04, 0);
        Turn("Move-Cleanse while riding", 0x01, 0x80, 0, 1);
        System.Runtime.InteropServices.NativeMemory.Clear(code.Units, Battle.UnitCount * Battle.UnitSize);
        *game.Routines.Target = null;
        *game.Battle.TargetResult = null;
        return failures;
    }

    private static bool _formulaRan;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void TestFormula() => _formulaRan = true;

    /// <summary>
    /// Team-only targeting through a formula table entry: the game's call goes into the stub,
    /// the stub into the team check, and a test routine stands in for the formula. The live
    /// ability table is built from the game's data on first use, which this copy doesn't have,
    /// so it's marked built and the test writes the ability's flags itself.
    /// </summary>
    public static int TeamTargeting(Game game, Log log)
    {
        var b = game.Battle;
        var r = game.Routines;
        var code = game.RuleCode;
        var units = game.TurnCode.Units;
        *code.AbilitiesReady = 1;
        const int ability = 0x03C, formula = 0x30;
        var record = code.GetAbilityAction(ability);
        var flagsBefore = record[4];
        var attacker = units;
        var target = units + Battle.UnitSize;
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        *b.Attacker = attacker;
        *r.Target = target;
        *b.TargetResult = result;
        *b.AbilityId = ability;
        var had = Rules.SwapFormula(formula, (nint)(delegate* unmanaged<void>)&TestFormula);
        var failures = 0;
        void Case(string name, byte only, byte attackerTeam, byte targetTeam, bool runs)
        {
            record[4] = (byte)((flagsBefore & 0x3F) | only);
            attacker[Battle.UnitTeam] = attackerTeam;
            target[Battle.UnitTeam] = targetTeam;
            result[0] = 1;
            result[2] = 0;
            *(ushort*)(result + Battle.ResultStatusChance) = 100;
            _formulaRan = false;
            ((delegate* unmanaged<void>)b.FormulaTable[formula - 1])();
            var missed = result[0] == 0 && result[2] == 7 && *(ushort*)(result + Battle.ResultStatusChance) == 0;
            var ok = _formulaRan == runs && missed == !runs;
            if (!ok) failures++;
            log.Info($"self-test: team-only targeting, {name}: {(_formulaRan ? "formula ran" : "")}{(missed ? "missed" : "")}" +
                     $"{(ok ? "" : ", WRONG")}");
        }
        Case("an ally-only ability (0x80) on an ally", 0x80, 0x00, 0x00, true);
        Case("an ally-only ability on an enemy", 0x80, 0x00, 0x10, false);
        Case("an enemy-only ability (0x40) on an ally", 0x40, 0x10, 0x10, false);
        Case("an enemy-only ability on an enemy", 0x40, 0x00, 0x20, true);
        Case("an ability for anyone", 0x00, 0x00, 0x10, true);
        Rules.SwapFormula(formula, had);
        record[4] = flagsBefore;
        *b.Attacker = null;
        *r.Target = null;
        *b.TargetResult = null;
        System.Runtime.InteropServices.NativeMemory.Free(result);
        System.Runtime.InteropServices.NativeMemory.Clear(units, Battle.UnitCount * Battle.UnitSize);
        return failures;
    }

    /// <summary>
    /// Defense and Warding Aura through the hooked Defense UP and Magic Defense UP steps, which
    /// are leaves, as are the tile info and status-group checks the neighbour test uses.
    /// </summary>
    public static int Auras(Game game, Log log)
    {
        var b = game.Battle;
        var r = game.Routines;
        var code = game.RuleCode;
        var units = game.TurnCode.Units;
        game.Map.SetSize(16, 16);
        for (var i = 0; i < Battle.UnitCount; i++)
            units[i * Battle.UnitSize + 1] = 0xFF;
        var target = units;
        var other = units + Battle.UnitSize;
        target[1] = 0;
        target[Battle.UnitIndex] = 0;
        target[Battle.UnitX] = 5;
        target[Battle.UnitY] = 5;
        other[1] = 1;
        other[Battle.UnitIndex] = 1;
        *r.Target = target;
        const int supports = Battle.UnitSupports + 1;
        var failures = 0;
        void Case(string name, delegate* unmanaged<void> step, byte targetHas, byte otherHas, int x, int y, byte team, int expected)
        {
            target[supports] = targetHas;
            other[supports] = otherHas;
            other[Battle.UnitX] = (byte)x;
            other[Battle.UnitY] = (byte)y;
            other[Battle.UnitCurrentTeam] = team;
            *b.XA = 30;
            step();
            var ok = *b.XA == expected && target[supports] == targetHas;
            if (!ok) failures++;
            log.Info($"self-test: auras, {name}: XA 30 -> {*b.XA} (expected {expected}){(ok ? "" : ", WRONG")}");
        }
        Case("the target has Defense UP", code.DefenseUp, 0x08, 0, 9, 9, 0, 20);
        Case("an ally next to it has Defense UP", code.DefenseUp, 0, 0x08, 6, 5, 0, 20);
        Case("the ally is two tiles away", code.DefenseUp, 0, 0x08, 7, 5, 0, 30);
        Case("the unit next to it is on another team", code.DefenseUp, 0, 0x08, 5, 6, 0x10, 30);
        Case("both have it, once", code.DefenseUp, 0x08, 0x08, 5, 4, 0, 20);
        Case("an ally next to it has Magic Defense UP", code.MagicDefenseUp, 0, 0x02, 4, 5, 0, 20);
        Case("Defense UP next to it, against magic", code.MagicDefenseUp, 0, 0x08, 4, 5, 0, 30);
        *r.Target = null;
        game.Map.SetSize(0, 0);
        System.Runtime.InteropServices.NativeMemory.Clear(units, Battle.UnitCount * Battle.UnitSize);
        return failures;
    }

    /// <summary>
    /// The rules' own arithmetic: Limit's cut charge time, who can't be evaded, and formula
    /// 0x1E's damage turned into healing. The game code around them (the action queue, the
    /// evade setup, the formula) reads the game's data, so it isn't run here.
    /// </summary>
    public static int RuleArithmetic(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: {name}{(ok ? "" : ": WRONG")}");
        }
        foreach (var (ct, hp, expected) in new[] { (8, 100, 8), (8, 75, 8), (8, 70, 6), (8, 50, 6), (8, 49, 4), (8, 1, 4),
                                                    (5, 60, 4), (5, 30, 3), (7, 30, 5), (3, 10, 3), (2, 10, 2) })
        {
            var got = Rules.LimitCharge(ct, hp, 100);
            Check($"Limit charge {ct} at {hp}% HP: {got} (expected {expected})", got == expected);
        }

        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        Check("an attacker without Transparent can be evaded", !Rules.IsUnevadable(unit, 0x001));
        unit[Battle.UnitStatuses + 2] = 0x10;
        Check("a Transparent attacker can't be evaded", Rules.IsUnevadable(unit, 0x001));
        unit[Battle.UnitStatuses + 2] = 0;
        Check("ReMixed's Aimed Shot (0x19C) can't be evaded", Rules.IsUnevadable(unit, 0x19C));
        System.Runtime.InteropServices.NativeMemory.Free(unit);

        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        *(ushort*)(result + Battle.HpDamage) = 120;
        result[Battle.ResultStatusAdded + 1] = 0x20;
        result[Battle.ResultFlags] = 0x88;
        Formulas.HealInstead(result);
        Check($"formula 0x1E on an ally: heals {*(ushort*)(result + Battle.HpHeal)}, damage {*(ushort*)(result + Battle.HpDamage)}, " +
              $"flags 0x{result[Battle.ResultFlags]:X2}",
              *(ushort*)(result + Battle.HpHeal) == 120 && *(ushort*)(result + Battle.HpDamage) == 0
              && result[Battle.ResultStatusAdded + 1] == 0 && result[Battle.ResultFlags] == 0x40);
        System.Runtime.InteropServices.NativeMemory.Free(result);
        return failures;
    }

    /// <summary>
    /// Status chance tiers through the hooked status step, and the hit's status roll through
    /// its hook. The step and the roll are the game's (they only call the CRT, the battle RNG,
    /// the percentage roll and a status helper), so they run here. The step itself marks a miss
    /// when it has nothing to inflict, so the tier's misses are counted by the hook. The copy's
    /// RNG state is all zeros (it would always roll 0), so it gets a seed.
    /// </summary>
    public static int StatusTiers(Game game, Log log)
    {
        var b = game.Battle;
        var r = game.Routines;
        var code = game.RuleCode;
        var units = game.TurnCode.Units;
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        *b.Attacker = units;
        *r.Target = units + Battle.UnitSize;
        *b.TargetResult = result;
        const int set = 0x7F;
        var entry = code.StatusSets + set * 6;
        var entryBefore = new byte[6];
        for (var i = 0; i < 6; i++)
            entryBefore[i] = entry[i];
        var rng = (uint*)GameImage.Rip((nint)game.TurnCode.Roll, 3, 7) - 3; // xorshift128 state, 0x8E5FC8
        rng[0] = 0x12345678;
        rng[1] = 0x9ABCDEF0;
        rng[2] = 0x0F1E2D3C;
        rng[3] = 0x4B5A6978;
        var failures = 0;
        void Case(string name, byte flags, int tier, bool simulating, bool sameCopy, int minMisses, int maxMisses)
        {
            entry[0] = (byte)(flags & 0xF0);
            for (var i = 1; i < 6; i++)
                entry[i] = 0;
            Rules.SetTier(set, tier);
            *code.StatusId = set;
            *r.Simulating = simulating ? 1 : 0;
            var before = Rules.TierMisses;
            for (var k = 0; k < 20; k++)
            {
                for (var i = 0; i < 6; i++)
                    code.StatusCopy[i] = entry[i];
                if (!sameCopy)
                    code.StatusCopy[0] |= 0x10; // Cancel: no longer the set, and still nothing to inflict
                result[0] = 1;
                result[2] = 0;
                code.StatusStep();
            }
            var misses = Rules.TierMisses - before;
            var ok = misses >= minMisses && misses <= maxMisses;
            if (!ok) failures++;
            log.Info($"self-test: status tiers, {name}: {misses} misses in 20 (expected {minMisses}-{maxMisses}){(ok ? "" : ", WRONG")}");
        }
        Case("a 10% set", 0x80, 1, false, true, 14, 20);
        Case("a 75% set", 0x80, 8, false, true, 1, 11);
        Case("a 10% set that's Separate", 0xA0, 1, false, true, 0, 0);
        Case("a 10% set while the AI simulates", 0x80, 1, true, true, 0, 0);
        Case("a 10% set the copy no longer matches", 0x80, 1, false, false, 0, 0);
        Case("a set without a tier", 0x80, 0, false, true, 0, 0);

        // A Separate set rolls each status at the step's chance: the tier's, 19 after formula 0x02's roll, else 100.
        // (The game's step can't add a real status here, so an empty Separate set shows the chance the hook passes.)
        int SeparateChanceFor(int tier, int category)
        {
            entry[0] = 0x20;                                 // Separate, nothing to inflict
            for (var i = 1; i < 6; i++)
                entry[i] = 0;
            Rules.SetTier(set, tier);
            Rules.SetCategory(category);
            *code.StatusId = set;
            *r.Simulating = 0;
            for (var i = 0; i < 6; i++)
                code.StatusCopy[i] = entry[i];
            result[0] = 1;
            var before = Rules.TierMisses;
            code.StatusStep();
            Rules.SetCategory(0);
            return Rules.TierMisses == before ? *Rules.SeparateChance : -1;
        }
        var chance75 = SeparateChanceFor(8, 0);
        var chance10 = SeparateChanceFor(1, 0);
        var chance100 = SeparateChanceFor(0, 0);
        var chance19 = SeparateChanceFor(8, 19);
        var ok1 = chance75 == 75 && chance10 == 10 && chance100 == 100 && chance19 == 19;
        if (!ok1) failures++;
        log.Info($"self-test: status tiers, a Separate set's statuses each roll at {chance75}% (75% tier), {chance10}% (10% tier), " +
                 $"{chance100}% (no tier), {chance19}% (75% tier after formula 0x02's roll), with no miss{(ok1 ? "" : ", WRONG")}");
        // The stub through a gadget that returns ecx × 1000 + edx.
        var gadget = GameImage.AllocateCode(16);
        byte[] g = [0x69, 0xC1, 0xE8, 0x03, 0x00, 0x00, 0x01, 0xD0, 0xC3]; // imul eax, ecx, 1000; add eax, edx; ret
        g.CopyTo(new Span<byte>(gadget, g.Length));
        var chance = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
        *chance = 50;
        var stub = GameImage.AllocateCode(32);
        Rules.WriteSeparateStub(stub, chance, (nint)gadget);
        var passed = ((delegate* unmanaged<int>)stub)();
        System.Runtime.InteropServices.NativeMemory.Free(chance);
        var ok3 = passed == 100050 && ((byte*)Rules.SeparateAt)[0] == 0xE9 && GameImage.Rip(Rules.SeparateAt, 1, 5) == Rules.SeparateStub;
        if (!ok3) failures++;
        log.Info($"self-test: status tiers, the per-status roll (0x{Rules.SeparateAt - game.Image.ImageStart:X}) gets (100, the chance) " +
                 $"from its stub: {passed}{(ok3 ? "" : ", WRONG")}");
        Rules.SetTier(set, 0);
        for (var i = 0; i < 6; i++)
            entry[i] = entryBefore[i];
        for (var i = 0; i < 4; i++)
            rng[i] = 0;

        // The hit's 19% roll: skipped for most formulas; kept for 0x02 and 0x05. While the AI
        // simulates the game's roll doesn't roll, and without the result's 0x200 says "skip".
        *r.Simulating = 1;
        var plain = Rules.HitStatusRollIn(0x01, code.HitStatusRoll);
        var spell = Rules.HitStatusRollIn(0x02, code.HitStatusRoll);
        var ok2 = plain == 0 && spell == 1;
        if (!ok2) failures++;
        log.Info($"self-test: the hit's status roll: formula 0x01 {plain} (statuses land), formula 0x02 {spell} (the game's roll)" +
                 $"{(ok2 ? "" : ", WRONG")}");
        *r.Simulating = 0;
        *b.Attacker = null;
        *r.Target = null;
        *b.TargetResult = null;
        System.Runtime.InteropServices.NativeMemory.Free(result);
        System.Runtime.InteropServices.NativeMemory.Clear(units, Battle.UnitCount * Battle.UnitSize);
        return failures;
    }

    /// <summary>
    /// Learning on hit through the secondary skillset: the scan (replaced in C#) runs through the
    /// game's entry and only calls leaves; the learn step needs the battle's unit objects, so its
    /// bit-setting is checked directly. White Magic's first ability is learned with a 100% rate.
    /// </summary>
    public static int LearnSecondary(Game game, Log log)
    {
        var units = game.TurnCode.Units;
        var common = game.Battle.AbilityCommon;
        var scan = (delegate* unmanaged<int, ushort*, int>)Learning.ScanEntry;
        var abilityNow = Learning.Ability;
        const int whiteMagic = 0x0A;
        var ability = (short)(ushort)Learning.AbilityAt(whiteMagic, 0);
        var commonBefore = (common[ability * 8 + 2], common[ability * 8 + 3]);
        common[ability * 8 + 2] = 100;
        common[ability * 8 + 3] |= 0x20;
        *abilityNow = ability;
        for (var i = 0; i < Battle.UnitCount; i++)
            units[i * Battle.UnitSize + 1] = 0xFF;
        var learner = units + 2 * Battle.UnitSize;
        learner[1] = 2;
        learner[3] = 0x4A;      // a Squire
        learner[0x12] = 0x05;   // with Basic Skill
        learner[0x13] = whiteMagic;
        learner[0x1BB] = 2;     // can learn from this action
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: learning, {name}{(ok ? "" : ": WRONG")}");
        }
        ushort learned = 0;
        var found = scan(0, &learned);
        var job = Learning.SecondaryJob(whiteMagic);
        Check($"a Squire with White Magic as secondary learns 0x{ability:X3} from it: unit {found}, job 0x{Learning.SecondaryFound(2).Job:X2}",
              found == 2 && learned == (ushort)ability && Learning.SecondaryFound(2) == (job, whiteMagic) && learner[0x1BB] == 4);
        Learning.MarkLearned(learner, (byte)job, whiteMagic);
        var bits = learner + 0xA2 + 3 * (job - 0x4A);
        Check($"the bit goes to the White Mage's learned abilities (0x{bits[0]:X2}), not the Squire's (0x{learner[0xA2]:X2})",
              (bits[0] & 0x80) != 0 && learner[0xA2] == 0);
        learner[0x1BB] = 2;
        Check("once learned, it isn't learned again", scan(0, &learned) == -1);
        Check("the jobs for other secondaries: Sing the Bard's (0x5B), Darkness the Dark Knight's (0xA0), others Squire's",
              Learning.SecondaryJob(0x16) == 0x5B && Learning.SecondaryJob(0xE0) == 0xA0 && Learning.SecondaryJob(0x4D) == 0x12
              && Learning.SecondaryJob(0x30) == 0x12 && Learning.SecondaryJob(0x05) == 0x4A);
        common[ability * 8 + 2] = commonBefore.Item1;
        common[ability * 8 + 3] = commonBefore.Item2;
        *abilityNow = 0;
        System.Runtime.InteropServices.NativeMemory.Clear(units, Battle.UnitCount * Battle.UnitSize);
        return failures;
    }

    /// <summary>
    /// Two-job stat growth: ReMixed's rule on a unit whose growths came from its job, against
    /// the copy's job table (vanilla values: the mod loader's job changes aren't applied here).
    /// The hooked step itself reads the remaster's settings, so the rule is checked directly.
    /// </summary>
    public static int TwoJobGrowth(Game game, Log log)
    {
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: two-job growth, {name}{(ok ? "" : ": WRONG")}");
        }
        const int dancer = 0x5C, knight = 0x4C;
        var mine = Growth.JobGrowths(dancer);
        var theirs = Growth.JobGrowths(knight);
        void Setup(ushort innate, byte secondary)
        {
            unit[3] = dancer;
            unit[0x13] = secondary;
            *(ushort*)(unit + 0x0A) = innate;
            for (var k = 0; k < 10; k++)
                unit[0x8A + k] = mine[k];
        }
        Setup(0x1E5, 0x07); // Battle Skill, the Knight's
        var applied = Growth.Combine(unit);
        var best = true;
        for (var k = 0; k < 10; k += 2)
            best &= unit[0x8A + k] == Math.Min(mine[k], theirs[k]) && unit[0x8B + k] == Math.Max(mine[k + 1], theirs[k + 1]);
        Check($"a Dancer with Battle Skill grows like the better of Dancer and Knight (HP {mine[0]}/{mine[1]} and " +
              $"{theirs[0]}/{theirs[1]}: {unit[0x8A]}/{unit[0x8B]})", applied && best);
        Setup(0, 0x07);
        Check("without 0x1E5 it doesn't", !Growth.Combine(unit) && unit[0x8A] == mine[0]);
        Setup(0x1E5, 0);
        Check("without a secondary it doesn't", !Growth.Combine(unit));
        var options = Options.Current;
        Options.Current = new Options { JobStats = Options.JobStatRule.JobAndSecondary };
        Setup(0, 0x07);
        Check("with Job Stats on job and secondary, it does without 0x1E5 too",
              Growth.Combine(unit) && unit[0x8A] == Math.Min(mine[0], theirs[0]));
        Options.Current = options;
        Check("Darkness (0xE0) belongs to the Dark Knight (0xA0) and Sing (0x16) to the Bard (0x5B)",
              Growth.SecondJob(Prepared(0xE0)) == 0xA0 && Growth.SecondJob(Prepared(0x16)) == 0x5B);
        byte* Prepared(byte secondary)
        {
            Setup(0x1E5, secondary);
            return unit;
        }
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        return failures;
    }

    /// <summary>
    /// Counter Steal: ReMixed's steal pick; its place in the reactions to HP damage, through the
    /// hooked chain with the AI's no-roll setting (its callees are leaf steps); its type byte; the
    /// after-damage queue that no longer takes it; and the builder leaving one it didn't reach
    /// queued as itself. The builder's cases call range and map code, so the swap itself isn't run here.
    /// </summary>
    public static int CounterSteal(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Counter Steal, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Reactions.CounterQueue == 0)
        {
            Check("isn't installed (0x1B7 isn't in the supports list)", false);
            return failures;
        }
        var thief = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var victim = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        void Dress(bool rightShield, bool leftShield, bool head, bool body, bool accessory)
        {
            static ushort Item(bool has, ushort id) => has ? id : (ushort)0xFF;
            *(ushort*)(victim + 0x20) = 0x01; // a dagger in the right hand
            *(ushort*)(victim + 0x22) = Item(rightShield, 0x80);
            *(ushort*)(victim + 0x24) = 0xFF;
            *(ushort*)(victim + 0x26) = Item(leftShield, 0x80);
            *(ushort*)(victim + 0x1A) = Item(head, 0x90);
            *(ushort*)(victim + 0x1C) = Item(body, 0xA0);
            *(ushort*)(victim + 0x1E) = Item(accessory, 0xD0);
        }
        int[] rolls = [0, 23, 24, 47, 48, 71, 72, 95, 96, 119, 120, 127];
        string Picks() => string.Join(" ", rolls.Select(roll => Reactions.StealPick(thief, victim, roll).ToString("X2")));
        thief[Battle.UnitTeam] = 0x00;
        victim[Battle.UnitTeam] = 0x10;
        victim[6] = 0x80;
        Dress(true, false, true, true, true);
        Check($"against a fully dressed enemy, rolls {string.Join(" ", rolls)} pick {Picks()}",
              Picks() == "71 71 70 70 6E 6E 6F 6F 72 72 6C 6C");
        Dress(false, false, false, false, false);
        Check($"against one with only a weapon: {Picks()} (Steal Weapon below 24 without a look at the hands, as in ReMixed)",
              Picks() == "71 71 6C 6C 6C 6C 6C 6C 6C 6C 6C 6C");
        Dress(false, true, false, false, true);
        Check($"a left-hand shield and an accessory: {Picks()}", Picks() == "71 71 70 70 72 72 72 72 72 72 6C 6C");
        victim[Battle.UnitTeam] = 0x00;
        Check("an ally gets Steal Exp", Reactions.StealPick(thief, victim, 30) == 0x73);
        victim[Battle.UnitTeam] = 0x10;
        victim[6] = 0x20;
        Check("so does a monster", Reactions.StealPick(thief, victim, 30) == 0x73);
        Check("its type byte is Counter's 0x29", Reactions.ReactionTypes[0x1B7] == 0x29 && Reactions.ReactionTypes[0x1BA] == 0x29);

        // The reactions to HP damage, through the hooked chain.
        var b = game.Battle;
        var r = game.Routines;
        var q = (byte*)Reactions.CounterQueue;
        if (!(q[4] == 0x80 && q[5] == 0x3D && q[10] == 0x15 && q[0x11] == 0x84 && q[0x12] == 0x15))
        {
            Check("the counters' queue isn't the code this test expects", false);
            return failures;
        }
        var skillsetCheck = (byte*)GameImage.Rip((nint)q + 4, 2, 7); // 0x15 means no counter
        var abilityFlags = (byte*)GameImage.Rip((nint)q + 0x11, 2, 6);
        var skillsetBefore = *skillsetCheck;
        var flagsBefore = *abilityFlags;
        var simulating = *r.Simulating;
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        *r.Target = target;
        *b.TargetResult = result;
        *b.EffectiveFormula = 1;
        *r.Simulating = 1; // the AI's estimate: no Brave roll
        *skillsetCheck = 0;
        *b.AbilityId = 0x123;
        int React(byte reactions94, byte reactions95, byte reactions96, byte flags)
        {
            target[Battle.UnitReactions] = reactions94;
            target[Battle.UnitReactions + 1] = reactions95;
            target[Battle.UnitReactions + 2] = reactions96;
            *abilityFlags = flags;
            *(ushort*)(result + Battle.ResultReaction) = 0;
            *(ushort*)(result + Battle.ResultReactionAmount) = 0;
            game.TurnCode.HpReactions();
            return *(ushort*)(result + Battle.ResultReaction);
        }
        Check("it reacts to an ability that can be countered (flag 0x10), keeping the ability",
              React(0, 0, 0x40, 0x10) == 0x1B7 && *(ushort*)(result + Battle.ResultReactionAmount) == 0x123);
        Check("but not to one that can't", React(0, 0, 0x40, 0x40) == 0);
        Check("it comes before Counter", React(0, 0, 0x48, 0x10) == 0x1B7);
        Check("Counter alone is still Counter", React(0, 0, 0x08, 0x10) == 0x1BA);
        Check("0x1B4 still comes before it", React(0, 0x02, 0x40, 0x10) == 0x1B4);

        // After the damage: Auto-Potion is still queued there, Counter Steal isn't.
        var afterDamage = (delegate* unmanaged<int, void>)Reactions.AfterDamageQueueAt;
        result[Battle.ResultFlags] = 0x80;
        *(ushort*)(result + Battle.HpDamage) = 50;
        *(ushort*)(result + Battle.ResultReaction) = 0;
        afterDamage(0x1B9);
        var autoPotion = *(ushort*)(result + Battle.ResultReaction);
        *(ushort*)(result + Battle.ResultReaction) = 0;
        afterDamage(0x1B7);
        Check($"after the damage, Auto-Potion still queues (0x{autoPotion:X3}) and Counter Steal doesn't",
              autoPotion == 0x1B9 && *(ushort*)(result + Battle.ResultReaction) == 0);

        // The builder with nobody to react to returns at once; a queued Counter Steal stays one.
        var reactor = game.TurnCode.Units + 5 * Battle.UnitSize;
        *(ushort*)(reactor + Battle.UnitResult + Battle.ResultReaction) = 0x1B7;
        var reactingTo = *Reactions.ReactingTo;
        *Reactions.ReactingTo = -1;
        ushort built = 0;
        var who = ((delegate* unmanaged<nint, int>)Reactions.BuildReactionAt)((nint)(&built));
        Check("the builder leaves a Counter Steal it didn't take queued as itself",
              who == -1 && *(ushort*)(reactor + Battle.UnitResult + Battle.ResultReaction) == 0x1B7);
        *(ushort*)(reactor + Battle.UnitResult + Battle.ResultReaction) = 0;
        *Reactions.ReactingTo = reactingTo;

        *skillsetCheck = skillsetBefore;
        *abilityFlags = flagsBefore;
        *r.Simulating = simulating;
        *r.Target = null;
        *b.TargetResult = null;
        foreach (var p in new[] { thief, victim, target, result })
            System.Runtime.InteropServices.NativeMemory.Free(p);
        return failures;
    }

    /// <summary>
    /// Weapon requirements per ability: ReMixed's masks against a unit holding weapons of each
    /// type (found in the copy's item table); each check's stub, run through a small harness with
    /// return thunks standing in for the game's pass and fail branches; and the three patched
    /// checks' jumps. The menu and AI code around the checks can't run here.
    /// </summary>
    public static int WeaponTypes(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: weapon requirements, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Weapons.Sites.Count != 3)
        {
            Check($"{Weapons.Sites.Count} checks patched, not 3", false);
            return failures;
        }
        var byType = new Dictionary<int, ushort>();
        for (ushort item = 1; item < 0xFE; item++)
            byType.TryAdd(Weapons.ItemType(item), item);
        const ushort none = 0xFF;
        ushort Of(int type) => byType.TryGetValue(type, out var item) ? item : none;
        Check($"an empty hand (item 0xFF) is type {Weapons.ItemType(none)}, a sword is item 0x{Of(3):X2}, a spear 0x{Of(15):X2}",
              Weapons.ItemType(none) == 0 && Of(3) != none && Of(4) != none && Of(1) != none && Of(15) != none);
        var unit = game.TurnCode.Units + 3 * Battle.UnitSize;
        var rightBefore = *(ushort*)(unit + 0x20);
        var leftBefore = *(ushort*)(unit + 0x24);
        void Hold(ushort right, ushort left)
        {
            *(ushort*)(unit + 0x20) = right;
            *(ushort*)(unit + 0x24) = left;
        }
        bool With(ushort right, ushort left, int ability)
        {
            Hold(right, left);
            return Weapons.Allows(ability, unit);
        }
        Check("Holy Sword skills (0x09B) take a sword or a knight's sword in either hand, not a knife or nothing",
              With(Of(3), none, 0x9B) && With(none, Of(4), 0x9B) && !With(Of(1), none, 0x9B) && !With(none, none, 0x9B));
        Check("Braver (0x101) takes a knife, not a spear", With(Of(1), none, 0x101) && !With(Of(15), none, 0x101));
        Check("0x144 takes only a spear", With(Of(15), none, 0x144) && !With(Of(3), none, 0x144));
        Check("an ability without an entry (Fire, 0x010) needs nothing", With(none, none, 0x10));

        // Each stub, entered from a 16-aligned frame like the game's (the harness), with its
        // branches pointed at thunks that return 1 (pass) or 0 (fail).
        var harness = GameImage.AllocateCode(128);
        byte[] code =
        [
            0x55, 0x41, 0x55, 0x56, 0x57, 0x41, 0x57, // push rbp, r13, rsi, rdi, r15
            0x48, 0x83, 0xEC, 0x28,             // sub rsp, 0x28: the stub then starts 16-aligned
            0x8B, 0xE9, 0x8B, 0xF1, 0x8B, 0xF9, // mov ebp, ecx; mov esi, ecx; mov edi, ecx (the ability)
            0x4C, 0x8B, 0xEA,                   // mov r13, rdx (the AI's unit offset)
            0x45, 0x31, 0xFF,                   // xor r15d, r15d (the menu's skillset: none)
            0x89, 0x54, 0x24, 0x20,             // mov [rsp+0x20], edx: the stub's [rsp+0x28] (the lists' battle index)
            0x41, 0xFF, 0xD0,                   // call r8 (the stub)
            0x48, 0x83, 0xC4, 0x28,             // add rsp, 0x28
            0x41, 0x5F, 0x5F, 0x5E, 0x41, 0x5D, 0x5D, 0xC3, // pop r15, rdi, rsi, r13, rbp; ret
        ];
        code.CopyTo(new Span<byte>(harness, code.Length));
        var pass = harness + 64;
        byte[] passCode = [0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3]; // mov eax, 1; ret
        passCode.CopyTo(new Span<byte>(pass, passCode.Length));
        var fail = harness + 80;
        byte[] failCode = [0x31, 0xC0, 0xC3];                    // xor eax, eax; ret
        failCode.CopyTo(new Span<byte>(fail, failCode.Length));
        var run = (delegate* unmanaged<int, long, byte*, int>)harness;
        var stub = GameImage.AllocateCode(256);
        (ushort Right, int Ability, int Expected)[] cases = [(Of(3), 0x9B, 1), (Of(1), 0x9B, 0), (Of(15), 0x144, 1), (none, 0x10, 1)];
        foreach (var site in Weapons.Sites)
        {
            Weapons.WriteStub(stub, site.Kind, (nint)pass, (nint)fail);
            var arg = site.Kind == Weapons.Kind.AiCandidates ? 3L * Battle.UnitSize : 3L;
            var got = "";
            var right = true;
            foreach (var c in cases)
            {
                Hold(c.Right, none);
                var r = run(c.Ability, arg, stub);
                got += r;
                right &= r == c.Expected;
            }
            Check($"{site.Name}'s stub gives {got} (expected 1011)", right);
            // The patched check jumps to its stub, which goes on to the game's branches.
            var at = (byte*)site.At;
            var target = (byte*)*(nint*)(at + 6);
            var jumps = -1;
            for (var k = 0; k < 240 && jumps < 0; k++)
                if (target[k] == 0xFF && target[k + 1] == 0x25 && *(int*)(target + k + 2) == 0)
                    jumps = k;
            Check($"{site.Name} (0x{site.At - game.Image.ImageStart:X}) jumps to its stub, which goes on to 0x{site.Pass - game.Image.ImageStart:X} or 0x{site.Fail - game.Image.ImageStart:X}",
                  at[0] == 0xFF && at[1] == 0x25 && *(int*)(at + 2) == 0 && jumps > 0
                  && *(nint*)(target + jumps + 6) == site.Pass && *(nint*)(target + jumps + 20) == site.Fail);
        }
        Hold(rightBefore, leftBefore);
        return failures;
    }

    /// <summary>
    /// EXP and JP rates: ReMixed's defaults from the build, the replaced EXP step (through its hooked
    /// entry), the JP stub (through a harness, as for the weapon checks), and the post-move rewrite,
    /// whose game step (setup, result clear, status check) runs here with the abilities hidden.
    /// </summary>
    public static int GainRates(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: gains, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Gains.ActionExpAt == 0 || Movement.MoveAmountAt == 0)
        {
            Check("the rates or the post-move rewrite aren't installed", false);
            return failures;
        }
        Check($"ReMixed's defaults: JP {Gains.JpRate}%, EXP {Gains.ExpRate}%, spillover 1/{Gains.Spillover}",
              Gains.JpRate == 125 && Gains.ExpRate == 100 && Gains.Spillover == 4);
        Check("the rate keeps at least 1 and gives nothing for nothing",
              Gains.Scale(8, 125) == 10 && Gains.Scale(1, 50) == 1 && Gains.Scale(0, 125) == 0 && Gains.Scale(9, 0) == 0);

        // The action EXP step.
        var b = game.Battle;
        var r = game.Routines;
        var attacker = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        *b.Attacker = attacker;
        *r.Target = target;
        var exp = (delegate* unmanaged<int, void>)Gains.ActionExpAt;
        int Exp(int mode, byte attackerLv, byte targetLv, byte split, bool expUp)
        {
            attacker[Battle.UnitLv] = attackerLv;
            target[Battle.UnitLv] = targetLv;
            target[0x18C] = split;
            attacker[Battle.UnitSupports + 1] = (byte)(expUp ? 0x20 : 0);
            *Gains.ActionExpGlobal = 0;
            exp(mode);
            return *Gains.ActionExpGlobal;
        }
        var reacting = *Gains.Reacting;
        *Gains.Reacting = 0;
        Check($"EXP for a hit: 10 + 10 − 12 = {Exp(1, 12, 10, 0, false)}, 20/2 + 30 − 10 = {Exp(2, 10, 30, 2, false)}",
              Exp(1, 12, 10, 0, false) == 8 && Exp(2, 10, 30, 2, false) == 30);
        Check("Gained EXP Up doesn't double it any more", Exp(1, 12, 10, 0, true) == 8);
        Check("a much lower level gives 1, a much higher one 99", Exp(1, 99, 1, 0, false) == 1 && Exp(2, 1, 99, 0, false) == 99);
        var rate = Gains.ExpRate;
        Gains.ExpRate = 50;
        Check($"at 50%, 30 EXP is {Exp(2, 10, 30, 2, false)}", Exp(2, 10, 30, 2, false) == 15);
        Gains.ExpRate = 0;
        Check("at 0%, nothing", Exp(2, 10, 30, 2, false) == 0);
        Gains.ExpRate = rate;
        *Gains.ActionExpGlobal = 40;
        exp(1); // 30 for this target
        Check("the action keeps its best target's EXP", *Gains.ActionExpGlobal == 40);
        *Gains.Reacting = 1;
        *Gains.ActionExpGlobal = 0;
        exp(1);
        Check("no EXP while a reaction is applied", *Gains.ActionExpGlobal == 0);
        *Gains.Reacting = reacting;
        *Gains.ActionExpGlobal = 0;

        // The JP stub, with its jump back pointed at a thunk that returns ebp.
        var harness = GameImage.AllocateCode(128);
        var stub = GameImage.AllocateCode(256);
        byte[] code =
        [
            0x55, 0x41, 0x55, 0x56, 0x48, 0x83, 0xEC, 0x08, // push rbp, r13, rsi; sub rsp, 8: the stub then starts 16-aligned
            0x8B, 0xE9, 0x8B, 0xC2, 0x45, 0x8B, 0xE8, 0x41, 0x8B, 0xF1, // mov ebp, ecx (level); eax, edx (the EXP); r13d, r8d (job level); esi, r9d
            0x49, 0xBA, 0, 0, 0, 0, 0, 0, 0, 0,         // mov r10, stub
            0x41, 0xFF, 0xD2,                           // call r10
            0x48, 0x83, 0xC4, 0x08, 0x5E, 0x41, 0x5D, 0x5D, 0xC3, // add rsp, 8; pop rsi, r13, rbp; ret
        ];
        BitConverter.TryWriteBytes(code.AsSpan(20), (ulong)stub);
        code.CopyTo(new Span<byte>(harness, code.Length));
        var thunk = harness + 96;
        thunk[0] = 0x8B; thunk[1] = 0xC5; thunk[2] = 0xC3; // mov eax, ebp; ret
        var site = (byte*)Gains.JpBaseAt;
        var real = (byte*)*(nint*)(site + 6); // the installed stub, which starts with the 20 bytes it replaced
        Gains.BuildJpStub(stub, (nint)real, (nint)thunk);
        var run = (delegate* unmanaged<int, int, int, int, int>)harness;
        var jp = run(20, 5, 3, 0x12);
        Check($"the JP stub: level 20, job level 3 gives a base of 8 + 5 + 6 = 19, at 125% {jp}; no EXP, no JP ({run(20, 0, 3, 0x12)})",
              jp == Gains.Scale(19, 125) && run(20, 0, 3, 0x12) == 0);
        var back = -1;
        for (var k = 0; k < 180 && back < 0; k++)
            if (real[k] == 0xFF && real[k + 1] == 0x25 && *(int*)(real + k + 2) == 0)
                back = k;
        Check($"the JP award (0x{(nint)site - game.Image.ImageStart:X}) jumps to its stub, which comes back 20 bytes on",
              site[0] == 0xFF && site[1] == 0x25 && back > 0 && *(nint*)(real + back + 6) == (nint)site + 20);

        // The post-move rewrite, through the hooked step.
        var unit = game.TurnCode.Units + 5 * Battle.UnitSize;
        var saved = new byte[Battle.UnitSize];
        new Span<byte>(unit, Battle.UnitSize).CopyTo(saved);
        new Span<byte>(unit, Battle.UnitSize).Clear();
        unit[Battle.UnitIndex] = 5;
        *(ushort*)(unit + Battle.UnitMaxHp) = 95;
        *(ushort*)(unit + Battle.UnitMaxMp) = 41;
        var amountOverride = (byte*)GameImage.Rip(Movement.MoveAmountAt, 2, 6); // 0xFE in play: 1 to Move at random
        var amountBefore = *amountOverride;
        *amountOverride = 8;
        var groups = (byte*)GameImage.Rip((nint)game.TurnCode.HasStatusIn + 21, 3, 7) + 4 * 5; // status group 4
        var groupBefore = groups[0];
        var result = unit + Battle.UnitResult;
        string After(byte hpUp, byte others, byte gainedUps, out int returned)
        {
            unit[Battle.UnitMovement] = hpUp;
            unit[Battle.UnitMovement + 1] = others;
            unit[Battle.UnitSupports + 1] = gainedUps;
            returned = game.TurnCode.PostMove(unit);
            return $"HP+{*(ushort*)(result + Battle.HpHeal)} MP+{*(ushort*)(result + Battle.MpHeal)} " +
                   $"EXP+{result[Battle.ResultExp]} JP+{result[Battle.ResultJp]} flags {result[Battle.ResultFlags]:X2}";
        }
        var all = After(0x01, 0xE0, 0, out var r1);
        Check($"all four post-move abilities apply: {all}", r1 == 0 && all == "HP+10 MP+5 EXP+8 JP+10 flags 51");
        Check("Gained EXP Up and JP Up don't double Move-Get", After(0x01, 0xE0, 0x60, out _) == all);
        groups[0] = 0x20;
        unit[Battle.UnitStatuses] = 0x20;
        var blocked = After(0x01, 0xE0, 0, out var r2);
        Check($"a status from group 4 blocks them ({blocked}, {r2})", r2 == -1 && blocked == "HP+0 MP+0 EXP+0 JP+0 flags 00");
        var cleansed = After(0x01, 0xE1, 0, out var r3);
        Check($"but not for a Move-Cleanse unit: {cleansed}", r3 == 0 && cleansed == "HP+10 MP+5 EXP+8 JP+10 flags 51");
        groups[0] = groupBefore;
        *amountOverride = amountBefore;
        saved.CopyTo(new Span<byte>(unit, Battle.UnitSize));
        *r.Target = null;
        *b.TargetResult = null;
        *b.Attacker = null;
        System.Runtime.InteropServices.NativeMemory.Free(attacker);
        System.Runtime.InteropServices.NativeMemory.Free(target);
        return failures;
    }

    /// <summary>
    /// Banditry: the back-attack bonus through the hooked critical step (with the AI's simulating
    /// flag on, the game's random number is a fixed 0x4000, so its own 4% never comes up); the AI's
    /// Mug for Gil Taking, through the stub, from a harness that keeps a battle index at its
    /// [rsp+0x28] as the AI's lists do; and the two patched calls.
    /// </summary>
    private static int _knockbacks;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void CountKnockback() => _knockbacks++;

    public static int BackAttacks(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Banditry, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Banditry.CriticalAt == 0)
        {
            Check("isn't installed (0x1CF isn't in the supports list)", false);
            return failures;
        }
        var b = game.Battle;
        var r = game.Routines;
        var attacker = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        *b.Attacker = attacker;
        *b.TargetResult = result;
        var simulating = *r.Simulating;
        *r.Simulating = 1;
        var facing = Banditry.Facing;
        var facingBefore = *facing;
        var weaponBefore = *r.WeaponItemId;
        var formulaBefore = *b.EffectiveFormula;
        ushort ItemOf(int type)
        {
            for (ushort item = 1; item < 0xFE; item++)
                if (r.GetItem(item)[5] == type)
                    return item;
            return 0xFF;
        }
        var knife = ItemOf(1);
        var sword = ItemOf(3);
        var critical = (delegate* unmanaged<void>)Banditry.CriticalAt;
        (int, int) Hit(bool banditry, byte angle, byte formula, ushort weapon)
        {
            attacker[Battle.UnitSupports + 1] = (byte)(banditry ? 0x40 : 0);
            *facing = angle;
            *b.EffectiveFormula = formula;
            *r.WeaponItemId = weapon;
            *b.XA = 40;
            result[1] = 0;
            critical();
            return (*b.XA, result[1]);
        }
        Check("a Banditry back attack with a knife: XA 40 becomes 60, a critical hit", Hit(true, 2, 1, knife) == (60, 1));
        Check("with a sword, 50", Hit(true, 2, 1, sword) == (50, 1));
        Check("with nothing in hand, 60", Hit(true, 2, 1, 0xFF) == (60, 1));
        Check("from the side, nothing", Hit(true, 1, 1, knife) == (40, 0));
        Check("without Banditry, nothing", Hit(false, 2, 1, knife) == (40, 0));
        Check("Mug's formula (0x27) from behind, Banditry or not", Hit(false, 2, 0x27, sword) == (50, 1));
        Check("other formulas, nothing", Hit(true, 2, 0x0A, knife) == (40, 0));
        var options = Options.Current;
        Options.Current = new Options { BackAttacks = Options.BackAttackRule.Vanilla };
        Check("Back Attacks on vanilla: no Banditry or Mug critical", Hit(true, 2, 1, knife) == (40, 0) && Hit(false, 2, 0x27, sword) == (40, 0));
        Options.Current = new Options { BackAttacks = Options.BackAttackRule.Everyone };
        Check("on everyone: any unit's basic attack from behind, 60 with a knife", Hit(false, 2, 1, knife) == (60, 1));
        Options.Current = options;
        // Momentum's knockback after moving (with the game's knockback swapped for a count).
        if (Supports.Momentum)
        {
            var bow = ItemOf(12);
            var realKnockback = Banditry.GameKnockback;
            Banditry.GameKnockback = &CountKnockback;
            (int, int, int) Rush(bool turn, bool moved, byte angle, ushort weapon, bool banditry = false)
            {
                attacker[Battle.UnitMovement + 2] = 0x20;
                attacker[Battle.UnitTurn] = (byte)(turn ? 1 : 0);
                attacker[Battle.UnitMoved] = (byte)(moved ? 1 : 0);
                _knockbacks = 0;
                var (xa, crit) = Hit(banditry, angle, 1, weapon);
                return (xa, crit, _knockbacks);
            }
            Check("Momentum: a sword attack after moving, from the front: XA stays 40, a critical hit with a knockback",
                  Rush(true, true, 0, sword) == (40, 1, 1));
            Check("not after moving, nothing; nor a unit whose turn it isn't (a counter)",
                  Rush(true, false, 0, sword) == (40, 0, 0) && Rush(false, true, 0, sword) == (40, 0, 0));
            Check("with a bow, nothing", Rush(true, true, 0, bow) == (40, 0, 0));
            Check("a Banditry back attack after moving: XA 60 with a knife, the critical and the knockback",
                  Rush(true, true, 2, knife, true) == (60, 1, 1));
            attacker[Battle.UnitMovement + 2] = 0;
            attacker[Battle.UnitTurn] = 0;
            attacker[Battle.UnitMoved] = 0;
            Banditry.GameKnockback = realKnockback;
        }
        else
            Check("Momentum isn't listed, so its knockback isn't checked", false);
        *facing = facingBefore;
        *r.WeaponItemId = weaponBefore;
        *b.EffectiveFormula = formulaBefore;
        *r.Simulating = simulating;
        *b.Attacker = null;
        *b.TargetResult = null;
        System.Runtime.InteropServices.NativeMemory.Free(attacker);
        System.Runtime.InteropServices.NativeMemory.Free(result);

        // The AI's Mug, through the stub.
        var harness = GameImage.AllocateCode(64);
        byte[] code =
        [
            0x48, 0x83, 0xEC, 0x48,             // sub rsp, 0x48: 16-aligned, like the AI's lists
            0x44, 0x89, 0x44, 0x24, 0x28,       // mov [rsp+0x28], r8d (the battle index)
            0x48, 0xB8, 0, 0, 0, 0, 0, 0, 0, 0, // mov rax, the stub
            0xFF, 0xD0,                         // call rax (skillset in ecx, slot in edx)
            0x48, 0x83, 0xC4, 0x48, 0xC3,       // add rsp, 0x48; ret
        ];
        BitConverter.TryWriteBytes(code.AsSpan(11), (ulong)Banditry.AiStub);
        code.CopyTo(new Span<byte>(harness, code.Length));
        var run = (delegate* unmanaged<int, int, int, int>)harness;
        var unit = game.TurnCode.Units + 3 * Battle.UnitSize;
        var supportsBefore = unit[Battle.UnitSupports + 1];
        unit[Battle.UnitSupports + 1] = 0;
        var slot = -1;
        for (var k = 0; k < 16 && slot < 0; k++)
            if ((short)run(14, k, 3) == 0x6C)
                slot = k;
        unit[Battle.UnitSupports + 1] = 0x40;
        var withBanditry = (short)run(14, Math.Max(slot, 0), 3);
        unit[Battle.UnitSupports + 1] = supportsBefore;
        Check($"an AI Thief's list has Gil Taking (skillset 14, slot {slot}), Mug (0x{withBanditry:X3}) with Banditry",
              slot >= 0 && withBanditry == 0x133);
        var patched = true;
        foreach (var call in Banditry.AiCalls)
            patched &= *(byte*)call == 0xE8 && call + 5 + *(int*)(call + 1) == Banditry.AiStub;
        Check("both AI lists call the stub", patched && Banditry.AiCalls.Length == 2);
        return failures;
    }

    /// <summary>Formula 0x11's stat change (ReMixed's 0x80186CA4): Slap's mask and Y, and a mask over several stats.</summary>
    public static int StatStrike(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: stat strike, {name}{(ok ? "" : ": WRONG")}");
        }
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        result[Battle.ResultFlags] = 0x80;
        Formulas.StatChange(result, 0x02, 40);
        Check("Slap (mask 0x02, Y 40) lowers CT by 40 and marks a stat change",
              result[0x15] == 40 && result[0x14] == 0 && result[0x16] == 0 && result[Battle.ResultFlags] == 0x01);
        new Span<byte>(result, 0x40).Clear();
        Formulas.StatChange(result, 0x11, 5);
        Check("mask 0x11 is Speed and Brave", result[0x14] == 5 && result[0x18] == 5 && result[0x15] == 0 && result[0x19] == 0);
        System.Runtime.InteropServices.NativeMemory.Free(result);
        return failures;
    }

    /// <summary>
    /// ReMixed's effect for formulas 0x2A and 0x2B on made-up data: Power Break, Praise (raised), Rally (CT up), and
    /// a lowering Maintenance blocks; formula 0x2B in the table and 0x2A's effect switch through its stub.
    /// </summary>
    public static int StatOrStatusEffects(Game game, Log log)
    {
        _game = game;
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: stats or status, {name}{(ok ? "" : ": WRONG")}");
        }
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var ability = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x20);
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var target = game.Routines.Target;
        var saved = *target;
        *target = unit;
        string Run(int y, int kind, bool maintenance)
        {
            new Span<byte>(result, 0x40).Clear();
            result[0] = 1;
            ability[10] = (byte)y;
            ability[11] = (byte)kind;
            unit[Battle.UnitSupports + 2] = (byte)(maintenance ? 0x04 : 0);
            return Formulas.StatOrStatus(result, ability);
        }
        Run(3, 0x04, false);
        Check("Power Break (Y 3, PA): PA down by 3", result[0x16] == 3 && result[Battle.ResultFlags] == 0x01 && result[0] == 1);
        Run(4, 0x90, false);
        Check("Praise (Y 4, raise Brave): Brave up by 4", result[0x18] == 0x84);
        Run(30, 0x82, true);
        Check("Rally (Y 30, raise CT): CT up by 30, Maintenance or not", result[0x15] == 0x9E && result[0] == 1);
        Run(20, 0x10, true);
        Check("Threaten (Y 20, Brave) on a target with Maintenance: a miss",
              result[0] == 0 && result[2] == 7 && result[0x18] == 0 && result[Battle.ResultFlags] == 0);
        // Formula 0x3B's effect: Speed, PA and MA by Y, CT, Brave and Faith by X.
        string Masked(int x, int y, int kind, bool maintenance)
        {
            new Span<byte>(result, 0x40).Clear();
            ability[9] = (byte)x;
            ability[10] = (byte)y;
            ability[11] = (byte)kind;
            unit[Battle.UnitSupports + 2] = (byte)(maintenance ? 0x04 : 0);
            return Formulas.MaskedStats(result, ability);
        }
        Masked(0, 1, 0x81, false);
        Check("Tailwind (formula 0x3B, X 0, Y 1, raise Speed): Speed up by 1 and nothing else",
              result[0x14] == 0x81 && result[0x16] == 0 && result[0x17] == 0 && result[0x18] == 0 && result[0] == 1
              && result[Battle.ResultFlags] == 0x01);
        Masked(5, 0, 0x90, false);
        Check("Steel (formula 0x3B, X 5, raise Brave): Brave up by 5", result[0x18] == 0x85 && result[0x14] == 0);
        Masked(10, 1, 0x9D, false);
        Check("Shout (formula 0x3B, X 10, Y 1, 0x9D): Speed, PA and MA up by 1, Brave by 10",
              result[0x14] == 0x81 && result[0x16] == 0x81 && result[0x17] == 0x81 && result[0x18] == 0x8A && result[0x15] == 0);
        Masked(0, 2, 0x04, true);
        Check("a lowering formula 0x3B on a target with Maintenance: a miss", result[0] == 0 && result[0x16] == 0);
        ability[9] = 0;
        // The songs', dances' and Alters' effect: raise or lower by the ability's flags, or by team.
        var attackerSlot = game.Battle.Attacker;
        var savedAttacker = *attackerSlot;
        var caster = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        *attackerSlot = caster;
        string Perform(int y, int kind, int direction, bool sameTeam, bool maintenance)
        {
            new Span<byte>(result, 0x40).Clear();
            result[0] = 1;
            ability[4] = (byte)direction;
            ability[10] = (byte)y;
            ability[11] = (byte)kind;
            caster[Battle.UnitTeam] = 0x00;
            unit[Battle.UnitTeam] = (byte)(sameTeam ? 0x00 : 0x10);
            unit[Battle.UnitSupports + 2] = (byte)(maintenance ? 0x04 : 0);
            return Formulas.StatsByTeam(result, ability);
        }
        Perform(1, 0x01, 0x80, false, true);
        Check("Cheer Song (flags 0x80, Speed, Y 1): Speed up by 1 even for an enemy with Maintenance", result[0x14] == 0x81);
        Perform(1, 0x04, 0x40, true, false);
        Check("Polka Polka (flags 0x40, PA, Y 1): PA down by 1 even for an ally", result[0x16] == 1 && result[0] == 1);
        Perform(1, 0x08, 0x40, false, true);
        Check("Disillusion on a target with Maintenance: a miss", result[0] == 0 && result[0x17] == 0);
        Perform(1, 0x01, 0x00, true, false);
        var allyUp = result[0x14];
        Perform(1, 0x01, 0x00, false, false);
        Check("Alter Speed (no direction): up for the caster's team, down for others", allyUp == 0x81 && result[0x14] == 1);
        var resultSlot = game.Battle.TargetResult;
        var savedResult = *resultSlot;
        *resultSlot = result;
        var realHp = game.Routines.HpDamage;
        game.Routines.HpDamage = &FakeHpDamage;
        *game.Battle.XA = 20;
        *game.Battle.YA = 20;
        Perform(20, 0x40, 0x80, true, false);
        var mpHeal = *(ushort*)(result + Battle.MpHeal);
        var flagsHeal = result[Battle.ResultFlags];
        Perform(10, 0x80, 0x40, false, false);
        game.Routines.HpDamage = realHp;
        *resultSlot = savedResult;
        Check("Angel Song (MP, Y 20, MA 20): MP +50; Wiznaibus (HP, YA 10 here 20): HP -50",
              mpHeal == 50 && flagsHeal == 0x10 && *(ushort*)(result + Battle.HpDamage) == 50 && result[Battle.ResultFlags] == 0x80);
        *attackerSlot = savedAttacker;
        System.Runtime.InteropServices.NativeMemory.Free(caster);
        *target = saved;
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        System.Runtime.InteropServices.NativeMemory.Free(ability);
        System.Runtime.InteropServices.NativeMemory.Free(result);
        var table = game.Battle.FormulaTable;
        Check("formula 0x2B is ReMixed's", Formulas.Replaced(0x2B) && game.Image.Pointer(table[0x2B - 1]) != 0);
        Check("formula 0x3B is ReMixed's", Formulas.Replaced(0x3B) && game.Image.Pointer(table[0x3B - 1]) != 0);
        var at = (byte*)Formulas.TalkEffectAt;
        Check($"formula 0x2A's effect switch (0x{Formulas.TalkEffectAt - game.Image.ImageStart:X}) jumps to its stub",
              at != null && at[0] == 0xE9 && GameImage.Rip(Formulas.TalkEffectAt, 1, 5) == Formulas.TalkEffectStub);
        foreach (var (name, site) in new[] { ("Sing", Formulas.SingAt), ("Dance", Formulas.DanceAt) })
            Check($"{name}'s effect (0x{site.At - game.Image.ImageStart:X}) jumps to its stub",
                  site.At != 0 && ((byte*)site.At)[0] == 0xE9 && GameImage.Rip(site.At, 1, 5) == site.Stub);
        return failures;
    }

    /// <summary>
    /// The reaction rule through the hooked bit setter, and the learned passives through the hooked
    /// builder, on a made-up unit (both are leaves apart from each other and the job index).
    /// </summary>
    public static int LearnedPassives(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: passives, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Passives.BuildAt == 0 || Passives.SetBitAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var setBit = (delegate* unmanaged<byte*, int, void>)Passives.SetBitAt;
        bool Has(int id) => (unit[0x94 + ((id - 0x1A6) >> 3)] & (0x80 >> ((id - 0x1A6) & 7))) != 0;
        *(ushort*)(unit + 0x14) = 0x1BA; // Counter equipped
        setBit(unit, 0x1B3);              // an innate Magick Counter
        Check("an innate reaction stays out when another is equipped", !Has(0x1B3) && *(ushort*)(unit + 0x14) == 0x1BA);
        setBit(unit, 0x1BA);
        setBit(unit, 0x1D0);
        Check("the equipped one and supports still set", Has(0x1BA) && Has(0x1D0));
        new Span<byte>(unit, Battle.UnitSize).Clear();
        setBit(unit, 0x1B3);
        Check("with no reaction equipped, the innate one goes in", Has(0x1B3) && *(ushort*)(unit + 0x14) == 0x1B3);

        // The builder: innate Magick Counter, Counter equipped, movement 0x1FD, and learned Geomancer 3rd,
        // Time Mage 5th, Thief 6th and Archer 5th RSM.
        new Span<byte>(unit, Battle.UnitSize).Clear();
        *(ushort*)(unit + 0x0A) = 0x1B3;
        *(ushort*)(unit + 0x14) = 0x1BA;
        *(ushort*)(unit + 0x18) = 0x1FD;
        unit[6] = 0x80; // a man
        *Passives.RsmLearned(unit, 0x56) |= 0x20;
        *Passives.RsmLearned(unit, 0x51) |= 0x08;
        *Passives.RsmLearned(unit, 0x53) |= 0x04;
        *Passives.RsmLearned(unit, 0x4D) |= 0x08;
        ((delegate* unmanaged<byte*, void>)Passives.BuildAt)(unit);
        Check("after the build: Counter, not the innate Magick Counter", Has(0x1BA) && !Has(0x1B3));
        Check("0x1FD turns on learned Move in Water and Float, and the best Jump (+2, not +1)",
              Has(0x1FD) && Has(0x1F6) && Has(0x1FA) && Has(0x1EA) && !Has(0x1E9) && !Has(0x1FB) && !Has(0x1EB));
        *Passives.RsmLearned(unit, 0x5B) |= 0x20;
        ((delegate* unmanaged<byte*, void>)Passives.BuildAt)(unit);
        Check("a man with the Bard's 3rd (Move+3) doesn't get Jump+3", !Has(0x1EB) && Has(0x1EA));
        *Passives.RsmLearned(unit, 0xA0) |= 0x20;
        *Passives.RsmLearned(unit, 0x5B) |= 0x10;
        ((delegate* unmanaged<byte*, void>)Passives.BuildAt)(unit);
        Check("with the Dark Knight's 3rd he gets Jump+3 instead, and Fly from the Bard's 4th", Has(0x1EB) && !Has(0x1EA) && Has(0x1FB));
        *(ushort*)(unit + 0x18) = 0;
        ((delegate* unmanaged<byte*, void>)Passives.BuildAt)(unit);
        Check("without 0x1FD, none of them", !Has(0x1F6) && !Has(0x1FA) && !Has(0x1EB));
        Check("no Non-charge (0x1E3) by default", !Has(0x1E3));
        var options = Options.Current;
        Options.Current = new Options { EveryoneNonCharge = true };
        ((delegate* unmanaged<byte*, void>)Passives.BuildAt)(unit);
        Check("with Cast instantly (everyone has Non-charge), the builder gives it to anyone", Has(0x1E3));
        Options.Current = options;
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        return failures;
    }

    /// <summary>
    /// The hooks whose callers keep scratch registers across the call (found with ExeTool's
    /// liveregs): called from a harness that fills r10, r11 and xmm0–5 with marks and checks them
    /// after. A C# detour without its stub fails this.
    /// </summary>
    public static int KeptRegisters(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: kept registers, {name}{(ok ? "" : ": WRONG")}");
        }
        const ulong r11Mark = 0x1111_2222_3333_4444, r10Mark = 0x5555_6666_7777_0888;
        var code = new List<byte>
        {
            0x53, 0x56, 0x48, 0x83, 0xEC, 0x38,       // push rbx, rsi; sub rsp, 0x38 (16-aligned at the call)
            0xC7, 0x44, 0x24, 0x28, 0x03, 0, 0, 0,    // mov dword [rsp+0x28], 3: a battle index, where the AI's lists keep it
            0x49, 0xBB,                               // mov r11, mark
        };
        code.AddRange(BitConverter.GetBytes(r11Mark));
        code.AddRange([0x49, 0xBA]);                  // mov r10, mark
        code.AddRange(BitConverter.GetBytes(r10Mark));
        for (var x = 0; x < 6; x++)
            code.AddRange([0x66, 0x49, 0x0F, 0x6E, (byte)(0xC3 + 8 * x)]); // movq xmmX, r11
        code.AddRange([0x49, 0x8B, 0xC0, 0xFF, 0xD0]); // mov rax, r8 (the hook); call rax (rcx, rdx: its arguments)
        code.AddRange([0x48, 0xBE]);                  // mov rsi, mark
        code.AddRange(BitConverter.GetBytes(r11Mark));
        var fails = new List<int>();                  // jne offsets to point at the failure
        code.AddRange([0x4C, 0x3B, 0xDE, 0x75, 0x00]); // cmp r11, rsi; jne fail
        fails.Add(code.Count - 1);
        for (var x = 0; x < 6; x++)
        {
            code.AddRange([0x66, 0x48, 0x0F, 0x7E, (byte)(0xC3 + 8 * x)]); // movq rbx, xmmX
            code.AddRange([0x48, 0x3B, 0xDE, 0x75, 0x00]);                  // cmp rbx, rsi; jne fail
            fails.Add(code.Count - 1);
        }
        code.AddRange([0x48, 0xBE]);                  // mov rsi, r10's mark
        code.AddRange(BitConverter.GetBytes(r10Mark));
        code.AddRange([0x4C, 0x3B, 0xD6, 0x75, 0x00]); // cmp r10, rsi; jne fail
        fails.Add(code.Count - 1);
        code.AddRange([0xB8, 0x01, 0x00, 0x00, 0x00, 0xEB, 0x02]); // mov eax, 1; jmp done
        var fail = code.Count;
        code.AddRange([0x31, 0xC0]);                  // fail: xor eax, eax
        code.AddRange([0x48, 0x83, 0xC4, 0x38, 0x5E, 0x5B, 0xC3]); // done: add rsp, 0x38; pop rsi, rbx; ret
        var bytes = code.ToArray();
        foreach (var at in fails)
            bytes[at] = (byte)(fail - (at + 1));
        var harness = GameImage.AllocateCode(256);
        bytes.CopyTo(new Span<byte>(harness, bytes.Length));
        var run = (delegate* unmanaged<long, long, nint, int>)harness;

        if (Gains.ActionExpAt != 0)
            Check("the replaced EXP step", run(0, 0, Gains.ActionExpAt) == 1); // mode 0: nothing to do
        var units = game.TurnCode.Units;
        var unit = units + 4 * Battle.UnitSize;
        var saved = new byte[Battle.UnitSize];
        new Span<byte>(unit, Battle.UnitSize).CopyTo(saved);
        Check("First Move's end of turn", run(4, 0, (nint)game.TurnCode.EndOfTurn) == 1);
        saved.CopyTo(new Span<byte>(unit, Battle.UnitSize));
        if (Banditry.AiStub != 0)
            Check("the AI lists' skillset call (Mug)", run(14, 0, Banditry.AiStub) == 1);
        return failures;
    }

    /// <summary>
    /// Knockback landings through the hooked fall damage step (it clears both results, then its own
    /// arithmetic): plain ground, the three water depths, drowning, Float, lava, and the cap after
    /// the cut.
    /// </summary>
    public static int Landings(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: landings, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Landing.FallStepAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var b = game.Battle;
        var r = game.Routines;
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var own = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        *r.Target = target;
        *b.TargetResult = result;
        var attackerResult = *r.AttackerResult;
        *r.AttackerResult = own;
        game.Map.SetSize(10, 10);
        var tile = game.Map.TileAt(2, 3, 0);
        target[Battle.UnitX] = 2;
        target[Battle.UnitY] = 3;
        var step = (delegate* unmanaged<void>)Landing.FallStepAt;
        string Land(byte surface, byte depth, byte movement, byte status2, bool fell, int height, int jump, int maxHp)
        {
            tile[0] = surface;
            tile[3] = depth;
            target[Battle.UnitMovement + 2] = movement;
            target[Battle.UnitStatuses + 2] = status2;
            target[Battle.UnitJump] = (byte)jump;
            *(ushort*)(target + Battle.UnitMaxHp) = (ushort)maxHp;
            *Landing.Knocked = (byte)(fell ? 0x81 : 0x80);
            *Landing.Height = (byte)height;
            step();
            return $"{*(ushort*)(result + Battle.HpDamage)}/{result[Battle.ResultFlags]:X2}";
        }
        // A fall of 10 with Jump 3: (5 − 3) × 200 / 10 = 40.
        Check("a fall onto plain ground: 40", Land(0, 0, 0, 0, true, 10, 3, 200) == "40/81");
        Check("into water: 30 at depth 1, 20 at depth 2", Land(0, 0x20, 0, 0, true, 10, 3, 200) == "30/81" && Land(0, 0x40, 0, 0, true, 10, 3, 200) == "20/81");
        Check("deeper: drowned (999), or 10 with Swim", Land(0, 0x60, 0, 0, true, 10, 3, 200) == "999/81" && Land(0, 0x60, 0x40, 0, true, 10, 3, 200) == "10/81");
        Check("Float (status) and Levitate take none", Land(0, 0x60, 0, 0x40, true, 10, 3, 200) == "0/01" && Land(0, 0, 0x08, 0, true, 10, 3, 200) == "0/01");
        Check("Waterwalking keeps the plain fall", Land(0, 0x60, 0x80, 0, true, 10, 3, 200) == "40/81");
        Check("lava is 999 even without a fall, but not with Waterwalking", Land(0x12, 0, 0, 0, false, 0, 3, 200) == "999/81"
              && Land(0x12, 0, 0x80, 0, false, 0, 3, 200) == "0/01");
        // (16 − 3) × 923 / 10 = 1199; at depth 1 that's 900 (the cap comes after the cut).
        Check("a long fall into shallow water: 900", Land(0, 0x20, 0, 0, true, 32, 3, 923) == "900/81");
        Check("no fall, no damage", Land(0, 0x60, 0, 0, false, 10, 3, 200) == "0/01");
        game.Map.SetSize(0, 0);
        *r.Target = null;
        *b.TargetResult = null;
        *r.AttackerResult = attackerResult;
        foreach (var p in new[] { target, result, own })
            System.Runtime.InteropServices.NativeMemory.Free(p);
        return failures;
    }

    /// <summary>
    /// The mod's settings: ReMixed's defaults, a round trip through Config.json (bad values reset,
    /// rates clamped), the configurator as Reloaded-II's launcher drives it, and the DLL loaded the
    /// way the launcher loads it, in a context of its own without the hooks library.
    /// </summary>
    public static int OptionSettings(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: options, {name}{(ok ? "" : ": WRONG")}");
        }
        var defaults = new Options();
        Check($"ReMixed's defaults: {defaults}",
              defaults is { PlayerDeaths: Options.PlayerDeathRule.DiePermanently, Bodies: Options.BodyRule.TransformRandomly, JpGain: 125,
                            ExpGain: 100, BackAttacks: Options.BackAttackRule.BanditryAndMug, JobStats: Options.JobStatRule.JobOnly });
        Check("the rates in effect are the options'", Gains.JpRate == Options.Current.JpGain && Gains.ExpRate == Options.Current.ExpGain);
        var dir = Path.Combine(Path.GetTempPath(), $"IvaliceRemixed.Check.{Environment.ProcessId}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, Options.FileName);
            var changed = new Options
            {
                PlayerDeaths = Options.PlayerDeathRule.ReviveAfterBattle, Bodies = Options.BodyRule.Crystal, JpGain = 150,
                ExpGain = 80, BackAttacks = Options.BackAttackRule.Everyone, JobStats = Options.JobStatRule.JobAndSecondary,
            };
            changed.Write(path);
            var text = File.ReadAllText(path);
            var back = Options.Read(path, out var problem);
            Check($"written to {Options.FileName} with the settings' names, and read back the same",
                  problem == null && back.ToString() == changed.ToString() && text.Contains("\"ReviveAfterBattle\"") && !text.Contains("FilePath"));
            File.WriteAllText(path, "{ \"JpGain\": 999, \"ExpGain\": -5, \"PlayerDeaths\": 7 }");
            var clamped = Options.Read(path, out problem);
            Check($"out of range: JP {clamped.JpGain}%, EXP {clamped.ExpGain}%, player deaths {clamped.PlayerDeaths}",
                  problem == null && clamped is { JpGain: 255, ExpGain: 0, PlayerDeaths: Options.PlayerDeathRule.DiePermanently });
            File.WriteAllText(path, "not json");
            var broken = Options.Read(path, out problem);
            Check("an unreadable file gives the defaults and says why", problem != null && broken.ToString() == defaults.ToString());
            File.Delete(path);
            var missing = Options.Read(path, out problem);
            Check("no file gives the defaults", problem == null && missing.ToString() == defaults.ToString());

            var configurator = new Configurator();
            configurator.SetModDirectory(dir);
            configurator.SetConfigDirectory(dir);
            var configs = configurator.GetConfigurations();
            ((Options)configs[0]).JpGain = 200;
            configs[0].Save();
            Check("the configurator's settings page saves to Config.json",
                  configs.Length == 1 && configs[0].ConfigName == "Ivalice ReMixed" && Options.Read(path, out _).JpGain == 200);

            var dll = typeof(Options).Assembly.Location;
            var context = new LauncherContext();
            try
            {
                var assembly = context.LoadFromAssemblyPath(dll);
                var types = assembly.GetTypes();
                var found = types.Where(t => typeof(Reloaded.Mod.Interfaces.IConfiguratorV1).IsAssignableFrom(t) && !t.IsAbstract).ToArray();
                var launched = (Reloaded.Mod.Interfaces.IConfiguratorV2)Activator.CreateInstance(found.Single())!;
                launched.SetConfigDirectory(dir);
                var pages = launched.GetConfigurations();
                Check($"loaded as the launcher loads it, without the hooks library: {types.Length} types, a configurator, " +
                      $"{pages.Length} settings page", found.Length == 1 && pages.Length == 1);
            }
            catch (Exception e)
            {
                Check($"loaded as the launcher loads it: {e.GetType().Name}: {e.Message}", false);
            }
            finally
            {
                context.Unload();
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
        return failures;
    }

    /// <summary>
    /// ReMixed's Brave chance for reactions: the rule, the asm load for each register pair the sites use
    /// (through a harness, on made-up units), and every site calling its stub.
    /// </summary>
    public static int BraveReactionRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: reactions' Brave chance, {name}{(ok ? "" : ": WRONG")}");
        }
        var sites = BraveReactions.Sites;
        var image = game.Image.ImageStart;
        Check($"{sites.Length} Brave loads call their stubs ({string.Join(", ", sites.Select(s => $"{s.Name} 0x{s.At - image:X}"))})",
              sites.Length == 16 && sites.All(s =>
              {
                  var at = (byte*)s.At;
                  return at[0] == 0xE8 && GameImage.Rip(s.At, 1, 5) == s.Stub
                         && Enumerable.Range(5, s.Length - 5).All(k => at[k] == 0x90);
              }));
        var rolls = sites.Where(s => s.CallsRoll).ToArray();
        Check("Finger Guard's roll goes through its stub on to the roll (jmp [rip+0] to it)",
              rolls.Length == 1 && Enumerable.Range(0, 64 - 14).Any(k =>
              {
                  var p = (byte*)rolls[0].Stub + k;
                  return p[0] == 0xFF && p[1] == 0x25 && *(int*)(p + 2) == 0 && *(long*)(p + 6) == BraveReactions.RollAt;
              }));
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var cases = new (byte Gender, bool MoveFind, byte Brave)[]
        {
            (0x80, true, 30), (0x40, true, 70), (0x80, true, 50), (0x40, true, 0), (0x80, true, 100), (0x40, true, 49),
            (0x80, false, 30), (0x20, true, 30), (0x00, true, 10), (0xA0, true, 20),
        };
        int Set((byte Gender, bool MoveFind, byte Brave) c)
        {
            unit[6] = c.Gender;
            unit[Battle.UnitMovement + 2] = (byte)(c.MoveFind ? 0x41 : 0x40); // other movement bits stay as they are
            unit[Battle.UnitBrave] = c.Brave;
            return BraveReactions.Chance(unit);
        }
        Check("a human with Move-Find Item reacts with max(Brave, 100 − Brave): 30 → 70, 70, 50, 0 → 100, 100, 49 → 51; " +
              "without it, a monster or no gender, the Brave",
              cases.Select(Set).SequenceEqual([70, 70, 50, 100, 100, 51, 30, 30, 10, 80]));
        var harness = GameImage.AllocateCode(64);
        var stub = GameImage.AllocateCode(64);
        foreach (var (u, d) in BraveReactions.RegisterPairs)
        {
            // push rbx; mov r11, rcx; mov rU, rcx; sub rsp, 0x20; call rdx; add rsp, 0x20; cmp rU, r11; jne bad;
            // mov eax, eD; jmp out; bad: mov eax, -1; out: pop rbx; ret
            var h = new List<byte> { 0x53, 0x49, 0x89, 0xCB };
            h.AddRange([(byte)(u >= 8 ? 0x49 : 0x48), 0x89, (byte)(0xC8 | (u & 7))]);
            h.AddRange([0x48, 0x83, 0xEC, 0x20, 0xFF, 0xD2, 0x48, 0x83, 0xC4, 0x20]);
            h.AddRange([(byte)(u >= 8 ? 0x4D : 0x4C), 0x39, (byte)(0xD8 | (u & 7))]);
            h.AddRange([0x75, 0x04, 0x89, (byte)(0xC0 | (d << 3)), 0xEB, 0x05, 0xB8, 0xFF, 0xFF, 0xFF, 0xFF, 0x5B, 0xC3]);
            h.ToArray().CopyTo(new Span<byte>(harness, 64));
            var load = new List<byte>();
            BraveReactions.WriteLoad(load, u, d);
            load.Add(0xC3);
            load.ToArray().CopyTo(new Span<byte>(stub, 64));
            var run = (delegate* unmanaged<byte*, byte*, int>)harness;
            var ok = cases.All(c =>
            {
                var want = Set(c);
                return run(unit, stub) == want;
            });
            Check($"the stub's load into e{"acd"[d]}x from {(u >= 8 ? $"r{u}" : $"r{"acdb"[u]}x")} gives the same and keeps the unit's register", ok);
        }
        Check("MP Rage's roll asks the same rule", BraveReactions.Active && BraveReactions.ChanceOf(unit) == BraveReactions.Chance(unit));
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        return failures;
    }

    /// <summary>
    /// ReMixed's in-place changes: each written, and the remaster's physical evade setup run on a made-up target
    /// without Weapon Guard, holding a weapon with evasion, which now counts.
    /// </summary>
    public static int InPlaceChanges(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: in-place changes, {name}{(ok ? "" : ": WRONG")}");
        }
        var image = game.Image.ImageStart;
        Check($"{InPlace.Applied.Count} written ({string.Join(", ", InPlace.Applied.Select(a => $"{a.Name} 0x{a.At - image:X}"))})",
              InPlace.Applied.Count == 13 && InPlace.Applied.All(a => new ReadOnlySpan<byte>((void*)a.At, a.Bytes.Length).SequenceEqual(a.Bytes)));
        // The AI's "can't be seen" test through the game's routine (0x48 before the patched test): a Transparent unit.
        var seen = InPlace.Applied.FirstOrDefault(a => a.Name.StartsWith("the AI sees"));
        if (seen.Name != null)
        {
            var hidden = (delegate* unmanaged<int, int>)(seen.Start - 0x48);
            const int index = 5;
            var units = game.TurnCode.Units;
            var unitAt = units + index * Battle.UnitSize;
            var known = (byte*)(game.Image.ImageStart + *(int*)(seen.Start - 0x0B + 4)) + 16 * index; // [0x1872EE0 + 16 × index]
            var savedKnown = *known;
            var savedUnit = new Span<byte>(unitAt, Battle.UnitSize).ToArray();
            new Span<byte>(unitAt, Battle.UnitSize).Clear();
            *known = 1;
            unitAt[1] = index;
            unitAt[Battle.UnitStatuses + 2] = 0x10;                   // Transparent
            var transparent = hidden(index);
            unitAt[Battle.UnitStatuses + 2] = 0;
            var plain = hidden(index);
            *known = savedKnown;
            savedUnit.CopyTo(new Span<byte>(unitAt, Battle.UnitSize));
            Check($"the AI's can't-be-seen test (0x{seen.Start - 0x48 - image:X}): a Transparent unit {transparent} (ReMixed: seen), a plain one {plain}",
                  transparent == 0 && plain == 0);
        }
        // Deep water through the game's routine (its pattern starts at it): the unit on tile (0, 0) at depth 2.
        var water = InPlace.Applied.FirstOrDefault(a => a.Name.StartsWith("deep water"));
        if (water.Name != null)
        {
            var deep = (delegate* unmanaged<byte*, int>)water.Start;
            var tiles = (byte*)GameImage.Rip(water.Start + 0x36, 3, 7); // the tiles' depth bytes, 8 apart
            var depthBefore = tiles[0];
            tiles[0] = 0x40;                                              // depth 2
            var swimmer = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
            int Deep(int movement, int statuses4)
            {
                swimmer[Battle.UnitMovement + 2] = (byte)movement;            // +0x9E
                swimmer[Battle.UnitStatuses + 4] = (byte)statuses4;           // +0x65
                return deep(swimmer);
            }
            var none = Deep(0, 0);
            var breathes = Deep(0x10, 0);
            var floats = Deep(0x08, 0);
            var stuck = Deep(0, 0x04);
            tiles[0] = depthBefore;
            System.Runtime.InteropServices.NativeMemory.Free(swimmer);
            Check($"in deep water: no reactions ({none}), but with Waterbreathing (ReMixed) {breathes} and Float {floats}; Don't Act {stuck}",
                  none == 2 && breathes == 0 && floats == 0 && stuck == 1);
        }
        var evasion = InPlace.Applied.FirstOrDefault(a => a.Name.StartsWith("weapon evasion"));
        if (evasion.Name == null)
            return failures;
        var setup = game.RuleCode.EvadeSetup;               // through the unevadable rule's hook; no attacker here
        var rightEv = (byte*)game.RuleCode.Evades + 1;        // the right hand's evade byte (0x7B0782)
        var item = RandomEquipment.ItemData;
        var weapon = RandomEquipment.WeaponData;
        var sword = Enumerable.Range(1, 0x7F).FirstOrDefault(id => (item(id)[3] & 0x80) != 0 && weapon(id)[5] > 0);
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        unit[6] = 0x80;
        *(ushort*)(unit + 0x20) = (ushort)sword;
        var target = game.Routines.Target;
        var saved = *target;
        *target = unit;
        setup();
        *target = saved;
        Check($"a weapon's evasion counts without Weapon Guard (item 0x{sword:X2}: {rightEv[0]}%, its data's {weapon(sword)[5]}%)",
              sword != 0 && weapon(sword)[5] > 0 && rightEv[0] == weapon(sword)[5]);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        return failures;
    }

    /// <summary>
    /// ReMixed's evasion by angle through the remaster's own angle routine on made-up units (front, back, and the side
    /// with and without Weapon Guard and hand items), and Invite's control through its stub in a harness.
    /// </summary>
    public static int AngleAndInvite(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: angle and Invite, {name}{(ok ? "" : ": WRONG")}");
        }
        if (SideEvasion.Angle == 0 || InviteControl.StubAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var image = game.Image.ImageStart;
        Check($"the angle's evade tail (0x{SideEvasion.At - image:X}) and Invite's stores (0x{InviteControl.At - image:X}) jump to stubs",
              ((byte*)SideEvasion.At)[0] == 0xE9 && GameImage.Rip(SideEvasion.At, 1, 5) == SideEvasion.StubAt
              && ((byte*)SideEvasion.At)[19] == 0x90 && ((byte*)SideEvasion.At)[20] == 0xC3
              && ((byte*)InviteControl.At)[0] == 0xE9 && GameImage.Rip(InviteControl.At, 1, 5) == InviteControl.StubAt);
        var angle = (delegate* unmanaged<void>)SideEvasion.Angle;
        var evades = (byte*)game.RuleCode.Evades;
        var attacker = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var attackerSlot = game.Battle.Attacker;
        var targetSlot = game.Routines.Target;
        var savedAttacker = *attackerSlot;
        var savedTarget = *targetSlot;
        *attackerSlot = attacker;
        *targetSlot = target;
        attacker[0x4F] = 5; attacker[0x50] = 5;
        target[0x4F] = 6; target[0x50] = 5; // the attacker is one tile west: facing 1 is front, 3 back, 0 and 2 side
        string Run(int facing, bool guard, bool holding)
        {
            target[0x51] = (byte)facing;
            target[Battle.UnitReactions + 3] = (byte)(guard ? 0x40 : 0);
            for (var k = 0; k < 4; k++)
                *(ushort*)(target + 0x20 + 2 * k) = 0xFF;
            if (holding)
                *(ushort*)(target + 0x20) = 0x13;
            evades[0] = 10; evades[1] = 20; evades[2] = 30; evades[3] = 40; evades[4] = 9;
            angle();
            return $"{evades[0]} {evades[1]} {evades[2]} {evades[3]} {evades[4]}";
        }
        Check("from the front, every evade (accessory, right, left, class), angle 0", Run(1, false, true) == "10 20 30 40 0");
        Check("from the back, only the accessory's, angle 2", Run(3, true, true) == "10 0 0 0 2");
        Check("from the side without Weapon Guard: no class or right hand's evade", Run(0, false, true) == "10 0 30 0 1");
        Check("from the side with Weapon Guard, holding something: no class evade (vanilla's)", Run(2, true, true) == "10 20 30 0 1");
        Check("from the side with Weapon Guard and empty hands: the class evade stays", Run(0, true, false) == "10 0 30 40 1");
        *attackerSlot = savedAttacker;
        *targetSlot = savedTarget;
        System.Runtime.InteropServices.NativeMemory.Free(target);
        System.Runtime.InteropServices.NativeMemory.Free(attacker);

        // Invite: a copy of the stub that returns to a harness instead of going back into the game's code.
        var ret = GameImage.AllocateCode(16);
        ret[0] = 0xC3;
        var stub = GameImage.AllocateCode(48);
        InviteControl.WriteStub(stub, (nint)ret);
        var harness = GameImage.AllocateCode(32);
        // mov r10, rcx; mov eax, edx; mov ecx, r8d; sub rsp, 0x28; call r9; add rsp, 0x28; ret
        byte[] h = [0x4C, 0x8B, 0xD1, 0x89, 0xD0, 0x44, 0x89, 0xC1, 0x48, 0x83, 0xEC, 0x28, 0x41, 0xFF, 0xD1, 0x48, 0x83, 0xC4, 0x28, 0xC3];
        h.CopyTo(new Span<byte>(harness, h.Length));
        var run = (delegate* unmanaged<byte*, int, int, byte*, void>)harness;
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        unit[5] = 0x00;
        run(unit, 0x00, 0x01, stub);
        var player = unit[5] == 0x08 && unit[0x1EE] == 0x09;
        unit[5] = 0x18;
        run(unit, 0x10, 0x11, stub);
        Check("invited by the player's team: control bit 0x08 in +5 and +0x1EE; by another team: cleared, as the remaster does",
              player && unit[5] == 0x10 && unit[0x1EE] == 0x11);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        return failures;
    }

    /// <summary>
    /// ReMixed's evasion statuses through both entries of the statuses stub and a copy of the attacker stub, the
    /// Reflect reaction and the hit mark through copies of their stubs, and formulas 0x15 and 0x42 with stand-ins for
    /// the game steps they call.
    /// </summary>
    public static int EvasionAndOthers(Game game, Log log)
    {
        _game = game;
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: evasion and others, {name}{(ok ? "" : ": WRONG")}");
        }
        if (EvadeChecks.PhysicalEntry == 0 || EvadeChecks.AttackerStub == 0 || ReflectReaction.StubAt == 0 || Learning.MarkStub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var image = game.Image.ImageStart;
        bool JumpsTo(nint at, nint to) => ((byte*)at)[0] == 0xE9 && GameImage.Rip(at, 1, 5) == to;
        Check($"the status routine (0x{EvadeChecks.StatusRoutine - image:X}) jumps to the physical entry, the magic hit check's "
              + $"call (0x{EvadeChecks.MagicCall - image:X}) calls the magic entry",
              JumpsTo(EvadeChecks.StatusRoutine, EvadeChecks.PhysicalEntry)
              && ((byte*)EvadeChecks.MagicCall)[0] == 0xE8 && GameImage.Rip(EvadeChecks.MagicCall, 1, 5) == EvadeChecks.MagicEntry);
        Check($"the physical check's attacker statuses (0x{EvadeChecks.AttackerAt - image:X}), the Reflect check "
              + $"(0x{ReflectReaction.At - image:X}) and the hit mark (0x{Learning.MarkAt - image:X}) jump to stubs",
              JumpsTo(EvadeChecks.AttackerAt, EvadeChecks.AttackerStub) && ((byte*)EvadeChecks.AttackerAt)[38] == 0x90
              && JumpsTo(ReflectReaction.At, ReflectReaction.StubAt) && JumpsTo(Learning.MarkAt, Learning.MarkStub));

        var evades = (byte*)game.RuleCode.Evades;
        var hit = evades - 2;
        var attacker = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var attackerSlot = game.Battle.Attacker;
        var targetSlot = game.Routines.Target;
        var savedAttacker = *attackerSlot;
        var savedTarget = *targetSlot;
        *attackerSlot = attacker;
        *targetSlot = target;
        const int st = Battle.UnitStatuses;

        // The target's statuses.
        string Statuses(nint entry, int dead, int transparent, int asleep, int defending)
        {
            new Span<byte>(target + st, 5).Clear();
            target[st] = (byte)(dead | defending);
            target[st + 2] = (byte)transparent;
            target[st + 4] = (byte)asleep;
            evades[0] = 10; evades[1] = 20; evades[2] = 30; evades[3] = 40;
            *hit = 100;
            ((delegate* unmanaged<void>)entry)();
            return $"{evades[0]} {evades[1]} {evades[2]} {evades[3]} {*hit}";
        }
        var physical = EvadeChecks.PhysicalEntry;
        var magic = EvadeChecks.MagicEntry;
        Check("nothing: every evasion kept", Statuses(physical, 0, 0, 0, 0) == "10 20 30 40 100" && Statuses(magic, 0, 0, 0, 0) == "10 20 30 40 100");
        Check("Dead loses its evasion (ReMixed adds it)", Statuses(physical, 0x20, 0, 0, 0) == "0 0 0 0 100" && Statuses(magic, 0x20, 0, 0, 0) == "0 0 0 0 100");
        Check("asleep: none against either check", Statuses(physical, 0, 0, 0x10, 0) == "0 0 0 0 100" && Statuses(magic, 0, 0, 0x10, 0) == "0 0 0 0 100");
        Check("asleep and Transparent: none against a physical attack, all against magic",
              Statuses(physical, 0, 0x10, 0x10, 0) == "0 0 0 0 100" && Statuses(magic, 0, 0x10, 0x10, 0) == "10 20 30 40 100");
        Check("Defending halves the hit chance", Statuses(physical, 0, 0, 0, 0x02) == "10 20 30 40 50" && Statuses(magic, 0, 0x10, 0x10, 0x02) == "10 20 30 40 50");

        // The attacker's statuses, through a copy of the stub that returns to a harness.
        var ret = GameImage.AllocateCode(16);
        ret[0] = 0xC3;
        var copy = GameImage.AllocateCode(128);
        EvadeChecks.WriteAttackerStub(copy, (nint)ret, EvadeChecks.Unevadable, evades, targetSlot);
        var harness = GameImage.AllocateCode(32);
        byte[] h = [0x48, 0x89, 0xC8, 0x48, 0x83, 0xEC, 0x28, 0xFF, 0xD2, 0x48, 0x83, 0xC4, 0x28, 0xC3]; // mov rax, rcx; call rdx
        h.CopyTo(new Span<byte>(harness, h.Length));
        var run = (delegate* unmanaged<byte*, byte*, void>)harness;
        string Attack(int blind, bool concentrate, bool hidden, bool targetHidden, bool marked)
        {
            new Span<byte>(attacker + st, 5).Clear();
            attacker[st + 1] = (byte)blind;
            attacker[st + 2] = (byte)(hidden ? 0x10 : 0);
            attacker[Battle.UnitSupports + 1] = (byte)(concentrate ? 1 : 0);
            target[st + 2] = (byte)(targetHidden ? 0x10 : 0);
            *EvadeChecks.Unevadable = (byte)(marked ? 1 : 0);
            evades[0] = 10; evades[1] = 20; evades[2] = 30; evades[3] = 40;
            *hit = 100;
            run(attacker, copy);
            return $"{evades[0]} {evades[1]} {evades[2]} {evades[3]} {*hit}";
        }
        Check("a plain attacker: evasion as set up", Attack(0, false, false, false, false) == "10 20 30 40 100");
        Check("Concentrate: no evasion", Attack(0, true, false, false, false) == "0 0 0 0 100");
        Check("blind: the accessory evasion +50, the hit chance not halved", Attack(0x20, false, false, false, false) == "60 20 30 40 100");
        Check("blind with Concentrate: no evasion, then +25", Attack(0x20, true, false, false, false) == "25 0 0 0 100");
        Check("blind and attacking an unevadable ability: +25", Attack(0x20, false, false, false, true) == "25 0 0 0 100");
        Check("confused: +25 (blind and confused: +50)", Attack(0x10, false, false, false, false) == "35 20 30 40 100"
              && Attack(0x30, false, false, false, false) == "60 20 30 40 100");
        Check("a Transparent target against a visible attacker: accessory evasion 100 (150 if blind)",
              Attack(0, false, false, true, false) == "100 20 30 40 100" && Attack(0x20, false, false, true, false) == "150 20 30 40 100");
        Check("a Transparent target against a Transparent attacker: no evasion", Attack(0, false, true, true, false) == "0 0 0 0 100");
        *EvadeChecks.Unevadable = 0;
        *attackerSlot = savedAttacker;
        *targetSlot = savedTarget;

        // The Reflect reaction: a copy that returns 1 for "reflect" and 0 for "skip".
        var yes = GameImage.AllocateCode(16);
        byte[] one = [0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3];
        one.CopyTo(new Span<byte>(yes, one.Length));
        var no = GameImage.AllocateCode(16);
        byte[] zero = [0x31, 0xC0, 0xC3];
        zero.CopyTo(new Span<byte>(no, zero.Length));
        var reflect = GameImage.AllocateCode(48);
        ReflectReaction.WriteStub(reflect, (nint)yes, (nint)no);
        var rh = GameImage.AllocateCode(32);
        byte[] r = [0x49, 0x89, 0xCA, 0x48, 0x83, 0xEC, 0x28, 0xFF, 0xD2, 0x48, 0x83, 0xC4, 0x28, 0xC3]; // mov r10, rcx; call rdx
        r.CopyTo(new Span<byte>(rh, r.Length));
        var reflects = (delegate* unmanaged<byte*, byte*, int>)rh;
        int Reflects(bool status, bool reaction)
        {
            target[st + 4] = (byte)(status ? 0x02 : 0);
            target[Battle.UnitReactions + 2] = (byte)(reaction ? 0x20 : 0);
            return reflects(target, reflect);
        }
        Check("Reflect: the status or the reaction (0x1B8) reflects, neither doesn't",
              Reflects(true, false) == 1 && Reflects(false, true) == 1 && Reflects(true, true) == 1 && Reflects(false, false) == 0);

        // The hit mark keeps the can-learn bit.
        var mark = GameImage.AllocateCode(32);
        Learning.WriteMarkStub(mark, (nint)ret);
        var mh = GameImage.AllocateCode(32);
        byte[] m = [0x48, 0x89, 0xD0, 0x48, 0x89, 0xCA, 0x48, 0x83, 0xEC, 0x28, 0xFF, 0xD0, 0x48, 0x83, 0xC4, 0x28, 0xC3]; // mov rdx, rcx; call rax
        m.CopyTo(new Span<byte>(mh, m.Length));
        var marks = (delegate* unmanaged<byte*, byte*, void>)mh;
        string Mark(params byte[] before)
        {
            var after = new List<string>();
            foreach (var b in before)
            {
                target[0x1BB] = b;
                marks(target, mark);
                after.Add(target[0x1BB].ToString());
            }
            return string.Join(" ", after);
        }
        Check("the hit mark: 1, keeping a can-learn 2 (0, 2, 4, 6 become 1, 3, 1, 3)", Mark(0, 2, 4, 6) == "1 3 1 3");

        // Formulas 0x15 and 0x42 with stand-ins for the game's steps.
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var own = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var resultSlot = game.Battle.TargetResult;
        var ownSlot = game.Routines.AttackerResult;
        var savedResult = *resultSlot;
        var savedOwn = *ownSlot;
        var ability = game.Battle.Ability;
        var savedAbility = new byte[0x14];
        new ReadOnlySpan<byte>(ability, 0x14).CopyTo(savedAbility);
        var routines = game.Routines;
        var realHit = routines.MagicHit;
        var realStatusHit = Formulas.StatusHit;
        var realZodiac = routines.MagicMod4;
        var realHp = routines.HpDamage;
        var realStatuses = routines.Statuses;
        *resultSlot = result;
        *ownSlot = own;
        *attackerSlot = attacker;
        *targetSlot = target;
        routines.MagicMod4 = &FakeNothing;
        routines.HpDamage = &FakeHpDamage;
        routines.Statuses = &FakeStatuses;
        string Return2(bool miss, bool maintenance)
        {
            new Span<byte>(result, 0x40).Clear();
            result[0] = 1;
            if (miss)
                routines.MagicHit = &FakeMiss;
            else
                routines.MagicHit = &FakeHit;
            Formulas.StatusHit = &FakeHit;
            target[Battle.UnitSupports + 2] = (byte)(maintenance ? 0x04 : 0);
            Formulas.Run(0x15);
            return $"{result[0]} {result[2]} {result[0x15]:X2} {result[Battle.ResultFlags]}";
        }
        Check("Return 2: CT to 0 (a lowering of 0x7F, flags 1)", Return2(false, false) == "1 0 7F 1");
        Check("Return 2 on a target with Maintenance: a miss (ReMixed adds it)", Return2(false, true) == "0 7 7F 1");
        Check("Return 2 missed: nothing", Return2(true, false) == "1 0 00 0");
        new Span<byte>(result, 0x40).Clear();
        new Span<byte>(own, 0x40).Clear();
        result[0] = 1;
        attacker[Battle.UnitPa] = 12;
        ability[9] = 4;
        ability[10] = 10;
        _statusSteps = 0;
        Formulas.Run(0x42);
        Check("Worker 8's Crush (PA 12, Y 10, X 4): 120 damage through the damage step, 30 back, the statuses applied",
              *(ushort*)(result + Battle.HpDamage) == 120 && result[Battle.ResultFlags] == 0x80 && own[0] == 1
              && *(ushort*)(own + Battle.HpDamage) == 30 && own[Battle.ResultFlags] == 0x80 && _statusSteps == 1);
        new Span<byte>(result, 0x40).Clear();
        new Span<byte>(own, 0x40).Clear();
        result[0] = 1;
        new Span<byte>(target, Battle.UnitSize).Clear(); // no statuses or supports left from the cases above
        attacker[Battle.UnitMa] = 8;
        ability[10] = 10;
        Formulas.Run(0x21);
        Check($"Bizen Boat (MA 8, Y 10): {*(ushort*)(result + Battle.MpDamage)} MP damage through the damage step, HP {*(ushort*)(result + Battle.HpDamage)}, flags 0x{result[Battle.ResultFlags]:X2}",
              *(ushort*)(result + Battle.MpDamage) == 80 && *(ushort*)(result + Battle.HpDamage) == 0 && result[Battle.ResultFlags] == 0x20);
        routines.MagicHit = realHit;
        Formulas.StatusHit = realStatusHit;
        routines.MagicMod4 = realZodiac;
        routines.HpDamage = realHp;
        routines.Statuses = realStatuses;
        savedAbility.CopyTo(new Span<byte>(ability, 0x14));
        *resultSlot = savedResult;
        *ownSlot = savedOwn;
        *attackerSlot = savedAttacker;
        *targetSlot = savedTarget;
        Check("formulas 0x15, 0x21 and 0x42 are ReMixed's", Formulas.Replaced(0x15) && Formulas.Replaced(0x21) && Formulas.Replaced(0x42));
        System.Runtime.InteropServices.NativeMemory.Free(own);
        System.Runtime.InteropServices.NativeMemory.Free(result);
        System.Runtime.InteropServices.NativeMemory.Free(target);
        System.Runtime.InteropServices.NativeMemory.Free(attacker);
        return failures;
    }

    private static int _statusSteps;
    private static int _facingSeen = -1;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeUsable(int index, int skillset, int type) => skillset == TwoSpells.BlackMagic ? 0 : 1;

    private static (nint Unit, int Ability, int Shown) _limitAsked;

    /// <summary>Stands in for Math Skill's spell list: asks the spell limit about one White Magic spell.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static nint FakeMathSpells(nint index, nint skillset, nint list, nint unused)
    {
        _limitAsked.Shown = TwoSpells.Shows((byte*)_limitAsked.Unit, TwoSpells.WhiteMagic, _limitAsked.Ability) ? 1 : 0;
        return 0;
    }

    private static readonly List<int> _formulasRun = [];

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FormulaOne() => _formulasRun.Add(1);

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FormulaTwo() => _formulasRun.Add(2);

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void CountStatusStep() => _formulasRun.Add(-1);

    /// <summary>
    /// The gun skills: ReMixed's table in the mod's copy, the classifier through the game's code, the bonus reads made
    /// signed, formula 5 with stand-ins for the weapon's formulas and the status step, the XA rules, and Trick Shot
    /// through the can-react check.
    /// </summary>
    public static int GunSkillRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: gun skills, {name}{(ok ? "" : ": WRONG")}");
        }
        if (GunSkills.Table == null)
        {
            Check("aren't installed", false);
            return failures;
        }
        var t = GunSkills.Table;
        string Row(int id) => $"{t[2 * (id - 0x190)]}/{(sbyte)t[2 * (id - 0x190) + 1]}/{GunSkills.StatusSet(id):X2}";
        Check($"ReMixed's table: Gunk Shot {Row(0x192)}, Hook Shot {Row(0x193)}, Two Shots {Row(0x195)}, Charge +1 {Row(0x196)}, Aimed Shot {Row(0x19C)}, Disruptive Shot {Row(0x19D)} (CT/bonus/status set)",
              Row(0x192) == "3/-6/5D" && Row(0x193) == "3/-4/00" && Row(0x195) == "4/0/00" && Row(0x196) == "3/1/00"
              && Row(0x19C) == "2/0/00" && Row(0x19D) == "1/-1/2B");
        Check($"{GunSkills.Patched.Count} changes written", GunSkills.Patched.Count == 28);
        // The classifier through the game's code: Jump up to 0x18F, Charge from 0x190 with the mod's table.
        var image = game.Image.ImageStart;
        var classify = (delegate* unmanaged<int, byte**, byte**, int>)game.Image.FindUnique(
            "40 53 48 83 EC 20 81 E1 FF 01 00 00 49 8B D8 44 8B C9 48 8D 0D");
        byte* common, own;
        var jump = classify(0x18B, &common, &own);
        var gunk = classify(0x192, &common, &own);
        var gunkData = own;
        var charge = classify(0x19D, &common, &own);
        Check($"the classifier: 0x18B kind {jump}, Gunk Shot kind {gunk} at the table's +{gunkData - t}, Disruptive Shot kind {charge} at +{own - t}",
              jump == 3 && gunk == 4 && gunkData == t + 4 && charge == 4 && own == t + 26);
        // The bonus reads: movsx now.
        var bonus = GunSkills.Bonus;
        var signed = 0;
        var from = (byte*)(nint)game.Routines.WeaponAttack - 0x2000;
        for (var k = 0; k < 0x4000; k++)
        {
            var p = from + k;
            if (p[0] == 0x0F && p[1] == 0xBE && (p[2] == 0x05 || p[2] == 0x0D) && p + 7 + *(int*)(p + 3) == bonus)
                signed++;
        }
        Check($"the six reads of the bonus (0x{(nint)bonus - image:X}) are signed: {signed}", signed == 6);

        // Formula 5 with stand-ins for formulas 1 and 2 and the status step.
        var b = game.Battle;
        var r = game.Routines;
        var code = game.RuleCode;
        var abilityBefore = *b.AbilityId;
        var weaponBefore = b.Weapon[2];
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var resultBefore = *b.TargetResult;
        *b.TargetResult = result;
        var one = Rules.SwapFormula(1, (nint)(delegate* unmanaged<void>)&FormulaOne);
        var two = Rules.SwapFormula(2, (nint)(delegate* unmanaged<void>)&FormulaTwo);
        var realStatuses = r.Statuses;
        r.Statuses = &CountStatusStep;
        var statusIdBefore = *code.StatusId;
        var copyBefore = new Span<byte>(code.StatusCopy, 6).ToArray();
        string Shoot(int ability, int weaponFormula)
        {
            _formulasRun.Clear();
            *b.AbilityId = (ushort)ability;
            b.Weapon[2] = (byte)weaponFormula;
            result[0] = 0; // a miss, so Hook Shot doesn't pull here
            Formulas.Run(0x05);
            return string.Join(" ", _formulasRun);
        }
        var gunkShot = Shoot(0x192, 1);
        var gunkSet = *code.StatusId == 0x5D && new ReadOnlySpan<byte>(code.StatusCopy, 6).SequenceEqual(new ReadOnlySpan<byte>(code.StatusSets + 6 * 0x5D, 6));
        var magicGunk = Shoot(0x192, 2);
        var magicHook = Shoot(0x193, 2);
        var bare = Shoot(0x196, 0);
        Rules.SwapFormula(1, one);
        Rules.SwapFormula(2, two);
        r.Statuses = realStatuses;
        *code.StatusId = statusIdBefore;
        copyBefore.CopyTo(new Span<byte>(code.StatusCopy, 6));
        Check($"formula 5: Gunk Shot runs the weapon's formula 1 then its status set 0x5D ({gunkShot}; set copied: {gunkSet}); with a spell-casting weapon, formula 1 ({magicGunk}); Hook Shot keeps formula 2 ({magicHook}); no weapon formula, 1 ({bare})",
              gunkShot == "1 -1" && gunkSet && magicGunk == "1 -1" && magicHook == "2" && bare == "1");

        // The XA rules at the attack modifiers.
        var item = RandomEquipment.ItemData;
        ushort ItemOf(int type)
        {
            for (ushort id = 1; id < 0xFE; id++)
                if (r.GetItem(id)[5] == type)
                    return id;
            return 0xFF;
        }
        var weaponItemBefore = *r.WeaponItemId;
        var simulatingBefore = *r.Simulating;
        int Xa(int ability, int xa, int type, int simulating = 0)
        {
            *b.AbilityId = (ushort)ability;
            *r.WeaponItemId = ItemOf(type);
            *r.Simulating = simulating;
            *b.XA = (short)xa;
            GunSkills.AdjustXa();
            return *b.XA;
        }
        var bow = Xa(0x195, 40, 12);
        var gun = Xa(0x195, 40, 10);
        var sword = Xa(0x195, 40, 3);
        var planned = Xa(0x195, 40, 12, 1);
        var floor = Xa(0x192, -3, 10);
        var plain = Xa(0x10, -3, 10);
        *r.WeaponItemId = weaponItemBefore;
        *r.Simulating = simulatingBefore;
        Check($"Two Shots' XA 40: {bow} with a bow, {gun} with a gun, {sword} with a sword, {planned} while the AI plans with a bow; Gunk Shot's -3 becomes {floor}, another ability's stays {plain}",
              bow == 30 && gun == 25 && sword == 20 && planned == 60 && floor == 1 && plain == -3);

        // Trick Shot through the can-react check (the target: a made-up unit that can react).
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var targetBefore = *r.Target;
        *r.Target = target;
        var canReact = (delegate* unmanaged<int>)GunSkills.CanReactAt;
        *b.AbilityId = 0x10;
        var otherAbility = canReact();
        *b.AbilityId = 0x194;
        var trick = canReact();
        *r.Target = targetBefore;
        System.Runtime.InteropServices.NativeMemory.Free(target);
        Check($"Trick Shot's target can't react ({trick}); another ability's can ({otherAbility})", trick == 1 && otherAbility == 0);

        *b.AbilityId = abilityBefore;
        b.Weapon[2] = weaponBefore;
        *b.TargetResult = resultBefore;
        System.Runtime.InteropServices.NativeMemory.Free(result);
        return failures;
    }

    private static (int Index, int Job, int Secondary) _jobChanged;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeJobChange(int index, int job, int reequip)
    {
        var record = (byte*)JobChange.Records[(short)index];
        _jobChanged = ((short)index, job, *(ushort*)(record + 0x60));
        return 1;
    }

    /// <summary>The job change through its hook, on a made-up menu record, with a stand-in for the game's change.</summary>
    public static int JobChangeSecondary(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: job change, {name}{(ok ? "" : ": WRONG")}");
        }
        if (JobChange.ChangeAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var units = game.Units;
        const int squire = 0x4A, monk = 0x4E, slot = 3;
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var before = JobChange.Records[slot];
        JobChange.Records[slot] = (nint)record;
        var real = JobChange.GameChange;
        JobChange.GameChange = &FakeJobChange;
        var change = (delegate* unmanaged<int, int, int, int>)JobChange.ChangeAt;
        int ChangeTo(int from, int to, int secondary)
        {
            *(ushort*)(record + 0x24) = (ushort)from;
            *(ushort*)(record + 0x60) = (ushort)secondary;
            _jobChanged = (-1, -1, -1);
            change(slot, to, 1);
            return _jobChanged.Index == slot && _jobChanged.Job == to ? _jobChanged.Secondary : -1;
        }
        int squireSet = units.SkillsetOf(squire), monkSet = units.SkillsetOf(monk);
        var kept = ChangeTo(squire, monk, monkSet);
        var other = ChangeTo(squire, monk, 0x0A);
        var none = ChangeTo(squire, monk, 0);
        JobChange.GameChange = real;
        JobChange.Records[slot] = before;
        System.Runtime.InteropServices.NativeMemory.Free(record);
        Check($"Squire (0x{squireSet:X2}) to Monk with Punch Art (0x{monkSet:X2}) as the secondary: the secondary becomes 0x{kept:X2} before the game's change (0x{JobChange.ChangeAt - game.Image.ImageStart:X})",
              squireSet == 5 && monkSet == 9 && kept == squireSet);
        Check($"another secondary stays (0x{other:X2}), and none stays none ({none})", other == 0x0A && none == 0);
        return failures;
    }

    /// <summary>
    /// The job change's innate rule, through its stub on made-up menu records, against the exe's job table (vanilla's
    /// innates offline; two of them changed for the test and put back).
    /// </summary>
    public static int JobChangeInnates(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: job change innates, {name}{(ok ? "" : ": WRONG")}");
        }
        if (JobChange.InnatesAt == 0)
        {
            log.Info("self-test: job change innates, not in this build's rules: installed on this copy");
            try
            {
                JobChange.InstallInnates(game);
            }
            catch (Exception e)
            {
                Check($"can't be installed: {e.Message}", false);
                return failures;
            }
        }
        var site = (byte*)JobChange.InnatesAt;
        Check($"the rebuild's support check (0x{JobChange.InnatesAt - game.Image.ImageStart:X}) jumps to a stub",
              site[0] == 0xFF && site[1] == 0x25 && *(int*)(site + 2) == 0 && Enumerable.Range(14, 14).All(k => site[k] == 0x90));

        // A harness: r8 the record, [r15 + rbp × 8] the same, then the stub (entered 16-aligned); the pad returns r8.
        var code = GameImage.AllocateCode(96);
        var stub = GameImage.AllocateCode(JobChange.InnateStubSize);
        var pointers = (nint*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(16);
        byte[] harness =
        [
            0x41, 0x57, 0x55,             // push r15, rbp
            0x49, 0x89, 0xC8,             // mov r8, rcx
            0x49, 0x89, 0xD7,             // mov r15, rdx
            0x31, 0xED,                   // xor ebp, ebp
            0x41, 0xFF, 0xD1,             // call r9 (the stub)
            0x5D, 0x41, 0x5F, 0xC3,       // pop rbp, r15; ret
        ];
        harness.CopyTo(new Span<byte>(code, harness.Length));
        byte[] pad = [0x4C, 0x89, 0xC0, 0xC3]; // mov rax, r8; ret
        pad.CopyTo(new Span<byte>(code + 64, pad.Length));
        JobChange.WriteInnateStub(stub, (nint)(code + 64));
        var run = (delegate* unmanaged<byte*, nint*, nint, byte*, nint>)code; // the third is unused: the stub goes in r9
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        string Change(int job, int reaction, int support, int movement)
        {
            *(ushort*)(record + 0x24) = (ushort)job;
            *(ushort*)(record + 0x62) = (ushort)reaction;
            *(ushort*)(record + 0x64) = (ushort)support;
            *(ushort*)(record + 0x66) = (ushort)movement;
            pointers[0] = (nint)record;
            var r8 = run(record, pointers, 0, stub);
            return $"{*(ushort*)(record + 0x62):X3} {*(ushort*)(record + 0x64):X3} {*(ushort*)(record + 0x66):X3}{(r8 == (nint)record ? "" : " (r8 lost)")}";
        }
        var jobs = game.Units.Jobs;
        int Innate(int job, int k) => *(ushort*)(jobs + job * UnitTables.JobSize + 1 + 2 * k);
        var chemist = Change(0x4B, 0x1A6, 0x1DA, 0x1F4);
        var monk = Change(0x4E, 0x1A6, 0x1DA, 0x1F4);
        var mediator = Change(0x54, 0, 0x1D9, 0);
        // A reaction innate and the fifth id (+9 and +10, ReMixed's quirk), written into the Chemist's record for the test.
        var chemistData = jobs + 0x4B * UnitTables.JobSize;
        var saved = new Span<byte>(chemistData, 11).ToArray();
        GameImage.MakeWritable((nint)chemistData, 11);
        *(ushort*)(chemistData + 3) = 0x1B4;
        *(ushort*)(chemistData + 9) = 0x1F4;
        var widened = Change(0x4B, 0x1B4, 0x1DA, 0x1F4);
        saved.CopyTo(new Span<byte>(chemistData, 11));
        var past = Change(0x200, 0x1A6, 0x1DA, 0x1F4);
        System.Runtime.InteropServices.NativeMemory.Free(record);
        System.Runtime.InteropServices.NativeMemory.Free(pointers);
        Check($"to Chemist (innate 0x{Innate(0x4B, 0):X3}): {chemist} (Throw Item comes off, the rest stays)", chemist == "1A6 000 1F4");
        Check($"to Monk (innate 0x{Innate(0x4E, 0):X3}): {monk} (another job's innate stays)", monk == "1A6 1DA 1F4");
        Check($"to Mediator (innate 0x{Innate(0x54, 0):X3} on this copy): {mediator}", mediator == "000 000 000");
        Check($"a reaction innate and the fifth id (the job's +9, a quirk ReMixed has) come off too: {widened}", widened == "000 000 000");
        Check($"a job past the table changes nothing: {past}", past == "1A6 1DA 1F4");
        return failures;
    }

    /// <summary>The Reflect bounce: the rewritten bytes in place, and a copy run near the image with a made-up caster.</summary>
    public static int ReflectBounces(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: reflect bounce, {name}{(ok ? "" : ": WRONG")}");
        }
        if (ReflectReaction.BounceAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var at = ReflectReaction.BounceAt;
        var expected = ReflectReaction.BounceCode(at, ReflectReaction.CasterX, ReflectReaction.CasterY);
        Check($"the bounce's difference (0x{at - game.Image.ImageStart:X}) is rewritten",
              new ReadOnlySpan<byte>((void*)at, ReflectReaction.BounceSize).SequenceEqual(expected));
        // push rbx; the 31 bytes; movzx eax, dx; shl r8d, 16; or eax, r8d; pop rbx; ret
        var copy = GameImage.AllocateCodeNear(at, 64);
        copy[0] = 0x53;
        ReflectReaction.BounceCode((nint)(copy + 1), ReflectReaction.CasterX, ReflectReaction.CasterY)
            .CopyTo(new Span<byte>(copy + 1, ReflectReaction.BounceSize));
        byte[] tail = [0x0F, 0xB7, 0xC2, 0x41, 0xC1, 0xE0, 0x10, 0x44, 0x09, 0xC0, 0x5B, 0xC3];
        tail.CopyTo(new Span<byte>(copy + 1 + ReflectReaction.BounceSize, tail.Length));
        var run = (delegate* unmanaged<byte*, int>)copy;
        var x = (byte*)ReflectReaction.CasterX;
        var y = (byte*)ReflectReaction.CasterY;
        var (savedX, savedY) = (*x, *y);
        *x = 3;
        *y = 5;
        var unit = stackalloc byte[0x200];
        unit[0x4F] = 6;
        unit[0x50] = 9;
        var r = run(unit);
        (*x, *y) = (savedX, savedY);
        int dx = (short)(r & 0xFFFF), dy = (short)(r >> 16);
        Check($"a reflector at (6, 9), its caster at (3, 5): the bounce is ({dx}, {dy}), onto ({6 + dx}, {9 + dy}), the caster's panel",
              dx == -3 && dy == -4);
        return failures;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeUnitSetup(byte* unit, byte* record)
    {
        unit[GuestControl.Team] = record[0x18];
        return 7;
    }

    /// <summary>Guest control's Always, through the hooked unit setup with a stand-in for the game's.</summary>
    public static int GuestControls(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: guest control, {name}{(ok ? "" : ": WRONG")}");
        }
        if (GuestControl.SetupAt == 0)
        {
            log.Info("self-test: guest control, the default (Deployed) installs nothing: Always installed on this copy");
            try
            {
                GuestControl.Install(game);
            }
            catch (Exception e)
            {
                Check($"can't be installed: {e.Message}", false);
                return failures;
            }
        }
        var entry = (byte*)GuestControl.SetupAt;
        Check($"the unit setup (0x{GuestControl.SetupAt - game.Image.ImageStart:X}) is hooked", entry[0] == 0xE9 || entry[0] == 0xFF);
        var real = GuestControl.GameSetup;
        GuestControl.GameSetup = &FakeUnitSetup;
        var unit = stackalloc byte[0x200];
        var record = stackalloc byte[0x28];
        string Setup(byte team)
        {
            record[0x18] = team;
            var r = ((delegate* unmanaged<byte*, byte*, int>)GuestControl.SetupAt)(unit, record);
            return $"{unit[GuestControl.Team]:X2}{(r == 7 ? "" : " (result lost)")}";
        }
        var deployed = Setup(0x00); // ReMixed's default, Deployed: the hook leaves a guest alone
        var optionsBefore = Options.Current;
        Options.Use(new Options { GuestControl = Options.GuestControlRule.Always }); // a save's own option
        var guest = Setup(0x00);
        var player = Setup(0x08);
        var enemy = Setup(0x10);
        var other = Setup(0x20);
        Options.Use(optionsBefore);
        GuestControl.GameSetup = real;
        Check($"with Deployed (the default) a guest stays a guest ({deployed}); with Always (a save's own option) a guest " +
              $"of the player's team becomes the player's ({guest}); the player's own ({player}) and other teams " +
              $"({enemy}, {other}) stay",
              deployed == "00" && guest == "08" && player == "08" && enemy == "10" && other == "20");
        return failures;
    }

    /// <summary>The harness's act (test runs only): the battle menu's frame, its state and its object are found and hooked.</summary>
    public static int HarnessAct(Game game, Log log)
    {
        try
        {
            BattleAct.Install(game);
            log.Info("self-test: the harness's act, the battle menu's frame, state and object found, the frame hooked");
            return 0;
        }
        catch (Exception e)
        {
            log.Info($"self-test: the harness's act: WRONG ({e.Message})");
            return 1;
        }
    }

    /// <summary>Between a unit's two, the list builder's ids from 0x170 are hidden but Two Shots (its stub, against pads).</summary>
    public static int TwoShotsLists(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Two Shots' lists, {name}{(ok ? "" : ": WRONG")}");
        }
        if (TwoSpells.HighIdsAt == 0)
        {
            Check("the high-id filter isn't installed", false);
            return failures;
        }
        var ja = (byte*)TwoSpells.HighIdsAt;
        Check($"the list builder's high ids (0x{TwoSpells.HighIdsAt - game.Image.ImageStart:X}) go to a stub",
              ja[0] == 0x0F && ja[1] == 0x87 && (nint)(ja + 6 + *(int*)(ja + 2)) == TwoSpells.HighIdsStub);
        // As the list builder: the ability in edi, the battle index at the function's [rsp+0x28].
        var code = GameImage.AllocateCode(128);
        byte[] harness =
        [
            0x57, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x30, 0x8B, 0xF9, 0x89, 0x54, 0x24, 0x20,
            0x41, 0xFF, 0xD1, 0x48, 0x83, 0xC4, 0x30, 0x41, 0x5F, 0x5F, 0xC3,
        ];
        harness.CopyTo(new Span<byte>(code, harness.Length));
        byte[] shown = [0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3];
        byte[] hidden = [0x31, 0xC0, 0xC3];
        shown.CopyTo(new Span<byte>(code + 64, shown.Length));
        hidden.CopyTo(new Span<byte>(code + 80, hidden.Length));
        var stub = GameImage.AllocateCode(TwoSpells.HighIdStubSize);
        TwoSpells.WriteHighIdStub(stub, (nint)(code + 64), (nint)(code + 80));
        var run = (delegate* unmanaged<int, int, nint, byte*, int>)code;
        const int index = 7;
        string Listed(params int[] ids) => string.Join(" ", ids.Where(id => run(id, index, 0, stub) != 0).Select(id => $"{id:X3}"));
        var free = Listed(0x18A, 0x190, 0x195, 0x196);
        TwoSpells.SetPair(index, 1);
        var between = Listed(0x18A, 0x190, 0x195, 0x196);
        TwoSpells.SetPair(index, 0);
        Check($"a unit's Charge and Jump ids: {free}; between its two: {between} (Two Shots alone)",
              free == "18A 190 195 196" && between == "195");
        return failures;
    }

    /// <summary>The charge that locks the move: ReMixed's test on made-up ability data, and the step after the queue.</summary>
    public static int ChargeLocks(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: charge lock, {name}{(ok ? "" : ": WRONG")}");
        }
        if (!ChargeLock.Installed)
        {
            log.Info("self-test: charge lock, not in this build's rules: installed on this copy");
            try
            {
                ChargeLock.Install(game);
            }
            catch (Exception e)
            {
                Check($"can't be installed: {e.Message}", false);
                return failures;
            }
        }
        *game.RuleCode.AbilitiesReady = 1;
        const int ability = 0x10;
        var data = game.RuleCode.GetAbilityAction(ability);
        var saved = new Span<byte>(data, 20).ToArray();
        const int slot = 6;
        var unit = game.TurnCode.Units + slot * Battle.UnitSize;
        var savedUnit = new Span<byte>(unit, Battle.UnitSize).ToArray();
        new Span<byte>(unit, Battle.UnitSize).Clear();
        unit[1] = slot;
        bool Locks(int range, int area, int flags3, int flags4, int flags5, int ct = 4, int skillset = 0x0B, int id = ability)
        {
            data[0] = (byte)range;
            data[1] = (byte)area;
            data[3] = (byte)flags3;
            data[4] = (byte)flags4;
            data[5] = (byte)flags5;
            data[0xC] = (byte)ct;
            return ChargeLock.Locks(unit, skillset, id);
        }
        var aimed = Locks(4, 1, 0, 0, 0);
        var aroundSelf = Locks(0, 2, 0, 0, 0);
        var weaponRange = Locks(4, 1, 0x20, 0, 0);
        var flag4 = Locks(4, 1, 0, 0x04, 0);
        var flag5 = Locks(0, 2, 0, 0, 0x04);
        var instant = Locks(0, 2, 0x20, 0, 0, ct: 0);
        var gun = ChargeLock.Locks(unit, 0x08, 0x192);
        unit[Battle.UnitSupports + 3] = 4; // Non-charge
        var nonCharge = Locks(0, 2, 0x20, 0, 0);
        var nonChargeGun = ChargeLock.Locks(unit, 0x08, 0x192);
        unit[Battle.UnitSupports + 3] = 0;
        unit[0x1B4] = 0x80; // riding
        var rider = Locks(0, 2, 0, 0, 0, skillset: 0xB0);
        unit[6] = 0x20;
        var mount = Locks(0, 2, 0, 0, 0, skillset: 0xB0);
        unit[6] = 0;
        unit[0x1B4] = 0;
        Check($"a charge aimed at a tile doesn't lock ({aimed}); round the caster ({aroundSelf}), by weapon range ({weaponRange}) or +4 & 4 ({flag4}) does",
              !aimed && aroundSelf && weaponRange && flag4);
        Check($"+5 & 4 spares an area round the caster ({flag5}); no CT, no lock ({instant}); a gun skill locks ({gun}), even with Non-charge ({nonChargeGun})",
              !flag5 && !instant && gun && nonChargeGun);
        Check($"Non-charge spares the rest ({nonCharge}); a rider using its mount's skillset isn't locked ({rider}), a monster riding is ({mount})",
              !nonCharge && !rider && mount);

        // After the queue: a real commit sets the moved bit; no commit, a failed queue or a simulation don't.
        Locks(0, 2, 0, 0, 0);
        var record = stackalloc byte[20];
        record[0] = slot;
        record[1] = 0x0B;
        *(ushort*)(record + 2) = ability;
        var queued = unit + 0x1A0;
        string After(int commit, int result, int simulating = 0)
        {
            unit[0x1B9] = 0;
            var before = *game.Routines.Simulating;
            *game.Routines.Simulating = simulating;
            ChargeLock.AfterQueue(record, queued, commit, result);
            *game.Routines.Simulating = before;
            return (unit[0x1B9] & 1).ToString();
        }
        var committed = After(1, 0);
        var preview = After(0, 0);
        var failed = After(1, -1);
        var simulated = After(1, 0, simulating: 1);
        saved.CopyTo(new Span<byte>(data, 20));
        savedUnit.CopyTo(new Span<byte>(unit, Battle.UnitSize));
        Check($"after the queue: a commit locks ({committed}); a preview ({preview}), a failed queue ({failed}) and the AI's planning ({simulated}) don't",
              committed == "1" && preview == "0" && failed == "0" && simulated == "0");
        return failures;
    }

    private static int _fakeCost = 200;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeJpCost(int ability) => _fakeCost;

    /// <summary>
    /// Unlearning on the learn screen, through the learned branch's stub (written again against landing pads), with
    /// a made-up menu record and list entry, the Squire's skillset, and a stand-in for the JP cost (an NXD offline).
    /// </summary>
    public static int Unlearning(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: unlearning, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Unlearn.State == null)
        {
            log.Info("self-test: unlearning, not in this build's rules: installed on this copy");
            try
            {
                Unlearn.Install(game);
            }
            catch (Exception e)
            {
                Check($"can't be installed: {e.Message}", false);
                return failures;
            }
        }
        var site = (byte*)Unlearn.BranchAt;
        Check($"the learn screen's refused entry (0x{Unlearn.BranchAt - game.Image.ImageStart:X}, before its message) jumps to a stub",
              site != null && site[0] == 0xE9 && Enumerable.Range(5, 7).All(k => site[k] == 0x90));
        // A harness: ebx the entry, then the stub (entered 16-aligned); the message pad returns ecx | edx << 8 (the stub sets
        // ecx 6 and rdx the made-up message 0x1234), the refresh pad 0x10000 | edi.
        var code = GameImage.AllocateCode(128);
        byte[] harness = [0x53, 0x57, 0x89, 0xCB, 0x31, 0xFF, 0x41, 0xFF, 0xD0, 0x5F, 0x5B, 0xC3];
        harness.CopyTo(new Span<byte>(code, harness.Length));
        byte[] stay = [0x89, 0xD0, 0xC1, 0xE0, 0x08, 0x09, 0xC8, 0xC3]; // mov eax, edx; shl eax, 8; or eax, ecx; ret
        byte[] refresh = [0x89, 0xF8, 0x0D, 0x00, 0x00, 0x01, 0x00, 0xC3];
        stay.CopyTo(new Span<byte>(code + 64, stay.Length));
        refresh.CopyTo(new Span<byte>(code + 80, refresh.Length));
        var stub = GameImage.AllocateCode(Unlearn.StubSize);
        Unlearn.WriteStub(stub, 0x1234, (nint)Unlearn.Job, (nint)(code + 64), (nint)(code + 80));
        var run = (delegate* unmanaged<int, nint, byte*, int>)code;

        const int index = 3, entry = 5, job = 0x4A;
        var ability = game.Units.SkillsetAbility(game.Units.SkillsetOf(job), 2);
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var savedRecord = Dismissal.Records[index];
        var (savedScreen, savedEntry, savedJob, savedCurrent) = (*Unlearn.Screen, Unlearn.Entries[entry], *Unlearn.Job, *Dismissal.Current);
        var realCost = Unlearn.Cost;
        Unlearn.Cost = &FakeJpCost;
        Dismissal.Records[index] = (nint)record;
        *Dismissal.Current = index;
        *Unlearn.Screen = Unlearn.LearnScreen;
        *Unlearn.Job = job;
        var savedOptions = Options.Current;
        string Choose(int jp, int cost = 200, bool half = false, int support = 0, bool learned = true)
        {
            record[0x7B] = (byte)(learned ? 0x20 : 0); // the Squire's slot 2
            *(ushort*)(record + 0xCA) = (ushort)jp;
            *(ushort*)(record + 0x64) = (ushort)support;
            Unlearn.Entries[entry] = (ushort)(ability | 0x4000); // the learn screen marks a learned one only "can't be bought"
            _fakeCost = cost;
            Options.Current = new Options { JpRefund = half ? Options.JpRefundRule.Half : Options.JpRefundRule.Full };
            var r = run(entry, 0, stub);
            Options.Current = savedOptions;
            var where = r == (0x1234 << 8 | 6) ? "message" : r == (0x10000 | job) ? "refresh" : $"0x{r:X}";
            return $"{where}, bit {(record[0x7B] & 0x20) != 0}, JP {*(ushort*)(record + 0xCA)}";
        }
        var full = Choose(1000);
        var half = Choose(1000, half: true);
        var capped = Choose(9900);
        var priceless = Choose(500, cost: 9999);
        var equipped = Choose(1000, support: ability);
        var notLearned = Choose(1000, learned: false);
        Unlearn.Cost = realCost;
        Dismissal.Records[index] = savedRecord;
        (*Unlearn.Screen, Unlearn.Entries[entry], *Unlearn.Job, *Dismissal.Current) = (savedScreen, savedEntry, savedJob, savedCurrent);
        System.Runtime.InteropServices.NativeMemory.Free(record);
        Check($"a learned Squire ability (0x{ability:X3}) at 200 JP: {full} (unlearned, all back)", full == "refresh, bit False, JP 1200");
        Check($"with half back: {half}", half == "refresh, bit False, JP 1100");
        Check($"JP stops at 9999 ({capped}), and a 9999 cost gives none back ({priceless})",
              capped == "refresh, bit False, JP 9999" && priceless == "refresh, bit False, JP 500");
        Check($"an equipped one stays learned, with the game's message: {equipped}", equipped == "message, bit True, JP 1000");
        Check($"an entry it can't afford gets the game's message as before: {notLearned}", notLearned == "message, bit False, JP 1000");
        return failures;
    }

    private static int _dismissed, _stored, _rolledChance, _rollResult;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeDismiss() => _dismissed++;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakePoachItems(int job, int* common, int* rare)
    {
        *common = job;
        *rare = job + 0x100;
        return 1;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeStore(int item) => _stored = item;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakePoachRoll(int range, int chance)
    {
        _rolledChance = chance;
        return _rollResult;
    }

    /// <summary>
    /// The monster dismissal's poach, through the hooked dismissal, with stand-ins for the game's roll, its poach table
    /// (not loaded offline), its stock and its own dismissal, on a made-up menu record and roster.
    /// </summary>
    public static int DismissalPoach(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: dismissal poach, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Dismissal.DismissAt == 0)
        {
            log.Info("self-test: dismissal poach, not in this build's rules: installed on this copy");
            try
            {
                Dismissal.Install(game);
            }
            catch (Exception e)
            {
                Check($"can't be installed: {e.Message}", false);
                return failures;
            }
        }
        var entry = (byte*)Dismissal.DismissAt;
        Check($"the dismissal (0x{Dismissal.DismissAt - game.Image.ImageStart:X}) is hooked", entry[0] == 0xE9 || entry[0] == 0xFF);
        int noTable, common, rare;
        noTable = ((delegate* unmanaged<int, int*, int*, int>)Dismissal.PoachItems)(0x5E, &common, &rare);
        Check($"the game's poach table isn't loaded offline, and the lookup says so ({noTable})", noTable == 0);

        var realDismiss = Dismissal.GameDismiss;
        var realItems = Dismissal.PoachItems;
        var realStore = Dismissal.Store;
        var realRoll = Dismissal.Roll;
        Dismissal.GameDismiss = &FakeDismiss;
        Dismissal.PoachItems = &FakePoachItems;
        Dismissal.Store = &FakeStore;
        Dismissal.Roll = &FakePoachRoll;
        var roster = game.Units.Roster;
        var rosterBytes = UnitTables.RosterSlots * UnitTables.RosterSize;
        var savedRoster = new Span<byte>(roster, rosterBytes).ToArray();
        new Span<byte>(roster, rosterBytes).Fill(0);
        for (var k = 0; k < UnitTables.RosterSlots; k++)
            roster[k * UnitTables.RosterSize + 1] = 0xFF;
        const int index = 3;
        var savedRecord = Dismissal.Records[index];
        var savedCurrent = *Dismissal.Current;
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        Dismissal.Records[index] = (nint)record;
        *Dismissal.Current = index;
        var savedOptions = Options.Current;
        var poacher = roster + 2 * UnitTables.RosterSize;
        string Dismiss(int job, int kind, int roll, bool vanilla = false)
        {
            *(ushort*)(record + 0x24) = (ushort)job;
            record[0x72] = (byte)kind;
            *(ushort*)(record + 0x2C) = 5;
            _rollResult = roll;
            _stored = -1;
            _rolledChance = -1;
            Options.Current = new Options { PoachingOdds = vanilla ? Options.PoachRule.Vanilla : Options.PoachRule.ByBrave };
            var before = _dismissed;
            ((delegate* unmanaged<void>)Dismissal.DismissAt)(); // through the hook
            Options.Current = savedOptions;
            return $"{(_stored < 0 ? "none" : $"0x{_stored:X3}")} at {_rolledChance}{(_dismissed == before + 1 ? "" : " (not dismissed)")}";
        }
        var none = Dismiss(0x5E, 0x80, 0);
        poacher[1] = 2;
        *(ushort*)(poacher + 0x0A) = Dismissal.SecretHunt;
        poacher[0x1E] = 70;
        var hit = Dismiss(0x5E, 0x80, 0);
        var miss = Dismiss(0x8D, 0x80, 1);
        var vanillaOdds = Dismiss(0x60, 0x80, 0, vanilla: true);
        var human = Dismiss(0x4A, 0x80, 0);
        var ramza = Dismiss(0x5E, 1, 0);
        // The poacher away on an errand (the roster's errand byte: 1 on an errand, 0x08 gone in ReMixed's events).
        poacher[Dismissal.RosterErrand] = 0x01;
        var errand = Dismiss(0x5E, 0x80, 0);
        poacher[Dismissal.RosterErrand] = 0x40;
        var locked = Dismiss(0x5E, 0x80, 0);
        poacher[Dismissal.RosterErrand] = 0;
        // A poacher by innate: job 0x4A's first innate made Secret Hunt for the test, then put back.
        *(ushort*)(poacher + 0x0A) = 0;
        poacher[2] = 0x4A;
        var squire = game.Units.Jobs + 0x4A * UnitTables.JobSize;
        var savedInnate = *(ushort*)(squire + 1);
        GameImage.MakeWritable((nint)squire, 3);
        var plain = Dismiss(0x5E, 0x80, 0);
        *(ushort*)(squire + 1) = Dismissal.SecretHunt;
        var innate = Dismiss(0x5E, 0x80, 0);
        *(ushort*)(squire + 1) = savedInnate;

        Check($"no one with Secret Hunt: {none}", none == "none at -1");
        Check($"a poacher with Brave 70: {hit} on a hit (the rare item, 91/256), {miss} on a miss (the common one)",
              hit == "0x15E at 91" && miss == "0x08D at 91");
        Check($"with vanilla's odds: {vanillaOdds} (31/256)", vanillaOdds == "0x160 at 31");
        Check($"a human isn't poached ({human}), nor a unit the game won't dismiss ({ramza})", human == "none at -1" && ramza == "none at -1");
        Check($"Secret Hunt as an innate counts: {innate} (without it: {plain})", innate == "0x15E at 91" && plain == "none at -1");
        Check($"a poacher away on an errand doesn't count ({errand}); one whose errand byte has only 0x40 does ({locked})",
              errand == "none at -1" && locked == "0x15E at 91");

        Dismissal.Records[index] = savedRecord;
        *Dismissal.Current = savedCurrent;
        savedRoster.CopyTo(new Span<byte>(roster, rosterBytes));
        System.Runtime.InteropServices.NativeMemory.Free(record);
        Dismissal.GameDismiss = realDismiss;
        Dismissal.PoachItems = realItems;
        Dismissal.Store = realStore;
        Dismissal.Roll = realRoll;

        return failures;
    }

    /// <summary>The katana break roll through its hook, with the chance at 0 so the game's roll always breaks.</summary>
    public static int KatanaBreaks(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: katana break, {name}{(ok ? "" : ": WRONG")}");
        }
        if (KatanaBreak.RollAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var b = game.Battle;
        var r = game.Routines;
        var attacker = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var own = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var attackerBefore = *b.Attacker;
        var ownBefore = *r.AttackerResult;
        var simulating = *r.Simulating;
        var chance = b.Weapon[4];
        *b.Attacker = attacker;
        *r.AttackerResult = own;
        *r.Simulating = 0;
        b.Weapon[4] = 0; // the break chance: the game's roll then always breaks
        var roll = (delegate* unmanaged<void>)KatanaBreak.RollAt;
        var remembered = new List<bool>();
        string Draw(int job, int skillset, int maintenance = 0, int adeptness = 0)
        {
            attacker[3] = (byte)job;
            attacker[0x1A1] = (byte)skillset;
            attacker[Battle.UnitSupports + 2] = (byte)maintenance;
            attacker[Battle.UnitSupports + 3] = (byte)adeptness;
            *(ushort*)(own + 0x12) = 0xBEEF;
            *KatanaBreak.Broke = 0;
            roll();
            remembered.Add(*KatanaBreak.Broke == attacker - game.TurnCode.Units + 1);
            var mark = *(ushort*)(own + 0x12);
            return mark switch { 8 => "break", 0x1000 => "none", 0xBEEF => "no roll", _ => $"0x{mark:X}" };
        }
        var plain = Draw(0x50, 0x13);
        var samurai = Draw(0x58, 0x13);
        var maintained = Draw(0x50, 0x13, maintenance: 0x04);
        var adept = Draw(0x50, 0x13, adeptness: 0x02);
        var otherSkillset = Draw(0x50, 0x0B);
        var mime = Draw(0x5D, 0x13);
        Check($"Draw Out by a Black Mage breaks ({plain}); a Samurai's doesn't ({samurai}), nor with Maintenance ({maintained}) or Adeptness ({adept}), nor another skillset's ({otherSkillset}); a Mime's has no roll ({mime})",
              plain == "break" && samurai == "none" && maintained == "none" && adept == "none" && otherSkillset == "none" && mime == "no roll");
        Check($"only the break is remembered for the setup ({string.Join(" ", remembered)})",
              remembered.SequenceEqual([true, false, false, false, false, false]));
        *KatanaBreak.Broke = 0;
        b.Weapon[4] = chance;
        *r.Simulating = simulating;
        *b.Attacker = attackerBefore;
        *r.AttackerResult = ownBefore;
        System.Runtime.InteropServices.NativeMemory.Free(attacker);
        System.Runtime.InteropServices.NativeMemory.Free(own);

        // The action setup's decision, through its stub written again against landing pads: rsi the unit's offset
        // (index × 0x200), r8b the count of targets.
        var site = (byte*)KatanaBreak.DecisionAt;
        Check($"the action setup's decision (0x{KatanaBreak.DecisionAt - game.Image.ImageStart:X}) jumps to its stub",
              site[0] == 0xE9 && GameImage.Rip(KatanaBreak.DecisionAt, 1, 5) == KatanaBreak.DecisionStub
              && Enumerable.Range(5, 23).All(k => site[k] == 0x90));
        var code = GameImage.AllocateCode(96);
        byte[] harness = [0x56, 0x4C, 0x89, 0xC6, 0x49, 0x89, 0xCA, 0x41, 0x89, 0xD0, 0x41, 0xFF, 0xE2];
        harness.CopyTo(new Span<byte>(code, harness.Length));
        for (var k = 1; k <= 3; k++)
        {
            byte[] pad = [0x5E, 0xB8, (byte)k, 0, 0, 0, 0xC3]; // pop rsi; mov eax, k; ret
            pad.CopyTo(new Span<byte>(code + 16 * k, pad.Length));
        }
        var flag = (long*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
        var stub = GameImage.AllocateCode(KatanaBreak.DecisionStubSize);
        KatanaBreak.WriteDecisionStub(stub, (nint)flag, (nint)(code + 16), (nint)(code + 32), (nint)(code + 48));
        var decide = (delegate* unmanaged<byte*, int, long, int>)code;
        string Decide(int count, long unit, long broke)
        {
            *flag = broke;
            var where = decide(stub, count, unit) switch { 1 => "break", 2 => "none", 3 => "as with no target", var r => $"{r}" };
            return *flag == 0 ? where : $"{where} (not cleared)";
        }
        var broken = Decide(2, 3 * 0x200, 3 * 0x200 + 1);
        var notBroken = Decide(2, 3 * 0x200, 0);
        var otherUnit = Decide(2, 3 * 0x200, 5 * 0x200 + 1);
        var noTarget = Decide(0, 3 * 0x200, 3 * 0x200 + 1);
        System.Runtime.InteropServices.NativeMemory.Free(flag);
        Check($"the setup breaks it when a target's roll did ({broken}), not when none did ({notBroken}) or another unit's did ({otherUnit}), and never without a target ({noTarget})",
              broken == "break" && notBroken == "none" && otherUnit == "none" && noTarget == "as with no target");
        return failures;
    }

    /// <summary>
    /// Formula 0x2E's two changes in place, and its Maintenance step's stub, written again against landing pads: the
    /// step pad marks r11, the pad after the step returns r11 + 1, the damage path's pad r11 + 2.
    /// </summary>
    public static int BreakFormula(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Breaks, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Breaks.Stub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var start = game.Image.ImageStart;
        Check($"nothing to break skips the halving (0x{Breaks.HalvingAt - start:X} is jmp)", ((byte*)Breaks.HalvingAt)[0] == 0xEB);
        Check($"the Maintenance step's call (0x{Breaks.StepCallAt - start:X}) jumps to the stub",
              ((byte*)Breaks.StepCallAt)[0] == 0xE9 && GameImage.Rip(Breaks.StepCallAt, 1, 5) == Breaks.Stub);
        var code = GameImage.AllocateCode(64);
        byte[] harness = [0x45, 0x31, 0xDB, 0xFF, 0xE1];                // xor r11d, r11d; jmp rcx
        byte[] step = [0x41, 0xBB, 0x00, 0x01, 0x00, 0x00, 0xC3];       // mov r11d, 0x100; ret
        byte[] back = [0x41, 0x8D, 0x43, 0x01, 0xC3];                   // lea eax, [r11 + 1]; ret
        byte[] damage = [0x41, 0x8D, 0x43, 0x02, 0xC3];                 // lea eax, [r11 + 2]; ret
        harness.CopyTo(new Span<byte>(code, harness.Length));
        step.CopyTo(new Span<byte>(code + 16, step.Length));
        back.CopyTo(new Span<byte>(code + 32, back.Length));
        damage.CopyTo(new Span<byte>(code + 48, damage.Length));
        var stub = GameImage.AllocateCode(Breaks.StubSize);
        Breaks.WriteStub(stub, (nint)game.Routines.Target, (nint)game.Battle.TargetResult, (nint)(code + 16), (nint)(code + 32), (nint)(code + 48));
        var run = (delegate* unmanaged<byte*, int>)code;

        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var savedTarget = *game.Routines.Target;
        var savedResult = *game.Battle.TargetResult;
        *game.Routines.Target = unit;
        *game.Battle.TargetResult = result;
        string Strike(int item, bool maintenance = false)
        {
            unit[Battle.UnitSupports + 2] = (byte)(maintenance ? 0x04 : 0);
            result[0x1B] = 0x20;
            *(ushort*)(result + 4) = (ushort)item;
            var where = run(stub) switch { 0x101 => "the game's step, then the break", 2 => "damage only", var r => $"0x{r:X}" };
            return $"{where} (slot 0x{result[0x1B]:X2}, item 0x{*(ushort*)(result + 4):X3})";
        }
        var plain = Strike(0x8E);
        var maintained = Strike(0x8E, maintenance: true);
        var spared = Strike(0x102);
        var after = Strike(0x105);
        *game.Routines.Target = savedTarget;
        *game.Battle.TargetResult = savedResult;
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        System.Runtime.InteropServices.NativeMemory.Free(result);
        Check($"a plain target: {plain}", plain == "the game's step, then the break (slot 0x20, item 0x08E)");
        Check($"Maintenance: {maintained}", maintained == "damage only (slot 0x00, item 0x000)");
        Check($"an item the step spares (0x102): {spared}; one past them (0x105): {after}",
              spared == "damage only (slot 0x00, item 0x000)" && after == "the game's step, then the break (slot 0x20, item 0x105)");
        return failures;
    }

    /// <summary>
    /// Pass in the movement setup's team test: the stub, written again against a landing pad, on a made-up tile record
    /// (r11) with the flags base in r8d and the team bits' difference in edx, as the game has them there.
    /// </summary>
    public static int PassRule(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Pass, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Pass.Stub == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var site = (byte*)Pass.SiteAt;
        Check($"the team test (0x{Pass.SiteAt - game.Image.ImageStart:X}) jumps to the stub",
              site[0] == 0xE9 && GameImage.Rip(Pass.SiteAt, 1, 5) == Pass.Stub && site[5] == 0x90 && site[7] == 0x90);
        // mov r11, r9; mov r10, rcx; mov ecx, r8d; or cl, 0x40; mov al, 0x77; and dl, 0x30; jmp r10. The pad: mov eax, ecx; ret.
        byte[] harness = [0x4D, 0x8B, 0xD9, 0x49, 0x89, 0xCA, 0x44, 0x89, 0xC1, 0x80, 0xC9, 0x40, 0xB0, 0x77, 0x80, 0xE2, 0x30, 0x41, 0xFF, 0xE2];
        byte[] pad = [0x89, 0xC8, 0xC3];
        var code = GameImage.AllocateCode(64);
        harness.CopyTo(new Span<byte>(code, harness.Length));
        pad.CopyTo(new Span<byte>(code + 48, pad.Length));
        var mover = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(1);
        var stub = GameImage.AllocateCode(Pass.StubSize);
        Pass.WriteStub(stub, (nint)mover, (nint)(code + 48));
        var run = (delegate* unmanaged<byte*, int, int, byte*, int>)code;
        var tile = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
        string Tile(int teams, int movement, int baseFlags = 0)
        {
            *mover = (byte)movement;
            tile[0] = 0;
            var flags = run(stub, teams, baseFlags, tile + 2) & 0xFF;
            return $"0x{flags:X2}{(tile[0] == 0x77 ? "" : " (x not stored)")}";
        }
        var enemy = Tile(0x10, 0);
        var passEnemy = Tile(0x10, Pass.PassBit);
        var ally = Tile(0x00, 0);
        var passAlly = Tile(0x00, Pass.PassBit);
        var stopEnemy = Tile(0x20, Pass.PassBit | 0x80, baseFlags: 0x20);
        var otherBits = Tile(0x10, 0xFD);
        System.Runtime.InteropServices.NativeMemory.Free(mover);
        System.Runtime.InteropServices.NativeMemory.Free(tile);
        Check($"an enemy's tile can't be passed ({enemy}); with Pass it can ({passEnemy}); an ally's either way ({ally}, {passAlly})",
              enemy == "0x40" && passEnemy == "0x00" && ally == "0x00" && passAlly == "0x00");
        Check($"Pass keeps \"can't stop here\" ({stopEnemy}); other movement bits don't pass ({otherBits})",
              stopEnemy == "0x20" && otherBits == "0x40");
        return failures;
    }

    /// <summary>
    /// The movement mode leaf, rewritten in place, against ReMixed's logic in C# over every combination of the bits it
    /// reads; and a copy of the game's own leaf against vanilla's in C#, which checks the reading of the original.
    /// </summary>
    public static int MovementModes(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: movement mode, {name}{(ok ? "" : ": WRONG")}");
        }
        if (MovementMode.LeafAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var original = MovementMode.Original();
        var copy = GameImage.AllocateCode(original.Length);
        original.CopyTo(new Span<byte>(copy, original.Length));
        var remixed = (delegate* unmanaged<byte*, byte>)MovementMode.LeafAt;
        var vanilla = (delegate* unmanaged<byte*, byte>)copy;
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        int wrong = 0, wrongVanilla = 0, cases = 0;
        string first = "";
        foreach (var statuses in new byte[] { 0, 0x02, 0x04, 0x06, 0x40, 0x42, 0x44, 0x46, 0xB9 })
            foreach (var movement1 in new byte[] { 0, 0x04, 0x08, 0x0C, 0xF3, 0xFF })
                for (var movement2 = 0; movement2 < 256; movement2++)
                    foreach (var linked in new[] { false, true })
                    {
                        unit[0x63] = statuses;
                        unit[0x9D] = movement1;
                        unit[0x9E] = (byte)movement2;
                        unit[0x1B4] = (byte)(linked ? 0x83 : 0x03);
                        cases++;
                        int got = remixed(unit), want = MovementMode.Mode(statuses, movement1, (byte)movement2, linked);
                        if (got != want && wrong++ == 0)
                            first = $"statuses 0x{statuses:X2}, 0x{movement1:X2} 0x{movement2:X2}, linked {linked}: 0x{got:X2}, not 0x{want:X2}";
                        if (vanilla(unit) != MovementMode.VanillaMode(statuses, movement1, (byte)movement2))
                            wrongVanilla++;
                    }
        string Of(byte statuses, byte movement1, byte movement2, bool linked = false)
        {
            unit[0x63] = statuses;
            unit[0x9D] = movement1;
            unit[0x9E] = movement2;
            unit[0x1B4] = (byte)(linked ? 0x80 : 0);
            return $"0x{remixed(unit):X2} (vanilla 0x{vanilla(unit):X2})";
        }
        var chocobo = Of(0, 0, 0x10);
        var rider = Of(0, 0, 0, linked: true);
        var chicken = Of(0x04, 0x08, 0);
        var pass = Of(0, 0, 0x02);
        var floating = Of(0x40, 0, 0x14);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        Check($"the leaf (0x{MovementMode.LeafAt - game.Image.ImageStart:X}) matches ReMixed's logic in all {cases} cases{(wrong == 0 ? "" : $" ({wrong} differ; first {first})")}",
              wrong == 0);
        Check($"a copy of the game's own leaf matches vanilla's logic ({wrongVanilla} differ)", wrongVanilla == 0);
        Check($"Waterbreathing: {chocobo}; a rider: {rider}; a Chicken with Teleport: {chicken}; Pass: {pass}; Float with Fly and Waterbreathing: {floating}",
              chocobo == "0x10 (vanilla 0x00)" && rider == "0x10 (vanilla 0x00)" && chicken == "0x00 (vanilla 0x08)"
              && pass == "0x00 (vanilla 0x02)" && floating == "0xC0 (vanilla 0xC0)");
        return failures;
    }

    /// <summary>
    /// The game's own area builder, run with both stubs on a flat 10 × 10 map: a caster at (5, 5) and an ability whose
    /// data the test writes (the live table is marked built, as in the team targeting test). Both bits give ReMixed's
    /// cone; Linear or 3 Directions alone the game's lines, through the stub's other path.
    /// </summary>
    public static int ConeAreas(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: cone areas, {name}{(ok ? "" : ": WRONG")}");
        }
        if (ConeArea.LinesStub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var start = game.Image.ImageStart;
        Check($"the diamond's area (0x{ConeArea.DoubleAt - start:X}) and the lines (0x{ConeArea.LinesAt - start:X}) jump to their stubs",
              ((byte*)ConeArea.DoubleAt)[0] == 0xE9 && GameImage.Rip(ConeArea.DoubleAt, 1, 5) == ConeArea.DoubleStub
              && ((byte*)ConeArea.LinesAt)[0] == 0xE9 && GameImage.Rip(ConeArea.LinesAt, 1, 5) == ConeArea.LinesStub);
        const int ability = 0x152, width = 10;
        var code = game.RuleCode;
        *code.AbilitiesReady = 1;
        var data = code.GetAbilityAction(ability);
        var savedData = new Span<byte>(data, 20).ToArray();
        var unit = game.TurnCode.Units;
        var savedUnit = new Span<byte>(unit, Battle.UnitSize).ToArray();
        var savedTerrain = new Span<byte>(ConeArea.Terrain, ConeArea.Tiles * 8).ToArray();
        var savedMap = new Span<byte>(ConeArea.Map, ConeArea.Tiles * ConeArea.TileSize).ToArray();
        var (savedWidth, savedDepth, savedSorted) = (ConeArea.Width[0], ConeArea.Width[1], *ConeArea.Sorted);
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x20);
        var build = (delegate* unmanaged<byte*, int>)ConeArea.Builder;
        try
        {
            new Span<byte>(ConeArea.Terrain, ConeArea.Tiles * 8).Clear();
            ConeArea.Width[0] = width;
            ConeArea.Width[1] = width;
            unit[0x4F] = 5;
            unit[0x50] = 5;
            unit[0x51] = 0;
            static string Signed(int v) => v < 0 ? $"{v}" : $"+{v}";
            string Area(int area, int bits, int tx, int ty)
            {
                new Span<byte>(data, 20).Clear();
                data[1] = (byte)area;
                data[2] = 3;                     // vertical tolerance
                data[4] = (byte)(0x11 | bits);   // not the caster's tile; 0x10 as ReMixed's data has it
                record[0] = 0;
                *(ushort*)(record + 2) = ability;
                record[0x0C] = (byte)tx;
                record[0x10] = (byte)ty;
                *ConeArea.Sorted = 0;
                var count = build(record);
                var tiles = new List<string>();
                for (var i = 0; i < width * width; i++)
                    if (ConeArea.Map[i * ConeArea.TileSize] != 0)
                        tiles.Add($"{Signed(i % width - 5)},{Signed(i / width - 5)}");
                return $"{count}: {string.Join(" ", tiles)}{(*ConeArea.Sorted == 1 ? "" : " (not sorted)")}";
            }
            var breath = Area(2, ConeArea.Both, 7, 5);
            var up = Area(2, ConeArea.Both, 5, 2);
            var small = Area(1, ConeArea.Both, 4, 5);
            var wave = Area(3, ConeArea.Both, 8, 6);
            var line = Area(2, ConeArea.Linear, 7, 5);
            var three = Area(2, ConeArea.ThreeDirections, 7, 5);
            Check($"area 2 toward +x: {breath}", breath == "4: +2,-1 +1,+0 +2,+0 +2,+1");
            Check($"area 2 toward -y: {up}", up == "4: -1,-2 +0,-2 +1,-2 +0,-1");
            Check($"area 1 toward -x: {small}", small == "3: -1,-1 -1,+0 -1,+1");
            Check($"area 3 toward +x (target off the axis): {wave}",
                  wave == "9: +3,-2 +2,-1 +3,-1 +1,+0 +2,+0 +3,+0 +2,+1 +3,+1 +3,+2");
            Check($"Linear alone stays the game's line: {line}", line == "2: +1,+0 +2,+0");
            Check($"3 Directions alone stays the game's three lines: {three}", three == "6: +0,-2 +0,-1 +1,+0 +2,+0 +0,+1 +0,+2");
        }
        finally
        {
            savedData.CopyTo(new Span<byte>(data, 20));
            savedUnit.CopyTo(new Span<byte>(unit, Battle.UnitSize));
            savedTerrain.CopyTo(new Span<byte>(ConeArea.Terrain, savedTerrain.Length));
            savedMap.CopyTo(new Span<byte>(ConeArea.Map, savedMap.Length));
            (ConeArea.Width[0], ConeArea.Width[1], *ConeArea.Sorted) = (savedWidth, savedDepth, savedSorted);
            System.Runtime.InteropServices.NativeMemory.Free(record);
        }
        return failures;
    }

    /// <summary>
    /// ReMixed's targeting for flag-0x10 ranges, through the game's own range builder on a flat 10 × 10 map (a caster at
    /// (5, 5), the ability's data written), and the ring order's key through a copy of its stub.
    /// </summary>
    public static int ConeTargeting(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: cone targeting, {name}{(ok ? "" : ": WRONG")}");
        }
        if (ConeArea.RingStub == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var start = game.Image.ImageStart;
        Check($"the range builder's diamond (0x{ConeArea.RangeDoubleAt - start:X}), its cross (0x{ConeArea.RangeCarveAt - start:X}) and the unit list's key (0x{ConeArea.RingAt - start:X}) jump to stubs",
              ((byte*)ConeArea.RangeDoubleAt)[0] == 0xE9 && ((byte*)ConeArea.RangeCarveAt)[0] == 0xE9
              && ((byte*)ConeArea.RingAt)[0] == 0xE9 && GameImage.Rip(ConeArea.RingAt, 1, 5) == ConeArea.RingStub);
        const int ability = 0x152, width = 10;
        var code = game.RuleCode;
        *code.AbilitiesReady = 1;
        var data = code.GetAbilityAction(ability);
        var savedData = new Span<byte>(data, 20).ToArray();
        var unit = game.TurnCode.Units;
        var savedUnit = new Span<byte>(unit, Battle.UnitSize).ToArray();
        var savedTerrain = new Span<byte>(ConeArea.Terrain, ConeArea.Tiles * 8).ToArray();
        var savedMap = new Span<byte>(ConeArea.Map, ConeArea.Tiles * ConeArea.TileSize).ToArray();
        var (savedWidth, savedDepth) = (ConeArea.Width[0], ConeArea.Width[1]);
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x20);
        var build = (delegate* unmanaged<byte*, int>)ConeArea.RangeBuilder;
        try
        {
            new Span<byte>(ConeArea.Terrain, ConeArea.Tiles * 8).Clear();
            ConeArea.Width[0] = width;
            ConeArea.Width[1] = width;
            unit[0x4F] = 5;
            unit[0x50] = 5;
            unit[0x51] = 0;
            static string Signed(int v) => v < 0 ? $"{v}" : $"+{v}";
            // The count is the builder's own, over both layers: this flat map fills the upper one too, and the game's cross
            // marks byte 1 in both layers as well.
            (string Selectable, int Reached, int Upper, int Count) Range(int range, int flags, int bits)
            {
                new Span<byte>(data, 20).Clear();
                data[0] = (byte)range;
                data[2] = 3;            // vertical tolerance
                data[3] = (byte)flags;  // 0x10: the cross ReMixed's carve replaces
                data[4] = (byte)bits;
                record[0] = 0;
                *(ushort*)(record + 2) = ability;
                var count = build(record);
                var tiles = new List<string>();
                var reached = 0;
                for (var i = 0; i < width * width; i++)
                {
                    var tile = ConeArea.Map + i * ConeArea.TileSize;
                    if (tile[1] != 0)
                        tiles.Add($"{Signed(i % width - 5)},{Signed(i / width - 5)}");
                    if (tile[0] != 0)
                        reached++;
                }
                var upper = 0;
                for (var i = 256; i < ConeArea.Tiles; i++)
                    if (ConeArea.Map[i * ConeArea.TileSize + 1] != 0)
                        upper++;
                return ($"{tiles.Count}: {string.Join(" ", tiles)}", reached, upper, count);
            }
            var both = Range(2, ConeArea.CrossRange, ConeArea.Both);
            var linear = Range(2, ConeArea.CrossRange, ConeArea.Linear);
            var plain = Range(2, 0, ConeArea.Both);
            Check($"both bits, range 2: {both.Selectable} (diamond {both.Reached} tiles; the builder counts {both.Count} with {both.Upper} in the upper layer)",
                  both.Selectable == "16: -1,-2 +0,-2 +1,-2 -2,-1 +0,-1 +2,-1 -2,+0 -1,+0 +1,+0 +2,+0 -2,+1 +0,+1 +2,+1 -1,+2 +0,+2 +1,+2"
                  && both.Reached == 41 && both.Count == 16 + both.Upper);
            Check($"Linear alone, range 2: the cross without the caster's tile: {linear.Selectable} (builder {linear.Count})",
                  linear.Selectable == "8: +0,-2 +0,-1 -2,+0 -1,+0 +1,+0 +2,+0 +0,+1 +0,+2" && linear.Count == 8 + linear.Upper);
            Check($"without flag 0x10 the diamond stays the range's: {plain.Reached} tiles", plain.Reached == 13);
        }
        finally
        {
            savedData.CopyTo(new Span<byte>(data, 20));
            savedUnit.CopyTo(new Span<byte>(unit, Battle.UnitSize));
            savedTerrain.CopyTo(new Span<byte>(ConeArea.Terrain, savedTerrain.Length));
            savedMap.CopyTo(new Span<byte>(ConeArea.Map, savedMap.Length));
            (ConeArea.Width[0], ConeArea.Width[1]) = (savedWidth, savedDepth);
            System.Runtime.InteropServices.NativeMemory.Free(record);
        }

        // The ring key through a copy of the stub: push rsi; esi = |dy|; edx = |dx|; the stub; eax = esi; pop rsi; ret.
        var harness = GameImage.AllocateCodeNear(ConeArea.RingStub, 32 + ConeArea.RingStubSize);
        byte[] entry = [0x56, 0x8B, 0xF1, 0xFF, 0x25, 0, 0, 0, 0];
        var copy = harness + 32;
        new ReadOnlySpan<byte>(entry).CopyTo(new Span<byte>(harness, entry.Length));
        *(long*)(harness + entry.Length) = (long)copy;
        byte[] tail = [0x8B, 0xC6, 0x5E, 0xC3];
        var back = harness + entry.Length + 8;
        new ReadOnlySpan<byte>(tail).CopyTo(new Span<byte>(back, tail.Length));
        ConeArea.WriteRingStub(copy, (nint)back);
        var key = (delegate* unmanaged<int, int, int>)harness;
        var keys = $"(2, 0) {key(0, 2)}, (2, 1) {key(1, 2)}, (1, 2) {key(2, 1)}, (3, 0) {key(0, 3)}, (1, 1) {key(1, 1)}";
        Check($"the target order's key, 16 × the larger + the smaller: {keys}",
              keys == "(2, 0) 32, (2, 1) 33, (1, 2) 33, (3, 0) 48, (1, 1) 17");
        return failures;
    }

    /// <summary>
    /// ReMixed's equipment start statuses: the remaster's loop jumps to the stub, and a copy of the stub run on a unit's
    /// 15 status bytes (innate, immune, current) and an item's.
    /// </summary>
    public static int StartStatusRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: start statuses, {name}{(ok ? "" : ": WRONG")}");
        }
        if (StartStatuses.Stub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        Check($"the status loop (0x{StartStatuses.At - game.Image.ImageStart:X}) jumps to the stub",
              ((byte*)StartStatuses.At)[0] == 0xE9 && GameImage.Rip(StartStatuses.At, 1, 5) == StartStatuses.Stub);
        // Harness: r9 = item - unit, r8 = 15, then a copy of the stub, which comes back to a ret.
        var harness = GameImage.AllocateCodeNear(StartStatuses.Stub, 32 + StartStatuses.StubSize);
        byte[] entry = [0x4C, 0x8B, 0xCA, 0x4C, 0x2B, 0xC9, 0x41, 0xB8, 0x0F, 0, 0, 0, 0xFF, 0x25, 0, 0, 0, 0];
        new ReadOnlySpan<byte>(entry).CopyTo(new Span<byte>(harness, entry.Length));
        var copy = harness + 32;
        *(long*)(harness + entry.Length) = (long)copy;
        var back = harness + entry.Length + 8;
        *back = 0xC3;
        StartStatuses.WriteStub(copy, (nint)back);
        var run = (delegate* unmanaged<byte*, byte*, void>)harness;
        var unit = stackalloc byte[15];
        var item = stackalloc byte[15];
        new Span<byte>(unit, 15).Clear();
        new Span<byte>(item, 15).Clear();
        unit[0] = 0x01;        // innate
        unit[5] = 0x40;        // immune from an earlier item
        unit[10] = 0x80;       // current
        unit[11] = 0x08;       // current, which this item's immunity takes away
        item[6] = 0x08;        // the item's immunity
        item[10] = 0x42;       // its start statuses: 0x40 the unit is immune to, 0x02 not
        item[11] = 0x18;
        run(unit, item);
        var bytes = Convert.ToHexString(new ReadOnlySpan<byte>(unit, 15));
        Check($"(current | start) & ~immune | innate, after the item's own immunity: {bytes}",
              bytes == "010000000040080000008310000000");
        return failures;
    }

    /// <summary>
    /// ReMixed's story AI: both sites jump to their stubs, the stubs are what a fresh write gives, and copies of them
    /// (run through <see cref="Harness"/>) send made-up units the right way: a Defensive unit with byte 38's 0x04 gets mode
    /// 0x11 unless it has Stay Near XY too, and the run-away step is skipped only for one with all three.
    /// </summary>
    public static int StoryAiRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: story AI, {name}{(ok ? "" : ": WRONG")}");
        }
        if (StoryAi.ModeStub == 0 || StoryAi.StepStub == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var image = game.Image.ImageStart;
        bool JumpsTo(nint site, nint stub) =>
            ((byte*)site)[0] == 0xE9 && site + 5 + *(int*)(site + 1) == stub && ((byte*)site)[5] == 0x90;
        var fresh = stackalloc byte[StoryAi.StubSize];
        new Span<byte>(fresh, StoryAi.StubSize).Clear();
        StoryAi.WriteModeStub(fresh, StoryAi.ModeTargets.Mode, StoryAi.ModeTargets.Focus);
        var modeSame = new ReadOnlySpan<byte>(fresh, 39).SequenceEqual(new ReadOnlySpan<byte>((void*)StoryAi.ModeStub, 39));
        new Span<byte>(fresh, StoryAi.StubSize).Clear();
        StoryAi.WriteStepStub(fresh, StoryAi.StepTargets.Step, StoryAi.StepTargets.NotCritical, StoryAi.StepTargets.Turn);
        var stepSame = new ReadOnlySpan<byte>(fresh, 75).SequenceEqual(new ReadOnlySpan<byte>((void*)StoryAi.StepStub, 75));
        Check($"the AI setup (0x{StoryAi.ModeSite - image:X}) and the step (0x{StoryAi.StepSite - image:X}) jump to their " +
              $"stubs: {JumpsTo(StoryAi.ModeSite, StoryAi.ModeStub)}/{JumpsTo(StoryAi.StepSite, StoryAi.StepStub)}, as written " +
              $"{modeSame}/{stepSame}; back to mode 0x11 at 0x{StoryAi.ModeTargets.Mode - image:X}, the Focus Unit test at " +
              $"0x{StoryAi.ModeTargets.Focus - image:X}, the mode's turn at 0x{StoryAi.StepTargets.Turn - image:X}",
              JumpsTo(StoryAi.ModeSite, StoryAi.ModeStub) && JumpsTo(StoryAi.StepSite, StoryAi.StepStub) && modeSame && stepSame
              && StoryAi.ModeTargets.Mode - image == 0x316A75 && StoryAi.ModeTargets.Focus - image == 0x316A80
              && StoryAi.StepTargets.Step - image == 0x320BA0 && StoryAi.StepTargets.NotCritical - image == 0x320BA9
              && StoryAi.StepTargets.Turn - image == 0x320973);
        // The mode test: push rdi; mov rdi, rdx (cl the flags, rdi the record); exits pop rdi: 1 mode 0x11, 2 Focus Unit.
        var mode = Harness(StoryAi.ModeStub, [0x57, 0x48, 0x8B, 0xFA],
                           [[0xB8, 1, 0, 0, 0, 0x5F, 0xC3], [0xB8, 2, 0, 0, 0, 0x5F, 0xC3]],
                           (copy, exits) => StoryAi.WriteModeStub((byte*)copy, exits[0], exits[1]));
        var record = stackalloc byte[16];
        new Span<byte>(record, 16).Clear();
        string Mode(int flags, int byte38)
        {
            record[7] = (byte)byte38;
            return ((delegate* unmanaged<int, byte*, int>)mode)(flags, record) == 1 ? "0x11" : "focus";
        }
        var post = Mode(0x28, 4);
        var keepAway = Mode(0x08, 4);
        var plain = Mode(0x08, 0);
        var nearOnly = Mode(0x28, 0);
        Check($"the mode: Defensive with 0x04 {keepAway}, with Stay Near XY too {post}, without 0x04 {plain} and {nearOnly}",
              keepAway == "0x11" && post == "focus" && plain == "focus" && nearOnly == "focus");
        // The step: mov rax, rcx (the unit); exits 1 the step (Critical), 2 the next test, 3 the mode's turn.
        var step = Harness(StoryAi.StepStub, [0x48, 0x8B, 0xC1],
                           [[0xB8, 1, 0, 0, 0, 0xC3], [0xB8, 2, 0, 0, 0, 0xC3], [0xB8, 3, 0, 0, 0, 0xC3]],
                           (copy, exits) => StoryAi.WriteStepStub((byte*)copy, exits[0], exits[1], exits[2]));
        var unit = stackalloc byte[0x200];
        new Span<byte>(unit, 0x200).Clear();
        int Step(int flags, int byte38, int critical)
        {
            unit[StoryAi.Flags] = (byte)flags;
            unit[StoryAi.Byte38] = (byte)byte38;
            unit[0x63] = (byte)critical;
            return ((delegate* unmanaged<byte*, int>)step)(unit);
        }
        var steps = $"{Step(0x28, 4, 1)}{Step(0x28, 4, 0)}{Step(0x08, 4, 1)}{Step(0x08, 4, 0)}{Step(0x28, 0, 2)}{Step(0x20, 4, 1)}" +
                    $"{Step(0, 0, 0)}{Step(0x2C, 0x84, 1)}";
        Check($"the step (3 skipped, 1 Critical or a Frog, 2 the next test): {steps}", steps == "33121123");
        return failures;
    }

    /// <summary>
    /// ReMixed's unit presets: the stat step's product jumps to the stub, a copy of the stub scales a slot's stats (and
    /// leaves the others and a unit outside the battle's alone), and the recalculation's part ORs statuses, abilities
    /// and elements and scales the class evade, on made-up tables.
    /// </summary>
    public static int UnitPresetRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: unit presets, {name}{(ok ? "" : ": WRONG")}");
        }
        if (UnitPresets.Stub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        Check($"the stat product (0x{UnitPresets.ProductAt - game.Image.ImageStart:X}) jumps to the stub, the recalculation "
              + $"at 0x{UnitPresets.RecalculateAt - game.Image.ImageStart:X}",
              ((byte*)UnitPresets.ProductAt)[0] == 0xE9 && GameImage.Rip(UnitPresets.ProductAt, 1, 5) == UnitPresets.Stub);
        // Harness: (unit, stat, raw, multiplier) -> r10, r9d, r8d, eax; ecx 100, edx 0; then a copy of the stub, back to a ret.
        var harness = GameImage.AllocateCodeNear(UnitPresets.Stub, 48 + UnitPresets.StubSize);
        byte[] entry = [0x49, 0x89, 0xCA, 0x44, 0x89, 0xC8, 0x41, 0x89, 0xD1, 0xB9, 0x64, 0, 0, 0, 0x33, 0xD2,
                        0xFF, 0x25, 0, 0, 0, 0];
        new ReadOnlySpan<byte>(entry).CopyTo(new Span<byte>(harness, entry.Length));
        var copy = harness + 48;
        *(long*)(harness + entry.Length) = (long)copy;
        var back = harness + entry.Length + 8;
        *back = 0xC3;
        var units = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)(Battle.UnitCount * Battle.UnitSize));
        var scales = (ushort*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)(Battle.UnitCount * UnitPresets.Stats * 2));
        UnitPresets.WriteStub(copy, (nint)back, units, scales);
        var run = (delegate* unmanaged<byte*, int, int, int, int>)harness;
        scales[3 * UnitPresets.Stats + 0] = 2 | 1 << 8;  // slot 3: HP x 2/1
        scales[3 * UnitPresets.Stats + 2] = 3 | 5 << 8;  // Speed x 3/5
        scales[3 * UnitPresets.Stats + 1] = 0 | 1 << 8;  // MP x 0
        var slot3 = units + 3 * Battle.UnitSize;
        var hp = run(slot3, 0, 1000, 12);
        var speed = run(slot3, 2, 1000, 12);
        var mp = run(slot3, 1, 1000, 12);
        var pa = run(slot3, 3, 1000, 12);
        var plain = run(units + 4 * Battle.UnitSize, 0, 1000, 12);
        var outside = run(units + Battle.UnitCount * Battle.UnitSize, 0, 1000, 12);
        Check($"the stub: HP {hp} (x2), Speed {speed} (x3/5), MP {mp} (x0), PA {pa}, another slot's {plain}, a unit outside "
              + $"{outside}", hp == 240 && speed == 72 && mp == 0 && pa == 120 && plain == 120 && outside == 120);
        // Made-up tables: preset 1 = scaling 1 (class evade x1/2), statuses 1 innate (0x20), 2 immune (0x40), 1 current;
        // abilities 1 (its 8th byte 0x80); elements given 2 (0x10 first), taken 0 (its fifth byte 0x80, as ReMixed's, whose
        // sets are 4 bytes apart and read 5).
        var tables = new byte[UnitPresets.RandomAt + 3];
        tables[UnitPresets.ScalingAt + UnitPresets.ScalingSize + 10] = 1;
        tables[UnitPresets.ScalingAt + UnitPresets.ScalingSize + 11] = 1;
        tables[UnitPresets.StatusAt + UnitPresets.StatusSize] = 0x20;
        tables[UnitPresets.StatusAt + 2 * UnitPresets.StatusSize + 1] = 0x40;
        tables[UnitPresets.AbilityAt + UnitPresets.AbilitySize + 7] = 0x80;
        tables[UnitPresets.ElementAt + 2 * UnitPresets.ElementStride] = 0x10;
        tables[UnitPresets.ElementAt + 4] = 0x80;
        byte[] preset1 = [0, 1, 1, 2, 1, 1, 2, 0, 0x02];
        preset1.CopyTo(tables, UnitPresets.PresetAt + UnitPresets.PresetSize);
        var unit = units + 5 * Battle.UnitSize;
        unit[UnitPresets.ClassEvade] = 15;
        unit[UnitPresets.Elements + 4] = 0x81;
        UnitPresets.Apply(unit, 1, tables);
        var statuses = Convert.ToHexString(new ReadOnlySpan<byte>(unit + UnitPresets.Statuses, 15));
        var abilities = Convert.ToHexString(new ReadOnlySpan<byte>(unit + UnitPresets.Abilities, 11));
        var elements = Convert.ToHexString(new ReadOnlySpan<byte>(unit + UnitPresets.Elements, 5));
        Check($"the recalculation's part: class evade {unit[UnitPresets.ClassEvade]}, statuses {statuses}, abilities {abilities}, "
              + $"elements {elements}",
              unit[UnitPresets.ClassEvade] == 7 && statuses == "200000000000400000002000000000"
              && abilities == "0000000000000080000000" && elements == "1000000003");
        System.Runtime.InteropServices.NativeMemory.Free(units);
        System.Runtime.InteropServices.NativeMemory.Free(scales);
        return failures;
    }

    /// <summary>
    /// Athlete's Jump +1 and Pass's Move +1 through the remaster's own Move and Jump step, on a made-up unit (base Move
    /// and Jump at +0x42 and +0x43, Move+N and Jump+N bits at +0x9C, ReMixed's at +0x9E).
    /// </summary>
    public static int MovementBonusRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Athlete and Pass, {name}{(ok ? "" : ": WRONG")}");
        }
        if (MovementBonus.Stub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        Check($"the step's caps (0x{MovementBonus.At - game.Image.ImageStart:X}) jump to the stub",
              ((byte*)MovementBonus.At)[0] == 0xE9 && GameImage.Rip(MovementBonus.At, 1, 5) == MovementBonus.Stub);
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var step = (delegate* unmanaged<byte*, int, void>)MovementBonus.Step;
        string Run(int move, int jump, int bonuses, int remixed)
        {
            unit[0x42] = (byte)move;
            unit[0x43] = (byte)jump;
            unit[0x9C] = (byte)bonuses;
            unit[0x9E] = (byte)remixed;
            step(unit, 1);
            return $"{unit[0x42]}/{unit[0x43]}";
        }
        var plain = Run(4, 3, 0, 0);
        var athlete = Run(4, 3, 0, MovementBonus.Athlete);
        var pass = Run(4, 3, 0, MovementBonus.Pass);
        var all = Run(4, 3, 0x80 | 0x10, MovementBonus.Athlete | MovementBonus.Pass);
        var capped = Run(4, 4, 0x04, MovementBonus.Athlete);
        var others = Run(4, 3, 0, 0xBD);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        Check($"Move/Jump 4/3: plain {plain}, Athlete {athlete}, Pass {pass}, both with Move+1 and Jump+1 {all}; Jump 4 with Jump+3 and Athlete {capped}; the other movement bits {others}",
              plain == "4/3" && athlete == "4/4" && pass == "5/3" && all == "6/5" && capped == "4/7" && others == "4/3");
        return failures;
    }

    /// <summary>
    /// The ability lists' innate check through the game's own address (now ReMixed's, on the job data), and a copy of
    /// the breeding stub on made-up roster units.
    /// </summary>
    public static int ErrandAndListRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string area, string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: {area}, {name}{(ok ? "" : ": WRONG")}");
        }
        var image = game.Image.ImageStart;
        if (JobChange.ListCheckStub == 0)
            Check("innates in the ability lists", "aren't installed", false);
        else
        {
            var check = (delegate* unmanaged<int, int, byte>)JobChange.ListCheck;
            const int job = 0x4B;
            var data = game.Units.Jobs + job * UnitTables.JobSize;
            var ids = Enumerable.Range(0, 5).Select(k => (int)*(ushort*)(data + 1 + 2 * k)).Where(id => id != 0).ToList();
            var listed = ids.Select(id => check(job, id)).ToList();
            var other = check(job, 0x1E0) + check(job, 0) + check(0x4E, ids[0] == 0x1DA ? 0x1DB : 0x1DA);
            Check("innates in the ability lists",
                  $"job 0x{job:X2}'s ids {string.Join(" ", ids.Select(i => $"0x{i:X3}"))} are innate ({string.Join(" ", listed)}); another ability, none and another job's aren't ({other}) (0x{JobChange.ListCheck - image:X})",
                  ids.Count > 0 && listed.All(l => l == 1) && other == 0);
        }
        if (Breeding.Stub == 0)
        {
            Check("breeding", "isn't installed", false);
            return failures;
        }
        Check("breeding", $"the empty-slot test (0x{Breeding.At - image:X}) jumps to the stub",
              ((byte*)Breeding.At)[0] == 0xE9 && GameImage.Rip(Breeding.At, 1, 5) == Breeding.Stub);
        // push rbx; mov rbx, rcx; the stub's copy; on: mov eax, 1 | away: xor eax, eax; pop rbx; ret.
        var harness = GameImage.AllocateCodeNear(Breeding.Stub, 48 + Breeding.StubSize);
        byte[] entry = [0x53, 0x48, 0x8B, 0xD9, 0xFF, 0x25, 0, 0, 0, 0];
        new ReadOnlySpan<byte>(entry).CopyTo(new Span<byte>(harness, entry.Length));
        var copy = harness + 48;
        *(long*)(harness + entry.Length) = (long)copy;
        var on = harness + entry.Length + 8;
        byte[] onCode = [0xB8, 1, 0, 0, 0, 0x5B, 0xC3];
        new ReadOnlySpan<byte>(onCode).CopyTo(new Span<byte>(on, onCode.Length));
        var away = on + onCode.Length;
        byte[] awayCode = [0x33, 0xC0, 0x5B, 0xC3];
        new ReadOnlySpan<byte>(awayCode).CopyTo(new Span<byte>(away, awayCode.Length));
        Breeding.WriteStub(copy, (nint)on, (nint)away);
        var run = (delegate* unmanaged<byte*, int>)harness;
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(UnitTables.RosterSize);
        string Step(int slotByte, int errand)
        {
            unit[1] = (byte)slotByte;
            unit[Breeding.Errand] = (byte)errand;
            return run(unit) == 1 ? "steps" : "skipped";
        }
        var present = Step(0x80, 0);
        var empty = Step(0xFF, 0);
        var errand = Step(0x80, 1);
        var gone = Step(0x80, 0x08);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        Check("breeding", $"a unit at home {present}, an empty slot {empty}, one on an errand {errand}, one gone (0x08) {gone}",
              present == "steps" && empty == "skipped" && errand == "skipped" && gone == "skipped");
        return failures;
    }

    /// <summary>
    /// Builds a harness: <paramref name="entry"/> then a jump to a copy of a stub, and each exit's code after it; the
    /// stub's copy is written by <paramref name="write"/> with the exits' addresses.
    /// </summary>
    private static nint Harness(nint near, byte[] entry, byte[][] exits, Action<nint, nint[]> write)
    {
        var harness = GameImage.AllocateCodeNear(near, 256);
        new ReadOnlySpan<byte>(entry).CopyTo(new Span<byte>(harness, entry.Length));
        var at = harness + entry.Length;
        at[0] = 0xFF; at[1] = 0x25; *(int*)(at + 2) = 0;
        var copy = harness + 160;
        *(long*)(at + 6) = (long)copy;
        at += 14;
        var addresses = new nint[exits.Length];
        for (var k = 0; k < exits.Length; k++)
        {
            addresses[k] = (nint)at;
            new ReadOnlySpan<byte>(exits[k]).CopyTo(new Span<byte>(at, exits[k].Length));
            at += exits[k].Length;
        }
        write((nint)copy, addresses);
        return (nint)harness;
    }

    /// <summary>
    /// Riding, step 2: a rider (battle slot 3) on a chocobo (slot 4), both put back after: the command list, the turn's
    /// action, the queue's charge time and "acted", and the attacker swap.
    /// </summary>
    public static int RidingActionRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: riding actions, {name}{(ok ? "" : ": WRONG")}");
        }
        if (!RidingActions.On)
        {
            Check("aren't installed", false);
            return failures;
        }
        var units = game.TurnCode.Units;
        var saved = new Span<byte>(units + 3 * Battle.UnitSize, 2 * Battle.UnitSize).ToArray();
        var battle = game.Battle;
        var savedAttacker = *battle.Attacker;
        var savedResult = *game.Routines.AttackerResult;
        var savedWeapon = *(ulong*)battle.Weapon;
        *game.RuleCode.AbilitiesReady = 1;
        const int ability = 0x10D;
        var data = game.RuleCode.GetAbilityAction(ability);
        var savedCt = data[0xC];
        var record = stackalloc byte[20];
        try
        {
            var rider = units + 3 * Battle.UnitSize;
            var mount = units + 4 * Battle.UnitSize;
            rider[1] = 0; mount[1] = 0;
            rider[Battle.UnitIndex] = 3; mount[Battle.UnitIndex] = 4;
            rider[6] = 0x80; mount[6] = 0x20;
            rider[RidingActions.Link] = 0x84; mount[RidingActions.Link] = 0x43;
            rider[RidingActions.Primary] = 0x05; mount[RidingActions.Primary] = 0xB0;
            RidingActions.Reset();
            string List()
            {
                var entries = new List<int> { 1, 5, 2, 3 };
                RidingActions.Commands(3, rider, entries);
                return string.Join(" ", entries.Select(e => $"{e:X2}"));
            }
            int Act(int skillset)
            {
                record[0] = 3;
                record[1] = (byte)skillset;
                *(ushort*)(record + 2) = ability;
                return RidingActions.Acts(3, 1, record);
            }
            var start = List();
            var own = Act(0x05);
            var afterOwn = List();
            var mountSkill = Act(0xB0);
            var afterMount = List();
            RidingActions.TurnEnded(3);
            var first = Act(0xB0);
            var thenOwn = Act(0x05);
            var jumpSet = Enumerable.Range(1, 0xA7).FirstOrDefault(s => (TwoSpells.TypeTable[s] & 0x0F) == RidingActions.JumpType);
            RidingActions.TurnEnded(3);
            var jump = jumpSet > 0 ? Act(jumpSet) : -1;
            Check($"the list {start}; the rider's own action marks {own} (not acted), then {afterOwn}; the mount's skill marks {mountSkill}, then {afterMount}; mount first {first}, then the rider's own {thenOwn}; a Jump (skillset {jumpSet:X2}) {jump}",
                  start == "01 05 B0 02 03" && own == 0 && afterOwn == "B0" && mountSkill == 1 && afterMount == "01 05 02 03"
                  && first == 1 && thenOwn == 1 && jump == 1);
            // The queue: "acted" given back after the rider's own action; the mount skill's charge time 0, then put back.
            RidingActions.TurnEnded(3);
            Act(0x05);
            record[1] = 0x05;
            rider[RidingActions.UnitActed] = 1;
            RidingActions.BeforeQueue(record, rider + RidingActions.UnitRecord, 1);
            RidingActions.AfterQueue(record, rider + RidingActions.UnitRecord, 1);
            var acted = rider[RidingActions.UnitActed];
            data[0xC] = 3;
            record[1] = 0xB0;
            RidingActions.BeforeQueue(record, rider + RidingActions.UnitRecord, 1);
            var during = data[0xC] & 0x7F;
            RidingActions.AfterQueue(record, rider + RidingActions.UnitRecord, 0);
            var after = data[0xC] & 0x7F;
            Check($"after the queue the rider's \"acted\" is {acted}; the mount's skill charges {during}, then the ability's CT is {after} again",
                  acted == 0 && during == 0 && after == 3);
            // The attacker swap.
            *battle.Attacker = rider;
            *game.Routines.AttackerResult = rider + Battle.UnitResult;
            *(ulong*)battle.Weapon = 0x0807060504030201;
            rider[RidingActions.UnitRecord + 1] = 0xB0;
            var swapped = RidingActions.SwapAttacker(out var swap);
            var during2 = $"{(*battle.Attacker == mount ? "mount" : "rider")}/{(*game.Routines.AttackerResult == mount + Battle.UnitResult ? "mount's" : "other")}/{*(ulong*)battle.Weapon:X16}";
            RidingActions.Restore(swap);
            var back = *battle.Attacker == rider && *game.Routines.AttackerResult == rider + Battle.UnitResult && *(ulong*)battle.Weapon == 0x0807060504030201;
            rider[RidingActions.UnitRecord + 1] = 0x05;
            var notMount = RidingActions.SwapAttacker(out _);
            Check($"the mount's skill: swapped {swapped}, the attacker {during2}; put back {back}; the rider's own skillset swaps {notMount}",
                  swapped && during2 == "mount/mount's/0000000000000201" && back && !notMount);
            // A dead rider placed off its mount: the mount takes its CT.
            rider[RidingActions.Ct] = 77; mount[RidingActions.Ct] = 100;
            rider[Battle.UnitStatuses] = 0;
            var alive = RidingActions.DeadRiderCt(rider);
            rider[Battle.UnitStatuses] = RidingActions.Dead;
            var dead = RidingActions.DeadRiderCt(rider);
            Check($"a living rider placed: {alive}; a dead one: {dead}, the mount's CT {mount[RidingActions.Ct]}",
                  !alive && dead && mount[RidingActions.Ct] == 77);
            // EXP and JP: the mount's skill awards the mount; the rider's own skill, the rider.
            rider[Battle.UnitStatuses] = 0;
            rider[RidingActions.UnitRecord + 1] = 0xB0;
            var toMount = RidingActions.ActingMount(rider) == mount;
            rider[RidingActions.UnitRecord + 1] = 0x05;
            var toRider = RidingActions.ActingMount(rider) == null;
            Check($"the EXP and JP: the mount's skill awards the mount {toMount}; the rider's own, the rider {toRider}", toMount && toRider);
            // A charging rider uses its mount's skill: its charge is parked, it keeps Charging, and gets it back at the end of its turn.
            RidingActions.TurnEnded(3);
            var ownRecord = rider + RidingActions.UnitRecord;
            ownRecord[0] = 3; ownRecord[1] = 0x05; ownRecord[2] = 0x10; ownRecord[3] = 0x00;
            rider[0x18D] = 6;
            rider[Battle.UnitStatuses] = 0x08;
            record[0] = 3; record[1] = 0xB0; record[2] = 0x7D; record[3] = 0x01;
            RidingActions.BeforeQueue(record, ownRecord, 1);
            new Span<byte>(record, 20).CopyTo(new Span<byte>(ownRecord, 20)); // the game's queue
            rider[0x18D] = 0xFF;                                          // the instant skill fired
            RidingActions.AfterQueue(record, ownRecord, 1);
            var parked = RidingActions.Parked(3);
            var keeps = TwoSpells.StartAction(rider, 0xFF) == 0;
            RidingActions.TurnEnded(3);
            var chargeBack = ownRecord[1] == 0x05 && ownRecord[2] == 0x10 && rider[0x18D] == 6 && !RidingActions.Parked(3);
            // One that stopped Charging meanwhile loses it.
            RidingActions.BeforeQueue(record, ownRecord, 1);
            new Span<byte>(record, 20).CopyTo(new Span<byte>(ownRecord, 20));
            rider[0x18D] = 0xFF;
            RidingActions.AfterQueue(record, ownRecord, 1);
            rider[Battle.UnitStatuses] = 0;
            RidingActions.TurnEnded(3);
            var dropped = ownRecord[1] == 0xB0 && rider[0x18D] == 0xFF && !RidingActions.Parked(3);
            Check($"a charging rider's charge: parked {parked}, keeps Charging at the action's start {keeps}, back at its turn's end {chargeBack}; dropped once it stops Charging {dropped}",
                  parked && keeps && chargeBack && dropped);
        }
        finally
        {
            saved.CopyTo(new Span<byte>(units + 3 * Battle.UnitSize, 2 * Battle.UnitSize));
            *battle.Attacker = savedAttacker;
            *game.Routines.AttackerResult = savedResult;
            *(ulong*)battle.Weapon = savedWeapon;
            data[0xC] = savedCt;
            RidingActions.Reset();
        }
        return failures;
    }

    /// <summary>
    /// Riding, step 1: the job test and boarding stubs through harnesses, and Train's conversion on two unit slots (put
    /// back after).
    /// </summary>
    public static int RidingBoardingRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: riding, {name}{(ok ? "" : ": WRONG")}");
        }
        if (RidingBoarding.Sites.Count != 7)
        {
            Check($"{RidingBoarding.Sites.Count} of 7 sites installed", false);
            return failures;
        }
        var image = game.Image.ImageStart;
        Check($"sites: {string.Join(", ", RidingBoarding.Sites.Select(s => $"{s.Name} 0x{s.At - image:X}"))}",
              RidingBoarding.Sites.All(s => ((byte*)s.At)[0] == 0xE9 && GameImage.Rip(s.At, 1, 5) == s.Stub));
        var near = RidingBoarding.Sites[0].Stub;
        // The job test: 1 inside (a mount's job), 0 outside.
        var job = (delegate* unmanaged<int, int>)Harness(near, [0x8B, 0xC1],
            [[0xB8, 1, 0, 0, 0, 0xC3], [0x33, 0xC0, 0xC3]],
            (copy, exits) => RidingBoarding.WriteJobStub((byte*)copy, exits[0], exits[1]));
        var jobs = string.Join("", new[] { 0x5D, 0x5E, 0x60, 0x61, 0x95, 0x4A }.Select(j => job(j)));
        Check($"mount jobs 5D 5E 60 61 95 4A: {jobs}", jobs == "011010");
        // The boarding record: r9 the units, r10 the other unit's offset, r12 the mover, r15b its team, dil the
        // other's link, bpl its index and flags; returns r8d (the flags the game goes on with).
        var board = (delegate* unmanaged<long*, int>)Harness(near,
            [0x55, 0x57, 0x41, 0x54, 0x41, 0x57, 0x4C, 0x8B, 0x09, 0x4C, 0x8B, 0x51, 0x08, 0x4C, 0x8B, 0x61, 0x10,
             0x4C, 0x8B, 0x79, 0x18, 0x48, 0x8B, 0x79, 0x20, 0x48, 0x8B, 0x69, 0x28],
            [[0x44, 0x89, 0xC0, 0x41, 0x5F, 0x41, 0x5C, 0x5F, 0x5D, 0xC3]],
            (copy, exits) => RidingBoarding.WriteBoardingStub((byte*)copy, Pass.Mover, exits[0]));
        var units = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(2 * Battle.UnitSize);
        var savedMover = *Pass.Mover;
        var args = stackalloc long[6];
        string Board(int team, int pass, int train, int link = 0, int flags = 0)
        {
            units[5] = (byte)team;                               // the other unit (offset 0)
            units[Battle.UnitSize + RidingBoarding.TrainByte] = (byte)(train != 0 ? RidingBoarding.Train : 0);
            *Pass.Mover = (byte)(pass != 0 ? Pass.PassBit : 0);
            args[0] = (long)units; args[1] = 0; args[2] = 1; args[3] = 0; args[4] = link; args[5] = 3 | flags;
            return (board(args) & 0x20) != 0 ? "can't stop" : "can stop";
        }
        var ally = Board(0, 0, 0);
        var enemy = Board(0x10, 0, 0);
        var passOnly = Board(0x10, 1, 0);
        var passTrain = Board(0x10, 1, 1);
        var own = Board(0x10, 0, 0, 0x41);
        var notMount = Board(0x10, 1, 1, 0, 0x20);
        *Pass.Mover = savedMover;
        System.Runtime.InteropServices.NativeMemory.Free(units);
        Check($"a free mount: an ally's {ally}, an enemy's {enemy}, with Pass {passOnly}, with Pass and Train {passTrain}; the mover's own {own}; a unit that isn't a free mount {notMount}",
              ally == "can stop" && enemy == "can't stop" && passOnly == "can't stop" && passTrain == "can stop"
              && own == "can stop" && notMount == "can't stop");
        // Train at the rider's turn start, on battle slots 3 (rider) and 4 (mount).
        var slots = game.TurnCode.Units;
        var saved = new Span<byte>(slots + 3 * Battle.UnitSize, 2 * Battle.UnitSize).ToArray();
        var rider = slots + 3 * Battle.UnitSize;
        var mount = slots + 4 * Battle.UnitSize;
        string Turn(int train, int mountTeam)
        {
            rider[1] = 0; mount[1] = 0;
            rider[RidingBoarding.Link] = 0x84; mount[RidingBoarding.Link] = 0x43;
            rider[Battle.UnitIndex] = 3; mount[Battle.UnitIndex] = 4;
            rider[5] = 0x08; rider[Battle.UnitTeam] = 0x08;
            mount[5] = (byte)mountTeam; mount[Battle.UnitTeam] = (byte)mountTeam;
            mount[RidingBoarding.InviteByte] = 0;
            rider[RidingBoarding.TrainByte] = (byte)(train != 0 ? RidingBoarding.Train : 0);
            var won = RidingBoarding.Convert(rider);
            return $"{(won ? "won" : "kept")} {mount[5]:X2}/{mount[Battle.UnitTeam]:X2}/{mount[RidingBoarding.InviteByte]:X2}";
        }
        var win = Turn(1, 0x10);
        var noTrain = Turn(0, 0x10);
        var mine = Turn(1, 0x00);
        saved.CopyTo(new Span<byte>(slots + 3 * Battle.UnitSize, 2 * Battle.UnitSize));
        Check($"Train: an enemy mount {win} (team +5/+0x1EE, Invite); without Train {noTrain}; an ally's mount {mine}",
              win == "won 00/00/40" && noTrain == "kept 10/10/00" && mine == "kept 00/00/00");
        return failures;
    }

    /// <summary>
    /// Errands: the remaster's candidate check called on roster slots set up for each case (then put back), the loops'
    /// bounds, and the party condition's stub through a harness.
    /// </summary>
    public static int ErrandRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: errands, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Errands.ConditionStub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var image = game.Image.ImageStart;
        Check($"bytes: {string.Join(", ", Errands.Bytes.Select(b => $"{b.Name} 0x{b.At - image:X} = 0x{*(byte*)b.At:X2}"))}",
              Errands.Bytes.Count == 3 && Errands.Bytes.All(b => *(byte*)b.At == b.Value));
        var roster = game.Units.Roster;
        var candidate = (delegate* unmanaged<int, nint>)Errands.Candidate;
        string Can(int slot, int sprite, int errand = 0)
        {
            var unit = roster + slot * UnitTables.RosterSize;
            var saved = new Span<byte>(unit, UnitTables.RosterSize).ToArray();
            unit[0] = (byte)sprite;
            unit[1] = 0;
            unit[4] = 0;
            unit[Errands.Errand] = (byte)errand;
            var can = candidate(slot) != 0;
            saved.CopyTo(new Span<byte>(unit, UnitTables.RosterSize));
            return can ? "can" : "can't";
        }
        var generic = Can(5, 0x80);
        var special = Can(5, 0x10);
        var ramza = Can(0, 0x02);
        var own = Can(5, 0xA2);
        var away = Can(5, 0x80, 1);
        var guest = Can(0x33, 0x10);
        Check($"a generic {generic}, a special character {special}, Ramza {ramza}, the remaster's 0xA2 {own}, a unit away {away}, a guest (slot 0x33) {guest}",
              generic == "can" && special == "can" && ramza == "can't" && own == "can't" && away == "can't" && guest == "can");
        Check($"the condition's sprite-set match (0x{Errands.ConditionAt - image:X}) jumps to the stub",
              ((byte*)Errands.ConditionAt)[0] == 0xE9 && GameImage.Rip(Errands.ConditionAt, 1, 5) == Errands.ConditionStub);
        // mov r10d, edx; the stub's copy; cont 0, next (away) 1, found 2.
        var match = (delegate* unmanaged<byte*, int, int>)Harness(Errands.ConditionStub, [0x44, 0x8B, 0xD2],
            [[0xB8, 0, 0, 0, 0, 0xC3], [0xB8, 1, 0, 0, 0, 0xC3], [0xB8, 2, 0, 0, 0, 0xC3]],
            (copy, exits) => Errands.WriteConditionStub((byte*)copy, exits[0], exits[1], exits[2]));
        var roster0 = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(UnitTables.RosterSize);
        string Match(int sprite, int id, int errand)
        {
            roster0[0] = (byte)sprite;
            roster0[Errands.Errand] = (byte)errand;
            return match(roster0, id) switch { 0 => "name-id paths", 1 => "skipped", _ => "found" };
        }
        var home = Match(0x10, 0x10, 0);
        var gone = Match(0x10, 0x10, 1);
        var other = Match(0x11, 0x10, 0);
        System.Runtime.InteropServices.NativeMemory.Free(roster0);
        Check($"the party condition: at home {home}, away {gone}, another character {other}",
              home == "found" && gone == "skipped" && other == "name-id paths");
        return failures;
    }

    /// <summary>One-handing's five stubs, each through a copy in a harness, with and without the supports (0x42).</summary>
    public static int OneHandingRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: one-handing, {name}{(ok ? "" : ": WRONG")}");
        }
        if (OneHanding.Sites.Count != 5)
        {
            Check($"{OneHanding.Sites.Count} of 5 sites installed", false);
            return failures;
        }
        var image = game.Image.ImageStart;
        Check($"sites: {string.Join(", ", OneHanding.Sites.Select(s => $"{s.Name} 0x{s.At - image:X}"))}",
              OneHanding.Sites.All(s => ((byte*)s.At)[0] == 0xE9 && GameImage.Rip(s.At, 1, 5) == s.Stub));
        var near = OneHanding.Sites[0].Stub;
        var records = OneHanding.Records;
        var saved = records[0];
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x280);
        records[0] = (nint)record;
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var weapon = stackalloc byte[8];
        try
        {
            // The slot check's new item: edi = flags & 1 unless the record has the supports (r11d = flags & 8).
            var newItem = (delegate* unmanaged<int, long, int>)Harness(near,
                [0x57, 0x55, 0x44, 0x8B, 0xD9, 0x8B, 0xF9, 0x48, 0x8B, 0xEA], [[0x8B, 0xC7, 0x5D, 0x5F, 0xC3]],
                (copy, exits) => OneHanding.WriteNewItemStub((byte*)copy, records, exits[0]));
            // The other hand: edx = flags & 1 unless the record (r12) has them.
            var otherHand = (delegate* unmanaged<int, byte*, int>)Harness(near,
                [0x41, 0x54, 0x4C, 0x8B, 0xE2, 0x8B, 0xD1], [[0x8B, 0xC2, 0x41, 0x5C, 0xC3]],
                (copy, exits) => OneHanding.WriteOtherHandStub((byte*)copy, exits[0]));
            // The optimize routines: 1 when the left hand is skipped (index at [rsp+0x36], or in r14).
            var optimizeStack = (delegate* unmanaged<byte*, long, int>)Harness(near,
                [0x48, 0x83, 0xEC, 0x48, 0x66, 0x89, 0x54, 0x24, 0x36, 0x48, 0x8B, 0xC1],
                [[0xB8, 1, 0, 0, 0, 0x48, 0x83, 0xC4, 0x48, 0xC3], [0x33, 0xC0, 0x48, 0x83, 0xC4, 0x48, 0xC3]],
                (copy, exits) => OneHanding.WriteOptimizeStub((byte*)copy, records, false, exits[0], exits[1]));
            var optimizeR14 = (delegate* unmanaged<byte*, long, int>)Harness(near,
                [0x41, 0x56, 0x4C, 0x8B, 0xF2, 0x48, 0x8B, 0xC1],
                [[0xB8, 1, 0, 0, 0, 0x41, 0x5E, 0xC3], [0x33, 0xC0, 0x41, 0x5E, 0xC3]],
                (copy, exits) => OneHanding.WriteOptimizeStub((byte*)copy, records, true, exits[0], exits[1]));
            // Random gear: 1 when no left-hand item (rbx the unit).
            var gear = (delegate* unmanaged<byte*, byte*, int>)Harness(near,
                [0x53, 0x48, 0x8B, 0xDA, 0x48, 0x8B, 0xC1],
                [[0xB8, 1, 0, 0, 0, 0x5B, 0xC3], [0x33, 0xC0, 0x5B, 0xC3]],
                (copy, exits) => OneHanding.WriteGearStub((byte*)copy, exits[1], exits[0]));
            string Run(int flags, int supports)
            {
                record[OneHanding.RecordSupports] = (byte)supports;
                unit[OneHanding.UnitSupports] = (byte)supports;
                weapon[1] = (byte)flags;
                return $"{newItem(flags, 0)}{otherHand(flags, record)}{optimizeStack(weapon, 0)}{optimizeR14(weapon, 0)}{gear(weapon, unit)}";
            }
            var axe = Run(0x09, 0);
            var axeShield = Run(0x09, 0x40);
            var axe1CC = Run(0x09, 0x02);
            var sword = Run(0x08, 0x42);
            var otherBits = Run(0x09, 0xBD);
            Check($"a forced weapon (slot check new/other hand, optimize ×2, random gear): without supports {axe}, with Equip Shield {axeShield}, with 0x1CC {axe1CC}; a sword {sword}; other support bits {otherBits}",
                  axe == "11111" && axeShield == "00000" && axe1CC == "00000" && sword == "00000" && otherBits == "11111");
        }
        finally
        {
            records[0] = saved;
            System.Runtime.InteropServices.NativeMemory.Free(record);
            System.Runtime.InteropServices.NativeMemory.Free(unit);
        }
        return failures;
    }

    /// <summary>The lock check and the job change's wrapper, called with a made-up record in the menu's first slot.</summary>
    public static int ErrandLockRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: errand locks, {name}{(ok ? "" : ": WRONG")}");
        }
        if (ErrandLocks.TailStub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var image = game.Image.ImageStart;
        var records = Dismissal.Records;
        var saved = records[0];
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x280);
        records[0] = (nint)record;
        var check = (delegate* unmanaged<int, int>)ErrandLocks.Check;
        var job = (delegate* unmanaged<int, int>)ErrandLocks.JobCheck;
        string Both(int errand, int jobId = 0x4A)
        {
            *(short*)(record + 0x24) = (short)jobId;
            record[ErrandLocks.Errand] = (byte)errand;
            return $"{check(0)}/{job(0)}";
        }
        var home = Both(0);
        var away = Both(ErrandLocks.Away);
        var locked = Both(ErrandLocks.Locked);
        var noJob = Both(ErrandLocks.NoJob);
        var gone = Both(0x08);
        var mime = Both(0, ErrandLocks.Mime);
        records[0] = saved;
        System.Runtime.InteropServices.NativeMemory.Free(record);
        Check($"check/job change: at home {home}, away (0x01) {away}, locked (0x40) {locked}, 0x20 {noJob}, 0x08 {gone}, a Mime {mime} (0x{ErrandLocks.Check - image:X}, 0x{ErrandLocks.JobCheck - image:X})",
              home == "1/1" && away == "0/0" && locked == "-6/-6" && noJob == "1/-2" && gone == "1/1" && mime == "-6/-6");
        return failures;
    }

    /// <summary>
    /// Hitting bodies: the game's tile info on a dead unit (slot 4), the spear's stub, a copy of the dead-height stub on a
    /// made-up sprite, and a copy of the height stub (a made-up height routine, the real C#) against an aimed panel.
    /// </summary>
    public static int HittingBodyRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: hitting bodies, {name}{(ok ? "" : ": WRONG")}");
        }
        if (HittingBodies.SpearStub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var units = game.TurnCode.Units;
        var saved = new Span<byte>(units + 3 * Battle.UnitSize, 2 * Battle.UnitSize).ToArray();
        var buffer = stackalloc byte[16];
        try
        {
            var archer = units + 3 * Battle.UnitSize;
            var body = units + 4 * Battle.UnitSize;
            archer[1] = 3; body[1] = 4;
            archer[Battle.UnitStatuses] = 0; body[Battle.UnitStatuses] = 0x20; body[0x62] = 0;
            body[HittingBodies.X] = 5; body[HittingBodies.Y] = 7; body[HittingBodies.Level] = 0x80;
            // The tile info and the spear.
            var nop = ((byte*)HittingBodies.DeadTest)[0] == 0x0F && ((byte*)HittingBodies.DeadTest)[1] == 0x1F;
            var tile = game.RuleCode.TileInfo(buffer, 4)[3] != 0x80;
            var spear = ((delegate* unmanaged<byte*, int, byte*>)HittingBodies.SpearStub)(buffer, 4)[3] == 0x80;
            body[Battle.UnitStatuses] = 0;
            var spearAlive = ((delegate* unmanaged<byte*, int, byte*>)HittingBodies.SpearStub)(buffer, 4)[3] != 0x80;
            body[Battle.UnitStatuses] = 0x20;
            Check($"the tile info finds a body {tile} (its dead test gone {nop}); the spear's middle tile still skips it {spear}, not a living unit {spearAlive}",
                  nop && tile && spear && spearAlive);
            // The dead-height stub: mov rax, rcx; xor ecx, ecx; the copy; next: mov eax, 2 | done: mov eax, ecx.
            var deadJumps = HittingBodies.DeadSites.All(s => ((byte*)s)[0] == 0xE9);
            var harness = Harness(HittingBodies.DeadStubs[0], [0x48, 0x8B, 0xC1, 0x33, 0xC9],
                                  [[0xB8, 2, 0, 0, 0, 0xC3], [0x8B, 0xC1, 0xC3]],
                                  (copy, exits) => HittingBodies.WriteDeadStub((byte*)copy, exits[0], exits[1]));
            var sprite = stackalloc byte[0x200];
            new Span<byte>(sprite, 0x200).Clear();
            var living = ((delegate* unmanaged<byte*, int>)harness)(sprite);
            sprite[0x15C] = 4;
            var deadSprite = ((delegate* unmanaged<byte*, int>)harness)(sprite);
            Check($"a weapon path's height: both copies jump to the stub {deadJumps}; a living sprite goes on ({living}), a dead one is the marker ({deadSprite})",
                  deadJumps && living == 2 && deadSprite == HittingBodies.Marker);
            // The height stub: a height routine returning ecx's low byte as the height (unit 4 → the marker if 1).
            var near = HittingBodies.SpearStub;
            var code = GameImage.AllocateCodeNear(near, 128);
            byte[] heightFn = [0x8B, 0xC2, 0xC3];                                   // mov eax, edx (the test's height); ret
            new ReadOnlySpan<byte>(heightFn).CopyTo(new Span<byte>(code, heightFn.Length));
            var stub = code + 16;
            HittingBodies.WriteHeightStub(stub, (nint)code, HittingBodies.Managed, [0x44, 0x89, 0xF2]);
            // entry: push r14; push r8; mov r14d, r8d (the archer); mov r8, sentinel... keep it simple: r14d = r8d.
            byte[] entry = [0x41, 0x56, 0x45, 0x89, 0xC6, 0x48, 0x83, 0xEC, 0x20, 0xE8, 0, 0, 0, 0, 0x48, 0x83, 0xC4, 0x20, 0x41, 0x5E, 0xC3];
            var run = code + 96;
            new ReadOnlySpan<byte>(entry).CopyTo(new Span<byte>(run, entry.Length));
            *(int*)(run + 10) = checked((int)(stub - (run + 14)));
            var height = (delegate* unmanaged<int, int, int, int>)run;  // (unit id, the game's height, archer id)
            archer[HittingBodies.AimType] = 1; archer[HittingBodies.AimX] = 5; archer[HittingBodies.AimY] = 7; archer[HittingBodies.AimLevel] = 1;
            var aimed = height(4, 1, 3);
            var plain = height(4, 12, 3);
            archer[HittingBodies.AimX] = 6;
            var elsewhere = height(4, 1, 3);
            archer[HittingBodies.AimX] = 5; archer[HittingBodies.AimLevel] = 0;
            var otherLevel = height(4, 1, 3);
            archer[HittingBodies.AimLevel] = 1; archer[HittingBodies.AimType] = 0;
            var noAim = height(4, 1, 3);
            var sets = $"{HittingBodies.SetHeights[0x49 * 4]}/{HittingBodies.SetHeights[HittingBodies.ShortSet * 4]}/{HittingBodies.SetHeights[HittingBodies.ShortSet2 * 4]}";
            Check($"the sprite set heights 0x49/0x4A/0x4B: {sets}", sets == "120/24/24");
            Check($"a body's height: on the aimed panel {aimed}, another panel {elsewhere}, another level {otherLevel}, no aimed panel {noAim}; a living unit's height passes ({plain})",
                  aimed == HittingBodies.FullHeight && elsewhere == 0 && otherLevel == 0 && noAim == 0 && plain == 12);
        }
        finally
        {
            saved.CopyTo(new Span<byte>(units + 3 * Battle.UnitSize, 2 * Battle.UnitSize));
        }
        return failures;
    }

    /// <summary>
    /// Stolen equipment: whose gear counts, a steal and a break on a unit slot (3), and the battle end's return into the
    /// inventory through the game's own owned-and-equipped count (the inventory put back after).
    /// </summary>
    public static int TakenEquipmentRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: taken equipment, {name}{(ok ? "" : ": WRONG")}");
        }
        if (TakenEquipment.Inventory == null)
        {
            Check("isn't installed", false);
            return failures;
        }
        var units = game.TurnCode.Units;
        var unit = units + 3 * Battle.UnitSize;
        var saved = new Span<byte>(unit, Battle.UnitSize).ToArray();
        var inventory = new Span<byte>(TakenEquipment.Inventory, TakenEquipment.Items).ToArray();
        var result = stackalloc byte[0x40];
        var before = stackalloc ushort[TakenEquipment.SlotCount];
        try
        {
            // Whose gear: a roster unit, a guest the player controls; not an enemy, not a guest the AI controls.
            unit[2] = 3; unit[5] = 0x10; unit[6] = 0x80;
            var roster = TakenEquipment.Players(unit);
            unit[2] = 0xFF; unit[5] = 0x08;
            var guest = TakenEquipment.Players(unit);
            unit[5] = 0x18;
            var enemy = TakenEquipment.Players(unit);
            unit[5] = 0x00;
            var aiGuest = TakenEquipment.Players(unit);
            Check($"whose gear comes back: a roster unit {roster}, a player's guest {guest}, an enemy {enemy}, an AI guest {aiGuest}",
                  roster && guest && !enemy && !aiGuest);
            // A steal empties slot 0 (item 0x24) of a roster unit; slot 1 (0x25) stays. Then a break of slot 1.
            TakenEquipment.Clear();
            unit[2] = 3; unit[5] = 0x08;
            var slots = (ushort*)(unit + TakenEquipment.Slots);
            for (var k = 0; k < TakenEquipment.SlotCount; k++)
                slots[k] = TakenEquipment.Empty;
            slots[0] = 0x24; slots[1] = 0x25;
            new Span<ushort>(slots, TakenEquipment.SlotCount).CopyTo(new Span<ushort>(before, TakenEquipment.SlotCount));
            slots[0] = TakenEquipment.Empty;
            new Span<byte>(result, 0x40).Clear();
            result[0x12] = TakenEquipment.Steal;
            var stolen = TakenEquipment.Note(unit, result, before);
            new Span<ushort>(slots, TakenEquipment.SlotCount).CopyTo(new Span<ushort>(before, TakenEquipment.SlotCount));
            slots[1] = TakenEquipment.Empty;
            result[0x12] = 0x04;
            var broken = TakenEquipment.Note(unit, result, before);
            Check($"a steal notes {stolen} (item 0x24 pooled {TakenEquipment.Pooled(0x24)}), a break {broken} (0x25 pooled {TakenEquipment.Pooled(0x25)})",
                  stolen == 1 && TakenEquipment.Pooled(0x24) == 1 && broken == 0 && TakenEquipment.Pooled(0x25) == 0);
            // The battle end: 5 owned becomes 6; one at 99 owned stays 99 (none lost from it either).
            TakenEquipment.Inventory[0x24] = 5;
            var counted = TakenEquipment.Owned(0x24);
            TakenEquipment.Flush();
            var back = TakenEquipment.Inventory[0x24];
            new Span<byte>(result, 0x40).Clear();
            result[0x12] = TakenEquipment.Steal;
            slots[0] = 0x24;
            new Span<ushort>(slots, TakenEquipment.SlotCount).CopyTo(new Span<ushort>(before, TakenEquipment.SlotCount));
            slots[0] = TakenEquipment.Empty;
            TakenEquipment.Note(unit, result, before);
            TakenEquipment.Inventory[0x24] = 99;
            TakenEquipment.Flush();
            var full = TakenEquipment.Inventory[0x24];
            Check($"at the battle end: the game counts {counted} owned, the item comes back ({back}); at 99 it stays {full}; the pool is empty after ({TakenEquipment.Pooled(0x24)})",
                  counted == 5 && back == 6 && full == 99 && TakenEquipment.Pooled(0x24) == 0);
        }
        finally
        {
            saved.CopyTo(new Span<byte>(unit, Battle.UnitSize));
            inventory.CopyTo(new Span<byte>(TakenEquipment.Inventory, TakenEquipment.Items));
            TakenEquipment.Clear();
        }
        return failures;
    }

    /// <summary>
    /// The Move-Find rework on made-up panels (the battle's records and the save's bits put back after), with a stand-in
    /// for the event variable (map 5): a first find, a second in the same battle, and a later battle's.
    /// </summary>
    public static int MoveFindRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: move-find, {name}{(ok ? "" : ": WRONG")}");
        }
        if (MoveFind.Records == null)
        {
            Check("isn't installed", false);
            return failures;
        }
        var records = new Span<byte>(MoveFind.Records, MoveFind.Panels * MoveFind.PanelSize).ToArray();
        var found = new Span<byte>(MoveFind.Found, 0x40).ToArray();
        var variable = MoveFind.Variable;
        var result = stackalloc byte[16];
        var item = stackalloc ushort[1];
        try
        {
            MoveFind.Variable = &FakeMap;
            new Span<byte>(MoveFind.Records, MoveFind.Panels * MoveFind.PanelSize).Clear();
            // Panel 1 at (3, 4), level 0: rare 0x0E0, common 0x0F0. Panel 2 at (6, 1) has no item (flag 0x20).
            var r = MoveFind.Records + MoveFind.PanelSize;
            r[0] = 0x34; r[1] = 0x00; *(ushort*)(r + 2) = 0xE0; *(ushort*)(r + 4) = 0xF0;
            var r2 = MoveFind.Records + 2 * MoveFind.PanelSize;
            r2[0] = 0x61; r2[1] = MoveFind.NoItem;
            MoveFind.Found[2] = 0;   // map 5's bits (low nibble), never found
            MoveFind.Reset();
            string Look()
            {
                new Span<byte>(result, 16).Clear();
                MoveFind.AfterCheck(3, 4, 0, result);
                return (result[0] & 1) != 0 ? $"0x{*(ushort*)(result + 2):X}/0x{*(ushort*)(result + 4):X}" : "nothing";
            }
            string Take(ushort rolled)
            {
                *item = rolled;
                MoveFind.Give(1, item);
                return $"0x{*item:X}";
            }
            var first = Look();
            var firstTake = Take(0xF0);                 // the roll gives the common one
            var second = Look();
            var secondTake = Take(0xF0);                // the common one is taken: the rare one
            var third = Look();
            // A later battle: the save's bit for panel 1 (0x08 >> 1 = 4, map 5 odd: the low nibble) is set by the game.
            MoveFind.Found[2] |= 0x04;
            MoveFind.Reset();
            var later = Look();
            var laterTake = Take(0xE0);                 // the roll gives the rare one, which a found panel has used up
            var laterAgain = Look();
            new Span<byte>(result, 16).Clear();
            result[0] = 1;
            MoveFind.AfterCheck(6, 1, 0, result);
            var trapOnly = result[0];
            Check($"a first battle: {first}, takes {firstTake}; then {second}, takes {secondTake}; then {third}",
                  first == "0xE0/0xF0" && firstTake == "0xF0" && second == "0xE0/0xF0" && secondTake == "0xE0" && third == "nothing");
            Check($"a later battle: {later}, a rare roll takes {laterTake}; then {laterAgain}; a panel with no item is left alone ({trapOnly})",
                  later == "0xE0/0xF0" && laterTake == "0xF0" && laterAgain == "nothing" && trapOnly == 1);
            // ReMixed's story lock: map 5 waits on story progress 0x1F (FakeMap's var 0x6E is 0), a panel never found.
            MoveFind.Thresholds = new byte[8];
            MoveFind.Thresholds[5] = 0x1F;
            MoveFind.Found[2] = 0;
            *(ushort*)(r + 2) = 0xE0;
            MoveFind.Reset();
            var locked = Look();
            var lockedTake = Take(0xE0);                // a rare roll before the story point: the common one
            var lockedAfter = Look();
            *(ushort*)(r + 2) = 0xE0;
            MoveFind.Variable = &FakeMapLater;          // var 0x6E 0x20: past the point
            MoveFind.Reset();
            var unlocked = Look();
            Check($"the story lock: before its point {locked}, a rare roll takes {lockedTake}, then {lockedAfter}; after it {unlocked}",
                  locked == "0xF0/0xF0" && lockedTake == "0xF0" && lockedAfter == "nothing" && unlocked == "0xE0/0xF0");
        }
        finally
        {
            MoveFind.Thresholds = [];
            MoveFind.Variable = variable;
            records.CopyTo(new Span<byte>(MoveFind.Records, MoveFind.Panels * MoveFind.PanelSize));
            found.CopyTo(new Span<byte>(MoveFind.Found, 0x40));
            MoveFind.Reset();
        }
        return failures;
    }

    /// <summary>
    /// The Nemesis System on unit slots 3–5: remembering a downed enemy, the countdown, and giving the name back (the
    /// game's variable isn't touched: the steps take and return its value). Its hook on the name picker is checked too.
    /// </summary>
    public static int NemesisRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: nemesis, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Nemesis.GetVariable == null)
        {
            Check("isn't installed", false);
            return failures;
        }
        var units = game.TurnCode.Units;
        var saved = new Span<byte>(units, Battle.UnitCount * Battle.UnitSize).ToArray();
        var roll = Nemesis.Roll;
        try
        {
            for (var k = 0; k < Battle.UnitCount; k++)
                units[k * Battle.UnitSize + 1] = 0xFF;
            byte* Unit(int slot, byte sprite, byte gender, byte team, int name)
            {
                var u = units + slot * Battle.UnitSize;
                new Span<byte>(u, Battle.UnitSize).Clear();
                u[0] = sprite; u[1] = (byte)slot; u[6] = gender; u[Nemesis.Team] = team;
                *(int*)(u + Nemesis.NameId) = name;
                return u;
            }
            Nemesis.Roll = range => range == 3 ? 0 : 10;     // a woman over a man; a countdown of (10 + 0x30) & 0xF0 = 3
            var ally = Unit(3, 0x80, 0x80, 0x00, 0x123);
            ally[Battle.UnitStatuses] = 0x20;
            var man = Unit(4, 0x80, 0x80, 0x10, 0x145);
            man[Battle.UnitStatuses] = 0x20;
            var woman = Unit(5, 0x81, 0x40, 0x10, 0x234);
            woman[Battle.UnitStatuses + 1] = 0x80;          // Petrified
            var remembered = Nemesis.Next(0, units);
            Check($"after a battle with a downed man and a petrified woman on the enemy side (and a dead ally): 0x{remembered:X8}",
                  remembered == 0x30400234);
            var down = Nemesis.Next(remembered, units);
            var later = Nemesis.Next(Nemesis.Next(down, units), units);
            Nemesis.Roll = range => 25;                        // (25 + 0x30) & 0xF0 = 4
            var rolled = Nemesis.Next(later, units);
            Check($"the countdown: 0x{remembered >> 28:X} → 0x{down >> 28:X} … 0x{later >> 28:X}, then rolled again to 0x{rolled >> 28:X}",
                  down >> 28 == 2 && later >> 28 == 0 && rolled >> 28 == 4 && (rolled & 0xFFFFFF) == 0x400234);
            for (var k = 3; k <= 5; k++)
                units[k * Battle.UnitSize + 1] = 0xFF;
            var due = 0x00400234;
            var fresh = Unit(6, 0x81, 0x40, 0x10, 0x2AA);
            var given = Nemesis.Give(fresh, due) && *(int*)(fresh + Nemesis.NameId) == 0x234;
            var second = Unit(7, 0x81, 0x40, 0x10, 0x2BB);
            var notTwice = !Nemesis.Give(second, due) && *(int*)(second + Nemesis.NameId) == 0x2BB;
            var manUnit = Unit(8, 0x80, 0x80, 0x10, 0x1CC);
            var otherGender = !Nemesis.Give(manUnit, due);
            var allyUnit = Unit(9, 0x81, 0x40, 0x00, 0x2DD);
            var notAlly = !Nemesis.Give(allyUnit, due);
            var waiting = !Nemesis.Give(Unit(10, 0x81, 0x40, 0x10, 0x2EE), due | 0x30000000);
            var off = !Nemesis.Give(Unit(11, 0x81, 0x40, 0x10, 0x2EF), due | 0x00100000);
            Check($"the name comes back on a generic enemy woman {given}, not twice {notTwice}, not on a man {otherGender}, not on an ally {notAlly}, not before the countdown {waiting}, not when switched off {off}",
                  given && notTwice && otherGender && notAlly && waiting && off);
            // The Nemesis's return (ReMixed's 0x11D): a battle's conditions run it on "flag 0x8F = 0" alone, which
            // RemixedScenarios.StartBattle sets as ReMixed's Preparation Menu does, with a Nemesis stored and not disabled.
            var preset = new byte[RemixedEvents.PresetBytes];
            void PutSet(int k, int at, params ushort[] words)
            {
                BitConverter.TryWriteBytes(preset.AsSpan(2 * k), (ushort)(2 * at));
                for (var i = 0; i < words.Length; i++)
                    BitConverter.TryWriteBytes(preset.AsSpan(RemixedEvents.PresetOffsets + 2 * (at + i)), words[i]);
            }
            PutSet(0, 0, 1, 0x1FD, 1, 0x19, 0x301);   // the opening
            PutSet(1, 5, 0x16, 0x19, 0x301);          // a set of another condition
            var without = !RemixedScenarios.HasStartSet(preset);
            PutSet(2, 8, 1, 0x8F, 0, 0x19, 0x1B5);    // "flag 0x8F = 0 -> the Nemesis's return"
            var with = RemixedScenarios.HasStartSet(preset);
            PutSet(2, 0, 1, 0x1FD, 1, 0x19, 0x301);   // its offset 0: no third set
            var ended = !RemixedScenarios.HasStartSet(preset);
            var stored = RemixedScenarios.NemesisStored(due) && !RemixedScenarios.NemesisStored(0)
                         && !RemixedScenarios.NemesisStored(due | SaveOptions.NemesisOff);
            Check($"the return's set found in a battle's conditions {with}, not without it {without}, not past the sets {ended}; "
                  + $"a Nemesis stored and not disabled {stored}", with && without && ended && stored);
            // Its writes, as 0x11D makes them for unit 3 (RemixedEvents.MapUnitPlaces): the snapshot's current statuses
            // (Protect and Shell) and second copy, +5's copy (Immortal) and the unit's preset reach the unit; the
            // snapshot's list of sprite effects to turn on doesn't.
            var fake = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitCount * Battle.UnitSize);
            var memory = new Ps1Memory(fake, fake, _ => { });
            RemixedEvents.MapUnitPlaces((address, length, place) => memory.Map(address, length, (byte*)place), fake);
            var presetBefore = UnitPresets.Slots[3];
            const uint snapshot = RemixedEvents.Snapshot;
            memory.Write(snapshot + 0x69 + 5 * 3 + 3, 1, 0x30);
            memory.Write(snapshot + 0xD2 + 5 * 3 + 3, 1, 0x08);
            memory.Write(snapshot + 0x387 + 3, 1, memory.Read(snapshot + 0x387 + 3, 1) | 4);
            memory.Write(snapshot + 0x13B + 5 * 3 + 3, 1, 0x30);
            memory.Write(Ps1Memory.Units + 3 * Ps1Memory.UnitSize + RemixedEvents.UnitPresetAt, 1, 0x13);
            var three = fake + 3 * Battle.UnitSize;
            var placed = three[Ps1Memory.UnitField(0x5B)] == 0x30 && three[Ps1Memory.UnitField(0x1BE)] == 0x08
                         && three[5] == 4 && UnitPresets.Slots[3] == 0x13;
            var effects = Enumerable.Range(0, Battle.UnitSize).Count(k => fake[k + 3 * Battle.UnitSize] != 0) == 3;
            UnitPresets.Slots[3] = presetBefore;
            // A roster unit's job levels (RemixedEvents.MapRosterJobLevels): PS1 roster slot 2's +0x64 (Squire high,
            // Chemist low) and +0x6D (the last pair) land at the remaster's +0x74 and +0x7D, its learned abilities kept.
            var roster = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(20 * UnitTables.RosterSize);
            var rosterMemory = new Ps1Memory(fake, roster, _ => { });
            RemixedEvents.MapRosterJobLevels((address, length, place) => rosterMemory.Map(address, length, (byte*)place), roster);
            var slot2 = Ps1Memory.Roster + 2 * Ps1Memory.RosterSize;
            rosterMemory.Write(slot2 + 0x64, 1, 0x53);
            rosterMemory.Write(slot2 + 0x6D, 1, 0x21);
            var two = roster + 2 * UnitTables.RosterSize;
            var levels = two[0x74] == 0x53 && two[0x7D] == 0x21 && two[0x6B] == 0 && rosterMemory.Read(slot2 + 0x64, 1) == 0x53;
            System.Runtime.InteropServices.NativeMemory.Free(roster);
            System.Runtime.InteropServices.NativeMemory.Free(fake);
            Check($"a roster unit's job levels (PS1 +0x64-0x6D) at the remaster's +0x74-0x7D, not its learned abilities {levels}",
                  levels);
            Check($"the return's writes reach unit 3 (statuses, their second copy, Immortal, preset 0x13) {placed}; the "
                  + $"snapshot's sprite-effect list stays out of the unit {effects}", placed && effects);
        }
        finally
        {
            Nemesis.Roll = roll;
            saved.CopyTo(new Span<byte>(units, Battle.UnitCount * Battle.UnitSize));
        }
        return failures;
    }

    /// <summary>
    /// The test mode's recorder on a made-up session: a battle with trace lines, the AI taking a player unit, a win, a
    /// menu action, then the files it writes (to a scratch folder) and what they say.
    /// </summary>
    public static int TestModeRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: test mode, {name}{(ok ? "" : ": WRONG")}");
        }
        var folder = Path.Combine(Path.GetTempPath(), "ivalice-remixed-test-mode-" + Environment.ProcessId);
        var units = game.TurnCode.Units;
        var saved = new Span<byte>(units, 2 * Battle.UnitSize).ToArray();
        var variable = MoveFind.Variable;
        try
        {
            Directory.CreateDirectory(folder);
            MoveFind.Variable = &FakeMap;
            TestRecorder.Reset(folder, Options.TestModeRule.RecordAndAutoBattle, ["Riding: a check by hand"]);
            TestRecorder.BattleStarted();
            Tracer.Line(log, "riding: unit 3 uses its mount's skill 0x10D (test mode check)");
            Tracer.Line(log, "riding: unit 3 has acted (test mode check)");
            Tracer.Line(log, "formula 0x11, ability 0x010: test mode check");
            new Span<byte>(units, 2 * Battle.UnitSize).Clear();
            units[1] = 0; units[5] = 0x08;                                  // a player unit the player controls
            units[Battle.UnitSize + 1] = 1; units[Battle.UnitSize + 5] = 0x18; // an enemy
            TestRecorder.AutoBattle(units);
            var handed = units[5] == 0x00 && units[Battle.UnitSize + 5] == 0x18;
            TestRecorder.Error("a made-up error");
            TestRecorder.BattleWon();
            Tracer.Line(log, "unlearn: an ability comes off (test mode check)");
            TestRecorder.Write();
            var results = TestRecorder.Current;
            var battle = results.Runs.LastOrDefault();
            var json = File.ReadAllText(Path.Combine(folder, TestRecorder.ResultsFile));
            var html = File.ReadAllText(Path.Combine(folder, TestRecorder.ReportFile));
            Check($"a battle: won {battle?.Outcome == "won"}, map {battle?.Map}, riding twice {battle?.Topics.GetValueOrDefault("riding")?.Count}, formulas {battle?.Topics.ContainsKey("formulas")}, the error kept {battle?.Errors.Count}; the AI takes the player's unit, not the enemy {handed}",
                  battle != null && battle.Outcome == "won" && battle.Map == 5 && battle.Topics["riding"].Count == 2
                  && battle.Topics.ContainsKey("formulas") && battle.Topics.ContainsKey("test mode") && battle.Errors.Count == 1 && handed);
            Check($"outside battles: the unlearning recorded {results.Outside?.Topics.ContainsKey("unlearn")}",
                  results.Outside?.Topics.ContainsKey("unlearn") == true);
            Check($"the files: TestResults.json ({json.Length} bytes) and TestReport.html ({html.Length} bytes) with the mechanics, the error, the hand check and what hasn't been seen",
                  json.Contains("riding") && html.Contains("Riding") && html.Contains("a made-up error") && html.Contains("Riding: a check by hand")
                  && html.Contains("Not seen yet") && html.Contains("Two Spells"));
            TestRecorder.BattleStarted();
            TestRecorder.BattleStarted();
            Check($"a battle that never reaches the battle end is recorded as such ({TestRecorder.Current.Runs.Last().Outcome})",
                  TestRecorder.Current.Runs.Last().Outcome?.StartsWith("it didn't reach") == true);
        }
        finally
        {
            TestRecorder.Stop();
            MoveFind.Variable = variable;
            saved.CopyTo(new Span<byte>(units, 2 * Battle.UnitSize));
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
        return failures;
    }

    /// <summary>The event script log's stub on a fake loader (returns its id + 1): the ids land in the ring, the
    /// loader still runs with its arguments.</summary>
    public static int EventLogStub(Game game, Log log)
    {
        var fake = GameImage.AllocateCode(32);
        byte[] body = [0x8D, 0x41, 0x01, 0xC3]; // lea eax, [rcx+1]; ret
        body.CopyTo(new Span<byte>(fake, body.Length));
        var ring = (uint*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(65 * sizeof(uint));
        var stub = GameImage.AllocateCode(96);
        IvaliceRemixed.Patches.EventLog.WriteStub(stub, ring, out var slot);
        *(nint*)(stub + slot) = (nint)fake;
        var call = (delegate* unmanaged<int, nint, int>)stub;
        var results = new[] { call(0x1D, 0), call(0x2A0, 0), call(7, 0) };
        uint read = 0;
        var ids = IvaliceRemixed.Patches.EventLog.Take(ring, ref read);
        for (var k = 0; k < 70; k++)
            call(k, 0);
        var wrapped = IvaliceRemixed.Patches.EventLog.Take(ring, ref read);
        System.Runtime.InteropServices.NativeMemory.Free(ring);
        var seen = $"returns {string.Join("/", results)}, ids {string.Join(",", ids.Select(i => $"0x{i:X}"))}, then {wrapped.Count} of 70 (last {wrapped.LastOrDefault()})";
        var at = game.Image.FindUnique(IvaliceRemixed.Patches.EventLog.LoaderPattern) - game.Image.ImageStart;
        seen += $"; the loader found at 0x{at:X}";
        var ok = seen == "returns 30/673/8, ids 0x1D,0x2A0,0x7, then 64 of 70 (last 69); the loader found at 0x2F3BC4";
        log.Info($"self-test: event script log, the loader's stub: {seen}{(ok ? "" : ": WRONG")}");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// ReMixed's event instructions: the two hooks' places, the length stub on a made-up code buffer, and the
    /// interpreter on a small ReMixed program (IF/ELSE nesting, SWITCH, CALL/RETURN, GOTO, a unit's address, PEEK and
    /// POKE through the PS1 map, PATCH_NEXT, HAS_CHARACTER), the remaster's own ZERO run by the test as it would.
    /// </summary>
    /// <summary>The 1-A roster repair (OldGuests.RepairSideChapterRoster), as each world-map frame runs it on a save
    /// from before 0.9.5: Lorne's PS1 Bard job, the three knights' 1-A bit, Agrias off Ashley's sprite set; Ashley, an
    /// empty slot and a second pass left alone.</summary>
    public static int SideChapterRepair(Log log)
    {
        const int size = UnitTables.RosterSize;
        var roster = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(UnitTables.RosterSlots * size);
        void Record(int slot, byte sprites, byte job, int name, byte errand)
        {
            var r = roster + slot * size;
            r[0] = sprites;
            r[1] = (byte)slot;
            r[OldGuests.RosterJob] = job;
            r[OldGuests.RosterErrand] = errand;
            *(int*)(r + OldGuests.RosterName) = name;
        }
        for (var k = 0; k < UnitTables.RosterSlots; k++)
            roster[k * size + 1] = 0xFF;
        Record(1, 0x80, OldGuests.Ps1Bard, OldGuests.Lorne, 0);
        Record(2, 0x81, 0x4C, OldGuests.Lavian, 0x11);
        Record(3, OldGuests.OldAgrias, OldGuests.OldAgrias, OldGuests.Agrias, 0);
        Record(4, OldGuests.OldAgrias, OldGuests.OldAgrias, 0x42, 0); // Ashley
        Record(5, 0x80, 0x4A, 0x123, 0x10); // someone else away
        Record(6, 0x9C, 0x50, OldGuests.Elias, 0x80); // Elias as 0.9.17's 0x0F3 left him
        Record(7, 0x80, 0x4A, 0x124, 0x80); // someone else with the bit
        var first = OldGuests.RepairSideChapterRoster(roster);
        var again = OldGuests.RepairSideChapterRoster(roster);
        byte At(int slot, int offset) => roster[slot * size + offset];
        var ok = first == 4 && again == 0 && At(1, OldGuests.RosterJob) == OldGuests.RemasterBard
                 && At(6, OldGuests.RosterErrand) == 0 && At(7, OldGuests.RosterErrand) == 0x80
                 && At(2, OldGuests.RosterErrand) == 0x01 && At(3, 0) == OldGuests.Agrias
                 && At(3, OldGuests.RosterJob) == OldGuests.Agrias && At(4, 0) == OldGuests.OldAgrias
                 && At(5, OldGuests.RosterErrand) == 0x10;
        // Bryn and Lorne past the river camp: dismissed with their gear back (Bryn's dagger 0x01 one more), not before.
        Record(10, 0xB5, 0x5C, OldGuests.Bryn, 0);
        *(ushort*)(roster + 10 * size + OldGuests.Equipment + 2 * 5) = 0x01;
        *(ushort*)(roster + 10 * size + OldGuests.Equipment + 2 * 1) = 0xFF;
        var inventory = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(OldGuests.Items);
        inventory[0x01] = 3;
        var early = OldGuests.DismissDepartedPerformers(roster, inventory, 6);
        var late = OldGuests.DismissDepartedPerformers(roster, inventory, 7);
        var twice = OldGuests.DismissDepartedPerformers(roster, inventory, 0xE);
        ok &= early == 0 && late == 2 && twice == 0 && At(1, 1) == 0xFF && At(10, 1) == 0xFF && inventory[0x01] == 4
              && inventory[0xFF] == 0 && At(2, 1) != 0xFF;
        System.Runtime.InteropServices.NativeMemory.Free(inventory);
        System.Runtime.InteropServices.NativeMemory.Free(roster);
        log.Info($"self-test: 1-A roster repair, {first} records then {again}: Lorne a Bard, Lavian's 1-A bit off (her own "
                 + $"errand bit kept), Agrias off 0x34, Elias's join bit off, Ashley and others' errands left; Bryn and Lorne dismissed past the river camp ({early}, {late}, {twice}) with their gear back{(ok ? "" : ": WRONG")}");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// DismissUnit by name id where no sprite set matched (<see cref="DismissByName"/>), through its stub's probe: the
    /// remaster's find by sprite set stands, then the name id from slot 1, empty slots skipped.
    /// </summary>
    public static int DismissByNameRule(Game game, Log log)
    {
        if (DismissByName.Probe == 0)
        {
            log.Info("self-test: DismissUnit by name, isn't installed: WRONG");
            return 1;
        }
        const int size = UnitTables.RosterSize;
        var roster = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(UnitTables.RosterSlots * size);
        for (var k = 0; k < UnitTables.RosterSlots; k++)
            roster[k * size + 1] = 0xFF;
        void Record(int slot, byte sprites, int name)
        {
            roster[slot * size] = sprites;
            roster[slot * size + 1] = (byte)slot;
            *(int*)(roster + slot * size + DismissByName.RosterName) = name;
        }
        Record(0, 0x01, 0x6B);                          // Ramza's slot, never dismissed (the remaster starts at 1)
        Record(4, 0xB5, 0x69);                          // Bryn
        Record(9, 0xB4, 0x6A);                          // Lorne
        Record(12, 0x9C, 0x6B);                         // Elias
        roster[20 * size] = 0x80;                       // an empty slot with a matching name: skipped
        *(int*)(roster + 20 * size + DismissByName.RosterName) = 0x78;
        var probe = (delegate* unmanaged<byte*, int, int, int>)DismissByName.Probe;
        int Find(int id, int bySprite = UnitTables.RosterSlots) => probe(roster, id, bySprite);
        var site = (byte*)DismissByName.SiteAt;
        var (bryn, lorne, elias) = (Find(0x69), Find(0x6A), Find(0x6B));
        var ok = bryn == 4 && lorne == 9 && elias == 12 && Find(0x78) == -1 && Find(0x6C) == -1
                 && Find(0x69, 7) == 7 && site[0] == 0xE9;
        System.Runtime.InteropServices.NativeMemory.Free(roster);
        log.Info($"self-test: DismissUnit by name, Bryn 0x69 → slot {bryn}, Lorne → {lorne}, Elias → {elias} "
                 + $"(not Ramza's slot 0), none for an empty slot's name; a sprite set's find stands; its test jumps "
                 + $"away ({site[0]:X2}){(ok ? "" : ": WRONG")}");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// A unit keeps the graphic a ReMixed event wrote, where the remaster's recompute (0x0FCAB0) would work it out again
    /// from the sprite set; and a PS1-memory write over a unit's +0x15F marks it.
    /// </summary>
    public static int UnitLooksRule(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: unit looks, {name}{(ok ? "" : ": WRONG")}");
        }
        if (UnitLooks.RecomputeAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var unit = game.TurnCode.Units + 5 * Battle.UnitSize;
        var saved = new byte[Battle.UnitSize];
        new ReadOnlySpan<byte>(unit, Battle.UnitSize).CopyTo(saved);
        var recompute = (delegate* unmanaged<int, byte*, byte*, void>)UnitLooks.RecomputeAt;
        byte graphic = 0, palette = 0;
        unit[0] = 1;
        unit[3] = 0;
        unit[UnitLooks.GraphicAt] = 0x73;
        unit[UnitLooks.PaletteAt] = 0;
        unit[UnitLooks.BattleIdAt] = 0x95;
        UnitLooks.Clear();
        UnitLooks.Mark(5);
        recompute(5, &graphic, &palette);
        Check($"a graphic an event wrote (0x73, a female Thief) stays, the sprite set Ramza's: {graphic:X2}, +0x18F {unit[UnitLooks.GraphicAt]:X2}",
              graphic == 0x73 && unit[UnitLooks.GraphicAt] == 0x73);
        unit[UnitLooks.BattleIdAt] = 0x96; // the slot set up anew
        recompute(5, &graphic, &palette);
        Check($"once the slot holds another unit, the game's recompute from the sprite set (1, Ramza): {graphic:X2}",
              graphic == 1 && unit[UnitLooks.GraphicAt] == 1);
        unit[UnitLooks.GraphicAt] = 0x73;
        unit[UnitLooks.BattleIdAt] = 0x95;
        recompute(5, &graphic, &palette);
        Check("and an old mark doesn't come back", graphic == 1);
        unit[0] = 0x90;
        unit[6] = 0x80;
        recompute(5, &graphic, &palette);
        Check($"a man's locked Squire look (sprite set 0x90) draws graphic 0x{graphic:X2}", graphic == 0x60);
        unit[0] = 0xB4;
        recompute(5, &graphic, &palette);
        Check($"Lorne's locked look (sprite set 0xB4) draws graphic 0x{graphic:X2}", graphic == 0x38);
        unit[0] = 0xB5;
        unit[6] = 0x40;
        recompute(5, &graphic, &palette);
        Check($"Bryn's locked look (sprite set 0xB5) draws graphic 0x{graphic:X2}", graphic == 0x83);
        unit[0] = 0xA5; // a remaster character of its added scenes, which the game draws as 0xA5
        recompute(5, &graphic, &palette);
        Check($"the remaster's sprite set 0xA5 keeps its own graphic: 0x{graphic:X2}", graphic == 0xA5);

        // The menus' picker (0x278EDC), on a unit as 0x275B84 builds one from a roster record.
        if (UnitLooks.PickerAt == 0)
            Check("the menus' picker isn't installed", false);
        else
        {
            var pick = (delegate* unmanaged<byte*, byte*, byte, byte>)UnitLooks.PickerAt;
            byte Pick(byte sprite, byte gender, byte job)
            {
                unit[0] = sprite;
                unit[6] = gender;
                byte p = 0xEE;
                var g = pick(unit, &p, job);
                return p == 0 ? g : (byte)0xFF;
            }
            Check($"the menus draw Lorne (0xB4, a male Bard) as 0x{Pick(0xB4, 0x80, 0x5B):X2}", Pick(0xB4, 0x80, 0x5B) == 0x38);
            Check($"and Bryn (0xB5, a female Dancer) as 0x{Pick(0xB5, 0x40, 0x5C):X2}", Pick(0xB5, 0x40, 0x5C) == 0x83);
            Check($"a woman's locked Knight look (0x94) as 0x{Pick(0x94, 0x40, 0x4A):X2}", Pick(0x94, 0x40, 0x4A) == 0x65);
            Check($"a generic male Bard as the game draws him: 0x{Pick(0x80, 0x80, 0x5B):X2}", Pick(0x80, 0x80, 0x5B) == 0x82);
            Check($"the remaster's 0xA5, 0xA2 and 0xA4 as it draws them: 0x{Pick(0xA5, 0x80, 0x4A):X2}, 0x{Pick(0xA2, 0x80, 0x4A):X2}, "
                  + $"0x{Pick(0xA4, 0x80, 0x4A):X2}", Pick(0xA5, 0x80, 0x4A) == 0xA5 && Pick(0xA2, 0x80, 0x4A) == 0xA3
                  && Pick(0xA4, 0x80, 0x4A) == 0x72);
            Check($"Agrias (0x1E) as herself: 0x{Pick(0x1E, 0x40, 0x4A):X2}", Pick(0x1E, 0x40, 0x4A) == 0x1E);
            Check($"a man's locked Mime look (0xB6), past Lorne's and Bryn's, as 0x{Pick(0xB6, 0x80, 0x4A):X2}; a locked "
                  + $"Ninja woman's (0xAE) as 0x{Pick(0xAE, 0x40, 0x4A):X2}", Pick(0xB6, 0x80, 0x4A) == 0x84 && Pick(0xAE, 0x40, 0x4A) == 0x7F);
        }
        // The job list's base job (0x2864AC), through its stub's probe: a locked look starts at the Squire like a generic.
        if (UnitLooks.JobListProbe == 0)
            Check("the job list isn't patched", false);
        else
        {
            var probe = (delegate* unmanaged<long, long, byte*, int, int>)UnitLooks.JobListProbe;
            var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
            int Base(int sprite, short job)
            {
                *(short*)record = job;
                return probe(0, 0, record, sprite);
            }
            var site = (byte*)UnitLooks.JobListAt;
            Check($"the job list's base job: Lorne 0x{Base(0xB4, 0x5B):X}, Bryn 0x{Base(0xB5, 0x5C):X}, a locked Knight "
                  + $"0x{Base(0x94, 0x4C):X}, a generic 0x{Base(0x80, 0x50):X}, a monster 0x{Base(0x82, 0x5E):X}, Agrias "
                  + $"0x{Base(0x1E, 0x1E):X}, the remaster's 0xA5 0x{Base(0xA5, 0x4A):X}; its case jumps away ({site[0]:X2} {site[1]:X2})",
                  Base(0xB4, 0x5B) == 0x4A && Base(0xB5, 0x5C) == 0x4A && Base(0x94, 0x4C) == 0x4A && Base(0x80, 0x50) == 0x4A
                  && Base(0x82, 0x5E) == 0x5E && Base(0x1E, 0x1E) == 0x1E && Base(0xA5, 0x4A) == 0xA5
                  && site[0] == 0x48 && site[1] == 0xB8);
            System.Runtime.InteropServices.NativeMemory.Free(record);
        }
        saved.CopyTo(new Span<byte>(unit, Battle.UnitSize));
        UnitLooks.Clear();

        var fake = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitCount * Battle.UnitSize);
        var memory = new Ps1Memory(fake, fake, _ => { });
        var marked = new List<uint>();
        memory.GraphicWritten = slot => marked.Add(slot);
        memory.Write(Ps1Memory.Units + 5 * Ps1Memory.UnitSize + 0x15F, 2, 0x0073);
        memory.Write(Ps1Memory.Units + 6 * Ps1Memory.UnitSize + 0x15E, 2, 0x7300);
        memory.Write(Ps1Memory.Units + 7 * Ps1Memory.UnitSize + 0x160, 1, 0);
        memory.Write(Ps1Memory.Units + 8 * Ps1Memory.UnitSize, 1, 0x73);
        Check($"a write over a unit's graphic marks it (a halfword at +0x15F, one at +0x15E), not one beside it: {string.Join(" ", marked)}",
              marked.SequenceEqual(new uint[] { 5, 6 }));
        System.Runtime.InteropServices.NativeMemory.Free(fake);
        return failures;
    }

    /// <summary>
    /// ReMixed's unlocked-jobs rule for Bryn and Lorne (<see cref="PerformerJobs"/>): the game's own 0x348684, hooked,
    /// on a roster record and a battle unit at job level 1 everywhere, as 0x0ED makes them, and the menu units' step.
    /// </summary>
    public static int PerformerJobRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: performer jobs, {name}{(ok ? "" : ": WRONG")}");
        }
        if (PerformerJobs.UnlockedAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var unlocked = (delegate* unmanaged<byte*, int, byte*, int, int>)PerformerJobs.UnlockedAt;
        var squireChemist = 0x800000 | 0x400000;

        // A roster record (levels +0x74, gender +4, learned +0x32, name id +0x230) in the roster's last slot.
        var record = game.Units.Roster + (UnitTables.RosterSlots - 1) * UnitTables.RosterSize;
        var savedRecord = new byte[UnitTables.RosterSize];
        new ReadOnlySpan<byte>(record, UnitTables.RosterSize).CopyTo(savedRecord);
        int Roster(int gender, int name)
        {
            new Span<byte>(record + 0x74, 12).Fill(0x11);
            new Span<byte>(record + 0x32, 66).Clear();
            record[4] = (byte)gender;
            *(ushort*)(record + 0x230) = (ushort)name;
            return unlocked(record + 0x74, gender, record + 0x32, 0); // a last byte under 0x14: no Mime or Onion Knight check
        }
        Check($"a roster record at job level 1, not a performer: 0x{Roster(0x40, 0x10):X6}", Roster(0x40, 0x10) == squireChemist);
        Check($"Bryn's (0x69, a woman) gets the Dancer: 0x{Roster(0x40, 0x69):X6}", Roster(0x40, 0x69) == (squireChemist | 0x20));
        Check($"Lorne's (0x6A, a man) the Bard: 0x{Roster(0x80, 0x6A):X6}", Roster(0x80, 0x6A) == (squireChemist | 0x40));
        Check($"a monster named so nothing: 0x{Roster(0x20, 0x69):X6}", Roster(0x20, 0x69) == 0);
        savedRecord.CopyTo(new Span<byte>(record, UnitTables.RosterSize));

        // A battle unit (levels +0xE4, gender +6, learned +0xA2, name id +0x1FC), and the same unit off the units' array.
        var unit = game.TurnCode.Units + 6 * Battle.UnitSize;
        var savedUnit = new byte[Battle.UnitSize];
        new ReadOnlySpan<byte>(unit, Battle.UnitSize).CopyTo(savedUnit);
        int Unit(byte* u, int gender, int name)
        {
            new Span<byte>(u + 0xE4, 12).Fill(0x11);
            new Span<byte>(u + 0xA2, 66).Clear();
            u[6] = (byte)gender;
            *(uint*)(u + 0x1FC) = (uint)name;
            return unlocked(u + 0xE4, gender, u + 0xA2, 0);
        }
        Check($"Lorne as a battle unit (as 0x0ED's setup makes him, flagged a woman): 0x{Unit(unit, 0x40, 0x6A):X6}",
              Unit(unit, 0x40, 0x6A) == (squireChemist | 0x40));
        Check($"Bryn as one (flagged a man): 0x{Unit(unit, 0x80, 0x69):X6}", Unit(unit, 0x80, 0x69) == (squireChemist | 0x20));
        var loose = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        Check($"a unit outside the units' array goes the game's way: 0x{Unit(loose, 0x40, 0x69):X6}",
              Unit(loose, 0x40, 0x69) == squireChemist);
        // The menus' step on such a unit (the party menu's on the stack): the menu unit's +0x78, high byte first.
        var menu = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x280);
        menu[0x78] = 0xC0;
        PerformerJobs.AddToMenu(loose, menu);
        Check($"the party menu's unit of Bryn gets the Dancer: {menu[0x78]:X2} {menu[0x79]:X2} {menu[0x7A]:X2}",
              menu[0x78] == 0xC0 && menu[0x79] == 0 && menu[0x7A] == 0x20);
        *(uint*)(loose + 0x1FC) = 0x6A;
        loose[6] = 0x80;
        new Span<byte>(menu + 0x78, 3).Clear();
        PerformerJobs.AddToMenu(loose, menu);
        Check($"Lorne's the Bard: {menu[0x7A]:X2}", menu[0x7A] == 0x40);
        *(uint*)(loose + 0x1FC) = 0x11;
        menu[0x7A] = 0;
        PerformerJobs.AddToMenu(loose, menu);
        Check("anyone else's nothing", menu[0x7A] == 0);
        System.Runtime.InteropServices.NativeMemory.Free(menu);
        System.Runtime.InteropServices.NativeMemory.Free(loose);
        savedUnit.CopyTo(new Span<byte>(unit, Battle.UnitSize));
        var hooked = new[] { PerformerJobs.UnlockedAt, PerformerJobs.PartyMenuUnitAt, PerformerJobs.BattleMenuUnitAt };
        Check($"hooked at 0x{hooked[0] - game.Image.ImageStart:X}, 0x{hooked[1] - game.Image.ImageStart:X} and "
              + $"0x{hooked[2] - game.Image.ImageStart:X}",
              hooked[0] - game.Image.ImageStart == 0x348684 && hooked[1] - game.Image.ImageStart == 0x284A08
              && hooked[2] - game.Image.ImageStart == 0x360048);
        return failures;
    }

    /// <summary>
    /// A ReMixed event's unit number past the groups (0x104 up) names every unit present, as on the PS1: the remaster's
    /// reader (0x240888) and its slot test (0x2408F0, patched by EventUnits) on a made-up list of sprite objects, units 3
    /// and 7 present, neither with a battle unit behind it.
    /// </summary>
    public static int EventUnitNumbers(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: event units by number, {name}{(ok ? "" : ": WRONG")}");
        }
        var matcher = EventUnits.MatcherAt;
        if (matcher == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var jump = (byte*)matcher + EventUnits.JumpAt;
        var landing = jump + 2 + ((sbyte*)jump)[1];
        Check($"mode 5's jne is ja to the return 1 (0x{(nint)jump - game.Image.ImageStart:X}: {jump[0]:X2} {jump[1]:X2}, on to " +
              $"{landing[0]:X2} {landing[1]:X2})", jump[0] == 0x77 && landing[0] == 0xB8 && *(int*)(landing + 1) == 1);
        // The reader, just before the test: 0 is mode 1, under 0x100 a unit's id (looked up), else the number less 0xFE.
        const string readerPattern =
            "48 89 5C 24 08 48 89 6C 24 10 57 48 83 EC 20 33 ED 48 8B DA 48 8B F9 66 39 29 74 30 0F B7 01 B9 00 01 00 00 " +
            "66 39 0F 73 1A 8B C8 E8 ?? ?? ?? ?? B9 D0 07 00 00 66 89 07 89 2B 66 3B C1 75 13 33 C0 EB 14 05 02 FF FF FF " +
            "89 02 EB 06 C7 02 01 00 00 00 B8 01 00 00 00";
        GameImage.Calls(matcher - 0x68, readerPattern, "the event unit number's reader");
        var reader = (delegate* unmanaged<ushort*, int*, int>)(matcher - 0x68);
        var test = (delegate* unmanaged<ushort*, ushort, int*, int>)matcher;
        // The sprite objects' list the test looks a unit up in (0x260A98: +0 the next, +8 the unit's id), its head
        // read rip-relative.
        var lookup = (byte*)GameImage.Calls(matcher, EventUnits.MatcherPattern, "the event units' test")[0];
        if (lookup[0] != 0x48 || lookup[1] != 0x8B || lookup[2] != 0x15)
        {
            Check("the unit lookup isn't the code this test expects", false);
            return failures;
        }
        var head = (nint*)GameImage.Rip((nint)lookup, 3, 7);
        var objects = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(2 * 0x200);
        *(nint*)objects = (nint)(objects + 0x200);
        objects[8] = 3;
        objects[0x200 + 8] = 7;
        var saved = *head;
        *head = (nint)objects;
        string Matched(ushort number)
        {
            ushort unit = number;
            int mode = 0;
            if (reader(&unit, &mode) == 0)
                return "unread";
            var found = new List<int>();
            for (ushort slot = 0; slot < Battle.UnitCount; slot++)
                if (test(&unit, slot, &mode) != 0)
                    found.Add(slot);
            return found.Count == 0 ? "none" : string.Join(" ", found);
        }
        try
        {
            Check($"0 is every unit present: {Matched(0)}", Matched(0) == "3 7");
            ushort[] past = [0x104, 0x105, 0x202, 0x204, 0x290, 0x301, 0x303, 0xFFFF];
            Check("a number past the groups is every unit present, as on the PS1 (the Treasure Wheel's 0x202 and 0x290; " +
                  $"0x105, 0x204, 0x301 and 0x303 in other events): {string.Join(", ", past.Select(n => $"0x{n:X} {Matched(n)}"))}",
                  past.All(n => Matched(n) == "3 7"));
            Check($"groups 4 and 5 (0x102, 0x103) still ask for a battle unit, which these have none of: {Matched(0x102)}, " +
                  $"{Matched(0x103)}", Matched(0x102) == "none" && Matched(0x103) == "none");
        }
        finally
        {
            *head = saved;
            System.Runtime.InteropServices.NativeMemory.Free(objects);
        }
        return failures;
    }

    public static int RemixedEventScripts(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: remixed events, {name}{(ok ? "" : ": WRONG")}");
        }
        var (fetch, next, length, variables) = EventHooks.Places; // installed before the patches, as the upgrade mod loads first
        var places = $"fetch 0x{fetch:X}, next 0x{next:X}, lengths 0x{length:X}, variables 0x{variables:X}";
        Check($"the hooks' places: {places}", places == "fetch 0x23AFD8, next 0x23B63A, lengths 0x2CBE2C, variables 0x7830D8");
        // PS1 battle unit fields in the remaster's unit, as the job routine (SCUS 0x8005B6xx, 0x276C64) and the unit
        // setup (0x8005DFAC, 0x27xxxx) place them.
        var fields = string.Join(" ", new[] { 0x4B, 0x4E, 0x53, 0x58, 0x6D, 0x71, 0x15C, 0x15E, 0x162, 0x1B8, 0x1B9, 0x163, 0xF8 }
            .Select(o => $"{o:X}:{(Ps1Memory.UnitField(o) is var r and >= 0 ? r.ToString("X") : "-")}"));
        Check($"PS1 unit fields mapped: {fields}",
              fields == "4B:53 4E:57 53:5C 58:61 6D:76 71:7A 15C:18C 15E:18E 162:192 1B8:1EC 1B9:1ED 163:194 F8:10C");
        var musicAt = EventHooks.Music(game.Image);
        var musicPlaces = $"0x{musicAt.Fd4 - game.Image.ImageStart:X} 0x{musicAt.Fd6 - game.Image.ImageStart:X} "
                          + $"0x{musicAt.Fd8 - game.Image.ImageStart:X}, 0x{musicAt.Play - game.Image.ImageStart:X}";
        Check($"0x14's music change: the PS1's 0x80165FD4, D6, D8 at {musicPlaces}", musicPlaces == "0xD4091C 0xD408FC 0xD40900, 0x234990");
        var buttonsAt = EventHooks.ButtonWords(game.Image);
        var buttons = $"0x{buttonsAt.Held - game.Image.ImageStart:X} 0x{buttonsAt.Before - game.Image.ImageStart:X} 0x{buttonsAt.Pressed - game.Image.ImageStart:X}";
        Check($"the buttons (PS1 0x80045944, 48, 4C): {buttons}", buttons == "0x1876EEC 0x187A3F4 0x1876EF0");
        var frameAt = game.Image.FindUnique(Ps1Pad.BattleFramePattern) - game.Image.ImageStart;
        var padAt = GameImage.Rip(game.Image.FindUnique(Ps1Pad.BattleFramePattern) + 11, 3, 7) - game.Image.ImageStart;
        var testAt = game.Image.FindUnique(Ps1Pad.ActionTestPattern) - game.Image.ImageStart;
        var (uiCheck, inputState) = Ps1Pad.Places;
        Check($"ReMixed's buttons made from the remaster's input: the battle frame 0x{frameAt:X} (the first pad's record at " +
              $"[0x{padAt:X}]), the action test 0x{testAt:X} and from it the UI's action check " +
              $"0x{uiCheck - game.Image.ImageStart:X} on [0x{inputState - game.Image.ImageStart:X}], {Ps1Pad.Actions.Length} buttons",
              frameAt == 0x37FB7C && padAt == 0x2E942E8 && testAt == 0xFC598 && uiCheck - game.Image.ImageStart == 0xF0D00
              && inputState - game.Image.ImageStart == 0x3CD9EE8 && Ps1Pad.Actions.Length == 12);
        var (setsAt, recordsAt) = EventTables.ConditionTables(game.Image);
        Check($"0xCE's tables: the condition sets at 0x{setsAt - game.Image.ImageStart:X}, the records at 0x{recordsAt - game.Image.ImageStart:X}",
              setsAt - game.Image.ImageStart == 0xD40040 && recordsAt - game.Image.ImageStart == 0xD3F550);
        Check($"AFTER_BATTLE_RETURN's live write-back routine 0x{RemixedEvents.LiveBattleWriteBack:X} is registered",
              EventHooks.Script?.Natives.ContainsKey(RemixedEvents.LiveBattleWriteBack) == true
              && BattleEnd.WriteBackAt != 0);
        var rosterFields = string.Join(" ", new[] { 0x0D, 0x0E, 0x14, 0x15, 0x16, 0x17, 0x19, 0x2B, 0x63, 0x64, 0x66, 0x67, 0x6D, 0x6E, 0x7A,
                                                     0x95, 0x96, 0xBD, 0xBE, 0xD0 }
            .Select(o => $"{o:X}:{(Ps1Memory.RosterField(o) is var r and >= 0 ? r.ToString("X") : "-")}"));
        var unitEquipment = $"{Ps1Memory.UnitField(0x1A):X} {Ps1Memory.UnitField(0x20):X} {Ps1Memory.UnitField(0x21):X}";
        Check($"PS1 roster fields mapped (the remaster's equipment is halfwords: +7 from EXP): {rosterFields}; a battle unit's equipment {unitEquipment}",
              rosterFields == "D:D E:E 14:1A 15:1C 16:1D 17:1E 19:20 2B:32 63:6A 64:74 66:76 67:77 6D:7D 6E:80 7A:8C 95:A7 96:AE BD:D5 BE:- D0:11E"
              && unitEquipment == "1A 26 28");
        var settingsAt = game.Image.FindUnique(EventTables.MapSettingsPattern);
        Check($"the per-event map settings (0xCD's table) at 0x{*(int*)(settingsAt + 21):X}", *(int*)(settingsAt + 21) == 0xD3F950);

        // The length stub: ReMixed's lengths inside marked code, the remaster's path elsewhere.
        var cell = (byte**)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
        var table = (byte*)System.Runtime.InteropServices.NativeMemory.Alloc(256);
        EventScript.Lengths.CopyTo(new Span<byte>(table, 256));
        var pads = GameImage.AllocateCode(32);
        byte[] padBytes = [0xC3, 0x90, 0x90, 0x90, 0xB8, 0x77, 0x00, 0x00, 0x00, 0xC3]; // done: ret; table0: mov eax, 0x77; ret
        padBytes.CopyTo(new Span<byte>(pads, padBytes.Length));
        var stub = GameImage.AllocateCode(256);
        var bytes = EventHooks.LengthStub(cell, table, (nint)(pads + 4), (nint)pads);
        bytes.CopyTo(new Span<byte>(stub, bytes.Length));
        var len = (delegate* unmanaged<byte*, int>)stub;
        var marked = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(EventHooks.Size + 16);
        *(int*)marked = 0x123; // the size the remaster puts there
        EventHooks.Magic.CopyTo(new Span<byte>(marked + 4, 4));
        marked[8] = 0xC2; marked[11] = 0xE6; marked[12] = 0x05; marked[14] = 0xE0;
        var other = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(16);
        other[0] = 0xE6; other[1] = 0x05; other[2] = 0xC2;
        *cell = marked;
        var lengths = $"{len(marked + 8)} {len(marked + 11)} {len(marked + 14)} | {len(other)} {len(other + 2)}";
        *cell = other;
        lengths += $" | {len(other)} {len(other + 2)}";
        *cell = null;
        lengths += $" | {len(other + 2)}";
        Check($"the length stub: ReMixed's 0xC2, 0xE6, 0xE0 in marked code, the remaster's elsewhere: {lengths}",
              lengths == "3 3 13 | 7 119 | 7 119 | 119");

        // The interpreter.
        byte[] program =
        [
            0x0E, 0x2C, 0x0E, 0x3C,
            0xCF, 0x00, 0x10, 0x15, 0x80,              // NATIVE_CALL 0x80151000 (ported)
            0xCF, 0x00, 0x06, 0x16, 0x80,              // NATIVE_CALL 0x80160600 (not yet)
            0xAE, 0x00, 0x00, 0x05, 0x00,              // SET var 0 = 5
            0x01, 0xAE, 0x10, 0x00, 0x01, 0x00,        // IF: var 0x10 = 1
            0x03, 0xAE, 0x10, 0x00, 0x02, 0x00, 0x04,  // ELSE: var 0x10 = 2; ENDIF
            0xBE, 0x00, 0x00,                          // ZERO var 0 (the remaster's)
            0x01, 0x01, 0xAE, 0x11, 0x00, 0x09, 0x00, 0x04, 0xAE, 0x11, 0x00, 0x08, 0x00, // IF { IF { 9 } 8 }
            0x03, 0xAE, 0x11, 0x00, 0x03, 0x00, 0x04,  // ELSE { var 0x11 = 3 }
            0xAE, 0x05, 0x00, 0x07, 0x00,              // SET var 5 = 7
            0x05, 0x05,                                // SWITCH var 5
            0x06, 0x01, 0x00, 0xAE, 0x12, 0x00, 0x01, 0x00, 0x08,
            0x06, 0x07, 0x00, 0xAE, 0x12, 0x00, 0x07, 0x00, 0x08,
            0x07, 0xAE, 0x12, 0x00, 0x09, 0x00, 0x09,  // DEFAULT; ENDSWITCH
            0x0C, 0x20,                                // CALL 0x20
            0x0D, 0x30,                                // GOTO 0x30
            0xAE, 0x13, 0x00, 0x63, 0x00,              // (passed over)
            0x0A, 0x20, 0xAE, 0x14, 0x00, 0x04, 0x00, 0x0B, // FUNCTION 0x20 { var 0x14 = 4 } RETURN
            0x0E, 0x30,                                // LABEL 0x30
            0xC2, 0x85, 0x70,                          // var 0x70 = the unit with event id 0x85
            0xAE, 0x71, 0x00, 0x2A, 0x00,              // var 0x71 = 0x2A
            0xC4, 0x71, 0x47, 0x00, 0x70, 0x01,        // POKE its X (PS1 +0x47)
            0xC1, 0x15, 0x24, 0x00, 0x70, 0x01,        // PEEK its Brave (PS1 +0x24)
            0xAE, 0x72, 0x00, 0x34, 0x12,              // var 0x72 = 0x1234
            0xE6, 0x42, 0x72,                          // PATCH_NEXT: 2 bytes at parameter 2
            0xAE, 0x16, 0x00, 0x00, 0x00,              // becomes var 0x16 = 0x1234
            0xC1, 0x17, 0x40, 0x01, 0x70, 0x01,        // PEEK PS1 +0x140: not mapped
            0xAC, 0x18, 0x00, 0x01, 0x00,              // SETHI var 0x18 = 0x10000
            0xAC, 0x1A, 0x00, 0x19, 0x80,              // var 0x1A = 0x80190000
            0xC4, 0x72, 0x44, 0x38, 0x1A, 0x02,        // POKE 0x1234 at PS1 0x80193844 (a mapped global)
            0xC4, 0x72, 0x50, 0xFD, 0x1A, 0x02,        // POKE 0x1234 at PS1 0x8019FD50 (the mod's own store)
            0xC1, 0x1B, 0x50, 0xFD, 0x1A, 0x02,        // PEEK it back
            0xAE, 0x73, 0x00, 0x00, 0x00,              // var 0x73 = 0
            0xC4, 0x73, 0xBA, 0x01, 0x70, 0x01,        // POKE 0 at its base team (PS1 +0x1BA): not in the remaster
            0xC1, 0x1C, 0xBA, 0x01, 0x70, 0x01,        // PEEK the team the remaster has
            0xC7, 0x80, 0x03, 0x00,                    // HAS_CHARACTER sprite set 3
            0xAF, 0x19, 0x00, 0x00, 0x00,              // var 0x19 = var 0
            0xAE, 0x27, 0x00, 0x92, 0x00,              // var 0x27 (the event's number) = 0x92
            0xCD, 0x83, 0x05, 0x02,                    // MAP_SETTINGS: event 0x92's word = (2 << 10) + 0x8305
            0x9A,                                      // LIST_RESET
            0x9B, 0x34, 0x12,                          // word 0 = 0x1234
            0x9C, 0x15,                                // word 1 = var 0x15 (the Brave read above, 0x46)
            0x9D, 0x10, 0x78, 0x56,                    // word 0x10 = 0x5678
            0x9E, 0x11, 0x72,                          // word 0x11 = var 0x72 (0x1234)
            0x14, 0x2A,                                // MUSIC 0x2A
            0x8D, 0x28, 0x05, 0x1D,                    // var 0x1D = item 5 owned and equipped (3 + 2)
            0x8D, 0x26, 0x05, 0x63,                    // item 5 + 99: capped at 99 with the 2 equipped, so 97 owned
            0x8D, 0x17, 0x07, 0x09,                    // item 7 - 9: 4 owned, so 0
            0x8D, 0x22, 0x05, 0x63,                    // var 0 = 97 + 2 >= 99
            0x8D, 0x30, 0x05, 0x00,                    // the Poachers' Den: not ported (noted)
            0xCE, 0x01, 0x00,                          // CONDITIONS of event 1
            0xDB,
        ];
        var code = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(EventHooks.Size);
        *(int*)code = program.Length + 4;
        program.CopyTo(new Span<byte>(code + EventScript.Start, program.Length));
        var units = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitCount * Battle.UnitSize);
        var roster = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(UnitTables.RosterSlots * UnitTables.RosterSize);
        for (var k = 0; k < Battle.UnitCount; k++)
            units[k * Battle.UnitSize + 1] = 0xFF;
        var unit3 = units + 3 * Battle.UnitSize;
        unit3[1] = 3; unit3[0x191] = 0x85; unit3[Battle.UnitBrave] = 70; unit3[Battle.UnitTeam] = 0x30;
        for (var k = 0; k < UnitTables.RosterSlots; k++)
            roster[k * UnitTables.RosterSize + 1] = 0xFF;
        roster[0] = 3; roster[1] = 0;
        var vars = new Dictionary<int, int>();
        var notes = new List<string>();
        var sum = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(4);
        var settings = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Ps1Memory.MapSettingsBytes);
        var words = (int*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Ps1Memory.ExtraWordsBytes);
        var conditionSets = (short*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Ps1Memory.ConditionSetsBytes);
        var conditions = (short*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Ps1Memory.ConditionsBytes);
        var script = new EventScript(id => vars.GetValueOrDefault(id), (id, v) => vars[id] = v,
                                       new Ps1Memory(units, roster, notes.Add).Map(0x80193844, 2, sum)
                                           .Map(Ps1Memory.MapSettings, Ps1Memory.MapSettingsBytes, settings)
                                           .Map(Ps1Memory.ExtraWords, Ps1Memory.ExtraWordsBytes, (byte*)words)
                                           .Map(Ps1Memory.ConditionSets, Ps1Memory.ConditionSetsBytes, (byte*)conditionSets)
                                           .Map(Ps1Memory.Conditions, Ps1Memory.ConditionsBytes, (byte*)conditions), notes.Add);
        var presets = new byte[2 * EventScript.PresetBytes];
        BitConverter.GetBytes((short)0).CopyTo(presets, EventScript.PresetBytes);              // event 1: set 0 at 0,
        BitConverter.GetBytes((short)0x0C).CopyTo(presets, EventScript.PresetBytes + 2);       // set 1 at 0x0C
        BitConverter.GetBytes((short)0x19).CopyTo(presets, EventScript.PresetBytes + 0x20);    // a record: type 0x19
        BitConverter.GetBytes((short)0x7F).CopyTo(presets, EventScript.PresetBytes + 0x21E);   // the last halfword
        script.Presets = presets;
        var natives = 0;
        script.Natives[RemixedEvents.LevelScalingRerun] = () => natives++;
        var music = -1;
        script.PlayMusic = track => music = track;
        var owned = new Dictionary<int, int> { [5] = 3, [7] = 4 };
        var equipped = new Dictionary<int, int> { [5] = 2 };
        script.Items = (item => owned.GetValueOrDefault(item), (item, n) => owned[item] = n,
                        item => owned.GetValueOrDefault(item) + equipped.GetValueOrDefault(item));
        var pc = EventScript.Start;
        var steps = 0;
        while (code[pc] != 0xDB && steps++ < 1000)
        {
            var to = script.Step(code, pc, EventHooks.Size);
            if (to < 0)
            {
                if (code[pc] == 0xBE)
                    vars[code[pc + 1] | code[pc + 2] << 8] = 0;
                to = EventScript.Next(code, pc);
            }
            pc = to;
        }
        string V(int id) => vars.TryGetValue(id, out var v) ? $"0x{v:X}" : "-";
        var seen = $"0x10 {V(0x10)}, 0x11 {V(0x11)}, 0x12 {V(0x12)}, 0x13 {V(0x13)}, 0x14 {V(0x14)}, unit {V(0x70)}, "
                   + $"X {unit3[Battle.UnitX]}, Brave {V(0x15)}, patched {V(0x16)}, unmapped {V(0x17)}, high {V(0x18)}, "
                   + $"has {V(0x19)}, global 0x{*(ushort*)sum:X}, own {V(0x1B)}, team 0x{unit3[Battle.UnitTeam]:X} read {V(0x1C)}, "
                   + $"map settings 0x{*(ushort*)(settings + 2 * 0x92):X}, list 0x{words[0]:X} 0x{words[1]:X} 0x{words[2]:X} 0x{words[0x10]:X} 0x{words[0x11]:X}, "
                   + $"music 0x{music:X}, items {V(0x1D)} {owned[5]} {owned[7]} {V(0)}, conditions {conditionSets[1]:X} {conditions[0]:X} {conditions[0xFF]:X}, "
                   + $"native {natives}, {notes.Count} note(s), ended {code[pc] == 0xDB}";
        Check($"a ReMixed program: {seen}", seen == "0x10 0x1, 0x11 0x3, 0x12 0x7, 0x13 -, 0x14 0x4, unit 0x80190E0C, "
                                                   + "X 42, Brave 0x46, patched 0x1234, unmapped 0x0, high 0x10000, has 0x1, global 0x1234, own 0x1234, team 0x30 read 0x30, "
                                                   + "map settings 0x8B05, list 0x1234 0x46 0x0 0x5678 0x1234, music 0x2A, items 0x5 97 0 0x1, conditions C 19 7F, native 1, 6 note(s), ended True");
        // 0x2F (a parallel block's loop) and 0xE8/0xE9 (a map tile's byte): 2A; F1 01 00; 2F 00 70 00 00; 2F 03 71 00 00;
        // E8 05 02 01 0F 00; E9 06 03 01 0F 00; 2B; DB.
        byte[] looping = [0x2A, 0xF1, 0x01, 0x00, 0x2F, 0x00, 0x70, 0x00, 0x00, 0x2F, 0x03, 0x71, 0x00, 0x00,
                          0xE8, 0x05, 0x02, 0x01, 0x0F, 0x00, 0xE9, 0x06, 0x03, 0x01, 0x0F, 0x00, 0x2B, 0xDB];
        new Span<byte>(code, EventHooks.Size).Clear();
        looping.CopyTo(new Span<byte>(code + EventScript.Start, looping.Length));
        int body = EventScript.Start + 1, loop = EventScript.Start + 4, countdown = EventScript.Start + 9;
        vars[0x70] = 1;
        var back = script.Step(code, loop, EventHooks.Size);
        vars[0x70] = 0;
        var through = script.Step(code, loop, EventHooks.Size);
        var counted = new List<int>();
        for (var k = 0; k < 4; k++)
            counted.Add(script.Step(code, countdown, EventHooks.Size));
        var tile = stackalloc byte[8];
        tile[2] = 0x2C;
        script.TileAt = (x, y, level) => x == 1 && y == 15 && level == 0 ? (nint)tile : 0;
        vars[6] = 0x77;
        script.Step(code, EventScript.Start + 14, EventHooks.Size);
        script.Step(code, EventScript.Start + 20, EventHooks.Size);
        Check($"0x2F: var set, back to {back} (the block at {body}); var 0, on to {through}; counting down from 3: "
              + $"{string.Join(" ", counted)}, var 0x{vars[0x71]:X}; 0xE8: var 5 0x{vars[5]:X}; 0xE9: the tile's byte 3 0x{tile[3]:X}",
              back == body && through == loop + 5 && string.Join(" ", counted) == $"{body} {body} {countdown + 5} {body}"
              && vars[5] == 0x2C && tile[3] == 0x77);
        System.Runtime.InteropServices.NativeMemory.Free(sum);
        System.Runtime.InteropServices.NativeMemory.Free(settings);
        System.Runtime.InteropServices.NativeMemory.Free(words);
        System.Runtime.InteropServices.NativeMemory.Free(conditionSets);
        System.Runtime.InteropServices.NativeMemory.Free(conditions);

        // ReMixed's words through the hooked variable executor, with a stand-in for the game's.
        Check($"the battle event variables' executor (0x{EventHooks.ArithmeticAt - game.Image.ImageStart:X}) is hooked",
              EventHooks.ArithmeticAt - game.Image.ImageStart == 0x242700 && ((byte*)EventHooks.ArithmeticAt)[0] is 0xE9 or 0xFF);
        var realArithmetic = EventHooks.GameArithmetic;
        EventHooks.GameArithmetic = &FakeArithmetic;
        var savedWords = new Span<byte>(EventHooks.Words, Ps1Memory.ExtraWordsBytes).ToArray();
        var run = (delegate* unmanaged<uint, uint, uint, nint, nint, void>)EventHooks.ArithmeticAt;
        _arithmetic.Clear();
        run(0xBE, 0xB05, 0, 0, 0);    // the setter: ZERO, then ADD the value
        run(0xB0, 0xB05, 7, 0, 0);
        run(0xB4, 0xB05, 6, 0, 0);    // MUL 6: 42
        run(0xB3, 0xB06, 0xB05, 0, 0); // SUB_VAR: word 6 = 0 - 42
        run(0xB1, 0x10, 0xB05, 0, 0); // ADD_VAR into an ordinary variable: the game adds 42 as a literal
        run(0xB0, 0x11, 3, 0, 0);     // nothing of ReMixed's: the game's own
        var arithmetic = $"0x{EventHooks.Words[5]:X} {EventHooks.Words[6]}, the game got {string.Join(" ", _arithmetic)}";
        new Span<byte>(savedWords).CopyTo(new Span<byte>(EventHooks.Words, Ps1Memory.ExtraWordsBytes));
        EventHooks.GameArithmetic = realArithmetic;
        Check($"ReMixed's words 0xB00 on in the mod's store, others the game's: {arithmetic}",
              arithmetic == "0x2A -42, the game got B0:10:2A B0:11:3");
        var apply = string.Join(" ", new uint[] { 0xB6, 0xB8, 0xBA, 0xBC }.Select(op => EventHooks.Apply(op, 45, 6)));
        Check($"the arithmetic: 45 / 6, % 6, & 6, | 6 = {apply}; / 0 leaves it ({EventHooks.Apply(0xB6, 45, 0)})",
              apply == "7 3 4 47" && EventHooks.Apply(0xB6, 45, 0) == 45);
        // A battle unit's name in the PS1's layout, as Be A Beta writes its testers' (0x0C6: three words at +0x12C), comes
        // out in the remaster's UTF-8 at +0x14C; read back as written.
        var named = new Ps1Memory(units, roster, _ => { });
        var nameAt = Ps1Memory.Units + 3 * Ps1Memory.UnitSize + Ps1Memory.UnitNameAt;
        named.Write(nameAt, 4, 0x3230350A);
        named.Write(nameAt + 4, 4, 0x14272835);
        named.Write(nameAt + 8, 4, unchecked((int)0xFE2C3532));
        var unitName = System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)(unit3 + Ps1Memory.RemasterUnitNameAt));
        Check($"a battle unit's name written as the PS1 has it: \"{unitName}\" in the remaster's, read back "
              + $"0x{named.Read(nameAt, 4):X8}", unitName == "ArmoredKori" && named.Read(nameAt, 4) == 0x3230350A);
        // The Treasure Wheel's JP cash-in (0x0C3) adds to a roster unit's JP for its job (PS1 +0x6E + 2 x index) and its
        // total (+0x96 + 2 x index) with no cap: the remaster's +0x80 and +0xAE, stopped at 9999.
        var jpAt = Ps1Memory.Roster + 2 * Ps1Memory.RosterSize + Ps1Memory.JpAt + 6;
        named.Write(jpAt, 2, 9990 + 50);
        named.Write(jpAt + (Ps1Memory.TotalJpAt - Ps1Memory.JpAt), 2, 1234);
        var slot2 = roster + 2 * UnitTables.RosterSize;
        var jp = $"{*(ushort*)(slot2 + Ps1Memory.RemasterJpAt + 6)} {*(ushort*)(slot2 + Ps1Memory.RemasterTotalJpAt + 6)}";
        Check($"a roster unit's JP for job index 3 written as the PS1 has it (10040, then a total of 1234): {jp} in the "
              + $"remaster's, read back {named.Read(jpAt, 2)}", jp == "9999 1234" && named.Read(jpAt, 2) == 9999);
        foreach (var p in new nint[] { (nint)cell, (nint)table, (nint)marked, (nint)other, (nint)code, (nint)units, (nint)roster })
            System.Runtime.InteropServices.NativeMemory.Free((void*)p);
        return failures;
    }

    private static readonly List<string> _arithmetic = [];

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeArithmetic(uint op, uint dest, uint source, nint r9, nint flag) =>
        _arithmetic.Add($"{op:X}:{dest:X}:{source:X}");

    private static int _recalculated;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeRecalculate(byte* unit, int zero)
    {
        _recalculated++;
        *(ushort*)(unit + Battle.UnitMaxHp) = (ushort)(unit[LevelScaling.RawStats] + 100);
        *(ushort*)(unit + Battle.UnitMaxMp) = 40;
    }

    /// <summary>
    /// Improved Level Scaling's default: the levels a scaling unit gets from the deployed units, and the rebuild on a
    /// made-up enemy (the game's base stat roll and level-ups, a stand-in for the recalculation).
    /// </summary>
    public static int DeployedLevels(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: deployed levels, {name}{(ok ? "" : ": WRONG")}");
        }
        int Low(int n) => 0;
        int High(int n) => n - 1;
        var levels = $"{LevelScaling.LevelFor(0xFE, 40, Low)}-{LevelScaling.LevelFor(0xFE, 40, High)}, "
                     + $"{LevelScaling.LevelFor(0, 8, Low)}, {LevelScaling.LevelFor(105, 40, Low)}, {LevelScaling.LevelFor(105, 97, Low)}, "
                     + $"{LevelScaling.LevelFor(30, 40, Low)}";
        Check($"scaling from P 40 gives 35-40, from 8 gives 7, 105 gives P + 5 (45, capped at 99), a fixed 30 stays: {levels}",
              levels == "35-40, 7, 45, 99, 30");
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var real = LevelScaling.Recalculate;
        LevelScaling.Recalculate = &FakeRecalculate;
        int Hp(int level)
        {
            unit[2] = 0xFF;      // not from the roster
            unit[6] = 0x80;      // a man
            for (var k = 0x8A; k <= 0x93; k++)
                unit[k] = 12;    // growths
            LevelScaling.Rebuild(unit, level);
            return unit[LevelScaling.RawStats] | unit[LevelScaling.RawStats + 1] << 8 | unit[LevelScaling.RawStats + 2] << 16;
        }
        var low = Enumerable.Range(0, 4).Average(_ => Hp(5));
        var high = Enumerable.Range(0, 4).Average(_ => Hp(60));
        var after = $"level {unit[Battle.UnitLv]}, marker 0x{unit[2]:X2}, recalculated {_recalculated}, "
                    + $"HP full {*(ushort*)(unit + Battle.UnitHp) == *(ushort*)(unit + Battle.UnitMaxHp)}, MP {*(ushort*)(unit + Battle.UnitMp)}";
        LevelScaling.Recalculate = real;
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        Check($"the rebuild: raw HP {low:0} at level 5, {high:0} at 60; then {after}",
              high > low * 1.5 && after == "level 60, marker 0xFF, recalculated 8, HP full True, MP 40");
        return failures;
    }

    /// <summary>
    /// Fast-forward as a toggle (FastForwardToggle): the input's frame, fast-forward's mode, GlobalWork and the input
    /// manager where the game has them; the game's own pressed and held checks reading the bytes the toggle writes; and
    /// a frame at a time, a press turning a speed on, the other speed's switching to it, and a press of the one on
    /// turning it off, with the game's bytes left alone where fast-forward can't be on.
    /// </summary>
    public static int FastForwardToggleRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: fast-forward toggle, {name}{(ok ? "" : ": WRONG")}");
        }
        long Off(nint at) => at == 0 ? -1 : at - game.Image.ImageStart;
        if (FastForwardToggle.FrameAt == 0)
            FastForwardToggle.Install(game);
        Check($"the input's frame 0x{Off(FastForwardToggle.FrameAt):X}, the mode 0x{Off(FastForwardToggle.ModeAt):X}, " +
              $"GlobalWork 0x{Off(FastForwardToggle.GlobalWorkAt):X}, the input manager 0x{Off(FastForwardToggle.InputManagerAt):X}",
              Off(FastForwardToggle.FrameAt) == 0xF17D4 && Off(FastForwardToggle.ModeAt) == 0x1E8A9C
              && Off(FastForwardToggle.GlobalWorkAt) == 0x3CD9DD0 && Off(FastForwardToggle.InputManagerAt) == 0x3CD9EE8);

        const int Slow = FastForwardToggle.Slow, Fast = FastForwardToggle.Fast;
        var size = FastForwardToggle.Actions + 0x100 * FastForwardToggle.ActionBytes;
        var state = (nint)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)size);
        var work = (nint)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x100);
        try
        {
            byte* At(int action, int binding, int what) =>
                (byte*)(state + FastForwardToggle.Actions + action * FastForwardToggle.ActionBytes
                        + binding * FastForwardToggle.BindingBytes + what);
            void Clear() => new Span<byte>((void*)state, size).Clear();
            // The game's pressed and held checks (0x0F0D00, 0x0F0D4C: the UI's, as Autopilot finds them).
            var pressed = (delegate* unmanaged<nint, int, byte>)game.Image.FindUnique(string.Format(Autopilot.UiActionPattern, 0x52));
            var held = (delegate* unmanaged<nint, int, byte>)game.Image.FindUnique(string.Format(Autopilot.UiActionPattern, 0x54));
            *At(Slow, 2, FastForwardToggle.Pressed) = 1;
            *At(Fast, 1, FastForwardToggle.Held) = 1;
            Check("the game's checks read the bytes the toggle reads and writes: the third binding's press, the second's hold",
                  pressed(state, Slow) == 1 && held(state, Slow) == 0 && pressed(state, Fast) == 0 && held(state, Fast) == 1
                  && FastForwardToggle.IsPressed(state, Slow) && !FastForwardToggle.IsPressed(state, Fast));
            // The game's mode for a GlobalWork in an event (+0x60 2), with no config loaded on the check's copy.
            var config = *(nint*)GameImage.Rip(FastForwardToggle.ModeAt, 3, 7);
            *(int*)(work + 0x60) = 2;
            if (config == 0)
                Check("the mode of an event's GlobalWork is 2", ((delegate* unmanaged<nint, int>)FastForwardToggle.ModeAt)(work) == 2);

            // A battle (mode 1): the keyboard's fast-forward (the second binding) pressed and held.
            var latch = new FastForwardToggle.Latch();
            void Step(bool filled, int mode) => FastForwardToggle.Step(state, filled, mode, ref latch);
            Clear();
            *At(Slow, 1, FastForwardToggle.Pressed) = 1;
            *At(Slow, 1, FastForwardToggle.Held) = 1;
            Step(true, 1);
            Check("a press turns fast-forward on, held as the first binding's", latch.On == Slow && held(state, Slow) == 1
                  && *At(Slow, 0, FastForwardToggle.Held) == 1 && *At(Slow, 1, FastForwardToggle.Held) == 0 && held(state, Fast) == 0);
            Clear(); // let go: the game's next frame has it neither pressed nor held
            Step(true, 1);
            Check("it stays on after the button is let go", latch.On == Slow && held(state, Slow) == 1 && held(state, Fast) == 0);
            *At(Slow, 0, FastForwardToggle.Pressed) = 1; // a stale press on a frame the game didn't fill
            Step(false, 1);
            Check("a frame the game didn't fill toggles nothing", latch.On == Slow && held(state, Slow) == 1);
            Clear();
            *At(Fast, 0, FastForwardToggle.Pressed) = 1;
            *At(Fast, 0, FastForwardToggle.Held) = 1;
            Step(true, 1);
            Check("the faster speed's press switches to it", latch.On == Fast && held(state, Fast) == 1 && held(state, Slow) == 0);
            Clear();
            *At(Fast, 0, FastForwardToggle.Pressed) = 1;
            *At(Fast, 0, FastForwardToggle.Held) = 1;
            Step(true, 1);
            Check("its press again turns fast-forward off, though the button is still held",
                  latch.On == 0 && held(state, Fast) == 0 && held(state, Slow) == 0);
            Clear();
            *At(Fast, 0, FastForwardToggle.Held) = 1;
            Step(true, 1);
            Check("and it stays off while the button stays held", latch.On == 0 && held(state, Fast) == 0);

            // Where fast-forward can't be on (mode 0): the game's bytes as they are, and a press waits for a while.
            latch = new FastForwardToggle.Latch { On = Fast };
            Clear();
            *At(Slow, 0, FastForwardToggle.Pressed) = 1;
            *At(Slow, 0, FastForwardToggle.Held) = 1;
            Step(true, 0);
            Check("where fast-forward can't be on, a press changes nothing yet and the bytes stay the game's",
                  latch.On == Fast && held(state, Slow) == 1 && held(state, Fast) == 0 && pressed(state, Slow) == 1);
            Clear();
            for (var k = 0; k < FastForwardToggle.PendingFrames - 2; k++)
                Step(true, 0);
            Step(true, 1);
            Check("and it's taken when fast-forward can be on again a moment later (a battle between two states)",
                  latch.On == Slow && latch.Pending == 0 && held(state, Slow) == 1 && held(state, Fast) == 0);
            Clear();
            *At(Slow, 0, FastForwardToggle.Pressed) = 1;
            Step(true, 0);
            Clear();
            for (var k = 0; k < FastForwardToggle.PendingFrames; k++)
                Step(true, 0);
            Step(true, 1);
            Check("but not after longer (a press in a menu)", latch.On == Slow && latch.Pending == 0 && held(state, Slow) == 1);

            // An event (mode 2) with fast-forward on from before it: pressed every frame, as the event's code needs.
            Clear();
            latch = new FastForwardToggle.Latch { On = Slow };
            Step(true, FastForwardToggle.Event);
            Check("in an event, the speed on is pressed and held", latch.On == Slow && pressed(state, Slow) == 1
                  && held(state, Slow) == 1 && pressed(state, Fast) == 0 && held(state, Fast) == 0);
            Clear();
            latch = new FastForwardToggle.Latch();
            Step(true, FastForwardToggle.Event);
            Check("in an event with it off, nothing is pressed", latch.On == 0 && pressed(state, Slow) == 0 && pressed(state, Fast) == 0);
        }
        finally
        {
            System.Runtime.InteropServices.NativeMemory.Free((void*)state);
            System.Runtime.InteropServices.NativeMemory.Free((void*)work);
        }
        return failures;
    }

    /// <summary>The save folder's name: the storage setup's copy now reads ReMixed's folder name.</summary>
    public static int SeparateSaves(Game game, Log log)
    {
        if (SaveFolder.CopyAt == 0)
            SaveFolder.Install(game);
        var target = (byte*)GameImage.Rip(SaveFolder.CopyAt, 3, 7);
        var name = System.Text.Encoding.ASCII.GetString(target, SaveFolder.Folder.Length + 1);
        var ok = target == SaveFolder.Name && name == SaveFolder.Folder + " ";
        log.Info($"self-test: separate saves, the storage setup's title copy (0x{SaveFolder.CopyAt - game.Image.ImageStart:X}) "
                 + $"reads \"{name.TrimEnd(' ')}\"{(ok ? "" : ": WRONG")}");
        return ok ? 0 : 1;
    }

    private static int _refreshedList, _refreshedMenu, _redrawn;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeRefreshList(int job) => _refreshedList++;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeRefreshMenu() => _refreshedMenu++;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeRedrawRow(nint list, byte* entry) => _redrawn++;

    /// <summary>
    /// Unlearning in the Job menu's ability list: the learned test's stub (written again against landing pads) with a
    /// made-up entry, menu record and job list, the Squire's skillset, and stand-ins for the cost and the refreshes.
    /// </summary>
    public static int UnlearnInMenu(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: unlearning in the Job menu, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Unlearn.LearnedAt == 0)
        {
            Check("not installed", false);
            return failures;
        }
        var site = (byte*)Unlearn.LearnedAt;
        Check($"the ability list's JP test (0x{Unlearn.LearnedAt - game.Image.ImageStart:X}) jumps to a stub",
              site[0] == 0xE9 && Enumerable.Range(5, 6).All(k => site[k] == 0x90));
        // A harness: rax the entry's offset, rdi the entries, rbx the list, then the stub (entered 16-aligned); the pads
        // return 1 (not learned), 2 (the message), 3 (the handler's end).
        var code = GameImage.AllocateCode(128);
        byte[] harness = [0x53, 0x57, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x89, 0xC8, 0x48, 0x89, 0xD7, 0x4C, 0x89, 0xC3,
                          0x41, 0xFF, 0xD1, 0x48, 0x83, 0xC4, 0x20, 0x5F, 0x5B, 0xC3];
        harness.CopyTo(new Span<byte>(code, harness.Length));
        for (var k = 1; k <= 3; k++)
        {
            byte[] pad = [0xB8, (byte)k, 0x00, 0x00, 0x00, 0xC3];
            pad.CopyTo(new Span<byte>(code + 32 + 16 * k, pad.Length));
        }
        var stub = GameImage.AllocateCode(Unlearn.StubSize);
        Unlearn.WriteLearnedStub(stub, (nint)(code + 48), (nint)(code + 64), (nint)(code + 80));
        var run = (delegate* unmanaged<long, nint, nint, nint, int>)code;

        const int index = 3, job = 0x4A, slot = 5;
        var ability = game.Units.SkillsetAbility(game.Units.SkillsetOf(job), 2);
        var entries = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(3 * 0xB4);
        var list = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x3000);
        *(byte**)(list + Unlearn.MenuBegin) = entries;
        *(byte**)(list + Unlearn.MenuBegin + 8) = entries + 3 * 0xB4;
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var (jobs, jobIndex, _) = Unlearn.MenuJob;
        var savedRecord = Dismissal.Records[index];
        var (savedCurrent, savedJob, savedSlot) = (*Dismissal.Current, *(short*)jobIndex, ((ushort*)jobs)[slot]);
        var realCost = Unlearn.Cost;
        var realList = Unlearn.RefreshList;
        var realMenu = Unlearn.RefreshMenu;
        var realRedraw = Unlearn.RedrawRow;
        Unlearn.Cost = &FakeJpCost;
        Unlearn.RefreshList = &FakeRefreshList;
        Unlearn.RefreshMenu = &FakeRefreshMenu;
        Unlearn.RedrawRow = &FakeRedrawRow;
        Dismissal.Records[index] = (nint)record;
        *Dismissal.Current = index;
        *(short*)jobIndex = slot;
        ((ushort*)jobs)[slot] = job;
        var entry = entries + 0xB4; // the second entry, so the offset in rax matters
        var other = entries + 2 * 0xB4; // another entry still to buy
        string Press(bool flag, bool learned = true, int support = 0)
        {
            record[0x7B] = (byte)(learned ? 0x20 : 0); // the Squire's slot 2
            *(ushort*)(record + 0xCA) = 1000;
            *(ushort*)(record + 0x64) = (ushort)support;
            *(int*)(entry + 0x40) = ability;
            *(int*)(entry + Unlearn.EntryJp) = flag ? -1 : 1000;
            *(int*)(entry + Unlearn.EntryCost) = flag ? 0 : 200;
            *(int*)(other + Unlearn.EntryJp) = 1000;
            *(int*)(entries + Unlearn.EntryJp) = -1; // a learned one that stays
            _fakeCost = 200;
            _refreshedList = _refreshedMenu = _redrawn = 0;
            var r = run(0xB4, (nint)entries, (nint)list, (nint)stub);
            var where = r switch { 1 => "the game's path", 2 => "the message", 3 => "unlearned", _ => $"{r}" };
            return $"{where}, bit {(record[0x7B] & 0x20) != 0}, entry JP {*(int*)(entry + Unlearn.EntryJp)} cost {*(int*)(entry + Unlearn.EntryCost)}, "
                   + $"others {*(int*)(other + Unlearn.EntryJp)}/{*(int*)(entries + Unlearn.EntryJp)}, JP {*(ushort*)(record + 0xCA)}, "
                   + $"refreshed {_refreshedList}/{_refreshedMenu}/{_redrawn}";
        }
        var learnedOne = Press(true);
        var notLearned = Press(false, learned: false);
        var equipped = Press(true, support: ability);
        Unlearn.Cost = realCost;
        Unlearn.RefreshList = realList;
        Unlearn.RefreshMenu = realMenu;
        Unlearn.RedrawRow = realRedraw;
        Dismissal.Records[index] = savedRecord;
        (*Dismissal.Current, *(short*)jobIndex, ((ushort*)jobs)[slot]) = (savedCurrent, savedJob, savedSlot);
        System.Runtime.InteropServices.NativeMemory.Free(entries);
        System.Runtime.InteropServices.NativeMemory.Free(list);
        System.Runtime.InteropServices.NativeMemory.Free(record);
        Check($"A on a learned Squire ability (0x{ability:X3}): {learnedOne}",
              learnedOne == "unlearned, bit False, entry JP 1200 cost 200, others 1200/-1, JP 1200, refreshed 1/1/2");
        Check($"A on one that isn't learned goes the game's way: {notLearned}",
              notLearned == "the game's path, bit False, entry JP 1000 cost 200, others 1000/-1, JP 1000, refreshed 0/0/0");
        Check($"A on an equipped one gets the game's message: {equipped}",
              equipped == "the message, bit True, entry JP -1 cost 0, others 1000/-1, JP 1000, refreshed 0/0/0");
        return failures;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeMap(int variable) => variable == MoveFind.MapVar ? 5 : 0;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeMapLater(int variable) => variable == MoveFind.MapVar ? 5 : variable == MoveFind.StoryVar ? 0x20 : 0;

    /// <summary>
    /// Bonus EXP &amp; JP on Victory (installed here if the default None left it out) on a unit slot (3): the game's own
    /// JP add for real, a stand-in for the level-up (which reaches sprite code), and an enemy that gets nothing.
    /// </summary>
    public static int VictoryBonusRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: victory bonus, {name}{(ok ? "" : ": WRONG")}");
        }
        if (VictoryBonus.AddJp == null)
        {
            log.Info("self-test: victory bonus, the default (None) installs nothing: installed on this copy with Both");
            try
            {
                VictoryBonus.Install(game, Options.VictoryBonusRule.Both);
            }
            catch (Exception e)
            {
                Check($"installs: {e.Message}", false);
                return failures;
            }
        }
        Check($"the battle end's write-back is hooked (0x{BattleEnd.WriteBackAt - game.Image.ImageStart:X})",
              ((byte*)BattleEnd.WriteBackAt)[0] is 0xE9 or 0xFF);
        var units = game.TurnCode.Units;
        var unit = units + 3 * Battle.UnitSize;
        var saved = new Span<byte>(unit, Battle.UnitSize).ToArray();
        var levelUp = VictoryBonus.LevelUp;
        // The stand-in: cmp byte [rcx+0x28], 100; jb +3; inc byte [rcx+0x29]; xor eax, eax; ret.
        byte[] fake = [0x80, 0x79, 0x28, 0x64, 0x72, 0x03, 0xFE, 0x41, 0x29, 0x31, 0xC0, 0xC3];
        var code = GameImage.AllocateCode(16);
        new ReadOnlySpan<byte>(fake).CopyTo(new Span<byte>(code, fake.Length));
        try
        {
            VictoryBonus.LevelUp = (delegate* unmanaged<byte*, int>)code;
            new Span<byte>(unit, Battle.UnitSize).Clear();
            unit[1] = 3; unit[3] = 0x4B; unit[6] = 0x80; unit[VictoryBonus.Level] = 20; unit[VictoryBonus.Exp] = 37;
            unit[Battle.UnitStatuses] = 0x20;   // fallen: still gets the JP, as in ReMixed
            var job = VictoryBonus.JobIndex(0x4B);
            unit[VictoryBonus.JobLevels + job / 2] = (byte)((job & 1) != 0 ? 0x03 : 0x30);
            *(ushort*)(unit + 0xF0 + 2 * job) = 100;
            *(ushort*)(unit + 0x11E + 2 * job) = 100;
            var jp = VictoryBonus.ApplyTo(unit, 3, Options.VictoryBonusRule.Both);
            var current = *(ushort*)(unit + 0xF0 + 2 * job);
            var total = *(ushort*)(unit + 0x11E + 2 * job);
            Check($"a fallen Lv 20 unit at job level 3 (job index {job}): {jp} JP (current {current}, total {total}), level {unit[VictoryBonus.Level]}, EXP {unit[VictoryBonus.Exp]}, still Dead {unit[Battle.UnitStatuses] == 0x20}",
                  jp == 190 && current == 290 && total == 290 && unit[VictoryBonus.Level] == 21 && unit[VictoryBonus.Exp] == 0
                  && unit[Battle.UnitStatuses] == 0x20);
            *(ushort*)(unit + 0xF0 + 2 * job) = 9950;
            *(ushort*)(unit + 0x11E + 2 * job) = 9950;
            VictoryBonus.ApplyTo(unit, 3, Options.VictoryBonusRule.Jp);
            var capped = *(ushort*)(unit + 0xF0 + 2 * job);
            unit[VictoryBonus.Team] = 0x10;
            var enemy = VictoryBonus.ApplyTo(unit, 3, Options.VictoryBonusRule.Both);
            Check($"JP only: capped at {capped}, level stays {unit[VictoryBonus.Level]}; an enemy gets {enemy} and no level",
                  capped == 9999 && unit[VictoryBonus.Level] == 21 && enemy == 0);
        }
        finally
        {
            VictoryBonus.LevelUp = levelUp;
            saved.CopyTo(new Span<byte>(unit, Battle.UnitSize));
        }
        return failures;
    }

    /// <summary>The bow path: the two removed tests, and ReMixed's arc choice through a copy of its stub.</summary>
    public static int ArcObstacleRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: arc obstacles, {name}{(ok ? "" : ": WRONG")}");
        }
        if (ArcObstacles.Stub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var jump = ((byte*)ArcObstacles.JumpSkip)[0] == 0x0F && ((byte*)ArcObstacles.SecondTest)[0] == 0x66
                   && ((byte*)ArcObstacles.FirstTest)[0] == 0xE9 && GameImage.Rip(ArcObstacles.FirstTest, 1, 5) == ArcObstacles.Stub;
        Check($"the Jump skip and the second arc's team test are gone, the first test jumps to the stub: {jump}", jump);
        // push rdi; mov edi, ecx (hit 1); mov r10d, -1; the copy | first: mov eax, 1 | next: mov eax, 2; pop rdi; ret.
        var harness = Harness(ArcObstacles.Stub, [0x57, 0x8B, 0xF9, 0x41, 0xBA, 0xFF, 0xFF, 0xFF, 0xFF],
                              [[0xB8, 1, 0, 0, 0, 0x5F, 0xC3], [0xB8, 2, 0, 0, 0, 0x5F, 0xC3]],
                              (copy, exits) => ArcObstacles.WriteStub((byte*)copy, exits[0], exits[1]));
        var choose = (delegate* unmanaged<int, int, int>)harness;   // (hit 1, hit 2): 1 the first arc, 2 the second's test
        string Arc(int hit1, int hit2) => choose(hit1, hit2) == 1 ? "first" : "second";
        var noSecond = Arc(3, -1);
        var same = Arc(3, 3);
        var differ = Arc(3, 5);
        var noFirst = Arc(-1, 5);
        Check($"the first arc hits 3: the second hits none {noSecond}, the same {same}, another {differ}; the first none, the second 5: {noFirst}",
              noSecond == "first" && same == "first" && differ == "second" && noFirst == "second");
        return failures;
    }

    /// <summary>The autopilot's pad wrapper: the frame sees the pad's bytes with the masks applied, they're back after,
    /// and the frame's return value comes through.</summary>
    public static int AutopilotPadWrapper(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: the autopilot's pad wrapper, {name}{(ok ? "" : ": WRONG")}");
        }
        var data = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(64);
        var pad = data;                 // the pad struct: +2 high, +3 low
        var pointer = (nint*)(data + 16);
        var masks = data + 32;
        var seen = (ushort*)(data + 40);
        *pointer = (nint)pad;
        pad[2] = 0xFF; pad[3] = 0xFF;   // nothing held
        masks[0] = 0xF7; masks[1] = 0xBF; // Start (0x08 high) and Cross (0x40 low)
        var code = GameImage.AllocateCode(256);
        // The "frame": mov rax, &pad; movzx eax, word [rax+2]; mov rcx, &seen; mov [rcx], ax; mov eax, 0x77; ret.
        var frame = code + 160;
        var f = new List<byte> { 0x48, 0xB8 };
        f.AddRange(BitConverter.GetBytes((long)pad));
        f.AddRange([0x0F, 0xB7, 0x40, 0x02, 0x48, 0xB9]);
        f.AddRange(BitConverter.GetBytes((long)seen));
        f.AddRange([0x66, 0x89, 0x01, 0xB8, 0x77, 0, 0, 0, 0xC3]);
        f.ToArray().CopyTo(new Span<byte>(frame, f.Count));
        var bytes = Autopilot.PadWrapper(pointer, masks, (nint)(code + 120));
        bytes.CopyTo(new Span<byte>(code, bytes.Length));
        *(nint*)(code + 120) = (nint)frame;
        var result = ((delegate* unmanaged<int>)code)();
        Check($"the frame saw 0x{*seen:X4}, returned 0x{result:X}; after, the pad's bytes are {pad[2]:X2} {pad[3]:X2}",
              *seen == 0xBFF7 && result == 0x77 && pad[2] == 0xFF && pad[3] == 0xFF);
        return failures;
    }

    /// <summary>The autopilot's native hook on the UI's action check: yes for its action, the bit set for any action
    /// asked about, and the original's answer otherwise, with the caller's other registers kept.</summary>
    public static int AutopilotUiStub(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: the autopilot's UI hook, {name}{(ok ? "" : ": WRONG")}");
        }
        var ui = (int*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
        var asked = (uint*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x80);
        var code = GameImage.AllocateCode(256);
        // The "original": mov al, 0x55; ret.
        var original = code + 160;
        original[0] = 0xB0; original[1] = 0x55; original[2] = 0xC3;
        var stub = code;
        var bytes = Autopilot.UiStub(ui, asked, (nint)(stub + 88));
        bytes.CopyTo(new Span<byte>(stub, bytes.Length));
        *(nint*)(stub + 88) = (nint)original;
        var run = (delegate* unmanaged<nint, uint, byte>)stub;
        *ui = 0x1A;
        var yes = run(0, 0x1A);
        var other = run(0, 0x23);
        *ui = -1;
        var none = run(0, 0x1A);
        var bits = (asked[0] >> 0x1A & 1, asked[1] >> 3 & 1);
        Check($"its action {yes}, another 0x{other:X}, none set 0x{none:X}; asked bits {bits}",
              yes == 1 && other == 0x55 && none == 0x55 && bits == (1u, 1u));
        return failures;
    }

    /// <summary>The crash log's native filter: anything but an access violation inside the exe goes on searching
    /// (returns 0) without reaching the managed handler; an access violation inside reaches it.</summary>
    public static int CrashLogFilter(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: the crash log's filter, {name}{(ok ? "" : ": WRONG")}");
        }
        var stub = GameImage.AllocateCode(96);
        var code = CrashLog.Stub(0x10000, 0x20000, (nint)(delegate* unmanaged<nint, int>)&FakeCrashHandler);
        code.CopyTo(new Span<byte>(stub, code.Length));
        var run = (delegate* unmanaged<nint, int>)stub;
        var record = stackalloc long[8];
        var pointers = stackalloc nint[2];
        pointers[0] = (nint)record;
        int Try(uint exceptionCode, long at)
        {
            *(uint*)record = exceptionCode;
            record[2] = at;
            return run((nint)pointers);
        }
        var other = Try(0xE06D7363, 0x15000);   // a C++ exception inside the range
        var outside = Try(0xC0000005, 0x30000); // an access violation outside it
        var inside = Try(0xC0000005, 0x15000);
        Check($"another exception {other}, a fault outside the exe {outside}, one inside {inside}",
              other == 0 && outside == 0 && inside == 0x42);
        return failures;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeCrashHandler(nint pointers) => 0x42;

    /// <summary>The harness's write watch: its native handler records a data breakpoint's single step (Rip, Dr6, the
    /// stack) and continues (-1), leaves other exceptions and other single steps alone (0); Dr7 for two watches.</summary>
    public static int WatchHandler(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: the harness's write watch, {name}{(ok ? "" : ": WRONG")}");
        }
        var bufferSize = 8 + Watch.Entries * Watch.EntryQwords * 8;
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(bufferSize);
        new Span<byte>((void*)buffer, bufferSize).Clear();
        var code = Watch.Stub(buffer);
        var stub = GameImage.AllocateCode(code.Length);
        code.CopyTo(new Span<byte>(stub, code.Length));
        var run = (delegate* unmanaged<nint, int>)stub;
        var record = stackalloc long[8];
        var context = (byte*)System.Runtime.InteropServices.NativeMemory.AlignedAlloc(0x4D0, 16);
        var stack = stackalloc long[Watch.StackQwords];
        for (var k = 0; k < Watch.StackQwords; k++)
            stack[k] = 0x1000 + k;
        var pointers = stackalloc nint[2];
        pointers[0] = (nint)record;
        pointers[1] = (nint)context;
        int Try(uint exceptionCode, long dr6)
        {
            new Span<byte>(context, 0x4D0).Clear();
            *(uint*)record = exceptionCode;
            *(long*)(context + 0x68) = dr6;
            *(long*)(context + 0xF8) = 0x140123456;
            *(long*)(context + 0x98) = (long)stack;
            return run((nint)pointers);
        }
        var other = Try(0xC0000005, 1);
        var stepping = Try(0x80000004, 0x4000); // a single step that isn't a data breakpoint (BS)
        var hit = Try(0x80000004, 2);
        var entry = (long*)(buffer + 8);
        Check($"another exception {other}, a plain single step {stepping}, a watch's {hit}; recorded {*(long*)buffer}: "
              + $"Rip 0x{entry[1 - 1]:X}, Dr6 {entry[1]}, the stack 0x{entry[2]:X}..0x{entry[1 + Watch.StackQwords]:X}, "
              + $"Dr6 after {*(long*)(context + 0x68)}",
              other == 0 && stepping == 0 && hit == -1 && *(long*)buffer == 1 && entry[0] == 0x140123456 && entry[1] == 2
              && entry[2] == 0x1000 && entry[1 + Watch.StackQwords] == 0x1000 + Watch.StackQwords - 1
              && *(long*)(context + 0x68) == 0);
        System.Runtime.InteropServices.NativeMemory.AlignedFree(context);
        System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        return failures;
    }

    /// <summary>ReMixed's unit art (RemixedSprites): the sheet, texture and animation tables found, and a graphic given a
    /// sheet of its own (both sheet tables naming its file, the pack files giving it, the game reading its old file in
    /// its place), its texture and an animation set; the tables put back after.</summary>
    public static int UnitSprites(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: ReMixed's unit art, {name}{(ok ? "" : ": WRONG")}");
        }
        RemixedSprites.Find(game.Image);
        const int graphic = 0x5D; // vanilla's DAMI.SPR, with no texture
        uint* sheets = RemixedSprites.SheetTable, others = RemixedSprites.OtherSheetTable;
        int* textures = RemixedSprites.Textures;
        uint* sets = RemixedSprites.AnimationSets;
        Check($"tables at 0x{(nint)sheets - game.Image.ImageStart:X}, 0x{(nint)others - game.Image.ImageStart:X} and "
              + $"0x{(nint)textures - game.Image.ImageStart:X}; 0x5D's sheet file {sheets[2 * graphic]} "
              + $"(0x{sheets[2 * graphic + 1]:X} bytes), its texture {textures[graphic]}; 0x97's texture 0x{textures[0x97]:X}",
              (nint)sheets - game.Image.ImageStart == 0x784690 && (nint)others - game.Image.ImageStart == 0x784BE0
              && (nint)textures - game.Image.ImageStart == 0x7151F0 && sheets[2 * graphic] == 91
              && textures[graphic] == -1 && textures[0x97] == 0x44E);
        // As vanilla's (BATTLE.BIN 0x2D748): the male Squire's copies a person's (type1, type1), DAMI.SPR a monster's,
        // the Hydras' mon and ruka; the dragon's and hydra's textures their own.
        Check($"animation sets at 0x{(nint)sets - game.Image.ImageStart:X}: 0x57's 0x{sets[0x57]:X8}, 0x5A's "
              + $"0x{sets[0x5A]:X8}, 0x5D's 0x{sets[graphic]:X8}, 0x97's 0x{sets[0x97]:X8}; 0x57's texture "
              + $"0x{textures[0x57]:X}, 0x5A's 0x{textures[0x5A]:X}",
              (nint)sets - game.Image.ImageStart == 0x673300 && sets[0x57] == 0x24000000 && sets[0x5A] == 0x24000000
              && sets[graphic] == 0x24000303 && sets[0x97] == 0x24000503 && textures[0x57] == 0x3D4
              && textures[0x5A] == 0x3DA);
        uint file = sheets[2 * graphic], size = sheets[2 * graphic + 1], oldSet = sets[graphic];
        var sheet = new byte[0xB000];
        sheet[0x9000] = 0x5D;
        RemixedSprites.Give(graphic, sheet, 0x370, 0x24000000);
        var number = RemixedSprites.FileBase + graphic;
        var buffer = stackalloc byte[4];
        var given = PackFiles.Give(number, 0x12, 4, buffer); // sector 0x12 is the sheet's 0x9000
        Check($"0x5D's sheet as file 0x{sheets[2 * graphic]:X} (0x{sheets[2 * graphic + 1]:X} bytes) in both tables, "
              + $"texture 0x{textures[graphic]:X}, animation set 0x{sets[graphic]:X8}, read in place of file "
              + $"{PackFiles.StandIns.GetValueOrDefault(number)}, given {given} (0x{buffer[0]:X2} at 0x9000)",
              sheets[2 * graphic] == number && others[2 * graphic] == number && sheets[2 * graphic + 1] == 0xB000
              && others[2 * graphic + 1] == 0xB000 && textures[graphic] == 0x370 && sets[graphic] == 0x24000000
              && PackFiles.StandIns.GetValueOrDefault(number) == file && given && buffer[0] == 0x5D);
        foreach (var table in new[] { sheets, others })
            GameImage.Unprotected((nint)(table + 2 * graphic), 8, () =>
            {
                table[2 * graphic] = file;
                table[2 * graphic + 1] = size;
            });
        GameImage.Unprotected((nint)(textures + graphic), 4, () => textures[graphic] = -1);
        GameImage.Unprotected((nint)(sets + graphic), 4, () => sets[graphic] = oldSet);
        PackFiles.Files.Remove(number);
        PackFiles.StandIns.Remove(number);
        RemixedSprites.Given.Remove(graphic);

        // A graphic drawn as another (RemixedSprites.DrawAs, as 0x38 as the remaster's male Bard): 0x3C, Velius's, which
        // nothing of the mod's changes, as 0x82, then put back.
        const int alias = 0x3C, bard = 0x82;
        uint aliasFile = sheets[2 * alias], aliasSize = sheets[2 * alias + 1];
        var aliasTexture = textures[alias];
        RemixedSprites.DrawAs(alias, bard);
        Check($"0x3C drawn as 0x82: sheet file {sheets[2 * alias]} (0x{sheets[2 * alias + 1]:X} bytes) in both tables, "
              + $"texture 0x{textures[alias]:X}; it was file {aliasFile}, texture 0x{aliasTexture:X}",
              aliasTexture != textures[bard] && sheets[2 * alias] == sheets[2 * bard] && others[2 * alias] == others[2 * bard]
              && sheets[2 * alias + 1] == sheets[2 * bard + 1] && textures[alias] == textures[bard]
              && textures[bard] == 0x424 && sheets[2 * bard] != aliasFile);
        foreach (var table in new[] { sheets, others })
            GameImage.Unprotected((nint)(table + 2 * alias), 8, () =>
            {
                table[2 * alias] = aliasFile;
                table[2 * alias + 1] = aliasSize;
            });
        GameImage.Unprotected((nint)(textures + alias), 4, () => textures[alias] = aliasTexture);
        RemixedSprites.Given.Remove(alias);
        return failures;
    }

    /// <summary>ReMixed's event graphics without a texture of their own (0x10 in place, appended alias 0xF2) each take
    /// an unused PSP stub that no other event graphic names.</summary>
    public static int EventGraphicTextures(Game game, Log log)
    {
        var table = RemixedEventGraphics.Textures;
        var at = table == null ? -1 : (nint)table - game.Image.ImageStart;
        var paletteIds = RemixedEventGraphics.PaletteIds;
        var paletteIdsAt = paletteIds == null ? -1 : (nint)paletteIds - game.Image.ImageStart;
        var palettePatches = RemixedEventGraphics.PaletteSites.Zip(RemixedEventGraphics.PaletteStubs)
            .All(pair => pair.First != 0 && pair.Second != 0 && *(byte*)pair.First == 0xE9
                         && pair.First + 5 + *(int*)(pair.First + 1) == pair.Second);
        var ok = table != null && at == 0x714490
                 && paletteIdsAt == 0x2E7FEF4
                 && RemixedEventGraphics.Routed.All(row => table[row.Graphic] == row.Texture)
                 && Enumerable.Range(0, 0x8F).Count(graphic => table[graphic] == RemixedEventGraphics.InPlaceTexture) == 1
                 && Enumerable.Range(0, 0x8F).All(graphic => table[graphic] != RemixedEventGraphics.AppendedTexture)
                 && palettePatches;
        log.Info($"self-test: ReMixed's event textures, table at 0x{at:X}, palette ids preserved at 0x{paletteIdsAt:X}; "
                 + $"in place 0x{RemixedEventGraphics.InPlaceGraphic:X}->0x{(table == null ? -1 : table[RemixedEventGraphics.InPlaceGraphic]):X}, "
                 + $"appended 0x{RemixedEventGraphics.AppendedAlias:X}->0x{(table == null ? -1 : table[RemixedEventGraphics.AppendedAlias]):X}, "
                 + $"neutral palette 0x{RemixedEventGraphics.NeutralPaletteTexture:X} patched {palettePatches}"
                 + (ok ? "" : ": WRONG"));
        return ok ? 0 : 1;
    }

    /// <summary>Guests saved on the old sprite sets (OldGuests): the battle units' making found, and the guest slots of
    /// a made-up roster moved or left as the story's progress and their name ids say; the Argath campaign's player
    /// Algus entry and made battle unit are recognized and linked to roster slot 0.</summary>
    public static int OldGuestMoves(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: guests saved on the old sprite sets, {name}{(ok ? "" : ": WRONG")}");
        }
        // The rule's hook (installed with the mod's rules) has the function's start; the pattern finds it otherwise.
        var at = OldGuests.At != 0 ? OldGuests.At : game.Image.FindUnique(OldGuests.MakeUnitsPattern);
        var rosterAt = (nint)OldGuests.FindRoster(game.Image) - game.Image.ImageStart;
        Check($"the battle units' making at 0x{at - game.Image.ImageStart:X}, the roster at 0x{rosterAt:X}",
              at - game.Image.ImageStart == 0x27128C && rosterAt == 0x11A7D10);
        // The build's rows: Agrias's 0x34 to 0x1E until progress 0x16, Rafa's 0x19 to 0x29 until 0x29.
        OldGuests.Guest[] guests = [new(0x34, 0x1E, 0x1E, 0x34, 0x16), new(0x19, 0x29, 0x29, 0x19, 0x29)];
        var size = UnitTables.RosterSlots * UnitTables.RosterSize;
        var roster = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)size);
        byte* Slot(int slot, byte sprites, byte job, int name)
        {
            var record = roster + slot * UnitTables.RosterSize;
            record[0] = sprites;
            record[1] = (byte)slot;
            record[OldGuests.RosterJob] = job;
            *(int*)(record + OldGuests.RosterName) = name;
            return record;
        }
        for (var slot = 0; slot < UnitTables.RosterSlots; slot++)
            roster[slot * UnitTables.RosterSize + 1] = 0xFF;
        var member = Slot(3, 0x34, 0x34, 0x34);      // in the party, not a guest: never moved
        var agrias = Slot(0x32, 0x34, 0x34, 0x34);   // the remaster's guest Agrias, saved with ReMixed's job row 0x34
        var other = Slot(0x33, 0x34, 0x34, 0x99);    // another name id on 0x34
        var rafa = Slot(0x34, 0x19, 0x29, 0x19);     // the guest Rafa with her own job already
        var moved = OldGuests.Move(roster, 0x10, guests);
        Check($"at progress 0x10: {moved.Count} moved; Agrias's guest 0x{agrias[0]:X2}/job 0x{agrias[2]:X2}, the party's "
              + $"0x{member[0]:X2}, another name's 0x{other[0]:X2}, Rafa's 0x{rafa[0]:X2}/job 0x{rafa[2]:X2}",
              moved.Count == 2 && agrias[0] == 0x1E && agrias[2] == 0x1E && member[0] == 0x34 && other[0] == 0x34
              && rafa[0] == 0x29 && rafa[2] == 0x29);
        // Ashley's guest record, saved by the Lich's battle: her sprite set and job 0x34, her own name id (the build's
        // ASHLEY_NAME, 0x42). Left as it is even in a save from before the guest Agrias's point (the PC's test save is
        // from chapter 1), and at her scene (progress 0x25).
        var ashley = Slot(0x32, 0x34, 0x34, 0x42);
        moved = OldGuests.Move(roster, 0x10, guests);
        Check($"at progress 0x10: {moved.Count} moved, Ashley's record (name id 0x42) "
              + $"0x{ashley[0]:X2}/job 0x{ashley[2]:X2}", moved.Count == 0 && ashley[0] == 0x34 && ashley[2] == 0x34);
        moved = OldGuests.Move(roster, 0x25, guests);
        Check($"at progress 0x25 (Ashley's): {moved.Count} moved, her record 0x{ashley[0]:X2}/job 0x{ashley[2]:X2}",
              moved.Count == 0 && ashley[0] == 0x34 && ashley[2] == 0x34);
        var lateRafa = Slot(0x35, 0x19, 0x19, 0x19);
        moved = OldGuests.Move(roster, 0x28, guests);
        Check($"at progress 0x28: Rafa's guest on 0x19 moved to 0x{lateRafa[0]:X2}/job 0x{lateRafa[2]:X2}, Ashley's left",
              moved.Count == 1 && lateRafa[0] == 0x29 && lateRafa[2] == 0x29 && ashley[0] == 0x34);
        roster[0x35 * UnitTables.RosterSize] = 0x19;
        moved = OldGuests.Move(roster, 0x29, guests);
        Check($"at progress 0x29: nothing moved ({moved.Count})", moved.Count == 0 && roster[0x35 * UnitTables.RosterSize] == 0x19);

        var records = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(
            (nuint)(OldGuests.EntryUnits * OldGuests.EntryUnitSize));
        var algusRecord = records + 3 * OldGuests.EntryUnitSize;
        algusRecord[0] = OldGuests.Algus;
        algusRecord[OldGuests.EntryFlags] = OldGuests.LoadsRoster;
        algusRecord[OldGuests.EntryTeam] = OldGuests.PlayerControl;
        Check($"the campaign leader is entry record {OldGuests.LeaderRecord(records)}",
              OldGuests.LeaderRecord(records) == 3);
        algusRecord[OldGuests.EntryTeam] = 0x84;
        Check("a guest Algus isn't the campaign leader", OldGuests.LeaderRecord(records) == -1);
        algusRecord[OldGuests.EntryTeam] = OldGuests.PlayerControl;

        var units = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(
            (nuint)(Battle.UnitCount * Battle.UnitSize));
        for (var k = 0; k < Battle.UnitCount; k++)
            units[k * Battle.UnitSize + 1] = 0xFF;
        var algusUnit = units + 5 * Battle.UnitSize;
        algusUnit[0] = OldGuests.Algus;
        algusUnit[1] = 5;
        algusUnit[2] = OldGuests.FirstGuestSlot;
        algusUnit[Battle.UnitTeam] = OldGuests.PlayerControl;
        var relinked = OldGuests.RelinkLeader(units, OldGuests.FirstGuestSlot);
        Check($"the made campaign leader is relinked to roster slot {algusUnit[2]}", relinked && algusUnit[2] == 0);

        // ReMixed's records that load a unit from the roster (LoadFromRoster), on the roster as the remaster keeps it.
        for (var slot = 0; slot < UnitTables.RosterSlots; slot++)
            roster[slot * UnitTables.RosterSize + 1] = 0xFF;
        var gaffgarionRecord = records + 7 * OldGuests.EntryUnitSize;
        var agriasRecord = records + 8 * OldGuests.EntryUnitSize;
        gaffgarionRecord[0] = OldGuests.Gaffgarion;
        agriasRecord[0] = OldGuests.Agrias;
        gaffgarionRecord[OldGuests.EntryFlags] = agriasRecord[OldGuests.EntryFlags] = OldGuests.LoadsRoster;
        algusRecord[0] = 0;
        Span<byte> changed = stackalloc byte[OldGuests.EntryUnits];
        Span<byte> savedGuests = stackalloc byte[OldGuests.GuestSlots * UnitTables.RosterSize];
        var borrowed = new List<OldGuests.Borrowed>();
        var fromData = OldGuests.LoadFromRoster(records, roster, changed, borrowed, savedGuests);
        Check($"guests in no slot of the roster are made from their data ({fromData})",
              fromData == 2 && borrowed.Count == 0 && (gaffgarionRecord[OldGuests.EntryFlags] & OldGuests.LoadsRoster) == 0
              && (agriasRecord[OldGuests.EntryFlags] & OldGuests.LoadsRoster) == 0);
        OldGuests.RestoreLoads(records, changed);
        Check("and their records' roster bit is put back after",
              gaffgarionRecord[OldGuests.EntryFlags] == OldGuests.LoadsRoster && agriasRecord[OldGuests.EntryFlags] == OldGuests.LoadsRoster);
        var savedAgrias = roster + OldGuests.FirstGuestSlot * UnitTables.RosterSize;
        savedAgrias[0] = OldGuests.Agrias;
        savedAgrias[1] = 0;
        fromData = OldGuests.LoadFromRoster(records, roster, changed, borrowed, savedGuests);
        Check($"a guest in its guest slot loads as the game does; only the missing one is made from data ({fromData})",
              fromData == 1 && (agriasRecord[OldGuests.EntryFlags] & OldGuests.LoadsRoster) != 0 && changed[7] == 1);
        OldGuests.RestoreLoads(records, changed);

        var monster = roster + 3 * UnitTables.RosterSize;
        monster[0] = OldGuests.Monster;
        monster[1] = 3;
        var bocoRecord = records;
        bocoRecord[0] = OldGuests.Monster;
        bocoRecord[OldGuests.EntryName] = OldGuests.BocoName;
        bocoRecord[OldGuests.EntryFlags] = 0x38;
        var knightRecord = records + 6 * OldGuests.EntryUnitSize;
        knightRecord[0] = 0x81;
        knightRecord[OldGuests.EntryName] = (byte)OldGuests.Whitney;
        knightRecord[OldGuests.EntryFlags] = 0x48;
        var knight = roster + 2 * UnitTables.RosterSize;
        knight[0] = 0x81;
        knight[1] = 2;
        fromData = OldGuests.LoadFromRoster(records, roster, changed, borrowed, savedGuests);
        Check($"Boco and a 1-A knight (sprite sets others share) are made from data, not from another chocobo or woman "
              + $"({fromData}, borrowed {borrowed.Count})",
              fromData == 3 && borrowed.Count == 0 && changed[0] == 1 && changed[6] == 1
              && (bocoRecord[OldGuests.EntryFlags] & 0x30) == 0x30);
        OldGuests.RestoreLoads(records, changed);
        Check("and the chocobo's and knight's records are put back", bocoRecord[OldGuests.EntryFlags] == 0x38
              && knightRecord[OldGuests.EntryFlags] == 0x48);
        bocoRecord[0] = knightRecord[0] = gaffgarionRecord[0] = 0;

        var hirame = roster + 4 * UnitTables.RosterSize;
        hirame[0] = 0x35;
        hirame[1] = 4;
        hirame[0x1E] = 77;
        var hirameRecord = records + 9 * OldGuests.EntryUnitSize;
        hirameRecord[0] = 0x35;
        hirameRecord[OldGuests.EntryFlags] = 0x49;
        fromData = OldGuests.LoadFromRoster(records, roster, changed, borrowed, savedGuests);
        var lent = roster + (OldGuests.FirstGuestSlot + 1) * UnitTables.RosterSize;
        Check($"a party member outside the guest slots (Hirame, slot 4) is lent the first free guest slot "
              + $"({string.Join(",", borrowed.Select(x => $"{x.Slot}->{x.Guest}"))}), not made from data ({fromData})",
              fromData == 0 && borrowed.Count == 1 && borrowed[0].Guest == OldGuests.FirstGuestSlot + 1 && borrowed[0].Slot == 4
              && lent[0] == 0x35 && lent[0x1E] == 77 && (hirameRecord[OldGuests.EntryFlags] & OldGuests.LoadsRoster) != 0);
        var made = units + 7 * Battle.UnitSize;
        made[0] = 0x35;
        made[1] = 7;
        made[2] = (byte)(OldGuests.FirstGuestSlot + 1);
        OldGuests.ReturnBorrowed(roster, units, borrowed, savedGuests);
        Check($"after the making her unit writes back to slot {made[2]} and the guest slot is as it was "
              + $"(0x{lent[0]:X2} 0x{lent[1]:X2})", made[2] == 4 && lent[1] == 0xFF && lent[0] != 0x35 && borrowed.Count == 0);
        for (var slot = OldGuests.FirstGuestSlot + 1; slot < OldGuests.FirstGuestSlot + OldGuests.GuestSlots; slot++)
        {
            roster[slot * UnitTables.RosterSize] = (byte)(0x40 + slot);
            roster[slot * UnitTables.RosterSize + 1] = (byte)slot;
        }
        fromData = OldGuests.LoadFromRoster(records, roster, changed, borrowed, savedGuests);
        Check($"with every guest slot taken (Agrias in the first) she's made from her data instead ({fromData})",
              fromData == 1 && borrowed.Count == 0 && changed[9] == 1);
        OldGuests.RestoreLoads(records, changed);
        System.Runtime.InteropServices.NativeMemory.Free(units);
        System.Runtime.InteropServices.NativeMemory.Free(records);
        System.Runtime.InteropServices.NativeMemory.Free(roster);
        return failures;
    }

    /// <summary>ReMixed's characters' faces (RemixedFaces): the remaster's fold (0x0F8338) through the hook, a graphic the
    /// build lists given its own face (Hirame's 0x59, which the game folds into the male Squire's 0x60; Ashley's 0x34
    /// under 0x14, as Agrias's face is 0x34's), the others the game's (0x59 again once unlisted, Mustadio's 0x16 into
    /// 0x22, Agrias's 0x1E into 0x34, 0x60 as it is).</summary>
    public static int FaceFold(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: faces, {name}{(ok ? "" : ": WRONG")}");
        }
        if (RemixedFaces.FoldAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var fold = (delegate* unmanaged<int, int>)RemixedFaces.FoldAt;
        int hirame = fold(0x59), ashley = fold(0x34), mustadio = fold(0x16), squire = fold(0x60), agrias = fold(0x1E);
        Check($"the build's graphics have faces of their own: 0x59 {hirame}, 0x34 0x{ashley:X2}, and the game's fold for "
              + $"the rest: 0x16 0x{mustadio:X2}, 0x60 0x{squire:X2}, 0x1E 0x{agrias:X2}",
              hirame == 0x59 && ashley == 0x14 && mustadio == 0x22 && squire == 0x60 && agrias == 0x34);
        RemixedFaces.Set(0x59, -1);
        var unlisted = fold(0x59);
        Check($"an unlisted graphic folds as the game folds it: 0x59 0x{unlisted:X2}", unlisted == 0x60);
        RemixedFaces.Set(0x59, 0x59);
        return failures;
    }

    /// <summary>The faces of the story characters whose face the remaster keeps under a number the mod gives one of
    /// ReMixed's characters (Agrias 0x1E and Ashley 0x34, Rafa 0x29 and ReMixed's 0x19, Celia 0x2D and ReMixed's 0x3A,
    /// Lede 0x2E and ReMixed's 0x3B), by each way a screen gets one. In battle (unit cards, the turn list, the battle's
    /// speech boxes, the formation's cards) from the unit's graphic, which is its sprite set: the fold. For a roster unit
    /// (the party menu's unit screen and its dialogs, shops, the formation list, the join screen) the world menus'
    /// 0x2C06D4: its sprite set's PS1 portrait (0x32AB3C, the table 0x683A70), that portrait's graphic (0x274028, the
    /// table 0x7146D0), then the fold. Each story character shows its own face (Agrias 0x34, not Ashley's 0x14, whose
    /// sprite set shared her portrait 0x17) and each of ReMixed's its own, by a portrait of its own.</summary>
    public static int FacePortraits(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: world menu faces, {name}{(ok ? "" : ": WRONG")}");
        }
        if (RemixedFaces.FoldAt == 0 || RemixedFaces.GraphicOfPortraitAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        // 0x32AB3C (ecx the roster slot, rdx what 0x2E16A8 fills in, r9 its palette out): mov rax, rsp; four saves;
        // push r15; sub rsp, 0x20; xor r11d, r11d; lea r15, [image]; or esi, -1; mov rbp, rdx; mov r10d, r11d;
        // cmp ecx, 0x36.
        var portraitOfUnit = (delegate* unmanaged<int, byte*, nint, int*, int>)game.Image.FindUnique(
            "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 57 48 83 EC 20 45 33 DB 4C 8D 3D ?? ?? ?? ?? "
            + "83 CE FF 48 8B EA 45 8B D3 83 F9 36");
        var graphicOf = (delegate* unmanaged<nint, int, int>)RemixedFaces.GraphicOfPortraitAt;
        var fold = (delegate* unmanaged<int, int>)RemixedFaces.FoldAt;
        // 0x2C06D4 (ecx 1 for a roster unit, edx its slot, r8 its palette out): mov [rsp+8], rbx; mov [rsp+0x20], rbp;
        // push rsi; push rdi; push r14; sub rsp, 0x2A0; the cookie; xor edi, edi; movsxd rbp, edx; mov r14, r8;
        // test ecx, ecx; je. Its roster path reads the menu's mode (+0x43: movsx ecx, word [mode]; 0 the menus'), a
        // level that has to reach its constant (+0x10E: movss xmm0, [level]; +0x116: comiss xmm0, [constant]; under it
        // gives none) and a kind (+0x129: test [kind], ~2; 0 or 2 looks the portrait up), which the check sets.
        var screenFace = game.Image.FindUnique(
            "48 89 5C 24 08 48 89 6C 24 20 56 57 41 56 48 81 EC A0 02 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 "
            + "90 02 00 00 33 FF 48 63 EA 4D 8B F0 85 C9 0F 84");
        var s = (byte*)screenFace;
        if (s[0x43] != 0x0F || s[0x44] != 0xBF || s[0x45] != 0x0D || s[0x10E] != 0xF3 || s[0x10F] != 0x0F
            || s[0x110] != 0x10 || s[0x116] != 0x0F || s[0x117] != 0x2F || s[0x129] != 0xF7 || s[0x12A] != 0x05)
        {
            Check("0x2C06D4 isn't laid out as this test expects", false);
            return failures;
        }
        var mode = (short*)GameImage.Rip(screenFace + 0x43, 3, 7);
        var level = (float*)GameImage.Rip(screenFace + 0x10E, 4, 8);
        var needed = (float*)GameImage.Rip(screenFace + 0x116, 3, 7);
        var kind = (int*)GameImage.Rip(screenFace + 0x129, 2, 10);
        (short savedMode, float savedLevel, int savedKind) = (*mode, *level, *kind);
        (*mode, *level, *kind) = ((short)0, *needed, 0);
        var screen = (delegate* unmanaged<int, int, int*, int>)screenFace;
        const int slot = 20;
        var unit = game.Units.Roster + slot * UnitTables.RosterSize;
        var saved = new Span<byte>(unit, UnitTables.RosterSize).ToArray();
        var drawn = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x100);
        string Face(int spriteSet)
        {
            unit[0] = (byte)spriteSet;
            var palette = -1;
            var portrait = portraitOfUnit(slot, drawn, 0, &palette);
            var graphic = graphicOf(0, portrait);
            var menuPalette = -1;
            var menuGraphic = screen(1, slot, &menuPalette);
            return $"0x{fold(graphic):X2} (portrait 0x{portrait:X2}, 0x{graphic:X2}, palette {palette})"
                   + (menuGraphic == graphic && menuPalette == palette ? "" : $", the menus' 0x{menuGraphic:X2} palette {menuPalette}");
        }
        string agrias = Face(0x1E), ashley = Face(0x34), rafa = Face(0x29), remixed19 = Face(0x19);
        string celia = Face(0x2D), remixed3A = Face(0x3A), lede = Face(0x2E), remixed3B = Face(0x3B), ramza = Face(2);
        (*mode, *level, *kind) = (savedMode, savedLevel, savedKind);
        new Span<byte>(saved).CopyTo(new Span<byte>(unit, UnitTables.RosterSize));
        System.Runtime.InteropServices.NativeMemory.Free(drawn);
        Check($"the world menus (0x2C06D4 agreeing): Agrias (0x1E) {agrias} and Ashley (0x34) {ashley}",
              agrias == "0x34 (portrait 0x17, 0x1E, palette 8)" && ashley.StartsWith("0x14 ") && ashley.EndsWith(", 0x34, palette 8)"));
        Check($"the world menus: Rafa (0x29) {rafa} and ReMixed's 0x19 {remixed19}",
              rafa == "0x19 (portrait 0x0E, 0x29, palette 8)" && remixed19.StartsWith("0x28 ") && remixed19.EndsWith(", 0x19, palette 8)"));
        Check($"the world menus: Celia (0x2D) {celia}, ReMixed's 0x3A {remixed3A}, Lede (0x2E) {lede}, ReMixed's 0x3B {remixed3B}",
              celia == "0x3A (portrait 0x8D, 0x2D, palette 8)" && remixed3A.StartsWith("0x2A ") && remixed3A.EndsWith(", 0x3A, palette 8)")
              && lede == "0x3B (portrait 0x8E, 0x2E, palette 8)" && remixed3B.StartsWith("0x2C ") && remixed3B.EndsWith(", 0x3B, palette 8)"));
        Check($"the world menus: Ramza's (2) as before: {ramza}", ramza == "0x02 (portrait 0x01, 0x02, palette 8)");
        int[] battle = [fold(0x1E), fold(0x34), fold(0x29), fold(0x19), fold(0x2D), fold(0x3A), fold(0x2E), fold(0x3B)];
        Check("in battle, by graphic: Agrias 0x1E, Ashley 0x34, Rafa 0x29, ReMixed's 0x19, Celia 0x2D, ReMixed's 0x3A, "
              + $"Lede 0x2E, ReMixed's 0x3B: {string.Join(", ", battle.Select(f => $"0x{f:X2}"))}",
              battle.SequenceEqual([0x34, 0x14, 0x19, 0x28, 0x3A, 0x2A, 0x3B, 0x2C]));
        var portraits = new[] { RemixedFaces.PortraitOfSpriteSet[0x34] & 0xFF, RemixedFaces.PortraitOfSpriteSet[0x19] & 0xFF,
                                RemixedFaces.PortraitOfSpriteSet[0x3A] & 0xFF, RemixedFaces.PortraitOfSpriteSet[0x3B] & 0xFF };
        Check($"ReMixed's four have unused portraits, each its own: {string.Join(", ", portraits.Select(p => $"0x{p:X2}"))}",
              portraits.Distinct().Count() == 4 && portraits.All(p => p is 0x70 or 0x84 or 0x8C or 0x98 or 0x9E or 0x9F));
        // An event's or a list's portrait of Agrias (0x17, the PS1's for her) is hers, through the game's own lookup.
        var agriasPortrait = fold(graphicOf(0, 0x17));
        Check($"Agrias's portrait 0x17 gives her face: 0x{agriasPortrait:X2}", agriasPortrait == 0x34);
        // Brave Story's people, by the graphics LoreDictionary gives them (+0x1C), through the hooked lookups' mapping
        // and the fold: Agrias 0x1E, Rafa 0x29, Celia 0x2D, Lede 0x2E, Gafgarion 0x11, Malak 0x12.
        int[] lore = [0x1E, 0x29, 0x2D, 0x2E, 0x11, 0x12];
        var people = lore.Select(g => fold(RemixedFaces.Person(g))).ToArray();
        Check($"Brave Story's people (Agrias, Rafa, Celia, Lede, Gafgarion, Malak): "
              + string.Join(", ", people.Select(f => $"0x{f:X2}")) + $"; 0x34 as 0x{RemixedFaces.Person(0x34):X2}",
              people.SequenceEqual([0x34, 0x19, 0x3A, 0x3B, 0x17, 0x1A]) && RemixedFaces.Person(0x34) == 0x1E
              && RemixedFaces.PersonFaceAt != 0 && RemixedFaces.ChronicleFaceAt != 0);
        return failures;
    }

    /// <summary>The mod's fftpack files: numbered from the mod loader's list (or the known ENTD numbers), and a read's
    /// range given from them.</summary>
    public static int PackFileGiving(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: the mod's fftpack files, {name}{(ok ? "" : ": WRONG")}");
        }
        var root = Path.Combine(Path.GetTempPath(), "ivalice-packfiles-test");
        if (Directory.Exists(root))
            Directory.Delete(root, true);
        var mod = Path.Combine(root, "Mods", "fftivc.gameplay.ivaliceremixed");
        var pack = Path.Combine(mod, "FFTIVC", "data", "enhanced", "fftpack");
        Directory.CreateDirectory(pack);
        Directory.CreateDirectory(Path.Combine(root, "Mods", "fftivc.utility.modloader", "FileLists"));
        File.WriteAllText(Path.Combine(root, "Mods", "fftivc.utility.modloader", "FileLists", "fftpack.txt"),
                          "226|battle_entd3_ent.bin\n892|event_btlevt_bin_entrydata_add_ent.bin\n");
        var entd4 = new byte[0x1000];
        for (var k = 0; k < entd4.Length; k++) entd4[k] = (byte)(k / 0x800 + 1);
        File.WriteAllBytes(Path.Combine(pack, "battle_entd4_ent.bin"), entd4);
        File.WriteAllBytes(Path.Combine(pack, "event_btlevt_bin_entrydata_add_ent.bin"), [7, 7]);
        Directory.CreateDirectory(Path.Combine(pack, "text"));
        PackFiles.Load(mod, pack);
        var buffer = stackalloc byte[0x10];
        var given = PackFiles.Give(227, 1, 0x10, buffer);
        var other = PackFiles.Give(225, 0, 0x10, buffer);
        Check($"numbers {string.Join(" ", PackFiles.Files.Keys.Order())}; ENTD4 from sector 1 given {given} (0x{buffer[0]:X2}), "
              + $"ENTD2 (not the mod's) {other}",
              string.Join(" ", PackFiles.Files.Keys.Order()) == "227 892" && given && buffer[0] == 2 && !other);
        PackFiles.Files.Clear();
        Directory.Delete(root, true);
        return failures;
    }

    /// <summary>ReMixed's phase blocks: 0xD1 n goes past the next 0xD2 n, or on to the END when there's none.</summary>
    public static int RemixedPhaseBlocks(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: ReMixed's phase blocks, {name}{(ok ? "" : ": WRONG")}");
        }
        // D1 02; D2 00; F1 1e 00; D2 02; F1 3c 00; D2 ff; DB
        var code = stackalloc byte[] { 0xD1, 0x02, 0xD2, 0x00, 0xF1, 0x1E, 0x00, 0xD2, 0x02, 0xF1, 0x3C, 0x00, 0xD2, 0xFF, 0xDB };
        var found = EventHooks.PhaseBlock(code, 0, 15);
        code[1] = 0xCC;
        var missing = EventHooks.PhaseBlock(code, 0, 15);
        Check($"phase 2 goes on at {found}, a missing phase at {missing} (the END)", found == 9 && missing == 14);
        return failures;
    }

    /// <summary>ReMixed's roster swap (0x80160600): two slots trade their records but for byte +1.</summary>
    public static int RemixedRosterSwap(Log log)
    {
        var roster = new byte[3 * UnitTables.RosterSize];
        for (var k = 0; k < roster.Length; k++)
            roster[k] = (byte)(k / UnitTables.RosterSize + 1);
        (uint, uint)? swapped, outside;
        fixed (byte* p = roster)
        {
            swapped = RemixedEvents.SwapRoster(p, Ps1Memory.Roster + 2 * Ps1Memory.RosterSize, Ps1Memory.Roster);
            outside = RemixedEvents.SwapRoster(p, 0x80057000, Ps1Memory.Roster);
        }
        var size = UnitTables.RosterSize;
        var ok = swapped == (2, 0) && outside == null && roster[0] == 3 && roster[1] == 1 && roster[size - 1] == 3
                 && roster[2 * size] == 1 && roster[2 * size + 1] == 3 && roster[size] == 2;
        log.Info($"self-test: ReMixed's roster swap, slots 2 and 0 trade all but byte +1{(ok ? "" : ": WRONG")}");
        return ok ? 0 : 1;
    }

    /// <summary>Mirror Match's copy of the party (0x0C4, <see cref="RemixedEvents.MirrorCopy"/>): Ramza (event id 1, unit
    /// 16) and a generic (0x78, unit 17) copied over Gogo's units 0 and 1 in the remaster's layout, each copy keeping its
    /// own index, team and battle id; a third copy whose source isn't there is left as it is; addresses outside the units
    /// copy nothing.</summary>
    public static int RemixedMirrorCopy(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Mirror Match's copy, {name}{(ok ? "" : ": WRONG")}");
        }
        const int size = Battle.UnitSize;
        var units = new byte[Battle.UnitCount * size];
        for (var k = 0; k < units.Length; k++)
            units[k] = (byte)(k * 7 + k / size);  // every unit's bytes different
        void Unit(int slot, byte sprite, byte index, byte team, byte id)
        {
            units[slot * size] = sprite;
            units[slot * size + 1] = index;
            units[slot * size + Battle.UnitCurrentTeam] = team;
            units[slot * size + 0x191] = id;
            units[slot * size + Battle.UnitTeam] = team;
        }
        Unit(16, 1, 16, 0x00, 1);       // Ramza
        Unit(17, 0x80, 17, 0x00, 0x78); // a generic
        units[17 * size + 6] = 0x5F;
        units[17 * size + 0x9A] = 0x10;
        for (var slot = 0; slot < 3; slot++)
            Unit(slot, 0x80, 0xFF, 0x10, (byte)(0x80 + slot));  // Gogo's copies, not on the map yet
        for (var slot = 3; slot < 16; slot++)
            units[slot * size + 0x191] = 0x90;
        for (var slot = 18; slot < Battle.UnitCount; slot++)
            units[slot * size + 0x191] = 0x90;
        var before = (byte[])units.Clone();
        string line, outside;
        fixed (byte* p = units)
        {
            line = RemixedEvents.CopyParty(p, Ps1Memory.Units, Ps1Memory.Units + 2 * Ps1Memory.UnitSize,
                                           k => k switch { 0 => 1, 1 => 0x78, _ => 0x7B });
            outside = RemixedEvents.CopyParty(p, 0x80057F74, Ps1Memory.Units, _ => 1);
        }
        bool Same(int to, int from, int start, int end) =>
            units.AsSpan(to * size + start, end - start + 1).SequenceEqual(before.AsSpan(from * size + start, end - start + 1));
        bool Copied(int to, int from) =>
            units[to * size] == before[from * size] && units[to * size + 3] == before[from * size + 3]
            && units[to * size + 6] == (before[from * size + 6] & 0xE0)
            && Same(to, from, 0x09, 0x99) && units[to * size + 0x9A] == (before[from * size + 0x9A] | 4) && Same(to, from, 0x9B, 0x190)
            && Same(to, from, 0x192, 0x192) && Same(to, from, 0x1FC, 0x1FF) && Same(to, from, 0x1B6, 0x1B7)
            && Same(to, from, 0x1EF, 0x1F2);
        bool Kept(int slot) =>
            Same(slot, slot, 1, 2) && Same(slot, slot, 4, 5) && Same(slot, slot, 7, 8) && Same(slot, slot, 0x191, 0x191)
            && Same(slot, slot, 0x193, 0x1B5) && Same(slot, slot, 0x1B8, 0x1EE) && Same(slot, slot, 0x1F3, 0x1FB);
        Check($"Ramza over unit 0, a generic over unit 1, each keeping its own slot fields: {line}",
              Copied(0, 16) && Copied(1, 17) && Kept(0) && Kept(1) && units[size + 6] == 0x40);
        Check("a copy whose source isn't there is left as it is", Same(2, 2, 0, size - 1));
        Check($"the party itself untouched; {outside}",
              Same(16, 16, 0, size - 1) && Same(17, 17, 0, size - 1) && outside.Contains("nothing copied"));
        return failures;
    }

    /// <summary>ReMixed's 0xE0: the remaster's unit making found where the decompile has it, and ReMixed's record built
    /// from the parameters (a special character, a generic enemy), from roster slot 0, and a roster search finding
    /// nothing.</summary>
    public static int RemixedCreateUnit(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: ReMixed's 0xE0, {name}{(ok ? "" : ": WRONG")}");
        }
        CreateUnit.Find(game.Image);
        var places = $"0x{CreateUnit.MakeAt - game.Image.ImageStart:X} 0x{CreateUnit.GraphicAt - game.Image.ImageStart:X} "
                     + $"0x{CreateUnit.FinishAt - game.Image.ImageStart:X} 0x{CreateUnit.CurrentAt - game.Image.ImageStart:X}";
        Check($"the unit making, its graphic and finishing steps, the current record: {places}",
              places == "0x275CA4 0x278EDC 0x278E0C 0x11A3868");
        var memory = new Dictionary<uint, int>();
        int Read(uint address) => memory.GetValueOrDefault(address);
        var record = new byte[CreateUnit.RecordSize];
        // 0x0A4's first: Ramza's job and sprite set (1), male, unit id 1 in slot 0 at (5, 1).
        var (made, _) = CreateUnit.Build(Convert.FromHexString("018001000000000005010040"), record, Read);
        Check($"a special character from its parameters: {Convert.ToHexString(record)}", made
              && Convert.ToHexString(record) == "01800164FEFEFEFE000001FEFE01FE01FE01FEFEFEFEFE0000000000FEFF00000000000000000000");
        // 0x103's: a Knight (0x4C), male, team 1 (p1 0x81).
        (made, _) = CreateUnit.Build(Convert.FromHexString("4C8181060000000006010240"), record, Read);
        Check($"a generic enemy from its parameters: sprite set 0x{record[0]:X2}, gender 0x{record[1]:X2}, name 0x{record[2]:X2}, "
              + $"job 0x{record[0xA]:X2}, team byte 0x{record[0x18]:X2}",
              made && record[0] == 0x80 && record[1] == 0x80 && record[2] == 0xFF && record[0xA] == 0x4C && record[0x18] == 0x10);
        // 0x104's: roster slot 0 (Ramza, level 12, name id 1) as unit 0x9A.
        memory[Ps1Memory.Roster] = 1;
        memory[Ps1Memory.Roster + 4] = 0x80;
        memory[Ps1Memory.Roster + 0x16] = 12;
        memory[Ps1Memory.Roster + 0xCE] = 1;
        memory[Ps1Memory.Roster + 2] = 1;
        var (fromRoster, source) = CreateUnit.Build(Convert.FromHexString("00009A100000000009080002"), record, Read);
        Check($"roster slot 0: sprite set 0x{record[0]:X2}, gender 0x{record[1]:X2}, name 0x{record[2]:X2}, level {record[3]}, "
              + $"job 0x{record[0xA]:X2}, flags 0x{record[0x18]:X2}, from 0x{source:X8}",
              fromRoster && source == Ps1Memory.Roster && record[0] == 1 && record[1] == 0x80 && record[2] == 1 && record[3] == 12
              && record[0xA] == 1 && record[0x18] == 0xE);
        // A roster search (no flags) for sprite set 0x30, which no slot has.
        var (found, _) = CreateUnit.Build(Convert.FromHexString("300000020000000000000000"), record, Read);
        Check($"a roster search finding nothing makes nothing ({found}); graphics 0xB5 0x{CreateUnit.Graphic(0xB5, 0x40):X2}, "
              + $"0xB6 female 0x{CreateUnit.Graphic(0xB6, 0x40):X2}, 0x90 male 0x{CreateUnit.Graphic(0x90, 0x80):X2}",
              !found && CreateUnit.Graphic(0xB5, 0x40) == 0x83 && CreateUnit.Graphic(0xB6, 0x40) == 0x85
              && CreateUnit.Graphic(0x90, 0x80) == 0x60);
        return failures;
    }

    /// <summary>The Algus campaign's pieces: a PS1 roster name (0x0FD's "Ramza") read as the remaster's, and the level
    /// change's checks (an address outside the battle units, no change).</summary>
    public static int RemixedAlgusPieces(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: the Algus campaign, {name}{(ok ? "" : ": WRONG")}");
        }
        byte[] ramza = [0x1B, 0x24, 0x30, 0x3D, 0x24, 0xFE, 0x00];
        var name = Ps1Memory.Name(k => ramza[k]);
        byte[] spaced = [0x0A, 0x2F, 0x2A, 0x38, 0x36, 0xFA, 0x01, 0xFF];
        var other = Ps1Memory.Name(k => spaced[k]);
        Check($"0x0FD's name bytes read \"{name}\", another \"{other}\"", name == "Ramza" && other == "Algus 1");
        var units = stackalloc byte[Battle.UnitCount * Battle.UnitSize];
        units[Battle.UnitSize + Battle.UnitLv] = 7;
        var outside = RemixedEvents.ChangeLevel(units, 0x80057F74, 2, false);
        var none = RemixedEvents.ChangeLevel(units, Ps1Memory.Units + Ps1Memory.UnitSize, 0, true);
        Check($"the level change: {outside}; {none}", outside.Contains("isn't a battle unit") && none.Contains("stays at level 7"));
        return failures;
    }

    /// <summary>ReMixed's own battle-condition types (0x14/0x15 a unit's status, 0x25 a team on a tile, 0x26/0x27 a
    /// unit's finished turns) on made-up units, and a walk over a set with them stepped by ReMixed's lengths.</summary>
    public static int RemixedConditionTypes(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: ReMixed's condition types, {name}{(ok ? "" : ": WRONG")}");
        }
        var units = stackalloc byte[Battle.UnitCount * Battle.UnitSize];
        new Span<byte>(units, Battle.UnitCount * Battle.UnitSize).Clear();
        for (var k = 0; k < Battle.UnitCount; k++)
            units[k * Battle.UnitSize + 1] = 0xFF;
        // Unit 10: Hirame (battle id 0x93), Dead (status 2: the first byte's 0x20); unit 3: a team-1 unit on (0, 4).
        var hirame = units + 10 * Battle.UnitSize;
        hirame[1] = 10;
        hirame[0] = 0x59;
        hirame[0x191] = 0x93;
        hirame[0x61] = 0x20;
        var enemy = units + 3 * Battle.UnitSize;
        enemy[1] = 3;
        enemy[0] = 0x82;
        enemy[0x191] = 0x82;
        enemy[0x4F] = 0;
        enemy[0x50] = 4;
        enemy[0x1EE] = 0x10;
        long Test(params short[] record)
        {
            fixed (short* r = record)
                return RemixedScenarios.RemixedCheck(units, r);
        }
        var dead = Test(0x14, 0x93, 2);
        var notDead = Test(0x15, 0x93, 2);
        var crystal = Test(0x14, 0x93, 1);
        var nobody = Test(0x14, 0x55, 2);
        hirame[0x61] = 0;
        hirame[0x62] = 0x01; // Treasure, status 15
        var treasure = Test(0x14, 0x93, 15);
        var revived = Test(0x15, 0x93, 2);
        Check($"Hirame Dead: has Dead {dead}, lacks it {notDead}, Crystal {crystal}, nobody {nobody}; a Treasure {treasure}, "
              + $"revived {revived}", dead == 1 && notDead == 0 && crystal == 0 && nobody == 0 && treasure == 1 && revived == 1);
        var team1 = Test(0x25, 1, 0, 4, 0);
        var team0 = Test(0x25, 0, 0, 4, 0);
        var anyEnemy = Test(0x25, 4, 0, 4, 0);
        var elsewhere = Test(0x25, 1, 1, 4, 0);
        Check($"a team on a tile: team 1 {team1}, team 0 {team0}, any enemy {anyEnemy}, another tile {elsewhere}",
              team1 == 1 && team0 == 0 && anyEnemy == 1 && elsewhere == 0);
        // Araguay's absent save-backed Boco leaves battle id 0x80 in a zeroed slot. An HP-at-most-zero condition
        // about that placeholder must be false; once it has an actual unit record, the stock checker owns the test.
        var emptyActor = units;
        emptyActor[0x191] = 0x80;
        var absentHp = Test(6, 0x80, 0);
        emptyActor[0] = 0x82;
        var presentHp = Test(6, 0x80, 0);
        emptyActor[0] = 0;
        Check($"an absent roster-backed actor cannot satisfy HP <= 0: absent {absentHp}, present delegates {presentHp}",
              absentHp == 0 && presentHp == -1);
        // 1-A's Agrias, ReMixed's 0x22: the scene's actor (unit 11, sprite set 0x1E, battle id 0x90, off the field) and the
        // formation's (unit 16, battle id 0x78), Dead.
        var actor = units + 11 * Battle.UnitSize;
        actor[0] = 0x1E;
        actor[0x191] = 0x90;
        var agrias = units + 16 * Battle.UnitSize;
        agrias[1] = 0x10;
        agrias[0] = 0x1E;
        agrias[0x191] = 0x78;
        agrias[0x61] = 0x20;
        var agriasDead = Test(0x14, 0x22, 2);
        var acting = RemixedScenarios.GameUnitId(units, 0xB, 0x22);
        var falls = RemixedScenarios.GameUnitId(units, 6, 0x22);
        var otherType = RemixedScenarios.GameUnitId(units, 0x16, 0x22);
        var hirameId = RemixedScenarios.GameUnitId(units, 6, 0x93);
        agrias[1] = 0xFF;
        var gone = RemixedScenarios.GameUnitId(units, 6, 0x22);
        var goneDead = Test(0x14, 0x22, 2);
        Check($"1-A's Agrias (0x22) as the formation's 0x1E: Dead {agriasDead}; the acting unit 0x{acting:X}, HP 0x{falls:X}, "
              + $"another type 0x{otherType:X}, Hirame 0x{hirameId:X}; off the field 0x{gone:X}, Dead {goneDead}",
              agriasDead == 1 && acting == 0x78 && falls == 0x78 && otherType == 0x22 && hirameId == 0x93 && gone == 0x22
              && goneDead == 0);
        // Its events' instructions name her 0x22 too (her boxes' faces, her poses): the event unit lookup takes the
        // port's 0x1E when no unit is 0x22, and leaves 0x22 to a scene's actor that has it, and other ids alone.
        // (the formation's Agrias is off the field here: unit 16's +1 is 0xFF)
        var ownLookup = RemixedScenarios.EventUnitId(units, 0x22);
        agrias[1] = 0x10;
        var lookedUp = RemixedScenarios.EventUnitId(units, 0x22);
        var otherId = RemixedScenarios.EventUnitId(units, 0x93);
        actor[0x191] = 0x22;
        var sceneActor = RemixedScenarios.EventUnitId(units, 0x22);
        actor[0x191] = 0x90;
        agrias[1] = 0xFF;
        Check($"1-A's Agrias in event instructions: 0x22 looked up as 0x{lookedUp:X} (the formation's), with none there as "
              + $"0x{ownLookup:X} (not the off-field actor), 0x93 as 0x{otherId:X}, with a unit of battle id 0x22 as 0x{sceneActor:X}",
              lookedUp == 0x78 && ownLookup == 0x22 && otherId == 0x93 && sceneActor == 0x22);
        // 1-A's second squad: Bryn's line names her battle-data place (0x91, off the field: +1 0xFF), where the formation
        // put her as a party unit (0x7A, the same name id 0x69, her locked look 0xB5).
        var place = units + 12 * Battle.UnitSize;
        var bryn = units + 17 * Battle.UnitSize;
        place[0] = 0x81; place[1] = 0xFF; place[0x191] = 0x91; *(ushort*)(place + 0x1FC) = 0x69;
        bryn[0] = 0xB5; bryn[1] = 0x11; bryn[0x191] = 0x7A; *(ushort*)(bryn + 0x1FC) = 0x69;
        var brynLine = RemixedScenarios.EventUnitId(units, 0x91);
        var brynNamed = RemixedScenarios.EventUnitId(units, 0x9A);   // ReMixed's 0x191 gives her 0x9A by name
        var brynCondition = RemixedScenarios.GameUnitId(units, 5, 0x9A);
        var lorneNamed = RemixedScenarios.EventUnitId(units, 0x9B);  // no Lorne in the party here
        *(ushort*)(bryn + 0x1FC) = 0x6A;
        var otherName = RemixedScenarios.EventUnitId(units, 0x91);
        *(ushort*)(bryn + 0x1FC) = 0x69;
        place[1] = 0x0C;
        var placeOnField = RemixedScenarios.EventUnitId(units, 0x91);
        new Span<byte>(place, Battle.UnitSize).Clear();
        new Span<byte>(bryn, Battle.UnitSize).Clear();
        Check($"1-A's second squad in event instructions: Bryn's place 0x91 looked up as 0x{brynLine:X} (her party unit), "
              + $"with no party unit of her name 0x{otherName:X}, with the place on the field 0x{placeOnField:X}",
              brynLine == 0x7A && otherName == 0x91 && placeOnField == 0x91);
        Check($"1-A's battles' ids by name (ReMixed's 0x191): 0x9A looked up as 0x{brynNamed:X} and as a condition's "
              + $"0x{brynCondition:X} (Bryn's party unit), 0x9B with no Lorne 0x{lorneNamed:X}",
              brynNamed == 0x7A && brynCondition == 0x7A && lorneNamed == 0x9B);
        RemixedScenarios.ResetVictoryObservation();
        enemy[1] = 0xFF;
        var emptyVictory = Test(0x16);
        enemy[1] = 3;
        var enemiesLive = Test(0x16);
        enemy[0x61] = 0x20;
        var enemiesDead = Test(0x16);
        enemy[0x61] = 0;
        Check($"plain victory before enemies are initialized {emptyVictory}, with one live {enemiesLive}, then Dead {enemiesDead}",
              emptyVictory == 0 && enemiesLive == 0 && enemiesDead == 1);
        var lengths = RemixedScenarios.RemixedRecordLengths;
        Check($"its record lengths: 0x14 {lengths[0x14]}, 0x25 {lengths[0x25]}, 0x26 {lengths[0x26]}, 0x19 {lengths[0x19]}",
              lengths[0x14] == 2 && lengths[0x25] == 4 && lengths[0x26] == 2 && lengths[0x19] == 2);
        return failures;
    }

    /// <summary>ReMixed's condition sets walked with the Nemesis's return right after the opening wherever the build put
    /// it (1-A's rescue, preset 0xEF, with the set the build adds last), so the others keep the numbers their events pick
    /// blocks by; and an event run again by a set while it's the one loaded is loaded afresh, for its text.</summary>
    public static int RemixedWalkOrder(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: ReMixed's condition walk, {name}{(ok ? "" : ": WRONG")}");
        }
        var sets = stackalloc short[16];
        var records = stackalloc short[0x100];
        var used = 0;
        void Put(int k, params short[] words)
        {
            sets[k] = (short)(2 * used);
            for (var i = 0; i < words.Length; i++)
                records[used + i] = words[i];
            used += words.Length;
        }
        string Order(bool remixed)
        {
            var order = stackalloc int[16];
            var n = RemixedScenarios.WalkOrder(sets, records, remixed, order);
            var text = new List<string>();
            for (var k = 0; k < n; k++)
                text.Add(order[k].ToString());
            return string.Join(" ", text);
        }
        // Preset 0xEF as ReMixed has it, then the Nemesis's return as the build adds it.
        Put(0, 1, 0x1FD, 1, 0x19, 0x196);                    // the opening
        Put(1, 1, 0x8E, 1, 0x19, 0x352);                     // the post-battle run: block 1
        Put(2, 1, 0x82, 0, 1, 0x81, 0, 0x14, 0x93, 15, 0x19, 0x2F1);  // Hirame a Treasure: block 2
        Put(3, 1, 0x82, 0, 1, 0x81, 0, 0x14, 0x93, 1, 0x19, 0x2F1);   // Crystal: block 3
        Put(4, 1, 0x81, 0, 1, 0x82, 0, 0x15, 0x93, 2, 0x19, 0x2F1);   // revived: block 4
        Put(5, 0x16, 0x19, 0x2F1);                           // the victory: block 5
        var without = Order(true);
        Put(6, 1, 0x8F, 0, 0x19, 0x1B5);                     // the Nemesis's return, last
        var added = Order(true);
        var others = Order(false);
        // ReMixed's own presets: the menu's set second, turned to the return.
        sets[6] = 0;
        used = 0;
        Put(0, 1, 0x1FD, 1, 0x19, 0x1EC);
        Put(1, 1, 0x8F, 0, 0x19, 0x1B5);
        Put(2, 0x16, 0x19, 0x1EC);
        sets[3] = sets[4] = sets[5] = 0;
        var own = Order(true);
        Check($"1-A's rescue: {without}; with the return added last {added}, outside ReMixed's scenarios {others}; "
              + $"ReMixed's own with it second {own}",
              without == "0 1 2 3 4 5" && added == "0 6 1 2 3 4 5" && others == "0 1 2 3 4 5 6" && own == "0 1 2");
        var savedMessages = RemixedScenarios.Messages.ToDictionary(p => p.Key, p => p.Value);
        try
        {
            RemixedScenarios.Messages.Clear();
            RemixedScenarios.Messages[850] = (100, 7);
            var loaded = 850;
            var again = RemixedScenarios.ReloadOnRerun(850, &loaded) && loaded == -1;
            loaded = 753;
            var another = !RemixedScenarios.ReloadOnRerun(850, &loaded) && loaded == 753;
            loaded = 2;
            var remasters = !RemixedScenarios.ReloadOnRerun(2, &loaded) && loaded == 2;
            var none = !RemixedScenarios.ReloadOnRerun(850, null);
            Check($"ReMixed's event run again while loaded: loaded afresh {again}; another loaded left {another}; the "
                  + $"remaster's own left {remasters}; no loaded number {none}", again && another && remasters && none);
        }
        finally
        {
            RemixedScenarios.Messages.Clear();
            foreach (var (k, v) in savedMessages)
                RemixedScenarios.Messages[k] = v;
        }
        return failures;
    }

    /// <summary>A battle played after its scene: its second group as the build marks it (one ReMixed's code sets under an
    /// IF) only when the scene's run set var 0x36; an unmarked one always.</summary>
    public static int MenuBattleGroups(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: a battle after its scene, {name}{(ok ? "" : ": WRONG")}");
        }
        var marked = (ushort)(RemixedScenarios.SecondGroupMark | 0x2FE);
        var set = RemixedScenarios.SecondGroup(marked, 0x157);
        var unset = RemixedScenarios.SecondGroup(marked, 0);
        var plain = RemixedScenarios.SecondGroup(0x2FD, 0);
        var none = RemixedScenarios.SecondGroup(0, 0x157);
        Check($"its second group: marked with var 0x36 set 0x{set:X}, unset 0x{unset:X}; unmarked 0x{plain:X}; none 0x{none:X}",
              set == 0x2FE && unset == 0 && plain == 0x2FD && none == 0);
        return failures;
    }

    /// <summary>ReMixed's event text: a MESSAGE of the running event moved onto its line once, others left alone.</summary>
    public static int RemixedMessageMoving(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: ReMixed's event text, {name}{(ok ? "" : ": WRONG")}");
        }
        var saved = RemixedScenarios.RunningEvent;
        var running = 492;
        RemixedScenarios.RunningEvent = &running;
        RemixedScenarios.Messages[492] = (53015, 2);
        // The fete's MESSAGE as the build converts it and 0xE6 fills it in (var 0x71: 2): 10 10 70 <Id 0x40002> 80 ...
        var message = stackalloc byte[] { 0x10, 0x10, 0x70, 0x02, 0x00, 0x04, 0x00, 0x80, 0x00 };
        var change = stackalloc byte[] { 0x51, 0x01, 0x01, 0x00, 0x04, 0x00, 0x00, 0x00 };
        var none = stackalloc byte[] { 0x51, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00 };
        var other = stackalloc byte[] { 0x11, 0x10, 0x70, 0x01, 0x00, 0x04, 0x00 };
        var past = stackalloc byte[] { 0x10, 0x10, 0x70, 0x03, 0x00, 0x04, 0x00 };
        RemixedScenarios.MoveMessage(message);
        var first = *(uint*)(message + 3);
        RemixedScenarios.MoveMessage(message);
        RemixedScenarios.MoveMessage(change);
        RemixedScenarios.MoveMessage(none);
        RemixedScenarios.MoveMessage(other);
        RemixedScenarios.MoveMessage(past);
        running = 2;
        var elsewhere = stackalloc byte[] { 0x10, 0x10, 0x70, 0x01, 0x00, 0x04, 0x00 };
        RemixedScenarios.MoveMessage(elsewhere);
        Check($"message 2 as line {first}, again {*(uint*)(message + 3)}, the unit byte 0x{message[7]:X2}; 0x51's message 1 "
              + $"as {*(uint*)(change + 2)}, its none 0x{*(uint*)(none + 2):X}; not a MESSAGE 0x{*(uint*)(other + 3):X}, past "
              + $"the count 0x{*(uint*)(past + 3):X}, another event's 0x{*(uint*)(elsewhere + 3):X}",
              first == 315161 && *(uint*)(message + 3) == 315161 && message[7] == 0x80 && *(uint*)(change + 2) == 315160
              && *(uint*)(none + 2) == 0xFFFFFFFF && *(uint*)(other + 3) == 0x40001 && *(uint*)(past + 3) == 0x40003
              && *(uint*)(elsewhere + 3) == 0x40001);
        RemixedScenarios.Messages.Clear();
        RemixedScenarios.RunningEvent = saved;
        return failures;
    }

    /// <summary>ReMixed's scenarios: the tables and the start found where the decompile has them, the battle-entry
    /// loader reading more, and a record and a formation added the way the build's file adds them.</summary>
    public static int RemixedScenarioTables(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: ReMixed's scenarios, {name}{(ok ? "" : ": WRONG")}");
        }
        if (RemixedScenarios.Scenarios == null)
        {
            Check("aren't installed", false);
            return failures;
        }
        var places = $"0x{(nint)RemixedScenarios.Scenarios - game.Image.ImageStart:X} "
                     + $"0x{(nint)RemixedScenarios.Formations - game.Image.ImageStart:X} "
                     + $"0x{(nint)RemixedScenarios.StartScenario - game.Image.ImageStart:X} "
                     + $"0x{(nint)RemixedScenarios.RunningEvent - game.Image.ImageStart:X} "
                     + $"0x{(nint)RemixedScenarios.PartyLocation - game.Image.ImageStart:X} "
                     + $"0x{RemixedScenarios.WalkAt - game.Image.ImageStart:X} 0x{RemixedScenarios.CheckAt - game.Image.ImageStart:X}";
        Check($"the scenario table, the formation table, the start, the running event, the party's location, the condition "
              + $"walker and its checker: {places}",
              places == "0x8ECAB0 0x7AE2D0 0x2D28C8 0x782A94 0xD493E8 0x2391C4 0x239294");
        var hooks = $"0x{RemixedScenarios.SetupAt - game.Image.ImageStart:X} "
                    + $"0x{RemixedScenarios.ButtonsAt - game.Image.ImageStart:X} "
                    + $"0x{PackFiles.ReadFileAt - game.Image.ImageStart:X} 0x{RemixedScenarios.LoadEventAt - game.Image.ImageStart:X} "
                    + $"0x{RemixedScenarios.WorldFlags - game.Image.ImageStart:X} 0x{RemixedScenarios.WorldPressed - game.Image.ImageStart:X} "
                    + $"0x{RemixedScenarios.RumorShownAt - game.Image.ImageStart:X} "
                    + $"0x{RemixedScenarios.BarListEventAt - game.Image.ImageStart:X} "
                    + $"0x{(nint)RemixedScenarios.RumorUnread - game.Image.ImageStart:X} "
                    + $"0x{RemixedScenarios.WorldHeld - game.Image.ImageStart:X} 0x{RemixedScenarios.WorldHeldBefore - game.Image.ImageStart:X}";
        Check($"the setup, the world's buttons, flags and pressed buttons, the bar's check of a rumor, its list events, "
              + $"the rumors' unread marks, the buttons held and held before: {hooks}",
              hooks == "0x2FB7C8 0x2CC980 0x2F40C4 0x2340A4 0x1D32E14 0xD4CC18 0x36F53C 0x19D548 0x11A7299 0xD4C96C 0xD4CC14");
        var world = $"0x{(nint)RemixedScenarios.WorldStart - game.Image.ImageStart:X} "
                    + $"0x{(nint)RemixedScenarios.WorldEvent - game.Image.ImageStart:X} "
                    + $"0x{(nint)RemixedScenarios.WorldMode - game.Image.ImageStart:X} "
                    + $"0x{(nint)RemixedScenarios.WorldDepth - game.Image.ImageStart:X} "
                    + $"0x{(nint)RemixedScenarios.WorldStates - game.Image.ImageStart:X}";
        Check($"the world map's start of a story event, its event, mode, state depth and states: {world}",
              world == "0x247E10 0xD493CC 0xD493D0 0xD8DBA0 0xD4C948");
        Check($"the unit-name lookup the name guard wraps: 0x{NameGuard.At - game.Image.ImageStart:X}",
              NameGuard.At - game.Image.ImageStart == 0x1E8B7C);
        Check($"the battle entry's records the mod's own entries replace: 0x{RemixedScenarios.EntryRecordsAt - game.Image.ImageStart:X}",
              RemixedScenarios.EntryRecordsAt - game.Image.ImageStart == 0x28416C);
        var empty = stackalloc byte[0x200];
        var ramza = stackalloc byte[0x200];
        ramza[0] = 1;
        ramza[3] = 0x53;
        Check($"the roster write-back the roster guard wraps: 0x{RosterGuard.At - game.Image.ImageStart:X}; an empty unit "
              + $"skipped {RosterGuard.Empty(empty)}, Ramza's {RosterGuard.Empty(ramza)}",
              RosterGuard.At - game.Image.ImageStart == 0x274EB8 && RosterGuard.Empty(empty) && !RosterGuard.Empty(ramza));
        var recruit = stackalloc byte[0x200];
        recruit[0] = 0x81;
        recruit[6] = 0x53;
        var joined = RosterGuard.Joined(recruit);
        var again = RosterGuard.Joined(recruit);
        Check($"the event's join now the roster guard wraps: 0x{RosterGuard.JoinNowAt - game.Image.ImageStart:X}; a recruit "
              + $"(flags 0x53) it writes keeps flags 0x{recruit[6]:X2} ({joined}, then {again})",
              RosterGuard.JoinNowAt - game.Image.ImageStart == 0x227458 && joined && !again && recruit[6] == 0x43);

        var count = 0;
        while (*(ushort*)(RemixedScenarios.Scenarios + count * RemixedScenarios.ScenarioSize) != 0)
            count++;
        var record = new byte[RemixedScenarios.ScenarioSize];
        new ReadOnlySpan<byte>(RemixedScenarios.Scenarios, record.Length).CopyTo(record);
        record[0] = 0xEC;
        record[1] = 0x01;
        var formation = new byte[RemixedScenarios.FormationSize];
        formation[0] = 0x42;
        formation[RemixedScenarios.FormationId] = 0xAA;
        formation[RemixedScenarios.FormationId + 1] = 0x01;
        var data = new List<byte> { 1, 0 };
        data.AddRange(record);
        data.AddRange([1, 0]);
        data.AddRange(formation);
        var savedFormation = new byte[RemixedScenarios.FormationSize];
        var formationAt = RemixedScenarios.Formations + 0x1AA * RemixedScenarios.FormationSize;
        new ReadOnlySpan<byte>(formationAt, savedFormation.Length).CopyTo(savedFormation);
        var added = RemixedScenarios.Add(data.ToArray());
        var last = RemixedScenarios.Scenarios + count * RemixedScenarios.ScenarioSize;
        var appended = *(ushort*)last == 0x1EC && *(ushort*)(last + RemixedScenarios.ScenarioSize) == 0;
        RemixedScenarios.Add(data.ToArray());
        var once = *(ushort*)(last + RemixedScenarios.ScenarioSize) == 0;
        var put = formationAt[0] == 0x42 && *(ushort*)(formationAt + RemixedScenarios.FormationId) == 0x1AA;
        Check($"{count} records; 0x1EC added at the end ({appended}), once when added again ({once}); formation 0x1AA "
              + $"written ({put}); counted {added}", appended && once && put && added == (1, 1));
        // The 1-A offer's hand-off into a record's word bytes (+0x12 low, +0x13 high, +0x14 mode).
        var handed = RemixedScenarios.SetHandOff(0x1EC, 0x193, 0x81);
        var missing = RemixedScenarios.SetHandOff(0x7FFF, 0x193, 0x81);
        Check($"the hand-off 0x1EC → 0x193 written ({handed}): {last[0x12]:X2} {last[0x13]:X2} {last[0x14]:X2}; a missing "
              + $"scenario's refused ({!missing})", handed && !missing && last[0x12] == 0x93 && last[0x13] == 1 && last[0x14] == 0x81);
        // ReMixed's missions in the taverns: three postings in a made-up file (the sections before them empty), which the
        // bar lists from made-up variables, and which one is under a made-up rumor list's cursor.
        static byte[] Words(params int[] values) => values.SelectMany(v => BitConverter.GetBytes((ushort)v)).ToArray();
        RemixedScenarios.Add([.. Words(0, 0, 0, 0, 0, 0, 0, 3), .. Words(23, 0x1ED, 15, 0x1B7), .. Words(27, 0x1F1, 24, 0),
                              .. Words(28, 0x1EC, 31, 0), .. Words(0, 1, 42, 0x22, 0x2ED, 0xAE, 0xAF)]);
        var posted = RemixedScenarios.Postings.Count;
        var conditional = RemixedScenarios.ConditionalRumors.Count;
        var vars = new Dictionary<int, int>();
        var read = RemixedScenarios.ReadVariable;
        RemixedScenarios.ReadVariable = id => vars.GetValueOrDefault(id);
        var listed = (delegate* unmanaged<int, int, byte>)&RemixedScenarios.RumorShown;
        vars[RemixedScenarios.StoryProgress] = 24;
        var midway = (listed(23, 11), listed(27, 11), listed(28, 11));
        vars[RemixedScenarios.StoryProgress] = 4;
        var early = (listed(23, 6), listed(27, 6));
        vars[0x1B7] = 1;
        var afterBiggs = listed(23, 6);
        vars[RemixedScenarios.StoryProgress] = 0x22;
        vars[0x2ED] = 1;
        var branchOpen = listed(42, 15);
        vars[0xAE] = 1;
        var hirameLost = listed(42, 15);
        vars[0xAE] = 0;
        vars[0x2ED] = 0;
        var branchTaken = listed(42, 15);
        RemixedScenarios.ReadVariable = read;
        int[] ids = [0, 1, 27, 23];
        var list = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(RemixedScenarios.ListEntriesAt + 16);
        var entries = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)(ids.Length * RemixedScenarios.ListEntryBytes));
        for (var i = 0; i < ids.Length; i++)
            *(int*)(entries + i * RemixedScenarios.ListEntryBytes) = ids[i];
        *(nint*)(list + RemixedScenarios.ListEntriesAt) = (nint)entries;
        *(nint*)(list + RemixedScenarios.ListEntriesAt + 8) = (nint)(entries + ids.Length * RemixedScenarios.ListEntryBytes);
        var savedList = RemixedScenarios.RumorList;
        RemixedScenarios.RumorList = (nint)list;
        int Under(int cursor)
        {
            *(int*)(list + RemixedScenarios.ListCursorAt) = cursor;
            return RemixedScenarios.PostingAt();
        }
        var (onHeirloom, onRumor, pastEnd) = (Under(2), Under(1), Under(4));
        RemixedScenarios.RumorList = savedList;
        System.Runtime.InteropServices.NativeMemory.Free(list);
        System.Runtime.InteropServices.NativeMemory.Free(entries);
        Check($"the rumor list's cursor on Lost Heirloom's posting gives posting {onHeirloom}, on one of the game's rumors "
              + $"{onRumor}, past the list's end {pastEnd}", onHeirloom == 1 && onRumor == -1 && pastEnd == -1);
        RemixedScenarios.Postings.Clear();
        RemixedScenarios.ConditionalRumors.Clear();
        Check($"{posted} postings read; at progress 24 Chocobo Defense's {midway.Item1}, Lost Heirloom's {midway.Item2}, "
              + $"The Ball's {midway.Item3}; at progress 4 Chocobo Defense's {early.Item1} (Lost Heirloom's {early.Item2}), "
              + $"after Biggs and Wedge {afterBiggs}",
              posted == 3 && midway == (1, 1, 0) && early == (0, 0) && afterBiggs == 1);
        Check($"the Fort Zeakden rumor: {conditional} conditional row, branch open {branchOpen}, Hirame lost {hirameLost}, "
              + $"branch taken {branchTaken}",
              conditional == 1 && branchOpen == 1 && hirameLost == 0 && branchTaken == 0);
        new Span<byte>(last, RemixedScenarios.ScenarioSize).Clear();
        savedFormation.CopyTo(new Span<byte>(formationAt, savedFormation.Length));
        return failures;
    }

    /// <summary>The formation screen's Start test through a copy of its stub (the game's group index set for each case).</summary>
    public static int FormationStartRule(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: split formations, {name}{(ok ? "" : ": WRONG")}");
        }
        if (FormationStart.Stub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        Check($"the Start test (0x{FormationStart.At - game.Image.ImageStart:X}) jumps to the stub",
              ((byte*)FormationStart.At)[0] == 0xE9 && GameImage.Rip(FormationStart.At, 1, 5) == FormationStart.Stub);
        // push r12; mov r12d, ecx; the copy; start: mov eax, 1 | refuse: xor eax, eax; pop r12; ret.
        var harness = GameImage.AllocateCodeNear(FormationStart.Stub, 48 + FormationStart.StubSize);
        byte[] entry = [0x41, 0x54, 0x44, 0x8B, 0xE1, 0xFF, 0x25, 0, 0, 0, 0];
        new ReadOnlySpan<byte>(entry).CopyTo(new Span<byte>(harness, entry.Length));
        var copy = harness + 48;
        *(long*)(harness + entry.Length) = (long)copy;
        var start = harness + entry.Length + 8;
        byte[] startCode = [0xB8, 1, 0, 0, 0, 0x41, 0x5C, 0xC3];
        new ReadOnlySpan<byte>(startCode).CopyTo(new Span<byte>(start, startCode.Length));
        var refuse = start + startCode.Length;
        byte[] refuseCode = [0x33, 0xC0, 0x41, 0x5C, 0xC3];
        new ReadOnlySpan<byte>(refuseCode).CopyTo(new Span<byte>(refuse, refuseCode.Length));
        FormationStart.WriteStub(copy, FormationStart.Group, (nint)start, (nint)refuse);
        var run = (delegate* unmanaged<int, int>)harness;
        var saved = *FormationStart.Group;
        string Try(int group, int deployed)
        {
            *FormationStart.Group = group;
            return run(deployed) == 1 ? "starts" : "refused";
        }
        var first = Try(0, 0);
        var firstSome = Try(0, 2);
        var second = Try(1, 0);
        *FormationStart.Group = saved;
        Check($"the first group empty {first}, with units {firstSome}; the second group empty {second}",
              first == "refused" && firstSome == "starts" && second == "starts");
        Check($"the remaster's own Start check (0x{FormationStart.SecondGroupAt - game.Image.ImageStart:X}) goes past "
              + "its refusal of an empty second group",
              FormationStart.SecondGroupAt != 0 && *(byte*)FormationStart.SecondGroupAt == 0xEB);
        return failures;
    }

    /// <summary>The formation's six places: each site as patched, then the patched count of units placed and the list's
    /// compaction run on a made-up formation.</summary>
    public static int SixPlacesRule(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: six places, {name}{(ok ? "" : ": WRONG")}");
        }
        if (SixPlaces.Count == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        for (var k = 0; k < SixPlaces.Sites.Length; k++)
        {
            var site = SixPlaces.Sites[k];
            Check($"{site.What} at 0x{SixPlaces.At[k] - game.Image.ImageStart:X}",
                  new ReadOnlySpan<byte>((void*)SixPlaces.At[k], site.Now.Length).SequenceEqual(site.Now));
        }
        var formation = stackalloc byte[0x80];
        var places = (int*)(formation + 0x40);
        var list = (int*)(formation + 0x44);
        void Fill(int count, params int[] entries)
        {
            new Span<byte>(formation, 0x80).Clear();
            *places = count;
            for (var n = 0; n < SixPlaces.Places; n++)
                list[n] = n < entries.Length ? entries[n] : -1;
            *(int*)(formation + 0x5C) = 0x5C5C5C5C; // past the list: left alone
        }
        string List()
        {
            var entries = new int[SixPlaces.Places];
            for (var n = 0; n < entries.Length; n++)
                entries[n] = list[n];
            return string.Join(" ", entries);
        }
        var count = (delegate* unmanaged<byte*, int>)SixPlaces.Count;
        var compact = (delegate* unmanaged<byte*, void>)SixPlaces.Compact;
        Fill(6, 3, -1, 5, -1, 7, 9);
        var six = count(formation);
        Fill(7, 3, -1, 5, -1, 7, 9);
        var seven = count(formation);
        Check($"units placed of six places: {six}; of seven: {seven}", six == 4 && seven == 0);
        Fill(6, 3, -1, 5, -1, 7, 9);
        compact(formation);
        var sixList = List();
        var kept = *(int*)(formation + 0x5C) == 0x5C5C5C5C;
        Fill(5, -1, 4, -1, 8);
        compact(formation);
        var fiveList = List();
        Check($"six places compacted to {sixList} (+0x5C kept: {kept}); five to {fiveList}",
              sixList == "3 5 7 9 -1 -1" && kept && fiveList == "4 8 -1 -1 -1 -1");
        // The battle ids at Commence: the party in 0x10-0x14 numbered 0x78-0x7C by the game, the sixth (roster slot 3)
        // in slot 15 still 0x78, an enemy in slot 5 (no roster slot) and a guest from the roster in slot 11 (slot 50).
        var units = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)(Battle.UnitCount * Battle.UnitSize));
        try
        {
            for (var slot = 0; slot < Battle.UnitCount; slot++)
                units[slot * Battle.UnitSize + SixPlaces.Present] = 0xFF;
            void Unit(int slot, byte sprites, byte roster, byte id)
            {
                var unit = units + slot * Battle.UnitSize;
                unit[0] = sprites;
                unit[SixPlaces.Present] = (byte)slot;
                unit[SixPlaces.RosterSlot] = roster;
                unit[SixPlaces.BattleId] = id;
            }
            Unit(16, 0x1E, 0, 0x78);
            Unit(17, 0x80, 1, 0x79);
            Unit(18, 0x81, 2, 0x7A);
            Unit(19, 0x80, 4, 0x7B);
            Unit(20, 0x81, 5, 0x7C);
            Unit(15, 0x80, 3, 0x78);
            Unit(5, 0x82, 0xFF, 0x85);
            Unit(11, 0x1E, 50, 0x90);
            Fill(6, 0, 1, 2, 4, 5, 3);
            var numbered = SixPlaces.Number(units, list);
            Check($"battle ids at Commence: {string.Join(", ", numbered.Select(n => $"slot {n.Slot} 0x{n.Id:X2}"))}; "
                  + $"the enemy 0x{units[5 * Battle.UnitSize + SixPlaces.BattleId]:X2}, "
                  + $"the guest 0x{units[11 * Battle.UnitSize + SixPlaces.BattleId]:X2}",
                  numbered.Count == 1 && numbered[0] == (15, (byte)0x7D)
                  && units[5 * Battle.UnitSize + SixPlaces.BattleId] == 0x85
                  && units[11 * Battle.UnitSize + SixPlaces.BattleId] == 0x90);
        }
        finally
        {
            System.Runtime.InteropServices.NativeMemory.Free(units);
        }
        return failures;
    }

    /// <summary>The party menu write-back's new copy, run in a harness on a made-up record and roster unit.</summary>
    public static int MonsterWriteBackRule(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: monster write-back, {name}{(ok ? "" : ": WRONG")}");
        }
        // push rbx; push rsi; mov r8, rcx; mov rbx, rdx; xor esi, esi; the copy; pop rsi; pop rbx; ret.
        byte[] code = [0x53, 0x56, 0x4C, 0x8B, 0xC1, 0x48, 0x8B, 0xDA, 0x33, 0xF6, .. InPlace.MonsterWriteBack, 0x5E, 0x5B, 0xC3];
        var harness = GameImage.AllocateCodeNear(game.Image.ImageStart + 0x285744, code.Length);
        new ReadOnlySpan<byte>(code).CopyTo(new Span<byte>(harness, code.Length));
        var run = (delegate* unmanaged<byte*, byte*, void>)harness;
        var record = stackalloc byte[0x80];
        var roster = stackalloc byte[0x20];
        string Copy(bool monster)
        {
            new Span<byte>(record, 0x80).Clear();
            new Span<byte>(roster, 0x20).Fill(0xAA);
            record[0x70] = 0x11;
            record[0x60] = 0x0B;                                 // the secondary (a monster's second action)
            *(ushort*)(record + 0x62) = 0x1A6;                  // reaction (a monster's third action)
            *(ushort*)(record + 0x64) = 0x1D7;
            *(ushort*)(record + 0x66) = 0x1F4;
            record[0x3E] = (byte)(monster ? 1 : 0);
            run(record, roster);
            return Convert.ToHexString(new ReadOnlySpan<byte>(roster + 4, 10));
        }
        var human = Copy(false);
        var monster = Copy(true);
        Check($"a human's +4..+0xD: {human}; a monster's keeps the roster's three: {monster}",
              human == "11AAAA0BA601D701F401" && monster == "11AAAA0BAAAAAAAAAAAA");
        return failures;
    }

    private static readonly List<(int Index, int Moved, int Acted)> _actedCalls = [];
    private static readonly List<int> _stateCalls = [];
    private static int _clockStopped;
    private static byte* _actedUnits;
    private static int _turnsEnded;

    /// <summary>
    /// Stands in for 0x310548: the move and action flags (+0x1B9, +0x1BA), then, once the unit has moved and acted, the
    /// end of turn, which Movement's hook passes to Two Spells before the game's own (so before the queue that follows).
    /// </summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeSetActed(int index, int moved, int acted)
    {
        _actedCalls.Add((index, moved, acted));
        if (_actedUnits == null)
            return 0;
        var unit = _actedUnits + index * Battle.UnitSize;
        if (moved != 0)
            unit[0x1B9] |= 1;
        if (acted != 0)
            unit[0x1BA] = 1;
        if ((unit[0x1B9] & 1) != 0 && unit[0x1BA] != 0)
        {
            _turnsEnded++;
            TwoSpells.TurnEnded(index);
        }
        return 0;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static nint FakeActionState(byte* unit, int state)
    {
        _stateCalls.Add(state & 0xFF);
        return 0;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeStopped(byte* unit) => _clockStopped;

    /// <summary>
    /// Two Spells' turn and second slot (installed here on this copy if the build holds it back): the three patched
    /// sites and their stubs, run through harnesses with stand-ins for the game's routines; the player's two spells
    /// marked and queued the way the battle UI does it; the countdown, the copy-back and a cancelled charge; the
    /// action's start keeping Charging; the AI casting twice; the menu between the two.
    /// </summary>
    public static int TwoSpellsTurn(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Two Spells' turn, {name}{(ok ? "" : ": WRONG")}");
        }
        if (!TwoSpells.Turns)
        {
            try
            {
                TwoSpells.Install(game);
            }
            catch (Exception e)
            {
                Check($"can't be installed: {e.Message}", false);
                return failures;
            }
        }
        var image = game.Image.ImageStart;
        nint Target(nint at) => at + 5 + *(int*)(at + 1);
        var actsStub = (byte*)Target(TwoSpells.ActsAt);
        var startStub = (byte*)Target(TwoSpells.StartAt);
        var tickStub = (byte*)Target(TwoSpells.TickAt);
        Check($"the player's action (0x{TwoSpells.ActsAt - image:X}) calls a stub that passes the battle UI (mov r9, rbx)",
              *(byte*)TwoSpells.ActsAt == 0xE8 && actsStub[0] == 0x49 && actsStub[1] == 0x89 && actsStub[2] == 0xD9 && actsStub[3] == 0xFF);
        Check($"the action's start (0x{TwoSpells.StartAt - image:X}) calls a stub", *(byte*)TwoSpells.StartAt == 0xE8 && startStub[0] == 0xFF);
        var tickBack = -1;
        for (var k = 0; k < 180 && tickBack < 0; k++)
            if (tickStub[k] == 0xC7 && tickStub[k + 1] == 0x05 && *(uint*)(tickStub + k + 6) == 3 && tickStub[k + 10] == 0xE9)
                tickBack = k + 10;
        Check($"the countdown's end (0x{TwoSpells.TickAt - image:X}) jumps to a stub that sets state 3 and jumps back after the game's mov",
              *(byte*)TwoSpells.TickAt == 0xE9 && tickBack > 0 && Target((nint)(tickStub + tickBack)) == TwoSpells.TickAt + 10);

        // Made-up spells (the live table is built from the game's data, which this copy doesn't have).
        *game.RuleCode.AbilitiesReady = 1;
        var saved = new Dictionary<int, byte[]>();
        void Spell(int id, int ct, int mp, int flags5)
        {
            var record = game.RuleCode.GetAbilityAction(id);
            saved.TryAdd(id, new Span<byte>(record, 20).ToArray());
            record[0x0C] = (byte)ct;
            record[0x0D] = (byte)mp;
            record[5] = (byte)flags5;
        }
        const int fire = 0x10, bolt = 0x14, ice = 0x18, persevere = 0x1C, attack = 0x0F0;
        Spell(fire, 4, 6, 0);
        Spell(bolt, 2, 6, 0);
        Spell(ice, 6, 6, 0);
        Spell(persevere, 3, 6, 4);
        Spell(attack, 0, 0, 0);

        const int slot = 6;
        var unit = game.TurnCode.Units + slot * Battle.UnitSize;
        var savedUnit = new Span<byte>(unit, Battle.UnitSize).ToArray();
        var queued = unit + 0x1A0;
        var realActed = TwoSpells.GameSetActed;
        var realState = TwoSpells.GameActionState;
        var realStopped = TwoSpells.GameStopped;
        TwoSpells.GameSetActed = &FakeSetActed;
        TwoSpells.GameActionState = &FakeActionState;
        TwoSpells.GameStopped = &FakeStopped;
        _actedUnits = game.TurnCode.Units;
        var ui = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x400);
        var harness = GameImage.AllocateCode(128);
        // acts(index, moved, acted, ui): push rbx; mov rbx, r9; sub rsp, 0x20; mov rax, the stub; call rax; ...
        var actsCode = new List<byte> { 0x53, 0x4C, 0x89, 0xCB, 0x48, 0x83, 0xEC, 0x20, 0x48, 0xB8 };
        actsCode.AddRange(BitConverter.GetBytes((ulong)actsStub));
        actsCode.AddRange([0xFF, 0xD0, 0x48, 0x83, 0xC4, 0x20, 0x5B, 0xC3]);
        actsCode.ToArray().CopyTo(new Span<byte>(harness, actsCode.Count));
        // call(stub, rdx, r8): sub rsp, 0x28; mov rax, rcx; mov rcx, rdx; mov edx, r8d; call rax; add rsp, 0x28; ret
        var callAt = harness + 64;
        byte[] callCode = [0x48, 0x83, 0xEC, 0x28, 0x48, 0x89, 0xC8, 0x48, 0x89, 0xD1, 0x44, 0x89, 0xC2, 0xFF, 0xD0, 0x48, 0x83, 0xC4, 0x28, 0xC3];
        callCode.CopyTo(new Span<byte>(callAt, callCode.Length));
        // tick(stub): sub rsp, 0x20; call rcx; add rsp, 0x20; ret. The stub then starts 16-aligned, as when the
        // loop jumps to it.
        byte[] tickCode = [0x48, 0x83, 0xEC, 0x20, 0xFF, 0xD1, 0x48, 0x83, 0xC4, 0x20, 0xC3];
        tickCode.CopyTo(new Span<byte>(harness + 96, tickCode.Length));
        var tickRun = (delegate* unmanaged<nint, void>)(harness + 96);
        var acts = (delegate* unmanaged<int, int, int, byte*, int>)harness;
        var call = (delegate* unmanaged<nint, nint, int, nint>)callAt;
        try
        {
            TwoSpells.ResetAll();
            void Fresh(bool innate, bool player)
            {
                new Span<byte>(unit, Battle.UnitSize).Clear();
                unit[1] = slot;
                unit[5] = (byte)(player ? 0x08 : 0);
                unit[Battle.UnitSupports + 1] = (byte)(innate ? 0x20 : 0);
                unit[0x18D] = 0xFF;
                TwoSpells.ResetAll();
            }
            void Choose(int skillset, int ability)
            {
                ui[0x178] = slot;
                ui[0x179] = (byte)skillset;
                *(ushort*)(ui + 0x17A) = (ushort)ability;
            }
            int Act()
            {
                _actedCalls.Clear();
                acts(slot, 0, 1, ui);
                return _actedCalls.Count == 1 && _actedCalls[0].Index == slot ? _actedCalls[0].Acted : -1;
            }
            // The game's commit as far as Two Spells cares: the record into +0x1A0, its CT, "acted", Charging; the
            // Charge skillset's case (0x2818B1) also uses up the move.
            void Commit(int ct)
            {
                TwoSpells.BeforeQueue(ui + 0x178, queued, 1);
                new Span<byte>(ui + 0x178, 20).CopyTo(new Span<byte>(queued, 20));
                unit[0x18D] = (byte)ct;
                unit[0x1BA] = 1;
                if (ui[0x179] == 0x08)
                    unit[0x1B9] |= 1;
                unit[Battle.UnitStatuses] |= 0x08;
                TwoSpells.AfterQueue(ui + 0x178, queued, 1);
            }
            int Main() => *(ushort*)(queued + 2);
            int Slotted() => TwoSpells.SlotCtOf(slot) == 0xFF ? 0 : TwoSpells.SlotRecordOf(slot)[2] | (TwoSpells.SlotRecordOf(slot)[3] << 8);

            Fresh(true, true);
            Choose(0x0B, fire);
            var firstActed = Act();
            Commit(4);
            Check($"a first spell (Fire, CT 4) through the stub: marked with acted {firstActed}, then 'acted' {unit[0x1BA]} after the queue, the pair open",
                  firstActed == 0 && unit[0x1BA] == 0 && TwoSpells.PairOf(slot) == 1);
            // Between the two: the command list and the lists.
            var realUsable = TwoSpells.Usable;
            TwoSpells.Usable = &FakeUsable;
            var list = stackalloc byte[8];
            byte[] before = [1, 0x15, 0x0C, 2, 3, 0xFF];
            unit[0x12] = 0x15;
            unit[Battle.UnitSecondary] = 0x0C;
            before.CopyTo(new Span<byte>(list, before.Length));
            TwoSpells.Insert(slot, list, unit);
            var entries = new List<string>();
            for (var k = 0; k < 7 && list[k] != 0xFF; k++)
                entries.Add($"{list[k]:X2}{(TwoSpells.Unusable[k] != 0 ? "x" : "")}");
            TwoSpells.Usable = realUsable;
            Check($"between the two the commands are {string.Join(" ", entries)} (no Attack, Defend or Equip Change; Math Skill greyed)",
                  string.Join(" ", entries) == "15x 0C 0A 0Bx");
            Check("between the two a list keeps spells and leaves out Persevere ones and abilities without CT or MP",
                  TwoSpells.Shows(unit, 0x0C, bolt) && !TwoSpells.Shows(unit, 0x0C, persevere) && !TwoSpells.Shows(unit, 0x0C, attack));
            unit[0x12] = 0;
            unit[Battle.UnitSecondary] = 0;

            Choose(0x0B, bolt);
            var secondActed = Act();
            Commit(2);
            Check($"a second spell that finishes first (Bolt, CT 2): acted {secondActed}; the record holds 0x{Main():X2} at CT {unit[0x18D]}, the slot 0x{Slotted():X2} at CT {TwoSpells.SlotCtOf(slot)}",
                  secondActed == 1 && Main() == bolt && unit[0x18D] == 2 && Slotted() == fire && TwoSpells.SlotCtOf(slot) == 4 && TwoSpells.PairOf(slot) == 0);

            Fresh(true, true);
            Choose(0x0B, fire);
            Act();
            Commit(4);
            Choose(0x0B, ice);
            Act();
            Commit(6);
            Check($"a second spell that finishes later (Ice, CT 6): the record keeps 0x{Main():X2} at CT {unit[0x18D]}, the slot holds 0x{Slotted():X2} at CT {TwoSpells.SlotCtOf(slot)}",
                  Main() == fire && unit[0x18D] == 4 && Slotted() == ice && TwoSpells.SlotCtOf(slot) == 6);

            // The countdown's stub, run through a harness: its final jump goes to a ret for the run.
            var ret = GameImage.AllocateCodeNear((nint)tickStub, 16);
            ret[0] = 0xC3;
            var backRel = *(int*)(tickStub + tickBack + 1);
            var stateAt = (int*)((nint)tickStub + tickBack + *(int*)(tickStub + tickBack - 8));
            var stateBefore = *stateAt;
            *stateAt = 2;
            var runRel = checked((int)((nint)ret - ((nint)tickStub + tickBack + 5)));
            GameImage.WriteCode((nint)(tickStub + tickBack + 1), BitConverter.GetBytes(runRel));
            void Countdown() => tickRun((nint)tickStub);
            _clockStopped = 0;
            Countdown();
            var ticked = TwoSpells.SlotCtOf(slot);
            var stateSet = *stateAt;
            _clockStopped = 1;
            Countdown();
            var stopped = TwoSpells.SlotCtOf(slot);
            _clockStopped = 0;
            Check($"the countdown's stub ticks the slot ({ticked}, CT 6 before) and sets state {stateSet}; a stopped clock keeps it ({stopped})",
                  ticked == 5 && stateSet == 3 && stopped == 5);
            unit[0x18D] = 0xFF; // the first has fired
            Countdown();
            Check($"once the first has fired the slot takes the record: 0x{Main():X2} at CT {unit[0x18D]}, slot {(TwoSpells.SlotCtOf(slot) == 0xFF ? "empty" : "full")}",
                  Main() == ice && unit[0x18D] == 4 && TwoSpells.SlotCtOf(slot) == 0xFF);

            Fresh(true, true);
            Choose(0x0B, fire);
            Act();
            Commit(4);
            Choose(0x0B, ice);
            Act();
            Commit(6);
            unit[0x18D] = 0xFF;
            unit[Battle.UnitStatuses] &= 0xF7; // Charging cancelled by a status
            Countdown();
            Check($"a cancelled charge (no Charging) empties the slot: record CT {unit[0x18D]:X2}, slot {(TwoSpells.SlotCtOf(slot) == 0xFF ? "empty" : "full")}",
                  unit[0x18D] == 0xFF && TwoSpells.SlotCtOf(slot) == 0xFF);
            GameImage.WriteCode((nint)(tickStub + tickBack + 1), BitConverter.GetBytes(backRel));
            *stateAt = stateBefore;

            // The action's start through its stub: with a full slot the reset is left out.
            Fresh(true, true);
            Choose(0x0B, fire);
            Act();
            Commit(4);
            Choose(0x0B, ice);
            Act();
            Commit(6);
            _stateCalls.Clear();
            call((nint)startStub, (nint)unit, 0xFF);
            var skipped = _stateCalls.Count == 0;
            call((nint)startStub, (nint)unit, 8);
            unit[Battle.UnitStatuses] |= 0x01;
            call((nint)startStub, (nint)unit, 0xFF);
            unit[Battle.UnitStatuses] &= 0xFE;
            TwoSpells.ResetAll();
            call((nint)startStub, (nint)unit, 0xFF);
            Check($"the action's start keeps Charging with a full slot ({(skipped ? "reset left out" : "reset")}); other calls go through: {string.Join(" ", _stateCalls.Select(s => $"{s:X2}"))}",
                  skipped && string.Join(" ", _stateCalls.Select(s => $"{s:X2}")) == "08 FF FF");

            // Not a spell, no 0x1D0, the end of turn, a new action replacing a pair's charges.
            Fresh(true, true);
            Choose(0x01, attack);
            var attackActed = Act();
            Fresh(false, true);
            Choose(0x0B, fire);
            var plainActed = Act();
            Fresh(true, true);
            Choose(0x0B, fire);
            Act();
            Commit(4);
            TwoSpells.TurnEnded(slot);
            var afterTurn = TwoSpells.PairOf(slot);
            Choose(0x0B, ice);
            var nextTurnActed = Act();
            Check($"Attack marks the action ({attackActed}), so does a spell without 0x1D0 ({plainActed}); the end of turn closes the pair, so the next turn's first spell opens a new one ({afterTurn}, acted {nextTurnActed})",
                  attackActed == 1 && plainActed == 1 && afterTurn == 0 && nextTurnActed == 0);
            Fresh(true, true);
            Choose(0x0B, fire);
            Act();
            Commit(4);
            Choose(0x0B, ice);
            Act();
            Commit(6);
            TwoSpells.TurnEnded(slot);
            Choose(0x01, attack);
            Act();
            Commit(0);
            Check($"a later action replaces both charges: slot {(TwoSpells.SlotCtOf(slot) == 0xFF ? "empty" : "full")}",
                  TwoSpells.SlotCtOf(slot) == 0xFF);

            // The AI casts a qualifying spell twice (no pair: it marks its own action).
            Fresh(true, false);
            Choose(0x0B, fire);
            Commit(4);
            Check($"an AI unit's spell goes into the slot too: 0x{Slotted():X2} at CT {TwoSpells.SlotCtOf(slot)}",
                  Slotted() == fire && TwoSpells.SlotCtOf(slot) == 4);

            // An AI unit with Non-charge: its spell gets two strikes in the action's setup (here the rule; the hook's
            // run needs a whole action).
            Fresh(true, false);
            Choose(0x0B, fire);
            new Span<byte>(ui + 0x178, 20).CopyTo(new Span<byte>(queued, 20));
            unit[Battle.UnitSupports + 3] = 0x04;
            var aiDouble = TwoSpells.DoubleCast(unit);
            unit[5] = 0x08;
            var playerDouble = TwoSpells.DoubleCast(unit);
            unit[5] = 0;
            unit[Battle.UnitSupports + 3] = 0;
            var chargedDouble = TwoSpells.DoubleCast(unit);
            Check($"with Non-charge an AI unit's spell gets two strikes ({aiDouble}); not a player's ({playerDouble}) or without Non-charge ({chargedDouble}); the action's setup (0x{TwoSpells.SetupAt - image:X}) is hooked",
                  aiDouble && !playerDouble && !chargedDouble && TwoSpells.SetupAt != 0 && TwoSpells.Strikes == Knockback.Strikes);

            // Two Shots without 0x1D0: a pair of its own, but not with a spell. The first's commit uses up the move, so
            // marking the second ends the turn before its queue (0x310548), as in the game.
            Fresh(false, true);
            Choose(0x08, 0x195);
            ui[0x178 + 0x10] = 1;
            var shotActed = Act();
            Commit(4);
            Choose(0x08, 0x195);
            ui[0x178 + 0x10] = 2;
            var endedBefore = _turnsEnded;
            var secondShot = Act();
            var shotTurnEnded = _turnsEnded - endedBefore;
            Commit(4);
            var shotSlot = Slotted();
            Check($"Two Shots: the second's marking ends the turn ({shotTurnEnded}) before its queue, and still the record keeps the first (0x{Main():X3}, shot {queued[0x10]}, CT {unit[0x18D]}) and the slot holds the second (0x{shotSlot:X3}, shot {TwoSpells.SlotRecordOf(slot)[0x10]}, CT {TwoSpells.SlotCtOf(slot)})",
                  shotTurnEnded == 1 && Main() == 0x195 && queued[0x10] == 1 && unit[0x18D] == 4
                  && shotSlot == 0x195 && TwoSpells.SlotRecordOf(slot)[0x10] == 2 && TwoSpells.SlotCtOf(slot) == 4);
            ui[0x178 + 0x10] = 0;

            // Two Spells after moving: the second spell's marking ends the turn too.
            Fresh(true, true);
            unit[0x1B9] = 1;
            Choose(0x0B, fire);
            Act();
            Commit(4);
            Choose(0x0B, ice);
            endedBefore = _turnsEnded;
            Act();
            var spellTurnEnded = _turnsEnded - endedBefore;
            Commit(6);
            Check($"two spells after moving: the second's marking ends the turn ({spellTurnEnded}); the record keeps 0x{Main():X2} at CT {unit[0x18D]}, the slot holds 0x{Slotted():X2} at CT {TwoSpells.SlotCtOf(slot)}",
                  spellTurnEnded == 1 && Main() == fire && unit[0x18D] == 4 && Slotted() == ice && TwoSpells.SlotCtOf(slot) == 6);

            Fresh(false, true);
            Choose(0x08, 0x195);
            Act();
            Commit(4);
            Choose(0x0B, fire);
            var spellAfter = Act();
            Check($"Two Shots without 0x1D0: the first keeps the action ({shotActed}), the second ends it ({secondShot}) and goes to the slot (0x{shotSlot:X3}); a spell after it isn't the pair's ({spellAfter})",
                  shotActed == 0 && secondShot == 1 && shotSlot == 0x195 && spellAfter == 1);

            // Nothing while the game simulates.
            Fresh(true, true);
            var simulating = game.Routines.Simulating;
            var simBefore = *simulating;
            *simulating = 1;
            Choose(0x0B, fire);
            var simActed = Act();
            *simulating = simBefore;
            Check($"while the game simulates the action is marked as usual ({simActed})", simActed == 1);
        }
        finally
        {
            TwoSpells.GameSetActed = realActed;
            TwoSpells.GameActionState = realState;
            TwoSpells.GameStopped = realStopped;
            _actedUnits = null;
            TwoSpells.ResetAll();
            savedUnit.CopyTo(new Span<byte>(unit, Battle.UnitSize));
            foreach (var (id, bytes) in saved)
                bytes.CopyTo(new Span<byte>(game.RuleCode.GetAbilityAction(id), 20));
            System.Runtime.InteropServices.NativeMemory.Free(ui);
        }
        return failures;
    }

    private static readonly List<(int Skillset, int Count)> _candidateCalls = [];
    private static int _candidatesAdded;

    /// <summary>Stands in for the AI's candidates per skillset: writes so many made-up entries and returns the count.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeCandidates(int index, int skillset, int count)
    {
        _candidateCalls.Add((skillset, count));
        var list = TwoSpells.AiLists + TwoSpells.AiSlots[index] * 0x22 * 4;
        for (var k = 0; k < _candidatesAdded; k++)
            *(uint*)(list + 4 * (count + k)) = 0xC0DE0000u | (uint)skillset;
        return count + _candidatesAdded;
    }

    /// <summary>
    /// Two Spells (held back in the build, so installed here on this copy): the extra commands put into a command
    /// list as the game's builder leaves it; the menu's list builder for a made-up Calculator, as an extra list and
    /// as its own; Math Skill's spells through its hook; the AI's extra lists, with a stand-in for the game's part.
    /// </summary>
    public static int TwoSpellsCommands(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Two Spells, {name}{(ok ? "" : ": WRONG")}");
        }
        if (TwoSpells.Builder == 0 || !TwoSpells.Limits)
        {
            log.Info("self-test: Two Spells, held back in this build (0x1D0 isn't in the supports list): installed on this copy");
            try
            {
                TwoSpells.Install(game);
            }
            catch (Exception e)
            {
                Check($"can't be installed: {e.Message}", false);
                return failures;
            }
        }
        var entry = (byte*)TwoSpells.Builder;
        Check($"the command list (0x{TwoSpells.Builder - game.Image.ImageStart:X}) is hooked", entry[0] == 0xE9 || entry[0] == 0xFF);
        var realUsable = TwoSpells.Usable;
        TwoSpells.Usable = &FakeUsable;
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var list = stackalloc byte[8];
        var table = TwoSpells.TypeTable;
        string Run(bool innate, int primary, int secondary, params byte[] before)
        {
            unit[Battle.UnitSupports + 1] = (byte)(innate ? 0x20 : 0);
            unit[0x12] = (byte)primary;
            unit[Battle.UnitSecondary] = (byte)secondary;
            for (var k = 0; k < 8; k++)
                list[k] = 0xFF;
            for (var k = 0; k < before.Length; k++)
                list[k] = before[k];
            TwoSpells.Insert(3, list, unit);
            var entries = new List<string>();
            for (var k = 0; k < 7 && list[k] != 0xFF; k++)
            {
                var typeOk = TwoSpells.Types[k] == (table[list[k]] & 0x0F);
                entries.Add($"{list[k]:X2}{(TwoSpells.Unusable[k] != 0 ? "x" : "")}{(typeOk ? "" : "?")}");
            }
            return string.Join(" ", entries);
        }
        Check("a Calculator (Math Skill, no secondary): White and Black Magic after it, Black unusable here",
              Run(true, 0x15, 0, 1, 0x15) == "01 15 0A 0Bx");
        Check("with White Magic as its secondary: only Black Magic added", Run(true, 0x15, 0x0A, 1, 0x15, 0x0A) == "01 15 0A 0Bx");
        Check("with Defend and Equip Change: the extras go before them", Run(true, 0x15, 0x0A, 1, 0x15, 0x0A, 2, 3) == "01 15 0A 0Bx 02 03");
        Check("a full list keeps six (Equip Change drops)", Run(true, 0x15, 0x10, 1, 0x15, 0x10, 2, 3) == "01 15 10 0A 0Bx 02");
        Check("without 0x1D0: the game's list", Run(false, 0x15, 0, 1, 0x15) == "01 15");
        TwoSpells.Usable = realUsable;
        System.Runtime.InteropServices.NativeMemory.Free(unit);

        // The spell limit through the menu's weapon stub (as in the list builder 0x30E368: the ability in edi, the
        // skillset in r15d, the battle index at the function's [rsp+0x28]), for a made-up Calculator in battle slot 4.
        // The builder itself can't run here: it works out each spell's turn by running the turn order.
        var image = game.Image.ImageStart;
        var site = Weapons.Sites.First(s => s.Kind == Weapons.Kind.MenuList);
        // The builder's call that reads a skillset's ability ids (0x275860), 0x33 before the weapon check's code.
        var spellCall = site.At - 13 - 0x33;
        var spellAt = (delegate* unmanaged<int, int, int>)GameImage.Rip(spellCall, 1, 5);
        var harness = GameImage.AllocateCode(128);
        byte[] harnessCode =
        [
            0x57, 0x41, 0x57,                   // push rdi, r15
            0x48, 0x83, 0xEC, 0x30,             // sub rsp, 0x30: the stub then starts 16-aligned
            0x8B, 0xF9,                         // mov edi, ecx (the ability)
            0x45, 0x8B, 0xF8,                   // mov r15d, r8d (the skillset)
            0x89, 0x54, 0x24, 0x20,             // mov [rsp+0x20], edx: the stub's [rsp+0x28] (the battle index)
            0x41, 0xFF, 0xD1,                   // call r9 (the stub)
            0x48, 0x83, 0xC4, 0x30,             // add rsp, 0x30
            0x41, 0x5F, 0x5F, 0xC3,             // pop r15, rdi; ret
        ];
        harnessCode.CopyTo(new Span<byte>(harness, harnessCode.Length));
        byte[] passCode = [0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3];
        byte[] failCode = [0x31, 0xC0, 0xC3];
        passCode.CopyTo(new Span<byte>(harness + 64, passCode.Length));
        failCode.CopyTo(new Span<byte>(harness + 80, failCode.Length));
        var stub = GameImage.AllocateCode(256);
        Weapons.WriteStub(stub, Weapons.Kind.MenuList, (nint)(harness + 64), (nint)(harness + 80));
        var listed = (delegate* unmanaged<int, int, int, byte*, int>)harness;
        const int slot = 4;
        var calc = game.TurnCode.Units + slot * Battle.UnitSize;
        var savedUnit = new Span<byte>(calc, Battle.UnitSize).ToArray();
        new Span<byte>(calc, Battle.UnitSize).Clear();
        calc[1] = slot;
        calc[0x12] = TwoSpells.MathSkill;
        calc[Battle.UnitSupports + 1] = 0x20;
        *(ushort*)(calc + 0x20) = 0xFF;
        *(ushort*)(calc + 0x24) = 0xFF;
        bool Fits(int id) => TwoSpells.ExtraSpell(game.RuleCode.GetAbilityAction(id));
        string Names(IEnumerable<int> list) => string.Join(" ", list.Select(id => $"{id:X2}"));
        List<int> Spells(int skillset) => Enumerable.Range(0, 16).Select(k => spellAt(skillset, k) & 0xFFFF)
            .Where(id => id is > 0 and < 0x170).ToList();
        List<int> Listed(int skillset) => Spells(skillset).Where(id => listed(id, slot, skillset, stub) != 0).ToList();
        // The live ability table is built from the game's data on first use, which this copy doesn't have: marked
        // built, with vanilla's CT and MP written into the two skillsets' spells for the test, and put back after.
        *game.RuleCode.AbilitiesReady = 1;
        (int Ct, int Mp)[] whiteData = [(4, 6), (5, 10), (7, 16), (10, 20), (4, 10), (10, 20), (7, 16), (4, 8), (4, 6), (7, 24),
                                        (4, 6), (7, 20), (4, 24), (3, 18), (6, 56)];
        (int Ct, int Mp)[] blackData = [(4, 6), (5, 12), (7, 24), (9, 48), (4, 6), (5, 12), (7, 24), (9, 48), (4, 6), (5, 12),
                                        (7, 24), (9, 48), (3, 6), (5, 12), (6, 24), (7, 60)];
        var savedRecords = new Dictionary<int, byte[]>();
        void WriteData(List<int> spells, (int Ct, int Mp)[] data)
        {
            for (var k = 0; k < spells.Count && k < data.Length; k++)
            {
                var record = game.RuleCode.GetAbilityAction(spells[k]);
                savedRecords.TryAdd(spells[k], new Span<byte>(record, 20).ToArray());
                record[0x0C] = (byte)data[k].Ct;
                record[0x0D] = (byte)data[k].Mp;
            }
        }
        var whiteAll = Spells(TwoSpells.WhiteMagic);
        WriteData(whiteAll, whiteData);
        WriteData(Spells(TwoSpells.BlackMagic), blackData);
        var white = Listed(TwoSpells.WhiteMagic);
        var black = Listed(TwoSpells.BlackMagic);
        calc[Battle.UnitSecondary] = TwoSpells.WhiteMagic;
        var whiteOwn = Listed(TwoSpells.WhiteMagic);
        calc[Battle.UnitSecondary] = 0;
        calc[Battle.UnitSupports + 1] = 0;
        var blackPlain = Listed(TwoSpells.BlackMagic);
        calc[Battle.UnitSupports + 1] = 0x20;
        Check($"the menu's stub (0x{site.At - image:X}), an extra White Magic list: {Names(white)} (Cure, Cure 2, Raise, Regen, Protect, Shell); all {whiteOwn.Count} as the secondary",
              *(byte*)spellCall == 0xE8 && Names(white) == "01 02 05 08 09 0B" && whiteOwn.SequenceEqual(whiteAll) && whiteAll.Count == 15);
        Check($"an extra Black Magic list: {Names(black)} (Fire, Fire 2, Bolt, Bolt 2, Ice, Ice 2, Poison, Frog); without 0x1D0 not limited ({blackPlain.Count})",
              Names(black) == "10 11 14 15 18 19 1C 1D" && blackPlain.Count == 16);
        // Math Skill's spells (0x30EA38, through its hook, with a stand-in for the game's list that asks the limit
        // about a White Magic spell past it): not limited while Math Skill lists them.
        var past = whiteAll.FirstOrDefault(id => !Fits(id));
        var realMath = TwoSpells.GameMathSpells;
        _limitAsked = ((nint)calc, past, -1);
        TwoSpells.GameMathSpells = &FakeMathSpells;
        ((delegate* unmanaged<nint, nint, nint, nint, nint>)TwoSpells.MathSpellsAt)(slot, TwoSpells.MathSkill, 0, 0);
        TwoSpells.GameMathSpells = realMath;
        var outside = TwoSpells.Shows(calc, TwoSpells.WhiteMagic, past) ? 1 : 0;
        Check($"Math Skill's spells (0x{TwoSpells.MathSpellsAt - image:X}) keep White Magic's 0x{past:X2} (shown {_limitAsked.Shown}), which an extra list leaves out (shown {outside})",
              past != 0 && _limitAsked.Shown == 1 && outside == 0);

        // The AI's extra lists after the secondary's, with a stand-in for the game's candidates that adds so many.
        var realCandidates = TwoSpells.GameCandidates;
        TwoSpells.GameCandidates = &FakeCandidates;
        var aiList = TwoSpells.AiLists + TwoSpells.AiSlots[slot] * 0x22 * 4;
        var savedLists = new Span<byte>(aiList, 2 * 0x88).ToArray();
        string Ask(int secondary, bool innate, int from, int adds, out bool nextKept)
        {
            calc[Battle.UnitSecondary] = (byte)secondary;
            calc[Battle.UnitSupports + 1] = (byte)(innate ? 0x20 : 0);
            _candidateCalls.Clear();
            _candidatesAdded = adds;
            var next = new Span<byte>(aiList + 0x88, 0x40).ToArray();
            var n = TwoSpells.AddCandidates(slot, secondary, from);
            nextKept = new Span<byte>(aiList + 0x88, 0x40).SequenceEqual(next);
            return $"{string.Join(" ", _candidateCalls.Select(c => $"{c.Skillset:X2}@{c.Count}"))} -> {n}";
        }
        var timeSecondary = Ask(0x0C, true, 1, 2, out _);
        var whiteSecondary = Ask(TwoSpells.WhiteMagic, true, 1, 2, out _);
        var noInnate = Ask(0x0C, false, 1, 2, out _);
        var full = Ask(0x0C, true, 10, 16, out var kept);
        TwoSpells.GameCandidates = realCandidates;
        savedLists.CopyTo(new Span<byte>(aiList, 2 * 0x88));
        savedUnit.CopyTo(new Span<byte>(calc, Battle.UnitSize));
        foreach (var (id, bytes) in savedRecords)
            bytes.CopyTo(new Span<byte>(game.RuleCode.GetAbilityAction(id), 20));
        Check($"the AI's lists after a Time Magic secondary: {timeSecondary} (White and Black after it)", timeSecondary == "0C@1 0A@3 0B@5 -> 7");
        Check($"with White Magic as the secondary: {whiteSecondary}", whiteSecondary == "0A@1 0B@3 -> 5");
        Check($"without 0x1D0: {noInnate}", noInnate == "0C@1 -> 3");
        Check($"a full list stops at 33 for the wait entry, and the next unit's list is put back: {full}, {(kept ? "kept" : "overwritten")}",
              full == "0C@10 0A@26 -> 33" && kept);
        return failures;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FacingFormula() => _facingSeen = *Banditry.Facing;

    /// <summary>A formula call through its table entry clears the attack's angle first, as ReMixed's action routine does.</summary>
    public static int AngleClear(Game game, Log log)
    {
        if (Banditry.Facing == null)
        {
            log.Info("self-test: the angle before a formula, Banditry isn't installed: WRONG");
            return 1;
        }
        const int formula = 0x30;
        var b = game.Battle;
        var units = game.TurnCode.Units;
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        *game.RuleCode.AbilitiesReady = 1;
        *b.Attacker = units;
        *game.Routines.Target = units + Battle.UnitSize;
        *b.TargetResult = result;
        *b.AbilityId = 0x03C;
        var had = Rules.SwapFormula(formula, (nint)(delegate* unmanaged<void>)&FacingFormula);
        *Banditry.Facing = 2;
        _facingSeen = -1;
        ((delegate* unmanaged<void>)b.FormulaTable[formula - 1])();
        Rules.SwapFormula(formula, had);
        *b.Attacker = null;
        *game.Routines.Target = null;
        *b.TargetResult = null;
        System.Runtime.InteropServices.NativeMemory.Free(result);
        var ok = _facingSeen == 0;
        log.Info($"self-test: the angle before a formula, a stale 'from behind' (2) is {_facingSeen} when the formula runs{(ok ? "" : ": WRONG")}");
        return ok ? 0 : 1;
    }
    private static int _steals;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeSteal() => _steals++;

    /// <summary>
    /// The third magic step through the game's routine (now ReMixed's stub), copies of the Phoenix Down, Composure and
    /// steal stubs in harnesses, and formulas 0x55 and 0x27 with stand-ins for the steps they call.
    /// </summary>
    public static int FormulaStepRules(Game game, Log log)
    {
        _game = game;
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: formula steps, {name}{(ok ? "" : ": WRONG")}");
        }
        if (FormulaSteps.MagicModStub == 0 || FormulaSteps.PhoenixStub == 0 || FormulaSteps.ComposureStub == 0 || FormulaSteps.StealStub == 0
            || FormulaSteps.StealExpStub == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var image = game.Image.ImageStart;
        bool JumpsTo(nint at, nint to) => ((byte*)at)[0] == 0xE9 && GameImage.Rip(at, 1, 5) == to;
        Check($"the third magic step (0x{FormulaSteps.MagicModAt - image:X}), Phoenix Down (0x{FormulaSteps.PhoenixAt - image:X}), "
              + $"the reactions' default (0x{FormulaSteps.ComposureAt - image:X}), the item steal's YA (0x{FormulaSteps.StealAt - image:X}) "
              + $"and Steal Exp's (0x{FormulaSteps.StealExpAt - image:X}) jump to stubs",
              JumpsTo(FormulaSteps.MagicModAt, FormulaSteps.MagicModStub) && JumpsTo(FormulaSteps.PhoenixAt, FormulaSteps.PhoenixStub)
              && JumpsTo(FormulaSteps.ComposureAt, FormulaSteps.ComposureStub) && JumpsTo(FormulaSteps.StealAt, FormulaSteps.StealStub)
              && JumpsTo(FormulaSteps.StealExpAt, FormulaSteps.StealExpStub));
        const int st = Battle.UnitStatuses;
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var attacker = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var targetSlot = game.Routines.Target;
        var attackerSlot = game.Battle.Attacker;
        var savedTarget = *targetSlot;
        var savedAttacker = *attackerSlot;
        *targetSlot = target;
        *attackerSlot = attacker;

        // The third magic step, through the game's own entry.
        var xa = game.Battle.XA;
        var savedXa = *xa;
        short Magic(short value, int defending, int transparent, int shell)
        {
            new Span<byte>(target + st, 5).Clear();
            target[st] = (byte)defending;
            target[st + 2] = (byte)transparent;
            target[st + 3] = (byte)shell;
            *xa = value;
            game.Routines.MagicMod3();
            return *xa;
        }
        Check("magic XA 30: Shell 20, Transparent 20 (ReMixed), both 20, Defending 20 (ReMixed), Shell and Defending 13",
              Magic(30, 0, 0, 0) == 30 && Magic(30, 0, 0, 0x10) == 20 && Magic(30, 0, 0x10, 0) == 20
              && Magic(30, 0, 0x10, 0x10) == 20 && Magic(30, 0x02, 0, 0) == 20 && Magic(30, 0x02, 0, 0x10) == 13);
        Check("magic XA 30: Frog or Chicken 45, Shell and Frog 30; XA -30 with Shell -20",
              Magic(30, 0, 0x02, 0) == 45 && Magic(30, 0, 0x04, 0) == 45 && Magic(30, 0, 0x02, 0x10) == 30 && Magic(-30, 0, 0, 0x10) == -20);
        *xa = savedXa;

        // Stubs copied with gadgets in place of the game's code they go back to.
        var one = GameImage.AllocateCode(16);
        byte[] retOne = [0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3];
        retOne.CopyTo(new Span<byte>(one, retOne.Length));
        var zero = GameImage.AllocateCode(16);
        byte[] retZero = [0x31, 0xC0, 0xC3];
        retZero.CopyTo(new Span<byte>(zero, retZero.Length));
        var ret = GameImage.AllocateCode(16);
        ret[0] = 0xC3;
        // mov rax, rcx; sub rsp, 0x28; call rdx; add rsp, 0x28; ret
        var withRax = GameImage.AllocateCode(32);
        byte[] h = [0x48, 0x89, 0xC8, 0x48, 0x83, 0xEC, 0x28, 0xFF, 0xD2, 0x48, 0x83, 0xC4, 0x28, 0xC3];
        h.CopyTo(new Span<byte>(withRax, h.Length));
        var run = (delegate* unmanaged<byte*, byte*, int>)withRax;

        var phoenix = GameImage.AllocateCode(64);
        FormulaSteps.WritePhoenixStub(phoenix, (nint)one, (nint)zero);
        int Phoenix(int undead, int vampire, int flags)
        {
            new Span<byte>(target + st, 5).Clear();
            target[st] = (byte)undead;
            target[st + 1] = (byte)vampire;
            target[5] = (byte)flags;
            return run(target, phoenix);
        }
        Check("Phoenix Down: takes the HP of the Undead and (ReMixed) of a Blood-Sucked unit, revives anyone else",
              Phoenix(0x10, 0, 0) == 1 && Phoenix(0, 0x04, 0x04) == 1 && Phoenix(0, 0, 0) == 0);
        Check("Phoenix Down: an Immortal story unit (+5 & 5 is 5) is revived even when Undead", Phoenix(0x10, 0, 0x05) == 0 && Phoenix(0, 0x04, 0x15) == 0);

        // Composure: the dispatcher keeps esi 0 and rbx, rsi callee-saved; the harness saves them.
        var composure = GameImage.AllocateCode(96);
        FormulaSteps.WriteComposureStub(composure, FormulaSteps.ReactionId, game.Battle.TargetResult, (nint)one, (nint)ret);
        // push rbx; push rsi; xor esi, esi; sub rsp, 0x28; call rcx; add rsp, 0x28; pop rsi; pop rbx; ret
        var ch = GameImage.AllocateCode(32);
        byte[] c = [0x53, 0x56, 0x31, 0xF6, 0x48, 0x83, 0xEC, 0x28, 0xFF, 0xD1, 0x48, 0x83, 0xC4, 0x28, 0x5E, 0x5B, 0xC3];
        c.CopyTo(new Span<byte>(ch, c.Length));
        var dispatch = (delegate* unmanaged<byte*, int>)ch;
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var resultSlot = game.Battle.TargetResult;
        var savedResult = *resultSlot;
        var savedReaction = *FormulaSteps.ReactionId;
        *resultSlot = result;
        *FormulaSteps.ReactionId = FormulaSteps.Composure;
        result[Battle.ResultFlags] = 0x55;
        var composed = dispatch(composure);
        var ct = result[0x15];
        *FormulaSteps.ReactionId = 0x1BF;
        result[Battle.ResultFlags] = 0x55;
        var other = dispatch(composure);
        Check("Composure (0x1C0): CT up by 30, then the dispatcher's common tail; another reaction: the default (flags 0, -1)",
              composed == 1 && ct == 0x9E && other == -1 && result[Battle.ResultFlags] == 0);
        *FormulaSteps.ReactionId = savedReaction;

        // The steal's YA: X, plus 10 with Banditry.
        var ability = game.Battle.Ability;
        var savedAbility = new byte[0x14];
        new ReadOnlySpan<byte>(ability, 0x14).CopyTo(savedAbility);
        var steal = GameImage.AllocateCode(FormulaSteps.StealStubSize);
        FormulaSteps.WriteStealStub(steal, ability + 9, (nint)ret);
        ability[9] = 40;
        attacker[Battle.UnitTurn] = 1;
        attacker[Battle.UnitSupports + 1] = 0;
        var plain = run(attacker, steal);
        attacker[Battle.UnitSupports + 1] = 0x40;
        var bandit = run(attacker, steal);
        Check("an item or EXP steal's YA on the stealer's turn: X 40, or 50 with Banditry (0x1CF)", plain == 40 && bandit == 50);
        attacker[Battle.UnitTurn] = 0;
        attacker[Battle.UnitSupports + 1] = 0;
        var counter = run(attacker, steal);
        attacker[Battle.UnitSupports + 1] = 0x40;
        var counterBandit = run(attacker, steal);
        Check("as a reaction (Counter Steal): X doubled to 80, then 90 with Banditry", counter == 80 && counterBandit == 90);
        attacker[Battle.UnitTurn] = 1;

        // Formulas 0x55 and 0x27 with stand-ins.
        var routines = game.Routines;
        var realEvade = routines.Evade;
        var realStatHit = Formulas.StatHit;
        var realGilRoll = routines.GilRoll;
        var realStealGil = routines.StealGil;
        routines.Evade = &FakeHit;
        Formulas.StatHit = &FakeHit;
        new Span<byte>(result, 0x40).Clear();
        result[0] = 1;
        ability[10] = 0x81;
        Formulas.Run(0x55);
        Check("Beaking (Y 1): Speed, PA and MA each down by 1 (vanilla: PA only)",
              result[0x14] == 1 && result[0x16] == 1 && result[0x17] == 1 && result[Battle.ResultFlags] == 1);
        routines.Evade = &FakeMiss;
        new Span<byte>(result, 0x40).Clear();
        Formulas.Run(0x55);
        Check("Beaking evaded: nothing", result[0x16] == 0 && result[Battle.ResultFlags] == 0);
        routines.Evade = &FakeHit;
        routines.GilRoll = &FakeHit;
        routines.StealGil = &FakeSteal;
        ability[3] = 0;
        ability[9] = 60;
        attacker[Battle.UnitSpeed] = 9;
        attacker[Battle.UnitSupports + 1] = 0x40;
        _steals = 0;
        new Span<byte>(result, 0x40).Clear();
        result[0] = 1;
        Formulas.Run(0x27);
        Check("Gil Taking: one roll and one steal (the earlier port stole twice), XA Speed 9, YA X 60 + 10 for Banditry",
              _steals == 1 && *game.Battle.XA == 9 && *game.Battle.YA == 70);
        attacker[Battle.UnitTurn] = 0;
        Formulas.Run(0x27);
        Check("Gil Taking as a reaction (Counter Steal): YA X 60 doubled + 10 for Banditry", *game.Battle.YA == 130);
        routines.Evade = realEvade;
        Formulas.StatHit = realStatHit;
        routines.GilRoll = realGilRoll;
        routines.StealGil = realStealGil;
        savedAbility.CopyTo(new Span<byte>(ability, 0x14));
        *xa = savedXa;
        *resultSlot = savedResult;
        *targetSlot = savedTarget;
        *attackerSlot = savedAttacker;
        Check("formula 0x55 is ReMixed's", Formulas.Replaced(0x55));
        System.Runtime.InteropServices.NativeMemory.Free(result);
        System.Runtime.InteropServices.NativeMemory.Free(attacker);
        System.Runtime.InteropServices.NativeMemory.Free(target);
        return failures;
    }

    /// <summary>
    /// The formulas ReMixed rewrote in place (read 2026-11-12): 0x25's knockback after a Momentum break, 0x32 through
    /// the HP damage step, 0x3C's heal and cost by Y and its dead target, 0x40 by the status set Y names. With
    /// stand-ins for the game's steps.
    /// </summary>
    public static int RewrittenFormulas(Game game, Log log)
    {
        _game = game;
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: formulas rewritten in place, {name}{(ok ? "" : ": WRONG")}");
        }
        if (!Formulas.Replaced(0x25) || !Formulas.Replaced(0x32) || !Formulas.Replaced(0x3C) || !Formulas.Replaced(0x40)
            || Formulas.FistHits == null || Formulas.PhoenixDown == null)
        {
            Check("aren't installed", false);
            return failures;
        }
        var battle = game.Battle;
        var routines = game.Routines;
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var own = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var attacker = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var resultSlot = battle.TargetResult;
        var ownSlot = routines.AttackerResult;
        var attackerSlot = battle.Attacker;
        var targetSlot = routines.Target;
        nint[] savedSlots = [(nint)(*resultSlot), (nint)(*ownSlot), (nint)(*attackerSlot), (nint)(*targetSlot)];
        var (savedXa, savedYa, savedHits) = (*battle.XA, *battle.YA, *Formulas.FistHits);
        var ability = battle.Ability;
        var savedAbility = new byte[0x14];
        new ReadOnlySpan<byte>(ability, 0x14).CopyTo(savedAbility);
        nint[] real = [(nint)routines.Evade, (nint)routines.MagicMod4, (nint)routines.Critical, (nint)routines.StatusChance,
                       (nint)routines.Random, (nint)routines.HpDamage, (nint)routines.Statuses, (nint)routines.StatusSet,
                       (nint)routines.AttackMods, (nint)routines.StatusRoll, (nint)routines.Knockback, (nint)Formulas.PhoenixDown];
        var realSteps = new nint[4];
        for (var k = 0; k < 4; k++)
            realSteps[k] = (nint)Formulas.FistSteps[k];
        *resultSlot = result;
        *ownSlot = own;
        *attackerSlot = attacker;
        *targetSlot = target;
        routines.Evade = &FakeHit;
        routines.MagicMod4 = &FakeNothing;
        routines.Critical = &FakeNothing;
        routines.StatusChance = &FakeStrike;
        routines.Random = &FakeHalfRandom;
        routines.HpDamage = &FakeHpDamage;
        routines.Statuses = &FakeStatuses;
        routines.StatusSet = &FakeStatuses;
        routines.AttackMods = &FakeNothing;
        routines.StatusRoll = &FakeNothing;
        routines.Knockback = &CountKnockback;
        Formulas.PhoenixDown = &FakePhoenix;
        for (var k = 0; k < 4; k++)
            Formulas.FistSteps[k] = &FakeNothing;

        // 0x32, Repeating Fist: PA 10, Y 0, so a strike is 10 + 5; X 9 with a random 0x4000: 4 + 1 strikes.
        new Span<byte>(result, 0x40).Clear();
        result[0] = 1;
        attacker[Battle.UnitPa] = 10;
        ability[9] = 9;
        ability[10] = 0;
        Formulas.Run(0x32);
        Check($"Repeating Fist (PA 10, X 9): {*Formulas.FistHits} strikes of 15 through the HP damage step: XA {*battle.XA}, "
              + $"YA {*battle.YA}, damage {*(ushort*)(result + Battle.HpDamage)}",
              *Formulas.FistHits == 5 && *battle.XA == 5 && *battle.YA == 15 && *(ushort*)(result + Battle.HpDamage) == 75);
        routines.Evade = &FakeMiss;
        new Span<byte>(result, 0x40).Clear();
        Formulas.Run(0x32);
        Check("Repeating Fist evaded: no damage", *(ushort*)(result + Battle.HpDamage) == 0);
        routines.Evade = &FakeHit;

        // 0x3C: Max HP 100, so four fifths 80.
        *(ushort*)(attacker + Battle.UnitMaxHp) = 100;
        string Heal(int y, bool dead)
        {
            new Span<byte>(result, 0x40).Clear();
            new Span<byte>(own, 0x40).Clear();
            result[0] = 1;
            target[Battle.UnitStatuses] = (byte)(dead ? 0x20 : 0);
            ability[10] = (byte)y;
            _statusSteps = 0;
            _phoenix = 0;
            Formulas.Run(0x3C);
            return $"{*(ushort*)(result + Battle.HpHeal)} {result[Battle.ResultFlags]:X2} {own[0]} {*(ushort*)(own + Battle.HpDamage)} "
                   + $"{_statusSteps} {_phoenix}";
        }
        Check("Wish (Y 0, Max HP 100): heals 40, the caster loses 20, then the statuses", Heal(0, false) == "40 40 1 20 1 0");
        Check("Lava Ball (Y 1): heals 20 for nothing, then the statuses (its cures)", Heal(1, false) == "20 40 0 0 1 0");
        Check("on a dead target: Phoenix Down's formula instead, no cost", Heal(0, true) == "0 00 0 0 0 1");
        *(ushort*)(attacker + Battle.UnitMaxHp) = 999;
        Check("Max HP 999: heals 399 (ReMixed's byte store would wrap), costs 199", Heal(0, false) == "399 40 1 199 1 0");
        target[Battle.UnitStatuses] = 0;

        // 0x40, Seal Evil: Y 6 names the status set it needs on the target (Undead or Blood Suck in vanilla's and ReMixed's).
        var set = game.RuleCode.StatusSets + 6 * 6 + 1;
        Check($"status set 6 holds Undead and Blood Suck ({set[0]:X2} {set[1]:X2} {set[2]:X2} {set[3]:X2} {set[4]:X2})",
              set[0] == 0x10 && set[1] == 0x04 && set[2] == 0 && set[3] == 0 && set[4] == 0);
        string Seal(int byte0, int byte1, bool hit)
        {
            new Span<byte>(result, 0x40).Clear();
            result[0] = (byte)(hit ? 1 : 0);
            *(ushort*)(result + Battle.ResultStatusChance) = 50;
            new Span<byte>(target + Battle.UnitStatuses, 5).Clear();
            target[Battle.UnitStatuses] = (byte)byte0;
            target[Battle.UnitStatuses + 1] = (byte)byte1;
            attacker[Battle.UnitSpeed] = 8;
            ability[9] = 70;
            ability[10] = 6;
            _statusSteps = 0;
            Formulas.Run(0x40);
            return $"{result[0]} {result[2]} {*(ushort*)(result + Battle.ResultStatusChance)} {_statusSteps} {*battle.XA} {*battle.YA}";
        }
        Check("Seal Evil on the Undead: XA Speed 8, YA X 70, the statuses", Seal(0x10, 0, true) == "1 0 50 1 8 70");
        Check("Seal Evil on a Blood-Sucked unit (ReMixed: set 6), the statuses", Seal(0, 0x04, true) == "1 0 50 1 8 70");
        Check("Seal Evil on anyone else: a miss (kind 7, chance 0)", Seal(0, 0, true) == "0 7 0 0 8 70");
        Check("Seal Evil's roll missed: nothing more", Seal(0x10, 0, false) == "0 0 50 0 8 70");

        // 0x25: the game's own Breaks (a stand-in), then the knockback only after a break Momentum's bonus helped.
        var had = Formulas.SwapOriginal(0x25, (nint)(delegate* unmanaged<void>)&FakeBreak);
        string Break(bool momentum, int flags, int hit)
        {
            new Span<byte>(result, 0x40).Clear();
            _breakMomentum = momentum;
            _breakFlags = flags;
            _breakHit = hit;
            _knockbacks = 0;
            Formulas.Run(0x25);
            return $"{_knockbacks}";
        }
        Check("a Break with Momentum's bonus that breaks: knocked back", Break(true, 4, 1) == "1");
        Check("without Momentum's bonus, a miss or no break: no knockback",
              Break(false, 4, 1) == "0" && Break(true, 4, 0) == "0" && Break(true, 0x2000, 1) == "0");
        Formulas.SwapOriginal(0x25, had);

        routines.Evade = (delegate* unmanaged<int>)real[0];
        routines.MagicMod4 = (delegate* unmanaged<void>)real[1];
        routines.Critical = (delegate* unmanaged<void>)real[2];
        routines.StatusChance = (delegate* unmanaged<void>)real[3];
        routines.Random = (delegate* unmanaged<int>)real[4];
        routines.HpDamage = (delegate* unmanaged<void>)real[5];
        routines.Statuses = (delegate* unmanaged<void>)real[6];
        routines.StatusSet = (delegate* unmanaged<void>)real[7];
        routines.AttackMods = (delegate* unmanaged<void>)real[8];
        routines.StatusRoll = (delegate* unmanaged<void>)real[9];
        routines.Knockback = (delegate* unmanaged<void>)real[10];
        Formulas.PhoenixDown = (delegate* unmanaged<void>)real[11];
        for (var k = 0; k < 4; k++)
            Formulas.FistSteps[k] = (delegate* unmanaged<void>)realSteps[k];
        savedAbility.CopyTo(new Span<byte>(ability, 0x14));
        *resultSlot = (byte*)savedSlots[0];
        *ownSlot = (byte*)savedSlots[1];
        *attackerSlot = (byte*)savedSlots[2];
        *targetSlot = (byte*)savedSlots[3];
        *battle.XA = savedXa;
        *battle.YA = savedYa;
        *Formulas.FistHits = savedHits;
        System.Runtime.InteropServices.NativeMemory.Free(result);
        System.Runtime.InteropServices.NativeMemory.Free(own);
        System.Runtime.InteropServices.NativeMemory.Free(attacker);
        System.Runtime.InteropServices.NativeMemory.Free(target);
        return failures;
    }

    private static int _phoenix, _breakFlags, _breakHit;
    private static bool _breakMomentum;

    /// <summary>Stands in for the game's XA + YA step (0x305F4C) without the zodiac.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeStrike()
    {
        var battle = _game!.Battle;
        *(ushort*)(*battle.TargetResult + Battle.HpDamage) = (ushort)(*battle.XA + *battle.YA);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeHalfRandom() => 0x4000;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakePhoenix() => _phoenix++;

    /// <summary>Stands in for the game's formula 0x25: Momentum's bonus (or not), then the result's hit and flags.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeBreak()
    {
        Supports.MomentumBreak = _breakMomentum;
        var result = *_game!.Battle.TargetResult;
        result[0] = (byte)_breakHit;
        *(ushort*)(result + Battle.ResultFlags2) = (ushort)_breakFlags;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeNothing() { }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeHit() => 0;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int FakeMiss() => 1;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeStatuses() => _statusSteps++;

    /// <summary>Stands in for the game's HP damage step: XA × YA into the result.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void FakeHpDamage()
    {
        var battle = _game!.Battle;
        *(ushort*)(*battle.TargetResult + Battle.HpDamage) = (ushort)(*battle.XA * *battle.YA);
    }

    private static Game? _game;

    /// <summary>The skillset type table after ReMixed's changes.</summary>
    public static int ElementalType(Game game, Log log)
    {
        var table = SkillsetTypes.Table;
        var ok = table != null && table[SkillsetTypes.Elemental] == SkillsetTypes.Ordinary
                 && SkillsetTypes.WeaponSkillsets.ToArray().All(s => table[s] == SkillsetTypes.Weapon)
                 && SkillsetTypes.MonsterSkillsets.ToArray().All(s => table[s] == SkillsetTypes.Monster)
                 && table[1] == 8 && table[0x12] == 9 && table[0x13] == 7 && table[0x14] == 2 && table[0x15] == 3;
        log.Info($"self-test: skillset types, Elemental (0x11) is ordinary, Magic Blade/Spell Strike/Fellblade are " +
                 $"weapon type 5, and ReMixed's monster lists are type 6 in the table " +
                 $"(0x{(table == null ? 0 : (nint)table - game.Image.ImageStart):X}), Jump, Draw Out, Throw and Math Skill as " +
                 $"they were{(ok ? "" : ": WRONG")}");
        return ok ? 0 : 1;
    }

    private static byte* _fakeItems, _fakeWeapons;

    /// <summary>Stands in for the game's item lookup: 12 bytes an item from a made-up table.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static byte* FakeItem(int id) => _fakeItems + 12 * (id & 0xFF);

    /// <summary>Stands in for the game's weapon data lookup: 8 bytes an item.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static byte* FakeWeapon(int id) => _fakeWeapons + 8 * (id & 0xFF);

    /// <summary>
    /// ReMixed's random equipment: its generator against the game's roll, the candidates on a made-up item table,
    /// the draw, what can be taken, the Iga and Koga pair through its stub, and the join rule's stub going in.
    /// </summary>
    public static int RandomEquipmentRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: random equipment, {name}{(ok ? "" : ": WRONG")}");
        }
        var image = game.Image.ImageStart;
        if (RandomEquipment.PickAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        Check($"the item pick (0x{RandomEquipment.PickAt - image:X}) and the taking (0x{RandomEquipment.TakeSlotAt - image:X}) " +
              $"are hooked; the left hand's pick (0x{RandomEquipment.PairAt - image:X}) calls its stub",
              ((byte*)RandomEquipment.PairAt)[0] == 0xE8 && GameImage.Rip(RandomEquipment.PairAt, 1, 5) == RandomEquipment.PairStub);
        var realItem = RandomEquipment.ItemData;
        var realWeapon = RandomEquipment.WeaponData;
        Check("in the game's table, the Iga and Koga Knives are both ninja blades (type 2)",
              realItem(RandomEquipment.IgaKnife)[5] == 2 && realItem(RandomEquipment.KogaKnife)[5] == 2);
        // The generator: one step of the game's roll against one of ours, on a copy of its state.
        var state = RandomEquipment.RandomState;
        var copy = stackalloc uint[4];
        for (var k = 0; k < 4; k++) state[k] = copy[k] = 0x11111111u * (uint)(k + 1);
        game.TurnCode.Roll(100, 50);
        RandomEquipment.NextRandom(copy);
        Check("its generator steps the game's state as the game's roll does",
              state[0] == copy[0] && state[1] == copy[1] && state[2] == copy[2] && state[3] == copy[3]);

        _fakeItems = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(12 * 256);
        _fakeWeapons = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8 * 256);
        void Item(int id, int type, int level, int flags = 0, int kinds = 0)
        {
            _fakeItems[12 * id + 2] = (byte)level;
            _fakeItems[12 * id + 3] = (byte)flags;
            _fakeItems[12 * id + 5] = (byte)type;
            _fakeWeapons[8 * id + 1] = (byte)kinds;
        }
        const int Knife = 1, Sword = 3, Rod = 7, Shoes = 26, Ring = 28;
        Item(1, Knife, 1); Item(2, Knife, 5, kinds: 8); Item(3, Knife, 9);      // one group: 3, the last at least as high as 1
        Item(4, Sword, 3); Item(5, Sword, 30);                                  // 5 is above the unit: 4
        Item(6, Knife, 2);                                                      // knives again, after swords: a group of its own
        Item(7, Rod, 1);                                                        // the unit can't equip rods
        Item(8, Knife, 8, flags: 2);                                            // rare, in 6's run (7 doesn't count)
        Item(9, Sword, 12); Item(10, Sword, 4);                                 // 10 is lower than its group's first: 9
        Item(0xD0, Shoes, 1); Item(0xD1, Shoes, 3); Item(0xD2, Ring, 2);         // accessories don't group
        RandomEquipment.ItemData = &FakeItem;
        RandomEquipment.WeaponData = &FakeWeapon;
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        unit[Battle.UnitLv] = 20;
        foreach (var t in new[] { Knife, Sword, Shoes, Ring })
            unit[Battle.UnitEquipTypes + (t >> 3)] |= (byte)(0x80 >> (t & 7));
        string Listed(int slot, int kind, int type, bool rare)
        {
            Span<byte> list = stackalloc byte[256];
            var n = RandomEquipment.Candidates(unit, slot, kind, type, rare, list);
            return string.Join(" ", list[..n].ToArray().Select(b => b.ToString("X")));
        }
        Check("a weapon's candidates: one per run of a type, the best the unit's level allows; no rare one",
              Listed(RandomEquipment.Weapon, 0, 0xFF, false) == "3 4 6 9");
        Check("with rare items allowed, the rare knife takes its run's place",
              Listed(RandomEquipment.Weapon, 0, 0xFF, true) == "3 4 8 9");
        Check("a type and a kind narrow it: the swords make one run, whose last at least as high as its first is A; kind 8: 2",
              Listed(RandomEquipment.Weapon, 0, Sword, false) == "A" && Listed(RandomEquipment.Weapon, 8, 0xFF, false) == "2");
        Check("accessories are each a candidate; an unknown category has none",
              Listed(RandomEquipment.Accessory, 0, 0xFF, false) == "D0 D1 D2" && Listed(0, 0, 0xFF, false) == "");
        for (var k = 0; k < 4; k++) copy[k] = state[k];
        var want = new byte[] { 3, 4, 6, 9 }[(int)((4u * RandomEquipment.NextRandom(copy)) >> 15)];
        Check($"the pick draws one number for its index (item {want:X})",
              RandomEquipment.Choose(unit, RandomEquipment.Weapon, 0, 0xFF, false) == want && state[3] == copy[3]
              && RandomEquipment.Choose(unit, RandomEquipment.Head, 0, 0xFF, false) == 0xFE && state[3] == copy[3]);
        Item(0x20, Sword, 10, flags: 1); Item(0x21, Sword, 10, flags: 2); Item(0x22, Sword, 0, flags: 2);
        Check("taking: never an item with +3 & 1; with rare items allowed, a steal (0x26) never a rare one of level 1–99",
              !RandomEquipment.CanTake(0x20, 0x25, false) && RandomEquipment.CanTake(0x21, 0x26, false)
              && !RandomEquipment.CanTake(0x21, 0x26, true) && RandomEquipment.CanTake(0x21, 0x25, true)
              && RandomEquipment.CanTake(0x22, 0x26, true) && RandomEquipment.CanTake(4, 0x26, true));
        // The pair, through its stub: a harness calls it with the unit in rcx.
        var harness = GameImage.AllocateCode(64);
        // mov rax, rdx; mov edx, 0x80; mov r8d, 8; mov r9d, 0xFF (the game's arguments); sub rsp, 0x28; call rax; add rsp, 0x28; ret
        byte[] h = [0x48, 0x89, 0xD0, 0xBA, 0x80, 0, 0, 0, 0x41, 0xB8, 8, 0, 0, 0, 0x41, 0xB9, 0xFF, 0, 0, 0,
                    0x48, 0x83, 0xEC, 0x28, 0xFF, 0xD0, 0x48, 0x83, 0xC4, 0x28, 0xC3];
        h.CopyTo(new Span<byte>(harness, h.Length));
        var run = (delegate* unmanaged<byte*, byte*, int>)harness;
        int Left(int right)
        {
            *(ushort*)(unit + 0x20) = (ushort)right;
            return run(unit, (byte*)RandomEquipment.PairStub) & 0xFF;
        }
        Check("a Two Swords unit's left hand: Koga for Iga, Iga for Koga, else the pick (kind 8: item 2)",
              Left(RandomEquipment.IgaKnife) == RandomEquipment.KogaKnife && Left(RandomEquipment.KogaKnife) == RandomEquipment.IgaKnife
              && Left(3) == 2);
        RandomEquipment.ItemData = realItem;
        RandomEquipment.WeaponData = realWeapon;
        var join = (byte*)RandomEquipment.JoinAt;
        Check($"whatever the option (a save's own can allow rare items), the after-battle join check " +
              $"(0x{RandomEquipment.JoinAt - image:X}) goes through a stub, its switch off with ReMixed's default",
              join != null && join[0] == 0xE9 && join[5] == 0x90 && join[9] == 0x90 && *RandomEquipment.JoinSwitch == 0);
        // A test copy of the stub, with rbx the unit, whose join and skip return 1 and 0.
        var joinHarness = GameImage.AllocateCode(RandomEquipment.JoinStubSize + 64);
        var exits = joinHarness + RandomEquipment.JoinStubSize;
        byte[] ends = [0xB8, 0x01, 0x00, 0x00, 0x00, 0x5B, 0xC3, 0xB8, 0x00, 0x00, 0x00, 0x00, 0x5B, 0xC3]; // eax = 1 or 0; pop rbx; ret
        ends.CopyTo(new Span<byte>(exits, ends.Length));
        var entry = exits + 16;
        byte[] enter = [0x53, 0x48, 0x89, 0xCB, 0x48, 0xB8]; // push rbx; mov rbx, rcx; mov rax, the stub; jmp rax
        enter.CopyTo(new Span<byte>(entry, enter.Length));
        *(ulong*)(entry + enter.Length) = (ulong)(nint)joinHarness;
        entry[enter.Length + 8] = 0xFF;
        entry[enter.Length + 9] = 0xE0;
        var joinSwitch = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(1);
        RandomEquipment.WriteJoinStub(joinHarness, (nint)exits, (nint)(exits + 7), joinSwitch);
        var unitData = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x100);
        int Joins(bool monster, bool invited)
        {
            unitData[6] = monster ? (byte)0x20 : (byte)0x80;
            unitData[0x62] = invited ? (byte)0x40 : (byte)0;
            return ((delegate* unmanaged<byte*, int>)entry)(unitData);
        }
        var vanilla = (Joins(false, true), Joins(true, true), Joins(true, false));
        *joinSwitch = 1;
        var rare = (Joins(false, true), Joins(true, true), Joins(true, false));
        Check("the join check: rare items left out, an invited human or monster joins and one not invited doesn't; " +
              "allowed, only an invited monster joins", vanilla == (1, 1, 0) && rare == (0, 1, 0));
        System.Runtime.InteropServices.NativeMemory.Free(unitData);
        System.Runtime.InteropServices.NativeMemory.Free(joinSwitch);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        System.Runtime.InteropServices.NativeMemory.Free(_fakeWeapons);
        System.Runtime.InteropServices.NativeMemory.Free(_fakeItems);
        return failures;
    }

    /// <summary>The spillover JP stub, through a harness that sets ebp and r15 as the game's loop has them.</summary>
    public static int SpilloverJp(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: spillover JP, {name}{(ok ? "" : ": WRONG")}");
        }
        var at = (byte*)Gains.SpilloverAt;
        Check($"the loop (0x{Gains.SpilloverAt - game.Image.ImageStart:X}) calls the stub", at != null && at[0] == 0xE8 && at[5] == 0x90);
        var harness = GameImage.AllocateCode(64);
        byte[] code =
        [
            0x55, 0x41, 0x57,             // push rbp; push r15
            0x89, 0xCD,                   // mov ebp, ecx: the JP
            0x41, 0xBF, 0x2A, 0, 0, 0,    // mov r15d, 42: the job the game passes on
            0x48, 0x83, 0xEC, 0x28,       // sub rsp, 0x28
            0xFF, 0xD2,                   // call rdx: the stub
            0x48, 0x83, 0xC4, 0x28,       // add rsp, 0x28
            0x89, 0xD0,                   // mov eax, edx: the share
            0x41, 0x83, 0xF9, 0x2A,       // cmp r9d, 42
            0x74, 0x05,                   // je +5
            0xB8, 0xFF, 0xFF, 0xFF, 0xFF, // mov eax, -1: r9d wrong
            0x41, 0x5F, 0x5D, 0xC3,       // pop r15; pop rbp; ret
        ];
        code.CopyTo(new Span<byte>(harness, code.Length));
        var stub = GameImage.AllocateCode(Gains.SpilloverStubSize);
        int Share(int jp, int divisor)
        {
            Gains.WriteSpilloverStub(stub, divisor);
            return ((delegate* unmanaged<int, byte*, int>)harness)(jp, stub);
        }
        Check("a quarter of 25 is 6, of 3 at least 1, of 0 nothing; 1/1 shares it all, 1/0 none; r9d passed on",
              Share(25, 4) == 6 && Share(3, 4) == 1 && Share(0, 4) == 0 && Share(30, 1) == 30 && Share(30, 0) == 0
              && Share(30, 15) == 2);
        return failures;
    }

    /// <summary>Arrow Guard's weapon type test, at both of the remaster's sites: 10 to 12 now.</summary>
    public static int ArrowGuardTypes(Game game, Log log)
    {
        var sites = ArrowGuard.Sites;
        byte[] want = [0x83, 0xE9, 0x0A, 0x90, 0x90, 0x83, 0xF9, 0x02, 0x77];
        var ok = sites.Length == 2 && sites.All(at => new ReadOnlySpan<byte>((void*)at, want.Length).SequenceEqual(want));
        log.Info($"self-test: Arrow Guard, both type tests ({string.Join(", ", sites.Select(s => $"0x{s - game.Image.ImageStart:X}"))}) " +
                 $"take guns, crossbows and bows (sub 0xA, cmp 2, ja){(ok ? "" : ": WRONG")}");
        return ok ? 0 : 1;
    }

    /// <summary>The three ability list builders skip an ability the unit can't learn (HideUnlearnable).</summary>
    public static int HideUnlearnableSite(Game game, Log log)
    {
        static bool Written(nint at, byte[] code) =>
            at != 0 && new ReadOnlySpan<byte>((void*)at, code.Length).SequenceEqual(code);
        static bool Lands(byte* landing, params byte[] next) =>
            landing != null && new ReadOnlySpan<byte>(landing, next.Length).SequenceEqual(next);

        var failures = 0;
        void Check(string name, nint at, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: the ability lists leave out abilities a unit can't learn, {name} " +
                     $"(0x{at - game.Image.ImageStart:X}){(ok ? "" : ": WRONG")}");
        }

        // 0x2867BC: jmp to the loop's next ability, inc rbp.
        var at = HideUnlearnable.Site;
        var landing = Written(at, HideUnlearnable.Skip) ? (byte*)at + 2 + ((sbyte*)at)[1] : null;
        Check("0x2867BC's jmp to the next one", at, Lands(landing, 0x48, 0xFF, 0xC5));

        // 0x28A474: jmp to the loop's next ability, mov rbx, [rsp + 0x60]; the store it skips (inc edi) just before.
        at = HideUnlearnable.EntriesSite;
        landing = Written(at, HideUnlearnable.EntriesSkip) ? (byte*)at + 2 + ((sbyte*)at)[1] : null;
        Check("0x28A474's jmp to the next one, past the store", at,
              Lands(landing, 0x48, 0x8B, 0x5C, 0x24, 0x60) && landing[-2] == 0xFF && landing[-1] == 0xC7);

        // 0x392620: cmp byte [rdx + r8 × 8], 0 after lea rdx, the flags (0x787F83); jl to add rsi, r13 (the loop's
        // next ability), before the count (add edi, r13d) and the entry; no 0x2000 left (xor ecx, ecx).
        at = HideUnlearnable.UnlearnedSite;
        var code = (byte*)at;
        var ok = Written(at, HideUnlearnable.UnlearnedTest);
        // The lea ends where the test starts, so its target is the test's address plus its displacement.
        ok = ok && code[-7] == 0x48 && code[-6] == 0x8D && code[-5] == 0x15 &&
             at + *(int*)(code - 4) - game.Image.ImageStart == 0x787F83;
        landing = ok ? code + 7 + ((sbyte*)code)[6] : null;
        Check("0x392620's flag test, jl to the next one", at, Lands(landing, 0x49, 0x03, 0xF5));
        return failures;
    }

    /// <summary>ReMixed's check of a battle unit's fixed reaction, support and movement.</summary>
    public static int AbilityValidation(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: ability validation, {name}{(ok ? "" : ": WRONG")}");
        }
        var entry = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(40);
        const Options.EnemyJobRule normal = Options.EnemyJobRule.Normal, expert = Options.EnemyJobRule.Expert;
        Check("an id of the slot's kind stays: Counter 0x1BA as a reaction, Equip Axe 0x1CC as a support, Move+1 0x1E6",
              MonsterReactions.Validate(0x1BA, 2, entry, normal) == 0x1BA
              && MonsterReactions.Validate(0x1CC, 4, entry, normal) == 0x1CC
              && MonsterReactions.Validate(0x1E6, 8, entry, normal) == 0x1E6);
        Check("the wrong kind becomes a random pick (0x1FE): a support as a reaction, a reaction as a movement",
              MonsterReactions.Validate(0x1CC, 2, entry, normal) == 0x1FE
              && MonsterReactions.Validate(0x1BA, 8, entry, normal) == 0x1FE);
        Check("an empty slot stays empty, also for an enemy unless Expert or Exceptional; random ids stay random",
              MonsterReactions.Validate(0, 4, entry, expert) == 0 && MonsterReactions.Validate(0x1FF, 4, entry, normal) == 0x1FF);
        entry[0x18] = 0x10;
        Check("Expert: an enemy's empty slot is a random pick; Normal: it stays empty",
              MonsterReactions.Validate(0, 4, entry, expert) == 0x1FE && MonsterReactions.Validate(0, 4, entry, normal) == 0
              && MonsterReactions.Validate(0, 2, entry, Options.EnemyJobRule.Exceptional) == 0x1FE);
        // Marking it learned, with a made-up job table and skillsets.
        var realTables = MonsterReactions.LearnedTables;
        var jobs = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(UnitTables.JobSize * 0x60);
        jobs[0x4B * UnitTables.JobSize] = 5;
        jobs[0x4C * UnitTables.JobSize] = 6;
        jobs[0x50 * UnitTables.JobSize] = 9;
        jobs[0x1E * UnitTables.JobSize] = 0x30;
        Array.Clear(FakeSkillsets);
        FakeSkillsets[6 * 22 + 17] = 0x1BA;
        FakeSkillsets[9 * 22 + 3] = 0x1BA;
        FakeSkillsets[0x30 * 22 + 18] = 0x1C6;
        FakeSkillsets[5 * 22 + 21] = 0x1CC; // the last R/S/M slot, which ReMixed doesn't search
        MonsterReactions.LearnedTables = ((nint)(delegate* unmanaged<int, int, ushort>)&FakeSkillsetAbility, (nint)jobs);
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(UnitTables.RosterSize);
        entry[1] = 0x80;
        record[0] = 0x80;
        var generic = MonsterReactions.MarkLearned(record, 0x1BA, entry);
        Check("a generic unit's Counter is learned in the first job that has it (Knight, slot 17: bit 0x40 of its third byte)",
              generic == (2, 17) && record[0xA2 + 3 * 2 + 2] == 0x40 && record[0xA2 + 3 * 6] == 0);
        record[0] = 0x1E;
        Check("a special character's own job comes first (stored as job index 0)",
              MonsterReactions.MarkLearned(record, 0x1C6, entry) == (0, 18) && record[0xA2 + 2] == 0x20);
        entry[1] = 0x20;
        record[0] = 0x80;
        Check("nothing for a monster's entry, an empty slot, a random id, or an ability only in the last R/S/M slot",
              MonsterReactions.MarkLearned(record, 0x1BA, entry) == null && MonsterReactions.MarkLearned(record, 0, entry) == null
              && MonsterReactions.MarkLearned(record, 0x1FE, entry) == null);
        entry[1] = 0x80;
        Check("…and not for 0x1CC, which only sits in slot 21", MonsterReactions.MarkLearned(record, 0x1CC, entry) == null);
        MonsterReactions.LearnedTables = realTables;
        System.Runtime.InteropServices.NativeMemory.Free(record);
        System.Runtime.InteropServices.NativeMemory.Free(jobs);
        System.Runtime.InteropServices.NativeMemory.Free(entry);
        return failures;
    }

    private static readonly ushort[] FakeSkillsets = new ushort[256 * 22];

    /// <summary>Stands in for the game's skillset slot lookup.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static ushort FakeSkillsetAbility(int skillset, int slot) =>
        (uint)skillset < 256 && (uint)slot < 22 ? FakeSkillsets[skillset * 22 + slot] : (ushort)0;

    private static readonly Queue<byte> JobsToPick = new();
    private static int RandomJobMask;

    /// <summary>Stands in for the game's random job: the next one queued, else 0.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static byte FakeRandomJob(byte* record)
    {
        RandomJobMask = record[JobLevels.Unlocked] << 16 | record[JobLevels.Unlocked + 1] << 8
                        | record[JobLevels.Unlocked + 2];
        return JobsToPick.Count > 0 ? JobsToPick.Dequeue() : (byte)0;
    }

    /// <summary>
    /// ReMixed's job levels on a made-up prerequisite table and job table: the nibble merge, each mode, Enemy
    /// Job Settings, the chapter from the roster, the secondary's job, the three random tries, and the whole
    /// step after the game's setup with its real unlocked-jobs routine.
    /// </summary>
    public static int JobLevelRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: job levels, {name}{(ok ? "" : ": WRONG")}");
        }
        if (JobLevels.SetupAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        Check("the merge keeps the higher nibble, at least 1: (0, 0) 0x11, (0x32, 0x14) 0x34, (0x80, 0x08) 0x88",
              JobLevels.Merge(0, 0) == 0x11 && JobLevels.Merge(0x32, 0x14) == 0x34 && JobLevels.Merge(0x80, 0x08) == 0x88);
        var realTable = JobLevels.Prerequisites;
        var realJobs = JobLevels.JobData;
        var realRandom = JobLevels.RandomJobFunction;
        var table = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(12 * 20);
        table[12 * 13 + 0] = 0x20; table[12 * 13 + 1] = 0x02; table[12 * 13 + 4] = 0x03; // job 13: Squire 2, Archer 2, Thief 3
        table[12 * 4 + 0] = 0x30;                                                   // job 4: Squire 3
        var jobs = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x31 * 0xB0);
        for (var j = 0x4A; j < 0x5E; j++)
            jobs[j * 0x31] = (byte)(0x10 + j - 0x4A); // job 0x4A's skillset 0x10, 0x4B's 0x11…
        JobLevels.Prerequisites = table;
        JobLevels.JobData = jobs;
        var entry = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(40);
        var levels = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(12);
        entry[8] = 13;
        entry[9] = 5;
        string Levels(int current, int secondary, int mode, Options.EnemyJobRule enemies, byte team = 0)
        {
            new Span<byte>(levels, 12).Clear();
            entry[0x18] = team;
            JobLevels.Apply(levels, entry, current, secondary, mode, enemies);
            return Convert.ToHexString(new ReadOnlySpan<byte>(levels, 10));
        }
        Check("mode 0: job 13 at 5 with its prerequisites, everything else 1",
              Levels(4, 0xFF, 0, Options.EnemyJobRule.Normal) == "21121111131115111111");
        Check("mode 1: the current job (4) at 5 too, with its own prerequisites",
              Levels(4, 0xFF, 1, Options.EnemyJobRule.Normal) == "31125111131115111111");
        Check("mode 2: the secondary's job (7) at 5 too",
              Levels(4, 7, 2, Options.EnemyJobRule.Normal) == "31125115131115111111");
        Check("Expert: an enemy's level is 8, a player unit's stays 5",
              Levels(4, 0xFF, 0, Options.EnemyJobRule.Expert, 0x10) == "21121111131118111111"
              && Levels(4, 0xFF, 0, Options.EnemyJobRule.Expert) == "21121111131115111111");
        Check("Exceptional: every job of an enemy at 8",
              Levels(4, 0xFF, 1, Options.EnemyJobRule.Exceptional, 0x10) == "88888888888888888888");
        // ReMixed's gendered slots as the build converts its battle data (remixed_levels_job): its Dark Knight slot (17)
        // is the remaster's Dark Knight, index 20, with that row's prerequisites (here Knight 8, Black Mage 8); a man's
        // slot 18 (0x92) the Bard's level (index 17) with row 18's prerequisites (here Geomancer 4).
        var wide = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(12 * 22);
        wide[12 * JobLevels.DarkKnight + 1] = 0x80;
        wide[12 * JobLevels.DarkKnight + 3] = 0x80;
        wide[12 * JobLevels.SharedSlot + 6] = 0x40;
        wide[12 * JobLevels.Bard + 4] = 0x55; // the Bard's own row, which a man's slot 18 doesn't take
        JobLevels.Prerequisites = wide;
        entry[8] = JobLevels.DarkKnight;
        entry[9] = 8;
        var knight = Levels(0xFF, 0xFF, 0, Options.EnemyJobRule.Normal);
        Check($"a Dark Knight at 8 (index 20): the 11th byte 0x{levels[10]:X2} (no Onion Knight level), Knight and Black Mage " +
              $"at 8: {knight}", levels[10] == 0x80 && levels[11] == 0 && knight == "11811181111111111111");
        entry[8] = JobLevels.MansBard;
        entry[9] = 7;
        var bard = Levels(0xFF, 0xFF, 0, Options.EnemyJobRule.Normal);
        Check($"a man's slot 18 at 7: the Bard at 7 with row 18's prerequisites, the Dancer at 1: {bard}",
              bard == "11111111111141111711" && levels[10] == 0);
        entry[8] = 13;
        entry[9] = 5;
        JobLevels.Prerequisites = table;
        System.Runtime.InteropServices.NativeMemory.Free(wide);
        var roster = JobLevels.Roster;
        var ramza = roster[0];
        int ModeAt(byte sprite)
        {
            roster[0] = sprite;
            return JobLevels.Mode(new Options());
        }
        Check("by chapter from Ramza's sprite set: 1 → mode 0, 2 → 1, 3 → 2 (and 0 → 0); the settings override it",
              ModeAt(1) == 0 && ModeAt(2) == 1 && ModeAt(3) == 2 && ModeAt(0) == 0
              && JobLevels.Mode(new Options { JobLevels = Options.JobLevelRule.SpecifiedJob }) == 0
              && JobLevels.Mode(new Options { JobLevels = Options.JobLevelRule.SpecifiedCurrentAndSecondary }) == 2);
        roster[0] = ramza;
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x258);
        record[0] = 0x80; // a generic man
        Check("the secondary's job: skillset 0x15 is job 5, an unknown one none, a random one none",
              JobLevels.SecondaryJob(record, 0x15) == 5 && JobLevels.SecondaryJob(record, 0x77) == 0xFF
              && JobLevels.SecondaryJob(record, 0xFE) == 0xFF);
        entry[0xA] = 0x4E;
        Check("the current job: 0x4E is 4; a unit in its own sprite set's job counts as 0",
              JobLevels.CurrentJob(record, entry) == 4 && JobLevels.CurrentJob((byte*)&entry[0xA] - 0, entry) == 0);
        entry[0xA] = JobLevels.DarkKnightJob;
        jobs[JobLevels.DarkKnightJob * 0x31] = 0xE0;
        Check("the Dark Knight (0xA0) as the current job and its skillset (0xE0) as the secondary's: index 20 for both",
              JobLevels.CurrentJob(record, entry) == JobLevels.DarkKnight
              && JobLevels.SecondaryJob(record, 0xE0) == JobLevels.DarkKnight);
        entry[0xA] = 0x4E;
        JobLevels.RandomJobFunction = &FakeRandomJob;
        var realReadVariable = JobLevels.ReadVariable;
        var variable6C = 0;
        JobLevels.ReadVariable = id => id == 0x6C ? variable6C : 0;
        record[JobLevels.Primary] = 0x14; // job 0x4E's skillset
        (byte, int) Pick(params byte[] queue)
        {
            JobsToPick.Clear();
            foreach (var q in queue)
                JobsToPick.Enqueue(q);
            return JobLevels.RandomSecondaryPick(record);
        }
        Check("the random secondary: a second try when the first is the primary, none after three, none at a 0",
              Pick(0x4E, 0x50) == (0x16, 0x50) && Pick(0x4E, 0x4E, 0x4E, 0x50) == (0, 0) && Pick(0, 0x50) == (0, 0));
        record[JobLevels.Unlocked] = 0x12;
        record[JobLevels.Unlocked + 1] = 0x34;
        record[JobLevels.Unlocked + 2] = 0x56;
        variable6C = 0x10000;
        _ = Pick(0x50);
        Check("the Completion Points room's unlock-all flag lends the random picker every job, then restores the unit",
              RandomJobMask == 0xFEFFFF && record[JobLevels.Unlocked] == 0x12
              && record[JobLevels.Unlocked + 1] == 0x34 && record[JobLevels.Unlocked + 2] == 0x56);
        variable6C = 0;
        // The whole step: entry with a random secondary, the real unlocked jobs.
        JobsToPick.Clear();
        JobsToPick.Enqueue(0x50);
        entry[1] = 0x80;
        entry[0xB] = 0xFE;
        entry[0x18] = 0;
        record[6] = 0x80;
        var options = new Options { JobLevels = Options.JobLevelRule.SpecifiedCurrentAndSecondary };
        // ReMixed works the unlocked jobs out before the random secondary's levels: from the first pass.
        new Span<byte>(levels, 12).Clear();
        entry[0x18] = 0;
        JobLevels.Apply(levels, entry, 4, 0xFF, 2, Options.EnemyJobRule.Normal);
        var bits = JobLevels.UnlockedFunction(levels, entry[1], record + 0xA2, record[0x1F4]);
        JobLevels.Apply(record, entry, options);
        Check("after the game's setup: ReMixed's levels, the secondary 0x16 with job 0x50's level, and the unlocked " +
              "jobs worked out again",
              record[JobLevels.Secondary] == 0x16 && (record[JobLevels.Levels + 3] >> 4) == 5
              && record[JobLevels.Unlocked] == (byte)(bits >> 16) && record[JobLevels.Unlocked + 2] == (byte)bits);
        JobLevels.RandomJobFunction = realRandom;
        JobLevels.ReadVariable = realReadVariable;
        JobLevels.Prerequisites = realTable;
        JobLevels.JobData = realJobs;
        System.Runtime.InteropServices.NativeMemory.Free(record);
        System.Runtime.InteropServices.NativeMemory.Free(levels);
        System.Runtime.InteropServices.NativeMemory.Free(entry);
        System.Runtime.InteropServices.NativeMemory.Free(jobs);
        System.Runtime.InteropServices.NativeMemory.Free(table);
        return failures;
    }

    private static readonly List<byte> PickTried = [];
    private static byte PickSucceedsOn;

    /// <summary>Stands in for the game's item pick: records the type and finds an item only for one type.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static byte FakePick(byte* unit, byte category, int kind, byte type)
    {
        PickTried.Add(type);
        return type == PickSucceedsOn ? (byte)(0x40 | (type & 0x3F)) : (byte)0xFE;
    }

    /// <summary>
    /// ReMixed's random weapon pick with a stand-in for the game's item pick: the order of the types tried,
    /// 0x1E1, Martial Arts, then the stub entered the way the game does.
    /// </summary>
    public static int WeaponPicks(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: weapon pick, {name}{(ok ? "" : ": WRONG")}");
        }
        if (WeaponPick.SiteAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var real = WeaponPick.PickFunction;
        WeaponPick.PickFunction = &FakePick;
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        string Tried(byte equip, byte third, byte fourth, byte succeed, out byte item)
        {
            unit[Battle.UnitSupports] = equip;
            unit[Battle.UnitSupports + 2] = third;
            unit[Battle.UnitSupports + 3] = fourth;
            PickTried.Clear();
            PickSucceedsOn = succeed;
            item = WeaponPick.Pick(unit, 0xFF, 0xFE);
            return string.Join(" ", PickTried.Select(t => t.ToString("X")));
        }
        Check("Equip Sword, Equip Crossbow and Two Hands try Katana, Bow, Crossbow, Sword, then any type, else none",
              Tried(0x28, 0x02, 0, 0, out var none) == "5 C B 3 FF" && none == 0xFF);
        Check("it stops at the first item (Crossbow: 0x4B)", Tried(0x28, 0x02, 0, 0xB, out var found) == "5 C B"
              && found == 0x4B);
        Check("0x1E1 goes straight to any type; 0x1C9 tries poles",
              Tried(0x28, 0, 0x10, 0, out _) == "FF" && Tried(0x10, 0, 0, 0x10, out var pole) == "10" && pole == 0x50);
        Check("Martial Arts alone: no weapon and nothing tried; with Equip Axe: Axe, then any type",
              Tried(0, 0x20, 0, 0, out var fists) == "" && fists == 0xFF && Tried(0x02, 0x20, 0, 0, out _) == "6 FF");

        var harness = GameImage.AllocateCode(64);
        byte[] code =
        [
            0x53, 0x57, 0x41, 0x57,             // push rbx; push rdi; push r15
            0x48, 0x89, 0xCB,                   // mov rbx, rcx: the unit
            0xBF, 0xFF, 0x00, 0x00, 0x00,       // mov edi, 0xFF: none
            0x41, 0xBF, 0xFE, 0x00, 0x00, 0x00, // mov r15d, 0xFE: not found
            0x48, 0x83, 0xEC, 0x28,             // sub rsp, 0x28: the stub then starts 16-aligned
            0xFF, 0xD2,                         // call rdx: the stub, back through the ret at +48
            0x89, 0xC8,                         // mov eax, ecx: the item it left in cl
            0x48, 0x83, 0xC4, 0x28,             // add rsp, 0x28
            0x41, 0x5F, 0x5F, 0x5B, 0xC3,       // pop r15; pop rdi; pop rbx; ret
        ];
        code.CopyTo(new Span<byte>(harness, code.Length));
        harness[48] = 0xC3;
        var stub = GameImage.AllocateCode(WeaponPick.StubSize);
        WeaponPick.WriteStub(stub, WeaponPick.ChooseAt, (nint)(harness + 48));
        unit[Battle.UnitSupports] = 0x01; // Equip Gun
        unit[Battle.UnitSupports + 2] = 0;
        unit[Battle.UnitSupports + 3] = 0;
        PickTried.Clear();
        PickSucceedsOn = 0xA;
        var item = ((delegate* unmanaged<byte*, byte*, int>)harness)(unit, stub) & 0xFF;
        Check($"the stub, entered as from the game, leaves the gun (0x{item:X2}) in cl; the game's block at " +
              $"0x{WeaponPick.SiteAt - game.Image.ImageStart:X} jumps to it",
              item == 0x4A && *(byte*)WeaponPick.SiteAt == 0xE9);
        WeaponPick.PickFunction = real;
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        return failures;
    }

    /// <summary>
    /// ReMixed's equip permissions, through the game's own routine (which jumps to the stub): each support
    /// adds its table mask, the secondary skillset 0x26 adds guns, the job's bits stay, women get nothing
    /// extra here, and rcx comes back as it went in (two callers rely on that).
    /// </summary>
    public static int EquipPermissions(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: equip supports, {name}{(ok ? "" : ": WRONG")}");
        }
        if (EquipSupports.RoutineAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var harness = GameImage.AllocateCode(32);
        byte[] code =
        [
            0x51,                   // push rcx: the unit
            0x48, 0x83, 0xEC, 0x20, // sub rsp, 0x20
            0xFF, 0xD2,             // call rdx: the routine
            0x48, 0x83, 0xC4, 0x20, // add rsp, 0x20
            0x58,                   // pop rax
            0x48, 0x31, 0xC8,       // xor rax, rcx: 0 if rcx came back
            0xC3,
        ];
        code.CopyTo(new Span<byte>(harness, code.Length));
        var run = (delegate* unmanaged<byte*, nint, long>)harness;
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var kept = true;
        uint Mask(int supportId, byte secondary = 0, uint job = 0, byte gender = 0x80)
        {
            new Span<byte>(unit, Battle.UnitSize).Clear();
            if (supportId != 0)
            {
                var k = supportId - 0x1C6;
                unit[Battle.UnitSupports + (k >> 3)] = (byte)(0x80 >> (k & 7));
            }
            unit[Battle.UnitSecondary] = secondary;
            unit[6] = gender;
            *(uint*)(unit + Battle.UnitEquipTypes) = job;
            kept &= run(unit, EquipSupports.RoutineAt) == 0;
            return *(uint*)(unit + Battle.UnitEquipTypes);
        }
        uint Type(int t) => (uint)(0x80 >> (t & 7)) << (8 * (t >> 3));
        const int knife = 1, sword = 3, katana = 5, axe = 6, gun = 0xA, crossbow = 0xB, bow = 0xC, spear = 0xF,
                  pole = 0x10, shield = 0x13, helmet = 0x14, armor = 0x17, clothing = 0x18;
        Check("vanilla's Equip Armor, Shield, Sword, Spear, Axe and Gun are unchanged",
              Mask(0x1C6) == (Type(helmet) | Type(armor)) && Mask(0x1C7) == Type(shield) && Mask(0x1C8) == Type(sword)
              && Mask(0x1CB) == Type(spear) && Mask(0x1CC) == Type(axe) && Mask(0x1CD) == Type(gun));
        Check("Equip Crossbow adds bows, 0x1C9 gives poles (not katanas), Two Hands gives katanas",
              Mask(0x1CA) == (Type(crossbow) | Type(bow)) && Mask(0x1C9) == Type(pole) && Mask(0x1DC) == Type(katana));
        var all = Mask(0x1E1);
        Check($"0x1E1 gives every weapon, bags, cloths and shields (0x{all:X8})",
              (all & Type(knife)) != 0 && (all & Type(spear)) != 0 && (all & Type(pole)) != 0 && (all & Type(shield)) != 0
              && (all & Type(helmet)) == 0 && (all & Type(clothing)) == 0);
        Check("a secondary of Snipe (0x26) adds guns; the job's own bits stay; a woman gets nothing extra here",
              Mask(0, 0x26) == Type(gun) && Mask(0x1C8, 0, Type(clothing)) == (Type(clothing) | Type(sword))
              && Mask(0, 0, 0, 0x40) == 0 && Mask(0x1D0) == 0);
        Check($"rcx comes back as it went in (the routine at 0x{EquipSupports.RoutineAt - game.Image.ImageStart:X} " +
              "jumps to the stub)", kept && *(byte*)EquipSupports.RoutineAt == 0xE9);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        return failures;
    }

    /// <summary>
    /// ReMixed's zodiac: compatibility by sign and gender for each Sexuality setting, the damage step's
    /// arithmetic for each strength (and its byte quirk), then both hooked routines.
    /// </summary>
    public static int ZodiacRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: zodiac, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Zodiac.StepAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var a = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var b = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        const byte male = 0x80, female = 0x40, monster = 0x20;
        void Set(int signA, byte kindA, int signB, byte kindB)
        {
            *(ushort*)(a + 8) = (ushort)(signA << 12);
            *(ushort*)(b + 8) = (ushort)(signB << 12);
            a[6] = kindA;
            b[6] = kindB;
        }
        int Of(int signA, byte kindA, int signB, byte kindB, Options.SexualityRule rule = Options.SexualityRule.Straight)
        {
            Set(signA, kindA, signB, kindB);
            return Zodiac.Of(a, b, rule);
        }
        Check("by sign: 4 apart Good, 3 apart Bad, 1 apart none, Serpentarius none",
              Of(0, male, 4, male) == Zodiac.Good && Of(9, male, 0, male) == Zodiac.Bad && Of(0, male, 1, male) == 0
              && Of(12, male, 6, male) == 0);
        Check("opposite signs, Straight (the PS1's): man and woman Best, two men Worst, with a monster Bad",
              Of(0, male, 6, female) == Zodiac.Best && Of(0, male, 6, male) == Zodiac.Worst
              && Of(0, female, 6, female) == Zodiac.Worst && Of(0, male, 6, monster) == Zodiac.Bad);
        Check("Gay the other way round; Bisexual and Tic (the remaster's) always Best",
              Of(0, male, 6, male, Options.SexualityRule.Gay) == Zodiac.Best
              && Of(0, male, 6, female, Options.SexualityRule.Gay) == Zodiac.Worst
              && Of(0, male, 6, male, Options.SexualityRule.Bisexual) == Zodiac.Best
              && Of(0, female, 6, male, Options.SexualityRule.Tic) == Zodiac.Best);
        var cell = (short*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(2);
        int Xa(int value, int signB, byte kindB, Options.ZodiacRule strength = Options.ZodiacRule.Normal)
        {
            Set(0, male, signB, kindB);
            *cell = (short)value;
            Zodiac.Apply(cell, a, b, Options.SexualityRule.Straight, strength);
            return *cell;
        }
        Check("XA 100: Good 125, Bad 75, Best 150, Worst 50, none 100",
              Xa(100, 4, male) == 125 && Xa(100, 3, male) == 75 && Xa(100, 6, female) == 150 && Xa(100, 6, male) == 50
              && Xa(100, 1, male) == 100);
        Check("Half: Good 112, Best 125; OneAndAHalf: Good 137, Worst 25; Off leaves 100",
              Xa(100, 4, male, Options.ZodiacRule.Half) == 112 && Xa(100, 6, female, Options.ZodiacRule.Half) == 125
              && Xa(100, 4, male, Options.ZodiacRule.OneAndAHalf) == 137
              && Xa(100, 6, male, Options.ZodiacRule.OneAndAHalf) == 25 && Xa(100, 6, male, Options.ZodiacRule.Off) == 100);
        Check("ReMixed's quirks: XA 300 Good is 44 + 75 = 119, and at least 1 (XA 1 Worst stays 1, 0 becomes 1)",
              Xa(300, 4, male) == 119 && Xa(1, 6, male) == 1 && Xa(0, 1, male) == 1);

        var bt = game.Battle;
        var attackerBefore = *bt.Attacker;
        var targetBefore = *game.Routines.Target;
        var xaBefore = *bt.XA;
        var shownBefore = *Zodiac.Shown;
        Set(0, male, 6, male);
        *bt.Attacker = a;
        *game.Routines.Target = b;
        *bt.XA = 80;
        ((delegate* unmanaged<void>)Zodiac.StepAt)();
        var stepped = *bt.XA;
        var shown = ((delegate* unmanaged<byte*, byte*, int>)Zodiac.ShownAt)(a, b);
        var shownNull = ((delegate* unmanaged<byte*, byte*, int>)Zodiac.ShownAt)(a, null);
        Check($"the hooked damage step gives two men of opposite signs Worst (XA 80 → {stepped}), and so does the screen's " +
              "compatibility (0 with a missing unit)",
              stepped == 40 && shown == Zodiac.Worst && shownNull == 0 && *Zodiac.Shown == 0);
        *bt.Attacker = attackerBefore;
        *game.Routines.Target = targetBefore;
        *bt.XA = xaBefore;
        *Zodiac.Shown = shownBefore;
        System.Runtime.InteropServices.NativeMemory.Free(cell);
        System.Runtime.InteropServices.NativeMemory.Free(a);
        System.Runtime.InteropServices.NativeMemory.Free(b);
        return failures;
    }

    /// <summary>
    /// Poison &amp; Regen on MP, Auto Renew and Starting MP: the tick's rules on their own, then test copies of
    /// both stubs, entered the way the game does (the tick's by a jump with rbx the unit, 16-aligned).
    /// </summary>
    /// <summary>
    /// Each save's options (SaveOptions): ReMixed's own defaults in its places read as the mod's defaults, the mod's
    /// defaults write ReMixed's (var 0x14 = 0x1004647D, every flag clear), and every setting comes back as written.
    /// </summary>
    public static int EachSavesOptions(Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: each save's options, {name}{(ok ? "" : ": WRONG")}");
        }
        var save = new Dictionary<int, int>();
        int Read(int id) => save.TryGetValue(id, out var v) ? v : 0;
        void Write(int id, int value) => save[id] = value;
        var settings = new Options
        {
            TestMode = Options.TestModeRule.Record, ArgathCampaign = true, FastForward = Options.FastForwardRule.Toggle,
        };
        save[SaveOptions.Rates] = 0x1004647D; // ReMixed's defaults, as its options event sets them
        var read = SaveOptions.Decode(Read, settings);
        var expected = new Options
        {
            TestMode = Options.TestModeRule.Record, ArgathCampaign = true, FastForward = Options.FastForwardRule.Toggle,
        };
        Check("ReMixed's defaults in a save are the mod's defaults; the settings with no place in a save are the mod's",
              Options.Same(read, expected));
        save.Clear();
        SaveOptions.Encode(new Options(), Read, Write);
        Check("the mod's defaults write ReMixed's: var 0x14 0x1004647D, var 0x16 and 0x40 zero, every flag clear",
              Read(SaveOptions.Rates) == 0x1004647D && Read(SaveOptions.Powers) == 0 && Read(SaveOptions.NemesisWord) == 0
              && SaveOptions.Flags.All(f => Read(f) == 0));
        var all = new Options
        {
            PlayerDeaths = Options.PlayerDeathRule.ReviveAfterBattle, Bodies = Options.BodyRule.TreasureBox, JpGain = 200,
            SpilloverJp = 15, JpRefund = Options.JpRefundRule.None, ExpGain = 37, VictoryBonus = Options.VictoryBonusRule.Both,
            NemesisSystem = false, BackAttacks = Options.BackAttackRule.Everyone, Bravery = Options.BraveryRule.Vanilla,
            Knockback = Options.KnockbackRule.Vanilla, WeaponRange = Options.WeaponRangeRule.MinimumOf3,
            GuestControl = Options.GuestControlRule.Always, PoachingOdds = Options.PoachRule.Vanilla,
            PoisonAndRegen = Options.PoisonRegenRule.HpAndMp, ZodiacStrength = Options.ZodiacRule.OneAndAHalf,
            Sexuality = Options.SexualityRule.Bisexual, PowerMode = Options.PowerModeRule.Custom, AllyPower = 255,
            EnemyPower = 1, AllyMathPower = 150, EnemyMathPower = 200, AutoRenew = true,
            StartingMp = Options.StartingMpRule.Ten, EveryoneNonCharge = true, LevelScaling = Options.LevelScalingRule.EntireRoster,
            FixedLevels = Options.FixedLevelRule.IfPartyLevelIsHigher, EnemyLevelBoost = Options.LevelBoostRule.NoFuture,
            JobLevels = Options.JobLevelRule.SpecifiedCurrentAndSecondary, EnemyJobs = Options.EnemyJobRule.Exceptional,
            RareEquipment = Options.RareEquipmentRule.Allowed, JobStats = Options.JobStatRule.JobAndSecondary,
        };
        save.Clear();
        save[SaveOptions.NemesisWord] = 0x30801234; // a Nemesis: a countdown, a man, name 0x1234
        SaveOptions.Encode(all, Read, Write);
        var back = SaveOptions.Decode(Read, new Options());
        Check("every setting written comes back, the custom percentages too",
              Options.Same(back, all) && (Read(SaveOptions.Rates) & SaveOptions.Marker) != 0);
        Check("the Nemesis System off sets var 0x40's bit 0x100000 and keeps the Nemesis itself",
              Read(SaveOptions.NemesisWord) == (0x30801234 | SaveOptions.NemesisOff));
        foreach (var mode in new[] { Options.PowerModeRule.Squire, Options.PowerModeRule.Tactician, Options.PowerModeRule.Chemist })
        {
            save.Clear();
            SaveOptions.Encode(new Options { PowerMode = mode }, Read, Write);
            Check($"the {mode} power mode is ReMixed's preset 0x{SaveOptions.Power(new Options { PowerMode = mode }):X8} and reads back",
                  SaveOptions.Decode(Read, new Options()).PowerMode == mode);
        }
        return failures;
    }

    /// <summary>
    /// The ReMixed tab of the Options screen (OptionsPage): its code is where it expects, the build's list loads, each
    /// option's default is one of its choices (so a new save shows a choice, not "set in the mod's settings"), a
    /// change of choice changes only its setting, and a choices table reads back as the screen reads one.
    /// </summary>
    public static int OptionsPageList(Game game, string modDir, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: options page, {name}{(ok ? "" : ": WRONG")}");
        }
        var patterns = new[] { OptionsPage.TabsSet, OptionsPage.ShowRow, OptionsPage.ItemChanged, OptionsPage.BuildTab,
                               OptionsPage.ItemDefault, OptionsPage.HostTableLoad, OptionsPage.RowByKey,
                               OptionsPage.ListCount };
        var found = patterns.Select(p => { try { return game.Image.FindUnique(p) - game.Image.ImageStart; } catch { return -1; } }).ToArray();
        Check($"the screen's code is where it expects ({string.Join(", ", found.Select(f => $"0x{f:X}"))})", found.All(f => f > 0));
        var path = Path.Combine(modDir, OptionsPage.FileName);
        if (!File.Exists(path))
        {
            Check($"the build's list ({path}) is there", false);
            return failures;
        }
        var page = OptionsPage.Load(path);
        var options = page.Groups.SelectMany(g => g.Options).ToArray();
        Check($"{options.Length} options in {page.Groups.Length} groups, rows {string.Join(",", page.Slots)}, none of them " +
              "an item the game uses or past the screen's arrays",
              options.Length == options.Select(o => o.Setting.Name).Distinct().Count() && page.Slots.Length > 1
              && page.Slots.All(s => s is 28 or 29 or (>= 34 and <= 38) or (>= 40 and <= 45) or (>= 57 and <= 60)));
        var defaults = new Options();
        var missing = options.Where(o => OptionsPage.Current(o, defaults) < 0).Select(o => o.Setting.Name).ToArray();
        Check($"each option's default is one of its choices{(missing.Length > 0 ? " (not: " + string.Join(", ", missing) + ")" : "")}",
              missing.Length == 0);
        var nemesis = options.First(o => o.Setting.Name == nameof(Options.NemesisSystem));
        var off = OptionsPage.With(nemesis, Array.FindIndex(nemesis.Choices, c => Equals(c.Value, false)), defaults);
        Check("a choice changes its setting and nothing else",
              !off.NemesisSystem && defaults.NemesisSystem && Options.Same(off, new Options { NemesisSystem = false }));
        var custom = new Options { JpGain = 37 };
        Check("a setting none of the choices has (set in the mod's settings) has no index",
              OptionsPage.Current(options.First(o => o.Setting.Name == nameof(Options.JpGain)), custom) == -1);
        Check("only Controls → Fast-forward is kept for every save (in the mod's settings), not in the save",
              options.Where(o => o.Mod).Select(o => o.Setting.Name).SequenceEqual(
                  options.Any(o => o.Setting.Name == nameof(Options.FastForward))
                      ? new[] { nameof(Options.FastForward) } : Array.Empty<string>()));
        var fastForward = options.FirstOrDefault(o => o.Setting.Name == nameof(Options.FastForward));
        if (fastForward == null)
            log.Info("self-test: options page, this list has no Controls group (one from before it; a full build adds it)");
        else
            Check("Controls → Fast-forward's choices are Hold and Toggle", fastForward.Choices.Select(c => c.Value)
                      .SequenceEqual(new object[] { Options.FastForwardRule.Hold, Options.FastForwardRule.Toggle }));
        // A change kept for every save, with a Config.json of its own: the mod's settings and the file get it, the
        // options in play get it with the save's own as they were.
        var kept = fastForward ?? new OptionsPage.Option(typeof(Options).GetProperty(nameof(Options.FastForward))!, 0, 0,
            [new(Options.FastForwardRule.Hold, 0), new(Options.FastForwardRule.Toggle, 0)], true);
        var (settings, current) = (Options.Settings, Options.Current);
        var config = Path.GetTempFileName();
        try
        {
            var mine = settings.Copy();
            mine.FilePath = config;
            mine.JpGain = 150;
            Options.Settings = mine;
            OptionsPage.Keep(kept, Options.FastForwardRule.Toggle);
            var written = Options.Read(config, out var problem);
            var back = Options.Current.Copy();
            back.FastForward = current.FastForward;
            Check("Fast-forward set on the page goes into the mod's settings and their Config.json, and into the options " +
                  "in play, the save's own left as they were",
                  Options.Settings.FastForward == Options.FastForwardRule.Toggle && Options.Settings.JpGain == 150
                  && problem == null && written.FastForward == Options.FastForwardRule.Toggle && written.JpGain == 150
                  && Options.Current.FastForward == Options.FastForwardRule.Toggle && Options.Same(back, current));
        }
        finally
        {
            Options.Settings = settings;
            Options.Use(current);
            File.Delete(config);
        }
        var table = (nint)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)OptionsPage.ChoicesBytes);
        var texts = new List<int> { 20005, 20006, 20007 };
        OptionsPage.FillChoices(table, texts);
        var read = Enumerable.Range(0, 3).Select(k => OptionsPage.ReadChoice(table, k)).ToArray();
        Check("a choices table reads back as the screen reads one: its count, each row's value (its index) and text",
              read.All(r => r.Count == 3) && read.Select(r => r.Value).SequenceEqual([0, 1, 2])
              && read.Select(r => r.Text).SequenceEqual(texts) && OptionsPage.ReadChoice(table, 3).Value == -1);
        System.Runtime.InteropServices.NativeMemory.Free((void*)table);
        return failures;
    }

    /// <summary>
    /// Reveal Treasures (RevealTreasures): its code is where it expects, ReMixed's price (a quarter of the items' prices,
    /// at most gil × panels / 40, as its 200 gil of 2000 at Gariland), the dialog's two numbers laid out as the game
    /// passes them, and the payment written into the formation's copy of the event variables as the game lays them out.
    /// </summary>
    public static int RevealTreasureRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: reveal treasures, {name}{(ok ? "" : ": WRONG")}");
        }
        var patterns = new[] { RevealTreasures.FormationFrame, RevealTreasures.FormationUi, RevealTreasures.AnswerPoll,
                               RevealTreasures.ShowBox, RevealTreasures.SetDialog, RevealTreasures.SetNumbers,
                               RevealTreasures.SetKind, RevealTreasures.ClearPanels, RevealTreasures.KeepVariables,
                               RevealTreasures.PutBackVariables };
        var found = patterns.Select(p => { try { return game.Image.FindUnique(p) - game.Image.ImageStart; } catch { return -1; } }).ToArray();
        Check($"the formation's code is where it expects ({string.Join(", ", found.Select(f => $"0x{f:X}"))})", found.All(f => f > 0));
        int kept;
        try { kept = RevealTreasures.FindKept(game.Image); } catch { kept = -1; }
        Check($"the formation keeps the event variables at its +0x{kept:X} (+0x68), put aside and put back from one block",
              kept == 0x68);
        var copy = (nint)System.Runtime.InteropServices.NativeMemory.AllocZeroed(SaveOptions.BlockBytes);
        SaveOptions.WriteIn(copy, 0x2C, 18155);
        SaveOptions.WriteIn(copy, 0x100 + 103, 1);
        SaveOptions.WriteIn(copy, 0x100 + 104, 1);
        SaveOptions.WriteIn(copy, 0x100 + 104, 0);
        SaveOptions.WriteIn(copy, 0x361, 0x1B);
        Check("the payment in a copy of the block: gil (var 0x2C) at +0xB0, map 103's flag 0x167 at +0x21C bit 7, a flag " +
              "cleared again, nibble 0x361 at +0x25C bits 4-7 (cut to four bits), as 0x242984 places them",
              *(int*)(copy + 0xB0) == 18155 && *(uint*)(copy + 0x21C) == 0x80 && *(uint*)(copy + 0x25C) == 0xB0
              && SaveOptions.ReadIn(copy, 0x2C) == 18155 && SaveOptions.ReadIn(copy, 0x167) == 1
              && SaveOptions.ReadIn(copy, 0x168) == 0 && SaveOptions.ReadIn(copy, 0x361) == 0xB
              && new Span<byte>((void*)copy, SaveOptions.BlockBytes).ToArray().Count(b => b != 0) == 4);
        System.Runtime.InteropServices.NativeMemory.Free((void*)copy);
        var prices = new Dictionary<int, int> { [0xBC] = 500, [0xF8] = 50, [0xC8] = 1200, [0xFD] = 300, [0xE5] = 300,
                                               [0xF0] = 50, [0x04] = 1500, [0xF1] = 200 };
        var panels = new List<(int Rare, int Common)> { (0xBC, 0xF8), (0xC8, 0xFD), (0xE5, 0xF0), (0x04, 0xF1) };
        Check("four panels' items at 4100 gil cost 1025 with 19180 gil (a quarter of their prices)",
              RevealTreasures.Cost(panels, 19180, i => prices[i]) == 1025);
        Check("and 200 with 2000 gil (gil × 4 / 40, as ReMixed asked at Gariland)",
              RevealTreasures.Cost(panels, 2000, i => prices[i]) == 200);
        Check("one panel left with 20000 gil: (1500 + 200) / 4 = 425, under 20000 × 1 / 40 = 500",
              RevealTreasures.Cost([(0x04, 0xF1)], 20000, i => prices[i]) == 425);
        Check("and with 10000 gil the cap, 10000 × 1 / 40 = 250",
              RevealTreasures.Cost([(0x04, 0xF1)], 10000, i => prices[i]) == 250);
        Check("nothing with no gil", RevealTreasures.Cost(panels, 0, i => prices[i]) == 0);
        var vector = (nint)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x20 + 2 * RevealTreasures.NumberBytes);
        RevealTreasures.Numbers(vector, 1025, 19180);
        var first = *(nint*)(vector + 8);
        Check("the dialog's numbers: two entries of 0x108 bytes, each value at +0 and type 1 at +0x100",
              *(nint*)(vector + 0x10) - first == 2 * RevealTreasures.NumberBytes && *(int*)first == 1025
              && *(int*)(first + RevealTreasures.NumberType) == 1 && *(int*)(first + RevealTreasures.NumberBytes) == 19180
              && *(int*)(first + RevealTreasures.NumberBytes + RevealTreasures.NumberType) == 1);
        System.Runtime.InteropServices.NativeMemory.Free((void*)vector);
        var arrowCode = new[] { TreasureArrows.SpriteAtPattern, TreasureArrows.QueuePattern, TreasureArrows.LoaderPattern,
                                TreasureArrows.RemovePattern, TreasureArrows.PalettePattern, TreasureArrows.AnimatePattern,
                                TreasureArrows.ShowPattern, TreasureArrows.TakePattern, TreasureArrows.ReadPattern,
                                TreasureArrows.TexturePattern, TreasureArrows.UploadPattern, TreasureArrows.InputPattern,
                                TreasureArrows.PlacePattern };
        var arrowsAt = arrowCode.Select(p => { try { return game.Image.FindUnique(p) - game.Image.ImageStart; } catch { return -1; } }).ToArray();
        Check($"the arrows' code is where it expects ({string.Join(", ", arrowsAt.Select(f => $"0x{f:X}"))})",
              arrowsAt.All(f => f > 0));
        string installed;
        try
        {
            TreasureArrows.Install(game, _ => { });
            installed = "";
        }
        catch (Exception e)
        {
            installed = e.Message;
        }
        var arrowPaletteIdsAt = installed == "" ? (nint)TreasureArrows.SlotPaletteIds - game.Image.ImageStart : -1;
        Check($"the arrows' hooks and the event slots' layout; the palette ids preserved before the event-graphics " +
              $"patch are reused at 0x{arrowPaletteIdsAt:X}{(installed == "" ? "" : $" ({installed})")}",
              installed == "" && arrowPaletteIdsAt == 0x2E7FEF4);
        if (installed == "")
        {
            // The tile lookup's replacement, run on a made-up list: an arrow first, then a unit on its tile, then another.
            var listHead = TreasureArrows.ListHead;
            var gameHead = *listHead;
            var sprites = (nint)System.Runtime.InteropServices.NativeMemory.AllocZeroed(3 * 0x100);
            nint Sprite(int k, byte x, byte y, byte level, nint next)
            {
                var s = sprites + k * 0x100;
                (((byte*)s)[TreasureArrows.X], ((byte*)s)[TreasureArrows.Y], ((byte*)s)[TreasureArrows.Level]) = (x, y, level);
                *(nint*)s = next;
                return s;
            }
            var other = Sprite(2, 5, 6, 1, 0);
            var unit = Sprite(1, 3, 4, 0, other);
            var arrow = Sprite(0, 3, 4, 0, unit);
            *listHead = arrow;
            var find = (delegate* unmanaged<int, int, int, nint>)TreasureArrows.LookupAt;
            var unlisted = find(3, 4, 0);
            TreasureArrows.ArrowTable[3] = arrow;
            var (passed, upper, none) = (find(3, 4, 0), find(5, 6, 1), find(5, 6, 0));
            TreasureArrows.ArrowTable[3] = 0;
            *listHead = gameHead;
            System.Runtime.InteropServices.NativeMemory.Free((void*)sprites);
            Check("the tile lookup's replacement finds a tile's first sprite (the arrow, with none listed), passes over a " +
                  "listed arrow to the unit under it, and matches the level",
                  unlisted == arrow && passed == unit && upper == other && none == 0);
        }
        Check("the arrows' palettes are ReMixed's for panels 0-3 (4, 2, 3, 5), grey (6) with the rare item found",
              Enumerable.Range(0, 4).Select(p => TreasureArrows.PaletteFor(p, false)).SequenceEqual([4, 2, 3, 5])
              && Enumerable.Range(0, 4).All(p => TreasureArrows.PaletteFor(p, true) == 6));
        Check("slot 0's arrow is ReMixed's animation 0x218, slot 1's 0x27C",
              TreasureArrows.AnimationFor(0) == 0x218 && TreasureArrows.AnimationFor(1) == 0x27C);
        // A made-up list of six sprites (+0 the next): units 0, 1, 2, then arrows 3 and 4 made later, so first.
        var nodes = (nint)System.Runtime.InteropServices.NativeMemory.AllocZeroed(6 * 0x10);
        nint Node(int k) => nodes + k * 0x10;
        var head = (nint*)Node(5);
        string Order()
        {
            var seen = new List<int>();
            for (var s = *head; s != 0 && seen.Count < 8; s = *(nint*)s)
                seen.Add((int)((s - nodes) / 0x10));
            return string.Join(" ", seen);
        }
        foreach (var k in new[] { 2, 1, 0, 3, 4 })
            (*(nint*)Node(k), *head) = (*head, Node(k)); // each goes first, as the game adds sprites
        TreasureArrows.ToEnd(head, Node(3));
        TreasureArrows.ToEnd(head, Node(4));
        Check($"arrows moved to the list's end: {Order()}", Order() == "0 1 2 3 4");
        nint[] arrows = [Node(3), Node(4)];
        var cut = TreasureArrows.Cut(head, arrows);
        var cutOrder = Order();
        TreasureArrows.Join(head, cut);
        Check($"the arrows cut off for the formation's input ({cutOrder}) and put back ({Order()})",
              cutOrder == "0 1 2" && Order() == "0 1 2 3 4");
        TreasureArrows.Unlink(head, Node(2)); // unit 2 taken off and placed again: first
        (*(nint*)Node(2), *head) = (*head, Node(2));
        Check($"a unit placed meanwhile goes first, the arrows stay last: {Order()}", Order() == "2 0 1 3 4");
        TreasureArrows.Unlink(head, Node(3));
        (*(nint*)Node(3), *head) = (*head, Node(3));
        Check($"an arrow not at the end isn't cut off ({Order()})", TreasureArrows.Cut(head, arrows) == 0 && Order() == "3 2 0 1 4");
        System.Runtime.InteropServices.NativeMemory.Free((void*)nodes);
        return failures;
    }


    /// <summary>
    /// ReMixed's text made while its events run (RemixedText): the line lookup is where it expects, and the tavern's
    /// slots fill in as ReMixed's do: a propositions menu's rows from its words (a colour that shows nothing, a title, a
    /// line break each), the empty rows left out; a price and a set by the words they name; a count and a plural's "s".
    /// </summary>
    public static int RemixedTextRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: remixed text, {name}{(ok ? "" : ": WRONG")}");
        }
        long at;
        try { at = game.Image.FindUnique(RemixedText.LinePattern) - game.Image.ImageStart; } catch { at = -1; }
        Check($"the event line lookup is where it expects (0x{at:X})", at > 0);
        var parts = new Dictionary<int, string> { [3] = "", [4] = "\n", [10] = "Seafarer Expedition", [13] = "Salvage Expedition",
                                                  [41] = "s" };
        string? Part(int word) => word < 0x1000 ? parts.GetValueOrDefault(word + 1) : $"[0x{word:X}]";
        int[] menu = [2, 9, 3, 2, 12, 3, 2, 2, 2];
        var rows = RemixedText.Fill("<cursor>{eb}{eb}{eb}{eb}{eb}{eb}{eb}{eb}{eb}Cancel<cursorend><forced>",
                                    k => k < menu.Length ? menu[k] : 2, Part);
        Check($"a propositions menu's rows: \"{rows}\"",
              rows == "<cursor>Seafarer Expedition\nSalvage Expedition\nCancel<cursorend><forced>");
        var words = new Dictionary<int, int> { [0x20] = 250, [0x23] = 1 };
        var price = RemixedText.Fill("Here's Set {w:23} of the Salvaging Propositions. They'll cost {w:20} gil.",
                                     k => words.GetValueOrDefault(k), Part);
        Check($"a set and its price: \"{price}\"", price == "Here's Set 1 of the Salvaging Propositions. They'll cost 250 gil.");
        int[] count = [2, 40];
        var plural = RemixedText.Fill("You've got {n} active mission{eb}. Want to call a group back?", k => count[k], Part);
        Check($"a count and a plural: \"{plural}\"", plural == "You've got 2 active missions. Want to call a group back?");
        var main = RemixedText.Fill("<cursor>{eb}Propositions\n{eb}Jobs\nExit Tavern<cursorend><forced>", _ => 14, Part);
        Check($"a menu with no colours: \"{main}\"", main == "<cursor>Propositions\nJobs\nExit Tavern<cursorend><forced>");
        parts[8] = "</color>";  // ReMixed's colour messages (E3 00, E3 04) as the build writes them
        parts[9] = "<color=116>";
        parts[30] = "<icon=460>";  // its zodiac glyph DA 0A, Aquarius
        int[] rowColours = [8, 7];
        var greyed = RemixedText.Fill("<cursor>{eb}Propositions\n{eb}Jobs\nExit Tavern<cursorend><forced>",
                                      k => rowColours[k], Part);
        Check($"a menu's row that can't be picked, greyed: \"{greyed}\"",
              greyed == "<cursor><color=116>Propositions</color>\nJobs\nExit Tavern<cursorend><forced>");
        var balanced = RemixedText.Balance("</color><color=61>For now,</color> anyway.<color=116>\nGo Back");
        Check($"colours paired up across a line break: \"{balanced}\"",
              balanced == "<color=61>For now,</color> anyway.\n<color=116>Go Back</color>");
        int[] card = [2, 7, 0x5801, 1, 0x304A, 0x1D, 7, 0x41, 0x2D];  // as ReMixed's list held them on the PS1
        var unit = RemixedText.Fill("<color=61>{n2} {eb}{eb} Lv. {n}\n    {eb}\n</color><color=116>{eb}</color>  {eb}Brave {n3}   Faith {n3}",
                                    k => card[k], Part, laidOut: true);
        Check($"a unit's card as the PS1 lays it out, its numbers in their fields: \"{unit}\"",
              unit == "<color=61><nbsp>2 </color>[0x5801] Lv. 1\n    [0x304A]\n" +
                      "<color=116><icon=460></color>  Brave <nbsp>65   Faith <nbsp>45");
        return failures;
    }

    /// <summary>
    /// The dispatch tavern's saved state (TavernStorage): the remaster's errand count, records and count downs where it
    /// expects (from the day change's code), the day's step by ReMixed's rule, and a mission state's nibble where the
    /// game keeps one (an even id in a byte's low half).
    /// </summary>
    public static int TavernStorageRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: dispatch tavern, {name}{(ok ? "" : ": WRONG")}");
        }
        long Off(nint at) => at == 0 ? -1 : at - game.Image.ImageStart;
        if (TallMenus.CursorAt == 0)
            TallMenus.Install(game);
        Check($"tall menus: the cursor 0x{Off(TallMenus.CursorAt):X}, a box's layout 0x{Off(TallMenus.LayoutAt):X} and a battle " +
              $"event's 0x{Off(TallMenus.BattleLayoutAt):X}, a side's record 0x{Off(TallMenus.RecordAt):X}, the cursor's move " +
              $"0x{Off(TallMenus.FocusAt):X}",
              Off(TallMenus.CursorAt) == 0x1365AC && Off(TallMenus.LayoutAt) == 0x2D9490 && Off(TallMenus.BattleLayoutAt) == 0x221C00
              && Off(TallMenus.RecordAt) == 0x13AAC8 && Off(TallMenus.FocusAt) == 0x1D2240 && Off(TallMenus.ButtonAt) == 0x135D18);
        Check($"frameless lines on without a press: a box's line 0x{Off(TallMenus.LineTextAt):X}, the press checks " +
              string.Join(" and ", TallMenus.PressedAt.Select(a => $"0x{Off(a):X}")),
              Off(TallMenus.LineTextAt) == 0x221994 && TallMenus.PressedAt.Count == 2 && Off(TallMenus.PressedAt[0]) == 0x22169C
              && Off(TallMenus.PressedAt[1]) == 0x2D9394);
        // The cost question: three lines of text, then No (row 3) and Yes (row 4); asked by the menu's row or by one
        // counted from its first choice. A menu of choices alone keeps its rows as they are.
        var lines = new[] { TallMenus.CursorLine(3, 3, 3), TallMenus.CursorLine(0, 3, 3), TallMenus.CursorLine(4, 3, 4),
                            TallMenus.CursorLine(1, 3, 4), TallMenus.CursorLine(5, 0, 5), TallMenus.CursorLine(1, 0, 6) };
        Check($"the cursor beside its choice under lines of text: No {lines[0]}/{lines[1]}, Yes {lines[2]}/{lines[3]}; " +
              $"a menu of choices alone: {lines[4]}, {lines[5]}", lines.SequenceEqual([3, 3, 4, 4, 5, 1]));
        var day = TavernStorage.DayChangeAt == 0 ? -1 : TavernStorage.DayChangeAt - game.Image.ImageStart;  // hooked by now
        long count = (nint)TavernStorage.Count - game.Image.ImageStart, records = (nint)TavernStorage.RecordsAt - game.Image.ImageStart,
             states = (nint)TavernStorage.States - game.Image.ImageStart;
        Check($"the native day change 0x{day:X}, the errand count 0x{count:X}, records 0x{records:X}, count downs 0x{states:X}",
              day == 0x2CCEE0 && count == 0xD407B2 && records == 0x11A71A0 && states == 0x11A7140);
        long list = TavernStorage.ErrandListAt == 0 ? -1 : TavernStorage.ErrandListAt - game.Image.ImageStart,
             menus = TavernStorage.NewInMenuAt == 0 ? -1 : TavernStorage.NewInMenuAt - game.Image.ImageStart,
             report = TavernStorage.ReportAt == 0 ? -1 : TavernStorage.ReportAt - game.Image.ImageStart,
             profit = TavernStorage.ProfitRowsAt == 0 ? -1 : TavernStorage.ProfitRowsAt - game.Image.ImageStart,
             numeric = Off(TavernStorage.NumericProfitAt), prepare = Off(TavernStorage.PrepareAt),
             send = Off(TavernStorage.SendAt), row = Off(TavernStorage.TableRowAt), barState = Off(TavernStorage.BarStateAt),
             profitUpdate = Off(TavernStorage.ProfitMainUpdateAt), profitState = Off(TavernStorage.ProfitMainStateAt),
             profitShow = Off(TavernStorage.ProfitMainShowAt), profitHeader = Off(TavernStorage.ProfitHeaderAt),
             setText = Off(TavernStorage.SetTextAt),
             selected = (nint)TavernStorage.SelectedRow - game.Image.ImageStart,
             profitTable = TavernStorage.ProfitTableSlot - game.Image.ImageStart,
             profitLoader = TavernStorage.ProfitTableLoaderAt - game.Image.ImageStart;
        Check($"the additive dispatch hooks: list 0x{list:X}, bounded new-menu check 0x{menus:X}, report 0x{report:X}, numeric row 0x{numeric:X}, " +
              $"prepare 0x{prepare:X}, send 0x{send:X}, localized row 0x{row:X}, Tavern state 0x{barState:X}, " +
              $"catalog update/state/show/header/text 0x{profitUpdate:X}/0x{profitState:X}/0x{profitShow:X}/" +
              $"0x{profitHeader:X}/0x{setText:X}, " +
              $"Profit cache 0x{profitTable:X}/loader 0x{profitLoader:X}",
              list == 0x16E7F4 && menus == 0x162D38 && report == 0x370054 && profit == 0x7B768C
              && numeric == 0x2CCDC4 && prepare == 0x2ED078 && send == 0x36A7D8 && row == 0x3DBC74
              && barState == 0x19DCD4 && selected == 0xD49434 && profitTable == 0x3CDA500
              && profitUpdate == 0x1A1714 && profitState == 0x1A1848 && profitShow == 0x19F160
              && profitHeader == 0x1A1600 && setText == 0x3F106C
              && profitLoader == 0x3D325C);
        var firstCourse = (byte*)TavernStorage.ProfitRowsAt + TavernStorage.TrainingFirst * TavernStorage.ProfitRowBytes;
        var lastCourse = (byte*)TavernStorage.ProfitRowsAt + TavernStorage.TrainingLast * TavernStorage.ProfitRowBytes;
        Check("native errand rows 89-95 remain byte-for-byte unrepurposed",
              firstCourse[0] == 7 && firstCourse[1] == 2 && firstCourse[2] == 0x5A && firstCourse[6] == 0x0B
              && lastCourse[0] == 8 && lastCourse[1] == 3 && lastCourse[2] == 0x60 && lastCourse[6] == 0x0E);
        var trainingRoster = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(2 * UnitTables.RosterSize);
        var trainee = trainingRoster + UnitTables.RosterSize;
        trainee[0x1E] = 96;
        trainee[0x1F] = 96;
        trainee[0] = 0x80;
        trainee[2] = 0x5B;
        trainee[4] = 0x80;
        trainee[6] = 0x71;
        var effects = TavernStorage.ApplyTraining(89, [(byte)1], trainingRoster)
                      + TavernStorage.ApplyTraining(90, [(byte)1], trainingRoster)
                      + TavernStorage.ApplyTraining(91, [(byte)1], trainingRoster)
                      + TavernStorage.ApplyTraining(92, [(byte)1], trainingRoster)
                      + TavernStorage.ApplyTraining(95, [(byte)1], trainingRoster);
        Check($"Special Training clamps Brave and Faith; Transitioning makes a male Bard a female Dancer (sprite set "
              + $"0x{trainee[0]:X2}, job 0x{trainee[2]:X2}, gender 0x{trainee[4]:X2}) and leaves +6 (0x{trainee[6]:X2})",
              effects == 5 && trainee[0x1E] == 94 && trainee[0x1F] == 94 && trainee[0] == 0x81 && trainee[2] == 0x5C
              && trainee[4] == 0x40 && trainee[6] == 0x71
              && TavernStorage.ApplyTraining(93, [(byte)1], trainingRoster) == 0);
        TavernStorage.ApplyTraining(95, [(byte)1], trainingRoster);
        Check($"and back: a male Bard again (0x{trainee[0]:X2}, 0x{trainee[2]:X2}, 0x{trainee[4]:X2})",
              trainee[0] == 0x80 && trainee[2] == 0x5B && trainee[4] == 0x80);
        trainee[0] = 0x82;
        trainee[4] = 0x20;
        Check("a monster isn't transitioned", TavernStorage.ApplyTraining(95, [(byte)1], trainingRoster) == 0 && trainee[0] == 0x82);
        System.Runtime.InteropServices.NativeMemory.Free(trainingRoster);
        var groups = stackalloc byte[2 * TavernStorage.RecordBytes];
        byte[] first = [1, 0x11, 5, 7, 75, 0, 3, 0, 0], second = [5, 0x80, 7, 7, 100, 0x80, 4, 0, 0];
        first.CopyTo(new Span<byte>(groups, 9));
        second.CopyTo(new Span<byte>(groups + 9, 9));
        var due1 = TavernStorage.Step(groups, 2);
        var due2 = TavernStorage.Step(groups, 2);
        Check($"a day on each group out, done when its days are up: days {groups[2]}, state {groups[0]}, came due {due1} then {due2}; " +
              $"the done one left as it was (days {groups[11]})",
              groups[2] == 7 && groups[0] == 5 && due1 == 0 && due2 == 1 && groups[11] == 7 && groups[9] == 5);
        var nibbles = stackalloc byte[TavernStorage.StatesBytes];
        var at = 0x3BE - TavernStorage.FirstState;
        nibbles[at >> 1] = 0x21;  // the contest byte: id 0x3BE in the low half (1), 0x3BF in the high (2)
        Check("a mission state's nibble: an even id in a byte's low half, as the game's address helper keeps them",
              (nibbles[at >> 1] >> (at & 1) * 4 & 0xF) == 1 && (nibbles[(at + 1) >> 1] >> ((at + 1) & 1) * 4 & 0xF) == 2);
        var jobMaps = new List<(uint Address, int Length, nint Place)>();
        RemixedEvents.MapTavernJobData((address, length, place) => jobMaps.Add((address, length, place)), game.Units.Jobs);
        (uint Address, int Length, nint Place) JobMap(int job, int part) => jobMaps[2 * job + part];
        var ordinary = JobMap(0x4A, 0);
        var ordinaryAfterGap = JobMap(0x4A, 1);
        var bard = JobMap(0x38, 0);
        var darkKnight = JobMap(0x5B, 0);
        Check("Training reads the live remaster job table, across its extra byte and the relocated Bard/Dark Knight rows",
              jobMaps.Count == 2 * RemixedEvents.Ps1JobCount
              && ordinary.Address == RemixedEvents.Ps1JobData + 0x4A * RemixedEvents.Ps1JobSize
              && ordinary.Length == RemixedEvents.Ps1JobExtraAt
              && ordinary.Place == (nint)(game.Units.Jobs + 0x4A * UnitTables.JobSize)
              && ordinaryAfterGap.Address == ordinary.Address + RemixedEvents.Ps1JobExtraAt
              && ordinaryAfterGap.Length == RemixedEvents.Ps1JobSize - RemixedEvents.Ps1JobExtraAt
              && ordinaryAfterGap.Place == ordinary.Place + RemixedEvents.Ps1JobExtraAt + 1
              && bard.Place == (nint)(game.Units.Jobs + 0x5B * UnitTables.JobSize)
              && darkKnight.Place == (nint)(game.Units.Jobs + 0xA0 * UnitTables.JobSize));
        return failures;
    }

    public static int SpellcastingOptions(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: spellcasting options, {name}{(ok ? "" : ": WRONG")}");
        }
        if (StatusTick.BlockAt == 0 || StartingMp.SiteAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        Check($"whatever the settings (a save's own can turn them on) the game's tick (0x{StatusTick.BlockAt - game.Image.ImageStart:X}) " +
              $"and HP and MP fill (0x{StartingMp.SiteAt - game.Image.ImageStart:X}) jump to their stubs, the switches off",
              StatusTick.Patched && StartingMp.Patched && *(byte*)StatusTick.BlockAt == 0xE9 && *(byte*)StartingMp.SiteAt == 0xE9
              && StatusTick.Settings == (false, false) && *StartingMp.Switch == 0);
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        *(ushort*)(unit + Battle.UnitMaxHp) = 200;
        *(ushort*)(unit + Battle.UnitMaxMp) = 60;
        (int Flags, int HpDamage, int HpHeal, int MpDamage, int MpHeal) Read() =>
            (result[Battle.ResultFlags], *(ushort*)(result + Battle.HpDamage), *(ushort*)(result + Battle.HpHeal),
             *(ushort*)(result + Battle.MpDamage), *(ushort*)(result + Battle.MpHeal));
        (int, int, int, int, int) Tick(byte statuses, bool mp, bool renew)
        {
            new Span<byte>(result, 0x40).Clear();
            unit[Battle.UnitStatuses + 3] = statuses;
            StatusTick.Apply(unit, result, mp, renew);
            return Read();
        }
        Check("vanilla: Poison 25 damage (0x80), Regen 25 healing (0x40), Poison over Regen, nothing otherwise",
              Tick(0x80, false, false) == (0x80, 25, 0, 0, 0) && Tick(0x40, false, false) == (0x40, 0, 25, 0, 0)
              && Tick(0xC0, false, false) == (0x80, 25, 0, 0, 0) && Tick(0, false, false) == (0, 0, 0, 0, 0));
        Check("on MP too: Poison takes 7 MP (0xA0), Regen gives 7 (0x50)",
              Tick(0x80, true, false) == (0xA0, 25, 0, 7, 0) && Tick(0x40, true, false) == (0x50, 0, 25, 0, 7));
        Check("Auto Renew: 7 MP back without Poison or Regen (0x10), nothing extra with either",
              Tick(0, false, true) == (0x10, 0, 0, 0, 7) && Tick(0x80, false, true) == (0x80, 25, 0, 0, 0)
              && Tick(0x40, true, true) == (0x50, 0, 25, 0, 7));
        *(ushort*)(unit + Battle.UnitMaxMp) = 3;
        Check("Max MP 3: Auto Renew still gives 1, Poison on MP takes 0", Tick(0, false, true) == (0x10, 0, 0, 0, 1)
              && Tick(0x80, true, false) == (0xA0, 25, 0, 0, 0));
        *(ushort*)(unit + Battle.UnitMaxMp) = 60;

        var harness = GameImage.AllocateCode(64);
        byte[] code =
        [
            0x53,                   // push rbx
            0x48, 0x89, 0xCB,       // mov rbx, rcx: the unit
            0x48, 0x83, 0xEC, 0x28, // sub rsp, 0x28: the stub then starts 16-aligned
            0xFF, 0xD2,             // call rdx: the stub, which comes back through the ret at +32
            0x48, 0x89, 0xC8,       // mov rax, rcx: what the stub left in rcx
            0x48, 0x83, 0xC4, 0x28, // add rsp, 0x28
            0x5B, 0xC3,             // pop rbx; ret
        ];
        code.CopyTo(new Span<byte>(harness, code.Length));
        harness[32] = 0xC3;
        var stub = GameImage.AllocateCode(StatusTick.StubSize);
        StatusTick.WriteStub(stub, StatusTick.TickAt, (nint)game.Battle.TargetResult, (nint)(harness + 32));
        var resultBefore = *game.Battle.TargetResult;
        var settingsBefore = StatusTick.Settings;
        *game.Battle.TargetResult = result;
        StatusTick.Set(true, true);
        new Span<byte>(result, 0x40).Clear();
        unit[Battle.UnitStatuses + 3] = 0x40;
        var left = ((delegate* unmanaged<byte*, byte*, nint>)harness)(unit, stub);
        Check("the tick's stub runs it (Regen on MP: 0x50) and leaves rcx on the result",
              Read() == (0x50, 0, 25, 0, 7) && left == (nint)result);
        StatusTick.Set(settingsBefore.Mp, settingsBefore.AutoRenew);
        *game.Battle.TargetResult = resultBefore;

        var mpStub = GameImage.AllocateCode(StartingMp.StubSize);
        var mpSwitch = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(1);
        StartingMp.WriteStub(mpStub, (nint)(harness + 32), mpSwitch);
        int Fill(int maxMp)
        {
            *(ushort*)(unit + Battle.UnitMaxMp) = (ushort)maxMp;
            *(ushort*)(unit + Battle.UnitMp) = 0;
            *(ushort*)(unit + Battle.UnitHp) = 123;
            ((delegate* unmanaged<byte*, byte*, nint>)harness)(unit, mpStub);
            return *(ushort*)(unit + Battle.UnitHp) == 123 ? *(ushort*)(unit + Battle.UnitMp) : -1;
        }
        Check("Starting MP's stub with its switch off: the game's full MP (60, 7), and HP is untouched",
              Fill(60) == 60 && Fill(7) == 7);
        *mpSwitch = 1;
        Check("with it on: Max MP 60 starts at 10, 7 at 7, 10 at 10, and HP is untouched",
              Fill(60) == 10 && Fill(7) == 7 && Fill(10) == 10);
        System.Runtime.InteropServices.NativeMemory.Free(mpSwitch);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        System.Runtime.InteropServices.NativeMemory.Free(result);

        // The installed stubs, and the options in effect turning them on and off (a save's own, SaveOptions).
        nint Target(nint jump) => *(byte*)jump == 0xE9 ? jump + 5 + *(int*)(jump + 1) : 0;
        nint Back(nint stubAt, int size)
        {
            var s = (byte*)stubAt;
            for (var k = 0; k + 14 <= size; k++)
                if (s[k] == 0xFF && s[k + 1] == 0x25 && *(int*)(s + k + 2) == 0)
                    return *(nint*)(s + k + 6);
            return 0;
        }
        var tickStub = Target(StatusTick.BlockAt);
        var fillStub = Target(StartingMp.SiteAt);
        Check("the tick jumps to its stub, which comes back after the block, and the MP fill jumps to its own, which " +
              "comes back after the MP",
              tickStub != 0 && fillStub != 0 && Back(tickStub, StatusTick.StubSize) == StatusTick.BlockAt + 0x34
              && Back(fillStub, StartingMp.StubSize) == StartingMp.SiteAt + 8);
        var optionsBefore = Options.Current;
        Options.Use(new Options
        {
            PoisonAndRegen = Options.PoisonRegenRule.HpAndMp, AutoRenew = true, StartingMp = Options.StartingMpRule.Ten,
        });
        var on = StatusTick.Settings == (true, true) && *StartingMp.Switch == 1;
        Options.Use(optionsBefore);
        Check("a save's options with the three on turn the tick's settings and Starting MP's switch on, and ReMixed's " +
              "defaults off again", on && StatusTick.Settings == (false, false) && *StartingMp.Switch == 0);
        StatusTick.Set(settingsBefore.Mp, settingsBefore.AutoRenew);
        return failures;
    }

    /// <summary>
    /// The poach roll's chance: the installed call goes to a stub, and a test copy of the stub, with a roll
    /// that returns its chance, gives 231 − 2 × the attacker's Brave (31 without an attacker).
    /// </summary>
    public static int PoachingOdds(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: poaching odds, {name}{(ok ? "" : ": WRONG")}");
        }
        if (PoachOdds.CallAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var call = (byte*)PoachOdds.CallAt;
        Check($"the apply step's roll (0x{PoachOdds.CallAt - game.Image.ImageStart:X}) goes to a stub, not straight to " +
              $"0x{PoachOdds.Roll - game.Image.ImageStart:X}",
              call[0] == 0xE8 && (nint)(call + 5 + *(int*)(call + 1)) != PoachOdds.Roll);
        var harness = GameImage.AllocateCode(PoachOdds.StubSize + 16);
        byte[] echo = [0x89, 0xD0, 0xC3]; // mov eax, edx; ret
        echo.CopyTo(new Span<byte>(harness + PoachOdds.StubSize, echo.Length));
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var attacker = (nint*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
        var poachSwitch = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(1);
        *poachSwitch = 1;
        PoachOdds.WriteStub(harness, (nint)attacker, (nint)(harness + PoachOdds.StubSize), poachSwitch);
        var run = (delegate* unmanaged<int, int, int>)harness;
        int Chance(int brave)
        {
            unit[Battle.UnitBrave] = (byte)brave;
            *attacker = (nint)unit;
            return run(256, 31);
        }
        Check("Brave 97 gives 37, 50 gives 131, 0 gives 231, 100 vanilla's 31",
              Chance(97) == 37 && Chance(50) == 131 && Chance(0) == 231 && Chance(100) == 31 && PoachOdds.Chance(70) == 91);
        *poachSwitch = 0;
        Check("with its switch off (a save's vanilla odds): the game's 31 whatever the Brave", Chance(50) == 31 && Chance(0) == 31);
        *poachSwitch = 1;
        *attacker = 0;
        Check("no attacker: vanilla's 31", run(256, 31) == 31);
        Check("the installed switch follows the options in effect: on with ReMixed's default", *PoachOdds.Switch == 1);
        System.Runtime.InteropServices.NativeMemory.Free(poachSwitch);
        System.Runtime.InteropServices.NativeMemory.Free(attacker);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        return failures;
    }

    /// <summary>
    /// No minimum weapon range: both sites patched, then the game's Arc builder on a flat 10 × 10 map for a
    /// unit at (5, 5) with range 5 marks the tiles 1 and 2 away, which vanilla skips.
    /// </summary>
    public static int WeaponRanges(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: weapon range, {name}{(ok ? "" : ": WRONG")}");
        }
        if (WeaponRange.ArcBuilderAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        Check($"the Arc builder (0x{WeaponRange.ArcBuilderAt - game.Image.ImageStart:X}) keeps near tiles, and the Direct " +
              "branch no longer cuts its hole", WeaponRange.Patched);
        const int size = 0xA00;
        var map = WeaponRange.RangeMap;
        var before = new byte[size];
        new Span<byte>(map, size).CopyTo(before);
        new Span<byte>(map, size).Clear();
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        unit[Battle.UnitX] = 5;
        unit[Battle.UnitY] = 5;
        game.Map.SetSize(10, 10);
        ((delegate* unmanaged<byte*, byte, void>)WeaponRange.ArcBuilderAt)(unit, 5);
        bool Marked(int x, int y) => map[5 * (y * 10 + x)] != 0;
        Check("Arc, range 5: the tiles 1 and 2 away are in range, 5 away too, 6 away not",
              Marked(5, 6) && Marked(6, 5) && Marked(5, 3) && Marked(4, 4) && Marked(5, 0) && !Marked(0, 0) && !Marked(9, 7));
        game.Map.SetSize(0, 0);
        before.CopyTo(new Span<byte>(map, size));
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        return failures;
    }

    /// <summary>
    /// ReMixed's knockback, the landing checks on their own and then the hooked routine on a 10 × 20 map
    /// with the game's own landing-level function: the attacker at (2, 3) knocks the target at (3, 3) to (4, 3).
    /// </summary>
    public static int KnockbackRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: knockback, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Knockback.RoutineAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var tile = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
        bool Land(byte surface, byte depth, int y, bool vanilla, byte movement = 0, byte flags6 = 0, byte blocked = 0)
        {
            tile[0] = surface;
            tile[3] = depth;
            tile[6] = blocked;
            unit[Battle.UnitMovement + 2] = movement;
            unit[6] = flags6;
            return Knockback.CanLand(unit, tile, y, vanilla);
        }
        Check("plain ground and a slope of 2 take it; a slope of 3, a blocked tile and surface 0x1C don't",
              Land(0, 0, 3, false) && Land(0, 2, 3, false) && !Land(0, 3, 3, false) && !Land(0, 0, 3, false, blocked: 2)
              && !Land(0x1C, 0, 3, false));
        Check("water takes it, but not with the vanilla setting, nor on rows 16–31 (ReMixed's quirk)",
              Land(0, 0x20, 3, false) && Land(0, 0x60, 15, false) && !Land(0, 0x20, 3, true) && !Land(0, 0x20, 16, false)
              && !Land(0, 0x20, 31, false) && Land(0, 0x20, 32, false));
        Check("lava takes it with either setting, but not a frozen body (+6 & 4) without Waterwalking or Levitate",
              Land(0x12, 0, 3, false) && Land(0x12, 0, 3, true) && !Land(0x12, 0, 3, false, flags6: 4)
              && Land(0x12, 0, 3, false, 0x80, 4) && Land(0x12, 0, 3, false, 0x08, 4));
        Check("only the last strike: 1 of 1 and 2 of 2, not 1 of 2",
              Knockback.LastStrike(1, 0) && Knockback.LastStrike(2, 1) && !Knockback.LastStrike(2, 0));
        System.Runtime.InteropServices.NativeMemory.Free(tile);
        System.Runtime.InteropServices.NativeMemory.Free(unit);

        var b = game.Battle;
        var r = game.Routines;
        var units = game.TurnCode.Units;
        var unitsBefore = new byte[Battle.UnitCount * Battle.UnitSize];
        new Span<byte>(units, unitsBefore.Length).CopyTo(unitsBefore);
        for (var k = 0; k < Battle.UnitCount; k++)
            units[k * Battle.UnitSize + 1] = 0xFF; // no one on the landing tiles
        var attacker = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var result = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var attackerBefore = *b.Attacker;
        var targetBefore = *r.Target;
        var resultBefore = *b.TargetResult;
        var single = Knockback.SingleTarget;
        var strikes = Knockback.Strikes;
        var landing = Knockback.LandingAt;
        var saved = (single[0], single[1], strikes[0], strikes[1], landing[0], landing[1], landing[2]);
        var vanillaBefore = Knockback.Vanilla;
        *b.Attacker = attacker;
        *r.Target = target;
        *b.TargetResult = result;
        game.Map.SetSize(10, 20);
        attacker[Battle.UnitX] = 2;
        attacker[Battle.UnitY] = 3;
        var routine = (delegate* unmanaged<void>)Knockback.RoutineAt;
        bool Knocked(byte targetFlags, int strike = 0, int count = 1, bool vanilla = false, byte depth = 0, int y = 3)
        {
            target[Battle.UnitX] = 3;
            target[Battle.UnitY] = (byte)y;
            attacker[Battle.UnitY] = (byte)y;
            target[Battle.UnitCurrentTeam] = targetFlags;
            game.Map.TileAt(4, y, 0)[3] = depth;
            *single = 1;
            strikes[0] = (byte)count;
            strikes[1] = (byte)strike;
            *(ushort*)(result + Battle.ResultFlags2) = 0;
            landing[0] = landing[1] = landing[2] = 0xEE;
            Knockback.Vanilla = vanilla;
            routine();
            game.Map.TileAt(4, y, 0)[3] = 0;
            return (*(ushort*)(result + Battle.ResultFlags2) & 0x4000) != 0;
        }
        var plain = Knocked(0);
        Check($"a plain hit lands at (4, 3) level 0, fall type 0x{single[1]:X2}",
              plain && landing[0] == 4 && landing[1] == 3 && landing[2] == 0 && single[1] is 0x81 or 0x82);
        Check("an Immortal target (+5 & 4) and the party leader (+5 & 3) are knocked back, but not with the vanilla setting",
              Knocked(0x04) && Knocked(0x03) && !Knocked(0x04, vanilla: true) && !Knocked(0x03, vanilla: true)
              && Knocked(0x01, vanilla: true));
        Check("a dual-wielder's first strike doesn't knock back, its second does",
              !Knocked(0, strike: 0, count: 2) && Knocked(0, strike: 1, count: 2));
        Check("into water on row 3, not on row 17, and not with the vanilla setting",
              Knocked(0, depth: 0x20) && !Knocked(0, depth: 0x20, y: 17) && !Knocked(0, depth: 0x20, vanilla: true));
        *single = 2;
        routine();
        Check("nothing for an action with more than one target", (*(ushort*)(result + Battle.ResultFlags2) & 0x4000) == 0);
        // Hook Shot's pull: a tile toward the attacker along the axis it's further on, on any strike.
        string Pulled(int tx, int ty, int strike = 0, int count = 2)
        {
            attacker[Battle.UnitX] = 2;
            attacker[Battle.UnitY] = 3;
            target[Battle.UnitX] = (byte)tx;
            target[Battle.UnitY] = (byte)ty;
            target[Battle.UnitCurrentTeam] = 0;
            *single = 1;
            strikes[0] = (byte)count;
            strikes[1] = (byte)strike;
            *(ushort*)(result + Battle.ResultFlags2) = 0;
            landing[0] = landing[1] = landing[2] = 0xEE;
            Knockback.Vanilla = false;
            return Knockback.Pull() ? $"({landing[0]}, {landing[1]})" : "stays";
        }
        var along = Pulled(5, 3);
        var down = Pulled(4, 6);
        var adjacent = Pulled(3, 3);
        Check($"Hook Shot pulls a target at (5, 3) to {along} and one at (4, 6) to {down} (attacker at (2, 3), even on a first strike); an adjacent one {adjacent}",
              along == "(4, 3)" && down == "(4, 5)" && adjacent == "stays");

        game.Map.SetSize(0, 0);
        Knockback.Vanilla = vanillaBefore;
        (single[0], single[1], strikes[0], strikes[1], landing[0], landing[1], landing[2]) = saved;
        *b.Attacker = attackerBefore;
        *r.Target = targetBefore;
        *b.TargetResult = resultBefore;
        unitsBefore.CopyTo(new Span<byte>(units, unitsBefore.Length));
        System.Runtime.InteropServices.NativeMemory.Free(attacker);
        System.Runtime.InteropServices.NativeMemory.Free(target);
        System.Runtime.InteropServices.NativeMemory.Free(result);
        return failures;
    }

    /// <summary>
    /// The Brave rule, through the hooked attacker's status step (a leaf): XA less (50 − Brave)% for a
    /// target under 50, before the game's own Berserk and Frog.
    /// </summary>
    public static int BraveRule(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Brave rule, {name}{(ok ? "" : ": WRONG")}");
        }
        if (BraveDamage.StepAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var b = game.Battle;
        var r = game.Routines;
        var attacker = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var target = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var attackerBefore = *b.Attacker;
        var targetBefore = *r.Target;
        var xaBefore = *b.XA;
        *b.Attacker = attacker;
        *r.Target = target;
        var step = (delegate* unmanaged<void>)BraveDamage.StepAt;
        int Xa(int brave, byte attackerStatus)
        {
            target[Battle.UnitBrave] = (byte)brave;
            attacker[Battle.UnitStatuses + 2] = attackerStatus;
            *b.XA = 100;
            step();
            return *b.XA;
        }
        Check("XA 100 against Brave 30 is 80, Brave 0 is 50, Brave 50 and 97 stay 100",
              Xa(30, 0) == 80 && Xa(0, 0) == 50 && Xa(50, 0) == 100 && Xa(97, 0) == 100);
        Check("then the game's Berserk: 120 against Brave 30, and Frog still makes it 1",
              Xa(30, 0x08) == 120 && Xa(30, 0x02) == 1);
        Check("the arithmetic rounds down the part taken off: 7 against Brave 45 is 7", BraveDamage.Reduced(7, 45) == 7
              && BraveDamage.Reduced(33, 10) == 20);
        *b.Attacker = attackerBefore;
        *r.Target = targetBefore;
        *b.XA = xaBefore;
        System.Runtime.InteropServices.NativeMemory.Free(attacker);
        System.Runtime.InteropServices.NativeMemory.Free(target);
        return failures;
    }

    /// <summary>
    /// The level options: ReMixed's rules for a record's level byte, the level read's stub through a
    /// harness (rbx the record, rdi the unit, sil 99, its three exits pointed at thunks), and P's stub
    /// with a dummy callee; then the patched sites.
    /// </summary>
    public static int LevelOptions(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: level options, {name}{(ok ? "" : ": WRONG")}");
        }
        if (LevelScaling.LevelAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var normal = new Options();
        var scale = new Options { FixedLevels = Options.FixedLevelRule.IfPartyLevelIsHigher };
        var nightmare = new Options { EnemyLevelBoost = Options.LevelBoostRule.Nightmare };
        var noFuture = new Options { EnemyLevelBoost = Options.LevelBoostRule.NoFuture };
        Check("by default every level byte stays (5, 0xFE, 103)",
              LevelScaling.Resolve(5, 0x10, 40, normal) == 5 && LevelScaling.Resolve(0xFE, 0x10, 40, normal) == 0xFE
              && LevelScaling.Resolve(103, 0x10, 40, normal) == 103);
        Check("fixed or minimum levels, party level 40: an enemy's 20 scales, 35 stays, 0 stays",
              LevelScaling.Resolve(20, 0x10, 40, scale) == 0xFE && LevelScaling.Resolve(35, 0x10, 40, scale) == 35
              && LevelScaling.Resolve(0, 0x10, 40, scale) == 0);
        Check("Nightmare: an enemy's fixed 20 is 69, 60 is 99, a scaling level stays",
              LevelScaling.Resolve(20, 0x10, 40, nightmare) == 69 && LevelScaling.Resolve(60, 0x10, 40, nightmare) == 99
              && LevelScaling.Resolve(0xFE, 0x10, 40, nightmare) == 0xFE);
        Check("No Future: an enemy's fixed 1 is 99", LevelScaling.Resolve(1, 0x10, 40, noFuture) == 99);
        var formation = stackalloc nint[3];
        formation[0] = 0x2FC870; formation[1] = 2; formation[2] = 1;
        var formed = LevelScaling.FromFormation(formation);
        formation[1] = 0;
        var resumed = !LevelScaling.FromFormation(formation);
        formation[2] = 0;
        Check("the re-run after the formation: P set with the formation screen up redoes, P set without it (a battle "
              + "resumed from a save) keeps the units, P never set redoes",
              formed && resumed && LevelScaling.FromFormation(formation) && LevelScaling.FromFormation(null));
        Check("the player's team keeps its levels", LevelScaling.Resolve(20, 0, 40, scale) == 20 && LevelScaling.Resolve(20, 0, 40, noFuture) == 20);
        LevelScaling.BattleEntry = () => 0x183;
        var opening = LevelScaling.OptionsSkipped();
        LevelScaling.BattleEntry = () => 0;
        Check($"the opening battle (entry 0x183) skips the options ({opening}), another doesn't ({LevelScaling.OptionsSkipped()})",
              opening && !LevelScaling.OptionsSkipped());

        var code = GameImage.AllocateCode(256);
        var harness = new List<byte>
        {
            0x53, 0x57, 0x56,                   // push rbx, rdi, rsi
            0x48, 0x83, 0xEC, 0x28,             // sub rsp, 0x28: the stub then starts 16-aligned
            0x48, 0x89, 0xCB, 0x48, 0x89, 0xD7, // mov rbx, rcx (the record); mov rdi, rdx (the unit)
            0x40, 0xB6, 0x63, 0x41, 0xB2, 0x77, // mov sil, 99; mov r10b, 0x77
            0x41, 0xFF, 0xD0,                   // call r8: the stub
            0x48, 0x83, 0xC4, 0x28, 0x5E, 0x5F, 0x5B, 0xC3, // add rsp, 0x28; pop rsi, rdi, rbx; ret
        };
        harness.ToArray().CopyTo(new Span<byte>(code, harness.Count));
        // The exits: eax = way << 8 | r10b.
        var exits = new nint[3];
        for (var k = 0; k < 3; k++)
        {
            var at = code + 64 + 16 * k;
            byte[] thunk = [0x41, 0x0F, 0xB6, 0xC2, 0x80, 0xCC, (byte)(k + 1), 0xC3]; // movzx eax, r10b; or ah, way; ret
            thunk.CopyTo(new Span<byte>(at, thunk.Length));
            exits[k] = (nint)at;
        }
        var stub = GameImage.AllocateCode(LevelScaling.StubSize);
        LevelScaling.WriteLevelStub(stub, exits[0], exits[1], exits[2]);
        var run = (delegate* unmanaged<byte*, byte*, byte*, int>)code;
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var options = Options.Current;
        var partyBefore = *LevelScaling.PartyLevel;
        *LevelScaling.PartyLevel = 40;
        string Read(byte level, byte team, Options set)
        {
            Options.Current = set;
            record[3] = level;
            unit[Battle.UnitCurrentTeam] = team;
            var r = run(record, unit, stub);
            return $"{(r >> 8) switch { 1 => "scales", 2 => "fixed", 3 => "party+", _ => "?" }} {r & 0xFF}";
        }
        Check("through the stub, by default: 0xFE scales, 5 is fixed, 103 is the party's plus 3",
              Read(0xFE, 0x10, normal) == "scales 254" && Read(5, 0x10, normal) == "fixed 5" && Read(103, 0x10, normal) == "party+ 103");
        Check("with the options: an enemy's 20 scales, or is 69 on Nightmare", Read(20, 0x10, scale) == "scales 254" && Read(20, 0x10, nightmare) == "fixed 69");
        Options.Current = options;

        // P's stub: max(al, floor), stored; a dummy callee, and back to a thunk that returns P.
        var partyCode = GameImage.AllocateCode(128);
        byte[] partyHarness = [0x89, 0xC8, 0x48, 0x83, 0xEC, 0x28, 0xFF, 0xD2, 0x48, 0x83, 0xC4, 0x28, 0xC3]; // mov eax, ecx; sub rsp, 0x28; call rdx; add rsp, 0x28; ret
        partyHarness.CopyTo(new Span<byte>(partyCode, partyHarness.Length));
        var callee = partyCode + 64;
        byte[] calleeCode = [0x31, 0xC0, 0xC3]; // xor eax, eax; ret
        calleeCode.CopyTo(new Span<byte>(callee, calleeCode.Length));
        var back = partyCode + 80;
        byte[] backCode = [0xC3]; // ret
        backCode.CopyTo(new Span<byte>(back, backCode.Length));
        var floor = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(1);
        var partyStub = GameImage.AllocateCode(LevelScaling.StubSize);
        LevelScaling.WritePartyStub(partyStub, floor, LevelScaling.PartyLevel, (nint)callee, (nint)back);
        var party = (delegate* unmanaged<int, byte*, int>)partyCode;
        int Party(int roster, byte min)
        {
            *floor = min;
            party(roster, partyStub);
            return *LevelScaling.PartyLevel;
        }
        Check("the party level: the roster's 30, or at least 57 on Nightmare and 114 on No Future, and 70 stays 70",
              Party(30, 0) == 30 && Party(30, 57) == 57 && Party(30, 114) == 114 && Party(70, 57) == 70);
        Check("the default floor is 0", *LevelScaling.Floor == 0);
        // The formation snapshot: a made-up task table with task 6's function and state, then none at all.
        var table = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x3100);
        *(nint*)(table + 0x3088) = 0x2FC870;
        *(nint*)(table + 0x3090) = 1;
        var tasks = (nint*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(8);
        var snapshot = (nint*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(24);
        var snapStub = GameImage.AllocateCode(LevelScaling.StubSize);
        LevelScaling.WritePartyStub(snapStub, floor, LevelScaling.PartyLevel, (nint)callee, (nint)back, (nint)tasks, snapshot);
        *floor = 0;
        *tasks = (nint)table;
        party(30, snapStub);
        var seen = $"P {*LevelScaling.PartyLevel}, task 0x{snapshot[0]:X} state {snapshot[1]}, count {snapshot[2]}";
        *tasks = 0;
        party(40, snapStub);
        var none = $"P {*LevelScaling.PartyLevel}, count {snapshot[2]}";
        Check($"the formation snapshot: {seen}; without a task table {none}",
              seen == "P 30, task 0x2FC870 state 1, count 1" && none == "P 40, count 1");
        System.Runtime.InteropServices.NativeMemory.Free(table);
        System.Runtime.InteropServices.NativeMemory.Free(tasks);
        System.Runtime.InteropServices.NativeMemory.Free(snapshot);
        // P's roster loop: push r14; mov rax, rcx (the slot's +1); mov r8, rdx (its +0x1D); mov r14d, 0xFF; the copy |
        // back: mov eax, ecx (the level) | next: mov eax, -1; then pop r14; ret.
        var rosterHarness = Harness(LevelScaling.RosterAt, [0x41, 0x56, 0x48, 0x89, 0xC8, 0x49, 0x89, 0xD0, 0x41, 0xBE, 0xFF, 0, 0, 0],
                                    [[0x8B, 0xC1, 0x41, 0x5E, 0xC3], [0xB8, 0xFF, 0xFF, 0xFF, 0xFF, 0x41, 0x5E, 0xC3]],
                                    (copy, exits) => LevelScaling.WriteRosterStub((byte*)copy, exits[0], exits[1]));
        var slot = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x258);
        int Counted(byte id, byte errand)
        {
            slot[1] = id; slot[LevelScaling.RosterLevel] = 40; slot[LevelScaling.Errand] = errand;
            return ((delegate* unmanaged<byte*, byte*, int>)rosterHarness)(slot + 1, slot + LevelScaling.RosterLevel);
        }
        var home = Counted(3, 0);
        var away = Counted(3, 0x01);
        var otherBits = Counted(3, 0x10);
        var empty = Counted(0xFF, 0);
        System.Runtime.InteropServices.NativeMemory.Free(slot);
        Check($"the party level's roster: a unit at home counts ({home}), one away doesn't ({away}), the errand byte's high bits don't matter ({otherBits}), an empty slot doesn't ({empty}); the loop jumps to the stub ({((byte*)LevelScaling.RosterAt)[0] == 0xE9})",
              home == 40 && away == -1 && otherBits == 40 && empty == -1 && ((byte*)LevelScaling.RosterAt)[0] == 0xE9);
        *LevelScaling.PartyLevel = partyBefore;
        System.Runtime.InteropServices.NativeMemory.Free(record);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        System.Runtime.InteropServices.NativeMemory.Free(floor);

        static bool JumpsTo(nint at, nint stub) => *(ushort*)at == 0x25FF && *(int*)(at + 2) == 0 && *(nint*)(at + 6) != 0;
        Check($"the level read (0x{LevelScaling.LevelAt - game.Image.ImageStart:X}) and the party level's store " +
              $"(0x{LevelScaling.PartyAt - game.Image.ImageStart:X}) jump to their stubs",
              JumpsTo(LevelScaling.LevelAt, 0) && JumpsTo(LevelScaling.PartyAt, 0));
        return failures;
    }

    /// <summary>
    /// Like Reloaded-II's launcher when it looks for a configurator: Reloaded.Mod.Interfaces is its own
    /// (shared), and there's no Reloaded.Hooks.
    /// </summary>
    private sealed class LauncherContext() : System.Runtime.Loader.AssemblyLoadContext("launcher", isCollectible: true)
    {
        protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName name)
        {
            if (name.Name == "Reloaded.Mod.Interfaces")
                return typeof(Reloaded.Mod.Interfaces.IConfiguratorV1).Assembly;
            if (name.Name?.StartsWith("Reloaded.Hooks") == true)
                throw new FileNotFoundException($"the launcher has no {name.Name}");
            return null;
        }
    }

    /// <summary>
    /// The death counter, through the game's own tick for a dead unit (0x30F9A0, with both patches
    /// in it): a counter counts down and a frozen one stays; at 0, by default, a crystal or treasure as
    /// in vanilla; with Revive after battle, a player unit (Ramza too) freezes; and General Deaths can
    /// force the body. Each of those ticks counts a turn for First Move.
    /// </summary>
    public static int DeathCounters(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: death counter, {name}{(ok ? "" : ": WRONG")}");
        }
        if (DeathCounter.TickAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var tick = (delegate* unmanaged<nint, byte*, int>)DeathCounter.TickAt;
        var unit = game.TurnCode.Units + 9 * Battle.UnitSize;
        var saved = new Span<byte>(unit, Battle.UnitSize).ToArray();
        var simulating = *game.Routines.Simulating;
        *game.Routines.Simulating = 0;
        var options = Options.Current;
        // The Rendezvous flag (0xD9) from a made-up variable, as the game's getter needs the running game.
        var read = DeathCounter.ReadVariable;
        var rendezvous = 0;
        DeathCounter.ReadVariable = id => id == DeathCounter.RendezvousFlag ? rendezvous : 0;
        void Dead(byte sprite, byte team, byte counter)
        {
            new Span<byte>(unit, Battle.UnitSize).Clear();
            unit[0] = sprite;
            unit[1] = 0x20;
            unit[Battle.UnitCurrentTeam] = team;
            unit[6] = 0x80; // a man
            unit[7] = counter;
            unit[Battle.UnitStatuses] = 0x20; // Dead
            unit[Battle.UnitIndex] = 9;
            *(ushort*)(unit + Battle.UnitMaxHp) = 100;
        }
        var result = unit + Battle.UnitResult;
        string Tick()
        {
            var r = tick(0, unit);
            return $"{r}, counter {unit[7]}, innate {unit[0x57]:X2}, +6 {unit[6]:X2}, crystal {result[0x1D]:X2}, treasure {result[0x1E]:X2}";
        }
        bool Lost(string got) => got.StartsWith("1, counter 0, innate 00, +6 80") && (result[0x1D] == 0x40 ^ result[0x1E] == 1);
        var turns = Movement.TurnsEndedBy(9);
        Options.Current = new Options();
        Dead(0x80, 0, 3);
        Check("a dead ally's counter counts down, 3 to 2", Tick() == "0, counter 2, innate 00, +6 80, crystal 00, treasure 00");
        Dead(0x80, 0, 0xFF);
        Check("a frozen corpse's counter stays 0xFF", Tick() == "0, counter 255, innate 00, +6 80, crystal 00, treasure 00");
        Dead(0x80, 0, 0);
        var ally = Tick();
        Check($"by default an ally whose counter runs out is lost, a crystal or a treasure ({ally})", Lost(ally));
        Options.Current = new Options { PlayerDeaths = Options.PlayerDeathRule.ReviveAfterBattle };
        Dead(0x80, 0, 0);
        Check("with Revive after battle it freezes instead: counter 0xFF, innate Dead, +6 | 4, no body",
              Tick() == "0, counter 255, innate 20, +6 84, crystal 00, treasure 00");
        Dead(1, 0, 0);
        Check("so does Ramza (sprite set 1)", Tick() == "0, counter 255, innate 20, +6 84, crystal 00, treasure 00");
        Dead(0x80, 0x10, 0);
        var enemy = Tick();
        Check($"an enemy is still lost ({enemy})", Lost(enemy));
        Options.Current = new Options { Bodies = Options.BodyRule.Crystal };
        Dead(0x80, 0x10, 0);
        Check("General Deaths on crystal: always a crystal", Tick() == "1, counter 0, innate 00, +6 80, crystal 40, treasure 00");
        Options.Current = new Options { Bodies = Options.BodyRule.TreasureBox };
        Dead(0x80, 0, 0);
        Check("on treasure box: always a treasure", Tick() == "1, counter 0, innate 00, +6 80, crystal 00, treasure 01");
        Check("each of those eight ticks counted a turn for First Move", Movement.TurnsEndedBy(9) - turns == 8);
        // A Rendezvous battle (ReMixed's flag 0xD9, set by a mission's event): an ally freezes whatever Player Deaths
        // says; an enemy is still lost.
        rendezvous = 1;
        Options.Current = new Options();
        Dead(0x80, 0, 0);
        var rendezvousAlly = Tick();
        Dead(0x80, 0x10, 0);
        var rendezvousEnemy = Tick();
        DeathCounter.ReadVariable = read;
        Check($"in a Rendezvous an ally freezes with Player Deaths at its default ({rendezvousAlly}), an enemy is lost "
              + $"({rendezvousEnemy})", rendezvousAlly == "0, counter 255, innate 20, +6 84, crystal 00, treasure 00"
              && Lost(rendezvousEnemy));
        Options.Current = options;
        *game.Routines.Simulating = simulating;
        saved.CopyTo(new Span<byte>(unit, Battle.UnitSize));
        Movement.ResetTurns();
        return failures;
    }

    /// <summary>
    /// ReMixed's Jump: the game's table holds ReMixed's values, the menu lists only Jump itself, and
    /// the range stub, through a harness that lays out the Jump range's frame (the battle index in
    /// its [rsp+0x48], the list in its [rsp+0xC0], the count in eax), gives ReMixed's sums.
    /// </summary>
    public static int JumpRanges(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Jump rework, {name}{(ok ? "" : ": WRONG")}");
        }
        if (JumpRework.LoopAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var table = JumpRework.Table;
        Check("the game's Jump table: Jump (3, 99), each Jump Range +1 (1, 99)",
              table[0] == 3 && table[1] == 99 && Enumerable.Range(1, 5).All(k => table[2 * k] == 1 && table[2 * k + 1] == 99));
        // With Dragon Tech the bound moves into its AI stub, which read it from the patched code.
        Check("the menu lists a Jump skillset's 0x18A alone (cmp ebp, 0x18B)",
              DragonTech.AiAt != 0 ? DragonTech.AiBound == 0x18B : *(byte*)JumpRework.MenuBound == 0x8B);

        var code = GameImage.AllocateCode(160);
        var stub = GameImage.AllocateCode(JumpRework.StubSize);
        var exit = code + 128;
        byte[] exitCode =
        [
            0x0F, 0xB6, 0xC3, 0x40, 0x0F, 0xB6, 0xCF, 0xC1, 0xE1, 0x08, 0x09, 0xC8, // movzx eax, bl; movzx ecx, dil; shl ecx, 8; or eax, ecx
            0x41, 0x0F, 0xB6, 0xC9, 0xC1, 0xE1, 0x10, 0x09, 0xC8, 0xC3,             // movzx ecx, r9b; shl ecx, 16; or eax, ecx; ret
        ];
        exitCode.CopyTo(new Span<byte>(exit, exitCode.Length));
        JumpRework.WriteStub(stub, (nint)exit);
        var harness = new List<byte>
        {
            0x53, 0x57, 0x56,                               // push rbx, rdi, rsi
            0x48, 0x81, 0xEC, 0xF8, 0x00, 0x00, 0x00,       // sub rsp, 0xF8: the stub then starts 16-aligned
            0x88, 0x4C, 0x24, 0x40,                         // mov [rsp+0x40], cl: the stub's [rsp+0x48], the battle index
            0x48, 0x89, 0xD6,                               // mov rsi, rdx: the list
            0x48, 0x8D, 0xBC, 0x24, 0xB8, 0x00, 0x00, 0x00, // lea rdi, [rsp+0xB8]: its [rsp+0xC0]
            0xB9, 0x14, 0x00, 0x00, 0x00, 0x66, 0xF3, 0xA5, // mov ecx, 20; rep movsw
            0x44, 0x89, 0xC0,                               // mov eax, r8d: the count
            0xB3, 0x77, 0x40, 0xB7, 0x66,                   // mov bl, 0x77; mov dil, 0x66: the stub must set both
            0x49, 0xC7, 0xC1, 0xFF, 0xFF, 0xFF, 0xFF,       // mov r9, -1: and r9
            0x49, 0xBA,                                     // mov r10, the stub
        };
        harness.AddRange(BitConverter.GetBytes((ulong)stub));
        harness.AddRange([0x41, 0xFF, 0xD2, 0x48, 0x81, 0xC4, 0xF8, 0x00, 0x00, 0x00, 0x5E, 0x5F, 0x5B, 0xC3]); // call r10; add rsp; pop; ret
        harness.ToArray().CopyTo(new Span<byte>(code, harness.Count));
        var run = (delegate* unmanaged<int, ushort*, int, int>)code;

        var units = game.TurnCode.Units;
        var lancer = units + 4 * Battle.UnitSize;
        var mime = units + 5 * Battle.UnitSize;
        var savedLancer = lancer[3];
        var savedMime = mime[3];
        lancer[3] = 0x51;
        mime[3] = 0x5D;
        var list = stackalloc ushort[20];
        string Jump(int index, params ushort[] abilities)
        {
            for (var k = 0; k < 20; k++)
                list[k] = k < abilities.Length ? abilities[k] : (ushort)0;
            var r = run(index, list, abilities.Length);
            return $"range {r & 0xFF}, height {(r >> 8) & 0xFF}, count {(r >> 16) & 0xFF}";
        }
        Check("Jump alone: range 3, height 54 (0x70 + 198 in a byte)", Jump(4, 0x18A) == "range 3, height 54, count 1");
        Check("with two +1s: range 5, height 194", Jump(4, 0x18A, 0x18B, 0x18C) == "range 5, height 194, count 3");
        Check("all six: range 8, height 20", Jump(4, 0x18A, 0x18B, 0x18C, 0x18D, 0x18E, 0x18F) == "range 8, height 20, count 6");
        Check("a Mime starts from 8: range 11", Jump(5, 0x18A) == "range 11, height 54, count 1");
        Check("other abilities in the list don't count", Jump(4, 0x18A, 0x190, 0x151, 0x144) == "range 3, height 54, count 4");
        Check("nothing listed: range 0, height 0x70", Jump(4) == "range 0, height 112, count 0");
        lancer[3] = savedLancer;
        mime[3] = savedMime;

        var at = (byte*)JumpRework.LoopAt;
        var target = (byte*)*(nint*)(at + 6);
        var exits = -1;
        for (var k = 0; k < JumpRework.StubSize - 14 && exits < 0; k++)
            if (target[k] == 0xFF && target[k + 1] == 0x25 && *(int*)(target + k + 2) == 0 && *(nint*)(target + k + 6) == JumpRework.Exit)
                exits = k;
        Check($"the Jump range's loop (0x{JumpRework.LoopAt - game.Image.ImageStart:X}) jumps to its stub, which goes on to 0x{JumpRework.Exit - game.Image.ImageStart:X}",
              at[0] == 0xFF && at[1] == 0x25 && *(int*)(at + 2) == 0 && exits > 0);
        return failures;
    }

    /// <summary>
    /// Dragon Tech: the command after Jump (through the command list's hook), the list builder's learned test for
    /// skillset 0x64 (its patched bytes, run from a copy), Jump's list without ordinary abilities (through the menu's
    /// weapon stub), and the AI's type 9 entries (its stub, run against landing pads).
    /// </summary>
    public static int DragonTechRules(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Dragon Tech, {name}{(ok ? "" : ": WRONG")}");
        }
        if (DragonTech.AiAt == 0)
        {
            log.Info("self-test: Dragon Tech, not in this build's rules: installed on this copy");
            try
            {
                DragonTech.Install(game);
            }
            catch (Exception e)
            {
                Check($"can't be installed: {e.Message}", false);
                return failures;
            }
        }
        var image = game.Image.ImageStart;

        // The command list.
        var realUsable = TwoSpells.Usable;
        TwoSpells.Usable = &FakeUsable;
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x200);
        var list = stackalloc byte[8];
        var table = TwoSpells.TypeTable;
        string Run(bool innate, int primary, int secondary, params byte[] before)
        {
            unit[Battle.UnitSupports + 1] = (byte)(innate ? 0x20 : 0);
            unit[0x12] = (byte)primary;
            unit[Battle.UnitSecondary] = (byte)secondary;
            for (var k = 0; k < 8; k++)
                list[k] = k < before.Length ? before[k] : (byte)0xFF;
            for (var k = 0; k < before.Length && k < 6; k++) // the game's own types, which a list left alone keeps
            {
                TwoSpells.Types[k] = (byte)(table[before[k]] & 0x0F);
                TwoSpells.Unusable[k] = 0;
            }
            TwoSpells.Insert(3, list, unit);
            var entries = new List<string>();
            for (var k = 0; k < 7 && list[k] != 0xFF; k++)
            {
                var typeOk = TwoSpells.Types[k] == (table[list[k]] & 0x0F);
                entries.Add($"{list[k]:X2}{(TwoSpells.Unusable[k] != 0 ? "x" : "")}{(typeOk ? "" : "?")}");
            }
            return string.Join(" ", entries);
        }
        var lancer = Run(false, 0x12, 0, 1, 0x12, 2);
        var second = Run(false, 0x07, 0x12, 1, 0x07, 0x12, 2, 3);
        var full = Run(false, 0x12, 0x07, 1, 0x12, 0x07, 2, 3);
        var calculator = TwoSpells.Extras ? Run(true, 0x15, 0x12, 1, 0x15, 0x12, 2, 3) : "01 15 12 64 0A 0Bx";
        var other = Run(false, 0x07, 0x0A, 1, 0x07, 0x0A, 2);
        TwoSpells.Usable = realUsable;
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        Check($"a Lancer's commands: {lancer} (Dragon Tech, type {table[DragonTech.Skillset] & 0x0F}, after Jump)", lancer == "01 12 64 02");
        Check($"Jump as the secondary: {second}", second == "01 07 12 64 02 03");
        Check($"a full list keeps six: {full} (Equip Change drops)", full == "01 12 64 07 02 03");
        Check($"a Calculator with Jump: {calculator} (Two Spells' after Dragon Tech, Defend and Equip Change drop)",
              calculator == "01 15 12 64 0A 0Bx");
        Check($"no Jump, no Dragon Tech: {other}", other == "01 07 0A 02");

        // The learned test's 14 bytes, run from a copy: the jbe lands 0x14 past them (a job index in eax), a fall
        // through goes on to the 0xE0 test.
        var learned = (byte*)DragonTech.LearnedAt;
        Check($"the list builder's learned test (0x{DragonTech.LearnedAt - image:X}) is rewritten",
              new ReadOnlySpan<byte>(learned, 14).SequenceEqual(DragonTech.LearnedCode));
        var code = GameImage.AllocateCode(64);
        byte[] head = [0x41, 0x57, 0x44, 0x8B, 0xF9, 0xE8, 0x06, 0x00, 0x00, 0x00, 0x41, 0x5F, 0xC3]; // push r15; mov r15d, ecx; call +6 (16); pop r15; ret
        head.CopyTo(new Span<byte>(code, head.Length));
        new ReadOnlySpan<byte>(learned, 14).CopyTo(new Span<byte>(code + 16, 14));
        byte[] through = [0xB8, 0xFF, 0xFF, 0xFF, 0x7F, 0xC3]; // mov eax, 0x7FFFFFFF; ret
        through.CopyTo(new Span<byte>(code + 30, through.Length));
        code[50] = 0xC3;
        var job = (delegate* unmanaged<int, int>)code;
        string Index(int skillset) => job(skillset) is var r && r == 0x7FFFFFFF ? "-" : r.ToString();
        var indices = string.Join(" ", new[] { 0x64, 0x12, 0x05, 0x17, 0x18, 0x63, 0x65, 0x04, 0xE0 }.Select(s => $"{s:X2}:{Index(s)}"));
        Check($"its job indices: {indices} (0x64 reads Jump's, 13; other skillsets as before)",
              indices == "64:13 12:13 05:0 17:18 18:- 63:- 65:- 04:- E0:-");

        // Jump's list through the menu's weapon stub (the ability in edi, the skillset in r15d, the battle index at
        // the function's [rsp+0x28]), for a unit with empty hands in battle slot 4.
        var harness = GameImage.AllocateCode(128);
        byte[] harnessCode =
        [
            0x57, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x30, 0x8B, 0xF9, 0x45, 0x8B, 0xF8, 0x89, 0x54, 0x24, 0x20,
            0x41, 0xFF, 0xD1, 0x48, 0x83, 0xC4, 0x30, 0x41, 0x5F, 0x5F, 0xC3,
        ];
        harnessCode.CopyTo(new Span<byte>(harness, harnessCode.Length));
        byte[] passCode = [0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3];
        byte[] failCode = [0x31, 0xC0, 0xC3];
        passCode.CopyTo(new Span<byte>(harness + 64, passCode.Length));
        failCode.CopyTo(new Span<byte>(harness + 80, failCode.Length));
        var stub = GameImage.AllocateCode(256);
        Weapons.WriteStub(stub, Weapons.Kind.MenuList, (nint)(harness + 64), (nint)(harness + 80));
        var listed = (delegate* unmanaged<int, int, int, byte*, int>)harness;
        const int slot = 4;
        var lancerUnit = game.TurnCode.Units + slot * Battle.UnitSize;
        var savedUnit = new Span<byte>(lancerUnit, Battle.UnitSize).ToArray();
        new Span<byte>(lancerUnit, Battle.UnitSize).Clear();
        lancerUnit[1] = slot;
        lancerUnit[0x12] = DragonTech.Jump;
        *(ushort*)(lancerUnit + 0x24) = 0xFF;
        // Lancet needs a spear (ReMixed's weapon requirements): the first item that passes.
        var spear = Enumerable.Range(1, 0xFE).FirstOrDefault(id =>
        {
            *(ushort*)(lancerUnit + 0x20) = (ushort)id;
            return Weapons.Allows(0x144, lancerUnit);
        });
        *(ushort*)(lancerUnit + 0x20) = (ushort)(spear == 0 ? 0xFF : spear);
        *game.RuleCode.AbilitiesReady = 1;
        string Listed(int skillset, params int[] ids) => string.Join(" ", ids.Where(id => listed(id, slot, skillset, stub) != 0).Select(id => $"{id:X3}"));
        var jumpList = Listed(DragonTech.Jump, 0x18A, 0x18B, 0x144, 0x151, 0x152, 0x153);
        var dragonList = Listed(DragonTech.Skillset, 0x144, 0x151, 0x152, 0x153);
        savedUnit.CopyTo(new Span<byte>(lancerUnit, Battle.UnitSize));
        Check($"Jump's list in the menu: {jumpList} (no Lancet or Breaths)", jumpList == "18A 18B");
        Check($"Dragon Tech's, holding item 0x{spear:X2}: {dragonList}", dragonList == "144 151 152 153");

        // The AI's stub, against landing pads: 1 goes on as an entry, 2 left out, 3 to the Jump merge.
        var ai = (byte*)DragonTech.AiAt;
        Check($"the AI's type 9 test (0x{DragonTech.AiAt - image:X}) jumps to a stub; the bare Jump is gone (0x{DragonTech.BareAt - image:X} jmp)",
              ai[0] == 0xFF && ai[1] == 0x25 && *(int*)(ai + 2) == 0 && *(byte*)DragonTech.BareAt == 0xEB);
        var pads = GameImage.AllocateCode(128);
        byte[] run =
        [
            0x41, 0x54, 0x55, 0x41, 0x56,       // push r12, rbp, r14
            0x41, 0x89, 0xCC, 0x89, 0xD5,       // mov r12d, ecx; mov ebp, edx
            0x4D, 0x89, 0xC6,                   // mov r14, r8
            0x41, 0xFF, 0xD1,                   // call r9 (the stub)
            0x41, 0x5E, 0x5D, 0x41, 0x5C, 0xC3, // pop r14, rbp, r12; ret
        ];
        run.CopyTo(new Span<byte>(pads, run.Length));
        for (var k = 1; k <= 3; k++)
        {
            byte[] pad = [0xB8, (byte)k, 0x00, 0x00, 0x00, 0xC3];
            pad.CopyTo(new Span<byte>(pads + 32 + 16 * k, pad.Length));
        }
        var aiStub = GameImage.AllocateCode(DragonTech.AiStubSize);
        DragonTech.WriteAiStub(aiStub, DragonTech.AiBound, (nint)(pads + 48), (nint)(pads + 64), (nint)(pads + 80));
        var go = (delegate* unmanaged<int, int, byte*, byte*, int>)pads;
        var entry = stackalloc byte[4];
        string Ai(int type, int ability)
        {
            entry[2] = DragonTech.Jump;
            var r = go(type, ability, entry, aiStub);
            return $"{r}/{entry[2]:X2}";
        }
        var aiCases = string.Join(" ", new (int Type, int Ability)[] { (9, 0x144), (9, 0x153), (9, 0x18A), (9, 0x18B), (9, 0x195), (0, 0x144) }
            .Select(c => $"{c.Ability:X3}:{Ai(c.Type, c.Ability)}"));
        Check($"the AI's entries in a Jump list: {aiCases} (Lancet and the Breaths as 0x64's; Jump merges; past the bound left out; another type's as before)",
              aiCases == "144:1/64 153:1/64 18A:3/12 18B:2/12 195:2/12 144:1/12");
        return failures;
    }

    /// <summary>
    /// Monster reactions by family, through the hooked R/S/M pick: a monster's random reaction is
    /// its family's (ReMixed's table, from the build), and everything the game decides itself stays:
    /// a fixed id, a monster's random support or movement (none), a monster job off the table.
    /// </summary>
    public static int MonsterReactionPicks(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: monster reactions, {name}{(ok ? "" : ": WRONG")}");
        }
        if (MonsterReactions.PickAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        var record = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(0x40);
        var pick = (delegate* unmanaged<byte*, int, int, byte*, int>)MonsterReactions.PickAt;
        unit[6] = 0x20; // a monster
        int Pick(int job, int id, int kind)
        {
            unit[3] = (byte)job;
            return pick(unit, id, kind, record);
        }
        Check("a Chocobo's (job 0x5E) random reaction is Counter (0x1BA), a Behemoth's (0x87) Hamedo (0x1C5), a Hydra's (0x8D) HP Restore (0x1AF)",
              Pick(0x5E, 0x1FE, 2) == 0x1BA && Pick(0x87, 0x1FE, 2) == 0x1C5 && Pick(0x8D, 0x1FE, 2) == 0x1AF);
        Check("a Juravis (0x76) gets Counter Steal's slot (0x1B7)", Pick(0x76, 0x1FE, 2) == 0x1B7);
        Check("a monster's random support and movement stay none", Pick(0x5E, 0x1FE, 4) == 0 && Pick(0x5E, 0x1FE, 8) == 0);
        Check("a fixed reaction stays", Pick(0x5E, 0x1A6, 2) == 0x1A6 && Pick(0x5E, 0, 2) == 0);
        Check("a monster job off the table (0x8E) gets none", Pick(0x8E, 0x1FE, 2) == 0);
        System.Runtime.InteropServices.NativeMemory.Free(unit);
        System.Runtime.InteropServices.NativeMemory.Free(record);
        return failures;
    }

    /// <summary>
    /// Limit's charge in the two ability lists: each stub through a harness that lays out the
    /// list builder's frame (the battle index in its [rsp+0x28], the skillset in its [rsp+0x21]
    /// and r15b, the CT in ebx, Short Charge in r14 and r12), with its exits pointed at thunks;
    /// then each patched test's jump and the installed stub's exits.
    /// </summary>
    public static int LimitPreviews(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: Limit preview, {name}{(ok ? "" : ": WRONG")}");
        }
        if (LimitPreview.Sites.Count != 2)
        {
            Check($"{LimitPreview.Sites.Count} lists patched, not 2", false);
            return failures;
        }
        var code = GameImage.AllocateCode(128);
        var stub = GameImage.AllocateCode(LimitPreview.StubSize);
        var done = code + 96;
        byte[] doneCode = [0x8B, 0xC3, 0xC3];                                           // mov eax, ebx; ret
        doneCode.CopyTo(new Span<byte>(done, doneCode.Length));
        var nonCharge = code + 112;
        byte[] nonChargeCode = [0x49, 0x83, 0xFC, 0x02, 0x41, 0x0F, 0x44, 0xDB, 0x8B, 0xC3, 0xC3]; // cmp r12, 2; cmove ebx, r11d; mov eax, ebx; ret
        nonChargeCode.CopyTo(new Span<byte>(nonCharge, nonChargeCode.Length));
        var harness = new List<byte>
        {
            0x53, 0x41, 0x54, 0x41, 0x56, 0x41, 0x57, // push rbx, r12, r14, r15
            0x48, 0x83, 0xEC, 0x30,                   // sub rsp, 0x30: the stub then starts 16-aligned
            0x89, 0x4C, 0x24, 0x20,                   // mov [rsp+0x20], ecx: the stub's [rsp+0x28], the battle index
            0x88, 0x54, 0x24, 0x19,                   // mov [rsp+0x19], dl: its [rsp+0x21], the skillset
            0x44, 0x0F, 0xB6, 0xFA,                   // movzx r15d, dl: and the second list's
            0x44, 0x89, 0xC3,                         // mov ebx, r8d: the CT
            0x45, 0x89, 0xCE, 0x45, 0x89, 0xCC,       // mov r14d, r9d; mov r12d, r9d: Short Charge 1, Non-charge 2
            0x45, 0x31, 0xDB,                         // xor r11d, r11d
            0x48, 0xB8,                               // mov rax, the stub
        };
        harness.AddRange(BitConverter.GetBytes((ulong)stub));
        harness.AddRange([0xFF, 0xD0, 0x48, 0x83, 0xC4, 0x30, 0x41, 0x5F, 0x41, 0x5E, 0x41, 0x5C, 0x5B, 0xC3]); // call rax; add rsp, 0x30; pop …; ret
        harness.ToArray().CopyTo(new Span<byte>(code, harness.Count));
        var run = (delegate* unmanaged<int, int, int, int, int>)code;

        var unit = game.TurnCode.Units + 4 * Battle.UnitSize;
        var saved = new Span<byte>(unit, Battle.UnitSize).ToArray();
        unit[1] = 0x32;
        *(ushort*)(unit + Battle.UnitMaxHp) = 200;
        foreach (var site in LimitPreview.Sites)
        {
            LimitPreview.WriteStub(stub, site with { Done = (nint)done, NonCharge = site.NonCharge == 0 ? 0 : (nint)nonCharge });
            int Ct(int hp, int skillset, int ct, int mode)
            {
                *(ushort*)(unit + Battle.UnitHp) = (ushort)hp;
                return run(4, skillset, ct, mode);
            }
            // CT 8 at 50 of 200 HP: two cuts of 2, so 4; Short Charge then makes it 2.
            Check($"{site.Name}: Limit (0x29) CT 8 at full HP 8, at a quarter 4, with Short Charge 2, with Non-charge 0",
                  Ct(200, 0x29, 8, 0) == 8 && Ct(50, 0x29, 8, 0) == 4 && Ct(50, 0x29, 8, 1) == 2 && Ct(50, 0x29, 8, 2) == 0);
            Check($"{site.Name}: another skillset keeps its CT (8, or 4 with Short Charge)", Ct(50, 0x10, 8, 0) == 8 && Ct(50, 0x10, 8, 1) == 4);
            var at = (byte*)site.At;
            var target = (byte*)*(nint*)(at + 6);
            var exits = -1;
            for (var k = 0; k < LimitPreview.StubSize - 14 && exits < 0; k++)
                if (target[k] == 0xFF && target[k + 1] == 0x25 && *(int*)(target + k + 2) == 0 && *(nint*)(target + k + 6) == site.Done)
                    exits = k;
            Check($"{site.Name}'s test (0x{site.At - game.Image.ImageStart:X}) jumps to its stub, which goes on to 0x{site.Done - game.Image.ImageStart:X}"
                  + (site.NonCharge != 0 ? $" or 0x{site.NonCharge - game.Image.ImageStart:X}" : ""),
                  at[0] == 0xFF && at[1] == 0x25 && *(int*)(at + 2) == 0 && exits > 0
                  && (site.NonCharge == 0 || *(nint*)(target + exits + 20) == site.NonCharge));
        }
        saved.CopyTo(new Span<byte>(unit, Battle.UnitSize));
        return failures;
    }

    /// <summary>
    /// Monster Skill and Train from anywhere, through the hooked check, with every other slot
    /// empty: an ally across the map with either gives the monster its 4th ability and Move+1,
    /// once; an enemy's doesn't, nor anyone's while a reaction is applied, and then the Move+1 comes off.
    /// </summary>
    public static int MonsterSkills(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: monster skill, {name}{(ok ? "" : ": WRONG")}");
        }
        if (MonsterSkill.CheckAt == 0)
        {
            Check("isn't installed", false);
            return failures;
        }
        var units = game.TurnCode.Units;
        var all = new Span<byte>(units, Battle.UnitCount * Battle.UnitSize);
        var saved = all.ToArray();
        all.Clear();
        for (var k = 0; k < Battle.UnitCount; k++)
            units[k * Battle.UnitSize + 1] = 0xFF;
        var monster = units + 6 * Battle.UnitSize;
        var ally = units + 7 * Battle.UnitSize;
        monster[1] = 0x50;
        monster[Battle.UnitIndex] = 6;
        monster[Battle.UnitMove] = 4;
        monster[Battle.UnitX] = 1;
        monster[Battle.UnitY] = 1;
        ally[1] = 0x51;
        ally[Battle.UnitIndex] = 7;
        ally[Battle.UnitX] = 9;
        ally[Battle.UnitY] = 9;
        var check = (delegate* unmanaged<byte*, int>)MonsterSkill.CheckAt;
        string Run()
        {
            var r = check(monster);
            return $"{r}, Move {monster[Battle.UnitMove]}{((monster[Battle.UnitMovement] & 0x80) != 0 ? " (Move+1)" : "")}";
        }
        Check("no one with Monster Skill: 0, Move 4", Run() == "0, Move 4");
        ally[0x9B] = 0x80;
        Check("an ally across the map with Monster Skill: 1, Move 5 with the Move+1 bit", Run() == "1, Move 5 (Move+1)");
        Check("asked again: still Move 5", Run() == "1, Move 5 (Move+1)");
        ally[0x9B] = 0;
        ally[0x9A] = 0x80;
        Check("Train counts too", Run() == "1, Move 5 (Move+1)");
        ally[Battle.UnitCurrentTeam] = 0x10;
        Check("an enemy's doesn't, and the Move+1 comes off", Run() == "0, Move 4");
        ally[Battle.UnitCurrentTeam] = 0;
        var reacting = *MonsterSkill.Reacting;
        *MonsterSkill.Reacting = 1;
        Check("while a reaction is applied, no one counts", Run() == "0, Move 4");
        *MonsterSkill.Reacting = reacting;
        Check("then back to Move 5", Run() == "1, Move 5 (Move+1)");
        saved.CopyTo(all);
        return failures;
    }

    /// <summary>
    /// Deadly tiles: the just-died stub through a harness (r11 the unit and bl 0, entered 16-aligned
    /// as from the game's apply step), with its exits pointed at thunks that return 0 or −1; then
    /// the patched check's jump and the installed stub's exits.
    /// </summary>
    public static int DeadlyTiles(Game game, Log log)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failures++;
            log.Info($"self-test: deadly tiles, {name}{(ok ? "" : ": WRONG")}");
        }
        if (Landing.DiedAt == 0)
        {
            Check("aren't installed", false);
            return failures;
        }
        var harness = GameImage.AllocateCode(64);
        byte[] code =
        [
            0x53,                   // push rbx
            0x31, 0xDB,             // xor ebx, ebx: bl is 0 there in the game
            0x49, 0x89, 0xCB,       // mov r11, rcx: the unit
            0x48, 0x83, 0xEC, 0x28, // sub rsp, 0x28: the stub then starts 16-aligned
            0xFF, 0xD2,             // call rdx: the stub
            0x48, 0x83, 0xC4, 0x28, // add rsp, 0x28
            0x5B, 0xC3,             // pop rbx; ret
        ];
        code.CopyTo(new Span<byte>(harness, code.Length));
        var zero = harness + 32;
        byte[] zeroCode = [0x31, 0xC0, 0xC3];        // xor eax, eax; ret
        zeroCode.CopyTo(new Span<byte>(zero, zeroCode.Length));
        var minusOne = harness + 48;
        byte[] minusCode = [0x83, 0xC8, 0xFF, 0xC3]; // or eax, -1; ret
        minusCode.CopyTo(new Span<byte>(minusOne, minusCode.Length));
        var stub = GameImage.AllocateCode(Landing.DiedStubSize);
        Landing.WriteDiedStub(stub, (nint)zero, (nint)minusOne);
        var run = (delegate* unmanaged<byte*, byte*, int>)harness;

        var unit = (byte*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(Battle.UnitSize);
        game.Map.SetSize(10, 10);
        var tile = game.Map.TileAt(2, 3, 0);
        unit[Battle.UnitX] = 2;
        unit[Battle.UnitY] = 3;
        int Died(byte surface, byte depth, byte movement, bool riding)
        {
            tile[0] = surface;
            tile[3] = depth;
            unit[Battle.UnitMovement + 2] = movement;
            unit[0x1B4] = (byte)(riding ? 0x80 : 0);
            return run(unit, stub);
        }
        Check("plain ground: the body stays (0), and a rider's moves (−1) as in the game",
              Died(0, 0, 0, false) == 0 && Died(0, 0, 0, true) == -1);
        Check("lava: it moves, unless Waterwalking or Levitate (Swim doesn't help)",
              Died(0x12, 0, 0, false) == -1 && Died(0x12, 0, 0x80, false) == 0 && Died(0x12, 0, 0x08, false) == 0
              && Died(0x12, 0, 0x40, false) == -1);
        Check("water 3 deep: it moves, unless Swim, Waterbreathing, Waterwalking or Levitate",
              Died(0, 0x60, 0, false) == -1 && Died(0, 0x60, 0x40, false) == 0 && Died(0, 0x60, 0x10, false) == 0
              && Died(0, 0x60, 0x80, false) == 0 && Died(0, 0x60, 0x08, false) == 0);
        Check("water 2 deep, or a slope: it stays", Died(0, 0x40, 0, false) == 0 && Died(0, 0x5F, 0, false) == 0);
        game.Map.SetSize(0, 0);
        System.Runtime.InteropServices.NativeMemory.Free(unit);

        var at = (byte*)Landing.DiedAt;
        var installed = at + 5 + *(int*)(at + 1);
        var exits = -1;
        for (var k = 0; k < Landing.DiedStubSize - 28 && exits < 0; k++)
            if (installed[k] == 0xFF && installed[k + 1] == 0x25 && *(int*)(installed + k + 2) == 0)
                exits = k;
        Check($"the apply step's just-died check (0x{Landing.DiedAt - game.Image.ImageStart:X}) jumps to its stub, which goes on to "
              + $"0x{Landing.DiedZero - game.Image.ImageStart:X} or 0x{Landing.DiedMinusOne - game.Image.ImageStart:X}",
              at[0] == 0xE9 && exits > 0 && *(nint*)(installed + exits + 6) == Landing.DiedZero
              && *(nint*)(installed + exits + 20) == Landing.DiedMinusOne);
        return failures;
    }
}
