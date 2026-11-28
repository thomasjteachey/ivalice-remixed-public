namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's small in-place changes to vanilla's code and data that are a few bytes in the remaster too, each its own
/// row in docs/MECHANICS.md or docs/analysis/scus-in-place-review.md. Installed by <see cref="Rules"/> with "in-place"; each change goes in on its own.
/// </summary>
internal static unsafe class InPlace
{
    /// <summary>A change: where (a pattern and the offset of the bytes in it), what's there, and what goes there.</summary>
    private sealed record Change(string Name, string Pattern, int Offset, byte[] Old, byte[] New, string Section = ".text");

    /// <summary>
    /// The write-back's new copy (r8 the record, rbx the roster unit, si 0): the roster's +4 from the record's +0x70 and
    /// the secondary from +0x60; then, unless the record is a monster's (+0x3E), the reaction, support and movement
    /// (+8, +0xA, +0xC) from +0x62, +0x64 and +0x66.
    /// </summary>
    internal static readonly byte[] MonsterWriteBack =
    [
        0x41, 0x8A, 0x40, 0x70,       // mov al, [r8+0x70]
        0x88, 0x43, 0x04,             // mov [rbx+4], al
        0x41, 0x8A, 0x40, 0x60,       // mov al, [r8+0x60]
        0x88, 0x43, 0x07,             // mov [rbx+7], al
        0x66, 0x41, 0x39, 0x70, 0x3E, // cmp [r8+0x3E], si
        0x75, 0x10,                   // jne past
        0x41, 0x8B, 0x40, 0x62,       // mov eax, [r8+0x62]
        0x89, 0x43, 0x08,             // mov [rbx+8], eax
        0x66, 0x41, 0x8B, 0x40, 0x66, // mov ax, [r8+0x66]
        0x66, 0x89, 0x43, 0x0C,       // mov [rbx+0xC], ax
    ];

    private static readonly Change[] Changes =
    [
        // Weapon evasion without Weapon Guard (PS1 0x80184FAC on): ReMixed drops the Weapon Guard (0x1BF) test and the
        // can-react call from the physical evade setup, so a weapon's evasion counts for everyone. The remaster's setup
        // (0x3055F0) keeps ebx = 1 only for a Weapon Guard unit that can react (0x30DD1C): mov ebx, 1, then a jump
        // past the test and the call. (Its first instructions belong to the unevadable rule's hook.)
        new("weapon evasion without Weapon Guard",
            "33 DB 41 F6 83 97 00 00 00 40 74 10 49 8B CB E8 ?? ?? ?? ?? 85 C0 8D 4B 01 0F 44 D9 66 83 25",
            2, [0x41, 0xF6, 0x83, 0x97, 0x00, 0x00, 0x00], [0xBB, 0x01, 0x00, 0x00, 0x00, 0xEB, 0x13]),
        // Talk Skills on monsters (formula 0x2A, PS1 0x801899E0): ReMixed checks Peacemaker (0x1CD, vanilla's Equip
        // Gun) instead of Monster Talk (0x1D9, its Vehemence). The remaster's 0x3082C3: the attacker's +0x98 & 1.
        new("Talk Skills on monsters with Peacemaker",
            "F6 43 06 20 48 8B 3D ?? ?? ?? ?? 74 ?? F6 87 9A 00 00 00 10 75",
            13, [0xF6, 0x87, 0x9A, 0x00, 0x00, 0x00, 0x10], [0xF6, 0x87, 0x98, 0x00, 0x00, 0x00, 0x01]),
        // Deep water (PS1 0x8018130C, at 0x8018138C): a unit standing in water at depth 2 or more can't react (nor use
        // its weapon's evasion, Abandon or the guard reactions) unless it floats, is a Chicken or a Frog, or has a
        // movement ability for water (+0x9E & 0xC8). ReMixed adds Waterbreathing (0x1F9, vanilla's Move Underwater,
        // +0x9E & 0x10): & 0xD8. The remaster's routine is 0x30DD1C; the pattern starts there.
        new("deep water spares Waterbreathing",
            "F6 41 65 04 4C 8B C1 75 ?? 44 8A 89 B4 01 00 00 41 F6 C1 40 75 ?? 0F B6 51 50 0F B6 05 ?? ?? ?? ?? 0F AF D0 " +
            "0F B6 41 51 C1 E8 07 C1 E0 08 03 D0 0F B6 41 4F 03 D0 48 8D 0D ?? ?? ?? ?? 8A 0C D1 80 E1 E0 80 F9 40 72 ?? " +
            "41 F6 40 63 46 75 ?? 41 F6 80 9E 00 00 00 C8",
            86, [0xC8], [0xD8]),
        // The AI's copy (PS1 0x80194980; the remaster's 0x31F96B): an AI unit with one of those (or Float) doesn't
        // keep out of deep water, and ReMixed adds Waterbreathing there too.
        new("the AI's deep water spares Waterbreathing",
            "48 8B 05 ?? ?? ?? ?? F6 80 9E 00 00 00 C8 75 0D F6 40 63 40 C6 05",
            13, [0xC8], [0xD8]),
        // The AI's "can't be seen" test (PS1 0x80196D58, in 0x80196CE8; the remaster's 0x321740): vanilla hides a
        // Transparent unit from the AI unless it's linked (+0x1B4 bit 7, PS1 +0x182) to another Transparent unit.
        // ReMixed jumps past the test (0x80196D64), so the AI sees Transparent units: the remaster's `je` after the
        // test becomes a `jmp`.
        new("the AI sees Transparent units",
            "42 F6 84 00 ?? ?? ?? ?? 10 74 18 84 C9 79 17 8B C1 83 E0 0F 48 C1 E0 09 42 F6 84 00 ?? ?? ?? ?? 10 74 03 " +
            "33 C0 C3 B8 01 00 00 00 C3",
            9, [0x74], [0xEB]),
        // The movement setup (PS1 0x80174430; the remaster's 0x27ADB8) reads the unit's movement abilities, the
        // remaster's +0x9E (PS1 +0x95: 0x80 is 0x1F6, 0x20 is 0x1F8, 0x08 is 0x1FA) and +0x9D & 1 (0x1F5). ReMixed
        // turned vanilla's Move on Lava (0x1F8) into Momentum and gave lava to 0x1F6 (its Terrain Master), so who may
        // stop on lava (the movement struct's +0x25; PS1 0x80174534) is & 0x88 where vanilla's was & 0x28.
        new("Terrain Master stops on lava, Momentum doesn't",
            "F6 C3 28 88 0D ?? ?? ?? ?? 8A 4F 63 0F 95 05",
            2, [0x28], [0x88]),
        // ... and the lava movement class (5, with lava passable; PS1 0x80174694) goes to & 0x80 where it was & 0x20.
        new("Terrain Master's lava movement class",
            "F6 C3 20 74 10 66 C7 05 ?? ?? ?? ?? 03 05",
            2, [0x20], [0x80]),
        // Vanilla's Any Ground (0x1F5) makes every passable surface cost 1 to cross; ReMixed turned it into Move-Cleanse
        // and drops that (PS1 0x801747C8, a nop for the cost of 1). The remaster's test (0x27B026) is skipped.
        new("Move-Cleanse doesn't cross any ground at a cost of 1",
            "40 84 ED 74 0B 80 F9 FF 8B C1 41 0F 45 C3",
            3, [0x74], [0xEB]),
        // The AI's Attack candidate (PS1 0x8019A3C4, in 0x8019A470; the remaster's 0x316B97 in 0x316B04) checks each
        // hand's weapon (0x316C90), and the hand checked last fills the candidate's weapon, range and elements. Vanilla
        // checks the right hand (+0x20, PS1 +0x1D) and then the left (+0x24, PS1 +0x1F), so a unit with two weapons
        // plans its Attack with the left one's; ReMixed checks them the other way round. The two loads swap.
        new("the AI plans Attack with the right hand's weapon",
            "42 0F B7 94 3F ?? ?? ?? ?? 4C 8D 44 24 40 48 8B CB E8 ?? ?? ?? ?? 42 0F B7 94 3F ?? ?? ?? ?? 4C 8D 44 24 40 " +
            "48 8B CB E8",
            5, [0x00, 0x3D, 0x85, 0x01, 0x4C, 0x8D, 0x44, 0x24, 0x40, 0x48, 0x8B, 0xCB, 0xE8, 0xE3, 0x00, 0x00, 0x00,
                0x42, 0x0F, 0xB7, 0x94, 0x3F, 0x04],
            [0x04, 0x3D, 0x85, 0x01, 0x4C, 0x8D, 0x44, 0x24, 0x40, 0x48, 0x8B, 0xCB, 0xE8, 0xE3, 0x00, 0x00, 0x00,
                0x42, 0x0F, 0xB7, 0x94, 0x3F, 0x00]),
        // The 64 characters' starting ages (SCUS 0x800454D0, copied into the save's ages at a new game by 0x800452EC; the
        // remaster's table 0x7BAC30, copied by 0x33FB84): ReMixed gives entries 52–57 their own ages where vanilla had a
        // placeholder 20. New games only; a save keeps the ages it stored.
        new("six characters' ages",
            "10 10 0F 0F 1C 25 25 38 01 1B 23 4E 10 34 14 38 18 35 37 3A 4C 20 1E 1E 17 11 34 22 0D 10 23 17 " +
            "2F 41 11 2A 37 1A 13 14 2E 23 10 18 26 12 14 0F 40 23 17 1C 14 14 14 14 14 14 14 14 14 34 14 35",
            52, [0x14, 0x14, 0x14, 0x14, 0x14, 0x14], [0x1C, 0x16, 0x10, 0x10, 0x18, 0x17], ".data"),
        // The party menu's write-back (WORLD 0x80121304 in 0x801212B8; the remaster's 0x285744) copies a unit's record
        // into its roster slot. A monster's record holds its actions where a human's has the secondary, reaction,
        // support and movement (+0x60 on), so vanilla wrote the third action into the roster's reaction and zeroed the
        // support and movement: a recruited monster lost the reaction it joined with (its family's). ReMixed keeps the
        // roster's three for a monster (the record's +0x3E). The copy (0x2857DE, 64 bytes, a byte loop and five stores)
        // is rewritten in place: the same stores for everyone else, none of the three for a monster.
        new("a recruited monster keeps its reaction, support and movement",
            "48 8B CE 41 8A 40 70 88 43 04 8A 02 48 8D 52 02 88 44 19 07 48 FF C1 48 83 F9 02 7C ED 41 8A 40 63 88 43 09 " +
            "41 8A 40 64 88 43 0A 41 8A 40 65 88 43 0B 41 8A 40 66 88 43 0C 41 8A 40 67 88 43 0D 66 41 39 70 3E 0F 85",
            0,
            [0x48, 0x8B, 0xCE, 0x41, 0x8A, 0x40, 0x70, 0x88, 0x43, 0x04, 0x8A, 0x02, 0x48, 0x8D, 0x52, 0x02, 0x88, 0x44, 0x19, 0x07,
             0x48, 0xFF, 0xC1, 0x48, 0x83, 0xF9, 0x02, 0x7C, 0xED, 0x41, 0x8A, 0x40, 0x63, 0x88, 0x43, 0x09, 0x41, 0x8A, 0x40, 0x64,
             0x88, 0x43, 0x0A, 0x41, 0x8A, 0x40, 0x65, 0x88, 0x43, 0x0B, 0x41, 0x8A, 0x40, 0x66, 0x88, 0x43, 0x0C, 0x41, 0x8A, 0x40,
             0x67, 0x88, 0x43, 0x0D],
            [.. MonsterWriteBack, 0xEB, 0x19, .. Enumerable.Repeat((byte)0x90, 25)]),
        // The statuses an ability is worth casting for against a target (PS1 0x8019EB94 on; the remaster's 0x38B34C), a
        // status byte at a time (ecx its new statuses, r11b the AI record's copy of battle data byte 38). Vanilla drops
        // the third byte's Float (0x40) unless byte 38 has 0x80, which no unit's battle data has. ReMixed nops that test
        // (0x8019EC8C), so the third byte goes on through the fourth byte's code: its 0x40 dropped unless byte 38 has
        // 0x01 (none has), 0x31 dropped only with 0x80, and 0x0C (Berserk, Chicken) dropped against a Stopped or Sleeping
        // target. The remaster's third-byte case jumps to the fourth's (0x38B7FC) after its Reraise and Frog tests. (This
        // one goes first: its pattern spans the next one's bytes.)
        new("the AI's third status byte goes through the fourth's tests",
            "F6 C1 40 74 09 41 F6 C3 01 75 03 83 F1 40 8B C1 25 CE 00 00 00 45 84 DB 0F 48 C1 8B C8 A8 0C 74 6C F6 43 64 02 " +
            "75 06 44 84 73 65 74 60 8B C8 81 E1 F3 00 00 00 EB 56 40 84 CD 74 08 41 F6 C3 01 75 02 33 CD F6 C1 02 74 0E " +
            "43 80 BC 38 ?? ?? ?? ?? 00 75 03 83 F1 02 8B C1 25 BF 00 00 00 45 84 DB 0F 48 C1 8B C8 EB 25",
            87, [0x8B, 0xC1, 0x25, 0xBF, 0x00, 0x00, 0x00], [0xEB, 0xA7, 0x90, 0x90, 0x90, 0x90, 0x90]),
        // ... and the fourth byte's Protect, Shell and Wall (0x31): vanilla drops them unless byte 38 has 0x80 (so always),
        // ReMixed only with it (0x8019ECD8, its branch the other way round). So ReMixed's AI counts giving Protect, Shell
        // or Wall toward an ability's worth. The remaster's cmovs (0x38B814) becomes cmovns.
        new("the AI counts Protect, Shell and Wall",
            "F6 C1 40 74 09 41 F6 C3 01 75 03 83 F1 40 8B C1 25 CE 00 00 00 45 84 DB 0F 48 C1 8B C8 A8 0C 74 6C F6 43 64 02 " +
            "75 06 44 84 73 65 74 60 8B C8 81 E1 F3 00 00 00 EB 56",
            24, [0x0F, 0x48, 0xC1], [0x0F, 0x49, 0xC1]),
        // Ultima's teleport (PS1 0x8008A700 and 0x8008A780; the remaster's 0x318090): her first form (sprite 0x41) and
        // second (0x49) vanish and reappear with an ability's effect where everyone else's teleport is a sound. Vanilla
        // plays 0xDB and 0xDC as she vanishes, 0xB8 and 0x028 as she reappears; ReMixed made those four slots abilities
        // of its own (Teleport 3, Summon Angel, Summon Demon) and moved the teleport to 0x1A2, 0x1A3, 0x1A1 and 0x1A0,
        // whose effects (AbilityEffectNumberFilterData) it set to vanilla's 231, 232, 192 and 43. The port took the
        // effects without the move, so her first teleport in the final battle played a Dark Knight's ability and the
        // battle waited for it for good (a Nexus user on 0.9.42). The four `mov edx` immediates take ReMixed's numbers.
        new("Ultima reappears from her teleport as 0x1A1 (first form)",
            "3C 41 74 2E 3C 49 74 20 8D 53 12 48 8B CE E8 ?? ?? ?? ?? 8D 4B 69 E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? " +
            "E9 ?? ?? ?? ?? BA ?? ?? 00 00 E9 ?? ?? ?? ?? BA ?? ?? 00 00",
            51, [0xB8, 0x00], [0xA1, 0x01]),
        new("Ultima reappears from her teleport as 0x1A0 (second form)",
            "3C 41 74 2E 3C 49 74 20 8D 53 12 48 8B CE E8 ?? ?? ?? ?? 8D 4B 69 E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? " +
            "E9 ?? ?? ?? ?? BA ?? ?? 00 00 E9 ?? ?? ?? ?? BA ?? ?? 00 00",
            41, [0x28, 0x00], [0xA0, 0x01]),
        new("Ultima vanishes into her teleport as 0x1A2 (first form)",
            "80 7E 0A 49 74 1D 8D 53 12 48 8B CE E8 ?? ?? ?? ?? 8D 4B 68 E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? " +
            "EB 18 BA ?? ?? 00 00 EB 05 BA ?? ?? 00 00",
            43, [0xDB, 0x00], [0xA2, 0x01]),
        new("Ultima vanishes into her teleport as 0x1A3 (second form)",
            "80 7E 0A 49 74 1D 8D 53 12 48 8B CE E8 ?? ?? ?? ?? 8D 4B 68 E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? " +
            "EB 18 BA ?? ?? 00 00 EB 05 BA ?? ?? 00 00",
            36, [0xDC, 0x00], [0xA3, 0x01]),
    ];

    // For the offline check: each change applied, where its pattern starts and the bytes written.
    internal static List<(string Name, nint Start, nint At, byte[] Bytes)> Applied { get; } = [];

    /// <summary>Applies each change it can. Returns the names of those applied.</summary>
    public static List<string> Install(Game game)
    {
        var names = new List<string>();
        foreach (var change in Changes)
        {
            try
            {
                var start = game.Image.FindUnique(change.Pattern, change.Section);
                var at = start + change.Offset;
                if (!new ReadOnlySpan<byte>((void*)at, change.Old.Length).SequenceEqual(change.Old))
                    throw new InvalidDataException("the code isn't what this DLL expects");
                GameImage.WriteCode(at, change.New);
                Applied.Add((change.Name, start, at, change.New));
                names.Add(change.Name);
            }
            catch (Exception e)
            {
                game.Log.Error($"rules: {change.Name} not applied: {e.Message}");
            }
        }
        return names;
    }
}
