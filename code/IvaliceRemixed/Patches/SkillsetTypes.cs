namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's skillset types (docs/MECHANICS.md: "Skillset types"; SCUS 0x80065CB4, a byte per skillset): Elemental
/// (0x11) is ordinary, its weapon skillsets are type 5, and its monster lists are type 6. Installed by
/// <see cref="Rules"/> with "skillset-types".
/// </summary>
/// <remarks>
/// Type 4 makes the action menus and the action queue swap the ability chosen for the one the terrain gives
/// (the queue's case at 0x281584: the tile's surface through .rdata 0x67F750, vanilla's twelve Geomancy ids
/// 0x7E–0x89). ReMixed's Elemental holds four abilities on the first four ids (Elemental Strike, Lance, Pulse and
/// Wave) and gave 0x82–0x89 to others (Chef's Knife, the Bio spells, First Aid), and its formula 0x24 works out
/// the terrain itself (<see cref="Formulas"/>), so with type 4 a Geomancer would cast whatever the terrain's id
/// now holds. Type 5 is ReMixed's weapon-range family (Magic Blade, Spell Strike and Fellblade); type 6 is the
/// remaster's own monster-list path, including Monster Skill's fourth-slot handling.
/// <para>The remaster's battle menus also know Elemental by its id, 0x11, beside the table, and those places go too
/// (a player's report, 2026-11-19: Elemental Strike was "a melee ability that has a 0% chance of hitting"). The menu's
/// request for the battle (0x237FA4, its globals 0x2FD35A4 on) takes the chosen list item's ability only for skillsets
/// 5–0x10 and from 0x13 (`lea eax, [r9-5]; cmp eax, 0xB; jbe`), and gives 0x11 and 0x12 ability 0, which the PS1's
/// terrain swap and Jump's own range filled in later; so Elemental Strike was aimed with ability 0's range (the panels
/// round the unit) and preview. The test becomes 5–0x11 (Jump keeps its own). The Abilities list's label (0x1560D0)
/// names Elemental "Geomancy: " and the terrain's ability (`cmp edi, 0x11; jne`, then text 0xBCA), and both battle
/// command menus (0x22CBE4 and 0x2E38E4, three places each) show the terrain's ability in the info panel for a job's
/// own 0x11 (`cmp reg, 0x11; jne` to the job command's info); those jumps are always taken now, as for any job's
/// skillset.</para>
/// </remarks>
internal static unsafe class SkillsetTypes
{
    internal const int Elemental = 0x11, Ordinary = 0, ByTerrain = 4, Weapon = 5, Monster = 6;
    internal static ReadOnlySpan<byte> WeaponSkillsets => [0x49, 0x52, 0x55];
    internal static ReadOnlySpan<byte> MonsterSkillsets => [0x51, 0xA8, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF];

    /// <summary>The menu request's test for a skillset whose list item gives the ability: lea eax, [r9-5]; cmp eax, 0xB
    /// (here +6); jbe; cmp r9d, 0x13; jae; then mov r8d, edi; mov [the request's ability], r14w (0).</summary>
    internal const string RequestTest = "41 8D 41 FB 83 F8 0B 76 ?? 41 83 F9 13 73 ?? 44 8B C7 66 44 89 35";
    /// <summary>The Abilities list's label: cmp edi, 0x11; jne (here +3); mov ecx, 0xBCA (the "name: ability" text).</summary>
    internal const string LabelTest = "83 FF 11 0F 85 ?? ?? ?? ?? B9 CA 0B 00 00 E8";
    /// <summary>The command menus' info panel for a job's own 0x11, two of each (one per menu): cmp reg, 0x11; jne (+3);
    /// then the terrain info's clearing (lea [reg+0x2F], 0x40 bytes).</summary>
    internal static readonly string[] InfoTests =
    [
        "83 ?? 11 75 ?? 44 8D ?? 2F 33 D2",
        "83 ?? 11 75 ?? 33 D2 44 8D ?? 2F 48 8D 4D 30",
        "83 ?? 11 0F 85 ?? ?? ?? ?? 33 D2 44 8D ?? 2F 48 8D 8D 70 06 00 00",
    ];

    // For the offline check.
    internal static byte* Table { get; private set; }
    internal static nint RequestAt { get; private set; }
    internal static nint LabelAt { get; private set; }
    internal static IReadOnlyList<nint> InfoAt { get; private set; } = [];

    /// <summary>The remaster's table (.rdata 0x67E010), from the action queue's switch on its skillset's type:
    /// lea r9, [image]; mov ebx, 1; movzx ecx, byte [rax+r9+table]; lea edx, [rbx+6]; cmp ecx, edx; ja.</summary>
    internal static byte* Find(GameImage image)
    {
        var at = image.FindUnique("4C 8D 0D ?? ?? ?? ?? BB 01 00 00 00 42 0F B6 8C 08 ?? ?? ?? ?? 8D 53 06 3B CA 0F 87");
        return (byte*)(image.ImageStart + *(int*)(at + 17));
    }

    public static void Install(Game game)
    {
        var table = Find(game.Image);
        // Attack 8, Item 1, Jump 9, Throw 2, Elemental 4, as in vanilla.
        if (table[1] != 8 || table[6] != 1 || table[0x12] != 9 || table[0x14] != 2 || table[Elemental] != ByTerrain)
            throw new InvalidDataException("the skillset type table isn't the one this DLL expects");
        GameImage.Unprotected((nint)table, 0x100, () =>
        {
            table[Elemental] = Ordinary;
            foreach (var skillset in WeaponSkillsets)
                table[skillset] = Weapon;
            foreach (var skillset in MonsterSkillsets)
                table[skillset] = Monster;
        });
        Table = table;

        // The menus' own tests for Elemental's id: all found before any is written.
        var image = game.Image;
        var request = image.FindUnique(RequestTest);
        var label = image.FindUnique(LabelTest);
        var info = InfoTests.SelectMany(p => image.FindExactly(p, 2)).ToArray();
        GameImage.WriteCode(request + 6, [0x0C]);   // cmp eax, 0xC: skillsets 5–0x11
        GameImage.WriteCode(label + 3, Jump(label + 3, 6));
        foreach (var at in info)
            GameImage.WriteCode(at + 3, Jump(at + 3, *(byte*)(at + 3) == 0x75 ? 2 : 6));
        RequestAt = request;
        LabelAt = label;
        InfoAt = info;
    }

    /// <summary>A jne (short, or near when <paramref name="size"/> is 6) made a jmp to the same place.</summary>
    internal static byte[] Jump(nint at, int size)
    {
        if (size == 2)
            return [0xEB, *(byte*)(at + 1)];
        var target = at + 6 + *(int*)(at + 2);
        var jump = new byte[6];
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)(target - (at + 5)))).CopyTo(jump, 1);
        jump[5] = 0x90;
        return jump;
    }
}
