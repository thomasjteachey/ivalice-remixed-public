using System.Runtime.InteropServices;
using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's gun skills and Charge (docs/MECHANICS.md: "Gun skills"): abilities 0x190–0x19D (the old Vertical Jump 3–8 and
/// Charge +1..+20) are Charge abilities with a charge time, an XA bonus and a status set from ReMixed's table, run as
/// formula 5. Installed by <see cref="Rules"/> with "gun-skills". Data: [[id, CT, bonus, status set], …].
/// </summary>
/// <remarks>
/// <para>The remaster splits ids by range: 0x18A–0x195 are Jumps and 0x196–0x19D Charges, each with its own table
/// (0x788F4C + 2 × id and 0x788F54 + 2 × id; not contiguous as on the PS1, with live game state between them). ReMixed
/// moves the boundary to 0x190 and reads one table for 0x190–0x19D. Here the mod's table T (CT, bonus a pair, then the
/// status sets) sits near the image, and each Charge read points at it: the classifier (0x275AFA, for the ability
/// panel), the queue's Charge case (0x28187C, its range test rewritten as lea/cmp/ja, and its CT read), both ability
/// list builders' CT (0x30DF66, 0x30E5C8), and the AI's (0x3205C3). The id-range tests in the AI's estimate (0x319D13,
/// 0x319D74), the camera (0x20CD50), the preview (0x212CD3), the action event (0x206E66) and the target select
/// (0x205068, 0x205132) move to 0x190–0x19D; the unit info, the turn order's entry and the action banner (0x215243,
/// 0x2157C8, 0x227ECC) call only 0x18A–0x18F "Jump". The range needs nothing: the remaster picks it by skillset type
/// (Charge's is the weapon's).</para>
/// <para>The action routine's Charge case (0x309EAD; PS1 0x8018B7E4) goes through a stub: for 0x190–0x19D the bonus into
/// [0x7B0787], the status set (if any) into the action's set id [0x7B077D], and formula 5 (<see cref="Formulas"/>'
/// GunShot); other ids take the Attack path as before, without a bonus, as ReMixed. The game adds the bonus to XA
/// unsigned (six movzx reads); ReMixed's bonuses go down to −6, so those reads become movsx. ReMixed's weapon-XA step
/// (0x8015F540) then keeps XA at 1 or more, cuts Two Shots' (×3/4 with a bow; ×5/8 with a gun, crossbow, instrument or
/// book; ×1/2 otherwise) and doubles it while the AI plans: here at the attack modifiers (0x306E34), which the weapon
/// formulas call right after the bonus (<see cref="AdjustXa"/>).</para>
/// <para>Hook Shot pulls the target a tile toward the attacker after a hit (<see cref="Knockback.Pull"/>, from formula
/// 5). Trick Shot's target can't react (PS1 0x8015D2C0 in the can-react check 0x8018C968; the remaster's 0x30AF88, with
/// the next checks inlined): every triggered reaction, while Abandon and Weapon Guard, which the evade setup checks on
/// its own (0x30DD1C), still work. Two Shots takes Two Spells' two-action path without 0x1D0 (<see cref="TwoSpells"/>).
/// Not here: Trick Shot's target window, which shows only Abandon and Weapon Guard (0x801529F0).</para>
/// </remarks>
internal static unsafe class GunSkills
{
    public const int First = 0x190, Count = 14, HookShot = 0x193, TrickShot = 0x194, TwoShots = 0x195;
    private const int StatusAt = 2 * Count; // T: (CT, bonus) × 14, then a status set a skill

    private static Game _game = null!;
    private static byte* _table;
    private static delegate* unmanaged<int> _canReact; // 0x30AF88's original: non-zero when the target can't react
    private static int _failed;

    // For the offline check.
    internal static byte* Table => _table;
    internal static nint ActionStub { get; private set; }
    internal static List<(string Name, nint At)> Patched { get; } = [];
    internal static byte* Bonus { get; private set; }
    internal static nint CanReactAt { get; private set; }

    /// <summary>The status set of skill <paramref name="ability"/> (0 for none), or -1 outside 0x190–0x19D.</summary>
    internal static int StatusSet(int ability) =>
        _table == null || (uint)(ability - First) >= Count ? -1 : _table[StatusAt + ability - First];

    public static List<string> Install(Game game, JsonElement data)
    {
        _game = game;
        var image = game.Image;
        var baseAt = image.ImageStart;
        var table = GameImage.AllocateCodeNear(baseAt + 0x300000, 64);
        foreach (var row in data.EnumerateArray())
        {
            var k = row[0].GetInt32() - First;
            if ((uint)k >= Count)
                continue;
            table[2 * k] = (byte)row[1].GetInt32();
            table[2 * k + 1] = (byte)(sbyte)row[2].GetInt32();
            table[StatusAt + k] = (byte)row[3].GetInt32();
        }
        long fromImage = (nint)table - baseAt - 2 * First; // table − base − 0x320: image + this + 2 × id = T + 2 × (id − 0x190)
        if (fromImage is < int.MinValue or > int.MaxValue)
            throw new InvalidOperationException("the gun skill table is out of the image's reach");
        var disp = BitConverter.GetBytes((int)fromImage);

        // The patches, checked first and written after, so a mismatch leaves the game as it was.
        var writes = new List<(string Name, nint At, byte[] Bytes)>();
        void Change(string name, string pattern, int offset, byte[] old, byte[] @new)
        {
            var at = image.FindUnique(pattern) + offset;
            if (!new ReadOnlySpan<byte>((void*)at, old.Length).SequenceEqual(old))
                throw new InvalidDataException($"{name} isn't the code this DLL expects");
            writes.Add((name, at, @new));
        }
        void Disp(string name, string pattern, int offset, byte[] old)
        {
            Change(name, pattern, offset, old, disp);
        }
        byte[] Rel(nint at, nint target) => BitConverter.GetBytes(checked((int)(target - (at + 4))));

        // The classifier: Jump up to 0x18F, Charge from 0x190, from T.
        Change("the classifier's Charge range", "41 81 F9 96 01 00 00 73 15 48 8D 81 4C 8F 78 00 4A 8D 04 48 49 89 00 B8 03 00 00 00",
               3, [0x96, 0x01], [0x90, 0x01]);
        Disp("the classifier's Charge table", "41 81 F9 9E 01 00 00 73 15 48 8D 81 54 8F 78 00 4A 8D 04 48 49 89 00 B8 04 00 00 00",
             12, [0x54, 0x8F, 0x78, 0x00]);
        // The queue's Charge case: lea eax, [rdi-0x190]; cmp eax, 13; ja (the same failure), then the CT from T.
        var queue = image.FindUnique(
            "0F B7 C7 66 2B C1 66 3B C2 0F 87 ?? ?? ?? ?? 4D 8B CD 4C 8D 45 E0 49 8B D5 49 8B CF E8 ?? ?? ?? ?? " +
            "4C 8D 05 ?? ?? ?? ?? 41 8A 8C 78 54 8F 78 00");
        var queueFail = queue + 15 + *(int*)(queue + 11);
        var rewrite = new byte[15];
        new byte[] { 0x8D, 0x87, 0x70, 0xFE, 0xFF, 0xFF, 0x83, 0xF8, 0x0D, 0x0F, 0x87 }.CopyTo(rewrite, 0);
        BitConverter.GetBytes(checked((int)(queueFail - (queue + 15)))).CopyTo(rewrite, 11);
        writes.Add(("the queue's Charge range", queue, rewrite));
        writes.Add(("the queue's Charge CT", queue + 0x2C, disp));
        // The two list builders' CT: 0x190..0x19D, lea rcx, [T + 0xC] (their read is [rcx + 2 × id − 0x32C]).
        foreach (var (name, register) in new[] { ("the whole-skillset list's CT", "C6"), ("the menu list's CT", "C7") })
        {
            var at = image.FindUnique($"B9 96 01 00 00 0F B7 {register} 66 2B C1 66 83 F8 07 77 ?? 48 8D 0D ?? ?? ?? ?? 45 33 C9");
            writes.Add((name + " (range)", at + 1, [0x90, 0x01]));
            writes.Add((name + " (count)", at + 14, [0x0D]));
            writes.Add((name + " (table)", at + 20, Rel(at + 20, (nint)table + 0xC)));
        }
        // The AI's CT for a Charge.
        Disp("the AI's Charge CT", "41 8A 84 44 54 8F 78 00 88 05 ?? ?? ?? ?? 43 0F B6 84 27", 4, [0x54, 0x8F, 0x78, 0x00]);
        // The id-range tests.
        Change("the AI estimate's Charge test", "8D 83 6A FE FF FF 83 F8 07 76 08 81 FB 92 00 00 00", 2, [0x6A, 0xFE, 0xFF, 0xFF, 0x83, 0xF8, 0x07],
               [0x70, 0xFE, 0xFF, 0xFF, 0x83, 0xF8, 0x0D]);
        Change("the AI estimate's special test", "8D 81 6B FE FF FF 83 F8 08 76", 2, [0x6B, 0xFE, 0xFF, 0xFF, 0x83, 0xF8, 0x08],
               [0x70, 0xFE, 0xFF, 0xFF, 0x83, 0xF8, 0x0D]);
        Change("the camera's Charge test", "41 8D 44 24 96 66 2B D0 66 83 FA 07", 4, [0x96, 0x66, 0x2B, 0xD0, 0x66, 0x83, 0xFA, 0x07],
               [0x90, 0x66, 0x2B, 0xD0, 0x66, 0x83, 0xFA, 0x0D]);
        Change("the preview's Charge test", "B9 96 01 00 00 41 0F B7 C1 66 2B C1 B9 07 00 00 00 66 3B C1 76", 1,
               [0x96, 0x01, 0x00, 0x00, 0x41, 0x0F, 0xB7, 0xC1, 0x66, 0x2B, 0xC1, 0xB9, 0x07],
               [0x90, 0x01, 0x00, 0x00, 0x41, 0x0F, 0xB7, 0xC1, 0x66, 0x2B, 0xC1, 0xB9, 0x0D]);
        Change("the action event's Charge test", "8D 81 6A FE FF FF 83 F8 07 77 ?? C7 45 A8 05 00 00 00", 2,
               [0x6A, 0xFE, 0xFF, 0xFF, 0x83, 0xF8, 0x07], [0x70, 0xFE, 0xFF, 0xFF, 0x83, 0xF8, 0x0D]);
        Change("the target select's Charge range", "44 89 43 08 B8 8A 01 00 00 66 3B C2 77 24 B8 96 01 00 00", 15, [0x96], [0x90]);
        Change("the target select's other Charge range", "E9 CD 00 00 00 B8 8A 01 00 00 66 3B C2 77 24 B8 96 01 00 00", 16,
               [0x96], [0x90]);
        Change("the unit info's Jump name", "BA 8A 01 00 00 0F B7 C1 66 2B C2 66 83 F8 0B 77", 14, [0x0B], [0x05]);
        Change("the turn order's Jump entry", "8D 81 76 FE FF FF 83 F8 0B 76", 8, [0x0B], [0x05]);
        Change("the action banner's Jump name", "41 B8 8A 01 00 00 0F B7 C1 66 41 2B C0 66 83 F8 0B", 16, [0x0B], [0x05]);

        // The action routine's Charge case and the bonus's readers.
        var charge = image.FindUnique(
            "B9 96 01 00 00 0F B7 C7 66 2B C1 66 41 3B C0 76 02 8B F9 0F B7 C7 41 8A 84 44 ?? ?? ?? ?? 88 05 ?? ?? ?? ?? " +
            "F6 05 ?? ?? ?? ?? 04 41 8A 4E 02");
        var bonus = Bonus = (byte*)GameImage.Rip(charge + 30, 2, 6);
        var twoHands = GameImage.Rip(charge + 36, 2, 7);
        var statusId = (nint)game.RuleCode.StatusId;
        var attackPath = charge + 36;   // test byte [twoHands], 4
        var formulaJump = charge + 47;  // after mov cl, [r14+2]: the je on the Two Hands test
        var stub = GameImage.AllocateCodeNear(charge, 96);
        var code = new List<byte>
        {
            0x0F, 0xB7, 0xC7,                   // movzx eax, di
            0x2D, 0x90, 0x01, 0x00, 0x00,       // sub eax, 0x190
            0x83, 0xF8, 0x0D,                   // cmp eax, 13
            0x77, 0x00,                         // ja out (patched below)
            0x48, 0x8D, 0x0D, 0, 0, 0, 0,       // lea rcx, [T]
            0x0F, 0xB6, 0x4C, 0x41, 0x01,       // movzx ecx, byte [rcx + rax*2 + 1]: the bonus
            0x88, 0x0D, 0, 0, 0, 0,             // mov [bonus], cl
            0x48, 0x8D, 0x0D, 0, 0, 0, 0,       // lea rcx, [T + statuses]
            0x0F, 0xB6, 0x0C, 0x01,             // movzx ecx, byte [rcx + rax]
            0x84, 0xC9,                         // test cl, cl
            0x74, 0x06,                         // jz +6
            0x88, 0x0D, 0, 0, 0, 0,             // mov [status id], cl
            0xF6, 0x05, 0, 0, 0, 0, 0x04,       // test byte [two hands], 4
            0xB1, 0x05,                         // mov cl, 5
            0xE9, 0, 0, 0, 0,                   // jmp the formula's je
            0xE9, 0, 0, 0, 0,                   // out: jmp the Attack path
        };
        var bytes = code.ToArray();
        void Put(int at, nint target, int after) =>
            BitConverter.TryWriteBytes(bytes.AsSpan(at), checked((int)(target - ((nint)stub + after))));
        Put(16, (nint)table, 20);
        Put(27, (nint)bonus, 31);
        Put(34, (nint)table + StatusAt, 38);
        Put(48, statusId, 52);
        Put(54, twoHands, 59);
        Put(62, formulaJump, 66);
        Put(67, attackPath, 71);
        bytes[12] = (byte)(66 - 13); // ja out
        bytes.CopyTo(new Span<byte>(stub, bytes.Length));
        var jump = new byte[36];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.TryWriteBytes(jump.AsSpan(1), checked((int)((nint)stub - (charge + 5))));
        writes.Add(("the action routine's Charge case", charge, jump));
        // The bonus's readers, around the weapon formulas: movzx e?x, byte [bonus] (0F B6 05/0D) becomes movsx (0F BE).
        var readers = new List<nint>();
        var from = (byte*)(nint)game.Routines.WeaponAttack - 0x2000;
        for (var k = 0; k < 0x4000; k++)
        {
            var p = from + k;
            if (p[0] == 0x0F && p[1] == 0xB6 && (p[2] == 0x05 || p[2] == 0x0D) && p + 7 + *(int*)(p + 3) == bonus)
                readers.Add((nint)p);
        }
        if (readers.Count != 6)
            throw new InvalidDataException($"found {readers.Count} reads of the Charge bonus, not 6");
        foreach (var at in readers)
            writes.Add(("a read of the Charge bonus", at + 1, [0xBE]));

        // Trick Shot: the can-react check (the target from [0x186AF68]; a caller keeps r11 across it).
        var canReact = image.FindUnique(
            "48 83 EC 28 48 8B 0D ?? ?? ?? ?? 45 33 D2 44 39 15 ?? ?? ?? ?? 74 0A B8 01 00 00 00 48 83 C4 28 C3 " +
            "BA 04 00 00 00 E8");

        foreach (var (name, at, write) in writes)
        {
            GameImage.WriteCode(at, write);
            Patched.Add((name, at));
        }
        _canReact = (delegate* unmanaged<int>)game.Hooks.Hook(
            canReact, Stub.KeepingRegisters((nint)(delegate* unmanaged<int>)&CanReact), "the can-react check");
        CanReactAt = canReact;
        Supports.HookAttackMods(game);
        _table = table;
        ActionStub = (nint)stub;
        return [$"ReMixed's gun skills and Charge (0x190–0x19D: CT, bonus and statuses from its table; {writes.Count} changes)"];
    }

    /// <summary>
    /// ReMixed's weapon-XA step for 0x190–0x19D (0x8015F540), after the game added the bonus: Two Shots' cut by weapon
    /// type and its doubling while the AI plans, then XA of at least 1.
    /// </summary>
    internal static void AdjustXa()
    {
        if (_table == null)
            return;
        var ability = *_game.Battle.AbilityId;
        if ((uint)(ability - First) >= Count)
            return;
        int xa = *_game.Battle.XA, before = xa;
        if (ability == TwoShots)
        {
            var item = _game.Routines.GetItem(*_game.Routines.WeaponItemId);
            var type = item == null ? 0 : item[5];
            xa = type == 12 ? xa * 3 / 4 : type is 10 or 11 or 13 or 14 ? xa * 5 / 8 : xa / 2;
            if (*_game.Routines.Simulating == 1)
                xa *= 2; // the AI values it as the two shots it fires
        }
        xa = Math.Max(xa, 1);
        if (xa != before)
        {
            *_game.Battle.XA = (short)xa;
            Tracer.Line(_game.Log, $"gun skills: 0x{ability:X3}'s XA {before} becomes {xa}");
        }
    }

    [UnmanagedCallersOnly]
    private static int CanReact()
    {
        var cant = _canReact();
        try
        {
            if (cant == 0 && _table != null && *_game.Battle.AbilityId == TrickShot)
            {
                Tracer.Line(_game.Log, "gun skills: Trick Shot's target can't react");
                return 1;
            }
        }
        catch (Exception e)
        {
            if (Interlocked.Increment(ref _failed) <= 3)
                _game.Log.Error($"gun skills: the can-react check failed: {e}");
        }
        return cant;
    }

}
