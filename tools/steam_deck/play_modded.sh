#!/bin/sh
# The "FINAL FANTASY TACTICS - Ivalice ReMixed" library entry on the Deck (docs/STEAM_DECK.md), installed as
#   /home/deck/.local/share/ivalice-remixed/FINAL FANTASY TACTICS - Ivalice ReMixed
# (Steam names a script's entry after its file). It marks the next start of The Ivalice Chronicles as modded, for
# launch.sh in the game's launch options, and has Steam start the game a second later, outside this entry, so Steam
# starts it as the normal game: same cloud saves, playtime and controller settings.
#
# The timer has a fixed name, so a second press while one is pending does nothing (the first goes ahead), and a tight
# accuracy: systemd's default lets a user timer fire a second or more late, which with the game's own start left
# several seconds without a sign that anything was happening. Three seconds gives Steam time to finish closing this
# non-Steam shortcut before it handles the real game's request; one second was too short on some Deck boots.
state="$HOME/.local/share/ivalice-remixed"
mkdir -p "$state"
date +%s > "$state/play-modded.new"
mv -f "$state/play-modded.new" "$state/play-modded"
printf '%s modded tile requested the Steam game\n' "$(date -Is)" >> "$state/launch.log"
unset LD_PRELOAD LD_LIBRARY_PATH
systemd-run --user --quiet --collect --unit=ivalice-remixed-play --on-active=3 \
    --timer-property=AccuracySec=50ms \
    -p EnvironmentFile=-/run/user/"$(id -u)"/gamescope-environment \
    /usr/bin/steam -ifrunning steam://rungameid/1004640
status=$?
if [ "$status" -ne 0 ]; then
    printf '%s could not schedule the Steam game (systemd-run status %s)\n' "$(date -Is)" "$status" >> "$state/launch.log"
fi
exit "$status"
