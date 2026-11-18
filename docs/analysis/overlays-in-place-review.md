# EVENT overlays' and WLDCORE's in-place patches: the 2026-11-04 review

ReMixed's in-place patches to EVENT/ATTACK.OUT, REQUIRE.OUT, EQUIP.OUT, CARD.OUT, BUNIT.OUT and WORLD/WLDCORE.BIN that
no other note covered (`tools/unreviewed.py` listed 27, 12, 8, 9, 5 and 7 blocks), read against vanilla. Addresses are PS1 RAM unless marked as
remaster RVAs (FFT_enhanced.exe, enhanced). Statuses as in the other reviews: **ported**, **waits** (for a named
feature), **nothing** (display, sound, dead code, re-coding, save layout), **open**.

## ATTACK.OUT: battle setup and the formation screen

Not a menu overlay: the scenario table and its setup (0x801C34B4: var 0x27 into the map, weather and formation vars),
the formation screen and its table, and their portraits. The remaster's: scenario setup 0x2FB7C8, formation screen
0x2FC870, formation table 0x7AE2D0 (12-byte records, ids 0–0x2FF), scenario table 0x8ECAB0 (24-byte records).

| Block | What it changes | Status |
|---|---|---|
| 0x801C36A8 | "On to the next scenario" (var 0x64) sets var 0x27 to the scenario + 0 where vanilla did + 1 (the special cases stay), so ReMixed's events set the next one themselves. The remaster: the `inc edx` at 0x2FB982 | waits: the story port (vanilla's events rely on the + 1) |
| 0x801C37EC | A delay slot copies the scenario id into s7; no reader found | nothing (dead) |
| 0x801C3F44, 0x801C5A44 | The formation screen's frame 72 → 112 bytes, for the locals of ReMixed's preset-group hooks (0x801606DC on, documented) | nothing (comes with the story port's preset groups) |
| 0x801C4994 | The formation screen's Start: vanilla refuses while the current group has no unit deployed; ReMixed tests deployed + the group's index, so in a split formation (var 0x34 = 2) the second group can stay empty | ported (`Patches/FormationStart.cs`, rule "formation-split": the remaster's 0x2FD230). It applies to the remaster's own split battles too |
| 0x801C5A98 | The L1/R1 handler re-coded as if/else-if, same behaviour | nothing |
| 0x801C6320, 0x801C633C, 0x801C6354, 0x801C637C, 0x801C63A4, 0x801C747C, 0x801C7C88, 0x801C7CDC, 0x801C7D0C, 0x801C7D80 | Coordinates, texture constants and counter positions on the formation screen | nothing (menu UI) |
| 0x801C7FE4 | Writes 1 to a freed SCUS byte (0x80042910) before the battle-start portraits; no reader that uses it | nothing (dead) |
| 0x801C81F0 → BATTLE 0x80157080 | The formation list's portraits: monsters by sprite set, generics by the job's portrait byte, the gendered slots 0x5B and 0x5C | waits: per-job portraits and locked looks |
| 0x801C85B8 | The battle-start portrait loader saves s6 for ReMixed's documented rewrite at 0x801C86A8 | nothing (re-coding) |
| 0x801C952C, 0x801C9890 | The formation screen's text loops over roster slots 0–19 instead of 0–15 (vanilla blanked guests); units on errands still blanked | waits: the bigger roster (guests deployed by hand) |
| 0x801C95D8, 0x801C9690, 0x801C973C, 0x801C97E4, 0x801C9860 | When the text buffer fills, the last copy still happens (with the 20 slots) | nothing (menu text) |
| 0x801CBF54 | Data: formation records 0x1A0–0x1C3 for ReMixed's new battles (empty in vanilla and the remaster) | waits: the story port |

ReMixed changes these tables much more than the tool lists (it sees only what decodes as code): 396 of the 768
formation records (+9 becomes its preset-group index, which the remaster still reads as a map id at 0x2FB6CB; new
records at 0x14E–0x16F, 0x1A0–0x1C3 and 0x1FA–0x200, of which 0x14E–0x16F clash with the remaster's own) and 464 of the
scenario table's 480. All story-port data.

## REQUIRE.OUT: the end of a battle

Writes the battle units back to the roster, removes the lost (crystal, treasure) and the departed, decides who joins
(0x801C4930), shows the party-full dismissal screen and adds items back. The remaster's: 0x2C60E4 and around
(0x2C4D1C–0x2C9xxx).

| Block | What it changes | Status |
|---|---|---|
| 0x801C2B14, 0x801C5C54 | Two status windows count a primary skillset of 0xA8 or more as a monster's (vanilla 0xB0–0xDF). The remaster: 0x2C4F35 | waits: ReMixed's monsters (the story port; display) |
| 0x801C2CEC, 0x801C5D74 | The same windows' two-handing mark, through ReMixed's hook 0x80150D54 (one-handing; here it reads EQUIP.OUT's record, which REQUIRE doesn't own) | nothing (display) |
| 0x801C3A38 (with 0x801C3A5C–0x801C3A84) | Lost or departed special characters set a story var (0xC dismissed, 0xD crystal, 0xE treasure, 0xF left): ReMixed's table (0x801D0ADC, 16 entries) adds the guests (Agrias 0x3F4, Rafa 0x3F5, Malak 0x3F6, Zalbag 0x3C4, Gafgarion 0x3CD). The remaster: 0x2C50E4, its table .rdata 0x680E90, whose own 0x3F4 and 0x3F5 clash | waits: the story port |
| 0x801C3E38, 0x801C3ECC, 0x801C3F28, 0x801CA6B8 | The final battle's special case by event id: ReMixed's id 0x7B where vanilla's was 0x145 | nothing (its own ids; comes with the story port if those do) |
| 0x801C4878 | The roster write-back after a battle: a unit with Join After Event (+6 & 0x10) and without & 1 is dropped, the guest marker cleared and the unit joins for good; a guest without the join flag stays in the roster (vanilla dropped slot 16+). The remaster: 0x2C6225 (slot ≥ 0x32) | waits: the bigger roster (the story port's guests) |
| 0x801C610C | The party-full dismissal list: slots 0–19, without guests (+4 & 2) | waits: the bigger roster (menu UI) |
| 0x801C678C | That screen refuses Ramza by his id (+1 == 0) instead of sprite set under 4 | nothing (the same in practice) |

## EQUIP.OUT: equipment on the formation screen

| Block | What it changes | Status |
|---|---|---|
| 0x801C5EEC, 0x801C5FF0, 0x801C60AC, 0x801C6134, 0x801C6168 | The weapon's hand labels: "Force Two Hands" isn't drawn with supports 0x1C7 or 0x1CC (the unit's +0x8F & 0x42), and ReMixed's redrawn label graphics | nothing (menu UI; the display side of one-handing) |
| 0x801C68E4 | The record's two-handing flag (0x801C688C): a forced two-handed weapon doesn't count as two-handed with 0x1C7 or 0x1CC. The remaster: 0x284998 (10 callers), test at 0x2849E7 | nothing (display; one-handing itself is ported) |
| 0x801C84A4, 0x801C8508 | The equip-slot check (0x801C83D0): with 0x1C7 or 0x1CC neither the new item's nor the other hand's "Force Two Hands" (weapon data +1 & 1: axes 0x30–0x32, bows 0x53–0x5B) applies, so an axe or a bow can go with a shield or another weapon. The remaster: 0x288788 (party menu and formation callers), the new item's bit at 0x288830, the other hand's at 0x28887E | ported (`Patches/OneHanding.cs`) |

One-handing, the menus' side: two small stubs in 0x288788 (the two `and`s give 0 when the record's equip supports
have 0x42; the record [0x1800F50 + 8 × rbp], the supports probably at +0x16C, inferred), the test in 0x284998 (some
callers pass a copy of the hands, so the supports must come per caller), and probably the optimize step 0x287D9C
(0x288111). The battle needs nothing: the Two Hands doubling (0x309ED1) doesn't read the forced bit.

## CARD.OUT: the event save prompt

The save screen events open ("Do you want to save?"): it builds the same save image as WORLD and writes it. Loading
goes through WORLD.

| Block | What it changes | Status |
|---|---|---|
| 0x801BFF70, 0x801BFF98, 0x801C0068, 0x801C08D8, 0x801C09FC | The save is 0x2000 bytes (unit storage's records 4–9 in the tail), checksummed over 64 × 128, from a buffer moved to 0x801CF000; WORLD's twin is 0x8012FA98 on | nothing to port: unit storage isn't needed (MECHANICS.md, Unit storage), and the remaster keeps its own save |
| 0x801C01BC, 0x801C03B0, 0x801C099C | The save header's unit: the first of slots 0–19 not gone (+0xD0 & 8) instead of Ramza | nothing (display) |
| 0x801C8E14 | Data: the job and monster names the save header shows (ReMixed's jobs and monsters) | nothing (display text, with the text port) |

Not listed by the tool: the save file names (BAFFTR-94221FFTx, so ReMixed's saves don't collide with vanilla's), the
message table (rewordings, a Learn/Refund prompt and a "Poach … w/ …?" prompt, the dismissal poach's) and location
names. All display text.

## BUNIT.OUT: the battle's unit Status screen

Builds a 0x10C-byte record from a battle unit (0x801C6854) and handles the screen (0x801C3674); about 69 KB of the
file's changes are its help text, rewritten in ReMixed's text compression (the party maximum reads 20).

| Block | What it changes | Status |
|---|---|---|
| 0x801C39A8 | The confirm button's sub-page opens for enemies and guests too (vanilla refused units not on the player's team); monsters still refused | nothing (menu UI) |
| 0x801C6AA4 | The record's monster flag for a skillset of 0xA8 or more | waits: ReMixed's monsters (display) |
| 0x801C6D18, 0x801C6D3C | The unlocked-jobs bytes get the name id and both gender bits (as the documented 0x801C728C) | settled: the gendered slots aren't ported, the remaster keeps Bard for men and Dancer for women and has its own Dark Knight (MECHANICS.md, Gendered Bard/Dancer slots), so nothing to port |
| 0x801CF890 (and 0x801CF87C) | Data: two windows' colour | nothing (display) |

## WLDCORE.BIN: the world map's core

Movement and random battles, the location event-condition interpreter (0x80091238, 41 opcodes at 0x8009EE50),
errands (candidates 0x800757B8, the success roll 0x80079844, the dispatch) and the day change. The remaster's
interpreter is 0x343AF8 (44 opcodes, table .rdata 0x684E90).

| Block | What it changes | Status |
|---|---|---|
| 0x80075908 | The errand candidates over roster slots 0–19 (with the documented sprite ≥ 4 change at 0x80075834), so guests and special characters can go. The remaster: its two callers loop to 0x32 (0x2EDEA5, 0x1A32F4), the check 0x2EE37C's sprite bound at 0x2EE3B1 | waits: errands (three byte changes) |
| 0x8007987C, 0x800798C0, 0x800798C8, 0x80079A64, 0x80079A74 | The success roll's level term reads errand-data section 7 (0x8009DD54, rewritten as a copy of sections 5 and 6) instead of spilling from section 5 into 6; every proposition's row index is 1–3, so the values are vanilla's | nothing (same result) |
| 0x8008E55C, 0x8008EC2C | Saves and restores s2 for the documented hook 0x8008E658 | nothing |
| 0x8009128C | The event-condition interpreter: an entry with bit 0x8000 points into ReMixed's condition data (WORLD 0x80149A00) | waits: the story port |
| 0x80091588 | Opcode 4, "character X in the party", skips units on an errand and matches the name id as well as the sprite set. The remaster's opcode 4 (0x344134) matches its +0x230 only at home but its sprite set (0x344189) regardless | waits: errands (special characters on errands): a stub at 0x344189 testing +0x11E |
| 0x80091F50 | "Find Ramza" over 20 slots instead of 16 | nothing |

## SMALL.OUT and DEBUGCHR.OUT (no load address known to the tool)

- SMALL.OUT: a little code, then the unit name list (character names by name id, then the random generic names), with
  ReMixed's names. Display text, for the text port.
- DEBUGCHR.OUT: another copy of the status-window code (the 0xA8 monster test, the one-handing hook's flag read,
  layout). Display only.
