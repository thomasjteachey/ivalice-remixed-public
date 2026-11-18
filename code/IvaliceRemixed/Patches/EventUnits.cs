namespace IvaliceRemixed.Patches;

/// <summary>
/// An event's unit number past the groups names every unit present, as on the PS1 (docs/REMASTER.md, "Event units by
/// number"). The instructions that name units (0x11, 0x2C, 0x2D, 0x32, 0x53, 0x69, 0x6C, 0x6D, 0x80, 0x81, 0x83) take a
/// halfword: 0 every unit, under 0x100 a unit's id, 0x100-0x103 groups 2-5. Both games make it a mode (the PS1's
/// 0x80147928, the remaster's 0x240888: 0 one unit, 1 every unit, else the number less 0xFE) and test each slot by it
/// (0x801479AC, 0x2408F0). For a mode past 5 the PS1's test returns the 5 it compared last (set in a branch's delay
/// slot), so every unit present matches; the remaster's returned 0, so none did. ReMixed's events have 112 such
/// instructions (the Treasure Wheel's chests, 1-A's end, ...); vanilla's and the remaster's own 1,061 scripts none.
/// Installed with ReMixed's events (<see cref="RemixedEvents"/>).
/// </summary>
/// <remarks>
/// The test's last compare, cmp dword [r8], 5; jne to its return 0 (0x240A11), becomes ja to its return 1 (0x2409E8): a
/// mode past 5 matches a unit present (one that isn't has returned 0 before the modes are tested), and mode 5 goes on
/// to its own test as before.
/// </remarks>
internal static unsafe class EventUnits
{
    /// <summary>The slot test (0x2408F0: the unit's id out, the slot's id, the mode) from its entry to its test of mode
    /// 1: the unit present (0x260A98), its battle unit (0x273064), present again.</summary>
    internal const string MatcherPattern =
        "40 53 48 83 EC 20 45 33 C9 0F B7 DA 45 8B D1 45 8B D9 45 39 08 0F 84 DD 00 00 00 66 89 19 0F B7 CB " +
        "E8 ?? ?? ?? ?? 48 85 C0 74 27 8B CB E8 ?? ?? ?? ?? 4C 63 D8 41 83 FB FF 74 17 41 83 FB 14 77 11 4D 8B D3 " +
        "48 8D 05 ?? ?? ?? ?? 49 C1 E2 09 4C 03 D0 0F B7 CB E8 ?? ?? ?? ?? 41 83 38 01 75 0F";
    /// <summary>Its tests of modes 4 and 5, at <see cref="ModesAt"/>; the last jne (<see cref="JumpAt"/>) to return 0.</summary>
    internal const string ModesPattern =
        "41 83 38 04 75 14 41 83 FB FF 74 6F 4D 85 D2 74 6A 41 F6 42 05 30 74 63 EB DB 41 83 38 05 75 5B";
    internal const int ModesAt = 0x103, JumpAt = ModesAt + 30;
    /// <summary>ja to the return 1 (mov eax, 1, 0x2B bytes back from the jump's end).</summary>
    internal static readonly byte[] PastGroups = [0x77, 0xD5];

    /// <summary>The slot test, for the offline check.</summary>
    internal static nint MatcherAt { get; private set; }

    public static void Install(GameImage image)
    {
        var at = image.FindUnique(MatcherPattern);
        GameImage.Calls(at + ModesAt, ModesPattern, "the event units' test of modes 4 and 5");
        GameImage.WriteCode(at + JumpAt, PastGroups);
        MatcherAt = at;
    }
}
