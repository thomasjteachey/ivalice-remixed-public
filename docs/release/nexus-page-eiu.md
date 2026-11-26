# Nexus Mods page: Event Instruction Upgrade

https://www.nexusmods.com/finalfantasytacticstheivalicechronicles/mods/186 (published 2026-11-14).

The upgrade gets its own page on Nexus, and Ivalice ReMixed lists it as a requirement. Its code is public
(https://github.com/thomasjteachey/eiu-ivalice-chronicles, MIT). The file is the upgrade's folder from ReMixed's
package (`mod/release/nexus/EventInstructionUpgrade-<version>.zip`, cut from `IvaliceReMixed-<version>.zip`), so it
carries the `eiu-battle-words.bin` ReMixed's build writes; upload a new one only when the upgrade changes.
Written for 1.0.2 (2026-11-26), the build in ReMixed 0.9.40.

## Page fields

- **Name:** Event Instruction Upgrade
- **Category:** Utilities (or Modders Resources)
- **Version:** 1.0.2
- **Language:** English
- **Requirements:** FFT Mod Loader by Nenkai (https://www.nexusmods.com/finalfantasytacticstheivalicechronicles/mods/4),
  and Reloaded-II with its shared hooks (the loader needs those too)
- **Brief overview:**

  > A port of Xifanie's Event Instruction Upgrade to The Ivalice Chronicles (enhanced): event scripts with If and
  > Switch, subroutines, 32-bit variables, the PS1's memory, GenerateUnit and more. A library: install it for the mods
  > that need it, such as Ivalice ReMixed.

## Description (BBCode)

```
[size=5][b]Event Instruction Upgrade[/b][/size]

An unofficial port of [b]Xifanie's Event Instruction Upgrade[/b], the FFHacktics hack that gives Final Fantasy Tactics' event scripts real control flow and memory access, to [b]Final Fantasy Tactics: The Ivalice Chronicles[/b] (enhanced mode), as a Reloaded-II mod.

[b]It's a library.[/b] On its own it changes nothing: it runs only the events a mod marks for it, and every other event is the remaster's own. Install it for a mod that needs it, such as [url=https://www.nexusmods.com/finalfantasytacticstheivalicechronicles/mods/187]Ivalice ReMixed[/url].

[size=4][b]What it does[/b][/size]
[list]
[*]Event scripts get If, ElseIf and Else, Switch and Case, subroutines and GoTo, and 32-bit variables.
[*]They can read and write the game's units and roster through their PS1 addresses (LoadAddress, SaveAddress, UnitAddress), patch their own next instruction from a variable (AlterParameter), make units on the fly (GenerateUnit), and call routines a mod supplies in C# (Routine).
[*]42 of the upgrade's instructions run. The hack's changes to vanilla instructions, a few instructions no event tested so far uses, and classic mode aren't ported yet.
[/list]

[size=4][b]Installation[/b][/size]
[list=1]
[*]Set up Reloaded-II and the FFT Mod Loader (see the loader's page).
[*]Drag the downloaded zip onto Reloaded-II's window.
[*]You don't need to tick it: it loads with the mods that need it. If Reloaded-II asks to download Reloaded's shared hooks, let it.
[/list]
Works on the Steam Deck through Reloaded-II as well.

[size=4][b]For modders[/b][/size]
The code, the docs (the instructions, writing events, the API for your own routines and conditions, the PS1 memory map) and Python tools that turn events into readable scripts and bring a PS1 hack's events over are on GitHub, under the MIT license: https://github.com/thomasjteachey/eiu-ivalice-chronicles

[size=4][b]Credits[/b][/size]
[list]
[*][b]Xifanie:[/b] the Event Instruction Upgrade, the PS1 hack this ports, and its instruction set. This is a clean reimplementation in C#, not Xifanie's code.
[*][b]The Lion War ReMixed[/b] by Nyzer: its events are what this was ported for and tested with.
[*][b]Nenkai:[/b] the FFT Mod Loader. [b]Sewer56:[/b] Reloaded-II and Reloaded.Hooks.
[*]The FFHacktics community: the wiki and its tools.
[/list]
```

## File (the upload)

- **File:** `mod/release/nexus/EventInstructionUpgrade-1.0.2.zip` (the folder `fftivc.utility.eventinstructionupgrade`
  at its top)
- **Name:** Event Instruction Upgrade 1.0.2
- **Category:** Main file
- **Description:** Drag onto Reloaded-II. Needs the FFT Mod Loader. Enhanced mode.

## Permissions

The code is MIT. Credit Xifanie for the hack and its design.
