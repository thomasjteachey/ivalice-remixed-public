using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's strikes and shots at bodies (docs/analysis/battle-in-place-review.md, 0x8017C34C and 0x8008DC50): a melee or
/// spear strike aimed at a dead unit's panel resolves to the body, and a bow shot aimed at one reaches it; bodies still
/// block nothing. Installed by <see cref="Rules"/> with "hitting-bodies".
/// </summary>
/// <remarks>
/// <para>Strikes: the tile info (PS1 0x8017C158; 0x281248) no longer marks a dead unit absent (+3 = 0x80; the remaster's
/// <c>cmovne eax, r11d</c> at 0x2813F4 goes), so the melee strike's lookup finds the body. The spear's middle tile
/// (PS1 0x8017B2A0 → 0x801503C0; the call at 0x28074C) puts the test back, so a body there doesn't stop the strike
/// short.</para>
/// <para>Shots: a dead unit's height for weapon paths (PS1 0x8008DC24, the remaster's two copies 0x273698 and 0x2736D0)
/// is 1 instead of 0, a marker; where the Arc solver (0x3129E0) reads the target's height (PS1 0x801AFF94 →
/// 0x80155900; here 0x312A40) and the obstacle list (0x3124E8) each unit's (PS1 0x801AFA98 → 0x80155A90; here
/// 0x312597), a marked body gets full height (0x24) on the panel the attacker aimed at (+0x1AA type, +0x1AC x,
/// +0x1AE level, +0x1B0 y) and 0 anywhere else. The action routine still skips a dead target unless the action's
/// statuses touch Dead (PS1 0x8018BA44), so this matters for abilities that act on bodies.</para>
/// <para>With it, ReMixed's sprite set table (PS1 0x80094748, 4 bytes a set: type, actor type, ?, height; the
/// remaster's .rdata 0x673300) gives sets 0x4A and 0x4B height 24 instead of 36, which the weapon paths read. Its type
/// and actor type changes there (sets 0x46, 0x4A, 0x4B) go with ReMixed's animations and aren't ported. ReMixed's other
/// arc changes (Jumping units and same-team units in the obstacle list) go with the previews.</para>
/// </remarks>
internal static unsafe class HittingBodies
{
    public const int FullHeight = 0x24, Marker = 1;
    public const int AimType = 0x1AA, AimX = 0x1AC, AimLevel = 0x1AE, AimY = 0x1B0, X = 0x4F, Y = 0x50, Level = 0x51;
    public const int SpearStubSize = 64, DeadStubSize = 48, HeightStubSize = 64;
    public const byte ShortSet = 0x4A, ShortSet2 = 0x4B, ShortHeight = 24, TallHeight = 36;

    private static Game _game = null!;
    private static int _failed;

    // For the offline check.
    internal static nint DeadTest { get; private set; }
    internal static nint SpearStub { get; private set; }
    internal static IReadOnlyList<nint> DeadSites { get; private set; } = [];
    internal static IReadOnlyList<nint> DeadStubs { get; private set; } = [];
    internal static nint Managed { get; private set; }
    /// <summary>The sprite set table's height bytes (set × 4).</summary>
    internal static byte* SetHeights { get; private set; }

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        var tileInfo = (nint)game.RuleCode.TileInfo;
        var units = game.TurnCode.Units;
        // The tile info's dead test: test [r9+0x61], 0x20; mov [r8+5], dl; cmovne eax, r11d (the 0x80 "absent").
        var dead = image.FindUnique("41 F6 41 61 20 41 88 50 05 41 0F 45 C3 45 88 50 06") + 9;
        if (dead - tileInfo is < 0 or > 0x200)
            throw new InvalidDataException("the dead test isn't in the tile info");
        // The spear's middle tile: mov edx, r8d; lea rcx, [rbp-0x18]; call the tile info; mov rcx, rax; movzx edx, dil.
        var spear = image.FindUnique("41 8B D0 48 8D 4D E8 E8 ?? ?? ?? ?? 48 8B C8 40 0F B6 D7") + 7;
        if (GameImage.Rip(spear, 1, 5) != tileInfo)
            throw new InvalidDataException("the spear's middle tile doesn't call the tile info");
        // The weapon-path height's two copies: the sprite, its 0x160 & 9 test, then its dead test (0x15C & 4) at +21.
        var heights = image.FindExactly(
            "E8 ?? ?? ?? ?? 33 C9 48 85 C0 74 21 F6 80 60 01 00 00 09 75 18 F6 80 5C 01 00 00 04 75 0F 0F B6 40 0A", 2);
        // The Arc solver's target height (ecx the target, r14d the attacker) and the obstacle list's (r12d the attacker).
        var target = image.FindUnique("41 83 F8 FF 74 0D 41 8B C8 E8 ?? ?? ?? ?? 89 45 E4 EB 04 83 65 E4 00 4C 8D 45 B0") + 9;
        var obstacle = image.FindUnique("41 0F B6 0E E8 ?? ?? ?? ?? 41 89 84 98 C4 00 00 00") + 4;
        var height = GameImage.Rip(target, 1, 5);
        if (GameImage.Rip(obstacle, 1, 5) != height || heights.All(h => h - 4 != height))
            throw new InvalidDataException("the Arc solver and the obstacle list don't read the weapon-path height");
        // The height's lea rcx, [the table + 3] (after movzx eax, [rax+0xA]).
        var setHeights = (byte*)GameImage.Rip(heights[0] + 0x22, 3, 7);
        if (setHeights[ShortSet * 4] != TallHeight || setHeights[ShortSet2 * 4] != TallHeight)
            throw new InvalidDataException("the sprite set table's heights aren't vanilla's");

        // Every stub first, so a failure leaves the game's code as it was.
        var spearStub = GameImage.AllocateCodeNear(spear, SpearStubSize);
        WriteSpearStub(spearStub, tileInfo, units);
        var sites = heights.Select(h => h + 21).ToList();
        var deadStubs = new List<nint>();
        foreach (var at in sites)
        {
            var stub = GameImage.AllocateCodeNear(at, DeadStubSize);
            WriteDeadStub(stub, at + 9, at + 0x18);
            deadStubs.Add((nint)stub);
        }
        var managed = Stub.KeepingRegisters((nint)(delegate* unmanaged<int, int, int, int>)&Height);
        var targetStub = GameImage.AllocateCodeNear(target, HeightStubSize);
        WriteHeightStub(targetStub, height, managed, [0x44, 0x89, 0xF2]);   // mov edx, r14d
        var obstacleStub = GameImage.AllocateCodeNear(obstacle, HeightStubSize);
        WriteHeightStub(obstacleStub, height, managed, [0x44, 0x89, 0xE2]); // mov edx, r12d

        GameImage.WriteCode(dead, [0x0F, 0x1F, 0x40, 0x00]);
        WriteCall(spear, (nint)spearStub);
        for (var k = 0; k < sites.Count; k++)
        {
            var jump = new byte[9];
            Array.Fill(jump, (byte)0x90);
            jump[0] = 0xE9;
            BitConverter.GetBytes(checked((int)(deadStubs[k] - (sites[k] + 5)))).CopyTo(jump, 1);
            GameImage.WriteCode(sites[k], jump);
        }
        WriteCall(target, (nint)targetStub);
        WriteCall(obstacle, (nint)obstacleStub);
        GameImage.WriteCode((nint)(setHeights + ShortSet * 4), [ShortHeight]);
        GameImage.WriteCode((nint)(setHeights + ShortSet2 * 4), [ShortHeight]);
        SetHeights = setHeights;
        DeadTest = dead;
        SpearStub = (nint)spearStub;
        DeadSites = sites;
        DeadStubs = deadStubs;
        Managed = managed;
    }

    private static void WriteCall(nint at, nint to)
    {
        byte[] call = [0xE8, 0, 0, 0, 0];
        BitConverter.GetBytes(checked((int)(to - (at + 5)))).CopyTo(call, 1);
        GameImage.WriteCode(at, call);
    }

    /// <summary>
    /// The spear's middle tile (PS1 0x801503C0): the tile info, then a dead unit (+0x61 &amp; 0x20) marked absent (+3 =
    /// 0x80) as vanilla's tile info did. (rcx the buffer, edx the battle index; returns the buffer.)
    /// </summary>
    internal static void WriteSpearStub(byte* stub, nint tileInfo, byte* units)
    {
        var code = new List<byte> { 0x52, 0x48, 0x83, 0xEC, 0x20, 0x48, 0xB8 };   // push rdx; sub rsp, 0x20; mov rax, tile info
        code.AddRange(BitConverter.GetBytes((long)tileInfo));
        code.AddRange([0xFF, 0xD0, 0x48, 0x83, 0xC4, 0x20, 0x5A]);              // call rax; add rsp, 0x20; pop rdx
        code.AddRange([0x83, 0xFA, 0x14, 0x77, 0x00]);                            // cmp edx, 0x14; ja done
        var above = code.Count - 1;
        code.AddRange([0x48, 0x63, 0xD2, 0x48, 0xC1, 0xE2, 0x09, 0x48, 0xB9]);  // movsxd rdx, edx; shl rdx, 9; mov rcx, units
        code.AddRange(BitConverter.GetBytes((long)units));
        code.AddRange([0xF6, 0x44, 0x0A, 0x61, 0x20, 0x74, 0x04]);              // test byte [rdx+rcx+0x61], 0x20; jz done
        code.AddRange([0xC6, 0x40, 0x03, 0x80]);                                  // mov byte [rax+3], 0x80
        code[above] = (byte)(code.Count - (above + 1));
        code.Add(0xC3);                                                           // done: ret
        if (code.Count > SpearStubSize)
            throw new InvalidOperationException("the spear stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>
    /// A weapon-path height's dead test (PS1 0x8008DC50): a dead sprite (rax; +0x15C &amp; 4) gives 1 (ecx, then
    /// <paramref name="done"/>'s mov eax, ecx) instead of 0; otherwise on to the height at <paramref name="next"/>.
    /// </summary>
    internal static void WriteDeadStub(byte* stub, nint next, nint done)
    {
        var code = new List<byte> { 0xF6, 0x80, 0x5C, 0x01, 0x00, 0x00, 0x04, 0x75, 0x0E }; // test [rax+0x15C], 4; jnz dead
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);                                              // jmp [rip+0] → next
        code.AddRange(BitConverter.GetBytes((long)next));
        code.AddRange([0xB9, 0x01, 0x00, 0x00, 0x00, 0xFF, 0x25, 0, 0, 0, 0]);               // dead: mov ecx, 1; jmp → done
        code.AddRange(BitConverter.GetBytes((long)done));
        if (code.Count > DeadStubSize)
            throw new InvalidOperationException("the dead height stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>
    /// A height read in the Arc solver: the game's height for the unit in ecx, then, only for the dead marker, the C#
    /// with the attacker (<paramref name="attacker"/> moves it into edx). Keeps r8, which the obstacle list's caller keeps.
    /// </summary>
    internal static void WriteHeightStub(byte* stub, nint height, nint managed, byte[] attacker)
    {
        var code = new List<byte> { 0x41, 0x50, 0x51, 0x48, 0x83, 0xEC, 0x28, 0x48, 0xB8 }; // push r8; push rcx; sub rsp, 0x28
        code.AddRange(BitConverter.GetBytes((long)height));
        code.AddRange([0xFF, 0xD0, 0x48, 0x83, 0xC4, 0x28, 0x59]);                         // call rax; add rsp, 0x28; pop rcx
        code.AddRange([0x83, 0xF8, Marker, 0x75, 0x00]);                                     // cmp eax, 1; jne done
        var jne = code.Count - 1;
        code.AddRange(attacker);
        code.AddRange([0x44, 0x8B, 0xC0, 0x48, 0xB8]);                                       // mov r8d, eax; mov rax, managed
        code.AddRange(BitConverter.GetBytes((long)managed));
        code.AddRange([0xFF, 0xD0]);                                                         // call rax
        code[jne] = (byte)(code.Count - (jne + 1));
        code.AddRange([0x41, 0x58, 0xC3]);                                                   // done: pop r8; ret
        if (code.Count > HeightStubSize)
            throw new InvalidOperationException("the height stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    [UnmanagedCallersOnly]
    private static int Height(int unit, int attacker, int height)
    {
        try
        {
            return BodyHeight(unit, attacker, height);
        }
        catch (Exception e)
        {
            if (Interlocked.Increment(ref _failed) <= 3)
                _game.Log.Error($"hitting bodies: the height failed: {e}");
            return 0;
        }
    }

    /// <summary>
    /// A marked body's height (PS1 0x80155900 and 0x80155A90): full on the panel the attacker aimed at, else 0. Units
    /// are by their id (+1), as the Arc solver passes them.
    /// </summary>
    internal static int BodyHeight(int unitId, int attackerId, int height)
    {
        if (height != Marker)
            return height;
        var unit = ById(unitId);
        var attacker = ById(attackerId);
        if (unit == null || attacker == null || attacker[AimType] == 0 || unit[X] != attacker[AimX]
            || unit[Y] != attacker[AimY] || unit[Level] >> 7 != attacker[AimLevel])
            return 0;
        Tracer.Line(_game.Log, $"hitting bodies: unit {unitId}'s body stands full height on unit {attackerId}'s aimed panel");
        return FullHeight;
    }

    private static byte* ById(int id)
    {
        if ((uint)id >= Battle.UnitCount)
            return null;
        var units = _game.TurnCode.Units;
        if (units[id * Battle.UnitSize + 1] == id)
            return units + id * Battle.UnitSize;
        for (var k = 0; k < Battle.UnitCount; k++)
            if (units[k * Battle.UnitSize + 1] == id)
                return units + k * Battle.UnitSize;
        return null;
    }
}
