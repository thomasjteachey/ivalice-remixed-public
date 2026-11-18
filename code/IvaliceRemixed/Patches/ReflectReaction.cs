namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Reflect reaction (docs/MECHANICS.md: "Reflect as a reaction"; PS1 0x8018CA24): reaction 0x1B8, which
/// ReMixed puts in White Magic's reaction slot, reflects spells like the Reflect status. Installed by
/// <see cref="Rules"/> with "reflect-reaction".
/// </summary>
/// <remarks>
/// The guard reactions' shared conditions (PS1 0x8018C9E4; the remaster's 0x30AFEC) bounce a reflectable spell off a
/// target with Reflect (+0x65 &amp; 2) and queue 0x1B8 to show it. ReMixed's test is the status or the reaction
/// (PS1 +0x8D &amp; 0x20, the remaster's +0x96), before the can-react check, so like the status it works while the
/// unit is asleep or stopped. The remaster's status test (0x30B041) jumps to a stub with both. The reflected spell lands
/// on its caster's panel (<see cref="InstallBounce"/>).
/// </remarks>
internal static unsafe class ReflectReaction
{
    public const int Ability = 0x1B8;
    private const int StubSize = 48;
    private const int ReactionByte = Battle.UnitReactions + (Ability - 0x1A6) / 8, ReactionBit = 0x80 >> ((Ability - 0x1A6) % 8);

    // For the offline check.
    internal static nint At { get; private set; }
    internal static nint StubAt { get; private set; }

    public static void Install(Game game)
    {
        // mov r10, [target]; mov r11, [result]; test byte [r10+0x65], 2 (Reflect); je; test cx, cx; jne;
        // cmp byte [skillset], 0x15 (Math Skill isn't reflected)
        var start = game.Image.FindUnique(
            "4C 8B 15 ?? ?? ?? ?? 4C 8B 1D ?? ?? ?? ?? 41 F6 42 65 02 74 ?? 66 85 C9 75 ?? 80 3D ?? ?? ?? ?? 15");
        if ((byte**)GameImage.Rip(start, 3, 7) != game.Routines.Target || (byte**)GameImage.Rip(start + 7, 3, 7) != game.Battle.TargetResult)
            throw new InvalidDataException("the Reflect check's target or result isn't the one this DLL expects");
        var at = start + 14;
        var skip = at + 7 + (sbyte)((byte*)at)[6];
        var stub = GameImage.AllocateCodeNear(at, StubSize);
        WriteStub(stub, at + 7, skip);
        byte[] patch = [0xE9, 0, 0, 0, 0, 0x90, 0x90];
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(patch, 1);
        GameImage.WriteCode(at, patch);
        At = at;
        StubAt = (nint)stub;
    }

    /// <summary>The stub: r10 the target; changes the flags, then jumps to <paramref name="reflect"/> or <paramref name="skip"/>.</summary>
    internal static void WriteStub(byte* stub, nint reflect, nint skip)
    {
        var code = new List<byte>
        {
            0x41, 0xF6, 0x42, Battle.UnitStatuses + 4, 0x02, 0x75, 0x00, // test byte [r10+0x65], 2 (the status); jnz reflect
        };
        var toReflect = code.Count - 1;
        code.AddRange([0x41, 0xF6, 0x82]);            // test byte [r10+0x96], 0x20 (the reaction)
        code.AddRange(BitConverter.GetBytes(ReactionByte));
        code.AddRange([ReactionBit, 0x75, 0x00]);     // jnz reflect
        var toReflect2 = code.Count - 1;
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);      // jmp [rip+0]: skip
        code.AddRange(BitConverter.GetBytes((ulong)skip));
        code[toReflect] = (byte)(code.Count - (toReflect + 1));
        code[toReflect2] = (byte)(code.Count - (toReflect2 + 1));
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);      // reflect: jmp [rip+0]: on with the Reflect checks
        code.AddRange(BitConverter.GetBytes((ulong)reflect));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the Reflect stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }

    // ---------------------------------------------------------------- the bounce

    internal static nint BounceAt { get; private set; }
    internal static nint CasterX { get; private set; }
    internal static nint CasterY { get; private set; }
    internal const int BounceSize = 31;

    /// <summary>
    /// Where a reflected spell goes (PS1 0x8017E590, the reaction 0x1B8's action, for the status and the reaction):
    /// vanilla aims it as far again past the reflecting unit (its place minus the caster's, 0x8018F2B0 adding that to
    /// the reflector's); ReMixed swaps the operands (0x8017E5A8), so it lands on the caster's own panel. The remaster's
    /// 0x2831D8 (one caller, which keeps only eax) works out vanilla's difference in 31 bytes at 0x2831DE, rewritten here
    /// with the same instructions the other way round.
    /// </summary>
    public static void InstallBounce(Game game)
    {
        // movzx eax, [caster x]; mov rbx, rcx; movzx edx, [rcx+0x4F]; movzx r8d, [rcx+0x50]; sub edx, eax;
        // movzx eax, [caster y]; sub r8d, eax; then the zero test.
        var at = game.Image.FindUnique(
            "0F B6 05 ?? ?? ?? ?? 48 8B D9 0F B6 51 4F 44 0F B6 41 50 2B D0 0F B6 05 ?? ?? ?? ?? 44 2B C0 85 D2 75 05 45 85 C0 74 2A");
        CasterX = GameImage.Rip(at, 3, 7);
        CasterY = GameImage.Rip(at + 21, 3, 7);
        GameImage.WriteCode(at, BounceCode(at, CasterX, CasterY));
        BounceAt = at;
    }

    /// <summary>edx = caster x − the unit's x, r8d = caster y − its y, rbx = rcx, for code placed at <paramref name="at"/>.</summary>
    internal static byte[] BounceCode(nint at, nint casterX, nint casterY)
    {
        var code = new List<byte> { 0x0F, 0xB6, 0x15 };                            // movzx edx, byte [caster x]
        code.AddRange(BitConverter.GetBytes(checked((int)(casterX - (at + 7)))));
        code.AddRange([0x48, 0x8B, 0xD9]);                                          // mov rbx, rcx
        code.AddRange([0x0F, 0xB6, 0x41, 0x4F]);                                    // movzx eax, byte [rcx+0x4F]
        code.AddRange([0x2B, 0xD0]);                                                // sub edx, eax
        code.AddRange([0x44, 0x0F, 0xB6, 0x05]);                                    // movzx r8d, byte [caster y]
        code.AddRange(BitConverter.GetBytes(checked((int)(casterY - (at + code.Count + 4)))));
        code.AddRange([0x0F, 0xB6, 0x41, 0x50]);                                    // movzx eax, byte [rcx+0x50]
        code.AddRange([0x44, 0x2B, 0xC0]);                                          // sub r8d, eax
        if (code.Count != BounceSize)
            throw new InvalidOperationException("the bounce's rewrite isn't 31 bytes");
        return code.ToArray();
    }
}
