# Bringing over ReMixed's own story content

Planned 2026-11-05, after [story-ship-plan.md](story-ship-plan.md) found that ReMixed's events only make sense in
its own numbering and flow.

**The rule** (decided 2026-11-05): where ReMixed tells the same story as the remaster, the remaster's scenes, lines
and voices stay. ReMixed's own content, which the remaster doesn't have, comes over with ReMixed's text and no
voices. ReMixed's gameplay in the story battles already comes over through its battle data and the mechanics.

## The split

Each line of ReMixed's 278 changed events with text, compared with every line of vanilla's PS1 script and of the
remaster's translation (by shared words, 40% or more counting as the same line; ReMixed reworded much of the
story, so a retold scene scores around 50%):

| Kind | Events | Lines | What happens to them |
|---|---|---|---|
| Retold: the main story | 171 | about 4,000 | The remaster's scenes stay as they are |
| In between (20–45% of lines match) | 21 | about 300 | Read one by one; the remaster's stays unless ReMixed changed what happens (list below) |
| New: ReMixed's own content | 86 | 1,854 | Ported, by group (below) |

In between: 0x00D, 0x01A, 0x01D, 0x033, 0x04C, 0x05D, 0x05E, 0x07E–0x086 (nine 4-line scenes), 0x08E, 0x09C, 0x0C5,
0x0E6, 0x10E.

Read 2026-11-05 (each ReMixed line matched against all of vanilla's script; the unmatched ones read):

| Event | What ReMixed adds | Decision |
|---|---|---|
| 0x00D | Two knights on Gustav's ransom, a line of Ramza's | The remaster's scene stays |
| 0x01A | The end of chapter 1 offers a side chapter, "Chapter 1-A: Delita", before chapter 2 | Goes with ReMixed's own content (a side chapter, not yet mapped) |
| 0x01D | Luso on a chocobo, rescued from goblins in the woods, with the party's comments | Shipped 2026-11-12 in the remaster's Araguay Woods' place (the user's rule: ReMixed's story where it differs): "Luso at Araguay Woods" below |
| 0x033 | Delacroix and Cúchulainn retold, with Gaffgarion there | The remaster's scene stays |
| 0x04C | One dying guard's line | The remaster's scene stays |
| 0x05D, 0x05E | At the split battle, a choice to press on or ask after the other group, and the Southern Sky's archers | The remaster's scenes stay; the split itself comes from the battle data (split formations) |
| 0x07E–0x086 | The Deep Dungeon's floors: leave by the exit (no rewards) or stay to find the way down | A system: the remaster's Deep Dungeon stays |
| 0x08E | The flower girl gives Ramza 5 gil when he has none | The remaster's scene stays |
| 0x09C | Beowulf and Reis ambushed by Aliste for Bremondt's bounty, leading to Lionel | Goes with Beowulf and Reis's events (0x0A0) |
| 0x0C5 | A mission (Of Vagrants) | Ported with the missions |
| 0x0E6 | The tavern errands' menus (cost, requirements) | Part of ReMixed's dispatch tavern ("Dispatch Missions" below), not in the port; the remaster's errands stay |
| 0x10E | Adrammelech retold, with lines for a protected party | Shipped with Zalbag at Eagrose (2026-11-12, the user's call: ReMixed's storyline) |

## ReMixed's own content

Numbers are ReMixed's events (its scenarios); "battle" is its scenario's battle entry.

| Group | Events | Lines | What |
|---|---|---|---|
| Agrias's story | 0x0EC–0x0FC, 0x111–0x11A | about 900 | A storyline with Bryn, Lorne, Balmafula, Elias and Orlandu, its own battles (entries 0x1D6–0x1E0), a Lich |
| Zalbag and Algus | 0x06E, 0x0FD–0x104 | about 160 | The Hokuten side: Zalbag, Algus, Golagros, Teta |
| Other characters | 0x03F (a Lich), 0x040 (Ashley), 0x091, 0x0A0 (Beowulf, Reis), 0x098 (Balthier), 0x0A4 (Biggs and Wedge), 0x0C4 (Gogo), 0x0C6, 0x0EA–0x0EB (Luso), 0x107, 0x10F (Algus, Argath), 0x11B, 0x11D (the Nemesis) | about 600 | Side scenes and battles, several of them recruitments |
| Missions (were "location battles") | 0x0A8–0x0B1, 0x0B5–0x0B8, 0x0C2, 0x0C5 (and 0x0A3) | about 40 | ReMixed's mission list (WORLD 0x8013E4BC, 24 entries), unlocked through its story and taken on from each one's text ("ReMixed's missions" below) |
| Systems | 0x0C7 and 0x0DF–0x0E9 (its dispatch tavern, "Dispatch Missions" below), 0x0A2–0x0A3 (the Depths of Murond), 0x0C3 (loot sets), 0x0C9 and 0x11C (options), 0x0CD–0x0DE (18 message templates), 0x190–0x194 (retry and preparation menus) | about 700 | Menus and reports for systems the port handles its own way or doesn't have; each one decided on its own. The Depths of Murond is shipped at 0x1B2 and 0x1B4 (2026-11-15); its hub, Spend CP, named unit picker and unit upgrade menu are seen in the game |

## What every group needs

1. **How ReMixed reaches each scene.** For each side story: what starts it (a world-map location and the story
   progress it needs, a tavern, the scene before it through the scenario's "next" field at +0x12) and what it sets
   when done. From ReMixed's WORLD data and its scenario records; the gates are in ReMixed's numbering and get
   translated to the remaster's story progress.
2. **New numbers.** The remaster's scenario table (compiled into the exe at 0x8ECAB0: 713 records, room for 0x377)
   leaves 28 ids free below 0x2E7 (0x192–0x199, 0x1EC–0x1FF), and ids 0x2E8–0x376 past its own. The content needs
   about 80, so most go past 0x2E7. The build renumbers each event and what in its code names a scenario (var
   0x27, the scenario to go on to); 0xCE keeps ReMixed's preset numbers.
3. **Scenario records.** The mod appends ReMixed's records, renumbered, to the remaster's table in memory (the setup
   searches it linearly), with their map settings words (the table holds 0x377).
4. **Battle entries.** ReMixed's entries sit where vanilla's story battles are. The remaster's scenarios leave 60 of
   0x100–0x1FF unused (0x150–0x153, 0x172–0x17F, 0x1D6–0x1FF); after checking nothing else uses them, the build
   moves ReMixed's there. If that isn't enough, more entries need a way to load past 0x1FF (the remaster's own reach
   0x249). Their formation records too (ATTACK.OUT's 0x1A0–0x1C3 are empty in the remaster).
5. **Events.** Each ships as `eventNNN.e` (decimal) with the ReMixed marker. Test first whether the mod loader adds
   a file the pack doesn't have.
6. **Text.** Each event's lines as the remaster's scenario text (a `.pzd` and a ScenarioId row; see "Text" under the
   first location battle). ReMixed's newer text already uses most of the remaster's names (Argath, Folmarv,
   Orlandeau); the rest (Algus, Gafgarion, Zalbag) are swapped for the remaster's.
7. **The opcodes and places** each event still needs, from docs/generated/story-readiness.md. The location battles
   all wait on one table (0x80075CB0); the side stories on their own lists.
8. **Units that join.** Recruitments write the roster through ReMixed's code; the roster's layout is mapped, and
   new characters need their sprites and jobs in the remaster.

## Order

1. One small scene end to end, to prove the pieces: a location battle (0x0AD: one line, its conditions, a battle at
   map 9), after the 0x80075CB0 table.
2. The other location battles.
3. Ashley (0x040: 12 lines, one battle), then the other single scenes.
4. Agrias's story, then Zalbag and Algus.
5. The in-between scenes, read one by one.
6. The systems, each decided on its own.

Not planned any more: replacing the remaster's whole story with ReMixed's.

## The first location battle (2026-11-05, on the Deck to test)

ReMixed's 0x0AD (a dancers' fete, map 9) as the remaster's scenario 0x1EC:

- **The table the location battles read** wasn't vanilla code: it's the skillset types (SCUS 0x80065CB4, a byte per
  skillset; the readiness report showed it as 0x80075CB3, the base plus an offset that an 0xE6 fills in at run time).
  It's mapped onto the remaster's (.rdata 0x67E010) and kept read-only. With it, and with run-time offsets counted
  as such, 220 of the 290 changed events need nothing more by the readiness count.
- **Scenario records** go on the end of the remaster's table (`Patches/RemixedScenarios.cs`). The setup (0x2FB7C8)
  reads +2 the map (into var 0x33; not +5, as this said before 2026-11-05: it takes +3 into vars 0x23 and 0x30, +4
  into var 0x24, +5 and +6 into two globals the battle start takes), +7 the battle entry (into var 0x32), +9 and +0xB
  the formations (vars 0x35, 0x36), and +0x16 the key of the battle conditions, which the remaster takes from an NXD table (0xA3), not ATTACK.OUT; with no row for
  the key the setup stops early, so ReMixed's key stays (its event loads its own conditions with 0xCE after).
- **Formation records** (12 bytes, the id at +10; the table behind [0x8ECA58], .data 0x7AE2D0): 0x0AD's two
  (0x1AA, 0x1AB) are empty in the remaster and go in as ReMixed has them. The remaster reads +9 as a map only in
  one mode (var 0x1FE).
- **Battle entries:** the remaster loads 0–0x1FF from the four ENTD files and 0x200 on from
  `fftpack/event_btlevt_bin_entrydata_add_ent.bin` (fftpack file 0x37C, 77 entries). ReMixed's go into ENTD4 slots
  no scenario names (from 0x1D6), not past 0x1FF: the formation setup (0x1EE9B4) reads the entry's row in the EntryNo
  NXD table through 0xFB65C without checking it's there, and that table's index stops at 0x1FF (a row added for
  0x250 wasn't found). That was the crash on the Deck, caught by the mod's crash log (`crash.txt`: exe+0xFB6C8,
  reading 8, with 0x250 in rdx). Each unit's byte 37 (ReMixed's preset, for its code) is cleared, and each new
  scenario gets a row in the Battle NXD table, which has one per scenario.
- **Seen on the Deck (2026-11-05):** after the formation, the event ran (a black screen with an empty beige box, then
  on with confirm) and the battle began, with Hashmal and other Lucavi: ReMixed's man's Bard is job 0x38, a Lucavi
  job in the remaster. The build now maps its units' jobs and secondary skillsets as the job tables port them (0x38
  Bard to 0x5B, its Dark Knight 0x5B and 0x37 to 0xA0). The empty box was the text (below). Two of the player's own
  units (Tony, Rachel) showed Duke Larg's and Goltana's portraits in that battle, which ran on the stock slot (below);
  with ReMixed's data they have their jobs' (0x78 and 0x73, seen on the PC).
- **The Lucavi weren't the job mapping's fault** (that was still needed): the game read the stock ENTD4 for every
  battle, because the mod loader's fftpack replacement doesn't work in this game version (its pattern
  `fileReadRequestOffset` isn't found). Slot 0x1D6 in the stock file is a test battle of seven Lucavi and seven
  Summoners. `Patches/PackFiles.cs` now gives the game the mod's fftpack files when it reads them; on the PC the fete
  then has its three Dancers, three Bards, two Knights and two Monks, at the party's level.
- **Seen on the PC (the harness, 2026-11-05):** the fete starts from the world map, takes ReMixed's conditions,
  builds its units, reaches the formation screen (two groups, 3 and 2 units, at least one in each), and after it the
  scene: ReMixed's troupe in place and the Dancer's line in a speech bubble with her name and portrait; then the
  battle, a Dancer opening with Slow Dance. On the Deck the battle had the right jobs too (the user, after PackFiles).
- **Text:** the enhanced build's events take their lines from neither `event_test_evt.en.bin` nor
  `fftpack/text/eventNNN.en.mes` (no path to the latter in the exe). Each takes them from a Panzer file,
  `nxd/text/scenario/scenarioKKKK.en.pzd` (each line: Id, plain text, a Speaker row's key, its voice): the
  EventMessageResourceManager (0x1DAE20) finds the ScenarioId row whose EventIds hold the event (key 10: [2]), loads the
  file its third column names in the ScionFiles table (key 10's 1000: `nxd/text/scenario/scenario0010.pzd`, the
  language added), and looks lines up there by Id (0x1DB0FC, 0x3D4534). The remaster widened its MESSAGE for that
  (its length helper 0x2CBE2C adds 3 in the enhanced mode, 2 to 0x51): type, style, the line's Id as a u32 (event 2's
  first is 300000), unit, two bytes, x, y, arrow, a u16 closing. So the build converts ReMixed's MESSAGEs (and 0x51s)
  to that layout (a 0xE6 patching a field past the number moves 2 bytes on; the code mod's length table has the new
  lengths); left in the PS1 layout, the remaster read the number and the unit byte as one Id (0x80CF18). The
  remaster's lines run to 315159, so ReMixed's take Ids from 315160: 0x0AD's two "Dancer" lines are 315160–315161 in
  `scenario5015.en.pzd` (FF16Tools' `pzd-conv` makes it from yaml), with a ScionFiles row (2165), a ScenarioId row
  (5015 → [492], 2165) and a Speaker row (6990000 "Dancer"); `root.nxl`'s row counts follow. A box's size comes from
  its message group, which the remaster builds by scanning the event's code for MESSAGEs when it loads (0x1DEC14), so
  each MESSAGE names its line in the file; a line with no group got a box of no width, a letter a row. ReMixed's code
  also writes message numbers in as it runs (0xE6 from var 0x71: 1, or 2 with flag 0xB2), into the Id's low half; the
  code mod moves such an Id (0x40000 + n) onto the event's line n as it's fetched (by the event the runner loaded,
  [0x782A94], and the first line `remixed-scenarios.bin` gives it), and the build puts a never-run MESSAGE for each
  line only reached that way past a GOTO at the event's start, for the scan.
- **The other location battles (2026-11-05):** all 16 (0x0A8–0x0B1, 0x0B5–0x0B8, 0x0C2, 0x0C5) go in as 0x1EC–0x1FB
  (events 492–507: no file of the remaster's, no ScenarioId row), battle entries 0x1D6–0x1E5, their formations, 81
  lines in 16 text files with 22 new speaker names. Seen on the PC: 0x0AE (Govis and the bombs) to its battle.
  **Started from the party's location:** ReMixed's Start handler asks only that the location's entry isn't 0xFFFF;
  the mod's world-buttons hook reads the party's location (.data 0xD493E8, found with the harness's snap/diff by
  moving from Mandalia Plain, 24, to Gariland, 6) and starts that location's battle; the remaster's location
  handler (below) doesn't run at towns, which most of these are.
- **Starting it (first try):** Start at Yardrow (location 7, as ReMixed's WORLD 0x8013E4BC has it) on the world map. The mod
  hooks the remaster's location handler (0x2D2578, its port of WLDCORE's handler for a location, which starts the
  location's scenario of .data 0x7B81B0 on its own button) and calls 0x2D28C8(scenario, 0), which sets var 0x1FC = 1
  and var 0x27 and changes mode (the replay screen's start, 0x169404). With var 0x1FC set the setup takes the battle
  entry from the record's +0xF, so the build fills both. The location handler never saw Start on the Deck (it may
  not run at towns), so with the test mode on Start anywhere on the world map starts it, from the world's button
  function (0x2CC980; Start is 0x800 there, as on the PS1). Seen on the Deck: Start started 0x1EC, the setup took
  ReMixed's conditions and built its units. (Since replaced by the world map's own start of a story event: "How they start", under
  "Battle event phases".)
- **Its conditions:** a battle's first event starts through its conditions ("var 0x1FD = 1 → the event"), which the
  setup loads from the NXD table enhancedbattleevent by the record's +0x16; that table has no key 0xAC, and without a
  row the setup stops before the units. So the record gets key 0, and the setup's hook replaces those conditions with
  ReMixed's preset 0xAC right after. The build renumbers the events ReMixed's presets run (0xAD → 0x1EC), and a set
  that runs an event the mod doesn't ship gets a condition that can't hold (0x192, ReMixed's preparation menu);
  147 presets still run such events another way and load nothing.
- **Coming back:** the story isn't var 0x27 (on the PS1 the world map keeps it in var 0x6E, and each location's script
  sets var 0x27 on entry), so nothing is saved or restored. After the battle ReMixed's 0xAD hands on to its event 8
  (its general after-battle code: results, then the map-settings word to 0x81C3) and event 0xC3 (the loot screen),
  then the world map. Neither is ported yet (event 8 calls the native 0x80150440), so the mod's copy of 0xAD loads an
  empty preset there and sets its word to 0x8000: straight back to the world map. The loot is a follow-up.

## ReMixed's missions (found 2026-11-06, on the PS1's ReMixed)

What the port called location battles are ReMixed's **missions**. Found by running ReMixed v1.2.64.23 itself on the
Deck, headless (`tools/deck/psx/`, docs/TESTING.md), from a new game through its first two battles, and from its code:

- **Just after the second battle** (Gariland, Jan. 1, progress 1) there's none: Start at Gariland opens the world
  map's menu (Move, Formation, Brave Story, Tutorial, Data, Option): [the run](images/remixed-psx-gariland.png).
  The mod had started entry i of the list below on Start at world-map location i, which gave Lost
  Heirloom (its time mages and thieves, on Goug's map) at Gariland that early; that's gone (`RemixedScenarios`).
- **The list**: WORLD 0x8013E4BC, a halfword per entry (the battle, 0xFFFF none). WLDCORE's window type 46
  (0x80092208, opened from window type 28's first case) lists entry i when flag 0x1A4 + i is set; choosing one asks
  (prompt 0xC), then shows its text (world message 0x5A8 + i, prompt 0xE) while window type 47 waits. ReMixed
  replaced type 47's callback (its table 0x8009E690 + 47 × 4) with WORLD 0x8013E4EC: Start while the text is up sets
  var 0x27 to the entry's battle and asks again (prompt 0xE), then leaves the world map for it. Every text ends
  "Press Start to accept mission." It opens from Bonus Content in the Brave Story menu, which Biggs and Wedge's scene
  (0x0A4, "the Rendezvous Introduction") unlocks; ReMixed's changelog and that scene's bartender say so. The chapter-1
  run hadn't reached that scene, so setting the flags alone added nothing to the menus.
  Vanilla uses the same list for something of its own: its 0x075 (after Orbonne's battle) sets flags 0x1A4–0x1AA,
  and so does the remaster's event 117, which the mod runs; ReMixed's 0x075 doesn't. So the flags can't be read as
  ReMixed's unlocks in the remaster without that in mind.
- **What unlocks each** (ReMixed's events that set flag 0x1A4 + i, from its TEST.EVT; the story points from their
  first lines):

| Entry | Mission (its text) | Battle | Unlocked by |
|---|---|---|---|
| 0 | (none) | 0x0A3 | 0x0A3, one of its menus |
| 1 | The dispatch tavern ("There are people all over Ivalice..."; "Dispatch Missions" below) | 0x0C7 | 0x0A4 (Biggs and Wedge, chapter 1, progress 4), 0x11A |
| 2 | Chocobo Defense | 0x0A8 | 0x0A4, 0x11A |
| 3 | Chicken Race | 0x0A9 | 0x01B (chapter 2's start, progress 15), 0x11A |
| 4 | Treasure Hunt | 0x0AA | 0x01B, 0x11A |
| 5 | Teioh | 0x0AB | 0x020 (Dorter, Mustadio chased) |
| 6 | Lost Heirloom | 0x0AC | 0x029 (Goug: "Mustadio... did they catch you?") |
| 7 | The Ball (the fete) | 0x0AD | 0x035 (chapter 3's start, progress 31) |
| 8 | Desert Minefield | 0x0AE | 0x03E (Orbonne: Alma taken) |
| 9 | Littering | 0x0AF | 0x046 (Rafa) |
| 10 | Shades of the Past | 0x0B0 | 0x052 (Malak and Rafa) |
| 11 | The Shrine Knights | 0x0B1 | 0x061 (Orlandu and Olan) |
| 12 | All-Star Melee | 0x0B5 | 0x074 (progress 53), 0x0A3 |
| 13–15 | The Guarded Temple, Nightmares, Brave Story | 0x0B6–0x0B8 | 0x07B (its final battle), 0x0A3 |
| 16–18 | An Ill Wind, Mirror Match, Of Vagrants | 0x0C2, 0x0C4, 0x0C5 | 0x07B |
| 19 | Be A Beta | 0x0C6 | 0x0A4 |
| 20 | Izlude's Last Stand | 0x0C8 | no event found |
| 21 | (none) | — | — |
| 22 | Extra Options | 0x0C9 | 0x0A4, 0x0C9, 0x0EC, 0x111 |
| 23 | Credits | 0x0A2 | 0x0A3 |

  The main-story ones (0x01B to 0x074) are ReMixed's numbers, which the mod doesn't ship: the remaster runs its own
  versions of those scenes, so nothing sets their flags. A port of the list needs its own unlocks, by story
  progress or by the remaster's matching scenes (it starts Goug's, its 164, at location 11 with progress 23).

- **What a mission is** (ReMixed's changelog and the bartender in 0x0A4): a Rendezvous, a repeatable battle that earns
  a spin on the Treasure Wheel (event 0xC3). In one, nobody is lost for good: a player unit whose death counter runs
  out stays down for that battle and is back after it, no crystal or chest ("Outside of a Rendezvous", ReMixed's
  options say of Player Deaths). And no skillset with a Mathematics action menu (type 3 in the skillset types; Math
  Skill, 0x15) comes in: since 1.2.40 that's any such skillset, not only 0x15. Each mission's event does both as its
  battle opens: it clears units 16–20's +0x12 and +0x13 where the skillset's type is 3, and sets flag 0xD9, which the
  death counter reads. ReMixed's other battles' starts clear 0xD9 (0x008, 0x07E–0x086, 0x190).

### In the port: postings in the taverns (2026-11-07)

The remaster has no Bonus Content, so the missions are posted among the tavern **Rumors**, the remaster's own list
with each one's text beside it:

- **The postings:** a Gossip row each (gossip.en.nxd), "Mission: Lost Heirloom" and so on, with ReMixed's text, its
  poster ("—Count Zanes") and its last line as "(Press Start to accept mission. It begins as you leave town.)". The
  game finds a row by its place in the table, so they're rows 23–38, straight after its own 0–22. No row action: a
  rumor's action is the remaster's sidequest progress (seen: a "Secrets Beneath Goug complete" toast).
- **Listed** once ReMixed's story would have unlocked each: the build's MISSIONS give the story progress (var 0x6E)
  where ReMixed's unlocking event falls in the remaster's story, or a flag ReMixed's own events set (Biggs and
  Wedge's 0x1B7; its final battle's 0x1B1–0x1B6). The bar's check of a rumor (0x36F53C) is hooked for those rows.
- **Start on a posting accepts it**: the world map's button function (0x2CC980) runs under the town's and the
  tavern's screens too, and its held words see Start (0x08000800) there. With the rumor list on top of the world
  map's state stack (state 6, depth 3), the posting under its cursor is the list's (+0xC0, the index into its
  entries, 16 bytes each from the vector at +0x3E8, the rumor first), the list being the bar screen's +0x70 as the
  bar's list events name it (0x19D548).
- **It begins as the player leaves town**: once the world map is back to itself (depth 1, state 0, flag 4 off), the
  accepted mission starts as the world map starts its story events. Started straight from the tavern, the tavern's
  screens stayed drawn over its formation. Seen on the PC (2026-11-07): Lost Heirloom accepted in Gariland's tavern,
  its battle on leaving town, its formation screen clean, won, and back to the world map.
- **The Rendezvous rules**: the missions' events run as they are, so Math Skill is removed (seen: Ramza given it as
  his secondary had none in the battle, his roster record unchanged) and flag 0xD9 is set (seen). The death counter
  freezes a player unit while it's set, and the world map clears it, since the remaster's battles don't run the
  events that clear it in ReMixed. The remaster's code doesn't use the flag.
- **Rounds** (2026-11-07): The Shrine Knights (0x0B1) and Brave Story (0x0B8) are fought in rounds. A round's
  preset (0x0B0, then 0x0B1, 0x0B2) runs the next round's event when its conditions hold: 0x0B1 → 0x0B2 → 0x0B3 →
  0x0B4, and 0x0B8 → 0x0B9 … 0x0C1, each on its mission's map with no formation of its own (the party stays). The
  last round loads preset 7, ReMixed's after-battle event, as a mission's end does. The mod ships the rounds from
  0x32C (`MISSION_ROUNDS`); before that their presets were left empty and the missions stopped after the first
  round. Seen on the PC: The Shrine Knights' four rounds, the scene between the first two, back to the world map.
  Brave Story's first round (0x0B8, preset 0xB7) didn't end with all ten of its enemies down (the harness's
  `defeat`; six minutes of turns): what its conditions wait for isn't known yet. Its Death Knights and monsters
  (sprite sets 0x12, 0x32, 0x82) drew as broken stand-ins.
  How a round hands on (read 2026-11-19): the next round's units come in by battle entry, the events stepping var
  0x32 by one (ADD var 0x32, 1), then 0x4C (the map again, with the entry's units in slots 0-15); the party, slots
  16-20, is put back on the tiles the first round's block 2 kept in vars 0x70-0x74. The Shrine Knights do it in each
  round's own event as it begins (the round before sets flag 0x81-0x83 and loads its preset, whose set 0 fires at
  once). Brave Story does it in the round's event as it's won: round 1's block 3 loads round 2 and its preset (0xB8),
  whose set 1 (the plain victory, type 0x16) runs 0x0B9, whose blocks 0 and 1 are one code that loads round 3, and so
  on (set 0, var 0x7D = the round less one, holds only while the event that loads the preset runs: it sets var 0x7D
  as it begins and zeroes it at its end). So 0x0BA loads only
  as round 3 is won. ReMixed's records leave the hand-off mode (+0x14) 0, so the remaster runs these events after the
  turn, not between actions (0x2387C4: `0xD3F950[event] & 0xF300`, a table the scenario setup makes from the records'
  +0x12-0x14). Each mission's entries are consecutive in ReMixed (0x1A4-0x1A7, 0x1AB-0x1B4), but the port's slots
  weren't (the first rounds 0x1DF and 0x1E3 among the location battles, the rounds from 0x1EA), so each second round
  loaded another battle's units; since 2026-11-19 each mission's go in a run (`MISSION_ROUND_CHAINS`): 0x1D6-0x1D9,
  0x1DA-0x1E3. So The Shrine Knights' rounds seen on 2026-11-07 had other battles' units from round 2 on; on their
  own units (2026-11-19) the last round's reinforcement scene (three Dragoons where its first three Knights fell)
  crashed the game, its third camera naming a unit the round hasn't (0x8B for 0x87, fixed in the build), and the
  mission now plays through its four rounds to the Treasure Wheel. The map's reload takes the deployed party off the field (+1 0xFF), and the
  rounds' FUNCTION 00 puts back each of units 16-20 whose sprite set isn't 0, the remaster's spare copy of the leader
  in unit 20 too.
- **The Treasure Wheel** (0x0C3, shipped as 0x368 on 2026-11-12): a mission won hands on to it (its vars 0x3A–0x3C
  name 0xC3), and it runs on map 116 (a zodiac floor) with its twelve chests, units 0x90–0x9B in a circle: battle data
  of Ramza's sprite set given ReMixed's event graphic 0x6F (LoadEVTCHR, 0x58), which vanilla's events never load and
  ReMixed's 0x098, 0x099, 0x0C3, 0x0CD–0x0DE and 0x192 do. Three things kept it from playing (held back on
  2026-11-07, when the chests drew as black figures and the scene stopped at the first opening): it sets all twelve
  chests' frames with unit numbers past the groups (0x0290, 0x0202), which the PS1 reads as every unit and the
  remaster as none (the code mod has it read them as the PS1 does: `Patches/EventUnits.cs`); with the enhanced
  sprites the remaster draws 0x6F from a texture of
  its own (vanilla's art, nothing where ReMixed's chests are; the build ships ReMixed's pixels doubled as that
  texture); and its boxes, which with no unit all went to the screen's top left, and the middle one, which the PS1
  closes as it's read, stayed open, over the black screen at the end, so it never handed back (the build centres them
  and closes the middle one). ReMixed's line along the bottom (the chest in front's kind, the prize once read) is box
  2 at the screen's bottom, closed before the cash-in and gold-chest menus open there (a menu over it kept it open
  and the scene hung on its END); `Patches/TallMenus.cs` fits the centred boxes to their text (seen on the PC in the regression pass, 2026-11-12; its top and bottom lines, ReMixed's frameless ones, go on without a press as on the PS1 since the same day: `Patches/TallMenus.cs`). Its loot set is var 0x15, set from the battle's entry as ReMixed numbers it
  (`LOOT_SET`); its stages read the story's progress where ReMixed reads the missions' unlock flags
  (`mission_unlock_reads`); its JP cash-in writes the roster's JP (the remaster's +0x80 and +0xAE for the PS1's
  first 20 jobs, at most 9,999). Controls: Left and Right turn the ring, Circle or Cross (F or Enter, a pad's A) opens
  the chest in front, Start (Space) cashes in what's left, Triangle the chapter-4 gold chest for gil (no key by
  default on the keyboard). A pad's Start is also the remaster's Pause in a scene, which once opened over the Wheel
  instead of the cash-in (GAMEPLAY_TODO.md).
- **Be A Beta** (0x0C6 as 0x349, entry 19; built 2026-11-07): posted with Chocobo Defense (Biggs and Wedge's flag
  0x1B7, else progress 15). Its event dispatches its phases itself (a SWITCH on var 1, GOTO labels 0x70–0x7A). The
  opening names the testers by writing ReMixed's name characters into battle units (+0x12C, three or four words, the
  end 0xFE last): ArmoredKori (0x80), Caazan (0x81), NinjaWeazel (0x82), Akashachi (0x83), Banana Phone (0x84),
  Cheeto, Wasabi and Nacho (0x85–0x87, chocobos) and OronJoker (one of 0x90–0x97 at random, given id 0x88). The port
  writes such a name, once whole, as the remaster's 0x40 bytes of UTF-8 at +0x14C (`Ps1Memory.UnitNameAt`). What
  the remaster shows is the name id's row (+0x1FC, table 0x5D `charaname` through 0x1E8B7C), the unit's own text only
  for a row whose +0x14 (IsGeneric) is set; Be A Beta's battle data gives every unit id 0x71, a blank row, so the
  name guard shows the unit's own text for a blank row (`NameGuard`). A story character's row isn't blank, so
  ReMixed's random-battle event (0x191), which writes names too ("Argath", "Zalmour" with flag 0xB2, which the port
  reads as 1), leaves the remaster's names. The opening also builds Caazan from NinjaWeazel: job 0x56, skillsets 0x11
  and 0x12, a level up (0x80150440, ported), and NinjaWeazel's +0x13C–0x149 (a second 16-byte text the PS1's unit
  screen shows) and +0x72–0x80 (learned abilities); neither has a place in the remaster (its name took the room after
  +0x14C, its learned bits are +0xA2 + 3 × (skillset − 5)), so Caazan doesn't get those. Its text switches var 83
  (0x53) mid-line (`TEXT_SYNC_UNWAITED`). When ArmoredKori's HP reaches 0 (set 3: type 6, and flag 0x80 clear),
  Banana Phone takes his place, then Cheeto, Wasabi and Nacho in turn (sets 6–9), each stronger; the end is set 2,
  Akashachi and the rest down. Seen on the PC: the names written and shown, the swap, and the battle to the world
  map. Unplaced: +0x1B1 (a byte), 0x8019FD7C and 0x80059434 (a count at the end).
- **Treasure Hunt** (0x0AA as 0x1EF; 2026-11-12): its preset (0xA9) ends it when a unit of the player's team reaches
  the chest's tile (0, 4) and fails it when a Rogue's does ("Heh heh heh! It's all ours now!"), through ReMixed's own
  condition type 0x25 (a team on a tile), which the remaster's checker reads as vanilla's (the unit whose +4 is the
  value). With ReMixed's types in the walker, seen on the PC: Ramza put on (0, 4) won the mission at once (on to the
  Treasure Wheel, as missions go now). Its chest is a unit with ReMixed's preset 5 (Treasure, class evade 0, its base
  HP scaled to 1), its Rogues and thieves are immune to Invite (preset 6), seen in the battle's units.
- **Mirror Match** (0x0C4 as 125, entry 17; built 2026-11-12, not seen in the game yet; `MIRROR_MATCH`): Famed Mimic
  Gogo is five units of its battle data (0x85–0x89 on map 116), generic men with job 0x5D: ReMixed draws them with
  vanilla's Mime (its 84.SPR draws as vanilla's MONO_M.SPR), and the remaster draws them with its own Mime, so there's
  no stand-in to choose. Its opening copies each party unit over one of units 0x80–0x84 (the entry's units 0–4, not on
  the map yet: the party's event ids are Ramza's sprite set and 0x78 on, as both games' formations number them), puts
  each copy on its unit's tile with y 0 and 1 turned to 9 and 8, warps a Gogo onto each, has one say a line by the
  wins so far, then the Gogos go and the copies come in. Each win makes the next copies stronger: flags 0xD0–0xD3 count
  them, and with 0xD0 set the copies get +1 PA and MA, with 0xD1 +1 Speed too, with 0xD2 ten times their HP (and other
  music each time, op 0x14). The victory ("Bravo! Bravo! Adieu!") counts the win and hands on to the Treasure Wheel.
  What the event read in the PS1's memory: 0x8019FA04 + 5i and 0x8019FA6D + 5i are vanilla's snapshot of unit 16 + i's
  innate and current statuses (BATTLE.BIN 0x80148580 into [0x80173C78], 0x8019F9B4: five bytes a unit, from +0 and
  +0x69), which the PS1 takes as a battle's opening event starts (flag 0x1FD) and puts back at its end; the event puts
  them back on the party before the copy, a no-op in effect since nothing changes them in between. The remaster keeps
  no such copy, so the build passes that loop over. 0x8019FD7A + 2 × unit is ReMixed's own table of each unit's Max HP
  (written by its level change and unit making); the event writes the copies' there, and no code reads it: it goes to
  the upgrade's store. The copy itself is by raw PS1 offsets (the sprite set, job, +6's gender bits, +0x09–0x160 a
  byte at a time, then +0x162, +0x16C–0x16E, +0x184–0x185 and +0x1BB–0x1BE), which the remaster's wider units don't
  hold as the PS1's (equipment halfwords, 22 jobs' learned abilities and JP, the name as UTF-8), so the build has the
  event call a routine of the code mod's own in its place (`RemixedEvents.MirrorCopy`, `0xCF 0x000000C4`; the
  remaster's +0x09–0x190 and the fields matching the rest, self-tested). Posted from progress 53 with An Ill Wind and
  Of Vagrants, whose presets end the same way (below). Izlude's Last Stand (0x0C8, entry 20) is an empty event in
  v1.2.64.23 and nothing unlocks it: nothing to port.
- **After ReMixed's final battle**: entries 13–18 are unlocked by ReMixed's 0x07B (13–15 also by one of 0x0A3's
  menus), which the remaster's story never runs (the mod ships it only for the Algus campaign). So the mod posts
  them late in chapter 4 too (asked 2026-11-07): from story progress 53, the party's last stretch on the world map.
  The remaster's last world-map starts are Mullonde's (location 16) at progress 52 (its events 441 and 443), and at
  53 Mullonde has only entries without a start; All-Star Melee is posted from 53 as well. Posted there: Of Vagrants
  (18) and An Ill Wind (16), each played to its end on the PC (their presets end on the plain victory test, type
  0x16, which the harness's `defeat` meets). The others stay on their flags until played through for real: The
  Guarded Temple's preset 0xB5 ends on records of types 4 and 6 for units 0x80 and 0x87 with var 0x7D counting
  turns, Nightmares' 0xB6 on each of five units (0x43, 0x3C, 0x3E, 0x45, 0x40) with flags 0x81–0x85 and var 0x7D
  reaching 5, and Brave Story's first round didn't end (above). Mirror Match (17) is posted there too since
  2026-11-12, its preset (0xC3) ending on the plain victory test as theirs do (not seen in the game yet). Seen on the PC: the
  postings at 53, and Start on Of Vagrants, the last of the scrolled list, taking it (the cursor is the list's
  index, not the row on screen).
- **A battle's scripted failure** (2026-11-07): `0xCE 4` then `0xCD 00 00 00`. Preset 4 runs ReMixed's 0x190, its
  game-over menu (Retry Battle, Return to Title Screen, Return to World Map), or 0x005; the mod ships neither, so the
  preset's sets can't fire and the word of 0 left the battle going with its enemies down: the closing ran again each
  turn (The Guarded Temple's block 7, seen on the PC). The build now treats such an idle preset as it does preset 7:
  the empty preset instead, and the hand-off to the world map (`RETRY_MENU`, `idle_presets`). Six scenarios have
  that path: Chocobo Defense (twice), Treasure Hunt, Desert Minefield, The Guarded Temple, 0x006 and 0x119.

## The Preparation Menu and Dispatch Missions (read 2026-11-11)

ReMixed's Preparation Menu (event 0x192) opens before its battles: Start Battle, View Map, Reveal Treasures and Extra
Options, then lines the battle picks. Its text has five slots (code 0xEB) that `op9D slot, message − 1` fills
before the menu shows: one after Reveal Treasures, four after Extra Options. From its code and text:

- **Save Game, Save & Formation, or Save, Formation, & Redeploy**, by var 0x3B's high nibble, which also decides
  whether **Return to World Map** is offered (Gariland's battle: Save, Formation, & Redeploy, no return).
- In random battles (flag 0x8E; its Flee line speaks of them): **Change Music** (arrows change the track, a button mutes, one picks at random),
  **Temporarily Retreat** ("come back later to see if any of these units leave or if any new ones arrive") and
  **Flee to World Map**, which costs gil after fleeing "too many random battles lately". A third layout (flag 0x8D)
  has Change Music, Temporarily Retreat and Return to World Map.
- **Dispatch Missions** once flag 0x1A5 is set (entry 1 of the mission list above: Biggs and Wedge's scene 0x0A4,
  chapter 1 at progress 4, and 0x11A set it). After "Are you sure?" it hands on to 0x0C7 (`0xCD 81 C7 00`, var
  0x28 = 0xDDDD; Extra Options takes the same path with 0xE0E0 to 0x0C9), which loads a tavern scene and runs
  ReMixed's tavern menu (0x0E5). Seen on the PC's emulator (Gariland's battle, the flag set by hand): the
  Tavernmaster, then Propositions, Jobs, Special Training, Contests, Recall Units, Formation and Exit Tavern.

The tavern is ReMixed's own errand system, in events 0x0DF–0x0E9 (673 messages):

- **Propositions** of six kinds (Salvaging 0x0DF, Mining 0x0E0, Exploration 0x0E1, Combat 0x0E2, Investigation
  0x0E3, Odd Jobs 0x0E4), offered in sets of up to five that cost gil. Each mission has its own posting and report
  ("Seafarer Expedition", "Frontier Expedition", Phantom Thief Zero's note) and preferences to meet "for perfect
  success odds"; after a failure the Tavernmaster suggests Special Training.
- **Jobs** (Mining, Logging, Farming, Hunting): repeatable, one unit at a time at first, then two, three, and
  several groups as "they expand their operations". Picking one asks which unit goes, and the unit walks out.
- **Special Training**: one group at a time on a course that costs gil.
- **Contests**: one a month, for a price, scored by Job Levels.
- **Recall Units**: call a group back, or hear finished missions' reports and what they earned (the Treasure Wheel
  among the rewards); mission levels go up as they're done.
- Units sent out are back "in about N days, or after beating one battle".

The port has its events built and running (seen on the PC: its menus, sets and prices), not yet posted: sending a
group waits for ReMixed's own button reads, which the remaster doesn't feed in that scene. The Treasure Wheel ships
from 2026-11-12 (above, with the missions). How it works in full, and the plan for porting it:
dispatch-missions-plan.md. Of the Preparation Menu, the port has Start Battle and View Map
as the remaster's formation screen, Reveal Treasures (asked as that screen opens) and Extra Options (the ReMixed tab
of Settings).

## Battle event phases (found 2026-11-05)

ReMixed's battle events keep their phases in blocks: `0xD1 n` skips to the instruction after the next `0xD2 n`
(the remaster's 0x2425D0 does it, scanning with the length helper, up to the END). Each event starts with
`E6 00 01; D1 ff; D2 00`, so the 0xD1's n is var 1 when the event starts: 0 for the opening, and the battle's
conditions (ReMixed's preset, e.g. the bombs' 0xAD: chains of three-halfword conditions ending in `0019 event`, with
a small varying number that looks like the phase) run the event again for each later phase. Traced on the PC with
the harness: the opening runs to message 1, then `D1 ff` jumps to the END and the battle starts; a later run had
var 1 = 0x80192BCC (left by the event's opening loop, `SET_VAR var 0x1, var 0x71`), found no block 0xCC, and the
remaster's search, finding none, ended the task and went back to offset 0: the event ran from its start forever
(the closing scene that hung). The mod now runs 0xD1 itself for ReMixed's code (`RemixedEvents.PhaseBlock`): the
same search, but a missing block goes on to the END.

- **The phase in var 1** (2026-11-05): ReMixed's run-event action (BATTLE 0x80159BE8) writes the firing condition
  set's index (the walker's s1) to var 1 before var 0x27. The remaster's walker (0x2391C4: 16 sets at 0xD40040,
  records at 0xD3F550, the checker 0x239294) doesn't, so for ReMixed's scenarios the mod replaces it whole
  (`RemixedScenarios.Walk`): the same walk through the game's checker, and var 1 = the set that fired. The bombs'
  closing scene (message 4, Govis's thanks) then ran on the PC.
- **The stand-in preset**: presets that run events the mod doesn't ship (ReMixed's after-battle event 8, preset 7)
  are swapped for an empty one; all zeros fired at once (record type 0), so it holds one condition that can't hold
  (var 0x1FD = 0x7FFF).
- **How they start (2026-11-05)**: as the world map starts its own story events. Its conditions' type 25 action
  (0x3443FC) only fills two globals (the event in 0xD493CC, its mode in 0xD493D0) and flags; on arrival at a location
  (0x247E8C) the world map runs the location's conditions and, when one fires, starts the event with 0x247E10: var
  0x27 = the event, then its state 0x23 (0x2CC2E0, the mode: 2 cuts straight to it, as 33 of the remaster's 63
  starts do; 1 the others) on its state stack (the depth at 0xD8DBA0, the states at 0xD4C948, the idle map is state
  0). That state leaves the map, and the world's loop (0x246078) returns 0, a story scenario, to the game's main loop
  (0x2D3FA0). The mod does the same from the world's buttons: the event and mode into those globals, then 0x247E10.
  The first tries used the replay screen's start (0x2D28C8) instead, which marks the run as a replay (var 0x1FC, the
  world's flag 0x40000 at 0x1D32E14, its loop returning 2, and the globals 0x2FF9E4C and 0x2FF9E50); a battle started
  that way went to a battle's preparation when its closing sent it back to the world map. Started the world map's way,
  the scenario also shows its map's name card first, as the remaster's own do.
- **The battle's end (solved 2026-11-05)**: the remaster closes a won battle with a scenario of its own, run by the
  battle's conditions (the set with a record of type 0x16, all enemies down; 0x01E's closing is 0x020), whose
  map-settings word (its record's +0x12 to +0x14; ReMixed's `0xCD` writes it) is read when its events end (0x23987C):
  0x80 back to the world map (28 of the remaster's 50 closings; var 0x27 = 0, mode 9 to 0x20DAEC), 0x81 on to a
  scenario (22; mode 10). ReMixed's battles hand on to its after-battle event 8 instead (results and loot, through
  preset 7 and `0xCD 00 00 00`), with where to go after it in vars 0x3A to 0x3C (0xC3, the loot event, for the
  location battles; 0x40, Ashley's scene, for the Lich). The build now writes that hand-off as 0x81 to the scenario
  event 8 would have gone on to when the mod ships it, else 0x80, and clears ReMixed's flag 0x8E there. Seen on the
  PC with the harness: the bombs battle (0x1F2) from its start through its opening, the battle and Govis's thanks back
  to the world map, which then answered as usual. The loot screen (0xC3) is still to port. Ways that didn't work,
  while the battles were started as replays: 0x80 (a battle's preparation, then a crash or the game closing; Windows'
  log has those crashes), `0x43 06` (var 100: waits forever inside a battle), and a word of 0 (the battle goes on with
  its enemies down).
- **A crash after Ashley's scene** (2026-11-05): the Lich battle's closing went on to Ashley's scene (0x1FD) as it
  should, and about 15 s after that scene ended the game closed: an access violation at exe+0x284E4A, in 0x284A08,
  which turns a roster record (54 records of 600 bytes at .data 0x11A7D10) into a battle-side record and copies its
  name from the unit-name lookup (0x1E8B7C: the record's name id at +0x230 in an NXD table) without checking it found
  one. Most of that lookup's 22 callers don't check either, so the mod now guards it (`Patches/NameGuard.cs`): a
  missing name logs its id once and the unit gets a stand-in name instead of a crash. With it the chain ran on to the
  world map, and the guard named id 0: the record was Ramza's (roster slot 0), blank. It was whole before Ashley's
  scene. The harness's write watch on it caught the writer: the end-of-scene roster update (0x2C60E4) writes each
  battle unit back to the roster slot in its byte +2 (0x274EB8). The scene's units (`slots`): its own Ramza (slot 0xFF,
  so nothing written) and Ashley, listed with ReMixed's battle-data flags 0x98 ("taken from the roster", joins); she
  isn't in the roster, so the remaster left that unit empty (sprite set 0, job 0) with +2 still 0 and battle id 0x5B,
  and the update wrote the empty unit over slot 0. The mod now never writes an empty unit back (`Patches/RosterGuard.cs`:
  the write answers 0xFFFFFFFF, "not written", which all five callers handle). Seen on the PC: after the scene Ramza's
  record is whole, the guard logs the unit it skipped, and nothing has no name. Why she wasn't there (found
  2026-11-13): the Lich's battle lists her as a guest it saves (entry 0x141, unit 0: flags 0x93, joins and saves her
  formation; battle id 0x5B), and her scene (entry 0x142, unit 1: 0x98, joins and is taken from the roster) finds a
  guest by sprite set in the roster's guest slots 50-53 (0x2766C8). A guest gets there only through the battle's
  write-back (0x2C60E4: a unit not from the roster with 0x10 and 0x01 goes to the first free guest slot, 0x274EB8 →
  0x2751D0), and the write-back isn't something a battle's end does by itself: it's event instruction 0x78 7
  (DisplayConditions 7; the remaster's 0x2CB9E8 state 7 → 0x2C8818 → 0x2C60E4, skipped in a replay), which every
  vanilla closing runs after its results (0x78 2-6). ReMixed moved those into its after-battle event 8 (0x78 2-6 at
  its 0x10385, 0x78 7 at 0x106DE), which the port doesn't ship (AFTER_BATTLE_EVENT): so after the Lich's battle
  nothing wrote her back. The log of the PC's run (2026-11-12) agrees: the battle end's hook (the test recorder's
  "event scripts loaded in this battle") ran only at the scene's own 0x78 7, never between the battle and the scene.
  The build now puts event 8's write-back (`0x78 07 3C`, `0xE5 38 00`) before the Lich's battle's hand-off to her
  scene (now AFTER_BATTLE_HOME, every battle's: below). Seen on the PC the same day (`harness` from the test save): after the battle
  her record is in guest slot 52 (sprite set 0x34, name id 0x42), her scene's unit 1 is loaded from it (roster slot
  0x34, flags 0x92), and at its end the remaster's "New Party Member" screen has her join (party slot 3), as the
  PS1's "JOIN UP!" does (work/harness/compare/ashley-joins). Her scene's start (HAS_CHARACTER 0xC0 0x34, then the roster's +4 & 2, ReMixed's
  guest mark from the battle data's 0x02) takes the "joins" path either way: the upgrade's HAS_CHARACTER doesn't look
  in the guest slots, which the PS1's check would find with the mark set. At the scene's 0x78 7 the remaster drops
  the guest record (a guest slot's unit without 0x01) and makes her join for good (0x10), as ReMixed's write-back
  (REQUIRE.OUT 0x801C4878) does. Her name: ReMixed's name id 0x34 is the remaster's "Agrias", so she has CharaName
  0x42, "Ashley" (ASHLEY_NAME), as Luso has 0x38; that also keeps OldGuests from taking her record for the remaster's
  guest Agrias (sprite set 0x34, name id 0x34) in a save from before progress 0x16, as the PC's test save is.
- **Every battle's write-back (2026-11-13).** ReMixed's other battles skipped the write-back the same way: no EXP,
  JP, levels, learned abilities, unit lost for good or invited one reached the roster, nor did the battle end's hooks
  run (`Patches/BattleEnd.cs`: Nemesis, Bonus EXP & JP, the stolen gear's return, the tavern's flag); armed at the
  battle's start, they ran at the next 0x78 7 anywhere, on whatever units that scene had. Event 8 runs its 0x78 7 on
  every way through its after-battle block (its block 0xFE: results 0x78 2-6 at 0x10385, the write-back at 0x106DE,
  then the hand-off at label 0xA0, a return to the battle's preset included), so every hand-off that stands for it
  runs the write-back, then sets the tavern's flag: 42 shipped events have one ordinary display-state-7 hand-off
  (`AFTER_BATTLE_HOME`), and 13 encoded same-map return edges use the live writer described below
  (`AFTER_BATTLE_RETURN_HOME`; the split/copy event sets overlap). These cover the missions on to the Treasure Wheel,
  1-A's and the Argath campaign's battles, and the side stories. The four ways
  through: on to the next scenario or the world map (the most); a return to the battle's own events for its
  post-battle scene (AFTER_BATTLE_RETURN: 1-A's 0x112, 0x114, 0x115, 0x119, 0x0F7, and 0x0EA), written back as the
  battle is won, before the scene, as on the PS1; a return chosen as the event runs (RETURN_CHOSEN: 0x01D, 0x11B),
  written back on both of its paths. The remaster's display state 7 also dismantles the battle renderer, so using
  `AFTER_BATTLE_HOME` for these same-map returns made their correctly running dialogue appear over black. They now use
  `AFTER_BATTLE_RETURN_HOME`: EIU native routine 0x000000C5 calls the already-hooked 0x2C60E4 synchronously, without
  0x2C8818's display/teardown, then loads the returned preset on the live map. The build and story regression checker
  require this at all 13 encoded return edges (including split/copy events) and forbid state 7 before a mode-0 return.
  Where the scene writes back again (0x0EA's 0x0EB, 0x01D's 0x107, 0x11B's second
  run) the units are written back twice, as on the PS1; the hooks run at the first. Units the write-back makes join
  (ENTD flag 0x10): Ashley in 0x03F (saved as a guest, 0x01) and the chocobo in 0x01D (also "from the formation",
  0x08, so an empty unit when the roster has none, which `RosterGuard` keeps out). The Argath campaign's first battle
  0x006 marks three generics "joins" (battle ids 0x91-0x93), but they aren't in the battle (their data's presence
  byte is 0). On the PS1 event 8 runs once the battle's event has ended; here the write-back comes at the hand-off,
  before the rest of that path, which is story progress and flags everywhere but 0x006: its Argath path makes Delita
  join at once (0x43 1) and clears the flags it set for that, after the write-back here, before it on the PS1, to the
  same end. Not run: event 8's results screens (0x78 2-6, the PS1's Bonus Money and spoils). Seen on the PC
  (2026-11-13, harness, the test save): a battle unit's EXP and JP poked mid-battle (EXP 77, JP +500 in job 0 and its
  total) are in its roster record after a mission (0x1F2, through the Treasure Wheel: slots 0 and 7 kept) and after
  1-A's 0x0F5 battle (0x2E8: the test mode's record "won" at 04:19:28, then 0x115's post-battle run as event 844 at
  04:19:30, the world map with var 0x6E 9; slot 0 kept, EXP 84 with an AI action after the poke). Before, the
  test mode recorded ReMixed's battles as not reaching the battle end. ReMixed on the PS1, the same poke from
  `ash-bt2` (the Lich's battle) and `f0-fight2` (1-A's rescue, Ramza's turn ended by Wait): the roster is unchanged
  while event 8 runs (var 0x27 8, frame 600) and has the values by frame 1200, before Ashley's scene (0x40) or the
  rescue's post-battle scene (0x112) runs.
- **Other hand-offs**: a ReMixed event's `0xCD` with mode 0x81–0x83, or `SET var 0x27`, naming a scenario the mod
  ships is renumbered by the build; scenarios that share a battle entry share its slot.

## How the side stories start (mapped 2026-11-05)

ReMixed's side stories (other characters, Zalbag and Algus, Agrias) have no "next" links and mostly map 0 in their
scenario records: they start from the world map's location conditions, the same system vanilla's story uses.

- **PS1:** WLDCORE's condition interpreter (0x80091238, a0 the location from [0x8009F254], a1 a mode) takes the
  location's list of halfword offsets from a table at [0x800D4674]; each entry is a program (a stream of halfword
  opcodes and their arguments, 41 opcodes, handlers at 0x8009EE50) run until it ends. An entry with bit 0x8000 points
  into ReMixed's own programs at WORLD 0x80149A00–0x8014A020 (uncompressed): e.g. `0001 006E 000D` (story progress,
  var 0x6E, compared with 0x0D), `0004 0078` (a character in the party), `0019 009F 0002` (start event 0x9F). Which
  locations list them is in the table behind [0x800D4674] (its loader not found yet; WORLD.LZW is a set of
  compressed sections, possibly not it).
- **The remaster:** the same programs as the NXD table EnhancedWorldEvent (id 199, 540 rows; Key the location, Key2
  the entry, Key3 the instruction; Type the opcode, Unknown10 a variable, Unknown8 the values, Unknown14/18 two more
  arguments, 47 when unused), run by 0x343AF8 (44 opcodes, .rdata 0x684E90). Location 0's first entry: Type 1, var
  110 (0x6E), value 32.
- **The table and the side stories' starts (read 2026-11-05):** the location table is in WLDCORE.BIN at
  0x80097234 (44 halfword offsets, each to a list of program offsets ending in 0; [0x800D4674] = [0x80096A54]).
  ReMixed rewrote every location's list; locations 14–43 run only its own programs. The opcodes seen: 0x01 var
  value (var == value), 0x02 var value (at least: WLDCORE 0x800914BC fails while the var is below the value), 0x03
  var value (at most: 0x80091518 fails while it's above; read 2026-11-12, this note had it as "at least" before),
  0x04 c (character c in the party), 0x05 (no arguments), 0x2C
  x, 0x1E a b, 0x1F a b, and the actions 0x19 event mode (start an event), 0x1A (start one of eight events), 0x20
  a b, 0x22 a, 0x24 a. In the remaster's rows: 0x01 is Type 1 (Unknown10 the var, Unknown8 [value]), 0x19 Type 25
  (Unknown8 [event, mode]), 0x20 Type 32 (Unknown14, Unknown18), 0x22 Type 34 (Unknown14), 0x04 Type 4 (Unknown8
  [character]); 0x343AF8 makes each row back into the PS1's program, and any other type is the type and all of
  Unknown8's values, so Types 2 and 3 carry [var, value] there (its ops 0x3440AC and 0x3440F0 are the PS1's 2 and 3).
  24 entries start
  ReMixed's own events, e.g. Gariland (6): progress 4 and var 0x97 → 0x0A4 (Biggs and Wedge); Dorter (9): vars 0xB7,
  0xB8 → 0x098 (Balthier); location 24: vars 0x92, 0x93, progress at most 0xF → 0x0EA (Luso); location 28: progress
  0x25, var 0x9D → 0x03F (a Lich); Eagrose (2): progress 0x33 → 0x06E (Zalbag); and 0x0ED–0x0FB at progress 0–0xD
  across a dozen locations.
- **Side chapters:** those low progress values are a side chapter's own count. The end of chapter 1 (0x01A) offers
  "Chapter 1-A" ("your items and gil will be cleared … your units … returned to you after"); choosing it sets var
  0x27 to 0x0EC, so 0x0EC–0x0FB (filed above as Agrias's story) are that side chapter, run on the story-progress
  variable from 0. Porting it needs a side-chapter mode: put the party, items, gil and progress aside, run the
  chapter's world-map conditions, and give everything back at its end.
- **So the port:** find which locations list ReMixed's 0x8000 entries, map the PS1 opcodes' arguments onto the
  table's columns (vanilla's own programs against the remaster's rows give the mapping), translate ReMixed's
  programs (renumbering their events and story-progress values into the remaster's), and add them as rows. Each
  side story's events, battles (their entries need free slots too: 26 of 0x1D6–0x1FF are left) and recruitments
  follow.

## The other side stories, read (2026-11-05)

How each of the 48 events left is reached (world-map starts, hand-offs from other events, other battles'
conditions), and what it still needs (docs/generated/story-readiness.md):

- **Side chapter 1-A** (0x0EC–0x0FB and 0x111–0x119, 25 events): ReMixed's end of chapter 1 (0x01A) asks "Continue
  to Chapter Two" or "Play Chapter 1-A"; the second, with 14 or more units in the roster, hands on to 0x0EC. Its world
  starts run on story progress 0–0xD (the chapter's own count, from location 5 at 0 to location 9 at 0xD); 0x111–0x119
  are scenes inside its battles (run by their conditions, e.g. 0x0EC's preset runs 0x111). 0x0EC and 0x111 swap
  roster units through ReMixed's native 0x80160600 (ported: two slots trade their records, but for byte +1). Most of
  its events need nothing else. Left: offering it after the remaster's own 0x01A, and the mode itself (the party, items,
  gil and story progress put aside and given back).
- **Zalbag and Algus** (0x06E, 0x0FD–0x104): 0x0FD follows ReMixed's 0x001, 0x0FE its 0x106, 0x0FF starts at location
  15 (progress 0xA), 0x103 follows 0x102's battle, 0x104 at location 15 needs the side chapter's var 0xAE; 0x06E
  starts at Eagrose (location 2) at progress 0x33, where the remaster has its own scene. Six of them need 0xE0.
  Read further (2026-11-06): **a secret campaign.** ReMixed's 0x001 is the opening (Orbonne); it compares Ramza's
  name (roster +0xBE on, ReMixed's charset) with "Algus" and "Argath" and on a match hands on to 0x0FD instead of
  the story (`0xCD 81 FD 00`). 0x0FD (map 118, TUNNEL) hands on to ReMixed's 0x006; its 0x105 and 0x106 set the
  progress to 4 and 10, 0x106 hands on to 0x0FE; 0x0FF starts at location 15 at progress 0xA and its battle hands on
  to 0x100, 0x101, 0x102 (after-battle var 0x3C), 0x102's to 0x103 (var 0x3B), and 0x102's end gives the roster
  back (ReMixed's roster swap, 0x80160600, the errand bytes, unit 0x6C dismissed, PS1 0x80057EEC-0x80057F33
  cleared). 0x103 hands on to ReMixed's 0x07B, its version of the final battle (map 55: Alma holding back Ajora's
  resurrection, "Come out, my servants", goblins and a chocobo among them). 0x100's 0x2F (in place in the wait dispatcher, BATTLE 0x80159868 from 0x8013ECD0) repeats a
  parallel block while var 0x70 isn't 0: passed over, its animation plays once. **0x104,** which looked like a branch in
  the normal game: 0x104 at location 15 when var 0xAE is 1 (set by 0x112, 1-A's rescue battle) and flag 0x2ED is 1 (set by ReMixed's
  0x03A with progress 0x22, chapter 3, only while 0xAE is 0: the two never hold together, "What's left of them" below);
  a battle with Ramza (roster slot 0) and Agrias made by 0xE0, whose
  conditions run 0x11B, which clears flag 0x2ED and hands on to 0x103 (var 0x3B), and so to the final battle: it
  belongs to the secret campaign too, which runs through ReMixed's own retelling of the main story (left out by
  choice). For any later start: the remaster's world conditions have "at least" (Type 2: fails when var < value;
  Type 3 is "at most").
- **Biggs and Wedge's start (2026-11-06):** ReMixed's is Gariland (location 6), progress 4 and flag 0x97, mode 2;
  its 0x009 sets the flag with progress 4 (the remaster's 0x01D sets the progress only) and 0x0A4 clears it, setting
  flag 0x92 (which vanilla's events first touch in chapter 2's 0x075). The mod's: progress 4, flag 0x92 = 0 and
  1-A not running. Seen on the PC: travelling to Gariland at progress 4 started 0x2FF; after it, flag 0x92 is 1.
  A Nexus report against 0.9.2 (2026-11-16) exposed two carrier differences: ReMixed's record uniquely used map-state
  `0xF0` for map 96, which the remaster rendered as black/sky, and its original world code committed location 6 while
  the remaster hand-off left the source location (usually Mandalia, 24) in var `0x31`. The generated carrier now uses
  state 0 and event 0x2FF writes location 6 before returning; otherwise its newly set flag 0x92 immediately unlocked
  and chained Mandalia's Luso start. Seen in the hidden PC runtime from location 24: the full tavern scene and dialogue
  rendered, returned with location 6 and flag 0x92 set, started no follow-on event, and logged no crash. The regression
  checker audits every imported map-state byte and this exact exit.
- **Other characters**: Biggs and Wedge (0x0A4: Gariland, progress 4, var 0x97, which ReMixed's 0x009 sets and the
  remaster's matching scene, 0x01D by story progress, doesn't) and Gogo (0x0C4) need 0xE0 or unmapped data; Luso
  (0x0EA, location 24, vars 0x92 and 0x93, progress 0xF or less) needs 0x2F; Balthier (0x098, Dorter, vars 0xB7 and
  0xB8; nothing in the events sets 0xB7) needs 0xE8. 0x091 starts at Lesalia (location 8) where the remaster starts
  its own Beowulf scene 0x1D4 (vanilla's start, less one flag), so shipping it would replace a scene of the
  remaster's: left out. 0x0A0 follows 0x09E's battle (Beowulf's chain). Both shipped 2026-11-12, with the arc
  before 0x0A0: "Beowulf's quest" below. 0x10F runs from ReMixed's main-story battle
  0x073's conditions; 0x11B from 0x104's; 0x0C4, 0x0C6, 0x107 and 0x10B have no start found yet.
- **What's left of them (read 2026-11-12, offline):** 0x2F and 0xE8 are ported, so by instructions 0x098 (shipped as
  0x369, waiting on flag 0xB7), 0x06E, 0x0A0, 0x104, 0x107, 0x10B, 0x11A and 0x0EB need nothing more
  (docs/generated/story-readiness.md). What holds the rest is where they start, as searched in ReMixed's world programs
  (WORLD.BIN, WLDCORE.BIN: `0019 event mode`), every event's hand-offs (0xCD 0x81-0x83, SET var 0x27) and its 512
  condition presets (`0019 event`):
  - **0x06E** (Eagrose, story progress 0x33, mode 2) is ReMixed's own turn of the story: after Dycedarg's change Zalbag
    lives, thanks Ramza and fakes his death to rally the Hokuten. The remaster starts its own event 433 at Eagrose at
    progress 51 (0x33), and its later chapters have Zalbag dead, so shipping it is a story decision, not a port step.
    Decided 2026-11-12 (ReMixed's) and shipped with its battle: "Zalbag at Eagrose" below.
  - **0x104** (location 15, flags 0xAE and 0x2ED, mode 1; a battle) and **0x11B** (from 0x104's conditions, preset
    0x103): 0x2ED is ReMixed's chapter-3 flag, which only its own retold main-story events set, and 0xAE is 0x112's
    "Hirame lives", which the port leaves 0 (it compares her unit's name id with ReMixed's 0x46). Both need a remaster
    point to stand for them; 0x11B also reads unit fields not mapped (+0x165-0x167, +0x16A). (Read again 2026-11-13,
    below: 0xAE is "Hirame lost", and ReMixed's start never holds.)
    Shipped 2026-11-12 (0x375, 0x376; CHAPTER_3_BRANCH): 0x112 compares with the remaster's 0x35, so 0xAE is set
    when Hirame is saved; 0x104 starts at location 15 on the story being at least 0x22 (EnhancedWorldEvent's type 2)
    with 0xAE set and 0x2ED clear, which 0x11B sets where it cleared it, so the branch runs once.
    **Read again 2026-11-13: that opened it when Hirame was lost (so in release 0.9.0).** ReMixed's start is location
    15's third entry (WORLD 0x80149A4C: `0001 00AE 0001`, `0001 02ED 0001`, `0019 0104 0001`; the list's other two
    are 0x019 at progress 0xD and the Algus campaign's 0x0FF at 0xA). Only 0x112 sets 0xAE: in its post-battle block,
    in the ELSE of flag 0x81 (set by its LABEL 04, Hirame revived), so on the way where she isn't revived (Lorne's
    "I'll keep you in my prayers", Bryn's "Rest in peace", Agrias's "I couldn't even do anything to save you"), when
    her unit is there with name id 0x46. Only 0x03A sets 0x2ED (`SET_VAR var 0, flag 0xAE; IF; ELSE; SET flag 0x2ED,
    1`: with 0xAE clear), and only 0x11B clears it. 1-A comes before chapter 3 (0x01A's offer; 0x002 can hand on to
    0x0EC at the opening), so 0xAE set means 0x2ED never is: ReMixed never plays 0x104. Nothing else writes either
    flag: no other event's variable ops or POKEs (into 0x80057931 or 0x80057979), no `li a0/a1` of them in WORLD,
    WLDCORE, BATTLE or the exe, no other world program; vanilla's and the remaster's events (all 1061 `.e` files) and
    the remaster's world rows don't name them. 0x11B's scene is for Hirame saved ("Thanks for saving me yet again,
    Dame Agrias"), and 0x0F0's rescue can be declined ("There's no saving her..."), which leaves 0xAE clear too. So
    the port opens the branch where Hirame is revived: 0x112's saved way sets 0x2ED (EVENT_CODE_INSERTS, after its
    `SET_VAR var 0, flag 0x81; IF; SET var 0x71, 0xB`), 0x104 starts on 0xAE clear (0x03A's rule; it also keeps out a
    save where 0.9.0 took the branch, which has 0xAE set) and 0x2ED set, the story at least 0x22, outside the Algus
    campaign, and 0x11B's own ZERO of 0x2ED (no longer patched) shuts it: once. 1-A played on 0.9.0 or earlier leaves
    0x2ED clear, so those saves don't get the branch. 0x11B's writes to
    the two units' AI targets (+0x165, +0x166, +0x16A: zeroed, so they stop holding their posts) go nowhere until
    the port maps those fields (Ps1Memory, in the EIU submodule). Not seen in the game yet. Fixed with Luso at
    location 24 (2026-11-12): the type-2 row carried only the value (0x22) in Unknown8, so the remaster's program read
    var 0x22 against the next row's type and then gave up on the location: the start never fired; its Unknown8 is
    [0x6E, 0x22] now. Fixed with Luso at Araguay
    Woods (RETURN_CHOSEN): 0x11B's victory returns to 0x104's conditions (event 8's vars 0, preset 0x103, 0xFFFF) on
    one path, which the build had sent to the world map, so its post-battle part (and 0x103) never came; it's chosen as
    it runs now. That part runs 0x11B again under its own number, which in 1-A showed portraits without lines (hence
    AFTER_SPLITS there): to check in the game.
  - **0x0A0** runs from 0x09E's battle conditions (preset 0x9D) and **0x107** from 0x01D's (preset 0x1C), **0x10F**
    from 0x073's (preset 0x72; it also needs ReMixed's opcode 0x15): events the remaster plays as its own. Each needs a
    hand-off put into the remaster's event at the matching point (as REMASTER_HAND_OFFS does elsewhere). (0x0A0 was
    another case: 0x09E is ReMixed's own, shipped with it; "Beowulf's quest" below. 0x10F rises inside ReMixed's own
    battle data, so its battle came over with it: "The temple's second wave" below.)
  - **0x11A** (Ovelia) comes after 0x0FC, ReMixed's telling of the Orbonne opening from Agrias's side, which the port
    doesn't run (the remaster's opening stays). **0x10B** and **0x11D** are run only by their own presets: no start.
    Read again 2026-11-12: 0x0FC is no retelling but side chapter 1-A's end, started at Orbonne once 1-A's last battle
    leaves the story at 0xE, and hands on to 0x11A: both shipped ("Side chapter 1-A's end at Orbonne" below). 0x10B is
    an empty event (its code an END alone; its record a battle on map 13, entry 0x135, formation 0x119, preset 0x10A)
    that nothing starts: no world statement names it (WORLD.BIN, WLDCORE.BIN, opcodes 0x19 and 0x1A), no event hands on
    to it (0xCD 0x81-0x83) or sets var 0x27 to it, no scenario record's word names it, the mission list doesn't, and
    its preset 0x10A, the only one that runs it, no event loads (ATTACK.OUT's own copy of the conditions has the same
    "var 0x1FD = 1 -> 0x10B" alone): nothing to port, as Izlude's Last Stand (0x0C8). 0x11D is reached: ReMixed's
    Preparation Menu (0x192) loads its preset 0x11C as a battle begins when a Nemesis is stored ("The Nemesis's
    return" below).
  - **0x0C4** (Mirror Match) runs from the presets 0xC3 and 0xC7 and reads the battle's copy of the party
    (0x8019FA00-0x8019FD80), not mapped. Read again 2026-11-12: 0xC3 is its own battle's conditions (its opening and
    its victory) and 0xC7 the empty Izlude's Last Stand's, whose victory names it; a mission, it starts from its
    posting. The "copy of the party" is vanilla's snapshot of the units' statuses and ReMixed's Max HP table, and its
    copy of the units is by raw offsets: shipped as 125 (`MIRROR_MATCH`; above, "Mirror Match"), not seen in the game
    yet. **0x0EA** (Luso) writes his unit record by raw offsets (his learned abilities, job level and JP; a
    chocobo's raw stats), mapped 2026-11-12 (Ps1Memory, the EIU submodule's b77b6bd); 0x0EB follows it. Shipped as 123 and 124: "Luso at
    location 24" below.
  - Free scenario numbers left below 0x377: 0x36A and 0x373-0x376 (five).
- **0xE0** (17 events): ReMixed's rewrite of vanilla's no-op (BATTLE 0x80154800, about 400 instructions) builds a
  40-byte battle-data record at 0x8015F880 from the instruction, the roster or another unit, then runs vanilla's unit
  creation into a battle slot. The remaster's unit creation (0x276758 and its setup) is where it would go.
  Read further (2026-11-06): it fills the record at 0x8015F880 (in the battle-data entries' 40-byte layout) one of
  three ways, picked by its flag byte (+11 of its parameters): from the parameters themselves (sprite set, flags,
  job, level, birthday, Brave and Faith, team and position at +0x18-0x1A), from another battle unit (the PS1's
  0x1C0-byte units at 0x801908CC: sprite set, flags, name, job, birthday, Brave, Faith, equipment, abilities, its
  position from +0x161-0x168), or from a roster slot (0x80057F74 + 0x100 × slot, found by the character's sprite set
  or unit id; a generic's sprite set by gender from +4). Then vanilla's unit creation from a record into battle slot
  s7 (0x8005A9B4), two steps on the new unit (0x8005E088, 0x8005DFD4), and fix-ups (+0x37, +0x161, +0x183, and the
  team bits by +11's 0x08/0x20). The remaster builds a battle unit from such a record with 0x275CA4 (its 0x2841D0
  does it for each 40-byte record in a list, then writes the unit to the roster with 0x274EB8), so the port is
  ReMixed's record building in C# (the PS1 unit and roster fields mapped as `RemixedScript`'s memory map does) and a
  call of 0x275CA4 into a free battle slot.
  Its parameters (read 2026-11-06; the interpreter hands its handlers p0-p3 in s2, s5, s6, s7, p0 | p1 << 8 in s3
  and a pointer to p0 in s1): p0 p1 a word (p0 the job, and for specials, below 0x4A, the sprite set and name too; or
  a roster or battle index; p1 gender and kind bits), p2 the new unit's id (+0x161), p3 the battle slot (21 or more
  becomes 0), p4 p5 the record's +8 +9, p6 its level (0: the slot's old unit's; the 0x40 path writes 100 over it),
  p7 its +0x25, p8-p10 the unit's x, y and facing (unless p11 & 4), and p11 the flags: 0x40 build from the
  parameters (all the shipped-to-be events' but two), 0x02 from roster slot p0 (0x104 and 0x11B: Ramza as unit 0x9A)
  or battle unit p0 (p1 & 1), neither: the roster slot with sprite set p0 (0x01: with unit id p0), 0x80 nothing
  built; then 0x08 keeps the slot's +0x37, 0x20 clears +2 and two of +6's bits, and 0xC0 skips the roster fix-ups
  (+9's high nibble, +0x16C, +0x15F by sprite set). The remaster's battle setup (0x27128C) makes each unit with the
  same three calls as the PS1's: 0x275CA4(unit, record, 0, 0, ctx), 0x278EDC(unit, &out, unit[3]) and
  0x278E0C(unit, 0) (the PS1's 0x8005A9B4, 0x8005E088, 0x8005DFD4); its battle units are 0x200 bytes from
  exe 0x1853CE0, its roster 600 bytes a slot from 0x11A7D10 (54 slots). The events with it: 0x0A4 (6), 0x0FD (4),
  0x0FF (2), 0x100 (4), 0x101 (3), 0x102 (3), 0x103 (14), 0x104 (2) and 0x11B (2), and ReMixed's retellings of the
  main story (0x004, 0x006, 0x019, 0x01C-0x01E), which aren't shipped.
  **Ported (2026-11-06), `Patches/CreateUnit.cs`:** ReMixed's record building as it stands, reading the PS1's unit
  and roster fields through `Ps1Memory`, then the unit made in its slot as the remaster's setup makes one: its +1
  and +0x1BC set to the slot, the record in the setup's current-record global (0x11A3868), 0x275CA4(unit, record,
  0, 0, 0), +0x1B5 (the PS1's +0x183) cleared and the unit id (+0x191) set, 0x278EDC's graphic into +0x18F and
  +0x190, 0x278E0C(unit, 0); var 0x70 at 0 meanwhile, as ReMixed does; then its place (merged into +0x51 as the
  remaster's own remaking, 0x30ADE4, does), +0x37, +2 and +6 by the flags, and for a roster unit +9, the name id and
  ReMixed's graphics for its sprite sets from 0x90; the name the slot had, or the roster unit's (roster +0xDC, 0x40
  bytes, to +0x14C). The remaster's 0x275CA4 skips a record that loads a formation (+1 & 8), where ReMixed's
  rewrite of the PS1's doesn't: those (flag 0x10) make nothing, noted. Seen on the PC: 0x0A4, Biggs and Wedge at
  Gariland, its six units made (Ramza's sprite set, which the scene's 0x58/0x7F change), shown by the event's
  add-unit instructions, named and speaking, to its end on the world map.

## The Algus campaign (read 2026-11-06; to port)

ReMixed's secret mode: name Ramza "Algus" or "Argath" and its opening (0x001) hands on to 0x0FD instead of 0x002.
The remaster's enhanced game doesn't let Ramza be renamed (its roster keeps names by id: slot 0's name field reads
empty), so the mod also starts it with a setting, "Argath campaign" (Configure Mod → Story, off by default): with it
on, the opening's hand-off (scenario 2's) goes to 0x300 as for the names (seen on the PC, 2026-11-06: the chapel
scene, then the campaign's first event).
Flag 0xAF marks the mode (0x0FD sets it; vanilla's events and the remaster's world rows never touch it). 0x0FD (map
118, TUNNEL) puts Algus in roster slot 0 (ReMixed's roster swap, 0x80160600), moves the real Ramza to slot 10 and
names him "Ramza" again (roster +0xBE), and hands on to 0x006. Then ReMixed's own chapter 1, with Algus's turns:

| Step | Event | What | Next |
|---|---|---|---|
| 1 | 0x0FD | the start, TUNNEL (entry 0x1C0) | 0x006 |
| 2 | 0x006 | a battle (Algus a guest, with Delita and the knights; its opening makes Ramza in slot 16) | 0x007 |
| 3 | 0x007 | progress 3 | Eagrose at 3: 0x008 |
| 4 | 0x008 | its flag 0xB2 branch (the script version: 0x105 is 0x009 in the PSP script) | 0x105 |
| 5 | 0x105 | progress 4 (entry 0x109) | location 26 at 4: 0x00A |
| 6 | 0x00A | a battle, progress 5 | location 9 at 5: 0x00B |
| 7 | 0x00B, 0x00C | a battle, then progress 6 | location 28 at 6: 0x00D |
| 8 | 0x00D, 0x00E | a battle, then progress 7 | Eagrose at 7: 0x00F |
| 9 | 0x00F | progress 8 | location 17 at 8: 0x010 |
| 10 | 0x010, 0x011, 0x012 | a battle, scenes, progress 9 | Eagrose at 9: 0x013 |
| 11 | 0x013 | its branch | 0x106 |
| 12 | 0x106, 0x0FE | progress 0xA (ReMixed's own story skips 0xA) | location 15 at 0xA: 0x0FF |
| 13 | 0x0FF, 0x100, 0x101, 0x102 | battles, each queuing the next (var 0x3C, through event 8); 0x102 gives the roster back | 0x103 |
| 14 | 0x103 | Zalbag and Algus | 0x07B |
| 15 | 0x07B | ReMixed's final battle (map 55: Alma holding back Ajora) | 0x07C |
| 16 | 0x07C | progress 0x63, the end: ReMixed goes on to 0x0A2 (its post-game rooms), the port to the world map | |

What the port needs (2026-11-06):

- **Battle data:** 19 distinct entries (0x106-0x113, 0x1C0-0x1C2, 0x17F, 0x180), none the remaster's own (ReMixed
  renumbered its story's); ENTD4 has 14 free slots left. The battle setup (0x27128C) reads a scenario's 16 records
  from the pointer it's given, so the mod can give it a scenario's units from its own file instead of a slot.
- **The trigger:** the remaster's opening hands on to the mod's 0x0FD when roster slot 0's name (+0xDC) is "Algus" or
  "Argath".
- **The mode:** ReMixed's world starts for these, each with "flag 0xAF = 1"; the remaster's chapter-1 starts wait
  with "flag 0xAF = 0" (as they wait for 1-A).
- **Maps:** TUNNEL (118) for 0x0FD, as VOLCANO.
- **Instructions and memory:** 0x0FD's name write (roster +0xBE-0xC3, ReMixed's charset); ReMixed's native
  0x80150440 (0x008, 0x07B: 0x80157E00, a unit's level up or down by N); the after-battle chain through var 0x3C
  (event 8's default: var 0x55 = var 0x3C, CallFunction 6); 0x80057EE0 (0x0FE, 0x102) and units' +0x99-0xAA (0x07B)
  unmapped; 0x100's 0x2F (passed over: its loop plays once).
- **The end:** read 2026-11-12, built 2026-11-15: 0x0A2 and 0x0A3 are The Lion War's Depths of Murond post-game hub,
  with its welcome, credits and Completion Points rooms, ending on the world map. They are shipped as 0x1B2 and
  0x1B4; the offline walk follows the ending through both rooms and their world-map exit. Seen in the game through
  the main hub, Spend CP, its named unit picker and a unit's upgrade menu; purchases and the real ending entrances
  and exits still need a pass.

**Progress (2026-11-06).** Built and partly seen on the PC:

- The trigger works once the hand-off is written in the per-event map-settings table (the remaster's 0xD3F950, which
  its world setup fills from the scenario records and its scenario end reads); setting scenario 2's record as it
  starts came too late. A Ramza named "Algus" went from the opening to the mod's 0x300 (0x0FD).
- TUNNEL: the remaster draws no built look on map 33 (neither TUNNEL's nor map 33's own); on map 98 (no remaster
  scenario or HD look, as 97, 99 and 101) it draws, its classic mesh cut to the file's 33,812 bytes without its moving
  parts' animations (`fitted_mesh`). 0x0FD's six 3D-object instructions (0x54) divided by a zero frame count
  (exe+0x24ED3B) on a built look; on those maps they become a one-frame wait (`without_map_objects`).
- 0x0FD is a battle: its opening ends at its END (the battle begins) and its second half (LABEL 01) runs when its
  condition set 1 fires: condition 6 (unit 1's HP at most 0, the remaster's 0x239294: 5 HP at least, 6 at most, 7
  and 8 HP as a percentage), Algus falling. With Algus defeated on the PC: Hokuten knights made by 0xE0, flag 0xAF
  set, Algus in roster slot 0, Ramza in slot 10 named "Ramza" (the PS1 name write), three knights joined, and 0x006
  (the mod's 0x301) started: its opening, then its battle as Algus (a guest, with Delita and the knights) on map 85.
- After 0x006's battle (its enemies defeated by the harness) the chain went on to the remaster's own scenarios 7 and 8.
  **Event 8 does two jobs:** its opening is Dycedarg's scene (the campaign's 0x008), and its block 0xFE is ReMixed's
  after-battle event (results and loot), which a battle's preset 7 runs as it ends (var 1, the condition set, skips the
  opening: `SET_VAR var 0, var 1; IF; D1 FE`). The build keeps a preset that runs an event the mod doesn't ship out
  of its battles and turns their end into a hand-off (location_scenario_code); once the campaign shipped 0x008 (as
  0x307), preset 7's "run event 8" was renumbered to 0x307 instead, so every ReMixed battle's end ran 0x307's
  after-battle block, which went on to the scenario its vars named as the remaster numbers it (0x006's 7: the
  remaster's own 7, then 8). Fixed (`AFTER_BATTLE_EVENT`): preset 7 is never renumbered. The Deck build of the
  same day had it too (every ReMixed battle's end), updated that evening.
- **0x008's branch** is on flag 0xB2, not 0xAF: ReMixed's Event Script Version (its 0x11C menu: 1 the PSP script,
  its default, 0 the PSX one). 0x105 is 0x009 in the PSP script ("Argath", "My own house was once respected as
  highly as House Beoulve"; 0x009's "Algus", "My family was once respected like the Beoulves"). The remaster never
  sets the flag, so all ReMixed's events ran the PSX script where they have both (107 read it, most to pick message
  n or n + a count), and 0x008 went to 0x009, which isn't shipped. The scenarios read 1 now (`SCRIPT_VERSION`).
- 0x007's last hand-off (`0xCD 81 91 01`, to ReMixed's random battles' setup 0x191) sits in its 0xF0 block, which its
  phase skip (`D1 FF`) passes over: 0x007 ends on the world map with progress 3. 0x102's other end, "I can't go on
  (End Chapter 1)" when Algus falls, hands on to ReMixed's 0x01A (its chapter-1 end, at Zeakden on map 49: "I'm
  sorry, Delita") with the roster given back: the remaster's 0x05A (its end of chapter 1, the explosion at Ziekden),
  with flag 0xAF cleared there, as the remaster's world starts wait for it to be 0 (built: `REMASTER_HAND_OFFS`
  and an insert after the hand-off; not tried in the game). 0x07C is ReMixed's
  ending (Ramza and Alma seen alive) and hands on to 0x0A2, its post-game hub ("Depths of Murond", Completion
  Points), now shipped as 0x1B2 and 0x1B4.
- **ReMixed's after-battle switches.** Event 8 zeroes var 0x3A (the after-battle mode) as it goes on; the mod's battle
  ends hand on straight to the next scenario, so var 0x3A stayed 0x81 after 0x006's battle. 0x007 opens with "var
  0x3A set: on to block 0xF0", its battle launcher (it fills the formation variables 0x32-0x36 from PS1 memory, which
  the remaster hasn't, and hands on to the random battles' setup 0x191): with the fix for event 8 in, 0x306 skipped
  its scene that way and the remaster's formation code (0x303408, called from 0x1EF0D8) crashed on the variables
  (exe+0x1EF889, seen on the PC). The battle ends now zero var 0x3A first, as event 8 does. 0x008 opens the same way
  on var 1 (set: on to its after-battle block 0xFE); from the world map var 1 holds whatever was left there, so
  started straight from it (no 0x300 before) 0x307 ran its after-battle block and the game closed twice without a
  crash report. The mod runs 0x008 only as Dycedarg's scene, so that jump is a no-op label now (EVENT_CODE_PATCHES).
- A unit 0xE0 makes in 0x006's opening (slot 16) had +1 at 0xFF once the battle was on.
- **Seen on the PC after the fixes (2026-11-06, evening):** 0x300's battle (Algus felled by the harness) on to its
  second half and 0x301; 0x301's opening (the PSP script's lines), its battle (its enemies felled), its closing, on to
  0x306 with var 0x3A zeroed, whose scene played whole (Argath meets Ramza and Delita on map 85), and back to the
  world map with progress 3. There var 1 held 0x82 (so 0x008's jump to its after-battle block would have been taken).
  0x307 started straight from the world map (the harness's start, as for 0x300) loaded no event: the battle on map
  11 began at once, Dycedarg and the others as guests. **Why:** a scenario's opening starts from its battle
  conditions, whose first set is "var 0x1FD = 1 → run the scenario's event" (the remaster sets var 0x1FD as a
  scenario starts). 0x008's record names preset 7, the after-battle preset, and with preset 7 left unrenumbered its
  "event 8" was the remaster's own: no opening, the battle at once. Preset 7 is renumbered again (0x307) and still
  counted unsafe, so battle ends keep loading the empty preset (`AFTER_BATTLE_EVENT`). Seen on the PC after it
  (started from the world map with flag 0xAF and progress 3): 0x307 loads its event and Dycedarg's scene plays whole
  in Eagrose castle (the PSP script: "Oh? You would not seem it.", Argath's "Lend me a hundred men"), its setup run
  again for its second half without restarting it, and hands on to 0x308 (0x105, not the PSX script's 0x009), whose
  scene (Argath, Ramza and Delita on the bridge, map 9) plays and returns to the world map with progress 4. Then
  0x309 (0x00A, started there): the remaster's formation screen (its records carry the formations the build reads
  from ReMixed's Preparation Menu variables), its opening (Delita and Argath in the woods, the PSP script), the battle
  (its enemies felled by the harness), its closing, and the world map with progress 5 and var 0x3A at 0. 0x30A
  (0x00B) started the same way reached its formation screen, but Space wouldn't commence it (keys delivered; for
  0x309 the same keys worked, with Ramza placed and locked there, and here he wasn't in the list at all): these tests
  started from an ordinary save, not the campaign's roster (Algus in slot 0, the knights), so 0x30A is to try from the
  campaign's own state. Read since (2026-11-12): each of the campaign's battles opens by marking Ramza (character 1)
  and Delita away in the roster (+0xD0 = 3) when flag 0xAF is set, as Algus leads and Ramza waits in slot 10. 0x309's
  test ran formation first (before the scene-first change), so its opening's mark came after Ramza was placed; it
  stayed, and 0x30A's formation had no Ramza and nobody placed. In the campaign's own roster Algus (slot 0) is there.

**Walked offline (2026-11-12).** The build now walks the campaign as built from 0x0FD to its end
(`check_algus_campaign`, its steps in `ALGUS_CHAIN`): each step's hand-off in the event's code, or the story progress
it leaves and the world-map start that runs the next, which must be at a place the party can reach by the roads the
world map has shown by then (the world-map table as built, read as the remaster runs it: a location's steps whose
checks hold when the party is there); its 11 battles' conditions (a set past the opening's that runs the battle again);
every hand-off of its events going to one of the mod's scenarios or a remaster scene that stands for one of ReMixed's;
the way out; the end. Against the 0.8.0 build it found four things that would have stopped the campaign:

- **No road from Mandalia.** 0x0FD puts the party at Mandalia (var 0x31, the party's world-map location: the remaster
  reads it at 0x2E9898(0x31); flag 0x218 shows the place) and the campaign's next start is Eagrose at progress 3. ReMixed
  shows the road from Mandalia to Eagrose and Eagrose at Mandalia at progress 3 (WLDCORE's list for location 24: `0001
  006E 0003, 0001 022D 0000, 0020 0018 0002`, then `0022 0002`), and at Eagrose at 4 the road back to Gariland and
  Gariland (`0020 0018 0006`, `0022 0006`) before the remaster's own steps there (Gariland on to Sweegy Woods and Dorter).
  The remaster shows all four at Gariland at progress 2, after its own battle there, which the campaign doesn't play.
  Now `ALGUS_WORLD_STEPS` adds ReMixed's four, each waiting for flag 0xAF. At 0xA ReMixed draws a road straight from
  Eagrose to location 15 (route flag 0x22C) that the remaster's map hasn't; the remaster's own steps at Eagrose at 0xA
  (to 15 by locations 29 and 25) run in the campaign as they are. The rest of the way, the remaster's steps at the same
  progress match ReMixed's.
- **The last battle had no conditions.** 0x102 loads preset 0x101, whose sets 2-9 are the main story's battle at Fort
  Zeakden: ReMixed's preset for that (0x18) runs its lines on Ramza's and Delita's turns (0x0A5); 0x101 runs 0x019
  there (Golagros's scene, which has no block for those set numbers) and 0x01A (the end of chapter 1, which would end
  the campaign without giving the roster back: set 9, Delita at half HP on Ramza's turn). Neither is shipped, and the
  build counted a preset with such a set of more than one record unsafe, so 0x102 loaded the empty preset: neither its
  victory (set 11, all enemies down: block 0b, on to 0x103) nor Algus falling (set 10: block 0a, give up or fight on)
  could fire. A set that runs an event the mod doesn't ship is now turned off by its first record whenever that's a
  comparison (`renumber_presets`); of the presets the shipped events load, only 0x101 changed. Whether ReMixed on the
  PS1 really plays the end of chapter 1 on set 9 is to see there.
- **0x103 never reached the final battle.** It hands on by `SET var 0x27, 0x07B` and `0xCD 82 00 00`. The remaster's
  scenario end (0x23987C) sets var 0x27 from the word alone (its scenario bits, 0) for every mode; 0x82 returns 0xB,
  game mode 0x55 (0x20871C, which waits on a dialog of its own before going on), and its own 20 scenarios with 0x82
  all name no scenario. Such a hand-off now goes on (0x81) to the scenario the event set var 0x27 to (`NEXT_BY_VAR_MODE`).
  ReMixed may ask to save there first; the port doesn't.
- **The ending went on to a scene of the remaster's.** 0x07C (ReMixed's ending) hands on to 0x0A2, The Lion War's
  Depths of Murond, in its code and its scenario record (+0x12 the scenario, +0x14 the mode). The remaster's own 0xA2
  is a chapter-2 battle scene (Mustadio in a swamp), so the port first sent both endings to the world map. Since
  2026-11-15, 0x0A2 and its Completion Points room 0x0A3 are shipped as 0x1B2 and 0x1B4. Their hand-offs, text,
  battle data, CP menus and world-map exit are built, and Reis's Morph writes her new job in place of the unported
  TransformUnit instruction. The chapter-3 branch reaches the same hub. Not seen in the game yet.

With those fixed the walk finds nothing wrong. Not seen in the game yet past 0x00A's battle.

**From a new game (the regression pass, 2026-11-12).** The setting's start ran on the PC: the chapel, 0x300 and its
second half, then 0x301's scene; as 0x301's battle began the game crashed (a divide by zero in the AI's turn timeline,
exe+0x38A844 in 0x38A468), twice. Its battle units had Algus (a guest), the three knights and the enemies, and an empty
unit 0 in Delita's place (the name guard: no name for id 0). Delita's battle data loads him from the formation (ENTD
flag 0x08), which in the remaster is a guest in the roster's slots 50-53 found by sprite set, left empty when there's
none; the earlier tests started from an ordinary save, which has Delita there. From a new game nothing in the campaign
saves him (0x006's data has no 0x01, and the remaster saves only a joining unit with it). ReMixed on the PS1 (named
Algus, 2026-11-12): its 0x006 battle has Delita made from his data, with no Delita in its roster. The same loads are
Delita in 0x007's scene and the battles 0x00A, 0x00B, 0x00D and 0x010, Algus in 0x013's and 0x106's scenes, and Alma
(0x30) in the final battle (0x07B, also the chapter-3 branch's). The build now clears flag 0x08 on those units in the
campaign's scenarios (`ALGUS_GUESTS_FROM_DATA`; they carry their units in the scenarios file, so no other scenario
shares them), and the walk checks that none is left. Not seen in the game yet. Two differences from the PS1 seen on
the way: there, 0x006's Algus is the player's own unit (from roster slot 0, his menu in battle), where the port makes
him a guest from his data; and the port's 0x300 scene shows black figures where the PS1 has none (and the wagon). Things the walk can't
show, to watch in a run: the formation screen with Algus in slot 0 and Ramza marked away; the three battles at
location 15 with two groups (2 and 4 units: six deployed, which no other battle does); 0x106's roster loop (every unit
but Ramza whose +0xCE isn't 0x12 is renamed 0x6C and dismissed as 0x6C: +0xCE is taken as the remaster's name id,
unconfirmed) and 0x102's way out (the same loop after the roster swap back); 0x0FE's and 0x102's clearing of
0x80057EEC-0x80057F33 (the errands' bytes on the PS1, unmapped here, so it goes to the upgrade's own store).

## Side chapter 1-A in order (2026-11-06)

Played on the PC from the end of chapter 1 with a driver over the harness (it answers a menu by finding the pointing
hand on a screenshot and pressing Down until the hand has moved that many rows), scenes only after the first battle:
a battle costs about six minutes (its setup, formation, opening and closing), so the rest wait for a quicker way.
Seen: 0x0EC (the offer, its scenes, Agrias and three Lionsguard joining at once), 0x0ED (Bryn and Lorne join),
0x0EE's battle, 0x0F0 (Agrias's rescue question: "There's no saving her..." skips its battle, as ReMixed lets it;
story progress 5), 0x0F2 on to 0x0F3 (progress 7), 0x0F9 (Gareth's ghost on map 101; progress 0xC), and 0x0F7's
formation on map 75. Earlier the same day: 0x0EF's battle, and 0x0F8 (VOLCANO) with 0x117.

**ReMixed's text code that sets a variable.** ReMixed's lines can carry F5 66 F6 a F5 b F6 v (F5 and F6 each take a
byte), which sets event variable 10a + b to v when the text gets there; the event waits for it with vanilla's 0x7E
(var, value) to time a gesture to the words. 0x0F9's "Come, join me in death" sets var 0x18 to 1 before Gareth
beckons; 0x0F4 does the same, 0x118 and 0x119 (the end, on to chapter 2) set var 1, and outside 1-A 0x01E and 0x098
(var 0x18), 0x06E, 0x100, 0x102 and 0x103 (var 0x70), 0x0C6 and 0x10F (var 0x53) use it. The remaster's text has
no such code: 0x0F9 stood still at its 0x7E with two boxes open, the codes showing as "241". The build drops the
codes (`TEXT_SYNC`) and turns each 0x7E on such a (variable, value) into 0xAE, setting it (`text_sync_waits`): the
gesture comes as the line opens rather than at its words. Waits on var 0x57 (0x0EC's three among them) work as they
are.

**ReMixed's two scripts (2026-11-06).** Flag 0xB2 (ReMixed's Event Script Version) picks the PSP script (1, its
default) or the PSX one (0) in the events that carry both; the remaster never set it, so the mod's ReMixed scenes showed
the PSX script where there was one. They read 1 now (`SCRIPT_VERSION` in tools/build_mod.py), the script nearest the
remaster's own. Its text needed three more characters: D1 17 the dash (93 in the shipped events, which showed as an
"N"), DA 75 a semicolon, D9 B8 a percent sign; and ReMixed's font has parentheses at 0x8D and 0x8E (vanilla's names
a hyphen at 0x8D): "(We wear the same vestments...)" had shown as "-We wear the same vestments...".

**Long lines.** Two of 0x0F9's lines ran past their boxes' bottom edge. Its boxes open with a short line (a MESSAGE)
and the event swaps the next ones in (0x51); the remaster seems to keep a box the size it opened with, and flows the
text in the box's width (Agrias's at the top right takes about 20 characters a line). 223 of the shipped events'
lines are longer than 120 characters (1 in 20 of the remaster's own speech lines, at most 203), up to 815. Not done.

**Map 75.** 0x0F7 is the only scenario on map 75 (no vanilla scenario uses it; the remaster's HD look of it is a
volcano with lava bubbles). Its battle loaded on the remaster's 75 (the formation seen, the deployment tiles on a
ledge), so ReMixed's reshaping (120 tiles: heights, water) wasn't in it. Chosen: ReMixed's. ReMixed changed ten of its
files, the default mesh (MAP075.9) and its time and weather variants (.12, .15, .18, .21, .26, .31, .36, .41, .46),
each the size of vanilla's; the remaster names them map_map075_<n>.bin by the disc's numbers (its identical files once
each). The build ships them in place of the remaster's and builds the look from ReMixed's texture and mesh
(`REMIXED_MAP_EDITS`), over the HD look's rows. The HD look's animations (RefinedBgAnim: its bubbles) and polygon
conditions (RefinedBgMeshPolygon) name its own parts and polygons, so they become the all-zero row a map without any
has; the units' lighting (RefinedBgSystemTexture, the lut_chara pictures) stays the remaster's. Seen on the PC: the
formation (its deployment tiles on the panels) and the opening in color, the lava floor, the rocks and the lavafall.

## Side chapter 1-A: its story progress (read 2026-11-05)

ReMixed runs the side chapter on the main story's progress variable (var 0x6E) from 0: its 0x0EC, when the player
takes the chapter, zeroes var 0x6E and sets var 0x6F to 1; its events then set var 0x6E to 2 through 0xC (0x0ED 2,
0x0EE 3, 0x0EF 4, 0x0F0 5, 0x0F1 6, 0x0F3 7, 0x0F4 8, 0x0F5 9, 0x116 0xA, 0x0F7 0xB, 0x0F9 0xC; the scenes inside its
battles, 0x111–0x119, repeat some of them) and var 0x6F to 2–4, and its last event (0x0FB) sets var 0x6E to 0xE,
where chapter 2 starts. Declining it (0x0EC's other path) goes on to 0x01B with var 0x6E = 0xE, var 0x6F = 5 and
flag 0x1C9 set. Its world-map starts (location 5 at 0 through location 9 at 0xD) check var 0x6E.

That clashes with the remaster: 13 of its own world-map starts check only var 0x6E == n, for n = 1 to 13 (location
6 at 1 → scenario 13, 24 at 2 → 15, 2 at 3 → 26, 26 at 4 → 30, 9 at 5 → 33, 28 at 6 → 39, 2 at 7 → 44, 17 at 8 →
46, 2 at 9 → 55, 24 at 10 → 59, 29 at 11 → 61, 25 at 12 → 68, 15 at 13 → 78), so with var 0x6E back at 0–0xD the
remaster's chapter-1 scenes would start again during the side chapter. So the port gives the side chapter a count of
its own: the build turns the side chapter's writes of var 0x6E (and var 0x6F) into writes of an event variable the
remaster doesn't use, and its world-map starts check that variable (and a "side chapter running" mark) instead;
var 0x6E keeps the remaster's value through the chapter, and 0x0FB's `SET var 0x6E, 0xE` ends it (the mark
cleared, var 0x6E to the remaster's start of chapter 2). Still to settle before building it:

- which variable is free in the remaster for the count and the mark (its own events and world rows name the ones it
  uses; `tools/exe.py refs` on the variable table, and a scan of its `.e` files and EnhancedWorldEvent);
- where the remaster's chapter 1 ends (its world start at location 15, progress 13, is scenario 78; ReMixed's offer
  is in its 0x01A) and how to send that end on to the mod's 0x0EC once, keeping the remaster's own scene;
- what var 0x6F means in the remaster (ReMixed uses it as a second counter here);
- the items, gil and units put aside (0x0EC's two calls of the roster swap, 0x80160600, ported) and given back;
- scenario numbers past 0x1FF for most of the 25 events (10 free ones are left below it), and its pre-battle menu
  (0x192, which most of its scenes hand on to with `0xCD 81 92 01`).

## Side chapter 1-A: its battles and its place (read 2026-11-05)

- **Its battles go through ReMixed's Preparation Menu.** Event 0x192 is a menu ReMixed added before its own battles
  ("Preparation Menu": Start Battle, View Map, Reveal Treasures, Extra Options, Save Game, Save Formation, Return to
  World Map, Change Music; its text). A 1-A battle's scenario (e.g. 0x0EE, started at location 40 on progress 2)
  first runs its opening (phase 0: the scene, its formations put in vars 0x34-0x36, and `0xCD 81 92 01`), then the
  menu, which starts the battle; the later phases run inside it, and the closing hands on to the after-battle event 8
  as the location battles do. The records carry no formations: the menu supplies them. The
  remaster's setup reads a record's formations into the same vars (0x35, 0x36) and shows its own formation screen,
  so the port drops the menu: the build puts the formations the opening sets outside any IF into the record
  (`opening_formations`; 0x0EE's 0x150, its second group 0x151 only under one of the menu's options) and drops the
  hand-off to 0x192 (only 0x0EE has one among the shipped events), so the battle goes on after the opening as a
  location battle's does. Their maps are in their records (+2, which the setup puts in var 0x33; 0x0EE's 0x58, 0x0EF's
  0x2C, 0x0F0's 0x56): only three of them (0x0F4, 0x0FB, 0x116) also set var 0x33 in their code. Of the menu's
  choices, Extra Options are the ReMixed tab of Settings and Reveal Treasures is asked as every battle's formation
  screen opens (GAMEPLAY_TODO.md, Done).
- **Formations:** 0x150-0x153 are empty in the remaster; 0x154-0x165 are its own. The build now moves a ReMixed
  formation that clashes to a number both leave empty (195 such from 0x100, taken from the table's end down).
- **The pilot:** 0x0EE ships as scenario 0x1FE (map 0x58, its battle data in slot 0x1E8, formation 0x150, preset
  0xED), started only by the test mode's autopilot for now. Built; not yet seen in the game (the PC test runs are on
  hold: the PC reset three times during them on 2026-11-05).
- **Numbers:** every one of 1-A's 25 event numbers is a scenario of the remaster's own (0x0EC-0x0FB, 0x111-0x119), so
  all of them move (10 free numbers are left below 0x200, then 0x2E8-0x376, untried).
- **Where the offer goes:** ReMixed renumbered the main story. At Ziekden (location 15, progress 0xD) vanilla and the
  remaster start scenario 0x4E, where ReMixed starts its 0x019, and Gariland's first scene is the remaster's 0x00D but
  ReMixed's 0x005. So ReMixed's 0x01A (the end of chapter 1 with the offer) matches a scene in the remaster's chain
  from 0x4E (its records 0x4E-0x58 hand on through var 100 + 1, then 0x58 → 0x59), which is where the offer and the
  hand-off to the mod's 0x0EC would go, once, keeping the remaster's scene.
- **Its progress, simpler than a counter of its own:** ReMixed's side events can keep var 0x6E as they are if the
  remaster's 13 chapter-1 world-map starts (above) each get one more condition, "the side chapter isn't running" (a
  flag the mod's 0x0EC sets when the player takes it and its 0x0FB clears), added to their EnhancedWorldEvent
  entries; 1-A's own starts check it the other way. The flag must be one neither game uses. ReMixed sets flags
  0x26C, 0x2EC, 0x3CE, 0x3CF and clears 0x270/0x283 when the chapter is taken, and 0x0FB sets 0x270 at its end, so
  0x270 may already be ReMixed's "taken and finished" mark.

## Side chapter 1-A: built (2026-11-06)

All 25 events ship (`SIDE_CHAPTER_A` in tools/build_mod.py). Seen on the PC (2026-11-06): the first battle (0x0EE as
0x1FE) from the world map through the formation, its opening (its scene's lines), the fight and its closing (Agrias's
line) back to the world map; it has two enemy teams (team bytes 0x90 and 0xA0). Its Agrias is battle unit 0x90, an
actor ReMixed's battle data gives sprite set and name 0x22 with no flags and job 0 (vanilla's 0x22 is Mustadio); the
remaster builds it as a 1-HP "Mustadio". ReMixed repurposed slot 0x22 (vanilla's and the remaster's Mustadio: sprite
set, name and job 0x22, "Engineer") for 1-A's young Agrias: its job 0x22 is a knight's (Basic Skill; swords, shield,
armour; no guns) and its sprite set 0x22 her look; 0x0EC's scene has her as sprite set 0x22 with Agrias's name (0x1E),
its battles as 0x22 with no flags and job 0. The mod had shipped ReMixed's job 0x22 changes, so the remaster's Mustadio
would have joined in chapter 2 as that knight. Fixed (2026-11-06): the job tables keep the remaster's row 0x22
(`REMASTER_KEPT_JOBS`) and 1-A's sprite-set-0x22 units become the remaster's Agrias (sprite set, name and job 0x1E, a
woman; `REMIXED_SPRITE_SETS`); seen in 0x0EC's scene with her own sprite and portrait. Her knight's job has no slot in
the remaster, so she's a Holy Knight from the start (1-A opens with her promotion to one). Also seen: 0x0ED (as 0x194,
progress 2) and the second battle 0x0EF (as 0x195; progress 4 after its closing), in one run from 0x0EC. After that
battle two recruits (name ids 105 and 106, Bryn and Lorne) each show twice in the roster: once with sprite sets 0xB5
and 0xB4 (slots 0 and 3), once as generics (slots 4 and 5); to look at.

- **Numbers:** 0x0EC → 0x193, 0x0ED → 0x194, 0x0EE → 0x1FE, 0x0EF-0x0F3 → 0x195-0x199, 0x0F4 → 0x1FF, and the rest
  from 0x2E8 where the remaster has no event file and no text (0x0F5-0x0FA → 0x2E8-0x2ED, 0x0FB → 0x2EF, 0x111-0x113 →
  0x2F0-0x2F2, 0x114-0x117 → 0x2F4-0x2F7, 0x118 → 0x2FD, 0x119 → 0x2FE). The remaster has a script at 0x192 and
  events of its own from 0x2EE up (no scenario records), and its Battle table stops at 0x2E7: the build adds rows past
  it, which nothing has tried yet (EntryNo's rows past its index weren't found; Battle may behave the same).
- **Starts:** its 16 world-map starts (`REMIXED_WORLD_STARTS`) each add "flag 0x2EC = 1"; ReMixed's mode 0 for
  0x0F6 becomes 1. The remaster's 13 chapter-1 starts (var 0x6E = 1-13) get "flag 0x2EC = 0" as their first
  instruction. The vanilla and the remaster's own events don't name flag 0x2EC; ReMixed's 0x0EC and 0x111 set it when
  the chapter is taken, and nothing clears it (after 1-A the progress is past 13, so neither set of starts fires).
- **Into it and out:** the remaster's end of chapter 1 (0x05A: the explosion at Ziekden; it sets var 0x6E to 14 and
  hands on to 0x074, chapter 2's first scenario, a script-less one; 0x075 sets 15) hands on to the mod's 0x193
  (ReMixed's 0x0EC) when the "Play Chapter 1-A" option is on (`RemixedScenarios.Offers`, the file's last section; the
  record's +0x12-0x14 hold the word: +0x12 + 0x400 × +0x13 | 0x100 × +0x14). 0x0EC asks "Continue to Chapter Two /
  Play Chapter 1-A" only with flag 0x20F set (no event script sets it; the PS1's code must, at a chapter's end), else
  it goes straight into the chapter. Made to ask, on the PC: ReMixed's box styles 0xE1/0xE3 (its menu-like boxes)
  crashed the remaster's box code (exe+0x136541; styles vanilla doesn't use now map onto vanilla's, `message_style`),
  then the question came up as one page of centred plain text (the offer, both answers and the next narration) with
  no way to choose, and Enter declined, which went on to 0x074 and the remaster's chapter 2 (0x075 ran). So the option
  is the choice and 0x0EC isn't made to ask. ReMixed's other in-text choices (e.g. 0x0EE's "1. See us as allies...")
  show the same way and go the way an answer of 0 does. Declining hands on to
  ReMixed's 0x01B, its chapter 2, which goes to the remaster's 0x074 (`REMASTER_HAND_OFFS`). Its two endings (0x0FB,
  and 0x119 inside the last battle) went back to the world map; they hand on to 0x074 and set var 0x6F to 5, as the
  remaster's 0x05A leaves it (vanilla counts story sections in var 0x6F; 1-A leaves it at 4). Since 2026-11-12 they go
  back to the world map again, as ReMixed's do: 1-A ends at Orbonne ("Side chapter 1-A's end at Orbonne" below).
- **Its battles:** the hand-offs to the Preparation Menu are dropped and the formations their openings set go in their
  records; their closings go through the after-battle hand-off as the location battles' do (the remaster's world map,
  or on to the next of 1-A's scenarios). Their maps are in their records (+2).
- **Two battles on another map than their scene (2026-11-07):** the menu sets a battle up from vars 0x32 and 0x33 (its
  battle entry and map), and two openings set them: 0x0F4 (the undead room: its scene on map 25 with entry 0x1D6,
  its battle on map 26 with entry 0x1DB) and 0x0FB (scene on map 31, battle on map 32, the same entry 0x1E0). With the
  menu dropped their battles had the scene's map and units, and 0x0F4's formation screen had no tile to deploy on (seen
  on the PC). Each now has a battle scenario of its own (`BATTLE_SPLITS`: 0x0F4's as 0x34A, 0x0FB's as 0x34B, where the
  remaster has no event file or text): the battle's map, entry and formations (0x0FB's two groups), the event's code
  with the menu's hand-off a jump to its end (ReMixed's own run of it from the menu ends there, var 0x18 = var 0x27),
  its own copy of the scene's conditions (ReMixed's preset 0xF3, 0xFA) with the events they run as the scene's run as
  the battle's, and the scene's lines in a text of its own. The scene keeps its record, without formations, and hands
  on to the battle's scenario in the menu's place; its answer that declines still goes back to the world map. A
  battle's opening starts through its conditions ("var 0x1FD = 1 → the event", which sets var 0x27 and loads that
  event): with the scene's preset, the battle's formation led into the scene again (var 0x27 back at 0x1FF), which
  handed on to the formation again (seen on the PC, from the scene and started directly alike).
- **ReMixed's two scripts out of step (2026-11-07):** 1-A's events carry both of ReMixed's translations as two blocks
  of n messages, in either order (0x0F6 has the PSX script first, 0x0F5, 0x0F9, 0x0FB and 0x119 the PSP one), and name
  a message by its number in the first block, plus n for the second (`ADD var 0x71, n` under a test of flag 0xB2,
  inline or in a FUNCTION the event CALLs; 0x115 counts in var 0x78); the port shows the PSP script (`SCRIPT_VERSION`).
  The box fitting (`fitted_boxes`) had taken var 0x71 as always raised, so where the PSP block comes first, and in
  0x115, it sized boxes and added later pages by the other block's lines; `message_numbers` now follows the code's
  tests and calls (seen on the PC: 0x0F9 shows its messages 1–32, 0x115 its 22–42). In five events the PSP block isn't one message
  for one: 0x0F4 runs Agrias's "We do but pass through..." and the knight's "Yet... we..." together (no message end
  before the knight's name), 0x0F8 the double's two lines and Agrias's into one and splits another, and 0x0EF, 0x114
  and 0x116 split a line in two. From there each MESSAGE showed the line after or before the one meant (seen on the PC
  in 0x0F4: its question whether to fight came on Balmafula's line, a line early, and the knight's thanks in Agrias's
  box). The build puts them back in step (`MESSAGE_FIXES`: the PSX and PSP lines now pair one for one). 0x0F7 has one
  message past both blocks (no shift); the Algus campaign's 0x00C has two fewer than its count, to check.
- **Back to the battle's events after event 8 (2026-11-07):** four of 1-A's battles end through event 8 and come back:
  the victory sets mode 0 (var 0x3A), var 0x3C to 0xFFFF and flag 0x8E, and puts a preset's number in var 0x3B, and
  event 8 (its case 0xFFFF) loads that preset, whose set "flag 0x8E = 1" runs the post-battle scene: 0x115 for 0x0F5's
  battle (preset 0xF2; 0x115 also jumps to its LABEL 10 itself with the flag set), 0x112 in 0x0F0's rescue (0xEF),
  0x114 (0xF0), and 0x119 in 0x0FB's (0xFA, the ending). The mod ships no event 8, so those battles went straight back
  to the world map without that scene and the story progress it sets (seen on the PC: 0x0F5's battle left the progress
  at 8, so 0x0F5 would have started again). Now the victory loads that preset itself (`AFTER_BATTLE_RETURN`; for 0x34B
  the battle's own copy of 0xFA), the word stays 0 so the battle goes on with its enemies down, and the scene's own
  hand-off to the world map clears flag 0x8E after it. Seen on the PC: 0x0F5's battle now ends in 0x115's post-battle
  scene and story progress 9, but that scene's boxes showed the speaker's portrait without the line (the game showed
  lines 30–37, the right ones, and 0x115's lines inside the battle had shown). Scenes the battle runs under another
  number show theirs after it (0x117, then 0x0F7's closing, after 0x0F8's battle), so the same event run again is what
  loses them: each of the four events' post-battle run is now an event of its own (`AFTER_SPLITS`: 0x115 as 0x34C,
  0x112 as 0x352, 0x114 as 0x353, 0x119 as 0x354; numbers with no event file or text of the remaster's), the same code
  and lines, which the "flag 0x8E = 1" sets run instead. Seen on the PC: 0x0F5's post-battle scene as event 844 shows
  its lines 30–37 in full, with the speakers' names, then story progress 9; 1-A's ending, 0x119's post-battle part as
  event 852 after 0x0FB's battle, shows its lines (its victory part, lines 16–18, in 0x119 itself before it).
- **Its optional battles:** its battles' openings ask whether to fight (the answer in var 0x18, 0 the first): 0x0F0
  ("There's no saving her..." skips the rescue, story progress 5), 0x0F4 ("Sorry, I'll have to pass", progress 8),
  0x0F5 ("Let us leave them be"), 0x0FA ("Very well": on to 0x118's scene without the battle) and 0x0FB ("No, forgive
  me" ends 1-A without its last battle, on to chapter 2). The second answer fights.

## Side chapter 1-A: the scene first, then the battle (2026-11-12)

Played on the PS1 (0x0F0, from a won battle with event 8's hand-off pointed at it; the PSP script, flag 0xB2 set) and
on the PC. ReMixed's order is the scene on the battle's map (0x0F0: Hirame climbing, the birds, her fall, Agrias's
question), then the formation ("Commencing Attack. Are you sure?"), the map's name card, "Conditions for Winning" and
the Preparation Menu over the battle map, then the battle; the menu's Start Battle sets the battle up again (its units
made afresh) and runs the scenario's event once more, which skips its scene. The port had the formation first and
played the scene inside the battle, which left the scene's actors on the field (Agrias's actor, battle id 0x90, fought
0x0F0's rescue as a guest; on the PS1 she isn't deployed). Four things found and fixed:

- **The order** (`MENU_BATTLES` in tools/build_mod.py, `RemixedScenarios.MenuBattles`): ReMixed's own design, without a
  scenario number of its own for each battle (only six free numbers are left below 0x377, the per-event table's
  size). The scene's record has no formations, so it runs as a scene; after its note of itself (`SET_VAR var 0x3E, var
  0x27`, which only the scene's run reaches) it hands on to its own scenario; that second run's setup gets the battle's
  formations in the record and var 0x3A = var 0x3E, as the menu sets it, so the event's opening finds var 0x18 = var
  0x27 and skips to its end, and the battle begins. ReMixed's hand-off comes before its check, so the battle's run
  had handed on again at its end (the scene played a third time, seen on the PC): it stays dropped there, the new one
  after the note. The battle's run fades in (0x4D 1E) as the menu does before its GOTO ff: without it the battle's
  later scenes played on a black screen. 0x0EE, 0x0EF, 0x0F0, 0x0F1 (and 0x113), 0x0F5, 0x0F7, 0x0FA and 0x116; 0x0F4
  and 0x0FB have their own battle scenarios already (their battles are on other maps). The Algus campaign's battles
  with the same hand-off and check, all on their scene's map: 0x00A, 0x00B, 0x010 and 0x102 are in too (not seen
  yet); 0x00D and 0x0FF-0x101 have two groups (below), so they keep the formation first for now; 0x104, the same,
  isn't shipped.
- **A scene left without its battle:** the scene's note of itself (var 0x3E) stayed when its question was answered no,
  and the next start of the scenario from the world map went to its battle without the scene (seen on the PC, 0x0F0).
  On the PS1 the note counts only once the menu has set var 0x3A from it. Now the world map clears a note naming one
  of these events (`RemixedScenarios.ClearStaleNote`): a scene's hand-off reaches its battle's run without the world map
  in between.
- **Second groups:** 1-A's battles set their formation's second group under an IF: 0x0EE with flag 0xAD (Bryn and
  Lorne along) and var 0x6C 0, the others with var 0x6C 0. var 0x6C holds ReMixed's Extra Options bits, 0 as a new
  game has them (0 in every PS1 state here) and in the port, which has its own options; the build read only the sets
  outside IFs, so the port deployed one group where ReMixed deploys two (seen on the PS1: 0x0F1's formation, Squad 1
  of 4 and Squad 2 of 2). The build ships that group with the battle, marked (`MENU_SECOND_GROUP`), and the code mod
  takes it when the scene's run left var 0x36 set, as ReMixed's menu reads it (`RemixedScenarios.SecondGroup`;
  self-tested offline). It was off at first: with two groups put in for the battle's run the remaster's formation
  screen showed both (Group 1 of 4, Group 2 of 2) and took units in both, but Space never commenced (0x0F0's, seen on
  the PC 2026-11-12). Found in the exe (2026-11-18): the remaster takes five places at most. As its formation opens
  (0x1EF0D8) it counts both groups' places into the formation's +0x40, its list of units placed (+0x44) holds five,
  and Start asks "Commence battle?" only for 1 to 5 places (0x1EE890); with six it does nothing. Every two-group
  battle in vanilla's and ReMixed's records has five or fewer; ReMixed's own events set six (4 + 2 in 1-A's
  battles, 2 + 4 in the Argath campaign's 0x0FF-0x101), which the PS1 deploys, its party past five in the battle's
  last slots. Since 2026-11-19 the code mod's "six-places" (`Patches/SixPlaces.cs`) lets the remaster take six
  (a sixth list entry, the limits six, a sixth unit in a free battle slot from 15 down) and the marked groups are on
  (`SecondGroupsOn`). 0x0F4's and 0x0F8's battles, with their formation first, get their second group in their
  records, as their code sets it in the port (only var 0x6C's ELSE around it; `second_group_conditions`), where the
  build had left it out. `tools/check_story_regressions.py` checks that no battle has more than six places, and the
  build stops on a second group it would leave out. vars 0x34-0x36 aren't ReMixed's alone: the remaster's setup
  writes the record's group count and formations there (0x34 = 2, 0x35 and 0x36 the formations, seen).
- **ReMixed's own battle conditions** (analysis/remixed-event-opcodes.md, "ReMixed's battle-condition types"): 0x0F0's
  in-battle scenes wait on Hirame's status (type 0x15: no longer Dead, so revived; 0x14: Crystal or Treasure, too late),
  which the remaster's checker doesn't have, so neither ever fired and the post-battle scene always took its "too
  late" branch. The mod's walker checks ReMixed's types now.
- **Unit presets** (MECHANICS.md, "Unit presets"): Hirame starts the battle Dead from her preset (15), which the
  port didn't have; now it does (HP stays full where the PS1 has 0; to look at).
- **ReMixed's 0x22** is side chapter 1-A's young Agrias, which the port draws as the remaster's Agrias (0x1E). Its
  battles' conditions name the player's Agrias as 0x22 (0x0F1's dialogue on her turn, 0x0F5's failure when she falls,
  0x0EE's and 0x0F4's sets on her HP), which the remaster finds by battle id, then by sprite set for a deployed special
  character (0x239144 → 0x272538): never, with her sprite set 0x1E. The presets name her in types 0xB (0xF0: the
  acting unit, 0x0F1's dialogue), 6 (0xF4: HP at most 0, 0x0F5's failure) and 5 (0xED, 0xF3: HP at least 1). Now the
  mod's walker, in ReMixed's scenarios, takes an id no unit has from `RemixedScenarios.UnitAliases` (0x22 → 0x1E) as
  the formation's unit of that sprite set (on the field, battle id 0x78–0x7C: not the scene's actor of the same
  sprite set, battle id 0x90): ReMixed's own types look it up so (`RemixedUnit`), and types 4–8 and 0xB go to the
  game's checker with that unit's battle id, which its lookup finds first and type 0xB compares with the acting
  unit's first (`GameUnitId`). Self-tested offline; to see in 0x0F1's and 0x0F5's battles. A first try (the same
  day) hooked the sprite-set lookup (0x272538) with a C# detour instead; it was taken out: that lookup writes only
  rax, rdx and r8–r10 and the game's callers count on it (0x2387C4 keeps a loop counter in r11 across it), it answers
  −4 or −5, not −1, for an id no deployed unit has, and the actor would have been found first.
- **A crash on the battle's first turn** (exe+0x317386, the move range read with no acting unit: 0x1872EA0 null;
  three runs of 0x0F0's battle, never before the fade-in above went in): the battle's run ended right after its
  fade-in, so the battle began mid-fade. ReMixed's menu waits on the player after it, so its fade is long over; the
  battle's run now waits it out (0xF1, 30 frames) before its GOTO ff. Seen on the PC: no crash since, and the battle's
  later scenes on the map, not on black.

Seen on the PC (2026-11-12): 0x0F0 from the world map through its scene and question ("We must speed to her rescue!")
to the formation after it, the battle with the birds, Hirame down at the foot of the cliff and no Agrias actor; Hirame
revived (her Dead status cleared by hand) ran 0x112's in-battle scene ("U-ugh... What... what has transpired?" ...
"Pray, remain still. We shall speak further when these foul beasts are vanquished!"), and after the birds 0x112's
post-battle scene took the branch where she lives (her harness, the Order of the Southern Sky, "Dead Men"), as on the
PS1, on to the world map with story progress 5. Hirame herself shows a generic squire's sprite and portrait, as her
own aren't in the remaster (the art port). Seen again after the fixes (the fade-in's wait, the stale note): the scene,
the question answered to fight, the formation, the battle's first turns, Hirame revived and 0x112's lines on the
battle map (no longer black), the victory and 0x112's scene after it, line for line with the PS1, the world map at
progress 5. Differences left: ReMixed's results screens (Congratulations, the bonus money and war trophies) aren't
shown; in the in-battle scene Agrias's boxes have no portrait (her unit isn't on the field; the PS1 shows it); and
0x112 sets flag 0xAE only when Hirame's unit has ReMixed's name id 0x46 (it reads unit +0x16C), while the port gave
her the remaster's blank 0x35, so the flag stayed 0. Only 0x104 (then not shipped) read it. Since 2026-11-12 it
compares with the port's id; since 2026-11-15 that is her named CharaName row 0x36. 0xAE is set on the way where
Hirame isn't revived, "Hirame lost": "What's left of them", 0x104.

**0x0F1 on the PS1 (2026-11-12),** for the comparison: event 8's hand-off pointed at 0xF1 with flag 0xB2 1 went on to
0x0F2's scene at once (0x0F1's code never loaded; its start is the world map's, Grog Hill at progress 5); with flag
0xB2 0 it ran 0x113's code with the PSX script's lines (the port shows the PSP script's, as everywhere): the knight
and Bryn and Lorne's past, four questions (each "Very well." gives them up: no battle, on to the next scene; the
second answer each time leads on to "They're in on it!!"), then the formation with two squads, Grog Hill's name card,
the Preparation Menu, and the battle. On the young Agrias's first turn 0x114's lines ("You honorless dog! Serving
smugglers?" ... "This is your fault now!"), on the victory her two ("And now your kids have no one." ...), the
results, and 0x114's scene among the crates (Bryn and Lorne: "So... what now?" ... "you'll be granted leniency,
rather than prison time."), then the world map. In that battle the young Agrias (sprite set 0x22, battle id 0x79) is
a roster unit, and the battle data has a unit of sprite set 0x1E as well (battle id 0x7B).

**0x0F1 on the PC (2026-11-12),** after 1-A's start (0x111) and 0x0ED (its question answered to help: Bryn and Lorne
join): 0x197 plays 0x113's scene (the PSP script) on Grog Hill's map, its four questions answered with the second
answers, the battle set up after it (one group, formation 0x2FD; Agrias the formation's unit 16, battle id 0x78, the
scene's actor unit 11, battle id 0x90, Bryn and Lorne guests), the enemies felled, then 0x114 run at the battle's end
and its scene among the crates (as 0x353, line for line with the PS1's, in the PSP script). Not seen: 0x114's lines
on Agrias's turn (preset 0xF0's set 3: flag 0x80 0, "the acting unit is 0x22", unit 0x80's HP at least 1) never came
in five minutes of the battle, with Agrias acting, and so neither did her two at the victory (set 4 needs flag 0x80,
which the first part sets). Most likely the test mode's doing (read in the exe afterwards, no game runs left): it
hands the player's units to the AI (TestRecorder.AutoBattle clears their control bit 0x08, so Agrias showed as a
guest), and the remaster checks a battle's conditions at a unit's turn start (0x20261C → 0x2387C4 with 8, the acting
unit's index into 0x1407DC494, then the walk) and runs what fires on a turn the player plays (the turn mode 0x100, or
0xFF00); on an AI-played one (0x200, 0x300) it only asks whether a set would fire, runs that event only when its
map-settings word ([0xD3F950 + 2 × event]) has a bit of 0xF300, and otherwise calls with 1, which skips the walk. So
"the acting unit is" likely never runs its event on a turn the test mode plays; the alias itself is self-tested. To
see with Agrias played by hand (regression.md). Seen also: the port draws Grog Hill
at night where the PS1 has dusk; Bryn and Lorne show as dark silhouettes (ReMixed's sprite sets 0xB5 and 0xB4, the
art port). A run that started 0x197 right as 0x0ED ended played the scene on black; started from the world map, it
shows the map.

## Zalbag at Eagrose (2026-11-12)

The user's call (2026-11-12): ReMixed's storyline over the remaster's ("everything on this mod needs to be as close to
remixed as possible"), the whole sequence ReMixed's, with its staging and its text (the PSP script), without the
remaster's voices for it.

- **The remaster's** (vanilla's events, numbered as vanilla's): its world row at Eagrose (location 2, entry 16: flag
  0xAF 0, story 51) starts 433, which goes on through Ramza's arrival (434: "No guards", "Zalbaag's chocobo") to the
  battle on map 10 (435, entry 0x1CB) and its scenes: 436 Zalbaag confronts Dycedarg and the guards turn on him, 437 he
  begs Ramza's pardon, 438 the brothers' quarrel, 439 Dycedarg falls, takes the Stone's offer and as Adramelk strikes
  Zalbaag down, 440 Ramza alone ("House Beoulve has fallen"). 440 sets the story to 0x34, flags 0x269 and 0x34C, and
  the Persons states 0x3C4 = 2 (Zalbag dead), 0x3C5 = 4, 0x3F0 = 2.
- **ReMixed's**: it empties 0x1B1-0x1B8 and starts its 0x06E there (map 1, entry 0x172). Its story check (var 0x6E
  against 0x34) picks the arrival (label 00: the same three lines), which hands on to 0x06F (CD 81 6F; map 10, entry
  0x173, preset 0x6E: the confrontation, then the battle). As Dycedarg falls, that preset's conditions run 0x10E: the
  change, then Adramelk's spell on Zalbag. Whether it kills him is the battle's puzzle: 0x10E reads Zalbag's statuses
  (UNIT_ADDRESS 8, PEEK +0x5B, 2 bytes, & 0x4210: Shell, Reflect, Innocent) and HAS_CHARACTER 0xC0 8 (one of the
  player's units: the roster, or ReMixed's own list of units away on its dispatch missions, PS1 0x800579C8 and
  0x80040EAC, which only its code uses). With either he lives (Adramelk: "I forgot about your protection", or a line
  for a stranger kind of protection), without them he dies as in the remaster; then Adramelk's own fall, "I must save
  Alma!". Its end sets what 440 sets, with 0x3C4 = 6 (lives) or 2 (dead), the story to 0x34,
  and event 8's vars: with Zalbag alive var 0x3A 0x81 and var 0x3B 0x6E (on to 0x06E), else 0x80 (the world map).
  0x06E again, past 0x34, plays label 01: Zalbag thanks Ramza, offers to fight at his side, and goes to fake his
  death and rally the Order in secret; 0x3C4 = 6 or 7 (one of the player's units or not); back to the world map.
  Each message has two translations (flag 0xB2: message n or n + 13), as in 1-A.
- **In the port**: 0x06E as 0x36A, 0x06F as 0x373, 0x10E as 0x374 (ZALBAG_AT_EAGROSE); the remaster's start at
  Eagrose runs 0x36A instead of 433 (REMASTER_STARTS_REPLACED: its conditions stay); 0x10E's end hands on to 0x36A
  itself when var 0x3A is 0x81 as it runs, else to the world map (location_scenario_code: preset 7 swapped for the
  empty one, flag 0x8E left 0; the choice, AFTER_BATTLE_CHOSEN, because event 8's vars are set under an IF there).
  HAS_CHARACTER's dispatch list isn't mapped (the port's dispatch tavern keeps its groups elsewhere) and nothing in the
  port recruits Zalbag, so he lives by Shell, Reflect or Innocent. No vanilla event reads 0x3C4 (the Persons list
  does), so the remaster's later chapters go on as they would; its entry may show an earlier page for 6 and 7. Not
  seen in the game yet.
- **What stays the remaster's**: the scene before (432, Zalbag and the chemist at Balbanes's tomb: ReMixed's 0x06D,
  the same scene) and the temple's undead Zalbag (vanilla's 0x1C4 on: ReMixed's 0x073, the same scenes; it reads
  0x3C4 only to pick the side scene it queues after its battle, the Steel Giant at Goug, 0x08A, with Zalbag dead at
  Eagrose, or the empty 0x088 with him alive, and the port's Steel Giant scenes are the remaster's own).

## Beowulf's quest (2026-11-12)

The user's rule (2026-11-12): ReMixed's story where it differs, the remaster's scenes and voices where it's the same.

- **The two side by side.** ReMixed retells the remaster's quest event for event, the remaster's numbers being
  vanilla's with each battle's in-battle scenes as events of their own: the tavern (ReMixed's 0x090, map 96; the
  remaster's 464-467, started at location 0 on flags 0x99, 0xA6, not 0xA7, where ReMixed's start checks 0x94 for
  0x99), the colliery's four battles (0x091-0x094 on maps 30, 29, 28 and 39; the remaster's 468-470, 471-473, 474-476
  and 477-481, the first started at location 8 on flags 0xA7, 0xA8, not 0xA9 and character 0x1F in the party, where
  ReMixed's leaves 0xA8 out), the end at the bottom ("Thank you", 0x095; 482-483: flag 0xA9), the "Warning!" battle
  (0x096 on map 70; 484-487, location 23: flag 0xAB) and Cancer restoring Reis (0x097; 488-489: flag 0xAC). The
  quest's flags are the same at the same points. ReMixed's battles queue the next with event 8's vars (0x091: var
  0x3C 0x92).
- **What ReMixed changes.** Its first colliery battle (0x091, map 30, entry 0x18A) puts dragon hunters there, who
  claim the hunt and then know Ramza for a heretic ("Everyone! Get them!"). And after Reis is restored it adds an arc
  of its own: 0x09C (map 115, started at location 3 with Beowulf 0x1F and Reis 0x0F in the party, flag 0xAB set and
  0xB3 clear; ReMixed's start also wants its PSP script, flag 0xB2): Aliste ambushes them for the bounty Bremondt
  has put on Reis, and they set out for Lionel; it queues 0x09D through ReMixed's world map (var 0x55 = 0x9D, 0x43 06,
  read by its own world code, WORLD 0x80068590), the gate (a battle, map 12), which queues 0x09E (Bremondt's castle,
  a battle, map 13), whose end goes on to 0x0A0 (map 11, Beowulf and Reis after it; flag 0xB3 set).
- **In the port** (BEOWULF_QUEST): the remaster's tavern, its other three colliery battles, the bottom, "Warning!"
  and Cancer stay. 0x091 is scenario 468 (0x1D4), so the remaster's start at location 8 runs it; its end goes on to
  the remaster's 471 (REMASTER_HAND_OFFS 0x92 -> 0x1D7). 0x09C (469), 0x09D (470), 0x09E (433) and 0x0A0 (435);
  0x09C's queue becomes a hand-off to 0x09D, with var 0x55 left 0 (WORLD_QUEUE_VAR); its start is a row at location 3
  with the party checks (type 4) and the flags, without the script flag.
- **Numbers.** The free numbers below 0x377 ran out with the chapter-3 branch, so these take the remaster's whose
  events can't run any more (REUSED_SCENARIO_NUMBERS): 433 and 435 (behind Zalbag at Eagrose's start) and 468-470
  (the first colliery battle's). None has text of the remaster's (the build checks the ScenarioId rows); each one's
  EventId row, which gave 433 and 435 a sortie prompt and 468 the colliery's run-of-battles prompt, becomes a free
  number's (eventid.nxd). Left for later events: 434, 436-440 (each with text of the remaster's to take off first)
  and whatever later replacements leave behind.
- Not seen in the game yet.

## The temple's second wave (2026-11-12)

- **The remaster's**: its battle at map 3 ("So, you're Ramza...", 448-450) leaves 451 in var 0x55 for the world map
  (vanilla's 0x43 06; the remaster's own 0xE9 with the scene's text key 1820). 451 (map 52, entry 0x1CE, formation
  0x146, no text) is the temple battle; its conditions run 452-455 (voiced: Vormav's "No more time to waste", the
  coffins, Zalbag undead, his plea to be killed), and 455's record goes on to 456 (map 51, no text), then 457 (the
  dying Funeral, "...somebody...").
- **ReMixed's**: its battle at map 3 (0x072) queues 0x073 the same way (event 8's var 0x3C). 0x073 (map 52, entry
  0x177, formation 0x146, preset 0x72) is the same battle and scenes in one event, but its preset also runs 0x10F as
  Zalbag falls (eight sets): Vormav's voice ("No, you're not done yet. Get up, idiot. Kill Ramza") raises Algus
  undead from ReMixed's battle data, "I have to fight you a third time?". Its names show "???" first (0x073 writes
  them into units 0x8A and 0x8B's +0x12C). Both 0x073 and 0x10F end with event 8's vars on to 0x074 (the Funeral,
  map 51: the remaster's 456-457). 0x073 also reads Zalbag's Persons state (0x3C4) to pick the side scene it queues
  for later (var 0x3C), which its end then clears.
- **In the port** (TEMPLE_SECOND_WAVE): 0x073 is 451, so the remaster's map-3 battle queues it; 0x10F is 452 (its
  conditions renumbered); their ends go on to the remaster's 456 (REMASTER_HAND_OFFS 0x74). The remaster's 452-455
  can't run any more: their voiced scenes go, ReMixed's text in their place. 452's text row (1830) comes off
  (drop_reused_texts, now that a reused number may have one), and both EventId rows become a free number's.
  0x10F's 0x15 (BATTLE.BIN 0x80158D6C: each battle unit's 0x2B bytes from +0x18C cleared) is the one instruction not
  ported; the interpreter (the EIU submodule's) passes over it, so the units keep that state into the second wave.
- Not seen in the game yet.

## Luso at Araguay Woods (2026-11-12)

- **The two side by side.** The remaster's Araguay Woods is 121 (map 80, entry 0x194, formation 0x10D: the battle,
  started at location 32 on story 0x10) with its scenes 122-127 (voiced), the last setting story 0x11 and flag 0x287;
  location 35 then starts 128 on 0x11. ReMixed's 0x01D (map 80, entry 0x11E) starts at the same place on the same
  story, plays its scene first (the goblins, the chocobo, and Luso on it), then the battle through its Preparation
  Menu (formation 0x10D, one group: MENU_BATTLES), and sets the same at the end.
- **Luso's flag.** ReMixed meets Luso first at location 24 (0x0EA-0x0EB, on flag 0x92, 0x93 clear and story 0xF or
  less; 0x0EB sets 0x93). At Araguay's victory 0x01D checks 0x93: set, it returns to its conditions (event 8's vars
  0, preset 0x1C, 0xFFFF, flag 0x8E) and preset 0x1C's "victory, flag 0x8E = 1" set runs 0x107, Luso's thanks ("It's
  been a couple weeks... It's been over a year"), which clears 0x93 again; clear, it sets 0x93 and goes back to the
  world map. Both paths come before the same end (story 0x11, flag 0x287).
- **In the port** (ARAGUAY_LUSO): 0x01D is 121 (the remaster's start runs it), 0x107 is 122 (its text row 470 comes
  off). The victory's two paths are chosen as it runs (RETURN_CHOSEN: var 0x3C 0xFFFF loads preset 0x1C and keeps the
  word 0, else the world map), and 0x107 clears flag 0x8E at its world-map hand-off (returned_to now takes a set with
  "flag 0x8E = 1" among other conditions). 0x0EA-0x0EB shipped later the same day ("Luso at location 24" below), so
  0x107 plays when Luso was met at Mandalia in chapter 1; Luso is ReMixed's slot 0x12 with a name row of his own.
  ReMixed's chapter-2 battles (0x01C-0x01E) also set Agrias's and Gafgarion's errand byte to 3 (roster +0xD0) until
  its 0x025 clears it; the remaster's battles around Araguay don't, so 0x01D's OR is 0 (EVENT_CODE_PATCHES). Its flags
  0xC0-0xC9 and 0x1FF are ReMixed's own (vanilla's events and the remaster's world rows never use them). The
  remaster's voiced 122-127 go with it.
- Not seen in the game yet.

## Side chapter 1-A's end at Orbonne (2026-11-12)

Read offline (ReMixed's events, world statements and records), shipped not seen in the game yet.

- **How ReMixed gets there.** 1-A's last battle (0x0FB, or 0x119 inside it) sets the story to 0xE and goes back to
  the world map (`0xCD 80 00 00`; 0x0FB's "No, forgive me", which skips the battle, the same). 0x0EC had hidden chapter
  1's places (flags 0x200 + a place cleared) and shown Zeltennia (5); 1-A's own world statements show its way at four
  points, the last at location 27 with the story at 0xB, as 0x0F9 starts: the camera to Orbonne, the routes 27-28 and
  28-9 with Sand Rat's Cellar (28) and Dorter (9) shown again, then the route 9-18 and Orbonne (18). At Orbonne with
  the story at 0xE its statement starts 0x0FC (mode 2: straight in). Nothing else starts it: no name check (ReMixed's
  0x001 checks Ramza's name for "Algus" or "Argath", the Algus campaign, and for "Agrias", which only draws Ramza as
  her, flag 0x8A), no hand-off, no preset an event loads.
- **0x0FC** (map 56, Orbonne's grounds; battle entry 0x100: Ovelia 0x0C, Simon 0x13, Agrias 0x1E, Ramza 0x02,
  Gafgarion 0x17, Delita 0x05 and the Lionsguard knights; preset 0xFB): Agrias, Lavian, Alicia and Whitney come to
  guard Ovelia and meet Father Simon; "Months later..."; the opening's day from their side: Gafgarion tired of waiting,
  Whitney wounded by soldiers in the woods. Before the scene, outside a replay (flag 0x1FC, the replay screen's mark),
  it gives Ramza his roster slot back (ReMixed's roster swap with Agrias's, his errand byte cleared) and, while var
  0x6C is 0 (ReMixed's Extra Options bits, 0 in the port), dismisses Whitney (DismissUnit, 0x7A, on name id 0x6C) and
  remakes Lavian and Alicia (name ids 0x78, 0x79) as Knights (job 0x4C, their equipment) and Agrias as a Holy Knight
  (job 0x1E). A Ramza not in the roster, or drawn as Agrias, takes another path that writes unit fields the port
  doesn't map (+0x99-0xC7, +0x12A); neither can happen in the port. It loads 0x0FB's conditions (0xCE 0xFA, as
  ReMixed's does; a scene walks them only as it starts) and hands on to 0x11A.
- **0x11A** (map 57, Orbonne's chapel; the same entry; preset 0x119): Simon tends Ovelia; Whitney, an Ordallian
  sleeper agent ("Bonne nouvelle pour moi... mais pas pour toi!"), turns on her; Delita, who knew of her, takes
  Ovelia. Then, outside a replay: var 0x6F 5, flag 0x92, the first four missions' unlocks (0x1A5-0x1A8), the world
  map's chapter-2 places and routes (chapter 1's and Orbonne shown, 1-A's hidden: the remaster's own chapter-1 reveals
  are the same flags, all among them), the Persons states (0x3C0-0x3FF), Agrias moved to roster slot 18 as a guest
  (+4 bit 0x02, ReMixed's guest marker, which the remaster doesn't read: in the port she stays an ordinary unit there),
  every unit's errand byte cleared but bit 0 (1-A's units kept aside), and on to ReMixed's 0x01B, chapter 2's start:
  the remaster's 0x074 (REMASTER_HAND_OFFS).
- **In the port** (`SIDE_CHAPTER_A_END` in tools/build_mod.py): 0x0FC as 126 and 0x11A as 127, Araguay Woods scenes
  of the remaster's that can't run since ARAGUAY_LUSO (their text rows come off); 1-A's endings back to the world map
  (they had handed on to 0x074 and set var 0x6F, which 0x11A sets now); the start at Orbonne (REMIXED_WORLD_STARTS);
  and 1-A's world-map steps (`REMIXED_WORLD_STEPS`: 28, at locations 5, 0, 31 and 27, each before the starts there),
  which the port hadn't had: after 0x0EC hid chapter 1's places nothing of 1-A's way was shown, and the regression
  runs started its scenarios with the harness. The remaster's EnhancedWorldEvent rows are the PS1's statements one
  for one (Type 32, 34 and 36 are the opcodes 0x20, 0x22 and 0x24, checked against vanilla's programs and the
  remaster's rows), so each step is its flag at 0 and the story's value, then the action, and waits for 1-A's mark
  (flag 0x2EC) as 1-A's starts do; ReMixed's two 0x1D steps (var 0x6D, a tutorial's) are left out, as the remaster
  leaves out vanilla's. Both events need nothing more by their instructions. Not seen in the game yet.
- **To look at in the game:** Agrias, an ordinary unit in slot 18 after 0x11A, when the remaster's chapter 2 has her
  join (its join skips a sprite set the roster has); Orbonne shown through chapter 2 (as in ReMixed; the remaster
  shows it at 34); 0x11A's flags are ReMixed's over the remaster's own at its chapter 2's start.

## The Nemesis's return (0x11D, read 2026-11-12)

ReMixed's Nemesis System (docs/MECHANICS.md; its name's return ported in `Patches/Nemesis.cs`) has a scene: 0x11D.

- **How ReMixed reaches it.** Its battles' conditions (108 presets: every story, side and mission battle of its own,
  not the random battles, whose 0x191 loads none) run its Preparation Menu (0x192) on "flag 0x8F = 0" as the battle
  begins. The menu's Start Battle zeroes vars 0x70-0x7F and flags 0x80-0x8E, sets flag 0x8F and, with a Nemesis
  stored in var 0x40 and not disabled (bit 0x100000), loads preset 0x11C ("flag 0x8F = 0 -> 0x11D") and clears flag
  0x8F again, so 0x11D runs at once. Nothing else names 0x11D or its preset.
- **What it does.** It looks for the battle unit whose name id (+0x16C) is the Nemesis's, on an enemy team (+5 &
  0x30). Found, the camera goes to it and it speaks, named by a text value (word 0 = 0x4000 | its +0x18A, its battle
  slot: shown by the text code 0xEB), with Ramza's answer: on a first meeting (the countdown nibble 0) "It's... it's
  you! [Ramza]! Do you remember me?", a menu ("Yes, I do." or "No, not at all."), and its threat; later "This time...
  I'm gonna make you pay!", "You again..." or "Do I know you?". Then it strengthens it by the story's section (var
  0x6F: Brave and Faith 70 to 85, ChangeStats 0x83 and InflictStatus 0x92, which the upgrade changed), sets ReMixed's
  unit preset (+0x169, 0x13 or 0x14), its secondary skillset by its weapon (+0x13), support and movement bits
  (+0x90-0x92), "Nemesis" as its title (+0x13C, ReMixed's second 16-byte text), its learned-ability bytes
  (+0x99-0xD2) and AI bytes in BATTLE 0x8019F9B4 + 5 x its slot; then (found or not) sets flag 0x8F, zeroes the same
  vars and flags, and loads the battle's own conditions back (0xCE var 0x3E - 1).
- **What its writes are** (read 2026-11-12 in ReMixed's BATTLE.BIN). 0x8019F9B4 is vanilla's snapshot of the units
  ([0x80173C78], taken by 0x80148580 as a battle's event starts and put back by 0x801488A8 as it ends), by unit index i:
  +0 + 5i the innate statuses (+0x4E), +0x69 + 5i the current ones (+0x58), +0xD2 + 5i the second copy (+0x1BB), +0x20D
  + 16i the counters (+0x5D), +0x35D + i (+0x15D), +0x372 + i (+7), ReMixed's addition +0x387 + i (+5: team, control, 4
  Immortal; 0x8019FD3B) and +0x3C6 + 2i (HP, +0x28), each copied back to its field. +0x13B and +0x1A4 + 5i aren't
  statuses: both start as the current ones under a mask (0x80169758-0x8016976C), and the putting back makes +0x1A4 the
  statuses the event added and ORs those it took away into +0x13B, puts the snapshot's statuses back, then for each of
  the 40 calls [0x80173CA8] = 0x80068E80(status, 0 or 1, unit), the sprite's status effect turned off for +0x1A4's and
  on for +0x13B's. So 0x11D gives the Nemesis Reraise (from section 5), Protect and Shell (always), Haste (from 0x10;
  var 0x6F reaches 0x10 at most) and Immortal by the snapshot, their effects shown by +0x13B, and +0x99-0xD1 (its
  learned abilities, three bytes a job) all but each job's middle byte. ChangeStats' stats in ReMixed's rewrite
  (0x801491D8): 0 HP, 1 MP, 2 CT (0-100), 3 Brave, 4 Faith, 5 PA, 6 MA, 7 Speed (5-7 the base +0x30-0x32 too); vanilla's
  has 0, 2, 3 and 4. The Preparation Menu gives every unit 100 CT (0x83 on 0x0204, a unit number past the groups: every
  unit) so the conditions are checked at once and 0x11D runs before any turn, and 0x11D takes 999 off again. Its 0x92
  (`ff 00 00 0c 00`) is vanilla's own (322 uses of `xx 00 00 0c 00` in vanilla's events). 0x8018F520 is a battle global,
  a unit's index (-1 none: 0x80182848; 0x80150414 takes the unit's address from it), which it sets to 0xFF. Its weapon
  test reads ReMixed's item table (SCUS 0x80062EB8 + 12 x the weapon, +5 its type). The meeting nibble (var 0x40's bits
  24-27, which picks the later meeting's lines) is set by nothing of ReMixed's (0x008 and the name picker keep it), so
  it's always the first meeting; with the Nemesis not in the battle and that nibble 0, it ORs 0x10 into var 0x40's top
  byte (a battle more on the countdown).
- **In the port** (`NEMESIS_SCENE` in tools/build_mod.py, 2026-11-12): 0x11D as 437 (0x1B5), a scene of the remaster's
  behind Zalbag at Eagrose's start. Its trigger: the 108 presets' "flag 0x8F = 0 -> 0x192" sets run 0x1B5 (the build had
  made them unable to fire), and the 15 shipped battles that hand on to the menu from their opening instead (side
  chapter 1-A's, the Algus campaign's 0x0FF-0x101, Luso's 0x0EA) get that set after their opening's; 0x0F5's battle has
  all 16 sets taken and goes without. The code mod does the menu's Start Battle (`RemixedScenarios.StartBattle`): for
  one of ReMixed's scenarios whose conditions have the set, flag 0x8F = 0 when its battle begins (its record has a
  formation: a scene's run has none, 1-A's scene-first battles' first run included) with the Nemesis System on and a
  Nemesis stored and not disabled; else 1, as the menu leaves it. So 0x11D runs at the first check after the opening,
  before the battle's phase sets (it's the second set), as the menu's did, and zeroes vars 0x70-0x7F and flags 0x80-0x8E
  there as the menu did. The remaster's own story battles load no conditions of ReMixed's and don't run it (in ReMixed
  they do); their events keep their state in those vars and flags.
- **Changed in 0x11D**: its opening's 999 off every unit's CT is left out (with no menu nothing gave the 100, and the
  remaster checks the conditions as a turn comes, so it would take that unit's CT); its closing reload of the battle's
  conditions (0xE6 40 01, 0xCE) is left out (they're never replaced: `EVENT_CODE_DROPS`); ReMixed's skillset 0x16 is the
  remaster's 0xE0 (`EVENT_CODE_PATCHES`); flag 0xB2 reads 1 (`SCRIPT_VERSION`), so its lines are the PSP script's
  (15-28). Its speaker is named by its text value: its lines with that name carry `<speaker="{eb}">`, which the code mod
  fills in with the battle unit's name as the line shows (`RemixedText.Reference`, 0x40xx: the slot's name id through
  the game's name lookup), as the remaster's own lines name a speaker (`<speaker="Marach">` on a line whose Speaker row
  is "Outland Mage"); its Speaker row, "Nemesis", is the stand-in. Five of the PSP lines (18, 20, 24, 25, 26) have no
  name in ReMixed either.
- **Its places**, in the code mod (`RemixedEvents.MapUnitPlaces`): the unit's preset (+0x169) is `UnitPresets.Slots`
  (taken into its stats at its next recalculation), and the snapshot's tables go to the units' own fields, where the
  putting back takes them (the remaster keeps no such copy), so its statuses and Immortal are the Nemesis's at once;
  +0x13B and +0x1A4 (the sprite effects) and the HP copy stay in the upgrade's store (whether the remaster shows a
  status's effect when its bit is set this way isn't known yet). The learned abilities (+0x96-0xD1, the remaster's +0x9F
  on) are the upgrade's (b77b6bd, pinned on main), as are the rest of the unit fields it reads and writes. Queued for
  the upgrade (its session, 2026-11-12; with its parked pin): the hack's ChangeStats 5-7 (+1 to +4 PA, MA and Speed do
  nothing yet; Brave and Faith are writes, and the +50 CT works) and the item table (its weapon reads as type 0: in
  sections 13 on, an armed Nemesis gets 0x55 whatever its weapon). +0x13C ("Nemesis", a second name the PS1's unit
  screen shows) and 0x8018F520 have no place: the store (setting the remaster's unit in turn to none mid-turn isn't
  wanted). Not seen in the game yet.

## Luso at location 24 (2026-11-12)

- **The start.** Location 24 is Mandalia Plains (the remaster starts its 15 there at story 2 and 59 at 10). ReMixed's
  list for it (WLDCORE's table 0x80097234) has five entries; the fifth (WORLD 0x80149D28) is `0001 0092 0001`,
  `0001 0093 0000`, `0003 006E 000F`, `0019 00EA 0001`: flag 0x92 set (Biggs and Wedge's scene at Gariland, 0x0A4,
  sets it at story 4), flag 0x93 clear, the story at most 0xF (op 3 fails while the variable is above the value,
  0x80091518; this plan had it as "at least"), then 0x0EA, mode 1. So it's chapter 1 (or chapter 2's start), before
  Araguay Woods (0x10), whose 0x107 reads 0x93: Luso's "It's been over a year" there.
- **0x0EA** (map 85, entry 0x107, preset 0xE9): Ramza, Delita and Algus (Ramza and Algus's lines swap in the Algus
  campaign, flag 0xAF) see monsters and a stranger fleeing a big chocobo; a question, stop the chocobo or help the
  man (flag 0x98), then the battle through its Preparation Menu (one group, formation 0x14E: MENU_BATTLES) with Luso
  (unit 0x12) as a guest. Its preset: set 1 (flag 0x82 clear, at once) runs label 01: with the man helped, the
  party's Brave or Faith up or down (0x83 on group 0x101 and on Algus), and unless Luso is already in the roster his
  unit gets learned abilities (+0x9E |= 0x10, +0x9F-0xA1 = 0x800000), a job level (+0xD3's high nibble 1) and JP
  (+0xE0 = 0x4D, total +0x108 = 0x7F) for job 2; the chocobo (unit 0x86) is named "Koco" ("Big Chocobo" at +0x13C)
  and its raw stats changed (HP, PA, MA × 2, Speed × 0.6: +0x72, +0x7B, +0x7E, +0x78). Set 2 (flag 0x80 clear,
  Luso acting, Luso and Ramza standing) runs label 02, his lines on his turn; set 3 (flag 0x81 clear, Luso's HP at
  most 0) label 03: if Ramza chose to help him, the battle is lost (preset 4, ReMixed's retry menu), else it goes on;
  set 4 (victory, flag 0x8E clear) label 04: a line, then a return to its own preset (event 8's vars 0, 0xE9, 0xFFFF,
  flag 0x8E: AFTER_BATTLE_RETURN), whose set 5 (victory, flag 0x8E set) runs 0x0EB.
- **0x0EB**: the units' statuses and places put right, Luso's story (from another land, a hunter learning Blue
  Magic), Delita's offer; unless he's in the roster, his unit is flagged to join (+6 = 0x90) and instruction 0x78 7
  joins him (the PS1's DisplayConditions 7 goes to REQUIRE.OUT's joiners; the remaster's 0x78 7 to the same, 0x2CB9E8
  → 0x2C8818 → 0x2C60E4, unless it's a replay), then a check of the roster for sprite set 0x12: joined, his "nice new
  entry in my Grimoire" (Persons state 0x3F6 = 1); not (likely a full roster; not read further), Ramza says no and
  Luso leaves for the Soldier Office (0x3F6 = 2). Then flag 0x93 and the world map.
- **The unit fields** (mapped 2026-11-12 in Ps1Memory.UnitField, the EIU submodule's b77b6bd, made by the EIU session;
  a unit's JP capped at 9999 as the roster's; read from the two roster-to-unit builds side by side: the PS1's copies roster +0x19 on to unit +0x72, +0x28 on to +0x96, with +0x64 to +0xD2 and +0x6E to +0xDC;
  the remaster's 0x276758 roster +0x20 to +0x7B, +0x2F to +0x9F, +0x32 to +0xA2, +0x74 to +0xE4, +0x80 to +0xF0, +0xAE
  to +0x11E): PS1 +0x72-0x80 (raw stats) and +0x96-0xD1 (unlocked jobs, learned abilities) are the remaster's + 9,
  +0xD2-0xDB (job levels) + 0x12, +0xDC-0x103 (JP) + 0x14, +0x104-0x12B (total JP) + 0x1A. Not mapped: +0x13C-0x14B
  ("Big Chocobo", which no reader of the remaster's shows) and the chocobo's sprite object (0x0EA walks the PS1's
  list from 0x80098A54 for unit 0x86's and writes its +0x325 and +0x327, likely its size: the walk ends at once in the
  port, as those addresses read 0, and the chocobo is drawn at its usual size).
- **Luso in the remaster.** It has none (no Luso or Balthier in its CharaName table). ReMixed's Luso is its slot 0x12
  (sprite set, name, job), the remaster's Marach's; ReMixed's events find him by the sprite set (HAS_CHARACTER and
  ROSTER_ADDRESS 0x12) or his battle id, never his name. The user's call (2026-11-12): he keeps slot 0x12, so all of
  that works as written, with a name of his own: CharaName 0x38 (blank in the remaster, named by no unit of vanilla's or
  ReMixed's battle data) is "Luso", and his battle-data units name it (REMIXED_SPRITE_SETS, LUSO_NAME), here and at
  Araguay. He's drawn with ReMixed's own sprite (its 12.SPR on graphic 0x12, with a texture pair of its own, the
  remaster's unused PSP-Luso stubs 0x460-0x461: REMIXED_SPRITES, c08eeca; the user's call, 2026-11-12), now that the
  remaster's rooftop Marach is on 0x1A (REMASTER_MOVED_SPRITE_SETS). His job is ReMixed's
  row 0x12, which the job tables port over the remaster's 0x12, and slot 0x12 is his alone ("Job row 0x12" below).
- **Job row 0x12** (2026-11-12, offline). Vanilla's (and the remaster's) row 0x12 is Marach lying dead on the Rooftop of
  Riovanes: skillset 0x2E, innate Dead, immune to every other status but Wall and Dark/Evil Looking, no growths; only
  that battle's entry (0x1B1, unit 2: sprite set, name and job 0x12, in all four of its scenarios, 0x123-0x126) and the
  test entry 0x1F2 use it. ReMixed made the row Luso's (skillset 0x50, Secret Hunt, his gear and growths) and gave its
  own rooftop (its entry 0x151, scenarios 0x4F and 0xA7) Malak's playable slot, sprite set, name and job 0x1A, with its
  unit preset 1 (byte 37), a corpse: innate and current Dead, immune to every other status (near enough vanilla's row).
  The port's battle merge already pairs the remaster's 0x1B1 with ReMixed's 0x151 (changed_battles) and gives Marach
  that unit's build, job 0x1A, but the remaster's battle data can't carry byte 37, so the corpse was lost and he'd have
  stood up and fought (not Luso's job, as first thought: the rooftop's job in the port was never 0x12). So, closest to
  ReMixed: the row stays Luso's (not a REMASTER_KEPT_JOBS row, as Mustadio's 0x22 is), and merge_entd now keeps the
  preset of every unit given ReMixed's build, which the build lists under each of the remaster's scenarios that load the
  entry (UNIT_PRESETS_FILE; the code mod's UnitPresets reads any scenario by var 0x27 and the loaded entry). That gives
  the rooftop's four scenarios Marach's corpse, and Gafgarion ReMixed's preset 23 (immune to Petrify, Invite, Blood
  Suck, Transparent, Berserk and Charm) at Golgorand Execution Site (0x19E, scenarios 0xB4-0xBC) and Lionel's gate
  (0x19F, 0xBF-0xC3), the only other units the merge gives ReMixed's build that have a preset there. Then, for Luso
  drawn as Luso (the user's call, 2026-11-12, with ReMixed's 12.SPR from the sprite port), slot 0x12 became his alone,
  as in ReMixed (its 12.SPR is Luso, its 1A.SPR vanilla's MARA.SPR, the sheet both of vanilla's Malak slots draw): the
  remaster's only units on sprite set 0x12, the rooftop's Marach and the unused test entry 0x1F2's, go to sprite set
  0x1A (REMASTER_MOVED_SPRITE_SETS; the remaster draws 0x12 and 0x1A from the same texture, 0x368), keeping name 0x12
  ("Marach") and battle id 0x12, by which the remaster's rooftop events find him. Every other Marach of the remaster's
  battle data, its added entries 0x233 and 0x23B too, is 0x1A already. Not seen in the game yet.
- **In the port** (LUSO_AT_MANDALIA): 0x0EA is 123 and 0x0EB 124, the remaster's Araguay Woods scenes that can't run
  since ARAGUAY_LUSO (REUSED_SCENARIO_NUMBERS: their voiced text rows come off). The start is a row at location 24
  after the remaster's (flag 0x92 = 1, 0x93 = 0, var 0x6E at most 0xF, outside side chapter 1-A). 0x0EA's opening
  marks Algus (or Ramza), Delita and Luso away for ReMixed's formation (roster +0xD0 = 3, 0xF), which ReMixed's
  Preparation Menu and random battles' event clear; the port has neither, so those writes are reads
  (EVENT_CODE_PATCHES; the remaster's chapter-1 roster has none of the three anyway). Its battle is scenario 0x34D,
  separate from scene 123 even though both use map 85: a hand-off to the same scenario can leave the scene setup live,
  where preset 0xE9's already-satisfied victory then starts 124 without loading formation 0x14E. The generated-data
  regression check requires the distinct hand-off, the battle formation, exactly one victory-to-124 set, and 124's
  single remove/add lifecycle for Ramza, Delita, Algus and Luso. 0x0EA's four PATCH_NEXT-selected lines also receive
  valid 0x0004xxxx templates before conversion; leaving their PS1 0xFFFF placeholder produced empty Ramza/Algus boxes
  after only the low half was patched. Seen through combat and recruitment on the PC (2026-11-15): battle 0x34D has a
  private entry carrier. Its ReMixed inclusion flags 0xC1-0xCC are restored before the remaster's battle setup, so that
  setup prepares Luso and battle ids 0x80-0x88 with their sprite resources, then the opening activates the prepared
  cast with AddUnit's immediate-visible mode. AddUnit without those pre-setup flags made them fight but made Luso
  reuse Ramza's sprite; preserving the source scene's hidden-add mode without its later Draw operations left monster
  portraits and HP bars but no models. In the corrected live pass all nine enemy models and Luso's own red-cap sprite
  rendered; the fight waited for all nine enemies, ran 124 once, recruited Luso, and returned to the world map.
  A later Deck pass (2026-11-16) exposed one more scene-carrier leak: entry 0x107's data-built Ramza (battle id 0x90)
  remained beside the roster Ramza placed by the formation, as a second guest in a corner. The split battle now omits
  that scene-only record; event 124's actor 1 resolves to the deployed roster Ramza. The generated-data checker audits
  every direct scene-to-battle carrier for retained data-built Ramza records, not only Mandalia. Seen in the hidden
  PC runtime: only roster Ramza remained, Luso and nine enemies loaded, recruitment completed with Luso in roster slot
  3, the world map returned, and the game logged no crash. The next Deck pass found the first-turn conversation on a
  black screen with empty Luso and Ramza boxes. The black screen was the opening scene's terminal full-screen colour:
  ReMixed's Preparation Menu normally cleared it, while the split's direct activation did not. The activation now
  fades in and waits 30 frames. The text itself had two ScenarioId owners: stock row 1813 assigned event 845 an
  unrelated Orlandeau/Folmarv PZD, while generated row 5140 assigned Mandalia's lines. Text generation now removes
  every event it owns from stock rows (rather than only REUSED_SCENARIO_NUMBERS) and verifies exactly one owner for
  each of 122 generated text events. That global audit also fixed latent duplicate owners for 0x375 and 0x376, the
  Hirame/Wright branch. Seen in the hidden PC runtime on 2026-11-16: Luso's message 51 (line 318466) and Ramza's
  message 52 (line 318467) both drew their full text over the visible Mandalia map during combat. Event 124's later
  recruitment phase no longer runs the PS1 battle-end DisplayConditions state 7 in the middle of the scene: its six
  audited recruitment sites use the remaster's Join Now operation, while 51 exact post-battle actor removals erase
  corpse/crystal drawables before logical removal. A dedicated `harness.py checkpoint 34D 7C 5 --messages 49` now
  reaches that phase without playing combat. Its 2026-11-16 frame kept map 85 and the dialogue visible. It also found
  that event graphic 0x10's appended private alias had no enhanced texture mapping (split body parts), then that its
  isolated PSP texture number's automatic +0x8C companion was an unrelated nonzero picture (black silhouettes).
  `RemixedEventGraphics` supplies the private textures; the settled checkpoint shows coherent coloured actors and
  Luso's complete message 49. A real victory-path pass then showed correct recruitment dialogue on a black map: unlike
  the direct phase checkpoint, event 845 first ran display state 7. Offline comparison proved event 124's 48
  Ramza/Delita/Algus animation instructions match source 0x0EB byte-for-byte and identified the cross-event teardown;
  all same-map battle returns now use the synchronous live write-back described above. The hidden real-victory run on
  2026-11-16 captured messages 30, 35, 40, 45 and 49 across natural event 124 plus its post-event frame: map 85 stayed
  rendered, all four actors remained coherent and coloured, and the game logged no crash.
  Correction (2026-11-18): Join Now (0x43 1, 0x227458) writes the flagged units into the roster with no screen, so
  from then on Luso at Mandalia, Luso and Boco after Araguay, Zalbag at Eagrose and 0x11B's recruit joined silently
  (reported for Boco: "scene ends, boom, he's in your party"). The remaster's join screen is display state 7 itself:
  0x2C8818 runs the write-back 0x2C60E4, which queues each joiner the roster hasn't yet (0x14376CD40, count
  0x14376CC68) and returns how many; with none the task ends there, with some it saves and deactivates task 1, plays
  the join fanfare, shows each one's "New Party Member" screen with Accept (written by 0x274EB8) and restores task 1.
  The black map of the 11-16 victory pass came when generated exits still jumped to state 7 without the results
  states 2-6 (fixed 2026-11-17). The six recruitment sites keep ReMixed's state 7 again, as does Araguay's world-map
  way for Boco (`build_mod.py` MIDSCENE_JOIN_SCREENS); the logs name each join screen's unit (`BattleEnd`,
  `RosterGuard`).

## ReMixed's menus (2026-11-06)

ReMixed's questions are vanilla's kind: the text lists the answers between FB and FC, an F8 between them, and the
answer goes to var 0x18 from 0 (vanilla's 0x10 has `<FB> 1. ...<F8> 2. ...<FC><FF>`). The remaster turned each of
vanilla's into a line `<cursor>1. ...\n2. ...<cursorend><forced>` (after `<speaker="Ramza">` or `<hidespeaker>`) and
its MESSAGE's style gained 0x4 in its kind (vanilla 0x70, 0x11, 0x12 became 0x74, 0x15, 0x16; its 9 MESSAGEs with the
bit are its 6 menus and 3 of vanilla's 0x15 boxes), its last byte 0 (vanilla's 3 for them). The build does the same
(`remixed_line`, `message_style(..., menu)`): 32 menus in the shipped events, all in side chapter 1-A.

- **Messages end with FE or FF**, not FE alone: FF closes a box without a button (after a wait, or a menu's answer),
  FD keeps a box open while the next shows. Splitting on FE only had merged each FF message with the next, so the
  lines after it went one off (the Lich's lines went to the wrong speakers). The split skips E2/E3/E6/E8's byte and
  D0-DA's second byte. E3 08 ... E3 00 is a speaker's name only at a message's start and ended by a line break; mid-
  line it's emphasis ("I *must* have it", which had become a speaker named "must").
- **Seen on the PC:** Agrias's question in 0x0ED (style 0x92, a menu 0x96, at her unit), the cursor and the answer
  (var 0x18 = 1 and its branch).
- **The offer (0x0EC):** ReMixed shows its explanation (message 26) and the menu (27) at once in two boxes of its own
  place (0xE, its screen-placed boxes) with unit 0, before its scene fades in. The remaster drew neither, before or
  after the fade-in, with unit 0 or Orlandu's, as place 0 with ReMixed's last byte or with 0x0A (as its own centred
  narrations have), though the menu took its answer unseen; its place-0 MESSAGEs are centred narrations at a unit.
  As one speech box at Orlandu, the explanation and the answers overflowed it (three lines). What works (seen both
  ways): the question block moved after the fade-in and the camera's first move (`EVENT_CODE_MOVES`), the explanation
  as the event's own narration (style 0x0B, unit 1) with a wait, then the answers in a speech box at Orlandu (0x0B)
  styled as Agrias's (`QUESTION_BOXES`); declining fades out first (`EVENT_CODE_INSERTS`: its end otherwise stayed on
  the scene) and goes on to the remaster's 0x074/0x075, as before. Flag 0x20F, which gates the question, is set by
  the PS1's code only, so it always asks (`EVENT_CODE_PATCHES`). 0x111, a near copy, has the same question behind
  the same flag and never asks.
- **Seen in passing:** long narrations show a literal `<invisible>` mid-line while they type out (the remaster's
  typing over ReMixed's 900-character narration); 0x0ED's scene has no map (map 99: no vanilla scenario uses it).

## Side chapter 1-A: its recruits (2026-11-06)

- **0x0EC** sets Ramza aside (roster slot 0 into 19 with ReMixed's roster swap), marks the rest unavailable (+0xD0
  bit 0x08) and flags its party to join (event units 0x22 Agrias, 0x84-0x86 three Lionsguard knights, names 121, 120,
  108: +6 |= 0x10). It makes them join at once (instruction 0x43 1) only for a roster size it knows in the low byte of
  var 0x17 (ReMixed's: 0x11 by default, docs/analysis/world-and-menus.md); otherwise it beeps and leaves them to the
  event's end, which in the remaster made no one join (seen on the PC), and the first battle then had no unit to place
  (black, no formation). The build has 0x0EC and 0x111 read 0x11 (`EVENT_CODE_PATCHES`).
- **0x43 1** in the remaster is 0x227458: it writes each unit flagged to join (+2 0xFF) into the roster at once, but
  leaves the flag set; at the scene's end 0x2C60E4 writes each still-flagged unit again, skipping one whose sprite set
  the roster has but not a generic (0x80, 0x81). Its own events call it twice (its opening, event 467). The code mod
  clears the flag of a unit that join wrote (`RosterGuard`).
- **0x0ED** (Bryn and Lorne, a dancer and a bard, names 105 and 106) flags units 0x90 and 0x91 to join, joins them at
  once if Agrias offered help (flag 0xAD), and gives their roster records ReMixed's sprite sets 0xB5 and 0xB4. Seen on
  the PC: Agrias and the knights in slots 0, 3-5 after 0x0EC, Bryn and Lorne in 6 and 9 after 0x0ED, once each.
  0x0ED runs by itself on the world map after 0x0EC (its start at location 5, progress 0).

## Maps the remaster doesn't have (2026-11-06)

The PS1 finds a map's GNS (its list of parts) by number in BATTLE.BIN's table at 0x8EC74 (a sector per map, 128).
ReMixed points 118 at its TUNNEL.GNS (sector 874), 119 at VOLCANO.GNS (209) and 120 at vanilla's MAP066.GNS; the rest
are vanilla's. The enhanced remaster draws a map from its own meshes (0000.pac `bg/meshes/map_NNN`), which cover maps
1-96 (not 15, 33, 42), 100 and 103-116; its PS1-format map files (0002.pac `fftpack/map/map_mapNNN_*`) have the
others, but the enhanced game shows nothing there: units on the sky backdrop (seen on the PC in 0x0ED). The shipped
scenarios on such maps: 0x0ED (99), 0x0F2 (97), 0x0F9 (101), 0x0F8 and 0x117 (119, VOLCANO). Vanilla's scenarios never
use 97, 99 or 101.

**Looks built (2026-11-06), `tools/maps.py`:** the format of a look is The Ivalice Chronicles Mod Studio's reading
(Zodi-ark's notes in its `map_enhanced.py`), checked on map 021's own files: the enhanced mesh shares the classic
map's coordinates (402 of 465 quads made from a classic one have exactly its corners) and reverses its winding (a quad
A C D B, a triangle A C B); a corner's word carries the polygon's tile, its classic index and its render bits. The
build (`write_map_looks`, maps `NEW_LOOKS`) makes a texture group per palette (its colour picture the classic texture
in that palette, 256 x 1024; a flat mid-grey lighting picture, as the game draws colour x lighting x 2), puts the
mesh and pictures under bg/, and fills the game's placeholder rows for the map in RefinedBgTexture,
RefinedBgMeshNode and RefinedBgSystemTexture (the units' lighting copied from map 044's). Seen on the PC: 99, 97 and
101 drawn, in 0x0ED, 0x0F2 and 0x0F9 (the last as scenario 0x2EC: a number past the remaster's last runs). 
**Highlight panels and VOLCANO (2026-11-06).** The enhanced game draws a tile's move and target highlights on panel
polygons of the look (a part `GT_<group>_panel_ui`, the group after the map's last, no pictures), not from the grid;
every built look has them now, made as the game's are (a quad 0.2 over each tile the cursor can stop on, two triangles
where one corner is raised or lowered against the rest, render 0x8001, its RefinedBgMeshNode row (0, -100, 0, 1, 0, 1,
1, 1)). ReMixed's VOLCANO is map 119 in its battle data (its GNS table points 119 at MAP/VOLCANO/VOLCANO.GNS); the
remaster has vanilla's 119 there (map_map119_6.bin, a texture, and map_map119_7.bin, a 24,436-byte mesh, which its
GNS names for the default state). The mod gives the game ReMixed's texture and mesh in their place
(`REMIXED_MAPS`; `PackFiles` now reads the mod's fftpack subfolders by the mod loader's list), the mesh without its
render properties, its last 4,096 bytes, so it fits (the game reads no more than the file it replaces), and builds
the look from ReMixed's full files. Seen on the PC: 0x0F8 (scenario 0x2EB) from its formation, the deployment tiles
on the panels, through its battle and its closing scenes (0x117, then 0x0F7) to the world map. Not done: ReMixed's
TUNNEL (118, no shipped event), its edits to MAP075 (1-A uses 75), its weather and night variants of VOLCANO (the
look is the default state's).

## Testing without the Deck (2026-11-05)

`tools/harness.py` is the way in now: `start` boots the game on a hidden desktop (below) with the mod's save folder
put back to a fixed set (`work/harness/saves`; a battle's autosave would otherwise be what Continue loads next), and
returns once the mod's pipe answers (`code/IvaliceRemixed/Harness.cs`, `\\.\pipe\ivalice-remixed`, only in test
runs); `send` gives the running game an autopilot step or `state`, `peek`, `poke`, `var`, `setvar`, `find`, `call`
(a game function, for lookups: `call +0x1DB0FC [+0x3CD9DB8] 315160` is a line's text); `shot` takes a screenshot;
`log` and `stop`. The formation screen answers once the game thinks its window is active (below): F picks a tile and
then a unit, Tab switches groups, Space commences once each group has a unit, F answers Yes; keys sent during a
screen's fade are lost, so 3 s or more between them and a look at a screenshot works best. A run from launch to the
fete's scene takes about four minutes.

`tools/headless_pc.py` runs the game with the mod on this PC on a Windows desktop of its own, which is never shown
(no window on the screens, no focus taken), and screenshots its window every few seconds (PrintWindow works there).
The mod's autopilot (`code/IvaliceRemixed/Autopilot.cs`, only with an `autopilot.txt` beside it) mutes the game's
audio session on every output device and runs a script: key presses (window messages plus GetAsyncKeyState and
GetKeyState), pad presses, a scenario started from the world map's next frame, and yes-answers from the game's
action test (0x0FC598) and the UI's three action checks (0x0F0D00/4C/98, hooked natively). Enter gets from the
title through Continue to the world map (about 100 s from launch). The formation screen's unit placement doesn't
answer to any of those yet: its "pressed" check asks for the directions, 0x59 and 0xDD–0xE0, and none of them places
a unit. The saves came from the Deck (the mod's own save folder), and the PC's mod config has the test mode on.
A native wrapper on the battle loop's frame (0x37FB7C) that clears pad bits in the first pad's struct for one call
doesn't place a unit either (Cross 0x40 or Circle 0x20). The reason was the window: the game's input update (0x1F9D8)
drops its input while GetActiveWindow isn't its window, which on a hidden desktop it never is. In test runs the
autopilot answers GetActiveWindow with the game's window and makes SetCursorPos and ClipCursor do nothing (the game
calls them for its active window, and they'd move or hold the real mouse); GetForegroundWindow (fullscreen) is left
alone. With that, keys reach the formation screen.
`tools/deck/headless.sh` tried the same on the Deck with gamescope's headless backend: the game starts, but Wine's
X11 window dies on a BadWindow when the swapchain is resized, and Wine's Wayland driver isn't available.
