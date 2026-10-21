#!/bin/bash
# Prepares a stand-alone copy of the native Linux game (Unity 4, Steam app 230310) to play with
# linux/play.sh, from an existing Linux install of the game:
#
#   linux/setup-client.sh "<folder with Akaneiro.x86>" [target folder, default: client]
#
# Big data files are linked, not copied. The executables and native libraries are copied (Unity
# looks for Akaneiro_Data next to the real binary, so they must not be symlinks). Not needed when
# the game is installed through Steam: see linux/steam-launch.sh.
set -e
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

SRC="${1:?usage: $0 <game folder with Akaneiro.x86> [target folder]}"
DEST="${2:-$REPO_DIR/client}"
SRC="$(cd "$SRC" && pwd)"
[ -f "$SRC/Akaneiro.x86" ] && [ -d "$SRC/Akaneiro_Data/Managed" ] \
    || { echo "'$SRC' is not a Linux install of Akaneiro (no Akaneiro.x86 / Akaneiro_Data)" >&2; exit 1; }

# The server is made for the unmodified Steam client: its Assembly-CSharp.dll logs in through
# http://mw.angry-red.com. Patched builds (local logins, 127.0.0.1 addresses) do not work with it.
is_stock_dll() {
    python3 -c '
import sys
d = open(sys.argv[1], "rb").read()
u = lambda s: s.encode("utf-16-le")
sys.exit(0 if u("http://mw.angry-red.com") in d and u("127.0.0.1") not in d else 1)' "$1"
}

MANAGED="$SRC/Akaneiro_Data/Managed"
CLIENT_DLL=""
for candidate in "$MANAGED/Assembly-CSharp.dll.bak" "$MANAGED/Assembly-CSharp.dll"; do
    if [ -f "$candidate" ] && is_stock_dll "$candidate"; then CLIENT_DLL="$candidate"; break; fi
done
[ -n "$CLIENT_DLL" ] || { echo "No unmodified Assembly-CSharp.dll in $MANAGED" >&2; exit 1; }

if [ -e "$DEST" ]; then
    echo "'$DEST' already exists; remove it first to set it up again." >&2
    exit 1
fi
mkdir -p "$DEST"

# Mirror the folder as links (directories are created, files become symlinks), then replace what
# has to be a real file.
cp -rs "$SRC/." "$DEST/"
for f in Akaneiro.x86 Akaneiro.x86_64 libsteam_api.so libSteamworksNative.so steam_appid.txt; do
    [ -e "$SRC/$f" ] && { rm -f "$DEST/$f"; cp "$SRC/$f" "$DEST/$f"; }
done
rm -f "$DEST/Akaneiro_Data/Managed/Assembly-CSharp.dll" "$DEST/Akaneiro_Data/Managed/Assembly-CSharp.dll.bak"
cp "$CLIENT_DLL" "$DEST/Akaneiro_Data/Managed/Assembly-CSharp.dll"
# Unity writes its log here; keep the source's file untouched.
rm -f "$DEST/Akaneiro_Data/output_log.txt"
[ -f "$DEST/steam_appid.txt" ] || echo 230310 >"$DEST/steam_appid.txt"
chmod +x "$DEST/Akaneiro.x86" "$DEST/Akaneiro.x86_64" 2>/dev/null || true

echo "Client ready in $DEST (game code: ${CLIENT_DLL#$SRC/})"
echo "Play with: $LINUX_DIR/play.sh"
