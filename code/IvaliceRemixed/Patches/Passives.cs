using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's learned passives and reaction priority (docs/MECHANICS.md: "Learned passives"; PS1
/// SCUS 0x8002D000, from the end of the ability-bit builder 0x8005B7A0, and 0x8005B82C).
/// Installed by <see cref="Supports"/> for the ids, and the reaction rule by <see cref="Rules"/>.
/// </summary>
/// <remarks>
/// After a unit's reaction, support and movement bits are built (+0x94; 0x276DF4, from five
/// places), movement 0x1FD (Move-Find Item) turns on Move in Water, Fly and Float and the best Jump+N the unit has
/// learned (in the Geomancer, Dancer, Time Mage, Bard or Dancer, Thief and Archer skillsets), and
/// support 0x1D0 (with Two Spells, not ported) Equip Shield, Equip Sword, Half of MP and Short
/// Charge (Knight, Summoner, Time Mage). And an innate reaction no longer replaces the equipped
/// one: it's only recorded and set when the reaction slot (+0x14) is empty or holds it.
/// </remarks>
/// <remarks>
/// Both hooks are stubs, not C# detours: the builder keeps r10 and r11 across its calls to the
/// setter (the setter never touches them), so the setter's rule is a few instructions that only
/// use rax; and the builder's own callers may keep xmm0–5 across it, so its stub saves them
/// around the C# part.
/// </remarks>
internal static unsafe class Passives
{
    private static Game _game = null!;
    private static delegate* unmanaged<int, int> _jobIndex;     // job -> index of its learned bits (0x2B8F18)
    private static bool _movement, _support, _built;
    private static int _failed;

    private const int LearnedMovement = 0x1FD, LearnedSupports = 0x1D0;
    private const int LearnedBits = 0xA2; // 3 bytes per job (PS1 +0x99); the third holds the RSM slots, MSB first

    // For the offline check.
    internal static nint BuildAt { get; private set; }
    internal static nint SetBitAt { get; private set; }
    internal static byte* RsmLearned(byte* unit, int job) => unit + LearnedBits + 3 * _jobIndex(job) + 2;

    /// <summary>The ids' parts (0x1FD now; 0x1D0 comes with Two Spells). Returns what it enabled.</summary>
    public static List<string> Install(Game game, HashSet<int> ids)
    {
        _movement = ids.Remove(LearnedMovement);
        _support = ids.Remove(LearnedSupports);
        // Hooked whatever the options: Cast instantly rides on the same hook, and a save's options can turn it on; with
        // nothing to grant the hook leaves the bits as the builder made them.
        Find(game);
        if (!_built)
        {
            // push rbx; mov rbx, rcx; sub rsp, 0x80; call the original; save xmm0–5; Grant(rbx);
            // restore xmm0–5; add rsp, 0x80; pop rbx; ret. 16-aligned for both calls. Near the builder, so the
            // hook library's own jump buffer, which has to reach both, has room.
            var stub = GameImage.AllocateCodeNear(BuildAt, 160);
            var code = new List<byte> { 0x53, 0x48, 0x8B, 0xD9, 0x48, 0x81, 0xEC, 0x80, 0x00, 0x00, 0x00, 0x48, 0xB8 };
            var originalAt = code.Count;
            code.AddRange(new byte[8]);
            code.AddRange([0xFF, 0xD0]);
            for (var x = 0; x < 6; x++)
                code.AddRange([0xF3, 0x0F, 0x7F, (byte)(0x44 + 8 * x), 0x24, (byte)(0x20 + 0x10 * x)]); // movdqu [rsp+…], xmmX
            code.AddRange([0x48, 0x8B, 0xCB, 0x48, 0xB8]);                                              // mov rcx, rbx; mov rax, Grant
            code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<byte*, void>)&GrantEntry));
            code.AddRange([0xFF, 0xD0]);
            for (var x = 0; x < 6; x++)
                code.AddRange([0xF3, 0x0F, 0x6F, (byte)(0x44 + 8 * x), 0x24, (byte)(0x20 + 0x10 * x)]); // movdqu xmmX, [rsp+…]
            code.AddRange([0x48, 0x81, 0xC4, 0x80, 0x00, 0x00, 0x00, 0x5B, 0xC3]);
            code.ToArray().CopyTo(new Span<byte>(stub, 160));
            var original = game.Hooks.Hook(BuildAt, (nint)stub, "the ability-bit builder");
            *(ulong*)(stub + originalAt) = (ulong)original;
            _built = true;
        }
        var enabled = new List<string>();
        if (_movement)
            enabled.Add("0x1FD learned movement");
        if (_support)
            enabled.Add("0x1D0 learned supports");
        if (Options.Current.EveryoneNonCharge)
            enabled.Add("everyone has Non-charge");
        return enabled;
    }

    /// <summary>The equipped reaction wins over an innate one (rule "equipped-reaction").</summary>
    public static void InstallReactionRule(Game game)
    {
        Find(game);
        // movzx eax, dx; sub ax, 0x1A6; cmp ax, 0x20; jae original (not a reaction);
        // movzx eax, word [rcx+0x14]; test ax, ax; je original (none equipped); cmp ax, dx;
        // je original (the same one); ret (another one is equipped); original: jmp [rip+0]. Near the setter, as
        // the builder's stub.
        var stub = GameImage.AllocateCodeNear(SetBitAt, 48);
        byte[] code =
        [
            0x0F, 0xB7, 0xC2, 0x66, 0x2D, 0xA6, 0x01, 0x66, 0x83, 0xF8, 0x20, 0x73, 0x0F,
            0x0F, 0xB7, 0x41, 0x14, 0x66, 0x85, 0xC0, 0x74, 0x06, 0x66, 0x3B, 0xC2, 0x74, 0x01, 0xC3,
            0xFF, 0x25, 0x00, 0x00, 0x00, 0x00, 0, 0, 0, 0, 0, 0, 0, 0,
        ];
        code.CopyTo(new Span<byte>(stub, code.Length));
        var original = game.Hooks.Hook(SetBitAt, (nint)stub, "the ability-bit setter");
        *(ulong*)(stub + 34) = (ulong)original;
    }

    private static void Find(Game game)
    {
        if (BuildAt != 0)
            return;
        _game = game;
        var image = game.Image;
        // The builder: clears the 11 bytes from +0x94, then sets the bits of the four innates (+0x0A),
        // the reaction (+0x14), the support (+0x16) and the movement (+0x18).
        var build = image.FindUnique(
            "40 53 48 83 EC 20 33 C0 4C 8D 59 0A 48 89 81 94 00 00 00 4C 8B D1 66 89 81 9C 00 00 00 88 81 9E 00 00 00 " +
            "8D 58 04 41 0F B7 13 49 8B CA E8 ?? ?? ?? ??");
        var setBit = GameImage.Rip(build + 45, 1, 5);
        // The setter: ids 0x1A6 to 0x1FD; a reaction (below 0x1C6) is written into the slot first.
        GameImage.Calls(setBit, "4C 8B C1 0F B7 C2 B9 A6 01 00 00 66 2B C1 66 83 F8 57 77 ?? B9 C6 01 00 00 66 3B D1 73 ?? 66 41 89 50 14",
                        "the ability-bit setter");
        // The job index of a unit's learned bits, as the learn step uses it.
        var learn = image.FindUnique(
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 40 48 8B 05 ?? ?? ?? ?? 48 33 C4 " +
            "48 89 44 24 38 0F B7 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 98 48 01 00 00 0F B6 4B 03 44 0F B6 7B 12 E8 ?? ?? ?? ??");
        _jobIndex = (delegate* unmanaged<int, int>)GameImage.Rip(learn + 67, 1, 5);
        BuildAt = build;
        SetBitAt = setBit;
    }

    /// <summary>After the builder (from its stub): ReMixed's learned passives (0x8002D000).</summary>
    [UnmanagedCallersOnly]
    private static void GrantEntry(byte* unit)
    {
        try
        {
            if (unit != null)
                Grant(unit);
        }
        catch (Exception e)
        {
            Failed("the learned passives", e);
        }
    }

    /// <summary>ReMixed's grants (0x8002D000), on the bits the builder just made.</summary>
    internal static void Grant(byte* unit)
    {
        byte Rsm(int job) => unit[LearnedBits + 3 * _jobIndex(job) + 2];
        var granted = "";
        if (_support && (unit[Battle.UnitSupports + 1] & 0x20) != 0) // 0x1D0
        {
            var knight = Rsm(0x4C);
            if ((knight & 0x10) != 0) { unit[Battle.UnitSupports] |= 0x20; granted += " Equip Sword"; }     // 0x1C8, Knight's 4th
            if ((knight & 0x20) != 0) { unit[Battle.UnitSupports] |= 0x40; granted += " Equip Shield"; }    // 0x1C7, its 3rd
            if ((Rsm(0x52) & 0x40) != 0) { unit[Battle.UnitSupports + 1] |= 0x80; granted += " Half of MP"; } // 0x1CE, Summoner's 2nd
            if ((Rsm(0x51) & 0x20) != 0) { unit[Battle.UnitSupports + 3] |= 0x08; granted += " Short Charge"; } // 0x1E2, Time Mage's 3rd
        }
        if (_movement && (unit[Battle.UnitMovement + 2] & 0x01) != 0) // 0x1FD
        {
            if ((Rsm(0x56) & 0x20) != 0) { unit[Battle.UnitMovement + 2] |= 0x80; granted += " Move in Water"; } // 0x1F6, Geomancer's 3rd
            var man = (unit[6] & 0x80) != 0;
            // Fly is the music job's 4th: a man's Bard (0x5B), a woman's Dancer (0x5C). ReMixed's man's Bard sat in the
            // Dancer slot; the port gives him the Bard's own (tools/build_mod.py's DARK_KNIGHT).
            if ((Rsm(man ? 0x5B : 0x5C) & 0x10) != 0) { unit[Battle.UnitMovement + 2] |= 0x04; granted += " Fly"; } // 0x1FB
            if ((Rsm(0x51) & 0x08) != 0) { unit[Battle.UnitMovement + 2] |= 0x08; granted += " Float"; }        // 0x1FA, Time Mage's 5th
            // The best Jump: +3 from the Dark Knight's 3rd (ReMixed's man's Dark Knight sat in the Bard slot; the port
            // puts it on the remaster's Dark Knight, 0xA0, for both) or a woman's Dancer's; +2 from Thief's 6th; +1 from
            // Archer's 5th.
            if ((Rsm(Growth.DarkKnight) & 0x20) != 0 || (!man && (Rsm(0x5C) & 0x20) != 0)) { unit[Battle.UnitMovement] |= 0x04; granted += " Jump+3"; }
            else if ((Rsm(0x53) & 0x04) != 0) { unit[Battle.UnitMovement] |= 0x08; granted += " Jump+2"; }
            else if ((Rsm(0x4D) & 0x08) != 0) { unit[Battle.UnitMovement] |= 0x10; granted += " Jump+1"; }
        }
        if (Options.Current.EveryoneNonCharge) // ReMixed's flag 0xF5 (0x8002D120)
            unit[Battle.UnitSupports + 3] |= 0x04; // 0x1E3
        if (granted.Length > 0)
            Tracer.Line(_game.Log, $"passives: a job 0x{unit[3]:X2} unit gets{granted}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"passives: {what} failed: {e}");
    }
}
