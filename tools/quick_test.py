"""Run small, named in-game checks against one warm harness process.

Case files live in tests/quick and contain one command per line. Values use Python-style integers (0x91, 12):

    name Lorne scene looks
    scenario 0x194
    wait unit battle_id=0x91 timeout=20
    expect unit battle_id=0x91 job=0x5b graphic=0x38

For a picture to check by eye, `shot NAME` saves the hidden window to work/harness/quick/CASE/NAME.png at that step;
every case also leaves pass.png (or fail.png) there as it ends. Pictures are evidence only: no step reads them.
`scenario 0x79 noted` starts a scene-first battle at its battle (its scene counted as seen). `defeat [UNIT...]`
downs the enemies (or those units); `expect roster name_id=0x76` wants exactly one roster record so.
`key K` posts one key to the game's own window (never the desktop's input). `wait log PATTERN press=RETURN every=1`
confirms through dialogue until a log line comes; `wait world depth=D state=S` waits for a world-map menu.

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
    return all(item.get(key) == value for key, value in wanted.items())


def describe(wanted: dict[str, int]) -> str:
    return " ".join(f"{key}=0x{value:X}" for key, value in wanted.items())


def roster_records(data: bytes | None = None) -> list[dict]:
    """The roster records in use: slot, sprite set and name id (harness.cmd_roster's fields)."""
    if data is None:
        data = harness.peek_bytes(harness.image_base() + harness.ROSTER, harness.ROSTER_SIZE * harness.ROSTER_COUNT)
    found = []
    for k in range(len(data) // harness.ROSTER_SIZE):
        r = data[k * harness.ROSTER_SIZE:(k + 1) * harness.ROSTER_SIZE]
        if r[1] == 0xFF or not any(r[:4]):
            continue
        found.append({"slot": k, "sprites": r[0],
                      "name_id": int.from_bytes(r[harness.ROSTER_NAME_ID:harness.ROSTER_NAME_ID + 4], "little")})
    return found


def records(kind: str) -> list[dict]:
    if kind == "roster":
        return roster_records()
    return harness.battle_units()[1] if kind == "unit" else harness.battle_entries()


def find(kind: str, wanted: dict[str, int], items: list[dict] | None = None) -> list[dict]:
    items = records(kind) if items is None else items
    return [item for item in items if matches(item, wanted)]


def expect_record(kind: str, wanted: dict[str, int], case: str, line: int, items: list[dict] | None = None) -> None:
    found = find(kind, wanted, items)
    label = f"{kind} {describe(wanted)}"
    if len(found) != 1:
        raise CaseFailure(f"{case}:{line}: {label}: expected exactly one, found {len(found)}")
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
        words = shlex.split(source, comments=True)
        command, args = words[0].lower(), words[1:]
        try:
            if command == "name":
                case = " ".join(args)
            elif command == "scenario":
                if not harness.on_world_map(5):
                    raise CaseFailure("scenario requires the idle world map")
                scenario = number(args[0])
                noted = " noted" if len(args) > 1 and args[1].lower() == "noted" else ""
                reply = harness.send(f"scenario {scenario:X}{noted}")
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
            elif command == "advance":
                timeout = 45.0
                positional = []
                for arg in args:
                    if arg.startswith("timeout="):
                        timeout = float(arg.split("=", 1)[1])
                    else:
                        positional.append(arg)
                reached = " ".join(positional)
                result = harness.advance(reached, timeout, every=0.25)
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
                    expect_record(kind, fields(args[1:]), case, line_number, snapshots.get(kind))
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
