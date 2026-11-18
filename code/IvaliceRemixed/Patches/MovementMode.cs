namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's movement mode (docs/MECHANICS.md: "Movement mode"; PS1 0x8017F020, rewritten in place): Waterbreathing
/// (0x1F9) moves in water like Swim (0x1F7), and so does a linked unit (a rider or its mount); Chicken and Frog lose
/// Teleport as well as Fly; Pass (0x1FC, vanilla Silent Walk's slot) has no mode bit. Installed by <see cref="Rules"/>
/// with "movement-mode".
/// </summary>
/// <remarks>
/// <para>The leaf returns a unit's movement mode: 0x80 Fly, 0x40 Float (the status or the ability; it drops the water
/// bits), 0x20 0x1F6, 0x10 0x1F7, 0x08 Teleport (0x1F2 or 0x1F3), and in vanilla 0x02 Silent Walk. ReMixed's Chocobos
/// have 0x1F9 where vanilla's had 0x1F7, so without this they lose their water movement on the remaster. The remaster's
/// leaf (0x283AB8; rcx the unit; reads +0x63, +0x9D and +0x9E, the PS1's +0x5A, +0x94 and +0x95) is vanilla's; its nine
/// callers keep only al and nothing else in the mod finds it by its bytes, so it's rewritten in place with ReMixed's
/// logic (<see cref="Mode"/>), using only the registers the game's own version clobbers.</para>
/// <para>The callers: the move routine (0x27D0DD, into the movement globals), the knockback's fall check (0x304EEC,
/// which <see cref="Knockback"/> calls itself), and sprite and map code that tests the water bits.</para>
/// </remarks>
internal static unsafe class MovementMode
{
    private const int Statuses = 0x63, Movement = 0x9D, Linked = 0x1B4;

    // The remaster's leaf, whole (190 bytes): vanilla's logic.
    private const string Leaf =
        "48 89 5C 24 08 44 8A 49 63 32 DB 44 0F B6 81 9E 00 00 00 41 8A D1 44 8A 99 9D 00 00 00 41 8A C0 0C 08 80 E2 40 " +
        "0F B6 C8 41 0F 44 C8 0F B6 D1 8A C2 24 FB 41 80 E1 06 44 0F B6 D0 41 B9 10 00 00 00 44 0F 44 D2 45 84 D2 79 04 " +
        "B0 20 EB 0B 41 F6 C2 40 0F B6 C3 41 0F 45 C1 0F B6 D0 41 8A CA 8A C2 0C 02 44 0F B6 C0 80 E1 02 41 8A C2 44 0F " +
        "44 C2 24 08 41 F6 C2 04 74 06 41 80 C8 80 EB 0A 41 F6 C3 0C 74 0E 41 80 C8 08 84 C0 74 28 41 80 E0 CE EB 08 84 " +
        "C0 74 0A 41 80 E0 06 41 80 C8 40 EB 14 45 84 D2 79 06 41 80 C8 20 EB 09 41 F6 C2 40 74 03 45 0A C1 48 8B 5C 24 " +
        "08 41 8A C0 C3";
    public const int LeafSize = 190;

    /// <summary>The leaf, rewritten; for the offline check.</summary>
    internal static nint LeafAt { get; private set; }

    /// <summary>The game's own leaf, byte for byte (it has nothing position-dependent); the offline check runs a copy.</summary>
    internal static byte[] Original() => Leaf.Split(' ').Select(t => Convert.ToByte(t, 16)).ToArray();

    public static void Install(Game game)
    {
        var leaf = game.Image.FindUnique(Leaf);
        var code = Code();
        var bytes = new byte[LeafSize];
        Array.Fill(bytes, (byte)0xCC);
        code.CopyTo(bytes, 0);
        GameImage.WriteCode(leaf, bytes);
        LeafAt = leaf;
    }

    /// <summary>ReMixed's leaf (PS1 0x8017F020) in x64: rcx the unit; uses rax and rdx only; the mode in eax.</summary>
    internal static byte[] Code()
    {
        var c = new List<byte>();
        var fixups = new List<(int At, string Label)>();
        var labels = new Dictionary<string, int>();
        void Emit(params byte[] b) => c.AddRange(b);
        void Jump(byte op, string label)
        {
            Emit(op, 0);
            fixups.Add((c.Count - 1, label));
        }
        void Label(string name) => labels[name] = c.Count;

        Emit(0x0F, 0xB7, 0x81, Movement, 0, 0, 0);       // movzx eax, word [rcx+0x9D]: al the PS1's +0x94, ah its +0x95
        Emit(0x0F, 0xB6, 0x51, Statuses);                // movzx edx, byte [rcx+0x63]
        Emit(0xF6, 0x81, Linked & 0xFF, Linked >> 8, 0, 0, 0x80); // test byte [rcx+0x1B4], 0x80: linked (riding)
        Jump(0x74, "float");
        Emit(0x0D, 0x00, 0x40, 0x00, 0x00);              // or eax, 0x4000: as 0x1F7
        Label("float");
        Emit(0xF6, 0xC2, 0x40);                          // test dl, 0x40: the Float status
        Jump(0x74, "shape");
        Emit(0x0D, 0x00, 0x08, 0x00, 0x00);              // or eax, 0x800: as 0x1FA
        Label("shape");
        Emit(0xF6, 0xC2, 0x06);                          // test dl, 6: Chicken or Frog
        Jump(0x74, "water");
        Emit(0x25, 0x00, 0xFB, 0x00, 0x00);              // and eax, 0xFB00: no Fly, no Teleport
        Label("water");
        Emit(0x31, 0xD2);                                // xor edx, edx
        Emit(0xF6, 0xC4, 0x80);                          // test ah, 0x80: 0x1F6
        Jump(0x74, "swim");
        Emit(0xB2, 0x20);                                // mov dl, 0x20
        Jump(0xEB, "fly");
        Label("swim");
        Emit(0xF6, 0xC4, 0x50);                          // test ah, 0x50: 0x1F7 or 0x1F9
        Jump(0x74, "fly");
        Emit(0xB2, 0x10);                                // mov dl, 0x10
        Label("fly");
        Emit(0xF6, 0xC4, 0x04);                          // test ah, 4: Fly (0x1FB)
        Jump(0x74, "teleport");
        Emit(0x80, 0xCA, 0x80);                          // or dl, 0x80
        Jump(0xEB, "floating");
        Label("teleport");
        Emit(0xA8, 0x0C);                                // test al, 0x0C: Teleport, Teleport 2
        Jump(0x74, "plain");
        Emit(0x80, 0xCA, 0x08);                          // or dl, 8
        Label("floating");
        Emit(0xF6, 0xC4, 0x08);                          // test ah, 8: Float
        Jump(0x74, "done");
        Emit(0x80, 0xE2, 0x8E);                          // and dl, 0x8E: no water bits
        Emit(0x80, 0xCA, 0x40);                          // or dl, 0x40
        Jump(0xEB, "done");
        Label("plain");
        Emit(0xF6, 0xC4, 0x08);                          // test ah, 8: Float
        Jump(0x74, "done");
        Emit(0xB2, 0x40);                                // mov dl, 0x40
        Label("done");
        Emit(0x0F, 0xB6, 0xC2);                          // movzx eax, dl
        Emit(0xC3);                                      // ret
        foreach (var (at, label) in fixups)
            c[at] = checked((byte)(sbyte)(labels[label] - (at + 1)));
        if (c.Count > LeafSize)
            throw new InvalidOperationException("the movement mode leaf is too big");
        return c.ToArray();
    }

    /// <summary>ReMixed's leaf (PS1 0x8017F020) in C#, from its MIPS: what the native code must match.</summary>
    internal static int Mode(byte statuses, byte movement1, byte movement2, bool linked)
    {
        int a = movement1 | movement2 << 8; // the PS1's halfword at +0x94
        if (linked)
            a |= 0x4000;
        if ((statuses & 0x40) != 0)
            a |= 0x800;
        if ((statuses & 6) != 0)
            a &= 0xFB00;
        int mode = (a & 0x8000) != 0 ? 0x20 : (a & 0x5000) != 0 ? 0x10 : 0;
        if ((a & 0x400) != 0)
            mode |= 0x80;
        else if ((a & 0x0C) != 0)
            mode |= 0x08;
        else
            return (a & 0x800) != 0 ? 0x40 : mode;
        return (a & 0x800) != 0 ? (mode & 0x8E) | 0x40 : mode;
    }

    /// <summary>Vanilla's leaf (the remaster's 0x283AB8 before this) in C#, for the check's comparison.</summary>
    internal static int VanillaMode(byte statuses, byte movement1, byte movement2)
    {
        int b = movement2;
        if ((statuses & 0x40) != 0)
            b |= 0x08;
        if ((statuses & 6) != 0)
            b &= 0xFB;
        int mode = (b & 0x80) != 0 ? 0x20 : (b & 0x40) != 0 ? 0x10 : 0;
        if ((b & 2) != 0)
            mode |= 2;
        if ((b & 4) != 0)
            mode |= 0x80;
        else if ((movement1 & 0x0C) != 0)
            mode |= 0x08;
        else
            return (b & 8) != 0 ? (mode & 6) | 0x40 : mode;
        return (b & 8) != 0 ? (mode & 0xCE) | 0x40 : mode;
    }
}
