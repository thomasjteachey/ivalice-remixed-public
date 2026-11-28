"""Build the Ivalice ReMixed mod: ReMixed's table changes as FFT mod loader files.

    python -I tools/build_mod.py [--only enhanced|classic] [--install] [--no-code|--code-only|--data-only]

Reads each table from the vanilla and ReMixed discs and writes only the fields
ReMixed changed, in the mod loader's XML format, to mod/<MOD_ID>/ (enhanced by
default). Also checks the PS1-to-loader conversion against the loader's own
templates (which hold the remaster's defaults) and writes a report to
docs/generated/. With the .NET SDK installed it also compiles the mod's C# part
(code/IvaliceRemixed), which goes into the mod once a patch needs it.

Needs `remixed.py prepare` first, and `tools.modloader` in local.toml.
"""

from __future__ import annotations

import argparse
import collections
import difflib
import json
import re
import shutil
import sqlite3
import struct
import subprocess
import sys
import textwrap
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable
from xml.sax.saxutils import escape

sys.path.insert(0, str(Path(__file__).resolve().parent))

import fft_text  # noqa: E402
import remixed_help  # noqa: E402
import sprites  # noqa: E402
import title_plaque  # noqa: E402
from names import Names  # noqa: E402
from psxdisc import Disc  # noqa: E402
from remixed import REPORT_DIR, ROOT, WORK, load_config, ppf_path, remixed_bin, vanilla_bin  # noqa: E402
from tables import FilePair, u16  # noqa: E402

# The Event Instruction Upgrade's event tools (the code/eiu submodule, eiu-ivalice-chronicles): PS1 events in the
# remaster's layout. ReMixed's own steps (its boxes, pages, text and patches) stay here.
if not (ROOT / "code" / "eiu" / "tools" / "eiu_events").is_dir():
    sys.exit("code/eiu is empty: run `git submodule update --init` (the Event Instruction Upgrade's repo).")
sys.path.insert(0, str(ROOT / "code" / "eiu" / "tools"))
import eiu_events  # noqa: E402

MOD_ID = "fftivc.gameplay.ivaliceremixed"
MOD_DIR = ROOT / "mod" / MOD_ID
# The Event Instruction Upgrade: Xifanie's PS1 event hack, which ReMixed's events are written for, as its own mod
# (the eiu-ivalice-chronicles repo, the code/eiu submodule); ReMixed depends on it and the build writes both.
EIU_REPO = ROOT / "code" / "eiu"
EIU_ID = "fftivc.utility.eventinstructionupgrade"
EIU_DIR = ROOT / "mod" / EIU_ID
SCUS = "SCUS_942.21"

Fields = dict[str, object]  # field name -> int, str, or set of flag names

# ---------------------------------------------------------------- loader names
# From the mod loader's Interfaces/Tables (Structures and Models). Flag lists
# run from the highest bit (0x80 of the first byte) down, like the PS1 data.

STATUS_NAMES = [
    "Unused1", "Crystal", "KO", "Undead", "Charging", "Jump", "Defending", "Performing",
    "Stone", "Traitor", "Blind", "Confuse", "Silence", "Vampire", "Unused2", "Chest",
    "Oil", "Float", "Reraise", "Invisible", "Berserk", "Chicken", "Toad", "Critical",
    "Poison", "Regen", "Protect", "Shell", "Haste", "Slow", "Stop", "Wall",
    "Faith", "Atheist", "Charm", "Sleep", "Immobilize", "Disable", "Reflect", "Doom",
]
STATUS_EFFECT_TYPES = [  # StatusEffectData lists; enum value = index + 1
    "None", "Crystal", "KO", "Undead", "Readying", "Jumping", "Evading", "Performing",
    "Stone", "Traitor", "Blindness", "Confusion", "Silence", "Vampire", "Empty_15", "Chest",
    "Oil", "Float", "Reraise", "Invisibility", "Berserk", "Chicken", "Toad", "Critical",
    "Poison", "Regen", "Protect", "Shell", "Haste", "Slow", "Stop", "Empty_32",
    "Faith", "Atheist", "Charmed", "Sleep", "Immobilize", "Disable", "Reflect", "Doom",
]
ELEMENTS = ["Fire", "Lightning", "Ice", "Wind", "Earth", "Water", "Holy", "Dark"]
EQUIPMENT = [
    "Unarmed", "Knife", "NinjaBlade", "Sword", "KnightSword", "Katana", "Axe", "Rod",
    "Staff", "Flail", "Gun", "Crossbow", "Bow", "Instrument", "Book", "Polearm",
    "Pole", "Bag", "Cloth", "Shield", "Helmet", "Hat", "HairAdornment", "Armor",
    "Clothing", "Robe", "Shoes", "Armguard", "Ring", "Armlet", "Cloak", "Perfume",
]
EQUIPMENT_REMASTER_ONLY = {"FellSword", "LipRouge"}  # fifth byte, not on the PS1
ITEM_TYPE_FLAGS = ["Weapon", "Shield", "Headgear", "Armor", "Accessory", "Unused_2", "Rare", "ImmuneToStealBreak"]
WEAPON_ATTACK_FLAGS = ["Striking", "Lunging", "Direct", "Arc", "TwoSwords", "TwoHands", "Throwable", "ForcedTwoHands"]
OPTION_TYPES = ["AllOrNothing", "Random", "Separate", "Cancel"]  # high nibble only
ABILITY_FLAGS = ["DontLearnWithJP", "DisplayAbilityName", "LearnOnHit", "Unk_0"]  # high nibble of byte 3
ABILITY_TYPES = ["None", "Normal", "Item", "Throwing", "Jumping", "Aim", "Math", "Reaction", "Support", "Movement"]
AI_FLAGS = [  # bit 0 upwards
    "TargetAllies", "TargetEnemies", "Unequip", "Stats", "AddStatus", "CancelStatus", "MP", "HP",
    "Silence", "Evadeable", "AffectedByFaith", "RandomHits", "CheckCT_Target", "UndeadReverse", "Reflectable", "TargetMap",
    "PhysicalAttack", "MagicalAttack", "Ranged3Directions", "Melee3Directions", "NonSpearAttack", "LinearAttack",
    "StopAtObstacle", "Arced", "EvadeWithMotion", "Unk_25", "UseWeaponRange", "RequireMonsterSkill", "Unk_28",
    "OnlyHitsEnemies", "OnlyHitsAlliesOrSelf", "UsableByAI",
]
STATUS_CHECK_FLAGS = [  # bit 0 upwards
    "KO", "Check1", "Check2", "ConfusionTransparentCharmSleep", "PoisonRegen", "DefendPerform", "CrystalTreasure",
    "FreezeCT", "Check8", "ImmortalCancels", "Check10", "Check11", "Check12", "IgnoreAttacks", "Unk14", "CantReact",
]
ITEM_CATEGORIES = [
    "None", "Knife", "NinjaBlade", "Sword", "KnightSword", "Katana", "Axe", "Rod", "Staff", "Flail", "Gun",
    "Crossbow", "Bow", "Instrument", "Book", "Polearm", "Pole", "Bag", "Cloth", "Shield", "Helmet", "Hat",
    "HairAdornment", "Armor", "Clothing", "Robe", "Shoes", "Armguard", "Ring", "Armlet", "Cloak", "Perfume",
    "Throwing", "Bomb", "Item",
]
SHOP_AVAILABILITY = [
    "Blank", "Chapter1_Start", "Chapter1_EnterIgros", "Chapter1_SaveElmdor", "Chapter1_KillMiluda",
    "Chapter2_Start", "Chapter2_SaveOvelia", "Chapter2_MeetDraclau", "Chapter2_SaveAgrias",
    "Chapter3_Start", "Chapter3_Zalmo", "Chapter3_MeetVelius", "Chapter3_SaveRafa",
    "Chapter4_Start", "Chapter4_Bethla", "Chapter4_KillElmdor", "Chapter4_KillZalbag",
    "Unknown17", "Unknown18", "Unknown19", "Unknown20",
]
REQUIREMENT_FIELDS = [
    "Squire", "Chemist", "Knight", "Archer", "Monk", "WhiteMage", "BlackMage", "TimeMage", "Summoner", "Thief",
    "Orator", "Mystic", "Geomancer", "Dragoon", "Samurai", "Ninja", "Arithmetician", "Bard", "Dancer", "Mime",
]


def msb_flags(data: bytes, names: list[str]) -> frozenset[str]:
    """Bits read from the highest bit of the first byte down."""
    return frozenset(n for i, byte in enumerate(data) for k, n in enumerate(names[i * 8 : i * 8 + 8])
                     if byte & (0x80 >> k))


def lsb_flags(value: int, names: list[str]) -> frozenset[str]:
    return frozenset(n for k, n in enumerate(names) if value >> k & 1)


def enum(value: int, names: list[str]) -> str:
    return names[value] if value < len(names) else str(value)


# ---------------------------------------------------------------- tables

@dataclass
class TableSpec:
    xml: str  # template and output name, e.g. "JobData"
    offset: int  # in SCUS_942.21
    count: int
    size: int
    decode: Callable[[bytes], Fields]
    first_id: int = 0  # remaster Id of the first PS1 record
    name: Callable[[int, Names], str] = field(default=lambda i, n: "")
    skip: Callable[[int, str, object], str | None] = field(default=lambda i, f, v: None)  # why to leave a field out
    whole_row: bool = False  # write every field of a changed entry, not just the changed ones
    blocked: str = ""  # why the loader can't take this table yet; it's computed but not written
    source: str = SCUS  # disc file the PS1 table lives in


def job(b: bytes) -> Fields:
    out: Fields = {"JobCommandId": b[0]}
    for k in range(4):
        out[f"InnateAbilityId{k + 1}"] = u16(b, 1 + 2 * k)
    out["EquippableItems"] = msb_flags(b[9:13], EQUIPMENT)
    stats = ["HPGrowth", "HPMultiplier", "MPGrowth", "MPMultiplier", "SpeedGrowth", "SpeedMultiplier",
             "PAGrowth", "PAMultiplier", "MAGrowth", "MAMultiplier", "Move", "Jump", "CharacterEvasion"]
    out.update({s: b[13 + k] for k, s in enumerate(stats)})
    out["InnateStatus"] = msb_flags(b[26:31], STATUS_NAMES)
    out["ImmuneStatus"] = msb_flags(b[31:36], STATUS_NAMES)
    out["StartingStatus"] = msb_flags(b[36:41], STATUS_NAMES)
    for k, name in enumerate(["AbsorbElements", "NullifyElements", "HalveElements", "WeakElements"]):
        out[name] = msb_flags(b[41 + k : 42 + k], ELEMENTS)
    out.update({"MonsterPortrait": b[45], "MonsterPalette": b[46], "MonsterGraphic": b[47]})
    return out


def skip_job(i: int, f: str, v: object) -> str | None:
    if f.startswith("Monster") and i < 0x5E:
        return ("ReMixed's per-job portrait (a WLDFACE number for its world menus); the remaster draws a human unit's "
                "face by its graphic, from its sprite set and job")
    return None


def job_need_level(b: bytes) -> Fields:
    nibbles = [v for byte in b for v in (byte >> 4, byte & 0x0F)]
    return dict(zip(REQUIREMENT_FIELDS, nibbles))


# Bit 0 upwards. Ability 1 is 0x80 of the first PS1 byte, ability 9 is 0x80 of the second.
EXTENDED_ABILITY_BITS = [f"ExtendedAbility{k}" for k in range(8, 0, -1)] + \
                        [f"ExtendedAbility{k}" for k in range(16, 8, -1)]
EXTENDED_RSM_BITS = ["Unused8", "Unused7"] + [f"ExtendRSMId{k}" for k in range(6, 0, -1)]


def job_command(b: bytes) -> Fields:
    high = (b[0] << 16) | (b[1] << 8) | b[2]
    # The loader's id setters update these flag fields, and fail if they're
    # missing, so they're always written.
    out: Fields = {
        "ExtendAbilityIdFlagBits": lsb_flags(b[0] | b[1] << 8, EXTENDED_ABILITY_BITS),
        "ExtendReactionSupportMovementIdFlagBits": lsb_flags(b[2], EXTENDED_RSM_BITS),
    }
    for i in range(16):
        out[f"AbilityId{i + 1}"] = b[3 + i] + (0x100 if high & (0x800000 >> i) else 0)
    for i in range(6):
        out[f"ReactionSupportMovementId{i + 1}"] = b[19 + i] + (0x100 if high & (0x80 >> i) else 0)
    return out


def monster_job_command(b: bytes) -> Fields:
    return {f"AbilityId{i + 1}": b[1 + i] + (0x100 if b[0] & (0x80 >> i) else 0) for i in range(4)}


def ability(b: bytes) -> Fields:
    return {
        "ChanceToLearn": b[2],
        "Flags": msb_flags(bytes([b[3] & 0xF0]), ABILITY_FLAGS),
        "AbilityType": enum(b[3] & 0x0F, ABILITY_TYPES),
        "AIBehaviorFlags": lsb_flags(int.from_bytes(b[4:8], "little"), AI_FLAGS),
    }


def item(b: bytes) -> Fields:
    return {
        "Palette": b[0], "SpriteID": b[1], "RequiredLevel": b[2],
        "TypeFlags": msb_flags(b[3:4], ITEM_TYPE_FLAGS), "AdditionalDataId": b[4],
        "ItemCategory": enum(b[5], ITEM_CATEGORIES), "Unused_0x06": b[6], "EquipBonusId": b[7],
        "Price": u16(b, 8), "ShopAvailability": enum(b[10], SHOP_AVAILABILITY), "Unused_0x0B": b[11],
    }


def weapon(b: bytes) -> Fields:
    return {
        "Range": b[0], "AttackFlags": msb_flags(b[1:2], WEAPON_ATTACK_FLAGS), "Formula": b[2],
        "Unused_0x03": b[3], "Power": b[4], "Evasion": b[5], "Elements": msb_flags(b[6:7], ELEMENTS),
        "OptionsAbilityId": b[7],
    }


def equip_bonus(b: bytes) -> Fields:
    out: Fields = {"PABonus": b[0], "MABonus": b[1], "SpeedBonus": b[2], "MoveBonus": b[3], "JumpBonus": b[4],
                   "InnateStatus": msb_flags(b[5:10], STATUS_NAMES),
                   "ImmuneStatus": msb_flags(b[10:15], STATUS_NAMES),
                   "StartingStatus": msb_flags(b[15:20], STATUS_NAMES)}
    for k, name in enumerate(["AbsorbElements", "NullifyElements", "HalveElements", "WeakElements", "StrongElements"]):
        out[name] = msb_flags(b[20 + k : 21 + k], ELEMENTS)
    return out


def item_options(b: bytes) -> Fields:
    # The low nibble holds ReMixed's status chance tiers, which need its code. Left out here.
    return {"OptionType": msb_flags(bytes([b[0] & 0xF0]), OPTION_TYPES),
            "Effects": msb_flags(b[1:6], STATUS_NAMES)}


TRAP_FLAGS = ["NoActivation", "Unused1", "AlwaysTrap", "DisableTrap", "SteelNeedle", "SleepingGas", "Deathtrap",
              "Degenerator"]


def map_trap_formation(b: bytes) -> Fields:
    """Move-Find items: 4 panels per map. The PS1 packs X/Y in one byte and uses 1-byte item ids."""
    out: Fields = {}
    for k in range(4):
        xy, flags, rare, common = b[4 * k : 4 * k + 4]
        out.update({f"X{k + 1}": xy >> 4, f"Y{k + 1}": xy & 0x0F,
                    f"TrapFlags{k + 1}": msb_flags(bytes([flags]), TRAP_FLAGS),
                    f"RareItemId{k + 1}": rare, f"CommonItemId{k + 1}": common})
    return out


def status_effect(b: bytes) -> Fields:
    return {
        "Unused_0x00": b[0], "Unused_0x01": b[1], "Order": b[2], "Counter": b[3],
        "CheckFlags": lsb_flags(b[4] | b[5] << 8, STATUS_CHECK_FLAGS),
        "CancelFlags": msb_flags(b[6:11], STATUS_EFFECT_TYPES),
        "NoStackFlags": msb_flags(b[11:16], STATUS_EFFECT_TYPES),
    }


ABILITIES = 0x4F3F0
ITEMS = 0x536B8
EFFECT_TABLE = 0x14F3F0  # BATTLE.BIN: visual effect id (an fftpack/effect file) per ability
NO_EFFECT = -1  # 0xFFFF; the loader's EffectId is signed


def skip_retired_effect(i: int, f: str, v: object) -> str | None:
    if v == NO_EFFECT:
        return "ReMixed gives it no effect (it retired the ability), so it keeps its current one"
    return None


# Mod loader 1.7.5 can't patch tables whose ids don't start at 0. With real ids
# (394+, 406+) it treats them as row numbers past the end and skips them. With
# 0-based ids it crashes the game at launch, because it looks rows up again by
# the real id. ReMixed's values in both tables are also inputs to its own code
# (its Jump rework and gun skills); the only plain data, Charge +1 and +2's
# faster charge, the remaster already has.
LOADER_ID_BUG = ("ReMixed reuses these values for its own code (its Jump rework and gun skills), and "
                 "mod loader 1.7.5 can't patch tables whose ids don't start at 0")
SPECS = [
    TableSpec("JobData", 0x518B8, 160, 48, job, name=lambda i, n: n.get("jobs", i), skip=skip_job),
    # Whole rows: the remaster raised several requirements, and ReMixed's rows
    # should replace them outright rather than mix with them.
    TableSpec("JobNeedLevelData", 0x568C4, 19, 10, job_need_level, whole_row=True),
    TableSpec("JobCommandData", 0x55294, 176, 25, job_command, name=lambda i, n: n.get("skillsets", i),
              whole_row=True),
    TableSpec("MonsterJobCommandData", 0x563C4, 48, 5, monster_job_command, first_id=176,
              name=lambda i, n: n.get("jobs", i - 176 + 0x5E)),
    TableSpec("AbilityData", ABILITIES, 512, 8, ability, name=lambda i, n: n.get("abilities", i)),
    TableSpec("AbilityJumpData", ABILITIES + 0x243C, 12, 2, lambda b: {"Range": b[0], "Vertical": b[1]},
              first_id=0x18A, name=lambda i, n: n.get("abilities", i), blocked=LOADER_ID_BUG),
    TableSpec("AbilityChargeAimData", ABILITIES + 0x2454, 8, 2, lambda b: {"Ticks": b[0], "Power": b[1]},
              first_id=0x196, name=lambda i, n: n.get("abilities", i), blocked=LOADER_ID_BUG),
    TableSpec("ItemData", ITEMS, 0xFE, 12, item, name=lambda i, n: n.get("items", i)),
    TableSpec("ItemWeaponData", ITEMS + 0xC00, 0x80, 8, weapon, name=lambda i, n: n.get("items", i)),
    TableSpec("ItemShieldData", ITEMS + 0x1000, 0x10, 2,
              lambda b: {"PhysicalEvasion": b[0], "MagicalEvasion": b[1]}),
    TableSpec("ItemArmorData", ITEMS + 0x1020, 0x40, 2, lambda b: {"HPBonus": b[0], "MPBonus": b[1]}),
    TableSpec("ItemAccessoryData", ITEMS + 0x10A0, 0x20, 2,
              lambda b: {"PhysicalEvasion": b[0], "MagicalEvasion": b[1]}),
    TableSpec("ItemEquipBonusData", 0x54AC4, 80, 25, equip_bonus),
    TableSpec("ItemOptionsData", 0x547C4, 128, 6, item_options),
    TableSpec("StatusEffectData", 0x565E4, 40, 16, status_effect, name=lambda i, n: STATUS_EFFECT_TYPES[i]),
    TableSpec("MapTrapFormationData", 0x8EE74, 128, 16, map_trap_formation, source="BATTLE.BIN"),
    # Charge effect, user animation and battle text per ability, rebuilt from
    # ReMixed's per-actor animation scheme (its default for each ability).
    TableSpec("AbilityTypeData", 0, 0x1C6, 3,
              lambda b: {"ChargeEffectType": b[0], "AnimationId": b[1], "BattleTextId": b[2]},
              name=lambda i, n: n.get("abilities", i), source="ability types"),
    # Visual effect per ability. Slots the remaster uses for its own abilities
    # (Chant) are left alone like every ability table (remaster_owned_abilities).
    TableSpec("AbilityEffectNumberFilterData", EFFECT_TABLE, 0x1C6, 2,
              lambda b: {"EffectId": int.from_bytes(b[:2], "little", signed=True)},
              name=lambda i, n: n.get("abilities", i), skip=skip_retired_effect, source="BATTLE.BIN"),
]
# Renamed by ReMixed, but the new name describes behaviour in its code
# (Defense/Warding Aura), so the remaster keeps the vanilla name for now.
RENAMES_NEED_CODE = {0x1D2, 0x1D4}
# Names the port sets itself. Arc Witch keeps its PS1 name, so the ordinary rename comparison does not write it,
# but the remaster's job 0x21 has no name. That blank also reaches its status card and job-level message.
EXTRA_RENAMES = {"jobs": {0x21: "Arc Witch"}, "skillsets": {0xE0: "Darkness"}}
# Ability slots the remaster left unused (no icon, no name, or a placeholder such as "A508" or "UNUSED") that a
# skillset this build writes hands out. A name alone doesn't make the party menu show one (the Thief's Pass showed as
# a blank line), so each gets ReMixed's name, an icon (from the skillset's other abilities, or its kind's) and a
# description: one of these where the port knows the effect, otherwise PLACEHOLDER_DESCRIPTION.
PLACEHOLDER_DESCRIPTIONS = {
    0x1FC: "Increases movement by 1, and allows the unit to move through tiles occupied by enemies.",  # Pass
    0x1E3: "Abilities that normally require time to charge take effect immediately.",  # Non-charge
    0x1E4: "Increases the damage the unit deals by 20 percent.",  # Adeptness
    0x1E5: "On level up, HP, MP, Speed, PA and MA grow by the better of the unit's job and its secondary skillset's "
           "job.",  # Two Jobs
}
PLACEHOLDER_DESCRIPTION = "A ReMixed ability. Its description hasn't been ported yet."
PLACEHOLDER_TEXT = re.compile(r"^(A\d+|UNUSED|DESCRIPTION UNUSED|MARKED FOR DELETION.*)?$")
RSM_ICONS = ((0x1A6, 0x1C5, 50), (0x1C6, 0x1E5, 42), (0x1E6, 0x1FF, 36))  # reaction, support, movement

# ReMixed fits a Dark Knight into the PS1's twenty job slots by sharing them by gender: the Bard slot (0x5B, job
# index 17) is a Dark Knight for both (a woman's stored as job 0x37, skillset 0x4E), and a man's Dancer slot is a
# Bard (job 0x38, skillset 0x4D, the songs). The remaster has a Dark Knight job of its own (0xA0: skillset 224,
# unlock row 19, job index 0x14) and keeps Bard for men and Dancer for women, so the enhanced build moves ReMixed's
# rows there instead of porting the slot sharing: its Dark Knight (job 0x5B's row, Sing's abilities 0x16 and the
# Bard slot's unlock row 16) to the remaster's Dark Knight; its man's Bard (job 0x38, skillset 0x4D) to the Bard
# (job 0x5B, Sing); and nothing to the remaster's story jobs 0x37 and 0x38 or skillsets 0x4D and 0x4E.
DARK_KNIGHT = {"job": (0x5B, 0xA0), "skillset": (0x16, 0xE0), "requirement": (16, 19)}
BARD = {"job": (0x38, 0x5B), "skillset": (0x4D, 0x16)}
DARK_KNIGHT_UNUSED = {"JobData": {0x37, 0x38}, "JobCommandData": {0x4D, 0x4E}}
# Job slots ReMixed repurposes that the remaster's story still uses as vanilla's: ReMixed made job 0x22 (vanilla's
# Engineer, Mustadio's) a knight's for side chapter 1-A's young Agrias (Basic Skill, swords, shield, armour; with sprite
# set 0x22) and moved its own Mustadio; the remaster's Mustadio joins as job 0x22, so its row stays the remaster's (seen
# 2026-11-06: 1-A's Agrias drawn as Mustadio). REMIXED_SPRITE_SETS gives 1-A's Agrias the remaster's own.
# Not job 0x12: ReMixed made vanilla's (Marach lying dead on the Rooftop of Riovanes: innate Dead, immune to nearly
# every other status, no growths) Luso's (LUSO_SLOT), and gave its rooftop Malak his own slot 0x1A (sprite set, name,
# job) with its unit preset 1, a corpse (near enough the same statuses). The remaster's rooftop (entry 0x1B1) takes
# that unit's build, job 0x1A, from ReMixed's (merge_entd: 0x1B1 is its 0x151), the preset through UNIT_PRESETS_FILE
# and sprite set 0x1A (REMASTER_MOVED_SPRITE_SETS), so nothing the remaster plays uses job 0x12 but Luso, and the row
# is ReMixed's (decided 2026-11-12, closest to ReMixed).
REMASTER_KEPT_JOBS = {0x22: "ReMixed's job 0x22 is side chapter 1-A's young Agrias; the remaster's Mustadio uses it"}
DARK_KNIGHT_REASON = ("ReMixed's gendered Bard/Dancer slots: the remaster has its own Dark Knight job (0xA0), "
                      "so ReMixed's Dark Knight and Bard go to the remaster's Dark Knight and Bard")
DARK_KNIGHT_NAMES = {"jobs": {0x37, 0x38, 0x5B}, "skillsets": {0x16, 0x4D, 0x4E}}

# ReMixed's descriptions, from its menus' help (EVENT/HELP.LZW, read by tools/remixed_help.py), for the abilities, jobs
# and skillsets whose text ReMixed changed (compared with vanilla's as text, so its re-encoding and quote marks don't
# count; 179 abilities, among them every renamed reaction, support and movement, and the jobs it gave innate
# abilities, "Innate: Banditry"), and for entries the remaster describes with a placeholder ("DESCRIPTION UNUSED").
# The remaster writes a description as one paragraph (none of its 512 has a line break), so ReMixed's lines are
# joined, a list after "Innate:" with commas. An action ability's stat lines (from "Range:" on: range, area, MP and
# speed, which the remaster's own panel shows from the ported tables) are left out but for its weapon requirement
# ("Req:", a ReMixed rule the panel can't show), and vanilla's names in it become the remaster's (description_names).
HELP_LISTS = {"abilities": remixed_help.ABILITIES, "jobs": remixed_help.JOBS, "skillsets": remixed_help.SKILLSETS}
ACTION_ABILITIES = range(0x1A6)
STAT_LINE = re.compile(r"^\s*(Range|Effect|MP|SP|CT|Req|Calc)\b")
REQUIREMENT = re.compile(r"\bReq\s*:\s*(.+)")
# Where ReMixed's help entries go in the remaster's tables when not at their own number (DARK_KNIGHT, BARD), and the
# remaster's entries that keep their own text whatever ReMixed's says (its story jobs and skillsets there, Mustadio).
HELP_MOVED = {"jobs": {DARK_KNIGHT["job"][0]: DARK_KNIGHT["job"][1], BARD["job"][0]: BARD["job"][1]},
              "skillsets": {DARK_KNIGHT["skillset"][0]: DARK_KNIGHT["skillset"][1],
                            BARD["skillset"][0]: BARD["skillset"][1]}}
HELP_KEPT = {"jobs": {0x37, 0x38} | set(REMASTER_KEPT_JOBS), "skillsets": {0x4D, 0x4E}}


def flowed(lines: list[str]) -> str:
    """Help lines as one paragraph: joined with spaces (a word the PS1's lines broke with a hyphen, "run-" "ning",
    joined back). ReMixed separates a line's items with two spaces: from an "Innate:" line to the end (a monster's
    "Innate: Counter, Float  Cannot enter water", then its elements) they become commas, as do the line breaks;
    elsewhere a capitalised item after them starts a sentence ("Add: Defend  Trigger: HP Damage")."""
    out, listing = "", False
    for line in lines:
        in_list = listing or line.strip().startswith("Innate:")
        parts = [" ".join(part.split()) for part in re.split(r" {2,}", line.strip())]
        line = parts[0]
        for part in parts[1:]:
            if in_list:
                line += ", " + part
            elif part[:1].isupper() and line[-1:].isalnum():
                line += ". " + part
            else:
                line += " " + part
        if not line:
            continue
        if not out:
            out = line
        elif out.endswith("-") and line[0].islower():
            out = out[:-1] + line
        else:
            out += (", " if listing and not out.endswith(":") else " ") + line
        listing = in_list
    return out


def help_description(entry: str, action: bool) -> tuple[str, str]:
    """A help entry as (its description, its weapon requirement: "" if none); an action ability's stat lines cut."""
    lines = entry.split("\n")
    if not action:
        return flowed(lines), ""
    cut = next((k for k, line in enumerate(lines) if STAT_LINE.match(line)), len(lines))
    needs = next((m.group(1).strip() for line in lines[cut:] for m in [REQUIREMENT.search(line)] if m), "")
    return flowed(lines[:cut]), " ".join(needs.split())


def same_text(a: str, b: str) -> bool:
    return " ".join(a.replace('"', "'").split()).casefold() == " ".join(b.replace('"', "'").split()).casefold()


def remixed_descriptions(help_old: bytes, help_new: bytes, two_letters: dict[int, tuple[int, int]]
                         ) -> tuple[dict[str, dict[int, str]], dict[str, dict[int, str]]]:
    """ReMixed's descriptions by list and number: (the ones it changed, every one it has), its names as it writes them."""
    changed: dict[str, dict[int, str]] = {}
    every: dict[str, dict[int, str]] = {}
    for name, section in HELP_LISTS.items():
        old, new = remixed_help.entries(help_old, section), remixed_help.entries(help_new, section)
        changed[name], every[name] = {}, {}
        for k, (a, b) in enumerate(zip(old, new)):
            action = name == "abilities" and k in ACTION_ABILITIES
            was, was_needs = help_description(remixed_help.text(a), action)
            text, needs = help_description(remixed_help.text(b, two_letters), action)
            if not re.search("[A-Za-z]", text) or "<" in text:
                continue  # empty or "..." (0x087, 0x088: abilities ReMixed retired), or a byte the reader doesn't know
            described = text + (f" Requires: {needs}." if needs else "")
            every[name][k] = described
            if not same_text(was, text) or not same_text(was_needs, needs):
                changed[name][k] = described
    return changed, every


def description_names(lists: list[tuple[dict[int, str], dict[int, str], dict[int, str]]]) -> list[tuple[str, str]]:
    """(vanilla's name, the remaster's) for ReMixed's descriptions, longest first: "Gil Taking" is "Steal Gil" in the
    remaster, "Talk Skill" "Speechcraft", "Mediator" "Orator". From each list's (vanilla's names, the remaster's,
    ReMixed's renames), the entries ReMixed didn't rename. A name stays as it is where the remaster has more than one
    for it ("Squire": Ramza's job and the generic one), where it's a status too ("Death Sentence", "Innocent" in a list
    of statuses), an everyday word (Counter, Spell, Paralyze) or a weapon the Ninja throws ("Knight Sword" in a
    weapon requirement isn't "Throw Knight's Swords"), and in KEPT_NAMES."""
    found: dict[str, set[str]] = {}
    for old, theirs, renamed in lists:
        for k, name in old.items():
            mine = theirs.get(k)
            if k in renamed or not mine or PLACEHOLDER_TEXT.match(mine):
                continue
            found.setdefault(name, set()).add(mine)
    pairs = []
    for name, mine in found.items():
        if len(mine) != 1 or name in mine or name in STATUS_WORDS or name in KEPT_NAMES:
            continue
        if next(iter(mine)).startswith("Throw ") and not name.startswith("Throw"):
            continue
        if " " not in name and "-" not in name and name.lower() in EVERYDAY_WORDS or NUMBER_NAME.match(name):
            continue
        pairs.append((name, next(iter(mine))))
    return sorted(pairs, key=lambda p: -len(p[0]))


# Vanilla's status names: ReMixed's descriptions list statuses by them ("Cancel: ... Don't Act, Death Sentence"), and
# some are abilities' names too (Death Sentence, Innocent, Petrify).
STATUS_WORDS = {"Dead", "Undead", "Petrify", "Invite", "Darkness", "Confusion", "Silence", "Blood Suck", "Cursed",
                "Treasure", "Oil", "Float", "Reraise", "Transparent", "Berserk", "Chicken", "Frog", "Critical",
                "Poison", "Regen", "Protect", "Shell", "Haste", "Slow", "Stop", "Wall", "Faith", "Innocent", "Charm",
                "Sleep", "Don't Move", "Don't Act", "Reflect", "Death Sentence", "Blind", "Zombie"}
EVERYDAY_WORDS = {"attack", "defend", "item", "items", "counter", "jump", "steal", "charge", "throw", "talk", "sing",
                  "dance", "guts", "elemental", "aim", "punch", "yell", "shout", "cure", "raise", "protect", "shell",
                  "wall", "regen", "haste", "slow", "stop", "float", "reflect", "quick", "dark", "holy", "fire", "ice",
                  "bolt", "poison", "sleep", "silence", "berserk", "frog", "death", "move", "accumulate", "scream",
                  "heal", "wish", "stone", "praise", "threaten", "preach", "solution", "mimic",
                  "spell", "paralyze", "energy", "circle", "catch", "dash", "crush", "loss", "melt", "mute", "soft",
                  "level", "height", "exp", "ct", "caution", "seal", "shock", "allure", "train", "work", "sacred",
                  "explosive", "vampire", "plague", "gust", "regulator", "porky", "knife", "sword", "spear", "stick",
                  "ball", "axe", "hammer", "katana", "dictionary", "shuriken", "scratch", "zodiac", "fairy", "hurricane",
                  "blizzard", "persuade", "invitation", "difference", "distribute", "tentacle"}
# ReMixed's "Dark Knight" is its own job (the remaster's Dark Knight), not vanilla's (Gafgarion's, the remaster's
# Fell Knight).
KEPT_NAMES = {"Dark Knight"}
# A name made of digits ("3", Math Skill's) is never an ability's name in a description.
NUMBER_NAME = re.compile(r"^\d+$")


def with_names(text: str, names: list[tuple[str, str]]) -> str:
    """text with each vanilla name in names (a whole word or words, as written) as the remaster's, in one pass."""
    if not names:
        return text
    table = dict(names)
    pattern = "|".join(re.escape(old) for old, _ in names)  # longest first
    return re.sub(rf"(?<![\w-])(?:{pattern})(?![\w-])", lambda m: table[m.group(0)], text)


# ---------------------------------------------------------------- templates

LIST_FIELDS = {"CancelFlags", "NoStackFlags"}


def read_template(path: Path) -> tuple[str, str, str, dict[int, dict[str, object]]]:
    """(root element, entry element, version, {id: {field: value}}) from a loader template."""
    text = re.sub(r"<!--.*?-->", "", path.read_text(encoding="utf-8-sig"), flags=re.S)
    root = re.search(r"<(\w+)>", text).group(1)
    # The loader names each root after its model class (JobTable…). The effect
    # template's root is named after the file instead; use the class name, which
    # is what the loader's own serializer writes.
    root = re.sub(r"Data$", "Table", root)
    version = re.search(r"<Version>(\d+)</Version>", text).group(1)
    entries: dict[int, dict[str, object]] = {}
    element = ""
    for element, body in re.findall(r"<(\w+)>\s*(<Id>.*?)</\1>", text, re.S):
        values: dict[str, object] = {}
        for name, inner, empty in re.findall(r"<(\w+)>(.*?)</\1>|<(\w+)\s*/>", body, re.S):
            if empty:
                values[empty] = frozenset()
            elif name in LIST_FIELDS:
                values[name] = frozenset(re.findall(r"<StatusEffectType>(\w+)</StatusEffectType>", inner))
            else:
                values[name] = inner.strip()
        entries[int(values.pop("Id"))] = values
    return root, element, version, entries


def flag_set(value: object) -> frozenset[str]:
    """A template flags value ("A, B", "None" or "0") as a set of names."""
    if isinstance(value, frozenset):
        return value
    return frozenset(s.strip() for s in str(value).split(",") if s.strip() not in ("", "None", "0"))


def same(ours: object, theirs: object) -> bool:
    """Compare a decoded value with a template value."""
    if isinstance(ours, frozenset):
        return ours == flag_set(theirs)
    return str(ours) == str(theirs)


# Flag types with no "None" member: the loader only accepts 0 for an empty set.
EMPTY_AS_ZERO = {"Flags", "AIBehaviorFlags", "CheckFlags", "TypeFlags",
                 "ExtendAbilityIdFlagBits", "ExtendReactionSupportMovementIdFlagBits"}


def show(value: object, order: list[str] | None = None, field_name: str = "") -> str:
    if isinstance(value, frozenset):
        if not value:
            return "0" if field_name in EMPTY_AS_ZERO else "None"
        return ", ".join(sorted(value, key=lambda n: order.index(n) if order and n in order else 0))
    return str(value)


# ---------------------------------------------------------------- build

BUILDS = ["enhanced", "classic"]
# Tables the classic build can't take (mod loader 1.7.5), and why.
CLASSIC_UNSUPPORTED = {
    "MapTrapFormationData": "the loader can't find classic's Move-Find table (it keeps the PS1's 16-byte records)",
    "ItemEquipBonusData": "the loader can't patch classic's equipment bonuses (they're 25-byte records, "
                          "it expects 26); items are re-pointed to matching vanilla sets instead",
    "ItemOptionsData": "classic abilities can't be updated to follow ReMixed's reordered status sets, so "
                       "classic keeps the vanilla order; weapons are re-pointed to matching sets instead",
    "AbilityEffectNumberFilterData": "classic can't take ReMixed's ability stats yet, so reworked abilities "
                                     "would get effects that don't match what they do",
}


@dataclass
class TableResult:
    spec: TableSpec
    entries: dict[int, dict[str, object]]  # id -> changed fields with ReMixed values
    skipped: list[tuple[int, str, str]]
    not_written: str = ""  # why this table isn't written for this build
    dropped_bits: int = 0
    agree: int = 0  # template check: fields where our vanilla decode matches the remaster
    checked: int = 0
    disagree: dict[str, int] = field(default_factory=dict)
    conflicts: list[tuple[int, str]] = field(default_factory=list)  # ReMixed changes a field the remaster also changed


@dataclass
class ClassicLinks:
    """Vanilla and ReMixed equipment-bonus and status sets, for re-pointing on classic.

    Classic can't take ReMixed's equipment-bonus table, and keeps the vanilla
    status-set order (its abilities can't be updated to follow a reorder). So
    items and weapons are pointed at whichever vanilla set has the contents
    ReMixed wants.
    """
    bonus_old: list[bytes]
    bonus_new: list[bytes]
    options_old: list[bytes]
    options_new: list[bytes]
    reworked_abilities: set[int]  # abilities whose combat stats ReMixed changes (classic can't take those)

    @classmethod
    def read(cls, scus_old: bytes, scus_new: bytes) -> ClassicLinks:
        def rows(scus: bytes, offset: int, count: int, size: int, mask_first: bool = False) -> list[bytes]:
            out = []
            for i in range(count):
                row = bytearray(scus[offset + i * size : offset + (i + 1) * size])
                if mask_first:
                    row[0] &= 0xF0  # chance-tier bits need ReMixed's code; compare the rest
                out.append(bytes(row))
            return out
        reworked = set()
        for a in range(0x170):
            old = bytearray(scus_old[ABILITY_ATTRIBUTES + a * 14 : ABILITY_ATTRIBUTES + (a + 1) * 14])
            new = bytearray(scus_new[ABILITY_ATTRIBUTES + a * 14 : ABILITY_ATTRIBUTES + (a + 1) * 14])
            old[6] &= ~KEEP_VANILLA_FLAG4
            new[6] &= ~KEEP_VANILLA_FLAG4
            if old != new:
                reworked.add(a)
        return cls(rows(scus_old, 0x54AC4, 80, 25), rows(scus_new, 0x54AC4, 80, 25),
                   rows(scus_old, 0x547C4, 128, 6, True), rows(scus_new, 0x547C4, 128, 6, True), reworked)


def repoint(old_id: int, new_id: int, table_old: list[bytes], table_new: list[bytes]) -> int | None:
    """The vanilla id whose contents match ReMixed's set new_id, or None if no vanilla set does."""
    if new_id >= len(table_new) or old_id >= len(table_old):
        return new_id
    wanted = table_new[new_id]
    if table_old[old_id] == wanted:
        return old_id  # already has the right effect
    if table_old[new_id] == wanted:
        return new_id
    return next((i for i, row in enumerate(table_old) if row == wanted), None)


def classic_adjust(spec: TableSpec, rid: int, a: Fields, b: Fields, links: ClassicLinks, skipped: list) -> Fields:
    out = dict(b)
    if spec.xml == "AbilityData" and rid in links.reworked_abilities and a["AIBehaviorFlags"] != b["AIBehaviorFlags"]:
        # AI flags describe what an ability does. Classic keeps the vanilla
        # version of reworked abilities, so it keeps their vanilla AI flags too.
        out["AIBehaviorFlags"] = a["AIBehaviorFlags"]
        skipped.append((rid, "AIBehaviorFlags", "they describe ReMixed's version of the ability, "
                                                "and classic can't take its combat stats"))
    if spec.xml == "ItemData":
        y = repoint(a["EquipBonusId"], b["EquipBonusId"], links.bonus_old, links.bonus_new)
        if y is None:
            out["EquipBonusId"] = a["EquipBonusId"]
            skipped.append((rid, "EquipBonusId", "classic can't take ReMixed's equipment bonus sets, "
                                                 "and no vanilla set matches the one it wants"))
        else:
            out["EquipBonusId"] = y
    if spec.xml == "ItemWeaponData" and b["Formula"] != 2:  # formula 2 casts a spell; the byte is an ability id
        y = repoint(a["OptionsAbilityId"], b["OptionsAbilityId"], links.options_old, links.options_new)
        if y is None:
            out["OptionsAbilityId"] = a["OptionsAbilityId"]
            skipped.append((rid, "OptionsAbilityId", "classic keeps the vanilla status sets, "
                                                     "and none matches the one ReMixed wants"))
        else:
            out["OptionsAbilityId"] = y
    return out


def build_table(spec: TableSpec, scus_old: bytes, scus_new: bytes, template: dict[int, dict],
                held: dict[int, str] | None = None, vanilla_homes: dict[int, set[str]] | None = None,
                classic: ClassicLinks | None = None) -> TableResult:
    result = TableResult(spec, {}, [])
    for i in range(spec.count):
        start = spec.offset + i * spec.size
        old, new = scus_old[start : start + spec.size], scus_new[start : start + spec.size]
        rid = spec.first_id + i
        a, b = spec.decode(old), spec.decode(new)
        if held is not None:
            b = hold_back_abilities(spec, rid, b, held, vanilla_homes or {}, result.skipped)
        if classic is not None:
            b = classic_adjust(spec, rid, a, b, classic, result.skipped)
        base = template.get(rid, {})
        for f, value in a.items():  # check the decoder against the remaster's defaults
            if f in base:
                result.checked += 1
                if same(value, base[f]):
                    result.agree += 1
                else:
                    result.disagree[f] = result.disagree.get(f, 0) + 1
        if spec.xml == "ItemOptionsData" and (old[0] & 0x0F) != (new[0] & 0x0F):
            result.dropped_bits += 1
        changed = {f: b[f] for f in a if a[f] != b[f]}
        for f in list(changed):
            reason = spec.skip(rid, f, b[f])
            if reason:
                result.skipped.append((rid, f, reason))
                del changed[f]
                continue
            if f in base and isinstance(b[f], frozenset):
                # Apply ReMixed's additions and removals to the remaster's value, so
                # the remaster's own extras (e.g. Perfume on more jobs) survive.
                theirs = flag_set(base[f])
                merged = (theirs - (a[f] - b[f])) | (b[f] - a[f])
                if merged == theirs:
                    del changed[f]
                else:
                    changed[f] = merged
            elif f in base and not same(a[f], base[f]):
                result.conflicts.append((rid, f))
        if changed and spec.whole_row:
            changed = {f: b[f] for f in b if not spec.skip(rid, f, b[f])}
        if changed:
            result.entries[rid] = changed
    return result


ORDERS = {
    **{f"TrapFlags{k}": list(reversed(TRAP_FLAGS)) for k in range(1, 5)},
    "EquippableItems": list(reversed(EQUIPMENT)) + sorted(EQUIPMENT_REMASTER_ONLY),
    "InnateStatus": list(reversed(STATUS_NAMES)), "ImmuneStatus": list(reversed(STATUS_NAMES)),
    "StartingStatus": list(reversed(STATUS_NAMES)), "Effects": list(reversed(STATUS_NAMES)),
    "AIBehaviorFlags": AI_FLAGS, "CheckFlags": STATUS_CHECK_FLAGS,
    "ExtendAbilityIdFlagBits": EXTENDED_ABILITY_BITS, "ExtendReactionSupportMovementIdFlagBits": EXTENDED_RSM_BITS,
    "CancelFlags": STATUS_EFFECT_TYPES, "NoStackFlags": STATUS_EFFECT_TYPES,
}


def write_xml(result: TableResult, root: str, element: str, version: str, names: Names, out: Path, source: str) -> None:
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        f"<!-- Generated by tools/build_mod.py from {escape(source)}. Only fields ReMixed changes are listed. -->",
        f"<{root}>",
        f"  <Version>{version}</Version>",
        "  <Entries>",
    ]
    for rid, changed in sorted(result.entries.items()):
        label = result.spec.name(rid, names)
        comment = f" <!-- {escape(label)} -->" if label else ""
        lines.append(f"    <{element}>")
        lines.append(f"      <Id>{rid}</Id>{comment}")
        for f, value in changed.items():
            if f in LIST_FIELDS:
                if value:
                    lines.append(f"      <{f}>")
                    for v in sorted(value, key=STATUS_EFFECT_TYPES.index):
                        lines.append(f"        <StatusEffectType>{v}</StatusEffectType>")
                    lines.append(f"      </{f}>")
                else:
                    lines.append(f"      <{f} />")
            else:
                order = ORDERS.get(f) or ORDERS.get(f.replace("Elements", "")) or (
                    list(reversed(ELEMENTS)) if f.endswith("Elements") else None)
                lines.append(f"      <{f}>{escape(show(value, order, f))}</{f}>")
        lines.append(f"    </{element}>")
    lines += ["  </Entries>", f"</{root}>"]
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")


# ---------------------------------------------------------------- ability attributes

ABILITY_ATTRIBUTES = ABILITIES + 0x1000  # 14 bytes x 368 in SCUS_942.21
# Flag byte 4 bits ReMixed reuses as markers for its own code. On the remaster
# they still mean "requires sword" / "requires Materia Blade", so keep vanilla's.
KEEP_VANILLA_FLAG4 = 0x0C
# Formulas whose behaviour ReMixed changes in code (docs/MECHANICS.md): the
# formula-table entries it repoints (0x13, 0x19, 0x1A, 0x27, 0x2D, 0x39, 0x46),
# the empty vanilla stubs it fills (0x11, 0x18), and vanilla routines it
# rewrites: 0x24 (Geomancy by terrain), 0x25 (a Momentum break knocks back), 0x2C
# (Magic Break), 0x2E (the Breaks always deal damage), 0x32 (Repeating Fist through
# the damage step), 0x3B (stats from the ability's data, vanilla's Scream), 0x3C
# (Wish by Y, Lava Ball's self-heal), 0x40 (Seal Evil by a status set) and
# 0x58 (HP/MP swap), and
# the effects that set stats or a status from the ability's data: 0x2A and 0x2B
# share one (which also swaps 0x2B's X and Y), 0x1C, 0x1D and 0x1A's Alters
# another. Other vanilla formulas only carry hooks for mechanics that apply across
# formulas (status chances, team targeting…).
CUSTOM_FORMULAS = {0x05, 0x11, 0x13, 0x15, 0x18, 0x19, 0x1A, 0x1C, 0x1D, 0x1E, 0x21, 0x24, 0x25, 0x27, 0x2A, 0x2B, 0x2C,
                   0x2D, 0x2E, 0x32, 0x39, 0x3B, 0x3C, 0x40, 0x42, 0x46, 0x55, 0x58}
FORMULA_TABLE = 0x8018F610 - 0x80067000  # BATTLE.BIN: routine address per formula id (u32)
# Formulas the code mod implements (code/IvaliceRemixed). An ability on an empty
# vanilla formula that isn't listed here would do nothing on the remaster.
CODE_FORMULAS = {0x05, 0x11, 0x13, 0x15, 0x18, 0x1A, 0x1C, 0x1D, 0x1E, 0x21, 0x24, 0x25, 0x27, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E,
                 0x32, 0x39, 0x3B, 0x3C, 0x40, 0x42, 0x46, 0x55, 0x58}
# ReMixed's battle rules the code mod implements (code/IvaliceRemixed/Patches/Rules.cs).
CODE_RULES = ["unevadable", "team-targeting", "auras", "limit-charge", "status-tiers", "learn-secondary",
              "weapon-requirements", "gain-rates", "post-move", "equipped-reaction", "landing", "monster-reactions",
              "jump-rework", "death-counter", "level-options", "brave-damage", "knockback", "weapon-range", "poach-odds", "status-ticks", "starting-mp", "zodiac", "equip-supports", "weapon-pick", "job-levels", "arrow-guard", "hide-unlearnable", "brave-reactions", "random-equipment", "skillset-types", "in-place", "side-evasion",
              "invite-control", "evade-checks", "reflect-reaction", "learn-flag",
              "formula-steps", "katana-break", "job-change-secondary", "gun-skills", "dragon-tech",
              "job-change-innates", "dismissal-poach", "unlearn", "charge-lock",
              "guest-control", "pass", "movement-mode", "cone-area", "start-statuses",
              "movement-bonus", "breeding-errands",
              "formation-split", "six-places", "errand-locks",
              "one-handing", "errands", "riding-boarding",
              "riding-actions", "persistent-performances", "hitting-bodies", "arc-obstacles",
              "taken-equipment", "victory-bonus",
              "move-find", "nemesis", "separate-saves", "remixed-events", "pack-files", "remixed-sprites",
              "remixed-faces", "old-guests", "guest-lookup", "map-movements", "remixed-scenarios", "world-roads",
              "unit-presets", "story-ai"]


GEOMANCY_TABLE = 0x8015D350  # ReMixed's BATTLE.BIN: 12 x (status set, element, effect), one per Elemental
GEOMANCY_TABLE_LOAD = bytes.fromhex("15800b3c" "50d36b35")  # lui t3, 0x8015; ori t3, t3, 0xD350 (at 0x8015D3E4)


def geomancy_table(battle_new: bytes) -> list[list[int]]:
    """ReMixed's Geomancy-by-terrain table, read where its code (0x8015D374) reads it."""
    if battle_new[0x8015D3E4 - 0x80067000 : 0x8015D3EC - 0x80067000] != GEOMANCY_TABLE_LOAD:
        sys.exit("ReMixed's Geomancy code doesn't read its table where this build expects; see docs/MECHANICS.md.")
    at = GEOMANCY_TABLE - 0x80067000
    return [list(battle_new[at + 3 * k : at + 3 * k + 3]) for k in range(12)]


UNEVADABLE_TABLE = 0x8015E690  # ReMixed's BATTLE.BIN: a byte per ability 0x190-0x19D; bit 0x80 = can't be evaded
# ori v1, zero, 0x190; subu v1, v0, v1; sltiu v0, v1, 14; beq; lui a1, 0x8016; addiu a1, a1, -6512 (at 0x80155E48)
UNEVADABLE_LOOKUP = bytes.fromhex("90010334" "23184300" "0e00622c" "04004010" "1680053c" "90e6a524")


def unevadable_abilities(battle_new: bytes) -> list[int]:
    """Abilities ReMixed's table marks as unevadable, read where its code (0x80155E40) reads them."""
    if battle_new[0x80155E48 - 0x80067000 : 0x80155E60 - 0x80067000] != UNEVADABLE_LOOKUP:
        sys.exit("ReMixed's unevadable-ability code doesn't read its table where this build expects; see docs/MECHANICS.md.")
    at = UNEVADABLE_TABLE - 0x80067000
    return [0x190 + k for k in range(14) if battle_new[at + k] & 0x80]


# ReMixed's gun skills and Charge (docs/MECHANICS.md: "Gun skills"): for ids 0x190-0x19D its action routine
# (0x8018B7E4) reads a (CT, signed bonus) pair at SCUS 0x80060D18 + 2 x id (the Jump table running into the Charge
# table) and, through 0x8015E660, a status set at 0x8015E690 + (id - 0x190) (& 0x7F; bit 0x80 is the unevadable flag),
# and runs the ability as formula 5.
GUN_TABLE = 0x80060D18
GUN_SKILL_IDS = range(0x190, 0x19E)
GUN_SKILLS = (0x192, 0x193, 0x194, 0x195, 0x19C, 0x19D)  # held back (CODE_ABILITIES) until the code mod has them
GUN_SKILL_CODE = {
    0x8018B7E4: "1200a597020030920680013c70fea3240e00622c080040101980043c4010050021082200190d22901680013c"
                "210823009879050ce43882a0ff388290",
    0x8015E660: "90e62390000000007f0062300200401000000000da3882a0050010340800e00300000000",
}


def gun_skills(battle_new: bytes, scus_new: bytes) -> list[list[int]]:
    """[id, CT, bonus, status set] for 0x190-0x19D, read where ReMixed's action routine reads them."""
    for at, code in GUN_SKILL_CODE.items():
        if battle_new[at - 0x80067000 : at - 0x80067000 + len(code) // 2] != bytes.fromhex(code):
            sys.exit(f"ReMixed's gun skill code at 0x{at:08X} isn't what this build expects; see docs/MECHANICS.md.")
    rows = []
    for ability in GUN_SKILL_IDS:
        at = GUN_TABLE - SCUS_FILE + 2 * ability
        bonus = scus_new[at + 1]
        status = battle_new[UNEVADABLE_TABLE - 0x80067000 + ability - 0x190] & 0x7F
        rows.append([ability, scus_new[at], bonus - 0x100 if bonus >= 0x80 else bonus, status])
    return rows


WEAPON_MASK_INDEX = 0x80154030  # ReMixed's BATTLE.BIN: a byte per ability (368): a mask number, or 0xFF for none
WEAPON_MASKS = 0x801541A0  # 16 u32 masks, bit 0x80000000 >> item type
# Where ReMixed's two checks read them (menu 0x80153BDC, AI 0x80153CA0): lui at, 0x8015; add; lbu v0, 0x4030(…);
# ori v1, zero, 0xFF; beq; sll v0, v0, 2; add v0, at, v0; lbu v1, 29(…) (the right hand); lw at, 0x41A0(v0).
WEAPON_LOOKUPS = {
    0x80153BDC: "1580013c" "20183200" "30406290" "ff000334" "21004310" "80100200" "20102200" "1d00a392" "a041418c",
    0x80153CA0: "1580013c" "20202400" "30408290" "ff000334" "21004310" "80100200" "20102200" "1d000392" "a041418c",
}


def weapon_requirements(battle_new: bytes) -> list[list[int]]:
    """ReMixed's weapon requirements: [ability, mask of allowed item types] for each ability that has one."""
    for at, code in WEAPON_LOOKUPS.items():
        if battle_new[at - 0x80067000 : at - 0x80067000 + 36] != bytes.fromhex(code):
            sys.exit(f"ReMixed's weapon-requirement code at 0x{at:08X} doesn't read its tables where this build expects; "
                     "see docs/MECHANICS.md.")
    index = battle_new[WEAPON_MASK_INDEX - 0x80067000 : WEAPON_MASK_INDEX - 0x80067000 + 368]
    masks = [int.from_bytes(battle_new[WEAPON_MASKS - 0x80067000 + 4 * k : WEAPON_MASKS - 0x80067000 + 4 * k + 4], "little")
             for k in range(16)]
    return [[a, masks[k]] for a, k in enumerate(index) if k != 0xFF]


# ReMixed's Extra Options event (EVENT/TEST.EVT event 0xC9) sets its defaults once, at 0x192050:
# AC 14 00 hi hi (var 0x14 = hi << 16, an opcode ReMixed adds) then B0 14 00 lo lo (ADD var 0x14, lo).
# Var 0x14: byte 0 JP Gain %, byte 1 EXP Gain %, byte 2 low nibble the spillover JP divisor.
OPTION_DEFAULTS = 0x192050


def option_defaults(test_evt: bytes) -> dict[str, int]:
    """ReMixed's default EXP and JP rates and spillover divisor, read where its options event sets them."""
    at = OPTION_DEFAULTS
    code = test_evt[at : at + 10]
    if code[0:3] != bytes.fromhex("ac1400") or code[5:8] != bytes.fromhex("b01400"):
        sys.exit("ReMixed's options event doesn't set var 0x14 where this build expects; see docs/MECHANICS.md.")
    value = (int.from_bytes(code[3:5], "little") << 16) + int.from_bytes(code[8:10], "little")
    return {"jp": value & 0xFF, "exp": (value >> 8) & 0xFF, "spillover": (value >> 16) & 0x0F}


STATUS_SETS = 0x547C4  # SCUS_942.21: 128 status-infliction sets, 6 bytes each (the loader's ItemOptionsData)

SCUS_FILE = 0x8000F800  # SCUS_942.21 is loaded here, header included: file offset = address - this
# ReMixed's monster reactions: where the random R/S/M pick (0x8005D0BC) gives a monster nothing, it jumps
# (0x8005D10C) to 0x8002D680, which gives a monster's random reaction its family's: a byte per monster job
# 0x5E-0x8D at 0x8002D700, + 0x100. Its code: job - 0x5E; only a monster; only a reaction (kind 2); < 48; the byte.
MONSTER_REACTIONS = 0x8002D700
MONSTER_REACTION_CODE = {
    0x8005D10C: "a0b50008",
    0x8002D680: "03000391" "0a004018" "a2ff6324" "02000234" "07005614" "0380013c" "3000622c" "04004018" "21082300"
                "00d72290" "e4740108" "00014234",
}


# ReMixed's Jump rework (BATTLE.BIN 0x80179DC8, the Jump range): it starts from range 0 (8 for a Mime) and
# height 0x70 (half-steps), then adds every learned 0x18A-0x18F's Jump table values (range, 2 x height), where
# vanilla starts from 1 and 2 and takes the best of 0x18A-0x195. Its code: s4 = 0; Mime s4 = 8; s0 = 0x70; then
# for each listed ability, if it's 0x18A-0x18F, s0 += 2 x vertical and s4 += horizontal.
JUMP_TABLE = 0x8006102C  # SCUS: (horizontal, vertical) per ability 0x18A-0x195
JUMP_RANGE_CODE = {
    0x80179DD0: "00001434",
    0x80179E28: "70001034" "08001434" "70001034",
    0x80179EB8: "76fe6224" "0600422c" "00000000" "11004010" "76fe6224" "ffff4230" "40100200" "21204900" "01008390"
                "00008290" "40280300" "2180b000" "21a05400",
}
JUMP_ABILITIES = range(0x18A, 0x190)  # Jump and five Jump Range +1
# ReMixed's Jump skillset (18) also holds Lancet and the three Breaths, in slots 8-11, where the Lancer learns them;
# battle shows them under Dragon Tech (skillset 0x64, the same four in the same slots, read with Jump's learned bits).
# The remaster runs every ability of a Jump-type skillset as a Jump (formula 0x64) and lists one entry, so without the
# code mod's Dragon Tech ("dragon-tech") they're left out of the Jump skillsets (only there).
JUMP_SKILLSET_EXTRAS = {a: "in ReMixed's Jump skillset, which the remaster runs as Jumps and lists as one entry"
                        for a in (0x144, 0x151, 0x152, 0x153)}


def jump_rework(battle_new: bytes, scus_new: bytes) -> list[list[int]]:
    """ReMixed's (range, height) for Jump and the five Jump Range +1, checked against the code that adds them."""
    for at, code in JUMP_RANGE_CODE.items():
        if battle_new[at - 0x80067000 : at - 0x80067000 + len(code) // 2] != bytes.fromhex(code):
            sys.exit(f"ReMixed's Jump range code at 0x{at:08X} isn't what this build expects; see docs/MECHANICS.md.")
    at = JUMP_TABLE - SCUS_FILE
    return [[scus_new[at + 2 * k], scus_new[at + 2 * k + 1]] for k in range(len(JUMP_ABILITIES))]


EQUIP_SUPPORTS = 0x8005C318  # ReMixed: an equip mask per support bit, read by its rewrite of 0x8005C27C
EQUIP_SUPPORT_CODE = {0x8005C27C: "0600073c920086884d0085888f0086984a0085987c0002340080013c241826002140470018c3088d"
                                  "020060100600899025280501fcff4224f8ff20144208010013008690"}


def equip_supports(scus_new: bytes) -> list[int]:
    """ReMixed's 32 equip masks, one per support bit (bit k is support byte k / 8, 1 << (k % 8))."""
    for at, code in EQUIP_SUPPORT_CODE.items():
        if scus_new[at - SCUS_FILE : at - SCUS_FILE + len(code) // 2] != bytes.fromhex(code):
            sys.exit(f"ReMixed's equip support code at 0x{at:08X} isn't what this build expects; see docs/MECHANICS.md.")
    at = EQUIP_SUPPORTS - SCUS_FILE
    return [int.from_bytes(scus_new[at + 4 * k : at + 4 * k + 4], "little") for k in range(32)]


def monster_reactions(scus_new: bytes) -> list[int]:
    """ReMixed's reaction per monster job (0x5E-0x8D), read where its code (0x8002D680) reads them."""
    for at, code in MONSTER_REACTION_CODE.items():
        if scus_new[at - SCUS_FILE : at - SCUS_FILE + len(code) // 2] != bytes.fromhex(code):
            sys.exit(f"ReMixed's monster reaction code at 0x{at:08X} isn't what this build expects; see docs/MECHANICS.md.")
    table = scus_new[MONSTER_REACTIONS - SCUS_FILE : MONSTER_REACTIONS - SCUS_FILE + 48]
    reactions = [0x100 + b for b in table]
    if any(not 0x1A6 <= r <= 0x1C5 for r in reactions):
        sys.exit("ReMixed's monster reaction table has an id that isn't a reaction; see docs/MECHANICS.md.")
    return reactions


def status_tiers(scus_new: bytes) -> list[list[int]]:
    """ReMixed's chance tiers: [set, low nibble of its flags] for each set that has one."""
    return [[i, scus_new[STATUS_SETS + 6 * i] & 0x0F] for i in range(128) if scus_new[STATUS_SETS + 6 * i] & 0x0F]


def empty_formulas(battle: bytes) -> set[int]:
    """Formula ids whose vanilla routine is just `jr ra`. The remaster leaves the same ones empty."""
    out = set()
    for f in range(1, 0x65):
        routine = int.from_bytes(battle[FORMULA_TABLE + 4 * f : FORMULA_TABLE + 4 * f + 4], "little")
        if battle[routine - 0x80067000 : routine - 0x80067000 + 4] == bytes.fromhex("0800e003"):
            out.add(f)
    return out
OVERRIDE_FIELDS = {  # attribute byte -> OverrideAbilityActionData column
    0: "Range", 1: "EffectArea", 2: "Vertical", 7: "Element", 8: "Formula",
    9: "X", 10: "Y", 11: "InflictStatus", 12: "CT", 13: "MPCost",
}
OVERRIDE_FLAGS = {3: ("Flags12", 0), 4: ("Flags12", 1), 5: ("Flags34", 0), 6: ("Flags34", 1)}
# The equipment slot ReMixed puts in an ability's status byte (its data: 1 weapon, 2 shield, 3 head, 4 body, 5
# accessory) for its own slot pick, where vanilla and the remaster pick by the ability's id (0x3065C4) and agree:
# the Steals, the Knight's Rends and Mighty Sword. The remaster reads the byte as the action's status set, so it
# showed as added effects and, with nothing to break, a Rend's plain attack inflicted it (a player's report,
# 2026-11-19: Rend Helm's 3 is Silence, Rend Armor's 4 Frog; ReMixed's tail 0x8018BAF8 clears the statuses its slot
# numbers would name). These keep vanilla's byte, 0.
SLOT_STATUS_BYTES = {0x6E: 3, 0x6F: 4, 0x70: 2, 0x71: 1, 0x72: 5,  # Steal Helmet, Armor, Shield, Weapon, Accessory
                     0x8A: 3, 0x8B: 4, 0x8C: 2, 0x8D: 1,  # Rend Helm, Armor, Shield, Weapon (Head/Armor/... Break)
                     0xA0: 4, 0xA1: 3, 0xA2: 1, 0xA3: 5}  # Shellbust Stab, Blastar Punch, Hellcry Punch, Icewolf Bite


@dataclass
class OverrideResult:
    abilities: int = 0
    fields: int = 0
    conflicts: list[tuple[int, str]] = field(default_factory=list)
    custom_formula: list[int] = field(default_factory=list)
    code_formula: list[int] = field(default_factory=list)  # on a formula the code mod implements
    kept_formula: dict[int, int] = field(default_factory=dict)  # ability -> ReMixed formula it can't use yet
    slot_bytes: list[int] = field(default_factory=list)  # abilities whose slot number stays out of the status byte
    remaster_owned: list[int] = field(default_factory=list)
    written: Path | None = None


def ff16tools(cfg: dict, *args: object) -> None:
    exe = cfg.get("tools", {}).get("ff16tools")
    if not exe or not Path(exe).exists():
        sys.exit("Set tools.ff16tools in local.toml to FF16Tools.CLI.exe.")
    subprocess.run([exe, *map(str, args), "-g", "fft"], check=True, capture_output=True)


def build_ability_overrides(cfg: dict, scus_old: bytes, scus_new: bytes, empty: set[int],
                            remaster_owned: dict[int, str], code_formulas: set[int]) -> OverrideResult:
    """ReMixed's ability attributes as the enhanced build's OverrideAbilityActionData table.

    Starts from the game's own table (the remaster overrides CT and MP on some
    abilities) and adds ReMixed's changed fields on top. Abilities ReMixed moves
    onto an empty vanilla formula keep their vanilla formula, X and Y until the
    code mod implements it, and slots the remaster uses for its own abilities
    are left alone.
    """
    result = OverrideResult()
    work = WORK / "remaster" / "build" / "overrides"
    base = WORK / "remaster" / "extract" / "enhanced" / "nxd" / "overrideabilityactiondata.nxd"
    if not base.exists():
        pack = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced" / "0004.pac"
        ff16tools(cfg, "unpack", "-i", pack, "-f", "nxd/overrideabilityactiondata.nxd",
                  "-o", WORK / "remaster" / "extract" / "enhanced")
    shutil.rmtree(work, ignore_errors=True)
    (work / "in").mkdir(parents=True)
    shutil.copy(base, work / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", work / "in", "-o", work / "table.sqlite")

    db = sqlite3.connect(work / "table.sqlite")
    columns = ["Flags12", "Flags34", *OVERRIDE_FIELDS.values()]
    rows = {r[0]: dict(zip(columns, r[1:])) for r in
            db.execute(f"select Key, {', '.join(columns)} from OverrideAbilityActionData")}
    for i in range(0x170):
        start = ABILITY_ATTRIBUTES + i * 14
        old, new = scus_old[start : start + 14], bytearray(scus_new[start : start + 14])
        new[6] = (new[6] & ~KEEP_VANILLA_FLAG4) | (old[6] & KEEP_VANILLA_FLAG4)
        if i in SLOT_STATUS_BYTES:
            if new[11] != SLOT_STATUS_BYTES[i] or old[11] != 0:
                sys.exit(f"ability 0x{i:03X}'s status byte is 0x{new[11]:02X} in ReMixed (vanilla 0x{old[11]:02X}), "
                         f"not its slot {SLOT_STATUS_BYTES[i]}: see SLOT_STATUS_BYTES")
            new[11] = old[11]
            result.slot_bytes.append(i)
        if i in remaster_owned:
            if old != bytes(new):
                result.remaster_owned.append(i)
            continue
        if new[8] in empty - code_formulas:
            result.kept_formula[i] = new[8]
            new[8:11] = old[8:11]  # formula, X, Y
        elif new[8] in code_formulas:
            result.code_formula.append(i)
        elif new[8] in CUSTOM_FORMULAS:
            result.custom_formula.append(i)
        if old == bytes(new):
            continue
        row = rows[i]
        flags = {"Flags12": json.loads(row["Flags12"]) or [-1, -1], "Flags34": json.loads(row["Flags34"]) or [-1, -1]}
        for k in range(14):
            if old[k] == new[k]:
                continue
            if k in OVERRIDE_FIELDS:
                column = OVERRIDE_FIELDS[k]
                if row[column] != -1:
                    result.conflicts.append((i, column))
                row[column] = new[k]
            else:
                column, slot = OVERRIDE_FLAGS[k]
                if flags[column][slot] != -1:
                    result.conflicts.append((i, f"{column}[{slot}]"))
                flags[column][slot] = new[k]
            result.fields += 1
        row["Flags12"] = json.dumps(flags["Flags12"]) if flags["Flags12"] != [-1, -1] else "[]"
        row["Flags34"] = json.dumps(flags["Flags34"]) if flags["Flags34"] != [-1, -1] else "[]"
        db.execute(f"update OverrideAbilityActionData set {', '.join(f'{c} = ?' for c in columns)} where Key = ?",
                   [row[c] for c in columns] + [i])
        result.abilities += 1
    db.commit()
    db.close()

    ff16tools(cfg, "sqlite-to-nxd", "-i", work / "table.sqlite", "-o", work / "out", "-t", "OverrideAbilityActionData")
    built = next((work / "out").rglob("*.nxd"))
    target = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "nxd" / "overrideabilityactiondata.nxd"
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(built, target)
    result.written = target
    return result


# The job menu's button bar (the game's UIButtonGuide table: a row per screen and slot, the input action whose icon
# shows, the UI text, a second icon, and Unknown25 = 1 for the left side). The job menu is screen 1023 (B Back, A
# Confirm, Job Abilities and Job Summary on the left, X Job Progress); its slot 4 is empty. The code mod pages the job
# grid with up and down past its edge (the Dark Knight on page two; Patches/JobMenuPages.cs, from cipherxof's
# GenericJobs, which does the same when it's installed), so the slot shows d-pad up and down (actions 33
# menu_up and 34 menu_down) with "Change Page" (UI text 3206), as screen 15003 does.
JOB_MENU_GUIDE = {"Key": 1023, "Key2": 4, "Unknown8": 33, "UnknownC": 3206, "Unknown14": 34, "Unknown25": 1}
# The Tavern's roster picker (Dispatch Missions) with its cursor on a unit already picked is screen 5023 (B Remove Unit,
# Change Display, and Start Confirm Units on the left). The code mod makes the confirm button there Confirm Units too
# (TavernStorage.OnPickerPress, asked 2026-11-19), so its empty slot 0 shows A (action 26) with "Confirm Units" (1969).
PICKER_GUIDE = {"Key": 5023, "Key2": 0, "Unknown8": 26, "UnknownC": 1969}


def build_job_menu_guide(cfg: dict) -> str:
    """The game's UIButtonGuide table with JOB_MENU_GUIDE's and PICKER_GUIDE's slots filled in. Returns a line for the
    report."""
    work = WORK / "remaster" / "build" / "buttonguide"
    base = WORK / "remaster" / "extract" / "enhanced" / "nxd" / "uibuttonguide.nxd"
    if not base.exists():
        pack = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced" / "0004.pac"
        ff16tools(cfg, "unpack", "-i", pack, "-f", "nxd/uibuttonguide.nxd", "-o", WORK / "remaster" / "extract" / "enhanced")
    shutil.rmtree(work, ignore_errors=True)
    (work / "in").mkdir(parents=True)
    shutil.copy(base, work / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", work / "in", "-o", work / "table.sqlite")
    db = sqlite3.connect(work / "table.sqlite")
    for slot, name in ((JOB_MENU_GUIDE, "job menu's"), (PICKER_GUIDE, "Tavern's roster picker's")):
        key = {"Key": slot["Key"], "Key2": slot["Key2"]}
        row = db.execute("select Unknown8, UnknownC from UIButtonGuide where Key = :Key and Key2 = :Key2", key).fetchone()
        if row is None or row != (0, 0):
            sys.exit(f"The {name} button bar slot isn't empty any more: {row}")
        values = {k: v for k, v in slot.items() if k not in key}
        db.execute(f"update UIButtonGuide set {', '.join(f'{c} = :{c}' for c in values)} where Key = :Key and Key2 = :Key2",
                   slot)
    db.commit()
    db.close()
    ff16tools(cfg, "sqlite-to-nxd", "-i", work / "table.sqlite", "-o", work / "out", "-t", "UIButtonGuide")
    built = next((work / "out").rglob("*.nxd"))
    target = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "nxd" / "uibuttonguide.nxd"
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(built, target)
    return ("the job menu's slot 4: d-pad up and down, \"Change Page\" (for the Dark Knight's page); the Tavern's "
            "roster picker on a picked unit: the confirm button, \"Confirm Units\"")


# ---------------------------------------------------------------- code-dependent abilities

# Ability slots whose ReMixed behaviour lives in its code (docs/MECHANICS.md).
# On the remaster they keep their vanilla name and effect, so handing them out
# in new places (Knight learning "Ignore Weather") looks and plays wrong.
CODE_ABILITIES = {
    0x1B7: "needs ReMixed's code (Counter Steal: a counter that steals, in vanilla's Gilgame Heart slot)",
    0x1BB: "needs ReMixed's code (MP Rage, a new reaction)",
    0x1CF: "needs ReMixed's code (Banditry: back attacks are critical hits; AI Thieves use Mug)",
    0x1D0: "needs ReMixed's code (Two Spells and learned passives)",
    0x1D9: "needs ReMixed's code (+50% damage dealt and taken)",
    0x1E4: "needs ReMixed's code (+20% damage)",
    0x1E5: "needs ReMixed's code (two-job stat growth)",
    0x1F4: "needs ReMixed's code (First Move: Move x2 until the unit's first turn ends)",
    0x1F5: "needs ReMixed's code (Move-Cleanse: moving removes bad statuses)",
    0x1F8: "needs ReMixed's code (+10 attack power with melee weapons)",
    0x1FD: "needs ReMixed's code (learned movement passives)",
    # ReMixed's Jump rework: one Jump plus five Jump Range +1, read by its code
    # from the Jump table. On the remaster they're still Level Jump 2-8 and
    # Vertical Jump 2.
    **{a: "needs ReMixed's code (its Jump rework)" for a in range(0x18A, 0x190)},
    # Gun skills in old Vertical Jump and Charge slots. On the remaster they're
    # still Vertical Jump 5-8 and Charge +10/+20.
    **{a: "needs ReMixed's code (a gun skill built on the Jump or Charge table)"
       for a in (0x192, 0x193, 0x194, 0x195, 0x19C, 0x19D)},
}
ABILITY_TABLES = {"AbilityData", "AbilityTypeData", "AbilityEffectNumberFilterData"}  # keyed by ability id
# CODE_ABILITIES the code mod implements (code/IvaliceRemixed/Patches/: Supports.cs,
# Movement.cs, Reactions.cs, Growth.cs, Banditry.cs, Passives.cs).
CODE_SUPPORTS = {0x1B7, 0x1BB, 0x1CF, 0x1D0, 0x1D9, 0x1DE, 0x1E4, 0x1E5, 0x1F4, 0x1F5, 0x1F8, 0x1FD}
# Skillsets that only work with ReMixed's code; they keep their vanilla rows.
CODE_SKILLSETS = {
    18: "ReMixed's Jump skillset needs its Jump rework (without it, Lancers would lose Vertical Jump 3-8)",
    52: "ReMixed's Jump skillset needs its Jump rework (without it, Lancers would lose Vertical Jump 3-8)",
}
ABILITY_ID_FIELDS = re.compile(r"^(AbilityId\d+|ReactionSupportMovementId\d+|InnateAbilityId\d+)$")


def ability_homes(scus: bytes) -> dict[int, set[str]]:
    """Where each ability is handed out: skillsets, monster skillsets and job innates."""
    homes: dict[int, set[str]] = {}

    def add(ability: int, home: str) -> None:
        if ability:
            homes.setdefault(ability, set()).add(home)

    for k in range(176):
        for f, v in job_command(scus[0x55294 + k * 25 : 0x55294 + (k + 1) * 25]).items():
            if ABILITY_ID_FIELDS.match(f):
                add(v, f"JobCommandData {k}")
    for k in range(48):
        for v in monster_job_command(scus[0x563C4 + k * 5 : 0x563C4 + (k + 1) * 5]).values():
            add(v, f"MonsterJobCommandData {176 + k}")
    for j in range(160):
        b = scus[0x518B8 + j * 48 : 0x518B8 + (j + 1) * 48]
        for k in range(4):
            add(u16(b, 1 + 2 * k), f"JobData {j}")
    return homes


def code_dependent_abilities(scus_old: bytes, scus_new: bytes, presentation: Presentation,
                             code_formulas: set[int], code_supports: set[int] = frozenset()) -> dict[int, str]:
    """Abilities that don't yet look or behave as ReMixed intends: id -> why.

    Covers slots whose behaviour is in ReMixed's code, abilities using a formula
    ReMixed rewrote, and slots ReMixed turned into a different ability (a new
    formula) whose name, animation or visual effect can't be brought along yet.
    """
    held = {a: why for a, why in CODE_ABILITIES.items() if a not in code_supports}
    for a in range(0x170):
        old = scus_old[ABILITY_ATTRIBUTES + a * 14 + 8]
        new = scus_new[ABILITY_ATTRIBUTES + a * 14 + 8]
        if new in CUSTOM_FORMULAS - code_formulas:
            held.setdefault(a, f"uses formula 0x{new:02X}, which ReMixed rewrote")
        elif new != old:
            missing = presentation.missing(a)
            if missing:
                held.setdefault(a, f"ReMixed made it a different ability (formula 0x{old:02X} to 0x{new:02X}), "
                                   f"and its {missing} can't be ported yet")
    return held


# ---------------------------------------------------------------- names, animations, effects

ABILITY_TYPES_SOURCE = "ability types"  # pseudo source: rebuilt from ReMixed's animation index scheme
BATTLE_BASE = 0x80067000
# What ReMixed names list slots it emptied ("-90" for skillset 0xA0), not names to port.
PLACEHOLDER_NAME = re.compile(r"^-\d+$")
NAME_LISTS = {  # list -> (remaster name table, entries, first names used to find the list)
    "abilities": ("Ability", 512, ["Cure", "Cure 2", "Cure 3"]),
    "jobs": ("Job", 160, ["Squire", "Squire", "Squire"]),
    "skillsets": ("JobCommand", 176, ["Attack", "Defend", "Equip Change"]),
}


@dataclass
class Presentation:
    """What it takes for a reworked ability to look right: name, animation, visual effect."""
    types_old: bytes  # vanilla ability animation table (3 bytes x 454)
    types_new: bytes  # ReMixed's, rebuilt as the same 3-byte table
    types_kept: set[int]  # abilities whose ReMixed animation uses values vanilla never does (kept vanilla)
    effects_changed: set[int]  # abilities whose visual effect ReMixed changes
    renames: dict[str, dict[int, str]]  # name list -> id -> ReMixed name (real renames only)
    unnamed: set[int]  # abilities ReMixed renamed to something this tool can't port (blank or undecodable)
    effects_ported: set[int] = field(default_factory=set)  # visual effects the mod writes (AbilityEffectNumberFilterData)
    ability_names: dict[int, str] = field(default_factory=dict)  # every ability's ReMixed name (clean ones only)
    names_old: dict[str, dict[int, str]] = field(default_factory=dict)  # name list -> id -> vanilla's name

    def missing(self, ability: int) -> str:
        if ability in self.effects_changed - self.effects_ported:
            return "visual effect"
        if ability in self.types_kept:
            return "animation"
        if ability in self.unnamed:
            return "name"
        return ""

    @classmethod
    def read(cls, battle_old: bytes, battle_new: bytes) -> Presentation:
        f = lambda ram: ram - BATTLE_BASE  # noqa: E731
        count = 0x1C6
        types_old = battle_old[0x2CE10 : 0x2CE10 + 3 * count]
        # ReMixed: a 1-byte index per ability, a halfword per index (top 3 bits:
        # number of per-actor variants; low 13 bits: offset into a byte pool),
        # variants of [mask, a, b], then the default [a, b]. Byte 3 has its own table.
        types_new = bytearray()
        for i in range(count):
            index = battle_new[f(0x80093E10) + i]
            hw = int.from_bytes(battle_new[f(0x80093FD6) + 2 * index : f(0x80093FD6) + 2 * index + 2], "little")
            pool = f(0x800940BC) + (hw & 0x1FFF) + 3 * (hw >> 13)
            types_new += bytes([battle_new[pool], battle_new[pool + 1], battle_new[f(0x80159DCC) + i]])
        used_charge = {types_old[3 * i] for i in range(count)}
        used_anim = {types_old[3 * i + 1] for i in range(count)}
        used_text = {types_old[3 * i + 2] for i in range(count)}
        kept = set()
        for i in range(count):
            c, a, t = types_new[3 * i : 3 * i + 3]
            if (c, a, t) != tuple(types_old[3 * i : 3 * i + 3]) and not (
                    c in used_charge and a in used_anim and t in used_text):
                kept.add(i)
                types_new[3 * i : 3 * i + 3] = types_old[3 * i : 3 * i + 3]
        effects = {i for i in range(count)
                   if battle_old[EFFECT_TABLE + 2 * i : EFFECT_TABLE + 2 * i + 2]
                   != battle_new[EFFECT_TABLE + 2 * i : EFFECT_TABLE + 2 * i + 2]}

        renames: dict[str, dict[int, str]] = {}
        unnamed: set[int] = set()
        ability_names: dict[int, str] = {}
        names_old: dict[str, dict[int, str]] = {}
        for name, (_, entries, first) in NAME_LISTS.items():
            old = fft_text.read_list(battle_old, fft_text.find_list(battle_old, first), entries)
            new = fft_text.read_list(battle_new, fft_text.find_list(battle_new, first), entries)
            renames[name] = {}
            names_old[name] = {}
            for i in range(entries):
                (a, a_clean), (b, clean) = fft_text.decode(old[i]), fft_text.decode(new[i])
                if a and a_clean:
                    names_old[name][i] = a
                if name == "abilities" and b and clean and not PLACEHOLDER_NAME.match(b):
                    ability_names[i] = b
                if a.casefold() != b.casefold():  # "Move-Get Jp" to "Move-Get JP" isn't a rename
                    if b and clean and not PLACEHOLDER_NAME.match(b):
                        renames[name][i] = b
                    elif name == "abilities":
                        unnamed.add(i)
        return cls(bytes(types_old), bytes(types_new), kept, effects, renames, unnamed, ability_names=ability_names,
                   names_old=names_old)


# ---------------------------------------------------------------- code mod

CODE_PROJECT = ROOT / "code" / "IvaliceRemixed.Check" / "IvaliceRemixed.Check.csproj"  # builds the DLL too
CODE_CHECK = "IvaliceRemixed.Check.exe"
CODE_FILES = ["IvaliceRemixed.dll", "IvaliceRemixed.deps.json", "IvaliceRemixed.pdb"]
EIU_FILES = ["EventInstructionUpgrade.dll", "EventInstructionUpgrade.deps.json", "EventInstructionUpgrade.pdb"]
# In both mods' folders: the code they share (each loads its own copy) and the upgrade's API (Reloaded-II shares
# the upgrade's copy of that).
SHARED_CODE_FILES = ["IvaliceCore.dll", "IvaliceCore.pdb",
                     "EventInstructionUpgrade.Interfaces.dll", "EventInstructionUpgrade.Interfaces.pdb"]
CODE_DATA = Path("Data") / "patches.json"  # in the mod folder; read by the DLL at startup
NOTICES = "THIRD-PARTY-NOTICES.md"  # the licences of code the DLL ports (GenericJobs' MIT), shipped with it


def build_code(cfg: dict) -> Path | None:
    """Compile the code mod (code/IvaliceRemixed) and its offline check. Returns the output folder, or None without .NET."""
    dotnet = shutil.which("dotnet")
    if not dotnet:
        print("Note: no .NET SDK (dotnet) found, so the mod is built without its code part.")
        return None
    reloaded = cfg.get("tools", {}).get("reloaded")
    if not reloaded and cfg.get("target", {}).get("reloaded_mods"):
        reloaded = Path(cfg["target"]["reloaded_mods"]).parent
    if not reloaded or not (Path(reloaded) / "Loader" / "X64" / "Reloaded.Mod.Interfaces.dll").exists():
        sys.exit("Set tools.reloaded in local.toml to your Reloaded-II folder (the code mod builds against it).")
    if not (EIU_REPO / "src").is_dir():
        sys.exit("code/eiu is empty: run `git submodule update --init` (the Event Instruction Upgrade's repo).")
    # Beside the built mod, not in work/: the worktrees share one work/ folder (a link to the main checkout's), and two
    # builds at once there checked and shipped each other's DLLs (2026-11-12).
    out = ROOT / "mod" / "code-out"
    shutil.rmtree(out, ignore_errors=True)
    mods = cfg.get("target", {}).get("reloaded_mods") or Path(reloaded) / "Mods"
    run = subprocess.run([dotnet, "build", str(CODE_PROJECT), "-c", "Release", "-o", str(out),
                          f"-p:ReloadedDir={reloaded}", f"-p:ReloadedModsDir={mods}", "--nologo", "-v", "q"],
                         capture_output=True, text=True)
    if run.returncode:
        sys.exit(f"The code mod didn't build:\n{run.stdout}{run.stderr}")
    return out


def check_code(cfg: dict, code: Path, patches: Path) -> None:
    """Run the DLL's patches against the exe mapped from disk (code/IvaliceRemixed.Check).

    Tests every byte pattern, cross-check and install, and installs the real
    hooks on that copy, without launching the game.
    """
    exe = Path(cfg["target"]["tic_dir"]) / "FFT_enhanced.exe"
    # live-hooks: also installs the real hooks on the check's copy of the exe.
    run = subprocess.run([str(code / CODE_CHECK), str(exe), str(patches), "live-hooks"], capture_output=True, text=True)
    lines = [line.strip() for line in run.stdout.splitlines() if line.strip()] or ["(no output)"]
    print(f"  {'Code check (offline)':24} " + ("\n" + " " * 27).join(lines))
    if run.returncode:
        sys.exit(f"The code mod wouldn't apply to {exe.name}:\n{run.stdout}{run.stderr}")


def write_code_data(source: str, patches: dict[str, dict]) -> Path:
    """Data/patches.json: what the DLL patches, per game build (exe name)."""
    out = MOD_DIR / CODE_DATA
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps({"source": source, "builds": patches}, indent=2) + "\n", encoding="utf-8")
    checks = test_checklist(ROOT / "docs" / "GAMEPLAY_TODO.md")
    (out.parent / "test-checklist.json").write_text(json.dumps(checks, indent=2) + "\n", encoding="utf-8")
    return out


def test_checklist(todo: Path) -> list[str]:
    """The to-do's Testing items, for the test mode's report (its hand checks)."""
    text = todo.read_text(encoding="utf-8")
    marker = "\n## Testing"
    section = text.split(marker, 1)[1].split("\n## ", 1)[0] if marker in text else ""
    items: list[str] = []
    for line in section.splitlines():
        if line.startswith("- [ ] "):
            items.append(line[6:].strip())
        elif line.startswith("  ") and items:
            items[-1] += " " + line.strip()
    return [item.replace("`", "") for item in items]


# The story port's first experiment (docs/GAMEPLAY_TODO.md, "Waiting for the story port", step 1): whether the
# remaster runs an event script the mod supplies. Random battles run event 401 (0x191; seen on the Deck through the
# test mode's event log). The mod ships the remaster's own event401.e with one instruction in front: add 0x1234 to
# event variable 0x6B, which neither vanilla's nor ReMixed's events use. The DLL logs that variable at a battle's
# first turn in the test mode, and puts it back to 0. It worked on the Deck (2026-11-04: "event variable 0x6B is
# 0x1234"), so it's off again (None): outside the test mode nothing would put the variable back.
STORY_EXPERIMENT: dict | None = None  # was {"event": 401, "marker": bytes([0xB0, 0x6B, 0x00, 0x34, 0x12])}


def remaster_event(cfg: dict, number: int) -> bytes:
    """The enhanced build's original event script (script/enhanced/eventNNN.e), extracted once into work/."""
    extract = WORK / "remaster" / "extract" / "enhanced"
    path = extract / "script" / "enhanced" / f"event{number:03d}.e"
    if not path.exists():
        pack = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced" / "0005.pac"
        ff16tools(cfg, "unpack", "-i", pack, "-f", f"script/enhanced/event{number:03d}.e", "-o", extract)
    return path.read_bytes()


def write_story_experiment(cfg: dict) -> str:
    """The marked event script (STORY_EXPERIMENT) into the mod. Returns a line for the report."""
    if STORY_EXPERIMENT is None:
        return "off (it showed the remaster runs the mod's event scripts)"
    number, marker = STORY_EXPERIMENT["event"], STORY_EXPERIMENT["marker"]
    original = remaster_event(cfg, number)
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "script" / "enhanced" / f"event{number:03d}.e"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(marker + original)
    return f"event{number:03d}.e: the remaster's ({len(original)} bytes) with {marker.hex(' ')} in front"


# The story port's events so far: ReMixed's own code as the remaster's eventNNN.e, with the code mod's mark in front
# (code/IvaliceRemixed/Patches/RemixedEvents.cs runs ReMixed's instructions in code that starts with it).
REMIXED_EVENTS = [0x191]  # random battles' setup
# ReMixed's flags 0xC0-0xCF pick which of a battle's 16 units are there: 30 of its story events set them before their
# battles, and its after-battle event 8 (and its retry and preparation menus) clear them all; vanilla's events never
# touch them. Random battles' 0x191 adds the unit of each flagged slot that isn't there and removes the rest, and only
# when none is set does it flag the slots the game filled. The mod ships no event 8, so the flags stayed set from an
# earlier battle (0xC0-0xC2, 0xC4, 0xC5 and 0xC8 on the PC's world map), and 0x191 brought that battle's slots into the
# next random battle, whose units come in pairs on one tile of which the game places one: two monsters on one tile
# (seen on the Deck at Mandalia Plains, 2026-11-07). So 0x191 clears them first, as event 8 would have.
UNIT_FLAGS = range(0xC0, 0xD0)
REMIXED_EVENT_PREFIXES = {0x191: b"".join(bytes([0xBE, flag, 0]) for flag in UNIT_FLAGS)}  # ZERO flag

# The ReMixed tab of the in-game Options screen (asked 2026-11-11: "a custom menu", "a page in the game's Options"):
# each save keeps its own options where ReMixed keeps them (the code mod's SaveOptions), and this page changes them.
# The remaster builds its Options screen from two tables, ConfigCategory (a tab: its name and the items it lists) and
# ConfigItem (an item: its kind, name and descriptions), and keeps each item's state in arrays sized for ids 0-60, so
# a tab's items must be ids the game leaves empty (28, 29, 34-38, 40-45, 57-60). The page is laid out as ReMixed's own
# Extra Options menu is: its first row ("Extra Options") picks one of ReMixed's groups and the rows below it are that
# group's options, each a list of its choices, with ReMixed's own wording. The build adds the tab (category 6, empty in
# the game, which the code mod adds to the in-game screen's tabs; the layout has a seventh tab button) and the texts
# (rows of the UI text table); the code mod (OptionsPage.cs) serves the items' rows for the group shown and the lists'
# choices, and puts a change into the save in play. OPTIONS_PAGE_FILE gives it the groups, settings, values and texts.
OPTIONS_PAGE_FILE = "options-page.json"
OPTIONS_PAGE_CATEGORY = 6
OPTIONS_PAGE_SLOTS = [28, 29, 34, 35, 36, 37, 38, 40, 41, 42, 43]  # the menu row, then a group's options
# The page's texts are rows of the UI text table numbered on from the game's last (its 0-4049): the game finds a UI
# row by its place in an unbroken run of keys, and rows numbered 20000 on weren't found (a crash on the PC as the
# tab's name was looked up, 2026-11-11).
OPTIONS_PAGE_TAB = "ReMixed"
OPTIONS_PAGE_MENU = ("Extra Options", "ReMixed's options, a group at a time: pick a group here to show its options "
                     "below. Each save keeps its own; a new game starts with the mod's settings.")
OPTIONS_PAGE_OTHER = "Set in mod settings"  # shown for a setting none of the choices has
_PERCENT = [50, 75, 100, 125, 150, 200]
# (group, [(the setting, its name, its description, [(its value, the choice's text)]), ...]); the values as
# Options.cs has them, the texts ReMixed's (events 0xC9 and 0x11C), shortened where the game's value box is narrow
# (about 22 letters show; seen on the PC, 2026-11-11).
OPTIONS_PAGE = [
    ("Levels, Jobs & Gear", [
        ("LevelScaling", "Improved Level Scaling",
         'The "Party Level", which affects units with scaling levels, is decided by:',
         [("DeployedUnits", "Only deployed units"), ("EntireRoster", "All units (Vanilla)")]),
        ("FixedLevels", "Fixed or Minimum Levels", "Units with fixed levels will have their levels scale:",
         [("Never", "Never"), ("IfPartyLevelIsHigher", "Up to the Party Level")]),
        ("EnemyLevelBoost", "Enemy Level Boost",
         "Nightmare Mode raises the levels of units with fixed levels by 50, No Future Mode to 99.",
         [("Normal", "Normal Mode"), ("Nightmare", "Nightmare Mode"), ("NoFuture", "No Future Mode")]),
        ("JobLevels", "Job Level Settings",
         "A newly generated unit's preset unlocked job and Job Level will now apply to:",
         [("ByChapter", "More in later chapters"), ("SpecifiedJob", "One job (Vanilla)"),
          ("SpecifiedAndCurrent", "And the current job"),
          ("SpecifiedCurrentAndSecondary", "Current and secondary")]),
        ("EnemyJobs", "Enemy Job Settings",
         "Normal has enemies follow the Job Level settings. Expert sets the specified, current and secondary jobs to "
         "Lv. 8, Exceptional all jobs.\nExpert and Exceptional randomize R/S/M abilities for enemies set to have none.",
         [("Normal", "Normal"), ("Expert", "Expert"), ("Exceptional", "Exceptional")]),
        ("JobStats", "Job Stats",
         "The growths and multipliers for units will be set according to the best values in:",
         [("JobOnly", "The unit's job only"), ("JobAndSecondary", "Job and secondary")]),
        ("RareEquipment", "Rare Equipment",
         "Rare equipment normally can't be randomly generated, even on units at the right level. This can be "
         "changed, but Rare equipment will no longer be able to be stolen,\nand invited humans will no longer join "
         "after battle. Unique items are unchanged.",
         [("LeftOut", "No random Rare gear"), ("Allowed", "Random Rare gear")]),
        # The mod's own, not ReMixed's (asked 2026-11-27, a player's request): kept in the mod's settings.
        ("BrokenEquipment", "Broken Equipment",
         "Equipment broken on your units in battle (Rend, or a katana in Draw Out):\nNot in ReMixed. For every "
         "save: kept in the mod's settings.",
         [("Lost", "Is lost"), ("BackAfterBattle", "Returns after battle")]),
    ]),
    ("Permadeath", [
        ("PlayerDeaths", "Player Deaths",
         "Outside of a Rendezvous, when a player unit's death timer runs out, they:",
         [("DiePermanently", "Die permanently"), ("ReviveAfterBattle", "Revive after battle")]),
        ("Bodies", "General Deaths", "When a unit permanently dies, their body will:",
         [("TransformRandomly", "Transform randomly"), ("Crystal", "Become a crystal"),
          ("TreasureBox", "Become a treasure box")]),
    ]),
    ("JP and EXP", [
        ("JpGain", "JP Gain", "JP earned from actions, as a percentage of vanilla's.",
         [(p, f"{p}%" + (" (Vanilla)" if p == 100 else "")) for p in _PERCENT]),
        ("SpilloverJp", "Spillover JP",
         "The share of an action's JP that each of the actor's teammates in the same job gets.",
         [(0, "None"), (2, "1/2"), (4, "1/4 (Vanilla)"), (8, "1/8")]),
        ("ExpGain", "EXP Gain", "EXP earned from actions, as a percentage of vanilla's.",
         [(p, f"{p}%" + (" (Vanilla)" if p == 100 else "")) for p in _PERCENT]),
        ("VictoryBonus", "Bonus EXP & JP on Victory",
         "After a won battle, every unit on your side gets a level (EXP), and ten times an action's JP base in its "
         "current job (JP).",
         [("None", "No bonus"), ("Jp", "Bonus JP only"), ("Exp", "Bonus EXP only"), ("Both", "Bonus JP & EXP")]),
        ("JpRefund", "JP Refund", "Attempting to refund abilities that have been purchased will:",
         [("Full", "Refund 100%"), ("Half", "Refund 50%"), ("None", "Do nothing (Vanilla)")]),
    ]),
    ("Spellcasting", [
        ("AutoRenew", "Auto Renew", "At the end of every turn, MP will:",
         [(False, "Not change"), (True, "Restore by 1/8")]),
        ("StartingMp", "Starting MP", "Units will start battle with:", [("Full", "Full MP"), ("Ten", "10 MP")]),
        ("EveryoneNonCharge", "Cast Time", "Abilities with Charge Time:",
         [(False, "Take time to cast"), (True, "Cast instantly")]),
    ]),
    ("Combat Adjustments", [
        ("Bravery", "Bravery/Wariness", "Below 50 Brave, the amount of damage units will take:",
         [("LowersWithBrave", "Lowers with Brave"), ("Vanilla", "The same (Vanilla)")]),
        ("PoachingOdds", "Poaching Odds", "The odds of getting a rare poach:",
         [("ByBrave", "Up with lower Brave"), ("Vanilla", "31/256 (Vanilla)")]),
        ("BackAttacks", "Back Attacks", "Attacking from behind can grant critical hits:",
         [("BanditryAndMug", "With Mug or Banditry"), ("Vanilla", "At 4% (Vanilla)"),
          ("Everyone", "At all times")]),
        ("Knockback", "Knockback", '"Immortal" units and your party leader:',
         [("Everyone", "Can be knocked back"), ("Vanilla", "Can't be (Vanilla)")]),
        ("WeaponRange", "Minimum Weapon Range", "Ranged weapons:",
         [("NoMinimum", "No minimum range"), ("MinimumOf3", "Minimum of 3 (Vanilla)")]),
        ("PoisonAndRegen", "Poison & Regen", "Poison and Regen:",
         [("HpOnly", "Only affect HP"), ("HpAndMp", "Affect HP and MP")]),
        ("GuestControl", "Guest Control", "You can control guests:",
         [("Deployed", "When deployed by hand"), ("Always", "At all times")]),
        ("PowerMode", "Power Modes",
         "Squire: ally power +25%, enemy power -25%. Tactician: ally power -25% (Math Skill -50%), enemy power "
         "+25%.\nChemist: all power -50%. Custom: the percentages in the mod's settings.",
         [("Knight", "Knight"), ("Squire", "Squire"), ("Tactician", "Tactician"), ("Chemist", "Chemist"),
          ("Custom", "Custom")]),
        ("ZodiacStrength", "Zodiac Compatibility", "Zodiac Compatibility affects the power of abilities by:",
         [("Off", "0%"), ("Half", "12.5%"), ("Normal", "25%"), ("OneAndAHalf", "37.5%")]),
        ("Sexuality", "Sexuality", "The mode used for Zodiac Compatibility and most forms of Charm will be:",
         [("Straight", "Straight Mode"), ("Gay", "Gay Mode"), ("Bisexual", "Bisexual Mode"), ("Tic", "TIC Mode")]),
    ]),
    ("Nemesis System", [
        ("NemesisSystem", "Nemesis System",
         "Occasionally, a past enemy that was downed but wasn't finished off will reappear as an enemy in another "
         "battle.\nThe Nemesis' strength depends on the current chapter you're in.",
         [(True, "On"), (False, "Off (Vanilla)")]),
    ]),
    # The mod's own, not ReMixed's (asked 2026-11-18): kept in the mod's settings for every save (OPTIONS_PAGE_MOD).
    ("Controls", [
        ("FastForward", "Fast-forward",
         "The fast-forward button speeds the game up while it's held, or from one press to the next (the faster "
         "speed's button switches to it).\nFor every save: kept in the mod's settings.",
         [("Hold", "While held"), ("Toggle", "Press on, press off")]),
    ]),
]
# Options the mod keeps in its settings (Config.json) for every save rather than in the save, which the page changes
# there.
OPTIONS_PAGE_MOD = {"FastForward", "BrokenEquipment"}


def options_page(first_text: int) -> tuple[dict[int, str], dict, dict]:
    """The ReMixed tab (OPTIONS_PAGE): its texts (UI text id -> text, from first_text on), its category row, and the
    code mod's list (OPTIONS_PAGE_FILE: the slots, the menu row's texts, each group's name and options with their texts
    and choices)."""
    texts: dict[int, str] = {}

    def text(value: str) -> int:
        key = first_text + len(texts)
        texts[key] = value
        return key
    if max(len(options) for _, options in OPTIONS_PAGE) + 1 > len(OPTIONS_PAGE_SLOTS):
        raise SystemExit("a group of the options page has more options than the tab has rows")
    tab = text(OPTIONS_PAGE_TAB)
    menu = {"name": text(OPTIONS_PAGE_MENU[0]), "description": text(OPTIONS_PAGE_MENU[1])}
    other = text(OPTIONS_PAGE_OTHER)
    groups = []
    for group, options in OPTIONS_PAGE:
        listed = []
        for setting, name, description, choices in options:
            listed.append({"setting": setting, "name": text(name), "description": text(description),
                           "choices": [{"value": value, "text": text(label)} for value, label in choices]}
                          | ({"mod": True} if setting in OPTIONS_PAGE_MOD else {}))
        groups.append({"name": text(group), "options": listed})
    category = {"DLCFlags": 0, "ConfigItemIds": "[" + ",".join(map(str, OPTIONS_PAGE_SLOTS)) + "]", "NameUIId": tab}
    listing = {"category": OPTIONS_PAGE_CATEGORY, "slots": OPTIONS_PAGE_SLOTS, "menu": menu, "other": other,
               "groups": groups}
    return texts, category, listing


# Reveal Treasures (asked 2026-11-11): ReMixed's Preparation Menu offers it before a battle (event 0x192; its text,
# "Reveal item locations for N gil? Current gil: N."). The code mod (RevealTreasures.cs) asks it as the formation screen
# opens, in the box the screen asks "Commence battle?" in: a dialog of the mod's own (a UIDialog row like that one's,
# 16, with this text; the price and the gil as its two numbers), numbered on from the game's last.
REVEAL_TEXT = "Reveal item locations for <num=lnum0> gil?\nCurrent gil: <num=lnum1>."
REVEAL_LIKE = 16  # the dialog it copies: "Commence battle?", Yes and No
DISPATCH_UI_TEXT = {
    "title": "Dispatch Missions",
    "prompt": "Select a dispatch category or mission.",
    "button": "Send units out on Propositions and Jobs, or to Special Training and Contests.",
    "empty": "No missions available.",
    "missions": "Missions",
    "missions_button": "Rendezvous missions on offer, listed apart from the tavern's rumors.",
    "missions_help": "Select a mission to accept it.",
    # The Missions list's question (asked 2026-11-19: the confirm button, not Start, takes a mission), in its prompt
    # line (the code mod's MissionPrompt), with the remaster's own button icons, as its formation's prompts have them.
    "missions_accept": "Accept this mission? It begins as you leave town.   <keyicon=ok> Yes   <keyicon=cancel> No",
    "missions_accepted": "Mission accepted. It begins as you leave town.",
}
# The Tavern's left menu (UIMenuButtonLeft: labels, descriptions, actions): row 3 Rumors and Errands, row 7 the same
# with Errands locked ("???"). Dispatch Missions replaces Errands, as ReMixed's replaces vanilla's errands: row 3's
# second button is Dispatch Missions (DISPATCH_UI_TEXT's title and button text), keeping Errands' action, and the
# code mod picks row 3 or 7 by ReMixed's unlock and opens the Dispatch view from Errands' own case
# (TavernStorage.OnTavernUnlock, OnBarRequest, OnPrepare). Row 7 keeps the remaster's locked button. Both rows get a
# third button, Missions (asked 2026-11-18): the screen asks for Rumors for any button but the second, and the code mod
# lists only ReMixed's mission postings there and only the rumors under Rumors (RemixedScenarios.RumorShown).
TAVERN_MENU_ROWS = (3, 7)
TAVERN_DISPATCH_ROW = 3
TAVERN_MENU_ACTIONS = [9, 10]

def write_options_page(cfg: dict) -> str:
    """The ReMixed tab's texts (UI-en rows) and category, Reveal Treasures' text and dialog (REVEAL_TEXT), and the code
    mod's list (with the dialog's number). Returns a line for the report."""
    taken = nxd_keys(cfg, "ui.en", "UI-en", "0004.en.pac")
    if taken != set(range(max(taken) + 1)):
        raise SystemExit("the game's UI text rows aren't an unbroken run from 0; the options page's would not be found")
    texts, category, listing = options_page(max(taken) + 1)
    reveal_text = max(texts) + 1
    texts[reveal_text] = REVEAL_TEXT
    dispatch = {}
    for name, value in DISPATCH_UI_TEXT.items():
        key = max(texts) + 1
        texts[key] = value
        dispatch[name] = key
    listing["dispatch"] = dispatch
    menu = tavern_menu_rows(cfg, dispatch)
    dialogs = nxd_keys(cfg, "uidialog", "UIDialog")
    if dialogs != set(range(max(dialogs) + 1)):
        raise SystemExit("the game's dialogs aren't an unbroken run from 0; Reveal Treasures' would not be found")
    dialog = max(dialogs) + 1
    db = sqlite3.connect(nxd_sqlite(cfg, "uidialog"))
    columns = [c for (_, c, *_) in db.execute('pragma table_info("UIDialog")')]
    like = dict(zip(columns, db.execute('select * from "UIDialog" where Key = ?', (REVEAL_LIKE,)).fetchone()))
    db.close()
    like.pop("Key")
    like["TextUIId"] = reveal_text
    listing["reveal"] = {"dialog": dialog}
    said = [add_nxd_rows(cfg, "ui.en", "UI-en", {k: {"Unknown0": 0, "Text": v} for k, v in texts.items()},
                         "0004.en.pac"),
            replace_nxd_rows(cfg, "configcategory", "ConfigCategory", {OPTIONS_PAGE_CATEGORY: [category]}),
            add_nxd_rows(cfg, "uidialog", "UIDialog", {dialog: like}), menu]
    (MOD_DIR / OPTIONS_PAGE_FILE).write_text(json.dumps(listing, indent=1), encoding="utf-8")
    options = sum(len(g["options"]) for g in listing["groups"])
    return (f"{options} options in {len(listing['groups'])} groups, {len(texts)} texts, Reveal Treasures' dialog "
            f"{dialog}; " + "; ".join(said))


EIU_WORDS, EIU_WORDS_BYTES, EIU_WORDS_FILE = 0x80165EF4, 0x400, "eiu-battle-words.bin"
MOVE_FIND_LOCK, MOVE_FIND_LOCK_MAPS, MOVE_FIND_LOCK_FILE = 0x8015C000, 0x80, "remixed-move-find-lock.bin"
# ReMixed's battle event conditions per event (its 0xCE n): sector 0x14B08 + n / 2 of its disc, the half n & 1, 0x20
# bytes of offsets then 0x200 of records; one 0x220-byte preset per n, for the 512 event numbers.
REMIXED_PRESETS_SECTOR, REMIXED_PRESET_BYTES, REMIXED_PRESETS = 0x14B08, 0x220, 0x200
REMIXED_PRESET_OFFSETS = 0x20
REMIXED_PRESETS_FILE = "remixed-event-presets.bin"
REMIXED_EVENT_MARK = eiu_events.MARKER  # LABEL 0x2C, LABEL 0x3C: the upgrade runs only code that starts so
EVENT_LENGTHS = 0x8014D170  # BATTLE.BIN: parameter bytes per opcode


# The remaster's enhanced event code widened two instructions for its 32-bit line ids (its length helper 0x2CBE2C adds 3
# to MESSAGE and 2 to 0x51 in the enhanced mode; seen lining up its events with the PS1's): MESSAGE (0x10) is type,
# style, the line's Id (u32), unit, two bytes, x, y and arrow (u16 each), then its closing as a u16 (the PS1's: a u16
# message number, a byte); 0x51 (another message in a box) is the box, the Id (u32, 0xFFFFFFFF for none), two bytes.
# ReMixed's event code is converted to that layout by the upgrade's tools (eiu_events.remaster_layout): its message n
# becomes its line's Id (the event's first line + n - 1; none, 0xFFFFFFFF, for an event without text), and a 0xE6
# that patches a field past the message number points 2 bytes further on. Left as it was, the remaster read
# the fete's MESSAGE's message number and unit byte as one Id (0x80CF18) and showed an empty box.
# A box's size comes from its message group: when an event loads, the remaster scans its code to its first END and
# makes a group of each MESSAGE's Id (and the 0x51s after it), and sizes the box to the group's lines (0x1DEC14,
# 0x1DF084); a line with no group gets a box of no width, a letter a row. ReMixed's code writes some message numbers in
# as it runs (0xE6 into the low half of the Id, which the code mod then moves onto the line), so the build puts a
# MESSAGE for each of the event's lines no MESSAGE names, past a GOTO the runner takes at the start: never run, only
# scanned (shadow_messages).
LINE_ID_BASE = 0x40000


# The MESSAGE box styles the remaster draws are vanilla's places and kinds, a menu's kind 4 (eiu_events.message_style:
# ReMixed's own 0xE1 and 0xE3 crashed it on the PC at exe+0x136541 in 0x0EC's offer). The remaster's menus close as 0.
# Where the remaster puts a box (0x2D9490): a style whose place bits (0x70) are 0x10 (0x70 and 0x90 count as 0x10) goes
# in a speech window, the top one for box 1 and the bottom one for any other; 0 or 0x20 goes in the narration layer
# (frameless and centred, as the remaster's own narration is), where a menu's cursor is drawn at the top window's first
# line whatever the row. The tavern's boxes (TAVERN): its menus go in the bottom window with no portrait (unit 0), at
# TAVERN_MENU_XY (with no unit the window goes to 192 + x, 42 + y in the remaster's 1920 x 1080 UI units: here under
# the Tavernmaster's box, which the top window holds), and the code mod stretches a menu's box to fit its rows
# (Patches/TallMenus.cs); its other boxes the remaster would put in the narration layer go in the bottom window too.
SPEECH_PLACES = (0x10, 0x70)
TAVERN_MENU_STYLE, TAVERN_BOX_STYLE = 0x16, 0x92
TAVERN_MENU_XY = (237, 370)


# ReMixed's conversations keep their boxes open and swap the next lines into them (0x51: the box, the message, the
# portrait's row). The remaster keeps a box the size its first line opened it with, its width and its rows, so a longer
# line swapped in ran past its edges (seen on the PC in 0x0F9: five rows in a narrow box of three). So a swap to a line
# longer than the one the box opened with closes the box (0x51 with no message, as ReMixed's own skip on Start closes
# them) and opens it again with that line, by a MESSAGE like the one that opened it (its place, style and unit), with
# the swap's portrait row: a box of the line's own size, as the remaster's own scenes give each line. A PATCH_NEXT
# that writes the swap's message number in as it runs writes the MESSAGE's. A swap to a list of answers, or from one,
# opens the box again too: the remaster makes a box a menu only as a MESSAGE opens it, so a list swapped in showed its
# rows and took no answer, and the event read the last one (1-A's 0x0ED: Agrias's second answers to Bryn, "1. I said,
# make way.", came and went unseen and turned Bryn and Lorne away; its money question showed with no cursor, testers,
# 2026-11-18), and a line swapped into a menu's box stayed a menu.
CLOSE_BOX = 0xFFFF
# ReMixed's lines run to 815 characters (its PS1 boxes paged them); a remaster speech box holds three rows of about 62
# (seen on the PC). So a line longer than PAGE_CHARS is shown in pages (split_pages), each a line of its own after the
# event's, in a box of its own: after the box with the first page, the event waits for it (0xE5 1) and opens the next
# the same way. Menus and the boxes other than the two speech boxes (1 and 2) stay whole.
PAGE_CHARS = 170
SPEECH_BOXES = (1, 2)
SENTENCE_END = re.compile("[.!?…]['\")’”]*\\s+")


def split_pages(text: str) -> list[str]:
    """A long line in pages of up to PAGE_CHARS, as few and as even as its sentence ends (or else its spaces) allow."""
    if len(text) <= PAGE_CHARS:
        return [text]
    best: list[str] = [text]
    for count in range(-(-len(text) // PAGE_CHARS), len(text) // 40 + 2):
        pages, start = [], 0
        for k in range(1, count):
            target = start + (len(text) - start) / (count - k + 1)
            low, high = start + 40, min(start + PAGE_CHARS, len(text) - 30)

            def fits(cut: int) -> bool:
                return low <= cut <= high and text[:cut].count("<") == text[:cut].count(">")
            cuts = ([m.end() for m in SENTENCE_END.finditer(text) if fits(m.end())]
                    or [m.end() for m in re.finditer(r"\s+", text) if fits(m.end())])
            if not cuts:
                break
            cut = min(cuts, key=lambda c: abs(c - target))
            pages.append(text[start:cut].strip())
            start = cut
        pages = [page for page in pages + [text[start:].strip()] if page]
        if max(map(len, pages)) <= PAGE_CHARS:
            return pages
        if max(map(len, pages)) < max(map(len, best)):
            best = pages
    return best


def paged_lines(lines: list[tuple[str, str]], menus: set[int],
                count: int) -> tuple[list[tuple[str, str]], dict[int, list[int]]]:
    """An event's lines with each one past PAGE_CHARS (a menu's aside) cut to its first page and its other pages added
    after the event's count messages (its empty last ones back as empty lines), and those pages' message numbers by
    the line's."""
    out, pages = list(lines), {}
    for n, (speaker, text) in enumerate(lines, 1):
        parts = [] if n in menus else split_pages(text)
        if len(parts) < 2:
            continue
        out += [("", "")] * (count - len(out))
        out[n - 1] = (speaker, parts[0])
        pages[n] = []
        for part in parts[1:]:
            out.append((speaker, part))
            pages[n].append(len(out))
    return out, pages


def message_numbers(code: bytes, lengths: bytes,
                    shown: list[tuple[int, int | None]] | None = None) -> dict[int, int | None]:
    """The message shown by each MESSAGE (0x10) and 0x51 whose number ReMixed's code writes in as it runs (0xE6), by its
    offset in code (the PS1 layout, SCRIPT_VERSION applied): the constants SET (0xAE), ADD (0xB0) and ZERO (0xBE) put in
    the variable the 0xE6 names, followed through IF/ELSE on a var 0 just set to a constant (both ways after any other
    test) and through CALLs to the event's FUNCTIONs; None where it isn't a constant. ReMixed's events pick their script
    so in many ways: var 0x71 raised by the other script's count for the PSP one (0x0F6) or for the PSX one (0x0F5,
    0x0F9, which put the PSP script first), the test inline or in a FUNCTION, var 0x78 (0x115). shown, when given, gets
    each MESSAGE and 0x51 the run reaches (not in an IF it knows is skipped), in turn: (its offset, the number it shows:
    its own where nothing writes it in)."""
    starts, at = [], 0
    while at < len(code):
        starts.append(at)
        at += 1 + lengths[code[at]]
    bodies = {code[at + 1]: k + 1 for k, at in enumerate(starts) if code[at] == 0x0A}  # FUNCTION's label -> its body
    values: dict[int, int] = {}
    numbers: dict[int, int | None] = {}

    def run(k: int, depth: int, called: bool) -> None:
        stack: list[tuple[bool, bool | None]] = []  # each open IF: (active before it, its test: known or None)
        active, test, pending = True, None, []
        while k < len(starts):
            at, op = starts[k], code[starts[k]]
            k += 1
            if op == 0x0A and not called:  # a FUNCTION's body runs only from a CALL: on past its RETURN
                nested = 0
                while k < len(starts) and not (code[starts[k]] == 0x0B and nested == 0):
                    nested += {0x01: 1, 0x04: -1}.get(code[starts[k]], 0)
                    k += 1
                k += 1
                continue
            if op == 0x0B and called and (active or not stack):  # RETURN
                return
            if op == 0xE6:  # PATCH_NEXT: (the parameter it writes, the variable)
                pending.append((code[at + 1] & 0x3F, code[at + 2]))
                continue
            if op == 0x01:  # IF
                stack.append((active, test))
                active = active and test is not False
            elif op == 0x02 and stack:  # ELSEIF: a test not followed
                active = stack[-1][0]
            elif op == 0x03 and stack:  # ELSE
                active = stack[-1][0] and stack[-1][1] is not True
            elif op == 0x04 and stack:  # ENDIF
                active = stack.pop()[0]
            elif op == 0x0C and active and depth < 4 and code[at + 1] in bodies:  # CALL
                run(bodies[code[at + 1]], depth + 1, True)
            elif op in (0x10, 0x51):
                patched = False
                for offset, var in pending:
                    if offset == (2 if op == 0x10 else 1):
                        numbers[at] = values.get(var)
                        patched = True
                if shown is not None and active:
                    shown.append((at, numbers[at] if patched else u16(code, at + (3 if op == 0x10 else 2))))
            elif 0xAC <= op <= 0xBE and active:  # the variable operations
                var = u16(code, at + 1)
                if op == 0xAE:
                    values[var] = u16(code, at + 3)
                elif op == 0xB0 and var in values:
                    values[var] = (values[var] + u16(code, at + 3)) & 0xFFFF
                elif op == 0xBE:
                    values[var] = 0
                else:
                    values.pop(var, None)
                if var == 0:
                    test = values[0] != 0 if 0 in values else None
            elif 0xA0 <= op <= 0xA5:  # a comparison, which IF tests then
                test = None
            pending = []

    run(0, 0, False)
    return numbers


# ReMixed's PSP script (the one the port shows, SCRIPT_VERSION) leaves most of its speakers' names out of its lines,
# where its PSX script names them (E3 08 <name> E3 00): in 1-A's 0x0ED, Bryn's "I... I beg of you!" is the PSX's
# "Dancer" line, and only some of its lines name anyone. The remaster draws a line with no speaker under an empty name
# plate, and its list of answers took no cursor (testers, 2026-11-18: "No way to select an option. But hitting the
# cancel button works to say no", on Agrias's "1. Aye, that shall suffice. 2. Nay, keep your coin."; "a number of text
# blocks without a name associated... this weird shadow where a name would go"). Each such line takes the name its PSX
# twin has: the message the same MESSAGE shows with flag 0xB2 at 0.
def script_twins(code: bytes, lengths: bytes) -> dict[int, int]:
    """Each PSP-script message ReMixed's event code shows -> the PSX-script message its MESSAGE (or 0x51) shows instead
    with flag 0xB2 at 0 (code in the PS1 layout, as ReMixed has it). Runs of the two that don't reach the same
    instructions are paired where they do."""
    runs = []
    for version in (SCRIPT_VERSION, (SCRIPT_VERSION[0], bytes([0xAE, 0x00, 0x00, 0x00, 0x00]))):
        shown: list[tuple[int, int | None]] = []
        message_numbers(code.replace(*version), lengths, shown)
        runs.append(shown)
    psp, psx = runs
    twins: dict[int, int] = {}
    matcher = difflib.SequenceMatcher(None, [at for at, _ in psp], [at for at, _ in psx], autojunk=False)
    for a, b, size in matcher.get_matching_blocks():
        for (_, mine), (_, theirs) in zip(psp[a:a + size], psx[b:b + size]):
            if mine is not None and theirs is not None and mine != theirs:
                twins.setdefault(mine, theirs)
    return twins


def named_line(speaker: str, text: str) -> tuple[str, str]:
    if "<cursor>" in text and not text.startswith(("<speaker=", "<hidespeaker>")):
        text = f'<speaker="{speaker}">{text}'
    return speaker, text


def twin_speakers(lines: list[tuple[str, str]], twins: dict[int, int],
                  pages: dict[int, list[int]]) -> tuple[list[tuple[str, str]], int]:
    """lines with each nameless one taking its PSX twin's speaker (script_twins), its later pages too, and a list of
    answers its <speaker> tag; and how many took one."""
    out, named = list(lines), 0
    for mine, theirs in sorted(twins.items()):
        if not (0 < mine <= len(out) and 0 < theirs <= len(out)) or out[mine - 1][0] or not out[theirs - 1][0]:
            continue
        speaker = out[theirs - 1][0]
        for k in [mine, *pages.get(mine, [])]:
            out[k - 1] = named_line(speaker, out[k - 1][1])
        named += 1
    return out, named


def parallel_speakers(lines: list[tuple[str, str]], psx: list[tuple[str, str]]) -> tuple[list[tuple[str, str]], int]:
    """Inherit each missing name from the same message of a separate PSX script."""
    if len(lines) != len(psx):
        raise ValueError("paired event scripts have different message counts")
    names: dict[str, str] = {}
    out, named = [], 0
    for (speaker, text), (source, _) in zip(lines, psx):
        if source and speaker:
            names[source] = speaker
        if source and not speaker and text:
            # Keep the PSP spelling once the character has introduced it.
            out.append(named_line(names.get(source, source), text))
            named += 1
        else:
            out.append((speaker, text))
    return out, named


def fitted_boxes(code: bytes, lengths: bytes, sizes: dict[int, int],
                 pages: dict[int, list[int]], menus: set[int] | frozenset[int] = frozenset(),
                 close_at_end: bool = True
                 ) -> tuple[bytes, int, int, int]:
    """ReMixed's event code (the PS1 layout) with its swaps to longer lines made boxes of their own (see CLOSE_BOX) and
    its long lines' later pages shown after their first (PAGE_CHARS). ReMixed kept both speakers' boxes open and
    alternated their text with ChangeDialog. The remaster clears an acknowledged box's text but leaves its frame,
    producing an empty box beside the next speaker; close the inactive speech box and reopen the active one instead.
    It can also keep a scene at its final END on a black screen while any message window remains logically open, so
    close every box the event opened before each END (closing an inactive box is a no-op in the remaster).
    Returns the numbers of resized swaps, later pages and speech-box switches. sizes: each message's length (its
    line's characters), by number; menus, the messages that are lists of answers (a swap to or from one opens its box
    again). Acknowledged notices can page in box 3 as well. For numbers written in as ReMixed runs, message_numbers
    works them out for the PSP script (SCRIPT_VERSION); where it can't, its valid template still reopens the box.
    ``close_at_end`` is false for a split battle copy: that entry jumps over every dialogue opener to the shared END,
    so emitting closes there creates an empty feather-cursor box instead of closing one."""
    out, at, openers, pending, reopened, paged, switched = bytearray(), 0, {}, [], 0, 0, 0
    menu_open: dict[int, bool] = {}  # each box: whether a list of answers holds it
    active_speech: int | None = None
    numbers = message_numbers(code, lengths)

    def close(kind: int) -> bytes:
        return bytes([0x51, kind]) + CLOSE_BOX.to_bytes(2, "little") + bytes(2)

    def pageable(opener: bytes) -> bool:
        style = opener[2]
        return style & 0x0F in SPEECH_BOXES or style & 0xF0 == NOTICE_STYLE_PLACE

    def box(opener: bytes, message: int, portrait: int) -> bytes:
        """The box closed, and its MESSAGE again with another message (and portrait row)."""
        again = bytearray(opener)
        again[3:5] = message.to_bytes(2, "little")
        again[7] = portrait
        return bytes([0x51, opener[2] & 0x0F]) + CLOSE_BOX.to_bytes(2, "little") + bytes(2) + bytes(again)

    def later_pages(kind: int, message: int, portrait: int) -> None:
        nonlocal paged
        opener, _ = openers[kind]
        for page in pages.get(message, []):
            out.extend(WAIT_FOR_MESSAGES + box(opener, page, portrait))
            paged += 1
        if pages.get(message):
            openers[kind] = (opener, sizes.get(pages[message][-1], 0))

    while at < len(code):
        start, op = at, code[at]
        ins = bytes(code[at:at + 1 + lengths[op]])
        at += len(ins)
        if op == 0xDB:  # END: no message window may survive an event's terminal cleanup
            out += b"".join(pending)
            pending = []
            # MESSAGE styles are restyled later (for example, box 2's menu becomes style 0x96), but ChangeDialog's
            # close operand remains the event window number. The remaster has the two speech windows and narration
            # window 3; close all three so conditional branches cannot leave one behind.
            if close_at_end and openers:
                for kind in (1, 2, 3):
                    out += close(kind)
            active_speech = None
            out += ins
            continue
        if op == 0xE6:  # PATCH_NEXT: kept with the instruction it patches
            pending.append(ins)
            continue
        if op == 0x10 and len(ins) == 15:
            message = numbers[start] if start in numbers else u16(ins, 3)
            kind = ins[2] & 0x0F
            if kind in SPEECH_BOXES and active_speech is not None and active_speech != kind:
                out += close(active_speech)
                switched += 1
            out += b"".join(pending) + ins
            pending = []
            openers[kind] = (ins, None if message is None else sizes.get(message, 0))
            menu_open[kind] = message in menus
            if kind in SPEECH_BOXES:
                active_speech = kind
            if message is not None and pageable(ins):
                later_pages(kind, message, ins[7])
            continue
        if op == 0x51 and len(ins) == 6 and u16(ins, 2) == CLOSE_BOX:
            out += b"".join(pending) + ins
            pending = []
            if active_speech == ins[1]:
                active_speech = None
            continue
        if (op == 0x51 and len(ins) == 6 and u16(ins, 2) != CLOSE_BOX and ins[1] in openers
                and pageable(openers[ins[1]][0]) and all(p[1] & 0x3F == 1 for p in pending)):
            opener, size = openers[ins[1]]
            message = numbers[start] if start in numbers else u16(ins, 2)
            menu_swap = message is not None and (message in menus) != menu_open.get(ins[1], False)
            speech_switch = ins[1] in SPEECH_BOXES and active_speech != ins[1]
            resize = message is not None and (menu_swap or size is not None and sizes.get(message, 0) > size)
            if speech_switch or resize:
                if speech_switch and active_speech is not None:
                    out += close(active_speech)
                again = box(opener, u16(ins, 2), ins[4])
                # the message's offset in a 0x51 (1) is 2 in a MESSAGE: its PATCH_NEXTs go right before the MESSAGE
                out += again[:6] + b"".join(bytes([0xE6, p[1] + 1]) + p[2:] for p in pending) + again[6:]
                openers[ins[1]] = (again[6:], None if message is None else sizes.get(message, 0))
                menu_open[ins[1]] = message is not None and message in menus
                if speech_switch:
                    active_speech = ins[1]
                    switched += 1
                if resize:
                    reopened += 1
            else:
                out += b"".join(pending) + ins
            pending = []
            if message is not None:
                later_pages(ins[1], message, ins[4])
            continue
        out += b"".join(pending) + ins
        pending = []
    return bytes(out), reopened, paged, switched


def remaster_message_layout(code: bytes, lengths: bytes, first_line: int | None,
                            menus: set[int] = frozenset(), tavern: bool = False,
                            boxes: dict[int, tuple[int, int, int, int]] | None = None,
                            menu_box: bool = False
                            ) -> tuple[bytes, set[int], bytes | None]:
    """ReMixed's event code in the remaster's layout (eiu_events.remaster_layout: message n as line first_line + n - 1,
    a box style the remaster draws, AlterParameters moved), with ReMixed's own box choices. With tavern, its boxes go
    in the remaster's speech windows (TAVERN_MENU_STYLE, TAVERN_BOX_STYLE); with boxes (EVENT_BOXES), a MESSAGE of one
    of its styles gets the style, place (no unit, at x, y) and centring given for it. Returns the code, the message
    numbers its MESSAGEs name, and one of them (converted, not a menu) as a template."""

    def at(x: int, y: int) -> bytes:  # a place with no unit, at x, y
        return bytes(3) + x.to_bytes(2, "little", signed=True) + y.to_bytes(2, "little", signed=True) + bytes(2)

    def restyle(style: int, placed: bytes, menu: bool) -> tuple[int, bytes, int]:
        if menu_box and menu:
            return TAVERN_MENU_STYLE, at(*TAVERN_MENU_XY), 0
        if tavern and menu:
            return TAVERN_MENU_STYLE, at(*TAVERN_MENU_XY), 0
        if tavern and style & 0x70 not in SPEECH_PLACES and style & 3 != 1:
            return TAVERN_BOX_STYLE, placed[:3] + bytes(6), 0
        if boxes and style in boxes:
            style, x, y, centred = boxes[style]
            return style, at(x, y), centred
        return style, placed, 0

    return eiu_events.remaster_layout(code, lengths, first_line, menus, restyle)


# A runtime-selected MESSAGE starts as the PS1's 0xFFFF placeholder. A plain conversion turns that placeholder into
# the remaster's 0xFFFFFFFF "no line", leaving the high half 0xFFFF after PATCH_NEXT replaces its low half;
# MoveMessage consequently cannot recognise the result as LINE_ID_BASE + message, and the box is blank. Give each
# such MESSAGE one valid template number before conversion. The runtime patch still replaces its low half, while the
# conversion now preserves the 0x0004 high half.
#
# The Completion Points room chooses four menus so; their templates also give each box its menu style and size it for
# the largest menu it shows: the hub, unit list, unit actions, then the hub again. Be A Beta randomly chooses its first
# speaker and line (messages 1-5) the same way; message 1 is enough to preserve a valid line-id high half.
DYNAMIC_MENU_PLACEHOLDERS = {0x0A3: (8, 22, 28, 8)}
# Each number only supplies a valid 0x0004 high half and a line for the remaster's pre-scan to size the box from;
# ReMixed's PATCH_NEXT replaces the low half before the instruction runs, and MoveMessage maps that message number to
# this event's actual line. 0xEA (Luso at Mandalia) has four runtime-selected lines, and 0x09D (Aliste) has four; both
# used 0xFFFF placeholders, whose converted 0xFFFFFFFF high half survived PATCH_NEXT and produced empty boxes. Their
# longest possible lines are safe sizing templates. The dispatch tavern (0x0E8) has two ChangeDialog placeholders
# left after EVENT_CODE_PATCHES gives its three opening MESSAGEs their already-established sizing lines; its longest
# ordinary report is a safe scan template. 0x0C6 randomly selects its first speaker and line.
DYNAMIC_LINE_PLACEHOLDERS = {0x0C6: (1,), 0x09D: (33, 33, 33, 33), 0x0E8: (240, 240),
                             0x0EA: (59, 59, 59, 59)}


def dynamic_message_placeholders(code: bytes, lengths: bytes, templates: tuple[int, ...],
                                 allowed: set[int] | None = None) -> tuple[bytes, int]:
    """Replace each PATCH_NEXT-selected 0xFFFF MESSAGE with a template number, for remaster conversion."""
    if not templates:
        return code, 0
    out, at, pending, made = bytearray(code), 0, [], 0
    if allowed is not None and not set(templates) <= allowed:
        raise ValueError(f"dynamic message templates {templates} aren't all allowed")
    while at < len(out):
        op = out[at]
        size = 1 + lengths[op]
        if op == 0xE6:
            pending.append(out[at + 1] & 0x3F)
        else:
            message_at, patched_at = (3, 2) if op == 0x10 else (2, 1) if op == 0x51 else (0, -1)
            if message_at and patched_at in pending and u16(out, at + message_at) == 0xFFFF:
                if made >= len(templates):
                    raise ValueError("more dynamic menu placeholders than templates")
                out[at + message_at:at + message_at + 2] = templates[made].to_bytes(2, "little")
                made += 1
            pending = []
        at += size
    return bytes(out), made


def audit_event_messages(code: bytes, ps1_lengths: bytes, first: int | None,
                         lines: list[tuple[str, str]], templated: dict[int, str]) -> tuple[int, int, int]:
    """Check every real MESSAGE in converted marked code against the text rows shipped for its event.

    A literal DisplayMessage/ChangeDialog must name one of ``lines``. A PATCH_NEXT-selected message is different: the
    event writes its message number into the line id's low half immediately before it runs, so its template must have
    the 0x0004 high half that :csharp:`RemixedScenarios.MoveMessage` recognises. ChangeDialog 0xFFFFFFFF is the PS1's
    intentional close-box operation. Returns (messages, runtime-selected messages, closes).
    """
    lengths = eiu_events.remaster_lengths(ps1_lengths)
    at, pending = 0, []
    messages = dynamic = closes = 0
    while at < len(code):
        op = code[at]
        size = 1 + lengths[op]
        if at + size > len(code):
            raise ValueError(f"converted event instruction 0x{op:02X} at 0x{at:X} runs past its code")
        if op == 0xE6:
            pending.append(code[at + 1] & 0x3F)
            at += size
            continue
        if op in (0x10, 0x51):
            field = 3 if op == 0x10 else 2
            patched = (2 if op == 0x10 else 1) in pending
            line_id = int.from_bytes(code[at + field:at + field + 4], "little")
            messages += 1
            if patched:
                dynamic += 1
                if line_id >> 16 != LINE_ID_BASE >> 16:
                    raise ValueError(f"runtime-selected MESSAGE at 0x{at:X} has template 0x{line_id:08X}, whose high "
                                     f"half isn't 0x{LINE_ID_BASE >> 16:04X}")
            elif op == 0x51 and line_id == 0xFFFFFFFF:
                closes += 1
            else:
                if first is None or not first <= line_id < first + len(lines):
                    expected = "no text rows" if first is None else f"lines {first}..{first + len(lines) - 1}"
                    raise ValueError(f"MESSAGE at 0x{at:X} names line {line_id}, outside {expected}")
                n = line_id - first + 1
                # SLOTTED events intentionally ship an empty PZD row when all of its visible content is assembled
                # from ReMixed's runtime text slots. RemixedText replaces that row from ``templated`` before it is
                # drawn (the Treasure Wheel's reward line is two message slots and no literal characters).
                # A nonempty speaker with an empty body is also visible and intentional: event 0x008 uses the
                # speaker strip itself as the "Bonus JP" heading, then changes the open box to its reward rows.
                if not any(lines[n - 1]) and not templated.get(n):
                    raise ValueError(f"MESSAGE at 0x{at:X} names empty line {n}")
        pending = []
        at += size
    return messages, dynamic, closes


# What goes between the mark and an event's code (see LINE_ID_BASE): a GOTO over a MESSAGE for each of its lines no
# MESSAGE names, to a LABEL the code doesn't use.
shadow_messages = eiu_events.shadow_messages


def remixed_event_code(test_evt: bytes, battle_new: bytes, number: int) -> bytes:
    """ReMixed's event code from TEST.EVT (eiu_events.event_code, with ReMixed's instruction lengths): up to its text,
    or with no text up to its first END (0xDB)."""
    try:
        return eiu_events.event_code(test_evt, number, eiu_events.read_lengths(battle_new))
    except ValueError:
        sys.exit(f"ReMixed's event 0x{number:X} has no END")


def route_event_graphics(code: bytes, lengths: bytes) -> tuple[bytes, int]:
    """Give ReMixed's redrawn event graphics private remaster slots.

    The remaster's own events share the original graphic numbers, so replacing those graphics in place would change
    its scenes too.  EVENT_GRAPHIC_ALIASES are slots no stock event loads; only ReMixed's marked scripts are rewritten
    to name them.  The instruction is LoadEVTCHR (0x58): slot, graphic, unknown.  A ReMixed event that loads one of
    those slots itself would get another redraw's art there, so that stops the build.
    """
    out, at, changed = bytearray(code), 0, 0
    taken = {alias: graphic for graphic, alias in EVENT_GRAPHIC_ALIASES.items()}
    while at < len(out):
        op = out[at]
        size = 1 + lengths[op]
        if at + size > len(out):
            raise ValueError(f"event instruction 0x{op:02X} at 0x{at:X} runs past its code")
        if op == 0x58 and size >= 4 and out[at + 2] in taken:
            sys.exit(f"a ReMixed event loads event graphic 0x{out[at + 2]:X} at 0x{at:X}, the slot ReMixed's "
                     f"0x{taken[out[at + 2]]:X} redraw takes (EVENT_GRAPHIC_ALIASES): give that redraw another slot")
        if op == 0x58 and size >= 4 and out[at + 2] in EVENT_GRAPHIC_ALIASES:
            out[at + 2] = EVENT_GRAPHIC_ALIASES[out[at + 2]]
            changed += 1
        at += size
    return bytes(out), changed


def remixed_presets(disc) -> bytes:
    """ReMixed's event presets (0xCE's data), outside its disc's file system after SOUND/."""
    out = bytearray()
    for n in range(REMIXED_PRESETS):
        sector = disc.user_data(REMIXED_PRESETS_SECTOR + n // 2)
        out += sector[(n & 1) * 0x400:(n & 1) * 0x400 + REMIXED_PRESET_BYTES]
    return bytes(out)


def write_remixed_events(test_evt: bytes, battle_new: bytes) -> str:
    """ReMixed's events (REMIXED_EVENTS) into the mod as the remaster's event scripts. Returns a line for the report."""
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "script" / "enhanced"
    out.mkdir(parents=True, exist_ok=True)
    done = []
    for number in REMIXED_EVENTS:
        lengths = battle_new[EVENT_LENGTHS - BATTLE_BASE:EVENT_LENGTHS - BATTLE_BASE + 256]
        raw = REMIXED_EVENT_PREFIXES.get(number, b"") + remixed_event_code(test_evt, battle_new, number)
        raw, graphics = route_event_graphics(raw, lengths)
        code, _, _ = remaster_message_layout(raw, lengths, None)
        if len(REMIXED_EVENT_MARK) + len(code) > 0x2000:
            sys.exit(f"ReMixed's event 0x{number:X} is too long for the code mod's range")
        (out / f"event{number:03d}.e").write_bytes(REMIXED_EVENT_MARK + code)
        done.append(f"event{number:03d}.e ({len(code)} bytes, {graphics} event graphic(s) rerouted)")
    # The Event Instruction Upgrade's words (its variables 0xB00-0xB7F and the text values of 0x9B-0x9E) live in
    # BATTLE.BIN's memory at 0x80165EF4, over vanilla data that's there when the file loads; the upgrade's store starts
    # as they do (in its own mod's folder).
    words = battle_new[EIU_WORDS - BATTLE_BASE : EIU_WORDS - BATTLE_BASE + EIU_WORDS_BYTES]
    EIU_DIR.mkdir(parents=True, exist_ok=True)
    (EIU_DIR / EIU_WORDS_FILE).write_bytes(words)
    done.append(f"{EIU_ID}/{EIU_WORDS_FILE} ({len(words)} bytes)")
    # Move-Find's story lock (MoveFind.cs): ReMixed's story point per map before which a panel's rare item is its common
    # one and counted as found (0x8015C100 reads it with var 0x6E), a byte for each of the first 128 maps.
    lock = battle_new[MOVE_FIND_LOCK - BATTLE_BASE : MOVE_FIND_LOCK - BATTLE_BASE + MOVE_FIND_LOCK_MAPS]
    (MOD_DIR / MOVE_FIND_LOCK_FILE).write_bytes(lock)
    done.append(f"{MOVE_FIND_LOCK_FILE} ({sum(1 for b in lock if b)} maps locked until a story point)")
    return ", ".join(done) or "none"


# ReMixed's own scenarios (docs/analysis/story-flow-plan.md), each renumbered into a scenario id the remaster leaves
# free (its table has none at 0x192-0x199 and 0x1EC-0x1FF): ReMixed's scenario (its event number) -> the remaster's.
# ReMixed's missions (its WORLD 0x8013E4BC, MISSION_BATTLES), into the free ids from 0x1EC (events 492-507: no file of
# the remaster's, no ScenarioId row); The Ball (the fete) first, as it was ported first.
REMIXED_SCENARIOS = {0xAD: 0x1EC, 0xA8: 0x1ED, 0xA9: 0x1EE, 0xAA: 0x1EF, 0xAB: 0x1F0, 0xAC: 0x1F1, 0xAE: 0x1F2,
                     0xAF: 0x1F3, 0xB0: 0x1F4, 0xB1: 0x1F5, 0xB5: 0x1F6, 0xB6: 0x1F7, 0xB7: 0x1F8, 0xB8: 0x1F9,
                     0xC2: 0x1FA, 0xC5: 0x1FB,
                     # Side events: a Lich at location 28 and Ashley's scene after it.
                     0x3F: 0x1FC, 0x40: 0x1FD,
                     # Be A Beta, the mission with ReMixed's beta testers (its entry 19), from 0x349 where the remaster has
                     # no event file or text.
                     0xC6: 0x349}
# The Treasure Wheel (0x0C3, "Loot Set"), the spin for a prize that a mission's battle hands on to once it's won (its
# vars 0x3A-0x3C, AFTER_BATTLE_VARS), from 0x368 where the remaster has no event file or text. Its twelve chests are
# units given ReMixed's own event graphic 0x6F (write_event_graphics), whose frames it sets with unit numbers past the
# groups (0x0290, 0x0202: every unit present, read so by the code mod, Patches/EventUnits.cs); which loot it offers
# comes from var 0x15, the mission's battle entry (LOOT_SET).
WHEEL = 0xC3
REMIXED_SCENARIOS |= {0xC3: 0x368}  # WHEEL (a literal: tools/port_progress.py reads these)
# The Wheel's loot set is var 0x15, which a mission's event sets to var 0x32, the battle's entry, as it ends (SET_VAR
# var 0x15, var 0x32); the Wheel picks the loot by it (a SWITCH on var 0x15: 0x19B Chocobo Defense's, and so on), and
# asks for a set ("Loot Set:") when it's none of them. The mod moves ReMixed's battle entries into free slots
# (FREE_ENTRIES), and var 0x32 names the slot, so each mission sets the entry its record names in ReMixed instead.
LOOT_SET = bytes([0xAF, 0x15, 0x00, 0x32, 0x00])
# Its boxes (remaster_message_layout's boxes: ReMixed's style -> the remaster's, a place with no unit, x and y, and the
# MESSAGE's centring byte). ReMixed shows three at once: the chests left and "Start: Cash In" across the top (style
# 0x81, box 1), the chest in front's kind or prize along the bottom (0x82, box 2), both with no frame, and its prize,
# notices and menus in a box in the middle (0x63, box 3). A battle event's layout (0x221C00) adds a speech box's x and y
# to its unit's place and then keeps it on the screen, so with no unit both went to the top left corner and covered each
# other (seen on the PC, 2026-11-12). With its centring byte at 1 it centres box 1 or 2 across the screen instead, box 1
# at the top (y 75.6 in its 1920 x 1080 UI units) and box 2 at the bottom (1004.4 less its height), then raises it by
# y. So the top line is box 1 in the top window, centred, with no tail (0x99); the prize box is the bottom window's box
# 2, centred and raised to the middle (0x9A, y 440); the line along the bottom is box 2 too, centred at the bottom (as
# a narration, 0x03, it never showed: seen on the PC). ReMixed closes that line before it shows its prize or a notice
# there, so the two take turns in box 2; its cash-in and gold-chest menus, which it shows with the line still up, close
# the line first (EVENT_CODE_INSERTS).
EVENT_BOXES = {WHEEL: {0x81: (0x99, 0, 0, 1), 0x82: (0x9A, 0, 0, 1), 0x63: (0x9A, 0, 440, 1)}}
# The tavern's report puts its gil in a small box of its own with no unit (0x0E8's style 0x61: "Earned 150 gil!"), just
# under the Tavernmaster's box and left of the screen's middle on the PS1 (work/psx/run/shots rpf-00060: its left edge
# 175 to 325 of 512 across, so about centred, 196 of 480 down): the bottom window's box 2 with no tail (0x9A), centred
# and raised to the middle as the Treasure Wheel's middle box is. As a narration (0x01) it never showed; placed with no
# unit and not centred it went to the top left corner, behind the Tavernmaster's box (seen on the PC, 2026-11-12).
EVENT_BOXES[0xE8] = {0x61: (0x9A, 0, 440, 1)}
WHEEL_CLOSE = bytes.fromhex("51 02 ff ff 00 00")  # closes its middle box, box 2 now (EVENT_CODE_INSERTS)
# The rounds of ReMixed's two missions in several rounds: each round's battle conditions (its preset) start the next
# round's event, numbered from 0x32C where the remaster has no event file or text. The Shrine Knights (0x0B1) goes on
# to 0x0B2-0x0B4, Brave Story (0x0B8) to 0x0B9-0x0C1; the last round's end hands on as a mission's does.
MISSION_ROUNDS = {0xB2: 0x32C, 0xB3: 0x32D, 0xB4: 0x32E, 0xB9: 0x331, 0xBA: 0x332, 0xBB: 0x333, 0xBC: 0x337,
                  0xBD: 0x338, 0xBE: 0x33C, 0xBF: 0x33D, 0xC0: 0x341, 0xC1: 0x342}
REMIXED_SCENARIOS |= MISSION_ROUNDS
# Each mission's first round and the rounds after it, in order. A round's units come in by its battle entry, which the
# events step by one (ADD var 0x32, 1, then 0x4C: the entry's units onto the map; the party put back at its first
# places): The Shrine Knights' round's own event as it begins, Brave Story's round's as it's won, for the next one. Their
# entries are consecutive in ReMixed (0x1A4-0x1A7, 0x1AB-0x1B4), so their slots must be too (ROUND_SLOTS): the first
# rounds took slots among the location battles (0x1DF, 0x1E3) and the rounds theirs after them (0x1EA on), so each
# mission's second round loaded another battle's units (the slot after its first's).
MISSION_ROUND_CHAINS = {0xB1: (0xB2, 0xB3, 0xB4), 0xB8: (0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xC1)}
# The rounds' events put the party back (their FUNCTION 00) by battle units 16-20, taking each whose first halfword (its
# sprite set, then its place on the field) isn't 0xFF00 for a deployed unit: the PS1's empty places are 00 FF. The map's
# reload (0x4C) has just taken the deployed units off the field (+1 0xFF), so it must stay so: reading the place alone
# (tried 2026-11-19) left the party off the field from the second round on, and the battle went to its game over or,
# with its enemies down, crashed with nobody to act. The remaster's spare copy of the party's leader in unit 20 (sprite
# set 1, battle id 0, off the field) comes on with each round too, with no sprite and its name tag in the screen's top
# left corner (GAMEPLAY_TODO.md).
# Side chapter 1-A (docs/analysis/story-flow-plan.md, "Side chapter 1-A"): ReMixed's 0x0EC-0x0FB and 0x111-0x119, all
# numbers of the remaster's own scenarios, so all move: below 0x200 where the remaster leaves numbers free (0x193-0x199,
# 0x1FE-0x1FF; it has a script at 0x192), then from 0x2E8 where it has no event file and no text (it runs events of its
# own from 0x2EE up, without scenario records). Numbers past 0x2E7 are untried in the game. 0x0EE, its first battle,
# was the pilot. Its battles were set up for ReMixed's Preparation Menu (PREPARATION_MENU).
SIDE_CHAPTER_A = {0xEC: 0x193, 0xED: 0x194, 0xEE: 0x1FE, 0xEF: 0x195, 0xF0: 0x196, 0xF1: 0x197, 0xF2: 0x198,
                  0xF3: 0x199, 0xF4: 0x1FF, 0xF5: 0x2E8, 0xF6: 0x2E9, 0xF7: 0x2EA, 0xF8: 0x1B6, 0xF9: 0x2EC, 0xFA: 0x2ED,
                  0xFB: 0x2EF, 0x111: 0x2F0, 0x112: 0x2F1, 0x113: 0x2F2, 0x114: 0x2F4, 0x115: 0x2F5, 0x116: 0x2F6,
                  0x117: 0x2F7, 0x118: 0x2FD, 0x119: 0x2FE}
REMIXED_SCENARIOS |= SIDE_CHAPTER_A
# Side events that make their units with ReMixed's 0xE0 ("create a unit", the code mod's CreateUnit), from 0x2FF (the
# remaster has no event file or text at 0x2FF-0x301 and 0x306-0x30B): Biggs and Wedge at Gariland, a scene on map
# 96. No world-map start yet: ReMixed's waits on a flag its version of 0x01D sets.
REMIXED_SCENARIOS |= {0xA4: 0x2FF}
# Balthier at Dorter (0x098: a battle, "a petty thief who wants in the Church vaults"), from 0x369 (a number the
# remaster leaves empty, below the per-event table's 0x377). Its start (REMIXED_WORLD_STARTS) waits on flag 0xB7,
# which nothing of ReMixed's sets by name: its dispatch tavern's events write flags by an offset (0x0E5, 0x0E7), likely
# a mission's reward, so it comes with the dispatch tavern. Not seen in the game yet.
REMIXED_SCENARIOS |= {0x98: 0x369}
# Zalbag at Eagrose (docs/analysis/story-flow-plan.md, "Zalbag at Eagrose"): ReMixed's own turn of chapter 4, where
# Zalbag can live. With the story at 0x33 the remaster starts its 433 there (Ramza's arrival, on through 434-440:
# Zalbaag confronts Dycedarg, the battle on map 10, Dycedarg's change, Adramelk striking Zalbaag down, Ramza alone).
# ReMixed empties those and runs its 0x06E there: the arrival, handing on to 0x06F (the confrontation and the battle;
# its conditions, preset 0x6E, run 0x10E as Dycedarg falls: the change, and Adramelk's spell on Zalbag). If Zalbag has
# Shell, Reflect or Innocent then (or is one of the player's units) it fails, and 0x10E's end goes back to 0x06E
# (through event 8's vars, chosen as it runs: AFTER_BATTLE_CHOSEN) for his thanks and his leaving to rally the Hokuten
# in secret; otherwise it kills him as in the remaster and the battle ends on the world map. Either way the story goes
# to 0x34 with what the remaster's ending sets (flags 0x269 and 0x34C, 0x3C5 and 0x3F0); Zalbag's Persons state 0x3C4
# is 2 (dead) as the remaster's, or 6 or 7 (alive). Decided 2026-11-12 (the user): ReMixed's storyline, with its
# staging and text and without the remaster's voices. The remaster's start goes to 0x06E (REMASTER_STARTS_REPLACED);
# its later scenes stay, the temple's undead Zalbag being the same in both. Not seen in the game yet.
ZALBAG_AT_EAGROSE = {0x6E: 0x36A, 0x6F: 0x373, 0x10E: 0x374}
REMIXED_SCENARIOS |= ZALBAG_AT_EAGROSE
# The chapter-3 branch after side chapter 1-A (docs/analysis/story-flow-plan.md, "Zalbag and Algus"): 0x104 at
# location 15, a battle with Ramza and Agrias (made by 0xE0) whose conditions run 0x11B, where Hirame, whom Agrias
# saved in 1-A's rescue (0x0F0), meets her again ("Thanks for saving me yet again"); 0x11B clears ReMixed's flag 0x2ED
# and hands on to 0x103 and so to ReMixed's own final battle (0x07B), as the Algus campaign's end does. ReMixed's own
# start (location 15's third entry, WORLD 0x80149A4C) is flag 0xAE = 1 and flag 0x2ED = 1, mode 1, which never holds:
# only 0x112 sets 0xAE, after the rescue, on its way where Hirame wasn't revived (the ELSE of flag 0x81: Lorne's
# prayers, Bryn's "Rest in peace"), and only 0x03A (chapter 3, story 0x22) sets 0x2ED, while 0xAE is 0; 1-A comes
# before chapter 3, so ReMixed never plays 0x104 (read 2026-11-13). The port opens it where its scenes belong, with
# Hirame saved: 0x112's way where she was revived sets 0x2ED (EVENT_CODE_INSERTS), as 0x03A would have later, so a
# rescue declined ("There's no saving her...") or 1-A not taken leaves it shut. 0x104 starts on 0x2ED set and 0xAE
# clear (0x03A's rule), the story at least 0x22 (WORLD_AT_LEAST), outside the Algus campaign; 0x11B's own clear of
# 0x2ED shuts it again, so it's taken once. 0xAE clear also keeps it shut in a save where 0.9.0's start (0xAE set,
# 0x2ED clear, its 0x11B setting 0x2ED) took it. 0x11B also clears two units' AI targets (PS1 +0x165-0x16A), which
# the port doesn't map yet: they keep their battle data's. Decided 2026-11-12 (the user: ReMixed's way, open when
# Hirame was saved); until 2026-11-13 the start waited on 0xAE set, so it opened when she died. Not seen in the game.
CHAPTER_3_BRANCH = {0x104: 0x375, 0x11B: 0x376}
REMIXED_SCENARIOS |= CHAPTER_3_BRANCH
HIRAME_LOST, CHAPTER_3_BRANCH_OPEN = 0xAE, 0x2ED
# Blank remaster CharaName rows reserved for the branch's two named guests.
HIRAME_NAME, WRIGHT_NAME = 0x36, 0x37
# The Depths of Murond, ReMixed's post-game hub: 0x0A2 welcomes the player and offers its credits, then its scenario
# record hands on to 0x0A3, the Completion Points rooms on map 116. The remaster uses both original numbers for its
# own story, so the hub takes two of the Eagrose scenes made unreachable by ZALBAG_AT_EAGROSE. Both entrances (the
# Algus campaign's 0x07C and Hirame's chapter-3 branch) already finish by handing on to 0x0A2. The hub's Leave and
# Continue+ paths return to the world map; its own return to 0x0A3 is renumbered with the rest of its code.
DEPTHS_OF_MUROND = {0xA2: 0x1B2, 0xA3: 0x1B4}
REMIXED_SCENARIOS |= DEPTHS_OF_MUROND
# Beowulf's quest (docs/analysis/story-flow-plan.md, "Beowulf's quest"): ReMixed tells the remaster's quest (the
# tavern, the colliery's four battles, the "Warning!" battle, Cancer restoring Reis) with the same flags at the same
# points, but its first colliery battle (0x091, map 30) adds dragon hunters who know Ramza for a heretic, and after Reis
# is restored its own arc follows: 0x09C (with Beowulf and Reis in the party, at location 3: Aliste's ambush for
# Bremondt's bounty, on to Lionel), 0x09D (the gate, a battle), 0x09E (Bremondt's, a battle) and 0x0A0 (Beowulf and
# Reis after it; its flag 0xB3 ends the arc). The remaster's scenes stay where the story is the same (their voices);
# 0x091 plays in the remaster's first colliery battle's place (its 468, whose start at location 8 runs it) and goes on
# to the remaster's next battle (471, REMASTER_HAND_OFFS), and the arc starts at location 3. Decided 2026-11-12 (the
# user: ReMixed's story where it differs). Not seen in the game yet. The free numbers below 0x377 are used up, so
# these take numbers of the remaster's whose events no longer run (REUSED_SCENARIO_NUMBERS).
BEOWULF_QUEST = {0x91: 0x1D4, 0x9C: 0x1D5, 0x9D: 0x1D6, 0x9E: 0x1B1, 0xA0: 0x1B3}
REMIXED_SCENARIOS |= BEOWULF_QUEST
# The remaster's scenario numbers the mod gives to ReMixed's events: ones whose own events can no longer run (433-440
# behind Zalbag at Eagrose's start, 468-470 behind the first colliery battle's, 451-455 behind the temple's). The
# remaster's text rows naming them come off (drop_reused_texts), and their EventId rows (a sortie prompt, flags) go
# back to a free number's (EVENT_ID_FREE_FROM), so the remaster's pre-battle prompts don't come up for ReMixed's events.
REUSED_SCENARIO_NUMBERS = {0x1B1, 0x1B2, 0x1B3, 0x1B4, 0x1D4, 0x1D5, 0x1D6}
EVENT_ID_FREE_FROM = 0x36A
# The temple (docs/analysis/story-flow-plan.md, "The temple's second wave"): ReMixed's 0x073 is the remaster's
# battle at the temple (its 451-455: Vormav's coffins, Zalbag undead, his plea to be killed) with a second wave of its
# own: as Zalbag falls its conditions (preset 0x72) run 0x10F, Vormav's voice raising Algus undead to fight Ramza a
# third time, on ReMixed's battle data (entry 0x177, where he waits). So the battle is ReMixed's, its whole event in
# the remaster's 451 (which the remaster's battle at map 3 queues through var 0x55) with 0x10F at 452, and its end
# goes on to the remaster's scene after it (456, REMASTER_HAND_OFFS). The remaster's voiced 452-455 go with it (the
# user's rule: ReMixed's story where it differs). 0x10F's 0x15 (clearing each unit's +0x18C state) isn't ported (in
# the EIU submodule's interpreter): passed over. Not seen in the game yet.
TEMPLE_SECOND_WAVE = {0x73: 0x1C3, 0x10F: 0x1C4}
REMIXED_SCENARIOS |= TEMPLE_SECOND_WAVE
REUSED_SCENARIO_NUMBERS |= {0x1C3, 0x1C4}
# Luso at Araguay Woods (docs/analysis/story-flow-plan.md, "Luso at Araguay Woods"): ReMixed's 0x01D is the
# remaster's Araguay Woods (its 121-127: the goblins, the chocobo saved), started the same way (location 32, story
# 0x10) and ending the same (story 0x11, flag 0x287), with Luso riding the chocobo, rescued with it, and the party's
# comments on him. Its scene comes first, then the battle (MENU_BATTLES). Its start (on the battle's run too) clears
# flags 0x93 and 0xC9 and, when Luso isn't in the party and his state (0x3F6, which Mandalia's 0x0EB sets) is under
# 12, sets both: he rides the chocobo (0xC9 includes his unit) and at the victory the battle returns to its conditions
# (preset 0x1C) for 0x107, his thanks ("It's been a couple weeks... It's been over a year", met at Mandalia or not),
# chosen as it runs (RETURN_CHOSEN), where he and Boco join. With Luso in the party the flag stays clear, the victory
# sets it and the world map follows, Boco joining at the hand-off (read 2026-11-19, when a test with Luso in the
# party and flag 0x93 set by hand took that way). So it takes the remaster's 121 (which its start runs) and 0x107 its 122; the
# remaster's voiced 122-127 go (the user's rule: ReMixed's story where it differs). ReMixed's chapter-2 battles also
# mark Agrias and Gafgarion's errand byte (3) until its 0x025 clears it; the remaster's battles around this one don't,
# so that's left out (EVENT_CODE_PATCHES). Not seen in the game yet; 0x107 waits on 0x0EA-0x0EB to be reached.
ARAGUAY_LUSO = {0x1D: 0x79, 0x107: 0x7A}
REMIXED_SCENARIOS |= ARAGUAY_LUSO
REUSED_SCENARIO_NUMBERS |= {0x79, 0x7A}
# Mirror Match (docs/analysis/story-flow-plan.md, "Mirror Match"): ReMixed's mission-list entry 17, posted after its
# final battle (MISSIONS). Famed Mimic Gogo, five Mimes (units 0x85-0x89; ReMixed draws them with vanilla's Mime, as the
# remaster does) on map 116, copies the party into its units 0x80-0x84 (its battle's units 0-4), warps onto them and
# leaves them to fight; each win makes the next copies stronger (flags 0xD0-0xD3). The free numbers below 0x377 are used
# up, so it takes the remaster's 125, an Araguay Woods scene that can't run since ARAGUAY_LUSO took the start before it.
# Its two loops over the PS1's memory go other ways in the port (EVENT_CODE_PATCHES). Not seen in the game yet.
MIRROR_MATCH = {0xC4: 0x7D}
REMIXED_SCENARIOS |= MIRROR_MATCH
REUSED_SCENARIO_NUMBERS |= {0x7D}
# Side chapter 1-A's end at Orbonne (docs/analysis/story-flow-plan.md, "Side chapter 1-A's end at Orbonne"): 1-A's
# last scenes (0x0FB, or 0x119 inside its last battle) leave the party on the world map with the story at 0xE, the way
# to Orbonne (location 18) opened at location 27 (REMIXED_WORLD_STEPS), and there 0x0FC plays (map 56: Agrias and her
# knights come to guard Ovelia, then months later the opening's day from their side, Whitney wounded), handing on to
# 0x11A (map 57: Ovelia tended, Whitney, an Ordallian sleeper agent, turning on her, Delita taking her). 0x11A gives
# back what 1-A set aside (Ramza to roster slot 0, the others no longer kept away, the world map's chapter-2 places,
# var 0x6F 5; 0x0FC dismisses Whitney and makes the other two knights Knights) and hands on to ReMixed's chapter 2, the
# remaster's 0x074 (REMASTER_HAND_OFFS). The free numbers below 0x377 are used up, so they take the remaster's 126 and
# 127, Araguay Woods scenes that can't run since ARAGUAY_LUSO took the start before them. Not seen in the game yet.
SIDE_CHAPTER_A_END = {0xFC: 0x7E, 0x11A: 0x7F}
REMIXED_SCENARIOS |= SIDE_CHAPTER_A_END
REUSED_SCENARIO_NUMBERS |= {0x7E, 0x7F}
# Luso at Mandalia (docs/analysis/story-flow-plan.md, "Luso at location 24"): ReMixed meets Luso in chapter 1, at
# location 24 (Mandalia Plains) once Biggs and Wedge's scene has set flag 0x92, with flag 0x93 clear and the story at
# 0xF or less (REMIXED_WORLD_STARTS). 0x0EA (map 85, entry 0x107) is a scene, Ramza, Delita and Algus finding a stranger
# chased by a big chocobo, then its battle (BATTLE_SPLITS: one group, formation 0x14E) with Luso as a guest; its
# victory returns to its own preset (0xE9: AFTER_BATTLE_RETURN), which runs 0x0EB, his story and his offer to come
# along (he joins, through instruction 0x78 7, unless the roster has no room), which sets flag 0x93 for 0x107 at
# Araguay Woods (ARAGUAY_LUSO). The remaster has no Luso: he keeps ReMixed's slot 0x12 (sprite set, graphic and job;
# the remaster's Marach moved off it, REMASTER_MOVED_SPRITE_SETS), so ReMixed's lookups of him work as written, with a
# name of his own (LUSO_NAME) and ReMixed's sprite of him (REMIXED_SPRITES). They take the
# remaster's 123 and 124, Araguay Woods scenes that can't run since ARAGUAY_LUSO. Decided 2026-11-12 (the user: slot
# 0x12, named Luso). Not seen in the game yet.
LUSO_AT_MANDALIA = {0xEA: 0x7B, 0xEB: 0x7C}
REMIXED_SCENARIOS |= LUSO_AT_MANDALIA
REUSED_SCENARIO_NUMBERS |= {0x7B, 0x7C}
# The Nemesis's return (docs/analysis/story-flow-plan.md, "The Nemesis's return"): ReMixed's 0x11D, the scene of its
# Nemesis System (Patches/Nemesis.cs). ReMixed reaches it from its Preparation Menu (PREPARATION_MENU), which its own
# battles run as they begin: the menu's Start Battle sets flag 0x8F and, with a Nemesis in var 0x40 that isn't disabled
# (bit 0x100000), loads preset 0x11C ("flag 0x8F = 0 -> 0x11D") and clears the flag again, so 0x11D runs at once. 0x11D
# finds the enemy with the Nemesis's name, has it speak (its name a text value: NEMESIS_SPEAKER) and Ramza answer, and
# makes it stronger by the story's section; then it sets flag 0x8F. The mod ships no menu, so the sets of ReMixed's
# battles that ran it ("flag 0x8F = 0 -> 0x192", NEMESIS_TRIGGER) run 0x11D instead, the battles that hand on to the
# menu from their opening (side chapter 1-A's, the Algus campaign's) get that set as their last (checked after their
# opening's all the same: nemesis_triggers), and the code mod sets flag 0x8F as the menu's Start Battle does
# (RemixedScenarios.StartBattle). The free numbers below 0x377 are
# used up, so it takes the remaster's 437, a scene behind Zalbag at Eagrose's start (ZALBAG_AT_EAGROSE). The remaster's
# own story battles don't load ReMixed's conditions, so they don't run it (in ReMixed its story battles do). Not seen
# in the game yet.
NEMESIS_SCENE = {0x11D: 0x1B5}
REMIXED_SCENARIOS |= NEMESIS_SCENE
REUSED_SCENARIO_NUMBERS |= {0x1B5}
# The volcano summoning scene uses the next displaced Eagrose scene. Keep the battle at 0x2EB for existing saves.
REUSED_SCENARIO_NUMBERS |= {SIDE_CHAPTER_A[0xF8]}
NEMESIS_TRIGGER = (1, 0x8F, 0)  # the set's one condition (type 1: flag 0x8F = 0), then 0x19 and the event


def tavern_menu_rows(cfg: dict, texts: dict[str, int]) -> str:
    """The Tavern's left-menu rows (TAVERN_MENU_ROWS): Dispatch Missions in Errands' place in the unlocked one, and a
    third button, Missions, in both (their UI text keys from DISPATCH_UI_TEXT)."""
    db = sqlite3.connect(nxd_sqlite(cfg, "uimenubuttonleft"))
    columns = [c for (_, c, *_) in db.execute('pragma table_info("UIMenuButtonLeft")')]
    rows = {}
    for key in TAVERN_MENU_ROWS:
        found = db.execute('select * from "UIMenuButtonLeft" where Key = ?', (key,)).fetchone()
        row = dict(zip(columns, found)) if found else {}
        labels, notes, actions = (json.loads(row.get(c) or "null") for c in ("Unknown8", "Unknown10", "Unknown18"))
        if actions != TAVERN_MENU_ACTIONS or len(labels) != 2 or len(notes) != 2:
            db.close()
            raise SystemExit(f"UIMenuButtonLeft row {key} isn't the Tavern's Rumors and Errands any more: {row}")
        if key == TAVERN_DISPATCH_ROW:
            labels[1], notes[1] = texts["title"], texts["button"]
        row.pop("Key")
        row["Unknown8"] = json.dumps(labels + [texts["missions"]], separators=(",", ":"))
        row["Unknown10"] = json.dumps(notes + [texts["missions_button"]], separators=(",", ":"))
        row["Unknown18"] = json.dumps(actions + [actions[0]], separators=(",", ":"))
        rows[key] = [row]
    db.close()
    return (replace_nxd_rows(cfg, "uimenubuttonleft", "UIMenuButtonLeft", rows)
            + " (Dispatch Missions in place of the Tavern's Errands, and Missions)")


BATTLE_OPENING = (1, 0x1FD, 1)  # a battle's first set: "var 0x1FD = 1 -> its event", its opening
# Its speaker is the Nemesis, named by a text value (word 0 = 0x4000 | the unit's battle slot, text code 0xEB): its lines
# take the remaster's speaker tag with that slot (RemixedText.Reference fills in the unit's name as a line shows), as
# the remaster's own lines name their speaker (`<speaker="Marach">` on a line whose Speaker row is "Outland Mage"), and
# this Speaker row's name where the tag isn't filled in.
NEMESIS_SPEAKER = "Nemesis"
# ReMixed's dispatch tavern, "Dispatch Missions" (docs/analysis/dispatch-missions-plan.md): its scene 0x0C7 (map 96,
# battle entry 0x1BA, music 0x21, preset 0xC6) and the events that preset runs by var 0x34 (the six kinds' lists
# 0x0DF-0x0E4, the menu 0x0E5, sending a group 0x0E6, training and contests 0x0E7, reports 0x0E8, rewards 0x0E9),
# numbered where the remaster has no event file or text. ReMixed's records for those events are blank: they ship as
# events alone, their text as the scenes' (TAVERN_EVENTS). It's posted in the taverns as ReMixed's mission-list entry 1
# (TAVERN_POSTING).
TAVERN_SCENE = 0xC7
TAVERN = {0xC7: 0x355, 0xDF: 0x356, 0xE0: 0x358, 0xE1: 0x359, 0xE2: 0x35A, 0xE3: 0x35B, 0xE4: 0x35E, 0xE5: 0x35F,
          0xE6: 0x360, 0xE7: 0x363, 0xE8: 0x364, 0xE9: 0x365}
REMIXED_SCENARIOS |= {0xC7: 0x355}  # TAVERN_SCENE (a literal: tools/port_progress.py reads these)
TAVERN_EVENTS = {theirs: ours for theirs, ours in TAVERN.items() if theirs != TAVERN_SCENE}
# The events whose text ReMixed makes as they run (SLOTS_FILE): the tavern's, and the Treasure Wheel's (the chests
# left, a chest's kind or prize, the cash-in).
SLOTTED = set(TAVERN) | {WHEEL, 0x0A3}
# The Algus campaign (docs/analysis/story-flow-plan.md, "The Algus campaign"): ReMixed's secret mode, for a Ramza named
# "Algus" or "Argath" at the start, played as Algus through ReMixed's own chapter 1, the Hokuten side story and its
# final battle; flag 0xAF marks it (vanilla's events and the remaster's world rows never touch it). Its events, numbered
# where the remaster has no event file or text.
ALGUS_FLAG = 0xAF
ALGUS_NAMES = ("Algus", "Argath")
ALGUS_CAMPAIGN = {0xFD: 0x300, 0x006: 0x301, 0x007: 0x306, 0x008: 0x307, 0x105: 0x308, 0x00A: 0x309, 0x00B: 0x30A,
                  0x00C: 0x30B, 0x00D: 0x311, 0x00E: 0x312, 0x00F: 0x313, 0x010: 0x314, 0x011: 0x315, 0x012: 0x317,
                  0x013: 0x318, 0x106: 0x319, 0xFE: 0x31A, 0xFF: 0x31D, 0x100: 0x31E, 0x101: 0x31F, 0x102: 0x323,
                  0x103: 0x324, 0x07B: 0x328, 0x07C: 0x329}
REMIXED_SCENARIOS |= ALGUS_CAMPAIGN
# Where it starts: the remaster's scenario 2 (the opening's chapel at Orbonne, ReMixed's 0x001 holds both) hands on to
# 3, its first battle; for a Ramza named one of ALGUS_NAMES it hands on to 0x0FD instead (RemixedScenarios sets the
# word as 2 starts, by roster slot 0's name).
ALGUS_TRIGGER = 2
# Maps ReMixed's scenarios name that go elsewhere in the mod: TUNNEL is ReMixed's 118, where the remaster's own map 118
# has a mesh file of 24,832 bytes, too small for TUNNEL's (57,085; the game reads no more than the file it replaces).
# Map 98 (no scenario of the remaster's, no HD look, as 97, 99 and 101) has one of 33,812, which TUNNEL's fits without
# its moving parts' animations (fitted_mesh). Map 33's (59,132) would hold it whole, but the remaster draws no built
# look there (seen on the PC: neither TUNNEL's nor one of map 33's own).
REMIXED_MAP_NUMBERS = {118: 98}
# ReMixed's Biggs and Wedge carrier is the sole imported scenario whose high map-state byte is 0xF0. That selector
# has no remaster equivalent: it leaves Gariland on a black/sky backdrop instead of drawing map 96. The remaster's
# own scenarios on map 96 use state 0 (or 0x10 for their deliberate alternate), so use the ordinary state here.
REMIXED_MAP_STATES = {0xA4: 0}
# ReMixed renumbered the main story; where its events hand on to one of its own main-story scenes, the hand-off goes to
# the remaster's scene that matches it: its 0x01B, chapter 2's start, is the remaster's 0x074 (the remaster's end of
# chapter 1, 0x05A, hands on to 0x074); its 0x01A, the end of chapter 1 at Zeakden ("I'm sorry, Delita"), is the
# remaster's 0x05A (the Algus campaign's way out, 0x102's "I can't go on (End Chapter 1)").
REMASTER_HAND_OFFS = {0x1B: 0x74, 0x1A: 0x5A}
# ReMixed's second colliery battle (0x092, queued by 0x091's end) is the remaster's 471 (map 29): BEOWULF_QUEST.
REMASTER_HAND_OFFS |= {0x92: 0x1D7}
# ReMixed's temple scene after the battle (0x074, the dying Funeral) is the remaster's 456 (map 51): TEMPLE_SECOND_WAVE.
REMASTER_HAND_OFFS |= {0x74: 0x1C8}
# A scene can leave the next event in var 0x55 (ZERO, ADD) for the world map to run as it comes back: vanilla's do it
# with 0x43 06 (0x1C2 leaves 0x1C3), the remaster's with its own 0xE9 and the scene's text key, ReMixed's with 0x43 06
# (its world code reads var 0x55 directly, WORLD 0x80068590). A scene of ReMixed's that queues one of the mod's
# scenarios hands on to it directly instead (location_scenario_code), as the Algus campaign's do, and leaves var 0x55
# at 0, so nothing of the remaster's runs from ReMixed's number there.
WORLD_QUEUE_VAR = 0x55
# The remaster's end of chapter 1 (0x05A, the explosion at Ziekden) hands on to the mod's 0x0EC (ReMixed's offer of
# 1-A, in a menu; its start) rather than to 0x074 (RemixedScenarios reads it from the file).
CHAPTER_1_END = 0x5A
# The mark that side chapter 1-A is running: ReMixed's flag 0x2EC, which 0x0EC and 0x111 set when the player takes the
# chapter (vanilla's events never name it; nor do the remaster's world-map conditions). 1-A counts its story on var 0x6E
# from 0, so the remaster's world-map starts on var 0x6E = 1-13 (chapter 1) get "flag 0x2EC = 0" and 1-A's get "= 1".
SIDE_CHAPTER_A_FLAG = 0x2EC
CHAPTER_1_PROGRESS = range(1, 14)
# Single instructions of ReMixed's events the port changes, by event: (the instruction's bytes, its replacement, of the
# same length). 0x0EC asks "Continue to Chapter Two / Play Chapter 1-A" only with flag 0x20F set, which no event script
# sets (the PS1's code does, at a chapter's end), and without it goes straight into the chapter: it always asks
# (SET_VAR var 0, flag 0x20F becomes SET var 0, 1). The question is a menu (MENU_START); declining goes on to chapter 2.
# Taking the chapter, 0x0EC (and 0x111, its near copy) makes its party join at once (Agrias and three Lionsguard
# knights, event units 0x22 and 0x84-0x86: the event's instruction 0x43 1) only for a roster size it knows in the low
# byte of var 0x17 (ReMixed's roster size, 0x11 by default; docs/analysis/world-and-menus.md); for others it beeps and
# leaves them to the end of the event, which doesn't make them join in the remaster (seen on the PC: no one joined, and
# the next battle had no units to place). The remaster's var 0x17 is no roster size: they read 0x11 (SET_VAR var 0x70,
# var 0x17 becomes SET var 0x70, 0x11).
_ROSTER_SIZE = (bytes([0xAF, 0x70, 0x00, 0x17, 0x00]), bytes([0xAE, 0x70, 0x00, 0x11, 0x00]))
# ReMixed's Event Script Version (a menu of its 0x11C): flag 0xB2, 1 for the PSP script (ReMixed's default), 0 for the
# PSX one, which some of its events also carry (message n + a count, chosen in the event's function 0xB2) or have as a
# copy (0x009 is 0x105 in the PSX script; 0x008 picks one by the flag). The remaster never sets the flag, so ReMixed's
# events read 0: the PSX script, and 0x008 went on to 0x009, which the mod doesn't ship (seen on the PC in the Algus
# campaign: on into the remaster's own scenes). The scenarios read 1, ReMixed's default and the script nearest the
# remaster's own; they all read it as SET_VAR var 0, flag 0xB2.
SCRIPT_VERSION = (bytes([0xAF, 0x00, 0x00, 0xB2, 0x00]), bytes([0xAE, 0x00, 0x00, 0x01, 0x00]))
# Three of ReMixed's scenes come as two copies, one per script, which their presets pick by the same flag: "var 0x1FD =
# 1, flag 0xB2 = 0 -> the PSX script's copy" and "... flag 0xB2 = 1 -> the PSP script's" (1-A's start, 0x0EC and 0x111;
# its 0x0F1 and 0x113; 0x048 and 0x10C, not shipped). With the flag never set they ran the PSX copies, so 1-A opened
# with "Count Orlandu" where the rest of it reads "Orlandeau". The two sets swap their flag values, so the PSP copies
# run, as SCRIPT_VERSION has the events read (the code of each pair is the same but for the preset it loads, and four
# small changes in 0x113).
SCRIPT_VERSION_SET = (1, 0x1FD, 1, 1, 0xB2)  # such a set's words before the flag's value (then 0x19, the event)
# Separate PSP and PSX scripts with matching dialogue numbers.
SCRIPT_COUNTERPARTS = {0x111: 0xEC, 0x113: 0xF1}
EVENT_CODE_PATCHES: dict[int, list[tuple[bytes, bytes]]] = {
    # The Shrine Knights' last round: its reinforcement scene brings 0x85-0x87 in, the camera on each in turn (0x1F, the
    # unit and the one to face), and the third names 0x8B, a unit the round hasn't (its first two: 0x85 on 0x85, 0x86 on
    # 0x86). On the remaster that crashed the game (TESTING: shrine-knights-rounds.txt).
    0x0B4: [(bytes.fromhex("1f 87 00 8b 00 00"), bytes.fromhex("1f 87 00 87 00 00"))],
    # Biggs and Wedge unlock the Mandalia Luso start as they leave. ReMixed's world-map code also commits Gariland
    # as the party's location; the remaster hand-off does not. Without that write, entering from Mandalia returns
    # with var 0x31 still at 24 and immediately satisfies the newly unlocked Mandalia start, chaining the wrong scene.
    0x0A4: [(bytes([0xCD, 0x80, 0x00, 0x00]),
             bytes([0xAE, 0x31, 0x00, 0x06, 0x00, 0xCD, 0x80, 0x00, 0x00]))],
    0xEC: [(bytes([0xAF, 0x00, 0x00, 0x0F, 0x02]), bytes([0xAE, 0x00, 0x00, 0x01, 0x00])), _ROSTER_SIZE,
           # ReMixed marks the four 1-A party members with roster-state bit 0x10. The remaster treats every nonzero
           # value in this byte as a dispatch errand, hiding them from formations and displaying "Days Remaining: -1".
           # Nothing in 1-A reads their 0x10 back (its ending clears all but the real errand bit), so leave them home.
           (bytes([0xAE, 0x01, 0x00, 0x10, 0x00]), bytes([0xBE, 0x01, 0x00])),
           # The chapter opens on Zeltennia itself. A world-map start at Zeltennia therefore isn't checked until the
           # party leaves and returns, letting Finath River be selected before 0x0ED recruits Bryn and Lorne. Chain
           # the taken chapter directly into 0x0ED; the earlier decline path has already handed off to chapter 2.
           # A direct remaster scenario chain is mode 0x81. ReMixed's PSP copy used 0x82 here; the remaster accepted
           # its state writes but stopped at END on a black screen instead of loading 0x0ED. Its world-map start
           # remains as an existing-save fallback.
           (bytes([0xCD, 0x80, 0x00, 0x00]), bytes([0xCD, 0x81, 0xED, 0x00]))],
    # 0x0ED makes Bryn and Lorne join, then writes their final jobs into the live units. Its man's Bard is job 0x38
    # on the PS1; the entry-data conversion cannot see this later event write, and 0x38 is a Lucavi job whose command
    # appears as Mystic Arts in the remaster. Use its Bard job (0x5B), as REMIXED_UNIT_JOBS does for entry data.
    # The same var then goes to Lorne's graphic (PS1 +0x15F), which must stay ReMixed's 0x38 (the remaster's Bard art,
    # REMIXED_SPRITE_ALIASES): graphic 0x5B is a Squire copy whose texture 0x5E's grave swords now use, so Lorne stood
    # in the scene as the swords with a Squire's face (seen on the PC, 0.9.7). His block's next instruction (SETHI var
    # 0x70, 0x20; Bryn's is 0x10, and the two POKEs are byte-for-byte hers) writes 0x38 back to the graphic first.
    # Each block then writes what its performer has learned and earned, by raw PS1 offsets: an Archer's (job index 3)
    # and, at index 18, ReMixed's slot 0x5C, a woman's Dancer and a man's Bard (Bryn: Slow Dance, 11 JP of 111;
    # Lorne: Battle Song, 61 of 161; read in the PS1's RAM after the scene, 2026-11-18). The port's man's Bard is the
    # remaster's Bard, index 17, so Lorne's three writes there (learned +0xCF, JP +0x100, total JP +0x128; the SETs
    # before them are his block's alone) go to index 17 (+0xCC, +0xFE, +0x126) and leave a man's Dancer empty: he had
    # joined with no songs (a tester on 0.9.17), the 61 JP on the Dancer.
    0x0ED: [(bytes([0xAE, 0x70, 0x00, 0x38, 0x00]),
             bytes([0xAE, 0x70, 0x00, BARD["job"][1], 0x00])),
            (bytes([0xAC, 0x70, 0x00, 0x20, 0x00]),
             bytes([0xAE, 0x70, 0x00, 0x38, 0x00, 0xC4, 0x70, 0x5F, 0x01, 0x00, 0x01, 0xAC, 0x70, 0x00, 0x20, 0x00])),
            *((bytes([0xAE, 0x70, 0x00, value, 0x00]),
               bytes([0xAE, 0x70, 0x00, value, 0x00, 0xC4, 0x70, *bard.to_bytes(2, "little"), 0x00, size, 0xBE, 0x70, 0x00]))
              for value, bard, size in ((0x10, 0xCC, 3), (0x3D, 0xFE, 2), (0xA1, 0x126, 2)))],
    # 0x0C5 (Ashley and the Lich) marks five actions of Ashley's slot 18 learned (+0xCF = 0xF8). For a man ReMixed's
    # ability lists read slot 18 for its man's Bard's songs (BATTLE 0x80152750: skillset 0x4D, job 0x38's) and slot 17
    # for the Dark Knight's (0x16, his secondary), so in ReMixed he knows five songs he has no command for; five is the
    # Dark Knight's count, but the port keeps what ReMixed does: the songs, in the port's Bard (+0xCC).
    0xC5: [(bytes([0xC4, 0x73, 0xCF, 0x00, 0x70, 0x01]), bytes([0xC4, 0x73, 0xCC, 0x00, 0x70, 0x01]))],
    # 0x0F3 joins Valmafra and Elias as guests and gives Elias a locked Black Mage look (0x9C) and roster-state bit 0x80
    # (+0xD0), which nothing of ReMixed's reads back (its formation shows him as a guest, seen on the PS1, 2026-11-18;
    # 1-A's end clears all but bit 0). The remaster takes any of that byte (+0x11E) for an errand, so Elias showed "On
    # Errand, Days Remaining: -1" and sat out the formation (a tester on 0.9.17): the write becomes a read, as 0x0EA's.
    0xF3: [(bytes([0xC4, 0x01, 0xD0, 0x00, 0x00, 0x01]), bytes([0xC1, 0x01, 0xD0, 0x00, 0x00, 0x01]))],
    # The troupe leader (0x95) and worker (0x97) are scene sprites with no face. Give their dialogue
    # the same box style as the portraitless civilian (0x93), retaining the actors and their offsets.
    0x116: [(bytes([0x10, 0x10, 0x11, message, 0, actor]) + rest,
             bytes([0x10, 0x10, 0x19, message, 0, actor]) + rest)
            for message, actor, offset in (
                (2, 0x95, "00 00 00 00"), (4, 0x95, "00 00 00 00"), (6, 0x95, "fe ff fe ff"),
                (8, 0x95, "ec ff ec ff"), (9, 0x95, "ee ff f6 ff"), (10, 0x95, "2e 00 10 00"),
                (11, 0x97, "00 00 00 00"), (12, 0x95, "00 00 00 00"))
            for rest in [bytes.fromhex("00 00 " + offset + " 00 00 03")]],
    # 0x0FE marks its man's equipped reaction, support and movement learned, by raw PS1 offsets: a Calculator's
    # (+0xCB), slot 18's (+0xD1: Face Up, a man's Bard's in ReMixed) and an Oracle's (+0xBC). The port's man's Bard is
    # the remaster's Bard, index 17 (BARD), so slot 18's goes to the Bard's third byte (+0xCE), not a man's Dancer's.
    0x0FE: [(bytes([0xC4, 0x01, 0xD1, 0x00, 0x00, 0x01]), bytes([0xC4, 0x01, 0xCE, 0x00, 0x00, 0x01]))],
    # 0x008 opens with ReMixed's after-battle switch (var 1 set: on to its block 0xFE, AFTER_BATTLE_EVENT). The mod
    # runs it only as Dycedarg's scene, from the world map, where var 1 holds whatever was left there, so its jump
    # becomes a label no code uses (a no-op of the same length).
    0x008: [(bytes([0xD1, 0xFE]), bytes([0x0E, 0x3D]))],
    # 0x112 sets flag 0xAE (Hirame lost: its way where she wasn't revived) when her unit's name id (PS1 +0x16C) is
    # ReMixed's 0x46; the port gives her unit HIRAME_NAME, so it compares with that. The chapter-3 branch's start
    # waits on 0xAE clear (CHAPTER_3_BRANCH).
    0x112: [(bytes([0xAE, 0x00, 0x00, 0x46, 0x00]),
             bytes([0xAE, 0x00, 0x00, HIRAME_NAME, 0x00]))],
    # 0x01D marks Agrias's and Gafgarion's errand byte (OR 3: roster +0xD0, the remaster's +0x11E) as ReMixed's chapter-2
    # battles do, until its 0x025 clears it; the port runs the remaster's battles around it, so they'd stay away for
    # good: the OR is 0 (ARAGUAY_LUSO).
    0x01D: [(bytes([0xBC, 0x00, 0x00, 0x03, 0x00]), bytes([0xBC, 0x00, 0x00, 0x00, 0x00]))],
    # The Depths' Reis Morph uses the upgrade's TransformUnit (0xC6), which the remaster port doesn't run. Its two
    # forms explicitly preserve stats and abilities; the event rebuilds the unit's sprite immediately afterwards and
    # writes the new job into its name record. Make the transformation itself the one material write it needs here:
    # SET var 2 to the new job, then POKE it into the selected unit's job byte (+3 from its PS1 address in var 0x70).
    # The event's following SET var 2 is deliberately left in place for its name-id write.
    0x0A3: [(bytes([0xC6, 0x70, 0x27, 0x48]),
             bytes([0xAE, 0x02, 0x00, 0x48, 0x00, 0xC4, 0x02, 0x03, 0x00, 0x70, 0x01])),
            (bytes([0xC6, 0x70, 0x43, 0x0F]),
             bytes([0xAE, 0x02, 0x00, 0x0F, 0x00, 0xC4, 0x02, 0x03, 0x00, 0x70, 0x01]))],
    # 0x0EA's opening marks Algus (Ramza in the Algus campaign) and Delita away (roster +0xD0 = 3) and Luso (0xF), so
    # ReMixed's formation leaves out the scene's guests; its Preparation Menu (0x192) and random battles' event (0x191)
    # clear those bits again, and the port ships neither, so the units would stay away for good. The remaster's
    # chapter-1 roster has none of them, so the writes find no one there anyway; each becomes a read of the byte
    # (LUSO_AT_MANDALIA).
    0x0EA: [(bytes([0xC4, 0x01, 0xD0, 0x00, 0x00, 0x01]), bytes([0xC1, 0x01, 0xD0, 0x00, 0x00, 0x01]))],
    # Mirror Match (MIRROR_MATCH) reads and writes the PS1's memory in two loops. The first puts units 16-20's innate
    # and current statuses (+0x4E, +0x58) back from vanilla's snapshot of each unit's statuses (BATTLE.BIN 0x80148580,
    # into [0x80173C78]: 0x8019F9B4, five bytes a unit from +0, and from +0x69), which the PS1 takes as a battle's
    # opening event starts (flag 0x1FD) and puts back at its end. Nothing changes them in between, and the remaster
    # keeps no such copy, so its units hold those statuses as they are: the loop is passed over (LABEL 02 becomes a
    # GOTO past its GOTO 02). The second copies each party unit (by its event id, vars 0x7A-0x7E) over units 0-4 by
    # raw PS1 offsets (its sprite set, job, +0x09-0x160 a byte at a time and a few fields after), the copy Gogo makes;
    # the remaster's units are laid out wider (equipment as halfwords, 22 jobs' abilities, the name as UTF-8), so the
    # code mod copies them in its layout (RemixedEvents.MirrorCopy, a routine of its own, 0xCF 0x000000C4: in place
    # of SET var 0x75, 0x80, which nothing reads) and the loop is passed over (LABEL 03, GOTO 03). Its writes of the
    # copies' Max HP to ReMixed's own table at 0x8019FD7A, which no code reads, go to the upgrade's store. The labels
    # are ones nothing else takes: a GOTO finds the first from the code's start, where the upgrade's mark is LABEL 0x2C,
    # LABEL 0x3C, and the shadow messages take the highest free one.
    0xC4: [(bytes([0x0E, 0x02]), bytes([0x0D, 0x4C])), (bytes([0x0D, 0x02]), bytes([0x0E, 0x4C])),
           (bytes([0xAE, 0x75, 0x00, 0x80, 0x00]), bytes([0xCF, 0xC4, 0x00, 0x00, 0x00])),
           (bytes([0x0E, 0x03]), bytes([0x0D, 0x4D])), (bytes([0x0D, 0x03]), bytes([0x0E, 0x4D]))],
    # The tavern's sending (0x0E6): its FUNCTION 04 shows or hides a unit's sprite by walking the PS1's sprite objects
    # (0x800B7308, 0x440 bytes each, +0x134 the unit) to the one that points at the unit, with nothing to end the walk;
    # in the port those addresses read 0, so it never ended. Its step back (GOTO 18, the only one) becomes a label no
    # code uses: the unit's sprite stays as it is.
    # Its picker shows a unit's portrait in a box of its own (box 1: MESSAGE 0x99, message 3, empty, at unit 0x95, the
    # unit picked) beside the unit's card (box 3: MESSAGE 0x9B, its message written in from var 0x54 as it runs, then
    # put in again with 0x51s as the picker moves). The remaster's speech box holds a portrait and text side by side,
    # and its box 3 shares the bottom window with box 2 (the mission's needs), so the card goes in box 1 with the
    # unit's portrait (style 0x99, the top window, with no tail: a speech box's tail would point at where the unit
    # stands): the portrait box's MESSAGE and closes become no-ops (ADD var 0, 0; labels no code uses), and the card's
    # 0x51s go to box 1 (docs/analysis/dispatch-missions-plan.md, "The tavern's look"). The card goes at the top of the
    # screen, as on the PS1 (dmu-00300, where it covers the Tavernmaster but his hands and leaves Ramza in view): a box
    # goes at its unit, plus the MESSAGE's x and less its y, then is kept on the screen (0x221C00), so x -388 centres
    # it about where the PS1's card is and y 300 takes it to the top (as the portrait box was, -70 and -14, it stood
    # right of the middle over the counter, hiding Ramza). Not seen in the game yet (2026-11-12).
    0xE6: [(bytes([0x0D, 0x18]), bytes([0x0E, 0x3E])),
           (bytes.fromhex("10 10 99 03 00 95 00 00 ba ff f2 ff 00 00 03"), bytes.fromhex("b0 00 00 00 00" * 3)),
           (bytes.fromhex("10 10 9b 25 00 90 00 09 2c 00 b0 ff 00 00 03"),
            bytes.fromhex("10 10 99 25 00 95 00 00 7c fe 2c 01 00 00 03")),
           (bytes.fromhex("10 10 9b 02 00 90 00 09 2c 00 b0 ff 00 00 03"),
            bytes.fromhex("10 10 99 02 00 95 00 00 7c fe 2c 01 00 00 03")),
           (bytes.fromhex("51 01 ff ff 00 00"), bytes.fromhex("0e 3f" * 3)),
           (bytes.fromhex("51 03 ff ff 00 00"), bytes.fromhex("51 01 ff ff 00 00")),
           (bytes.fromhex("51 03 25 00 00 00"), bytes.fromhex("51 01 25 00 00 00")),
           (bytes.fromhex("51 03 02 00 00 00"), bytes.fromhex("51 01 02 00 00 00")),
           (bytes.fromhex("51 03 02 00 08 00"), bytes.fromhex("51 01 02 00 08 00"))],
    # The tavern's reports (0x0E8) and rewards (0x0E9) show lines picked as they run: each MESSAGE names message 0xFFFF
    # and a PATCH writes the real number in. The remaster sizes a box by the line its MESSAGE names as the event loads
    # (its group), and a line with no group gets a box of no width, so those boxes opened as a face alone and showed no
    # text (seen on the PC, 2026-11-12: "This is what your units earned...", "Earned N gil!" and the unit's "Squire JP:
    # +70" all missing). Each names a line of the size the PS1 shows instead (work/psx/run/shots rpf-*): the
    # Tavernmaster's report as his three-row lines (message 178, "This is what your units earned from ..."), the gil in
    # a one-row box (180, "Earned N gil!"), and a unit's rewards in a speech box of three rows (0x0E9's message 1). On
    # the PS1 that box stands at the top of the screen, as the unit picker's card does, its middle about x 946 of the
    # remaster's 1920 (rpf-00240), so it goes 300 up and 310 left of where it stood over the counter (seen on the PC).
    # The menu (0x0E5) has one too, the Tavernmaster's line as it comes back to a list (var 0x38's message): one of
    # his three-row lines (message 2, "Quite rewarding, but they have preferences...").
    0xE5: [(bytes.fromhex("10 10 91 ff ff 90 00 05 00 00 ec ff f2 ff 03"),
            bytes.fromhex("10 10 91 02 00 90 00 05 00 00 ec ff f2 ff 03"))],
    0xE8: [(bytes.fromhex("10 10 91 ff ff 90 00 05 00 00 ec ff f2 ff 03"),
            bytes.fromhex("10 10 91 b2 00 90 00 05 00 00 ec ff f2 ff 03")),
           (bytes.fromhex("10 10 61 ff ff 00 00 00 00 00 4a 00 00 00 03"),
            bytes.fromhex("10 10 61 b4 00 00 00 00 00 00 4a 00 00 00 03"))],
    0xE9: [(bytes.fromhex("10 10 99 ff ff 98 00 00 00 00 00 00 00 00 03"),
            bytes.fromhex("10 10 99 01 00 98 00 00 ca fe 2c 01 00 00 03")),
           (bytes.fromhex("10 10 9b ff ff 99 00 00 00 00 04 00 00 00 03"),
            bytes.fromhex("10 10 9b 01 00 99 00 00 00 00 04 00 00 00 03")),
           (bytes.fromhex("10 10 9a ff ff 9a 00 00 00 00 00 00 00 00 03"),
            bytes.fromhex("10 10 9a 01 00 9a 00 00 00 00 00 00 00 00 03"))],
    # Start (0x0800) is Pause/scene skip in the remaster. Triangle is already the Wheel's paid-upgrade button and has
    # no scene-level action, so it safely takes cash-in too.
    WHEEL: [(bytes.fromhex("06 00 08 9d 00 07 00"), bytes.fromhex("06 10 00 9d 00 07 00"))],
    # The Nemesis's return (NEMESIS_SCENE) gives the Nemesis a secondary skillset by its weapon's type; ReMixed's 0x16
    # (for a katana or an axe) is the remaster's 0xE0, as the battle data's units have it (REMIXED_UNIT_SKILLSETS).
    0x11D: [(bytes([0xAE, 0x18, 0x00, 0x16, 0x00]), bytes([0xAE, 0x18, 0x00, 0xE0, 0x00]))]}
# Runs of whole instructions the port leaves out of ReMixed's events, by event. 0x11D opens by taking 999 off every unit's
# CT (ChangeStats 0x83 on 0x0204, stat 2), undoing its Preparation Menu's 100, which the menu gives everyone so that
# the battle's conditions are checked at once and 0x11D runs before any turn; the mod has no menu, so it would take a
# turn's CT from whoever's turn it is (the conditions are checked as a turn comes): left out, as nothing gave the 100.
# It ends by loading the battle's own conditions back (0xE6 40 01, 0xCE: preset var 0x3E - 1), which the menu had
# replaced with 0x11C's; in the mod they're never replaced, and var 0x3E isn't the menu's note: left out.
# The Treasure Wheel's unknown-loot-set DEFAULT opens message 10, an actually empty line, then waits for it before
# returning to its picker. That produced a real empty box in the remaster (unlike the unreachable shadow messages
# inserted only to size boxes). Leave out both the empty display and its message wait; the branch still does its
# camera/reveal setup and returns to label 0, where the player can choose a loot set.
EVENT_CODE_DROPS: dict[int, list[bytes | tuple[bytes, int]]] = {
    WHEEL: [(bytes.fromhex("10 10 82 0a 00 90 00 00 00 00 5c 00 00 00 00 e5 01 00"), 2)],
    # At the river camp, each chocobo's UnitAddress puts its address in var 0, but the next SET_VAR replaced it with
    # the chosen reply's message number before the event read and changed the chocobo's teams. Leave the address for
    # the IF and the +5/+0x1BA writes. The same stray instruction is in ReMixed's PS1 event; the dialogue and the
    # surrounding team-change loop make its intended result unambiguous.
    0x0EE: [bytes.fromhex("af 00 00 78 00")],
    # The PS1 waits for input after the victory sound. The remaster's Victory screen already waits for
    # input, so drop the earlier Pause that would leave the battlefield onscreen with no prompt.
    0x115: [bytes([0x16])],
    0x11D: [bytes.fromhex("83 04 02 02 19 fc"), bytes.fromhex("e6 40 01 ce ff ff")]}
# Blocks of ReMixed's events the port moves, by event: (the block's first instruction, the instruction after its last,
# the instruction it goes before), each as the start of its bytes. 0x0EC asks its question before its scene fades in
# (op 0x4D), where the PS1 drew its boxes over the black screen with no unit on the map yet; the remaster draws a box at
# a unit (QUESTION_BOXES), so the question (from the replay check, SET_VAR var 0, flag 0x1FC, to the scene's start, SET
# var 0x31, 5; its labels are its own) goes after the fade-in and the camera's first move, with Orlandu on the map,
# before Agrias's story (its narration, message 0x1C). Seen on the PC both ways.
EVENT_CODE_MOVES: dict[int, tuple[bytes, bytes, bytes]] = {
    0xEC: (bytes([0xAF, 0x00, 0x00, 0xFC, 0x01]), bytes([0xAE, 0x31, 0x00, 0x05, 0x00]),
           bytes([0x10, 0x10, 0x0B, 0x1C, 0x00, 0x01])),
    # 1-A's last battle, a child falling (0x119's blocks 2 and 3): ReMixed patches the fallen child in as the speaker
    # of its cry (PATCH_NEXT 04, var 0x70) before the script-version pick, so the patch lands on that pick's first
    # instruction (SET var 0x71, 1: four parameter bytes) and overwrites the opcode after it. The PS1's code happens
    # to fall back into step two instructions on (the cry then comes from unit 0x80, a goblin); the port's doesn't
    # (its pick reads a constant, SCRIPT_VERSION): the block ran to the event's END without loading the failure's
    # conditions, the child's fall ran it again, and the battle hung there for good (a tester on the Deck,
    # 2026-11-22: a black screen; seen in the hidden PC game as event 766 loaded every 16 s). The patch goes with the
    # MESSAGE's other one, so the child speaks. No other PATCH_NEXT in ReMixed's events misses its instruction
    # (patch_next_targets checks each).
    0x119: (bytes([0xE6, 0x04, 0x70]), bytes([0xAE, 0x71, 0x00, 0x01, 0x00]), bytes([0xE6, 0x42, 0x71]))}


def patch_next_targets(code: bytes, lengths: bytes, event: int) -> None:
    """Stop the build on a PATCH_NEXT (0xE6) that writes past the parameters of the instruction it patches (the next
    one that isn't a PATCH_NEXT): it would overwrite the instruction after, as 0x119's did (EVENT_CODE_MOVES)."""
    at = 0
    while at < len(code):
        op = code[at]
        if op == 0xE6:
            target = at + 3
            while target < len(code) and code[target] == 0xE6:
                target += 3
            if target < len(code) and (code[at + 1] & 0x3F) + (code[at + 1] >> 6) + 1 > lengths[code[target]]:
                sys.exit(f"ReMixed's event 0x{event:X}: the PATCH_NEXT at 0x{at:X} writes past instruction "
                         f"0x{code[target]:02X}'s parameters (patch_next_targets)")
        if op == 0xDB:  # END: nothing after it is code
            break
        at += 1 + lengths[op]


# A PATCH_NEXT aimed at the value of a SET of its own variable (PATCH_NEXT 42 71, then SET var 0x71, n). 0x0F7's
# closing after its first battle (its LABEL 01, Agrias's "We ought to seek out a more secluded venue.") works its
# message number out twice, the PATCH_NEXT of the first copy left in front of the second: the second SET then keeps
# the number the first worked out, and the second ADD puts the PSP script's offset on it again. In ReMixed's PSX script
# (flag 0xB2 clear) the offset is 0 and both copies give 7; in the PSP script, which the port shows (SCRIPT_VERSION),
# 7 + 0x18 became 55, past the event's 54 lines: the box opened with Agrias's face and no line (a tester, 2026-11-25:
# "the finishing dialogue" of 1-A's volcano battle on map 75, a blank box with Agrias). ReMixed's own PSP script has
# the same slip. Such a PATCH_NEXT is left out: the SET then sets its number, as the PSX script's run of it does. Only
# 0x0F7 (and its private copy, AFTER_SPLITS) has one; check_event_dialogue.py stops on one in any shipped event.
def self_patched_sets(code: bytes, lengths: bytes) -> tuple[bytes, int]:
    """ReMixed's event code (the PS1 layout) without each PATCH_NEXT (0xE6) that writes its variable into the value of
    a SET (0xAE) of that same variable, and how many were left out."""
    out, at, dropped = bytearray(), 0, 0
    while at < len(code):
        op = code[at]
        end = at + 1 + lengths[op]
        if op == 0xE6:
            target = end
            while target < len(code) and code[target] == 0xE6:
                target += 1 + lengths[0xE6]
            if target + 3 <= len(code) and code[target] == SET_VALUE and code[at + 1] & 0x3F == 2 \
                    and u16(code, target + 1) == code[at + 2]:
                dropped += 1
                at = end
                continue
        out += code[at:end]
        if op == 0xDB:  # END: nothing after it is code
            out += code[end:]
            break
        at = end
    return bytes(out), dropped


def drop_code(code: bytes, lengths: bytes, drops: list[bytes | tuple[bytes, int]] | None) -> bytes:
    """ReMixed's event code (the PS1 layout) without the runs of whole instructions EVENT_CODE_DROPS names.

    A bytes entry must occur once; ``(bytes, count)`` explicitly documents a repeated identical run. Every occurrence
    must begin and end on instruction boundaries.
    """
    out = bytes(code)
    for drop in drops or []:
        starts, at = [], 0
        while at < len(out):
            starts.append(at)
            at += 1 + lengths[out[at]]
        run, expected = drop if isinstance(drop, tuple) else (drop, 1)
        found = [k for k in starts if out[k:k + len(run)] == run]
        if len(found) != expected:
            sys.exit(f"EVENT_CODE_DROPS: {run.hex(' ')} found {len(found)} times, expected {expected}")
        if any(k + len(run) not in starts and k + len(run) != len(out) for k in found):
            sys.exit(f"EVENT_CODE_DROPS: {run.hex(' ')} doesn't end at an instruction")
        for k in reversed(found):
            out = out[:k] + out[k + len(run):]
        starts = [s if s < k else s - len(run) for s in starts if not k <= s < k + len(run)]
    return out


# The set is added as the preset's last: ReMixed's events pick their phase block by the number of the set that ran them
# (var 1, RemixedScenarios.WalkSets), so its own sets keep theirs. Put in second (after the opening's) from 2026-11-12,
# it moved each later set on by one, and every battle given it ran the wrong blocks (seen on the PC, 2026-11-12: Hirame
# revived ran 0x112's victory block, which turns her to crystal, then its post-battle run came up as the block of her
# crystal and ran again and again; Luso's 0x0EA and the Argath campaign's 0x006 the same). The code mod's walker checks
# that set second all the same, as ReMixed's menu ran before the battle's own sets: 0x11D clears flags 0x80-0x8E, which
# those sets keep their state in (RemixedScenarios.WalkOrder).
def nemesis_triggers(presets: bytes, records: dict[int, bytes],
                     code: Callable[[int], bytes]) -> tuple[bytes, int, int, list[int]]:
    """ReMixed's presets (renumbered: renumber_presets left the sets that ran its Preparation Menu unable to fire) with
    those sets running the Nemesis's return (NEMESIS_SCENE) as the menu would have, and the set added last in the
    presets of the shipped battles that hand on to the menu from their opening instead (code: ReMixed's event's code).
    Returns them, the number of sets turned and added, and the events whose preset has no room for one."""
    out = bytearray(presets)
    full: list[int] = []
    ours = NEMESIS_SCENE[0x11D]
    menu_set = [*NEMESIS_TRIGGER[:2], 0xFFFF, 0x19, PREPARATION_MENU]
    turned = 0
    for n in range(REMIXED_PRESETS):
        base = n * REMIXED_PRESET_BYTES
        for start, end, event in remixed_preset_targets(out[base:base + REMIXED_PRESET_BYTES]):
            at = base + REMIXED_PRESET_OFFSETS + 2 * start
            if [u16(out, at + 2 * k) for k in range(end - start)] == menu_set:
                out[at:at + 10] = struct.pack("<5H", *NEMESIS_TRIGGER, 0x19, ours)
                turned += 1
    added = 0
    hand_off = bytes([0xCD, 0x81, PREPARATION_MENU & 0xFF, PREPARATION_MENU >> 8])
    for theirs in sorted(set(REMIXED_SCENARIOS) - set(NEMESIS_SCENE)):
        if theirs not in records or hand_off not in code(theirs):
            continue
        n = u16(records[theirs], 0x16)
        base = n * REMIXED_PRESET_BYTES
        preset = out[base:base + REMIXED_PRESET_BYTES]
        sets = remixed_preset_targets(preset)
        words = [u16(preset, REMIXED_PRESET_OFFSETS + 2 * k) for k in range((REMIXED_PRESET_BYTES - REMIXED_PRESET_OFFSETS) // 2)]
        if not sets or any(tuple(words[s:s + 3]) == NEMESIS_TRIGGER and e == ours for s, _, e in sets):
            continue
        offsets = [u16(preset, 2 * k) for k in range(16)]
        count = next((k for k in range(1, 16) if not offsets[k]), 16)
        used = max((k for k, w in enumerate(words) if w), default=-1) + 1
        if count >= 16 or used + 5 > len(words):
            full.append(theirs)  # all 16 sets taken: that battle goes without the Nemesis's return
            continue
        first = offsets[0] // 2
        if tuple(words[first:first + 3]) != BATTLE_OPENING:  # the walker checks it after the opening's set
            sys.exit(f"ReMixed's 0x{theirs:X}: its preset 0x{n:X} doesn't open with its opening's set (NEMESIS_SCENE)")
        offsets[count] = 2 * used  # the last set: the others keep ReMixed's numbers
        out[base:base + 32] = struct.pack("<16H", *offsets[:16])
        at = base + REMIXED_PRESET_OFFSETS + 2 * used
        out[at:at + 10] = struct.pack("<5H", *NEMESIS_TRIGGER, 0x19, ours)
        added += 1
    return bytes(out), turned, added, full


def move_block(code: bytes, lengths: bytes, move: tuple[bytes, bytes, bytes] | None) -> bytes:
    """ReMixed's event code (the PS1 layout) with a block of its instructions moved later (EVENT_CODE_MOVES)."""
    if move is None:
        return code
    first, after, before = move
    starts, at = [], 0
    while at < len(code):
        starts.append(at)
        at += 1 + lengths[code[at]]

    def find(start: bytes, since: int) -> int:
        return next((k for k in starts if k >= since and code[k:k + len(start)] == start), -1)
    a = find(first, 0)  # the first of them
    b = find(after, a + 1) if a >= 0 else -1
    c = find(before, b) if b >= 0 else -1
    if c < 0:
        sys.exit("a block of ReMixed's event code to move isn't where the build expects it")
    return code[:a] + code[b:c] + code[a:b] + code[c:]


# 0x0EC's question in boxes the remaster draws. ReMixed shows its explanation (message 26) and its menu (27) at once,
# in two boxes it placed on the screen itself with no unit (the only such boxes in its shipped events); the remaster
# draws a box at a unit (two of its own thousands of MESSAGEs name none, of another type) and drew neither (seen on the
# PC: the menu took its answer unseen, before and after the fade-in, with no unit or Orlandu's, as a centred box or
# not). As one speech box at Orlandu the explanation and the answers overflowed it (it holds three lines). So the
# explanation shows first, as the event's own narration does (message 0x1C: style 0x0B, unit 1, seen centred), the
# event waiting on it (0xE5 1), then the menu with the answers alone, in a speech box at Orlandu (0x0B, on the screen
# once the question comes after the fade-in) styled as Agrias's menus are (0x92, a menu: 0x96, seen working).
# Event: (the explanation's message, the menu's, the menu's unit).
QUESTION_BOXES = {0xEC: (26, 27, 0x0B)}
NARRATION_STYLE, NARRATION_UNIT, MENU_BOX_STYLE = 0x0B, 0x01, 0x92
WAIT_FOR_MESSAGES = bytes([0xE5, 0x01, 0x00])
# One-shot notices that ReMixed placed in the narration layer. The remaster completes that layer when its text finishes
# animating, so an E5 1 after it does not hold for a confirm press. Give these messages private temporary styles while
# preserving each message's original box number: the source event closes that same box after the acknowledgement. A
# single box-2 style left 0x0A4's original close of box 3 closing the wrong window, so its following E5 1 waited forever
# with the Rendezvous-unlocked notice still open. remaster_message_layout places the private styles in centred speech
# windows; fitted_boxes pages the long 1-A prologue in its original box 3. The Treasure Wheel's persistent
# frameless labels remain unchanged.
# 0x0A4's notice (message 19, the Rendezvous unlock) is timed in ReMixed: 400 frames (F1 0x190), then a wait for its box
# (E5 1), then its close (51 3). In the speech window above, that wait needs a press, which Auto-proceed never gives:
# the scene sat on the notice with Auto-proceed on (a player's report on the private repo, 2026-11-17; seen on the PC
# 2026-11-18). So its box closes when the 400 frames are up and the wait comes after: the notice shows as long as
# ReMixed's does, a press during it still closes it, and nothing waits on a press. Event: the three instructions.
TIMED_NOTICES = {0xA4: (bytes.fromhex("F1 90 01"), bytes.fromhex("E5 01 00"), bytes.fromhex("51 03 FF FF 00 00"))}
NOTICE_STYLE_PLACE = 0xF0
# The same holds for every line of ReMixed's in the narration layer that ends on a press (its FE) with the event waiting
# on it (E5 1, at once or after a few steps), checked in all the shipped events (2026-11-26): 1-A's prologue (0x0EC and
# 0x111, its copy in the PSP script, which is the one that runs), the Stone's four lines at Eagrose (0x10E: "Holder of
# the Holy Stone...", style 9, the voice with no box; a tester on 0.9.39: "some on screen text that SPEEDS by") and
# the Argath campaign's opening narration (0x0FD's "During the Fifty Year War..."). ReMixed's timed narration (its FF:
# the Argath campaign's ending, 0x103's 12, and its credits, 0x0A2) would need a hold instead, as 0x0A4's has: open in
# docs/GAMEPLAY_TODO.md.
NOTICE_MESSAGES = {0xA4: {19}, 0xEC: {26, 28}, 0x111: {26, 28}, 0x10E: {2, 4, 8, 11}, 0x0FD: {18}}
NOTICE_BOX_PLACE = 0x98
for _event in NOTICE_MESSAGES:
    EVENT_BOXES.setdefault(_event, {}).update({NOTICE_STYLE_PLACE | box:
                                               (NOTICE_BOX_PLACE | box, 0, 440, 1)
                                               for box in SPEECH_BOXES + (3,)})
# Instructions the port adds to ReMixed's events, by event: (an instruction, what goes after it). The remaster's
# display state 8 is its event save prompt. Put it immediately before the 1-A / chapter-2 question so the player can
# preserve the completed chapter-1 state before choosing a route; state 0 closes the display cleanly before the menu.
# Declining 0x0EC's
# offer happens after its scene has faded in now, and its end then stayed on the scene (seen on the PC), so the decline
# (its LABEL 04) fades out first as the chapter's own opening does at its end: RemoveDarkScreen (0x77), its fade (0x3E
# 02 ... 80) and a wait (0xF1 0x80).
EVENT_CODE_INSERTS: dict[int, list[tuple[bytes, bytes]]] = {
    0xEC: [(bytes([0xAF, 0x00, 0x00, 0xFC, 0x01]),
            bytes.fromhex("78 08 3c e5 38 00 78 00 3c e5 38 00")),
           (bytes([0x0E, 0x04]), bytes([0x77, 0x3E, 0x02, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x80, 0x00,
                                        0xF1, 0x80, 0x00]))],
    # The Algus campaign's way out (0x102, Algus giving up: on to the remaster's end of chapter 1, REMASTER_HAND_OFFS)
    # clears its flag: the remaster's world starts wait for flag 0xAF to be 0 (write_world_starts).
    0x102: [(bytes([0xCD, 0x81, 0x5A, 0x00]), bytes([0xAE, 0xAF, 0x00, 0x00, 0x00]))],
    # 0x112's post-battle scene, on its way where Hirame was revived (SET_VAR var 0, flag 0x81; IF; its first line's
    # number, SET var 0x71, 0xB), opens the chapter-3 branch: SET flag 0x2ED, 1, as ReMixed's 0x03A would in chapter 3
    # (CHAPTER_3_BRANCH). The first SET_VAR of flag 0x81 and IF in 0x112 (its scene's set-up) is followed by another
    # instruction, so the insert goes in the post-battle one.
    # The post-battle scene copies Bryn's and Lorne's graphics from roster records by name. If either is missing,
    # the copied value stays zero and its scene actor draws as a different character. The scene also opens its first
    # dialogue after a colour fade without revealing the map, as Grog Hill's closing did.
    0x112: [(bytes.fromhex("0e 01"), bytes.fromhex("ae 79 00 83 00 ae 7a 00 38 00")),
            (bytes.fromhex("3e 02 ff ff ff 00 00 00 80 00"), bytes.fromhex("4d 1e f1 1e 00")),
            (bytes([0xAF, 0x00, 0x00, 0x81, 0x00, 0x01, 0xAE, 0x71, 0x00, 0x0B, 0x00]),
             bytes([0xAE, CHAPTER_3_BRANCH_OPEN & 0xFF, CHAPTER_3_BRANCH_OPEN >> 8, 0x01, 0x00]))],
    # Grog Hill's battle entry reveals the map before formation, but its later 0x114 runs still open their dialogue
    # over black: Agrias's turn (labels 03/04) and Bryn's post-battle scene (label 02, copied as 0x353).
    # Reveal in each run, after the scene's own colour fade starts for label 02, so its map is drawn behind the boxes.
    # Label 02 reads the scene actors' graphics from battle ids 0x9A/0x9B, which are absent after a resumed battle.
    # Default to Bryn's Dancer and Lorne's Bard; a present source actor still replaces the default.
    0x114: [(bytes.fromhex("0e 02"), bytes.fromhex("ae 79 00 83 00 ae 7a 00 38 00")),
            (bytes.fromhex("3e 02 ff ff ff 00 00 00 80 00"), bytes.fromhex("4d 1e f1 1e 00")),
            (bytes.fromhex("0e 03"), bytes.fromhex("4d 1e f1 1e 00")),
            (bytes.fromhex("0e 04"), bytes.fromhex("4d 1e f1 1e 00"))],
    # The tavern's scene (0x0C7) reads flag 0x2D0 first: set, it came from the world map (ReMixed's mission list) and
    # Exit Tavern goes back there; clear, it came from the Preparation Menu (var 0x3E = 7) and goes back to the battle.
    # Nothing in ReMixed's events sets it. The mod reaches the tavern from the world map only (a posting), so the scene
    # sets it after loading its preset (SET flag 0x2D0, 1).
    TAVERN_SCENE: [(bytes([0xCE, 0xC6, 0x00]), bytes([0xAE, 0xD0, 0x02, 0x01, 0x00]))],
    # The tavern's unit picker (0x0E6): L1 and R1 step through the roster, and its card's text is swapped in (0x51)
    # before the unit it shows the face of (0x95) is added again with the next unit's look (LABEL 12: CALL 02). The PS1
    # draws a box's face from its unit as it is; the remaster takes it once, as the box opens, so the card kept the
    # first unit's face beside the others' names (seen on the PC, 2026-11-12: Rachel's face by "3 Tony ... Geomancer").
    # So after CALL 02 the card's box opens again at the unit (WaitAddUnit, its close, then its MESSAGE as EVENT_CODE_
    # PATCHES has it, the message number written in from var 0x54 as the first one is).
    0xE6: [(bytes.fromhex("0e 12 0c 02"),
            bytes.fromhex("48 51 01 ff ff 00 00 e6 42 54 10 10 99 25 00 95 00 00 7c fe 2c 01 00 00 03"))],
    # The report (0x0E8) puts "This is what your units earned from ..." in the Tavernmaster's box while that box still
    # holds "You've got 1 completed mission..." (box 1): with a MESSAGE when var 0x71 is set, else by swapping it in
    # (0x51, its number written in as it runs). The PS1 shows the new line there either way; in the port neither did
    # (seen on the PC, 2026-11-12: the swap was the way taken, and the line stayed). So box 1 closes and the MESSAGE
    # opens it again whichever way (v0 = 1 after v0 = v71, the only place they follow two ENDIFs).
    # Its gil box (style 0x61, the port's box 2: EVENT_BOXES) goes when ReMixed closes the Tavernmaster's box before
    # the rewards (its SWITCH's DEFAULT: BOX 01 close, the press, on to 0x0E9): on the PS1 both are gone there
    # (rpf-00120); left open, the report stayed on its END with it up and the rewards never came (seen on the PC).
    0xE8: [(bytes.fromhex("04 04 af 00 00 71 00 01"), bytes.fromhex("51 01 ff ff 00 00")),
           (bytes.fromhex("04 04 af 00 00 71 00"), bytes.fromhex("ae 00 00 01 00")),
           (bytes.fromhex("07 51 01 ff ff 00 00"), bytes.fromhex("51 02 ff ff 00 00"))],
    # The Treasure Wheel's middle box (its prize and notices: box 3 on the PS1, which closes it as the player reads on)
    # stayed open in the remaster once read, and at the end it stayed over the black screen and the scene never handed
    # back to the world map (seen on the PC, 2026-11-12). So it closes (0x51, box 2 now: EVENT_BOXES) after the wait for
    # the "Chapter 4 Feature" notice (message 9) and after the wait that follows a prize (message 11, read as "N left."
    # comes back: PEEK var 1, ADD var 1, 0x3800, then 0xE5 1); at the end ReMixed's own close of box 2 (its line along
    # the bottom on the PS1) closes it. Its line along the bottom, box 2 too now, closes before its cash-in and
    # gold-chest menus open there (after op 0x9D 0, 0x12 and its ENDIF, and after POKE var 0x22): a menu opened over it
    # left it open under the menu, and the scene stayed on its END over the black screen (seen on the PC). Seen on the
    # PC: the prize box closes once read, and the scene ends on the world map.
    WHEEL: [(bytes.fromhex("10 10 63 09 00 90 00 00 00 00 00 00 00 00 03 e5 01 00"), WHEEL_CLOSE),
            (bytes.fromhex("c1 01 2a 00 7f 02 b0 01 00 00 38 e5 01 00"), WHEEL_CLOSE),
            (bytes.fromhex("9d 00 12 00 04"), WHEEL_CLOSE),
             (bytes.fromhex("c4 22 08 00 2b 04"), WHEEL_CLOSE)]}
# 0x111, 1-A's start in the PSP script (SCRIPT_VERSION_SET), is 0x0EC's code but for the preset it loads: its fixes too.
for _fixes in (EVENT_CODE_PATCHES, EVENT_CODE_MOVES, QUESTION_BOXES, EVENT_CODE_INSERTS):
    _fixes[0x111] = _fixes[0xEC]
# The Algus campaign's chapter-1 battles (0x00A, 0x00B, 0x00D, 0x010) open by marking Ramza (in roster slot 10 while
# Algus leads) and Delita away: roster +0xD0 = 3 (the remaster's +0x11E), which keeps him out of the formation's list
# (0x2FB4F8 lists no unit with that byte set). The remaster's formation screen still asks for Ramza while a place is
# free ("Ramza is required but has not been placed.", 0x00A's, from a new game), and commences once every place is
# filled (the regression pass, 2026-11-12/13). Marking him as 1-A does (bit 0x08) changed nothing there, so ReMixed's
# 3 stays. Each of the campaign's battles has no more places than its party fills (4; 2 and 2; 2 and 4; 5), so they
# commence with every place filled. Where the remaster's rule sits (its own sortie screen, not 0x2FC870, whose commence
# skips the "fewer units" step for a battle's last group) is still to find, for a battle with places to spare.
# The real block: roster +0xD0 is a bitfield in ReMixed, whose "away" is only its low nibble (& 0x0F: 0x80027180, its
# random battles' 0x191), and its Preparation Menu clears its marks after each battle's setup; the port maps the whole
# byte onto the remaster's errand byte (+0x11E), and the remaster lists no unit with any bit set (0x2FB4F8). 0x0FD
# writes 0x10 into Algus's right after swapping him into slot 0 (and 0x0FE 0xE0 into Zalbag's), so the remaster never
# listed Algus, and its formation, which wants the unit in roster slot 0 placed (1-A's Agrias there is listed and its
# battles commence; it calls that unit Ramza), refused every battle of the campaign, full or not (the regression pass,
# 2026-11-13: Algus's +0x11E 0x10). Nothing in the campaign's events reads those bits back (only 0x102 ORs 0x0F in),
# so they become 0 here; the low-nibble marks (Ramza and Delita away, 3; 0x106's 0x0C; 0x102's 0x0F) stay.
ALGUS_HIGH_MARKS = {0xFD: (bytes([0xAE, 0x18, 0x00, 0x10, 0x00]), bytes([0xAE, 0x18, 0x00, 0x00, 0x00])),
                    0xFE: (bytes([0xAE, 0x01, 0x00, 0xE0, 0x00]), bytes([0xAE, 0x01, 0x00, 0x00, 0x00]))}
for _event, _mark in ALGUS_HIGH_MARKS.items():
    EVENT_CODE_PATCHES.setdefault(_event, []).append(_mark)
# 1-A's end (0x11A) makes its Agrias chapter 2's guest Agrias: it moves her (ReMixed's roster swap) into the PS1's
# roster slot 18 (0x80059174, +1 = 0x12), its third guest slot (the PS1 keeps its guests in 16-19; 1-A's Valmafra and
# Elias are in the first two), so its chapter 2 finds her there and makes no Agrias of its own (0x01B). The remaster's
# guests are in 50-53, so slot 18 is an ordinary party slot there: she stayed a party unit, and the remaster's chapter-2
# opening (event 117) saved a guest Agrias of its own beside her (a player, 2026-11-27: two Agrias, Alicias and Lavians,
# and their gear; RosterGuard keeps the repeats out of the join). She goes to the remaster's third guest slot, 52, at the
# PS1 address the upgrade gives it (Ps1Memory.RosterAddress: 0x80E00000 + 52 x 0x100), +1 its number. The upgrade's
# swap reads either address (RemixedEvents.SwapRoster).
SIDE_CHAPTER_A_GUEST_SLOT = 52
_GUEST_ADDRESS = 0x80E00000 + SIDE_CHAPTER_A_GUEST_SLOT * 0x100
SIDE_CHAPTER_A_GUEST = [
    (bytes.fromhex("b0 01 00 74 91"),  # ADD var 1, 0x9174 (var 1 is 0x80050000): slot 18
     bytes([0xAC, 0x01, 0x00]) + (_GUEST_ADDRESS >> 16).to_bytes(2, "little")
     + bytes([0xB0, 0x01, 0x00]) + (_GUEST_ADDRESS & 0xFFFF).to_bytes(2, "little")),
    (bytes.fromhex("ae 00 00 12 00"), bytes([0xAE, 0x00, 0x00, SIDE_CHAPTER_A_GUEST_SLOT, 0x00]))]  # its +1
EVENT_CODE_PATCHES.setdefault(0x11A, []).extend(SIDE_CHAPTER_A_GUEST)


def question_box(code: bytes, lengths: bytes, question: tuple[int, int, int] | None) -> bytes:
    """ReMixed's event code (the PS1 layout) with its question in boxes the remaster draws (QUESTION_BOXES): the
    explanation's MESSAGE as a narration and a wait after it, the menu's at the unit (x and y 0)."""
    if question is None:
        return code
    lead, menu, unit = question
    out, at, found = bytearray(), 0, set()
    while at < len(code):
        ins = bytearray(code[at:at + 1 + lengths[code[at]]])
        at += len(ins)
        message = ins[3] | ins[4] << 8 if len(ins) == 15 else -1
        if ins[0] == 0x10 and message == lead:
            ins[2], ins[5], ins[6:15] = NARRATION_STYLE, NARRATION_UNIT, bytes(9)
            ins += WAIT_FOR_MESSAGES
        elif ins[0] == 0x10 and message == menu:
            ins[2], ins[5], ins[8:12] = MENU_BOX_STYLE, unit, bytes(4)
        if ins[0] == 0x10 and message in (lead, menu):
            found.add(message)
        out += ins
    if found != {lead, menu}:
        sys.exit("ReMixed's question isn't where the build expects it")
    return bytes(out)


def shown_notices(code: bytes, lengths: bytes, notices: set[int]) -> set[int]:
    """The messages NOTICE_MESSAGES' MESSAGEs show as the event runs: where ReMixed writes the number in (the PSP
    script's, as in 0x0FD: its 18 shows as 38), the one message_numbers works out, else their own."""
    numbers, out, at = message_numbers(code, lengths), set(), 0
    while at < len(code):
        if code[at] == 0x10 and u16(code, at + 3) in notices:
            shown = numbers.get(at)
            out.add(u16(code, at + 3) if shown is None else shown)
        at += 1 + lengths[code[at]]
    return out


def timed_notice(code: bytes, parts: tuple[bytes, bytes, bytes] | None) -> bytes:
    """A notice's wait, hold and close (TIMED_NOTICES) as wait, close, hold: it closes on its own."""
    if parts is None:
        return code
    wait, hold, close = parts
    if code.count(wait + hold + close) != 1:
        sys.exit("a timed notice's wait, hold and close aren't where the build expects them (TIMED_NOTICES)")
    return code.replace(wait + hold + close, wait + close + hold)


def closed_by_event(code: bytes, lengths: bytes, at: int, box: int) -> bool:
    """Whether the event's next use of a box from `at` on is a close of it (0x51 with no message), rather than a
    MESSAGE or a swap into it, or its END."""
    while at < len(code):
        ins = code[at:at + 1 + lengths[code[at]]]
        at += len(ins)
        if ins[0] == 0xDB:
            return False
        if ins[0] == 0x10 and len(ins) == 15 and ins[2] & 3 == box:
            return False
        if ins[0] == 0x51 and len(ins) == 6 and ins[1] == box:
            return u16(ins, 2) == CLOSE_BOX
    return False


def notice_boxes(code: bytes, lengths: bytes, messages: set[int] | None) -> tuple[bytes, int]:
    """Put one-shot narration messages in private speech-box styles. Their existing E5 1 waits then require a player
    press, and keeping the source box number lets the event's own close release the wait that follows it. Where the
    event doesn't close the box before using it again (ReMixed's line closed itself on the press, as the Stone's do in
    0x10E), the box is closed after the wait: in a speech window it stayed up, the line and then an empty frame, over
    the scene's next lines (seen on the PC, 2026-11-28). Returns the code and the number of closes added."""
    if not messages:
        return code, 0
    out, at, found, waiting, closed = bytearray(), 0, set(), set(), 0
    while at < len(code):
        ins = bytearray(code[at:at + 1 + lengths[code[at]]])
        at += len(ins)
        message = u16(ins, 3) if ins[0] == 0x10 and len(ins) == 15 else -1
        if message in messages:
            box = ins[2] & 3
            if box not in (*SPEECH_BOXES, 3):
                sys.exit(f"ReMixed's notice message {message} has unsupported box {box}")
            ins[2] = NOTICE_STYLE_PLACE | box
            found.add(message)
            waiting.add(box)
        out += ins
        if bytes(ins) == WAIT_FOR_MESSAGES and waiting:
            for box in sorted(waiting):
                if not closed_by_event(code, lengths, at, box):
                    out += bytes([0x51, box]) + CLOSE_BOX.to_bytes(2, "little") + bytes(2)
                    closed += 1
            waiting.clear()
    if found != messages:
        missing = ", ".join(str(n) for n in sorted(messages - found))
        sys.exit(f"ReMixed's acknowledgable notice message(s) {missing} aren't where the build expects them")
    return bytes(out), closed


# Vanilla's 3D-object instructions (0x54 Use3DObject, 0x56 Wait3DObject) animate a map's moving parts from the
# remaster's tables for its own looks (RefinedBgAnim); a look the build makes has none, and the remaster divided by a
# zero frame count (exe+0x24ED3B in 0x24E88C, seen on the PC in 0x0FD on TUNNEL). On those maps 0x54 becomes a wait
# of a frame (0xF1, the same length) and 0x56 goes (jumps go by label, so the code may shrink).
MAP_OBJECT_USE, MAP_OBJECT_WAIT = 0x54, 0x56


def without_map_objects(code: bytes, lengths: bytes) -> tuple[bytes, int]:
    """ReMixed's event code (the PS1 layout) without its 3D-object instructions, and how many there were."""
    out, at, count = bytearray(), 0, 0
    while at < len(code):
        op = code[at]
        ins = code[at:at + 1 + lengths[op]]
        at += len(ins)
        if op == MAP_OBJECT_USE and len(ins) == 3:
            ins, count = bytes([0xF1, 0x01, 0x00]), count + 1
        elif op == MAP_OBJECT_WAIT:
            ins, count = b"", count + 1
        out += ins
    return bytes(out), count


# The redrawn maps' moving parts (docs/REMASTER.md, "The event interpreter"). A field object (0x55 n, waited for by
# 0x57) moves a part of the classic map: a gate, a door, a drawbridge. The remaster's redrawn map has animations of
# its own for those (its RefinedBgAnim rows), and which of them a field object starts is compiled into its code by the
# scene's number (var 0x27) and the object: exe 0xC729C and 0xC9538, both called from the field objects' start
# (0x24F5A0). REMASTER_MAP_MOVEMENTS is those lists: the remaster's scene -> the objects that move something there
# (read from the code, 2026-11-26; the code mod's offline check runs the two functions for every scene and object and
# fails if this isn't what they do). ReMixed's scenes run under the port's own numbers, which the lists don't know:
# the classic gate rose and the redrawn one stood still, and Ramza walked in through Eagrose Castle's portcullis (a
# tester on 0.9.39, 2026-11-26).
REMASTER_MAP_MOVEMENTS = {
    0x02: {1}, 0x04: {1, 2}, 0x06: {2, 4}, 0x2D: {3}, 0x36: {1}, 0x3A: {1}, 0x4D: {0}, 0x4F: {1}, 0x75: {2},
    0x93: {0}, 0x9C: {2}, 0xA0: {0}, 0xA7: {0, 1}, 0xAC: {1}, 0xC1: {2}, 0xCD: {0, 1, 4, 5}, 0xD9: {0}, 0xE9: {2},
    0xF3: {2}, 0xF9: {2}, 0xFB: {2}, 0x113: {2, 3}, 0x11B: {0}, 0x12E: {2}, 0x159: {2}, 0x176: {0}, 0x18C: {1},
    0x1AD: {0, 1}, 0x1B2: {1}, 0x1B4: {0, 4}}
# ReMixed's scene -> the remaster's scene it stands in for where its field objects are concerned: the same map, and
# the objects ReMixed's scene uses among those the remaster's moves (None: ReMixed's scene uses a map's objects that
# the remaster's list moves nothing for in any scene). The code mod gives the lists that scene's number while one of
# ReMixed's runs (Patches/MapMovements.cs), and 0, a scene on neither list, for every other scene of ReMixed's: some
# of the port's numbers are the remaster's own (0x1B2 and 0x1B4 are ReMixed's Depths of Murond).
#   0x06E Eagrose Castle's gate (map 1): vanilla's 0x1B2 retold, its switch and portcullis (object 1 of 1-4).
#   0x06F inside Eagrose (map 10): vanilla's 0x1B4, objects 4 and 0: the hall's two doors, opened for the Hokuten
#     knights (the map's first and second animations).
#   0x09D Lionel Castle's gate (map 12): vanilla's 0x09C (the remaster's 156), object 2; its object 6 is the
#     remaster's own instruction there (MAP_ANIMATION_CALLS).
#   0x00F (the Argath campaign, map 11): vanilla's 0x02D (the remaster's 45), object 3; its object 5 likewise.
#   0x012 and 0x106 (map 23, the Beoulve manor): vanilla's 0x036, object 1 (0x03A's is the same): the manor's door
#     (the map's second animation). In 0x012 it opens out of the picture, the camera on the roofs.
#   0x0FC (map 56, Orbonne): the door that opens and shuts, objects 2 and 1, as in the remaster's 4 (the first
#     animation the opening; object 1 stops it and starts the second, the shutting). Three times over in 0x0FC.
#   0x100 and 0x104 (map 27, a snowbound fort): the door at the top of its slope, as in the remaster's 205. Object 0
#     opens it (the third animation), and in 0x104 object 1 shuts it again (the fourth).
#   0x102 (map 49, Fort Zeakden): vanilla's 0x04F, object 1: the tower's door shutting (the map's one animation).
#     Its object 0, the door's opening, is in the remaster's own scene too and moves nothing redrawn there, since
#     the redrawn map starts with the door open: 0x102, and 0x103 after it, which uses only object 0, set that
#     animation by hand (MAP_ANIMATION_CALLS).
#   0x11A (map 57, under Orbonne): object 2, as in the remaster's 233 and 243: the vault's door, opened for Delita.
# Seen in the hidden PC game (2026-11-26), each beside ReMixed on the PS1 and the same: 0x06E's portcullis, 0x00F's
# door (tests/quick/eagrose-gate-arrival.txt, argath-reprimand-doors.txt), 0x06F's two doors (eagrose-hall-doors),
# 0x012's and 0x106's (beoulve-manor-door-reprimand, -farewell), 0x100's and 0x104's (sluice-gate-argath, -hirame),
# 0x0FC's three times (orbonne-door-agrias), 0x11A's (orbonne-vault-door), and 0x102's (zeakden-gate-argath) once
# its door started shut. 0x09D's gate is open at its first line (lionel-gate-movements.txt), with no PS1 frame.
MAP_MOVEMENT_SCENES: dict[int, int | None] = {
    0x06E: 0x1B2, 0x06F: 0x1B4, 0x09D: 0x9C, 0x00F: 0x2D, 0x012: 0x36, 0x106: 0x36, 0x0FC: 0x04, 0x100: 0xCD,
    0x104: 0xCD, 0x102: 0x4F, 0x103: None, 0x11A: 0xE9}
FIELD_OBJECT_USE = 0x55
# The remaster's own instruction for the redrawn map's animations, 0xCD (u32 animation, u32 mode: state 1 for mode 1,
# else 2), which its scripts have at two of the scenes above where vanilla's had a field object alone; in one of
# ReMixed's events 0xCD is the upgrade's PostEvent, so the event calls the code mod's routine instead (0xCF 0xC8,
# Patches/MapMovements.cs Routine), the animation and the mode in the two words the extended statuses borrow
# (EXTENDED_STATUS_ACTOR, EXTENDED_STATUS_MODE: written before each call). ReMixed's event -> [(the instruction it
# comes after, the animation, the mode)]: the remaster's 156 has "0xCD 5, 0" in place of vanilla's object 6 (Lionel's
# gate), and its 45 "0xCD 2, 0" after object 5.
# The routine has a mode of the mod's own too, 2: the animation stands on its last frame. Fort Zeakden's tower (map
# 49) needs it. Vanilla's scene there (0x04F, the remaster's 79) opens the tower's door with object 0 as it starts and
# shuts it with object 1 near its end, so the redrawn map draws the door open until its one animation, the shutting,
# has played, and the remaster's list moves nothing for object 0. ReMixed's 0x102 has three lines of its own before
# object 0, with the tower in the picture's right edge on a wide screen: the door stood open through them (seen in
# the hidden PC game, 2026-11-26), where the PS1's is shut until object 0. And the battle after the scene loads the
# map again, door open, where the PS1's battle has it shut (seen on the PS1 the same day). So 0x102 puts the
# animation on its last frame as it starts (its LABEL 00, which the scene's run and the battle's both pass: the door
# shut), and back on its first after object 0 (mode 1: the door open; at once, there being no opening to play, and
# at the picture's edge). 0x103, the scene after that battle, opens the door with its own object 0 as it starts: the
# same call there, in case its map is still the battle's.
MAP_ANIMATION_ROUTINE = 0x000000C8
MAP_ANIMATION_AT_END = 2
MAP_ANIMATION_CALLS: dict[int, list[tuple[bytes, int, int]]] = {
    0x09D: [(bytes([0x55, 6, 0]), 5, 0)],
    0x00F: [(bytes([0x55, 5, 0]), 2, 0)],
    0x102: [(bytes([0x0E, 0x00]), 0, MAP_ANIMATION_AT_END), (bytes([0x55, 0, 0]), 0, 1)],
    0x103: [(bytes([0x55, 0, 0]), 0, 1)]}


def map_animation_call(animation: int, mode: int) -> bytes:
    """ReMixed's event code (the PS1 layout) for the remaster's 0xCD: the two words set, then the routine."""
    return (struct.pack("<BHH", 0xAE, 0xB7E, animation) + struct.pack("<BHH", 0xAE, 0xB7F, mode)
            + bytes([0xCF]) + MAP_ANIMATION_ROUTINE.to_bytes(4, "little"))


for _event, _calls in MAP_ANIMATION_CALLS.items():
    for _after, _animation, _mode in _calls:
        EVENT_CODE_INSERTS.setdefault(_event, []).append((_after, map_animation_call(_animation, _mode)))


def field_objects(code: bytes, lengths: bytes) -> set[int]:
    """The field objects an event's code (the PS1 layout) uses (0x55 n)."""
    used, at = set(), 0
    while at < len(code):
        op = code[at]
        if op == FIELD_OBJECT_USE and at + 2 < len(code):
            used.add(u16(code, at + 1))
        at += 1 + lengths[op]
    return used


def map_movement_rows(event_code, lengths: bytes, records: dict[int, bytes], remaster_maps: dict[int, int],
                      scenes: dict[int, int | None] | None = None) -> list[list[int]]:
    """The code mod's data for MAP_MOVEMENT_SCENES (Patches/MapMovements.cs): [the port's scenario, the remaster's
    scene whose map movements it takes or 0], for every number the port gives one of ReMixed's events. event_code(n)
    is ReMixed's event n's code, records its scenario records (the map at +2), remaster_maps the remaster's scenes'
    maps. Stops the build for a scene of ReMixed's that uses a field object on a map where the remaster's list moves
    something and isn't in the table, and for a stand-in on another map or one that moves none of the objects used."""
    scenes = MAP_MOVEMENT_SCENES if scenes is None else scenes
    listed_maps: dict[int, set[int]] = {}
    for scene in REMASTER_MAP_MOVEMENTS:
        listed_maps.setdefault(remaster_maps[scene], set()).add(scene)
    rows: dict[int, int] = {}
    for theirs, ours in sorted(REMIXED_SCENARIOS.items()):
        stand_in = scenes.get(theirs)
        used = field_objects(event_code(theirs), lengths)
        where = records[theirs][2] if theirs in records else None
        if theirs not in scenes:
            if used and listed_maps.get(where):
                sys.exit(f"ReMixed's event 0x{theirs:X} uses field object(s) {sorted(used)} on map {where}, where the "
                         f"remaster's scene(s) {', '.join(str(s) for s in sorted(listed_maps[where]))} move the redrawn "
                         "map by their own numbers: give it the scene it stands in for (MAP_MOVEMENT_SCENES)")
        elif stand_in is not None:
            if remaster_maps[stand_in] != where:
                sys.exit(f"ReMixed's event 0x{theirs:X} is on map {where}, not map {remaster_maps[stand_in]} as the "
                         f"remaster's scene {stand_in} (MAP_MOVEMENT_SCENES)")
            if not used & REMASTER_MAP_MOVEMENTS[stand_in]:
                sys.exit(f"ReMixed's event 0x{theirs:X} uses field object(s) {sorted(used)}, none of those the "
                         f"remaster's scene {stand_in} moves the redrawn map for (MAP_MOVEMENT_SCENES)")
        for number in (ours, BATTLE_SPLITS.get(theirs), AFTER_SPLITS.get(theirs)):
            if number is not None:
                rows[number] = stand_in or 0
    for ours in TAVERN_EVENTS.values():
        rows.setdefault(ours, 0)
    return [[ours, scene] for ours, scene in sorted(rows.items())]


def map_movement_data(cfg: dict, test_evt: bytes, battle_new: bytes) -> dict:
    """The code mod's rule data for the redrawn maps' moving parts (map_movement_rows), with the remaster's own list
    (REMASTER_MAP_MOVEMENTS) for its offline check: ReMixed's scenario records from its ATTACK.OUT, the remaster's
    scenes' maps from its exe."""
    with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
        attack = FilePair(vanilla, patched).get("EVENT/ATTACK.OUT")[1]
    records = {}
    for k in range(0x1F4):
        at = SCENARIO_TABLE + SCENARIO_SIZE * k
        if u16(attack, at):
            records[u16(attack, at)] = attack[at:at + SCENARIO_SIZE]
    exe = (Path(cfg["target"]["tic_dir"]) / "FFT_enhanced.exe").read_bytes()
    table, remaster_maps = 0x8ECAB0 - 0x1C00, {}  # the remaster's scenario records in the exe file (+2 the map)
    for k in range(0x377):
        record = exe[table + SCENARIO_SIZE * k:table + SCENARIO_SIZE * (k + 1)]
        if not u16(record, 0):
            break
        remaster_maps[u16(record, 0)] = record[2]
    lengths = battle_new[EVENT_LENGTHS - BATTLE_BASE:EVENT_LENGTHS - BATTLE_BASE + 256]
    rows = map_movement_rows(lambda n: remixed_event_code(test_evt, battle_new, n), lengths, records, remaster_maps)
    return {"scenes": rows,
            "listed": [[scene, sorted(objects)] for scene, objects in sorted(REMASTER_MAP_MOVEMENTS.items())]}


def insert_code(code: bytes, lengths: bytes, inserts: list[tuple[bytes, bytes]] | None) -> bytes:
    """ReMixed's event code (the PS1 layout) with instructions added after others (EVENT_CODE_INSERTS)."""
    for after, added in inserts or []:
        at = 0
        while at < len(code) and code[at:at + len(after)] != after:
            at += 1 + lengths[code[at]]
        if at >= len(code):
            sys.exit("an instruction to add code after isn't in ReMixed's event")
        at += len(after)
        code = code[:at] + added + code[at:]
    return code


# Where ReMixed's side events start on the world map (its WLDCORE conditions, docs/analysis/story-flow-plan.md "How
# the side stories start"): (location, [(variable, value), ...] all equal, ReMixed's event, mode). The story progress
# (var 0x6E) counts as vanilla's and the remaster's do; the events go in by their numbers in REMIXED_SCENARIOS. Mode 2
# cuts straight to the event, 1 swirls into it (the remaster's starts use only those: ReMixed's 0 for 0x0F6 becomes 1).
_A = (SIDE_CHAPTER_A_FLAG, 1)
REMIXED_WORLD_STARTS = [(28, [(0x6E, 0x25), (0x9D, 0)], 0x3F, 1),
                        (5, [(0x6E, 0), _A], 0xED, 2), (40, [(0x6E, 2), _A], 0xEE, 1),
                        (13, [(0x6E, 3), (0x2C, 0), _A], 0xEF, 2), (13, [(0x6E, 3), _A], 0xEF, 1),
                        (38, [(0x6E, 4), _A], 0xF0, 1),
                        (33, [(0x6E, 5), (0xAD, 1), _A], 0xF1, 1), (33, [(0x6E, 5), _A], 0xF1, 2),
                        (0, [(0x6E, 6), (0xAD, 1), _A], 0xF2, 2), (0, [(0x6E, 6), (0xAD, 0), _A], 0xF3, 2),
                        (7, [(0x6E, 7), _A], 0xF4, 1), (31, [(0x6E, 8), _A], 0xF5, 1), (1, [(0x6E, 9), _A], 0xF6, 1),
                        (27, [(0x6E, 0xA), _A], 0xF7, 1), (27, [(0x6E, 0xB), _A], 0xF9, 2),
                        (28, [(0x6E, 0xC), (0x202, 0), _A], 0xFA, 1), (9, [(0x6E, 0xD), (0x202, 0), _A], 0xFB, 1),
                        (18, [(0x6E, 0xE), _A], 0xFC, 2)]  # Orbonne, after 1-A's end (SIDE_CHAPTER_A_END)

SIDE_CHAPTER_DESTINATIONS = {
    0: "the Royal City of Lesalia", 1: "Riovanes Castle", 5: "Zeltennia Castle", 7: "the Walled City of Yardrow",
    9: "the Merchant City of Dorter", 13: "the Free City of Bervenia", 18: "Orbonne Monastery",
    27: "Mount Bervenia", 28: "the Zeklaus Desert", 31: "the Yuguewood", 33: "Grogh Heights",
    38: "Dugeura Pass", 40: "Finnath Creek",
}


def side_chapter_destinations() -> dict[int, int]:
    """The place 1-A is headed for by its story progress: where its world-map start at that progress is."""
    route = {}
    for location, checks, _, _ in REMIXED_WORLD_STARTS:
        condition = {variable: value for variable, value, *kind in checks if not kind}
        if condition.get(SIDE_CHAPTER_A_FLAG) == 1 and 0x6E in condition:
            progress = condition[0x6E]
            if route.setdefault(progress, location) != location:
                raise ValueError(f"1-A progress {progress} has multiple objective destinations")
    # Zeltennia runs some of its opening from the event, so progress 1 has no world start of its own.
    route.setdefault(1, route[0])
    return dict(sorted(route.items()))


def side_chapter_objectives() -> dict[int, str]:
    """World-map captions by story progress, from the same destinations that start the events."""
    return {progress: f"Travel to {SIDE_CHAPTER_DESTINATIONS[location]}."
            for progress, location in side_chapter_destinations().items()}


# The world map's steps on 1-A's way (its WLDCORE statements before or beside those starts): 0x0EC hides chapter 1's
# places (their flags, 0x200 + the location, cleared) and shows Zeltennia (5), and at four points 1-A shows the next
# stretch: the camera to a place (ReMixed's opcode 0x24, the remaster's EnhancedWorldEvent Type 36), a route drawn
# between two places (0x20, Type 32: its flag from 0x22D) and a place shown (0x22, Type 34), each once, while its flag
# is 0, as the remaster's own steps are. A story condition may be an exact value, or (comparison type, value). The
# opening accepts progress through 2: 0x0EC can return at 0, while its direct 0x0ED chain recruits Bryn and Lorne and
# reaches the world map at 2. That range also repairs a save made there before the route was revealed. At location 27
# with the story at 0xB (0x0F9's start) the way goes on to
# Dorter and Orbonne, where 1-A ends. (location, story, [(the flag, type, place, the other place), ...]), in ReMixed's
# order; each step waits for 1-A as its starts do. ReMixed's two 0x1D steps (var 0x6D, at 5 and 27) are left out, as
# the remaster leaves vanilla's out.
WORLD_AT_LEAST, WORLD_AT_MOST = 2, 3
WORLD_CAMERA, WORLD_ROUTE, WORLD_PLACE = 36, 32, 34
REMIXED_WORLD_STEPS = [
    (5, (WORLD_AT_MOST, 2), [(0x200, WORLD_CAMERA, 13, None), (0x248, WORLD_ROUTE, 5, 40),
            (0x228, WORLD_PLACE, 40, None),
            (0x247, WORLD_ROUTE, 40, 13), (0x20D, WORLD_PLACE, 13, None), (0x245, WORLD_ROUTE, 13, 38),
            (0x226, WORLD_PLACE, 38, None), (0x246, WORLD_ROUTE, 38, 33), (0x221, WORLD_PLACE, 33, None),
            (0x23C, WORLD_ROUTE, 33, 0), (0x200, WORLD_PLACE, 0, None)]),
    (0, 7, [(0x21F, WORLD_CAMERA, 31, None), (0x23B, WORLD_ROUTE, 33, 7), (0x207, WORLD_PLACE, 7, None),
            (0x23A, WORLD_ROUTE, 7, 31), (0x21F, WORLD_PLACE, 31, None)]),
    (31, 9, [(0x21B, WORLD_CAMERA, 27, None), (0x239, WORLD_ROUTE, 31, 1), (0x201, WORLD_PLACE, 1, None),
             (0x234, WORLD_ROUTE, 1, 27), (0x21B, WORLD_PLACE, 27, None)]),
    (27, 0xB, [(0x212, WORLD_CAMERA, 18, None), (0x235, WORLD_ROUTE, 27, 28), (0x21C, WORLD_PLACE, 28, None),
               (0x236, WORLD_ROUTE, 28, 9), (0x209, WORLD_PLACE, 9, None), (0x23F, WORLD_ROUTE, 9, 18),
               (0x212, WORLD_PLACE, 18, None)])]
# Biggs and Wedge at Gariland: ReMixed's start is story progress 4 and flag 0x97, which its 0x009 sets with progress 4
# (the remaster's 0x01D sets the progress, not the flag) and 0x0A4 clears, setting flag 0x92. So here: progress 4 and
# flag 0x92 not yet set (no vanilla or remaster event touches 0x92 before chapter 2's 0x075), outside side chapter 1-A,
# which counts its own story from 0 on the same variable.
REMIXED_WORLD_STARTS += [(6, [(0x6E, 4), (0x92, 0), (SIDE_CHAPTER_A_FLAG, 0), (ALGUS_FLAG, 0)], 0xA4, 2)]
# Balthier at Dorter: ReMixed's (WLDCORE's program before its 0x19 0x98 1) is flag 0xB7 = 1 and flag 0xB8 = 0, mode 1.
REMIXED_WORLD_STARTS += [(9, [(0xB7, 1), (0xB8, 0), (SIDE_CHAPTER_A_FLAG, 0), (ALGUS_FLAG, 0)], 0x98, 1)]
# The Algus campaign's: ReMixed's own (its chapter 1's, on the main story's progress as the remaster counts it), each
# with "flag ALGUS_FLAG = 1"; every start of the remaster's own waits with "= 0" (write_world_starts).
_G = (ALGUS_FLAG, 1)
REMIXED_WORLD_STARTS += [(2, [(0x6E, 3), _G], 0x008, 2), (26, [(0x6E, 4), _G], 0x00A, 1), (9, [(0x6E, 5), _G], 0x00B, 1),
                         (28, [(0x6E, 6), _G], 0x00D, 1), (2, [(0x6E, 7), _G], 0x00F, 2), (17, [(0x6E, 8), _G], 0x010, 1),
                         (2, [(0x6E, 9), _G], 0x013, 2), (15, [(0x6E, 0xA), _G], 0xFF, 1)]
# The Algus campaign's world-map steps: ReMixed's own (its main story's too, in WLDCORE's lists for locations 24 and 2),
# where the remaster has none. The campaign's start puts the party at Mandalia (0x0FD: var 0x31, the party's location,
# = 24, and that place shown); at the story's 3 there ReMixed draws the road on to Eagrose and shows it, and at Eagrose
# at 4 the road from Mandalia to Gariland and Gariland, from where the remaster's own steps there go on (Gariland to
# Sweegy Woods, then Dorter). The remaster draws those four at Gariland at 2, after its own battle there, which the
# campaign doesn't play: without them its party stood at Mandalia with no road to Eagrose (read 2026-11-12; the
# harness had started each scenario from the world map). At 0xA ReMixed draws a road straight from Eagrose to location
# 15 (route flag 0x22C), which the remaster's map hasn't; the remaster's own steps at Eagrose at 0xA (to 15 by way of
# locations 29 and 25) run in the campaign as they are. (location, story, steps) as REMIXED_WORLD_STEPS; each step
# waits for the campaign ("flag ALGUS_FLAG = 1").
ALGUS_WORLD_STEPS = [(24, 3, [(0x22D, WORLD_ROUTE, 24, 2), (0x202, WORLD_PLACE, 2, None)]),
                     (2, 4, [(0x22F, WORLD_ROUTE, 24, 6), (0x206, WORLD_PLACE, 6, None)])]
SIDE_CHAPTER_A_START_PLACE = 5  # Zeltennia Castle, which 0x0EC itself shows as the chapter starts
# ReMixed's own stories and the world-map steps each one's roads come from: (name, steps, the flag that says it runs).
# write_world_starts writes every step from this list, and the code mod gets the flags from it (Patches/WorldRoads.cs):
# the remaster draws a road grey while its own count of the story (the stage: GameFlowManager's, quest 0) says the road
# isn't open yet, from a list of roads per stage (WorldMapProgressInfo's RouteIds, by way of GameProgress). Only the
# remaster's own events move the stage (their instruction 0xE9), so in one of these stories it stays where the main
# story stopped: 440 from chapter 1's end, whose row greys roads 11 and 10, and 1-A walks road 10 (the Zeklaus Desert to
# Dorter) at its end (a tester, 2026-11-22: "grey though walkable"). While one of these flags is set no road is greyed.
OWN_STORY_STEPS = [("1-A", REMIXED_WORLD_STEPS, SIDE_CHAPTER_A_FLAG), ("the Algus campaign", ALGUS_WORLD_STEPS, ALGUS_FLAG)]
# The remaster's events whose numbers ReMixed's events take (REUSED_SCENARIO_NUMBERS) no longer move the stage as they
# end. Where the main story's progress (var 0x6E) says the story is past one, the stage follows it as the world map
# starts: (var 0x6E at least, the stage at least, the stage it becomes). Araguay Woods' last scene (127) set the story
# to 0x11 and the stage to 478 together; ReMixed's (0x01D, 0x107) set the story alone, which left the stage at Dorter's
# 465 and the road on from the woods to Zeirchele Falls (road 21) grey until the falls.
# Eagrose in chapter 4: the remaster's scenes there (433-440, REMASTER_SCENES_SKIPPED) moved the stage from 1710 ("Meet
# with Dycedarg at Eagrose Castle.") to 1770 ("Rescue Alma from Mullonde Cathedral."), its last (440) with the story
# to 0x34; ReMixed's (0x06E, 0x06F, 0x10E) set the story alone, so the objective line and its marker stayed on Eagrose,
# where nothing more plays, until Mullonde's first scene (442: 1780). A tester on 0.9.39 (2026-11-26): "Storyline can
# continue just by going to Mullonde. Nothing triggers if you leave and go back to Eagrose."
STORY_STAGE_FLOORS = [(0x11, 465, 478), (0x34, 1700, 1770)]
# The remaster's scenes that one of REMASTER_STARTS_REPLACED's starts no longer leads to: its chain from that event on,
# which ReMixed's scenes play in place of. Their stages count as lost (check_story_stages), as a reused number's do.
REMASTER_SCENES_SKIPPED = {(2, 433): range(433, 441)}
# The stages the reused numbers' events set that nothing sets now, each with why that is left: (the remaster's event,
# quest, stage). check_story_stages stops the build on one that isn't here or in STORY_STAGE_FLOORS.
STORY_STAGES_LEFT = {
    (122, 0, 470): "Araguay Woods, inside the battle: the same roads and objective as 465, and 478 follows",
    (123, 0, 472): "Araguay Woods, inside the battle: as 470",
    (124, 0, 474): "Araguay Woods, inside the battle: as 470",
    (434, 0, 1720): "Eagrose, inside its scenes: the objective and roads of 1770, which follows as the story reaches 0x34",
    (436, 0, 1730): "Eagrose, inside its scenes: as 1720",
    (439, 0, 1760): "Eagrose, inside its scenes: as 1720",
    (452, 0, 1830): "the temple: the same roads and objective as 1820",
    (469, 2, 35): "a side quest's stage (quest 2): the same roads and objective as its 30",
}
STAGE_OPCODE = 0xE9  # the remaster's own events': a u32 quest and a u32 stage (0x23B5xx hands them to 0xFF660)


def own_story_destinations() -> dict[int, dict[int, int]]:
    """Where each of ReMixed's own stories is headed for by its progress (var 0x6E), by the story's flag, for the world
    map's objective marker (the code mod's WorldRoads): the remaster puts its diamond on the place its own stage names
    (Dorter all through 1-A, from stage 440). 1-A's are its world-map starts' places, as its objective lines are; the
    Algus campaign has none worked out, so its map shows no marker."""
    return {SIDE_CHAPTER_A_FLAG: side_chapter_destinations()}
# The chapter-3 branch (CHAPTER_3_BRANCH): location 15, ReMixed's two flags in its order, 0xAE clear (Hirame not lost)
# and 0x2ED set (opened by her rescue, shut once taken), then the story at least 0x22, outside the Algus campaign; mode
# 1, as ReMixed's. A check may name a row type: EnhancedWorldEvent's 2 is "at least" (it fails
# while the variable is below the value; 3 is "at most"), which none of the remaster's own rows use. The remaster
# makes each row into the PS1's program (0x343AF8: type 1 as 1, Unknown10, Unknown8's value; type 4 as 4 and the value;
# others as the type and all of Unknown8's values), and its ops 2 and 3 (0x3440AC, 0x3440F0, as WLDCORE's 0x800914BC
# and 0x80091518) read a variable and a value after it: so their rows carry both in Unknown8 (write_world_starts). Until
# 2026-11-12 the build wrote only the value there, which made this start's program [2, 0x22] and read var 0x22 against
# the next row's type, then the next row's variable as an opcode (past 0x2B: the location's check gave up).
REMIXED_WORLD_STARTS += [(15, [(HIRAME_LOST, 0), (CHAPTER_3_BRANCH_OPEN, 1), (0x6E, 0x22, WORLD_AT_LEAST),
                               (ALGUS_FLAG, 0)], 0x104, 1)]
# Beowulf's arc (BEOWULF_QUEST): 0x09C at location 3 with Beowulf (0x1F) and Reis (0x0F) in the party (a check of type
# 4, its value the character), the "Warning!" battle won (flag 0xAB) and the arc not over (0xB3, which 0x0A0 sets),
# mode 2. ReMixed's also wants its PSP script (flag 0xB2 = 1), which the port always shows (SCRIPT_VERSION) without
# setting the flag, so that check is left out.
WORLD_PARTY_HAS = 4
REMIXED_WORLD_STARTS += [(3, [(0, 0x1F, WORLD_PARTY_HAS), (0, 0x0F, WORLD_PARTY_HAS), (0xAB, 1), (0xB3, 0),
                              (ALGUS_FLAG, 0)], 0x9C, 2)]
# Luso at Mandalia (LUSO_AT_MANDALIA): ReMixed's is location 24's fifth entry (WLDCORE's table 0x80097234, its program at
# WORLD 0x80149D28): flag 0x92 = 1 (Biggs and Wedge's scene, 0x0A4, sets it), flag 0x93 = 0 (0x0EB sets it), the story
# at most 0xF (op 3, which fails while the variable is above the value), then 0x0EA, mode 1; after the remaster's own
# entries there, as ReMixed's is after its. The story at most 0xF is chapter 1 and chapter 2's start, before Araguay
# Woods (0x10), where 0x107 reads flag 0x93. Outside side chapter 1-A, which counts its own story from 0 on var 0x6E;
# ReMixed's start doesn't check the Algus campaign (0x0EA has his lines for it), and the mod's 0x0A4 doesn't run there.
REMIXED_WORLD_STARTS += [(24, [(0x92, 1), (0x93, 0), (0x6E, 0xF, WORLD_AT_MOST), (SIDE_CHAPTER_A_FLAG, 0)], 0xEA, 1)]
# The remaster's own starts that run one of ReMixed's scenarios in place of its event, where ReMixed's story replaces
# the remaster's at the same point: (location, the remaster's event) -> ReMixed's (in REMIXED_SCENARIOS). The start's
# conditions stay the remaster's (they're ReMixed's too). Eagrose with the story at 51 (0x33): Zalbag at Eagrose.
REMASTER_STARTS_REPLACED = {(2, 433): 0x6E}
WORLD_EVENT_UNUSED = 47  # EnhancedWorldEvent's value for an argument an opcode doesn't take
SCENARIO_TABLE, SCENARIO_SIZE = 0x10938, 24  # ATTACK.OUT: +0 id, +2 map, +7 battle entry, +9 and +0xB formations
RECORD_NEXT, RECORD_NEXT_MODE = 0x12, 0x14  # a record's own hand-off: the scenario (u16), the map-settings mode
FORMATION_TABLE, FORMATION_SIZE, FORMATION_ID = 0xBBD4, 12, 10  # ATTACK.OUT: record n for formation n, its id at +10
REMASTER_FORMATIONS_FILE = 0x7AE2D0 - 0x1C00  # FFT_enhanced.exe: .data 0x7AE2D0
REMASTER_FORMATION_COUNT = 0x300  # the formations it has room for (RemixedScenarios.FormationCount)
# ReMixed's battle entries go into vanilla slots no scenario of the remaster's names (nor its events: none sets var
# 0x32), in ENTD4. Not past 0x1FF: the game reads a row per entry in the EntryNo NXD table, whose index stops there (a
# row added for 0x250 wasn't found, and the formation setup crashed on it).
FREE_ENTRIES = range(0x1D6, 0x200)
# ENTD4's slots no scenario names hold ReMixed's battle entries; the last is kept for the rest (OWN_ENTRY_SLOT): a
# scenario past the slots names it, the scenarios file carries its 16 units, and the code mod hands them to the battle
# setup in its place (RemixedScenarios, the entry's records 0x28416C).
ENTD_PRESET = 37
ENTD_SECONDARY = 11
ENTD_BATTLE_ID = 0x20
REMIXED_UNIT_JOBS = {0x38: 0x5B, 0x5B: 0xA0, 0x37: 0xA0}  # ReMixed's job -> the remaster's (DARK_KNIGHT, BARD)
# Characters ReMixed puts in a slot the remaster's story uses for someone else: sprite set -> (the remaster's sprite set
# for them, their name, their job, their gender flag). ReMixed's 0x22 is side chapter 1-A's young Agrias (vanilla's and
# the remaster's 0x22 is Mustadio; REMASTER_KEPT_JOBS), with a name of 0x22, 0x1E or none and a job of 0x22 or none:
# she becomes the remaster's Agrias (sprite set, name and job 0x1E, a woman). Her knight's job isn't the remaster's to
# give, so she's a Holy Knight from the start (ReMixed's 1-A opens with her promotion to one).
ENTD_FLAGS, ENTD_NAME, ENTD_WOMAN, ENTD_GENDER = 1, 2, 0x40, 0xE0
# A battle unit whose data loads it from the formation (flag 0x08: a guest the story saved; in the remaster the roster's
# guest slots 50-53, found by sprite set) is left empty when there's none (docs/REMASTER.md, "Guests and joins"). The
# Algus campaign's battle data loads Delita (0x04) in its chapter-1 battles and scenes, Algus (0x07) in 0x013 and
# 0x106, and Alma (0x30) in the final battle, but from a new game nothing ever saves them there: 0x006's battle had an
# empty unit 0 in Delita's place (name id 0) and crashed as it began (a divide by zero in the AI's turn timeline,
# exe+0x38A844; the regression pass, 2026-11-12). The earlier tests started from an ordinary save, which has Delita as
# a guest. On the PS1 ReMixed's battle has Delita, made from the data (its roster has none; seen 2026-11-12). So in the
# campaign's scenarios a special character's unit is made from its data: flag 0x08 cleared.
ENTD_LOAD = 0x08
ALGUS_GUESTS_FROM_DATA = ENTD_LOAD
REMIXED_SPRITE_SETS = {0x22: (0x1E, 0x1E, 0x1E, ENTD_WOMAN)}
# ReMixed's Luso (LUSO_AT_MANDALIA, ARAGUAY_LUSO) is its slot 0x12 (sprite set, name and job), which the remaster has as
# Marach's and which has no Luso of its own. He keeps the slot, so ReMixed's events find him by it (HAS_CHARACTER,
# ROSTER_ADDRESS and the conditions name sprite set 0x12, never the name), and he's drawn with ReMixed's own sprite
# there (REMIXED_SPRITES); his name is a CharaName row of its own (LUSO_NAME, one that no unit of vanilla's or
# ReMixed's battle data names and the remaster leaves blank), so his units and his roster entry read "Luso" and the
# remaster's Marach keeps his. Decided 2026-11-12 (the user). His job is ReMixed's row 0x12 as well, not one of
# REMASTER_KEPT_JOBS: the remaster's rooftop Marach plays as ReMixed's Malak there (sprite set and job 0x1A, a corpse's
# preset), so slot 0x12 is Luso's alone (REMASTER_MOVED_SPRITE_SETS), for his own art (the sprite port).
ENTD_MAN = 0x80
LUSO_SLOT, LUSO_NAME, LUSO_TEXT = 0x12, 0x38, "Luso"
REMIXED_SPRITE_SETS[LUSO_SLOT] = (LUSO_SLOT, LUSO_NAME, LUSO_SLOT, ENTD_MAN)
# ReMixed's Ashley is its slot 0x34 (sprite set, name and job: Riskbreaker), Agrias's second sprite set in the remaster,
# whose CharaName 0x34 is "Agrias": her unit card said so (seen on the PC, 2026-11-12). ReMixed's events find her by
# the sprite set (HAS_CHARACTER and ROSTER_ADDRESS 0x34) and battle id 0x5B, never the name, so she keeps the slot and
# job and takes a name of her own as Luso does: CharaName 0x42, blank in the remaster and named by no unit of
# vanilla's, ReMixed's or the remaster's battle data (read 2026-11-13). Her units are in ReMixed's entries 0x141 (the
# Lich's battle), 0x142 (her scene) and 0x1B8 (0x0C5). With a name id of her own her guest record is never taken for
# the remaster's guest Agrias's (REMASTER_OLD_GUESTS: sprite set 0x34 with name id 0x34), even in a save from before
# the story's point 0x16.
ASHLEY_SLOT, ASHLEY_NAME, ASHLEY_TEXT = 0x34, 0x42, "Ashley"
REMIXED_SPRITE_SETS[ASHLEY_SLOT] = (ASHLEY_SLOT, ASHLEY_NAME, ASHLEY_SLOT, ENTD_MAN)
# Hirame's two appearances use her two graphics with their name bytes crossed (0x59 on graphic 0x35 in the
# chapter-3 branch, 0x35 on graphic 0x59 in 1-A's rescue). Wright, beside her in the branch, has graphic and name
# 0x36. Give both blank CharaName rows of their own and normalize every unit of those graphics to them. Their events
# find them by battle id or sprite set, except 0x112's comparison patched above.
HIRAME_TEXT, WRIGHT_TEXT = "Hirame", "Wright"
REMIXED_SPRITE_SETS[0x35] = (0x35, HIRAME_NAME, 0x35, ENTD_WOMAN)
REMIXED_SPRITE_SETS[0x59] = (0x59, HIRAME_NAME, 0x59, ENTD_WOMAN)
REMIXED_SPRITE_SETS[0x36] = (0x36, WRIGHT_NAME, 0x36, ENTD_MAN)
REMIXED_OWN_NAME_SPRITES = {0x35, 0x36, 0x59}
# The CharaName rows the build names (write_character_names).
# These three 1-A recruits use name ids which are populated in ReMixed but blank in the remaster. Without rows of
# their own their party cards are unnamed, and the remaster's job screen can dereference the missing name.
BRYN_NAME, LORNE_NAME, WHITNEY_NAME = 0x69, 0x6A, 0x6C
CHARACTER_NAMES = {HIRAME_NAME: HIRAME_TEXT, WRIGHT_NAME: WRIGHT_TEXT,
                   LUSO_NAME: LUSO_TEXT, ASHLEY_NAME: ASHLEY_TEXT,
                   BRYN_NAME: "Bryn", LORNE_NAME: "Lorne", WHITNEY_NAME: "Whitney"}
# The remaster's own units on a slot ReMixed made someone else's: sprite set -> (the sprite set they move to, the job
# they take where theirs is the old slot's own, or None to keep it); the name and the battle id stay, so the remaster's
# events still find the unit and name it. Each new sprite set draws the same art as the old one (the same enhanced
# texture, checked against g2d.dat), so the remaster's units look as before, and ReMixed's characters on the old slots
# draw with ReMixed's own art (REMIXED_SPRITES). Applied to the four battle data files and the remaster's added entries
# (ADDED_ENTRIES_FILE, from 0x200, its own new scenes); ReMixed's own entries go in later, on their own slots.
# - 0x12, Malak's second: ReMixed's Luso alone (sprite set, graphic and job row 0x12, as in ReMixed, whose 12.SPR is Luso
#   and whose 1A.SPR is vanilla's MARA.SPR, the sheet both of vanilla's Malak slots draw): the remaster's only units
#   there, Marach lying dead on the Rooftop of Riovanes (entry 0x1B1, unit 2, battle id 0x12) and the unused test entry
#   0x1F2's, become sprite set 0x1A, which the remaster draws from the same texture (0x368) as 0x12, as ReMixed's own
#   rooftop has him. Every other Marach of the remaster's is 0x1A already (its added entries 0x233 and 0x23B too).
#   Decided 2026-11-12 (the user, with Luso drawn as Luso by the sprite port).
# - ReMixed gave each story character one sprite set and put its own characters in the others (2026-11-12, the shared
#   sprite slots): 0x34, Agrias's second (the guest of chapter 2: Orbonne to Lionel), is ReMixed's Ashley, and its
#   guest Agrias is 0x1E with battle id 0x34; 0x19, Rafa's second (her guest at Riovanes), is someone of ReMixed's and
#   its guest Rafa is 0x29; 0x11, Gafgarion's second (the enemy), is someone of ReMixed's and its enemy Gafgarion is
#   0x17. The remaster's units on them move the same way, jobs with them (vanilla's 0x34, 0x19 and 0x11 are those
#   characters' own rows, which ReMixed's characters now have). A guest is kept in the roster's guest slots and found
#   there by sprite set (0x2766C8), so all of a guest's units move together, and the remaster's events that take a
#   guest out of the party by sprite set (instruction 0x7A) move with them (REMASTER_SPRITE_SET_OPS); saves made
#   with a guest on its old slot are moved by the code mod (REMASTER_OLD_GUESTS).
# - 0x36-0x38, copies of the female White Mage's, the male Black Mage's and the male Mystic's sheets that only the
#   remaster's tutorial battles use (0x00A, 0x00C and 0x02E, with jobs of the same numbers): ReMixed made those units
#   generics of those jobs, which draw the same art (graphics 0x6B, 0x6C and 0x76), and put its own characters in the
#   slots (0x37 and 0x38 are its locked looks 0xB3 and 0xB4).
# - 0x46 and 0x47, copies of the male Mystic's and the female Summoner's sheets that only Poeskas Lake uses (0x1C5,
#   the undead Mystic and Summoner, jobs 0x46 and 0x47 with innate Undead): ReMixed made them generic Mystics and
#   Summoners, but without its unit presets they'd lose the Undead, so they keep their jobs and take the sprite sets
#   that are those sheets' graphics (0x76, 0x71), which the remaster draws as they are (0x278EDC).
# Not 0x22, Mustadio's second: ReMixed's 22.SPR is vanilla's KANBA.SPR, Agrias's sheet, which its young Agrias draws
# from, and she draws as the remaster's Agrias already (REMIXED_SPRITE_SETS).
REMASTER_MOVED_SPRITE_SETS = {LUSO_SLOT: (0x1A, 0x1A), 0x34: (0x1E, 0x1E), 0x19: (0x29, 0x29), 0x11: (0x17, 0x17),
                              0x36: (0x81, 0x4F), 0x37: (0x80, 0x50), 0x38: (0x80, 0x55), 0x46: (0x76, None),
                              0x47: (0x71, None)}
GENERIC_SPRITE_SETS = (0x80, 0x81)  # a generic's graphic comes from its job (0x278EDC)
# Moving a unit off a slot moves it everywhere the remaster names it by sprite set, or the remaster finds ReMixed's
# character there instead (or no one). The remaster names a sprite set in three places besides its battle data:
# - Its event instructions whose first parameter byte is a sprite set (REMASTER_SPRITE_SET_OPS): AddGhostUnit (0x47)
#   puts a unit with no battle unit on the map with that sprite set's sheet and face (op 0x47's queue 0x2730C4,
#   Patches/TreasureArrows.cs), and DismissUnit (0x7A, a halfword) takes the roster unit with it out of the party
#   (0x23A868 frees the first roster slot from 1 on with it). Read as the game reads them, the stock scripts name a
#   moved one in three: event 158 at Lionel (entry 0x117) takes the guest Agrias out (DismissUnit 0x34) before she
#   rejoins at Bariaus Valley as 0x1E (entry 0x19D, refused while any roster slot has her sprite set); event 270, the
#   frog scene, walks Rafa in as AddGhostUnit 0x19; event 281 at Riovanes Castle's gate (entry 0x1AF) takes her out as
#   DismissUnit 0x19 before she rejoins as 0x29 (entry 0x1B2). Until 0.9.23 only the two DismissUnits moved (a
#   hand-kept list), and event 270 drew ReMixed's character for Rafa. write_remaster_sprite_sets now moves every one, in
#   each remaster script the mod ships or leaves stock. Not event 281's 0xC3 0x19: in a script the remaster runs
#   that is its own unit lookup for the cards (0x214284, not the upgrade's WorldUnitAddress), which finds the guest
#   Rafa's battle unit by her id (25, "Rapha"), which stays; named 0x29 it found no one and the game closed there
#   (seen in the hidden PC game, 2026-11-19), and as 0x19 it shows her leaving (REMASTER_CODE_SPRITE_SETS below).
# - Its enhanced battle-unit overrides (ENTRY_OVERRIDE_FIELDS, write_entry_overrides): entry 0x1AD's unit 1 is the
#   guest Rafa loaded from the formation as 0x19.
# - Its PS1 portrait table, which RemixedFaces separates (SeparatePortraits).
REMASTER_SPRITE_SET_OPS = {0x47: "AddGhostUnit", 0x7A: "DismissUnit"}
# 0.9.32 to 0.9.38 shipped Riovanes Castle Gate's closing (event 281) with a "guard" around its six battle-end screens
# (DisplayConditions 2-7), written in the upgrade's instructions (If, SETVar, LoadAddress, Else, SaveAddress, EndIf).
# Event 281 is the remaster's own script, which the remaster's interpreter runs, and to it an opcode below 0x10 ends
# the event (0x23A868's default, as END does): the scene stopped at the guard's first byte, after its dark screen came
# up and before the screens, the write-back, the dark screen's removal and DismissUnit, and sat on a black screen for
# good (seen in the hidden PC game with an ordinary guest Rafa, 2026-11-25). The guard's bytes come out again
# (strip_world_unit_screen_guard), wherever a build left them; what it was for is the code mod's now
# (Patches/GuestLookup.cs: a unit the event's lookup can't find is passed over instead of handed on as null).
# The event file -> the bytes put before its first DisplayConditions and after its last screen's wait.
STRIPPED_SCREEN_GUARDS = {"event281.e": (bytes.fromhex("01 AF 02 00 00 00 C1 00 16 00 02 01 01 AF 00 00 02 00"),
                                         bytes.fromhex("03 AE 01 00 FF 00 C4 01 01 00 02 01 04 04"))}
# A save made with an earlier build may hold a guest on its old sprite set, which the moved battle data would no longer
# find: old sprite set -> (the guest's name id, the story progress (var 0x6E) from which the remaster's own event has
# taken the guest out). The code mod moves such a roster record to the new sprite set (and job) as a battle's units
# are made, while the story is before that point; ReMixed's Ashley (0x34) has a name id of her own (ASHLEY_NAME) and
# comes later, at progress 0x25, so a record of hers is never moved (before her name, a test save from chapter 1 would
# have had her guest record moved to Agrias's). Agrias is a guest from progress 0xF (event 117) until event 158 sets
# 0x16; Rafa from 0x26 (event 261) until event 281 (progress 0x28; event 298 sets 0x29).
REMASTER_OLD_GUESTS = {0x34: (0x34, 0x16), 0x19: (0x19, 0x29)}
# The remaster's code names one sprite set itself: event instruction 0xC3 (0x214284) takes a character's battle unit for
# the unit cards that follow, and when the unit can't be found by its id (a crystal, a chest: 0x226A18 answers 2000) it
# looks, in event 0x119 for character 0x19 only (Riovanes Castle Gate's closing, event 281), for the battle unit
# whose sprite set is 0x19, and hands the status panel a null unit when there is none: the game closed at
# exe+0x360086 for a tester whose guest Rafa had become a crystal by the gate battle's end (0.9.38, 2026-11-25). The
# code mod's rule "guest-lookup" writes her moved sprite set into that compare, and has the panel passed over for a
# unit that isn't found at all (Patches/GuestLookup.cs). The same fallback names the face for the cards that follow
# (mov dword [0x2FCF86C], 0x19 at 0x21430C), which the "Guest Departure" card shows when the status panel can't
# name one (a body gone to a crystal): Balthier's in the port, on 0.9.39; the rule writes her sprite set there too. Character id -> the sprite set the code names. A scan of the exe for "cmp byte ptr [reg], a moved sprite set" finds no other
# lookup like it (the other hits compare event opcodes and ranges).
REMASTER_CODE_SPRITE_SETS = {0x19: 0x19}
REMIXED_UNIT_SKILLSETS = {0x4D: 0x16, 0x16: 0xE0, 0x4E: 0xE0}
# A unit's primary skillset (battle data +0x1D; 0 and 0xFF none) is a ReMixed skillset like its secondary, and the
# remaster reads it as vanilla does. ReMixed's battle menu (BATTLE 0x80152750, 0x801818C4) reads its Dark Knight slot's
# skillset (0x16, and a woman's 0x4E) from that slot's learned abilities, which the unit learns by its Dark Knight level,
# and a man's 0x4E as a special skillset, from job index 0's, which the unit setup marks all learned for an override
# (SCUS 0x8005BCAC): its entry 0x1AD's five men know all of Darkness, 0x1B4's eight women what their level 8 buys. The
# remaster reads Darkness (0xE0) from the Dark Knight's index whatever the override, so on the port's Dark Knight the
# first is no override (Darkness is its own skillset) and the second an override of Darkness, which the code mod
# (Patches/JobLevels.cs) marks known in full.
ENTD_PRIMARY = 0x1D
DARK_KNIGHT_SLOT_SKILLSETS = {0x16: (ENTD_MAN, ENTD_WOMAN), 0x4E: (ENTD_WOMAN,)}  # read by level, for these genders


def remixed_primary(skillset: int, job: int, flags: int) -> int:
    """ReMixed's primary skillset override as the port's, for a unit whose port job is `job` (REMIXED_UNIT_SKILLSETS)."""
    if job == DARK_KNIGHT["job"][1] and (flags & (ENTD_MAN | ENTD_WOMAN)) in DARK_KNIGHT_SLOT_SKILLSETS.get(skillset, ()):
        return 0xFF
    return REMIXED_UNIT_SKILLSETS.get(skillset, skillset)
# A unit's job to level (battle data +0x08: that job gets the unit's level, its prerequisites theirs) is a job index,
# and ReMixed's 17 and 18 are its gendered slots: 17 its Dark Knight (with that row's prerequisites, Knight 8 and Black
# Mage 8), 18 a woman's Dancer and a man's Bard (with the slot's one row, the Dancer's). Left as they were, the port
# gave those Dark Knight levels to the Bard and a man's Bard levels to the Dancer (2026-11-18: 51 of the shipped units in
# 11 battles, among them the Dark Knights of ReMixed's entries 0x1AD and 0x1B4 and the story characters of 0x1A8). The
# port's Dark Knight is the remaster's index 20, and its man's Bard index 17; JobLevels reads 0x92 as a man's slot 18
# (the Bard's level, row 18's prerequisites), which the remaster's own setup, taking only indexes under 20, leaves to it.
ENTD_LEVELS_JOB = 8
LEVELS_JOB_DARK_KNIGHT, LEVELS_JOB_MANS_BARD = 0x14, 0x92


def remixed_levels_job(index: int, flags: int) -> int:
    """ReMixed's job-to-level index as the port's (ENTD_LEVELS_JOB): its gendered slots 17 and 18 moved."""
    if index == 17:
        return LEVELS_JOB_DARK_KNIGHT
    if index == 18 and flags & ENTD_GENDER == ENTD_MAN:
        return LEVELS_JOB_MANS_BARD
    return index


ADDED_ENTRIES_FILE = "event_btlevt_bin_entrydata_add_ent.bin"  # entries from 0x200; shipped by earlier builds
REMIXED_SCENARIOS_FILE = "remixed-scenarios.bin"
# ReMixed's missions (its WORLD.BIN, 24 halfwords): entry i's battle (0xFFFF none). Not world-map locations: ReMixed
# lists entry i once flag 0x1A4 + i is set (its story events set them), titled by world message 0x5A8 + i, and Start
# in its text accepts it (docs/analysis/story-flow-plan.md, "ReMixed's missions", has the list and what unlocks each).
MISSION_BATTLES = 0x8013E4BC
WORLD_BASE = 0x800E0000
# ReMixed's missions, as close to ReMixed as the remaster allows: ReMixed lists the unlocked ones under Bonus Content in
# its Brave Story, shows a mission's posting, and Start while it's up starts the battle. The mod posts them among the
# remaster's tavern rumors (the Gossip table: a list and each one's text, the remaster's own screen for that; ReMixed's
# Biggs calls them "odd jobs from all the posters"), listed once ReMixed's story would have unlocked them; Start on a
# posting accepts it, and its battle starts as the player leaves town (RemixedScenarios). Entry i -> (ReMixed's
# scenario, the story progress (var 0x6E) from which it's posted, or None, and a flag that posts it too, or 0). The
# progress is where ReMixed's own event that unlocks it runs in the remaster's story (its map, matched with the
# remaster's world-map starts); docs/analysis/story-flow-plan.md, "ReMixed's missions".
MISSIONS = {2: (0xA8, 15, 0x1B7),   # Chocobo Defense: Biggs and Wedge's scene (it sets 0x1B7), else chapter 2
            3: (0xA9, 15, 0),       # Chicken Race: chapter 2's start
            4: (0xAA, 15, 0),       # Treasure Hunt: chapter 2's start
            5: (0xAB, 19, 0),       # Teioh: Zaland's scene (18)
            6: (0xAC, 24, 0),       # Lost Heirloom: Goug's (23)
            7: (0xAD, 31, 0),       # The Ball: chapter 3's start
            8: (0xAE, 35, 0),       # Desert Minefield: Orbonne's vaults (34)
            9: (0xAF, 39, 0),       # Littering: Yardrow's (38)
            10: (0xB0, 41, 0),      # Shades of the Past: Riovanes's (40)
            11: (0xB1, 47, 0),      # The Shrine Knights: Fort Besselat's (about 46)
            12: (0xB5, 53, 0),      # All-Star Melee: Mullonde Cathedral's (ReMixed's 0x074 sets 53)
            # ReMixed posts these after its final battle (its 0x07B sets 0x1B1-0x1B6), which the remaster's story never
            # runs; the mod posts them late in chapter 4 too (asked 2026-11-07): from progress 53, the party's last
            # stretch on the world map, after Mullonde's battles (the remaster's last world-map starts, at 52). Only
            # those seen ending on the PC so far; the others end by scripted conditions on particular units and a
            # turn counter, which the harness's defeat-them-all can't meet, so they wait for a real play-through.
            13: (0xB6, 53, 0x1B1),  # The Guarded Temple: late chapter 4, or ReMixed's post-game unlock
            14: (0xB7, 53, 0x1B2),  # Nightmares: late chapter 4, or ReMixed's post-game unlock
            15: (0xB8, 53, 0x1B3),  # Brave Story: late chapter 4, or ReMixed's post-game unlock
            16: (0xC2, 53, 0x1B4),  # An Ill Wind (seen to its end on the PC; its units are broken stand-ins)
            18: (0xC5, 53, 0x1B6),  # Of Vagrants (seen from its posting to its end on the PC)
            19: (0xC6, 15, 0x1B7)}  # Be A Beta: Biggs and Wedge's scene too, else chapter 2's start
# Mirror Match (MIRROR_MATCH), posted late in chapter 4 with An Ill Wind and Of Vagrants: its preset (0xC3) has their
# sets (the opening, the Preparation Menu, and the plain victory test, type 0x16, for its closing). Added last, so the
# other postings keep the Gossip rows earlier builds gave them. Not seen in the game yet.
MISSIONS[17] = (0xC4, 53, 0x1B5)
# The postings' Gossip rows, from 23 (the bar lists rumors 0-63). The game finds a rumor's row by its place in the table,
# not its key (seen on the PC: postings from 24 showed the row after theirs), so they go straight after the game's 23.
MISSION_RUMORS = 23
MISSION_TEXTS = 0x5A8  # ReMixed's WORLD/WLDMES.BIN: entry i's posting is message 0x5A8 + i
MISSION_TITLE = "{}"  # the posting's title in the Tavern's Missions list (it said "Mission: " among the rumors)
# ReMixed's last line of each posting, and the mod's: its battle starts once the world map is back (the code mod's
# RemixedScenarios.Accepted), as started under the tavern's screens they stayed drawn over its formation. ReMixed takes
# a mission with Start; the mod with the confirm button and a Yes/No (MissionPrompt, asked 2026-11-19), as the Deck
# holds Start to skip scenes.
MISSION_ACCEPT = ("(Press Start to accept mission.)", "(Select this mission to accept it. It begins as you leave town.)")
# Entry 1 of ReMixed's list is its dispatch tavern (TAVERN): its posting has no title line (the list names it; the
# Preparation Menu calls it Dispatch Missions). It's never listed (RemixedScenarios.RumorShown, asked 2026-11-18): the
# Tavern menu's Dispatch Missions button (TAVERN_MENU_ROWS) opens the native dispatch view instead.
TAVERN_POSTING = 1
TAVERN_TITLE = "Dispatch Missions"
TAVERN_NAME_CHARS = 40  # the tavern's lines this short with no speaker are names that go into others' slots
TAVERN_ACCEPT = ("(Press Start to go to the tavern.)", "(Choose Dispatch Missions in the Tavern's menu.)")
# Still a posting, unlocked with Chocobo Defense as in ReMixed, and last, so the missions' rows stay where earlier
# builds put them: its unlock is the Tavern button's, locked ("???") until then.
TAVERN_POSTED = True
if TAVERN_POSTED:
    MISSIONS[TAVERN_POSTING] = (TAVERN_SCENE, 15, 0x1B7)
# ReMixed's ordinary rumor 0x1C points to the chapter-3 Fort Zeakden branch. It is not a mission posting: Start on it
# does nothing. The code mod lists it only for the same window in which the branch can start: chapter 3 on, Hirame
# saved, the branch still open, and outside the Algus campaign. It follows the postings so their shipped row numbers
# remain stable.
HIRAME_RUMOR = MISSION_RUMORS + len(MISSIONS)
HIRAME_RUMOR_MESSAGE = 0x1C
HIRAME_RUMOR_TITLE = "Squatters at Fort Zeakden"
CONDITIONAL_RUMORS = {HIRAME_RUMOR: (0x22, CHAPTER_3_BRANCH_OPEN, HIRAME_LOST, ALGUS_FLAG)}
# ReMixed's events read a mission's unlock (flag 0x1A4 + entry, which its story events set) as the story's stage: the
# Treasure Wheel's gil a chest (500 to 10,000), its loot's least tier, its gold chests (from Shades of the Past's
# unlock) and its cash-in for JP (from All-Star Melee's), and Be A Beta's loot set. The remaster's story never sets
# them (its own event 117 sets 0x1A4-0x1AA for something else), so the Wheel stayed at its lowest stage until then and
# jumped to the fifth there. Each read of the unlock of a mission posted from a progress (MISSIONS) reads the story
# instead: var = var 0x6E / that progress, nonzero once the mission is posted, other variables untouched.
STORY_PROGRESS = 0x6E
MISSION_UNLOCK_PROGRESS = {0x1A4 + entry: progress for entry, (_, progress, _) in MISSIONS.items() if progress}


def mission_unlock_reads(code: bytes, lengths: bytes) -> list[tuple[bytes, bytes]]:
    """Patches (location_scenario_code's) for an event's reads of missions' unlocks (SET_VAR var, flag): the story's
    progress over the mission's, as the mod posts it (MISSION_UNLOCK_PROGRESS)."""
    out, at = [], 0
    while at < len(code):
        op = code[at]
        ins = bytes(code[at:at + 1 + lengths[op]])
        if op == 0xAF and len(ins) == 5 and u16(ins, 3) in MISSION_UNLOCK_PROGRESS:
            var = ins[1:3]
            out.append((ins, bytes([0xAF]) + var + STORY_PROGRESS.to_bytes(2, "little")  # SET_VAR var, var 0x6E
                        + bytes([0xB6]) + var + MISSION_UNLOCK_PROGRESS[u16(ins, 3)].to_bytes(2, "little")))  # DIV
        if op == 0xDB and at + len(ins) >= len(code):
            break
        at += len(ins)
    return out


# Lines of ReMixed's that the mod words its own way, by ReMixed's event: (part of the line as converted, the mod's). Biggs
# and Wedge's bartender (0x0A4, which unlocks the first mission) sends the player to Bonus Content in the Brave Story;
# the mod posts the missions in the taverns.
LINE_CHANGES = {0xA4: {"Check out \"Bonus Content\" in the Brave Story if you're thinking about giving them a try!":
                       "Look over the Missions in any tavern if you're thinking about giving them a try!",
                       "New Bonus Content unlocked! Check it out in the Brave Story.":
                       "Rendezvous missions are now posted under Missions in taverns."},
                WHEEL: {"Start: Cash In": "Triangle: Cash In"}}
# Lines that only size a box, by event and message number: the tavern's picker opens its card's box with message 0x25
# ("1234567890123456789012345", its message written in as it runs), and the remaster sizes a box by the line its
# MESSAGE names: as a card of three lines, so the box is one of three lines too.
SIZING_LINES = {0xE6: {0x25: "<color=61>99 </color>Abcdefghijklmn Lv. 99\n    Abcdefghijklmnopq\n"
                             "<color=116><icon=450></color>  Brave 100   Faith 100"}}
# The tavern's six kinds' events (0x0DF-0x0E4) show a mission's posting (message 11 + 3j) and its report (12 + 3j) in
# a box of their own under the Tavernmaster's (style 0x61), each MESSAGE naming message 0xFFFF and the real number
# written in as it runs: with no line to size it by, the box had no width (as the reports' did, EVENT_CODE_PATCHES).
# So it names the event's widest posting or report (kind_box_patch). Special Training and Contests (0x0E7) show a
# course's or contest's description in the same box: there, its widest line that no one speaks and that isn't a menu
# or a name.
KIND_EVENTS = range(0xDF, 0xE5)
TRAINING_EVENT = 0xE7
KIND_BOX = bytes.fromhex("10 10 61 ff ff 00 00 00 00 00 4a 00 00 00 00")
# As a narration (style 0x01, the remaster's frameless layer) that box showed as bare text that went on without a
# press (seen on the PC in the regression pass, 2026-11-12, beside work/psx/run/shots rmg-00080). On the PS1 it's a
# framed box under the Tavernmaster's, about centred, its bottom about 78% down the screen, until a press. So, as the
# report's gil box: the bottom window's box 2 with no tail, centred and raised 160 (EVENT_BOXES), closed after the
# press that follows it (KIND_BOX_CLOSE): the event's own close is box 1's, and the lists it goes back to open in box 2.
KIND_BOX_PLACE = (0x9A, 0, 160, 1)
EVENT_BOXES.update({event: {0x61: KIND_BOX_PLACE} for event in (*KIND_EVENTS, TRAINING_EVENT)})
KIND_BOX_CLOSE = bytes.fromhex("51 02 ff ff 00 00")


def close_kind_boxes(code: bytes, box: bytes, expected: int) -> bytes:
    """ReMixed's event code (the PS1 layout) with box 2 closed after each press that follows the info box's MESSAGE
    (box: its bytes as patched; KIND_BOX_CLOSE)."""
    shown = box + WAIT_FOR_MESSAGES
    if code.count(shown) != expected:
        sys.exit(f"a tavern info box shows {code.count(shown)} times, not {expected} (KIND_BOX_CLOSE)")
    return code.replace(shown, shown + KIND_BOX_CLOSE)


def kind_box_patch(lines: list[tuple[str, str]], training: bool = False) -> tuple[bytes, bytes]:
    """The kinds' events' posting and report box (KIND_BOX) naming its widest line among the postings and reports
    (with training, 0x0E7's descriptions): the longest row (without markup), then the most rows."""
    def size(n: int) -> tuple[int, int]:
        rows = re.sub(r"<[^>]*>", "", lines[n - 1][1]).split("\n")
        return max(len(row) for row in rows), len(rows)
    if training:
        shown = [n for n, (speaker, text) in enumerate(lines, 1)
                 if not speaker and "<cursor>" not in text and len(text) > TAVERN_NAME_CHARS]
    else:
        shown = [n for j in range(len(lines)) for n in (11 + 3 * j, 12 + 3 * j) if n <= len(lines) and lines[n - 1][1]]
    if not shown:
        sys.exit("a tavern kind's event has no postings (KIND_EVENTS)")
    widest = max(shown, key=size)
    return KIND_BOX, KIND_BOX[:3] + widest.to_bytes(2, "little") + KIND_BOX[5:]


def mission_posting(message: bytes, title: str | None = None,
                    accept: tuple[str, str] = MISSION_ACCEPT) -> tuple[str, list[str]]:
    """ReMixed's posting (a world message): its title and its paragraphs, as the remaster's rumors write them. Its lines
    are F8-separated: the first is the title (unless one is given: the tavern's, TAVERN_POSTING), a blank one starts a
    paragraph, one starting "-" is who posted it, and the last is "(Press Start to accept mission.)", which says when
    the mod starts it too (MISSION_ACCEPT; the tavern's, TAVERN_ACCEPT)."""
    lines = [" ".join((speaker or text).split()) for speaker, text in map(remixed_line, message.split(b"\xf8"))]
    if title is None:
        title, lines = lines[0], lines[1:]
    paragraphs, current = [], []
    for line in lines:
        if line.startswith("-") or line == accept[0]:  # who posted it, or the last line (the tavern's has no blank)
            if current:
                paragraphs.append(" ".join(current))
                current = []
            paragraphs.append(line if line == accept[0] else f"—{line.lstrip('-').strip()}")
        elif not line:
            if current:
                paragraphs.append(" ".join(current))
                current = []
        else:
            current.append(line)
    if current:
        paragraphs.append(" ".join(current))
    if paragraphs[-1:] != [accept[0]]:
        raise SystemExit(f"ReMixed's posting {title} doesn't end with {accept[0]}")
    return title, paragraphs[:-1] + [accept[1]]


def write_mission_postings(cfg: dict) -> str:
    """ReMixed's missions (MISSIONS) as postings among the remaster's tavern rumors, plus the Fort Zeakden rumor
    (CONDITIONAL_RUMORS). Which rows the bar lists, and Start on a posting, are the code mod's (RemixedScenarios,
    from the scenarios file). Returns a line for the report."""
    with Disc(remixed_bin(cfg)) as disc:
        messages = disc.read(disc.find("WORLD/WLDMES.BIN")).split(b"\xfe")
    rows, titles = {}, []
    for k, i in enumerate(MISSIONS):
        if i == TAVERN_POSTING:
            title, paragraphs = mission_posting(messages[MISSION_TEXTS + i], TAVERN_TITLE, TAVERN_ACCEPT)
        else:
            title, paragraphs = mission_posting(messages[MISSION_TEXTS + i])
        # No action of the row's own (+4 and +8 zero): a rumor's action is the remaster's sidequest progress (it sets
        # the counter +4 names to +8 and tells its listeners: seen on the PC, a toast and a speech box for Ramza).
        rows[MISSION_RUMORS + k] = {"DLCFlags": 0, "Unknown4": 0, "Unknown8": 0, "Unknown14": "[]", "Unknown1C": "[]",
                                    "UserSituationID": 0, "IconId": 0, "UnkBool2C": 0,
                                    "Title": title if i == TAVERN_POSTING else MISSION_TITLE.format(title),
                                    "Text": "\n\n".join(paragraphs)}
        titles.append(title)
    _, rumor_text = remixed_line(messages[HIRAME_RUMOR_MESSAGE])
    rows[HIRAME_RUMOR] = {"DLCFlags": 0, "Unknown4": 0, "Unknown8": 0, "Unknown14": "[]", "Unknown1C": "[]",
                           "UserSituationID": 0, "IconId": 0, "UnkBool2C": 0,
                           "Title": HIRAME_RUMOR_TITLE, "Text": rumor_text}
    if max(rows) >= 64:
        raise SystemExit("the bar lists rumors 0-63; the postings don't fit")
    line = add_nxd_rows(cfg, "gossip.en", "Gossip-en", rows, "0004.en.pac")
    return f"{len(MISSIONS)} postings ({', '.join(titles)}) and {HIRAME_RUMOR_TITLE}; {line}"


# ROUND_SLOTS: the battle entries of the missions in rounds (MISSION_ROUND_CHAINS) go in runs of consecutive slots, one
# run a mission in ReMixed's order, taken from the first free slots before any other entry's, so that the events'
# ADD var 0x32, 1 steps from one round's slot to the next's as it steps from one of ReMixed's entries to the next.
def round_slots(records: dict[int, bytes], free: list[int]) -> dict[int, int]:
    """ReMixed's battle entries of the missions in rounds -> their slots: each mission's a run of consecutive free slots
    (the first long enough), taken out of free (ascending)."""
    out: dict[int, int] = {}
    for first, rounds in MISSION_ROUND_CHAINS.items():
        if set(rounds) - set(MISSION_ROUNDS) or first not in REMIXED_SCENARIOS:
            sys.exit(f"mission 0x{first:X}'s rounds aren't all shipped (MISSION_ROUND_CHAINS, MISSION_ROUNDS)")
        entries = [u16(records[s], 7) for s in (first, *rounds)]
        if entries != list(range(entries[0], entries[0] + len(entries))):
            sys.exit(f"ReMixed's mission 0x{first:X}: its rounds' entries aren't consecutive "
                     f"({', '.join(f'0x{e:X}' for e in entries)}; MISSION_ROUND_CHAINS)")
        n = len(entries)
        start = next((k for k in range(len(free) - n + 1) if free[k + n - 1] - free[k] == n - 1), None)
        if start is None:
            sys.exit(f"no {n} consecutive free battle entries for mission 0x{first:X}'s rounds (ROUND_SLOTS)")
        out |= dict(zip(entries, free[start:start + n]))
        del free[start:start + n]
    if set(MISSION_ROUNDS) != {r for rounds in MISSION_ROUND_CHAINS.values() for r in rounds}:
        sys.exit("MISSION_ROUNDS and MISSION_ROUND_CHAINS name different rounds")
    return out


def remixed_preset_targets(preset: bytes) -> list[tuple[int, int, int]]:
    """(set start, set end, event) for each condition set of a preset (0x20 bytes of byte offsets, then records of
    halfwords) whose last record runs an event (0x19, the event): its conditions come before it."""
    words = [u16(preset, REMIXED_PRESET_OFFSETS + 2 * k) for k in range((REMIXED_PRESET_BYTES - REMIXED_PRESET_OFFSETS) // 2)]
    starts = sorted({u16(preset, 2 * k) // 2 for k in range(16) if k == 0 or u16(preset, 2 * k)})
    used = max((k for k, w in enumerate(words) if w), default=-1) + 1
    out = []
    for i, start in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else used
        if end - start >= 2 and words[end - 2] == 0x19:
            out.append((start, end, words[end - 1]))
    return out


# A set of ReMixed's that runs an event the mod doesn't ship is turned off by its first record when that's a comparison.
# Until 2026-11-12 only a set of one comparison was, and a preset with a longer such set counted unsafe, so its battle
# loaded the empty preset: the Algus campaign's last battle at Fort Zeakden (0x102, preset 0x101) had no conditions at
# all, neither its victory (all enemies down: 0x102's block 0b, on to 0x103) nor Algus falling (block 0a: give up or
# fight on), so it could never end. That preset's other sets are the main story's: its lines on Ramza's and Delita's
# turns (ReMixed's own preset for that battle, 0x18, runs 0x0A5 there) here run 0x019 (Golagros's scene, which has no
# block for them) and 0x01A (the end of chapter 1, which would end the campaign without giving the roster back). Each
# begins with a comparison, so they're off now and the preset is safe. Of the presets the shipped events load, only
# 0x101 changed (read 2026-11-12).
def renumber_presets(presets: bytes, events: dict[int, int]) -> tuple[bytes, set[int]]:
    """ReMixed's presets with the events they run renumbered as the mod ships them. A set that runs an event the mod
    doesn't ship gets a condition that can't hold when its first record is a comparison (type 1: var or flag = value;
    the value becomes 0xFFFF; a set fires only when all its records hold, RemixedScenarios.WalkSets); the presets with
    other such sets are returned, for the events that load them to load none."""
    out = bytearray(presets)
    unsafe = set()
    for n in range(REMIXED_PRESETS):
        base = n * REMIXED_PRESET_BYTES
        for start, end, event in remixed_preset_targets(out[base:base + REMIXED_PRESET_BYTES]):
            at = base + REMIXED_PRESET_OFFSETS + 2 * start
            if event in events:
                out[at + 2 * (end - start - 1):at + 2 * (end - start)] = events[event].to_bytes(2, "little")
                if event == AFTER_BATTLE_EVENT:
                    unsafe.add(n)  # its battle-end use stays out of the mod's battles (AFTER_BATTLE_EVENT)
            elif u16(out, at) == 1:
                out[at + 4:at + 6] = (0xFFFF).to_bytes(2, "little")
            else:
                unsafe.add(n)
    return bytes(out), unsafe


# What ReMixed's after-battle event 8 goes on to: its map-settings word's three bytes, set in vars 0x3A (the mode:
# 0x80 the world map, 0x81 the next scenario), 0x3B (the scenario's low byte) and 0x3C (its high bits) before the
# battle's event hands on to event 8.
AFTER_BATTLE_VARS = (0x3A, 0x3B, 0x3C)
# Where a battle's event sets those vars under an IF (0x10E: on to 0x06E only when Zalbag lives), the hand-off to the
# scenario they name goes under this test of var 0x3A as the event runs (SET_VAR var 0, var 0x3A; SET var 1, 0x81; EQ;
# IF), with the world map after an ELSE; set unconditionally, the hand-off is fixed as before.
AFTER_BATTLE_CHOSEN = bytes([0xAF, 0x00, 0x00, AFTER_BATTLE_VARS[0], 0x00, 0xAE, 0x01, 0x00, 0x81, 0x00, 0xA2, 0x01])
# Likewise a return to the battle's conditions (AFTER_BATTLE_RETURN) set under an IF, with the world map on the other
# path (0x01D: Luso not in the party or in it): the preset loaded, and the word, are chosen by var 0x3C (SET_VAR var 0, var
# 0x3C; SET var 1, 0xFFFF; EQ; IF).
RETURN_CHOSEN = bytes([0xAF, 0x00, 0x00, AFTER_BATTLE_VARS[2], 0x00, 0xAE, 0x01, 0x00, 0xFF, 0xFF, 0xA2, 0x01])
# ReMixed's battles hand on to its after-battle event 8 (results and loot), which the mod doesn't ship. Where event 8
# would have gone on to one of ReMixed's scenarios the mod ships, the hand-off goes there (0x81, as the remaster's own
# closing scenes do, e.g. 0x006 to 0x007); otherwise back to the world map (0x80, as 28 of the remaster's closings do;
# docs/analysis/story-flow-plan.md, "The battle's end"). With a word of 0 the battle goes on with its enemies down.
# ReMixed sets flag 0x8E there too (for its after-battle chain), which with 0x81 offered the next scenario as a battle
# ("Enemy encountered!"); the hand-off clears it.
AFTER_BATTLE_FLAG = 0x8E
# Event 8 is a scene too: its opening is Dycedarg's, which the Algus campaign ships (0x008 as 0x307), and its block
# 0xFE the after-battle event a battle's preset 7 runs (as var 1 skips the opening). Preset 7 does both: a battle's
# end loads it (0xCE 7), and 0x008's record names it (+0x16), its first set "var 0x1FD = 1 → event 8" starting the
# scene's opening. So it's renumbered (0x307) for the record, and still counted unsafe (renumber_presets), so no battle
# of the mod's loads it at its end: once the campaign shipped 0x008, every ReMixed battle's end ran 0x307's
# after-battle block, on to the remaster's own scenario its vars named (seen on the PC: 0x006's battle on to the
# remaster's 7 and 8); left unrenumbered, 0x307 started as a bare battle with no opening (seen on the PC).
AFTER_BATTLE_EVENT = 0x008
WORLD_MAP_WORD = (0x80, 0, 0)  # a map-settings word's three bytes (0xCD p0 p1 p2): back to the world map
# ReMixed scenarios deliberately omitted from the port whose hand-offs should become a return to the world map.
# This used to contain 0x0A2, before the Depths of Murond were shipped as DEPTHS_OF_MUROND.
WORLD_MAP_ENDS: set[int] = set()
# ReMixed's 0x103 hands on to its final battle by SET var 0x27, 0x07B, then 0xCD 82 00 00. The remaster's scenario end
# (0x23987C) sets var 0x27 from the word alone (its scenario bits, 0 here) for every mode, and for 0x82 goes on to a
# screen of its own (it returns 0xB: game mode 0x55, 0x20871C, a dialog; the remaster's own 20 scenarios with 0x82 all
# name no scenario), so the battle was never reached. Such a hand-off goes to the scenario var 0x27 named, as 0x81 does.
NEXT_BY_VAR_MODE = 0x82
# Event 8's end also sets flag 0xD4, which brings the dispatch tavern's groups home (TAVERN: the scene finishes every
# group out while it's set, the next time it opens). The remaster's own battles set it through the code mod (a won
# battle's write-back, Patches/TavernStorage.cs); ReMixed's hand on to event 8 instead and never reach that write-back
# (seen on the PC, 2026-11-12: a mission won, on through the Treasure Wheel, left the flag clear), so their hand-off
# sets it where event 8 would have (SET flag 0xD4, 1), on each of its paths.
TAVERN_HOME = bytes([0xAE, 0xD4, 0x00, 0x01, 0x00])
# Event 8 also writes the battle's units back into the roster (instruction 0x78 7, then 0xE5 0x38 to wait for it:
# REQUIRE.OUT's write-back on the PS1, the remaster's 0x2CB9E8 state 7 -> 0x2C8818 -> 0x2C60E4, which every one of the
# remaster's own battle closings runs). That's what saves a guest the battle lists with "saves its formation" (flag
# 0x01) into the roster's guest slots, for a later scene that loads it (flag 0x08). The Lich's battle (0x03F) lists
# Ashley so (flags 0x93: joins, saves; battle id 0x5B), and her scene (0x040) loads her from the roster and has her
# join (0x98, then 0x78 7 itself): without event 8 the battle never wrote her back, so the scene found no guest, left
# her unit empty (a black figure) and nothing joined (seen on the PC, 2026-11-12; the log has no write-back between the
# battle and the scene). And it's what keeps a battle's EXP, JP, levels, learned abilities, a unit lost for good and
# an invited one: without it every ReMixed battle left the roster as it was before the battle (2026-11-13). Event 8
# runs it on every way through its after-battle block (read 2026-11-13: its block 0xFE, 0x78 2-6 at 0x10385, 0x78 7 at
# 0x106DE, then the hand-off at label 0xA0, the return to a battle's preset too), so each hand-off that stands for it
# (location_scenario_code: on to the next scenario or the world map, AFTER_BATTLE_RETURN and RETURN_CHOSEN) runs its
# five results states, the write-back, then sets TAVERN_HOME's flag, in event 8's order. The battles that return to
# their own events have
# the write-back as the battle is won, before the preset's post-battle scene, as on the PS1; those whose scene writes
# back again (0x0EA's 0x0EB, 0x01D's 0x107, 0x11B's second run) write back twice, as on the PS1. On the PS1 event 8
# runs once the battle's event has ended, and here the write-back comes at the hand-off, before the rest of that path:
# story progress and flags in all but 0x006, whose Argath path then makes Delita join at once (0x43 1, which writes
# him into the roster itself) and clears the join flags it set; its three generics marked "joins" (0x91-0x93) aren't
# in the battle (their data's presence byte is 0), so nothing joins twice (read 2026-11-13). The remaster runs display
# states 2-6 before state 7 in every stock battle close. Omitting them made ReMixed/random battles switch to a black
# renderer as soon as the last enemy fell, then show their result UI over black. Preserve that native order.
AFTER_BATTLE_RESULTS = b"".join(bytes([0x78, state, 0x3C, 0xE5, 0x38, 0x00]) for state in range(2, 7))
AFTER_BATTLE_WRITE_BACK = bytes([0x78, 0x07, 0x3C, 0xE5, 0x38, 0x00])
AFTER_BATTLE_HOME = AFTER_BATTLE_RESULTS + AFTER_BATTLE_WRITE_BACK + TAVERN_HOME
# A same-battlefield return must write the roster without entering the remaster's display state 7, which tears down
# the live map before the returned preset's post-battle event begins. 0xC5 is a mod-owned EIU native routine address,
# audited globally below and registered as RemixedEvents.LiveBattleWriteBack.
LIVE_BATTLE_WRITE_BACK_ADDRESS = 0x000000C5
AFTER_BATTLE_LIVE_WRITE_BACK = bytes([0xCF]) + LIVE_BATTLE_WRITE_BACK_ADDRESS.to_bytes(4, "little")
AFTER_BATTLE_RETURN_HOME = AFTER_BATTLE_RESULTS + AFTER_BATTLE_LIVE_WRITE_BACK + TAVERN_HOME
# Luso's two battles return to a recruitment scene, whose own display state 7 (MIDSCENE_JOIN_SCREENS) writes the
# battle back and shows each joiner's join screen. Show the results at the victory, but leave the writer to that join.
AFTER_BATTLE_DEFERRED_HOME = AFTER_BATTLE_RESULTS + TAVERN_HOME
DEFER_RETURN_WRITE_BACK = {0x01D, 0x0EA}
# A battle unit the write-back saves for a later scene (ENTD flags: 0x10 joins, 0x01 saves it, into the roster's guest
# slots): its battle must hand on with the write-back (0x03F's Ashley), or the scene finds no one.
ENTD_JOINS, ENTD_SAVES = 0x10, 0x01
# Some of ReMixed's battles return to their own events after event 8: their victory sets mode 0 (var 0x3A), var 0x3C to
# 0xFFFF and flag 0x8E, and puts a preset's number in var 0x3B; event 8 then loads that preset (its case 0xFFFF: 0xCE
# with the number), whose set "flag 0x8E = 1" runs the battle's post-battle scene (0x115's, 0x112's, 0x114's and 0x119's;
# 0x115 also jumps there itself with the flag set). The mod ships no event 8, and those battles went straight back to
# the world map without that scene and the story progress it sets (seen on the PC: 0x0F5's battle left the progress at
# 8). So the victory loads that preset itself, the word stays 0 (the battle goes on, its enemies down) and the preset
# runs the scene; the scene's own hand-off to the world map clears the flag (it isn't event 8's to clear any more).
AFTER_BATTLE_RETURN = 0xFFFF
# ReMixed's Preparation Menu (its 0x192: start the battle, view the map, reveal treasures, save, back to the world
# map), which its side chapters' battles hand on to from their opening (0xCD 81 92 01) and which takes their
# formations from vars 0x34-0x36. The remaster has its own formation screen, so the mod ships no menu: a battle's
# formations go in its record (opening_formations) and the hand-off is dropped (its word stays 0, so the battle goes on
# after the opening, as a mission's does).
PREPARATION_MENU = 0x192
FORMATION_VARS = (0x35, 0x36)  # the formations ReMixed's code sets for the menu, as the remaster's setup reads them
# ReMixed's battles that play a scene on the battle's own map before its Preparation Menu: the scene (and its question
# to fight, if any) runs first, its end hands on to the menu, the menu's Start Battle sets the battle up again (the
# formation, the units made afresh from the battle data) and runs the scenario's event once more, which skips the scene
# (its opening keeps var 0x3A in var 0x18, and the menu sets var 0x3A to the event it noted, SET_VAR var 0x3E, var 0x27,
# so var 0x18 = var 0x27: GOTO ff) and the battle begins (seen on the PS1, 0x0F0: scene, question, formation, "Conditions
# for Winning", the menu). With the hand-off dropped the port showed the formation first and played the scene inside the
# battle, where the scene's actors stayed on the field (Agrias's fought 0x0F0's rescue as a guest; seen on the PC,
# 2026-11-12). So these scenarios' records have no formations and the menu's hand-off goes to the scenario itself;
# the code mod sets that second run up as the menu does (RemixedScenarios.MenuBattles: the formations, var 0x3A = var
# 0x3E). ReMixed's event -> the scenario its scene hands on to (0x113, 0x0F1 in the PSP script, runs under 0x0F1's).
MENU_BATTLES = {0xEE: 0xEE, 0xEF: 0xEF, 0xF0: 0xF0, 0xF1: 0xF1, 0x113: 0xF1, 0xF5: 0xF5, 0xF7: 0xF7, 0xFA: 0xFA,
                0x116: 0x116}
# The Zalbag and Algus campaign's battles have the same scene, hand-off and check, on the scene's own map (none sets
# another battle entry or map in vars 0x32/0x33): 0x00A, 0x00B, 0x00D, 0x010, 0x0FF-0x102 (0x104, the same, isn't
# shipped yet). 0x0FF-0x101 have two groups, 2 + 4: six places, which the remaster takes with the code mod's
# "six-places" (see MENU_SECOND_GROUP). 0x00D (2 + 2) keeps its formation first.
MENU_BATTLES |= {e: e for e in (0x00A, 0x00B, 0x010, 0x0FF, 0x100, 0x101, 0x102)}
MENU_BATTLES |= {0x01D: 0x01D}  # Luso at Araguay Woods (ARAGUAY_LUSO): one group, formation 0x10D
# Where the scene's hand-off goes: ReMixed's comes before its check (var 0x18 = var 0x27: GOTO ff), so its battle run
# would hand on again at its END (the battle didn't begin: the scene played a third time, seen on the PC). The hand-off
# stays dropped there (the word 0, so the battle run's END begins the battle) and a new one goes after the event's note
# of itself (SET_VAR var 0x3E, var 0x27), which only the scene's run reaches.
MENU_NOTE = bytes([0xAF, 0x3E, 0x00, 0x27, 0x00])
# The battle's run skips its scene at ReMixed's check (SET_VAR var 0, var 0x18; SET_VAR var 1, var 0x27; EQ; IF; GOTO
# ff), and with it the scene's fade-in (0x4D), while the scene's run ended faded out: on the PS1 the menu fades the
# picture in as it opens over the battle (0x4D 1E), but in the port nothing did, and the battle's later scenes played
# on a black screen (0x112's, seen on the PC). The battle's run fades in as the menu does, before its GOTO ff. The menu
# then waits on the player, so its fade is over long before the battle is set up again; the battle's run ended right
# after it, the battle began mid-fade, and its first turn crashed (exe+0x317386: the move range read with no acting
# unit; three runs of 0x0F0's battle, never without the fade-in): so it waits out the fade (0xF1, 30 frames) first.
MENU_CHECK = bytes([0xAF, 0x00, 0x00, 0x18, 0x00, 0xAF, 0x01, 0x00, 0x27, 0x00, 0xA2, 0x01])
MENU_FADE_IN = bytes([0x4D, 0x1E, 0xF1, 0x1E, 0x00])
# ReMixed's riders: in its battles a unit with battle id 0xC0, 0xC2, ... 0xCE rides the one numbered after it (its
# mount), seated by ReMixed's random battles' setup (0x191), which runs as such a battle begins (one handed to its
# Preparation Menu, 0x192, has no conditions of its own then): it reads each mount's tile (UNIT_ADDRESS mount -> var
# 0x70, PEEK var n = its +0x47, x and y) and walks each rider onto it (PATCH_NEXT those into a WalkTo, 0x28, speed
# 0x63). A scene may seat them too (1-A's river camp, 0x0EE), but the formation sets the battle's units up afresh,
# unmounted ("Intro event > deployment > random battle event, normally. They have to mount twice", ReMixed's
# author). The port never runs 0x191 there, so the river camp's Mindflayers began its battle off their chocobos (a
# tester, 2026-11-18), and 0x0FB's goblins theirs. The battle's run (MENU_BATTLES' check, BATTLE_SPLITS' battle copy)
# now seats the pairs its battle entry has, as 0x191 does, and zeroes the variables, as 0x191 leaves them.
UNIT_ADDRESS, PEEK, PATCH_NEXT, WALK_TO, ZERO_VAR = 0xC2, 0xC1, 0xE6, 0x28, 0xBE
RIDERS = range(0xC0, 0xD0, 2)
SEAT_SPEED = 0x63


def seat_riders(battle_ids: set[int]) -> bytes:
    """ReMixed's 0x191's seating of riders on their mounts (RIDERS), for the pairs among battle_ids; b"" for none."""
    pairs = [(rider, rider + 1) for rider in RIDERS if rider in battle_ids and rider + 1 in battle_ids]
    read = b"".join(bytes([UNIT_ADDRESS, mount, 0x70, PEEK, 0x71 + k, 0x47, 0, 0x70, 2])
                    for k, (_, mount) in enumerate(pairs))
    walk = b"".join(bytes([PATCH_NEXT, 0x42, 0x71 + k, WALK_TO, rider, 0, 0, 0, 0, 0, SEAT_SPEED, 1])
                    for k, (rider, _) in enumerate(pairs))
    zero = b"".join(bytes([ZERO_VAR, var, 0]) for var in [0x70, *range(0x71, 0x71 + len(pairs))])
    return read + walk + zero if pairs else b""


# Who joins, and the join screen. The remaster has two ways to make a battle's flagged units (+6 & 0x10) join:
# - Event instruction 0x43 1 (vanilla's CallFunction 1; 0x227458, RosterGuard): writes each one into the roster at
#   once, silently. ReMixed uses it where it means to (0x006's Delita).
# - Display state 7 (0x78 7, then the wait 0xE5 0x38: 0x2CB9E8 -> 0x2C8818). It writes the battle back (0x2C60E4),
#   which queues the joiners not in the roster yet and returns how many. With none it ends there. With some it parks
#   task 1 (its 0x800-byte context saved, the task made inactive), plays the join fanfare, shows each joiner's "New
#   Party Member" screen (the PS1's "JOIN UP!" over the unit's status) and writes them on Accept, then restores task 1
#   and makes it active again. Its exit sets the battle-renderer exit flag (0x2FF9E00) to 1, leaving a continuing
#   scene's map black. Each midscene join calls routine C7 after its wait to restore the live field.
# ReMixed makes its recruits join through state 7 in a dark screen, often continuing on the same map. This includes
# Ashley after the Lich (0x040: two), the Depths hub (0x0A3), Dycedarg's scene (0x008), the final battle (0x07B),
# Luso at Mandalia (0x0EB), Luso and Boco after Araguay (0x107), Zalbag at Eagrose (0x06E: three), and the
# chapter-3 branch (0x11B). From 2026-11-16 the build made four of these 0x43 1 and the live writer instead, on the
# belief that state 7 destroys
# the map; that was seen while battles' exits still jumped to state 7 without the results states 2-6 first
# (AFTER_BATTLE_RESULTS, fixed 2026-11-17). Every recruit then joined with no screen: the scene ended and the unit was simply in the party
# (reported 2026-11-18, Boco; Luso's before). ReMixed's own state 7 stays now, as on the PS1, and each scene's count of
# them is checked, so a later change can't quietly make those joins silent again.
MIDSCENE_JOIN_SCREENS = {0x008: 1, 0x040: 2, 0x06E: 3, 0x07B: 1,
                         0x0A3: 1, 0x0EB: 1, 0x107: 1, 0x11B: 1}

# ReMixed's 1-A rescue names Agrias while she is off the field, so a unit portrait resolves to none. Its EVTFACE
# set 0 portrait 7 is her own ReMixed face. Gareth's dream scene names portrait 6 of set 1 (Orlandeau) for the knight,
# but ReMixed drew Gareth in portrait 8. The remaster face-set table is remapped to those source pictures by
# RemixedFaces.RemasterEventFaces, and the build ships their textures (EVENT_FACE_ART).
# event -> (speaker battle id, old portrait, new portrait, set to load before the message, exact message count).
SCENE_PORTRAIT_FIXES = {0x112: (0x22, 0, 7, 0, 5), 0x0F9: (0x0B, 6, 8, None, 15)}


def fix_scene_portraits(code: bytes, lengths: bytes, event: int) -> bytes:
    """Give the two scene speakers their intended explicit ReMixed EVTFACE portrait."""
    if event not in SCENE_PORTRAIT_FIXES:
        return code
    actor, before, after, face_set, expected = SCENE_PORTRAIT_FIXES[event]
    out = bytearray()
    at = count = 0
    while at < len(code):
        end = at + 1 + lengths[code[at]]
        instruction = bytearray(code[at:end])
        # At this stage the script still has PS1's 16-bit message numbers. The speaker and portrait are bytes 5/7;
        # write_scenario_texts later expands the number to 32 bits, moving them to 7/9 in the shipped event.
        if instruction[0] == 0x10 and instruction[5] == actor and instruction[7] == before:
            if face_set is not None:
                out.extend((0x50, face_set))
            instruction[7] = after
            count += 1
        out.extend(instruction)
        at = end
    if count != expected:
        sys.exit(f"event 0x{event:X}: changed {count} portraits for actor 0x{actor:X}, expected {expected}")
    return bytes(out)


def insert_midscene_join_return(code: bytes, lengths: bytes, expected: int) -> bytes:
    """Restore the field after every midscene display-state-7 wait, before its dark screen is removed."""
    display, wait = bytes.fromhex("78 07 3c"), bytes.fromhex("e5 38 00")
    restore = bytes.fromhex("cf c7 00 00 00")  # RemixedEvents.MidsceneJoinReturn
    out = bytearray()
    at = count = 0
    while at < len(code):
        end = at + 1 + lengths[code[at]]
        if code[at:end] == display and code[end:end + len(wait)] == wait:
            out.extend(code[at:end + len(wait)])
            out.extend(restore)
            at = end + len(wait)
            count += 1
        else:
            out.extend(code[at:end])
            at = end
    if count != expected:
        sys.exit(f"midscene join return: found {count} display-state waits, expected {expected}")
    return bytes(out)


def midscene_join_screens(code: bytes, lengths: bytes) -> int:
    """The join screens (display state 7, then its wait) a scene shows inside a dark screen (0x76 ... 0x77)."""
    instructions: list[tuple[int, bytes]] = []
    at = 0
    while at < len(code):
        op = code[at]
        end = at + 1 + lengths[op]
        if end > len(code):
            break
        instructions.append((op, code[at + 1:end]))
        at = end
    found = 0
    for dark, (op, _) in enumerate(instructions):
        if op != 0x76:
            continue
        remove = next((n for n in range(dark + 1, len(instructions)) if instructions[n][0] == 0x77), len(instructions))
        found += sum(instructions[n] == (0x78, bytes([0x07, 0x3C])) and instructions[n + 1] == (0xE5, bytes([0x38, 0x00]))
                     for n in range(dark + 1, remove - 1))
    return found


# Source-authored post-battle cast resets. ReMixed's cleanup runs remove the battle actors before adding the scene's
# speakers; the remaster's RemoveUnit drops the logical unit but leaves a defeated unit's corpse/crystal drawable.
# Erase each actor first, exactly in these reset runs. Other victory scenes deliberately keep bodies, crystals or live
# battle actors for their choreography and must not receive a blanket cleanup.
POSTBATTLE_CLEANUP = {
    0x107: (0x02, 0x17, 0x1E, 0x90, *range(0x80, 0x87), *range(0x78, 0x7D)),
    0x0EB: (0x01, 0x04, 0x07, 0x12, *range(0x80, 0x89), *range(0x78, 0x7D)),
    0x115: tuple(range(0x80, 0x86)),
    0x0F7: tuple(range(0x8B, 0x90)),  # 0x8A already has a source-authored Erase
}


def erase_postbattle_cleanup(code: bytes, actors: tuple[int, ...] | None) -> tuple[bytes, int]:
    """Erase corpse/crystal drawables before one exact source-authored RemoveUnit reset run."""
    if actors is None:
        return code, 0
    removed = b"".join(bytes([0x3D, actor, 0x00]) for actor in actors)
    if code.count(removed) != 1:
        raise ValueError(f"post-battle cleanup run {removed.hex(' ')} occurs {code.count(removed)} times")
    erased = b"".join(bytes([0x46, actor, 0x00, 0x3D, actor, 0x00]) for actor in actors)
    return code.replace(removed, erased, 1), len(actors)
# Two of 1-A's battles are fought on another map than their opening scene: the menu sets a battle up from what its code
# sets in vars 0x32 and 0x33 (its battle entry and map), which for 0x0F4 (map 26, entry 0x1DB) and 0x0FB (map 32) aren't
# their records' (the scene's: maps 25 and 31). With the menu dropped, the battle had the scene's map and units, and
# its formation screen no tile to deploy on (seen on the PC with 0x0F4). So each of those battles gets a scenario of
# its own (ReMixed's event -> its number, where the remaster has no event file or text): the battle's map, entry and
# formations, the event's code with the hand-off to the menu a jump to its end (ReMixed's own run of it from the menu
# ends there: var 0x18 = var 0x27), the scene's conditions with the events they run as the scene's run as its own (the
# battle's opening starts through them, "var 0x1FD = 1 -> the event": with the scene's, the battle's start ran the
# scene again, which handed on to the formation again; seen on the PC), and the scene's lines in a text of its own. The
# scene keeps its record, without formations, and hands on to that scenario in the menu's place.
BATTLE_SPLITS = {
    0xF4: 0x34A,
    0xFB: 0x34B,
    # Luso at Mandalia's scene and battle use the same map, but still need distinct remaster scenarios. A hand-off back
    # to the scene's own scenario can leave its setup in place: the condition walker then reaches the victory set and
    # runs 0x0EB's recruitment without ever loading formation 0x14E. A split forces a fresh setup, with the battle's
    # formation and a private copy of preset 0xE9, before any battle condition is walked.
    0xEA: 0x34D,
    0xF8: 0x2EB,
}
# A battle's two tracks. ReMixed's battles after a scene take them from its Preparation Menu, which reads the battle's
# record as the scene's opening leaves it in vars 0x3A-0x3C: SETHI var 0x3C's value is the record's bytes 10 and 11,
# the track before the battle (8 in most) and the battle's own, which is also the last track the scene's intro on the
# battle's map starts (0x0F4 8, 0x0F7 0x5C, 0x0F8 0x63, 0x0FB 0x0C; read 2026-11-22). The scene's own record has
# zeroes there (it plays its own music, and fades it out as it ends), so a battle with a scenario of its own
# (BATTLE_SPLITS) gets them in its record; the others are given theirs by the code mod at the battle's run
# (RemixedScenarios.PrepareMenuBattle). Without them the battle began in silence, or with the scene's last track
# until something stopped it (a tester, 2026-11-22: Mount Bervenia's battle and 1-A's last). Yardrow's armory battle
# (0x0F4) had (8, 0x29) by hand until then, its second track misread from the low half; it's (8, 8), as ReMixed's.
YARDROW_ARMORY = 0xF4


def battle_music(code: bytes) -> bytes | None:
    """The two tracks a ReMixed scene's opening sets for its battle (SETHI var 0x3C, hi: its low and high byte)."""
    at = code.find(bytes([0xAC, 0x3C, 0x00]))
    return bytes([code[at + 3], code[at + 4]]) if at >= 0 and code[at + 3] and code[at + 4] else None
# A map reload in an event (0x4C) loads the map and puts the units of the battle entry var 0x32 names in battle units
# 0-15, on the PS1 and in the remaster (the setup 0x3044EC/0x30476C: [0x1856930] = var 0x32, its ENTD file, its
# records through 0x28416C). ReMixed's code may set var 0x32 to one of its own entry numbers first: 0x0F4's opening
# sets it to 0x1DB, its battle's, and after the town scene its map reload brings the armory's units in (the enemies, and
# 0x90 Agrias, 0x91 Balmafula, 0x92 the Wizard; seen on the PS1, work/psx/run/shots f4y-*), whose first line names
# 0x90. The port moves ReMixed's entries to other slots or into the mod's file, so its 0x1DB is another battle's (one of
# Brave Story's rounds, ROUND_SLOTS): the reload brought no 0x90, and Agrias's "So these are the fiends?" had no face
# (a tester on 0.9.22). So every literal var 0x32 a shipped event's map reload uses (MAP_RELOAD) carries that entry's
# units as the port builds them, for that scenario: the code mod hands them to the reload in the slot's place
# (RemixedScenarios.ReloadEntries, by the scenario and the entry asked). The literal stays ReMixed's: it differs from the
# scene's own slot and from the battle's carrier, as the reload must (an entry number the setup has just loaded keeps
# that load's units; see FRESH_ENTRY_BATTLES), and the build checks that it does.
MAP_RELOAD, SET_VALUE = 0x4C, 0xAE


def reload_entries(code: bytes, lengths: bytes) -> list[int]:
    """The entries ReMixed's code names by a literal in var 0x32 (SET var 0x32, N) where a map reload (MAP_RELOAD) of
    its uses them, in code order. Any other write to var 0x32 (ADD in the missions' rounds, SET_VAR, ZERO) leaves the
    reloads after it to the slots as the port has them (ROUND_SLOTS); a reload with no literal before it loads the
    scenario's own entry. Read straight through: 0x0F4's literal is in its opening, its reload after the town scene."""
    found: list[int] = []
    literal = None
    at = 0
    while at < len(code):
        op = code[at]
        if op in range(0xAC, 0xBF) and at + 3 <= len(code) and u16(code, at + 1) == MENU_ENTRY_VAR:
            literal = u16(code, at + 3) if op == SET_VALUE else None
        elif op == MAP_RELOAD and literal is not None and literal not in found:
            found.append(literal)
        at += 1 + lengths[op]
    return found


# Actors needed by the scene around a split battle, but not by the battle itself: ReMixed's Mandalia carrier has a
# data-built Ramza (battle id 0x90) for its opening/recruitment staging. The remaster also deploys the player's real
# roster Ramza through the formation, leaving that scene actor as a second guest Ramza in a corner. Keep the actor in
# scene 0xEA, but omit it from split battle 0x34D; event 0xEB then resolves actor 1 to the deployed roster Ramza.
BATTLE_SPLIT_SCENE_ONLY = {0xEA: {0x90}}
# Mandalia's battle records use flags 0xC1-0xCC to include their units. ReMixed sets them before its Preparation Menu
# starts the battle setup; the port's direct scenario hand-off clears them before the new setup can read them. The code
# mod therefore sets them before GameSetup for split battle 0x34D. Its opening then activates the prepared cast with
# AddUnit's immediate-visible mode. ReMixed's scene originally adds them hidden (mode 1) and draws each later; that
# scene has already run before the split, so carrying hidden mode into the fresh battle left only portraits and HP bars.
# AddUnit without the pre-setup flags consumed excluded units' stale graphics (Luso drew as Ramza and the monsters
# were invisible); flags without AddUnit left every battle id inactive; hidden AddUnit without Draw hid every model.
MANDALIA_SETUP_FLAGS = range(0xC1, 0xCD)
MANDALIA_BATTLE_ACTIVATE = (bytes([0x49])
                             + b"".join(bytes([0x45, unit, 0x00, 0x00])
                                        for unit in [0x12, *range(0x80, 0x89)])
                             + bytes([0x4A, 0x4B])
                             # The opening scene ends under a full-screen colour fade. The original Preparation Menu
                             # cleared it while setting the battle up; the direct split hand-off has no menu, so the
                             # map otherwise stays black through the battle's Luso/Ramza interruption.
                             + MENU_FADE_IN)
# Other direct scene -> battle hand-offs which use the same ReMixed entry on both sides.  The remaster caches an ENTD
# entry by its number: if the battle keeps the scene's number, it keeps the scene's erased/hidden actors instead of
# constructing the battle records again.  BATTLE_SPLITS' battle jobs are handled by ``scene is not None`` below; these
# are the same shape without a synthetic split (Zalbag at Eagrose, Beowulf's continuation, and 1-A's 0x0F7 -> 0x0F8).
FRESH_ENTRY_BATTLES = {0x6F, 0x9D, 0xF8}
# Five of 1-A's battles return to their own in-battle event after their victory (AFTER_BATTLE_RETURN): the victory runs
# the event, whose code loads the preset with "flag 0x8E = 1 -> that same event", and it runs again into its
# post-battle scene. Run again under its own number, the scene's boxes showed the speaker's portrait without the line
# (seen on the PC: 0x115 after 0x0F5's battle, 2026-11-07), where scenes the battle runs under other numbers showed
# theirs (0x117, then 0x0F7's final closing, after 0x0F8's). 0x0F7 gets one too: on VOLCANO the doppelganger's fall runs
# it (preset 0xF7's set 7, its LABEL 17), which ends with the flag-0x8E return to the same preset, and that runs 0x0F7
# again for its closing (LABEL 11). Its first battle's victory isn't one of these: preset 0xF6's victory set runs 0x0F7's
# LABEL 01 directly. (The blank Agrias box after that first battle, from 0.9.31 on, was its line's number worked out
# twice: self_patched_sets.) So each one's post-battle run is an event of its own: its code
# and lines again under a number where the remaster has no event file or text, which every "flag 0x8E = 1" set that ran
# the event runs instead. The code is the same: 0x115 tests the flag at its start, the others go to the label of the
# set that ran them (var 1), as before. Why the lines went (read 2026-11-13): an event's END unloads its scenario text
# (0x21F14C, 0x1DB06C) but keeps the loaded event's number ([0x782A94]), and the loader (0x23F37C) loads only an event
# of another number, so the same event run again had its code and no text. The code mod's walker now has such a run
# loaded afresh (RemixedScenarios.ReloadOnRerun), which is what ReMixed's other in-battle runs of one event needed
# (their boxes came up empty); the splits stay, as seen working.
# 0x0F7's copy had 0x355 from 0.9.32, the number the dispatch tavern's scene (TAVERN_SCENE) already had: the closing ran
# 0x0F7's code on the tavern's record, so its map reload brought the tavern's battle entry in, Ramza placeholders, in
# Agrias's and Valmafra's places (their boxes with Ramza's face, a stray Ramza at the map's edge) and the scene stuck
# (a player on 0.9.40, 2026-11-27). The free numbers below 0x377 are used up, so it takes the remaster's 453, a temple
# scene that can't run since TEMPLE_SECOND_WAVE took the battle before it; check_scenario_numbers stops the build on
# two events at one number.
AFTER_SPLITS = {0x115: 0x34C, 0x112: 0x352, 0x114: 0x353, 0x119: 0x354, 0x0F7: 0x1C5}
REUSED_SCENARIO_NUMBERS |= {AFTER_SPLITS[0x0F7]}
# Every generated event that contains an AFTER_BATTLE_RETURN hand-off. This deliberately includes inactive copies of
# a split scene's victory label: keeping the transform global prevents a later condition or checkpoint from reviving
# a display-state-7 map teardown. Each has exactly one such edge in ReMixed's source.
LIVE_RETURN_EVENTS = {
    ARAGUAY_LUSO[0x1D], LUSO_AT_MANDALIA[0xEA], BATTLE_SPLITS[0xEA],
    *(REMIXED_SCENARIOS[event] for event in (0x0F7, 0x112, 0x114, 0x115, 0x119)),
    *AFTER_SPLITS.values(), CHAPTER_3_BRANCH[0x11B],
}
MENU_ENTRY_VAR, MENU_MAP_VAR = 0x32, 0x33
GOTO_END = bytes([0x0D, 0xFF])  # GOTO ff: ReMixed's events end at LABEL ff, END
# ReMixed's game-over menu (its 0x190: Retry Battle, Return to Title Screen, Return to World Map), which a battle's
# scripted failure reaches through a preset (4: "... -> 0x190") loaded before a 0xCD 00 00 00. The mod ships neither, so
# that preset runs nothing (an idle preset, idle_presets), and the hand-off goes back to the world map, ReMixed's third
# choice. Left as it was, the battle went on with its enemies down and the closing ran again and again (seen on the PC
# with The Guarded Temple, 2026-11-07).
RETRY_MENU = 0x190


def idle_presets(presets: bytes, events: dict[int, int]) -> set[int]:
    """ReMixed's presets that run events, none of which the mod ships (events, theirs -> ours): loaded in the mod they
    run nothing (renumber_presets makes such sets unable to fire)."""
    idle = set()
    for n in range(REMIXED_PRESETS):
        preset = presets[n * REMIXED_PRESET_BYTES:(n + 1) * REMIXED_PRESET_BYTES]
        targets = [e for _, _, e in remixed_preset_targets(preset)]
        if targets and not any(e in events for e in targets):
            idle.add(n)
    return idle


# The words borrowed by the build's extended InflictStatus conversion. They are in the upgrade's private
# variable space and are written before each call to RemixedEvents.ApplyExtendedStatus.
EXTENDED_STATUS_ACTOR, EXTENDED_STATUS_MODE = 0xB7E, 0xB7F
APPLY_EXTENDED_STATUS = bytes.fromhex("cf c6 00 00 00")


def remaster_extended_statuses(code: bytes, lengths: bytes, event: int) -> bytes:
    """Port ReMixed's InflictStatus modes for individual status bits, preserving the operand delay."""
    out, pending, at = bytearray(), [], 0
    while at < len(code):
        op = code[at]
        ins = code[at:at + 1 + lengths[op]]
        at += len(ins)
        if op == 0xE6:
            pending.append(ins)
            continue
        if op == 0x92 and (ins[3] >= 0x80 or any((patch[1] & 0x3F) == 2 for patch in pending)):
            mode = ins[3]
            if mode < 0x80 or (mode & 0x3F) >= 40:
                sys.exit(f"ReMixed event 0x{event:X} uses unsupported InflictStatus mode 0x{mode:X}")
            operands: tuple[list[bytes], ...] = ([], [], [])
            for patch in pending:
                field = patch[1] & 0x3F
                if field >= 5:
                    sys.exit(f"ReMixed event 0x{event:X} patches past InflictStatus's operands")
                target, offset = ((0, field + 2) if field < 2 else (1, 2) if field == 2 else (2, field - 3))
                operands[target].append(bytes([0xE6, (patch[1] & 0xC0) | offset, patch[2]]))
            actor, mode_patches, delay = operands
            out.extend(b"".join(actor) + struct.pack("<BHH", 0xAE, EXTENDED_STATUS_ACTOR, u16(ins, 1)))
            out.extend(b"".join(mode_patches) + struct.pack("<BHH", 0xAE, EXTENDED_STATUS_MODE, mode))
            out.extend(APPLY_EXTENDED_STATUS)
            if delay or u16(ins, 4):
                out.extend(b"".join(delay) + bytes([0xF1]) + ins[4:6])
        else:
            out.extend(b"".join(pending) + ins)
        pending.clear()
    out.extend(b"".join(pending))
    return bytes(out)


def location_scenario_code(code: bytes, lengths: bytes, unsafe: set[int], empty: int,
                           scenarios: dict[int, int] | None = None,
                           patches: list[tuple[bytes, bytes]] | None = None,
                           idle: set[int] = frozenset(), returned_to: bool = False,
                           return_presets: dict[int, int] | None = None,
                           defer_return_writeback: bool = False) -> tuple[bytes, int | None]:
    """A ReMixed scenario's code for the mod, and the scenario it hands on to after its battle (None if it doesn't): a
    0xCE that loads a preset running events the mod doesn't ship (the after-battle event 8, through preset 7) loads an
    empty one, AFTER_BATTLE_FLAG is set to 0 rather than 1 after it, and the map-settings word that would have handed
    on to event 8 (0xCD 00 00 00 after it) goes on to where event 8 would have gone (AFTER_BATTLE_VARS) if that's one
    of ReMixed's scenarios the mod ships (scenarios, theirs -> ours: a mission's goes on to the Treasure Wheel, WHEEL),
    else back to the world map (WORLD_MAP_WORD). A hand-off to the Preparation Menu (PREPARATION_MENU) is dropped. Its
    other hand-offs to scenarios the mod ships (0xCD with mode 0x81-0x83, and SET var 0x27) are renumbered, and those to
    ReMixed's main-story scenes go to the remaster's (REMASTER_HAND_OFFS). A 0xCE that loads an idle preset (a scripted
    failure, on to ReMixed's game-over menu, RETRY_MENU) loads the empty one too, and the 0xCD 00 00 00 after it goes
    back to the world map. A hand-off to event 8 that returns to the battle's
    events (AFTER_BATTLE_RETURN) loads the preset event 8 would have loaded instead (or the copy return_presets names
    for it), and the word stays 0; with returned_to (an event such a preset runs), each hand-off to the world map clears
    AFTER_BATTLE_FLAG before it. A hand-off to one of WORLD_MAP_ENDS goes back to the world map, and one of mode
    NEXT_BY_VAR_MODE naming no scenario goes on (0x81) to the one the event last set var 0x27 to. Each hand-off that
    stands for event 8 writes the battle's units back into the roster first and sets the tavern's flag. Ordinary exits
    use display state 7 (AFTER_BATTLE_HOME). A return to a post-battle event on the same battlefield uses the synchronous
    native roster writer instead (AFTER_BATTLE_RETURN_HOME), because the remaster's state 7 destroys the map that scene
    continues on. Instructions equal to one of patches' first bytes become its second (EVENT_CODE_PATCHES)."""
    out = bytearray()
    at, handed, failed, target = 0, False, False, None
    named = None  # the mod's scenario the event last set var 0x27 to (NEXT_BY_VAR_MODE)
    after = {var: 0 for var in AFTER_BATTLE_VARS}
    depth, chosen = 0, False  # IF depth; whether event 8's vars were set under an IF (AFTER_BATTLE_CHOSEN)
    queued = None  # a scenario a scene left for ReMixed's world map in var 0x55 (WORLD_QUEUE_VAR)
    return_preset, return_chosen = None, False  # a return set under an IF (RETURN_CHOSEN), and that it was written
    while at < len(code):
        op = code[at]
        ins = bytearray(code[at:at + 1 + lengths[op]])
        for was, becomes in patches or []:
            if ins == was:
                ins = bytearray(becomes)
        if op == 0x01:  # IF
            depth += 1
        elif op == 0x04:  # ENDIF
            depth = max(0, depth - 1)
        if op == 0xAE and u16(ins, 1) in after:  # SET var, value
            after[u16(ins, 1)] = u16(ins, 3)
            chosen |= depth > 0
            if after[AFTER_BATTLE_VARS[0]] == 0 and after[AFTER_BATTLE_VARS[2]] == AFTER_BATTLE_RETURN \
                    and after[AFTER_BATTLE_VARS[1]]:
                return_preset = after[AFTER_BATTLE_VARS[1]]
        elif op == 0xBE and u16(ins, 1) in after:  # ZERO var
            after[u16(ins, 1)] = 0
            chosen |= depth > 0
        elif op == 0xB0 and u16(ins, 1) == WORLD_QUEUE_VAR and scenarios and u16(ins, 3) in scenarios:
            queued = scenarios[u16(ins, 3)]  # ADD var 0x55, a scenario: handed on to at the scene's world-map word
            ins[3:5] = bytes(2)
        elif op == 0xCE and u16(ins, 1) in unsafe and after[AFTER_BATTLE_VARS[0]] == 0 \
                and after[AFTER_BATTLE_VARS[2]] == AFTER_BATTLE_RETURN and after[AFTER_BATTLE_VARS[1]]:
            back = after[AFTER_BATTLE_VARS[1]]  # event 8's return (AFTER_BATTLE_RETURN)
            ins[1:3] = (return_presets or {}).get(back, back).to_bytes(2, "little")
            ins = bytearray(AFTER_BATTLE_DEFERRED_HOME if defer_return_writeback else AFTER_BATTLE_RETURN_HOME) + ins
        elif op == 0xCE and u16(ins, 1) in unsafe and chosen and return_preset is not None:
            back = (return_presets or {}).get(return_preset, return_preset)  # the return on one path only
            if defer_return_writeback:
                # Only the return goes on to the source's join (Araguay's 122, Luso's thanks, whose join screen writes
                # the battle back). The other way ends at the world map with no scene, so it writes back here as event 8
                # would, with the join screen (display state 7, MIDSCENE_JOIN_SCREENS): Boco, whose record joins when
                # the roster hasn't him yet. Deferring both left Araguay's results unsaved and Boco unjoined when Luso
                # was already in the party (seen in the hidden PC game, 2026-11-18); a silent join (0x43 1) there put
                # him in the party with no screen (reported the same day).
                ins = bytearray(AFTER_BATTLE_DEFERRED_HOME + RETURN_CHOSEN + bytes([0xCE]) + back.to_bytes(2, "little")
                                + bytes([0x03]) + AFTER_BATTLE_WRITE_BACK
                                + bytes([0xCE]) + empty.to_bytes(2, "little") + bytes([0x04]))
            else:
                ins = bytearray(AFTER_BATTLE_RETURN_HOME + RETURN_CHOSEN + bytes([0xCE]) + back.to_bytes(2, "little")
                                + bytes([0x03, 0xCE]) + empty.to_bytes(2, "little") + bytes([0x04]))
            return_chosen = True
        elif op == 0xCE and u16(ins, 1) in unsafe:
            ins[1:3] = empty.to_bytes(2, "little")
            handed = True
        elif op == 0xCE and u16(ins, 1) in idle:
            ins[1:3] = empty.to_bytes(2, "little")
            failed = True
        elif op == 0xAE and (handed or failed) and u16(ins, 1) == AFTER_BATTLE_FLAG:
            ins[3:5] = bytes(2)  # SET flag 0x8E, 0
        elif op == 0xCD and failed and ins[1:4] == bytes(3):
            ins = bytearray([0xBE]) + AFTER_BATTLE_VARS[0].to_bytes(2, "little") + bytes([0xCD, *WORLD_MAP_WORD])
            failed = False
        elif op == 0xAE and u16(ins, 1) == 0x27 and scenarios and u16(ins, 3) in scenarios:
            named = scenarios[u16(ins, 3)]
            ins[3:5] = named.to_bytes(2, "little")  # SET var 0x27, a scenario
        elif op == 0xCD and ins[1] & 0xF3 == NEXT_BY_VAR_MODE and ins[2:4] == bytes(2) and named is not None:
            ins[1:4] = bytes([0x81, named & 0xFF, named >> 8])  # on to var 0x27's scenario (NEXT_BY_VAR_MODE)
        elif op == 0xCD and ins[1] & 0xF3 in (0x81, 0x82, 0x83) and (ins[3] << 8) + ins[2] in WORLD_MAP_ENDS:
            ins[1:4] = bytes(WORLD_MAP_WORD)  # a scene the mod has no place for: the world map (WORLD_MAP_ENDS)
        elif op == 0xCD and ins[1] & 0xF3 == 0x81 and (ins[3] << 8) + ins[2] == PREPARATION_MENU:
            ins[1:4] = bytes(3)  # on to the Preparation Menu: dropped (see PREPARATION_MENU and MENU_BATTLES)
        elif op == 0xCD and ins[1] & 0xF3 in (0x81, 0x82, 0x83) and scenarios and (ins[3] << 8) + ins[2] in scenarios:
            ours = scenarios[(ins[3] << 8) + ins[2]]  # a hand-off to another of ReMixed's scenarios
            ins[2:4] = bytes([ours & 0xFF, ours >> 8])
        elif op == 0xCD and ins[1] & 0xF3 == 0x81 and (ins[3] << 8) + ins[2] in REMASTER_HAND_OFFS:
            theirs = REMASTER_HAND_OFFS[(ins[3] << 8) + ins[2]]  # on to the remaster's scene for one of ReMixed's
            ins[2:4] = bytes([theirs & 0xFF, theirs >> 8])
        elif op == 0xCD and queued is not None and not handed and ins[1:4] == bytes(WORLD_MAP_WORD):
            ins[1:4] = bytes([0x81, queued & 0xFF, queued >> 8])  # the scenario the scene queued (WORLD_QUEUE_VAR)
            queued = None
        elif op == 0xCD and returned_to and not handed and ins[1:4] == bytes(WORLD_MAP_WORD):
            # Put the return cleanup before the world-map hand-off. The previous generated order left the flag set
            # in the reported Luso paths, repeating Mandalia and leaving Araguay's encounter active.
            clear = bytes([0xAE]) + AFTER_BATTLE_FLAG.to_bytes(2, "little") + bytes(2)  # SET flag 0x8E, 0
            ins = bytearray(clear) + ins
        elif op == 0xCD and return_chosen and ins[1:4] == bytes(3):
            # The return's path keeps the word 0 (the battle goes on, its enemies down, for the preset's scene); the
            # other goes back to the world map as event 8 would have (its vars: mode 0x80).
            zero = bytes([0xBE]) + AFTER_BATTLE_VARS[0].to_bytes(2, "little")
            ins = bytearray(RETURN_CHOSEN + bytes([0xCD, 0x00, 0x00, 0x00, 0x03]) + zero
                            + bytes([0xCD, *WORLD_MAP_WORD, 0x04]))
            return_chosen = False
        elif op == 0xCD and handed and ins[1:4] == bytes(3):
            mode, low, high = (after[var] for var in AFTER_BATTLE_VARS)
            theirs = (high << 8) + low
            target = scenarios.get(theirs) if scenarios and mode & 0xF3 == 0x81 else None
            if target is None and mode & 0xF3 == 0x81 and theirs in REMASTER_HAND_OFFS:
                target = REMASTER_HAND_OFFS[theirs]  # one of ReMixed's that the remaster's own event stands for
            # Back to the world map with var 0x3C naming an event: ReMixed's event 8 runs that one next (its default
            # case: var 0x55 = var 0x3C, CallFunction 6), as the Algus campaign's battles each queue the next.
            if target is None and scenarios and mode == 0x80 and high in scenarios:
                target = scenarios[high]
            if target is None and mode == 0x80 and high in REMASTER_HAND_OFFS:
                target = REMASTER_HAND_OFFS[high]
            ins[1:4] = bytes([0x81, target & 0xFF, target >> 8]) if target is not None else bytes(WORLD_MAP_WORD)
            # Event 8 zeroes the mode as it goes on (ZERO var 0x3A); left at 0x81, 0x007 (the next scene after
            # 0x006's battle) took itself for ReMixed's battle launcher (its first instruction: var 0x3A set, on to
            # its block 0xF0), filled the formation variables from PS1 memory and handed on to the random battles'
            # setup, and the remaster's formation code (0x303408, from 0x1EF0D8) crashed on them (seen on the PC).
            zero = bytes([0xBE]) + AFTER_BATTLE_VARS[0].to_bytes(2, "little")
            home = AFTER_BATTLE_HOME  # event 8's write-back and the tavern's flag
            if chosen and target is not None and mode & 0xF3 == 0x81:
                # Set under an IF, event 8's vars name the scenario only on one path (0x10E: on to 0x06E only when
                # Zalbag lives, else the world map): the hand-off is chosen as the event runs (AFTER_BATTLE_CHOSEN).
                ins = bytearray(home + AFTER_BATTLE_CHOSEN + zero + ins + bytes([0x03]) + zero
                                + bytes([0xCD, *WORLD_MAP_WORD]) + bytes([0x04]))
            else:
                ins = bytearray(home + zero) + ins
            handed = False
        out += ins
        if op == 0xDB and at + 1 + lengths[op] >= len(code):
            break
        at += 1 + lengths[op]
    if returned_to:
        clear = bytes([0xAE]) + AFTER_BATTLE_FLAG.to_bytes(2, "little") + bytes(2)
        hand_off = bytes([0xCD, *WORLD_MAP_WORD])
        if hand_off + clear in out:
            raise ValueError("a post-battle scene clears flag 0x8E after its world-map hand-off")
    return bytes(out), target



def opening_sets(code: bytes, lengths: bytes, variables: tuple[int, ...], inside: bool = False) -> dict[int, int]:
    """What a ReMixed battle's code sets in variables for its Preparation Menu (by SET or ZERO outside any IF, before
    its first END: e.g. 0x0EE's formation 0x150). With `inside`, a SET of a value under an IF instead (0x0EE's second
    group 0x151, with flag 0xAD and var 0x6C 0; see MENU_SECOND_GROUP). Nothing for code that doesn't hand on to the
    menu: the Treasure Wheel (0x0C3) uses the formation variables for its loot."""
    found: dict[int, int] = {}
    at, depth, ended, menu = 0, 0, False, False
    while at < len(code):
        op = code[at]
        if op == 0x01:  # IF
            depth += 1
        elif op == 0x04:  # ENDIF
            depth = max(0, depth - 1)
        elif op == 0xAE and (depth > 0) == inside and not ended and u16(code, at + 1) in variables:
            if not inside or u16(code, at + 3):
                found.setdefault(u16(code, at + 1), u16(code, at + 3))
        elif op == 0xBE and depth == 0 and not inside and not ended and u16(code, at + 1) in variables:
            found.setdefault(u16(code, at + 1), 0)  # ZERO
        elif op == 0xCD and code[at + 1] & 0xF3 == 0x81 and (code[at + 3] << 8) + code[at + 2] == PREPARATION_MENU:
            menu = True
        if op == 0xDB:  # its first END ends the opening; the menu's hand-off may come in a later block
            if at + 1 + lengths[op] >= len(code):
                break
            ended = True
        at += 1 + lengths[op]
    return found if menu else {}


# ReMixed's frameless lines: a MESSAGE whose style's place is 8 (0x80-0x8F: the Treasure Wheel's line across the top and
# the one along the bottom) shows plain text on the PS1, which the event goes on past at once, its end FE or not: the
# vanilla wait for a message box after it (0xE5 1) doesn't wait for a press there. The port shows them in speech boxes,
# which wait for one, so the Wheel answered only after two presses (seen on the PC, 2026-11-12, its instructions
# traced). The build lists them (SLOTS_FILE's "plain"), and the code mod has their boxes go on without one
# (Patches/TallMenus.cs).
FRAMELESS_PLACE = 0x80


def frameless_messages(code: bytes, lengths: bytes) -> list[int]:
    """The messages a ReMixed event's MESSAGEs show in a frameless line (FRAMELESS_PLACE), but for those whose number
    the event writes in as it runs (a PATCH_NEXT of the MESSAGE's message)."""
    found: set[int] = set()
    at, patched = 0, set()
    while at < len(code):
        op = code[at]
        if op == 0xE6:  # PATCH_NEXT: the next instruction's parameter at this offset gets a variable's bytes
            patched.add(code[at + 1])
        else:
            if op == 0x10 and code[at + 2] & 0xF0 == FRAMELESS_PLACE and not patched & {2, 3}:
                found.add(u16(code, at + 3))
            patched = set()
        at += 1 + lengths[op]
    return sorted(found)


def opening_formations(code: bytes, lengths: bytes) -> list[int]:
    """The formations a ReMixed battle's code sets for its Preparation Menu (FORMATION_VARS), for its record, in
    order."""
    found = opening_sets(code, lengths, FORMATION_VARS)
    return [f for f in (found.get(var, 0) for var in FORMATION_VARS) if f]


# A battle played after its scene whose code sets its second group only under an IF (0x0EE: with flag 0xAD, Bryn and
# Lorne along, and var 0x6C 0; 0x0F1: var 0x6C 0. var 0x6C holds ReMixed's Extra Options bits, 0 as a new game has them
# and in the port, which has its own options; seen on the PS1: 0x0F1's formation has a second squad of two). The scene's
# run sets var 0x36 as ReMixed's code decides, so the build ships that group with the battle and marks it
# (MENU_SECOND_GROUP | formation in the scenarios file); the code mod takes it when var 0x36 is set
# (RemixedScenarios.SecondGroupsOn). 4 + 2 is six places, which the remaster takes with the code mod's "six-places"
# (Patches/SixPlaces.cs): without it Space never commenced (0x0F0's, seen on the PC 2026-11-12).
MENU_SECOND_GROUP = 0x8000
# A battle with its formation first (0x0F4's, 0x0F8's) has no run before its formation to decide its second group, so
# the build decides: it ships the group when every IF around its SET holds in the port (CONDITIONS_IN_PORT: the ELSE of
# "IF var 0x6C", the Extra Options being 0), and stops on any other (write_remixed_scenarios' dropped groups).
EXTRA_OPTIONS_VAR = 0x6C
CONDITIONS_IN_PORT = {(EXTRA_OPTIONS_VAR, "else")}


def conditional_second_group(code: bytes, lengths: bytes) -> int:
    """The formation a ReMixed battle's code sets for its second group under an IF (FORMATION_VARS' second), or 0."""
    return opening_sets(code, lengths, FORMATION_VARS, inside=True).get(FORMATION_VARS[1], 0)


def second_group_conditions(code: bytes, lengths: bytes) -> list[tuple[int, str]]:
    """The IFs around the first SET of a ReMixed battle's second group (FORMATION_VARS' second) before its first END,
    outermost first: (the variable its var 0 was set from, "then" or "else"). 0x0F4: [(0x6C, "else")]; 0x0EE:
    [(0xAD, "then"), (0x6C, "else")]."""
    at, source, stack = 0, None, []
    while at < len(code):
        op = code[at]
        if op == 0xAF and u16(code, at + 1) == 0:  # SET_VAR var 0, var N: what the next IF tests
            source = u16(code, at + 3)
        elif op == 0x01:  # IF var 0
            stack.append([source, "then"])
        elif op in (0x02, 0x03) and stack:  # ELSE IF, ELSE
            stack[-1][1] = "else"
        elif op == 0x04 and stack:  # ENDIF
            stack.pop()
        elif op == 0xAE and u16(code, at + 1) == FORMATION_VARS[1] and u16(code, at + 3) and stack:
            return [(var, branch) for var, branch in stack]
        elif op == 0xDB:
            break
        at += 1 + lengths[op]
    return []

def nxd_sqlite(cfg: dict, file: str, pack_name: str = "0004.pac") -> Path:
    """The game's nxd/<file>.nxd (extracted once) as a fresh SQLite file in work/."""
    work = WORK / "remaster" / "build" / file
    base = WORK / "remaster" / "extract" / "enhanced" / "nxd" / f"{file}.nxd"
    if not base.exists():
        pack = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced" / pack_name
        ff16tools(cfg, "unpack", "-i", pack, "-f", f"nxd/{file}.nxd", "-o", WORK / "remaster" / "extract" / "enhanced")
    shutil.rmtree(work, ignore_errors=True)
    (work / "in").mkdir(parents=True)
    shutil.copy(base, work / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", work / "in", "-o", work / "table.sqlite")
    return work / "table.sqlite"


def nxd_keys(cfg: dict, file: str, table: str, pack_name: str = "0004.pac") -> set[int]:
    """The keys of the game's own NXD table."""
    db = sqlite3.connect(nxd_sqlite(cfg, file, pack_name))
    keys = {k for (k,) in db.execute(f'select Key from "{table}"')}
    db.close()
    return keys


def add_nxd_rows(cfg: dict, file: str, table: str, rows: dict[int, dict], pack_name: str = "0004.pac",
                 edit: Callable[[sqlite3.Connection], str] | None = None) -> str:
    """The game's NXD table in nxd/<file>.nxd with a row (column -> value, Key added) for each key it lacks, after
    edit (which gets the table's database and returns a note) changes the game's own rows. Returns a line for the
    report."""
    work = WORK / "remaster" / "build" / file
    db = sqlite3.connect(nxd_sqlite(cfg, file, pack_name))
    note = edit(db) if edit else ""
    have = {k for (k,) in db.execute(f'select Key from "{table}"')}
    added = [k for k in rows if k not in have]
    for k in added:
        row = {"Key": k, **rows[k]}
        names = ", ".join(f'"{c}"' for c in row)  # quoted: some tables' columns have "+" in them (CharShape)
        db.execute(f'insert into "{table}" ({names}) values ({", ".join("?" for _ in row)})', list(row.values()))
    db.commit()
    db.close()
    ff16tools(cfg, "sqlite-to-nxd", "-i", work / "table.sqlite", "-o", work / "out", "-t", table)
    built = next((work / "out").rglob("*.nxd"))
    target = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "nxd" / f"{file}.nxd"
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(built, target)
    return f"{file}.nxd: {len(added)} row(s) added ({', '.join(f'0x{k:X}' for k in added)}){note}"


def replace_nxd_rows(cfg: dict, file: str, table: str, rows: dict[int, list[dict]]) -> str:
    """The game's NXD table in nxd/<file>.nxd with the rows of each key (its first key column, Key) replaced by the
    given rows (column -> value, Key added). Returns a line for the report."""
    work = WORK / "remaster" / "build" / file
    db = sqlite3.connect(nxd_sqlite(cfg, file))
    for key, new in rows.items():
        db.execute(f'delete from "{table}" where Key = ?', (key,))
        for fields in new:
            row = {"Key": key, **fields}
            db.execute(f'insert into "{table}" ({", ".join(row)}) values ({", ".join("?" for _ in row)})',
                       list(row.values()))
    db.commit()
    db.close()
    ff16tools(cfg, "sqlite-to-nxd", "-i", work / "table.sqlite", "-o", work / "out", "-t", table)
    built = next((work / "out").rglob("*.nxd"))
    target = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "nxd" / f"{file}.nxd"
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(built, target)
    return f"{file}.nxd: {sum(len(r) for r in rows.values())} row(s) for {len(rows)} key(s)"




# The enhanced build's event text: Panzer files (nxd/text/scenario/scenarioNNNN.en.pzd: a line's Id, text and speaker),
# one per ScenarioId row, whose EventIds say which events use it; the event's message m shows the line with Id
# m + 0x40000 (the remaster renumbered its events' messages: event 2's 37856 is line 300000). Its own lines reachable
# that way end at 315159, so ReMixed's take Ids from 315160 (to 0x4FFFF); the code mod moves its events' messages
# (1, 2, ...) onto them as they run. The speaker is a Speaker row's key (its English name). The game loads the files
# the ScionFiles table lists (Key -> "nxd/text/scenario/scenarioNNNN.pzd", the language added), and a ScenarioId row's
# third column (UnknownC to FF16Tools) is the ScionFiles key of its file: key 10's is 1000, scenario0010.pzd.
PZD_FIRST_ID, PZD_LAST_ID, PZD_MESSAGE_OFFSET = 315160, 0x4FFFF, 0x40000
SCENARIO_TEXT_FIRST_KEY = 5015  # ReMixed's text files take the ScenarioId keys the remaster doesn't use from here
SPEAKER_FIRST = 6990000  # new Speaker rows' keys (the remaster's run to 7000200)
# Its font (EVENT/FONT.BIN, which ReMixed edited) has parentheses at 0x8D and 0x8E ("(We wear the same vestments...)";
# vanilla's names have a hyphen at 0x8D), so its text reads these before fft_text's characters.
REMIXED_TEXT = {0xFA: " ", 0x3E: "!", 0x40: "?", 0x5F: ".", 0x93: "'", 0x91: '"', 0x46: ":", 0x44: "/", 0x42: "+",
                0x8D: "(", 0x8E: ")"}
# Its two-byte characters. D1 17 is the PSP script's dash ("the enemy stands before us—attack!"; 93 in the shipped
# events, which showed as an N, its second byte, before the build knew it), as the remaster's own script writes it
# (U+2014, 220 times); DA 75 a semicolon ("Same last name; I'm the husband."), D9 B8 a percent sign ("tax by 30%").
REMIXED_TEXT_PAIRS = {b"\xda\x74": ",", b"\xd1\x1d": "-", b"\xd1\x29": "*", b"\xda\x76": "'", b"\xd1\x17": "—",
                      b"\xda\x75": ";", b"\xd9\xb8": "%", b"\xd9\xb7": "&",  # D9 B7: the tavern's "Save & Formation"
                      b"\xda\x73": " ",  # DA 73: a space (the tavern's unit card: "Brave" DA 73 65, as the PS1 shows it)
                      # D9 C7: the Triangle button (the Treasure Wheel's "Press <D9><C7> to pay gil...", read on
                      # Triangle, 0x0010), as the remaster's own text shows a button: its key icon by its input action
                      # (UIKeyAssign 30, "menu", which the code mod reads as Triangle: Ps1Pad.Actions).
                      b"\xd9\xc7": "<keyicon=menu>"}
# The tavern's own look, kept in its text (markup): ReMixed's colours (E3 xx: 08 red, its names and emphasis; 04 grey,
# a menu row that can't be picked; 00 the text's own again) as the remaster's text colours, UIColor rows (AABBGGRR):
# 61 a red (183, 72, 72) and 116 a dark grey (115, 98, 86, the remaster's own "No active equipment effects."); its
# zodiac glyphs (DA 00-0C, Aries to Serpentarius) as the remaster's zodiac icons (the Icon table's 450-462,
# zodiac_sign zs_000-012); and its line breaks, where no one speaks (a unit's card, a mission's needs), kept.
REMIXED_COLOURS = {0x08: 61, 0x04: 116}
ZODIAC_GLYPH_FIRST, ZODIAC_GLYPHS, ZODIAC_ICON_FIRST = 0xDA00, 13, 450


# ReMixed's text (vanilla's encoding): a message ends with FE (closed with a button) or FF (closed by the event: after a
# wait, or a menu's answer); FD keeps its box open while the next one shows. Control codes take one byte after them
# (E2 a wait, E3 08/00 a speaker's name, E6, E8), and D0-DA start two-byte characters. FB ... FC is a list of answers
# to pick from, an F8 between them (vanilla's menus are the same); the remaster writes a menu as its line's text,
# <cursor>1. ...\n2. ...<cursorend><forced>, after a <speaker="Name"> or <hidespeaker> tag, and puts the answer in var
# 0x18 from 0 as vanilla did.
MESSAGE_ENDS = (0xFE, 0xFF)
TEXT_CODES_WITH_A_BYTE = (0xE2, 0xE3, 0xE6, 0xE8, 0xF5, 0xF6)
TWO_BYTE_CHARACTERS = range(0xD0, 0xDB)
MENU_START, MENU_END = 0xFB, 0xFC
# ReMixed's text sets an event variable as the text reaches it: F5 66 F6 a F5 b F6 v sets variable 10a + b to v (F5 and
# F6 each take a byte), and its event waits for that with vanilla's 0x7E (var, value) to time a gesture to the words
# (0x0F9's "Come, join me in death" sets var 0x18 to 1 before Gareth beckons; 0x119's, on to chapter 2, sets var 1).
# The remaster's text has nothing like it, so the wait never ended (0x0F9 stood still on the PC, its codes showing as
# "241"). The build drops the codes from the lines and makes each such wait set the variable instead (0xAE, the same
# length): the event goes on at once rather than when the text gets there.
TEXT_SYNC = b"\xf5\x66"
WAIT_VALUE, SET_VALUE = 0x7E, 0xAE
# Variables a text sets with no event waiting for them, whose codes are only dropped: 83 (0x53), which ReMixed's events
# also set around a line (The Guarded Temple's 0x4B and 0x5F before an Automaton speaks, 0 after; it looks like the
# speaker's dialogue sound), and which Be A Beta's ArmoredKori switches mid-line (2, then a "click" and 0, then 126).
TEXT_SYNC_UNWAITED = {83}
# ReMixed's text made while its events run, as its dispatch tavern's is (TAVERN). Its codes that take the next of its
# list words (0x80165EF4, the list 0x9A-0x9E fill; in a line, the k-th of them takes word k): 0xEB shows that word + 1
# as one of the event's own messages (a name; a menu's rows with their line breaks; a colour; a plural's "s"), 0xE4
# shows it as a number (the Preparation Menu puts the price and the gil in words 0 and 1 for its two). 0xE6 xx shows
# word xx as a number. The build keeps them in the tavern's lines as tokens and writes those lines, and each of its
# messages as a slot shows it, in SLOTS_FILE; the code mod fills them in as a line shows (Patches/RemixedText.cs). The
# lines the game reads have them left out.
SLOTS_FILE = "remixed-slots.json"
SLOT_MESSAGE, SLOT_NUMBER, SLOT_WORD = "{eb}", "{n}", "{w:%02X}"
# ReMixed's E8 xx before a number (E4) right-aligns it in a field of xx characters (a unit card's "Brave  65", a
# mission's "Odds:  0%"): kept as {nX}, so a box sized for one value fits the next.
SLOT_FIELD = "{n%d}"
SLOT_CODES = (0xEB, 0xE4)


def text_syncs(text: bytes) -> set[tuple[int, int]]:
    """The (variable, value) pairs an event's text sets with ReMixed's text code (TEXT_SYNC)."""
    out, at = set(), 0
    while at < len(text):
        sync = text[at:at + 8]
        if sync[:2] == TEXT_SYNC and len(sync) == 8 and sync[2] == sync[6] == 0xF6 and sync[4] == 0xF5:
            out.add((10 * sync[3] + sync[5], sync[7]))
            at += 8
            continue
        at += 2 if text[at] in TEXT_CODES_WITH_A_BYTE or text[at] in TWO_BYTE_CHARACTERS else 1
    return out


def text_sync_waits(code: bytes, lengths: bytes, syncs: set[tuple[int, int]]) -> tuple[bytes, int]:
    """ReMixed's event code (the PS1 layout) with each 0x7E wait for a (variable, value) its text sets made a SET of
    it, and the number made."""
    out, at, done, made = bytearray(code), 0, set(), 0
    while at < len(code):
        op = code[at]
        if op == WAIT_VALUE and (u16(code, at + 1), u16(code, at + 3)) in syncs:
            out[at] = SET_VALUE
            done.add((u16(code, at + 1), u16(code, at + 3)))
            made += 1
        at += 1 + lengths[op]
    missing = {sync for sync in syncs - done if sync[0] not in TEXT_SYNC_UNWAITED}
    if missing:
        sys.exit(f"a ReMixed event's text sets {sorted(missing)} (variable, value) with no 0x7E waiting for it")
    return bytes(out), made


# ReMixed's PSP script (the translation the port shows, SCRIPT_VERSION) runs some lines together where its PSX script
# has a message each, or splits one the PSX has whole, and its events name a PSP message as the PSX's number plus the
# PSX's count, so from there on they showed the line after or before the one meant (seen on the PC in 0x0F4: its
# question whether to fight came a line early, on Balmafula's line, and the knight's thanks went to Agrias's box). The
# build puts the messages back in step: (event: [("split", message, the bytes it's cut before, their first byte a
# space or line break that goes), ("merge", message: with the next one)], in turn, numbered as they stand then).
NAME_AFTER_BREAK = bytes([0xF8, 0xE3, 0x08])  # a line break, then a speaker's name (E3 08 ... E3 00)
MESSAGE_FIXES: dict[int, list[tuple]] = {
    0xF4: [("split", 32, NAME_AFTER_BREAK)],          # Agrias's "We do but pass through..." / the knight's "Yet... we..."
    0xF8: [("split", 33, bytes([0xF8, 0x11, 0x24, 0x2B])),  # the double's "...Motherfucking Oaks." / Agrias's "Hah hah!"
           ("split", 34, NAME_AFTER_BREAK),           # / the double's "Pitiful..."
           ("merge", 39)],                            # "No? Are you quite certain?..." with "The nobles hoarding..."
    0xEF: [("merge", 39)],   # Agrias's "Count yourself fortunate..." with "Were it otherwise, I would drag you..."
    0x114: [("merge", 24)],  # Bryn's "The claim that a price lay upon our heads..." with "Corrupt knights would scarce..."
    0x116: [("merge", 43)],  # the knight's "'Tis well. You have our gratitude..." with "Or at least, to any that would..."
}


def fixed_messages(messages: list[bytes], fixes: list[tuple]) -> list[bytes]:
    """An event's messages with MESSAGE_FIXES's splits and merges."""
    out = list(messages)
    for fix in fixes:
        n = fix[1]
        if fix[0] == "split":
            message, at = out[n - 1], out[n - 1].find(fix[2])
            if at < 0:
                sys.exit(f"MESSAGE_FIXES: message {n} has no {fix[2].hex(' ')}")
            out[n - 1:n] = [message[:at] + message[-1:], message[at + 1:]]
        else:
            out[n - 1:n + 1] = [out[n - 1][:-1] + bytes([0xF8]) + out[n]]  # a line break between them
    return out


def remixed_messages(text: bytes) -> list[bytes]:
    """An event's text split into its messages (each with its end), numbered from 1 as its MESSAGEs name them."""
    messages, at, start = [], 0, 0
    text = text.rstrip(b"\0")
    while at < len(text):
        b = text[at]
        if b in TEXT_CODES_WITH_A_BYTE or b in TWO_BYTE_CHARACTERS:
            at += 2
            continue
        at += 1
        if b in MESSAGE_ENDS:
            messages.append(text[start:at])
            start = at
    if text[start:]:
        messages.append(text[start:])
    return messages


def remixed_line(message: bytes, slots: bool = False, markup: bool = False) -> tuple[str, str]:
    """(speaker, text) of a ReMixed message. E3 08 <name> ... E3 00 is a speaker's name when nothing shows before it in
    the message and a line break ends it (F8 before or just after the E3 00), or when it repeats the speaker's (a long
    message's later pages); anywhere else it's emphasis, and its words stay in the text (also when the message ends
    before an E3 00). F8 is a line break (a space here: the remaster flows its lines); E0 is Ramza's name. A list of
    answers (MENU_START) becomes the remaster's menu, with a <speaker> tag for a named speaker. With slots, the codes
    the code mod fills in (SLOTS_FILE) are kept as tokens. With markup (the tavern's own look: REMIXED_COLOURS), its
    emphasis and other colours are the remaster's colour tags, its zodiac glyphs icons, and where no one speaks its
    line breaks are kept."""
    speaker, text, at = "", [], 0
    naming, name, broke = False, [], False
    answers: list[list[str]] | None = None
    listing = False
    coloured = False  # markup: a colour tag open
    slotted = False  # markup with slots: a slot since the last colour code, which may have opened a colour itself

    def shown() -> bool:
        return bool("".join(text).strip()) or answers is not None

    def out() -> list[str]:
        return answers[-1] if listing else text

    def colour(code: int) -> None:  # markup: E3 code as the remaster's tags (closing the one open)
        nonlocal coloured, slotted
        if coloured or (slotted and code not in REMIXED_COLOURS):  # one a slot opened: the code mod pairs them up
            out().append("</color>")
            coloured = False
        slotted = False
        if code in REMIXED_COLOURS:
            out().append(f"<color={REMIXED_COLOURS[code]}>")
            coloured = True

    def emphasis(said: str) -> str:
        return f"<color={REMIXED_COLOURS[0x08]}>{said}</color>" if markup and said else said

    while at < len(message):
        b = message[at]
        if b == 0xE3:
            if message[at + 1] == 0x08:
                # A red run with slots that another follows (the tavern's rewards for a group of two or three, a row
                # each: E3 08 <slots> F8 E3 08 <slots>) is text, kept, not a name the next one replaces.
                if naming and markup and "{" in "".join(name):
                    out().append(emphasis("".join(name).rstrip("\n")) + "\n" * ("".join(name).endswith("\n")))
                if markup and coloured:
                    colour(0)
                naming, name, broke = True, [], False
            elif naming:
                raw = "".join(name)
                naming, said = False, " ".join(raw.split())
                after = at + 2  # what follows the name, past spaces and waits (the tavern pads its names)
                while message[after:after + 1] in (b"\xfa", b"\xe2"):
                    after += 2 if message[after] == 0xE2 else 1
                # With markup a run with slots or a line break inside isn't a name: the start of a unit's card (its
                # number in red, the rest coloured by its slots), kept as laid out.
                laid_out = markup and ("{" in raw or "\n" in raw.strip("\n"))
                named = False
                if not speaker and not shown() and (broke or message[after:after + 1] == b"\xf8") and not laid_out:
                    speaker, named = said, True
                elif laid_out:
                    out().append(emphasis(raw))
                elif said != speaker:
                    out().append(emphasis(said))
                else:
                    named = True
                if markup and not (named and message[at + 1] not in REMIXED_COLOURS):  # a name's end shows nothing
                    colour(message[at + 1])
            elif markup:
                colour(message[at + 1])
            at += 2
            continue
        if slots and (b == 0xE6 or b in SLOT_CODES):  # a slot the code mod fills in (SLOTS_FILE)
            ch = SLOT_WORD % message[at + 1] if b == 0xE6 else SLOT_MESSAGE if b == 0xEB else SLOT_NUMBER
            at += 2 if b == 0xE6 else 1
            (name if naming else out()).append(ch)
            slotted = slotted or b == 0xEB
            continue
        if slots and markup and b == 0xE8 and message[at + 2:at + 3] == bytes([0xE4]):  # a number in a field of its own
            (name if naming else out()).append(SLOT_FIELD % message[at + 1])
            at += 3
            continue
        if b in (0xE2, 0xE6, 0xE8, 0xF5, 0xF6):  # codes with a byte that show nothing (F5, F6: TEXT_SYNC)
            at += 2
            continue
        if b in (MENU_START, MENU_END):
            if markup and coloured:
                colour(0)
            listing = b == MENU_START
            answers = [[]] if listing else answers
            at += 1
            continue
        pair = message[at:at + 2]
        if pair in REMIXED_TEXT_PAIRS:
            ch = REMIXED_TEXT_PAIRS[pair]
            at += 2
        elif markup and b in TWO_BYTE_CHARACTERS:
            glyph = b << 8 | message[at + 1] if at + 1 < len(message) else 0
            k = glyph - ZODIAC_GLYPH_FIRST
            ch = f"<icon={ZODIAC_ICON_FIRST + k}>" if 0 <= k < ZODIAC_GLYPHS else ""
            at += 2
        elif b == 0xF8:
            at += 1
            if naming:
                broke = True
                if markup:
                    name.append("\n")
                continue
            if listing:
                if markup and coloured:
                    colour(0)
                answers.append([])
                continue
            ch = "\n" if markup else " "
        elif b == 0xE0:
            ch = "Ramza"
            at += 1
        else:
            ch = REMIXED_TEXT.get(b, fft_text.CHARS.get(b, ""))
            at += 1
        if naming:
            name.append(ch)
        else:
            out().append(ch)
    # A red run the message ends in is its speaker's name, unless slots fill it: the tavern's rewards ("<job> JP: +N",
    # E3 08 then its slots, the colour closed by a slot) are red text, and as a speaker they showed with no colour.
    if naming and not speaker and not shown() and not (markup and "{" in "".join(name)):
        speaker = " ".join("".join(name).split())
    elif naming:  # emphasis the message ends in (the tavern's quoted mission name: E3 08 "<name>". with no E3 00)
        out().append(emphasis("".join(name)))
    if markup and coloured:
        colour(0)
    joined = "".join(text)
    if markup and not speaker:  # its own line breaks and spacing (a unit's card, a mission's needs), as laid out
        line = "\n".join(part.rstrip() for part in joined.strip("\n").split("\n"))
    else:
        line = " ".join(joined.split())
    if answers is None:
        return speaker, line
    choices = [" ".join("".join(answer).split()) for answer in answers]
    tag = f'<speaker="{speaker}">' if speaker else ""
    lead = "\n".join(lead_rows(line)) + "\n" if line else ""
    return speaker, tag + lead + "<cursor>" + "\n".join(c for c in choices if c) + "<cursorend><forced>"


# A list of answers with a sentence before it (1-A's river camp: Agrias's "Nay. Beasts gather often by the waters..."
# and her three answers) ran past its box: the remaster wraps a long row itself, but the code mod's tall boxes
# (Patches/TallMenus.cs) count a text's rows by its line breaks, so the box stayed three rows high with six in it, and
# the cursor stood two rows up (seen on the PC, 2026-11-19). A lead's rows past MENU_LEAD_CHARS (a speech box takes
# about 62 beside a portrait) are broken at their spaces here, so every row is one the tall boxes count.
MENU_LEAD_CHARS = 56


def lead_rows(text: str) -> list[str]:
    """A menu's lead as the rows it shows: each of its own rows longer than MENU_LEAD_CHARS broken at spaces (a row
    with markup, the tavern's, is left whole)."""
    rows = []
    for row in text.split("\n"):
        if len(row) > MENU_LEAD_CHARS and "<" not in row and "{" not in row:
            rows += textwrap.wrap(row, MENU_LEAD_CHARS, break_long_words=False, break_on_hyphens=False)
        else:
            rows.append(row)
    return rows


def slot_part(message: bytes) -> str:
    """A ReMixed message as a slot (0xEB) shows it inside another line (SLOTS_FILE): its characters, a line break (F8)
    as "\\n" (a menu's next row), its own slots as tokens, its colours as the remaster's colour tags (a slot that's only
    a colour, as a menu row's or a unit card's are, is a lone tag: the code mod pairs them up) and its zodiac glyphs as
    icons (REMIXED_COLOURS), its other codes left out."""
    out, at = [], 0
    while at < len(message):
        b = message[at]
        pair = message[at:at + 2]
        if b in MESSAGE_ENDS:
            break
        if b == 0xE6:
            out.append(SLOT_WORD % message[at + 1])
            at += 2
        elif b == 0xE3 and at + 1 < len(message):
            code = message[at + 1]
            out.append(f"<color={REMIXED_COLOURS[code]}>" if code in REMIXED_COLOURS else "</color>")
            at += 2
        elif b == 0xE8 and message[at + 2:at + 3] == bytes([0xE4]):
            out.append(SLOT_FIELD % message[at + 1])
            at += 3
        elif b in SLOT_CODES:
            out.append(SLOT_MESSAGE if b == 0xEB else SLOT_NUMBER)
            at += 1
        elif b in TEXT_CODES_WITH_A_BYTE:
            at += 2
        elif pair in REMIXED_TEXT_PAIRS:
            out.append(REMIXED_TEXT_PAIRS[pair])
            at += 2
        elif b in TWO_BYTE_CHARACTERS:
            k = (b << 8 | message[at + 1] if at + 1 < len(message) else 0) - ZODIAC_GLYPH_FIRST
            if 0 <= k < ZODIAC_GLYPHS:
                out.append(f"<icon={ZODIAC_ICON_FIRST + k}>")
            at += 2
        elif b == 0xF8:
            out.append("\n")
            at += 1
        elif b == 0xE0:
            out.append("Ramza")
            at += 1
        elif b in (MENU_START, MENU_END):
            at += 1
        else:
            out.append(REMIXED_TEXT.get(b, fft_text.CHARS.get(b, "")))
            at += 1
    return "".join(out)


def drop_reused_texts(db: sqlite3.Connection, owned: set[int]) -> str:
    """Take every generated text event off the remaster's original ``ScenarioId`` rows.

    A generated event must have exactly one text owner: the row written for it below. This used to remove only
    ``REUSED_SCENARIO_NUMBERS``, overlooking synthetic battle/post-battle event numbers which happened to have a stock
    text row. Event 0x34D then loaded the stock Orlandeau/Folmarv file instead of Mandalia's, so valid Luso and Ramza
    line ids drew empty boxes. A row left with no events is removed. Returns a note for the report.
    """
    dropped = []
    for key, ids in db.execute('select Key, EventIds from "ScenarioId"').fetchall():
        events = json.loads(ids or "[]")
        kept = [e for e in events if e not in owned]
        if kept == events:
            continue
        dropped += [f"0x{e:X} (row {key})" for e in events if e in owned]
        if kept:
            db.execute('update "ScenarioId" set EventIds = ? where Key = ?', (json.dumps(kept, separators=(",", ":")), key))
        else:
            db.execute('delete from "ScenarioId" where Key = ?', (key,))
    return f"; the remaster's text taken off {', '.join(dropped)}" if dropped else ""


def write_scenario_texts(cfg: dict, written: list[tuple[list[int], int, list]]) -> str:
    """ReMixed's scenes' lines as the remaster's Panzer files (one per event: the events that show it, its first line
    Id, its (speaker, text) lines), each under a ScenarioId key the remaster doesn't use, with the ScenarioId rows that
    give them to the events (several, as some of the remaster's own rows list) and Speaker rows for its speakers' names
    the remaster hasn't. Returns a line for the report."""
    if not written:
        return "no text"
    taken = nxd_keys(cfg, "scenarioid", "ScenarioId")
    free = (k for k in range(SCENARIO_TEXT_FIRST_KEY, 10000) if k not in taken)
    texts = {next(free): text for text in written}
    english = WORK / "remaster" / "build" / "speakers"
    shutil.rmtree(english, ignore_errors=True)
    (english / "in").mkdir(parents=True)
    shutil.copy(english_nxd(cfg, "Speaker"), english / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", english / "in", "-o", english / "table.sqlite")
    db = sqlite3.connect(english / "table.sqlite")
    speakers = {name: key for key, name in db.execute('select Key, Name from "Speaker-en"')}
    db.close()
    new_speakers: dict[int, dict] = {}
    for _, _, lines in texts.values():
        for speaker, _ in lines:
            if speaker and speaker not in speakers:
                key = SPEAKER_FIRST + len(new_speakers)
                speakers[speaker] = key
                new_speakers[key] = {"DLCFlags": 0, "Name": speaker}
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "nxd" / "text" / "scenario"
    out.mkdir(parents=True, exist_ok=True)
    work = WORK / "remaster" / "build" / "pzd"
    shutil.rmtree(work, ignore_errors=True)
    work.mkdir(parents=True)
    for key, (event, first, lines) in texts.items():
        entries = []
        for k, (speaker, text) in enumerate(lines):
            entries.append(f"- Id: {first + k}\n  Line: {json.dumps(text)}\n  Speaker:\n    Type: 24\n"
                           f"    Value: {speakers.get(speaker, 0)}\n    TypeName: \n  VoiceSoundPath: ''\n"
                           f"  ShowType: Normal\n  ShortVoiceSoundPath: ''\n  IsShortened: false\n")
        yaml = work / f"scenario{key:04d}.en.yaml"
        yaml.write_text("".join(entries), encoding="utf-8")
        # pzd-conv writes scenarioNNNN.en.pzd beside the yaml (and takes no game option, unlike ff16tools' others).
        subprocess.run([cfg["tools"]["ff16tools"], "pzd-conv", "-i", str(yaml)], check=True, capture_output=True)
        shutil.copy(yaml.with_suffix(".pzd"), out / yaml.with_suffix(".pzd").name)
    first_file = max(nxd_keys(cfg, "scionfiles", "ScionFiles")) + 1
    files = {key: first_file + k for k, key in enumerate(texts)}
    owned = {event for events, _, _ in texts.values() for event in events}
    listed = add_nxd_rows(cfg, "scionfiles", "ScionFiles",
                          {files[key]: {"DLCFlags": 0, "File": f"nxd/text/scenario/scenario{key:04d}.pzd"} for key in texts})
    rows = add_nxd_rows(cfg, "scenarioid", "ScenarioId",
                        {key: {"DLCFlags": 0, "EventIds": json.dumps(events, separators=(",", ":")),
                               "UnknownC": files[key]}
                         for key, (events, _, _) in texts.items()},
                        edit=lambda db: drop_reused_texts(db, owned))
    # Audit the final table, not only the generated scripts: a line id can be perfectly valid yet draw blank when a
    # second ScenarioId row claims the same event and the runtime loads that other PZD file first.
    db = sqlite3.connect(WORK / "remaster" / "build" / "scenarioid" / "table.sqlite")
    owners: dict[int, list[int]] = {event: [] for event in owned}
    for key, ids in db.execute('select Key, EventIds from "ScenarioId"'):
        for event in json.loads(ids or "[]"):
            if event in owners:
                owners[event].append(key)
    db.close()
    duplicates = {event: keys for event, keys in owners.items() if len(keys) != 1}
    if duplicates:
        sys.exit("generated event text has non-unique ScenarioId ownership: "
                 + ", ".join(f"0x{event:X} -> {keys}" for event, keys in sorted(duplicates.items())))
    said = add_nxd_rows(cfg, "speaker.en", "Speaker-en", new_speakers, "0004.en.pac") if new_speakers else "no new speakers"
    return (f"text: {sum(len(l) for _, _, l in texts.values())} line(s) in {len(texts)} scenario file(s); {listed}; "
            f"{rows}; {said}")


def write_world_starts(cfg: dict) -> str:
    """ReMixed's side events' starts (REMIXED_WORLD_STARTS) as entries in the remaster's world-map conditions (the NXD
    table EnhancedWorldEvent: Key the location, Key2 the entry, Key3 the instruction; Type 1 compares the variable in
    Unknown10 with Unknown8's value, Type 25 starts Unknown8's event with its mode, as the PS1's opcodes 0x01 and 0x19
    do). Each goes after the location's own entries. Returns a line for the report."""
    if not REMIXED_WORLD_STARTS:
        return "none"
    work = WORK / "remaster" / "build" / "enhancedworldevent"
    db = sqlite3.connect(nxd_sqlite(cfg, "enhancedworldevent"))
    table = "EnhancedWorldEvent"
    done = []
    # The remaster's chapter-1 starts (var 0x6E = 1-13, then a Type 25) wait while side chapter 1-A runs: a first
    # instruction "flag SIDE_CHAPTER_A_FLAG = 0" (the entry's others move up one). Every start of the remaster's waits
    # while the Algus campaign runs likewise ("flag ALGUS_FLAG = 0").
    guarded, waiting = [], 0
    def guard(location: int, entry: int, flag: int) -> None:
        db.execute(f'update "{table}" set Key3 = Key3 + 1000 where Key = ? and Key2 = ?', (location, entry))
        db.execute(f'update "{table}" set Key3 = Key3 - 999 where Key = ? and Key2 = ?', (location, entry))
        db.execute(f'insert into "{table}" (Key, Key2, Key3, DLCFlags, Type, Unknown8, Unknown10, Unknown14, Unknown18, '
                   f'Unknown1C) values (?, ?, 0, 0, 1, ?, ?, ?, ?, 0)',
                   (location, entry, json.dumps([0]), flag, WORLD_EVENT_UNUSED, WORLD_EVENT_UNUSED))
    for location, entry in db.execute(f'select distinct Key, Key2 from "{table}" where Type = 25').fetchall():
        guard(location, entry, ALGUS_FLAG)
        waiting += 1
        checks = db.execute(f'select Unknown8 from "{table}" where Key = ? and Key2 = ? and Type = 1 and Unknown10 = ?',
                            (location, entry, 0x6E)).fetchall()
        if not any(json.loads(values)[0] in CHAPTER_1_PROGRESS for (values,) in checks):
            continue
        guard(location, entry, SIDE_CHAPTER_A_FLAG)
        guarded.append(f"{location}/{entry}")
    done.append(f"{len(guarded)} chapter-1 start(s) wait while 1-A runs ({', '.join(guarded)}), {waiting} while the "
                f"Algus campaign does")
    for (location, event), theirs in REMASTER_STARTS_REPLACED.items():
        starts = [(entry, step, json.loads(values)) for entry, step, values in db.execute(
            f'select Key2, Key3, Unknown8 from "{table}" where Key = ? and Type = 25', (location,))]
        starts = [(entry, step, values) for entry, step, values in starts if values and values[0] == event]
        if len(starts) != 1:
            sys.exit(f"the remaster's start of event {event} at location {location} isn't one entry "
                     "(REMASTER_STARTS_REPLACED)")
        entry, step, values = starts[0]
        values[0] = REMIXED_SCENARIOS[theirs]
        db.execute(f'update "{table}" set Unknown8 = ? where Key = ? and Key2 = ? and Key3 = ?',
                   (json.dumps(values), location, entry, step))
        done.append(f"location {location} entry {entry}'s {event} runs 0x{theirs:X} (0x{values[0]:X}) instead")
    # 1-A's world-map steps (REMIXED_WORLD_STEPS) and the Algus campaign's (ALGUS_WORLD_STEPS), an entry each, before
    # the starts at the same place: a start that cuts straight to its event (0x0F9 at location 27) would otherwise come
    # first, and the story moves on past them. Each waits for its own mode (its flag = 1).
    for name, all_steps, mark in OWN_STORY_STEPS:
        steps = 0
        for location, story, actions in all_steps:
            for flag, kind, place, other in actions:
                last = db.execute(f'select max(Key2) from "{table}" where Key = ?', (location,)).fetchone()[0]
                entry = 0 if last is None else last + 1
                # Type 32 names the destination first and the source second. ReMixed's op 0x20 data and the tuples
                # above name them source, destination, so reverse only the serialized operands. With them unswapped
                # the remaster animates every new road backwards, from the newly revealed node to the party.
                first, second = ((other, place) if kind == WORLD_ROUTE else
                                 (place, WORLD_EVENT_UNUSED if other is None else other))
                if isinstance(story, tuple):
                    comparison, story_value = story
                    if comparison not in (WORLD_AT_LEAST, WORLD_AT_MOST):
                        raise ValueError(f"unsupported world-step story comparison {comparison}")
                    story_row = (comparison, json.dumps([0x6E, story_value]), 0,
                                 WORLD_EVENT_UNUSED, WORLD_EVENT_UNUSED)
                else:
                    story_row = (1, json.dumps([story]), 0x6E, WORLD_EVENT_UNUSED, WORLD_EVENT_UNUSED)
                rows = [story_row,
                        (1, json.dumps([0]), flag, WORLD_EVENT_UNUSED, WORLD_EVENT_UNUSED),
                        (1, json.dumps([1]), mark, WORLD_EVENT_UNUSED, WORLD_EVENT_UNUSED),
                        (kind, json.dumps([]), 0, first, second)]
                for k, (row_type, values, variable, first, second) in enumerate(rows):
                    db.execute(f'insert into "{table}" (Key, Key2, Key3, DLCFlags, Type, Unknown8, Unknown10, Unknown14, '
                               f'Unknown18, Unknown1C) values (?, ?, ?, 0, ?, ?, ?, ?, ?, 0)',
                               (location, entry, k, row_type, values, variable, first, second))
                steps += 1
        done.append(f"{steps} of {name}'s world-map steps at locations "
                    f"{', '.join(str(location) for location, _, _ in all_steps)}")
    for location, checks, theirs, mode in REMIXED_WORLD_STARTS:
        ours = REMIXED_SCENARIOS[theirs]
        last = db.execute(f'select max(Key2) from "{table}" where Key = ?', (location,)).fetchone()[0]
        entry = 0 if last is None else last + 1
        # Types 2 and 3 take the variable and the value from Unknown8 (WORLD_AT_LEAST); type 1 the variable from Unknown10.
        rows = [(kind[0] if kind else 1,
                 json.dumps([variable, value] if kind and kind[0] in (WORLD_AT_LEAST, WORLD_AT_MOST) else [value]),
                 variable) for variable, value, *kind in checks]
        rows += [(25, json.dumps([ours, mode]), 0)]
        for k, (kind, values, variable) in enumerate(rows):
            db.execute(f'insert into "{table}" (Key, Key2, Key3, DLCFlags, Type, Unknown8, Unknown10, Unknown14, Unknown18, '
                       f'Unknown1C) values (?, ?, ?, 0, ?, ?, ?, ?, ?, 0)',
                       (location, entry, k, kind, values, variable, WORLD_EVENT_UNUSED, WORLD_EVENT_UNUSED))
        done.append(f"0x{theirs:X} (0x{ours:X}) at location {location} entry {entry}")
    db.commit()
    db.close()
    ff16tools(cfg, "sqlite-to-nxd", "-i", work / "table.sqlite", "-o", work / "out", "-t", table)
    target = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "nxd" / "enhancedworldevent.nxd"
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(next((work / "out").rglob("*.nxd")), target)
    return ", ".join(done)


# The Algus campaign step by step (docs/analysis/story-flow-plan.md, "The Algus campaign"), for the build's check of it
# (check_algus_campaign): (ReMixed's event, the one after it, whether the world map comes between them: the event sets
# the story's progress and goes back to the world map, where a start of REMIXED_WORLD_STARTS runs the next). After
# 0x07C, ReMixed's ending, the two rooms of the Depths of Murond; 0x0A3's exits include the world map.
ALGUS_CHAIN = [(0x0FD, 0x006, False), (0x006, 0x007, False), (0x007, 0x008, True), (0x008, 0x105, False),
               (0x105, 0x00A, True), (0x00A, 0x00B, True), (0x00B, 0x00C, False), (0x00C, 0x00D, True),
               (0x00D, 0x00E, False), (0x00E, 0x00F, True), (0x00F, 0x010, True), (0x010, 0x011, False),
               (0x011, 0x012, False), (0x012, 0x013, True), (0x013, 0x106, False), (0x106, 0x0FE, False),
               (0x0FE, 0x0FF, True), (0x0FF, 0x100, False), (0x100, 0x101, False), (0x101, 0x102, False),
               (0x102, 0x103, False), (0x103, 0x07B, False), (0x07B, 0x07C, False), (0x07C, 0x0A2, False),
               (0x0A2, 0x0A3, False), (0x0A3, None, True)]
# Its battles: each loads conditions with a set past its opening's that runs it again (its victory; 0x0FD's, Algus
# falling, its second half).
ALGUS_BATTLES = {0x0FD, 0x006, 0x00A, 0x00B, 0x00D, 0x010, 0x0FF, 0x100, 0x101, 0x102, 0x07B}
# Hand-offs in its events that are never reached: 0x007's to ReMixed's random battles' setup (0x191) sits in its block
# 0xF0, which its phase skip passes over; 0x008's to 0x009 is the PSX script's branch (SCRIPT_VERSION).
ALGUS_DEAD_HAND_OFFS = {0x007: {0x191}, 0x008: {0x009}}
# 0x102's other end: Algus gives up at Fort Zeakden, and the remaster's end of chapter 1 follows (REMASTER_HAND_OFFS),
# with the campaign's flag cleared (EVENT_CODE_INSERTS) and the roster given back (0x102's own code).
ALGUS_WAY_OUT = (0x102, 0x5A)
# The Depths' New Game+ choice starts the game's scenario 1 directly; it is deliberately a remaster scenario rather
# than one of the mod's renumbered events. Its other scenario hand-off is back to its own Completion Points room.
DEPTHS_REMASTER_HAND_OFFS = {0x001}
WORLD_LOCATION_VAR = 0x31  # the event variable that holds the party's location on the world map (0x2E9898(0x31))


def check_algus_campaign(lengths: bytes, presets: bytes) -> str:
    """The Algus campaign as built, walked from its start to its end without the game (ALGUS_CHAIN): each step's
    hand-off in its event's code, or the story progress it sets and the world-map start that runs the next, which must
    be at a place the party can reach by the roads shown so far (the world-map table as built: the remaster's own
    steps, which the campaign shares, and ALGUS_WORLD_STEPS); its battles' conditions (ALGUS_BATTLES; presets as
    built); every hand-off in its events going to one of the mod's scenarios or a scene of the remaster's that stands
    for one of ReMixed's; its way out (ALGUS_WAY_OUT) and its end. Exits with what's wrong. Returns a line for the
    report."""
    table = eiu_events.remaster_lengths(lengths)
    script = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "script" / "enhanced"
    wrong: list[str] = []
    data = (MOD_DIR / REMIXED_SCENARIOS_FILE).read_bytes()  # its records first: a u16 count, then the records
    records = {u16(data, 2 + SCENARIO_SIZE * k): data[2 + SCENARIO_SIZE * k:2 + SCENARIO_SIZE * (k + 1)]
               for k in range(u16(data, 0))}
    # Then the formations (12 bytes each), places (4), message bases (6), offers (5) and the scenarios' own units (a
    # u16 scenario and its 16 units: write_remixed_scenarios), each a u16 count first.
    at = 2 + SCENARIO_SIZE * u16(data, 0)
    for size in (FORMATION_SIZE, 4, 6, 5):
        at += 2 + size * u16(data, at)
    unit_sets = {u16(data, at + 2 + k * (2 + UNITS_PER_EVENT * UNIT_SIZE)):
                 data[at + 4 + k * (2 + UNITS_PER_EVENT * UNIT_SIZE):at + 2 + (k + 1) * (2 + UNITS_PER_EVENT * UNIT_SIZE)]
                 for k in range(u16(data, at))}

    def code(theirs: int) -> bytes:
        data = (script / f"event{REMIXED_SCENARIOS[theirs]:03d}.e").read_bytes()
        if not data.startswith(REMIXED_EVENT_MARK):
            sys.exit(f"the mod's event for ReMixed's 0x{theirs:X} hasn't the upgrade's mark")
        return data[len(REMIXED_EVENT_MARK):]

    def instructions(c: bytes):
        at = 0
        while at < len(c):
            yield c[at], c[at + 1:at + 1 + table[c[at]]]
            at += 1 + table[c[at]]

    def set_to(c: bytes, var: int) -> int | None:  # what the event leaves in a variable (SET, or ZERO then ADD)
        value = None
        for op, p in instructions(c):
            if op == 0xAE and u16(p, 0) == var:
                value = u16(p, 2)
            elif op == 0xBE and u16(p, 0) == var:
                value = 0
            elif op == 0xB0 and u16(p, 0) == var and value is not None:
                value += u16(p, 2)
        return value

    def name(theirs: int) -> str:
        return f"0x{theirs:03X} (0x{REMIXED_SCENARIOS[theirs]:X})"

    # The world map as built, and the party on it from the campaign's start: its location (var 0x31) and the places
    # and roads its flags show (flag 0x200 + a place).
    db = sqlite3.connect(WORK / "remaster" / "build" / "enhancedworldevent" / "table.sqlite")
    entries: dict[int, dict[int, list]] = collections.defaultdict(lambda: collections.defaultdict(list))
    for location, entry, kind, values, variable, first, second in db.execute(
            'select Key, Key2, Type, Unknown8, Unknown10, Unknown14, Unknown18 from "EnhancedWorldEvent" '
            'order by Key, Key2, Key3'):
        entries[location][entry].append((kind, json.loads(values), variable, first, second))
    db.close()
    start = code(0x0FD)
    state = {ALGUS_FLAG: 1}
    state |= {u16(p, 0): u16(p, 2) for op, p in instructions(start) if op == 0xAE and u16(p, 0) >= 0x80}
    places = {f - 0x200 for f, v in state.items() if 0x200 <= f < 0x22C and v}
    roads: set[frozenset] = set()
    party = set_to(start, WORLD_LOCATION_VAR)

    def world_steps(location: int) -> None:  # the location's steps that hold now: its roads and places shown
        for rows in entries[location].values():
            held = True
            for kind, values, variable, *_ in rows:
                if kind == 1:
                    held &= state.get(variable, 0) == values[0]
                elif kind in (WORLD_AT_LEAST, WORLD_AT_MOST):
                    have = state.get(values[0], 0)
                    held &= have >= values[1] if kind == WORLD_AT_LEAST else have <= values[1]
                elif kind not in (WORLD_ROUTE, WORLD_PLACE, WORLD_CAMERA, 25):
                    held = False  # a check the walk doesn't model (a character in the party): taken as failing
            if not held:
                continue
            for kind, values, variable, first, second in rows:
                if kind == WORLD_ROUTE:
                    roads.add(frozenset((first, second)))
                    state.update({v: 1 for k, vs, v, *_ in rows if k == 1 and vs == [0] and v >= 0x22C})
                elif kind == WORLD_PLACE:
                    places.add(first)
                    state[0x200 + first] = 1

    def reachable(location: int) -> bool:
        seen, todo = {party}, [party]
        while todo:
            here = todo.pop()
            for road in roads:
                if here in road:
                    there = next(iter(road - {here}), here)
                    if there in places and there not in seen:
                        seen.add(there)
                        todo.append(there)
        return location in seen

    allowed = set(REMIXED_SCENARIOS.values()) | set(REMASTER_HAND_OFFS.values()) | DEPTHS_REMASTER_HAND_OFFS
    starts_walked = 0
    for theirs, after, by_world in ALGUS_CHAIN:
        c = code(theirs)
        hands = [(p[0] & 0xF3, (p[2] << 8) + p[1]) for op, p in instructions(c) if op == 0xCD]
        for mode, to in hands:
            if mode in (0x81, 0x82, 0x83) and to not in allowed and to not in ALGUS_DEAD_HAND_OFFS.get(theirs, ()):
                wrong.append(f"{name(theirs)} hands on to 0x{to:X} (mode 0x{mode:X}), not a scenario of the mod's")
        if theirs in ALGUS_BATTLES:
            loads = [u16(p, 0) for op, p in instructions(c) if op == 0xCE]
            preset = presets[loads[0] * REMIXED_PRESET_BYTES:(loads[0] + 1) * REMIXED_PRESET_BYTES] if loads else None
            if preset is None:
                wrong.append(f"{name(theirs)}'s battle loads no conditions")
            elif not any(event == REMIXED_SCENARIOS[theirs] for _, _, event in remixed_preset_targets(preset)[1:]):
                wrong.append(f"{name(theirs)}'s battle loads conditions (preset 0x{loads[0]:X}) that never run it "
                             "again: it couldn't end")
        if theirs in ALGUS_HIGH_MARKS and ALGUS_HIGH_MARKS[theirs][0] in c:
            wrong.append(f"{name(theirs)} still marks a unit with bits the remaster takes for away (ALGUS_HIGH_MARKS)")
        units = unit_sets.get(REMIXED_SCENARIOS[theirs], b"")
        if theirs == 0x006:
            algus = [units[u * UNIT_SIZE:(u + 1) * UNIT_SIZE] for u in range(len(units) // UNIT_SIZE)
                     if units[u * UNIT_SIZE] == 0x07]
            if (len(algus) != 1 or not algus[0][ENTD_FLAGS] & ENTD_LOAD or not algus[0][0x18] & 0x08):
                wrong.append(f"{name(theirs)} doesn't load Algus from roster slot 0 with the player's command menu")
        loaded = [units[u * UNIT_SIZE] for u in range(len(units) // UNIT_SIZE)
                  if 0 < units[u * UNIT_SIZE] < ENTD_MAN and units[u * UNIT_SIZE + ENTD_FLAGS] & ENTD_LOAD
                  and not (theirs == 0x006 and units[u * UNIT_SIZE] == 0x07
                           and units[u * UNIT_SIZE + 0x18] & 0x08)]
        if loaded:
            wrong.append(f"{name(theirs)}'s battle data loads sprite sets {[f'0x{s:02X}' for s in loaded]} from the "
                         "guest slots, which nothing in the campaign fills (ALGUS_GUESTS_FROM_DATA)")
        if set_to(c, 0x6E) is not None:
            state[0x6E] = set_to(c, 0x6E)
        if set_to(c, WORLD_LOCATION_VAR) is not None:
            party = set_to(c, WORLD_LOCATION_VAR)
        if after is None:
            if (WORLD_MAP_WORD[0], 0) not in hands:
                wrong.append(f"{name(theirs)}, the end, has no way back to the world map ({hands})")
            record = records.get(REMIXED_SCENARIOS[theirs])
            if record is None or record[RECORD_NEXT_MODE] & 0xF3 in (0x81, 0x82, 0x83):
                wrong.append(f"{name(theirs)}'s scenario record is missing or hands on elsewhere")
            continue
        ours = REMIXED_SCENARIOS[after]
        if not by_world:
            record = records.get(REMIXED_SCENARIOS[theirs])
            record_hands_on = record is not None and record[RECORD_NEXT_MODE] & 0xF3 == 0x81 \
                              and u16(record, RECORD_NEXT) == ours
            if (0x81, ours) not in hands and not record_hands_on:
                wrong.append(f"{name(theirs)} doesn't hand on to {name(after)}")
            continue
        found = [(location, dict((c_[0], c_[1]) for c_ in checks)) for location, checks, event, _ in REMIXED_WORLD_STARTS
                 if event == after and (ALGUS_FLAG, 1) in checks]
        if len(found) != 1:
            wrong.append(f"{name(after)} has {len(found)} world-map starts in the campaign")
            continue
        location, checks = found[0]
        if (WORLD_MAP_WORD[0], 0) not in hands:
            wrong.append(f"{name(theirs)} doesn't go back to the world map")
        if state.get(0x6E) != checks.get(0x6E):
            wrong.append(f"after {name(theirs)} the story is at {state.get(0x6E)}, where {name(after)} starts at "
                         f"{checks.get(0x6E)}")
        world_steps(party)
        if not reachable(location):
            wrong.append(f"{name(after)} starts at location {location}, which the party at {party} can't reach "
                         f"(places shown {sorted(places)})")
        party = location
        starts_walked += 1
    way_out, to = ALGUS_WAY_OUT
    if bytes([0xCD, 0x81, to & 0xFF, to >> 8, 0xAE, ALGUS_FLAG, 0x00, 0x00, 0x00]) not in code(way_out):
        wrong.append(f"{name(way_out)}'s way out doesn't go on to the remaster's 0x{to:X} with the campaign's flag cleared")
    if wrong:
        sys.exit("The Algus campaign as built is WRONG:\n  " + "\n  ".join(wrong))
    return (f"{len(ALGUS_CHAIN)} steps from {name(0x0FD)} through the Depths to the world map: every hand-off, "
            f"{len(ALGUS_BATTLES)} battles' conditions, {starts_walked} world-map starts each reachable by the roads "
            f"shown by then, and the way out to the remaster's 0x{to:X}; 0 wrong")


# ReMixed's unit names (EVENT/ATTACK.OUT, its list of special names, entry 1 Ramza): a unit's name id (battle data +2)
# indexes it, and the remaster's CharaName rows by the same numbers. Where the port's battle data names a unit by a row
# the remaster leaves blank, the unit, its card and its roster entry once it joins read no name (Elias at 1-A's river
# camp, a tester on 0.9.22, after Bryn, Lorne and Whitney on 0.9.17), so every such row takes ReMixed's name
# (unit_name_rows), besides CHARACTER_NAMES.
REMIXED_NAME_LIST_START = ["Ramza", "Ramza", "Ramza", "Delita", "Delita", "Delita", "Algus"]
NO_NAME_IDS = {0x00, 0xFE, 0xFF}  # a generic's, and the battle data's "none"


def battle_unit_name_ids() -> dict[int, set[int]]:
    """Name id -> the battle entries whose units the port's battle data (the mod's files, else the remaster's: ENTD 1-4,
    the added entries, ReMixed's scenarios' own units in REMIXED_SCENARIOS_FILE) name by it."""
    stock = WORK / "remaster" / "extract" / "enhanced" / "fftpack"
    built = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "fftpack"
    uses: dict[int, set[int]] = collections.defaultdict(set)
    files = [(f"battle_entd{k}_ent.bin", (k - 1) * 0x80) for k in range(1, 5)] + [(ADDED_ENTRIES_FILE, 0x200)]
    for name, first in files:
        data = (built / name if (built / name).exists() else stock / name).read_bytes()
        for u in range(len(data) // UNIT_SIZE):
            unit = data[u * UNIT_SIZE:(u + 1) * UNIT_SIZE]
            if unit[0] and unit[ENTD_NAME] not in NO_NAME_IDS:
                uses[unit[ENTD_NAME]].add(first + u // UNITS_PER_EVENT)
    data = (MOD_DIR / REMIXED_SCENARIOS_FILE).read_bytes()
    at = 2 + SCENARIO_SIZE * u16(data, 0)
    for size in (FORMATION_SIZE, 4, 6, 5):
        at += 2 + size * u16(data, at)
    block = 2 + UNITS_PER_EVENT * UNIT_SIZE
    for k in range(u16(data, at)):
        scenario = u16(data, at + 2 + k * block)
        for u in range(UNITS_PER_EVENT):
            unit = data[at + 4 + k * block + u * UNIT_SIZE:at + 4 + k * block + (u + 1) * UNIT_SIZE]
            if unit[0] and unit[ENTD_NAME] not in NO_NAME_IDS:
                uses[unit[ENTD_NAME]].add(0x10000 | scenario)
    return uses


def unit_name_rows(cfg: dict, blank: set[int]) -> dict[int, str]:
    """ReMixed's name for each of the blank CharaName rows the port's units name (battle_unit_name_ids)."""
    with Disc(remixed_bin(cfg)) as patched:
        attack = patched.read(patched.find("EVENT/ATTACK.OUT"))
    names = fft_text.read_list(attack, fft_text.find_list(attack, REMIXED_NAME_LIST_START), 0x100)
    rows, unclean = {}, []
    for key in sorted(set(battle_unit_name_ids()) & blank):
        text, clean = fft_text.decode(names[key])
        if not text:
            continue
        if not clean:
            unclean.append(f"0x{key:X} {text}")
            continue
        rows[key] = text
    if unclean:
        sys.exit("ReMixed's names for units the port names that this decoder can't read: " + ", ".join(unclean))
    return rows


def write_character_names(cfg: dict) -> str:
    """ReMixed's characters' names (CHARACTER_NAMES, and unit_name_rows: CharaName rows the remaster leaves blank),
    which their units' battle data names (REMIXED_SPRITE_SETS) and their roster entries keep once they join. Returns a
    line for the report."""
    def name(db: sqlite3.Connection) -> str:
        notes = []
        blank = {k for (k,) in db.execute('select Key from "CharaName-en" where Name is null or Name = ""')}
        named = {**unit_name_rows(cfg, blank - set(CHARACTER_NAMES)), **CHARACTER_NAMES}
        for key, text in sorted(named.items()):
            if key not in blank:
                sys.exit(f"the remaster's CharaName 0x{key:X} isn't a blank row (CHARACTER_NAMES)")
            db.execute('update "CharaName-en" set Name = ? where Key = ?', (text, key))
            notes.append(f"0x{key:X} named {text}")
        missing = sorted(k for k in battle_unit_name_ids() if k in blank and k not in named)
        return "; " + ", ".join(notes) + (f"; blank and unnamed by ReMixed: {', '.join(f'0x{k:X}' for k in missing)}"
                                          if missing else "")
    return add_nxd_rows(cfg, "charaname.en", "CharaName-en", {}, "0004.en.pac", edit=name)


def write_reused_numbers(cfg: dict) -> str:
    """The remaster's scenario numbers ReMixed's events take (REUSED_SCENARIO_NUMBERS): each one's EventId row becomes
    a free number's (EVENT_ID_FREE_FROM: no sortie prompt). Their text rows come off in write_scenario_texts
    (drop_reused_texts). Returns a line for the report."""
    db = sqlite3.connect(nxd_sqlite(cfg, "eventid"))
    columns = [row[1] for row in db.execute('pragma table_info("EventId")')]
    free = dict(zip(columns, db.execute('select * from "EventId" where Key = ?', (EVENT_ID_FREE_FROM,)).fetchone()))
    db.close()
    free.pop("Key")
    return replace_nxd_rows(cfg, "eventid", "EventId", {n: [free] for n in sorted(REUSED_SCENARIO_NUMBERS)})


def update_root_nxl(cfg: dict) -> str:
    """The game's table list (nxd/root.nxl: "name,id,rows,kind" a line) with the row counts of the NXD tables the mod
    ships, when one has more or fewer rows than the game's (rows added: Battle, ScenarioId, ScionFiles, Speaker).
    Returns a line for the report."""
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "nxd"
    target = out / "root.nxl"
    target.unlink(missing_ok=True)
    shipped = sorted(out.glob("*.nxd"))
    if not shipped:
        return "no tables"
    base = WORK / "remaster" / "extract" / "enhanced" / "nxd" / "root.nxl"
    if not base.exists():
        ff16tools(cfg, "unpack", "-i", Path(cfg["target"]["tic_dir"]) / "data" / "enhanced" / "0004.pac",
                  "-f", "nxd/root.nxl", "-o", WORK / "remaster" / "extract" / "enhanced")
    work = WORK / "remaster" / "build" / "root-nxl"
    shutil.rmtree(work, ignore_errors=True)
    (work / "in").mkdir(parents=True)
    for path in shipped:
        shutil.copy(path, work / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", work / "in", "-o", work / "tables.sqlite")
    db = sqlite3.connect(work / "tables.sqlite")
    counts = {}
    for (name,) in db.execute("select name from sqlite_master where type = 'table' and name not like '\\_%' escape '\\'"):
        counts[name.lower().removesuffix("-en")] = db.execute(f'select count(*) from "{name}"').fetchone()[0]
    db.close()
    text = base.read_bytes().decode("utf-8-sig")
    changed = []
    lines = text.split("\r\n")
    for k, line in enumerate(lines):
        parts = line.split(",")
        if len(parts) == 4 and parts[0] in counts and parts[2].isdigit() and int(parts[2]) != counts[parts[0]]:
            changed.append(f"{parts[0]} {parts[2]} -> {counts[parts[0]]}")
            parts[2] = str(counts[parts[0]])
            lines[k] = ",".join(parts)
    if not changed:
        return "root.nxl: no counts changed"
    target.write_bytes(b"\xef\xbb\xbf" + "\r\n".join(lines).encode("utf-8"))
    return f"root.nxl: {', '.join(changed)}"


def remaster_fftpack(cfg: dict, name: str, pack: str = "0002.pac") -> bytes:
    """One of the enhanced build's fftpack files, extracted once into work/."""
    extract = WORK / "remaster" / "extract" / "enhanced"
    path = extract / "fftpack" / name
    if not path.exists():
        ff16tools(cfg, "unpack", "-i", Path(cfg["target"]["tic_dir"]) / "data" / "enhanced" / pack,
                  "-f", f"fftpack/{name}", "-o", extract)
    return path.read_bytes()


# The enhanced build has a modern 1920 x 840 title logo in its UI pack. The older FFTPACK opntitle.tex is the
# Classic/PS1 title and is not shown by the enhanced title flow. Ship the marked enhanced logo itself so the runtime
# proof appears on the mode the mod supports, without replacing The Ivalice Chronicles artwork with the PS1 screen.
# The plaque under the logo says "IVALICE REMIXED" and RELEASE_VERSION, drawn by every build (tools/title_plaque.py),
# so the title always shows the release it's from.
TITLE_WIDTH, TITLE_HEIGHT = 1920, 840
TITLE_TEXTURE = "ui/ffto/title/texture/ui_logo_fft_enhanced_uitx.tex"
TITLE_LABEL = "IVALICE REMIXED {version}"


def write_title_screen(cfg: dict) -> str:
    """Draw the release's version on the enhanced UI logo's plaque and ship it in the game's .tex container."""
    work = WORK / "remaster" / "build" / "title-logo"
    shutil.rmtree(work, ignore_errors=True)
    work.mkdir(parents=True)
    source = work / title_plaque.BASE.name
    label = TITLE_LABEL.format(version=RELEASE_VERSION)
    try:
        title_plaque.draw_title(label, source)
    except (OSError, ValueError) as error:
        sys.exit(f"the title screen's plaque: {error}")
    # img-conv is not a game-specific command and rejects the `-g fft` that ff16tools() appends for pack/table work.
    subprocess.run([cfg["tools"]["ff16tools"], "img-conv", "-i", str(source)], check=True, capture_output=True)
    built = source.with_suffix(".tex")
    if not built.exists():
        sys.exit(f"FF16Tools did not make the enhanced title texture: {built}")
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / TITLE_TEXTURE
    out.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(built, out)
    return f"{label} on enhanced {TITLE_TEXTURE} ({TITLE_WIDTH} x {TITLE_HEIGHT})"


# ReMixed's unit presets (docs/MECHANICS.md, "Unit presets"; code/IvaliceRemixed/Patches/UnitPresets.cs): a battle-data
# unit's byte 37 (ENTD_PRESET), which the build clears from the remaster's battle data, picks one of ReMixed's 24
# presets: statuses (a corpse, an undead, a chest, Hirame starting Dead in 1-A's rescue, immunity to Invite on the
# missions' enemies), ability bits, elements, the class evade and stats scaled, a tint. The code mod gives each unit made
# from one of a scenario's records its preset from this file: ReMixed's tables (UNIT_PRESET_TABLES: the stat scalings,
# status sets, ability sets, element sets, tints, the 24 presets and the random pick), then each scenario's 16 presets.
UNIT_PRESETS_FILE = "remixed-unit-presets.bin"
UNIT_PRESET_TABLES = (0x801553F4, 0x80155586)
# Where ReMixed's code (0x80155184, its unit setup's part) loads the tables: lui/addiu pairs for the random pick
# (0x80155583), the presets (0x801554AB), the scalings (0x801553F4), the status sets (0x8015542C), the ability sets
# (0x80155468) and the element sets (0x80155489).
UNIT_PRESET_LOADS = {0x801551A0: "1580053c8355a524", 0x801551E0: "1580013cab542124", 0x801551FC: "1580023cf4534224",
                     0x801552C4: "1580033c2c546324", 0x8015530C: "1580023c68544224", 0x80155348: "1580043c89548424"}


def write_unit_presets(battle_new: bytes, unit_presets: dict[int, bytes]) -> str:
    """UNIT_PRESETS_FILE: u16 the tables' length, ReMixed's tables (UNIT_PRESET_TABLES), u16 count, then (u16 the mod's
    scenario, its 16 units' presets). Returns a line for the report."""
    for address, code in UNIT_PRESET_LOADS.items():
        if battle_new[address - BATTLE_BASE:address - BATTLE_BASE + 8] != bytes.fromhex(code):
            sys.exit(f"ReMixed's unit presets aren't read where this build expects (BATTLE.BIN 0x{address:X})")
    start, end = UNIT_PRESET_TABLES
    tables = battle_new[start - BATTLE_BASE:end - BATTLE_BASE]
    data = len(tables).to_bytes(2, "little") + tables + len(unit_presets).to_bytes(2, "little")
    data += b"".join(s.to_bytes(2, "little") + p for s, p in sorted(unit_presets.items()))
    (MOD_DIR / UNIT_PRESETS_FILE).write_bytes(data)
    used = sorted({p for presets in unit_presets.values() for p in presets if p})
    return (f"unit presets for {len(unit_presets)} scenario(s) ({UNIT_PRESETS_FILE}; presets used: "
            f"{', '.join(str(p) for p in used)})")


def scenario_number_owners() -> dict[int, list[str]]:
    """Each event number the mod writes a script (and, but for the tavern's events and REMIXED_EVENTS, a record) at ->
    what it's written for: ReMixed's scenarios, the battles split from their scenes, their post-battle runs, the
    tavern's events and ReMixed's events at their own numbers."""
    owners: dict[int, list[str]] = {}
    for kind, numbers in (("scenario", REMIXED_SCENARIOS), ("split battle", BATTLE_SPLITS),
                          ("post-battle run", AFTER_SPLITS), ("tavern event", TAVERN_EVENTS),
                          ("event", {n: n for n in REMIXED_EVENTS})):
        for theirs, ours in numbers.items():
            owners.setdefault(ours, []).append(f"ReMixed's 0x{theirs:03X} ({kind})")
    return owners


def check_scenario_numbers() -> None:
    """Exits when two of ReMixed's events take one number: the second written takes the first's script while the
    record stays the first's, so it runs on the other's map, battle entry and units (AFTER_SPLITS: 0x0F7's closing
    and the tavern's scene, 0.9.32-0.9.40)."""
    shared = {ours: owners for ours, owners in scenario_number_owners().items() if len(owners) > 1}
    if shared:
        sys.exit("two events at one number: " + "; ".join(f"0x{ours:X}: {', '.join(owners)}"
                                                          for ours, owners in sorted(shared.items())))


def write_remixed_scenarios(cfg: dict, held: dict[int, str], presets: bytes,
                            entry_unit_presets: dict[int, bytes]) -> tuple[str, bytes]:
    """ReMixed's scenarios (REMIXED_SCENARIOS), renumbered: their events as eventNNN.e with the code mod's mark, their
    lines as the remaster's scenario text (write_scenario_texts), their battle entries in ENTD4's free slots, and their
    scenario and formation records, the world-map locations that start them and their events' first line Ids in
    REMIXED_SCENARIOS_FILE for the code mod; the unit presets of those and of the remaster's scenarios whose battle
    entry took ReMixed's units (entry_unit_presets, from merge_entd) in UNIT_PRESETS_FILE. Returns a
    line for the report, and ReMixed's presets with the events they run renumbered."""
    with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
        files = FilePair(vanilla, patched)
        attack = files.get("EVENT/ATTACK.OUT")[1]
        test_evt = files.get("EVENT/TEST.EVT")[1]
        battle_new = files.get("BATTLE.BIN")[1]
        entd = b"".join(files.get(p)[1] for p in ENTD_FILES)
        world = files.get("WORLD/WORLD.BIN")[1]
    lengths = battle_new[EVENT_LENGTHS - BATTLE_BASE:EVENT_LENGTHS - BATTLE_BASE + 256]
    shipped = REMIXED_SCENARIOS | TAVERN_EVENTS  # what presets may run: the scenarios and the tavern's events
    idle = idle_presets(presets, shipped)
    presets, unsafe = renumber_presets(presets, shipped)
    empty = next(n for n in range(REMIXED_PRESETS - 1, 0, -1)
                 if not any(presets[n * REMIXED_PRESET_BYTES:(n + 1) * REMIXED_PRESET_BYTES]))
    # The stand-in for a preset that would run an event the mod doesn't ship (ReMixed's after-battle chain, event 8):
    # one condition set that can't fire (var 0x1FD == 0x7FFF). All zeros fired (record type 0), so after a location
    # battle's closing scene its conditions ran the event's opening again (seen on the PC with the harness).
    presets = bytearray(presets)
    # The scenes in two copies (SCRIPT_VERSION_SET): the PSP copy's set fires with flag 0xB2 at 0, the PSX one's never.
    for n in range(REMIXED_PRESETS):
        base = n * REMIXED_PRESET_BYTES
        for start, end, _ in remixed_preset_targets(presets[base:base + REMIXED_PRESET_BYTES]):
            at = base + REMIXED_PRESET_OFFSETS + 2 * start
            if end - start == 8 and tuple(u16(presets, at + 2 * k) for k in range(5)) == SCRIPT_VERSION_SET:
                presets[at + 10:at + 12] = (u16(presets, at + 10) ^ 1).to_bytes(2, "little")
    never = struct.pack("<3h", 1, 0x1FD, 0x7FFF)
    at = empty * REMIXED_PRESET_BYTES + REMIXED_PRESET_OFFSETS
    presets[at:at + len(never)] = never
    # Presets ReMixed leaves empty, for the split battles' own (BATTLE_SPLITS), from the table's end down.
    preset_room = [n for n in range(REMIXED_PRESETS - 1, 0, -1)
                   if not any(presets[n * REMIXED_PRESET_BYTES:(n + 1) * REMIXED_PRESET_BYTES])]
    # The events a preset runs once flag 0x8E is set (AFTER_BATTLE_RETURN), by the mod's numbers.
    returned_to = set()
    for n in range(REMIXED_PRESETS):
        preset = presets[n * REMIXED_PRESET_BYTES:(n + 1) * REMIXED_PRESET_BYTES]
        for start, end, event in remixed_preset_targets(preset):
            at = REMIXED_PRESET_OFFSETS + 2 * start
            words = [u16(preset, at + 2 * k) for k in range(end - start - 2)]
            if any(words[k:k + 3] == [1, AFTER_BATTLE_FLAG, 1] for k in range(len(words) - 2)):
                returned_to.add(event)  # "flag 0x8E = 1" among the set's conditions (0x107's has the victory too)
    # Those sets run AFTER_SPLITS' events' post-battle runs under their own numbers (the split battles' presets, copied
    # below, take it from their source).
    after_splits = {REMIXED_SCENARIOS[theirs]: split for theirs, split in AFTER_SPLITS.items()}
    retargeted = set()
    for n in range(REMIXED_PRESETS):
        base = n * REMIXED_PRESET_BYTES
        for start, end, event in remixed_preset_targets(presets[base:base + REMIXED_PRESET_BYTES]):
            at = base + REMIXED_PRESET_OFFSETS + 2 * start
            if event in after_splits and end - start == 5 and u16(presets, at) == 1 \
                    and u16(presets, at + 2) == AFTER_BATTLE_FLAG and u16(presets, at + 4) == 1:
                presets[at + 8:at + 10] = after_splits[event].to_bytes(2, "little")
                retargeted.add(event)
    if retargeted != set(after_splits):
        sys.exit(f"no 'flag 0x8E = 1' set runs {sorted(set(after_splits) - retargeted)} (AFTER_SPLITS)")
    returned_to |= set(after_splits.values())
    exe = (Path(cfg["target"]["tic_dir"]) / "FFT_enhanced.exe").read_bytes()
    entry_bytes = UNITS_PER_EVENT * UNIT_SIZE
    entd4_path = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "fftpack" / "battle_entd4_ent.bin"
    with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
        entd4 = bytearray(entd4_path.read_bytes() if entd4_path.exists()
                          else FilePair(vanilla, patched).get(ENTD_FILES[3])[0])
    named = set()
    remaster_presets: dict[int, bytes] = {}  # the remaster's scenario -> its entry's units' ReMixed presets
    table = 0x8ECAB0 - 0x1C00  # the remaster's scenario records in the exe file
    for k in range(0x377):
        record = exe[table + SCENARIO_SIZE * k:table + SCENARIO_SIZE * (k + 1)]
        if not u16(record, 0):
            break
        named |= {u16(record, 7), u16(record, 0xF)}
        for entry in (u16(record, 7), u16(record, 0xF)):
            if entry in entry_unit_presets:
                remaster_presets[u16(record, 0)] = entry_unit_presets[entry]
    free = [e for e in FREE_ENTRIES if e not in named]
    # Two slots kept for scenarios whose records come from the file (OWN_ENTRY_SLOT): the ordinary carrier, and a
    # distinct carrier for a battle entered directly from a scene.  A changed entry number makes the remaster discard
    # the scene's hidden/removed battle-unit state before EntryRecords supplies the battle's fresh records.
    carrier = free.pop()
    fresh_carrier = free.pop()
    own_entries: dict[int, bytes] = {}  # the mod's scenario -> its 16 units
    fresh_scenarios: set[int] = set()  # battle scenarios whose ENTD carrier differs from the scene's
    fresh_entries = 0
    carrier_written = False
    # Formation numbers for ReMixed's formations that clash with the remaster's own: ones both leave empty (all zero
    # but the id), taken from the table's end (RemixedScenarios.FormationCount) down.
    def formation_empty(data: bytes, base: int, f: int) -> bool:
        rec = data[base + FORMATION_SIZE * f:base + FORMATION_SIZE * (f + 1)]
        return len(rec) == FORMATION_SIZE and not any(rec[:FORMATION_ID])
    formation_room = [f for f in range(0x100, REMASTER_FORMATION_COUNT)
                      if formation_empty(exe, REMASTER_FORMATIONS_FILE, f)
                      and (u16(attack, FORMATION_TABLE + FORMATION_SIZE * f + FORMATION_ID) != f
                           or formation_empty(attack, FORMATION_TABLE, f))]
    moved_formations: dict[int, int] = {}
    shutil.rmtree(MOD_DIR / "FFTIVC" / "data" / "enhanced" / "fftpack" / "text", ignore_errors=True)  # earlier builds' .mes
    texts_written: list[tuple[int, int, list]] = []
    message_bases: list[tuple[int, int, int]] = []
    slot_events: list[dict] = []  # the tavern's text made while its events run (SLOTS_FILE)
    entry_slots: dict[int, int] = {}  # ReMixed's battle entry -> the slot it goes in
    slot_from_data: dict[int, bool] = {}  # an entry's slot: whether its campaign guests are made from their data
    guests_from_data = 0  # the Algus campaign's guests made from their data (ALGUS_GUESTS_FROM_DATA)
    next_line_id = PZD_FIRST_ID
    menus_shown = menus_all = 0  # ReMixed's menus (lists of answers), and those a MESSAGE names as it stands
    sync_sets = 0  # waits for ReMixed's text code made sets (TEXT_SYNC)
    boxes_reopened = 0  # swaps to a longer line made boxes of their own (CLOSE_BOX)
    pages_shown = 0  # long lines' later pages shown after their first (PAGE_CHARS)
    notices_closed = 0  # notice boxes closed after their wait (notice_boxes)
    speech_boxes_switched = 0  # inactive remaster speech boxes closed before the other speaker's line
    objects_dropped = 0  # 3D-object instructions on maps the build makes looks for (MAP_OBJECT_USE)
    graphics_rerouted = 0  # LoadEVTCHR instructions moved to ReMixed-only copies of redrawn graphics
    twin_named = 0  # PSP-script lines named from their PSX twins (script_twins)
    remounted = 0  # battles whose run seats ReMixed's riders on their mounts (seat_riders)
    self_patches: dict[int, int] = {}  # event -> PATCH_NEXTs into a SET of their own variable left out
    messages_audited = dynamic_messages = message_closes = midscene_joins = postbattle_erases = 0
    write_backs: dict[int, int] = {}  # event -> ordinary exit hand-offs using display state 7 (AFTER_BATTLE_HOME)
    live_write_backs: dict[int, int] = {}  # event -> same-map returns using the synchronous native roster writer
    script = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "script" / "enhanced"
    script.mkdir(parents=True, exist_ok=True)

    records = {}
    for k in range(0x1F4):
        at = SCENARIO_TABLE + SCENARIO_SIZE * k
        if u16(attack, at):
            records[u16(attack, at)] = attack[at:at + SCENARIO_SIZE]
    chain_slots = round_slots(records, free)  # the missions in rounds: their entries' slots, in runs (ROUND_SLOTS)
    chained = dict(chain_slots)
    # The Nemesis's return runs where ReMixed's Preparation Menu would have (NEMESIS_SCENE).
    nemesis_sets = (0, 0, [])
    if set(NEMESIS_SCENE) <= set(REMIXED_SCENARIOS):
        turned_presets, *nemesis_sets = nemesis_triggers(presets, records,
                                                         lambda e: remixed_event_code(test_evt, battle_new, e))
        presets = bytearray(turned_presets)
    scenarios, formations, done = [], [], []
    used_entries = []
    # The split battles' own presets (BATTLE_SPLITS), and for the scene's preset its battle's copy, which a return after
    # the battle (AFTER_BATTLE_RETURN, 0x119 in 0x0FB's) loads in its place: the scene's runs the scene's opening.
    split_presets = {theirs: preset_room.pop(0) for theirs in BATTLE_SPLITS}
    return_presets = {u16(records[theirs], 0x16): split_presets[theirs] for theirs in BATTLE_SPLITS if theirs != 0xF8}
    # Each of ReMixed's scenarios, then the battles split from their scenes (BATTLE_SPLITS), which share their scene's
    # text: (ReMixed's, the mod's, the scene's for a split battle).
    check_scenario_numbers()
    jobs = [(theirs, ours, None) for theirs, ours in REMIXED_SCENARIOS.items()]
    jobs += [(theirs, battle, REMIXED_SCENARIOS[theirs]) for theirs, battle in BATTLE_SPLITS.items()]
    jobs += [(theirs, split, None) for theirs, split in AFTER_SPLITS.items()]  # their post-battle runs: as themselves
    jobs += [(theirs, ours, None) for theirs, ours in TAVERN_EVENTS.items()]  # events alone: no record of their own
    shared_text: dict[int, tuple] = {}  # a split's scene -> its text: (lines, menus, syncs, pages)
    unit_presets: dict[int, bytes] = {}  # the mod's scenario -> its 16 units' presets (UNIT_PRESETS_FILE)
    menu_scenarios = {REMIXED_SCENARIOS[s] for s in MENU_BATTLES.values()}
    menu_battles: dict[int, tuple[int, int]] = {}  # the mod's scenario -> its battle's formations (MENU_BATTLES)
    dropped_groups: list[int] = []  # ReMixed's battles whose second group the mod would leave out
    entry_units: dict[int, bytes] = {}  # ReMixed's battle entry -> its 16 units as the port builds them (MAP_RELOAD)
    named_reloads: list[tuple[int, int, int]] = []  # (ReMixed's event, the mod's scenario, the entry its reload names)
    job_slots: dict[int, int] = {}  # the mod's scenario -> the entry number its record names
    for theirs, ours, scene in jobs:
        record = bytearray(SCENARIO_SIZE if theirs in TAVERN_EVENTS else records[theirs])
        record[0:2] = ours.to_bytes(2, "little")
        # Its own hand-off (+0x12 the scenario, +0x14 the mode; the world setup copies it into the map-settings table),
        # as its code's are (location_scenario_code): 0x07C goes to the Depths' 0x0A2, and 0x0A2 to 0x0A3.
        if record[RECORD_NEXT_MODE] & 0xF3 in (0x81, 0x82, 0x83):
            after_it = u16(record, RECORD_NEXT)
            if after_it in WORLD_MAP_ENDS:
                record[RECORD_NEXT:RECORD_NEXT_MODE + 1] = bytes([0, 0, WORLD_MAP_WORD[0]])
            elif after_it in REMIXED_SCENARIOS:
                record[RECORD_NEXT:RECORD_NEXT + 2] = REMIXED_SCENARIOS[after_it].to_bytes(2, "little")
            else:
                sys.exit(f"ReMixed's scenario 0x{theirs:X} hands on to 0x{after_it:X}, which the mod doesn't ship")
        raw_code = remixed_event_code(test_evt, battle_new, theirs)
        # A split battle's copy too, though its opening jumps to its end: the code is there, so its units are.
        named_reloads += [(theirs, ours, named) for named in reload_entries(raw_code, lengths)]
        own_preset = None
        if theirs == 0xF8 and scene is None:
            # Keep existing battle saves' preset 0xF7 for the battle, and put the new scene on the copy.
            source = u16(record, 0x16)
            own_preset = split_presets[theirs]
            presets[own_preset * REMIXED_PRESET_BYTES:(own_preset + 1) * REMIXED_PRESET_BYTES] = (
                presets[source * REMIXED_PRESET_BYTES:(source + 1) * REMIXED_PRESET_BYTES])
            record[0x16:0x18] = own_preset.to_bytes(2, "little")
        if scene is not None:  # the battle's map and entry, as the menu sets them up
            setup = opening_sets(raw_code, lengths, (MENU_ENTRY_VAR, MENU_MAP_VAR))
            record[2] = setup.get(MENU_MAP_VAR, record[2])
            record[7:9] = setup.get(MENU_ENTRY_VAR, u16(record, 7)).to_bytes(2, "little")
            if not any(record[5:7]):
                record[5:7] = battle_music(raw_code) or bytes(2)
            # Its conditions: the scene's preset again, with the events it runs as the scene's run as the battle's. A
            # battle's opening starts through them ("var 0x1FD = 1 -> the event"), and with the scene's preset the
            # battle's start ran the scene again (seen on the PC), as did the preset its opening loads (0xCE).
            source = u16(record, 0x16)
            own_preset = source if theirs == 0xF8 else split_presets[theirs]
            copy = presets[source * REMIXED_PRESET_BYTES:(source + 1) * REMIXED_PRESET_BYTES]
            for start, end, event in remixed_preset_targets(copy):
                if event == scene:
                    at = REMIXED_PRESET_OFFSETS + 2 * end - 2
                    copy[at:at + 2] = ours.to_bytes(2, "little")
            presets[own_preset * REMIXED_PRESET_BYTES:(own_preset + 1) * REMIXED_PRESET_BYTES] = copy
            record[0x16:0x18] = own_preset.to_bytes(2, "little")
            unsafe |= {own_preset} if source in unsafe else set()
            idle |= {own_preset} if source in idle else set()
        record[2] = REMIXED_MAP_NUMBERS.get(record[2], record[2])
        record[3] = REMIXED_MAP_STATES.get(theirs, record[3])
        # Its battle entry, into the added entries, with abilities the mod holds back left to chance.
        entry = u16(record, 7)
        saves_guest = False  # a unit the write-back saves for a later scene (ENTD_JOINS and ENTD_SAVES)
        battle_ids: set[int] = set()  # its units' battle ids (seat_riders)
        if entry:
            units = bytearray(entd[entry * entry_bytes:(entry + 1) * entry_bytes])
            battle_ids = {units[u * UNIT_SIZE + ENTD_BATTLE_ID] for u in range(UNITS_PER_EVENT) if units[u * UNIT_SIZE]}
            saves_guest = any(units[u * UNIT_SIZE] and units[u * UNIT_SIZE + ENTD_FLAGS] & (ENTD_JOINS | ENTD_SAVES)
                              == ENTD_JOINS | ENTD_SAVES for u in range(UNITS_PER_EVENT))
            # ReMixed's unit presets go in a file of their own (UNIT_PRESETS_FILE), for the code mod.
            found = bytes(units[u * UNIT_SIZE + ENTD_PRESET] for u in range(UNITS_PER_EVENT))
            if any(found):
                unit_presets[ours] = found
            cleared = False  # a guest of the Algus campaign's made from its data (ALGUS_GUESTS_FROM_DATA)
            for u in range(UNITS_PER_EVENT):
                # ReMixed's unit preset (byte 37), for its code; 0 in every vanilla and remaster entry, and with it the
                # remaster crashed after loading the units' sprites (seen on the Deck).
                units[u * UNIT_SIZE + ENTD_PRESET] = 0
                # ReMixed's gendered job slots, as the job tables port them (DARK_KNIGHT, BARD): its man's Bard (job
                # 0x38) is the remaster's Bard, its Dark Knight (0x5B, a woman's 0x37) the remaster's; their skillsets
                # likewise. Left as they are, job 0x38 is a Lucavi in the remaster (seen on the Deck: Hashmal).
                at = u * UNIT_SIZE
                if units[at] in REMIXED_SPRITE_SETS:
                    sprites, name, job, gender = REMIXED_SPRITE_SETS[units[at]]
                    if units[at + ENTD_NAME] in (0, units[at]) or units[at] in REMIXED_OWN_NAME_SPRITES:
                        units[at + ENTD_NAME] = name
                    if units[at + ENTD_JOB] in (0, units[at]):
                        units[at + ENTD_JOB] = job
                    if not units[at + ENTD_FLAGS] & ENTD_GENDER:
                        units[at + ENTD_FLAGS] |= gender
                    units[at] = sprites
                units[at + ENTD_JOB] = REMIXED_UNIT_JOBS.get(units[at + ENTD_JOB], units[at + ENTD_JOB])
                units[at + ENTD_SECONDARY] = REMIXED_UNIT_SKILLSETS.get(units[at + ENTD_SECONDARY], units[at + ENTD_SECONDARY])
                units[at + ENTD_PRIMARY] = remixed_primary(units[at + ENTD_PRIMARY], units[at + ENTD_JOB], units[at + ENTD_FLAGS])
                units[at + ENTD_LEVELS_JOB] = remixed_levels_job(units[at + ENTD_LEVELS_JOB], units[at + ENTD_FLAGS])
                for i in UNIT_RSM_FIELDS:
                    if u16(units, u * UNIT_SIZE + i) in held:
                        units[u * UNIT_SIZE + i:u * UNIT_SIZE + i + 2] = ABILITY_RANDOM.to_bytes(2, "little")
                # ReMixed keeps the actor it has already made from roster slot 0. The remaster rebuilds every entry
                # record instead, so explicitly ask its roster-load path for this unit as well as carrying control.
                if theirs == 0x006 and units[at] == 0x07:
                    units[at + ENTD_FLAGS] |= ENTD_LOAD
                    units[at + 0x18] |= 0x08  # 0x84 guest -> 0x8C player
                # The campaign normally has no saved guests, so its special characters are made from their entry
                # data. Its first battle is different: the opening has swapped Algus into roster slot 0, and ReMixed
                # loads that record so the player keeps his menu, EXP and JP. OldGuests bridges the remaster's
                # guest-only ENTD_LOAD lookup to slot 0 for this one player-team record.
                algus_leader = theirs == 0x006 and units[at] == 0x07 and units[at + 0x18] & 0x08
                if (theirs in ALGUS_CAMPAIGN and units[at] and units[at] < ENTD_MAN
                        and units[at + ENTD_FLAGS] & ENTD_LOAD and not algus_leader):
                    units[at + ENTD_FLAGS] &= ~ENTD_LOAD & 0xFF  # made from its data (ALGUS_GUESTS_FROM_DATA)
                    guests_from_data += 1
                    cleared = True
            entry_units.setdefault(entry, bytes(units))  # as ReMixed has the entry, for a map reload (MAP_RELOAD)
            # A split battle copies the source scene's entry. Remove actors used only to stage that scene when the
            # formation supplies the real party member; retaining them produces a second, data-built guest copy.
            if scene is not None:
                scene_only = BATTLE_SPLIT_SCENE_ONLY.get(theirs, set())
                for u in range(UNITS_PER_EVENT):
                    at = u * UNIT_SIZE
                    if units[at + ENTD_BATTLE_ID] in scene_only:
                        units[at:at + UNIT_SIZE] = bytes(UNIT_SIZE)
                if theirs == 0xF8:
                    # The two volcano battles share ENTD 0x1DE. Its defaults put the first fight's monsters
                    # on the field. The PS1 Preparation Menu rebuilds the final battle with flags C6-CB
                    # (slots 6-11). Encode that selection in the battle's entry so deployment and reload agree.
                    included = opening_sets(raw_code, lengths, tuple(UNIT_FLAGS))
                    for flag in UNIT_FLAGS:
                        presence = (flag - UNIT_FLAGS.start) * UNIT_SIZE + 0x18
                        units[presence] &= 0x3F
                        if included.get(flag):
                            units[presence] |= 0x80
            # A slot holds the units of the first scenario that names its entry for all that share it: the guests
            # made from their data mustn't reach a scenario outside the campaign that way, nor the campaign miss them.
            if entry in entry_slots and slot_from_data.get(entry, False) != cleared:
                sys.exit(f"ReMixed's 0x{theirs:X} shares its battle entry's slot with the Algus campaign "
                         "(ALGUS_GUESTS_FROM_DATA)")
            # Scenarios that share a ReMixed entry (a side story's scenes on one cast) share its slot. Past the free
            # slots, a scenario names the kept one and its units go in the file (OWN_ENTRY_SLOT).
            fresh_entry = scene is not None or theirs in FRESH_ENTRY_BATTLES
            if fresh_entry:
                # The summoning scene comes directly from another ENTD 0x1DE battle. All three loads
                # need distinct adjacent carrier numbers to invalidate the remaster's cached actors.
                slot = carrier if theirs == 0xF8 and scene is None else fresh_carrier
                if fresh_entries == 0:  # anything which reads the physical carrier still sees a valid entry
                    at = (slot - 0x180) * entry_bytes
                    entd4[at:at + entry_bytes] = units
                own_entries[ours] = bytes(units)
                fresh_scenarios.add(ours)
                fresh_entries += 1
            elif entry not in entry_slots and (entry in chain_slots or free):
                slot_from_data[entry] = cleared
                entry_slots[entry] = chain_slots.pop(entry) if entry in chain_slots else free.pop(0)
                at = (entry_slots[entry] - 0x180) * entry_bytes
                entd4[at:at + entry_bytes] = units
                used_entries.append(entry_slots[entry])
            if not fresh_entry and entry in entry_slots:
                slot = entry_slots[entry]
            elif not fresh_entry:
                slot = carrier
                if not carrier_written:  # anything which reads the physical carrier still sees a valid entry
                    at = (carrier - 0x180) * entry_bytes
                    entd4[at:at + entry_bytes] = units
                    carrier_written = True
                own_entries[ours] = bytes(units)
            record[7:9] = slot.to_bytes(2, "little")
            record[0xF:0x11] = slot.to_bytes(2, "little")  # what the setup takes when started from the world map
            job_slots[ours] = slot
        # Its formations: the record's, or (a battle ReMixed sets up for its Preparation Menu) the ones its code sets;
        # none for a scene whose battle is a scenario of its own.
        if not u16(record, 9) and not (theirs in BATTLE_SPLITS and scene is None):
            for at, formation in zip((9, 11), opening_formations(raw_code, lengths)):
                record[at:at + 2] = formation.to_bytes(2, "little")
        second_group = 0  # a battle after its scene: its second group, set under an IF (MENU_SECOND_GROUP)
        if ours in menu_scenarios and scene is None and u16(record, 9) and not u16(record, 11):
            second_group = conditional_second_group(raw_code, lengths)
            record[11:13] = second_group.to_bytes(2, "little")
        elif (u16(record, 9) and not u16(record, 11) and MENU_BATTLES.get(theirs, theirs) == theirs
              and set(second_group_conditions(raw_code, lengths)) <= CONDITIONS_IN_PORT):
            # Its formation first (CONDITIONS_IN_PORT): its second group as ReMixed's code sets it in the port.
            record[11:13] = conditional_second_group(raw_code, lengths).to_bytes(2, "little")
        # A second group ReMixed's code sets that no run of the battle puts in (not 0x113's: its battle is 0x0F1's).
        if (u16(record, 9) and not u16(record, 11) and MENU_BATTLES.get(theirs, theirs) == theirs
                and conditional_second_group(raw_code, lengths)):
            dropped_groups.append(theirs)
        # Where the remaster's records are empty, at their own numbers; where the remaster uses one, at a number it
        # leaves empty (from the table's end down).
        for at in (9, 11):
            formation = u16(record, at)
            if not formation:
                continue
            start = FORMATION_TABLE + FORMATION_SIZE * formation
            theirs_f = bytearray(attack[start:start + FORMATION_SIZE])
            start = REMASTER_FORMATIONS_FILE + FORMATION_SIZE * formation
            mine = exe[start:start + FORMATION_SIZE]
            if u16(theirs_f, FORMATION_ID) != formation or u16(mine, FORMATION_ID) != formation:
                sys.exit(f"formation 0x{formation:X} isn't where the build expects it")
            if any(mine[:FORMATION_ID]) and mine != theirs_f:
                if formation not in moved_formations:
                    if not formation_room:
                        sys.exit("no formation number left for ReMixed's")
                    moved_formations[formation] = formation_room.pop()
                formation = moved_formations[formation]
                theirs_f[FORMATION_ID:FORMATION_ID + 2] = formation.to_bytes(2, "little")
                record[at:at + 2] = formation.to_bytes(2, "little")
            formations.append(bytes(theirs_f))
        # A battle played after a scene (MENU_BATTLES): its formations are for its second run, which the code mod sets
        # up; the record (the scene's run) has none.
        if ours in menu_scenarios and scene is None:
            menu_battles[ours] = (u16(record, 9), u16(record, 11) | (MENU_SECOND_GROUP if second_group else 0))
            record[9:13] = bytes(4)
        if ours in AFTER_SPLITS.values():
            # A post-battle copy is loaded by the live battle's conditions; it is not another battle setup. In
            # particular 0x0F7's code contains the formations for its earlier monster battle, which must not make the
            # private closing event (0x1C5) advertise a deployable formation of its own.
            record[9:13] = bytes(4)
        if theirs not in TAVERN_EVENTS:
            scenarios.append(bytes(record))
        # Its text (from the event's block) as lines of their own, and its event: ReMixed's code with the mark, in the
        # remaster's layout naming those lines.
        block = test_evt[theirs * 0x2000:(theirs + 1) * 0x2000]
        at = int.from_bytes(block[:4], "little")
        lines, first, menus, syncs, pages = [], None, set(), set(), {}
        if scene is not None:  # the scene's lines, again in a text of its own
            lines, menus, syncs, pages = shared_text[scene]
            at = -1
        if 4 <= at < 0x2000:
            messages = fixed_messages(remixed_messages(block[at:]), MESSAGE_FIXES.get(theirs, []))
            lines = [remixed_line(m, markup=theirs in SLOTTED) for m in messages]
            if theirs in SCRIPT_COUNTERPARTS:
                counterpart = SCRIPT_COUNTERPARTS[theirs]
                psx = test_evt[counterpart * 0x2000:(counterpart + 1) * 0x2000]
                source = fixed_messages(remixed_messages(psx[int.from_bytes(psx[:4], "little"):]),
                                        MESSAGE_FIXES.get(counterpart, []))
                lines, made = parallel_speakers(lines, [remixed_line(m) for m in source])
                twin_named += made
            if theirs in SIDE_CHAPTER_A:
                # Nameless PSP lines borrow their PSX twins' speaker. Use the remaster's name in either script.
                lines = [(speaker.replace("Balmafula", "Valmafra"), text) for speaker, text in lines]
            for was, now in LINE_CHANGES.get(theirs, {}).items():
                if not any(was in text for _, text in lines):
                    raise SystemExit(f"event 0x{theirs:X} has no line with {was!r} (LINE_CHANGES)")
                lines = [(speaker, text.replace(was, now)) for speaker, text in lines]
            for n, text in SIZING_LINES.get(theirs, {}).items():
                lines[n - 1] = ("", text)
            menus = {n for n, m in enumerate(messages, 1) if MENU_START in m}
            syncs = text_syncs(block[at:])
            while lines and not lines[-1][1]:
                lines.pop()
            # The tavern's info boxes (KIND_BOX) show a posting, a report or a description whole, five rows at most, as
            # the PS1 does: paged, the rest went to a page no MESSAGE shows (theirs pick their line as they run), and
            # the fifth row was lost (seen on the PC in the regression pass, 2026-11-12: Mission 17's last sentence).
            whole = set(range(1, len(lines) + 1)) if theirs in KIND_EVENTS or theirs == TRAINING_EVENT else menus
            if theirs in TIMED_NOTICES:
                whole = whole | NOTICE_MESSAGES[theirs]  # the notice dismisses itself after its allotted time
            lines, pages = paged_lines(lines, whole, len(messages))
            if theirs not in SLOTTED:
                lines, made = twin_speakers(lines, script_twins(raw_code, lengths), pages)
                twin_named += made
        templated: dict[int, str] = {}
        twins: dict[int, int] = {}
        laid_out: list[int] = []  # templated lines no one speaks: their line breaks kept as they fill in
        if theirs in SLOTTED and lines:  # its lines with slots, and its messages as slots show them (SLOTS_FILE)
            for n, m in enumerate(messages, 1):
                speaker, text = remixed_line(m, slots=True, markup=True)
                if n > len(lines) or n in pages or (speaker, text) == remixed_line(m, markup=True):
                    continue
                if "{" in speaker:  # a name line made of slots (a unit's card): shown at the line's start
                    lines[n - 1] = ("", lines[n - 1][1])
                    text = f"{speaker} {text}"
                    speaker = ""
                templated[n] = text
                if not speaker:
                    laid_out.append(n)
            # Each with a twin, a line of its own after the event's: the game looks a line's text up once while its
            # box shows it, and a slot's word may change meanwhile (a unit's card as the picker moves), so the code
            # mod shows the two by turns, and each turn is looked up afresh.
            lines += [("", "")] * (len(messages) - len(lines))
            for n in templated:
                lines.append(lines[n - 1])
                twins[n] = len(lines)
        if theirs in NEMESIS_SCENE and lines:  # its speaker named as a line shows (NEMESIS_SPEAKER)
            for n, m in enumerate(messages, 1):
                if n > len(lines) or remixed_line(m, slots=True)[0] != SLOT_MESSAGE:
                    continue
                for k in [n, *pages.get(n, [])]:
                    lines[k - 1] = (NEMESIS_SPEAKER, lines[k - 1][1])
                    templated[k] = f'<speaker="{SLOT_MESSAGE}">{lines[k - 1][1]}'
            if not templated:
                sys.exit(f"ReMixed's event 0x{theirs:X} has no line its slot names the speaker of (NEMESIS_SPEAKER)")
        if lines:
            first = next_line_id
            next_line_id += len(lines)
            if next_line_id - 1 > PZD_LAST_ID:
                sys.exit("no line ids left for ReMixed's text")
            texts_written.append(([ours], first, lines))
            if theirs in BATTLE_SPLITS and scene is None:
                shared_text[ours] = (lines, menus, syncs, pages)
            message_bases.append((ours, first - PZD_MESSAGE_OFFSET - 1, len(lines)))  # message n: line base + n
            if theirs in SLOTTED:
                slot_events.append({"event": ours, "first": first,
                                    "lines": {str(n): t for n, t in templated.items()},
                                    "twins": {str(n): t for n, t in twins.items()},
                                    "layout": laid_out,
                                    "plain": frameless_messages(raw_code, lengths),
                                    "parts": {str(n): slot_part(m) for n, m in enumerate(messages, 1)}})
            elif templated:  # only its speakers' names (NEMESIS_SPEAKER)
                slot_events.append({"event": ours, "first": first, "lines": {str(n): t for n, t in templated.items()},
                                    "twins": {}, "layout": [], "plain": [], "parts": {}})
        patches = [SCRIPT_VERSION, *EVENT_CODE_PATCHES.get(theirs, [])]
        if theirs == 0xF7:
            # Its final victory runs on the second battle's entry. Its closing map reload brings the
            # first entry's scene actors back, as on the PS1 where both battles used 0x1DE.
            reload_map = bytes([MAP_RELOAD, 119, 0])
            patches.append((reload_map, bytes([SET_VALUE, MENU_ENTRY_VAR, 0]) + record[7:9] + reload_map))
        info_box = None  # the tavern's info box as patched (KIND_BOX)
        if (theirs in KIND_EVENTS or theirs == TRAINING_EVENT) and lines:  # its info box by its widest (KIND_BOX)
            patches.append(kind_box_patch(lines, training=theirs == TRAINING_EVENT))
            info_box = patches[-1][1]
        if theirs in records and u16(records[theirs], 7):  # a mission's loot set: ReMixed's entry (LOOT_SET)
            patches.append((LOOT_SET, bytes([0xAE, 0x15, 0x00]) + records[theirs][7:9]))
        patches += mission_unlock_reads(raw_code, lengths)
        if theirs in BATTLE_SPLITS:  # the menu's hand-off: on to the battle's scenario, or in it to the event's end
            end_label = 0xFA if theirs == 0xF8 else 0xFF
            ending = bytes([0x0E, end_label]) + (bytes([0xBE, 0x53, 0]) if theirs == 0xF8 else b"") + bytes([0xDB])
            if ending not in raw_code:
                sys.exit(f"ReMixed's event 0x{theirs:X} has no expected ending at LABEL {end_label:02x} (BATTLE_SPLITS)")
            battle = BATTLE_SPLITS[theirs]
            seats = seat_riders(battle_ids) if scene is not None else b""
            remounted += bool(seats)
            battle_start = seats
            if scene is not None:
                if theirs == 0xEA:
                    battle_start += MANDALIA_BATTLE_ACTIVATE
                else:
                    # The scene fades out before handing off, and ReMixed's menu clears that fade as it opens. Every
                    # battle with a scenario of its own does so as it starts (MENU_FADE_IN): it was given only to
                    # Yardrow's and Mount Bervenia's, and in 1-A's last (0x0FB) the turns drew, as the battle's own
                    # screen does, but each scene inside it played over black (a tester, 2026-11-22: a black screen
                    # as a child fell; seen in the hidden PC game with the scene's boxes over it).
                    battle_start += MENU_FADE_IN
            battle_start += bytes([0x0D, end_label])
            patches.append((bytes([0xCD, 0x81, PREPARATION_MENU & 0xFF, PREPARATION_MENU >> 8]),
                            battle_start if scene is not None else bytes([0xCD, 0x81, battle & 0xFF, battle >> 8])))
            if own_preset is not None:  # the conditions its opening loads: its own (see above)
                source = u16(records[theirs], 0x16)
                patches.append((bytes([0xCE, source & 0xFF, source >> 8]),
                                bytes([0xCE, own_preset & 0xFF, own_preset >> 8])))
        ps1, _ = location_scenario_code(raw_code, lengths, unsafe, empty, shipped,
                                        patches=patches, idle=idle - unsafe,
                                        returned_to=ours in returned_to, return_presets=return_presets,
                                        defer_return_writeback=theirs in DEFER_RETURN_WRITE_BACK)
        if theirs == 0x11A and any(ps1.count(new) != 1 or ps1.count(old) for old, new in SIDE_CHAPTER_A_GUEST):
            sys.exit("ReMixed's 0x11A doesn't move 1-A's Agrias as the build expects (SIDE_CHAPTER_A_GUEST)")
        made = ps1.count(AFTER_BATTLE_HOME) - raw_code.count(AFTER_BATTLE_HOME)
        live_made = (ps1.count(AFTER_BATTLE_RETURN_HOME) - raw_code.count(AFTER_BATTLE_RETURN_HOME)
                     + ps1.count(AFTER_BATTLE_DEFERRED_HOME) - raw_code.count(AFTER_BATTLE_DEFERRED_HOME))
        if made:
            write_backs[ours] = made
        if live_made:
            live_write_backs[ours] = live_made
        if not made and not live_made and saves_guest and scene is None and ours not in AFTER_SPLITS.values():
            sys.exit(f"ReMixed's 0x{theirs:X} saves a guest at its end, but hands on to no write-back (AFTER_BATTLE_HOME)")
        ps1 = drop_code(ps1, lengths, EVENT_CODE_DROPS.get(theirs))
        if theirs in MENU_BATTLES and scene is None:  # the scene's run hands on to the battle's (MENU_NOTE)
            back = REMIXED_SCENARIOS[MENU_BATTLES[theirs]]
            ps1 = insert_code(ps1, lengths, [(MENU_NOTE, bytes([0xCD, 0x81, back & 0xFF, back >> 8]))])
            if ps1.count(MENU_CHECK + GOTO_END) != 1:
                sys.exit(f"ReMixed's event 0x{theirs:X} hasn't its check where the build expects it (MENU_CHECK)")
            seats = seat_riders(battle_ids)
            ps1 = ps1.replace(MENU_CHECK + GOTO_END, MENU_CHECK + seats + MENU_FADE_IN + GOTO_END)
            remounted += bool(seats)
        ps1 = move_block(ps1, lengths, EVENT_CODE_MOVES.get(theirs))
        ps1, made = self_patched_sets(ps1, lengths)
        if made:
            self_patches[ours] = made
        patch_next_targets(ps1, lengths, theirs)
        ps1 = question_box(ps1, lengths, QUESTION_BOXES.get(theirs))
        ps1, made = notice_boxes(ps1, lengths, NOTICE_MESSAGES.get(theirs))
        notices_closed += made
        ps1 = timed_notice(ps1, TIMED_NOTICES.get(theirs))
        ps1 = insert_code(ps1, lengths, EVENT_CODE_INSERTS.get(theirs))
        if theirs in MIDSCENE_JOIN_SCREENS:
            ps1 = insert_midscene_join_return(ps1, lengths, MIDSCENE_JOIN_SCREENS[theirs])
        ps1, made = erase_postbattle_cleanup(ps1, POSTBATTLE_CLEANUP.get(theirs))
        postbattle_erases += made
        made = midscene_join_screens(ps1, lengths)
        expected_joins = MIDSCENE_JOIN_SCREENS.get(theirs, 0)
        if made != expected_joins:
            sys.exit(f"ReMixed's 0x{theirs:X} shows {made} join screen(s) in a dark screen, expected "
                     f"{expected_joins} (MIDSCENE_JOIN_SCREENS)")
        if theirs in MIDSCENE_JOIN_SCREENS:
            midscene_joins += made
        ps1, made = route_event_graphics(ps1, lengths)
        graphics_rerouted += made
        if info_box is not None:  # closed after each press it waits for (KIND_BOX_CLOSE)
            ps1 = close_kind_boxes(ps1, info_box, 3 if theirs == TRAINING_EVENT else 2)
        ps1, made = text_sync_waits(ps1, lengths, syncs)
        sync_sets += made
        # Repair 0xFFFF line placeholders before fitted_boxes: that pass can turn a ChangeDialog into a reopened
        # MESSAGE, so doing it afterwards would miss the original instruction and leave a converted 0xFFFFFFFF.
        if theirs in DYNAMIC_LINE_PLACEHOLDERS:
            templates = DYNAMIC_LINE_PLACEHOLDERS[theirs]
            ps1, made = dynamic_message_placeholders(ps1, lengths, templates)
            expected = len(templates)
            if made != expected:
                sys.exit(f"ReMixed's event 0x{theirs:X} has {made} dynamic line placeholder(s), expected {expected}")
        if theirs in DYNAMIC_MENU_PLACEHOLDERS:
            templates = DYNAMIC_MENU_PLACEHOLDERS[theirs]
            ps1, made = dynamic_message_placeholders(ps1, lengths, templates, menus)
            expected = len(templates)
            if made != expected:
                sys.exit(f"ReMixed's event 0x{theirs:X} has {made} dynamic menu placeholder(s), expected {expected}")
        ps1, made, paged, switched = fitted_boxes(
            ps1, lengths, {n: len(text) for n, (_, text) in enumerate(lines, 1)}, pages,
            menus if theirs not in SLOTTED and theirs not in DYNAMIC_MENU_PLACEHOLDERS else frozenset(),
            close_at_end=not (theirs in BATTLE_SPLITS and scene is not None)
        )  # the tavern's menus are its own (TallMenus)
        boxes_reopened += made
        pages_shown += paged
        speech_boxes_switched += switched
        ps1 = fix_scene_portraits(ps1, lengths, theirs)
        if record[2] in set(NEW_LOOKS) | set(REMIXED_MAPS) | set(REMIXED_MAP_EDITS):
            ps1, dropped = without_map_objects(ps1, lengths)
            objects_dropped += dropped
        ps1 = remaster_extended_statuses(ps1, lengths, theirs)
        code, named, template = remaster_message_layout(ps1, lengths, first, menus, tavern=theirs in TAVERN,
                                                        boxes=EVENT_BOXES.get(theirs),
                                                        menu_box=theirs in DYNAMIC_MENU_PLACEHOLDERS)
        notice_pages = {page for notice in shown_notices(ps1, lengths, NOTICE_MESSAGES.get(theirs, set()))
                        for page in pages.get(notice, ())}
        if missing_pages := notice_pages - named:
            sys.exit(f"ReMixed's event 0x{theirs:X} omits notice pages {sorted(missing_pages)}")
        try:
            audited, selected, closes = audit_event_messages(code, lengths, first, lines, templated)
        except ValueError as e:
            sys.exit(f"ReMixed's event 0x{theirs:X} as 0x{ours:X} has broken dialogue: {e}")
        messages_audited += audited
        dynamic_messages += selected
        message_closes += closes
        menus_shown += len(menus & named)
        menus_all += len(menus)
        # The tavern's names (missions, treasures, kinds: short lines with no speaker) only go into other lines' slots
        # (text code 0xEB, filled in as a line shows): no box of their own to size, and with them 0x0E8 was too long.
        names = {n for n, (speaker, text) in enumerate(lines, 1)
                 if theirs in SLOTTED and not speaker and "<cursor>" not in text and len(text) <= TAVERN_NAME_CHARS
                 and n not in templated and n not in twins.values()}  # lines with slots and their twins show
        code = shadow_messages(ps1, lengths, template, first, len(lines), named, names) + code
        if len(REMIXED_EVENT_MARK) + len(code) > 0x2000:
            sys.exit(f"ReMixed's event 0x{theirs:X} is too long for the code mod's range")
        (script / f"event{ours:03d}.e").write_bytes(REMIXED_EVENT_MARK + code)
        kind = "'s battle" if scene is not None else "'s post-battle run" if ours in AFTER_SPLITS.values() else ""
        done.append(f"0x{theirs:X}{kind} as 0x{ours:X}")

    if chain_slots or any(entry_slots.get(e) != s for e, s in chained.items()):
        sys.exit("the missions in rounds didn't get their slots (ROUND_SLOTS): "
                 + ", ".join(f"0x{e:X}" for e, s in chained.items() if entry_slots.get(e) != s))
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "fftpack"
    out.mkdir(parents=True, exist_ok=True)
    entd4_path.write_bytes(entd4)
    (out / "event_test_evt.en.bin").unlink(missing_ok=True)  # shipped by an earlier build; the game doesn't read it
    places = [(k, REMIXED_SCENARIOS[u16(world, MISSION_BATTLES - WORLD_BASE + 2 * k)]) for k in range(24)
              if u16(world, MISSION_BATTLES - WORLD_BASE + 2 * k) in REMIXED_SCENARIOS]
    done.append(write_scenario_texts(cfg, texts_written))
    done.append(f"{menus_all} menu(s), {menus_shown} named by a MESSAGE as it stands (the rest through 0xE6)")
    done.append(f"{sync_sets} wait(s) for ReMixed's text code made sets")
    done.append(f"{boxes_reopened} swap(s) to a longer line made boxes of their own, {pages_shown} later page(s) of long lines shown in boxes of their own, {notices_closed} notice box(es) closed after their wait")
    done.append(f"{speech_boxes_switched} speaker switch(es) close the inactive speech box before the next line")
    done.append(f"{objects_dropped} 3D-object instruction(s) on the build's own map looks made waits")
    done.append(f"{graphics_rerouted} LoadEVTCHR instruction(s) routed to ReMixed-only redraws")
    done.append(f"{twin_named} PSP-script line(s) named from their PSX twins")
    done.append(f"{remounted} battle(s) seating ReMixed's riders on their mounts as they begin (seat_riders)")
    done.append(f"{sum(self_patches.values())} PATCH_NEXT(s) into a SET of their own variable left out "
                f"({', '.join(f'0x{e:X}' for e in sorted(self_patches)) or 'none'}; self_patched_sets)")
    done.append(f"{midscene_joins} mid-scene recruit(s) keep ReMixed's join screen (display state 7)")
    done.append(f"{postbattle_erases} post-battle actor removal(s) erase corpse/crystal drawables first")
    done.append(f"{messages_audited} real MESSAGE/ChangeDialog instruction(s) audited against their text rows "
                f"({dynamic_messages} runtime-selected, {message_closes} intentional closes)")
    done.append(f"{len(write_backs)} event(s) write their battle's units back as event 8 does, at "
                f"{sum(write_backs.values())} hand-off(s) (AFTER_BATTLE_HOME)")
    expected_live = {event: 1 for event in LIVE_RETURN_EVENTS}
    if live_write_backs != expected_live:
        missing = sorted(set(expected_live) - set(live_write_backs))
        extra = sorted(set(live_write_backs) - set(expected_live))
        wrong = sorted((event, count) for event, count in live_write_backs.items()
                       if event in expected_live and count != 1)
        sys.exit(f"same-map battle returns are not the exact audited set: missing {missing}, extra {extra}, counts {wrong}")
    done.append(f"{len(live_write_backs)} same-map post-battle return(s) use the synchronous roster write-back "
                "without display-state-7 teardown (AFTER_BATTLE_RETURN_HOME)")
    # A second group ReMixed's code sets only under an IF goes with a battle played after its scene (MENU_SECOND_GROUP);
    # one set so in a battle with its formation first would be left out without a word (0x0F4's and 0x0F8's, var 0x6C
    # 0: on the PS1 they deploy a second squad of two). The places the remaster takes are checked from the built file
    # (check_story_regressions.check_places).
    if dropped_groups:
        sys.exit("second formation group(s) ReMixed's code sets that the mod leaves out: "
                 + ", ".join(f"0x{e:X}" for e in sorted(set(dropped_groups))))
    data = (len(scenarios).to_bytes(2, "little") + b"".join(scenarios)
            + len(formations).to_bytes(2, "little") + b"".join(formations)
            + len(places).to_bytes(2, "little") + b"".join(k.to_bytes(2, "little") + s.to_bytes(2, "little")
                                                           for k, s in places)
            + len(message_bases).to_bytes(2, "little") + b"".join(e.to_bytes(2, "little") + b.to_bytes(2, "little")
                                                                  + n.to_bytes(2, "little") for e, b, n in message_bases))
    # The hand-offs that offer side chapter 1-A: (the remaster's scenario, the one it hands on to, the mode).
    offers = [(CHAPTER_1_END, REMIXED_SCENARIOS[0xEC], 0x81)] if 0xEC in REMIXED_SCENARIOS else []
    data += len(offers).to_bytes(2, "little") + b"".join(
        s.to_bytes(2, "little") + t.to_bytes(2, "little") + bytes([m]) for s, t, m in offers)
    # The units of the scenarios past the free slots: (the mod's scenario, its 16 units), OWN_ENTRY_SLOT.
    data += len(own_entries).to_bytes(2, "little") + b"".join(
        s.to_bytes(2, "little") + units for s, units in sorted(own_entries.items()))
    # Hand-offs by Ramza's name: (the remaster's scenario, the mod's it hands on to, the mode, the names), u8-counted
    # UTF-8 names: the Algus campaign's start (ALGUS_TRIGGER).
    named = [(ALGUS_TRIGGER, REMIXED_SCENARIOS[0xFD], 0x81, ALGUS_NAMES)] if 0xFD in REMIXED_SCENARIOS else []
    data += len(named).to_bytes(2, "little")
    for s, t, m, names in named:
        data += s.to_bytes(2, "little") + t.to_bytes(2, "little") + bytes([m, len(names)])
        data += b"".join(bytes([len(n.encode())]) + n.encode() for n in names)
    # ReMixed's missions posted in the taverns (MISSIONS): the posting's rumor, the mod's scenario, the story progress
    # from which it's posted (0xFFFF: only by the flag) and that flag (0: none).
    for i, (theirs, _, _) in MISSIONS.items():
        if u16(world, MISSION_BATTLES - WORLD_BASE + 2 * i) != theirs:
            raise SystemExit(f"ReMixed's mission {i} isn't scenario 0x{theirs:X}")
    data += len(MISSIONS).to_bytes(2, "little") + b"".join(
        b"".join(v.to_bytes(2, "little") for v in (MISSION_RUMORS + k, REMIXED_SCENARIOS[theirs],
                                                   0xFFFF if progress is None else progress, flag))
        for k, (i, (theirs, progress, flag)) in enumerate(MISSIONS.items()))
    # The battles played after a scene (MENU_BATTLES): (the mod's scenario, its two formations, u8 count, the events
    # whose scene hands on to it), for the code mod to set their second run up as ReMixed's Preparation Menu does.
    data += len(menu_battles).to_bytes(2, "little")
    for s, (first_formation, second_formation) in sorted(menu_battles.items()):
        events = sorted(REMIXED_SCENARIOS[e] for e, back in MENU_BATTLES.items() if REMIXED_SCENARIOS[back] == s)
        data += b"".join(v.to_bytes(2, "little") for v in (s, first_formation, second_formation)) + bytes([len(events)])
        data += b"".join(e.to_bytes(2, "little") for e in events)
    # Ordinary rumors the code mod gates itself: rumor, minimum story progress, required-set flag, and two
    # required-clear flags. Unlike Postings, these cannot be accepted with Start.
    data += len(CONDITIONAL_RUMORS).to_bytes(2, "little") + b"".join(
        b"".join(v.to_bytes(2, "little") for v in (rumor, *conditions))
        for rumor, conditions in sorted(CONDITIONAL_RUMORS.items()))
    # The units a map reload brings in where ReMixed's code names the entry (MAP_RELOAD): (the mod's scenario, the
    # entry asked, its 16 units), for the code mod to hand to that scenario's reload of that entry.
    reloads = []
    for theirs, ours, named in named_reloads:
        if named not in entry_units:
            sys.exit(f"ReMixed's event 0x{theirs:X} reloads its map with entry 0x{named:X}, which no shipped scenario "
                     "has (MAP_RELOAD)")
        battle = BATTLE_SPLITS.get(theirs)
        clashes = {carrier, fresh_carrier, job_slots.get(ours), job_slots.get(battle) if battle else None}
        if named in clashes:
            sys.exit(f"ReMixed's event 0x{theirs:X} reloads its map with entry 0x{named:X}, the number of an entry the "
                     "scenario or its battle loads itself, so the reload would keep that load's units (MAP_RELOAD)")
        reloads.append((ours, named, entry_units[named]))
    data += len(reloads).to_bytes(2, "little") + b"".join(
        s.to_bytes(2, "little") + e.to_bytes(2, "little") + units for s, e, units in reloads)
    done.append(f"{len(reloads)} map reload(s) with the entry ReMixed's code names ("
                + ", ".join(f"0x{s:X}'s of 0x{e:X}" for s, e, _ in reloads) + ")")
    if len(menu_battles) != len(set(MENU_BATTLES.values())):
        sys.exit("a scenario of MENU_BATTLES has no battle")
    done.append(f"{len(menu_battles)} battle(s) set up again after their scene, as ReMixed's Preparation Menu does")
    done.append(f"{len(fresh_scenarios)} scene-to-battle scenario(s) use the fresh ENTD carrier")
    done.append(f"the Nemesis's return run by {nemesis_sets[0]} preset(s)' Preparation Menu set(s) and added to "
                f"{nemesis_sets[1]} more (NEMESIS_SCENE)" + (f", no room in {', '.join(f'0x{e:X}' for e in nemesis_sets[2])}'s"
                                                             if nemesis_sets[2] else ""))
    (MOD_DIR / REMIXED_SCENARIOS_FILE).write_bytes(data)
    # The remaster's own scenarios whose battle entry took ReMixed's units' builds (merge_entd) take their presets too,
    # but not at a number the mod's scenarios now hold (REUSED_SCENARIO_NUMBERS): Marach on the Rooftop of Riovanes is
    # ReMixed's corpse (preset 1, as its Malak there; job row 0x12 is Luso's, LUSO_SLOT), Gafgarion at Golgorand and
    # Lionel's gate its preset 23 (immunities).
    ours_now = {u16(record, 0) for record in scenarios} | set(unit_presets)
    remaster_presets = {s: p for s, p in remaster_presets.items() if s not in ours_now}
    unit_presets.update(remaster_presets)
    done.append(write_unit_presets(battle_new, unit_presets))
    if remaster_presets:
        done.append(f"{len(remaster_presets)} of the remaster's scenarios with ReMixed's unit presets for the units its "
                    f"battle data took from ReMixed ({', '.join(f'0x{s:X}' for s in sorted(remaster_presets))})")
    (MOD_DIR / SLOTS_FILE).write_text(json.dumps({"events": slot_events}, ensure_ascii=False), encoding="utf-8")
    done.append(f"{sum(len(e['lines']) for e in slot_events)} line(s) with slots the code mod fills in ({SLOTS_FILE})")
    entries = len(used_entries)
    done.append(f"{guests_from_data} of the Algus campaign's guests made from their battle data (ALGUS_GUESTS_FROM_DATA)")
    if own_entries:
        done.append(f"{len(own_entries)} scenario(s) with their units in the file, on slots 0x{carrier:X} and "
                    f"0x{fresh_carrier:X} ({fresh_entries} fresh after a scene): "
                    f"({', '.join(f'0x{s:X}' for s in sorted(own_entries))})")
    # The game reads a row per scenario in Battle (and per battle entry in EntryNo, which the vanilla slots have).
    done.append(add_nxd_rows(cfg, "battle", "Battle", {k: {"DLCFlags": 0} for k in
                                                       [*REMIXED_SCENARIOS.values(), *BATTLE_SPLITS.values(),
                                                        *AFTER_SPLITS.values(), *TAVERN_EVENTS.values()]}))

    return (f"{', '.join(done)} (missions {', '.join(str(k) for k, _ in places)}, {len(MISSIONS)} posted in the "
            f"bars; {len(CONDITIONAL_RUMORS)} conditional rumor(s)); {len(formations)} formation record(s), {entries} battle "
            f"entr{'y' if entries == 1 else 'ies'} ({', '.join(f'0x{e:X}' for e in used_entries)}); presets 0x{empty:X} empty, "
            f"{len(unsafe)} loading none"), presets


# Maps ReMixed's events use that the enhanced game has no look for (tools/maps.py: on one, a scene's units stand on
# the sky, seen on the PC in 0x0ED): vanilla's 97, 99 and 101, which no vanilla scenario uses, made into looks of their
# own from the game's classic files of them. 0x0ED is on 99, 0x0F2 on 97, 0x0F9 on 101.
# The remaster's Yardrow Armory (26) is a cutscene map without a tile panel layer. Use its classic mesh for the
# enhanced terrain and panels so the tile highlights and units share the same coordinates.
YARDROW_MAP = 26
NEW_LOOKS = (YARDROW_MAP, 97, 99, 101)
# ReMixed's own maps, by the number its battle data gives them (BATTLE.BIN's GNS table at 0x8EC74 points 119 at its
# MAP/VOLCANO/VOLCANO.GNS, where vanilla has a map 119 of its own, and the remaster has vanilla's): (folder, its
# texture file, its mesh file), their default state. VOLCANO is 0x0F8's battle and 0x117's scene. Its texture and mesh
# go in place of the remaster's map 119 files (map_map119_6.bin, map_map119_7.bin; vanilla's GNS already names those
# two for the default state), the mesh without its render properties (its last part), which the game reads no more
# of than the file it replaces (24,436 bytes; ReMixed's is 27,865): the look carries those bits.
REMIXED_MAPS = {119: ("VOLCANO", "VOLCANO.8", "VOLCANO.9", "map_map119_6.bin", "map_map119_7.bin"),
                98: ("TUNNEL", "TUNNEL.7", "TUNNEL.8", "map_map098_6.bin", "map_map098_7.bin")}
# ReMixed's edits to vanilla maps: map -> (its texture and mesh on ReMixed's disc, for the look; the numbered files it
# changed, which the remaster names map_mapNNN_<n>.bin as the disc's MAPNNN.<n>). MAP075 is 0x0F7's battle (no vanilla
# scenario uses 75; the remaster's HD look of it is a volcano with lava bubbles): ReMixed reshaped it (120 tiles'
# heights and water, its polygons, lights and palettes, in the default mesh and its nine time and weather variants,
# each the size of vanilla's). Those go in place of the remaster's, and a look built from ReMixed's replaces the HD
# one: its texture and mesh rows, and its animations and polygon conditions (RefinedBgAnim, RefinedBgMeshPolygon)
# dropped, as they name the HD mesh's parts and polygons. The units' lighting (RefinedBgSystemTexture and its lut
# pictures) stays the remaster's volcano light.
REMIXED_MAP_EDITS = {75: ("MAP/MAP075.8", "MAP/MAP075.9", (9, 12, 15, 18, 21, 26, 31, 36, 41, 46))}
CLASSIC_RENDER_POINTER = 0xB0
# The units' lighting on them (RefinedBgSystemTexture's rows and the lut_chara pictures they name): a town by day's.
LOOK_LIGHTS_FROM = 44
NODE_ROW = {"DLCFlags": 0, "Comment": None, "Unknown8": 0, "UnknownC": 0, "Unknown10": 0, "Unknown14": 1,
            "Unknown15": 0, "Unknown16": 0, "Unknown17": 1, "Unknown18": 1}
# A highlight panel part's row (the Mod Studio's PANEL_ROW: 75 of the game's 77): drawn in every view, the first of
# the see-through parts.
PANEL_NODE_ROW = dict(NODE_ROW, UnknownC=-100, Unknown16=1)


def without_render_properties(mesh: bytes) -> bytes:
    """A classic mesh file without its render properties, its last part: the pointer cleared, the file cut there."""
    at = int.from_bytes(mesh[CLASSIC_RENDER_POINTER:CLASSIC_RENDER_POINTER + 4], "little")
    others = [int.from_bytes(mesh[p:p + 4], "little") for p in range(0x40, 0xC4, 4) if p != CLASSIC_RENDER_POINTER]
    if not at or any(o > at for o in others):
        sys.exit("a classic mesh's render properties aren't its last part")
    out = bytearray(mesh[:at])
    out[CLASSIC_RENDER_POINTER:CLASSIC_RENDER_POINTER + 4] = bytes(4)
    return bytes(out)


# A classic mesh's moving parts (their animation instructions at the pointer 0x8C, the animated meshes at 0x90-0xAC):
# the 3D-object instructions that would play them become waits on the build's looks (MAP_OBJECT_USE).
MESH_ANIMATION_POINTERS = range(0x8C, 0xB0, 4)


def fitted_mesh(mesh: bytes, room: int) -> bytes:
    """A classic mesh cut to fit the file it replaces (the game reads no more than that): whole if it fits, else without
    its render properties, else without its moving parts too (both its last parts)."""
    if len(mesh) <= room:
        return mesh
    out = bytearray(without_render_properties(mesh))
    if len(out) <= room:
        return bytes(out)
    pointer = lambda p: int.from_bytes(out[p:p + 4], "little")  # noqa: E731
    moving = [p for p in MESH_ANIMATION_POINTERS if pointer(p)]
    cut = min(pointer(p) for p in moving) if moving else len(out)
    if any(pointer(p) > cut for p in range(0x40, 0xC4, 4) if p not in moving):
        sys.exit("a classic mesh's moving parts aren't its last parts")
    for p in moving:
        out[p:p + 4] = bytes(4)
    if cut > room:
        sys.exit(f"a classic mesh ({len(mesh):,} bytes) doesn't fit its file ({room:,}) even without its moving parts")
    return bytes(out[:cut])


# ReMixed's own event graphics. EVENT/EVTCHR.BIN holds the graphics events load (LoadEVTCHR, 0x58): 0x7800 bytes each,
# graphic N at N x 0x7800 (palettes first, the pixels from +0x980). The remaster loads them the same way from
# fftpack/event_evtchr_bin.bin (0x233EA0), a copy of vanilla's with six graphics more, taking the pixels of five (0x23,
# 0x3D, 0x60, 0x70, 0x87) from fftpack/spr/evtchr/evtchr_NNN.tex instead. ReMixed redrew twelve graphics, and 0x6F is
# all its own (where vanilla keeps an unused copy of 0x7A): the Treasure Wheel's chests and the arrows its Preparation
# Menu stands over hidden items. The mod ships 0x6F and 0x10 in place because no stock event loads them
# (audit_stock_event_graphics). Nine other redraws used by the port get private slots: eight unused slots already in
# the file and one appended one. ReMixed's unused 0x7A redraw remains held back until one of its events that loads it is
# shipped. 0x10 took native slot 0x1E from 2026-11-16 until 2026-11-18: the scan that picked it read the remaster's own
# instruction 0xC5 (8 parameter bytes, which the PS1's table doesn't have) as no longer than its opcode, stepped over
# event 149's LoadEVTCHR(1, 0x1E), and that scene (Agrias and Ovelia with Lionel in sight, chapter 2) drew Agrias's
# shading pose and Ovelia's reach for the tree in ReMixed's 0x10 art (seen on the PC, 2026-11-18).
EVTCHR_FILE, EVTCHR_BYTES, EVTCHR_PIXELS = "EVENT/EVTCHR.BIN", 0x7800, 0x980
EVENT_GRAPHICS_IN_PLACE = (0x6F, 0x10)
EVENT_GRAPHIC_ALIASES = {
    0x12: 0x0A,
    0x14: 0x78,
    0x19: 0x89,
    0x1B: 0x8A,
    0x1D: 0x8B,
    0x2E: 0x8C,
    0x59: 0x8D,
    0x67: 0x8E,
    0x79: 0xF2,
}
REMIXED_EVENT_GRAPHICS = (*EVENT_GRAPHICS_IN_PLACE, *EVENT_GRAPHIC_ALIASES)
# An event graphic's frame has a second one that stands for its other side: as the camera turns, the game draws the
# frame paired with the one held (0 with 1, 12 with 13), as it turns a unit's sprite between its front and back.
# ReMixed's events keep the camera still while such a graphic is out, and where a pose's pair isn't the same picture
# ReMixed draws it so (Dycedarg's two kneeling frames, 12 and 13 of 0x59). The Stone isn't: frame 0 of 0x59, beside
# vanilla's figure in frame 1, so as the remaster's camera turned in Dycedarg's change (0x10E) the Stone became a man
# (a tester on 0.9.39, 2026-11-26: "the stone EVTCHR sprite might need to get swapped out"; ReMixed's author: "the
# camera angle change is fucking up the EVTCHR... I didn't think to do the same for the Stone"). The port's copy draws
# the Stone from both sides: graphic -> the frames whose pair is pointed at them (the frame table, EVENT_FRAMES).
EVENT_FRAMES = 0x500  # a graphic's frame table: a u32 offset (from 0x5A0) per frame
EVENT_GRAPHIC_ONE_SIDED = {0x59: (0,)}
# With the enhanced sprites on, the remaster draws an event graphic from a texture of its own instead of its pixels: the
# entry of system/ffto/g2d.dat that op 0x58 names for it (the table at 0x714490: 1242 for 0x6F), the graphic's own art
# redrawn at twice the size, sampled where the graphic's frames say. 1242 is vanilla's 0x6F (a copy of 0x7A, figures
# along the top), with nothing where ReMixed's chests are, so the Treasure Wheel's chests drew nothing but their
# shadows (seen on the PC, 2026-11-12). Such a texture is the graphic's pixels (4 bits each, 256 wide, from +0x980) at
# twice the size, 512 x 442, in the same palettes; the code mod's loader (fftivc.utility.modloader) gives the game an
# entry of its own from system/ffto/g2d/tex_<entry>.bin, as the game has it once decoded. So ReMixed's private copies
# ship their pixels doubled under the otherwise-unused alias slot's texture. Values are alias -> (source graphic,
# g2d entry). Every event texture (0x47A-0x505) belongs to a graphic, so the two with none of their own borrow PSP
# stubs: the remaster's table gives 0x10 and appended alias 0xF2 -1, and RemixedEventGraphics.cs points them at 0x468
# and 0x46A, the unused stub textures of PSP graphics 0xA8 and 0xA9. Nothing can draw those graphics (their sheets
# aren't in the remaster), and ReMixedSprites deliberately uses only the earlier PSP stubs.
EVENT_TEXTURES = {
    0x10: (0x10, 0x468),
    0x0A: (0x12, 0x489),
    0x78: (0x14, 0x4DD),
    0x89: (0x19, 0x502),
    0x8A: (0x1B, 0x504),
    0x8B: (0x1D, 0x501),
    0x8C: (0x2E, 0x500),
    0x8D: (0x59, 0x503),
    0x8E: (0x67, 0x4FF),
    0x6F: (0x6F, 0x4DA),
    0xF2: (0x79, 0x46A),
}
# Their base entries are PSP-unit stubs, not event textures: base + the event renderer's 0x8C stride would land on
# unrelated event pictures 0x4F4 and 0x4F6 and make their actors black silhouettes. The code patch gives them the
# neutral all-zero palette texture paired with the ordinary event texture 0x489 instead.
STUB_EVENT_TEXTURE_GRAPHICS = (0x10, 0xF2)
EVENT_NEUTRAL_PALETTE_TEXTURE = 0x515
# The textures the event graphics can be given: the remaster's event range and PSP graphics 0xA8 and 0xA9's stubs. Any
# of them the build doesn't write this time is taken out of the mod, so a texture an earlier build gave a redraw
# doesn't stay over the remaster's own art (0x49B, 0x1E's, did after 0x10 left that slot).
EVENT_TEXTURE_RANGE = (*range(0x47A, 0x506), *range(0x468, 0x46C))


# ReMixed's event graphics whose textures stay plain doubled pixels: 0x6F, the Treasure Wheel's chests and Reveal
# Treasures' arrows, seen so on the PC beside the PS1 (2026-11-12) and not looked at smoothed. Every other one is
# people's poses, drawn on a field beside the remaster's own sprites, and is smoothed with Scale2x as ReMixed's unit
# sprites are (REMIXED_SPRITES): until 2026-11-25 only 0x2E was (0.9.27), and the rest stood out as blocky (a tester,
# 2026-11-21: the dream's knight carrying the child is 0x1B's).
EVENT_TEXTURES_PLAIN = {0x6F}


def event_texture(graphic: bytes, source: int) -> bytes:
    """An event graphic's pixels at twice the size, as the enhanced sprites' texture of it (4 bits a pixel, the left one
    in the low bits, 256 bytes a row): smoothed with Scale2x, but for EVENT_TEXTURES_PLAIN."""
    pixels, out = graphic[EVTCHR_PIXELS:], bytearray()
    if source not in EVENT_TEXTURES_PLAIN:
        return sprites.pack4(sprites.scale2x(sprites.unpack4(pixels), 256, len(pixels) // 128))
    for y in range(len(pixels) // 128):
        row = bytearray()
        for b in pixels[y * 128:(y + 1) * 128]:
            row += bytes([(b & 0xF) * 0x11, (b >> 4) * 0x11])
        out += row * 2
    return bytes(out)


# Cell-size codes reached by the custom event graphics the mod currently ships. Unknown sizes fail the build until
# their exact bounds are established, rather than letting a new pose sample an unchecked texture rectangle.
EVENT_CELL_SIZES = {0x06: (32, 24), 0x0B: (32, 40)}


def event_animation_frame(graphic: bytes, animation: int) -> tuple[int, list[tuple[int, int, int, int, int]]]:
    """Resolve one EVTCHR held animation to its frame and (screen x/y, texture x/y, size-code) parts."""
    if len(graphic) != EVTCHR_BYTES or not 0 <= animation < 64:
        raise ValueError(f"event graphic animation {animation} is outside its 64-entry table")
    animation_at = 0x100 + struct.unpack_from("<I", graphic, 4 * animation)[0]
    if not 0x100 <= animation_at <= 0x4FC:
        raise ValueError(f"event graphic animation {animation} points outside its data (0x{animation_at:X})")
    command, end = struct.unpack_from("<HH", graphic, animation_at)
    if not 0x2D2 <= command < 0x2D2 + 40 or end != 0xFFFF:
        raise ValueError(f"event graphic animation {animation} isn't one held frame: 0x{command:04X}, 0x{end:04X}")
    frame = command - 0x2D2
    frame_at = 0x5A0 + struct.unpack_from("<I", graphic, 0x500 + 4 * frame)[0]
    if not 0x5A0 <= frame_at < 0x780 or graphic[frame_at + 1] != 0:
        raise ValueError(f"event graphic frame {frame} has an invalid header at 0x{frame_at:X}")
    count = graphic[frame_at] + 1
    if frame_at + 2 + 4 * count > 0x780:
        raise ValueError(f"event graphic frame {frame}'s {count} parts run into its palettes")
    parts = []
    for part in range(count):
        x, y, cell = struct.unpack_from("<bbH", graphic, frame_at + 2 + 4 * part)
        size = cell >> 10 & 0x1F
        if size not in EVENT_CELL_SIZES:
            raise ValueError(f"event graphic frame {frame} uses unknown cell size 0x{size:X}")
        parts.append((x, y, (cell & 0x1F) * 8, (cell >> 5 & 0x1F) * 8, size))
    return frame, parts


def audit_event_graphic_animations(graphics: bytes, textures: Path) -> tuple[int, int]:
    """Resolve every custom EVTCHR pose reached by a shipped upgraded event and validate its pixels and geometry."""
    scripts = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "script" / "enhanced"
    uses: list[tuple[Path, int, int, int]] = []
    for path in sorted(scripts.glob("event*.e")):
        code = path.read_bytes()
        if not code.startswith(REMIXED_EVENT_MARK):
            continue
        slots: dict[int, int] = {}
        at = len(REMIXED_EVENT_MARK)
        while at < len(code):
            op = code[at]
            size = 1 + eiu_events.REMASTER_LENGTHS[op]
            if at + size > len(code):
                raise ValueError(f"{path.name}: instruction 0x{op:02X} at 0x{at:X} is truncated")
            params = code[at + 1:at + size]
            if op == 0x58:
                slots[params[0]] = params[1]
            elif op in (0x5A, 0x5B):
                slots.pop(params[0], None)
            elif op in (0x11, 0x8C):
                animation = u16(params, 2 if op == 0x11 else 3)
                if 500 <= animation < 700:
                    slot = 0 if animation < 600 else 1
                    alias = slots.get(slot)
                    if alias in EVENT_TEXTURES:
                        uses.append((path, at, alias, animation - (500 if slot == 0 else 600)))
            at += size
            if op == 0xDB:
                break
    missing = set(EVENT_TEXTURES) - {alias for _, _, alias, _ in uses}
    if missing:
        raise ValueError("routed event graphics have no pose reached by a shipped scene: "
                         + ", ".join(f"0x{alias:X}" for alias in sorted(missing)))
    unique: set[tuple[int, int]] = set()
    for path, at, alias, animation in uses:
        source, entry = EVENT_TEXTURES[alias]
        graphic = graphics[alias * EVTCHR_BYTES:(alias + 1) * EVTCHR_BYTES]
        frame, parts = event_animation_frame(graphic, animation)
        pixels = graphic[EVTCHR_PIXELS:]
        for _, _, x, y, size in parts:
            width, height = EVENT_CELL_SIZES[size]
            if x + width > 256 or y + height > len(pixels) * 2 // 256:
                raise ValueError(f"{path.name}:0x{at:X}: graphic 0x{alias:X} animation {animation}, frame {frame} "
                                 f"samples {width}x{height} at ({x},{y}) outside its pixels")
        texture = textures / f"tex_{entry}.bin"
        if not texture.exists() or texture.read_bytes() != event_texture(graphic, source):
            raise ValueError(f"{path.name}:0x{at:X}: graphic 0x{alias:X}'s {texture.name} doesn't match its pixels")
        unique.add((alias, animation))
    return len(uses), len(unique)


# The remaster's event instructions are as long as its own length helper (0x2CBE2C) says: its byte table (.rdata
# 0x680F00, found from the helper's code, which loads it right after these bytes) plus one; 0xE6, its next byte plus
# two; and with the enhanced build on, 0x10, 0x51 and 0xEB longer by 3, 2 and 4 (32-bit text ids). The remaster adds
# instructions of its own, so the PS1's table misreads its scripts: it gives 0xC5 (8 parameter bytes) none, which hid
# event 149's LoadEVTCHR(1, 0x1E) from the build until 2026-11-18. Read this way, all 560 stock scripts end at an END.
REMASTER_LENGTH_HELPER = bytes.fromhex("8039E675090FB6410183C002EB100FB601488D15")
REMASTER_ENHANCED_LENGTHS = {0x10: 3, 0x51: 2, 0xEB: 4}


def remaster_length_table(cfg: dict) -> bytes:
    """The remaster's event-instruction length table, from its exe."""
    from exe import Exe  # noqa: PLC0415
    image = Exe(Path(cfg["target"]["tic_dir"]) / "FFT_enhanced.exe")
    at = image.data.find(REMASTER_LENGTH_HELPER)
    if at < 0 or image.data.find(REMASTER_LENGTH_HELPER, at + 1) >= 0:
        sys.exit("the remaster's event length helper isn't found once in its exe")
    disp = at + len(REMASTER_LENGTH_HELPER)
    rva = next(va + disp - raw for _, va, _, raw, size in image.sections if raw <= disp < raw + size)
    return image.read(rva + 4 + struct.unpack_from("<i", image.data, disp)[0], 256)


def remaster_instruction_size(table: bytes, code: bytes, at: int) -> int:
    """An enhanced event instruction's length, opcode included, as the remaster's length helper works it out."""
    op = code[at]
    if op == 0xE6:
        return code[at + 1] + 2 if at + 1 < len(code) else 1
    return table[op] + 1 + REMASTER_ENHANCED_LENGTHS.get(op, 0)


def check_story_stages(cfg: dict) -> str:
    """The remaster's count of the story (the stage its events set with STAGE_OPCODE as they end) against the events
    the mod ships: a stock event the mod ships under the upgrade's mark is one of ReMixed's with the remaster's number,
    and sets no stage, so each stage lost that way has to be one the story's progress brings back (STORY_STAGE_FLOORS)
    or one looked at and left (STORY_STAGES_LEFT); a stock event shipped without the mark has to set what it did. And
    every story whose roads the mod opens itself is in OWN_STORY_STEPS, which the code mod greys no road in. Exits with
    what's wrong. Returns a line for the report."""
    table = remaster_length_table(cfg)
    ours = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "script" / "enhanced"

    def stages(code: bytes, name: str) -> list[tuple[int, int]]:
        found, at = [], 0
        while at < len(code) and code[at] != 0xDB:
            size = remaster_instruction_size(table, code, at)
            if code[at] == STAGE_OPCODE:
                if size != 9:
                    sys.exit(f"{name}: the remaster's stage instruction isn't 8 bytes of parameters (check_story_stages)")
                found.append((int.from_bytes(code[at + 1:at + 5], "little"), int.from_bytes(code[at + 5:at + 9], "little")))
            at += size
        return found

    lost, kept, wrong = set(), 0, []
    skipped = {number for chain in REMASTER_SCENES_SKIPPED.values() for number in chain}
    if set(REMASTER_SCENES_SKIPPED) - set(REMASTER_STARTS_REPLACED):
        wrong.append("REMASTER_SCENES_SKIPPED names a start that REMASTER_STARTS_REPLACED doesn't replace")
    for path in stock_event_paths(cfg):
        theirs = stages(path.read_bytes(), path.name)
        mine = ours / path.name
        if theirs and int(path.stem[5:]) in skipped:
            lost |= {(int(path.stem[5:]), quest, stage) for quest, stage in theirs}
            continue
        if not theirs or not mine.exists():
            continue
        code = mine.read_bytes()
        if code.startswith(REMIXED_EVENT_MARK):
            lost |= {(int(path.stem[5:]), quest, stage) for quest, stage in theirs}
        elif stages(code, f"the mod's {path.name}") != theirs:
            wrong.append(f"the mod's {path.name} no longer sets the remaster's stage(s) {theirs}")
        else:
            kept += len(theirs)
    floors = {to for _, _, to in STORY_STAGE_FLOORS}
    for number, quest, stage in sorted(lost):
        if (number, quest, stage) not in STORY_STAGES_LEFT and not (quest == 0 and stage in floors):
            wrong.append(f"event {number}, now one of ReMixed's, set the remaster's stage {stage} (quest {quest}): the "
                         "world map's grey roads and objective line follow the stage (STORY_STAGE_FLOORS, or "
                         "STORY_STAGES_LEFT with why)")
    wrong += [f"STORY_STAGES_LEFT names event {number}'s stage {stage} (quest {quest}), which no reused event set"
              for number, quest, stage in sorted(set(STORY_STAGES_LEFT) - lost)]
    wrong += [f"STORY_STAGE_FLOORS brings back stage {to}, which no reused event set"
              for to in sorted(floors - {stage for _, quest, stage in lost if quest == 0})]
    wrong += [f"STORY_STAGE_FLOORS: the stage has to rise ({low} to {to})"
              for _, low, to in STORY_STAGE_FLOORS if low >= to]
    marks = {mark for _, _, mark in OWN_STORY_STEPS}
    for flag in (SIDE_CHAPTER_A_FLAG, ALGUS_FLAG):
        if flag not in marks:
            wrong.append(f"flag 0x{flag:X}'s story isn't in OWN_STORY_STEPS: the remaster's stage would grey its roads")
    for flag, places in own_story_destinations().items():
        if flag not in marks:
            wrong.append(f"flag 0x{flag:X} has destinations but isn't a story in OWN_STORY_STEPS")
        shown = {place for name, steps, mark in OWN_STORY_STEPS if mark == flag for _, _, actions in steps
                 for _, kind, place, _ in actions if kind == WORLD_PLACE}
        for progress, place in places.items():
            if not 0 <= place < WORLD_EVENT_UNUSED:
                wrong.append(f"flag 0x{flag:X}'s destination at progress 0x{progress:X} isn't a place ({place})")
            elif flag == SIDE_CHAPTER_A_FLAG and place not in shown and place != SIDE_CHAPTER_A_START_PLACE:
                wrong.append(f"1-A's destination at progress 0x{progress:X}, place {place}, is one none of its "
                             "world-map steps shows: its marker would stand on a hidden place")
    if wrong:
        sys.exit("the remaster's story stage: " + "; ".join(wrong))
    return (f"{len(lost)} stage(s) of the remaster's lost with reused numbers or skipped scenes: {len(floors)} follow the story's "
            f"progress, {len(STORY_STAGES_LEFT)} left with a reason; {kept} kept in the stock events the mod ships; no "
            f"road greyed in {len(OWN_STORY_STEPS)} stories of ReMixed's own")


def audit_stock_event_graphics(cfg: dict, graphics: set[int]) -> int:
    """Prove no stock enhanced event script loads any of these event graphics (the slots ReMixed's art takes): each is
    read instruction by instruction as the game reads it, to its END, and a script that can't be read so fails too."""
    paths = stock_event_paths(cfg)
    table = remaster_length_table(cfg)
    found, unread = [], []
    for path in paths:
        code, at = path.read_bytes(), 0
        while at < len(code) and code[at] != 0xDB:
            size = remaster_instruction_size(table, code, at)
            if code[at] == 0x58 and at + 2 < len(code) and code[at + 2] in graphics:
                found.append(f"{path.name}:0x{at:X} loads 0x{code[at + 2]:X} into slot {code[at + 1]}")
            at += size
        if at >= len(code):
            unread.append(path.name)
    if unread:
        raise ValueError("stock enhanced events that can't be read to their END (an instruction of the remaster's "
                         "the build doesn't know?), so their event graphics can't be checked: " + ", ".join(unread))
    if found:
        raise ValueError("ReMixed's event art takes graphics stock enhanced events load: " + ", ".join(found))
    return len(paths)


def write_event_graphics(cfg: dict) -> str:
    """The remaster's event graphics with REMIXED_EVENT_GRAPHICS from ReMixed's disc, as fftpack/event_evtchr_bin.bin in
    the mod. Returns a line for the report."""
    tic = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced"
    extract = WORK / "remaster" / "extract" / "enhanced"
    source = extract / "fftpack" / "event_evtchr_bin.bin"
    if not source.exists():
        ff16tools(cfg, "unpack-all", "-i", tic / "0002.pac", "-o", extract)
    graphics = bytearray(source.read_bytes())
    with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
        theirs_vanilla, theirs = FilePair(vanilla, patched).get(EVTCHR_FILE)
    for source_graphic in REMIXED_EVENT_GRAPHICS:
        source_at = slice(source_graphic * EVTCHR_BYTES, (source_graphic + 1) * EVTCHR_BYTES)
        if graphics[source_at] != theirs_vanilla[source_at]:
            sys.exit(f"the remaster's event graphic 0x{source_graphic:X} isn't vanilla's: check what it shows before "
                     "copying ReMixed's redraw")
        target_graphic = EVENT_GRAPHIC_ALIASES.get(source_graphic, source_graphic)
        need = (target_graphic + 1) * EVTCHR_BYTES
        if len(graphics) < need:
            graphics.extend(bytes(need - len(graphics)))
        target_at = slice(target_graphic * EVTCHR_BYTES, (target_graphic + 1) * EVTCHR_BYTES)
        graphics[target_at] = theirs[source_at]
        for frame in EVENT_GRAPHIC_ONE_SIDED.get(source_graphic, ()):
            table = target_graphic * EVTCHR_BYTES + EVENT_FRAMES
            graphics[table + 4 * (frame ^ 1):table + 4 * (frame ^ 1) + 4] = graphics[table + 4 * frame:table + 4 * frame + 4]
    one_sided = sum(len(frames) for frames in EVENT_GRAPHIC_ONE_SIDED.values())
    if not set(EVENT_GRAPHIC_ONE_SIDED) <= set(REMIXED_EVENT_GRAPHICS):
        sys.exit("EVENT_GRAPHIC_ONE_SIDED names a graphic the mod doesn't ship")
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "fftpack" / "event_evtchr_bin.bin"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(graphics)
    expected_textures = set(EVENT_GRAPHIC_ALIASES.values()) | set(EVENT_GRAPHICS_IN_PLACE)
    if set(EVENT_TEXTURES) != expected_textures:
        missing = expected_textures - set(EVENT_TEXTURES)
        extra = set(EVENT_TEXTURES) - expected_textures
        sys.exit("the routed event graphics don't each have exactly one enhanced texture: "
                 f"missing {sorted(missing)}, extra {sorted(extra)}")
    entries = [entry for _, entry in EVENT_TEXTURES.values()]
    if any(entry < 0 for entry in entries) or len(entries) != len(set(entries)):
        sys.exit("the routed event graphics' enhanced texture entries must be nonnegative and unique")
    sprite_entries = {entry + half for _, entry in SPRITE_TEXTURES.values() if entry >= 0 for half in (0, 1)}
    collisions = sorted(set(entries) & sprite_entries)
    if collisions:
        sys.exit(f"event graphics and unit sprites share replacement texture entries: {collisions}")
    g2d = WORK / "remaster" / "extract" / "enhanced" / "system" / "ffto" / "g2d.dat"
    if not g2d.exists():
        tic = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced"
        ff16tools(cfg, "unpack-all", "-i", tic / "0007.pac", "-o", WORK / "remaster" / "extract" / "enhanced")
    neutral = sprites.g2d_entry(g2d, EVENT_NEUTRAL_PALETTE_TEXTURE)
    if len(neutral) != 512 * 442 or any(neutral):
        sys.exit(f"event palette texture 0x{EVENT_NEUTRAL_PALETTE_TEXTURE:X} isn't the expected neutral 512x442 map")
    for alias, (_, entry) in EVENT_TEXTURES.items():
        if alias in STUB_EVENT_TEXTURE_GRAPHICS:
            continue
        base, palette = sprites.g2d_entry(g2d, entry), sprites.g2d_entry(g2d, entry + 0x8C)
        if len(base) != 256 * 442 or len(palette) != 512 * 442 or any(palette):
            sys.exit(f"event graphic 0x{alias:X}'s native texture pair 0x{entry:X}/0x{entry + 0x8C:X} "
                     "isn't an event-sized base with a neutral palette map")
    for alias in STUB_EVENT_TEXTURE_GRAPHICS:
        entry = EVENT_TEXTURES[alias][1]
        if entry + 0x8C == EVENT_NEUTRAL_PALETTE_TEXTURE:
            sys.exit(f"event graphic 0x{alias:X} no longer needs its explicit neutral-palette routing")
        accidental = sprites.g2d_entry(g2d, entry + 0x8C)
        if len(accidental) != 512 * 442 // 2 or not any(accidental):
            sys.exit(f"event graphic 0x{alias:X}'s automatic palette entry 0x{entry + 0x8C:X} isn't the expected "
                     "unrelated non-neutral event picture")
    textures = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "system" / "ffto" / "g2d"
    textures.mkdir(parents=True, exist_ok=True)
    stale = []
    for entry in sorted(set(EVENT_TEXTURE_RANGE) - set(entries) - sprite_entries):
        path = textures / f"tex_{entry}.bin"
        if path.exists():
            path.unlink()
            stale.append(path.name)
    for alias, (source_graphic, entry) in EVENT_TEXTURES.items():
        source_at = slice(source_graphic * EVTCHR_BYTES, (source_graphic + 1) * EVTCHR_BYTES)
        texture = event_texture(theirs[source_at], source_graphic)
        path = textures / f"tex_{entry}.bin"
        path.write_bytes(texture)
        if path.read_bytes() != texture:
            sys.exit(f"event graphic 0x{alias:X}'s enhanced texture wasn't written intact as {path.name}")
    try:
        stock_events = audit_stock_event_graphics(cfg, set(EVENT_TEXTURES))
        animation_uses, unique_animations = audit_event_graphic_animations(bytes(graphics), textures)
    except ValueError as error:
        sys.exit(str(error))
    own = ", ".join(f"0x{n:X}" for n in REMIXED_EVENT_GRAPHICS)
    textured = ", ".join(f"0x{source:X}->0x{alias:X} (g2d {entry})"
                         for alias, (source, entry) in EVENT_TEXTURES.items())
    return (f"{len(REMIXED_EVENT_GRAPHICS)} of ReMixed's ({own}), {len(EVENT_GRAPHICS_IN_PLACE)} in place and "
            f"{len(EVENT_GRAPHIC_ALIASES)} in private slots; enhanced textures for " + textured
            + f"; {one_sided} frame(s) drawn the same from both sides (EVENT_GRAPHIC_ONE_SIDED)"
            + f"; none of their slots loaded by the {stock_events} stock scenes; "
            + (f"old texture(s) taken out: {', '.join(stale)}; " if stale else "")
            + f"{animation_uses} pose use(s), {unique_animations} unique alias/animation pairs audited")


# ReMixed rebuilt E331 for Cleave and Scratch, and changed E306 for Rally and Composure. The enhanced remaster's two
# names for each effect are both byte copies of the PS1 file (one is used by the effect loader and one by its data
# lookup), so both must be replaced together. Keeping the vanilla comparison here makes a future remaster update fail
# loudly instead of replacing a newly-redrawn effect unknowingly.
REMIXED_EFFECTS = (306, 331)


def write_remixed_effects(cfg: dict) -> str:
    """Ship ReMixed's changed PS1 effect files under both names the enhanced remaster reads."""
    extract = WORK / "remaster" / "extract" / "enhanced"
    effect_dir = extract / "fftpack" / "effect"
    if not all((effect_dir / f"effect_e{effect:03d}_bin.bin").exists() for effect in REMIXED_EFFECTS):
        tic = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced"
        ff16tools(cfg, "unpack-all", "-i", tic / "0002.pac", "-o", extract)
    output = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "fftpack" / "effect"
    output.mkdir(parents=True, exist_ok=True)
    with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
        for effect in REMIXED_EFFECTS:
            path = f"EFFECT/E{effect:03d}.BIN"
            old, new = FilePair(vanilla, patched).get(path)
            names = (f"effect_e{effect:03d}_bin.bin", f"efc_e{effect:03d}dat_data.bin")
            for filename in names:
                source = effect_dir / filename
                if source.read_bytes() != old:
                    sys.exit(f"the remaster's {filename} isn't vanilla's {path}; check it before replacing it")
                (output / filename).write_bytes(new)
    return f"{len(REMIXED_EFFECTS)} ({', '.join(f'E{effect:03d}' for effect in REMIXED_EFFECTS)}), under both remaster names"


# ReMixed's unit art the remaster has none of (asked 2026-11-12: "for any art not in the remaster, just use the psx
# remix sprites", with a filter to bring them nearer the remaster's own). ReMixed names its sheets by graphic
# (BATTLE/00.SPR to 9E.SPR); of its 159, about 120 are the remaster's art as they are. The rest:
# - its own characters and monsters in slots vanilla filled with copies or dummies (below), which nothing of the
#   remaster's draws, so they take ReMixed's art outright;
# - its own characters in the second sprite sets of vanilla's story characters (0x11 Gafgarion, 0x12 Malak, 0x19
#   Rafa, 0x34 Agrias, 0x3A Celia, 0x3B Lede, 0x46-0x47) and in 0x36-0x38, which ReMixed freed by giving each story
#   character one sprite set: the remaster's own units there move to the sprite sets that draw the same art
#   (REMASTER_MOVED_SPRITE_SETS), and these take ReMixed's (below; 0x38 the remaster's Bard, REMIXED_SPRITE_ALIASES).
#   ReMixed animates four of them as type1 where vanilla's were type2 (0x19, 0x37, 0x3A, 0x46: BATTLE.BIN 0x2D748),
#   which their art needs too (the animation sets, done for every graphic here by the monster animations' work);
#   0x22, Mustadio's second, is vanilla's Agrias sheet in ReMixed, which its young Agrias draws as the remaster's Agrias;
# - its versions of characters the remaster has (its Dark Knight in the Bard's 0x82, children with more frames,
#   recoloured palettes for 0x08 and the chocobos), which keep the remaster's art.
# Graphic -> what it shows. Each ships its PS1 sheet (the classic sprites, and the palettes the enhanced ones use), its
# two enhanced textures, scaled with Scale2x (tools/sprites.py), under the textures the table gives the graphic, and
# ReMixed's animation set for it (SPRITE_ANIMATIONS).
# The build holds one back while a battle entry of the remaster's still uses its sprite set (remaster_sprite_sets).
REMIXED_SPRITES = {
    0x11: "a blonde woman in a long green cloak (vanilla: Gafgarion's second sprite set, the enemy)",
    0x12: "Luso (vanilla: Malak's second sprite set; ReMixed's events find Luso by it)",
    0x19: "a spiky-haired young man in white and grey (vanilla: Rafa's second sprite set, the guest)",
    0x34: "Ashley, bare-armed in brown, with gauntlets (vanilla: Agrias's second sprite set, the guest)",
    0x36: "a brown-haired woman in white with a red scarf (vanilla: a copy of the female White Mage)",
    0x37: "a figure in dark blue armour and a skull helm (vanilla: a copy of the male Black Mage)",
    0x3A: "a brown-haired knight in white and blue (vanilla: Celia's second sprite set, unused)",
    0x3B: "a woman in a purple and white robe (vanilla: Lede's second sprite set, unused)",
    0x46: "the purple-haired fighter again, 0x59's sheet (vanilla: a copy of the male Mystic)",
    0x47: "0x36's woman in other colours (vanilla: a copy of the female Summoner)",
    # The children: vanilla's 10M and 10W sheets are a scene's (type2 frames and animations: walking, a few poses),
    # and the remaster's redraws of them are too. ReMixed fields a boy and a girl as guests in 1-A's last battle
    # (0x0FB), on sheets of its own with a battle unit's frames (type1, and shorter: height 0x18). On the remaster's
    # art they stood there unseen but when walking or in water, with no face, and the camera had nothing to find
    # for the scene of one falling (a tester, 2026-11-22; seen in the hidden PC game). ReMixed draws vanilla's scenes
    # with these sheets too, so they take ReMixed's art everywhere.
    0x4A: "a boy, with a battle unit's frames (vanilla: 10M.SPR, a scene's sheet)",
    0x4B: "a girl, with a battle unit's frames (vanilla: 10W.SPR, a scene's sheet)",
    0x35: "a purple-haired fighter in green (vanilla: a copy of the female Chemist)",
    0x39: "a figure in a dark hooded cloak (vanilla: a copy of the male Squire)",
    0x57: "a grey dragon (vanilla: a copy of the male Squire); ReMixed's 0x198 and 0x1B2, the mod's 0x1F3",
    0x58: "an orange-haired woman (vanilla: a copy of the male Squire)",
    0x59: "the purple-haired fighter again (vanilla: a copy of the male Squire)",
    0x5A: "a hydra (vanilla: a copy of the male Squire); ReMixed's 0x182 and 0x1AE, the mod's 0x1EF",
    0x5D: "a blond man in red (vanilla: a test pattern, DAMI.SPR)",
    0x5E: "swords standing in the ground (vanilla: a red silhouette, KASANEK.SPR)",
    0x5F: "a knight in grey with a red hood (vanilla: a red silhouette, KASANEM.SPR)",
    0x97: "a Lamia, in the Hydras' slot (ReMixed's Hydra family and its Serpentarius draw as Lamias)",
    0x98: "a Tonberry (vanilla: a copy of the Dragon; ReMixed's job 0x98 draws as it)",
    0x9F: "ReMixed's man's Dark Knight, its 82.SPR (the remaster's Dark Knight's man, with no art; DARK_KNIGHT_GRAPHICS)",
}
# The remaster's Dark Knight job (0xA0, DARK_KNIGHT) draws a man as graphic 0x9F and a woman as 0xA0 (0x278EDC), the
# PSP's numbers, which it has no art for (4,608-byte stub textures, sheets its archives lack: fftpack 880-881,
# unit_psp/) and no row in CharShape (NXD 0xAE, keyed by graphic, 0x00-0x9E). The per-unit draw 0x216EB4 (from
# 0x213F14) reads the row's +0x2C (its height) without checking it, so a battle with a Dark Knight in it went black and
# crashed about 12 s in, at exe+0x217172 (Brave Story's rounds 0x332 and 0x342, ReMixed's only battles with Dark
# Knights; seen on the PC, 2026-11-19, on every build). GenericJobs (the job menu's second page, docs/release) ships
# rows, art and faces of its own for them, which the PC's install hasn't. The port draws them as ReMixed does without
# it: ReMixed's man's Dark Knight is its 82.SPR (it moved the Bard out of 0x82 for it), its woman's its 37.SPR (its
# job 0x37's), which the mod already gives graphic 0x37. So 0x9F takes ReMixed's 82.SPR (REMIXED_SPRITE_SHEETS) under
# texture 0x39A, 0x38's own, which nothing draws once 0x38 draws as the Bard (REMIXED_SPRITE_ALIASES), and face 0x5B
# (a male Squire copy's number, which the remaster's fold never gives); 0xA0 draws as 0x37, its face too; and each gets
# a CharShape row, a copy of the row of a graphic drawn the same way (DARK_KNIGHT_SHAPES). None of these are the
# numbers GenericJobs' files take (textures 0x456-0x45E, faces 159-162), so with it installed the two mods' art for
# a graphic never mixes (ReMixed's sheet and palettes with its textures); its rows merge with these cell by cell.
DARK_KNIGHT_GRAPHICS = (0x9F, 0xA0)
DARK_KNIGHT_SHAPES = {0x9F: 0x64, 0xA0: 0x37}  # graphic -> the game's row it copies (the male Knight's, 0x37's)
# ReMixed's sheet for a graphic of REMIXED_SPRITES that ReMixed draws under another number (else its own BATTLE/XX.SPR).
REMIXED_SPRITE_SHEETS = {0x9F: 0x82}
# The remaster's animation sets by graphic (.rdata 0x673300, 4 bytes each, 0xAA of them): BATTLE.BIN's (SPRITE_ANIMATIONS)
# for 0x00-0x9E, then the PSP's graphics, which ReMixed's BATTLE.BIN hasn't. The build checks the first part matches.
REMASTER_ANIMATION_SETS, REMASTER_GRAPHICS = 0x673300, 0xAA
# The texture each graphic's art goes under (its first; the second is the next number), and what the remaster's table
# (.rdata 0x7151F0) holds there now, which the DLL checks before changing it. 0x5D-0x5F have no texture in the
# remaster (-1, so it drew their classic dummies, the red stand-ins): 0x5D takes 0x370, which nothing names (a copy of
# Agrias's), and 0x5E and 0x5F take those of 0x5B and 0x5C, copies of the male Squire that neither game draws.
# 0x12 shares Malak's (0x1A's, 0x368) in the remaster, so it takes 0x460: the remaster's slot for the PSP's Luso (0xA4),
# whose textures, like all of 0x9F-0xA9's, are 4,608-byte stubs (and whose sheets aren't in its archives). The others
# that share a story character's texture (0x11 Gafgarion's 0x362, 0x19 Rafa's 0x366, 0x34 Agrias's 0x392, 0x3A
# Celia's 0x39E, 0x3B Lede's 0x3A0, 0x47 the female Summoner's 0x402) take the stubs of the PSP's other characters
# that nothing draws (0xA1-0xA3 and 0xA5-0xA7), not the Dark Knight's (0x9F, 0xA0), the remaster's own job's;
# 0x36, 0x37 and 0x46 have textures of their own (copies of the generics', which keep theirs), which take ReMixed's.
SPRITE_TEXTURES = {0x11: (0x362, 0x45A), 0x12: (0x368, 0x460), 0x19: (0x366, 0x45C), 0x34: (0x392, 0x45E),
                   0x35: (0x394, 0x394), 0x36: (0x396, 0x396), 0x37: (0x398, 0x398), 0x39: (0x39C, 0x39C),
                   0x3A: (0x39E, 0x462), 0x3B: (0x3A0, 0x464), 0x46: (0x3B4, 0x3B4), 0x47: (0x402, 0x466),
                   0x4A: (0x3BA, 0x3BA), 0x4B: (0x3BC, 0x3BC),
                   0x57: (0x3D4, 0x3D4), 0x58: (0x3D6, 0x3D6), 0x59: (0x3D8, 0x3D8), 0x5A: (0x3DA, 0x3DA),
                   0x5D: (-1, 0x370), 0x5E: (-1, 0x3DC), 0x5F: (-1, 0x3DE), 0x97: (0x44E, 0x44E),
                   0x98: (0x450, 0x450), 0x9F: (0x456, 0x39A)}
# ReMixed's graphics whose sheet is one of vanilla's that the remaster draws under another graphic: graphic -> (that
# graphic, what the remaster's texture table holds for this one now). The DLL points the graphic's sheet and texture at
# that graphic's, so it draws the remaster's own redraw rather than a scaled copy. ReMixed's 38.SPR is vanilla's
# GIN_M.SPR, the male Bard, which the remaster draws as 0x82 (ReMixed moved the Bard there to give 0x82 to its Dark
# Knight); its locked look 0xB4 draws as 0x38 (IvaliceCore's CreateUnit.Graphic). Both are type2 (their animation
# sets, BATTLE.BIN 0x2D748, the same in the remaster and ReMixed), so 0x38 animates as the Bard does. The remaster's
# woman's Dark Knight, 0xA0, draws as ReMixed's 0x37 (DARK_KNIGHT_GRAPHICS), whose animation set (ReMixed's: type1 and
# type1) is the one the remaster gives 0xA0; the build checks it.
REMIXED_SPRITE_ALIASES = {0x38: (0x82, 0x39A), 0xA0: (0x37, 0x458)}
# A graphic's animation set: 4 bytes a graphic in BATTLE.BIN (its SHP, its SEQ, a hover flag and its height), the same
# for 0x00-0x9E in the remaster's .rdata 0x673300 (RemixedSprites.cs has what the numbers pick). ReMixed changed it
# for its own art: 0x57 (the dragon) mon and mon, and 0x5A (the hydra) mon and ruka, as vanilla's Hydras, where
# vanilla's copies of the male Squire were a person's; 0x97 (its Lamia) mon and mon, where the Hydras' was mon and
# ruka; 0x5D (a person) type1, where DAMI.SPR's was mon; 0x35 and 0x5F type1, where vanilla's were type2. Each graphic
# of REMIXED_SPRITES takes ReMixed's. The SP2 second sheets, the monsters' frames for some of their actions, need
# nothing: the PS1 names them by graphic (BATTLE.BIN 0x2E1CC, {sector, size}) and the remaster the same way (files
# .rdata 0x785760 and 0x785210, textures 0x714950), and ReMixed has them only for vanilla's 0x88-0x9A, as both have.
SPRITE_ANIMATIONS = 0x2D748
# ReMixed's art that is vanilla's under another graphic, so the remaster has its own redraw of it: graphic -> vanilla's
# graphic for that art. ReMixed's hydra (0x5A) is vanilla's Hydra sheet (HEBI.SPR, 0x97), moved to make room for its
# Lamia; the remaster's redraw is 0x97's two textures (0x44E-0x44F), which the Lamia's take over in the mod, so 0x5A gets
# a copy of them from the remaster's g2d.dat under its own (0x3DA-0x3DB), not Scale2x. The build checks the sheets match.
SPRITE_REMASTER_ART = {0x5A: 0x97}
# ReMixed's sheets that are vanilla's pixel for pixel with palettes of its own added: graphic -> the palettes that
# differ (0-7 a unit's, 8-15 its portrait's). The chocobo's fourth is its White Chocobo's (job 0x95: the chocobo's
# graphic, palette 3) and Zalbag's second and third are his other outfits'; all but the chocobo's 11 are empty in
# vanilla, so the remaster drew such a unit black: Zalbag's mount at Eagrose's gate, a black shape (a tester on
# 0.9.39, 2026-11-26). A scan of ReMixed's BATTLE/*.SPR finds these two and no other (every other sheet of its own
# has pixels of its own: REMIXED_SPRITES, or art the port doesn't draw). The remaster colours its redrawn textures
# by the sheet's palettes, so the mod gives the game ReMixed's sheet in the graphic's place and leaves its textures,
# animation set and faces as they are; the added portrait palettes get faces made from the sheet's portrait.
REMIXED_PALETTE_SHEETS = {0x08: (1, 2, 9, 10), 0x86: (3, 11)}
REMASTER_SPRITE_TEXTURES = 0x7151F0  # .rdata: a graphic's first texture, a dword each (-1 none)
SPRITE_SHEETS = 0x2DCD4  # BATTLE.BIN: {sector, size} a graphic, its sheet on the disc
SPRITES_DIR = "Sprites"


def remaster_sprite_sets(cfg: dict) -> dict[int, list[int]]:
    """The sprite sets the remaster's own battle entries (below FREE_ENTRIES, which hold ReMixed's, and its added
    entries from 0x200) use in the mod's battle data as built (the remaster's file where the mod has none): sprite set
    -> entries. A special character's sprite set is its graphic, so ReMixed's art for one that the remaster still
    draws would show in its battles."""
    stock = WORK / "remaster" / "extract" / "enhanced" / "fftpack"
    built = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "fftpack"
    entry_bytes = UNITS_PER_EVENT * UNIT_SIZE
    used: dict[int, list[int]] = {}
    files = [(f"battle_entd{k}_ent.bin", (k - 1) * 0x80, FREE_ENTRIES.start) for k in range(1, 5)]
    for name, first, end in files + [(ADDED_ENTRIES_FILE, 0x200, 0x400)]:
        path = built / name if (built / name).exists() else stock / name
        data = path.read_bytes()
        for e in range(len(data) // entry_bytes):
            entry = first + e
            if entry >= end:
                break
            for u in range(UNITS_PER_EVENT):
                at = e * entry_bytes + u * UNIT_SIZE
                if data[at] and entry not in used.setdefault(data[at], []):
                    used[data[at]].append(entry)
    return used


def old_guest_rows() -> list[list[int]]:
    """The code mod's data for REMASTER_OLD_GUESTS (Patches/OldGuests.cs): [old sprite set, new sprite set, the job for
    one whose job is the old slot's, the guest's name id, the story progress from which it's left as it is]."""
    rows = []
    for old, (name, until) in sorted(REMASTER_OLD_GUESTS.items()):
        sprites, job = REMASTER_MOVED_SPRITE_SETS[old]
        rows.append([old, sprites, job if job is not None else old, name, until])
    return rows


def guest_lookup_rows() -> list[list[int]]:
    """The code mod's data for REMASTER_CODE_SPRITE_SETS (Patches/GuestLookup.cs): [the character id, the sprite set the
    remaster's code looks for, the sprite set the port's battle data has the unit on]."""
    return [[character, old, REMASTER_MOVED_SPRITE_SETS[old][0]]
            for character, old in sorted(REMASTER_CODE_SPRITE_SETS.items())]


def write_remaster_moves(cfg: dict) -> str:
    """The remaster's own added entries (ADDED_ENTRIES_FILE, from 0x200) with its units off the slots ReMixed gave its
    own characters (REMASTER_MOVED_SPRITE_SETS), into the mod. Returns a line for the report."""
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced"
    stock = WORK / "remaster" / "extract" / "enhanced" / "fftpack" / ADDED_ENTRIES_FILE
    added = bytearray(stock.read_bytes())
    moved = move_remaster_units(added, 0x200)
    if moved:
        (out / "fftpack").mkdir(parents=True, exist_ok=True)
        (out / "fftpack" / ADDED_ENTRIES_FILE).write_bytes(added)
    return f"{moved} units of the added entries moved"


def stock_event_paths(cfg: dict) -> list[Path]:
    """The enhanced build's 560 stock event scripts (script/enhanced/eventNNN.e), extracted into work/."""
    extract = WORK / "remaster" / "extract" / "enhanced"
    scripts = extract / "script" / "enhanced"
    paths = sorted(scripts.glob("event*.e"))
    if len(paths) != 560:
        ff16tools(cfg, "unpack-all", "-i", Path(cfg["target"]["tic_dir"]) / "data" / "enhanced" / "0005.pac",
                  "-o", extract)
        paths = sorted(scripts.glob("event*.e"))
    if len(paths) != 560:
        raise ValueError(f"the enhanced script pack has {len(paths)} event files, expected 560")
    return paths


def remaster_sprite_set_uses(table: bytes, code: bytes, name: str) -> list[tuple[int, int]]:
    """(offset, opcode) of each instruction of a remaster event script, read as the game reads it to its END, that
    names a moved sprite set (REMASTER_SPRITE_SET_OPS, REMASTER_MOVED_SPRITE_SETS)."""
    uses, at = [], 0
    while at < len(code) and code[at] != 0xDB:
        op = code[at]
        if (op in REMASTER_SPRITE_SET_OPS and at + 2 < len(code) and code[at + 1] in REMASTER_MOVED_SPRITE_SETS
                and (op != 0x7A or code[at + 2] == 0)):
            uses.append((at, op))
        at += remaster_instruction_size(table, code, at)
    if at >= len(code):
        raise ValueError(f"{name} can't be read to its END, so its sprite sets can't be checked")
    return uses


def strip_world_unit_screen_guard(code: bytearray, name: str) -> bool:
    """Take an earlier build's guard out of a stock event (STRIPPED_SCREEN_GUARDS): its two runs of bytes, each found
    once and in order, or neither. True if they were there."""
    wanted = STRIPPED_SCREEN_GUARDS.get(name)
    if wanted is None:
        return False
    before, after = wanted
    first, last = code.find(before), code.find(after)
    if first < 0 and last < 0:
        return False
    if first < 0 or last < first or code.count(before) != 1 or code.count(after) != 1:
        raise ValueError(f"{name}: half of an earlier build's guard is there (at {first} and {last})")
    del code[last:last + len(after)]
    del code[first:first + len(before)]
    return True


def write_remaster_sprite_sets(cfg: dict) -> str:
    """Every remaster event script, as the mod ships it (unmarked: ReMixed's own carry REMIXED_EVENT_MARK and its
    numbers) or stock, with each sprite set REMASTER_SPRITE_SET_OPS names moved as its units are
    (REMASTER_MOVED_SPRITE_SETS), into the mod. Run after every step that writes events. Returns a line for the
    report."""
    table = remaster_length_table(cfg)
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "script" / "enhanced"
    done, stripped = [], []
    for stock in stock_event_paths(cfg):
        shipped = out / stock.name
        code = bytearray(shipped.read_bytes() if shipped.exists() else stock.read_bytes())
        if code.startswith(REMIXED_EVENT_MARK):
            continue
        if strip_world_unit_screen_guard(code, stock.name):
            stripped.append(stock.stem)
        uses = remaster_sprite_set_uses(table, bytes(code), stock.name)
        for at, op in uses:
            old = code[at + 1]
            new = REMASTER_MOVED_SPRITE_SETS[old][0]
            if new in GENERIC_SPRITE_SETS:
                sys.exit(f"{stock.name}: {REMASTER_SPRITE_SET_OPS[op]} names 0x{old:02X}, whose units became generics "
                         f"(0x{new:02X}), which have no sheet of their own to name")
            code[at + 1] = new
            done.append(f"{stock.stem} {REMASTER_SPRITE_SET_OPS[op]} 0x{old:02X} to 0x{new:02X}")
        if uses or stock.stem in stripped:
            # Proof: what's shipped is the stock script but for the sprite sets moved (same length, those bytes only).
            original = stock.read_bytes()
            moved = {at + 1 for at, _ in uses}
            if len(code) != len(original) or any(a != b and k not in moved for k, (a, b) in enumerate(zip(code, original))):
                sys.exit(f"{stock.name} as shipped differs from the stock script in more than its moved sprite sets")
            out.mkdir(parents=True, exist_ok=True)
            shipped.write_bytes(code)
    for path in sorted(out.glob("event*.e")):  # proof: no unmarked script the mod ships names one still
        code = path.read_bytes()
        if not code.startswith(REMIXED_EVENT_MARK) and remaster_sprite_set_uses(table, code, path.name):
            sys.exit(f"{path.name} still names a moved sprite set")
    return f"{len(done)} moved: " + ", ".join(done) + f"; {len(stripped)} earlier build's guard taken out: " \
           + ", ".join(stripped)


# The remaster's enhanced battle-unit overrides (nxd/overrideentrydata.nxd, OverrideEntryData: Key a battle entry, Key2
# one of its units) lay its own fixes over the battle data as the enhanced build makes a battle's units; a cell at its
# column's none (the value most of its 516 rows have) keeps the battle data's. The mod left the table alone until
# 0.9.23, so where an override meets the port it undid it:
# - Sprite sets: entry 0x1AD's unit 1, the frog scene's (event 270) Rafa, loads the guest with sprite set 0x19 from the
#   formation. No roster unit has it (Rafa's is 0x29, REMASTER_MOVED_SPRITE_SETS), so the unit was made empty, in
#   0x19's art and face (ReMixed's character's), and its join screen read "?" at level 0 (a tester on 0.9.22). An
#   override's sprite set moves as the battle data's do.
# - Fields of ReMixed's unit builds (UNIT_BUILD_FIELDS; the column -> the battle data's bytes): where the port changed
#   the unit's field from the remaster's (its sprite-set moves aside), an override put the remaster's value back (the
#   secondary skillset of 0x14A's, 0x183's, 0x185's and 0x197's units, 0x183's abilities). Those cells go to none and
#   the port's value stands; the remaster's other overrides (its abilities for a unit ReMixed left as vanilla's) stay.
# - ReMixed's own battle entries (FREE_ENTRIES), which no scenario of the remaster's names: its rows for 0x1E6 and
#   0x1EE gave units 2-5 there a weapon. Every cell of a row there goes to none.
ENTRY_OVERRIDE_FIELDS = {"Level": (3,), "Bravery": (6,), "Faith": (7,), "JobUnlock": (8,), "JobLevel": (9,),
                         "MainJob": (10,), "SecondarySkillset": (11,), "Reaction": (12, 13), "Support": (14, 15),
                         "Movement": (16, 17), "Head": (18,), "Body": (19,), "Accessory": (20,), "RightHand": (21,),
                         "LeftHand": (22,), "EntryUnknown1D": (ENTD_PRIMARY,)}
ENTRY_OVERRIDE_KEYS = ("Key", "Key2")


def write_entry_overrides(cfg: dict) -> str:
    """The remaster's OverrideEntryData with its sprite sets moved and its cells that undo the port's battle data at
    none (ENTRY_OVERRIDE_FIELDS), into the mod. Run after the battle data is written. Returns a line for the report."""
    stock_dir = WORK / "remaster" / "extract" / "enhanced" / "fftpack"
    built_dir = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "fftpack"
    names = [f"battle_entd{k}_ent.bin" for k in range(1, 5)]
    expected = b"".join(bytes(_moved_copy(stock_dir / n, (k - 1) * 0x80)) for k, n in enumerate(names, 1))
    built = b"".join((built_dir / n if (built_dir / n).exists() else stock_dir / n).read_bytes() for n in names)
    entry_bytes = UNITS_PER_EVENT * UNIT_SIZE
    counts = collections.Counter()

    def edit(db: sqlite3.Connection) -> str:
        columns = [r[1] for r in db.execute('pragma table_info("OverrideEntryData")')]
        rows = [dict(zip(columns, r)) for r in db.execute('select * from "OverrideEntryData"')]
        none = {c: collections.Counter(json.dumps(r[c]) for r in rows).most_common(1)[0][0] for c in columns}
        for c in ENTRY_OVERRIDE_FIELDS:
            if c not in columns:
                sys.exit(f"OverrideEntryData has no column {c} (ENTRY_OVERRIDE_FIELDS)")
        for r in rows:
            entry, u = r["Key"], r["Key2"]
            changes: dict[str, object] = {}
            if entry in FREE_ENTRIES:
                changes = {c: json.loads(none[c]) for c in columns
                           if c not in ENTRY_OVERRIDE_KEYS and json.dumps(r[c]) != none[c]}
                counts["ReMixed's entries"] += len(changes)
            else:
                if r["Spriteset"] in REMASTER_MOVED_SPRITE_SETS:
                    changes["Spriteset"] = REMASTER_MOVED_SPRITE_SETS[r["Spriteset"]][0]
                    counts["sprite sets"] += 1
                if entry < FREE_ENTRIES.start and u < UNITS_PER_EVENT:
                    at = entry * entry_bytes + u * UNIT_SIZE
                    for c, offsets in ENTRY_OVERRIDE_FIELDS.items():
                        if json.dumps(r[c]) != none[c] and any(built[at + i] != expected[at + i] for i in offsets):
                            changes[c] = json.loads(none[c])
                            counts["port's fields"] += 1
            if changes:
                sets = ", ".join(f'"{c}" = ?' for c in changes)
                db.execute(f'update "OverrideEntryData" set {sets} where Key = ? and Key2 = ?',
                           [*(json.dumps(v) if isinstance(v, list) else v for v in changes.values()), entry, u])
        moved = [r for r in db.execute('select Key, Key2 from "OverrideEntryData" where Spriteset in ({})'.format(
            ", ".join(str(s) for s in REMASTER_MOVED_SPRITE_SETS)))]
        if moved:
            sys.exit(f"OverrideEntryData still names a moved sprite set: {moved}")
        return "; " + ", ".join(f"{n} cell(s) for {k}" for k, n in counts.items())

    return add_nxd_rows(cfg, "overrideentrydata", "OverrideEntryData", {}, edit=edit)


def _moved_copy(path: Path, first_entry: int) -> bytearray:
    """A stock battle data file with the remaster's units moved off ReMixed's slots, as the port moves them."""
    data = bytearray(path.read_bytes())
    move_remaster_units(data, first_entry)
    return data


def write_remixed_sprites(cfg: dict, battle_old: bytes, battle_new: bytes
                          ) -> tuple[str, list[list[int]], list[list[int]]]:
    """REMIXED_SPRITES from ReMixed's disc: each sheet as Sprites/XX.spr in the mod (the DLL gives the game it in the
    graphic's place, RemixedSprites.cs) and its two textures as system/ffto/g2d/tex_N.bin (the mod loader serves
    them), with its animation set from BATTLE.BIN (SPRITE_ANIMATIONS). A graphic of REMIXED_SPRITE_SHEETS takes that
    sheet and its animation set. REMIXED_PALETTE_SHEETS' sheets go in as well, with the remaster's own textures.
    Returns a line for the report, the DLL's data: [graphic, its texture now, its texture, its animation set now
    (the remaster's), ReMixed's], and the same rows for the palette sheets (which get no faces or aliases of the
    mod's: write_remixed_faces makes only their added palettes')."""
    sheets = MOD_DIR / SPRITES_DIR
    textures = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "system" / "ffto" / "g2d"
    if sheets.exists():
        shutil.rmtree(sheets)
    sheets.mkdir(parents=True)
    textures.mkdir(parents=True, exist_ok=True)
    data, held, redrawn = [], [], []
    remasters = remaster_sprite_sets(cfg)
    remaster_sets = remaster_animation_sets(cfg, battle_old)
    # A texture an alias frees (0x38's, which draws as the Bard) is free only while that alias isn't held back.
    freed = {now: graphic for graphic, (_, now) in REMIXED_SPRITE_ALIASES.items()}
    g2d = WORK / "remaster" / "extract" / "enhanced" / "system" / "ffto" / "g2d.dat"
    if SPRITE_REMASTER_ART and not g2d.exists():
        tic = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced"
        ff16tools(cfg, "unpack-all", "-i", tic / "0007.pac", "-o", WORK / "remaster" / "extract" / "enhanced")
    with Disc(remixed_bin(cfg)) as patched, Disc(vanilla_bin(cfg)) as vanilla:
        for graphic in sorted(REMIXED_SPRITES):
            if graphic < 0x80 and remasters.get(graphic):
                # Still one of the remaster's characters' (0x12, Malak on Riovanes's rooftop, until its unit moves to
                # his own 0x1A): left as the remaster draws it.
                held.append(f"0x{graphic:02X} (the remaster's entries "
                            + ", ".join(f"0x{e:03X}" for e in remasters[graphic]) + " use it)")
                continue
            source = REMIXED_SPRITE_SHEETS.get(graphic, graphic)
            entry = patched.find(f"BATTLE/{source:02X}.SPR")
            if entry is None:
                sys.exit(f"ReMixed's disc has no BATTLE/{source:02X}.SPR")
            spr = patched.read(entry)
            (sheets / f"{graphic:02X}.spr").write_bytes(spr[:sprites.sheet_length(spr)])
            now, texture = SPRITE_TEXTURES[graphic]
            owner = freed.get(texture)
            if owner is not None and owner < 0x80 and remasters.get(owner):
                sys.exit(f"0x{graphic:02X}'s texture 0x{texture:X} is 0x{owner:02X}'s, which the remaster's entries "
                         + ", ".join(f"0x{e:03X}" for e in remasters[owner]) + " still draw (SPRITE_TEXTURES)")
            if graphic in SPRITE_REMASTER_ART:
                theirs = SPRITE_REMASTER_ART[graphic]
                sector, _ = struct.unpack_from("<II", battle_old, SPRITE_SHEETS + 8 * theirs)
                sheet = vanilla.owner(sector)
                if sheet is None or vanilla.read(sheet).rstrip(b"\0") != spr.rstrip(b"\0"):
                    sys.exit(f"ReMixed's 0x{graphic:02X} isn't vanilla's 0x{theirs:02X} any more (SPRITE_REMASTER_ART)")
                first = SPRITE_TEXTURES.get(theirs, (None,))[0]
                top, bottom = sprites.g2d_entry(g2d, first), sprites.g2d_entry(g2d, first + 1)
                redrawn.append(f"0x{graphic:02X} the remaster's redraw of 0x{theirs:02X} ({sheet.path})")
            else:
                top, bottom = sprites.textures(spr)
            (textures / f"tex_{texture}.bin").write_bytes(top)
            (textures / f"tex_{texture + 1}.bin").write_bytes(bottom)
            set_now = remaster_sets[graphic]
            remixed_set, = struct.unpack_from("<I", battle_new, SPRITE_ANIMATIONS + 4 * source)
            data.append([graphic, now, texture, set_now, remixed_set])
        from exe import Exe  # noqa: PLC0415
        image = Exe(Path(cfg["target"]["tic_dir"]) / "FFT_enhanced.exe")
        recoloured = []
        for graphic, palettes in sorted(REMIXED_PALETTE_SHEETS.items()):
            if graphic in REMIXED_SPRITES:
                sys.exit(f"0x{graphic:02X} is in REMIXED_SPRITES and REMIXED_PALETTE_SHEETS")
            spr = patched.read(patched.find(f"BATTLE/{graphic:02X}.SPR"))
            spr = spr[:sprites.sheet_length(spr)]
            sector, _ = struct.unpack_from("<II", battle_old, SPRITE_SHEETS + 8 * graphic)
            theirs = vanilla.read(vanilla.owner(sector))
            differ = tuple(k for k in range(16) if spr[32 * k:32 * k + 32] != theirs[32 * k:32 * k + 32])
            if spr[0x200:].rstrip(b"\0") != theirs[0x200:].rstrip(b"\0") or differ != palettes:
                sys.exit(f"ReMixed's {graphic:02X}.SPR isn't vanilla's sheet with palettes {palettes} of its own "
                         f"(palettes {differ} differ; REMIXED_PALETTE_SHEETS)")
            texture, = struct.unpack("<i", image.read(REMASTER_SPRITE_TEXTURES + 4 * graphic, 4))
            set_now = remaster_sets[graphic]
            remixed_set, = struct.unpack_from("<I", battle_new, SPRITE_ANIMATIONS + 4 * graphic)
            if texture < 0 or remixed_set != set_now:
                sys.exit(f"0x{graphic:02X} has no texture of the remaster's, or ReMixed animates it otherwise "
                         "(REMIXED_PALETTE_SHEETS)")
            (sheets / f"{graphic:02X}.spr").write_bytes(spr)
            recoloured.append([graphic, texture, texture, set_now, remixed_set])
    names = ", ".join(f"0x{row[0]:02X}" for row in data)
    sets = ", ".join(f"0x{g:02X} {animation_set_name(now)} -> {animation_set_name(new)}"
                     for g, _, _, now, new in data if now != new)
    line = (f"{len(data)} of ReMixed's graphics ({names}): sheets in {SPRITES_DIR}/, Scale2x textures in g2d/"
            + (f" (but {', '.join(redrawn)})" if redrawn else "") + f", ReMixed's animation sets ({sets})")
    line += "; ReMixed's added palettes on the remaster's art: " + ", ".join(
        f"0x{row[0]:02X} ({', '.join(str(k) for k in REMIXED_PALETTE_SHEETS[row[0]])})" for row in recoloured)
    return line + (f"; held back: {'; '.join(held)}" if held else ""), data, recoloured


def remaster_animation_sets(cfg: dict, battle_old: bytes) -> list[int]:
    """The remaster's animation set by graphic (REMASTER_ANIMATION_SETS), checked against vanilla's BATTLE.BIN for the
    graphics it has (0x00-0x9E)."""
    from exe import Exe  # noqa: PLC0415
    image = Exe(Path(cfg["target"]["tic_dir"]) / "FFT_enhanced.exe")
    sets = list(struct.unpack("<" + "I" * REMASTER_GRAPHICS,
                              image.read(REMASTER_ANIMATION_SETS, 4 * REMASTER_GRAPHICS)))
    vanilla = list(struct.unpack_from("<" + "I" * 0x9F, battle_old, SPRITE_ANIMATIONS))
    if sets[:0x9F] != vanilla:
        sys.exit(f"the remaster's animation sets (.rdata 0x{REMASTER_ANIMATION_SETS:X}) aren't vanilla's BATTLE.BIN's: "
                 "find the table again (RemixedSprites.AnimationSetsPattern)")
    return sets


def animation_set_name(entry: int) -> str:
    """An animation set (SPRITE_ANIMATIONS) for the report: its SHP/SEQ by name, and its hover flag and height when
    they're not a person's (0 and 0x24)."""
    shps = {0: "type1", 1: "type2", 2: "cyoko", 3: "mon", 4: "other", 6: "arute", 7: "kanzen"}
    seqs = {0: "type1", 1: "type3", 2: "cyoko", 3: "mon", 4: "other", 5: "ruka", 6: "arute", 7: "kanzen"}
    shp, seq, hover, height = entry & 0xFF, entry >> 8 & 0xFF, entry >> 16 & 0xFF, entry >> 24
    text = f"{shps.get(shp, shp)}/{seqs.get(seq, seq)}"
    return text + (f" (hover {hover}, height 0x{height:X})" if (hover, height) != (0, 0x24) else "")


def write_sprite_aliases(cfg: dict, battle_old: bytes, sprite_data: list[list[int]]) -> tuple[str, list[list[int]]]:
    """REMIXED_SPRITE_ALIASES for the DLL (RemixedSprites.InstallAliases), each unless a battle entry of the remaster's
    still uses its sprite set. The DLL leaves a graphic's animation set as it is, so each must already animate as the
    graphic it draws as does (with ReMixed's set where write_remixed_sprites gives one). Returns a line for the report
    and the data: [graphic, its texture now, the graphic it draws as]."""
    remasters = remaster_sprite_sets(cfg)
    sets = remaster_animation_sets(cfg, battle_old)
    for graphic, _, _, _, remixed_set in sprite_data:
        sets[graphic] = remixed_set
    data, held = [], []
    for graphic, (source, now) in sorted(REMIXED_SPRITE_ALIASES.items()):
        if graphic < 0x80 and remasters.get(graphic):
            held.append(f"0x{graphic:02X} (the remaster's entries "
                        + ", ".join(f"0x{e:03X}" for e in remasters[graphic]) + " use it)")
            continue
        if sets[graphic] != sets[source]:
            sys.exit(f"0x{graphic:02X} animates as {animation_set_name(sets[graphic])}, 0x{source:02X}, which it draws "
                     f"as, as {animation_set_name(sets[source])} (REMIXED_SPRITE_ALIASES)")
        data.append([graphic, now, source])
    given = {row[0] for row in sprite_data}
    line = ", ".join(f"0x{g:02X} as {'ReMixed' if s in given else 'the remaster'}'s 0x{s:02X}" for g, _, s in data)
    return (line or "none") + (f"; held back: {'; '.join(held)}" if held else ""), data


def write_dark_knight_shapes(cfg: dict) -> str:
    """CharShape (NXD 0xAE) rows for the remaster's Dark Knight graphics (DARK_KNIGHT_GRAPHICS), each a copy of the
    row of the graphic DARK_KNIGHT_SHAPES names. Returns a line for the report."""
    db = sqlite3.connect(nxd_sqlite(cfg, "charshape"))
    columns = [c for _, c, *_ in db.execute('pragma table_info("CharShape")') if c != "Key"]
    rows = {}
    for graphic in DARK_KNIGHT_GRAPHICS:
        like = DARK_KNIGHT_SHAPES[graphic]
        found = db.execute(f'select {", ".join(chr(34) + c + chr(34) for c in columns)} from "CharShape" where Key = ?',
                           (like,)).fetchone()
        if found is None:
            sys.exit(f"CharShape has no row 0x{like:02X} for 0x{graphic:02X} to copy (DARK_KNIGHT_SHAPES)")
        rows[graphic] = dict(zip(columns, found))
    keys = {k for (k,) in db.execute('select Key from "CharShape"')}
    db.close()
    if any(g in keys for g in DARK_KNIGHT_GRAPHICS) or max(keys) + 1 != min(DARK_KNIGHT_GRAPHICS):
        sys.exit("CharShape's keys aren't 0x00-0x9E any more: check DARK_KNIGHT_GRAPHICS")
    line = add_nxd_rows(cfg, "charshape", "CharShape", rows)
    return line + " (" + ", ".join(f"0x{g:02X} as 0x{DARK_KNIGHT_SHAPES[g]:02X}" for g in DARK_KNIGHT_GRAPHICS) + ")"


# ReMixed's characters' faces (asked 2026-11-12: their speech boxes and unit cards showed the remaster's faces of
# whoever had their graphic before, a male Squire's for Hirame). The remaster's speech boxes, unit cards and unit lists
# all draw a unit's face by its graphic: a fold (0x0F8338) turns copies into the graphic they copy (0x58 and 0x59 the
# male Squire's 0x60, 0x12 Malak's 0x1A, 0x98 the Dragon's 0x94), and the face is the texture
# ui/ffto/common/face/texture/wldface_<that, 3 digits>_<the unit's palette + 8>_uitx.tex with its parts file in
# textureparts/ (pack 0008; docs/REMASTER.md "Faces"). So each graphic the mod draws with ReMixed's sheet
# (write_remixed_sprites) gets a face of its own: its sheet's portrait at four times the size (tools/sprites.py
# face_atlas), as wldface_<the graphic>_08 to _15, numbers the remaster has no face under but 0x97's (its Hydra's,
# which the Lamia takes as it takes the Hydra's sprite); the DLL's fold gives each graphic its own number
# (Patches/RemixedFaces.cs). The mod loader serves the files as the game names them.
FACE_FOLDER = "ui/ffto/common/face"
FACE_TEMPLATE = "wldface_096_08_uitx"  # the male Squire's face, whose parts (cells, names) every face shares
FACE_STAGING = WORK / "build-faces"  # the faces as PNGs for FF16Tools (cleared each build)
FACE_CACHE = WORK / "cache" / "faces"  # the .tex FF16Tools made of each picture, by its hash
# The remaster's fold (0x0F8338) by graphic, where it isn't the graphic itself (read 2026-11-12). It files the story
# characters' faces under their second sprite sets: Agrias's (0x1E) under 0x34, Rafa's (0x29) under 0x19, Mustadio's
# (0x16) under 0x22, Celia's (0x2D) under 0x3A, Lede's (0x2E) under 0x3B; and a copy's under the graphic it copies.
REMASTER_FACE_FOLDS = {0x11: 0x17, 0x12: 0x1A, 0x14: 0x30, 0x16: 0x22, 0x1E: 0x34, 0x28: 0xC3, 0x29: 0x19, 0x2A: 0x2F,
                       0x2C: 0x30, 0x2D: 0x3A, 0x2E: 0x3B, 0x33: 0xAF, 0x35: 0x63, 0x36: 0x6B, 0x37: 0x6C, 0x38: 0x76,
                       0x39: 0x60, 0x3D: 0x64, 0x3F: 0x66, 0x42: 0x6C, 0x44: 0x6F, 0x46: 0x76, 0x47: 0x71, 0x57: 0x60,
                       0x58: 0x60, 0x59: 0x60, 0x5A: 0x60, 0x5B: 0x60, 0x5C: 0x60, 0x98: 0x94}
# ReMixed's graphics whose own number holds a face the remaster still shows for one of its characters (above), so their
# faces go under numbers the fold never gives (graphics it folds into others): Ashley's 0x34 under 0x14, ReMixed's 0x19
# under 0x28, its 0x3A under 0x2A and its 0x3B under 0x2C (the shared sprite slots, 2026-11-12). The remaster's
# man's Dark Knight, 0x9F, under 0x5B (a male Squire copy's, which the fold sends to 0x60), not its own 159, which
# GenericJobs ships a face as (DARK_KNIGHT_GRAPHICS).
FACE_NUMBERS = {0x34: 0x14, 0x19: 0x28, 0x3A: 0x2A, 0x3B: 0x2C, 0x9F: 0x5B}
# ReMixed changes four EVTFACE portraits in sets 0 and 1. The remaster's table still points their slots at a stock
# placeholder (0xB7), so message portraits using them draw the wrong person or none. Numbers 0x9B-0x9E are the four
# unused unit graphic slots, within the message palette-cap table; each gets the portrait ReMixed drew for that slot.
EVENT_FACE_ART = {(0, 7): 0x9B, (0, 8): 0x9C, (1, 6): 0x9D, (1, 8): 0x9E}


def face_number(graphic: int, given: set[int]) -> int:
    """The face number for a graphic the mod draws with ReMixed's sheet (FACE_NUMBERS, else its own), checked against
    the remaster's fold: every graphic the fold sends there must be one of `given` (the mod's own faces)."""
    face = FACE_NUMBERS.get(graphic, graphic)
    folded = {g for g in range(0xAA) if REMASTER_FACE_FOLDS.get(g, g) == face}
    if not folded <= given:
        sys.exit(f"graphic 0x{graphic:02X}'s face {face:03d} is where the remaster keeps the face of graphics "
                 + ", ".join(f"0x{g:02X}" for g in sorted(folded - given)) + " (FACE_NUMBERS)")
    return face


def write_remixed_faces(cfg: dict, sprite_data: list[list[int]]) -> tuple[str, list[list[int]]]:
    """A face for each graphic of write_remixed_sprites' with a portrait: its textures (RGBA, made by FF16Tools'
    img-conv from PNGs, one run per distinct picture, cached) and parts files, a pair per palette 8-15, under the
    graphic's own number. Returns a line for the report and the DLL's data: [graphic, its face]."""
    import hashlib
    tic = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced"
    extract = WORK / "remaster" / "extract" / "enhanced"
    template_file = extract / FACE_FOLDER / "textureparts" / f"{FACE_TEMPLATE}.utexpt"
    if not template_file.exists():
        ff16tools(cfg, "unpack", "-i", tic / "0008.pac", "-f", f"{FACE_FOLDER}/textureparts/{FACE_TEMPLATE}.utexpt",
                  "-o", extract)
    template = template_file.read_bytes()
    template_path, parts = sprites.texture_parts(template)
    if template_path != f"{FACE_FOLDER}/texture/{FACE_TEMPLATE}.tex":
        sys.exit(f"the remaster's face parts {FACE_TEMPLATE} name {template_path}: check its layout")
    _, _, atlas_w, atlas_h = parts["TexturePart"]
    exe = cfg.get("tools", {}).get("ff16tools")
    if not exe or not Path(exe).exists():
        sys.exit("Set tools.ff16tools in local.toml to FF16Tools.CLI.exe.")
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / FACE_FOLDER
    shutil.rmtree(out, ignore_errors=True)
    (out / "texture").mkdir(parents=True)
    (out / "textureparts").mkdir(parents=True)
    shutil.rmtree(FACE_STAGING, ignore_errors=True)
    FACE_STAGING.mkdir(parents=True)
    FACE_CACHE.mkdir(parents=True, exist_ok=True)
    data, made, none, recoloured = [], 0, [], []
    def put_face(face: int, palette: int, rgba: bytes) -> None:
        nonlocal made
        name = f"wldface_{face:03d}_{palette:02d}_uitx"
        cached = FACE_CACHE / f"{hashlib.sha1(rgba).hexdigest()}.tex"
        if not cached.exists():
            png = FACE_STAGING / f"{name}.png"
            sprites.write_rgba_png(png, rgba, atlas_w, atlas_h)
            subprocess.run([exe, "img-conv", "-i", png], check=True, capture_output=True)
            if not png.with_suffix(".tex").exists():
                sys.exit(f"FF16Tools made no texture of {png}")
            shutil.move(png.with_suffix(".tex"), cached)
            made += 1
        shutil.copy2(cached, out / "texture" / f"{name}.tex")
        (out / "textureparts" / f"{name}.utexpt").write_bytes(
            template.replace(FACE_TEMPLATE.encode(), name.encode()))

    given = {row[0] for row in sprite_data}
    with Disc(remixed_bin(cfg)) as patched, Disc(vanilla_bin(cfg)) as vanilla:
        for graphic in [row[0] for row in sprite_data]:
            source = REMIXED_SPRITE_SHEETS.get(graphic, graphic)
            faces = sprites.face_atlas(patched.read(patched.find(f"BATTLE/{source:02X}.SPR")), parts)
            if faces is None:
                none.append(f"0x{graphic:02X}")
                continue
            face = face_number(graphic, given)
            for palette, rgba in faces.items():
                put_face(face, palette, rgba)
            data.append([graphic, face])
        # A sheet with palettes ReMixed added (REMIXED_PALETTE_SHEETS): a face for each added portrait palette, under
        # the remaster's own face number for the graphic, whose other palettes' faces stay the remaster's.
        for graphic, palettes in sorted(REMIXED_PALETTE_SHEETS.items()):
            faces = sprites.face_atlas(patched.read(patched.find(f"BATTLE/{graphic:02X}.SPR")), parts)
            for palette in palettes:
                if palette >= 8 and faces is not None:
                    put_face(REMASTER_FACE_FOLDS.get(graphic, graphic), palette, faces[palette])
                    recoloured.append(f"0x{graphic:02X}'s {palette}")
        source_face = patched.read(patched.find("EVENT/EVTFACE.BIN"))
        stock_face = vanilla.read(vanilla.find("EVENT/EVTFACE.BIN"))
        changed = set()
        for face_set in range(8):
            for portrait_number in range(1, 9):
                base = face_set * 0x2000
                pixels = base + (portrait_number - 1) * 0x300
                palette = base + 0x1800 + (portrait_number - 1) * 0x20
                if (source_face[pixels:pixels + 0x300] != stock_face[pixels:pixels + 0x300]
                    or source_face[palette:palette + 0x20] != stock_face[palette:palette + 0x20]):
                    changed.add((face_set, portrait_number))
        if changed != set(EVENT_FACE_ART):
            sys.exit(f"ReMixed's changed EVTFACE portraits are {sorted(changed)}, expected {sorted(EVENT_FACE_ART)}")
        for (face_set, portrait_number), face in EVENT_FACE_ART.items():
            rgba = sprites.event_face_atlas(source_face, face_set, portrait_number, parts)
            for palette in sprites.FACE_PALETTES:
                put_face(face, palette, rgba)
    shutil.rmtree(FACE_STAGING, ignore_errors=True)
    names = ", ".join(f"0x{g:02X} as {face}" for g, face in data)
    return (f"{len(data)} faces from ReMixed's portraits ({names}), palettes 8-15, {made} textures made"
            + f"; {len(EVENT_FACE_ART)} EVTFACE portraits from ReMixed"
            + f"; faces for ReMixed's added palettes: {', '.join(recoloured)}"
            + (f"; no portrait: {', '.join(none)}" if none else "")), data


def write_map_looks(cfg: dict) -> str:
    """A look for each of NEW_LOOKS (maps.build_look): its mesh and pictures under bg/ in the mod, and its rows in
    RefinedBgTexture (a texture group's colour and lighting pictures), RefinedBgMeshNode (a row per part) and
    RefinedBgSystemTexture (LOOK_LIGHTS_FROM's, with its pictures), each over the game's placeholder rows for the map.
    Returns a line for the report."""
    import maps
    tic = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced"
    extract = WORK / "remaster" / "extract" / "enhanced"
    classic = extract / "fftpack" / "map"
    if not all((classic / f"map_map{n:03d}_gns.bin").exists() for n in NEW_LOOKS):
        ff16tools(cfg, "unpack-all", "-i", tic / "0002.pac", "-o", extract)
    db = sqlite3.connect(nxd_sqlite(cfg, "refinedbgsystemtexture"))
    lights = [dict(zip(("Key2", "Key3", "DLCFlags", "Comment", "Unknown8", "Unknown10"), row)) for row in db.execute(
        'select Key2, Key3, DLCFlags, Comment, Unknown8, Unknown10 from "RefinedBgSystemTexture" where Key = ? '
        'order by Key2, Key3', (LOOK_LIGHTS_FROM,))]
    db.close()
    light_pictures = sorted({name for row in lights for column in ("Unknown8", "Unknown10")
                             for name in json.loads(row[column] or "[]") if name})
    source = extract / "bg" / "textures" / f"{LOOK_LIGHTS_FROM:03d}"
    for name in light_pictures:
        if not (source / f"{name}.tga").exists():
            ff16tools(cfg, "unpack", "-i", tic / "0000.pac", "-f", f"bg/textures/{LOOK_LIGHTS_FROM:03d}/{name}.tga",
                      "-o", extract)
    bg = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "bg"
    pack_maps = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "fftpack" / "map"
    shutil.rmtree(pack_maps, ignore_errors=True)
    textures, nodes, systems, done = {}, {}, {}, []
    looks = {number: maps.build_look(classic, number) for number in NEW_LOOKS}
    with Disc(remixed_bin(cfg)) as disc:
        for number, (folder, texture_name, mesh_name, texture_file, mesh_file) in REMIXED_MAPS.items():
            texture = disc.read(disc.find(f"MAP/{folder}/{texture_name}"))
            mesh_data = disc.read(disc.find(f"MAP/{folder}/{mesh_name}"))
            mesh = maps.read_classic_mesh(mesh_data)
            looks[number] = maps.look_from(number, mesh.polygons, mesh.palettes, maps.texture_pixels(texture),
                                           mesh.tiles or [])
            pack_maps.mkdir(parents=True, exist_ok=True)
            (pack_maps / texture_file).write_bytes(texture)
            (pack_maps / mesh_file).write_bytes(fitted_mesh(mesh_data, (classic / mesh_file).stat().st_size))
            # Map 33's default state names its mesh as an override (kind 0x2F01), and the look didn't draw on the PC
            # with TUNNEL there; ReMixed's maps' (and VOLCANO's 119) are primary meshes (0x2E01), so the GNS says so.
            gns_name = f"map_map{number:03d}_gns.bin"
            gns = bytearray((classic / gns_name).read_bytes())
            for o in range(0, len(gns) - 19, 20):
                if gns[o + 2] == 0 and gns[o + 3] == 0 and gns[o + 4:o + 6] == b"/":
                    gns[o + 5] = 0x2E
            if gns != (classic / gns_name).read_bytes():
                (pack_maps / gns_name).write_bytes(gns)
        for number, (texture_path, mesh_path, changed) in REMIXED_MAP_EDITS.items():
            mesh = maps.read_classic_mesh(disc.read(disc.find(mesh_path)))
            looks[number] = maps.look_from(number, mesh.polygons, mesh.palettes,
                                           maps.texture_pixels(disc.read(disc.find(texture_path))), mesh.tiles or [])
            pack_maps.mkdir(parents=True, exist_ok=True)
            stem = mesh_path.rsplit(".", 1)[0]
            for n in changed:
                data, theirs = disc.read(disc.find(f"{stem}.{n}")), classic / f"map_map{number:03d}_{n}.bin"
                if len(data) != theirs.stat().st_size:
                    sys.exit(f"ReMixed's {stem}.{n} isn't the size of the remaster's {theirs.name}")
                (pack_maps / theirs.name).write_bytes(data)
                # The enhanced pack also has a second default mesh for map 75, with the stock heights
                # at a different offset. Keep its other sections intact and give it the same battle grid.
                alternate = classic / f"new_map_new_map{number:03d}_{n}.bin"
                if alternate.is_file():
                    original = theirs.read_bytes()
                    extra = bytearray(alternate.read_bytes())
                    src = struct.unpack_from("<I", original, maps.TERRAIN)[0]
                    dst = struct.unpack_from("<I", extra, maps.TERRAIN)[0]
                    grid_size = 2 + 2 * 256 * maps.TILE_BYTES
                    if not src or not dst or len(extra) < dst + grid_size or len(data) < src + grid_size or (
                            extra[dst:dst + grid_size] != original[src:src + grid_size]):
                        sys.exit(f"unexpected alternate terrain section in {alternate.name}")
                    extra[dst:dst + grid_size] = data[src:src + grid_size]
                    (pack_maps / alternate.name).write_bytes(extra)
    for number, look in looks.items():
        (bg / "meshes").mkdir(parents=True, exist_ok=True)
        (bg / "meshes" / f"map_{number:03d}_mesh.bin").write_bytes(look.mesh)
        folder = bg / "textures" / f"{number:03d}"
        shutil.rmtree(folder, ignore_errors=True)
        folder.mkdir(parents=True)
        for name, data in look.pictures.items():
            (folder / name).write_bytes(data)
        if number not in REMIXED_MAP_EDITS and number != YARDROW_MAP:  # keep the existing lighting where available
            for name in light_pictures:
                shutil.copy(source / f"{name}.tga", folder / f"{name}.tga")
            systems[number] = lights
        textures[number] = [{"Key2": group, "Key3": kind, "DLCFlags": 0, "Comment": None,
                             "Unknown8": json.dumps([picture]), "Unknown10": "[1]", "Unknown18": "[]", "Unknown20": "[]"}
                            for group, pair in sorted(look.groups.items()) for kind, picture in enumerate(pair)]
        nodes[number] = [{"Key2": 0, "Key3": slot, **(PANEL_NODE_ROW if slot == len(look.names) - 1 else NODE_ROW)}
                         for slot in range(len(look.names))]
        done.append(f"{number:03d} ({len(look.groups)} groups, {len(look.mesh):,} bytes)")
    # An edited map's animations and polygon conditions become the placeholder row (all zero) of a map without any.
    anims = {number: [{"Key2": 0, "Unknown0": 0, "Unknown4": 0, "Unknown8": 0}] for number in REMIXED_MAP_EDITS}
    polygons = {number: [{"Key2": 0, "DLCFlags": 0, "Unknown4": 0, "Unknown8": 0, "UnknownC": 0,
                          "RefinedBgMeshPolygonCondId": 0, "Unknown14": 0}] for number in REMIXED_MAP_EDITS}
    lines = [replace_nxd_rows(cfg, "refinedbgtexture", "RefinedBgTexture", textures),
             replace_nxd_rows(cfg, "refinedbgmeshnode", "RefinedBgMeshNode", nodes),
             replace_nxd_rows(cfg, "refinedbgsystemtexture", "RefinedBgSystemTexture", systems),
             replace_nxd_rows(cfg, "refinedbganim", "RefinedBgAnim", anims),
             replace_nxd_rows(cfg, "refinedbgmeshpolygon", "RefinedBgMeshPolygon", polygons)]
    return f"{', '.join(done)}; {'; '.join(lines)}"


def english_nxd(cfg: dict, table: str) -> Path:
    """The enhanced build's original English NXD for a name table, extracted once into work/."""
    extract = WORK / "remaster" / "extract" / "enhanced"
    nxd = extract / "nxd" / f"{table.lower()}.en.nxd"
    if not nxd.exists():
        pack = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced" / "0004.en.pac"
        ff16tools(cfg, "unpack", "-i", pack, "-f", f"nxd/{table.lower()}.en.nxd", "-o", extract)
    return nxd


DISPATCH_TRAINING = {
    96: ("Valor", "Permanently raises Brave by 3 (maximum 97)."),
    97: ("Detection", "Permanently lowers Brave by 3 (minimum 3)."),
    98: ("Theology", "Permanently raises Faith by 3 (maximum 97)."),
    99: ("Evolutionary", "Permanently lowers Faith by 3 (minimum 3)."),
    100: ("Training", "Awards job points through the standard dispatch report."),
    101: ("Retraining", "Awards additional job points through the standard dispatch report."),
    102: ("Transitioning", "Permanently changes the unit between male and female gender data."),
}


DISPATCH_PROPOSITIONS = [
    # kind, event, (first mission, count, story section) for each of its five sets
    ("Salvaging", 0xDF, ((1, 2, 4), (3, 3, 7), (6, 3, 9), (9, 3, 12), (12, 5, 16))),
    ("Mining", 0xE0, ((17, 3, 3), (20, 2, 6), (22, 3, 11), (25, 4, 12), (29, 5, 15))),
    ("Exploration", 0xE1, ((34, 2, 3), (36, 3, 8), (39, 3, 9), (42, 4, 14), (46, 4, 16))),
    ("Combat", 0xE2, ((50, 2, 5), (52, 2, 7), (54, 3, 11), (57, 4, 13), (61, 4, 16))),
    ("Investigation", 0xE3, ((65, 2, 4), (67, 2, 8), (69, 2, 10), (71, 2, 13), (73, 3, 15))),
    ("Odd Jobs", 0xE4, ((76, 3, 5), (79, 3, 6), (82, 3, 10), (85, 4, 13), (89, 4, 15))),
]
DISPATCH_JOBS = {
    195: ("Mining", "Work a repeatable mining shift for gil and job points."),
    196: ("Logging", "Work a repeatable logging shift for gil and job points."),
    197: ("Farming", "Work a repeatable farming shift for gil and job points."),
    198: ("Hunting", "Work a repeatable hunting shift for gil and job points."),
}
DISPATCH_CONTEST_MESSAGES = list(range(129, 135)) + list(range(145, 151))


def dispatch_event_lines(test_evt: bytes, event: int) -> list[str]:
    """ReMixed event messages as player-facing text, for the native Dispatch catalog."""
    block = test_evt[event * 0x2000:(event + 1) * 0x2000]
    at = int.from_bytes(block[:4], "little")
    if not 4 <= at < 0x2000:
        raise SystemExit(f"Dispatch event 0x{event:X} has no text")
    return [remixed_line(message)[1] for message in remixed_messages(block[at:])]


def write_dispatch_text(cfg: dict, test_evt: bytes) -> str:
    """Append ReMixed's Dispatch catalog without changing any native Errand row.

    The DLL virtualizes these rows only in the separate Dispatch Missions view. Keys 96-210 are dispatchable leaves;
    keys 211 onward are the native-control hierarchy (main menu and Proposition categories). Training keeps keys
    96-102 so an in-flight 0.9.6 course remains compatible.
    """
    work = WORK / "remaster" / "build" / "dispatch"
    shutil.rmtree(work, ignore_errors=True)
    (work / "in").mkdir(parents=True)
    shutil.copy(english_nxd(cfg, "Profit"), work / "in")
    shutil.copy(english_nxd(cfg, "ProfitClientName"), work / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", work / "in", "-o", work / "table.sqlite")
    db = sqlite3.connect(work / "table.sqlite")
    profit_columns = [column for _, column, *_ in db.execute('pragma table_info("Profit-en")')]
    client_columns = [column for _, column, *_ in db.execute('pragma table_info("ProfitClientName-en")')]
    if db.execute('select max(Key) from "Profit-en"').fetchone()[0] != 95:
        raise SystemExit("the game's Profit table no longer ends at row 95; Dispatch Missions needs review")
    client_template = dict(zip(client_columns, db.execute(
        'select * from "ProfitClientName-en" order by Key desc limit 1').fetchone()))
    next_client = db.execute('select max(Key) from "ProfitClientName-en"').fetchone()[0] + 1
    clients = {}
    for name in ("Dispatch Missions", "Propositions", "Jobs", "Special Training", "Contests"):
        client = dict(client_template)
        client.update(Key=next_client, Name=name)
        db.execute(f'insert into "ProfitClientName-en" ({",".join(client_columns)}) values '
                   f'({",".join("?" for _ in client_columns)})', [client[column] for column in client_columns])
        clients[name] = next_client
        next_client += 1
    template = dict(zip(profit_columns, db.execute('select * from "Profit-en" where Key = 91').fetchone()))

    catalog = []

    def append(key: int, kind: str, name: str, category: str, set_number: int, unlock: int,
               days: int, units: int, effect: int, title: str, posting: str, report: str,
               nav: str = "") -> None:
        row = dict(template)
        client = {"Menu": "Dispatch Missions", "Proposition": "Propositions", "Job": "Jobs",
                  "Training": "Special Training", "Contest": "Contests"}[kind]
        row.update(Key=key, Title=title, Unknown8=posting, UnknownC=report, Unknown10=report,
                   Unknown14=f"{name} Complete", Unknown18=report, Unknown1C=report,
                   ProfitClientNameId=clients[client])
        db.execute(f'insert into "Profit-en" ({",".join(profit_columns)}) values '
                   f'({",".join("?" for _ in profit_columns)})', [row[column] for column in profit_columns])
        catalog.append({"Key": key, "Kind": kind, "Name": name, "Category": category, "Set": set_number,
                        "UnlockStory": unlock, "Days": days, "MaxUnits": units, "Effect": effect, "Nav": nav})

    for key, (title, effect) in DISPATCH_TRAINING.items():
        posting = (f"Special Training - {title}\n\nSend one unit away for three days. {effect} "
                   "The native dispatch screen handles selection, cancellation, time away, and the final report.")
        report = f"The three-day {title} course is complete. {effect}"
        append(key, "Training", title, "Special Training", 0, 0, 3, 1, key - 96,
               f"Training - {title}", posting, report)

    report_lines = dispatch_event_lines(test_evt, 0xE8)
    proposition_key = 103
    for category, event, sets in DISPATCH_PROPOSITIONS:
        lines = dispatch_event_lines(test_evt, event)
        first_of_kind = sets[0][0]
        for set_number, (first, count, unlock) in enumerate(sets, 1):
            for mission in range(first, first + count):
                local = mission - first_of_kind
                title, posting, report = lines[9 + 3 * local:12 + 3 * local]
                # Event 0xE8's report list renames a few missions (for example Combat mission 63); the kind event's
                # title is what the player saw while choosing the Proposition, so keep it in the native catalog.
                append(proposition_key, "Proposition", title, category, set_number, unlock, 7, 3, mission,
                       f"Proposition - {category}: {title}",
                       f"Proposition - {category}, Set {set_number}\n\n{posting}", report)
                proposition_key += 1
    if proposition_key != 195:
        raise SystemExit(f"Dispatch catalog built {proposition_key - 103} Propositions, expected 92")

    for key, (name, description) in DISPATCH_JOBS.items():
        append(key, "Job", name, "Jobs", 0, 3, 7, 3, key - 195,
               f"Job - {name}", f"Repeatable Job - {name}\n\n{description}",
               f"The {name} job is complete. The dispatched units receive the standard gil and job-point report.")

    for contest, message in enumerate(DISPATCH_CONTEST_MESSAGES):
        name = report_lines[message - 1]
        append(199 + contest, "Contest", name, "Contests", 0, 5, 3, 3, contest,
               f"Contest - {name}",
               f"Monthly Contest - {name}\n\nSend up to three units. Their native dispatch report awards gil and job points.",
               f"The {name} is complete. The dispatched units receive their contest report.")

    # Native-control navigation. These rows never enter the sender: TavernStorage consumes their confirmation event,
    # replaces the same native list's backing rows, and keeps its keyboard/controller focus intact.
    navigation = [
        (211, "Propositions", "Choose one of the six kinds of Proposition.", "propositions"),
        (212, "Jobs", "Repeatable work for gil and job points.", "jobs"),
        (213, "Special Training", "Courses that permanently develop dispatched units.", "training"),
        (214, "Contests", "Enter the contest being held this month.", "contests"),
        (215, "Recall Units", "Review, report, or recall groups that are away.", "recall"),
    ]
    for key, name, description, nav in navigation:
        append(key, "Menu", name, "Dispatch Missions", 0, 0, 0, 0, 0, name,
               f"Dispatch Missions\n\n{description}", name, nav)
    for offset, (category, _event, _sets) in enumerate(DISPATCH_PROPOSITIONS):
        append(216 + offset, "Menu", category, "Propositions", 0, 0, 0, 0, 0, category,
               f"{category} Propositions\n\nChoose from the current set of {category} missions.", category,
               f"category:{category}")

    (MOD_DIR / "dispatch-missions.json").write_text(json.dumps(catalog, indent=2) + "\n", encoding="utf-8")
    db.commit()
    db.close()
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "nxd"
    out.mkdir(parents=True, exist_ok=True)
    built = []
    for table, name in (("Profit-en", "profit.en.nxd"), ("ProfitClientName-en", "profitclientname.en.nxd")):
        ff16tools(cfg, "sqlite-to-nxd", "-i", work / "table.sqlite", "-o", work / "out", "-t", table)
        source = next(p for p in (work / "out").rglob("*.nxd") if p.name.lower() == name)
        shutil.copy(source, out / name)
        built.append(name)
    counts = collections.Counter(entry["Kind"] for entry in catalog)
    return (f"{len(catalog)} additive entries ({counts['Proposition']} Propositions, {counts['Job']} Jobs, "
            f"{counts['Training']} training, {counts['Contest']} Contests, {counts['Menu']} navigation; "
            f"{', '.join(built)})")


REMASTER_ABILITY_RECORD = 20  # the exe's built-in ability attributes: the PS1's 14 bytes, then 6 more


def remaster_owned_abilities(cfg: dict, scus_old: bytes) -> dict[int, str]:
    """Slots the remaster uses for abilities of its own (Ramza's Chant at 0x2D): id -> name.

    Found in the exe's built-in attribute table, where such a slot has a
    different formula than the PS1's. Slots the remaster names "UNUSED" with no
    name (its leftover War of the Lions abilities) are free for ReMixed.
    """
    exe = (Path(cfg["target"]["tic_dir"]) / "FFT_enhanced.exe").read_bytes()
    rec = lambda a: scus_old[ABILITY_ATTRIBUTES + 14 * a : ABILITY_ATTRIBUTES + 14 * a + 14]  # noqa: E731
    gap = b".{%d}" % (REMASTER_ABILITY_RECORD - 14)
    hits = [m.start() for m in re.finditer(gap.join(re.escape(rec(a)) for a in range(1, 7)), exe, re.S)]
    if len(hits) != 1:
        sys.exit(f"Found the exe's ability attribute table {len(hits)} times (expected once).")
    table = hits[0] - REMASTER_ABILITY_RECORD
    work = WORK / "remaster" / "build" / "original-ability-names"
    shutil.rmtree(work, ignore_errors=True)
    (work / "in").mkdir(parents=True)
    shutil.copy(english_nxd(cfg, "Ability"), work / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", work / "in", "-o", work / "names.sqlite")
    db = sqlite3.connect(work / "names.sqlite")
    names = dict(db.execute('select Key, Name from "Ability-en"'))
    db.close()
    return {a: names[a] for a in range(0x170)
            if exe[table + REMASTER_ABILITY_RECORD * a + 8] != rec(a)[8] and names.get(a)}


# The localized Ability table's JP cost: a u16 the game reads at row +0x40 (the cost getter, 0x2CC0AC, for the learn
# routine 0x2780B0), split in two byte columns. FF16Tools' layouts name them JpCost1 (low) and JpCost2 (high) since
# 2025-10-11, Unknown40 and Unknown41 before. The exe's own table (0x787F80, the loader's AbilityData) holds a copy that
# nothing reads for the price.
JP_COST_COLUMNS = (("JpCost1", "JpCost2"), ("Unknown40", "Unknown41"))


def jp_costs(scus_old: bytes, scus_new: bytes, skip: set[int]) -> dict[int, int]:
    """ReMixed's changed JP costs, the u16 its ability records start with: ability -> cost."""
    costs = {}
    for a in range(512):
        old, new = u16(scus_old, ABILITIES + 8 * a), u16(scus_new, ABILITIES + 8 * a)
        if new != old and a not in skip:
            costs[a] = new
    return costs


def write_jp_costs(db: sqlite3.Connection, table: str, costs: dict[int, int]) -> int:
    """Set the JP costs in a converted Ability table (whichever column names this FF16Tools uses). Returns the count."""
    columns = {row[1] for row in db.execute(f'pragma table_info("{table}")')}
    pair = next((pair for pair in JP_COST_COLUMNS if set(pair) <= columns), None)
    if pair is None:
        sys.exit(f"The {table} table has no JP cost columns this build knows ({', '.join(sorted(columns))}).")
    low, high = pair
    for key, cost in costs.items():
        db.execute(f'update "{table}" set "{low}" = ?, "{high}" = ? where Key = ?', (cost & 0xFF, cost >> 8, key))
    return len(costs)


def fill_placeholder_abilities(db: sqlite3.Connection, table: str, handed_out: dict[int, set[int]],
                               all_names: dict[int, str], renamed: dict[int, str], skip: set[int]) -> dict[int, str]:
    """Name, describe and give an icon to abilities handed out in unused remaster slots (PLACEHOLDER_DESCRIPTIONS)."""
    row = {k: (icon, name, text) for k, icon, name, text
           in db.execute(f'select Key, IconId, Name, Description from "{table}"')}
    filled: dict[int, str] = {}
    for ability, siblings in sorted(handed_out.items()):
        if ability in skip or ability not in row:
            continue
        icon, name, text = row[ability]
        named = bool(name) and not PLACEHOLDER_TEXT.match(name)
        described = bool(text) and not PLACEHOLDER_TEXT.match(text)
        if named and described:
            continue  # the remaster's own ability (an enemy-only one may have no icon, and needs none)
        title = name if named else renamed.get(ability) or all_names.get(ability)
        if not title:
            continue  # no ReMixed name to give it (Presentation.unnamed)
        kind = next((i for lo, hi, i in RSM_ICONS if lo <= ability <= hi), None)
        # An action ability takes the icon most of its skillset's others have; a reaction, support or movement its kind's.
        icons = [row[s][0] for s in siblings if s != ability and s in row and row[s][0]]
        new_icon = icon or kind or (max(sorted(set(icons)), key=icons.count) if icons else 0)
        description = text if described else PLACEHOLDER_DESCRIPTIONS.get(ability) or PLACEHOLDER_DESCRIPTION
        db.execute(f'update "{table}" set Name = ?, Description = ?, IconId = ? where Key = ?',
                   (title, description, new_icon, ability))
        filled[ability] = title
    return filled


# The remaster's icons for special commands (JobCommand's IconId, `job_comm/jc_NNN`): 21 a special job's (Mettle, Holy
# Sword, Spellcraft), 22 a boss monster's (Bio, Dark Arts, Demon Magicks).
SPECIAL_COMMAND_ICONS = (21, 22)


def fill_command_icons(db: sqlite3.Connection, commands: str, jobs: str,
                       job_commands: dict[int, int]) -> dict[int, int]:
    """Icons for the skillsets the port names in rows the remaster left without one (ReMixed's own: Spell Strike,
    Witchcraft, Blue Magic, its bosses' lists...). A unit card's skillset row shows its icon and its name only for an
    icon above 0 (0x29AF64), so a skillset with none was a blank row: Valmafra's Spell Strike, seen on the Deck
    (2026-11-25). Each takes the special icon (SPECIAL_COMMAND_ICONS) of the command its jobs had in the remaster (the
    job table's link, which mirrors the remaster's own JobData), else a special job's. `job_commands` is the port's
    job -> command. Returns skillset -> the icon given."""
    icon = dict(db.execute(f'select Key, IconId from "{commands}"').fetchall())
    stock = dict(db.execute(f'select Key, "jobcommand+Id" from "{jobs}"').fetchall())
    given: dict[int, int] = {}
    for key, name in db.execute(f'select Key, Name from "{commands}" where IconId = 0').fetchall():
        if not name:
            continue
        theirs = {icon.get(stock.get(job)) for job, command in job_commands.items() if command == key}
        theirs &= set(SPECIAL_COMMAND_ICONS)
        given[key] = theirs.pop() if len(theirs) == 1 else SPECIAL_COMMAND_ICONS[0]
        db.execute(f'update "{commands}" set IconId = ? where Key = ?', (given[key], key))
    return given


def write_descriptions(db: sqlite3.Connection, table: str, name: str, changed: dict[int, str], every: dict[int, str],
                       skip: set[int], names: list[tuple[str, str]]) -> int:
    """ReMixed's descriptions (remixed_descriptions) into a name table: the ones it changed, and its own where the
    remaster's is a placeholder or empty, or the stand-in fill_placeholder_abilities gave a slot. Returns how many
    were written."""
    current = dict(db.execute(f'select Key, Description from "{table}"').fetchall())
    moved, kept = HELP_MOVED.get(name, {}), HELP_KEPT.get(name, set())
    written = 0
    for k, text in every.items():
        key = moved.get(k, k)
        if k in skip or key in kept or key not in current:
            continue
        stand_in = current[key] == PLACEHOLDER_DESCRIPTION or current[key] in PLACEHOLDER_DESCRIPTIONS.values()
        if k in changed or not current[key] or PLACEHOLDER_TEXT.match(current[key]) or stand_in:
            db.execute(f'update "{table}" set Description = ? where Key = ?', (with_names(text, names), key))
            written += 1
    return written


def build_name_tables(cfg: dict, renames: dict[str, dict[int, str]], skip: dict[str, set[int]],
                      costs: dict[int, int], handed_out: dict[int, set[int]], all_names: dict[int, str],
                      descriptions: tuple[dict[str, dict[int, str]], dict[str, dict[int, str]]] | None = None,
                      names_old: dict[str, dict[int, str]] | None = None,
                      job_commands: dict[int, int] | None = None) -> dict:
    """Write ReMixed's renames, its descriptions and its JP costs into the enhanced build's English name tables, and
    icons for the skillsets it names that have none (fill_command_icons; `job_commands`, the port's job -> command).
    Returns counts."""
    work = WORK / "remaster" / "build" / "names"
    shutil.rmtree(work, ignore_errors=True)
    (work / "in").mkdir(parents=True)
    for table, _, _ in NAME_LISTS.values():
        shutil.copy(english_nxd(cfg, table), work / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", work / "in", "-o", work / "names.sqlite")
    db = sqlite3.connect(work / "names.sqlite")
    counts = {}
    # The names ReMixed's descriptions use that the remaster writes otherwise (description_names), from every list:
    # an ability's text may name a skillset or a job.
    theirs = {name: dict(db.execute(f'select Key, Name from "{table}-en"').fetchall())
              for name, (table, _, _) in NAME_LISTS.items()}
    names = description_names([((names_old or {}).get(name, {}), theirs[name], renames[name]) for name in NAME_LISTS])
    changed, every = descriptions or ({}, {})
    for name, (table, _, _) in NAME_LISTS.items():
        rows = {k: v for k, v in renames[name].items() if k not in skip.get(name, set())}
        for key, value in rows.items():
            db.execute(f'update "{table}-en" set Name = ? where Key = ?', (value, key))
        if name == "abilities":
            counts["placeholders"] = fill_placeholder_abilities(db, f"{table}-en", handed_out, all_names, rows,
                                                                skip.get(name, set()))
        counts[f"{name} described"] = write_descriptions(db, f"{table}-en", name, changed.get(name, {}),
                                                         every.get(name, {}), skip.get(name, set()), names)
        counts[name] = len(rows)
    jp = write_jp_costs(db, f"{NAME_LISTS['abilities'][0]}-en", costs)
    counts["Dark Knight's rows"] = write_dark_knight_rows(db)
    counts["skillset icons"] = len(fill_command_icons(db, f"{NAME_LISTS['skillsets'][0]}-en",
                                                      f"{NAME_LISTS['jobs'][0]}-en", job_commands or {}))
    counts["jobs"] = counts["jobs"] or 1  # the job and command tables carry the Dark Knight's rows either way
    counts["skillsets"] = counts["skillsets"] or 1
    db.commit()
    db.close()
    out_dir = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "nxd"
    out_dir.mkdir(parents=True, exist_ok=True)
    for name, (table, _, _) in NAME_LISTS.items():
        if not counts[name] and not counts[f"{name} described"] and not (name == "abilities" and jp):
            continue
        ff16tools(cfg, "sqlite-to-nxd", "-i", work / "names.sqlite", "-o", work / "out", "-t", f"{table}-en")
        built = next(p for p in (work / "out").rglob("*.nxd") if p.name.lower() == f"{table.lower()}.en.nxd")
        shutil.copy(built, out_dir / f"{table.lower()}.en.nxd")
    counts["JP costs"] = jp
    return counts


# ReMixed's Dark Knight in the job menu (Patches/JobMenuPages.cs, from cipherxof's GenericJobs, which was needed for it
# before 0.9.22). The remaster's own Dark Knight rows are bare: job 0xA0 has no name, command, icon or help in the job
# table, its command (Darkness, 224) no icon, its unlock row in generaljob.nxd (20) no JP curve or requirements, and
# the job help (UiJobAbilityHelp) has no row for it. Here they are ReMixed's: its name, Darkness, an icon drawn from its
# own sheet (tools/job_icons.py; number 160, past the remaster's 1-24, which the DLL lets through), the generic jobs' JP
# curve, ReMixed's requirements (Knight 8, Black Mage 8; its row 17) and a help page in the remaster's style from the
# port's names. Its job art (jobtype) stays the remaster's none: the job menu hides a job's art past Mime anyway.
DARK_KNIGHT_JOB_ROW = 0xA0
DARK_KNIGHT_COMMAND = 0xE0
DARK_KNIGHT_ICON = 160
DARK_KNIGHT_HELP = 35  # the job help's first free row (the remaster's are 0-34)
DARK_KNIGHT_SORT = 90  # Job's Unknown15, 200-390 by tens for the generic jobs; 90 as GenericJobs had it on the Deck
DARK_KNIGHT_UNLOCK_ROW = 20  # generaljob.nxd's rows by job index; the Dark Knight's is 20
DARK_KNIGHT_REQUIREMENTS = ([2, 6], [8, 8])  # ReMixed's row 17: Knight (index 2) 8, Black Mage (index 6) 8
DARK_KNIGHT_EQUIPMENT = {  # ReMixed's job 0x5B row (its Dark Knight), as the remaster's help lists equipment
    "Hands": "Sword/Knight's Sword/Katana/Axe/Flail/Shield", "Head": "Helm", "Body": "Armor/Clothing/Robe",
    "Accessories": "Shoes/Cloak/Armguard/Armlet/Ring"}
DARK_KNIGHT_SHOWN = [(105, "Action Ability", 0xA5), (105, "Action Ability", 0xB8), (107, "Support Ability", 0x1D9)]


def write_dark_knight_rows(db: sqlite3.Connection) -> int:
    """The Dark Knight's rows in the job and command name tables (their English database): its name if the remaster
    has none, Darkness, its icon, its help row and its sort number. Returns the fields changed."""
    job = db.execute('select Name, "jobcommand+Id", TexturePartsIndex, "uijobabilityhelp+Id", Unknown15 '
                     'from "Job-en" where Key = ?', (DARK_KNIGHT_JOB_ROW,)).fetchone()
    if job is None:
        sys.exit("the remaster's job table has no Dark Knight row (0xA0); see Patches/JobMenuPages.cs")
    name, command, icon, help_row, sort = job
    db.execute('update "Job-en" set Name = ?, "jobcommand+Id" = ?, TexturePartsIndex = ?, "uijobabilityhelp+Id" = ?, '
               'Unknown15 = ? where Key = ?', (name or "Dark Knight", DARK_KNIGHT_COMMAND, DARK_KNIGHT_ICON,
                                               DARK_KNIGHT_HELP, DARK_KNIGHT_SORT, DARK_KNIGHT_JOB_ROW))
    db.execute('update "JobCommand-en" set IconId = ? where Key = ?', (DARK_KNIGHT_ICON, DARK_KNIGHT_COMMAND))
    return sum([not name, command != DARK_KNIGHT_COMMAND, icon != DARK_KNIGHT_ICON, help_row != DARK_KNIGHT_HELP,
                sort != DARK_KNIGHT_SORT, 1])


def dark_knight_help(abilities: dict[int, tuple[str, str]]) -> str:
    """The Dark Knight's job help in the remaster's layout, from the port's ability names and descriptions."""
    def head(title: str) -> str:
        return f"<color=abilityhint_cap_01><icon=104>{title}</color>"
    lines = [head("Job Overview"), "",
             " - A knight trained in the dark arts, who draws on unholy power.",
             " - The job command Darkness enables blade techniques that drain HP or MP, and",
             "   others that sacrifice the user's own HP to strike harder.",
             " - Opens once Knight and Black Mage have both reached job level 8.",
             " - Possesses high physical attack.", "", head("Usable Equipment"), ""]
    lines += [f" - {slot}: {kinds}" for slot, kinds in DARK_KNIGHT_EQUIPMENT.items()]
    lines += ["", head("Select Abilities")]
    for icon, kind, ability in DARK_KNIGHT_SHOWN:
        name, description = abilities[ability]
        description = re.sub(r"\s*Requires:.*$", "", description or "").strip()
        lines += ["", f" <icon={icon}>{kind}: {name}", f"  - {description}"]
    return "\n".join(lines)


def write_dark_knight_page(cfg: dict) -> str:
    """The Dark Knight's unlock row (generaljob.nxd), its job help (UiJobAbilityHelp) and its two icons. Returns a line
    for the report. Run after build_name_tables, whose ability names the help uses."""
    import hashlib
    import job_icons
    names_db = sqlite3.connect(WORK / "remaster" / "build" / "names" / "names.sqlite")
    abilities = {k: (n, d) for k, n, d in names_db.execute(
        f'select Key, Name, Description from "{NAME_LISTS["abilities"][0]}-en"')}
    names_db.close()

    def unlock(db: sqlite3.Connection) -> str:
        curve = db.execute('select RequiredJobExp from "GeneralJob" where Key = 0').fetchone()[0]
        ids, levels = DARK_KNIGHT_REQUIREMENTS
        db.execute('update "GeneralJob" set RequiredJobExp = ?, RequiredJobIds = ?, RequiredJobLevels = ? where Key = ?',
                   (curve, json.dumps(ids), json.dumps(levels), DARK_KNIGHT_UNLOCK_ROW))
        return f", row {DARK_KNIGHT_UNLOCK_ROW}: the JP curve {curve}, Knight 8 and Black Mage 8"
    lines = [add_nxd_rows(cfg, "generaljob", "GeneralJob", {}, edit=unlock),
             add_nxd_rows(cfg, "uijobabilityhelp.en", "UiJobAbilityHelp-en",
                          {DARK_KNIGHT_HELP: {"DLCFlags": 0, "Comment": None, "Text": dark_knight_help(abilities)}},
                          pack_name="0004.en.pac")]

    # The icons: drawn, made textures by FF16Tools (cached by picture), with the remaster's Mime's parts renamed.
    tic = Path(cfg["target"]["tic_dir"]) / "data" / "enhanced"
    extract = WORK / "remaster" / "extract" / "enhanced"
    exe = cfg["tools"]["ff16tools"]
    cache = WORK / "cache" / "job-icons"
    cache.mkdir(parents=True, exist_ok=True)
    with Disc(remixed_bin(cfg)) as patched:
        icons = job_icons.dark_knight_icons(patched.read(patched.find(f"BATTLE/{job_icons.SHEET:02X}.SPR")))
    for kind, folder in (("j", "job"), ("jc", "job_comm")):
        size, rgba = icons[kind]
        template_name = f"{kind}_020_uitx"
        name = f"{kind}_{DARK_KNIGHT_ICON:03d}_uitx"
        parts_file = extract / "ui" / "ffto" / "icon" / folder / "textureparts" / f"{template_name}.utexpt"
        if not parts_file.exists():
            ff16tools(cfg, "unpack", "-i", tic / "0008.pac", "-f",
                      f"ui/ffto/icon/{folder}/textureparts/{template_name}.utexpt", "-o", extract)
        parts = parts_file.read_bytes()
        if sprites.texture_parts(parts)[0] != f"ui/ffto/icon/{folder}/texture/{template_name}.tex":
            sys.exit(f"the remaster's {template_name} parts name another texture: check its layout")
        cached = cache / f"{hashlib.sha1(rgba).hexdigest()}.tex"
        if not cached.exists():
            png = cache / f"{name}.png"
            sprites.write_rgba_png(png, rgba, size, size)
            subprocess.run([exe, "img-conv", "-i", png], check=True, capture_output=True)
            if not png.with_suffix(".tex").exists():
                sys.exit(f"FF16Tools made no texture of {png}")
            shutil.move(png.with_suffix(".tex"), cached)
            png.unlink()
        out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "ui" / "ffto" / "icon" / folder
        (out / "texture").mkdir(parents=True, exist_ok=True)
        (out / "textureparts").mkdir(parents=True, exist_ok=True)
        shutil.copy2(cached, out / "texture" / f"{name}.tex")
        (out / "textureparts" / f"{name}.utexpt").write_bytes(parts.replace(template_name.encode(), name.encode()))
    lines.append(f"icons {DARK_KNIGHT_ICON} (job menu and Darkness) from ReMixed's {job_icons.SHEET:02X}.SPR")
    return "; ".join(lines)


# ReMixed's names for the second Javelin (106) and the second Escutcheon (143): the remaster names both of each alike
# (99 and 106 "Javelin", 128 and 143 "Escutcheon"). Of ReMixed's ten item renames the remaster has the other eight
# already, in its own spelling (the Glacial and Blaze Guns the right way round, Lamia's Harp, the poles).
ITEM_RENAMES = {106: "Javelin II", 143: "Escutcheon II"}


def write_item_names(cfg: dict) -> str:
    """ITEM_RENAMES into the enhanced build's English item table. Returns a line for the report."""
    work = WORK / "remaster" / "build" / "items"
    shutil.rmtree(work, ignore_errors=True)
    (work / "in").mkdir(parents=True)
    shutil.copy(english_nxd(cfg, "Item"), work / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", work / "in", "-o", work / "items.sqlite")
    db = sqlite3.connect(work / "items.sqlite")
    for key, name in ITEM_RENAMES.items():
        db.execute('update "Item-en" set Name = ? where Key = ?', (name, key))
    db.commit()
    db.close()
    ff16tools(cfg, "sqlite-to-nxd", "-i", work / "items.sqlite", "-o", work / "out", "-t", "Item-en")
    built = next(p for p in (work / "out").rglob("*.nxd") if p.name.lower() == "item.en.nxd")
    out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "nxd"
    out.mkdir(parents=True, exist_ok=True)
    shutil.copy(built, out / "item.en.nxd")
    return ", ".join(f"{key} {name}" for key, name in ITEM_RENAMES.items())


def hold_back_abilities(spec: TableSpec, rid: int, fields: Fields, held: dict[int, str],
                        vanilla_homes: dict[int, set[str]], skipped: list) -> Fields:
    """Empty ability slots that hand out a code-dependent ability somewhere vanilla doesn't."""
    home = f"{spec.xml} {rid}"
    out = dict(fields)
    emptied = False
    for f, v in fields.items():
        if ABILITY_ID_FIELDS.match(f) and v in held and home not in vanilla_homes.get(v, set()):
            out[f] = 0
            emptied = True
            skipped.append((rid, f, f"it would hand out ability 0x{v:03X}: {held[v]}"))
    if spec.xml == "JobCommandData" and emptied:
        # Close the gaps so the menu doesn't start with or contain blank lines.
        # (Monster skillsets keep their order: slot 4 is the Beastmaster one.)
        for prefix, count in (("AbilityId", 16), ("ReactionSupportMovementId", 6)):
            ids = [out[f"{prefix}{k}"] for k in range(1, count + 1)]
            ids = [a for a in ids if a] + [0] * ids.count(0)
            out.update({f"{prefix}{k}": a for k, a in enumerate(ids, 1)})
    if spec.xml == "JobCommandData":  # keep the extended-id flags in step with the ids
        out["ExtendAbilityIdFlagBits"] = frozenset(
            name for k, name in enumerate(EXTENDED_ABILITY_BITS)
            if out[f"AbilityId{(8 - k) if k < 8 else (24 - k)}"] >= 0x100)
        out["ExtendReactionSupportMovementIdFlagBits"] = frozenset(
            f"ExtendRSMId{k}" for k in range(1, 7) if out[f"ReactionSupportMovementId{k}"] >= 0x100)
    return out


def keep_remaster_jobs(spec: TableSpec, result: TableResult) -> None:
    """Leave out ReMixed's changes to the job rows the remaster's story still uses as vanilla's (REMASTER_KEPT_JOBS)."""
    if spec.xml != "JobData":
        return
    for rid, reason in REMASTER_KEPT_JOBS.items():
        for f in result.entries.pop(rid, {}):
            result.skipped.append((rid, f, reason))
        result.conflicts = [c for c in result.conflicts if c[0] != rid]


def relocate_dark_knight(spec: TableSpec, result: TableResult, scus_old: bytes, scus_new: bytes,
                         template: dict[int, dict], held: dict[int, str], vanilla_homes: dict[int, set[str]]) -> None:
    """Move ReMixed's Dark Knight and man's Bard rows onto the remaster's Dark Knight and Bard (DARK_KNIGHT)."""
    def row(data: bytes, i: int) -> Fields:
        start = spec.offset + i * spec.size
        return spec.decode(data[start : start + spec.size])

    def drop(rid: int, rewritten: bool = False, reason: str = DARK_KNIGHT_REASON) -> None:
        for f in result.entries.pop(rid, {}):
            if not rewritten:  # a row that gets moved content isn't left out
                result.skipped.append((rid, f, reason))
        result.conflicts = [c for c in result.conflicts if c[0] != rid]

    if spec.xml == "JobData":
        source, target = DARK_KNIGHT["job"]
        for rid in DARK_KNIGHT_UNUSED["JobData"]:
            drop(rid)
        drop(source, rewritten=True)
        # The Dark Knight: ReMixed's values for every stat, status and element field, its gear plus the remaster's
        # own fell swords; the remaster's skillset (224), innates and graphics.
        knight, base = row(scus_new, source), template.get(target, {})
        fields: Fields = {}
        for f, v in knight.items():
            if f == "JobCommandId" or f.startswith(("Monster", "InnateAbility")):
                continue
            if f == "EquippableItems" and f in base:
                v = v | (flag_set(base[f]) & EQUIPMENT_REMASTER_ONLY)
            if f not in base or not same(v, base[f]):
                fields[f] = v
        if fields:
            result.entries[target] = fields
        # The Bard: what ReMixed's man's Bard changes from vanilla's Bard, on the remaster's Bard.
        source, target = BARD["job"]
        vanilla, bard, base = row(scus_old, target), row(scus_new, source), template.get(target, {})
        fields = {}
        for f in vanilla:
            if f == "JobCommandId" or f.startswith("Monster") or same(vanilla[f], bard[f]):
                continue
            v = bard[f]
            if isinstance(v, frozenset) and f in base:
                v = (flag_set(base[f]) - (vanilla[f] - v)) | (v - vanilla[f])
            fields[f] = v
        if fields:
            result.entries[target] = fields
    elif spec.xml == "JobCommandData":
        for rid in DARK_KNIGHT_UNUSED["JobCommandData"]:
            drop(rid)
        drop(DARK_KNIGHT["skillset"][0], rewritten=True)
        for source, target in (DARK_KNIGHT["skillset"], BARD["skillset"]):
            result.entries[target] = hold_back_abilities(spec, target, row(scus_new, source), held, vanilla_homes,
                                                         result.skipped)
    elif spec.xml == "JobNeedLevelData":
        source, target = DARK_KNIGHT["requirement"]
        drop(source, rewritten=True)  # the Bard keeps the remaster's requirements
        result.entries[target] = row(scus_new, source)


# ---------------------------------------------------------------- battle units (ENTD)

ENTD_FILES = [f"BATTLE/ENTD{k}.ENT" for k in range(1, 5)]
UNIT_SIZE = 40
# Unit fields ported from ReMixed: the unit's build. Everything else (who
# appears, sprite, name, flags, team and presence, position and facing, unit
# id, AI targets) stays vanilla, because it's tied to ReMixed's event scripts
# and code, which aren't ported yet.
UNIT_BUILD_FIELDS = [3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 28, 29, 30, 31]
UNIT_RSM_FIELDS = (12, 14, 16)


UNITS_PER_EVENT = 16


ABILITY_RANDOM = 0x1FE  # battle-unit ability value meaning "pick at random"


@dataclass
class EntdResult:
    files: list[bytes]  # merged ENTD1..4
    random_battles: int = 0  # random battles ported whole
    events_same: int = 0  # other entries whose cast is the same in vanilla and ReMixed
    events_changed: int = 0  # of those, entries with units given ReMixed's build
    events_reorganized: int = 0  # entries whose cast differs (ReMixed moved or rewrote them): deferred
    events_moved: int = 0  # of those, story entries whose exact cast ReMixed moved to one other entry
    moved_ported: int = 0  # units of those given ReMixed's build from the entry it moved them to
    battles_changed: int = 0  # story battles whose cast ReMixed changed and moved, matched by cast_score
    changed_ported: int = 0  # units of those given ReMixed's build
    ported: int = 0  # units given ReMixed's build (outside random battles)
    held_rsm: int = 0  # abilities not ported because they need ReMixed's code
    # Entry -> its 16 units' ReMixed unit presets (ENTD_PRESET), where a unit given ReMixed's build has one there: the
    # battle data can't carry the byte, so the remaster's scenarios that load the entry list them (UNIT_PRESETS_FILE).
    presets: dict[int, bytes] = field(default_factory=dict)
    sprites_moved: int = 0  # the remaster's units moved off a slot ReMixed gave someone else (REMASTER_MOVED_SPRITE_SETS)


def is_random_battle(index: int, names: Names, old_units: list[bytes], new_units: list[bytes]) -> bool:
    """A per-map random battle: named "<map> <n>", only generic units, same characters in both."""
    if not re.search(r" \d$", names.get("events", index)):
        return False
    old_sprites = sorted(x[0] for x in old_units if x[0])
    new_sprites = sorted(x[0] for x in new_units if x[0])
    return old_sprites == new_sprites and all(s >= 0x80 for s in old_sprites)


def unit_key(unit: bytes) -> tuple[int, int, int]:
    return unit[0], unit[25], unit[26]  # sprite set, X, Y


STORY_ENTRIES = range(0x100, 0x200)  # battle entries of the story (the random battles and the rest come first)
MIN_MOVED_CAST = 3  # a smaller cast could match another scene's by chance
# From a moved entry, units take ReMixed's build without the palette: ReMixed sets palette 0 on its story units
# (its tint code colours them, the presets, not ported), which in the remaster would show enemies in the player's
# colours. A special character's job 0 (ReMixed's cutscene actors, Larg and Draclau among them; its loader copies
# the job as it is, SCUS 0x8005B358) keeps vanilla's job.
MOVED_BUILD_FIELDS = [i for i in UNIT_BUILD_FIELDS if i != 23]
ENTD_JOB = 10


def cast_key(units: list[bytes]) -> tuple:
    return tuple(sorted(unit_key(x) for x in units if x[0]))


def moved_entries(old: list[list[bytes]], new: list[list[bytes]]) -> dict[int, int]:
    """Story entries whose exact cast (every unit's sprite and position) ReMixed moved to one other story entry:
    vanilla entry -> ReMixed entry. One to one only, and casts of at least MIN_MOVED_CAST units with no two units
    alike, so a match can't be a chance one between scenes."""
    where: dict[tuple, list[int]] = {}
    for f in STORY_ENTRIES:
        key = cast_key(new[f])
        if len(key) >= MIN_MOVED_CAST and len(set(key)) == len(key):
            where.setdefault(key, []).append(f)
    found: dict[int, int] = {}
    for e in STORY_ENTRIES:
        key = cast_key(old[e])
        hits = [f for f in where.get(key, []) if f != e]
        if old[e] != new[e] and len(hits) == 1 and cast_key(new[e]) != key:
            found[e] = hits[0]
    # One ReMixed entry for two vanilla ones only when both have that exact cast (a scene and its battle,
    # which ReMixed files as one).
    return found


BATTLE_ENTRIES = range(0x180, 0x200)  # the story's battles (the entries before them are its scenes)
# Story battles paired by reading their casts, where cast_score is too close to call: vanilla entry -> ReMixed entry.
REVIEWED_BATTLES = {
    0x1AE: 0x14B,  # Yuguo Woods: Rafa and the Ghoul, Gust and Revenant (ReMixed has mages for the other monsters)
    0x1C5: 0x167,  # Poeskas Lake: the two Revenants (ReMixed has archers, an Oracle and a Summoner for the others)
    0x1CE: 0x177,  # Chapel of St Murond Temple: Vormav, the two Zalbags and the demons (ReMixed adds six units)
}
CHANGED_SCORE, CHANGED_GAP = 0.6, 0.2


def character(names: Names, sprite: int) -> object:
    """Who a unit is: a special character by name, so its sprite sets are one (ReMixed uses Mustadio's 0x16 where
    vanilla has his 0x22, Agrias' 0x1E for 0x34, Gafgarion's 0x17 for 0x11); a generic by its sprite set."""
    if sprite >= 0x80:
        return sprite
    label = names.label("sprite_sets", sprite)
    name = re.sub(r"\s*\([0-9A-Fa-f]+\)$", "", label)
    return name if name and not re.fullmatch(r"[0-9A-Fa-f]+", name) else sprite


def cast_score(a: list[bytes], b: list[bytes], who=lambda s: s) -> float:
    """How alike two entries' casts are: the special characters (0.4, by `who`), the generics by sprite and job
    (0.25), the tiles (0.25) and the teams (0.1), each as a Jaccard overlap."""
    def jaccard(x: list, y: list) -> float:
        cx, cy = collections.Counter(x), collections.Counter(y)
        union = sum((cx | cy).values())
        return sum((cx & cy).values()) / union if union else 1.0
    ua, ub = [u for u in a if u[0]], [u for u in b if u[0]]
    return (0.4 * jaccard([who(u[0]) for u in ua if u[0] < 0x80], [who(u[0]) for u in ub if u[0] < 0x80])
            + 0.25 * jaccard([(u[0], u[10]) for u in ua if u[0] >= 0x80], [(u[0], u[10]) for u in ub if u[0] >= 0x80])
            + 0.25 * jaccard([(u[25], u[26]) for u in ua], [(u[25], u[26]) for u in ub])
            + 0.1 * jaccard([u[24] & 0x30 for u in ua], [u[24] & 0x30 for u in ub]))


def changed_battles(old: list[list[bytes]], new: list[list[bytes]], taken: dict[int, int], who=lambda s: s) -> dict[int, int]:
    """Story battles whose cast ReMixed changed (units added, dropped or moved) and moved to another entry:
    vanilla entry -> ReMixed entry, where one story entry is clearly the most alike (cast_score at least
    CHANGED_SCORE, CHANGED_GAP ahead of the next, or tied only with an entry identical to it) and the vanilla
    battle is the most alike to it in turn."""
    def best(entry: list[bytes], pool: list[list[bytes]], indices) -> list[tuple[float, int]]:
        return sorted(((cast_score(entry, pool[f], who), -f) for f in indices if any(u[0] for u in pool[f])), reverse=True)
    found = {}
    for e in BATTLE_ENTRIES:
        if e in taken or old[e] == new[e] or not any(u[0] for u in old[e]) or cast_key(old[e]) == cast_key(new[e]):
            continue
        ranked = [(score, -f) for score, f in best(old[e], new, STORY_ENTRIES)]
        if len(ranked) < 2 or ranked[0][0] < CHANGED_SCORE:
            continue
        f = ranked[0][1]
        rivals = [g for score, g in ranked[1:] if ranked[0][0] - score < CHANGED_GAP]
        if any(new[g] != new[f] for g in rivals):
            continue
        back = best(new[f], old, BATTLE_ENTRIES)
        if back and -back[0][1] == e and f != e:
            found[e] = f
    for e, f in REVIEWED_BATTLES.items():
        if e not in taken and e not in found:
            found[e] = f
    return found


def match_units(old: list[bytes], new: list[bytes], who=lambda s: s) -> list[tuple[int, bytes]]:
    """(vanilla slot, ReMixed unit) for the units of a changed cast that are surely the same: a special
    character by who it is (when it appears once on each side), a generic by its sprite set and tile, then the
    generics left by sprite set, job and team, in order."""
    pairs, used = [], set()
    for u, a in enumerate(old):
        if not a[0]:
            continue
        if a[0] < 0x80:
            mine = [x for x in old if x[0] and who(x[0]) == who(a[0])]
            theirs = [k for k, x in enumerate(new) if x[0] and x[0] < 0x80 and who(x[0]) == who(a[0])]
            if len(mine) == 1 and len(theirs) == 1:
                pairs.append((u, new[theirs[0]]))
                used.add(theirs[0])
        else:
            theirs = [k for k, x in enumerate(new) if x[0] and unit_key(x) == unit_key(a)]
            if len(theirs) == 1:
                pairs.append((u, new[theirs[0]]))
                used.add(theirs[0])
    paired = {u for u, _ in pairs}
    for u, a in enumerate(old):
        if not a[0] or a[0] < 0x80 or u in paired:
            continue
        kind = (a[0], a[10], a[24] & 0x30)
        for k, x in enumerate(new):
            if k not in used and x[0] and (x[0], x[10], x[24] & 0x30) == kind:
                pairs.append((u, x))
                used.add(k)
                break
    return pairs


def merge_entd(files: FilePair, held: dict[int, str], names: Names) -> EntdResult:
    """Vanilla battle entries with ReMixed's changes applied where that's safe.

    ReMixed reorganized its entry list: about half of the entries hold a
    different cast than vanilla's entry with the same number, tied to its own
    event scripts. So:

    - Random battles (generic units only, same characters) are ported whole,
      positions included. Unit ids stay vanilla.
    - Other entries whose cast (every unit's sprite and position) matches get
      ReMixed's unit builds (UNIT_BUILD_FIELDS), units matched by sprite and position.
    - Story entries whose exact cast ReMixed moved to one other entry (moved_entries) get the unit builds from
      there, the same way: ReMixed renumbered its story battles, and the remaster's own events (its voice acting)
      stay, so the remaster's battle keeps its entry and takes ReMixed's units.
    - The rest waits for the story port.
    """
    result = EntdResult([])
    def keep_preset(entry: int, slot: int, theirs: bytes) -> None:
        if theirs[ENTD_PRESET]:
            presets = bytearray(result.presets.get(entry, bytes(UNITS_PER_EVENT)))
            presets[slot] = theirs[ENTD_PRESET]
            result.presets[entry] = bytes(presets)

    def entries(data: bytes) -> list[list[bytes]]:
        return [[data[(e * UNITS_PER_EVENT + u) * UNIT_SIZE:(e * UNITS_PER_EVENT + u + 1) * UNIT_SIZE]
                 for u in range(UNITS_PER_EVENT)] for e in range(len(data) // (UNIT_SIZE * UNITS_PER_EVENT))]
    all_old = [x for path in ENTD_FILES for x in entries(files.get(path)[0])]
    all_new = [x for path in ENTD_FILES for x in entries(files.get(path)[1])]
    moved = moved_entries(all_old, all_new)
    who = lambda sprite: character(names, sprite)  # noqa: E731
    changed_cast = changed_battles(all_old, all_new, moved, who)
    for k, path in enumerate(ENTD_FILES):
        old_file, new_file = files.get(path)
        merged = bytearray(old_file)
        for e in range(len(old_file) // (UNIT_SIZE * UNITS_PER_EVENT)):
            base = e * UNITS_PER_EVENT
            old_units = [old_file[(base + u) * UNIT_SIZE : (base + u + 1) * UNIT_SIZE] for u in range(UNITS_PER_EVENT)]
            new_units = [new_file[(base + u) * UNIT_SIZE : (base + u + 1) * UNIT_SIZE] for u in range(UNITS_PER_EVENT)]
            if old_units == new_units:
                continue
            if is_random_battle(k * 128 + e, names, old_units, new_units):
                for u, (a, b) in enumerate(zip(old_units, new_units)):
                    unit = bytearray(b)
                    unit[32] = a[32]  # unit id: ReMixed's code numbers units its own way
                    for i in UNIT_RSM_FIELDS:
                        if u16(b, i) in held:
                            unit[i : i + 2] = ABILITY_RANDOM.to_bytes(2, "little")
                            result.held_rsm += 1
                    merged[(base + u) * UNIT_SIZE : (base + u + 1) * UNIT_SIZE] = unit
                result.random_battles += 1
                continue
            old_keys = [unit_key(x) for x in old_units if x[0]]
            new_keys = [unit_key(x) for x in new_units if x[0]]
            if (k * 128 + e) in changed_cast:
                result.events_reorganized += 1
                result.battles_changed += 1
                for u, b in match_units(old_units, all_new[changed_cast[k * 128 + e]], who):
                    a = old_units[u]
                    keep_preset(k * 128 + e, u, b)
                    unit = bytearray(a)
                    for i in MOVED_BUILD_FIELDS:
                        if not (i == ENTD_JOB and b[i] == 0):
                            unit[i] = b[i]
                    for i in UNIT_RSM_FIELDS:
                        if u16(b, i) in held and u16(b, i) != u16(a, i):
                            unit[i : i + 2] = a[i : i + 2]
                            result.held_rsm += 1
                    if bytes(unit) != a:
                        merged[(base + u) * UNIT_SIZE : (base + u + 1) * UNIT_SIZE] = unit
                        result.changed_ported += 1
                continue
            if (k * 128 + e) in moved:
                result.events_reorganized += 1
                result.events_moved += 1
                new_units = all_new[moved[k * 128 + e]]
            elif sorted(old_keys) != sorted(new_keys) or len(set(old_keys)) != len(old_keys):
                result.events_reorganized += 1
                continue
            else:
                result.events_same += 1
            by_key = {unit_key(x): x for x in new_units if x[0]}
            changed = False
            from_moved = (k * 128 + e) in moved
            for u, a in enumerate(old_units):
                if not a[0]:
                    continue
                b = by_key[unit_key(a)]
                keep_preset(k * 128 + e, u, b)
                unit = bytearray(a)
                for i in MOVED_BUILD_FIELDS if from_moved else UNIT_BUILD_FIELDS:
                    if not (from_moved and i == ENTD_JOB and b[i] == 0):
                        unit[i] = b[i]
                for i in UNIT_RSM_FIELDS:
                    if u16(b, i) in held and u16(b, i) != u16(a, i):
                        unit[i : i + 2] = a[i : i + 2]
                        result.held_rsm += 1
                if bytes(unit) != a:
                    merged[(base + u) * UNIT_SIZE : (base + u + 1) * UNIT_SIZE] = unit
                    if (k * 128 + e) in moved:
                        result.moved_ported += 1
                    else:
                        result.ported += 1
                    changed = True
            if (k * 128 + e) not in moved:
                result.events_changed += changed
        # The remaster's units off the slots ReMixed gave someone else (REMASTER_MOVED_SPRITE_SETS); ReMixed's own
        # scenarios' units go in later (write_remixed_scenarios), on their own slots.
        result.sprites_moved += move_remaster_units(merged, k * 128)
        result.files.append(bytes(merged))
    return result


def move_remaster_units(data: bytearray, first_entry: int) -> int:
    """The remaster's units in battle data (UNIT_SIZE a unit, from entry first_entry) off the sprite sets ReMixed gave its
    own characters (REMASTER_MOVED_SPRITE_SETS), in place. Returns how many moved."""
    moved = 0
    for at in range(0, len(data) - UNIT_SIZE + 1, UNIT_SIZE):
        if data[at] not in REMASTER_MOVED_SPRITE_SETS:
            continue
        old = data[at]
        sprites, job = REMASTER_MOVED_SPRITE_SETS[old]
        if data[at + ENTD_JOB] == old and job is not None:
            data[at + ENTD_JOB] = job
        elif sprites in GENERIC_SPRITE_SETS:
            sys.exit(f"entry 0x{first_entry + at // (UNIT_SIZE * UNITS_PER_EVENT):03X}'s unit on sprite set 0x{old:02X} "
                     f"has job 0x{data[at + ENTD_JOB]:02X}, which wouldn't draw as a generic (REMASTER_MOVED_SPRITE_SETS)")
        data[at] = sprites
        moved += 1
    return moved


def battle_coupled(files: FilePair, merged: list[bytes]) -> tuple[set[int], set[int]]:
    """Jobs and skillsets the mod's battles still use but ReMixed's battles don't.

    ReMixed blanks or repurposes these because its own battle setups moved the
    units elsewhere. Where the mod's (merged) battles still use one, its
    ReMixed changes would break those units, so they wait for the rest of the
    battle and story port.
    """
    def usage(entd: list[bytes], scus: bytes) -> tuple[set[int], set[int]]:
        jobs, skillsets = set(), set()
        for data in entd:
            for u in range(len(data) // UNIT_SIZE):
                unit = data[u * UNIT_SIZE : (u + 1) * UNIT_SIZE]
                if unit[0]:  # sprite set 0 is an empty slot
                    jobs.add(unit[10])
                    skillsets.update((unit[11], unit[29]))
        skillsets.update(scus[0x518B8 + j * 48] for j in range(160))
        return jobs, skillsets

    remixed = [files.get(p)[1] for p in ENTD_FILES]
    (old_jobs, old_sets), (new_jobs, new_sets) = usage(merged, files.get(SCUS)[0]), usage(remixed, files.get(SCUS)[1])
    return {j for j in old_jobs - new_jobs if j < 160}, {s for s in old_sets - new_sets if s < 176}


PORTRAIT_REASON = ("ReMixed's per-job portrait (the WLDFACE portrait its world menus show for the job) is a monster "
                   "unit's graphic in the remaster (the job's +0x2E into the unit's +0x18F: its sprite and its face), so "
                   "its numbers would draw other characters (seen in the game: monsters with Lucavi's faces); the remaster "
                   "shows each unit's face by its graphic, ReMixed's own from their sheets' portraits (write_remixed_faces)")


# A monster job's graphic (its +47 on the PS1, the loader's MonsterGraphic) names the sprite graphic 0x85 + it.
MONSTER_GRAPHICS = 0x85


def monster_sheets_drawn(cfg: dict, shipped: bool = True) -> set[int]:
    """The monster graphics (0x86-0x9E, as a job names them: less 0x85) the port draws as ReMixed does: ReMixed's
    sheet is one of vanilla's byte for byte (the remaster has that art), or the mod ships ReMixed's (REMIXED_SPRITES).
    ReMixed's special monster jobs name Byblos's, the Demon's and the Steel Giant's sheets (vanilla's, where vanilla's
    jobs give 0 and draw them otherwise), and its Lamia's and Tonberry's (0x97 and 0x98, shipped for the enhanced
    build only: `shipped` False leaves them out)."""
    drawn = {g - MONSTER_GRAPHICS for g in REMIXED_SPRITES if g > MONSTER_GRAPHICS} if shipped else set()
    with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
        theirs = {vanilla.read(e).rstrip(b"\0") for e in vanilla._read_tree()
                  if e.path.startswith("BATTLE/") and e.path.endswith(".SPR")}
        for g in range(MONSTER_GRAPHICS + 1, 0x9F):
            entry = patched.find(f"BATTLE/{g:02X}.SPR")
            if entry is not None and patched.read(entry).rstrip(b"\0") in theirs:
                drawn.add(g - MONSTER_GRAPHICS)
    return drawn


def hold_custom_monster_graphics(result: TableResult, scus_old: bytes, scus_new: bytes, drawn: set[int]) -> None:
    """Monster jobs that would point at a graphic the port doesn't draw as ReMixed does (no vanilla monster job's,
    nor one of monster_sheets_drawn), and every job's portrait, which ReMixed renumbers for its own art
    (PORTRAIT_REASON)."""
    vanilla = {scus_old[0x518B8 + j * 48 + 47] for j in range(0x5E, 0xA0)} | drawn
    for rid, changed in result.entries.items():
        if changed.pop("MonsterPortrait", None) is not None:
            result.skipped.append((rid, "MonsterPortrait", PORTRAIT_REASON))
        graphic = scus_new[0x518B8 + rid * 48 + 47]
        if rid >= 0x5E and "MonsterGraphic" in changed and graphic not in vanilla:
            for f in ("MonsterPortrait", "MonsterPalette", "MonsterGraphic"):
                if changed.pop(f, None) is not None:
                    result.skipped.append((rid, f, f"graphic {graphic} (sprite 0x{graphic + MONSTER_GRAPHICS:02X}) "
                                                   "is ReMixed's art the mod doesn't ship"))
        elif rid >= 0x5E and "MonsterGraphic" in changed and graphic:
            # The remaster draws a monster by its job's portrait byte, which holds the graphic's own number (its
            # record's +0x2E into the unit's +0x18F, 0x27xxxx's unit build; the PS1's graphic byte goes to +0x18E):
            # with only the graphic written, ReMixed's Tonberry (job 0x98, the Holy Dragon's) still drew as the
            # Dragon, 0x94 (a tester, 2026-11-22: green dragons with a Tonberry's abilities; seen in the hidden PC
            # game), and its jobs 0x92 and 0x95, whose byte the remaster leaves 0, had no graphic at all.
            changed["MonsterPortrait"] = graphic + MONSTER_GRAPHICS


# The release's version: raise it for each published build (docs/release/RELEASING.md). The ReMixed version the
# build is made from goes after it, as build metadata. The title screen's plaque shows it (write_title_screen).
RELEASE_VERSION = "0.9.44"


def write_mod_config(version: str, with_code: bool) -> None:
    config = {
        "ModId": MOD_ID,
        "ModName": "Ivalice ReMixed",
        "ModAuthor": "Ivalice ReMixed port (unofficial)",
        "ModVersion": version,
        "ModDescription": "The Lion War ReMixed for The Ivalice Chronicles (enhanced): its balance, rules and Extra "
                          "Options, its missions and the side chapter 1-A. An unofficial port, in beta: some of "
                          "ReMixed's story and art isn't ported yet.",
        "ModDll": CODE_FILES[0] if with_code else "",
        "ModIcon": "",
        "ModR2RManagedDll32": "",
        "ModR2RManagedDll64": "",
        "ModNativeDll32": "",
        "ModNativeDll64": "",
        "Tags": ["Gameplay", "Overhaul"],
        "CanUnload": not with_code,  # the DLL patches the game's memory and can't undo that
        "HasExports": False,
        "IsLibrary": False,
        "ReleaseMetadataFileName": f"{MOD_ID}.ReleaseMetadata.json",
        "IsUniversalMod": False,
        # The DLL uses Reloaded-II's shared hooks mod (the FFT mod loader depends on it too), and ReMixed's events run
        # on the Event Instruction Upgrade.
        "ModDependencies": ["fftivc.utility.modloader"] + (["reloaded.sharedlib.hooks", EIU_ID] if with_code else []),
        "OptionalDependencies": [],
        "SupportedAppId": ["fft_classic.exe", "fft_enhanced.exe"],
    }
    (MOD_DIR / "ModConfig.json").write_text(json.dumps(config, indent=2) + "\n", encoding="utf-8")


def write_eiu_config() -> None:
    """The upgrade's ModConfig.json, as its repo has it."""
    shutil.copy2(EIU_REPO / "mod" / "ModConfig.json", EIU_DIR / "ModConfig.json")


def left_out_lines(results: list[TableResult]) -> list[str]:
    lines = []
    for r in results:
        reasons: dict[str, set[int]] = {}
        counts: dict[str, int] = {}
        for rid, _, reason in r.skipped:
            reasons.setdefault(reason, set()).add(rid)
            counts[reason] = counts.get(reason, 0) + 1
        for reason, ids in reasons.items():
            shown = ", ".join(str(i) for i in sorted(ids)[:30]) + (" …" if len(ids) > 30 else "")
            what = f"{counts[reason]} fields in {len(ids)} entries ({shown})"
            if reason.startswith("it would hand out ability"):
                what = f"{counts[reason]} slots in entries {shown}"
            lines.append(f"- {r.spec.xml}: {what}, because {reason}")
        if r.dropped_bits and not r.not_written:
            lines.append(f"- {r.spec.xml}: chance-tier bits on {r.dropped_bits} sets: the loader's sets can't hold them, so "
                         "they go to the code mod instead (Data/patches.json)")
        if r.not_written and r.entries:
            fields_held = sum(len(v) for v in r.entries.values())
            lines.append(f"- {r.spec.xml}: {len(r.entries)} entries ({fields_held} fields) not written, because {r.not_written}")
    return lines


def write_report(builds: list[tuple[str, list[TableResult]]], overrides: OverrideResult, source: str,
                 entd: EntdResult | None = None, presentation: Presentation | None = None,
                 name_counts: dict[str, int] | None = None, code_state: str = "") -> Path:
    lines = [
        "# Mod build report",
        "",
        "Generated by `python -I tools/build_mod.py`. Don't edit by hand.",
        "",
        f"Source: {source}. Output: `mod/{MOD_ID}/`, with tables for each game build in",
        "`FFTIVC/tables/<build>/` and enhanced's ability overrides in `FFTIVC/data/enhanced/`.",
        "",
        "**Decoder check** (enhanced) compares our reading of the vanilla PS1 tables with",
        "the mod loader's templates (the remaster's defaults). High agreement means the",
        "field mapping is right; the remaster's own rebalances show up as small gaps.",
        "**Conflicts** are fields ReMixed changes that the remaster had also changed",
        "from the PS1. ReMixed's value is used.",
    ]
    results = dict(builds).get("enhanced", [])
    for target, target_results in builds:
        lines += ["", f"## {target.capitalize()} build", "",
                  "| Table | Entries written | Fields written | Decoder check | Conflicts | Left out |",
                  "|---|--:|--:|--:|--:|--:|"]
        for r in target_results:
            written = 0 if r.not_written else len(r.entries)
            fields_written = 0 if r.not_written else sum(len(v) for v in r.entries.values())
            check = f"{r.agree / r.checked:.1%}" if r.checked and target == "enhanced" else "n/a"
            left_out = len(r.skipped) + r.dropped_bits + (sum(len(v) for v in r.entries.values()) if r.not_written else 0)
            lines.append(f"| {r.spec.xml} | {written} | {fields_written} | {check} | {len(r.conflicts)} | {left_out} |")
        lines += ["", "Left out on purpose:", ""] + left_out_lines(target_results)
        if target == "enhanced":
            lines += [
                "- ReMixed's gendered Bard/Dancer slots aren't ported: its Dark Knight (job 0x5B's row, the Darkness",
                "  skillset in Sing's slot 0x16, the Bard slot's unlock row 16) goes to the remaster's own Dark Knight",
                "  (job 160, skillset 224, unlock row 19), its man's Bard (job 0x38, skillset 0x4D) to the Bard (job 91,",
                "  Sing), and the Bard keeps the remaster's requirements (tools/build_mod.py, DARK_KNIGHT).",
            ]
        if target == "classic":
            lines += [
                "- Ability combat stats (range, area, formula, X, Y, status, CT, MP): the loader",
                "  doesn't accept the ability override table for classic.",
                "- Classic's jobs only load with the job-table pattern from `tools/loader_signatures.py`",
                "  applied to the mod loader.",
            ]
        lines.append("- Battle units, events, text, art: not handled by this build.")

    if presentation is not None and name_counts:
        renamed = {k: len(v) for k, v in presentation.renames.items()}
        lines += [
            "", "## Names, animations and effects (enhanced)", "",
            "Names: ReMixed's real renames (compared with vanilla PS1 names, so translation",
            "differences don't count), written into the English `ability`, `job` and",
            "`jobcommand` tables, and ReMixed's descriptions (its HELP.LZW) where it changed vanilla's",
            "text or the remaster's slot has a placeholder: "
            + ", ".join(f"{name_counts.get(f'{k} described', 0)} {k}" for k in NAME_LISTS) + ".",
            "",
            *[f"- {k.capitalize()}: {name_counts.get(k, 0)} of {renamed[k]} renames written "
              f"(the rest belong to held-back entries)" for k in renamed],
            "- Abilities handed out in remaster slots with a placeholder name or description (\"A187\", "
            "\"UNUSED\"), given ReMixed's name where the slot has none, an icon, and a description "
            "(PLACEHOLDER_DESCRIPTIONS; the rest say theirs isn't ported yet): "
            + (", ".join(f"0x{k:03X} {v}" for k, v in name_counts.get("placeholders", {}).items()) or "none"),
            f"- JP costs: {name_counts.get('JP costs', 0)} of ReMixed's, in the same English `ability` table (the "
            "game reads the price there; other languages keep the remaster's)",
            "",
            "Animations: ReMixed stores per-actor animation variants; each ability's default",
            "goes into `AbilityTypeData` (see the table above).",
            "",
            f"- Kept vanilla because ReMixed's values need its own code: {len(presentation.types_kept)}",
            "",
            f"Visual effects: ReMixed changes {len(presentation.effects_changed)}, and "
            f"{len(presentation.effects_ported)} go into `AbilityEffectNumberFilterData`.",
            "The rest are under \"Left out\" above: abilities ReMixed retired, and slots the",
            "remaster reused for its own abilities. Reworked abilities among those stay held back.",
        ]

    if code_state:
        lines += ["", "## Code part", "",
                  f"The DLL (`code/IvaliceRemixed`, reads `{CODE_DATA.as_posix()}`): {code_state}."]

    if entd is not None:
        lines += [
            "", "## Battle units (enhanced)", "",
            "Written as `FFTIVC/data/enhanced/fftpack/battle_entd<1-4>_ent.bin`. ReMixed",
            "reorganized its battle entries: about half hold a different cast than vanilla's",
            "entry with the same number, tied to its own event scripts. So:",
            "",
            f"- Random battles ported whole (positions, jobs, gear, levels…; unit ids stay vanilla): {entd.random_battles}",
            f"- Other entries with an identical cast: {entd.events_same}, of which {entd.events_changed} get ReMixed's",
            f"  unit builds ({entd.ported} units: job, skillsets, abilities, gear, level, Brave/Faith,",
            "  birthday, palette, EXP, spoils; staging stays vanilla)",
            f"- Abilities not ported because they need ReMixed's code: {entd.held_rsm}",
            f"- Story battles, scenes and tutorials ReMixed moved or rewrote: {entd.events_reorganized}; of those, {entd.events_moved}",
            f"  whose exact cast ReMixed moved to one other entry give their units its builds from there",
            f"  ({entd.moved_ported} units; vanilla's palette, and vanilla's job where ReMixed's is 0);",
            f"  {entd.battles_changed} story battles whose cast ReMixed changed and moved, matched by their cast's",
            f"  likeness, give the units surely the same their builds ({entd.changed_ported} units: a special character",
            "  by sprite, a generic by sprite and tile); units ReMixed added or dropped stay as vanilla has them;",
            "  the rest wait for the story port",
        ]

    lines += [
        "", "## Ability attributes (enhanced)", "",
        f"{overrides.abilities} abilities, {overrides.fields} fields, written to the enhanced build's",
        "`nxd/overrideabilityactiondata.nxd` on top of the remaster's own overrides.",
        "",
        f"- Fields the remaster had also overridden (ReMixed's value used): {len(overrides.conflicts)}",
        "- The \"requires sword\" and \"requires Materia Blade\" flag bits keep vanilla's values,",
        "  because ReMixed reuses them as markers for its own code.",
        *([f"- {len(overrides.custom_formula)} abilities use a formula whose behaviour ReMixed changes in code. They",
           "  get ReMixed's numbers but vanilla's formula logic until those mechanics are ported:",
           "  " + ", ".join(f"0x{i:03X}" for i in overrides.custom_formula)]
          if overrides.custom_formula else
          ["- Every ability on a formula ReMixed rewrote runs ReMixed's version (in the code mod)."]),
        f"- {len(overrides.code_formula)} abilities use a formula the code mod implements "
        f"({', '.join(f'0x{f:02X}' for f in sorted(CODE_FORMULAS))}, if the DLL is built): "
        + ", ".join(f"0x{i:03X}" for i in overrides.code_formula),
        *([f"- {len(overrides.kept_formula)} abilities keep their vanilla formula, X and Y: ReMixed moves them onto a",
           "  formula that's empty in vanilla (and on the remaster) and only its code fills, so they'd do",
           "  nothing: " + ", ".join(f"0x{i:03X} (0x{f:02X})" for i, f in overrides.kept_formula.items())]
          if overrides.kept_formula else
          ["- No ability is left on a formula that's empty without ReMixed's code."]),
        f"- Left alone because the remaster uses the slot for its own ability: "
        + (", ".join(f"0x{i:03X}" for i in overrides.remaster_owned) or "none"),
        f"- The status byte stays vanilla's for {len(overrides.slot_bytes)} abilities whose slot number ReMixed keeps "
        "there (the remaster picks their slot by id and reads the byte as statuses): "
        + ", ".join(f"0x{i:03X}" for i in overrides.slot_bytes),
    ]

    lines += ["", "## Decoder disagreements by field (enhanced)", "",
              "Fields where our vanilla reading differs from the remaster's default in more than one entry.", ""]
    any_disagree = False
    for r in results:
        for f, n in sorted(r.disagree.items(), key=lambda x: -x[1]):
            if n > 1:
                any_disagree = True
                lines.append(f"- {r.spec.xml}.{f}: {n} entries")
    if not any_disagree:
        lines.append("None.")

    lines += ["", "## Loader workarounds", "",
              "- JobCommandData rows are written whole, with both extended-id flag fields, because",
              "  the loader's id setters fail when those fields are missing.",
              "- AbilityJumpData and AbilityChargeAimData are held back (see above). Writing them",
              "  with their real ids is ignored, and with 0-based ids the game crashes at launch."]

    lines += ["", "## Conflicts (enhanced)", ""]
    rows = [(r.spec.xml, rid, f) for r in results for rid, f in r.conflicts]
    lines += [f"- {t} {rid}: {f}" for t, rid, f in rows] or ["None."]

    out = REPORT_DIR / "mod-build.md"
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return out


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    # Enhanced only for now; classic support works but is parked until enhanced is done.
    parser.add_argument("--only", choices=BUILDS, action="append", default=None,
                        help="game build to build for; repeat for both (default: enhanced)")
    parser.add_argument("--install", action="store_true",
                        help="copy the built mod into the Reloaded-II Mods folder set as target.reloaded_mods")
    parser.add_argument("--no-code", action="store_true",
                        help="leave out the code part (the DLL), and everything that depends on it")
    quick = parser.add_mutually_exclusive_group()
    quick.add_argument("--code-only", action="store_true",
                       help="compile/check/install only the DLLs, reusing the last generated Data/patches.json")
    quick.add_argument("--data-only", action="store_true",
                       help="rebuild generated data while reusing mod/code-out (skip the C# build and offline check)")
    parser.add_argument("--package", action="store_true",
                        help="also zip the mod and the mods it needs (as installed in target.reloaded_mods) into "
                             "mod/steam-deck/, one archive each, for another machine such as a Steam Deck "
                             "(docs/STEAM_DECK.md)")
    args = parser.parse_args()

    cfg = load_config()
    if args.no_code and (args.code_only or args.data_only):
        parser.error("--no-code cannot be combined with --code-only or --data-only")
    if args.code_only and (args.only or args.package):
        parser.error("--code-only does not use --only and cannot package a partial build")
    if args.code_only:
        code_only(cfg, args.install)
        return
    if args.data_only:
        code = ROOT / "mod" / "code-out"
        missing = [name for name in CODE_FILES + EIU_FILES + SHARED_CODE_FILES if not (code / name).is_file()]
        if missing:
            sys.exit("--data-only needs a prior code build in mod/code-out; missing: " + ", ".join(missing))
        print("Reusing mod/code-out (C# build and offline code check skipped)")
    else:
        code = None if args.no_code else build_code(cfg)
    code_formulas = CODE_FORMULAS if code else set()  # without the DLL, nothing is implemented
    code_supports = CODE_SUPPORTS if code else set()
    code_rules = CODE_RULES if code else []
    jump_reworked = "jump-rework" in code_rules
    dragon_tech = jump_reworked and "dragon-tech" in code_rules  # Lancet and the Breaths stay in Jump's skillset
    guns_ported = "gun-skills" in code_rules
    code_abilities = (code_supports | (set(JUMP_ABILITIES) if jump_reworked else set())
                      | (set(GUN_SKILLS) if guns_ported else set()))
    code_skillsets = {} if jump_reworked else CODE_SKILLSETS
    loader = cfg.get("tools", {}).get("modloader")
    if not loader or not (Path(loader) / "TableData").is_dir():
        sys.exit("Set tools.modloader in local.toml to the FFT mod loader's folder (it has TableData/).")
    templates = Path(loader) / "TableData"
    source = f"The Lion War ReMixed ({ppf_path(cfg).stem})"
    version = re.search(r"v([\d.]+)", ppf_path(cfg).stem)
    mod_version = f"{RELEASE_VERSION}+remixed.{version.group(1)}" if version else RELEASE_VERSION

    names = Names(WORK / "cache" / "fftpatcher")
    with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
        files = FilePair(vanilla, patched)
        scus_old, scus_new = files.get(SCUS)
        battle_old, battle_new = files.get("BATTLE.BIN")
        test_evt = files.get("EVENT/TEST.EVT")[1]
        presentation = Presentation.read(battle_old, battle_new)
        sources = {source: files.get(source) for source in {spec.source for spec in SPECS}
                   if source != ABILITY_TYPES_SOURCE}
        sources[ABILITY_TYPES_SOURCE] = (presentation.types_old, presentation.types_new)
        # Enhanced gets ReMixed's visual effects (AbilityEffectNumberFilterData),
        # which releases reworked abilities that only waited for theirs. Classic
        # doesn't get them yet (CLASSIC_UNSUPPORTED), so it holds those back still.
        held_classic = code_dependent_abilities(scus_old, scus_new, presentation, set())  # no code mod on classic
        remaster_owned = remaster_owned_abilities(cfg, scus_old)
        drawn_monsters = monster_sheets_drawn(cfg)  # enhanced: with REMIXED_SPRITES' sheets
        drawn_classic = monster_sheets_drawn(cfg, shipped=False)
        effect_spec = next(s for s in SPECS if s.xml == "AbilityEffectNumberFilterData")
        effect_template = read_template(templates / f"{effect_spec.xml}.xml")[3]
        presentation.effects_ported = (set(build_table(effect_spec, battle_old, battle_new, effect_template).entries)
                                       - set(remaster_owned))
        held = code_dependent_abilities(scus_old, scus_new, presentation, code_formulas, code_abilities)
        empty = empty_formulas(battle_old)
        for a, name in remaster_owned.items():
            held[a] = held_classic[a] = f"the remaster uses this slot for its own ability ({name})"
        entd = merge_entd(files, held, Names(WORK / "cache" / "fftpatcher"))
        coupled_jobs, coupled_skillsets = battle_coupled(files, entd.files)
    coupled = {"JobData": coupled_jobs, "JobCommandData": coupled_skillsets}
    coupled_reason = "the mod's battles still use it but ReMixed's don't (waits for the story port)"
    vanilla_homes = ability_homes(scus_old)

    shutil.rmtree(MOD_DIR, ignore_errors=True)  # rebuild from scratch so nothing stale lingers
    shutil.rmtree(EIU_DIR, ignore_errors=True)
    links = ClassicLinks.read(scus_old, scus_new)
    builds: list[tuple[str, list[TableResult]]] = []
    handed_out: dict[int, set[int]] = {}  # ability -> the other abilities of skillsets (enhanced) that hand it out
    targets = args.only or ["enhanced"]
    for target in targets:
        print(f"== {target}")
        results = []
        for spec in SPECS:
            root, element, ver, template = read_template(templates / f"{spec.xml}.xml")
            hands_out = spec.xml in ("JobData", "JobCommandData", "MonsterJobCommandData")
            classic = target == "classic"
            # Classic's defaults are the PS1's, so ReMixed's values go in as they are,
            # without merging onto enhanced's template values.
            source_old, source_new = sources[spec.source]
            result = build_table(spec, source_old, source_new, {} if classic else template,
                                 (held_classic if classic else held) if hands_out else None,
                                 vanilla_homes, links if classic else None)
            keep_remaster_jobs(spec, result)
            if not classic and spec.xml in ("JobData", "JobCommandData", "JobNeedLevelData"):
                relocate_dark_knight(spec, result, source_old, source_new, template, held, vanilla_homes)
            keep_vanilla = {rid: coupled_reason for rid in coupled.get(spec.xml, set())}
            if spec.xml == "JobCommandData":
                keep_vanilla.update(CODE_SKILLSETS if classic else code_skillsets)  # classic has no code mod
                if jump_reworked and not dragon_tech and not classic:
                    for rid in set(CODE_SKILLSETS) & set(result.entries):
                        result.entries[rid] = hold_back_abilities(spec, rid, result.entries[rid], JUMP_SKILLSET_EXTRAS,
                                                                  {}, result.skipped)
            if spec.xml in ABILITY_TABLES:
                keep_vanilla.update({a: f"the remaster uses this slot for its own ability ({name})"
                                     for a, name in remaster_owned.items()})
            for rid in sorted(set(keep_vanilla) & set(result.entries)):
                result.skipped = [s for s in result.skipped if s[0] != rid]  # the whole row stays vanilla
                result.conflicts = [c for c in result.conflicts if c[0] != rid]
                for f in result.entries.pop(rid):
                    result.skipped.append((rid, f, keep_vanilla[rid]))
            if spec.xml == "JobData":
                hold_custom_monster_graphics(result, scus_old, scus_new, drawn_classic if classic else drawn_monsters)
            if spec.xml == "JobCommandData" and not classic:
                for rid, fields in result.entries.items():
                    row = {**template.get(rid, {}), **fields}
                    ids = {v for f, v in row.items() if ABILITY_ID_FIELDS.match(f) and isinstance(v, int) and v}
                    for a in ids:
                        if any(fields.get(f) == a for f in fields if ABILITY_ID_FIELDS.match(f)):
                            handed_out.setdefault(a, set()).update(ids)
            unsupported = CLASSIC_UNSUPPORTED.get(spec.xml) if classic else None
            result.not_written = spec.blocked or unsupported or ""
            results.append(result)
            if result.entries and not result.not_written:
                write_xml(result, root, element, ver, names,
                          MOD_DIR / "FFTIVC" / "tables" / target / f"{spec.xml}.xml", source)
            fields_written = sum(len(v) for v in result.entries.values())
            note = "   (not written)" if result.not_written and result.entries else ""
            print(f"  {spec.xml:24} {len(result.entries):4} entries {fields_written:5} fields{note}")
        builds.append((target, results))

    overrides = OverrideResult()
    if "enhanced" in targets:
        overrides = build_ability_overrides(cfg, scus_old, scus_new, empty, remaster_owned, code_formulas)
        print(f"  {'Ability attributes':24} {overrides.abilities:4} entries {overrides.fields:5} fields (enhanced NXD)")
        print(f"  {'Button bar':24} " + build_job_menu_guide(cfg))
        with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
            originals = [FilePair(vanilla, patched).get(p)[0] for p in ENTD_FILES]
        for k, (merged, original) in enumerate(zip(entd.files, originals), 1):
            if merged != original:
                out = MOD_DIR / "FFTIVC" / "data" / "enhanced" / "fftpack" / f"battle_entd{k}_ent.bin"
                out.parent.mkdir(parents=True, exist_ok=True)
                out.write_bytes(merged)
        print(f"  {'Battle units (ENTD)':24} {entd.random_battles:4} random battles, {entd.ported} story units, "
              f"{entd.moved_ported} more from {entd.events_moved} moved entries, "
              f"{entd.changed_ported} from {entd.battles_changed} changed battles, {entd.sprites_moved} of the "
              f"remaster's off slots ReMixed gave others ({', '.join(f'0x{s:X} to 0x{t[0]:X}' for s, t in REMASTER_MOVED_SPRITE_SETS.items())})")
        print(f"  {'Remaster scenes':24} " + write_remaster_moves(cfg))
        skip_names = {"abilities": set(held) | RENAMES_NEED_CODE,
                      "jobs": set(coupled_jobs) | set(REMASTER_KEPT_JOBS),
                      "skillsets": coupled_skillsets | set(code_skillsets)}
        renames = {k: {**{i: n for i, n in v.items() if i not in DARK_KNIGHT_NAMES.get(k, set())},
                       **EXTRA_RENAMES.get(k, {})} for k, v in presentation.renames.items()}
        with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
            help_old, help_new = FilePair(vanilla, patched).get("EVENT/HELP.LZW")
        two_letters = remixed_help.digraphs(scus_new, SCUS_FILE)
        jobs_ported = next(r for t, rs in builds if t == "enhanced" for r in rs if r.spec.xml == "JobData")
        job_commands = {job: fields["JobCommandId"] for job, fields in jobs_ported.entries.items()
                        if "JobCommandId" in fields}
        name_counts = build_name_tables(cfg, renames, skip_names, jp_costs(scus_old, scus_new, set(held)),
                                        handed_out, presentation.ability_names,
                                        remixed_descriptions(help_old, help_new, two_letters), presentation.names_old,
                                        job_commands)
        print(f"  {'Story experiment':24} " + write_story_experiment(cfg))
        print(f"  {'ReMixed events':24} " + write_remixed_events(test_evt, battle_new))
        with Disc(remixed_bin(cfg)) as patched:
            presets = remixed_presets(patched)
        line, presets = write_remixed_scenarios(cfg, held, presets, entd.presets)
        print(f"  {'ReMixed scenarios':24} " + line)
        (MOD_DIR / REMIXED_PRESETS_FILE).write_bytes(presets)
        print(f"  {'ReMixed event presets':24} {REMIXED_PRESETS_FILE} ({REMIXED_PRESETS} events, {len(presets)} bytes)")
        print(f"  {'Names (English)':24} " + ", ".join(f"{len(n) if isinstance(n, (list, dict)) else n} {k}"
                                                         for k, n in name_counts.items()))
        print(f"  {'Item names (English)':24} " + write_item_names(cfg))
        print(f"  {'Dark Knight page':24} " + write_dark_knight_page(cfg))
        print(f"  {'Mission postings':24} " + write_mission_postings(cfg))
        print(f"  {'Options page, reveal':24} " + write_options_page(cfg))
        print(f"  {'Dispatch Missions':24} " + write_dispatch_text(cfg, test_evt))
        print(f"  {'World-map starts':24} " + write_world_starts(cfg))
        print(f"  {'Remaster sprite sets':24} " + write_remaster_sprite_sets(cfg))
        print(f"  {'Battle unit overrides':24} " + write_entry_overrides(cfg))
        print(f"  {'Algus campaign (offline)':24} "
              + check_algus_campaign(eiu_events.read_lengths(battle_new), presets))
        print(f"  {'Map looks':24} " + write_map_looks(cfg))
        from check_story_regressions import check as check_story_regressions
        print(f"  {'Story exits (offline)':24} " + "; ".join(check_story_regressions()))
        from audit_battle_units import audit as audit_battle_units
        print(f"  {'Battle units (offline)':24} {audit_battle_units()}")
        print(f"  {'Reused numbers':24} " + write_reused_numbers(cfg))
        print(f"  {'Character names':24} " + write_character_names(cfg))
        print(f"  {'Title screen':24} " + write_title_screen(cfg))
        print(f"  {'Event graphics':24} " + write_event_graphics(cfg))
        print(f"  {'Ability effects':24} " + write_remixed_effects(cfg))
        line, sprite_data, palette_sheets = write_remixed_sprites(cfg, battle_old, battle_new)
        print(f"  {'Unit sprites':24} " + line)
        line, face_data = write_remixed_faces(cfg, sprite_data)
        print(f"  {'Faces':24} " + line)
        line, sprite_aliases = write_sprite_aliases(cfg, battle_old, sprite_data)
        print(f"  {'Unit sprites as others':24} " + line)
        # A graphic drawn as another shows that one's face too (0x38 the Bard's, 0xA0 ReMixed's 0x37's).
        own_faces = dict((graphic, face) for graphic, face in face_data)
        face_data += [[graphic, own_faces.get(source, REMASTER_FACE_FOLDS.get(source, source))]
                      for graphic, _, source in sprite_aliases]
        print(f"  {'Dark Knight shapes':24} " + write_dark_knight_shapes(cfg))
        print(f"  {'Table list':24} " + update_root_nxl(cfg))
        print(f"  {'Story stages (offline)':24} " + check_story_stages(cfg))

    # Patches for the DLL: exe name -> patch name -> data (see code/IvaliceRemixed/Patcher.cs).
    # The DLL only ships once something needs it.
    code_patches: dict[str, dict] = {}
    if "enhanced" in targets:
        code_patches["fft_enhanced.exe"] = {"formulas": {"install": sorted(code_formulas),
                                                          "geomancy": geomancy_table(battle_new)},
                                            "supports": sorted(code_supports),
                                            "rules": {"enable": code_rules,
                                                      "unevadable": unevadable_abilities(battle_new),
                                                      "gun-skills": gun_skills(battle_new, scus_new),
                                                      "tiers": status_tiers(scus_new),
                                                      "weapons": weapon_requirements(battle_new),
                                                      "rates": option_defaults(test_evt),
                                                      "monster-reactions": monster_reactions(scus_new),
                                                      "jump": jump_rework(battle_new, scus_new),
                                                      "equip-supports": equip_supports(scus_new),
                                                      "sprites": sprite_data + palette_sheets,
                                                      "faces": face_data,
                                                      "sprite-aliases": sprite_aliases,
                                                      "old-guests": old_guest_rows(),
                                                      "guest-lookup": guest_lookup_rows(),
                                                      "map-movements": map_movement_data(cfg, test_evt, battle_new),
                                                      "side-chapter-objectives": side_chapter_objectives(),
                                                      "own-story-flags": [mark for _, _, mark in OWN_STORY_STEPS],
                                                      "own-story-destinations": own_story_destinations(),
                                                      "story-stage-floors": [list(row) for row in STORY_STAGE_FLOORS]}}
    shipped = code is not None and bool(code_patches)
    if code and shipped:
        for name in CODE_FILES + SHARED_CODE_FILES:
            shutil.copy2(code / name, MOD_DIR / name)
        shutil.copy2(ROOT / NOTICES, MOD_DIR / NOTICES)
        EIU_DIR.mkdir(parents=True, exist_ok=True)
        for name in EIU_FILES + SHARED_CODE_FILES:
            shutil.copy2(code / name, EIU_DIR / name)
        write_eiu_config()
        patches = write_code_data(source, code_patches)
        from check_story_regressions import check_side_chapter_opening_route, check_world_objectives
        print(f"  {'World objectives (offline)':24} " + check_world_objectives(patches))
        print(f"  {'Opening route (offline)':24} " + check_side_chapter_opening_route())
        if not args.data_only:
            check_code(cfg, code, patches)
    else:
        shutil.rmtree(EIU_DIR, ignore_errors=True)  # ReMixed's events need the code part; no upgrade without it
    write_mod_config(mod_version, with_code=shipped)
    print(f"Wrote {(MOD_DIR).relative_to(ROOT)}" + (f" and {EIU_DIR.relative_to(ROOT)}" if EIU_DIR.is_dir() else ""))
    code_state = "shipped" if shipped else "built, not shipped (nothing needs it yet)" if code else "not built"
    report = write_report(builds, overrides, source, entd, presentation, locals().get("name_counts", {}), code_state)
    print(f"Wrote {report.relative_to(ROOT)}")

    if args.install:
        mods = cfg.get("target", {}).get("reloaded_mods")
        if not mods or not Path(mods).is_dir():
            sys.exit("Set target.reloaded_mods in local.toml to Reloaded-II's Mods folder.")
        skipped = set(CODE_FILES + SHARED_CODE_FILES) if args.data_only else set()
        install(MOD_DIR, Path(mods) / MOD_ID, skipped)
        if EIU_DIR.is_dir():
            install(EIU_DIR, Path(mods) / EIU_ID, set(EIU_FILES + SHARED_CODE_FILES) if args.data_only else set())
        check_classic_job_pattern(Path(mods))
    if args.package:
        for archive in package(MOD_DIR, cfg.get("target", {}).get("reloaded_mods")):
            print(f"Wrote {archive.relative_to(ROOT)}")


# The mods this one needs, packaged as installed here (the versions the build is checked against): the FFT mod
# loader (with the classic job pattern, if tools/loader_signatures.py --apply was run) and its two libraries.
PACKAGE_DEPENDENCIES = ("fftivc.utility.modloader", "reloaded.sharedlib.hooks", "Reloaded.Memory.SigScan.ReloadedII")


def package(built: Path, reloaded_mods: str | None) -> list[Path]:
    """One zip a mod in mod/steam-deck/, each holding the mod's folder, as Reloaded-II installs a dropped archive."""
    import zipfile

    out = ROOT / "mod" / "steam-deck"
    out.mkdir(parents=True, exist_ok=True)
    folders = [built] + [EIU_DIR] * EIU_DIR.is_dir()
    if reloaded_mods and Path(reloaded_mods).is_dir():
        folders += [Path(reloaded_mods) / mod for mod in PACKAGE_DEPENDENCIES if (Path(reloaded_mods) / mod).is_dir()]
    else:
        print("Note: target.reloaded_mods isn't set, so the package has this mod only; get the FFT mod loader 1.7.5 "
              "and its libraries on the other machine.")
    written = []
    for folder in folders:
        name = MOD_ID if folder == built else folder.name  # (the upgrade's folder is named by its id)
        archive = out / f"{name}.zip"
        with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
            for file in sorted(folder.rglob("*")):
                if file.is_file():
                    z.write(file, Path(name) / file.relative_to(folder))
        written.append(archive)
    return written


def check_classic_job_pattern(mods: Path) -> None:
    """Classic's job table is only found with the pattern from tools/loader_signatures.py."""
    from loader_signatures import LOADER_ID, PATTERNS
    ini = mods / LOADER_ID / "Signatures" / "ffto.ini"
    text = ini.read_text(encoding="utf-8") if ini.exists() else ""
    if f"JobDataTable = {PATTERNS['JobDataTable']}" not in text:
        print("Note: the loader can't find classic's job table yet, so classic gets no job changes.\n"
              "      Run `python -I tools/loader_signatures.py --apply` to fix that.")


def code_only(cfg: dict, install_now: bool) -> None:
    """Compile and check just the code artifacts against the last generated patch data."""
    code = build_code(cfg)
    if code is None:
        sys.exit("The code-only build needs the .NET SDK.")
    mods = cfg.get("target", {}).get("reloaded_mods")
    candidates = [MOD_DIR / CODE_DATA]
    if mods:
        candidates.append(Path(mods) / MOD_ID / CODE_DATA)
    patches = next((path for path in candidates if path.is_file()), None)
    if patches is None:
        sys.exit("--code-only needs a prior full build (Data/patches.json was not found)")
    check_code(cfg, code, patches)
    MOD_DIR.mkdir(parents=True, exist_ok=True)
    EIU_DIR.mkdir(parents=True, exist_ok=True)
    for name in CODE_FILES + SHARED_CODE_FILES:
        shutil.copy2(code / name, MOD_DIR / name)
    shutil.copy2(ROOT / NOTICES, MOD_DIR / NOTICES)
    for name in EIU_FILES + SHARED_CODE_FILES:
        shutil.copy2(code / name, EIU_DIR / name)
    print(f"Wrote code artifacts to {MOD_DIR.relative_to(ROOT)} and {EIU_DIR.relative_to(ROOT)}")
    if install_now:
        if not mods or not Path(mods).is_dir():
            sys.exit("Set target.reloaded_mods in local.toml to Reloaded-II's Mods folder.")
        install(MOD_DIR, Path(mods) / MOD_ID, data_only=True)
        install(EIU_DIR, Path(mods) / EIU_ID, data_only=True)


def install(source: Path, dest: Path, skip: set[str] | None = None, data_only: bool = False) -> None:
    """Copy a built mod over the installed one, removing files the build no longer has.

    Overwrites in place rather than deleting the folder, which fails while
    Reloaded-II or the game has files open.
    """
    skip = skip or set()
    locked = []
    dest.mkdir(parents=True, exist_ok=True)
    for old in sorted(dest.rglob("*"), reverse=True):
        relative = old.relative_to(dest)
        if old.is_file() and relative.parts[0] not in skip and not data_only and not (source / relative).exists():
            try:
                old.unlink()
            except PermissionError:
                locked.append(old)
    for new in source.rglob("*"):
        if new.is_file():
            relative = new.relative_to(source)
            if relative.parts[0] in skip or (data_only and relative.parts[0] not in CODE_FILES + EIU_FILES + SHARED_CODE_FILES):
                continue
            target = dest / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            try:
                shutil.copy2(new, target)
            except PermissionError:
                locked.append(target)
    for folder in sorted((p for p in dest.rglob("*") if p.is_dir()), reverse=True):
        if not any(folder.iterdir()):
            folder.rmdir()  # left behind by files the build no longer has
    if locked:
        listing = "\n".join(f"  {p}" for p in locked)
        sys.exit(f"Couldn't replace these files (close the game and try again):\n{listing}")
    print(f"Installed to {dest}")


if __name__ == "__main__":
    main()
