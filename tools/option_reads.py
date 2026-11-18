"""List ReMixed code that reads its Extra Options flags (docs/MECHANICS.md, "Where the options live").

    python -I tools/option_reads.py           # loads of the option flag bytes
    python -I tools/option_reads.py --wide    # also nearby loads, the area's address, and flag-helper calls

Flags 0x80 and up are bits of the bytes from 0x8005791C, the low bit first, so the option flags
0xD0-0xFF are the bytes 0x80057926-0x8005792B. This finds every lb/lbu/lh/lhu/lw with one of those
offsets (0x7926-0x792B) in every file tools/codemap.py knows the load address of, marked * where ReMixed
changed the word. With --wide it also lists, in ReMixed's changed code, loads of 0x80057918-0x8005792F
(a word or halfword can cover the option bytes), address computations of that area, and an a0 of
0xD0-0xFF set next to a jal: a call of a flag helper, as WORLD's dismissal does for flag 0xE5
(0x800EF1A8 at 0x8013DD40).

It doesn't follow base registers, so a hit can be data that decodes as a load (WORLD 0x80166464 is
one), and a read through another base is missed. Some options aren't code at all: ReMixed's events
read them (tools/events.py; Guest Control is event 0x192). Check each hit with tools/disasm.py.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from codemap import LOAD_ADDRESS  # noqa: E402
from mips import decode  # noqa: E402
from psxdisc import Disc  # noqa: E402
from remixed import load_config, remixed_bin, vanilla_bin  # noqa: E402
from tables import FilePair  # noqa: E402

FLAG_BYTES = {0x7926: "0xD0-0xD7", 0x7927: "0xD8-0xDF", 0x7928: "0xE0-0xE7", 0x7929: "0xE8-0xEF", 0x792A: "0xF0-0xF7",
              0x792B: "0xF8-0xFF"}
LOADS = (0x20, 0x21, 0x23, 0x24, 0x25)  # lb, lh, lw, lbu, lhu
JAL, ORI, ADDIU, A0 = 0x03, 0x0D, 0x09, 4


def hits(words: list[int], i: int, wide: bool) -> str | None:
    w = words[i]
    op, imm = w >> 26, w & 0xFFFF
    if op in LOADS and imm in FLAG_BYTES:
        return f"flags {FLAG_BYTES[imm]}"
    if not wide:
        return None
    if op in LOADS and 0x7918 <= imm <= 0x792F:
        return "a load near the option bytes"
    if op == ADDIU and 0x7918 <= imm <= 0x792F:
        return "the flag area's address"
    if op in (ORI, ADDIU) and (w >> 21) & 31 == 0 and (w >> 16) & 31 == A0 and 0xD0 <= imm <= 0xFF:
        near = [j for j in (i - 1, i + 1, i + 2, i + 3) if 0 <= j < len(words) and words[j] >> 26 == JAL]
        if near:
            return f"flag 0x{imm:X} passed to a call"
    return None


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--wide", action="store_true", help="also nearby loads, address computations, helper calls")
    args = parser.parse_args()
    cfg = load_config()
    with Disc(vanilla_bin(cfg)) as v, Disc(remixed_bin(cfg)) as p:
        pair = FilePair(v, p)
        for name, base in LOAD_ADDRESS.items():
            try:
                old, new = pair.get(name)
            except Exception as e:  # noqa: BLE001 - a file this disc doesn't have
                print(f"{name}: not read ({e})")
                continue
            words = [int.from_bytes(new[k:k + 4], "little") for k in range(0, len(new) - 3, 4)]
            for i, w in enumerate(words):
                what = hits(words, i, args.wide)
                if what is None:
                    continue
                at = 4 * i
                changed = at + 4 > len(old) or new[at:at + 4] != old[at:at + 4]
                if not changed and not what.startswith("flags"):
                    continue  # the wider checks only matter in ReMixed's own code
                print(f"{name} 0x{base + at:08X} {'*' if changed else ' '} {what}: {decode(w, base + at)}")


if __name__ == "__main__":
    main()
