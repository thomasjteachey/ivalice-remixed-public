# ReMixed outside BATTLE.BIN: SCUS, WORLD, WLDCORE, EVENT/*.OUT

Reverse-engineering notes for the slice covering SCUS_942.21 (new and patched code),
WORLD/WORLD.BIN (new and patched code), WORLD/WLDCORE.BIN patches and the EVENT/*.OUT menu
overlays. Everything here comes from reading the code (tools/disasm.py plus a reference
scanner). The FFHacktics wiki wasn't available, so struct offsets rely on my memory of FFT
PS1 plus what the code itself shows. Each section gives a confidence level.

Conventions: "party unit" means the 0x100-byte roster record at 0x80057F74 + 0x100*slot
(20 slots). "Battle unit" means the 0x1C0-byte unit struct that SCUS builds for battles and
menus. "Menu unit" means WORLD.BIN's party-menu entry: pointers at 0x801CD5EC[i], cursor
index at 0x8018BB20. Hex ability, job and skillset ids use vanilla numbering.

---

## 0. Where settings live (option flags and storage)

**ReMixed stores its options in the event-variable block.** Vanilla never reads or writes any
of these addresses. That block is part of the save, so the options persist per save file.

Event-variable layout. The world engine resolves it at WORLD 0x800FD7C4 and the battle copy
is identical. The base is 0x8005771C (pointer stored at WORLD 0x80153280 / BATTLE 0x80165F9C):
- vars 0x00–0x7F: 32-bit words at `0x8005771C + 4*n`.
- flags 0x80–0x35F: bit `n & 31` of the word at `0x8005791C + ((n-0x80)>>5)*4`.
  Equivalently, byte `0x8005791C + (n-0x80)/8`, bit `n%8`, counted from bit 0.
- nibble vars 0x360–0x3FF: 4-bit fields from 0x80057978.
- WORLD GetVar is 0x800EF1A8 and SetVar is 0x800EF25C. Both go through the event-opcode
  executor 0x800FD4A8.

Option and state bits that ReMixed code reads (none of them are written by any MIPS code I
found, see the end of this section):

| Address / bit | Event id | Read by | Meaning (best guess) |
|---|---|---|---|
| 0x80057926 & 0x20 | flag 0xD5 | WORLD 0x8013DFF0 | Learn an ability without enough JP (free learning) |
| 0x80057926 & 0x40 | flag 0xD6 | WORLD 0x8013DFF0 | Toggle (unlearn) learned abilities without the refund path |
| 0x80057926 & 0x80 | flag 0xD7 | WORLD 0x8013DFF0 | Unlearn refunds half the JP cost instead of all of it |
| 0x80057927 | flags 0xD8–0xDF | BATTLE only (0x80151014, 0x80158218, 0x8018626C) | battle-side: 0xDC the party level for Improved Level Scaling, the death counter (0xD8–0xDB, 0xDD), 0xDF the Brave rule for attacks (MECHANICS.md) |
| 0x80057928 & 0x10 | flag 0xE4 | SCUS 0x8005ADB4 | **Improved Level Scaling**: under-levelled fixed-level enemies scale to the party |
| 0x80057928 & 0x20 | flag 0xE5 | WORLD 0x8013DD40 (GetVar 0xE5) | Monster-dismissal "poach" always uses the base chance |
| 0x80057928 (other bits) | 0xE0–0xE7 | BATTLE 0x801605C0, 0x801849C4, 0x8018C6DC | battle-side |
| 0x80057929 & 0x03 | flags 0xE8/0xE9 | SCUS 0x8005B2D0 | **Enemy Job Level Progression**: forced chapter tier 1–3 (0 = use the story chapter) |
| 0x80057929 & 0x04 | flag 0xEA | SCUS 0x8002D234 | "Two-job" stat growth for every unit (see §3) |
| 0x80057929 & 0xC0 | flags 0xEE/0xEF | BATTLE 0x80160D10 | battle-side scaling of a value (two bits: /2, or ±25% by team) |
| 0x8005792A & 0x20 | flag 0xF5 | SCUS 0x8002D124 | Every unit gets support 0x1E3 (vanilla Non-charge) |
| 0x8005792A & 0x40 | flag 0xF6 | SCUS 0x8005CD24, REQUIRE.OUT 0x801C4950, BATTLE 0x80160EA0 | Rare Equipment (MECHANICS.md: random equipment, steals, invitations) |
| 0x8005792A (others) | 0xF0–0xF7 | SCUS 0x8005C8F8 (& 0x10, flag 0xF4 Starting MP), BATTLE 0x8018D814 (& 4 flag 0xF2 Poison & Regen, & 8 flag 0xF3 Auto Renew); bit 0x80 (flag 0xF7, Monster Reactions) is read by nothing | identified (MECHANICS.md) |
| 0x8005792B | flags 0xF8–0xFF | BATTLE 0x80152684, 0x801849C8 | battle-side |
| 0x8005776C..0x8005776F | var 0x14 (4 bytes) | bytes 0–2: BATTLE 0x8018DEAC, 0x8018D6A8, 0x8017E95C. Byte 3: SCUS | byte 3: bits 0–1 = **power/difficulty mode** (§5), bits 2–3 = enemy R/S/M mode (§6). The exe image starts bytes 0 and 2 as 0x7D and 0x04 |
| 0x80057774 | var 0x16 | BATTLE 0x80160E10 | three percentage bytes; one is picked by team / byte 0x801938D4 and used to scale a value (×p/100) |
| 0x80057778 / 0x80057779 | var 0x17 | SCUS 0x80027084, 0x80059DA4; WORLD storage/reorder code | regular roster size / first guest slot. **Defaults to 0x11 = 17 each** (SCUS writes 0x1111 when the var is 0) |
| 0x8005777C / 0x8005777E | var 0x18 | BATTLE 0x80150454; WORLD 0x800F6F28 writes the upper half | not identified |
| 0x800578DC == 0xFFFF | var 0x70 | SCUS unit loaders; written by BATTLE 0x801510C0–0x801511E8, 0x80154D00–0x80154D58 | special battle-setup mode (keep the ENTD primary skillset, use the battle-side level function, …) |

**Where is the Extra Options menu?** No code in SCUS, WORLD, WLDCORE or the EVENT overlays
writes flags 0xD0–0xFF or vars 0x14, 0x16 or 0x18. I scanned for absolute stores and for
GetVar/SetVar calls with those ids. Because the settings are ordinary event flags and vars,
the menu is most likely an **event script** (dialog choices setting flags/vars), or BATTLE
code writing through a computed pointer. OPTION.OUT's only change is the option-value
renderer hook (§19). Next step: search the event scripts for set-flag or set-var ops on
0xD0–0xFF and vars 0x14/0x16/0x17/0x18. *Confidence: high on the storage layout; low on
which flag is which named option* (only 0xE4 and 0xE8/0xE9 map cleanly to FEATURES names).

Other storage ReMixed adds:
- **Unit storage records** (§10): 10 records × 84 bytes. Records 0–3 are at 0x800579C8,
  just after the event block, so probably saved. Records 4–9 are at 0x80040F00, dead SCUS
  code freed by compacting the display-init routine 0x80040BD8; I can't tell whether these
  are saved. Each stored unit's 16-byte name lives in the unused tails of party slots,
  `0x8005804C + idx*0x200` and `+0x100` (party offsets 0xD8–0xDF of slots 2i and 2i+1).
- WORLD scratch: 0x80141F00–0x80141F70 (storage-menu state). BATTLE also reads the storage
  records: 0x80158670, 0x80159544, 0x80159590 (base 0x80040EAC) and 0x8015B4A8.

---

## 1. Overlay dispatcher (SCUS)

- **Code:** SCUS 0x8002705C–0x800270E4 (routines) and 0x80028650 / 0x80028670 (stubs).
- **Purpose:** ReMixed rewrote a long unrolled vanilla routine (an 8-byte-stride table copy)
  to free space for small trampolines.
- **How they tell world from battle:** they compare the first word of the 0x80067000
  overlay slot.
  - 0x80068A44 means WLDCORE (world map) is loaded.
  - A low half of 0x8888 means BATTLE.BIN is loaded.
- **Branches:**

  | Stub | World map target | Battle target | Returns to |
  |---|---|---|---|
  | 0x8002705C | 0x8013F500 | 0x80152000 | 0x8005DCE8 |
  | 0x800270C0 | 0x8013F430 | 0x801502C4 | 0x8005DCFC |
  | 0x80028650 | vanilla 0x8005B984 | 0x8015511C | |
  | 0x80028670 | vanilla 0x8005C5E8 | 0x80155184 | |

- **Other contents of the freed region:**
  - Small helpers: 0x80027084 (roster size, §9), 0x80027124 (power mode, §5) and
    0x80027150 / 0x80027180 (level helpers, §5).
  - Two 10-byte job-requirement records: 0x80027100 and 0x80027110 (§2).
- **Confidence:** high.
- **Remaster:** no equivalent needed; implement each mechanic directly.

## 2. Bard/Dancer slot sharing and Dark Knight (job unlock and job-id remap)

**Data (job table):**
- Job 0x5B (Bard slot) is now male Dark Knight.
- Job 0x37 (vanilla special "Wizard") is now **female Dark Knight**: same stats as 0x5B,
  skillset 0x4E, portrait 10.
- Job 0x38 (vanilla special "Oracle") is now **male Bard**: vanilla Bard stats, skillset
  0x4D, innate 0x1E5, portrait 11.
- Job 0x5C stays Dancer (female) and also gets innate 0x1E5.

**What the player sees:**

| Grid slot | Male | Female |
|---|---|---|
| 0x5B | Dark Knight (job 0x5B) | Dark Knight (job 0x37) |
| 0x5C | Bard (job 0x38) | Dancer (job 0x5C) |

The unit's job byte stores 0x37 or 0x38. Job levels, JP and learned abilities stay in the
0x5B and 0x5C slot positions (index 17 and 18).

**Unlock check:** SCUS 0x8005DC14 is the vanilla "compute unlocked generic jobs from job
levels" routine (requirements table 0x800660C4).
- **Before:** it masked out Dancer for males and Bard for females.
- **Now:** after the table loop it dispatches through §1.
- **Male** (WORLD 0x8013F500 / BATTLE 0x80152000): sets bit 0x20 (slot 0x5C) only if the
  unit meets the 10-byte record at **0x80027100**. That record is vanilla Bard's chain:
  Chemist 2, Priest 2, Wizard 2, Time Mage 2, Summoner 4, Mediator 4, Oracle 2. Otherwise
  it applies the vanilla male mask (0xFFFFD0).
- **Female** (WORLD 0x8013F430 / BATTLE 0x801502C4): sets bit 0x40 (slot 0x5B) only if the
  unit meets **0x80027110**: Squire 2, Chemist 2, Knight 8, Wizard 8. Otherwise mask
  0xFFFFB0.
- **Male slot 0x5B** uses the normal requirements-table entry, which now holds Knight 8 +
  Black Mage 8.
- **Name override:** callers now pass the unit's name id in **t7** (party +0xCE, battle
  +0x16C, ENTD byte 2). Name ids **0x69 or 0x6A** always unlock slot 0x5C. These are
  unnamed in the vanilla list, so probably ReMixed-added characters. Checks for 0x17
  (Gafgarion) and 0x26 (Izlude) exist but can't take effect because of delay-slot ordering.
- **Monsters** (gender 0x20) still get no generic jobs.

**Job-id remap helpers.** Each converts between grid slot and real job by gender
(menu-unit byte 112 & 0xC0):
- Slot to real job (0x5B → 0x37 if not male, 0x5C → 0x38 if not female):
  - WORLD 0x8013F700 (job change, hook 0x80122950)
  - 0x8013FC00 (0x80122824)
  - 0x8013FCC0 (0x80128F48)
  - 0x8013FE80 (0x80128BE8, only when flag byte 0x8013D8AC is set by 0x8013FF40 / hook
    0x8012895C)
  - 0x8013FF80 (0x8011FE78)
  - 0x80140040 (0x8011F1F8)
  - 0x801401B0 (0x8011F16C)
- Reverse direction (0x37 → 0x5B, 0x38 → 0x5C, or a grid position):
  - 0x8013FE00 (0x80118E34)
  - 0x8013E300 (0x801227D4)
  - 0x8013F7C0 / 0x8013F800 (0x80122970, 0x801224E4)
  - 0x80140100 (0x80125614)
- **JP gain:** 0x8013FD80, hooked from SCUS 0x8005DDA8 inside the add-JP routine
  0x8005DD48. It maps male 0x38 → slot 0x5C and female 0x37 → slot 0x5B before indexing
  the JP arrays at party +0x6E and +0x96.
- **Battle-prep job screen** (JOBSTTS.OUT 0x801E0270): the same gender logic. The unlock call
  now passes gender & 0xC0 and t7 = battle +0x16C.

**Confidence:** high for the unlock logic and data; high for the remap pattern; medium on
which UI list each remap hook feeds.

**Remaster:** this is the biggest structural change. The remaster has its own job ids and
its own Dark Knight/Onion Knight slots, so decide early whether to keep "one slot, two
jobs by gender" or to give Bard, Dancer and Dark Knight separate slots. If you keep it, you
need hooks for:
- the unlock rule
- the job-change id mapping
- the JP and job-level slot mapping
- the job-grid display

## 3. Two-job stat growth (ability 0x1E5, flag 0xEA)

- **Routine:** SCUS **0x8002D200**.
- **Entry:** 0x8005AA6C (end of party-to-battle build 0x8005A9BC) and 0x8005B66C (job-to-unit
  copy in 0x8005B620).
- **Vanilla:** both copied the job's 10 growth/multiplier bytes (job +0x0D..0x16) to battle
  unit +0x81..0x8A.
- **What it does:**
  - Applies when flag 0xEA is set, or the unit has ability **0x1E5** as support or as one of
    its four innates. 0x1E5 is the Bard (0x38) and Dancer (0x5C) innate.
  - Needs a secondary skillset (+0x13) and a non-monster sprite.
  - It finds the job that owns the secondary skillset:
    - skillsets 0x05–0x17 → jobs 0x4A–0x5C
    - skillset 0x4E → job 0x37
    - skillset 0x4D → job 0x38
    - otherwise, for special characters, the job equal to their sprite id
  - For each of HP, MP, SP, PA and MA it stores **min(growth)** and **max(multiplier)** of
    the primary and that job. That's the best of both.
  - A check on +0x92 bit 0 is dead code.
- **Confidence:** high.
- **Remaster:** hook the stat-growth/multiplier lookup (level-up gains and displayed stats).

## 4. Learned-ability passives and reaction priority (SCUS)

- **Routine:** **0x8002D000**, entered from 0x8005B820, the tail of 0x8005B7A0. That vanilla
  routine builds the R/S/M ability bitfield at battle +0x8B: reaction 0x1A6+, support
  0x1C6+, movement 0x1E6+, MSB-first.
- **Support 0x1D0 active** (vanilla "Gained Exp UP"; Calculator innate in ReMixed): turns on
  abilities the unit has *learned* in other jobs. The learned bits are at battle
  +0x99 + 3*jobIndex, byte 2 = RSM slots.

  | Learned RSM | Grants |
  |---|---|
  | Knight RSM#4 | 0x1C8 Equip Sword |
  | Knight RSM#3 | 0x1C7 Equip Shield |
  | Summoner RSM#2 | 0x1CE Half of MP |
  | Time Mage RSM#3 | 0x1E2 Short Charge |

- **Movement 0x1FD active** (vanilla "Move-Find Item", learn rate 0 in ReMixed):

  | Learned RSM | Grants |
  |---|---|
  | Geomancer RSM#3 | 0x1F6 Move in Water |
  | Dancer-slot RSM#4 | 0x1FB Fly |
  | Time Mage RSM#5 | 0x1FA Float |

  It also grants the single best Jump+N:
  - **Jump+3 (0x1EB):** from Dark Knight-slot RSM#3 for males or Dancer-slot RSM#3 for
    females. Sing (0x16) RSM3 is Jump+3 in ReMixed.
  - **Jump+2:** otherwise from Thief RSM#6.
  - **Jump+1:** otherwise from Archer RSM#5.
- **Flag 0xF5:** gives every unit support 0x1E3 (Non-charge), whatever it has equipped.
  Non-charge is no longer JP-learnable in ReMixed.
- **SCUS 0x8005B82C** (sets one ability bit, patched in place): an innate *reaction* is only
  recorded or activated if the reaction slot is empty or holds the same ability. So an
  equipped reaction wins and the two don't stack.
- **Confidence:** high on the bits; medium on the exact RSM-slot names (cross-check with
  ReMixed's skillset table).
- **Remaster:** hook the "has ability X" query or the ability-bitfield build.

## 5. Level scaling and power mode (Improved Level Scaling, Power Modes)

**ENTD unit loader:** SCUS 0x8005AC1C (patched 0x8005AC40–0x8005AE70). The ENTD level
byte L works like this in vanilla:
- 0 or 0xFE means 7/8·P + rand(P/8+1).
- 100 and up means P + (L−100).
- Anything else is fixed.

P is the byte at 0x80066308.

ReMixed changes:
- **Flag 0xE4 (Fixed or Minimum Levels; MECHANICS.md):** a fixed level 0 < L < 100 that is below
  P − P/8 is treated as 0xFE, so it scales to the party.
- **Enemy Level Boost = var 0x14 byte 3 (0x8005776F) & 3 (Normal, Nightmare, No Future):**
  - Fixed-level units on a non-player team (battle +5 & 0x30) get **+49 × mode** levels,
    capped at 99.
  - P is recomputed as **max(57 × mode, highest party level)** (0x8005CBD0 → 0x80027124).
  - Units on errands are excluded from P: party +0xD0 & 0xF ≠ 0 means away (helper
    0x80027180).
  - When var 0x70 == 0xFFFF the level source is BATTLE 0x80150500 instead.
- **Confidence:** high; the options menu (event 0xC9) names the modes Normal, Nightmare
  (Lv + 50) and No Future (Lv → 99).
- **Remaster:** hook ENTD level resolution. The Level Scaling mod already covers the basic
  case.

## 6. Enemy reaction/support/movement selection

- **Routines:**
  - 0x8002D400, entered from 0x8005D100 (vanilla returned the id for ids < 510)
  - 0x8002D500 (helper)
  - 0x8002D680, entered from 0x8005D10C
- **Vanilla behaviour:** SCUS 0x8005D0BC picks R/S/M for ENTD units. Ids ≥ 510 mean
  "random"; smaller ids are used as given.
- **ReMixed, for enemies:** when var 0x14 byte 3 & 0x0C is set and the ENTD team ≠ 0, a
  fixed ability id is only kept if some job the unit could have owns it. That means the
  special character's own job, or any generic job 0x4A–0x5C using the gender mapping of
  §2. A kept ability is marked learned in that job's learned bitfield. Anything else falls
  through to the random pick.
- **Monster random reaction:** 0x8002D680. Monsters (jobs 0x5E–0x8D) get a fixed reaction
  per family from the table at **SCUS 0x8002D700** (48 bytes, +0x100). By family: Counter,
  Counter Tackle, Critical Quick, Speed Save, Counter Flood, Meatbone Slash, Sunken State,
  Counter Magic, Gilgame Heart, Caution, Distribute, A Save, Regenerator, Hamedo, Dragon
  Spirit, HP Restore. Families in order: Chocobo, Goblin, Bomb, Panther, Pisco, Skeleton,
  Ghoul, Flotiball, Juravis, Uribo, Woodman, Bull Demon, Morbol, Behemoth, Dragon, Hydra.
- **Confidence:** medium-high.
- **Remaster:** ENTD post-processing hook.

## 7. Enemy job levels by chapter (Enemy Job Level Progression)

- **Routines:** SCUS 0x8005B2B4 (rewritten ENTD job-level / skillset setup) with helpers
  0x8005B1C4, 0x8005B4B0 and 0x8005B500 (relocated/new).
- **Battle call:** 0x8005B1F8 calls **BATTLE 0x80151A00** (another agent's slice).
- **Chapter tier (0..2):** flag bits 0xE8/0xE9 (0x80057929 & 3) − 1 if non-zero, otherwise
  Ramza's sprite (party slot 0 byte 0: 1/2/3 → tier 0/1/2). This tier is passed down to
  the job-level computation.
- **Other changes:**
  - ENTD byte 30 sets the primary skillset.
  - ENTD secondary 0xFE means random among valid ones.
  - Up to 3 rerolls (0x8005B500) find a job whose skillset matches.
  - Job-level nibbles are combined with max per job (0x8005B4B0).
  - The unlock call passes the ENTD name id (byte 2) in t7.
- **Confidence:** medium (BATTLE 0x80151A00 holds the actual formula).

## 8. Random ENTD equipment (SCUS 0x8005BEA4, in place)

- **Vanilla:** tried weapon categories in a fixed priority.
- **ReMixed:** builds a list of every category the job can equip, honours unit ability bits
  at +0x91/+0x92 (Two Hands and similar), then picks.
- **Confidence:** low-medium.

## 9. Roster rules (party join, lookup, size)

- **Party size:** SCUS 0x80027084, used by the free-slot finder 0x80059F24 and the guest
  range at 0x80059DA4. Var 0x17 byte 0 is the number of regular slots and byte 1 the first
  guest slot. Vanilla used 16/16 out of 20; ReMixed defaults to **17/17** (0x1111 written
  when the var is 0). WORLD storage and reorder code also use byte 0 as the limit.
- **Unit joins party:** SCUS 0x80059BB0 (battle unit → party, rewritten). It handles ENTD
  "Join After Event" (0x10) and "Save Formation" (0x01) with party flag bit 0x02, can
  replace an existing slot, and keeps party +4 & 0xE2. It also copies the name id
  (battle +0x16C → party +0xCE) and the learned/JP blocks in bulk.
- **Find by sprite or name:** SCUS 0x80059F94 now also matches party +0xCE (name id), not
  just sprite.
- **Party → battle copy:** SCUS 0x8005B0D0 (rewritten compactly), and 0x8005A9BC (now calls
  0x8002D200 and changes the special-character branch).
- **Confidence:** medium-high.
- **Remaster:** not needed (checked 2026-11-04): the remaster's free-slot finder (0x2751D0) gives regular
  units slots 0–49 and guests 50–53, so it has 50 regular slots to ReMixed's 17.

## 10. Unit storage ("reserve" units beyond the roster), WORLD party menu

**Routines (WORLD):**
- Store / swap: 0x80142000
- Retrieve: 0x801420BC
- Swap: 0x80142178
- Name move: 0x801421E4
- **Pack:** 0x8014221C
- **Unpack:** 0x80142450
- Record pointer: 0x80142844
- Free-slot finder (wraps at var 0x17): 0x8014287C
- Count: 0x801428D4
- Eligibility: 0x80142918
- Name-list builder: 0x80142980
- Menu handlers: 0x80142B40 and 0x80142E68
- Message-id interceptor: 0x80142C68 / 0x80142C78 / 0x80142C90
- Hooks: 0x80142D60, 0x80142D94
- Unequip to inventory: 0x80142F28 / 0x80142F7C
- New-game clear: 0x80143004

**Entry points:**
- SCUS **0x80042B14**, the `jr ra` of new-game init 0x80042AB0 (gil = 2000), now jumps to
  0x80143004, which empties all 10 records.
- WORLD 0x80114344, the party-menu command dispatcher: new commands 5 and 6 (store,
  retrieve).
- WORLD 0x801140D0: per-frame submenu while storage mode (byte 0x80141EFB) is on.
- WORLD 0x800E4BB8, 0x800ECFD0, 0x80112520, 0x800E50E8: text calls 0x800FE774 / 0x800E6EDC
  for message ids 0xD800, 0xC880–0xC883 and 0x708 now return ReMixed strings at
  0x80141D00–0x80141FAC.
- WORLD 0x8018A5B4 / 0x8018A5C8: pointer table entries changed to new strings.

**What it does:**
- **Pack (84 bytes):** sprite, job, gender, birth month, level, brave and faith (high bits
  carry name-id bits), raw HP/MP/SP/PA/MA, learned abilities and job levels bit-packed
  (3 bits per job level), JP, and the low byte of the name id. Monsters other than sprite
  0x48 store their 8 ability bytes instead.
- **Unpack:** rebuilds a full party record (JP tables from 0x80066182, unlocks via
  0x8005DC14).
- **Storing:** first unequips gear into inventory at 0x800596E0[item]; past a stack of 99
  it calls 0x801207BC with price/4, probably gil. Then it removes the unit (0x80120930).
- **Eligibility:** not Ramza (sprite < 4), slot < var 0x17, not slot 0, party +4 bits
  0x03 and 0x04 clear, party +0xD0 == 0 (not on an errand).

**Confidence:** high on the mechanics; medium on save persistence of records 4–9.

**Remaster:** not needed (2026-11-04): with 50 regular roster slots the remaster already
holds more than ReMixed's 17 plus 10 in storage.

## 11. Roster reorder (WORLD party list)

- **Routines:** 0x80148200, 0x80148278, 0x80148340, 0x801483F0 (byte swap), 0x8014841C
  (errand fix-up), 0x8014824C.
- **Entry:** WORLD 0x8012BCC8 / 0x8012BCE8, list cursor ±1 when a modifier bit (t2 & 4/8)
  is held. Also 0x8010DB28.
- **What it does:**
  - Instead of moving the cursor, swaps the selected unit with its neighbour: 0xD8 bytes of
    the two party records, then re-stamps party +1.
  - Fixes the 8 proposition records at 0x80057EEC (9 bytes each, unit slots at +6..+8) if
    either unit is on an errand.
  - Refreshes the list (0x801140BC, 0x80108920).
  - Slot 0 and slots ≥ var 0x17 are excluded.
  - Byte 0x801481FF suppresses one cursor step after a swap.
- **Confidence:** medium-high.
- **Remaster:** not needed (2026-11-04): the remaster's unit list and formation menu reorder units.

## 12. JP: learning and unlearning abilities (WORLD Abilities → Learn menu)

- **Routine:** 0x8013DFF0, entered from 0x8011FACC. Vanilla there computed "JP after
  purchase" (menu unit +190 = JP, s1 = cost).
- **Buying with too little JP:** fails as in vanilla, unless **flag 0xD5** is set; then the
  ability is learned for free.
- **Selecting an already-learned ability:** unlearns it and **refunds its JP cost**.
  - Refund is halved with **flag 0xD7**; cost 9999 refunds 0; JP cap 9999.
  - **Flag 0xD6:** toggles the ability without the refund path.
  - If the ability is currently equipped as reaction/support/movement (or is 0x1C7/0x1C8
    while 0x1D0 is equipped), it only toggles.
- **List filter:** WORLD 0x8013E400 (from 0x80122BB4) skips JP-learnable abilities (common
  flag byte 3 bit 7) in one list mode (type 3).
- **The toggle:** ReMixed makes the marker 0x801255E4 xor the bit (0x801256A4, vanilla or), and
  returns the byte (t1) and the bit (a2) for 0x8013DFF0 to undo it.
- **Confidence:** high (worked out 2026-11-04: 0xD5 makes the glitch fail, as MECHANICS says).

## 13. Job change keeps a useful secondary

- **Routine:** WORLD 0x8013DE00, entered from 0x80124824 inside change-job 0x801247E8.
- **What it does:** if the unit's secondary skillset (menu unit +96) equals the new job's
  own skillset, the secondary becomes the old job's skillset.
- **The rebuild's innate step** (0x80124B08–0x80124B94, rewritten): for each of five ids read
  from the new job's record (0x800610B8 + 48 × job, +1 on: the four innates, then +9 and +10),
  an equal reaction (+98), support (+100) or movement (+102) is cleared. Vanilla cleared
  Chemist/Throw Item, Monk/Martial Arts, Mediator/Monster Talk and Ninja/Two Swords only.
- **Confidence:** high.

## 14. Dismissing a monster gives its poach item

- **Routines:** WORLD 0x8013DC80 (from 0x80120634) and 0x8013DD40 (from 0x80120704).
- **Context:** the party-menu Dismiss confirmation.
- **When it applies:** the dismissed unit is a monster (job 0x5E–0x8D) and a party member
  has Secret Hunt (0x801228CC: menu unit +294 & 0x40, the built unit's support byte +0x91,
  so support 0x1D7 equipped or innate) and isn't away on an errand (+287 & 0xB == 0; +287 is
  the roster's +0xD0, copied at 0x80121098). The first such member counts: the dialog type
  changes to 0x16, its Brave (+38, the built unit's +0x24, copied at 0x80120D0C) is patched
  into the code at 0x8013DD48, and its roster slot (+44) is kept at 0x801531DC.
- **On confirm:**
  - The roll is 0x8005E0CC(256, t), 0 on a hit, as the battle poach's (BATTLE 0x8018C6EC).
  - t = 31 when flag 0xE5 is set, otherwise 231 − 2 × Brave.
  - A hit gives the rare item, a miss the common one (0x80066064 + 2 × (job − 0x5E) + 1 or
    + 0), as one more at 0x80059494 + item, where the battle poach puts its items too (the
    Poachers' Den's stock; 0x800596E0 is the inventory), unless that's 0xFF.
  - Then the dismissal, through menu state 0xA3 or 0x46 (vanilla's 9), probably its messages.
- **Who can be dismissed** (0x801208F8, rewritten): roster slot not 0, +287 zero, +112 & 2
  clear; vanilla's test was +114 (the roster's sprite set) at least 4. Part of unit storage.
- **Confidence:** high (the messages' text isn't checked).

## 15. Job/equipment lock flags (likely "Mustadio locked after Goug")

- **Routine:** WORLD 0x80125540, the "can change job/equip" check, now takes an unused
  a1 = 0/1. It's called via 0x8013F300 (a1 = 0, 5 sites) and 0x8013F308 (a1 = 1, 5 sites).
- **Vanilla:** in order, any non-zero menu unit +287 (the roster's +0xD0, the errand byte,
  copied at 0x80121098) → −2; +62 non-zero → −3; 0x80120B90(job) → −3; +112 & 4 → −4; a Mime
  (job 0x5D) → −6; else 1.
- **ReMixed:** the same without the first test, then after the Mime: +287 & 0x40 → −6,
  +287 & 0x01 → 0, else 1. The callers read 0 differently (world-in-place-review.md): the
  equipment screens allow it, setting an ability wants 1, and the Job menu's list empties for
  & 0x21. So a unit away on an errand (byte 1) can re-equip but can't change job or set
  abilities; 0x40 locks everything.
- **Greying:** WORLD 0x8013E4A0 (from 0x80116A8C) greys out list entries only for
  +287 & 3.
- **Panel icons:** WORLD 0x8013EA00 / 0x8013EA50 (from 0x80117F24, 0x80117F80,
  0x80117F88) pick a unit panel's status icon (kind 9, 0xB, or ReMixed's 0xC for a locked look),
  by +287 & 0x80 / & 3 and the sprite (≥ 0x90).
- **Confidence:** medium (needs the source of +287, which is probably built from party +4
  flags or +0xD0).

## 16. Custom appearance sprites (0x90+) and per-job portraits

- **Appearance toggle:** WORLD 0x8013E898. A generic unit's sprite set (party +0, menu
  +114) can be set to **0x90 + 2·(job − 0x4A) + female**, with special values 0xB2–0xB4 for
  jobs 0x37/0x38; setting it again restores 0x80/0x81. That locks the unit's look to a
  chosen job. The job comes from 0x80100090 (list selection). Sounds 0x30 / 0x99. I found
  no direct caller (it may be reached through a pointer).
- **Mapping back for display:**
  - 0x8013EA80 (from 0x800E4FE4): turns sprite ≥ 0x90 back into a generic gender sprite
    plus a display job.
  - Also 0x8013E994 (0x8010CAE4), 0x8013E978 (0x8012572C), 0x8013F6A0 (0x80109260), and
    0x8013F620 (no caller).
- **Portraits:** in WORLD 0x800E4FE0, 0x801090DC and 0x8010CACC (rewritten in place),
  human jobs use the job table's monster-portrait byte (+0x2D): male uses value−1, female
  uses value. Otherwise the vanilla Bard/Dancer special cases apply. This matches the data
  change "monster portrait ids set on most human jobs".
- **Confidence:** medium-high for the portraits; medium for the toggle UI.
- **Remaster:** uses HD sprites per job; needs art and a hook. Optional.

## 17. One-handing "Force Two Hands" weapons (equip menus)

- **Routines:** WORLD 0x801498C0 / 0x801498C4 / 0x801498F8 / 0x8014992C, called from 7 equip
  sites including 0x800E8238, 0x8011D5CC, 0x80120B58, 0x80123BF8, 0x801241CC, 0x801246C8,
  0x8012472C and 0x80124A94. The same idea is on the battle side: EQUIP.OUT / REQUIRE.OUT →
  BATTLE 0x80150D50 / 0x80150D54.
- **What it does:** when reading weapon attack flags (0x80063AB9 + 8·id), clears bit 0x01
  (Force Two Hands) if menu unit byte +0x123 & 0x42 is set. Those are probably the
  Two Hands / Two Swords-type support bits. 0x8014992C unequips the other hand (0x801208B8)
  when a forced-two-hand weapon goes on a unit without them.
- **Confidence:** medium (which abilities 0x42 maps to isn't proven).

## 18. World-map encounters (Smart Encounters?)

- **WLDCORE 0x8008F188 → WORLD 0x8013E100:** the random-battle roll (vanilla: rand% <
  location chance) now reads the held-pad word at 0x8019A204.
  - **Bit 0x40 held:** no battle.
  - **Bit 0x20 held:** a battle only if the words at 0x800D0B80 and 0x800D0B7C match,
    probably "at the destination node".
  - Otherwise vanilla.
  - These are probably Cross and Circle; verify FFT's pad bit order.
- **WLDCORE window callbacks 0x8009E690 (57 window types), type 47 (0x8009E74C):** 0x800928AC is replaced by
  WORLD **0x8013E4EC**: ReMixed's missions (story-flow-plan.md, "ReMixed's missions", read 2026-11-06 with the
  PS1's ReMixed running). The world map keeps a stack of windows (count 0x800BB4F0, types 0x800D4580 + 4n, 92-byte
  structs from 0x800BB930) and calls the top one's callback each frame (0x8006C3DC). Type 46 (0x80092208) lists the
  entries whose flag 0x1A4 + i is set; choosing one shows its text (world message 0x5A8 + i) under type 47, and
  ReMixed's callback takes Start there: var 0x27 = the entry's battle from WORLD 0x8013E4BC, world flag 0x80057966
  bit 0, 0x8004D950 |= 4, a sound, prompt 0xE; when it closes, the world map leaves for the battle. Other buttons
  close the text as vanilla's did. Seen in the PS1's: idle on the map is type 0, its menu type 1, a town's menu 4,
  the bar 5, rumors 8; type 47 never runs from those.
- **Confidence:** high that the pad bits gate random battles; high on the missions (run in the PS1's).
- **Remaster:** not needed: the enhanced build handles random battles its own way.

## 19. EVENT overlays

All of these load at 0x801BF000 / 0x801DF000 in the battle context (formation/battle menus),
so their new targets are BATTLE.BIN routines.

- **OPTION.OUT 0x801C15DC (rewritten):** the option-value text renderer (◀value▶ with
  0xD9BE / 0xD9BF arrows) can draw two strings. It jumps to **BATTLE 0x80160DB0**, which adds
  a prefix (0xFA 0xE3 …) based on battle-unit +0x1BA & 0x30. Also 0x801C1704: a 2-bit
  setting gets 12/8 instead of 8 entries. This is probably the hook that displays ReMixed
  settings in the Options screen; follow it from the BATTLE side. *Low confidence.*
- **JOBSTTS.OUT:** 5 calls to 0x801E0390 now go to BATTLE 0x80152500. It also has the
  gender slot logic at 0x801E0270 and the unlock call with t7 / 0xC0 at 0x801DFFE4 and
  0x801E006C. *High.*
- **EQUIP.OUT:** weapon-flag reads go to BATTLE 0x80150D50/D54 (see §17), plus a few
  layout constants (0x801C5F58: 0xBE/0x58 → 0x92/0x70, etc.). *Medium.*
- **REQUIRE.OUT:** 11 hooks into BATTLE 0x80150CB8, 0x80150D54, 0x8015B100/154/1A8,
  0x8015C880 and 0x801570C4. 0x801C4930 adds a condition using flag 0xF6, monster bit 0x20
  and unit +0x59 & 0x40. 0x801C554C / 0x801C5564 use 0x8005A8A4 (get job entry).
  Probably the battle-prep job/ability screen. *Low.*
- **BUNIT.OUT:**
  - 0x801C07D0 → BATTLE 0x801526C0.
  - Unlock call with gender & 0xC0 (0x801C7294).
  - 0x801C7394 / 0x801C73B8 → BATTLE 0x8015ECC0 / 0x8015ECE0 (gender checks).
  - Reads 0x80057B1F.
  *Medium.*
- **ATTACK.OUT:**
  - The battle action menu: about 7 KB of vanilla code removed at 0x801CD418–0x801D0118,
    a new routine at 0x801CB7DC (1.3 KB), and many hooks into BATTLE 0x80152EC0 / 0x80152FF0
    / 0x801606DC / 0x80160800 / 0x80160890 / 0x80160A70 / 0x80160CB0 / 0x80153260 /
    0x80157080 / 0x8015703C.
  - 0x801C86A8 (834 bytes) also has the gender job remap through 0x8005A8A4.
  - I didn't analyse it further: it's battle UI and belongs with the BATTLE agents.
    It probably covers Swap Ability Preview Targets.
- **ETC.OUT:** this overlay starts with "WAIT FOR FILE ALLOCATION ERR…" strings, so it's a
  file loader. The 808-byte rewrite at 0x801BF028 adds a second path for resource indices
  16–146; probably an extended asset table for new files. *Low.*
- **CARD.OUT:** the event save prompt; its changes are the save's 0x2000 bytes with unit storage's
  records in the tail (0x801C046C / 0x801C0480 copy them via 0x8005E254) and the header's unit.
  See overlays-in-place-review.md.

## 20. WLDCORE miscellany

- **0x80073D54 and the twin at 0x800758xx (party-count loops for propositions):**
  - Vanilla counted slots 0–15 with sprite ≥ 0x4A (generics only).
  - ReMixed counts slots 0–19 with sprite ≥ 4 (anyone but Ramza).
  - So **special characters and guest slots can be sent on errands.** *Medium.*
- **0x800715C4, 0x80074C48, 0x80081888 (+0x800818A4):** reads of byte 0x80057CE8 and of
  nibble vars 0x360+i bit 4 are forced to 0. Corrected 2026-11-04: 0x80057CE8 is the number of
  errands under way (the 9-byte records at 0x80057EEC, flag 4 a finished one, +4 its town), and
  0x80074C40(town) counts the finished ones (−1: any town). Two town menus build their entries
  from these (0x8007148C: entry 2 when this town has a finished errand, entry 3 while not every
  errand is finished; 0x800817xx: entry 3 while errands are out or a proposition var has bit 4).
  With the counts at 0, ReMixed never shows those entries. It's not a cap: ReMixed hides the
  errand entries, presumably reporting errands another way. Which entries they are needs
  ReMixed's menu texts. *Medium on the code, low on the meaning.*
  Read again 2026-11-04: 0x80074C40 itself reads the count as 0 (0x80074C48), so it always
  returns 0, and the first builder's entry 2 (a finished errand in this town: the report) and
  entry 3 (not every errand finished) and the second's entry 3 all stay hidden. No other hook
  into WLDCORE reports errands (the changed calls are the world refresh, a route update, var
  0x31's copy, the random-battle roll and opcode 4), and the menu texts are compressed. Not
  ported: the remaster's own town menus and errand reports stay, as hiding the entries alone
  could leave a finished errand with no report.
- **0x80081BEC loop:** counts flags 0x321+ over 47 instead of 31, threshold 45 instead of 31.
  It's probably a "found all treasures/wonders" check expanded for ReMixed's extra entries.
  *Low-medium.*
- **0x8008E658 → WORLD 0x8013DE40:** before setting var 0x31 it copies the old value into
  var 0x2B ("previous X"). *Low on meaning.*
- **0x80083220:** adds SetVar(flag 0x1FC, 0) before a route update. *Low.*
- **0x80068590:**
  - The world-refresh check now reads var 0x55 directly, clears it, and also calls
    0x8012F0F8(2).
  - The matching SetVar(0x55, 0) at WORLD 0x801067B8 was removed.
  *Medium.*
- **0x800798C0 / 0x80079A64:** a table base is moved from a computed value into a stack
  temp at 0x8009DD54. *Low.*

## 21. Text engine tweaks (possibly Cross/Scene Skip or text speed)

- **WORLD 0x8013ED00** (from 0x800E41AC): the per-message constant 0x73 can be overridden by
  var 0x53 when 0 < var 0x53 < 168.
- **WORLD 0x8013EE50** (from 0x800E5B54): at text control bytes 0xF4, 0xF8 and 0xFC–0xFF it
  resets a counter at 0x80153298 and calls SCUS 0x80012518(0).
- **SCUS 0x80032048–0x80032254** (called from WORLD 0x800E4C04, 0x800E4CC0 and 0x800E6CAC):
  - Text compression (scus-in-place-review.md, 0x800320E0): character codes 0x56–0xCF print as two letters from
    the table at 0x80032034 + 2 × code ("th", "in", "an"…), the byte at 0x80032044 picking the first or second.
  - No port at run time (the remaster's text is Unicode); porting ReMixed's help text means expanding these codes.

## 22. Code compaction (no behavior to port)

- **SCUS 0x8001D658:** rotation-matrix routine rewritten more compactly.
- **SCUS 0x80040BD8:** display/draw-environment init compacted. This frees
  0x80040E8C–0x8004168C, used for storage records. Background colour constants changed to
  0x50/0x60/0x55, and 0x80040B38 changes a1 from 0x12A to 0x7D.
- **SCUS 0x80042880:** a resource-pointer getter. When var 0x70 == 0xFFFF it copies 0x280
  bytes from BATTLE 0x8015F600 instead.
- **SCUS 0x8005D4D4, 0x8005C258, 0x8005C8F0–0x8005CF18, 0x8005D278:** other in-place
  rewrites in the unit/inventory code. Several read 0x8005792A or var 0x70, and 0x8005C8F8
  reads flag byte 0x8005792A. **Not fully analysed:** they contain option-dependent
  branches I didn't decode (see "unidentified" below).
- **WORLD 0x800F6F20:** a whole vanilla function is stubbed to `jr ra`. Its first bytes are
  reused for a helper that stores v0 into 0x8005777E (var 0x18 upper half). I don't know
  what the original function did; it uses var 0x44. *Worth checking.*
- **WORLD 0x80153434:** a 68-byte table changed (values 0x0A → 0x04–0x0A). It's data,
  meaning unknown.
- **WORLD 0x8018BB50–0x8018BFD4:** 4bpp image data (menu icons or font), not code.

---

## Unidentified / needs follow-up

1. **The writer of the option flags/vars** (§0). Search event scripts, or have the BATTLE
   agents grep for stores through var-base pointers.
2. **WORLD 0x8013E898** (appearance toggle) has no caller found; also 0x8013F620,
   0x8013CC1C (32 bytes), and 0x80148500 / 0x801406B0 (birthday/zodiac day arithmetic using
   0x800661D0/0x800661E8 with a 365-day wrap; stores into 0x801531DC/E0 for text). They
   may be reached through pointers or be dead.
3. **SCUS 0x8005C258, 0x8005C8F0–0x8005CA5C, 0x8005CC98–0x8005CF18 and 0x8005D4D4** (0x8005CC98–0x8005CE6C, the item pick, is now mapped: MECHANICS.md, Random battle equipment):
   option-dependent tweaks in item/equipment and ability-list code. Only skimmed; they
   read 0x8005792A and var 0x70.
4. **Menu-unit field +287 (0x11F) and +0x123:** where they're built from (needed for §15
   and §17).
5. **The "unit id = 0x80 + slot" lead:** nothing in my slice reads ENTD byte 32 or battle +1
   specially. It must be BATTLE-side.
6. **Records 4–9 of unit storage at 0x80040F00:** does the save routine include them?
   Probably not, unless the save copy range was changed.

## Calls from my slice into BATTLE.BIN new code (for the other agents)

- **Dispatchers** (gender-split job unlock in battle): 0x80152000, 0x801502C4, 0x8015511C,
  0x80155184.
- **From SCUS:**
  - 0x80151A00: ENTD job-level setup, called from 0x8005B1F8.
  - 0x80151A60: from 0x8005B534.
  - 0x80157000: from 0x8005E124.
  - 0x80150500: party-level source when var 0x70 == 0xFFFF, via 0x8005CBEC.
- **Var 0x70 writes:** BATTLE 0x801510C0–0x801511E8 and 0x80154D00–0x80154D58.
- **Unit-storage reads:** BATTLE 0x80158670, 0x80159544, 0x80159590, 0x8015B4A8.
- **Option byte reads:** var 0x14 bytes 0–2, var 0x16, var 0x18, and flags 0xD8–0xDF,
  0xE0–0xE7, 0xEE/0xEF, 0xF0–0xFF (table in §0).
- **EVENT overlay targets:** listed in §19.
