using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's random weapon pick (docs/MECHANICS.md: "Random battle equipment"; PS1 SCUS 0x8005BEA4,
/// rewritten in place). Installed by <see cref="Rules"/> with "weapon-pick".
/// </summary>
/// <remarks>
/// A unit whose record asks for a random weapon (0xFE) gets one from the remaster's 0x277564: Two
/// Hands' and Two Swords' tries, then, from 0x277616, its Equip supports' types in vanilla's order
/// (Sword, Katana, Crossbow, Spear, Axe, Gun) through the game's item pick (0x277F10), then any type,
/// else none. ReMixed lists the types its own equip table gives (<see cref="EquipSupports"/>: Pole for
/// 0x1C9, Crossbow and Bow, Katana for Two Hands) and tries them from the last back; 0x1E1 goes straight
/// to any type, and Martial Arts with nothing listed means no weapon. The block jumps to a stub and
/// <see cref="Pick"/>.
/// </remarks>
internal static unsafe class WeaponPick
{
    private static Game _game = null!;
    private static delegate* unmanaged<byte*, byte, int, byte, byte> _pick; // the game's item pick
    private static int _failed;

    private const byte Sword = 3, Katana = 5, Axe = 6, Gun = 0xA, Crossbow = 0xB, Bow = 0xC, Spear = 0xF, Pole = 0x10;
    private const byte Weapon = 0x80; // the pick's category for a weapon
    internal const int StubSize = 192;

    // For the offline check.
    internal static nint SiteAt { get; private set; }
    internal static nint BackAt { get; private set; }
    internal static nint ChooseAt => (nint)(delegate* unmanaged<byte*, int, int, int>)&Choose;
    internal static delegate* unmanaged<byte*, byte, int, byte, byte> PickFunction
    {
        get => _pick;
        set => _pick = value;
    }

    public static void Install(Game game)
    {
        _game = game;
        var image = game.Image;
        // After the Two Hands and Two Swords tries: the Equip supports (+0x98), Sword (3) then Katana (5) first.
        var at = image.FindUnique(
            "40 8A B3 98 00 00 00 41 3A CF 0F 85 ?? ?? ?? ?? 40 F6 C6 20 74 1B 41 B1 03 45 33 C0 B2 80 48 8B CB " +
            "E8 ?? ?? ?? ?? 8A C8 41 3A C7 0F 85 ?? ?? ?? ?? 40 F6 C6 10 74 1B 41 B1 05 45 33 C0 B2 80 48 8B CB E8");
        // The last try, any type (dil, 0xFF), then the store from 0x2776E5.
        var any = GameImage.Calls(at + 0xC6, "44 8A CF 45 33 C0 B2 80 48 8B CB E8 ?? ?? ?? ?? 41 3A C7 0F B6 C8 0F 44 CF",
                                  "the weapon pick's last try");
        var pick = GameImage.Rip(at + 0x21, 1, 5);
        var back = GameImage.Rip(at + 0xA, 2, 6);
        if (any[0] != pick || back != at + 0xDF || GameImage.Rip(at + 0x2B, 2, 6) != back)
            throw new InvalidDataException("the weapon pick isn't the code this DLL expects");
        _pick = (delegate* unmanaged<byte*, byte, int, byte, byte>)pick;
        SiteAt = at + 0x10;
        BackAt = back;
        var stub = GameImage.AllocateCodeNear(SiteAt, StubSize);
        WriteStub(stub, ChooseAt, back);
        byte[] jump = [0xE9, 0, 0, 0, 0, 0x90];
        BitConverter.GetBytes(checked((int)((nint)stub - (SiteAt + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(SiteAt, jump);
    }

    /// <summary>
    /// The stub, entered by a jump with the unit in rbx, "none" in dil and "not found" in r15b: calls
    /// <paramref name="managed"/>(unit, none, not found) keeping the scratch registers, then goes on to
    /// <paramref name="back"/> with the item in cl.
    /// </summary>
    internal static void WriteStub(byte* stub, nint managed, nint back)
    {
        var code = new List<byte>
        {
            0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, // push rdx, r8–r11
            0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00,              // sub rsp, 0x88
        };
        Stub.SaveXmm(code, 0x20);
        code.AddRange([0x48, 0x89, 0xD9]);                     // mov rcx, rbx: the unit
        code.AddRange([0x40, 0x0F, 0xB6, 0xD7]);               // movzx edx, dil: none
        code.AddRange([0x45, 0x0F, 0xB6, 0xC7]);               // movzx r8d, r15b: not found
        code.AddRange([0x48, 0xB8]);                           // mov rax, managed
        code.AddRange(BitConverter.GetBytes((long)managed));
        code.AddRange([0xFF, 0xD0]);                           // call rax
        Stub.RestoreXmm(code, 0x20);
        code.AddRange([0x48, 0x81, 0xC4, 0x88, 0x00, 0x00, 0x00]); // add rsp, 0x88
        code.AddRange([0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A]); // pop r11–r8, rdx
        code.AddRange([0x0F, 0xB6, 0xC8]);                     // movzx ecx, al: the item
        code.AddRange([0xFF, 0x25, 0x00, 0x00, 0x00, 0x00]);   // jmp [rip]: back
        code.AddRange(BitConverter.GetBytes((long)back));
        if (code.Count > StubSize)
            throw new InvalidOperationException("the weapon pick stub outgrew its space");
        code.ToArray().CopyTo(new Span<byte>(stub, StubSize));
    }

    [UnmanagedCallersOnly]
    private static int Choose(byte* unit, int none, int notFound)
    {
        try
        {
            return Pick(unit, (byte)none, (byte)notFound);
        }
        catch (Exception e)
        {
            Failed("the weapon pick", e);
            return none;
        }
    }

    /// <summary>ReMixed's pick: its types from the last listed back, then any type, else none.</summary>
    internal static byte Pick(byte* unit, byte none, byte notFound)
    {
        var equip = unit[Battle.UnitSupports];      // Equip Armor … Equip Gun
        var third = unit[Battle.UnitSupports + 2];  // 0x02 Two Hands, 0x20 Martial Arts
        var fourth = unit[Battle.UnitSupports + 3]; // 0x10 the support 0x1E1
        Span<byte> types = stackalloc byte[8];
        var n = 0;
        if ((fourth & 0x10) == 0)
        {
            if ((equip & 0x20) != 0) types[n++] = Sword;
            if ((equip & 0x10) != 0) types[n++] = Pole;
            if ((equip & 0x08) != 0)
            {
                types[n++] = Crossbow;
                types[n++] = Bow;
            }
            if ((equip & 0x04) != 0) types[n++] = Spear;
            if ((equip & 0x02) != 0) types[n++] = Axe;
            if ((equip & 0x01) != 0) types[n++] = Gun;
            if ((third & 0x02) != 0) types[n++] = Katana;
            if (n == 0 && (third & 0x20) != 0)
                return none;
        }
        for (var k = n - 1; k >= 0; k--)
        {
            var item = _pick(unit, Weapon, 0, types[k]);
            if (item != notFound)
                return item;
        }
        var anything = _pick(unit, Weapon, 0, none);
        return anything != notFound ? anything : none;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _game.Log.Error($"weapon pick: {what} failed: {e}");
    }
}
