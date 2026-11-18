#!/usr/bin/env python3
"""ReMixed's unit sprite sheets for the remaster: decoded, scaled to the enhanced sprites' size, written as textures.

A PS1 unit sheet (BATTLE/*.SPR; ReMixed names them by graphic, 00.SPR to 9E.SPR) is 16 palettes of 16 colours
(0x200 bytes), 288 rows of 256 pixels at 4 bits (0x9000 bytes, the left pixel in the low bits), then 200 more rows
packed: a nibble stream, high nibble first, where a non-zero nibble is a pixel and a zero starts a run of empty
pixels (0 n: n; 0 0 n: n; 0 7 a b: b << 4 | a; 0 8 a b c: c << 8 | b << 4 | a). The remaster unpacks it the same way
(0x0CB5B4), two pixels a byte with the first in the high bits. The sheet as drawn is rows 0-255, then the 200 packed
rows, then the file's rows 256-287 (the portrait) at the bottom: 488 rows.

With the enhanced sprites on, the remaster draws a unit's graphic from two textures of system/ffto/g2d.dat (the table
at .rdata 0x7151F0 names the first, by graphic): the sheet's rows 0-255 and rows 256-487, each redrawn at twice the
size (512 x 512 and 512 x 464), 4 bits a pixel in the sheet's own palettes. For ReMixed's own art there's no redraw,
so its sheet is scaled up with Scale2x (EPX), which keeps every pixel one of the sheet's palette indices (each team's
palette still works) and rounds the diagonals. Scored against the remaster's own redraws of sheets ReMixed left as
they were, it matches them more closely than doubling the pixels (Ramza 70% of the drawn pixels against 67%, a
Knight 80% against 77%, Agrias 75% against 72%, a Behemoth 86% against 82%); an xBR variant without blending scored
the same as Scale2x and left jagged outlines.

    python -I tools/sprites.py preview 5D 97      # ReMixed's sheets beside their textures, as PNGs in work/sprites/
"""
from __future__ import annotations

import argparse
import struct
import sys
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

WIDTH = 256
RAW_ROWS, PACKED_ROWS, PORTRAIT_ROWS = 288, 200, 32
ROWS = 256 + PACKED_ROWS + PORTRAIT_ROWS  # 488
PALETTES = 0x200
PACKED_AT = PALETTES + RAW_ROWS * WIDTH // 2  # 0x9200
TOP_ROWS = 256  # the first texture: the sheet's rows 0-255; the second: rows 256-487
TOP_BYTES, BOTTOM_BYTES = 2 * TOP_ROWS * WIDTH, 2 * (ROWS - TOP_ROWS) * WIDTH  # at twice the size, 4 bits a pixel
SECTOR = 0x800


def _nibbles(data: bytes, start: int):
    for at in range(start, len(data)):
        yield at, data[at] >> 4
        yield at, data[at] & 0xF


def unpack(spr: bytes) -> tuple[bytearray, int]:
    """The 200 packed rows' pixels, and the file's length up to the last byte they use."""
    out, need, last = bytearray(), PACKED_ROWS * WIDTH, PACKED_AT
    stream = _nibbles(spr, PACKED_AT)

    def take() -> int:
        nonlocal last
        at, n = next(stream)
        last = at + 1
        return n

    try:
        while len(out) < need:
            n = take()
            if n:
                out.append(n)
                continue
            count = take()
            if count == 0:
                count = take()
            elif count == 7:
                low = take()
                count = take() << 4 | low
            elif count == 8:
                a, b = take(), take()
                count = take() << 8 | b << 4 | a
            out.extend(bytes(count))
    except StopIteration:
        pass
    out = out[:need]
    out.extend(bytes(need - len(out)))
    # The remaster puts the first of each two pixels in a byte's high bits, so it's the right one.
    out[0::2], out[1::2] = out[1::2], out[0::2]
    return out, last


def decode(spr: bytes) -> tuple[list[tuple[int, ...]], bytearray]:
    """A sheet's 16 palettes (15-bit colours) and its 256 x 488 pixels (palette indices), as the game draws it."""
    palettes = [struct.unpack_from("<16H", spr, 32 * k) for k in range(16)]
    raw = bytearray()
    for b in spr[PALETTES:PACKED_AT]:
        raw += bytes((b & 0xF, b >> 4))
    raw.extend(bytes(RAW_ROWS * WIDTH - len(raw)))
    packed, _ = unpack(spr)
    return palettes, raw[:256 * WIDTH] + packed + raw[256 * WIDTH:]


def sheet_length(spr: bytes) -> int:
    """A sheet file's length without ReMixed's padding (its sheets take 0x10000 on its disc), rounded up to a sector
    as the remaster's own are. Zero bytes at the end of the packed stream draw nothing (0 0 0 is a run of none), and
    a small sheet's stream may stop short of its 200 rows, as vanilla's own do (KASANEK.SPR's gives 33,081 of 51,200
    pixels): the rows past it are never drawn."""
    content = len(spr.rstrip(b"\0"))
    return min(len(spr), -(-max(content, PACKED_AT) // SECTOR) * SECTOR)


def scale2x(pixels: bytes | bytearray, width: int, height: int) -> bytearray:
    """Scale2x (EPX): each pixel becomes four, a corner taking a neighbour's index where two neighbours that meet at it
    agree and the other two don't. Every output pixel is one of the input's."""
    out, w2 = bytearray(4 * width * height), 2 * width
    last_x, last_y = width - 1, height - 1
    for y in range(height):
        row = y * width
        up = (y - 1 if y else 0) * width
        down = (y + 1 if y < last_y else last_y) * width
        o = 2 * y * w2
        for x in range(width):
            e = pixels[row + x]
            b, h = pixels[up + x], pixels[down + x]
            d = pixels[row + x - 1] if x else e
            f = pixels[row + x + 1] if x < last_x else e
            e0 = e1 = e2 = e3 = e
            if b != h and d != f:
                if d == b:
                    e0 = d
                if b == f:
                    e1 = f
                if d == h:
                    e2 = d
                if h == f:
                    e3 = f
            out[o] = e0
            out[o + 1] = e1
            out[o + w2] = e2
            out[o + w2 + 1] = e3
            o += 2
    return out


def pack4(pixels: bytes | bytearray) -> bytes:
    """Pixels at 4 bits, the left one of each two in the low bits."""
    return bytes(pixels[k] | pixels[k + 1] << 4 for k in range(0, len(pixels), 2))


def textures(spr: bytes) -> tuple[bytes, bytes]:
    """The enhanced sprites' two textures of a sheet: rows 0-255 and 256-487, at twice the size."""
    _, pixels = decode(spr)
    big = scale2x(pixels, WIDTH, ROWS)
    split = 2 * TOP_ROWS * 2 * WIDTH
    top, bottom = pack4(big[:split]), pack4(big[split:])
    assert len(top) == TOP_BYTES and len(bottom) == BOTTOM_BYTES
    return top, bottom


def unpack4(data: bytes) -> bytearray:
    out = bytearray()
    for b in data:
        out += bytes((b & 0xF, b >> 4))
    return out


G2D_TABLE = 0xCEC800  # system/ffto/g2d.dat: 16 bytes an entry, {offset, size, 6, 0}


def g2d_entry(path: Path, n: int) -> bytes:
    """Entry n of the remaster's system/ffto/g2d.dat, decoded as the game has it (and as the mod's tex_N.bin gives it):
    an entry is "YOX", 2, its size decoded, 0, then zlib."""
    with open(path, "rb") as f:
        f.seek(G2D_TABLE + 16 * n)
        offset, size = struct.unpack("<II", f.read(8))
        f.seek(offset)
        entry = f.read(size)
    if entry[:8] != b"YOX\0\2\0\0\0":
        raise ValueError(f"g2d entry {n} isn't a YOX texture")
    data = zlib.decompress(entry[16:])
    if len(data) != struct.unpack_from("<I", entry, 8)[0]:
        raise ValueError(f"g2d entry {n} decodes to {len(data)} bytes, not its {struct.unpack_from('<I', entry, 8)[0]}")
    return data


# ------------------------------------------------------------------ portraits (the remaster's faces)
#
# A sheet's portrait is 32 x 48 pixels on its side in the drawn sheet's last rows (456-487, x 80-127), its top at the
# right; its palettes are 8-15, the portrait palette of unit palette k being 8 + k. The remaster's face for a graphic
# is a texture of 520 x 388 (ui/ffto/common/face/texture/wldface_<face>_<palette>_uitx.tex) whose parts (the
# .utexpt beside it) are cells of 128 x 192: three eye frames, four mouth frames and the whole face ("All"), each the
# portrait redrawn at twice its size (64 x 96) and shown doubled. Scale2x then doubling is the same step for ReMixed's
# portraits: scored against the remaster's faces of 23 portraits ReMixed kept (generics, monsters, Delita, Malak),
# 91.9% of their pixels within 32 of the remaster's colour (91.7% doubled, 90.8% Scale2x twice, 92.6% bicubic), the
# outline 99.3% the same (99.1%, 99.1%, 96.5%), and at the redraw's own 64 x 96 the same as doubling (82.2% of the
# pixels the palette colour the remaster's is nearest), without bicubic's blur and new colours.

PORTRAIT_X, PORTRAIT_ROW = 80, ROWS - PORTRAIT_ROWS  # the portrait on its side: 48 wide, 32 high
FACE_WIDTH, FACE_HEIGHT = 32, 48  # upright
FACE_SCALE = 4  # Scale2x, then doubled
FACE_PALETTES = range(8, 16)


def portrait(pixels: bytes | bytearray) -> bytearray:
    """A drawn sheet's portrait upright (32 x 48 palette indices; 0 is clear)."""
    out = bytearray(FACE_WIDTH * FACE_HEIGHT)
    for y in range(FACE_HEIGHT):
        for x in range(FACE_WIDTH):
            out[y * FACE_WIDTH + x] = pixels[(PORTRAIT_ROW + x) * WIDTH + PORTRAIT_X + FACE_HEIGHT - 1 - y]
    return out


def face_palettes(palettes: list[tuple[int, ...]]) -> dict[int, tuple[int, ...]]:
    """The palette of each of a face's files (8-15): a sheet's portrait palette 8 + k where it has unit palette k
    and its own colours there (not empty, nor a copy of a unit palette, as some of ReMixed's sheets fill the unused
    ones), else palette 8."""
    units = {palettes[k][1:] for k in range(8)}
    out = {}
    for p in FACE_PALETTES:
        own = palettes[p]
        real = any(palettes[p - 8][1:]) and any(own[1:]) and own[1:] not in units
        out[p] = own if real else palettes[8]
    return out


def texture_parts(utexpt: bytes) -> tuple[str, dict[str, tuple[int, int, int, int]]]:
    """A texture-parts file (.utexpt): its texture's path and its parts by name, (x0, y0, x1, y1). The file: the path
    (an offset from +0), where the parts start (+4) and their count (+8); each part 20 bytes, its name (an offset from
    where it's stored) and its corners; the first part, "TexturePart", is the whole texture."""
    def text(at: int) -> str:
        return utexpt[at:utexpt.index(b"\0", at)].decode("ascii")
    path_at, parts_at, count = struct.unpack_from("<3I", utexpt, 0)
    parts = {}
    for k in range(count):
        at = parts_at + 20 * k
        name_at, *corners = struct.unpack_from("<5I", utexpt, at)
        parts[text(at + name_at)] = tuple(corners)
    return text(path_at), parts


def face_atlas(spr: bytes, parts: dict[str, tuple[int, int, int, int]]) -> dict[int, bytes] | None:
    """A sheet's portrait as the remaster's face textures, by palette (8-15): RGBA, the size of the parts' whole
    texture, the portrait at four times its size (Scale2x, then doubled) in every part (so eyes and mouth stay as they
    are: ReMixed's portraits don't move). None for a sheet with no portrait."""
    palettes, pixels = decode(spr)
    face = portrait(pixels)
    if not any(face):
        return None
    big = scale2x(face, FACE_WIDTH, FACE_HEIGHT)
    w2 = 2 * FACE_WIDTH
    width, height = FACE_SCALE * FACE_WIDTH, FACE_SCALE * FACE_HEIGHT
    cells = [corners for name, corners in parts.items() if name != "TexturePart"]
    _, _, atlas_w, atlas_h = parts["TexturePart"]
    if any((x1 - x0, y1 - y0) != (width, height) for x0, y0, x1, y1 in cells):
        raise ValueError(f"the face's parts aren't {width} x {height}: {parts}")
    out = {}
    for p, palette in face_palettes(palettes).items():
        # Index 0 is clear, as is colour 0 (the PS1 draws neither).
        colours = [b"\0\0\0\0"] + [bytes((*rgb(c), 255)) if c else b"\0\0\0\0" for c in palette[1:]]
        rows = []
        for y in range(height):
            src = big[(y // 2) * w2:(y // 2 + 1) * w2]
            rows.append(b"".join(colours[i] * 2 for i in src))
        rgba = bytearray(4 * atlas_w * atlas_h)
        for x0, y0, _, _ in cells:
            for y, row in enumerate(rows):
                at = 4 * ((y0 + y) * atlas_w + x0)
                rgba[at:at + len(row)] = row
        out[p] = bytes(rgba)
    return out


# ------------------------------------------------------------------ previews (PNG, standard library only)

def rgb(colour: int) -> tuple[int, int, int]:
    r, g, b = colour & 31, colour >> 5 & 31, colour >> 10 & 31
    return r << 3 | r >> 2, g << 3 | g >> 2, b << 3 | b >> 2


def write_png(path: Path, pixels: bytes | bytearray, width: int, height: int, palette: tuple[int, ...],
              background: tuple[int, int, int] = (48, 48, 64)) -> None:
    colours = [background] + [rgb(c) for c in palette[1:]]
    rows = bytearray()
    for y in range(height):
        rows.append(0)
        for i in pixels[y * width:(y + 1) * width]:
            rows += bytes(colours[i])

    def chunk(kind: bytes, body: bytes) -> bytes:
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body))

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(bytes(rows), 6)) + chunk(b"IEND", b""))


def write_rgba_png(path: Path, rgba: bytes, width: int, height: int) -> None:
    """An RGBA picture (4 bytes a pixel, rows top down) as a PNG."""
    def chunk(kind: bytes, body: bytes) -> bytes:
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body))

    stride = 4 * width
    rows = b"".join(b"\0" + rgba[y * stride:(y + 1) * stride] for y in range(height))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(rows, 6)) + chunk(b"IEND", b""))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("preview", help="ReMixed's sheets doubled beside their Scale2x textures, as PNGs")
    p.add_argument("graphics", nargs="+", help="graphics in hex (ReMixed's BATTLE/XX.SPR)")
    p.add_argument("--palette", type=int, default=0)
    args = parser.parse_args()

    from psxdisc import Disc
    from remixed import WORK, load_config, remixed_bin

    cfg = load_config()
    with Disc(remixed_bin(cfg)) as disc:
        sheets = {int(g, 16): disc.read(disc.find(f"BATTLE/{int(g, 16):02X}.SPR")) for g in args.graphics}
    out = WORK / "sprites"
    for g, spr in sheets.items():
        palettes, pixels = decode(spr)
        doubled = bytearray()
        for y in range(ROWS):
            row = bytearray()
            for i in pixels[y * WIDTH:(y + 1) * WIDTH]:
                row += bytes((i, i))
            doubled += row + row
        top, bottom = textures(spr)
        filtered = unpack4(top) + unpack4(bottom)
        gap, w = 16, 2 * WIDTH
        both = bytearray()
        for y in range(2 * ROWS):
            both += doubled[y * w:(y + 1) * w] + bytes(gap) + filtered[y * w:(y + 1) * w]
        path = out / f"{g:02X}.png"
        write_png(path, both, 2 * w + gap, 2 * ROWS, palettes[args.palette])
        print(f"{path}: ReMixed's {g:02X}.SPR doubled (left) and its texture (right)")


if __name__ == "__main__":
    main()
