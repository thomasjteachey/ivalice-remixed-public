"""Read FFT's PS1 name lists (abilities, jobs, skillsets) from the vanilla and ReMixed discs.

Only what the port needs: decoding names in FFT's PS1 text encoding, and
finding a name list by the names it starts with. ReMixed (from The Lion War)
moved and rewrote these lists, so they're found by content, not by offset.
"""

from __future__ import annotations

import re

END = 0xFE  # ends each entry in a name list
CHARS: dict[int, str] = {i: str(i) for i in range(10)}
CHARS.update({0x0A + i: chr(65 + i) for i in range(26)})
CHARS.update({0x24 + i: chr(97 + i) for i in range(26)})
CHARS.update({0xFA: " ", 0x3E: "!", 0x40: "?", 0x42: "+", 0x44: "/", 0x46: ":", 0x5F: ".", 0x8D: "-", 0x93: "'"})
TWO_BYTE: dict[bytes, str] = {b"\xda\x76": "'", b"\xd9\xb6": ".", b"\xd9\xc1": ",", b"\xd1\x1d": "-"}


def encode(text: str) -> bytes:
    reverse = {v: k for k, v in CHARS.items()}
    return bytes(reverse[c] for c in text)


def decode(data: bytes) -> tuple[str, bool]:
    """(text, clean). clean is False if the name has bytes this decoder doesn't know."""
    out, clean, i = [], True, 0
    while i < len(data):
        pair = data[i : i + 2]
        if pair in TWO_BYTE:
            out.append(TWO_BYTE[pair])
            i += 2
            continue
        ch = CHARS.get(data[i])
        if ch is None:
            clean = False
            out.append(f"<{data[i]:02X}>")
        else:
            out.append(ch)
        i += 1
    return "".join(out), clean


def find_list(data: bytes, first_names: list[str]) -> int:
    """Offset of entry 1 of the list whose entries 1, 2, … are first_names (entry 0 is empty)."""
    needle = bytes([END]) + bytes([END]).join(encode(n) for n in first_names) + bytes([END])
    hits = [m.start() + 1 for m in re.finditer(re.escape(needle), data)]
    if not hits:
        raise ValueError(f"no name list starting with {first_names}")
    return hits[0]


def read_list(data: bytes, entry1: int, count: int) -> list[bytes]:
    """count entries of a list, entry 0 (empty) included, given where entry 1 starts."""
    out, pos = [b""], entry1
    for _ in range(count - 1):
        end = data.index(END, pos)
        out.append(data[pos:end])
        pos = end + 1
    return out
