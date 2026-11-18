# ReMixed's event opcodes

Worked out 2026-11-04 for the story port (GAMEPLAY_TODO.md, "Waiting for the story port", step 3).

**They're the Event Instruction Upgrade's** (found 2026-11-12): ReMixed carries Xifanie's Event Instruction Upgrade,
an FFHacktics hack that adds and changes event instructions, and its instruction list names nearly every opcode
below (https://ffhacktics.com/wiki/Event_Instructions; the hack and its changelog at
https://ffhacktics.com/wiki/Event_Instruction_Upgrade). ReMixed has at least its 1.4.3 (ExitTo, 0x0F, is 1.43+;
GenerateUnit, 0xE0, 1.4+). The upgrade's names: 0x00 Not, 0x01 If, 0x02 ElseIf(var) (as guessed below), 0x03 Else,
0x04 EndIf, 0x05–0x09 Switch, Case, Default, Break, EndSwitch, 0x0A DefineSub, 0x0B EndSub, 0x0C CallSub, 0x0D GoTo,
0x0E GoToTarget, 0x0F ExitTo(target, exit type, nest level), 0x14 PlayTrack, 0x15 ResetUnitCalculations, 0x17
GraphicTimeout, 0x2F BlockLoop, 0x8D Inventory, **0x9A–0x9E the text values** (ResetTextIncrement, PushTextVal,
PushTextVar, SetTextVal, SetTextVar: numbers a message shows), 0x9F ButtonPressGoTo, 0xAC LUI, 0xAE SET, 0xAF SETVar,
0xBF Random, 0xC1 LoadAddress, 0xC2 UnitAddress, 0xC3 WorldUnitAddress, 0xC4 SaveAddress, 0xC5 UpdateUnit, 0xC6
TransformUnit, 0xC7 RosterHasUnit, 0xCC ResetTurnCounter, 0xCD PostEvent, 0xCE LoadConditionals, 0xCF Routine, 0xE0
GenerateUnit, 0xE6 AlterParameter, 0xE7 GetTileUnit, 0xE8/0xE9 LoadTileData/SaveTileData, 0xEA LoadENTD, 0xEB
LoadENTDUnit, 0xEC ConditionalDefeat, 0xED LoadDatabase, 0xEE GetTableSize, 0xEF FetchTableRow. Its other changes
are to vanilla instructions the remaster runs as its own, and aren't ported: Effect with a caster, an improved
Focus, ChangeStats, RemoveUnit and Draw upgraded, InflictStatus adding any status, choice boxes answering button
presses, {stayopen} in messages, DismissUnit on the party leader, mounted units' event handling, and turn counters
going on while a unit is dead. Not checked yet: which of those ReMixed's events lean on.

The port is its own mod, in its own repo (eiu-ivalice-chronicles, the submodule `code/eiu`, whose docs/ document it), which ReMixed depends on; what's ReMixed's alone (the
native routines 0xCF calls, the presets 0xCE loads, the dispatch tavern's variables) ReMixed's `RemixedEvents` gives
it through its API.

## Where the interpreter is

The battle event interpreter's per-instruction step is BATTLE.BIN **0x80143D0C**. It reads the instruction at
`[0x80173CA4] + fp` (fp is the offset into the event's code), with the opcode in s4, the address after the opcode in
s1, parameter bytes 0–3 in s2, s5, s6, s7, and the first two parameters as a value-or-variable in s3 (0x80146078).
Vanilla then runs a chain of compares (0x80143D44 on); every handler ends at 0x80145F24, which steps past the
instruction by the length table (0x8014D170). There is no jump table.

ReMixed replaces the instruction at 0x80143D3C with `j 0x801588BC`: its own compare chain runs first
(0x801588BC–0x801597B8, then 0x8015A600 for 0x0F) and falls back to vanilla's at 0x80143D44. Vanilla's chain is
patched in place too: the variable arithmetic's range (0x80143D5C) is widened to 0xAC–0xBE, and the arithmetic
itself (0x8014A018) reads full 32-bit variables (0x80158710 builds the mask) and handles the sets at 0x801597C0.
0x2F and 0xE0 are vanilla's handlers rewritten in place.

Event variables are 32-bit words at 0x8005771C + 4 × id; var 0 holds the last condition.

## The opcodes

"Uses" counts instructions in the events whose code differs from vanilla's (298 events by this count; 103 of them
start with the same ReMixed prologue, `E6 00 01 D1 FF D2…`).

| Op | Params | Handler | What it does | Uses |
|---|---|---|---|---|
| 0x00 | — | 0x801588BC | var 0 = !var 0 (NOT) | (padding too) |
| 0x01 | — | 0x801588E4 | IF: var 0 ≠ 0 goes on; else skips to the matching 0x02/0x03/0x04 (scanner 0x8015880C, nesting by 0x01) | 4366 |
| 0x02 | 1 | 0x801589E8 | likely ELSE IF; reached from a taken branch it skips to the ENDIF | 30 |
| 0x03 | — | 0x801589E8 | ELSE, reached from a taken branch: skip to the ENDIF | 1383 |
| 0x04 | — | (no-op) | ENDIF | 4373 |
| 0x05 | var | 0x80158910 | SWITCH on var: skip to the CASE with its value, else DEFAULT, else ENDSWITCH | 436 |
| 0x06 | u16 | (no-op) | CASE value | 2155 |
| 0x07 | — | (no-op) | DEFAULT | 273 |
| 0x08 | — | 0x801589FC | BREAK: skip to the ENDSWITCH | 1860 |
| 0x09 | — | (no-op) | ENDSWITCH | 436 |
| 0x0A | id | 0x80158A54 | FUNCTION id: when reached, skips its body to the 0x0B | 315 |
| 0x0B | — | 0x80158A6C | RETURN: pops the return offset (0x80158784) and runs from there | 315 |
| 0x0C | id | 0x80158A84 | CALL function id: pushes the offset after it, finds `0A id` from the event's start | 5116 |
| 0x0D | id | 0x80158AAC | GOTO label id (finds `0E id` from the event's start) | 817 |
| 0x0E | id | (no-op) | LABEL id | 711 |
| 0x0F | 3 | 0x8015A600 | with p1 = 3: byte 0x8015029B −= 2 × p2 (not below 0), then GOTO p0 | 8 |
| 0x14 | 1 | 0x80158C64 | 0x80165FD4/D6/D8 = 0, p0, 0; calls 0x8013D278(p0, 0) | 139 |
| 0x15 | — | 0x80158D6C | clears 0x2B bytes at +0x18C of all 21 battle units | — |
| 0x17 | 1 | 0x80158B30 | byte 0x801502C0 = p0 | 2 |
| 0x2F | 4 | 0x80159868 (from vanilla's wait dispatcher, 0x8013ECD0) | **LOOP** p0 var offset: while var isn't 0, back to the start of the parallel block it's in (offset 0: where its task started, after the block's 0x2A, kept at 0x8016986C + task × 0x400), or by the halfword offset (back from the 0x2F, or on from after it); then on. A bit per variable (0x80152AE0) marks a loop running; with p0 the variable counts down a step each time round, from p0 − 1 when the loop starts (p0 of 2 or more). Luso's goblins (0x0EA), 0x100, 0x107, 0x10F, 0x11A and 0x11B loop a unit's gesture while their scene goes on (`op2F 00 70 00 00`, until the scene clears var 0x70). Ported (2026-11-12, `RemixedScript.Loop`; the block's start found by its 0x2A) | 14 |
| 0x8D | 3 | 0x80158E4C | inventory: the count of item p1 (0x800596E0 + item), with or without the party's equipped ones (roster 0x80057F74 and battle units), compared with p2 or changed by it (op in p0's low nibble; capped at 99 or 255) | 49 |
| 0x9A | — | 0x80158DAC | byte 0x8015029A = 0 (the list below) | — |
| 0x9B–0x9E | 2–3 | 0x80158DC4 | add a value or a variable to a list at 0x80165EF4 (index 0x8015029A, or given) | 676 |
| 0x9F | 3 | 0x80158B08 | if the buttons [0x80045944] hold the mask p0–p1, GOTO p2 | 0 |
| 0xAC/0xAD | var, value | 0x801597D8 | var = value << 16 (0xAD: another var's) | 1271 / 0 |
| 0xAE/0xAF | var, value | 0x801597E8 | var = value (0xAF: another var's) | 8493 / 4539 |
| 0xBF | var, u16 | 0x80158ACC | var = random % n | 268 |
| 0xC1 | var, u16 off, base var, size | 0x80159088 | **PEEK**: var = the 1–4 bytes at [base var] + off, unless the base is −1 | 2232 |
| 0xC2 | id, var | 0x801591CC | var = the PS1 address of the battle unit with +0x161 = id, else with +0x00 = id, else 0 | 866 |
| 0xC3 | id, var | 0x80159180 | var = the PS1 address of the roster slot (0x80057F74, 0x100 each) with +0x00 or +0xCE = id, else 0 | 140 |
| 0xC4 | var, u16 off, base var, size | 0x80159088 | **POKE**: the 1–4 bytes at [base var] + off = var | 2442 |
| 0xC5 | id, var | 0x801591C8 | 0xC2, and with var's bit 1 marks byte 0x8019FD65 + slot | 0 |
| 0xC6 | var, flags, job | 0x80159270 | roughly: changes the job of the unit or roster slot at [var], clearing its equipment back to the inventory with flag 4, and rescales its raw stats for the new job's growths and multipliers (0x800610B8, 0x8005E90C) | 4 |
| 0xC7 | flags, u16 id | 0x801594EC | var 0 = whether character id is in the roster (flag 0x80) or another list (0x800579C8, flag 0x40) | 66 |
| 0xCC | slot | 0x80158B44 | clears byte 0x80150284 + slot, or all 0x15 | 0 |
| 0xCD | 3 | 0x801595BC | u16 at 0x8004E5D0 + 2 × [0x800577B8] = (p2 << 10) + (p0 << 8 \| p1) | 397 |
| 0xCE | 2 | 0x801595F4 | reads sectors (0x80044954) from 0x14B08 + p/2 to 0x801DF000 and copies 0x20 + 0x200 bytes to 0x800475E0 and 0x80049A18 | 396 |
| 0xCF | u32 address | 0x80159790 | **calls native PS1 code** at the address (jalr) | 131 |
| 0xE0 | 12 | vanilla's, in place | creates a battle unit from parameters, the roster or another unit (story-flow-plan.md; ported: `Patches/CreateUnit.cs`) | 63 |
| 0xE6 | spec, var | 0x80159020 | **patches the next instruction**: writes (spec >> 6) + 1 bytes of var at its parameter offset spec & 0x3F (skipping other 0xE6s) | 4088 |
| 0xE7 | x, y, level | 0x80158B88 | var 0 = the battle unit on that tile (+0x47, +0x48, +0x49 >> 7), else 0 | 3 |
| 0xE8/0xE9 | 5 | 0x80158BF0 | **TILE** p0 var, p1 byte, p2 x, p3 y, p4 level: var = byte p1 of the map tile at 0x8018F8CC + 8 × (x + y × width + 256 × level) (0xE9: the byte = var). The remaster keeps its tiles the same way (.data 0xD8DCB0). Balthier's 0x098 reads byte 2 of tile (1, 15). Ported (2026-11-12, `RemixedScript`, through `BattleMap.TileAt`) | 1 / 0 |
| 0xEA | 4 | 0x80158C90 | loads a sector set to 0x801DF000 and 0x801BF000, then 0x80158400 per bit | 0 |
| 0xEB | 4 | 0x80158D48 | 0x80158400(p0, p1, p2, p3) | 0 |
| 0xEC | 1 | 0x80158E38 | byte 0x80150299 = p0 | 0 |
| 0xED | 2 | 0x80159680 | reads p1 sectors from 0xB90 + p0 to 0x801DF000 | 0 |
| 0xEE | 2 | 0x801596A8 | var p0 = u16 at 0x801DF002 + 4 × p1 | 0 |
| 0xEF | 2 | 0x801596D4 | table p0 at 0x801DF000, row var p1: unpacks its fields into variables | 0 |

## The remaster's interpreter

The event runner 0x23A868 is the interpreter. [0xD40A90] is the buffer 0xD3CD50: the event loader 0x2340A4 reads
the event's 0x2800-byte block there, writes the script's size into its first u32 and copies the `.e` file's code to
0xD3CD54, so the code starts at offset 4, as on the PS1 (whose code pointer is the TEST.EVT block). Its
per-instruction fetch is **0x23AFD8**: the buffer [0xD40A90], the
offset in r11d (kept in [rsp+0x44]), the opcode read into [rsp+0x58], the parameters into edi, r8d, r13d and r12w;
then a binary tree of compares on the opcode (r9d), no jump table. A handler that's done goes to 0x23B604, which adds
the instruction's length (the length helper 0x2CBE2C) and comes back round through 0x23B63A (`mov r11d, [rsp+0x44]`)
and 0x23AD12. The compares 0xA0–0xA5 work as the PS1's (var 0 against var 1, the result in var 0, through
[0x7830D8]), and so does the arithmetic (0x242700). [0x782A94] is the running event's number (set by 0x23F37C);
event 0x190 has a special path at 0x23AE8E.

## The port (`code/eiu/src/EventInstructionUpgrade`: `EventHooks.cs`, `EventScript.cs`; ReMixed's part `Patches/RemixedEvents.cs`)

The build ships a ReMixed event as `eventNNN.e` with `0E 2C 0E 3C` in front (two LABELs no ReMixed event uses), and
two hooks act only on code that starts with it (at [0xD40A90] + 4; the first try looked at + 0, so the remaster ran
ReMixed's code as its own and a random battle hung on a black screen): the fetch hands each instruction to the C# interpreter, which runs
ReMixed's own and returns the offset to go on from (into [rsp+0x44], then 0x23B63A); the length helper gives ReMixed's
lengths for that code, so the remaster's own scans read it right. The remaster runs everything else.

Ported: the control flow (0x00–0x0F, with the PS1 scanner's quirks), the sets 0xAC–0xAF, 0xBF, 0xC1–0xC4, 0xC7,
0xE6, 0xE7. PS1 addresses go through a map: battle units field by field (+0x00–0x19 as they are, +0x21–0x26 +7,
+0x28–0x4A +8, statuses and +0x8B–0x95 +9, +0x161 the battle-data id at +0x191 (the remaster's own events read
it there), the name id +0x16C at +0x1FC, +0x182–0x18C +0x32, +0x1BA–0x1BF +0x34) and roster fields (+0x00–0x7A,
+0xD0 at +0x11E, +0xCE taken as the name id at +0x230, unconfirmed) and two battle globals: the enemies' level sum
0x80193844 (a word) and their highest level 0x80193920, the remaster's 0x114B0B4 and 0xD405A2 (its battle setup adds
them at 0x2716C5, where the PS1 adds them as units are built, 0x80180DC4). Any other address is kept in a store of
the mod's own, so ReMixed's own data (0x8019FD50, the native routine's) reads back what was written, noted once
in the test mode's trace. ReMixed's word variables (under 0x80) are read and written in the remaster's variable
array ([0x7830D8], var 0 at +0): the getter and setter (0x2332D8, 0x233368) use var 0 as scratch and put it back,
so through them var 0 always reads 0, and the setter cuts a negative value to its low byte (the first Deck run
lost the high half of every PS1 address that way). The base team, PS1 +0x1BA (the remaster's +0x1EE), is read but not
written: event 0x191 clears its enemy bits (0x30) on every unit it counts, which the PS1 survives (why isn't known
yet) but which in the remaster leaves no enemies, so the second Deck run won the battle at once. Passed over for now, noted once: 0xCF's native calls, 0xE0, 0xCD and
the rest.

First event: 0x191, random battles' setup (2,885 bytes, 798 instructions). It renames guests to ReMixed's spellings
(the name text at unit +0x12C–0x137: no place in the remaster, which names units by id), spawns a unit on four maps
(0xE0: a roster unit whose record loads a formation, which the remaster's unit making skips, so nothing is made),
sets event ids, and for the units whose flag 0xC0–0xCF is set, flags it sets from var 0x52, clears
their team bits and runs vanilla's 0x45/0x48/0x3D on them; it also sums the enemies' levels into 0x80193844 and
keeps the highest in 0x80193920 (not mapped yet).

**Flags 0xC0–0xCF (2026-11-11).** They pick which of a battle's 16 units are there, one flag a slot: 30 of
ReMixed's story events set them before their battles, and its after-battle event 8, its retry menu (0x190) and its
preparation menu (0x192) clear all 16; vanilla's events never touch them. 0x191 adds the unit of each flagged slot
that isn't there (0x45, 0x48: the unit by its battle-data id) and removes the unit of each slot that isn't flagged
(0x3D); only when none is set does it flag the slots the game filled, and those stay set. The mod ships no event
8, so the flags of one battle stayed for the next (0xC0–0xC2, 0xC4, 0xC5 and 0xC8 on the PC's world map), and
0x191 brought that battle's slots into the next random battle. A random battle's units come in pairs on one tile,
of which the game places one (Mandalia Plains West 1: its units 3 and 4 at (9,12), 5 and 6 at (2,11), 7 and 8
at (0,2)), so it added units onto tiles already taken: two monsters on one tile, seen on the Deck at Mandalia
Plains (2026-11-07). The build now puts 16 ZEROs of the flags in front of 0x191 (`REMIXED_EVENT_PREFIXES`), as event
8 would have cleared them after the battle before; its own sets after that (the Algus campaign's 0xCC and 0xCD,
the party size's 0xCC–0xCF) stand.

### 0x80151000, Improved Level Scaling's re-run

Unless flag 0xDC (the entire roster) is set or var 0x70 is 0xFEFE, it saves the first 16 units' +0x01, +0x47,
+0x48–0x49, +0x182–0x183, +0x161 and ReMixed's per-unit bytes at 0x8019FD50; clears the first 16 − (var 0x52 − 5)
of those bytes (SCUS 0x8005E644 is a memset); sets var 0x70 to 0xFFFF and re-runs the battle's unit setup
(0x8008E7B8 → 0x8017F8A0, which zeroes the enemy level sum and builds the units again, with P from the battle's
roster units); sets flag 0x1FD; keeps each of 17 units' HP at 0x8019FD7A and snapshots the statuses (0x80148580)
of those whose saved byte was set; puts the saved fields back; adds up the levels again for units with +0x183 = 1
(0x80180DB0); and puts var 0x70 back. The port already does the re-run in C# at each battle's first countdown
(`Patches/LevelScaling.cs`, MECHANICS.md "Improved Level Scaling"), so the event's call goes there: it does nothing
more, except that var 0x70 = 0xFEFE turns that battle's re-run off, and the rebuild now moves the remaster's enemy
level sum and highest level (0x114B0B4, 0xD405A2) by the levels it changes. The per-unit bytes and status snapshots
(0x8019FD50 on) aren't ported: random battles' event clears those bytes.

### Ported 2026-11-05, with the decompiled exe (`tools/decompile_game.py`)

- **0xCD** writes the current event's (var 0x27) word in the remaster's copy of the per-event map settings,
  0xD3F950 (0x6EE bytes, the PS1's 0x8004E5D0, mapped whole for PEEK/POKE): the world setup fills it from the
  scenario records at 0x8ECAB0 (0x18 bytes each: word = b12 + b13 × 0x400 | b14 × 0x100, 0x2FBB15, the very
  formula 0xCD uses), the event runner reads it (0x23EC1D), and it's saved with the game (copied to 0x81CCF2).
- **ReMixed's words**: its variables 0xB00–0xB7F and the list 0x9A–0x9E fill are words at 0x80165EF4, over vanilla
  data in BATTLE.BIN's memory (the same bytes in vanilla: a powers-of-ten table, pointers). The remaster's variables
  stop at 0x3FF (words under 0x80, bits to 0x35F, 4-bit fields to 0x3FF); from 0x400 its address helper (0x242984)
  returns nothing and its executor (0x242700, behind the getter, the setter, the arithmetic 0xB0–0xBE and the
  comparisons) ends the running event (0x2F7EC8). A hook on the executor handles 0xB00–0xB7F in the mod's store,
  which starts as BATTLE.BIN has it (the build writes `eiu-battle-words.bin`, in the upgrade's folder) and isn't reset. 14 changed
  events use them (316 times); ReMixed's other ranges (0x400–0xAFF) don't appear in its arithmetic. The world's
  copy of the executor (0x2EC038, its own variables) isn't hooked.
- **0x14** is vanilla's CallFunction 0xC with its own track: the PS1's 0x80165FD4 (track on channel 1), FD6
  (channel 2) and FD8 (which one plays) are the remaster's 0xD4091C, 0xD408FC and 0xD40900 (both pick the track
  the same way: FD4 when FD8 is 0, else FD6), and 0x8013D278 is 0x234990, which queues the track (0x23C38D does
  CallFunction 0xC). 0x14 sets FD4 = 0, FD6 = the track, FD8 = 0, then plays it.
- **0x8D** (0x80158E4C): p0's high nibble is what's counted (3 the Poachers' Den's stock, 0x80059494, capped at
  255; any other the inventory, 0x800596E0, capped at 99 counting the equipped copies; 2 compares the count with
  the equipped ones), its low nibble the operation (0–4 compare with p2 into var 0: =, ≤, ≥, <, >; 6/7 add or take
  away p2; 9/A/B set, add or take away var p2; 8 the count into var p2), p1 the item. The remaster's inventory is
  0x11A7C00 and its count of an item owned and equipped 0x278FEC (the roster's units not in battle and the player's
  battle units, as the PS1 counts them). The events use only 0x16, 0x26, 0x28 and 0x2A; the Den isn't ported.
- **0x9A–0x9E**: 0x9A sets the list's index (byte 0x8015029A) to 0; 0x9B (u16) and 0x9C (a variable) store at the
  index and step it (wrapping at 0x80); 0x9D (index, u16) and 0x9E (index, variable) store at the given index.
- **Unit fields** (from the job routine, SCUS 0x8005B6xx and 0x276C64, and the unit setup, 0x8005DFAC and its twin
  in 0x27xxxx): PS1 +0x4B–0x4D +8 (the equip types; the remaster has a fifth byte at +0x56), +0x4E–0x71 +9
  (innate statuses, immunities, statuses, their counters, elements), +0x15C–0x162 +0x30 (+0x15E–0x160 the job's
  graphic, portrait and palette, +0x162 its skillset), +0x1B8/+0x1B9 +0x34 (+0x1EC, +0x1ED, cleared with the
  turn's flags), +0x163 +0x194 (a halfword in the remaster: the setup, 0x8005DFF8 and 0x278E0C, clears +0x163 and
  +0x164 where the remaster clears +0x194–0x195 and +0x196) and +0x164 +0x196, so from there on it's +0x32, as at
  +0x182 (the name id, +0x16C, moved on its own to +0x1FC; +0x165–0x181 aren't mapped until checked).

- **0xCE n** loads event n's battle event conditions: ReMixed's presets (sector 0x14B08 + n / 2 of its disc, the
  half n & 1: 0x20 bytes of set offsets, then 0x200 of records), which the build writes for the 512 event numbers
  (`remixed-event-presets.bin`), into the remaster's tables: the sets at 0xD40040 and the records at 0xD3F550, which
  its check (0x2391C4, found as the call before 0x234A94(0, 0xFF, 0xFF) and [0xD4080C] = 1, vanilla's 0x8013D634
  and 0x80166048) walks just as the PS1's 0x801425B0 does: 16 sets, the evaluator 0x239294 with five halfwords, the
  next record by a per-type length (0x672140). Vanilla's own conditions are ATTACK.OUT's table at 0x14938
  (0x801D3938, copied by 0x801C38F4), shipped by the remaster as `fftpack/event_btlevt_bin.bin`.

- **Roster fields** were mapped one to one up to +0x7A, which was wrong from +0x0E: the remaster keeps the
  equipment as halfwords (roster +0x0E–0x1B, a battle unit's +0x1A–0x27), so from EXP to the learned abilities of
  the PS1's 20 jobs it's +7 (the unit build from the roster, 0x276758, beside the PS1's: level +0x16 → +0x1D); the
  job levels and JP after them, for 22 jobs, aren't mapped. A PS1 equipment byte is its halfword's low byte.

- **The buttons** (165 events, nearly all testing Start held, a skip): the PS1's 0x80045944 (held), 0x80045948 (the
  frame before) and 0x8004594C (newly pressed), built from the pad in BATTLE 0x80093654 (L1 + R1 + Start + Select,
  0x90C, resets), are the remaster's 0x1876EEC, 0x187A3F4 and 0x1876EF0, built the same way by its battle-mode loop
  (0x37FB7C; the world map's copy, 0x2CC98E, keeps its own).
- **The event variables as memory**: the PS1's 0x8005771C (words, then the flags a bit each from +0x200, then the
  four-bit fields from +0x25C) is laid out as the remaster's array (0xD40060) and mapped whole.

The readiness report had a bug until 2026-11-05: addresses below the unit or roster arrays counted as their fields
(the roster's errand byte showed as "unit +0xF8", the buttons as a roster field). Counted right, 188 of the 290
changed events need nothing more. What blocks the most now: 0x80075CB3 (18: vanilla's code bytes, read as a table
indexed by a skillset), 0x801A08CB (18: zeros in BATTLE.BIN, kept in the mod's store for now), 0xE0 (17, a unit's
creation), the native routines 0x80160AB0 and 0x80160C00 (9 each), 0x80093CAC (9: a word of vanilla's code set to
0, a patch at run time).

### 0xCD and 0xCE (2026-11-04)

- **0xCD p0 p1 p2** writes (p2 << 10) + (p0 << 8 | p1) into the u16 table at 0x8004E5D0, at the current event's
  number (var 0x27, which the remaster also keeps: its battle setup tests var 0x27 against 0x184). Vanilla's
  event code (BATTLE 0x80142B5C, 0x80142BA4: the word & 0xF300 >> 8), WORLD (0x800F6444…) and ATTACK.OUT read
  that table: per-scenario settings of the map's.
- **0xCE n** (n a u16, value or variable) reads sector 0x14B08 + n / 2 and copies its half (n odd: + 0x400) into the
  event's presets: 0x20 bytes to 0x800475E0 (16 halfwords, offsets into the next) and 0x200 to 0x80049A18
  (records of five halfwords that the event code passes to 0x80142694). Those sectors lie outside the file
  system, after SOUND/ (it ends at 0x14B05): ReMixed's own data, zeros on vanilla's disc. Most events start
  with `0xCE` and another event's number (0x92 loads 0x91's, 0x93 0x92's, 0xC7 0xC6's), so each event's presets
  come from ReMixed's table rather than from vanilla's place for them.

Porting either means finding the remaster's copies of those tables and what writes them; the build would ship
ReMixed's preset table (read from those sectors). Not started: the dialogue-free events that need them
(0x088, 0x092, 0x093, 0x0A9, 0x0C1, 0x0C7) replace short scenes the remaster has versions of its own, and only 8
of the 298 changed events differ only after their END.

### ReMixed's battle-condition types (read 2026-11-12)

A preset's records are checked by vanilla's evaluator (BATTLE 0x80142694: types 1–0x13, 0x16, 0x18, 0x19, 0x25),
whose fall-through (0x80142B14, vanilla's "no") jumps to ReMixed's own (0x8015996C), with the record's halfwords in s2,
s3, s5, s6 and its length table at 0x801696BC:

| Type | Halfwords | What holds | Its presets |
|---|---|---|---|
| 0x14, 0x15 | unit, status | the unit (0x801585B8: the first with battle id +0x161 or sprite set +0 = it) has, or hasn't, the status: bit n of its five current-status bytes (+0x58) from the first's 0x80 (1 Crystal, 2 Dead, 15 Treasure) | 0xEF (0x0F0's rescue: Hirame revived, or crystal or chest), 0x111 |
| 0x17 | — | 0x80183374 < 0 | none |
| 0x1C | var, value | sets var (as 0xBE and 0xB0) and holds | none |
| 0x21, 0x22 | 7 | the count of units (of a team, or that unit's) on a rectangle of tiles ≥ or ≤ n | none |
| 0x23, 0x24 | 3 | two units' distance (x + y) against n (it compares a unit's y, not the distance, past the equal case) | none |
| 0x25 (changed) | team, x, y, level | a unit of that team (+0x1BA's bits 4–5) on the tile; for a team of 4 or more, one of any team but the player's. Vanilla's: the unit whose +4 is the first | 0xA9 (Treasure Hunt's goal tile), 0x5F, 0x7D–0x85 |
| 0x26, 0x27 | unit, n | the unit has finished more, or fewer, turns than n (0x80150284; its 0x26 also compares the unit's address with n, which never holds) | 0xF7 (0x0F8's battle), 0x103, 0x116, 0x11A |

The remaster's evaluator (0x239294) has none of them: 0x14, 0x15, 0x17, 0x24, 0x26 and 0x27 never hold there, 0x1C
and 0x21–0x23 are its own types, and its record lengths (0x672140) give 0x26 none, so a preset with 0x26 was read out
of step from there. **Ported** (`RemixedScenarios.RemixedCheck`, 2026-11-12): in ReMixed's scenarios the mod's walker
steps the records by ReMixed's lengths and checks 0x14, 0x15, 0x25, 0x26 and 0x27 itself; the rest go to the game's.
Self-tested offline. The others no preset uses.

## What it means for the port

ReMixed's scripts aren't only new instructions: they are programs over the PS1's memory.

- **Raw memory.** 0xC1 and 0xC4 read and write PS1 addresses: 4,674 of them in the changed events. Most are battle
  unit fields through 0xC2 (about 200 different offsets: +0x58/+0x59 statuses, +0x47 position, +0x06 flags,
  +0x1BB/+0x1BC, +0x15F…), roster fields through 0xC3 (+0xD0 and others), and about 240 absolute addresses built
  with 0xAC + 0xB0: the buttons 0x80045944 (193 reads), the battle unit array 0x801908CC (by unit and field),
  0x801A08CB, the roster 0x80057F74, 0x80075CB3, 0x80058044, ReMixed's own data (0x80131FD8, 0x801316D0) and more.
  299 more go through addresses only known when the event runs.
- **Native code.** 0xCF calls ReMixed's MIPS routines: 0x80151000 (99 calls, from most battle events; Improved
  Level Scaling's re-run, now routed to the port's C# one: see below),
  0x80160600 (10), 0x80160C00 and 0x80160AB0 (9 each), 0x80150440 (4).
- **Self-modifying parameters.** 0xE6 (4,088 uses) writes a variable into the next instruction before it runs;
  the remaster's 0xE6 is something else (variable-length).
- **Disc reads.** 0xCE, 0xED, 0xEA load ReMixed's data straight from disc sectors.

Only 17 of the changed events use none of 0xC1–0xC4, 0xCF and 0xE6. So translating at build time can cover the
control flow (IF/ELSE/SWITCH/CALL/GOTO onto vanilla's labelled jumps) and the variable sets, but not the rest: the
remaster would need an interpreter for ReMixed's instructions with a PS1 address map (PS1 unit field → remaster
unit field, roster field, each absolute address), plus C# ports of the five native routines and the disc data.
