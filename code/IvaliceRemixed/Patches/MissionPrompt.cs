using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's missions taken from the Tavern's Missions list with the confirm button (A, Enter or F) and a Yes/No
/// question, not Start (asked 2026-11-19: "can't we just press A"; on the Deck Start is the button held to skip
/// scenes). ReMixed's own list (WORLD 0x8013E4EC) takes a mission with Start while its text is up and asks nothing
/// more; the port's list shows each posting's text beside it and took Start the same way. Now the confirm button on a
/// posting asks in the list's prompt line, "Accept this mission? It begins as you leave town. (confirm) Yes (back) No"
/// (the build's DISPATCH_UI_TEXT, with the remaster's own button icons): the confirm button accepts it
/// (<see cref="RemixedScenarios.Accepted"/>: its battle starts once the world map is back) and the line says so; the
/// back button leaves the list as it was. Installed at startup.
/// </summary>
/// <remarks>
/// The list is the Tavern screen's rumor list (its +0x70, the remaster's GossipMain), whose frame (0x1A0100: rcx the
/// list, edx the Tavern's state, 2 while the list is up, r8 the next state it asks for) reads its input: its cursor and
/// the back button, which closes it. While the question is up that frame doesn't run, so no press moves the cursor or
/// closes the list; the answer is read through the action test (0x0FC598: the UI's pressed check on the keyboard's and
/// the pad's bindings) for Confirm (0x1A) and Back (0x1B), on a later frame than the press that asked. A first try put
/// the question in the dispatch addon's box (its "Abandon Errand?"), but that box is the errand confirmation window: it
/// showed empty fee and duration rows and three placeholder units, and left the hand cursor where its Yes had been
/// (seen in the hidden PC game, 2026-11-19).
/// </remarks>
internal static unsafe class MissionPrompt
{
    // 0x1A0100: the rumor list's frame (rcx the list, edx the Tavern's state, r8 the next state): mov [rsp+8], rbx;
    // mov [rsp+0x10], rsi; mov [rsp+0x18], rdi; push r14; sub rsp, 0x40; mov rax, [rcx]; mov r14, r8; mov edi, edx;
    // mov rbx, rcx; call [rax+0x18]; test al, al; je.
    internal const string ListFramePattern =
        "48 89 5C 24 08 48 89 74 24 10 48 89 7C 24 18 41 56 48 83 EC 40 48 8B 01 4D 8B F0 8B FA 48 8B D9 FF 50 18 84 C0 0F 84";
    internal const int Confirm = 0x1A, Back = 0x1B, ListState = 2;

    private static Log _log = null!;
    private static delegate* unmanaged<nint, int, int*, void> _listFrame;
    private static delegate* unmanaged<uint, int, uint, byte, byte> _actionPressed;
    /// <summary>The posting asked about (its index in <see cref="RemixedScenarios.Postings"/>), or -1, and the world
    /// frame it was asked on.</summary>
    private static int _asking = -1, _askedFrame;

    /// <summary>For the offline check.</summary>
    internal static nint ListFrameAt, ActionPressedAt;
    internal static bool Asking => _asking >= 0;

    public static void Install(Game game)
    {
        _log = game.Log;
        Find(game.Image);
        _actionPressed = (delegate* unmanaged<uint, int, uint, byte, byte>)ActionPressedAt;
        _listFrame = (delegate* unmanaged<nint, int, int*, void>)game.Hooks.Hook(
            ListFrameAt, (nint)(delegate* unmanaged<nint, int, int*, void>)&OnListFrame, "the Tavern's rumor list's frame");
        _log.Info("missions: the confirm button on a posting in the Tavern's Missions asks in its prompt line; Yes accepts");
    }

    internal static void Find(GameImage image)
    {
        ListFrameAt = image.FindUnique(ListFramePattern);
        // The test harness's autopilot hooks the action test where its pattern starts; it notes the place first.
        ActionPressedAt = Autopilot.ActionTestAt != 0 ? Autopilot.ActionTestAt : image.FindUnique(Autopilot.ActionTestPattern);
    }

    private static bool Pressed(int action) => _actionPressed(0, 0, (uint)action, 0) != 0;

    [UnmanagedCallersOnly]
    private static void OnListFrame(nint list, int state, int* next)
    {
        try
        {
            if (_asking >= 0)
            {
                if (state == ListState && list == RemixedScenarios.RumorList)
                {
                    if (RemixedScenarios.WorldFrames == _askedFrame)
                        return;
                    if (Pressed(Confirm))
                        Answer(true, "confirm");
                    else if (Pressed(Back))
                        Answer(false, "back");
                    return;
                }
                Answer(false, "the list closed");
            }
            else if (state == ListState && RemixedScenarios.RumorFilter == RemixedScenarios.MissionsOnly
                     && list == RemixedScenarios.RumorList && Pressed(Confirm))
            {
                var at = RemixedScenarios.PostingAt();
                if (at >= 0 && RemixedScenarios.Postings[at].Scenario != TavernStorage.DispatchScenario
                    && TavernStorage.ShowMissionsPrompt(TavernStorage.MissionsPrompt.Question))
                {
                    _asking = at;
                    _askedFrame = RemixedScenarios.WorldFrames;
                    _log.Info("missions: asked whether to accept the posting of mission scenario " +
                              $"0x{RemixedScenarios.Postings[at].Scenario:X} (rumor {RemixedScenarios.Postings[at].Rumor})");
                    return;
                }
            }
        }
        catch (Exception e)
        {
            _log.Error($"missions: the question couldn't be asked ({e.Message})");
            _asking = -1;
        }
        _listFrame(list, state, next);
    }

    private static void Answer(bool accept, string how)
    {
        var at = _asking;
        _asking = -1;
        if (at < 0 || at >= RemixedScenarios.Postings.Count)
            return;
        var posting = RemixedScenarios.Postings[at];
        if (accept)
        {
            RemixedScenarios.Accepted = at;
            TavernStorage.ShowMissionsPrompt(TavernStorage.MissionsPrompt.Accepted);
            _log.Info($"missions: Yes to the posting of mission scenario 0x{posting.Scenario:X} (rumor {posting.Rumor}): "
                      + "accepted, to start on the world map");
        }
        else
        {
            TavernStorage.ShowMissionsPrompt(TavernStorage.MissionsPrompt.Help);
            _log.Info($"missions: the posting of mission scenario 0x{posting.Scenario:X} not accepted ({how})");
        }
    }
}
