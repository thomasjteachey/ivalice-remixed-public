namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Minimum Weapon Range (docs/MECHANICS.md: "No minimum weapon range"; PS1 0x801605B0, from
/// the weapon range builders). Installed by <see cref="Rules"/> with "weapon-range".
/// </summary>
/// <remarks>
/// A weapon's range comes from a dispatcher on its attack flags (the remaster's 0x27EFC0; PS1
/// 0x80179A20): melee, Direct (the range, then 0x27EB40 clears every tile within 2), or Arc (0x27EE40,
/// which skips tiles closer than 3). By default ReMixed does neither, so the attacker's own tile and
/// its neighbours are in range too; the vanilla setting leaves the code alone.
/// </remarks>
internal static unsafe class WeaponRange
{
    private const string Dispatch =
        "41 F6 C0 C0 74 0D 41 8A D0 49 8B CB E8 ?? ?? ?? ?? EB 24 41 F6 C0 20 74 13 41 8A C9 E8 ?? ?? ?? ?? " +
        "8B D7 8B CE E8 ?? ?? ?? ?? EB 0B 41 8A D1 49 8B CB E8 ?? ?? ?? ??";
    private const int HoleCall = 0x25;   // the Direct branch's call of the hole within 2
    private const int ArcSite = 0x99;    // in the Arc builder: distance < 3 skips the tile
    private const int ArcSkip = 15;      // its jl

    // For the offline check.
    internal static nint ArcBuilderAt { get; private set; }
    internal static nint DirectAt { get; private set; }
    internal static byte* RangeMap { get; private set; } // 5 bytes a tile, level 1 at +0x500 (PS1 0x80192DD8)

    public static void Install(Game game)
    {
        var image = game.Image;
        var dispatch = image.FindUnique(Dispatch);
        var calls = GameImage.Calls(dispatch, Dispatch, "the weapon range dispatcher");
        var hole = calls[2];
        var arc = calls[3];
        // The hole: tiles within 2 of (x, y) (cmp eax, 2; jg) are cleared.
        if (*(uint*)(hole + 0x63) != 0x7F02F883)
            throw new InvalidDataException("the Direct weapons' hole isn't the code this DLL expects");
        // The Arc builder: the distance, then < 3 skips the tile; then the range map.
        GameImage.Calls(arc + ArcSite,
            "45 3B C4 43 8D 04 01 8B DF 0F 47 D8 83 FB 03 7C ?? 43 8D 04 06 BD 02 00 00 00 48 63 C8 44 8B E6 " +
            "48 8D 05 ?? ?? ?? ??", "the Arc range builder");
        RangeMap = (byte*)GameImage.Rip(arc + ArcSite + 0x20, 3, 7);
        if (RangeMap != (byte*)GameImage.Rip(hole + 0x32, 3, 7))
            throw new InvalidDataException("the range builders don't share the range map this DLL expects");
        ArcBuilderAt = arc;
        DirectAt = dispatch;
        // Whatever the option, as a save's own can change it: the game's bytes kept, and the nops or they written in
        // (on the game's thread, between battles' range builds).
        var skip = (byte*)(arc + ArcSite + ArcSkip);
        var holeCall = (byte*)(dispatch + HoleCall);
        byte[] skipBytes = new ReadOnlySpan<byte>(skip, 2).ToArray(), holeBytes = new ReadOnlySpan<byte>(holeCall, 5).ToArray();
        void Apply()
        {
            var none = Options.Current.WeaponRange == Options.WeaponRangeRule.NoMinimum;
            GameImage.WriteCode((nint)skip, none ? new byte[] { 0x66, 0x90 } : skipBytes);                        // two-byte nop
            GameImage.WriteCode((nint)holeCall, none ? new byte[] { 0x0F, 0x1F, 0x44, 0x00, 0x00 } : holeBytes);  // five-byte nop
        }
        Apply();
        Options.Changed += Apply;
    }

    /// <summary>Whether the patches are in (the setting is ReMixed's default).</summary>
    internal static bool Patched =>
        ArcBuilderAt != 0 && *(ushort*)(ArcBuilderAt + ArcSite + ArcSkip) == 0x9066 && *(byte*)(DirectAt + HoleCall) == 0x0F;
}
