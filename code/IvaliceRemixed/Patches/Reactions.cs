using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's reactions MP Rage (0x1BB, new) and Counter Steal (0x1B7, was Gilgame Heart;
/// docs/MECHANICS.md). Installed by <see cref="Supports"/>.
/// </summary>
internal static unsafe class Reactions
{
    private static Game _game = null!;
    private static TurnCode _code = null!;
    private static delegate* unmanaged<void> _hpReactions; // the originals
    private static delegate* unmanaged<int> _reactionEffect;
    private static delegate* unmanaged<int, int, void> _counterQueue;          // (id, ability flag mask): the counters' queue
    private static delegate* unmanaged<int, void> _afterDamageQueue;           // the original
    private static delegate* unmanaged<nint, int> _buildReaction;              // the original
    private static delegate* unmanaged<byte*, int, int, int, int> _counterSetup; // the original
    private static int* _reactingTo;     // the battle index of the unit being reacted to (0x186AFF4)
    private static bool _mpRage, _counterSteal;
    // While the reaction builder runs: units whose queued Counter Steal it's shown as Counter.
    private static readonly bool[] StealQueued = new bool[Battle.UnitCount];
    private static int _failed;

    private const int MpRageId = 0x1BB, AbsorbUsedMp = 0x1B6, CounterStealId = 0x1B7, CounterId = 0x1BA;
    private const int QueuedReaction = Battle.UnitResult + Battle.ResultReaction; // +0x1CE

    /// <summary>Hooks what <paramref name="ids"/> asks for and takes those ids out of it. Returns what it enabled.</summary>
    public static List<string> Install(Game game, HashSet<int> ids)
    {
        _mpRage = ids.Remove(MpRageId);
        var counterSteal = ids.Remove(CounterStealId);
        if (!_mpRage && !counterSteal)
            return [];
        _game = game;
        var code = _code = game.TurnCode;
        var enabled = new List<string>();
        if (counterSteal)
        {
            // On its own, so MP Rage still goes in if this can't. Its hooks pass everything through until it's all in.
            try
            {
                InstallCounterSteal(game);
                _counterSteal = true;
                enabled.Add("0x1B7 Counter Steal");
            }
            catch (Exception e)
            {
                game.Log.Error($"reactions: Counter Steal not applied: {e.Message}");
            }
        }
        _hpReactions = (delegate* unmanaged<void>)game.Hooks.Hook(
            (nint)code.HpReactions, (nint)(delegate* unmanaged<void>)&HpReactions, "the reactions to HP damage");
        if (_mpRage)
        {
            _reactionEffect = (delegate* unmanaged<int>)game.Hooks.Hook(
                (nint)code.ReactionEffect, (nint)(delegate* unmanaged<int>)&ReactionEffect, "the reaction effects");
            enabled.Add("0x1BB MP Rage");
        }
        return enabled;
    }

    private static void InstallCounterSteal(Game game)
    {
        var code = game.TurnCode;
        var image = game.Image;
        // The last of the reactions to HP damage: Magick Counter (0x1B3, ability flag 0x40) through the counters' queue.
        var queue = image.FindUnique("B9 B3 01 00 00 B2 40 E8 ?? ?? ?? ?? 48 83 C4 28 C3");
        if ((ulong)(queue - (nint)code.HpReactions) > 0x100)
            throw new InvalidDataException("the counters' queue call isn't in the reactions to HP damage");
        // Applying a result: Gilgame Heart (0x1B7) and Auto-Potion (0x1B9) after the damage, through one call.
        var afterDamage = image.FindUnique(
            "48 8B 05 ?? ?? ?? ?? 8A 88 96 00 00 00 84 CA 74 ?? B9 B7 01 00 00 EB ?? F6 C1 10 74 ?? B9 B9 01 00 00 E8 ?? ?? ?? ??");
        // Building the reactor's action from its queued reaction (+0x1CE), by id. It starts by
        // reading the index of the unit reacted to.
        var build = image.FindUnique(
            "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 54 41 56 41 57 48 83 EC 20 " +
            "44 8B 15 ?? ?? ?? ?? 4C 8B F1 41 83 FA FF 0F 84");
        // ... the id goes to the reaction id global and the queue is cleared, before the cases.
        var take = image.FindUnique("66 89 77 02 66 89 35 ?? ?? ?? ?? 66 44 89 BB CE 01 00 00 81 E9 B2 01 00 00");
        if (GameImage.Rip(take + 4, 3, 7) != (nint)code.ReactionId)
            throw new InvalidDataException("the reaction builder doesn't set the reaction id this DLL found");
        // ... Counter's case: the attack (skillset 1, ability 0) against the unit reacted to, if it's in range.
        var counter = image.FindUnique("45 33 C0 45 8B CC 41 8B D4 48 8B CB E8 ?? ?? ?? ?? 85 C0 0F 84 ?? ?? ?? ?? E9");
        // ... any other id: its type byte decides (0x30: no action of its own).
        var types = image.FindUnique("48 8D 0D ?? ?? ?? ?? F6 84 0E ?? ?? ?? ?? 30 0F 85 ?? ?? ?? ?? 0F B7 43 20 66 89 05");
        if (GameImage.Rip(types, 3, 7) != image.ImageStart)
            throw new InvalidDataException("the reaction type table isn't indexed from the image base");
        _counterQueue = (delegate* unmanaged<int, int, void>)GameImage.Rip(queue + 7, 1, 5);
        _reactingTo = (int*)GameImage.Rip(build + 29, 3, 7);
        var setup = GameImage.Rip(counter + 12, 1, 5);
        var typeOf = (byte*)(image.ImageStart + *(int*)(types + 10));
        if (typeOf[CounterId] != 0x29)
            throw new InvalidDataException($"Counter's reaction type is 0x{typeOf[CounterId]:X2}, not 0x29");
        AfterDamageQueueAt = GameImage.Rip(afterDamage + 34, 1, 5);
        BuildReactionAt = build;
        _afterDamageQueue = (delegate* unmanaged<int, void>)game.Hooks.Hook(
            AfterDamageQueueAt, (nint)(delegate* unmanaged<int, void>)&AfterDamageQueue, "the after-damage reactions' queue");
        _buildReaction = (delegate* unmanaged<nint, int>)game.Hooks.Hook(
            build, (nint)(delegate* unmanaged<nint, int>)&BuildReaction, "the reaction builder");
        _counterSetup = (delegate* unmanaged<byte*, int, int, int, int>)game.Hooks.Hook(
            setup, (nint)(delegate* unmanaged<byte*, int, int, int, int>)&CounterSetup, "the counter setup");
        // Last, once the hooks are in: ReMixed's type byte for Counter Steal (SCUS 0x8005EBE1), Counter's
        // 0x29, so the reaction effect step leaves it to the action (Gilgame Heart's 0x06 is an effect of its own).
        GameImage.Unprotected((nint)(typeOf + CounterStealId), 1, () => typeOf[CounterStealId] = 0x29);
        ReactionTypes = typeOf;
    }

    /// <summary>
    /// The target's reaction to HP damage, the first equipped one in ReMixed's order (PS1
    /// 0x8018CB00): 0x1A9, 0x1AA, 0x1AB, 0x1AD; then MP Rage, Faith Up (0x1AE) or Absorb Used MP
    /// (0x1B6) in one check (0x8018CB90 reads the bits as a halfword, mask 0x8480); 0x1B4, 0x1B5;
    /// Counter Steal (0x1B7, moved here from the after-damage reactions, 0x8018CBE0); then
    /// Counter and Magick Counter. The game's chain is vanilla's, so it's taken over when MP Rage
    /// or Counter Steal is the one.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void HpReactions()
    {
        try
        {
            var target = *_game.Routines.Target;
            if (*_game.Battle.EffectiveFormula != 7 && target != null)
            {
                var bits = target + Battle.UnitReactions;
                var earlier = (bits[0] & 0x1D) != 0; // 0x1A9, 0x1AA, 0x1AB, 0x1AD
                if (_mpRage && (bits[2] & 0x04) != 0 && !earlier)
                {
                    if (_code.CannotReact() == 0)
                        MpRage(target);
                    return;
                }
                if (_counterSteal && (bits[2] & 0x40) != 0 && !earlier
                    && (bits[1] & 0x83) == 0     // 0x1AE, 0x1B4, 0x1B5
                    && (bits[2] & 0x84) == 0)    // 0x1B6, MP Rage
                {
                    if (_code.CannotReact() == 0)
                        _counterQueue(CounterStealId, 0x10);
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Failed("the reactions to HP damage", e);
        }
        _hpReactions();
    }

    /// <summary>
    /// MP Rage (PS1 0x80150F00, from ReMixed's version of 0x8018CF74): when HP damage lands,
    /// on a Brave roll (none while the AI simulates; ReMixed's roll, so at least half the time for a
    /// human with Move-Find Item, <see cref="BraveReactions"/>), restore the larger of Max MP / 8
    /// and a quarter of the damage.
    /// </summary>
    private static void MpRage(byte* target)
    {
        var result = *_game.Battle.TargetResult;
        int damage = *(ushort*)(result + Battle.HpDamage);
        if (damage == 0)
            return;
        var amount = Math.Max(*(ushort*)(target + Battle.UnitMaxMp) >> 3, damage >> 2);
        if (*_game.Routines.Simulating == 0 && _code.Roll(100, BraveReactions.ChanceOf(target)) != 0)
            return;
        *(ushort*)(result + Battle.ResultReaction) = MpRageId;
        *(ushort*)(result + Battle.ResultReactionAmount) = (ushort)amount;
        Tracer.Line(_game.Log, $"reactions: MP Rage after {damage} damage, MP +{amount}");
    }

    /// <summary>
    /// MP Rage's effect is Absorb Used MP's (0x1B6): MP heal = the queued amount. ReMixed
    /// points 0x1BB's entry in the reaction jump table (PS1 0x8017429C) at 0x1B6's code; the
    /// remaster's is a compare chain, so the id reads as 0x1B6 for the call.
    /// </summary>
    [UnmanagedCallersOnly]
    private static int ReactionEffect()
    {
        var id = _code.ReactionId;
        if (*id != MpRageId)
            return _reactionEffect();
        *id = AbsorbUsedMp;
        try
        {
            return _reactionEffect();
        }
        finally
        {
            *id = MpRageId;
        }
    }

    // ---------------------------------------------------------------- Counter Steal

    /// <summary>The reactions queued after a result is applied: ReMixed took Counter Steal out of them (0x8018CDD4).</summary>
    [UnmanagedCallersOnly]
    private static void AfterDamageQueue(int reaction)
    {
        if (reaction != CounterStealId || !_counterSteal)
            _afterDamageQueue(reaction);
    }

    /// <summary>
    /// Building a reactor's action from its queued reaction (PS1 0x8017E1xx; 0x282E38; it returns
    /// the reactor's index, or -1). ReMixed gives Counter Steal a case of its own (0x80151700,
    /// through the jump table at 0x80174134): Counter's setup with a steal instead of the attack.
    /// Here the builder sees a queued Counter Steal as Counter for the length of the call, and
    /// <see cref="CounterSetup"/> swaps in the steal. One it didn't get to stays queued as itself.
    /// </summary>
    [UnmanagedCallersOnly]
    private static int BuildReaction(nint reactionOut)
    {
        if (!_counterSteal)
            return _buildReaction(reactionOut);
        var units = _code.Units;
        var any = false;
        for (var i = 0; i < Battle.UnitCount; i++)
        {
            var queued = (ushort*)(units + i * Battle.UnitSize + QueuedReaction);
            StealQueued[i] = *queued == CounterStealId;
            if (StealQueued[i])
            {
                *queued = CounterId;
                any = true;
            }
        }
        var reactor = _buildReaction(reactionOut);
        if (any)
            for (var i = 0; i < Battle.UnitCount; i++)
            {
                var queued = (ushort*)(units + i * Battle.UnitSize + QueuedReaction);
                if (StealQueued[i] && *queued == CounterId) // the builder takes a queue by clearing it
                    *queued = CounterStealId;
                StealQueued[i] = false;
            }
        return reactor;
    }

    /// <summary>
    /// The counter setup (PS1 0x8017E650; 0x283280): (reactor, skillset, ability, range check)
    /// writes the reactor's action against the unit reacted to and returns 0 if it can go ahead.
    /// For a Counter Steal it gets the Steal skillset (0x0E) and ReMixed's pick, and the reaction
    /// id is Counter Steal's again, as in ReMixed, so the rest of the builder and the name shown use it.
    /// </summary>
    [UnmanagedCallersOnly]
    private static int CounterSetup(byte* unit, int skillset, int ability, int check)
    {
        var steal = -1;
        var index = 0L;
        try
        {
            var units = _code.Units;
            index = (unit - units) / Battle.UnitSize;
            var victim = *_reactingTo;
            if (unit >= units && index < Battle.UnitCount && (unit - units) % Battle.UnitSize == 0 && StealQueued[index]
                && (byte)skillset == 1 && (ushort)ability == 0 && (uint)victim < Battle.UnitCount)
            {
                StealQueued[index] = false; // once: the builder sets up each reaction once
                steal = StealPick(unit, units + victim * Battle.UnitSize, _game.Routines.Random() >> 8);
            }
        }
        catch (Exception e)
        {
            Failed("Counter Steal", e);
        }
        if (steal < 0)
            return _counterSetup(unit, skillset, ability, check);
        *_code.ReactionId = CounterStealId;
        var result = _counterSetup(unit, 0x0E, steal, check);
        Tracer.Line(_game.Log, $"reactions: Counter Steal, unit {index} uses ability 0x{steal:X3}{(result == 0 ? "" : $" (can't: {result})")}");
        return result;
    }

    /// <summary>
    /// ReMixed's steal pick (PS1 0x80151700, which Banditry uses too): against an ally or a monster,
    /// Steal Exp (0x73). Otherwise a roll of 0–127 picks Steal Weapon (below 24; ReMixed doesn't check
    /// the hands there), Steal Shield (below 48), Helmet (72), Armor (96), Accessory (120) or Gil
    /// Taking, and an empty slot falls through to the next.
    /// </summary>
    internal static int StealPick(byte* thief, byte* victim, int roll)
    {
        if (((thief[Battle.UnitTeam] ^ victim[Battle.UnitTeam]) & 0x30) == 0 || (victim[6] & 0xC0) == 0)
            return 0x73;
        bool Has(int slot) => *(ushort*)(victim + slot) != 0xFF;
        if (roll < 24)
            return 0x71;
        if (roll < 48 && (Has(0x22) || Has(0x26)))
            return 0x70;
        if (roll < 72 && Has(0x1A))
            return 0x6E;
        if (roll < 96 && Has(0x1C))
            return 0x6F;
        if (roll < 120 && Has(0x1E))
            return 0x72;
        return 0x6C;
    }

    // For the offline check, which calls the hooked entries.
    internal static nint CounterQueue => (nint)_counterQueue;
    internal static nint AfterDamageQueueAt { get; private set; }
    internal static nint BuildReactionAt { get; private set; }
    internal static int* ReactingTo => _reactingTo;
    internal static byte* ReactionTypes { get; private set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"reactions: {what} failed: {e}");
    }
}
