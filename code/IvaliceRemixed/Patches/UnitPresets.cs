using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's unit presets (docs/MECHANICS.md, "Unit presets"; docs/analysis/battle-80150000.md, section 13): a battle-data
/// unit's byte 37 picks one of its 24 presets, which ReMixed's code applies each time the unit's stats are worked out:
/// statuses OR'd into its innate, immune and current ones (a corpse, an undead, a treasure chest, a unit that starts Dead
/// or Critical, immunity to Invite for its missions' enemies), its ability bits (Monster Skill), its elements, its class
/// evade and its HP, MP, Speed, PA and MA scaled as they're worked out (HP and PA ×2, Speed ×3/5, MP 0, a chest's HP 1),
/// and a tint for its sprite. The remaster's battle data can't carry the byte (a nonzero byte 37 crashed it, seen on the
/// Deck), so the build writes ReMixed's tables and each scenario's 16 units' presets in <see cref="FileName"/>.
/// Installed by <see cref="Rules"/> with "unit-presets", after "remixed-scenarios".
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Which unit has which: the unit making (0x27128C) makes battle slot k from the scenario's record k with 0x275CA4
/// (the unit, the record, 0, 0, …), hooked here: a record of the entry the setup was handed (the loaded battle data,
/// or the mod's file's for <see cref="RemixedScenarios.OwnEntries"/>) in a scenario with presets gives its slot that
/// unit's preset (ReMixed's unit +0x169, which its loader copies from the record's byte 37); 0xE0's record
/// (<see cref="CreateUnit"/>) its parameter p7, which ReMixed's record has at +0x25 too; any other unit none. 0xFF picks
/// 1–14 at random (0x80155184). Every slot's is cleared as a battle is set up.</item>
/// <item>The stats: ReMixed multiplies each stat's raw value × the job's multiplier by num / (den + 1) before the divide
/// (SCUS 0x8005B97C → 0x8015511C, keeping the product's low word). The remaster's step (0x276EBC: the level-ups and
/// the stats, from the unit build and the recalculation) has the product at 0x276FC4 (imul eax, r8d; div ecx: r10 the
/// unit, r9d the stat, HP, MP, Speed, PA, MA), which jumps to a stub doing the same from <see cref="Scales"/>.</item>
/// <item>The rest, at the start of the recalculation (SCUS 0x8005C5E0 → 0x80155184; the remaster's 0x2779EC, hooked):
/// the class evade (+0x4B) × num / (den + 1), at most 100; three status sets OR'd into innate (+0x57), immune (+0x5C) and
/// current (+0x61); an ability set OR'd into +0x94–0x9E; elements (+0x76–0x7A) given and taken; the preset's last byte
/// OR'd into +0x7A. ReMixed also changes Move and Jump by its scaling's last two bytes, which are 0 in all four of its
/// scalings: not ported. ReMixed runs all this for every unit, preset 0 too, which takes Fire off +0x7A's innate
/// strengthening (its element set 0 runs into the tints); here only units with a preset.</item>
/// <item>Not ported (the display port): the tint and blending of presets 10–12 and 16–18 (dark, gold, grey, bronze; 18
/// half-transparent).</item>
/// </list>
/// </remarks>
internal static unsafe class UnitPresets
{
    internal const string FileName = "remixed-unit-presets.bin";
    /// <summary>ReMixed's tables, by offset from BATTLE.BIN 0x801553F4 (the file's first part): the stat scalings (14
    /// bytes: num and den for HP, MP, Speed, PA and MA, the class evade's, Move's and Jump's change), the status sets
    /// (5), the ability sets (11), the element sets (4 apart, 5 read), the tints (5), the presets (9: tint, scaling,
    /// innate, immune and current statuses, abilities, elements given and taken, a byte for +0x7A) and the random
    /// pick (0xFF: from the second byte up to the third).</summary>
    internal const int ScalingAt = 0x00, ScalingSize = 14, StatusAt = 0x38, StatusSize = 5, AbilityAt = 0x74, AbilitySize = 11,
                       ElementAt = 0x95, ElementStride = 4, ElementSize = 5, PresetAt = 0xB7, PresetSize = 9, RandomAt = 0x18F;
    /// <summary>The remaster's unit fields (PS1 in brackets): class evade (+0x43), innate statuses (+0x4E; immune and
    /// current 5 and 10 bytes on), elements (+0x6D), abilities (+0x8B).</summary>
    internal const int ClassEvade = 0x4B, Statuses = 0x57, Elements = 0x76, Abilities = 0x94;
    internal const int Stats = 5, StubSize = 96;

    /// <summary>The recalculation (0x2779EC) at +0x1D: and word [rcx+0x3C], 0; lea r14, [rcx+0x3B]; inc edx;
    /// mov byte [r14], 0; mov rbx, rcx; call 0x276EBC.</summary>
    internal const string RecalculatePattern = "66 83 61 3C 00 4C 8D 71 3B FF C2 41 C6 06 00 48 8B D9 E8";
    private const int RecalculateOffset = 0x1D;
    /// <summary>The stat step's product (0x276FB8): movzx eax, byte [r12+1]; cmovb ecx, r13d; xor edx, edx; then at +12
    /// imul eax, r8d; div ecx; mov edx, eax; mov eax, 1; shr edx, 0xE.</summary>
    internal const string ProductPattern = "41 0F B6 44 24 01 41 0F 42 CD 33 D2 41 0F AF C0 F7 F1 8B D0 B8 01 00 00 00 C1 EA 0E";
    private const int ProductOffset = 12, ProductBytes = 6;
    /// <summary>The loaded battle data's records (0x28416C's: lea rax, [rcx+rcx*4]; shl rax, 7; add rax, [buffer]).</summary>
    internal const string EntryDataPattern = "48 8D 04 89 48 C1 E0 07 48 03 05 ?? ?? ?? ?? C3";

    internal static byte[] Tables = [];
    /// <summary>The mod's scenario -> its 16 records' presets.</summary>
    internal static readonly Dictionary<int, byte[]> Scenarios = [];
    /// <summary>Each battle slot's preset (ReMixed's unit +0x169), 0 for none. Native, so ReMixed's events read and write
    /// it as the PS1's +0x169 (<see cref="RemixedEvents"/>: the Nemesis's return, 0x11D, gives its Nemesis one).</summary>
    internal static readonly byte* Slots = (byte*)NativeMemory.AllocZeroed(Battle.UnitCount);
    /// <summary>The preset each slot's <see cref="Scales"/> were worked out for: a preset an event writes takes its scales
    /// at the unit's next recalculation.</summary>
    private static readonly byte* Scaled = (byte*)NativeMemory.AllocZeroed(Battle.UnitCount);
    /// <summary>The stub's table: per battle slot and stat (HP, MP, Speed, PA, MA), num | (den + 1) &lt;&lt; 8, 0 for none.</summary>
    internal static readonly ushort* Scales = (ushort*)NativeMemory.AllocZeroed(Battle.UnitCount * Stats * sizeof(ushort));
    /// <summary>The preset 0xE0 gives the unit it makes next (its record's +0x25), −1 for none.</summary>
    internal static int Pending = -1;

    internal static nint ProductAt, Stub, RecalculateAt;
    private static Game _game = null!;
    private static byte* _units;
    private static byte** _entryData;
    private static delegate* unmanaged<byte*, byte*, int, int, nint, long> _make;
    /// <summary>The recalculation: the unit, its mode, and r8 and r9 as they came (it hands r8 on to the stat step,
    /// 0x276EBC, as that step's third argument, which it reads for an even mode: a hook that dropped it crashed the
    /// game).</summary>
    private static delegate* unmanaged<byte*, int, nint, nint, void> _recalculate;
    private static readonly HashSet<string> Failures = [];

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        var file = Path.Combine(Path.GetDirectoryName(typeof(UnitPresets).Assembly.Location) ?? "", FileName);
        if (File.Exists(file))
            Load(File.ReadAllBytes(file));
        else
            game.Log.Info($"unit presets: no {FileName} beside the DLL; no unit has one");
        _units = game.TurnCode.Units;
        _entryData = (byte**)GameImage.Rip(image.FindUnique(EntryDataPattern) + 8, 3, 7);
        if (!CreateUnit.Found)
            CreateUnit.Find(image);
        RecalculateAt = image.FindUnique(RecalculatePattern) - RecalculateOffset;
        if (*(uint*)RecalculateAt != 0x48C48B48) // mov rax, rsp; mov [rax+8], rbx
            throw new InvalidDataException("the recalculation isn't laid out as this DLL expects");
        ProductAt = image.FindUnique(ProductPattern) + ProductOffset;
        Stub = (nint)GameImage.AllocateCodeNear(ProductAt, StubSize);
        WriteStub((byte*)Stub, ProductAt + ProductBytes, _units, Scales);
        var jump = new byte[ProductBytes];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)(Stub - (ProductAt + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(ProductAt, jump);
        _make = (delegate* unmanaged<byte*, byte*, int, int, nint, long>)game.Hooks.Hook(
            CreateUnit.MakeAt, (nint)(delegate* unmanaged<byte*, byte*, int, int, nint, long>)&Make, "the unit making");
        _recalculate = (delegate* unmanaged<byte*, int, nint, nint, void>)game.Hooks.Hook(
            RecalculateAt, (nint)(delegate* unmanaged<byte*, int, nint, nint, void>)&Recalculate, "the stat recalculation");
        BattleEvents.OnStart(game, "unit presets", Clear);
        game.Log.Info($"unit presets: ReMixed's tables ({Tables.Length} bytes) and {Scenarios.Count} scenario(s)' units");
    }

    /// <summary>The file: u16 the tables' length, the tables, u16 count, then (u16 the mod's scenario, 16 presets).</summary>
    internal static void Load(byte[] data)
    {
        var length = BitConverter.ToUInt16(data, 0);
        Tables = data[2..(2 + length)];
        var at = 2 + length;
        int count = BitConverter.ToUInt16(data, at);
        at += 2;
        Scenarios.Clear();
        for (var k = 0; k < count; k++, at += 18)
            Scenarios[BitConverter.ToUInt16(data, at)] = data[(at + 2)..(at + 18)];
    }

    /// <summary>A new battle: no slot has a preset until its unit is made.</summary>
    internal static void Clear()
    {
        new Span<byte>(Slots, Battle.UnitCount).Clear();
        new Span<byte>(Scaled, Battle.UnitCount).Clear();
        new Span<ushort>(Scales, Battle.UnitCount * Stats).Clear();
        Pending = -1;
    }

    /// <summary>The unit making (0x275CA4): the slot's preset first (<see cref="PresetFor"/>), then the game's.</summary>
    [UnmanagedCallersOnly]
    private static long Make(byte* unit, byte* record, int p3, int mode, nint p5)
    {
        try
        {
            var slot = Slot(unit);
            if (slot >= 0)
                Set(slot, PresetFor(record, mode));
        }
        catch (Exception e)
        {
            Fail("the unit making", e);
        }
        return _make(unit, record, p3, mode, p5);
    }

    /// <summary>The preset of the unit made from <paramref name="record"/>: 0xE0's (<see cref="Pending"/>), or its
    /// scenario's record's (by its place among the 16 the setup was handed); 0 for any other, or for a record that
    /// stands for a formation's unit (+1 &amp; 8).</summary>
    internal static int PresetFor(byte* record, int mode)
    {
        if (Pending >= 0)
        {
            var given = Pending;
            Pending = -1;
            return Pick(given);
        }
        if (mode != 0 || record == null || (record[1] & 8) != 0)
            return 0;
        var scenario = Nemesis.GetVariable(0x27);
        if (!Scenarios.TryGetValue(scenario, out var presets))
            return 0;
        var entry = Nemesis.GetVariable(0x32);
        var bases = new List<nint>();
        if (RemixedScenarios.OwnEntries.TryGetValue(scenario, out var own))
            bases.Add(own);
        if (*_entryData != null)
            bases.Add((nint)(*_entryData + (entry < 0x200 ? entry & 0x7F : entry - 0x200) * RemixedScenarios.EntryBytes));
        foreach (var start in bases)
        {
            var offset = (nint)record - start;
            if (offset >= 0 && offset < RemixedScenarios.EntryBytes && offset % CreateUnit.RecordSize == 0)
                return Pick(presets[offset / CreateUnit.RecordSize]);
        }
        return 0;
    }

    /// <summary>ReMixed's random pick for 0xFF (0x80155184): from the table's second byte up to before its third.</summary>
    private static int Pick(int preset)
    {
        if (Tables.Length <= RandomAt + 2 || preset != Tables[RandomAt])
            return preset;
        int low = Tables[RandomAt + 1], high = Tables[RandomAt + 2];
        return high > low ? low + Random.Shared.Next(high - low) : low;
    }

    /// <summary>The slot's preset, and its stat scales for the stub (none where a scale is 1).</summary>
    internal static void Set(int slot, int preset)
    {
        if (Tables.Length <= RandomAt + 2 || PresetAt + PresetSize * (preset + 1) > RandomAt)
            preset = 0;
        Slots[slot] = Scaled[slot] = (byte)preset;
        var scales = new Span<ushort>(Scales + slot * Stats, Stats);
        scales.Clear();
        if (preset == 0)
            return;
        var scaling = ScalingAt + ScalingSize * Tables[PresetAt + PresetSize * preset + 1];
        for (var k = 0; k < Stats; k++)
        {
            int num = Tables[scaling + 2 * k], den = Tables[scaling + 2 * k + 1];
            if ((num != 1 || den != 0) && den != 0xFF)
                scales[k] = (ushort)(num | (den + 1) << 8);
        }
    }

    /// <summary>The recalculation (0x2779EC): the slot's preset applied first (<see cref="Apply"/>), as ReMixed does at
    /// its start, then the game's.</summary>
    [UnmanagedCallersOnly]
    private static void Recalculate(byte* unit, int mode, nint r8, nint r9)
    {
        try
        {
            var slot = Slot(unit);
            if (slot >= 0 && Slots[slot] != Scaled[slot])
                Set(slot, Slots[slot]); // written by an event since: its scales too
            if (slot >= 0 && Slots[slot] != 0)
                Apply(unit, Slots[slot], Tables);
        }
        catch (Exception e)
        {
            Fail("the recalculation", e);
        }
        _recalculate(unit, mode, r8, r9);
    }

    /// <summary>ReMixed's part of the recalculation (0x80155184, after its random pick) for <paramref name="preset"/>.</summary>
    internal static void Apply(byte* unit, int preset, ReadOnlySpan<byte> tables)
    {
        var record = tables.Slice(PresetAt + PresetSize * preset, PresetSize);
        var scaling = tables.Slice(ScalingAt + ScalingSize * record[1], ScalingSize);
        unit[ClassEvade] = (byte)Math.Min((uint)(unit[ClassEvade] * scaling[10]) / (uint)(scaling[11] + 1), 100u);
        for (var set = 0; set < 3; set++)
        {
            var statuses = tables.Slice(StatusAt + StatusSize * record[2 + set], StatusSize);
            for (var k = 0; k < StatusSize; k++)
                unit[Statuses + StatusSize * set + k] |= statuses[k];
        }
        var abilities = tables.Slice(AbilityAt + AbilitySize * record[5], AbilitySize);
        for (var k = 0; k < AbilitySize; k++)
            unit[Abilities + k] |= abilities[k];
        var given = tables.Slice(ElementAt + ElementStride * record[6], ElementSize);
        var taken = tables.Slice(ElementAt + ElementStride * record[7], ElementSize);
        for (var k = 0; k < ElementSize; k++)
            unit[Elements + k] = (byte)((unit[Elements + k] | given[k]) & ~taken[k]);
        unit[Elements + ElementSize - 1] |= record[8];
    }

    private static int Slot(byte* unit)
    {
        var offset = unit - _units;
        return offset >= 0 && offset < Battle.UnitCount * Battle.UnitSize && offset % Battle.UnitSize == 0
            ? (int)(offset / Battle.UnitSize) : -1;
    }

    /// <summary>
    /// The stub at the stat step's product: eax the job's multiplier, r8d the raw stat, ecx the divisor, edx 0, r10 the
    /// unit, r9d the stat. The product, × num / (den + 1) (its low word, as the PS1's) for a battle slot with a scale in
    /// <paramref name="scales"/>, then the game's divide, and back to <paramref name="back"/>.
    /// </summary>
    internal static void WriteStub(byte* stub, nint back, byte* units, ushort* scales)
    {
        var code = new List<byte>
        {
            0x41, 0x0F, 0xAF, 0xC0,             // imul eax, r8d (the game's)
            0x51,                               // push rcx
            0x53,                               // push rbx
            0x48, 0xBB,                         // mov rbx, units
        };
        code.AddRange(BitConverter.GetBytes((long)units));
        code.AddRange([
            0x49, 0x8B, 0xCA,                   // mov rcx, r10
            0x48, 0x2B, 0xCB,                   // sub rcx, rbx
            0x48, 0x81, 0xF9,                   // cmp rcx, 21 * 0x200
        ]);
        code.AddRange(BitConverter.GetBytes(Battle.UnitCount * Battle.UnitSize));
        code.AddRange([0x73, 0x00]);            // jae done (filled in below)
        var firstSkip = code.Count - 1;
        code.AddRange([
            0xC1, 0xE9, 0x09,                   // shr ecx, 9 (the slot)
            0x8D, 0x0C, 0x89,                   // lea ecx, [rcx+rcx*4]
            0x41, 0x03, 0xC9,                   // add ecx, r9d (the stat)
            0x48, 0xBB,                         // mov rbx, scales
        ]);
        code.AddRange(BitConverter.GetBytes((long)scales));
        code.AddRange([
            0x0F, 0xB7, 0x0C, 0x4B,             // movzx ecx, word [rbx+rcx*2]
            0x84, 0xED,                         // test ch, ch
            0x74, 0x00,                         // je done
        ]);
        var secondSkip = code.Count - 1;
        code.AddRange([
            0x0F, 0xB6, 0xDD,                   // movzx ebx, ch (den + 1)
            0x0F, 0xB6, 0xC9,                   // movzx ecx, cl (num)
            0xF7, 0xE1,                         // mul ecx
            0x33, 0xD2,                         // xor edx, edx (the product's low word)
            0xF7, 0xF3,                         // div ebx
        ]);
        var done = code.Count;
        code[firstSkip] = (byte)(done - (firstSkip + 1));
        code[secondSkip] = (byte)(done - (secondSkip + 1));
        code.AddRange([
            0x5B,                               // done: pop rbx
            0x59,                               // pop rcx
            0x33, 0xD2,                         // xor edx, edx
            0xF7, 0xF1,                         // div ecx (the game's)
            0xFF, 0x25, 0, 0, 0, 0,             // jmp [rip+0] → back
        ]);
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the unit presets' stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    private static void Fail(string where, Exception e)
    {
        if (Failures.Add(where))
            _game.Log.Error($"unit presets: {where}: {e.Message}");
    }
}
