namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's evasion by the attack's angle (docs/MECHANICS.md: "Evasion by angle"; PS1 0x801854FC, rewritten round
/// the angle routine 0x80185588). Installed by <see cref="Rules"/> with "side-evasion".
/// </summary>
/// <remarks>
/// The evade checks work out the angle (front 0, side 1, back 2; the remaster's 0x305788) and drop evasions by it.
/// Vanilla drops the class evasion from the side, and that and both hands' from the back. ReMixed keeps the back
/// rule; from the side it also drops the right hand's evasion (weapon or shield) without Weapon Guard (+0x97 &amp;
/// 0x40), and with Weapon Guard and nothing in either hand drops only the right hand's, so the class evasion stays.
/// The angle itself is vanilla's. The remaster's tail after the angle is stored (0x305825, also entered for a unit
/// on the attacker's own tile) jumps to a stub with ReMixed's rule; the front's early return and the shared ret
/// stay.
/// </remarks>
internal static unsafe class SideEvasion
{
    private const int StubSize = 96;
    private const int WeaponGuardByte = Battle.UnitReactions + 3, WeaponGuardBit = 0x40; // 0x1BF
    private const int RightHand = 0x20, Hands = 4;                                        // u16 slots, 0xFF empty

    // For the offline check.
    internal static nint At { get; private set; }
    internal static nint StubAt { get; private set; }
    internal static nint Angle { get; private set; }

    public static void Install(Game game)
    {
        // mov [angle], cl; test cl, cl; je ret; mov byte [class evade], 0; cmp cl, 2; jne ret;
        // and word [right and left hands' evades], 0; ret
        var tail = game.Image.FindUnique("88 0D ?? ?? ?? ?? 84 C9 74 ?? C6 05 ?? ?? ?? ?? 00 80 F9 02 75 ?? 66 83 25 ?? ?? ?? ?? 00 C3");
        var evades = (byte*)game.RuleCode.Evades; // accessory, right hand, left hand, class, angle
        if ((byte*)GameImage.Rip(tail, 2, 6) != evades + 4 || (byte*)GameImage.Rip(tail + 10, 2, 7) != evades + 3
            || (byte*)GameImage.Rip(tail + 22, 3, 8) != evades + 1)
            throw new InvalidDataException("the angle's evades aren't the ones this DLL expects");
        // The routine: the attacker's and target's x (+0x4F) and y (+0x50), then the target's facing (+0x51).
        var angle = game.Image.FindUnique(
            "48 8B 0D ?? ?? ?? ?? 4C 8B 05 ?? ?? ?? ?? 0F B6 41 4F 45 0F B6 50 4F 45 0F B6 48 50 44 2B D0 0F B6 41 50");
        if ((ulong)(tail - angle) > 0x100)
            throw new InvalidDataException("the angle's evades aren't in the angle routine");
        Angle = angle;
        var at = tail + 10;
        var stub = GameImage.AllocateCodeNear(at, StubSize);
        WriteStub(stub, evades);
        // jmp stub, then nops up to the ret (0x305839), which the front's je still reaches.
        var patch = Enumerable.Repeat((byte)0x90, 30 - 10).ToArray();
        patch[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(patch, 1);
        GameImage.WriteCode(at, patch);
        At = at;
        StubAt = (nint)stub;
    }

    /// <summary>The stub: cl the angle (1 or 2), r8 the target; changes rax and the flags, then returns for the routine.</summary>
    internal static void WriteStub(byte* stub, byte* evades)
    {
        var code = new List<byte> { 0x48, 0xB8 };            // mov rax, the evades
        code.AddRange(BitConverter.GetBytes((ulong)(nint)evades));
        code.AddRange([0x80, 0xF9, 0x02, 0x75, 0x0B]);        // cmp cl, 2; jne side
        code.AddRange([0xC6, 0x40, 0x03, 0x00]);              // back: class evade 0
        code.AddRange([0x66, 0xC7, 0x40, 0x01, 0x00, 0x00]);  // both hands' 0
        code.Add(0xC3);
        // side: test byte [r8 + Weapon Guard], 0x40; jz no guard
        code.AddRange([0x41, 0xF6, 0x80]);
        code.AddRange(BitConverter.GetBytes(WeaponGuardByte));
        code.AddRange([WeaponGuardBit, 0x74, 0x00]);
        var noGuard = code.Count - 1;
        var items = new List<int>();
        for (var k = 0; k < Hands; k++)
        {
            code.AddRange([0x66, 0x41, 0x81, 0x78, (byte)(RightHand + 2 * k), 0xFF, 0x00]); // cmp word [r8+slot], 0xFF
            code.AddRange([0x72, 0x00]);                                                   // jb holding
            items.Add(code.Count - 1);
        }
        code.AddRange([0xC6, 0x40, 0x01, 0x00, 0xC3]);        // empty hands: the right hand's evade 0
        var holding = code.Count;
        code.AddRange([0xC6, 0x40, 0x03, 0x00, 0xC3]);        // holding something: class evade 0 (vanilla's side)
        var unguarded = code.Count;
        code.AddRange([0xC6, 0x40, 0x03, 0x00, 0xC6, 0x40, 0x01, 0x00, 0xC3]); // no Weapon Guard: class and right hand's 0
        code[noGuard] = (byte)(unguarded - (noGuard + 1));
        foreach (var k in items)
            code[k] = (byte)(holding - (k + 1));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the side evasion stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }
}
