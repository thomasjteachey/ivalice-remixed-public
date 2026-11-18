using System.Text.Json;

namespace IvaliceRemixed.Patches;

/// <summary>
/// ReMixed's Geomancy by terrain (PS1 0x8015D374, called from formula 0x24
/// before its damage step). The terrain under the targeted panel (even ability
/// ids) or under the caster (odd ids) picks one of the 12 Elemental abilities,
/// and ReMixed's table gives that terrain's status set, element and visual
/// effect, which replace the ability's for this use.
/// </summary>
internal sealed unsafe class Geomancy
{
    private BattleMap _map = null!;
    private byte* _statusId;    // the action's status set id (PS1 —; 0x7B077D)
    private byte* _statusSets;  // 6-byte status sets, the loader's ItemOptionsData (PS1 SCUS 0x80063FC4; 0x80FBA0)
    private byte* _statusCopy;  // the action's copy of its set (PS1 0x80193906; 0x7B07B0)
    private byte* _extraStatus; // the remaster's second copy when the set's flags have 0x10 (0x186B028)
    private ushort* _effects;   // the effect table (AbilityEffectNumberFilterData; PS1 0x801B63F0; .rdata 0x683500)
    private (byte Status, byte Element, byte Effect)[] _table = [];

    private const int Abilities = 454;

    /// <summary>Data: 12 entries of [status set, element, effect], one per Elemental from 0x7E.</summary>
    public static Geomancy Find(GameImage image, JsonElement data)
    {
        // Action setup: copy the 6-byte status set for the action, then the extra copy when flagged.
        var status = image.FindUnique(
            "0F B6 05 ?? ?? ?? ?? 4D 8D 84 24 ?? ?? ?? ?? 0F 47 FB 41 8B CF BA 06 00 00 00 40 80 FF 03 44 8B CA " +
            "66 0F 45 C8 0F B7 C1 48 8D 0C 40 4D 8D 04 48 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? F6 05 ?? ?? ?? ?? 10 " +
            "74 ?? 8B 0D ?? ?? ?? ?? 89 0D ?? ?? ?? ?? 8A 0D ?? ?? ?? ?? 88 0D ?? ?? ?? ??");
        // The effect-number getter: table[id] for ids below 0x1C6.
        var effects = image.FindUnique("81 FB C6 01 00 00 1B C0 23 C3 48 63 C8 0F B7 84 4F ?? ?? ?? ??");

        var geomancy = new Geomancy
        {
            _map = BattleMap.Find(image),
            _statusId = (byte*)GameImage.Rip(status, 3, 7),
            _statusSets = (byte*)(image.ImageStart + *(int*)(status + 11)),
            _statusCopy = (byte*)GameImage.Rip(status + 48, 3, 7),
            _extraStatus = (byte*)GameImage.Rip(status + 75, 2, 6),
            _effects = (ushort*)(image.ImageStart + *(int*)(effects + 17)),
            _table = data.EnumerateArray().Select(e => ((byte)e[0].GetInt32(), (byte)e[1].GetInt32(), (byte)e[2].GetInt32())).ToArray(),
        };
        var copy = geomancy._statusCopy;
        if ((byte*)GameImage.Rip(status + 60, 2, 7) != copy || (byte*)GameImage.Rip(status + 69, 2, 6) != copy + 1
            || (byte*)GameImage.Rip(status + 81, 2, 6) != copy + 5 || (byte*)GameImage.Rip(status + 87, 2, 6) != geomancy._extraStatus + 4)
            throw new InvalidDataException("the action's status-set copy isn't laid out as this DLL expects");
        if (geomancy._table.Length != 12)
            throw new InvalidDataException($"the Geomancy table has {geomancy._table.Length} entries, not 12");
        // ReMixed changes an ability's effect as it's used. The loader leaves the table writable; make sure.
        GameImage.MakeWritable((nint)geomancy._effects, Abilities * sizeof(ushort));
        return geomancy;
    }

    /// <summary>Sets the current ability's status set, element and effect from the terrain; describes it for the trace.</summary>
    public string Apply(Battle battle)
    {
        var id = *battle.AbilityId;
        var attacker = *battle.Attacker;
        var tile = (id & 1) == 0 ? _map.TargetedTile(attacker) : _map.TileUnder(attacker);
        if (tile == null)
            return "no tile";
        var terrain = tile[0] & 0x3F;
        var elemental = _map.TerrainAbility[terrain];
        var k = elemental - 0x7E;
        if (k < 0 || k >= _table.Length)
            return $"terrain {terrain}: no Elemental";
        var (status, element, effect) = _table[k];
        battle.Ability[11] = status;
        battle.Weapon[7] = status;
        battle.Ability[7] = element;
        *_statusId = status;
        Buffer.MemoryCopy(_statusSets + status * 6, _statusCopy, 6, 6);
        if ((_statusCopy[0] & 0x10) != 0)
        {
            *(uint*)_extraStatus = *(uint*)(_statusCopy + 1);
            _extraStatus[4] = _statusCopy[5];
        }
        if (id < Abilities)
            _effects[id] = effect;
        return $"terrain {terrain} ({((id & 1) == 0 ? "targeted panel" : "caster's panel")}) -> 0x{elemental:X2}: " +
               $"status set {status}, element 0x{element:X2}, effect {effect}";
    }
}
