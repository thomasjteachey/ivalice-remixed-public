namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Bonus EXP &amp; JP on Victory (var 0x14 byte 2's high nibble; docs/MECHANICS.md), off by default: after a
/// won battle every unit on the player's team gets a level, and/or ten times an action's JP base in its job. Installed
/// by <see cref="Rules"/> with "victory-bonus" when the setting isn't None.
/// </summary>
/// <remarks>
/// <para>ReMixed does it in event 0x008's script. EXP (bit 1): every battle unit on the player's team (+1 isn't 0xFF,
/// the team +0x1BA &amp; 0x30 is 0) gets 100 EXP: EXP to 0 and, under level 99, one level up (0x80150440). JP (bit 0):
/// every one that isn't a monster gets 10 × (8 + level / 4 + 2 × job level) in its current job and that job's total,
/// each at most 9999. Then a message lists them.</para>
/// <para>Here it runs before the battle end's write-back (<see cref="BattleEnd"/>), on the battle units, so the roster
/// gets it. The level goes through the game's own level-up (0x278AD8: EXP 100 or more, under 99, levels up with the
/// stat growth and sets EXP to 0); the JP through the game's own add (0x283664(unit, JP, team, job index): both fields,
/// the 9999 cap, the job level and the jobs it opens), with the unit's Dead, Crystal and 0x62 &amp; 1 bits masked for
/// the call, as ReMixed's script gives fallen units JP too. No message is shown; the log has it.</para>
/// </remarks>
internal static unsafe class VictoryBonus
{
    public const int Exp = Battle.UnitExp, Level = Battle.UnitLv, Job = 3, JobLevels = 0xE4, Team = Battle.UnitTeam;

    private static Game _game = null!;
    private static Options.VictoryBonusRule _rule;

    /// <summary>The game's level-up (unit), JP add (unit index, JP, team bits, job index) and job index (job). The offline check swaps the level-up.</summary>
    internal static delegate* unmanaged<byte*, int> LevelUp;
    internal static delegate* unmanaged<int, int, int, int, int> AddJp { get; private set; }
    internal static delegate* unmanaged<int, int> JobIndex { get; private set; }

    public static void Install(Game game, Options.VictoryBonusRule rule)
    {
        _game = game;
        _rule = rule;
        var image = game.Image;
        // The level-up: cmp byte [rcx+0x28], 0x64; mov rbx, rcx; jae; xor eax, eax.
        var levelUp = image.FindUnique("48 89 5C 24 08 57 48 83 EC 20 80 79 28 64 48 8B D9 73 04 33 C0 EB 27");
        // The JP add: movsxd rdi, r9d; test edx, edx; js; movsxd rbx, ecx; lea rax, [the units].
        var addJp = image.FindUnique("48 89 5C 24 08 57 48 83 EC 20 49 63 F9 85 D2 78 42 48 63 D9 48 8D 05");
        if (GameImage.Rip(addJp + 0x14, 3, 7) != (nint)game.TurnCode.Units)
            throw new InvalidDataException("the JP add doesn't index the battle units");
        // In the award: movzx ecx, byte [rbx+3]; call the job index; mov sil, [rbx+5]; mov r15d, eax.
        var jobIndexCall = image.FindUnique("0F B6 4B 03 E8 ?? ?? ?? ?? 40 8A 73 05 44 8B F8") + 4;
        LevelUp = (delegate* unmanaged<byte*, int>)levelUp;
        AddJp = (delegate* unmanaged<int, int, int, int, int>)addJp;
        JobIndex = (delegate* unmanaged<int, int>)GameImage.Rip(jobIndexCall, 1, 5);
        BattleEnd.OnBefore(game, "Bonus EXP & JP on Victory", Apply);
    }

    /// <summary>The bonus for every unit on the player's team (battle slots 0–20).</summary>
    internal static void Apply()
    {
        for (var k = 0; k < Battle.UnitCount; k++)
            ApplyTo(_game.TurnCode.Units + k * Battle.UnitSize, k, Options.Current.VictoryBonus); // a save's own
    }

    /// <summary>One unit's bonus. Returns the JP it got (0 for none, −1 when the game's add refused).</summary>
    internal static int ApplyTo(byte* unit, int index, Options.VictoryBonusRule rule)
    {
        if (unit[1] == 0xFF || (unit[Team] & 0x30) != 0)
            return 0;
        // The JP first, at the level before the bonus one (ReMixed's script gives it first).
        var jp = rule is Options.VictoryBonusRule.Jp or Options.VictoryBonusRule.Both ? GiveJp(unit, index) : 0;
        if (rule is Options.VictoryBonusRule.Exp or Options.VictoryBonusRule.Both)
        {
            int level = unit[Level];
            unit[Exp] = 100;
            LevelUp(unit);
            unit[Exp] = 0;
            Tracer.Line(_game.Log, $"victory bonus: unit {index} goes from level {level} to {unit[Level]}");
        }
        return jp;
    }

    private static int GiveJp(byte* unit, int index)
    {
        if ((unit[6] & 0x20) != 0)
            return 0;
        var job = JobIndex(unit[Job]);
        if ((uint)job >= 0x17)
            return 0;
        int levels = unit[JobLevels + job / 2];
        var jobLevel = (job & 1) != 0 ? levels & 0xF : levels >> 4;
        var jp = 10 * (8 + unit[Level] / 4 + 2 * jobLevel);
        var statuses = unit[Battle.UnitStatuses];
        var statuses2 = unit[Battle.UnitStatuses + 1];
        unit[Battle.UnitStatuses] = (byte)(statuses & ~0x60);
        unit[Battle.UnitStatuses + 1] = (byte)(statuses2 & ~0x01);
        int r;
        try
        {
            r = AddJp(index, jp, unit[Team] & 0x30, job);
        }
        finally
        {
            unit[Battle.UnitStatuses] = statuses;
            unit[Battle.UnitStatuses + 1] = statuses2;
        }
        Tracer.Line(_game.Log, $"victory bonus: unit {index} gets {jp} JP in job {job}{(r < 0 ? " (refused)" : "")}");
        return r < 0 ? -1 : jp;
    }
}
