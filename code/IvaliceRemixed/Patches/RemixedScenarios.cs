using System.Runtime.InteropServices;
using System.Text;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's own scenarios in the remaster (docs/analysis/story-flow-plan.md): its missions first. The build
/// renumbers each into a scenario id the remaster leaves free and writes <see cref="FileName"/> beside the DLL: their
/// scenario records and formation records. This adds them to the remaster's tables, and makes room for their battle
/// entries. Installed by <see cref="Rules"/> with "remixed-scenarios".
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Scenario records (24 bytes: +0 the id, +2 the map, +7 the battle entry, +9 and +0xB the formations, +0x16 the
/// conditions' key): the remaster's table is compiled in (.data 0x8ECAB0, room for 0x377), ending at the first id 0;
/// its setup (0x2FB7C8) and the others search it in order, so new records go on the end.</item>
/// <item>Formation records (12 bytes, record n for formation n, its id at +10): the table behind [0x8ECA58] (.data
/// 0x7AE2D0); ReMixed's new ones are empty in the remaster.</item>
/// <item>Battle conditions: the setup loads them from an NXD table (0xA3, enhancedbattleevent) by the record's +0x16,
/// and stops before the battle entries without a row, so the record gets a key that has one (<see cref="SafeConditions"/>,
/// whose "var 0x1FD = 1 → event 2" is replaced before it's checked). The record's own +0x16 is ReMixed's preset
/// (its 0xCE number), which the setup's hook loads in its place: "var 0x1FD = 1 → the scenario's event" and the rest,
/// renumbered by the build.</item>
/// <item>ReMixed's missions: its WORLD 0x8013E4BC is a list of 24 (entry i: its battle, the mod's scenario in
/// <see cref="Missions"/>; its text world message 0x5A8 + i, "Lost Heirloom", "The Ball" and so on), each shown once
/// ReMixed's story sets flag 0x1A4 + i, and accepted with Start while its text is up (WORLD 0x8013E4EC, the list's
/// description window). They aren't world-map locations: an earlier build started entry i on Start at location i,
/// which gave Lost Heirloom at Gariland just after the second battle, where ReMixed has none (seen in the PS1's, run on
/// the Deck, 2026-11-06). ReMixed lists the unlocked ones under Bonus Content in its Brave Story, and Start while a
/// mission's posting shows starts its battle. The mod posts them among the tavern rumors (<see cref="Postings"/>: rows
/// the build adds to the Gossip table, listed by <see cref="RumorShown"/> once the story has reached the point where
/// ReMixed unlocks each), and Start on one accepts it (<see cref="PostingAt"/>, from the world map's buttons, 0x2CC980,
/// which run under the tavern's screens too); its battle starts once the world map is back to itself
/// (<see cref="Accepted"/>), as the world map starts its story events (<see cref="Begin"/>). In the self-playing test
/// mode Start on the world map starts the first mission (<see cref="TestMission"/>).</item>
/// <item>Text: the remaster's MESSAGE names a line by its Id (a u32), looked up in the scenario text its ScenarioId row
/// gives the event (nxd/text/scenario/scenarioNNNN.en.pzd, listed in ScionFiles). The build writes ReMixed's lines
/// there from 315160 on, past the remaster's own, converts its MESSAGEs to the remaster's layout naming them (with a
/// never-run MESSAGE for each line only reached at run time, so the box sizes have their groups), and the file gives
/// each event's first line (less 0x40001) and count, for the numbers ReMixed's code writes in as it runs
/// (<see cref="MoveMessage"/>).</item>
/// <item>Phases: a battle event's later phases run when one of its condition sets fires (the walker 0x2391C4, over
/// 16 sets, each a chain of records for the checker 0x239294). ReMixed's run-event action (BATTLE 0x80159BE8) puts
/// the firing set's index in var 1 before setting var 0x27, and its events pick their phase block by it (0xD1 n,
/// the Event Instruction Upgrade's <see cref="EventInstructionUpgrade.Interfaces.IEventInstructions.PhaseBlock"/>); the remaster's doesn't. So for ReMixed's scenarios the walker is the
/// mod's (<see cref="Walk"/>, which replaces the game's whole): the same walk through the game's checker, and var 1
/// set to the set that fired.</item>
/// <item>Battle entries: the build puts ReMixed's in ENTD4's slots no scenario names (from 0x1D6). Not from 0x200 on
/// (the remaster's <c>event_btlevt_bin_entrydata_add_ent.bin</c>): the game also reads a row per entry in the EntryNo NXD
/// table (0xFB65C, from the formation setup 0x1EE9B4, unchecked), and a row added past 0x1FF wasn't found.</item>
/// </list>
/// </remarks>
internal static unsafe class RemixedScenarios
{
    internal const string FileName = "remixed-scenarios.bin";
    internal const int ScenarioSize = 24, FormationSize = 12, FormationId = 10, ScenarioRoom = 0x377, FormationCount = 0x300;
    internal const int ConditionsKey = 0x16, SafeConditions = 0, MessageAt = 3, ChangeAt = 2, LineIdBase = 0x40000;
    /// <summary>The replay screen's mark, set by the start from the world map (<see cref="StartScenario"/>).</summary>
    internal const int ReplayVariable = 0x1FC;

    /// <summary>Start in either controller's half of the world's button word (the PS1's layout: the first pad's in the
    /// low half; which of the remaster's two pads is the player's isn't known yet).</summary>
    internal const uint Start = 0x08000800;
    internal const string PresetsFileName = "remixed-event-presets.bin";

    /// <summary>ReMixed's preset per scenario added, and the scenario per entry of ReMixed's mission list.</summary>
    internal static readonly Dictionary<int, int> Presets = [];
    internal static readonly Dictionary<int, int> Missions = [];
    /// <summary>ReMixed's missions posted among the tavern rumors (the build's MISSIONS): the posting's rumor (a Gossip
    /// row), the mod's scenario, the story progress (var 0x6E) from which it's posted (0xFFFF: only by the flag) and a
    /// flag that posts it too (0: none).</summary>
    internal static readonly List<(int Rumor, int Scenario, int Progress, int Unlock)> Postings = [];
    /// <summary>Ordinary ReMixed rumors added by the build: rumor, minimum story progress, a flag that must be set,
    /// and two flags that must be clear. They are shown by <see cref="RumorShown"/> but cannot be accepted as
    /// missions.</summary>
    internal static readonly List<(int Rumor, int Progress, int Required, int Clear1, int Clear2)> ConditionalRumors = [];
    /// <summary>The mission accepted in a tavern (its index in <see cref="Postings"/>), or -1: its battle starts once the
    /// world map is back to itself (<see cref="MapState"/> at depth 1), as starting it under the tavern's screens left
    /// them drawn over the battle's formation (seen on the PC, 2026-11-07).</summary>
    internal static volatile int Accepted = -1;
    /// <summary>The tavern's rumor list (the bar screen's +0x70), as its last event named it (<see cref="BarListEvent"/>):
    /// the cursor's entry at <see cref="ListCursorAt"/>, the entries (16 bytes each, the rumor first) from the vector at
    /// <see cref="ListEntriesAt"/> (begin, end), as the list's cursor move (0x19FD0C) reads them.</summary>
    internal static nint RumorList;
    /// <summary>The Tavern screen that owns <see cref="RumorList"/>. Dispatch Missions uses it to leave the rumor
    /// posting for the remaster's native dispatch UI without starting the old battle-event menu.</summary>
    internal static nint BarScreen;
    /// <summary>A Tavern screen whose Dispatch Missions posting was chosen. Rumors must finish its native close
    /// transition before the same screen can enter the separate dispatch view.</summary>
    private static nint PendingDispatchScreen;
    internal const int ListCursorAt = 0xC0, ListEntriesAt = 0x3E8, ListEntryBytes = 16;
    /// <summary>The world map's states (the top of its stack, <see cref="WorldStates"/>): 0 the map itself (at depth 1),
    /// 6 a tavern's rumor list (at depth 3, over the tavern's menu, 5, which replaces the town's, 4).</summary>
    internal const int MapState = 0, TavernMenuState = 5, RumorListState = 6;
    internal const int StoryProgress = 0x6E, NoProgress = 0xFFFF;
    /// <summary>Per ReMixed event: the remaster's message number for its message 0 (its first line's Id less 0x40001
    /// plus 1), and how many it has.</summary>
    internal static readonly Dictionary<int, (int Base, int Count)> Messages = [];
    /// <summary>The hand-offs that offer side chapter 1-A: (a scenario of the remaster's, the one it goes on to, the
    /// mode), from the file. The remaster's end of chapter 1 (0x05A) goes on to ReMixed's offer (its 0x0EC as the mod
    /// numbers it), which asks in a menu, as ReMixed does: declining goes on to chapter 2.</summary>
    internal static readonly List<(int From, int To, int Mode)> Offers = [];

    /// <summary>
    /// Scenarios whose 16 units come from the mod's file (the build's, past ENTD4's free slots): the mod's scenario ->
    /// its records (unmanaged, <see cref="EntryBytes"/>). Their records name one kept slot; the battle setup gets a
    /// scenario's units from <see cref="EntryRecords"/> (0x28416C: the entry in [0x1856930], the data of the ENTD file
    /// the setup loaded for it in [0x1856938]), which the setup (0x30428C) hands to the unit making (0x27128C), and
    /// the join after a battle (0x2841D0) reads too.
    /// </summary>
    internal static readonly Dictionary<int, nint> OwnEntries = [];

    // The bar's check of a rumor (0x36F53C: its Gossip row's situation and "always" byte, then the town's bit and flag
    // 0x2D1 + the rumor): mov [rsp+8], rbx; mov [rsp+0x10], rsi; push rdi; sub rsp, 0x20; movsxd rdi, ecx; mov esi, edx;
    // mov edx, edi; mov ecx, 0xB5 (the Gossip table); call (its row).
    internal const string RumorShownPattern =
        "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 48 63 F9 8B F2 8B D7 B9 B5 00 00 00 E8";
    internal static nint RumorShownAt;
    private static delegate* unmanaged<int, int, byte> GameRumorShown;
    // The bar's handler of its lists' events (0x19D548, the tavern screen's: rcx the screen, rdx the list's node, r8d the
    // event, r9d the entry; the rumor list is the screen's +0x70, the same node when their +0x20 ids match), at +0x30:
    // mov rsi, rcx; mov [rsp+0x30], rcx; mov rcx, [rcx+0x70]; xor ebx, ebx; movsxd r14, r9d; mov r13, rdx;
    // lea rdx, [the rumors' marks]; mov edi, r8d; test rcx, rcx.
    internal const string BarListEventPattern =
        "48 8B F1 48 89 4C 24 30 48 8B 49 70 33 DB 4D 63 F1 4C 8B EA 48 8D 15 ?? ?? ?? ?? 41 8B F8 48 85 C9";
    internal static nint BarListEventAt;
    private static delegate* unmanaged<nint, nint, int, int, int, void> GameBarListEvent;
    /// <summary>The rumors' unread marks (0x11A7299 + 2 × rumor, saved; the handler's lea at +0x44 names 0x11A7280): the
    /// bar sends event 0 for a rumor shown while its mark is set, and clears it.</summary>
    internal static byte* RumorUnread;

    /// <summary>
    /// Hand-offs by Ramza's name (roster slot 0's, +0xDC, UTF-8): (the remaster's scenario, the mod's it hands on to
    /// instead, the mode, the names). As the scenario starts (<see cref="Setup"/>) its word (<see cref="SetHandOff"/>)
    /// goes to the mod's for one of the names, else back to its own: ReMixed's Algus campaign starts from the remaster's
    /// opening (scenario 2) for a Ramza named "Algus" or "Argath".
    /// </summary>
    internal static readonly List<(int From, int To, int Mode, string[] Names)> NamedHandOffs = [];
    private static readonly Dictionary<int, (int To, int Mode)> OwnHandOffs = [];
    private static byte* _roster;
    internal const int RosterNameAt = 0xDC, NameBytes = 0x40;
    internal const int EntryBytes = 16 * 40;
    internal const string EntryRecordsPattern =
        "8B 05 ?? ?? ?? ?? 85 C0 74 56 83 3D ?? ?? ?? ?? 00 75 4D 3D 00 02 00 00 7C 18 05 00 FE FF FF";
    internal static nint EntryRecordsAt;
    private static delegate* unmanaged<byte*> GameEntryRecords;
    private static readonly HashSet<int> OwnEntriesNoted = [];
    /// <summary>The event the runner has loaded ([0x782A94], set after the event loader).</summary>
    internal static int* RunningEvent;
    private static readonly HashSet<int> MovedNoted = [];
    private static byte[]? _presets;
    private static nint _sets, _records;
    private static uint* _flags, _pressed, _heldNow, _heldBefore;
    /// <summary>The two pads' records the world map's button function reads (+2 and +3 of each, active low), and buttons
    /// a test holds there for a few frames (Harness's worldpad).</summary>
    private static byte** _pad1, _pad2;
    internal static volatile uint InjectHeld;
    internal static volatile int InjectFrames;
    private static Log _log = null!;
    internal static delegate* unmanaged<long> GameSetup;
    internal static delegate* unmanaged<void> GameButtons;
    internal static delegate* unmanaged<int, void> GameLoadEvent;
    /// <summary>Breadcrumbs while one of ReMixed's scenarios starts (the first tests crashed after its units were
    /// built): each file the game reads and each event it loads, up to <see cref="WatchLines"/> lines.</summary>
    private static int _watch;
    private const int WatchLines = 60;
    private static readonly HashSet<uint> SeenButtons = [];
    /// <summary>A scenario the autopilot (a test run) asks for: the world map's next frame starts it.</summary>
    internal static int Requested;
    /// <summary>For the harness: the scene a requested scenario's start notes as seen (var 0x3E), so a scene-first battle
    /// (MenuBattles) starts at its battle's run. Set as the request starts, after the world map's stale-note clear.</summary>
    internal static int RequestedNote;
    /// <summary>A note the harness set, held from the stale-note clear until the scenario's setup has read it.</summary>
    private static int NoteHeld;
    /// <summary>An event phase the private harness asks the battle condition walker to run. The walker consumes both
    /// on the game thread, so the ordinary loader and interpreter run it; only its in-battle condition is bypassed.</summary>
    private static int _requestedEventPhase;
    /// <summary>The battle entries the added scenarios name (+7), for the breadcrumbs.</summary>
    internal static readonly HashSet<int> EntriesUsed = [];
    private static delegate* unmanaged<int, int, int, int, int, long> _check;
    private static int* _walkBusy, _walkBusy2;
    private static nint* _walkState;
    private static sbyte* _recordLengths;
    private static byte* _battleUnits;
    // A plain-victory set can be walked during a split battle's black setup transition, before its ENTD units have
    // reached the battle array. An empty array is not a victory: arm the condition only after this setup has exposed
    // at least one enemy record. This also protects event-spawned battles such as Brave Story's first round.
    private static bool _victorySawEnemy;
    internal static nint WalkAt, CheckAt;
    /// <summary>World-map frames seen (the autopilot waits for them).</summary>
    internal static volatile int WorldFrames;

    /// <summary>Which of the bar's rumors the list shows (<see cref="RumorShown"/>): all (outside the Tavern's lists),
    /// only the rumors (its Rumors button) or only ReMixed's mission postings (its Missions button, asked 2026-11-18).
    /// TavernStorage sets it as a button is chosen and clears it as the Tavern's menu comes back.</summary>
    internal static volatile int RumorFilter;
    internal const int AllRumors = 0, RumorsOnly = 1, MissionsOnly = 2;
    /// <summary>The Tavern screen's state while its rumor list is up (its +0x98).</summary>
    internal const int RumorListBarState = 2;

    /// <summary>Whether any of ReMixed's missions is posted (Dispatch Missions' posting aside, which is never listed).</summary>
    internal static bool AnyMissionPosted =>
        Postings.Exists(p => p.Scenario != TavernStorage.DispatchScenario && Unlocked(p));

    /// <summary>Whether ReMixed has unlocked Dispatch Missions: its posting's unlock (<see cref="Unlocked"/>).</summary>
    internal static bool DispatchUnlocked =>
        Postings.Exists(p => p.Scenario == TavernStorage.DispatchScenario && Unlocked(p));

    /// <summary>Queue the native Dispatch screen from the stable Tavern menu. It runs in <see cref="Buttons"/> on the
    /// game thread, just like the player-facing posting, without opening and scrolling through Rumors first.</summary>
    internal static string RequestDispatchForHarness(string kind, int story, int month)
    {
        if (WorldDepth == null || WorldStates == null || *WorldDepth != 2
            || WorldStates[1] != TavernMenuState || (*_flags & 4) != 0 || BarScreen == 0)
            return "error: dispatch requires the stable Tavern menu";
        Nemesis.SetVariable(0x6F, Math.Clamp(story, 0, 0x10), 0);
        Nemesis.SetVariable(0x2E, Math.Clamp(month, 1, 12), 0);
        var selected = TavernStorage.SetHarnessKind(kind);
        PendingDispatchScreen = BarScreen;
        return $"queued Dispatch Missions ({selected}, story {Math.Clamp(story, 0, 0x10)}, month {Math.Clamp(month, 1, 12)})";
    }

    /// <summary>The replay screen's start of a scenario (0x2D28C8): var 0x1FC = 1, var 0x27 = the scenario, then the change
    /// of mode its replay screen uses. Not used to start ReMixed's scenarios any more (see <see cref="Begin"/>); kept as a
    /// landmark the offline check finds.</summary>
    internal static delegate* unmanaged<int, byte, void> StartScenario;

    /// <summary>The world map's start of a story event (0x247E10, what its conditions' type 25 action leads to on
    /// arrival): var 0x27 = the event in <see cref="WorldEvent"/>[n], then its state 0x23 (0x2CC2E0) with the mode in
    /// <see cref="WorldMode"/> (2 cuts straight to the event, as most of the remaster's starts do).</summary>
    internal static delegate* unmanaged<int, void> WorldStart;
    internal static int* WorldEvent, WorldMode, WorldDepth, WorldStates;
    internal const int WorldStartMode = 2;
    // push rbx; sub rsp, 0x20; movsxd rbx, ecx; call; mov eax, [depth]; lea rcx, [image]; and dword [..], 0; dec eax;
    // movsxd rdx, eax; imul rax, rdx, 0x5C (0x247E10).
    internal const string WorldStartPattern =
        "40 53 48 83 EC 20 48 63 D9 E8 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 83 25 ?? ?? ?? ?? 00 FF C8 48 63 D0 48 6B C2 5C";

    // mov r9d, [the running event]; xor ebx, ebx; test r9d, r9d; js; lea rcx, [scenarios] (0x20DFAE).
    internal const string ScenariosPattern = "44 8B 0D ?? ?? ?? ?? 33 DB 45 85 C9 0F 88 ?? ?? ?? ?? 48 8D 0D";
    // mov rax, [formations]; lea rcx, [rdx+rdx*2]; mov ecx, [rax+rcx*4]; neg ecx; mov ecx, 0x1FE (0x2FB680).
    internal const string FormationsPattern = "48 8B 05 ?? ?? ?? ?? 48 8D 0C 52 8B 0C 88 F7 D9 B9 FE 01 00 00";
    // mov [rsp+8], rbx; mov [rsp+0x10], rsi; push rdi; sub rsp, 0x20; movzx ebx, dl; mov edi, ecx; mov esi, 1;
    // mov ecx, 0x1FC; mov edx, esi; call; mov edx, edi; lea ecx, [rsi+0x26] (0x2D28C8).
    internal const string StartPattern =
        "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 0F B6 DA 8B F9 BE 01 00 00 00 B9 FC 01 00 00 8B D6 E8 ?? ?? ?? ?? 8B D7 8D 4E 26";

    // push rbx, rbp, rsi, rdi, r12-r15; sub rsp, 0x68; mov edi, 0x64 (var 100); mov ecx, edi; call (0x2FB7C8).
    internal const string SetupPattern =
        "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 68 BF 64 00 00 00 8B CF E8";
    // The event loader (0x2340A4): ... mov edi, 2; lea rbp, [the event buffer]; mov esi, ecx; mov r13d, 0x2800.
    internal const string LoadEventPattern =
        "48 83 EC 30 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 28 BF 02 00 00 00 48 8D 2D ?? ?? ?? ?? 8B F1 41 BD 00 28 00 00";
    // mov r10d, [the party's location]; lea rcx, [..]; test byte [rcx-4], 4; je; cmp r10d, -1 (0x2D4D19).
    internal const string PartyLocationPattern = "44 8B 15 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? F6 41 FC 04 74 11 41 83 FA FF";
    /// <summary>The world-map location the party stands on.</summary>
    internal static int* PartyLocation;

    // The world's buttons (0x2CC9C0): test byte [flags], 4; not r8d; mov [held before], r8d; je; xor edx, edx;
    // mov [held], edx; jmp; not edx; mov [held], r8d; and edx, r8d; mov [pressed], edx.
    internal const string WorldButtonsPattern =
        "F6 05 ?? ?? ?? ?? 04 41 F7 D0 44 89 05 ?? ?? ?? ?? 74 0A 33 D2 89 15 ?? ?? ?? ?? EB 0C F7 D2 44 89 05 ?? ?? ?? ?? 41 23 D0 89 15";

    internal static byte* Scenarios, Formations;
    /// <summary>For the offline check: the hooked setup, the world's flags and pressed buttons.</summary>
    internal static nint SetupAt, ButtonsAt, LoadEventAt;
    internal static nint WorldFlags => (nint)_flags;
    internal static nint WorldPressed => (nint)_pressed;
    internal static nint WorldHeld => (nint)_heldNow;
    internal static nint WorldHeldBefore => (nint)_heldBefore;
    /// <summary>The controller buttons newly held this world-UI frame. Native menus sometimes consume their own
    /// pressed word before a mod hook can observe it, so Dispatch uses this non-consuming edge as a fallback.</summary>
    internal static uint NewlyHeldButtons => _heldNow == null || _heldBefore == null ? 0 : *_heldNow & ~*_heldBefore;

    public static void Install(Game game)
    {
        var image = game.Image;
        _log = game.Log;
        var scenarios0 = image.FindUnique(ScenariosPattern);
        Scenarios = (byte*)GameImage.Rip(scenarios0 + 0x12, 3, 7);
        RunningEvent = (int*)GameImage.Rip(scenarios0, 3, 7);
        Formations = (byte*)image.Pointer(*(nint*)GameImage.Rip(image.FindUnique(FormationsPattern), 3, 7));
        StartScenario = (delegate* unmanaged<int, byte, void>)image.FindUnique(StartPattern);
        // The world's start (0x247E10): the depth of its state stack at +0xE, the event at +0x49 (image-relative, by
        // n), the mode at +0x5A, then its call of 0x2CC2E0, which writes state 0x23 into the state array at +0xF8.
        var world = image.FindUnique(WorldStartPattern);
        var push = GameImage.Rip(world + 0x60, 1, 5);
        if ((*(uint*)(world + 0x49) & 0xFFFFFF) != 0x99948B || *(ushort*)(world + 0x5A) != 0x0D8B || *(byte*)(world + 0x60) != 0xE8
            || *(uint*)(push + 0xF8) != 0x9084C741 || *(int*)(push + 0x100) != 0x23)
            throw new InvalidDataException("the world map's start of an event isn't laid out as this DLL expects");
        WorldStart = (delegate* unmanaged<int, void>)world;
        WorldDepth = (int*)GameImage.Rip(world + 0xE, 2, 6);
        WorldEvent = (int*)(image.ImageStart + *(int*)(world + 0x4C));
        WorldMode = (int*)GameImage.Rip(world + 0x5A, 2, 6);
        WorldStates = (int*)(image.ImageStart + *(int*)(push + 0xFC));
        if (Id(Scenarios) != 1 || *(ushort*)(Formations + FormationSize + FormationId) != 1)
            throw new InvalidDataException("the scenario or formation table isn't the one this DLL expects");


        PartyLocation = (int*)GameImage.Rip(image.FindUnique(PartyLocationPattern), 3, 7);

        var buttons = image.FindUnique(WorldButtonsPattern);
        _flags = (uint*)GameImage.Rip(buttons, 2, 7);
        _pressed = (uint*)GameImage.Rip(buttons + 0x29, 2, 6);
        // The buttons held, kept every frame (mov [rip+X], r8d at +0xA) even while a menu clears the pressed word, and
        // the word before them (mov [rip+X], edx at the function's +0x14).
        _heldNow = (uint*)GameImage.Rip(buttons + 0xA, 3, 7);
        _heldBefore = (uint*)GameImage.Rip(buttons - 0x40 + 0x14, 2, 6);
        _pad1 = (byte**)GameImage.Rip(buttons - 0x40, 3, 7);
        _pad2 = (byte**)GameImage.Rip(buttons - 0x40 + 7, 3, 7);
        (_sets, _records) = EventTables.ConditionTables(image);
        if (Nemesis.GetVariable == null)
            Nemesis.FindVariables(image);
        // The walker (0x2391C4) holds the conditions pattern at +0x53: its flags at +0xF and +0x18, its state at +0x43,
        // the checker's call at +0x96, the record lengths (image-relative) at +0xA3.
        WalkAt = image.FindUnique(EventTables.ConditionsPattern) - 0x53;
        if (*(uint*)WalkAt != 0x245C8948 || *(byte*)(WalkAt + 0x96) != 0xE8 || *(ushort*)(WalkAt + 0x9F) != 0xBE0F)
            throw new InvalidDataException("the condition walker isn't laid out as this DLL expects");
        _walkBusy = (int*)GameImage.Rip(WalkAt + 0xF, 2, 7);
        _walkBusy2 = (int*)GameImage.Rip(WalkAt + 0x18, 2, 6);
        _walkState = (nint*)GameImage.Rip(WalkAt + 0x43, 3, 7);
        CheckAt = GameImage.Rip(WalkAt + 0x96, 1, 5);
        _check = (delegate* unmanaged<int, int, int, int, int, long>)CheckAt;
        _recordLengths = (sbyte*)(image.ImageStart + *(int*)(WalkAt + 0xA3));
        _battleUnits = game.TurnCode.Units;
        SetupAt = image.FindUnique(SetupPattern);
        RumorShownAt = image.FindUnique(RumorShownPattern);
        BarListEventAt = image.FindUnique(BarListEventPattern) - 0x30;
        if (*(uint*)BarListEventAt != 0x245C8948 || (*(uint*)(BarListEventAt + 0x44) & 0xFFFFFF) != 0x158D48)
            throw new InvalidDataException("the bar's list handler isn't laid out as this DLL expects");
        RumorUnread = (byte*)GameImage.Rip(BarListEventAt + 0x44, 3, 7) + 0x19;
        ButtonsAt = buttons - 0x40;
        LoadEventAt = image.FindUnique(LoadEventPattern) - 0x18;
        EntryRecordsAt = image.FindUnique(EntryRecordsPattern);
        if (*(uint*)ButtonsAt != 0xC1058B48)  // mov rax, [first pad]
            throw new InvalidDataException("the world's button function isn't laid out as this DLL expects");

        NameGuard.Install(game);
        RosterGuard.Install(game);
        var folder = Path.GetDirectoryName(typeof(RemixedScenarios).Assembly.Location) ?? "";
        var file = Path.Combine(folder, FileName);
        if (!File.Exists(file))
        {
            game.Log.Info($"remixed scenarios: no {FileName} beside the DLL; none added");
            return;
        }
        var presets = Path.Combine(folder, PresetsFileName);
        _presets = File.Exists(presets) ? File.ReadAllBytes(presets) : null;
        var (scenarios, formations) = Add(File.ReadAllBytes(file));
        _roster = game.Units.Roster;
        foreach (var (from, to, _, names) in NamedHandOffs)
        {
            OwnHandOffs[from] = GetHandOff(from) ?? (0, 0);
            game.Log.Info($"remixed scenarios: scenario 0x{from:X} hands on to 0x{to:X} for a Ramza named {string.Join(" or ", names)}");
        }
        foreach (var (from, to, mode) in Offers)
            game.Log.Info(SetHandOff(from, to, mode)
                ? $"remixed scenarios: scenario 0x{from:X} hands on to 0x{to:X} (the offer of side chapter 1-A)"
                : $"remixed scenarios: scenario 0x{from:X} isn't in the table; Chapter 1-A isn't offered");
        GameSetup = (delegate* unmanaged<long>)game.Hooks.Hook(
            SetupAt, (nint)(delegate* unmanaged<long>)&Setup, "the scenario setup");
        if (OwnEntries.Count > 0)
            GameEntryRecords = (delegate* unmanaged<byte*>)game.Hooks.Hook(
                EntryRecordsAt, (nint)(delegate* unmanaged<byte*>)&EntryRecords, "the battle entry's records");
        // The walker is replaced whole (an absolute jump at its start, no hook buffer: the space near some of the
        // other rules' hooks is short), since Walk does all it did.
        byte[] jump = [0x48, 0xB8, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xE0]; // mov rax, Walk; jmp rax
        BitConverter.GetBytes((long)(nint)(delegate* unmanaged<long>)&Walk).CopyTo(jump, 2);
        GameImage.WriteCode(WalkAt, jump);
        GameButtons = (delegate* unmanaged<void>)game.Hooks.Hook(
            ButtonsAt, (nint)(delegate* unmanaged<void>)&Buttons, "the world map's buttons");
        PackFiles.Watch = WatchRead;
        GameLoadEvent = (delegate* unmanaged<int, void>)game.Hooks.Hook(
            LoadEventAt, (nint)(delegate* unmanaged<int, void>)&LoadEvent, "the event loader");
        if (Postings.Count > 0 || ConditionalRumors.Count > 0)
        {
            GameRumorShown = (delegate* unmanaged<int, int, byte>)game.Hooks.Hook(
                RumorShownAt, (nint)(delegate* unmanaged<int, int, byte>)&RumorShown, "the bar's check of a rumor");
            GameBarListEvent = (delegate* unmanaged<nint, nint, int, int, int, void>)game.Hooks.Hook(
                BarListEventAt, (nint)(delegate* unmanaged<nint, nint, int, int, int, void>)&BarListEvent,
                "the bar's list events");
        }
        game.Log.Info($"remixed scenarios: {scenarios} scenario record(s) and {formations} formation record(s) added, "
                      + $"{Missions.Count} of them ReMixed's missions ({string.Join(", ", Missions.Select(p => $"{p.Key} 0x{p.Value:X}"))}), "
                      + $"{Postings.Count} posted in the taverns (rumors {string.Join(", ", Postings.Select(p => p.Rumor))})"
                      + $", {ConditionalRumors.Count} conditional rumor(s) ({string.Join(", ", ConditionalRumors.Select(p => p.Rumor))})"
                      + (_presets == null ? $"; no {PresetsFileName}, so their conditions are the remaster's" : ""));
    }

    /// <summary>The setup, then for one of ReMixed's scenarios its preset in place of the conditions it loaded. A battle
    /// played after a scene gets its record set for the run first (<see cref="PrepareMenuBattle"/>).</summary>
    [UnmanagedCallersOnly]
    private static long Setup()
    {
        ResetVictoryObservation();
        try
        {
            PrepareSplitBattle(Nemesis.GetVariable(0x27));
            if (MenuBattles.Count > 0)
                PrepareMenuBattle(Nemesis.GetVariable(0x27));
            NoteHeld = 0;
        }
        catch (Exception e)
        {
            _log.Error($"remixed scenarios: a battle after its scene: {e.Message}");
        }
        var result = GameSetup();
        try
        {
            var scenario = Nemesis.GetVariable(0x27);
            // The named hand-off (the Argath campaign's, from the opening): taken for Ramza named one of its names, as
            // in ReMixed, or with the Argath campaign setting on, as the remaster's enhanced game won't rename Ramza.
            foreach (var (from, to, mode, names) in NamedHandOffs)
            {
                if (scenario != from)
                    continue;
                var name = RosterName(0);
                var named = names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                var taken = named || Options.Current.ArgathCampaign;
                var (ownTo, ownMode) = OwnHandOffs.GetValueOrDefault(from);
                SetHandOff(from, taken ? to : ownTo, taken ? mode : ownMode);
                _log.Info($"remixed scenarios: scenario 0x{from:X} with Ramza named \"{name}\"" +
                          $"{(Options.Current.ArgathCampaign ? " and the Argath campaign setting on" : "")} " +
                          $"hands on to 0x{(taken ? to : ownTo):X}");
            }
            if (Presets.TryGetValue(scenario, out var preset))
            {
                LoadPreset(preset, scenario);
                StartBattle(scenario, preset);
                // The replay screen's mark (var 0x1FC): with it set, the end of the scenario's events (0x23987C) sets
                // the same scenario up again instead of going where its word says. The mod's start doesn't set it any
                // more (Begin); a scenario of ReMixed's reached some other way with it set still runs as a story one.
                if (Nemesis.GetVariable(ReplayVariable) != 0)
                {
                    Nemesis.SetVariable(ReplayVariable, 0, 0);
                    _log.Info($"remixed scenarios: scenario 0x{scenario:X} runs as a story scenario, not a replay");
                }
            }
        }
        catch (Exception e)
        {
            _log.Error($"remixed scenarios: {e.Message}");
        }
        return result;
    }

    /// <summary>State ReMixed's Preparation Menu carries into a split battle's setup. Mandalia's source event sets
    /// flags 0xC1-0xCC before opening the menu; the direct remaster hand-off clears them before <see cref="GameSetup"/>
    /// can include the cast. They must be restored before setup; its opening can then visibly activate the prepared
    /// units. AddUnit alone consumed excluded units' stale resources and produced a Ramza-looking Luso; carrying the
    /// source scene's hidden-add mode into the fresh battle left monster portraits and HP bars but no models.</summary>
    internal static void PrepareSplitBattle(int scenario)
    {
        const int mandalia = 0x34D, first = 0xC1, last = 0xCC;
        if (scenario != mandalia)
            return;
        for (var flag = first; flag <= last; flag++)
            Nemesis.SetVariable(flag, 1, 0);
        _log?.Info("remixed scenarios: Mandalia's inclusion flags 0xC1-0xCC set before battle setup");
    }

    /// <summary>
    /// ReMixed's Preparation Menu's Start Battle, as the Nemesis's return needs it (the build's NEMESIS_SCENE): ReMixed's
    /// battles run its menu as they begin, whose Start Battle sets flag 0x8F and, with a Nemesis stored in var 0x40 and
    /// not disabled (bit 0x100000), clears it again with 0x11D's conditions loaded, so 0x11D runs at once; 0x11D sets the
    /// flag as it ends. The mod has no menu: the battles' conditions run 0x11D on "flag 0x8F = 0" themselves, in a set of
    /// that one condition walked after the opening's (<see cref="HasStartSet"/>, <see cref="WalkOrder"/>). So for one of
    /// ReMixed's scenarios whose conditions have it, flag 0x8F = 0 when its battle begins (its record has a formation: a
    /// scene's run has none, the first run of <see cref="MenuBattles"/> included) and the Nemesis System is on with a
    /// Nemesis stored; else 1, as the menu leaves it.
    /// </summary>
    internal static void StartBattle(int scenario, int preset)
    {
        const int bytes = RemixedEvents.PresetBytes;
        if (_presets == null || (preset + 1) * bytes > _presets.Length || !HasStartSet(_presets.AsSpan(preset * bytes, bytes)))
            return;
        var record = FindRecord(scenario);
        var battle = record != null && *(ushort*)(record + 9) != 0;
        var nemesis = Nemesis.GetVariable(Nemesis.Variable);
        var due = battle && Options.Current.NemesisSystem && NemesisStored(nemesis);
        Nemesis.SetVariable(StartFlag, due ? 0 : 1, 0);
        _log?.Info($"remixed scenarios: scenario 0x{scenario:X} " + (due
            ? $"runs the Nemesis's return as its battle begins (var 0x40 = 0x{nemesis:X8})"
            : battle ? "begins without the Nemesis's return (no Nemesis stored, or the system off)"
                     : "runs its scene first: no Nemesis's return before its battle"));
    }

    /// <summary>ReMixed's flag for its Preparation Menu having run (0x8F), on which its battles run the menu, and in the
    /// mod the Nemesis's return.</summary>
    internal const int StartFlag = 0x8F;

    /// <summary>A Nemesis stored and not disabled, as ReMixed's menu tests var 0x40 (bit 0x100000 its Disable, the
    /// port's off bit, <see cref="SaveOptions.NemesisOff"/>).</summary>
    internal static bool NemesisStored(int nemesis) => nemesis != 0 && (nemesis & SaveOptions.NemesisOff) == 0;

    /// <summary>Whether a preset (0x20 bytes of set offsets, then the records) has a set of the one condition "flag 0x8F
    /// = 0" (type 1) that runs an event (0x19): the Nemesis's return as the build gives it to ReMixed's battles.</summary>
    internal static bool HasStartSet(ReadOnlySpan<byte> preset)
    {
        const int offsets = RemixedEvents.PresetOffsets;
        var words = MemoryMarshal.Cast<byte, ushort>(preset[offsets..]);
        for (var k = 0; k < 16; k++)
        {
            int at = BitConverter.ToUInt16(preset[(2 * k)..]);
            if (k != 0 && at == 0)
                break;
            at /= 2;
            if (at + 5 <= words.Length && words[at] == 1 && words[at + 1] == StartFlag && words[at + 2] == 0
                && words[at + 3] == 0x19)
                return true;
        }
        return false;
    }

    internal static void LoadPreset(int preset, int scenario)
    {
        const int bytes = RemixedEvents.PresetBytes, offsets = RemixedEvents.PresetOffsets;
        if (_presets == null || (preset + 1) * bytes > _presets.Length)
            return;
        _presets.AsSpan(preset * bytes, offsets).CopyTo(new Span<byte>((void*)_sets, offsets));
        _presets.AsSpan(preset * bytes + offsets, bytes - offsets).CopyTo(new Span<byte>((void*)_records, bytes - offsets));
        _log.Info($"remixed scenarios: scenario 0x{scenario:X} takes ReMixed's battle conditions 0x{preset:X}");
    }

    /// <summary>The world map's buttons, each frame there (0x2CC980), which run under its towns' and taverns' screens
    /// too: Start on a mission's posting in a tavern's rumor list accepts it, as Start on a mission's text does in
    /// ReMixed's list (<see cref="PostingAt"/>), and the accepted one starts once the world map is back to itself
    /// (<see cref="Accepted"/>). Also the autopilot's scenario, and in the self-playing test mode Start on the map starts
    /// the first of ReMixed's missions, so it can be tried from any save (<see cref="TestMission"/>). Start on the map
    /// itself does nothing else: it's the remaster's way to skip a scene (the class's remarks). The first presses are
    /// logged.</summary>
    [UnmanagedCallersOnly]
    private static void Buttons()
    {
        if (InjectFrames > 0 && *_pad1 != null && *_pad2 != null)
        {
            InjectFrames--;
            var held = InjectHeld;
            (*_pad1)[2] &= (byte)~(held >> 24);
            (*_pad1)[3] &= (byte)~(held >> 16);
            (*_pad2)[2] &= (byte)~(held >> 8);
            (*_pad2)[3] &= (byte)~held;
        }
        GameButtons();
        WorldFrames++;
        // A save is loaded after the rules install. Repair old 1-A records here, once a second, so the three
        // Lionsguard are available in the party menu before another battle is started.
        if (WorldFrames % 60 == 1)
            OldGuests.RepairLoadedSideChapterRoster();
        SaveOptions.Sync();
        TavernStorage.Tick();
        try
        {
            var pressed = *_pressed;
            // Pressed whether or not a menu is open (the world's pressed word is 0 while one is: its flag 4).
            var newly = *_heldNow & ~*_heldBefore;
            if (newly != 0 && SeenButtons.Count < 24 && SeenButtons.Add(newly))
                _log.Info($"remixed scenarios: world-map buttons pressed 0x{newly:X8} (world flags 0x{*_flags:X}, "
                          + $"the party at location {*PartyLocation})");
            // A mission's Rendezvous flag lasts its battle, as the next battle's start clears it in ReMixed; the
            // remaster's battles start from here (DeathCounter.RendezvousFlag).
            if (Nemesis.GetVariable(DeathCounter.RendezvousFlag) != 0)
            {
                Nemesis.SetVariable(DeathCounter.RendezvousFlag, 0, 0);
                _log.Info("remixed scenarios: the Rendezvous flag (0xD9) cleared on the world map");
            }
            var noted = Nemesis.GetVariable(0x3E);
            if (noted != NoteHeld)
                ClearStaleNote(noted);
#if DEVELOPER_MENU
            // A private build's Configure Mod actions wait until the save and world map are actually in play.
            var developerScenario = DeveloperMenu.ApplyPendingWorldMap(PartyLocation);
            if (developerScenario >= 0)
            {
                _watch = WatchLines;
                Begin(developerScenario);
                return;
            }
#endif
            var depth = *WorldDepth;
            var state = depth > 0 ? WorldStates[depth - 1] : -1;
            if (PendingDispatchScreen != 0 && depth == 2 && state == TavernMenuState && (*_flags & 4) == 0)
            {
                var screen = PendingDispatchScreen;
                PendingDispatchScreen = 0;
                TavernStorage.OpenDispatch(screen);
                return;
            }
            // Keep a harness request queued while the world-map UI is opening or closing. Exchanging it before the
            // flag check made a request sent during that short transition disappear instead of starting next frame.
            var asked = (*_flags & 4) == 0 ? Interlocked.Exchange(ref Requested, 0) : 0;
            if (asked != 0)
            {
                _log.Info($"remixed scenarios: the autopilot starts scenario 0x{asked:X}");
                var note = Interlocked.Exchange(ref RequestedNote, 0);
                if (note != 0)
                {
                    Nemesis.SetVariable(0x3E, note, 0);
                    NoteHeld = note;
                    _log.Info($"remixed scenarios: the autopilot notes event 0x{note:X}'s scene as seen (var 0x3E)");
                }
                _watch = WatchLines;
                Begin(asked);
                return;
            }
            if ((newly & Start) != 0 && state == RumorListState)
            {
                var at = PostingAt();
                // The Dispatch Missions posting only tells of it: it opens from the Tavern's menu, below Errands.
                if (at >= 0 && Postings[at].Scenario == TavernStorage.DispatchScenario)
                    return;
                if (at >= 0 && at != Accepted)
                {
                    Accepted = at;
                    _log.Info($"remixed scenarios: Start on the posting of mission scenario 0x{Postings[at].Scenario:X} "
                              + $"(rumor {Postings[at].Rumor}): accepted, to start on the world map");
                }
                return;
            }
            // The map itself, not one of its towns' or menus' screens, with its buttons on (the world's flag 4 holds
            // them off, as while a screen opens or closes).
            var onMap = depth == 1 && state == MapState && (*_flags & 4) == 0;
            var accepted = Accepted;
            if (accepted >= 0 && accepted < Postings.Count && onMap)
            {
                Accepted = -1;
                _watch = WatchLines;
                Begin(Postings[accepted].Scenario);
                return;
            }
            if ((pressed & Start) != 0 && onMap && TestMission(out var scenario))
            {
                *_pressed &= ~Start;
                _log.Info($"remixed scenarios: Start starts scenario 0x{scenario:X} (test mode: the first of ReMixed's missions)");
                _watch = WatchLines;
                Begin(scenario);
            }
        }
        catch (Exception e)
        {
            _log.Error($"remixed scenarios: {e.Message}");
        }
    }

    /// <summary>The breadcrumbs of each file read while one of ReMixed's scenarios starts (from <see cref="PackFiles"/>,
    /// before the read and after it, with the mod's bytes given).</summary>
    private static void WatchRead(uint file, int number, int size, nint buffer, long result, bool after)
    {
        if (!after)
        {
            if (_watch > 0)
            {
                _watch--;
                _log.Info($"remixed scenarios: reading file 0x{file:X} ({number}, 0x{size:X} bytes)");
            }
            return;
        }
        if (_watch > 0)
        {
            _log.Info($"remixed scenarios: read file 0x{file:X}: {result}");
            if (file is >= 0xE0 and <= 0xE3)
            {
                // The battle-data file just read: the units of each entry the added scenarios use in it.
                foreach (var entry in EntriesUsed.Where(e => e >> 7 == file - 0xE0))
                {
                    var data = (byte*)buffer + (entry & 0x7F) * 0x280;
                    _log.Info($"remixed scenarios: file 0x{file:X} entry 0x{entry:X}: " + string.Join(" ",
                        Enumerable.Range(0, 16).Where(u => data[u * 40] != 0).Select(u => $"{data[u * 40]:X2}/{data[u * 40 + 10]:X2}")));
                }
            }
        }
    }

    [UnmanagedCallersOnly]
    private static void LoadEvent(int number)
    {
        if (_watch > 0)
        {
            _watch--;
            _log.Info($"remixed scenarios: loading event {number}");
        }
        GameLoadEvent(number);
        if (_watch > 0)
            _log.Info($"remixed scenarios: loaded event {number}");
    }

    /// <summary>Starts one of ReMixed's scenarios from the world map as the world map starts its story events (0x247E10:
    /// var 0x27, then its state 0x23, which leaves the map with the world's loop returning 0, a story scenario). Not
    /// through the replay screen's start (0x2D28C8): that marks the run as a replay (var 0x1FC, the world's flag
    /// 0x40000, its loop returning 2), and a battle started so went to a battle's preparation when its closing sent it
    /// back to the world map (0x80). The conditions' type 25 action (0x3443FC) only sets the globals this reads.</summary>
    private static void Begin(int scenario)
    {
        var depth = *WorldDepth;
        _log.Info($"remixed scenarios: scenario 0x{scenario:X} starts as a story event (the world map's state "
                  + $"{(depth > 0 ? WorldStates[depth - 1] : -1)}, depth {depth})");
        WorldEvent[0] = scenario;
        *WorldMode = WorldStartMode;
        WorldStart(0);
    }

    /// <summary>The bar's check of a rumor (which it lists): one of ReMixed's postings once its mission is unlocked
    /// (<see cref="Unlocked"/>), an ordinary conditional rumor while all of its conditions hold, or one of the game's
    /// own rumors as the game decides; under the Tavern's Missions only the postings, under its Rumors only the rest
    /// (<see cref="RumorFilter"/>). Dispatch Missions' posting is never listed: the Tavern has its own button.</summary>
    [UnmanagedCallersOnly]
    internal static byte RumorShown(int rumor, int location)
    {
        var filter = RumorFilter;
        try
        {
            foreach (var p in ConditionalRumors)
                if (rumor == p.Rumor)
                    return (byte)(filter != MissionsOnly && ReadVariable(StoryProgress) >= p.Progress
                                  && ReadVariable(p.Required) != 0 && ReadVariable(p.Clear1) == 0
                                  && ReadVariable(p.Clear2) == 0 ? 1 : 0);
            foreach (var p in Postings)
                if (rumor == p.Rumor)
                    return (byte)(filter != RumorsOnly && p.Scenario != TavernStorage.DispatchScenario
                                  && Unlocked(p) ? 1 : 0);
            if (filter == MissionsOnly)
                return 0;
        }
        catch (Exception e)
        {
            _log.Error($"remixed scenarios: {e.Message}");
        }
        return GameRumorShown(rumor, location);
    }

    /// <summary>The bar's list events: the game's, then the rumor list they name, kept for <see cref="PostingAt"/> (its
    /// events come as the tavern opens and as the list does: 65537, then 50 and 51).</summary>
    [UnmanagedCallersOnly]
    private static void BarListEvent(nint screen, nint node, int kind, int entry, int extra)
    {
        GameBarListEvent(screen, node, kind, entry, extra);
        try
        {
            var rumors = *(nint*)(screen + 0x70);
            if (rumors != 0 && node != 0 && *(int*)(rumors + 0x20) == *(int*)(node + 0x20))
            {
                BarScreen = screen;
                RumorList = rumors;
            }
        }
        catch (Exception e)
        {
            _log.Error($"remixed scenarios: {e.Message}");
        }
    }

    /// <summary>The posting under the rumor list's cursor (its index in <see cref="Postings"/>), or -1: read only while
    /// the list is the world map's top state (<see cref="Buttons"/>), and checked readable first, as the list is freed
    /// when the tavern closes.</summary>
    internal static int PostingAt()
    {
        var list = RumorList;
        if (list == 0 || !Harness.Readable(list, ListEntriesAt + 16))
            return -1;
        var cursor = *(int*)(list + ListCursorAt);
        var begin = *(nint*)(list + ListEntriesAt);
        var end = *(nint*)(list + ListEntriesAt + 8);
        var count = (end - begin) / ListEntryBytes;
        if (begin == 0 || end < begin || count > 0x100 || cursor < 0 || cursor >= count
            || !Harness.Readable(begin + cursor * ListEntryBytes, 4))
            return -1;
        var rumor = *(int*)(begin + cursor * ListEntryBytes);
        return Postings.FindIndex(p => p.Rumor == rumor);
    }

    /// <summary>The event variables the postings read: the game's getter (the offline check gives its own).</summary>
    internal static Func<int, int> ReadVariable = id => Nemesis.GetVariable(id);

    /// <summary>A mission ReMixed's story has unlocked by now: the story progress reached (var 0x6E, which side chapter
    /// 1-A counts from 0 again), or the flag ReMixed's own event sets for it.</summary>
    internal static bool Unlocked((int Rumor, int Scenario, int Progress, int Unlock) p) =>
        (p.Progress != NoProgress && ReadVariable(StoryProgress) >= p.Progress)
        || (p.Unlock != 0 && ReadVariable(p.Unlock) != 0);

    /// <summary>In the self-playing test mode (RecordAndAutoBattle), the first of ReMixed's missions, wherever the player
    /// is. Not in the plain Record mode: a player recording a playthrough pressed Start for the world map's menu just
    /// after the opening battle and got the fete (seen on the Deck, 2026-11-06).</summary>
    private static bool TestMission(out int scenario)
    {
        scenario = Missions.Count > 0 ? Missions.Values.Min() : 0;
        return Options.Current.TestMode == Options.TestModeRule.RecordAndAutoBattle && Missions.Count > 0;
    }

    private static int Id(byte* record) => *(ushort*)record;

    /// <summary>The condition walker (0x2391C4), as the game's: while the game has an event waiting (its two flags), var
    /// 0x27 = that event and yes; otherwise its walk over the 16 sets, with var 1 set to the set that fires in one of
    /// ReMixed's scenarios (see the remarks).</summary>
    [UnmanagedCallersOnly]
    private static long Walk()
    {
        var request = Interlocked.Exchange(ref _requestedEventPhase, 0);
        if (request != 0)
        {
            var requested = request >> 4;
            var phase = request & 0xF;
            Nemesis.SetVariable(1, phase, 0);
            Nemesis.SetVariable(0x27, requested, 0);
            Rerun();
            _log.Info($"remixed scenarios: harness runs event 0x{requested:X} phase {phase} through the battle walker");
            return 1;
        }
        if (*_walkBusy != 0 && *_walkBusy2 != 0)
        {
            Nemesis.SetVariable(0x27, *_walkBusy2, 0);
            Rerun();
            return 1;
        }
        var remixed = false;
        try
        {
            remixed = Presets.ContainsKey(Nemesis.GetVariable(0x27));
        }
        catch (Exception)
        {
        }
        var fired = WalkSets((short*)_sets, (short*)_records, _recordLengths, _check, *_walkState, remixed, remixed ? _battleUnits : null);
        if (fired != 0)
            Rerun();
        return fired;
    }

    /// <summary>Queues a phase for the next battle condition walk. Available only through <see cref="Harness"/>, whose
    /// pipe exists only when the private autopilot marker file is installed beside the DLL.</summary>
    internal static string RequestEventPhase(int number, int phase)
    {
        if (number <= 0)
            return "error: the event number must be positive";
        if ((uint)phase >= 16)
            return "error: the phase must be from 0 through 15";
        if (!Messages.ContainsKey(number))
            return $"error: event 0x{number:X} isn't one of ReMixed's generated events";
        if (RunningEvent == null || *RunningEvent < 0)
            return "error: no battle event is loaded; start the scenario and commence its formation first";
        if (Interlocked.CompareExchange(ref _requestedEventPhase, number << 4 | phase, 0) != 0)
            return "error: another event phase is already queued";
        return $"event 0x{number:X} phase {phase} queued for the next battle condition walk";
    }

    private static void Rerun()
    {
        try
        {
            var next = Nemesis.GetVariable(0x27);
            if (ReloadOnRerun(next, RunningEvent) && RerunNoted.Add(next))
                _log.Info($"remixed scenarios: event {next} runs again, loaded afresh for its text");
        }
        catch (Exception e)
        {
            _log.Error($"remixed scenarios: an event run again: {e.Message}");
        }
    }

    private static readonly HashSet<int> RerunNoted = [];

    /// <summary>
    /// An event of ReMixed's (one with lines, <see cref="Messages"/>) that a set runs while it's the one loaded: the
    /// loaded number (<paramref name="loaded"/>, [0x782A94]) is cleared, so the runner's loader (0x23F37C, which loads an
    /// event only when var 0x27 isn't that number) loads it afresh, its text with it. An event's END unloads the scenario
    /// text (0x21F14C → 0x1DB06C: the text manager's table, +0x28, to 0) and keeps the number, so the same event run
    /// again showed its boxes with no line in them (a face, or nothing), and run from an END its loader said "nothing
    /// new" and the scenes ended there (seen on the PC: Algus's first line in the Argath campaign's 0x006, Luso's lines
    /// after his battle, 0x112's post-battle run run again). ReMixed's battles run one event for all their phases, picked
    /// by var 1 (<see cref="WalkSets"/>); the remaster's run an event per phase. True if it was cleared.
    /// </summary>
    internal static bool ReloadOnRerun(int next, int* loaded)
    {
        if (loaded == null || next != *loaded || !Messages.ContainsKey(next))
            return false;
        *loaded = -1;
        return true;
    }

    /// <summary>The walk itself: each set's records through <paramref name="check"/> while it says 1 (go on); 2 means the
    /// set fired, and with <paramref name="phase"/> var 1 gets its index. With <paramref name="units"/> (the battle's, in
    /// ReMixed's scenarios) ReMixed's own condition types are its own (<see cref="RemixedCheck"/>), its records are
    /// stepped by its lengths (<see cref="RemixedRecordLengths"/>) and the sets are walked in <see cref="WalkOrder"/>.</summary>
    internal static long WalkSets(short* sets, short* records, sbyte* lengths, delegate* unmanaged<int, int, int, int, int, long> check,
                                  nint state, bool phase, byte* units = null)
    {
        if (state != 0)
            *(byte*)(state + 0x6C) = 0;
        var order = stackalloc int[16];
        var count = WalkOrder(sets, records, units != null, order);
        for (var k = 0; k < count; k++)
        {
            var set = order[k];
            int at = sets[set];
            if (at < 0)
                at++;
            var record = records + (at >> 1);
            long result;
            do
            {
                var type = record[0];
                result = units != null ? RemixedCheck(units, record) : -1;
                if (result < 0)
                    result = check(type, units != null ? GameUnitId(units, type, record[1]) : record[1], record[2], record[3], record[4]);
                record += (units != null && (uint)type < (uint)RemixedRecordLengths.Length ? RemixedRecordLengths[type] : lengths[type]) + 1;
            }
            while (result == 1);
            if (result == 2)
            {
                if (phase && Nemesis.SetVariable != null)
                {
                    Nemesis.SetVariable(1, set, 0);
                    _log?.Info($"remixed scenarios: condition set {set} fires (its event's block {set})");
                }
                return 1;
            }
        }
        return 0;
    }

    /// <summary>The order the sets are walked in: their numbers into <paramref name="order"/> (16 at most), how many
    /// returned. As they stand, but in ReMixed's (<paramref name="remixed"/>) a set that runs the Nemesis's return
    /// (<see cref="IsStartSet"/>) comes right after the opening's, set 0, wherever it is. ReMixed's own presets have it
    /// there already (the Preparation Menu's set, second in all 108); the build gives the battles that hand on to the menu
    /// from their opening that set as their last, so that their own sets keep the numbers their events pick blocks by
    /// (var 1), and it still runs before any of them, as ReMixed's menu did: 0x11D clears flags 0x80–0x8E, where those
    /// sets' events keep their state.</summary>
    internal static int WalkOrder(short* sets, short* records, bool remixed, int* order)
    {
        var count = 1;
        while (count < 16 && sets[count] != 0)
            count++;
        var start = -1;
        for (var set = 2; remixed && set < count && start < 0; set++)
        {
            int at = sets[set];
            if (at < 0)
                at++;
            if (IsStartSet(records + (at >> 1)))
                start = set;
        }
        var n = 0;
        order[n++] = 0;
        if (start > 0)
            order[n++] = start;
        for (var set = 1; set < count; set++)
            if (set != start)
                order[n++] = set;
        return n;
    }

    /// <summary>A set of the one condition "flag 0x8F = 0" (type 1) that runs an event (0x19): the Nemesis's return as
    /// the build gives it to ReMixed's battles.</summary>
    internal static bool IsStartSet(short* record) =>
        record[0] == 1 && record[1] == StartFlag && record[2] == 0 && record[3] == 0x19;

    /// <summary>ReMixed's lengths of its condition records (BATTLE.BIN 0x801696BC: the halfwords after the type), types
    /// 0x00–0x27: vanilla's, with its own types' (0x1C, 0x21–0x27) and none for 0x17. The remaster's table (0x672140) has
    /// its own types' there, and 0 for 0x26, which ReMixed's presets 0xF7 (0x0F8's battle) and 0x103 use: their sets were
    /// read out of step.</summary>
    internal static ReadOnlySpan<byte> RemixedRecordLengths =>
        [0, 2, 2, 2, 1, 2, 2, 2, 2, 2, 2, 1, 1, 1, 2, 2, 3, 3, 2, 2, 2, 2, 0, 0, 4, 2, 8, 1, 2, 0, 0, 0, 0, 7, 7, 3, 3, 4, 2, 2];

    /// <summary>ReMixed's own condition types, which the remaster's checker doesn't have or has as others (ReMixed's
    /// BATTLE.BIN 0x80142A78 and 0x8015996C): 1 if the record holds (the set goes on), 0 if not, −1 for the other types
    /// (the game's checker). <paramref name="units"/> are the battle's (21 of 0x200 bytes).
    /// <list type="bullet">
    /// <item>0x14, 0x15 (unit, status): the unit (<see cref="RemixedUnit"/>) has, or hasn't, the status, numbered through
    /// its five current-status bytes (+0x61–0x65, the PS1's +0x58) from the first byte's 0x80 bit (1 Crystal, 2 Dead, 15
    /// Treasure). No such unit: no. 0x0F0's rescue waits on them (Hirame revived, or too late).</item>
    /// <item>0x25 (team, x, y, level): a unit of that team (+0x1EE's bits 4–5, the PS1's +0x1BA) on the tile, or for a
    /// team of 4 or more, a unit of any team but the player's. Vanilla's, which the remaster keeps, wants the unit whose
    /// +4 is the first value (Treasure Hunt's goal tile).</item>
    /// <item>0x26, 0x27 (unit, n): the unit has finished more, or fewer, than n turns this battle (ReMixed's count at
    /// 0x80150284, <see cref="Movement.TurnsEndedBy"/>; its 0x26 also compares the unit's address with n first, which
    /// never holds).</item>
    /// </list>
    /// The others ReMixed adds (0x1C, 0x21–0x24, 0x27) no preset of its uses.</summary>
    internal static long RemixedCheck(byte* units, short* record)
    {
        switch (record[0])
        {
            // The remaster can leave an absent roster-backed actor's battle id in an otherwise empty unit slot. Its
            // stock HP/status checker finds that id and reads the zeroed record as HP 0, so Araguay's missing Boco
            // (battle id 0x80) immediately satisfies its loss condition. A condition about an actor that does not
            // exist is false, as ReMixed's own lookup is for the first actual unit.
            case >= 4 and <= 8:
            {
                var id = record[1];
                if (RemixedUnit(units, id) >= 0)
                    return -1;
                if (UnitAliases.TryGetValue(id, out var alias) && AliasUnit(units, alias) >= 0)
                    return -1;
                return 0;
            }
            case 0x14 or 0x15:
            {
                var k = RemixedUnit(units, record[1]);
                if (k < 0)
                    return 0;
                int bit = record[2];
                var has = bit is >= 0 and < 40
                          && (units[k * Battle.UnitSize + UnitStatuses + (bit >> 3)] & (0x80 >> (bit & 7))) != 0;
                return has == (record[0] == 0x14) ? 1 : 0;
            }
            case 0x25:
                for (var k = 0; k < Battle.UnitCount; k++)
                {
                    var unit = units + k * Battle.UnitSize;
                    if (unit[1] == 0xFF || unit[UnitX] != record[2] || unit[UnitY] != record[3] || unit[UnitY + 1] >> 7 != record[4])
                        continue;
                    var team = (unit[UnitBaseTeam] & 0x30) >> 4;
                    if (record[1] < 4 ? team == record[1] : team > 0)
                        return 1;
                }
                return 0;
            case 0x26 or 0x27:
            {
                var k = RemixedUnit(units, record[1]);
                if (k < 0)
                    return 0;
                var turns = Movement.TurnsEndedBy(k);
                return (record[0] == 0x26 ? turns > record[2] : turns < record[2]) ? 1 : 0;
            }
            // ReMixed's plain-victory condition is "no live enemy unit remains". The remaster's checker misses the
            // first Brave Story round's event-made Death Knights/monsters, so that round never advances and eventually
            // lets an actor target a unit with no sprite. Check the battle array directly, including all 21 slots.
            case 0x16:
                var sawEnemy = false;
                for (var k = 0; k < Battle.UnitCount; k++)
                {
                    var unit = units + k * Battle.UnitSize;
                    if (unit[1] == 0xFF || (unit[UnitBaseTeam] & 0x30) == 0)
                        continue;
                    sawEnemy = true;
                    var defeated = (unit[UnitStatuses] & 0x60) != 0 || (unit[UnitStatuses + 1] & 0x01) != 0;
                    if (!defeated)
                    {
                        _victorySawEnemy = true;
                        return 0;
                    }
                }
                _victorySawEnemy |= sawEnemy;
                return _victorySawEnemy ? 1 : 0;
            default:
                return -1;
        }
    }

    /// <summary>A battle unit's current statuses (the PS1's +0x58), its tile (+0x4F x, +0x50 y, +0x51's top bit the level;
    /// the PS1's +0x47) and its base team byte (+0x1EE, the PS1's +0x1BA).</summary>
    private const int UnitStatuses = 0x61, UnitX = 0x4F, UnitY = 0x50, UnitBaseTeam = 0x1EE;

    /// <summary>A new scenario setup cannot inherit an earlier battle's observed enemies.</summary>
    internal static void ResetVictoryObservation() => _victorySawEnemy = false;

    /// <summary>ReMixed's lookup of a condition's unit (BATTLE.BIN 0x801585B8): the first battle unit whose battle id
    /// (+0x191, the PS1's +0x161) or sprite set (+0) is <paramref name="id"/>; its index, or −1. An id of
    /// <see cref="UnitAliases"/> that no unit has is the formation's unit of the port's number (<see cref="AliasUnit"/>).</summary>
    internal static int RemixedUnit(byte* units, int id)
    {
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var unit = units + k * Battle.UnitSize;
            if (unit[0] != 0 && (unit[0x191] == id || unit[0] == id))
                return k;
        }
        return UnitAliases.TryGetValue(id, out var alias) ? AliasUnit(units, alias) : -1;
    }

    /// <summary>ReMixed's sprite sets that the port gives another number (the build's REMIXED_SPRITE_SETS), as its
    /// conditions name units: side chapter 1-A's young Agrias, 0x22 (the remaster's Mustadio), is the remaster's Agrias,
    /// 0x1E. Its battles name the player's Agrias 0x22: 0x0F1's dialogue on her turn (preset 0xF0, type 0xB), 0x0F5's
    /// failure when she falls (0xF4, type 6: HP at most 0), 0x0EE's and 0x0F4's lines while she stands (0xED and 0xF3,
    /// type 5: HP at least 1).</summary>
    internal static readonly Dictionary<int, int> UnitAliases = new() { [0x22] = 0x1E };

    /// <summary>The formation's unit of sprite set <paramref name="sprites"/>: a roster unit (+1, its roster slot, not
    /// 0xFF as the battle data's units have) with a formation's battle id (0x78–0x7C, as the remaster's lookup 0x239144
    /// wants for a sprite set); not a scene's actor of the same sprite set (1-A's Agrias, battle id 0x90) or a battle
    /// data's unit of it (0x0F1's battle has one, battle id 0x7B, on the PS1). Its index, or −1.</summary>
    internal static int AliasUnit(byte* units, int sprites)
    {
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var unit = units + k * Battle.UnitSize;
            if (unit[1] != 0xFF && unit[0] == sprites && unit[0x191] is >= 0x78 and <= 0x7C)
                return k;
        }
        return -1;
    }

    /// <summary>The unit a record of the game's checker names, for one of ReMixed's scenarios: for a type that names a
    /// unit (4–8: there, or its HP at least, at most, its HP % at least, at most; 0xB: the acting unit) an id of
    /// <see cref="UnitAliases"/> that no unit has becomes the battle id of the formation's unit it stands for, which the
    /// checker finds first (by battle id, 0x27260C; the first unit with it, and battle ids can repeat: on the PS1 a
    /// battle data's unit of 0x0F1's has the young Agrias's) and which type 0xB compares with the acting unit's first.</summary>
    internal static int GameUnitId(byte* units, int type, int id)
    {
        if (type is < 4 or > 8 and not 0xB || !UnitAliases.TryGetValue(id, out var alias))
            return id;
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var unit = units + k * Battle.UnitSize;
            if (unit[0x191] == id || unit[0] == id)
                return id;
        }
        var stands = AliasUnit(units, alias);
        return stands >= 0 ? units[stands * Battle.UnitSize + 0x191] : id;
    }

    /// <summary>An instruction of a ReMixed event about to run: a MESSAGE (0x10, its line's Id a u32 at +3) or 0x51 (at
    /// +2), in the remaster's layout as the build converts them. The build gives each its line's Id; where ReMixed's
    /// code writes a message number n in as it runs (0xE6, into the Id's low half: 0x40000 + n), this gives it the
    /// line's Id. One with its line's Id is past the count, so it's left alone.</summary>
    internal static void MoveMessage(byte* instruction)
    {
        var at = instruction[0] switch { 0x10 => MessageAt, 0x51 => ChangeAt, _ => 0 };
        if (at == 0 || RunningEvent == null || !Messages.TryGetValue(*RunningEvent, out var lines))
            return;
        var message = (long)*(uint*)(instruction + at) - LineIdBase;
        if (message < 1 || message > lines.Count)
            return;
        *(uint*)(instruction + at) = (uint)(LineIdBase + lines.Base + message);
        if (_log != null && MovedNoted.Add(*RunningEvent * 0x10000 + (int)message))
            _log.Info($"remixed scenarios: event {*RunningEvent}'s message {message} shown as line {LineIdBase + lines.Base + message}");
    }

    /// <summary>The file's records into the tables: u16 count, the scenario records; u16 count, the formation records;
    /// u16 count, (u16 location, u16 scenario) pairs; u16 count, (u16 event, u16 message base, u16 messages); u16 count,
    /// (u16 scenario, u16 its hand-off, u8 mode) for <see cref="Offers"/>. A scenario id the table has already is replaced.</summary>
    internal static (int Scenarios, int Formations) Add(byte[] data)
    {
        var at = 0;
        int Count() { var n = BitConverter.ToUInt16(data, at); at += 2; return n; }
        var scenarios = Count();
        for (var k = 0; k < scenarios; k++, at += ScenarioSize)
        {
            var record = data.AsSpan(at, ScenarioSize).ToArray();
            Presets[BitConverter.ToUInt16(record)] = BitConverter.ToUInt16(record, ConditionsKey);
            EntriesUsed.Add(BitConverter.ToUInt16(record, 7));
            BitConverter.TryWriteBytes(record.AsSpan(ConditionsKey), (ushort)SafeConditions);
            PutScenario(record);
        }
        var formations = Count();
        for (var k = 0; k < formations; k++, at += FormationSize)
        {
            var id = BitConverter.ToUInt16(data, at + FormationId);
            if (id >= FormationCount)
                throw new InvalidDataException($"formation 0x{id:X} is past the table");
            data.AsSpan(at, FormationSize).CopyTo(new Span<byte>(Formations + id * FormationSize, FormationSize));
        }
        if (at < data.Length)
        {
            var missions = Count();
            for (var k = 0; k < missions; k++, at += 4)
                Missions[BitConverter.ToUInt16(data, at)] = BitConverter.ToUInt16(data, at + 2);
        }
        if (at < data.Length)
        {
            var events = Count();
            for (var k = 0; k < events; k++, at += 6)
                Messages[BitConverter.ToUInt16(data, at)] = (BitConverter.ToUInt16(data, at + 2), BitConverter.ToUInt16(data, at + 4));
        }
        if (at < data.Length)
        {
            var offers = Count();
            for (var k = 0; k < offers; k++, at += 5)
                Offers.Add((BitConverter.ToUInt16(data, at), BitConverter.ToUInt16(data, at + 2), data[at + 4]));
        }
        if (at < data.Length)
        {
            var own = Count();
            for (var k = 0; k < own; k++, at += 2 + EntryBytes)
            {
                var records = (byte*)NativeMemory.Alloc(EntryBytes);
                data.AsSpan(at + 2, EntryBytes).CopyTo(new Span<byte>(records, EntryBytes));
                OwnEntries[BitConverter.ToUInt16(data, at)] = (nint)records;
            }
        }
        if (at < data.Length)
        {
            var named = Count();
            for (var k = 0; k < named; k++)
            {
                int from = BitConverter.ToUInt16(data, at), to = BitConverter.ToUInt16(data, at + 2), mode = data[at + 4];
                var names = new string[data[at + 5]];
                at += 6;
                for (var n = 0; n < names.Length; n++)
                {
                    names[n] = Encoding.UTF8.GetString(data, at + 1, data[at]);
                    at += 1 + data[at];
                }
                NamedHandOffs.Add((from, to, mode, names));
            }
        }
        if (at < data.Length)
        {
            var postings = Count();
            for (var k = 0; k < postings; k++, at += 8)
                Postings.Add((BitConverter.ToUInt16(data, at), BitConverter.ToUInt16(data, at + 2),
                              BitConverter.ToUInt16(data, at + 4), BitConverter.ToUInt16(data, at + 6)));
        }
        if (at < data.Length)
        {
            var battles = Count();
            for (var k = 0; k < battles; k++)
            {
                int scenario = BitConverter.ToUInt16(data, at), first = BitConverter.ToUInt16(data, at + 2),
                    second = BitConverter.ToUInt16(data, at + 4);
                var events = new int[data[at + 6]];
                at += 7;
                for (var n = 0; n < events.Length; n++, at += 2)
                    events[n] = BitConverter.ToUInt16(data, at);
                MenuBattles[scenario] = ((ushort)first, (ushort)second, events);
            }
        }
        if (at < data.Length)
        {
            var rumors = Count();
            for (var k = 0; k < rumors; k++, at += 10)
                ConditionalRumors.Add((BitConverter.ToUInt16(data, at), BitConverter.ToUInt16(data, at + 2),
                                       BitConverter.ToUInt16(data, at + 4), BitConverter.ToUInt16(data, at + 6),
                                       BitConverter.ToUInt16(data, at + 8)));
        }
        return (scenarios, formations);
    }

    /// <summary>
    /// ReMixed's battles played after a scene on their own map (the build's MENU_BATTLES): the mod's scenario -> the
    /// formations of its battle and the events whose scene hands on to it. ReMixed's scene hands on to its Preparation
    /// Menu, whose Start Battle sets the battle up again and runs the scenario's event once more, with var 0x3A = the event
    /// the scene noted in var 0x3E: the event's opening keeps var 0x3A in var 0x18 and, finding it var 0x27, skips the
    /// scene (GOTO ff), and the battle begins. The mod's scene hands on to its scenario itself, whose record has no
    /// formations (so the scene runs without one, as ReMixed's does); <see cref="PrepareMenuBattle"/> sets that second run
    /// up as the menu does.
    /// </summary>
    internal static readonly Dictionary<int, (ushort First, ushort Second, int[] Events)> MenuBattles = [];

    /// <summary>Before the setup reads the scenario's record (<see cref="Setup"/>): for a scenario of
    /// <see cref="MenuBattles"/>, its second run (var 0x3E names one of its events: its scene just ran and handed on to
    /// it) gets its battle's formations in the record and var 0x3A = that event, and var 0x3E is cleared; any other run
    /// (from the world map) gets none, so its scene runs first.</summary>
    internal static void PrepareMenuBattle(int scenario)
    {
        if (!MenuBattles.TryGetValue(scenario, out var battle))
            return;
        var record = FindRecord(scenario);
        if (record == null)
            return;
        var noted = Nemesis.GetVariable(0x3E);
        var second = Array.IndexOf(battle.Events, noted) >= 0;
        var group = SecondGroup(battle.Second, SecondGroupsOn ? Nemesis.GetVariable(0x36) : 0);
        *(ushort*)(record + 9) = second ? battle.First : (ushort)0;
        *(ushort*)(record + 0xB) = second ? group : (ushort)0;
        if (second)
        {
            Nemesis.SetVariable(0x3A, noted, 0);
            Nemesis.SetVariable(0x3E, 0, 0);
        }
        _log?.Info($"remixed scenarios: scenario 0x{scenario:X} " + (second
            ? $"sets its battle up after event 0x{noted:X}'s scene (formations 0x{battle.First:X}, 0x{group:X})"
            : "plays its scene first, without a formation"));
    }

    /// <summary>A scene of <see cref="MenuBattles"/> that noted itself (var 0x3E, <paramref name="noted"/>) and ended
    /// without handing on to its battle (its question answered no: on the PS1 the note only counts once the menu has set
    /// var 0x3A from it) leaves the note behind; with the world map running, the next start of that scenario is its scene
    /// again, not the battle's run (seen on the PC: 0x0F0's rescue declined, then started again, went to its battle
    /// without its scene). A scene's hand-off goes on to its battle's run without the world map in between.</summary>
    internal static void ClearStaleNote(int noted)
    {
        if (noted == 0 || !MenuBattles.Values.Any(b => Array.IndexOf(b.Events, noted) >= 0))
            return;
        Nemesis.SetVariable(0x3E, 0, 0);
        _log?.Info($"remixed scenarios: event 0x{noted:X}'s scene note (var 0x3E) cleared on the world map: its battle "
                   + "wasn't set up after it");
    }

    /// <summary>Whether a marked second group is put in when the scene's run set it: off, as with two groups put in for
    /// the battle's run the remaster's formation screen showed both and took units in both, but Space never commenced
    /// (0x0F0's, seen on the PC 2026-11-12; with one group it commences). So 1-A's battles deploy one group for now, where
    /// ReMixed (its options as a new game has them) deploys a second (0x0F1's, seen on the PS1).</summary>
    internal static bool SecondGroupsOn;

    /// <summary>The build's mark on a battle's second group that ReMixed's code sets only under an IF (0x0EE's with Bryn
    /// and Lorne along, 0x0F1's): the build's MENU_SECOND_GROUP.</summary>
    internal const ushort SecondGroupMark = 0x8000;

    /// <summary>A battle's second group as its scene's run left it: a marked one only when ReMixed's code set its second
    /// group's variable (var 0x36, <paramref name="setByCode"/>), as its Preparation Menu reads it.</summary>
    internal static ushort SecondGroup(ushort second, int setByCode) =>
        (second & SecondGroupMark) == 0 ? second : setByCode != 0 ? (ushort)(second & ~SecondGroupMark) : (ushort)0;

    /// <summary>A scenario's record in the remaster's table, or null.</summary>
    internal static byte* FindRecord(int scenario)
    {
        for (var k = 0; k < ScenarioRoom && Id(Scenarios + k * ScenarioSize) != 0; k++)
        {
            var record = Scenarios + k * ScenarioSize;
            if (Id(record) == scenario)
                return record;
        }
        return null;
    }

    /// <summary>The battle entry's records: the game's, or for a scenario of <see cref="OwnEntries"/> (var 0x27, the
    /// scenario being set up) its own.</summary>
    [UnmanagedCallersOnly]
    private static byte* EntryRecords()
    {
        var records = GameEntryRecords();
        try
        {
            var scenario = Nemesis.GetVariable(0x27);
            if (records != null && OwnEntries.TryGetValue(scenario, out var own))
            {
                if (OwnEntriesNoted.Add(scenario))
                    _log?.Info($"remixed scenarios: scenario 0x{scenario:X} takes its units from the mod's file");
                return (byte*)own;
            }
        }
        catch (Exception e)
        {
            _log?.Error($"remixed scenarios: the battle entry's records: {e.Message}");
        }
        return records;
    }

    /// <summary>A roster slot's name (+0xDC, UTF-8 up to its first 0), trimmed.</summary>
    internal static string RosterName(int slot)
    {
        var name = _roster + slot * UnitTables.RosterSize + RosterNameAt;
        var length = 0;
        while (length < NameBytes && name[length] != 0)
            length++;
        return Encoding.UTF8.GetString(name, length).Trim();
    }

    /// <summary>A scenario's map-settings word from its record (the scenario it hands on to, the mode); null if the table
    /// hasn't it.</summary>
    internal static (int To, int Mode)? GetHandOff(int scenario)
    {
        for (var k = 0; k < ScenarioRoom && Id(Scenarios + k * ScenarioSize) != 0; k++)
        {
            var record = Scenarios + k * ScenarioSize;
            if (Id(record) == scenario)
                return (record[0x12] | record[0x13] << 8, record[0x14]);
        }
        return null;
    }

    /// <summary>A scenario's map-settings word, in its record (+0x12 the scenario's low byte, +0x13 its high bits, +0x14
    /// the mode: the setup builds the word as +0x12 + 0x400 × +0x13 | 0x100 × +0x14); false if it isn't in the table.</summary>
    internal static bool SetHandOff(int scenario, int to, int mode)
    {
        for (var k = 0; k < ScenarioRoom && Id(Scenarios + k * ScenarioSize) != 0; k++)
        {
            var record = Scenarios + k * ScenarioSize;
            if (Id(record) != scenario)
                continue;
            record[0x12] = (byte)to;
            record[0x13] = (byte)(to >> 8);
            record[0x14] = (byte)mode;
            // The table the world setup filled from the records is what the scenario's end reads: set as the scenario
            // starts (a hand-off by name), the record alone came too late (seen on the PC: scenario 2 went on to 3).
            if (RemixedEvents.MapSettings != null && scenario < 0x400)
                RemixedEvents.MapSettings[scenario] = (ushort)((to & 0xFF) + ((to >> 8) << 10) | mode << 8);
            return true;
        }
        return false;
    }

    private static void PutScenario(ReadOnlySpan<byte> record)
    {
        var id = BitConverter.ToUInt16(record);
        var k = 0;
        while (k < ScenarioRoom && Id(Scenarios + k * ScenarioSize) != 0 && Id(Scenarios + k * ScenarioSize) != id)
            k++;
        if (k >= ScenarioRoom - 1)
            throw new InvalidDataException("the scenario table is full");
        var end = Id(Scenarios + k * ScenarioSize) == 0;
        record.CopyTo(new Span<byte>(Scenarios + k * ScenarioSize, ScenarioSize));
        if (end)
            new Span<byte>(Scenarios + (k + 1) * ScenarioSize, ScenarioSize).Clear();
    }
}
