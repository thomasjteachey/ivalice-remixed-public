using System.Runtime.InteropServices;
using System.Text;

namespace IvaliceRemixed.Patches;

/// <summary>
/// A guard for the remaster's unit-name lookup (0x1E8B7C: a name id, from a battle unit's +0x1FC or a roster record's
/// +0x230, looked up in an NXD table; a custom name from the text beside it). It returns null when the id has no row
/// (or is 0), and most of its 22 callers copy the name from what it returns without a check: the roster's conversion
/// to a battle-side record (0x284A08) crashed on the PC after ReMixed's Lich battle and Ashley's scene (exe+0x284E4A).
/// With this, a missing name logs its id once and gives the unit a stand-in name (the record's own text, else "?")
/// instead of a crash. A row with a blank name (the game's 0x71, which ReMixed's battle data gives units its events
/// name, such as Be A Beta's testers) shows the record's own text too, when it has one: the game shows that only for
/// a generic row. Installed with ReMixed's scenarios (<see cref="RemixedScenarios"/>).
/// </summary>
internal static unsafe class NameGuard
{
    // mov [rsp+0x18], rbx; push rdi; sub rsp, 0x20; mov rax, [the table]; mov rdi, rdx; mov ebx, ecx; test rax, rax;
    // jne; mov rcx, [the tables]; test rcx, rcx; je; lea edx, [rax+0x5D] (0x1E8B7C).
    internal const string Pattern =
        "48 89 5C 24 18 57 48 83 EC 20 48 8B 05 ?? ?? ?? ?? 48 8B FA 8B D9 48 85 C0 75 28 48 8B 0D ?? ?? ?? ?? 48 85 C9 0F 84 ?? ?? ?? ?? 8D 50 5D";
    internal const int NameBytes = 64; // what the callers copy

    internal static nint At;
    private static delegate* unmanaged<int, nint, nint> _game;
    private static Log _log = null!;
    private static readonly HashSet<int> Noted = [];
    private static nint _standIn;

    public static void Install(Game game)
    {
        _log = game.Log;
        At = game.Image.FindUnique(Pattern);
        _standIn = Marshal.AllocHGlobal(NameBytes * 2);
        new Span<byte>((void*)_standIn, NameBytes * 2).Clear();
        _game = (delegate* unmanaged<int, nint, nint>)game.Hooks.Hook(
            At, (nint)(delegate* unmanaged<int, nint, nint>)&Lookup, "the unit-name lookup");
    }

    [UnmanagedCallersOnly]
    private static nint Lookup(int id, nint text)
    {
        var name = _game(id, text);
        if (name != 0 && *(byte*)name != 0)
            return name;
        try
        {
            var own = text != 0 && Harness.Readable(text, 1) ? Marshal.PtrToStringUTF8(text, Math.Min(NameBytes - 1, Length(text))) : "";
            if (name != 0 && string.IsNullOrEmpty(own))
                return name; // a blank row and no text of its own: blank, as the game has it
            var standIn = string.IsNullOrEmpty(own) ? "?" : own;
            var bytes = Encoding.UTF8.GetBytes(standIn);
            var span = new Span<byte>((void*)_standIn, NameBytes);
            span.Clear();
            bytes.AsSpan(0, Math.Min(bytes.Length, NameBytes - 1)).CopyTo(span);
            lock (Noted)
                if (Noted.Add(id))
                    _log.Info(name != 0
                        ? $"name guard: id {id} (0x{id:X}) has a blank name; its unit's own, \"{standIn}\", shown"
                        : $"name guard: no name for id {id} (0x{id:X}); shown as \"{standIn}\" instead of a crash");
        }
        catch (Exception e)
        {
            _log.Error($"name guard: {e.Message}");
        }
        return _standIn;
    }

    private static int Length(nint text)
    {
        var n = 0;
        while (n < NameBytes - 1 && *(byte*)(text + n) != 0)
            n++;
        return n;
    }
}
