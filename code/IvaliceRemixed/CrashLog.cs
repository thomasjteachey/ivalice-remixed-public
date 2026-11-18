using System.Runtime.InteropServices;
using System.Text;

namespace IvaliceRemixed;

/// <summary>
/// A diagnostic: when the game's own code faults (an access violation with its address inside the exe), writes the
/// address as an RVA, the registers and the exe's return addresses on the stack to <see cref="FileName"/> in the
/// mod's folder and the log, then lets the crash go on. The Deck records no crash address otherwise.
/// </summary>
/// <remarks>
/// A vectored handler written in C# alone was called for .NET's own exceptions too, from managed code, and failed
/// ("Invalid Program"); so a native stub filters first and calls the managed logger only for a fault in the exe, where
/// the thread is in native code.
/// </remarks>
internal static unsafe class CrashLog
{
    public const string FileName = "crash.txt";
    private static nint _start, _end;
    private static string _file = "";
    private static Log _log = null!;
    private static int _written;

    [DllImport("kernel32.dll")]
    private static extern nint AddVectoredExceptionHandler(uint first, nint handler);

    public static void Install(Game game)
    {
        _log = game.Log;
        _start = game.Image.ImageStart;
        var nt = _start + *(int*)(_start + 0x3C);
        _end = _start + *(int*)(nt + 0x50);  // SizeOfImage
        _file = Path.Combine(Path.GetDirectoryName(typeof(CrashLog).Assembly.Location) ?? "", FileName);
        var stub = GameImage.AllocateCode(96);
        var code = Stub(_start, _end, (nint)(delegate* unmanaged<nint, int>)&Handle);
        code.CopyTo(new Span<byte>(stub, code.Length));
        AddVectoredExceptionHandler(1, (nint)stub);
        game.Log.Info($"crash log: a fault in the exe goes to {FileName}");
    }

    /// <summary>rcx = EXCEPTION_POINTERS: an access violation (0xC0000005) at an address in [start, end) jumps to the
    /// managed handler; anything else returns 0 (go on searching).</summary>
    internal static byte[] Stub(nint start, nint end, nint handler)
    {
        var c = new List<byte>();
        c.AddRange([0x48, 0x8B, 0x01]);                                  // mov rax, [rcx]
        c.AddRange([0x81, 0x38, 0x05, 0x00, 0x00, 0xC0]);                // cmp dword [rax], 0xC0000005
        c.AddRange([0x75, 0x00]); var j1 = c.Count - 1;                  // jne pass
        c.AddRange([0x48, 0x8B, 0x50, 0x10]);                            // mov rdx, [rax+0x10]
        c.AddRange([0x49, 0xB8]); c.AddRange(BitConverter.GetBytes((long)start));  // mov r8, start
        c.AddRange([0x4C, 0x39, 0xC2]);                                  // cmp rdx, r8
        c.AddRange([0x72, 0x00]); var j2 = c.Count - 1;                  // jb pass
        c.AddRange([0x49, 0xB8]); c.AddRange(BitConverter.GetBytes((long)end));    // mov r8, end
        c.AddRange([0x4C, 0x39, 0xC2]);                                  // cmp rdx, r8
        c.AddRange([0x73, 0x00]); var j3 = c.Count - 1;                  // jae pass
        c.AddRange([0x48, 0xB8]); c.AddRange(BitConverter.GetBytes((long)handler)); // mov rax, handler
        c.AddRange([0xFF, 0xE0]);                                        // jmp rax
        var pass = c.Count;
        c.AddRange([0x31, 0xC0, 0xC3]);                                  // pass: xor eax, eax; ret
        foreach (var j in (int[])[j1, j2, j3])
            c[j] = (byte)(pass - (j + 1));
        return [.. c];
    }

    [UnmanagedCallersOnly]
    private static int Handle(nint pointers)
    {
        try
        {
            if (Interlocked.Increment(ref _written) > 3)
                return 0;
            var record = *(nint*)pointers;
            var context = *(nint*)(pointers + 8);
            var at = *(nint*)(record + 0x10);
            var reading = *(long*)(record + 0x20) == 0 ? "reading" : "writing";
            var address = *(nint*)(record + 0x28);
            var text = new StringBuilder();
            text.AppendLine($"{DateTime.Now:HH:mm:ss} access violation at exe+0x{at - _start:X}, {reading} 0x{address:X}");
            string[] names = ["rax", "rcx", "rdx", "rbx", "rsp", "rbp", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"];
            for (var k = 0; k < names.Length; k++)
            {
                var value = *(long*)(context + 0x78 + 8 * k);
                var inExe = value >= _start && value < _end ? $" (exe+0x{value - _start:X})" : "";
                text.Append($"  {names[k]} 0x{value:X}{inExe}");
                if (k % 4 == 3)
                    text.AppendLine();
            }
            var rsp = *(nint*)(context + 0x98);
            var calls = new List<string>();
            for (var k = 0; k < 96 && calls.Count < 16; k++)
            {
                var value = *(nint*)(rsp + 8 * k);
                if (value > _start + 0x1000 && value < _end)
                    calls.Add($"exe+0x{value - _start:X}");
            }
            text.AppendLine($"  exe addresses on the stack: {string.Join(" ", calls)}");
            File.AppendAllText(_file, text.ToString());
            _log.Error("crash log: " + text.ToString().Replace(Environment.NewLine, " | "));
        }
        catch
        {
            // Nothing more can be done while the game is faulting.
        }
        return 0;
    }
}
