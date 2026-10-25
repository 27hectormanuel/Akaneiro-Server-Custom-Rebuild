#!/bin/bash
# One-time setup on Linux and SteamOS, run once in Desktop Mode. Afterwards Akaneiro started from
# Steam (the Play button, also in Game Mode) starts this private server with it.
#   linux/install.sh              set up (again: safe to repeat, e.g. after an update)
#   linux/install.sh --uninstall  take the Steam launch options out again (save games stay in data/)
set -e
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

say() { echo "==> $*"; }
# Started from a file manager, the terminal closes when the script ends: keep it open.
finish() {
    local status=$?
    [ -t 0 ] && [ -t 1 ] && read -rp "Press Enter to close. " _
    exit $status
}
trap finish EXIT
fail() { echo; echo "Not finished: $*" >&2; exit 1; }

if [ "$1" = --uninstall ]; then
    "$LINUX_DIR/steam-set-launch-options.sh" --remove
    say "Akaneiro starts without the private server again. Save games stay in $AKANEIRO_DATA_DIR."
    exit 0
fi

[ -d "$STEAM_ROOT/steamapps" ] || fail "Steam was not found ($STEAM_ROOT). Install Steam and log in first."

say "Looking for Akaneiro: Demon Hunters in your Steam libraries"
if ! GAME_DIR="$(find_steam_game)"; then
    xdg-open steam://install/230310 >/dev/null 2>&1 || true
    fail "the Linux version of Akaneiro is not installed. Steam now offers to install it; when it is
done, run this again. (If Akaneiro is set to run with Proton in Steam, turn that off in its
Properties > Compatibility: the private server uses the native Linux version.)"
fi
echo "    $GAME_DIR"

say "Looking for Steam Linux Runtime 1.0 (scout), which the game runs in"
if ! RUNTIME="$(find_scout_runtime)"; then
    xdg-open steam://install/1070560 >/dev/null 2>&1 || true
    fail "Steam Linux Runtime 1.0 (scout) is not installed. Steam now offers to install it (it is a
free tool in your library); when it is done, run this again."
fi
echo "    $RUNTIME"

say "Checking the redirect library"
redirect_preload >/dev/null || fail "the redirect library is missing and could not be built (see above)."

say "Testing the server"
if server_running || server_ready; then
    echo "    already running"
else
    "$LINUX_DIR/server.sh" start || fail "the server did not start (see above)."
    "$LINUX_DIR/server.sh" stop >/dev/null
fi

# Speech bubbles use the game's own "edo" font, taken from the game's data (see run-game.sh). That
# needs the Python modules UnityPy and fontTools, installed here into .venv.
FONT="$AKANEIRO_DATA_DIR/fonts/edo/arial.ttf"
if [ ! -f "$FONT" ]; then
    say "Making the speech bubble font from the game's data (downloads UnityPy and fontTools once)"
    if { [ -x "$REPO_DIR/.venv/bin/python" ] || python3 -m venv "$REPO_DIR/.venv"; } \
        && "$REPO_DIR/.venv/bin/python" -m pip install -q --disable-pip-version-check UnityPy fonttools \
        && "$REPO_DIR/.venv/bin/python" -I "$LINUX_DIR/fonts/make-bubble-font.py" "$GAME_DIR" "$FONT"; then
        :
    elif [ -n "$AKANEIRO_FONT_DIR" ]; then
        echo "    Could not make it; the bubbles use Proton's Arial instead."
    else
        echo "    Could not make it, and no Proton is installed for the Arial fallback: speech bubbles"
        echo "    will show no text. Install any Proton version in Steam, or run this again later."
    fi
fi

say "Setting Akaneiro's launch options in Steam (Steam closes and starts again)"
"$LINUX_DIR/steam-set-launch-options.sh" || fail "the launch options could not be set (see above)."

echo
say "Done. Start Akaneiro from Steam, in Desktop or Game Mode: the server starts with the game and"
echo "    stops with it. Save games: $AKANEIRO_DATA_DIR/accounts. Logs: $AKANEIRO_DATA_DIR/logs."
echo "    Keep this folder where it is ($REPO_DIR); Steam starts the game through it."
