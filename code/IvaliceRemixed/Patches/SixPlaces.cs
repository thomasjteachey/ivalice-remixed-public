namespace IvaliceRemixed.Patches;

/// <summary>
/// Six of the player's units in a battle, as ReMixed deploys in its battles with two groups of 4 + 2 (side chapter
/// 1-A, with Bryn and Lorne along) and 2 + 4 (the Argath campaign's Hokuten battles). The remaster's formation (its
/// GenerateAttackTeamManager) counts both groups' places (formation +8) into its +0x40 as it opens (0x1EF0D8) and keeps
/// the units placed in a list of five at its +0x44; with six places Start opened nothing ("Commence battle?" only for
/// 1 to 5, 0x1EE890), so those battles never began (0x0F0's, seen on the PC 2026-11-12). Installed by
/// <see cref="Rules"/> with "six-places".
/// </summary>
/// <remarks>
/// The list gets a sixth entry at +0x58, a qword the constructor (0x1EE21C) zeroes and nothing in the formation reads;
/// +0x5C stays 0. The limits of five become six: the count (0x1EE6AC), the compaction (0x1EE6DC, whose copy on its
/// stack has room for six in its 0x28-byte frame), the commence check, an entry's group (0x1EE734, which looked for its
/// battle unit in the party's slots 0x10–0x14 only, so the sixth counted in no group: "Group 2 - Remaining: 1" with both
/// placed, seen on the PC 2026-11-19; now in all 21), the unit's group lookup (0x1F3E3C), the cursor's
/// walk back from the last entry (0x1F4898) and the screen's refresh of its group icons (0x1F3858, five for the first
/// group and four for the second: enough for 4 + 2 and 2 + 4). The two places that empty the list (the reset 0x1EE518
/// and the setup 0x1EE9B4) write −1 in the sixth entry too, from the xmm0 of −1s they write the first five from. A
/// placed unit past the party's five battle slots (0x10–0x14) takes a free one of 0–15: the remaster's own placing does
/// that during a formation (0x270FBC, battle state 0x53), from 0 up; here from 15 down, as ReMixed puts its party past
/// five in the battle's last slots (its level scaling's re-run keeps the first 16 − (var 0x52 − 5) as the battle
/// data's), which its 4 + 2 battles leave empty, so a low slot an event fills later (a unit added by its battle id)
/// isn't taken. The battle end's write-back looks for a roster slot's unit in all 21 (0x2C60E4).
/// </remarks>
internal static unsafe class SixPlaces
{
    /// <summary>A site: what it is, its pattern, where the change starts in it, the bytes there and what they become.</summary>
    internal readonly record struct Site(string What, string Pattern, int At, byte[] Was, byte[] Now);

    internal static readonly Site[] Sites =
    [
        // cmp dword [rcx+0x40], 5; jbe; xor eax, eax; ret; movsxd rdx, [rcx+0x40]
        new("the count of units placed (0x1EE6AC)", "83 79 40 05 76 03 33 C0 C3 48 63 51 40", 3, [5], [6]),
        // sub rsp, 0x28; mov edx, 5; cmp [rcx+0x40], edx; ja
        new("the list's compaction (0x1EE6DC)", "48 83 EC 28 BA 05 00 00 00 39 51 40 77 42", 5, [5], [6]),
        // sub eax, r15d; mov rbx, rcx; cmp eax, 4; ja (places − 1 at most 4)
        new("the commence check (0x1EE890)", "41 2B C7 48 8B D9 83 F8 04 0F 87", 8, [4], [5]),
        // add rdi, 4; cmp esi, 5
        new("a unit's group lookup (0x1F3E3C)", "48 83 C7 04 83 FE 05", 6, [5], [6]),
        // mov edx, 0x10; cmp edx, 0x14; ja: an entry's battle unit, looked for in slots 0x10-0x14 (its loop ends at
        // edx − 0x10 = 5) → from 0, all 21: the sixth is in one of 0-15, and units not from the roster have +2 0xFF
        new("an entry's battle unit (0x1EE734)", "BA 10 00 00 00 83 FA 14 77 13", 1, [0x10], [0x00]),
        // mov ecx, 4; lea r8, [r9+0x54]: the walk back from the last entry
        new("the cursor's walk (0x1F4898)", "B9 04 00 00 00 4D 8D 41 54 48 85 C9", 1, [4, 0, 0, 0, 0x4D, 0x8D, 0x41, 0x54],
            [5, 0, 0, 0, 0x4D, 0x8D, 0x41, 0x58]),
        // add r12, 4; cmp r13d, 5; jl: the group icons' refresh
        new("the screen's refresh (0x1F3858)", "49 83 C4 04 41 83 FD 05 0F 8C", 7, [5], [6]),
        // movups [rcx+0x44], xmm0; movd [rcx+0x54], xmm0; mov [rcx+0x58], rdi
        //   → movq [rcx+0x54], xmm0 (+0x54 and +0x58 −1); mov [rcx+0x5C], edi
        new("the list's reset (0x1EE518)", "0F 11 41 44 66 0F 7E 41 54 48 89 79 58 48 89 79 60", 4,
            [0x66, 0x0F, 0x7E, 0x41, 0x54, 0x48, 0x89, 0x79, 0x58],
            [0x66, 0x48, 0x0F, 0x7E, 0x41, 0x54, 0x89, 0x79, 0x5C]),
        // movups [r14+0x44], xmm0; movd [r14+0x54], xmm0; mov [r14+0x58], r12
        //   → movq [r14+0x54], xmm0; mov [r14+0x5C], r12d
        new("the list's setup (0x1EE9B4)", "41 0F 11 46 44 66 41 0F 7E 46 54 4D 89 66 58", 5,
            [0x66, 0x41, 0x0F, 0x7E, 0x46, 0x54, 0x4D, 0x89, 0x66, 0x58],
            [0x66, 0x49, 0x0F, 0x7E, 0x46, 0x54, 0x45, 0x89, 0x66, 0x5C]),
        // The placing's battle slot past the party's five (0x270FBC): cmp eax, -1; jne found; xor eax, eax (from 0 up)
        //   → test eax, eax; jns found; lea eax, [rax+0x10] (eax −1: from 15 down)
        new("the sixth unit's slot, from 15 (0x270FBC)", SlotSearch, 0, [0x83, 0xF8, 0xFF, 0x75, 0x2F, 0x33, 0xC0],
            [0x85, 0xC0, 0x79, 0x30, 0x8D, 0x40, 0x10]),
        //   inc eax; cmp eax, 0x10; jl → dec eax; cmp eax, -1; jg
        new("the sixth unit's slot, down to 0 (0x270FBC)", SlotSearch, 31, [0xFF, 0xC0, 0x83, 0xF8, 0x10, 0x7C, 0xE1],
            [0xFF, 0xC8, 0x83, 0xF8, 0xFF, 0x7F, 0xE1]),
    ];

    /// <summary>0x271018 in 0x270FBC, where a formation's placing looks in battle slots 0-15 when 0x10-0x14 are taken
    /// (battle state 0x53): cmp eax, -1; jne; xor eax, eax; then the loop (movsxd rbx, eax; mov edi, eax; shl rbx, 9;
    /// add rbx, r12; mov [rbx+0x1BC], al; cmp [rbx+1], r13b; je; inc eax; cmp eax, 0x10; jl).</summary>
    internal const string SlotSearch =
        "83 F8 FF 75 2F 33 C0 48 63 D8 8B F8 48 C1 E3 09 49 03 DC 88 83 BC 01 00 00 44 38 6B 01 74 0C FF C0 83 F8 10 7C E1";

    /// <summary>The places this many groups' units may take in all, and the list's entries.</summary>
    internal const int Places = 6;

    /// <summary>For the offline check: each site's address, once installed.</summary>
    internal static nint[] At { get; } = new nint[Sites.Length];

    /// <summary>The count (0x1EE6AC) and the compaction (0x1EE6DC), for the offline check to run on a made-up list.</summary>
    internal static nint Count { get; private set; }
    internal static nint Compact { get; private set; }

    internal const int State = 0x30, Commenced = 400, List = 0x44, PartyFirst = 0x10;
    internal const int Present = 1, RosterSlot = 2, BattleId = 0x191;
    internal const byte FirstPartyId = 0x78, LastPartyId = 0x7F;

    private static Log? _log;
    private static byte* _units;
    private static bool _numbered;

    /// <summary>After the formation's frame (<see cref="RevealTreasures"/>' hook calls it): once "Commence battle?" is
    /// answered Yes (the formation's state 400), a placed unit below the party's slots gets the battle id after theirs.
    /// The frame numbers slots 0x10–0x14 from 0x78 as it turns 400 (0x1F16F4; Ramza keeps his sprite set), and the
    /// sixth kept the 0x78 its making gave every party unit, the first one's too (seen on the PC, 2026-11-19).</summary>
    internal static void AfterFrame(nint formation)
    {
        if (_units == null)
            return;
        if (*(int*)(formation + State) != Commenced)
        {
            _numbered = false;
            return;
        }
        if (_numbered)
            return;
        _numbered = true;
        foreach (var (slot, id) in Number(_units, (int*)(formation + List)))
            _log?.Info($"six places: the unit placed in battle slot {slot} takes battle id 0x{id:X2}");
    }

    /// <summary>Gives each unit of the list (roster slots, −1 for none) found below the party's slots, other than Ramza
    /// (sprite sets 1–3), the battle id after the party's highest (0x78–0x7F); returns (slot, id) for each.</summary>
    internal static List<(int Slot, byte Id)> Number(byte* units, int* list)
    {
        var numbered = new List<(int, byte)>();
        var next = FirstPartyId;
        for (var slot = PartyFirst; slot < Battle.UnitCount; slot++)
        {
            var unit = units + slot * Battle.UnitSize;
            if (unit[Present] != 0xFF && unit[BattleId] >= next && unit[BattleId] <= LastPartyId)
                next = (byte)(unit[BattleId] + 1);
        }
        for (var k = 0; k < Places; k++)
        {
            if (list[k] < 0)
                continue;
            for (var slot = 0; slot < PartyFirst; slot++)
            {
                var unit = units + slot * Battle.UnitSize;
                if (unit[Present] == 0xFF || unit[RosterSlot] != list[k] || (byte)(unit[0] - 1) < 3 || next > LastPartyId)
                    continue;
                unit[BattleId] = next;
                numbered.Add((slot, next++));
            }
        }
        return numbered;
    }

    public static void Install(Game game)
    {
        _log = game.Log;
        var found = new nint[Sites.Length];
        for (var k = 0; k < Sites.Length; k++)
        {
            var site = Sites[k];
            found[k] = game.Image.FindUnique(site.Pattern) + site.At;
            if (!new ReadOnlySpan<byte>((void*)found[k], site.Was.Length).SequenceEqual(site.Was))
                throw new InvalidDataException($"{site.What} isn't laid out as this DLL expects");
        }
        for (var k = 0; k < Sites.Length; k++)
        {
            GameImage.WriteCode(found[k], Sites[k].Now);
            At[k] = found[k];
        }
        Count = found[0] - Sites[0].At;
        Compact = found[1] - Sites[1].At;
        _units = game.TurnCode.Units;
    }
}
