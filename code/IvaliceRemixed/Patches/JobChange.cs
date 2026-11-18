using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's job change keeps a usable secondary (docs/MECHANICS.md: "Job change keeps a useful secondary"; WORLD
/// 0x8013DE00): changing to the job whose skillset is the unit's secondary makes the old job's skillset the secondary,
/// where vanilla leaves the secondary empty. Installed by <see cref="Rules"/> with "job-change-secondary". And the
/// new job's innates come off (below), with "job-change-innates".
/// </summary>
/// <remarks>
/// <para>The party menu's job change (PS1 0x801247E8; the remaster's 0x2888E8: cx the record's index, 0x36 for the job
/// list's preview, dx the job, r8d whether to re-equip) works on the menu's records ([0x1800F50 + 8 × index]: +0x24 the
/// job, +0x54–0x5D the equipment, +0x5E the primary, +0x60 the secondary, +0x62 the reaction, +0x64 the support, +0x66
/// the movement, u16), stores the job, writes the records back to the roster and rebuilds them, and in that rebuild
/// clears a secondary equal to the primary (0x288B5E). The hook changes the secondary before the game stores the job,
/// as ReMixed does, while +0x24 still holds the old job. A unit with no secondary keeps none (ReMixed's code doesn't
/// test for that; no job it can change to has skillset 0).</para>
/// <para>ReMixed's innate step (WORLD 0x80124B08–0x80124B94, in the record's rebuild after the job change): an equipped
/// reaction, support or movement ability (+0x62, +0x64, +0x66) that's one of the new job's innates comes off, where
/// vanilla cleared four fixed pairs, all supports (Chemist with Throw Item, Monk with Martial Arts, Mediator with
/// Monster Talk, Ninja with Two Swords). ReMixed reads five ids from the job's record (PS1 0x800610B8 + 48 × job, +1
/// on): the four innates, then bytes +9 and +10, the first equip-type bytes, as a fifth (a quirk, kept; with ReMixed's
/// data it only matters for job 0x26, whose fifth is 0x1B8, the Reflect reaction). The remaster's rebuild (0x288A24,
/// from the job change and 0x28916C) asks the same fixed list for the support only (0x289F4C, which also has its own
/// job 0x0F), and its 28 bytes at 0x288B42 jump to a stub that applies ReMixed's rule instead.</para>
/// </remarks>
internal static unsafe class JobChange
{
    private const int Job = 0x24, Secondary = 0x60;
    private const int Reaction = 0x62, Movement = 0x66, InnateIds = 5; // the support is +0x64
    internal const int InnateStubSize = 192;

    private static Game _game = null!;
    private static nint* _records;
    private static int _failed;

    // For the offline check.
    internal static nint InnatesAt { get; private set; }
    internal static nint InnatesBack { get; private set; }

    /// <summary>The game's job change (0x2888E8's original). The offline check swaps it.</summary>
    internal static delegate* unmanaged<int, int, int, int> GameChange;
    internal static nint ChangeAt { get; private set; }
    internal static nint* Records => _records;

    public static void Install(Game game)
    {
        _game = game;
        _ = game.Units.Jobs; // found now, so a missing pattern stops this rule here
        var body = game.Image.FindUnique(
            "48 0F BF F1 4C 8D 35 ?? ?? ?? ?? 41 8B E8 48 8B FE 49 8B 04 F6 48 8B C8 80 B8 77 01 00 00 FF 74 ?? " +
            "66 39 50 24 74 ?? C6 80 77 01 00 00 FF 49 8B 0C F6 45 33 E4 66 89 51 24");
        var start = body - 0x27;
        GameImage.Calls(start, "48 89 5C 24 10 48 89 6C 24 18 48 89 74 24 20 57 41 54 41 56 48 83 EC 40", "the job change");
        _records = (nint*)GameImage.Rip(body + 4, 3, 7);
        GameChange = (delegate* unmanaged<int, int, int, int>)game.Hooks.Hook(
            start, (nint)(delegate* unmanaged<int, int, int, int>)&Change, "the job change");
        ChangeAt = start;
    }

    [UnmanagedCallersOnly]
    private static int Change(int index, int job, int reequip)
    {
        try
        {
            Keep((short)index, (ushort)job);
        }
        catch (Exception e)
        {
            Failed(e);
        }
        return GameChange(index, job, reequip);
    }

    /// <summary>ReMixed's innate step in the record's rebuild, in place of the game's support check.</summary>
    public static void InstallInnates(Game game)
    {
        _game = game;
        _ = game.Units.Jobs; // found now, so a missing pattern stops this rule here
        // The rebuild's support check: the support (+0x64) and the job (+0x24) to the fixed list, cleared on a match.
        var site = game.Image.FindUnique(
            "41 0F B7 50 64 41 0F B7 48 24 E8 ?? ?? ?? ?? 84 C0 74 09 66 45 89 70 64 4D 8B 04 EF 41 0F B7 40 60");
        var check = (byte*)GameImage.Rip(site + 10, 1, 5);
        ReadOnlySpan<byte> pairs = [0x66, 0x83, 0xF9, 0x0F, 0x74, 0x3A, 0x66, 0x83, 0xF9, 0x4B, 0x74, 0x27];
        if (!new ReadOnlySpan<byte>(check, pairs.Length).SequenceEqual(pairs))
            throw new InvalidDataException("the rebuild's support check isn't the code this DLL expects");
        var stub = GameImage.AllocateCode(InnateStubSize);
        WriteInnateStub(stub, site + 28);
        var jump = new byte[28];
        Array.Fill(jump, (byte)0x90);
        AbsoluteJump(jump, 0, (nint)stub);
        GameImage.WriteCode(site, jump);
        InnatesAt = site;
        InnatesBack = site + 28;

        // The ability lists' innate check (WORLD 0x80122D6C in 0x80122C20; the remaster's 0x289F4C, from the list
        // builders 0x286B73 and 0x28A9E2): an ability innate to the current job is listed as one that can't be
        // equipped. ReMixed asks the job's five ids where vanilla had its fixed pairs; the check jumps to a stub that
        // does. Its callers keep r8–r11, so the stub uses rax, rcx and rdx only.
        var listStub = GameImage.AllocateCode(ListCheckStubSize);
        WriteListCheckStub(listStub, game.Units.Jobs + 1);
        var listJump = new byte[14];
        AbsoluteJump(listJump, 0, (nint)listStub);
        GameImage.WriteCode((nint)check, listJump);
        (ListCheck, ListCheckStub) = ((nint)check, (nint)listStub);
    }

    internal const int ListCheckStubSize = 80;
    /// <summary>For the offline check: the remaster's innate check (now jumping to the stub) and the stub.</summary>
    internal static nint ListCheck { get; private set; }
    internal static nint ListCheckStub { get; private set; }

    /// <summary>
    /// The ability lists' check (cx the job, dx the ability; al 1 when it's one of the job's five ids, ReMixed's
    /// 0x80122D6C): no ability and a job past the table are 0.
    /// </summary>
    internal static void WriteListCheckStub(byte* stub, byte* firstInnate)
    {
        var code = new List<byte>
        {
            0x0F, 0xB7, 0xC2,                   // movzx eax, dx
            0x85, 0xC0,                         // test eax, eax
            0x74, 0,                            // jz none
        };
        var toNone1 = code.Count;
        code.AddRange([0x0F, 0xB7, 0xC9]);                                   // movzx ecx, cx
        code.AddRange([0x81, 0xF9]);                                         // cmp ecx, JobCount
        code.AddRange(BitConverter.GetBytes(UnitTables.JobCount));
        code.AddRange([0x73, 0]);                                            // jae none
        var toNone2 = code.Count;
        code.AddRange([0x48, 0x6B, 0xC9, UnitTables.JobSize]);               // imul rcx, rcx, JobSize
        code.AddRange([0x48, 0xBA]);                                         // mov rdx, the first innate
        code.AddRange(BitConverter.GetBytes((long)firstInnate));
        code.AddRange([0x48, 0x01, 0xD1]);                                   // add rcx, rdx
        var toOne = new List<int>();
        for (var k = 0; k < InnateIds; k++)
        {
            if (k == 0)
                code.AddRange([0x66, 0x3B, 0x01]);                           // cmp ax, [rcx]
            else
                code.AddRange([0x66, 0x3B, 0x41, (byte)(2 * k)]);            // cmp ax, [rcx+2k]
            code.AddRange([0x74, 0]);                                        // je one
            toOne.Add(code.Count);
        }
        var none = code.Count;
        code.AddRange([0x32, 0xC0, 0xC3]);                                   // none: xor al, al; ret
        var one = code.Count;
        code.AddRange([0xB0, 0x01, 0xC3]);                                   // one: mov al, 1; ret
        code[toNone1 - 1] = checked((byte)(none - toNone1));
        code[toNone2 - 1] = checked((byte)(none - toNone2));
        foreach (var at in toOne)
            code[at - 1] = checked((byte)(one - at));
        if (code.Count > ListCheckStubSize)
            throw new InvalidOperationException("the ability lists' check stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>
    /// The rebuild's stub, entered by a jump from its body (16-aligned) with the record in r8: save the scratch
    /// registers, call <see cref="Innates"/>, restore, reload r8 from the records ([r15 + rbp × 8]) as the game's
    /// clearing path does, and go on to <paramref name="back"/>.
    /// </summary>
    internal static void WriteInnateStub(byte* stub, nint back)
    {
        var code = new List<byte>
        {
            0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, // push rax, rcx, rdx, r8–r11
            0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00,                         // sub rsp, 0x88
        };
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x4C, 0x89, 0xC1]);                                     // mov rcx, r8: the record
        code.AddRange([0x48, 0xB8]);                                           // mov rax, Innates
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<byte*, void>)&Innates));
        code.AddRange([0xFF, 0xD0]);                                           // call rax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);            // add rsp, 0x88
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax
        code.AddRange([0x4D, 0x8B, 0x04, 0xEF]);                               // mov r8, [r15+rbp*8]
        var bytes = code.ToArray();
        var at = bytes.Length;
        Array.Resize(ref bytes, at + 14);
        AbsoluteJump(bytes, at, back);
        if (bytes.Length > InnateStubSize)
            throw new InvalidOperationException("the innate stub is too big");
        bytes.CopyTo(new Span<byte>(stub, InnateStubSize));
    }

    /// <summary>jmp [rip+0] with the target after it.</summary>
    private static void AbsoluteJump(byte[] code, int at, nint target)
    {
        code[at] = 0xFF;
        code[at + 1] = 0x25;
        BitConverter.TryWriteBytes(code.AsSpan(at + 2), 0);
        BitConverter.TryWriteBytes(code.AsSpan(at + 6), (ulong)target);
    }

    [UnmanagedCallersOnly]
    private static void Innates(byte* record)
    {
        try
        {
            ClearInnates(record);
        }
        catch (Exception e)
        {
            Failed(e);
        }
    }

    /// <summary>ReMixed's rule: the reaction, support and movement that are one of the job's five ids come off.</summary>
    internal static void ClearInnates(byte* record)
    {
        if (record == null)
            return;
        int job = *(ushort*)(record + Job);
        if ((uint)job >= UnitTables.JobCount)
            return;
        var data = _game.Units.Jobs + job * UnitTables.JobSize;
        for (var slot = Reaction; slot <= Movement; slot += 2)
        {
            var id = *(ushort*)(record + slot);
            if (id == 0)
                continue;
            for (var k = 0; k < InnateIds; k++)
                if (*(ushort*)(data + 1 + 2 * k) == id)
                {
                    *(ushort*)(record + slot) = 0;
                    Tracer.Line(_game.Log, $"job change: job 0x{job:X2}'s innate 0x{id:X3} comes off (+0x{slot:X2})");
                    break;
                }
        }
    }

    /// <summary>ReMixed's rule on a menu record, before the game stores the new job.</summary>
    internal static void Keep(int index, int job)
    {
        if ((uint)index > 0x36)
            return;
        var record = (byte*)_records[index];
        if (record == null)
            return;
        var units = _game.Units;
        int oldJob = *(ushort*)(record + Job), secondary = *(ushort*)(record + Secondary);
        if (job == oldJob || secondary == 0 || job > 0xFF || oldJob > 0xFF || (byte)secondary != units.SkillsetOf(job))
            return;
        var kept = units.SkillsetOf(oldJob);
        *(ushort*)(record + Secondary) = (ushort)kept;
        Tracer.Line(_game.Log, $"job change: record {index}, job 0x{oldJob:X2} to 0x{job:X2}: the secondary 0x{secondary:X2} becomes 0x{kept:X2}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"job change: the secondary rule failed: {e}");
    }
}
