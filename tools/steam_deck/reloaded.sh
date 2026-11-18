#!/bin/sh
# The "Reloaded-II - Ivalice ReMixed" library entry on the Deck (docs/STEAM_DECK.md), installed as
#   /home/deck/.local/share/ivalice-remixed/Reloaded-II - Ivalice ReMixed
# (Steam names a script's entry after its file). It opens Reloaded-II in The Ivalice Chronicles' Proton prefix, where
# Setup-Linux installed it, so its settings (Configure Mod) can be changed from Gaming Mode. Protontricks runs it with
# the game's own Proton. Close Reloaded-II (its X, or the Steam button's Exit game) before starting the game.
reloaded="${RELOADED_II:-$HOME/Desktop/Reloaded-II - FINAL FANTASY TACTICS - The Ivalice Chronicles}"
cd "$reloaded" || exit 1

# Gaming Mode only shows windows tagged with the running entry's app id (the STEAM_GAME property). Steam's overlay
# normally tags them, but it doesn't reach a program run through the Protontricks Flatpak, so the entry would show
# Steam's loading icon over an untagged window. tag_windows.py (installed beside this script) tags Reloaded-II's
# windows, dialogs and dropdowns included, while it runs.
tagger=
if [ -n "$SteamAppId" ] && [ -n "$DISPLAY" ]; then
    python3 -I "$(dirname "$0")/tag_windows.py" "$SteamAppId" &
    tagger=$!
fi

flatpak run --command=protontricks-launch com.github.Matoking.protontricks \
    --no-term --no-background-wineserver --appid 1004640 "$reloaded/Reloaded-II.exe"
status=$?
[ -n "$tagger" ] && kill "$tagger" 2>/dev/null
exit $status
