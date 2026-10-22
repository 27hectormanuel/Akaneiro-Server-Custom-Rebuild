#!/bin/bash
# Watches the running game, started in the background by run-game.sh:
# - every minute, notes the game's memory use (data/logs/watch-*.log);
# - notes map loads that take longer than 20 seconds;
# - when the game, while it sends its pings to the server (every 2 seconds in missions; not on
#   every screen), sends nothing at all for HANG_SECONDS while it still runs, saves what every
#   thread is doing to
#   data/logs/hang-*.txt: thread states, and the C# stacks and locks from Mono's thread dump
#   (SIGQUIT, which the game survives), plus the last garbage collections.
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
# The game's preload libraries (Steam's overlay) are not for this script: in bash they print
# "skipping destruction" for every subshell.
[ -n "$LD_PRELOAD" ] && exec env -u LD_PRELOAD bash "$0" "$@"

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

# Seconds of the day of a server log line ("HH:MM:SS.fff info: ..."), and of now.
day_seconds() { echo $(( 10#${1:0:2} * 3600 + 10#${1:3:2} * 60 + 10#${1:6:2} )); }
now_seconds() { day_seconds "$(date +%T)"; }

# The client's messages in the server log as "HH:MM:SS name", newest last.
client_messages() {
    awk '/^[0-9][0-9]:[0-9][0-9]:[0-9][0-9]/ {t = substr($0, 1, 8)}
         /^ +<- / {sub(/^ +<- /, ""); print t, $1}' "$SERVER_LOG" 2>/dev/null | tail -n "$1"
}

# "quiet pinging last": seconds since the client's last message; 1 if it was still on a screen that
# pings (its last two pings came about 2 seconds apart, the last one shortly before its last
# message); and the name of its last message.
client_state() {
    local last pings p1 p2 pinging=0
    last="$(client_messages 1)"
    [[ "$last" =~ ^[0-9]{2}:[0-9]{2}:[0-9]{2}\  ]] || return 1
    pings="$(client_messages 200 | grep ' pingRequest$' | tail -2 | cut -c1-8)"
    p1="$(head -1 <<<"$pings")" p2="$(tail -1 <<<"$pings")"
    if [ -n "$p1" ] && [ "$p1" != "$p2" ]; then
        (( ($(day_seconds "$p2") - $(day_seconds "$p1") + 86400) % 86400 <= 4 )) \
            && (( ($(day_seconds "$last") - $(day_seconds "$p2") + 86400) % 86400 <= 3 )) && pinging=1
    fi
    echo "$(( ($(now_seconds) - $(day_seconds "$last") + 86400) % 86400 )) $pinging ${last#* }"
}

# Map loads (EnterScene to MapLoadOK) noted so far.
noted_loads=0
note_slow_loads() {
    local loads log="${1:-$WATCH_LOG}"
    loads="$(client_messages +1 | awk '$2 == "EnterScene" {s = $1} $2 == "MapLoadOK" && s {print s, $1; s = ""}')"
    local count
    count=$(grep -c . <<<"$loads")
    [ -n "$loads" ] || count=0
    [ "$count" -gt "$noted_loads" ] || return 0
    tail -n $((count - noted_loads)) <<<"$loads" | while read -r from to; do
        local took=$(( ($(day_seconds "$to") - $(day_seconds "$from") + 86400) % 86400 ))
        [ "$took" -gt 20 ] && echo "[$(date '+%T')] map load $from-$to took $took s ($(memory))" >>"$log"
    done
    noted_loads=$count
}

report() {
    local out="$AKANEIRO_DATA_DIR/logs/hang-$(date +%Y%m%d-%H%M%S).txt"
    {
        echo "Game (PID $pid) sent nothing for $1 s at $(date '+%F %T')"
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
# Loads from before this game started are not its own.
note_slow_loads /dev/null
reported=0
tick=0
while kill -0 "$pid" 2>/dev/null; do
    sleep 5
    tick=$((tick + 1))
    (( tick % 12 == 0 )) && echo "[$(date '+%T')] $(memory)" >>"$WATCH_LOG"
    note_slow_loads
    read -r quiet pinging last < <(client_state) || continue
    if [ "$quiet" -lt "$HANG_SECONDS" ]; then
        reported=0
        continue
    fi
    # Silence is only a hang on a screen that pings, and not while a map loads.
    [ "$pinging" = 1 ] && [ "$last" != EnterScene ] || continue
    if [ $reported = 0 ]; then
        report "$quiet"
        reported=1
    fi
done
echo "[$(date '+%T')] game ended" >>"$WATCH_LOG"
