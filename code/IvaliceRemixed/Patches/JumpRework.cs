using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Jump rework (docs/MECHANICS.md: "Jump rework"; BATTLE.BIN 0x80179DC8, the Jump range).
/// Installed by <see cref="Rules"/> with "jump-rework". Data: (range, height) for 0x18A–0x18F.
/// </summary>
/// <remarks>
/// <para>
/// The remaster's Jump range (0x27F270) lists the unit's Jump skillset (0x30E368), then takes the
/// best range and height of the listed 0x18A–0x195 from the Jump table, from 1 and 2 half-steps
/// (8 and 16 for a Mime, job 0x5D). ReMixed starts from 0 (8 for a Mime) and 0x70 half-steps and
/// adds up every listed 0x18A–0x18F: Jump is (3, 99) and each Jump Range +1 (1, 99), so the range
/// is 3 plus one per +1. The height adds up in a byte, as in ReMixed: 54 half-steps with Jump
/// alone, then 252, 194, 136, 78 and 20 as the +1s come. The loop becomes a jump to a stub that asks
/// <see cref="Compute"/>, and since the mod loader can't write the Jump table, ReMixed's values go
/// into the game's.
/// </para>
/// <para>
/// The AI's candidates per skillset (0x316D24) list a Jump skillset as one entry: the last of
/// 0x18A–0x18E the unit learned. Here that's 0x18A alone, so the AI's Jump is Jump whichever +1s are
/// learned. <see cref="DragonTech"/> takes that test over (and keeps the bound).
/// </para>
/// </remarks>
internal static unsafe class JumpRework
{
    private static Game _game = null!;
    private static byte* _table; // (range, height) per ability from 0x18A
    private static int _failed;

    private const int FirstJump = 0x18A, JumpAbilities = 6, Mime = 0x5D, StartHeight = 0x70, MimeRange = 8;
    private const int ListSize = 20; // the function's list: 0x28 bytes at its [rsp+0xC0]
    internal const int StubSize = 192;

    // For the offline check.
    internal static nint LoopAt { get; private set; }
    internal static nint Exit { get; private set; }
    internal static nint MenuBound { get; private set; }
    internal static byte* Table => _table;

    public static void Install(Game game, JsonElement data)
    {
        _game = game;
        var values = data.EnumerateArray().Select(p => (Range: p[0].GetInt32(), Height: p[1].GetInt32())).ToArray();
        if (values.Length != JumpAbilities)
            throw new InvalidDataException($"the Jump data has {values.Length} entries, not {JumpAbilities}");
        var image = game.Image;
        _ = game.TurnCode.Units; // found now, so a missing pattern stops this here
        // The Jump range: copy the 20-byte action record to [rsp+0x48] (its battle index first) ...
        var start = image.FindUnique(
            "BA 14 00 00 00 4C 8B C1 44 8B CA 48 8D 4C 24 48 E8 ?? ?? ?? ?? 0F B6 05 ?? ?? ?? ?? 48 8D 2D ?? ?? ?? ?? " +
            "0F B6 54 24 48 4C 8D 8C 24 A8 00 00 00 48 C1 E2 09");
        // ... list the Jump skillset into [rsp+0xC0], then for each listed 0x18A–0x195 the best range and height.
        var call = image.FindUnique(
            "E8 ?? ?? ?? ?? 4C 63 C8 85 C0 7E ?? 33 C9 41 BA 8A 01 00 00 44 8D 71 01 0F B7 94 4C C0 00 00 00 " +
            "0F B7 C2 66 41 2B C2 66 83 F8 0B 77 ?? 66 41 2B D2 0F B7 C2 0F B6 94 45 ?? ?? ?? ?? 44 8A 84 45");
        var exit = call + 12 + *(sbyte*)(call + 11);
        var table = (byte*)(image.ImageStart + *(int*)(call + 56));
        // The AI's Jump entry: in a type 9 skillset, an ability from 0x18F isn't listed.
        var menu = image.FindUnique("41 80 FC 09 75 ?? 81 FD 8F 01 00 00 73 ?? 83 BC 24 98 00 00 00 01");
        byte[] vanillaTable = [2, 0, 3, 0, 4, 0, 5, 0, 8, 0, 0, 2];
        if ((ulong)(call - start) > 0x120 || !Matches((nint)exit, [0x40, 0x0F, 0xB6, 0xC7, 0x0F, 0xB7, 0xCE, 0x66, 0x2B, 0xC8])
            || (!Matches((nint)table, vanillaTable) && !Matches((nint)table, values.SelectMany(v => new[] { (byte)v.Range, (byte)v.Height }).ToArray())))
            throw new InvalidDataException("the Jump range isn't the code this DLL expects");

        GameImage.MakeWritable((nint)table, 2 * JumpAbilities);
        for (var k = 0; k < JumpAbilities; k++)
        {
            table[2 * k] = (byte)values[k].Range;
            table[2 * k + 1] = (byte)values[k].Height;
        }
        _table = table;

        var stub = GameImage.AllocateCode(StubSize);
        WriteStub(stub, exit);
        var site = call + 5; // movsxd r9, eax; test; jle; xor ecx, ecx; mov r10d, 0x18A: 15 bytes
        var jump = new byte[15];
        Array.Fill(jump, (byte)0xCC);
        AbsoluteJump(jump, 0, (nint)stub);
        GameImage.WriteCode(site, jump);
        GameImage.WriteCode(menu + 8, [0x8B]); // cmp ebp, 0x18B: only Jump itself
        LoopAt = site;
        Exit = exit;
        MenuBound = menu + 8;
    }

    private static bool Matches(nint at, byte[] bytes) => new ReadOnlySpan<byte>((void*)at, bytes.Length).SequenceEqual(bytes);

    /// <summary>
    /// The loop's stub, entered by a jump from the function body (16-aligned) with eax the list's
    /// count: bl = range and dil = height from <see cref="Compute"/>, r9 = the count as the game
    /// leaves it, then on to <paramref name="exit"/>, past the loop.
    /// </summary>
    internal static void WriteStub(byte* stub, nint exit)
    {
        var code = new List<byte>
        {
            0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, // push rax, rcx, rdx, r8–r11
            0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00,                         // sub rsp, 0x88
        };
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x48, 0x8D, 0x8C, 0x24, 0x80, 0x01, 0x00, 0x00]);       // lea rcx, [rsp+0x180]: the function's [rsp+0xC0]
        code.AddRange([0x8B, 0xD0]);                                           // mov edx, eax: the count
        code.AddRange([0x44, 0x0F, 0xB6, 0x84, 0x24, 0x08, 0x01, 0x00, 0x00]); // movzx r8d, byte [rsp+0x108]: its [rsp+0x48]
        code.AddRange([0x48, 0xB8]);                                           // mov rax, Range
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<ushort*, int, int, int>)&Range));
        code.AddRange([0xFF, 0xD0]);                                           // call rax
        code.AddRange([0x88, 0xC3, 0x8B, 0xC8, 0xC1, 0xE9, 0x08, 0x40, 0x88, 0xCF]); // mov bl, al; mov ecx, eax; shr ecx, 8; mov dil, cl
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);            // add rsp, 0x88
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax
        code.AddRange([0x4C, 0x63, 0xC8]);                                     // movsxd r9, eax
        var bytes = code.ToArray();
        var at = bytes.Length;
        Array.Resize(ref bytes, at + 14);
        AbsoluteJump(bytes, at, exit);
        if (bytes.Length > StubSize)
            throw new InvalidOperationException("the Jump range stub is too big");
        bytes.CopyTo(new Span<byte>(stub, StubSize));
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
    private static int Range(ushort* list, int count, int index)
    {
        try
        {
            return Compute(list, count, index);
        }
        catch (Exception e)
        {
            Failed("the Jump range", e);
            return 1 | 2 << 8; // vanilla's start
        }
    }

    /// <summary>ReMixed's range (low byte) and height in half-steps (next byte) for a unit's listed Jump abilities.</summary>
    internal static int Compute(ushort* list, int count, int index)
    {
        var unit = (uint)index < Battle.UnitCount ? _game.TurnCode.Units + index * Battle.UnitSize : null;
        var range = unit != null && unit[3] == Mime ? MimeRange : 0;
        var height = StartHeight;
        for (var k = 0; k < Math.Min(count, ListSize); k++)
        {
            var slot = list[k] - FirstJump;
            if ((uint)slot < JumpAbilities)
            {
                range += _table[2 * slot];
                height += 2 * _table[2 * slot + 1];
            }
        }
        return (range & 0xFF) | (height & 0xFF) << 8;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"jump rework: {what} failed: {e}");
    }
}
