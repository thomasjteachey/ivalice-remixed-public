# Port progress

How far the port of ReMixed to The Ivalice Chronicles has come, as one figure. The figure is counted, not judged:
`python -I tools/port_progress.py --write` counts the lists ([GAMEPLAY_TODO.md](GAMEPLAY_TODO.md) and the events
below) and rewrites the figures here, the history's row for the day and the figures in GAMEPLAY_TODO.md. Run it with
every change to those lists; don't edit the figures by hand.

<!-- figures -->
**The port: 80% done** (2026-11-19).

| Part | Share of the port | Done | Counted from |
|---|--:|--:|---|
| Gameplay: ReMixed's rules ported | 40% | 99% | GAMEPLAY_TODO.md's gameplay items by size: 106 done, 2 open |
| Gameplay: seen working in the game | 10% | 25% | GAMEPLAY_TODO.md's "Testing" by size: 14 done, 39 open |
| Story: ReMixed's own events | 25% | 77% | the 79 events below, half for each the mod ships (79) and the rest once seen working (42) |
| Story: what its events and battles need | 10% | 85% | GAMEPLAY_TODO.md's "Story port progress" and "Waiting for the story port" by size: 42 done, 6 open |
| Display, text and art | 15% | 69% | GAMEPLAY_TODO.md's "Waiting for the display, text and art port" by size: 21 done, 10 open |
<!-- /figures -->

## How it's counted

- **The shares are fixed.** They're a judgment of how much of the whole port's work each part is (set 2026-11-06),
  and only the lists move the figure. Gameplay is the bulk of ReMixed (its balance, its rules, its random battles);
  the story is its own events (79 with scenes and battles), what they need (their instructions, menus, results
  screen, the rules only story battles use); the display, text and art part is what ReMixed shows rather than what it
  does, and its new sprites, portraits and maps.
- **Items by size**, as in GAMEPLAY_TODO.md: small 1, medium 3, large 15, an item with no size medium. The gameplay
  list's Done section counts 2 for each line (`tools/todo_progress.py`).
- **An event** counts half once the mod ships it (it's in `REMIXED_SCENARIOS` in tools/build_mod.py; the script
  reads the mod's number from there) and whole once it's been seen working in the game (the date in its row, kept by
  hand). An event the port leaves out on purpose would leave the list, with the reason.
- **Not counted:** ReMixed's retelling of the main story, left out by choice (the remaster's scenes, lines and voices
  stay: docs/analysis/story-flow-plan.md), and the release, which waits on ReMixed's author.

## History

<!-- history -->
| Date | The port | Rules | Seen working | Story events | Story needs | Display and art |
|---|--:|--:|--:|--:|--:|--:|
| 2026-11-06 | 63% | 100% | 7% | 51% | 48% | 32% |
| 2026-11-07 | 66% | 99% | 7% | 63% | 52% | 29% |
| 2026-11-11 | 66% | 100% | 6% | 63% | 46% | 31% |
| 2026-11-12 | 76% | 100% | 11% | 75% | 67% | 62% |
| 2026-11-13 | 77% | 100% | 14% | 75% | 74% | 61% |
| 2026-11-15 | 77% | 100% | 14% | 75% | 77% | 61% |
| 2026-11-19 | 80% | 99% | 25% | 77% | 85% | 69% |
<!-- /history -->

Before this count, 2026-11-04: "roughly 70%", a judgment.

## ReMixed's own story events

The 79 events with ReMixed's own scenes and battles (docs/analysis/story-flow-plan.md, "ReMixed's own content"), by
ReMixed's numbers. Its system events (tavern errands, options, the preparation and retry menus, the loot screen,
message templates) aren't in it: each is an item in GAMEPLAY_TODO.md, or left to the remaster's own. Left out as
nothing to port (2026-11-12): 0x0C8 (Izlude's Last Stand, a mission-list entry) and 0x10B (a battle record, map 13)
are empty events in v1.2.64.23 (an END alone) that nothing starts. 0x0FC, first taken for a retelling of the opening,
is side chapter 1-A's end and joined the list (story-flow-plan.md, "Side chapter 1-A's end at Orbonne").

<!-- events -->
| Event | Group | What | The mod's number | Seen working |
|---|---|---|---|---|
| 0x0A8 | Missions | Chocobo Defense | 0x1ED |  |
| 0x0A9 | Missions | Chicken Race | 0x1EE |  |
| 0x0AA | Missions | Treasure Hunt: its battle won by reaching the chest's tile (ReMixed's own condition type) | 0x1EF | 2026-11-12 |
| 0x0AB | Missions | Teioh | 0x1F0 |  |
| 0x0AC | Missions | Lost Heirloom | 0x1F1 | 2026-11-07 |
| 0x0AD | Missions | The Ball: the dancers' fete (Yardrow) | 0x1EC | 2026-11-05 |
| 0x0AE | Missions | Desert Minefield: Govis and the bombs | 0x1F2 | 2026-11-05 |
| 0x0AF | Missions | Littering | 0x1F3 |  |
| 0x0B0 | Missions | Shades of the Past | 0x1F4 |  |
| 0x0B1 | Missions | The Shrine Knights | 0x1F5 | 2026-11-07 |
| 0x0B2 | Missions | The Shrine Knights' second round | 0x32C | 2026-11-07 |
| 0x0B3 | Missions | The Shrine Knights' third round | 0x32D | 2026-11-07 |
| 0x0B4 | Missions | The Shrine Knights' last round (Adrammelech) | 0x32E | 2026-11-07 |
| 0x0B5 | Missions | All-Star Melee | 0x1F6 |  |
| 0x0B6 | Missions | The Guarded Temple | 0x1F7 |  |
| 0x0B7 | Missions | Nightmares | 0x1F8 |  |
| 0x0B8 | Missions | Brave Story | 0x1F9 | 2026-11-19 |
| 0x0B9 | Missions | Brave Story's second round | 0x331 | 2026-11-19 |
| 0x0BA | Missions | Brave Story's third round | 0x332 |  |
| 0x0BB | Missions | Brave Story's fourth round | 0x333 |  |
| 0x0BC | Missions | Brave Story's fifth round | 0x337 |  |
| 0x0BD | Missions | Brave Story's sixth round | 0x338 |  |
| 0x0BE | Missions | Brave Story's seventh round | 0x33C |  |
| 0x0BF | Missions | Brave Story's eighth round | 0x33D |  |
| 0x0C0 | Missions | Brave Story's ninth round | 0x341 |  |
| 0x0C1 | Missions | Brave Story's last round | 0x342 |  |
| 0x0C2 | Missions | An Ill Wind | 0x1FA | 2026-11-07 |
| 0x0C3 | Missions | The Treasure Wheel: a spin for a prize after each mission won | 0x368 | 2026-11-12 |
| 0x0C4 | Missions | Mirror Match: Gogo copies your party (the copy in the remaster's layout, a code-mod routine), posted late in chapter 4 | 0x07D |  |
| 0x0C5 | Missions | Of Vagrants | 0x1FB | 2026-11-07 |
| 0x0C6 | Missions | Be A Beta: ReMixed's beta testers, ArmoredKori's swap | 0x349 | 2026-11-07 |
| 0x03F | Other characters | A Lich at location 28 and his Dark Crusaders | 0x1FC | 2026-11-05 |
| 0x040 | Other characters | Ashley's scene after the Lich | 0x1FD | 2026-11-05 |
| 0x091 | Other characters | Beowulf's first colliery battle with dragon hunters, in the remaster's first colliery battle's place (location 8), on to the remaster's next | 0x1D4 |  |
| 0x098 | Other characters | Balthier at Dorter, a battle; its start waits on flag 0xB7, likely a reward of ReMixed's dispatch tavern (seen in the regression pass, the flag set by hand) | 0x369 | 2026-11-12 |
| 0x0A0 | Other characters | Beowulf and Reis after ReMixed's Bremondt arc (0x09C at location 3 with both in the party, the gate 0x09D, Bremondt's battle 0x09E) | 0x1B3 |  |
| 0x0A4 | Other characters | Biggs and Wedge at Gariland, a tavern scene (map 96), at story progress 4 | 0x2FF | 2026-11-06 |
| 0x0EA | Other characters | Luso at location 24 (Mandalia, chapter 1): the scene and the battle with Luso as a guest; his unit's learned abilities, job level and JP by raw offsets (mapped in the EIU submodule's Ps1Memory); Luso is ReMixed's slot 0x12, drawn with ReMixed's own sprite, with a name row of his own | 0x07B |  |
| 0x0EB | Other characters | Luso, after 0x0EA's battle: his story and his joining (or his farewell, if he isn't in the roster then), flag 0x93 for 0x107 | 0x07C |  |
| 0x107 | Other characters | Luso's thanks after ReMixed's Araguay Woods (0x01D, in the remaster's place), when he was met before (0x0EA-0x0EB) | 0x07A |  |
| 0x10F | Other characters | Algus undead, the second wave of ReMixed's temple battle (0x073, in the remaster's place) as Zalbag falls | 0x1C4 |  |
| 0x11B | Other characters | Inside 0x104's battle (its conditions), on to 0x103 and ReMixed's final battle | 0x376 |  |
| 0x11D | Other characters | The Nemesis's return: the Nemesis System's enemy meets Ramza as one of ReMixed's own battles begins (its Preparation Menu's sets, and 1-A's, the Algus campaign's and Luso's at Mandalia; not 0x0F5's, whose conditions are full, nor the remaster's story battles), talks and gets stronger | 0x1B5 |  |
| 0x06E | Zalbag and Algus | Zalbag at Eagrose (story progress 0x33), in the remaster's place, with its battle (0x06F, 0x10E): Zalbag lives if he has Shell, Reflect or Innocent as Dycedarg falls | 0x36A |  |
| 0x0FD | Zalbag and Algus | The secret campaign's start: after ReMixed's opening with Ramza named Algus or Argath (TUNNEL, on map 98), a battle | 0x300 | 2026-11-06 |
| 0x0FE | Zalbag and Algus | The secret campaign, after its 0x106 | 0x31A |  |
| 0x0FF | Zalbag and Algus | The secret campaign's first battle: location 15 at its progress 0xA | 0x31D |  |
| 0x100 | Zalbag and Algus | The secret campaign: a battle (a looping block, 0x2F) | 0x31E |  |
| 0x101 | Zalbag and Algus | The secret campaign: a battle | 0x31F |  |
| 0x102 | Zalbag and Algus | The secret campaign: its last battle, the roster given back | 0x323 |  |
| 0x103 | Zalbag and Algus | After 0x102's battle or 0x11B, on to ReMixed's 0x07B | 0x324 |  |
| 0x104 | Zalbag and Algus | After 1-A's rescue, from chapter 3: location 15, a battle (Ramza and Agrias made by 0xE0), with Hirame saved; once | 0x375 |  |
| 0x0EC | Side chapter 1-A | Its offer (an in-game menu, both answers seen) and start: Agrias and Orlandu's scene, the roster swap | 0x193 | 2026-11-06 |
| 0x0ED | Side chapter 1-A | Bryn and Lorne join (story progress 2; its menu seen; map 99) | 0x194 | 2026-11-06 |
| 0x0EE | Side chapter 1-A | Its first battle: Agrias's camp by the river | 0x1FE | 2026-11-06 |
| 0x0EF | Side chapter 1-A | Its second battle (story progress 4) | 0x195 | 2026-11-06 |
| 0x0F0 | Side chapter 1-A | Agrias's rescue: its scene and question ("no" skips the battle), then the battle set up after it, Hirame starting Dead (story progress 5) | 0x196 | 2026-11-12 |
| 0x0F1 | Side chapter 1-A | The knight's charge against Bryn and Lorne: its scene and four questions (the second answers fight), then the battle set up after it, on to 0x114's scene (story progress 6) | 0x197 | 2026-11-12 |
| 0x0F2 | Side chapter 1-A | A scene at a castle gate (map 97), on to 0x0F3 | 0x198 | 2026-11-06 |
| 0x0F3 | Side chapter 1-A | A scene (story progress 7) | 0x199 | 2026-11-06 |
| 0x0F4 | Side chapter 1-A | The cursed armory: its scene, the question to fight, and its battle on the armory's map (as 0x34A; story progress 8) | 0x1FF | 2026-11-07 |
| 0x0F5 | Side chapter 1-A | A battle with a question to fight (answered to fight), then 0x115's scene after it (story progress 9) | 0x2E8 | 2026-11-07 |
| 0x0F6 | Side chapter 1-A | A scene with a dialogue choice (both answers go on), on into 0x116's battle | 0x2E9 | 2026-11-07 |
| 0x0F7 | Side chapter 1-A | Its battle on ReMixed's map 75, on into 0x0F8's battle, and its closing after that one (as private event 0x355; story progress 0xB) | 0x2EA | 2026-11-22 |
| 0x0F8 | Side chapter 1-A | Its battle on ReMixed's VOLCANO (map 119) | 0x2EB | 2026-11-06 |
| 0x0F9 | Side chapter 1-A | Gareth's ghost (map 101; story progress 0xC) | 0x2EC | 2026-11-06 |
| 0x0FA | Side chapter 1-A | A battle with a question to fight (answered to fight), then 0x118's scene after it (story progress 0xD) | 0x2ED | 2026-11-07 |
| 0x0FB | Side chapter 1-A | Its last battle: its scene, the question to fight, and its battle on map 32 (as 0x34B), then 0x119's scenes; since 2026-11-12 back to the world map with the story at 0xE, as ReMixed's, the way open to Orbonne (0x0FC; not seen) | 0x2EF | 2026-11-07 |
| 0x0FC | Side chapter 1-A | Its end at Orbonne (location 18, story 0xE): Agrias and her knights come to guard Ovelia, then months later the opening's day from their side (map 56), on to 0x11A; dismisses Whitney | 0x07E |  |
| 0x111 | Side chapter 1-A | 0x0EC in the PSP script (its conditions pick it now): the offer and the chapter's start | 0x2F0 | 2026-11-11 |
| 0x112 | Side chapter 1-A | Inside 0x0F0's battle: Hirame revived, then its post-battle scene (as 0x352) on the branch where she lives | 0x2F1 | 2026-11-12 |
| 0x113 | Side chapter 1-A | 0x0F1 in the PSP script, which the port plays as 0x0F1's scene | 0x2F2 | 2026-11-12 |
| 0x114 | Side chapter 1-A | Inside 0x0F1's battle: its end and its scene after it (as 0x353) seen; its lines on Agrias's turn not seen (the test mode's AI played her, and the remaster doesn't run such an event then; to see by hand) | 0x2F4 |  |
| 0x115 | Side chapter 1-A | Inside 0x0F5's battle, and its scene after the battle (event 8's return) | 0x2F5 | 2026-11-07 |
| 0x116 | Side chapter 1-A | A scene inside its battles (story progress 0xA) | 0x2F6 |  |
| 0x117 | Side chapter 1-A | After the VOLCANO battle | 0x2F7 | 2026-11-06 |
| 0x118 | Side chapter 1-A | 0x0FA's scene after its battle | 0x2FD | 2026-11-07 |
| 0x119 | Side chapter 1-A | Inside its last battle: its victory lines, then its post-battle scene (as 0x354), back to the world map (see 0x0FB) | 0x2FE | 2026-11-07 |
| 0x11A | Side chapter 1-A | After 0x0FC, in Orbonne's chapel (map 57): Whitney, an Ordallian sleeper agent, turns on Ovelia and Delita takes her; the roster and the world map given back, on to the remaster's chapter 2 | 0x07F |  |
<!-- /events -->
