namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Starting MP option (docs/MECHANICS.md: "Poison &amp; Regen on MP, Auto Renew, Starting MP";
/// PS1 SCUS 0x8005C8EC → ReMixed's 0x800271F4). Installed by <see cref="Rules"/> with "starting-mp", whatever the
/// option, as a save's own options can change it (<see cref="SaveOptions"/>).
/// </summary>
/// <remarks>
/// A battle unit's setup fills HP and MP from their maxima (the remaster's 0x277C58, rcx the unit, from
/// three unit builders). Its MP half jumps to a stub that gives min(Max MP, 10) with the setting on (its switch,
/// <see cref="Switches"/>), and Max MP, as the game does, with it off.
/// </remarks>
internal static unsafe class StartingMp
{
    private const string Fill =
        "4C 8B D9 85 D2 75 10 0F B7 41 32 66 89 41 30 0F B7 41 36 66 89 41 34 44 8A 91 9C 00 00 00";
    private const int MpAt = 0xF, MpSize = 8;
    internal const int StubSize = 64;
    internal const int Limit = 10;

    // For the offline check.
    internal static nint SiteAt { get; private set; }
    internal static bool Patched { get; private set; }
    internal static byte* Switch { get; private set; }

    public static void Install(Game game)
    {
        SiteAt = game.Image.FindUnique(Fill) + MpAt;
        Switch = Switches.Of(o => o.StartingMp == Options.StartingMpRule.Ten);
        var stub = GameImage.AllocateCodeNear(SiteAt, StubSize);
        WriteStub(stub, SiteAt + MpSize, Switch);
        byte[] jump = [0xE9, 0, 0, 0, 0, 0xCC, 0xCC, 0xCC];
        BitConverter.GetBytes(checked((int)((nint)stub - (SiteAt + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(SiteAt, jump);
        Patched = true;
    }

    /// <summary>The stub, rcx the unit: MP = Max MP, or min(Max MP, 10) with <paramref name="on"/> set, then on to
    /// <paramref name="back"/>.</summary>
    internal static void WriteStub(byte* stub, nint back, byte* on)
    {
        var code = new List<byte>();
        Switches.Test(code, on);                                                 // ZF: off
        code.AddRange([0x0F, 0xB7, 0x41, (byte)Battle.UnitMaxMp]);              // movzx eax, word [rcx + Max MP]
        code.AddRange([0x74, 0x0A]);                                             // jz: the game's own
        code.AddRange([0x83, 0xF8, Limit]);                                      // cmp eax, 10
        code.AddRange([0x76, 0x05]);                                             // jbe +5
        code.AddRange([0xB8, Limit, 0x00, 0x00, 0x00]);                          // mov eax, 10
        code.AddRange([0x66, 0x89, 0x41, (byte)Battle.UnitMp]);                  // mov [rcx + MP], ax
        code.AddRange([0xFF, 0x25, 0x00, 0x00, 0x00, 0x00]);                     // jmp [rip]: back
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the Starting MP stub outgrew its space");
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }
}
