using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's EXP and JP rates (its Extra Options "EXP Gain" and "JP Gain", event var 0x14 bytes 1
/// and 0; docs/MECHANICS.md: "EXP and JP rates"). Installed by <see cref="Rules"/>. Data:
/// {"jp": percent, "exp": percent, "spillover": divisor}, ReMixed's defaults, which the build
/// reads where its options event sets them (125%, 100%, 1/4).
/// </summary>
/// <remarks>
/// An action's EXP (PS1 0x8018D6xx; 0x30C110) and JP (ReMixed's 0x801516C8, from the JP award
/// at 0x8017E954; here in the award step 0x28338C) scale by the rates, at least 1 unless the rate
/// is 0. Gained EXP Up and Gained JP Up (0x1D0 and 0x1CF, ReMixed's Two Spells and Banditry) no
/// longer double EXP or add half to JP. Move-Get Exp and JP use the same rates (in
/// <see cref="Movement"/>). The remaster's own JP rate setting and its JP-up equipment still apply.
/// </remarks>
internal static unsafe class Gains
{
    private static Game _game = null!;
    private static delegate* unmanaged<int, nint, int> _award; // the original
    private static int* _reacting;   // set while a reaction is applied (0x186AF9C)
    private static byte* _actionExp; // the action's EXP: the best over its targets (0x7B078A)
    private static int _failed;

    /// <summary>Percentages and the spillover divisor; ReMixed's defaults until <see cref="SetRates"/>.</summary>
    internal static int JpRate = 125, ExpRate = 100, Spillover = 4;

    private const int GainedJpUp = 0x40; // in the support byte +0x99 (0x1CF)
    private const int StubSize = 192;

    // For the offline check.
    internal static nint ActionExpAt { get; private set; }
    internal static nint JpBaseAt { get; private set; }
    internal static byte* ActionExpGlobal => _actionExp;
    internal static int* Reacting => _reacting;

    /// <summary>
    /// ReMixed's rates from the build's data (read from its options event), then the player's from
    /// <see cref="Options"/>, whose defaults are the same.
    /// </summary>
    public static void SetRates(Game game, JsonElement data)
    {
        JpRate = data.GetProperty("jp").GetInt32();
        ExpRate = data.GetProperty("exp").GetInt32();
        Spillover = data.GetProperty("spillover").GetInt32();
        var options = Options.Current;
        if (options.SpilloverJp != Spillover)
            game.Log.Info($"rules: spillover JP 1/{options.SpilloverJp} from the options (ReMixed's: 1/{Spillover})");
        Spillover = options.SpilloverJp;
        if (options.JpGain != JpRate || options.ExpGain != ExpRate)
            game.Log.Info($"rules: EXP {options.ExpGain}% and JP {options.JpGain}% from the options (ReMixed's: EXP {ExpRate}%, JP {JpRate}%)");
        JpRate = options.JpGain;
        ExpRate = options.ExpGain;
    }

    /// <summary>Replaces the action EXP step, scales the action's JP and hides Gained JP Up from the award.</summary>
    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // An action's EXP for its current target (PS1 0x8018D6xx): a leaf, replaced whole.
        var exp = image.FindUnique(
            "83 3D ?? ?? ?? ?? 00 0F 85 ?? ?? ?? ?? 85 C9 0F 84 ?? ?? ?? ?? 4C 8B 05 ?? ?? ?? ?? 4D 85 C0 0F 84 ?? ?? ?? ?? " +
            "4C 8B 0D ?? ?? ?? ?? 41 BA 01 00 00 00 41 3B CA 75 ?? 41 8D 52 09 EB ?? 41 0F B6 89 8C 01 00 00");
        var tail = image.FindUnique(
            "41 8A 80 99 00 00 00 44 0F B7 C2 66 45 03 C0 24 20 B8 63 00 00 00 66 44 0F 44 C2 66 44 3B C0 7E 03 44 8B C0 " +
            "0F B6 05 ?? ?? ?? ?? 66 44 3B C0 8B D0 41 0F B6 C0 0F 4F D0 88 15 ?? ?? ?? ?? C3");
        if (tail - exp != 0x72 || GameImage.Rip(exp + 21, 3, 7) != (nint)game.Battle.Attacker
            || GameImage.Rip(exp + 37, 3, 7) != (nint)game.Routines.Target || GameImage.Rip(tail + 36, 3, 7) != GameImage.Rip(tail + 56, 2, 6))
            throw new InvalidDataException("the action EXP step isn't the code this DLL expects");
        _reacting = (int*)GameImage.Rip(exp, 2, 7);
        _actionExp = (byte*)GameImage.Rip(tail + 36, 3, 7);
        // The JP award (PS1 0x8017E7xx): the base (8 + level / 4 + 2 × job level, none without EXP) is
        // formed in ebp, then a quarter goes to the actor's team and the rest, times the remaster's
        // rate, to the actor.
        var award = image.FindUnique(
            "48 89 5C 24 18 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 40 4C 63 E1 45 33 FF 33 C9 44 89 3A 48 8B F2");
        var jpBase = image.FindUnique(
            "C1 ED 02 F6 D8 8D 4D 08 1B ED 42 8D 0C 69 40 80 E6 30 23 E9 33 FF 41 3B FC 74 ?? 8B D5 45 8B CF C1 EA 02 " +
            "44 8A C6 8B CF E8 ?? ?? ?? ?? FF C7 83 FF 15 7C ?? 41 0F AF EE");
        if ((ulong)(jpBase - award) > 0x300)
            throw new InvalidDataException("the JP base isn't in the JP award");
        ActionExpAt = exp;
        JpBaseAt = jpBase;

        // Its caller in the result step keeps a unit in r11 across the call (the step never touches it).
        game.Hooks.Hook(exp, Stub.KeepingRegisters((nint)(delegate* unmanaged<int, void>)&ActionExp), "the action EXP step (replaced)");
        _award = (delegate* unmanaged<int, nint, int>)game.Hooks.Hook(
            award, (nint)(delegate* unmanaged<int, nint, int>)&Award, "the EXP and JP award");
        WriteJpStub(jpBase);
        // The spillover's mov edx, ebp; mov r9d, r15d; shr edx, 2 in the loop over the actor's teammates.
        SpilloverAt = jpBase + 0x1B;
        var spill = GameImage.AllocateCodeNear(SpilloverAt, SpilloverStubSize);
        WriteSpilloverStub(spill, Spillover);
        // A save's own rates (SaveOptions): the EXP and JP steps read the rates as they run; the spillover's divisor
        // is written into its stub (on the game's thread, between actions).
        Options.Changed += () =>
        {
            (JpRate, ExpRate, Spillover) = (Options.Current.JpGain, Options.Current.ExpGain, Options.Current.SpilloverJp);
            WriteSpilloverStub(spill, Spillover);
        };
        byte[] call = [0xE8, 0, 0, 0, 0, 0x90, 0x90, 0x90];
        BitConverter.GetBytes(checked((int)((nint)spill - (SpilloverAt + 5)))).CopyTo(call, 1);
        GameImage.WriteCode(SpilloverAt, call);
    }

    internal const int SpilloverStubSize = 64;
    internal static nint SpilloverAt { get; private set; }

    /// <summary>
    /// ReMixed's spillover (PS1 0x8017E95C): the action's JP (ebp, already at the JP rate) over
    /// <paramref name="divisor"/>, at least 1 when there's any, none for a divisor of 0, into edx; then
    /// r9d = r15d as the game's code had it. A leaf called from the loop; keeps everything else.
    /// </summary>
    internal static void WriteSpilloverStub(byte* stub, int divisor)
    {
        var code = new List<byte> { 0x50, 0x51 };          // push rax; push rcx
        code.AddRange([0x89, 0xE8, 0x31, 0xD2]);           // mov eax, ebp; xor edx, edx
        code.AddRange([0x85, 0xC0, 0x74, 0x00]);           // test eax, eax; jz done
        var noJp = code.Count - 1;
        code.Add(0xB9);                                    // mov ecx, divisor
        code.AddRange(BitConverter.GetBytes(divisor));
        code.AddRange([0x85, 0xC9, 0x74, 0x00]);           // test ecx, ecx; jz done
        var noShare = code.Count - 1;
        code.AddRange([0xF7, 0xF1, 0x89, 0xC2]);           // div ecx; mov edx, eax
        code.AddRange([0x85, 0xD2, 0x75, 0x05]);           // test edx, edx; jnz done
        code.AddRange([0xBA, 0x01, 0x00, 0x00, 0x00]);     // mov edx, 1
        var done = code.Count;
        code[noJp] = (byte)(done - (noJp + 1));
        code[noShare] = (byte)(done - (noShare + 1));
        code.AddRange([0x59, 0x58, 0x45, 0x8B, 0xCF, 0xC3]); // pop rcx; pop rax; mov r9d, r15d; ret
        if (code.Count > SpilloverStubSize)
            throw new InvalidOperationException("the spillover stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, SpilloverStubSize));
    }

    /// <summary>
    /// The JP base's 20 bytes (shr ebp, 2 … and ebp, ecx) move to a stub that runs them, then
    /// scales ebp by the JP rate, and jumps back. They read no memory and nothing branches into them.
    /// </summary>
    private static void WriteJpStub(nint at)
    {
        var stub = GameImage.AllocateCode(StubSize);
        BuildJpStub(stub, at, at + 20);
        var jump = new byte[20];
        Array.Fill(jump, (byte)0x90);
        AbsoluteJump((nint)stub).CopyTo(jump, 0);
        GameImage.WriteCode(at, jump);
    }

    /// <summary>The JP stub: the 20 bytes at <paramref name="original"/>, the scaling, then a jump to <paramref name="resume"/>.</summary>
    internal static void BuildJpStub(byte* stub, nint original, nint resume)
    {
        var code = new List<byte>(new ReadOnlySpan<byte>((void*)original, 20).ToArray());
        code.AddRange([0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53]); // push rax, rcx, rdx, r8–r11
        code.AddRange([0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00]);                        // sub rsp, 0x88
        Stub.SaveXmm(code, 0x20);                                                         // mid-function, anything can be live
        code.AddRange([0x8B, 0xCD]);                                                      // mov ecx, ebp
        code.AddRange([0x48, 0xB8]);                                                      // mov rax, ScaleJp
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<int, int>)&ScaleJp));
        code.AddRange([0xFF, 0xD0]);                                                      // call rax
        code.AddRange([0x8B, 0xE8]);                                                      // mov ebp, eax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);                        // add rsp, 0x88
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax
        code.AddRange(AbsoluteJump(resume));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the JP stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }

    private static byte[] AbsoluteJump(nint target)
    {
        var jump = new byte[14];
        jump[0] = 0xFF;
        jump[1] = 0x25; // jmp [rip+0], then the target
        BitConverter.TryWriteBytes(jump.AsSpan(6), (ulong)target);
        return jump;
    }

    /// <summary>
    /// The EXP for the current target (PS1 0x8018D6xx as ReMixed rewrote it; 0x30C110(mode)): 10 for
    /// mode 1, otherwise 20 split by the target's +0x18C, plus the target's level minus the
    /// attacker's, at least 1; then the EXP rate where Gained EXP Up doubled it, capped at 99 and at
    /// least 1 unless the rate is 0. Nothing while a reaction is applied. The action keeps the best.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void ActionExp(int mode)
    {
        try
        {
            var attacker = *_game.Battle.Attacker;
            var target = *_game.Routines.Target;
            if (*_reacting != 0 || mode == 0 || attacker == null || target == null)
                return;
            var mount = RidingActions.ActingMount(attacker);
            if (mount != null)
                attacker = mount;
            var exp = Exp(mode, attacker, target);
            if (exp > *_actionExp)
                *_actionExp = (byte)exp;
        }
        catch (Exception e)
        {
            Failed("EXP", e);
        }
    }

    internal static int Exp(int mode, byte* attacker, byte* target)
    {
        int split = target[0x18C];
        var basis = mode == 1 ? 10 : split != 0 ? 20 / split : 20;
        var amount = Math.Max(basis + target[Battle.UnitLv] - attacker[Battle.UnitLv], 1);
        var exp = amount * ExpRate / 100;
        if (exp >= 100)
            exp = 99;
        if (exp == 0 && ExpRate != 0)
            exp = 1;
        return exp;
    }

    /// <summary>
    /// The EXP and JP award, without the actor's Gained JP Up (its ×1.5; PS1 0x8017E998, which ReMixed removed). A
    /// rider's use of its mount's skill awards the mount (<see cref="RidingActions.ActingMount"/>).
    /// </summary>
    [UnmanagedCallersOnly]
    private static int Award(int actor, nint shown)
    {
        byte* supports = null;
        try
        {
            var unit = (uint)actor < Battle.UnitCount ? _game.TurnCode.Units + actor * Battle.UnitSize : null;
            var mount = unit != null ? RidingActions.ActingMount(unit) : null;
            if (mount != null)
            {
                Tracer.Line(_game.Log, $"riding: unit {actor}'s EXP and JP go to its mount, unit {mount[Battle.UnitIndex]}");
                actor = mount[Battle.UnitIndex];
                unit = mount;
            }
            if (unit != null && (unit[Battle.UnitSupports + 1] & GainedJpUp) != 0)
            {
                supports = unit + Battle.UnitSupports + 1;
                *supports &= unchecked((byte)~GainedJpUp);
            }
        }
        catch (Exception e)
        {
            Failed("the award", e);
        }
        var r = _award(actor, shown);
        if (supports != null)
            *supports |= GainedJpUp;
        return r;
    }

    /// <summary>The action's JP base times the JP rate (ReMixed's 0x801516C8), from the award's stub.</summary>
    [UnmanagedCallersOnly]
    private static int ScaleJp(int basis)
    {
        try
        {
            var jp = Scale(basis, JpRate);
            if (jp != basis)
                Tracer.Line(_game.Log, $"gains: JP {basis} at {JpRate}% = {jp}");
            return jp;
        }
        catch (Exception e)
        {
            Failed("JP", e);
            return basis;
        }
    }

    /// <summary>ReMixed's rate: amount × rate / 100, at least 1; nothing for no amount or a rate of 0.</summary>
    internal static int Scale(int amount, int rate) => amount <= 0 || rate <= 0 ? 0 : Math.Max(1, amount * rate / 100);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"gains: {what} failed: {e}");
    }
}
