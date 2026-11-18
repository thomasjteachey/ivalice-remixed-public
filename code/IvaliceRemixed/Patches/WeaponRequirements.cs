using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's weapon requirements per ability (docs/MECHANICS.md: "Weapon requirements per
/// ability"; PS1 0x80153BCC and 0x80153C8C, tables 0x80154030 and 0x801541A0): an ability with
/// an entry needs a weapon of one of its types in either hand. They replace vanilla's "requires
/// sword" and "requires Materia Blade" checks, so an ability without an entry needs no weapon.
/// Data: [[ability, mask]], bit 0x80000000 >> item type. Installed by <see cref="Rules"/>.
/// </summary>
/// <remarks>
/// The remaster checks in three places: the AI's candidates per skillset (0x316F8A, in 0x316D24;
/// PS1 0x8019A5F8) and two ability list builders, the action menu's (0x30E4DC, in 0x30E368, which
/// the command list's usable test and Math Skill also use; PS1 0x801817C0) and a copy that lists
/// a whole skillset, learned or not (0x30DEC4, in 0x30DD8C, from the battle UI). The PS1 has the
/// menu's and the AI's, and ReMixed changed both. Each test of the ability's flag byte (+6: 8
/// sword, 4 Materia Blade) against the unit's (+0x1B6) becomes a jump to a stub, which saves the
/// scratch registers, asks <see cref="Allows"/> (and <see cref="TwoSpells.Shows"/> and, in the lists,
/// <see cref="DragonTech.Shows"/>, with the list's skillset), and goes on to the game's own pass or fail branch with the registers as they
/// were. A failed check leaves the ability out of the list. (Labels fixed 2026-11-03: the first
/// was called the action menu and the other two the AI's.)
/// </remarks>
internal static unsafe class WeaponRequirements
{
    private static Game _game = null!;
    private static readonly uint[] Masks = new uint[Abilities];
    private static readonly bool[] Required = new bool[Abilities];
    private static int _failed;

    private const int Abilities = 368;
    private const int RightWeapon = 0x20, LeftWeapon = 0x24; // u16 item ids; 0xFF is an empty hand (type 0)
    private const int StubSize = 256;

    /// <summary>
    /// Where the ability id, the unit and the skillset are at a patched check: the AI's candidates
    /// (ebp, units + r13, esi), the whole-skillset list (esi, the battle index at the function's
    /// [rsp+0x28], no skillset register), the menu's lists (edi, the same index, r15d).
    /// </summary>
    internal enum Kind { AiCandidates, WholeList, MenuList }

    /// <summary>A patched check: its code, the stub it jumps to, and where the game goes on.</summary>
    internal readonly record struct Site(string Name, Kind Kind, nint At, int Length, nint Pass, nint Fail);

    /// <summary>For the offline check: the patched checks.</summary>
    internal static readonly List<Site> Sites = [];
    internal static nint Units { get; private set; }

    /// <summary>Patches the three checks. Returns how many abilities have a requirement.</summary>
    public static int Install(Game game, JsonElement data)
    {
        _game = game;
        var count = 0;
        foreach (var pair in data.EnumerateArray())
        {
            var ability = pair[0].GetInt32();
            Masks[ability] = pair[1].GetUInt32();
            Required[ability] = true;
            count++;
        }
        var image = game.Image;
        var units = Units = (nint)game.TurnCode.Units;
        var getAbility = (nint)game.RuleCode.GetAbilityAction;
        _ = game.Routines.GetItem; // found now, so a missing pattern stops this rule here

        // The AI's candidates: the ability in ebp, the unit at units + r13 (its flags read image-base-relative).
        var menu = image.FindUnique(
            "8B CD E8 ?? ?? ?? ?? 0F B6 48 06 F6 C1 08 74 ?? 4C 8D 05 ?? ?? ?? ?? 43 F6 84 28 ?? ?? ?? ?? 08 " +
            "EB ?? F6 C1 04 74 ?? 4C 8D 05 ?? ?? ?? ?? 43 F6 84 28 ?? ?? ?? ?? 04 0F 84 ?? ?? ?? ?? 41 BB 00 04 00 00");
        if (GameImage.Rip(menu + 2, 1, 5) != getAbility || GameImage.Rip(menu + 16, 3, 7) != image.ImageStart
            || image.ImageStart + *(int*)(menu + 27) != units + 0x1B6 || image.ImageStart + *(int*)(menu + 50) != units + 0x1B6)
            throw new InvalidDataException("the AI's weapon check isn't the code this DLL expects");
        // The two list builders: the ability in esi or edi, the unit's battle index in the function's [rsp+0x28].
        var ai1 = image.FindUnique(
            "8B CE E8 ?? ?? ?? ?? 45 33 DB 48 8B D0 F6 40 06 08 74 ?? 84 DB 0F 84 ?? ?? ?? ?? " +
            "F6 40 06 04 74 ?? 44 38 5C 24 22 0F 84 ?? ?? ?? ?? 0F B6 40 0D");
        var ai2 = image.FindUnique(
            "8B CF E8 ?? ?? ?? ?? 45 33 DB 48 8B D0 F6 40 06 08 74 ?? 44 38 5C 24 21 0F 84 ?? ?? ?? ?? " +
            "F6 40 06 04 74 ?? 44 38 5C 24 22 0F 84 ?? ?? ?? ?? 0F B6 40 0D");
        var ai1Index = image.FindUnique("8B C1 89 4C 24 28 49 8B F1 48 8B 8C 24 80 01 00 00");
        var ai2Index = image.FindUnique("44 0F B6 FA 89 4C 24 28 89 7C 24 24 E8");
        if (GameImage.Rip(ai1 + 2, 1, 5) != getAbility || GameImage.Rip(ai2 + 2, 1, 5) != getAbility
            || GameImage.Rip(ai1 + 21, 2, 6) != GameImage.Rip(ai1 + 38, 2, 6) || GameImage.Rip(ai2 + 24, 2, 6) != GameImage.Rip(ai2 + 41, 2, 6)
            || (ulong)(ai1 - ai1Index) > 0x200 || (ulong)(ai2 - ai2Index) > 0x200)
            throw new InvalidDataException("the list builders' weapon checks aren't the code this DLL expects");

        Site[] sites =
        [
            new("the AI's candidates", Kind.AiCandidates, menu + 7, 54, menu + 61, GameImage.Rip(menu + 55, 2, 6)),
            new("the whole-skillset list", Kind.WholeList, ai1 + 13, 31, ai1 + 44, GameImage.Rip(ai1 + 21, 2, 6)),
            new("the menu's ability lists", Kind.MenuList, ai2 + 13, 34, ai2 + 47, GameImage.Rip(ai2 + 24, 2, 6)),
        ];
        var stubs = GameImage.AllocateCode(sites.Length * StubSize);
        for (var k = 0; k < sites.Length; k++)
            WriteStub(stubs + k * StubSize, sites[k].Kind, sites[k].Pass, sites[k].Fail);
        foreach (var (site, k) in sites.Select((s, k) => (s, k)))
        {
            var jump = new byte[site.Length];
            Array.Fill(jump, (byte)0x90);
            AbsoluteJump(jump, 0, (nint)(stubs + k * StubSize));
            GameImage.WriteCode(site.At, jump);
            Sites.Add(site);
        }
        return count;
    }

    /// <summary>
    /// A check's stub: save rax, rcx, rdx, r8–r11 and xmm0–5 (in the middle of a function anything
    /// can be live), call the C# check with the ability in ecx, the unit (or its battle index) in
    /// edx and the list's skillset in r8d (-1 where it isn't in a register), restore, then jump to
    /// the game's pass or fail branch. Entered by a jump from code whose stack is 16-aligned (a
    /// function body), so with 7 pushes and 0x88 more the call is aligned too.
    /// </summary>
    internal static void WriteStub(byte* stub, Kind kind, nint pass, nint fail)
    {
        var code = new List<byte>
        {
            0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, // push rax, rcx, rdx, r8, r9, r10, r11
            0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00,                         // sub rsp, 0x88
        };
        Stub.SaveXmm(code, 0x20); // mid-function, anything can be live
        nint check;
        switch (kind)
        {
            case Kind.AiCandidates:
                code.AddRange([0x8B, 0xCD]);                                  // mov ecx, ebp
                code.AddRange([0x48, 0xBA]);                                  // mov rdx, units
                code.AddRange(BitConverter.GetBytes((ulong)Units));
                code.AddRange([0x4C, 0x01, 0xEA]);                            // add rdx, r13
                code.AddRange([0x44, 0x8B, 0xC6]);                            // mov r8d, esi
                check = (nint)(delegate* unmanaged<int, byte*, int, int>)&AllowsUnit;
                break;
            default:
                code.AddRange(new byte[] { 0x8B, kind == Kind.WholeList ? (byte)0xCE : (byte)0xCF }); // mov ecx, esi / edi
                code.AddRange([0x8B, 0x94, 0x24, 0xE8, 0x00, 0x00, 0x00]);    // mov edx, [rsp+0xE8]: the function's [rsp+0x28]
                if (kind == Kind.MenuList)
                    code.AddRange([0x45, 0x8B, 0xC7]);                        // mov r8d, r15d
                else
                    code.AddRange([0x41, 0xB8, 0xFF, 0xFF, 0xFF, 0xFF]);      // mov r8d, -1
                check = (nint)(delegate* unmanaged<int, int, int, int>)&AllowsIndex;
                break;
        }
        code.AddRange([0x48, 0xB8]);                                          // mov rax, check
        code.AddRange(BitConverter.GetBytes((ulong)check));
        code.AddRange([0xFF, 0xD0]);                                          // call rax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);            // add rsp, 0x88
        code.AddRange([0x85, 0xC0]);                                          // test eax, eax
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax (flags kept)
        code.AddRange([0x74, 0x0E]);                                          // jz fail
        var bytes = code.ToArray();
        var at = bytes.Length;
        Array.Resize(ref bytes, at + 28);
        AbsoluteJump(bytes, at, pass);
        AbsoluteJump(bytes, at + 14, fail);
        if (bytes.Length > StubSize)
            throw new InvalidOperationException("the weapon check stub is too big");
        bytes.CopyTo(new Span<byte>(stub, StubSize));
    }

    /// <summary>jmp [rip+0] with the target after it: 14 bytes that reach anywhere.</summary>
    private static void AbsoluteJump(byte[] code, int at, nint target)
    {
        code[at] = 0xFF;
        code[at + 1] = 0x25;
        BitConverter.TryWriteBytes(code.AsSpan(at + 2), 0);
        BitConverter.TryWriteBytes(code.AsSpan(at + 6), (ulong)target);
    }

    [UnmanagedCallersOnly]
    private static int AllowsUnit(int ability, byte* unit, int skillset)
    {
        try
        {
            return Allows(ability, unit) && TwoSpells.Shows(unit, skillset, ability) ? 1 : 0;
        }
        catch (Exception e)
        {
            Failed(e);
            return 1;
        }
    }

    [UnmanagedCallersOnly]
    private static int AllowsIndex(int ability, int index, int skillset)
    {
        try
        {
            if ((uint)index >= Battle.UnitCount)
                return 1;
            var unit = (byte*)Units + index * Battle.UnitSize;
            return Allows(ability, unit) && TwoSpells.Shows(unit, skillset, ability) && DragonTech.Shows(skillset, ability) ? 1 : 0;
        }
        catch (Exception e)
        {
            Failed(e);
            return 1;
        }
    }

    /// <summary>
    /// ReMixed's rule: no entry, no requirement; otherwise the right- or left-hand weapon's item
    /// type has to be in the ability's mask (PS1 unit +0x1D and +0x1F, here +0x20 and +0x24).
    /// </summary>
    internal static bool Allows(int ability, byte* unit)
    {
        if ((uint)ability >= Abilities || !Required[ability])
            return true;
        var mask = Masks[ability];
        if (Fits(mask, *(ushort*)(unit + RightWeapon)) || Fits(mask, *(ushort*)(unit + LeftWeapon)))
            return true;
        Tracer.Line(_game.Log, $"weapons: ability 0x{ability:X3} needs another weapon (mask 0x{mask:X8})");
        return false;
    }

    /// <summary>An item's type is one the mask allows. Types run past 31 on the remaster (Throwing, Bomb, Item); no mask has those.</summary>
    internal static bool Fits(uint mask, ushort item)
    {
        var type = ItemType(item);
        return type < 32 && (mask & (0x80000000u >> type)) != 0;
    }

    internal static int ItemType(ushort item)
    {
        var record = _game.Routines.GetItem(item);
        return record == null ? 0 : record[5];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"weapons: the check failed: {e}");
    }
}
