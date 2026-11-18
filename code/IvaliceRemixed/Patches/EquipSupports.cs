using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's equipment permissions from supports (docs/MECHANICS.md: "Equip supports"; PS1 SCUS
/// 0x8005C27C, rewritten in place). Installed by <see cref="Rules"/> with "equip-supports". Data: 32
/// equip masks, one per support bit, from ReMixed's table at 0x8005C318.
/// </summary>
/// <remarks>
/// Vanilla's routine (the remaster's 0x27788C, rcx the unit, from five unit builders) adds fixed item
/// types for the eight Equip supports. ReMixed's ORs a mask from its table for every support bit set,
/// so its Equip Crossbow also allows bows, 0x1C9 (vanilla's Equip Katana, "Equip Knife" in the US
/// release) allows poles, Two Hands allows katanas, and the new support 0x1E1 allows every weapon,
/// bags, cloths and shields. A secondary skillset of 0x26 adds bit 0's mask (guns), and 0xFC bit 21's.
/// Two of the callers pass rcx on to the next call, so the replacement is a stub that keeps it, and
/// like the original it only uses rax and rdx. ReMixed also gives women Bag, Hair Adornment and Perfume
/// here, as vanilla does; the remaster's routine doesn't, so that's left to however the remaster does it.
/// </remarks>
internal static unsafe class EquipSupports
{
    private const string Routine =
        "8A 81 98 00 00 00 84 C0 79 04 80 49 54 09 B2 10 A8 40 74 03 08 51 54 A8 20 74 03 08 51 52 84 C2 74 04 " +
        "80 49 52 04 A8 08 74 03 08 51 53 A8 04 74 04 80 49 53 01 A8 02 74 04 80 49 52 02 A8 01 74 04 80 49 53 20 C3";
    internal const int Supports = 32, StubSize = 256;
    private const byte SnipeSkillset = 0x26, OtherSkillset = 0xFC; // they add bit 0's and bit 21's masks
    private const int OtherBit = 21;

    // For the offline check.
    internal static nint RoutineAt { get; private set; }

    /// <summary>Replaces the routine. Returns how many supports grant something.</summary>
    public static int Install(Game game, JsonElement data)
    {
        var masks = data.EnumerateArray().Select(e => e.GetUInt32()).ToArray();
        if (masks.Length != Supports)
            throw new InvalidDataException($"ReMixed's equip table should have {Supports} masks, not {masks.Length}");
        var at = game.Image.FindUnique(Routine);
        var stub = GameImage.AllocateCodeNear(at, StubSize);
        WriteStub(stub, masks);
        byte[] jump = [0xE9, 0, 0, 0, 0];
        BitConverter.GetBytes(checked((int)((nint)stub - (at + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(at, jump);
        RoutineAt = at;
        return masks.Count(m => m != 0);
    }

    /// <summary>
    /// The routine, rcx the unit: its equip mask (+0x52) gets the mask of every support bit set in its
    /// four support bytes (+0x98, low bit of the first byte first), and one more for the two secondary
    /// skillsets. Keeps every register but rax and rdx; the table follows the code.
    /// </summary>
    internal static void WriteStub(byte* stub, uint[] masks)
    {
        var code = new List<byte> { 0x53, 0x56 };                         // push rbx; push rsi
        code.AddRange([0x8B, 0x81, (byte)Battle.UnitSupports, 0x00, 0x00, 0x00]); // mov eax, [rcx + supports]
        code.AddRange([0x8B, 0x51, (byte)Battle.UnitEquipTypes]);          // mov edx, [rcx + equip mask]
        code.AddRange([0x48, 0xBE]);                                      // mov rsi, the table
        var tableAt = code.Count;
        code.AddRange(new byte[8]);
        code.AddRange([0x31, 0xDB]);                                      // xor ebx, ebx
        code.AddRange([0x0F, 0xA3, 0xD8]);                                // loop: bt eax, ebx
        code.AddRange([0x73, 0x03]);                                      // jnc next
        code.AddRange([0x0B, 0x14, 0x9E]);                                // or edx, [rsi + rbx*4]
        code.AddRange([0xFF, 0xC3]);                                      // next: inc ebx
        code.AddRange([0x83, 0xFB, Supports]);                            // cmp ebx, 32
        code.AddRange([0x72, 0xF1]);                                      // jb loop
        code.AddRange([0x0F, 0xB6, 0x41, (byte)Battle.UnitSecondary]);   // movzx eax, byte [rcx + secondary]
        code.AddRange([0x3C, SnipeSkillset, 0x75, 0x02, 0x0B, 0x16]);    // cmp al, 0x26; jne; or edx, [rsi]
        code.AddRange([0x3C, OtherSkillset, 0x75, 0x03, 0x0B, 0x56, 4 * OtherBit]); // cmp al, 0xFC; jne; or edx, [rsi + 84]
        code.AddRange([0x89, 0x51, (byte)Battle.UnitEquipTypes]);          // mov [rcx + equip mask], edx
        code.AddRange([0x5E, 0x5B, 0xC3]);                                // pop rsi; pop rbx; ret
        while (code.Count % 8 != 0)
            code.Add(0xCC);
        var table = code.Count;
        foreach (var m in masks)
            code.AddRange(BitConverter.GetBytes(m));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the equip stub outgrew its space");
        var bytes = code.ToArray();
        BitConverter.GetBytes((long)(stub + table)).CopyTo(bytes, tableAt);
        bytes.CopyTo(new Span<byte>(stub, StubSize));
    }
}
