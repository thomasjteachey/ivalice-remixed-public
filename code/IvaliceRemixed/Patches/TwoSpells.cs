using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Two Spells (docs/MECHANICS.md: "Two Spells"; support 0x1D0, the Calculator's innate): the action menu's
/// extra White and Black Magic commands, the spell limit in those lists, the AI's extra lists, a first spell that
/// keeps the action, and the second charging slot. Still to come, in the battle menus: Cancel Casting, the charging
/// notice and the turn order's second entry. Installed by <see cref="Supports"/> when the build lists 0x1D0, which it
/// holds back until then: 0x1D0 stays vanilla's Gained EXP Up.
/// </summary>
/// <remarks>
/// <para>The command list (PS1 0x801813C0; the remaster's 0x30E010, from the battle UI) is Attack, the primary and
/// secondary skillsets, Defend and Equip Change, at most six, each with its skillset type and whether it's usable.
/// ReMixed's 0x8015A140 (from 0x801814D0) adds White Magic (0x0A) and Black Magic (0x0B) after the secondary for a
/// unit with 0x1D0 that doesn't already have them. The hook keeps the game's list and inserts them, then works out
/// the types and usable flags again for the whole list, as the game's loop does.</para>
/// <para>An extra list only offers spells with MP 1–15 and CT 1–5 as the data has them (PS1 0x8015E1D0, the menu's
/// filter; 0x8015F020, the AI's). A list is an extra one when it's White or Black Magic and neither the unit's
/// primary nor its secondary. The limit sits in <see cref="WeaponRequirements"/>' stubs, which already run for each
/// ability of the menu's list builder (0x30E368) and of the AI's per-skillset candidates (0x316D24); a failed
/// check leaves the ability out. Math Skill lists the spells it calculates with through the same builder
/// (0x30E94C, 0x30EA38): those aren't limited, so hooks on both mark when they run, as ReMixed checks the opened
/// command (0x8016982D) for 0x15.</para>
/// <para>The AI (0x8015EFA0, from 0x8019A490) adds White and then Black Magic after the secondary. Here a hook on the
/// per-skillset candidates (0x316D24, which 0x316B04 calls for the primary and then the secondary unless the
/// primary is a monster's) asks for them after the secondary's. ReMixed's version compares the secondary with the
/// primary where it means the extra skillset, so a unit whose secondary is White Magic gets that list twice; this
/// one skips the unit's own lists. The list holds 34 entries and the game puts its wait entry last: whatever an
/// extra list would put past the 33rd is put back as it was.</para>
/// <para>The turn (PS1 0x8015E3F0 at the queue's commit): a first qualifying spell (below 0x170, CT and MP in its data,
/// not Persevere) doesn't count as the unit's action. The remaster marks the action before it queues it, as the
/// PS1 does (0x310548 from the player's 0x206CE8, then the queue 0x281434 with the battle UI's record at +0x178);
/// ReMixed lets the turn end there and opens it again at the commit, while here the call at 0x206D48 goes through
/// <see cref="Acts"/>, which leaves the action out for such a spell, and the commit's "acted" is cleared after it.
/// Between the two only qualifying abilities show, and Attack, Defend and Equip Change go. The pair ends with the
/// turn (the end of turn, hooked by <see cref="Movement"/>). Marking the second can end the turn itself, before the
/// queue (0x310548 ends it once the unit has moved, which a Charge's commit always does): that end of turn keeps
/// the second's mark for the queue.</para>
/// <para>The second slot (PS1 0x8015E900, 0x80156300, 0x8015EA00, 0x8015EEC8, 0x8015F400): at the second spell's
/// commit the one that finishes first keeps the unit's record (+0x1A0, the CT at +0x18D) and the other goes to the
/// slot, kept here. It ticks with the unit's charge at the end of the turn loop's countdown (0x30F33A); once the
/// first has fired (+0x18D is 0xFF) the slot takes the record and the CT, and if the unit isn't Charging any more (a
/// status cancelled it) the slot goes too. The action's start resets the unit's action state (0x279370 with 0xFF at
/// 0x28292A); with a full slot that's skipped, so the unit keeps Charging for its second spell. A unit that queues
/// any other action loses its slot, as its pending charge is replaced. ReMixed copies the slot back as soon as the
/// first action's results are done; here it's the next countdown, so two spells that finish on the same tick land
/// a tick apart. The AI never takes a second action: each qualifying spell an AI unit with 0x1D0 casts also goes
/// into the slot, so it lands twice; with Non-charge there's no charge to copy, so the action setup (0x282234, PS1
/// 0x8015DC40 at 0x8017D4B8) gives the spell two strikes instead. Nothing runs while the game simulates (the AI's
/// planning, the previews).</para>
/// </remarks>
internal static unsafe class TwoSpells
{
    public const int Id = 0x1D0, WhiteMagic = 0x0A, BlackMagic = 0x0B, MathSkill = 0x15;
    private const int Support = Battle.UnitSupports + 1, SupportBit = 0x20; // +0x99 & 0x20
    private const int MaxCommands = 6;
    private const int AiEntries = 0x22, AiEntrySize = 4, AiSaved = 32; // a skillset adds at most 16

    private static Game _game = null!;
    private static delegate* unmanaged<int, byte*, int> _commands;   // the original builder
    /// <summary>The game's usable test, (battle index, skillset, type): non-zero if usable (0x30E174). The offline check swaps it.</summary>
    internal static delegate* unmanaged<int, int, int, int> Usable;
    private static byte* _types, _unusable, _typeTable;
    /// <summary>The game's candidates per skillset (0x316D24's original). The offline check swaps it.</summary>
    internal static delegate* unmanaged<int, int, int, int> GameCandidates;
    private static delegate* unmanaged<nint, nint, nint, nint, nint> _mathTerms;
    /// <summary>Math Skill's spell list (0x30EA38's original). The offline check swaps it.</summary>
    internal static delegate* unmanaged<nint, nint, nint, nint, nint> GameMathSpells;
    private static byte* _aiLists, _aiSlots;
    private static int _inMath;
    private static int _failed;

    // The turn and the second slot, a battle unit each.
    private const int RecordSize = 20, UnitRecord = 0x1A0, UnitCharge = 0x18D, UnitActed = 0x1BA, Empty = 0xFF;
    private const int Charging = 0x08, Performing = 0x01; // +0x61
    private const int UiRecord = 0x178;                   // the battle UI's action record
    private const int ChargeType = 0x0A;                  // the Charge skillset's type: Two Shots stays usable between
    private const byte PendingFirst = 1, PendingSecond = 2;
    private static readonly byte[] Pair = new byte[Battle.UnitCount];      // 1: the first spell is cast this turn
    private static readonly byte[] Pending = new byte[Battle.UnitCount];   // what the action being queued is
    private static readonly byte[] SlotCt = Enumerable.Repeat((byte)Empty, Battle.UnitCount).ToArray();
    private static readonly byte[] SlotRecords = new byte[Battle.UnitCount * RecordSize];
    private static readonly byte[] FirstRecord = new byte[RecordSize];
    private static byte _firstCt = Empty;
    private static int _markingSecond = -1;                                // the unit whose second action 0x310548 marks
    private static int* _dryRun;
    /// <summary>0x310548 (battle index, moved, acted): marks the turn's move and action, then the turn-over check. The offline check swaps it.</summary>
    internal static delegate* unmanaged<int, int, int, int> GameSetActed;
    /// <summary>0x279370 (unit, state): the unit's action state. The offline check swaps it.</summary>
    internal static delegate* unmanaged<byte*, int, nint> GameActionState;
    /// <summary>0x30FB9C (unit): non-zero low bits when the unit's clock doesn't run. The offline check swaps it.</summary>
    internal static delegate* unmanaged<byte*, int> GameStopped;
    /// <summary>0x282234 (unit): the action's setup, which starts the strike count at 1 (the original).</summary>
    private static delegate* unmanaged<byte*, void> _actionSetup;
    private static byte* _strikes;

    /// <summary>The spell limit applies (the lists' part is installed).</summary>
    internal static bool Limits { get; private set; }
    /// <summary>The turn and the second slot are installed.</summary>
    internal static bool Turns { get; private set; }
    internal static nint ActsAt { get; private set; }
    internal static nint StartAt { get; private set; }
    internal static nint TickAt { get; private set; }
    internal static nint SetupAt { get; private set; }
    internal static byte* Strikes => _strikes;
    internal static byte PairOf(int index) => Pair[index];
    internal static int SlotCtOf(int index) => SlotCt[index];
    internal static Span<byte> SlotRecordOf(int index) => SlotRecords.AsSpan(index * RecordSize, RecordSize);

    // For the offline check.
    internal static nint Builder { get; private set; }
    internal static nint CandidatesAt { get; private set; }
    internal static nint MathSpellsAt { get; private set; }
    internal static byte* Types => _types;
    internal static byte* Unusable => _unusable;
    internal static byte* TypeTable => _typeTable;
    internal static byte* AiLists => _aiLists;
    internal static byte* AiSlots => _aiSlots;

    /// <summary>The command list gets White and Black Magic for 0x1D0 (the hook can be in for Dragon Tech alone).</summary>
    internal static bool Extras { get; private set; }

    /// <summary>Installs what's ported so far. Returns what it enabled.</summary>
    public static List<string> Install(Game game)
    {
        var enabled = new List<string>();
        if (Builder == 0)
            InstallCommands(game);
        Extras = true;
        enabled.Add("0x1D0 Two Spells: extra White and Black Magic commands");
        if (!Limits)
            InstallLists(game);
        enabled.Add("0x1D0 Two Spells: the extra lists' spell limit, the AI's extra lists");
        if (!Turns)
            InstallTurn(game);
        enabled.Add("0x1D0 Two Spells: a first spell keeps the action, the second charging slot");
        return enabled;
    }

    public static void InstallCommands(Game game)
    {
        _game = game;
        var image = game.Image;
        // The builder: the unit's primary (+0x12) and secondary (+0x13), each tested for a monster skillset (0xB0-0xDF).
        var body = image.FindUnique("0F B6 48 12 49 8B F0 44 8A 50 13 8D 41 50 3C 2F 0F 86 ?? ?? ?? ?? 41 8D 42 50 3C 2F 0F 86");
        var start = body - 0x33;
        GameImage.Calls(start, "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 56 48 83 EC 20 4C 8B C2 8B E9 E8",
                        "the command list's start");
        // ... its loop: the types into one array, the usable test, "unusable" into another.
        var loop = image.FindUnique("4C 8D 35 ?? ?? ?? ?? 49 2B F6 48 8D 1D ?? ?? ?? ?? 4A 8D 0C 37 0F B6 14 0E 80 FA FF");
        if ((ulong)(loop - start) > 0x100)
            throw new InvalidDataException("the command list's loop isn't in it");
        _types = (byte*)GameImage.Rip(loop, 3, 7);
        GameImage.Calls(loop + 0x1E, "44 8A 84 1A ?? ?? ?? ?? 41 80 E0 0F 44 88 01 8B CD E8 ?? ?? ?? ?? 85 C0 0F 94 C0 88 84 1F",
                        "the command list's types and usable test");
        _typeTable = (byte*)(image.ImageStart + *(int*)(loop + 0x1E + 4));
        Usable = (delegate* unmanaged<int, int, int, int>)GameImage.Rip(loop + 0x2F, 1, 5);
        _unusable = (byte*)(image.ImageStart + *(int*)(loop + 0x1E + 0x1B + 3));
        if (SkillsetTypes.Table != null && SkillsetTypes.Table != _typeTable)
            throw new InvalidDataException("the command list reads another skillset type table");
        _commands = (delegate* unmanaged<int, byte*, int>)game.Hooks.Hook(
            start, (nint)(delegate* unmanaged<int, byte*, int>)&Commands, "the action menu's command list");
        Builder = start;
    }

    /// <summary>The limit's two halves: Math Skill's lists marked, and the AI's extra lists.</summary>
    public static void InstallLists(Game game)
    {
        _game = game;
        var image = game.Image;
        // Math Skill's two lists: its terms (CT, Level, ...; 0x30E94C) and the spells it calculates with, from
        // White, Black, Time and Yin-Yang Magic (0x30EA38, the four ids in one dword). Both start by testing for 0x15.
        var terms = image.FindUnique(
            "40 53 56 57 48 81 EC D0 00 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 C8 00 00 00 49 8B F8 41 8A F1 " +
            "44 8A C2 44 8B D1 33 DB E8 ?? ?? ?? ?? 48 85 C0 74 0D B2 15");
        var spells = image.FindUnique(
            "48 89 5C 24 10 48 89 6C 24 20 56 57 41 54 41 56 41 57 48 81 EC E0 00 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 " +
            "48 89 84 24 D0 00 00 00 49 8B D8 C7 44 24 40 0A 0B 0C 10");
        // The AI's candidates per skillset: (battle index, skillset, entries so far) → entries; 0x22 entries a list
        // at lists + 0x88 × the unit's list number (slots + index). The weapon stub's site is inside it.
        var candidates = image.FindUnique(
            "48 89 5C 24 08 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 40 45 33 F6 49 63 F8 48 63 F2 48 63 D9 85 D2");
        var slotRead = image.FindUnique("46 0F B6 B4 3D ?? ?? ?? ?? 42 8A 84 3F ?? ?? ?? ?? 3C B0 72 08 3C E0");
        var listWrite = image.FindUnique("66 42 89 84 39 ?? ?? ?? ?? 8B CE 48 89 33 80 4B 02 02");
        _aiSlots = (byte*)(image.ImageStart + *(int*)(slotRead + 5));
        _aiLists = (byte*)(image.ImageStart + *(int*)(listWrite + 5));
        if (_aiSlots - _aiLists != 20 * AiEntries * AiEntrySize)
            throw new InvalidDataException("the AI's candidate lists aren't where this DLL expects");

        _mathTerms = (delegate* unmanaged<nint, nint, nint, nint, nint>)game.Hooks.Hook(
            terms, (nint)(delegate* unmanaged<nint, nint, nint, nint, nint>)&MathTerms, "Math Skill's terms");
        GameMathSpells = (delegate* unmanaged<nint, nint, nint, nint, nint>)game.Hooks.Hook(
            spells, (nint)(delegate* unmanaged<nint, nint, nint, nint, nint>)&MathSpells, "Math Skill's spells");
        GameCandidates = (delegate* unmanaged<int, int, int, int>)game.Hooks.Hook(
            candidates, (nint)(delegate* unmanaged<int, int, int, int>)&Candidates, "the AI's candidates per skillset");
        CandidatesAt = candidates;
        MathSpellsAt = spells;
        Limits = true;
    }

    /// <summary>The turn and the second slot: the player's action, the action's start, the countdown, the queue, the end of turn.</summary>
    public static void InstallTurn(Game game)
    {
        _game = game;
        var image = game.Image;
        // The player's action (0x206CE8): the turn's action marked (0x310548 with acted 1), then the action queued from
        // the battle UI's record (rbx + 0x178).
        var acts = image.FindUnique(
            "48 8B 83 48 01 00 00 33 D2 0F B6 88 BC 01 00 00 44 8D 42 01 E8 ?? ?? ?? ?? 80 8B 53 01 00 00 02") + 20;
        var setActed = GameImage.Rip(acts, 1, 5);
        GameImage.Calls(setActed, "40 53 48 83 EC 20 48 63 D9 48 8D 05 ?? ?? ?? ?? 48 8B CB 48 C1 E1 09 48 03 C8 80 79 01 FF 75 09",
                        "the turn's move and action");
        // The action's start (0x2828E4): the action state reset unless the unit is Performing (0x279370 with 0xFF).
        var start = image.FindUnique("75 0A B2 FF 48 8B CB E8 ?? ?? ?? ?? 48 8B CB E8") + 7;
        var actionState = GameImage.Rip(start, 1, 5);
        GameImage.Calls(actionState, "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 20 44 8A 71 61 32 DB",
                        "the action state");
        // The turn loop's countdown (state 2): a charge in 1..0xFE ticks for a unit whose clock runs; then state 3.
        var countdown = image.FindUnique(
            "4C 8D 15 ?? ?? ?? ?? 49 8D 8A 73 FE FF FF E8 ?? ?? ?? ?? A8 0F 75 12 41 8A 0A 8A C1 41 2A C3 3C FD 77 06 " +
            "41 2A CB 41 88 0A 4D 03 D7 48 8D 05 ?? ?? ?? ?? 4C 3B D0 7C CF C7 05 ?? ?? ?? ?? 03 00 00 00 E9");
        var stopped = GameImage.Rip(countdown + 14, 1, 5);
        var tick = countdown + 56;
        var state = GameImage.Rip(tick, 2, 10);
        // The action's setup: the strike count (and the strike) to 1, 0, then the unit's skillset and its type.
        var setup = image.FindUnique(
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 20 33 F6 66 C7 05 ?? ?? ?? ?? 01 00 39 35");
        _strikes = (byte*)GameImage.Rip(setup + 0x1A, 3, 9);
        if (_strikes != Knockback.Strikes && Knockback.Strikes != null)
            throw new InvalidDataException("the action setup and the knockback disagree on the strike count");
        // The queue's commit leaves "acted" alone during a dry run ([0x186AF9C]).
        var dryRun = image.FindUnique("44 84 F3 74 2B 39 3D ?? ?? ?? ?? 75 23 88 9E BA 01 00 00");
        if ((ulong)(dryRun - (nint)game.RuleCode.QueueAction) > 0x600)
            throw new InvalidDataException("the dry-run test isn't in the action queue");
        _dryRun = (int*)GameImage.Rip(dryRun + 5, 2, 6);

        GameSetActed = (delegate* unmanaged<int, int, int, int>)setActed;
        GameActionState = (delegate* unmanaged<byte*, int, nint>)actionState;
        GameStopped = (delegate* unmanaged<byte*, int>)stopped;
        Rules.HookQueue(game);
        Movement.HookEndOfTurn(game);
        BattleEvents.OnStart(game, "Two Spells' pairs and slots", ResetAll);
        _actionSetup = (delegate* unmanaged<byte*, void>)game.Hooks.Hook(
            setup, (nint)(delegate* unmanaged<byte*, void>)&ActionSetup, "the action's setup");
        SetupAt = setup;

        // The two calls: mov r9, rbx (the battle UI) for the first; then a jump to the C#, which returns to the game.
        var actsStub = GameImage.AllocateCodeNear(acts, 32);
        var code = new List<byte> { 0x49, 0x89, 0xD9 };
        AbsoluteJump(code, (nint)(delegate* unmanaged<int, int, int, byte*, int>)&PlayerActs);
        code.ToArray().CopyTo(new Span<byte>(actsStub, 32));
        WriteCall(acts, (nint)actsStub);
        var startStub = GameImage.AllocateCodeNear(start, 32);
        code = [];
        AbsoluteJump(code, (nint)(delegate* unmanaged<byte*, int, nint>)&ActionStart);
        code.ToArray().CopyTo(new Span<byte>(startStub, 32));
        WriteCall(start, (nint)startStub);
        // The countdown's end: Tick with every scratch register kept (entered by a jump from the loop's 16-aligned
        // body: 7 pushes and 0x88 keep the call aligned), the game's mov from here, and back after it.
        const int tickSize = 192;
        var tickStub = GameImage.AllocateCodeNear(tick, tickSize);
        code = [0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, 0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00];
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x48, 0xB8]);
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<void>)&TickEntry));
        code.AddRange([0xFF, 0xD0]);
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00, 0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]);
        var movAt = code.Count;
        code.AddRange([0xC7, 0x05, 0, 0, 0, 0, 0x03, 0x00, 0x00, 0x00]);  // mov dword [state], 3
        var jmpAt = code.Count;
        code.AddRange([0xE9, 0, 0, 0, 0]);                                // jmp back, after the game's mov
        var bytes = code.ToArray();
        if (bytes.Length > tickSize)
            throw new InvalidOperationException("the countdown stub is too big");
        BitConverter.TryWriteBytes(bytes.AsSpan(movAt + 2), checked((int)(state - ((nint)tickStub + movAt + 10))));
        BitConverter.TryWriteBytes(bytes.AsSpan(jmpAt + 1), checked((int)(tick + 10 - ((nint)tickStub + jmpAt + 5))));
        bytes.CopyTo(new Span<byte>(tickStub, tickSize));
        var jump = new byte[10];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.TryWriteBytes(jump.AsSpan(1), checked((int)((nint)tickStub - (tick + 5))));
        GameImage.WriteCode(tick, jump);
        ActsAt = acts;
        StartAt = start;
        TickAt = tick;
        InstallHighIds(game);
        Turns = true;
    }

    /// <summary>
    /// Between the two, ReMixed's lists hide every id from 0x170 but Two Shots (PS1 0x8015D9F8, from 0x801819D0). The
    /// menu's list builder (0x30E368, which the Charge list 0x30EFE4 calls too) sends those ids to their own branch
    /// (its ja at 0x30E4C9 to 0x30E5C8, which <see cref="GunSkills"/> rewrites further on); the ja goes to a stub that
    /// asks <see cref="ShowsHigh"/> and skips the entry (0x30E63C, the loop's next slot) or goes on as before.
    /// </summary>
    private static void InstallHighIds(Game game)
    {
        // lea eax, [rdi-1]; mov ecx, 0x16E; cmp ax, cx; ja the high ids; then the ability's live record.
        var at = game.Image.FindUnique("8D 47 FF B9 6E 01 00 00 66 3B C1 0F 87 ?? ?? ?? ?? 8B CF E8");
        var ja = at + 11;
        var high = ja + 6 + *(int*)(ja + 2);
        // The loop's "next slot" for an entry left out: mov ebx, 1; mov edi, [rsp+0x24]; add r14d, ebx.
        var next = game.Image.FindUnique("BB 01 00 00 00 8B 7C 24 24 44 03 F3 41 83 FE 10 0F 8C");
        if ((ulong)(next - ja) > 0x200 || (ulong)(high - ja) > 0x200)
            throw new InvalidDataException("the list builder's high ids aren't the code this DLL expects");
        var stub = GameImage.AllocateCodeNear(ja, HighIdStubSize);
        WriteHighIdStub(stub, high, next);
        var jump = new byte[6];
        jump[0] = 0x0F;
        jump[1] = 0x87;
        BitConverter.TryWriteBytes(jump.AsSpan(2), checked((int)((nint)stub - (ja + 6))));
        GameImage.WriteCode(ja, jump);
        HighIdsAt = ja;
        HighIdsStub = (nint)stub;
    }

    internal const int HighIdStubSize = 192;

    /// <summary>The stub: <see cref="ShowsHigh"/>(edi, the function's [rsp+0x28]), then on to <paramref name="high"/> or <paramref name="next"/>.</summary>
    internal static void WriteHighIdStub(byte* stub, nint high, nint next)
    {
        var code = new List<byte> { 0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, 0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00 };
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x8B, 0xCF]);                                 // mov ecx, edi: the ability
        code.AddRange([0x8B, 0x94, 0x24, 0xE8, 0x00, 0x00, 0x00]);   // mov edx, [rsp+0xE8]: the function's [rsp+0x28], the battle index
        code.AddRange([0x48, 0xB8]);
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<int, int, int>)&ShowsHigh));
        code.AddRange([0xFF, 0xD0]);                                 // call rax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);  // add rsp, 0x88
        code.AddRange([0x85, 0xC0]);                                 // test eax, eax
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax
        code.AddRange([0x74, 0x0E]);                                 // jz: left out
        AbsoluteJump(code, high);
        AbsoluteJump(code, next);
        if (code.Count > HighIdStubSize)
            throw new InvalidOperationException("the high-id stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>For the offline check: a unit's pair state.</summary>
    internal static void SetPair(int index, byte value) => Pair[index] = value;

    internal static nint HighIdsAt { get; private set; }
    internal static nint HighIdsStub { get; private set; }

    /// <summary>An id from 0x170 in a unit's list: hidden between its two, unless it's Two Shots.</summary>
    [UnmanagedCallersOnly]
    private static int ShowsHigh(int ability, int index) => HidesHigh(ability, index) ? 0 : 1;

    internal static bool HidesHigh(int ability, int index) =>
        Turns && (uint)index < Battle.UnitCount && Pair[index] != 0 && (ushort)ability != GunSkills.TwoShots;

    internal static void AbsoluteJump(List<byte> code, nint target)
    {
        code.AddRange([0xFF, 0x25, 0x00, 0x00, 0x00, 0x00]);
        code.AddRange(BitConverter.GetBytes((ulong)target));
    }

    internal static void WriteCall(nint at, nint target)
    {
        var call = new byte[5];
        call[0] = 0xE8;
        BitConverter.TryWriteBytes(call.AsSpan(1), checked((int)(target - (at + 5))));
        GameImage.WriteCode(at, call);
    }

    private static bool Active => *_game.Routines.Simulating == 0;

    private static int IndexOf(byte* unit)
    {
        var offset = unit - _game.TurnCode.Units;
        return offset >= 0 && offset < Battle.UnitCount * Battle.UnitSize && offset % Battle.UnitSize == 0
            ? (int)(offset / Battle.UnitSize) : -1;
    }

    /// <summary>
    /// A Two Spells ability (PS1 0x80156300's test): below 0x170, with CT (its low 7 bits) and MP in its data, and not
    /// Persevere (+5 &amp; 4).
    /// </summary>
    internal static bool Qualifies(int ability)
    {
        if (ability == GunSkills.TwoShots)
            return true; // a gun skill: ReMixed's pair takes it as a qualifying action
        if (ability is <= 0 or >= 0x170)
            return false;
        var record = _game.RuleCode.GetAbilityAction(ability);
        return record != null && (record[0x0C] & 0x7F) > 0 && record[0x0D] > 0 && (record[5] & 4) == 0;
    }

    /// <summary>
    /// Whether an action takes the pair's path (PS1 0x80156300, 0x8015E3F0): with 0x1D0 any qualifying action (a spell or
    /// Two Shots); without it only Two Shots, and as the second, only after a first Two Shots (<paramref name="first"/>,
    /// -1 for the first).
    /// </summary>
    internal static bool OpensPair(byte* unit, int ability, int first)
    {
        if ((unit[Support] & SupportBit) != 0)
            return Qualifies(ability);
        return ability == GunSkills.TwoShots && (first < 0 || first == GunSkills.TwoShots);
    }

    [UnmanagedCallersOnly]
    private static int PlayerActs(int index, int moved, int acted, byte* ui) => Acts(index, moved, acted, ui);

    /// <summary>The player's action marked (0x310548): a first qualifying spell isn't counted as the turn's action.</summary>
    internal static int Acts(int index, int moved, int acted, byte* ui)
    {
        try
        {
            if (acted != 0 && Active && (uint)index < Battle.UnitCount && ui != null)
            {
                var unit = _game.TurnCode.Units + index * Battle.UnitSize;
                var record = ui + UiRecord;
                var ability = *(ushort*)(record + 2);
                if (record[0] == index && !RidingActions.IsMountSkill(record)
                    && OpensPair(unit, ability, Pair[index] != 0 ? *(ushort*)(unit + UnitRecord + 2) : -1))
                {
                    if (Pair[index] == 0)
                    {
                        Pending[index] = PendingFirst;
                        acted = 0;
                        Tracer.Line(_game.Log, $"two spells: unit {index}'s first spell 0x{ability:X3} keeps the action");
                    }
                    else
                        Pending[index] = PendingSecond;
                }
            }
        }
        catch (Exception e)
        {
            Failed("the turn's action", e);
        }
        try
        {
            if (Active && ui != null)
                acted = RidingActions.Acts(index, acted, ui + UiRecord);
        }
        catch (Exception e)
        {
            RidingActions.Failed("the turn's action", e);
        }
        // Marking the second ends the turn there and then if the unit has moved (always after Two Shots: a Charge's
        // commit uses the move), before the queue: TurnEnded keeps the second's mark for the queue.
        _markingSecond = (uint)index < Battle.UnitCount && Pending[index] == PendingSecond ? index : -1;
        try
        {
            return GameSetActed(index, moved, acted);
        }
        finally
        {
            _markingSecond = -1;
        }
    }

    /// <summary>Before the queue's commit (from <see cref="Rules"/>' hook): the first spell kept for a second one.</summary>
    internal static void BeforeQueue(byte* record, byte* queued, int commit)
    {
        try
        {
            var index = Queued(record, queued, commit);
            if (index < 0)
                return;
            var unit = queued - UnitRecord;
            if (Pending[index] == PendingSecond)
            {
                new Span<byte>(queued, RecordSize).CopyTo(FirstRecord);
                _firstCt = unit[UnitCharge];
            }
            else if (SlotCt[index] != Empty && !RidingActions.IsMountSkill(record))
                EmptySlot(index, "a new action replaces its charges");
        }
        catch (Exception e)
        {
            Failed("the queue", e);
        }
    }

    /// <summary>After the queue's commit: the first spell's action given back, or the two spells sorted into the record and the slot.</summary>
    internal static void AfterQueue(byte* record, byte* queued, int commit)
    {
        try
        {
            var index = Queued(record, queued, commit);
            if (index < 0)
                return;
            var unit = queued - UnitRecord;
            switch (Pending[index])
            {
                case PendingFirst:
                    unit[UnitActed] = 0;
                    Pair[index] = 1;
                    break;
                case PendingSecond:
                    Pair[index] = 0;
                    if (_firstCt is > 0 and < Empty)
                    {
                        var ct = unit[UnitCharge];
                        if (ct < _firstCt)
                            Fill(index, FirstRecord, _firstCt, "the first, which finishes later");
                        else
                        {
                            Fill(index, new ReadOnlySpan<byte>(queued, RecordSize), ct, "the second");
                            FirstRecord.CopyTo(new Span<byte>(queued, RecordSize));
                            unit[UnitCharge] = _firstCt;
                        }
                    }
                    break;
                default:
                    // An AI unit with 0x1D0 casts its qualifying spell twice.
                    if ((unit[5] & 0x08) == 0 && unit[UnitCharge] is > 0 and < Empty
                        && OpensPair(unit, *(ushort*)(queued + 2), -1))
                        Fill(index, new ReadOnlySpan<byte>(queued, RecordSize), unit[UnitCharge], "a copy, for the AI");
                    break;
            }
            Pending[index] = 0;
            _firstCt = Empty;
        }
        catch (Exception e)
        {
            Failed("the queue", e);
        }
    }

    /// <summary>The queued unit's battle index for a real commit (not simulated, not a dry run), or -1.</summary>
    internal static int Queued(byte* record, byte* queued, int commit)
    {
        if (!Turns || commit == 0 || record == null || !Active || *_dryRun != 0)
            return -1;
        int index = record[0];
        return (uint)index < Battle.UnitCount && queued == _game.TurnCode.Units + index * Battle.UnitSize + UnitRecord ? index : -1;
    }

    private static void Fill(int index, ReadOnlySpan<byte> record, byte ct, string what)
    {
        record.CopyTo(SlotRecords.AsSpan(index * RecordSize, RecordSize));
        SlotCt[index] = ct;
        Tracer.Line(_game.Log, $"two spells: unit {index}'s second slot holds {what}, 0x{record[2] | (record[3] << 8):X3} at CT {ct}");
    }

    private static void EmptySlot(int index, string why)
    {
        SlotCt[index] = Empty;
        Tracer.Line(_game.Log, $"two spells: unit {index}'s second slot is emptied: {why}");
    }

    [UnmanagedCallersOnly]
    private static void TickEntry()
    {
        try
        {
            Tick();
        }
        catch (Exception e)
        {
            Failed("the second slot's countdown", e);
        }
    }

    /// <summary>After the turn loop's countdown: each slot ticks like a charge, and takes the record once the first has fired.</summary>
    internal static void Tick()
    {
        if (Active)
        {
            TestRecorder.AutoBattle(_game.TurnCode.Units); // the test mode's AI-played units, before the first turn
            LevelScaling.AfterFormation(_game.TurnCode.Units);
        }
        if (!Turns || !Active)
            return;
        for (var index = 0; index < Battle.UnitCount; index++)
        {
            if (SlotCt[index] == Empty)
                continue;
            var unit = _game.TurnCode.Units + index * Battle.UnitSize;
            if (unit[1] == Empty)
            {
                EmptySlot(index, "the unit is gone");
                continue;
            }
            if ((GameStopped(unit) & 0x0F) == 0 && (byte)(SlotCt[index] - 1) <= 0xFD)
                SlotCt[index]--;
            if (unit[UnitCharge] != Empty)
                continue;
            if ((unit[Battle.UnitStatuses] & Charging) == 0)
            {
                EmptySlot(index, "its charge was cancelled");
                continue;
            }
            SlotRecords.AsSpan(index * RecordSize, RecordSize).CopyTo(new Span<byte>(unit + UnitRecord, RecordSize));
            unit[UnitCharge] = SlotCt[index];
            Tracer.Line(_game.Log, $"two spells: unit {index}'s second spell 0x{*(ushort*)(unit + UnitRecord + 2):X3} takes the charge, CT {SlotCt[index]}");
            SlotCt[index] = Empty;
        }
    }

    [UnmanagedCallersOnly]
    private static nint ActionStart(byte* unit, int state) => StartAction(unit, state);

    /// <summary>
    /// The action's start (0x279370 with 0xFF): with a second spell in the slot the unit keeps Charging; a performer
    /// whose song <see cref="Performances"/> keeps through an instant action keeps Performing as it was.
    /// </summary>
    internal static nint StartAction(byte* unit, int state)
    {
        try
        {
            if (Turns && Active && (byte)state == 0xFF && unit != null && (unit[Battle.UnitStatuses] & Performing) != 0
                && Performances.AtActionStart(IndexOf(unit), unit))
                return 0;
        }
        catch (Exception e)
        {
            Performances.Failed("the action's start", e);
        }
        try
        {
            if (Turns && Active && (byte)state == 0xFF && unit != null && (unit[Battle.UnitStatuses] & Performing) == 0)
            {
                var index = IndexOf(unit);
                if (index >= 0 && SlotCt[index] != Empty)
                {
                    Tracer.Line(_game.Log, $"two spells: unit {index} keeps charging its second spell");
                    return 0;
                }
                if (index >= 0 && RidingActions.Parked(index))
                {
                    Tracer.Line(_game.Log, $"riding: unit {index} keeps charging while its mount acts");
                    return 0;
                }
            }
        }
        catch (Exception e)
        {
            Failed("the action's start", e);
        }
        return GameActionState(unit, state);
    }

    [UnmanagedCallersOnly]
    private static void ActionSetup(byte* unit)
    {
        _actionSetup(unit);
        try
        {
            if (Turns && Active && unit != null && DoubleCast(unit) && *_strikes == 1)
            {
                *_strikes = 2;
                Tracer.Line(_game.Log, $"two spells: unit {IndexOf(unit)} casts 0x{*(ushort*)(unit + UnitRecord + 2):X3} twice at once (Non-charge)");
            }
        }
        catch (Exception e)
        {
            Failed("the action's setup", e);
        }
    }

    /// <summary>An AI unit with 0x1D0 and Non-charge (+0x9B &amp; 4) casting a qualifying spell: no charge to copy, so two strikes.</summary>
    internal static bool DoubleCast(byte* unit) =>
        (unit[5] & 0x08) == 0 && (unit[Support] & SupportBit) != 0 && (unit[Battle.UnitSupports + 3] & 0x04) != 0
        && Qualifies(*(ushort*)(unit + UnitRecord + 2));

    /// <summary>
    /// The end of a unit's turn (from <see cref="Movement"/>' hook): its pair is over. When it comes from marking the
    /// pair's second action (0x310548 ends the turn of a unit that has moved and now acted), the second is queued
    /// right after, so its mark stays for <see cref="BeforeQueue"/> and <see cref="AfterQueue"/>, which clear it.
    /// </summary>
    internal static void TurnEnded(int index)
    {
        if (Turns && (uint)index < Battle.UnitCount)
        {
            Pair[index] = 0;
            if (index != _markingSecond || Pending[index] != PendingSecond)
                Pending[index] = 0;
            else
                Tracer.Line(_game.Log, $"two spells: unit {index}'s turn ends as its second action is marked; the second still goes to the slot");
        }
        RidingActions.TurnEnded(index);
    }

    /// <summary>A new battle (and the offline check): no pairs, empty slots.</summary>
    internal static void ResetAll()
    {
        Array.Clear(Pair);
        Array.Clear(Pending);
        Array.Fill(SlotCt, (byte)Empty);
        _firstCt = Empty;
        _markingSecond = -1;
        RidingActions.Reset();
    }

    /// <summary>The skillsets a unit's command list gets after its secondary, in order.</summary>
    internal static List<int> ExtraCommands(byte* unit)
    {
        var extra = new List<int>();
        if (!Extras || (unit[Support] & SupportBit) == 0)
            return extra;
        foreach (var skillset in new[] { WhiteMagic, BlackMagic })
            if (IsExtra(unit, skillset))
                extra.Add(skillset);
        return extra;
    }

    /// <summary>A skillset is one of the unit's extra lists: White or Black Magic, with 0x1D0, not its primary or secondary.</summary>
    internal static bool IsExtra(byte* unit, int skillset) =>
        skillset is WhiteMagic or BlackMagic && (unit[Support] & SupportBit) != 0
        && unit[0x12] != skillset && unit[Battle.UnitSecondary] != skillset;

    /// <summary>ReMixed's limit for the extra lists: MP 1–15 and CT 1–5 in the ability's data (the CT's low 7 bits, as the game reads it).</summary>
    internal static bool ExtraSpell(byte* record)
    {
        int ct = record[0x0C] & 0x7F, mp = record[0x0D];
        return mp is >= 1 and <= 15 && ct is >= 1 and <= 5;
    }

    /// <summary>
    /// Whether an ability stays in a unit's list, by Two Spells' rules (asked by the weapon stubs; skillset -1 when
    /// the list's skillset isn't known there).
    /// </summary>
    internal static bool Shows(byte* unit, int skillset, int ability)
    {
        if (!Limits || _inMath > 0 || unit == null)
            return true;
        var index = Turns ? IndexOf(unit) : -1;
        var between = index >= 0 && Pair[index] != 0;
        var extra = skillset >= 0 && IsExtra(unit, skillset);
        if (!between && !extra)
            return true;
        var record = _game.RuleCode.GetAbilityAction(ability);
        if (record == null)
            return true;
        if (extra && !ExtraSpell(record))
        {
            Tracer.Line(_game.Log, $"two spells: 0x{ability:X3} left out of skillset 0x{skillset:X2} (CT {record[0x0C] & 0x7F}, MP {record[0x0D]})");
            return false;
        }
        if (between && (!Qualifies(ability) || (unit[Support] & SupportBit) == 0))
        {
            Tracer.Line(_game.Log, $"two spells: 0x{ability:X3} left out between unit {index}'s two spells");
            return false;
        }
        return true;
    }

    [UnmanagedCallersOnly]
    private static int Commands(int index, byte* list)
    {
        var result = _commands(index, list);
        try
        {
            if (result == 0 && (uint)index < Battle.UnitCount)
                Insert(index, list, _game.TurnCode.Units + index * Battle.UnitSize);
        }
        catch (Exception e)
        {
            Failed("the command list", e);
        }
        return result;
    }

    /// <summary>
    /// Puts the extra commands after the secondary (or the primary, or Attack), and Dragon Tech after Jump
    /// (<see cref="DragonTech"/>), then redoes the types and flags.
    /// </summary>
    internal static void Insert(int index, byte* list, byte* unit)
    {
        var extra = ExtraCommands(unit);
        var between = Turns && (uint)index < Battle.UnitCount && Pair[index] != 0;
        var entries = new List<int>();
        for (var k = 0; k < MaxCommands + 1 && list[k] != 0xFF; k++)
            entries.Add(list[k]);
        var dragon = DragonTech.Commands && entries.Contains(DragonTech.Jump);
        var riding = RidingActions.On && Riding(index, unit, entries);
        if (extra.Count == 0 && !between && !dragon && !riding)
            return;
        // After Attack (1), the primary and the secondary: before Defend (2) and Equip Change (3).
        var at = entries.FindIndex(e => e is 2 or 3);
        if (at < 0)
            at = entries.Count;
        entries.InsertRange(at, extra);
        if (dragon)
            DragonTech.AddCommands(entries);
        // Between the two spells: no Attack, Defend or Equip Change, and only ability lists can be used.
        if (between)
            entries.RemoveAll(e => e is 1 or 2 or 3);
        // A rider: the mount's skillset (RidingActions), last so it sees the list as it stands.
        if (riding)
            RidingActions.Commands(index, unit, entries);
        if (entries.Count > MaxCommands)
            entries.RemoveRange(MaxCommands, entries.Count - MaxCommands);
        for (var k = 0; k < entries.Count; k++)
            list[k] = (byte)entries[k];
        list[entries.Count] = 0xFF;
        for (var k = 0; k < entries.Count; k++)
        {
            var type = _typeTable[entries[k]] & 0x0F;
            _types[k] = (byte)type;
            _unusable[k] = (byte)(Usable(index, entries[k], type) == 0 || (between && type is not (0 or ChargeType)) ? 1 : 0);
        }
        Tracer.Line(_game.Log, $"{(extra.Count > 0 || between ? "two spells" : riding ? "riding" : "dragon tech")}: unit {index}'s commands "
                               + string.Join(" ", entries.Select(e => $"0x{e:X2}")));
    }

    /// <summary>Whether riding changes this unit's list (asked on a copy, so the real change comes after the others).</summary>
    private static bool Riding(int index, byte* unit, List<int> entries) => RidingActions.Commands(index, unit, [.. entries]);

    [UnmanagedCallersOnly]
    private static nint MathTerms(nint a, nint b, nint c, nint d)
    {
        _inMath++;
        try
        {
            return _mathTerms(a, b, c, d);
        }
        finally
        {
            _inMath--;
        }
    }

    [UnmanagedCallersOnly]
    private static nint MathSpells(nint a, nint b, nint c, nint d)
    {
        _inMath++;
        try
        {
            return GameMathSpells(a, b, c, d);
        }
        finally
        {
            _inMath--;
        }
    }

    [UnmanagedCallersOnly]
    private static int Candidates(int index, int skillset, int count) => AddCandidates(index, skillset, count);

    /// <summary>The game's candidates for a skillset, then, after the secondary's, the extra lists'.</summary>
    internal static int AddCandidates(int index, int skillset, int count)
    {
        count = GameCandidates(index, skillset, count);
        try
        {
            if ((uint)index >= Battle.UnitCount)
                return count;
            var unit = _game.TurnCode.Units + index * Battle.UnitSize;
            if (skillset != unit[Battle.UnitSecondary] || skillset == unit[0x12])
                return count;
            foreach (var extra in ExtraCommands(unit))
                count = ExtraCandidates(index, extra, count);
        }
        catch (Exception e)
        {
            Failed("the AI's extra lists", e);
        }
        return count;
    }

    /// <summary>One extra list's candidates, kept within the unit's list (the wait entry needs the last place).</summary>
    private static int ExtraCandidates(int index, int skillset, int count)
    {
        const int last = AiEntries - 1;
        if ((uint)count >= last)
            return count;
        var list = _aiLists + _aiSlots[index] * AiEntries * AiEntrySize;
        var saved = stackalloc byte[AiSaved * AiEntrySize];
        var from = list + count * AiEntrySize;
        new Span<byte>(from, AiSaved * AiEntrySize).CopyTo(new Span<byte>(saved, AiSaved * AiEntrySize));
        var after = GameCandidates(index, skillset, count);
        if (after > last)
        {
            // Put back what went past the room: the next list's entries (or what follows the last list).
            var keep = (last - count) * AiEntrySize;
            var spill = Math.Min(after - count, AiSaved) * AiEntrySize - keep;
            new Span<byte>(saved + keep, spill).CopyTo(new Span<byte>(from + keep, spill));
            Tracer.Line(_game.Log, $"two spells: unit {index}'s AI list is full; skillset 0x{skillset:X2} cut at {last}");
            after = last;
        }
        if (after > count)
            Tracer.Line(_game.Log, $"two spells: unit {index}'s AI list gets {after - count} from skillset 0x{skillset:X2}");
        return after;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"two spells: {what} failed: {e}");
    }
}
