# Testing the mod in the game

The code mod has a test mode. It watches each battle you play and writes a report page: which of ReMixed's mechanics
fired, any errors, what hasn't been seen yet and how to make it happen, plus a checklist of things only you can check
(menus, what's shown on screen).

None of the mod has been tried in the game yet, so the first battles are the real test.

## Turning it on

1. In Reloaded-II, select "Ivalice ReMixed", then **Configure Mod**.
2. Under **Testing**, set **Test mode**:
   - **Record**: you play as usual; the mod records what happens.
   - **RecordAndAutoBattle**: the same, and the AI plays your units too, so a battle plays itself. Untested so far:
     if your units still wait for orders, use Record.
   - **Off** (the default): nothing is recorded.
3. Start the game from Reloaded-II and play battles. On the Steam Deck, the **Reloaded-II - Ivalice ReMixed** entry
   opens these settings from Gaming Mode (STEAM_DECK.md); close it, then start the modded entry. Random battles on the world map are the quickest way in.

The test mode doesn't change the game's speed.

## Reading the results

After every battle (and when the game closes) the mod writes two files to its settings folder in Reloaded-II,
`Reloaded-II\User\Mods\fftivc.gameplay.ivaliceremixed\` (on the Deck, under
`~/Desktop/Reloaded-II - FINAL FANTASY TACTICS - The Ivalice Chronicles/`):

- **TestReport.html**: open it in a browser. From the top:
  - **Errors**: anything the mod logged as an error, by battle. This should stay empty.
  - **Mechanics seen**: each mechanic that fired, in how many battles, with example lines.
  - **Not seen yet**: the mechanics still to try, each with a way to make it happen.
  - **Battles**: one entry per battle, won or not, with what fired in it. "Outside battles" holds what happened in
    the menus (job changes, unlearning).
  - **Hand checks**: the Testing list from [GAMEPLAY_TODO.md](GAMEPLAY_TODO.md). Tick them as you go; the ticks are
    kept in that browser.
- **TestResults.json**: the same results as data.

A battle you lose, retry or quit is recorded as not reaching the battle end.

## Rerunning

Play another battle: its results are added to the report. To start over, close the game and delete
`TestResults.json`. The last 60 battles are kept.

If something looks wrong, the Reloaded-II log (its console, or `Reloaded-II\Logs\`) has a `trace` line for each
mechanic as it fires; the report's examples are taken from those lines.

## The offline check

Each build also runs the offline check (`python -I tools/build_mod.py`), which tests the code mod against your copy of
the exe without starting the game. The test mode has its own checks there, on a made-up battle.

## Test saves

`python -I tools/make_story_regression_saves.py enhanced.png out-dir` makes two one-slot saves for the Luso story
regressions. `before-gariland.png` loads at Mandalia at story progress 4; enter adjacent Gariland for Biggs and Wedge,
then verify it returns to Gariland without a Mandalia battle in the sky. `before-araguay.png` loads beside Araguay
Woods at story progress 0x10 with Luso already met; enter Araguay, then verify the battle and his scene leave story
progress at 0x11 with Araguay no longer mandatory. The source save is untouched; `--from-slot` chooses the party
copied into FILE01.

`python -I tools/check_story_regressions.py` checks the generated event bytecode without launching the game. It walks
all generated handoffs and simulates the exit-state writes for Araguay, Luso's thanks and Mandalia; the normal mod
build runs the same check and fails if those states regress.

`python -I tools/make_test_saves.py enhanced.png out.png` adds two slots to a copy of your manual save (the slots you
have stay as they are), made from FILE03 (`--from-slot` picks another):

- FILE04: Ramza with Secret Hunt, the first generic with Throw Item equipped, and a Chocobo (Boco) in the roster: for
  the dismissal poach and its message, the job change to Chemist taking off Throw Item, and riding.
- FILE05: the same with a Black Chocobo (Kuro) too.

The save's layout is in the tool's notes (50 slots of 0x9CE4 bytes after a 16-byte header with a CRC32; a slot's
roster at +0x518). On the Deck the saves are in `Documents/My Games/FINAL FANTASY TACTICS - Ivalice ReMixed/Steam/
<id>/enhanced.png` inside the game's Proton prefix; back it up before putting a test save there (2026-11-04: in
`~/Downloads/ivalice-remixed/save-backup-before-test-saves`). Close the game without saving first, or it writes its
own copy back.

## Driving the game from the PC (for the mod's own tests)

`tools/harness.py` runs the game on a hidden desktop of its own (muted, never on screen, no focus taken) and drives it
through the mod's pipe; the notes at the top of the tool list every command, and docs/analysis/story-flow-plan.md
("Testing without the Deck") says how it works. Always `stop` a started game when done.

The enforced order is:

1. Run the offline build and audits.
2. Use `checkpoint` for exact event phases and dialogue screenshots. Its default direct-phase mode does not play the
   battle; `--victory` is the targeted gate for a real victory-condition transition without taking turns.
3. Run `battle --integration` once, only after the targeted checkpoints pass.

`battle` refuses to run without `--integration`, so a screenshot or event bug cannot accidentally start another slow
end-to-end playthrough.

```bash
python -I tools/harness.py start --world
python -I tools/harness.py checkpoint 34D 7C 5 --messages 30 49
python -I tools/harness.py checkpoint 34D 7C --victory --advance-phase 2 4 --dismiss-end-phase 2 --messages 30 49 --after-event
python -I tools/harness.py battle 34D --integration --units 1
python -I tools/harness.py stop
```

Tavern Dispatch coverage uses a disposable startup location and one targeted command:

```powershell
python -I tools/harness.py start --world --location 2
python -I tools/harness.py dispatch all --file work/harness/shots/dispatch-all.png
```

`start --location N` patches all three serialized world-location fields and the checksum in the restored private
harness copy of the manual save, and disables that run's restored autosave so Continue uses it. The checked-in
baseline and the player's PC/Deck saves are untouched; the next `start` restores the baseline again. This provides a
generic, real startup teleport without mutating live world-map memory.

`dispatch` waits until the boot autopilot has stopped sending input, selects the party's current Eagrose node, enters
its Tavern once, and opens the native Dispatch view without traversing Rumors. `all` validates the five-row Dispatch
main menu; `propositions` validates its six kinds; `jobs`, `training` and `contests` open those leaf lists directly.
Row totals, kind totals and unique virtual keys must match before the one final screenshot is written. The player path
continues from a Proposition kind into the first story-eligible unfinished set. `--story 0..16` and `--month 1..12`
select the test state. The view closes
after capture so another category can run in the same game; `--leave-open` keeps it visible for manual inspection.
There are no intermediate screenshots.

Once a ReMixed battle has commenced, a later phase can be tested without waiting for its turn, HP, or story
condition. `event` queues the phase through the battle's real condition walker, waits for its first message, and takes
a screenshot while that box is open:

```powershell
python -I tools/harness.py event 34D 2
```

This only exists while the harness's private `autopilot.txt` marker is beside the DLL. It preserves the battle's
loaded map and units and uses the ordinary event loader, scenario text, sprites, and interpreter; it bypasses only the
condition that would have selected the phase. The screenshot is written under `work/harness/shots/` by default.

`checkpoint SCENARIO EVENT PHASE --messages ...` wraps that fast path: it opens the carrier and formation, advances
only its opening, queues the requested phase, confirms boxes at 0.25-second intervals, and pauses to capture each exact
runtime message number before pressing again (six seconds by default, so the target line finishes typing). It neither
defeats enemies nor traverses the closing/world-map chain.
For Mandalia's post-battle dialogue, `checkpoint 34D 7C 5 --messages 30 49` captures the first staged line and the old
black-background line in one targeted run.

When the bug is specifically in the real victory-to-closing handoff, omit `PHASE` and pass `--victory`:

```powershell
python -I tools/harness.py checkpoint 34D 7C --victory --advance-phase 2 4 --dismiss-end-phase 2 --messages 30 49 --after-event
```

That uses the same carrier/formation/opening setup, marks every enemy-team battle unit defeated through the harness's
ordinary `defeat` primitive, then waits for the battle condition walker to load runtime event 0x7C. Mandalia can run
intervening carrier phases 2 and 4 before recruitment: `--advance-phase 2 4` permits confirmation only while their
dialogue is active, and `--dismiss-end-phase 2` captures phase 2's result notice before permitting one confirmation
at its stable `END`. With `--prefix work/harness/final/mandalia`, the proof frame is written as
`work/harness/final/mandalia-result-phase-2.png`. No other battle input is sent. For a target that reuses the carrier's runtime event number,
`--condition-phase N` is required so a fresh reload of an unrelated phase cannot be mistaken for victory; use
`--runtime-event HEX` when the target differs from the source event argument. Once event 0x7C is running it captures
the requested messages normally. `--after-event` then confirms through that event
and writes `checkpoint-event-07C-after-event.png`, stopping on its completed `END`, its handoff to another event, or
the first world-map frame; it never presses a key on the world map. `--prefix PATH` also names this frame
`PATH-after-event.png`. Use `--enemies 3 4 ...` only when a scenario needs selected battle-unit indexes instead of all
enemy teams. `PHASE` and `--victory` are mutually exclusive, and `--enemies` is rejected outside victory mode.

For a quick spot check after a merge, `python tools/smoke.py` (no `-I`: its contact sheets need Pillow) runs five
of the riskiest areas in one game session, each from the test save's world map: a mission start to finish, the Lich's
battle and Ashley's scene, the dispatch tavern's report, 1-A's rescue with Hirame revived, and the Argath campaign's
start on to its 0x00A battle's formation. It writes one contact sheet per check and a log to work/harness/smoke/ and
stops the game; `--only` picks checks. About 50 minutes (2026-11-13), half of it the Argath check.

`battle --integration` plays one of ReMixed's scenarios through: starts it from the world map (as the world map starts its story
events), places units, commences, presses Enter through its opening, defeats its enemies, then presses Enter through
its closing and whatever follows until the world map runs again, printing each event and scenario change. 1F2 is a
mission (Desert Minefield), so `--second 1` places a unit in its second group too: a mission won't start with one
empty.

As the formation screen opens (45 to 78 s after the scenario's setup in the runs so far), Reveal Treasures asks its
price on a map with a rare item still hidden, in the box that takes the formation's F. `battle` waits for its log line
(up to `--formation-wait`, 90 s) and declines it (Down, then F) before placing anyone; `--reveal yes` pays instead and
waits for its arrows. A map shown for free gets a few seconds for its arrows; a map with nothing left hidden says
nothing, and the wait runs out. A mission's win goes on to the Treasure Wheel (event 872). Its lines go on without a
press, so Enter opens the chest in front and reads its prize, and then changes nothing. Once two presses in a row leave
ReMixed's code at the same instruction (`state`'s "event at"), `advance` presses Right to turn the ring to the next
chest, and Enter opens that one. When its tries are spent, the Wheel ends and the world map runs again. Space would
cash in the rest instead, but a Start press can also open the remaster's Pause in a scene (seen once with a pad), and
Right does no harm if it comes before the Wheel waits.

Seen on the PC (2026-11-12): `battle 1F2 --integration --units 1 --second 1` ran to `done:` on the world map with no hand presses.
Reveal Treasures was answered before any placement, the battle commenced at the first try, and the Wheel waited at the
same instruction after each chest (0x1982, its one-frame wait). Two turns opened its three chests, and the world map ran
166 s into the closing. Down came 2 s after the question and was lost, so the F paid. The harness now waits 5 s
before answering, as the declines by hand did. A second run the same day declined (Down 6 s after the question) and
went through to the world map again. Its Wheel also held one instruction through its opening (0x11E0) for two
presses, so one Right came before the Wheel waited; it did no harm (three chests opened, as in the first run).

Its steps also run alone: `formation` (`--reveal` too), `advance --until opening|world|"event N"` (it never presses
Enter on the world map, where Enter searches for enemies), `defeat` (the enemy team, or the unit numbers given), `wait
PATTERN` (a log line from now on). For looking around: `send units`, `send state`, `roster` (the remaster's roster records), `slots` (the
battle units' roster slots, flags and battle ids), `shot`, `log`, and `crashes`, which reads the game's crashes from Windows' Application log and names each address's function (the
mod's own crash.txt misses a crash that .NET reports first). To find what writes a place in memory, `send watch ADDR
[LEN]` sets a hardware write watch on it (up to four at once), `send watch` lists the writes caught (the instruction
after each, with the exe addresses on its stack, for `tools/exe.py func`), and `send watch off` ends them.

The world map reads its buttons from the pad's own words, which Steam Input fills only from a controller, so a key
sent to the hidden window never reaches them. `send worldpad BITS [FRAMES]` holds buttons there for a few frames
instead (Start is 0x08000800). A mission's posting is tested that way: in a tavern's Rumors, on the posting, `send
worldpad 0x08000800 4` accepts it (the log says so), and Escape out of the tavern and the town starts its battle.
`setvar 0x6E 24` first lists the postings up to Lost Heirloom.

### Quick checks while developing

Use the incremental paths instead of rebuilding everything after every edit:

```powershell
# C# only: incremental compile, the full offline code check, and DLL install. The game must be stopped.
python -I tools/build_mod.py --code-only --install

# Generated tables/events only: reuse the last compiled DLLs and avoid the C# build/check.
python -I tools/build_mod.py --data-only --install

# Start one hidden game and run a named, machine-checked case. It stays running unless --stop is added.
python -I tools/quick_test.py --start tests/quick/lorne-scene.txt
```

After a data-only build, restart directly into the next case. The boot autopilot confirms every second and stops as
soon as world-map frames run, instead of spending roughly a minute on four-second fixed waits:

```powershell
python -I tools/quick_test.py --restart tests/quick/araguay-entry.txt
```

Each case prints one `PASS` or `FAIL` line per expectation and a final elapsed time. Case files use one small command
per line: `scenario`, `setvar`, `send`, `advance`, `formation`, `defeat`, `key`, `shot`,
`wait unit|entry|log|world|seconds`, and `expect unit|entry|roster|var`.
Each `wait log` consumes a matching line logged during the current case, so earlier runs in the same game cannot pass it.
`wait log PATTERN press=RETURN every=1` confirms through a scene until the line comes. `key K [times=N] [wait=S]`
posts a key to the game's own window; before each key in a menu, `wait world depth=D state=S` waits for that menu
(the world map is depth 1, state 0; Esc opens Units at depth 2, state 1), so a key never lands on the wrong screen.
`shot NAME` saves `work/harness/quick/CASE/NAME.png` at that step, for checking by eye, and every case also leaves
`pass.png` (or `fail.png`) there as it ends. Frames are evidence only: no step reads them or chooses a key by them. Put a
`shot` where the thing checked is on screen, for example after the line the tested character speaks (`wait log
"message (8|20) shown" press=RETURN`; the numbers are ReMixed's message ids, the second set for its other translation).
Set state rather than play to it: `send poke` writes a roster record as a scene would leave it
(`tests/quick/party-looks.txt`, 17.5 s), `scenario N noted` starts a scene-first battle at its battle
(`araguay-battle.txt`), `defeat` downs the enemies, `expect roster name_id=N` checks a recruit joined. Scenes still play
in real time (`tests/quick/lorne-scene-shots.txt`, about 2 minutes; the scene in `roster-loads-knights.txt`, about 4).
Integer values are decimal or `0x`-prefixed. `entries` exposes each raw 40-byte battle-data record,
including its unit-selection `flags`; `units` exposes the live job, graphic, palette and battle id. Screenshots are
still for a final visual review, not a prerequisite for these checks.

To check the Dispatch navigation soft lock, start the disposable Eagrose fixture and run the full catalog check:

```powershell
python -I tools/harness.py start --world --location 2
python -I tools/harness.py dispatch all --cycles 10 --settle 0 --timeout 5
python -I tools/harness.py stop
```

Each cycle uses Confirm from the main page through Propositions to Salvaging, then Back twice. The check waits for the
native list and input to be ready at each step. It takes one screenshot after the last cycle and checks that Back closes
the overlay.

The generated scenario tables cannot be replaced safely inside a running game, and code changes cannot replace .NET
or installed hooks in process, so both use the fast clean restart rather than a fragile hot reload.

## The PS1's ReMixed on the Deck and the PC

To see what ReMixed itself does, `tools/deck/psx/` runs the PS1's ReMixed v1.2.64.23 headless on the Deck (nothing on
its screen; a fraction of one CPU core): a small libretro frontend driving RetroArch's SwanStation core (the
Flatpak's), the US BIOS from ~/Emulation/bios, and the build's patched disc (`tools/remixed.py prepare`), copied to
~/Emulation/roms/psx as "Final Fantasy Tactics ReMixed v1.2.64.23" (playable in DuckStation or RetroArch too). Setup,
once:

```bash
ssh deck 'mkdir -p ~/psxrun/system && ln -sf ~/Emulation/bios/SCPH-5501.BIN ~/psxrun/system/scph5501.bin'
scp tools/deck/psx/psxrun.py tools/deck/psx/psxcommon.py tools/deck/psx/step.py deck:psxrun/
```

Then each step loads a save state (or boots with `-`), runs its commands and saves: `step.py`'s notes list them
(frames, buttons, films of shots, RAM, `defeat` for a battle's enemies, the world map's window callbacks counted).

```bash
ssh deck 'cd ~/psxrun && python3 -I step.py - "film boot 2400 600; save title"'
ssh deck 'cd ~/psxrun && python3 -I step.py title "tap UP 10; tap CIRCLE 10; film new 3000 150"'
scp 'deck:psxrun/shots/*.png' work/psx/ && python tools/deck/psx/sheet.py work/psx/sheet.png 5 1 work/psx/new-*.png
```

The way in, as found (2026-11-06): the title's cursor starts on Continue (Up once for New Game); Start held skips
ReMixed's scenes but pauses the opening movie, which has to play out (about 9,000 frames); the name screen needs the
cursor walked to END (Up, then Right five times); a battle starts with ReMixed's Preparation Menu (Start Battle, then
Down to Yes); a formation ends with Start; `defeat` then Circle ends a battle. Saved states of that run are in
~/psxrun/states: `wm` is the world map just after the second battle.

### On the PC

The same tools run on the PC (2026-11-11) with Steam's RetroArch SwanStation core
(`swanstation_libretro.dll`, from buildbot.libretro.com's Windows nightlies, in its `cores` folder) and a run folder
`work/psx/run` with the Deck's: `system/` (the BIOS as scph5501.bin and the core's memory card), `states/`, and
`shots/`. A save state names its disc and the core takes it back only for a disc of that file name, so
`psxcommon.py` gives the build's disc (`work/disc/remixed`) a .cue under the Deck's name; the Deck's states and card
load as they are. Run it from the repo, a step at a time:

```bash
python -I tools/deck/psx/step.py gb-2400 "run 10; tap DOWN 20; tap DOWN 20; tap CIRCLE 4; film rt 600 40"
```

Circle confirms and Cross cancels, as in the US PS1 release: Cross on the Preparation Menu closes it into View Map.
States from the PC's run: `rt-question` (Reveal Treasures' price asked), `eo-menu` (ReMixed's Extra Options list,
its own screen: a blue-green backdrop and one menu box) and `eo-levels` (its first group).

Any of ReMixed's scenarios can be reached from `wm` (Gariland, chapter 1): on the world map hold Up, then Left, 10
frames each (the cursor on Mandalia Plains), press Circle, and in the next frames, as soon as the location's script sets
var 0x27 (0x800577B8, to 5), write the scenario wanted there instead; ReMixed then loads that one, with its formation
and Preparation Menu (Start Battle, then Down to Yes). A Brave Story round won't load that way (each goes on from
the last; a lone round hands on to 0xC2), so its rounds were played from the first (0xB8), with the player's units
handed to the AI (unit +5 & ~0x08) and the enemies defeated: states `mbr-b9` to `mbr-c1` are each round as it loads
(2026-11-12; the hydra in `mbr-bb`, the grey dragon in `mbr-bf`).

## Reading the remaster's code

`tools/exe.py` reads FFT_enhanced.exe and the Ghidra decompile (work/ghidra/decompiled) by RVA:

```bash
python -I tools/exe.py func 0x284E4A        # the decompiled function holding an address
python -I tools/exe.py callers 0x284A08     # who calls it
python -I tools/exe.py refs 0xD493CC        # who uses a global
python -I tools/exe.py dis 0x247E10 0x80    # disassembly (capstone, from the user's site-packages)
python -I tools/exe.py read q 0x681400 0x3E # a pointer table, as RVAs and function names
python -I tools/exe.py find "40 53 48 83 EC 20 48 63 D9 E8 ?? ?? ?? ??"   # where a pattern is
```
# Event staging audit

After building the enhanced mod, audit its event scripts for malformed AddUnit groups and path-local actor staging
mistakes (for example, animating an actor after the definite logical `RemoveUnit`):

```powershell
python -I tools/audit_event_staging.py
python -I tools/audit_event_staging.py --strict   # warnings fail too
python -I tools/test_event_staging.py
```

The audit forgets actor state at branches, labels, subroutine boundaries, and phase markers. This intentionally avoids
claiming that mutually exclusive paths are one sequence; warnings are review leads unless `--strict` is requested.
`Erase` and `TeleportOut` only hide a drawable, so the audit permits off-screen warp, movement, animation, and waits
before a later reveal. `BlueRemoveUnit` is team-conditional and likewise cannot prove that its selector was removed.

## Private developer menu

Build the code mod explicitly with `-p:EnableDeveloperMenu=true`, then create an empty file named `DEV-MENU` beside
`IvaliceRemixed.dll` in the installed mod. Reloaded-II's **Configure Mod** window then has a second page, clearly named
`DEV ONLY — one-shot test actions`, for story progress, one event variable/flag, world-map location, and scenario launch.
The selected actions wait until a save reaches a usable world-map frame, apply on the game thread, and reset
`Developer.json` immediately so a restart cannot repeat them by surprise.

Ordinary Debug builds and Release builds compile the developer surface out. The build/package scripts neither enable the
MSBuild property nor create the marker, so public packages cannot expose it. These are live-memory test controls, not a
replacement for a complete game save editor.

