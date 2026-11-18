#!/bin/sh
# The Ivalice Chronicles' Steam launch options on the Deck (docs/STEAM_DECK.md):
#   /home/deck/.local/share/ivalice-remixed/launch.sh %command%
# The game starts unmodded, unless the "FINAL FANTASY TACTICS - Ivalice ReMixed" library entry (play_modded.sh) asked
# for the mod in the last minute. Then Wine loads the game folder's version.dll (Ultimate ASI Loader) instead of its
# own, and that loads Reloaded-II and its mods.
flag="$HOME/.local/share/ivalice-remixed/play-modded"
log="$HOME/.local/share/ivalice-remixed/launch.log"
if [ -f "$flag" ]; then
    requested=$(cat "$flag" 2>/dev/null)
    case "$requested" in
        ''|*[!0-9]*) requested=$(stat -c %Y "$flag") ;;
    esac
    age=$(( $(date +%s) - requested ))
    rm -f "$flag"
    if [ "$age" -ge 0 ] && [ "$age" -le 60 ]; then
        # Put this last. Wine honours the last rule for a DLL; putting it first let a Proton/user `version=b` rule
        # later in WINEDLLOVERRIDES silently turn the ASI loader back off, producing an ordinary vanilla launch.
        export WINEDLLOVERRIDES="${WINEDLLOVERRIDES:+$WINEDLLOVERRIDES;}version=n,b"
        printf '%s accepted modded request (%ss old); version.dll forced native,builtin\n' \
            "$(date -Is)" "$age" >> "$log"
    else
        printf '%s ignored stale modded request (%ss old)\n' "$(date -Is)" "$age" >> "$log"
    fi
else
    printf '%s ordinary unmodded launch\n' "$(date -Is)" >> "$log"
fi
exec "$@"
