"""Disassemble a RAM range of a code file, from the vanilla or the ReMixed disc.

    python -I tools/disasm.py BATTLE.BIN 0x80151C00 0x80151D00
    python -I tools/disasm.py BATTLE.BIN 0x80151C00 0x80151D00 --vanilla
    python -I tools/disasm.py SCUS_942.21 0x80027060 0x80027100 --both

File names are vanilla names (SCUS_942.21, BATTLE.BIN, WORLD/WORLD.BIN,
EVENT/ATTACK.OUT...). Lines ReMixed changed are marked with *.
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


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("file")
    parser.add_argument("start", type=lambda s: int(s, 0))
    parser.add_argument("end", type=lambda s: int(s, 0))
    side = parser.add_mutually_exclusive_group()
    side.add_argument("--vanilla", action="store_true", help="show vanilla code")
    side.add_argument("--both", action="store_true", help="show vanilla and ReMixed side by side")
    args = parser.parse_args()

    path = args.file.replace("\\", "/").upper()
    base = LOAD_ADDRESS.get(path)
    if base is None:
        sys.exit(f"No load address known for {args.file}. Known: {', '.join(LOAD_ADDRESS)}")

    cfg = load_config()
    with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
        old_file, new_file = FilePair(vanilla, patched).get(path)
    for addr in range(args.start & ~3, args.end, 4):
        at = addr - base
        if not 0 <= at <= len(new_file) - 4:
            break
        ow = int.from_bytes(old_file[at : at + 4], "little")
        nw = int.from_bytes(new_file[at : at + 4], "little")
        o, n = decode(ow, addr), decode(nw, addr)
        o_text = str(o) if o else f".word 0x{ow:08X}"
        n_text = str(n) if n else f".word 0x{nw:08X}"
        mark = "*" if ow != nw else " "
        if args.both:
            print(f"{addr:08X} {mark} {o_text:<36} | {n_text}")
        elif args.vanilla:
            print(f"{addr:08X} {mark} {ow:08X} {o_text}")
        else:
            print(f"{addr:08X} {mark} {nw:08X} {n_text}")


if __name__ == "__main__":
    main()
