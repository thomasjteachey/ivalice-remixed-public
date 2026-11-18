namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Move and Jump from movement abilities (docs/MECHANICS.md: "Athlete and Pass"; SCUS 0x8005C904–0x8005C980,
/// rewritten in place): after Move+1–3 and Jump+1–3, Athlete (0x1F7, +0x95 &amp; 0x40) adds 1 to Jump and Pass (0x1FC,
/// +0x95 &amp; 0x02) 1 to Move. The party menu's preview does the same (WORLD 0x80122E88). Installed by
/// <see cref="Rules"/> with "movement-bonus".
/// </summary>
/// <remarks>
/// The remaster's step (0x277C58, from the unit build 0x276112 and the rebuilds 0x2787BA and 0x278A04; the menu's Move and
/// Jump come from the built unit) adds the bonuses from +0x9C into r9w (Move) and cx (Jump), then caps them at 0xFC and
/// 7 from 0x277CCC. There a stub adds ReMixed's two from +0x9E, then redoes what it replaced (eax = Move's low byte, edx
/// = 0xFC, the compare) and goes on to the caps. ReMixed caps Jump at 7 only when Athlete adds to it and drops the Move
/// cap; the remaster's caps stay (a Jump over 7 needs a base Jump over 4).
/// </remarks>
internal static unsafe class MovementBonus
{
    public const int Athlete = 0x40, Pass = 0x02; // the unit's +0x9E (PS1 +0x95)
    public const int StubSize = 64;

    /// <summary>For the offline check: the step, the site and the stub.</summary>
    internal static nint Step { get; private set; }
    internal static nint At { get; private set; }
    internal static nint Stub { get; private set; }

    public static void Install(Game game)
    {
        // The step: r11 = unit; ... the last bonus (Jump+3) into cx; mov edx, 0xFC; cmp r9w, dx; cmova eax, edx.
        var step = game.Image.FindUnique(
            "4C 8B D9 85 D2 75 10 0F B7 41 32 66 89 41 30 ?? ?? ?? ?? ?? ?? ?? ?? 44 8A 91 9C 00 00 00"); // its MP fill is Starting MP's
        var site = game.Image.FindUnique("41 80 E2 04 8D 4A 03 66 0F 44 CA BA FC 00 00 00 66 44 3B CA 0F 47 C2") + 11;
        if ((ulong)(site - step) > 0x100)
            throw new InvalidDataException("the Move and Jump step isn't laid out as this DLL expects");
        var stub = GameImage.AllocateCodeNear(site, StubSize);
        WriteStub(stub, site + 9);
        var jump = new byte[9];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (site + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(site, jump);
        (Step, At, Stub) = (step, site, (nint)stub);
    }

    /// <summary>The stub: Jump (cx) +1 for Athlete, Move (r9w) +1 for Pass, then eax, edx and the compare, and back.</summary>
    internal static void WriteStub(byte* stub, nint back)
    {
        var code = new List<byte>
        {
            0x41, 0xF6, 0x83, 0x9E, 0, 0, 0, Athlete, // test byte [r11+0x9E], 0x40
            0x74, 0x03,                               // jz
            0x66, 0xFF, 0xC1,                         // inc cx
            0x41, 0xF6, 0x83, 0x9E, 0, 0, 0, Pass,    // test byte [r11+0x9E], 0x02
            0x74, 0x04,                               // jz
            0x66, 0x41, 0xFF, 0xC1,                   // inc r9w
            0x41, 0x0F, 0xB6, 0xC1,                   // movzx eax, r9b
            0xBA, 0xFC, 0, 0, 0,                      // mov edx, 0xFC
            0x66, 0x44, 0x3B, 0xCA,                   // cmp r9w, dx
            0xFF, 0x25, 0, 0, 0, 0,                   // jmp [rip+0] → back (the cmova)
        };
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the movement bonus stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
