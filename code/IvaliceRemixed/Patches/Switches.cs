using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// A byte that native code (a patch's stub) tests for one of the options, kept equal to it as the options in effect
/// change (a save's own, <see cref="SaveOptions"/>): 1 on, 0 off. A stub reads it with
/// <see cref="Test"/>, which leaves every register as it was and sets the flags (ZF for off).
/// </summary>
internal static unsafe class Switches
{
    /// <summary>A switch for <paramref name="on"/>, set now and whenever the options change.</summary>
    internal static byte* Of(Func<Options, bool> on)
    {
        var at = (byte*)NativeMemory.AllocZeroed(1);
        *at = on(Options.Current) ? (byte)1 : (byte)0;
        Options.Changed += () => *at = on(Options.Current) ? (byte)1 : (byte)0;
        return at;
    }

    /// <summary>push rax; mov rax, the switch; cmp byte [rax], 0; pop rax: ZF set when it's off (16 bytes).</summary>
    internal static void Test(List<byte> code, byte* at)
    {
        code.AddRange([0x50, 0x48, 0xB8]);
        code.AddRange(BitConverter.GetBytes((ulong)(nint)at));
        code.AddRange([0x80, 0x38, 0x00, 0x58]);
    }
}
