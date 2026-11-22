"""Fast, offline checks for story handoffs and the two Luso regressions.

Run after ``tools/build_mod.py``. This reads the generated event bytecode; it
does not launch the game or depend on UI timing.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "code" / "eiu" / "tools"))

from eiu_events.instructions import MARKER, read_lengths, remaster_lengths  # noqa: E402

SCRIPT = ROOT / "mod" / "fftivc.gameplay.ivaliceremixed" / "FFTIVC" / "data" / "enhanced" / "script" / "enhanced"
SCENARIOS = ROOT / "mod" / "fftivc.gameplay.ivaliceremixed" / "remixed-scenarios.bin"
PRESETS = ROOT / "mod" / "fftivc.gameplay.ivaliceremixed" / "remixed-event-presets.bin"
WORLD = bytes([0xCD, 0x80, 0x00, 0x00])
MANDALIA_SETUP_FLAGS = (b"".join(bytes([0xAE, flag, 0, 1, 0]) for flag in range(0xC1, 0xCD))
                         + bytes([0x49])
                         + b"".join(bytes([0x45, unit, 0, 0]) for unit in [0x12, *range(0x80, 0x89)])
                         + bytes([0x4A, 0x4B, 0x4D, 0x1E, 0xF1, 0x1E, 0x00, 0x0D, 0xFF]))
MENU_CHECK = bytes([0xAF, 0x00, 0x00, 0x18, 0x00, 0xAF, 0x01, 0x00, 0x27, 0x00, 0xA2, 0x01])  # build_mod.py's
MENU_FADE_IN = bytes([0x4D, 0x1E, 0xF1, 0x1E, 0x00])
YARDROW_BATTLE = 0x34A
LIVE_BATTLE_WRITE_BACK = bytes([0xCF, 0xC5, 0x00, 0x00, 0x00])
TAVERN_HOME = bytes([0xAE, 0xD4, 0x00, 0x01, 0x00])
DISPLAY_STATE_7 = bytes([0x78, 0x07, 0x3C, 0xE5, 0x38, 0x00])
DISPLAY_STATE_6 = bytes([0x78, 0x06, 0x3C, 0xE5, 0x38, 0x00])
# Generated event -> (returned preset, event selected by that preset once flag 0x8E is set). Split scene/battle and
# post-battle copies remain here deliberately: every encoded same-map edge must stay safe even if normally inactive.
LIVE_RETURN_EDGES = {
    121: (0x01C, 122), 123: (0x1FC, 124), 746: (0x0F7, 746), 753: (0x0EF, 850),
    756: (0x0F0, 851), 757: (0x0F2, 844), 766: (0x1FD, 852), 844: (0x0F2, 844),
    845: (0x1FC, 124), 850: (0x0EF, 850), 851: (0x0F0, 851), 852: (0x1FD, 852),
    886: (0x103, 886),
}
# These returns feed a recruitment scene. Their roster writer is left to that scene's join screen (display state 7),
# which writes the battle back and shows each joiner.
DEFERRED_RETURN_EDGES = {121, 123, 845}
# Of those, the returns chosen as the event runs: only the return goes on to the recruitment; the other way ends at the
# world map with no scene, so it writes the battle back with the join screen (Boco) itself in its ELSE branch (Araguay
# with Luso already recruited: its results were never written back and Boco never joined, 2026-11-18; then a silent
# 0x43 1 there made him join with no screen, reported the same day).
DEFERRED_CHOSEN_RETURNS = {121}
# ReMixed's recruitment scenes: their join screens (display state 7 inside a dark screen), which go on to dialogue on
# the same map (build_mod.MIDSCENE_JOIN_SCREENS). A silent join (0x43 1) there put the unit in the party with no
# screen (Boco after Araguay, Luso at Mandalia).
MIDSCENE_JOINS = {122: 1, 124: 1, 874: 3, 886: 1}
POSTBATTLE_CLEANUP = {
    122: (0x02, 0x17, 0x1E, 0x90, *range(0x80, 0x87), *range(0x78, 0x7D)),
    124: (0x01, 0x04, 0x07, 0x12, *range(0x80, 0x89), *range(0x78, 0x7D)),
    746: tuple(range(0x8B, 0x90)),
    757: tuple(range(0x80, 0x86)),
    844: tuple(range(0x80, 0x86)),
}
LENGTHS = remaster_lengths(read_lengths((ROOT / "work" / "files" / "remixed" / "BATTLE.BIN").read_bytes()))
SCENARIO_BYTES = 24
PRESET_BYTES, PRESET_OFFSETS = 0x220, 0x20
CONDITION_LENGTHS = (0, 2, 2, 2, 1, 2, 2, 2, 2, 2, 2, 1, 1, 1, 2, 2, 3, 3, 2, 2,
                     2, 2, 0, 0, 4, 2, 8, 1, 2, 0, 0, 0, 0, 7, 7, 3, 3, 4, 2, 2)


def event(number: int) -> bytes:
    data = (SCRIPT / f"event{number:03d}.e").read_bytes()
    if not data.startswith(MARKER):
        raise AssertionError(f"event {number} is not marked upgrade bytecode")
    return data[len(MARKER):]


def instructions(code: bytes, start: int = 0):
    at = start
    while at < len(code):
        op = code[at]
        end = at + 1 + LENGTHS[op]
        if end > len(code):
            return  # two source events end in bytes the PS1 table does not describe; their valid prefix is audited
        yield at, op, code[at + 1:end]
        at = end


def mutate_tail(code: bytes, start: int, state: dict[int, int]) -> dict[int, int]:
    """Execute the variable writes in a straight exit tail, through its END."""
    state = dict(state)
    for _, op, params in instructions(code, start):
        if op == 0xDB:
            return state
        if op == 0xAE:  # SET variable, value
            state[int.from_bytes(params[:2], "little")] = int.from_bytes(params[2:4], "little")
        elif op == 0xAF:  # SET variable, variable
            state[int.from_bytes(params[:2], "little")] = state.get(int.from_bytes(params[2:4], "little"), 0)
        elif op == 0xBE:  # ZERO variable
            state[int.from_bytes(params[:2], "little")] = 0
        elif op == 0xB0:  # ADD variable, value
            variable = int.from_bytes(params[:2], "little")
            state[variable] = state.get(variable, 0) + int.from_bytes(params[2:4], "little")
    raise AssertionError("exit tail has no END")


def last(code: bytes, pattern: bytes) -> int:
    at = code.rfind(pattern)
    if at < 0:
        raise AssertionError(f"missing {pattern.hex(' ')}")
    return at


def scenario_records() -> dict[int, bytes]:
    data = SCENARIOS.read_bytes()
    count = int.from_bytes(data[:2], "little")
    return {int.from_bytes(record[:2], "little"): record for record in
            (data[2 + SCENARIO_BYTES * k:2 + SCENARIO_BYTES * (k + 1)] for k in range(count))}


def scenario_own_entries() -> dict[int, bytes]:
    """The scenarios whose 16 40-byte ENTD records are carried in remixed-scenarios.bin."""
    data = SCENARIOS.read_bytes()
    at = 2 + SCENARIO_BYTES * int.from_bytes(data[:2], "little")
    for size in (12, 4, 6, 5):  # formations, mission places, message bases, offers
        count = int.from_bytes(data[at:at + 2], "little")
        at += 2 + size * count
    count = int.from_bytes(data[at:at + 2], "little")
    at += 2
    size = 2 + 16 * 40
    return {int.from_bytes(data[at + size * k:at + size * k + 2], "little"):
            data[at + size * k + 2:at + size * (k + 1)] for k in range(count)}


# The remaster deploys at most five of the player's units. As the formation screen opens (0x1EF0D8) it counts both
# groups' places (a formation's +8, var 0x35's and var 0x36's) into the formation's +0x40, and Start opens "Commence
# battle?" only for 1 to 5 of them (0x1EE890: [rcx+0x40] - 1, cmp 4, ja to nothing): with more, Start does nothing and
# says nothing (1-A's 0x0F0 with both its groups, 4 + 2, seen on the PC 2026-11-12). Its list of the units placed (the
# formation's +0x44) holds five. ReMixed's missions with two groups have 3 + 2 or 2 + 2; its own events set 4 + 2 in
# side chapter 1-A and 2 + 4 in the Argath campaign's Hokuten battles, which on the PS1 deploy six. The code mod's rule
# "six-places" (code/IvaliceRemixed/Patches/SixPlaces.cs) lets the remaster take six, and no more.
REMASTER_PLACES = 6
FORMATION_BYTES, FORMATION_ID, FORMATION_PLACES = 12, 10, 8
REMASTER_FORMATIONS = 0x7AE2D0 - 0x1C00  # FFT_enhanced.exe, .data 0x7AE2D0: formation n at 12 * n
SECOND_GROUP_MARK = 0x8000  # the build's MENU_SECOND_GROUP, RemixedScenarios.SecondGroupMark
SCENARIOS_CODE = ROOT / "code" / "IvaliceRemixed" / "Patches" / "RemixedScenarios.cs"


def scenario_sections(data: bytes) -> tuple[list[bytes], dict[int, bytes], dict[int, tuple[int, int]]]:
    """remixed-scenarios.bin's records, its formations by id and its battles set up after their scene (the mod's
    scenario -> its two formations, the second maybe marked), read in RemixedScenarios.Add's order."""
    u16 = lambda at: int.from_bytes(data[at:at + 2], "little")  # noqa: E731
    at = 2 + SCENARIO_BYTES * u16(0)
    records = [data[2 + SCENARIO_BYTES * k:2 + SCENARIO_BYTES * (k + 1)] for k in range(u16(0))]
    formations = {u16(at + 2 + FORMATION_BYTES * k + FORMATION_ID):
                  data[at + 2 + FORMATION_BYTES * k:at + 2 + FORMATION_BYTES * (k + 1)] for k in range(u16(at))}
    for size in (FORMATION_BYTES, 4, 6, 5, 2 + 16 * 40):  # formations, mission places, message bases, offers, units
        at += 2 + size * u16(at)
    named, at = u16(at), at + 2  # hand-offs by Ramza's name: 6 bytes, then u8-counted names
    for _ in range(named):
        names, at = data[at + 5], at + 6
        for _ in range(names):
            at += 1 + data[at]
    at += 2 + 8 * u16(at)  # mission postings
    battles, menu = u16(at), {}
    at += 2
    for _ in range(battles):
        menu[u16(at)] = (u16(at + 2), u16(at + 4))
        at += 7 + 2 * data[at + 6]
    return records, formations, menu


def second_groups_on() -> bool:
    """RemixedScenarios.SecondGroupsOn as the code mod is built: whether a marked second group is put in."""
    import re
    found = re.search(r"internal static bool SecondGroupsOn(\s*=\s*(true|false))?;", SCENARIOS_CODE.read_text())
    if not found:
        raise AssertionError("RemixedScenarios.SecondGroupsOn isn't where the check reads it")
    return found.group(2) == "true"


def remaster_formations() -> bytes:
    """The remaster's formation records, from its exe (where the mod's file has none of that number)."""
    sys.path.insert(0, str(ROOT / "tools"))
    from remixed import load_config
    exe = (Path(load_config()["target"]["tic_dir"]) / "FFT_enhanced.exe").read_bytes()
    return exe[REMASTER_FORMATIONS:REMASTER_FORMATIONS + FORMATION_BYTES * 0x400]


def battle_places(scenarios: bytes, remaster: bytes, on: bool) -> dict[int, tuple[str, list[int]]]:
    """Each of the mod's battles (its scenario) -> how its formation is set up ("record": from the world map or a
    hand-off; "after scene": its scene's run first, RemixedScenarios.MenuBattles) and its groups' places, a marked
    second group only when on (the most the scene's run can put in)."""
    records, formations, menu = scenario_sections(scenarios)

    def places(formation: int) -> int:
        record = formations.get(formation) or remaster[FORMATION_BYTES * formation:FORMATION_BYTES * (formation + 1)]
        if int.from_bytes(record[FORMATION_ID:FORMATION_ID + 2], "little") != formation:
            raise AssertionError(f"formation 0x{formation:X} is neither the mod's nor the remaster's")
        return record[FORMATION_PLACES]

    battles: dict[int, tuple[str, list[int]]] = {}
    for record in records:
        first, second = (int.from_bytes(record[at:at + 2], "little") for at in (9, 0xB))
        if first:
            battles[int.from_bytes(record[:2], "little")] = ("record", [places(f) for f in (first, second) if f])
    for scenario, (first, second) in menu.items():
        marked, second = second & SECOND_GROUP_MARK, second & ~SECOND_GROUP_MARK
        battles[scenario] = ("after scene", [places(first)] + ([places(second)] if second and (on or not marked)
                                                                else []))
    return battles


def check_places(scenarios: bytes | None = None, remaster: bytes | None = None) -> str:
    """Every battle the mod sets up has places the remaster will commence with (REMASTER_PLACES at most)."""
    on = second_groups_on()
    battles = battle_places(SCENARIOS.read_bytes() if scenarios is None else scenarios,
                            remaster_formations() if remaster is None else remaster, on)
    if battles.get(YARDROW_BATTLE) != ("record", [4, 2]):
        raise AssertionError("Yardrow Armory must have its original four- and two-place formations")
    over = {s: groups for s, (_, groups) in battles.items() if sum(groups) > REMASTER_PLACES}
    if over:
        raise AssertionError(f"battle(s) with more than the {REMASTER_PLACES} places the remaster takes, which won't "
                             "commence: " + ", ".join(f"0x{s:X} ({' + '.join(map(str, g))})"
                                                      for s, g in sorted(over.items())))
    two = sum(len(groups) == 2 for _, groups in battles.values())
    six = sum(sum(groups) == 6 for _, groups in battles.values())
    return (f"Formation places: {len(battles)} battle(s), {two} with two groups ({six} of six places), none over "
            f"{REMASTER_PLACES}" + ("" if on else " (marked second groups off)"))


FFTPACK = ROOT / "mod" / "fftivc.gameplay.ivaliceremixed" / "FFTIVC" / "data" / "enhanced" / "fftpack"
ENTD_FILES = ("battle_entd1_ent.bin", "battle_entd3_ent.bin", "battle_entd4_ent.bin",
              "event_btlevt_bin_entrydata_add_ent.bin")
UNIT_BYTES, UNIT_JOB, UNIT_PRIMARY = 40, 0x0A, 0x1D
DARK_KNIGHT, DARKNESS, SING = 0xA0, 0xE0, 0x16


MAP_RELOAD, MENU_ENTRY_VAR = 0x4C, 0x32  # build_mod.py's


def scenario_reloads(data: bytes | None = None) -> dict[tuple[int, int], bytes]:
    """remixed-scenarios.bin's last section: (the mod's scenario, the entry a map reload asks) -> its 16 units
    (build_mod.py MAP_RELOAD, RemixedScenarios.ReloadEntries); {} for data built without it."""
    data = SCENARIOS.read_bytes() if data is None else data
    u16 = lambda at: int.from_bytes(data[at:at + 2], "little")  # noqa: E731
    at = 2 + SCENARIO_BYTES * u16(0)
    for size in (FORMATION_BYTES, 4, 6, 5, 2 + 16 * 40):  # formations, mission places, message bases, offers, units
        at += 2 + size * u16(at)
    named, at = u16(at), at + 2  # hand-offs by Ramza's name: 6 bytes, then u8-counted names
    for _ in range(named):
        names, at = data[at + 5], at + 6
        for _ in range(names):
            at += 1 + data[at]
    at += 2 + 8 * u16(at)  # mission postings
    battles, at = u16(at), at + 2  # battles set up after their scene
    for _ in range(battles):
        at += 7 + 2 * data[at + 6]
    at += 2 + 10 * u16(at)  # conditional rumors
    if at >= len(data):
        return {}
    size = 4 + 16 * UNIT_BYTES
    return {(u16(at + 2 + size * k), u16(at + 4 + size * k)): data[at + 6 + size * k:at + 2 + size * (k + 1)]
            for k in range(u16(at))}


def check_map_reloads() -> str:
    """A map reload (0x4C) puts the units of the entry var 0x32 names in battle units 0-15. Where a generated event's
    code names that entry by a literal (ReMixed's numbers: 0x0F4's town scene, the mod's 511, reloads the armory with
    0x1DB), the units the port gives that reload (the mod's file's MAP_RELOAD section, else the slot of that number in
    ENTD4) must be the ones its boxes then name: every battle id from 0x80 up a MESSAGE names, up to the reload's GOTO,
    LABEL or END. The port's slot 0x1DB is one of Brave Story's rounds, so Agrias's "So these are the fiends?" named a
    0x90 the reload never brought, and had no face (a tester on 0.9.22)."""
    reloads = scenario_reloads()
    entd4 = (FFTPACK / "battle_entd4_ent.bin").read_bytes()
    seen: list[tuple[int, int]] = []
    for path in sorted(SCRIPT.glob("event*.e")):
        if not path.read_bytes().startswith(MARKER):
            continue
        number = int(path.stem[5:])
        literal, reload = None, None
        for _, op, params in instructions(event(number)):
            if 0xAC <= op <= 0xBE and int.from_bytes(params[:2], "little") == MENU_ENTRY_VAR:
                literal = int.from_bytes(params[2:4], "little") if op == 0xAE else None
            if op == MAP_RELOAD:
                reload = None
                if literal is None:
                    continue
                units = reloads.get((number, literal))
                if units is None and 0x180 <= literal < 0x200:
                    units = entd4[(literal - 0x180) * 16 * UNIT_BYTES:(literal - 0x17F) * 16 * UNIT_BYTES]
                ids = {units[u * UNIT_BYTES + 0x20] for u in range(16) if units and units[u * UNIT_BYTES]}
                reload = (literal, ids, (number, literal) in reloads)
                seen.append((number, literal))
            elif reload is not None and op == 0x10 and params[6] >= 0x80 and params[6] not in reload[1]:
                where = "the mod's file" if reload[2] else f"ENTD4's slot 0x{reload[0]:X}"
                raise AssertionError(f"event {number}'s map reload with entry 0x{reload[0]:X} ({where}) has no unit "
                                     f"0x{params[6]:X}, which its box names (line {int.from_bytes(params[2:6], 'little')}"
                                     "): no face there (MAP_RELOAD)")
            elif op in (0x0D, 0x0E, 0xDB):
                reload = None
    unused = set(reloads) - set(seen)
    if unused:
        raise AssertionError(f"map reload units no event asks for: {sorted(unused)}")
    return (f"Map reloads naming their entry: {', '.join(f'event {n} of 0x{e:X}' for n, e in seen) or 'none'}, "
            "the units their boxes name brought in")


def check_yardrow_battle() -> str:
    record = scenario_records()[YARDROW_BATTLE]
    if record[2] != 26 or record[5:7] != bytes([0x08, 0x29]):
        raise AssertionError("Yardrow Armory's battle lost its map or music from the Preparation Menu")
    opening = [(op, params) for _, op, params in instructions(event(YARDROW_BATTLE))]
    clear_fade = [(0x4D, b"\x1e"), (0xF1, b"\x1e\x00"), (0x0D, b"\xff")]
    if not any(opening[i:i + len(clear_fade)] == clear_fade for i in range(len(opening))):
        raise AssertionError("Yardrow Armory's opening does not clear and wait out the town scene's fade")
    mesh = SCRIPT.parents[1] / "bg" / "meshes" / "map_026_mesh.bin"
    if b"_panel_ui\x00" not in mesh.read_bytes():
        raise AssertionError("Yardrow Armory's enhanced mesh has no tile highlights")
    return "Yardrow Armory: battle map and tracks, tile panels, opening fade cleared"


def check_primaries() -> str:
    """ReMixed's primary skillsets as the port gives them (build_mod.py remixed_primary): none names the remaster's
    story skillsets 0x4D and 0x4E (ReMixed's Bard and woman's Darkness), and no Dark Knight sings."""
    dark_knights = darkness = 0
    for name in ENTD_FILES:
        data = (FFTPACK / name).read_bytes()
        for at in range(0, len(data) - UNIT_BYTES + 1, UNIT_BYTES):
            job, primary = data[at + UNIT_JOB], data[at + UNIT_PRIMARY]
            where = f"{name} unit {at // UNIT_BYTES} (entry 0x{at // (16 * UNIT_BYTES):X})"
            if primary in (0x4D, 0x4E):
                raise AssertionError(f"{where}'s primary skillset is the remaster's story skillset 0x{primary:X}")
            if job == DARK_KNIGHT:
                dark_knights += 1
                darkness += primary == DARKNESS
                if primary == SING:
                    raise AssertionError(f"{where} is a Dark Knight with the Bard's Sing as its primary")
    return f"Primary skillsets: {dark_knights} Dark Knights, {darkness} with Darkness known in full, none singing"


def preset_sets(number: int) -> list[list[tuple[int, tuple[int, ...]]]]:
    """Condition records in each set of a generated ReMixed preset, excluding its event action only by type."""
    data = PRESETS.read_bytes()[number * PRESET_BYTES:(number + 1) * PRESET_BYTES]
    if len(data) != PRESET_BYTES:
        raise AssertionError(f"preset 0x{number:X} is outside {PRESETS.name}")
    words = [int.from_bytes(data[PRESET_OFFSETS + 2 * k:PRESET_OFFSETS + 2 * k + 2], "little")
             for k in range((PRESET_BYTES - PRESET_OFFSETS) // 2)]
    starts = sorted({int.from_bytes(data[2 * k:2 * k + 2], "little") // 2 for k in range(16)
                     if k == 0 or int.from_bytes(data[2 * k:2 * k + 2], "little")})
    used = max((k for k, value in enumerate(words) if value), default=-1) + 1
    sets = []
    for n, start in enumerate(starts):
        end = starts[n + 1] if n + 1 < len(starts) else used
        records, at = [], start
        while at < end:
            kind = words[at]
            # A set's terminal action is encoded as just (0x19, event); the runtime returns as it fires it, before its
            # ordinary condition-length-table step matters.
            if kind == 0x19 and at + 2 == end:
                records.append((kind, (words[at + 1],)))
                at = end
                continue
            if kind >= len(CONDITION_LENGTHS):
                raise AssertionError(f"preset 0x{number:X} has unknown condition type 0x{kind:X}")
            stop = at + 1 + CONDITION_LENGTHS[kind]
            if stop > end:
                raise AssertionError(f"preset 0x{number:X}'s set {n} ends inside condition type 0x{kind:X}")
            records.append((kind, tuple(words[at + 1:stop])))
            at = stop
        sets.append(records)
    return sets


def dynamic_message_ids(code: bytes) -> list[int]:
    """Line ids of MESSAGE instructions whose low half is selected by the preceding PATCH_NEXT."""
    pending, selected = [], []
    for _, op, params in instructions(code):
        if op == 0xE6:
            pending.append(params[0] & 0x3F)
            continue
        if op == 0x10 and 2 in pending:
            selected.append(int.from_bytes(params[2:6], "little"))
        pending = []
    return selected


def check() -> list[str]:
    # The mod also ships a handful of lightly patched stock event files. Only marked ReMixed bytecode uses the EIU
    # instruction lengths and belongs in these event-code audits.
    paths = [path for path in sorted(SCRIPT.glob("event*.e")) if path.read_bytes().startswith(MARKER)]
    handoffs = []
    direct_handoffs = []
    unsupported_direct_modes = []
    stale = []
    clear = bytes([0xAE, 0x8E, 0x00, 0x00, 0x00])
    for path in paths:
        code = path.read_bytes()[len(MARKER):]
        for at, op, params in instructions(code):
            if op == 0xCD:
                handoffs.append((path.name, at, bytes(params)))
                if len(params) >= 3 and params[0] == 0x81:
                    direct_handoffs.append((int(path.stem[5:]), int.from_bytes(params[1:3], "little")))
                elif len(params) >= 3 and params[0] & 0xF3 in (0x82, 0x83):
                    unsupported_direct_modes.append((path.name, at, params[0], int.from_bytes(params[1:3], "little")))
        if WORLD + clear in code:
            stale.append(path.name)
    if stale:
        raise AssertionError(f"return flag clear follows a world handoff in: {', '.join(stale)}")
    if unsupported_direct_modes:
        raise AssertionError("direct scenario handoffs use remaster-incompatible modes: " + ", ".join(
            f"{name}@0x{at:X} mode 0x{mode:X} -> 0x{target:X}"
            for name, at, mode, target in unsupported_direct_modes))
    opening_handoff = (0x193, 0x194)
    psp_opening_handoff = (0x2F0, 0x194)
    if direct_handoffs.count(opening_handoff) != 1 or direct_handoffs.count(psp_opening_handoff) != 1:
        raise AssertionError("both 1-A opening scripts must chain once to 0x194 with direct mode 0x81")

    # A recruitment scene's join is display state 7 in a dark screen: the remaster's join screen, which writes the battle
    # back and shows each joiner. A silent join (0x43 1) there leaves the unit in the party with no screen.
    midscene_joins = 0
    for number, expected in MIDSCENE_JOINS.items():
        code = event(number)
        parsed = list(instructions(code))
        found = 0
        for dark, (_, op, _) in enumerate(parsed):
            if op != 0x76:
                continue
            remove = next((n for n in range(dark + 1, len(parsed)) if parsed[n][1] == 0x77), len(parsed))
            found += sum(bytes([parsed[n][1]]) + parsed[n][2] + bytes([parsed[n + 1][1]]) + parsed[n + 1][2]
                         == DISPLAY_STATE_7 for n in range(dark + 1, remove - 1))
        if found != expected:
            raise AssertionError(f"event {number} shows {found} join screen(s) in a dark screen, expected {expected}")
        silent = sum(op == 0x43 and params[:1] == b"\x01" for _, op, params in parsed)
        if silent:
            raise AssertionError(f"event {number} has {silent} silent join(s) (0x43 1), which skip the join screen")
        midscene_joins += found

    # Event 8's same-battlefield returns cannot use the remaster's display state 7: its roster write-back is correct,
    # but the surrounding state destroys the map before the returned preset starts its dialogue event. Every encoded
    # return instead calls the mod's synchronous native writer, sets the tavern-home flag, and keeps PostEvent mode 0.
    live_sites: dict[int, tuple[int, int]] = {}
    else_writebacks: dict[int, int] = {}
    destructive_returns = []
    for path in paths:
        number = int(path.stem[5:])
        code = path.read_bytes()[len(MARKER):]
        parsed = list(instructions(code))
        for index, (at, op, params) in enumerate(parsed):
            if index + 1 < len(parsed) and bytes([op]) + params + bytes([parsed[index + 1][1]]) \
                    + parsed[index + 1][2] == DISPLAY_STATE_7:
                # A chosen deferred return's other way (ELSE, to the world map) writes the battle back itself, with
                # the join screen; its preset load is the empty one and the mode-0 hand-off is the return's alone.
                if index and number in DEFERRED_CHOSEN_RETURNS and parsed[index - 1][1] == 0x03:
                    else_writebacks[number] = else_writebacks.get(number, 0) + 1
                    continue
                window = parsed[index + 2:index + 26]
                if any(later == 0xCE for _, later, _ in window) \
                        and any(later == 0xCD and args == bytes(3) for _, later, args in window):
                    destructive_returns.append((path.name, at))
            if bytes([op]) + params != LIVE_BATTLE_WRITE_BACK:
                continue
            window = parsed[index + 1:index + 25]
            if not window or bytes([window[0][1]]) + window[0][2] != TAVERN_HOME:
                raise AssertionError(f"{path.name}@0x{at:X}: live write-back does not set tavern-home flag next")
            presets = [int.from_bytes(args, "little") for _, later, args in window if later == 0xCE]
            if not presets or not any(later == 0xCD and args == bytes(3) for _, later, args in window):
                raise AssertionError(f"{path.name}@0x{at:X}: live write-back has no same-map conditional handoff")
            if number in live_sites:
                raise AssertionError(f"event {number} has more than one live write-back")
            live_sites[number] = (at, presets[0])
    if destructive_returns:
        raise AssertionError("same-map battle returns tear down their map: "
                             + ", ".join(f"{name}@0x{at:X}" for name, at in destructive_returns))
    if else_writebacks != {number: 1 for number in DEFERRED_CHOSEN_RETURNS}:
        raise AssertionError(f"chosen deferred returns' world-map write-backs are {else_writebacks}, "
                             f"expected one each in {sorted(DEFERRED_CHOSEN_RETURNS)}")
    expected_live = set(LIVE_RETURN_EDGES) - DEFERRED_RETURN_EDGES
    if set(live_sites) != expected_live:
        raise AssertionError(f"live return events are {sorted(live_sites)}, expected {sorted(expected_live)}")
    deferred_presets: dict[int, int] = {}
    for number in DEFERRED_RETURN_EDGES:
        code = event(number)
        if code.count(DISPLAY_STATE_6 + TAVERN_HOME) != 1:
            raise AssertionError(f"event {number}'s deferred return does not show results then set tavern-home once")
        parsed = list(instructions(code))
        index = next(i for i, (_, op, params) in enumerate(parsed)
                     if bytes([op]) + params == TAVERN_HOME and i >= 2
                     and b"".join(bytes([p[1]]) + p[2] for p in parsed[i - 2:i]) == DISPLAY_STATE_6)
        window = parsed[index + 1:index + 25]
        presets = [int.from_bytes(args, "little") for _, later, args in window if later == 0xCE]
        if not presets or not any(later == 0xCD and args == bytes(3) for _, later, args in window):
            raise AssertionError(f"event {number}'s deferred writer has no same-map conditional handoff")
        deferred_presets[number] = presets[0]
    for number, (expected_preset, expected_target) in LIVE_RETURN_EDGES.items():
        actual_preset = deferred_presets[number] if number in DEFERRED_RETURN_EDGES else live_sites[number][1]
        if actual_preset != expected_preset:
            raise AssertionError(f"event {number}'s live return loads preset 0x{actual_preset:X}, expected 0x{expected_preset:X}")
        targets = [records[-1][1][0] for records in preset_sets(expected_preset)
                   if (1, (0x8E, 1)) in records and records and records[-1][0] == 0x19]
        if targets != [expected_target]:
            raise AssertionError(f"preset 0x{expected_preset:X}'s flag-0x8E return targets {targets}, expected [{expected_target}]")

    # A source-authored post-battle cast reset must erase the remaster's drawable before removing each logical unit;
    # otherwise defeated actors remain as corpses/crystals behind the following scene. The allowlist excludes victory
    # scenes that deliberately keep a body or crystal for their choreography.
    cleanup_erases = 0
    for number, actors in POSTBATTLE_CLEANUP.items():
        code = event(number)
        run = b"".join(bytes([0x46, actor, 0x00, 0x3D, actor, 0x00]) for actor in actors)
        if code.count(run) != 1:
            raise AssertionError(f"event {number}'s drawable-safe cleanup occurs {code.count(run)} times")
        cleanup_erases += len(actors)

    # Araguay battle's common exit: both paths finish the remaster's story state.
    araguay = event(121)
    state = mutate_tail(araguay, last(araguay, WORLD), {0x6E: 0x10, 0x287: 0})
    if (state.get(0x6E), state.get(0x287)) != (0x11, 1):
        raise AssertionError(f"Araguay exit is story {state.get(0x6E)!r}, flag 0x287 {state.get(0x287)!r}")

    # Luso's post-Araguay thanks must consume both the return and met-Luso flags.
    thanks = event(122)
    thanks_start = last(thanks, clear + WORLD)
    state = mutate_tail(thanks, thanks_start, {0x8E: 1, 0x93: 1})
    if (state.get(0x8E), state.get(0x93)) != (0, 0):
        raise AssertionError(f"Luso thanks leaves flags 0x8E/0x93 at {state.get(0x8E)!r}/{state.get(0x93)!r}")

    # Mandalia's closing marks Luso met, but consumes its post-battle return.
    mandalia = event(124)
    set_met = bytes([0xAE, 0x93, 0x00, 0x01, 0x00])
    mandalia_start = last(mandalia, set_met + clear + WORLD)
    state = mutate_tail(mandalia, mandalia_start, {0x8E: 1, 0x93: 0})
    if (state.get(0x8E), state.get(0x93)) != (0, 1):
        raise AssertionError(f"Mandalia exit leaves flags 0x8E/0x93 at {state.get(0x8E)!r}/{state.get(0x93)!r}")

    # Mandalia must cross a real scenario boundary between its scene and battle. Returning event 123 to scenario 123
    # let the scene's preset keep walking: its plain-victory set then ran recruitment event 124 without a battle.
    records = scenario_records()
    # ReMixed's Biggs/Wedge record was uniquely 0xF060: 0x60 is map 96, but 0xF0 is not a map-state selector the
    # remaster uses. It rendered only black/sky. Audit every imported record as well as the exact Gariland carrier,
    # so another unsupported state cannot be introduced under a different event number.
    gariland = records[0x2FF]
    if gariland[2:4] != bytes([0x60, 0x00]) or any(gariland[9:13]):
        raise AssertionError(f"Gariland carrier has map/state {gariland[2:4].hex(' ')} or a formation")
    supported_map_states = {0, 1, 2, 3, 4, 0x10, 0x20}
    invalid_map_states = [(number, record[3]) for number, record in records.items()
                          if record[3] not in supported_map_states]
    if invalid_map_states:
        raise AssertionError("unsupported imported map states: "
                             + ", ".join(f"0x{number:X}=0x{state:02X}" for number, state in invalid_map_states))

    # The scene sets flag 0x92, which unlocks Mandalia's Luso scene. It must first commit Gariland as the current
    # node or a party that entered from Mandalia immediately re-enters that newly satisfied start on world return.
    gariland_exit = bytes([0xAE, 0x31, 0x00, 0x06, 0x00]) + WORLD
    biggs = event(0x2FF)
    if biggs.count(gariland_exit) != 1:
        raise AssertionError(f"Gariland scene has {biggs.count(gariland_exit)} location-safe world exits")
    # Its last line is the Rendezvous-unlocked notice. The notice conversion must keep the source box number: after
    # acknowledging it, ReMixed closes that box and waits once more before returning to the world map. Opening box 2
    # while retaining the source close of box 3 leaves the visible notice open and deadlocks that second wait.
    biggs_instructions = list(instructions(biggs))
    last_message = max(k for k, (_, op, _) in enumerate(biggs_instructions) if op == 0x10)
    message_box = biggs_instructions[last_message][2][1] & 3
    notice_close = next((params[0] for _, op, params in biggs_instructions[last_message + 1:]
                         if op == 0x51 and params[1:5] == bytes([0xFF]) * 4), None)
    if notice_close != message_box:
        raise AssertionError(f"Gariland's Rendezvous notice opens box {message_box} but closes box {notice_close}")
    # Audit the whole generated graph for this bug class, not only Mandalia.  A direct scene -> battle hand-off must
    # change ENTD entry number or the remaster retains the scene's erased/hidden battle-unit objects.  This currently
    # covers all seven such edges, including the three synthetic BATTLE_SPLITS.
    scene_battles = [(source, target) for source, target in direct_handoffs
                     if source in records and target in records
                     and not any(records[source][9:13]) and any(records[target][9:13])]
    stale_entries = [(source, target, int.from_bytes(records[source][7:9], "little"))
                     for source, target in scene_battles if records[source][7:9] == records[target][7:9]]
    if stale_entries:
        raise AssertionError("scene -> battle hand-offs reuse a stale ENTD entry: "
                             + ", ".join(f"0x{s:X}->0x{t:X} entry 0x{e:X}" for s, t, e in stale_entries))
    own = scenario_own_entries()
    # A formation supplies the player's roster Ramza. A data-built Ramza retained from the preceding scene becomes a
    # second guest copy, as Mandalia did. Audit every direct scene -> battle carrier for this duplicate-party leak.
    duplicate_ramzas = []
    for source, target in scene_battles:
        units = own.get(target, b"")
        for slot in range(len(units) // 40):
            unit = units[slot * 40:(slot + 1) * 40]
            if unit[0] == 1:
                duplicate_ramzas.append((source, target, slot, unit[0x20]))
    if duplicate_ramzas:
        raise AssertionError("scene -> battle carriers retain data-built Ramza actors: "
                             + ", ".join(f"0x{s:X}->0x{t:X} slot {u} id 0x{i:X}"
                                         for s, t, u, i in duplicate_ramzas))

    scene, battle = records[123], records[0x34D]
    if any(scene[9:13]) or not int.from_bytes(battle[9:11], "little"):
        raise AssertionError("Mandalia scene has a formation, or its split battle has none")
    handoff = bytes([0xCD, 0x81, 0x4D, 0x03])
    opening = event(123)
    if opening.count(handoff) != 1:
        raise AssertionError(f"Mandalia opening has {opening.count(handoff)} handoffs to split battle 0x34D")
    if handoff in event(0x34D):
        raise AssertionError("Mandalia split battle hands back to itself instead of beginning combat")
    battle_event = event(0x34D)
    if battle_event.count(MANDALIA_SETUP_FLAGS) != 1:
        raise AssertionError("Mandalia battle does not visibly activate its prepared cast and clear the scene fade exactly once")
    battle_preset = int.from_bytes(battle[0x16:0x18], "little")
    battle_sets = preset_sets(battle_preset)
    recruit_sets = [condition_set for condition_set in battle_sets
                    if condition_set and condition_set[-1] == (0x19, (124,))]
    victory_sets = [condition_set for condition_set in recruit_sets
                    if any(kind == 0x16 for kind, _ in condition_set)]
    if len(recruit_sets) != 1 or len(victory_sets) != 1:
        raise AssertionError(f"Mandalia battle preset 0x{battle_preset:X} has a malformed victory -> recruitment set")

    # The second carrier must contain the actual battle cast, not merely force a reload: Luso and all nine opponents.
    units = own.get(0x34D)
    if units is None:
        raise AssertionError("Mandalia split battle has no private fresh ENTD records")
    records_34d = [units[k * 40:(k + 1) * 40] for k in range(16)]
    lusos = [unit for unit in records_34d if unit[0] == 0x12]
    ramzas = [unit for unit in records_34d if unit[0] == 0x01]
    enemies = [unit for unit in records_34d if unit[0] and unit[0x18] & 0x30]
    if len(lusos) != 1 or ramzas or len(enemies) != 9:
        raise AssertionError(f"Mandalia fresh entry has {len(lusos)} Luso record(s), {len(ramzas)} duplicate "
                             f"Ramza record(s), and {len(enemies)} enemies")

    # Recruitment deliberately replaces the four battle actors with four scene actors once. Re-running its satisfied
    # victory set made Ramza and Delita repeatedly disappear/reappear; drawing or erasing them inside 0x0EB would cause
    # the same symptom even with the handoff fixed.
    recruit = event(124)
    for actor in (1, 4, 7, 0x12):
        removed = sum(op == 0x3D and int.from_bytes(params[:2], "little") == actor
                      for _, op, params in instructions(recruit))
        added = sum(op == 0x45 and int.from_bytes(params[:2], "little") == actor
                    for _, op, params in instructions(recruit))
        erased = sum(op == 0x46 and int.from_bytes(params[:2], "little") == actor
                     for _, op, params in instructions(recruit))
        drawn = sum(op == 0x44 and int.from_bytes(params[:2], "little") == actor
                    for _, op, params in instructions(recruit))
        if (removed, added, erased, drawn) != (1, 1, 1, 0):
            raise AssertionError(f"Mandalia recruit actor 0x{actor:X} lifecycle is remove/add/erase/draw "
                                 f"{removed}/{added}/{erased}/{drawn}, expected 1/1/1/0")

    # All four lines selected at runtime need a real 0x0004xxxx line template; 0xFFFFFFFF produces a blank box even
    # after PATCH_NEXT writes the low half. This covers both answers and the normal/Argath speaker branches.
    selected = dynamic_message_ids(opening)
    # The event has other legitimate PATCH_NEXT-selected dialogue too. The four repaired 0xFFFF operands all use
    # its longest line as their sizing template (therefore the greatest line id in this event), while every dynamic
    # operand must retain the 0x0004 high half MoveMessage recognizes.
    if not selected or any(line >> 16 != 4 for line in selected) or selected.count(max(selected)) != 4:
        raise AssertionError(f"Mandalia's runtime-selected lines lack four valid repair templates: {selected!r}")

    # ReMixed's riders (build_mod.py seat_riders): battle ids 0xC0, 0xC2, ... ride the next id, seated by ReMixed's
    # 0x191 as the battle begins, which the port doesn't run. The river camp's battle run (the mod's 510, past
    # ReMixed's check: var 0x18 == var 0x27) seats its five Mindflayers before its fade-in; 0x0FB's battle copy
    # (843) its three goblins.
    river = event(510)
    check_at = river.find(MENU_CHECK)
    fade_at = river.find(MENU_FADE_IN, check_at)
    if check_at < 0 or fade_at < 0:
        raise AssertionError("the river camp's battle run (its check, then its fade-in) isn't where expected")
    seated = sorted(params[0] for at, op, params in instructions(river, check_at)
                    if at < fade_at and op == 0x28 and params[6] == 0x63)
    if seated != [0xC0, 0xC2, 0xC4, 0xC6, 0xC8]:
        raise AssertionError(f"the river camp's battle run seats riders {[hex(u) for u in seated]}, not C0-C8")
    goblins = sorted(params[0] for _, op, params in instructions(event(843))
                     if op == 0x28 and params[6] == 0x63 and 0xC0 <= params[0] <= 0xCF)
    if goblins != [0xC0, 0xC4, 0xC8]:
        raise AssertionError(f"0x0FB's battle seats riders {[hex(u) for u in goblins]}, not C0, C4 and C8")

    return [f"{len(paths)} generated events, {len(handoffs)} handoffs: no stale return cleanup",
            "Gariland: valid map state, no formation, Rendezvous notice closes, commits location 6 before world return",
            "Araguay exit: story 0x11, completion flag 0x287 set",
            "Luso exits: return flag consumed; met-Luso flag cleared at Araguay and set at Mandalia",
            f"Fresh battle entries: {len(scene_battles)} scene -> battle hand-offs use distinct ENTD carriers",
            f"Recruitment scenes: {midscene_joins} join screen(s) in a dark screen, no silent joins; Araguay's "
            "world-map way writes back with the join screen",
            f"Same-map battle returns: {len(live_sites)} exact live write-back edge(s), no display-state teardown",
            f"Post-battle cleanup: {cleanup_erases} corpse/crystal drawable(s) erased before logical removal",
            "Mandalia: no duplicate Ramza, prepared and visibly activated Luso + nine enemies, "
            "scene fade cleared, one victory -> recruitment, stable actors, "
            "four runtime lines valid",
            "Riders: the river camp's battle run seats its five, 0x0FB's battle its three",
            check_places(), check_primaries(), check_map_reloads(), check_yardrow_battle(), check_yuguo_woods(),
            check_extended_statuses(),
            check_bervenia()]


def check_bervenia() -> str:
    from maps import read_classic_mesh

    records = scenario_records()
    first, scene, battle = (records[n] for n in (0x2EA, 0x1B6, 0x2EB))
    if (first[2], scene[2], battle[2]) != (75, 119, 119) or any(scene[9:13]) or not any(battle[9:11]):
        raise AssertionError("Bervenia must stage the summon before the second deployment on map 119")
    if len({r[7:9] for r in (first, scene, battle)}) != 3:
        raise AssertionError("Bervenia's successive setups reuse cached units")
    if bytes.fromhex("cd 81 b6 01") not in event(0x2EA) or bytes.fromhex("cd 81 eb 02") not in event(0x1B6):
        raise AssertionError("Bervenia does not reach the summoning scene and then the formation")
    if MENU_FADE_IN + bytes.fromhex("0d fa") not in event(0x2EB):
        raise AssertionError("the manifestation battle replays its summoning scene")
    entries = scenario_own_entries()
    for number, expected in ((0x1B6, range(0x8A, 0x90)), (0x2EB, range(0x80, 0x86))):
        units = entries[number]
        present = [units[i + 0x20] for i in range(0, len(units), UNIT_BYTES) if units[i + 0x18] & 0xC0]
        if present != list(expected):
            raise AssertionError(f"Bervenia 0x{number:X} starts with the wrong combatants: {present}")
    preset = int.from_bytes(battle[0x16:0x18], "little")
    if preset != 0xF7 or preset_sets(preset)[0][-1] != (0x19, (0x2EB,)):
        raise AssertionError("existing Bervenia battle autosaves must retain their preset and battle opening")
    if [(1, (0x85, 0)), (6, (0x80, 0)), (0x19, (0x2EA,))] not in preset_sets(preset):
        raise AssertionError("the manifestation's defeat must start the victory scene")
    for name, tile, height in (("map_map075_9", (0, 11), 6), ("new_map_new_map075_9", (0, 11), 6),
                               ("map_map119_7", (9, 2), 5)):
        mesh = read_classic_mesh((FFTPACK / "map" / f"{name}.bin").read_bytes())
        heights = {(t.x, t.z): t.raw[2] for t in mesh.tiles or [] if t.level == 0}
        if heights.get(tile) != height:
            raise AssertionError(f"Bervenia's {name} has the wrong height at {tile}")
    return "Bervenia: summoning scene before deployment, six manifestations at battle setup, boss victory and terrain heights"


def check_yuguo_woods() -> str:
    battle = list(instructions(event(0x2F5)))
    ritual_calls = [(offset, params) for offset, op, params in battle if op == 0xCF and params == b"\xC6\0\0\0"]
    if len(ritual_calls) != 1 or any(op == 0x92 and params[:3] == b"\x80\0\x83" for _, op, params in battle):
        raise AssertionError("Yuguo's ritual must use the native Charging status setter")
    _, ritual_delay_op, ritual_delay = battle[next(i for i, (at, _, _) in enumerate(battle)
                                                 if at == ritual_calls[0][0]) + 1]
    if (ritual_delay_op, ritual_delay) != (0xF1, b"\x14\0"):
        raise AssertionError("Yuguo's ritual lost its 20-frame pause")
    victory_at = next(i for i, (_, op, params) in enumerate(battle) if (op, params) == (0x0E, b"\x0F"))
    closing_at = next(i for i, (_, op, params) in enumerate(battle) if (op, params) == (0x0E, b"\x10"))
    victory = [(op, params) for _, op, params in battle[victory_at:closing_at]]
    if any(op == 0x16 for op, _ in victory) or (0x78, b"\x02\x3C") not in victory:
        raise AssertionError("Yuguo's victory should open the results without an earlier button pause")
    troupe_boxes = [params[1] for _, op, params in instructions(event(0x2F6))
                   if op == 0x10 and params[6:8] in (b"\x95\0", b"\x97\0")]
    if len(troupe_boxes) < 8 or set(troupe_boxes) != {0x19}:
        raise AssertionError(f"the troupe leader and worker's boxes should be portraitless: {troupe_boxes!r}")
    return "Yuguo Woods: ritual uses Charging, results open without a hidden input wait, troupe boxes are portraitless"


def check_extended_statuses() -> str:
    expected = {0x1F7: [0x88] * 2, 0x1FB: [0x84] * 9, 0x349: [0x88] * 3,
                0x1C4: [0x9D, 0x9C, 0x9B, 0x90], 0x2F5: [0x83]}
    converted = {}
    for path in SCRIPT.glob("event*.e"):
        data = path.read_bytes()
        if not data.startswith(MARKER):
            continue
        stream = list(instructions(data[len(MARKER):]))
        modes = []
        for k, (_, op, params) in enumerate(stream):
            if op == 0x92 and params[2] >= 0x80:
                raise AssertionError(f"{path.name}: extended InflictStatus would execute as a knockout")
            if op != 0xCF or params != b"\xC6\0\0\0":
                continue
            if k < 2 or stream[k - 2][1] != 0xAE or stream[k - 2][2][:2] != b"\x7E\x0B" \
                    or stream[k - 1][1] != 0xAE or stream[k - 1][2][:2] != b"\x7F\x0B":
                raise AssertionError(f"{path.name}: extended InflictStatus call lacks its actor and mode")
            modes.append(int.from_bytes(stream[k - 1][2][2:], "little"))
        if modes:
            converted[int(path.stem[5:])] = modes
    for number, modes in expected.items():
        if converted.get(number) != modes:
            raise AssertionError(f"event 0x{number:X}: extended status modes {converted.get(number)}, expected {modes}")
    return f"Extended InflictStatus: {sum(map(len, converted.values()))} status operations routed in {len(converted)} scripts"


def check_world_objectives(path: Path) -> str:
    from build_mod import REMIXED_WORLD_STARTS, SIDE_CHAPTER_A_FLAG, SIDE_CHAPTER_DESTINATIONS

    data = json.loads(path.read_text(encoding="utf-8"))
    captions = data["builds"]["fft_enhanced.exe"]["rules"]["side-chapter-objectives"]
    covered = set()
    for location, checks, _, _ in REMIXED_WORLD_STARTS:
        if (SIDE_CHAPTER_A_FLAG, 1) not in checks:
            continue
        for variable, value, *kind in checks:
            if variable != 0x6E or kind:
                continue
            progress = str(value)
            expected = f"Travel to {SIDE_CHAPTER_DESTINATIONS[location]}."
            if captions.get(progress) != expected:
                raise AssertionError(f"1-A progress {progress} must direct the player to {expected}")
            covered.add(progress)
    if "1" not in covered:
        if captions.get("1") != captions["0"]:
            raise AssertionError("the prologue's progress-1 objective must match progress 0")
        covered.add("1")
    if set(captions) != covered:
        raise AssertionError("1-A objective coverage differs from the world starts")
    return f"1-A: {len(captions)} captions follow the world starts, including conditional starts and the prologue"


def main() -> None:
    from build_mod import CODE_DATA, MOD_DIR

    print("\n".join([*check(), check_world_objectives(MOD_DIR / CODE_DATA)]))


if __name__ == "__main__":
    main()
