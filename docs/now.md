# Now

_Updated 2026-11-13._

- **Step**: 2, Observe ([ROADMAP.md](../ROADMAP.md)); its first feature, the relay and the client's redirection, is
  designed with the owner ([docs/relay.md](relay.md), ADR 0011); its second, the protocol and its per-build mapping,
  is designed with the owner on 2026-11-11 ([docs/protocol.md](protocol.md), ADR 0012); its third, the live view of
  each character and the protocol inspector, on 2026-11-12 ([docs/game.md](game.md)). Steps 0 and 1 are done but
  for their measures below, which the owner takes alongside: the owner started step 2 before them, on 2026-11-11.
- **Done**: the rewrite starts on branch `next`, which will replace the C# version on `main`. The Tauri 2 + React 19
  shell, the typed IPC, the packed data's manifest, i18n FR/EN, the Claude setup (`CLAUDE.md`, `.claude/`), CI on
  Ubuntu, release-please running as the project's GitHub App, the release workflow for six targets, and the app's
  signed updates through the `updater-next` channel ([docs/updates.md](updates.md)). **v0.10.0**, the first
  pre-release, is published with installers for the six targets and no game data yet. Decisions:
  [docs/adr/](adr/README.md) 0001 to 0012.
- **Measured** by the owner, 2026-11-11, Windows x64: v0.10.0 installs per user, starts dark and in French, its status
  bar says "D3B 0.10.0" and "No game data", the close button quits, and a second launch brings the running window
  back. Then the first update: the installed version downloaded v0.12.0 at start, the bar offered to restart, and
  Restart started v0.12.0, whose bar names client 3.7.13.17 and says the relay is ready, and a client plays through it.
  On 2026-11-12, an update installed by closing: v0.12.0 downloaded v0.13.0, closing D3B installed it and put
  Ankama's `https:` `ConfigUrl` back in `version`; v0.13.0 then said ready, and a client played, walked, changed maps
  and opened the inventory through it.
- **Not measured**, by the owner:
  - v0.10.0 on macOS, Linux and ARM64: built by CI, run by nobody yet. The owner has none of them and no tester yet; the README says so, and a round starts when a tester comes ([docs/testing.md](testing.md));
  - **step 1, the extractor**, on the installed client (Windows). Measured by the owner on 2026-11-11, Windows x64:
    items 1, 2 (every count as below, in 27.8 s) and 4 pass; on 2026-11-13, `bun run app` on `next` 3454d38, item 5
    passes (the new status bar's "Aucune donnée de jeu" and the format in its hover). Not measured yet: item 3.
    1. `dotnet run --project tools/extractor/src -- --help` lists `--client` and `--output`;
    2. `dotnet run --project tools/extractor/src` prints build 3.7.13.17 and 15,470 map positions, 569 sub-areas, 70
       areas, 63 zaaps, 40 worlds, 161 zooms, 1,250 tiles, 17,623 maps, 46,490 elements, 11,331 vertices, 30,917
       edges, then exits 0 in about 30 s, writing `artifacts/data/3.7.13.17/` and `artifacts/data/client-3.7.13.17.tar.gz`;
    3. a second run writes the same `manifest.json`;
    4. `tar -xzf artifacts/data/client-3.7.13.17.tar.gz -C data`, then `bun run app`: the status bar's right item says
       "Client 3.7.13.17", and its hover names no error;
    5. with `"format": 2` in `data/manifest.json`, the status bar says there is no data and its hover names the format.
  - **step 2, the relay** ([relay.md](relay.md)), on Windows x64 with client 3.7.13.17, from `bun run app` or the
    next release. Never paste a capture or a log line holding a ticket. Measured by the owner on 2026-11-11, Windows
    x64, `bun run app` on `next` 4fa724f: items 1 to 9 and 12 pass; on 2026-11-12, item 10's close (v0.12.0 to
    v0.13.0); on 2026-11-13, `bun run app` on `next` 3454d38, items 1, 2, 4, 7, 8 and 9 pass again in the new
    shell's status bar (the relay's item, its red once off, its hover). Not measured yet: item 10's Restart (v0.13.0
    installed, then the release after it) and 11 (needs a Windows account whose name holds a space or an accent).
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
  - **step 2, the protocol** ([protocol.md](protocol.md)), on Windows x64 with client 3.7.13.17, from `bun run app` on
    `next` or the next release. Never paste a capture. Measured by the owner on 2026-11-12, Windows x64, `bun run app`
    on `next` b200248: items 1 to 6 pass; its two captures replayed through the relay byte for byte and through the
    protocol with no frame not trusted; on 2026-11-13, `bun run app` on `next` d978253, items 1 and 3's counts in the
    inspector and its items 8 and 9 pass. Not measured yet: item 7, at the next client patch.
    Since the new shell (the owner, 2026-11-13), the relay's hover no longer lists the connections: items 1, 2, 4, 5
    and 7 read the Characters view and a character's tab; the counts of items 1 and 3 read the inspector: the panel's
    "Protocol" view under the character's tab (Ctrl+J shows the panel), its counts on its header's line.
    1. With 3.7.13.17's data in `data/` (the extractor's item 4), start D3B, then a client; enter the game and open the
       character from the Characters view: its tab names the coordinates the game shows for its map, and the panel's
       Protocol header says "non fiables 0" and "perdus 0";
    2. walk to the next map: within about a second the tab names the new coordinates;
    3. play five minutes (walk, open the inventory, harvest, a fight if one is at hand): "compris" and "lus" grow,
       "non fiables" and "perdus" stay at 0;
    4. play a second account at once: two connections, each with its own map;
    5. close one client's window: its character leaves the Characters view, the other stays;
    6. with `D3B_RECORD` set to an absolute folder, play a minute: the capture is named `3.7.13.17-<ms>-<n>.d3bcap`;
    7. after the next client patch, before its pins: the Characters view says "Protocole non lu", the character's tab
       that its build has no pins, the Protocol panel "Aucune trame gardée." without counts, and the game plays as before;
    8. the inspector, while playing: its frames scroll in, newest last; scroll up: the list stands still while frames
       come; click a `MapCurrentEvent` row: the detail on its right reads `map_id` and no message name; click a dimmer
       row (a message not pinned): its fields by number, a length for anything else; type "Map" in the filter: only
       names holding it; choose "↑ Client": only the client's; press the pause: the list stops and says how many
       arrived, press it again: they are listed; switch to a second character's tab: its own frames and counts;
    9. click a "secret" row: the detail says its content is never kept.
  - **the live view's life** (the owner's report of 2026-11-13, fixed): after a fight, stand still out of combat: the
    character's tab says "Vie — en régénération" while the bar fills, and no longer once it is full, before any sit
    or fight.
- **Done in code, step 1**: `tools/extractor` reads the client's bundles, data tables, texts in five languages and flag
  enums, and writes the map positions, sub-areas, areas, zaaps, worlds and their tiles, maps, and the world graph,
  checked against one another, with the `client-<build>.tar.gz` that `data:fetch` reads; the app reads it all at start
  but the maps, which it reads one file at a time.
- **Debt**, from the review of step 1: the readers of maps, of the world graph and of the tiles are tested on the
  installed client only (skipped in CI); hand-built trees (`TypeTree.Build`) would test their failures in CI too.
- **Published**: `client-3.7.13.17.tar.gz`, from `next` f3104e9 (SHA-256 `74cfd3c3…`), added to the C# version's
  `client-3.7.13.17` release on the owner's go; `.github/game-data.json` names it, so the next release packs it.
  v0.11.0 shipped without data, named before. **v0.12.0**, published on the owner's go on 2026-11-11, packs that data
  and the relay, with installers for the six targets and its `latest.json` on `updater-next`; **v0.13.0**, on
  2026-11-11, adds the game connection split into frames.
- **Done in code, step 2's relay**: the redirect, the connection phase and its one rewrite, the game connection, the
  replay of the owner's 225 captures (171 connection phases, 161 handed over, 54 game connections, no failure), the
  status-bar indicator and the restore before an update.
- **Next**: the owner measures the relay's item 10's Restart with the next release, and item 11 when an account is at
  hand; the protocol's item 7 at the next client patch, with its pins (and, before it, `GameAssembly.dll`,
  `global-metadata.dat` and `version` copied into `artifacts/` for Q17). Step 2's next feature is designed below. The
  protocol's code was a stack of seven PRs against `next`, all merged (#155 to #171): (1) the game connection split into
  frames both ways, with the tap (done in code, in v0.13.0); (2) `relay/capture/` and `D3B_RECORD` (done in code); (3)
  the connection's build from path 1.3.5, carried by the handover (done in code); (4) the extractor's protocol schema
  from the metadata (done in code); (5) the `protocol` domain: prost, our `.proto` files, the envelope, the four
  outcomes, the pins of 3.7.13.17 (done in code); (6) the pins' audit and the capture replay (done in code: 3.7.13.17's
  pins hold, no frame not trusted); (7) the protocol task per connection, the gate, the status and its tooltip (done in
  code). Its in-game checklist is under "Not measured".
- **Next, the live view and the inspector** ([game.md](game.md), [protocol.md](protocol.md#the-inspector)), the
  owner's choices of 2026-11-12: ids for monsters, NPCs and resources (their names come later), the map as diamonds,
  the characters on the left with a View and a Protocol tab, text of unread frames never shown, 3,000 frames kept per
  connection. A stack of PRs against `next`: (1) the protocol: the `.proto` files extended (actors, the
  characteristics' values), the level gained and the kamas pinned, `koh` a secret (done in code: the 17 pins hold, no
  frame not trusted); (2) `game`, in two PRs: the characteristics and life with its regeneration, then who, where,
  pods, kamas, level, context, staleness (done in code); (3) `game`, in two PRs: what is around, then the game's
  capture replay (done in code: lives within a point at all 634 fights, the character on all 66,166 maps, 99.35% of
  its walks from its known cell, 67,713 of 71,882 stated updates on a known element); (4) in two PRs: the session's observer,
  then the view through the IPC, `CharacterChanged` (done in code); (5) the characters list and the view (done in code); (6) the map and `map_cells` (done in code); (7) the inspector's frames
  kept and streamed (done in code); (8) the inspector, in the panel's Protocol view rather than a tab (the owner, 2026-11-13, from mock-ups), in two PRs: its frames listed under the connection's counts (done in code, #197), then a frame's detail, the filters and the pause (done in code); its in-game checklist, the protocol's items 1, 3, 8 and 9, measured by the owner on 2026-11-13; item 7 waits for the next client patch.
- **The app's UI, rebuilt** (the owner, 2026-11-12): the interface on `next` (the characters list and view, the map's
  diamonds, the relay's indicator, the update bar, the status bar) is removed; the stores, the updater, i18n and the
  IPC stay. The new shell ([docs/shell.md](shell.md)) lands in PRs: dark only (#186), its parts in VS Code's layout as
  discreet islands (Dark Modern, Inter, JetBrains' icons since replaced by Lucide's (the owner, 2026-11-13), parts that slide open and closed; #187), then its title bar
  with the command palette (#188); then the features one at a time, each decided with the owner: the Characters
  view (done in code, #189), a character's tab (layout "A", done in code, #190), the character's map in the tab
  (the owner's style "B": discreet diamonds, actors as dots; done in code, #191). The status bar, the owner's layout "A" (2026-11-13): the
  app's version and the game data's build (done in code, #193), the relay's item and its hover (done in code, #194), the
  update offer (done in code, #195). A renderer of the actors' sprites
  was studied, then dropped by the owner on 2026-11-13: the client's sprites would take too much room. The relay's
  items 1, 2, 4, 7, 8 and 9 were measured again in it by the owner on 2026-11-13; the protocol's items that read the
  inspector, on the same day. The bot's model decided for step 3 (blueprints, zones, routes, settings) holds.
- **Waiting on testers**: Dofus 3 has a native client on macOS and on Linux (the owner, 2026-11-11); where it is
  installed, whether its data is Windows', and the relay on each are measured by the first testers there.
