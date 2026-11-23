# Step 2, the relay

Status: waiting

**step 2, the relay** ([relay.md](../relay.md)), on Windows x64 with client 3.7.13.17 (or 3.7.14.18, the bar then naming it), from `bun run app` or the
next release. Never paste a capture or a log line holding a ticket. Measured by the owner on 2026-11-11, Windows
x64, `bun run app` on `next` 4fa724f: items 1 to 9 and 12 pass; on 2026-11-12, item 10's close (v0.12.0 to
v0.13.0); on 2026-11-13, `bun run app` on `next` 3454d38, items 1, 2, 4, 7, 8 and 9 pass again in the new
shell's status bar (the relay's item, its red once off, its hover); on 2026-11-13, v0.13.0 installed, item 10's
Restart from its update bar into v0.14.0 (the new D3B ready, "Client 3.7.13.17", a client plays). Not measured
yet: item 10's status-bar offer (its hover and "Installation de <version>…", from v0.14.0 to the release after it)
and 11 (needs a Windows account whose name holds a space or an accent).

1. Close every client, start D3B: the bar says "Relais prêt · 0 en jeu", then on the right "Client 3.7.13.17";
   its hover says to start Dofus after D3B, and names the client only if its build is not the data's;
2. start a client from the launcher, log in, choose a server and a character, enter the game: it plays as before,
   and the bar says 1 in game; walk to another map and open the inventory;
3. set the launcher's connection port to 443 and do step 2 again: it plays;
4. play a second account at once: the bar says 2 in game;
5. with D3B running, repair Dofus from the launcher: within seconds a new client goes through D3B again;
6. quit D3B: the relayed clients lose their connection (expected); the `ConfigUrl` of
   `%LOCALAPPDATA%\Ankama\Dofus-dofus3\Dofus_Data\StreamingAssets\version` is Ankama's `https:` URL again, and a
   client started now plays without D3B;
7. kill D3B from the Task Manager, start a client: it cannot connect (expected); start D3B: the bar says ready
   and a new client plays;
8. start a client, then D3B: the client plays, the bar says 0 in game, the relay's hover says to start it after;
9. take port 5555 first (`[Net.Sockets.TcpListener]::new([ipaddress]'127.0.13.59',5555).Start()` in a
   PowerShell kept open), then start D3B: the bar says "Relais arrêté" in red, its hover names port 5555, `version` is
   untouched;
10. with an update downloaded (from the next release on), close D3B: once it installed, `version` names
    Ankama's URL; on another one, with a client in game, hover "Redémarrer pour passer à <version>" on the status
    bar's right: its hover says restarting cuts the game connections (none without a client in game); choose it:
    it says "Installation de <version>…", then the new D3B says ready and a client plays;
11. if a Windows account with a space or an accent in its name is at hand: steps 1 and 2 there;
12. after a disconnection in game (Wi-Fi off and on), the client reconnects through D3B.
