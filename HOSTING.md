# Hosting the server yourself

This guide sets up the server on your own Windows PC, so you can play *Akaneiro: Demon Hunters*
from Steam again. Setting it up for several PCs in your home network is covered further down.

## Do I need MAMP, XAMPP or a web server?

No. The server is one self-contained .NET program. It already contains everything the game
talks to:

| Part | Port | What it does |
|---|---|---|
| Web service | 80 (HTTP) | Stands in for the old login service at `mw.angry-red.com`: the game sends its Steam login there and is told which game server to use. |
| Game server | 7001 (TCP) | Everything in the game: characters, village, missions, monsters, loot, saving. |

MAMP, XAMPP, IIS or Apache are not used. PHP and MySQL are not used either; save games are
plain JSON files. A web server that is already running on port 80 is actually in the way (see
[Troubleshooting](#troubleshooting)).

## What you need

- **Windows 10 or 11.** The game is a Windows game. The server itself runs anywhere .NET runs,
  but the steps below are for Windows.
- **Akaneiro: Demon Hunters** in your Steam library, installed. The game is not modified.
- **The .NET 8 SDK**: <https://dotnet.microsoft.com/download/dotnet/8.0>, or in a terminal:

      winget install Microsoft.DotNet.SDK.8

- **This repository.** On its GitHub page choose **Code > Download ZIP** and unpack it, for
  example to `C:\Akaneiro`. That gives you the server folder
  `C:\Akaneiro\Akaneiro-Server-Custom-Rebuild-master`. (With Git:
  `git clone https://github.com/Paprikhan/Akaneiro-Server-Custom-Rebuild.git`.)

  Everything the server needs from the game client is already in it (`client-source`).

## Step 1: send the game to your own PC

The game always connects to `mw.angry-red.com`. The Windows *hosts* file redirects that name to
your own PC.

1. Open the Start menu, type `Notepad`, right-click it and choose **Run as administrator**.
2. In Notepad choose **File > Open**, go to `C:\Windows\System32\drivers\etc\`, set the file type
   filter to **All files** and open `hosts`.
3. Add this line at the end and save:

       127.0.0.1 mw.angry-red.com

4. Optional, in a terminal: `ipconfig /flushdns`, so Windows forgets any old address.

To undo it later, delete the line again.

## Step 2: start the server

1. Open a terminal: open the Start menu, type `PowerShell` and start **Windows PowerShell**.
2. Go to the server folder: type `cd`, a space, and the folder's path in quotes, then press Enter.
   For the example folder from above:

       cd "C:\Akaneiro\Akaneiro-Server-Custom-Rebuild-master"

   Tip: type `cd ` and drag the folder from the Explorer into the window, which pastes its path.
   You are in the right folder when `dir` lists `README.md` and the `src` folder.
3. Start the server:

       dotnet run --project src/Akaneiro.Server -c Release

The first start builds the server, which takes a moment. When it is ready, it prints:

    Akaneiro server: web service on 127.0.0.1:80, game port 7001 (clients connect to 127.0.0.1), ...
    Game server listening on port 7001
    Now listening on: http://127.0.0.1:80

Leave the window open while you play. **Ctrl+C** stops the server.

Windows may ask whether the server may communicate through the firewall. Playing on one PC works
either way; for a home network, allow it (see below).

Quick check, in a second terminal:

    curl.exe -X POST http://mw.angry-red.com/client/steam/user/authorize

It should answer with something like `{"success":{"token":"...","server":"127.0.0.1:7001"}}`.
If it does, the hosts entry and the web service both work.

## Step 3: play

Start Steam and start Akaneiro from Steam. The login happens automatically: your account on this
server is your Steam account. The server window logs what happens (`Client #1 connected`,
`login: account ...`, ...).

## Save games

Save data lives in the `data` folder inside the server folder:

- `data/accounts/<Steam ID>.json`: one file per Steam account, with all its characters, items,
  karma and progress;
- `data/state.json`: counters for new account and character ids.

The server saves after every change and when a player disconnects. To back up your progress,
copy the `data` folder. Restore or edit the files only while nobody is playing: a connected game
would overwrite your changes with its next save.

## Settings

The settings are in `src/Akaneiro.Server/appsettings.json`, section `Akaneiro`:

| Setting | Default | Meaning |
|---|---|---|
| `HttpPort` | `80` | Port of the web service. The game always uses 80; change it only for a second test instance. |
| `HttpListenAddress` | `127.0.0.1` | Where the web service listens. `127.0.0.1` = this PC only, `0.0.0.0` = the whole network. |
| `GamePort` | `7001` | Port of the game server. |
| `PublicHost` | `127.0.0.1` | The address the game is told to connect to. Must be reachable from every player's PC. |
| `DataDir` | `../../data` | Where save games go. |
| `ClientAssetsDir` | `../../client-source` | Where the client's data tables are (the repository's `client-source` folder). |
| `LogPackets` | `true` | Logs the name of every message between game and server. Set to `false` for a quieter log. |

Relative paths are resolved against `src/Akaneiro.Server` (the folder `dotnet run` runs the server
in). Every setting can also be given on the command line, which is handy for trying things out:

    dotnet run --project src/Akaneiro.Server -c Release -- --Akaneiro:LogPackets=false

### Running a published build (optional)

If you prefer a ready-to-run folder over `dotnet run`:

    dotnet publish src/Akaneiro.Server -c Release -o ../akaneiro-server-build

Then start `Akaneiro.Server.exe` in that folder. A published build resolves relative paths against
the folder it is started from, so give it absolute paths, either in the `appsettings.json` next to
the exe or on the command line:

    Akaneiro.Server.exe --Akaneiro:DataDir=C:\Akaneiro\data --Akaneiro:ClientAssetsDir=C:\Akaneiro\Akaneiro-Server-Custom-Rebuild-master\client-source

## Playing on several PCs in your home network

One PC runs the server; everyone (that PC included) plays with their own Steam account. Each
player gets their own world: you share the server and the save folder, but you do not meet
in the game (see [KNOWN-ISSUES.md](KNOWN-ISSUES.md)).

1. **Find the server PC's address**: run `ipconfig` on it and note the IPv4 address of your
   network adapter, for example `192.168.0.10`. A fixed address (set in your router) avoids
   having to redo the next steps when it changes.
2. **Configure the server** in `appsettings.json` (or on the command line):

       "HttpListenAddress": "0.0.0.0",
       "PublicHost": "192.168.0.10",

3. **Open the firewall** on the server PC. In PowerShell as administrator:

       New-NetFirewallRule -DisplayName "Akaneiro server" -Direction Inbound -Protocol TCP -LocalPort 80,7001 -Action Allow

4. **Hosts file on every other PC**: as in step 1, but with the server's address:

       192.168.0.10 mw.angry-red.com

   The server PC itself can keep `127.0.0.1 mw.angry-red.com`.

### Over the internet?

Possible in principle (forward TCP ports 80 and 7001 in your router to the server PC and set
`PublicHost` to your public address), but **not recommended**. The server does not verify Steam
logins: whoever can reach it can log in as any Steam ID and play or change that account's
characters. Only run it where you trust everyone who can connect.

## Troubleshooting

**"Client assets not found at ..." when the server starts.**
`ClientAssetsDir` points to the wrong place; it has to point to the repository's `client-source`
folder. With the default settings this only happens when the repository is incomplete.

**"Project file does not exist" when starting the server.**
The terminal is not in the server folder. Go there with `cd` as in step 2.

**"Failed to bind to address http://127.0.0.1:80: address already in use".**
Another program uses port 80: often IIS ("World Wide Web Publishing Service"), MAMP/XAMPP's
Apache, or another local web server. Find it with `netstat -ano | findstr :80` (the last column is
the process id, look it up in the Task Manager's Details tab) and stop it while you play.

**The game hangs at the login or says it cannot connect.**
- Is the server running, and does the quick check from step 2 answer with a server address?
  If not, check the hosts entry (no `#` in front of it, saved as `hosts` without a `.txt` ending).
  Some antivirus programs protect the hosts file and undo changes.
- The server log shows `Steam authorize -> <address>:7001` when the game logs in. If that line
  appears but nothing after it, the game cannot reach the game port: check `PublicHost` and, in a
  network, the firewall.

**Something in the game does not work.**
Check [KNOWN-ISSUES.md](KNOWN-ISSUES.md) first. The server log names every request and what the
server did with it; the game's own log is in
`<Steam library>\steamapps\common\Akaneiro Demon Hunters\Akaneiro_Data\output_log.txt`.

## For developers

    dotnet test tests/Akaneiro.Server.Tests

runs the whole game logic test suite in a few seconds; [README.md](README.md) has the details and
how to regenerate the protocol code and level data.
