#!/bin/bash
# Watches the running game, started in the background by run-game.sh:
# - every minute, notes the game's memory use (data/logs/watch-*.log);
# - when the game stops sending its pings to the server (every 2 seconds while it is connected)
#   for HANG_SECONDS while it still runs, saves what every thread is doing to
#   data/logs/hang-*.txt: thread states, and the C# stacks and locks from Mono's thread dump
#   (SIGQUIT, which the game survives), plus the last garbage collections.
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
# The game's preload libraries are not for gdb and the tools here.
unset LD_PRELOAD

HANG_SECONDS="${AKANEIRO_HANG_SECONDS:-20}"
mkdir -p "$AKANEIRO_DATA_DIR/logs"
WATCH_LOG="$AKANEIRO_DATA_DIR/logs/watch-$(date +%Y%m%d-%H%M%S).log"
ls -1t "$AKANEIRO_DATA_DIR/logs/watch-"*.log 2>/dev/null | tail -n +11 | xargs -r rm -f

# The game process (the newest Akaneiro.x86), waiting up to a minute for it to appear.
pid=""
for _ in $(seq 60); do
    pid="$(pgrep -n -x Akaneiro.x86)" && break
    sleep 1
done
[ -n "$pid" ] || exit 0

memory() {
    awk '/^(VmPeak|VmSize|VmRSS|Threads):/ {printf "%s %s %s  ", $1, $2, $3}' "/proc/$pid/status" 2>/dev/null
    echo "fds $(ls "/proc/$pid/fd" 2>/dev/null | wc -l)"
}

# Seconds since the last ping in the server log ("HH:MM:SS.fff info: ..." then "<- pingRequest").
seconds_since_ping() {
    local t
    t="$(grep -B1 -- '<- pingRequest' "$SERVER_LOG" 2>/dev/null | tail -2 | head -1 | cut -c1-8)"
    [[ "$t" =~ ^[0-9]{2}:[0-9]{2}:[0-9]{2}$ ]] || return 1
    local now then
    now=$(( 10#$(date +%H) * 3600 + 10#$(date +%M) * 60 + 10#$(date +%S) ))
    then=$(( 10#${t:0:2} * 3600 + 10#${t:3:2} * 60 + 10#${t:6:2} ))
    echo $(( (now - then + 86400) % 86400 ))
}

# The client's last message; while it is EnterScene the game loads a map and sends no pings.
last_client_message() {
    grep -E -- '^ +<- ' "$SERVER_LOG" 2>/dev/null | tail -1 | sed -E 's/^ +<- //'
}

report() {
    local out="$AKANEIRO_DATA_DIR/logs/hang-$(date +%Y%m%d-%H%M%S).txt"
    {
        echo "Game (PID $pid) sent no ping for $1 s at $(date '+%F %T')"
        echo "Memory: $(memory)"
        echo
        echo "== Threads (name, state, kernel wait)"
        for task in /proc/"$pid"/task/*; do
            printf "%-8s %-18s %s %s\n" "${task##*/}" "$(cat "$task/comm" 2>/dev/null)" \
                "$(awk '{print $3}' "$task/stat" 2>/dev/null)" "$(cat "$task/wchan" 2>/dev/null)"
        done
        echo
        # Mono prints every thread's C# stack and the locks it owns on SIGQUIT, into the game
        # log, and carries on. (Calling into the game with gdb instead crashes it.)
        local before
        before=$(grep -c "Full thread dump" "$UNITY_LOG" 2>/dev/null)
        kill -QUIT "$pid" 2>/dev/null
        for _ in $(seq 20); do
            sleep 1
            [ "$(grep -c "Full thread dump" "$UNITY_LOG" 2>/dev/null)" -gt "$before" ] && break
        done
        sleep 2
        echo "== C# threads (Mono thread dump)"
        awk -v n=$((before + 1)) '/Full thread dump/{c++} c==n' "$UNITY_LOG" 2>/dev/null \
            | grep -E '^"|^  at |^Full thread dump' | head -n 600
        echo
        echo "== Last garbage collections"
        grep -E "Complete collection took|heapsize =" "$UNITY_LOG" 2>/dev/null | tail -n 20
        echo
        echo "== Last lines of the game log"
        tail -n 40 "$UNITY_LOG" 2>/dev/null
    } >"$out" 2>&1
    echo "[$(date '+%T')] hang report: $out" >>"$WATCH_LOG"
}

echo "[$(date '+%T')] watching game PID $pid" >>"$WATCH_LOG"
reported=0
seen_ping=0
tick=0
while kill -0 "$pid" 2>/dev/null; do
    sleep 5
    tick=$((tick + 1))
    (( tick % 12 == 0 )) && echo "[$(date '+%T')] $(memory)" >>"$WATCH_LOG"
    quiet="$(seconds_since_ping)" || continue
    if [ "$quiet" -lt "$HANG_SECONDS" ]; then
        seen_ping=1
        reported=0
        continue
    fi
    # Only after this game pinged (not a ping from an older run), and not while a map loads.
    [ $seen_ping = 1 ] || continue
    [ "$(last_client_message)" = EnterScene ] && [ "$quiet" -lt 180 ] && continue
    if [ $reported = 0 ]; then
        report "$quiet"
        reported=1
    fi
done
echo "[$(date '+%T')] game ended" >>"$WATCH_LOG"
