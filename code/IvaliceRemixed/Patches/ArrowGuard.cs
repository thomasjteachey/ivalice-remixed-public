namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Arrow Guard against guns too (docs/MECHANICS.md: "Guard reactions"; PS1 0x8018D128, where
/// ReMixed widened the weapon type test from 11–12 to 10–12). Installed by <see cref="Rules"/> with
/// "arrow-guard".
/// </summary>
/// <remarks>
/// The remaster tests the weapon's item type (+5) for Arrow Guard (0x1C4) in two places, the reaction
/// with its Brave roll (0x30B143) and a path without one (0x30B733): sub ecx, 0xB; je; cmp ecx, 1; jne.
/// Both become sub ecx, 0xA; nop; nop; cmp ecx, 2; ja, so guns (10), crossbows (11) and bows (12) pass.
/// </remarks>
internal static unsafe class ArrowGuard
{
    private const string Test = "0F B6 48 05 83 E9 0B 74 05 83 F9 01 75";

    // For the offline check.
    internal static nint[] Sites { get; private set; } = [];

    public static void Install(Game game)
    {
        var image = game.Image;
        // The reaction: the ability's Arrow Guard flag (+3 & 0x20), the weapon's item data, its type.
        var rolled = image.FindUnique("F6 05 ?? ?? ?? ?? 20 74 67 0F B7 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? " + Test);
        var other = image.FindUnique("F6 05 ?? ?? ?? ?? 20 74 33 0F B7 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? " + Test);
        Sites = [rolled + 21 + 4, other + 21 + 4];
        foreach (var at in Sites)
            GameImage.WriteCode(at, [0x83, 0xE9, 0x0A, 0x90, 0x90, 0x83, 0xF9, 0x02, 0x77]);
    }
}
