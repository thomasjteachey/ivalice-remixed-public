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
/// same (ReMixed gives Ashley's 0x34 a portrait of her own). Event speech boxes that name an EVTFACE portrait take it
/// through another table (0x714D50), left as it is: ReMixed redrew part of EVTFACE, and which of its events name one
/// isn't known.
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
        var people = new List<string>();
        try
        {
            people = RemasterPeople();
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
               + $"Brave Story's people the remaster's)";
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
        // Ramza's portraits and graphics (sprite sets 2 and 3: portraits 1 and 2, graphics 2 and 3), and Agrias's
        // portrait, which this leaves as it is.
        if (graphics[1] != 2 || graphics[2] != 3 || portraits[2] != 1 || portraits[3] != 2 || portraits[0x1E] != 0x17)
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
        }
        GraphicOfPortraitAt = reader;
        GraphicOfPortrait = graphics;
        PortraitOfSpriteSet = portraits;
        return moves.Select(move => $"0x{move.Graphic:X2} portrait 0x{move.Own:X2} (0x{move.Shared:X2} to "
                                    + $"0x{move.Story:X2})").ToList();
    }

    /// <summary>For the offline check: a graphic's face set, or (-1) left to the game.</summary>
    internal static void Set(int graphic, int face) => Faces[graphic] = face;

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _log.Error($"faces: {e}");
    }
}
