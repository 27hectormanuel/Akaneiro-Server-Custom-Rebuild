# Shared settings and helpers for the Linux scripts. Sourced, not run.
# Every setting can be overridden from the environment, e.g. AKANEIRO_HTTP_PORT=8881 ./linux/play.sh

LINUX_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(dirname "$LINUX_DIR")"

# Web service port. The game always calls port 80; akaneiro_redirect.so moves that to this port,
# so the server needs no root. (8080 and 8081 are taken by Steam on a Steam Deck.)
export AKANEIRO_HTTP_PORT="${AKANEIRO_HTTP_PORT:-8880}"
# Game server port (unprivileged already).
AKANEIRO_GAME_PORT="${AKANEIRO_GAME_PORT:-7001}"
# Address of the server as the game sees it. Set to the server's LAN address on other PCs.
export AKANEIRO_SERVER_IP="${AKANEIRO_SERVER_IP:-127.0.0.1}"
# Where the web service listens: 127.0.0.1 = this PC only, 0.0.0.0 = the whole network.
AKANEIRO_LISTEN="${AKANEIRO_LISTEN:-127.0.0.1}"
# Save games and the server log.
AKANEIRO_DATA_DIR="${AKANEIRO_DATA_DIR:-$REPO_DIR/data}"

SERVER_LOG="$AKANEIRO_DATA_DIR/server.log"
SERVER_PID="$AKANEIRO_DATA_DIR/server.pid"
STEAM_ROOT="${STEAM_ROOT:-$HOME/.steam/steam}"

find_dotnet() {
    if command -v dotnet >/dev/null 2>&1; then command -v dotnet
    elif [ -x "$HOME/.dotnet/dotnet" ]; then echo "$HOME/.dotnet/dotnet"
    else
        echo "The .NET 8 SDK was not found. Install it without root with:" >&2
        echo "  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0" >&2
        return 1
    fi
}

# LD_PRELOAD value with both builds of the redirect library; the loader takes the one that
# matches the program (32-bit game, 64-bit helpers) and skips the other with a harmless warning.
redirect_preload() {
    local lib32="$LINUX_DIR/redirect/lib32/akaneiro_redirect.so"
    local lib64="$LINUX_DIR/redirect/lib64/akaneiro_redirect.so"
    if [ ! -f "$lib32" ] || [ ! -f "$lib64" ]; then
        sh "$LINUX_DIR/redirect/build.sh" >&2 || return 1
    fi
    echo "$lib32:$lib64${LD_PRELOAD:+:$LD_PRELOAD}"
}

server_running() {
    [ -f "$SERVER_PID" ] && kill -0 "$(cat "$SERVER_PID")" 2>/dev/null
}

# True once the web service answers.
server_ready() {
    (exec 3<>"/dev/tcp/$AKANEIRO_SERVER_IP/$AKANEIRO_HTTP_PORT") 2>/dev/null
}

# True when the server is meant to run on this PC (play.sh and steam-launch.sh only start it then).
server_is_local() {
    [ "$AKANEIRO_SERVER_IP" = 127.0.0.1 ] || [ "$AKANEIRO_SERVER_IP" = localhost ] \
        || ip -o addr show 2>/dev/null | grep -qw "inet $AKANEIRO_SERVER_IP"
}

# run.sh of the old scout LD_LIBRARY_PATH runtime. Steam used to ship it as ubuntu12_32/steam-runtime;
# since late 2026 that is a stub that runs games on the host, and the runtime lives in
# steamapps/common/LegacySteamRuntime or, still, in "Steam Linux Runtime 1.0 (scout)".
find_scout_runtime() {
    local libs=("$STEAM_ROOT/steamapps") lib run
    # Other Steam libraries (SD cards, extra drives).
    while IFS= read -r lib; do libs+=("$lib/steamapps"); done < <(
        grep -oP '^\s*"path"\s*"\K[^"]+' "$STEAM_ROOT/steamapps/libraryfolders.vdf" 2>/dev/null)
    for lib in "${libs[@]}"; do
        for run in "$lib/common/LegacySteamRuntime/run.sh" "$lib/common/SteamLinuxRuntime/steam-runtime/run.sh"; do
            [ -x "$run" ] && { echo "$run"; return 0; }
        done
    done
    run="$STEAM_ROOT/ubuntu12_32/steam-runtime/run.sh"
    if [ -x "$run" ] && [ "$(basename "$(realpath "$run")")" != legacy-run-stub.sh ]; then
        echo "$run"; return 0
    fi
    return 1
}
