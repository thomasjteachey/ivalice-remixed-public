using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's unlearning for JP (docs/MECHANICS.md: "Learn and unlearn for JP"; WORLD 0x8013DFF0): on the learn screen,
/// choosing an ability the unit has learned unlearns it and gives its JP cost back. Installed by <see cref="Rules"/>
/// with "unlearn", per the JP refund setting.
/// </summary>
/// <remarks>
/// <para>ReMixed makes the learn screen's marker toggle the learned bit (0x801256A4, or to xor) and settles the JP after
/// it (0x8013DFF0, from 0x8011FACC): a learned ability comes off with its cost back (half with flag 0xD7, none for a
/// cost of 9999, JP at most 9999), unless flag 0xD6 is set, it's the equipped reaction, support or movement, or it's
/// Equip Shield or Equip Sword (0x1C7, 0x1C8) while 0x1D0 is the support; then the toggle is undone. An unlearned
/// ability bought without the JP (the PS1's JP scroll glitch) goes through and leaves 0 JP, or with flag 0xD5 is
/// undone.</para>
/// <para>The remaster's learn screen (0x2B9598) tests the chosen entry with 0x2B956C: on screen 0xC whether it's
/// learned (+0x1000), on the others, the learn screen among them, whether it can be bought (+0x4000 clear, from the
/// list builder 0x2867BC). One that passes goes to state 5, the purchase dialog, whose yes buys it (0x2B9AD0: 0x28936C
/// sets the bit, the JP at the record's +0xCA + 2 × job index goes down by 0x2CC0AC's cost) and refreshes the list
/// (0x2B9996). One that fails gets message 6 (0x2B98CD: "This ability has already been learned" for a learned one).
/// Here that message's setup (12 bytes) jumps to a stub that asks <see cref="Choose"/>: on an unlearning it goes on to
/// the refresh, otherwise to the message as before. (Seen in the game, 2026-11-04: the first version hooked the
/// state-5 branch, which a learned entry on the learn screen never takes.) There's no scroll glitch to keep (an entry
/// without the JP is refused), so flag 0xD5 has nothing to port.</para>
/// </remarks>
internal static unsafe class Unlearn
{
    public const int Learned = 0x1000, LearnScreen = 0x0C, MaxJp = 9999, TwoSpells = 0x1D0;
    private const int Reaction = 0x62, Support = 0x64, Movement = 0x66, Flags = 0x70, LearnedBits = 0x7B, Jp = 0xCA;
    private const int Slots = 22;
    internal const int StubSize = 256;

    private static Game _game = null!;
    private static ushort* _entries, _job;
    private static int* _screen;
    private static nint* _records;
    private static short* _current;
    private static delegate* unmanaged<int, int> _jobIndex;
    private static delegate* unmanaged<int, int, ushort*> _abilities;
    private static int _failed;

    /// <summary>The JP cost (0x2CC0AC; the NXD isn't loaded offline). The offline check swaps it.</summary>
    internal static delegate* unmanaged<int, int> Cost;
    internal static nint BranchAt { get; private set; }
    internal static nint Refresh { get; private set; }
    internal static nint Resume { get; private set; }
    internal static nint Message { get; private set; }
    internal static byte* State { get; private set; }
    internal static ushort* Entries => _entries;
    internal static ushort* Job => _job;
    internal static int* Screen => _screen;

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        _ = game.Units.Jobs; // found now, so a missing pattern stops this rule here
        // The learn screen's choice: a learned entry (0x2B956C) goes to state 5, another to the purchase dialog.
        var branch = image.FindUnique(
            "8B CB E8 ?? ?? ?? ?? 48 85 C0 74 0C C6 05 ?? ?? ?? ?? 05 E9 ?? ?? ?? ?? 48 8D 15 ?? ?? ?? ?? B9 06 00 00 00 E8");
        var check = (byte*)GameImage.Rip(branch + 2, 1, 5);
        ReadOnlySpan<byte> checkCode = [0x83, 0x3D];
        if (!new ReadOnlySpan<byte>(check, 2).SequenceEqual(checkCode) || check[6] != LearnScreen
            || check[7] != 0x48 || check[8] != 0x63 || check[0x11] != 0x0F || *(int*)(check + 0x18) != Learned)
            throw new InvalidDataException("the learn screen's learned test isn't the code this DLL expects");
        _screen = (int*)GameImage.Rip((nint)check, 2, 7);
        _entries = (ushort*)GameImage.Rip((nint)check + 10, 3, 7);
        State = (byte*)GameImage.Rip(branch + 12, 2, 7);
        var message = branch + 24; // lea rdx, [message 6's text]; mov ecx, 6; call the message box
        Message = GameImage.Rip(message, 3, 7);
        Resume = message + 12;
        // The purchase's yes: the purchase (0x2B9AD0), the list's job, then on to the refresh.
        var buy = image.FindUnique("41 0F BF 0C 5C 0F B7 D7 E8 ?? ?? ?? ?? 0F B7 3D ?? ?? ?? ?? EB 15");
        _job = (ushort*)GameImage.Rip(buy + 13, 3, 7);
        Refresh = buy + 22 + 0x15;
        var purchase = GameImage.Rip(buy + 8, 1, 5);
        _purchase = purchase;
        // The purchase: the marker (0x28936C), the cost (0x2CC0AC), the job index (0x2B8F18), the record's JP.
        var body = image.FindUnique(
            "44 0F B7 C1 0F B7 FA 8B D9 E8 ?? ?? ?? ?? 8B CB E8 ?? ?? ?? ?? 0F B7 CF 44 0F B7 C8 E8 ?? ?? ?? ?? " +
            "48 0F BF 0D ?? ?? ?? ?? 48 8D 15 ?? ?? ?? ?? 4C 0F BF C0 48 8B 14 CA 66 46 29 8C 42 CA 00 00 00");
        if (body - purchase != 10 || (ulong)(branch - buy) > 0x200 && (ulong)(buy - branch) > 0x200)
            throw new InvalidDataException("the learn screen's purchase isn't the code this DLL expects");
        var marker = GameImage.Rip(body + 9, 1, 5);
        Cost = (delegate* unmanaged<int, int>)GameImage.Rip(body + 16, 1, 5);
        _jobIndex = (delegate* unmanaged<int, int>)GameImage.Rip(body + 28, 1, 5);
        _current = (short*)GameImage.Rip(body + 33, 4, 8);
        _records = (nint*)GameImage.Rip(body + 41, 3, 7);
        if (Dismissal.Records != null && Dismissal.Records != _records || JobChange.Records != null && JobChange.Records != _records)
            throw new InvalidDataException("the learn screen reads other menu records than the dismissal or the job change");
        // The marker's slot search: the skillset's abilities by kind (0x275980; 0x2F with record +0x70 & 0x40).
        var slots = image.FindUnique("41 8B C9 44 8A 42 70 41 80 E0 40 41 F6 D8 1B D2 83 E2 10 83 C2 1F E8");
        if ((ulong)(slots - marker) > 0x100)
            throw new InvalidDataException("the learn marker isn't the code this DLL expects");
        _abilities = (delegate* unmanaged<int, int, ushort*>)GameImage.Rip(slots + 22, 1, 5);

        // Patched whatever the option, as a save's own can change it; with no refund, ApplyTo leaves a learned ability
        // to the game's own message.
        var stub = GameImage.AllocateCodeNear(message, StubSize);
        WriteStub(stub, Message, (nint)_job, Resume, Refresh);
        var jump = new byte[12];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.TryWriteBytes(jump.AsSpan(1), checked((int)((nint)stub - (message + 5))));
        GameImage.WriteCode(message, jump);
        BranchAt = message;
        InstallMenu(game);
    }

    // The remaster's own Job menu (what the Deck's game shows; the learn screen above is the older layer's).
    private static ushort* _menuJobs;   // the job list's jobs (u16)
    private static short* _menuJob;     // the cursor's index in it
    private static short* _refreshJob;  // the job whose list the purchase refreshes
    private static int _menuAbility;    // an ability list entry's ability (+0x40)
    private static int _menuBegin;      // the list's entries (+0x2D88; their end follows)
    internal const int EntrySize = 0xB4;
    private static nint _purchase;
    internal const int EntryJp = 0xA0;      // an ability list entry's JP (the job's; -1 once it's learned)
    internal const int EntryCost = 0x9C;    // its price (0 once it's learned)
    /// <summary>The purchase's refreshes (0x285744 with the job, 0x2BD05C) and a row's redraw (0x12F000: the list,
    /// the entry); the offline check swaps them.</summary>
    internal static delegate* unmanaged<int, void> RefreshList;
    internal static delegate* unmanaged<void> RefreshMenu;
    internal static delegate* unmanaged<nint, byte*, void> RedrawRow;
    internal static nint LearnedAt { get; private set; }
    internal static nint NotLearned { get; private set; }
    internal static nint ShowMessage { get; private set; }
    internal static nint Done { get; private set; }

    /// <summary>
    /// The Job menu's ability list (its input in state 9, 0x1312C8) handles A (button 0x1A) itself: with the
    /// selected entry (+0xE58; 0xB4-byte entries from +0x2D88, the ability at +0x40) it picks a message for the
    /// popup (0x152494): 0x1A when +0xB2 is set, 0x19 "This ability has already been learned." when the entry's JP
    /// (+0xA0) is negative (a purchase sets it to -1), 0x18 when its cost (+0x9C) is more than the JP, otherwise it
    /// opens the purchase prompt. A yes there sends event 0x57F, and the Job menu (0x368B4C) buys the ability
    /// (0x2B9AD0, the job from the job list at 0x3C51890, its index at 0x3C51880) and refreshes (0x285744 with the job
    /// at 0x1811428, then 0x2BD05C). Here the JP's test (0x1314BD, 11 bytes) jumps to a stub: a learned entry goes to
    /// <see cref="LearnedA"/>, which unlearns it if it can come off (giving every entry of the list the job's JP back,
    /// redrawing the rows and refreshing as a purchase does, then on to the handler's end), otherwise on to the
    /// message as before. (Found on the Deck, 2026-11-04: the older layer's learn screen never runs there, no event
    /// or menu state marks the press, and the first try took +0xB2 for the learned mark.)
    /// </summary>
    private static void InstallMenu(Game game)
    {
        var image = game.Image;
        var buy = image.FindUnique(
            "48 0F BF 05 ?? ?? ?? ?? 48 8D 35 ?? ?? ?? ?? 8B CF 0F B7 94 46 ?? ?? ?? ?? E8 ?? ?? ?? ?? " +
            "0F BF 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? E9");
        _menuJob = (short*)GameImage.Rip(buy, 4, 8);
        _menuJobs = (ushort*)(image.ImageStart + *(int*)(buy + 21));
        if (GameImage.Rip(buy + 25, 1, 5) != _purchase)
            throw new InvalidDataException("the Job menu's purchase isn't the older screen's purchase");
        _refreshJob = (short*)GameImage.Rip(buy + 30, 3, 7);
        RefreshList = (delegate* unmanaged<int, void>)GameImage.Rip(buy + 37, 1, 5);
        RefreshMenu = (delegate* unmanaged<void>)GameImage.Rip(buy + 42, 1, 5);
        var list = image.FindUnique(
            "48 8B 4B 78 48 85 C9 74 ?? 48 63 81 ?? ?? ?? ?? 85 C0 78 ?? 4C 8B 89 ?? ?? ?? ?? 4C 8B C0 48 8B 89 " +
            "?? ?? ?? ?? 48 B8 83 2D D8 82 2D D8 82 2D 49 2B C9 48 F7 E9 48 C1 FA 05 48 8B C2 48 C1 E8 3F 48 03 D0 " +
            "49 3B D0 0F 86 ?? ?? ?? ?? 49 69 C0 ?? ?? ?? ?? 46 8B 74 08 ??");
        _menuAbility = ((byte*)list)[87];
        _menuBegin = *(int*)(list + 23);
        if (*(int*)(list + 33) != _menuBegin + 8 || *(int*)(list + 79) != EntrySize)
            throw new InvalidDataException("the ability list's entries aren't laid out as this DLL expects");
        // The purchase's mark on its entry, then the row's redraw: or [rdx+0xA0], -1; call 0x12F000.
        RedrawRow = (delegate* unmanaged<nint, byte*, void>)GameImage.Rip(image.FindUnique("83 8A A0 00 00 00 FF E8") + 7, 1, 5);
        // The A press: imul rax, rbp, 0xB4; cmp byte [rax+rdi+0xB2], 0; jne …; mov ecx, [rax+rdi+0xA0]; test ecx, ecx;
        // jns the cost test; mov esi, 0x19 (already learned); jmp the message; cmp [rax+rdi+0x9C], ecx …
        var a = image.FindUnique(
            "48 69 C5 B4 00 00 00 80 BC 38 B2 00 00 00 00 75 20 8B 8C 38 A0 00 00 00 85 C9 79 07 BE 19 00 00 00 EB 0E " +
            "39 8C 38 9C 00 00 00");
        LearnedAt = a + 0x11;
        ShowMessage = LearnedAt + 11;  // mov esi, 0x19; jmp the message
        NotLearned = LearnedAt + 18;   // the cost test, with the JP in ecx
        Done = a + 7 + 0x6A;           // the handler's end
        ReadOnlySpan<byte> end = [0x48, 0x8B, 0x5C, 0x24, 0x60];
        if (!new ReadOnlySpan<byte>((byte*)Done, 5).SequenceEqual(end))
            throw new InvalidDataException("the ability list's A press isn't laid out as this DLL expects");
        var stub = GameImage.AllocateCodeNear(LearnedAt, StubSize);
        WriteLearnedStub(stub, NotLearned, ShowMessage, Done);
        var jump = new byte[11];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.TryWriteBytes(jump.AsSpan(1), checked((int)((nint)stub - (LearnedAt + 5))));
        GameImage.WriteCode(LearnedAt, jump);
        game.Log.Info($"unlearn: the Job menu's ability list, A on a learned entry at 0x{LearnedAt - image.ImageStart:X}");
    }

    /// <summary>
    /// The JP test's stub, entered by a jump from the handler's body (16-aligned) with the entry at rax + rdi and the
    /// list in rbx: an entry with JP (ecx, as the moved code leaves it) goes on to <paramref name="notLearned"/>; for a
    /// learned one (-1) <see cref="LearnedA"/> decides: unlearned, on to <paramref name="done"/>, otherwise
    /// <paramref name="message"/>.
    /// </summary>
    internal static void WriteLearnedStub(byte* stub, nint notLearned, nint message, nint done)
    {
        var code = new List<byte>();
        code.AddRange([0x8B, 0x8C, 0x38, 0xA0, 0x00, 0x00, 0x00]);            // mov ecx, [rax+rdi+0xA0] (the moved code)
        code.AddRange([0x85, 0xC9]);                                           // test ecx, ecx
        code.AddRange([0x78, 0x0E]);                                           // js: learned
        AbsoluteJump(code, notLearned);                                        // (14 bytes)
        code.AddRange([0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53]); // push rax, rcx, rdx, r8–r11
        code.AddRange([0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00]);            // sub rsp, 0x88
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x48, 0x8D, 0x0C, 0x38]);                               // lea rcx, [rax+rdi]: the entry
        code.AddRange([0x48, 0x89, 0xDA]);                                     // mov rdx, rbx: the list
        code.AddRange([0x48, 0xB8]);                                           // mov rax, LearnedA
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<byte*, nint, int>)&LearnedA));
        code.AddRange([0xFF, 0xD0]);                                           // call rax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);            // add rsp, 0x88
        code.AddRange([0x85, 0xC0]);                                           // test eax, eax
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax (flags kept)
        code.AddRange([0x75, 0x0E]);                                           // jnz: unlearned
        AbsoluteJump(code, message);
        AbsoluteJump(code, done);
        if (code.Count > StubSize)
            throw new InvalidOperationException("the learned stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    [UnmanagedCallersOnly]
    private static int LearnedA(byte* entry, nint list)
    {
        try
        {
            var ability = *(int*)(entry + _menuAbility);
            var job = (uint)*_menuJob < 0x40 ? _menuJobs[*_menuJob] : -1;
            if (job < 0)
                return Refused($"0x{ability:X3}: the menu's job index {*_menuJob} is out of range") ? 1 : 0;
            if (!ApplyTo(ability, job))
                return 0;
            // Every entry still to buy shows the job's JP, and this one is buyable again: all get the new JP, then
            // the rows are redrawn (before the refreshes, which may rebuild the entries).
            var jp = JpOf(job);
            *(int*)(entry + EntryCost) = Cost(ability); // the price shows again (a learned entry's is 0)
            var begin = *(byte**)(list + _menuBegin);
            var count = (int)((*(byte**)(list + _menuBegin + 8) - begin) / EntrySize);
            for (var k = 0; k < count; k++)
            {
                var other = begin + k * EntrySize;
                if (other == entry || *(int*)(other + EntryJp) >= 0)
                {
                    *(int*)(other + EntryJp) = jp;
                    RedrawRow(list, other);
                }
            }
            RefreshList(*_refreshJob);
            RefreshMenu();
            return 1;
        }
        catch (Exception e)
        {
            Failed(e);
            return 0;
        }
    }

    /// <summary>The menu unit's JP in <paramref name="job"/>.</summary>
    private static int JpOf(int job)
    {
        var record = (byte*)_records[*_current];
        return *(ushort*)(record + Jp + 2 * _jobIndex(job));
    }

    internal static int MenuBegin => _menuBegin;

    /// <summary>The menu job list's job under the cursor, for the offline check.</summary>
    internal static (nint Jobs, nint Index, nint RefreshJob) MenuJob => ((nint)_menuJobs, (nint)_menuJob, (nint)_refreshJob);

    internal static int CurrentRecord => _current != null ? *_current : -1;

    /// <summary>
    /// The refused entry's stub, entered by a jump from the handler's body (16-aligned) with the entry's index in ebx:
    /// <see cref="Choose"/>, then on to <paramref name="refresh"/> with edi the list's job after an unlearning, or the
    /// game's message (rdx <paramref name="message"/>, ecx 6) and on to <paramref name="resume"/>, its call. Only rdi
    /// (which the refresh reloads), or rdx and ecx (which the message sets anyway), change.
    /// </summary>
    internal static void WriteStub(byte* stub, nint message, nint job, nint resume, nint refresh)
    {
        var code = new List<byte>
        {
            0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, // push rax, rcx, rdx, r8–r11
            0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00,                         // sub rsp, 0x88
        };
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x8B, 0xCB]);                                           // mov ecx, ebx: the entry
        code.AddRange([0x48, 0xB8]);                                           // mov rax, Choose
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<int, int>)&Choose));
        code.AddRange([0xFF, 0xD0]);                                           // call rax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);            // add rsp, 0x88
        code.AddRange([0x85, 0xC0]);                                           // test eax, eax
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax (flags kept)
        code.AddRange([0x75, 0x00]);                                           // jnz: unlearned (set below)
        var jnz = code.Count;
        code.AddRange([0x48, 0xBA]);                                           // mov rdx, message
        code.AddRange(BitConverter.GetBytes((ulong)message));
        code.AddRange([0xB9, 0x06, 0x00, 0x00, 0x00]);                         // mov ecx, 6
        AbsoluteJump(code, resume);
        code[jnz - 1] = (byte)(code.Count - jnz);
        code.AddRange([0x48, 0xBF]);                                           // mov rdi, job
        code.AddRange(BitConverter.GetBytes((ulong)job));
        code.AddRange([0x0F, 0xB7, 0x3F]);                                     // movzx edi, word [rdi]
        AbsoluteJump(code, refresh);
        if (code.Count > StubSize)
            throw new InvalidOperationException("the unlearn stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    private static void AbsoluteJump(List<byte> code, nint target)
    {
        code.AddRange([0xFF, 0x25, 0x00, 0x00, 0x00, 0x00]);
        code.AddRange(BitConverter.GetBytes((ulong)target));
    }

    [UnmanagedCallersOnly]
    private static int Choose(int entry)
    {
        try
        {
            return Apply(entry) ? 1 : 0;
        }
        catch (Exception e)
        {
            Failed(e);
            return 0;
        }
    }

    /// <summary>ReMixed's unlearning for the chosen entry of the learn list. True when it came off.</summary>
    internal static bool Apply(int entry)
    {
        if ((uint)entry >= 0x400)
            return Refused($"entry {entry}");
        // Whether it's learned comes from the unit's learned bits: the learn screen's entries carry only "can't be
        // bought" (+0x4000) for a learned ability, the same as for one the unit can't afford (seen in the game).
        var value = _entries[entry];
        return ApplyTo(value & 0x3FF, *_job);
    }

    /// <summary>Unlearns <paramref name="ability"/> of <paramref name="job"/> for the menu's unit, if it can come off.</summary>
    internal static bool ApplyTo(int ability, int job)
    {
        if (Options.Current.JpRefund == Options.JpRefundRule.None) // a save's own may be vanilla's: the game's message
            return false;
        var index = *_current;
        if ((uint)index >= 0x37 || (uint)job >= UnitTables.JobCount || (uint)ability >= 0x200)
            return Refused($"0x{ability:X3}: record {index}, job 0x{job:X2} out of range");
        var record = (byte*)_records[index];
        if (record == null)
            return Refused($"0x{ability:X3}: record {index} is empty");
        var slot = SlotOf(record, job, ability);
        var jobIndex = _jobIndex(job);
        if (slot < 0 || (uint)jobIndex >= 24)
            return Refused($"0x{ability:X3}: job 0x{job:X2} (index {jobIndex}, skillset "
                           + $"0x{_game.Units.Jobs[job * UnitTables.JobSize]:X2}) has it in slot {slot}");
        var bits = record + LearnedBits + 3 * jobIndex + slot / 8;
        var bit = (byte)(0x80 >> (slot & 7));
        if ((*bits & bit) == 0)
            return Refused($"0x{ability:X3} isn't learned: the game's message, as before");
        if (Equipped(record, ability))
            return Refused($"0x{ability:X3} is equipped (reaction 0x{*(ushort*)(record + Reaction):X3}, support "
                           + $"0x{*(ushort*)(record + Support):X3}, movement 0x{*(ushort*)(record + Movement):X3})");
        var cost = Cost(ability);
        if (cost == 0xFFFF)
            return Refused($"0x{ability:X3} has no price");
        var refund = cost == MaxJp ? 0 : Options.Current.JpRefund == Options.JpRefundRule.Half ? cost >> 1 : cost;
        *bits &= (byte)~bit;
        var jp = (ushort*)(record + Jp + 2 * jobIndex);
        *jp = (ushort)Math.Min(MaxJp, *jp + refund);
        Tracer.Line(_game.Log, $"unlearn: record {index}, job 0x{job:X2}: 0x{ability:X3} unlearned (slot {slot}), {refund} JP back");
        return true;
    }

    /// <summary>Why a learned entry stays learned, in the log (for the in-game test).</summary>
    private static bool Refused(string why)
    {
        Tracer.Line(_game.Log, $"unlearn: stays learned: {why}");
        return false;
    }

    /// <summary>The equipped reaction, support or movement, and Two Spells' Equip Shield and Equip Sword, stay learned.</summary>
    internal static bool Equipped(byte* record, int ability)
    {
        if (*(ushort*)(record + Reaction) == ability || *(ushort*)(record + Support) == ability
            || *(ushort*)(record + Movement) == ability)
            return true;
        return *(ushort*)(record + Support) == TwoSpells && ability is 0x1C7 or 0x1C8;
    }

    /// <summary>The ability's slot in the job's skillset, found as the game's marker finds it (0x28936C).</summary>
    internal static int SlotOf(byte* record, int job, int ability)
    {
        var skillset = _game.Units.Jobs[job * UnitTables.JobSize];
        var ids = _abilities(skillset, (record[Flags] & 0x40) != 0 ? 0x2F : 0x1F);
        if (ids == null)
            return -1;
        for (var k = 0; k < Slots; k++)
            if (ids[k] == ability)
                return k;
        return -1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"unlearn: failed: {e}");
    }
}
