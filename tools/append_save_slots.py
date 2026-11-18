"""Append FILE01 from one-slot save containers to the first empty slots of a manual save.

    python -I tools/append_save_slots.py BASE.png OUT.png TEST1.png [TEST2.png ...]

The base and inputs are left untouched. Existing slots in BASE are preserved, copied slots are
renumbered for their new FILE positions, and the output CRC is rebuilt.
"""

from __future__ import annotations

import argparse
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from remixed import load_config  # noqa: E402

HEADER, SLOT, SLOTS = 0x10, 0x9CE4, 50
FULLWIDTH_DIGITS = "０１２３４５６７８９"


def ff16tools(*args: object) -> None:
    exe = load_config().get("tools", {}).get("ff16tools")
    if not exe:
        sys.exit("Set tools.ff16tools in local.toml to FF16Tools.CLI.exe.")
    subprocess.run([exe, *map(str, args)], check=True, capture_output=True)


def slot_at(k: int) -> int:
    return HEADER + k * SLOT


def validate(data: bytearray, name: Path) -> None:
    if len(data) != HEADER + SLOTS * SLOT or struct.unpack_from("<I", data, 0)[0] != HEADER:
        sys.exit(f"{name} does not use the known manual-save layout.")
    if zlib.crc32(data[HEADER:]) != struct.unpack_from("<I", data, 4)[0]:
        sys.exit(f"{name} has an invalid CRC; not touching it.")


def retitle(slot: bytearray, file_number: int) -> None:
    marker = "ＦＩＬＥ".encode("shift_jis")
    at = slot.index(marker, 0, 0x40) + len(marker)
    number = f"{file_number:02d}".translate(str.maketrans("0123456789", FULLWIDTH_DIGITS))
    slot[at:at + 4] = number.encode("shift_jis")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("base", type=Path)
    parser.add_argument("out", type=Path)
    parser.add_argument("tests", type=Path, nargs="+")
    args = parser.parse_args()

    with tempfile.TemporaryDirectory() as temporary:
        work = Path(temporary)
        ff16tools("unpack-save", "-i", args.base.resolve(), "-o", work / "base")
        base_path = work / "base" / "fftsave.bin"
        data = bytearray(base_path.read_bytes())
        validate(data, args.base)
        free = [k for k in range(SLOTS) if data[slot_at(k):slot_at(k) + 2] != b"SC"]
        if len(free) < len(args.tests):
            sys.exit(f"Need {len(args.tests)} empty slots; the base has {len(free)}.")

        added = []
        for n, test in enumerate(args.tests):
            folder = work / f"test-{n}"
            ff16tools("unpack-save", "-i", test.resolve(), "-o", folder)
            source = bytearray((folder / "fftsave.bin").read_bytes())
            validate(source, test)
            occupied = [k for k in range(SLOTS) if source[slot_at(k):slot_at(k) + 2] == b"SC"]
            if len(occupied) != 1:
                sys.exit(f"{test} must contain exactly one occupied slot, not {len(occupied)}.")
            target = free[n]
            copied = source[slot_at(occupied[0]):slot_at(occupied[0]) + SLOT]
            retitle(copied, target + 1)
            data[slot_at(target):slot_at(target) + SLOT] = copied
            added.append(f"{test.name} -> FILE{target + 1:02d}")

        struct.pack_into("<I", data, 4, zlib.crc32(data[HEADER:]))
        base_path.write_bytes(data)
        args.out.parent.mkdir(parents=True, exist_ok=True)
        ff16tools("pack-save", "-i", work / "base", "-o", args.out.resolve(), "-s", "-g", "fft")
        # A pack/unpack round trip catches a malformed container before it reaches a real save folder.
        ff16tools("unpack-save", "-i", args.out.resolve(), "-o", work / "verify")
        verified = bytearray((work / "verify" / "fftsave.bin").read_bytes())
        validate(verified, args.out)
    print("\n".join(added))


if __name__ == "__main__":
    main()
