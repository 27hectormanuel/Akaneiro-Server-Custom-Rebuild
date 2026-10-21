#!/bin/bash
# Starts the native Linux game from a game folder with the server redirect in place. Used by
# play.sh and steam-launch.sh; also usable alone when the server already runs:
#   linux/run-game.sh <game folder> [Unity player arguments...]
set -e
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

GAME_DIR="${1:?usage: $0 <game folder> [arguments]}"
shift
cd "$GAME_DIR"
[ -f Akaneiro.x86 ] || { echo "No Akaneiro.x86 in $GAME_DIR" >&2; exit 1; }

# Mono in this old Unity crashes with some locales.
export LC_ALL=en_US.UTF-8
export LD_PRELOAD="$(redirect_preload)"
# Always Akaneiro's Steam app id: a Non-Steam game shortcut gets an id of its own, and the Steam
# login would fail with it.
export SteamAppId=230310 SteamGameId=230310
# libSteamworksNative.so and libsteam_api.so sit next to the executable; Mono only finds them
# through the library path.
export LD_LIBRARY_PATH="$PWD${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

# The game is 32-bit Unity 4.1. Started directly or inside the newer Steam Linux Runtime
# containers it crashes in the system's 32-bit Mesa driver; the old ubuntu12_32 runtime of the
# Steam client runs it fine.
if [ -x "$STEAM_RUNTIME_RUN" ]; then
    exec "$STEAM_RUNTIME_RUN" ./Akaneiro.x86 "$@"
fi
echo "Steam runtime not found at $STEAM_RUNTIME_RUN, starting the game directly" >&2
exec ./Akaneiro.x86 "$@"
