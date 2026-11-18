#!/bin/bash
# Runs The Ivalice Chronicles on the Steam Deck with the mod, headless and silent, for the mod's own tests:
#   systemd-run --user --unit=ivalice-headless ~/.local/share/ivalice-remixed/headless.sh [seconds]
# gamescope's headless backend draws to no display; the game's sound goes to a null PipeWire sink, unloaded after. The
# mod loads as launch.sh has it (version.dll, the Ultimate ASI Loader, then Reloaded-II). The run stops after the given
# seconds (default 180) or when the game exits; everything it started is stopped. Steam must be running (the game's
# steam_api) and the game mustn't be: one copy per account.
set -u
seconds=${1:-180}
game="/run/media/deck/1fb496c3-9151-4748-b052-7abce079d3d4/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles"
runtime="/run/media/deck/1fb496c3-9151-4748-b052-7abce079d3d4/steamapps/common/SteamLinuxRuntime_4/_v2-entry-point"
proton="$HOME/.local/share/Steam/steamapps/common/Proton - Experimental/proton"
prefix="$HOME/.local/share/Steam/steamapps/compatdata/1004640"
out="$HOME/.local/share/ivalice-remixed/headless.log"

if pgrep -f "[F]FT_enhanced.exe" > /dev/null; then
    echo "the game is already running"; exit 1
fi
sink=$(pactl load-module module-null-sink sink_name=ivalice_silent sink_properties=device.description=ivalice_silent)
cleanup() {
    pkill -f "[g]amescope --backend headless" 2> /dev/null
    sleep 2
    pkill -9 -f "[F]FT_enhanced.exe" 2> /dev/null
    WINEPREFIX="$prefix/pfx" "$HOME/.local/share/Steam/steamapps/common/Proton - Experimental/files/bin/wineserver" -k 2> /dev/null
    [ -n "$sink" ] && pactl unload-module "$sink"
}
trap cleanup EXIT

export STEAM_COMPAT_DATA_PATH="$prefix"
export STEAM_COMPAT_CLIENT_INSTALL_PATH="$HOME/.local/share/Steam"
export SteamAppId=1004640 SteamGameId=1004640
export WINEDLLOVERRIDES="version=n,b"
export PULSE_SINK=ivalice_silent
export ENABLE_GAMESCOPE_WSI=0  # its swapchain override trips an assertion in the headless backend
export PROTON_LOG=1 PROTON_LOG_DIR="$HOME/.local/share/ivalice-remixed"
# Wine's Wayland driver on gamescope's own display: under its Xwayland the X11 driver died on a BadWindow when the game
# resized its swapchain.
export PROTON_ENABLE_WAYLAND=1
cd "$game" || exit 1
timeout --kill-after=10 "$seconds" gamescope --backend headless -w 1280 -h 800 -- \
    sh -c 'unset DISPLAY; WAYLAND_DISPLAY="$GAMESCOPE_WAYLAND_DISPLAY" exec "$@"' sh \
    "$runtime" --verb=waitforexitandrun -- "$proton" waitforexitandrun "$game/FFT_enhanced.exe" > "$out" 2>&1
echo "exit $?"
