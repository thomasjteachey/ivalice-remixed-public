namespace IvaliceRemixed.Patches;

/// <summary>
/// The abilities a unit can't learn left off the job ability lists (asked for 2026-11-06, after "???" entries on the
/// Deck, and again 2026-11-12, still on the Monk's). ReMixed gives the Thief's and the Monk's skillsets one of the
/// assassins' skills each (Shadow Stitch 0xB6, Stop Bracelet 0xB7) that can't be learned (no JP cost, a learn rate of
/// 0), for the enemies that use their skillset; the remaster lists such an ability as "???" (its name, icon and
/// description hidden) until a unit has it. A unit that has one (learned on hit, say) lists it as before. Installed by
/// <see cref="Rules"/> with "hide-unlearnable".
/// </summary>
/// <remarks>
/// <para>The remaster builds these lists in three places, and each marks an ability the unit hasn't learned with
/// 0x4000, or 0x6000 (the "???" mark) when its flags (the ability's +3, the table at 0x787F80) have bit 7, can't learn
/// with JP. Each now leaves that ability out instead, so no entry is made or counted for it, as the learn screen
/// (0x2867BC's mode 2) already did:</para>
/// <list type="bullet">
/// <item>0x2867BC (unit, job, kind, the list, mode), mode 0: mov eax, 0x6000 (0x286A25) becomes a jump to the loop's
/// next ability (jmp +0x10, then three nops). This alone didn't take the "???" away (2026-11-12).</item>
/// <item>0x28A474 (8-byte entries, through 0x28A6E8 and 0x28A770), modes 0, 4, 6 and 7, the remaster's own list
/// screens (modes 7 and 4 from 0x2BA05A and 0x2BA3F3, 4 from 0x3677EE): all four reach or ebx, 0x6000
/// (0x28A678), which becomes a jump to the loop's next ability (jmp +0x12, then four nops). The callers take the count
/// it returns.</item>
/// <item>0x392620 (the unit at 0x1D3B778): every ability not learned is counted and listed, 0x2000 added from the flags'
/// sign (sar dl, 7). From 0x39277D the flags are tested first (cmp byte [rdx + r8 × 8], 0; jl to the loop's next
/// ability, add rsi, r13), then counted as before with no 0x2000 (xor ecx, ecx). Its mode-2 caller 0x392920 sets
/// 0x3C5220C when the count is 0 (nothing left to learn, it seems), which an unlearnable ability no longer holds
/// off.</item>
/// </list>
/// <para>Left as they are: the counts of abilities not learned (0x2867BC's and 0x28A474's mode 3), which list
/// nothing.</para>
/// </remarks>
internal static unsafe class HideUnlearnable
{
    private const string Branch =
        "45 85 C0 75 17 B8 00 40 00 00 45 38 8C DA ?? ?? ?? ?? 7D 05 B8 00 60 00 00 66 0B D8 FF C7 66 41 89 1C 77 48 FF C6 48 FF C5";
    internal const int MarkAt = 20, NextAt = 38;
    internal static readonly byte[] Skip = [0xEB, 0x10, 0x90, 0x90, 0x90];

    // 0x28A474: cmp [r10 + r8 × 8 + flags], r9b; jl 0x6000; bts ebx, 0xE; jmp store; or ebx, 0x6000; the store
    // (movsxd rax, edi; the ability and the job; inc edi); the next ability (mov rbx, [rsp + 0x60]).
    private const string Entries =
        "47 38 8C C2 ?? ?? ?? ?? 7C 06 0F BA EB 0E EB 06 81 CB 00 60 00 00 48 63 C7 66 89 5C C6 02 66 89 2C C6 FF C7 48 8B 5C 24 60";
    internal const int EntriesMarkAt = 16, EntriesNextAt = 36;
    internal static readonly byte[] EntriesSkip = [0xEB, EntriesNextAt - EntriesMarkAt - 2, 0x90, 0x90, 0x90, 0x90];

    // 0x392620: lea rdx, flags; add edi, r13d; mov dl, [rdx + r8 × 8]; sar dl, 7; movsx ecx, dl; and cx, 0x2000;
    // add cx, 0x4000; or cx, r8w; the store; the learned one's store in mode 0; the next ability (add rsi, r13).
    private const string Unlearned =
        "48 8D 15 ?? ?? ?? ?? 41 03 FD 42 8A 14 C2 C0 FA 07 0F BE CA BA 00 20 00 00 66 23 CA BA 00 40 00 00 66 03 CA " +
        "66 41 0B C8 66 41 89 0E 44 0F B7 C1 49 83 C6 02 39 6C 24 70 75 0F 85 C0 74 0B 41 03 FD 66 45 89 06 49 83 C6 02 49 03 F5";
    internal const int UnlearnedAt = 7, UnlearnedNextAt = 73;
    // cmp byte [rdx + r8 × 8], 0; jl next; add edi, r13d; xor ecx, ecx; nop (13 bytes, as many as it replaces).
    internal static readonly byte[] UnlearnedTest =
        [0x42, 0x80, 0x3C, 0xC2, 0x00, 0x7C, UnlearnedNextAt - UnlearnedAt - 7, 0x41, 0x03, 0xFD, 0x31, 0xC9, 0x90];

    // For the offline check.
    internal static nint Site { get; private set; }
    internal static nint EntriesSite { get; private set; }
    internal static nint UnlearnedSite { get; private set; }

    public static void Install(Game game)
    {
        // All three found before any is written, so a missing one leaves the game as it was.
        var branch = game.Image.FindUnique(Branch) + MarkAt;
        var entries = game.Image.FindUnique(Entries) + EntriesMarkAt;
        var unlearned = game.Image.FindUnique(Unlearned) + UnlearnedAt;
        Site = branch;
        EntriesSite = entries;
        UnlearnedSite = unlearned;
        GameImage.WriteCode(Site, Skip);
        GameImage.WriteCode(EntriesSite, EntriesSkip);
        GameImage.WriteCode(UnlearnedSite, UnlearnedTest);
    }
}
