using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// The world map's grey roads (a tester, 2026-11-22: the road from the Zeklaus Desert to Dorter drawn grey in side
/// chapter 1-A, though the party walks it). The remaster draws a road grey while the story hasn't opened it yet: the
/// roads between the next battle and the place the story is headed for. Which roads those are it reads from its own
/// count of the story, the stage, which only its own events move; ReMixed's events don't. So while one of ReMixed's own
/// stories runs (side chapter 1-A, the Algus campaign) the stage is the one the main story stopped at, and its roads are
/// greyed over the roads that story opens: no road is greyed then. And where a ReMixed event stands in for the
/// remaster's that moved the stage (Araguay Woods), the stage follows the story's progress instead.
/// <para>The objective's marker comes from the stage too (the diamond on the place the story is headed for, and a
/// smaller one on the next stop on the way): through 1-A it stood on Dorter, chapter 2's first stop, under 1-A's own
/// objective line ("Travel to the Zeklaus Desert."). In a story of ReMixed's own it stands on that story's
/// destination, where the build knows one (1-A's, from its world-map starts), and nowhere otherwise.</para>
/// </summary>
/// <remarks>
/// Read in the exe (2026-11-25). The stage is the GameFlowManager's ([0x3CD9DE0]: a u32 per quest from +0x18, quest 0
/// the main story), set by the remaster's own event instruction 0xE9 (quest, value; 0x23B5xx calls the setter 0xFF660),
/// which the upgraded events don't have (their 0xE9 is ReMixed's SaveTileData). The world map's start (0x33EC0C) hands
/// the party's place and the stage to each map screen (0x15A754: rdx a u32 pair), which looks up GameProgress (table
/// 0x5C, key 0 and the stage) for the keys of a WorldMapProgressInfo row (table 0x7B) and keeps them at +0xCEC and
/// +0xCF0. Each road's look is then set by 0x15B3AC (at the map's start for all 51, and as a road is revealed): it
/// plays the road's Show or Hide by its flag (0x22C + the road), and asks 0x15DFCC (rcx the screen, edx the road, r8b
/// grey) for R_%s_Color_Grayout when the road is in that row's RouteIds, else R_%s_Color_Default. 0x15DFCC has no other
/// caller. Chapter 1 ends at stage 440, whose row greys roads 11 and 10 (Sweegy Woods to Dorter, the Zeklaus Desert to
/// Dorter: the roads beyond chapter 2's first battle), and 1-A, offered there, walks road 10 at its end.
/// <para>The markers: 0x341094 (no arguments; from the world map's start, before it sets each place up, and from
/// 0x2B340C and 0x2D50E4) takes each quest's stage to its row's TownId1 (+8) and, for the main story, TownId2 (+0xC),
/// keeps them as bytes at 0x11A73F3 + 1 (a side quest's place), + 2 (the next stop) and + 3 (the main story's place;
/// 0x2F or negative for none), and when one changed hands all four to the map screen (+0x9A8, four u32) to show the
/// change. Each place's set-up (0x340FD0) reads those bytes for its marker.</para>
/// </remarks>
internal static unsafe class WorldRoads
{
    // 0x15DFCC: a road's colour (rcx the map screen, edx the road, r8b grey): mov [rsp+0x20], rbx; push rbp, rsi, rdi;
    // lea rbp, [rsp-0x160]; sub rsp, 0x260; the cookie; mov rdi, rcx; mov sil, r8b; mov ecx, 0x61 (the Route table).
    internal const string RoadColourPattern =
        "48 89 5C 24 20 55 56 57 48 8D AC 24 A0 FE FF FF 48 81 EC 60 02 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 50 01 00 00 48 8B F9 41 8A F0 B9 61 00 00 00";
    // 0x15A754: a map screen given the party's place and the stage (rdx: two u32): mov [rsp+8], rbx; push rdi; sub rsp,
    // 0x30; mov eax, [rdx]; mov rdi, rdx; mov [rcx+0xD08], eax; mov rbx, rcx; mov r8d, [rdx+4]; xor edx, edx; lea ecx,
    // [rdx+0x5C] (the GameProgress table).
    internal const string MapStagePattern =
        "48 89 5C 24 08 57 48 83 EC 30 8B 02 48 8B FA 89 81 08 0D 00 00 48 8B D9 44 8B 42 04 33 D2 8D 4A 5C";
    // 0xFF660: the stage's setter (rcx the manager, edx the quest, r8d the stage), which tells the manager's listeners.
    internal const string SetStagePattern =
        "48 89 5C 24 18 48 89 74 24 20 57 48 83 EC 20 F6 41 10 01 8B C2 74 07 44 89 44 81 68 EB 05 44 89 44 81 18";
    // 0x33F240, in the world map's start: mov rax, [the manager]; mov ecx, [rax+0x18]; mov rax, [..]; mov [rbp-0x14], ecx.
    internal const string ManagerPattern = "48 8B 05 ?? ?? ?? ?? 8B 48 18 48 8B 05 ?? ?? ?? ?? 89 4D EC";

    // 0x3410C0, in the markers' function (0x341094 + 0x2C): cmp [a flag], 0; jne its end; mov rsi, [the GameProgress
    // table]; xor edi, edi; movdqa xmm0, [four -1s]; xor ebx, ebx; movups [rsp+0x20], xmm0. The function's lea r8,
    // [the marker bytes] is at +0x288 and its mov rax, [the game's modules] (0x3CD9DA8) at +0x2DB.
    internal const string MarkersPattern =
        "83 3D ?? ?? ?? ?? 00 0F 85 ?? ?? ?? ?? 48 8B 35 ?? ?? ?? ?? 33 FF 66 0F 6F 05 ?? ?? ?? ?? 33 DB 0F 11 44 24 20";
    internal const int MarkersInto = 0x2C, MarkerBytesAt = 0x288, ModulesAt = 0x2DB;

    internal const int Stages = 0x18, OtherStages = 0x68, StageFlags = 0x10, MainStory = 0;
    /// <summary>The marker bytes (from the function's address): the next stop on the way and the main story's place;
    /// and no place.</summary>
    internal const int NextStop = 2, Objective = 3, NoPlace = 0x2F;
    /// <summary>The map screen ([[[modules] + 0x10] + 0xA0] + 0x40) and its four changed markers.</summary>
    internal const int ModuleList = 0x10, WorldModule = 0xA0, MapScreen = 0x40, ChangedMarkers = 0x9A8;

    /// <summary>Where the hooked functions, the setter and the manager's pointer are, for the offline check.</summary>
    internal static nint RoadColourAt, MapStageAt, SetStageAt, ManagerAt, MarkersAt, MarkerBytes, Modules;

    /// <summary>The place each of ReMixed's own stories is headed for, by the story's flag and its progress (var
    /// 0x6E): the build's, from the story's world-map starts.</summary>
    internal static readonly Dictionary<int, Dictionary<int, int>> Destinations = [];

    /// <summary>The flags of ReMixed's own stories (the build's: every story whose world-map steps it writes).</summary>
    internal static int[] OwnStories = [];

    /// <summary>Where the story's progress (var 0x6E) says the stage is further on than the remaster's count: at least
    /// Story, with the stage from From up to (not including) To, makes it To.</summary>
    internal static (int Story, int From, int To)[] Floors = [];

    /// <summary>A test's switch (the harness's "roads off"): roads are left as the remaster colours them, as before
    /// this patch, so a case can show the grey road it puts right.</summary>
    internal static volatile bool Off;

    private static Log _log = null!;
    private static delegate* unmanaged<nint, nint, byte, void> _roadColour;
    private static delegate* unmanaged<nint, uint*, void> _mapStage;
    private static delegate* unmanaged<nint, int, int, void> _setStage;
    private static delegate* unmanaged<void> _markers;
    private static nint* _manager;
    private static int _failed, _marked = -2;

    public static string Install(Game game, JsonElement ownStories, JsonElement floors, JsonElement destinations)
    {
        _log = game.Log;
        var image = game.Image;
        OwnStories = [.. ownStories.EnumerateArray().Select(flag => flag.GetInt32())];
        Floors = [.. floors.EnumerateArray().Select(row => (row[0].GetInt32(), row[1].GetInt32(), row[2].GetInt32()))];
        Destinations.Clear();
        foreach (var story in destinations.EnumerateObject())
            Destinations[int.Parse(story.Name, System.Globalization.CultureInfo.InvariantCulture)] =
                story.Value.EnumerateObject().ToDictionary(
                    step => int.Parse(step.Name, System.Globalization.CultureInfo.InvariantCulture), step => step.Value.GetInt32());
        if (Nemesis.GetVariable == null)
            Nemesis.FindVariables(image);
        RoadColourAt = image.FindUnique(RoadColourPattern);
        MapStageAt = image.FindUnique(MapStagePattern);
        SetStageAt = image.FindUnique(SetStagePattern);
        ManagerAt = GameImage.Rip(image.FindUnique(ManagerPattern), 3, 7);
        _manager = (nint*)ManagerAt;
        _setStage = (delegate* unmanaged<nint, int, int, void>)SetStageAt;
        MarkersAt = image.FindUnique(MarkersPattern) - MarkersInto;
        if ((*(uint*)MarkersAt & 0xFFFFFF) != 0xC48B48 || (*(uint*)(MarkersAt + MarkerBytesAt) & 0xFFFFFF) != 0x058D4C
            || (*(uint*)(MarkersAt + ModulesAt) & 0xFFFFFF) != 0x058B48)
            throw new InvalidDataException("the world map's markers aren't worked out as this DLL expects");
        MarkerBytes = GameImage.Rip(MarkersAt + MarkerBytesAt, 3, 7);
        Modules = GameImage.Rip(MarkersAt + ModulesAt, 3, 7);
        _roadColour = (delegate* unmanaged<nint, nint, byte, void>)game.Hooks.Hook(
            RoadColourAt, (nint)(delegate* unmanaged<nint, nint, byte, void>)&RoadColour, "a world-map road's colour");
        _mapStage = (delegate* unmanaged<nint, uint*, void>)game.Hooks.Hook(
            MapStageAt, (nint)(delegate* unmanaged<nint, uint*, void>)&MapStage, "the world map's story stage");
        _markers = (delegate* unmanaged<void>)game.Hooks.Hook(
            MarkersAt, (nint)(delegate* unmanaged<void>)&Markers, "the world map's objective markers");
        return $"no world-map road greyed while a story of ReMixed's own runs (flags {string.Join(", ", OwnStories.Select(f => $"0x{f:X}"))}), "
               + $"the objective's marker on that story's destination ({Destinations.Sum(story => story.Value.Count)} known), "
               + $"and {Floors.Length} stage(s) of the remaster's that follow the story's progress";
    }

    /// <summary>The place the running story of ReMixed's own is headed for at this progress, or -1 with none known.</summary>
    internal static int Destination(Func<int, int> variable, int story)
    {
        foreach (var flag in OwnStories)
            if (variable(flag) != 0)
                return Destinations.TryGetValue(flag, out var steps) && steps.TryGetValue(story, out var place) ? place : -1;
        return -1;
    }

    private static nint Map()
    {
        var modules = Modules == 0 ? 0 : *(nint*)Modules;
        var list = modules == 0 ? 0 : *(nint*)(modules + ModuleList);
        var world = list == 0 ? 0 : *(nint*)(list + WorldModule);
        return world == 0 ? 0 : *(nint*)(world + MapScreen);
    }

    /// <summary>Whether one of ReMixed's own stories runs: its roads are its own, not the remaster's stage's.</summary>
    internal static bool OwnStory(Func<int, int> variable)
    {
        foreach (var flag in OwnStories)
            if (variable(flag) != 0)
                return true;
        return false;
    }

    /// <summary>The stage the story's progress says it is at least: the furthest To of the rules that hold.</summary>
    internal static int Floor(int stage, int story)
    {
        foreach (var (at, from, to) in Floors)
            if (story >= at && stage >= from && stage < to)
                stage = to;
        return stage;
    }

    /// <summary>The main story's stage as the game has it, or -1 before the manager is there.</summary>
    internal static int Stage(int quest = MainStory)
    {
        var manager = _manager == null ? 0 : *_manager;
        return manager == 0 ? -1 : *(int*)(manager + Stages + quest * 4);
    }

    /// <summary>A stage written where the game keeps it, for a test (no listener told).</summary>
    internal static bool Poke(int quest, int stage)
    {
        var manager = _manager == null ? 0 : *_manager;
        if (manager == 0)
            return false;
        *(int*)(manager + Stages + quest * 4) = stage;
        return true;
    }

    [UnmanagedCallersOnly]
    private static void RoadColour(nint map, nint route, byte grey)
    {
        try
        {
            if (grey != 0 && OwnStory(id => Nemesis.GetVariable(id)))
            {
                if (Off)
                    _log.Info($"world roads: road {(int)route} left grey, as the remaster's stage {Stage()} has it (a test's)");
                else
                {
                    grey = 0;
                    _log.Info($"world roads: road {(int)route} drawn in colour (the remaster's stage {Stage()} greys it; "
                              + "a story of ReMixed's own runs)");
                }
            }
        }
        catch (Exception e)
        {
            if (_failed++ < 3)
                _log.Error($"world roads: {e.Message}");
        }
        _roadColour(map, route, grey);
    }

    /// <summary>The markers as the remaster works them out from its stage; then, in a story of ReMixed's own, the main
    /// story's on that story's destination and none on a next stop, with the map screen's changed markers as they were
    /// (the remaster's would show a change to its own stage's place each time).</summary>
    [UnmanagedCallersOnly]
    private static void Markers()
    {
        var own = false;
        nint map = 0;
        long first = 0, second = 0;
        try
        {
            own = !Off && OwnStory(id => Nemesis.GetVariable(id));
            map = own ? Map() : 0;
            if (map != 0)
                (first, second) = (*(long*)(map + ChangedMarkers), *(long*)(map + ChangedMarkers + 8));
        }
        catch (Exception e)
        {
            if (_failed++ < 3)
                _log.Error($"world roads: {e.Message}");
        }
        _markers();
        if (!own)
            return;
        try
        {
            if (map != 0 && Map() == map)
                (*(long*)(map + ChangedMarkers), *(long*)(map + ChangedMarkers + 8)) = (first, second);
            var story = Nemesis.GetVariable(RemixedScenarios.StoryProgress);
            var place = Destination(id => Nemesis.GetVariable(id), story);
            var bytes = (byte*)MarkerBytes;
            bytes[NextStop] = NoPlace;
            bytes[Objective] = (byte)(place < 0 ? NoPlace : place);
            if (_marked != (place << 8 | story & 0xFF))
            {
                _marked = place << 8 | story & 0xFF;
                _log.Info(place < 0
                    ? $"world roads: no objective marker (the story's progress is 0x{story:X}; the remaster's stage is {Stage()})"
                    : $"world roads: the objective's marker on place {place} (the story's progress is 0x{story:X}; the remaster's stage is {Stage()})");
            }
        }
        catch (Exception e)
        {
            if (_failed++ < 3)
                _log.Error($"world roads: {e.Message}");
        }
    }

    [UnmanagedCallersOnly]
    private static void MapStage(nint map, uint* place)
    {
        try
        {
            var stage = (int)place[1];
            var manager = *_manager;
            if (manager != 0 && !OwnStory(id => Nemesis.GetVariable(id)))
            {
                var story = Nemesis.GetVariable(RemixedScenarios.StoryProgress);
                var to = Floor(stage, story);
                if (to != stage)
                {
                    _setStage(manager, MainStory, to);
                    place[1] = (uint)to;
                    _log.Info($"world roads: the remaster's stage {stage} is {to} (the story's progress is 0x{story:X})");
                }
            }
        }
        catch (Exception e)
        {
            if (_failed++ < 3)
                _log.Error($"world roads: {e.Message}");
        }
        _mapStage(map, place);
    }
}
