using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// Limit's faster charge in the ability lists (docs/MECHANICS.md: "Limit charges faster when
/// hurt"; PS1 0x8015D480, from 0x80181A6C in the list builder behind the action menu and its
/// turn-order preview). Installed by <see cref="Rules"/> with "limit-charge", after the action
/// queue's hook, which cuts the real charge.
/// </summary>
/// <remarks>
/// The remaster has two copies of the PS1's list builder: 0x30DD8C (one caller) and 0x30E368
/// (the menus and the AI). Each reads an ability's CT (+0xC &amp; 0x7F), skips the rest for a
/// no-charge ability, then halves it for Short Charge or zeroes it for Non-charge, hands it to the
/// turn-order preview (0x30E2F0) and keeps it in the list. ReMixed cuts the Limit skillset's CT
/// just before Short Charge, as in the queue. Each copy's Short Charge test becomes a jump to a
/// stub that asks <see cref="Charge"/> first, then runs the test. ReMixed's same spot also zeroes
/// the CT of a type 5 skillset, which comes with that skillset type.
/// </remarks>
internal static unsafe class LimitPreview
{
    private static Game _game = null!;
    private static int _failed;

    internal const int StubSize = 192;

    /// <summary>A patched test: where, how long, where its function keeps the skillset, and where it goes on.</summary>
    internal readonly record struct Site(string Name, nint At, int Length, bool SkillsetInR15, nint Done, nint NonCharge);

    /// <summary>For the offline check: the patched tests.</summary>
    internal static readonly List<Site> Sites = [];

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        _ = game.TurnCode.Units; // found now, so a missing pattern stops this here
        // The first copy keeps the battle index in [rsp+0x28] and the skillset in [rsp+0x21];
        // the second the index in [rsp+0x28] and the skillset in r15b.
        var firstStart = image.FindUnique(
            "8B C1 89 4C 24 28 49 8B F1 48 8B 8C 24 80 01 00 00 4D 8B F8 48 89 4C 24 30 45 33 E4 " +
            "48 8B 8C 24 88 01 00 00 48 89 4C 24 38 8B C8 88 54 24 21 E8");
        var secondStart = image.FindUnique("44 0F B6 FA 89 4C 24 28 89 7C 24 24 E8");
        // No-charge? Short Charge (r14 = 1): (CT + 1) / 2; Non-charge (2): 0. Then the preview.
        var first = image.FindUnique(
            "F6 C1 04 75 18 B8 01 00 00 00 4C 3B F0 75 06 FF C3 D1 EB EB 08 49 83 FE 02 41 0F 44 DB " +
            "8B 4C 24 28 4C 8D 44 24 50 45 33 C9 8B D3 E8");
        // The same with r12, and the Non-charge test out of line.
        var second = image.FindUnique("F6 C1 04 75 5F B8 01 00 00 00 4C 3B E0 75 4D FF C3 D1 EB EB 4F");
        var secondDone = second + 0x64;
        var secondNonCharge = second + 0x5C;
        if ((ulong)(first - firstStart) > 0x200 || (ulong)(second - secondStart) > 0x200
            || !Matches(secondNonCharge, [0x49, 0x83, 0xFC, 0x02, 0x41, 0x0F, 0x44, 0xDB])
            || !Matches(secondDone, [0x8B, 0x4C, 0x24, 0x28, 0x4C, 0x8D, 0x44, 0x24, 0x60, 0x45, 0x33, 0xC9, 0x8B, 0xD3, 0xE8])
            || GameImage.Rip(first + 43, 1, 5) != GameImage.Rip(secondDone + 14, 1, 5))
            throw new InvalidDataException("the ability lists' charge time isn't the code this DLL expects");

        Site[] sites =
        [
            new("the first ability list", first + 5, 24, false, first + 29, 0),
            new("the second ability list", second + 5, 16, true, secondDone, secondNonCharge),
        ];
        var stubs = GameImage.AllocateCode(sites.Length * StubSize);
        for (var k = 0; k < sites.Length; k++)
            WriteStub(stubs + k * StubSize, sites[k]);
        foreach (var (site, k) in sites.Select((s, k) => (s, k)))
        {
            var jump = new byte[site.Length];
            Array.Fill(jump, (byte)0xCC);
            AbsoluteJump(jump, 0, (nint)(stubs + k * StubSize));
            GameImage.WriteCode(site.At, jump);
            Sites.Add(site);
        }
    }

    private static bool Matches(nint at, byte[] bytes) => new ReadOnlySpan<byte>((void*)at, bytes.Length).SequenceEqual(bytes);

    /// <summary>
    /// A test's stub, entered by a jump from the function body (16-aligned), with the CT in ebx:
    /// save the scratch registers, ebx = <see cref="Charge"/>(index, skillset, ebx), restore, then
    /// the game's Short Charge test and on to <see cref="Site.Done"/> (or the Non-charge test).
    /// </summary>
    internal static void WriteStub(byte* stub, Site site)
    {
        var code = new List<byte>
        {
            0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, // push rax, rcx, rdx, r8–r11
            0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00,                         // sub rsp, 0x88
        };
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x8B, 0x8C, 0x24, 0xE8, 0x00, 0x00, 0x00]);             // mov ecx, [rsp+0xE8]: the function's [rsp+0x28]
        if (site.SkillsetInR15)
            code.AddRange([0x41, 0x0F, 0xB6, 0xD7]);                          // movzx edx, r15b
        else
            code.AddRange([0x0F, 0xB6, 0x94, 0x24, 0xE1, 0x00, 0x00, 0x00]);  // movzx edx, byte [rsp+0xE1]: its [rsp+0x21]
        code.AddRange([0x44, 0x8B, 0xC3]);                                    // mov r8d, ebx
        code.AddRange([0x48, 0xB8]);                                          // mov rax, Charge
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<int, int, int, int>)&Charge));
        code.AddRange([0xFF, 0xD0]);                                          // call rax
        code.AddRange([0x8B, 0xD8]);                                          // mov ebx, eax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);           // add rsp, 0x88
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax
        var bytes = code.ToArray();
        if (site.NonCharge == 0)
        {
            // The first copy's test, as it was: its short jumps stay inside it or land on its end.
            bytes = [.. bytes, .. FirstTest, .. new byte[14]];
            AbsoluteJump(bytes, bytes.Length - 14, site.Done);
        }
        else
        {
            // mov eax, 1; cmp r12, rax; jne the Non-charge test; inc ebx; shr ebx, 1; on to the preview.
            byte[] test = [0xB8, 0x01, 0x00, 0x00, 0x00, 0x4C, 0x3B, 0xE0, 0x75, 0x12, 0xFF, 0xC3, 0xD1, 0xEB];
            bytes = [.. bytes, .. test, .. new byte[28]];
            AbsoluteJump(bytes, bytes.Length - 28, site.Done);
            AbsoluteJump(bytes, bytes.Length - 14, site.NonCharge);
        }
        if (bytes.Length > StubSize)
            throw new InvalidOperationException("the charge time stub is too big");
        bytes.CopyTo(new Span<byte>(stub, StubSize));
    }

    /// <summary>
    /// The first copy's Short Charge test: mov eax, 1; cmp r14, rax; jne +6; inc ebx; shr ebx, 1;
    /// jmp +8; cmp r14, 2; cmove ebx, r11d (r11d is 0 there).
    /// </summary>
    private static readonly byte[] FirstTest =
        [0xB8, 0x01, 0x00, 0x00, 0x00, 0x4C, 0x3B, 0xF0, 0x75, 0x06, 0xFF, 0xC3, 0xD1, 0xEB, 0xEB, 0x08, 0x49, 0x83, 0xFE, 0x02, 0x41, 0x0F, 0x44, 0xDB];

    /// <summary>jmp [rip+0] with the target after it.</summary>
    private static void AbsoluteJump(byte[] code, int at, nint target)
    {
        code[at] = 0xFF;
        code[at + 1] = 0x25;
        BitConverter.TryWriteBytes(code.AsSpan(at + 2), 0);
        BitConverter.TryWriteBytes(code.AsSpan(at + 6), (ulong)target);
    }

    /// <summary>The CT a list shows for an ability of <paramref name="skillset"/>: Limit's cut by the unit's HP.</summary>
    [UnmanagedCallersOnly]
    internal static int Charge(int index, int skillset, int ct)
    {
        try
        {
            if (skillset != Rules.LimitSkillset || (uint)index >= Battle.UnitCount)
                return ct;
            var unit = _game.TurnCode.Units + index * Battle.UnitSize;
            if (unit[1] == 0xFF)
                return ct;
            return Rules.LimitCharge(ct, *(ushort*)(unit + Battle.UnitHp), *(ushort*)(unit + Battle.UnitMaxHp));
        }
        catch (Exception e)
        {
            Failed("the charge time", e);
            return ct;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"limit preview: {what} failed: {e}");
    }
}
