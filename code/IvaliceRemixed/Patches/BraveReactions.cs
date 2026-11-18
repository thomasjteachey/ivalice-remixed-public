namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Brave chance for reactions (docs/MECHANICS.md: "Guard reactions"; PS1 0x80152800, which
/// ReMixed calls from the reaction roll 0x8018D384 and from three guard reactions): a human target
/// (+0x06 &amp; 0xC0) with Move-Find Item (0x1FD, the ability ReMixed also uses for the learned
/// movement passives) reacts with max(Brave, 100 − Brave)%, so at least half the time. Every reaction
/// that rolls on Brave gets it. Installed by <see cref="Rules"/> with "brave-reactions".
/// </summary>
/// <remarks>
/// The remaster inlines the PS1's roll: each reaction loads its unit's Brave (+0x2B) for
/// 0x278E68(100, Brave), and the four guards (Finger Guard, Catch, Blade Grasp, Arrow Guard) load it
/// once more for the hit chance they show (result +0x2C = 100 − Brave). Each of those loads becomes a
/// call to a stub that loads the chance instead, with the instruction next to it when the load is too
/// short for the call. Finger Guard's roll keeps its load, and its call goes through a stub that
/// reloads it on the way to the roll. MP Rage's roll is C# (<see cref="Reactions"/>) and asks
/// <see cref="ChanceOf"/>.
/// </remarks>
internal static unsafe class BraveReactions
{
    // Registers by their x64 number.
    internal const int Rax = 0, Rcx = 1, Rdx = 2, Rbx = 3, R8 = 8, R10 = 10;

    private const int HumanFlags = 0xC0;
    private const int MoveFindItemByte = Battle.UnitMovement + 2, MoveFindItemBit = 0x01; // 0x1FD
    private const int StubSize = 64;

    private static readonly byte[] MovEcx100 = [0xB9, 0x64, 0x00, 0x00, 0x00];
    private static readonly byte[] LeaEcxRdi100 = [0x8D, 0x4F, 0x64];

    /// <summary>
    /// A load of a unit's Brave to patch: <see cref="Offset"/> from the pattern's start, the unit's and
    /// the loaded register, and the instructions in the replaced span before and after the load, which
    /// the stub runs. <see cref="CallsRoll"/>: the span is the call to the roll instead, and the stub
    /// goes on to the roll.
    /// </summary>
    private sealed record Site(string Name, string Pattern, int Offset, int Unit, int Dest,
                               byte[]? Before = null, byte[]? After = null, bool CallsRoll = false);

    // Finger Guard (0x1C0, ReMixed's Composure; +0x97 & 0x20; 0x308284): the shown chance, then the roll.
    private const string FingerGuard =
        "F6 83 97 00 00 00 20 74 ?? 0F B6 43 2B B9 64 00 00 00 8B D1 66 2B D0 66 41 89 53 2C 74 ?? " +
        "8B 05 ?? ?? ?? ?? 85 C0 75 ?? 0F B6 53 2B E8";
    // Catch (0x1C2, +0x97 & 8; 0x309380).
    private const string Catch =
        "41 F6 80 97 00 00 00 08 74 ?? 41 0F B6 40 2B B9 64 00 00 00 8B D1 66 2B D0 66 41 89 53 2C 74 ?? " +
        "8B 05 ?? ?? ?? ?? 85 C0 75 ?? 41 0F B6 50 2B E8";
    // Blade Grasp (0x1C3, +0x97 & 4) in 0x30AFEC, for an ability with flag 0x7B079A & 0x10.
    private const string BladeGrasp =
        "A8 04 74 ?? F6 05 ?? ?? ?? ?? 10 0F 84 ?? ?? ?? ?? 41 0F B6 52 2B 41 B8 64 00 00 00 41 8B C0 66 2B C2 " +
        "66 41 89 43 2C 8B 05 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? 41 0F B6 52 2B 41 8B C8 E8";
    // Arrow Guard (0x1C4, +0x97 & 2) in 0x30AFEC, after its weapon type test (ArrowGuard.cs).
    private const string ArrowGuardRoll =
        "41 0F B6 4A 2B 41 B8 64 00 00 00 41 8B C0 66 2B C1 66 41 89 43 2C 8B 05 ?? ?? ?? ?? 85 C0 75 ?? " +
        "41 0F B6 52 2B 41 8B C8 E8";

    // PS1 callers in brackets.
    private static readonly Site[] Plan =
    [
        // Hamedo (0x1C5, +0x97 & 1), checking a queued action's targets (0x8017E078; 0x282BDC).
        new("Hamedo", "F6 83 97 00 00 00 01 0F 84 ?? ?? ?? ?? 44 39 25 ?? ?? ?? ?? 75 ?? 0F B6 53 2B B9 64 00 00 00 E8",
            22, Rbx, Rdx, After: MovEcx100),
        new("Finger Guard's shown chance", FingerGuard, 9, Rbx, Rax, After: MovEcx100), // (0x80188338)
        new("Finger Guard", FingerGuard, 44, Rbx, Rdx, CallsRoll: true),                // (0x80188350)
        new("Catch's shown chance", Catch, 10, R8, Rax),                                // (0x80188408)
        new("Catch", Catch, 42, R8, Rdx),                                               // (0x80188420)
        new("Blade Grasp's shown chance", BladeGrasp, 17, R10, Rdx),                    // (0x8018D074)
        new("Blade Grasp", BladeGrasp, 53, R10, Rdx),                                   // (0x8018D084)
        new("Arrow Guard's shown chance", ArrowGuardRoll, 0, R10, Rcx),                 // (0x8018D138)
        new("Arrow Guard", ArrowGuardRoll, 32, R10, Rdx),                               // (0x8018D154)
        // MP Switch (0x1BD), Distribute (0x1BC) and Damage Split (0x1BE) in 0x30BC64 (0x8018D1F4, 0x8018D2E0, 0x8018D344).
        new("MP Switch", "66 39 79 34 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? 0F B6 51 2B 8D 4F 64 E8",
            24, Rcx, Rdx, After: LeaEcxRdi100),
        new("Distribute", "45 85 DB 7E ?? 39 3D ?? ?? ?? ?? 75 ?? 0F B6 51 2B B9 64 00 00 00 E8",
            13, Rcx, Rdx, After: MovEcx100),
        new("Damage Split", "66 45 85 DB 74 ?? 39 3D ?? ?? ?? ?? 75 ?? 0F B6 51 2B B9 64 00 00 00 E8",
            14, Rcx, Rdx, After: MovEcx100),
        // The counters' queue: Counter, Magick Counter, Counter Tackle, Counter Steal (0x8018CE44; 0x30BDC0).
        new("the counters", "84 15 ?? ?? ?? ?? 74 ?? 83 3D ?? ?? ?? ?? 00 75 ?? 48 8B 05 ?? ?? ?? ?? B9 64 00 00 00 0F B6 50 2B E8",
            24, Rax, Rdx, Before: MovEcx100),
        // The after-damage reactions: Auto-Potion (0x8018CEB8; 0x30BE18).
        new("the after-damage reactions",
            "41 80 7B 27 00 7D ?? 83 3D ?? ?? ?? ?? 00 75 ?? 48 8B 05 ?? ?? ?? ?? B9 64 00 00 00 0F B6 50 2B E8",
            23, Rax, Rdx, Before: MovEcx100),
        // The reactions in Critical (+0x63 & 1): Critical Quick, Meatbone Slash (0x8018CF40; 0x30BE68).
        new("the reactions in Critical",
            "F6 40 63 01 74 ?? 4C 8B 1D ?? ?? ?? ?? 41 80 7B 27 00 7D ?? 83 3D ?? ?? ?? ?? 00 75 ?? 0F B6 50 2B B9 64 00 00 00 E8",
            29, Rax, Rdx, After: MovEcx100),
        // The reactions with an amount: Counter Flood, Absorb Used MP and the like (0x8018CFA4; 0x30BEB4).
        new("the reactions with an amount",
            "80 3D ?? ?? ?? ?? 00 44 0F B7 D9 74 ?? 83 3D ?? ?? ?? ?? 00 75 ?? 48 8B 05 ?? ?? ?? ?? B9 64 00 00 00 0F B6 50 2B E8",
            29, Rax, Rdx, Before: MovEcx100),
    ];

    /// <summary>Whether the stubs are in, for <see cref="ChanceOf"/>.</summary>
    internal static bool Active { get; private set; }

    // For the offline check: each patched span and the stub it calls.
    internal static (string Name, nint At, int Length, nint Stub, bool CallsRoll)[] Sites { get; private set; } = [];
    internal static (int Unit, int Dest)[] RegisterPairs => Plan.Select(s => (s.Unit, s.Dest)).Distinct().ToArray();
    internal static nint RollAt { get; private set; }

    /// <summary>Patches every load, or none if any isn't where this DLL expects. Returns how many.</summary>
    public static int Install(Game game)
    {
        var image = game.Image;
        var roll = (nint)game.TurnCode.Roll;
        var found = new List<(Site Site, nint At, byte[] Span)>();
        foreach (var site in Plan)
        {
            var at = image.FindUnique(site.Pattern) + site.Offset;
            byte[] span = site.CallsRoll
                ? new ReadOnlySpan<byte>((void*)at, 5).ToArray()
                : [.. site.Before ?? [], .. OriginalLoad(site.Unit, site.Dest), .. site.After ?? []];
            if (!new ReadOnlySpan<byte>((void*)at, span.Length).SequenceEqual(span)
                || (site.CallsRoll && (span[0] != 0xE8 || GameImage.Rip(at, 1, 5) != roll)))
                throw new InvalidDataException($"{site.Name}'s Brave load isn't the code this DLL expects");
            found.Add((site, at, span));
        }
        var stubs = GameImage.AllocateCodeNear(found[0].At, found.Count * StubSize);
        var sites = new List<(string, nint, int, nint, bool)>();
        foreach (var ((site, at, span), k) in found.Select((f, k) => (f, k)))
        {
            var stub = stubs + k * StubSize;
            var code = new List<byte>(site.Before ?? []);
            WriteLoad(code, site.Unit, site.Dest);
            code.AddRange(site.After ?? []);
            if (site.CallsRoll)
            {
                code.AddRange([0xFF, 0x25, 0, 0, 0, 0]); // jmp [rip+0]: on to the roll, which returns to the call's caller
                code.AddRange(BitConverter.GetBytes((ulong)roll));
            }
            else
                code.Add(0xC3);
            if (code.Count > StubSize)
                throw new InvalidOperationException($"{site.Name}'s stub is too big");
            code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
            sites.Add((site.Name, at, span.Length, (nint)stub, site.CallsRoll));
        }
        // Written last, all together, so a failure above leaves the game's code as it was.
        foreach (var (_, at, length, stub, _) in sites)
        {
            var patch = Enumerable.Repeat((byte)0x90, length).ToArray();
            patch[0] = 0xE8;
            BitConverter.GetBytes(checked((int)(stub - (at + 5)))).CopyTo(patch, 1);
            GameImage.WriteCode(at, patch);
        }
        Sites = [.. sites];
        RollAt = roll;
        Active = true;
        return sites.Count;
    }

    /// <summary>The chance a unit reacts with: its Brave, or 100 − Brave if that's more for a human with Move-Find Item.</summary>
    internal static int Chance(byte* unit)
    {
        int brave = unit[Battle.UnitBrave];
        return (unit[6] & HumanFlags) != 0 && (unit[MoveFindItemByte] & MoveFindItemBit) != 0 && brave < 50
            ? 100 - brave
            : brave;
    }

    /// <summary><see cref="Chance"/> once the stubs are in, else the unit's Brave as in vanilla.</summary>
    internal static int ChanceOf(byte* unit) => Active ? Chance(unit) : unit[Battle.UnitBrave];

    /// <summary>The game's movzx dest32, byte [unit + Brave].</summary>
    internal static byte[] OriginalLoad(int unit, int dest)
    {
        var modrm = (byte)(0x40 | (dest << 3) | (unit & 7));
        return unit >= 8 ? [0x41, 0x0F, 0xB6, modrm, Battle.UnitBrave] : [0x0F, 0xB6, modrm, Battle.UnitBrave];
    }

    /// <summary>
    /// <see cref="Chance"/> in asm, into <paramref name="dest"/> (eax, ecx or edx) from the unit in
    /// <paramref name="unit"/>. Changes only that register (all 64 bits, as the game's movzx does) and
    /// the flags; uses no stack.
    /// </summary>
    internal static void WriteLoad(List<byte> code, int unit, int dest)
    {
        if (dest > Rdx || unit == dest)
            throw new ArgumentException("the load goes into eax, ecx or edx, not the unit's own register");
        byte d = (byte)dest;
        code.AddRange([(byte)(unit >= 8 ? 0x49 : 0x48), 0x8B, (byte)(0xC0 | (d << 3) | (unit & 7))]); // mov rD, rU
        code.AddRange([0xF6, (byte)(0x40 | d), 6, HumanFlags]);                                         // test byte [rD+6], 0xC0
        code.AddRange([0x74, 0x00]);                                                                    // jz plain
        var noHuman = code.Count - 1;
        code.AddRange([0xF6, (byte)(0x80 | d)]);                                                        // test byte [rD+0x9E], 1
        code.AddRange(BitConverter.GetBytes(MoveFindItemByte));
        code.Add(MoveFindItemBit);
        code.AddRange([0x74, 0x00]);                                                                    // jz plain
        var noItem = code.Count - 1;
        var load = new byte[] { 0x0F, 0xB6, (byte)(0x40 | (d << 3) | d), Battle.UnitBrave };            // movzx eD, byte [rD+0x2B]
        code.AddRange(load);
        code.AddRange([0x83, (byte)(0xF8 | d), 50]);                                                    // cmp eD, 50
        code.AddRange([0x73, 0x00]);                                                                    // jae done
        var high = code.Count - 1;
        code.AddRange([0xF7, (byte)(0xD8 | d), 0x83, (byte)(0xC0 | d), 100]);                           // neg eD; add eD, 100
        code.AddRange([0xEB, (byte)load.Length]);                                                       // jmp done
        var plain = code.Count;
        code.AddRange(load);
        var done = code.Count;
        code[noHuman] = (byte)(plain - (noHuman + 1));
        code[noItem] = (byte)(plain - (noItem + 1));
        code[high] = (byte)(done - (high + 1));
    }
}
