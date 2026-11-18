using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Dispatch Missions in the Tavern, in Errands' place (as ReMixed's replaces vanilla's errands), with the
/// remaster's native errand controls: a virtual catalog of Propositions, repeatable Jobs, Special Training and the
/// current Contest uses the native list, roster picker, away-unit locks, timers, cancellation/recall and report
/// screens. The remaster's 96 errands can no longer be started; ones already under way still come back.
/// </summary>
internal static unsafe class TavernStorage
{
    internal const uint Ps1Count = 0x80057CE8, Ps1Records = 0x80057EEC, Ps1States = 0x80057978;
    internal const int RecordBytes = 9, Records = 8, NativeRecords = 14, StatesBytes = 48;
    internal const int FirstState = 0x360, LastState = 0x3BF;
    internal const int NativeRows = 96, TrainingFirst = 89, TrainingLast = 95,
                       TrainingTextFirst = 96, TrainingTextLast = 102,
                       PropositionTextFirst = 103, PropositionTextLast = 194,
                       JobTextFirst = 195, JobTextLast = 198,
                       ContestTextFirst = 199, ContestTextLast = 210,
                       DispatchMenuFirst = 211, DispatchMenuLast = 221,
                       DispatchTextLast = DispatchMenuLast, ProfitRowBytes = 0x17;
    internal const int DispatchScenario = 0x355;
    /// <summary>The Tavern screen's states (its +0x98, set by <see cref="BarStateAt"/>): 0 the farewell, 1 its menu,
    /// 2 Rumors, 4 Errands, 5 Errands while the story keeps them locked. Each frame the world map's Tavern state
    /// (0x2D5210) asks the screen for a request (<see cref="BarRequestAt"/>: the first int it fills in) and acts on it:
    /// 0 leave, 2 open Rumors (0x36EE50, which also pushes the world map's Rumors state), 4 Errands, 5 the locked
    /// Errands' note; -1 is nothing. Its menu (0x19F498) requests 4 or 5 for its second button, by whether that
    /// button is enabled, and 2 for any other (seen in the PC game, 2026-11-18). Dispatch Missions replaces Errands
    /// there (tools/build_mod.py, TAVERN_MENU_ROW), as ReMixed's replaces vanilla's errands.</summary>
    internal const int MenuState = 1, ErrandsRequest = 4, LockedErrandsRequest = 5, NoRequest = -1;
    /// <summary>The Tavern menu's setup (0x19F330) asks the unlock table whether Errands are open (0xFF8A4, row
    /// 0xC94) and builds UIMenuButtonLeft row 3 if so, row 7 (Errands as a locked "???") if not: that call, which
    /// <see cref="OnTavernUnlock"/> answers with ReMixed's unlock of Dispatch Missions instead.</summary>
    internal const string TavernUnlockPattern = "BA 94 0C 00 00 E8 ?? ?? ?? ?? 8A C8 C7 44 24 20 17 00 00 00 F6 D9";
    internal const int TavernUnlockCallAt = 5;
    internal const string CatalogFileName = "dispatch-missions.json";
    private const byte DispatchRecord = 0x80;
    private const int NumericTemplateRow = 89;
    // ProfitMain's row renderer receives this object at +0x30, so its decompiled formatter fields
    // (+0x6F8/+0x6FC/+0x700) live at these offsets from the actual ProfitMain object.
    private const int ProfitCostTextOffset = 0x728, ProfitDayStartTextOffset = 0x72C,
                      ProfitDayEndTextOffset = 0x730;
    private const int EmptyUiText = 0x74C;
    /// <summary>The Errands screen's layout text nodes (vtable-checked, <see cref="LayoutNodeBytes"/> apart, from the
    /// one at its +0x420): the column heads "Days" and "Finder's Fee", and the "-" each row copies for a job with no
    /// days, are three of them (seen in the PC game, 2026-11-18, as the 14th, 15th and 8th). A node's UI text is at
    /// +0xF8, re-read when +0x13C is set; a row's nodes are copied from these as the list is shown (0x3F0E40).</summary>
    private const int ColumnHeadAnchor = 0x420, LayoutNodeBytes = 0x1C8, LayoutNodesScanned = 24,
                      NodeTextAt = 0xF8, NodeDirtyAt = 0x13C, DaysHeadText = 0x74F, FeeHeadText = 0x750,
                      NoDaysText = 0x75A, NoErrandsText = 0x758;
    private static readonly List<(nint Node, int Text)> ColumnHeads = [];
    private static nint _emptyListNode;

    internal const string DayChangePattern =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 30 BB 2E 00 00 00 8B CB E8";
    internal const string ErrandLoopPattern =
        "8A 15 ?? ?? ?? ?? 33 ED 45 33 FF 45 33 F6 8D 4D 20 84 D2 0F 84 ?? ?? ?? ?? 48 8D 1D ?? ?? ?? ?? 8A 4B FE F6 C1 04";
    internal const string CountDownsPattern = "41 BF 60 00 00 00 48 8D 15 ?? ?? ?? ?? 45 8B CF 44 0F B6 02";
    internal const string ErrandListPattern =
        "48 89 5C 24 10 48 89 6C 24 18 48 89 74 24 20 57 48 83 EC 30 4C 8B 05 ?? ?? ?? ?? 48 8B E9";
    internal const string NewInMenuPattern =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 20 33 DB 83 FA 05 0F 8F";
    internal const string ReportPattern =
        "48 89 5C 24 18 48 89 6C 24 20 48 89 4C 24 08 56 57 41 54 41 56 41 57 48 83 EC 20 48 63 41 2C 4C 8D 25";
    internal const string ProfitRowsPattern =
        "01 02 01 01 02 05 05 08 01 04 00 01 06 01 01 01 0D 01 01 04 0C 05 00";
    internal const string NumericProfitPattern =
        "44 8B 05 ?? ?? ?? ?? 4C 8D 1D ?? ?? ?? ?? 4C 8B D1 48 63 C2 48 6B C8 17";
    internal const string PreparePattern =
        "48 8B C4 48 89 58 08 48 89 70 10 48 89 78 18 55 41 54 41 55 41 56 41 57 48 8D A8 08 FC FF FF 48 81 EC D0 04 00 00";
    internal const string SendPattern =
        "40 53 48 83 EC 30 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 28 BB 2C 00 00 00 8B CB E8";
    internal const string TableRowPattern =
        "40 53 48 83 EC 20 8B DA 8B D1 48 8B 0D ?? ?? ?? ?? 48 85 C9 74 ?? E8 ?? ?? ?? ?? 4C 8B C0 " +
        "48 85 C0 74 ?? 8B 48 0C 83 E9 02 83 F9 01 77 ?? 48 8B C8 E8 ?? ?? ?? ?? 84 C0 75 ?? " +
        "8B D3 49 8B C8 E8";
    internal const string BarStatePattern =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 20 8B B1 98 00 00 00 48 8B F9 3B F2";
    internal const string BarRequestPattern =
        "48 89 5C 24 08 57 48 83 EC 20 48 8B FA 48 8B D9 8B 91 98 00 00 00 83 FA 0A 0F 8F";
    /// <summary>A screen's breadcrumb (rcx its object, edx the level, r8d the UI text): the Tavern's state 4 names
    /// level 2 "Errands" (<see cref="ErrandsCrumb"/>), which the Dispatch view names Dispatch Missions.</summary>
    internal const string BreadcrumbPattern =
        "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 48 8D 99 18 02 00 00 41 8B F0 48 8B CB 8B FA E8";
    internal const int ErrandsCrumb = 0x732, CrumbLevel = 2;
    internal const string WorkHistoryEventPattern =
        "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 30 49 8B F0 48 8B DA 48 8B F9 E8 ?? ?? ?? ?? 8B 0E 83 E9 1B";
    internal const string ListCountPattern =
        "48 89 5C 24 08 57 48 83 EC 20 48 8B D9 8B FA 48 8B 49 18 48 85 C9 0F 84 ?? ?? ?? ?? 83 4B 60 FF";
    internal const string ListSelectPattern =
        "48 89 5C 24 08 44 88 44 24 18 88 54 24 10 55 56 57 41 54 41 55 41 56 41 57 48 8B EC 48 83 EC 70";
    internal const string ListDetailPattern =
        "48 89 5C 24 18 48 89 6C 24 20 56 57 41 56 48 83 EC 30 48 83 B9 98 01 00 00 00 48 8B F9";
    internal const string AbandonPattern =
        "48 89 5C 24 08 48 89 74 24 10 57 48 81 EC 90 01 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4";
    internal const string ProfitMainUpdatePattern =
        "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 56 48 83 EC 20 48 8B 01 49 8B F0 44 8B F2";
    internal const string ProfitMainStatePattern =
        "48 89 5C 24 08 48 89 74 24 10 48 89 7C 24 18 41 56 48 83 EC 30 41 8B F0 48 8B D9 83 EA 01";
    internal const string ProfitMainShowPattern =
        "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 56 48 83 EC 40 48 8D 69 60 48 8B F9";
    internal const string ProfitHeaderPattern =
        "B8 4E 07 00 00 39 82 F8 00 00 00 74 0D 89 82 F8 00 00 00 C6 82 3C 01 00 00 01 C3";
    internal const string SetTextPattern =
        "48 89 5C 24 08 57 48 83 EC 20 48 8B D9 49 8B F8 8B CA E8 ?? ?? ?? ?? 48 8B D0 4C 8B C7 48 8B CB";

    internal const int ProfitTable = 0xB6, ListBegin = 0x60, ListEnd = 0x68;
    internal static nint DayChangeAt { get; private set; }
    internal static nint ErrandListAt { get; private set; }
    internal static nint NewInMenuAt { get; private set; }
    internal static nint ReportAt { get; private set; }
    internal static nint ProfitRowsAt { get; private set; }
    internal static nint NumericProfitAt { get; private set; }
    internal static nint PrepareAt { get; private set; }
    internal static nint SendAt { get; private set; }
    internal static nint TableRowAt { get; private set; }
    internal static nint BarStateAt { get; private set; }
    internal static nint BarRequestAt { get; private set; }
    internal static nint BreadcrumbAt { get; private set; }
    internal static nint TavernUnlockAt { get; private set; }
    internal static nint ProfitTableSlot { get; private set; }
    internal static nint ProfitTableLoaderAt { get; private set; }
    internal static nint WorkHistoryEventAt { get; private set; }
    internal static nint ListCountAt { get; private set; }
    internal static nint ListSelectAt { get; private set; }
    internal static nint ListDetailAt { get; private set; }
    internal static nint AbandonAt { get; private set; }
    internal static nint ProfitMainUpdateAt { get; private set; }
    internal static nint ProfitMainStateAt { get; private set; }
    internal static nint ProfitMainShowAt { get; private set; }
    internal static nint ProfitHeaderAt { get; private set; }
    internal static nint SetTextAt { get; private set; }
    internal static byte* Count { get; private set; }
    internal static byte* RecordsAt { get; private set; }
    internal static byte* States { get; private set; }
    internal static ushort* SelectedRow { get; private set; }
    internal static bool DispatchView => _dispatchView;

    private sealed record DispatchEntry(int Key, string Kind, string Name, string Category, int Set,
                                        int UnlockStory, int Days, int MaxUnits, int Effect, string Nav = "");

    private enum DispatchPage { Main, PropositionKinds, Propositions, Jobs, Training, Contests, Recall }

    private static delegate* unmanaged<void> _dayChange;
    private static delegate* unmanaged<nint, void> _errandList;
    private static delegate* unmanaged<nint, int, byte> _newInMenu;
    private static delegate* unmanaged<nint, void> _report;
    private static delegate* unmanaged<ushort*, int, void> _numericProfit;
    private static delegate* unmanaged<void> _prepare;
    private static delegate* unmanaged<void> _send;
    private static delegate* unmanaged<int, int, nint> _tableRow;
    private static delegate* unmanaged<nint, int, void> _barState;
    private static delegate* unmanaged<nint, int*, nint> _barRequest;
    private static delegate* unmanaged<nint, int, int, nint> _breadcrumb;
    private static delegate* unmanaged<nint, int, nint> _profitTableLoader;
    private static delegate* unmanaged<nint, nint, int*, void> _workHistoryEvent;
    private static delegate* unmanaged<nint, int, void> _listCount;
    private static delegate* unmanaged<nint, int, byte, void> _listSelect;
    private static delegate* unmanaged<nint, int, void> _listDetail;
    private static delegate* unmanaged<void> _abandon;
    private static delegate* unmanaged<nint, int, int*, void> _profitMainUpdate;
    private static delegate* unmanaged<nint, int, int, void> _profitMainState;
    private static delegate* unmanaged<nint, int, void> _profitMainShow;
    private static delegate* unmanaged<nint, nint, void> _profitHeader;
    private static delegate* unmanaged<nint, int, nint, void> _setText;
    private static delegate* unmanaged<nint, nint> _profitRecordInit;
    private static nint* _profitTable;
    private static nint* _tableManager;
    private static nint* _uiRoot;
    private static byte* _roster;
    private static Log _log = null!;
    private static volatile bool _dispatchView;
    private static int _migrations;
    private static bool _catalogLoaded;
    private static DispatchEntry[] _catalog = [];
    private static readonly Dictionary<int, DispatchEntry> Entries = [];
    private static readonly int[] CarrierKeys = Enumerable.Repeat(-1, NativeRows).ToArray();
    private static readonly Dictionary<int, int> Carriers = [];
    private static readonly Dictionary<int, byte[]> PreparedRecords = [];
    private static int _carrierCount;
    private static bool _buildingPool;
    private static string? _harnessKind;
    private static DispatchPage _page;
    private static string? _category;
    private static nint _listAddon;
    private static nint _listModel;
    private static nint _barScreen;
    /// <summary>The Tavern screen whose Dispatch Missions button was chosen, until Errands' Prepare opens the view.</summary>
    private static nint _menuDispatchScreen;
    private static nint _profitMain;
    private static readonly HashSet<nint> ProfitHeaderNodes = [];
    private static int _dispatchTitleText;
    private static int _dispatchPromptText;
    private static int _dispatchEmptyText;
    private static bool _nativeNumericTextCaptured;
    private static int _nativeCostText;
    private static int _nativeDayStartText;
    private static int _nativeDayEndText;
    private static bool _needsRefresh;
    private static int _refreshDelay;
    private static DispatchPage _pendingBackPage;
    private static int _pendingBackFrame = -1;

    internal static void Find(GameImage image)
    {
        var loop = image.FindUnique(ErrandLoopPattern);
        Count = (byte*)GameImage.Rip(loop, 2, 6);
        RecordsAt = (byte*)(GameImage.Rip(loop + 0x19, 3, 7) - 2);
        States = (byte*)GameImage.Rip(image.FindUnique(CountDownsPattern) + 6, 3, 7);
    }

    public static void Install(Game game)
    {
        _log = game.Log;
        _roster = game.Units.Roster;
        if (Count == null)
            Find(game.Image);

        var image = game.Image;
        ProfitRowsAt = image.FindUnique(ProfitRowsPattern, ".data");
        DayChangeAt = image.FindUnique(DayChangePattern);
        ErrandListAt = image.FindUnique(ErrandListPattern);
        NewInMenuAt = image.FindUnique(NewInMenuPattern);
        ReportAt = image.FindUnique(ReportPattern);
        NumericProfitAt = image.FindUnique(NumericProfitPattern);
        PrepareAt = image.FindUnique(PreparePattern);
        SendAt = image.FindUnique(SendPattern);
        TableRowAt = image.FindUnique(TableRowPattern);
        BarStateAt = image.FindUnique(BarStatePattern);
        BarRequestAt = image.FindUnique(BarRequestPattern);
        BreadcrumbAt = image.FindUnique(BreadcrumbPattern);
        TavernUnlockAt = image.FindUnique(TavernUnlockPattern) + TavernUnlockCallAt;
        ProfitTableSlot = GameImage.Rip(ErrandListAt + 0x14, 3, 7);
        ProfitTableLoaderAt = GameImage.Rip(ErrandListAt + 0x3C, 1, 5);
        _profitTable = (nint*)ProfitTableSlot;
        _tableManager = (nint*)GameImage.Rip(ErrandListAt + 0x2B, 3, 7);
        _profitTableLoader = (delegate* unmanaged<nint, int, nint>)ProfitTableLoaderAt;
        _uiRoot = (nint*)GameImage.Rip(BarStateAt + 0x29, 3, 7);
        WorkHistoryEventAt = image.FindUnique(WorkHistoryEventPattern);
        ListCountAt = image.FindUnique(ListCountPattern);
        ListSelectAt = image.FindUnique(ListSelectPattern);
        ListDetailAt = image.FindUnique(ListDetailPattern);
        AbandonAt = image.FindUnique(AbandonPattern);
        ProfitMainUpdateAt = image.FindUnique(ProfitMainUpdatePattern);
        ProfitMainStateAt = image.FindUnique(ProfitMainStatePattern);
        ProfitMainShowAt = image.FindUnique(ProfitMainShowPattern);
        ProfitHeaderAt = image.FindUnique(ProfitHeaderPattern);
        SetTextAt = image.FindUnique(SetTextPattern);
        if (*(byte*)(SendAt + 0x37) != 0x0F || *(byte*)(SendAt + 0x38) != 0xB7)
            throw new InvalidDataException("the native errand sender's selected row moved");
        SelectedRow = (ushort*)GameImage.Rip(SendAt + 0x37, 3, 7);

        _dayChange = (delegate* unmanaged<void>)game.Hooks.Hook(
            DayChangeAt, (nint)(delegate* unmanaged<void>)&OnDayChange, "the native dispatch day change");
        _errandList = (delegate* unmanaged<nint, void>)game.Hooks.Hook(
            ErrandListAt, (nint)(delegate* unmanaged<nint, void>)&OnErrandList, "the native errand list");
        _newInMenu = (delegate* unmanaged<nint, int, byte>)game.Hooks.Hook(
            NewInMenuAt, (nint)(delegate* unmanaged<nint, int, byte>)&OnNewInMenu,
            "the Tavern's new-content check");
        _report = (delegate* unmanaged<nint, void>)game.Hooks.Hook(
            ReportAt, (nint)(delegate* unmanaged<nint, void>)&OnReport, "the native dispatch report");
        _numericProfit = (delegate* unmanaged<ushort*, int, void>)game.Hooks.Hook(
            NumericProfitAt, (nint)(delegate* unmanaged<ushort*, int, void>)&OnNumericProfit,
            "the native errand's numeric row");
        _prepare = (delegate* unmanaged<void>)game.Hooks.Hook(
            PrepareAt, (nint)(delegate* unmanaged<void>)&OnPrepare, "the native errand screen preparation");
        _send = (delegate* unmanaged<void>)game.Hooks.Hook(
            SendAt, (nint)(delegate* unmanaged<void>)&OnSend, "the native errand sender");
        _tableRow = (delegate* unmanaged<int, int, nint>)game.Hooks.Hook(
            TableRowAt, (nint)(delegate* unmanaged<int, int, nint>)&OnTableRow, "the localized table row lookup");
        _barState = (delegate* unmanaged<nint, int, void>)game.Hooks.Hook(
            BarStateAt, (nint)(delegate* unmanaged<nint, int, void>)&OnBarState, "the Tavern screen state");
        _barRequest = (delegate* unmanaged<nint, int*, nint>)game.Hooks.Hook(
            BarRequestAt, (nint)(delegate* unmanaged<nint, int*, nint>)&OnBarRequest, "the Tavern screen's request");
        PatchTavernUnlock();
        _breadcrumb = (delegate* unmanaged<nint, int, int, nint>)game.Hooks.Hook(
            BreadcrumbAt, (nint)(delegate* unmanaged<nint, int, int, nint>)&OnBreadcrumb, "a screen's breadcrumb");
        _workHistoryEvent = (delegate* unmanaged<nint, nint, int*, void>)game.Hooks.Hook(
            WorkHistoryEventAt, (nint)(delegate* unmanaged<nint, nint, int*, void>)&OnWorkHistoryEvent,
            "the native Errands list's selection event");
        _listCount = (delegate* unmanaged<nint, int, void>)ListCountAt;
        _listSelect = (delegate* unmanaged<nint, int, byte, void>)ListSelectAt;
        _listDetail = (delegate* unmanaged<nint, int, void>)game.Hooks.Hook(
            ListDetailAt, (nint)(delegate* unmanaged<nint, int, void>)&OnListDetail,
            "the native Errands list's focused detail");
        _abandon = (delegate* unmanaged<void>)game.Hooks.Hook(
            AbandonAt, (nint)(delegate* unmanaged<void>)&OnAbandon, "the native dispatch recall");
        _profitMainUpdate = (delegate* unmanaged<nint, int, int*, void>)game.Hooks.Hook(
            ProfitMainUpdateAt, (nint)(delegate* unmanaged<nint, int, int*, void>)&OnProfitMainUpdate,
            "the Tavern Errands catalog's controls");
        _profitMainState = (delegate* unmanaged<nint, int, int, void>)ProfitMainStateAt;
        _profitMainShow = (delegate* unmanaged<nint, int, void>)ProfitMainShowAt;
        _profitRecordInit = (delegate* unmanaged<nint, nint>)(BarStateAt - 0x78);
        _profitHeader = (delegate* unmanaged<nint, nint, void>)game.Hooks.Hook(
            ProfitHeaderAt, (nint)(delegate* unmanaged<nint, nint, void>)&OnProfitHeader,
            "the Tavern Errands catalog's heading");
        _setText = (delegate* unmanaged<nint, int, nint, void>)SetTextAt;

        _log.Info("dispatch missions: in the Tavern's menu in place of Errands, with Propositions, Jobs, Special " +
                  "Training, Contests and recall/reports in the native view");
    }

    internal static bool OpenDispatch(nint barScreen)
    {
        if (barScreen == 0 || _prepare == null)
            return false;
        try
        {
            LoadCatalog();
            MigrateLegacyRecords();
            _dispatchView = true;
            _barScreen = barScreen;
            _page = HarnessPage();
            _category = null;
            _listAddon = 0;
            _profitMain = FindProfitMain(barScreen);
            if (_profitMain == 0)
                throw new InvalidDataException("the Tavern's Errands catalog is unavailable");
            _nativeNumericTextCaptured = false;
            _needsRefresh = false;
            _refreshDelay = 0;
            _pendingBackFrame = -1;
            // Prepare is a whole-screen routine which also pushes the Tavern's world-UI state. Run it once with a
            // full carrier pool, then rewrite that native record pool for category changes instead of calling it again.
            // The rows copy the layout's texts as Prepare makes them, so the category page's blanks go in first.
            FindColumnHeads();
            ApplyColumnHeads(IsNavigationPage());
            _buildingPool = true;
            BuildCarrierPool();
            ClearProfitMain();
            ((delegate* unmanaged<void>)PrepareAt)();
            _buildingPool = false;
            if (ColumnHeads.Count == 0)
                FindColumnHeads();
            CachePreparedRecords();
            BuildCarrierMap();
            RewriteProfitRecords();
            RefreshProfitMain();
            _profitMainState(_profitMain, 4, 6);
            ApplyProfitHeading(true);
            ApplyProfitPrompt(true);
            _log.Info($"dispatch missions: opened the native view with {_carrierCount} available row(s)");
            return true;
        }
        catch (Exception e)
        {
            _buildingPool = false;
            RestoreProfitNumericText();
            ApplyColumnHeads(false);
            SetEmptyListText(false);
            _dispatchView = false;
            _log.Error($"dispatch missions: the native view couldn't open ({e.Message})");
            return false;
        }
    }

    /// <summary>Restrict the next private-harness opening to one catalog kind. The ordinary player path never sets
    /// this; it lets tools/harness.py capture each part of the native view without scrolling through unrelated rows.</summary>
    internal static string SetHarnessKind(string kind)
    {
        _harnessKind = kind.ToLowerInvariant() switch
        {
            "all" => null,
            "proposition" or "propositions" => "Proposition",
            "job" or "jobs" => "Job",
            "training" => "Training",
            "contest" or "contests" => "Contest",
            _ => throw new ArgumentException($"unknown Dispatch kind '{kind}'"),
        };
        return _harnessKind ?? "all";
    }

    internal static string HarnessSummary()
    {
        var entries = CarrierKeys.Take(_carrierCount).Select(key => Entries.GetValueOrDefault(key)).OfType<DispatchEntry>().ToArray();
        var profitRows = _profitMain == 0 ? -1 :
            (int)((*(nint*)(_profitMain + 0x3F0) - *(nint*)(_profitMain + 0x3E8)) / 0x158);
        var profitFocus = _profitMain == 0 ? -1 : *(int*)(_profitMain + 0xC0);
        var profitState = _profitMain == 0 ? -1 : *(int*)(_profitMain + 0x40);
        var profitGate = _profitMain == 0 ? -1 : *(byte*)(_profitMain + 0xDC);
        var profitPending = _profitMain == 0 ? -2 : *(int*)(_profitMain + 0xC8);
        return $"{_page} ({_category ?? "-"}), {entries.Length} row(s): " + string.Join(", ", entries.GroupBy(e => e.Kind)
            .Select(group => $"{group.Count()} {group.Key}")) + "\n" + string.Join("\n", entries.Select((e, k) =>
                $"{k + 1}: {e.Kind} | {e.Category} | {e.Name} | key {e.Key}")) +
                $"\nprofit-main: rows {profitRows}, focus {profitFocus}, state {profitState}, gate {profitGate}, " +
                $"pending {profitPending}, at 0x{_profitMain:X}, heading nodes " +
                string.Join(" ", ProfitHeaderNodes.Select(node => $"0x{node:X}"));
    }

    /// <summary>The native list add-on is created just after Prepare returns on some Tavern paths. Retry on the next
    /// world UI frame so stale ordinary Errand widgets cannot remain below the Dispatch page.</summary>
    internal static void Tick()
    {
        if (!_dispatchView)
            return;
        try
        {
            var frame = RemixedScenarios.WorldFrames;
            if (_harnessKind == null && _page != DispatchPage.Main && !_needsRefresh &&
                (RemixedScenarios.NewlyHeldButtons & 0x20) != 0)
            {
                // Give ProfitMain its normal update later in this frame. A zero-row native page does not always
                // publish its Back transition; if it still has not done so next frame, unwind our page directly.
                _pendingBackPage = _page;
                _pendingBackFrame = frame;
            }
            if (_pendingBackFrame >= 0 && frame > _pendingBackFrame)
            {
                var page = _pendingBackPage;
                _pendingBackFrame = -1;
                if (!_needsRefresh && _page == page)
                {
                    NavigateBack();
                    _needsRefresh = true;
                    _refreshDelay = 1;
                    _log.Info($"dispatch missions: controller Back fallback returned from {page}");
                }
            }
            if (!_needsRefresh)
                return;
            if (_refreshDelay-- > 0)
                return;
            if (_profitMain != 0)
                RebuildProfitMain();
            else
            {
                var addon = CurrentListAddon();
                if (addon != 0)
                    RefreshList(addon);
            }
        }
        catch (Exception e)
        {
            _needsRefresh = false;
            _log.Error($"dispatch missions: the native list couldn't finish opening ({e.Message})");
        }
    }

    [UnmanagedCallersOnly]
    private static byte OnNewInMenu(nint screen, int kind)
    {
        // Kind 6 marked Errands' button for new errands. Dispatch Missions has its place and the remaster's errands
        // can't be reached, so the mark would only ever point at them.
        return kind == 6 ? (byte)0 : _newInMenu(screen, kind);
    }

    /// <summary>The Tavern menu's unlock call (<see cref="TavernUnlockPattern"/>) goes to a stub that jumps to
    /// <see cref="OnTavernUnlock"/>; it only reads the table, so nothing is lost by not asking it.</summary>
    private static void PatchTavernUnlock()
    {
        if (*(byte*)TavernUnlockAt != 0xE8)
            throw new InvalidDataException("the Tavern menu's unlock call moved");
        var stub = GameImage.AllocateCodeNear(TavernUnlockAt, 16);
        var code = new byte[12];
        code[0] = 0x48;
        code[1] = 0xB8; // mov rax, OnTavernUnlock
        BitConverter.GetBytes((long)(nint)(delegate* unmanaged<nint, int, byte>)&OnTavernUnlock).CopyTo(code, 2);
        code[10] = 0xFF;
        code[11] = 0xE0; // jmp rax
        code.CopyTo(new Span<byte>(stub, code.Length));
        var call = new byte[5];
        call[0] = 0xE8;
        BitConverter.GetBytes((int)((nint)stub - (TavernUnlockAt + 5))).CopyTo(call, 1);
        GameImage.WriteCode(TavernUnlockAt, call);
    }

    /// <summary>Whether the Tavern menu's second button, Dispatch Missions, is open: when ReMixed posts it.</summary>
    [UnmanagedCallersOnly]
    private static byte OnTavernUnlock(nint table, int row)
    {
        try
        {
            return RemixedScenarios.DispatchUnlocked ? (byte)1 : (byte)0;
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: the Tavern menu's unlock couldn't be read ({e.Message})");
            return 0;
        }
    }

    /// <summary>The Tavern menu's Dispatch Missions button, in Errands' place: the world map's Tavern state runs
    /// Errands' own case for it (closing the Tavernmaster's line, preparing the screen, clearing its world flag 4), and
    /// the Prepare it calls opens the Dispatch view (<see cref="OnPrepare"/>). Locked, the button does nothing: the
    /// remaster's note for it speaks of Errands.</summary>
    [UnmanagedCallersOnly]
    private static nint OnBarRequest(nint screen, int* request)
    {
        var result = _barRequest(screen, request);
        try
        {
            if (!_dispatchView && request != null && *(int*)(screen + 0x98) == MenuState
                && *request is ErrandsRequest or LockedErrandsRequest)
            {
                if (*request == ErrandsRequest && RemixedScenarios.DispatchUnlocked)
                {
                    _menuDispatchScreen = screen;
                    _log.Info("dispatch missions: chosen in the Tavern's menu");
                }
                else
                {
                    *request = NoRequest;
                    _log.Info("dispatch missions: chosen in the Tavern's menu before ReMixed unlocks it; nothing opens");
                }
            }
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: the Tavern menu's button couldn't be read ({e.Message})");
        }
        return result;
    }

    [UnmanagedCallersOnly]
    private static nint OnBreadcrumb(nint crumbs, int level, int text)
    {
        if (_dispatchView && level == CrumbLevel && text == ErrandsCrumb && _dispatchTitleText > 0)
            text = _dispatchTitleText;
        return _breadcrumb(crumbs, level, text);
    }

    [UnmanagedCallersOnly]
    private static void OnBarState(nint screen, int state)
    {
        try
        {
            if (_dispatchView && state != 4 && state is not (>= 6 and <= 12) && state != 23)
            {
                ApplyProfitHeading(false);
                ApplyProfitPrompt(false);
                RestoreProfitNumericText();
                ApplyColumnHeads(false);
                ColumnHeads.Clear();
                SetEmptyListText(false);
                _dispatchView = false;
                _harnessKind = null;
                _listAddon = 0;
                _listModel = 0;
                _barScreen = 0;
                _profitMain = 0;
                _nativeNumericTextCaptured = false;
                ProfitHeaderNodes.Clear();
                _needsRefresh = false;
                _refreshDelay = 0;
                _pendingBackFrame = -1;
            }
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: the Tavern state couldn't be observed ({e.Message})");
        }
        _barState(screen, state);
    }

    [UnmanagedCallersOnly]
    private static void OnProfitHeader(nint main, nint node)
    {
        _profitHeader(main, node);
        if (node == 0)
            return;
        try
        {
            ProfitHeaderNodes.Add(node);
            if (_dispatchView && _dispatchTitleText > 0)
            {
                *(int*)(node + 0xF8) = _dispatchTitleText;
                *(byte*)(node + 0x13C) = 1;
            }
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: the catalog heading couldn't change ({e.Message})");
        }
    }

    [UnmanagedCallersOnly]
    private static void OnProfitMainUpdate(nint main, int state, int* nextState)
    {
        _profitMainUpdate(main, state, nextState);
        if (!_dispatchView || nextState == null || main != _profitMain || state != 4)
            return;
        try
        {
            var requested = *nextState;
            if (requested >= 0 && requested != 4)
                _log.Info($"dispatch missions: catalog requested Tavern state {requested}, row " +
                          $"{*(int*)(main + 0xC0)} on {_page}");
            if (requested == 6)
            {
                var entry = EntryForCarrier(*(int*)(main + 0xC0));
                if (entry?.Kind == "Menu" && Navigate(entry))
                {
                    _pendingBackFrame = -1;
                    _needsRefresh = true;
                    _refreshDelay = 1;
                    *nextState = -1;
                    return;
                }
            }
            if (requested == 1 && _page != DispatchPage.Main && _harnessKind == null)
            {
                _pendingBackFrame = -1;
                NavigateBack();
                _needsRefresh = true;
                _refreshDelay = 1;
                *nextState = -1;
            }
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: the Tavern catalog couldn't navigate ({e.Message})");
        }
    }

    [UnmanagedCallersOnly]
    private static void OnWorkHistoryEvent(nint addon, nint node, int* message)
    {
        try
        {
            if (_dispatchView)
                _listAddon = addon;
            // 0x1C is the native list's Confirm event. The addon's +0x34 is the focused Profit row, maintained by
            // its ordinary focus handler. Navigation rows are consumed here; dispatchable leaves continue unchanged.
            var carrier = *(int*)(addon + 0x34);
            var entry = EntryForCarrier(carrier);
            if (_dispatchView && message != null && *message == 0x1C && entry?.Kind == "Menu")
            {
                if (Navigate(entry))
                    RefreshList(addon);
                return;
            }
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: a native menu selection couldn't navigate ({e.Message})");
        }
        _workHistoryEvent(addon, node, message);
    }

    [UnmanagedCallersOnly]
    private static void OnListDetail(nint addon, int row)
    {
        if (_dispatchView)
        {
            _listAddon = addon;
            var source = *(nint*)(addon + 0x38);
            if (source != 0)
                _listModel = source - 0x58;
        }
        _listDetail(addon, row);
    }

    [UnmanagedCallersOnly]
    private static nint OnTableRow(int table, int key)
    {
        try
        {
            if (_dispatchView && table == ProfitTable && EntryForCarrier(key) is { } entry)
                key = entry.Key;
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: a virtual text row couldn't be selected ({e.Message})");
        }
        return _tableRow(table, key);
    }

    [UnmanagedCallersOnly]
    private static void OnNumericProfit(ushort* output, int row)
    {
        var entry = _dispatchView ? EntryForCarrier(row) : null;
        _numericProfit(output, entry == null ? row : NumericTemplateRow);
        if (entry == null)
            return;
        try
        {
            PrepareNumericEntry(output, entry);
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: {entry.Name}'s numeric row couldn't be prepared ({e.Message})");
        }
    }

    private static void PrepareNumericEntry(ushort* output, DispatchEntry entry)
    {
        output[1] = (ushort)Math.Clamp(entry.MaxUnits, 0, 3);
        output[6] = (ushort)Math.Clamp(entry.Days, 0, 30);
        output[7] = output[6];
        output[16] = 0;
        output[17] = 0;
        if (entry.Kind == "Menu")
        {
            // Native fee is the sum of two indexed amounts. Navigation is not an errand and must not look as if
            // accepting the row costs 2,000 gil or takes seven days.
            output[4] = 0;
            output[5] = 0;
        }
    }

    [UnmanagedCallersOnly]
    private static void OnErrandList(nint screen)
    {
        Span<int> variables = stackalloc int[NativeRows];
        Span<byte> countdowns = stackalloc byte[NativeRows];
        var forced = false;
        int* rowCount = null;
        var savedRowCount = 0;
        var limited = false;
        try
        {
            MigrateLegacyRecords();
            _listModel = screen;
            if (_dispatchView)
            {
                BuildCarrierMap();
                SaveCarrierState(variables, countdowns, open: false);
                forced = true;
            }
            rowCount = NativeProfitRowCount();
            if (rowCount == null)
                throw new InvalidDataException("the Profit table's native row count is unavailable");
            savedRowCount = *rowCount;
            if (savedRowCount > NativeRows)
            {
                *rowCount = NativeRows;
                limited = true;
            }
            _errandList(screen);
            if (limited)
            {
                *rowCount = savedRowCount;
                limited = false;
            }
            var before = (*(nint*)(screen + ListEnd) - *(nint*)(screen + ListBegin)) / 8;
            FilterList(screen, _dispatchView);
            var after = (*(nint*)(screen + ListEnd) - *(nint*)(screen + ListBegin)) / 8;
            _log.Info($"dispatch missions: native list built {before} row(s), kept {after}; separate view {_dispatchView}");
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: the native list couldn't be virtualized ({e.Message})");
        }
        finally
        {
            if (limited)
                *rowCount = savedRowCount;
            if (forced)
                RestoreCarrierState(variables, countdowns);
        }
    }

    /// <summary>
    /// The native list assumes Profit has exactly 96 rows: for every row it reads event variable 0x360 + row, whose
    /// valid storage ends at 0x3BF. The localized table is larger only so the virtual rows have native text records.
    /// Limit the original scan through this count, then restore it before the virtual text lookups in FilterList.
    /// </summary>
    private static int* NativeProfitRowCount()
    {
        var table = _profitTable == null ? 0 : *_profitTable;
        if (table == 0 && _tableManager != null && *_tableManager != 0 && _profitTableLoader != null)
        {
            table = _profitTableLoader(*_tableManager, ProfitTable);
            if (table != 0)
                *_profitTable = table;
        }
        if (table == 0)
            return null;
        var rows = *(nint*)(table + 0x30);
        if (rows == 0)
            return null;
        rows = *(nint*)(rows + 0x38);
        return rows == 0 ? null : (int*)(rows + 0x24);
    }

    private static void FilterList(nint screen, bool dispatch)
    {
        var begin = *(nint*)(screen + ListBegin);
        var end = *(nint*)(screen + ListEnd);
        if (begin == 0 || end < begin || (end - begin) % 8 != 0 || end - begin > 0x1000)
            return;
        var write = begin;
        for (var read = begin; read < end; read += 8)
        {
            var row = *(int*)read;
            var entry = dispatch ? EntryForCarrier(row) : null;
            var keep = dispatch ? entry != null : row < NativeRows;
            if (!keep)
                continue;
            *(long*)write = *(long*)read;
            if (entry != null)
            {
                var text = _tableRow(ProfitTable, entry.Key);
                if (text != 0)
                    *(int*)(write + 4) = *(int*)(text + 0x24);
            }
            write += 8;
        }
        *(nint*)(screen + ListEnd) = write;
    }

    [UnmanagedCallersOnly]
    private static void OnPrepare()
    {
        // The Tavern menu's Dispatch Missions button, by Errands' own case: the view prepares the screen itself
        // (calling Prepare again, which comes back here with no button pending). If it can't, Errands open as usual.
        var chosen = _menuDispatchScreen;
        _menuDispatchScreen = 0;
        if (chosen != 0 && OpenDispatch(chosen))
            return;
        Span<int> variables = stackalloc int[NativeRows];
        Span<byte> countdowns = stackalloc byte[NativeRows];
        Span<byte> rows = stackalloc byte[NativeRecords];
        Span<byte> states = stackalloc byte[NativeRecords];
        Span<byte> indices = stackalloc byte[NativeRecords];
        var mapped = 0;
        var forced = false;
        var called = false;
        try
        {
            MigrateLegacyRecords();
            if (_dispatchView)
            {
                if (!_buildingPool)
                    BuildCarrierMap();
                SaveCarrierState(variables, countdowns, open: true);
                forced = true;
                var count = Math.Min(*Count, (byte)NativeRecords);
                for (var i = 0; i < count; i++)
                {
                    var record = RecordsAt + i * RecordBytes;
                    if (!IsDispatchRecord(record) || !Carriers.TryGetValue(record[1], out var carrier))
                        continue;
                    rows[mapped] = record[1];
                    states[mapped] = record[0];
                    indices[mapped] = (byte)i;
                    record[1] = (byte)carrier;
                    record[0] &= 0x7F;
                    mapped++;
                }
            }
            _prepare();
            called = true;
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: the native screen couldn't prepare ({e.Message})");
        }
        finally
        {
            if (!called)
                _prepare();
            for (var k = 0; k < mapped; k++)
            {
                var record = RecordsAt + indices[k] * RecordBytes;
                record[1] = rows[k];
                record[0] = states[k];
            }
            if (forced)
                RestoreCarrierState(variables, countdowns);
        }
    }

    [UnmanagedCallersOnly]
    private static void OnSend()
    {
        var carrier = SelectedRow == null ? -1 : *SelectedRow - 1;
        var entry = _dispatchView ? EntryForCarrier(carrier) : null;
        if (entry == null)
        {
            _send();
            return;
        }
        var variable = FirstState + carrier;
        var savedVariable = Nemesis.GetVariable(variable);
        var savedCountdown = States[carrier];
        var before = Math.Min(*Count, (byte)NativeRecords);
        var sent = false;
        try
        {
            _send();
            if (*Count == before + 1 && before < NativeRecords)
            {
                var record = RecordsAt + before * RecordBytes;
                record[0] |= DispatchRecord;
                record[1] = (byte)entry.Key;
                sent = true;
                _log.Info($"dispatch missions: sent {record[5]} unit(s) on {entry.Kind} '{entry.Name}'");
            }
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: {entry.Name} couldn't send its group ({e.Message})");
        }
        finally
        {
            Nemesis.SetVariable(variable, savedVariable, 0);
            States[carrier] = savedCountdown;
        }
        if (sent && entry.Kind == "Proposition")
            Set(0x35F + entry.Effect, 0xA);
    }

    [UnmanagedCallersOnly]
    private static void OnAbandon()
    {
        var carrier = SelectedRow == null ? -1 : *SelectedRow - 1;
        var entry = _dispatchView ? EntryForCarrier(carrier) : null;
        if (entry == null)
        {
            _abandon();
            return;
        }

        var count = Math.Min(*Count, (byte)NativeRecords);
        byte* selected = null;
        for (var i = 0; i < count; i++)
        {
            var record = RecordsAt + i * RecordBytes;
            if (IsDispatchRecord(record) && record[1] == entry.Key)
                selected = record;
            else if (!IsDispatchRecord(record) && record[1] == carrier)
                record[1] = 0xFE; // prevent a native errand with the carrier row from being recalled instead
        }
        if (selected == null)
        {
            for (var i = 0; i < count; i++)
                if (RecordsAt[i * RecordBytes + 1] == 0xFE)
                    RecordsAt[i * RecordBytes + 1] = (byte)carrier;
            _abandon();
            return;
        }

        selected[0] &= 0x7F;
        selected[1] = (byte)carrier;
        var before = *Count;
        _abandon();
        var after = Math.Min(*Count, (byte)NativeRecords);
        for (var i = 0; i < after; i++)
        {
            var record = RecordsAt + i * RecordBytes;
            if (record[1] == 0xFE)
                record[1] = (byte)carrier;
        }
        if (*Count < before)
        {
            if (entry.Kind == "Proposition")
                Set(0x35F + entry.Effect, 0);
            _log.Info($"dispatch missions: recalled {entry.Kind} '{entry.Name}'");
        }
        else
        {
            // The native routine declined the recall. Put the selected virtual record back exactly as it was.
            for (var i = 0; i < after; i++)
            {
                var record = RecordsAt + i * RecordBytes;
                if (!IsDispatchRecord(record) && record[1] == carrier)
                {
                    record[0] |= DispatchRecord;
                    record[1] = (byte)entry.Key;
                    break;
                }
            }
        }
    }

    [UnmanagedCallersOnly]
    private static void OnDayChange()
    {
        Span<byte> dispatch = stackalloc byte[NativeRecords * RecordBytes];
        var dispatchCount = 0;
        var calledNative = false;
        try
        {
            MigrateLegacyRecords();
            var count = Math.Min(*Count, (byte)NativeRecords);
            var kept = 0;
            for (var i = 0; i < count; i++)
            {
                var record = RecordsAt + i * RecordBytes;
                if (IsDispatchRecord(record))
                {
                    new ReadOnlySpan<byte>(record, RecordBytes).CopyTo(dispatch[(dispatchCount * RecordBytes)..]);
                    dispatchCount++;
                }
                else
                {
                    if (kept != i)
                        new ReadOnlySpan<byte>(record, RecordBytes).CopyTo(
                            new Span<byte>(RecordsAt + kept * RecordBytes, RecordBytes));
                    kept++;
                }
            }
            new Span<byte>(RecordsAt + kept * RecordBytes, (count - kept) * RecordBytes).Clear();
            *Count = (byte)kept;
            _dayChange();
            calledNative = true;
            var afterNative = Math.Min(*Count, (byte)NativeRecords);
            for (var i = 0; i < dispatchCount && afterNative < NativeRecords; i++, afterNative++)
            {
                var source = dispatch[(i * RecordBytes)..((i + 1) * RecordBytes)];
                if ((source[0] & 4) == 0)
                {
                    source[2]++;
                    if (source[2] >= source[3])
                        source[0] |= 4;
                }
                source.CopyTo(new Span<byte>(RecordsAt + afterNative * RecordBytes, RecordBytes));
            }
            *Count = (byte)afterNative;
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: a dispatch group couldn't advance a day ({e.Message})");
            if (!calledNative)
                _dayChange();
        }
    }

    [UnmanagedCallersOnly]
    private static void OnReport(nint screen)
    {
        DispatchEntry? entry = null;
        var carrier = -1;
        var variable = -1;
        var savedVariable = 0;
        byte savedCountdown = 0;
        Span<byte> slots = stackalloc byte[3];
        var units = 0;
        try
        {
            var index = *(int*)(screen + 0x2C);
            if ((uint)index < *Count)
            {
                var record = RecordsAt + index * RecordBytes;
                if (IsDispatchRecord(record) && Entries.TryGetValue(record[1], out entry)
                    && Carriers.TryGetValue(entry.Key, out carrier))
                {
                    units = Math.Min(record[5], (byte)slots.Length);
                    new ReadOnlySpan<byte>(record + 6, units).CopyTo(slots);
                    record[0] &= 0x7F;
                    record[1] = (byte)carrier;
                    variable = FirstState + carrier;
                    savedVariable = Nemesis.GetVariable(variable);
                    savedCountdown = States[carrier];
                }
            }
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: a report couldn't read its group ({e.Message})");
        }

        _report(screen);
        if (variable >= 0)
        {
            Nemesis.SetVariable(variable, savedVariable, 0);
            States[carrier] = savedCountdown;
        }
        if (entry == null)
            return;
        try
        {
            var changed = entry.Kind == "Training"
                ? ApplyTraining(89 + entry.Effect, slots[..units], _roster)
                : 0;
            _log.Info($"dispatch missions: {entry.Kind} '{entry.Name}' reported for {units} unit(s); " +
                      $"{changed} permanent training effect(s)");
            if (entry.Kind == "Proposition")
                Set(0x35F + entry.Effect, 0xC);
        }
        catch (Exception e)
        {
            _log.Error($"dispatch missions: {entry.Name}'s result couldn't be applied ({e.Message})");
        }
    }

    private static DispatchEntry? EntryForCarrier(int carrier) =>
        carrier is >= 0 and < NativeRows && CarrierKeys[carrier] is var key && key >= 0
            && Entries.TryGetValue(key, out var entry) ? entry : null;

    private static bool IsDispatchRecord(byte* record) =>
        (record[0] & DispatchRecord) != 0 && record[1] is >= TrainingTextFirst and <= DispatchTextLast;

    private static void SaveCarrierState(Span<int> variables, Span<byte> countdowns, bool open)
    {
        for (var carrier = 0; carrier < _carrierCount; carrier++)
        {
            variables[carrier] = Nemesis.GetVariable(FirstState + carrier);
            countdowns[carrier] = States[carrier];
            Nemesis.SetVariable(FirstState + carrier, open ? variables[carrier] & ~6 : variables[carrier] | 4, 0);
            States[carrier] = 0;
        }
    }

    private static void RestoreCarrierState(ReadOnlySpan<int> variables, ReadOnlySpan<byte> countdowns)
    {
        for (var carrier = 0; carrier < _carrierCount; carrier++)
        {
            Nemesis.SetVariable(FirstState + carrier, variables[carrier], 0);
            States[carrier] = countdowns[carrier];
        }
    }

    private static void LoadCatalog()
    {
        if (_catalogLoaded)
            return;
        _catalogLoaded = true;
        var file = Path.Combine(Path.GetDirectoryName(typeof(TavernStorage).Assembly.Location) ?? "", CatalogFileName);
        if (File.Exists(file))
        {
            _catalog = JsonSerializer.Deserialize<DispatchEntry[]>(File.ReadAllText(file),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        if (_catalog.Length == 0)
        {
            _catalog = Enumerable.Range(0, 7).Select(k => new DispatchEntry(
                TrainingTextFirst + k, "Training", TrainingName(89 + k), "Special Training", 0, 0, 3, 1, k)).ToArray();
            _log.Warn($"dispatch missions: no {CatalogFileName}; only the seven fallback training rows are available");
        }
        Entries.Clear();
        foreach (var entry in _catalog)
        {
            if (entry.Key is < TrainingTextFirst or > DispatchTextLast || !Entries.TryAdd(entry.Key, entry))
                throw new InvalidDataException($"invalid or duplicate dispatch catalog key {entry.Key}");
        }
        var options = Path.Combine(Path.GetDirectoryName(typeof(TavernStorage).Assembly.Location) ?? "",
                                   OptionsPage.FileName);
        if (File.Exists(options))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(options));
            if (document.RootElement.TryGetProperty("dispatch", out var dispatch)
                && dispatch.TryGetProperty("title", out var title))
            {
                _dispatchTitleText = title.GetInt32();
                if (dispatch.TryGetProperty("prompt", out var prompt))
                    _dispatchPromptText = prompt.GetInt32();
                if (dispatch.TryGetProperty("empty", out var empty))
                    _dispatchEmptyText = empty.GetInt32();
            }
        }
        _log.Info($"dispatch missions: loaded {_catalog.Length} virtual entries from {CatalogFileName}");
    }

    private static void ApplyProfitHeading(bool dispatch)
    {
        var text = dispatch && _dispatchTitleText > 0 ? _dispatchTitleText : 0x74E;
        foreach (var node in ProfitHeaderNodes)
        {
            if (node == 0)
                continue;
            *(int*)(node + 0xF8) = text;
            *(byte*)(node + 0x13C) = 1;
        }
    }

    private static void ApplyProfitPrompt(bool dispatch)
    {
        if (_profitMain == 0 || _setText == null)
            return;
        var node = *(nint*)(_profitMain + 0x378);
        if (node == 0)
            return;
        var text = dispatch && _dispatchPromptText > 0 ? _dispatchPromptText : 0x74B;
        *(int*)(_profitMain + 0x3C8) = text;
        _setText(node, text, _profitMain + 0x3A8);
    }

    private static void BuildCarrierMap()
    {
        LoadCatalog();
        Array.Fill(CarrierKeys, -1);
        Carriers.Clear();
        var chosen = new List<DispatchEntry>();
        var story = Math.Max(0, Nemesis.GetVariable(0x6F));

        switch (_page)
        {
            case DispatchPage.Main:
                chosen.AddRange(_catalog.Where(e => e.Kind == "Menu" && e.Nav is "propositions" or "jobs" or
                    "training" or "contests" or "recall"));
                break;
            case DispatchPage.PropositionKinds:
                chosen.AddRange(_catalog.Where(e => e.Kind == "Menu" && e.Nav.StartsWith("category:",
                    StringComparison.Ordinal)));
                break;
            case DispatchPage.Propositions:
                if (_category != null && CurrentPropositionSet(_category, story) is var set && set > 0)
                    chosen.AddRange(_catalog.Where(e => e.Kind == "Proposition" && e.Category == _category
                        && e.Set == set && Get(0x35F + e.Effect) == 0));
                break;
            case DispatchPage.Jobs:
                chosen.AddRange(_catalog.Where(e => e.Kind == "Job" && e.UnlockStory <= story));
                break;
            case DispatchPage.Training:
                chosen.AddRange(_catalog.Where(e => e.Kind == "Training" && e.UnlockStory <= story));
                break;
            case DispatchPage.Contests:
                if (story >= 5)
                {
                    var contests = _catalog.Where(e => e.Kind == "Contest").OrderBy(e => e.Effect).ToArray();
                    if (contests.Length > 0)
                        chosen.Add(contests[(Math.Max(1, Nemesis.GetVariable(0x2E)) - 1) % contests.Length]);
                }
                break;
            case DispatchPage.Recall:
                var count = Math.Min(*Count, (byte)NativeRecords);
                for (var i = 0; i < count; i++)
                {
                    var record = RecordsAt + i * RecordBytes;
                    if (IsDispatchRecord(record) && Entries.TryGetValue(record[1], out var active))
                        chosen.Add(active);
                }
                break;
        }
        foreach (var entry in chosen.DistinctBy(e => e.Key))
        {
            if (Carriers.Count >= NativeRows)
                break;
            var carrier = Carriers.Count;
            CarrierKeys[carrier] = entry.Key;
            Carriers[entry.Key] = carrier;
        }
        _carrierCount = Carriers.Count;
    }

    private static void BuildCarrierPool()
    {
        LoadCatalog();
        Array.Fill(CarrierKeys, -1);
        Carriers.Clear();
        var chosen = new List<DispatchEntry>();
        var count = Math.Min(*Count, (byte)NativeRecords);
        for (var i = 0; i < count; i++)
        {
            var record = RecordsAt + i * RecordBytes;
            if (IsDispatchRecord(record) && Entries.TryGetValue(record[1], out var active))
                chosen.Add(active);
        }
        chosen.AddRange(_catalog);
        foreach (var entry in chosen.DistinctBy(e => e.Key).Take(NativeRows))
        {
            var carrier = Carriers.Count;
            CarrierKeys[carrier] = entry.Key;
            Carriers[entry.Key] = carrier;
        }
        _carrierCount = Carriers.Count;
    }

    private static int CurrentPropositionSet(string category, int story)
    {
        foreach (var group in _catalog.Where(e => e.Kind == "Proposition" && e.Category == category
                    && e.UnlockStory <= story).GroupBy(e => e.Set).OrderBy(g => g.Key))
            if (group.Any(entry => Get(0x35F + entry.Effect) != 0xC))
                return group.Key;
        return 0;
    }

    private static DispatchPage HarnessPage() => _harnessKind switch
    {
        "Proposition" => DispatchPage.PropositionKinds,
        "Job" => DispatchPage.Jobs,
        "Training" => DispatchPage.Training,
        "Contest" => DispatchPage.Contests,
        _ => DispatchPage.Main,
    };

    private static bool Navigate(DispatchEntry entry)
    {
        switch (entry.Nav)
        {
            case "propositions": _page = DispatchPage.PropositionKinds; break;
            case "jobs": _page = DispatchPage.Jobs; break;
            case "training": _page = DispatchPage.Training; break;
            case "contests": _page = DispatchPage.Contests; break;
            case "recall": _page = DispatchPage.Recall; break;
            default:
                if (!entry.Nav.StartsWith("category:", StringComparison.Ordinal))
                    return false;
                _category = entry.Nav["category:".Length..];
                _page = DispatchPage.Propositions;
                break;
        }
        BuildCarrierMap();
        _log.Info($"dispatch missions: opened {_page}{(_category == null ? "" : $" ({_category})")}");
        return true;
    }

    private static void NavigateBack()
    {
        if (_page == DispatchPage.Propositions)
        {
            _page = DispatchPage.PropositionKinds;
            _category = null;
        }
        else
        {
            _page = DispatchPage.Main;
            _category = null;
        }
        BuildCarrierMap();
    }

    private static nint FindProfitMain(nint barScreen)
    {
        if (barScreen == 0 || ProfitMainStateAt == 0)
            return 0;
        var begin = *(nint*)(barScreen + 0x50);
        var end = *(nint*)(barScreen + 0x58);
        if (begin == 0 || end < begin || (end - begin) % 8 != 0 || end - begin > 0x100)
            return 0;
        for (var at = begin; at < end; at += 8)
        {
            var child = *(nint*)at;
            var vtable = child == 0 ? 0 : *(nint*)child;
            if (vtable != 0 && *(nint*)(vtable + 0xD0) == ProfitMainStateAt)
                return child;
        }
        return 0;
    }

    private static void ClearProfitMain()
    {
        if (_profitMain == 0)
            throw new InvalidDataException("the Tavern's Errands catalog is unavailable");
        var begin = *(nint*)(_profitMain + 0x3E8);
        var end = *(nint*)(_profitMain + 0x3F0);
        var capacity = *(nint*)(_profitMain + 0x3F8);
        if (begin == 0 && end == 0 && capacity == 0)
            return;
        if (begin == 0 || end < begin || capacity < end || (end - begin) % 0x158 != 0)
            throw new InvalidDataException("the Tavern's Errands records are invalid");
        *(nint*)(_profitMain + 0x3F0) = begin;
    }

    private static void CachePreparedRecords()
    {
        PreparedRecords.Clear();
        if (_profitMain == 0)
            return;
        var begin = *(nint*)(_profitMain + 0x3E8);
        var end = *(nint*)(_profitMain + 0x3F0);
        if (begin == 0 || end < begin || (end - begin) % 0x158 != 0)
            throw new InvalidDataException("the prepared Dispatch record pool is invalid");
        for (var record = begin; record < end; record += 0x158)
        {
            var carrier = *(int*)(record + 0x138);
            var entry = EntryForCarrier(carrier);
            if (entry == null)
                continue;
            var copy = new byte[0x158];
            new ReadOnlySpan<byte>((void*)record, copy.Length).CopyTo(copy);
            PreparedRecords[entry.Key] = copy;
        }
    }

    private static void RewriteProfitRecords()
    {
        if (_profitMain == 0 || _profitRecordInit == null)
            throw new InvalidDataException("the Tavern's Dispatch record pool is unavailable");
        var begin = *(nint*)(_profitMain + 0x3E8);
        var capacity = *(nint*)(_profitMain + 0x3F8);
        var required = (nint)(_carrierCount * 0x158L);
        if (begin == 0 || capacity < begin || capacity - begin < required)
            throw new InvalidDataException($"the Tavern's Dispatch record pool has room for fewer than {_carrierCount} rows");
        Span<ushort> numeric = stackalloc ushort[ProfitRowBytes];
        for (var carrier = 0; carrier < _carrierCount; carrier++)
        {
            var entry = EntryForCarrier(carrier) ?? throw new InvalidDataException($"missing carrier {carrier}");
            var record = begin + (nint)(carrier * 0x158L);
            if (_page == DispatchPage.Recall && PreparedRecords.TryGetValue(entry.Key, out var prepared))
            {
                prepared.CopyTo(new Span<byte>((void*)record, prepared.Length));
                *(int*)(record + 0x138) = carrier;
                continue;
            }
            _profitRecordInit(record);
            fixed (ushort* values = numeric)
            {
                _numericProfit(values, NumericTemplateRow);
                PrepareNumericEntry(values, entry);
                *(int*)(record + 0x13C) = 0;
                *(int*)(record + 0x140) = values[4] + values[5];
                *(int*)(record + 0x144) = -1;
                *(byte*)(record + 0x148) = 0;
                *(int*)(record + 0x150) = values[6];
                *(int*)(record + 0x154) = values[7];
            }
            *(int*)(record + 0x138) = carrier;
        }
        *(nint*)(_profitMain + 0x3F0) = begin + required;
    }

    private static void RefreshProfitMain(bool show = true)
    {
        if (_profitMain == 0 || _profitMainShow == null)
            throw new InvalidDataException("the Tavern's Errands catalog is unavailable");
        var begin = *(nint*)(_profitMain + 0x3E8);
        var end = *(nint*)(_profitMain + 0x3F0);
        if (begin == 0 || end < begin || (end - begin) % 0x158 != 0)
            throw new InvalidDataException("the Tavern's Errands records are invalid");
        var rows = (int)((end - begin) / 0x158);
        if (rows < _carrierCount)
            throw new InvalidDataException($"the Tavern built {rows} row(s), fewer than Dispatch needs ({_carrierCount})");
        *(nint*)(_profitMain + 0x3F0) = begin + (nint)(_carrierCount * 0x158L);
        *(int*)(_profitMain + 0xC0) = 0;
        ApplyProfitNumericText();
        if (show)
            _profitMainShow(_profitMain, _carrierCount);
        _needsRefresh = false;
    }

    private static bool IsNavigationPage() => _page is DispatchPage.Main or DispatchPage.PropositionKinds;

    private static void ApplyProfitNumericText()
    {
        if (_profitMain == 0)
            return;
        if (!_nativeNumericTextCaptured)
        {
            _nativeCostText = *(int*)(_profitMain + ProfitCostTextOffset);
            _nativeDayStartText = *(int*)(_profitMain + ProfitDayStartTextOffset);
            _nativeDayEndText = *(int*)(_profitMain + ProfitDayEndTextOffset);
            _nativeNumericTextCaptured = true;
        }
        if (IsNavigationPage())
        {
            // Categories are navigation, not errands, as in ReMixed's menus: no Days or Finder's Fee columns, their
            // heads and cells blank. Mission pages keep the native columns.
            *(int*)(_profitMain + ProfitCostTextOffset) = EmptyUiText;
            *(int*)(_profitMain + ProfitDayStartTextOffset) = EmptyUiText;
            *(int*)(_profitMain + ProfitDayEndTextOffset) = EmptyUiText;
            ApplyColumnHeads(true);
        }
        else
        {
            RestoreProfitNumericText();
            ApplyColumnHeads(false);
        }
    }

    /// <summary>The Errands screen's two column heads (<see cref="ColumnHeadAnchor"/>), or none if its layout isn't
    /// as seen: nothing is written to a node that isn't checked to be one of them.</summary>
    private static void FindColumnHeads()
    {
        ColumnHeads.Clear();
        _emptyListNode = 0;
        var first = _profitMain == 0 ? 0 : *(nint*)(_profitMain + ColumnHeadAnchor);
        if (first == 0 || !Harness.Readable(first, LayoutNodeBytes))
            return;
        var vtable = *(nint*)first;
        for (var k = 1; k < LayoutNodesScanned; k++)
        {
            var node = first + k * LayoutNodeBytes;
            if (!Harness.Readable(node, LayoutNodeBytes) || *(nint*)node != vtable)
                break;
            var text = *(int*)(node + NodeTextAt);
            if (text is DaysHeadText or FeeHeadText or NoDaysText)
                ColumnHeads.Add((node, text));
            else if (text == NoErrandsText && _dispatchEmptyText > 0)
                _emptyListNode = node;
        }
        SetEmptyListText(true);
        if (ColumnHeads.Count != 3)
        {
            _log.Info($"dispatch missions: the Errands screen's column texts weren't found ({ColumnHeads.Count} of 3); " +
                      "they stay on the category pages");
            ColumnHeads.Clear();
        }
    }

    /// <summary>The empty list's line, "No errands available.", as the Dispatch view's "No missions available."</summary>
    private static void SetEmptyListText(bool dispatch)
    {
        if (_emptyListNode == 0)
            return;
        var want = dispatch ? _dispatchEmptyText : NoErrandsText;
        if (*(int*)(_emptyListNode + NodeTextAt) != want)
        {
            *(int*)(_emptyListNode + NodeTextAt) = want;
            *(byte*)(_emptyListNode + NodeDirtyAt) = 1;
        }
        if (!dispatch)
            _emptyListNode = 0;
    }

    private static void ApplyColumnHeads(bool hide)
    {
        foreach (var (node, text) in ColumnHeads)
        {
            var want = hide ? EmptyUiText : text;
            if (*(int*)(node + NodeTextAt) == want)
                continue;
            *(int*)(node + NodeTextAt) = want;
            *(byte*)(node + NodeDirtyAt) = 1;
        }
    }

    private static void RestoreProfitNumericText()
    {
        if (_profitMain == 0 || !_nativeNumericTextCaptured)
            return;
        *(int*)(_profitMain + ProfitCostTextOffset) = _nativeCostText;
        *(int*)(_profitMain + ProfitDayStartTextOffset) = _nativeDayStartText;
        *(int*)(_profitMain + ProfitDayEndTextOffset) = _nativeDayEndText;
    }

    private static void RebuildProfitMain()
    {
        RewriteProfitRecords();
        RefreshProfitMain();
        _profitMainState(_profitMain, 4, 6);
        ApplyProfitHeading(true);
        ApplyProfitPrompt(true);
    }

    private static void RefreshList(nint addon)
    {
        var source = *(nint*)(addon + 0x38);
        if (source == 0)
            throw new InvalidDataException("the native list has no backing vector");
        var begin = *(nint*)(source + 8);
        var capacity = *(nint*)(source + 0x18);
        if (begin == 0 || capacity < begin || capacity - begin < _carrierCount * 8L)
            throw new InvalidDataException("the native list's backing vector is too small");
        var write = begin;
        for (var carrier = 0; carrier < _carrierCount; carrier++)
        {
            var entry = EntryForCarrier(carrier) ?? throw new InvalidDataException($"missing carrier {carrier}");
            var text = _tableRow(ProfitTable, entry.Key);
            *(int*)write = carrier;
            *(int*)(write + 4) = text == 0 ? 0 : *(int*)(text + 0x24);
            write += 8;
        }
        *(nint*)(source + 0x10) = write;
        _listCount(addon + 0x48, _carrierCount);
        if (_carrierCount > 0)
        {
            _listSelect(addon + 0x48, 0, 0);
            _listDetail(addon, 0);
        }
        _listAddon = addon;
        _needsRefresh = false;
    }

    private static nint CurrentListAddon()
    {
        if (_listAddon != 0)
            return _listAddon;
        if (_listModel != 0)
        {
            var addon = *(nint*)(_listModel + 0x50);
            if (addon != 0)
                return addon;
        }
        var root = _uiRoot == null ? 0 : *_uiRoot;
        var context = root == 0 ? 0 : *(nint*)(root + 0x10);
        var model = context == 0 ? 0 : *(nint*)(context + 0xC8);
        return model == 0 ? 0 : *(nint*)(model + 0x50);
    }

    /// <summary>A Special Training course's effect on the units in <paramref name="slots"/> (roster records): Valor and
    /// Detection Brave, Theology and Evolutionary Faith (3 each way, 3-97), and Transitioning as ReMixed's 0x0E8 does it
    /// (TransitionUnit). How many units it changed.</summary>
    internal static int ApplyTraining(int course, ReadOnlySpan<byte> slots, byte* roster)
    {
        const int empty = 1, brave = 0x1E, faith = 0x1F;
        var changed = 0;
        foreach (var slot in slots)
        {
            if (slot >= UnitTables.RosterSlots)
                continue;
            var unit = roster + slot * UnitTables.RosterSize;
            if (unit[empty] == 0xFF)
                continue;
            switch (course)
            {
                case 89:
                    unit[brave] = (byte)Math.Min(unit[brave] + 3, 97);
                    changed++;
                    break;
                case 90:
                    unit[brave] = (byte)Math.Max(unit[brave] - 3, 3);
                    changed++;
                    break;
                case 91:
                    unit[faith] = (byte)Math.Min(unit[faith] + 3, 97);
                    changed++;
                    break;
                case 92:
                    unit[faith] = (byte)Math.Max(unit[faith] - 3, 3);
                    changed++;
                    break;
                case 95:
                    if (TransitionUnit(unit))
                        changed++;
                    break;
            }
        }
        return changed;
    }

    /// <summary>Transitioning, as ReMixed's 0x0E8 does it to a roster record: the gender byte (+4, the PS1's too) a man's
    /// 0x80 to a woman's 0x40 and back, a generic's sprite set with it (0x80 and 0x81, which pick its look), and the
    /// gendered job: ReMixed's male Bard and female Dancer swap (the remaster's 0x5B and 0x5C), its two Dark Knights are
    /// the remaster's one (0xA0) and stay. 0.9.7's port flipped bits of +6, which isn't the gender: it did nothing to
    /// most units and changed another byte of the rest. Whether the unit changed (not for a monster).</summary>
    internal static bool TransitionUnit(byte* unit)
    {
        const int sprites = 0, job = 2, gender = 4;
        const byte man = 0x80, woman = 0x40, maleGeneric = 0x80, femaleGeneric = 0x81, bard = 0x5B, dancer = 0x5C;
        var before = unit[gender] & 0xC0;
        if (before != man && before != woman)
            return false;
        unit[gender] = before == man ? woman : man;
        unit[sprites] = unit[sprites] switch
        {
            maleGeneric => femaleGeneric,
            femaleGeneric => maleGeneric,
            var other => other,
        };
        unit[job] = unit[job] switch
        {
            bard => dancer,
            dancer => bard,
            var other => other,
        };
        return true;
    }

    internal static string TrainingName(int course) => course switch
    {
        89 => "Valor", 90 => "Detection", 91 => "Theology", 92 => "Evolutionary", 93 => "Training",
        94 => "Retraining", 95 => "Transitioning", _ => $"course {course}",
    };

    internal static int MigrateLegacyRecords()
    {
        var count = Math.Min(*Count, (byte)NativeRecords);
        var kept = 0;
        var removed = 0;
        for (var i = 0; i < count; i++)
        {
            var record = RecordsAt + i * RecordBytes;
            var legacy = !IsDispatchRecord(record) && (record[1] > 95 || record[4] > 63 || record[5] > 3);
            if (!legacy)
            {
                if (kept != i)
                    new ReadOnlySpan<byte>(record, RecordBytes).CopyTo(
                        new Span<byte>(RecordsAt + kept * RecordBytes, RecordBytes));
                kept++;
                continue;
            }
            removed++;
            for (var k = 0; k < 3; k++)
            {
                var slot = record[6 + k];
                if (slot < UnitTables.RosterSlots)
                    _roster[slot * UnitTables.RosterSize + Errands.Errand] = 0;
            }
        }
        if (removed == 0)
            return 0;
        new Span<byte>(RecordsAt + kept * RecordBytes, (count - kept) * RecordBytes).Clear();
        *Count = (byte)kept;
        if (Interlocked.Increment(ref _migrations) <= 2)
            _log.Warn($"dispatch missions: recalled {removed} group(s) left by the retired event-menu screen");
        return removed;
    }

    internal static bool Holds(int id) => States != null && id is >= FirstState and <= LastState;

    internal static int Get(int id)
    {
        var at = id - FirstState;
        return States[at >> 1] >> (at & 1) * 4 & 0xF;
    }

    internal static void Set(int id, int value)
    {
        var at = id - FirstState;
        var shift = (at & 1) * 4;
        States[at >> 1] = (byte)(States[at >> 1] & ~(0xF << shift) | (value & 0xF) << shift);
    }

    internal static int Step(byte* records, int count)
    {
        var due = 0;
        for (var i = 0; i < Math.Min(count, Records); i++)
        {
            var record = records + i * RecordBytes;
            if ((record[0] & 4) != 0)
                continue;
            record[2]++;
            if (record[2] >= record[3])
            {
                record[0] |= 4;
                due++;
            }
        }
        return due;
    }
}
