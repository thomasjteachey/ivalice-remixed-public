using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's characters' faces (tools/build_mod.py write_remixed_faces): each graphic the mod draws with ReMixed's sheet
/// (<see cref="RemixedSprites"/>) shows the face the build made of that sheet's portrait, where the remaster showed the
/// face of the graphic it folds it into (a male Squire's for Hirame's 0x59, Malak's for Luso's 0x12). Installed by
/// <see cref="Rules"/> with "remixed-faces". Data: [[graphic, its face], ...].
/// </summary>
/// <remarks>
/// The remaster draws a face from the texture ui/ffto/common/face/texture/wldface_&lt;face&gt;_&lt;palette + 8&gt;_uitx.tex
/// (its parts file in textureparts/, UILoadTexturePath row 4 the name's format, 0x0FC22C the formatting). Every place
/// that draws one (speech boxes 0x1389C4, the unit cards 0x144E24, 0x147D94 and 0x1480D4, the unit lists 0x28FF34 and
/// 0x10ABE4) first passes the unit's graphic through the fold (0x0F8338), which turns a copy into the graphic it copies
/// (0x11 into 0x17, 0x12 into 0x1A, 0x39 and 0x57-0x5C into 0x60, 0x98 into 0x94, ...) and gives any other number back.
/// Here the fold gives a listed graphic its own face, whose files the build ships (the mod loader serves them).
///
/// The world menus (the party menu's unit screen 0x2C08C0, the formation list 0x2C1EA8, the join screen 0x36F5D0)
/// don't start from the graphic: a roster unit's sprite set gives a PS1 portrait (0x32AB3C, the table 0x683A70), and
/// the portrait a graphic (0x274028, the table 0x7146D0), which the fold then takes. Both of a story character's sprite
/// sets share its portrait, and the portrait gives the second one, where the remaster keeps the face: Agrias's 0x1E and
/// 0x34 both have 0x17, which gives 0x34, and the hook turned that into Ashley's face (Rafa's 0x29 and ReMixed's 0x19
/// the same, by 0x0E; Celia's and Lede's by 0x8D and 0x8E). So a listed graphic whose number holds a story
/// character's face gets an unused portrait of its own, and the portrait they shared gives the story character's own
/// sprite set, which the game's fold turns into that face (<see cref="SeparatePortraits"/>). The PS1's tables do the
/// same (ReMixed gives Ashley's 0x34 a portrait of her own). An event's speech boxes start from the speaker's PS1
/// portrait too, and take one of 0x51 or less through a table of their own (0x714D50, <see
/// cref="EventGraphicOfPortraitPattern"/>), which gave Rafa's 0x0E as 0x19 and Agrias's 0x17 as 0x34: her boxes showed
/// ReMixed's character's face, his Ashley's (a tester, 2026-11-19, Rafa in the frog scene). The separated portraits'
/// entries there go to the story character's own sprite set as well. The portraits it gives no sprite set of the
/// remaster's are left (0x08, Gafgarion's second, as ReMixed's 0x11): ReMixed redrew part of EVTFACE, and which of its
/// events name one isn't known.
///
/// A message's portrait 1-8 (the PS1's EVTFACE portrait of the set event op 0x50 loaded) is a graphic from the set's
/// row of a table (.rdata 0x7141E0, copied to [0x37A1348] by 0x2459E0, read by 0x221798 for the box record's face),
/// which the fold then takes. Set 4's portrait 4 is Rafa's, as 0x19, so her boxes in the frog scene (event 270, style
/// 0x92, portrait 4) showed Balthier's face (a tester on 0.9.22); <see cref="RemasterEventFaces"/> points such entries at
/// the graphic the remaster means, Rafa's 0x29 there (the only one; none holds Agrias's 0x34), with the palette cap
/// 0x221798 gives the graphic it replaced (.rdata 0x680970; 0x29's was 0, which drew no face).
///
/// Brave Story's people (0x172B70 and 0x177004, through 0x1E4720 and 0x1E5B6C) carry the remaster's graphic numbers
/// for its own characters, two of which the mod gives ReMixed's (Gafgarion's 0x11, Malak's 0x12, which the game folds
/// into 0x17 and 0x1A). The two lookups are hooked to hand Brave Story the graphic the remaster means
/// (<see cref="RemasterPeople"/>). Were the mod to add ReMixed's characters to Brave Story, this would need their own
/// numbers.
/// </remarks>
internal static unsafe class RemixedFaces
{
    /// <summary>The fold (0x0F8338: ecx the graphic, eax the face): mov edx, ecx; cmp ecx, 0x38; jg; je; cmp ecx,
    /// 0x2A; jg; je; sub ecx, 0x11; je; sub ecx, 1; je.</summary>
    internal const string FoldPattern =
        "8B D1 83 F9 38 0F 8F ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 83 F9 2A 7F ?? 74 ?? 83 E9 11 74 ?? 83 E9 01 74";
    /// <summary>A PS1 portrait's graphic (0x274028: edx the portrait, eax the graphic, from .rdata 0x7146D0, 160
    /// dwords, -1 for none): or ecx, -1; cmp edx, 0x9F; ja; movsxd rax, edx; lea rcx, [table]; mov eax, [rcx+rax*4];
    /// ret.</summary>
    internal const string GraphicOfPortraitPattern =
        "83 C9 FF 81 FA 9F 00 00 00 77 ?? 48 63 C2 48 8D 0D ?? ?? ?? ?? 8B 04 81 C3";
    /// <summary>A roster unit's PS1 portrait by its sprite set (.rdata 0x683A70, 0xD3 halfwords: the portrait, and its
    /// palette shifted left 10), read in 0x32AB3C (r15 the image): cmp ecx, 0xD3; jae; movsxd rax, ecx;
    /// movzx r11d, word [r15+rax*2+table]; mov al, r8b.</summary>
    internal const string PortraitOfSpriteSetPattern =
        "81 F9 D3 00 00 00 73 ?? 48 63 C1 45 0F B7 9C 47 ?? ?? ?? ?? 41 8A C0";
    /// <summary>An event speech box's graphic by its portrait ([0x2FF9EDC], the speaker unit's PS1 portrait) with the
    /// enhanced sprites, at 0x2F999C (the same at 0x339DF4 and 0x3994DD): cmp r8d, 0x51; ja (0x274028's table above);
    /// lea rcx, [image]; mov ecx, [rcx + r8*4 + table], the table .rdata 0x714D50 (0x52 dwords), which the fold then
    /// takes.</summary>
    internal const string EventGraphicOfPortraitPattern = "41 83 F8 51 77 ?? 48 8D 0D ?? ?? ?? ?? 42 8B 8C 81";
    private const int EventPortraits = 0x52;
    /// <summary>An EVTFACE set's graphics (0x2459E0, ecx the set event op 0x50 loads): cmp ecx, 7; ja; lea eax,
    /// [rcx*8]; movsxd rcx, eax; lea rax, [table]; lea rax, [rax+rcx*4]; test rax, rax; je; movups the set's 8 dwords
    /// into the event's portraits ([0x37A1348], which a message's portrait 1-8 names: 0x221798, 0x21FA50).</summary>
    internal const string EventFaceSetPattern =
        "83 F9 07 77 30 8D 04 CD 00 00 00 00 48 63 C8 48 8D 05 ?? ?? ?? ?? 48 8D 04 88 48 85 C0 74 16 0F 10 00 0F 10 " +
        "48 10 0F 11 05";
    internal const int EventFaceSets = 8, EventFacesPerSet = 8;
    /// <summary>A message's face (0x221798: ecx its portrait less 1, -1 for the speaker unit's own; edx the unit's
    /// slot; r8 the palette out; eax the graphic the fold takes): push rbx; sub rsp, 0xB0; or dword [r8], -1;
    /// movsxd r9, ecx; movsxd rbx, edx; cmp r9d, -1; je. A portrait's palette is the unit's + 8, at most the graphic's
    /// byte in a table only this reads (+<see cref="PaletteCapAt"/>: add ecx, 8; movsxd rax, r9d; mov [r8], ecx; movzx
    /// edx, byte [r10 + rax + table], .rdata 0x680970), 0 for the graphics the fold folds: no face file.</summary>
    internal const string MessageFacePattern = "40 53 48 81 EC B0 00 00 00 41 83 08 FF 4C 63 C9 48 63 DA 41 83 F9 FF 74";
    private const int PaletteCapAt = 0x63;
    private static ReadOnlySpan<byte> PaletteCapRead =>
        [0x83, 0xC1, 0x08, 0x49, 0x63, 0xC1, 0x41, 0x89, 0x08, 0x42, 0x0F, 0xB6, 0x94, 0x10];
    private const int Graphics = 0x100, Portraits = 0xA0, SpriteSets = 0xD3;
    private const int Portrait = 0xFF, NoPortrait = 0x300; // a sprite set's portrait; bits that make 0x32AB3C give none

    /// <summary>Portraits the remaster leaves unused (-1), past ReMixed's own placeholders (0x61-0x67, 0x9D) and the
    /// numbers 0x2E16A8 places apart (0x3F-0x52, 0x68-0x6F).</summary>
    private static ReadOnlySpan<byte> UnusedPortraits => [0x9F, 0x9E, 0x98, 0x8C, 0x84, 0x70];
    /// <summary>A Brave Story person's face (0x1E4720: rdx the person's keys; LoreDictionary's +0x1C, NXD 237, the
    /// person's graphic): its prologue, the row lookup (0x1E5018), then mov eax, [row+0x1C].</summary>
    internal const string PersonFacePattern =
        "40 53 48 83 EC 30 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 28 8B 1A E8 ?? ?? ?? ?? 48 83 64 24 20 00 48 8D " +
        "4C 24 20 44 8B C0 8B D3 E8 ?? ?? ?? ?? 4C 8B 44 24 20 4D 85 C0 74 2F 49 83 38 00 7C 1D 49 8B C8 E8 ?? ?? ?? ?? " +
        "48 85 C0 74 1C 49 83 38 00 7C 0A 49 8B C8 E8 ?? ?? ?? ?? EB 07 48 8D 05 ?? ?? ?? ?? 8B 40 1C";
    /// <summary>The chronicle list's person's face (0x1E5B6C: rdx a UIChronicleCharaList row's keys, NXD 283, whose +8
    /// and +0x10 key LoreDictionary): its prologue, both lookups, then mov eax, [row+0x1C].</summary>
    internal const string ChronicleFacePattern =
        "40 53 48 83 EC 30 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 28 44 8B 42 04 48 8D 4C 24 20 8B 12 48 83 64 24 " +
        "20 00 E8 ?? ?? ?? ?? 4C 8B 44 24 20 4D 85 C0 0F 84 85 00 00 00 49 83 38 00 7C 0D 49 8B C8 E8 ?? ?? ?? ?? 48 85 " +
        "C0 74 72 48 83 64 24 20 00 48 8D 1D ?? ?? ?? ?? 49 83 38 00 7C 15 49 8B C8 E8 ?? ?? ?? ?? 49 8B C8 4C 8B C8 E8 " +
        "?? ?? ?? ?? EB 06 4C 8B CB 48 8B C3 45 8B 41 10 48 8D 4C 24 20 8B 50 08 E8 ?? ?? ?? ?? 4C 8B 44 24 20 4D 85 C0 " +
        "74 29 49 83 38 00 7C 1E 49 8B C8 E8 ?? ?? ?? ?? 48 85 C0 74 16 49 83 38 00 7C 0B 49 8B C8 E8 ?? ?? ?? ?? 48 8B " +
        "D8 8B 43 1C";

    /// <summary>A speech box's face for a battle unit (0x22AA88: rcx the box's layout, edx the unit's slot, r8 and r9
    /// out; eax the face): the graphic recomputed (0x0FCAB0), then the face from the unit's sprite's +9, its sheet slot,
    /// which the callers (0x214153 and others) take through <see cref="SheetSlots"/> when under 10: the graphic of the
    /// sheet loaded there. A story character whose graphic shares its sheet with its second sprite set's (Rafa's 0x29
    /// with 0x19, Agrias's 0x1E with 0x34) gets the sheet's graphic, which is ReMixed's character's number in the mod, so
    /// the fold would give her box his face (Balthier's for Rafa, Ashley's for Agrias; the frog scene's boxes name an
    /// EVTFACE portrait instead, <see cref="RemasterEventFaces"/>): <see cref="BoxFace"/> hands back the unit's own
    /// graphic there, which the fold sends to her face. Its prologue: mov
    /// [rsp+8], rbx; push rbp, rsi, rdi, r14, r15; sub rsp, 0x20; or edi, -1; movsxd rbp, edx; mov r15, r9;
    /// mov rsi, r8; mov rbx, rcx; test r8, r8; je; mov [r8], edi; cmp ebp, 0x14 (+0x33: lea rax, [the battle units]).
    /// </summary>
    internal const string BoxFacePattern =
        "48 89 5C 24 08 55 56 57 41 56 41 57 48 83 EC 20 83 CF FF 48 63 EA 4D 8B F9 49 8B F0 48 8B D9 4D 85 C0 74 03 " +
        "41 89 38 83 FD 14";
    /// <summary>A caller's sheet slot to graphic (0x214162): cmp rdx, 0xA; jae; mov edx, [r8 + rdx*4 + table]
    /// (r8 the image's start; .data 0x37A59E0, 10 dwords).</summary>
    internal const string SheetSlotsPattern = "48 83 FA 0A 73 08 41 8B 94 90";
    internal static nint BoxFaceAt { get; private set; }
    internal static int* SheetSlots { get; private set; }
    private static byte* _boxUnits;
    private static delegate* unmanaged<nint, int, nint, int*, int> _boxFace; // the original

    /// <summary>The face by graphic (-1: the game's fold).</summary>
    private static readonly int[] Faces = NewFaces();
    /// <summary>The graphic Brave Story means by each number (<see cref="RemasterPeople"/>).</summary>
    private static readonly int[] People = Enumerable.Range(0, 0x100).ToArray();
    private static delegate* unmanaged<int, int> _fold; // the original
    private static delegate* unmanaged<nint, nint, int> _personFace, _chronicleFace; // the originals
    private static Log _log = null!;
    private static int _failed;

    /// <summary>The fold as found, for the offline check (calling it runs the hook).</summary>
    internal static nint FoldAt { get; private set; }
    // For the offline check: 0x274028 and the two portrait tables.
    internal static nint GraphicOfPortraitAt { get; private set; }
    internal static int* GraphicOfPortrait { get; private set; }
    internal static ushort* PortraitOfSpriteSet { get; private set; }
    internal static int* EventGraphicOfPortrait { get; private set; }
    // For the offline check: 0x2459E0 (calling it loads a set into the event's portraits) and its table.
    internal static nint EventFaceSetAt { get; private set; }
    internal static int* EventFaceSet { get; private set; }
    internal static nint MessageFaceAt { get; private set; }
    internal static byte* PaletteCaps { get; private set; }
    // For the offline check: Brave Story's two lookups as hooked.
    internal static nint PersonFaceAt { get; private set; }
    internal static nint ChronicleFaceAt { get; private set; }

    private static int[] NewFaces()
    {
        var faces = new int[Graphics];
        Array.Fill(faces, -1);
        return faces;
    }

    public static string Install(Game game, JsonElement data)
    {
        _log = game.Log;
        Array.Fill(Faces, -1);
        var given = new List<string>();
        foreach (var row in data.EnumerateArray())
        {
            int graphic = row[0].GetInt32(), face = row[1].GetInt32();
            if ((uint)graphic >= Graphics || face is < 0 or > 999)
                throw new InvalidDataException($"a face out of range: graphic {graphic}, face {face}");
            Faces[graphic] = face;
            given.Add($"0x{graphic:X2} as {face:D3}");
        }
        var at = game.Image.FindUnique(FoldPattern);
        _fold = (delegate* unmanaged<int, int>)game.Hooks.Hook(
            at, (nint)(delegate* unmanaged<int, int>)&Fold, "the face fold");
        FoldAt = at;
        game.Log.Info($"faces: ReMixed's portraits for {string.Join(", ", given)}");
        try
        {
            BoxFaceAt = game.Image.FindUnique(BoxFacePattern);
            _boxUnits = (byte*)GameImage.Rip(BoxFaceAt + 0x33, 3, 7);
            SheetSlots = (int*)(game.Image.ImageStart + *(int*)(game.Image.FindUnique(SheetSlotsPattern) + 10));
            _boxFace = (delegate* unmanaged<nint, int, nint, int*, int>)game.Hooks.Hook(
                BoxFaceAt, (nint)(delegate* unmanaged<nint, int, nint, int*, int>)&BoxFace, "a speech box's face");
        }
        catch (Exception e)
        {
            game.Log.Error($"faces: a speech box's face not hooked (Rafa and Agrias can show ReMixed's faces in "
                           + $"theirs): {e.Message}");
        }
        var apart = new List<string>();
        try
        {
            apart = SeparatePortraits(game.Image);
            game.Log.Info($"faces: the world menus' portraits apart: {string.Join(", ", apart)}");
        }
        catch (Exception e)
        {
            game.Log.Error($"faces: the world menus' portraits not separated (a story character there can show the "
                           + $"face of ReMixed's character on its second sprite set): {e.Message}");
        }
        var people = RemasterPeople();
        var sets = new List<string>();
        try
        {
            sets = RemasterEventFaces(game.Image);
            game.Log.Info($"faces: the event face sets as the remaster's people: {string.Join(", ", sets)}");
        }
        catch (Exception e)
        {
            game.Log.Error($"faces: the event face sets not mended (Rafa's boxes in the frog scene can show the face of "
                           + $"ReMixed's character on 0x19): {e.Message}");
        }
        try
        {
            PersonFaceAt = game.Image.FindUnique(PersonFacePattern);
            ChronicleFaceAt = game.Image.FindUnique(ChronicleFacePattern);
            _personFace = (delegate* unmanaged<nint, nint, int>)game.Hooks.Hook(
                PersonFaceAt, (nint)(delegate* unmanaged<nint, nint, int>)&PersonFace, "Brave Story's person faces");
            _chronicleFace = (delegate* unmanaged<nint, nint, int>)game.Hooks.Hook(
                ChronicleFaceAt, (nint)(delegate* unmanaged<nint, nint, int>)&ChronicleFace,
                "the chronicle list's person faces");
            game.Log.Info($"faces: Brave Story's people as the remaster's: {string.Join(", ", people)}");
        }
        catch (Exception e)
        {
            game.Log.Error($"faces: Brave Story's people not given the remaster's faces: {e.Message}");
        }
        return $"ReMixed's characters' faces ({given.Count} graphics, {apart.Count} with portraits of their own, "
               + $"{sets.Count} event face set entries and Brave Story's people the remaster's)";
    }

    /// <summary>Points each EVTFACE set's graphic (.rdata 0x7141E0, 8 sets of 8) that the mod gives one of ReMixed's
    /// characters at the one the remaster means (<see cref="Person"/>): set 4's portrait 4 is Rafa's, 0x19, where the
    /// remaster keeps her face, so the frog scene's boxes (op 0x50 4, then portrait 4) showed Balthier's (a tester on
    /// 0.9.22). EVTFACE's sets 2-7 are vanilla's in ReMixed too, so its events that load them mean the same people.
    /// The graphic it gives takes the palette cap of the one it replaces (Rafa's 0x29 has none of its own: the remaster
    /// keeps her face under 0x19, and a box asked for 0x29's at palette 0 showed no face). Returns what it changed.
    /// </summary>
    private static List<string> RemasterEventFaces(GameImage image)
    {
        var loader = image.FindUnique(EventFaceSetPattern);
        var sets = (int*)GameImage.Rip(loader + 15, 3, 7);
        var messageFace = image.FindUnique(MessageFacePattern);
        if (!new ReadOnlySpan<byte>((byte*)messageFace + PaletteCapAt, PaletteCapRead.Length).SequenceEqual(PaletteCapRead))
            throw new InvalidDataException("a message's face (0x221798) isn't laid out as this DLL expects");
        var caps = (byte*)(image.ImageStart + *(int*)(messageFace + PaletteCapAt + PaletteCapRead.Length));
        const int count = EventFaceSets * EventFacesPerSet;
        // Set 0 starts 0xB7, 0x1C; set 4's portrait 4 is Rafa's 0x19 (her 0x29 once mended), whose palette cap is 8.
        if (sets[0] != 0xB7 || sets[1] != 0x1C || sets[4 * EventFacesPerSet + 3] is not (0x19 or 0x29) || caps[0x19] != 8
            || caps[2] != 0x0B || Enumerable.Range(0, count).Any(at => (uint)sets[at] >= Graphics))
            throw new InvalidDataException("the EVTFACE sets' graphics aren't the ones this DLL expects");
        var moves = new List<(int At, int Graphic, int Person)>();
        for (var at = 0; at < count; at++)
            if (People[sets[at]] != sets[at])
                moves.Add((at, sets[at], People[sets[at]]));
        if (moves.Count > 0)
        {
            GameImage.Unprotected((nint)sets, count * sizeof(int), () =>
            {
                foreach (var move in moves)
                    sets[move.At] = move.Person;
            });
            GameImage.Unprotected((nint)caps, Graphics, () =>
            {
                foreach (var move in moves)
                    caps[move.Person] = Math.Max(caps[move.Person], caps[move.Graphic]);
            });
        }
        EventFaceSetAt = loader;
        EventFaceSet = sets;
        MessageFaceAt = messageFace;
        PaletteCaps = caps;
        return moves.Select(move => $"set {move.At / EventFacesPerSet} portrait {move.At % EventFacesPerSet + 1} "
                                    + $"0x{move.Graphic:X2} as 0x{move.Person:X2} (palette cap {caps[move.Person]})")
                    .ToList();
    }

    /// <summary>The graphic Brave Story means by each number the mod gives one of ReMixed's characters: its people are
    /// the remaster's own (LoreDictionary lists none of ReMixed's), so a number the game folds into another goes to
    /// that one (Gafgarion's 0x11 to 0x17, Malak's 0x12 to 0x1A), and one that holds a story character's face to that
    /// character's own sprite set (0x34 to Agrias's 0x1E). Returns what it changed.</summary>
    private static List<string> RemasterPeople()
    {
        var changed = new List<string>();
        for (var graphic = 0; graphic < Graphics; graphic++)
        {
            People[graphic] = graphic;
            if (Faces[graphic] < 0)
                continue;
            var folded = _fold(graphic);
            if (folded != graphic)
                People[graphic] = folded;
            else
            {
                var story = -1;
                for (var other = 0; other < Graphics; other++)
                    if (other != graphic && Faces[other] < 0 && _fold(other) == graphic)
                        story = story == -1 ? other : -2;
                if (story >= 0)
                    People[graphic] = story;
            }
            if (People[graphic] != graphic)
                changed.Add($"0x{graphic:X2} as 0x{People[graphic]:X2}");
        }
        return changed;
    }

    /// <summary>For the offline check and the hooks: the graphic Brave Story means by a number.</summary>
    internal static int Person(int graphic)
    {
        try
        {
            if ((uint)graphic < Graphics)
                return People[graphic];
        }
        catch (Exception e)
        {
            Failed(e);
        }
        return graphic;
    }

    [UnmanagedCallersOnly]
    private static int PersonFace(nint context, nint keys) => Person(_personFace(context, keys));

    [UnmanagedCallersOnly]
    private static int ChronicleFace(nint context, nint keys) => Person(_chronicleFace(context, keys));

    /// <summary>Gives each listed graphic whose number holds a story character's face (Ashley's 0x34, where the remaster
    /// keeps Agrias's) an unused PS1 portrait that gives it, and the portrait it shared with the story character's own
    /// sprite set (Agrias's 0x1E, which the game folds into 0x34) back to that sprite set. Returns what it moved.</summary>
    private static List<string> SeparatePortraits(GameImage image)
    {
        var reader = image.FindUnique(GraphicOfPortraitPattern);
        var graphics = (int*)GameImage.Rip(reader + 14, 3, 7);
        var portraits = (ushort*)(image.ImageStart + *(int*)(image.FindUnique(PortraitOfSpriteSetPattern) + 16));
        var events = (int*)(image.ImageStart + *(int*)(image.FindUnique(EventGraphicOfPortraitPattern) + 17));
        // Ramza's portraits and graphics (sprite sets 2 and 3: portraits 1 and 2, graphics 2 and 3, the event table's
        // too), and Agrias's portrait, which this leaves as it is.
        if (graphics[1] != 2 || graphics[2] != 3 || portraits[2] != 1 || portraits[3] != 2 || portraits[0x1E] != 0x17
            || events[1] != 2 || events[2] != 3)
            throw new InvalidDataException("the portrait tables aren't the ones this DLL expects");
        var unused = new Queue<int>();
        foreach (var portrait in UnusedPortraits)
            if (graphics[portrait] == -1)
                unused.Enqueue(portrait);
        var moves = new List<(int Graphic, int Shared, int Story, int Own)>();
        for (var graphic = 0; graphic < SpriteSets; graphic++)
        {
            if (Faces[graphic] < 0 || (portraits[graphic] & NoPortrait) != 0)
                continue;
            var shared = portraits[graphic] & Portrait;
            if (shared >= Portraits || graphics[shared] != graphic)
                continue;
            // The one sprite set with that portrait whose face the remaster keeps under this graphic's number.
            var story = -1;
            for (var other = 0; other < SpriteSets; other++)
                if (other != graphic && Faces[other] < 0 && (portraits[other] & (Portrait | NoPortrait)) == shared
                    && _fold(other) == graphic)
                    story = story == -1 ? other : -2;
            if (story < 0)
                continue;
            if (!unused.TryDequeue(out var own))
                throw new InvalidDataException($"no unused portrait left for graphic 0x{graphic:X2}");
            moves.Add((graphic, shared, story, own));
        }
        if (moves.Count > 0)
        {
            GameImage.Unprotected((nint)graphics, Portraits * sizeof(int), () =>
            {
                foreach (var move in moves)
                {
                    graphics[move.Shared] = move.Story;
                    graphics[move.Own] = move.Graphic;
                }
            });
            GameImage.Unprotected((nint)portraits, SpriteSets * sizeof(ushort), () =>
            {
                foreach (var move in moves)
                    portraits[move.Graphic] = (ushort)(portraits[move.Graphic] & ~Portrait | move.Own);
            });
            // An event box's portrait of 0x51 or less: the shared one now gives the story character's sprite set
            // there too (the own ones are past 0x51, so 0x274028's table, above, gives them).
            GameImage.Unprotected((nint)events, EventPortraits * sizeof(int), () =>
            {
                foreach (var move in moves)
                    if (move.Shared < EventPortraits && events[move.Shared] == move.Graphic)
                        events[move.Shared] = move.Story;
            });
        }
        GraphicOfPortraitAt = reader;
        GraphicOfPortrait = graphics;
        PortraitOfSpriteSet = portraits;
        EventGraphicOfPortrait = events;
        return moves.Select(move => $"0x{move.Graphic:X2} portrait 0x{move.Own:X2} (0x{move.Shared:X2} to "
                                    + $"0x{move.Story:X2}"
                                    + (move.Shared < EventPortraits && events[move.Shared] == move.Story ? ", events' too" : "")
                                    + ")").ToList();
    }

    /// <summary>For the offline check: a graphic's face set, or (-1) left to the game.</summary>
    internal static void Set(int graphic, int face) => Faces[graphic] = face;

    /// <summary>For the offline check and the hook: the face a box shows for a unit, given what the game found (a sheet
    /// slot under 10, else a graphic) and the unit's own graphic: the unit's graphic where the sheet's is one of ReMixed's
    /// (it has a face of its own) that the game's fold makes of the unit's (a story character on its second sprite set's
    /// sheet), else what the game found.</summary>
    internal static int BoxFaceOf(int found, int unitGraphic)
    {
        var graphic = found is >= 0 and < 10 && SheetSlots != null ? SheetSlots[found] : found;
        if ((uint)graphic < Graphics && Faces[graphic] >= 0 && unitGraphic >= 10 && unitGraphic != graphic
            && (uint)unitGraphic < Graphics && Faces[unitGraphic] < 0 && _fold(unitGraphic) == graphic)
            return unitGraphic;
        return found;
    }

    [UnmanagedCallersOnly]
    private static int BoxFace(nint layout, int slot, nint found, int* palette)
    {
        var face = _boxFace(layout, slot, found, palette);
        try
        {
            if (face < 0)
                NoFace(slot);
            if ((uint)slot <= 0x14 && _boxUnits != null)
                return BoxFaceOf(face, _boxUnits[slot * 0x200 + 0x18F]);
        }
        catch (Exception e)
        {
            Failed(e);
        }
        return face;
    }

    [UnmanagedCallersOnly]
    private static int Fold(int graphic)
    {
        try
        {
            if ((uint)graphic < Graphics && Faces[graphic] >= 0)
                return Faces[graphic];
        }
        catch (Exception e)
        {
            Failed(e);
        }
        return _fold(graphic);
    }

    private static int _noFaceSlot = -1, _noFaces;

    /// <summary>Logs a speech box drawn with no face: its unit has no sprite (or isn't a battle unit), as a line naming
    /// a unit the scene hasn't put on the field (a tester on 0.9.22: Agrias's line at the Yardrow armory). Once a slot
    /// in a row, and at most 32 times a run.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NoFace(int slot)
    {
        if (slot == _noFaceSlot || _noFaces >= 32)
            return;
        _noFaceSlot = slot;
        _noFaces++;
        _log.Info((uint)slot <= 0x14 && _boxUnits != null
            ? $"faces: a speech box's unit (battle unit {slot}, battle id 0x{_boxUnits[slot * 0x200 + 0x191]:X2}, "
              + $"graphic 0x{_boxUnits[slot * 0x200 + 0x18F]:X2}) has no sprite: no face"
            : $"faces: a speech box names no battle unit ({slot}): no face");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _log.Error($"faces: {e}");
    }
}
