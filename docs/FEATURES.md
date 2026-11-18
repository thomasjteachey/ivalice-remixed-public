# Feature tracker: data, story and art

ReMixed's changes to game data (stats, abilities, items, battles), story and
art, and how each gets to The Ivalice Chronicles. Mechanics that need code on
the remaster, including all of ReMixed's Extra Options, are tracked in
[MECHANICS.md](MECHANICS.md).

Rows marked **seen** were confirmed in ReMixed's data by `tools/tables.py`
(see [the table report](generated/FFT-ReMixed-v1.2.64.23-tables.md)). The rest
come from a summary of ReMixed's release thread.

**Kind:** `data` = table values · `event` = scripts and battle setups ·
`art` = sprites and portraits · `text` = names and help text

**Status:** `todo` · `exists` (an existing remaster mod already covers it) ·
`ported` · `skip` (the remaster already has it)

Ability ids are hex. Where ReMixed reuses a vanilla ability slot, the vanilla
name is shown.

## Jobs

| Change | Kind | Evidence | Approach on the remaster | Status |
|---|---|---|---|---|
| Samurai: innate Two Hands, HP ×75→130, MA ×90→102 | data | seen | Job table (exe) | todo |
| Thief: innate 1CF (Banditry, see MECHANICS); reaction 1B7 Counter Steal (was Gilgame Heart) | data | seen | Job innates, with Banditry and Counter Steal in the code mod | ported (enhanced) |
| Mediator: innate Equip Gun (replaces Monster Talk) | data | seen | Job innates | todo |
| Calculator: innate 1D0 (Two Spells and learned passives, see MECHANICS) | data | seen | Job innates | todo |
| Dancer and Bard: innate 1E5 (two-job stat growth, see MECHANICS); Dancer PA growth 50→80 | data | seen | Job innates, with the growth in the code mod | ported (enhanced) |
| **Bard slot becomes Dark Knight**: Knight 8 + Black Mage 8, swords and heavy armor, Dark Knight skills. Bard (male) and Dancer (female) move to the next slot. The slot logic is in MECHANICS | data/art | seen | Job table, plus the gendered-slot mechanic. Compare with the Dark Knight/Onion Knight mod | ported the remaster's way: ReMixed's Dark Knight on the remaster's own Dark Knight job, Bard and Dancer in their own slots (MECHANICS) |
| New jobs 37 (female Dark Knight) and 38 (male Bard), and job 39 (Dark Knight-style unit with innate Undead) | data | seen | Job table; needs free job slots on the remaster | todo |
| Special-character jobs reworked (about 110 story jobs: stats, equipment, innates, skillsets) | data | seen | Job table | todo |
| Portrait ids set on most human jobs | data | seen | Used by the per-job portrait mechanic (MECHANICS) | settled: held back (the remaster draws each unit's face by its graphic; ReMixed's characters' faces come from their sheets) |
| Dark Knight: Sturdy removed | data | not checked | Job innates | todo |
| Knight learns First Move (movement slot 1F4, Move ×2 until the unit's first turn ends), Momentum (slot 1F8, +10 attack with melee weapons) and a new self-targeted skill in slot 089 | data/code | seen in Knight's skillset | 089 is ported (stats, name, animation, effect); 1F4 and 1F8 are in the code mod (MECHANICS) | ported (enhanced) |
| Lich and Dark Crusader: innate Undead | data | not checked | Monster job statuses | todo |

## Skillsets and abilities

| Change | Kind | Evidence | Approach on the remaster | Status |
|---|---|---|---|---|
| Geomancy (Elemental) cut from 12 to 4 abilities, now terrain-driven (MECHANICS) | data | seen | Skillset table, plus ability ranges | todo |
| Battle Skill, Charge, Jump, Steal, Sing, Guts and others re-slotted | data | seen (91 skillsets) | Skillset table | todo |
| New abilities in unused slots (165–16E, 1E4, 1E5…) | data/text | seen | Need free ability slots on the remaster, plus names | todo |
| Gained JP Up (1CF) removed from every skillset (it's Banditry now) | data | seen | Skillset table | todo |
| Jump rework (Jump plus Jump Range +1) and six gun skills in old Jump and Charge slots | data/code | seen (names, skillsets, Jump and Charge tables) | Needs the mechanics (MECHANICS). Until then the Jump skillsets stay vanilla and the gun skills are held back | todo |
| 47 abilities get a different visual effect | data | seen | `AbilityEffectNumberFilterData`: 26 ported. 10 are abilities ReMixed retired, and 11 are slots the remaster reused | ported (enhanced) |
| 290 ability definitions changed (range, area, formula, CT, MP, status) | data | seen | Ability tables (NXD) | todo |
| Status-infliction sets reordered (111 of 128 changed), with chance tiers in the flag bits (MECHANICS) | data | seen | The sets through the loader's `ItemOptionsData`; the tier bits, which it can't hold, through the code mod | ported (enhanced) |
| Status attributes: 9 statuses can't stack on Defending | data | seen | Status table | todo |
| Per-ability weapon requirements table | data | seen in code | Read by the code mod's weapon checks (MECHANICS) | ported (enhanced) |

## Items

| Change | Kind | Evidence | Approach on the remaster | Status |
|---|---|---|---|---|
| 87 items changed (enemy level, shop availability, attribute sets) | data | seen | Item tables (NXD) | todo |
| 55 weapons changed (power, evade, formula, status) | data | seen | Item tables | todo |
| 65 item attribute sets changed (stat bonuses, elements, statuses) | data | seen | Item attribute table | todo |
| Move-Find items changed on 8 maps, rare items story-gated (MECHANICS) | data | seen | Move-Find table | todo |

## Story, characters and battles

| Change | Kind | Evidence | Approach on the remaster | Status |
|---|---|---|---|---|
| 230 random battles rebalanced (enemies, jobs, gear, levels, positions) | event | seen | ENTD `fftpack` battle files, ported whole | ported (enhanced) |
| Story battles and scenes: ReMixed moved or rewrote 273 entries, tied to its event scripts | event | seen | With the story port (events plus ENTD together) | todo |
| War of the Lions characters (Balthier, Luso…) via custom events | event | not checked | WotL Restoration exists; decide whether to depend on it | exists? |
| Ashley Riot (from The Lion War) | event/art | not checked | New events and an HD sprite | todo |
| Mustadio's job and equipment locked after Goug | event | lock bits found (MECHANICS) | Event flag plus the lock mechanic | todo |
| New maps (`MAP/TUNNEL`, `MAP/VOLCANO`) | event/art | seen on disc | Map pipeline (Mod Studio) | todo |
| Job unlock: only Bard's requirements changed (to Dark Knight's) | data | seen | Job requirements | todo |

## Base content from The Lion War

| Change | Kind | Evidence | Approach on the remaster | Status |
|---|---|---|---|---|
| War of the Lions-style script | text | 2.3 MB of event changes | The remaster's script is already War of the Lions-based | skip? |
