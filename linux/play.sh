#!/bin/bash
# One command to play: starts the server (if it does not run yet), starts the game, and stops the
# server again when the game ends (only if this script started it). Steam must be running and
# logged in: the game logs in with your Steam account.
#   linux/play.sh [game folder, default: client or the Steam install] [Unity player arguments...]
set -e
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

GAME_DIR=""
if [ -n "$1" ] && [ -d "$1" ]; then GAME_DIR="$1"; shift; fi
if [ -z "$GAME_DIR" ]; then
    for d in "$REPO_DIR/client" \
             "$STEAM_ROOT/steamapps/common/Akaneiro Demon Hunters" \
             /run/media/*/*/steamapps/common/"Akaneiro Demon Hunters"; do
        [ -f "$d/Akaneiro.x86" ] && { GAME_DIR="$d"; break; }
    done
fi
[ -n "$GAME_DIR" ] || { echo "Game not found: install it in Steam or run linux/setup-client.sh first" >&2; exit 1; }

pgrep -x steam >/dev/null || pgrep -f ubuntu12_32/steam >/dev/null \
    || echo "Warning: Steam does not seem to run; the game needs it to log in." >&2

started=0
if server_is_local && ! server_running && ! server_ready; then
    "$LINUX_DIR/server.sh" start
    started=1
fi

echo "Starting the game from $GAME_DIR"
set +e
"$LINUX_DIR/run-game.sh" "$GAME_DIR" "$@"
status=$?
[ $started = 1 ] && "$LINUX_DIR/server.sh" stop
exit $status
