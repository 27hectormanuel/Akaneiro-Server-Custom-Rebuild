#!/bin/bash
# Steam launch option wrapper. In Steam: Akaneiro > Properties > General > Launch Options:
#
#   "/path/to/Akaneiro-Server-Custom-Rebuild/linux/steam-launch.sh" %command%
#
# Starts the server if needed, then the game with the redirect, and stops the server when the game
# ends. The game is started through the ubuntu12_32 Steam runtime, not the container Steam would
# use (see run-game.sh), so the Steam compatibility setting does not matter.
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

# Find the game folder in Steam's command line (the path of its Akaneiro or Akaneiro.x86 script).
GAME_DIR=""
for arg in "$@"; do
    case "$(basename -- "$arg")" in
        Akaneiro|Akaneiro.x86|Akaneiro.x86_64)
            [ -f "$arg" ] && { GAME_DIR="$(cd "$(dirname -- "$arg")" && pwd)"; break; } ;;
    esac
done

started=0
if server_is_local && ! server_running && ! server_ready; then
    "$LINUX_DIR/server.sh" start >&2 && started=1
fi

if [ -n "$GAME_DIR" ]; then
    "$LINUX_DIR/run-game.sh" "$GAME_DIR"
else
    # Unknown command line: run it as Steam gave it, with the redirect.
    LD_PRELOAD="$(redirect_preload)" "$@"
fi
status=$?
[ $started = 1 ] && "$LINUX_DIR/server.sh" stop >&2
exit $status
