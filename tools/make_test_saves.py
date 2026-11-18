"""Test saves for the Testing list, made from one of your own: copies of a used slot in empty slots, with the units a
test needs.

    python -I tools/make_test_saves.py SAVE.png OUT.png [--from-slot 3]

SAVE.png is the game's manual save (`enhanced.png`; FF16Tools' unpack-save gives `fftsave.bin`). OUT.png is the same
file with two more slots; the slots already there are left as they are. The save's layout, as worked out
(2026-11-04): a 16-byte header (u32 16, u32 CRC32 of everything after the header, u32 14), then 50 slots of 0x9CE4
bytes, each starting with "SC" and its title ("ＦＦＴ　ＦＩＬＥ０３　…", Shift-JIS); a slot's roster is at +0x518, 54
records of 0x258 bytes laid out as the game's (docs/REMASTER.md, "A roster unit's fields"; +1 the slot, 0xFF when
empty; +0x120–0x124 its places in the party lists). The slots made:

- FILE04, the dismissal and job change tests: Ramza with Secret Hunt (0x1D7) as his support, the first other unit
  with Throw Item (0x1DA, which a job change to Chemist should take off), and a Chocobo in the roster (to dismiss, and
  to ride in a battle).
- FILE05, riding: the same with a Black Chocobo too.

A monster is made the way the game's new-unit routine (0x275310, kind 3) starts one: sprite set 0x82, gender 0x20,
then a monster job, no equipment and no abilities, from a copy of a human record.
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

HEADER, SLOT, SLOTS, ROSTER, RECORD, UNITS = 0x10, 0x9CE4, 50, 0x518, 0x258, 54
SECRET_HUNT, THROW_ITEM, CHOCOBO, BLACK_CHOCOBO = 0x1D7, 0x1DA, 0x5E, 0x5F
SUPPORT, EQUIPMENT, NAME, ORDER = 0x0A, 0x0E, 0xDC, 0x121
FULLWIDTH_DIGITS = "０１２３４５６７８９"


def ff16tools(*args: object) -> None:
    exe = load_config().get("tools", {}).get("ff16tools")
    if not exe:
        sys.exit("Set tools.ff16tools in local.toml to FF16Tools.CLI.exe.")
    subprocess.run([exe, *map(str, args)], check=True, capture_output=True)


def slot_at(k: int) -> int:
    return HEADER + k * SLOT


def records(data: bytearray, k: int) -> list[int]:
    """The offsets of slot k's roster records in use."""
    base = slot_at(k) + ROSTER
    return [base + u * RECORD for u in range(UNITS) if data[base + u * RECORD + 1] != 0xFF and any(data[base + u * RECORD: base + u * RECORD + 3])]


def retitle(data: bytearray, k: int) -> None:
    """The slot's title names its file number (ＦＩＬＥ０４…)."""
    marker = "ＦＩＬＥ".encode("shift_jis")
    i = data.index(marker, slot_at(k), slot_at(k) + 0x40) + len(marker)
    number = f"{k + 1:02d}".translate(str.maketrans("0123456789", FULLWIDTH_DIGITS))
    data[i: i + 4] = number.encode("shift_jis")


def add_monster(data: bytearray, k: int, job: int, name: str) -> int:
    """A monster in slot k's first free roster place, from a copy of a human record; its place."""
    base = slot_at(k) + ROSTER
    used = records(data, k)
    human = next(r for r in used if data[r] >= 0x80)  # a generic's record as the template
    free = next(u for u in range(50) if data[base + u * RECORD + 1] == 0xFF or not any(data[base + u * RECORD: base + u * RECORD + 3]))
    at = base + free * RECORD
    record = bytearray(data[human: human + RECORD])
    record[0], record[1], record[2], record[4] = 0x82, free, job, 0x20
    record[7] = 0                                   # no secondary
    record[8:0x0E] = bytes(6)                       # no reaction, support or movement
    for slot in range(7):
        struct.pack_into("<H", record, EQUIPMENT + 2 * slot, 0xFF)
    encoded = name.encode("ascii") + b"\0"
    record[NAME: NAME + 16] = encoded.ljust(16, b"\0")
    places = [data[r + ORDER + 1] for r in used]
    record[ORDER + 1] = record[ORDER + 3] = max(places) + 1
    data[at: at + RECORD] = record
    return free


def make(data: bytearray, source: int, target: int, black_chocobo: bool) -> list[str]:
    data[slot_at(target): slot_at(target) + SLOT] = data[slot_at(source): slot_at(source) + SLOT]
    retitle(data, target)
    used = records(data, target)
    ramza = next(r for r in used if data[r] in (1, 2, 3))
    struct.pack_into("<H", data, ramza + SUPPORT, SECRET_HUNT)
    other = next(r for r in used if data[r] >= 0x80)
    struct.pack_into("<H", data, other + SUPPORT, THROW_ITEM)
    notes = [f"FILE{target + 1:02d}: Ramza with Secret Hunt; the unit in place {data[other + 1]} with Throw Item"]
    place = add_monster(data, target, CHOCOBO, "Boco")
    notes.append(f"a Chocobo (Boco) in place {place}")
    if black_chocobo:
        place = add_monster(data, target, BLACK_CHOCOBO, "Kuro")
        notes.append(f"a Black Chocobo (Kuro) in place {place}")
    return notes


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("save", type=Path)
    parser.add_argument("out", type=Path)
    parser.add_argument("--from-slot", type=int, default=3, help="the file number to copy (default 3)")
    args = parser.parse_args()
    with tempfile.TemporaryDirectory() as work:
        work = Path(work)
        ff16tools("unpack-save", "-i", args.save, "-o", work / "save")
        path = work / "save" / "fftsave.bin"
        data = bytearray(path.read_bytes())
        if struct.unpack_from("<I", data, 0)[0] != HEADER or len(data) != HEADER + SLOTS * SLOT:
            sys.exit("Not the save layout this tool knows.")
        if zlib.crc32(data[HEADER:]) != struct.unpack_from("<I", data, 4)[0]:
            sys.exit("The save's CRC doesn't match; not touching it.")
        source = args.from_slot - 1
        if data[slot_at(source): slot_at(source) + 2] != b"SC":
            sys.exit(f"FILE{args.from_slot:02d} is empty.")
        free = [k for k in range(SLOTS) if data[slot_at(k): slot_at(k) + 2] != b"SC"][:2]
        if len(free) < 2:
            sys.exit("No two empty slots.")
        notes = make(data, source, free[0], black_chocobo=False) + make(data, source, free[1], black_chocobo=True)
        struct.pack_into("<I", data, 4, zlib.crc32(data[HEADER:]))
        path.write_bytes(data)
        args.out.parent.mkdir(parents=True, exist_ok=True)
        ff16tools("pack-save", "-i", work / "save", "-o", args.out.resolve(), "-s", "-g", "fft")
    print("\n".join(notes))


if __name__ == "__main__":
    main()
