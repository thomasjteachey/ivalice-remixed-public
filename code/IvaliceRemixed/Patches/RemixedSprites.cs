using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's own unit art where the remaster has none (tools/build_mod.py REMIXED_SPRITES): each graphic's PS1 sheet
/// from the mod's Sprites/XX.spr under a file number of its own, its enhanced sprites' two textures (written by the
/// build to FFTIVC/data/enhanced/system/ffto/g2d/, served by the mod loader) under the texture numbers the build wrote
/// them for, and ReMixed's animation set for it. Installed by <see cref="Rules"/> with "remixed-sprites", after
/// "pack-files", whose read gives the game the sheets. Data: [[graphic, its texture in the remaster's table, its
/// texture, its animation set in the remaster's table, ReMixed's], ...].
/// </summary>
/// <remarks>
/// The remaster loads a unit graphic's sheet from the fftpack file two tables name by graphic ({file, bytes to read},
/// .rdata 0x784690 and 0x784BE0, the same in both; the sheet's loader 0x26E750 picks one by a setting), and with the
/// enhanced sprites on draws it from the textures .rdata 0x7151F0 names by graphic (the first of two, rows 0-255 and
/// 256-487 of the sheet at twice the size; -1 for none, when it draws the classic sheet). Vanilla fills the slots
/// ReMixed took with copies (the male Squire's sheet for 0x39 and 0x57-0x5C) and dummies (0x5D-0x5F, with no
/// textures), so each graphic gets a file number of its own, <see cref="FileBase"/> + the graphic: the game's read
/// never sees it (<see cref="PackFiles.StandIns"/>: it reads the graphic's old file in its place, the sheet goes over
/// that). A graphic's animation set is its 4 bytes in .rdata 0x673300 (BATTLE.BIN 0x2D748 on the PS1, the same for
/// 0x00-0x9E): its SHP (frames: 0 type1, 1 type2, 2 cyoko, 3 mon, 4 other, 6 arute, 7 kanzen, fftpack files 46-52),
/// its SEQ (animations: 0 type1, 1 type3, 2 cyoko, 3 mon, 4 other, 5 ruka, 6 arute, 7 kanzen, files 53-62; 0-4 are
/// always loaded, the unit loader 0x26FB28 loads the one above 4 a queued unit needs), a hover flag (another idle
/// animation) and its height (0x24 a person's). ReMixed changed those for its own art: monsters in people's slots (a
/// dragon in 0x57: mon and mon; a hydra in 0x5A: mon and ruka, as vanilla's Hydras), a Lamia in the Hydras' 0x97
/// (mon and mon), a person in the dummy 0x5D (mon in vanilla) and type1 where vanilla's copies were type2 (0x35,
/// 0x5F), so each graphic takes ReMixed's. A graphic whose texture or animation set in the tables isn't the one the
/// build expects is left as it is.
/// </remarks>
internal static unsafe class RemixedSprites
{
    internal const uint FileBase = 0x10000;
    internal const int Graphics = 0xAA;
    internal const string Folder = "Sprites";
    /// <summary>The PSP's graphics (0x9F-0xA9, the Dark Knight's 0x9F and 0xA0 among them): the remaster's sheet table
    /// names files for them (fftpack 880-890, unit_psp/) that its archives don't have, and its read looks a number up
    /// unchecked, so a sheet given to one is read in place of the male Squire's (<see cref="StandInGraphic"/>).</summary>
    internal const int FirstPspGraphic = 0x9F, StandInGraphic = 0x60;

    /// <summary>The sheet tables, from the sheet's loader (0x26E750): test eax, eax; lea rcx, [one table];
    /// lea r14, [the other]; cmove r14, rcx; mov ebp, [r14 + r15*8 + 4] (the size to read).</summary>
    internal const string SheetTablesPattern =
        "85 C0 48 8D 0D ?? ?? ?? ?? 4C 8D 35 ?? ?? ?? ?? 4C 0F 44 F1 43 8B 6C FE 04";
    /// <summary>The texture table, from a sprite slot's texture (0x26C33B): movzx eax, byte [rcx+0xA];
    /// mov eax, [r15 + rax*4 + table]; cmp eax, -1; je; inc eax (r15 the image's start).</summary>
    internal const string TexturesPattern = "0F B6 41 0A 41 8B 84 87 ?? ?? ?? ?? 83 F8 FF 74 ?? FF C0";
    /// <summary>The animation sets, from a graphic's frames (0x268E80): movzx eax, cx; lea rcx, [table];
    /// cmp byte [rcx + rax*4], 5; jae; movzx eax, byte [rcx + rax*4]; lea rcx, [the loaded SHPs];
    /// imul rax, rax, 0xD08 (0x268EB0 is its twin for the SEQ, + 1 and 0x808).</summary>
    internal const string AnimationSetsPattern =
        "0F B7 C1 48 8D 0D ?? ?? ?? ?? 80 3C 81 05 73 16 0F B6 04 81 48 8D 0D ?? ?? ?? ?? 48 69 C0 08 0D 00 00";
    /// <summary>The palette limit of a generic's sprite, in the sprite's maker 0x26EB74 (0x26EDE8): and byte
    /// [rbx+0x12C], 0xCF; mov r8d, 5 (the limit); movsx r12d, bp; mov ecx, r12d; sub ecx, 0x9F; je (the generics'
    /// case). For graphics 0x9F-0xA2 (the PSP's Dark Knights and others) and 0x60-0x85 (the generics) the maker takes
    /// the unit's palette (battle unit +4, the battle data's +0x17) and keeps it only under the limit, else draws
    /// palette 0 (0x26EEE7: cmp dx, r8w; cmovl), so ReMixed's Dark Knights with palettes 5 and 6 (Brave Story's third
    /// round, its entry 0x1AD's units 4 and 3) drew in palette 0's blue and gold where the PS1 draws its sheet's own 5
    /// (grey) and 6 (blue and orange), and showed face 08 (seen on the PC, 2026-11-19). The PS1 has no limit; a sheet
    /// has eight unit palettes (8-15 are its portrait's), and no battle data of vanilla's, the remaster's or
    /// ReMixed's but those two gives a generic one past 4.</summary>
    internal const string PaletteLimitPattern =
        "80 A3 2C 01 00 00 CF 41 B8 ?? 00 00 00 44 0F BF E5 41 8B CC 81 E9 9F 00 00 00 0F 84";
    internal const int PaletteLimitAt = 9;
    internal const byte RemasterPaletteLimit = 5, UnitPalettes = 8;
    /// <summary>The palette limit's immediate (null until found), for the offline check.</summary>
    internal static byte* PaletteLimit;

    /// <summary>{file, size} by graphic, in each of the two tables.</summary>
    internal static uint* SheetTable, OtherSheetTable;
    /// <summary>The first texture by graphic (-1 none).</summary>
    internal static int* Textures;
    /// <summary>The animation set by graphic: SHP, SEQ, hover flag and height, a byte each (the first in the low
    /// bits).</summary>
    internal static uint* AnimationSets;
    /// <summary>The graphics given ReMixed's art, for the offline check.</summary>
    internal static readonly List<int> Given = [];

    internal static void Find(GameImage image)
    {
        var at = image.FindUnique(SheetTablesPattern);
        OtherSheetTable = (uint*)GameImage.Rip(at + 2, 3, 7);
        SheetTable = (uint*)GameImage.Rip(at + 9, 3, 7);
        at = image.FindUnique(TexturesPattern);
        Textures = (int*)(image.ImageStart + *(int*)(at + 8));
        at = image.FindUnique(AnimationSetsPattern);
        AnimationSets = (uint*)GameImage.Rip(at + 3, 3, 7);
        // Ramza's first sheet (file 158, 0xB000 bytes), texture (0x33E) and animation set (type1 and type1, height
        // 0x24), and Ultima's last form's (kanzen and kanzen, height 0x78), as in the remaster's tables.
        if (SheetTable[2] != OtherSheetTable[2] || SheetTable[3] != OtherSheetTable[3] || SheetTable[3] == 0
            || Textures[1] != 0x33E || Textures[0] != -1 || AnimationSets[1] != 0x24000000
            || AnimationSets[0x49] != 0x78000707)
            throw new InvalidDataException("the unit sheet, texture and animation tables aren't the ones this DLL expects");
    }

    public static string Install(Game game, JsonElement data)
    {
        if (PackFiles.ReadFileAt == 0)
            throw new InvalidOperationException("needs the pack files' read (\"pack-files\")");
        Find(game.Image);
        var folder = Path.Combine(Path.GetDirectoryName(typeof(RemixedSprites).Assembly.Location) ?? "", Folder);
        Given.Clear();
        var left = new List<string>();
        foreach (var row in data.EnumerateArray())
        {
            int graphic = row[0].GetInt32(), now = row[1].GetInt32(), texture = row[2].GetInt32();
            uint setNow = row[3].GetUInt32(), remixedSet = row[4].GetUInt32();
            var path = Path.Combine(folder, $"{graphic:X2}.spr");
            // The tables first, so that the offline check (with no sheets beside it) tests what the build expects.
            if ((uint)graphic >= Graphics)
                left.Add($"0x{graphic:X2} (not a graphic)");
            else if (Textures[graphic] != now)
                left.Add($"0x{graphic:X2} (its texture is {Textures[graphic]}, not {now})");
            // The height byte may be ReMixed's already: HittingBodies gives the children's graphics (0x4A, 0x4B) theirs.
            else if (AnimationSets[graphic] != setNow
                     && AnimationSets[graphic] != ((setNow & 0x00FFFFFF) | (remixedSet & 0xFF000000)))
                left.Add($"0x{graphic:X2} (its animation set is 0x{AnimationSets[graphic]:X8}, not 0x{setNow:X8})");
            else if (!File.Exists(path))
                left.Add($"0x{graphic:X2} (no sheet)");
            else
                Give(graphic, File.ReadAllBytes(path), texture, remixedSet);
        }
        var palettes = LiftPaletteLimit(game.Image);
        game.Log.Info($"unit sprites: ReMixed's art for {string.Join(", ", Given.Select(g => $"0x{g:X2}"))}"
                      + (left.Count > 0 ? $"; left as the remaster has them: {string.Join(", ", left)}" : "")
                      + (palettes ? $"; generics and Dark Knights keep palettes 0-{UnitPalettes - 1}"
                                  : "; the palette limit isn't the one this DLL expects (left at the remaster's)"));
        return $"ReMixed's own unit art ({Given.Count} graphics)";
    }

    /// <summary>The generics' and Dark Knights' palette limit (<see cref="PaletteLimitPattern"/>) raised from 5 to the
    /// sheet's eight unit palettes, as the PS1 draws them (already raised is fine). False if the code isn't the one this
    /// DLL expects.</summary>
    internal static bool LiftPaletteLimit(GameImage image)
    {
        nint at;
        try
        {
            at = image.FindUnique(PaletteLimitPattern);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        var limit = (byte*)at + PaletteLimitAt;
        if (*limit != RemasterPaletteLimit && *limit != UnitPalettes)
            return false;
        GameImage.Unprotected((nint)limit, 1, () => *limit = UnitPalettes);
        PaletteLimit = limit;
        return true;
    }

    /// <summary>Graphic <paramref name="graphic"/> drawn from <paramref name="sheet"/> and texture
    /// <paramref name="texture"/> (and the next), and animated with <paramref name="animationSet"/>.</summary>
    internal static void Give(int graphic, byte[] sheet, int texture, uint animationSet)
    {
        var number = FileBase + (uint)graphic;
        PackFiles.StandIns[number] = SheetTable[2 * (graphic >= FirstPspGraphic ? StandInGraphic : graphic)];
        PackFiles.Files[number] = sheet;
        foreach (var table in new[] { (nint)SheetTable, (nint)OtherSheetTable })
        {
            var entry = (uint*)table + 2 * graphic;
            GameImage.Unprotected((nint)entry, 8, () =>
            {
                entry[0] = number;
                entry[1] = (uint)sheet.Length;
            });
        }
        var at = Textures + graphic;
        GameImage.Unprotected((nint)at, 4, () => *at = texture);
        var animations = AnimationSets + graphic;
        GameImage.Unprotected((nint)animations, 4, () => *animations = animationSet);
        Given.Add(graphic);
    }

    /// <summary>
    /// ReMixed's graphics whose sheet is vanilla's under another graphic (tools/build_mod.py REMIXED_SPRITE_ALIASES):
    /// each takes that graphic's sheet and textures, the remaster's own redraw (0x38, ReMixed's male Bard, as the
    /// remaster's Bard 0x82, whose animation set it has already). Installed with "remixed-sprites", after
    /// <see cref="Install"/>. Data: [[graphic, its texture in the remaster's table, the graphic it draws as], ...].
    /// </summary>
    public static string InstallAliases(Game game, JsonElement data)
    {
        if (SheetTable == null)
            Find(game.Image);
        var done = new List<string>();
        var left = new List<string>();
        foreach (var row in data.EnumerateArray())
        {
            int graphic = row[0].GetInt32(), now = row[1].GetInt32(), source = row[2].GetInt32();
            if ((uint)graphic >= Graphics || (uint)source >= Graphics || Textures[graphic] != now)
            {
                left.Add($"0x{graphic:X2}");
                continue;
            }
            DrawAs(graphic, source);
            done.Add($"0x{graphic:X2} as 0x{source:X2}");
        }
        game.Log.Info($"unit sprites: drawn as the remaster's: {string.Join(", ", done)}"
                      + (left.Count > 0 ? $"; left as they are (not the tables expected): {string.Join(", ", left)}" : ""));
        return $"ReMixed's graphics drawn as the remaster's own ({done.Count})";
    }

    /// <summary>Graphic <paramref name="graphic"/> drawn as <paramref name="source"/> is: its sheet (both tables'
    /// {file, size}) and its texture.</summary>
    internal static void DrawAs(int graphic, int source)
    {
        foreach (var table in new[] { (nint)SheetTable, (nint)OtherSheetTable })
        {
            var entry = (uint*)table + 2 * graphic;
            var from = (uint*)table + 2 * source;
            GameImage.Unprotected((nint)entry, 8, () =>
            {
                entry[0] = from[0];
                entry[1] = from[1];
            });
        }
        var at = Textures + graphic;
        var texture = Textures[source];
        GameImage.Unprotected((nint)at, 4, () => *at = texture);
        Given.Add(graphic);
    }
}
