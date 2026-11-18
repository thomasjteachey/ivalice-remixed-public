using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's riding rework, step 1 (docs/analysis/riding.md, pieces 1, 2 and 9): the White Chocobo (job 0x95) can be
/// ridden, another team's free chocobo can only be boarded by a mover with Pass and Train, and at a rider's turn start a
/// mount from another team under a rider with Train joins the rider's team, with Invite. Installed by
/// <see cref="Rules"/> with "riding-boarding".
/// </summary>
/// <remarks>
/// <para>The mount test (PS1 0x801743C8, jobs 0x5E–0x60; ReMixed adds 0x95) is inlined four times in the remaster as
/// sub al, 0x5E; cmp al, 2; a short branch: the mover is a chocobo (0x27B07F), the movement setup's free-mount rule
/// (0x27B18F), the tile info (0x2812B7) and the unit at a tile (0x282B5D). Each jumps to a stub that also takes 0x95.</para>
/// <para>Boarding (PS1 0x80152860): an enemy's tile is 0x60 where vanilla's was 0x40, so an enemy's free chocobo can't
/// be stopped on, unless the mover has Pass and Train, which clear 0x20. ReMixed clears it on any unlinked unit; here only
/// on a free mount, the evident intent (two units on one tile otherwise). The movement setup's unit record (0x27B1BB,
/// bpl the index with 0x20 already decided, dil the unit's link, r15b the mover's team, r12 the mover) jumps to a stub
/// that adds 0x20 for another team's free mount unless the mover has Pass (the setup's byte, <see cref="Pass.Mover"/>)
/// and Train (+0x9A &amp; 0x80).</para>
/// <para>Train (PS1 0x8015E018, from the turn start 0x80182B64): the turn loop's two turn starts (0x30F8D4, rbx the
/// unit; 0x30F95C, rcx + r8) jump to stubs that call <see cref="TurnStart"/> with the unit, then do the game's store.
/// The mount gets the rider's team bits (+5 and +0x1EE &amp; 0x30) and Invite (+0x62 &amp; 0x40; the end of battle's join
/// reads the current statuses, as REQUIRE.OUT's 0x801C4978 does).</para>
/// </remarks>
internal static unsafe class RidingBoarding
{
    public const int WhiteChocobo = 0x95, Link = 0x1B4, Train = 0x80, TrainByte = 0x9A, InviteByte = 0x62, Invite = 0x40;
    public const int StubSize = 160;

    private static Game _game = null!;
    private static byte* _units;
    private static int _failed;

    /// <summary>For the offline check: each site and its stub.</summary>
    internal static List<(string Name, nint At, nint Stub)> Sites { get; } = [];

    public static void Install(Game game)
    {
        _game = game;
        _units = game.TurnCode.Units;
        if (Pass.Mover == null)
            throw new InvalidOperationException("needs the Pass rule, whose stub keeps the mover's movement byte");
        var image = game.Image;
        var sites = new List<(string, nint, int, Action<nint>)>();
        // The four job tests: sub al, 0x5E; cmp al, 2; then ja out (or jbe in).
        foreach (var (name, pattern, offset) in new[]
                 {
                     ("the mover is a chocobo", "8A 47 03 2C 5E 3C 02 77 18 BA 0A 00 00 00", 3),
                     ("the free-mount rule", "43 8A 44 0A 03 40 8A EE 2C 5E 3C 02 77 1A", 8),
                     ("the tile info", "42 8A 44 39 03 41 8A D2 2C 5E 3C 02 76 03 80 CA 20", 8),
                     ("the unit at a tile", "41 8A 42 A2 2C 5E 3C 02 77 2C BA 0A 00 00 00", 4),
                 })
        {
            var at = image.FindUnique(pattern) + offset;
            var branch = ((byte*)at)[4];
            var target = at + 6 + *(sbyte*)(at + 5);
            var fall = at + 6;
            // ja: out is the target; jbe: in is the target.
            var (inside, outside) = branch == 0x77 ? (fall, target) : (target, fall);
            sites.Add((name, at, 6, s => WriteJobStub((byte*)s, inside, outside)));
        }
        // The unit record: movzx r8d, bpl; mov dl, r15b; xor dl, [r10+r9+5].
        var record = image.FindUnique("40 80 CD 20 44 0F B6 C5 41 8A D7 43 32 54 0A 05") + 4;
        sites.Add(("boarding another team's mount", record, 7, s => WriteBoardingStub((byte*)s, Pass.Mover, record + 7)));
        // The turn starts: mov word [rbx+0x1B8], 1 (then +0x1BA, +0x2E); mov [rcx+r8+0x1B8], ax (then +0x2E).
        var startA = image.FindUnique("66 C7 83 B8 01 00 00 01 00 40 88 B3 BA 01 00 00 40 88 6B 2E");
        var startB = image.FindUnique("66 42 89 84 01 B8 01 00 00 42 88 44 01 2E 41 8B C2");
        var turnStart = (nint)(delegate* unmanaged<byte*, void>)&TurnStart;
        sites.Add(("the turn start", startA, 9, s => WriteTurnStartStub((byte*)s, turnStart, false,
            new ReadOnlySpan<byte>((void*)startA, 9).ToArray(), startA + 9)));
        sites.Add(("the turn start (the other path)", startB, 9, s => WriteTurnStartStub((byte*)s, turnStart, true,
            new ReadOnlySpan<byte>((void*)startB, 9).ToArray(), startB + 9)));

        var built = new List<(string, nint, int, nint)>();
        foreach (var (name, at, length, write) in sites)
        {
            var stub = (nint)GameImage.AllocateCodeNear(at, StubSize);
            write(stub);
            built.Add((name, at, length, stub));
        }
        foreach (var (name, at, length, stub) in built)
        {
            var jump = new byte[length];
            Array.Fill(jump, (byte)0x90);
            jump[0] = 0xE9;
            BitConverter.GetBytes(checked((int)(stub - (at + 5)))).CopyTo(jump, 1);
            GameImage.WriteCode(at, jump);
            Sites.Add((name, at, stub));
        }
    }

    private static void JumpTo(List<byte> code, nint target)
    {
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);
        code.AddRange(BitConverter.GetBytes((long)target));
    }

    private static void Finish(List<byte> code, byte* stub, string what)
    {
        if (code.Count > StubSize)
            throw new InvalidOperationException($"riding's {what} stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>A job test (al the job): 0x5E–0x60 or 0x95 → <paramref name="inside"/>, else <paramref name="outside"/>.</summary>
    internal static void WriteJobStub(byte* stub, nint inside, nint outside)
    {
        var code = new List<byte>
        {
            0x3C, WhiteChocobo,  // cmp al, 0x95
            0x74, 0x14,          // je in
            0x2C, 0x5E,          // sub al, 0x5E
            0x3C, 0x02,          // cmp al, 2
            0x76, 0x0E,          // jbe in
        };
        JumpTo(code, outside);
        JumpTo(code, inside);    // in
        Finish(code, stub, "job test");
    }

    /// <summary>
    /// The unit record: another team's free mount (0x20 not set, unlinked) gets 0x20 unless the mover has Pass and Train;
    /// then the game's movzx r8d, bpl and mov dl, r15b.
    /// </summary>
    internal static void WriteBoardingStub(byte* stub, byte* passMover, nint back)
    {
        var code = new List<byte>
        {
            0x40, 0xF6, 0xC5, 0x20,                         // test bpl, 0x20
            0x75, 0x36,                                     // jnz done
            0x40, 0x84, 0xFF,                               // test dil, dil
            0x75, 0x31,                                     // jnz done (the mover's own mount)
            0x41, 0x8A, 0xC7,                               // mov al, r15b
            0x43, 0x32, 0x44, 0x0A, 0x05,                   // xor al, [r10+r9+5]
            0xA8, 0x30,                                     // test al, 0x30
            0x74, 0x25,                                     // jz done (same team)
            0x48, 0xB8,                                     // mov rax, Pass's mover byte
        };
        code.AddRange(BitConverter.GetBytes((long)passMover));
        code.AddRange([0xF6, 0x00, Pass.PassBit]);                          // test byte [rax], 2
        code.AddRange([0x74, 0x12]);                                        // jz block
        code.AddRange([0x49, 0x63, 0xC4]);                                  // movsxd rax, r12d
        code.AddRange([0x48, 0xC1, 0xE0, 0x09]);                            // shl rax, 9
        code.AddRange([0x42, 0xF6, 0x84, 0x08, TrainByte, 0, 0, 0, Train]); // test byte [rax+r9+0x9A], 0x80
        code.AddRange([0x75, 0x04]);                                        // jnz done
        code.AddRange([0x40, 0x80, 0xCD, 0x20]);                            // block: or bpl, 0x20
        code.AddRange([0x44, 0x0F, 0xB6, 0xC5]);                            // done: movzx r8d, bpl
        code.AddRange([0x41, 0x8A, 0xD7]);                                  // mov dl, r15b
        JumpTo(code, back);
        Finish(code, stub, "boarding");
    }

    /// <summary>
    /// A turn start: the scratch registers and xmm0–5 saved, <paramref name="call"/>(the unit: rbx, or rcx + r8), then
    /// the game's store and back. Entered by a jmp from the turn loop's body (16-aligned).
    /// </summary>
    internal static void WriteTurnStartStub(byte* stub, nint call, bool unitInRcxR8, byte[] original, nint back)
    {
        var code = new List<byte>
        {
            0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, // push rax, rcx, rdx, r8–r11
            0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00,                         // sub rsp, 0x88
        };
        for (var k = 0; k < 6; k++)                                            // movdqu [rsp+0x20+16k], xmmk
            code.AddRange([0xF3, 0x0F, 0x7F, (byte)(0x44 + 8 * k), 0x24, (byte)(0x20 + 16 * k)]);
        code.AddRange(unitInRcxR8 ? [0x4C, 0x01, 0xC1] : [0x48, 0x8B, 0xCB]); // add rcx, r8 | mov rcx, rbx
        code.AddRange([0x48, 0xB8]);                                           // mov rax, call
        code.AddRange(BitConverter.GetBytes((long)call));
        code.AddRange([0xFF, 0xD0]);                                           // call rax
        for (var k = 0; k < 6; k++)                                            // movdqu xmmk, [rsp+0x20+16k]
            code.AddRange([0xF3, 0x0F, 0x6F, (byte)(0x44 + 8 * k), 0x24, (byte)(0x20 + 16 * k)]);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);             // add rsp, 0x88
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11–r8, rdx, rcx, rax
        code.AddRange(original);                                               // the game's store
        JumpTo(code, back);
        Finish(code, stub, "turn start");
    }

    [UnmanagedCallersOnly]
    private static void TurnStart(byte* unit)
    {
        try
        {
            Convert(unit);
        }
        catch (Exception e)
        {
            if (Interlocked.Increment(ref _failed) <= 3)
                _game.Log.Error($"riding: the turn start failed: {e}");
        }
    }

    /// <summary>
    /// ReMixed's Train at a rider's turn start (PS1 0x8015E018): a mount on another team under a rider with Train takes
    /// the rider's team bits and Invite. Returns whether it did.
    /// </summary>
    internal static bool Convert(byte* rider)
    {
        var link = rider[Link];
        if ((link & 0x80) == 0 || (rider[TrainByte] & Train) == 0)
            return false;
        var mount = _units + (link & 0x1F) * Battle.UnitSize;
        if (mount[1] == 0xFF || ((mount[Battle.UnitTeam] ^ rider[Battle.UnitTeam]) & 0x30) == 0)
            return false;
        mount[Battle.UnitCurrentTeam] = (byte)((mount[Battle.UnitCurrentTeam] & ~0x30) | (rider[Battle.UnitCurrentTeam] & 0x30));
        mount[Battle.UnitTeam] = (byte)((mount[Battle.UnitTeam] & ~0x30) | (rider[Battle.UnitTeam] & 0x30));
        mount[InviteByte] |= Invite;
        Tracer.Line(_game.Log, $"riding: unit {rider[Battle.UnitIndex]}'s Train wins its mount, unit {mount[Battle.UnitIndex]}");
        return true;
    }
}
