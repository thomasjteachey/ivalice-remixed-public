namespace IvaliceRemixed.Patches;

/// <summary>
/// The modded game's saves in a folder of their own, so ReMixed's saves and the plain game's don't mix. Installed by
/// <see cref="Rules"/> with "separate-saves" when the setting is on (the default). Not part of ReMixed.
/// </summary>
/// <remarks>
/// The remaster's storage setup (0x6B144) copies the game's title (0x65CDA0, or a demo's) into a buffer (0x2FCF6B0,
/// at 0x6B1A1: lea r8, [the title]) and builds the save folder from it (0x6E5DC): Documents/My Games/&lt;title&gt;/,
/// then Steam/&lt;the Steam id&gt;/ with the saves (ENHANCED.png, autoenhanced.png, system.png). Here that lea points at
/// <see cref="Folder"/> instead, so only the save folder changes; the title stays the game's everywhere else. Steam
/// Cloud syncs the game's own folder, not this one, so the modded saves stay on the machine.
/// </remarks>
internal static unsafe class SaveFolder
{
    public const string Folder = "FINAL FANTASY TACTICS - Ivalice ReMixed";
    internal static nint CopyAt { get; private set; }
    internal static byte* Name { get; private set; }

    public static void Install(Game game)
    {
        var image = game.Image;
        // test cl, cl; lea rax, [the demo's]; mov edx, 0x80; lea r8, [the title]; cmovne r8, rax; lea rcx, [the buffer]
        var at = image.FindUnique("84 C9 48 8D 05 ?? ?? ?? ?? BA 80 00 00 00 4C 8D 05 ?? ?? ?? ?? 4C 0F 45 C0 48 8D 0D");
        var lea = at + 14;
        var title = (byte*)GameImage.Rip(lea, 3, 7);
        if (!new ReadOnlySpan<byte>(title, 22).SequenceEqual("FINAL FANTASY TACTICS "u8))
            throw new InvalidDataException("the storage setup doesn't copy the game's title where this DLL expects");
        var name = (byte*)GameImage.AllocateCodeNear(lea, 64);
        var bytes = System.Text.Encoding.ASCII.GetBytes(Folder + "\0");
        bytes.CopyTo(new Span<byte>(name, bytes.Length));
        var disp = checked((int)((nint)name - (lea + 7)));
        GameImage.WriteCode(lea + 3, BitConverter.GetBytes(disp));
        CopyAt = lea;
        Name = name;
    }
}
