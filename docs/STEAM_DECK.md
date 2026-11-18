# Playing on a Steam Deck

The mod runs inside the game's own (Windows) process, so on a Steam Deck it runs under Proton like the game, with
Reloaded-II installed into the game's Proton prefix. The code mod only uses Windows calls Proton provides (memory
allocation and protection, the game's module), so it needs nothing special.

It works: set up on 2026-11-03 with the game on an SD card, Proton Experimental, Reloaded-II 1.31.0 and .NET 9.0.22,
started from Steam in Gaming Mode, every patch applied. The Deck's game has to be the same version as the PC's (both
were build 24304444): the code mod finds its places in the exe by pattern and checks them, and skips what it can't
find.

## 1. Make the package (on the PC)

```bash
python -I tools/build_mod.py --install --package
```

This writes five archives to `mod/steam-deck/` (kept out of git):

- `fftivc.gameplay.ivaliceremixed.zip`: this mod.
- `fftivc.utility.eventinstructionupgrade.zip`: the Event Instruction Upgrade, the port of the PS1 event hack
  ReMixed's events are written for; a mod of its own that ReMixed depends on.
- `fftivc.utility.modloader.zip`, `reloaded.sharedlib.hooks.zip`, `Reloaded.Memory.SigScan.ReloadedII.zip`: the FFT mod
  loader and its two libraries, as installed in your PC's Reloaded-II. The build is checked against loader 1.7.5, and
  this copy has the classic job pattern from `tools/loader_signatures.py --apply`, so use these rather than a newer
  download.

They're for your own Deck: the mod holds data derived from ReMixed, which is someone else's work, so don't share them.
Copy them to the Deck (a USB stick, a network share, KDE Connect or `scp`).

## 2. Set up Reloaded-II on the Deck (Desktop Mode)

Reloaded-II's own guide is [Linux Setup Guide](https://reloaded-project.github.io/Reloaded-II/LinuxSetupGuideNew); in
short:

1. Install The Ivalice Chronicles and start it once from Steam, so Proton makes its prefix
   (`~/.local/share/Steam/steamapps/compatdata/1004640/pfx`).
2. Install **Protontricks** from Discover.
3. Download `Setup-Linux.exe` from [Reloaded-II's releases](https://github.com/Reloaded-Project/Reloaded-II/releases/latest),
   right-click it, choose **Open with Protontricks Launcher**, and pick The Ivalice Chronicles. It runs without a
   window for a minute or so (it downloads about 140 MB), then puts a Reloaded-II shortcut on the desktop.

   Run it in Desktop Mode, not over SSH: the .NET installers it runs need a display. Without one they stop with
   "Failed to create main window" (their logs are in the prefix's `drive_c/users/steamuser/AppData/Local/Temp`) and
   the prefix gets no `drive_c/Program Files/dotnet`. If that happens, unzip Microsoft's
   `dotnet-runtime-9.0.x-win-x64.zip` and `windowsdesktop-runtime-9.0.x-win-x64.zip` (the .NET 9 download page,
   "Binaries") into that folder, and the two `-win-x86.zip` ones into `drive_c/Program Files (x86)/dotnet`. The game's
   loader only needs the x64 ones; Reloaded-II's own window checks for both.

   The Visual C++ runtimes fail the same way. Proton already has their DLLs in the prefix, but Reloaded-II's window
   looks for the registry keys Microsoft's installer leaves, and shows "Missing Requirements" without them.
   [tools/steam_deck/vc_markers.reg](../tools/steam_deck/vc_markers.reg) adds just those keys (its header has the
   command).
4. Start Reloaded-II from that shortcut. In its settings, turn off **Show Console**: in Gaming Mode a console window
   would take focus from the game.
5. Add the game: **Add an Application**, then `FFT_enhanced.exe` in the game folder:
   `Z:\home\deck\.local\share\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\`, or on an SD
   card `Z:\run\media\deck\<card id>\steamapps\common\…`. In the game itself Wine calls the card `D:`, so the path
   doesn't match and Reloaded-II matches the exe by its name instead (`Can't match by App Path, Matching by AppId!` in
   the log), which works.

## 3. Add the mods

1. Drag each of the five zips onto Reloaded-II's window (or unzip them into its `Mods` folder).
2. For `FFT_enhanced.exe`, tick **FINAL FANTASY TACTICS Mod Loader** and **Ivalice ReMixed**. The two libraries and
   the Event Instruction Upgrade are pulled in as dependencies.
3. Optionally set ReMixed's Extra Options under **Configure Mod** for Ivalice ReMixed, as on the PC: they're what a
   new save starts with. In the game each save keeps its own, changed on the **ReMixed** tab of Settings (Y for the
   party menu, its Options tab, Settings, then RB to the last tab). That tab's last group, **Controls**, has the mod's
   Fast-forward (while the button is held, or a press to turn it on and another to turn it off), kept for every save.
4. The package is for enhanced only (`--only classic` builds for classic; README, step 6). If you add `FFT_classic.exe`,
   give it the launch argument `/language en` (README, step 8) and, with the enhanced package, only the mod loader, as
   on the PC.

## 4. Play

- **Desktop Mode:** **Launch Application** in Reloaded-II.
- **Gaming Mode:** two library entries, both starting the real Steam game, so cloud saves, playtime and controller
  settings are the same:
  - **FINAL FANTASY TACTICS - The Ivalice Chronicles**: unmodded.
  - **FINAL FANTASY TACTICS - Ivalice ReMixed**: with the mod. Its tile is the game's art with an "IVALICE REMIXED"
    band.
  - A third, **Reloaded-II - Ivalice ReMixed**, opens Reloaded-II for its settings (see "Changing settings in
    Gaming Mode").

### How the two entries work

1. **The ASI loader.** In Reloaded-II, **Edit Application → Advanced Tools & Options → Deploy ASI Loader**. It puts
   Ultimate ASI Loader in the game folder as `version.dll` (both exes import `VERSION.dll`, the first name on
   Reloaded-II's list), and its bootstrapper beside it as `Reloaded.Mod.Loader.Bootstrapper.asi`. By hand:
   `ASILoader64.dll` from Reloaded-II's `Loader/Asi/UltimateAsiLoader.7z` as `version.dll`, and
   `Loader/X64/Bootstrapper/Reloaded.Mod.Loader.Bootstrapper.dll` as the `.asi`. Wine still loads its own
   `version.dll` unless the launch says `WINEDLLOVERRIDES="version=n,b"`, so the game stays vanilla until then.
2. **The launch options.** Copy [tools/steam_deck/launch.sh](../tools/steam_deck/launch.sh) to
   `~/.local/share/ivalice-remixed/launch.sh`, make it executable, and set the game's **Properties → Launch Options**
   to `/home/deck/.local/share/ivalice-remixed/launch.sh %command%`. It adds the override only when the modded entry
   asked for it in the last minute.
3. **The modded entry.** Copy [tools/steam_deck/play_modded.sh](../tools/steam_deck/play_modded.sh) to
   `~/.local/share/ivalice-remixed/FINAL FANTASY TACTICS - Ivalice ReMixed` (Steam names a script's entry after its
   file), make it executable, and add it to Steam: in Desktop Mode, right-click it → **Add to Steam**, or
   `steamos-add-to-steam <file>`. It leaves the flag for `launch.sh` and has Steam start the game three seconds later,
   once the entry itself has closed (a named timer, so a second press while it's pending does nothing). Current
   versions wait three seconds: one second was not always enough for Steam to finish closing the shortcut.
4. **Its art** (optional): Gaming Mode's library shows only art, so without its own the entry is a plain tile. The game's
   art is in `~/.local/share/Steam/appcache/librarycache/1004640/`; copied into
   `~/.local/share/Steam/userdata/<user>/config/grid/` as `<id>p.jpg` (portrait), `<id>.jpg` (wide), `<id>_hero.jpg`
   and `<id>_logo.png`, with `<id>` the entry's `appid` from `shortcuts.vdf` (as an unsigned number), it shows after
   Steam restarts.

Over SSH, Steam's launch options can only be written while Steam is stopped: it keeps them in
`userdata/<user>/config/localconfig.vdf` (`apps/1004640/LaunchOptions`) and rewrites that file when it exits. Gaming
Mode also starts Steam again at once (`gamescope-session.target` upholds `steam-launcher.service`), so hold that first:
`systemctl --user mask --runtime steam-launcher.service`, stop the service, edit the file, unmask it and start it. In
Steam itself it's one field.

For a single, always-modded entry instead, the launch options `WINEDLLOVERRIDES="version=n,b" %command%` are enough. A
per-exe registry override in the prefix works too (`HKCU\Software\Wine\AppDefaults\FFT_enhanced.exe\DllOverrides`,
`version` = `native,builtin`), but it makes every start of enhanced modded. With Protontricks, run its `wine reg add`
and then `wineserver -w` in one `protontricks -c "…" 1004640` session: a `reg add` through `protontricks-launch` says it
worked but isn't saved, because the session ends before Wine writes the registry.

To undo all of it, delete the two files from the game folder, clear the launch options and remove the entry.

To check it's on: the enhanced title screen says **IVALICE REMIXED** and the mod version beneath The Ivalice
Chronicles logo. That mark is inside the enhanced UI logo texture (not Classic's PS1 `opntitle.tex`), so it only
appears when the mod loader actually served ReMixed's files.
The two launcher scripts also append their handshake to `~/.local/share/ivalice-remixed/launch.log`. After a start
from the modded entry, Reloaded-II's log, in the prefix's
`drive_c/users/steamuser/AppData/Roaming/Reloaded-Mod-Loader-II/Logs`, should show `Loaded via: …Reloaded.Mod.Loader.Bootstrapper.asi`, then Ivalice ReMixed's `options:`, `formulas:`,
`supports:` and `rules:` lines and no `not applied` errors, and the mod loader's `Game successfully loaded modded pack`.
These are expected:

- `Can't match by App Path, Matching by AppId!` (see step 2.5).
- The mod loader's `Pattern … not found!` for `CFILE_DAT__Decode` (it uses another pattern), `AbilityJumpDataTable` and
  `CommandTypeDataTable`. The loader finds each table by searching for its vanilla bytes, and ReMixed's Jump rework and
  skillset types rewrite those two tables before it searches. The same happens on the PC; it would only matter for
  another mod that edits those tables.
- `[R-II SteamHook] Application not found in any Steam library`: only for starting the exe outside Steam.

## Changing settings in Gaming Mode

A third library entry, **Reloaded-II - Ivalice ReMixed**, opens Reloaded-II itself, so Configure Mod (ReMixed's Extra
Options for new saves, the Test mode) works without Desktop Mode. Saving there gives no sign, but writes the settings (the mod
reads them when the game starts). Close it, then start the game from the modded entry: Reloaded-II's own Launch
Application starts the exe inside Reloaded-II's Proton session rather than through Steam, so it isn't the way in here.

1. Copy [tools/steam_deck/reloaded.sh](../tools/steam_deck/reloaded.sh) to
   `~/.local/share/ivalice-remixed/Reloaded-II - Ivalice ReMixed` and make it executable, and
   [tools/steam_deck/tag_windows.py](../tools/steam_deck/tag_windows.py) beside it. It runs `Reloaded-II.exe` in
   the game's prefix through the Protontricks Flatpak (`protontricks-launch`, which isn't on the Deck's path; the
   desktop shortcut Setup-Linux makes assumes it is), with the game's own Proton.
2. Add it to Steam as in step 3 of "How the two entries work". Over SSH, with Steam running:
   `touch /tmp/addnonsteamgamefile`, then `steam -ifrunning steam://addnonsteamgame/<the file's path, URL-encoded>`
   with `DISPLAY=:0`.
3. Art as for the modded entry, with the entry's own `appid`.

It needs the x86 .NET runtimes and the Visual C++ keys from step 2.3. With both, the window opens in about 15 seconds.
Once (the first start after adding the keys), Reloaded-II showed "Index was out of range" before its window; Cancel
went on to the window, and later starts were clean.

Its `--no-background-wineserver` makes the entry end when Reloaded-II closes; without it, Steam keeps showing it as
running.

Gaming Mode only shows windows carrying the running entry's app id in their `STEAM_GAME` property. Steam's overlay
sets it for games, but not through the Protontricks Flatpak, so without help the entry shows Steam's loading icon over
a window that is there. The script runs `tag_windows.py`, which tags Reloaded-II's windows itself (those whose class Proton
names `steam_app_<id>`) as they appear, Configure Mod's dialog and its dropdowns included, until Reloaded-II closes.
It talks to X directly, ten checks a second: a shell loop of `xwininfo` and `xprop` did the same, but made the
dialogs lag.

## Saves

With the Separate saves setting (on by default) the modded game keeps its saves in
`Documents/My Games/FINAL FANTASY TACTICS - Ivalice ReMixed/Steam/<Steam id>/` in the game's prefix
(`compatdata/1004640/pfx/drive_c/users/steamuser/`), and the plain game keeps its own in
`.../FINAL FANTASY TACTICS - The Ivalice Chronicles/Steam/<Steam id>/`. Steam Cloud syncs only the plain game's. To
start the modded game from the plain game's saves, copy the `.png` files across (ENHANCED.png, autoenhanced.png,
system.png) while the game is closed. On 2026-11-04 the Deck's were copied, with a backup in
`~/Downloads/ivalice-remixed/save-backup-2026-11-04`. A save from the modded game then went to the new folder and left the plain
game's alone.

## Updating

Rebuild with `--package` on the PC. Then, with the game closed, replace the two mods' folders in Reloaded-II's `Mods`:
drag the new `fftivc.gameplay.ivaliceremixed.zip` and `fftivc.utility.eventinstructionupgrade.zip` onto Reloaded-II, or
over SSH (here the SSH host `deck`, with Reloaded-II where Setup-Linux puts it):

```bash
scp mod/steam-deck/fftivc.gameplay.ivaliceremixed.zip mod/steam-deck/fftivc.utility.eventinstructionupgrade.zip deck:Downloads/ivalice-remixed/
```

```bash
ssh deck 'M="$HOME/Desktop/Reloaded-II - FINAL FANTASY TACTICS - The Ivalice Chronicles/Mods"; for m in fftivc.gameplay.ivaliceremixed fftivc.utility.eventinstructionupgrade; do rm -rf "$M/$m" && unzip -q ~/Downloads/ivalice-remixed/$m.zip -d "$M"; done'
```

Your Configure Mod settings are kept, as they live in Reloaded-II's own folders.
