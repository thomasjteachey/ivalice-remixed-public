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
/// </remarks>
internal static unsafe class FormationStart
{
    public const int StubSize = 48;

    /// <summary>For the offline check: the site, the group index and the stub.</summary>
    internal static nint At { get; private set; }
    internal static int* Group { get; private set; }
    internal static nint Stub { get; private set; }

    public static void Install(Game game)
    {
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
