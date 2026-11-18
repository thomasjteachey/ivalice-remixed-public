using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Fixed or Minimum Levels and Enemy Level Boost options (docs/MECHANICS.md: "Improved Level
/// Scaling", "Power modes"; SCUS 0x8005ADB4, 0x8005CBD0 → 0x80027124). Installed by <see cref="Rules"/>
/// with "level-options"; with ReMixed's defaults it changes nothing.
/// </summary>
/// <remarks>
/// <para>
/// Building a unit from its battle data (0x27612C) reads the record's level (+3): 0 or 0xFE scales
/// with the party level P (0xD405AB: from P − P/8 to P), 1–99 is fixed, 100 and up is P plus the
/// excess; the result is capped at 99. P is the highest level in the roster, capped at 99 (0x27128C).
/// ReMixed: with flag 0xE4, a fixed level below P − P/8 scales instead; with power mode n, a fixed
/// level on a non-player team gets 49 × n more, and P is at least 57 × n.
/// </para>
/// <para>
/// Here both apply to non-player units only: ReMixed's Fixed or Minimum Levels doesn't test the team,
/// but on the PS1 the roster's own units don't come through that step, and on the remaster that isn't
/// certain.
/// </para>
/// <para>
/// P leaves out roster units away on an errand (ReMixed's 0x80027180, from 0x8005CBD0: the errand byte, the PS1's
/// +0xD0 and the remaster's +0x11E, &amp; 0xF), where vanilla takes the whole roster: the roster loop's test (0x27131E)
/// jumps to a stub that adds it. ReMixed's default ("only deployed units", which re-runs the enemies' setup after the
/// formation screen; the remaster builds the enemies in 0x27128C before it) isn't ported.
/// </para>
/// </remarks>
internal static unsafe class LevelScaling
{
    private static Game _game = null!;
    private static byte* _partyLevel; // P (0xD405AB)
    private static byte* _floor;      // P's minimum: 57 × the boost
    private static int _failed;
    private static nint* _formation; // task 6's function and state when P was set, and how often P was set
    private static readonly List<(int Index, byte Level)> Scaled = []; // this battle's units built with P (resolved level)
    private static bool _deployedChecked;
    /// <summary>Set by a ReMixed event that turns the re-run off for its battle (var 0x70 0xFEFE); used up by the next
    /// <see cref="AfterFormation"/>.</summary>
    private static bool _declined;
    /// <summary>The battle setup's enemy level sum and highest level (PS1 0x80193844, 0x80193920), which ReMixed's
    /// re-run adds up again: the rebuild changes them by the levels it changes.</summary>
    private static ushort* _levelSum;
    private static byte* _levelMax;
    /// <summary>The unit build's steps redone for a new level: the level-1 base stats (0x2757B0: the five 3-byte
    /// stats' addresses, then 0 a man, 1 a woman, 3 a monster), the level-ups to the unit's level (0x276EBC: the unit,
    /// 0, 0; for a generated unit, +2 = 0xFE), and the recalculation (0x278688: the unit, 0). The offline check swaps
    /// the last.</summary>
    private static delegate* unmanaged<byte**, int, void> _baseStats;
    private static delegate* unmanaged<byte*, int, int, void> _levelUps;
    internal static delegate* unmanaged<byte*, int, void> Recalculate;
    internal const int RawStats = 0x7B; // five 3-byte stats: HP, MP, Speed, PA, MA

    internal const int StubSize = 192;
    private const int Scaling = 0xFE, BoostPerMode = 49, FloorPerMode = 57, MaxLevel = 99;

    // For the offline check.
    internal static nint LevelAt { get; private set; }
    internal static nint Scales { get; private set; }
    internal static nint Fixed { get; private set; }
    internal static nint High { get; private set; }
    internal static nint PartyAt { get; private set; }
    internal static nint PartyCallee { get; private set; }
    internal static nint PartyBack { get; private set; }
    internal static nint RosterAt { get; private set; }
    internal const int RosterStubSize = 64, Errand = 0x11E, RosterLevel = 0x1D;
    internal static byte* PartyLevel => _partyLevel;
    internal static byte* Floor => _floor;

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        _baseStats = (delegate* unmanaged<byte**, int, void>)GameImage.Rip(
            image.FindUnique("48 8D 83 87 00 00 00 48 89 44 24 40 E8") + 12, 1, 5);
        _levelUps = (delegate* unmanaged<byte*, int, int, void>)GameImage.Rip(
            image.FindUnique("45 33 C0 33 D2 48 8B CB E8 ?? ?? ?? ?? 80 7B 02 FE") + 8, 1, 5);
        Recalculate = (delegate* unmanaged<byte*, int, void>)GameImage.Rip(
            image.FindUnique("4D 8D 49 03 49 83 FA 05 7C ?? 33 D2 48 8B CB E8") + 15, 1, 5);
        var levels = image.FindUnique(EventTables.EnemyLevelsPattern);
        _levelSum = (ushort*)GameImage.Rip(levels + 4, 3, 7);
        _levelMax = (byte*)GameImage.Rip(levels + 11, 2, 6);
        BattleEvents.OnStart(game, "level scaling's deployed units", () =>
        {
            lock (Scaled)
                Scaled.Clear();
            _deployedChecked = false;
        });
        // The level: bpl = 100, esi = 99, the record's +0 to the unit; then +3: scaling, fixed, or P + excess.
        var level = image.FindUnique(
            "40 B5 64 8A 03 BE 63 00 00 00 88 07 41 BB FF 7F 00 00 C6 47 02 FF 44 8A 53 03 41 8D 42 02 A8 FD 74 ?? " +
            "44 3A D6 76 ?? 8A 05 ?? ?? ?? ??");
        // P: the roster's highest level, capped at 99, then stored.
        var party = image.FindUnique("B9 63 00 00 00 41 0F B6 C2 44 3A D1 0F 47 C1 B9 39 00 00 00 88 05 ?? ?? ?? ?? E8 ?? ?? ?? ?? B9 09 00 00 00");
        var partyLevel = (byte*)GameImage.Rip(level + 39, 2, 6);
        if ((byte*)GameImage.Rip(party + 20, 2, 6) != partyLevel)
            throw new InvalidDataException("the party level and the level step disagree");
        _ = game.Battle; // found now, so a missing pattern stops this here
        _partyLevel = partyLevel;
        _floor = (byte*)NativeMemory.AllocZeroed(1);
        *_floor = (byte)(FloorPerMode * (int)Options.Current.EnemyLevelBoost);
        Options.Changed += () => *_floor = (byte)(FloorPerMode * (int)Options.Current.EnemyLevelBoost); // a save's own

        var stubs = GameImage.AllocateCode(2 * StubSize);
        var scales = level + 34 + *(sbyte*)(level + 33);
        var fixedLevel = level + 39 + *(sbyte*)(level + 38);
        WriteLevelStub(stubs, scales, fixedLevel, level + 39);
        var callee = GameImage.Rip(party + 26, 1, 5);
        // For the in-game check of Improved Level Scaling's default: the task table (the queue 0x2F7DE4 fills; the
        // formation screen 0x2FC870 is task 6), read when P is set, to tell whether the formation is over by then.
        var tasks = GameImage.Rip(image.FindUnique("48 8B 05 ?? ?? ?? ?? 45 33 D2 44 8B C1 44 89 15 ?? ?? ?? ?? 41 C1 E0 0B"), 3, 7);
        _formation = (nint*)NativeMemory.AllocZeroed(24);
        WritePartyStub(stubs + StubSize, _floor, partyLevel, callee, party + 31, tasks, _formation);
        // P's roster loop: cmp [slot+1], 0xFF (r14b); je next; movzx ecx, byte [slot+0x1D] (r8); then the max.
        var roster = image.FindUnique("44 38 30 74 11 41 0F B6 08 41 3A CA 41 0F B6 C2 0F 47 C1");
        if (roster - party is > 0 or < -0x80)
            throw new InvalidDataException("the roster loop isn't P's");
        var rosterStub = GameImage.AllocateCodeNear(roster, RosterStubSize);
        WriteRosterStub(rosterStub, roster + 9, roster + 5 + *(sbyte*)(roster + 4));
        Patch(level + 22, 17, (nint)stubs);
        Patch(party + 15, 16, (nint)(stubs + StubSize));
        var jump = new byte[9];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)rosterStub - (roster + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(roster, jump);
        RosterAt = roster;
        LevelAt = level + 22;
        Scales = scales;
        Fixed = fixedLevel;
        High = level + 39;
        PartyAt = party + 15;
        PartyCallee = callee;
        PartyBack = party + 31;
    }

    private static void Patch(nint at, int length, nint stub)
    {
        var jump = new byte[length];
        Array.Fill(jump, (byte)0xCC);
        jump[0] = 0xFF;
        jump[1] = 0x25;
        BitConverter.TryWriteBytes(jump.AsSpan(2), 0);
        BitConverter.TryWriteBytes(jump.AsSpan(6), (ulong)stub);
        GameImage.WriteCode(at, jump);
    }

    private static void AbsoluteJump(List<byte> code, nint target)
    {
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0]);
        code.AddRange(BitConverter.GetBytes((ulong)target));
    }

    /// <summary>
    /// The level read's stub, entered by a jump from the function body (16-aligned) with rbx the
    /// record and rdi the unit: r10b = <see cref="Level"/>, then the game's own three-way choice.
    /// </summary>
    internal static void WriteLevelStub(byte* stub, nint scales, nint fixedLevel, nint high)
    {
        var code = new List<byte>
        {
            0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, // push rax, rcx, rdx, r8–r11
            0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00,                         // sub rsp, 0x88
        };
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x48, 0x89, 0xD9, 0x48, 0x89, 0xFA, 0x48, 0xB8]);       // mov rcx, rbx; mov rdx, rdi; mov rax, Level
        code.AddRange(BitConverter.GetBytes((ulong)(nint)(delegate* unmanaged<byte*, byte*, int>)&Level));
        code.AddRange([0xFF, 0xD0]);                                           // call rax
        code.AddRange([0x88, 0x84, 0x24, 0x90, 0x00, 0x00, 0x00]);             // mov [rsp+0x90], al: the saved r10
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]);            // add rsp, 0x88
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58]); // pop r11 … rax (r10b the level)
        code.AddRange([0x41, 0x8D, 0x42, 0x02, 0xA8, 0xFD, 0x75, 0x0E]);       // lea eax, [r10+2]; test al, 0xFD; jne
        AbsoluteJump(code, scales);                                            // 0 or 0xFE
        code.AddRange([0x44, 0x3A, 0xD6, 0x77, 0x0E]);                         // cmp r10b, sil (99); ja
        AbsoluteJump(code, fixedLevel);
        AbsoluteJump(code, high);
        if (code.Count > StubSize)
            throw new InvalidOperationException("the level stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }

    /// <summary>
    /// P's stub, entered by a jump with al the roster's level: al = max(al, *floor), then the game's
    /// mov ecx, 0x39; mov [P], al; call; and back.
    /// </summary>
    internal static void WritePartyStub(byte* stub, byte* floor, byte* partyLevel, nint callee, nint back,
                                        nint tasks = 0, nint* snapshot = null)
    {
        var code = new List<byte>();
        if (tasks != 0 && snapshot != null)
        {
            // Task 6's function and state (the table's +0x3088, +0x3090; a finished task keeps its function, its state
            // goes to 0) into the snapshot, and how often P was set.
            code.AddRange([0x50, 0x52, 0x48, 0xB8]);                            // push rax; push rdx; mov rax, tasks
            code.AddRange(BitConverter.GetBytes((ulong)tasks));
            code.AddRange([0x48, 0x8B, 0x00, 0x48, 0x85, 0xC0, 0x74, 0x00]);   // mov rax, [rax]; test rax, rax; jz skip
            var jz = code.Count;
            code.AddRange([0x48, 0xB9]);                                        // mov rcx, snapshot
            code.AddRange(BitConverter.GetBytes((ulong)snapshot));
            code.AddRange([0x48, 0x8B, 0x90, 0x88, 0x30, 0x00, 0x00]);         // mov rdx, [rax+0x3088]: the function
            code.AddRange([0x48, 0x89, 0x11]);                                  // mov [rcx], rdx
            code.AddRange([0x48, 0x8B, 0x90, 0x90, 0x30, 0x00, 0x00]);         // mov rdx, [rax+0x3090]: the state
            code.AddRange([0x48, 0x89, 0x51, 0x08]);                            // mov [rcx+8], rdx
            code.AddRange([0x48, 0xFF, 0x41, 0x10]);                            // inc qword [rcx+16]
            code[jz - 1] = checked((byte)(code.Count - jz));
            code.AddRange([0x5A, 0x58]);                                        // skip: pop rdx; pop rax
        }
        code.AddRange([0x48, 0xB9]);                                           // mov rcx, floor
        code.AddRange(BitConverter.GetBytes((ulong)floor));
        code.AddRange([0x3A, 0x01, 0x73, 0x02, 0x8A, 0x01]);                   // cmp al, [rcx]; jae +2; mov al, [rcx]
        code.AddRange([0xB9, 0x39, 0x00, 0x00, 0x00, 0xA2]);                   // mov ecx, 0x39; mov [P], al
        code.AddRange(BitConverter.GetBytes((ulong)partyLevel));
        code.AddRange([0x48, 0xB8]);                                           // mov rax, callee; call rax
        code.AddRange(BitConverter.GetBytes((ulong)callee));
        code.AddRange([0xFF, 0xD0]);
        AbsoluteJump(code, back);
        if (code.Count > StubSize)
            throw new InvalidOperationException("the party level stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }

    /// <summary>
    /// The roster loop's stub (rax the slot's +1, r8 its level at +0x1D, r14b 0xFF): an empty slot or a unit away
    /// (+0x11E &amp; 0xF, so [r8 + 0x101]) goes to <paramref name="next"/>; otherwise ecx = its level and on to
    /// <paramref name="back"/>.
    /// </summary>
    internal static void WriteRosterStub(byte* stub, nint back, nint next)
    {
        var code = new List<byte> { 0x44, 0x38, 0x30, 0x74, 0x1C };                     // cmp [rax], r14b; je next
        code.AddRange([0x41, 0xF6, 0x80]);                                                  // test byte [r8 + errand − level], 0xF
        code.AddRange(BitConverter.GetBytes(Errand - RosterLevel));
        code.AddRange([0x0F, 0x75, 0x12]);                                                  // jnz next
        code.AddRange([0x41, 0x0F, 0xB6, 0x08]);                                            // movzx ecx, byte [r8]
        AbsoluteJump(code, back);
        AbsoluteJump(code, next);                                                           // next
        if (code.Count > RosterStubSize)
            throw new InvalidOperationException("the roster loop's stub is too big");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }

    /// <summary>
    /// Improved Level Scaling's default (docs/MECHANICS.md): the party level P comes from the deployed units. The
    /// remaster builds the enemies while the formation screen is still up (seen on the Deck), with P from the roster,
    /// so at the first countdown of a battle (from <see cref="TwoSpells.Tick"/>, after the formation) each enemy that
    /// scaled with P gets a level from the deployed units' highest instead, and the unit build's stat steps again
    /// (<see cref="Rebuild"/>); its place, equipment and the rest stay. ReMixed re-runs the enemies' whole setup.
    /// With the test mode on it runs every battle (the levels roll again even when the two agree), so it can be seen
    /// working on any save; otherwise only when the deployed units' highest level is under the roster's.
    /// </summary>
    /// <summary>
    /// ReMixed's re-run (0x80151000) called from a ReMixed event: it runs unless var 0x70 is 0xFEFE, which ReMixed's
    /// events set to keep a battle's levels as built. The port's re-run is <see cref="AfterFormation"/>, which every
    /// battle gets; an event's 0xFEFE turns it off for that battle.
    /// </summary>
    internal static void FromEvent(int var70, Action<string> note)
    {
        if (var70 == 0xFEFE)
        {
            _declined = true;
            note("0x80151000 (Improved Level Scaling's re-run): turned off for this battle (var 0x70 is 0xFEFE)");
        }
        else
            note("0x80151000 (Improved Level Scaling's re-run): left to the level scaling at the first countdown");
    }

    internal static void AfterFormation(byte* units)
    {
        if (_deployedChecked)
            return;
        _deployedChecked = true;
        if (_declined)
        {
            _declined = false;
            Tracer.Line(_game.Log, "level scaling: this battle's ReMixed event turned the re-run off (var 0x70 is 0xFEFE)");
            return;
        }
        if (!FromFormation(_formation))
        {
            // Rebuilt, a resumed battle's enemies had their HP and MP full again (seen on the Deck, 2026-11-07).
            Tracer.Line(_game.Log, "level scaling: the battle didn't come through the formation screen (resumed from a "
                                   + "save): its units stay as saved");
            return;
        }
        var test = Options.Current.TestMode != Options.TestModeRule.Off;
        if (Options.Current.LevelScaling == Options.LevelScalingRule.EntireRoster && !test)
            return;
        var deployed = 0;
        var count = 0;
        for (var k = 0; k < Battle.UnitCount; k++)
        {
            var unit = units + k * Battle.UnitSize;
            if (unit[1] == 0xFF || unit[2] == 0xFF || (unit[Battle.UnitTeam] & 0x30) != 0)
                continue;
            count++;
            deployed = Math.Max(deployed, (int)unit[Battle.UnitLv]);
        }
        (int Index, byte Level)[] scaled;
        lock (Scaled)
            scaled = [.. Scaled];
        int roster = *_partyLevel, party = Math.Max(Math.Min(deployed, MaxLevel), (int)*_floor);
        if (count == 0 || scaled.Length == 0 || party >= roster && !test)
        {
            Tracer.Line(_game.Log, $"level scaling: the {count} deployed units' highest level is {deployed}, the roster's P "
                                   + $"{roster}; {scaled.Length} units scaled; nothing to redo");
            return;
        }
        var changes = new List<string>();
        foreach (var (index, level) in scaled)
        {
            var unit = units + index * Battle.UnitSize;
            if (unit[1] == 0xFF || (unit[Battle.UnitTeam] & 0x30) == 0)
                continue;
            var before = unit[Battle.UnitLv];
            var after = LevelFor(level, party, Random.Shared.Next);
            Rebuild(unit, after);
            if (_levelSum != null)
            {
                *_levelSum = (ushort)Math.Max(0, *_levelSum + after - before);
                if (after > *_levelMax)
                    *_levelMax = (byte)after;
            }
            changes.Add($"unit {index} {before} → {after}");
        }
        *_partyLevel = (byte)party;
        Tracer.Line(_game.Log, $"level scaling: P {roster} (the roster's) → {party} (the {count} deployed units'"
                               + $"{(test ? ", the test mode redoes it every battle" : "")}): {string.Join(", ", changes)}");
    }

    /// <summary>Whether the battle came through the formation screen, by the snapshot the party level's stub takes as P
    /// is set (task 6's function, its state, how often P was set): a new battle sets P while the formation screen (task
    /// 6) is up (seen on the Deck); a battle resumed from a save is set up again without it, and its units are then the
    /// saved ones. With P never set it can't tell, and says yes.</summary>
    internal static bool FromFormation(nint* snapshot) => snapshot == null || snapshot[2] == 0 || snapshot[1] != 0;

    /// <summary>A resolved level with the party level <paramref name="party"/>, as the unit build has it (0x2763FF): 0 or
    /// 0xFE from P − P/8 to P, 100 and up P plus the excess, capped at 99.</summary>
    internal static int LevelFor(int level, int party, Func<int, int> roll)
    {
        var result = level is 0 or Scaling ? party - (party >> 3) + roll((party >> 3) + 1)
            : level >= 100 ? party + level - 100 : level;
        return Math.Clamp(result, 1, MaxLevel);
    }

    /// <summary>The unit at <paramref name="level"/>: its level-1 base stats rolled again, levelled up and recalculated
    /// as the unit build does (0x275D68 on), then its HP and MP full. Its growths, equipment and the rest stay.</summary>
    internal static bool CanRebuild => _baseStats != null && _levelUps != null && Recalculate != null;

    internal static void Rebuild(byte* unit, int level)
    {
        unit[Battle.UnitLv] = (byte)level;
        var gender = unit[6];
        var kind = (gender & 0x80) != 0 ? 0 : (((~gender) & 0x40) | 0x20) >> 5;
        var stats = stackalloc byte*[5];
        for (var k = 0; k < 5; k++)
            stats[k] = unit + RawStats + 3 * k;
        _baseStats(stats, kind);
        var marker = unit[2];
        if (marker == 0xFF)
            unit[2] = 0xFE; // a generated unit, as the build marks it for the level-ups
        _levelUps(unit, 0, 0);
        unit[2] = marker;
        Recalculate(unit, 0);
        *(ushort*)(unit + Battle.UnitHp) = *(ushort*)(unit + Battle.UnitMaxHp);
        *(ushort*)(unit + Battle.UnitMp) = *(ushort*)(unit + Battle.UnitMaxMp);
    }

    [UnmanagedCallersOnly]
    private static int Level(byte* record, byte* unit)
    {
        try
        {
            var level = OptionsSkipped() ? record[3] : Resolve(record[3], unit[Battle.UnitCurrentTeam], *_partyLevel, Options.Current);
            if (level is 0 or Scaling or >= 100 && (unit[Battle.UnitCurrentTeam] & 0x30) != 0)
                lock (Scaled)
                    Scaled.Add(((int)((unit - _game.TurnCode.Units) / Battle.UnitSize), (byte)level));
            if (level != record[3])
                Tracer.Line(_game.Log, $"level options: a level of {record[3]} is {(level == Scaling ? "the party's" : level.ToString())}");
            return level;
        }
        catch (Exception e)
        {
            Failed("the level", e);
            return record[3];
        }
    }

    /// <summary>
    /// The battle entries whose units ignore the level options. Three of ReMixed's story events set their units up with
    /// them off (0x002, 0x032, 0x047: var 0x14's byte 3, the Enemy Level Boost and Enemy Job Settings, and flags 0xE4 and
    /// 0xDC cleared meanwhile, opCF 00 10 15 80, then put back). The remaster plays its own scenes there; of their battles
    /// only 0x002's has a match in it, the opening battle at Orbonne (ReMixed's entry 0x101, the remaster's 0x183). 0x032's
    /// and 0x047's (entries 0x134 and 0x149) are battles of ReMixed's own in scenes the remaster plays without one.
    /// </summary>
    internal static readonly HashSet<int> PlainEntries = [0x183];

    /// <summary>Whether the battle being set up (var 0x32, its entry) is one of <see cref="PlainEntries"/>.</summary>
    internal static bool OptionsSkipped() => PlainEntries.Contains(BattleEntry());

    /// <summary>The battle being set up's entry (var 0x32). The offline check gives its own: the game's variables aren't
    /// there.</summary>
    internal static Func<int> BattleEntry = () => RemixedEvents.Word(0x32);

    /// <summary>The level byte the game resolves, after ReMixed's options, for a unit of <paramref name="team"/> (+5).</summary>
    internal static int Resolve(int level, int team, int party, Options options)
    {
        if ((team & 0x30) == 0)
            return level;
        if (options.FixedLevels == Options.FixedLevelRule.IfPartyLevelIsHigher && level is > 0 and < 100 && level < party - (party >> 3))
            level = Scaling;
        if (options.EnemyLevelBoost != Options.LevelBoostRule.Normal && level is > 0 and < 100)
            level = Math.Min(level + BoostPerMode * (int)options.EnemyLevelBoost, MaxLevel);
        return level;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"level options: {what} failed: {e}");
    }
}
