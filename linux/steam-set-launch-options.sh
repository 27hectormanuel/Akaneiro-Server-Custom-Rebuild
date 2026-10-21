#!/bin/bash
# Sets Akaneiro's Steam launch options to linux/steam-launch.sh, so the official Steam copy
# (Play button, steam://launch/230310) starts with the private server. Steam rewrites its config
# when it exits, so this closes Steam, edits the config and starts Steam again.
#   linux/steam-set-launch-options.sh          set them
#   linux/steam-set-launch-options.sh --remove take them out again
set -e
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

APPID=230310
STEAM_DATA="$HOME/.local/share/Steam"
OPTIONS="\"$LINUX_DIR/steam-launch.sh\" %command%"
[ "$1" = --remove ] && OPTIONS=""

steam_running() { pgrep -f "ubuntu12_32/steam( |$)" >/dev/null || pgrep -x steam >/dev/null; }

if pgrep -f "Akaneiro.x86" >/dev/null; then
    echo "Akaneiro is running; quit it first." >&2
    exit 1
fi

was_running=0
if steam_running; then
    was_running=1
    echo "Closing Steam..."
    steam -shutdown >/dev/null 2>&1 || true
    for _ in $(seq 60); do steam_running || break; sleep 1; done
    steam_running && { echo "Steam did not close." >&2; exit 1; }
fi

for cfg in "$STEAM_DATA"/userdata/*/config/localconfig.vdf; do
    [ -f "$cfg" ] || continue
    cp "$cfg" "$cfg.akaneiro-backup"
    python3 - "$cfg" "$APPID" "$OPTIONS" <<'EOF'
import re, sys
path, appid, options = sys.argv[1], sys.argv[2], sys.argv[3]
text = open(path, encoding="utf-8").read()
# The per-app block lives under UserLocalConfigStore/Software/Valve/Steam/apps.
apps = re.search(r'\n(\t+)"apps"\n\1\{\n', text)
if not apps:
    sys.exit(f"{path}: no apps section, skipped")
indent = apps.group(1) + "\t"
block = re.compile(r'(\n' + indent + '"' + appid + r'"\n' + indent + r'\{\n)(.*?)(\n' + indent + r'\})', re.S)
value = options.replace("\\", "\\\\").replace('"', '\\"')
line = indent + '\t"LaunchOptions"\t\t"' + value + '"'
m = block.search(text, apps.end() - 1)
if m:
    lines = [l for l in m.group(2).split("\n") if l and not l.startswith(indent + '\t"LaunchOptions"')]
    if options:
        lines.insert(0, line)
    body = "\n".join(lines)
    text = text[:m.start()] + m.group(1) + body + m.group(3) + text[m.end():]
elif options:
    entry = "\n" + indent + '"' + appid + '"\n' + indent + "{\n" + line + "\n" + indent + "}"
    pos = apps.end() - 1
    text = text[:pos] + entry + text[pos:]
open(path, "w", encoding="utf-8").write(text)
print(f"{path}: launch options {'set' if options else 'removed'}")
EOF
done

if [ $was_running = 1 ]; then
    echo "Starting Steam again..."
    setsid -f steam >/dev/null 2>&1 </dev/null
fi
