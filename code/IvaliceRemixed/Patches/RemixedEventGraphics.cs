namespace IvaliceRemixed.Patches;

/// <summary>
/// Gives ReMixed's event graphics enhanced textures. Two have no table entry and every event texture belongs to a
/// graphic, so they borrow unused PSP-character stubs: its heavily-used 0x10 redraw, shipped in place (no stock
/// enhanced scene loads 0x10), takes 0x468, and appended alias 0xF2 (its 0x79) takes 0x46A. Those numbers aren't in the
/// event-texture range, and their ordinary +0x8C companions are unrelated pictures, so both take a stock event
/// graphic's neutral palette map. (0x10 used native slot 0x1E until 2026-11-18, which the remaster's event 149 loads
/// for Agrias's and Ovelia's poses.) Installed with ReMixed's event support.
/// </summary>
internal static unsafe class RemixedEventGraphics
{
    internal const int InPlaceGraphic = 0x10, InPlaceTexture = 0x468;
    internal const int AppendedAlias = 0xF2, AppendedTexture = 0x46A;
    internal const int PaletteDistance = 0x8C, NeutralPaletteTexture = 0x515;
    /// <summary>The graphics whose textures are PSP stubs, which take the neutral palette map.</summary>
    internal static readonly int[] StubGraphics = [InPlaceGraphic, AppendedAlias];
    private const int PalettePatchBytes = 11, PaletteStubSize = 48;
    /// <summary>(graphic, stock table entry, texture shipped by the build), for every routed event graphic.</summary>
    internal static readonly (int Graphic, int Stock, int Texture)[] Routed =
    [
        (0x0A, 0x489, 0x489), (0x78, 0x4DD, 0x4DD), (0x89, 0x502, 0x502),
        (0x8A, 0x504, 0x504), (0x8B, 0x501, 0x501), (0x8C, 0x500, 0x500),
        (0x8D, 0x503, 0x503), (0x8E, 0x4FF, 0x4FF), (0x6F, 0x4DA, 0x4DA),
        (InPlaceGraphic, -1, InPlaceTexture), (AppendedAlias, -1, AppendedTexture),
    ];

    /// <summary>The event texture table, exposed for the offline check.</summary>
    internal static int* Textures { get; private set; }
    /// <summary>The two event slots' palette-texture ids. This is captured before the palette assignment is patched;
    /// the patch overwrites the RIP-relative instruction that otherwise reveals this address.</summary>
    internal static int* PaletteIds { get; private set; }
    /// <summary>The enhanced event-texture setup and its two patched palette assignments, for the offline check and
    /// TreasureArrows (which installs later, after the original byte pattern has been patched).</summary>
    internal static nint TextureAt { get; private set; }
    internal static readonly nint[] PaletteSites = new nint[2];
    internal static readonly nint[] PaletteStubs = new nint[2];

    public static string Install(Game game)
    {
        var texture = game.Image.FindUnique(TreasureArrows.TexturePattern);
        TextureAt = texture;
        Textures = (int*)(game.Image.ImageStart + *(int*)(texture + 0x4E));
        PaletteIds = (int*)GameImage.Rip(texture + 0x5D, 2, 6);
        var wrong = Routed.Where(row => Textures[row.Graphic] != row.Stock).ToList();
        var stubsFree = Enumerable.Range(0, 0x8F)
            .All(graphic => Textures[graphic] != InPlaceTexture && Textures[graphic] != AppendedTexture);
        if (Textures[0] != 0x47B || wrong.Count != 0 || !stubsFree)
            throw new InvalidDataException("the event-graphic texture table isn't the one this DLL expects: "
                + string.Join(", ", wrong.Select(row =>
                    $"0x{row.Graphic:X} is {Textures[row.Graphic]}, expected {row.Stock}")));
        foreach (var row in Routed.Where(row => row.Stock != row.Texture))
            Set(row.Graphic, row.Texture);
        // Slot 1 and slot 0 contain the same `add eax, 0x8C; mov [palette], eax` sequence. Preserve it for every stock
        // graphic, but give the stub-textured graphics a real event palette texture instead of their texture + 0x8C.
        var sites = new[] { texture + 0x31, texture + 0x58 };
        var stubs = GameImage.AllocateCodeNear(texture, sites.Length * PaletteStubSize);
        for (var k = 0; k < sites.Length; k++)
        {
            var site = sites[k];
            if (!new ReadOnlySpan<byte>((void*)site, 5).SequenceEqual(
                    new byte[] { 0x05, (byte)PaletteDistance, 0, 0, 0 })
                || *(byte*)(site + 5) != 0x89 || *(byte*)(site + 6) != 0x05)
                throw new InvalidDataException("the event-graphic palette assignment isn't the one this DLL expects");
            var palette = GameImage.Rip(site + 5, 2, 6);
            var stub = stubs + k * PaletteStubSize;
            WritePaletteStub(stub, palette, site + PalettePatchBytes);
            var patch = Enumerable.Repeat((byte)0x90, PalettePatchBytes).ToArray();
            patch[0] = 0xE9;
            BitConverter.GetBytes(checked((int)((nint)stub - (site + 5)))).CopyTo(patch, 1);
            GameImage.WriteCode(site, patch);
            (PaletteSites[k], PaletteStubs[k]) = (site, (nint)stub);
        }
        return $"ReMixed's event graphics textured (in place 0x{InPlaceGraphic:X}->0x{InPlaceTexture:X}, appended "
               + $"0x{AppendedAlias:X}->0x{AppendedTexture:X}; neutral palette 0x{NeutralPaletteTexture:X})";
    }

    private static void Set(int graphic, int texture)
    {
        var at = Textures + graphic;
        GameImage.Unprotected((nint)at, sizeof(int), () => *at = texture);
    }

    /// <summary>Writes `eax = texture + 0x8C`, except the StubGraphics take the neutral event palette texture; stores
    /// it and returns after the replaced assignment. Inputs are eax (texture) and edi (graphic).</summary>
    internal static void WritePaletteStub(byte* stub, nint palette, nint back)
    {
        var code = new List<byte> { 0x05, PaletteDistance, 0, 0, 0 };              // add eax, 0x8C
        for (var k = 0; k < StubGraphics.Length; k++)
        {
            code.AddRange([0x81, 0xFF]);
            code.AddRange(BitConverter.GetBytes(StubGraphics[k]));                 // cmp edi, graphic
            var after = StubGraphics.Length - 1 - k;
            code.AddRange(after > 0
                ? new byte[] { 0x74, (byte)(after * 8) }                            // je over the other compares
                : new byte[] { 0x75, 0x05 });                                       // jne over the mov
        }
        code.Add(0xB8);
        code.AddRange(BitConverter.GetBytes(NeutralPaletteTexture));                // mov eax, neutral
        code.AddRange([0x89, 0x05]);                                                // mov [palette], eax
        code.AddRange(BitConverter.GetBytes(checked((int)(palette - ((nint)stub + code.Count + 4)))));
        code.AddRange([0xE9]);
        code.AddRange(BitConverter.GetBytes(checked((int)(back - ((nint)stub + code.Count + 4)))));
        if (code.Count > PaletteStubSize)
            throw new InvalidOperationException("the event palette stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
