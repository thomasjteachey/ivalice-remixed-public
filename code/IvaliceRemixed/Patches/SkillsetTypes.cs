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
/// </remarks>
internal static unsafe class SkillsetTypes
{
    internal const int Elemental = 0x11, Ordinary = 0, ByTerrain = 4, Weapon = 5, Monster = 6;
    internal static ReadOnlySpan<byte> WeaponSkillsets => [0x49, 0x52, 0x55];
    internal static ReadOnlySpan<byte> MonsterSkillsets => [0x51, 0xA8, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF];

    // For the offline check.
    internal static byte* Table { get; private set; }

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
    }
}
