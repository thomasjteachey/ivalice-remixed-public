# Releasing

How to publish a build on Nexus Mods. This game has no Steam Workshop; its mods run through Reloaded-II and the
FFT Mod Loader, and players get them from Nexus.

## Before the first release

- **Permission.** ReMixed is someone else's work, and the mod ships its data (its tables, event scripts, story text
  and battle setups). Granted: its author, Nyzer, said yes to publishing the port on Nexus (2026-11-12, "Sure, go
  nuts", on Discord), with no conditions. The credits are in [nexus-page.md](nexus-page.md).
- **The page.** Copy the fields and the BBCode description from [nexus-page.md](nexus-page.md), and add screenshots
  (a mission posting, a ReMixed scene, the Extra Options).

## Each release

1. Raise `RELEASE_VERSION` in `tools/build_mod.py` (0.7.0 was the first; the ReMixed version is added after it).
2. Build the package (the game needn't be running):
   ```bash
   python -I tools/build_mod.py --package
   ```
   Check the output has no `WRONG`, `error` or `Traceback`.
3. Make `mod/release/IvaliceReMixed-<version>.zip` from the two zips of ours in `mod/steam-deck/`,
   `fftivc.gameplay.ivaliceremixed.zip` and `fftivc.utility.eventinstructionupgrade.zip` (the Event Instruction
   Upgrade, which ReMixed depends on), with both mods' folders at the zip's top: the GitHub release's file. The other
   zips the build writes there are the loader and its libraries, other people's mods.
   For Nexus, where the upgrade has its own page, cut it in two in `mod/release/nexus/`: `IvaliceReMixed-<version>.zip`
   with only ReMixed's folder, and `EventInstructionUpgrade-<its version>.zip` with only the upgrade's (its
   `ModVersion`). Upload the upgrade's only when it changed.
4. Install the zip once on a machine that has never had the mod (the Steam Deck will do): drag it onto Reloaded-II,
   enable it with the loader, and start a new game.
5. Update the page's "What's in it" and "Not ported yet" lists from [GAMEPLAY_TODO.md](../GAMEPLAY_TODO.md).
6. Commit the release, write its notes (Markdown, a few lines for players), and publish it on the public repo,
   https://github.com/thomasjteachey/ivalice-remixed-public:
   ```bash
   python -I tools/release_public.py <version> notes.md --dry-run
   python -I tools/release_public.py <version> notes.md
   ```
   The public repo's history is one snapshot commit a release: the script puts HEAD's tree on top of its main and
   pushes only that commit and its tag, refusing a tree that holds the game's files (the disc, `FFT_enhanced.exe`,
   the ReMixed `.ppf`). It makes the release with the zip from step 3, which starts the Nexus upload below. The pages: Ivalice ReMixed https://www.nexusmods.com/finalfantasytacticstheivalicechronicles/mods/187, Event
   Instruction Upgrade https://www.nexusmods.com/finalfantasytacticstheivalicechronicles/mods/186 ([nexus-page-eiu.md](nexus-page-eiu.md)).

## Automated Nexus upload

The `publish-nexus.yml` GitHub workflow publishes Ivalice ReMixed without controlling a local browser. It runs for a
new non-prerelease GitHub release, or manually for an existing release version. It downloads the combined, tested
GitHub release asset, validates its two top-level mod folders, and uploads `fftivc.gameplay.ivaliceremixed` to Nexus
mod 187 and `fftivc.utility.eventinstructionupgrade` to Nexus mod 186.

Configure an Actions secret named `NEXUSMODS_API_KEY` in the public repository (or its `nexus` environment); without it
the workflow fails at "Find Event Instruction Upgrade file". Create a personal
key on the Nexus Mods API Access page and paste it directly into the GitHub secret; never commit it or put it in a
release log. The workflow resolves the Event Instruction Upgrade's active file group through the Nexus v3 API and
uses file-group ID 8389512 for ReMixed. It archives each previous version, makes the new version primary, and updates
both Nexus mod versions.
