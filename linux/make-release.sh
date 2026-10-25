#!/bin/bash
# Builds the Linux / SteamOS release into dist/:
#   akaneiro-server-linux-<version>.tar.gz          the release folder (server prebuilt, no .NET or
#                                                   compiler needed); run linux/install.sh in it
#   akaneiro-server-linux-<version>-installer.sh    the same as one file that unpacks itself into
#                                                   ~/Akaneiro-Server and runs install.sh
#   linux/make-release.sh [version, default: git describe]
set -e
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
cd "$REPO_DIR"

VERSION="${1:-$(git describe --tags --always --dirty)}"
NAME="akaneiro-server-linux-$VERSION"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
OUT="$STAGE/akaneiro-server"
mkdir -p "$OUT" dist

echo "Building the redirect library"
sh linux/redirect/build.sh

echo "Building the server (self-contained, linux-x64)"
DOTNET="$(find_dotnet)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
"$DOTNET" publish src/Akaneiro.Server -c Release -r linux-x64 --self-contained -nologo -v quiet \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
    -o "$OUT/server"

# The scripts, the client's data tables the server reads, and the documentation, as in the repository.
git ls-files -z --cached --others --exclude-standard -- linux client-source README.txt HOSTING.txt KNOWN-ISSUES.txt \
    | xargs -0 cp --parents -t "$OUT"
cp --parents linux/redirect/lib32/akaneiro_redirect.so linux/redirect/lib64/akaneiro_redirect.so "$OUT"
echo "$VERSION" >"$OUT/VERSION"

tar -C "$STAGE" -czf "dist/$NAME.tar.gz" akaneiro-server

# Self-extracting installer: a short script with the archive appended.
{
    cat <<'EOF'
#!/bin/bash
# Akaneiro private server for Linux and SteamOS. Run it once in Desktop Mode, in a terminal:
#   bash akaneiro-server-linux-*-installer.sh
# It unpacks into ~/Akaneiro-Server (or $AKANEIRO_INSTALL_DIR) and sets up Steam, so Akaneiro
# started from Steam, also in Game Mode, plays on the private server. Running a newer installer
# updates that folder; save games in its data folder stay.
set -e
DEST="${AKANEIRO_INSTALL_DIR:-$HOME/Akaneiro-Server}"
if [ -f "$DEST/data/server.pid" ] && kill -0 "$(cat "$DEST/data/server.pid")" 2>/dev/null; then
    echo "The Akaneiro server is running; quit the game first." >&2
    exit 1
fi
echo "Unpacking into $DEST"
mkdir -p "$DEST"
line=$(awk '/^__ARCHIVE_BELOW__$/ {print NR + 1; exit}' "$0")
tail -n +"$line" "$0" | tar -xz -C "$DEST" --strip-components=1
exec bash "$DEST/linux/install.sh" "$@"
__ARCHIVE_BELOW__
EOF
    cat "dist/$NAME.tar.gz"
} >"dist/$NAME-installer.sh"
chmod +x "dist/$NAME-installer.sh"

ls -lh "dist/$NAME.tar.gz" "dist/$NAME-installer.sh"
