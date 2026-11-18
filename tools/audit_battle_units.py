"""Audit generated private battle carriers for unit, faction and preset-reference regressions.

Run after ``tools/build_mod.py``. This is deliberately separate from the event-staging audit: it checks ENTD records
and battle-condition references, not the order in which an event hides, removes or adds its actors.
"""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from check_story_regressions import (SCENARIO_BYTES, preset_sets, scenario_own_entries, scenario_records)

UNIT_BYTES = 40
UNIT_TEAM, UNIT_X, UNIT_Y, UNIT_ID = 0x18, 0x19, 0x1A, 0x20
UNIT_AI_MODE, UNIT_AI_TARGET = 0x23, 0x24
INITIAL_PRESENCE = 0x80
HOSTILE_AI_MODES = {0x50, 0x58, 0x70}
UNIT_CONDITION_TYPES = {4, 5, 6, 7, 8, 0xB, 0x14, 0x15, 0x26, 0x27}

# These battles deliberately have two enemy factions. 0x34B is 1-A's final battle; 0x34D is the player and Luso,
# the goblins, and Koco's chocobos. Any other carrier gaining a second enemy faction needs a deliberate review.
MULTI_FACTION_BATTLES = {0x34B: {0x10, 0x20}, 0x34D: {0x10, 0x20}}

# Unit references in a battle preset that are supplied by its formation or made by its event rather than held in the
# private ENTD carrier. Exact sets make a newly dangling condition fail instead of being waved through as "formation".
EXTERNAL_CONDITION_UNITS = {
    0x1B1: {0x0F, 0x1F},  # Reis and Beowulf, from the formation
    0x1C3: {0x03}, 0x1C4: {0x03},  # the temple's deployed unit
    0x1D6: {0x0F, 0x1F},
    0x34A: {0x22, 0x9A},  # deployed Agrias; an actor the event makes
    0x34D: {0x01},  # deployed Ramza
    0x373: {0x03}, 0x374: {0x03},
}


def audit() -> str:
    records, own = scenario_records(), scenario_own_entries()
    active_tiles = []
    battle_count = 0
    hostile_targets = 0
    factions = {}
    external = {}
    for scenario, data in own.items():
        slots = [(slot, data[slot * UNIT_BYTES:(slot + 1) * UNIT_BYTES]) for slot in range(16)]
        slots = [(slot, unit) for slot, unit in slots if unit[0]]
        for slot, unit in slots:
            x, y = unit[UNIT_X], unit[UNIT_Y]
            if ((x not in (0xFE, 0xFF) and x > 31)
                    or (y not in (0xFE, 0xFF) and y > 31)):
                raise AssertionError(f"scenario 0x{scenario:X}'s slot {slot} has impossible tile ({x}, {y})")
            if unit[UNIT_TEAM] & INITIAL_PRESENCE and x not in (0xFE, 0xFF) and y not in (0xFE, 0xFF):
                active_tiles.append((scenario, x, y, unit[UNIT_ID]))

        record = records.get(scenario, b"")
        if len(record) < SCENARIO_BYTES or not any(record[9:13]):
            continue
        battle_count += 1
        units = [unit for _, unit in slots]
        by_id = {}
        for unit in units:
            if unit[UNIT_ID] in by_id:
                raise AssertionError(f"battle scenario 0x{scenario:X} has duplicate unit id 0x{unit[UNIT_ID]:02X}")
            by_id[unit[UNIT_ID]] = unit
        if not any(unit[UNIT_TEAM] & 0x30 for unit in units):
            raise AssertionError(f"battle scenario 0x{scenario:X} has no enemy record")

        enemy_teams = {unit[UNIT_TEAM] & 0x30 for unit in units if unit[UNIT_TEAM] & 0x30}
        if len(enemy_teams) > 1:
            factions[scenario] = enemy_teams
        for unit in units:
            target = by_id.get(unit[UNIT_AI_TARGET])
            if target is None or unit[UNIT_AI_MODE] not in HOSTILE_AI_MODES:
                continue
            hostile_targets += 1
            if unit[UNIT_TEAM] & 0x30 == target[UNIT_TEAM] & 0x30:
                raise AssertionError(f"battle scenario 0x{scenario:X}'s hostile AI unit 0x{unit[UNIT_ID]:02X} "
                                     f"targets teammate 0x{target[UNIT_ID]:02X}")

        available = {unit[0] for unit in units} | set(by_id)
        missing = set()
        preset = int.from_bytes(record[0x16:0x18], "little")
        for condition_set in preset_sets(preset):
            for kind, params in condition_set:
                if (kind in UNIT_CONDITION_TYPES and params and params[0] not in available
                        and params[0] not in range(0x78, 0x80)):
                    missing.add(params[0])
        if missing:
            external[scenario] = missing

    occupied = {}
    for scenario, x, y, unit in active_tiles:
        key = scenario, x, y
        if key in occupied:
            raise AssertionError(f"scenario 0x{scenario:X}'s initially present units 0x{occupied[key]:02X} and "
                                 f"0x{unit:02X} share tile ({x}, {y})")
        occupied[key] = unit
    if factions != MULTI_FACTION_BATTLES:
        raise AssertionError(f"multi-faction battles changed: {factions!r}, expected {MULTI_FACTION_BATTLES!r}")
    if external != EXTERNAL_CONDITION_UNITS:
        raise AssertionError(f"battle presets' external unit references changed: {external!r}, "
                             f"expected {EXTERNAL_CONDITION_UNITS!r}")
    return (f"{len(own)} private carriers, {battle_count} battles: unique battle ids and active tiles; "
            f"{hostile_targets} hostile AI targets cross teams; external condition actors accounted for")


if __name__ == "__main__":
    print(audit())
