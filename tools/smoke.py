"""A quick spot check of the riskiest parts of the mod in the game (about 50 minutes: the Argath check is half of it),
for after a merge: one game session on the PC through the harness, each check started from the world map of the test
save, and one contact sheet of its key frames per check for looking at beside ReMixed on the PS1.

    python tools/smoke.py [--only mission lich tavern rescue argath]     (Pillow is needed for the sheets: no -I)

The checks, in order (the game restarts before those that don't end on the world map):

- mission: Desert Minefield (1F2) start to finish with `harness battle`: the formation, Reveal Treasures, a mission's
  end and the Treasure Wheel.
- lich: the Lich at Zeklaus (1FC) and Ashley's scene after it (1FD): ReMixed's own sprites and faces, its lines in a
  battle, a guest joining the roster.
- tavern: a finished Mission 17 group of three, poked, then the dispatch tavern (355): its boxes, its report, the
  rewards' rows.
- rescue: side chapter 1-A's rescue (196), Hirame revived in its battle: ReMixed's battle conditions and its lines
  inside a battle (the branch where she lives).
- argath: the Argath campaign's start (300) from the test save, Algus felled, its second battle (301) to the world
  map, then 0x00A's battle (309) up to its formation and Commence: the campaign's roster, its guests, and whether the
  remaster lets its battles begin without Ramza.

Sheets and a log go to work/harness/smoke/; the game is stopped at the end. Ask before running it (docs/TESTING.md).
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import harness as h  # noqa: E402
from remixed import ROOT, WORK  # noqa: E402

OUT = WORK / "harness" / "smoke"
EVENTS = ROOT / "mod" / "fftivc.gameplay.ivaliceremixed" / "FFTIVC" / "data" / "enhanced" / "script" / "enhanced"
LENGTHS_SOURCE = ROOT / "code" / "eiu" / "src" / "EventInstructionUpgrade" / "EventScript.cs"
ROSTER, ROSTER_SIZE = 0x11A7D10, 600
T0 = time.time()
shots: dict[str, list[Path]] = {}
results: list[str] = []


def say(*parts) -> None:
    line = f"{time.time() - T0:5.0f} s " + " ".join(str(p) for p in parts)
    print(line, flush=True)
    with open(OUT / "smoke.log", "a", encoding="utf-8") as log:
        log.write(line + "\n")


def snap(check: str, tag: str) -> None:
    try:
        path = h.snap(f"smoke-{check}-{len(shots.setdefault(check, [])):02d}-{tag}")
        shots[check].append(path)
    except SystemExit:
        pass


def sheet(check: str) -> None:
    """The check's frames, cropped to the picture and side by side, four to a row."""
    from PIL import Image, ImageDraw
    frames = shots.get(check, [])
    if not frames:
        return
    tw, th = 640, 360
    rows = (len(frames) + 3) // 4
    out = Image.new("RGB", (4 * tw, rows * (th + 18)), "black")
    draw = ImageDraw.Draw(out)
    for k, path in enumerate(frames):
        im = Image.open(path).convert("RGB")
        if im.size == (1720, 720):
            im = im.crop((220, 0, 1500, 720))
        x, y = (k % 4) * tw, (k // 4) * (th + 18)
        out.paste(im.resize((tw, th)), (x, y + 18))
        draw.text((x + 4, y + 3), path.stem.removeprefix(f"smoke-{check}-"), fill="yellow")
    out.save(OUT / f"{check}.png")
    say(f"{check}: sheet {OUT / f'{check}.png'}")


def harness(*args: str, timeout: float = 600) -> str:
    run = subprocess.run([sys.executable, "-I", str(Path(__file__).with_name("harness.py")), *args],
                         capture_output=True, text=True, timeout=timeout)
    return (run.stdout + run.stderr).strip()


def restart() -> None:
    harness("stop")
    say("start:", harness("start", "--world").splitlines()[-1][:80])
    if h.wait_log("autopilot: the script is done", 240, fresh=False) is None or not h.on_world_map(60):
        raise SystemExit("the world map never came")


def state() -> dict:
    if not h.game_running():
        raise SystemExit("the game is gone")
    return h.game_state()


def menu_waits(event: int) -> list[int]:
    """Where the built event's menus wait (the harness state's "ReMixed event at"), in order."""
    text = LENGTHS_SOURCE.read_text(encoding="utf-8")
    hexes = re.search(r"Lengths = Widened\(Convert\.FromHexString\((.*?)\)\);", text, re.S).group(1)
    lengths = bytearray.fromhex("".join(re.findall(r'"([0-9A-Fa-f]+)"', hexes)))
    lengths[0x10] += 3
    lengths[0x51] += 2
    data = (EVENTS / f"event{event:03d}.e").read_bytes()[4:]
    at, waits = 0, []
    while at < len(data) and data[at] != 0xDB:
        size = 1 + lengths[data[at]]
        if data[at] == 0x10 and size >= 16 and data[at + 2] & 0x04 and data[at + 2] != 0x0B:
            waits.append(at + 8 + size)
        at += size
    return waits


def press_until(check: str, test, limit: float, every: float = 5, answers: dict[int, int] | None = None,
                snap_every: int = 3) -> dict | None:
    """Enter every few seconds until test(state) holds; a menu waiting at a known place gets its answer (Down that
    many times less one, then Enter). A frame every few presses, and at each change of event."""
    answers = dict(answers or {})
    end, last, k = time.time() + limit, None, 0
    while time.time() < end:
        s = state()
        if (s["event"], s["scenario"]) != last:
            say(f"{check}:", s["text"][:110])
            snap(check, f"e{s['event']}")
            last = (s["event"], s["scenario"])
        if test(s):
            return s
        if s["pc"] in answers:
            time.sleep(4)
            for _ in range(answers.pop(s["pc"]) - 1):
                h.send("key DOWN")
                time.sleep(1.5)
            snap(check, "menu")
        elif k % snap_every == 0:
            snap(check, "step")
        h.send("key RETURN")
        k += 1
        time.sleep(every)
    say(f"{check}: not reached in {limit:.0f} s")
    return None


def battle_on() -> callable:
    """A test that holds once ReMixed's code sits at the same END twice (its battle is on)."""
    seen = {"pc": None, "n": 0}

    def test(s: dict) -> bool:
        seen["n"] = seen["n"] + 1 if s["op"] == 0xDB and s["pc"] == seen["pc"] else 0
        seen["pc"] = s["pc"]
        return seen["n"] >= 2
    return test


# The mod's line as a battle begins, in either wording (LevelScaling.cs: "the N deployed units' highest level", or
# with the test mode's redo "P n (the roster's) → m (the N deployed units'").
BATTLE_BEGUN = r"trace level scaling: .*the \d+ deployed units"
LINE_SHOWN = r"remixed scenarios: event \d+'s message \d+ shown as line"  # each of ReMixed's lines, the first time


def commence(check: str, units: int, ask_wait: float = 60) -> bool:
    """A scene-first battle's formation: Reveal Treasures declined if it asks, units placed, Space, F once. Whether
    the battle began (the mod's level-scaling line; ReMixed's scene-first battles load no event as they begin)."""
    begun = h.count_log(BATTLE_BEGUN)
    free = h.count_log(h.REVEAL_FREE)
    if ask_wait:
        h.wait_until(lambda: h.reveal_asking() or h.count_log(h.REVEAL_FREE) > free, ask_wait)
    time.sleep(6)
    h.answer_reveal("no")
    for _ in range(units):
        h.place("no")
        h.place("no")
    snap(check, "placed")
    h.send("key SPACE")
    time.sleep(3)
    snap(check, "commence")
    h.send("key F")
    # 60 s: 1-A's rescue began 20 s after F (its arrows off at once, its opening's set 20 s later, the level-scaling
    # line after that), and a wait of 20 stopped it there (2026-11-13).
    began = h.wait_log(BATTLE_BEGUN, 60, before=begun) is not None
    snap(check, "began" if began else "not-begun")
    return began


def to_world(check: str, limit: float = 900) -> bool:
    """Through scenes and battles (their enemies felled once each battle waits) to the world map."""
    end, test, last = time.time() + limit, battle_on(), None
    while time.time() < end:
        s = state()
        if (s["event"], s["scenario"]) != last:
            say(f"{check}:", s["text"][:110])
            snap(check, f"e{s['event']}")
            last = (s["event"], s["scenario"])
        if s["event"] == -1 and h.on_world_map(8):
            return True
        if test(s):
            snap(check, "battle")
            say(f"{check}: felled", h.defeat(None))
            test = battle_on()
            time.sleep(10)
            continue
        h.send("key RETURN")
        time.sleep(6)
    say(f"{check}: no world map in {limit:.0f} s")
    return False


def unit_line(sprites: int) -> tuple[int, int] | None:
    """The battle unit drawn with this sprite set: its index and address."""
    base, listed = h.battle_units()
    for u in listed:
        if f"sprites 0x{sprites:02X} " in u["line"]:
            return u["index"], base + u["index"] * h.UNIT_SIZE
    return None


# ---- the checks ---------------------------------------------------------------------------------------------------

def check_mission() -> None:
    out = harness("battle", "1F2", "--units", "1", "--second", "1", timeout=900)
    say("mission:", " | ".join(line.strip()[:90] for line in out.splitlines() if re.search(
        r"starting|reveal|commenced|loading event|defeated|done:|not reached|didn't", line))[:900])
    snap("mission", "end")
    for name in ("formation-0-start", "formation-1-commence"):
        path = h.SHOTS / f"{name}.png"
        if path.exists():
            shots["mission"].insert(0, path)
    results.append("mission: " + ("world map again" if "done:" in out else "DIDN'T FINISH"))


def check_lich() -> None:
    h.send("scenario 1FC")
    say("lich: commenced", h.formation(1, 0, "no", ask_wait=60))
    press_until("lich", battle_on(), 400)
    snap("lich", "battle")
    for _ in range(3):  # the unit cards as the turns go
        time.sleep(8)
        snap("lich", "turn")
    joined = h.count_log(r"roster guard: an empty battle unit")
    ok = to_world("lich")
    empty = h.count_log(r"roster guard: an empty battle unit") > joined
    results.append(f"lich: {'world map again' if ok else 'STUCK'}; Ashley "
                   + ("an EMPTY unit (not joined)" if empty else "no empty unit logged"))


def check_tavern() -> None:
    for command in ("setvar 0x6F 5", "poke +0xD407B2 01", "poke +0x11A71A0 051100076400010203", "setvar 0xD4 1",
                    "scenario 355"):
        h.send(command)
    reached = press_until("tavern", lambda s: s["scenario"] == 0 and s["event"] == -1, 240, every=4, snap_every=1)
    results.append("tavern: " + ("its report ran through" if reached else "DIDN'T END"))


def check_rescue() -> None:
    h.send("setvar 0x6E 4")
    h.send("setvar 0x2EC 1")
    setups = h.count_log(r"scenario 0x196 sets its battle up")
    h.send("scenario 196")
    answers = {pc: 2 for pc in menu_waits(406)[:1]}  # its question: fight
    if press_until("rescue", lambda s: h.count_log(r"scenario 0x196 sets its battle up") > setups, 400,
                   answers=answers) is None:
        results.append("rescue: NO BATTLE after its scene")
        return
    if not commence("rescue", 1):
        results.append("rescue: its battle DIDN'T BEGIN")
        return
    time.sleep(10)
    ramza, hirame = unit_line(0x01), unit_line(0x59)
    if hirame is None:
        results.append("rescue: Hirame NOT FOUND in the battle")
        return
    if ramza:
        h.send(f"poke 0x{ramza[1] + h.UNIT_HP:X} E7 03 E7 03")  # the test mode's AI keeps him alive
    snap("rescue", "battle")
    status = h.peek_bytes(hirame[1] + h.UNIT_STATUS, 1)[0]
    copy = h.peek_bytes(hirame[1] + h.UNIT_STATUS_COPY, 1)[0]
    h.send(f"poke 0x{hirame[1] + h.UNIT_STATUS:X} {status & ~h.DEAD & 0xFF:02X}")
    h.send(f"poke 0x{hirame[1] + h.UNIT_STATUS_COPY:X} {copy & ~h.DEAD & 0xFF:02X}")
    say(f"rescue: Hirame (unit {hirame[0]}) revived from status 0x{status:02X}")
    end, seen, scenes, test = time.time() + 600, {}, set(), battle_on()
    # A place counts towards a loop only while no new line shows: ReMixed's scenes wait in one shared function (0x112's
    # FUNCTION b0, after every line), and 0x352's opening takes 30 s before its first line, as on the PS1 (2026-11-13).
    lines = h.count_log(LINE_SHOWN)
    while time.time() < end:
        s = state()
        scenes.add(s["scenario"])
        if h.count_log(LINE_SHOWN) != lines:
            lines, seen = h.count_log(LINE_SHOWN), {}
        if s["event"] == -1 and h.on_world_map(8):
            results.append(f"rescue: on to the world map (scenarios {sorted(hex(x) for x in scenes)}); its lines on "
                           "the sheet")
            return
        if test(s):  # back in the battle once its scene is over: its enemies felled
            snap("rescue", "battle")
            say("rescue: felled", h.defeat(None))
            test = battle_on()
            time.sleep(10)
            continue
        if s["op"] != 0xDB:  # a scene: the same two places again and again is the empty box's loop
            key = (s["event"], s["pc"])
            seen[key] = seen.get(key, 0) + 1
            if seen[key] in (1, 4):
                snap("rescue", f"s{s['scenario']:X}")
            if seen[key] >= 15:
                say(f"rescue: the same place 15 times with no new line (event {s['event']}, 0x{s['pc']:X}): a loop")
                results.append(f"rescue: LOOPS after the revival (scenarios {sorted(hex(x) for x in scenes)})")
                return
        h.send("key RETURN")
        time.sleep(4)
    results.append(f"rescue: no world map in time (scenarios {sorted(hex(x) for x in scenes)})")


def check_argath() -> None:
    h.send("setvar 0xAF 0")
    h.send("scenario 300")
    if press_until("argath", battle_on(), 600) is None:
        results.append("argath: 0x300's battle never began")
        return
    time.sleep(8)
    say("argath: Algus felled", h.defeat([0]))
    if press_until("argath", lambda s: s["event"] == 769, 400) is None:
        results.append("argath: no 0x301 after Algus fell")
        return
    if not to_world("argath", 1200):
        results.append("argath: 0x301 DIDN'T reach the world map")
        return
    say("argath:", h.send("var 0x6E"), h.send("var 0xAF"))
    h.send("setvar 0x6E 4")
    setups = h.count_log(r"scenario 0x309 sets its battle up")
    h.send("scenario 309")
    if press_until("argath", lambda s: h.count_log(r"scenario 0x309 sets its battle up") > setups, 600) is None:
        results.append("argath: no formation after 0x309's scene")
        return
    base = h.image_base() + ROSTER
    marks = [h.peek_bytes(base + slot * ROSTER_SIZE + 0x11E, 1)[0] for slot in (0, 10)]
    say(f"argath: roster +0x11E: slot 0 (Algus) 0x{marks[0]:02X}, slot 10 (Ramza) 0x{marks[1]:02X}")
    # Seen 2026-11-13: with a place left empty the remaster answered "Ramza is required but has not been placed.";
    # with every place filled it asked "Commence battle?". So both: two units first, then the rest.
    partly = commence("argath", 2)
    began = partly
    if not partly:
        # The refused Commence's F leaves the unit list up with the next unit picked. Enter places it (F there opened
        # the unit's status screen on 2026-11-13), and with every place filled the remaster asks "Commence battle?"
        # by itself; neither Enter nor F answered that question 5 s after it opened (2026-11-13, twice). So each way
        # in turn, until the battle begins: F 8 s after it opens, then Enter, then backed out of and Commence asked
        # for as `harness formation` does it (Space, 4 s, F).
        before = h.count_log(BATTLE_BEGUN)
        h.send("key RETURN")
        time.sleep(8)
        snap("argath", "filled")
        ways = [("F, 8 s after the question", ["key F"]), ("Enter", ["key RETURN"]),
                ("Space then F", ["key BACK", "key SPACE", "key F"])]
        how = None
        for name, keys in ways:
            for key in keys:
                h.send(key)
                time.sleep(4)
            snap("argath", name.split(",")[0].replace(" ", "-"))
            if h.wait_log(BATTLE_BEGUN, 20, before=before) is not None:
                how = name
                break
        began = how is not None
        snap("argath", "began" if began else "not-begun")
        say(f"argath: with the places filled, {'began by ' + how if began else 'no way in began it'}")
    results.append(f"argath: 0x309's battle {'began' if partly else 'NOT begun'} with two units placed, "
                   f"{'began' if began else 'NOT begun'} with the places filled; +0x11E Algus 0x{marks[0]:02X}, "
                   f"Ramza 0x{marks[1]:02X}")


# name -> (check, restart before it). Every check after the first starts from a fresh world map: after a battle at a
# battle location, a scenario asked for can raise "Enemy encountered!" instead, whose Fight the presses take (seen
# 2026-11-13: the Lich's check played a random battle at Mandalia after the mission's).
CHECKS = {"mission": (check_mission, True), "lich": (check_lich, True), "tavern": (check_tavern, True),
          "rescue": (check_rescue, True), "argath": (check_argath, True)}


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument("--only", nargs="*", choices=list(CHECKS), help="these checks only, in the usual order")
    args = p.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "smoke.log").write_text("", encoding="utf-8")
    chosen = [name for name in CHECKS if not args.only or name in args.only]
    try:
        restart()
        for k, name in enumerate(chosen):
            check, fresh = CHECKS[name]
            if fresh and k > 0:
                restart()
            say(f"== {name}")
            try:
                check()
            except SystemExit as stop:
                results.append(f"{name}: STOPPED ({stop})")
                if not h.game_running():
                    restart()
            sheet(name)
    finally:
        harness("stop")
        say("crashes:", harness("crashes").splitlines()[:2])
        say("== results")
        for line in results:
            say(line)


if __name__ == "__main__":
    main()
