using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// The remaster's formation filters 0x64 and 0x67 omit the guest roster slots. ReMixed lets the player deploy an
/// accompanying character when the encounter's ENTD names them as a friendly actor whose placement is optional.
/// Permit those records through the guest check. The stock errand, availability and ordinary roster checks still run.
/// </summary>
internal static unsafe class GuestDeployment
{
    private const string FilterPattern = "66 83 7C 33 2C 32 73 41 66 45 85 ED 74 29 41 83 FE 0B";
    private const int FilterBytes = 8, ScenarioAt = OldGuests.ScenarioVar * 4;
    internal const int StubSize = 96;
    internal static nint At { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Formation
    {
        public int Scenario;
        public uint Slots;
    }

    private static Formation* _prepared;

    public static void Install(Game game)
    {
        var at = game.Image.FindUnique(FilterPattern);
        var accepted = at + FilterBytes;
        var rejected = accepted + *(sbyte*)(at + FilterBytes - 1);
        var variables = EventVariables.Words(EventVariables.Find(game.Image).Set);
        var prepared = (Formation*)NativeMemory.AllocZeroed((nuint)sizeof(Formation));
        var stub = GameImage.AllocateCodeNear(at, StubSize);
        WriteStub(stub, variables, prepared, accepted, rejected);
        var patch = new byte[FilterBytes];
        Array.Fill(patch, (byte)0x90);
        patch[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(patch, 1);
        GameImage.WriteCode(at, patch);
        _prepared = prepared;
        At = at;
    }

    internal static void Prepare(int scenario, byte* entry, byte* roster)
    {
        if (_prepared == null)
            return;
        var formation = RemixedScenarios.Presets.ContainsKey(scenario) ? RemixedScenarios.FindRecord(scenario) : null;
        *_prepared = new Formation
        {
            Scenario = scenario,
            Slots = formation != null && *(ushort*)(formation + 9) != 0 ? EligibleSlots(entry, roster) : 0,
        };
    }

    internal static uint EligibleSlots(byte* entry, byte* roster)
    {
        if (entry == null || roster == null)
            return 0;
        uint eligible = 0;
        for (var k = 0; k < OldGuests.GuestSlots; k++)
        {
            var guest = roster + (OldGuests.FirstGuestSlot + k) * UnitTables.RosterSize;
            var name = *(int*)(guest + OldGuests.RosterName);
            if (guest[1] == 0xFF || guest[OldGuests.RosterErrand] != 0 || name is <= 0 or >= 0xFF)
                continue;
            var optional = false;
            var scripted = false;
            for (var i = 0; i < OldGuests.EntryUnits; i++)
            {
                var actor = entry + i * OldGuests.EntryUnitSize;
                var presence = actor[OldGuests.EntryTeam];
                if (actor[0] == 0 || actor[OldGuests.EntryName] != name || (presence & 0x30) != 0)
                    continue;
                // An actor set up by the battle must keep its own placement. Optional actors give the scene a stand-in
                // for a character the player can place from the roster, even when its costume (Elias's) differs.
                scripted |= (presence & 0xC0) != 0;
                optional |= (presence & 0xC0) == 0;
            }
            if (optional && !scripted)
                eligible |= 1u << k;
        }
        return eligible;
    }

    /// <summary>
    /// At 0x2851DC, rbx+rsi is the candidate. Its +0x2C marks a guest and +0xA is the actual roster id.
    /// Only the guest path consults the prepared encounter's mask. Different scenarios cannot inherit it.
    /// </summary>
    internal static void WriteStub(byte* stub, int** variables, Formation* prepared, nint accepted, nint rejected)
    {
        var code = new List<byte>
        {
            0x66, 0x83, 0x7C, 0x33, 0x2C, OldGuests.FirstGuestSlot, // cmp word [rbx+rsi+0x2C], 0x32
            0x72, 0,                                               // jb accept
            0x48, 0xB8,                                            // mov rax, variables
        };
        var rejectOffsets = new List<int>();
        void RejectIf(byte opcode)
        {
            code.AddRange([opcode, 0]);
            rejectOffsets.Add(code.Count - 1);
        }
        code.AddRange(BitConverter.GetBytes((long)variables));
        code.AddRange([0x48, 0x8B, 0x00, 0x48, 0x85, 0xC0]);      // mov rax, [rax]; test rax, rax
        RejectIf(0x74);                                            // jz
        code.AddRange([0x48, 0xBA, .. BitConverter.GetBytes((long)prepared)]); // mov rdx, prepared
        code.AddRange([0x8B, 0x80, .. BitConverter.GetBytes(ScenarioAt)]); // mov eax, [rax+scenario var]
        code.AddRange([0x3B, 0x02]);                               // cmp eax, [rdx+scenario]
        RejectIf(0x75);                                            // jne
        code.AddRange([0x0F, 0xB7, 0x44, 0x33, 0x0A]);            // movzx eax, word [rbx+rsi+0xA]
        code.AddRange([0x83, 0xE8, OldGuests.FirstGuestSlot, 0x83, 0xF8, OldGuests.GuestSlots]);
        RejectIf(0x73);                                            // jae (also excludes a slot below the guest range)
        code.AddRange([0x0F, 0xA3, 0x42, 0x04]);                   // bt [rdx+slots], eax
        RejectIf(0x73);                                            // jnc
        code[7] = checked((byte)(code.Count - 8));
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0, .. BitConverter.GetBytes((long)accepted)]);
        foreach (var offset in rejectOffsets)
            code[offset] = checked((byte)(code.Count - offset - 1));
        code.AddRange([0xFF, 0x25, 0, 0, 0, 0, .. BitConverter.GetBytes((long)rejected)]);
        if (code.Count > StubSize)
            throw new InvalidOperationException("the guest roster filter stub is too large");
        code.ToArray().CopyTo(new Span<byte>(stub, code.Count));
    }
}
