# Now

_Updated 2026-11-11._

- **Step**: 2, Observe ([ROADMAP.md](../ROADMAP.md)); its first feature, the relay and the client's redirection, is
  designed with the owner ([docs/relay.md](relay.md), ADR 0011). Steps 0 and 1 are done but for their measures below,
  which the owner takes alongside: the owner started step 2 before them, on 2026-11-11.
- **Done**: the rewrite starts on branch `next`, which will replace the C# version on `main`. The Tauri 2 + React 19
  shell, the typed IPC, the packed data's manifest, i18n FR/EN, the Claude setup (`CLAUDE.md`, `.claude/`), CI on
  Ubuntu, release-please running as the project's GitHub App, the release workflow for six targets, and the app's
  signed updates through the `updater-next` channel ([docs/updates.md](updates.md)). **v0.10.0**, the first
  pre-release, is published with installers for the six targets and no game data yet. Decisions:
  [docs/adr/](adr/README.md) 0001 to 0011.
- **Measured** by the owner, 2026-11-11, Windows x64: v0.10.0 installs per user, starts dark and in French, its status
  bar says "D3B 0.10.0" and "No game data", the close button quits, and a second launch brings the running window
  back.
- **Not measured**, by the owner:
  - v0.10.0 on macOS, Linux and ARM64: built by CI, run by nobody yet. The owner has none of them and no tester yet; the README says so, and a round starts when a tester comes ([docs/testing.md](testing.md));
  - the first update: with v0.10.0 installed, the next release is downloaded at start, the bar offers to restart,
    Restart installs it; on another try, closing installs it. It starts with a new identifier (ADR 0009), so its data
    folders start empty;
  - **step 1, the extractor**, on the installed client (Windows). Measured by the owner on 2026-11-11, Windows x64:
    items 1, 2 (every count as below, in 27.8 s) and 4 pass. Not measured yet: items 3 and 5.
    1. `dotnet run --project tools/extractor/src -- --help` lists `--client` and `--output`;
    2. `dotnet run --project tools/extractor/src` prints build 3.7.13.17 and 15,470 map positions, 569 sub-areas, 70
       areas, 63 zaaps, 40 worlds, 161 zooms, 1,250 tiles, 17,623 maps, 46,490 elements, 11,331 vertices, 30,917
       edges, then exits 0 in about 30 s, writing `artifacts/data/3.7.13.17/` and `artifacts/data/client-3.7.13.17.tar.gz`;
    3. a second run writes the same `manifest.json`;
    4. `tar -xzf artifacts/data/client-3.7.13.17.tar.gz -C data`, then `bun run app`: the status bar names 3.7.13.17,
       with no error in its tooltip;
    5. with `"format": 2` in `data/manifest.json`, the status bar says there is no data and its tooltip names the format.
  - **step 2, the relay** ([relay.md](relay.md)), on Windows x64 with client 3.7.13.17, from `bun run app` or the
    next release. Never paste a capture or a log line holding a ticket.
    1. Close every client, start D3B: the bar says "Relay ready · 0 in game", its tooltip names client 3.7.13.17;
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
    8. start a client, then D3B: the client plays, the bar says 0 in game, the tooltip says to start it after;
    9. take port 5555 first (`[Net.Sockets.TcpListener]::new([ipaddress]'127.0.13.59',5555).Start()` in a
       PowerShell kept open), then start D3B: the bar says "Relay off", the tooltip names port 5555, `version` is
       untouched;
    10. with an update downloaded (from the next release on), close D3B: once it installed, `version` names
        Ankama's URL; on another one, choose Restart: the new D3B says ready and a client plays;
    11. if a Windows account with a space or an accent in its name is at hand: steps 1 and 2 there;
    12. after a disconnection in game (Wi-Fi off and on), the client reconnects through D3B.
- **Done in code, step 1**: `tools/extractor` reads the client's bundles, data tables, texts in five languages and flag
  enums, and writes the map positions, sub-areas, areas, zaaps, worlds and their tiles, maps, and the world graph,
  checked against one another, with the `client-<build>.tar.gz` that `data:fetch` reads; the app reads it all at start
  but the maps, which it reads one file at a time.
- **Debt**, from the review of step 1: the readers of maps, of the world graph and of the tiles are tested on the
  installed client only (skipped in CI); hand-built trees (`TypeTree.Build`) would test their failures in CI too.
- **Published**: `client-3.7.13.17.tar.gz`, from `next` f3104e9 (SHA-256 `74cfd3c3…`), added to the C# version's
  `client-3.7.13.17` release on the owner's go; `.github/game-data.json` names it, so the next release packs it.
  v0.11.0 shipped without data, named before.
- **Done in code, step 2's relay**: the redirect, the connection phase and its one rewrite, the game connection, the
  replay of the owner's 225 captures (171 connection phases, 161 handed over, 54 game connections, no failure), the
  status-bar indicator and the restore before an update.
- **Next**: the owner removes Repository admin from the "Protected branches" ruleset's bypass list once CI is back;
  the owner measures the relay in game from its checklist above, then the protocol and its per-build mapping.
- **Waiting on testers**: Dofus 3 has a native client on macOS and on Linux (the owner, 2026-11-11); where it is
  installed, whether its data is Windows', and the relay on each are measured by the first testers there.
