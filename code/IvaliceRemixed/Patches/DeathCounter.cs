using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's death counter (docs/MECHANICS.md: "Death counter"; PS1 0x80183158 in the death tick,
/// then 0x801581D8 and 0x801582C4). Installed by <see cref="Rules"/> with "death-counter".
/// </summary>
/// <remarks>
/// <para>
/// The remaster's tick for a dead unit (in 0x30F9A0, from the battle loop) skips a unit with +5 &amp; 4
/// or +6 &amp; 9, counts +7 down, and at 0 revives an undead unit on a coin flip or else puts a crystal
/// or a treasure into the unit's result. ReMixed leaves a counter of 0xFF alone (a frozen corpse).
/// With the Player Deaths option on "Revive after battle" (flag 0xDD), a player unit whose counter
/// runs out freezes instead, Ramza too: counter 0xFF, innate Dead (+0x57 |= 0x20, so nothing revives
/// it this battle) and +6 |= 4, which keeps it out of the battle's unit lists (0x360780, 0x33E038)
/// and which the game clears when it writes a battle unit into the roster. So it's back after the
/// battle. The General Deaths option can make every lasting death a crystal or a treasure box.
/// </para>
/// <para>
/// The default is ReMixed's once its story begins: event 0x003 sets flag 0xD8, so death is
/// permanent as in vanilla. Its Rendezvous battles (the missions, events 0xA8 on) set flag 0xD9,
/// which freezes a player unit whatever the Player Deaths setting (<see cref="RendezvousFlag"/>).
/// Without any of those flags (before that event) ReMixed freezes everyone but Ramza; the story's
/// flags don't exist here, so neither does that state. Every tick but a revival counts as a turn sat
/// out for First Move.
/// </para>
/// </remarks>
internal static unsafe class DeathCounter
{
    private static Game _game = null!;
    private static int _failed;

    private const int Counter = 7, Frozen = 0xFF, InnateStatuses = 0x57, Dead = 0x20, Hidden = 0x04;
    /// <summary>ReMixed's flag for a Rendezvous battle: each mission's event sets it as the battle opens (the mod runs
    /// those events), and its options say Player Deaths applies "outside of a Rendezvous" (event 0xC9). The starts of
    /// ReMixed's other battles clear it (events 0x008, 0x07E–0x086, 0x190); the remaster's battles run none of those,
    /// so the world map clears it (<see cref="RemixedScenarios"/>' buttons). The remaster's code doesn't use it.</summary>
    internal const int RendezvousFlag = 0xD9;
    /// <summary>The event variables the rule reads: the game's getter (the offline check gives its own).</summary>
    internal static Func<int, int> ReadVariable = id => Nemesis.GetVariable(id);
    internal const int StubSize = 256;

    // For the offline check.
    internal static nint TickAt { get; private set; }     // the tick (0x30F9A0)
    internal static nint CountAt { get; private set; }    // the patched count-down
    internal static nint ExpireAt { get; private set; }   // the patched crystal-or-treasure choice

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // Not while simulating; skip +5 & 4 and +6 & 9; counter − 1; at 0, undead first.
        var at = image.FindUnique(
            "39 05 ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? F6 47 05 04 0F 85 ?? ?? ?? ?? F6 47 06 09 0F 85 ?? ?? ?? ?? " +
            "8A 47 07 41 B8 01 00 00 00 41 2A C0 3C FF 74 0A 88 47 07 33 C0 E9 ?? ?? ?? ?? F6 C1 10 74");
        var ret = GameImage.Rip(at + 53, 1, 5);
        var expire = at + 63 + *(sbyte*)(at + 62);
        // The crystal or treasure: random & (0x10, or 0x110 for team +0x1EE & 8).
        byte[] choice = [0x0F, 0xB6, 0x8F, 0xEE, 0x01, 0x00, 0x00, 0xBA, 0x08, 0x00, 0x00, 0x00, 0x23, 0xCA, 0x41, 0x8B, 0xC0,
                         0xC1, 0xE1, 0x05, 0x83, 0xC9, 0x10, 0x41, 0x85, 0xC9];
        // The tick itself: its unit in rdx, its result at the unit's +0x1BE.
        var tick = image.FindUnique("48 89 5C 24 08 57 48 83 EC 20 48 8D 9A BE 01 00 00 48 8B FA 48 8B CB E8");
        if ((int*)GameImage.Rip(at, 2, 6) != game.Routines.Simulating || GameImage.Rip(at + 16, 2, 6) != ret
            || !Matches(ret, [0x48, 0x8B, 0x5C, 0x24, 0x30]) || !Matches(expire, choice) || (ulong)(at - tick) > 0x100)
            throw new InvalidDataException("the death counter isn't the code this DLL expects");
        _ = game.Battle; // found now, so a missing pattern stops this here

        var stubs = GameImage.AllocateCode(2 * StubSize);
        WriteCountStub(stubs, at + 58, at + 48, ret);
        WriteExpireStub(stubs + StubSize, ret, expire + 17);
        Patch(at + 32, 16, (nint)stubs);
        Patch(expire, 17, (nint)(stubs + StubSize));
        TickAt = tick;
        CountAt = at + 32;
        ExpireAt = expire;
    }

    private static bool Matches(nint at, byte[] bytes) => new ReadOnlySpan<byte>((void*)at, bytes.Length).SequenceEqual(bytes);

    private static void Patch(nint at, int length, nint stub)
    {
        var jump = new byte[length];
        Array.Fill(jump, (byte)0xCC);
        AbsoluteJump(jump, 0, stub);
        GameImage.WriteCode(at, jump);
    }

    /// <summary>
    /// Save the scratch registers, rcx = rdi (the unit), call <paramref name="managed"/>, restore, then
    /// test eax, eax (or cmp eax, 1).
    /// </summary>
    private static List<byte> CallWithUnit(nint managed, bool compareWithOne = false)
    {
        var code = new List<byte>
        {
            0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, // push rax, rcx, rdx, r8–r11
            0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00,                         // sub rsp, 0x88
        };
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x48, 0x89, 0xF9, 0x48, 0xB8]);                         // mov rcx, rdi; mov rax, managed
        code.AddRange(BitConverter.GetBytes((ulong)managed));
        code.AddRange([0xFF, 0xD0]);                                           // call rax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);           // add rsp, 0x88
        code.AddRange(compareWithOne ? [0x83, 0xF8, 0x01] : [0x85, 0xC0]);     // cmp eax, 1 / test eax, eax
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax (flags kept)
        return code;
    }

    /// <summary>
    /// The count-down's stub, entered by a jump with rdi the unit and the stack 16-aligned: a frozen
    /// counter returns 0 at once; otherwise the game's own counter − 1, on to its expiry or its store.
    /// </summary>
    internal static void WriteCountStub(byte* stub, nint expired, nint store, nint ret)
    {
        var code = CallWithUnit((nint)(delegate* unmanaged<byte*, int>)&Count);
        code.AddRange([0x74, 0x2C]);                                           // jz frozen
        code.AddRange([0x8A, 0x47, 0x07, 0x41, 0xB8, 0x01, 0x00, 0x00, 0x00, 0x41, 0x2A, 0xC0, 0x3C, 0xFF]); // the game's
        code.AddRange([0x75, 0x0E]);                                           // jne store
        AbsoluteJump(code, expired);
        AbsoluteJump(code, store);
        code.AddRange([0x33, 0xC0]);                                           // frozen: xor eax, eax
        AbsoluteJump(code, ret);
        Copy(stub, code);
    }

    /// <summary>
    /// The expiry's stub: <see cref="Expire"/> 1 (frozen) returns 0, 2 (it set the body itself) returns
    /// 1, and 0 goes on to the game's own crystal or treasure.
    /// </summary>
    internal static void WriteExpireStub(byte* stub, nint ret, nint resume)
    {
        var code = CallWithUnit((nint)(delegate* unmanaged<byte*, int>)&Expire, compareWithOne: true);
        code.AddRange([0x74, 0x15]);                                           // je frozen
        code.AddRange([0x72, 0x23]);                                           // jb the game's choice
        code.AddRange([0xB8, 0x01, 0x00, 0x00, 0x00]);                         // mov eax, 1: a crystal or treasure
        AbsoluteJump(code, ret);
        code.AddRange([0x33, 0xC0]);                                           // frozen: xor eax, eax
        AbsoluteJump(code, ret);
        code.AddRange([0x0F, 0xB6, 0x8F, 0xEE, 0x01, 0x00, 0x00, 0xBA, 0x08, 0x00, 0x00, 0x00, 0x23, 0xCA, 0x41, 0x8B, 0xC0]); // the game's
        AbsoluteJump(code, resume);
        Copy(stub, code);
    }

    private static void Copy(byte* stub, List<byte> code)
    {
        if (code.Count > StubSize)
            throw new InvalidOperationException("the death counter stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }

    /// <summary>jmp [rip+0] with the target after it.</summary>
    private static void AbsoluteJump(List<byte> code, nint target)
    {
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);
        code.AddRange(BitConverter.GetBytes((ulong)target));
    }

    private static void AbsoluteJump(byte[] code, int at, nint target)
    {
        code[at] = 0xFF;
        code[at + 1] = 0x25;
        BitConverter.TryWriteBytes(code.AsSpan(at + 2), 0);
        BitConverter.TryWriteBytes(code.AsSpan(at + 6), (ulong)target);
    }

    /// <summary>A dead unit's tick: 0 for a frozen corpse, which stays; 1 to count down as the game does.</summary>
    [UnmanagedCallersOnly]
    private static int Count(byte* unit)
    {
        try
        {
            var counter = unit[Counter];
            if (counter != 0)
                Movement.CountDeadTurn(unit[Battle.UnitIndex]); // a turn sat out
            return counter == Frozen ? 0 : 1;
        }
        catch (Exception e)
        {
            Failed("the count-down", e);
            return 1;
        }
    }

    /// <summary>
    /// A counter that ran out (and no undead revival): 1 if the unit froze, 2 if this chose its body,
    /// 0 for the game's own crystal or treasure.
    /// </summary>
    [UnmanagedCallersOnly]
    private static int Expire(byte* unit)
    {
        try
        {
            Movement.CountDeadTurn(unit[Battle.UnitIndex]);
            var options = Options.Current;
            if (Freezes(unit, options))
            {
                unit[Counter] = Frozen;
                unit[InnateStatuses] |= Dead;
                unit[6] |= Hidden;
                Tracer.Line(_game.Log, $"death counter: unit {unit[Battle.UnitIndex]} freezes, to revive after the battle");
                return 1;
            }
            var result = unit + Battle.UnitResult;
            switch (options.Bodies)
            {
                case Options.BodyRule.Crystal:
                    result[Battle.ResultStatusAdded] = 0x40;
                    result[Battle.ResultFlags] = 8;
                    return 2;
                case Options.BodyRule.TreasureBox:
                    result[Battle.ResultStatusAdded + 1] = 0x01;
                    result[Battle.ResultFlags] = 8;
                    return 2;
                default:
                    return 0;
            }
        }
        catch (Exception e)
        {
            Failed("the expiry", e);
            return 0;
        }
    }

    /// <summary>Whether a unit whose counter ran out freezes: one on the player's team (+5 &amp; 0x30), with Revive after
    /// battle or in a Rendezvous battle (<see cref="RendezvousFlag"/>).</summary>
    internal static bool Freezes(byte* unit, Options options) =>
        (unit[Battle.UnitCurrentTeam] & 0x30) == 0
        && (options.PlayerDeaths == Options.PlayerDeathRule.ReviveAfterBattle || ReadVariable(RendezvousFlag) != 0);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"death counter: {what} failed: {e}");
    }
}
