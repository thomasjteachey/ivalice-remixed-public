# Dispatch Missions: porting ReMixed's tavern (plan, 2026-11-11)

> **2026-11-19: the confirm button, not Start.** ReMixed itself sends a group with Start: in its picker (0x0E6)
> Circle (button word 0x20) adds the unit under the cursor, Cross (0x40) takes one out, Left/Right (0x8000/0x2000)
> move between the group's places, L1/R1 (0x4/0x8) step through the roster, and Start (0x800) sends the group, after
> its "Mission cost: N gil. / You have: N gil." No/Yes. Its mission list (WORLD 0x8013E4EC) takes a mission with Start
> once the mission's text is up, and asks nothing more. The remaster's native picker that Dispatch uses (0x1A2E90)
> binds Confirm Units to Start (Space) alone; Select on a picked unit only buzzed, and only filling every place went on
> by itself. Now Select (A, Enter or F) on a unit already picked confirms the units (`TavernStorage.OnPickerPress`; the
> button bar shows it, the build's PICKER_GUIDE), and the remaster's own confirmation, "Dispatch" or "Cancel", and the
> days follow on the same button. In the Tavern's Missions, Select on a posting asks "Accept this mission? It begins as
> you leave town." in the list's prompt line, with the confirm and back buttons' icons for Yes and No (`MissionPrompt`);
> Start no longer accepts one. Sending and recalling now go by the catalog's focused record (its +0x138, the
> carrier), as the remaster's abandon does: the sender's selected errand (0xD49434) comes from the row's virtual text
> record (Valor: 90, not its carrier 0), so sends were never marked as ReMixed's groups and recalls found none
> (`TavernStorage.OnSend`, `OnAbandon`; seen in the hidden PC game). Recall stays
> on the remaster's Abandon Errand button (its action 181, pad button 24 and the B key, held), which isn't Start.

> **2026-11-18: in Errands' place.** The Tavern menu is Rumors and Dispatch Missions: the button replaces Errands,
> as ReMixed's tavern replaces vanilla's errands (the user's call, for a faithful port). It's locked ("???") until
> ReMixed posts Dispatch Missions, its category pages have no Days or Finder's Fee columns, and the Rumors posting
> only points to the button. The remaster's 96 errands can't be started from it.

> **Design changed 2026-11-17 (0.9.6–0.9.7).** The faithful event-menu port below was retired after repeated
> controller lock-ins, missing focus, and crashes in its picker and Special Training paths. Dispatch Missions now
> uses the remaster's native dispatch controls for list navigation, roster selection, cancellation, days away,
> recall, and reports. It does not replace the Tavern's Rumors or Errands entries, any of the 96 vanilla errands, or
> their history. In 0.9.7 the extra Dispatch Missions rumor opens the PSX flow as nested native screens: the main
> menu (Propositions, Jobs, Special Training, Contests and Recall Units), Propositions' six kinds, then that kind's
> current unfinished progression set. Its catalog has all 92 Propositions, four repeatable Jobs, seven Special
> Training courses, the current monthly Contest, and active groups. Back unwinds one level at a time. A native scan stays capped at
> its original 96 rows because its event-variable storage ends there; virtual carrier rows expose the larger catalog
> after that scan. Existing groups recognizably saved by the retired screen are recalled before the native daily step
> can misread their differently laid-out records. The old design remains below as implementation history, not the
> current player-facing flow.

ReMixed's "Dispatch Missions" opens a tavern of its own, where the player sends units out on missions for gil, JP,
items and treasures. It takes the place of vanilla's errands: ReMixed hides vanilla's errand reports in the towns'
menus (world-and-menus.md, section 20) and keeps its own groups in vanilla's errand records. This is the plan for
bringing it into the port, from reading its events (story-flow-plan.md, "The Preparation Menu and Dispatch
Missions") and auditing them against the port's event runner. Annotated listings of the tavern's events, with their
messages inline, are in `work/analysis/tavern` on the PC (not in git; `evdis.py` there rebuilds them:
`python -I work/analysis/tavern/evdis.py 0xE5`).

## How ReMixed does it

**The way in.** The Preparation Menu (0x192) shows Dispatch Missions once flag 0x1A5 is set (Biggs and Wedge's
0x0A4 sets it, and 0x11A); ReMixed's mission list under Bonus Content has the tavern as entry 1 too ("There are
people all over Ivalice who have things that need doing… Press Start to go to the tavern", WLDMES 0x5A9). From the
menu, 0x192 saves vars 0x32–0x36, 0x23 and 0x24 at 0x80057804, sets var 0x3E = 7 and runs `0xCD 81 C7`.

**The scene.** Scenario 0xC7: map 96, battle data 0x1BA (11 units), music 0x21, preset 0xC6. The preset is the
tavern's dispatcher: by var 0x34 it runs 0xDF–0xE4 (0–5, the six kinds' lists), 0xE5 (0xFF, the menu), 0xE6 (0xF0,
sending a group), 0xE7 (6, training and contests), 0xE8 (0xF1, reports and recall) or 0xE9 (7, rewards and level
ups); each sets var 0x34 and ends. 0xC7 first loads its tables into battle units 0x90–0x97 (it assumes battle slots
0–7), adds the Tavernmaster (0x90), lists the free units (roster slots 1–19), finishes the groups that are due, and
opens the reports first if any are. Exit Tavern goes to the world map when flag 0x2D0 is set, else back to the
battle (`0xCD 81 [var 0x3E]`: event 7 puts the vars back and goes on to 0x191, then the Preparation Menu). Formation,
and the end of the reports, go to the world map with var 0x55 = 0xC7: the world opens the formation screen, then
the tavern again.

**Saved state: vanilla's errand storage.**
- Groups: a count byte at 0x80057CE8 (8 at most) and 8 records of 9 bytes at 0x80057EEC: +0 the state (1 out, 5
  done), +1 the mission (0x01–0x5C propositions, 0x80–0x83 jobs, 0x90–0x96 training, 0xA1–0xA6 and 0xB1–0xB6
  contests), +2 days out, +3 days needed (7), +4 the success odds (a contest's score), +5 the reward, +6–8 roster
  slots. Written by 0xE6, removed and closed up by 0xE8.
- Units away: roster +0xD0 = 1 (0xE6 sets it, 0xE8 clears it).
- Each mission's state: nibble var 0x35F + mission (0x360–0x3BB; 0 open, 0xA out, 0xC done): vanilla's
  propositions' nibbles. The six kinds' levels (0–5) aren't stored: 0xC7 works them out from these.
- This month's contest: byte 0x800579A7 (the nibble block's tail). Treasures and wonders found: flags 0x320 + id.
- Gil is var 0x2C, the month and day vars 0x2E and 0x2F (WLDCORE's day change), the story section var 0x6F.

**Time.** Vanilla's daily step (WLDCORE 0x80074B30, from the day change at 0x80069B80), which ReMixed doesn't change,
adds a day to each record out and marks it done when its days are up; a group starts at the Exploration level, so it
takes 7 − that many days. A won battle finishes every group at once: event 8 sets flag 0xD4 at its end (0x004,
0x032, 0x047, 0x07B and 0x07E–0x086 do too), and 0xC7 finishes every group out while it's set, then clears it.
Nothing is reported until the tavern opens.

**The menus' rules.**
- **Propositions**: six kinds (Salvaging, Mining, Exploration, Combat, Investigation, Odd Jobs), five sets each; the
  set offered is the kind's level + 1, up to a cap by the story, and a set has to be finished to open the next. A
  set's missions cost the same, 10% less a Salvaging level. A group is up to three units, one of them human. Each
  mission prefers a level, a Brave (or Faith) and one of four jobs, each met if any unit meets it (a monster meets the
  job, a special character's own job counts as Squire). Odds: 15 a unit + 20 a preference met + 3 × the
  Investigation level, at most 100; the report rolls them. A failure gives nothing and opens the mission again
  ("Perhaps your units should do Special Training").
- **Jobs** (Mining, Logging, Farming, Hunting): repeatable, always successful, priced by the story section (100 to
  15,000), paying gil and JP; one unit, then two (story section 5), three (9) and several crews (13).
- **Special Training**, one group a course, same prices: Valor (+Brave), Detection (−Brave), Theology (+Faith),
  Evolutionary (−Faith), by 15/10/5/3/2 depending on the value, doubled with certain treasures; Training (levels up
  toward the roster's highest, at most 3); Retraining (levels again, through ReMixed's 0x80150440); Transitioning
  (the other gender: sprite, jobs, gear off).
- **Contests**: one a month by the zodiac (vars 0x2E, 0x2F), from story section 5, one group: the squad's Job Levels
  in the contest's job score it (5–9 third, 10–14 second, 15 first); gil ×½/×1/×2 and JP in that job.
- **Recall**: no refund; the mission opens again and the units come back at once.
- **Rewards**: gil (a base by the story section when the report is heard, × (1 + the Mining level / 5)); JP to each
  human (about 100 + 20 × the Odd Jobs level, plus a random part) and a share of the others' (× (Combat level + 2) /
  20); a treasure or wonder (flags 0x320 + id), three items, or for Eureka (reward 0x71) a spin of the Treasure Wheel
  (event 0xC3, the loot screen).

**Unlocks.** The kinds' first sets open by the story section (var 0x6F: Mining and Exploration at 3, Salvaging and
Investigation at 4, Combat and Odd Jobs at 5), one more set each up to 0x10.

**Its numbers.** 92 missions in 0xC7's SWITCH (0x18E02B–0x18EFCA): cost, reward, level, Brave or Faith, four jobs,
as 9-byte entries at unit 0x92 + kind +0xA0; the sets at 0x18F6F5. Titles are 0xE8's messages 1–92; in each kind's
event, mission j's title, posting and report are messages 10 + 3j, 11 + 3j and 12 + 3j. ReMixed's job ids: 0x37 and
0x5B the Dark Knight (the remaster's 0xA0), 0x38 the men's Bard, 0x5C the women's Dancer.

## What the port has

Every ReMixed opcode these events use is ported (`RemixedScript.cs`), and every vanilla one runs in the remaster
with the same length. One native call (0x80150440, Retraining) is ported; event 0xE0 once (a unit from the roster
into battle slot 16) is handled by `CreateUnit`. What's missing, most work first:

1. **Text made while the event runs.** The menus and screens fill in slots (code 0xEB: message list[k] + 1, 273
   uses) and numbers (0xE6 with a list word, 39 uses, "They'll cost N gil"); the build drops both, renders the zodiac
   glyphs as "0"–"C" and loses "&", and a menu made only of slots ships as "Cancel". 0xE8, with its 236 fragments,
   would pass the event size limit.
2. **Saved state.** The errand count and records (0x80057CE8, 0x80057EEC) fall into the mod's own store, which isn't
   saved; the remaster keeps both, in vanilla's layout (Phase 0, below). The nibbles 0x360+ are mapped and saved,
   and they're the remaster's own errands' too. No event 8 sets flag 0xD4.
3. **The dispatcher** runs through the battle's condition list in a "battle" with no player units and no formation:
   untried.
4. **0xE6's walk over 0x800B7308** (+0x440 an entry) has no end; with the store reading 0 it would hang the game.
5. **Job levels and job data.** Roster +0x64–0x6D (job levels) map wrong (into learned abilities); the remaster
   keeps them at +0x74. Job data 0x800610B8 + 0x30 × job reads 0 (the remaster's is .data 0x785E30, 0x31 a job).
6. **Level ups.** 0xE9 writes battle-unit stats and growth (+0x72–0x89), which aren't mapped.
7. **Event numbers.** All twelve clash with the remaster's events; free ids from 0x355 have room (0x355, 0x356,
   0x358–0x35B, 0x35E–0x360, 0x363–0x365, 0x368–0x36A, 0x373–0x376), and `renumber_presets` rewrites the preset.
8. **The Treasure Wheel** (0xC3) ships as 0x368 from 2026-11-12, after each mission won; 0xE8 hands Eureka's reward on
   to it with its own loot set (var 0x15 = 0x1BA), which the Wheel's end sends back with var 0x55 = 0xC7 (not tried).

## Decision: the remaster's errands

ReMixed's tavern takes vanilla's errand storage, and the remaster's own Errands most likely keep theirs in the same
places, so the two can't both run as they are. **Decided (2026-11-11): replace them, as ReMixed does.**

- **Replace them, as ReMixed does** (chosen): the remaster's Errands entry hidden in its taverns, as ReMixed
  hid vanilla's reports, and ReMixed's groups kept where the remaster keeps errands. The remaster's daily step then
  advances them, and its own code already treats units away (left out of formations, locked jobs, "On Errand" in the
  party menu). To check: what the remaster does with a finished record it didn't make (its "units have returned"
  notice and report), to silence.
- **Keep both**: ReMixed's groups in a saved store of the mod's own, with a day step of its own in C#. More code,
  and two errand systems side by side.

## Phase 0: the remaster's errands (read 2026-11-11, from its code)

The remaster keeps vanilla's errand storage and its daily step, so ReMixed's tavern can keep its groups where the
remaster's errands are:

- **The day change** is 0x2CCEE0: the month and day are vars 0x2E and 0x2F, as on the PS1 (the days of each month at
  .rdata 0x680FF8). It adds a day to each errand record that isn't done and marks it done (+0 |= 4) when +2 reaches
  +3, or at once when +0 has bit 2; then it ages the 96 bytes at 0x11A7140 (an errand each, the low six bits a count
  down), runs the birthdays and the daily monster step (0x289900).
- **The records**: a count byte at 0xD407B2 (the PS1's 0x80057CE8) and 9-byte records from 0x11A71A0 (0x80057EEC;
  room for 14 before 0x11A7220): +0 the state, +1 the errand, +2 days out, +3 days needed, +4 the location its report
  waits at, +5 the number of units, +6–8 their roster slots. Units away: roster +0x11E, which the port already maps
  from ReMixed's +0xD0.
- **The errands**: NXD table 0xB6 (`profit`, 96 rows; their reports in `unitbarprofitresultmessage`, 240), each with
  a state in nibble var 0x360 + row: bit 4 posted (the Errands list, 0x16E7F4, shows those), 8 out, 2 one more day.
  ReMixed's 92 missions use the same nibbles (0x35F + mission).
- **Its screens write them**: sending (0x36A7D8: bit 8, and a record: +0 = 1, +1 the errand less one, +3 the days,
  +4 the location, +5 the units and their slots, the count up by one), Abandon Errand (0x2EE1C8: the units freed,
  the record taken out, bit 2 cleared) and the report (0x370054, from the tavern's report screens 0x37085C through
  0x36FF30: gil into var 0x2C, the nibble, and the date of the errand, a treasure (flag 0x321 + id, its date at
  0x11A71E8) or a wonder (flag 0x350 + id, at 0x11A7260), and the units freed). ReMixed's treasures and wonders set the
  same flags.
- **Its notices**: for each record done the day change marks its report's location on the world map (0x340FD0 with
  +4) and counts +5, then shows "units have returned" (UI event 0x611).

What it means for the tavern:
- ReMixed's count and records map onto the remaster's (0xD407B2, 0x11A71A0), saved with the game.
- The remaster's day step advances them by ReMixed's own rule, but it reads +4 as a location and +5 as a number of
  units, where ReMixed keeps the odds and the reward. So the port runs the step for the tavern's records itself and
  keeps them out of the remaster's loop (or makes those two reads skip them).
- With the remaster's Errands entry hidden, its sending, abandoning and reports don't run. Still to find: what posts
  the remaster's errands (sets bit 4 of their nibbles). It isn't in the exe's code by name, so most likely its world
  events; it has to stay off ReMixed's nibbles.
- The remaster logs when a treasure or wonder was found, for its records; the port can write those dates for
  ReMixed's finds.

## The plan

**Phase 0: the remaster's errand storage** (done 2026-11-11, above).

**Phase 1: open the tavern** (large).
1. Ship 0x0C7 and 0x0DF–0x0E9 under free ids, preset 0xC6 rewritten, and the tavern's scenario record (map 96,
   battle data 0x1BA, music 0x21).
2. Post it among the taverns' Rumors as ReMixed's mission-list entry 1 (its text, "Press Start to go to the
   tavern"), listed from flag 0x1A5 as the missions are (`MISSIONS`), and set flag 0x2D0 when it starts there, so
   Exit goes back to the world map.
3. The text: keep the slots and numbers in the shipped lines and fill them as a line is shown (a hook on the event
   line lookup, 0x1DB0FC, from ReMixed's list words); menus whose rows come from the event; the zodiac glyphs and
   "&"; fragments used only through slots left out as lines (0xE8 under its limit).
4. 0xE6's endless walk patched out at build time, or answered in C#.
5. The dispatcher: the condition list with 0xE5 alone first; if that fails, a C# dispatcher by var 0x34.

Done when, on the PC, the tavern opens from its posting, its menus list ReMixed's sets with their prices, and Exit
goes back to the world map.

Built (2026-11-11), all but the posting:
- The events ship as 0x355 (the scene), 0x356, 0x358–0x35B, 0x35E–0x360 and 0x363–0x365 (the build's `TAVERN`); the
  others have no records of their own, and preset 0xC6, renumbered, runs them through the scene's condition list. The
  scene sets flag 0x2D0 after loading its preset; 0xE6's step back (its only GOTO 18) is a label no code uses;
  0xE8's hand-off to the Treasure Wheel goes to it (0x368; to the world map before the Wheel shipped on 2026-11-12);
  the tavern's names (short lines with no speaker) get no never-run MESSAGE, so 0xE8 fits.
- The text (`Patches/RemixedText.cs`, the build's SLOTS_FILE): the lines with slots keep them as tokens, and the hook
  on the line lookup (0x1DB0FC, rcx the text table, edx the Id; its callers keep only rax) fills them in from
  ReMixed's list words: the k-th 0xEB or 0xE4 in a line takes word k (0xEB: that word + 1 as one of the event's
  messages; 0xE4: as a number), 0xE6 xx word xx. A propositions menu's 15 slots are five rows of a colour, a title
  and a line break (message 4); a slot that shows nothing makes no row.
- Seen on the PC (started by its number from the harness, the story section at 5): the scene on map 96 in the
  remaster's look, the Tavernmaster's lines in its speech boxes, the menu (Propositions, Jobs, Special Training,
  Contests, Recall Units, Formation, Exit Tavern), Propositions' six kinds, Mining's five sets, Set 1's "Here's Set 1
  of the Mining Propositions. They'll cost 200 gil." with its missions' titles (Abandoned Mine Expedition and on), a
  mission's "Here's the info on Mission 18: "Salt Mine Expedition".", Jobs' four, and Exit Tavern's "All done here?"
  → Leave → the world map.
- Not yet: the posting (built, held back by the build's TAVERN_POSTED): sending a group (0xE6, from a job or a
  mission) waits in a loop for ReMixed's PS1 buttons (0x80045944, the held word), which the remaster makes in a
  battle's frame (0x37FB7C) and not during the tavern's scene. On the PC neither the harness's pad step (which works
  through that frame) nor a held pad button reached it, so it waited for good; posted, the tavern would leave a
  player stuck there. Also: the unit card shows its number and level but not the name, job, Brave and Faith; the
  Jobs line lost "For now, anyway." from its box (two rows, one shown); the remaster draws no cursor highlight in
  these menus.

**Phase 2: send groups out and bring them back** (large).
0. ReMixed's PS1 buttons during the tavern's scene: made from the pad (and the keyboard, through the remaster's
   actions) when ReMixed's events read them outside a battle's turns. Done (2026-11-11, `Patches/Ps1Pad.cs`): its
   three words (held, before, pressed) are a buffer the mod fills as an event reads them, from the pad's record
   and the UI's action check (0x0F0D00) for each button's action (Up 0x21, Down 0x22, Left 0x23, Right 0x24,
   Circle 0x1A, Cross 0x1B, Square 0x1D, Triangle 0x1E, Select 0x20, Start 0x1F; the action test 0x0FC598 said no
   outright on the PC). Seen on the PC: Right moves the unit picker on (var 0x71), Escape reads as Cross. Also
   done: the slots that name things (0x3xxx a job, 0x58xx a roster unit, 0x7xxx an ability, 0xBxxx a skillset, read
   off ReMixed's list on the PS1 while its card showed "2 Justin Lv. 1 Squire Brave 65 Faith 45"), so the card reads
   "2 Rachel Lv. 8 Thief Brave69 Faith40"; and each line with slots ships with a twin the mod shows by turns, as the
   game looks a line up once while its box is open. Still to do here: the picker doesn't redraw the card as it
   moves (it redraws on flag 0x86, which its units' walk-ins look to set: their 0x44/0x46 steps), a mission's four
   job names stay blank (their slots' words to read), and Escape's Cross doesn't leave the picker yet. The posting
   waits on those and on steps 1-2 (sending a unit marks it away in the saved roster while its group's record is
   in the mod's unsaved store). Then (later the same day): L1 and R1 (actions 0x2B and 0x2C, the pair the remaster's
   screens switch tabs by) step the picker through the roster, and the card redraws ("3 Tony Lv. 8 Geomancer
   Brave69 Faith66"); Space is Start. Cross leaves the picker only with an empty group, as ReMixed wrote it. Each of
   its `WaitFor 01` steps (after L1 or R1, and on Start) waits for a confirm press in the remaster, where ReMixed only
   waited for the text: to make smooth.
1. Done (2026-11-11, `Patches/TavernStorage.cs`): the groups' count and records are the remaster's (0xD407B2,
   0x11A71A0; found in the day change's code). The missions' states (nibble variables 0x360-0x3BF and the contest
   byte, by id and as PS1 memory) are kept in the remaster's errand count downs (0x11A7140, 48 of its 96 bytes), not
   its nibbles: the remaster's story events post its errands in those (bit 4), which would read as ReMixed's states.
   Seen on the PC: Rachel sent on the Mining job, its record in the remaster's storage (state 1, 0x80, 0 of 7 days,
   odds 100, roster slot 1), her errand byte set, and she walks out. Then (built 2026-11-12, not seen in the game
   yet): the remaster's Errands, whose screens would write the same records, retired as ReMixed retired vanilla's:
   the tavern screen's Errands list (0x16E7F4) is built empty, so it says "No errands available.", and the check of
   whether a menu has something new (0x162D38) says no for the errands (kind 6).
2. Days: done (the same file): the day change (0x2CCEE0) steps the tavern's groups by ReMixed's rule, its own loop run
   with the count at 0 (it would read the odds as a location) and the count downs put back after it. Self-tested
   offline; not yet seen in the game. Built too (not seen yet): a won battle sets flag 0xD4, as the end of ReMixed's
   after-battle event 8 does (the mod ships no event 8), and the tavern brings every group out home while it's set,
   when it next opens.
3. Done (2026-11-15): job levels map to the remaster's +0x74 for preferences and Contests. The PS1 job table
   (0x800610B8, 0x30 bytes a row) now maps field by field to the remaster's live JobData table (0x31 bytes a row,
   with its extra byte before the growths), including ReMixed's Bard and Dark Knight rows relocated by the build.
   Training and Retraining therefore read the patched jobs' real growths and multipliers instead of zeros from the
   event upgrade's private store. Built and self-tested offline; not seen in the game yet.
4. Reports and rewards: gil, JP and its share, items, treasures and wonders (check what the remaster's Artefacts and
   Wonders lists show for ReMixed's ids), and Recall. A Job's report runs (2026-11-12, seen on the PC beside ReMixed
   on the PS1, work/psx/run/shots rpf-*: a group's record marked done, then Recall Units): its MESSAGEs name message
   0xFFFF and have the real number written in as they run, and the remaster sizes a box by the line a MESSAGE names as
   the event loads (no line, a box of no width), so each now names a line of the PS1 box's size (EVENT_CODE_PATCHES);
   the Tavernmaster's box closes before its next line (the remaster leaves an open box as it is), and the gil's box
   is the bottom window's, centred under his (EVENT_BOXES) and closed with his. Seen: the completed mission listed,
   "This is what your units earned from "Mining"." with "Earned 300 gil!" (paid), "Geomancer JP: +74" with the unit's
   face, "That's all of 'em..." and the party screen. Still to see: a mission's own report (through its kind's
   event), treasures, items and the Treasure Wheel's spin, Recall, level ups, and groups of two and three (0x0E9's
   boxes 2 and 3). Still to match: the PS1 wraps the Tavernmaster's line in two rows and colours a job's name red.

Done when a group sent out comes back after its days or a won battle, across a save and load, and its report pays.

Built after that (2026-11-12, checked offline, not seen in the game yet: the user had the sessions push without game
runs; GAMEPLAY_TODO.md's Testing lists them):
- The posting (TAVERN_POSTED): among the Rumors from Chocobo Defense's unlock, started on leaving town as a mission is.
- A mission's report: the six kinds' events (0x0DF-0x0E4) show a mission's posting and report in a box under the
  Tavernmaster's (style 0x61) whose MESSAGE names message 0xFFFF, so it had no width; it now names the event's widest
  posting or report (KIND_BOX), and Special Training and Contests' (0x0E7) its widest description; the menu's (0x0E5)
  comeback line one of the Tavernmaster's. ReMixed's own, filmed on the PS1 for the comparison (rmg-*: mission 0x11's
  record poked done in state rp-c, then Recall Units): "Here's the info on Mission 17: "Abandoned Mine Expedition"."
  with the five-row report under it, then the earnings, "Earned 150 gil!" and "Squire JP: +130".
- The rewards' lines (0x0E9's 1-3 and 23, 0x0E8's 181-183): a red run (E3 08) the message ends in was read as the
  speaker's name, so "Squire JP:" lost its red, and a second red run dropped the first, so a group of two or three got
  one row; runs with slots are text now (remixed_line).
- Job levels: a roster unit's (PS1 +0x64-0x6D) went into its learned abilities; they map to the remaster's +0x74
  (RemixedEvents.MapRosterJobLevels, from its unit build 0x276758), for Contests and the missions' preferences.

**Phase 3: the rest** (built, needs in-game coverage). The shipped events already perform Brave and Faith training,
Transitioning (the roster sprite/gender, job and equipment writes are mapped), and Contests (their saved month byte,
job levels, gil and JP are mapped). Training and Retraining's level-change path is complete from 2026-11-15: battle
unit level/raw-stat/growth fields are mapped, 0x80150440 is ported, and the job table it compares against is now the
live remaster table (Phase 2.3). Formation's world-map word and var 0x55 return are renumbered to the port's tavern;
Eureka's report hands its loot set to the shipped Treasure Wheel, whose return is renumbered likewise. These paths
are not marked proven until each is exercised in the game: all seven training reports, a contest at each placing,
Formation returning to the tavern, and Eureka's wheel returning to the remaining reports.

**Phase 4: from a battle's formation screen** (large, with the rest of the Preparation Menu). A menu there with
Dispatch Missions needs a way to leave a formation for another scenario and come back to it, which the port doesn't
have; random battles have no scenario to come back to.

## The tavern's look (2026-11-12)

Side by side with ReMixed on the PS1 (work/psx/run/shots dm*), the port's tavern looked wrong: menus floating with no
box, the hand cursor in the Tavernmaster's box, no greyed rows, the unit card on one line ("Brave69 Faith40", no
zodiac sign, the number not red), an empty speech box for the portrait, and the picked unit walking in as a second
Ramza. What the remaster's event boxes can do, found in the game (harness pokes) and its code:

- **Where a box goes.** A MESSAGE's style: its place bits (0x70) 0x10 make a speech window (0x70 and 0x90 count as
  0x10; 0x2D9490), box 1 the top window and any other box the bottom one; place bits 0 or 0x20 make the narration
  layer (frameless, centred: the remaster's own `<center><scale=1.4>` narration); other places crash or misbehave.
  The build turned the tavern's places 0xE, 0x6 and 0x4 into 0, so its menus fell into the narration layer.
- **Two windows, three rows.** The windows are a UI layout ("Balloon00": LayerNone/Left/Right with "ScrollText…" text
  views of 162 units, three rows; frames "Normal", "Round" (menus) and "Spike", each a nine-grid). A window's height
  isn't measured from its text: it's the root node's (+0x44), which the one-line or three-line state ("Text01EnFrDe",
  "Text03EnFrDe") sets. Rows past the third show faded below the frame. The remaster's own event menus have two rows.
- **The cursor.** The menu state is in the message window object ([[[0x3CD9DA8]+0x10]+0x68]): +0x84 the row, +0x90 and
  +0x94 the first and last rows, +0x88 a row to move to (the game's own path, 0x136DB8, which also draws the cursor).
  In a speech window the cursor sits beside its row; in the narration layer it's drawn at the top window's first line
  whatever the row.
- **Stretching a box.** UI nodes: +8 the name, +0x20 the next sibling, +0x34 the type (1 a group, its first child at
  +0xD0; 12 an instance of a sub-layout, at +0x130), +0x38 the position, +0x40 the size; 0x3EF6B4 sets a size. A
  frame's nine-grid repeats its texture past three lines (mode +0xF4 = 2, no top or bottom border), so set taller it
  drew as two pieces; mode 0 with a border of 100 at the top and bottom (+0xE8, +0xF0) stretches it with its corners
  kept. A nine-grid is anchored to its frame sideways only (+0x74-0x80 its margins, +0x8C its flags); the size setter
  makes its size its own, so after it the right margin goes back and its width follows the box again.
- **The cursor past two rows.** The cursor (0x1365AC) goes to two anchors only, buttons at the side's record +0x88 and
  +0x90 (each the instance of a "ButtonCollision00"/"01" node in the text view's layout, the record's +0x30), 54
  units apart. The UI's answer to a button (0x135D18) makes its index the menu's row.
- **Text.** The text engine's tags include `color` (UIColor rows, AABBGGRR), `icon` (the zodiac signs are icons
  450-462), `nbsp`, `left`, `center`, `xy`, `scale` and `br`; the event text uses `speaker`, `hidespeaker`,
  `cursor`/`cursorend`, `forced`, `center`, `scale`, `i`, `icon`, `stoplip` and `br`.
- **Battle events.** The tavern is a battle event: its boxes are laid out by 0x221C00 (the world's by 0x2D9490, the same
  record: its floats [0]-[3] the box's place and size, [0xB] its window, [0xC] its side). With no unit a box goes to
  the screen's top left (x 192, y 75.6, the least it clamps to).

What the port does now (build_mod.py's tavern boxes, Patches/TallMenus.cs, Patches/RemixedText.cs):

- **Menus** go in the bottom speech window (style 0x16, no unit), placed under the Tavernmaster's box, as wide as
  their own rows (the game's text width, 0x136A7C, with no portrait; it never gives less than a speech box's least
  width, the manager's +0x6D0, 310, so that's lifted while a menu is measured) and in the speech boxes' frame
  ("Normal": the layout record's [0x13], which the remaster makes "Round" for menus) with no tail ([0x14]; the
  "Normal" frame draws one at the box's corner where "Round" didn't); a box laid out with more than three lines has
  its frames stretched to fit; for a row from the third on, the second anchor moves to it and takes the cursor (its
  mouse collision off meanwhile), and the UI's answer for it gives that row. Rows that can't be picked are grey.
- **Text** keeps ReMixed's colours (E3 08 red, a UIColor of 183, 72, 72; E3 04 grey, 115, 98, 86), its line breaks
  where no one speaks, its zodiac glyphs as the remaster's icons, and its fixed-width numbers (E8) right-aligned with
  non-breaking spaces; a slot that's only a colour is a lone tag, paired up as a line fills in.
- **The unit picker** shows the card in the top window with the picked unit's portrait beside it (0x0E6's portrait box
  dropped, its card's box moved to box 1, positioned as the portrait box was, with no tail; its opener a three-line
  sizing line). The other boxes the remaster would put in the narration layer go in the bottom window, and a box of
  the tavern's own laid-out lines opens 80 units wider, as a number that grows a digit while it's open would break
  its line.
- **Fixed (2026-11-12): the picked unit walked in with Ramza's sprite.** 0xE6's function 02 works the graphic out
  from the picked roster unit as the PS1 numbers them (a generic 0x60 + 2 × (job − 0x4A), + 1 for a woman; a monster
  its job's graphic + 0x85 with the palette into +0x160; a special character its sprite set, less 0x30 past 0x8F and 2
  past 0xB5; ReMixed's gendered Dark Knight, Bard, Dancer and Mime 0x37, 0x38, 0x82–0x85), writes it to unit 0x95's
  +0x15F (the remaster's +0x18F) with +3 (job) 0, and copies it into the walking unit's +0x15F and +0 (its sprite
  set) before adding it again. The remaster's numbers are the same (its job table holds a monster's graphic + 0x85),
  and its AddUnit (op 0x45, 0x23C6C0) makes the sprite from +0x18F and +0x190 as they are (0x27313C → 0x3041A4). But
  when a box shows a unit's face (0x22AA88, the card's), the remaster works the graphic out again from the sprite set
  and job (0x0FCAB0, through 0x278EDC: 0x80/0x81 the generic formula, 0x82 a monster's +0x18F, under 0x81 the sprite
  set itself, else job + 0x28) and writes it back: unit 0x95's sprite set is the battle data's 1, so its graphic
  became 1, Ramza's, before the walking unit copied it (a write watch on its +0x18F, then a poke of its sprite set
  to 0x73 that made the walk-in a Thief). Now a unit keeps a graphic a ReMixed event wrote (`Patches/UnitLooks.cs`).
  Seen on the PC: Rachel walks in as a Thief (side by side with dmy-00300 in work/harness/compare/tavern-walk-in.png).
- **The figure at the counter (settled 2026-11-12).** On the PS1 only Ramza stands at the counter while units are
  browsed (dmx-00100 to dmx-00600). The port showed a figure with the browsed unit's look there before the walk-in's
  sprite and the card's place were fixed; seen on the PC since, only Ramza stands there, and the slots' markers show
  as shadows on the table as on the PS1. The remaster's sprite list ([0xD3A410], +0 the next, +8 the unit's index,
  +0xE and +0x1F8 drawn, +0x88-0x8A the tile, +0x148 the battle unit, +0x318 the shadow) read through the harness: the
  face's unit (0x95, at 7,4) isn't drawn. Its op 0x44 draws a unit (+0xE = +0x1F8 = 1; 0x2731E8) and 0x46 erases one
  (both 0, +0x156 = 1; 0x27322C), which FUNCTION 04's hiding of the walkers would map onto if it's ever needed.
- **The card's face (2026-11-12, seen on the PC).** L1 and R1 swap the card's text in, then add the face's unit again
  with the next unit's look; the remaster takes a box's face once, as it opens, so the card kept the first unit's
  face. After that re-add (LABEL 12: CALL 02) the card's box opens again at the unit (EVENT_CODE_INSERTS).
- **A cost question's cursor (2026-11-12, seen on the PC).** The remaster's cursor goes to two anchors at a box's
  first two lines and is asked, on one path, for a row counted from the menu's first choice; the questions with lines
  of text above their choices ("Mission cost: ... / No / Yes") had the hand at those first lines. TallMenus puts it
  at the choice's own line.
- **Placed as on the PS1 (2026-11-12; seen on the PC in the regression pass the same day):**
  - The menus: the PS1 centres each at x 124 of its 256 whatever its width (dmf-01800, dmj-00300), a little left of
    the screen's middle, not under the Tavernmaster's box. TallMenus places a menu with no unit with its middle at UI
    x 940 (the 4:3 picture is 1440 wide in the middle of the remaster's 1920). Before: under the box's left end, then
    (seen on the PC) centred under the box.
  - The unit card: on the PS1 it's at the top, the portrait in a box of its own on the left, covering the
    Tavernmaster but his hands, with Ramza in view in front of the counter (dmu-00300). The port's card stood right of
    the middle over the counter, where it hid Ramza. A speech box goes at its unit (its x less half the box's width,
    plus the MESSAGE's x; above the unit for the top window, less the MESSAGE's y), then is kept in x 192-1728 and y
    75.6-1004.4 (0x221C00; 0x2D9490 for the world's), so the card's MESSAGEs now have x -388 and y 300: about where
    the PS1's card is across, at the top. Its portrait stays on its right: the remaster puts it on the side away from
    the unit's half of the screen (side 1 for a unit left of x 960, swapped by the style's 0x10 bit, which every speech
    box has).

## Testing

On the PC with the harness: a command to start the tavern; each menu by keyboard; a save and load with groups out;
days passing on the world map; a battle won. Then on the Deck with a controller. The harness's `code` gives the
running event's code (a built event file's offset X is at code + X + 4), `line ID TEXT` shows other text for a line,
and the menu row can be moved with a poke of the window object's +0x88 (key presses reach the menus only now and
then).

## Open questions

- Who sets flag 0x2D0 in ReMixed? Nothing in its events or WORLD's programs does.
- What vanilla's opcodes 0x84, 0x85, 0x50 and 0x51 show in these events (the remaster runs them).
- ReMixed's quirks, to keep as they are unless they break: above story section 0x10 prices fall back to 100;
  Detection's and Evolutionary's band reads 16 where 10 looks meant; a Job's JP is halved unless two crews finish
  together; a report sets roster +0x15 (EXP) to 99; holding two buttons lifts Training's limit of 3.
