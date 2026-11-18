using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// The ReMixed tab of the in-game Options screen (asked 2026-11-11: a page in the game's own Options, laid out as
/// ReMixed's Extra Options menu). Its first row, "Extra Options", picks one of ReMixed's groups (Unit Levels, Stats &amp;
/// Equipment; Permadeath; JP and EXP; Spellcasting; Combat Adjustments; Nemesis System) and the rows below it are that
/// group's options, each a list of its choices; a change goes into the save in play (<see cref="SaveOptions"/>) at once.
/// tools/build_mod.py (OPTIONS_PAGE) adds the tab's category and texts to the game's tables and lists the groups in
/// options-page.json beside the DLL.
/// </summary>
/// <remarks>
/// The screen (docs/REMASTER.md, "The Options screen") lists its tabs as ConfigCategory ids (+0x5C, the count at
/// +0x58: the in-game screen 5, 3, 4, 0, 2, 1; its layout has seven tab buttons), each tab's rows as ConfigItem ids
/// (+0xA0, the count at +0x9C), and each item's state in arrays sized for ids 0-60 (+0x194 its choice's index, +0x288
/// its value, +0x37C the index before). So the tab's rows are ids the game leaves empty, and the mod points those rows'
/// entries in the ConfigItem table at rows of its own (a list, its name and description texts), rewritten for the
/// group shown. The screen's row builder (0x18EA74) adds only the ids it knows, one check each, so for the mod's tab
/// the rows go in as the builder hands its count to the list (0x28D7BC, the list at the screen's +0x6D0): the menu
/// row and the group's options, and that count. A list item's choices come from a table
/// the screen picks by the item's id in code (0x190194, which shows a row's value and steps it), so for one of the
/// mod's rows the hook runs it as the game's Fast-forward item (0x16) with that item's cached table (0x143) swapped for
/// one of the mod's (the choices: a value and a UI text id each), then puts the Fast-forward item's state back. The
/// screen's "item changed" (0x1922F4, which puts a value into the game's config) passes over the mod's rows, and
/// Restore Defaults (which resets the tab's items one by one through 0xB6498) puts the group's options back to the
/// mod's settings, which a new save starts with.
/// </remarks>
internal static unsafe class OptionsPage
{
    // 0x192FA8: the screen's setup runs it right after its tab list is set (rcx the screen).
    internal const string TabsSet =
        "48 89 5C 24 10 55 48 8B EC 48 83 EC 60 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 F0 48 8B 81 40 0D 00 00 48 8B D9 48 89 81 48 0D 00 00";
    // 0x190194: shows a row's value and steps it (rcx the screen, edx the row, r8d the step, r9b fast, [rsp+0x28]
    // force); returns the value.
    internal const string ShowRow =
        "48 89 5C 24 20 55 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 30 FD FF FF 48 81 EC D0 03 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 C0 02 00 00 4C 8B E9 44 88 4C 24 40 48 8B 49 18";
    // 0x1922F4: an item's new value into the game's config (rcx the screen, edx the item, r8d the value, r9b).
    internal const string ItemChanged =
        "48 89 5C 24 20 55 56 57 41 56 41 57 48 83 EC 20 33 DB 8B FA 45 8B F8 48 8B F1 45 84 C9 75 65 85 D2 74 2D 83 FF 01";
    // 0x18EA74: builds a tab's rows (rcx the screen, edx the tab's index), as a tab switch does.
    internal const string BuildTab =
        "48 89 5C 24 18 55 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 70 FF FF FF 48 81 EC 90 01 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 80 00 00 00 48 8B 05 ?? ?? ?? ?? 33 F6 44 8B 61 54";
    // 0xB6498: an item back to its default. It loads the ConfigItem table's cache, the tables and their getter.
    internal const string ItemDefault =
        "48 89 5C 24 18 57 48 83 EC 20 48 8B 05 ?? ?? ?? ?? 8B DA 48 8B F9 48 85 C0 75 22 48 8B 0D ?? ?? ?? ?? 48 85 C9 74 7A BA FA 00 00 00 E8 ?? ?? ?? ?? 48 89 05";
    // The Fast-forward list's table (0x143) into the global the screen caches it in (0x18DB52).
    internal const string HostTableLoad = "BA 43 01 00 00 E8 ?? ?? ?? ?? 48 89 05 ?? ?? ?? ??";
    // 0x3D3AE8: a table's row by key (rcx the table, edx the key); returns its entry (kind, pointer), or 0.
    internal const string RowByKey = "48 8B 41 38 48 85 C0 74 65 48 83 E8 01 74 19 48 83 F8 01 74 0B 45 33 C9 45 33 C0 E9";
    // 0x28D7BC: a list's row count (rcx the list, edx the count); the screen's row builder calls it for its list.
    internal const string ListCount =
        "48 89 5C 24 08 57 48 83 EC 20 48 8B D9 8B FA 48 8B 49 18 48 85 C9 0F 84 ?? ?? ?? ?? 83 4B 60 FF 85 D2 0F 9F C0 88 81 A0 01 00 00";

    internal const string FileName = "options-page.json";
    internal const int ConfigItemTable = 0xFA, ConfigCategoryTable = 0xF9, UiTextTable = 0x13;
    internal const int Host = 0x16; // the Fast-forward item: a list whose table the screen keeps in a global
    internal const int GeneralOptions = 0x34, Tab = 0x54, TabCount = 0x58, Tabs = 0x5C, MaxTabs = 7, RowCount = 0x9C,
        RowItems = 0xA0, Indices = 0x194, Values = 0x288, Before = 0x37C, List = 0x6D0;
    private const int RowBytes = 0x50, MaxChoices = 16;

    private static Log _log = null!;
    private static delegate* unmanaged<nint, nint, void> _tabsSet;
    private static delegate* unmanaged<nint, int, int, byte, byte, uint> _showRow;
    private static delegate* unmanaged<nint, int, int, byte, void> _itemChanged;
    private static delegate* unmanaged<nint, int, void> _buildTab;
    private static delegate* unmanaged<nint, int, nint> _getTable;
    private static delegate* unmanaged<nint, int, nint> _rowByKey;
    private static delegate* unmanaged<nint, int, void> _listCount;
    private static delegate* unmanaged<nint, int, void> _itemDefault;
    private static nint _screen, _screenType; // the in-game Options screen last set up, and its vtable
    private static nint* _tables, _hostTable;

    private static Page _page = null!;
    private static int _group;
    private static bool _ready, _inside;
    private static nint _rows;   // a row (a key, an offset, its data) for each slot
    private static nint _choices; // the table the screen reads a list's choices from
    private static int _failed;

    internal sealed record Choice(object? Value, int Text);
    internal sealed record Option(PropertyInfo Setting, int Name, int Description, Choice[] Choices);
    internal sealed record Group(int Name, Option[] Options);
    internal sealed record Page(int Category, int[] Slots, int MenuName, int MenuDescription, int Other, Group[] Groups);

    public static void Install(Game game, string path)
    {
        _log = game.Log;
        _page = Load(path);
        var at = game.Image.FindUnique(ItemDefault);
        _tables = (nint*)GameImage.Rip(at + 27, 3, 7);
        _itemDefault = (delegate* unmanaged<nint, int, void>)game.Hooks.Hook(
            at, (nint)(delegate* unmanaged<nint, int, void>)&OnItemDefault, "the Options items' defaults");
        _getTable = (delegate* unmanaged<nint, int, nint>)GameImage.Rip(at + 44, 1, 5);
        _hostTable = (nint*)GameImage.Rip(game.Image.FindUnique(HostTableLoad) + 10, 3, 7);
        _rowByKey = (delegate* unmanaged<nint, int, nint>)game.Image.FindUnique(RowByKey);
        _buildTab = (delegate* unmanaged<nint, int, void>)game.Image.FindUnique(BuildTab);
        _rows = Marshal.AllocHGlobal(_page.Slots.Length * (8 + RowBytes));
        new Span<byte>((void*)_rows, _page.Slots.Length * (8 + RowBytes)).Clear();
        _choices = Marshal.AllocHGlobal(ChoicesBytes);
        new Span<byte>((void*)_choices, ChoicesBytes).Clear();
        _tabsSet = (delegate* unmanaged<nint, nint, void>)game.Hooks.Hook(
            game.Image.FindUnique(TabsSet), (nint)(delegate* unmanaged<nint, nint, void>)&OnTabsSet, "the Options screen's tabs");
        _showRow = (delegate* unmanaged<nint, int, int, byte, byte, uint>)game.Hooks.Hook(
            game.Image.FindUnique(ShowRow), (nint)(delegate* unmanaged<nint, int, int, byte, byte, uint>)&OnShowRow,
            "the Options screen's row values");
        _itemChanged = (delegate* unmanaged<nint, int, int, byte, void>)game.Hooks.Hook(
            game.Image.FindUnique(ItemChanged), (nint)(delegate* unmanaged<nint, int, int, byte, void>)&OnItemChanged,
            "the Options screen's item changes");
        _listCount = (delegate* unmanaged<nint, int, void>)game.Hooks.Hook(
            game.Image.FindUnique(ListCount), (nint)(delegate* unmanaged<nint, int, void>)&OnListCount,
            "the lists' row counts");
        _log.Info($"options page: {_page.Groups.Sum(g => g.Options.Length)} options in {_page.Groups.Length} groups, " +
                  $"on a tab of the in-game Options screen");
    }

    /// <summary>The page's groups and options from the build's list (OPTIONS_PAGE in tools/build_mod.py).</summary>
    internal static Page Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var groups = new List<Group>();
        foreach (var g in root.GetProperty("groups").EnumerateArray())
        {
            var options = new List<Option>();
            foreach (var o in g.GetProperty("options").EnumerateArray())
            {
                var name = o.GetProperty("setting").GetString()!;
                var setting = typeof(Options).GetProperty(name)
                              ?? throw new InvalidDataException($"the options page names a setting the mod hasn't: {name}");
                var choices = o.GetProperty("choices").EnumerateArray()
                    .Select(c => new Choice(Value(setting.PropertyType, c.GetProperty("value")), c.GetProperty("text").GetInt32()))
                    .ToArray();
                if (choices.Length is 0 or > MaxChoices - 1)
                    throw new InvalidDataException($"the options page's {name} has {choices.Length} choices");
                options.Add(new Option(setting, o.GetProperty("name").GetInt32(), o.GetProperty("description").GetInt32(), choices));
            }
            groups.Add(new Group(g.GetProperty("name").GetInt32(), options.ToArray()));
        }
        var slots = root.GetProperty("slots").EnumerateArray().Select(s => s.GetInt32()).ToArray();
        if (groups.Count is 0 or > MaxChoices || groups.Any(g => g.Options.Length is 0 || g.Options.Length >= slots.Length))
            throw new InvalidDataException("the options page's groups don't fit its rows");
        if (slots.Any(s => s is < 0 or > 60 || s == Host))
            throw new InvalidDataException("the options page's rows must be the screen's item ids 0-60, Fast-forward's left alone");
        var menu = root.GetProperty("menu");
        return new Page(root.GetProperty("category").GetInt32(), slots, menu.GetProperty("name").GetInt32(),
                        menu.GetProperty("description").GetInt32(), root.GetProperty("other").GetInt32(), groups.ToArray());
    }

    private static object Value(Type type, JsonElement value) =>
        type.IsEnum ? Enum.Parse(type, value.GetString()!)
        : type == typeof(bool) ? value.GetBoolean()
        : value.GetInt32();

    /// <summary>The index of <paramref name="option"/>'s choice that <paramref name="options"/> has, or -1.</summary>
    internal static int Current(Option option, Options options)
    {
        var value = option.Setting.GetValue(options);
        return Array.FindIndex(option.Choices, c => Equals(c.Value, value));
    }

    /// <summary><paramref name="options"/> with <paramref name="option"/> at its choice <paramref name="index"/>.</summary>
    internal static Options With(Option option, int index, Options options)
    {
        var next = options.Copy();
        option.Setting.SetValue(next, option.Choices[index].Value);
        return next;
    }

    // --- the screen

    [UnmanagedCallersOnly]
    private static void OnTabsSet(nint screen, nint unused)
    {
        try
        {
            if (*(byte*)(screen + GeneralOptions) == 0 && Ready())
            {
                var count = *(int*)(screen + TabCount);
                var tabs = (int*)(screen + Tabs);
                if (count < MaxTabs && !new Span<int>(tabs, count).Contains(_page.Category))
                {
                    tabs[count] = _page.Category;
                    *(int*)(screen + TabCount) = count + 1;
                }
                (_screen, _screenType) = (screen, *(nint*)screen);
            }
        }
        catch (Exception e)
        {
            Failed("adding the tab", e);
        }
        _tabsSet(screen, unused);
    }

    [UnmanagedCallersOnly]
    private static uint OnShowRow(nint screen, int row, int step, byte fast, byte force)
    {
        var slot = *(int*)(screen + RowItems + row * 4);
        var at = _ready ? Array.IndexOf(_page.Slots, slot) : -1;
        if (at < 0 || _inside)
            return _showRow(screen, row, step, fast, force);
        try
        {
            return Show(screen, row, step, fast, force, slot, at);
        }
        catch (Exception e)
        {
            Failed("a row", e);
            return 0;
        }
    }

    /// <summary>The screen's row builder hands its list the count of the rows it added: none for the mod's tab, whose
    /// item ids it doesn't know, so they go in here.</summary>
    [UnmanagedCallersOnly]
    private static void OnListCount(nint list, int count)
    {
        try
        {
            var screen = _screen;
            if (_ready && screen != 0 && list == screen + List && Shown(screen))
            {
                count = 1 + _page.Groups[_group].Options.Length;
                for (var k = 0; k < count; k++)
                    *(int*)(screen + RowItems + k * 4) = _page.Slots[k];
                *(int*)(screen + RowCount) = count;
            }
        }
        catch (Exception e)
        {
            Failed("the tab's rows", e);
        }
        _listCount(list, count);
    }

    /// <summary>An item back to its default: for one of the page's options shown, the mod's setting for it (in the
    /// save in play); the game's items as the game does.</summary>
    [UnmanagedCallersOnly]
    private static void OnItemDefault(nint config, int item)
    {
        var at = _ready ? Array.IndexOf(_page.Slots, item) : -1;
        if (at < 0)
        {
            _itemDefault(config, item);
            return;
        }
        try
        {
            var screen = _screen;
            var group = _page.Groups[_group];
            if (at == 0 || at > group.Options.Length || screen == 0 || !Shown(screen))
                return; // the menu row, or not from the tab
            var option = group.Options[at - 1];
            var value = option.Setting.GetValue(Options.Settings);
            if (Equals(option.Setting.GetValue(Options.Current), value))
                return;
            var next = Options.Current.Copy();
            option.Setting.SetValue(next, value);
            SaveOptions.Save(next);
            _log.Info($"options page: {option.Setting.Name} back to the mod's setting, {value}, for the save in play");
        }
        catch (Exception e)
        {
            Failed("restoring a default", e);
        }
    }

    /// <summary>Whether the in-game Options screen last set up (still that screen: its vtable) shows the tab.</summary>
    private static bool Shown(nint screen) =>
        Harness.Readable(screen, Before) && *(nint*)screen == _screenType
        && *(int*)(screen + Tab) is >= 0 and < MaxTabs && *(int*)(screen + Tabs + *(int*)(screen + Tab) * 4) == _page.Category;

    [UnmanagedCallersOnly]
    private static void OnItemChanged(nint screen, int item, int value, byte flag)
    {
        if (_inside || (_ready && Array.IndexOf(_page.Slots, item) >= 0))
            return; // the page's own rows, and the Fast-forward item while it stands in for one of them
        _itemChanged(screen, item, value, flag);
    }

    /// <summary>One of the page's rows shown and stepped as the Fast-forward item (a list), with its choices.</summary>
    private static uint Show(nint screen, int row, int step, byte fast, byte force, int slot, int at)
    {
        Option? option = null;
        int current;
        var shown = new List<int>();
        if (at == 0)
        {
            shown.AddRange(_page.Groups.Select(g => g.Name));
            current = _group;
        }
        else
        {
            var group = _page.Groups[_group];
            if (at > group.Options.Length)
                return 0;
            option = group.Options[at - 1];
            shown.AddRange(option.Choices.Select(c => c.Text));
            current = Current(option, Options.Current);
            if (current < 0)
            {
                shown.Add(_page.Other);
                current = shown.Count - 1;
            }
        }
        FillChoices(shown);
        var indices = (int*)(screen + Indices);
        var values = (int*)(screen + Values);
        var before = (int*)(screen + Before);
        var item = (int*)(screen + RowItems + row * 4);
        var saved = (indices[Host], values[Host], before[Host], *_hostTable);
        indices[Host] = current;
        *item = Host;
        *_hostTable = _choices;
        _inside = true;
        uint result;
        int picked;
        try
        {
            result = _showRow(screen, row, step, fast, force);
        }
        finally
        {
            picked = indices[Host];
            *item = slot;
            (indices[Host], values[Host], before[Host], *_hostTable) = saved;
            _inside = false;
        }
        if (picked == current || picked < 0 || picked >= shown.Count)
            return result;
        if (option == null)
        {
            _group = picked;
            Layout();
            _buildTab(screen, *(int*)(screen + Tab)); // the group's rows, as a tab switch builds them
            _log.Info($"options page: showing group {_group + 1} of {_page.Groups.Length}");
        }
        else if (picked < option.Choices.Length)
        {
            var next = With(option, picked, Options.Current);
            SaveOptions.Save(next);
            _log.Info($"options page: {option.Setting.Name} set to {option.Setting.GetValue(next)}, for the save in play");
        }
        return result;
    }

    // --- the tables

    /// <summary>Points the page's rows at the mod's own the first time the screen is set up (the tables are loaded by
    /// then).</summary>
    private static bool Ready()
    {
        if (_ready)
            return true;
        if (*_tables == 0)
            return false;
        var items = _getTable(*_tables, ConfigItemTable);
        var categories = _getTable(*_tables, ConfigCategoryTable);
        if (items == 0 || categories == 0)
            return false;
        _category = Data(_rowByKey(categories, _page.Category));
        if (_category == 0)
            throw new InvalidDataException($"the game's ConfigCategory table has no row {_page.Category} (the build's tab)");
        // The screen takes a text it doesn't find as a crash (the tab's name, 0x18E944), so every text must be there.
        var ui = _getTable(*_tables, UiTextTable);
        var missing = Texts().Append(*(int*)(_category + 0xC)).Where(t => ui == 0 || _rowByKey(ui, t) == 0).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"the game's UI text table has no row {string.Join(", ", missing.Take(4))}" +
                                           $"{(missing.Length > 4 ? $" (and {missing.Length - 4} more)" : "")}, the build's texts");
        for (var k = 0; k < _page.Slots.Length; k++)
        {
            var entry = (long*)_rowByKey(items, _page.Slots[k]);
            if (entry == null)
                throw new InvalidDataException($"the game's ConfigItem table has no row {_page.Slots[k]}");
            var own = _rows + k * (8 + RowBytes);
            *(int*)own = _page.Slots[k]; // its key
            *(int*)(own + 4) = 8;        // its data, after these two
            GameImage.MakeWritable((nint)entry, 16);
            entry[0] = 1;
            entry[1] = own;
        }
        Layout();
        _ready = true;
        _log.Info("options page: its rows are in the game's tables");
        return true;
    }

    private static nint _category;

    /// <summary>Every UI text the page shows.</summary>
    internal static IEnumerable<int> Texts() =>
        new[] { _page.MenuName, _page.MenuDescription, _page.Other }
            .Concat(_page.Groups.Select(g => g.Name))
            .Concat(_page.Groups.SelectMany(g => g.Options).SelectMany(o => o.Choices.Select(c => c.Text).Append(o.Name).Append(o.Description)));

    /// <summary>The rows for the group shown: the menu row, then its options.</summary>
    private static void Layout()
    {
        var group = _page.Groups[_group];
        for (var k = 0; k < _page.Slots.Length; k++)
        {
            var data = _rows + k * (8 + RowBytes) + 8;
            new Span<byte>((void*)data, RowBytes).Clear();
            var (name, description) = k == 0 ? (_page.MenuName, _page.MenuDescription)
                : k <= group.Options.Length ? (group.Options[k - 1].Name, group.Options[k - 1].Description)
                : (_page.MenuName, _page.MenuDescription);
            *(int*)(data + 4) = 1;            // a list
            *(int*)(data + 0x38) = name;
            *(int*)(data + 0x3C) = description;
            *(int*)(data + 0x40) = description;
            *(int*)(data + 0x44) = description;
        }
    }

    internal const int ChoicesBytes = 0x40 + 0x40 + 0x30 + MaxChoices * 0x10 + MaxChoices * 0x18;

    /// <summary>The choices table: a table of the game's single-keyed kind (+0x38 = 1) with its row count where the
    /// screen reads it (+0x30 → +0x38 → +0x24) and in +0x28, its entries at +0x10 (kind 1 and a row: its key, the
    /// offset of its data, then the data, whose +4 is the value and +8 the UI text).</summary>
    private static void FillChoices(List<int> texts) => FillChoices(_choices, texts);

    internal static void FillChoices(nint table, List<int> texts)
    {
        new Span<byte>((void*)table, ChoicesBytes).Clear();
        var outer = table + 0x40;
        var header = outer + 0x40;
        var entries = header + 0x30;
        var rows = entries + MaxChoices * 0x10;
        *(long*)(table + 0x10) = entries;
        *(long*)(table + 0x28) = texts.Count;
        *(long*)(table + 0x30) = outer;
        *(long*)(table + 0x38) = 1;
        *(long*)(outer + 0x38) = header;
        *(int*)(header + 0x24) = texts.Count;
        for (var k = 0; k < texts.Count; k++)
        {
            var row = rows + k * 0x18;
            *(int*)row = k;
            *(int*)(row + 4) = 8;
            *(int*)(row + 8 + 4) = k;        // the value: the choice's index
            *(int*)(row + 8 + 8) = texts[k]; // its text
            *(long*)(entries + k * 0x10) = 1;
            *(long*)(entries + k * 0x10 + 8) = row;
        }
    }

    /// <summary>A list table's row count and its row <paramref name="index"/>'s value and text, read as the screen
    /// reads them (0x190194: the count through +0x30, the entry by index as 0x3D3BE4 finds it for kind 1).</summary>
    internal static (int Count, int Value, int Text) ReadChoice(nint table, int index)
    {
        var count = *(int*)(*(nint*)(*(nint*)(table + 0x30) + 0x38) + 0x24);
        if (*(long*)(table + 0x38) != 1 || (ulong)index >= *(ulong*)(table + 0x28))
            return (count, -1, -1);
        var data = Data(*(nint*)(table + 0x10) + index * 0x10);
        return (count, *(int*)(data + 4), *(int*)(data + 8));
    }

    /// <summary>A row's data in one of the game's tables by its key (0 before the tables load, or with no such row):
    /// for <see cref="RemixedText"/>'s names.</summary>
    internal static nint Row(int table, int key)
    {
        if (_getTable == null || _rowByKey == null || _tables == null || *_tables == 0)
            return 0;
        var found = _getTable(*_tables, table);
        return found == 0 ? 0 : Data(_rowByKey(found, key));
    }

    /// <summary>A table entry's data, as the game's 0x3D2FD8 finds it.</summary>
    internal static nint Data(nint entry)
    {
        if (entry == 0)
            return 0;
        var kind = *(long*)entry & long.MaxValue;
        var row = *(nint*)(entry + 8);
        return kind switch
        {
            0 => 0,
            1 => row + *(int*)(row + 4),
            2 => row + *(int*)(row + 8),
            _ => row + *(int*)(row + 0x10),
        };
    }

    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _log.Error($"options page: {what} failed: {e.Message}");
    }
}
