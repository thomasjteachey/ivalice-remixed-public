using System.Runtime.InteropServices;

namespace IvaliceRemixed.Patches;

/// <summary>
/// Reveal Treasures' arrows (asked 2026-11-12), as ReMixed's Preparation Menu (event 0x192) stands them: a long arrow over
/// each hidden item with something left, from ReMixed's own event graphic 0x6F (its frame 35, three 24-pixel tiles 72
/// pixels tall, shown by the graphic's animation 36), in ReMixed's colours by panel (palettes 4, 2, 3 and 5: green, red,
/// teal and purple; 6, grey, where the rare item is found and only the common one is left), each with its shadow on the
/// tile. The formation screen stands in for ReMixed's menu, so they stand there while it's up and come off as the battle
/// begins (the formation's state turns 400 on "Commence battle?" Yes).
/// </summary>
/// <remarks>
/// <para>ReMixed: op 0x58 loads graphic 0x6F into event slot 0, op 0x47 adds four units with no battle unit at the panels'
/// x and y (sprite set var 0x7F, the last unit's own, whose sheet is loaded already; level 0), op 0x59 puts the slot in
/// VRAM, then for units 0x64-0x67 op 0x7F a palette, op 0x11 animation 0x218 and op 0x44 shows them. With 12 units or
/// more on the map it takes four off first (flag 0x8F) and puts them back as its menu closes. Its grey test reads the
/// panels' found bits in the other order (flag 0x84 + k for unit k, where its price reads flag 0x87 − k for panel k);
/// here a panel is grey when its own rare item is found, and an arrow stands at its panel's level.</para>
/// <para>The remaster runs the same instructions' code. A graphic: 0x7800 bytes read from file 0x2C at sector N × 15
/// (0x2F40C4), taken into a slot (0x2732A0: the slot in [0xD40818], the buffer in [0xD40820]; two slot records of 0x7CE8
/// bytes from 0xCE3260: +0 its VRAM area, 0xFF free, 0xFE the graphic, 0x80 | a sprite's id when lent to a unit's sheet;
/// +4 its data, 0 free, 0xFE a graphic, 2 lent; +0xC its VRAM rectangle; +0x18E8 its pixels), put in VRAM (0x25FA60(the
/// rectangle, the pixels), as the event update does for op 0x59) and, with the enhanced sprites (the float [0x2C81554] at
/// least 2 and [0x2E7FC88] set), its texture named ([0x2E7FE98 + 4 × slot] 1, [0x2E7FEAC + 4 × slot] the table 0x714490
/// [N], [0x2E7FEF4 + 4 × slot] that + 0x8C, [0x2E801E8 + 4 × slot] N). A sprite's animations 500-599 are slot 0's own (its
/// 64; ReMixed's 0x218 is 536, the 36th), 600-699 slot 1's. Units are sprites from a pool of 16 (0x548 bytes from
/// 0xD30030, +0 in use; the list [0xD3A410] links those in use by +0, a new one first: +8 its id, the pool index, +0xA its
/// sprite set, +0x10 an animation asked for (+ 1), +0x88 x, +0x89 y, +0x8A level, +0x148 its battle unit (none for an
/// event's), +0x1FC the animation shown). 0x2730C4 (op 0x47's) queues one for the unit loader (0x26FB28, stepped while it
/// returns 2, again the next frame on 1, done on 0, as the formation's placing 0x1F2E4C does), 0x2735F0(id, slot, palette)
/// sets its palette (and [0x2E801A8 + 4 × id], the enhanced sprites' slot and palette, which the end of an event
/// clears), 0x272A04(id, animation) asks for an animation, 0x2731E8(id) shows it, 0x26E0F4(sprite) takes it off.</para>
/// <para>Vanilla's formation screen never has event units: it finds the sprite on a tile with 0x2607E4 and reads that
/// sprite's battle unit unchecked, and its input (0x1F22D0) walks the list reading each sprite's, for the unit last placed
/// (+0x778). So the arrows stay at the list's end, the tile lookup passes them over (a replacement written over it in
/// machine code: its callers keep r10, r11 and rcx across it, which a C# hook doesn't), and the input runs with them cut
/// off the list. The pool's 16 are the formation's too: the arrows take free ones, and one is given back (its panel marked
/// yellow instead) whenever the formation queues a unit (0x270FBC) with none free.</para>
/// </remarks>
internal static unsafe class TreasureArrows
{
    // 0x2607E4: the sprite on a tile (ecx x, edx y, r8d the level), the first the list has; +0 reads the list's head.
    // Replaced (LookupCode), not hooked.
    internal const string SpriteAtPattern =
        "4C 8B 0D ?? ?? ?? ?? 4D 85 C9 74 2E 41 0F B6 81 88 00 00 00 3B C1 75 19 41 0F B6 81 89 00 00 00 3B C2";
    // 0x2730C4: op 0x47's queue of a unit with no battle unit (x, y, level, facing, sprite set, -, id, hidden); +0x12
    // calls the find by id (0x260A98); +0x43 and +0x50 clear the loader's queue count and its next.
    internal const string QueuePattern =
        "48 83 EC 58 44 8B D9 44 8B D2 0F B7 8C 24 90 00 00 00 E8 ?? ?? ?? ?? 33 D2 48 85 C0 75 53";
    // 0x26FB28: one step of the unit loader.
    internal const string LoaderPattern =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 50 8B 0D ?? ?? ?? ?? 33 ED BB 01 00 00 00 85 C9 0F 84";
    // 0x26E0F4: takes a sprite off (rcx the sprite); +0x20 frees its pool entry (the image + the dword at +0x24).
    internal const string RemovePattern =
        "48 89 5C 24 08 0F B6 41 08 48 8D 1D ?? ?? ?? ?? 48 69 D0 48 05 00 00 45 33 DB 4C 8B C1 45 8B CB 44 89 9C 1A";
    // 0x2735F0: a sprite's palette from a slot (id, slot, palette); +0x1E the enhanced sprites' table of them.
    internal const string PalettePattern =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 20 48 63 F1 8D 42 01 44 6B C8 64 48 8D 0D";
    // 0x272A04: asks a sprite for an animation (id, animation).
    internal const string AnimatePattern =
        "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 56 48 83 EC 20 8B DA E8 ?? ?? ?? ?? 48 8B F8 48 85 C0 " +
        "74 61 81 3D ?? ?? ?? ?? B7 01 00 00";
    // 0x2731E8: shows a sprite (id), op 0x44's.
    internal const string ShowPattern =
        "48 83 EC 28 E8 ?? ?? ?? ?? 48 85 C0 74 2E 48 8B 88 48 01 00 00 41 B9 01 00 00 00 66 44 89 48 0E";
    // 0x2732A0: takes a read graphic into its slot; +0xF reads the slot, +0x16 the slots, +0x1D the buffer.
    internal const string TakePattern =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 7C 24 18 48 63 05 ?? ?? ?? ?? 48 8D 2D ?? ?? ?? ?? 4C 8B 0D ?? ?? ?? ?? 45 33 D2 " +
        "4C 69 C0 E8 7C 00 00";
    // 0x233F07, in op 0x58's task: the buffer, then the read of file 0x2C at sector N × 15 (+0x16 calls 0x2F40C4).
    internal const string ReadPattern = "E8 ?? ?? ?? ?? 6B D7 0F 4C 8B C8 44 8B C3 B9 2C 00 00 00 48 8B F0 E8 ?? ?? ?? ??";
    // 0x233FE4, in op 0x58's task: with the enhanced sprites (+0 the float, +8 the least, +0x11 the switch) the slot's
    // texture: +0x4A the table (the image + the dword at +0x4E), +0x52 slot 0's texture, +0x59 the palette's distance,
    // +0x5D its palette texture, +0x63 its kind, +0x6D its graphic.
    internal const string TexturePattern =
        "F3 0F 10 05 ?? ?? ?? ?? 0F 2F 05 ?? ?? ?? ?? 72 62 83 3D ?? ?? ?? ?? 00 74 59 85 ED 74 2C 83 FD 01 75 50 41 8B 84 BC";
    // 0x23A5A4, in the event update: op 0x59's slot put in VRAM; +0x1F the slots, +0x29 its pixels, +0x31 its rectangle,
    // +0x3B the call.
    internal const string UploadPattern =
        "48 63 05 ?? ?? ?? ?? BF FF 00 00 00 44 8D 77 FF 85 C0 78 34 48 69 D8 E8 7C 00 00 42 39 BC 23 ?? ?? ?? ?? 75 23 " +
        "49 8D 94 24 ?? ?? ?? ?? 49 8D 8C 24 ?? ?? ?? ?? 48 03 D3 48 03 CB E8";
    // 0x1F22D0: the formation screen's input (rcx the formation).
    internal const string InputPattern =
        "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 54 41 56 41 57 48 83 EC 30 4C 8B 3D ?? ?? ?? ?? 45 33 C9 " +
        "48 8B F9 8B 0D";
    // 0x270FBC: queues a unit the formation places (rcx its record).
    internal const string PlacePattern =
        "48 89 5C 24 08 48 89 74 24 18 57 41 54 41 55 48 83 EC 50 83 C8 FF 4C 8D 25 ?? ?? ?? ?? 48 8B F1 45 33 C0";

    internal const int Graphic = 0x6F, GraphicBytes = 0x7800, GraphicFile = 0x2C, GraphicSectors = 15;
    internal const int Animation = 36, SlotAnimations = 500, SlotStride = 100, Slots = 2, SlotBytes = 0x7CE8;
    internal const int SlotVram = 0, SlotData = 1, SlotState = 2, Free = 0xFF, Loaded = 0xFE; // the slot's dwords
    internal const int Sprites = 16, PoolBytes = 0x548, TakenPalette = 6, StateEnded = 400, FormationState = 0x30;
    internal const int Next = 0, Id = 8, Set = 0xA, Asked = 0x10, Flags = 0x8F, Unit = 0x148, Shown = 0x1FC;
    internal const byte X = 0x88, Y = 0x89, Level = 0x8A;
    internal const int MaxFrames = 120, MaxSteps = 64, TableSize = 4, TableAt = 0x70, LookupBytes = 0x90;
    /// <summary>ReMixed's palettes for its units 0x64-0x67, the map's panels 0-3: green, red, teal, purple.</summary>
    internal static readonly int[] Palettes = [4, 2, 3, 5];

    /// <summary>For the offline check: the tile lookup (now the replacement's), the list's head and the arrows' table.</summary>
    internal static nint LookupAt { get; private set; }
    internal static nint* ListHead => _head;
    internal static nint* ArrowTable => _table;
    internal static int* SlotPaletteIds => _paletteIds;

    private static Log _log = null!;
    private static nint* _head, _table;
    private static nint _pool, _slots, _pixels, _rect, _textures;
    private static int* _graphicSlot, _queued, _next, _spritePalettes, _enabled, _kinds, _textureIds, _paletteIds, _graphics;
    private static nint* _graphicBuffer;
    private static float* _version, _least;
    private static int _paletteDistance;
    private static delegate* unmanaged<int, nint> _find;
    private static delegate* unmanaged<int, int, int, int, int, nint, int, int, byte> _queue;
    private static delegate* unmanaged<int> _step;
    private static delegate* unmanaged<nint, void> _remove;
    private static delegate* unmanaged<int, int, int, void> _palette;
    private static delegate* unmanaged<int, int, void> _animate;
    private static delegate* unmanaged<int, int> _show;
    private static delegate* unmanaged<int> _take;
    private static delegate* unmanaged<int, int, int, nint, int> _read;
    private static delegate* unmanaged<nint, nint, void> _upload;
    private static delegate* unmanaged<nint, void> _input;
    private static delegate* unmanaged<nint, nint> _place;
    private static Action<int> _fallback = _ => { };
    private static int _failed;

    /// <summary>An arrow: its panel, where it stands, its palette, and its sprite once made.</summary>
    internal sealed record Arrow(int Panel, int X, int Y, int Level, int Palette)
    {
        public nint Sprite;
        public int Id = -1;
    }

    private static readonly List<Arrow> Waiting = [], Standing = [];
    private static nint[] _sprites = []; // Standing's sprites (and the tile lookup's table), for the input
    private static nint _cut;            // the arrows while the formation's input runs without them, else 0
    private static int _slot = -1, _set, _frames, _queuedId = -1;
    private static (int Kind, int Texture, int Palette, int Graphic) _savedTexture;
    private static bool _textured;

    /// <summary>Whether arrows are being made (the formation takes no input meanwhile).</summary>
    internal static bool Busy => Waiting.Count > 0;

    public static void Install(Game game, Action<int> fallback)
    {
        _log = game.Log;
        _fallback = fallback;
        var image = game.Image;
        var spriteAt = image.FindUnique(SpriteAtPattern);
        _head = (nint*)GameImage.Rip(spriteAt, 3, 7);
        var queue = image.FindUnique(QueuePattern);
        _queue = (delegate* unmanaged<int, int, int, int, int, nint, int, int, byte>)queue;
        _find = (delegate* unmanaged<int, nint>)GameImage.Rip(queue + 0x12, 1, 5);
        _queued = (int*)GameImage.Rip(queue + 0x43, 2, 6);
        _next = (int*)GameImage.Rip(queue + 0x50, 2, 6);
        _step = (delegate* unmanaged<int>)image.FindUnique(LoaderPattern);
        var remove = image.FindUnique(RemovePattern);
        _remove = (delegate* unmanaged<nint, void>)remove;
        _pool = image.ImageStart + *(int*)(remove + 0x24);
        var palette = image.FindUnique(PalettePattern);
        _palette = (delegate* unmanaged<int, int, int, void>)palette;
        _spritePalettes = (int*)GameImage.Rip(palette + 0x1E, 3, 7);
        _animate = (delegate* unmanaged<int, int, void>)image.FindUnique(AnimatePattern);
        _show = (delegate* unmanaged<int, int>)image.FindUnique(ShowPattern);
        var take = image.FindUnique(TakePattern);
        _take = (delegate* unmanaged<int>)take;
        _graphicSlot = (int*)GameImage.Rip(take + 0xF, 3, 7);
        _slots = GameImage.Rip(take + 0x16, 3, 7);
        _graphicBuffer = (nint*)GameImage.Rip(take + 0x1D, 3, 7);
        _read = (delegate* unmanaged<int, int, int, nint, int>)GameImage.Rip(image.FindUnique(ReadPattern) + 0x16, 1, 5);
        var texture = RemixedEventGraphics.TextureAt != 0
            ? RemixedEventGraphics.TextureAt : image.FindUnique(TexturePattern);
        _version = (float*)GameImage.Rip(texture, 4, 8);
        _least = (float*)GameImage.Rip(texture + 8, 3, 7);
        _enabled = (int*)GameImage.Rip(texture + 0x11, 2, 7);
        _textures = image.ImageStart + *(int*)(texture + 0x4E);
        _textureIds = (int*)GameImage.Rip(texture + 0x52, 2, 6);
        _paletteDistance = RemixedEventGraphics.PaletteDistance;
        // RemixedEventGraphics installs first and replaces the instruction at +0x5D. Use the pointer it captured
        // before patching instead of interpreting the replacement jump's padding as a RIP displacement.
        _paletteIds = RemixedEventGraphics.PaletteIds != null
            ? RemixedEventGraphics.PaletteIds : (int*)GameImage.Rip(texture + 0x5D, 2, 6);
        _kinds = (int*)GameImage.Rip(texture + 0x63, 2, 10);
        _graphics = (int*)GameImage.Rip(texture + 0x6D, 2, 6);
        var upload = image.FindUnique(UploadPattern);
        _upload = (delegate* unmanaged<nint, nint, void>)GameImage.Rip(upload + 0x3B, 1, 5);
        _pixels = image.ImageStart + *(int*)(upload + 0x29) - _slots;
        _rect = image.ImageStart + *(int*)(upload + 0x31) - _slots;
        if (image.ImageStart + *(int*)(upload + 0x1F) != _slots || _rect <= 0 || _pixels <= _rect || _pixels >= SlotBytes)
            throw new InvalidDataException("the event slots aren't laid out as this DLL expects");
        var lookup = GameImage.AllocateCodeNear(spriteAt, LookupBytes);
        _table = (nint*)(lookup + TableAt);
        var code = LookupCode((nint)_head, (nint)_table);
        if (code.Length > TableAt)
            throw new InvalidOperationException("the tile lookup is too big");
        code.CopyTo(new Span<byte>(lookup, code.Length));
        var jump = new byte[5];
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((nint)lookup - (spriteAt + 5)))).CopyTo(jump, 1);
        GameImage.WriteCode(spriteAt, jump);
        LookupAt = spriteAt;
        _input = (delegate* unmanaged<nint, void>)game.Hooks.Hook(
            image.FindUnique(InputPattern), (nint)(delegate* unmanaged<nint, void>)&OnInput, "the formation's input");
        _place = (delegate* unmanaged<nint, nint>)game.Hooks.Hook(
            image.FindUnique(PlacePattern), (nint)(delegate* unmanaged<nint, nint>)&OnPlace, "the formation's placing");
        BattleEvents.OnStart(game, "reveal treasures' arrows", Forget);
    }

    /// <summary>The palette of a panel's arrow: ReMixed's by panel, grey with its rare item found.</summary>
    internal static int PaletteFor(int panel, bool rareTaken) => rareTaken ? TakenPalette : Palettes[panel & 3];

    /// <summary>The animation that shows the arrow from a slot.</summary>
    internal static int AnimationFor(int slot) => SlotAnimations + slot * SlotStride + Animation;

    /// <summary>Whether a sprite needs the arrow requested again. The formation asks every sprite for animation 2;
    /// that pending request must be replaced too, rather than waiting for it to turn the arrow back into a unit.</summary>
    internal static bool NeedsAnimation(int asked, int shown, int arrow) => asked != arrow + 1 && shown != arrow;

    /// <summary>Starts making the arrows, which the formation's frames go on with (<see cref="Step"/>); false when no event
    /// slot is free or the graphic can't be had, and the caller marks the panels yellow instead.</summary>
    internal static bool Begin(IEnumerable<Arrow> arrows)
    {
        TakeOff();
        for (var k = 0; k < Slots && _slot < 0; k++)
            if (Slot(k)[SlotVram] == Free && Slot(k)[SlotData] == 0)
                _slot = k;
        if (_slot < 0)
        {
            _log.Info("reveal treasures: no event slot free for the arrows; the tiles are marked yellow");
            return false;
        }
        if (!Load(_slot))
        {
            _slot = -1;
            return false;
        }
        Waiting.AddRange(arrows);
        (_set, _frames, _queuedId) = (SpriteSet(), 0, -1);
        _log.Info($"reveal treasures: {Waiting.Count} arrow(s) to stand (event slot {_slot}, sprite set 0x{_set:X}, " +
                  $"{FreeSprites()} of {Sprites} sprites free, enhanced sprites {*_enabled} at {*_version}" +
                  $"{(_textured ? ", the slot's texture named" : "")})");
        if (Waiting.Count == 0)
            Release(true);
        return true;
    }

    private static int* Slot(int k) => (int*)(_slots + k * SlotBytes);

    /// <summary>Graphic 0x6F read, taken into the slot and put in VRAM, as ops 0x58 and 0x59 do.</summary>
    private static bool Load(int slot)
    {
        var buffer = (nint)NativeMemory.Alloc(GraphicBytes);
        try
        {
            if (_read(GraphicFile, Graphic * GraphicSectors, GraphicBytes, buffer) != 0)
            {
                _log.Error("reveal treasures: event graphic 0x6F couldn't be read; the tiles are marked yellow");
                return false;
            }
            var (oldSlot, oldBuffer) = (*_graphicSlot, *_graphicBuffer);
            (*_graphicSlot, *_graphicBuffer) = (slot, buffer);
            var taken = _take();
            (*_graphicSlot, *_graphicBuffer) = (oldSlot, oldBuffer);
            if (taken == 0 || Slot(slot)[SlotData] != Loaded)
            {
                _log.Error($"reveal treasures: event slot {slot} didn't take graphic 0x6F; the tiles are marked yellow");
                return false;
            }
        }
        finally
        {
            NativeMemory.Free((void*)buffer);
        }
        var record = (nint)Slot(slot);
        _upload(record + _rect, record + _pixels);
        Slot(slot)[SlotVram] = Loaded;
        _textured = *_version >= *_least && *_enabled != 0;
        if (_textured)
        {
            _savedTexture = (_kinds[slot], _textureIds[slot], _paletteIds[slot], _graphics[slot]);
            var id = ((int*)_textures)[Graphic];
            (_kinds[slot], _textureIds[slot], _paletteIds[slot], _graphics[slot]) = (1, id, id + _paletteDistance, Graphic);
        }
        return true;
    }

    /// <summary>The sprite set the arrows are made with: the newest unit's (its sheet is loaded), as ReMixed takes the last
    /// unit's; Ramza's with none. The arrow's frames come from the slot, whatever the set.</summary>
    private static int SpriteSet()
    {
        for (var s = *_head; s != 0; s = *(nint*)(s + Next))
            if (*(nint*)(s + Unit) != 0)
                return *(byte*)(s + Set);
        return 1;
    }

    private static bool InUse(int id) => *(int*)(_pool + id * PoolBytes) != 0;

    private static int FreeSprites() => Enumerable.Range(0, Sprites).Count(k => !InUse(k));

    private static int FreeId()
    {
        for (var k = 0; k < Sprites; k++)
            if (!InUse(k) && _find(k) == 0)
                return k;
        return -1;
    }

    /// <summary>One frame of making the arrows: the loader stepped for the one queued, which is dressed once made, and the
    /// next queued.</summary>
    internal static void Step()
    {
        try
        {
            if (++_frames > MaxFrames)
            {
                _log.Error($"reveal treasures: the arrows weren't made in {MaxFrames} frames; the rest are marked yellow");
                GiveUp();
                return;
            }
            while (Waiting.Count > 0)
            {
                if (_queuedId < 0)
                {
                    if (*_queued > *_next)
                        return; // the loader has a unit of the formation's to make first
                    var arrow = Waiting[0];
                    var id = FreeId();
                    if (id < 0 || _queue(arrow.X, arrow.Y, arrow.Level, 0, _set, 0, id, 1) == 0)
                    {
                        _log.Info($"reveal treasures: no sprite free for panel {arrow.Panel}'s arrow; its tile is marked yellow");
                        Waiting.RemoveAt(0);
                        _fallback(arrow.Panel);
                        continue;
                    }
                    _queuedId = id;
                }
                if (!Made())
                    return;
            }
            Release(Standing.Count == 0);
            _log.Info($"reveal treasures: {Standing.Count} arrow(s) stand over the hidden items" +
                      (Standing.Count == 0 ? "" : " (" + string.Join(", ", Standing.Select(a =>
                          $"panel {a.Panel} at {a.X},{a.Y} level {a.Level} palette {a.Palette} sprite {a.Id}")) + ")"));
        }
        catch (Exception e)
        {
            Failed("making the arrows", e);
            GiveUp();
        }
    }

    /// <summary>Steps the loader for the queued arrow; once it's made, dresses it as ReMixed's ops 0x7F, 0x11 and 0x44 do
    /// (or marks its tile yellow if it wasn't). Whether it's done.</summary>
    private static bool Made()
    {
        int step;
        do
            step = _step();
        while (step == 2);
        if (step != 0)
            return false;
        var arrow = Waiting[0];
        Waiting.RemoveAt(0);
        var sprite = _find(_queuedId);
        if (sprite == 0 || *(nint*)(sprite + Unit) != 0)
        {
            _log.Error($"reveal treasures: panel {arrow.Panel}'s arrow wasn't made; its tile is marked yellow");
            _fallback(arrow.Panel);
        }
        else
        {
            (arrow.Sprite, arrow.Id) = (sprite, _queuedId);
            _palette(arrow.Id, _slot, arrow.Palette);
            *(byte*)(sprite + Flags) &= 0xFE; // op 0x11 with its last byte 0
            _animate(arrow.Id, AnimationFor(_slot));
            _show(arrow.Id);
            ToEnd(_head, sprite);
            Standing.Add(arrow);
            Publish();
        }
        _queuedId = -1;
        return true;
    }

    /// <summary>Stops making arrows: one queued is made (the loader always finishes) and stands with the rest; those not
    /// made yet have their tiles marked yellow.</summary>
    private static void GiveUp()
    {
        for (var k = 0; _queuedId >= 0 && k < MaxSteps; k++)
            Made();
        _queuedId = -1;
        foreach (var arrow in Waiting)
            _fallback(arrow.Panel);
        Waiting.Clear();
        Release(Standing.Count == 0);
    }

    /// <summary>Each frame of the formation while arrows stand: they keep their animation, and come off as it ends.</summary>
    internal static void Keep(nint formation)
    {
        if (Standing.Count == 0)
            return;
        if (*(int*)(formation + FormationState) >= StateEnded)
        {
            TakeOff();
            return;
        }
        var animation = AnimationFor(_slot);
        foreach (var arrow in Standing)
            if (NeedsAnimation(*(ushort*)(arrow.Sprite + Asked), *(ushort*)(arrow.Sprite + Shown), animation))
                _animate(arrow.Id, animation); // something asked every sprite for another (the formation's setup asks 2)
    }

    /// <summary>Takes the arrows off and gives the event slot back.</summary>
    internal static void TakeOff()
    {
        if (Waiting.Count > 0)
            GiveUp();
        var count = Standing.Count;
        foreach (var arrow in Standing.ToList())
            Remove(arrow);
        Release(true);
        if (count == 0)
            return;
        var (sprites, unitless) = (0, 0);
        for (var s = *_head; s != 0 && sprites < Sprites; s = *(nint*)(s + Next), sprites++)
            if (*(nint*)(s + Unit) == 0)
                unitless++;
        _log.Info($"reveal treasures: {count} arrow(s) taken off as the battle begins ({sprites} sprite(s) left, " +
                  $"{unitless} without a unit)");
    }

    /// <summary>Takes an arrow's sprite off (out of the cut-off arrows too, while the formation's input runs).</summary>
    private static void Remove(Arrow arrow)
    {
        Standing.Remove(arrow);
        Publish();
        if (_cut != 0)
        {
            var cut = _cut;
            Unlink(&cut, arrow.Sprite);
            _cut = cut;
        }
        if (InUse(arrow.Id) && *(byte*)(arrow.Sprite + Id) == arrow.Id && *(nint*)(arrow.Sprite + Unit) == 0)
            _remove(arrow.Sprite);
        _spritePalettes[arrow.Id] = 0;
    }

    /// <summary>With <paramref name="slot"/>, the slot as an event's end leaves it and its texture as it was.</summary>
    private static void Release(bool slot)
    {
        if (!slot || _slot < 0)
            return;
        var record = Slot(_slot);
        if (record[SlotVram] == Loaded && record[SlotData] == Loaded)
            (record[SlotVram], record[SlotData], record[SlotState]) = (Free, 0, 0);
        if (_textured)
            (_kinds[_slot], _textureIds[_slot], _paletteIds[_slot], _graphics[_slot]) = _savedTexture;
        (_slot, _textured) = (-1, false);
    }

    /// <summary>A new battle: the game has cleared its sprites and slots, so the arrows are only forgotten.</summary>
    private static void Forget()
    {
        foreach (var arrow in Standing)
            _spritePalettes[arrow.Id] = 0;
        Standing.Clear();
        Waiting.Clear();
        Publish();
        (_cut, _slot, _textured, _queuedId) = (0, -1, false, -1);
    }

    // The sprites' list: +0 the next. The arrows are kept at its end, after every unit (a new sprite goes first).

    /// <summary>Moves a sprite to the list's end.</summary>
    internal static void ToEnd(nint* head, nint sprite)
    {
        if (!Unlink(head, sprite))
            return;
        *(nint*)(sprite + Next) = 0;
        var link = head;
        while (*link != 0)
            link = (nint*)(*link + Next);
        *link = sprite;
    }

    /// <summary>Takes a sprite out of a list; whether it was there.</summary>
    internal static bool Unlink(nint* head, nint sprite)
    {
        for (var link = head; *link != 0; link = (nint*)(*link + Next))
            if (*link == sprite)
            {
                *link = *(nint*)(sprite + Next);
                return true;
            }
        return false;
    }

    /// <summary>Cuts the list before the first of <paramref name="ours"/> when everything from there on is ours; returns
    /// what was cut off (0 for nothing).</summary>
    internal static nint Cut(nint* head, ReadOnlySpan<nint> ours)
    {
        for (var link = head; *link != 0; link = (nint*)(*link + Next))
            if (ours.Contains(*link))
            {
                for (var s = *link; s != 0; s = *(nint*)(s + Next))
                    if (!ours.Contains(s))
                        return 0;
                var cut = *link;
                *link = 0;
                return cut;
            }
        return 0;
    }

    /// <summary>Puts what <see cref="Cut"/> took back at the list's end.</summary>
    internal static void Join(nint* head, nint cut)
    {
        if (cut == 0)
            return;
        var link = head;
        while (*link != 0)
            link = (nint*)(*link + Next);
        *link = cut;
    }

    /// <summary>The tile lookup's replacement, put over 0x2607E4: vanilla's walk of the list for the first sprite on (ecx
    /// x, edx y, r8d level), passing over the sprites in <paramref name="table"/> (the arrows: four, 0 for none). It
    /// touches only rax and r9, as vanilla's does, since its callers keep r10, r11 and rcx across it (ExeTool liveregs;
    /// a C# hook crashed the panel check 0xD9CF0, which reads r10 after it).</summary>
    internal static byte[] LookupCode(nint head, nint table)
    {
        var code = new List<byte> { 0x49, 0xB9 };                                        //    mov r9, head
        code.AddRange(BitConverter.GetBytes((long)head));
        code.AddRange([0x4D, 0x8B, 0x09]);                                               //    mov r9, [r9]
        code.AddRange([0x4D, 0x85, 0xC9, 0x74, 0x4F]);                                   // 13 test r9, r9; jz none
        code.AddRange([0x41, 0x0F, 0xB6, 0x81, X, 0, 0, 0, 0x3B, 0xC1, 0x75, 0x3E]);     //    movzx eax, [r9+x]; cmp ecx; jne next
        code.AddRange([0x41, 0x0F, 0xB6, 0x81, Y, 0, 0, 0, 0x3B, 0xC2, 0x75, 0x32]);     //    the same for y, edx
        code.AddRange([0x41, 0x0F, 0xB6, 0x81, Level, 0, 0, 0, 0x41, 0x3B, 0xC0, 0x75, 0x25]); // and the level, r8d
        code.AddRange([0x48, 0xB8]);                                                     // 55 mov rax, table
        code.AddRange(BitConverter.GetBytes((long)table));
        code.AddRange([0x4C, 0x3B, 0x08, 0x74, 0x16]);                                   //    cmp r9, [rax]; je next
        code.AddRange([0x4C, 0x3B, 0x48, 0x08, 0x74, 0x10]);                             //    cmp r9, [rax+8]; je next
        code.AddRange([0x4C, 0x3B, 0x48, 0x10, 0x74, 0x0A]);                             //    cmp r9, [rax+0x10]; je next
        code.AddRange([0x4C, 0x3B, 0x48, 0x18, 0x74, 0x04]);                             //    cmp r9, [rax+0x18]; je next
        code.AddRange([0x49, 0x8B, 0xC1, 0xC3]);                                         //    mov rax, r9; ret
        code.AddRange([0x4D, 0x8B, 0x09, 0xEB, 0xAC]);                                   // 92 next: mov r9, [r9]; jmp 13
        code.AddRange([0x33, 0xC0, 0xC3]);                                               // 97 none: xor eax, eax; ret
        return code.ToArray();
    }

    /// <summary>The arrows' sprites, for the tile lookup's table and the formation's input.</summary>
    private static void Publish()
    {
        _sprites = Standing.Select(a => a.Sprite).ToArray();
        for (var k = 0; k < TableSize; k++)
            _table[k] = k < _sprites.Length ? _sprites[k] : 0;
    }

    /// <summary>The formation's input, run with the arrows cut off the list.</summary>
    [UnmanagedCallersOnly]
    private static void OnInput(nint formation)
    {
        var cut = false;
        try
        {
            if (_sprites.Length > 0 && _cut == 0)
            {
                foreach (var sprite in _sprites)
                    ToEnd(_head, sprite); // they stay last (a new sprite goes first), but nothing is left to chance
                _cut = Cut(_head, _sprites);
                cut = true;
            }
        }
        catch (Exception e)
        {
            Failed("the formation's input", e);
        }
        _input(formation);
        if (cut)
        {
            Join(_head, _cut);
            _cut = 0;
        }
    }

    /// <summary>The formation's placing of a unit: with no sprite free, an arrow gives its own back first.</summary>
    [UnmanagedCallersOnly]
    private static nint OnPlace(nint record)
    {
        try
        {
            if (Standing.Count > 0 && FreeSprites() == 0)
            {
                var arrow = Standing[^1];
                Remove(arrow);
                _fallback(arrow.Panel);
                _log.Info($"reveal treasures: panel {arrow.Panel}'s arrow gives its sprite to a unit placed; its tile is " +
                          "marked yellow");
            }
        }
        catch (Exception e)
        {
            Failed("giving a sprite back", e);
        }
        return _place(record);
    }

    private static void Failed(string what, Exception e)
    {
        if (Interlocked.Increment(ref _failed) <= 3)
            _log.Error($"reveal treasures: {what}: {e.Message}");
    }
}
