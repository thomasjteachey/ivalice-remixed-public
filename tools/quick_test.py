"""Run small, named in-game checks against one warm harness process.

Case files live in tests/quick and contain one command per line. Values use Python-style integers (0x91, 12):

    name Lorne scene looks
    scenario 0x194
    wait unit battle_id=0x91 timeout=20
    expect unit battle_id=0x91 job=0x5b graphic=0x38

For a picture to check by eye, `shot NAME` saves the hidden window to work/harness/quick/CASE/NAME.png at that step;
every case also leaves pass.png (or fail.png) there as it ends. Pictures are evidence only: no step reads them.
`scenario 0x79 noted` starts a scene-first battle at its battle (its scene counted as seen). `defeat [UNIT...]`
downs the enemies (or those units); `expect roster name_id=0x76` wants exactly one roster record so, and
`count=N` another number of them (`count=0`: a recruit turned down on its join screen isn't there).
`show slots` (or `roster`) prints the battle units' roster slots and ids into the case's output, `show units` their
jobs, skillsets, unlocked jobs and job fields (levels, learned abilities, JP); `copy roster 2 3`
copies roster slot 2's record into slot 3, a party member more (`sprites=0x29 job=0x29 name_id=25` on the end make
the copy someone else: in slot 50, the guest Rafa).
`key K` posts one key to the game's own window (never the desktop's input). `wait log PATTERN press=RETURN every=1`
confirms through dialogue until a log line comes; `wait world depth=D state=S` waits for a world-map menu.
A battle unit matches on its listing's fields (index, id, job, team, primary, secondary, battle_id, ...) and on its
live ones (turn, moved, acted, ability, charge, statuses, hp, right_hand; player and present, 1 or 0):
`wait unit player=1 present=1 turn=1 timeout=300` waits for a party unit's turn; `FIELD&MASK=V` compares only the
mask's bits (`dark_knight_learned&0xA00000=0xA00000`: those two abilities learned, whatever the rest). `poke unit FIELD=VALUE... offset=0x13 hex=08` writes bytes into every matching unit (`or=`
ORs them in), to set a unit up for a test: a skillset, a learned ability, a weapon. `ride [foe] [skillset=0xB0]` links the
first party unit on the field as a rider to the next party unit (with `foe`, the first enemy) as its mount, the
mount's primary skillset a monster's (the Chocobo's by default), as boarding would; they keep their own panels.

Start or restart the hidden game and print strict PASS/FAIL lines:

    python -I tools/quick_test.py --start tests/quick/lorne-scene.txt
    python -I tools/quick_test.py --restart tests/quick/araguay-entry.txt
"""

from __future__ import annotations

import argparse
import shlex
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import harness

SHOTS = harness.WORK / "harness" / "quick"


class CaseFailure(RuntimeError):
    pass


def number(text: str) -> int:
    return int(text, 0)


def fields(words: list[str]) -> dict[str, int]:
    out = {}
    for word in words:
        if "=" not in word:
            raise CaseFailure(f"expected FIELD=VALUE, got {word!r}")
        key, value = word.split("=", 1)
        out[key.replace("-", "_")] = number(value)
    return out


def matches(item: dict, wanted: dict[str, int]) -> bool:
    """Every field equal; a `field&MASK` key compares only the mask's bits (`dark_knight_learned&0xA00000=0xA00000`)."""
    def field(key: str) -> int | None:
        name, _, mask = key.partition("&")
        value = item.get(name)
        return value & number(mask) if mask and value is not None else value
    return all(field(key) == value for key, value in wanted.items())


def describe(wanted: dict[str, int]) -> str:
    return " ".join(f"{key}=0x{value:X}" for key, value in wanted.items())


# Jobs by the remaster's job index (Squire 0 ... Mime 19, the Dark Knight 20) whose learned abilities, JP and total JP
# are fields.
JOB_FIELDS = {"archer": 3, "bard": 17, "dancer": 18, "dark_knight": 20}
# Where a record keeps the unlocked jobs (3 bytes, the high first: 0x800000 the Squire), the learned abilities (3 bytes a
# job, the high first), the JP and the total JP (halfwords): a roster record's, and a battle unit's (the PS1's +0x96,
# +0x99, +0xDC and +0x104, moved as the Event Instruction Upgrade's unit fields move them).
ROSTER_JOBS = (0x2F, 0x32, 0x80, 0xAE)
UNIT_JOBS, UNIT_SIZE = (0x9F, 0xA2, 0xF0, 0x11E), 0x200
# Job levels (a nibble a job, the even index high: 12 bytes, the remaster's 22 jobs) as `<job>_level` fields.
LEVEL_FIELDS = {"knight": 2, "black_mage": 6, "bard": 17, "dancer": 18, "dark_knight": 20}
ROSTER_LEVELS, UNIT_LEVELS, ROSTER_ERRAND = 0x74, 0xE4, 0x11E


def job_fields(r: bytes, at: tuple[int, int, int, int]) -> dict[str, int]:
    """A record's unlocked jobs and its Archer's, Bard's and Dancer's learned abilities, JP and total JP."""
    unlocked, learned, jp, total = at
    out = {"unlocked": int.from_bytes(r[unlocked:unlocked + 3], "big")}
    for job, k in JOB_FIELDS.items():
        out[f"{job}_learned"] = int.from_bytes(r[learned + 3 * k:learned + 3 * k + 3], "big")
        out[f"{job}_jp"] = int.from_bytes(r[jp + 2 * k:jp + 2 * k + 2], "little")
        out[f"{job}_total"] = int.from_bytes(r[total + 2 * k:total + 2 * k + 2], "little")
    return out


def level_fields(r: bytes, levels: int) -> dict[str, int]:
    """A record's job levels for LEVEL_FIELDS' jobs."""
    return {f"{job}_level": r[levels + k // 2] >> (0 if k & 1 else 4) & 0xF for job, k in LEVEL_FIELDS.items()}


def roster_records(data: bytes | None = None) -> list[dict]:
    """The roster records in use: slot, sprite set, name id (harness.cmd_roster's fields), job, EXP and job fields."""
    if data is None:
        data = harness.peek_bytes(harness.image_base() + harness.ROSTER, harness.ROSTER_SIZE * harness.ROSTER_COUNT)
    found = []
    for k in range(len(data) // harness.ROSTER_SIZE):
        r = data[k * harness.ROSTER_SIZE:(k + 1) * harness.ROSTER_SIZE]
        if r[1] == 0xFF or not any(r[:4]):
            continue
        found.append({"slot": k, "sprites": r[0], "job": r[2], "exp": r[0x1C],
                      "name_id": int.from_bytes(r[harness.ROSTER_NAME_ID:harness.ROSTER_NAME_ID + 4], "little"),
                      "errand": r[ROSTER_ERRAND], **job_fields(r, ROSTER_JOBS), **level_fields(r, ROSTER_LEVELS)})
    return found


# A battle unit's live fields beyond `units`' listing, read in one peek of the whole array: the turn flag, moved and
# acted, the pending action's ability and charge (0xFF none), the action statuses (+0x61: 8 Charging) and the HP.
UNIT_FIELDS = {"turn": (0x1B8, 1), "moved": (0x1B9, 1), "acted": (0x1BA, 1), "ability": (0x1A2, 2),
               "charge": (0x18D, 1), "statuses": (0x61, 1), "hp": (harness.UNIT_HP, 2), "right_hand": (0x20, 2)}


def unit_records() -> list[dict]:
    """The battle units (the in-game harness's fields), with their live fields and job fields read from memory."""
    base, units = harness.battle_units()
    if not base or not units:
        return units
    try:
        data = harness.peek_bytes(base, (max(u["index"] for u in units) + 1) * UNIT_SIZE)
    except (OSError, ValueError):
        return units  # no memory read back: the in-game fields only
    for u in units:
        at = u["index"] * UNIT_SIZE
        record = data[at:at + UNIT_SIZE]
        u["address"] = base + at
        for name, (offset, size) in UNIT_FIELDS.items():
            u[name] = int.from_bytes(record[offset:offset + size], "little")
        u["player"] = 1 if u["team"] & 0x08 else 0  # the player's to command (+5 & 8)
        u["present"] = 0 if u["id"] == 0xFF else 1  # on the field (a formation's spare records have id 0xFF)
        u.update(job_fields(record, UNIT_JOBS))
        u.update(level_fields(record, UNIT_LEVELS))
    return units


def show_units(_: object = None) -> None:
    """The battle units' jobs as a case's evidence: job, skillsets, unlocked jobs, and the job fields' levels, learned
    abilities and JP."""
    for u in unit_records():
        jobs = " ".join(f"{job} {u.get(f'{job}_level', '-')}/{u.get(f'{job}_learned', 0):06X}/{u.get(f'{job}_jp', 0)}"
                        for job in JOB_FIELDS)
        print(f"  unit {u['index']:2} battle id 0x{u['battle_id']:02X} job 0x{u['job']:02X} team 0x{u['team']:02X} "
              f"skillsets 0x{u['primary']:02X} 0x{u['secondary']:02X} unlocked 0x{u.get('unlocked', 0):06X} "
              f"(level/learned/JP) {jobs}")


def records(kind: str) -> list[dict]:
    if kind == "roster":
        return roster_records()
    return unit_records() if kind == "unit" else harness.battle_entries()


def poke_units(args: list[str], case: str) -> None:
    """poke unit FIELD=VALUE... offset=OFF hex=BYTES (or or=BYTES, ORed into what's there): every matching unit."""
    wanted, offset, data, merge = {}, None, b"", False
    for arg in args:
        key, value = arg.split("=", 1)
        if key == "offset":
            offset = number(value)
        elif key in ("hex", "or"):
            data, merge = bytes.fromhex(value), key == "or"
        else:
            wanted[key.replace("-", "_")] = number(value)
    if offset is None or not data:
        raise CaseFailure("poke unit needs offset= and hex= (or or=)")
    chosen = find("unit", wanted)
    if not chosen:
        raise CaseFailure(f"poke unit: no unit {describe(wanted)}")
    for u in chosen:
        at = u["address"] + offset
        if merge:
            data = bytes(a | b for a, b in zip(harness.peek_bytes(at, len(data)), data))
        reply = harness.send(f"poke 0x{at:X} {data.hex()}")
        if reply.startswith("error"):
            raise CaseFailure(reply)
    print(f"PASS {case}: poked +0x{offset:X} = {data.hex()} on unit(s) {[u['index'] for u in chosen]}")


UNIT_LINK, UNIT_PRIMARY = 0x1B4, 0x12  # a rider's link is 0x80 | its mount's index, a mount's 0x40 | its rider's


def ride(args: list[str], case: str) -> None:
    """ride [foe] [skillset=0xB0]: the first party unit on the field (by battle index) becomes a rider, and the next
    party unit, or with foe the first enemy, its mount."""
    foe = "foe" in args
    skillset = fields([a for a in args if a != "foe"]).get("skillset", 0xB0)
    units = sorted(find("unit", {"present": 1}), key=lambda u: u["index"])
    party = [u for u in units if u["player"]]
    mounts = [u for u in units if not u["player"]] if foe else party[1:]
    if not party or not mounts:
        raise CaseFailure(f"ride needs a party unit and a{'n enemy' if foe else 'nother party unit'} on the field")
    rider, mount = party[0], mounts[0]
    for at, value in ((rider["address"] + UNIT_LINK, 0x80 | mount["index"]),
                      (mount["address"] + UNIT_LINK, 0x40 | rider["index"]),
                      (mount["address"] + UNIT_PRIMARY, skillset)):
        reply = harness.send(f"poke 0x{at:X} {value:02x}")
        if reply.startswith("error"):
            raise CaseFailure(reply)
    print(f"PASS {case}: unit {rider['index']} rides unit {mount['index']}, whose skillset is 0x{skillset:X}")


def find(kind: str, wanted: dict[str, int], items: list[dict] | None = None) -> list[dict]:
    items = records(kind) if items is None else items
    return [item for item in items if matches(item, wanted)]


def expect_record(kind: str, wanted: dict[str, int], case: str, line: int, items: list[dict] | None = None,
                  count: int | None = None) -> None:
    """Exactly one record so, or `count=N` of them (count=0: none)."""
    wanted = dict(wanted)
    count = wanted.pop("count", 1) if count is None else count
    found = find(kind, wanted, items)
    label = f"{kind} {describe(wanted)}" + (f" count={count}" if count != 1 else "")
    if len(found) != count:
        raise CaseFailure(f"{case}:{line}: {label}: expected {'exactly one' if count == 1 else count}, "
                          f"found {len(found)}")
    print(f"PASS {case}: {label}")


def wait_record(kind: str, wanted: dict[str, int], timeout: float, case: str, line: int,
                press: str | None = None, every: float = 1.0) -> list[dict]:
    deadline = time.monotonic() + timeout
    next_press = time.monotonic() + every
    while time.monotonic() < deadline:
        items = records(kind)
        if find(kind, wanted, items):
            print(f"PASS {case}: {kind} appeared ({describe(wanted)})")
            return items
        if press and time.monotonic() >= next_press:
            harness.send(f"key {press}")
            next_press = time.monotonic() + every
        time.sleep(0.25)
    raise CaseFailure(f"{case}:{line}: {kind} did not appear in {timeout:g}s ({describe(wanted)})")


def wait_log_pressing(pattern: str, timeout: float, before: int, press: str | None, every: float) -> bool:
    """Whether a log line matching pattern beyond the first `before` comes within timeout, pressing `press` (if any)
    every `every` seconds meanwhile, as a player confirming through a scene would."""
    deadline = time.monotonic() + timeout
    next_press = time.monotonic() + every
    while time.monotonic() < deadline:
        if harness.count_log(pattern) > before:
            return True
        if press and time.monotonic() >= next_press:
            harness.send(f"key {press}")
            next_press = time.monotonic() + every
        time.sleep(0.25)
    return harness.count_log(pattern) > before


def path_name(path: Path) -> str:
    return path.stem


def run_case(path: Path) -> None:
    case_file = path
    case = path.stem
    started = time.monotonic()
    snapshots: dict[str, list[dict]] = {}
    starting_logs = harness.log_lines()
    log_matches: dict[str, int] = {}
    for line_number, source in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        source = source.strip()
        if not source or source.startswith("#"):
            continue
        if source.split(maxsplit=1)[0].lower() == "name":
            case = source[4:].strip()  # as written: an apostrophe in a name ("Luso's") is no quote
            continue
        words = shlex.split(source, comments=True)
        command, args = words[0].lower(), words[1:]
        try:
            if command == "name":
                case = " ".join(args)
            elif command == "scenario":
                if not harness.on_world_map(5):
                    raise CaseFailure("scenario requires the idle world map")
                # scenario N [noted] [event=E]: event= loads E for it (the remaster's own scenes, whose events a
                # requested start doesn't load: tests/quick/lionel-in-sight.txt).
                scenario = number(args[0])
                noted = " noted" if any(a.lower() == "noted" for a in args[1:]) else ""
                event = next((f" event {number(a.split('=', 1)[1]):X}" for a in args[1:] if a.startswith("event=")), "")
                reply = harness.send(f"scenario {scenario:X}{noted}{event}")
                if reply.startswith("error"):
                    raise CaseFailure(reply)
            elif command == "setvar":
                reply = harness.send(f"setvar {number(args[0])} {number(args[1])}")
                if reply.startswith("error"):
                    raise CaseFailure(reply)
            elif command == "send":
                reply = harness.send(" ".join(args))
                if reply.startswith("error"):
                    raise CaseFailure(reply)
            elif command == "poke" and args and args[0] == "unit":
                poke_units(args[1:], case)
            elif command == "ride":
                ride(args, case)
            elif command == "copy" and args and args[0] == "roster":
                # copy roster FROM TO [sprites=S] [job=J] [name_id=N]: a party member more, or with those fields
                # someone else (a guest in slots 50-53: sprites=0x29 job=0x29 name_id=25 is the guest Rafa)
                source, target = number(args[1]), number(args[2])
                base = harness.image_base() + harness.ROSTER
                record = bytearray(harness.peek_bytes(base + source * harness.ROSTER_SIZE, harness.ROSTER_SIZE))
                record[1] = target  # a roster unit's id is its slot
                for arg in args[3:]:
                    field, _, value = arg.partition("=")
                    if field == "sprites":
                        record[0] = number(value)
                    elif field == "job":
                        record[2] = number(value)  # +0x02 (roster_records' job)
                    elif field == "name_id":
                        record[harness.ROSTER_NAME_ID:harness.ROSTER_NAME_ID + 4] = number(value).to_bytes(4, "little")
                    else:
                        raise CaseFailure(f"copy roster: unknown field {field!r}")
                reply = harness.send(f"poke 0x{base + target * harness.ROSTER_SIZE:X} {record.hex(' ')}")
                if reply.startswith("error"):
                    raise CaseFailure(reply)
            elif command == "show":  # a listing into the case's output, as evidence: slots, roster or units
                {"slots": harness.cmd_slots, "roster": harness.cmd_roster, "units": show_units}[args[0].lower()](None)
            elif command == "advance":
                # advance world|opening|event N [timeout=S] [every=S] [shots=1]: with shots, a frame of the scene
                # before each press, in the case's folder (advance-NNNN.png, tenths of a second in).
                timeout, every, shots = 45.0, 0.25, False
                positional = []
                for arg in args:
                    if arg.startswith("timeout="):
                        timeout = float(arg.split("=", 1)[1])
                    elif arg.startswith("every="):
                        every = float(arg.split("=", 1)[1])
                    elif arg.startswith("shots="):
                        shots = arg.split("=", 1)[1] not in ("0", "no")
                    else:
                        positional.append(arg)
                reached = " ".join(positional)
                result = harness.advance(reached, timeout, every=every, shots=shots,
                                         shot_dir=SHOTS / path_name(case_file) if shots else None)
                if reached == "world" and not harness.on_world_map(2):
                    raise CaseFailure("world map was not reached")
                if reached == "opening" and result["op"] != 0xDB:
                    raise CaseFailure("battle opening END was not reached")
            elif command == "formation":
                options = fields(args)
                if not harness.formation(options.get("units", 1), options.get("second", 0),
                                         "yes" if options.get("reveal", 0) else "no", ask_wait=1):
                    raise CaseFailure("formation did not commence")
            elif command == "wait":
                kind = args[0].lower()
                if kind in ("unit", "entry"):
                    wanted = {}
                    timeout, press, every = 20.0, None, 1.0
                    for arg in args[1:]:
                        key, value = arg.split("=", 1)
                        key = key.replace("-", "_")
                        if key == "timeout":
                            timeout = float(value)
                        elif key == "press":
                            press = value
                        elif key == "every":
                            every = float(value)
                        else:
                            wanted[key] = number(value)
                    snapshots[kind] = wait_record(kind, wanted, timeout, case, line_number, press, every)
                elif kind == "log":
                    timeout, press, every = 20.0, None, 1.0
                    patterns = []
                    for arg in args[1:]:
                        if arg.startswith("timeout="):
                            timeout = float(arg.split("=", 1)[1])
                        elif arg.startswith("press="):
                            press = arg.split("=", 1)[1]
                        elif arg.startswith("every="):
                            every = float(arg.split("=", 1)[1])
                        else:
                            patterns.append(arg)
                    pattern = " ".join(patterns)
                    if pattern not in log_matches:
                        log_matches[pattern] = harness.count_log(pattern, starting_logs)
                    found = (wait_log_pressing(pattern, timeout, log_matches[pattern], press, every) if press
                             else harness.wait_log(pattern, timeout, before=log_matches[pattern]) is not None)
                    if not found:
                        raise CaseFailure(f"log did not match {pattern!r} in {timeout:g}s")
                    log_matches[pattern] += 1
                    print(f"PASS {case}: log matched {pattern!r}")
                elif kind == "seconds":
                    time.sleep(float(args[1]))
                elif kind == "event":
                    # wait event none timeout=S: until no event runs (one that holds on a line fails it), no keys sent.
                    timeout = float(next((a.split("=", 1)[1] for a in args[2:] if a.startswith("timeout=")), "60"))
                    until = time.monotonic() + timeout
                    while harness.game_state()["event"] != -1:
                        if time.monotonic() > until:
                            raise CaseFailure(f"an event still runs after {timeout:g}s")
                        time.sleep(1)
                    print(f"PASS {case}: no event runs")
                elif kind == "world":
                    options = {key: float(value) for key, value in (arg.split("=", 1) for arg in args[1:])}
                    try:
                        harness.wait_world_state(int(options["depth"]), int(options["state"]),
                                                 options.get("timeout", 15.0))
                    except RuntimeError as error:
                        raise CaseFailure(str(error)) from error
                    print(f"PASS {case}: world menu depth {int(options['depth'])}, state {int(options['state'])}")
                else:
                    raise CaseFailure("wait supports unit, entry, log, world, event, or seconds")
            elif command == "key":
                pause = 0.5
                for arg in args[1:]:
                    if arg.startswith("wait="):
                        pause = float(arg.split("=", 1)[1])
                for _ in range(int(next((a.split("=", 1)[1] for a in args[1:] if a.startswith("times=")), "1"))):
                    reply = harness.send(f"key {args[0]}")
                    if reply.startswith("error"):
                        raise CaseFailure(reply)
                    time.sleep(pause)
            elif command == "shot":
                path = SHOTS / path_name(case_file) / f"{args[0]}.png"
                harness.shot(argparse.Namespace(file=path))
            elif command == "defeat":
                chosen = harness.defeat([number(a) for a in args] or None)
                print(f"PASS {case}: defeated units {chosen}")
            elif command == "expect":
                kind = args[0].lower()
                if kind in ("unit", "entry", "roster"):
                    wanted = fields(args[1:])
                    count = wanted.pop("count", 1)
                    expect_record(kind, wanted, case, line_number, snapshots.get(kind), count)
                elif kind == "var":
                    variable, expected = number(args[1]), number(args[2])
                    reply = harness.send(f"var {variable}")
                    actual = number(reply.rsplit(" ", 1)[1])
                    if actual != expected:
                        raise CaseFailure(f"var 0x{variable:X}: expected 0x{expected:X}, got 0x{actual:X}")
                    print(f"PASS {case}: var 0x{variable:X}=0x{actual:X}")
                else:
                    raise CaseFailure("expect supports unit, entry, or var")
            else:
                raise CaseFailure(f"unknown command {command!r}")
        except (IndexError, ValueError) as error:
            raise CaseFailure(f"{path}:{line_number}: {error}") from error
        except CaseFailure as error:
            if str(error).startswith(f"{case}:") or str(error).startswith(str(path)):
                raise
            raise CaseFailure(f"{case}:{line_number}: {error}") from error
    print(f"PASS {case}: complete in {time.monotonic() - started:.1f}s")


def start_game(location: int | None = None) -> None:
    if harness.game_running():
        if not harness.answering():
            raise CaseFailure("the game is running without the quick-test harness; stop it first")
        return
    args = argparse.Namespace(world=True, script=None, timeout=120.0, keep_saves=False, location=location)
    harness.start(args)
    harness.wait_world_ready(160)


def final_frame(case: Path, outcome: str) -> None:
    """Every case's evidence: the hidden window as the case ends, work/harness/quick/CASE/pass.png (or fail.png), so a
    result can be checked by eye afterwards. Only a picture: nothing is driven from it."""
    if not harness.game_running():
        return
    try:
        harness.shot(argparse.Namespace(file=SHOTS / path_name(case) / f"{outcome}.png"))
    except (OSError, SystemExit) as error:
        print(f"  (no {outcome} frame: {error})")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("cases", nargs="+", type=Path)
    parser.add_argument("--start", action="store_true", help="start the hidden game if needed and wait for its world map")
    parser.add_argument("--restart", action="store_true", help="stop any prior hidden game, then start from a clean save")
    parser.add_argument("--stop", action="store_true", help="stop the hidden game after the cases")
    parser.add_argument("--location", type=lambda text: int(text, 0), default=None,
                        help="start the save at this world location (2 is Eagrose, which has a tavern)")
    args = parser.parse_args()
    try:
        if args.start and args.restart:
            raise CaseFailure("choose --start or --restart, not both")
        if args.restart and harness.game_running():
            harness.stop(argparse.Namespace())
        if args.start or args.restart:
            start_game(args.location)
        elif not harness.answering():
            raise CaseFailure("no warm harness game; pass --start")
        for case in args.cases:
            try:
                run_case(case)
            except CaseFailure:
                final_frame(case, "fail")
                raise
            final_frame(case, "pass")
        return 0
    except (CaseFailure, OSError) as error:
        print(f"FAIL {error}")
        return 1
    finally:
        if args.stop and harness.game_running():
            harness.stop(argparse.Namespace())


if __name__ == "__main__":
    raise SystemExit(main())
