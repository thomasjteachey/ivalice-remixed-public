# BATTLE.BIN in-place patches: the 2026-11-04 review

ReMixed's in-place patches to BATTLE.BIN that no other note covered (`tools/unreviewed.py` listed 79 blocks), read
block by block against vanilla. Addresses are PS1 RAM unless marked as remaster RVAs (FFT_enhanced.exe, enhanced).
Each block is one of: **ported** (with where), **waits** for a feature that isn't ported (named), **nothing** to port
(display, sound or a re-coding with the same effect), or **open** (a gameplay change not ported yet).

Ability names below are ReMixed's own (from its BATTLE.BIN name list): 0x1F4 First Move, 0x1F5 Move-Cleanse, 0x1F6
Terrain Master, 0x1F7 Athlete, 0x1F8 Momentum, 0x1F9 Waterbreathing, 0x1FC Pass (vanilla Silent Walk; in the Thief's
skillset). Job 0x95 is the White Chocobo.

## The action menus and the movement (0x80170000–0x8017C000)

| Block | What it changes | Status |
|---|---|---|
| 0x8017407C | The range dispatcher's table (0x80174068, used by 0x8017A8C0): type 5's entry goes from "no range" to Attack's and Charge's weapon range (0x8017AA08 → 0x8017A518). The remaster's dispatcher is 0x27FE20 | waits: skillset type 5 |
| 0x801740BC | The area dispatcher's table (0x801740A8, used by 0x8017AAF8): type 5 gets the weapon or one-target area (0x8017ABB8), as types 1, 2, 8, 9 and 10. The remaster's 0x27FFE8 | waits: skillset type 5 |
| 0x80174358 | The action routine's type table (0x80174344, used at 0x8018B670): type 5 takes the ordinary path (ReMixed's formula 0x65 at 0x8018B734); Attack's entry moves to 0x8018B7E4, Charge's case, because the gun-skill rewrite overwrote its old entry (0x8018B818). A plain Attack is unchanged | waits: type 5; the gun-skill part needs nothing |
| 0x801743C8 | "Is this unit a mount?" (jobs 0x5E–0x60 and status group 10: 1 a free mount, 2 a ridden one): adds job 0x95, the White Chocobo (in ReMixed's events 0x109, 0x119, 0x172, 0x173). Callers: the movement setup (0x80174844, 0x80174994) and a unit-at-tile lookup (0x8017DE2C). The remaster inlines the job test (0x27B07C, 0x27B187 in 0x27ADB8) | waits: the riding rework and ReMixed's monsters |
| 0x801744C0 | The movement setup (0x80174430; the remaster's 0x27ADB8) keeps the mover's unit at 12(sp) for ReMixed's rewrite of the per-unit tile flags (0x801749A4–0x801749D8): Pass and the riding rules | ported: Pass (`Patches/Pass.cs`); the riding rules wait |
| 0x80174534 | The lava flag: Terrain Master (+0x95 & 0x80) and Float instead of Move on Lava's bit (0x20, now Momentum) | ported (`Patches/InPlace.cs`, 0x27AE4B) |
| 0x80174694 | The movement class: Terrain Master gets the lava class (5) | ported (`Patches/InPlace.cs`, 0x27AF3D) |
| 0x801747C8 | Any Ground's cost of 1 everywhere is gone (0x1F5 is Move-Cleanse) | ported (`Patches/InPlace.cs`, 0x27B029) |
| 0x80176964 | The move routine's Teleport branch (0x801766B4; the remaster's 0x27C7B4, branch at 0x27C981): writes 0xFE or 0xFF (a failed roll) to the snapshot flag 0x8015029F, and a failed Teleport skips the snapshot after the move (0x80153D58), so it can't be undone and rolled again | nothing to port: the remaster has its own Reset Move; whether it re-rolls a failed Teleport is to check in the game (GAMEPLAY_TODO.md, Testing) |
| 0x80179E28 | The Jump range starts at height 0x70, for a Mime too | ported (`Patches/JumpRework.cs`) |
| 0x8017A300, hook 0x8017A45C | The range builder (0x8017A290; the remaster's 0x27F974) for Linear and 3 Directions: the range is doubled for the diamond (0x8017C000), +4 goes on the stack, and ReMixed's carve replaces vanilla's cross: a tile in the range's square off the diagonals, on the caster's row or column unless both bits are set (then any). The caster's own tile is no longer selectable | ported (`Patches/ConeArea.cs`, 0x27FAB5 and 0x27FAD8) |
| 0x8017A7BC, hook 0x8017B910 → 0x8017C030, 0x8017BB24, 0x8017BE68 | The area builder's cone (0x8017B874; the remaster's 0x280B40) | ported (`Patches/ConeArea.cs`) |

## The queue, the action routine and the reactions (0x8017C000–0x801A0000)

| Block | What it changes | Status |
|---|---|---|
| 0x8017C000 | The range builder's doubling, above | ported, with the targeting |
| 0x8017C34C | The tile info (0x8017C158; the remaster's 0x281248) no longer marks a dead unit as absent (+3 = 0x80); the spear's middle-tile check (0x8017B2A0 → 0x801503C0) adds the test back. So a melee strike (0x8017B3F4) or a spear's (0x8017AFC0) aimed at a dead unit's panel resolves to the body. The action routine still skips a dead target unless the action's statuses touch Dead (0x8018BA44), so it matters for abilities that act on bodies. Remaster: the dead test's `cmovne eax, r11d` at 0x2813F4 (from 0x2813EB), the spear's call at 0x28074C (its test at 0x28076A) | ported (`Patches/HittingBodies.cs`, rule "hitting-bodies") |
| 0x8017C5A8, 0x8017C64C, 0x8017C71C | The queue (0x8017C45C; the remaster's 0x281488) no longer sets +0x188 = 1 at the commit for Item, Throw and Attack (Math Skill and Jump the same, 0x8017C6AC, 0x8017C77C): ReMixed's turn state uses new bits (bit 7 acted, set by 0x8015F100 from 0x80183BF0; bits 0, 1 and 3 for Two Spells and riding). The remaster's own +0x1BA = 1 is consistent on its own | nothing |
| 0x8017CE80 | The action stage's start (the remaster's 0x281CE8) also clears ReMixed's recoil flag 0x8015247C (set by formulas 0x13 and 0x39 at 0x80152450, 2 at 0x8018BB94, read by the per-target reset 0x80152480) | nothing (the formulas are ported) |
| 0x8017D19C | The "more strikes?" tail re-coded, same result | nothing |
| 0x8017D4A4 | The action setup's strike count comes from 0x8015DC40: 2 for the AI's Non-charge double cast and Two Shots' pair | ported (`Patches/TwoSpells.cs`, 0x282234) |
| 0x8017D8C4 | The area's unit list (0x8017D850; the remaster's 0x282754), when the sort flag is set: by 16 × the larger distance + the smaller (Chebyshev rings) instead of |dx| + |dy|. Only the cone's order changes (key at 0x28281F) | ported (`Patches/ConeArea.cs`, 0x28281C) |
| 0x8017E5A8 | Reflect's bounce lands on the caster's panel | ported (`Patches/ReflectReaction.cs`, 0x2831DE) |
| 0x8017EB10 | Spillover JP (0x8017EA80; the remaster's 0x283664): no gender test on the Bard (index 0x11) and Dancer (0x12) shares. With 0x8017EBB8–0x8017EBE4 (+0x16C to SCUS 0x8005DC14). Remaster: the tests at 0x2836AD–0x2836CC | settled: the gendered slots aren't ported, the remaster keeps Bard for men and Dancer for women and has its own Dark Knight (MECHANICS.md, Gendered Bard/Dancer slots), so the remaster's gender tests stay |
| 0x8017F020, 0x8017F094, 0x8017F0AC, 0x8017F0D4 | The movement mode | ported (`Patches/MovementMode.cs`) |
| 0x8017F4B4 | Placing units (0x8017F388): each unit's event id +0x161 keeps its sprite set for any non-zero set when +2 (the roster slot) is 0, Ramza, where vanilla kept sets 1–3. Read by event lookups (0x80180CB8), the battle UI, ReMixed's event opcodes (0x801585C4, 0x801591D8) and the death counter's Ramza test (0x80158220) | waits: the story port |
| 0x8017FD04, 0x8017FDA8 | `addu v0, zero, zero` → `or v0, zero, zero` | nothing |
| 0x80181838 | The list builder (0x801817C0) keeps the unit at 12(sp) for ReMixed's hooks: the Limit preview (0x8015D480), 0x8015E3B0 and Two Spells (0x8015E820) | nothing (those are ported) |
| hook 0x80189334 | The Ruins' common tail (0x8018BAE8) | nothing (MECHANICS.md) |
| 0x80189BB0, hook 0x80189C48 | Formula 0x2E, the Breaks | ported (`Patches/Breaks.cs`) |
| 0x8019AA6C | The AI's learned test (0x8019AA50; the remaster's 0x3170CC, at 0x317106–0x317119): skillset 0x4D reads index 0x12, 0x4E index 0x11 (the remaster takes both as all learned); the test of the unit's primary (+0x12) is gone, so a unit whose primary came from battle data counts all of it learned (the menus keep the test) | settled: the gendered slots aren't ported, the remaster keeps Bard for men and Dancer for women and has its own Dark Knight (MECHANICS.md, Gendered Bard/Dancer slots), so skillsets 0x4D and 0x4E are unused; the primary test's purpose is still open |

## The battle event interpreter and its windows (0x8012D000–0x80149000)

Most of this range is the battle event interpreter (its opcode switch is 0x80143D0C–0x80145F24) and its text and
message windows, not the battle menus.

| Block | What it changes | Status |
|---|---|---|
| 0x8012D90C, 0x8012D970 | The event pre-scan for opcode 0x92 only handles instances whose byte 3 is 0; only Jumping units (+0x58 & 4) get its pre-spawned task (0x80148E88), not Floating ones | waits: the story port |
| 0x8012DA90 | The event animation pick (0x8012DA0C): the riding variant (+0x182 & 0xC0 → 0x8008BE54) is off | nothing |
| 0x8012DE64, 0x8012DE84, 0x8012DEB0 | The event camera scroll (0x8012DDF0): scale 0xC00 instead of 0x1000 when 0x800960D0 is 4 | nothing |
| hook 0x801301B8 | Ramza's name for text code 0xE0 comes from the roster unit with sprite set 1–3 (SCUS 0x80027220 via 0x80059F94), not slot 0 | waits: roster reorder |
| 0x8013209C | The text's wait for a button sets the task word 0x80149D48 to 0x33, not 2 (perhaps more buttons advance) | nothing (low confidence) |
| 0x801323F0 | Text code 0xE0 re-coded; its parameter counter doesn't advance | nothing |
| 0x801330F8 | An event's focus on a unit: Y + 295 while it's Jumping (sprite +0x144 & 0x40) | nothing |
| 0x801339E0 | The status window counts a primary from 0xA8 as a monster's (vanilla 0xB0–0xDF) | waits: ReMixed's monsters (the bound 0xA8) |
| 0x80133D44, 0x8013797C | The status window's sub-entries and sub-window | nothing |
| 0x801340BC, 0x801340D4 | Event opcode 0x7A (dismiss a unit, 0x801340AC): the roster search starts at slot 0 and matches the sprite set or the name id (+0xCE) | waits: the story port and the bigger roster |
| 0x80134AD8, 0x80134E08 | The message window's setup (0x80134A74) | nothing |
| 0x80135208 | The window's resize branch lands on 0x8013527C, so ReMixed's 0x8015C540 always draws its tag | open: status chance in the info window (GAMEPLAY_TODO.md) |
| hook 0x80136268 | The result preview (0x801361E8): abilities with +5 & 4 no longer skip it | nothing |
| 0x80136378 | Two loads swapped | nothing |
| 0x801393B8 | The unit list window's palette for player-team entries | nothing |
| 0x80140578, 0x80140658 | The command menu (0x801404CC) holds 8 entries, not 6 | nothing (the remaster's menu takes Two Spells' and Dragon Tech's commands) |
| 0x80140BE0 | The turn-start notice (0x80140B04): "Performing" (0x8002) is gone; with the hook 0x8015F0E0 at 0x80140B88 the charging notice is hidden during Two Spells' turn (+0x188 & 9) | open: Two Spells' menu extras. The remaster's turn-start notices are 0x237494 and its copy 0x2EB408 (+0x61 & 8: 0x8001, "The unit is readying ..."; +0x61 & 1: 0x8002, "The unit is performing. Using another ability will cancel the performance.", c_mes_id_sample): the Performing case (`test al, 1; je` at 0x237554, and its copy's at 0x2EB4CE) is skipped since the persistent performances it was taken out for are ported (2026-11-19, `Patches/Performances.cs`; battle-80150000.md §17) |
| 0x8014138C | Only Jump (0x12) goes straight to targeting; Elemental (0x11) opens its list | ported (skillset types: Elemental is ordinary) |
| 0x801417C0 | The ability banner: no quote for primaries from 0xA8 | nothing |
| 0x80142664 | The battle event conditions (0x801425B0, table 0x800475E0 from ReMixed's opcodes 0xED and 0xEE): 16, not 10 | waits: the story port |
| 0x80142EB4 | The battle event loop: event 0x147's special exit (var 0x27 returning 0x13) is gone (sentinel 0xFFFF) | waits: the story port |
| 0x80143764, 0x80144ADC | Event effects: modes 2 and 3 pass a second unit (0x80165FFA, from opcode 0x18's byte 4) | waits: the story port |
| 0x80143E10 | Event CallFunction (0x43): function 0 resets the inventory (SCUS 0x80042A10), function 1 only adds the joining units (+6 & 0x10) to the roster (0x80134180); vanilla's 1 did both while var 0x27 < 78 | waits: the story port |
| 0x80143FC0 | CallFunction 9 sets var 0x53 to 0xFF; ReMixed's dialog style pick (0x8012FCD0 → 0x80153300) runs only for var 0x53 under 0xA8 | nothing |
| 0x80144590 | DisplayMessage (opcode 0x10) re-coded | nothing |
| 0x80145404 | Opcode 0x60 also clears a sound slot (0x8015A650) | nothing |
| 0x80147DF0 | Opcode 0x99 tests the base team (+0x1BA) instead of the current one (+5) | waits: the story port |

## The engine (0x80060000–0x800F0000)

| Block | What it changes | Status |
|---|---|---|
| 0x8006759C | The command table (0x80067580): 0x800751C8's entry goes to 0x8015EDE8, which empties both charge records and gives the move back: Cancel Casting | open: Two Spells' menu extras |
| 0x8007335C | The action-state 6 popup (0x801419B8, message 0x1838) gets a text parameter (0x80165EF4) | nothing (perhaps a Two Spells notice) |
| 0x80073BD8 | `addu` → `or` | nothing |
| 0x8007752C | The Charge camera step (0x80070640) covers 0x190–0x19D (vanilla 0x196–0x19D) | nothing (display; the gun skills are ported) |
| 0x80079314 | The battle-start fade-in loop is always skipped | nothing |
| 0x80081DCC, 0x80081FE8, 0x800879C0, 0x80087A14, 0x80087AE4 | Unit palettes: every sprite takes its palette byte (+7); sprites 0x9B–0x9E take 256-byte blocks from ReMixed's buffer 0x80151C00 (672 → 1024 bytes); the record's palette applies to any non-monster, 0–7 | waits: per-job looks and unit presets |
| 0x80082B9C, 0x80082C60, 0x800E9B9C, 0x800E9C60 | Animation lookups through 0x80159D00 | nothing (ReMixed's animation table) |
| 0x8008A740, 0x8008A7C0 | Altima's effects (sprite sets 0x41, 0x49) load ability ids 0x1A0–0x1A3 instead of 0xDB/0xDC and 0xB8/0x28, which ReMixed reused | nothing |
| 0x8008DC50 | A dead unit's height for weapon paths (0x8008DC24) is 1, not 0: a marker for ReMixed's bow hooks 0x801AFF94 → 0x80155900 (the target's height) and 0x801AFA98 → 0x80155A90 (each obstacle's), which give a body full height (0x24) on the panel the attacker aimed at. A bow shot aimed at a body reaches it; bodies still block nothing. With 0x801AF7E4 (Jumping units stay in the obstacle list), 0x801B02AC → 0x80160900 and 0x801B0330 (same-team units are no longer skipped), and the prologue of 0x801AF8B4 (through 0x80150408). Remaster: the dead tests at 0x2736E9 and 0x2736B1, the obstacle list 0x3124E8 (heights at 0x312597, the Jump skip at 0x312559), the Arc solver 0x3129E0 (the target's height at 0x312A40), the collision loop 0x31270E (0x312717); the attacker's aimed panel at +0x1AA type, +0x1AC x, +0x1B0 y, +0x1AE level | the bodies part ported (`Patches/HittingBodies.cs`: both height copies, the target height and the obstacle heights; units by their id, +1). The Jumping and same-team changes: ported (`Patches/ArcObstacles.cs`, rule "arc-obstacles"). The same-team tests are in the Arc solver's choice between its two arcs (0x312C73 and 0x312CBC, not the collision loop): ReMixed takes the first arc when the second hits nothing or both hit the same unit, else the second, teams aside |
| 0x8008EE74 | Clears a sound byte | nothing |
| 0x80093D1C | Status display codes (0x80093D08): new ones for Shell, Poison and Oil and bits 38 and 47 | nothing (display) |
| 0x800947AC, 0x8009481C, 0x800948A4, 0x80094950, 0x800949A4 | The sprite set table (0x80094748: type, actor type, ?, height): actor types for sets 0x19, 0x35, 0x37, 0x3A, 0x57, 0x5A, 0x5D, 0x5F, 0x82, 0x97 | nothing (ReMixed's animations) |
| 0x80094860 | The same table: sets 0x46, 0x4A, 0x4B get actor type 0, and 0x4A and 0x4B height 24 instead of 36 (read by the weapon paths too). The remaster's table is .rdata 0x673300, vanilla's values | the two heights ported (`Patches/HittingBodies.cs`); the type and actor type bytes go with ReMixed's animations |

## The five large patches

| Block | What it changes | Status |
|---|---|---|
| 0x80072BF0 (232 bytes) | How an ability's effect is shown (the action struct's ability +0x138, then 0x801A1814, 0x801A1854, 0x801A1880, mode [0x80045980] 1 or 2): ReMixed reads its effect table 0x801B63F0 (0xFFFF none) and asks 0x801609C0, where vanilla tested ids (0, 0x200, 0x92, 0x196–0x19D) | nothing (ReMixed's effect and animation data) |
| 0x800822C0 (422 bytes) | A unit sprite's status colour tint (the sprite's status word +320): vanilla's case per status becomes a table (0x80082408, 4 bytes a case), with a case for status +92 & 2 and a job flag (SCUS 0x800610D2 + 48 × job) | nothing (display) |
| 0x8013FBE4 (388 bytes) | The battle menu's list setup by menu type (0x801817C0, 0x80181B70, 0x80181CB8, 0x80181EF0, 0x80181F14, 0x80182068, 0x801822CC, 0x80182664, 0x801827B4) re-coded, with early exits for types 4, 8 and from 11, which vanilla's chain did nothing for | nothing |
| 0x801491D8 (444 bytes) | An event opcode that changes the units' stats by a delta, each clamped (0x8012DF68): vanilla HP (to Max HP), +57 (to 100), +36 and +38 (to 100); ReMixed adds MP (+44, to +46) and +54–56 (99 or 50) | waits: the story port |
| 0x8014F1DA (308 bytes) | 4-bit graphics in the battle UI's data (icons or glyphs) | nothing (display) |

## Doc corrections made with this review

- battle-80150000.md §10 took 0x80155900 and 0x80155A90 for turn-list markers; they're the bow-path checks above.
- 0x801503C0 (called at 0x8017B2A0) is the spear's check of the unit on its middle tile, not an AI loop.
- The hook at 0x801749D4 is Pass's and the riding rules', not Train's alone (battle-80150000.md, corrected).
