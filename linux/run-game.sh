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

# Keep the game log of the previous run (Unity starts it anew).
keep_log "$UNITY_LOG" game

# Speech bubble font: a font from the game's data under the name the bubbles ask for (Arial).
# AKANEIRO_BUBBLE_FONT=arial keeps the original look (Proton's Arial, set in common.sh).
BUBBLE_FONT="${AKANEIRO_BUBBLE_FONT:-edo}"
if [ "$BUBBLE_FONT" != arial ]; then
    font_dir="$AKANEIRO_DATA_DIR/fonts/$BUBBLE_FONT"
    if [ ! -f "$font_dir/arial.ttf" ]; then
        # install.sh sets up .venv with UnityPy and fontTools.
        py="$REPO_DIR/.venv/bin/python"
        [ -x "$py" ] || py="$(dirname "$REPO_DIR")/.toolchain/bin/python"
        [ -x "$py" ] || py=python3
        "$py" -I "$LINUX_DIR/fonts/make-bubble-font.py" "$PWD" "$font_dir/arial.ttf" "$BUBBLE_FONT" >&2 \
            || echo "Could not make the '$BUBBLE_FONT' bubble font; the bubbles keep Arial" >&2
    fi
    [ -f "$font_dir/arial.ttf" ] && export AKANEIRO_FONT_DIR="$font_dir"
fi

# Mono in this old Unity crashes with some locales.
export LC_ALL=en_US.UTF-8
export LD_PRELOAD="$(redirect_preload)"
# Always Akaneiro's Steam app id: a Non-Steam game shortcut gets an id of its own, and the Steam
# login would fail with it.
export SteamAppId=230310 SteamGameId=230310
# libSteamworksNative.so and libsteam_api.so sit next to the executable; Mono only finds them
# through the library path.
export LD_LIBRARY_PATH="$PWD${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

# Mono's garbage collector collects whenever a third of its heap is newly allocated. The game
# allocates tens of MB per second and its heap grows during long missions, so this became
# collections twice a second taking up to 100 ms each (stutter), and map loads of over a minute
# after long missions. A large initial heap lets it allocate that much before collecting
# (57 collections at startup become 2). AKANEIRO_GC_HEAP_MB=0 keeps Mono's default.
GC_HEAP_MB="${AKANEIRO_GC_HEAP_MB:-512}"
if [ "$GC_HEAP_MB" -gt 0 ] 2>/dev/null; then
    export GC_INITIAL_HEAP_SIZE="${GC_INITIAL_HEAP_SIZE:-$((GC_HEAP_MB * 1024 * 1024))}"
fi

# Notes memory use and saves thread stacks if the game hangs (data/logs). AKANEIRO_HANG_WATCH=0 = off.
if [ "${AKANEIRO_HANG_WATCH:-1}" != 0 ]; then
    # Garbage collection times in the game log, to tell GC pauses from other stalls.
    export GC_PRINT_STATS="${GC_PRINT_STATS:-1}"
    setsid -f "$LINUX_DIR/hang-watch.sh" </dev/null >/dev/null 2>&1
fi

# The game is 32-bit Unity 4.1. Started directly or inside the newer Steam Linux Runtime
# containers it crashes in the system's 32-bit Mesa driver; the old "scout" LD_LIBRARY_PATH
# runtime runs it fine.
RUNTIME_RUN="$(find_scout_runtime)" || true
if [ -n "$RUNTIME_RUN" ]; then
    exec "$RUNTIME_RUN" ./Akaneiro.x86 "$@"
fi
echo "No Steam scout runtime found (install \"Steam Linux Runtime 1.0 (scout)\" in Steam);" \
     "starting the game directly" >&2
exec ./Akaneiro.x86 "$@"
