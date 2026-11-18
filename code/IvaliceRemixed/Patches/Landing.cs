using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's knockback landings (docs/MECHANICS.md: "Knockback into lava or water"; PS1 0x801581A0
/// and 0x801580A0, in the knockback's fall damage step 0x8018AE3C). Installed by <see cref="Rules"/>.
/// </summary>
/// <remarks>
/// <para>
/// The remaster's step (0x309540, run as its own sub-action after a knockback) gives
/// (height fallen / 2 − Jump) × Max HP / 10, at most 999, unless the target is dead, crystal or has
/// a +0x62 bit 7 status. ReMixed: landing on lava is 999 unless the target has Waterwalking (0x1F6)
/// or Levitate (0x1FA), fallen or not; and a fall that hurts is none with Float or Levitate,
/// unchanged with Waterwalking, and otherwise cut by the water: depth 1, 75%; depth 2, 50%; deeper,
/// 25% with Swim (0x1F7) or Waterbreathing (0x1F9) and 999 (drowning) without. The cap comes after.
/// </para>
/// <para>
/// ReMixed reads the depth as tile byte 3 under 64 or 96, and that byte also holds the slope's
/// height (low 5 bits), so a sloped dry tile counts as depth 1. That's kept.
/// </para>
/// <para>
/// And a unit that dies on lava, or in water 3 deep or more, without the abilities that keep it
/// there, has its body moved to a free tile nearby (PS1 0x80158130, from the apply step's
/// just-died path 0x8018C320): the apply step returns −1, as it does for a rider that dies, and its
/// callers run the game's own search (0x283784) and move. <see cref="InstallBodies"/>.
/// </para>
/// </remarks>
internal static unsafe class Landing
{
    private static Game _game = null!;
    private static delegate* unmanaged<void> _fallStep; // the original
    private static byte* _knocked;  // 1 when the knockback dropped the target (0x7B0790, bit 7 masked off)
    private static byte* _height;   // how far it fell (0x7B0791)
    private static int _failed;

    private const int Lava = 0x12;
    // The movement byte +0x9E (PS1 +0x95).
    private const int Waterwalking = 0x80, Swim = 0x40, Waterbreathing = 0x10, Levitate = 0x08;

    // For the offline check.
    internal static nint FallStepAt { get; private set; }
    internal static byte* Knocked => _knocked;
    internal static byte* Height => _height;
    internal static nint DiedAt { get; private set; }       // the patched just-died check
    internal static nint DiedZero { get; private set; }     // where the game returns 0 (xor eax, eax)
    internal static nint DiedMinusOne { get; private set; } // where it returns −1 (or eax, −1)
    internal const int DiedStubSize = 192;

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // Both results cleared; the attacker's not hit; the flag's bit 7 off; result flags 1; then
        // the status checks, the flag, the height / 2 − Jump (+0x43), × Max HP (+0x32) / 10.
        var step = image.FindUnique(
            "48 83 EC 28 48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 33 D2 " +
            "4C 8B 0D ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? C6 00 00 80 25 ?? ?? ?? ?? 7F 41 C6 41 27 01 F6 41 61 60 75 ?? " +
            "38 51 62 7C ?? 80 3D ?? ?? ?? ?? 01 75 ?? 44 0F B6 05 ?? ?? ?? ?? 0F B6 41 43 41 D1 E8 44 2B C0 45 85 C0 7E ?? " +
            "0F B7 49 32 B8 67 66 66 66");
        if (GameImage.Rip(step + 37, 3, 7) != (nint)game.Battle.TargetResult || GameImage.Rip(step + 44, 3, 7) != (nint)game.Routines.Target
            || GameImage.Rip(step + 54, 2, 7) != GameImage.Rip(step + 77, 2, 7))
            throw new InvalidDataException("the knockback's fall damage step isn't the code this DLL expects");
        _knocked = (byte*)GameImage.Rip(step + 54, 2, 7);
        _height = (byte*)GameImage.Rip(step + 86, 4, 8);
        _ = game.Map; // found now, so a missing pattern stops this rule here
        FallStepAt = step;
        // Its one caller (the action routine) keeps no scratch register across it.
        _fallStep = (delegate* unmanaged<void>)game.Hooks.Hook(step, (nint)(delegate* unmanaged<void>)&FallStep, "the knockback's fall damage");
    }

    /// <summary>
    /// The apply step's just-died path (0x30A8E0): after the kill count and the kill's EXP step, it
    /// asks whether the target rides (+0x1B4 bit 7) by jumping into the end's own check (jge → 0,
    /// else −1). That compare and jump become a jump to a stub that asks <see cref="Deadly"/> too.
    /// The step's callers keep nothing but its result across it (liveregs), and r11 is the target
    /// there (the EXP step keeps it).
    /// </summary>
    public static void InstallBodies(Game game)
    {
        _game = game;
        var image = game.Image;
        // cmp [preview flag], ebx; mov r11, [target]; jne; add [r11+0x18C], r13b; mov ecx, 2;
        // call the EXP step; cmp [r11+0x1B4], bl; jmp the end's check.
        var at = image.FindUnique(
            "39 1D ?? ?? ?? ?? 4C 8B 1D ?? ?? ?? ?? 75 07 45 00 AB 8C 01 00 00 B9 02 00 00 00 E8 ?? ?? ?? ?? " +
            "41 38 9B B4 01 00 00 E9 ?? ?? ?? ??");
        var join = (byte*)GameImage.Rip(at + 39, 1, 5);
        if ((byte**)GameImage.Rip(at + 6, 3, 7) != game.Routines.Target
            || join[0] != 0x0F || join[1] != 0x8D || join[6] != 0x83 || join[7] != 0xC8 || join[8] != 0xFF)
            throw new InvalidDataException("the apply step's just-died check isn't the code this DLL expects");
        var zero = GameImage.Rip((nint)join, 2, 6);
        if (*(ushort*)zero != 0xC033)
            throw new InvalidDataException("the apply step's 0 return isn't the code this DLL expects");
        _ = game.Map;
        var site = at + 32;
        var stub = GameImage.AllocateCodeNear(site, DiedStubSize);
        WriteDiedStub(stub, zero, (nint)join + 6);
        var jump = new byte[12]; // the compare (7) and the jump (5); nothing jumps into them
        Array.Fill(jump, (byte)0xCC);
        jump[0] = 0xE9;
        BitConverter.TryWriteBytes(jump.AsSpan(1), checked((int)((nint)stub - (site + 5))));
        GameImage.WriteCode(site, jump);
        DiedAt = site;
        DiedZero = zero;
        DiedMinusOne = (nint)join + 6;
    }

    /// <summary>
    /// The just-died stub, entered by a jump with r11 the target and the stack 16-aligned (a
    /// function body): the game's riding check, then the C# one with every scratch register kept
    /// (mid-function), then on to the game's 0 or −1 return.
    /// </summary>
    internal static void WriteDiedStub(byte* stub, nint zero, nint minusOne)
    {
        var code = new List<byte>();
        var toMinusOne = new List<int>();
        void JumpToMinusOne(byte condition)
        {
            code.AddRange([0x0F, condition, 0, 0, 0, 0]);
            toMinusOne.Add(code.Count);
        }
        code.AddRange([0x41, 0x38, 0x9B, 0xB4, 0x01, 0x00, 0x00]);                         // cmp [r11+0x1B4], bl: the game's
        JumpToMinusOne(0x8C);                                                               // jl: riding, so −1
        code.AddRange([0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53]); // push rax, rcx, rdx, r8–r11
        code.AddRange([0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00]);                         // sub rsp, 0x88
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x4C, 0x89, 0xD9]);                                                  // mov rcx, r11
        code.AddRange([0x48, 0xB8]);                                                        // mov rax, the check
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<byte*, int>)&OnDeadlyTile));
        code.AddRange([0xFF, 0xD0]);                                                        // call rax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);                         // add rsp, 0x88
        code.AddRange([0x85, 0xC0]);                                                        // test eax, eax
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax (flags kept)
        JumpToMinusOne(0x85);                                                               // jnz: a deadly tile, so −1
        AbsoluteJump(code, zero);
        var minusAt = code.Count;
        AbsoluteJump(code, minusOne);
        var bytes = code.ToArray();
        foreach (var end in toMinusOne)
            BitConverter.TryWriteBytes(bytes.AsSpan(end - 4), minusAt - end);
        if (bytes.Length > DiedStubSize)
            throw new InvalidOperationException("the just-died stub is too big");
        bytes.CopyTo(new Span<byte>(stub, DiedStubSize));
    }

    /// <summary>jmp [rip+0] with the target after it.</summary>
    private static void AbsoluteJump(List<byte> code, nint target)
    {
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);
        code.AddRange(BitConverter.GetBytes((ulong)target));
    }

    [UnmanagedCallersOnly]
    private static int OnDeadlyTile(byte* unit)
    {
        try
        {
            if (unit == null || !Deadly(unit, _game.Map.TileUnder(unit)))
                return 0;
            Tracer.Line(_game.Log, $"landing: unit {unit[Battle.UnitIndex]} died on a deadly tile; its body moves");
            return 1;
        }
        catch (Exception e)
        {
            Failed("the deadly tile check", e);
            return 0;
        }
    }

    /// <summary>
    /// ReMixed's deadly tiles (PS1 0x80158130): lava without Waterwalking or Levitate, or water 3
    /// deep or more (tile byte 3 from 96) without Waterwalking, Swim, Waterbreathing or Levitate.
    /// </summary>
    internal static bool Deadly(byte* unit, byte* tile)
    {
        if (tile == null)
            return false;
        var movement = unit[Battle.UnitMovement + 2];
        if ((tile[0] & 0x3F) == Lava)
            return (movement & (Waterwalking | Levitate)) == 0;
        return tile[3] >= 96 && (movement & (Waterwalking | Swim | Waterbreathing | Levitate)) == 0;
    }

    /// <summary>The game's step, then ReMixed's landing on the tile the target ended up on.</summary>
    [UnmanagedCallersOnly]
    private static void FallStep()
    {
        _fallStep();
        try
        {
            var target = *_game.Routines.Target;
            var result = *_game.Battle.TargetResult;
            if (target == null || result == null || (target[Battle.UnitStatuses] & 0x60) != 0 || (sbyte)target[Battle.UnitStatuses + 1] < 0)
                return;
            var damage = Damage(target, _game.Map.TileUnder(target), *_knocked == 1, *_height);
            if (damage < 0)
                return;
            var before = *(ushort*)(result + Battle.HpDamage);
            *(ushort*)(result + Battle.HpDamage) = (ushort)damage;
            result[Battle.ResultFlags] = (byte)(damage != 0 ? 0x81 : 0x01);
            if (damage != before)
                Tracer.Line(_game.Log, $"landing: unit {target[Battle.UnitIndex]} lands for {damage} instead of {before}");
        }
        catch (Exception e)
        {
            Failed("the landing", e);
        }
    }

    /// <summary>
    /// ReMixed's landing damage on <paramref name="tile"/>, or -1 where the game's own stands (no fall,
    /// or one too short to hurt, and no lava).
    /// </summary>
    internal static int Damage(byte* target, byte* tile, bool fell, int height)
    {
        var movement = target[Battle.UnitMovement + 2];
        if (tile != null && (tile[0] & 0x3F) == Lava && (movement & (Waterwalking | Levitate)) == 0)
            return 999;
        if (!fell)
            return -1;
        var over = (height >> 1) - target[Battle.UnitJump];
        if (over <= 0)
            return -1;
        var damage = over * *(ushort*)(target + Battle.UnitMaxHp) / 10;
        if ((target[Battle.UnitStatuses + 2] & 0x40) != 0 || (movement & Levitate) != 0) // Float
            return 0;
        if ((movement & Waterwalking) == 0 && tile != null && tile[3] != 0)
        {
            if (tile[3] < 64)
                damage -= damage >> 2;
            else if (tile[3] < 96)
                damage >>= 1;
            else if ((movement & (Swim | Waterbreathing)) != 0)
                damage >>= 2;
            else
                return 999; // drowned
        }
        return Math.Min(damage, 999);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"landing: {what} failed: {e}");
    }
}
