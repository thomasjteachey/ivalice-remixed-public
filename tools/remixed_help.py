"""Read FFT's PS1 help texts (EVENT/HELP.LZW, ATCHELP.LZW): ability, job, skillset and item descriptions.

A help file starts with 32 u32 offsets, one per section, from 0x80; a section is entries ended by FE. HELP.LZW is
compressed with FFT's back-references: F0-F3 b1 b2 copies (((b0 & 3) << 3 | b1 >> 5) + 4) bytes of text from
((b1 & 0x1F) * 254 + b2) bytes before the code, in the file as stored (base 254: FE and FF never appear there),
expanded in turn. Checked against vanilla, whose ATCHELP.LZW holds the same text uncompressed in four sections.

ReMixed's text adds two things: two-letter codes 0x56-0x96 (SCUS 0x800320E0: a pair of character codes per code
from 0x56, 122 of them; ReMixed's text routine prints a nonzero pair as two letters, a zero one as the font's glyph;
docs/analysis/scus-in-place-review.md) and its own font (EVENT/FONT.BIN), where 0x8D and 0x8E are "(" and ")"
("Range:3 (Cone)"; vanilla writes parentheses as D9 BE and D9 BF).
"""

from __future__ import annotations

import fft_text

SECTIONS_AT, SECTION_COUNT = 0, 32
DATA_AT = 0x80
END = 0xFE
LINE_BREAK = 0xF8
CODES = (0xF0, 0xF1, 0xF2, 0xF3)
TEXT_CODES_WITH_A_BYTE = (0xE2, 0xE3, 0xE6, 0xE8, 0xF5, 0xF6)
DIGRAPHS_AT, DIGRAPHS_FIRST, DIGRAPHS_COUNT = 0x800320E0, 0x56, 122  # in ReMixed's SCUS

# Sections by what they hold (both files; HELP's are the menus' text, ATCHELP's the battle's).
ABILITIES, JOBS, ITEMS, SKILLSETS = 15, 12, 13, 19

# Two-byte characters (D0-DA xx) the help texts use, as text.
PAIRS = {
    b"\xd9\xb6": ".", b"\xd9\xb7": "&", b"\xd9\xb8": "%", b"\xd9\xbc": "/", b"\xd9\xbd": ":", b"\xd9\xbe": "(",
    b"\xd9\xbf": ")", b"\xd9\xc0": '"', b"\xd9\xc1": "'", b"\xd9\xc6": "?", b"\xda\x73": " ", b"\xda\x74": ",",
    b"\xda\x75": ";", b"\xda\x76": "'", b"\xd1\x1d": "-", b"\xd1\x17": "—", b"\xd1\x29": "*",
}
SINGLE = {0x91: '"', 0x93: "'"}
REMIXED_FONT = {0x8D: "(", 0x8E: ")"}


def expand(data: bytes, start: int, end: int | None = None, want: int | None = None) -> bytes:
    """The text from data[start:end] (or until want bytes of it), each back-reference expanded."""
    end = len(data) if end is None else end
    out, at = bytearray(), start
    while at < end and (want is None or len(out) < want):
        b = data[at]
        if b in CODES:
            length = ((b & 3) << 3 | data[at + 1] >> 5) + 4
            source = at - ((data[at + 1] & 0x1F) * 254 + data[at + 2])
            out += expand(data, source, at, length)
            at += 3
            continue
        out.append(b)
        at += 1
    return bytes(out if want is None else out[:want])


def entries(data: bytes, section: int) -> list[bytes]:
    """A section's entries (each without its FE), expanded."""
    offsets = [int.from_bytes(data[SECTIONS_AT + 4 * k:SECTIONS_AT + 4 * k + 4], "little")
               for k in range(SECTION_COUNT)]
    if section and not offsets[section]:
        return []
    ends = sorted(set(offsets + [len(data) - DATA_AT]))
    start = offsets[section]
    stop = next(o for o in ends if o > start)
    return expand(data, DATA_AT + start, DATA_AT + stop).split(bytes([END]))[:-1]


def digraphs(scus: bytes, load_address: int) -> dict[int, tuple[int, int]]:
    """ReMixed's two-letter codes from its SCUS (loaded at load_address): code -> its two character codes."""
    at = DIGRAPHS_AT - load_address
    pairs = {}
    for k in range(DIGRAPHS_COUNT):
        first, second = scus[at + 2 * k], scus[at + 2 * k + 1]
        if first or second:
            pairs[DIGRAPHS_FIRST + k] = (first, second)
    return pairs


def text(entry: bytes, two_letters: dict[int, tuple[int, int]] | None = None) -> str:
    """An entry as text, with "\\n" for its line breaks and "<xx>" for a byte it doesn't know. With two_letters (ReMixed's
    codes, from digraphs) it reads ReMixed's text: those codes and its font's parentheses."""
    out, at = [], 0
    while at < len(entry):
        b = entry[at]
        pair = entry[at:at + 2]
        if pair in PAIRS:
            out.append(PAIRS[pair])
            at += 2
        elif 0xD0 <= b <= 0xDA:
            out.append(f"<{pair.hex()}>")
            at += 2
        elif b in TEXT_CODES_WITH_A_BYTE:
            at += 2  # colours and the like
        elif b == LINE_BREAK:
            out.append("\n")
            at += 1
        elif two_letters is not None and b in two_letters:
            out.extend(fft_text.CHARS.get(c, SINGLE.get(c, f"<{c:02X}>")) for c in two_letters[b])
            at += 1
        elif two_letters is not None and b in REMIXED_FONT:
            out.append(REMIXED_FONT[b])
            at += 1
        else:
            out.append(fft_text.CHARS.get(b) or SINGLE.get(b) or f"<{b:02X}>")
            at += 1
    return "".join(out)
