namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's charge that locks the move (docs/MECHANICS.md: "A charge locks the move"; PS1 0x8015E428–0x8015E4DC, in
/// its turn-state code at the queue's commit, 0x8015E3F0 from 0x8017C8A8): queueing a charged ability whose target
/// depends on where the unit stands uses up its move. Installed by <see cref="Rules"/> with "charge-lock".
/// </summary>
/// <remarks>
/// ReMixed's test, for the unit whose action is committed: a gun skill (0x190–0x19D) always locks; otherwise nothing
/// with Non-charge (+0x92 & 4, here +0x9B), nor for a rider (+0x182 bit 7, here +0x1B4) using a monster's skillset
/// (the opened command from 0xA8) unless it's a monster itself, nor without a CT in the ability's data (+0xC). Then
/// it locks for weapon range (+3 & 0x20) or +4 & 4, not with +5 & 4, and otherwise for an area round the unit (range
/// +0 zero, area +1 not). The lock is the moved byte (+0x187): 0x90 if the unit hadn't moved, 0x80 if it had (bit 7
/// is ReMixed's "moved", 0x10 marks the lock for Cancel Casting, which gives the move back). Here the shared queue
/// hook (<see cref="Rules"/>) asks <see cref="AfterQueue"/> after a real commit (not while the game simulates, not in
/// a dry run), and the lock is the remaster's moved bit (+0x1B9 & 1, as 0x310548 sets it). Cancel Casting isn't in
/// the remaster, so there's nothing to give back.
/// </remarks>
internal static unsafe class ChargeLock
{
    public const int FirstGunSkill = 0x190, LastGunSkill = 0x19D, MonsterSkillsets = 0xA8;
    private const int UnitRecord = 0x1A0, Moved = 0x1B9, Linked = 0x1B4, NonCharge = Battle.UnitSupports + 3;

    private static Game _game = null!;
    private static int* _dryRun;

    internal static bool Installed { get; private set; }

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // The queue's commit leaves "acted" alone during a dry run ([0x186AF9C]), as Two Spells finds it.
        var dryRun = image.FindUnique("44 84 F3 74 2B 39 3D ?? ?? ?? ?? 75 23 88 9E BA 01 00 00");
        if ((ulong)(dryRun - (nint)game.RuleCode.QueueAction) > 0x600)
            throw new InvalidDataException("the dry-run test isn't in the action queue");
        _dryRun = (int*)GameImage.Rip(dryRun + 5, 2, 6);
        _ = game.TurnCode.Units;
        Rules.HookQueue(game);
        Installed = true;
    }

    /// <summary>After the queue (from <see cref="Rules"/>' hook): a committed position-dependent charge locks the move.</summary>
    internal static void AfterQueue(byte* record, byte* queued, int commit, int result)
    {
        if (!Installed || commit == 0 || result < 0 || record == null || *_game.Routines.Simulating != 0 || *_dryRun != 0)
            return;
        int index = record[0];
        if ((uint)index >= Battle.UnitCount || queued != _game.TurnCode.Units + index * Battle.UnitSize + UnitRecord)
            return;
        var unit = queued - UnitRecord;
        int skillset = record[1], ability = *(ushort*)(record + 2);
        if (!Locks(unit, skillset, ability) || (unit[Moved] & 1) != 0)
            return;
        unit[Moved] |= 1;
        Tracer.Line(_game.Log, $"charge lock: unit {index}'s 0x{ability:X3} (skillset 0x{skillset:X2}) uses up its move");
    }

    /// <summary>ReMixed's test: whether committing this ability locks the unit's move.</summary>
    internal static bool Locks(byte* unit, int skillset, int ability)
    {
        if (ability is >= FirstGunSkill and <= LastGunSkill)
            return true;
        if ((unit[NonCharge] & 4) != 0)
            return false;
        if ((unit[Linked] & 0x80) != 0 && (unit[6] & 0x20) == 0 && skillset >= MonsterSkillsets)
            return false; // a rider using its mount's skills
        var data = (uint)ability < 0x170 ? _game.RuleCode.GetAbilityAction(ability) : null;
        if (data == null || data[0xC] == 0)
            return false;
        if ((data[3] & 0x20) != 0 || (data[4] & 0x04) != 0)
            return true;
        if ((data[5] & 0x04) != 0)
            return false;
        return data[0] == 0 && data[1] != 0;
    }
}
