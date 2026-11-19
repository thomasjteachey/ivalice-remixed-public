namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's split formations (EVENT/ATTACK.OUT 0x801C4994, the formation screen's Start): vanilla refuses to start while
/// the current group has no unit deployed; ReMixed tests the deployed count plus the group's index, so in a battle with
/// two groups (var 0x34 = 2) the second can be left empty. Installed by <see cref="Rules"/> with "formation-split".
/// </summary>
/// <remarks>
/// The remaster's formation screen (0x2FC870) tests the deployed count (r12d) at 0x2FD230 (test r12d, r12d; jne to the
/// start, else message 0x900B). Those 9 bytes jump to a stub that ORs in the group's index (the dword the start
/// compares with var 0x34 − 1 at 0x2FD432); both are at least 0, so OR gives ReMixed's sum's zero test. It applies to the
/// remaster's own split battles too, as ReMixed's does to vanilla's.
/// <para>The remaster's own formation screen (its GenerateAttackTeamManager) asks its own question on Start (0x1EE890):
/// with two groups and none placed in the second, it refuses (messages 0x79 and 0x7C) unless every unit that can be
/// placed is in the first, so a mission wouldn't start with its second group empty (the harness placed one there,
/// TESTING.md). Its jne past that refusal (0x1EE928) becomes a jmp: the first group still has to have a unit
/// (message 0x78), as ReMixed's sum does on the first group.</para>
/// </remarks>
internal static unsafe class FormationStart
{
    public const int StubSize = 48;

    /// <summary>For the offline check: the site, the group index and the stub.</summary>
    internal static nint At { get; private set; }
    internal static int* Group { get; private set; }
    internal static nint Stub { get; private set; }

    /// <summary>The remaster's own Start check: after the second group's count (call 0x1EE830 with group 1), test eax, eax;
    /// jne to the commence (75 24, made EB 24); then the refusal unless +0x724 equals the first group's count.</summary>
    internal const string SecondGroupTest = "41 8B D7 E8 ?? ?? ?? ?? 85 C0 75 24 8B 83 24 07 00 00 3B C5 74 1A";
    internal const int SecondGroupJump = 10;
    internal static nint SecondGroupAt { get; private set; }

    public static void Install(Game game)
    {
        var test = game.Image.FindUnique(SecondGroupTest) + SecondGroupJump;
        if (*(byte*)test != 0x75)
            throw new InvalidDataException("the formation's Start check isn't laid out as this DLL expects");
        GameImage.WriteCode(test, [0xEB]);
        SecondGroupAt = test;
        // xor ebx, ebx; test r12d, r12d; jne start; mov r11, [rsp+0x20]; lea edi, [rbx+0xD]; mov eax, 0xFFFF900B.
        var site = game.Image.FindUnique("33 DB 45 85 E4 0F 85 ?? ?? ?? ?? 4C 8B 5C 24 20 8D 7B 0D B8 0B 90 FF FF") + 2;
        var start = GameImage.Rip(site + 3, 2, 6);
        // The start: eax = var 0x34; dec eax; mov [..], 0x85; cmp eax, [the group's index].
        ReadOnlySpan<byte> head = [0x8B, 0x05];
        if (!new ReadOnlySpan<byte>((void*)start, 2).SequenceEqual(head) || ((byte*)start)[6] != 0xFF
            || ((byte*)start)[18] != 0x3B || ((byte*)start)[19] != 0x05)
            throw new InvalidDataException("the formation screen's start isn't laid out as this DLL expects");
        var group = (int*)GameImage.Rip(start + 18, 2, 6);
        var stub = GameImage.AllocateCodeNear(site, StubSize);
        WriteStub(stub, group, start, site + 9);
        var jump = new byte[9];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (site + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(site, jump);
        At = site;
        Group = group;
        Stub = (nint)stub;
    }

    /// <summary>The stub: eax = the group's index | r12d (the deployed count); non-zero to <paramref name="start"/>.</summary>
    internal static void WriteStub(byte* stub, int* group, nint start, nint refuse)
    {
        var code = new List<byte> { 0x48, 0xB8 };                  // mov rax, group
        code.AddRange(BitConverter.GetBytes((long)group));
        code.AddRange([0x8B, 0x00]);                               // mov eax, [rax]
        code.AddRange([0x41, 0x0B, 0xC4]);                         // or eax, r12d
        code.AddRange([0x74, 0x0E]);                               // jz refuse
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);                   // jmp [rip+0] → start
        code.AddRange(BitConverter.GetBytes((long)start));
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);                   // refuse: jmp [rip+0] → back
        code.AddRange(BitConverter.GetBytes((long)refuse));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the formation start stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
