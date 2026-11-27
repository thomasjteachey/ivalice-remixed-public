# Gameplay to-do

What's left of ReMixed's gameplay to port to the enhanced build. Each item says what changes in the game, how big it
is, and what it waits on. The technical details (what ReMixed's code does, where the remaster's code is) are in
[MECHANICS.md](MECHANICS.md) and [REMASTER.md](REMASTER.md); data held back is in
[the build report](generated/mod-build.md).

**Progress: 94% of gameplay** (106 done, 6 open: 2 small, 4 medium, 0 large). Worked out from this list by
`python -I tools/todo_progress.py`, open items weighted by size (small 1, medium 3, large 15) and done ones at 2 each,
and updated with every change to the list. Everything outside the story and display ports is ported or settled
(the formulas ReMixed rewrote in place were the last, 2026-11-12; ReMixed's own battles writing their units back,
found and built 2026-11-13); the game is being played on the Deck now, with the test mode on.
**The whole port: 80% done**, counted by
`python -I tools/port_progress.py` ([PORT_PROGRESS.md](PORT_PROGRESS.md): the parts, their shares and the history).
Done: the gameplay; random battles' event running ReMixed's own code; ReMixed's unit
builds in 122 story entries; the Dark Knight's page and its hint. Off the list by choice: ReMixed's re-staged
scenes and its text (the remaster's scenes, dialogue and voice acting stay). Left: ReMixed's battles that only its
own events reach (its side stories and missions, with its unit presets and AI flags: planned in
docs/analysis/story-flow-plan.md), its Depths of Murond post-game hub, and the display and art items (dismissal
message, zodiac markers, target cycling and the status tier label).

**Sizes:** *small* is a hook or two, *medium* a few hooks or a table, *large* means code nobody has mapped yet, or
menus. What's been seen working in the game is noted on its line; the rest is in Testing.

## Story port progress

ReMixed's own story content, which the remaster doesn't have (plan: docs/analysis/story-flow-plan.md). Counted in
events: built means the mod ships it; working means seen running in the game. Each event is listed in
[PORT_PROGRESS.md](PORT_PROGRESS.md), where `tools/port_progress.py` counts them.

**79 of ReMixed's own events to bring over: 79 built, 42 seen working; 21 in-between scenes to read: 21 read.**

The events, by group:

- **Missions** (31 events, all built; once called location battles): ReMixed's Rendezvous battles, posted under the
  Missions button in every tavern as its story unlocks each (since 2026-11-18; they were among the Rumors as "Mission:
  Lost Heirloom" and so on before), with ReMixed's text. The confirm
  button on a posting asks whether to accept it (Start, as in ReMixed, until 2026-11-19), and the battle begins as you
  leave town. Nobody is lost for good in one (a fallen unit stays
  down for that battle only) and Math Skill can't be brought in, as in ReMixed. Two are fought in rounds: The Shrine
  Knights in four, Brave Story in ten, each round on from the last. Seen on the PC (2026-11-07): Lost Heirloom from
  its posting in Gariland's tavern through its battle and back to the world map, The Shrine Knights' four rounds,
  Of Vagrants and An Ill Wind, posted late in chapter 4, and Be A Beta; before that the dancers' fete (on the PC and
  the Deck) and the bombs battle with Govis. The others: their scenes, lines (81 in all, 22 new speakers), units and
  maps. Mirror Match, the last built (2026-11-12, below), is posted late in chapter 4 too; not seen in the game yet.
- **Other characters' scenes and battles** (14 events, 3 built): the first side event, a Lich at location 28 (story
  progress 0x25), and Ashley's scene after it, seen on the PC from its scene through its battle and Ashley's scene
  back to the world map (the Lich and his Dark Crusaders, Ashley as the "Strange Man"). The Lich draws with ReMixed's
  own sprite and Ashley with hers (both seen on the PC, 2026-11-12). Ashley joins the party at her scene's end under
  her own name, as in ReMixed (seen on the PC, 2026-11-13; she didn't join before). Biggs and Wedge's scene at Gariland
  (0x0A4, the mod's 0x2FF), whose units ReMixed's
  0xE0 makes, seen on the PC: walking into Gariland at story progress 4 plays it once (ReMixed's start waits on its
  flag 0x97, which its version of the scene before sets with the progress and the remaster's doesn't; the mod's
  waits on flag 0x92, which the scene sets). The Nemesis's return (0x11D, the mod's 437; 2026-11-12): as one of
  ReMixed's own battles begins with a Nemesis due, the enemy with its name meets Ramza and grows stronger; not seen in
  the game yet, and its PA, MA and Speed gains and its weapon's skillset wait on the event upgrade. Gafgarion's 0x10B
  is an empty event nothing starts, and Ovelia's 0x11A is side chapter 1-A's end (below). Beowulf's quest (2026-11-12, your call: ReMixed's story where it differs):
  the dragon hunters in its first colliery battle (0x091, in the remaster's first colliery battle's place) and its
  Bremondt arc after Reis is restored (0x09C-0x09E, then 0x0A0); the remaster's other quest scenes and their voices
  stay. Not seen in the game yet. Luso (2026-11-12): met at Mandalia Plains in chapter 1, after Biggs and Wedge's
  scene, in a scene and a battle where he fights alongside the party against a big chocobo, then offered a place in
  it; his thanks at Araguay Woods in chapter 2 follow from it. The remaster has no Luso, so he's ReMixed's own
  (your call): his name, and ReMixed's sprite of him. Not seen in the game yet.
- **Zalbag and Algus** (9 events): mostly a secret campaign. ReMixed's opening (0x001) checks Ramza's name and,
  for "Algus" or "Argath", hands on to 0x0FD (on its TUNNEL map) instead of the story: a short campaign played as
  Algus on the Hokuten side (0x0FD, 0x0FE, then 0x0FF at location 15 through 0x103, battles but for the last), which
  ends by handing on to ReMixed's 0x07B, its version of the final battle (Alma against Ajora's resurrection). 0x104
  (location 15, opened by Hirame's rescue in 1-A) leads through 0x11B to 0x103 and the same final battle: shipped
  2026-11-12 as a branch of the normal game (your call: ReMixed's way), open from chapter 3 when Hirame was saved,
  once (until 2026-11-13 it opened when she was lost; ReMixed's own start for it never holds, so ReMixed never plays
  it); not seen in the game yet. 0x06E (Eagrose at progress 0x33) plays in place of the
  remaster's own scenes there, with its battle (2026-11-12, your call: ReMixed's storyline, where Zalbag lives if he has
  Shell, Reflect or Innocent as Dycedarg falls; not seen in the game yet). Six make units with "create a unit" (ported). Being ported (2026-11-06, the
  "Algus campaign": its 24 events with ReMixed's own chapter 1 for it). The remaster won't rename Ramza, so a
  setting starts it ("Argath campaign", Configure Mod → Story, off by default; seen on the PC). Seen on the PC, the
  remaster's opening goes on to 0x0FD on TUNNEL (on map 98), whose ambush battle goes on to its second half when
  Algus falls (the Hokuten arrive, Algus takes roster slot 0, Ramza moves to slot 10 under his own name), then 0x006's
  battle as Algus. After that battle the chain went wrong (into the remaster's own scenes 7 and 8), through three
  things found 2026-11-06: ReMixed's event 8 is both Dycedarg's scene and its after-battle event (shipping the scene
  had made every ReMixed battle's end run it), its after-battle mode (var 0x3A) wasn't cleared, and the script
  version flag 0xB2 (0x008 picks its next scene by it). All three fixed: seen on the PC, 0x006's battle now hands on
  to 0x007 (Argath's introduction), which plays whole and returns to the world map with progress 3. 0x008
  (Dycedarg's scene at Eagrose) ran as a bare battle (its preset left unrenumbered by the first fix), fixed: its scene
  plays and hands on to 0x105 (Argath's story on the bridge), which returns to the world map with progress 4, and
  0x00A's battle (formation, opening, battle, closing) returns to it with progress 5. Those tests started each
  scenario from the world map of an ordinary save; 0x00B's formation wouldn't commence there because the battles
  before it had marked Ramza away (as the campaign does: Algus leads, and Ramza waits in the roster).
  Ported to its end (2026-11-12), walked step by step by the build without the game: four things that would have
  stopped it, found and fixed. The world map had no road on from Mandalia, where the campaign puts the party, so
  Eagrose was out of reach after Argath's introduction; now the roads and places ReMixed shows there (on to Eagrose,
  then back to Gariland) are shown too, and the remaster's own take it on from there. The last battle at Fort Zeakden
  had no conditions at all, so it could never end: it has its own now (victory goes on, Algus falling asks whether to
  give up). The scene after it never reached the final battle (it went to a prompt of the remaster's), and ReMixed's
  ending went on to one of the remaster's chapter-2 scenes; now the final battle follows, and after the ending you're
  back on the world map, as ReMixed's post-game rooms end. The build checks all 24 steps now (every hand-off, the 11
  battles' conditions, the 8 world-map starts each reachable by the roads shown by then, the way out to the end of
  chapter 1): 0 wrong. Played in the game as far as 0x00A's battle; the rest is not seen in the game yet (Next up).
- **The side chapter "1-A"** (25 events, 0x0EC–0x0FB and 0x111–0x119: Agrias's story, offered at the end of chapter
  1; all built 2026-11-06). Seen working on the PC: its start (0x0EC: Agrias and Orlandu's scene in the remaster's
  speech boxes with ReMixed's lines, then the world map with the chapter's count at 0, its mark set and Ramza set
  aside in the roster, as ReMixed does), 0x0ED, and its first two battles (0x0EE, Agrias's camp by the river: from
  the world map through the formation, its opening, the fight against two enemy teams and its closing back to the
  world map; then 0x0EF). Its Agrias is the remaster's Agrias (ReMixed made her from Mustadio's sprite and job slot,
  0x22; she's a Holy Knight from the start, as ReMixed's young knight job isn't there), and Mustadio keeps the
  remaster's job (fixed 2026-11-06). Its recruits join once each (fixed 2026-11-06, seen on the PC): Agrias and three
  Lionsguard knights when the chapter starts, Bryn and Lorne in its second scene when Agrias offers them help. Its
  older-build roster repair now runs after a save reaches the world map, rather than only while the title-screen
  roster is resident: Lorne's PS1 Bard job is converted before the party menu opens, the three Lionsguard errand bits
  are cleared, and an old Agrias record is moved off Ashley's slot. That covers the three locked knights and Guest
  Agrias appearing as Ashley on a loaded 0.9.7 save; it's self-tested offline, not yet seen on a loaded save.
  battles went through ReMixed's own "Preparation Menu", which the remaster's formation screen replaces
  (each battle's formations go in its record). Its 16 world-map starts are in, each waiting for the chapter to be
  running, and the remaster's 13 chapter-1 starts wait while it runs (1-A counts its story on the main story's
  progress from 0). The end of chapter 1 offers it in the game, as ReMixed does (below); declining it goes on to the
  remaster's chapter 2, and finishing it (since 2026-11-12, ReMixed's way) back to the world map, the way open to
  Orbonne, where its end plays (0x0FC: Agrias's knights come to guard Ovelia; 0x11A: Whitney, an Ordallian sleeper
  agent, turns on Ovelia and Delita takes her), the roster given back, then chapter 2. Its world-map steps are in
  since then too (the routes and places it shows on its way; 0x0EC hides chapter 1's), so it can be walked. Its scenes on maps no vanilla scenario uses (97, 99, 101) and its VOLCANO
  battle draw now (the art items). Played in order on the PC (2026-11-06), scenes only after the first battle (a
  battle costs about six minutes, so the rest wait for a quicker way to test them): 0x0F0's rescue question answered
  "no", which skips its battle as ReMixed allows (story progress 5), 0x0F2 on to 0x0F3 (progress 7), and 0x0F9
  (Gareth's ghost, progress 0xC), which stood still until the build learned ReMixed's text code (below); 0x0F7's
  battle loads on ReMixed's map 75 (its formation and opening seen). 2026-11-07: 0x0F4 (the cursed armory) played
  from its scene through its question to its battle, which now has a scenario of its own on the armory's map (it and
  0x0FB's battle fight on another map than their scene; analysis/story-flow-plan.md), with its lines in step (ReMixed's
  script had run two together there and in four other events, which shifted every later line by one). 0x0F5's battle
  ended back on the world map without its post-battle scene or the story progress it sets (ReMixed returns to it
  through its after-battle event), so 0x0F5 would have started again; the same for 0x0F0's rescue and 1-A's ending.
  Fixed, and played on the PC the same day from 0x0F5 to the end: 0x0F5's battle into its post-battle scene
  (story progress 9), 0x0F6's choice on into 0x116's battle (its opening seen), 0x0F7's battle on into 0x0F8's and
  their closing scenes (0x117, then 0x0F7; progress 0xB), 0x0F9 (0xC), 0x0FA's battle and 0x118's scene (0xD), and
  0x0FB's scene, question and battle on map 32, 0x119's victory and post-battle scenes, on to the remaster's
  chapter 2 (var 0x6E 0xF, var 0x6F 5). Two fixes on the way (analysis/story-flow-plan.md): a post-battle scene
  that ran its battle's own event again showed the speakers' portraits without their lines, so those runs are
  events of their own now (0x115, 0x112, 0x114, 0x119); and the boxes of events that put the PSP script first
  (0x0F9, 0x0FB, 0x119) and of 0x115 had been fitted to the other script's lines.

What the events need:

- [x] What ReMixed's "location battles" are, found on the PS1's ReMixed run on the Deck (2026-11-06): its
  **missions**, a list of 24 (Chocobo Defense, Chicken Race, Teioh, Lost Heirloom, The Ball, Desert Minefield,
  ... Mirror Match, Be A Beta, Izlude's Last Stand, plus Extra Options and Credits), each unlocked at a point of
  its story and taken on from its text with Start, not offered at world-map locations. Just after the second
  battle none is unlocked: Start at Gariland opens the world map's menu (screenshots of the run). The mod started
  mission i on Start at location i, which gave Lost Heirloom's time mages and thieves at Gariland that early; Start
  no longer starts one in play (the test mode's Start still starts The Ball). The list and what unlocks each:
  docs/analysis/story-flow-plan.md, "ReMixed's missions".
- [x] **ReMixed's missions through a menu** (2026-11-07): ReMixed lists them under Bonus Content in its Brave Story
  menu, which the remaster doesn't have, so they're posted among the tavern Rumors instead, each as ReMixed's story
  unlocks it (Chocobo Defense after Biggs and Wedge in chapter 1, Chicken Race and Treasure Hunt at chapter 2's
  start, Lost Heirloom after the Goug scene, The Ball at chapter 3's start, and so on). Start on a posting accepts
  the mission, as Start on its text does in ReMixed, and the battle begins once you leave town (started inside the
  tavern, the tavern's screens stayed over the battle). Biggs and Wedge's bartender now points at the taverns.
  Rendezvous rules as in ReMixed: no one is lost for good, and Math Skill is removed for the battle. Seen on the PC
  with Lost Heirloom.
- [x] The phases of ReMixed's battle events: its location battles' scenes during and after the battle (the bombs
  waking up, Govis's thanks). The phase is passed in as ReMixed's conditions did, and the bombs battle's closing
  scene runs (seen on the PC).
- [x] The way back to the world map after a location battle. The mod now starts these battles the way the world
  map starts its own story events (it used the replay screen's start before, which sent the battle to a preparation
  screen at its end), and the closing goes back to the world map. Seen on the PC: the bombs battle from the world map
  through Govis's thanks and back; the Lich battle on into Ashley's scene and back.
- [x] Ramza's roster record wiped after Ashley's scene (fixed). The scene lists Ashley as "taken from the roster";
  she isn't in it, so the remaster left an empty unit pointing at roster slot 0 and wrote it over Ramza at the scene's
  end (then crashed on the blank record's missing name). The mod no longer writes an empty unit back into the roster,
  and gives a unit with no name a stand-in name rather than crash. Seen on the PC: Ramza intact after the scene.
- [x] How ReMixed starts its side stories, read: each starts at a world-map location once the story has reached a
  point and some flags are set (24 such starts found), and the remaster keeps the same kind of conditions in a table
  the mod can add rows to.
- [x] The 21 in-between scenes, read: 15 keep the remaster's scene (ReMixed reworded them or added a line or
  two), 2 are systems left to the remaster (the Deep Dungeon's exits, the tavern errands' menus), and 2 carry new
  content that joins ReMixed's own: Beowulf and Reis ambushed by Aliste, and the side chapter 1-A.
- [x] ReMixed's menus in its events: its 32 questions with answers to pick (all in side chapter 1-A: its offer at
  the end of chapter 1, Agrias's answers in its scenes, a three-answer question in its first battle) are the
  remaster's own menus, with the pointing-hand cursor, and the answer takes ReMixed's branch. Seen on the PC
  (2026-11-06): Agrias's first question in 0x0ED (the second answer, then its branch of the scene), and the offer
  at the end of chapter 1 both ways: "Continue to Chapter Two" goes on to the remaster's chapter 2, "Play Chapter 1-A"
  into Agrias's story. The offer shows a little differently from ReMixed: its two boxes were placed on the screen with
  no character, which the remaster doesn't draw, so it now comes over the chapter's opening scene, its explanation
  first as a narration, then the answers in a box at Orlandu. The same fix of the text put the Lich's lines back with
  their speakers (they had shifted by one).
- [x] ReMixed's text code that sets an event variable as a line reaches it (F5 66 F6 a F5 b F6 v: variable 10a + b
  becomes v), which four shipped events wait for with vanilla's 0x7E to time a gesture to the words (0x0F4, 0x0F9,
  0x118, and 0x119, the end of 1-A on to chapter 2). The remaster's text has nothing like it, so 0x0F9 stood still on
  the PC with its codes showing as "241". The build drops the codes from the lines and makes those waits set the
  variable (2026-11-06): 0x0F9 seen going through, Gareth beckoning as his line opens.
- [x] What follows a mission in ReMixed: the Treasure Wheel, a spin for a prize after each win (twelve chests in a
  circle, ten loot sets, gil or JP for what's left). A won mission hands on to it, and it goes back to the world map
  once its tries are spent or cashed in (2026-11-12). Seen on the PC: Lost Heirloom won, then the Wheel, its ring
  turned, chests opened to their prizes and the rest cashed in for gil, then the world map, with the keyboard and the
  pad, its lines in the remaster's speech boxes, centred, where ReMixed has plain lines at the top and bottom of the
  screen. Since then the boxes are fitted to their text (no room for a portrait, as wide and as high as their lines):
  seen on the PC in the regression pass (2026-11-12), with the chests in their colours and a prize and the cash-in.
- [x] The Treasure Wheel answers at once, as on the PS1: its top line ("N left. / Start: Cash In") and its bottom
  line (the chest in front's kind) are ReMixed's frameless lines, which the event goes on past without a press; in
  the port's speech boxes each waited for one, so the ring answered only after two presses (the regression pass,
  2026-11-12). Their boxes now go on as if pressed and stay up. Seen on the PC the same day: both lines up as the
  Wheel opens, Right and Left turning the ring, one F opening the chest in front to its prize, the prize named along
  the bottom once read, the cash-in, the world map.
- [x] The Treasure Wheel's cash-in on a pad (2026-11-15): the port accepts Triangle, the Wheel's existing paid-upgrade
  button, as cash-in too. Start remains the remaster's Pause/scene-skip button, so the event no longer competes for it;
  Space on the keyboard still cashes in. Not yet exercised on the Deck. *medium*
- [x] The rounds of the missions fought in rounds (2026-11-07): each round's battle conditions start the next
  round's event, which the mod didn't have, so The Shrine Knights and Brave Story stopped after their first round.
  Seen on the PC: The Shrine Knights' four rounds, the scene between the first two, and back to the world map.
- [x] Brave Story's first round didn't end once its enemies were down (seen on the PC, 2026-11-07 and 2026-11-12;
  after the enemies fell it eventually crashed on a target with no sprite). Fixed 2026-11-15: ReMixed's type-0x16
  condition now checks all 21 battle slots itself and succeeds only when no live enemy remains, including the event-
  made Death Knights and monsters the remaster's checker missed. The same condition ends every later round; their
  scenario records and presets form the ten-round chain, so their openings arrive from the preceding round rather
  than when a later round is started alone. Self-tested offline; the complete chain still needs an in-game pass.
- [x] Be A Beta, the mission with ReMixed's beta testers (2026-11-07): posted with Chocobo Defense, after Biggs and
  Wedge in chapter 1. Its testers have their names (ArmoredKori, NinjaWeazel, Caazan, Akashachi, OronJoker and the
  rest), which its event writes as the battle starts; when ArmoredKori falls, Banana Phone takes his place, then
  the chocobos one after another. Seen on the PC: the names, the swap, and the battle to its end. Caazan is built
  from NinjaWeazel as in ReMixed, but without NinjaWeazel's learned abilities, which the remaster keeps otherwise.
  Izlude's Last Stand, listed in ReMixed, is empty there, so there's nothing to port.
- [x] Mirror Match, Gogo copying your party (2026-11-12): posted late in chapter 4 with An Ill Wind and Of Vagrants.
  Famed Mimic Gogo's five Mimes stand where your units will be mirrored, copies of your party take their places
  (each one's job, equipment, abilities, stats and name), and every win makes the next copies stronger; the victory
  goes on to the Treasure Wheel. Its event copies the units by the PS1's layout, so the code mod copies them in the
  remaster's (self-tested), and what it read of "the party's copy" was the PS1's snapshot of their statuses, which
  the remaster's units hold as they are. Gogo is a Mime in ReMixed too (its Mime sprite is vanilla's), so the
  remaster's Mime is his look. Not seen in the game yet.
- [x] The missions ReMixed unlocks after its final battle (2026-11-07): the remaster's story never runs that battle,
  so they're posted late in chapter 4 instead, as asked: from the party's last stretch on the world map, after
  Mullonde's battles (where All-Star Melee is posted too). Posted there so far: Of Vagrants and An Ill Wind, both
  played to their end on the PC (Of Vagrants from its posting; An Ill Wind's units draw as broken stand-ins, the art
  port). Start on the last posting of the scrolled list took that one.
- [ ] The Guarded Temple and Nightmares, before they're posted late in chapter 4: their battles end through scripted
  conditions on particular units and a turn counter (Nightmares' demons fall in order), which the test's defeat-
  everyone shortcut can't meet, so they need playing through for real. The Guarded Temple ended three ways in three
  tries (once on an empty speech bubble from an Automaton). Brave Story's plain-victory condition is fixed above,
  and its first three rounds hand on in the game (below); its later rounds still need the same pass. *medium*
- [x] A ReMixed battle's scripted failure (2026-11-07): ReMixed sends it to its own game-over menu (Retry Battle,
  Return to Title Screen, Return to World Map), which the mod doesn't have, so the battle went on with its enemies
  down and its closing ran again and again (seen on the PC with The Guarded Temple). It now goes back to the world
  map, ReMixed's third choice. In Chocobo Defense, Treasure Hunt, Desert Minefield, The Guarded Temple, the Algus
  campaign's first battle and side chapter 1-A's last.
- [ ] Balthier's remaining side-event start: his 0x098 battle is shipped at Dorter with ReMixed's proven world-map
  conditions (flag 0xB7 set, 0xB8 clear), but nothing in ReMixed's events sets 0xB7; it is probably a dispatch-tavern
  reward. The other shipped side events' starts and hand-offs are mapped, and the apparent exceptions were resolved
  as mission/preset starts or events with no start in ReMixed itself. Do not invent a remaster trigger until the
  source of 0xB7 is proven. *small*
- [x] ReMixed's "create a unit" instruction (0xE0), which 9 of its own events use to make their units (Biggs and
  Wedge, Zalbag and Algus's, 0x11B), and its random battles' setup on four maps: ReMixed's record building ported
  as it stands (from the parameters, a roster slot or another unit), the unit made by the remaster's own three
  calls, as its battle setup makes each unit (`Patches/CreateUnit.cs`, self-tested). Seen on the PC (2026-11-06):
  Biggs and Wedge's scene at Gariland (0x0A4), its six units made and shown, named, with their lines, to its end.
  Its 0.9.2 Nexus regression is fixed (2026-11-16): the imported carrier's unique unsupported map-state `0xF0`
  produced the blue/black sky instead of map 96, and returning with Mandalia still in location var `0x31` chained
  the just-unlocked Luso scene. It now uses state 0 and commits Gariland (6) before the world-map hand-off; generated
  checks audit every imported map-state and the exact safe exit sequence.
  Not made: a record that loads a formation (flag 0x10), which the remaster's unit making skips; the random battles'
  setup asks for one on maps 0x0A, 0x0B, 0x0D and 0x10, which nothing made before either. *large*
- [x] ReMixed's instructions 0x2F (Luso's events) and 0xE8 (Balthier's), 2026-11-12: 0x2F loops a unit's gesture in a
  scene while a variable is set (Luso's goblins dancing, and four other events), 0xE8 and 0xE9 read and write a map
  tile's byte (Balthier's scene at Dorter reads one). Ported and self-tested offline; to see in the game once those
  events ship.
- [x] ReMixed's own battle conditions (2026-11-12): its events wait on conditions the remaster doesn't have (a unit's
  status, a team reaching a tile, a unit's turns taken), so 1-A's rescue never noticed Hirame revived or lost,
  Treasure Hunt's goal tile didn't end it, and the VOLCANO battle's lines on its units' turns never came. They work
  now. Seen on the PC: Hirame revived runs her scene, the rescue ends on the branch where she lives, and Treasure
  Hunt ends when a unit reaches its chest.
- [x] 1-A's rescue went the way where Hirame is lost when she was revived (seen on the PC four times, 2026-11-12
  evening: "Nay...", Hirame turning to crystal, "Curse it! We... we were too late...", then an empty speech box again
  and again). Found 2026-11-13: ReMixed's battles run one event for all their in-battle scenes, each picked by the
  number of the condition set that runs it, and the Nemesis's return (added that afternoon) had put its set second in
  the conditions of 1-A's battles and Luso's at Mandalia, so every later set ran the scene after its own: Hirame
  revived ran the victory's (where she's lost), and the victory's scene ran again and again. Its set is the last one
  now, still checked first after the opening. Seen on the PC (2026-11-13), beside the PS1: revived, her "U-ugh...
  What... what has transpired?" and Agrias's three lines on the battle map, then after the birds all 17 lines of the
  scene where she lives ("'Tis a blessing you live..." to "Very well. If your mind is resolved."), then the world
  map. Agrias's five in-battle boxes now explicitly select her ReMixed event portrait, since her unit is off the
  field (built and checked in the generated events 2026-11-24; the revival branch still needs a visual PC capture).
  A line with no speaker's name still shows an empty name tab over its box, where the PS1 shows none.
- [x] ReMixed's lines inside a battle came up as empty speech boxes (seen on the PC, 2026-11-12, evening: Algus's
  "Reinforcements? Thank God!" in the Argath campaign's second battle, a face and no text; the scene after 1-A's
  rescue over and over; Luso's lines after his battle at Mandalia). Found 2026-11-13: the remaster lets go of a
  scene's text when it ends but keeps it marked as loaded, so the same scene run again had no text, and ReMixed's
  battles run the same scene again for each of their moments. A scene run again is loaded afresh now. Seen on the PC
  in 1-A's rescue (2026-11-13: its battle's run and its victory's run load afresh, and every line after shows its
  text); Algus's and Luso's lines not seen yet (Testing).
- [x] Luso's post-battle return flag stayed set after his Mandalia and Araguay Woods scenes (reported 2026-11-15):
  its clear was generated after the world-map hand-off, so the early encounter repeated before Siedge Weald and
  Araguay remained a story mission after Boko joined. The clear now runs before the hand-off. Built and checked
  offline; not seen in the game yet. *small*
- [x] Araguay Woods' Agrias and Gaffgarion became generic actors with the same wrong portrait (seen on the Deck,
  2026-11-15): the regression save rewound the story without restoring their chapter-two guest-roster records, while
  ReMixed's 0x01D entry asks the remaster to load both from guest slots 50-53. At story progress 0x10 only, either
  missing guest is now made from its complete entry data; a normal chapter-two save still loads and preserves its
  roster copy. The two-missing and one-missing paths are self-tested offline. *small*
- [x] Araguay Woods could immediately lose and return to the world map when Boko was absent (reported 2026-11-17).
  Its roster-backed victory-condition actor kept battle id 0x80 even when no unit had been made, and the stock HP
  comparison treated that empty record as a dead actor. ReMixed's named-actor condition now reports a missing actor
  as absent instead of delegating the zeroed record to the stock comparison. Covered by the offline condition
  regression; the natural chapter-two route has not yet been played end to end. This is a different case from the
  reported early victory (Open, below), whose line comes from the victory's block, not from those HP checks. *small*
- [x] The chapter-3 branch (0x104 at location 15, where Hirame meets Agrias again) opened the wrong way round: when
  Hirame was lost in 1-A's rescue, not when she was saved (so in release 0.9.0). Read 2026-11-13: ReMixed's own
  start for it wants both its "Hirame lost" flag and its branch flag, and it sets the branch flag (in chapter 3) only
  while Hirame isn't lost, so ReMixed itself never plays the branch; its scenes are for Hirame saved ("Thanks for
  saving me yet again"). Now reviving her in the rescue opens it, and it starts at location 15 from chapter 3 if she
  wasn't lost, once. Declining the rescue or skipping 1-A leaves it shut, and so does 1-A played on an earlier
  build. Checked offline; not seen in the game yet. *small*
- [x] The chapter-3 branch had no tavern lead: ReMixed's rumor about dangerous squatters at Fort Zeakden was not
  shipped. Its original rumor is now in the remaster's Rumors while the branch is open (chapter 3 on, Hirame saved,
  before the branch is taken), distinct from the "An Ill Wind" mission posting. Checked offline; not seen in the game
  yet. *small*
- [ ] Black figures stand around the Argath campaign's start in the tunnel, in its scene and its battle (seen on the
  PC, 2026-11-12), where ReMixed on the PS1 shows none (and its wagon, which the port leaves out). *small*
- [x] The Argath campaign's battles won't commence without Ramza (seen on the PC, 2026-11-12, late, at 0x00A's
  formation screen, in a run from a new game): the campaign keeps Ramza out, as ReMixed does, with Algus leading and
  two knights placed, and the remaster answers Commence with "Ramza is required but has not been placed." The earlier
  test of the same battle commenced because it started from an ordinary save with Ramza placed. Every later battle
  of the campaign waits on this. Tried offline (2026-11-12, late; not seen in the game yet): side chapter 1-A also
  keeps Ramza out of its battles and they commence; it sets him aside with another mark than the campaign's (bit 0x08,
  where the campaign's battles write 3), so the campaign's battles now write both. Where the remaster's rule is hasn't
  been found. If the formation still asks for Ramza, the next thing to look at is his roster slot (1-A's Ramza is in
  slot 19, the campaign's in slot 10). Seen on the PC with that change (2026-11-13, the spot check, the campaign
  started from the test save): Ramza's mark 0x0B, Algus's 0x10; with three places filled Commence still answers
  "Ramza is required but has not been placed.", and with the fourth filled it asks "Commence battle?" (the battle
  itself not seen). Tried again three times the same day with Ramza's mark 3: the question that comes up as the
  last place fills couldn't be answered (Enter, then F, then F 8 s after it opened), and Commence asked for with every
  place filled answers "Ramza is required but has not been placed." too. So filling every place doesn't get round
  it: the remaster won't begin the campaign's battles without Ramza. Left: its rule itself (in its own formation
  screen, not yet found), and how 1-A's battles, which keep him out too, pass it. Found offline (2026-11-13; built,
  not seen in the game yet): the unit the remaster wants placed is the one in roster slot 0 (it calls it Ramza;
  1-A's Agrias is there, listed, and its battles commence), and the campaign's Algus there was never listed. ReMixed's
  own start (0x0FD) writes 0x10 into his roster mark as it swaps him in, a bit that isn't "away" in ReMixed (only the
  mark's low four bits are), but the remaster leaves out of its list any unit whose mark is set at all; 0x0FE does the
  same to Zalbag (0xE0). Nothing in the campaign reads those bits back, so the port now leaves them out: Algus and
  Zalbag can be placed, and Ramza and Delita stay kept out as before. Seen on the PC (2026-11-13, the spot check, the
  campaign started afresh from the test save): Algus's mark 0, Algus listed and placed at 0x00A's formation, and
  Commence (Space, then Yes) begins the battle. *medium*
- [x] Side chapter 1-A's battles after their scenes (2026-11-12): ReMixed plays a battle's scene first (its question
  to fight, if any), then the formation, then the battle, set up afresh; the port had the formation first and the
  scene inside the battle, which left the scene's actors on the field (Agrias's double fought the rescue as a guest).
  Now the scene runs first and the battle is set up after it, as on the PS1, in eight of 1-A's battles. Seen on the
  PC with 0x0F0's rescue and 0x0F1's battle, start to finish (no more black scenes inside the battle, no crash on its
  first turn). A scene answered "no" and started again plays again rather than going straight to the battle. Its
  Agrias is the remaster's, under another number: the conditions on her are built to find her again (her turn in
  0x0F1 not seen yet; under Next up). ReMixed's second formation group (Bryn and Lorne's) is on since 2026-11-19
  (Open: "Only four places in 1-A's battles").

## Next up

In the order I'd take them:

1. **The side chapter "1-A" in the game** (27 events built, 23 seen working): walked from its start on the world map
   (its steps shown as it goes, built 2026-11-12, not seen) to its end at Orbonne (0x0FC, 0x11A, then chapter 2 with
   the roster given back; not seen); inside battles not
   yet played through, now with the scene first and the battle after it: Agrias's lines on her turn in 0x0F1's
   battle (0x114: not seen, as the test mode's AI played her turn; by hand), 0x116's battle after 0x0F6, 0x0FA's
   battle in the new order, and the second formation group (Bryn and Lorne's), which won't commence yet. Seen again
   in the new order in the regression pass (2026-11-12): 0x0EE, 0x0EF, 0x0F5 and 0x0F7 on into 0x0F8, each scene,
   battle, closing and the world map (their fights ended at once there; 0x0F8's lines on turns taken came up).
2. **The Argath campaign in the game** (24 events, 6 seen working, 18 left): ported to its end and walked step by
   step by the build without the game (2026-11-12, 0 wrong). Played on the PC from its start through 0x00A's battle
   (2026-11-06), each scenario started by hand. Left to see, in one run from a new game with the setting on: the
   roads on the world map from Mandalia on (built 2026-11-12), the rest of its chapter 1 (0x00B to 0x013, four
   battles: three with the scene first, one with two groups), the Hokuten's side (0x106, 0x0FE, then three battles at
   location 15 with two groups each, six units deployed), the last battle at Fort Zeakden both ways (won: the final
   battle; Algus falls and gives up: the remaster's end of chapter 1 with the roster given back), the final battle and
   the ending, back on the world map. Tried from a new game twice (2026-11-12, evening): with the crash fixed it plays
   on through 0x006's battle and Dycedarg's scenes at Eagrose, then stopped at 0x00A's formation screen, where the
   remaster won't begin it without Ramza, even with every place filled (2026-11-13; Story port progress). Fixed
   the same day (Algus in roster slot 0 was never listed, so the slot the remaster wants placed couldn't be), and seen
   on the PC: with Algus placed that battle begins. Next, the rest of the campaign from there.
3. **Battles with the test mode on** (see Testing and [TESTING.md](TESTING.md)), then **the display, text and art
   port** (below).
4. **Dispatch Missions on the Deck** (native-UI redesign asked 2026-11-17; plan/history in
   [analysis/dispatch-missions-plan.md](analysis/dispatch-missions-plan.md)). Since 2026-11-18 it replaces the
   Tavern's Errands, as ReMixed's replaces vanilla's errands: the Tavern menu is Rumors and Dispatch Missions, the
   button locked ("???") until ReMixed posts Dispatch Missions, and the remaster's 96 errands can't be started. Its
   PSX menu flow uses the remaster's own list, roster picker, timers, cancellation and reports: main menu, six
   Proposition kinds, the current unfinished set, Jobs, Special Training, Contests and Recall; its category pages
   have no Days or Finder's Fee columns. Seen in the hidden PC game (`tests/quick/dispatch-button.txt`): the button,
   the category pages, an empty mission page, a group sent with the confirm button alone and recalled (2026-11-19)
   and the locked button. Left to verify: three daily steps, reporting and returning to the Tavern.

## Open

Everything else ReMixed's gameplay changes is ported or settled (Done), or waits for the display, text and art port
or the story port (below). The formulas ReMixed rewrote in place were ported on 2026-11-12 (Done; to see in a
battle, Testing).

- [ ] The lock-up a tester still had in 1-A's last battle on 0.9.35 (2026-11-23) is fixed for the way that could be
  found, a battle continued from an older build's autosave (below). A battle begun anew did not hang on the PC in any
  way tried. Still to hear from him: whether he pressed Continue or began the battle again, what the screen showed,
  and his log from the Deck. *high*
- [x] 1-A's last battle, continued from an autosave made before 0.9.35, still went wrong on 0.9.35 (a tester,
  2026-11-23: "still locked up here"). Three faults, each seen in the hidden PC game (2026-11-25) and fixed:
  - A save from 0.9.33 or earlier carries no note of which battle it is. With a Nemesis stored, the last event run
    in the battle is the Nemesis's return, which has no conditions of its own, so the continued battle had no failure
    and no victory: it went on for good, with no enemy left. A battle continued without that note is now told from
    its map and its units, and gets its own conditions back. This covers every ReMixed battle that begins under its
    own number (48 of them; two later phases that run under another battle's opening are taken for that battle), and
    the build stops if two battles could be taken for each other. The System Menu's "Retry from Start of Battle"
    reloads the battle's first turn the same way, so on such a save it had the same fault.
  - A save from before 0.9.35 keeps the faded-out picture the scene before the battle left, so each scene inside the
    continued battle still played over black, only its speech box showing. The first event of a continued battle now
    fades the picture in.
  - Once a fallen child's body had turned to a crystal or a treasure chest, the remaster no longer counted the child
    as fallen. A battle that got its conditions back in that state, with no enemy left, went to its victory scene.
    A unit whose body is gone counts as fallen, as in ReMixed.

  Tried on the PC as a player meets the battle, and working on 0.9.35 as released: from its scene, with Agrias in
  the party and a Nemesis returning, a child killed mid-turn by a goblin; the failure with the party standing on
  Dorter (the world map plays the scene again by itself five seconds later, and that second battle fails and ends
  properly too); both of the System Menu's retries; a battle saved and continued on the same build. Not tried: the
  Steam Deck itself, and a child killed by Choco Meteor or while riding. `tests/quick/dorter-old-save.txt`, then
  `dorter-old-save-load.txt`. *high*
- [x] Side chapter 1-A's two volcano battles (reported on the Deck, 2026-11-25), two empty boxes:
  Agrias's closing line after the first battle ("We ought to seek out a more secluded venue.") came up as an empty
  box with her face. ReMixed works that line's number out twice, and a leftover step made the second pass add the
  translation's offset again, so the box asked for a line past the scene's last. ReMixed's own game hits the same slip
  only with its newer translation switched on; the port always shows that translation. Fixed in the build: the
  leftover step is dropped, and the dialogue check stops on any event that has one (this was the only one in
  ReMixed's events). Seen in the hidden PC game: her line with her name and face after the battle.
  The empty box as the dark-Agrias battle fades in: the doppelganger's first turn comes right after the fade-in, and
  her scene there ends with Elias's reply ("..."). With Elias not deployed (ReMixed doesn't require him) no unit
  answers to it, so the box had his name, no face, no tail and only the dots; ReMixed on the PS1 has the same gap. A
  line of a party member who isn't on the field (Bryn, Valmafra, Lorne or Elias, in any of 1-A's battles) now takes
  the face of the battle's off-field actor of that person, where it has one (your call, 2026-11-25). Seen in the
  hidden PC game: Elias's reply with his Black Mage face (`tests/quick/bervenia-elias-stand-in.txt`). The music in
  that battle is ReMixed's own track 99, still to port (the music item below); the remaster's track 99 plays in its
  place. The harness's formation helper now fills them (2026-11-27: it backs out of the next tile's unit list when
  "Commence battle?" doesn't come up). *medium*
- [x] The scene after the dark-Agrias battle on the volcano went wrong (a player on 0.9.40, 2026-11-27: "Valmafra and
  Agrias had Ramza's portraits, a random Ramza looking over in the corner, then it soft-locked"). Since 0.9.32 that
  closing scene shared its event number with the dispatch tavern's scene, so it ran on the tavern's setup and brought
  the tavern's stand-in Ramzas onto the volcano in Agrias's, Valmafra's and the Wizard's places. It has a number of its
  own now, and the build stops if two of ReMixed's events ever share one (no other pair did). Seen in the hidden PC
  game: the closing with Agrias, Valmafra and the Wizard, each with their own face, through its last line to the world
  map (`tests/quick/bervenia-dark-agrias-closing.txt`). *small*
- [x] Valmafra's unit card showed no primary skillset (seen on the Deck, 2026-11-25): her Arc Witch's Spell Strike
  had its name but no icon, and the remaster leaves a skillset with no icon off the card. All 21 skillsets ReMixed
  names that the remaster had left empty had the same gap (Blue Magic, Witchcraft, Combat Skill, Sorcery, Fellblade,
  its bosses' lists and others), on a unit's primary or secondary row alike. Each gets the icon the remaster gives the
  same kind of command now: its special jobs' one, or its boss monsters' where the job had one before (the Reaver,
  the Archaeodaemon, the Ultima Demon, the Lamia). Checked offline (`tools/test_name_tables.py` and the built table)
  and seen on the PC (`tests/quick/skillset-icons-card.txt`): Blue Magic, Spell Strike and the Reaver's Bio list on
  three units' cards, each with its icon. Valmafra's own card not seen yet (the fixed table is on the Deck).

From the testers' 0.9.17 playthrough (2026-11-18, evening: chapter 2 cleared with no crashes, then side chapter 1-A;
the Lionel scene's event art and Two Shots have sessions of their own):

- [x] The dragon hunters' scene at the Gollund colliery's first floor ended after four lines and put the party back
  on the map, with Beowulf a guest in the party (a tester on 0.9.39, 2026-11-25). ReMixed's scene asks whether
  Beowulf is in the party and took its "he's not here now" way out, which also takes him off the field. The
  upgrade's "is this character in the party" test, and its lookup of a party member, looked only at the first 20
  places, which is the PS1's whole party; the remaster keeps guests in the last four of its 54, so a guest was never
  seen, and neither was anyone past the 20th place of a big party. Fixed in the upgrade (its 1.0.2): both look
  through all 54 places, as does the search that makes a battle unit from a party member, and an event can read and
  change a place past the 20th like any other. 79 of ReMixed's events ask one of the two. 42 of them run in the
  port: 41 story events and the random battles' setup, which asks about seven characters. Each was open to it
  whenever the one asked about was a guest or sat past the 20th place: Beowulf and Reis all through their quest,
  Zalbag at Eagrose, Agrias and her knights, Luso, Ashley, the Argath campaign's people among them. The three that
  only ask about Ramza never were (he is always first). Seen in the hidden PC game: on 0.9.39 the scene left for
  the world map; on the fix Beowulf answers the hunters ("I've been searching for the Holy Dragon for too long"),
  they know the heretics, and the battle starts with him on the field
  (`tests/quick/gollund-hunters-guest-beowulf.txt`). Not changed: the same test can also ask about ReMixed's units
  away on its dispatch missions, a list of its own that the port doesn't keep. *medium*
- [x] A save's own Extra Options didn't hold for a battle continued from its autosave, nor after either Retry in
  the System Menu: the mod's settings were in effect until the world map came back (a tester on 0.9.39: Mustadio
  turned to a crystal and left the party with Player Deaths on "Revive after battle", and JP came at 125% for the
  save's 200%). The game sets such a battle up before it puts the save's variables back, so the mod's check at the
  setup found no options there, took its own settings and kept them. The save itself was never changed: the game
  puts its variables back over what the check wrote (read back as the save's own every time), so no save needs
  repairing. Now the check at the setup writes nothing and leaves the options as they are, and they are read again
  as the battle's first turn comes, when the save's are there. Seen in the hidden PC game all three ways
  (Continue, "Retry from Start of Battle", "Change Formation and Retry"): the mod's settings on 0.9.39, the save's
  own on the fix (`tests/quick/options-continued-save.txt`, then `options-continued-load.txt`). One case is left
  as it is: a new game begun right after playing another save, without closing the game, keeps that save's options
  until its first battle's first turn. *medium*
- [x] Rapha's "Guest Departure" card showed Balthier's face when she left as a crystal (the same tester, on
  0.9.39). For a guest it can't find the usual way, the game names the card's face by a number in its own code,
  her old one, which is ReMixed's Balthier in the port; that number is what the card shows once her body is gone
  from the field. The mod puts her own number there. Seen in the hidden PC game: Balthier's face on her card on
  0.9.39, her own on the fix (`tests/quick/riovanes-gate-end-real-crystal.txt`). The test takes her sprite away at
  the moment the game looks her up, because the harness's copy of that battle doesn't play turns: it has not been
  played through with a body counted down to a crystal. *small*
- [x] Zalbag's mount at Eagrose Castle's gate was a black shape (a tester on 0.9.39, 2026-11-26: "looks like a
  missing sprite"). The mount is ReMixed's White Chocobo: the ordinary chocobo drawn in a fourth set of colours
  that ReMixed added to the chocobo's art. The port didn't ship that set, and the remaster's own fourth set is
  empty, so every White Chocobo drew black, wherever one appears. ReMixed's art was checked sheet by sheet for the
  same thing: one other has it, Zalbag's own, with two more outfits' colours. Both sets ship now, on the
  remaster's redrawn art (the pictures themselves are unchanged), with a face for each from ReMixed's portrait.
  Seen in the hidden PC game: the black shape on 0.9.39, the white bird on the fix
  (`tests/quick/eagrose-gate-arrival.txt`). Not seen: Zalbag in his other colours, and a White Chocobo's face on
  a unit card. *small*
- [ ] Eagrose Castle's gate stands still after Ramza uses the switch, and he walks in through it (the same
  tester, same scene; seen in the hidden PC game: the portcullis down as he walks into it). Looked into
  (2026-11-26), not fixed. It isn't ReMixed's script: with the remaster's own script for this scene put in its
  place the gate stayed down just the same, and so it did with the game's own table of map movements instead of
  the mod's. The remaster's script has nothing in it that moves the redrawn gate, where its other gate scenes
  (Lionel's among them) do carry an instruction for that. So either the remaster's own scene leaves this gate
  down too, or something in how the port sets the scene up stops it. Next: watch this scene in the game without
  the mod (Eagrose Castle at the start of chapter 4's ending, a save just before it). If the gate rises there,
  compare how the two scenes are set up; if not, it is the remaster's own and stays. Found on the way: ReMixed's
  scenes at Lionel's gate (Beowulf and Reis's story) and one in the Argath campaign retell scenes where the
  remaster does move a part of the map, with an instruction ReMixed's scenes can't carry yet; not seen in the
  game, and likely still as well. *medium*
- [ ] ReMixed's choice before the duel with Wiegraf inside Riovanes Castle ("Reject duel" or "Accept duel", so the
  fight needn't be fought alone) isn't in the port: the remaster's scene and its solo fight play (the same tester
  remembered it rightly). Looked into (2026-11-26), not built: it is story-port work, bigger than the three fixes
  above together. ReMixed asks after Wiegraf's "Draw your sword". Refused, Ramza tells him to change into his true
  self, Wiegraf answers ("Very well. If you're so eager to die, I'll oblige you."), Belias and his demons come in at
  once, and the party, which never left the field, fights from the start. Accepted, everything goes on as in the
  remaster. By the port's rules the remaster's scene, lines and voices stay, and only the choice and the two new
  lines come over with ReMixed's text. What that takes:
  - The remaster's opening scene of that battle has to become one the upgrade runs, since only those can ask a
    question and branch. No scene of the remaster's own has been converted that way yet.
  - Three new lines (the choice, Ramza's refusal, Wiegraf's answer) added to that scene's text, without voices.
  - The refusal's staging written in the remaster's terms: what its "Wiegraf changes" scene does from Belias's
    arrival on, without the party walking in again.
  - The battle's own conditions told that the duel is over, so the mid-duel talk and the change don't fire later
    (ReMixed sets two of its variables for this; the remaster's conditions for this battle haven't been read yet).
  - Both answers played in the hidden PC game, and frames checked beside ReMixed on the PS1. *medium*
- [ ] Left from the same tester's 1-A (2026-11-21 and 2026-11-22), each with what was found (2026-11-25):
  - The dream's knight, reported with no sprite and no portrait on the Deck: on the PC he draws, under the name
    "Knight" with a knight's own face, not Orlandeau's (the dream is played on the way in
    `tests/quick/side-chapter-roads.txt`). Not seen on a Deck.
  - Agrias "walking in place while Valmafra and Elias talk on": the scene is the chapel at Riovanes, after the
    Yuguewood. She stands at the foot of the aisle seen from behind, in poses of ReMixed's own with her feet apart
    as in mid-stride, and holds each while the others speak. Seen on the PC both ways through the scene (her first
    answer, and her second, where Elias speaks, three frames in a row at seven of its lines): she never moves
    between frames. Beside ReMixed on the PS1 the staging is the same, so this is ReMixed's own, plainer to see at
    the remaster's size; nothing to fix unless the tester saw her legs actually moving on the Deck.
  - The "vessel" Valmafra seemed to draw water from, possibly missing: it is a figure in a white hooded cloak (the
    game's priest) beside the altar's candles in that chapel. On the PS1 it is bright white. On the PC it is
    there, but so dark a blue-grey that it is nearly lost against the wall. Its art and its colours are the right
    ones (the remaster's own priest, first colour set), and no instruction of the scene colours it; what darkens
    it isn't found. Still to try: the same figure on another tile or another map, to tell the map's lighting from
    the way the unit is drawn.
  - The 1-A Agrias, Alicia and Lavian kept beside chapter 2's new ones: a design choice to settle before building.
    ReMixed turns the 1-A three into chapter 2's guests; the remaster's chapter 2 makes its own at its opening
    (two Lv 8 knights who join, a Lv 10 guest Agrias), so a player has both. What each way would take is written
    up for the choice (keep the levelled three, or let them leave as 1-A ends).

  *medium*
- [x] The road from the Zeklaus Desert to Dorter was drawn grey in side chapter 1-A, though the party walks it (a
  tester, 2026-11-22), and the objective's diamond stood on Dorter all through the chapter, whatever its objective line
  said. The remaster keeps its own count of the story, which only its own scenes move on. From it the world map takes
  the place the story is headed for (the diamond), the next stop on the way, and the roads it draws grey because the
  story hasn't opened them yet. 1-A is offered where chapter 1 ends, and none of ReMixed's scenes moves that count, so
  for the whole chapter the map showed chapter 2's first step: Dorter as the goal, and the roads beyond it (to Sweegy
  Woods and to the Zeklaus Desert) grey. Now, while one of ReMixed's own stories runs (1-A, the Argath campaign), no
  road is greyed, and in 1-A the diamond stands on the place its objective line names (the Argath campaign has no
  list of destinations yet, so its map shows no diamond rather than a wrong one). The same count was also left behind
  in the main story wherever a ReMixed scene stands in for the remaster's: after Araguay Woods the road on to
  Zeirchele Falls stayed grey, with the next-stop mark still on the woods, until the falls. There the count now
  follows the story. The build lists every scene of the remaster's that moved the count and is now ReMixed's (eight)
  and stops on a new one. Seen in the hidden PC game (2026-11-25): on Mount Bervenia 1-A's roads are drawn as a player
  meets them, the Zeklaus road grey with the fix switched off; with it on, after the dream there, both roads are in
  colour and the diamond is on the Zeklaus Desert under "Travel to the Zeklaus Desert."
  (`tests/quick/side-chapter-roads.txt`); and Araguay's battle, begun at the count Dorter leaves, ends on the world
  map with the count where the remaster's own scenes would leave it (`tests/quick/araguay-battle.txt`; the test
  save has none of chapter 2's roads to show, so that one is seen in the count, not on the map). *medium*
- [x] ReMixed's event art looked blocky beside the remaster's sprites (a tester, 2026-11-21). The poses ReMixed
  drew itself (a character kneeling, carrying someone, turned away) ship at twice their size for the enhanced
  sprites, and only one of the eleven pictures had been smoothed the way ReMixed's unit sprites are (0.9.27's); the
  rest were plain doubled pixels. All the pictures of people are smoothed now (nine more, used in Luso's scenes,
  all through side chapter 1-A, in the Argath campaign and in Beowulf's arc). The Treasure Wheel's chests and Reveal
  Treasures' arrows stay as they were seen beside the PS1. Seen in the hidden PC game (2026-11-25): the dream's
  knight carrying the child, before and after, in `tests/quick/side-chapter-roads.txt`'s frame of him. *small*
- [ ] After Eagrose in chapter 4 the world map's objective line stays "Meet with Dycedarg at Eagrose Castle." until
  Mullonde (found 2026-11-25, reading the scenes; not seen in the game). ReMixed's Eagrose scenes stand in for the
  remaster's two that moved its count of the story on to "Rescue Alma from Mullonde Cathedral.", and the story's own
  progress doesn't change between the two, so the count can't follow it as it does after Araguay: it has to be set as
  ReMixed's Eagrose ends. No road is greyed by it. The other scenes whose count is left behind change nothing a player
  sees (the build names each, with why). *small*
- [x] 1-A's last battle hung on a black screen when a child fell (a tester on the Deck, 2026-11-22: a red chocobo's
  Choco Meteor, then nothing answered, fast-forward included). ReMixed's event for the fall names the fallen child as
  the speaker of its cry one instruction too early, so it overwrote the next instruction instead. The PS1's script
  happens to fall back into step; the port's ran to its end without loading the failure, the fall started it again,
  and so on for good (seen in the hidden PC game: the event loaded afresh every 16 seconds). The child now speaks its
  line and the failure goes back to the world map (ReMixed's own game-over menu isn't ported; this is its third
  choice). Every such write in ReMixed's events was checked: this was the only one out of place, and the build now
  stops on another. Seen in the hidden PC game (`tests/quick/dorter-child-falls.txt`): the first child downed, "Child:
  M-mother...", and the world map 14 seconds later. *high*
- [x] The scenes inside 1-A's last battle played over a black screen (a tester, 2026-11-22; left open in 0.9.34). The
  scene before the battle fades the picture out, and ReMixed's Preparation Menu fades it back in; the port's battles
  that have a scenario of their own do that themselves as they start, but only Yardrow's and Mount Bervenia's had been
  given it. The battle's turns still drew, so it only showed when a scene started inside the battle. All four such
  battles fade in now (Luso's at Mandalia has its own), and the build checks each. Seen in the hidden PC game
  (`tests/quick/dorter-child-falls.txt`): the boy's cry over the map, the camera on him. *medium*
- [x] A ReMixed battle continued from its mid-battle save lost its conditions (a tester, 2026-11-22: in 1-A's last
  battle a child fell and nothing happened, then the other, then the battle went on with no enemy left while the
  children's bodies turned to treasure). The remaster doesn't save a battle's conditions: it sets a continued battle
  up again from the last event that ran in it, which in ReMixed's battles is an in-battle event (the Nemesis's
  return, a phase's scene) whose own conditions are others or none. Each ReMixed battle now keeps its scenario and the
  conditions it has loaded in the save (a variable neither game uses), and a continued battle gets both back before
  its first check. This covers every ReMixed battle (its 108 condition sets, story battles, missions and rounds), not
  only this one. Seen in the hidden PC game (`tests/quick/dorter-resume-save.txt`, then `dorter-resume-load.txt`):
  before the fix the continued battle took event 0x2FE's conditions; now it is scenario 0x34B with its own, a child's
  fall fails it, and the world map follows. A save made mid-battle on an earlier build still continued without them
  (fixed 2026-11-25, above). *high*
- [x] The two children of 1-A's last battle stood unseen except while walking or in water, with no faces (a tester,
  2026-11-22). ReMixed made its own sheets for the boy and the girl, with a battle unit's frames; the port had kept
  the remaster's art for them, which is a scene's (walking and a few poses). They now take ReMixed's sheets, smoothed
  like its other art, with faces from those sheets, wherever a boy or a girl of those two looks appears. Seen in the
  hidden PC game (`tests/quick/dorter-child-falls.txt`): both children on the field, their faces on the timeline, and
  the boy's face in his speech box. *medium*
- [x] ReMixed's Tonberries drew as green dragons, with a Tonberry's abilities (a tester in 1-A, 2026-11-22). The
  remaster draws a monster by the look its job names, and the port had written ReMixed's look in the wrong place of the
  job, so the Tonberry kept the look of the job it replaced. The same mistake left two more of ReMixed's monster jobs
  with no look at all: its Holy Dragon (a dragon in other colours) and the big chocobo's job. All nine monster jobs
  whose look ReMixed changed now name it where the remaster reads it. Seen in the hidden PC game
  (`tests/quick/tonberry-looks.txt`): the three in 1-A's desert battle are Tonberries, with their faces on the
  timeline. The other two jobs are checked in the built table, not seen in the game. *medium*
- [x] No music in ReMixed's battles that follow their own scene (a tester, 2026-11-22: Mount Bervenia's battle had
  none, and 1-A's last lost its music partway). ReMixed's scene fades its own track out and its Preparation Menu
  starts the battle's two (one before the battle, one in it); the port has no such menu and gave those battles no
  tracks. Every such battle now has ReMixed's two: 1-A's nine battles after a scene, the Argath campaign's seven, Luso's
  at Araguay, and the four with a scenario of their own (Yardrow's armory, 1-A's last, Mount Bervenia's second, Luso's
  at Mandalia). Yardrow's second track had been set by hand to the wrong one (0x29 for 8). A continued battle starts
  its track again too, as its setup reads the same place. Seen in the hidden PC game
  (`tests/quick/bervenia-battle-music.txt`): after its scene Mount Bervenia's battle sets up with tracks 8 and 0x5C.
  The hidden game is muted, so nobody has heard it yet: to hear on the Deck. *medium*
- [x] After 1-A's opening was chained directly into Bryn and Lorne's recruitment, it returned to Zeltennia at story
  progress 2 while the world-map rows that reveal Finnath Creek and its route still required progress 0. The objective
  named Finnath, but the party had nowhere it could travel. The opening route is now eligible through progress 2,
  retaining its 1-A and one-time guards, so new runs and already-stuck saves both reveal it through the remaster's
  native world events. Verified by cold-loading the Deck's exact stuck autosave: all eleven opening world actions
  completed, traveling to Finnath selected scenario 0x1FE, and event 510 began. The generated world-event rows have a
  regression audit for the progress-2 recovery path. *high*
- [x] The 1-A offer's Agrias and Orlandeau scene ended on a black screen instead of continuing to Bryn and Lorne
  (reported from the Steam Deck on 2026-11-21). Its PSP script used a PS1 hand-off mode the remaster does not support,
  and its narration window could remain logically open after the picture faded. Both opening copies now use the
  remaster's direct-scenario mode, and every generated ReMixed event closes all three message windows before its
  terminal END. The dialogue audit checks that cleanup across all 126 generated scripts. Seen in the hidden PC game:
  event 0x2F0 finished and loaded 0x194, with the 1-A flag and party changes preserved. *high*
- [x] ReMixed's lines with no speaker's name: in 1-A's scenes most boxes showed an empty name tab. The port shows
  ReMixed's PSP script, which leaves most speakers' names out of its lines where its PSX script has them. Each such
  line now takes the name its PSX copy has (558 lines in 50 events; the 11 left are narration and the Lucavi, nameless
  in both), and a list of answers its speaker, as the remaster's own have. Seen in the hidden PC game (2026-11-18,
  `tests/quick/bryn-answers.txt`): Bryn's lines under "Dancer" with her face, Agrias's answers under her name. *small*
- [x] Answer lists that took no answer: in Bryn and Lorne's scene Agrias's second answers to Bryn ("1. I said, make
  way.") came and went unseen, so the event read the last answer and turned them away, and her answers to the money
  ("1. Aye, that shall suffice. 2. Nay, keep your coin.") showed with no cursor; only Cancel answered (testers,
  2026-11-18). ReMixed shows such a list by swapping it into the box already open, which on the PS1 takes an answer;
  the remaster makes a box a menu only as it opens. A swap to a list of answers, or from one, now opens the box again
  (eight swaps across ReMixed's events). Seen in the hidden PC game: both lists wait, with Agrias's name, face and a
  cursor; Down and Enter took "2. Very well. What troubles you?" and Bryn and Lorne came along. *small*
- [x] Riders: the river camp's Mindflayers began its battle off their chocobos, though its scene had seated them.
  ReMixed seats a battle's riders (battle ids 0xC0, 0xC2, ... on the next id) as it begins, by its random battles'
  setup (0x191), once the formation has set the units up afresh; the port never ran that there. A battle's run now
  seats the pairs its battle has, as 0x191 does: the river camp's five and 0x0FB's three goblins on chocobos
  (`tools/check_story_regressions.py` checks both). Seen in the hidden PC game (`tests/quick/river-camp-start.txt`):
  all five linked to their chocobos as the battle begins and riding them through its turns. 0x0FB's not seen in the
  game (its battle started on its own won't take a placement in the harness). *small*
- [x] The river camp's freed chocobos ignored Agrias's answer about whose side they would take (a tester, 2026-11-22).
  Her first, second and third answers are meant to give all five chocobos team 0x00, 0x10 or 0x20 before the battle;
  once a Mindflayer falls, the normal riding unlink leaves that team in place. ReMixed's source event accidentally
  replaced each UnitAddress result with the answer's message number before writing the teams, and the port also kept
  its base-team write in the event-memory safety store (that safeguard stops random-battle event 0x191 from clearing
  every enemy team). The stray assignment is now dropped, and deliberate base-team writes from every ReMixed event
  except 0x191 are mirrored into the live battle unit. Offline checks cover the choice loop, the 0x191 exception, and
  allegiance surviving a mounted rider's death or an already-completed dismount. *small*
- [x] 1-A's Agrias with no face in its battles (the thief battle's "This chase is ended.", the river camp's closing
  line, testers 2026-11-18): ReMixed's events name her by its sprite set for her, 0x22, in some 320 instructions (her
  lines' boxes, poses, walks), which in its battles finds no one, as she's the remaster's Agrias (0x1E) from the
  formation. Its conditions already took 0x22 as her; the event unit lookup now does too. Seen in the hidden PC game
  (`tests/quick/river-camp-lines.txt`): her answers to how the chocobos take the Mindflayers' fall, and her closing
  line, with her face and a tail to her. The thief battle's lines are the same lookup; not seen (as 0x0FB's). *small*
- [x] Bryn's and Lorne's abilities as they join (fixed 2026-11-18): a tester found Lorne's Bard with no songs, and
  neither could go back to their own job once they changed. In ReMixed (its scene played on the PS1 and their records
  read once they joined) Bryn knows Slow Dance and an Archer's Concentrate, with 11 JP in the Dancer; Lorne knows
  Battle Song and an Archer's Equip Crossbow, with 61 JP in the Bard; both have the Squire, the Chemist and their own
  job open, and keep that job open for good by name, however their job levels go. The port gave Lorne's song and JP
  to a man's Dancer slot (ReMixed keeps its man's Bard there), and the remaster opened only the Squire and the
  Chemist. Lorne's song and JP now go to the Bard, and Bryn and Lorne keep the Dancer and the Bard open, in the party
  menu and in battle. Bryn was already right. Checked offline; not seen in the game yet. *small*
- [x] The same mix-up elsewhere (found checking every ReMixed scene that joins a unit, gives a locked look or writes
  a job or what a unit has learned, 2026-11-18): two more scenes put a man's Bard abilities in the Dancer. Ashley's
  scene with the Lich gives him five songs (as ReMixed does, though five is the count of his Dark Knight skills),
  and the Argath campaign's scene before its last battles marks a man's Face Up learned; both now go to the Bard. The
  other joins and looks (Luso, 1-A's later recruits and its locked Black Mage look, the Argath campaign's units, Reis's
  two forms, Mirror Match's copies) write nothing a gendered slot holds. Checked offline; not seen in the game. *small*
- [x] ReMixed's Dark Knights and men's Bards made from battle data levelled the wrong job (fixed 2026-11-18): its
  battle data names the job a unit's level goes to, and ReMixed's Dark Knight and a man's Bard share slots with the
  Bard and the Dancer. 51 units in 11 of the port's battles had theirs moved (the Dark Knights of two of ReMixed's
  battles, the men's Bards of another, and story characters ReMixed builds with those levels), so their other jobs'
  levels and what they've learned came out otherwise. They now level the Dark Knight (with its Knight 8 and Black
  Mage 8) and a man's Bard as in ReMixed. Checked offline; not seen in the game. *small*
- [x] Bryn and Lorne stayed after 1-A's river camp, and Elias was stuck "On Errand" (a tester on 0.9.17; fixed
  2026-11-18): in ReMixed the two leave there and Valmafra and Elias join as guests (played on the PS1: their slots
  emptied, then the two guests in the formation, Elias a Lv 4 Wizard). ReMixed dismisses a unit by its name, and the
  remaster looked only at its look, which Bryn's, Lorne's and Elias's locked looks never match; it now looks at the
  name when no look matches. The same goes for those 1-A's end sends away (Lavian, Alicia, Whitney) and the Argath
  campaign's. Elias's join also set a roster byte ReMixed never reads back, which the remaster shows as an errand; it's
  left out. A save that's already past the river camp is put right as it loads (Bryn and Lorne leave, their gear back
  in the inventory; Elias back from his "errand"). Checked offline; not seen in the game yet. *small*
- [x] ReMixed's Dark Knight enemies knew none of their own skills: ReMixed teaches a unit made from battle data the
  abilities of each job by its level, its Dark Knight among them, and the remaster's step stopped before the Dark
  Knight (and never opened the job for these units). On top of that the two Brave Story rounds that field Dark Knights
  gave them the wrong command: five men the remaster's own story skillset, eight women the Bard's songs, which a woman
  never learns. Seen in the hidden PC game (2026-11-19) before the fix: no Dark Knight skills on any of the 13. Now,
  as in ReMixed, the Dark Knight opens when a unit's Knight and Black Mage reach 8, gets its JP from its level and
  learns from it like any other job, and those units fight with Darkness: the five men know all of it (ReMixed gives
  them its whole set), the eight women what their level buys (Night Sword and Summon Demon always, the rest by
  chance). Seen in the hidden PC game (2026-11-19, `tests/quick/remixed-dark-knight-men.txt` and `-women.txt`). Not
  seen: a Dark Knight using them, as both rounds crashed as their battle started drawing (fixed below). *medium*
- [x] Brave Story's two Dark Knight rounds went black and closed the game about 12 seconds into their battle (found
  2026-11-19, on every build): the remaster draws its Dark Knights with sprite numbers it has no art and no shape for,
  so its battle drawing read a shape that isn't there. GenericJobs, the mod that adds the Dark Knight's job-menu page,
  ships its own art for them, which a game without it doesn't have. They now draw with ReMixed's own Dark Knight
  sprites and faces, the men in ReMixed's horned armour, the women in its skull helm, with GenericJobs or without it
  (the port's art takes over from GenericJobs' for these two, as ReMixed's look). How a round starts doesn't matter:
  its units and their drawing are the same either way. Seen in the hidden PC game (2026-11-19, without GenericJobs):
  both rounds play on for minutes, a man's and a woman's turn with their faces on the unit card, a move and an action
  (`tests/quick/remixed-dark-knight-men.txt`, 5 minutes, and `-women.txt`), and a player's Dark Knight, a man and a
  woman made so by a job change, in a random battle, the formation screen and their turns
  (`tests/quick/party-dark-knight-man.txt` and `-woman.txt`). Not seen: on the Deck with GenericJobs, and reached
  from the round before (below). *small*
- [x] A performer's song through another action (ported 2026-11-19): in ReMixed a performer that takes an instant
  action (an item, an attack) goes on performing, the song kept and given back after the action (seen on the PS1: a
  Potion, then Battle Song landing again). A Math Skill ends the performance, and a spell that charges replaces it, as
  in vanilla. That's why ReMixed took out its "Performing" notice at the start of a performer's turn. The port now
  does the same: the song and how long it has left wait through the action and come back as the turn ends, and the
  remaster's notice ("The unit is performing. Using another ability will cancel the performance.") is gone. Equip
  Change still ends a performance (in ReMixed it leaves a song that never lands). Seen in the hidden PC game
  (`tests/quick/performance-through-item.txt`, 2026-11-19): Ramza sang Battle Song, used a Potion with two ticks of
  it left, had it back in the turn order right after, it landed again, and his next turn's menu came with no notice.
  *medium*
- [x] Rafa's and Agrias's speech boxes showed the faces of ReMixed's characters on their second sprite sets: Balthier's
  for Rafa in the frog scene (event 270, a tester on 0.9.22; seen again in the hidden PC game on 0.9.24). Her lines
  there name a portrait from the set of event portraits the scene loads, and the remaster gives that portrait as the
  number it keeps her face under, which the mod gives ReMixed's character his. That portrait now names her own sprite
  set, which leads to her face, with that face's colours (on its own it gave a box with no face). It was the only one
  of the remaster's 64 event portraits so affected; none of them is Agrias's. Seen in the hidden PC game (2026-11-20):
  Rafa's own face in her boxes in the frog scene, and Agrias's own in hers with Lionel in sight (her box's other way,
  through her unit, mended 2026-11-19). *small*
- [x] An answer list with a sentence before it ran past its box: the river camp's "Nay. Beasts gather often..." and
  its three answers drew their last two rows under the box, and the cursor stood two rows up (seen in the hidden PC
  game). The remaster wraps a long row itself, but the tall boxes count a text's rows by its line breaks, so the box
  stayed three rows high. A list's lead is now broken into rows of its own (only the river camp's two versions had a
  long one). Seen in the hidden PC game (2026-11-19): seven rows in the box, the cursor on "1." and, after two
  Downs, on "3.". *small*
- [x] Only four places in 1-A's battles with Bryn and Lorne along (the river camp, 0x0F1): ReMixed deploys a second
  squad of two beside the four, which the port left out because its formation screen wouldn't commence with two
  groups. The remaster takes five of the player's units at most (its formation's list of units placed and its Start
  check: with 4 + 2 Start did nothing), and ReMixed's events set six in all of 1-A's battles and in the Argath
  campaign's three Hokuten battles (2 + 4, which couldn't commence at all). Now it takes six, the sixth in a free battle
  slot from 15 down as ReMixed puts its party past five, with the battle id after the party's; the second squads are
  on, 0x0F4's and 0x0F8's too (the build had left theirs out), and a second group can be left empty, as in ReMixed
  (the remaster's own Start check refused that). `tools/check_story_regressions.py` checks every battle's places
  (34 with two groups, 13 of them six). Seen in the hidden PC game (`tests/quick/river-camp-squads.txt`, 2026-11-19):
  the river camp with Bryn and Lorne along, Group 1 of 4 and Group 2 of 2 placed, Commence, the battle with six of
  the party (the sixth in slot 15), Bryn's and Agrias's lines, the victory and the world map, and the sixth's record
  written back. Not seen in the game: the other 1-A battles with two squads and the Argath campaign's 2 + 4. *medium*
- [x] Agrias, once she joins for good, showed Ashley's face in the party menu's unit screen (her sprite there was
  hers; a tester, 0.9.17). The world menus find a roster unit's face through the PS1's portrait numbers, and both of
  Agrias's sprite sets share one portrait, which led to the face the mod gives Ashley; Rafa's was shared the same way
  with ReMixed's character on Rafa's second sprite set. Ashley and the others ReMixed put on story characters' second
  sprite sets now have portraits of their own, and the shared ones lead back to the story characters, so Agrias and
  Rafa show their own faces there (and on the formation and join screens and in the party menu's dialogs) while
  Ashley keeps hers. Checked offline through the game's own code; not seen in the game yet. *medium*
- [x] Lorne showed chapter-one Ramza's portrait in the roster and outfitter (Steam Deck report). His locked sprite
  set draws graphic 0x38, which aliases the Bard's art. The stock PS1 portrait table has no entry for that graphic
  and gives him portrait 0. The enhanced roster menus now resolve locked looks and configured character and monster
  graphics before the existing face aliases apply. They keep the game's palette and shared portrait mappings.
  The offline check covers Lorne as a Bard and after changing jobs, Bryn, Elias, Hirame and the shared story portraits.
- [x] Brave Story's pages for Gafgarion and Malak showed the faces of ReMixed's characters that share their numbers
  (ReMixed's character on Gafgarion's second sprite set, and Luso). Brave Story lists only the remaster's people, so it
  now gets their own faces back. Checked offline; not seen in the game yet. *small*
- [x] Bryn's in-battle line with no face, and Agrias's choice at the river camp never coming up by itself (testers,
  2026-11-18): as 1-A's battles begin ReMixed's 0x191 gives the party's Bryn or Valmafra battle id 0x9A, Lorne or
  Elias 0x9B, by name, and its lines and conditions name them so (Agrias's choice waits on 0x9A and 0x22). The port
  never runs 0x191 there, so no unit had them. The event unit lookup and the conditions now take 0x9A and 0x9B as the
  party unit of those names, and a battle-data place off the field (0x91, 0x92) as the party unit of its name. Seen
  in the hidden PC game (2026-11-19, the river camp with both squads): the choice's condition set fires by itself,
  and Bryn's line shows her face with a tail to her. *small*

Fixed from a 0.9.7 playthrough on the PC (2026-11-18; Luso's recruitment screen and the 1-A save repair are under Testing and
side chapter 1-A above):

- [x] Bryn and Lorne's scene: Lorne stood as ReMixed's grave swords with a Squire's face (still so in 0.9.8-0.9.10).
  The event's fix to his job also changed the look it gives him to a slot that now holds the swords' art; his look is
  the Bard's again. Seen in the hidden PC game: Lorne job 0x5B, graphic 0x38 (the Bard), Bryn graphic 0x83 (the
  Dancer).
- [x] Bryn and Lorne both looked like Dancers in the party screens: the menus drew their locked looks by job and
  never by ReMixed's rule, so Lorne showed as the Dancer too. The menus now draw a locked look as the battle does
  (Lorne the Bard, Bryn the Dancer, a locked generic its job's look), and leave the remaster's own characters in its
  added scenes as they were (which the battle hook had also been changing). Seen in the hidden PC game's Units and
  status screens (2026-11-18), with Bryn's and Lorne's records set as their scene leaves them. *small*
- [x] Hovering Bardsong in Lorne's Equipment & Abilities screen crashed the game (fixed 2026-11-18). The job list
  takes a special character's base job from its sprite set, so Lorne's locked look (0xB4) was asked for job 0xB4,
  past the end of the job table, and the next step read address 0. Locked looks now start their job list at the
  Squire, as generics do. Reproduced twice in the hidden PC game (the Units screen opening on Lorne's record), then
  gone with the fix: the Units screen, his status and the cursor on Bardsong, with Bryn as the Dancer beside him.
  *small*
- [x] Araguay Woods ended as soon as its battle began (fixed 2026-11-18). Its chocobo, Boco, is loaded from the
  roster when you have him and otherwise made from his own battle data; the remaster looks for such a unit only in
  the guest slots, so Boco was always an empty unit. On 0.9.7 his "HP at most 0" conditions fired at once; on
  0.9.8-0.9.11 the empty unit crashed the HP display (a divide by zero) once the battle ran. He's now made from his
  data when missing, as Agrias and Gafgarion already were. Its victory also never wrote the battle back with Luso
  already in the party (results unsaved, Boco never joining): that way now joins and writes back itself. Seen in the
  hidden PC game (`tests/quick/araguay-luso.txt`, `tests/quick/araguay-battle.txt`): Boco a real chocobo, no early
  end, the victory once the goblins fall, story 0x11, Boco in the roster. *medium*
- [x] Dispatch's Transitioning course flipped bits of the wrong byte (+6 of a roster record, not the gender at +4),
  so it did nothing to most units and changed an unrelated byte of others (fixed 2026-11-18). It now does what
  ReMixed's does: the gender, a generic's sprite set with it (its look), and a Bard becomes a Dancer and back.
  Self-tested offline. *small*
- [x] Other battles' units loaded from the roster came out empty too, as Araguay's Boco did (found auditing,
  fixed 2026-11-18): side chapter 1-A's end at Orbonne (its three knights), Hirame and Wright's scene, one of Ashley's
  battles, Mustadio's two, Rafa's and one more, wherever the character wasn't in one of the 4 guest slots. In ReMixed's
  own battles, a unique character in the party is now lent a free guest slot while the battle's units are made and
  writes back to its own slot after (as the Argath campaign's Algus already did); a generic, a monster or anyone not in
  the party is made from its own battle data. The remaster's own battles are as before. Seen in the hidden PC game:
  Hirame loaded from the party and Wright from his data (`tests/quick/roster-loads-hirame.txt`), 1-A's knights from
  theirs (`roster-loads-knights.txt`), Araguay as before (`araguay-battle.txt`). *medium*

Asked on the Deck (2026-11-18):

- [x] The Tavern's menu has its own Missions button under Dispatch Missions: ReMixed's mission postings are listed
  there under their own heading and prompt, and Rumors lists only the ordinary rumors. Dispatch Missions' posting is
  gone from both (the Tavern's Dispatch button opens it). The bartender who unlocks the first mission, and the notice
  after him, point to Missions. Seen in the hidden PC game (`tests/quick/tavern-missions.txt`). *small*
- [x] B on a controller in Dispatch's roster picker (Special Training's, seen on the Deck) pulled the catalog back to
  its main page under the picker's units. The mod's Back fallback for empty pages now acts only when the catalog's
  list was on screen as B was pressed; B on an empty page still goes back. Seen in the hidden PC game with a
  controller's B held in the pad words (`tests/quick/tavern-missions.txt`). *small*
- [x] Dispatch Missions and the Tavern's Missions no longer need Start (asked 2026-11-19; on the Deck Start is held
  to skip scenes). In Dispatch's unit picker, the confirm button on a unit already picked now sends the units picked
  (the button bar says "Confirm Units" for it); before, only Start sent fewer units than the places. The remaster's
  own days and "Dispatch" or "Cancel" still follow, both on the confirm button. In Missions, the confirm button on a
  posting asks "Accept this mission? It begins as you leave town." in the prompt line, confirm for Yes and back for
  No; Start no longer accepts one. ReMixed itself used Start for both, its mission list once a mission's text is up.
  Seen in the hidden PC game (`tests/quick/dispatch-button.txt`, `tests/quick/tavern-missions.txt`): a unit sent on
  Valor training with Enter alone, its fee paid; a posting declined with Escape, then accepted. *small*
- [x] A group sent from Dispatch Missions was never kept as ReMixed's (found 2026-11-19 while moving Dispatch off
  Start): the game names the errand it sends by the row's text, which for Dispatch's rows isn't the row (Valor went
  as errand 90), so the mod didn't recognise it and left it as one of the remaster's own errands. Its days would have
  ended in the remaster's report of that errand, not ReMixed's, and an errand number past the remaster's 96 would
  have written past their storage. Recall had the same mix-up, so it didn't find the group. Both now go by the row
  chosen in the list. Seen in the hidden PC game (`tests/quick/dispatch-button.txt`): Valor sent and kept as
  ReMixed's group, its unit away, then recalled with the unit back. *small*
- [x] The game froze as units came back from Special Training (a player on Nexus, 0.9.41). Choosing a finished group
  in Dispatch Missions asks the game for its report, and the game looks for the group by the list's row; ReMixed's
  groups keep their own mission number there, so it found none, never started the report, and the Tavern waited on
  it for good. It affected every kind of mission, not only Special Training. The group now shows the game its row
  while it's looked for, the Dispatch screens stay up through the report (they closed as it began, so its numbers
  were a remaster errand's), and the report's payout no longer writes past the remaster's 96 errands. Seen in the
  hidden PC game (`tests/quick/dispatch-training-report.txt`, which brings the group home at once with the harness's
  `dispatchhome`, 2026-11-27): Valor sent, home, its report from Recall Units played through, its Brave effect
  applied, the unit free again and the Dispatch list back. Still wrong: the report paid 258 gil, the remaster's
  numbers for the row it borrows, where ReMixed's Special Training pays none (ReMixed's own report rules are on the
  unmerged branch `claude/remove-native-dispatch-text-d031b0`). *high*
- Dispatch Missions with nothing to send yet is ReMixed's own design: its catalog opens by the story section (var
  0x6F, which the story's events step and side chapter 1-A restarts at 1): Special Training from the start, Jobs and
  the first Mining and Exploration sets at section 3 (the Wiegraf and Gustav scene in chapter 1, or 1-A's scene at its
  progress 7), Salvaging and Investigation at 4, Combat, Odd Jobs and the monthly Contests at 5.

Reported by a player:

- [x] The game closed at the end of the battle at Riovanes Castle's gate, a few seconds after "Pray let them be safe.
  Alma...and Rapha, too." (a tester on 0.9.22), and in the frog scene before it Rafa was another character's sprite
  and face, with "? joined the party as a guest" at level 0. The port moved the remaster's Rafa off sprite set 0x19
  (ReMixed's character's) onto 0x29 in its battle data, but the remaster also names a character by sprite set in two
  event instructions (AddGhostUnit, DismissUnit) and in its enhanced battle-unit overrides (OverrideEntryData: entry
  0x1AD's unit 1 loads her from the formation as 0x19, so the unit was made empty and joined as that "?"), and the
  build moved only two DismissUnits, by hand. Now every remaster event and override row is read for a moved sprite
  set and moved with its units (build_mod.py write_remaster_sprite_sets, write_entry_overrides; the build stops if one
  is left). The gate's scene then finds the guest Rafa by name (WorldUnitAddress, name id 25, which stays) for its
  "Guest Departure" screens, and the empty one could be found there: a save's guest slot with Rafa's sprite set at
  level 0 is emptied (Patches/OldGuests.cs, checked offline). Seen in the hidden PC game (2026-11-19,
  `tests/quick/rafa-frog-scene.txt`, `riovanes-gate-end.txt`): the frog scene's Rafa is 0x29 with her own sprite and
  her join screen reads "Rapha joined the party as a guest"; the gate battle won, "Rapha is no longer a guest member of
  your party" and on to the next scene. Reported again on 0.9.31: the repair ran only on a world-map frame, so a gate
  battle autosave could resume with 0.9.22's level-0 Rafa still present; and if the repair had run, event 281 still
  passed WorldUnitAddress's zero result to its six Guest Departure pages. The first regression supplied a healthy
  Rafa and missed both routes. Event 281 now validates the record at its point of use, clears a corrupt one, and runs
  the pages only for a valid record. The focused case injects the corrupt record after battle setup (past both earlier
  repair opportunities) and reaches event 282. The later rooftop entry supplies Rafa anew, so affected saves continue
  in place and do not need a rollback. (That check in the scene was itself at fault and is out again: the next
  entry.) *medium*
- [x] The battle at Riovanes Castle's gate still couldn't be finished (a tester on 0.9.38, the game closing a few
  seconds after the last enemy fell, three times on one save, 2026-11-25). Two faults sat at the same spot. First, his
  guest Rafa had fallen during the battle and turned to a crystal; the scene after the battle shows her leaving the
  party, and for a Rafa who is a crystal or a chest the game looks for her by her sprite, by a number written into
  the game's own code. The port has her on another sprite (ReMixed's own character has hers), so no one was found and
  the game closed. Second, 0.9.32's repair for the earlier report had put instructions into that scene which only
  ReMixed's own scenes can use; the game ends a scene when it meets one, so with an ordinary living Rafa the scene
  stopped after "Pray let them be safe. Alma...and Rapha, too." and stayed on a black screen for good. From 0.9.32 to
  0.9.38 nobody could get past this battle. Now the scene is the game's own again, the code mod has the game look for
  her on the sprite the port gave her, and a Rafa who can't be found at all (a save an earlier build left an empty
  one in) is passed over instead of closing the game. A search of the game's code found no other place that names a
  moved character's sprite this way. Seen in the hidden PC game, the battle won each time: on 0.9.38 with her a
  crystal the game closed at the tester's spot (the same crash line), and with her alive it stayed black; on the fix,
  alive, a crystal, and with an earlier build's empty Rafa, the scene goes through "Guest Departure", she is out of
  the party, and the next scene starts (`tests/quick/riovanes-gate-end-healthy.txt`, `riovanes-gate-end-crystal.txt`,
  `riovanes-gate-end.txt`). A battle autosave made at the gate on an earlier build continues from where it is.
  *medium*
- [x] The remaster's enhanced battle-unit overrides undid the port's units where they met: four story units got the
  remaster's secondary skillset and abilities back over ReMixed's (entries 0x14A, 0x183, 0x185, 0x197), and two of
  ReMixed's own battles (0x1E6, 0x1EE) gave units 2-5 a weapon of the remaster's choosing. The cells that meet the
  port's changes and every cell in ReMixed's entries are left out now (16 cells; checked in the built table). *small*
- [x] Elias joined at 1-A's river camp with no name and Ramza's chapter 1 face (a tester on 0.9.22). The menus take a
  unit's portrait from its sprite set, and a locked look (Elias's 0x9C, a Black Mage) read entry 0, Ramza's: every
  locked look now takes the portrait of the look it draws as (Patches/UnitLooks.cs, checked offline through the game's
  own function: a man's Black Mage, as a generic's). His name id (0x6B) is a blank row in the remaster's names, as were
  Aliste's, Bremondt's, Valens's, Govis's, Teioh's and the workers'; every blank row a unit of the port's battle data
  names takes ReMixed's name now (build_mod.py unit_name_rows, 20 rows). Not seen in the game yet. *small*
- [x] Yardrow Armory (ReMixed's scenario 0xF4, the mod's event 511): Agrias's "So these are the fiends?" showed no
  portrait before its battle, where the scene before had them all (a tester on 0.9.22). After the town scene's "yes"
  the scene changes to the armory's map, which also brings in the units of the battle the scene names: on the PS1 the
  armory's enemies, Agrias, Balmafula and the Wizard, and the line is Agrias's own, with her face. The port keeps
  ReMixed's battles under other numbers, so the armory got another battle's units (one of Brave Story's rounds) and no
  Agrias to give the line a face. Now the armory gets its battle's units; the build checks that every such change of
  map brings in the units its lines name (only this one names its battle so). Seen in the hidden PC game (2026-11-20,
  `tests/quick/yardrow-armory-faces.txt`, about 2 minutes, the town scene fast-forwarded): Agrias, Balmafula and the
  Wizard at the armory's stairs and Agrias's face in her box, as on the PS1; then on to the battle. *small*
- [x] The game froze on the line "Rendezvous missions are now posted among the Rumors in taverns." (reported
  2026-11-17 with a screenshot, Auto-proceed on, most likely on 0.9.5; the report's screenshot and folder of files are
  kept outside the repo). It's the last line of ReMixed's 0x0A4, Biggs and Wedge's bartender. On 0.9.5 its box's wait
  never ended (0.9.6 fixed that, NOTICE_MESSAGES); since then the notice waited for a press after ReMixed's 400-frame
  hold, which Auto-proceed doesn't give. ReMixed's notice is timed, so now it closes when its 400 frames are up
  (TIMED_NOTICES, 2026-11-18). Seen in the hidden PC game with Auto-proceed on and no key pressed
  (`tests/quick/biggs-wedge-notice.txt`): the notice for its 400 frames, then the scene's end and flag 0x92. *medium*
- [x] Boco joined with no join screen after Araguay Woods: the scene ended and he was simply in the party (reported
  2026-11-18; Luso's join at Mandalia was the same a few releases before). ReMixed shows "JOIN UP!" for each recruit
  in the middle of the scene, and the scene's lines follow whether they joined (Luso: "a nice new entry in my
  Grimoire", or Ramza: "not right now"; after Araguay: "We will take that Chocobo"). The remaster's join screen asks
  it: "A unit wishes to join your party", Recruit or Refuse. The port had swapped that for the remaster's silent join,
  which asked nothing and always took them, in Luso's thanks after Araguay (Luso and Boco: it plays when Luso isn't in
  the party), Luso's recruitment at Mandalia, Zalbag's at Eagrose and the chapter-3 branch's, and in Araguay's ending
  without the thanks scene (Boco, when Luso is in the party). All of them keep ReMixed's join now, the remaster's "New
  Party Member" screen; the build checks it stays so (built 2026-11-18). Seen in the hidden PC game (2026-11-19,
  `tests/quick/araguay-boco-join-screen.txt`): on Araguay's world-map way, Boco's screen (him in the middle, Ramza at
  the side offering Recruit and Refuse), "JOINED!", then Boco in the party. Seen too, on the 0.9.18 build: Luso's and
  Boco's screens in the thanks scene, which goes on over its map (`araguay-thanks-join-screen.txt`), Luso's at Mandalia
  (`mandalia-luso-join-screen.txt`) and turning him down (`mandalia-luso-declined.txt`). Not seen yet: Zalbag's and the
  chapter-3 branch's. *small*
- [x] Brave Story's rounds handing on (2026-11-19). A round's win runs its event, which loads the next round's units
  (the battle entry stepped by one, then the map again with them) and its conditions. Each mission's second round
  loaded another battle's units: its first round had a slot among the location battles, not the one before its
  second round's (Brave Story 0x1E3 and 0x1ED, The Shrine Knights 0x1DF and 0x1EA). Their rounds now take runs of
  slots in ReMixed's order. The test's `defeat` left a unit its Reraise (Brave Story's 0x19 has an Angel Ring), and
  ReMixed's events run after the turn, not between actions, so it was up again before the win could run. Seen in the
  hidden PC game: Brave Story from its formation, its first round won, its second round's units and scene, its second
  round won and the third's five Dark Knights with their scene, the party's unit back on the field after each
  (`tests/quick/brave-story-first-rounds.txt`), and the second round started on its own handing on to the third
  (`brave-story-round-handoff.txt`); The Shrine Knights from its formation through its four rounds, round 2's and
  round 4's reinforcements coming in as their first ones fall, the party back after each hand-off, and round 4's win
  handing on to the Treasure Wheel (`shrine-knights-rounds.txt`, with the crash below fixed). Not seen yet: Brave
  Story's rounds 4 to 10 and its end. *medium*
- [x] The Shrine Knights' last round crashed the game as its reinforcements came in (fixed 2026-11-19): when its first
  three Knights fall, its event brings three Dragoons in where they stood, the camera on each in turn, and its third
  camera named unit 0x8B, which the round hasn't (ReMixed's slip: 0x85 on 0x85, 0x86 on 0x86, 0x87 on 0x8B). The
  remaster's camera instruction (0x1F) looks both units up; with one missing, the battle's drawing soon wrote through a
  null (exe 0x257F8B: a frame's 2 MB of GPU commands overflowed), most readily with Enter pressed through the scene.
  The build names 0x87 there. Seen in the hidden PC game: the round started on its own with Enter pressed every quarter
  second, which crashed every time before, plays its scene to its end (`tests/quick/shrine-knights-last-round.txt`,
  1.5 minutes), and the whole mission goes on from round 4's win to the Treasure Wheel (`shrine-knights-rounds.txt`).
  Not proven: that a missing unit is all it takes, as the remaster turns the camera that follows it off (writes 0xC0,
  a no-op, over its 0x19); a wider look at ReMixed's other cameras on units its battles haven't is still to do.
  *medium*
- [ ] The remaster's spare copy of the party's leader comes on with each round of The Shrine Knights and Brave Story:
  battle unit 20 holds one (sprite set 1, battle id 0, off the field), and the rounds' events put back each of units
  16-20 whose sprite set isn't 0 (the map's reload has just taken the party off the field), so it comes on with no
  sprite (whether it takes turns isn't known), its name tag ("Guest") in the screen's top left corner (seen on the
  PC, 2026-11-19). Taking a place by its +1 alone left the real party off too (tried and reverted the same day). Where
  the remaster makes that copy, and whether to clear it before the round's step, is still to find. *small*
- [x] The Dark Knight's unit card showed no job name without GenericJobs: the remaster's job table has no name for its
  Dark Knight (nor its command), which GenericJobs filled (seen on the PC, 2026-11-19, on both Brave Story rounds'
  Dark Knights). The port now gives it its name, Darkness and its icon itself (below, the job menu's page).
- [x] The Dark Knight's job menu page without GenericJobs (asked 2026-11-19: the mod needed cipherxof's GenericJobs
  installed for it). The mod pages the job menu itself, from GenericJobs' MIT code for the Dark Knight alone (not its
  Onion Knight, its art or its fixes for the remaster's own Dark Knight abilities): scroll down past the grid's last
  row for the second page, the first page's last row and then the Dark Knight. It opens by ReMixed's rule, Knight 8
  and Black Mage 8 (War of the Lions' 20 kills and six jobs before), and has its name, Darkness, the generic jobs'
  JP curve, a job help page and icons drawn from ReMixed's own Dark Knight sheet. With GenericJobs still installed
  the mod leaves the page to it and keeps ReMixed's rule. Checked offline (the unlock on made-up levels) and seen in
  the hidden PC game without GenericJobs (2026-11-19, `tests/quick/dark-knight-page.txt`): the second page, the
  Dark Knight locked with ReMixed's requirements, back to the first page, and roster Dark Knights in ReMixed's
  armour with "Dark Knight", Darkness and their icons. Not seen: the slot opened (a poked level doesn't open it;
  levels follow JP). *medium*


ReMixed's in-place patches are all reviewed: BATTLE.BIN, SCUS, WORLD and the EVENT overlays (analysis/
battle-in-place-review.md, scus-in-place-review.md, world-in-place-review.md, overlays-in-place-review.md, the last
with BUNIT.OUT and WLDCORE). Each save keeps its own settings, changed on the ReMixed tab of the in-game Settings;
Configure Mod in Reloaded-II (README, step 9) gives a new save's. Improved Level Scaling's choice of deployed units or
the roster is its own setting. (JP Scroll Glitch needs none: the remaster has no
such glitch.)

## Waiting for the display, text and art port

What ReMixed shows rather than what it does. Each needs the remaster's rebuilt battle or party menus, its text tables
or new art, none of which the offline check can exercise; the rules behind them are ported.

- [ ] The status chance tier in the battle info window. The hit chance the remaster shows already follows the ported
  tiers, as its preview runs the same formula code (simulated); only ReMixed's tier label is missing.
- [ ] Two Spells' menu extras: Cancel Casting between the two spells (a new battle-menu command; the remaster has no
  Cancel Casting), the second charge in the turn order, and no "charging" notice between them. The spells themselves
  work without them.
- [ ] Previews and markers (turn and charge previews settled: the remaster's turn order shows charges): zodiac markers, cycling targets in a preview (three changes in
  the target-selection code), and Move-Find's hidden-item panels highlighted for units with Move-Find Item
  (0x8015C300).
- [ ] Ability animations by unit type (read 2026-11-12): ReMixed gives 41 abilities a second animation for another
  kind of unit, so its monsters' reactions (A Save, Regenerator, Counter Magic and others) and its people's monster
  skills (the chocobos', Goblin Punch, Tentacle and others) move as that unit can, and 55 abilities another animation
  outright. The remaster plays its own animation for each ability, made for the units vanilla gave it. It shows in
  the ported battles: nine of the monster families' reactions are among those (Bull Demons' A Save, Ghouls' Sunken
  State, Dragons' Dragon Spirit, Morbols' Regenerator, Hydras' HP Restore, Bombs' Critical Quick, Flotiballs' Counter
  Magic, Juravis' Counter Steal, Woodmen's Distribute). To check first: what a monster shows when one goes off.
  *medium*
- [ ] The monster dismissal's messages: ReMixed's own confirmation, and a result naming the item (parked 2026-11-05:
  the dialog draws its text from a copy the mod hasn't found; what's known is in `Patches/Dismissal.cs`). The poach
  itself is ported and seen working on the Deck.
- [x] Per-job portraits (settled 2026-11-12): ReMixed's portrait number for each job is the WLDFACE portrait its world
  menus show for that job's units. The remaster draws every face (speech boxes, unit cards, unit lists) by the unit's
  graphic, so a generic or a monster already shows its own job's face there, the remaster's redraw of the same
  picture, and ReMixed's own characters now show their sheets' portraits (the item below), which are the pictures
  ReMixed put in WLDFACE for them (Hirame's, the Tonberry's, the Lamia's). The numbers stay held back for good: in the
  remaster that byte is a monster unit's graphic, its sprite and face both (monsters showed Lucavi faces on the Deck).
- [x] Locked looks and ReMixed's gendered jobs' looks (built 2026-11-15; not seen in the game yet): ReMixed lets a generic lock its look to a job (sprite sets 0x90
  up) and gives Bryn and Lorne their own looks once they join (sprite sets 0xB5 and 0xB4 in the roster); the remaster
  gives a sprite set past 0xA9 the graphic of its job + 0x28, which has no sheet, so they draw as dark silhouettes, with
  no face. The unit-look hook now decodes those sprite sets as ReMixed does, including Bryn's 0xB5 to the female
  Dancer graphic (0x83), Lorne's 0xB4 to the male Bard graphic ReMixed uses (0x38, aliased to the remaster's Bard art),
  and the job-locked generic sets by gender. The port already maps their jobs to the remaster's gendered Bard, Dancer
  and Dark Knight jobs. Checked offline for a locked male Squire, Lorne and Bryn. Battles only until 2026-11-18,
  when the menus got the same rule (Open, "Fixed from the same playthrough"); Lorne's scene look is there too.
  *medium*
- [x] ReMixed's own unit art where the remaster has none (asked 2026-11-12: "for any art not in the remaster, just
  use the psx remix sprites", with a filter to bring them nearer the remaster's art): nine of ReMixed's sprites come
  from its disc, for the slots nothing of the remaster's draws: its purple-haired fighter, a figure in a dark hooded
  cloak, an orange-haired woman, a blond man in red, a knight in a red hood, swords standing in the ground, a Lamia
  (in the Hydras' place, as in ReMixed) and a Tonberry. With the enhanced sprites they're drawn at twice the size,
  smoothed with Scale2x, the filter that came closest to the remaster's own redraws of the sprites ReMixed kept.
  ReMixed's special monster jobs (Byblos, Steel Giant, Apanda, Serpentarius, Holy Dragon and the two Demons) now
  get their ReMixed looks too, which the build held back; those draw the Lamia, the Tonberry, and the remaster's
  Byblos, Demon and Steel Giant. Seen on the PC (2026-11-12, enhanced sprites): the Lich (a hooded skeleton in grey
  armour) and his two Dark Crusaders in their battle at Zeklaus, its opening and its turns, in ReMixed's colours under
  the map's dusk light (purple in the battle: they're Undead, which the remaster tints), where the Lich drew as the
  game's red stand-in figure before; and in 1-A's rescue, the woman climbing the cliff, beside ReMixed on the PS1:
  the same sprite, colours and poses. Not seen yet: the Lamia, the Tonberry and the special monster jobs. Each now
  moves with ReMixed's own set of animations for its slot (built 2026-11-12, not seen in the game yet): until then
  the Lamia moved as the Hydras do, the blond man in red as a monster, and the purple-haired fighter and the knight
  in the red hood with the other set of poses vanilla gave those slots. *medium*
- [ ] Luso's "Big Chocobo" at Mandalia must actually be visually enlarged (reported from the Deck, 2026-11-15).
  Its boss stats are already ported (HP, PA and MA doubled; Speed reduced), but ReMixed also walks the PS1 sprite
  object list and writes fields +0x325 and +0x327 to scale unit 0x86. Those sprite-object fields have no remaster
  mapping yet, so the enhanced game draws an ordinary-sized Chocobo. Find the remaster sprite transform/scale fields
  and apply the same effect without changing every Chocobo. Read 2026-11-18: ReMixed finds the object whose +0x134 is
  the unit and writes 0x14 to +0x325 and 0x18 to +0x327 (no code of BATTLE.BIN's or SCUS's reads those offsets
  directly). The remaster's sprite objects (from 0x260ABC) are 0x608 bytes with the unit pointer at +0xE8 and no float
  fields, so the PS1's offsets don't carry over. Left: find the remaster's draw size, with the test mode's auto-battle
  off so a change can be seen (`tests/quick/mandalia-koco.txt` reaches the battle). *medium*
- [x] ReMixed's characters in the story characters' second sprites (built 2026-11-12, not seen in the game yet):
  ReMixed gave each story character one sprite and put its own characters in the others. The remaster's own battles
  and scenes now draw their characters from the first ones, as ReMixed does: the guest Agrias of chapter 2, the guest
  Rafa at Riovanes and the enemy Gafgarion, with their jobs, in its story battles and in its own added scenes; the
  tutorial's units become the White Mage, Black Mage and Mystic they copied, as in ReMixed; and Poeskas Lake's undead
  Mystic and Summoner keep their jobs on the generic sprites they copied. They look as before (the same art). So
  ReMixed's characters there draw its own art: Ashley in Agrias's second, Luso in Malak's (since the rooftop Malak
  moved), eight more in Gafgarion's, Rafa's, Celia's and Lede's seconds and in two of the tutorial's and both of
  Poeskas Lake's slots, and its Bard's look in the tutorial's third, which draws the remaster's own Bard (ReMixed's
  is vanilla's). Each has its own face from its sprite too, kept apart from the story characters' faces, which the
  remaster files under those second sprites (Agrias's, Rafa's, Celia's and Lede's stay theirs); the Bard's look
  shows the Bard's face. Young Agrias, in Mustadio's second, is ReMixed's Agrias sprite and drew
  as the remaster's Agrias already. The remaster's two events that send a guest away name the moved sprites, and a
  save from an earlier build with a guest on the old sprite has it moved over as a battle starts. Before this, the
  guest Agrias had Ashley's job in chapter 2. To see in the game: Ashley in the Lich's battle and her scene, Agrias
  as a guest in chapter 2 and her return at Bariaus Valley, beside ReMixed on the PS1. *medium*
- [x] ReMixed's dragon and hydra in Brave Story's rounds (a grey dragon in round 7, a hydra, Serpentarius, in round
  3), in sprite slots the remaster animated as people: they now come from ReMixed's sprites with a monster's animations
  there, as ReMixed gives them (the dragon moves as the Dragons do, the hydra as the Hydras). The dragon is drawn at
  twice the size like the other art from ReMixed; the hydra is vanilla's Hydra art, which ReMixed moved to make room
  for its Lamia, so it gets the remaster's own redraw of it. Neither needs the second sheet some monsters use for
  their attacks: ReMixed gives them none, as the remaster doesn't. Seen on the PC (2026-11-12), beside ReMixed on the
  PS1: the hydra standing, facing the camera and taking its turn in round 3, and the dragon standing and taking its
  turn (moving, then acting) in round 7, in ReMixed's poses and colours. Round 7 was started on its own with the
  dragon placed by hand, as its opening (where Reis rides in on it) can't be reached yet (Brave Story's first
  round, above). *medium*
- [x] The new characters' portraits (asked 2026-11-12: Hirame showed a generic boy's face): the remaster draws a face
  in speech boxes, unit cards and unit lists by the unit's graphic, and turned the graphics ReMixed's characters have
  into others' (Hirame's into a male Squire's, Luso's into Malak's, the Tonberry's into a Dragon's). Each graphic drawn
  with ReMixed's sheet now has a face of its own made from that sheet's portrait, the way its sprite is made: Scale2x,
  then doubled, which matched the remaster's own faces of 23 portraits ReMixed kept as closely as doubling the pixels
  and with a closer outline: Luso, Hirame, the Lich and his Dark Crusaders, the Lamia, the Tonberry and the others
  drawn from ReMixed's sheets (twenty with the dragon and hydra in Brave Story and the characters in the story
  characters' second sprites; the swords have no portrait). Built 2026-11-12; seen on the PC the same day (Hirame,
  Luso, the Lich and Ashley; the rest under Testing). *medium*
- [x] Ashley's unit card said "Agrias" in the Lich's battle (seen on the PC, 2026-11-12): she's on Agrias's second
  sprite set, whose name the remaster's character names give as Agrias. She now has a name of her own, "Ashley", as
  Luso has (a blank row of the remaster's names; ReMixed's events find her by her sprite set, never her name), in
  each of her battles and scenes, so her card, and her roster entry once she joins, say Ashley. Her face and job
  (Riskbreaker) were hers already, and her speech boxes say "Strange Man" and then "Ashley", as ReMixed has them.
  Seen on the PC (2026-11-13): her card in the Lich's battle and the join screen say Ashley, as the PS1's do.
  *small*
- [x] Hirame's and Wright's unit-card names: their CharaName rows were blank in the remaster. Their units now use
  dedicated rows 0x36 and 0x37, including Hirame's crossed name bytes between 1-A's rescue and the Fort Zeakden
  branch. Checked offline; not seen in the game yet. *small*
- [ ] The remaster's join screen ("New Party Member", "JOINED!") draws Ramza on its pedestal for Ashley (seen on the
  PC, 2026-11-13; her name, face and level on it are hers). The PS1 has no such sprite (its "JOIN UP!" is over her
  status screen). Not checked yet for the other joins ReMixed's events make (Luso's, Boco's: the screen shows for them
  from 2026-11-18). Boco's (seen 2026-11-19) shows the stock layout: the joiner in the middle, Ramza at the side
  offering Recruit and Refuse; so Ramza is as the remaster has him, and what Ashley's lacked was her own sprite in the
  middle. *medium*
- [x] ReMixed's redraws of the ten vanilla event graphics its currently shipped scenes load (built 2026-11-15; 0x10
  seen in Mandalia on 2026-11-16): each goes where none of the remaster's own scenes looks for it, so those scenes
  keep their art. 0x10 (Luso's scenes) ships under its own number, which no remaster scene loads; the other nine
  have private slots, and the port rewrites only ReMixed's own scenes to load them there. All ten have their art at
  twice the size for the enhanced sprites. The build checks every pose ReMixed's shipped scenes show against its art,
  and that no remaster scene loads any slot ReMixed's art is in. Fixed 2026-11-18: 0x10 had been put in a slot the
  remaster's chapter 2 scene of Agrias and Ovelia with Lionel in sight uses, so Agrias's hand-to-brow pose and
  Ovelia's reach for the tree drew in ReMixed's art (an orange-haired man in her place). Not seen in the game yet.
  *medium*
- [ ] ReMixed's remaining vanilla event-graphic redraw, 0x7A: only its event 0x09F loads it, and that event isn't in
  the currently shipped scenarios. Give it a private enhanced-texture slot when that scene is ported (0x7A shares
  vanilla's texture with ReMixed's already-custom 0x6F, so it can't replace that texture in place). *small*
- [x] Reveal Treasures' arrows (asked 2026-11-12): once Reveal Treasures is paid for (or free on a map paid for
  before), ReMixed's long arrow stands over each hidden item with something left while the formation screen is up, as
  its Preparation Menu stands them: in its colours by item (green, red, teal and purple; grey where only the common
  item is left), each with its shadow, at the item's height. They come off as the battle begins. A tile is marked
  yellow instead only when an arrow can't be had (the game has sixteen unit sprites; an arrow gives its own back to a
  unit placed when all are taken). With the enhanced sprites they draw from ReMixed's own art at twice the size,
  which the Treasure Wheel's work ships. Seen on the PC (2026-11-12, Chocobo Defense, enhanced sprites): the four
  arrows after Yes, units placed in both groups beside and under them, all four gone as the battle opened with no
  extra units left, and again in a second battle.
- [x] ReMixed's own event graphic 0x6F, the Treasure Wheel's chests and the arrows its Preparation Menu stands over
  hidden items: a graphic vanilla's events never load, which the remaster drew from its own art with the enhanced
  sprites, where ReMixed's chests have nothing (only their shadows showed). It ships with ReMixed's art at twice the
  size as that texture (2026-11-12). Seen on the PC: the Wheel's twelve chests in their four colours, each opening
  as on the PS1 (the arrows: above).
- [x] Reveal Treasures at a battle's start (asked 2026-11-11), from ReMixed's Preparation Menu (event 0x192): when a
  battle's formation screen opens on a map whose hidden items still hold a rare one, it asks "Reveal item locations
  for N gil? Current gil: N." in the box that asks "Commence battle?". The price is ReMixed's: a quarter of the
  rare and common item prices of the panels whose rare item is left, at most gil × those panels / 40. Yes takes the
  gil and marks the hidden items' tiles yellow for the formation, where ReMixed stands arrows over them in its
  menu; a deployment tile among them stays one, and the game clears the marks as the battle begins. A map paid for
  once (flag 0x100 + the map, as in ReMixed), or with its rare items found, shows them without asking. Seen on the
  PC (2026-11-11, Chocobo Defense): 1025 gil asked of 19180; Yes (18155 left, four tiles marked, Ramza placed on
  one); No (nothing taken, "Commence battle?" back); the paid map's tiles shown for free. The payment lasts past the
  battle (2026-11-12): the remaster copies the event variables back as a battle begins, from the copy its formation
  screen took as it opened, which took the gil and the paid flag back (a second Chocobo Defense after the first was
  won asked again, with the gil from before paying), so the port pays into that copy too. ReMixed's menu runs after
  the formation, and the PS1 copies nothing back. Seen on the PC in the regression pass (2026-11-12): the gil (18155)
  and the paid flag kept after "Commence battle?" and after the won battle, and the second Chocobo Defense showing its
  four arrows for free.
- [x] Maps 97, 99 and 101 for side chapter 1-A's scenes (0x0F2, 0x0ED, 0x0F9): the enhanced game draws a map from a
  look of its own, and had none for these (vanilla maps no vanilla scenario uses, which ReMixed does), so those scenes
  showed their units on the sky. The build makes a look for each from the classic map (`tools/maps.py`): its polygons
  where the classic map has them, its texture in each palette, flat lighting. Seen on the PC (2026-11-06): the street
  of 0x0ED, the castle gate of 0x0F2 and the stone floor of 0x0F9.
- [x] ReMixed's own map VOLCANO (119) for side chapter 1-A's battle 0x0F8 and scene 0x117: its classic files in
  place of the remaster's map 119 (its battle grid; the mesh without its render properties, which the look carries,
  to fit the file the game reads), a look built from ReMixed's files, and highlight panels on every built look (the
  enhanced game draws move and target highlights on them). Seen on the PC (2026-11-06): its volcanic ground, the
  deployment tiles drawn on the panels, the battle through its closing scenes back to the world map.
- [x] ReMixed's edits to MAP075, for 1-A's battle 0x0F7 (the only scenario on 75; vanilla's story never fights
  there): ReMixed reshaped it (120 tiles' heights and water, its polygons, lights and palettes, in its default mesh
  and nine time and weather variants). Those ten files go in place of the remaster's (each the same size), and a look
  built from them replaces the remaster's HD volcano (ReMixed's ground over the HD look, chosen 2026-11-06); the HD
  look's lava-bubble animations go with it, and the units keep its volcano lighting. Seen on the PC (2026-11-06):
  0x0F7's formation and opening on ReMixed's 75, its lava and rocks.
- [x] ReMixed's TUNNEL (118), for the Algus campaign's start (0x0FD): on map 98 (the remaster draws no built look
  on map 33), its mesh cut to the file's size without its moving parts' animations. Seen on the PC (2026-11-06).
- [x] ReMixed's descriptions (2026-11-06): its abilities', jobs' and skillsets' help (EVENT/HELP.LZW, read by
  `tools/remixed_help.py`: FFT's text compression, ReMixed's two-letter codes and its font's parentheses) where it
  changed vanilla's text or the remaster's slot has a placeholder: 205 abilities (Momentum, Banditry, Two Spells,
  Counter Steal, Pass; Waterbreathing had "DESCRIPTION UNUSED"), 98 jobs (ReMixed names each job's innate abilities:
  "Innate: Banditry") and 55 skillsets. One paragraph each, as the remaster writes them; an action ability's stat
  lines are left out (the remaster's panel shows range, MP and speed) but for its weapon requirement; vanilla's names
  in them become the remaster's where that's unambiguous ("Gil Taking" is "Steal Gil"). Not seen in the game yet.
  Two run longer than the remaster's longest (Jump 236 characters, Two Spells 290; the remaster's reach 211).
- [x] Items' descriptions (HELP.LZW's section 13, 256 items) and ReMixed's battle help (ATCHELP.LZW), settled
  2026-11-12: nothing to port. Of the 70 items ReMixed rewrote, 40 change the description's words, all proofreading
  of vanilla's English ("stick" to "pole", "Mythril?Platinum" to "Mythril/Platinum"), which the remaster's own
  translation replaced; the rest, and the stat lines of all of them ("WP:4 EV:5% Dual Wield 25% Add:Darkness",
  Genji Shield's "Half:All Elements"), say what the remaster's item panel shows from the ported data, but for an added
  status's chance (the tier label item above) and ReMixed's Rare tag. ATCHELP holds the same texts for the battle
  menus, and a section of its own (135 texts) for the skillsets of ReMixed's special jobs (Arc Witch, Black Knight,
  Fellblade, General...), which come with those jobs (the story port). Item names: of ReMixed's 10 renames the
  remaster has 8 already (the Glacial and Blaze Guns the right way round, Lamia's Harp, the poles); its "Javelin II"
  (106) and "Escutcheon II" (143), the second of each (the remaster names both alike), are in since 2026-11-06
  (`ITEM_RENAMES`).
- [x] The "???" entries in the Thief's and Monk's ability lists (seen on the Deck, 2026-11-06, and on the Monk's
  again 2026-11-12): ReMixed gives them the assassins' Shadow Stitch and Stop Bracelet with no way to learn them (no
  JP cost, a learn rate of 0), for enemies that use their skillset, and the remaster shows such an ability as "???"
  until a unit has it. The remaster builds its ability lists in three places; the first fix (2026-11-06) left it off
  only one of them, so the "???" stayed. All three leave it out now (2026-11-12). A unit that has one (learned on
  hit, say) still lists it. Seen on the PC (2026-11-12): eight Monk and eight Thief abilities, no "???".
- [x] ReMixed's longer lines in the remaster's speech boxes (2026-11-07): a speech box holds three rows of about 62
  characters and keeps the size its first line opened it with, while ReMixed swaps later lines into an open box
  (0x51) and has lines up to 815 characters; in 0x0F9 two ran past their boxes' bottom edge. Now a swap to a longer
  line opens the box again at that line's size (64 swaps), and a line past 170 characters shows in pages of up to 170,
  each a box of its own after the one before (168 pages). Seen on the PC in 0x0F4's scene: the knight's 480-character
  plea in three full boxes, his thanks in two; and in 0x0F9's (2026-11-07, later), Gareth's three rows in a box opened
  again and a line's second page in a box after its first, once the build followed which of ReMixed's two scripts an
  event shows (some put the one the port reads first: they had been fitted to the other's lines).
- [x] ReMixed's two changed effect files (built 2026-11-15; not seen in the game yet): E331, which ReMixed rebuilt for
  its Cleave (0x0B5, a wide weapon swing) and Scratch, and E306 (three bytes) for Rally and Composure. The remaster's
  `effect_eNNN_bin` and `efc_eNNNdat_data` are byte copies of vanilla's PS1 files; the build verifies that and ships
  ReMixed's E306 and E331 under both names. ReMixed rebuilt E331's data, not just its code: its header's later sections
  all start 0x1C bytes further on. Cleave, Scratch, Rally and Composure remain in Testing to prove which name the
  remaster draws. *small*
- [ ] ReMixed's remaining music track: track 99, a new piece ReMixed
  plays only in side chapter 1-A's volcano battle (0x0F8).
  The remaster streams its own recordings (`sound/music/music_00099.sab`), not the PS1's sequences, so the piece needs
  rendering and encoding in the remaster's sound format; until then that battle plays the remaster's track 99.
  Settled, the remaster drawing its own: the menu frames (FRAME.BIN), the font, the title and opening pictures
  (OPEN/), the tutorials (MENU/TUTO*, the remaster's text), the map titles (MAPTITLE.BIN), the item pictures
  (ITEM.BIN), the town backdrops (WLDBK.BIN) and the Bonus Content pictures (BONUS.BIN, its missions are posted in the
  taverns); the portraits (EVTFACE, WLDFACE): ReMixed's new ones are its sheets' portraits, which its characters'
  faces are made from (the portraits items above). *medium*
- [x] Brave Story's grey Dark Knight drew blue and gold (round 3; found beside ReMixed on the PS1, 2026-11-19, and
  first put down as the round's first knight, which is the Red Dark Knight and was right). ReMixed colours two of
  its five knights with colours 5 and 6 of their sheet, grey and orange with gold; the remaster draws any colour
  past the fifth of a generic's or Dark Knight's sheet as the first, blue and gold, where the PS1 draws them all. The
  port now lets those units keep all eight of their sheet's colours; no other unit in ReMixed's battles or the
  remaster's uses one past the fifth, so nothing else changes. Seen in the hidden PC game (2026-11-19): the five drawn
  in their own colours, the grey one beside the PS1's (`tests/quick/brave-story-dark-knight-palettes.txt`, 81 s,
  which also reads each knight's colour from the game). *small*
- [ ] Brave Story's fifth Dark Knight (round 3, orange with gold, standing behind the purple one) shows faint and
  see-through in its opening scene, where ReMixed on the PS1 draws it solid (seen in the hidden PC game beside the
  PS1's, 2026-11-19; its colours are its own). Not looked into: the remaster may fade a unit that its own map hides,
  or the knight may stand where the remaster's map has a step in front of it. *small*

## Waiting for the story port

What's known (2026-11-04, docs/REMASTER.md "Event scripts"): the remaster runs the PS1's event bytecode, one file per
event (`script/enhanced/eventNNN.e`, the PS1 event's code without its text), with edits of its own (new opcodes such
as 0xC5, likely its voices and subtitles, and changed camera and timing steps). Its event text isn't the PS1's: it's a
new translation, in a 7.6 MB `fftpack/event_test_evt.en.bin` of a different layout. ReMixed rewrote its events
wholesale: of 500 slots, 179 events are rewritten, 112 identical to vanilla, 8 changed only in their text, the rest
empty; none are light edits. So the port brings ReMixed's events over whole rather than patching the remaster's.
In order:

1. ~~Whether the remaster runs an event it didn't write.~~ It does: the mod shipped the remaster's own event401.e
   (random battles' event, 0x191, found through the test mode's event log) with an instruction in front that adds
   0x1234 to an unused event variable, and the Deck's game ran it (2026-11-04). The mod loader serves
   `FFTIVC/data/enhanced/script/enhanced/eventNNN.e`; the loader (0x2F3BC4) takes the event number × 5.
2. (Decided 2026-11-04: the remaster's names, dialogue and voice acting stay; ReMixed's PSX text and spellings
   aren't ported, only its event logic. Where a ported event shows a message, it uses the remaster's line or none.)
   The event text file's layout, and how an event finds its text; then ReMixed's text for its rewritten events (in
   ReMixed's own English, beside the remaster's translation elsewhere). First look: `event_test_evt.en.bin` is 930 × 0x2000
   bytes but not one block per event (375 blocks start non-empty); it uses the PS1's character encoding ("Delita"
   0D 28 2F 2C 37 24 shows 419 times, "Agrias" 132), with the remaster's spellings ("Gafgarion" never appears).
   Its layout: event N at N × 0x2800 (5 sectors of 0x800; the event loader's id is N × 5), laid out like the PS1's
   (u32 text offset, code, text), and the text is the remaster's new translation in the PS1's format, a space being
   0x95 where the PS1 has 0xFA (event 2: "Father, abandon not Your wayward children of Ivalice…"). The code in it
   matches the `.e` only for some events (401), so the game likely takes code from `.e` and text from here. So a
   ReMixed event ports as its code in a `.e` and its text in its block here.
3. ReMixed's opcodes come first, though: it adds or changes 44 (its interpreter's length table, BATTLE.BIN
   0x8014D170: 0x02, 0x05, 0x06, 0x0A, 0x0C–0x0F, 0x14, 0x17, 0x2F, 0x8D, 0x9B–0x9F, 0xAC–0xAF, 0xBF, 0xC1–0xC7,
   0xCC–0xCF, 0xE0, 0xE6–0xEF), and nearly all its rewritten events use them, random battles' 0x191 included (0xC2,
   0xC4, 0xC7, 0xCF and the variable sets, for the level scaling variables 0x70–0x72). Some clash with the
   remaster's own (both use 0xC5). Each needs its handler read from ReMixed's interpreter, then either translated at
   build time into instructions the remaster has (ReMixed's variable sets 0xAC–0xAF become vanilla's clear and add,
   with multiplies for the high word), or taught to the remaster's interpreter under a free number (REMASTER.md, "The
   event interpreter": 20 clash, 3 repurpose vanilla's, the rest are free). This is the bulk of the story port.
   Decoded (2026-11-04, docs/analysis/remixed-event-opcodes.md): ReMixed's interpreter runs its own compare chain
   before vanilla's (BATTLE.BIN 0x801588BC, from 0x80143D3C). Beyond control flow (IF/ELSE, SWITCH/CASE, functions
   with CALL/RETURN, GOTO/LABEL) and the variable sets, its scripts work on the PS1's memory: 0xC1/0xC4 read and
   write PS1 addresses (4,674 times: about 200 battle unit fields, roster fields, about 240 absolute addresses),
   0xCF calls ReMixed's MIPS code (five routines), 0xE6 patches the next instruction with a variable, and three
   opcodes read disc sectors. Only 17 of the changed events avoid all of that. So build-time translation covers
   the control flow and variable sets only; the rest needs an interpreter for ReMixed's instructions in the
   remaster with a map from PS1 addresses to the remaster's memory, and C# ports of the five routines.
   Started (2026-11-04): the remaster's fetch is 0x23AFD8; the mod hooks it and the length helper for code marked
   as ReMixed's, and runs ReMixed's control flow, sets, PEEK/POKE (through a map of unit and roster fields) and five
   more opcodes in C# (`Patches/RemixedEvents.cs`). The first event shipped is random battles' 0x191: on the Deck
   it runs to its END and the battle plays with its enemies (2026-11-04, the third try: the first two found the
   code at the wrong offset, then the remaster's 16-bit setter and var 0 scratch, then the base team). Done since: the native routine 0x80151000, Improved Level
   Scaling's re-run, goes to the port's own (var 0x70 0xFEFE turns it off; the enemy level sums follow the rebuild);
   seen on the Deck (2026-11-04: the event's call, then the first countdown's rebuild).
   Done 2026-11-05 with the decompiled exe: 0xCD (the per-event map settings), ReMixed's words (its variables
   0xB00–0xB7F, which ended a remaster-run event, and the list 0x9A–0x9E), and the unit fields +0x1B8/+0x1B9 (166
   events), +0x4E–0x71 and +0x15C–0x162, 0x14 (the music change, 88 events), 0x8D (the item counts, 21), unit
   +0x163/+0x164 (22), 0xCE (the battle event conditions, 262 events: ReMixed's presets off its disc into the
   remaster's tables), the buttons (165) and the event variables as memory; 188 of the 290 changed events need
   nothing more (docs/generated/story-readiness.md). What blocks the most now is 0xE0 (17 events), two native
   routines (9 each) and a few BATTLE.BIN places.
   Still to do for it: (was: the native routine 0x80151000, which saves the
   units' fields, calls SCUS 0x8005E644 with a count from var 0x52 and 0x8008E7B8, and puts them back: likely the
   enemy count for the party's size), 0xE0's spawns on four maps, and 0xCD.
   How the port goes from here (2026-11-04, `python -I tools/story_readiness.py` writes
   docs/generated/story-readiness.md, each changed event's needs): the remaster added its own instructions to 181
   of the 290 events ReMixed changed (its voice cues and timing, around the dialogue; corrected 2026-11-05, the
   count had read the leftover bytes after each `.e`'s END), and ReMixed re-staged its
   story scenes (80–95% of a scene's instructions rewritten: camera, walks, waits); a three-way merge of the two
   conflicts in 189 events. So the remaster's story events stay (their voice acting with them), and the port
   brings ReMixed's in where the remaster has none to lose: events without dialogue (random battles' 0x191),
   its new events in slots vanilla left empty, and the gameplay in its battle data. ReMixed's options menus
   (0xC9, 0x11C) could come back the same way. What the events need most: opcodes 0xCE (262 events) and 0xCD
   (257), the unit fields +0x1B8/+0x1B9 (166), opcode 0x14 (88).
   The battle data didn't reach the game until 2026-11-05: the mod loader's replacement of fftpack files (the ENTD
   files) finds no pattern in this version of the game, so battles read the stock files; ReMixed's builds in the story
   entries and its random battles' units never applied. The code mod now gives the game the mod's files itself
   (`Patches/PackFiles.cs`; seen on the PC: ReMixed's units in the battle it reads them for).
   Which to ship (2026-11-05, docs/analysis/story-ship-plan.md): nothing more. ReMixed renumbered its events (it
   rewrote 464 of the 480 scenario records and merged vanilla's runs of short scenes: its event 2 is vanilla's 3–6),
   so its event N is rarely the remaster's N, and its battle setup (0xCE's conditions, 0xCD's map settings) belongs
   to its own event code. ReMixed's story-battle gameplay comes over through its battle data and the mechanics.
   Its own content comes next (planned 2026-11-05, docs/analysis/story-flow-plan.md; the rule now: the remaster's
   scenes and voices stay where ReMixed tells the same story, ReMixed's own content comes with its text): 86 new
   events, 1,854 lines (Agrias's storyline, Zalbag and Algus, single scenes with Ashley, Balthier, Luso and others,
   the location battles, its systems' menus), renumbered into scenario ids the remaster leaves free.
4. The battles: ReMixed's reorganized battle entries (ENTD) go with its events, then the jobs, skillsets, presets
   and AI flags below. ReMixed renumbered its story battles; 68 story entries whose exact cast it moved to one
   other entry now give their units its builds from there (31 units: the generic enemies of 7 battles, such as
   Bariaus Valley and the Archaic and Ultima Demons of Riovanes, Elmdor II and Goland, and the special
   characters' builds: Delita and Algus as guests with ReMixed's gear and levels, Beowulf's Rune Blade and Aegis
   Shield in the colliery, Rofel's and Meliadoul's Divine Knight jobs, Zalbag's Battle Axe). Vanilla's palette
   stays (ReMixed's 0 is for its tint code), and vanilla's job where ReMixed's is 0 (its loader copies the job as
   it is, SCUS 0x8005B358; those are cutscene actors, Larg and Draclau).
   A scene and its battle with one cast may share ReMixed's entry (90 entries, 34 units in all now). And 22
   story battles whose cast ReMixed changed as well as moved are matched by how alike the casts are (special
   characters, generics by job, tiles, teams; clearly ahead of the next entry, both ways), and give the units
   surely the same their builds (23 units: Delita and Algus as guests through chapter 1, with fixed levels and
   gear and Algus an Archer, then a Knight; Gafgarion at Zirekile; Ramza's Move-Find Item at Orbonne; Riovanes'
   war trophies; the final battle's levels and Finger Guard); units ReMixed added or dropped stay as vanilla has
   them. The battle's entry is the world's 0x800D4574 (WLDCORE), set by the world code, the same in both games.
   Since then the matching knows a special character by name, so its alternate sprite sets are one (ReMixed has
   Mustadio's 0x16 where vanilla has 0x22, Agrias' 0x1E for 0x34, Gafgarion's 0x17 for 0x11; the battle data keeps
   vanilla's sprite either way), takes a tie with an identical entry, and pairs the generics left by sprite, job
   and team: 29 changed battles, 41 units, the guest chapters among them (Dorter 2, Araguay Woods, Zaland, Bariaus
   Hill, Zigolis Swamp, Golgorand, Lionel Castle Gate: Agrias' Gold armor and White Magic, Mustadio's Maintenance,
   fixed levels). ReMixed added few enemies to the matched battles (9 units in 3). Three more paired by reading
   their casts (REVIEWED_BATTLES: Yuguo Woods with Rafa's build, Poeskas Lake, the Chapel of St Murond Temple):
   45 units in 32 battles. The 12 left are scenes (Military Academy, Family Meeting, Besrodio Saved, Reviving
   Malak and others, where builds don't matter), the Deep Dungeon's end and unnamed entries.
   Original plan:    and AI flags below.

- [x] 273 story battles, scenes and tutorials, tied to ReMixed's event scripts: settled (2026-11-05). The
  remaster's story events stay; ReMixed's unit builds come over in 122 of its story entries (item 4 above), and its
  own events through the story port (Story port progress).
- [x] 16 jobs and 5 skillsets, settled (2026-11-12): rows ReMixed emptied once it had moved their characters to
  other jobs (scene actors such as the Duke, the Bishop and the Cardinal, Delita's Arc Knight, the Lucavi's
  Knight, Archer, Wizard, Time Mage, Oracle and Summoner, Mustadio's Snipe), and two it changed but uses nowhere
  (the Arc Duke, the Wildbow). None of ReMixed's battles or events makes a unit with them, and the remaster's
  story battles still do, so the remaster's rows stay. ReMixed's own monsters' sprites wait for the art port (the
  new characters' sprites, above).
- [x] ReMixed's AI flags (2026-11-12): its story units that hold a post (Defensive and Stay Near XY, with a
  battle-data bit) don't run away, neither as their way of fighting nor when Critical, a Frog or unable to act;
  in the port, an enemy Thief in side chapter 1-A's second battle and two AI-run units on your side in its last.
  And what an ability is worth to the AI against a target, which turned out to be for every unit: ReMixed's AI counts giving Protect, Shell or Wall
  (vanilla's never did), and doesn't count Berserk or Chicken on a target that's stopped or asleep. Ported
  offline and self-tested; not seen in the game yet.
- [x] Unit presets (2026-11-12): ReMixed's 24 presets, picked by a battle-data unit's byte 37, which its story and
  mission battles use: a unit that starts Dead (Hirame in 1-A's rescue), a corpse, an undead, a treasure chest,
  immunity to Invite (nearly every mission enemy), Monster Skill, stats and class evade scaled. The build sends each
  scenario's units' presets to the code mod, which applies them as ReMixed does whenever a unit's stats are worked
  out. Seen on the PC: Hirame Dead as the rescue begins, Treasure Hunt's chest and its Rogues immune to Invite. Not
  ported: their tints (dark, gold, grey, bronze; the art port). Since 2026-11-12 the remaster's own battles that take
  ReMixed's units get their presets too: Marach lies dead on the Rooftop of Riovanes as ReMixed's corpse, on his usual
  sprite slot (Luso has his old job and slot, for Luso's own art), and Gafgarion at Golgorand and Lionel's gate has
  ReMixed's immunities. Not seen in the game yet.
- [x] Weapon skillsets and monster lists (2026-11-25): Magic Blade, Spell Strike and Fellblade use the remaster's
  ordinary list and action path because it has no type-5 handler. Their targeting calls Attack's weapon range and
  area; their queued CT is zero, and their selected ability runs ReMixed's MA and weapon-power strike formula.
  Monster Skill and ReMixed's eight monster lists use the remaster's monster-list type 6. Elemental is ordinary
  instead of terrain substitution. Spell Strike's list, weapon targeting and a Fire strike were checked in a PC battle.
- [x] Some story battles skip the level options (2026-11-12): three of ReMixed's story events set their units up with
  the Enemy Level Boost, Enemy Job Settings and fixed-or-minimum levels off. Only one of those battles is in the
  remaster's story, the opening battle at Orbonne; its units ignore those settings now. The other two are battles of
  ReMixed's own in scenes the remaster plays without one. Self-tested offline.
- [x] The Argath campaign's way to its end (2026-11-12): roads on the world map where ReMixed shows them and the
  remaster doesn't (from Mandalia, where the campaign starts the party, on to Eagrose, then back to Gariland), the
  last battle's own conditions (it had none, so it couldn't end), and its last two hand-offs (on to the final battle,
  then back to the world map after the ending, where ReMixed's post-game rooms would go). The build walks the whole
  campaign now and stops on anything that would block it. Not seen in the game yet. *medium*
- [x] **The Depths of Murond post-game hub** (2026-11-15; ReMixed events 0x0A2 and 0x0A3): after ReMixed's final
  battle and ending, both the Argath campaign and Hirame's chapter-3 branch now open its welcome and credits, then
  its Completion Points rooms on map 116. The two events and their text use the free scenario numbers 0x1B2 and
  0x1B4; their self-return, New Game+, Continue+, regular ending and world-map exits are kept, and Reis's stat- and
  ability-preserving Morph changes her job without the event instruction the remaster port lacks. The build's offline
  campaign walk reaches the hub and its world-map exit. Seen in the game through the main hub, Spend CP, its named
  unit picker and a unit's upgrade menu; both story entrances, completing each kind of purchase and each exit still
  need an in-game pass. *large*
- [x] The Argath campaign's guests from a new game (2026-11-12): its battles and scenes take Delita (and Algus in two
  scenes, Alma in the final battle) as guests the story saved, which from a new game nobody had saved, so the second
  battle had an empty unit where Delita stands and crashed as it began (the regression pass). On the PS1 ReMixed makes
  him from his battle data, and the port does too now; the build's walk of the campaign checks it. Not seen in the
  game yet. *small*
- [x] Algus as your own unit in the Argath campaign's first battle, as on the PS1 (2026-11-15): 0x006's entry keeps
  ReMixed's load-from-roster and player-control bits. Because the remaster normally searches only guest slots for that
  load flag, the unit-maker bridge presents the Algus whom the opening swapped into roster slot 0 there temporarily,
  relinks the made battle unit to slot 0, then restores the guest slot. His menu, EXP, JP and battle-end write-back
  therefore stay with the campaign leader. Self-tested offline; not yet seen in the game.
- [ ] Six deployed units in three Zalbag and Algus battles (events 0x0FF, 0x100, 0x101: two groups of 2 and 4). The
  formation screen keeps how many were deployed (var 0x52, vanilla's too), and ReMixed's battle setup places up to
  nine of them where vanilla placed five; no other battle deploys more than five. Whether the remaster's setup takes
  a sixth isn't known yet. *small*
- [x] Move-Find's rare items locked until a story point per map (2026-11-12): before ReMixed's point for a map (the
  story progress against its table), a hidden item's panel gives its common item, and that find uses the rare one
  up for good, as in ReMixed. 92 maps have a point. Ported offline and self-tested; not seen in the game yet.
- [ ] Two abilities ReMixed turned into others (0x088, 0x0F8). Their combat formulas are ordinary/available, but
  handing them out is still deliberately blocked: ReMixed's 0x088 name is blank or undecodable in its source list,
  and 0x0F8 uses an animation tuple the remaster has no existing equivalent for. Substituting the remaster's old
  name or animation would expose a different-looking ability, so this needs a chosen replacement name/animation or
  new presentation support rather than a mechanics patch.
- [x] Unlock all jobs (2026-11-15): it widens which jobs generated enemies pick at random when either of the low two
  bits in var 0x6C byte 2 is set. Event 0x0A3, the Completion Points room restored by the port, carries those flags;
  the random-secondary hook now lends the remaster's picker ReMixed's 0xFEFFFF all-jobs mask for that pick and restores
  the unit's actual unlocked mask afterwards. Self-tested offline; not yet exercised from the room in game.
- [ ] Dispatch Missions (redesigned 2026-11-17, needs a Deck play-through): the faithful PS1 event-menu port was
  retired after repeated input lock-ins and crashes. The Tavern keeps its native Rumors and Errands entries, every
  one of its 96 errands, and its history. The extra "Dispatch Missions" Rumors posting opens a separate view using
  the remaster's controller navigation, roster picker, cancellation, time away, recall, and reports. From 0.9.7 its
  virtual catalog restores all 92 Propositions, four repeatable Jobs, all seven Special Training courses, and the
  current monthly Contest; active groups stay visible for reports and Recall. The menu follows the PSX hierarchy:
  five main choices, six Proposition kinds, then the first story-eligible set not yet completed; Back returns through
  each parent screen. Valor/Detection and Theology/Evolutionary change
  Brave/Faith by 3, Transitioning changes gender data, and Training/Retraining use the native JP report. Existing
  records recognizably made by the retired screen are recalled before the native day step sees them. The native
  Profit scan is bounded to its original 96 rows while the appended text table is active, avoiding event-variable
  ids beyond the engine's 0x3BF storage limit.
  The original faithful-port work below is retained as history and as a source for later mission-data improvements.
  ReMixed's original system bought Propositions of six kinds in sets and sent groups of up to three on them, with
  odds from the units' levels,
  Brave or Faith and jobs. It also has repeatable Jobs, Special Training (Brave, Faith, levels, gender), a Contest
  each month, and Recall. Groups come back after some days or a won battle, with gil, JP, items and treasures. In
  ReMixed you reach it from its mission list and its pre-battle menu; the port would post it in the taverns like the
  missions. The original five-phase plan is in analysis/dispatch-missions-plan.md. Its old decision to replace the
  remaster's Errands was superseded by the additive 0.9.6 design above. Read (Phase 0): the remaster keeps vanilla's errand records, day step and nibbles, so the
  tavern's groups go where its errands are. Built (Phase 1): its events run in the remaster, with their menus' rows,
  names and prices filled in as they show; seen on the PC, from its menu to Mining's first set, its prices and
  missions, and out to the world map. Then (Phase 2, in part): a group sent out, kept in the save, and the tavern's
  menus and boxes drawn as ReMixed's; a won battle brings groups home, and a Job's report pays (2026-11-12, seen on
  the PC). Posted from 2026-11-12 with a mission's reports (not seen in the game yet). From 2026-11-15 the remaining
  Special Training/Contest data paths are implemented too: job levels and live JobData (including relocated Bard and
  Dark Knight rows), mapped roster and battle stats, Transitioning writes, and the level-change routine. Formation
  and Eureka's Treasure Wheel are wired back to the renumbered tavern but still need their in-game runs. Entry from a
  battle's formation screen remains a separate Preparation Menu problem: leaving and resuming an arbitrary formation,
  especially a random battle with no scenario to return to, has no port mechanism yet. *large*

## Testing

- [x] Random/ReMixed battle results retain the battlefield (fixed and seen on the PC, 2026-11-17). Generated battle
  exits had jumped straight to display state 7, which tears down the map, so the results UI appeared over black as
  soon as the final enemy fell. They now run the stock state 2-6 sequence before the roster writer. A Mandalia
  victory showed “THE ENEMY IS VANQUISHED”, Spoils of War and gil over the still-rendered battlefield.
- [x] The end of chapter 1 offers a save before the 1-A / chapter-2 decision (built 2026-11-17). Event 0x0EC invokes
  the remaster's native save display state 8 immediately before the choice and closes it with state 0 afterward.
- [x] Luso's Mandalia/Araguay return scenes defer their roster writer to the scene's join (2026-11-17). This was
  meant to let the remaster's join screen show him, but the join it used then never shows one (found 2026-11-18, when
  Boco was reported joining with no screen); the scenes keep ReMixed's own join now, which does (Reported by a
  player, above).

- [x] Retest Luso's Mandalia encounter from the choice through combat and recruitment (fixed and seen on the PC,
  2026-11-15): scenario 0x7B hands to split battle 0x34D and its formation screen appears. The split originally reused
  the scene's ENTD carrier, retaining scene state: Ramza appeared as the guest, Luso and all nine enemies were absent,
  the plain-victory condition fired, and event 124 ran over a black screen. The battle now has a distinct carrier and
  setup receives ReMixed's inclusion flags 0xC1-0xCC before it prepares Luso and battle ids 0x80-0x88, then the opening
  visibly activates that prepared cast. A first fix used AddUnit without the pre-setup flags: the
  units fought, but unloaded graphic resources made the monsters invisible and made Luso draw as Ramza. Pre-setup
  construction followed by activation initializes graphics, placement and turn state together; the activation must use
  immediate-visible mode because the source scene's hidden additions were already revealed before this fresh battle.
  Carrying hidden mode across the hand-off left portraits and HP bars but no monster models. Raw unit-id restoration did
  not initialize the actors and crashed at a turn start. In the harness the
  fight waited for all nine enemies to be defeated, ran recruitment once, put Luso in roster slot 3, and returned to
  the world map without a new crash. The build audits all seven generated direct scene-to-battle hand-offs for distinct
  carriers, and the staging audit checks AddUnit groups in every generated upgraded event. A later Deck report
  (2026-11-16) found the split carrier still retained the opening scene's data-built Ramza (battle id 0x90), so the
  formation deployed the player's roster Ramza alongside a second guest Ramza in a corner. Split 0x34D now omits that
  scene-only record; recruitment resolves actor 1 to the deployed Ramza. All seven scene-to-battle carriers are also
  audited for any retained data-built Ramza. Seen in the hidden PC runtime with the same one-unit formation: only the
  roster-backed Ramza was present, Luso and all nine enemies remained, event 124 completed, Luso joined roster slot 3,
  the game returned to the world map, and no crash was logged. A second Deck report exposed a black map and blank
  Luso/Ramza boxes on Luso's first turn. Two independent hand-off bugs were fixed on 2026-11-16: the direct split now
  fades the opening scene's full-screen colour away, and every generated text event is removed from any stock
  ScenarioId row before receiving its own. Battle 0x34D had been claimed both by stock row 1813 (unrelated
  Orlandeau/Folmarv text) and ReMixed row 5140, so its correct line ids resolved against the wrong file. The ownership
  audit found the same latent collision at 0x375 and 0x376 (Hirame/Wright) and now requires one owner for all 122
  generated text events. Seen in the hidden PC runtime: map 85 remained drawn and Luso's message 51 and Ramza's message
  52 both showed their full text during combat. Recruitment event 124's later message 49 is now an exact harness
  checkpoint: it skips combat, queues phase 5 through the real condition walker, and captures that runtime message.
  The checkpoint exposed and then verified fixes for two enhanced event-art failures: 0x10's private alias originally
  sampled the unit sheet (split body parts), then its PSP texture number selected an unrelated palette companion (black
  silhouettes). The final frame has the visible Mandalia map, coherent coloured actors, Luso's full line, and no game
  crash. A 2026-11-16 hidden real-victory pass then captured natural recruitment messages 30/35/40/45/49 and the
  post-event frame: the map stayed rendered throughout, the actors' poses remained coherent and coloured, and no crash
  was logged. This is the 0.9.4 regression gate; a Deck retest remains useful.

- [x] Play battles and watch the ported rules (looked at 2026-11-12): the test mode's reports hold 35 battles played
  on the Deck (2026-11-04 to 11-07) and 60 run on the PC (to 11-11), with no errors. Seen at work there: ReMixed's
  formulas (the gun skills, the stat Breaks, Focus and the other stat raisers, the weapon skills, Gil Taking), Counter
  Steal (a counter that took gil and an armor), the monster reactions by family, Banditry's back attacks, First Move
  and Move-MP Up, knockbacks, Move-Find's finds, the Nemesis System remembering names, strikes reaching bodies, the
  level scaling, the EXP and JP rates, Two Spells' lists and the unlearning screen. The items below are what the
  reports haven't shown.
- [ ] The Argath campaign from its start to its end, in one run (for the regression pass; not seen in the game yet past
  its battle in the woods). Turn on Argath campaign under Configure Mod (Story) and start a new game: the chapel at
  Orbonne goes on to Algus's ambush in the tunnel (seen again on the PC, 2026-11-12, where the tunnel scene showed
  black figures the PS1 doesn't have: its 0x1B event graphic now uses ReMixed's redraw in a private slot, built
  2026-11-15 but not seen in the game yet; Argath's second battle crashed as it began: Delita's empty unit, fixed
  offline the same day). Seen on the PC after the fix (2026-11-12, late): Delita beside Algus in 0x006's battle, which
  plays through to Argath's introduction (0x007), back on the world map at Mandalia with the road on to Eagrose drawn,
  as on the PS1; then Dycedarg's scene and the bridge scene at Eagrose (0x008, 0x105, started there by the harness, as
  the PC can't travel the map), back with the story at 4. 0x00A's formation screen has Ramza kept out, and it won't
  commence: "Ramza is required but has not been placed." (Story port progress), so the run stops there. Still to watch
  for: the road back to Gariland (drawn at Eagrose, where the harness's start didn't take the party); each later
  battle's formation screen; the three Hokuten battles before Fort Zeakden, each with two groups (six units deployed in all);
  the last battle at Fort Zeakden ending both ways (won: on to the final battle; Algus falls and gives up: the
  remaster's end of chapter 1, with Ramza back at the head of the roster); the final battle, the ending, and the world
  map after it. Since 2026-11-13 its battles write their units back at their end, as ReMixed's do: Delita joins once
  after 0x006's battle, and the EXP and JP its units earn stay. *large*
- [ ] ReMixed's characters in the story characters' second sprites (built 2026-11-12, not seen in the game yet), beside
  ReMixed on the PS1: Ashley's sprite and face, in the Lich's battle (2026-11-12) and in her scene after it
  (2026-11-13), are seen on the PC beside the PS1; still to see: ReMixed's others drawn with its art where its own
  battles bring them (the ones in Gafgarion's, Rafa's, Celia's and Lede's seconds, the
  tutorial's and Poeskas Lake's slots, and its Bard's look); and the remaster's characters as before, sprites and
  faces (Agrias, Rafa, Celia and Lede keep theirs): Agrias as a guest in chapter 2 (Orbonne,
  Dorter, Zeirchele, Zaland, Bariaus Hill), leaving at Lionel and back for good at Bariaus Valley; Rafa as a guest at
  Riovanes and back after the rooftop; the enemy Gafgarion at Golgorand and Lionel's gate; Poeskas Lake's undead; a
  save from before with Agrias as a guest. Seen on the PC (2026-11-12): Golgorand's Gafgarion on his own sprite set,
  as the remaster's hooded Executioner. The guests need a save from chapter 2 or 3 with them in the party; the PC's
  test save is from chapter 1. *medium*
- [x] Ashley joining after her scene (fixed 2026-11-13): in ReMixed the Lich's battle has her as a guest it saves,
  its after-battle event writes the battle's units back into the roster (which puts her in a guest place), and her
  scene takes her from there and has her join. The port skipped that after-battle event, so the battle never wrote
  her back: her scene found no guest, left her a black figure with no face, and nothing joined (seen on the PC,
  2026-11-12, and 2026-11-05 before that). The Lich's battle now writes its units back as that event does before
  going on to her scene. Seen on the PC (2026-11-13), beside ReMixed on the PS1: her card in the battle says Ashley,
  Riskbreaker, Lv 25, Guest; after the battle she's in a guest place of the roster; her scene shows her own sprite
  and face; at its end "Ashley has joined your party" at Lv 25 with the battle's EXP kept, as the PS1's "JOIN UP!"
  shows, and she's in the party on the world map. The remaster shows its own join screen where the PS1 shows her
  status, and its pedestal draws Ramza (below, under the display port); the PS1's "Bonus Money" after the battle
  isn't shown (event 8's results screens aren't run). *small*
- [x] ReMixed's battles keeping what they leave behind (built 2026-11-13). Seen on the PC the same day, beside ReMixed
  on the PS1: a unit's EXP and JP set in the middle of a battle are in its roster record after it, in a mission
  (Desert Minefield, on through the Treasure Wheel to the world map) and in side chapter 1-A's battle with the six
  mages (written back as it's won, then its scene after the battle, then the world map with 1-A's story on). On the
  PS1 ReMixed's after-battle event writes them back at the same points (the Lich's battle before Ashley's scene, 1-A's
  rescue before its scene). The test mode's report now counts both battles as won (before, ReMixed's battles never
  reached the battle end). The Argath campaign's battles are left to its own run (below). *medium*
- [ ] The formulas ReMixed rewrote in place (2026-11-12, self-tested offline): Repeating Fist's damage (a trace line
  "formula 0x32"), Wish on an ally (the caster loses a fifth of its Max HP), Lava Ball curing its user, Seal Evil on
  an Undead or Blood-Sucked enemy, a Knight with Momentum breaking an item (the target knocked back), and a Counter
  Steal (its steal's YA is twice X).
- [ ] The dispatch tavern's last changes (2026-11-12, built and checked offline, not seen in the game yet): its
  posting among the Rumors ("Dispatch Missions", from Chocobo Defense's unlock) and going there on leaving town; a
  mission's own report (Propositions, then a group back: "Here's the info on Mission N" with the report under it, as
  on the PS1); Special Training's and Contests' description boxes; the rewards' job names in red, and a row for each
  unit of a group of two or three; Contests reading the units' job levels; the remaster's own Errands list empty in a
  town tavern ("No errands available."); and a group's days passing on the world map. Seen on the PC already: only
  Ramza at the counter while the unit picker is up, and a won battle bringing a group home. Compare each with
  ReMixed on the PS1 (work/psx/run/shots rmg-* for a mission's report). Seen on the PC (2026-11-12, evening): a Job's
  report with the job's name in red ("Mining", "Geomancer JP:"), as on the PS1; a mission's report for a group of
  three back from Mission 17 (the list, "Here's the info on Mission 17: "Abandoned Mine Expedition"", what the units
  earned and the gil, then a box per unit with a red row for each job's JP, two boxes up at once, and "That's all of
  'em"; the PS1's reference is a group of one). Not right yet: the mission's report under its line, and Special
  Training's and Contests' descriptions, show as bare text over the scene, with no box, and go before any press,
  where the PS1 puts them in a box under the Tavernmaster's line until a press (work/psx/run/shots rmg-00080,
  rgp-vt-*); Special Training costs 300 gil there and 100 in the PS1's save. Since (2026-11-12): those boxes framed
  in the bottom window under the Tavernmaster's line, closed after their press; seen on the PC the same evening for
  Mission 17's report and Valor Training's description, each in its box until the press. Not right yet: the box
  shows four lines and the PS1's fifth is missing ("There was more to it than we'd imagined!", and Valor Training's
  "But overcoming your fears can be hard without the proper conditioning.", its red words with it). Since: those
  lines are no longer cut into pages, so the box holds all of them (up to seven); seen on the PC (2026-11-13, the
  spot check) for Mission 17's report, all five lines in one box ending "There was more to it than we'd imagined!",
  as on the PS1. The 300 gil is ReMixed's own price for the save's later story section (prices rise with it, 100 to
  15,000; the harness sets it to 5, the PS1's save is earlier). Still to exercise: save/load and days from a town;
  Valor, Detection, Theology and Evolutionary changing Brave/Faith; Training and Retraining changing levels with
  the live job growths; Transitioning changing gender/job/equipment; all three Contest placings and JP; Formation
  returning to the tavern; and Eureka's Treasure Wheel returning to the reports. The Errands, posting and days need
  a town's tavern (travel on the world map), which the PC's runs didn't reach yet. *medium*
- [x] The test mode's RecordAndAutoBattle: the AI plays your units. Seen on the PC (2026-11-05) once the control bit
  is cleared in the base team (+0x1EE) as well as the current one (+5); with +5 alone the units stayed the player's.
- [ ] Evasive Stance after moving (asked from the Deck, 2026-11-07): it ends the turn, as Defend always has; ReMixed's
  "doesn't consume Act" ("next turn comes sooner") means the turn's end counts it as not acting. To check the unit's
  CT after a move and Evasive Stance against a move and Wait.
- [ ] The ReMixed tab of Settings with a controller on the Deck (Y, the Options tab, Settings, RB to the last tab):
  pick a group, change an option, leave and come back, then see it at work in a battle. Restore Defaults there
  should put the shown group's options back to the mod's settings (the PC's test run couldn't open its question).
- [ ] Reveal Treasures with a controller on the Deck: a battle on a map with hidden items left asks the price as its
  formation screen opens; A and B answer it (the PC's run answered with the keyboard), Yes stands ReMixed's arrows
  over the hidden items until the battle begins, and a later battle on that map shows them without asking, the gil
  still paid (on the PC, 2026-11-12, a second Chocobo Defense after the first was won asked again, its gil back as
  before paying: the battle's start put the payment back; the fix that keeps it was seen working on the PC in the
  regression pass the same day).
- [ ] Fast-forward as a toggle on the Deck's controller (asked 2026-11-18): with the mod's Fast-forward setting at
  Toggle, a press of the fast-forward button should turn it on and leave it on after it's let go, until the next press;
  the faster speed is that button held with another (the pad's 0x1E). Seen on the PC the same day with the keyboard
  (Araguay's battle, the hidden harness, the speed level read from GlobalWork): Ctrl turned ×3 on in the battle and in
  its opening scene and it stayed on after the key was let go; 5 switched to ×5 and Ctrl back to ×3; Ctrl turned it
  off, in the battle and in the closing scene; one turned on in the battle went on into the closing scene (×3 after
  the scene's own couple of seconds, as when held). A press in the second or so between a battle's states, when the
  game has no fast-forward, now waits for it (one was lost before). The setting is on the ReMixed tab of Settings too
  (its last group, Controls: Fast-forward, While held or Press on, press off), kept in the mod's settings for every
  save; seen on the PC: set there it went into Config.json at once and Ctrl toggled in the next battle with no restart.
  Still to see: the controller (the harness's LT and RT aren't the fast-forward button, game button 0x12), and the
  game's own Fast-forward option at Off.
- [ ] Random battles field one unit a tile (asked from the Deck, 2026-11-11: two monsters on one tile at Mandalia
  Plains): ReMixed's random-battle event took the unit flags an earlier battle had left set, and added the units
  of that battle's slots onto tiles the game had filled (analysis/remixed-event-opcodes.md, "Flags 0xC0–0xCF").
  It clears them first now. To see on the Deck: a few random battles in a row, on different maps.
- [ ] A battle resumed from its autosave keeps its enemies as saved (asked from the Deck, 2026-11-07): their HP went
  back to full, as Improved Level Scaling's re-run rebuilt them again at the first countdown (the Deck's test mode,
  Record, re-runs it every battle; its log showed the loaded enemies redone 4 → 4). The re-run now skips a battle set up
  without the formation screen. To see on the Deck: load a mid-battle autosave with hurt enemies.
- [x] ReMixed's characters' faces (built 2026-11-12). Seen on the PC the same day: in 1-A's rescue, Hirame's own
  purple-haired portrait in her speech box where a boy's showed, as on the PS1; Luso's at Mandalia (red cap) where
  Malak's showed; the Lich's in his lines and on his unit card at Zeklaus; Ashley's in her lines and on her card in
  that battle. The uncompressed pictures load and show. The rest is the next item.
- [ ] The faces not seen yet: Hirame's unit card (her revival plays the way where she lives again, 2026-11-13), the
  Dark Crusaders', a Lamia's and a Tonberry's, each beside ReMixed on the PS1. *small*
- [ ] The Fort Zeakden branch from a save where Hirame lived: its tavern rumor appears from chapter 3, names Hirame
  and Wright on their unit cards, disappears after the battle, and the whole branch plays through beside ReMixed on
  the PS1. *small*
- [x] Luso's in-battle Mandalia lines (fixed 2026-11-16): the hidden PC runtime showed Luso's introduction and Ramza's
  answer on the drawn map, with event 845 reloaded for its text and a unique ScenarioId owner.
- [ ] Algus's "Reinforcements? Thank God!" in the Argath campaign's second battle, beside the PS1. *small*
- [ ] Two Spells: a Calculator casts two cheap spells in one turn (Act stays open after the first), the second
  fires right after the first, and an enemy Calculator casts each spell twice.
- [x] Two Shots: an Archer queues it twice in one turn and both shots fire, the second right after the first. A
  player reported only one firing (2026-11-18): the second shot replaced the first whenever the unit had already
  moved, which a Charge always counts as (a Calculator's second spell after moving too). Fixed the same day and seen
  on the PC (tests/quick/two-shots.txt, the turn's two shots chosen by the harness's act): the second goes to the
  slot as the turn ends, the unit keeps Charging when the first fires, and the second fires on the next tick.
- [x] The Knight's Rends inflicted statuses (a player's report, 2026-11-19: a second Rend Helm on a helmetless
  target dealt damage and silenced; a Rend Armor turned a unit into a frog). ReMixed keeps an equipment slot number
  in the Rends', Steals' and Mighty Sword's status byte for its own slot pick; the remaster picks the slot by the
  ability's id but reads the byte as statuses, so the preview listed added effects and a Rend with nothing to break
  inflicted them. Those 13 abilities now keep vanilla's byte. Seen on the PC (`tests/quick/rend-helm-statuses.txt`):
  Rend Helm on a chocobo, with no helm to break, hit with the action's status set 0 and added no statuses.
- [x] Elemental Strike aims at range 7 with its own preview. A player reported it as a melee ability with a 0% hit
  chance (2026-11-19): the battle menu still handed Geomancy's choice over as "no ability", for the terrain to fill
  in as in vanilla, so the targeting and the preview were worked out for no ability (the panels round the unit), and
  the command read "Geomancy: " with the terrain's old ability (Chef's Knife, now). Fixed the same day and seen on the
  PC (tests/quick/elemental-strike.txt, through the menus): the command reads "Geomancy" with the
  Geomancer's help, Strike's targeting covers range 7, and its strike lands. The preview's hit chance over a foe
  isn't framed yet.
- [ ] Spot-check: Samurai's Two Hands innate, the four Geomancy abilities (each as chosen, on any terrain), and the
  Dark Knight slot's requirement (Knight 8 and Black Mage 8).
- [x] No "???" in the ability lists (asked 2026-11-12). Seen on the PC the same day: the Monk's Job Abilities and
  learn list in the Job menu (eight actions, Cyclone to Revive; the cursor wraps after Revive, its description
  matching), and View Abilities for the Thief's Steal and for Martial Arts as a secondary (eight each). Still to see
  on the Deck once deployed.
- [x] The learn screen: choosing a learned ability unlearns it and gives its JP back (no dialog, as on the PS1),
  and an equipped one stays. Works on the Deck (2026-11-04), in the remaster's own Job menu, where it took finding
  the ability list's A press; its price shows again after.
- [ ] The party menu: dismissing a monster with a Secret Hunt user in the party adds its item to the Poachers' Den,
  and a job change to Chemist takes off an equipped Throw Item (and to the Mediator, Peacemaker). (added 2026-11-04; needs a save with a recruited monster and a Secret Hunt user).
- [ ] Dragon Tech: a Lancer who has learned Lancet sees Dragon Tech after Jump and can cast it, Jump still jumps,
  and the Lancer's learning screen lists the four after the Jump Range +1s, Lancet at 600 JP. The JP costs are the
  build's first write of those columns, so check the build log too.
- [ ] Cone abilities: a Breath or Elemental Wave can be aimed at a tile off the caster's row and column (but not on
  a diagonal), and the cone points the way of the target.
- [ ] The party menu with a unit away on an errand: it can change equipment but not its job, and whether it can set
  abilities (ReMixed: no). Which menus let it in tells which lock-check caller is which (MECHANICS.md, Job and
  equipment locks).
- [ ] Riding in the battle menu: after boarding, take one mounted Move and use a rider action and a chocobo skill
  (including Choco Cure) in either order. Acting on foot first should leave only the chocobo skill after boarding.
- [ ] Riding: a unit with Pass and Train can stop on an enemy's chocobo and wins it at its next turn (it joins after
  the battle); without them it can't board one. The White Chocobo can be ridden.
- [ ] Move-Find: a unit with Move-Find Item finds an item on a hidden panel, and the same panel gives its common item
  again in the next battle on that map (vanilla: nothing).
- [ ] Errands: a special character or a guest sent on an errand comes back, and the errand reports as usual.
- [ ] The Nemesis System: after a won battle with a downed (not finished off) generic enemy, an enemy a few battles
  later has that name. The save's variable it uses is free: none of the remaster's 1,061 event scripts touches it
  (checked 2026-11-12), nor vanilla's events.
- [ ] The roster takes more than 24 units (the remaster's code allows 50; one guide says 24).
- [ ] Dark Knight: a man and a woman with Knight 8 and Black Mage 8 can become a Dark Knight, its list shows Night
  Sword, Dark Sword and the rest, and it can equip katanas; the Bard sings ReMixed's songs and men still can't be
  Dancers. Check whether the remaster asks more of its Dark Knight than the job levels. Waits for the job menu (above).
- [ ] Bonus EXP & JP on Victory (set it to Both): after a win each unit is a level up with its stats grown and has
  the JP in its job; after a loss and a retry, nobody got anything.
- [ ] Stolen equipment: an enemy Thief steals a party member's weapon or armor; after the win it's back in the
  inventory (and the battle-end hook runs once, after the results).
- [ ] The bow path: an archer shooting past an ally standing in the line of fire can hit the ally (vanilla picks
  the other arc).
- [ ] Reset Move against ReMixed's move undo: after a Teleport that fails its roll, Reset Move shouldn't let the
  unit roll again (ReMixed blocks it); and see whether Reset Move is offered after Move-HP Up, a found item or a trap
  (ReMixed allows it and undoes them).
- [ ] Hitting bodies: a Raise or Phoenix Down thrown, struck or shot at a body lands on it (an archer's shot at a
  body over a living unit's shoulder hits the body), and a body between the archer and its target doesn't block.
- [ ] Riding, the rest: a rider charging a spell can use Choco Cure and still casts the spell on time, whether or
  not it moved first; the chocobo gets the EXP and JP for its skill; when a rider dies, its chocobo takes its next
  turn where the rider would have. Found reading the code and fixed (2026-11-19): the spell was always lost, as the
  game's tidy-up after the chocobo's skill cleared the rider's charging. Seen in the hidden PC game since
  (`tests/quick/riding-charge.txt`, the harness picking the actions, no menus): Ramza, set on an enemy chocobo as
  its rider, began charging Fire, used Choco Cure (the chocobo acting for him and getting the EXP), had Fire back in
  the timeline as his turn ended, marked as having moved or not, and cast it before his next turn. Still to see by
  hand: the menus, a real move first, and a rider's death.
  Also see what ReMixed shows that isn't ported: the action window naming the chocobo, and a rider boarding a
  chocobo that has already moved (ReMixed counts the rider as moved).
- [ ] Trick Shot: the target window. ReMixed shows only Abandon or Weapon Guard there while aiming Trick Shot (the
  reactions it doesn't block); if the remaster's target panel shows a reaction at all, it still shows any. Display
  only: Trick Shot blocks the reactions either way.
- [ ] Errands: a special character (Mustadio, Agrias…) or a guest shows up as a candidate at the bar, and a story
  event that needs that character waits while it's away.
- [ ] One-handing: a unit with Equip Shield can equip an axe or a bow with a shield, and Optimize keeps the shield.
- [ ] Athlete and Pass: a unit with Athlete shows and gets one more Jump, with Pass one more Move.
- [ ] Equipment start statuses: a unit with a Stone Gun and a Ribbon doesn't start the battle Petrified.
- [ ] Check the party's equipment screen follows ReMixed's equip supports (a bow with Equip Crossbow, a katana with
  Two Hands).
- [ ] ReMixed's own sprites move with ReMixed's poses (2026-11-12, not seen in the game yet): the Lamia (in the Hydras'
  place; it moved as a Hydra before), the blond man in red (he moved as a monster), the purple-haired fighter and the
  knight in the red hood, and ReMixed's characters in four of the story characters' second sprites (Rafa's,
  Celia's, the tutorial's second and Poeskas Lake's first), each beside ReMixed on the PS1. The dragon and hydra in
  Brave Story were seen on the PC; still to see is round 7's opening, where Reis rides in on the dragon, once the
  rounds play through (Brave Story's first round, above). Seen on the PC (2026-11-12), with no PS1 save state to put
  beside them yet: Hirame (the purple-haired fighter) standing and walking in 1-A's rescue as on the PS1, and the Lich
  (the knight in the red hood) moving and acting in his battle. *small*

## Done

- The Dark Knight in the job menu on a second page of its own (the mod's since 2026-11-19, from cipherxof's GenericJobs before), with d-pad up and down and "Change Page" in the job menu's button bar (the game's UIButtonGuide table, screen 1023's slot 4). Seen on the Deck with GenericJobs (2026-11-04).
- Improved Level Scaling's default, deployed units only: the remaster builds the enemies while the formation screen is
  still up (seen on the Deck), so at a battle's first countdown each enemy that scaled with the roster's party level
  gets a level from the deployed units' highest instead, its stats redone by the game's own steps (base stats,
  level-ups, recalculation); its place and equipment stay. A setting picks deployed units (default) or the entire
  roster. Seen working on the Deck (2026-11-04: five deployed units, four enemies redone).

**Battle rules**

- ReMixed's small AI changes, settled: a monster's Attack record set to range 1 and a score that starts at 99 have
  no reader in the battle code, so nothing to port; the rest is its story units' AI flags (story port).
- Errands, the town menus, settled: ReMixed hides the pubs' entries for finished errands and errands under way (its
  finished count always reads 0), with no other report found in its code, so the remaster's own town menus and errand
  reports stay.
- The Nemesis System (setting, on by default): a downed generic enemy is remembered after a battle, and 3 to 5
  battles later its name comes back on a new generic enemy of the same gender; kept in the save's event variable 0x40.
- Unit presets, mapped: they come from a battle-data unit's byte 37 and only ReMixed's story battles use them, so
  they moved to the story port's list.
- Roster reorder, settled: the remaster's unit list and formation menu reorder units already.
- Bigger roster and unit storage, settled: the remaster's roster has 50 regular slots (and 4 for guests), more
  than ReMixed's 17 plus 10 in storage.
- ReMixed's Dark Knight is the remaster's own Dark Knight job (its gear, skills and unlock: Knight 8 and Black Mage
  8), for men and women; Bard and Dancer keep their own slots, with ReMixed's songs and the two-job innate. The job
  menu doesn't offer it yet (below, waiting for the display port).
- Broken gear at the Poachers' Den, settled as not portable: the remaster's den trades 96 monster carcasses
  (PoachItem, one common and one rare row per monster), and its saved counts have exactly 97 slots with other save
  data after them, so there's no room for broken equipment or katanas.
- Sexuality and Charm, settled: ReMixed's setting reaches Charm only through zodiac compatibility (already
  ported); the menu's other Charm differences have no code behind them.
- The party level that enemies scale to leaves out units away on errands (ReMixed's rule; vanilla counts the whole
  roster).
- Move-Find: a hidden item's panel gives its common item again in every later battle; its rare item only on its
  first find (by the Brave roll).
- Bonus EXP & JP on Victory (setting, off by default): after a win every unit on your side gets a level and/or ten
  times an action's JP base in its job, through the game's own level-up and JP add.
- ReMixed's own battles keep what they leave behind (found and built 2026-11-13; seen on the PC in a mission and a
  side chapter 1-A battle, beside the PS1):
  ReMixed ends each of its battles with an after-battle event that writes the battle's units back into the roster, as
  the remaster's own battles do at their end. The port skipped that event, so after a mission, a side story's or a
  campaign's battle the EXP, levels, JP and abilities learned, a unit lost for good and one invited were lost, and
  the after-battle rules (the Nemesis, Bonus EXP & JP, stolen gear back, the tavern's groups home) waited for the next
  battle. Every one of its battles now writes back at its end, as that event does: the missions before the Treasure
  Wheel, side chapter 1-A's (before their scenes after the battle), the Argath campaign's and the side stories'. Its
  results screens (Bonus Money, the spoils) aren't shown.
- Equipment stolen from your units comes back to the inventory after the battle (a roster unit or a guest you
  control; each item up to 99 owned and equipped).
- The bow path: the Arc solver no longer steers a shot around the shooter's allies (an ally in the way can be hit),
  and Jumping units count as obstacles.
- Move snapshot, settled: it's ReMixed's move undo (Cancel after moving puts the unit back, HP, statuses and a
  boarded mount included); the remaster's enhanced mode has its own Reset Move, so nothing is ported.
- Hitting bodies: a melee or spear strike aimed at a dead unit's panel reaches the body, and so does a bow shot
  (the body stands full height on the aimed panel, and blocks nothing elsewhere); sprite sets 0x4A and 0x4B are 24
  high for weapon paths, not 36.
- Brave lowers damage taken: a physical attack on a target under 50 Brave loses (50 − Brave)% (Bravery/Wariness
  setting).
- Knockback: Immortal units and the party leader can be knocked back (Knockback setting), only an attack's last strike
  knocks back, and knockbacks can land in water (falls cut or drown, Float takes none) and on lava (999). A body on
  lava or in deep water moves to a free tile.
- Weapon evasion counts for every unit, not only with Weapon Guard.
- Waterbreathing lets a unit standing in deep water react, and AI units with it don't keep out of deep water.
- AI units see Transparent units (vanilla's AI leaves them alone).
- A job change keeps a usable secondary: taking the job whose skillset was the secondary makes the old job's
  skillset the secondary (vanilla left it empty).
- A job change takes off an equipped reaction, support or movement ability that the new job has innately (vanilla
  only knew four job and support pairs, which ReMixed's jobs no longer match).
- Unlearning for JP: on the learn screen, choosing a learned ability unlearns it and gives its JP back (the JP refund
  setting: all, half or none), unless it's equipped.
- Dismissing a monster while a party member has Secret Hunt poaches it: its common or rare item, at the battle
  poach's odds (by the poacher's Brave with the Poaching odds setting), goes to the Poachers' Den.
- Momentum's knockback: a basic melee attack after moving is a critical hit with a knockback (no extra damage), not
  for counters.
- The Archer's gun skills: Gunk Shot (Darkness and Oil), Hook Shot (pulls the target a tile), Trick Shot (no
  reactions), Two Shots (two weaker shots a turn), Aimed Shot (no evasion) and Disruptive Shot (cancels charging),
  with ReMixed's charge times and XA bonuses, for Charge +1–7 too.
- Draw Out's katana break: a katana breaks only for Draw Out, never for a Samurai or with Maintenance or Adeptness,
  and with one roll per target until one breaks (none without a target, as in the remaster). Bizen Boat's MP damage goes through the damage rules.
- Two Spells (the Calculator's innate): extra White and Black Magic commands with spells of MP 1–15 and CT 1–5; a
  first spell keeps the action, the second charges alongside it and fires right after; AI Calculators cast each
  spell twice. Its learned Equip Sword, Equip Shield, Half of MP and Short Charge come with it.
- A strike that skips the evade check (a gun's Mug) can't count as from behind because of an earlier attack.
- Evasion and statuses: a Dead target has no evasion; a Transparent one keeps its evasion against magic while
  asleep or stopped, and dodges physical attacks from visible units unless disabled; a blind attacker's target gets
  +50 evasion (+25 against Concentrate or a Transparent attacker), a confused attacker's +25, in place of vanilla's
  doubled evasion.
- Magic damage: a Transparent target takes a third less, like Shell, and a Defending one another third less.
- Evasion by angle: from the side, a weapon's evasion needs Weapon Guard, and Weapon Guard with empty hands keeps
  the class evasion.
- An invited unit is yours to control for the rest of the battle.
- No minimum weapon range: bows, crossbows and guns can hit the nearest tiles (Minimum weapon range setting).
- Unevadable attacks (Transparent attackers), team-only targeting, same-team damage that heals, Defense and Warding
  Aura.
- Status chance tiers, with vanilla's 19% roll dropped where ReMixed drops it. A set whose statuses roll one by one
  rolls each at its tier's chance, where the remaster rolled 24%.
- Death counter: units are lost for good by default, with the Player Deaths (revive after battle) and General Deaths
  (crystal or treasure) settings.
- EXP and JP rates (100% and 125%) and Spillover JP (a quarter by default), without Gained EXP Up's and JP Up's boosts.
- Power Modes (Knight, Squire, Tactician, Chemist or Custom percentages) and Cast Time ("Cast instantly").
- Poison and Regen on MP, Auto Renew and Starting MP (settings, off by default).
- Zodiac compatibility by gender, as on the PS1 (Sexuality and Zodiac compatibility settings).
- Poach odds by Brave: a low-Brave poacher gets the rare item more often (Poaching odds setting).
- Learning an ability on hit through the secondary skillset, and a unit the action reaches again keeps its chance
  to learn. Seen on the Deck (2026-11-18): Blue Magic learned by a unit that isn't a Blue Mage, with Blue Magic as its
  secondary.

**Abilities, jobs and supports**

- 13 reworked formulas, including Slap lowering the target's CT by 40 and Geomancy by terrain.
- The formulas ReMixed rewrote in place (2026-11-12): a Knight's Break that Momentum helped knocks the target back,
  Repeating Fist's total goes through the damage rules (Power Modes and the damage supports), Wish and Energy cost the
  caster a fifth of its Max HP and heal two fifths (a dead target gets Phoenix Down's formula instead), Lava Ball heals
  a fifth and cures, Seal Evil petrifies Blood-Sucked units as well as the Undead, Counter Steal's steals have double
  the chance, and Steal Exp gets Banditry's +10. Draw Out's, the "Back" skills' and the punches' changes were already
  in.
- Stat and status abilities follow the ability's data, as in ReMixed: the Knight's Breaks, Talk Skills (Rally raises
  CT by 30), songs and dances (healing and damage by (stat × Y) / 8), the Alters (up for allies, down for enemies),
  and Secret Fist, Eye Gouge and Beak as statuses. Maintenance blocks the lowering ones. Before this the Breaks and
  Alters were far off (low hit chance, stats down by 50 or more) and Rally stole gil.
- Focus, Tailwind, Steel, Shout and Gather Power raise the stats their data names (formula 0x3B, ReMixed's: Tailwind
  Speed, Focus PA, Steel Brave by 5). Before this Tailwind raised Brave by 0 and PA, MA and Speed by 1 (seen in the
  game, 2026-11-07), and Focus raised PA, MA and Speed.
- The Knight's Head, Armor, Shield and Weapon Break always deal their damage: in full with nothing to break, and
  without the break on a target with Maintenance (vanilla missed; the remaster halved the damage with nothing to
  break).
- Elemental is an ordinary skillset: Geomancers pick Elemental Strike, Lance, Pulse or Wave from the list (the remaster
  swapped the choice for the terrain's ability, which was often a different skill now), and the battle menu aims the
  one picked, with its range and preview, under the plain "Geomancy" (2026-11-19).
- Talk Skills work on monsters with Peacemaker (ReMixed moved that from Monster Talk, now Vehemence).
- Return 2 misses a target with Maintenance; Worker 8's skills go through the damage rules and can inflict
  statuses (Compress).
- Adeptness, Vehemence and Momentum's damage; First Move, Move-Cleanse and MP Rage.
- Banditry: Thieves' back attacks are critical hits, steals get +10% with it, and AI Thieves use Mug.
- Gil Taking steals once per use, as in ReMixed (the port stole twice before).
- Beaking lowers Speed, PA and MA, and can be dodged like an attack.
- Phoenix Down follows ReMixed's new check: Immortal story units are always revived, even Undead ones, and a
  vampire (Blood Suck) gets its current HP back instead of a random revive.
- Move-Find Item's learned movement: Move in Water, Fly, Float and the best Jump+N the unit has learned.
- Monster Skill and Train from anywhere on the field.
- Limit charges faster when hurt, in the menu and its turn-order preview too.
- Jump rework: the Lancer's Jump plus five Jump Range +1, up to range 8, at almost any height.
- Dragon Tech: Lancet and Ice, Fire and Thunder Breath, learned in the Lancer's Jump skillset, are used from a Dragon
  Tech command after Jump (greyed until one is learned); Jump's own list holds Jump alone, and AI Lancers use them too.
- Weapon requirements for 53 abilities, in the action menu and for the AI.
- AI units with two weapons plan their Attack with the right hand's (vanilla took the left's).
- Cone areas: the Breaths, Elemental Wave, Cleave and Abyssal Blade hit a cone in front of the caster (a Breath 4
  tiles, Elemental Wave 9) where the remaster hit a straight line. They can aim at any tile in range off the
  diagonals (not only the caster's row and column), and their targets are hit ring by ring. Line abilities can't
  target the caster's own tile.
- Movement abilities in their ReMixed slots: Terrain Master (0x1F6) stops on lava where Momentum (vanilla Move on
  Lava's slot) did, and Move-Cleanse (vanilla Any Ground's slot) no longer crosses any ground at a cost of 1.
- Pass (vanilla Silent Walk's slot) lets a unit move through enemies, AI units too.
- Movement modes: Waterbreathing moves in water like Swim (so ReMixed's Chocobos keep their water movement), and
  so does a rider; Chicken and Frog lose Teleport as well as Fly.
- Guest Control: with the setting at Always, every guest on the player's side is the player's to control (the default,
  guests deployed by hand, waits for the bigger roster).
- A charge started before moving locks the move: a charged ability aimed from where the unit stands (by weapon
  range, an area round it, and the gun skills) uses up its move, unless it has Non-charge.
- Every post-move ability applies, and Move-Get Exp and JP use the rates.
- Two-job stat growth: the Bard's and Dancer's innate.
- Equip supports: what each support lets a unit equip (bows with Equip Crossbow, katanas with Two Hands, everything
  with Armsmaster).
- Arrow Guard blocks guns too.

**Reactions**

- Counter Steal (was Gilgame Heart): a counter that steals.
- The Reflect reaction (White Magic's) reflects spells like the Reflect status, and a reflected spell lands back on its
  caster's panel (vanilla's went as far again past the reflector).
- Composure (was Finger Guard) gives 30 CT when it blocks a Talk Skill.
- An equipped reaction wins over an innate one.
- Monster reactions by family: a monster with a random reaction gets its family's (Chocobos Counter, Behemoths
  Hamedo…).
- A human with Move-Find Item reacts at least half the time: every reaction that rolls on Brave, and the guards show
  that chance.

**Enemies, units and gear**

- Job and equipment locks by the errand byte: a unit away on an errand can re-equip but not change job; locked
  units (0x40) can do neither.
- Riding, step 3: a charging rider that uses its mount's skill keeps its charge; the mount gets the EXP and JP for
  its skill; a dead rider's mount takes over its CT. With steps 1 and 2, the riding rework is in.
- Riding, step 2: a rider can use its mount's skillset once a turn as an extra action (instant, the mount as the
  attacker); after its own action, that's all it has left.
- Riding, step 1: the White Chocobo can be ridden, an enemy's chocobo only by a unit with Pass and Train, and a
  rider with Train wins an enemy mount at its turn start.
- Special characters and guests can go on errands, and an event's "in the party" check skips anyone away.
- One-handing: with Equip Shield or ReMixed's 0x1CC, axes and bows go with a shield or another weapon (the menus,
  optimize and random gear).
- Athlete adds 1 to Jump and Pass 1 to Move.
- A recruited monster keeps its reaction, support and movement through the party menu.
- A split formation's second group can be left empty.
- The ability lists mark a job's innate abilities as not equippable by the job's own data.
- Units away on an errand sit out the daily monster step (no breeding or hatching), and a poacher away on one doesn't
  poach a dismissed monster.
- Equipment start statuses: an item's start status doesn't take on a unit immune to it (the Stone Gun's Petrify with
  a Ribbon), and innate statuses count as current.
- Six characters' starting ages as ReMixed has them (new games).
- Random gear as ReMixed picks it: the best item of a random type the unit can use, Iga and Koga Knives in pairs, and
  the Rare equipment setting (rare items on random units; then no stealing them and no invited humans joining).
- The random weapon follows ReMixed's equip supports.
- Steals and breaks refuse an item that can never be taken.
- Job Level Settings (more jobs at level in later chapters), Enemy Job Settings and the random secondary skillset's
  three tries.
- A battle unit's fixed ability of the wrong kind is picked at random, and a fixed one is marked learned, so an
  invited unit keeps it.
- Fixed or Minimum Levels and Enemy Level Boost (settings).
- 230 random battles ported whole.

**Settings and data**

- ReMixed's Extra Options in the game (asked 2026-11-11): each save keeps its own where ReMixed keeps them (flags
  0xD6-0xF9, vars 0x14 and 0x16, a bit of var 0x40), a new save taking the mod's settings in Reloaded-II (Configure
  Mod, at ReMixed's defaults out of the box). They're changed on a ReMixed tab of the game's Settings, laid out as
  ReMixed's own menu: its first row picks a group (Levels, Jobs & Gear; Permadeath; JP and EXP; Spellcasting; Combat
  Adjustments; Nemesis System) and the group's options follow, with ReMixed's wording. Seen on the PC: the tab, the
  groups, a change kept in the save and after leaving Settings, and the game's own tabs as they were. ReMixed's
  Monster Reactions option turned out to be read by nothing, so it has no setting.
- JP costs: ReMixed's 90 changed prices (Lancet 600, each Breath 150), in the English ability table the game charges
  from (other languages keep the remaster's).
- Data: jobs, skillsets, abilities, items, weapons, equipment bonuses, status sets, statuses, Move-Find items, names,
  animations and visual effects.
