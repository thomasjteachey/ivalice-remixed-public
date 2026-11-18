"""Make focused saves for ReMixed story-flow regressions.

    python -I tools/make_story_regression_saves.py enhanced.png OUT_DIR [--from-slot 1]

The source is left untouched. Three copies are packed in OUT_DIR:

- before-gariland.png loads at Mandalia at story progress 4; enter the adjacent
  Gariland node for the Biggs and Wedge scene.
- before-araguay.png loads at location 9 beside Araguay Woods at story progress
  0x10, with Luso's Mandalia encounter already complete; enter Araguay.
- before-hirame-wright.png loads at Eagrose after the chapter-three branch has
  opened; enter Fort Zeakden to begin Hirame and Wright's route.

All contain one slot, FILE01, using the selected slot's roster and inventory
with only the story/world variables needed by the test changed.
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
EVENTS = 0x8634
# The world map also serializes its current node in the slot summary and world
# state, then restores var 0x31 while loading. Keeping only the event-variable
# copy changed makes a save validate on disk but puts the party back at its old
# node in game.
WORLD_LOCATION_SUMMARY, WORLD_LOCATION = 0x116, 0x96A8
FIRST_FLAG, FLAG_WORDS = 0x80, 0x200


def ff16tools(*args: object) -> None:
    exe = load_config().get("tools", {}).get("ff16tools")
    if not exe:
        sys.exit("Set tools.ff16tools in local.toml to FF16Tools.CLI.exe.")
    subprocess.run([exe, *map(str, args)], check=True, capture_output=True)


def slot_at(k: int) -> int:
    return HEADER + k * SLOT


def variable_at(slot: int, variable: int) -> tuple[int, int, int]:
    """The containing word, bit shift and mask for a saved event variable."""
    base = slot_at(slot) + EVENTS
    if variable < FIRST_FLAG:
        return base + variable * 4, 0, 0xFFFFFFFF
    if variable < 0x360:
        return base + FLAG_WORDS + (variable - FIRST_FLAG) // 32 * 4, (variable - FIRST_FLAG) % 32, 1
    raise ValueError(f"this test does not write nibble variable 0x{variable:X}")


def write_variable(data: bytearray, slot: int, variable: int, value: int) -> None:
    at, shift, mask = variable_at(slot, variable)
    word = struct.unpack_from("<I", data, at)[0]
    word = value if mask == 0xFFFFFFFF else (word & ~(mask << shift)) | ((value & mask) << shift)
    struct.pack_into("<I", data, at, word)


def read_variable(data: bytearray, slot: int, variable: int) -> int:
    at, shift, mask = variable_at(slot, variable)
    word = struct.unpack_from("<I", data, at)[0]
    return word if mask == 0xFFFFFFFF else (word >> shift) & mask


def make(source: bytearray, source_slot: int, values: dict[int, int]) -> bytearray:
    data = bytearray(source)
    chosen = bytes(source[slot_at(source_slot):slot_at(source_slot) + SLOT])
    empty_slot = next(k for k in range(SLOTS) if source[slot_at(k):slot_at(k) + 2] != b"SC")
    empty = bytes(source[slot_at(empty_slot):slot_at(empty_slot) + SLOT])
    for k in range(SLOTS):
        data[slot_at(k):slot_at(k) + SLOT] = empty
    data[slot_at(0):slot_at(0) + SLOT] = chosen
    for variable, value in values.items():
        write_variable(data, 0, variable, value)
    if 0x31 in values:
        data[slot_at(0) + WORLD_LOCATION_SUMMARY] = values[0x31]
        struct.pack_into("<I", data, slot_at(0) + WORLD_LOCATION, values[0x31])
    struct.pack_into("<I", data, 4, zlib.crc32(data[HEADER:]))
    for variable, value in values.items():
        if read_variable(data, 0, variable) != value:
            raise AssertionError(f"variable 0x{variable:X} did not round-trip")
    return data


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("save", type=Path)
    parser.add_argument("out_dir", type=Path)
    parser.add_argument("--from-slot", type=int, default=1, help="the file number whose party to copy (default 1)")
    args = parser.parse_args()
    source_slot = args.from_slot - 1

    tests = {
        "before-gariland.png": {
            0x31: 24,      # party at Mandalia; entering adjacent Gariland starts 0x0A4
            0x6E: 4,
            0x8E: 0,       # no post-battle return pending
            0x92: 0,       # Biggs and Wedge not completed
            0x93: 0,       # Luso not met at Mandalia
            0xAF: 0,       # main campaign, not the Argath campaign
            0x206: 1,      # Gariland visible
            0x22F: 1,      # route from Mandalia to Gariland visible
            0x2EC: 0,      # side chapter 1-A not running
        },
        "before-araguay.png": {
            0x31: 9,       # beside Araguay; entering location 32 starts scenario 121
            0x6E: 0x10,
            0x8E: 0,
            0x92: 1,       # Biggs and Wedge completed
            0x93: 1,       # Luso met at Mandalia; victory should run 0x107
            0xAF: 0,
            0x220: 1,      # Araguay Woods visible
            0x240: 1,      # route from location 9 to Araguay visible
            0x287: 0,      # Araguay story not completed
            0x2EC: 0,
        },
        "before-hirame-wright.png": {
            0x31: 2,       # Eagrose; enter Fort Zeakden (location 15)
            0x6E: 0x22,    # chapter-three branch is eligible
            0x8E: 0,
            0xAE: 0,       # Hirame has not been lost
            0xAF: 0,       # main campaign, not the Argath campaign
            0x20F: 1,      # Fort Zeakden visible
            0x22C: 1,      # ReMixed's direct Eagrose-to-Zeakden route visible
            0x2EC: 0,      # side chapter 1-A not running
            0x2ED: 1,      # Hirame/Wright chapter-three branch open
        },
    }

    with tempfile.TemporaryDirectory() as temporary:
        work = Path(temporary)
        ff16tools("unpack-save", "-i", args.save.resolve(), "-o", work / "save")
        save_path = work / "save" / "fftsave.bin"
        source = bytearray(save_path.read_bytes())
        if len(source) != HEADER + SLOTS * SLOT or struct.unpack_from("<I", source, 0)[0] != HEADER:
            sys.exit("Not the save layout this tool knows.")
        if zlib.crc32(source[HEADER:]) != struct.unpack_from("<I", source, 4)[0]:
            sys.exit("The save's CRC does not match; not touching it.")
        if not 0 <= source_slot < SLOTS or source[slot_at(source_slot):slot_at(source_slot) + 2] != b"SC":
            sys.exit(f"FILE{args.from_slot:02d} is empty.")

        args.out_dir.mkdir(parents=True, exist_ok=True)
        for name, values in tests.items():
            packed = work / name
            shutil.copytree(work / "save", packed)
            (packed / "fftsave.bin").write_bytes(make(source, source_slot, values))
            output = (args.out_dir / name).resolve()
            ff16tools("pack-save", "-i", packed, "-o", output, "-s", "-g", "fft")
            print(f"{output}: FILE01 from FILE{args.from_slot:02d}")


if __name__ == "__main__":
    main()
