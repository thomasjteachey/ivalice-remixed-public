# Ivalice ReMixed

A port of **The Lion War ReMixed** (a PS1 ROM hack of *Final Fantasy Tactics*) to
**Final Fantasy Tactics: The Ivalice Chronicles** (the 2025 PC remaster), built as a
Reloaded-II mod.

**Status:** ReMixed's balance is ported to the enhanced build: jobs, skillsets,
abilities, items, random battles, names, descriptions and visual effects. The code mod has
ReMixed's formulas and its first support abilities; most other custom
mechanics, the story and the art aren't ported yet. See [docs/PORT_PLAN.md](docs/PORT_PLAN.md).

- [docs/PORT_PROGRESS.md](docs/PORT_PROGRESS.md): how far the whole port is, as one counted figure, and its
  history (`python -I tools/port_progress.py --write` updates it)
- [docs/GAMEPLAY_TODO.md](docs/GAMEPLAY_TODO.md): the running list of gameplay still to port
- [docs/MECHANICS.md](docs/MECHANICS.md): every custom mechanic in ReMixed's code, and where its options live
- [docs/FEATURES.md](docs/FEATURES.md): ReMixed's data, story and art changes
- [docs/REMASTER.md](docs/REMASTER.md): how the remaster stores the game, and where each change goes

## Why a port and not a patch

ReMixed is a PPF patch for the US PS1 disc. Its changes are bytes at fixed disc
offsets, and much of it is MIPS code for the PS1. The Ivalice Chronicles is a
native PC rebuild with its own executables and `.pac` archives, so none of that
applies directly. Every change has to be understood and rebuilt for the new game:

- **Data** (jobs, abilities, items, battles) is re-entered into the remaster's tables.
- **Code** (ReMixed's custom mechanics) is rewritten as C# hooks.
- **Story, text and art** go through the remaster's own formats.

## Layout

```
docs/                  plan, mechanics and feature trackers
docs/analysis/         detailed reverse-engineering notes on ReMixed's code
docs/generated/        reports written by the tools (don't edit)
data/remixed/          every value ReMixed changes, as CSV (written by tools/tables.py)
tools/                 Python 3.11+ scripts, standard library only
code/IvaliceRemixed/   the mod's C# part, for ReMixed's mechanics (built by tools/build_mod.py)
code/eiu/              a submodule: eiu-ivalice-chronicles, Xifanie's Event Instruction Upgrade (the PS1 event
                       hack ReMixed's events are written for) as its own Reloaded-II mod, which ReMixed depends
                       on, with IvaliceCore (what both mods share: the exe searched, hooks, battle units, the
                       PS1 memory map); https://github.com/thomasjteachey/eiu-ivalice-chronicles
code/ExeTool/          x64 disassembler for the remaster's exes (reverse engineering only)
code/IvaliceRemixed.Check/  offline check: runs both mods' patches against the exe on disk
mod/                   the built Reloaded-II mods, ReMixed's and the upgrade's (gitignored)
work/                  local output: disc images, extracted files (gitignored)
local.toml             your file paths (gitignored)
```

## Setup

You need your own copies of:

- the US PS1 disc of *Final Fantasy Tactics* (SCUS-94221) as a `.bin`/`.cue`, or a `.7z` holding them
- the ReMixed `.ppf` from its FFHacktics thread
- The Ivalice Chronicles on Steam
- 7-Zip, if the disc is in a `.7z`

Then:

1. Fetch the Event Instruction Upgrade's repo, a submodule at `code/eiu` (or clone this repo with
   `--recurse-submodules`):
   ```bash
   git submodule update --init
   ```
   Copy `local.example.toml` to `local.toml` and fill in the paths.
2. Build the ReMixed reference disc (also playable in a PS1 emulator):
   ```bash
   python -I tools/remixed.py prepare
   ```
3. Map every byte ReMixed changes to the disc file it lands in:
   ```bash
   python -I tools/remixed.py diff --extract
   ```
   This writes [docs/generated/](docs/generated/) and copies each changed file
   (vanilla and ReMixed versions) to `work/files/`.
4. List every data-table value ReMixed changes (jobs, abilities, items, battles…):
   ```bash
   python -I tools/tables.py
   ```
   This writes `data/remixed/*.csv` and two more reports in `docs/generated/`.
   The first run downloads FFTPatcher's name lists into `work/cache/` (they're
   GPL-licensed, so they aren't kept in this repo).
5. Find ReMixed's code: patched vanilla routines, new routines, and the hook
   sites where vanilla code jumps into them:
   ```bash
   python -I tools/codemap.py
   ```
   This writes `data/remixed/hooks.csv`, `data/remixed/routines.csv`, a summary
   in `docs/generated/`, and full side-by-side disassembly in `work/asm/`
   (kept out of git, since it's game and ReMixed code).

6. Build the mod (ReMixed's job, skillset, ability, item and status changes) and
   copy it into Reloaded-II:
   ```bash
   python -I tools/build_mod.py --install
   ```
   This needs `tools.ff16tools`, `tools.modloader`, `target.tic_dir` and
   `target.reloaded_mods` in `local.toml`. The mod is written to `mod/` (kept out
   of git), and what went in or was left out is in
   [docs/generated/mod-build.md](docs/generated/mod-build.md). It builds for
   enhanced by default. Add `--only classic` for classic, which gets less (no
   ability combat stats or equipment bonuses; see the report).

   The enhanced build audits every shipped event's dialogue operands and fails
   on missing text rows, invalid line ids, or broken runtime-selected templates.
   To recheck the generated scripts quickly without rebuilding, run:
   ```bash
   python -I tools/check_event_dialogue.py
   ```

   If the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) is
   installed, the build also compiles the mod's C# part against your Reloaded-II
   install (`tools.reloaded`, or the folder above `target.reloaded_mods`). It
   carries the mechanics ported so far (ReMixed's formulas 0x1A, 0x2D and 0x46),
   and is checked against your exe on every build. `--no-code`
   skips it, and holds back what depends on it.
7. For classic's jobs, the mod loader needs one extra search pattern to find
   classic's job table:
   ```bash
   python -I tools/loader_signatures.py --apply
   ```
   This edits the installed loader's `Signatures/ffto.ini` and keeps the
   original (`--restore` puts it back). A loader update overwrites it, so rerun
   it after updating.
8. In Reloaded-II, register both `FFT_enhanced.exe` and `FFT_classic.exe`,
   enable the mod loader and "Ivalice ReMixed" for each, and give classic the
   launch argument `/language en`. Without it, classic starts in Japanese,
   because enhanced normally passes the language when it starts classic.
9. Optionally, pick ReMixed's Extra Options under "Configure Mod" for
   "Ivalice ReMixed" in Reloaded-II (permadeath, JP and EXP gain, spillover JP, JP refund, bonus EXP & JP on victory, the Nemesis System, back attacks,
   job stats, enemy levels and level scaling, damage by team, Brave and damage, knockback, weapon range, guest control, poaching, MP options, zodiac, job levels, rare equipment, cast time, test mode, separate saves, the Argath campaign: ReMixed's secret campaign for a new game, ported to its end but played in the game only as far as its battle in the woods). They start at ReMixed's defaults and take effect the next time
   the game starts. They're what a new save starts with: in the game each save keeps its own Extra Options, as in
   ReMixed, changed on the **ReMixed** tab of Settings (the party menu's Options tab, then Settings).
10. To test it in the game, set **Test mode** under Testing in the same settings: each battle's mechanics,
    errors and what's left to try go to a report page. See [docs/TESTING.md](docs/TESTING.md).

To play on a Steam Deck, build with `--package` as well and follow [docs/STEAM_DECK.md](docs/STEAM_DECK.md).

To read any stretch of code, vanilla or ReMixed:

```bash
python -I tools/disasm.py BATTLE.BIN 0x80151C00 0x80151D00 --both
```

and the remaster's (`tools/exe.py`: the decompiled function at an address, its callers, disassembly, pattern search),
or to drive the game from the PC for a test (`tools/harness.py`): see [docs/TESTING.md](docs/TESTING.md).

`tools/ppf.py` and `tools/psxdisc.py` also work on their own: run either with
`--help`.

## Rules

- **Never commit game files or the ReMixed patch.** `.gitignore` blocks the usual
  extensions, but check `git status` before committing.
- ReMixed is someone else's work. Get its author's permission before publishing
  anything built from it.
- Publishing a build: [docs/release/RELEASING.md](docs/release/RELEASING.md), with the Nexus page's text in
  [docs/release/nexus-page.md](docs/release/nexus-page.md).

## Credits

- *The Lion War ReMixed*, and *The Lion War* it builds on: their authors at FFHacktics
- FFHacktics wiki and tools (FFTPatcher by Glain and others)
- Nenkai: FF16Tools and the Ivalice Chronicles mod loader
