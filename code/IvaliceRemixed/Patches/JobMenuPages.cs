using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Dark Knight in the job menu. The remaster's job menu has 19 slots and builds its job list with the Dark
/// Knight (0xA0) after them, unseen; ReMixed's 20 jobs (its Dark Knight and its performer slot by gender) need it. A
/// second page, reached by scrolling down past the last row (or up past the first), shows the first page's last row and
/// then the Dark Knight. The unlock follows ReMixed's row for its Dark Knight (Knight 8, Black Mage 8; PS1 SCUS
/// 0x800660BA row 17): the remaster's unlocked-jobs routine (0x348684) works out 19 jobs from its requirement table
/// (.rdata 0x67F390, 12 bytes a job, a level a nibble by job index) and clears the Dark Knight's bit (0x8) unless 20
/// kills and some masteries (War of the Lions' rule); here it works out 20, the Dark Knight's row is ReMixed's, and the
/// clearing is gone.
/// </summary>
/// <remarks>
/// <para>Ported from cipherxof's GenericJobs (https://github.com/cipherxof/FFTGenericJobs, MIT licence; the notice is
/// in THIRD-PARTY-NOTICES.md): its job menu paging (Mod.cs), for the Dark Knight alone. Left out: its Onion Knight,
/// its job tree nodes (JobTree.cs; the tree shows the 19, and opening it from the second page goes back to the first,
/// as its earlier releases did), its Sanguine Sword and effect fixes for the remaster's own Dark Knight abilities
/// (ReMixed's are the port's), and its art (the mod's Dark Knight art is ReMixed's: tools/build_mod.py).</para>
/// <para>When GenericJobs itself is loaded (ffttic.jobs.genericjobs, which pages the same menu), none of the menu's
/// hooks go in, and neither do the unlock's code changes, which its own patterns look for; ReMixed's requirement row is
/// still written, so its Dark Knight opens by ReMixed's rule either way.</para>
/// </remarks>
internal static unsafe class JobMenuPages
{
    internal const string GenericJobsModId = "ffttic.jobs.genericjobs";
    internal const ushort DarkKnightJob = 0xA0;
    /// <summary>The job index of the Dark Knight, and its bit in the unlocked jobs.</summary>
    internal const int DarkKnightIndex = 20, DarkKnightBit = 0x800000 >> DarkKnightIndex;

    private const int PerPage = 19, ListLength = 21, PageTwoStart = 6, SlotCount = 19, SlotStride = 0x100,
        SlotUnit = 0x10, UiVisible = 0x1A0, MenuIndex = 0x4A48, MenuPageData = 0x2270, PageDataSize = 0xB00;
    private const int ChangingJob = 1402, LearningAbility = 1404, ViewingTree = 1409;
    private const ushort End = 0xFFFF, RsmLoopEnd = 0x5E;
    private static readonly ushort[] Extra = [DarkKnightJob];

    // ReMixed's row: Knight (index 2) and Black Mage (index 6) at 8, high nibbles of bytes 1 and 3.
    internal static readonly byte[] DarkKnightRequirement = [0x00, 0x80, 0x00, 0x80, 0, 0, 0, 0, 0, 0, 0, 0];

    // GenericJobs' patterns (names as there), each found once in the exe (checked 2026-11-19).
    internal const string SelectSlotPattern = // Sub1309C0: the menu's selection set to a slot
        "48 89 5C 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 80 B9 ?? ?? ?? ?? ?? 48 8B D9 4C 63 CA";
    internal const string RefreshPattern = // Sub131A6C: the menu's details for its selection
        "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B F1 48 8D 99 ?? ?? ?? ?? 33 FF 83 7B ?? 06";
    internal const string SlotVisiblePattern =
        "48 85 D2 0F 84 ?? ?? ?? ?? 48 8B C4 48 89 58 ?? 48 89 68 ?? 48 89 70 ?? 48 89 78 ?? 41 56 48 83 EC ?? 48 8B DA 41 0F B6 E9 45 84 C0";
    internal const string SlotRootsPattern = "45 33 D2 4C 8D 99 ?? ?? ?? ?? 49 8B 03 4A 89 44 D4 ?? 4D 3B D1"; // +6: the offset
    internal const string BuildListPattern = // Sub363718: the job menu's list and slots
        "48 8B C4 48 89 58 ?? 48 89 70 ?? 48 89 78 ?? 55 41 54 41 55 41 56 41 57 48 8D A8 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? " +
        "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 4C 8D 25";
    internal const string MoveCursorPattern = // Sub12FBB8: the cursor moved
        "48 8B C4 48 89 58 ?? 48 89 68 ?? 48 89 70 ?? 48 89 78 ?? 41 54 41 56 41 57 48 83 EC ?? 45 8B F9 45 8B E0";
    internal const string UpdateListPattern =
        "48 8B C4 48 89 58 ?? 48 89 68 ?? 48 89 70 ?? 48 89 48 ?? 57 41 54 41 55 41 56 41 57 48 83 EC ?? 45 0F B6 48 02";
    internal const string PopulateSlotPattern =
        "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC ?? 49 63 F8 4C 8B EA 48 8B D9";
    internal const string RequirementsPopupPattern =
        "48 8B C4 48 89 58 ?? 48 89 70 ?? 48 89 78 ?? 55 41 54 41 55 41 56 41 57 48 8D A8 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? " +
        "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 48 63 81";
    internal const string RequirementNamePattern = "40 53 48 83 EC ?? 48 8B 81 ?? ?? ?? ?? 48 8B D9 48 89 81 ?? ?? ?? ?? 48 81 C1";
    internal const string MenuClickPattern =
        "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 44 8B B9";
    internal const string MenuStatePattern = "48 8B C4 48 89 58 ?? 48 89 68 ?? 48 89 70 ?? 48 89 78 ?? 41 56 48 83 EC ?? 48 8B 01 41 8B E9";
    /// <summary>The list builder's stores NOPped while a page is put in (GenericJobs' JobPatch1-5): pattern, offset, length.</summary>
    internal static readonly (string Pattern, int At, int Length)[] HeldStores =
    [
        ("89 83 ?? ?? ?? ?? 45 33 C0 8B D7", 0, 6),
        ("66 89 0E EB ?? 0F B7 CB", 0, 3),
        ("83 8F ?? ?? ?? ?? ?? 4C 8D 5C 24", 0, 7),
        ("66 89 1E 41 03 ED", 0, 3),
        ("75 ?? 44 0B C9 66 45 89 0F", 5, 4),
    ];
    /// <summary>The unlock: `and ebx, ~8` (the Dark Knight's bit cleared without War of the Lions' kills; NOPped) and
    /// `mov r15d, 19` (the jobs worked out from the table; 20).</summary>
    internal const string UnlockClearPattern = "83 E3 F7 48 8B 6C 24 ?? 8B C3 48 8B 5C 24";
    internal const string UnlockCountPattern = "41 BF 13 00 00 00 8B C6 8B F9";
    /// <summary>The requirement table's rows 18 (Mime) and 19 (the Dark Knight, War of the Lions' Geomancer, Dragoon,
    /// Samurai and Ninja at 8), in .rdata.</summary>
    internal const string RequirementRowsPattern =
        "88 33 43 33 54 53 55 00 00 00 00 00 00 00 00 00 00 00 88 88 00 00 00 00";
    /// <summary>The job icon's id from 1 to 24, else 0 (`jbe; xor edx, edx`): a jmp past the zeroing, for the Dark
    /// Knight's icon 160.</summary>
    internal const string IconLimitPattern = "76 ?? 33 D2 41 B8 ?? ?? ?? ?? 48 8B CB";
    /// <summary>The reactions, supports and movements to equip, by job: a loop over the 19 jobs from 0x4A to 0x5C
    /// (`cmp ebx, 0x13; jl`; Mime has none) taken to 21, whose 0x5E the hook makes the Dark Knight (GenericJobs'
    /// DK_RSMFixup).</summary>
    internal const string RsmJobsPattern = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 8B 74 24";
    internal const string RsmLoopPattern = "83 FB 13 7C ?? BB";

    private static delegate* unmanaged<int> _buildList;
    private static delegate* unmanaged<nint, nint, int, int, void> _moveCursor;
    private static delegate* unmanaged<nint, int, nint, nint> _updateList;
    private static delegate* unmanaged<nint, nint, int, void> _populateSlot;
    private static delegate* unmanaged<nint, int> _requirementsPopup;
    private static delegate* unmanaged<nint, nint, void> _requirementName;
    private static delegate* unmanaged<nint, int, nint, nint, void> _menuClick;
    private static delegate* unmanaged<nint, nint, uint, short, int, void> _menuState;
    private static delegate* unmanaged<short, ushort, int, nint, int, nint> _rsmJobs;
    private static delegate* unmanaged<nint, int, byte, void> _selectSlot;
    private static delegate* unmanaged<nint, void> _refresh;
    private static delegate* unmanaged<nint, nint, byte, byte, byte> _slotVisible;
    private static int _slotRoots;
    private static (nint At, byte[] Original)[] _held = [];

    private static ushort* _list;
    private static readonly ushort[] Original = new ushort[ListLength];
    private static int _page;
    private static bool _dontReset = true;
    private static nint _menu, _popupMenu;
    private static readonly byte[] PageOne = new byte[PageDataSize];
    private static int _state;
    private static Log _log = null!;
    private static bool _failed;

    // For the offline check.
    internal static nint RequirementRowAt { get; private set; }
    internal static nint UnlockClearAt { get; private set; }
    internal static nint UnlockCountAt { get; private set; }
    internal static nint IconLimitAt { get; private set; }
    internal static nint RsmLoopAt { get; private set; }
    internal static bool Paging { get; private set; }

    /// <summary>Installs the page and ReMixed's unlock; with GenericJobs loaded only ReMixed's requirement row.</summary>
    public static string Install(Game game, bool genericJobsLoaded)
    {
        _log = game.Log;
        var image = game.Image;
        var rows = image.FindUnique(RequirementRowsPattern, ".rdata");
        var row = rows + 12;
        GameImage.Unprotected(row, 12, () => DarkKnightRequirement.CopyTo(new Span<byte>((void*)row, 12)));
        RequirementRowAt = row;
        if (genericJobsLoaded)
            return "the Dark Knight's unlock by ReMixed's row (Knight 8, Black Mage 8); GenericJobs is loaded, so the "
                   + "job menu's second page is its";

        // Everything found before anything is written.
        var clear = image.FindUnique(UnlockClearPattern);
        var count = image.FindUnique(UnlockCountPattern);
        var icon = image.FindUnique(IconLimitPattern);
        var rsmLoop = image.FindUnique(RsmLoopPattern);
        var rsmJobs = image.FindUnique(RsmJobsPattern);
        var selectSlot = image.FindUnique(SelectSlotPattern);
        var refresh = image.FindUnique(RefreshPattern);
        var slotVisible = image.FindUnique(SlotVisiblePattern);
        var slotRoots = image.FindUnique(SlotRootsPattern);
        var buildList = image.FindUnique(BuildListPattern);
        var moveCursor = image.FindUnique(MoveCursorPattern);
        var updateList = image.FindUnique(UpdateListPattern);
        var populateSlot = image.FindUnique(PopulateSlotPattern);
        var popup = image.FindUnique(RequirementsPopupPattern);
        var name = image.FindUnique(RequirementNamePattern);
        var click = image.FindUnique(MenuClickPattern);
        var state = image.FindUnique(MenuStatePattern);
        var held = HeldStores.Select(h => image.FindUnique(h.Pattern) + h.At).ToArray();

        GameImage.WriteCode(clear, [0x90, 0x90, 0x90]);
        GameImage.WriteCode(count + 2, [0x14]);
        GameImage.WriteCode(icon, [0xEB, 0x02]);
        GameImage.WriteCode(rsmLoop + 2, [0x15]);
        UnlockClearAt = clear;
        UnlockCountAt = count;
        IconLimitAt = icon;
        RsmLoopAt = rsmLoop;

        _selectSlot = (delegate* unmanaged<nint, int, byte, void>)selectSlot;
        _refresh = (delegate* unmanaged<nint, void>)refresh;
        _slotVisible = (delegate* unmanaged<nint, nint, byte, byte, byte>)slotVisible;
        _slotRoots = *(int*)(slotRoots + 6);
        _held = held.Select((at, k) => (at, new ReadOnlySpan<byte>((void*)at, HeldStores[k].Length).ToArray())).ToArray();

        var hooks = game.Hooks;
        _rsmJobs = (delegate* unmanaged<short, ushort, int, nint, int, nint>)hooks.Hook(rsmJobs,
            (nint)(delegate* unmanaged<short, ushort, int, nint, int, nint>)&RsmJobs, "the reactions and supports by job");
        _buildList = (delegate* unmanaged<int>)hooks.Hook(buildList, (nint)(delegate* unmanaged<int>)&BuildList,
            "the job menu's list");
        _moveCursor = (delegate* unmanaged<nint, nint, int, int, void>)hooks.Hook(moveCursor,
            (nint)(delegate* unmanaged<nint, nint, int, int, void>)&MoveCursor, "the job menu's cursor");
        _updateList = (delegate* unmanaged<nint, int, nint, nint>)hooks.Hook(updateList,
            (nint)(delegate* unmanaged<nint, int, nint, nint>)&UpdateList, "the job list");
        _populateSlot = (delegate* unmanaged<nint, nint, int, void>)hooks.Hook(populateSlot,
            (nint)(delegate* unmanaged<nint, nint, int, void>)&PopulateSlot, "a job menu slot");
        _requirementsPopup = (delegate* unmanaged<nint, int>)hooks.Hook(popup,
            (nint)(delegate* unmanaged<nint, int>)&RequirementsPopup, "the job requirements popup");
        _requirementName = (delegate* unmanaged<nint, nint, void>)hooks.Hook(name,
            (nint)(delegate* unmanaged<nint, nint, void>)&RequirementName, "a job requirement's name");
        _menuClick = (delegate* unmanaged<nint, int, nint, nint, void>)hooks.Hook(click,
            (nint)(delegate* unmanaged<nint, int, nint, nint, void>)&MenuClick, "the job menu's click");
        _menuState = (delegate* unmanaged<nint, nint, uint, short, int, void>)hooks.Hook(state,
            (nint)(delegate* unmanaged<nint, nint, uint, short, int, void>)&MenuState, "the job menu's state");
        Paging = true;
        return "the Dark Knight on the job menu's second page (scroll past the last row), unlocked by ReMixed's row "
               + "(Knight 8, Black Mage 8)";
    }

    private static void Failed(string where, Exception e)
    {
        if (_failed)
            return;
        _failed = true;
        _log.Error($"job menu pages: {where} failed ({e.Message})");
    }

    /// <summary>The list builder's stores taken out (or put back) while a page is put in its list.</summary>
    private static void HoldStores(bool hold)
    {
        foreach (var (at, original) in _held)
            GameImage.WriteCode(at, hold ? Enumerable.Repeat((byte)0x90, original.Length).ToArray() : original);
    }

    /// <summary>The list as the page shows it: the first page as built; the second the first's last row, then the
    /// Dark Knight (as the builder put it, after the 19, with its flags), then the end.</summary>
    private static void ShowPage()
    {
        var list = _list;
        if (list == null)
            return;
        if (_page == 0)
        {
            for (var k = 0; k < ListLength; k++)
                list[k] = Original[k];
            return;
        }
        for (var k = 0; k < PageTwoStart; k++)
            list[k] = Original[PerPage - PageTwoStart + k];
        for (var k = PageTwoStart; k < PerPage; k++)
        {
            var extra = k - PageTwoStart;
            if (extra < Extra.Length)
            {
                list[k] = Extra[extra];
                if (list[k] == DarkKnightJob)
                    list[k] += (ushort)(Original[PerPage] - DarkKnightJob);
            }
            else
                list[k] = End;
        }
    }

    private static void SetPage(nint menu, nint cursor, int page, int index)
    {
        HoldStores(true);
        _page = page;
        _menu = menu;
        ShowPage();
        *(int*)(menu + MenuIndex) = index;
        _dontReset = true;
        _buildList();
        _dontReset = false;
        _selectSlot(menu, index, 0);
        *(int*)(menu + MenuIndex) = index;
        *(int*)(cursor + 4) = index;
        *(byte*)(cursor + 8) = 1;
        UpdateVisibility(menu);
        HoldStores(false);
    }

    private static void Select(nint menu, nint cursor, int index)
    {
        *(int*)(menu + MenuIndex) = index;
        _selectSlot(menu, index, 0);
        *(int*)(menu + MenuIndex) = index;
        *(int*)(cursor + 4) = index;
        *(byte*)(cursor + 8) = 1;
        _refresh(menu);
        UpdateVisibility(menu);
    }

    private static int SecondPageIndex(int column) => PageTwoStart + Math.Clamp(column, 0, Extra.Length - 1);

    /// <summary>The second page's slots past the Dark Knight hidden (all shown on the first).</summary>
    private static void UpdateVisibility(nint menu, bool showAll = false)
    {
        if (menu == 0 || _slotRoots == 0)
            return;
        var shown = Math.Min(PageTwoStart + Extra.Length, PerPage);
        for (var k = 0; k < SlotCount; k++)
        {
            var visible = showAll || _page == 0 || k < shown;
            var root = *(nint*)(menu + _slotRoots + k * SlotStride + SlotUnit);
            if (root == 0)
                continue;
            _slotVisible(menu, root, (byte)(visible ? 1 : 0), 1);
            *(byte*)(root + UiVisible) = (byte)(visible ? 1 : 0);
        }
    }

    [UnmanagedCallersOnly]
    private static int BuildList()
    {
        try
        {
            // Changing job or learning an ability on the second page: the page built again as it is.
            if (_page > 0 && (_state == ChangingJob || _state == LearningAbility))
            {
                HoldStores(true);
                ShowPage();
                _buildList();
                UpdateVisibility(_menu);
                HoldStores(false);
                return 0;
            }
            if (!_dontReset && _page > 0)
                UpdateVisibility(_menu, showAll: true);
            var result = _buildList();
            if (!_dontReset)
            {
                _page = 0;
                Array.Clear(Original);
                _menu = 0;
            }
            return result;
        }
        catch (Exception e)
        {
            Failed("the list", e);
            return _buildList();
        }
    }

    [UnmanagedCallersOnly]
    private static void MoveCursor(nint menu, nint cursor, int a3, int a4)
    {
        try
        {
            _menu = menu;
            var current = *(int*)(menu + MenuIndex);
            var list = _list;
            // A unit with one job (monsters, guests): as the game has it.
            if (list == null || (_page == 0 && list[1] == End))
            {
                _moveCursor(menu, cursor, a3, a4);
                return;
            }
            if (Original[0] == 0)
                for (var k = 0; k < ListLength; k++)
                    Original[k] = list[k];
            _moveCursor(menu, cursor, a3, a4);
            var next = *(int*)(menu + MenuIndex);

            // Down from the last row, wrapping to the first: to the other page.
            if (current is >= 13 and <= 18 && next is >= 0 and < 6)
            {
                if (_page == 0)
                {
                    Marshal.Copy(menu + MenuPageData, PageOne, 0, PageDataSize);
                    SetPage(menu, cursor, 1, SecondPageIndex(next));
                }
                else
                    SetPage(menu, cursor, 0, next);
                return;
            }
            // Up from the first row, wrapping to the last.
            if (current is >= 0 and <= 5 && next is >= 13 and <= 18)
            {
                if (_page == 1)
                    SetPage(menu, cursor, 0, next);
                else
                {
                    Marshal.Copy(menu + MenuPageData, PageOne, 0, PageDataSize);
                    SetPage(menu, cursor, 1, SecondPageIndex(current));
                }
                return;
            }
            var firstEmpty = PageTwoStart + Extra.Length;
            var onExtra = current >= PageTwoStart && current < firstEmpty;
            if (_page > 0 && onExtra && next >= 13)
            {
                SetPage(menu, cursor, 0, current - PageTwoStart);
                return;
            }
            if (_page > 0 && current is >= 0 and < PageTwoStart && next >= firstEmpty && next < 13)
            {
                Select(menu, cursor, firstEmpty - 1);
                return;
            }
            if (_page > 0 && next >= firstEmpty)
            {
                Select(menu, cursor, current);
                return;
            }
            if (_page > 0)
                UpdateVisibility(menu);
        }
        catch (Exception e)
        {
            Failed("the cursor", e);
        }
    }

    [UnmanagedCallersOnly]
    private static nint UpdateList(nint list, int a2, nint a3)
    {
        _list = (ushort*)list;
        return _updateList(list, a2, a3);
    }

    [UnmanagedCallersOnly]
    private static void PopulateSlot(nint menu, nint job, int index)
    {
        // The Dark Knight's slot on the second page shown even at level 0.
        if (_page > 0 && index >= PageTwoStart && index < Math.Min(PageTwoStart + Extra.Length, PerPage) && *(int*)(job + 0x44) < 1)
            *(int*)(job + 0x44) = 1;
        _populateSlot(menu, job, index);
    }

    [UnmanagedCallersOnly]
    private static void MenuClick(nint menu, int a2, nint a3, nint a4)
    {
        if (_page == 1 && *(int*)(menu + MenuIndex) >= PageTwoStart + Extra.Length)
            return;
        _menuClick(menu, a2, a3, a4);
    }

    [UnmanagedCallersOnly]
    private static void MenuState(nint screen, nint a2, uint a3, short a4, int state)
    {
        try
        {
            var menu = *(nint*)(screen + 0x78);
            _state = state;
            // The job tree has no Dark Knight: viewing it from the second page goes back to the first.
            if (menu != 0 && _page > 0 && state == ViewingTree)
            {
                _page = 0;
                ShowPage();
                _buildList();
                UpdateVisibility(menu);
            }
        }
        catch (Exception e)
        {
            Failed("the menu's state", e);
        }
        _menuState(screen, a2, a3, a4, state);
    }

    [UnmanagedCallersOnly]
    private static int RequirementsPopup(nint menu)
    {
        _popupMenu = menu;
        var saved = new byte[PageDataSize];
        Marshal.Copy(menu + MenuPageData, saved, 0, PageDataSize);
        var result = _requirementsPopup(menu);
        Marshal.Copy(saved, 0, menu + MenuPageData, PageDataSize);
        _popupMenu = 0;
        return result;
    }

    [UnmanagedCallersOnly]
    private static void RequirementName(nint a1, nint a2)
    {
        _requirementName(a1, a2);
        // The popup names the required jobs from the first page's data.
        if (_popupMenu != 0 && _page > 0)
            Marshal.Copy(PageOne, 0, _popupMenu + MenuPageData, PageDataSize);
    }

    [UnmanagedCallersOnly]
    private static nint RsmJobs(short a1, ushort job, int a3, nint a4, int a5)
    {
        // The loop's 21st job (0x5E, past Mime) is the Dark Knight, unless the list itself starts there.
        var list = _list;
        if (list != null && list[0] != RsmLoopEnd && job == RsmLoopEnd)
            job = DarkKnightJob;
        return _rsmJobs(a1, job, a3, a4, a5);
    }
}
