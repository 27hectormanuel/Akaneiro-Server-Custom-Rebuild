PLAYING ON LINUX (AND STEAM DECK)
=================================

The server itself runs on Linux unchanged (all tests pass). This folder adds what the Windows
guide does with the hosts file, port 80 and Steam, without needing root:

- The native Linux version of the game (Steam app 230310, Unity 4.1, 32-bit) is used, not the
  Windows one under Proton. The game is not modified.
- redirect/akaneiro_redirect.so is loaded into the game with LD_PRELOAD. It answers the game's
  lookups of mw.angry-red.com with the server's address (instead of /etc/hosts) and moves its
  connection to port 80 to the server's web port, 8880 (ports below 1024 need root on Linux).
- Speech bubbles use the system font "Arial", which this Unity version only looks for in
  /usr/share/fonts. The redirect library adds a folder of our own to that search. By default it
  holds the game's own "edo" font (taken from your game data on the first start and named Arial,
  in data/fonts). AKANEIRO_BUBBLE_FONT=arial gives the original look instead: Proton's free
  Arial replacement (any installed Proton, found automatically). Without either the bubbles
  appear without text.
- The game is started through Steam's old "scout" runtime (from Steam Linux Runtime 1.0, or the
  former ubuntu12_32/steam-runtime, which Steam replaced with a stub in late 2026). Started directly, or inside the
  newer Steam Linux Runtime containers, this Unity version crashes in the 32-bit Mesa driver.


WHAT YOU NEED
-------------

- Steam, running and logged in, with Akaneiro: Demon Hunters in your library. The game logs in
  with your Steam account, as on Windows.
- The .NET 8 SDK. Without root:
      curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
  (installs to ~/.dotnet, which the scripts find on their own).
- To build the redirect library: a C compiler that can build 32-bit code, or zig. Without root:
      python3 -m pip install --user ziglang
  It is built automatically the first time it is needed (or: sh linux/redirect/build.sh).


OPTION A: FROM STEAM (RECOMMENDED)
----------------------------------

1. Install Akaneiro in Steam (the native Linux version; do not force Proton for it).
2. Properties > General > Launch Options:

       "/full/path/to/Akaneiro-Server-Custom-Rebuild/linux/steam-launch.sh" %command%

   Or let linux/steam-set-launch-options.sh do it (it closes and restarts Steam, which rewrites
   that setting on exit; --remove takes it out again).
3. Press Play (or open steam://launch/230310). The server starts with the game and stops when the game ends.

The Steam compatibility setting does not matter: the wrapper starts the game through the
scout runtime itself.


OPTION B: A STAND-ALONE COPY
----------------------------

For a copy of the Linux game outside Steam (Steam still has to run for the login):

    linux/setup-client.sh "/path/to/Akaneiro Demon Hunters"   # once, creates ./client
    linux/play.sh                                            # server + game

setup-client.sh links the big data files and copies the rest. If the folder holds a patched
Assembly-CSharp.dll (local logins, 127.0.0.1 addresses), it uses the unmodified
Assembly-CSharp.dll.bak next to it; without an unmodified one it stops.
play.sh also finds a Steam install by itself. Unity player arguments go at the end, for example
windowed: linux/play.sh -screen-width 1280 -screen-height 800 -screen-fullscreen 0


THE SERVER ON ITS OWN
---------------------

    linux/server.sh             foreground, Ctrl+C stops it
    linux/server.sh start       background (log: data/server.log)
    linux/server.sh stop
    linux/server.sh status

Settings are environment variables (defaults in linux/common.sh):

AKANEIRO_HTTP_PORT (8880)    web service port. 8080 and 8081 are taken by Steam on a Steam Deck.
AKANEIRO_GAME_PORT (7001)    game server port.
AKANEIRO_SERVER_IP (127.0.0.1)
                             the server's address as players see it; used by the server
                             (PublicHost) and by the redirect library in the game.
AKANEIRO_LISTEN (127.0.0.1)  where the web service listens; 0.0.0.0 for a home network.
AKANEIRO_DATA_DIR (data)     save games and the server log.
AKANEIRO_BUBBLE_FONT (edo)   font of the speech bubbles: edo (the game's own font) or arial
                             (the original look). In Steam, put it before the wrapper:
                             AKANEIRO_BUBBLE_FONT=arial "/path/to/linux/steam-launch.sh" %command%

Save games are the same JSON files as on Windows (data/accounts/<Steam ID>.json), so they can
be copied between both.


HOME NETWORK
------------

On the server PC:

    AKANEIRO_LISTEN=0.0.0.0 AKANEIRO_SERVER_IP=192.168.0.10 linux/server.sh

(with the server's own address), and allow TCP ports 8880 and 7001 in its firewall if it has one.
On every other Linux PC, start the game with the same AKANEIRO_SERVER_IP, for example in Steam:

    AKANEIRO_SERVER_IP=192.168.0.10 "/path/to/linux/steam-launch.sh" %command%

play.sh and steam-launch.sh only start a server of their own when AKANEIRO_SERVER_IP is an
address of the PC they run on.
Windows PCs keep using the hosts file; they need the web service on port 80, so for mixed
networks run the server with AKANEIRO_HTTP_PORT=80 (needs root, or
sysctl net.ipv4.ip_unprivileged_port_start=80).


TROUBLESHOOTING
---------------

- The game hangs at the login: is Steam running? The server log (data/server.log) shows
  "Steam authorize" when the game got through to the web service. If it is missing, run the game
  with AKANEIRO_REDIRECT_DEBUG=1 to see every redirect on the terminal.
- "DllNotFoundException: SteamworksNative" in the game log: the game was started without its
  folder on the library path. Start it through run-game.sh, play.sh or steam-launch.sh.
- "wrong ELF class" lines on the terminal are harmless: both the 32-bit and the 64-bit redirect
  library are preloaded and each program takes the one that fits.
- Started from Steam (also in Game Mode) and nothing happens: data/launch.log has the output of
  the last launch. "No Steam scout runtime found" means Steam Linux Runtime 1.0 (scout) is not
  installed: install it from the Steam library (Tools), or any game that uses it.
- Speech bubbles without text: no Proton is installed (install any Proton version in Steam) or
  AKANEIRO_FONT_DIR points to a folder without arial.ttf.
- After a SteamOS update the redirect library will not build ("No module named ziglang"): the
  system Python changed; recreate the zig environment with
      rm -rf ../.toolchain && python3 -m venv ../.toolchain && ../.toolchain/bin/pip install ziglang
- Logs of the last 10 runs are kept in data/logs (server-*, game-*, launch-*), so a problem can
  still be looked at after the game was started again.
- The game freezes: linux/hang-watch.sh runs with every game. When the game stops pinging the
  server for 20 seconds, it saves the stacks of all game threads to data/logs/hang-*.txt, and it
  notes the game's memory use every minute in data/logs/watch-*.log. AKANEIRO_HANG_WATCH=0
  turns it off; AKANEIRO_ALLOW_DEBUG=0 keeps debuggers out of the game.
- The game's own log: ~/.config/unity3d/Spicy Horse/Akaneiro/Player.log
