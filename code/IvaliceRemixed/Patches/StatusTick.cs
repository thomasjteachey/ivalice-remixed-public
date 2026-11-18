using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Poison &amp; Regen and Auto Renew options (docs/MECHANICS.md: "Poison &amp; Regen on MP, Auto
/// Renew, Starting MP"; PS1 0x8018D7C8 and 0x80150E48). Installed by <see cref="Rules"/> with
/// "status-ticks", and only patches the game when one of the two settings is on.
/// </summary>
/// <remarks>
/// At the end of a unit's turn the battle loop (the remaster's 0x30F030) works out Poison (Max HP / 8
/// damage) or else Regen (Max HP / 8 healing) inline, from 0x30F5FC. Its 52 bytes jump to a stub and
/// <see cref="Apply"/> instead: the same, plus Max MP / 8 for both with the Poison &amp; Regen setting
/// (flag 0xF2), and for a unit with neither, Max MP / 8 back (at least 1) with Auto Renew (0xF3).
/// </remarks>
internal static unsafe class StatusTick
{
    private static Game _game = null!;
    private static bool _mp, _autoRenew;
    private static int _failed;

    private const string Block =
        "E8 ?? ?? ?? ?? 8A 43 64 48 8B 0D ?? ?? ?? ?? 84 C0 79 12 0F B7 43 32 66 C1 E8 03 66 89 41 06 C6 41 27 80 " +
        "EB 14 A8 40 74 10 0F B7 43 32 66 C1 E8 03 66 89 41 08 C6 41 27 40 40 38 71 27 0F 84";
    private const int BlockStart = 5, BlockSize = 0x34;
    internal const int StubSize = 192;

    // For the offline check.
    internal static nint BlockAt { get; private set; }
    internal static bool Patched { get; private set; }

    public static void Install(Game game)
    {
        _game = game;
        var at = game.Image.FindUnique(Block);
        if ((byte**)GameImage.Rip(at + 8, 3, 7) != game.Battle.TargetResult)
            throw new InvalidDataException("the Poison and Regen tick isn't the code this DLL expects");
        BlockAt = at + BlockStart;
        _mp = Options.Current.PoisonAndRegen == Options.PoisonRegenRule.HpAndMp;
        _autoRenew = Options.Current.AutoRenew;
        // Installed whatever the options: with both off its tick is the game's own, and a save's options can turn
        // either on.
        Options.Changed += () => (_mp, _autoRenew) =
            (Options.Current.PoisonAndRegen == Options.PoisonRegenRule.HpAndMp, Options.Current.AutoRenew);
        var stub = GameImage.AllocateCodeNear(BlockAt, StubSize);
        WriteStub(stub, (nint)(delegate* unmanaged<byte*, byte*, void>)&Tick, (nint)game.Battle.TargetResult,
                  BlockAt + BlockSize);
        var jump = new byte[BlockSize];
        Array.Fill(jump, (byte)0xCC);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (BlockAt + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(BlockAt, jump);
        Patched = true;
    }

    /// <summary>For the offline check: the settings the tick follows, and the C# its stub calls.</summary>
    internal static void Set(bool mp, bool autoRenew) => (_mp, _autoRenew) = (mp, autoRenew);
    internal static (bool Mp, bool AutoRenew) Settings => (_mp, _autoRenew);
    internal static nint TickAt => (nint)(delegate* unmanaged<byte*, byte*, void>)&Tick;

    /// <summary>
    /// The stub, entered by a jump with the unit in rbx: calls <paramref name="managed"/>(unit, result)
    /// keeping every scratch register, then goes on to <paramref name="back"/> with rcx the result, as
    /// the game's own code leaves it.
    /// </summary>
    internal static void WriteStub(byte* stub, nint managed, nint resultPointer, nint back)
    {
        var code = new List<byte>
        {
            0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, // push rax, rcx, rdx, r8–r11
            0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00,                          // sub rsp, 0x88
        };
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x48, 0x89, 0xD9]);                    // mov rcx, rbx: the unit
        code.AddRange([0x48, 0xB8]);                          // mov rax, &the target's result pointer
        code.AddRange(BitConverter.GetBytes((long)resultPointer));
        code.AddRange([0x48, 0x8B, 0x10]);                    // mov rdx, [rax]
        code.AddRange([0x48, 0xB8]);                          // mov rax, managed
        code.AddRange(BitConverter.GetBytes((long)managed));
        code.AddRange([0xFF, 0xD0]);                          // call rax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]); // add rsp, 0x88
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11–r8, rdx, rcx, rax
        code.AddRange([0x48, 0xB9]);                          // mov rcx, &the target's result pointer
        code.AddRange(BitConverter.GetBytes((long)resultPointer));
        code.AddRange([0x48, 0x8B, 0x09]);                    // mov rcx, [rcx]
        code.AddRange([0xFF, 0x25, 0x00, 0x00, 0x00, 0x00]);  // jmp [rip]: back
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the status tick stub outgrew its space");
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }

    [UnmanagedCallersOnly]
    private static void Tick(byte* unit, byte* result)
    {
        try
        {
            Apply(unit, result, _mp, _autoRenew);
        }
        catch (Exception e)
        {
            Failed("the status tick", e);
        }
    }

    /// <summary>
    /// The end-of-turn Poison or Regen into the unit's (cleared) result: vanilla's HP, then ReMixed's MP
    /// with <paramref name="mp"/>, or with <paramref name="autoRenew"/> MP back for a unit with neither.
    /// </summary>
    internal static void Apply(byte* unit, byte* result, bool mp, bool autoRenew)
    {
        var statuses = unit[Battle.UnitStatuses + 3];
        var hp = (ushort)(*(ushort*)(unit + Battle.UnitMaxHp) >> 3);
        var maxMp = *(ushort*)(unit + Battle.UnitMaxMp);
        if ((statuses & 0x80) != 0) // Poison
        {
            *(ushort*)(result + Battle.HpDamage) = hp;
            result[Battle.ResultFlags] = 0x80;
            if (mp)
            {
                *(ushort*)(result + Battle.MpDamage) = (ushort)(maxMp >> 3);
                result[Battle.ResultFlags] |= 0x20;
            }
        }
        else if ((statuses & 0x40) != 0) // Regen
        {
            *(ushort*)(result + Battle.HpHeal) = hp;
            result[Battle.ResultFlags] = 0x40;
            if (mp)
            {
                *(ushort*)(result + Battle.MpHeal) = (ushort)(maxMp >> 3);
                result[Battle.ResultFlags] |= 0x10;
            }
        }
        else if (autoRenew)
        {
            *(ushort*)(result + Battle.MpHeal) = (ushort)Math.Max(1, maxMp >> 3);
            result[Battle.ResultFlags] = 0x10;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"status tick: {what} failed: {e}");
    }
}
