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
    /// <summary>The graphic from a unit's sprite set and job (0x278EDC: the unit, its palette out, the job), which the
    /// recompute and the menus' units (the roster screen, a unit's status, the formation, each a battle unit built from
    /// the roster record by 0x275B84) call: Dancer and Mime as the job before; generics by job; 0xA6-0xA9 fixed; and
    /// past 0xA9, with the remaster's 0x83-0xA5 others, job + 0x28. So the menus drew Lorne (0xB4, Bard 0x5B) and Bryn
    /// (0xB5, Dancer 0x5C, as 0x5B) both as 0x83, the Dancer (seen on the PC, 0.9.7).</summary>
    internal const string PickerPattern =
        "45 0F B6 C8 4C 8B D1 C6 02 00 41 83 E9 5C 74 06 41 83 F9 01 75 04 41 80 C0 FF 0F B6 09 B8 A5 00 00 00 3B C8";
    internal const int GraphicAt = 0x18F, PaletteAt = 0x190, BattleIdAt = 0x191;
    /// <summary>ReMixed's locked looks: a sprite set from here up encodes a job's look (SCUS 0x80154DC0).</summary>
    internal const int LockedFrom = 0x90;
    /// <summary>The remaster's own characters in its added scenes (event_btlevt_bin_entrydata_add_ent.bin: 0xA2, 0xA3
    /// and 0xA5-0xA9), which the picker draws as they are; ReMixed's locked Thief, Mediator, Oracle and Geomancer looks
    /// (0xA2-0xA8) share those numbers and go the game's way.</summary>
    internal const int RemasterFirst = 0xA2, RemasterLast = 0xA9;

    /// <summary>A sprite set that is one of ReMixed's locked looks and none of the remaster's.</summary>
    internal static bool IsLocked(int sprite) => sprite >= LockedFrom && (sprite < RemasterFirst || sprite > RemasterLast);
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
        PatchPicker(game.Image);
        PatchBaseJobs(game.Image);
        _roster = game.Units.Roster;
        PortraitAt = game.Image.FindUnique(PortraitPattern);
        _portrait = (delegate* unmanaged<int, nint, nint, int*, int>)game.Hooks.Hook(
            PortraitAt, (nint)(delegate* unmanaged<int, nint, nint, int*, int>)&Portrait, "a roster unit's portrait");
    }

    /// <summary>A roster unit's PS1 portrait for the world menus (0x32AB3C: ecx the roster slot, rdx what 0x2E16A8 fills
    /// in, r9 its palette out), the one reader of the portrait table (.rdata 0x683A70): a special character by its sprite
    /// set, a generic (0x80, 0x81) by its job's graphic (job × 2 − 0x34, a woman's − 0x35), and any other sprite set
    /// from 0x83 by its job row's monster byte (+0x2E), 0 for a human job, and table entry 0 is Ramza's chapter 1
    /// portrait. So a locked look showed Ramza's face on its card, the formation list and its join screen (Elias, 0x9C,
    /// a Black Mage, at 1-A's river camp: a tester on 0.9.22). mov rax, rsp; four saves; push r15; sub rsp, 0x20;
    /// xor r11d, r11d; lea r15, [image]; or esi, -1; mov rbp, rdx; mov r10d, r11d; cmp ecx, 0x36.</summary>
    internal const string PortraitPattern =
        "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 57 48 83 EC 20 45 33 DB 4C 8D 3D ?? ?? ?? ?? " +
        "83 CE FF 48 8B EA 45 8B D3 83 F9 36";
    /// <summary>A roster record's gender bits (0x80 a man), as a battle unit's +6.</summary>
    internal const int RosterGenderAt = 4;
    internal static nint PortraitAt { get; private set; }
    private static byte* _roster;
    private static delegate* unmanaged<int, nint, nint, int*, int> _portrait; // the original

    /// <summary>Use a locked look's graphic as the PS1 portrait table index wherever it is under 0x80. Higher graphics
    /// keep the stock job lookup. The enhanced roster menus resolve the effective graphic in <see cref="RemixedFaces"/>
    /// because the PS1 portrait table does not cover every graphic, including Lorne's 0x38.</summary>
    [UnmanagedCallersOnly]
    private static int Portrait(int slot, nint drawn, nint palette8, int* palette)
    {
        byte* record = null;
        byte sprite = 0;
        try
        {
            if (_roster != null && (uint)slot < UnitTables.RosterSlots)
            {
                record = _roster + slot * UnitTables.RosterSize;
                if (IsLocked(record[0]) && LockedGraphic(record[0], record[RosterGenderAt]) is var look and < 0x80)
                {
                    sprite = record[0];
                    record[0] = (byte)look;
                }
            }
        }
        catch (Exception e)
        {
            Failed(e);
        }
        try
        {
            return _portrait(slot, drawn, palette8, palette);
        }
        finally
        {
            if (sprite != 0)
                record[0] = sprite;
        }
    }

    /// <summary>The picker as found, for the offline check (calling it runs the patched picker).</summary>
    internal static nint PickerAt { get; private set; }

    /// <summary>The picker's last case (0x278F88, from its entry): sprite sets 0x83-0xA1, 0xA4 and past 0xA9, and the
    /// special characters' own (under 0x80): movzx ecx, byte [r10]; add r8b, 0x28; cmp byte [r10], 0x80;
    /// movzx eax, r8b; cmovbe eax, ecx; ret. 20 bytes, entered only by jumps from above it.</summary>
    internal const int DefaultCaseAt = 0xAC;
    internal static readonly byte[] DefaultCase =
        [0x41, 0x0F, 0xB6, 0x0A, 0x41, 0x80, 0xC0, 0x28, 0x41, 0x80, 0x3A, 0x80, 0x41, 0x0F, 0xB6, 0xC0, 0x0F, 0x46, 0xC1, 0xC3];

    /// <summary>
    /// The menus draw a unit by the picker (0x278EDC) alone, so a locked look's graphic comes from there: its last case
    /// becomes a jump (mov rax, stub; jmp rax) to <see cref="PickerCode"/>, which gives a locked look ReMixed's graphic
    /// and anything else the case's own. Not a C# hook: the picker is a leaf, and its callers keep registers across it
    /// that a hook wouldn't (r11 in the recompute 0x0FCAB0, rdx in 0x270CE4; ExeTool liveregs, and the offline check
    /// crashed). The stub touches what the case did (rax, rcx, r8, the flags); the palette is 0 already.
    /// </summary>
    private static void PatchPicker(GameImage image)
    {
        var picker = image.FindUnique(PickerPattern);
        var at = picker + DefaultCaseAt;
        if (!new ReadOnlySpan<byte>((void*)at, DefaultCase.Length).SequenceEqual(DefaultCase))
            throw new InvalidDataException("the picker's last case isn't the one this DLL expects");
        var code = PickerCode();
        var stub = GameImage.AllocateCode(code.Length);
        code.CopyTo(new Span<byte>(stub, code.Length));
        var jump = new byte[DefaultCase.Length];
        Array.Fill(jump, (byte)0xCC);
        jump[0] = 0x48; // mov rax, stub
        jump[1] = 0xB8;
        BitConverter.GetBytes((long)stub).CopyTo(jump, 2);
        jump[10] = 0xFF; // jmp rax
        jump[11] = 0xE0;
        GameImage.WriteCode(at, jump);
        PickerAt = picker;
    }

    /// <summary>The picker's last case with ReMixed's locked looks (<see cref="LockedGraphic"/>, for a sprite set
    /// <see cref="IsLocked"/>): r10 the unit, r8b the job; the graphic in al.</summary>
    internal static byte[] PickerCode()
    {
        var a = new Asm();
        a.Emit(0x41, 0x0F, 0xB6, 0x0A);                            // movzx ecx, byte [r10]: the sprite set
        a.Cmp(0xF9, LockedFrom);                                    // cmp ecx
        a.Jump(0x72, "game");                                       // jb: none of ReMixed's
        a.Cmp(0xF9, RemasterFirst);
        a.Jump(0x72, "locked");
        a.Cmp(0xF9, RemasterLast);
        a.Jump(0x76, "game");                                       // jbe: the remaster's own
        a.Label("locked");
        foreach (var (sprite, graphic) in new[] { (0xB2, 0x82), (0xB3, 0x37), (0xB4, 0x38), (0xB5, 0x83) })
        {
            a.Emit(0xB8, (byte)graphic, 0, 0, 0);                   // mov eax, graphic
            a.Cmp(0xF9, sprite);
            a.Jump(0x74, "done");                                   // je
        }
        a.Emit(0x8D, 0x41, 0xD0);                                   // lea eax, [rcx - 0x30]
        a.Cmp(0xF9, 0xB5);
        a.Jump(0x76, "man");                                        // jbe: no gap to close
        a.Emit(0x83, 0xE8, 0x02);                                   // sub eax, 2
        a.Label("man");
        a.Emit(0x41, 0xF6, 0x42, 0x06, 0x80);                       // test byte [r10 + 6], 0x80: a man
        a.Jump(0x75, "done");                                       // jnz
        a.Emit(0xFF, 0xC0);                                         // inc eax: a woman's
        a.Label("done");
        a.Emit(0xC3);                                               // ret
        a.Label("game");                                            // the case as the game has it, after its movzx
        a.Emit(DefaultCase.AsSpan(4).ToArray());
        return a.Build();
    }

    // These five copies classify 0x80/0x81 as Squire, 0x82 as the monster's actual job and other sprites as jobs.
    // Locked looks have their own sprite IDs, so using them as jobs can index past the job table. The two preview
    // builders use the same classification to choose the generic job display.
    private static readonly (string Pattern, int Offset, bool UsesR10)[] BaseJobCases =
    [
        // 0x2864AC: the party job list, including the Bardsong crash at 0x286826.
        ("48 8B C4 48 89 58 10 48 89 68 18 48 89 70 20 48 89 48 08 57 41 54 41 55 41 56 41 57 48 83 EC 20 " +
         "45 0F B6 48 02 49 8B F8 45 8B D1 44 8B E2 4C 8B F9 41 81 EA 80 00 00 00 74 12 41 83 EA 01 74 0C " +
         "41 83 FA 01 75 0C 45 0F BF 08 EB 06 41 B9 4A 00 00 00 B9 00 40 00 00", 0x31, true),
        // 0x2863E4: the shared menu lookup, including the empty Reaction slot crash at 0x28A42C.
        ("48 0F BF C1 4C 8D 05 ?? ?? ?? ?? 4D 8B 04 C0 41 0F B6 50 72 8B CA 81 E9 80 00 00 00 74 11 " +
         "83 E9 01 74 0C 83 F9 01 75 0C 41 0F BF 50 24 EB 05 BA 4A 00 00 00 8B C2 C3", 0x16, false),
        // 0x39254A: the older ability screen's job list, consumed by 0x392620.
        ("41 0F B6 50 72 49 8D 40 78 8B CA 48 89 05 ?? ?? ?? ?? 81 E9 80 00 00 00 74 11 " +
         "41 2B CF 74 0C 41 3B CF 75 0C 41 0F BF 50 24 EB 05 BA 4A 00 00 00 66 89 15", 0x12, false),
        // 0x2C18DB: the equipment preview's job display.
        ("41 0F B6 57 02 44 0F BF 46 02 8B CA 41 8A 07 44 89 45 B8 88 45 C0 81 E9 80 00 00 00 74 0F " +
         "83 E9 01 74 0A 83 F9 01 75 0A 41 8B D0 EB 05 BA 4A 00 00 00 89 55 BC", 0x16, false),
        // 0x337AA2: the shop equipment comparison's job display.
        ("0F B6 51 72 8B CA 88 85 78 01 00 00 81 E9 80 00 00 00 74 1B 83 E9 01 74 16 83 F9 01 74 08 " +
         "89 95 74 01 00 00 EB 13 44 89 85 74 01 00 00 EB 0A C7 85 74 01 00 00 4A 00 00 00", 0x0C, false),
    ];

    // Function entries for the offline check.
    internal static nint JobListAt { get; private set; }
    internal static nint AbilityBaseJobAt { get; private set; }

    private static void PatchBaseJobs(GameImage image)
    {
        var entries = BaseJobCases.Select(site => image.FindUnique(site.Pattern)).ToArray();
        for (var i = 0; i < entries.Length; i++)
        {
            var (_, offset, usesR10) = BaseJobCases[i];
            PatchBaseJobCase(entries[i] + offset, usesR10);
        }
        JobListAt = entries[0];
        AbilityBaseJobAt = entries[1];
    }

    private static void PatchBaseJobCase(nint at, bool usesR10)
    {
        // Normalize only the register the game's generic test subtracts from. Its result and flags feed the
        // original branches, including the monster case. The roster and UI records keep the actual sprite set.
        var rex = (byte)(usesR10 ? 0x41 : 0);
        var compare = (byte)(usesR10 ? 0xFA : 0xF9);                // cmp r10d or ecx
        var length = usesR10 ? 7 : 6;                               // sub r10d/ecx, 0x80
        var a = new Asm();
        a.Cmp(compare, LockedFrom, rex);
        a.Jump(0x72, "game");                                       // jb
        a.Cmp(compare, RemasterFirst, rex);
        a.Jump(0x72, "generic");
        a.Cmp(compare, RemasterLast, rex);
        a.Jump(0x76, "game");                                       // jbe
        a.Label("generic");
        if (usesR10)
            a.Emit(0x41, 0xBA, 0x80, 0, 0, 0);                     // mov r10d, 0x80
        else
            a.Emit(0xB9, 0x80, 0, 0, 0);                           // mov ecx, 0x80
        a.Label("game");
        a.Emit(new ReadOnlySpan<byte>((void*)at, length).ToArray());
        a.Emit(0xFF, 0x25, 0, 0, 0, 0);                             // jmp [rip], preserving the subtraction's flags
        a.Emit(BitConverter.GetBytes((long)(at + length)));
        var code = a.Build();
        var stub = GameImage.AllocateCodeNear(at, code.Length);
        code.CopyTo(new Span<byte>(stub, code.Length));

        Span<byte> jump = stackalloc byte[length];
        jump.Fill(0x90);
        jump[0] = 0xE9;
        BitConverter.TryWriteBytes(jump[1..], checked((int)((nint)stub - (at + 5))));
        GameImage.WriteCode(at, jump);
    }

    /// <summary>A few bytes of x64 with short jumps to labels, for the stubs above.</summary>
    private sealed class Asm
    {
        private readonly List<byte> _code = [];
        private readonly List<(int At, string Label)> _fixups = [];
        private readonly Dictionary<string, int> _labels = [];

        public void Emit(params byte[] bytes) => _code.AddRange(bytes);

        /// <summary>cmp reg, imm32 (ModRM <paramref name="modrm"/> selects the register with the optional REX prefix).</summary>
        public void Cmp(byte modrm, int value, byte rex = 0)
        {
            if (rex != 0)
                _code.Add(rex);
            Emit(0x81, modrm);
            Emit(BitConverter.GetBytes(value));
        }

        /// <summary>A short jcc (or jmp, 0xEB) to a label.</summary>
        public void Jump(byte op, string label)
        {
            Emit(op, 0);
            _fixups.Add((_code.Count - 1, label));
        }

        public void Label(string name) => _labels[name] = _code.Count;

        public byte[] Build()
        {
            foreach (var (at, label) in _fixups)
                _code[at] = (byte)checked((sbyte)(_labels[label] - (at + 1)));
            return [.. _code];
        }
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
            if (IsLocked(unit[0]))
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
