namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's job and equipment locks (docs/MECHANICS.md: "Job and equipment locks"; WORLD 0x80125540, 0x8011A468):
/// the party menu's lock check by the errand byte's bits instead of locking any unit that has one. Installed by
/// <see cref="Rules"/> with "errand-locks".
/// </summary>
/// <remarks>
/// <para>The remaster's lock check (0x289298: index → −2 errand, −3 monster or job, −4 egg, −6 Mime, else 1; the record
/// [0x1800F50 + 8 × index], its errand byte +0x167, a copy of the roster's +0x11E) is vanilla's 0x80125540. ReMixed
/// drops the errand test and, after the Mime's, gives −6 for 0x40 and 0 for 0x01. Its callers read 0 differently: the
/// equipment screens allow it (≥ 0), setting an ability wants 1, and the Job menu empties for & 0x21. The remaster's
/// callers mostly test the sign as well, and its job change asks through 0x289320, which wants exactly 1; that wrapper
/// gets ReMixed's 0x20 too. So a unit away on an errand (the dispatch writes 1) can re-equip but not change job.</para>
/// <para>Two changes in 0x289298: its errand test (0x2892C0, 14 bytes) becomes a jump past it, and its Mime tail
/// (0x2892F2, 18 bytes, rcx the record) a jump to a stub. One in 0x289320: its test of the result (0x289339, cmp eax, 1;
/// jne) a jump to a stub that also refuses & 0x20. The menus' own tests of +0x167 (greying 0x2C28D7, icons 0x2BEFA8,
/// the mode checks 0x2C0EFC and 0x2C0F70, the list builder 0x2850D0, the equipment search 0x28A01D) aren't changed: which
/// rebuilt menu each belongs to wants checking in the game.</para>
/// </remarks>
internal static unsafe class ErrandLocks
{
    public const int Errand = 0x167, Away = 0x01, Locked = 0x40, NoJob = 0x20, Mime = 0x5D;
    public const int StubSize = 64;

    /// <summary>For the offline check: the check, the job change's wrapper and the stubs.</summary>
    internal static nint Check { get; private set; }
    internal static nint JobCheck { get; private set; }
    internal static nint TailStub { get; private set; }
    internal static nint JobStub { get; private set; }

    public static void Install(Game game)
    {
        var image = game.Image;
        // The check: its bounds, the records, the errand test, the monster test, the job test, the egg, the Mime.
        var check = image.FindUnique(
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 83 F9 38 73 5F 33 FF 85 C9 78 59 48 63 D9 48 8D 35 ?? ?? ?? ?? " +
            "48 8B 04 DE 40 38 B8 67 01 00 00 74 05 8D 47 FE EB 40 66 39 78 3E 75 30 0F BF 48 24 E8 ?? ?? ?? ?? 85 C0 75 23 " +
            "48 8B 0C DE F6 41 70 04 74 07 B8 FC FF FF FF EB 1C 66 83 79 24 5D B8 01 00 00 00 8D 50 F9 0F 44 C2 EB 0A");
        var errandTest = check + 0x28;
        var tail = check + 0x5A;
        var back = check + 0x76;
        // The job change's wrapper: bounds, the check, cmp eax, 1; jne out; the records; jobs 0xA1 and 0xA4 refused.
        var job = image.FindUnique(
            "40 53 48 83 EC 20 48 63 D9 83 FB 38 73 35 85 C9 78 31 8B CB E8 ?? ?? ?? ?? 83 F8 01 75 28 48 8D 15 ?? ?? ?? ?? " +
            "48 8B 0C DA 0F BF 51 24 81 EA A1 00 00 00");
        if (GameImage.Rip(job + 0x14, 1, 5) != check)
            throw new InvalidDataException("the job change's wrapper doesn't call the lock check");
        var resultTest = job + 0x19;
        var records = GameImage.Rip(job + 0x1E, 3, 7);
        var jobOn = job + 0x1E;
        var jobOut = job + 0x46;

        var tailStub = GameImage.AllocateCodeNear(tail, StubSize);
        WriteTailStub(tailStub, back);
        var jobStub = GameImage.AllocateCodeNear(resultTest, StubSize);
        WriteJobStub(jobStub, records, jobOn, jobOut);

        byte[] skip = [0xEB, 0x0C, .. Enumerable.Repeat((byte)0x90, 12)];
        GameImage.WriteCode(errandTest, skip);
        GameImage.WriteCode(tail, Jump(tail, (nint)tailStub, 18));
        GameImage.WriteCode(resultTest, Jump(resultTest, (nint)jobStub, 5));
        (Check, JobCheck, TailStub, JobStub) = (check, job, (nint)tailStub, (nint)jobStub);
    }

    private static byte[] Jump(nint at, nint to, int length)
    {
        var jump = new byte[length];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)(to - (at + 5)))).CopyTo(jump, 1);
        return jump;
    }

    /// <summary>The check's tail (rcx the record): a Mime or & 0x40 → −6, & 0x01 → 0, else 1; then <paramref name="back"/>.</summary>
    internal static void WriteTailStub(byte* stub, nint back)
    {
        var code = new List<byte>
        {
            0x66, 0x83, 0x79, 0x24, Mime,                // cmp word [rcx+0x24], 0x5D
            0x74, 0x19,                                  // je six
            0xF6, 0x81, 0x67, 0x01, 0x00, 0x00, Locked,  // test byte [rcx+0x167], 0x40
            0x75, 0x10,                                  // jnz six
            0xF6, 0x81, 0x67, 0x01, 0x00, 0x00, Away,    // test byte [rcx+0x167], 0x01
            0x75, 0x0E,                                  // jnz zero
            0xB8, 0x01, 0x00, 0x00, 0x00,                // mov eax, 1
            0xEB, 0x09,                                  // jmp done
            0xB8, 0xFA, 0xFF, 0xFF, 0xFF,                // six: mov eax, -6
            0xEB, 0x02,                                  // jmp done
            0x33, 0xC0,                                  // zero: xor eax, eax
            0xFF, 0x25, 0, 0, 0, 0,                      // done: jmp [rip+0] → back
        };
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the lock check's stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>
    /// The job change's result test (eax the check's, rbx the index): not 1 → <paramref name="refuse"/> as it is; a record
    /// with & 0x20 → −2 there; else <paramref name="allow"/>.
    /// </summary>
    internal static void WriteJobStub(byte* stub, nint records, nint allow, nint refuse)
    {
        var code = new List<byte>
        {
            0x83, 0xF8, 0x01,                            // cmp eax, 1
            0x75, 0x2A,                                  // jne out
            0x48, 0xB9,                                  // mov rcx, records
        };
        code.AddRange(BitConverter.GetBytes((long)records));
        code.AddRange([0x48, 0x8B, 0x0C, 0xD9]);                         // mov rcx, [rcx+rbx*8]
        code.AddRange([0xF6, 0x81, 0x67, 0x01, 0x00, 0x00, NoJob]);      // test byte [rcx+0x167], 0x20
        code.AddRange([0x75, 0x0E]);                                     // jnz refused
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);                         // jmp [rip+0] → allow
        code.AddRange(BitConverter.GetBytes((long)allow));
        code.AddRange([0xB8, 0xFE, 0xFF, 0xFF, 0xFF]);                   // refused: mov eax, -2
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);                         // out: jmp [rip+0] → refuse
        code.AddRange(BitConverter.GetBytes((long)refuse));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the job change's stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
