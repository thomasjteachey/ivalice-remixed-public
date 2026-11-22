using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// The mod's fftpack files (FFTIVC/data/enhanced/fftpack: the battle data ENTD1–4, and in map/ the classic files of
/// ReMixed's own maps in place of the remaster's) given to the game when it reads them by number. Installed by
/// <see cref="Rules"/> with "pack-files".
/// </summary>
/// <remarks>
/// The mod loader (fftivc.utility.modloader) serves replaced fftpack files through a hook it finds by pattern, and in
/// this version of the game that pattern isn't found (its log: "Pattern fileReadRequestOffset not found!"), so the
/// game read the stock files: a battle using ENTD4's entry 0x1D6 got vanilla's Lucavi on the Deck and on the PC (the
/// bytes read, logged after the read, were vanilla's). So this hooks the game's read (0x2F40C4: the file's number, the
/// sector to start at, the size, the buffer; most files come from one pack container at the file's offset) and, after
/// it, copies the mod's bytes for the same range over the buffer. A file's number comes from the mod loader's list
/// (FileLists/fftpack.txt, "number|path"), with known numbers for ENTD and the Bervenia maps.
/// The files in fftpack/text/ (the event text) aren't read by number and aren't handled here.
/// </remarks>
internal static unsafe class PackFiles
{
    // The file read (0x2F40C4): mov [rsp+8], rbx; mov [rsp+0x10], rbp; mov [rsp+0x18], rsi; push rdi; sub rsp, 0x30;
    // cmp byte [..], 0; mov rsi, r9; mov ebp, r8d; mov ebx, ecx; mov edi, edx.
    internal const string ReadFilePattern =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 30 80 3D ?? ?? ?? ?? 00 49 8B F1 41 8B E8 8B D9 8B FA";
    internal const int Sector = 0x800;

    private static readonly Dictionary<string, uint> KnownNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["battle_entd1_ent.bin"] = 224, ["battle_entd2_ent.bin"] = 225,
        ["battle_entd3_ent.bin"] = 226, ["battle_entd4_ent.bin"] = 227,
        ["map/map_map075_9.bin"] = 3149, ["map/new_map_new_map075_9.bin"] = 3883,
        ["map/map_map119_6.bin"] = 3747, ["map/map_map119_7.bin"] = 3748,
    };

    /// <summary>The mod's files by the game's number for them.</summary>
    internal static readonly Dictionary<uint, byte[]> Files = [];

    /// <summary>
    /// Numbers the game has no file for, the mod's own (<see cref="RemixedSprites"/>'s sheets): the game's read takes
    /// the file named here in their place, so its loader's state goes on as for any read (the read looks a number up
    /// in its pack's offsets unchecked), and the mod's bytes go over what it read.
    /// </summary>
    internal static readonly Dictionary<uint, uint> StandIns = [];
    internal static string PackDirectory = "", FileList = "";
    internal static nint ReadFileAt;
    private static delegate* unmanaged<uint, int, int, nint, long> _gameReadFile;
    private static Log _log = null!;
    private static readonly HashSet<uint> Noted = [];

    /// <summary>Called before (false) and after (true) each read, for the scenario code's breadcrumbs.</summary>
    internal static Action<uint, int, int, nint, long, bool>? Watch;

    public static void Install(Game game)
    {
        _log = game.Log;
        ReadFileAt = game.Image.FindUnique(ReadFilePattern);
        Load(FileList, PackDirectory);
        _gameReadFile = (delegate* unmanaged<uint, int, int, nint, long>)game.Hooks.Hook(
            ReadFileAt, (nint)(delegate* unmanaged<uint, int, int, nint, long>)&ReadFile, "the fftpack file read");
        _log.Info($"pack files: {Files.Count} of the mod's fftpack files given to the game by number "
                  + $"({string.Join(", ", Files.Select(f => $"{f.Key}: {f.Value.Length} bytes"))})");
    }

    /// <summary>Number the replaced files using the loader's installed file list. Reloaded resolves each mod's
    /// directory by ID, including installs with a renamed folder.</summary>
    internal static void Load(string list, string pack)
    {
        Files.Clear();
        StandIns.Clear();
        if (!Directory.Exists(pack))
            return;
        var numbers = new Dictionary<string, uint>(KnownNumbers, StringComparer.OrdinalIgnoreCase);
        if (File.Exists(list))
        {
            foreach (var line in File.ReadLines(list))
            {
                var bar = line.IndexOf('|');
                if (bar > 0 && uint.TryParse(line.AsSpan(0, bar), out var number))
                    numbers[line[(bar + 1)..].Replace('\\', '/')] = number;
            }
        }
        foreach (var path in Directory.GetFiles(pack, "*.bin", SearchOption.AllDirectories))
        {
            // The list names a file by its path in fftpack ("map\map_map119_7.bin" for the maps' folder).
            var relative = Path.GetRelativePath(pack, path).Replace('\\', '/');
            if (numbers.TryGetValue(relative, out var number))
                Files[number] = File.ReadAllBytes(path);
        }
    }

    /// <summary>The bytes of the mod's file <paramref name="file"/> for a read from <paramref name="sector"/> of
    /// <paramref name="size"/> bytes, copied over <paramref name="buffer"/>; false if the mod hasn't that file.</summary>
    internal static bool Give(uint file, int sector, int size, byte* buffer)
    {
        if (!Files.TryGetValue(file, out var data))
            return false;
        var from = (long)sector * Sector;
        var count = (int)Math.Min(size, Math.Max(0, data.Length - from));
        if (count > 0)
            data.AsSpan((int)from, count).CopyTo(new Span<byte>(buffer, count));
        return true;
    }

    [UnmanagedCallersOnly]
    private static long ReadFile(uint file, int sector, int size, nint buffer)
    {
        Watch?.Invoke(file, sector, size, buffer, 0, false);
        var result = _gameReadFile(StandIns.TryGetValue(file, out var standIn) ? standIn : file, sector, size, buffer);
        try
        {
            if (result == 0 && Give(file, sector, size, (byte*)buffer) && Noted.Add(file))
                _log.Info($"pack files: the game read file {file}; the mod's given ({size} bytes from sector {sector})");
            Watch?.Invoke(file, sector, size, buffer, result, true);
        }
        catch (Exception e)
        {
            _log.Error($"pack files: {e.Message}");
        }
        return result;
    }
}
