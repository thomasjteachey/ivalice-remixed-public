# WORLD.BIN in-place patches: the 2026-11-04 review

ReMixed's in-place patches to WORLD/WORLD.BIN that no other note covered (`tools/unreviewed.py WORLD/WORLD.BIN` listed 58
blocks, and 17 more that only call ReMixed's new code), read block by block against vanilla. Addresses are PS1 RAM
unless marked as remaster RVAs (FFT_enhanced.exe, enhanced). Statuses as in the SCUS and BATTLE.BIN reviews:
**ported**, **waits** (for a named feature), **nothing** (display, sound, dead code, re-coding, save layout), **open**.
WORLD is the world map and the menus, which the remaster rebuilt, so most blocks are menu UI.

## The errand byte and the lock check in the remaster

The remaster's errand byte is roster **+0x11E** (the PS1's +0xD0), found in the code, not tried in the game:

- The party menu's record builder (0x284A08, the PS1's 0x80120BB0) copies it into the record's **+0x167** at 0x284E81
  (`8A 87 1E 01 00 00 88 83 67 01 00 00`), the step where the PS1 copies +0xD0 to the record's +287 (0x80121098).
- The errand-candidate check 0x2EE37C wants +0x11E == 0, as WLDCORE 0x80073D54 wants +0xD0 == 0. The join (0x2751A9)
  and the new-unit routine (0x275773) clear +0x11E and +0x120 where the PS1 clears +0xD0 and +0xD2.
- The record's +0x167 is read where the PS1 reads +287: the lock check 0x289298, the dismiss buzzer 0x369CD6, the
  list builder 0x284F6C (0x2850D0), the equipment search 0x289FAC (0x28A01D), the list icons 0x2BEFA8, the card
  status 0x2C287D and greying 0x2C28D7.

The lock check **0x289298** (12 callers) is the PS1's 0x80125540 in the same order: errand byte non-zero −2, monster or
job −3, +0x70 & 4 −4, Mime −6, else 1. ReMixed's version (§15 of world-and-menus.md) drops the first test and adds,
after the Mime: & 0x40 −6, & 0x01 → 0, else 1. Its callers read 0 differently, so in ReMixed:

- **equipment** (the four equipment screens 0x8011ACD8, 0x8011AD60, 0x8011B008, 0x8011B8FC and the equip routine
  0x80124614, all `bgez` or `bltz`) allows 0: equipment locks only on 0x40;
- **setting an ability** (0x801253D4) wants exactly 1: 0x01 and 0x40 lock it;
- **the Job menu's list** (0x8011A468) empties for & 0x21.

So a unit away on an errand (the dispatch writes 1) can re-equip but can't change job or set abilities. The other
bits only come from ReMixed's event data.

## The world and the menus (0x800E0000–0x8011C000)

| Block | What it changes | Status |
|---|---|---|
| 0x800E4F44 | The world's text window (0x800E4D9C): message 0x807 shows the current unit's name (0xA000 + roster +0xCE); for a name id of 767 or more vanilla shows name 767, ReMixed name 0 | nothing (display) |
| 0x800E8058 | A status-window builder counting a skillset of 0xA8 or more as a monster's: a dead copy of BATTLE's 0x801339E0 (no caller in WORLD) | nothing here (BATTLE's twin waits for ReMixed's monsters) |
| 0x800EBD60, 0x800EBD7C | A list window's confirm sound skipped while unit storage's mode (0x80141EFB) is on | nothing (sound) |
| 0x800FE854 → SCUS 0x80027220 | The text's Ramza name buffer (0x801A2458) comes from the first unit with name id or sprite set 1–3 instead of slot 0. Only matters if Ramza leaves slot 0, which ReMixed's reorder and storage refuse | waits: the story port, if its events move Ramza; nothing otherwise |
| 0x80114594 | A party-menu popup's Y 0x78 → 0x5C, room for storage's two commands | nothing (layout) |
| 0x80116B14, 0x80118074 | A monster egg drawn with look 2 and a fixed palette (0x170, 0xFE) instead of its stored look | nothing (looks) |
| 0x801182A4, 0x80118A90 | A unit panel's status icon: a re-coding, and a new kind 0xC drawing ReMixed's icon (0x8013E9D8) for a locked look (sprite set 0x90+) in the Job menu and three others (through the hook 0x80117F88 → 0x8013EA50) | waits: per-job portraits and locked looks |
| 0x80119768 → 0x8013E850 | In the Job menu, Triangle (pad bit 0x10) runs ReMixed's appearance toggle 0x8013E898 (world-and-menus.md §16's missing caller): a generic unit's look locks to the highlighted job (0x90 + 2 × (job − 0x4A) + female, 0xB2–0xB5 for 0x37 and 0x38) and back | waits: per-job portraits and locked looks |
| 0x8011A468 | The Job menu's list (0x8011A378) empties for errand byte & 0x21 instead of any non-zero | waits: the job and equipment locks |
| 0x8011ACD8, 0x8011AD60, 0x8011B008, 0x8011B8FC | Equipment screens call the lock check through 0x8013F300 (a1 = 0, unused): only the check's rewrite matters | waits: the job and equipment locks |
| 0x8011D630, 0x8011D664, 0x8011D6C8, 0x8011D784, 0x8011D80C, 0x8011D840 | The weapon's hand labels (Two Hands, Two Swords…) on the equip screen take ReMixed's redrawn graphics | nothing (menu UI) |
| 0x8011DE14, 0x8011DEFC, 0x8011E1A4, 0x8011E73C | The Abilities screens call the lock check through 0x8013F308 (a1 = 1, unused) | waits: the job and equipment locks |
| 0x8011E298, 0x8011E45C, 0x8011E474, 0x8011EB10, 0x8011EB54, 0x8011EBA8, and 0x80122E88 | The movement-ability screen's Move/Jump preview, rewritten (0x80122E88) round a nibble table at 0x8013E180 (one byte per id 0x1E6–0x205): Move+1–3, Jump+1–3, Athlete (0x1F7) Jump +1, Pass (0x1FC) Move +1, and Move-Find's learned Jump+N; the callers' bound widens from 6 ids to 255 | nothing for the preview (the remaster's menus show the built unit's Move and Jump); the battle rule it mirrors is ported, see below |
| 0x8011F2F8 | A list callback's text id: `addiu` → `ori`, so flagged entries keep 0x7000 + id | nothing |
| 0x8011F8BC (with 0x8011F868–0x8011F89C), 0x8011F934 | The learn screen: a learned entry goes to vanilla's state 5 only with flag 0xD6, else the purchase path, whose JP step is ReMixed's unlearn; and a re-coding | ported (`Patches/Unlearn.cs`) |

## The party menu (0x8012034C–0x80125B00)

| Block | What it changes | Status |
|---|---|---|
| 0x801203A0, 0x801203CC | After a dismissal, each (sprite set, story var) pair matching the unit sets the var to 0xC. ReMixed's table (0x8013DE80, room for 23) changes Agrias's, Rafa's and Malak's vars (0x3F4–0x3F6), drops Mustadio's, adds Zalbag (0x3C4) and Gafgarion (0x3CD). The remaster: loop 0x369BB3–0x369C30, table 0x71F090, whose own 0x3F4 and 0x3F5 clash with ReMixed's | waits: the story port |
| 0x80120450 | The dismiss buzzer only for errand & 0x01; other bits give the unit's refusal line (0x801208F8). Both refuse | nothing (feedback; the remaster's 0x369CD6) |
| 0x80120BF8 | The record's +2 = 2 (a guest marker, readers not found) by roster +4 & 0x02 instead of slot ≥ 16 (the remaster's builder 0x284A08: slot ≥ 0x32 at 0x284A5D) | waits: the bigger roster, the story port's guests |
| 0x80120D94 | The record's monster flag (+0x3E) for a primary skillset of 0xA8 or more (vanilla 0xB0–0xDF) | waits: ReMixed's monsters (the story port). Then 0x284B5B `80 C2 50 80 FA 2F` → `80 C2 58 80 FA 57`, 0x284BBC `80 FA 2F` → `80 FA 57` |
| 0x80120E20, 0x80120E68 → new code, 0x80120F20; 0x80121304, 0x8012152C → 0x8013E200 | A monster's record also gets the roster's equipment and skips the secondary and reaction/support/movement copy; the write-back (0x801212B8) keeps a monster's roster reaction, support and movement (vanilla wrote its third action into the reaction and zeroed the others, so a recruited monster lost the reaction it joined with) and writes its equipment back. The hook keeps a stat baseline (display) | ported for the reaction, support and movement (`Patches/InPlace.cs`: the remaster's write-back 0x285744, rewritten in place at 0x2857DE). The equipment half only with the builder's change (0x284BE8 skips a monster's equipment) |
| 0x80120F88 | The job unlock (0x8005DC14) gets the unit's name id (+0xCE), for world-and-menus.md §2's name ids 0x69 and 0x6A | settled: the gendered slots aren't ported, the remaster keeps Bard for men and Dancer for women and has its own Dark Knight (MECHANICS.md, Gendered Bard/Dancer slots), so nothing to port (the name ids 0x69 and 0x6A that always unlock the slot go with the story port) |
| 0x80121178 | The party list's mode 1 (town shops and context 0x64, units that can change equipment) leaves out only errand & 0x08 (gone) instead of any errand | waits: the job and equipment locks (the remaster's list builder 0x284F6C, its test at 0x2850D0 → & 8) |
| 0x80121204 | Mode 2 (context 0x65, the Soldier Office) lists everyone but eggs instead of monsters only | nothing (the remaster lists all units there) |
| 0x80122548 | The unlock bits with the name id: no caller | nothing (dead) |
| 0x80122660, 0x8012267C → 0x8013F5A0 | The job grid lists slots 0x5B and 0x5C for everyone, as job 0x37 or 0x38 by gender in the job change | settled: the gendered slots aren't ported, the remaster keeps Bard for men and Dancer for women and has its own Dark Knight (MECHANICS.md, Gendered Bard/Dancer slots), so nothing to port |
| 0x80122D6C | The learned reaction/support/movement list marks an ability innate to the current job as not equippable by the job's five ids (+1 on), where vanilla had four fixed pairs | ported (`Patches/JobChange.cs`: the remaster's check 0x289F4C, from the list builders 0x286B73 and 0x28A9E2, jumps to a stub) |
| 0x80124654, 0x80125410 | Equip and set-skill call the lock check through 0x8013F300 and 0x8013F308 | waits: the job and equipment locks |
| 0x801258A8 | The job screen's sprite record from ReMixed's table 0x80140800: generic looks 24 + 2 × (job − 0x4A) + female for every job | nothing for the gendered slots (settled, MECHANICS.md); the per-job looks wait for per-job portraits (display) |
| 0x80125AA0 | The daily monster step (0x80125A04, from WLDCORE's day change) skips roster units whose errand byte is set: a monster away doesn't breed, an egg away doesn't hatch, units away don't feed a hatchling's level | ported (`Patches/Breeding.cs`: the remaster's step 0x289900, its empty-slot test 0x2899C1 jumps to a stub) |

## Saving (0x8012D000–0x80131000) and the towns

| Block | What it changes | Status |
|---|---|---|
| 0x8012DCDC | Restoring the save's RAM copy also restores unit storage's records 4–9 (0x801E8000, 0x200 bytes) | waits: unit storage |
| 0x8012FA98, 0x8012FC64, 0x8012FFB8, 0x80130308, 0x8013032C, 0x80130360, 0x80130394, 0x801303FC, 0x801305B0, 0x8013088C | The save's unused tail (+0x1E00) holds storage records 4–9; the checksum covers 64 × 128 bytes; the card's read and verify size 0x2000; the rest re-coded. So storage is saved (world-and-menus.md's unidentified #6) | waits: unit storage (the port keeps its own file) |
| 0x8012FDE4, 0x8012FE14, 0x8012FE3C | The save header's unit is the first listed instead of Ramza | nothing (display) |
| 0x8012F170, 0x8012F7EC | The save screen's start, compacted; ReMixed's event-triggered save (mode 2, var 0x55) takes mode 0's path | nothing (comes with the story port) |
| 0x801382F8 | A town screen counts slots 0–19 instead of 0–15 (the Soldier Office's count) | nothing (display; the bigger roster) |
| 0x8018BC3C | Pixel data | nothing |

## Battle rules found through the menus

- **Athlete and Pass** (SCUS 0x8005C904–0x8005C980, counted as documented only because a MECHANICS row mentions a nearby
  address): after Move+N and Jump+N, Athlete (0x1F7, +0x95 & 0x40) adds 1 to Jump and Pass (0x1FC, +0x95 & 0x02) 1 to
  Move. Ported (`Patches/MovementBonus.cs`: the remaster's Move and Jump step 0x277C58, its caps at 0x277CCC).
- **The dismissal's poacher** skips a unit away on an errand (& 0xB): ported (`Patches/Dismissal.cs`, roster +0x11E).
