#!/bin/bash
# Runs the Akaneiro server on Linux without root.
#   linux/server.sh            run in the foreground (Ctrl+C stops it)
#   linux/server.sh start      start in the background (log: data/server.log)
#   linux/server.sh stop       stop the background server
#   linux/server.sh status     show whether it runs
# Extra arguments are passed to the server, e.g. linux/server.sh -- --Akaneiro:LogPackets=false
set -e
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

case "$1" in
    run|start|stop|status) cmd="$1"; shift ;;
    *) cmd=run ;;
esac
[ "$1" = "--" ] && shift

# The server runs from its project folder (relative settings resolve against it, as with "dotnet run").
SERVER_DLL="$REPO_DIR/src/Akaneiro.Server/bin/Release/net8.0/Akaneiro.Server.dll"

build() {
    DOTNET="$(find_dotnet)"
    export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
    "$DOTNET" build "$REPO_DIR/src/Akaneiro.Server" -c Release -v quiet -nologo >/dev/null || "$DOTNET" build "$REPO_DIR/src/Akaneiro.Server" -c Release -nologo >&2
}

SERVER_ARGS=(
    --Akaneiro:HttpPort="$AKANEIRO_HTTP_PORT"
    --Akaneiro:HttpListenAddress="$AKANEIRO_LISTEN"
    --Akaneiro:GamePort="$AKANEIRO_GAME_PORT"
    --Akaneiro:PublicHost="$AKANEIRO_SERVER_IP"
    --Akaneiro:DataDir="$AKANEIRO_DATA_DIR"
    # Times in the log, to see how long the game waited for an answer.
    --Logging:Console:FormatterName=simple
    --Logging:Console:FormatterOptions:TimestampFormat="HH:mm:ss.fff "
)

mkdir -p "$AKANEIRO_DATA_DIR"
case "$cmd" in
    run)
        build
        cd "$REPO_DIR/src/Akaneiro.Server"
        exec "$DOTNET" "$SERVER_DLL" "${SERVER_ARGS[@]}" "$@"
        ;;
    start)
        if server_running; then echo "Server already running (PID $(cat "$SERVER_PID"))"; exit 0; fi
        build
        cd "$REPO_DIR/src/Akaneiro.Server"
        keep_log "$SERVER_LOG" server
        nohup "$DOTNET" "$SERVER_DLL" "${SERVER_ARGS[@]}" "$@" >"$SERVER_LOG" 2>&1 &
        echo $! >"$SERVER_PID"
        for _ in $(seq 60); do
            server_ready && { echo "Server running (PID $(cat "$SERVER_PID")), log: $SERVER_LOG"; exit 0; }
            server_running || break
            sleep 0.5
        done
        echo "Server did not start, see $SERVER_LOG:" >&2
        tail -n 20 "$SERVER_LOG" >&2
        exit 1
        ;;
    stop)
        if server_running; then
            pid="$(cat "$SERVER_PID")"
            kill -TERM "$pid" 2>/dev/null || true
            echo "Server stopped"
        else
            echo "Server not running"
        fi
        rm -f "$SERVER_PID"
        ;;
    status)
        if server_running; then echo "Server running (PID $(cat "$SERVER_PID")), web service on $AKANEIRO_LISTEN:$AKANEIRO_HTTP_PORT, game port $AKANEIRO_GAME_PORT"
        else echo "Server not running"; exit 1; fi
        ;;
esac
