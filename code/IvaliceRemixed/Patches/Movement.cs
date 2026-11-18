using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's movement abilities that act around a unit's turn (docs/MECHANICS.md:
/// "Repurposed movement and reaction abilities"): First Move (0x1F4) and
/// Move-Cleanse (0x1F5), installed by <see cref="Supports"/>; and its rewrite of the
/// post-move step, installed by <see cref="Rules"/> ("post-move").
/// </summary>
internal static unsafe class Movement
{
    private static Game _game = null!;
    private static TurnCode _code = null!;
    private static delegate* unmanaged<int, void> _movementSetup; // the originals
    private static delegate* unmanaged<int, int> _endOfTurn;
    private static delegate* unmanaged<byte*, int> _moveTriggers, _postMove, _turnOver;
    private static delegate* unmanaged<byte> _moveAmount; // Move-Get Exp and JP's amount (PS1 0x8017F0EC; 0x283B78)
    private static bool _cleanse, _rewrite;
    private static int _failed;

    /// <summary>
    /// Turns each unit has finished this battle, by battle index, up to 255. ReMixed
    /// keeps the same count per unit (PS1 0x80150284, 21 bytes) for First Move and
    /// its event conditions.
    /// </summary>
    private static readonly byte[] TurnsEnded = new byte[Battle.UnitCount];

    /// <summary>
    /// What Move-Cleanse removes, per status byte (PS1 0x80152960): Undead; Darkness,
    /// Confusion, Silence, Blood Suck; Oil, Berserk, Frog; Poison, Slow; Innocent,
    /// Charm, Don't Act, Death Sentence.
    /// </summary>
    private static readonly byte[] Cleansed = [0x10, 0x3C, 0x8A, 0x84, 0x65];

    /// <summary>Hooks what <paramref name="ids"/> asks for and takes those ids out of it. Returns what it enabled.</summary>
    public static List<string> Install(Game game, HashSet<int> ids)
    {
        var firstMove = ids.Remove(0x1F4);
        var cleanse = ids.Remove(0x1F5);
        var enabled = new List<string>();
        if (!firstMove && !cleanse)
            return enabled;
        _game = game;
        var code = _code = game.TurnCode;
        var hooks = game.Hooks;
        if (firstMove)
        {
            _movementSetup = (delegate* unmanaged<int, void>)hooks.Hook(
                (nint)code.MovementSetup, (nint)(delegate* unmanaged<int, void>)&MovementSetup, "the movement setup");
            HookEndOfTurn(game);
            _firstMove = true;
            BattleEvents.OnStart(game, "First Move's turn count", ResetTurns);
            enabled.Add("0x1F4 First Move");
        }
        if (cleanse)
        {
            _moveTriggers = (delegate* unmanaged<byte*, int>)hooks.Hook(
                (nint)code.MoveTriggers, (nint)(delegate* unmanaged<byte*, int>)&MoveTriggers, "the after-move triggers");
            HookPostMove(game);
            _turnOver = (delegate* unmanaged<byte*, int>)hooks.Hook(
                (nint)code.TurnOver, (nint)(delegate* unmanaged<byte*, int>)&TurnOver, "the turn-over check");
            _cleanse = true;
            enabled.Add("0x1F5 Move-Cleanse");
        }
        return enabled;
    }

    /// <summary>
    /// ReMixed's rewrite of the post-move ability step (rule "post-move"; PS1 0x8018DD44): every
    /// post-move ability the unit has applies rather than the first, Move-Get Exp and JP scale by
    /// the rates (<see cref="Gains"/>) instead of doubling with 0x1D0 and 0x1CF, and a status that
    /// blocks them doesn't for a Move-Cleanse unit.
    /// </summary>
    public static void UseRewrite(Game game)
    {
        _game = game;
        var code = _code = game.TurnCode;
        var step = (nint)code.PostMove;
        // The step's own reads: +0x9D (& 0x80 Move-MP Up, 0x40 Move-Get Exp, 0x20 Move-Get JP) and
        // +0x9C & 1 (Move-HP Up); and the amount Move-Get Exp gives, into the result's +0x2A.
        var bits = game.Image.FindUnique("44 8A 83 9D 00 00 00 41 B2 20 8A 83 9C 00 00 00 41 B1 40 24 01");
        var amount = game.Image.FindUnique("E8 ?? ?? ?? ?? 48 8B 15 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 88 42 2A");
        if ((ulong)(bits - step) > 0x100 || (ulong)(amount - step) > 0x100)
            throw new InvalidDataException("the post-move step isn't the code this DLL expects");
        _moveAmount = (delegate* unmanaged<byte>)GameImage.Rip(amount, 1, 5);
        HookPostMove(game);
        _rewrite = true;
    }

    private static void HookPostMove(Game game)
    {
        if (_postMove == null)
            _postMove = (delegate* unmanaged<byte*, int>)game.Hooks.Hook(
                (nint)game.TurnCode.PostMove, (nint)(delegate* unmanaged<byte*, int>)&PostMove, "the post-move ability");
    }

    /// <summary>For the offline check: Move-Get Exp and JP's amount function.</summary>
    internal static nint MoveAmountAt => (nint)_moveAmount;

    /// <summary>
    /// First Move (PS1 0x80151800, from the Move read at 0x80174860): Move × 2 until the
    /// unit has finished its first turn of the battle. The movement setup reads Move
    /// once, so it's doubled on the unit for the call and put back after.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void MovementSetup(int index)
    {
        byte* unit = null;
        var move = 0;
        try
        {
            if (FirstMoveApplies(index))
            {
                unit = _code.Units + index * Battle.UnitSize;
                move = unit[Battle.UnitMove];
                unit[Battle.UnitMove] = (byte)Math.Min(move * 2, 0xFF);
            }
        }
        catch (Exception e)
        {
            Failed("First Move", e);
        }
        _movementSetup(index);
        if (unit != null)
        {
            unit[Battle.UnitMove] = (byte)move;
            Tracer.Line(_game.Log, $"movement: First Move, unit {index}, Move {move} doubled to {*_code.MoveRange}");
        }
    }

    /// <summary>Whether the unit in a battle slot gets First Move's Move × 2 now.</summary>
    internal static bool FirstMoveApplies(int index)
    {
        if ((uint)index >= Battle.UnitCount || TurnsEnded[index] != 0)
            return false;
        var unit = _code.Units + index * Battle.UnitSize;
        return unit[1] != 0xFF && (unit[Battle.UnitMovement + 1] & 0x02) != 0;
    }

    private static bool _firstMove;

    /// <summary>Hooks the end of turn once: First Move's count and Two Spells' pair share it.</summary>
    internal static void HookEndOfTurn(Game game)
    {
        if (_endOfTurn != null)
            return;
        _game = game;
        _code = game.TurnCode;
        // The battle loop keeps r11 across the end of turn, which never touches it.
        _endOfTurn = (delegate* unmanaged<int, int>)game.Hooks.Hook(
            (nint)_code.EndOfTurn, Stub.KeepingRegisters((nint)(delegate* unmanaged<int, int>)&EndOfTurn), "the end of turn");
    }

    /// <summary>
    /// Counts a unit's finished turn, as ReMixed does at the end of turn (PS1 0x80159C04,
    /// from 0x80183C9C): only for a unit whose turn it is, and not while the AI simulates.
    /// </summary>
    /// <remarks>
    /// ReMixed also counts the turns a dead unit sits out (0x80157EC0, from the death
    /// counter's code); that's <see cref="CountDeadTurn"/>, from <see cref="DeathCounter"/>.
    /// </remarks>
    [UnmanagedCallersOnly]
    private static int EndOfTurn(int index)
    {
        try
        {
            if ((uint)index < Battle.UnitCount && *_game.Routines.Simulating == 0)
            {
                var unit = _code.Units + index * Battle.UnitSize;
                if (_firstMove && unit[1] != 0xFF && unit[Battle.UnitTurn] != 0 && TurnsEnded[index] < 0xFF)
                    TurnsEnded[index]++;
                TwoSpells.TurnEnded(index);
            }
        }
        catch (Exception e)
        {
            Failed("counting turns", e);
        }
        return _endOfTurn(index);
    }

    /// <summary>
    /// A turn a dead unit sits out, which ReMixed's death counter also counts (PS1 0x80157EC0), so a
    /// unit revived before its first turn ends doesn't get First Move. <see cref="DeathCounter"/>.
    /// </summary>
    internal static void CountDeadTurn(int index)
    {
        if (_endOfTurn != null && (uint)index < Battle.UnitCount && TurnsEnded[index] < 0xFF)
            TurnsEnded[index]++;
    }

    /// <summary>A new battle: no unit has had a turn. (ReMixed's count lives in BATTLE.BIN, reloaded each battle.)</summary>
    internal static void ResetTurns() => Array.Clear(TurnsEnded);

    internal static int TurnsEndedBy(int index) => TurnsEnded[index];

    /// <summary>
    /// After a move (PS1 0x80152920, at the end of 0x80180134): a unit with Move-Cleanse and
    /// something to cleanse gets a post-move ability (bit 2) and a status change to show (0x400).
    /// </summary>
    [UnmanagedCallersOnly]
    private static int MoveTriggers(byte* unit)
    {
        var flags = _moveTriggers(unit);
        try
        {
            if (unit != null && (unit[Battle.UnitMovement + 1] & 0x01) != 0 && Cleanse(unit, null))
                flags |= 0x402;
        }
        catch (Exception e)
        {
            Failed("Move-Cleanse's trigger", e);
        }
        return flags;
    }

    /// <summary>
    /// The post-move ability. With the "post-move" rule, ReMixed's whole rewrite (<see cref="Rewritten"/>).
    /// Without it, the game's step, and Move-Cleanse (ReMixed's 0x80152960 from its rewrite)
    /// removes its statuses through the unit's own result, as a status change (flags 8), even
    /// when a status would block Move-HP Up and the like (vanilla returns -1 then).
    /// </summary>
    [UnmanagedCallersOnly]
    private static int PostMove(byte* unit)
    {
        if (_rewrite && unit != null)
            return Rewritten(unit);
        var done = _postMove(unit);
        try
        {
            if (_cleanse && unit != null && (unit[Battle.UnitMovement + 1] & 0x01) != 0 && Cleanse(unit, unit + Battle.UnitResult))
            {
                done = 0;
                var result = unit + Battle.UnitResult + Battle.ResultStatusRemoved;
                Tracer.Line(_game.Log, $"movement: Move-Cleanse removes {result[0]:X2} {result[1]:X2} {result[2]:X2} {result[3]:X2} {result[4]:X2}");
            }
        }
        catch (Exception e)
        {
            Failed("Move-Cleanse", e);
        }
        return done;
    }

    /// <summary>
    /// Whether the unit's turn is over after it moves or acts (ReMixed's rewrite of PS1
    /// 0x80183D10, in 0x80151B80). It runs before the post-move ability, so ReMixed doesn't
    /// count Don't Move or Don't Act that the unit's own result is about to remove, nor Don't
    /// Act on a Move-Cleanse unit that isn't riding: its move will cleanse it, and it can
    /// still act. The bits are cleared on the unit for the call and put back after.
    /// </summary>
    /// <remarks>
    /// ReMixed toggles the bits (xor) on a copy of the unit, which would set one the unit
    /// doesn't have; only the ones it has are cleared here.
    /// </remarks>
    [UnmanagedCallersOnly]
    private static int TurnOver(byte* unit)
    {
        byte* statuses = null;
        byte before = 0;
        try
        {
            if (unit != null)
            {
                var gone = unit[Battle.UnitResult + Battle.ResultStatusRemoved + 4] & 0x0C; // Don't Move, Don't Act
                if ((unit[Battle.UnitMovement + 1] & 0x01) != 0 && (unit[Battle.UnitLinked] & 0x80) == 0)
                    gone |= 0x04;
                before = unit[Battle.UnitStatuses + 4];
                if ((before & gone) != 0)
                {
                    statuses = unit + Battle.UnitStatuses + 4;
                    *statuses = (byte)(before & ~gone);
                }
            }
        }
        catch (Exception e)
        {
            Failed("Move-Cleanse's turn check", e);
        }
        var over = _turnOver(unit);
        if (statuses != null)
        {
            Tracer.Line(_game.Log, $"movement: turn check without statuses {before & ~*statuses:X2}: turn over {over}");
            *statuses = before;
        }
        return over;
    }

    /// <summary>
    /// ReMixed's post-move step (PS1 0x8018DD44): the game's step sets up the unit's own result and
    /// checks for a status that blocks post-move abilities; with the four abilities hidden from it,
    /// it applies none. Then, in ReMixed's order, all of them do: Move-Cleanse, Move-HP Up and
    /// Move-MP Up (a tenth of the maximum, rounded up), Move-Get Exp and JP (the game's amount, 1 to
    /// Move, at the EXP and JP rates). A blocking status stops them unless the unit has Move-Cleanse.
    /// </summary>
    /// <remarks>The remaster doubles Move-Get JP for its JP-up equipment (+0x1F8 & 0x800); that stays.</remarks>
    private static int Rewritten(byte* unit)
    {
        var bits = unit + Battle.UnitMovement;
        byte hp = bits[0], others = bits[1];
        var cleanse = _cleanse && (others & 0x01) != 0;
        bits[0] = (byte)(hp & ~0x01);
        bits[1] = (byte)(others & ~0xE0);
        var blocked = _postMove(unit) < 0;
        bits[0] = hp;
        bits[1] = others;
        if (blocked && !cleanse)
            return -1;
        try
        {
            var result = unit + Battle.UnitResult;
            var applied = "";
            if (cleanse && Cleanse(unit, result))
                applied += " Move-Cleanse";
            if ((hp & 0x01) != 0)
            {
                *(ushort*)(result + Battle.HpHeal) = (ushort)((*(ushort*)(unit + Battle.UnitMaxHp) + 9) / 10);
                result[Battle.ResultFlags] |= 0x40;
                applied += $" HP+{*(ushort*)(result + Battle.HpHeal)}";
            }
            if ((others & 0x80) != 0)
            {
                *(ushort*)(result + Battle.MpHeal) = (ushort)((*(ushort*)(unit + Battle.UnitMaxMp) + 9) / 10);
                result[Battle.ResultFlags] |= 0x10;
                applied += $" MP+{*(ushort*)(result + Battle.MpHeal)}";
            }
            if ((others & 0x40) != 0)
            {
                result[Battle.ResultExp] = (byte)Math.Min(Gains.Scale(_moveAmount(), Gains.ExpRate), 0xFF);
                result[Battle.ResultFlags] |= 0x01;
                applied += $" EXP+{result[Battle.ResultExp]}";
            }
            if ((others & 0x20) != 0)
            {
                var jp = Gains.Scale(_moveAmount(), Gains.JpRate);
                if ((*(uint*)(unit + 0x1F8) & 0x800) != 0)
                    jp *= 2;
                result[Battle.ResultJp] = (byte)Math.Min(jp, 0xFF);
                result[Battle.ResultFlags] |= 0x01;
                applied += $" JP+{result[Battle.ResultJp]}";
            }
            if (applied.Length > 0)
                Tracer.Line(_game.Log, $"movement: after the move, unit {unit[Battle.UnitIndex]}:{applied}");
        }
        catch (Exception e)
        {
            Failed("the post-move abilities", e);
        }
        return 0;
    }

    /// <summary>The unit's statuses Move-Cleanse removes, added to a result's removals if one is given. True if there are any.</summary>
    private static bool Cleanse(byte* unit, byte* result)
    {
        var any = false;
        for (var i = 0; i < Cleansed.Length; i++)
        {
            var removed = (byte)(unit[Battle.UnitStatusCopy + i] & Cleansed[i]);
            if (result != null)
                result[Battle.ResultStatusRemoved + i] |= removed;
            any |= removed != 0;
        }
        if (any && result != null)
            result[Battle.ResultFlags] |= 0x08;
        return any;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"movement: {what} failed: {e}");
    }
}
