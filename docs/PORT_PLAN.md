# Port plan

## Source: ReMixed v1.2.64.23

A PPF 3.0 patch for the raw `.bin` of the US PS1 disc (SCUS-94221). It writes
9.6 MB across 616,622 records. It's built on *The Lion War* 2.031, which brought
War of the Lions content to the PS1 version.

What it changes, from [the generated change map](generated/FFT-ReMixed-v1.2.64.23-disc-changes.md):

| Area | Files | Bytes changed | What's there | What it means for the port |
|---|--:|--:|---|---|
| Battle sprites | 162 | 2.5 M | Sprite slots renumbered (`ADORA.SPR` becomes `00.SPR`…) and most sheets edited | The remaster draws HD sprites. Only sprites for new characters or jobs need HD versions. |
| Events | 2 | 2.3 M | `EVENT/TEST.EVT` (event scripts) and `EVENT/ATTACK.OUT` (event setup) | Mostly The Lion War's War of the Lions script, which the remaster already uses. Port only ReMixed and Lion War-specific scenes. |
| Text | 18 | 1.6 M | World map, help, rumor and tutorial text, plus the font | Same as events: carry over only new names and help text. |
| Maps | 38 | 0.8 M | Two new map sets (`MAP/TUNNEL`, `MAP/VOLCANO`) and edits to `MAP075` | Custom maps. Needs the remaster's map pipeline (Mod Studio's map editor builds on GaneshaDx). |
| Code and tables | 14 | 0.6 M | Main exe (renamed `FFTR_942.21`), `BATTLE.BIN`, `WORLD.BIN`, `WLDCORE.BIN` and the menu overlays (`*.OUT`) | Mixes stat-table edits with ASM hacks. Separating the two is the next job. |
| Battle setups | 4 | 54 K | `BATTLE/ENTD1-4.ENT` | Unit placement per battle. The remaster still has ENTD-style files. |
| Other | 16 | 116 K | Portraits, menu graphics, effects `E306`/`E331`, one music track | Case by case |

About 7.7 KB is also written to unused sectors 84744–84895, outside any file.
That's probably data loaded by ReMixed's own code.

## Target: The Ivalice Chronicles

- Native x64 rebuild. Square Enix lost the original source and reconstructed the
  game, so PS1 code doesn't run in it.
- Two builds, each with its own exe and data: `FFT_classic.exe` with
  `data/classic/*.pac`, and `FFT_enhanced.exe` with `data/enhanced/*.pac`.
  Archives are numbered (`0000.pac`…), with per-language variants (`0002.en.pac`).
- Game data is split three ways:
  - NXD tables inside the `.pac` files (abilities, items, encounters…)
  - tables hardcoded in the exe (job stats, skillsets…)
  - PS1-style files under `fftpack` (`battle_entd*`)
- Modding stack:
  - Reloaded-II with Nenkai's FFT mod loader, which overrides files and patches hardcoded tables
  - C# hooks for code changes
  - FF16Tools for `.pac` and NXD
  - The Ivalice Chronicles Mod Studio for tables, maps and event setup
- Existing mods to build on or stay compatible with:
  - **WotL Restoration**: War of the Lions events, items, Balthier and Luso
  - **Dark Knight and Onion Knight (Generic)**
  - **Level Scaling**: a code hook

### Open questions

- **Classic or enhanced?** ReMixed's balance comes from the PS1 game, so
  `classic` is closest. Most existing mods and tools target `enhanced`. Check
  what the mod loader and Mod Studio support for each.
- **Depend on WotL Restoration, or rebuild its content?** ReMixed's versions of
  the War of the Lions characters may not match it.

## Phases

### 0. Setup (done)
Reference disc build and the change map.

### 1. Turn the change map into a change list
- **Done:** `tools/tables.py` reads 40 PS1 data tables from both discs at
  FFTPatcher's offsets and writes every changed value to `data/remixed/*.csv`.
  See [the table report](generated/FFT-ReMixed-v1.2.64.23-tables.md). Headlines:
  133 jobs, 91 skillsets, 290 ability definitions, 111 status-infliction sets,
  55 weapons, 65 item attribute sets and 273 battles changed. Bard is now Dark
  Knight.
- **Done:** changed bytes that no table explains are listed in
  [the code report](generated/FFT-ReMixed-v1.2.64.23-code-changes.md) and
  `data/remixed/code_blocks.csv`, with PS1 RAM addresses for the main exe and
  `BATTLE.BIN`. About 7.5 KB in the main exe and 79 KB in `BATTLE.BIN` are left,
  plus the menu modules (`*.OUT`) and `WORLD.BIN`.
- **Done:** `tools/codemap.py` sorts the leftovers into patched vanilla code, new
  code and data (mostly text), and finds every hook. See
  [the code summary](generated/FFT-ReMixed-v1.2.64.23-code.md). ReMixed's own
  code is about 41 KB of new routines in `BATTLE.BIN`, 10 KB in `WORLD.BIN` and
  1 KB in the main exe, reached from 332 hook sites in vanilla code.
- **Done:** read those routines and named about 60 mechanics in
  [MECHANICS.md](MECHANICS.md), with detailed notes in [analysis/](analysis/).
  ReMixed's Extra Options are event flags 0xD5–0xFF and event variables 0x14,
  0x16 and 0x17, stored in the save. The menu that sets them, a few advertised
  features (Wariness, the Phoenix Down check) and about 400 small in-place
  patches are still open.
- **Skipped for now: text.** ReMixed's names and help text aren't needed to port
  the mechanics. FFTactext's `PSXText.xml` (FFTPatcher repo) maps the text
  sections if they're wanted later.
- Get ReMixed's patch notes from the FFHacktics thread. Its bot check blocks
  automated reading, so paste them into `docs/` by hand.

### 2. Data port (milestone 1: "ReMixed balance" playable)
- **Done:** mapped each PS1 table to where the remaster keeps it. See
  [REMASTER.md](REMASTER.md). Almost every table ReMixed changes has a mod
  loader XML. Ability attributes go through the `OverrideAbilityActionData`
  table, and battle units through `OverrideEntryData` or a whole-file
  replacement.
- **Done:** `tools/build_mod.py` writes the mod from ReMixed's data, emitting
  only the fields ReMixed changed. It covers jobs, unlock requirements,
  skillsets, monster skills, abilities (common data, Jump, Charge and all
  attributes), items, weapons, equipment bonuses, status sets and statuses. See
  [the build report](generated/mod-build.md). The rules it follows:
  - It checks its PS1 reading against the mod loader's templates. Every table
    agrees at 97–100%, except where the remaster rebalanced things itself.
  - Flag fields apply ReMixed's additions and removals on top of the
    remaster's values, so the remaster's own extras survive.
  - It holds back jobs and skillsets that only make sense with ReMixed's
    battle setups, portrait ids for ReMixed's portrait code, and the flag bits
    ReMixed reuses as markers.
  - It targets the enhanced build, the only one known to read ability overrides.
- **Done:** installed through Reloaded-II with mod loader 1.7.5. Every table
  applies on the enhanced build without errors: jobs, skillsets, abilities,
  ability overrides, items, weapons, equipment bonuses, status sets, monster
  skills, unlock requirements and statuses. Jump and Charge tables are held back
  by a loader bug (see [REMASTER.md](REMASTER.md#mod-loader-gotchas)).
- **Fixed after the first look in-game:** Knight was learning "Ignore Weather"
  and "Lavawalking". ReMixed reuses those slots for Move ×2 and +10 attack,
  and both need its code. The build now holds back any new placement of an
  ability that needs ReMixed's code, uses a formula ReMixed rewrote, or that
  ReMixed turned into a different ability (its name and animation aren't
  ported). Each one comes back as its mechanic, or its name and animation,
  gets ported. The report lists every held-back slot.
- **Next:** spot-check in-game (Samurai's Two Hands innate, Geomancy's 4
  abilities, the Bard slot's Knight 8 + Black Mage 8 requirement), and play a
  few battles for anything broken.
- **Classic too (goal).** Tested: with mod loader 1.7.5, classic can't take
  ability combat stats, jobs or equipment bonuses (details in
  [REMASTER.md](REMASTER.md#classic-build-support-mod-loader-175)). The ways
  forward:
  1. **Loader support.** Ask the loader's author for classic signatures for
     `JobData` and `ItemEquipBonusData`, classic support for ability combat
     stats, and a fix for the Jump/Charge id bug.
  2. **Find the tables ourselves.** Done: all four missing tables are in
     `FFT_classic.exe` (see REMASTER.md). Jobs and Charge only need patterns
     that match classic too. `tools/loader_signatures.py` checks them and can
     write them into the installed loader. Equipment bonuses and Move-Find use
     smaller records in classic, so they need a loader change. Ability combat
     stats on classic still need either loader support for classic's built-in
     ability table or our own code mod.
  3. **Done: a classic build.** `build_mod.py` now builds both. Classic gets
     jobs (with the loader pattern applied), skillsets, monster skills, unlock
     requirements, ability learn data, items, weapons and statuses. It keeps
     the vanilla status-set order, and items and weapons are re-pointed to
     the vanilla equipment-bonus and status sets with the contents ReMixed
     wants. Reworked abilities keep their vanilla AI flags, since classic keeps
     their vanilla stats. Still missing on classic: ability combat stats,
     equipment-bonus contents, Jump and Charge.
- **Done (enhanced): battle data, the part that's safe without events.**
  ReMixed reorganized its battle entries: about half hold a different cast
  than vanilla's entry with the same number, tied to its own event scripts.
  The build ports the **230 random battles** whole, plus ReMixed's unit builds
  in the one other changed entry whose cast is unchanged. The 273 story
  battles, scenes and tutorials ReMixed moved or rewrote wait for the story
  port. So do the 16 jobs and 5 skillsets those vanilla story battles still
  use.
- **Done (enhanced): Move-Find items, names and animations.**
  - **Move-Find:** ReMixed's hidden items on 8 maps.
  - **Names:** ReMixed's real renames (compared with vanilla PS1 names, so
    translation differences don't count), written into the English ability, job
    and skillset tables. That covers 64 abilities, 24 jobs and 30 skillsets.
    The Bard slot is named "Dark Knight", since without ReMixed's code it *is*
    the Dark Knight. Descriptions came later (2026-11-06): ReMixed's help text for the abilities, jobs and
    skillsets it changed (docs/GAMEPLAY_TODO.md).
  - **Animations:** ReMixed's per-ability defaults go into `AbilityTypeData`
    (53 abilities).
  - **Abilities released:** reworked abilities now come back when their name and
    animation are ported, unless ReMixed also changed their **visual effect**.
- **Done (enhanced): visual effects.** The loader's `AbilityEffectNumberFilterData`
  is the effect table (the name doesn't say so). 26 of ReMixed's 47 changes go
  in. The rest are abilities ReMixed retired, and slots the remaster reused
  for its own abilities. That released 15 reworked abilities, including
  Knight's 0x089 and the Bio series (0x083–0x086). Checked in the running
  game's memory.
- **Fixed: Jump and gun skills.** ReMixed rebuilt Jump (one Jump plus Jump
  Range +1s) and put six gun skills in old Vertical Jump and Charge slots, all
  driven by its code. The build was handing those slots out, so Archers learned
  Vertical Jumps named as gun skills, and Lancers lost Vertical Jump 3–8. They're
  held back now, and the Jump skillsets stay vanilla, until the mechanics are
  ported.
- **Focus: enhanced first.** Classic support is built but parked
  (`build_mod.py --only classic`) until enhanced is done.
- Scaffold the Reloaded-II mod and generate its data files from
  `data/remixed/` rather than by hand, so ReMixed updates can be re-ported.
- Port ENTD changes for reworked battles.

### 3. Story and characters (milestone 2)
- Decide on WotL Restoration (see open questions).
- Port ReMixed and Lion War-only events, battles and characters (Ashley Riot…).
- Custom maps, if the map pipeline allows.

### 4. Code features (milestone 3)
- **Done: the code mod's base.** `code/IvaliceRemixed/` builds with
  `build_mod.py` and is tested in-game: Reloaded-II loads it, and it can find
  and patch the exe's data (see [REMASTER.md](REMASTER.md#code-mod)).
- **Done: the remaster's battle code mapped** for formulas: the formula
  table, the action setup, and the globals the formulas work on
  ([REMASTER.md](REMASTER.md#battle-code-enhanced)). `code/ExeTool` is the
  disassembler used for it.
- **Done: formula 0x46** (ReMixed's weapon skill: weapon strike × X/16 with
  the ability's element and Two Hands). It runs Cloud's Braver, Cross-slash
  and Omnislash, Poison Nail, and ReMixed's ten new weapon skills
  (0x165–0x16E), which are now in the mod. Checked in-game that it installs;
  not yet watched in a battle.
- **Fixed on the way:** abilities ReMixed moved onto formulas that are empty
  in vanilla did nothing on the remaster (Cloud's Limits, Poison Nail, Thunder
  Bracelet, Wind Soul). Until their formula is ported they keep their vanilla
  formula. And ReMixed's ability 0x2D overwrote the remaster's Chant; slots the
  remaster uses for its own abilities are now left alone.
- **Done: formulas 0x2D and 0x1A.** 0x2D turns magical sword skills
  (Meteorain, Cherry Blossom) into magic damage, MA × (Y + WP) × X/16, and
  leaves Holy Sword as it was. 0x1A gives Magic Ruin ReMixed's MP-and-half-HP
  healing; Speed, Power and Mind Ruin stay stat breaks. Both are built from
  the remaster's own formula steps, mapped from the vanilla formulas that use
  them. That released 0x028, Local Quake and 0x0DC.
- **Testing without the game:** `code/IvaliceRemixed.Check` runs the DLL's
  patches against the exe on disk, and the build runs it every time. What it
  can't show is the damage in a battle; the formulas' `trace` log lines are
  for that, whenever a battle uses them.
- **Done: formulas 0x13 and 0x39,** ReMixed's strikes with recoil: 0x13 hits
  for PA × MA plus the attacker's Max HP / 3 and costs the attacker that much;
  0x39 loses power with distance and costs Max HP / 5. Both adjust for Knight
  Sword, Katana and Axe. The recoil goes through the attacker's own result
  struct, which the remaster already applies (its Chant works that way). That
  released 0x0DB and 0x0B8.
- **Done: formulas 0x11 and 0x18.** 0x11 ("Slap", ability 0x0FA) is a
  weapon strike that adds its element and inflicts its status with Y% chance,
  blocked by Maintenance; the chance is a single roll until ReMixed's
  status-chance mechanic is ported. 0x18 is vanilla 0x4E plus a knockback
  (Wind Soul). No ability is left on an empty formula now.
- **Done: formulas 0x27, 0x23 and 0x58.** Gil Taking runs ReMixed's
  double roll and steal (corrected 2026-11-03: one roll and steal, with Banditry's +10; the
  second came from misreading a routine ReMixed rewrote), and Shine Lover is a weapon hit that steals gil from
  enemies. Murasame no longer risks the katana. Death Sentence's slot becomes
  ReMixed's "Matra Magic", which swaps HP and MP.
- **Corrected along the way:** diffing ReMixed's changes inside every
  vanilla formula routine showed my notes had three labels off by one: the
  HP/MP swap is formula 0x58 (not 0x57), Geomancy by terrain is 0x24 (not
  0x23), and ReMixed's magic sword is 0x2D (not 0x2C). 0x57 is unchanged, and
  no ability uses 0x64.
- **Done: formula 0x24, Geomancy by terrain.** ReMixed's four Geomancy
  abilities (Elemental Strike, Lance, Pulse and Wave) take their status set,
  element and visual effect from the terrain: under the targeted panel for
  Strike and Pulse, under the caster for Lance and Wave. The remaster's map,
  tile and action-target data are mapped for it (`code/IvaliceRemixed/BattleMap.cs`),
  ready for ReMixed's other terrain mechanics.
- **Done: formula 0x2C, Magic Break:** a plain weapon attack run inside the
  action, with what it does to HP moved to MP. Built on the sub-action the
  remaster's formula 0x25 uses.
- **Done: function hooks** (Reloaded.Hooks from the shared hooks mod), with the
  offline check installing them on its copy of the exe and running the HP
  damage step through them.
- **Done: Adeptness, Vehemence and Momentum** (0x1E4, 0x1D9, 0x1F8): +20%
  damage; +50% dealt and +50% taken; +10 XA on the Breaks with a melee
  weapon. All three are back in ReMixed's skillsets.
- **Done: First Move, Move-Cleanse and MP Rage** (0x1F4, 0x1F5, 0x1BB), the last
  of ReMixed's repurposed movement and reaction abilities. First Move doubles
  Move until the unit's first turn ends (a per-unit turn count, reset at battle
  start). Move-Cleanse removes bad statuses after a move, and a unit with Don't
  Act can still act after moving. MP Rage restores MP when the unit takes HP
  damage, through Absorb Used MP's effect. They're back in Knight's and Oracle's
  skillsets, with ReMixed's names (the build now reads FFT's hyphen).
- **Done: five battle rules** (`Patches/Rules.cs`, plus formula 0x1E). A
  Transparent attacker can't be evaded. Ally-only abilities (the helping
  summons, the songs) miss enemies, and attack summons miss allies, through a
  stub in every formula table entry. Formula 0x1E heals targets on the
  attacker's team. Cloud's Limits charge faster when he's hurt. Defense UP and
  Magic Defense UP also protect the units next to the holder. The offline
  check runs the stubs and the Defense UP steps through their hooks.
- **Done: status chance tiers.** A tiered set rolls its chance in the status
  step (a miss on failure), and vanilla's 19% roll before a hit's statuses
  is gone where ReMixed removed it. The tier bits travel in `Data/patches.json`,
  since the loader's status sets can't hold them. The offline check runs the
  status step through its hook with a seeded RNG.
- **Done: learning on hit through the secondary skillset.** The remaster's
  learn scan is rewritten in C# with ReMixed's second pass, and the learn
  step marks the secondary job's bit.
- **Done: two-job stat growth** (0x1E5). Right after a job's growths are
  copied into a battle unit, a Bard or Dancer (or anyone with 0x1E5) takes the
  best growth and multiplier of its job and its secondary skillset's job.
  The innate is back on both jobs.
- **Done: Counter Steal** (0x1B7). It's a counter now: it reacts to HP damage
  from abilities that can be countered, and the reactor answers with a steal
  ReMixed's way (an ally or a monster loses EXP; otherwise a roll picks the
  slot). The reaction builder sees it as Counter for one call, and the counter
  setup swaps in the steal.
- **Done: weapon requirements per ability.** ReMixed's table of weapon types
  per ability replaces "requires sword" and "requires Materia Blade" in the
  action menu and in the AI's two ability lists (the remaster has two where
  the PS1 had one). Each check jumps to a small stub that calls the code mod.
- **Done: EXP and JP rates, and the post-move rewrite.** ReMixed's options
  event sets its defaults (JP 125%, EXP 100%, spillover 1/4); the build reads
  them there. Actions' EXP and JP and Move-Get Exp and JP use them, Gained EXP
  Up and JP Up don't boost anything any more, and every post-move ability a
  unit has applies after its move.
- **Done: Banditry** (0x1CF). A Thief's basic attack from behind is a
  critical hit (+50% or +25% XA by weapon), in the remaster's critical step,
  and an AI Thief uses Mug for Gil Taking. The Thief's innate is back.
- **Done: learned movement passives** (0x1FD) and the reaction rule (an
  equipped reaction wins over an innate one), around the remaster's
  ability-bit builder. Both hooks are small stubs: the builder keeps r10
  and r11 across its calls to the setter, which a C# detour would clobber.
- **Done: knockback into lava or water.** After the remaster's fall damage
  step, ReMixed's landing: 999 on lava, a fall into water cut by depth or
  drowned, none with Float. And a unit that dies there has its body moved
  to a free tile nearby, through the apply step's dead-rider return.
- **Done: Monster Skill and Train range.** The remaster's Monster Skill
  check is replaced: an ally anywhere with either gives a monster its 4th
  skill and Move+1.
- **Done: Limit's charge in the ability lists.** Both copies of the list
  builder cut the Limit's CT before Short Charge, so the menu and its
  turn-order preview match the real charge.
- **Done: monster reactions by family.** The remaster's reaction pick gives
  a monster's random reaction its family's, from ReMixed's table.
- **Done: the Jump rework**, without Lancet and the three Breaths. Jump and
  five Jump Range +1 add up through a stub in the remaster's Jump range,
  with ReMixed's values written into the game's Jump table.
- **Done: the death counter**, through two stubs in the remaster's tick for
  a dead unit. By default a fallen unit is lost for good, as in ReMixed once
  its story begins; its Player Deaths and General Deaths options are in.
- **Formulas: done.** Every ability on a formula ReMixed rewrote now runs
  ReMixed's version. The one left, 0x19 (repeat the queued ability), only
  serves ReMixed's ability 0x2D, whose slot the remaster gave to Chant.
- Work through [MECHANICS.md](MECHANICS.md): one C# hook per mechanic, each
  with a test battle to check it against the PS1 build in an emulator.
- Most of ReMixed's routines are small detours from a vanilla step (the
  damage calculation, a formula, the action menu check). On the remaster, find
  the matching step in the exe and hook it there, rather than translating the
  PS1 code.
- Put ReMixed's in-game Extra Options toggles in Reloaded-II's mod config
  instead of rebuilding that menu. This avoids most of the UI work. **Started**:
  the DLL is its own configurator (`Options.cs`), with the options whose
  mechanics are ported (eight so far, the level ones included).
- Power Modes might map onto the remaster's own difficulty settings, which
  already scale HP and damage.

### 5. Art and text
HD sprites and portraits for new characters and jobs. New ability, job and item
names and help text.

### 6. Release
Only with the ReMixed author's permission.
