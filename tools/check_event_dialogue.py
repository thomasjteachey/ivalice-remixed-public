"""Audit the generated ReMixed event scripts' dialogue references.

Run after ``tools/build_mod.py --only enhanced``. This is a fast, read-only
check of every marked event shipped by the mod; it does not launch the game.
"""

from __future__ import annotations

import json
import re
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
# Generated copies which enter at the battle setup and jump over their source scene's dialogue to its shared END.
# Their terminal ChangeDialog closes are actively wrong: no box was opened on that path, and the remaster creates an
# empty feather-cursor box for the attempted close. Keep this in sync with build_mod.py's BATTLE_SPLITS values.
SPLIT_BATTLE_EVENTS = {0x2EB, 0x34A, 0x34B, 0x34D}


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


def dialogue_rows(key: int, lines: range) -> dict[int, tuple[str, int]]:
    path = ROOT / "work" / "remaster" / "build" / "pzd" / f"scenario{key:04d}.en.yaml"
    pattern = r"(?m)^- Id: (\d+)\n  Line: (.*)\n  Speaker:\n    Type: 24\n    Value: (\d+)$"
    return {int(line) - lines.start + 1: (json.loads(text), int(speaker)) for line, text, speaker in
            re.findall(pattern, path.read_text(encoding="utf-8"))}


def check_chapter_opening_speakers(rows: dict[int, tuple[str, int]]) -> None:
    for intro, continuing in ((2, (*range(4, 23, 2), 24)), (3, (*range(5, 22, 2), 25))):
        speaker = rows[intro][1]
        if not speaker or any(rows[message][1] != speaker for message in continuing):
            raise AssertionError(f"side chapter 1-A's PSP opening lost the speaker introduced in message {intro}")


def check_parallel_speakers(psp: dict[int, tuple[str, int]], psx: dict[int, tuple[str, int]], count: int) -> None:
    for message in range(1, count + 1):
        text, speaker = psp[message]
        if psx[message][1] and (not speaker or "<cursor>" in text and not text.startswith("<speaker=")):
            raise AssertionError(f"PSP counterpart message {message} lost its speaker")


def check_chapter_opening_notices(code: bytes, lengths: bytes, lines: range) -> None:
    shown, at = [], 0
    sizing_label = code[1] if code[0] == 0x0D else None  # the jump over the sizing-only messages
    while at < len(code):
        op = code[at]
        if sizing_label is not None:
            if op == 0x0E and code[at + 1] == sizing_label:
                sizing_label = None
        elif op == 0x10:
            message = int.from_bytes(code[at + 3:at + 7], "little") - lines.start + 1
            style = code[at + 2]
            if style == 0x9B or (style == 0x96 and message == 27):
                shown.append(message)
        at += 1 + lengths[op]
    # The option's inventory warning continues as page 35. The prologue's own pages start at 36.
    expected = [26, 35, 27, 28, *range(36, 42)]
    if shown != expected:
        raise AssertionError(f"side chapter 1-A's opening shows notices and choice {shown}, expected {expected}")


def check_chapter_opening_speech_boxes(code: bytes, lengths: bytes, lines: range) -> None:
    """The remaster must never leave the other speaker's acknowledged, empty frame on screen."""
    active: set[int] = set()
    opened, at = [], 0
    sizing_label = code[1] if code[0] == 0x0D else None
    while at < len(code):
        op = code[at]
        if sizing_label is not None:
            if op == 0x0E and code[at + 1] == sizing_label:
                sizing_label = None
            at += 1 + lengths[op]
            continue
        if op == 0x51:
            box = code[at + 1]
            line = int.from_bytes(code[at + 2:at + 6], "little")
            if line == 0xFFFFFFFF:
                active.discard(box)
            elif box in (1, 2) and line in lines:
                message = line - lines.start + 1
                if message <= 25:
                    raise AssertionError(f"1-A opening message {message} swaps into persistent speech box {box}")
        elif op == 0x10:
            box = code[at + 2] & 0x0F
            line = int.from_bytes(code[at + 3:at + 7], "little")
            if box in (1, 2) and line in lines:
                message = line - lines.start + 1
                if message <= 25:
                    if active:
                        raise AssertionError(f"1-A opening message {message} opens box {box} while {sorted(active)} remains")
                    active.add(box)
                    opened.append(message)
        at += 1 + lengths[op]
    # Message 23 is an empty spacer in both source texts; 24 and 25 are the conditional continuation.
    expected = [*range(1, 23), 24, 25]
    if sorted(opened) != expected:
        raise AssertionError(f"1-A opening speech boxes cover {sorted(opened)}, expected {expected}")


def check_bervenia_postbattle_copy(owners: dict[int, list[int]], ranges: dict[int, range]) -> None:
    """The first Bervenia battle's same-map closing must own a populated copy of 0x2EA's dialogue."""
    source, closing = 0x2EA, 0x355
    copied = dialogue_rows(owners[closing][0], ranges[closing])
    if copied != dialogue_rows(owners[source][0], ranges[source]):
        raise AssertionError("Bervenia's post-battle event does not have 0x2EA's ReMixed dialogue")
    text, speaker = copied[32]
    if not text.startswith("Nay") or not speaker:
        raise AssertionError("Bervenia's first post-battle Agrias line is empty or unnamed")


def check() -> str:
    lengths = remaster_lengths(read_lengths(BATTLE.read_bytes()))
    ranges = message_ranges(SCENARIOS.read_bytes())
    scripts = sorted(SCRIPT.glob("event*.e"))
    if not scripts:
        raise AssertionError(f"no generated scripts in {SCRIPT}")

    messages = dynamic = closes = terminal_closes = clean_split_ends = skipped = 0
    wrong: list[str] = []
    for path in scripts:
        event = int(path.stem[5:])
        marked = path.read_bytes()
        if not marked.startswith(MARKER):
            # The folder also contains the build's two small stock-format event overrides; ReMixed's converted
            # scripts are exactly the marked ones.
            skipped += 1
            continue
        code, at, pending, has_messages, recent_closes = marked[len(MARKER):], 0, [], False, set()
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
                    recent_closes.add(code[at + 1])
                elif event not in ranges or line_id not in ranges[event]:
                    expected = ranges.get(event)
                    wanted = "no assigned text rows" if expected is None else \
                        f"0x{expected.start:X}..0x{expected.stop - 1:X}"
                    wrong.append(f"{path.name}: message at 0x{at:X} names 0x{line_id:X}, expected {wanted}")
                if op == 0x10:
                    has_messages = True
                    recent_closes.clear()
                elif line_id != 0xFFFFFFFF:
                    recent_closes.clear()
            elif op == 0xDB and has_messages:
                if event in SPLIT_BATTLE_EVENTS:
                    if recent_closes:
                        wrong.append(f"{path.name}: split battle END at 0x{at:X} closes unopened box(es) "
                                     f"{sorted(recent_closes)}")
                    else:
                        clean_split_ends += 1
                else:
                    missing = {1, 2, 3} - recent_closes
                    if missing:
                        wrong.append(f"{path.name}: END at 0x{at:X} leaves message box(es) {sorted(missing)} open")
                    else:
                        terminal_closes += 3
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
    opening, opening_psx = 0x2F0, 0x193
    for psp, psx, count in ((opening, opening_psx, 28), (0x2F2, 0x197, 45)):
        psp_rows = dialogue_rows(owners[psp][0], ranges[psp])
        check_parallel_speakers(psp_rows, dialogue_rows(owners[psx][0], ranges[psx]), count)
        if psp == opening:
            check_chapter_opening_speakers(psp_rows)
    for event in (opening_psx, opening):
        code = (SCRIPT / f"event{event}.e").read_bytes()[len(MARKER):]
        check_chapter_opening_notices(code, lengths, ranges[event])
        check_chapter_opening_speech_boxes(code, lengths, ranges[event])
    check_bervenia_postbattle_copy(owners, ranges)
    return (f"{len(scripts) - skipped} marked scripts: {messages} MESSAGE/ChangeDialog references valid "
            f"({dynamic} runtime-selected, {closes} intentional closes, {terminal_closes} terminal box closes, "
            f"{clean_split_ends} split battle ENDs close no unopened boxes; "
            f"{skipped} stock-format overrides skipped); "
            f"{len(owners)} events have one ScenarioId text owner each; 1-A's opening speakers and notices and "
            "Bervenia's post-battle dialogue copy verified")


def main() -> None:
    print(check())


if __name__ == "__main__":
    main()
