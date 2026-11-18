"""Battle maps for the enhanced remaster: its classic map files read, and new enhanced looks built from them.

The remaster keeps every PS1 map as classic files in its packs (`fftpack/map/map_mapNNN_gns.bin` and numbered parts:
textures of 256 x 1024 pixels at 4 bits, and meshes holding the polygons, palettes, lights and the battle grid; the
PS1's layout, the FFHacktics wiki's Maps/GNS and Maps/Mesh). The enhanced game draws a map from a look of its own:
`bg/meshes/map_NNN_mesh.bin` (FFTOMESH), pictures in `bg/textures/NNN/` and rows in the RefinedBg* tables. It has
looks for maps 1-96 (not 15, 33, 42), 100 and 103-116; on any other map a scene shows its units on the sky.
ReMixed uses maps 97, 99 and 101 (vanilla's, no vanilla scenario does) and its own VOLCANO (119), so the build makes
looks for them from their classic files: every polygon where the classic map has it (the two share coordinates: x and z
on a 28-unit grid, y in 12-unit steps, negative upward), a texture group per palette the polygons use, its colour
picture the classic texture in that palette, and a flat lighting picture.

FFTOMESH, as The Ivalice Chronicles Mod Studio's notes describe it (Zodi-ark, `mod_studio/map_enhanced.py`) and as
the game's own files have it (checked on map 021):

    0x00  "FFTOMESH", u32 map number, u32 block count, u32 4096, 4096, 4096, 4096, 8192, 12 zero bytes
    0x30  per block: 32 name slots of 32 bytes (the name, a zero, 0xFE to the end; unused slots zeros), u32 names
          used; u32 corners of textured quads, textured triangles, untextured quads, untextured triangles; u32 V (the
          UV pool's size); 3 u32 zeros. With T the textured corners and A all of them:
          A bytes: each corner's part (name slot); A x 16: x, y, z float32 and a word (below); T x 8: normal int16
          x, y, z (4096 = 1) and 2 zeros; T x 32: u32 its UV pool entry, u32 0, u32 its texture group, 20 zeros;
          V x 28: u32 texture group, float32 u, v, 16 zeros; 32 x 16: per texture group, how many times its colour
          picture repeats across its UVs, two float64 (1.0, 1.0).

A corner's word: the first corner's is the classic polygon's tile (2 bytes as the classic mesh has them, FE FF none;
00 00 on untextured polygons) then FF FF; the second's 00 00 and the classic polygon's index among those of its kind
(FF FF a new one); the last's its render properties (u16: bit 15 unlit, 13-2 hidden from camera angles) then FF FF; a
textured quad's third BB BB FF FF, an untextured quad's 00 00 FF FF. Polygons go textured quads, textured triangles,
untextured quads, untextured triangles; parts are named GT_<group>_<name> for textured polygons and FT_<n> for
untextured ones, after a root "model"; the RefinedBgMeshNode table has a row per name.
"""
from __future__ import annotations

import math
import struct
from dataclasses import dataclass, field
from pathlib import Path

# The classic mesh file's pointer table (Maps/Mesh): each part's offset, 0 if absent.
POLYGONS, PALETTES, LIGHTS, TERRAIN, RENDER = 0x40, 0x44, 0x64, 0x68, 0xB0
KINDS = ("tt", "tq", "ut", "uq")  # the classic order: textured triangles, quads, untextured triangles, quads
CORNERS = {"tt": 3, "tq": 4, "ut": 3, "uq": 4}
# Render properties: 896 bytes, then 16 bits per polygon for up to 512 textured triangles, 768 textured quads, 64 and
# 256 untextured.
RENDER_STARTS = {"tt": 896, "tq": 896 + 1024, "ut": 896 + 2560, "uq": 896 + 2688}
UNBOUND_TILE = b"\xfe\xff"
TEXTURE_BYTES, TEXTURE_WIDTH, TEXTURE_HEIGHT, PAGE_HEIGHT = 131072, 256, 1024, 256


@dataclass
class ClassicPolygon:
    kind: str                       # "tt", "tq", "ut", "uq"
    index: int                      # among the polygons of its kind
    corners: tuple                  # ((x, y, z), ...) int16, y negative upward
    normals: tuple = ()             # ((x, y, z), ...) int16, 4096 = 1 (textured only)
    uvs: tuple = ()                 # ((u, v), ...) 0-255 within the page
    palette: int = 0
    page: int = 0                   # 0-3: the 256-row band of the texture
    tile: bytes = UNBOUND_TILE      # its tile as the mesh stores it (2 bytes)
    render: int = 0                 # its 16 render bits

    @property
    def textured(self) -> bool:
        return self.kind in ("tt", "tq")


@dataclass
class Tile:
    """A battle grid tile: x, z, level (1 the upper one) and its 8 bytes (Maps/Mesh's terrain): byte 2 its height in
    steps, byte 3 bits 0-4 its slope's rise, byte 4 the slope, byte 6 bit 0 "no cursor"."""
    x: int
    z: int
    level: int
    raw: bytes

    def corner_heights(self) -> tuple:
        """The heights of its corners (x0,z0), (x1,z0), (x1,z1), (x0,z1), in height steps."""
        rises = SLOPES.get(self.raw[4], (0, 0, 0, 0))
        return tuple(self.raw[2] + (self.raw[3] & 0x1F) * r for r in rises)

    @property
    def has_panel(self) -> bool:
        """A tile the cursor can stop on (not a blank place, not "no cursor"): the game draws highlights over it."""
        return any(self.raw) and not self.raw[6] & 1


# Slopes (a tile's byte 4): the corners each raises by the slope's rise, (x0,z0), (x1,z0), (x1,z1), (x0,z1); north is
# +z, east +x (the Mod Studio's measurement of the documents' slope names on the game's maps).
SLOPES = {0x85: (0, 0, 1, 1), 0x52: (0, 1, 1, 0), 0x25: (1, 1, 0, 0), 0x58: (1, 0, 0, 1),
          0x41: (0, 0, 1, 0), 0x11: (0, 1, 0, 0), 0x14: (1, 0, 0, 0), 0x44: (0, 0, 0, 1),
          0x96: (0, 1, 1, 1), 0x66: (1, 1, 1, 0), 0x69: (1, 1, 0, 1), 0x99: (1, 0, 1, 1)}
TILE_SIZE, HEIGHT_STEP, TILE_BYTES = 28, 12, 8


@dataclass
class ClassicMesh:
    polygons: list = field(default_factory=list)
    palettes: list | None = None    # 16 palettes of 16 raw 15-bit colours
    tiles: list | None = None       # the battle grid, both levels


def read_classic_mesh(data: bytes) -> ClassicMesh:
    """A classic mesh file's polygons (with their render bits), palettes and battle grid."""
    mesh = ClassicMesh()
    at = struct.unpack_from("<I", data, TERRAIN)[0]
    if at:
        size_x, size_z = data[at], data[at + 1]
        mesh.tiles = [Tile(x, z, level, bytes(data[o:o + TILE_BYTES]))
                      for level in range(2) for z in range(size_z) for x in range(size_x)
                      for o in [at + 2 + level * 256 * TILE_BYTES + TILE_BYTES * (z * size_x + x)]]
    at = struct.unpack_from("<I", data, POLYGONS)[0]
    if at:
        mesh.polygons = classic_polygons(data, at)
        render = struct.unpack_from("<I", data, RENDER)[0]
        if render:
            for p in mesh.polygons:
                p.render = struct.unpack_from("<H", data, render + RENDER_STARTS[p.kind] + 2 * p.index)[0]
    at = struct.unpack_from("<I", data, PALETTES)[0]
    if at:
        mesh.palettes = [list(struct.unpack_from("<16H", data, at + 32 * k)) for k in range(16)]
    return mesh


def classic_polygons(data: bytes, at: int) -> list:
    counts = dict(zip(KINDS, struct.unpack_from("<4H", data, at)))
    o = at + 8
    corners = {}
    for kind in KINDS:
        n = CORNERS[kind]
        corners[kind] = [tuple(struct.unpack_from("<3h", data, o + 6 * (n * i + k)) for k in range(n))
                         for i in range(counts[kind])]
        o += 6 * n * counts[kind]
    normals = {}
    for kind in ("tt", "tq"):
        n = CORNERS[kind]
        normals[kind] = [tuple(struct.unpack_from("<3h", data, o + 6 * (n * i + k)) for k in range(n))
                         for i in range(counts[kind])]
        o += 6 * n * counts[kind]
    out = []
    for kind, size in (("tt", 10), ("tq", 12)):
        for i in range(counts[kind]):
            r = data[o:o + size]
            uvs = ((r[0], r[1]), (r[4], r[5]), (r[8], r[9])) + (((r[10], r[11]),) if size == 12 else ())
            out.append(ClassicPolygon(kind, i, corners[kind][i], normals[kind][i], uvs, r[2] & 0x0F, r[6] & 3))
            o += size
    for kind in ("ut", "uq"):
        for i in range(counts[kind]):
            out.append(ClassicPolygon(kind, i, corners[kind][i]))
            o += 4
    for p in out:
        if p.textured:
            p.tile = bytes(data[o:o + 2])
            o += 2
    return out


# The GNS (Maps/GNS): 20-byte records: u16, u8 arrangement, u8 time and weather (0x80 night, bits 4-6 the weather),
# u16 the kind, then (in the remaster) at +12 the part's place among the map's numbered files in number order.
TEXTURE, PRIMARY_MESH, OVERRIDE_MESH, ALTERNATE_MESH = 0x1701, 0x2E01, 0x2F01, 0x3001


def numbered_files(folder: Path, number: int) -> list:
    """A map's numbered classic files (`map_mapNNN_<n>.bin`), in number order."""
    found = []
    for path in folder.glob(f"map_map{number:03d}_*.bin"):
        tail = path.stem.rsplit("_", 1)[-1]
        if tail.isdigit():
            found.append((int(tail), path))
    return [path for _, path in sorted(found)]


def default_parts(folder: Path, number: int) -> tuple:
    """The map's default state (arrangement 0, day, no weather): its texture file and its mesh files, in GNS order."""
    gns = (folder / f"map_map{number:03d}_gns.bin").read_bytes()
    files = numbered_files(folder, number)
    texture, meshes = None, []
    for o in range(0, len(gns) - 19, 20):
        arrangement, time_weather, kind = gns[o + 2], gns[o + 3], struct.unpack_from("<H", gns, o + 4)[0]
        index = struct.unpack_from("<I", gns, o + 12)[0]
        if arrangement or time_weather or index >= len(files):
            continue
        if kind == TEXTURE and texture is None:
            texture = files[index]
        elif kind in (PRIMARY_MESH, OVERRIDE_MESH, ALTERNATE_MESH):
            meshes.append(files[index])
    return texture, meshes


def classic_map(folder: Path, number: int) -> tuple:
    """(polygons, palettes, texture, tiles) of a map's default state: the first of its meshes with polygons, with
    palettes and with a battle grid, and its texture file's bytes."""
    texture_file, mesh_files = default_parts(folder, number)
    polygons = palettes = tiles = None
    for path in mesh_files:
        mesh = read_classic_mesh(path.read_bytes())
        polygons = polygons or mesh.polygons or None
        palettes = palettes or mesh.palettes
        tiles = tiles or mesh.tiles
    raw = texture_file.read_bytes() if texture_file else b""
    if polygons is None or palettes is None:
        raise ValueError(f"map {number} has no polygons or palettes in its default state")
    return polygons, palettes, raw, tiles or []


def texture_pixels(raw: bytes) -> bytes:
    """A classic texture as one palette index per pixel (256 x 1024; a byte's left pixel is its low 4 bits)."""
    if len(raw) != TEXTURE_BYTES:
        raise ValueError(f"a map texture is {TEXTURE_BYTES} bytes, not {len(raw)}")
    pixels = bytearray(len(raw) * 2)
    pixels[0::2] = bytes(b & 0x0F for b in raw)
    pixels[1::2] = bytes(b >> 4 for b in raw)
    return bytes(pixels)


def rgba(colour: int) -> tuple:
    """A PS1 15-bit colour (red lowest) as red, green, blue, alpha; 0x0000 is see-through."""
    r, g, b = colour & 31, (colour >> 5) & 31, (colour >> 10) & 31
    return (r * 255 // 31, g * 255 // 31, b * 255 // 31, 0 if colour == 0 else 255)


def tga(width: int, height: int, rows_top_down: list, alpha: bool) -> bytes:
    """An uncompressed true-colour TGA as the game's are (bottom-left origin; 32 bits with 8 alpha bits, or 24 bits),
    from `height` rows of (r, g, b, a), the top row first."""
    header = bytes([0, 0, 2]) + bytes(9) + struct.pack("<HHBB", width, height, 32 if alpha else 24, 8 if alpha else 0)
    body = bytearray()
    for row in reversed(rows_top_down):
        for r, g, b, a in row:
            body += bytes([b, g, r, a]) if alpha else bytes([b, g, r])
    return header + bytes(body)


def colour_picture(pixels: bytes, palette: list) -> bytes:
    """The classic texture in one palette, 256 x 1024, as a 32-bit TGA."""
    colours = [rgba(c) for c in palette]
    rows = [[colours[pixels[y * TEXTURE_WIDTH + x]] for x in range(TEXTURE_WIDTH)] for y in range(TEXTURE_HEIGHT)]
    return tga(TEXTURE_WIDTH, TEXTURE_HEIGHT, rows, True)


# A lighting picture: the game draws a colour as colour x lighting x 2, so mid grey leaves it as it is, as the classic
# map draws most of its polygons (unlit: bit 15 of their render properties).
LIGHTING_SIZE, NEUTRAL_LIGHT = 16, 128


def lighting_picture() -> bytes:
    grey = (NEUTRAL_LIGHT, NEUTRAL_LIGHT, NEUTRAL_LIGHT, 255)
    return tga(LIGHTING_SIZE, LIGHTING_SIZE, [[grey] * LIGHTING_SIZE for _ in range(LIGHTING_SIZE)], False)


# The enhanced mesh's corner order: it reverses the classic winding, a quad stored as a strip (A B / C D) going round
# A C D B, a triangle A C B (measured on maps 001 and 021, every polygon made from a classic one: those orders, from
# any first corner).
HD_ORDER = {"tq": (0, 2, 3, 1), "uq": (0, 2, 3, 1), "tt": (0, 2, 1), "ut": (0, 2, 1)}
HD_KINDS = ("tq", "tt", "uq", "ut")
MESH_HEADER = (4096, 4096, 4096, 4096, 8192)
QUAD_THIRD = {"tq": b"\xbb\xbb\xff\xff", "uq": b"\x00\x00\xff\xff"}
GROUP_REPEAT = (1.0, 1.0)


@dataclass
class Look:
    """A map's enhanced look: its mesh file, its pictures (file name -> bytes), each texture group's (colour,
    lighting) picture names without ".tga", and its part names (a RefinedBgMeshNode row each; the last its
    highlight panels)."""
    mesh: bytes
    pictures: dict
    groups: dict
    names: list


# Highlight panels: the enhanced game draws a tile's move and target highlights on polygons of the look over it, in a
# part of their own (GT_<group>_panel_ui, the group after the map's last, with no pictures: the game's own in bg/ui/
# panel), not from the grid. As the game's (the Mod Studio's measurement of its 14,311): a quad over each tile the
# cursor can stop on at its corners' heights and 0.2 above them (two triangles where one corner is raised or lowered
# against the other three), the corners (x0,z0), (x1,z0), (x1,z1), (x0,z1), the UVs (x, 1 - z) of the tile, render
# 0x8001; its RefinedBgMeshNode row PANEL_ROW (drawn in every view, the first of the see-through parts).
PANEL_PART, PANEL_LIFT, PANEL_RENDER = "GT_{group}_panel_ui", 0.2, 0x8001
PANEL_CORNERS = ((0, 0), (1, 0), (1, 1), (0, 1))


def tile_panels(tile: Tile) -> list:
    """A tile's panels: [(kind, [(x, y, z)], [(u, v)])]."""
    points = [(float((tile.x + dx) * TILE_SIZE), -(h * HEIGHT_STEP + PANEL_LIFT), float((tile.z + dz) * TILE_SIZE))
              for (dx, dz), h in zip(PANEL_CORNERS, tile.corner_heights())]
    uvs = [(float(dx), float(1 - dz)) for dx, dz in PANEL_CORNERS]
    rises = SLOPES.get(tile.raw[4], (0, 0, 0, 0))
    if sum(rises) in (1, 3) and tile.raw[3] & 0x1F:
        odd = rises.index(1 if sum(rises) == 1 else 0)
        halves = ([odd, (odd + 1) % 4, (odd + 3) % 4], [(odd + 1) % 4, (odd + 2) % 4, (odd + 3) % 4])
        return [("tt", [points[i] for i in half], [uvs[i] for i in half]) for half in halves]
    return [("tq", points, uvs)]


def face_normal(points: list) -> tuple:
    """A face's normal as the mesh keeps it: (b - a) x (c - a), 4096 long."""
    (ax, ay, az), (bx, by, bz), (cx, cy, cz) = points[0], points[1], points[2]
    ux, uy, uz, vx, vy, vz = bx - ax, by - ay, bz - az, cx - ax, cy - ay, cz - az
    n = (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx)
    length = math.sqrt(sum(c * c for c in n)) or 1.0
    return tuple(int(round(c / length * 4096)) for c in n)


def build_look(folder: Path, number: int) -> Look:
    """The enhanced look of map `number` from its classic files in `folder` (see the module's notes)."""
    polygons, palettes, raw, tiles = classic_map(folder, number)
    return look_from(number, polygons, palettes, texture_pixels(raw), tiles)


def look_from(number: int, polygons: list, palettes: list, pixels: bytes, tiles: list) -> Look:
    """The enhanced look of map `number` from its classic polygons, palettes, texture (one index per pixel) and battle
    grid (for its highlight panels)."""
    used = sorted({p.palette for p in polygons if p.textured})
    group_of = {palette: k for k, palette in enumerate(used)}
    panel_group = len(used)
    names = ["model", "FT_0"] + [f"GT_{k}_pal{palette}" for k, palette in enumerate(used)]
    names.append(PANEL_PART.format(group=panel_group))
    if len(names) > 32:
        raise ValueError(f"map {number}: {len(names)} parts; a block has room for 32")
    # (kind, corners in the mesh's order, normals, uvs, part, group, words), the panels after the map's polygons.
    faces = []
    for p in sorted(polygons, key=lambda p: (HD_KINDS.index(p.kind), p.index)):
        order = HD_ORDER[p.kind]
        words = []
        for k in range(len(order)):
            if k == 0:
                words.append((p.tile if p.textured else b"\x00\x00") + b"\xff\xff")
            elif k == 1:
                words.append(b"\x00\x00" + struct.pack("<H", p.index))
            elif k == len(order) - 1:
                words.append(struct.pack("<H", p.render) + b"\xff\xff")
            else:
                words.append(QUAD_THIRD[p.kind])
        uvs = [(u / TEXTURE_WIDTH, (v + PAGE_HEIGHT * p.page) / TEXTURE_HEIGHT) for u, v in p.uvs]
        faces.append((p.kind, [tuple(float(n) for n in p.corners[c]) for c in order],
                      [p.normals[c] for c in order] if p.textured else [], [uvs[c] for c in order] if p.textured else [],
                      2 + group_of[p.palette] if p.textured else 1, group_of.get(p.palette), words))
    for tile in tiles:
        if not tile.has_panel:
            continue
        for kind, points, uvs in tile_panels(tile):
            words = [bytes([(tile.z << 1) | tile.level, tile.x]) + b"\xff\xff", b"\x00\x00\xff\xff"]
            words += [QUAD_THIRD["tq"]] if kind == "tq" else []
            words.append(struct.pack("<H", PANEL_RENDER) + b"\xff\xff")
            normal = face_normal(points)
            faces.append((kind, points, [normal] * len(points), uvs, len(names) - 1, panel_group, words))
    faces.sort(key=lambda f: HD_KINDS.index(f[0]))
    parts, positions, words, normals, uv_index, groups, pool = bytearray(), [], [], [], [], [], []
    counts = {kind: 0 for kind in HD_KINDS}
    for kind, corners, face_normals, uvs, part, group, face_words in faces:
        counts[kind] += len(corners)
        for k, corner in enumerate(corners):
            positions.append(corner)
            parts.append(part)
            words.append(face_words[k])
            if kind in ("tq", "tt"):
                normals.append(face_normals[k])
                uv_index.append(len(pool))
                groups.append(group)
                pool.append((group, *uvs[k]))
    out = bytearray(b"FFTOMESH" + struct.pack("<2I", number, 1) + struct.pack("<5I", *MESH_HEADER) + bytes(12))
    for i in range(32):
        if i < len(names):
            raw = names[i].encode("ascii")
            out += raw + b"\0" + b"\xfe" * (31 - len(raw))
        else:
            out += bytes(32)
    out += struct.pack("<9I", len(names), counts["tq"], counts["tt"], counts["uq"], counts["ut"], len(pool), 0, 0, 0)
    out += bytes(parts)
    for position, word in zip(positions, words):
        out += struct.pack("<3f", *position) + word
    for normal in normals:
        out += struct.pack("<3h", *normal) + bytes(2)
    for index, group in zip(uv_index, groups):
        out += struct.pack("<3I", index, 0, group) + bytes(20)
    for group, u, v in pool:
        out += struct.pack("<I2f", group, u, v) + bytes(16)
    for _ in range(32):
        out += struct.pack("<2d", *GROUP_REPEAT)
    pictures, group_pictures = {}, {}
    light = lighting_picture()
    for palette, k in group_of.items():
        colour = f"gt_{k}_map{number:03d}_pal{palette}_color"
        lighting = f"gt_{k}_map{number:03d}_pal{palette}_lighting"
        pictures[colour + ".tga"] = colour_picture(pixels, palettes[palette])
        pictures[lighting + ".tga"] = light
        group_pictures[k] = (colour, lighting)
    return Look(bytes(out), pictures, group_pictures, names)
