using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's cone-shaped area (docs/MECHANICS.md: "Cone areas"; PS1 0x8017A7BC, 0x8017BE68's tile loop, the area
/// builder's hooks 0x8017B910 → 0x8017C030 and 0x8017BB24): an ability with both Linear and 3 Directions (attribute
/// +4 &amp; 6 is 6) hits a cone from the caster toward the target. The Breaths, Elemental Wave and two sword skills.
/// Installed by <see cref="Rules"/> with "cone-area".
/// </summary>
/// <remarks>
/// <para>The area builder (PS1 0x8017B874, the remaster's 0x280B40) centres a Linear or 3 Directions area on the caster,
/// marks a diamond of the area's size (0x27EBF8) with the height test (0x27EA18), and then vanilla keeps the line
/// toward the target (0x281014 with 1) and, for 3 Directions, the lines to the sides as well (with 3). With both bits it
/// runs both, which leaves only the front line: a Breath (area 2) hits 2 tiles. ReMixed marks a diamond of twice the
/// area, then keeps the tiles within the area's Chebyshev distance, off the exact diagonals (for an area of 2 or more),
/// whose side of the caster matches the direction to the target (along x when |dx| ≥ |dy|): area 1 the 3 tiles in
/// front, area 2 four tiles ((1, 0), (2, 0), (2, ±1)), area 3 nine. Kept tiles get byte 0 = 1, the others bytes 0
/// and 1 cleared, and the sort flag is set (PS1 0x8018F4F8, here [0x11A7024], as the lines routine sets it).</para>
/// <para>Two stubs in the builder. The diamond (0x27EBF8) is a flood fill from the centre tiles, whose mark (byte 0,
/// the area + 1) bounds it, cl the number of steps: for both bits the stub at 0x280D0B doubles both, as ReMixed
/// doubles the area before the marks. And the lines' calls (0x280DD3) go to <see cref="Cone"/> for both bits; anything
/// else runs the game's code. Every area goes through this
/// builder (one caller, the area dispatcher 0x27FFE8), so the AI's plans get the cone too. For Linear or 3 Directions
/// alone ReMixed's carve gives vanilla's lines, so those stay the game's.</para>
/// <para>The targeting (PS1 0x8017A300 and 0x8017C000 in the range builder 0x8017A290, its carve 0x8017BE68; the
/// remaster's builder 0x27F974): for a range with flag 0x10 (the record's +3), where vanilla makes the caster's row and
/// column selectable, ReMixed marks a diamond of twice the range and then makes selectable (byte 1) the tiles of it within
/// the range's square, off the exact diagonals (so never the caster's own tile), and on the caster's row or column unless
/// the ability has both Linear and 3 Directions: those can aim at any tile in the square. Two more stubs: the diamond's
/// call (0x27FAB5) doubles its steps and the caster's marks, and the cross (0x27FAD8) goes to <see cref="RangeCarve"/>.
/// The builder has one caller (0x27FFB6), so the AI aims the same way.</para>
/// <para>The target order (PS1 0x8017D8C4 in the area's unit list 0x8017D850; the remaster's 0x282754): while the sort
/// flag is set, units are ordered by 16 × the larger of |dx| and |dy| plus the smaller (Chebyshev rings) instead of
/// |dx| + |dy|; a stub at 0x28281C. Lines are ordered the same either way, so only the cone's order changes.</para>
/// </remarks>
internal static unsafe class ConeArea
{
    public const int Linear = 0x04, ThreeDirections = 0x02, Both = Linear | ThreeDirections;
    public const int TileSize = 5, Tiles = 512;
    public const int DoubleStubSize = 48, LinesStubSize = 96;
    public const int RangeDoubleStubSize = 48, RangeCarveStubSize = 64, RingStubSize = 48;
    /// <summary>The range record's flag (+3) for vanilla's cross, which ReMixed's carve replaces.</summary>
    public const int CrossRange = 0x10;

    private static Game _game = null!;
    private static byte* _units;
    private static int _failed;

    /// <summary>The builder's map of tiles (5 bytes each, two layers of 256; byte 0 in the area). (0x18004E0)</summary>
    internal static byte* Map { get; private set; }
    /// <summary>The battle map's width. (0xC6AD6A)</summary>
    internal static byte* Width { get; private set; }
    /// <summary>The sort flag the lines routine sets (PS1 0x8018F4F8). (0x11A7024)</summary>
    internal static int* Sorted { get; private set; }
    /// <summary>For the offline check: the builder, its terrain table (8 bytes a tile) and the two sites and stubs.</summary>
    internal static nint Builder { get; private set; }
    internal static byte* Terrain { get; private set; }
    internal static nint DoubleAt { get; private set; }
    internal static nint LinesAt { get; private set; }
    internal static nint DoubleStub { get; private set; }
    internal static nint LinesStub { get; private set; }
    /// <summary>For the offline check: the range builder, its two sites and stubs, and the ring order's site and stub.</summary>
    internal static nint RangeBuilder { get; private set; }
    internal static nint RangeDoubleAt { get; private set; }
    internal static nint RangeCarveAt { get; private set; }
    internal static nint RingAt { get; private set; }
    internal static nint RingStub { get; private set; }

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // The diamond: mov [r9+rbx], r8b; mov cl, r14b (the area); mov [rdx], r8b; call the diamond; the height test.
        var diamond = image.FindUnique(
            "45 88 04 19 41 8A CE 44 88 02 E8 ?? ?? ?? ?? 44 0F B6 C7 41 8A D4 41 C1 E8 05 40 8A CE 41 83 E0 01 E8");
        // The lines: test dil, 4 (Linear); je; lines(record, 1); test dil, 2 (3 Directions); je; lines(record, 3).
        var lines = image.FindUnique(
            "40 F6 C7 04 74 0C B2 01 48 8D 4C 24 20 E8 ?? ?? ?? ?? 40 F6 C7 02 74 0C B2 03 48 8D 4C 24 20 E8 ?? ?? ?? ?? " +
            "40 F6 C7 C0");
        var mapLoad = image.FindUnique("48 8D 1D ?? ?? ?? ?? 40 02 F6 40 02 F0");
        var widthLoad = image.FindUnique("44 0F B6 0D ?? ?? ?? ?? 41 3B D1 0F 83");
        var terrainTest = image.FindUnique("F6 84 D3 ?? ?? ?? ?? 01 0F 85 ?? ?? ?? ?? 8A 84 D3");
        // The builder's start, before all of them: its prologue and the record's copy.
        var builder = image.FindUnique(
            "48 89 5C 24 10 48 89 6C 24 18 56 57 41 54 41 55 41 56 48 83 EC 40 48 8B 05 ?? ?? ?? ?? 48 33 C4 " +
            "48 89 44 24 38 BA 14 00 00 00 4C 8B C1 44 8B CA 48 8D 4C 24 20 E8");
        foreach (var at in new[] { widthLoad, terrainTest, mapLoad, diamond, lines })
            if ((ulong)(at - builder) > 0x400)
                throw new InvalidDataException("the area builder isn't laid out as this DLL expects");
        var routine = GameImage.Rip(lines + 13, 1, 5);
        if (GameImage.Rip(lines + 31, 1, 5) != routine)
            throw new InvalidDataException("the area builder's two line calls don't go to one routine");
        GameImage.Calls(routine,
            "48 89 5C 24 10 48 89 6C 24 18 56 57 41 56 48 83 EC 40 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 38 " +
            "40 8A F2 4C 8B C1 BA 14 00 00 00 48 8D 4C 24 20 44 8B CA E8 ?? ?? ?? ?? 0F B6 44 24 20 48 8D 0D ?? ?? ?? ?? " +
            "44 0F BF 74 24 2C 0F BF 6C 24 30 48 C1 E0 09 C7 05 ?? ?? ?? ?? 01 00 00 00 0F B6 7C 08 4F 0F B6 5C 08 50",
            "the lines routine");
        _units = (byte*)GameImage.Rip(routine + 0x3E, 3, 7);
        if (_units != game.TurnCode.Units)
            throw new InvalidDataException("the lines routine's units aren't the battle's");
        Sorted = (int*)GameImage.Rip(routine + 0x54, 2, 10);
        Map = (byte*)GameImage.Rip(mapLoad, 3, 7);
        Width = (byte*)GameImage.Rip(widthLoad, 4, 8);
        Terrain = (byte*)(image.ImageStart + *(int*)(terrainTest + 3) - 6);

        var doubleAt = diamond;
        var doubleStub = GameImage.AllocateCodeNear(doubleAt, DoubleStubSize);
        WriteDoubleStub(doubleStub, doubleAt + 10);
        var linesStub = GameImage.AllocateCodeNear(lines, LinesStubSize);
        WriteLinesStub(linesStub, (nint)(delegate* unmanaged<byte*, int, void>)&Carve, routine, lines + 18, lines + 36);
        // The range builder (one caller, 0x27FFB6). Its start: the prologue and the record's copy at rsp+0x30.
        var rangeBuilder = image.FindUnique(
            "48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 8B 05 ?? ?? ?? ?? 48 33 C4 " +
            "48 89 44 24 48 BA 14 00 00 00 4C 8B C1 44 8B CA 48 8D 4C 24 30 E8");
        // The caster: rbp = the units + record[0] × 0x200.
        var casterLoad = image.FindUnique("0F B6 6C 24 30 48 8D 05 ?? ?? ?? ?? 0F BF 4C 24 32 48 C1 E5 09 48 03 E8");
        // The caster's marks (eax = range + 1 into [rdi] and [r14], both layers), the 0x20 branch, then the diamond's call.
        var marks = image.FindUnique(
            "41 8D 44 24 01 4B 8D 3C 80 49 03 FA 4F 8D 34 80 4C 03 F2 88 07 41 88 06 C6 47 01 01 40 F6 C6 20 74 ?? " +
            "48 8B CD E8 ?? ?? ?? ?? C6 07 00 41 C6 06 00 EB ?? 41 8A CC E8");
        // The cross: test sil, 0x10; je past it; movzx r8d, r11b (the width); lea edx, [r15-0x1F].
        var cross = image.FindUnique("40 F6 C6 10 0F 84 ?? ?? ?? ?? 45 0F B6 C3 41 8D 57 E1");
        foreach (var at in new[] { casterLoad, marks, cross })
            if ((ulong)(at - rangeBuilder) > 0x200)
                throw new InvalidDataException("the range builder isn't laid out as this DLL expects");
        if ((byte*)GameImage.Rip(casterLoad + 5, 3, 7) != _units)
            throw new InvalidDataException("the range builder's units aren't the battle's");
        var rangeDoubleAt = marks + 51;
        if (GameImage.Rip(rangeDoubleAt + 3, 1, 5) != GameImage.Rip(diamond + 10, 1, 5))
            throw new InvalidDataException("the range builder's diamond isn't the area builder's");
        var pastCross = GameImage.Rip(cross + 4, 2, 6);

        // The area's unit list: while the sort flag is set, the key |dy| + |dx| (xor r8d, r8d; add esi, edx).
        var key = image.FindUnique(
            "83 3D ?? ?? ?? ?? 00 74 ?? 0F B6 75 01 41 8B C6 0F B6 55 00 2B C6 41 2B F6 41 8B CC 2B CA 85 C0 0F 49 F0 " +
            "41 2B D4 85 C9 0F 49 D1 45 33 C0 03 F2");
        if ((int*)GameImage.Rip(key, 2, 7) != Sorted)
            throw new InvalidDataException("the unit list's sort flag isn't the lines routine's");
        var ringAt = key + 43;

        var rangeDoubleStub = GameImage.AllocateCodeNear(rangeDoubleAt, RangeDoubleStubSize);
        WriteRangeDoubleStub(rangeDoubleStub, GameImage.Rip(rangeDoubleAt + 3, 1, 5), rangeDoubleAt + 8);
        var rangeCarveStub = GameImage.AllocateCodeNear(cross, RangeCarveStubSize);
        WriteRangeCarveStub(rangeCarveStub, (nint)(delegate* unmanaged<byte*, int, void>)&RangeCarveEntry, (nint)Width,
            pastCross);
        var ringStub = GameImage.AllocateCodeNear(ringAt, RingStubSize);
        WriteRingStub(ringStub, ringAt + 5);

        WriteJump(doubleAt, (nint)doubleStub, 10);
        WriteJump(lines, (nint)linesStub, 8);
        WriteJump(rangeDoubleAt, (nint)rangeDoubleStub, 8);
        WriteJump(cross, (nint)rangeCarveStub, 10);
        WriteJump(ringAt, (nint)ringStub, 5);
        (Builder, DoubleAt, LinesAt, DoubleStub, LinesStub) = (builder, doubleAt, lines, (nint)doubleStub, (nint)linesStub);
        (RangeBuilder, RangeDoubleAt, RangeCarveAt, RingAt, RingStub) =
            (rangeBuilder, rangeDoubleAt, cross, ringAt, (nint)ringStub);
    }

    /// <summary>
    /// The range diamond's stub, in place of mov cl, r12b (the range) and the call: for a range with flag 0x10 (sil), the
    /// steps and the caster's marks in both layers ([rdi], [r14]) become 2 × range and 2 × range + 1, as ReMixed doubles
    /// the range before marking (PS1 0x8017C00C). Then the call, and back.
    /// </summary>
    internal static void WriteRangeDoubleStub(byte* stub, nint diamond, nint back)
    {
        var code = new List<byte>
        {
            0x41, 0x8A, 0xCC,             // mov cl, r12b
            0x40, 0xF6, 0xC6, CrossRange, // test sil, 0x10
            0x74, 0x0A,                   // jz call
            0x00, 0xC9,                   // add cl, cl
            0x8D, 0x41, 0x01,             // lea eax, [rcx+1]
            0x88, 0x07,                   // mov [rdi], al
            0x41, 0x88, 0x06,             // mov [r14], al
            0xFF, 0x15, 0x06, 0, 0, 0,    // call: call [rip+6] → diamond
            0xFF, 0x25, 0x08, 0, 0, 0,    // jmp [rip+8] → back
        };
        code.AddRange(BitConverter.GetBytes((long)diamond));
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > RangeDoubleStubSize)
            throw new InvalidOperationException("the range diamond's stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>
    /// The cross's stub: for a range with flag 0x10, <paramref name="carve"/>(the record copy at rsp+0x30, the range r12b),
    /// and r11b (the width, which the code after the cross expects) reloaded; then past the cross either way.
    /// Entered by a jmp: rsp is the builder's, 16-aligned, with its shadow space free.
    /// </summary>
    internal static void WriteRangeCarveStub(byte* stub, nint carve, nint width, nint past)
    {
        var code = new List<byte>
        {
            0x40, 0xF6, 0xC6, CrossRange, // test sil, 0x10
            0x74, 0x23,                   // jz past
            0x48, 0x8D, 0x4C, 0x24, 0x30, // lea rcx, [rsp+0x30]
            0x41, 0x0F, 0xB6, 0xD4,       // movzx edx, r12b
            0x48, 0xB8,                   // mov rax, carve
        };
        code.AddRange(BitConverter.GetBytes((long)carve));
        code.AddRange([0xFF, 0xD0]);                     // call rax
        code.AddRange([0x48, 0xB8]);                     // mov rax, width
        code.AddRange(BitConverter.GetBytes((long)width));
        code.AddRange([0x44, 0x0F, 0xB6, 0x18]);         // movzx r11d, byte [rax]
        if (code.Count != 6 + 0x23)
            throw new InvalidOperationException("the range carve stub's jump is off");
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);         // past: jmp [rip+0] → past
        code.AddRange(BitConverter.GetBytes((long)past));
        if (code.Count > RangeCarveStubSize)
            throw new InvalidOperationException("the range carve stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>
    /// The ring order's stub, in place of xor r8d, r8d and add esi, edx (esi |dy|, edx |dx|): esi = 16 × the larger plus
    /// the smaller (PS1 0x8017D8C4), then xor r8d, r8d and back. eax and ecx are free there.
    /// </summary>
    internal static void WriteRingStub(byte* stub, nint back)
    {
        var code = new List<byte>
        {
            0x8B, 0xC6,             // mov eax, esi
            0x8B, 0xCA,             // mov ecx, edx
            0x3B, 0xC1,             // cmp eax, ecx
            0x73, 0x02,             // jae larger
            0x87, 0xC1,             // xchg eax, ecx
            0xC1, 0xE0, 0x04,       // larger: shl eax, 4
            0x03, 0xC1,             // add eax, ecx
            0x8B, 0xF0,             // mov esi, eax
            0x45, 0x33, 0xC0,       // xor r8d, r8d
            0xFF, 0x25, 0, 0, 0, 0, // jmp [rip+0] → back
        };
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > RingStubSize)
            throw new InvalidOperationException("the ring order's stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    [UnmanagedCallersOnly]
    private static void RangeCarveEntry(byte* record, int range)
    {
        try
        {
            RangeCarve(record, range);
        }
        catch (Exception e)
        {
            Failed(e);
        }
    }

    /// <summary>ReMixed's range carve (PS1 0x8017BE68 and its loop 0x8017BEA8) on the builder's map: byte 1 for the kept tiles.</summary>
    internal static void RangeCarve(byte* record, int range)
    {
        var caster = _units + record[0] * Battle.UnitSize;
        int cx = caster[0x4F], cy = caster[0x50];
        var ability = *(ushort*)(record + 2);
        var data = _game.RuleCode.GetAbilityAction(ability);
        var both = data != null && (data[4] & Both) == Both;
        int width = *Width, kept = 0;
        for (var i = 0; i < Tiles; i++)
        {
            var tile = Map + i * TileSize;
            var local = i & 0xFF;
            int x = width == 0 ? 0 : local % width, y = width == 0 ? 0 : local / width;
            if (tile[0] != 0 && RangeKeeps(x - cx, y - cy, range, both))
            {
                tile[1] = 1;
                kept++;
            }
            else
            {
                tile[1] = 0;
            }
        }
        Tracer.Line(_game.Log, $"cone area: 0x{ability:X3}'s range {range} from ({cx}, {cy}){(both ? ", any tile in the square" : "")}: {kept} tiles");
    }

    /// <summary>Whether ReMixed's range carve keeps a tile at (dx, dy) from the caster (PS1 0x8017BF20–0x8017BF64).</summary>
    internal static bool RangeKeeps(int dx, int dy, int range, bool both)
    {
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        if (ax == ay)
            return false; // the diagonals, and the caster's own tile
        return ax <= range && ay <= range && (both || dx == 0 || dy == 0);
    }

    private static void WriteJump(nint at, nint to, int length)
    {
        var jump = new byte[length];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)(to - (at + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(at, jump);
    }

    /// <summary>
    /// The diamond's stub: for both bits (dil), the area in cl and the centre tiles' mark in r8b (the area + 1) doubled
    /// as 2 × area and 2 × area + 1; then the game's three instructions (the two marks and cl).
    /// </summary>
    internal static void WriteDoubleStub(byte* stub, nint back)
    {
        var code = new List<byte>
        {
            0x40, 0x8A, 0xC7,       // mov al, dil
            0x24, Both,             // and al, 6
            0x3C, Both,             // cmp al, 6
            0x41, 0x8A, 0xCE,       // mov cl, r14b
            0x75, 0x08,             // jne marks
            0x00, 0xC9,             // add cl, cl
            0x44, 0x8A, 0xC1,       // mov r8b, cl
            0x41, 0xFE, 0xC0,       // inc r8b
            0x45, 0x88, 0x04, 0x19, // marks: mov [r9+rbx], r8b
            0x44, 0x88, 0x02,       // mov [rdx], r8b
            0xFF, 0x25, 0, 0, 0, 0, // jmp [rip+0]
        };
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > DoubleStubSize)
            throw new InvalidOperationException("the cone's diamond stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>
    /// The lines' stub: for both bits, <paramref name="carve"/>(the record copy at rsp+0x20, the area r14b), then
    /// <paramref name="end"/>; otherwise the game's Linear step (<paramref name="lines"/>(record, 1)), then
    /// <paramref name="back"/>, the game's 3 Directions test. Entered by a jmp: rsp is the builder's, 16-aligned, with its
    /// shadow space free, as for the game's own calls.
    /// </summary>
    internal static void WriteLinesStub(byte* stub, nint carve, nint lines, nint back, nint end)
    {
        var code = new List<byte>
        {
            0x40, 0x8A, 0xC7,               // mov al, dil
            0x24, Both,                     // and al, 6
            0x3C, Both,                     // cmp al, 6
            0x75, 0,                        // jne vanilla
        };
        var toVanilla = code.Count;
        code.AddRange([0x48, 0x8D, 0x4C, 0x24, 0x20]);   // lea rcx, [rsp+0x20]
        code.AddRange([0x41, 0x0F, 0xB6, 0xD6]);         // movzx edx, r14b
        code.AddRange([0x48, 0xB8]);                     // mov rax, carve
        code.AddRange(BitConverter.GetBytes((long)carve));
        code.AddRange([0xFF, 0xD0]);                     // call rax
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);         // jmp [rip+0] → end
        code.AddRange(BitConverter.GetBytes((long)end));
        code[toVanilla - 1] = checked((byte)(code.Count - toVanilla));
        code.AddRange([0x40, 0xF6, 0xC7, Linear]);       // test dil, 4
        code.AddRange([0x74, 0]);                        // je skip
        var toSkip = code.Count;
        code.AddRange([0xB2, 0x01]);                     // mov dl, 1
        code.AddRange([0x48, 0x8D, 0x4C, 0x24, 0x20]);   // lea rcx, [rsp+0x20]
        code.AddRange([0x48, 0xB8]);                     // mov rax, lines
        code.AddRange(BitConverter.GetBytes((long)lines));
        code.AddRange([0xFF, 0xD0]);                     // call rax
        code[toSkip - 1] = checked((byte)(code.Count - toSkip));
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);         // jmp [rip+0] → back
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > LinesStubSize)
            throw new InvalidOperationException("the cone's lines stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    [UnmanagedCallersOnly]
    private static void Carve(byte* record, int area)
    {
        try
        {
            Cone(record, area);
        }
        catch (Exception e)
        {
            Failed(e);
        }
    }

    /// <summary>ReMixed's carve (PS1 0x8017A7BC with the loop at 0x8017BEA8) on the builder's map, for both bits.</summary>
    internal static void Cone(byte* record, int area)
    {
        var caster = _units + record[0] * Battle.UnitSize;
        int cx = caster[0x4F], cy = caster[0x50];
        var direction = Direction(record[0x0C] - cx, record[0x10] - cy);
        int width = *Width, kept = 0;
        for (var i = 0; i < Tiles; i++)
        {
            var tile = Map + i * TileSize;
            var local = i & 0xFF;
            int x = width == 0 ? 0 : local % width, y = width == 0 ? 0 : local / width;
            if (tile[0] != 0 && Keeps(x - cx, y - cy, area, direction))
            {
                tile[0] = 1;
                kept++;
            }
            else
            {
                tile[0] = 0;
                tile[1] = 0;
            }
        }
        *Sorted = 1;
        Tracer.Line(_game.Log, $"cone area: from ({cx}, {cy}) toward ({record[0x0C]}, {record[0x10]}), area {area}: {kept} tiles");
    }

    /// <summary>The direction from the caster to the target: 1 −y, 2 −x, 4 +y, 8 +x; along x when |dx| ≥ |dy|.</summary>
    internal static int Direction(int dx, int dy) =>
        Math.Abs(dx) >= Math.Abs(dy) ? (dx < 0 ? 2 : 8) : (dy < 0 ? 1 : 4);

    /// <summary>Whether the cone keeps a tile at (dx, dy) from the caster (PS1 0x8017BEB4–0x8017BF54).</summary>
    internal static bool Keeps(int dx, int dy, int area, int direction)
    {
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        var sides = (dy == 0 ? 5 : dy < 0 ? 1 : 4) | (dx == 0 ? 0xA : dx < 0 ? 2 : 8);
        if (ax < ay)
            sides &= 5;
        else if (ax > ay)
            sides &= 0xA;
        if (ax == ay && area >= 2)
            return false;
        return ax <= area && ay <= area && (sides & direction) != 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"cone area: failed: {e}");
    }
}
