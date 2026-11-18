"""List every data-table value ReMixed changes, and the changed bytes no table explains.

    python -I tools/tables.py

Reads the vanilla and ReMixed discs that `remixed.py prepare` builds, plus the
change map from `remixed.py diff`. Writes:

    data/remixed/*.csv                        one row per changed field
    data/remixed/code_blocks.csv              changed bytes outside every known table
    docs/generated/<patch>-tables.md          readable summary of the table changes
    docs/generated/<patch>-code-changes.md    summary of the leftover changes

Table positions and layouts follow FFTPatcher (PatcherLib/ISOPatching/PsxIso.cs
and Datatypes/). Flag bytes list their bits from the highest (0x80) down.
"""

from __future__ import annotations

import csv
import json
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable

# `python -I` leaves the script's folder off sys.path, so add it for the sibling modules.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from names import Names  # noqa: E402
from psxdisc import Disc  # noqa: E402
from remixed import REPORT_DIR, ROOT, WORK, load_config, ppf_path, remixed_bin, slug, vanilla_bin  # noqa: E402

DATA_DIR = ROOT / "data" / "remixed"
SCUS = "SCUS_942.21"
BATTLE = "BATTLE.BIN"
WORLD = "WORLD/WORLD.BIN"
WLDCORE = "WORLD/WLDCORE.BIN"

# Where each file sits in PS1 RAM: RAM address = base + file offset.
RAM_BASE = {SCUS: 0x8000F800, BATTLE: 0x80067000}

Fields = dict[str, object]

# ---------------------------------------------------------------- bit names

STATUSES = [
    "No Effect", "Crystal", "Dead", "Undead", "Charging", "Jump", "Defending", "Performing",
    "Petrify", "Invite", "Darkness", "Confusion", "Silence", "Blood Suck", "Dark/Evil Looking", "Treasure",
    "Oil", "Float", "Reraise", "Transparent", "Berserk", "Chicken", "Frog", "Critical",
    "Poison", "Regen", "Protect", "Shell", "Haste", "Slow", "Stop", "Wall",
    "Faith", "Innocent", "Charm", "Sleep", "Don't Move", "Don't Act", "Reflect", "Death Sentence",
]
ELEMENTS = ["Fire", "Lightning", "Ice", "Wind", "Earth", "Water", "Holy", "Dark"]
EQUIPMENT = [
    "Unused", "Knife", "Ninja Blade", "Sword", "Knight's Sword", "Katana", "Axe", "Rod",
    "Staff", "Flail", "Gun", "Crossbow", "Bow", "Instrument", "Book", "Spear",
    "Pole", "Bag", "Cloth", "Shield", "Helmet", "Hat", "Hair Adornment", "Armor",
    "Clothing", "Robe", "Shoes", "Armguard", "Ring", "Armlet", "Cloak", "Perfume",
]
AI_FLAGS = [
    "HP", "MP", "Cancel Status", "Add Status", "Stats", "Unequip", "Target Enemies", "Target Allies",
    "Line of Sight", "Reflectable", "Undead Reverse", "Unknown1", "Allow Randomly", "Unknown2", "Unknown3", "Silence",
    "", "Unknown4", "Unknown5", "Unknown6", "Unknown7", "Unknown8", "Unknown9", "Unknown10",
]
# (name, stored inverted)
ABILITY_FLAGS = [
    [("Force Self Target", 0), ("", 0), ("Weapon Range", 0), ("Vertical Fixed", 0),
     ("Vertical Tolerance", 0), ("Weapon Strike", 0), ("Auto", 0), ("Target Self", 1)],
    [("Hit Enemies", 1), ("Hit Allies", 1), ("Top-down Target", 0), ("Follow Target", 1),
     ("Random Fire", 0), ("Linear Attack", 0), ("Three Directions", 0), ("Hit Caster", 1)],
    [("Reflect", 0), ("Arithmetick", 0), ("Silence", 1), ("Mimic", 1),
     ("Normal Attack", 0), ("Persevere", 0), ("Show Quote", 0), ("Animate Miss", 0)],
    [("Counter Flood", 0), ("Counter Magic", 0), ("Direct", 0), ("Shirahadori", 0),
     ("Requires Sword", 0), ("Requires Materia Blade", 0), ("Evadeable", 0), ("Targeting", 1)],
]
WEAPON_FLAGS = ["Striking", "Lunging", "Direct", "Arc", "Two Swords", "Two Hands", "Throwable", "Force Two Hands"]
ITEM_KIND_FLAGS = ["Weapon", "Shield", "Head", "Body", "Accessory", "", "Rare", ""]
UNIT_FLAGS = ["Male", "Female", "Monster", "Join After Event", "Load Formation", "Zodiac Monster", "", "Save Formation"]
UNIT_AI_FLAGS = ["", "Focus Unit", "Stay Near XY", "Aggressive", "Defensive", "", "", ""]
REQUIREMENT_JOBS = [
    "Chemist", "Knight", "Archer", "Monk", "White Mage", "Black Mage", "Time Mage", "Summoner", "Thief",
    "Orator", "Mystic", "Geomancer", "Dragoon", "Samurai", "Ninja", "Arithmetician", "Bard", "Dancer", "Mime",
]
REQUIREMENT_NIBBLES = ["Squire"] + REQUIREMENT_JOBS


# ---------------------------------------------------------------- field decoders

def u16(b: bytes, i: int) -> int:
    return b[i] | b[i + 1] << 8


def flags(byte: int, names: list[str]) -> tuple[str, ...]:
    return tuple(name for k, name in enumerate(names) if name and byte & (0x80 >> k))


def flag_bytes(data: bytes, names: list[str]) -> tuple[str, ...]:
    return tuple(n for i, byte in enumerate(data) for n in flags(byte, names[i * 8 : i * 8 + 8]))


def statuses(data: bytes) -> tuple[str, ...]:
    return flag_bytes(data, STATUSES)


def elements(byte: int) -> tuple[str, ...]:
    return flags(byte, ELEMENTS)


def ability_flags(data: bytes) -> tuple[str, ...]:
    out = []
    for byte, spec in zip(data, ABILITY_FLAGS):
        for k, (name, inverted) in enumerate(spec):
            if name and bool(byte & (0x80 >> k)) != bool(inverted):
                out.append(name)
    return tuple(out)


def hexbyte(v: int) -> str:
    return f"0x{v:02X}"


# ---------------------------------------------------------------- tables

@dataclass
class Table:
    key: str  # unique id, also the CSV row's table column
    title: str
    group: str  # CSV file under data/remixed/
    file: str  # vanilla disc path
    offset: int
    count: int
    size: int
    decode: Callable[[bytes, Names], Fields] | None = None  # None: compare raw bytes
    label: Callable[[int, Names], str] = field(default=lambda i, n: f"{i:03X}")
    base_id: int = 0

    @property
    def end(self) -> int:
        return self.offset + self.count * self.size


def job(b: bytes, n: Names) -> Fields:
    a = lambda i: n.label("abilities", u16(b, i), 3)  # noqa: E731
    return {
        "Skillset": n.label("skillsets", b[0]),
        "Innate A": a(1), "Innate B": a(3), "Innate C": a(5), "Innate D": a(7),
        "Equipment": flag_bytes(b[9:13], EQUIPMENT),
        "HP growth": b[13], "HP multiplier": b[14], "MP growth": b[15], "MP multiplier": b[16],
        "Speed growth": b[17], "Speed multiplier": b[18], "PA growth": b[19], "PA multiplier": b[20],
        "MA growth": b[21], "MA multiplier": b[22], "Move": b[23], "Jump": b[24], "C-Evade": b[25],
        "Innate statuses": statuses(b[26:31]), "Status immunity": statuses(b[31:36]),
        "Starting statuses": statuses(b[36:41]),
        "Absorb": elements(b[41]), "Cancel": elements(b[42]), "Half": elements(b[43]), "Weak": elements(b[44]),
        "Monster portrait": b[45], "Monster palette": b[46], "Monster graphic": b[47],
    }


def ability_common(b: bytes, n: Names) -> Fields:
    return {
        "JP cost": u16(b, 0),
        "Learn rate": b[2],
        "Learn flags": tuple(name for name, on in (
            ("Learn with JP", not b[3] & 0x80), ("Display name", b[3] & 0x40),
            ("Learn on hit", b[3] & 0x20), ("Blank", b[3] & 0x10)) if on),
        "Type": n.get("ability_types", b[3] & 0x0F) or str(b[3] & 0x0F),
        "AI flags": flag_bytes(b[4:7], AI_FLAGS),
        "Flags": flags(b[7], ["Used by enemies", "Unknown2", "Unknown3", "", "", "", "", "Unknown4"]),
    }


def ability_attributes(b: bytes, n: Names) -> Fields:
    return {
        "Range": b[0], "Effect area": b[1], "Vertical": b[2],
        "Flags": ability_flags(b[3:7]), "Element": elements(b[7]),
        "Formula": hexbyte(b[8]), "X": b[9], "Y": b[10],
        "Inflict status": hexbyte(b[11]), "CT": b[12], "MP cost": b[13],
    }


def skillset(b: bytes, n: Names) -> Fields:
    high = (b[0] << 16) | (b[1] << 8) | b[2]  # 16 action bits then 6 reaction/support/movement bits
    out: Fields = {}
    for i in range(16):
        out[f"Action {i + 1}"] = n.label("abilities", b[3 + i] + (0x100 if high & (0x800000 >> i) else 0), 3)
    for i in range(6):
        out[f"RSM {i + 1}"] = n.label("abilities", b[19 + i] + (0x100 if high & (0x80 >> i) else 0), 3)
    return out


def monster_skills(b: bytes, n: Names) -> Fields:
    names = ["Ability 1", "Ability 2", "Ability 3", "Beastmaster"]
    return {name: n.label("abilities", b[1 + i] + (0x100 if b[0] & (0x80 >> i) else 0), 3)
            for i, name in enumerate(names)}


def item_common(b: bytes, n: Names) -> Fields:
    return {
        "Palette": b[0], "Graphic": b[1], "Enemy level": b[2], "Kind": flags(b[3], ITEM_KIND_FLAGS),
        "Second table id": b[4], "Item type": b[5], "Unknown1": b[6], "Attributes id": hexbyte(b[7]),
        "Price": u16(b, 8), "Shop availability": b[10], "Unknown2": b[11],
    }


def weapon(b: bytes, n: Names) -> Fields:
    return {
        "Range": b[0], "Flags": flags(b[1], WEAPON_FLAGS), "Formula": hexbyte(b[2]), "Unknown": b[3],
        "Power": b[4], "Evade": b[5], "Element": elements(b[6]), "Inflict status/spell": hexbyte(b[7]),
    }


def item_attributes(b: bytes, n: Names) -> Fields:
    return {
        "PA": b[0], "MA": b[1], "Speed": b[2], "Move": b[3], "Jump": b[4],
        "Innate statuses": statuses(b[5:10]), "Status immunity": statuses(b[10:15]),
        "Starting statuses": statuses(b[15:20]),
        "Absorb": elements(b[20]), "Cancel": elements(b[21]), "Half": elements(b[22]),
        "Weak": elements(b[23]), "Strengthen": elements(b[24]),
    }


def inflict_status(b: bytes, n: Names) -> Fields:
    return {
        "Mode": flags(b[0], ["All or nothing", "Random", "Separate", "Cancel", "", "", "", ""]),
        "Statuses": statuses(b[1:6]),
    }


def status_attribute(b: bytes, n: Names) -> Fields:
    return {
        "Unknown0": b[0], "Unknown1": b[1], "Order": b[2], "CT": b[3],
        "Flags": flags(b[4], ["Freeze CT", "U1", "U2", "U3", "U4", "U5", "U6", "Counts as KO"])
        + flags(b[5], ["Can react", "", "Ignore attacks", "Ignored if mount", "U8", "U9",
                       "Cancelled by Immortal", "Lower target priority"]),
        "Cancels": statuses(b[6:11]), "Can't stack on": statuses(b[11:16]),
    }


def job_requirements(b: bytes, n: Names) -> Fields:
    nibbles = [v for byte in b for v in (byte >> 4, byte & 0x0F)]
    return {f"{job} level": v for job, v in zip(REQUIREMENT_NIBBLES, nibbles)}


def jp_levels(b: bytes, n: Names) -> Fields:
    return {f"JP for level {i + 2}": u16(b, i * 2) for i in range(8)}


def poach(b: bytes, n: Names) -> Fields:
    return {"Common": n.label("items", b[0]), "Rare": n.label("items", b[1])}


def entd_unit(b: bytes, n: Names) -> Fields:
    item = lambda v: n.label("items", v)  # noqa: E731
    ability = lambda i: n.label("abilities", u16(b, i), 3)  # noqa: E731
    return {
        "Sprite set": n.label("sprite_sets", b[0]), "Flags": flags(b[1], UNIT_FLAGS),
        "Name": n.label("special_names", b[2]), "Level": b[3], "Month": b[4], "Day": b[5],
        "Brave": b[6], "Faith": b[7], "Prereq job": b[8], "Prereq job level": b[9],
        "Job": n.label("jobs", b[10]), "Secondary": n.label("skillsets", b[11]),
        "Reaction": ability(12), "Support": ability(14), "Movement": ability(16),
        "Head": item(b[18]), "Body": item(b[19]), "Accessory": item(b[20]),
        "Right hand": item(b[21]), "Left hand": item(b[22]), "Palette": b[23],
        "Presence/control": flags(b[24], ["Always present", "Randomly present", "", "", "Control",
                                          "Immortal", "", ""]),
        "Team": (b[24] & 0x30) >> 4, "X": b[25], "Y": b[26], "Facing": b[27] & 0x7F,
        "Upper level": bool(b[27] & 0x80), "Experience": b[28], "Primary": n.label("skillsets", b[29]),
        "War trophy": item(b[30]), "Bonus money": b[31], "Unit id": b[32],
        "Target X": b[33], "Target Y": b[34], "AI": flags(b[35], UNIT_AI_FLAGS), "Target": b[36],
        "Unknown37": b[37], "Save CT": bool(b[38] & 0x04), "Unknown38": b[38] & ~0x04, "Unknown39": b[39],
    }


def ability_effect(b: bytes, n: Names) -> Fields:
    return {"Effect id": f"0x{u16(b, 0):03X}"}


def by_name(kind: str, width: int = 2) -> Callable[[int, Names], str]:
    return lambda i, n: n.label(kind, i, width)


def entd_label(file_index: int) -> Callable[[int, Names], str]:
    def label(i: int, n: Names) -> str:
        event = file_index * 128 + i // 16
        return f"{n.label('events', event, 3)} / unit {i % 16}"
    return label


def one_byte(name: str, fmt: Callable[[int, Names], object] = lambda v, n: v):
    return lambda b, n: {name: fmt(b[0], n)}


ABILITIES = 0x4F3F0
ITEMS = 0x536B8
TABLES: list[Table] = [
    Table("jobs", "Jobs", "jobs", SCUS, 0x518B8, 160, 48, job, by_name("jobs")),
    Table("job_requirements", "Job unlock requirements", "jobs", SCUS, 0x568C4, 19, 10, job_requirements,
          lambda i, n: REQUIREMENT_JOBS[i]),
    Table("jp_levels", "JP per job level", "jobs", SCUS, 0x568C4 + 192, 1, 16, jp_levels, lambda i, n: "All jobs"),
    Table("skillsets", "Skillsets", "skillsets", SCUS, 0x55294, 176, 25, skillset, by_name("skillsets")),
    Table("monster_skills", "Monster skills", "skillsets", SCUS, 0x563C4, 48, 5, monster_skills,
          lambda i, n: n.label("jobs", 0x5E + i)),
    Table("abilities", "Abilities: common", "abilities", SCUS, ABILITIES, 512, 8, ability_common,
          by_name("abilities", 3)),
    Table("ability_attributes", "Abilities: attributes", "abilities", SCUS, ABILITIES + 0x1000, 0x170, 14,
          ability_attributes, by_name("abilities", 3)),
    Table("ability_items", "Abilities: Item", "abilities", SCUS, ABILITIES + 0x2420, 14, 1,
          one_byte("Item", lambda v, n: n.label("items", v)), by_name("abilities", 3), 0x170),
    Table("ability_throw", "Abilities: Throw", "abilities", SCUS, ABILITIES + 0x2430, 12, 1,
          one_byte("Item type", lambda v, n: hexbyte(v)), by_name("abilities", 3), 0x17E),
    Table("ability_jump", "Abilities: Jump", "abilities", SCUS, ABILITIES + 0x243C, 12, 2,
          lambda b, n: {"Horizontal": b[0], "Vertical": b[1]}, by_name("abilities", 3), 0x18A),
    Table("ability_charge", "Abilities: Charge", "abilities", SCUS, ABILITIES + 0x2454, 8, 2,
          lambda b, n: {"CT": b[0], "Power bonus": b[1]}, by_name("abilities", 3), 0x196),
    Table("ability_arithmetick", "Abilities: Arithmetick", "abilities", SCUS, ABILITIES + 0x2464, 8, 1,
          one_byte("Value", lambda v, n: hexbyte(v)), by_name("abilities", 3), 0x19E),
    Table("ability_other", "Abilities: reaction/support/movement", "abilities", SCUS, ABILITIES + 0x246C, 90, 1,
          one_byte("Value", lambda v, n: hexbyte(v)), by_name("abilities", 3), 0x1A6),
    Table("ability_effects", "Abilities: animation effect", "abilities", BATTLE, 0x14F3F0, 0x1C6, 2,
          ability_effect, by_name("abilities", 3)),
    Table("items", "Items: common", "items", SCUS, ITEMS, 0xFE, 12, item_common, by_name("items")),
    Table("item_after_phoenix_down", "Items: block after Phoenix Down", "items", SCUS, ITEMS + 0xBE8, 1, 0x18),
    Table("weapons", "Items: weapons", "items", SCUS, ITEMS + 0xC00, 0x80, 8, weapon, by_name("items")),
    Table("shields", "Items: shields", "items", SCUS, ITEMS + 0x1000, 0x10, 2,
          lambda b, n: {"Physical block": b[0], "Magic block": b[1]}, by_name("items"), 0x80),
    Table("armor", "Items: helmets and armor", "items", SCUS, ITEMS + 0x1020, 0x40, 2,
          lambda b, n: {"HP bonus": b[0], "MP bonus": b[1]}, by_name("items"), 0x90),
    Table("accessories", "Items: accessories", "items", SCUS, ITEMS + 0x10A0, 0x20, 2,
          lambda b, n: {"Physical evade": b[0], "Magic evade": b[1]}, by_name("items"), 0xD0),
    Table("chemist_items", "Items: consumables", "items", SCUS, ITEMS + 0x10E0, 14, 3,
          lambda b, n: {"Formula": hexbyte(b[0]), "X": b[1], "Inflict status": hexbyte(b[2])},
          by_name("items"), 0xF0),
    Table("item_attributes", "Item attributes", "items", SCUS, 0x54AC4, 80, 25, item_attributes),
    Table("poach", "Poach items", "items", SCUS, 0x56864, 48, 2, poach, lambda i, n: n.label("jobs", 0x5E + i)),
    Table("inflict_statuses", "Inflict status sets", "statuses", SCUS, 0x547C4, 128, 6, inflict_status),
    Table("status_attributes", "Status attributes", "statuses", SCUS, 0x565E4, 40, 16, status_attribute,
          lambda i, n: STATUSES[i]),
    *[Table(f"entd{k + 1}", f"Battle units (ENTD{k + 1})", "entd", f"BATTLE/ENTD{k + 1}.ENT", 0, 2048, 40,
            entd_unit, entd_label(k)) for k in range(4)],
    # Known tables compared as raw bytes for now.
    Table("action_events", "Action events", "other", SCUS, 0x564B4, 14, 16),
    Table("ability_animations", "Ability animations", "other", BATTLE, 0x2CE10, 0x1C6, 3,
          label=by_name("abilities", 3)),
    Table("move_find_items", "Move-Find items", "other", BATTLE, 0x8EE74, 0x80, 16),
    Table("store_inventories", "Store inventories", "other", WORLD, 0xAD844, 0x20, 16),
    Table("propositions", "Propositions (errands)", "other", WLDCORE, 0x36380, 0xA7C // 4, 4),
    Table("formation_sprites1", "Formation sprites 1", "other", WORLD, 0xADE34, 1, 0x4A),
    Table("formation_sprites2", "Formation sprites 2", "other", WORLD, 0xAA168, 1, 0x94),
    Table("formation_sprites2a", "Formation sprites 2A", "other", WORLD, 0xAA8B8, 1, 0x94),
    Table("formation_sprites2b", "Formation sprites 2B", "other", WORLD, 0x74B14, 1, 0x94),
    Table("formation_sprites2c", "Formation sprites 2C", "other", "EVENT/ATTACK.OUT", 0x16BB0, 1, 0x94),
    Table("formation_sprites2d", "Formation sprites 2D", "other", "EVENT/REQUIRE.OUT", 0x11B3C, 1, 0x94),
]


# ---------------------------------------------------------------- diffing

def show(value: object) -> str:
    if isinstance(value, tuple):
        return ", ".join(value) if value else "none"
    if isinstance(value, bool):
        return "yes" if value else "no"
    return str(value)


def show_change(old: object, new: object) -> str:
    if isinstance(old, tuple) and isinstance(new, tuple):
        added = [f"+{v}" for v in new if v not in old]
        removed = [f"-{v}" for v in old if v not in new]
        return " ".join(added + removed) or f"{show(old)} → {show(new)}"
    return f"{show(old)} → {show(new)}"


class FilePair:
    """A disc file's bytes in vanilla and ReMixed, matched by sector so renames don't matter."""

    def __init__(self, vanilla: Disc, patched: Disc):
        self.vanilla, self.patched = vanilla, patched
        self._cache: dict[str, tuple[bytes, bytes]] = {}

    def get(self, path: str) -> tuple[bytes, bytes]:
        if path not in self._cache:
            entry = self.vanilla.find(path)
            if entry is None:
                raise KeyError(f"{path} isn't on the vanilla disc")
            twin = self.patched.owner(entry.lba)
            self._cache[path] = (self.vanilla.read(entry), self.patched.read(twin) if twin else b"")
        return self._cache[path]


def diff_table(table: Table, files: FilePair, names: Names) -> list[dict]:
    old_file, new_file = files.get(table.file)
    rows = []
    for i in range(table.count):
        start = table.offset + i * table.size
        old, new = old_file[start : start + table.size], new_file[start : start + table.size]
        if old == new:
            continue
        rid = table.base_id + i
        label = table.label(rid, names)
        changes: list[tuple[str, object, object]] = []
        if table.decode is not None:
            a, b = table.decode(old, names), table.decode(new, names)
            changes = [(k, a[k], b[k]) for k in a if a[k] != b[k]]
        if not changes:
            changes = [("bytes", old.hex(" "), new.hex(" "))]
        for name, a, b in changes:
            rows.append({"table": table.key, "id": f"{rid:03X}", "name": label, "field": name,
                         "vanilla": show(a), "remixed": show(b), "change": show_change(a, b)})
    return rows


def entd_records(table: Table, files: FilePair, names: Names) -> list[dict]:
    """Each changed battle unit as one wide row of its ReMixed values."""
    old_file, new_file = files.get(table.file)
    out = []
    for i in range(table.count):
        start = i * table.size
        old, new = old_file[start : start + table.size], new_file[start : start + table.size]
        if old == new:
            continue
        a, b = table.decode(old, names), table.decode(new, names)
        event = int(table.key[-1]) * 128 - 128 + i // 16
        out.append({
            "event": f"{event:03X}", "event name": names.get("events", event), "unit": i % 16,
            "new unit": "yes" if not any(old) else "no",
            "changed": ", ".join(k for k in a if a[k] != b[k]) or "bytes",
            **{k: show(v) for k, v in b.items()},
        })
    return out


def write_entd_csv(records: list[dict]) -> Path:
    out = DATA_DIR / "entd.csv"
    with open(out, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, list(records[0]))
        writer.writeheader()
        writer.writerows(records)
    return out


def write_csvs(rows: list[dict]) -> list[Path]:
    """Long format, one row per changed field. Battle units are written by write_entd_csv."""
    DATA_DIR.mkdir(parents=True, exist_ok=True)
    groups: dict[str, list[dict]] = {}
    group_of = {t.key: t.group for t in TABLES}
    for row in rows:
        if group_of[row["table"]] != "entd":
            groups.setdefault(group_of[row["table"]], []).append(row)
    written = []
    for group, group_rows in sorted(groups.items()):
        out = DATA_DIR / f"{group}.csv"
        with open(out, "w", newline="", encoding="utf-8") as f:
            writer = csv.DictWriter(f, ["table", "id", "name", "field", "vanilla", "remixed"])
            writer.writeheader()
            writer.writerows({k: r[k] for k in writer.fieldnames} for r in group_rows)
        written.append(out)
    return written


def md(text: str) -> str:
    return text.replace("<", "&lt;").replace(">", "&gt;")


def write_tables_report(name: str, rows: list[dict], units: list[dict]) -> Path:
    by_table: dict[str, list[dict]] = {}
    for row in rows:
        by_table.setdefault(row["table"], []).append(row)

    lines = [
        f"# {name}: data table changes",
        "",
        "Generated by `python -I tools/tables.py`. Don't edit by hand.",
        "Every changed field is in `data/remixed/*.csv`. Battle units are in",
        "`data/remixed/entd.csv`, one row per changed unit with its ReMixed values.",
        "Names are vanilla names from FFTPatcher, so anything ReMixed renamed or",
        "added still shows its old name or a bare id here.",
        "",
        "| Table | Records changed | Fields changed |",
        "|---|--:|--:|",
    ]
    for table in TABLES:
        table_rows = by_table.get(table.key, [])
        records = len({r["id"] for r in table_rows})
        lines.append(f"| {table.title} | {records} | {len(table_rows)} |")

    for table in TABLES:
        table_rows = by_table.get(table.key, [])
        if not table_rows or table.group == "entd":
            continue
        lines += ["", f"## {table.title}", ""]
        records: dict[str, list[dict]] = {}
        for r in table_rows:
            records.setdefault(r["name"], []).append(r)
        for record, changes in records.items():
            parts = "; ".join(f"{c['field']}: {c['change']}" for c in changes)
            lines.append(f"- **{md(record)}**: {md(parts)}")

    if units:
        # ReMixed gives nearly every unit an id of 0x80 + its slot (vanilla: 0xFF).
        # That's for its own code, so it's counted once here instead of per battle.
        id_only = [u for u in units if u["changed"] == "Unit id"]
        real = [u for u in units if u["changed"] != "Unit id"]
        events: dict[str, list[dict]] = {}
        for u in real:
            events.setdefault(f"{u['event']} {u['event name']}".strip(), []).append(u)
        lines += [
            "", "## Battle units by event", "",
            f"{len(units):,} units changed. {len(id_only):,} of them only had their unit id",
            "changed (ReMixed numbers units 128 + slot instead of vanilla's 255, probably",
            f"for its own code). The other {len(real):,} units, in {len(events)} events, are listed below.",
            "",
            "| Event | Units changed | Most-changed fields |", "|---|--:|---|",
        ]
        for event, event_units in events.items():
            counts: dict[str, int] = {}
            for u in event_units:
                for f in u["changed"].split(", "):
                    if f != "Unit id":
                        counts[f] = counts.get(f, 0) + 1
            top = ", ".join(sorted(counts, key=lambda f: -counts[f])[:4])
            lines.append(f"| {md(event)} | {len(event_units)} | {md(top)} |")

    out = REPORT_DIR / f"{slug(name)}-tables.md"
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return out


# ---------------------------------------------------------------- leftovers

def subtract(ranges: list[list[int]], holes: list[tuple[int, int]]) -> list[list[int]]:
    out = []
    for start, end in ranges:
        pieces = [[start, end]]
        for h_start, h_end in holes:
            next_pieces = []
            for s, e in pieces:
                if h_end <= s or e <= h_start:
                    next_pieces.append([s, e])
                    continue
                if s < h_start:
                    next_pieces.append([s, h_start])
                if h_end < e:
                    next_pieces.append([h_end, e])
            pieces = next_pieces
        out += pieces
    return out


def blocks(ranges: list[list[int]], gap: int = 16) -> list[list[int]]:
    """Merge changed ranges closer than `gap` bytes: [start, end, bytes changed]."""
    merged: list[list[int]] = []
    for start, end in sorted(ranges):
        if merged and start - merged[-1][1] <= gap:
            merged[-1][1] = max(merged[-1][1], end)
            merged[-1][2] += end - start
        else:
            merged.append([start, end, end - start])
    return merged


def is_code_file(path: str) -> bool:
    upper = path.upper()
    return upper in (SCUS, BATTLE, WORLD, WLDCORE) or upper.endswith(".OUT")


def write_code_report(name: str, change_map: dict) -> tuple[Path, Path]:
    holes: dict[str, list[tuple[int, int]]] = {}
    for t in TABLES:
        holes.setdefault(t.file.upper(), []).append((t.offset, t.end))

    summary, detail = [], []
    for path, info in change_map["changes"]["files"].items():
        vanilla_path = info.get("was", path)
        if not is_code_file(vanilla_path):
            continue
        left = subtract(info["ranges"], holes.get(vanilla_path.upper(), []))
        found = blocks(left)
        explained = info["changed"] - sum(e - s for s, e in left)
        summary.append((vanilla_path, path, info["changed"], explained, sum(b[2] for b in found), len(found)))
        base = RAM_BASE.get(vanilla_path.upper())
        for start, end, changed in found:
            ram = f"0x{base + start:08X}" if base else ""
            detail.append({"file": vanilla_path, "offset": f"0x{start:X}", "ram": ram,
                           "span": end - start, "bytes_changed": changed})

    csv_out = DATA_DIR / "code_blocks.csv"
    with open(csv_out, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, ["file", "offset", "ram", "span", "bytes_changed"])
        writer.writeheader()
        writer.writerows(detail)

    summary.sort(key=lambda s: -s[4])
    lines = [
        f"# {name}: changes outside known tables",
        "",
        "Generated by `python -I tools/tables.py`. Don't edit by hand.",
        "",
        "Changed bytes in the code files that no known data table explains. These are",
        "ReMixed's ASM hacks, plus text and any tables FFTPatcher doesn't cover.",
        "Changes within 16 bytes of each other are merged into one block.",
        f"Every block is in `data/remixed/{csv_out.name}`, with PS1 RAM addresses",
        "where the file's load address is known.",
        "",
        "| File | Bytes changed | In known tables | Left over | Blocks |",
        "|---|--:|--:|--:|--:|",
    ]
    for vanilla_path, path, changed, explained, left, count in summary:
        label = f"`{vanilla_path}`" + (f" (ReMixed: `{path}`)" if path != vanilla_path else "")
        lines.append(f"| {label} | {changed:,} | {explained:,} | {left:,} | {count:,} |")

    outside = change_map["changes"]["outside"]
    if outside:
        lines += ["", "Also changed, outside any file:", ""]
        lines += [f"- sectors {a}-{b}: {n:,} bytes" for a, b, n in outside]

    for vanilla_path in (SCUS, BATTLE):
        rows = [d for d in detail if d["file"].upper() == vanilla_path]
        if not rows:
            continue
        rows.sort(key=lambda d: -d["bytes_changed"])
        lines += ["", f"## Largest blocks in `{vanilla_path}`", "",
                  "| Offset | RAM | Span | Bytes changed |", "|---|---|--:|--:|"]
        lines += [f"| {d['offset']} | {d['ram']} | {d['span']:,} | {d['bytes_changed']:,} |" for d in rows[:40]]

    md_out = REPORT_DIR / f"{slug(name)}-code-changes.md"
    md_out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return md_out, csv_out


def main() -> None:
    cfg = load_config()
    name = ppf_path(cfg).stem
    change_map_path = WORK / "reports" / f"{slug(name)}-disc-changes.json"
    patched_path = remixed_bin(cfg)
    if not patched_path.exists() or not change_map_path.exists():
        sys.exit("Run `python -I tools/remixed.py prepare` and `python -I tools/remixed.py diff` first.")

    names = Names(WORK / "cache" / "fftpatcher")
    with Disc(vanilla_bin(cfg)) as vanilla, Disc(patched_path) as patched:
        files = FilePair(vanilla, patched)
        rows = [row for table in TABLES for row in diff_table(table, files, names)]
        units = [u for table in TABLES if table.group == "entd" for u in entd_records(table, files, names)]

    for out in write_csvs(rows):
        print(f"Wrote {out.relative_to(ROOT)}")
    if units:
        print(f"Wrote {write_entd_csv(units).relative_to(ROOT)}")
    print(f"Wrote {write_tables_report(name, rows, units).relative_to(ROOT)}")
    change_map = json.loads(change_map_path.read_text(encoding="utf-8"))
    for out in write_code_report(name, change_map):
        print(f"Wrote {out.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
