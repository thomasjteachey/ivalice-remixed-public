using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// The remaster's lookup of a character's battle unit for the unit cards an event shows, made to find the units the
/// port moved and to pass over one it can't find. Event instruction 0xC3 (0x214284: a u16 character id) asks for the
/// unit by id (0x226A18), which answers 2000 for one that isn't there to show — a crystal (+0x61 &amp; 0x40), a chest
/// (+0x62 &amp; 1), one with no sprite — and then, only in event 0x119 for character 0x19 (the end of the battle at
/// Riovanes Castle's gate, event 281, whose guest Rafa leaves the party), looks over the 21 battle units for the one
/// whose sprite set is 0x19 and takes that. With no unit found it still hands the status panel (0x2CBCA0, its record
/// 0x360048) the unit, null: the game closes at exe+0x360086 reading 0x29.
/// <list type="bullet">
/// <item>The fallback's sprite set is one byte, the immediate of "cmp byte ptr [rax], 0x19" at 0x2142F1. The port has
/// the remaster's Rafa on sprite set 0x29 (tools/build_mod.py REMASTER_MOVED_SPRITE_SETS), so a Rafa who had become a
/// crystal or a chest by the battle's end was never found (a tester on 0.9.38, three times on one save, 2026-11-25).
/// The rule writes her sprite set there; the event and the character id it tests stay the remaster's.</item>
/// <item>The fallback also names the face for the cards that follow, in its own code: "mov dword ptr [0x2FCF86C],
/// 0x19" at 0x21430C, a graphic the faces' fold (0x0F8338) turns into a face. The panel puts its own number there
/// unless it has none (−1, its record's +0x9C from 0x214134: a unit whose sprite is gone, as a body that has turned
/// to a crystal), and the departure card (0x214404) takes the number when its own panel has none. In the port
/// graphic 0x19 is ReMixed's Balthier and Rafa's is 0x29, which the fold sends to her face: a Rafa who left as a
/// crystal had Balthier's face on her "Guest Departure" card (a tester on 0.9.39, 2026-11-25). The rule writes her
/// sprite set there too.</item>
/// <item>The panel's call (0x2143B0) goes through a stub that returns at once for a null unit, so a unit the lookup
/// can't find at all (no guest in the battle, a record an earlier build left) is passed over: the instruction's
/// other work is skipped by the game itself for a null unit, and the event goes on to its screens.</item>
/// </list>
/// Data: [[the character id, its old sprite set, its new sprite set], ...] (one row: the remaster has one such lookup).
/// Installed by <see cref="Rules"/> with "guest-lookup".
/// </summary>
internal static unsafe class GuestLookup
{
    // cmp dword ptr [the running event], 0x119; jne; cmp esi, 0x19; jne; mov ecx, ebp; lea rdx, [the battle units];
    // movsxd rax, ecx; shl rax, 9; add rax, rdx; cmp byte ptr [rax], 0x19; je; inc ecx; cmp ecx, 0x15; jl; jmp;
    // movzx edi, byte ptr [rax+1]; mov rbx, rax; mov [rax+1], cl; mov dword ptr [the cards' face], 0x19.
    internal const string Pattern =
        "81 3D ?? ?? ?? ?? 19 01 00 00 0F 85 ?? ?? ?? ?? 83 FE 19 0F 85 ?? ?? ?? ?? 8B CD 48 8D 15 ?? ?? ?? ?? " +
        "48 63 C1 48 C1 E0 09 48 03 C2 80 38 19 74 0C FF C1 83 F9 15 7C EA E9 ?? ?? ?? ?? 0F B6 78 01 48 8B D8 " +
        "88 48 01 C7 05 ?? ?? ?? ?? 19 00 00 00";
    internal const int CharacterAt = 18, SpriteSetAt = 46, FaceAt = 77;
    // mov rdx, rbx; lea rcx, [rsp+0x20]; call the panel; cmp [rsp+0xBC], r14d.
    internal const string PanelCallPattern = "48 8B D3 48 8D 4C 24 20 E8 ?? ?? ?? ?? 44 39 B4 24 BC 00 00 00";
    internal const int PanelCallAt = 8;

    /// <summary>Where the fallback's sprite set is (the immediate's address), for the offline check; 0 until installed.</summary>
    internal static nint SpriteSetByte { get; private set; }

    /// <summary>The sprite set written, for the offline check.</summary>
    internal static byte SpriteSet { get; private set; }

    /// <summary>Where the fallback's face for the cards is (the immediate's address, a dword), for the offline check.</summary>
    internal static nint FaceNumber { get; private set; }

    /// <summary>The panel's call in the lookup, the panel's own start and the stub the call goes to now.</summary>
    internal static nint PanelCall { get; private set; }
    internal static nint Panel { get; private set; }
    internal static nint NullStub { get; private set; }

    public static string Install(Game game, JsonElement rows)
    {
        if (rows.GetArrayLength() != 1)
            throw new InvalidOperationException($"{rows.GetArrayLength()} rows for the remaster's one lookup by sprite set");
        var row = rows[0];
        byte character = row[0].GetByte(), old = row[1].GetByte(), moved = row[2].GetByte();
        var at = game.Image.FindUnique(Pattern);
        if (*(byte*)(at + CharacterAt) != character || *(byte*)(at + SpriteSetAt) != old || *(int*)(at + FaceAt) != old)
            throw new InvalidOperationException(
                $"the lookup is for character 0x{*(byte*)(at + CharacterAt):X2} by sprite set " +
                $"0x{*(byte*)(at + SpriteSetAt):X2} with the face 0x{*(int*)(at + FaceAt):X}, not 0x{character:X2} by 0x{old:X2}");
        var call = game.Image.FindUnique(PanelCallPattern) + PanelCallAt;
        if (call < at || call > at + 0x200)
            throw new InvalidOperationException("the panel's call isn't the lookup's");
        var panel = call + 5 + *(int*)(call + 1);
        var stub = (nint)GameImage.AllocateCodeNear(game.Image.ImageStart, 16);
        var code = NullUnitStub(stub, panel);
        code.CopyTo(new Span<byte>((void*)stub, code.Length));
        var rel = (long)stub - (call + 5);
        if (rel is < int.MinValue or > int.MaxValue)
            throw new InvalidOperationException("the stub is out of the call's reach");
        var patch = new byte[5];
        patch[0] = 0xE8;
        BitConverter.TryWriteBytes(patch.AsSpan(1), (int)rel);
        GameImage.WriteCode(call, patch);
        GameImage.WriteCode(at + SpriteSetAt, [moved]);
        GameImage.WriteCode(at + FaceAt, [moved, 0, 0, 0]);
        FaceNumber = at + FaceAt;
        SpriteSetByte = at + SpriteSetAt;
        SpriteSet = moved;
        PanelCall = call;
        Panel = panel;
        NullStub = stub;
        return $"character 0x{character:X2}'s battle unit found by sprite set 0x{moved:X2} (the remaster's 0x{old:X2}) " +
               "when it's a crystal or a chest at the Riovanes gate's end, with that graphic's face on its cards, " +
               "and a unit that isn't found passed over";
    }

    /// <summary>test rdx, rdx; jz the ret; jmp <paramref name="panel"/>; ret: the panel for a unit, nothing for none.</summary>
    internal static byte[] NullUnitStub(nint stub, nint panel)
    {
        var rel = (long)panel - (stub + 10);
        if (rel is < int.MinValue or > int.MaxValue)
            throw new InvalidOperationException("the panel is out of the stub's reach");
        var code = new byte[11];
        code[0] = 0x48; code[1] = 0x85; code[2] = 0xD2; // test rdx, rdx
        code[3] = 0x74; code[4] = 0x05;                 // jz +5
        code[5] = 0xE9;                                 // jmp panel
        BitConverter.TryWriteBytes(code.AsSpan(6), (int)rel);
        code[10] = 0xC3;                                // ret
        return code;
    }
}
