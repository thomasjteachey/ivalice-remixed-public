"""Drive the running game on this PC for the mod's tests: started once, unseen and unheard, then told what to do.

    python -I tools/harness.py start --world --location 2                # boots a disposable save at Eagrose
    python -I tools/harness.py send key F                                  # one command, its answer printed
    python -I tools/harness.py send peek +0x782A94 4
    python -I tools/harness.py shot work/harness/now.png                   # a screenshot of its window
    python -I tools/harness.py dispatch all --file work/harness/dispatch.png # enter the test fixture's Tavern once,
                                                       # assert every catalog row and capture only the final view
    python -I tools/harness.py dispatch all --cycles 10                    # regression check for the navigation lock
    python -I tools/harness.py log                                         # the mod's log lines since the start
    python -I tools/harness.py stop                                        # ends it and cleans up

Steps for battles, built on `send` (each prints what it saw; TESTING.md has the flow):

    python -I tools/harness.py checkpoint 34D 7C 5 --messages 30 49  # jump to event 124's post-battle phase and
                                                       # capture its exact messages, without playing the battle
    python -I tools/harness.py checkpoint 34D 7C --victory --advance-phase 2 4 --dismiss-end-phase 2 \
        --messages 30 49 --after-event
                                                       # defeat the carrier's enemies, let its real victory condition
                                                       # select source event 124 inside runtime carrier 0x34D, capture
                                                       # its boxes, then one frame after it
    python -I tools/harness.py battle 1F2 --integration --units 1 --second 1  # final integration only: started
                                                       # from the world map, Reveal Treasures declined (--reveal yes
                                                       # pays), units placed in both groups, its opening, its enemies
                                                       # defeated, its closing and the Treasure Wheel, until the world
                                                       # map runs again
    python -I tools/harness.py formation --units 2     # place units and commence (--second 1: a second group too)
    python -I tools/harness.py advance --until opening # Enter through a scene; --until world never presses on the map
                                                       # (on the Treasure Wheel, Right once Enter changes nothing)
    python -I tools/harness.py defeat [0 1 2]          # HP 0 and the dead bit for the enemy team (or these units)
    python -I tools/harness.py wait "loading event 498" --timeout 60   # a log line (a regex) appearing from now on
    python -I tools/harness.py roster                  # the remaster's roster records in use (name ids, names)
    python -I tools/harness.py slots                   # the battle units' roster slots, flags and battle ids
    python -I tools/harness.py event 34D 2             # force an in-battle event phase and screenshot its first box
    python -I tools/harness.py crashes                 # the game's crashes in Windows' log, as RVAs in functions
    python -I tools/harness.py send watch +0x11A7D10 8 # catch what writes there (a hardware watch, up to four)
    python -I tools/harness.py send watch              # the writes caught, with the exe addresses on each one's stack

`start` runs the game through Reloaded-II on a desktop of its own (tools/headless_pc.py: Windows never shows it, and the
autopilot file mutes the game), with an autopilot script beside the mod (--world: Enter until the world map), and
returns once the mod's pipe answers. It first puts the mod's save folder back as work/harness/saves has it (see
restore_saves; --keep-saves to skip). The commands `send` takes are code/IvaliceRemixed/Harness.cs's (`help` lists
them): the autopilot's steps (key, press, scenario, units, ...) and state, peek, poke, var, setvar, find. `stop` ends
the game and Reloaded-II and removes the script; always stop a started game when done.
"""

from __future__ import annotations

import argparse
from collections import Counter
import ctypes
import ctypes.wintypes as wt
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import time
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from headless_pc import (DESKTOP, GENERIC_ALL, MOD_ID, PROCESS_INFORMATION, STARTUPINFOW, end,  # noqa: E402
                         game_running, reloaded, screenshot)
from make_story_regression_saves import (EVENTS as SAVE_EVENTS, HEADER as SAVE_HEADER,  # noqa: E402
                                         SLOT as SAVE_SLOT, SLOTS as SAVE_SLOTS,
                                         WORLD_LOCATION as SAVE_WORLD_LOCATION,
                                         WORLD_LOCATION_SUMMARY as SAVE_WORLD_LOCATION_SUMMARY)
from remixed import WORK, load_config  # noqa: E402

PIPE = r"\\.\pipe\ivalice-remixed"
STATE = WORK / "harness" / "state.json"
LOGS = Path(os.environ["APPDATA"]) / "Reloaded-Mod-Loader-II" / "Logs"
# The mod's own saves on this PC (test copies; the player's are on the Deck), and the set each start puts back, so a
# run's autosave (the game makes one when a battle starts, and Continue loads the newest) doesn't carry into the next.
SAVES = Path(os.environ["USERPROFILE"]) / "Documents" / "My Games" / "FINAL FANTASY TACTICS - Ivalice ReMixed" / "Steam"
START_SAVES = WORK / "harness" / "saves"


def patch_save_data_location(data: bytearray, location: int) -> int:
    """Put every occupied manual-save slot at one world node and rebuild its checksum."""
    if len(data) != SAVE_HEADER + SAVE_SLOTS * SAVE_SLOT or struct.unpack_from("<I", data, 0)[0] != SAVE_HEADER:
        raise ValueError("not the enhanced manual-save layout")
    if zlib.crc32(data[SAVE_HEADER:]) != struct.unpack_from("<I", data, 4)[0]:
        raise ValueError("the manual save's CRC does not match")
    changed = 0
    for slot in range(SAVE_SLOTS):
        base = SAVE_HEADER + slot * SAVE_SLOT
        if data[base:base + 2] != b"SC":
            continue
        variable = base + SAVE_EVENTS + 0x31 * 4
        struct.pack_into("<I", data, variable, location)
        data[base + SAVE_WORLD_LOCATION_SUMMARY] = location
        struct.pack_into("<I", data, base + SAVE_WORLD_LOCATION, location)
        changed += 1
    if not changed:
        raise ValueError("the manual save has no occupied slots")
    struct.pack_into("<I", data, 4, zlib.crc32(data[SAVE_HEADER:]))
    return changed


def patch_packed_save_location(path: Path, location: int) -> int:
    """Patch one restored private harness save through the repository's configured save packer."""
    tool = load_config().get("tools", {}).get("ff16tools")
    if not tool:
        raise RuntimeError("Set tools.ff16tools in local.toml to use --location")
    with tempfile.TemporaryDirectory() as temporary:
        work = Path(temporary)
        unpacked = work / "save"
        subprocess.run([tool, "unpack-save", "-i", path.resolve(), "-o", unpacked], check=True,
                       capture_output=True)
        save = unpacked / "fftsave.bin"
        data = bytearray(save.read_bytes())
        changed = patch_save_data_location(data, location)
        save.write_bytes(data)
        output = work / "patched.png"
        subprocess.run([tool, "pack-save", "-i", unpacked, "-o", output, "-s", "-g", "fft"], check=True,
                       capture_output=True)
        shutil.copy2(output, path)
    return changed


def restore_saves(location: int | None = None) -> str:
    """The save folder as START_SAVES has it, optionally relocated only in its disposable restored copy."""
    folders = [p for p in SAVES.iterdir() if p.is_dir()] if SAVES.exists() else []
    if len(folders) != 1:
        if location is not None:
            raise RuntimeError(f"--location needs exactly one PC test-save folder, found {len(folders)}")
        return f"saves left as they are ({len(folders)} save folders)"
    folder = folders[0]
    if not START_SAVES.exists():
        shutil.copytree(folder, START_SAVES)
        result = f"saves as they are, kept in {START_SAVES} for the next starts"
    else:
        for path in START_SAVES.iterdir():
            shutil.copy2(path, folder / path.name)
        result = f"saves put back from {START_SAVES}"
    if location is not None:
        if not 0 <= location <= 255:
            raise ValueError("--location must be 0..255")
        changed = patch_packed_save_location(folder / "enhanced.png", location)
        auto = folder / "autoenhanced.png"
        if auto.exists():
            auto.replace(folder / "autoenhanced.png.harness-disabled")
        result += f"; {changed} manual slot(s) relocated to world location {location} (autosave disabled for this run)"
    else:
        (folder / "autoenhanced.png.harness-disabled").unlink(missing_ok=True)
    return result


def send(command: str, timeout: float = 120) -> str:
    """The game's answer to one command."""
    deadline = time.time() + timeout
    while True:
        try:
            pipe = open(PIPE, "r+b", buffering=0)  # noqa: SIM115
            break
        except OSError:
            if time.time() > deadline or not game_running():  # no game: no point waiting for its pipe
                raise
            time.sleep(0.5)
    with pipe:
        pipe.write(command.encode("utf-8") + b"\n")
        reply = b""
        while not reply.endswith(b"\n.\n"):
            chunk = pipe.read(65536)
            if not chunk:
                break
            reply += chunk
    return reply.decode("utf-8", errors="replace").removesuffix("\n.\n")


def answering() -> bool:
    try:
        send("state", timeout=0)
        return True
    except OSError:
        return False


def start(args: argparse.Namespace) -> None:
    if game_running():
        sys.exit("the game is already running (stop it first)")
    cfg = load_config()
    game = Path(cfg["target"]["tic_dir"]) / "FFT_enhanced.exe"
    launcher, mods = reloaded()
    mod = mods / MOD_ID
    (mod / "crash.txt").unlink(missing_ok=True)
    if args.keep_saves and args.location is not None:
        sys.exit("--location needs the disposable restored save; remove --keep-saves")
    if not args.keep_saves:
        print(restore_saves(args.location))
    if args.script:
        shutil.copy(args.script, mod / "autopilot.txt")
    else:
        (mod / "autopilot.txt").write_text("# the harness\n" + ("until-world 150\n" if args.world else ""), encoding="utf-8")
    STATE.parent.mkdir(parents=True, exist_ok=True)
    STATE.write_text(json.dumps({"logs": sorted(p.name for p in LOGS.glob("*.txt")), "started": time.time()}))

    user32, kernel32 = ctypes.WinDLL("user32", use_last_error=True), ctypes.WinDLL("kernel32", use_last_error=True)
    user32.CreateDesktopW.restype = wt.HANDLE
    desktop = user32.CreateDesktopW(DESKTOP, None, None, 0, GENERIC_ALL, None)
    if not desktop:
        sys.exit(f"CreateDesktop failed: {ctypes.get_last_error()}")
    si = STARTUPINFOW(cb=ctypes.sizeof(STARTUPINFOW), lpDesktop=DESKTOP)
    pi = PROCESS_INFORMATION()
    command = f'"{launcher}" --launch "{game}"'
    if not kernel32.CreateProcessW(None, ctypes.create_unicode_buffer(command), None, None, False, 0, None,
                                   str(launcher.parent), ctypes.byref(si), ctypes.byref(pi)):
        sys.exit(f"CreateProcess failed: {ctypes.get_last_error()}")
    kernel32.CloseHandle(pi.hThread)
    kernel32.CloseHandle(pi.hProcess)
    # This process holds the desktop until the game is on it (the desktop goes when nothing uses it).
    started = time.time()
    while time.time() - started < args.timeout:
        if answering():
            print(f"the game answers after {time.time() - started:.0f} s: {send('state')}")
            return
        time.sleep(2)
    print(f"no answer after {args.timeout} s; see `log`, or `stop`")


def shot(args: argparse.Namespace) -> None:
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    user32.OpenDesktopW.restype = wt.HANDLE
    desktop = user32.OpenDesktopW(DESKTOP, 0, False, GENERIC_ALL)
    if not desktop:
        sys.exit(f"no hidden desktop ({ctypes.get_last_error()}): is the game started?")
    args.file.parent.mkdir(parents=True, exist_ok=True)
    print(screenshot(desktop, args.file), args.file)
    user32.CloseDesktop(desktop)


def log(args: argparse.Namespace) -> None:
    seen = set(json.loads(STATE.read_text())["logs"]) if STATE.exists() else set()
    for path in sorted((p for p in LOGS.glob("*.txt") if p.name not in seen), key=lambda p: p.stat().st_mtime):
        lines = [line for line in path.read_text(encoding="utf-8", errors="replace").splitlines()
                 if args.all or "[Ivalice ReMixed]" in line or "rror" in line]
        print(f"--- {path.name}")
        print("\n".join(line[:400] for line in lines[-args.lines:]))


def stop(_: argparse.Namespace) -> None:
    end("FFT_enhanced.exe")
    end("Reloaded-II.exe")
    _, mods = reloaded()
    mod = mods / MOD_ID
    (mod / "autopilot.txt").unlink(missing_ok=True)
    crash = mod / "crash.txt"
    print("stopped" + (f"; crash.txt:\n{crash.read_text(encoding='utf-8')}" if crash.exists() else ""))


# ---- steps for battles -------------------------------------------------------------------------------------------

def nap(seconds: float) -> None:
    time.sleep(seconds)


def log_lines() -> list[str]:
    """The mod's log lines since `start` (the logs that weren't there before it)."""
    seen = set(json.loads(STATE.read_text())["logs"]) if STATE.exists() else set()
    out = []
    for path in sorted((p for p in LOGS.glob("*.txt") if p.name not in seen), key=lambda p: p.stat().st_mtime):
        out += [line for line in path.read_text(encoding="utf-8", errors="replace").splitlines()
                if "[Ivalice ReMixed]" in line]
    return out


def all_log_lines() -> list[str]:
    """The last few logs' lines, whenever they were written."""
    out = []
    for path in sorted(LOGS.glob("*.txt"), key=lambda p: p.stat().st_mtime)[-3:]:
        out += path.read_text(encoding="utf-8", errors="replace").splitlines()
    return out


def count_log(pattern: str, lines: list[str] | None = None) -> int:
    """How many of the mod's log lines since `start` match pattern (a regex)."""
    regex = re.compile(pattern)
    return sum(1 for line in (log_lines() if lines is None else lines) if regex.search(line))


def wait_log(pattern: str, timeout: float, fresh: bool = True, before: int | None = None) -> str | None:
    """The first log line matching pattern (a regex), from now on unless fresh is False (or after the first `before`
    of them, counted before a key that makes one); None after timeout."""
    regex = re.compile(pattern)
    if before is None:
        before = count_log(pattern) if fresh else 0
    end_at = time.time() + timeout
    while True:
        hits = [line for line in log_lines() if regex.search(line)]
        if len(hits) > before:
            return hits[before]
        if time.time() > end_at:
            return None
        nap(2)


def game_state() -> dict:
    """`state`, parsed: world frames, running event, condition phase (var 1), scenario (var 0x27), the last ReMixed
    instruction's pc/op, and the unparsed text."""
    text = send("state")

    def field(regex: str, base: int = 10) -> int:
        m = re.search(regex, text)
        return int(m.group(1), base) if m else -1
    return {"world": field(r"world frames (\d+)"), "event": field(r"running event (-?\d+)"),
            "phase": field(r"var 0x1 = 0x([0-9A-F]+)", 16),
            "scenario": field(r"var 0x27 = 0x([0-9A-F]+)", 16), "pc": field(r"event at 0x([0-9A-F]+)", 16),
            "op": field(r"\(op 0x([0-9A-F]+)\)", 16), "text": text}


def battle_units() -> tuple[int, list[dict]]:
    """The battle units' base address and the fields printed by the in-game harness."""
    text = send("units")
    m = re.search(r"battle units at 0x([0-9A-F]+)", text)
    base = int(m.group(1), 16) if m else 0
    found = []
    for line in text.splitlines():
        u = re.search(r"unit (\d+): sprites 0x([0-9A-F]+) id 0x([0-9A-F]+) job 0x([0-9A-F]+) "
                      r"team 0x([0-9A-F]+) skillsets 0x([0-9A-F]+) 0x([0-9A-F]+) level (\d+) "
                      r"graphic 0x([0-9A-F]+) palette 0x([0-9A-F]+) battle id 0x([0-9A-F]+) name 0x([0-9A-F]+)",
                      line)
        if u:
            # The sprite drawing it (newer DLLs): the palette it's drawn in and the unit's own (+4), -1 without.
            s = re.search(r"sprite \d+ graphic 0x[0-9A-F]+ palette (\d+) \(unit \+4 (\d+)\)", line)
            found.append({"index": int(u.group(1)), "sprites": int(u.group(2), 16), "id": int(u.group(3), 16),
                          "job": int(u.group(4), 16), "team": int(u.group(5), 16),
                          "primary": int(u.group(6), 16), "secondary": int(u.group(7), 16),
                          "level": int(u.group(8)), "graphic": int(u.group(9), 16),
                          "palette": int(u.group(10), 16), "battle_id": int(u.group(11), 16),
                          "name": int(u.group(12), 16),
                          "sprite_palette": int(s.group(1)) if s else -1,
                          "palette_byte": int(s.group(2)) if s else -1,
                          "line": line[line.index("unit "):]})
    return base, found


def battle_entries() -> list[dict]:
    """The current scenario's raw battle-entry records, including their unit-selection flags."""
    found = []
    for line in send("entries").splitlines():
        entry = re.search(r"entry (\d+): sprites 0x([0-9A-F]+) flags 0x([0-9A-F]+) name 0x([0-9A-F]+) "
                          r"job 0x([0-9A-F]+) secondary 0x([0-9A-F]+) team 0x([0-9A-F]+) "
                          r"x 0x([0-9A-F]+) y 0x([0-9A-F]+) battle id 0x([0-9A-F]+)", line)
        if entry:
            found.append({"index": int(entry.group(1)), "sprites": int(entry.group(2), 16),
                          "flags": int(entry.group(3), 16), "name": int(entry.group(4), 16),
                          "job": int(entry.group(5), 16), "secondary": int(entry.group(6), 16),
                          "team": int(entry.group(7), 16), "x": int(entry.group(8), 16),
                          "y": int(entry.group(9), 16), "battle_id": int(entry.group(10), 16),
                          "line": line[line.index("entry "):]})
    return found


UNIT_SIZE, UNIT_HP, UNIT_STATUS, UNIT_STATUS_COPY, DEAD = 0x200, 0x30, 0x61, 0x1EF, 0x20
# Reraise: the third status byte's 0x20, in the innate statuses (+0x57, the PS1's +0x4E), the current ones and their copy.
UNIT_INNATE, RERAISE_BYTE, RERAISE = 0x57, 2, 0x20
# The team bits of a unit's team byte (unit +5, the battle data's +0x18): 0 the player's side (the party 0x00-0x0B,
# guests 0x84), 1 and 2 the enemy teams (0x90, 0x95; 0xA0 in side chapter 1-A's first battle).
TEAM_BITS = 0x30


def image_base() -> int:
    return int(re.match(r"0x([0-9A-F]+)", send("peek +0x0 1")).group(1), 16)


def peek_bytes(address: int, count: int) -> bytes:
    """count bytes at an absolute address, from `peek`'s hex dump (0x4000 at a time)."""
    out = bytearray()
    while len(out) < count:
        chunk = min(0x4000, count - len(out))
        text = send(f"peek 0x{address + len(out):X} {chunk}")
        if text.startswith("error"):
            raise OSError(text)
        before = len(out)
        for line in text.splitlines()[1:]:
            out += bytes.fromhex(line[6:6 + 47].strip())
        if len(out) == before:
            raise OSError(f"peek 0x{address + before:X} gave no bytes: {text[:80]!r}")
    return bytes(out[:count])


def defeat(units: list[int] | None) -> list[int]:
    """HP 0 and the dead status bit (+0x61 and its copy at +0x1EF) for the given units, or for the enemies, and no
    Reraise (innate or current), so they stay down. A battle's own events (ReMixed's all) run after the turn, not between
    actions, and by then a unit with Reraise was up again: Brave Story's second round, whose 0x19 wears an Angel Ring,
    fired its victory at the poke and went on (seen on the PC, 2026-11-19). The enemies are those on the field (+1 not
    0xFF), as the victory checks count them: a unit still to come (The Shrine Knights' reinforcements, brought in by
    their rounds' events) is left as it is."""
    base, listed = battle_units()
    if not base:
        raise SystemExit("no battle units (not in a battle?)")
    chosen = units or [u["index"] for u in listed if u["team"] & TEAM_BITS and u["id"] != 0xFF]
    for k in chosen:
        at = base + k * UNIT_SIZE
        status = peek_bytes(at + UNIT_STATUS, 1)[0] | DEAD
        send(f"poke 0x{at + UNIT_HP:X} 00 00")
        send(f"poke 0x{at + UNIT_STATUS:X} {status:02X}")
        send(f"poke 0x{at + UNIT_STATUS_COPY:X} {status:02X}")
        for place in (UNIT_INNATE, UNIT_STATUS, UNIT_STATUS_COPY):
            byte = peek_bytes(at + place + RERAISE_BYTE, 1)[0]
            if byte & RERAISE:
                send(f"poke 0x{at + place + RERAISE_BYTE:X} {byte & ~RERAISE & 0xFF:02X}")
    return chosen


# ReMixed's Treasure Wheel (its event 0x0C3, shipped as 0x368), which a won mission hands on to. It waits in a loop for
# a button (Left/Right turn the ring, F/Enter opens the chest in front, Space cashes in the rest), and its top and
# bottom lines go on without a press, so once the chest in front is open, Enter changes nothing and it waits for good.
WHEEL = 872


def advance(until: str, timeout: float, key: str = "RETURN", every: float = 6, shots: bool = False,
            shot_dir: Path | None = None) -> dict:
    """Press key every few seconds while a scene runs, printing each change of event or scenario, until: "world" (the
    world map runs again: its frame counter moves; nothing is pressed there, as Enter searches for enemies on a field),
    "opening" (ReMixed's code has reached an END and stays there: the battle is on), or "event N" (it's running).
    On the Treasure Wheel, once two presses in a row change nothing (its code stays at the same instruction: the
    chest in front is open), Right turns the ring to the next chest for the presses to open, until its tries are
    spent and it ends by itself. With shots, a screenshot before each press (work/harness/shots/advance-NNN.png, or
    in shot_dir), named by the tenths of a second since the start."""
    start = game_state()
    began = time.time()
    last, still, pc = None, 0, None
    before, idle = start, 0  # the Wheel: the state before the last press, and presses in a row that changed nothing
    while time.time() - began < timeout:
        nap(every)
        now = game_state()
        if (now["event"], now["scenario"]) != last:
            print(f"  {time.time() - began:4.0f} s: {now['text'][:150]}")
            last = (now["event"], now["scenario"])
        if until == "world" and now["world"] > start["world"] + 30:
            return now
        if until.startswith("event ") and now["event"] == int(until.split()[1]):
            return now
        if until == "opening":
            still = still + 1 if now["op"] == 0xDB and now["pc"] == pc else 0
            pc = now["pc"]
            if still >= 2:
                return now
        if shots:
            name = f"advance-{int(10 * (time.time() - began)):04d}"
            if shot_dir:
                shot(argparse.Namespace(file=shot_dir / f"{name}.png"))
            else:
                snap(name)
        press = key
        if now["event"] == WHEEL:
            idle = idle + 1 if (now["pc"], now["op"]) == (before["pc"], before["op"]) else 0
            if idle >= 2:
                press, idle = "RIGHT", 0
                print(f"  {time.time() - began:4.0f} s: the Wheel waits (event at 0x{now['pc']:X}, "
                      f"op 0x{now['op']:X}): Right turns its ring")
        else:
            idle = 0
        before = now
        send(f"key {press}")
    print(f"  not reached after {timeout:.0f} s")
    return game_state()


SHOTS = WORK / "harness" / "shots"


def snap(name: str) -> Path:
    """A screenshot into work/harness/shots (each step's, for looking at when a step fails)."""
    path = SHOTS / f"{name}.png"
    shot(argparse.Namespace(file=path))
    return path


# Reveal Treasures (code/IvaliceRemixed/Patches/RevealTreasures.cs) asks its price as a formation screen opens on a map
# whose hidden items still hold a rare one, in the box "Commence battle?" uses (Yes first). Its log says when it asks,
# how it was answered, and when the arrows it stands over the hidden items (after Yes, or shown for free) are up.
COMMENCE_ASKED = r"formation: Commence battle\? asked"
COMMENCE_CLOSED = r"formation: Commence battle\? closed"  # the state it watches set while a unit is placed, too
# The formation screen's first frame that takes input (RevealTreasures.OnFrame), with its places and groups; Reveal
# Treasures asks (or shows for free) in that frame, so a map with nothing hidden needn't wait out the question.
FORMATION_OPEN = r"formation: screen open, (\d+) place\(s\) in (\d+|\?) group\(s\)"
REVEAL_ASKED = r"reveal treasures: map \d+, \d+ panel\(s\) with their rare item: asked"
REVEAL_ANSWERED = r"reveal treasures: (paid \d+ gil|declined|\d+ gil asked, \d+ held)"
REVEAL_FREE = r"reveal treasures: map \d+'s \d+ hidden item\(s\) shown, for free"
REVEAL_ARROWS = (r"reveal treasures: (\d+ arrow\(s\) stand over|no event slot free|the arrows weren't made|"
                 r"event graphic 0x6F couldn't|event slot \d+ didn't take)")


def reveal_asking() -> bool:
    """Whether Reveal Treasures' question is up: asked more often than answered since `start`."""
    lines = log_lines()
    return count_log(REVEAL_ASKED, lines) > count_log(REVEAL_ANSWERED, lines)


def wait_until(check, timeout: float, every: float = 2) -> bool:
    """Whether check() came true within timeout."""
    end_at = time.time() + timeout
    while not check():
        if time.time() > end_at:
            return False
        nap(every)
    return True


def answer_reveal(reveal: str) -> bool:
    """Answer Reveal Treasures' question if it's up: "no" Down then F (No), "yes" F (Yes: it pays, and the formation
    takes no input until its arrows are up). Whether it was up."""
    if not reveal_asking():
        return False
    lines = log_lines()
    answered, arrows = count_log(REVEAL_ANSWERED, lines), count_log(REVEAL_ARROWS, lines)
    # The box takes no keys while it opens: a Down 2 s after the question was lost and the F paid (2026-11-12); the
    # declines by hand pressed Down 6 s or more after it, and F 2-3 s after Down.
    nap(5)
    if reveal == "yes":
        send("key F")
    else:
        send("key DOWN")
        nap(3)
        send("key F")
    line = wait_log(REVEAL_ANSWERED, 15, before=answered)
    said = line[line.find("reveal"):] if line else "no answer logged in 15 s"
    print(f"  reveal treasures, answered {reveal}: {said}")
    if line and "paid" in line:
        if reveal != "yes":
            print("  (Down didn't reach No: it paid)")
        line = wait_log(REVEAL_ARROWS, 20, before=arrows)
        print(f"  {line[line.find('reveal'):] if line else 'no arrows logged in 20 s'}")
    nap(2)
    return True


def place(reveal: str) -> None:
    """One F on the formation screen (a tile picked, or a unit), Reveal Treasures' question answered first if it's up
    (its box takes the formation's F)."""
    answer_reveal(reveal)
    send("key F")
    nap(5)


def formation(units: int, second: int = 0, reveal: str = "no", ask_wait: float = 5, free: int | None = None,
              opened: int | None = None) -> bool:
    """Place units on the formation screen (F picks a tile, F a unit), and with second, that many more in its second
    group (Tab switches to it; ReMixed's missions have two groups, and won't start with one empty), commence (Space; F
    for Yes once the box is up) and wait for the battle's event to load; twice more if it doesn't. First Reveal
    Treasures is waited for, up to ask_wait seconds: its question is answered with reveal ("no" or "yes"), and when it
    shows a map's items for free (a free line past the first `free` of them), its arrows are given time to stand. Each
    step leaves a screenshot in work/harness/shots. The sequence that commenced by hand, and in this order (a bare Space
    then F 5-10 s later didn't, three times): Space, 4 s, a screenshot, F. With opened (FORMATION_OPEN's count before the
    battle was started), the screen's own line is waited for instead, up to ask_wait, and Reveal Treasures for 3 s."""
    free = count_log(REVEAL_FREE) if free is None else free
    if opened is not None:
        line = wait_log(FORMATION_OPEN, ask_wait, before=opened)
        print(f"  {line[line.find('formation'):] if line else f'no formation screen logged in {ask_wait:g} s'}")
        ask_wait = 3
    if wait_until(lambda: reveal_asking() or count_log(REVEAL_FREE) > free, ask_wait, every=0.5) and not reveal_asking():
        print("  reveal treasures: the hidden items shown for free")
        nap(5)  # its arrows are made, and the formation takes no input meanwhile
    snap("formation-0-start")
    if answer_reveal(reveal):
        snap("formation-0-answered")
    asked_start, closed_start = count_log(COMMENCE_ASKED), count_log(COMMENCE_CLOSED)

    def box_up() -> bool:
        """Whether "Commence battle?" is up: asked more often than closed since the placing began."""
        return count_log(COMMENCE_ASKED) - asked_start > count_log(COMMENCE_CLOSED) - closed_start

    for _ in range(units * 2):
        place(reveal)
    if second:
        send("key TAB")
        nap(3)
        for _ in range(second * 2):
            place(reveal)
    for attempt in range(5):
        answer_reveal(reveal)
        snap(f"formation-{attempt + 1}-placed")
        # Space opens "Commence battle?" (the mod logs it, COMMENCE_ASKED); a Space the screen didn't take is tried
        # again at once instead of after the 40 s a battle may take to load (two lost tries cost 80 s, 2026-11-18).
        asked = count_log(COMMENCE_ASKED)
        if box_up():
            # Placing the last unit there is to place brings the box up by itself (seen on the PC with six places,
            # 2026-11-19); a Space then is lost in the box.
            print("  \"Commence battle?\" came up with the last unit placed")
        else:
            send("key SPACE")
            if not wait_log(COMMENCE_ASKED, 6, before=asked):
                # After a placing that leaves places open the screen waits on the next tile's unit list, which takes
                # no Space (nor Tab): Escape (its Back) returns to the tiles, where Space commences with places left
                # over (a two-group formation not filled, 2026-11-27).
                print(f"  commence try {attempt + 1}: Space didn't bring up \"Commence battle?\"; Escape to the tiles")
                send("key ESCAPE")
                nap(2)
                continue
        nap(4)  # the box takes no keys while it opens (an F 1.5 s after it was lost, 2026-11-18)
        snap(f"formation-{attempt + 1}-commence")
        send("key F")
        # A ReMixed battle loads its event; a random battle has none and goes straight to its first turn.
        before = count_log(r"loading event \d+")

        def started() -> bool:
            return count_log(r"loading event \d+") > before or turn_started()

        commenced = wait_until(started, 12, every=1)
        if not commenced:
            send("key F")  # the box hadn't taken the first F yet; an F in a loading battle's opening is harmless
            commenced = wait_until(started, 40, every=1)
        if commenced:
            line = wait_log(r"loading event \d+", 0.1, before=before)
            print(f"  {line[line.find('remixed'):]}" if line else "  a unit's turn started")
            return True
        print(f"  commence try {attempt + 1}: no event loaded (screenshots in {SHOTS})")
    return False


def turn_started() -> bool:
    """Whether a battle unit on the field has its turn (+0x1B8): the battle runs."""
    base, units = battle_units()
    present = [u for u in units if u["id"] != 0xFF]
    if not base or not present:
        return False
    data = peek_bytes(base, (max(u["index"] for u in present) + 1) * UNIT_SIZE)
    return any(data[u["index"] * UNIT_SIZE + 0x1B8] for u in present)


def on_world_map(timeout: float) -> bool:
    """Whether the world map's frames are running (waiting up to timeout for them)."""
    end_at = time.time() + timeout
    before = game_state()["world"]
    while time.time() < end_at:
        nap(3)
        now = game_state()["world"]
        if now > before + 5:
            return True
        before = now
    return False


def cmd_battle(args: argparse.Namespace) -> None:
    if not args.integration:
        raise SystemExit("battle is the slow final integration gate; pass --integration explicitly, or use "
                         "`checkpoint SCENARIO EVENT PHASE --messages ...` for event rendering")
    scenario = int(args.scenario, 16)
    if not on_world_map(args.timeout):
        raise SystemExit("the world map isn't running (start with --world, or get there first)")
    free, opened = count_log(REVEAL_FREE), count_log(FORMATION_OPEN)
    print(f"starting scenario 0x{scenario:X}: {send(f'scenario {scenario:X}')}")
    line = wait_log(rf"scenario 0x{scenario:X} takes", 90)
    print(f"  {line[line.find('remixed'):] if line else 'its setup never logged'}")
    # The formation screen logs its first frame that takes input (45-78 s after the setup in the runs so far).
    print("formation:")
    if not formation(args.units, args.second, args.reveal, ask_wait=args.formation_wait, free=free, opened=opened):
        raise SystemExit("the battle didn't start; look at a `shot`")
    print("its opening:")
    advance("opening", args.timeout, every=args.every)
    print(f"defeated units {defeat(args.enemies)}")
    print("its closing, until the world map:")
    now = advance("world", args.timeout, every=args.every)
    print(f"done: {now['text']}")


def event_message(line: str, event: int) -> int | None:
    """The message number from one runtime event-log line."""
    found = re.search(rf"event {event}'s message (\d+) shown as line", line)
    return int(found.group(1)) if found else None


def capture_event_messages(event: int, messages: list[int], timeout: float, every: float, settle: float,
                           prefix: Path | None = None, seen: int | None = None) -> list[Path]:
    """Advance a running event quickly and capture each requested message before pressing past it."""
    wanted = list(dict.fromkeys(messages))
    if not wanted:
        raise SystemExit("checkpoint needs at least one --messages value")
    if any(message <= 0 for message in wanted):
        raise SystemExit("message numbers must be positive")
    seen = len(log_lines()) if seen is None else seen
    captured: list[Path] = []
    end_at = time.time() + timeout
    while time.time() < end_at:
        lines = log_lines()
        for line in lines[seen:]:
            message = event_message(line, event)
            if message not in wanted:
                continue
            nap(settle)
            path = ((prefix.parent / f"{prefix.name}-message-{message}.png") if prefix else
                    SHOTS / f"checkpoint-event-{event:03X}-message-{message}.png")
            shot(argparse.Namespace(file=path))
            print(f"  captured event {event} message {message}: {path}")
            captured.append(path)
            wanted.remove(message)
            if not wanted:
                return captured
        seen = len(lines)
        send("key RETURN")
        nap(every)
    raise SystemExit(f"event {event} did not show message(s) {', '.join(map(str, wanted))} in {timeout:.0f} s")


def wait_condition_event(event: int, phase: int | None, timeout: float, seen: int, opening: dict,
                         advance_phases: set[int] | None = None, dismiss_end_phases: set[int] | None = None,
                         every: float = 0.25, prefix: Path | None = None) -> tuple[str, int]:
    """Wait for a battle condition to load runtime ``event`` (and optional ``phase``) after the opening.

    A ReMixed battle carrier can reuse one runtime event number for its opening, turn dialogue, and closing, even when
    those phases came from different source events.  A fresh reload is therefore not enough evidence of victory: var
    1 must equal the expected condition-set phase when the target reuses the opening's number.  The returned log index
    starts message capture at the latest reload breadcrumb.  By default this is polling-only. ``advance_phases`` is
    an explicit allowlist whose active dialogue may be confirmed. ``dismiss_end_phases`` permits exactly one confirm
    at a named phase's stable END, for result notices that otherwise block the next condition. No other input is sent.
    """
    load = re.compile(rf"remixed scenarios: (?:loading event {event}(?:\D|$)|event {event} runs again)")
    end_at = time.time() + timeout
    capture_seen = seen
    evidence = ""
    advance_phases = advance_phases or set()
    dismiss_end_phases = dismiss_end_phases or set()
    dismissed: set[tuple[int, int]] = set()
    last_press = 0.0
    while time.time() < end_at:
        lines = log_lines()
        for index, line in enumerate(lines[seen:], start=seen):
            if load.search(line):
                capture_seen, evidence = index, line
        now = game_state()
        left_opening_end = (now["pc"], now["op"]) != (opening["pc"], opening["op"])
        phase_matches = phase is None or now["phase"] == phase
        phase_changed = phase is not None and opening.get("phase") != phase
        if now["event"] == event and phase_matches and (opening["event"] != event or phase_changed or
                                                         left_opening_end):
            return evidence or now["text"], capture_seen
        if (now["event"] == opening["event"] and now["phase"] in advance_phases and now["op"] != 0xDB and
                time.time() - last_press >= every):
            send("key RETURN")
            last_press = time.time()
        end_key = (now["phase"], now["pc"])
        if (now["event"] == opening["event"] and now["phase"] in dismiss_end_phases and now["op"] == 0xDB and
                end_key not in dismissed):
            path = ((prefix.parent / f"{prefix.name}-result-phase-{now['phase']}.png") if prefix else
                    SHOTS / f"checkpoint-event-{opening['event']:03X}-result-phase-{now['phase']}.png")
            shot(argparse.Namespace(file=path))
            print(f"  captured result notice for phase {now['phase']}: {path}")
            send("key RETURN")
            dismissed.add(end_key)
        nap(0.5)
    phase_text = "" if phase is None else f" phase {phase}"
    raise SystemExit(f"victory did not select runtime event {event}{phase_text} in {timeout:.0f} s")


def capture_after_event(event: int, timeout: float, every: float, prefix: Path | None = None) -> Path:
    """Confirm through ``event`` and capture its first completed/handed-off frame, never pressing on the world map."""
    began = time.time()
    first = game_state()
    last_end_pc, still = None, 0
    while time.time() - began < timeout:
        now = game_state()
        if now["world"] > first["world"] + 5 or now["event"] != event:
            break
        if now["op"] == 0xDB:
            still = still + 1 if now["pc"] == last_end_pc else 1
            last_end_pc = now["pc"]
            if still >= 2:
                break
            # END needs no confirmation.  Let the game hand off on its own; a Return here could land on the battle
            # command UI (or the world map) during the transition this helper exists to inspect.
            nap(every)
            continue
        else:
            still, last_end_pc = 0, None
        send("key RETURN")
        nap(every)
    else:
        raise SystemExit(f"event {event} did not finish or hand off in {timeout:.0f} s")
    path = ((prefix.parent / f"{prefix.name}-after-event.png") if prefix else
            SHOTS / f"checkpoint-event-{event:03X}-after-event.png")
    shot(argparse.Namespace(file=path))
    print(f"  captured after event {event}: {path}")
    return path


def validate_checkpoint_args(args: argparse.Namespace) -> None:
    """Fail before touching the game when direct-phase and real-victory checkpoint options are mixed."""
    if args.victory and args.phase is not None:
        raise SystemExit("checkpoint --victory uses the battle's real condition; omit PHASE")
    if not args.victory and args.phase is None:
        raise SystemExit("checkpoint needs PHASE unless --victory is used")
    if args.phase is not None and not 0 <= args.phase < 16:
        raise SystemExit("checkpoint PHASE must be from 0 through 15")
    if args.enemies is not None and not args.victory:
        raise SystemExit("checkpoint --enemies is only valid with --victory")
    if args.condition_phase is not None and not args.victory:
        raise SystemExit("checkpoint --condition-phase is only valid with --victory")
    if args.condition_phase is not None and not 0 <= args.condition_phase < 16:
        raise SystemExit("checkpoint --condition-phase must be from 0 through 15")
    if args.runtime_event is not None and not args.victory:
        raise SystemExit("checkpoint --runtime-event is only valid with --victory")
    if args.advance_phase and not args.victory:
        raise SystemExit("checkpoint --advance-phase is only valid with --victory")
    if any(not 0 <= phase < 16 for phase in args.advance_phase):
        raise SystemExit("checkpoint --advance-phase values must be from 0 through 15")
    if args.dismiss_end_phase and not args.victory:
        raise SystemExit("checkpoint --dismiss-end-phase is only valid with --victory")
    if any(not 0 <= phase < 16 for phase in args.dismiss_end_phase):
        raise SystemExit("checkpoint --dismiss-end-phase values must be from 0 through 15")
    scenario = int(args.scenario, 16)
    event = int((args.runtime_event or args.event).removeprefix("0x"), 16)
    if args.victory and event == scenario and args.condition_phase is None:
        raise SystemExit("checkpoint --victory needs --condition-phase when the target reuses the carrier event")
    if not args.messages or any(message <= 0 for message in args.messages):
        raise SystemExit("checkpoint --messages values must be positive")


def cmd_checkpoint(args: argparse.Namespace) -> None:
    """Reach one battle carrier, then force a phase or reproduce its actual victory-condition transition."""
    validate_checkpoint_args(args)
    scenario = int(args.scenario, 16)
    event = int(args.event.removeprefix("0x"), 16)
    if not on_world_map(args.timeout):
        raise SystemExit("the world map isn't running (start with --world, or get there first)")
    free = count_log(REVEAL_FREE)
    print(f"starting carrier 0x{scenario:X}: {send(f'scenario {scenario:X}')}")
    line = wait_log(rf"scenario 0x{scenario:X} takes", 90)
    print(f"  {line[line.find('remixed'):] if line else 'its setup never logged'}")
    if not formation(args.units, args.second, args.reveal, ask_wait=args.formation_wait, free=free):
        raise SystemExit("the carrier did not commence; inspect work/harness/shots")
    print("fast-forwarding only the carrier opening:")
    opening = advance("opening", args.timeout, every=args.every)
    seen = len(log_lines())
    capture_event, capture_seen = event, seen
    if args.victory:
        capture_event = int(args.runtime_event.removeprefix("0x"), 16) if args.runtime_event else event
        phase_text = "" if args.condition_phase is None else f" phase {args.condition_phase}"
        print(f"defeated units {defeat(args.enemies)}; waiting for runtime event {capture_event}{phase_text}")
        line, capture_seen = wait_condition_event(capture_event, args.condition_phase, args.timeout, seen, opening,
                                                  set(args.advance_phase), set(args.dismiss_end_phase), args.every,
                                                  args.prefix)
        print(f"  condition reached runtime event {capture_event}{phase_text}: "
              f"{line[line.find('remixed'):] if 'remixed' in line else line[:180]}")
    else:
        reply = send(f"event {event:X} {args.phase}")
        print(reply)
        if reply.startswith("error"):
            raise SystemExit(1)
    capture_event_messages(capture_event, args.messages, args.timeout, args.every, args.settle, args.prefix,
                           seen=capture_seen)
    if args.after_event:
        capture_after_event(capture_event, args.timeout, args.every, args.prefix)


def cmd_formation(args: argparse.Namespace) -> None:
    print("commenced" if formation(args.units, args.second, args.reveal) else "not commenced")


def cmd_advance(args: argparse.Namespace) -> None:
    print(advance(args.until, args.timeout, args.key, args.every, shots=args.shots)["text"])


def cmd_defeat(args: argparse.Namespace) -> None:
    print(f"defeated units {defeat(args.units)}")


def cmd_event(args: argparse.Namespace) -> None:
    """Run one generated event phase in the current battle, wait for its first message, and capture it."""
    event = int(args.event.removeprefix("0x"), 16)
    pattern = rf"event {event}'s message \d+ shown as line"
    before = count_log(pattern)
    reply = send(f"event {event:X} {args.phase}")
    print(reply)
    if reply.startswith("error"):
        raise SystemExit(1)
    line = wait_log(pattern, args.timeout, before=before)
    if line:
        print(f"  {line[line.find('remixed'):]}")
        nap(args.settle)
    else:
        print(f"  no message appeared in {args.timeout:.0f} s; capturing the current frame")
    path = args.file or SHOTS / f"event-{event:03X}-phase-{args.phase}.png"
    shot(argparse.Namespace(file=path))


def cmd_wait(args: argparse.Namespace) -> None:
    line = wait_log(args.pattern, args.timeout, fresh=not args.any)
    print(line if line else f"not seen in {args.timeout:.0f} s")
    if not line:
        sys.exit(1)


ROSTER, ROSTER_SIZE, ROSTER_COUNT, ROSTER_NAME_ID, ROSTER_NAME = 0x11A7D10, 600, 0x36, 0x230, 0xDC


def cmd_roster(_: argparse.Namespace) -> None:
    """The remaster's roster records (.data 0x11A7D10, 54 of 600 bytes) in use: their first bytes (sprite set, unit
    id), job (+0x02), name id (+0x230, looked up in an NXD table by 0x1E8B7C) and name text (+0xDC)."""
    data = peek_bytes(image_base() + ROSTER, ROSTER_SIZE * ROSTER_COUNT)
    for k in range(ROSTER_COUNT):
        r = data[k * ROSTER_SIZE:(k + 1) * ROSTER_SIZE]
        if r[1] == 0xFF or not any(r[:4]):
            continue
        name = r[ROSTER_NAME:ROSTER_NAME + 32].split(b"\0")[0].decode("utf-8", "replace")
        name_id = int.from_bytes(r[ROSTER_NAME_ID:ROSTER_NAME_ID + 4], "little")
        print(f"{k:2}: sprites 0x{r[0]:02X} id 0x{r[1]:02X} job 0x{r[2]:02X} name id {name_id} name {name!r}")


BATTLE_UNITS, BATTLE_UNIT_COUNT = 0x1853CE0, 21


# A battle's unit sprites: 15 sheet slots (.data 0xD005A0, 0x32D6 bytes each: +0 nonzero while a sprite uses it, +1 the
# graphic in it; the allocator 0x268FF4 reuses a graphic's slot or takes a free one, else fails), and 16 sprite objects
# (their in-use words at 0xD30030, 0x548 apart; the object at 0xD30038: +8 its index, +9 its sheet slot, +10 its
# graphic), the active ones listed from [0xD3A410] (+0 the next).
SHEET_SLOTS, SHEET_SLOT_SIZE, SHEET_SLOT_COUNT = 0xD005A0, 0x32D6, 15
SPRITE_OBJECTS, SPRITE_OBJECT_SIZE, SPRITE_OBJECT_COUNT, SPRITE_LIST = 0xD30030, 0x548, 16, 0xD3A410


def cmd_sprites(_: argparse.Namespace) -> None:
    """The unit sprites' sheet slots and sprite objects (SHEET_SLOTS, SPRITE_OBJECTS)."""
    base = image_base()
    slots = []
    for k in range(SHEET_SLOT_COUNT):
        used, graphic = peek_bytes(base + SHEET_SLOTS + k * SHEET_SLOT_SIZE, 2)
        slots.append(f"{k}:{'+' if used else '-'}{graphic:02X}")
    print(f"  sheet slots ({sum(s[s.index(':') + 1] == '+' for s in slots)} of {SHEET_SLOT_COUNT} in use): "
          + " ".join(slots))
    objects = []
    for k in range(SPRITE_OBJECT_COUNT):
        at = base + SPRITE_OBJECTS + k * SPRITE_OBJECT_SIZE
        used = int.from_bytes(peek_bytes(at, 4), "little")
        index, slot, graphic = peek_bytes(at + 8 + 8, 3)
        if used:
            objects.append(f"{k}:slot {slot} graphic {graphic:02X}")
    listed, node = [], int.from_bytes(peek_bytes(base + SPRITE_LIST, 8), "little")
    while node and len(listed) < 32:
        listed.append((node - (base + SPRITE_OBJECTS + 8)) // SPRITE_OBJECT_SIZE)
        node = int.from_bytes(peek_bytes(node, 8), "little")
    print(f"  sprite objects in use ({len(objects)} of {SPRITE_OBJECT_COUNT}): " + ", ".join(objects))
    print(f"  listed: {listed}")


def cmd_slots(_: argparse.Namespace) -> None:
    """The battle units in use (.data 0x1853CE0, 21 of 0x200 bytes) with the fields that tie them to the roster: +0
    sprite set, +1, +2 the roster slot it's written back to at the end (0xFF none), +3 job, +6 its battle-data flags
    (0x08 taken from the roster, 0x10 joins), +0x191 battle id, +0x1B5, +0x1FC name id."""
    data = peek_bytes(image_base() + BATTLE_UNITS, BATTLE_UNIT_COUNT * UNIT_SIZE)
    for k in range(BATTLE_UNIT_COUNT):
        u = data[k * UNIT_SIZE:(k + 1) * UNIT_SIZE]
        if u[0x191] or u[0] or u[3]:
            print(f"unit {k:2}: sprites 0x{u[0]:02X} +1 0x{u[1]:02X} slot 0x{u[2]:02X} job 0x{u[3]:02X} flags 0x{u[6]:02X} "
                  f"battle id 0x{u[0x191]:02X} +0x1B5 0x{u[0x1B5]:02X} name id {int.from_bytes(u[0x1FC:0x200], 'little')}")


def scenario_maps() -> list[tuple[int, int, int]]:
    """The exe's scenario table (24-byte records from the lea at ScenariosPattern + 0x12, RemixedScenarios.cs): each
    scenario's id, map and battle entry, read from the file (no game needed)."""
    exe = (Path(load_config()["target"]["tic_dir"]) / "FFT_enhanced.exe").read_bytes()
    pe = struct.unpack_from("<I", exe, 0x3C)[0]
    count, optional = struct.unpack_from("<H", exe, pe + 6)[0], struct.unpack_from("<H", exe, pe + 20)[0]
    sections = [struct.unpack_from("<IIII", exe, pe + 24 + optional + 40 * k + 8) for k in range(count)]

    def offset(rva: int) -> int:
        for size, address, raw_size, raw in sections:
            if address <= rva < address + max(size, raw_size):
                return raw + rva - address
        raise ValueError(f"RVA 0x{rva:X} is in no section")

    pattern = re.compile(rb"\x44\x8B\x0D....\x33\xDB\x45\x85\xC9\x0F\x88....\x48\x8D\x0D", re.S)
    found = [m.start() for m in pattern.finditer(exe)]
    if len(found) != 1:
        raise SystemExit(f"the scenario table's reference is found {len(found)} times")
    text = next(s for s in sections if s[3] <= found[0] < s[3] + s[2])
    lea_rva = text[1] + found[0] - text[3] + 0x12
    table = offset(lea_rva + 7 + struct.unpack_from("<i", exe, offset(lea_rva) + 3)[0])
    out = []
    for k in range(0x400):
        sid, map_, entry = (struct.unpack_from("<H", exe, table + 24 * k + o)[0] for o in (0, 2, 7))
        if k and sid == 0:
            break
        out.append((sid, map_, entry))
    return out


def cmd_maps(args: argparse.Namespace) -> None:
    rows = [r for r in scenario_maps() if r[1] == args.map]
    print(f"map {args.map}: " + (", ".join(f"scenario 0x{s:X} (battle entry 0x{e:X})" for s, _, e in rows) or "no scenario"))
    if rows:
        print("A story battle's first scenario plays its scene, then its formation; the next one starts at the formation "
              "(its battle may then load the wrong event, but the map is up in about 10 s).")


def cmd_crashes(args: argparse.Namespace) -> None:
    """The game's crashes in Windows' Application log over the last minutes, each address as an RVA in its function
    (the image base from the mod's log line "the exe at 0x...", so from a run with this harness)."""
    import subprocess  # noqa: PLC0415

    script = ("Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-%d)} "
              "-ErrorAction SilentlyContinue | Where-Object { $_.Message -match 'FFT_enhanced' } | "
              "ForEach-Object { $_.TimeCreated.ToString('s') + ' ' + ($_.Message -replace '\\s+', ' ') }") % args.minutes
    out = subprocess.run(["powershell", "-NoProfile", "-Command", script], capture_output=True, text=True,
                         check=False).stdout
    bases = [int(m.group(1), 16) for m in (re.search(r"the exe at 0x([0-9A-F]+)", line) for line in all_log_lines()) if m]
    import exe  # noqa: PLC0415

    table = exe.functions()
    for line in out.splitlines():
        print(line[:300])
        m = re.search(r"exception address (0x[0-9A-Fa-f]+|[0-9A-Fa-f]{8,})", line) or re.search(r"Fault offset: (0x[0-9a-f]+)", line)
        if not m:
            continue
        address = int(m.group(1), 16)
        if address > 0x10000000 and not bases:
            print("  (no image base in the logs: a run from before the harness logged it)")
            continue
        rva = address - bases[-1] if address > 0x10000000 else address
        entry = exe.containing(rva, table)
        print(f"  -> exe+0x{rva:X}" + (f" in 0x{entry[0]:X} {entry[2]} +0x{rva - entry[0]:X} ({entry[3].name}:{entry[4]})"
                                       if entry else ""))
    if not out.strip():
        print(f"no crash of the game in the last {args.minutes} minutes")


def wait_world_state(depth: int, top: int, timeout: float = 15) -> str:
    """Wait for one exact world UI stack state, for deterministic menu helpers."""
    deadline = time.time() + timeout
    last = ""
    pattern = re.compile(rf"world depth {depth}, top state {top}(?:,|$)")
    while time.time() < deadline:
        last = send("state")
        if pattern.search(last):
            return last
        nap(0.25)
    raise RuntimeError(f"world UI did not reach depth {depth}, state {top}: {last}")


def wait_world_ready(timeout: float = 120) -> str:
    """Wait for the fixture's world map and for its boot script to stop sending input."""
    deadline = time.time() + timeout
    last = ""
    while time.time() < deadline:
        last = send("state")
        if ("world depth 1, top state 0" in last and "autopilot done true" in last):
            return last
        nap(0.25)
    raise RuntimeError(f"the test fixture did not become idle on the world map: {last}")


def state_party_location(state: str) -> int:
    match = re.search(r"party location (-?\d+)(?:,|$)", state)
    return int(match.group(1)) if match else -1


def wait_dispatch_view(opened: bool, timeout: float = 15) -> str:
    """Wait for the native Dispatch overlay to open or close."""
    deadline = time.time() + timeout
    last = ""
    wanted = f"dispatch open {str(opened).lower()}"
    while time.time() < deadline:
        last = send("state")
        if wanted in last:
            return last
        nap(0.25)
    raise RuntimeError(f"Dispatch view did not become {opened}: {last}")


def expected_dispatch_counts(story: int, kind: str = "all") -> Counter[str]:
    """Expected rows on one page of the native Dispatch hierarchy."""
    story = max(0, min(story, 16))
    return {
        "all": Counter({"Menu": 5}),
        "propositions": Counter({"Menu": 6}),
        "jobs": Counter({"Job": 4 if story >= 3 else 0}),
        "training": Counter({"Training": 7}),
        "contests": Counter({"Contest": 1 if story >= 5 else 0}),
    }[kind]


def validate_dispatch_info(info: str, kind: str, story: int) -> None:
    """Assert total, kind mix and unique backing keys in the game's structured catalog dump."""
    expected = expected_dispatch_counts(story, kind)
    rows = []
    for line in info.splitlines()[1:]:
        match = re.match(r"\d+: ([^|]+) \| .* \| key (\d+)$", line)
        if match:
            rows.append((match.group(1).strip(), int(match.group(2))))
    header = re.search(r"(?:^|, )(\d+) row\(s\):", info.splitlines()[0])
    expected_total = sum(expected.values())
    if not header or int(header.group(1)) != expected_total or len(rows) != expected_total:
        raise RuntimeError(f"expected {expected_total} {kind} rows, got:\n{info}")
    actual = Counter(row_kind for row_kind, _ in rows)
    if actual != +expected or len({key for _, key in rows}) != len(rows):
        raise RuntimeError(f"unexpected or duplicate Dispatch rows (expected {dict(+expected)}, got {dict(actual)}):\n{info}")


def wait_dispatch_page(page: str, timeout: float) -> str:
    """Wait for the native rows, first-row focus, and input gate after changing page."""
    deadline = time.monotonic() + timeout
    info = ""
    heading = re.compile(rf"^{re.escape(page)}, (\d+) row\(s\):")
    native = re.compile(r"^profit-main: rows (\d+), focus 0, state 4, gate 0, pending -1$", re.MULTILINE)
    while time.monotonic() < deadline:
        info = send("dispatchinfo", timeout=min(1.0, timeout))
        expected = heading.match(info)
        displayed = native.search(info)
        if expected and displayed and expected[1] == displayed[1] and int(displayed[1]) > 0:
            return info
        nap(0.25)
    raise RuntimeError(f"Dispatch did not reach {page} within {timeout:g}s:\n{info}")


def check_dispatch_navigation(cycles: int, story: int, timeout: float) -> str:
    """Exercise native Confirm and Back through both levels, keeping the same overlay open."""
    info = wait_dispatch_page("Main (-)", timeout)
    for cycle in range(1, cycles + 1):
        try:
            send("key RETURN", timeout=1)
            info = wait_dispatch_page("PropositionKinds (-)", timeout)
            validate_dispatch_info(info, "propositions", story)
            send("key RETURN", timeout=1)
            wait_dispatch_page("Propositions (Salvaging)", timeout)
            send("key ESCAPE", timeout=1)
            info = wait_dispatch_page("PropositionKinds (-)", timeout)
            validate_dispatch_info(info, "propositions", story)
            send("key ESCAPE", timeout=1)
            info = wait_dispatch_page("Main (-)", timeout)
            validate_dispatch_info(info, "all", story)
        except (RuntimeError, OSError) as error:
            raise RuntimeError(f"Dispatch navigation cycle {cycle}/{cycles} failed: {error}") from error
        print(f"PASS Dispatch navigation cycle {cycle}/{cycles}")
    return info


def cmd_dispatch(args: argparse.Namespace) -> None:
    """Enter the fixture Tavern once, then open and verify one native Dispatch catalog directly."""
    if args.cycles < 0 or (args.cycles and args.kind != "all"):
        raise RuntimeError("--cycles needs a non-negative count and the full Dispatch catalog (all)")
    opened_pattern = r"dispatch missions: opened the native view with"
    opened_before = count_log(opened_pattern)
    reply = send(f"dispatch {args.kind} {args.story} {args.month}")
    if reply.startswith("error: dispatch requires"):
        state = wait_world_ready(max(120, args.timeout))
        location = state_party_location(state)
        if location != 2:
            raise RuntimeError(f"Dispatch needs the Eagrose fixture, not location {location}; restart with "
                               "`python -I tools/harness.py start --world --location 2`")
        send("key C")  # select the party's current Eagrose node, independent of the old world cursor
        nap(0.75)
        send("key RETURN")
        wait_world_state(2, 4)
        send("key S")
        send("key RETURN")
        wait_world_state(2, 5)
        opened_before = count_log(opened_pattern)
        reply = send(f"dispatch {args.kind} {args.story} {args.month}")
    if reply.startswith("error:"):
        raise RuntimeError(reply)
    print(reply)
    opened = wait_log(opened_pattern, args.timeout, before=opened_before)
    if opened is None:
        raise RuntimeError("the native Dispatch view did not open")
    wait_dispatch_view(True, args.timeout)
    nap(args.settle)
    info = send("dispatchinfo")
    validate_dispatch_info(info, args.kind, args.story)
    if args.cycles:
        info = check_dispatch_navigation(args.cycles, args.story, args.timeout)
    print(info)
    shot(args)
    if not args.leave_open:
        send("key ESCAPE")
        wait_dispatch_view(False, args.timeout)


def build_parser() -> argparse.ArgumentParser:
    """The command-line grammar, exposed separately so its safety guards can be tested without running the game."""
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="what", required=True)
    p = sub.add_parser("start")
    p.add_argument("--world", action="store_true", help="press Enter until the world map")
    p.add_argument("--script", type=Path, help="an autopilot script to run instead")
    p.add_argument("--timeout", type=float, default=120)
    p.add_argument("--keep-saves", action="store_true", help="leave the saves as the last run left them")
    p.add_argument("--location", type=int, help="relocate the disposable restored manual save before launch (0-255)")
    p = sub.add_parser("send")
    p.add_argument("command", nargs="+")
    p = sub.add_parser("shot")
    p.add_argument("file", type=Path)
    p = sub.add_parser("log")
    p.add_argument("--all", action="store_true")
    p.add_argument("--lines", type=int, default=60)
    sub.add_parser("stop")
    p = sub.add_parser("battle", help="slow final integration: one ReMixed scenario start to finish")
    p.add_argument("scenario", help="hex, e.g. 1F2")
    p.add_argument("--integration", action="store_true",
                   help="required acknowledgement that this is the slow final integration gate")
    p.add_argument("--units", type=int, default=1, help="units to place")
    p.add_argument("--second", type=int, default=0, help="units to place in a second formation group (missions: 1)")
    p.add_argument("--reveal", choices=("no", "yes"), default="no",
                   help="the answer to Reveal Treasures' question as the formation opens, if it asks (yes pays)")
    p.add_argument("--enemies", type=int, nargs="*", help="the units to defeat (default: the enemy team)")
    p.add_argument("--formation-wait", type=float, default=90,
                   help="seconds from its setup to the formation screen, at most (Reveal Treasures' question ends it)")
    p.add_argument("--timeout", type=float, default=300)
    p.add_argument("--every", type=float, default=6, help="seconds between Enter presses in its scenes")
    p.set_defaults(run=cmd_battle)
    p = sub.add_parser("checkpoint", help="capture dialogue from a forced phase or a real battle victory")
    p.add_argument("scenario", help="battle carrier scenario in hex, e.g. 34D")
    p.add_argument("event", help="generated event number in hex, e.g. 7C for event 124")
    p.add_argument("phase", type=int, nargs="?", help="condition-set phase, 0 through 15 (omit with --victory)")
    p.add_argument("--messages", type=int, nargs="+", required=True, help="runtime message numbers to capture")
    p.add_argument("--victory", action="store_true",
                   help="defeat enemies and wait for the battle's real condition to load EVENT; never force PHASE")
    p.add_argument("--condition-phase", type=int,
                   help="with --victory, the condition-set phase proving the intended transition fired")
    p.add_argument("--runtime-event",
                   help="with --victory, runtime event number in hex (default: the carrier SCENARIO)")
    p.add_argument("--advance-phase", type=int, nargs="*", default=[],
                   help="with --victory, intervening event phases whose dialogue may be confirmed (never combat)")
    p.add_argument("--dismiss-end-phase", type=int, nargs="*", default=[],
                   help="with --victory, intervening phases whose result notice gets one confirm at stable END")
    p.add_argument("--enemies", type=int, nargs="*",
                   help="with --victory, unit indexes to defeat (default: every enemy-team unit)")
    p.add_argument("--after-event", action="store_true",
                   help="confirm through EVENT and capture its first completed/handed-off frame")
    p.add_argument("--units", type=int, default=1, help="units to place in the carrier")
    p.add_argument("--second", type=int, default=0, help="units to place in a second formation group")
    p.add_argument("--reveal", choices=("no", "yes"), default="no")
    p.add_argument("--formation-wait", type=float, default=90)
    p.add_argument("--timeout", type=float, default=180)
    p.add_argument("--every", type=float, default=0.25, help="seconds between automatic confirmations")
    p.add_argument("--settle", type=float, default=6.0, help="seconds for a target box to finish opening and typing")
    p.add_argument("--prefix", type=Path, help="output prefix; -message-N.png is appended")
    p.set_defaults(run=cmd_checkpoint)
    p = sub.add_parser("formation", help="place units and commence")
    p.add_argument("--units", type=int, default=1)
    p.add_argument("--second", type=int, default=0, help="units for a second formation group")
    p.add_argument("--reveal", choices=("no", "yes"), default="no", help="the answer to Reveal Treasures, if it asks")
    p.set_defaults(run=cmd_formation)
    p = sub.add_parser("advance", help="press Enter through a scene")
    p.add_argument("--until", default="world", help="world, opening, or 'event N'")
    p.add_argument("--timeout", type=float, default=300)
    p.add_argument("--key", default="RETURN")
    p.add_argument("--shots", action="store_true", help="a screenshot before each press")
    p.add_argument("--every", type=float, default=6, help="seconds between presses")
    p.set_defaults(run=cmd_advance)
    p = sub.add_parser("defeat", help="defeat the enemies (or the units given)")
    p.add_argument("units", type=int, nargs="*")
    p.set_defaults(run=cmd_defeat)
    p = sub.add_parser("event", help="run an in-battle ReMixed event phase and screenshot its first message")
    p.add_argument("event", help="hex event number, e.g. 34D")
    p.add_argument("phase", type=int, help="phase/condition-set number, 0 through 15")
    p.add_argument("--file", type=Path, help="screenshot path (default: work/harness/shots/event-...png)")
    p.add_argument("--timeout", type=float, default=45, help="seconds to wait for the first message")
    p.add_argument("--settle", type=float, default=2, help="seconds to let the message box finish opening")
    p.set_defaults(run=cmd_event)
    p = sub.add_parser("dispatch", help="open, verify and capture a native Dispatch catalog directly")
    p.add_argument("kind", choices=("all", "propositions", "jobs", "training", "contests"), nargs="?", default="all")
    p.add_argument("--story", type=int, default=16, help="ReMixed story section, 0-16")
    p.add_argument("--month", type=int, default=1, help="contest month, 1-12")
    p.add_argument("--file", type=Path, default=WORK / "harness" / "shots" / "dispatch.png")
    p.add_argument("--settle", type=float, default=4.0,
                   help="seconds for the native Dispatch panels to finish their opening animation")
    p.add_argument("--timeout", type=float, default=20.0)
    p.add_argument("--cycles", type=int, default=0,
                   help="repeat full native navigation through Propositions and Back, checking each transition")
    p.add_argument("--leave-open", action="store_true", help="leave the captured native view open for manual inspection")
    p.set_defaults(run=cmd_dispatch)
    p = sub.add_parser("wait", help="wait for a log line")
    p.add_argument("pattern")
    p.add_argument("--timeout", type=float, default=60)
    p.add_argument("--any", action="store_true", help="a line already there counts too")
    p.set_defaults(run=cmd_wait)
    sub.add_parser("roster", help="the roster records in use").set_defaults(run=cmd_roster)
    sub.add_parser("slots", help="the battle units' roster slots, flags and battle ids").set_defaults(run=cmd_slots)
    p = sub.add_parser("maps", help="the scenarios fought on a map (from the exe's scenario table), for a quick look "
                                     "at a map: `start --world`, then `send scenario N` reaches its formation")
    p.add_argument("map", type=lambda text: int(text, 0))
    p.set_defaults(run=cmd_maps)
    p = sub.add_parser("crashes", help="the game's crashes in Windows' log")
    p.add_argument("--minutes", type=int, default=120)
    p.set_defaults(run=cmd_crashes)
    return parser


def main() -> None:
    sys.stdout.reconfigure(errors="replace")  # the log has arrows and the console may be cp1252
    args = build_parser().parse_args()
    if hasattr(args, "run"):
        args.run(args)
        return
    if args.what == "start":
        start(args)
    elif args.what == "send":
        print(send(" ".join(args.command)))
    elif args.what == "shot":
        shot(args)
    elif args.what == "log":
        log(args)
    else:
        stop(args)


if __name__ == "__main__":
    main()
