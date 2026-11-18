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
/// </remarks>
internal static unsafe class RemixedFaces
{
    /// <summary>The fold (0x0F8338: ecx the graphic, eax the face): mov edx, ecx; cmp ecx, 0x38; jg; je; cmp ecx,
    /// 0x2A; jg; je; sub ecx, 0x11; je; sub ecx, 1; je.</summary>
    internal const string FoldPattern =
        "8B D1 83 F9 38 0F 8F ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 83 F9 2A 7F ?? 74 ?? 83 E9 11 74 ?? 83 E9 01 74";
    private const int Graphics = 0x100;

    /// <summary>The face by graphic (-1: the game's fold).</summary>
    private static readonly int[] Faces = NewFaces();
    private static delegate* unmanaged<int, int> _fold; // the original
    private static Log _log = null!;
    private static int _failed;

    /// <summary>The fold as found, for the offline check (calling it runs the hook).</summary>
    internal static nint FoldAt { get; private set; }

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
        return $"ReMixed's characters' faces ({given.Count} graphics)";
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
