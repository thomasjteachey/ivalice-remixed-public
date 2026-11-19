"""The Dark Knight's job icons, drawn from ReMixed's own Dark Knight sheet: the job menu's (60 x 60, the remaster's
ui/ffto/icon/job/texture/j_NNN) and its command's (96 x 96, job_comm/texture/jc_NNN).

The remaster's job icons are an emblem in one colour on a framed tile: a cream emblem on a dark brown field with a
light rim for the job menu, a gold one on a red field with a gold rim for the command. ReMixed's Dark Knight (its Bard
slot's sheet, 82.SPR, the man's) has no emblem of its own, so the crested helm of its front-facing frame becomes one:
its shape, Scale2x three times, smoothed and averaged down to the icon's size, flat in the emblem colour, with the
sheet's dark plate and gold trim inside it drawn as the emblems' darker lines. The tiles are drawn here from the
colours of the remaster's own (sampled from its Mime's, j_020 and jc_020), not copied from them. No Pillow, so
`python -I` works:

    python -I tools/job_icons.py preview      # work/sprites/dark-knight-icons.png, both icons at four times the size
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from sprites import WIDTH, decode, rgb, scale2x, write_rgba_png  # noqa: E402

SHEET = 0x82  # ReMixed's Dark Knight (its Bard slot's man's sheet)
HELMET = (72, 2, 90, 13)  # the front-facing frame's helmet: x0, y0, x1, y1 (its crest, trim and visor)
HELMET_LINES = frozenset({14, 15})  # its gold trim, drawn as the emblem's lines with the darks
HELMET_STRETCH = 1.35  # drawn taller than the sheet's 18 x 11, so it reads as a helm and not a bag

# The two tiles: size, the transparent margin round them, the corner radius, the rim's colours (top, bottom) and
# width, a line inside the rim, the field's colours (top, bottom), the emblem's colours (light, dark) and its size.
MENU = {"size": 60, "margin": 5, "radius": 9, "rim": ((228, 214, 200), (176, 154, 128)), "rim_width": 3,
        "line": (40, 28, 14), "field": ((86, 70, 50), (52, 40, 26)), "emblem": ((226, 212, 192), (150, 128, 100)),
        "emblem_size": 34, "shadow": (20, 12, 4)}
COMMAND = {"size": 96, "margin": 8, "radius": 4, "rim": ((232, 214, 150), (150, 100, 30)), "rim_width": 5,
           "line": (90, 26, 18), "field": ((140, 40, 38), (84, 18, 10)), "emblem": ((236, 214, 140), (170, 126, 50)),
           "emblem_size": 54, "shadow": (56, 16, 6), "inner_rim": (196, 150, 84)}


def _mix(a: tuple[int, ...], b: tuple[int, ...], t: float) -> tuple[int, ...]:
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def _rounded_inside(x: float, y: float, x0: float, y0: float, x1: float, y1: float, r: float) -> float:
    """How far (x, y) is inside the rounded rectangle, in pixels (negative outside)."""
    cx = min(max(x, x0 + r), x1 - r)
    cy = min(max(y, y0 + r), y1 - r)
    if (x0 + r <= x <= x1 - r) or (y0 + r <= y <= y1 - r):
        return min(x - x0, x1 - x, y - y0, y1 - y)
    return r - math.hypot(x - cx, y - cy)


UP = 8  # the sheet's pixels this many times over (Scale2x three times) before the shapes are smoothed
DARK = 0.3  # a sheet colour this dark (luminance 0-1) inside the shape is one of the emblem's lines
PAD = 2  # empty sheet pixels round the frame, so smoothing has room


def _blur(values: list[float], w: int, h: int, r: int) -> list[float]:
    """A box blur of radius r, along rows then columns."""
    out = values[:]
    for horizontal in (True, False):
        src, out = out, [0.0] * (w * h)
        for a in range(h if horizontal else w):
            line = [src[a * w + b] if horizontal else src[b * w + a] for b in range(w if horizontal else h)]
            n = len(line)
            prefix = [0.0]
            for v in line:
                prefix.append(prefix[-1] + v)
            for b in range(n):
                lo, hi = max(0, b - r), min(n, b + r + 1)
                v = (prefix[hi] - prefix[lo]) / (hi - lo)
                if horizontal:
                    out[a * w + b] = v
                else:
                    out[b * w + a] = v
    return out


def _smooth_edge(values: list[float], softness: float) -> list[float]:
    """A blurred shape cut at its half-way line, `softness` wide either side (in blurred units)."""
    return [min(1.0, max(0.0, (v - 0.5) / (2 * softness) + 0.5)) for v in values]


def helmet_shapes(spr: bytes, frame: tuple[int, int, int, int] = HELMET, dark_below: float = DARK,
                  line_indices: frozenset[int] = frozenset()) -> tuple[int, int, list[float], list[float]]:
    """The helmet at UP times the sheet's size, smoothed: (width, height, its shape 0-1, its lines 0-1). The lines
    are the sheet's colours darker than `dark_below` inside the shape (not its outer edge, which is the shape's own),
    and any of `line_indices`."""
    palettes, pixels = decode(spr)
    palette = palettes[0]
    x0, y0, x1, y1 = frame
    w, h = x1 - x0 + 2 * PAD, y1 - y0 + 2 * PAD
    crop = bytearray(w * h)
    for y in range(y1 - y0):
        for x in range(x1 - x0):
            crop[(y + PAD) * w + x + PAD] = pixels[(y0 + y) * WIDTH + x0 + x]
    def inside(x: int, y: int) -> bool:
        return 0 <= x < w and 0 <= y < h and crop[y * w + x] != 0
    shape = bytearray(1 if i else 0 for i in crop)
    lines = bytearray(w * h)
    for y in range(h):
        for x in range(w):
            i = crop[y * w + x]
            if not i:
                continue
            r, g, b = rgb(palette[i])
            dark = (0.3 * r + 0.59 * g + 0.11 * b) / 255 < dark_below or i in line_indices
            edge = not all(inside(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            lines[y * w + x] = 1 if dark and not edge else 0
    for _ in range(3):  # Scale2x three times: UP = 8
        shape, lines = scale2x(shape, w, h), scale2x(lines, w, h)
        w, h = 2 * w, 2 * h
    shape_f = _smooth_edge(_blur([float(v) for v in shape], w, h, UP // 2), 0.08)
    lines_f = _smooth_edge(_blur([float(v) for v in lines], w, h, UP // 4), 0.12)
    return w, h, shape_f, lines_f


def emblem(spr: bytes, size: int, frame: tuple[int, int, int, int] = HELMET, dark_below: float = DARK,
           line_indices: frozenset[int] = frozenset(), stretch: float = 1.0
           ) -> tuple[int, int, list[float], list[float]]:
    """The helmet averaged down to fit a square of `size`: (width, height, alpha, lines), its padding trimmed, and
    `stretch` times as tall for its width as the sheet draws it."""
    W, H, shape, lines = helmet_shapes(spr, frame, dark_below, line_indices)
    xs = [x for y in range(H) for x in range(W) if shape[y * W + x] > 0.02]
    ys = [y for y in range(H) for x in range(W) if shape[y * W + x] > 0.02]
    bx0, bx1, by0, by1 = min(xs), max(xs) + 1, min(ys), max(ys) + 1
    bw, bh = bx1 - bx0, by1 - by0
    scale = size / max(bw, bh * stretch)
    sx_scale, sy_scale = scale, scale * stretch
    w, h = max(1, round(bw * sx_scale)), max(1, round(bh * sy_scale))
    alpha, line = [], []
    for y in range(h):
        for x in range(w):
            sx0, sy0 = bx0 + int(x / sx_scale), by0 + int(y / sy_scale)
            sx1 = min(bx1, max(sx0 + 1, bx0 + int((x + 1) / sx_scale)))
            sy1 = min(by1, max(sy0 + 1, by0 + int((y + 1) / sy_scale)))
            n = a = l = 0.0
            for sy in range(sy0, sy1):
                for sx in range(sx0, sx1):
                    n += 1
                    a += shape[sy * W + sx]
                    l += lines[sy * W + sx]
            alpha.append(a / n if n else 0.0)
            line.append(min(1.0, l / a) if a else 0.0)
    return w, h, alpha, line


def tile(spec: dict, spr: bytes) -> bytes:
    """One icon as RGBA."""
    size, margin, radius = spec["size"], spec["margin"], spec["radius"]
    x0, y0, x1, y1 = margin, margin, size - margin, size - margin
    out = bytearray(size * size * 4)
    for y in range(size):
        t = (y - y0) / max(1, y1 - y0)
        rim = _mix(*spec["rim"], min(max(t, 0), 1))
        field = _mix(*spec["field"], min(max(t, 0), 1))
        for x in range(size):
            d = _rounded_inside(x + 0.5, y + 0.5, x0, y0, x1, y1, radius)
            at = 4 * (y * size + x)
            if d <= -1.5:
                continue
            if d <= 0:  # a soft dark edge just outside the rim
                out[at:at + 4] = bytes((*spec["shadow"], round(140 * (1 + d / 1.5))))
                continue
            if d < spec["rim_width"]:
                colour = rim
            elif d < spec["rim_width"] + 1:
                colour = spec["line"]
            elif "inner_rim" in spec and d < spec["rim_width"] + 2:
                colour = spec["inner_rim"]
            elif "inner_rim" in spec and d < spec["rim_width"] + 3:
                colour = spec["line"]
            else:
                colour = field
            edge = min(1.0, d)  # the outermost pixel antialiased
            out[at:at + 4] = bytes((*colour, round(255 * edge)))
    # The emblem, centred, flat in its light colour with its lines in the field's dark, over a one-pixel shadow down
    # and right, as the remaster's emblems are.
    w, h, alpha, line = emblem(spr, spec["emblem_size"], spec.get("frame", HELMET), spec.get("dark_below", DARK),
                               spec.get("line_indices", HELMET_LINES), spec.get("stretch", HELMET_STRETCH))
    ex, ey = (size - w) // 2, (size - h) // 2
    light, _ = spec["emblem"]
    for dy, dx, kind in ((1, 1, "shadow"), (0, 0, "emblem")):
        for y in range(h):
            for x in range(w):
                a = alpha[y * w + x]
                if a <= 0:
                    continue
                at = 4 * ((ey + y + dy) * size + ex + x + dx)
                base = out[at:at + 3]
                if kind == "shadow":
                    colour, a = spec["shadow"], a * 0.7
                else:
                    t = (ey + y) / size  # a little darker toward the bottom, like the remaster's
                    colour = _mix(_mix(light, spec["emblem"][1], 0.25 * t), spec["field"][1], 0.85 * line[y * w + x])
                out[at:at + 3] = bytes(_mix(tuple(base), colour, a))
    return bytes(out)


def dark_knight_icons(spr: bytes) -> dict[str, tuple[int, bytes]]:
    """Both icons from ReMixed's Dark Knight sheet: {"j": (60, RGBA), "jc": (96, RGBA)}."""
    return {"j": (MENU["size"], tile(MENU, spr)), "jc": (COMMAND["size"], tile(COMMAND, spr))}


HEADS = [(229, 11, 247, 24), (229, 27, 247, 40), (229, 43, 246, 56)]  # the sheet's three head frames, for `preview all`


def main() -> None:
    if sys.argv[1:2] != ["preview"]:
        sys.exit(__doc__)
    from psxdisc import Disc
    from remixed import WORK, load_config, remixed_bin

    cfg = load_config()
    with Disc(remixed_bin(cfg)) as disc:
        spr = disc.read(disc.find(f"BATTLE/{SHEET:02X}.SPR"))
    if sys.argv[2:] == ["all"]:
        icons = {f"{k}{n}": (spec["size"], tile({**spec, "frame": frame}, spr))
                 for n, frame in enumerate(HEADS) for k, spec in (("j", MENU), ("jc", COMMAND))}
    else:
        icons = dark_knight_icons(spr)
    scale, gap = 4, 16
    width = sum(s * scale for s, _ in icons.values()) + gap * (len(icons) + 1)
    height = max(s for s, _ in icons.values()) * scale + 2 * gap
    canvas = bytearray(bytes((60, 60, 70, 255)) * width * height)
    x = gap
    for size, rgba in icons.values():
        for y in range(size * scale):
            for xx in range(size * scale):
                at = 4 * ((y // scale) * size + xx // scale)
                a = rgba[at + 3] / 255
                dst = 4 * ((gap + y) * width + x + xx)
                for c in range(3):
                    canvas[dst + c] = round(canvas[dst + c] * (1 - a) + rgba[at + c] * a)
        x += size * scale + gap
    path = WORK / "sprites" / "dark-knight-icons.png"
    path.parent.mkdir(parents=True, exist_ok=True)
    write_rgba_png(path, bytes(canvas), width, height)
    print(path)


if __name__ == "__main__":
    main()
