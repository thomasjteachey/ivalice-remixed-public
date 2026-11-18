"""Audit the generated ReMixed event scripts' dialogue references.

Run after ``tools/build_mod.py --only enhanced``. This is a fast, read-only
check of every marked event shipped by the mod; it does not launch the game.
"""

from __future__ import annotations

import json
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "code" / "eiu" / "tools"))

from eiu_events.instructions import MARKER, read_lengths, remaster_lengths  # noqa: E402

MOD = ROOT / "mod" / "fftivc.gameplay.ivaliceremixed"
SCRIPT = MOD / "FFTIVC" / "data" / "enhanced" / "script" / "enhanced"
SCENARIOS = MOD / "remixed-scenarios.bin"
BATTLE = ROOT / "work" / "files" / "remixed" / "BATTLE.BIN"
LINE_ID_BASE = 0x40000
SCENARIO_BYTES, FORMATION_BYTES, PLACE_BYTES, MESSAGE_BYTES = 24, 12, 4, 6


def u16(data: bytes, at: int) -> int:
    return int.from_bytes(data[at:at + 2], "little")


def message_ranges(data: bytes) -> dict[int, range]:
    """Return event -> its actual generated PZD line-id range."""
    at = 0
    for size in (SCENARIO_BYTES, FORMATION_BYTES, PLACE_BYTES):
        count = u16(data, at)
        at += 2 + count * size
    count = u16(data, at)
    at += 2
    out = {}
    for k in range(count):
        event, base, lines = (u16(data, at + k * MESSAGE_BYTES + n) for n in (0, 2, 4))
        first = LINE_ID_BASE + base + 1
        out[event] = range(first, first + lines)
    return out


def check() -> str:
    lengths = remaster_lengths(read_lengths(BATTLE.read_bytes()))
    ranges = message_ranges(SCENARIOS.read_bytes())
    scripts = sorted(SCRIPT.glob("event*.e"))
    if not scripts:
        raise AssertionError(f"no generated scripts in {SCRIPT}")

    messages = dynamic = closes = skipped = 0
    wrong: list[str] = []
    for path in scripts:
        event = int(path.stem[5:])
        marked = path.read_bytes()
        if not marked.startswith(MARKER):
            # The folder also contains the build's two small stock-format event overrides; ReMixed's converted
            # scripts are exactly the marked ones.
            skipped += 1
            continue
        code, at, pending = marked[len(MARKER):], 0, []
        while at < len(code):
            op = code[at]
            end = at + 1 + lengths[op]
            if end > len(code):
                wrong.append(f"{path.name}: instruction 0x{op:02X} at 0x{at:X} runs past the script")
                break
            if op == 0xE6:  # PATCH_NEXT; low 6 bits are the patched parameter's byte offset
                pending.append(code[at + 1] & 0x3F)
                at = end
                continue
            if op in (0x10, 0x51):
                field, patched_at = (3, 2) if op == 0x10 else (2, 1)
                line_id = int.from_bytes(code[at + field:at + field + 4], "little")
                messages += 1
                if patched_at in pending:
                    dynamic += 1
                    if line_id >> 16 != LINE_ID_BASE >> 16:
                        wrong.append(f"{path.name}: runtime-selected message at 0x{at:X} has invalid template "
                                     f"0x{line_id:08X}")
                elif op == 0x51 and line_id == 0xFFFFFFFF:
                    closes += 1  # the PS1's intentional close-box ChangeDialog
                elif event not in ranges or line_id not in ranges[event]:
                    expected = ranges.get(event)
                    wanted = "no assigned text rows" if expected is None else \
                        f"0x{expected.start:X}..0x{expected.stop - 1:X}"
                    wrong.append(f"{path.name}: message at 0x{at:X} names 0x{line_id:X}, expected {wanted}")
            pending = []
            at = end
    if wrong:
        raise AssertionError("broken generated dialogue:\n  " + "\n  ".join(wrong))
    # Validate runtime ownership too. A generated line id can be in range but still draw empty when two ScenarioId
    # rows claim its event and the game loads the stock row's unrelated PZD first (Mandalia battle 0x34D did this).
    scenario_db = ROOT / "work" / "remaster" / "build" / "scenarioid" / "table.sqlite"
    if not scenario_db.exists():
        raise AssertionError(f"missing built ScenarioId database {scenario_db}; run tools/build_mod.py first")
    db = sqlite3.connect(scenario_db)
    owners: dict[int, list[int]] = {event: [] for event in ranges}
    for key, ids in db.execute('select Key, EventIds from "ScenarioId"'):
        for event in json.loads(ids or "[]"):
            if event in owners:
                owners[event].append(key)
    db.close()
    bad_owners = {event: keys for event, keys in owners.items() if len(keys) != 1}
    if bad_owners:
        raise AssertionError("generated dialogue has non-unique ScenarioId ownership:\n  "
                             + "\n  ".join(f"event 0x{event:X}: {keys}" for event, keys in sorted(bad_owners.items())))
    return (f"{len(scripts) - skipped} marked scripts: {messages} MESSAGE/ChangeDialog references valid "
            f"({dynamic} runtime-selected, {closes} intentional closes; {skipped} stock-format overrides skipped); "
            f"{len(owners)} events have one ScenarioId text owner each")


def main() -> None:
    print(check())


if __name__ == "__main__":
    main()
