using System.Runtime.InteropServices;
using System.Text;

namespace IvaliceRemixed;

/// <summary>
/// For test runs (the harness's <c>watch</c>): hardware write breakpoints, to find what writes a place in memory. Up to
/// four addresses (the debug registers Dr0–Dr3) are set on every thread the game has; each write traps after the
/// instruction, and a native exception handler (no managed code runs in it) records the instruction pointer and the
/// top of the stack in a ring of <see cref="Entries"/>, then lets the game go on. <see cref="Report"/> gives each hit as
/// an RVA with the exe addresses on the stack (its callers, roughly): <c>tools/exe.py func</c> names them.
/// </summary>
internal static unsafe class Watch
{
    internal const int Entries = 32, EntryQwords = 16, StackQwords = 14;
    private const uint SingleStep = 0x80000004, ContextDebugRegisters = 0x00100010;
    private const int ContextSize = 0x4D0, ContextFlags = 0x30, Dr0 = 0x48, Dr7 = 0x70;
    private static nint _buffer, _handler;
    private static readonly nint[] Slots = new nint[4];
    private static readonly int[] Lengths = new int[4];

    [DllImport("kernel32.dll")] private static extern nint AddVectoredExceptionHandler(uint first, nint handler);
    [DllImport("kernel32.dll")] private static extern nint CreateToolhelp32Snapshot(uint flags, uint process);
    [DllImport("kernel32.dll")] private static extern bool Thread32First(nint snapshot, ref ThreadEntry entry);
    [DllImport("kernel32.dll")] private static extern bool Thread32Next(nint snapshot, ref ThreadEntry entry);
    [DllImport("kernel32.dll")] private static extern nint OpenThread(uint access, bool inherit, uint id);
    [DllImport("kernel32.dll")] private static extern uint SuspendThread(nint thread);
    [DllImport("kernel32.dll")] private static extern uint ResumeThread(nint thread);
    [DllImport("kernel32.dll")] private static extern bool GetThreadContext(nint thread, nint context);
    [DllImport("kernel32.dll")] private static extern bool SetThreadContext(nint thread, nint context);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();

    [StructLayout(LayoutKind.Sequential)]
    private struct ThreadEntry
    {
        public uint Size, Usage, ThreadId, OwnerProcessId;
        public int BasePriority, DeltaPriority;
        public uint Flags;
    }

    /// <summary>Sets a write watch on [address, address + length) (length 1, 2, 4 or 8, aligned to it) in a free debug
    /// register; returns what it did.</summary>
    internal static string Add(nint address, int length)
    {
        if (length is not (1 or 2 or 4 or 8) || address % length != 0)
            return "error: the length must be 1, 2, 4 or 8, and the address aligned to it";
        Install();
        var slot = Array.IndexOf(Slots, (nint)0);
        if (slot < 0)
            return "error: all four watches are in use (watch off)";
        Slots[slot] = address;
        Lengths[slot] = length;
        return $"watching 0x{address:X} ({length} bytes) in Dr{slot} on {Apply()} thread(s)";
    }

    internal static string Clear()
    {
        Array.Clear(Slots);
        Array.Clear(Lengths);
        return $"watches off on {Apply()} thread(s)";
    }

    /// <summary>Each recorded hit, oldest first: the instruction after the write (an RVA when it's in the exe), which
    /// watch fired, and the exe addresses on the stack above it.</summary>
    internal static string Report(nint start, nint end)
    {
        if (_buffer == 0)
            return "no watch set yet";
        var count = *(long*)_buffer;
        var text = new StringBuilder($"{count} hit(s)");
        for (var n = Math.Max(0, count - Entries); n < count; n++)
        {
            var entry = (long*)(_buffer + 8 + (n % Entries) * EntryQwords * 8);
            var rip = (nint)entry[0];
            var which = string.Join(",", Enumerable.Range(0, 4).Where(k => (entry[1] & (1 << k)) != 0).Select(k => $"Dr{k}"));
            var calls = Enumerable.Range(2, StackQwords).Select(k => (nint)entry[k]).Where(v => v > start + 0x1000 && v < end)
                .Select(v => $"exe+0x{v - start:X}");
            text.Append($"\n#{n}: {which} written at {Place(rip, start, end)}; on the stack: {string.Join(" ", calls)}");
        }
        return text.ToString();
    }

    private static string Place(nint at, nint start, nint end) => at >= start && at < end ? $"exe+0x{at - start:X}" : $"0x{at:X}";

    private static void Install()
    {
        if (_handler != 0)
            return;
        _buffer = Marshal.AllocHGlobal(8 + Entries * EntryQwords * 8);
        new Span<byte>((void*)_buffer, 8 + Entries * EntryQwords * 8).Clear();
        var code = Stub(_buffer);
        var stub = GameImage.AllocateCode(code.Length);
        code.CopyTo(new Span<byte>(stub, code.Length));
        _handler = AddVectoredExceptionHandler(1, (nint)stub);
    }

    /// <summary>rcx = EXCEPTION_POINTERS: a single step with a data breakpoint's bit in Dr6 records Rip, Dr6 and
    /// <see cref="StackQwords"/> qwords from Rsp at the next entry of the ring at buffer + 8 (the count at buffer),
    /// clears Dr6 and continues; anything else goes on searching.</summary>
    internal static byte[] Stub(nint buffer)
    {
        var c = new List<byte>();
        c.AddRange([0x48, 0x8B, 0x01]);                                  // mov rax, [rcx]
        c.AddRange([0x81, 0x38]); c.AddRange(BitConverter.GetBytes(SingleStep)); // cmp dword [rax], 0x80000004
        c.AddRange([0x75, 0x00]); var j1 = c.Count - 1;                  // jne pass
        c.AddRange([0x48, 0x8B, 0x51, 0x08]);                            // mov rdx, [rcx+8] (the context)
        c.AddRange([0x4C, 0x8B, 0x42, 0x68]);                            // mov r8, [rdx+0x68] (Dr6)
        c.AddRange([0x41, 0xF6, 0xC0, 0x0F]);                            // test r8b, 0xF
        c.AddRange([0x74, 0x00]); var j2 = c.Count - 1;                  // jz pass
        c.AddRange([0x49, 0xB9]); c.AddRange(BitConverter.GetBytes((long)buffer)); // mov r9, buffer
        c.AddRange([0x4D, 0x8B, 0x11]);                                  // mov r10, [r9] (the count)
        c.AddRange([0x49, 0x83, 0xE2, Entries - 1]);                     // and r10, Entries - 1
        c.AddRange([0x49, 0xC1, 0xE2, 0x07]);                            // shl r10, 7 (16 qwords an entry)
        c.AddRange([0x4F, 0x8D, 0x54, 0x11, 0x08]);                      // lea r10, [r9 + r10 + 8]
        c.AddRange([0x48, 0x8B, 0x82, 0xF8, 0x00, 0x00, 0x00]);          // mov rax, [rdx+0xF8] (Rip)
        c.AddRange([0x49, 0x89, 0x02]);                                  // mov [r10], rax
        c.AddRange([0x4D, 0x89, 0x42, 0x08]);                            // mov [r10+8], r8
        c.AddRange([0x4C, 0x8B, 0x9A, 0x98, 0x00, 0x00, 0x00]);          // mov r11, [rdx+0x98] (Rsp)
        c.AddRange([0xB9, StackQwords, 0x00, 0x00, 0x00]);               // mov ecx, StackQwords
        var loop = c.Count;
        c.AddRange([0x49, 0x8B, 0x03]);                                  // mov rax, [r11]
        c.AddRange([0x49, 0x89, 0x42, 0x10]);                            // mov [r10+0x10], rax
        c.AddRange([0x49, 0x83, 0xC3, 0x08]);                            // add r11, 8
        c.AddRange([0x49, 0x83, 0xC2, 0x08]);                            // add r10, 8
        c.AddRange([0xFF, 0xC9]);                                        // dec ecx
        c.AddRange([0x75, (byte)(loop - (c.Count + 2))]);                // jnz loop
        c.AddRange([0x49, 0xFF, 0x01]);                                  // inc qword [r9]
        c.AddRange([0x48, 0xC7, 0x42, 0x68, 0, 0, 0, 0]);                // mov qword [rdx+0x68], 0 (Dr6)
        c.AddRange([0xB8, 0xFF, 0xFF, 0xFF, 0xFF]);                      // mov eax, -1 (continue execution)
        c.Add(0xC3);                                                     // ret
        var pass = c.Count;
        c.AddRange([0x31, 0xC0, 0xC3]);                                  // pass: xor eax, eax; ret
        c[j1] = (byte)(pass - (j1 + 1));
        c[j2] = (byte)(pass - (j2 + 1));
        return [.. c];
    }

    /// <summary>Dr7 for the watches: each slot's local enable bit, write-only (01) and its length.</summary>
    internal static ulong Control()
    {
        ulong dr7 = 0;
        for (var k = 0; k < 4; k++)
        {
            if (Slots[k] == 0)
                continue;
            ulong len = Lengths[k] switch { 1 => 0, 2 => 1, 8 => 2, _ => 3 };
            dr7 |= 1UL << (2 * k);
            dr7 |= 1UL << (16 + 4 * k);   // RW = 01: writes
            dr7 |= len << (18 + 4 * k);
        }
        return dr7;
    }

    /// <summary>The debug registers onto every other thread of the game; the number done.</summary>
    private static int Apply()
    {
        const uint snapThreads = 0x4, access = 0x0002 | 0x0008 | 0x0010; // suspend/resume, get and set context
        var self = GetCurrentThreadId();
        var process = GetCurrentProcessId();
        var context = (nint)NativeMemory.AlignedAlloc(ContextSize, 16);
        var done = 0;
        var snapshot = CreateToolhelp32Snapshot(snapThreads, 0);
        try
        {
            var entry = new ThreadEntry { Size = (uint)sizeof(ThreadEntry) };
            for (var more = Thread32First(snapshot, ref entry); more; more = Thread32Next(snapshot, ref entry))
            {
                if (entry.OwnerProcessId != process || entry.ThreadId == self)
                    continue;
                var thread = OpenThread(access, false, entry.ThreadId);
                if (thread == 0)
                    continue;
                if (SuspendThread(thread) != uint.MaxValue)
                {
                    new Span<byte>((void*)context, ContextSize).Clear();
                    *(uint*)(context + ContextFlags) = ContextDebugRegisters;
                    if (GetThreadContext(thread, context))
                    {
                        for (var k = 0; k < 4; k++)
                            *(ulong*)(context + Dr0 + 8 * k) = (ulong)Slots[k];
                        *(ulong*)(context + Dr7) = Control();
                        if (SetThreadContext(thread, context))
                            done++;
                    }
                    ResumeThread(thread);
                }
                CloseHandle(thread);
            }
        }
        finally
        {
            CloseHandle(snapshot);
            NativeMemory.AlignedFree((void*)context);
        }
        return done;
    }
}
