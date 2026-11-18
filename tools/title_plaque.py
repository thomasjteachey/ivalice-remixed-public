"""The title screen's plaque: "IVALICE REMIXED <version>" under the enhanced logo, written by every build from
tools/build_mod.py's RELEASE_VERSION, so the title always shows the release it's from (asked 2026-11-18: it showed
0.9.2 through 0.9.13, painted into the picture by hand).

The build (no Pillow, so `python -I` works) takes the logo with its plaque left blank and the plaque's lettering
pre-drawn a character at a time, both in data/remaster/title/, lays the line out as the hand-made plaque had it and
draws it on:

    from title_plaque import draw_title
    draw_title("IVALICE REMIXED 0.9.13", out_png)

Those two pictures are made once, from the marked logo, by this tool's `make` (it needs Pillow, so no -I, and
Windows' Sylfaen, the face closest to the plaque's lettering: letter widths within a pixel at 26 px, drawn a pixel
heavier):

    python tools/title_plaque.py make SOURCE.png

SOURCE is the 1920 x 840 logo with its plaque lettered (git show
08ba4d8:data/remaster/title/ui_logo_fft_enhanced_uitx.png, "IVALICE REMIXED 0.9.2"). `make` takes the lettering off
(each row of the plaque's middle drawn again between its clean columns: beside the ornaments and in the two gaps
between the words), and draws each character as the plaque's were: cream at the top to gold at the bottom, with a
one-pixel dark edge. Both pictures are kept unfiltered (filter 0 on every row), which is all `read_png` reads.
"""

from __future__ import annotations

import json
import struct
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TITLE_DIR = ROOT / "data" / "remaster" / "title"
BASE = TITLE_DIR / "ui_logo_fft_enhanced_uitx.png"  # the enhanced logo, its plaque blank
GLYPHS = TITLE_DIR / "plaque-glyphs.png"             # the lettering, a character at a time
LAYOUT = TITLE_DIR / "plaque-glyphs.json"            # where each character is in it, and the plaque's measures

WIDTH, HEIGHT = 1920, 840
# The hand-made plaque, measured on the marked logo: the letters' cap height 18 px from row 792, the plaque's middle
# at x 944 (between its ornaments, 701 and 1188), room for the line from x 728 to 1157. The space between letters'
# ink: 7 px, 5 next to a digit, a dot or a dash, 2 in V A, and 20 between words.
CAP_TOP, CENTRE, LEFT, RIGHT = 792, 944, 728, 1157
GAP, NUMBER_GAP, WORD_GAP, MIN_GAP = 7, 5, 20, 2
KERNED = {"AV": 2, "VA": 2}
NUMBERS = set("0123456789.-")
CHARACTERS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-"
# Taking the lettering off: the plaque's middle (x, y, past x, past y) and the clean columns each row is drawn
# again between.
ERASE = (743, 787, 1156, 815)
CLEAN = [(740, 743), (889, 898), (1069, 1078), (1156, 1159)]
# The lettering's look: font, size, the rows a character's picture covers (from 3 above the cap height), its colours.
FONT, SIZE, TOP, ROWS = "C:/Windows/Fonts/sylfaen.ttf", 26, CAP_TOP - 3, 24
BOLD = 1.0  # a share of each character drawn again a pixel to the right, for the plaque's heavier strokes
FILL_TOP, FILL_BOTTOM, EDGE, EDGE_ALPHA = (246, 240, 212), (214, 190, 132), (8, 6, 3), 235
INK = 96  # coverage from which a column counts as a letter's ink (as the plaque's were measured)


# ---------------------------------------------------------------- unfiltered PNGs (no Pillow)

def read_png(path: Path) -> tuple[int, int, bytearray]:
    """Width, height and RGBA rows of an 8-bit RGBA, non-interlaced PNG whose rows are all unfiltered."""
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path} isn't a PNG")
    at, idat, header = 8, bytearray(), None
    while at < len(data):
        length, kind = struct.unpack(">I4s", data[at:at + 8])
        body = data[at + 8:at + 8 + length]
        if kind == b"IHDR":
            header = struct.unpack(">IIBBBBB", body)
        elif kind == b"IDAT":
            idat += body
        elif kind == b"IEND":
            break
        at += 12 + length
    if header is None or header[2:] != (8, 6, 0, 0, 0):
        raise ValueError(f"{path} isn't an 8-bit RGBA, non-interlaced PNG")
    width, height = header[:2]
    raw = zlib.decompress(bytes(idat))
    stride = width * 4 + 1
    if len(raw) != stride * height or any(raw[y * stride] for y in range(height)):
        raise ValueError(f"{path} has filtered rows; make it again with `python tools/title_plaque.py make`")
    pixels = bytearray(width * height * 4)
    for y in range(height):
        pixels[y * width * 4:(y + 1) * width * 4] = raw[y * stride + 1:(y + 1) * stride]
    return width, height, pixels


def write_png(path: Path, width: int, height: int, pixels: bytes | bytearray) -> None:
    """An 8-bit RGBA PNG, every row unfiltered."""
    row = width * 4
    raw = b"".join(b"\x00" + bytes(pixels[y * row:(y + 1) * row]) for y in range(height))

    def chunk(kind: bytes, body: bytes) -> bytes:
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body))
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


# ---------------------------------------------------------------- the line

def gap(before: str, after: str) -> int:
    """The space between two characters' ink, as the plaque had it."""
    if before + after in KERNED:
        return KERNED[before + after]
    return NUMBER_GAP if before in NUMBERS or after in NUMBERS else GAP


def layout(label: str, glyphs: dict) -> list[tuple[str, int]]:
    """Each character of label with the x its ink starts at, the line centred on the plaque. Its gaps close up
    (down to MIN_GAP) for a line too long for the plaque at the plaque's spacing; one longer still is an error."""
    words = label.upper().split()
    missing = sorted({c for w in words for c in w} - set(glyphs))
    if missing:
        raise ValueError(f"the plaque has no {' '.join(missing)} (only {CHARACTERS})")
    ink = sum(glyphs[c]["ink"] for w in words for c in w)
    gaps = []
    for n, word in enumerate(words):
        for k, c in enumerate(word):
            if k:
                gaps.append(gap(word[k - 1], c))
            elif n:
                gaps.append(WORD_GAP)
    room = RIGHT - LEFT - ink
    spaced = sum(gaps)
    if spaced > room:
        floor = MIN_GAP * len(gaps)
        if room < floor:
            raise ValueError(f"{label!r} is too long for the title's plaque ({ink + floor} px; it has {RIGHT - LEFT})")
        gaps = [MIN_GAP + (g - MIN_GAP) * (room - floor) // max(spaced - floor, 1) for g in gaps]
    x = CENTRE - (ink + sum(gaps)) // 2
    placed, k = [], 0
    for c in "".join(words):
        if placed:
            x += gaps[k]
            k += 1
        placed.append((c, x))
        x += glyphs[c]["ink"]
    return placed


def draw(pixels: bytearray, width: int, label: str, atlas: tuple[int, int, bytearray], glyphs: dict) -> None:
    """label drawn on the plaque of pixels (RGBA rows width wide), each character from atlas over what's there."""
    atlas_width, _, atlas_pixels = atlas
    for c, x in layout(label, glyphs):
        g = glyphs[c]
        left = x - g["left"]
        for row in range(ROWS):
            src = (row * atlas_width + g["x"]) * 4
            dst = ((TOP + row) * width + left) * 4
            for k in range(g["w"]):
                r, gr, b, a = atlas_pixels[src + 4 * k:src + 4 * k + 4]
                if not a:
                    continue
                at = dst + 4 * k
                keep = 255 - a
                pixels[at] = (r * a + pixels[at] * keep + 127) // 255
                pixels[at + 1] = (gr * a + pixels[at + 1] * keep + 127) // 255
                pixels[at + 2] = (b * a + pixels[at + 2] * keep + 127) // 255
                pixels[at + 3] = a + (pixels[at + 3] * keep + 127) // 255


def draw_title(label: str, out: Path) -> None:
    """The enhanced logo with label on its plaque, written to out."""
    width, height, pixels = read_png(BASE)
    if (width, height) != (WIDTH, HEIGHT):
        raise ValueError(f"{BASE} is {width} x {height}, not {WIDTH} x {HEIGHT}")
    glyphs = json.loads(LAYOUT.read_text(encoding="utf-8"))["glyphs"]
    draw(pixels, width, label, read_png(GLYPHS), glyphs)
    write_png(out, width, height, pixels)


# ---------------------------------------------------------------- making the pictures (Pillow)

def make(source: Path) -> None:
    from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

    logo = Image.open(source).convert("RGBA")
    if logo.size != (WIDTH, HEIGHT):
        sys.exit(f"{source} is {logo.size[0]} x {logo.size[1]}, not {WIDTH} x {HEIGHT}")
    p = logo.load()
    for y in range(ERASE[1], ERASE[3]):
        keys = []
        for a, b in CLEAN:
            cols = [p[x, y] for x in range(a, b)]
            keys.append(((a + b - 1) / 2, [sum(c[k] for c in cols) / len(cols) for k in range(4)]))
        for x in range(ERASE[0], ERASE[2]):
            for (xa, ca), (xb, cb) in zip(keys, keys[1:]):
                if xa <= x <= xb:
                    t = (x - xa) / (xb - xa)
                    p[x, y] = tuple(round(ca[k] + (cb[k] - ca[k]) * t) for k in range(4))
                    break
    write_png(BASE, WIDTH, HEIGHT, logo.tobytes())

    font = ImageFont.truetype(FONT, SIZE)
    cap_top = font.getbbox("E")[1]
    pictures, glyphs, x = [], {}, 0
    for c in CHARACTERS:
        box = font.getbbox(c)
        w = box[2] - box[0] + 6
        mask = Image.new("L", (w, ROWS), 0)
        ImageDraw.Draw(mask).text((3 - box[0], CAP_TOP - TOP - cap_top), c, font=font, fill=255)
        if BOLD:
            mask = ImageChops.lighter(mask, ImageChops.offset(mask, 1, 0).point(lambda v: round(v * BOLD)))
        edge = Image.new("RGBA", mask.size, EDGE + (0,))
        edge.putalpha(mask.filter(ImageFilter.MaxFilter(3)).point(lambda v: v * EDGE_ALPHA // 255))
        fill = Image.new("RGBA", mask.size)
        for row in range(ROWS):
            t = min(max((TOP + row - CAP_TOP) / 17, 0), 1)
            colour = tuple(round(FILL_TOP[k] + (FILL_BOTTOM[k] - FILL_TOP[k]) * t) for k in range(3))
            fill.paste(colour + (255,), (0, row, w, row + 1))
        fill.putalpha(mask)
        picture = Image.alpha_composite(edge, fill)
        columns = [k for k in range(w) if max(mask.getpixel((k, r)) for r in range(ROWS)) >= INK]
        glyphs[c] = {"x": x, "w": w, "left": columns[0], "ink": columns[-1] - columns[0] + 1}
        pictures.append(picture)
        x += w
    atlas = Image.new("RGBA", (x, ROWS))
    for picture, c in zip(pictures, CHARACTERS):
        atlas.paste(picture, (glyphs[c]["x"], 0))
    write_png(GLYPHS, atlas.width, ROWS, atlas.tobytes())
    LAYOUT.write_text(json.dumps({"font": Path(FONT).name, "size": SIZE, "glyphs": glyphs}, indent=1) + "\n",
                      encoding="utf-8")
    print(f"wrote {BASE.relative_to(ROOT)}, {GLYPHS.relative_to(ROOT)} ({len(CHARACTERS)} characters) "
          f"and {LAYOUT.relative_to(ROOT)}")


def main() -> None:
    if len(sys.argv) == 3 and sys.argv[1] == "make":
        make(Path(sys.argv[2]))
    elif len(sys.argv) == 4 and sys.argv[1] == "draw":
        draw_title(sys.argv[2], Path(sys.argv[3]))
    else:
        sys.exit("usage: title_plaque.py make SOURCE.png | draw LABEL OUT.png")


if __name__ == "__main__":
    main()
