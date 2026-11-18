namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Poaching Odds (docs/MECHANICS.md: "Poach odds by Brave"; PS1 0x8018C6DC). Installed by
/// <see cref="Rules"/> with "poach-odds".
/// </summary>
/// <remarks>
/// A poached monster gives one of its two items on a roll (PS1 0x8005E0CC(256, 31); the remaster's
/// 0x278E68, called inline in the apply step at 0x30ABBA): 31/256 for the rare one. ReMixed passes
/// 231 − 2 × the poacher's Brave instead, so 37/256 at Brave 97 and 131/256 at Brave 50. The patched
/// call goes to a stub that sets that chance and jumps on to the roll.
/// </remarks>
internal static unsafe class PoachOdds
{
    internal const int StubSize = 96;

    // For the offline check.
    internal static nint CallAt { get; private set; }
    internal static nint Roll { get; private set; }
    internal static byte* Switch { get; private set; }

    public static void Install(Game game)
    {
        var image = game.Image;
        // Not while the AI simulates, only for a poached result (+0x12 & 0x20), then the roll (256, 31).
        var at = image.FindUnique("39 1D ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? F6 40 12 20 0F 84 ?? ?? ?? ?? " +
                                  "8D 53 1F B9 00 01 00 00 E8 ?? ?? ?? ??");
        if ((int*)GameImage.Rip(at, 2, 6) != game.Routines.Simulating
            || (byte**)GameImage.Rip(at + 0xC, 3, 7) != game.Battle.TargetResult)
            throw new InvalidDataException("the apply step's poach isn't the code this DLL expects");
        var call = at + 0x25;
        CallAt = call;
        Roll = GameImage.Rip(call, 1, 5);
        // Patched whatever the option (a save's own can change it): with its switch off the roll gets the game's 31.
        Switch = Switches.Of(o => o.PoachingOdds == Options.PoachRule.ByBrave);
        var stub = GameImage.AllocateCodeNear(call, StubSize);
        WriteStub(stub, (nint)game.Battle.Attacker, Roll, Switch);
        GameImage.WriteCode(call + 1, BitConverter.GetBytes(checked((int)((nint)stub - (call + 5)))));
    }

    /// <summary>
    /// The stub: with <paramref name="on"/> set, edx = 231 − 2 × the attacker's Brave (31 without an attacker); then on
    /// to the roll, with ecx (256), and edx with the switch off, as the game set them.
    /// </summary>
    internal static void WriteStub(byte* stub, nint attacker, nint roll, byte* on)
    {
        var code = new List<byte>();
        Switches.Test(code, on);                                      // ZF: off
        code.AddRange([0x74, 0x00]);                                  // jz the roll (set below)
        var off = code.Count;
        code.AddRange([0x48, 0xB8]);                                  // mov rax, the attacker pointer's address
        code.AddRange(BitConverter.GetBytes((long)attacker));
        code.AddRange([0x48, 0x8B, 0x00]);                            // mov rax, [rax]
        code.AddRange([0xBA, 0x1F, 0x00, 0x00, 0x00]);                // mov edx, 31
        code.AddRange([0x48, 0x85, 0xC0]);                            // test rax, rax
        code.AddRange([0x74, 0x0D]);                                  // jz the roll
        code.AddRange([0x0F, 0xB6, 0x40, (byte)Battle.UnitBrave]);    // movzx eax, byte [rax + Brave]
        code.AddRange([0x01, 0xC0]);                                  // add eax, eax
        code.AddRange([0xBA, 0xE7, 0x00, 0x00, 0x00]);                // mov edx, 231
        code.AddRange([0x29, 0xC2]);                                  // sub edx, eax
        code[off - 1] = checked((byte)(code.Count - off));
        code.AddRange([0x48, 0xB8]);                                  // the roll: mov rax, the roll
        code.AddRange(BitConverter.GetBytes((long)roll));
        code.AddRange([0xFF, 0xE0]);                                  // jmp rax
        if (code.Count > StubSize)
            throw new InvalidOperationException("the poach stub outgrew its space");
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }

    /// <summary>The rare item's chance in 256 (ReMixed's: 231 − 2 × Brave).</summary>
    internal static int Chance(int brave) => 231 - 2 * brave;
}
