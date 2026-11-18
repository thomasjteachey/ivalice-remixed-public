using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// A unit's look as ReMixed's events give it: they write the graphic to draw (PS1 battle unit +0x15F, the remaster's
/// +0x18F, with its palette at +0x160 and +0x190) and add the unit again, and on the PS1 that graphic stays. The
/// remaster works a unit's graphic out again from its sprite set (+0) and job (+3) whenever a box shows the unit's face
/// (0x0FCAB0, through 0x278EDC, from the face's layout 0x22AA88) and writes it back, so a look an event gave went back
/// to the sprite set's: the dispatch tavern's units are Ramza placeholders (sprite set 1) in their battle data, and
/// the unit picked for a group walked in as Ramza (seen on the PC, 2026-11-11; found with a write watch, 2026-11-12).
/// Installed with ReMixed's events (<see cref="RemixedEvents"/>).
/// </summary>
/// <remarks>
/// A unit whose graphic a ReMixed event wrote keeps it: the recompute hands back +0x18F and +0x190 as they are (and the
/// sprite object's palette, as the game's does). The mark lasts while the unit's graphic and its battle id (+0x191) are
/// the ones the event left; once the remaster sets the slot up anew they differ, and the game's own recompute runs.
/// ReMixed's graphics are the PS1's numbers, which the remaster keeps (a generic 0x60 + 2 × its job's index, + 1 for a
/// woman; a monster its job's graphic + 0x85, the remaster's job table holding the sum).
/// A roster unit with a sprite set from 0x90 also keeps ReMixed's locked look. Those sprite sets encode a job look
/// rather than naming a sheet directly; the remaster otherwise treats sets past its own table as job + 0x28 and draws
/// Bryn, Lorne and locked generics as silhouettes.
/// </remarks>
internal static unsafe class UnitLooks
{
    // The recompute (0x0FCAB0: the slot, the graphic and palette to return): the unit (slot under 0x15), the graphic
    // from 0x278EDC into +0x18F and the palette into +0x190, then the sprite object's palette (0x260ABC, its +0xB).
    internal const string RecomputePattern =
        "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 48 63 F9 49 8B D8 48 8B F2 83 FF 14 77 13 4C 8B DF " +
        "48 8D 05 ?? ?? ?? ?? 49 C1 E3 09 4C 03 D8 EB 03 45 33 DB 41 C6 00 00 48 8B D3 45 8A 43 03 49 8B CB " +
        "E8 ?? ?? ?? ?? 8A 0B 41 88 8B 90 01 00 00 0F B7 CF 41 88 83 8F 01 00 00 88 06 " +
        "E8 ?? ?? ?? ?? 48 85 C0 74 05 8A 40 0B 88 03";
    internal const int GraphicAt = 0x18F, PaletteAt = 0x190, BattleIdAt = 0x191;
    private const int Slots = Battle.UnitCount;

    private static byte* _units;
    private static delegate* unmanaged<int, byte*, byte*, void> _recompute; // the original
    private static delegate* unmanaged<long, nint> _spriteObject;
    /// <summary>Per slot: the graphic and battle id an event left (+1 so 0 is "no mark").</summary>
    private static readonly int[] Kept = new int[Slots];
    private static Log _log = null!;
    private static int _failed;

    /// <summary>The recompute as found, for the offline check (calling it runs the hook).</summary>
    internal static nint RecomputeAt { get; private set; }

    public static void Install(Game game, byte* units)
    {
        _log = game.Log;
        var at = game.Image.FindUnique(RecomputePattern);
        var calls = GameImage.Calls(at, RecomputePattern, "the graphic's recompute");
        if ((byte*)GameImage.Rip(at + 0x20, 3, 7) != units)
            throw new InvalidDataException("the graphic's recompute doesn't read the battle units this DLL knows");
        _units = units;
        _spriteObject = (delegate* unmanaged<long, nint>)calls[1];
        _recompute = (delegate* unmanaged<int, byte*, byte*, void>)game.Hooks.Hook(
            at, (nint)(delegate* unmanaged<int, byte*, byte*, void>)&Recompute, "the graphic's recompute");
        RecomputeAt = at;
    }

    /// <summary>A ReMixed event wrote battle unit <paramref name="slot"/>'s graphic (PS1 +0x15F): it keeps it.</summary>
    internal static void Mark(uint slot)
    {
        if (_units == null || slot >= Slots)
            return;
        var unit = _units + slot * Battle.UnitSize;
        Kept[slot] = (unit[GraphicAt] | unit[BattleIdAt] << 8) + 1;
    }

    /// <summary>For the offline check: the marks dropped.</summary>
    internal static void Clear() => Array.Clear(Kept);

    /// <summary>The graphic encoded by one of ReMixed's locked-look sprite sets (SCUS 0x80154DC0).</summary>
    internal static int LockedGraphic(int sprite, int gender) => sprite switch
    {
        0xB2 => 0x82,
        0xB3 => 0x37,
        0xB4 => 0x38,
        0xB5 => 0x83,
        _ => (sprite > 0xB5 ? sprite - 2 : sprite) - 48 + ((gender & 0x80) != 0 ? 0 : 1),
    };

    [UnmanagedCallersOnly]
    private static void Recompute(int slot, byte* graphic, byte* palette)
    {
        try
        {
            if ((uint)slot < Slots && Kept[slot] != 0)
            {
                var unit = _units + slot * Battle.UnitSize;
                if (Kept[slot] == (unit[GraphicAt] | unit[BattleIdAt] << 8) + 1)
                {
                    *graphic = unit[GraphicAt];
                    *palette = unit[PaletteAt];
                    var sprite = _spriteObject(slot & 0xFFFF);
                    if (sprite != 0)
                        *palette = *(byte*)(sprite + 0xB);
                    return;
                }
                Kept[slot] = 0; // the remaster set the slot up anew
            }
        }
        catch (Exception e)
        {
            Failed(e);
        }
        _recompute(slot, graphic, palette);
        if ((uint)slot < Slots)
        {
            var unit = _units + slot * Battle.UnitSize;
            if (unit[0] >= 0x90)
            {
                *graphic = (byte)LockedGraphic(unit[0], unit[6]);
                unit[GraphicAt] = *graphic;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _log.Error($"unit looks: {e}");
    }
}
