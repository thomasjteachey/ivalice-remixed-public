using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// For the story port, with the test mode on (from <see cref="TestRecorder"/>): which event scripts the game loads.
/// The loader (0x2F3BC4: ecx an id, rdx the result; it formats script/enhanced/event%03u.e from the id divided by 3,
/// or by 5 when 0x3CD9B10's +8 is clear) is hooked by a native stub that only puts the id in a ring of 64 and goes on,
/// with nothing managed on the way (the loader may run in one of the game's tasks). <see cref="Drain"/> logs the new
/// ones from a safe place (a battle's start and end).
/// </summary>
internal static unsafe class EventLog
{
    private const int Ring = 64;
    internal const string LoaderPattern =
        "48 89 5C 24 18 55 56 57 48 8D 6C 24 80 48 81 EC 80 01 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 70 " +
        "48 8B 05 ?? ?? ?? ?? 33 DB 48 8B F2 44 8B C1";
    private static uint* _ring; // [0] how many so far, then Ring ids
    private static uint _read;
    private static Log _log = null!;

    public static void Install(Game game)
    {
        _log = game.Log;
        var loader = game.Image.FindUnique(LoaderPattern);
        _ring = (uint*)NativeMemory.AllocZeroed((Ring + 1) * sizeof(uint));
        var stub = GameImage.AllocateCodeNear(loader, 96);
        WriteStub(stub, _ring, out var slot);
        *(nint*)(stub + slot) = game.Hooks.Hook(loader, (nint)stub, "the event script loader (test mode)");
    }

    /// <summary>push rax; push rdx; the id into the ring; pop; then on to the original through the slot.</summary>
    internal static void WriteStub(byte* stub, uint* ring, out int slot)
    {
        var code = new List<byte> { 0x50, 0x52, 0x48, 0xB8 };                  // push rax; push rdx; mov rax, ring
        code.AddRange(BitConverter.GetBytes((ulong)ring));
        code.AddRange([0x8B, 0x10]);                                            // mov edx, [rax]
        code.AddRange([0x83, 0xE2, Ring - 1]);                                  // and edx, 63
        code.AddRange([0x89, 0x4C, 0x90, 0x04]);                                // mov [rax+rdx*4+4], ecx
        code.AddRange([0xFF, 0x00]);                                            // inc dword [rax]
        code.AddRange([0x5A, 0x58]);                                            // pop rdx; pop rax
        code.AddRange([0xFF, 0x25, 0x00, 0x00, 0x00, 0x00]);                    // jmp [rip]: the original
        slot = code.Count;
        code.AddRange(new byte[8]);
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>The ids loaded since the last call, as "id (id/3, id/5)".</summary>
    internal static List<uint> Take(uint* ring, ref uint read)
    {
        var ids = new List<uint>();
        var count = ring[0];
        if (count - read > Ring)
            read = count - Ring;
        for (; read < count; read++)
            ids.Add(ring[1 + (read & (Ring - 1))]);
        return ids;
    }

    /// <summary>The story experiment's mark (tools/build_mod.py, STORY_EXPERIMENT): the shipped event401.e adds
    /// 0x1234 to event variable 0x6B; logged once it shows, then put back to 0.</summary>
    public static void CheckMarker()
    {
        if (_ring == null || Nemesis.GetVariable == null || Nemesis.SetVariable == null)
            return; // installed in the game with the test mode only
        var value = Nemesis.GetVariable(MarkerVariable);
        if (value == 0)
            return;
        _log.Info($"story experiment: event variable 0x{MarkerVariable:X} is 0x{value:X}"
                  + (value % 0x1234 == 0 ? " (the mod's event401.e ran)" : " (not the mod's mark)"));
        if (value % 0x1234 == 0)
            Nemesis.SetVariable(MarkerVariable, 0, 0);
    }

    internal const int MarkerVariable = 0x6B;

    public static void Drain(string when)
    {
        if (_ring == null)
            return;
        var ids = Take(_ring, ref _read);
        if (ids.Count > 0)
            _log.Info($"event scripts loaded {when}: "
                      + string.Join(", ", ids.Select(id => $"0x{id:X} (÷3 {id / 3:D3}, ÷5 {id / 5:D3})")));
    }
}
