# Now

_Updated 2026-11-14._

- **Step**: 2, Observe ([ROADMAP.md](../ROADMAP.md)); its first feature, the relay and the client's redirection, is
  designed with the owner ([docs/relay.md](relay.md), ADR 0011); its second, the protocol and its per-build mapping,
  is designed with the owner on 2026-11-11 ([docs/protocol.md](protocol.md), ADR 0012); its third, the live view of
  each character and the protocol inspector, on 2026-11-12 ([docs/game.md](game.md)). Steps 0 and 1 are done but
  for their measures below, which the owner takes alongside: the owner started step 2 before them, on 2026-11-11.
- **Step 3 designed ahead**: what is left of step 2 waits on outside events (the next release for the in-app Restart,
  the next client patch for the protocol's item 7, a Windows account whose name holds a space or an accent for the
  relay's item 11, testers on macOS and Linux) or on the owner's measures taken alongside (the breed, the life's fix),
  so the owner granted on 2026-11-13 an exception to "the next feature does not start": step 3, the bot's
  model, was designed, from mock-ups then a prototype in the real shell (local branch `prototype/step3-model`, never
  merged), and written in [docs/bot.md](bot.md) (ADRs 0014 and 0015). On 2026-11-13 the owner granted a second
  exception: step 2 is measured on Windows but for items that wait on those outside events, so step 3's code starts
  now, its stack below.
- **Done**: the rewrite starts on branch `next`, which will replace the C# version on `main`. The Tauri 2 + React 19
  shell, the typed IPC, the packed data's manifest, i18n FR/EN, the Claude setup (`CLAUDE.md`, `.claude/`), CI on
  Ubuntu, release-please running as the project's GitHub App, the release workflow for six targets, and the app's
  signed updates through the `updater-next` channel ([docs/updates.md](updates.md)). **v0.10.0**, the first
  pre-release, is published with installers for the six targets and no game data yet. Decisions:
  [docs/adr/](adr/README.md) 0001 to 0015.
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
    passes (the new status bar's "Aucune donnée de jeu" and the format in its hover); on 2026-11-13, `next` 506b1e7,
    item 3 passes (a second run writes the same `manifest.json`, every count as in item 2): step 1 is measured on
    Windows x64.
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
  - **the breed in a character's tab** (the owner, 2026-11-13: its name in the player's language and its head, 40 px in
    a rounded square in place of the initials, chosen from mock-ups; then the same in the Characters list), a stack
    against `next`: (1) the data's format 2, `breeds.json` and the 38 heads, named in a second asset of the build's
    release (done in code); (2) the character's sex, read from its look (done in code: the pins' audit holds, and the owner's 54 game connections
    replay with no frame not trusted); (3) the tab's breed name and head (done in code); (4) the
    Characters list's head (done in code). Measured by the owner on 2026-11-13, v0.14.0 installed: items 3 and 4
    pass. Not measured yet: items 1 and 2 (the owner's own extraction and `data:fetch`), 5 at the next client patch.
    On Windows x64:
    1. on `next` at 25c5c4c or before (format 2; since the items' PR the extractor writes format 3),
       `dotnet run --project tools/extractor/src` also prints "Breeds: 19 breeds, 38 heads of 64 px" and writes
       `artifacts/data/client-3.7.13.17-format2.tar.gz`; then `tar -xzf artifacts/data/client-3.7.13.17-format2.tar.gz
       -C data` (after emptying `data/` but its README): a `data/` still in format 1 makes `bun run app` say there is
       no game data and fails the Rust test that reads `data/`. On a later `next`, the same with format 3's archive,
       `client-3.7.13.17-format3.tar.gz`, whose run also prints the breeds' line;
    2. on the same commit, `bun run data:fetch 3.7.13.17` fills `data/` with format 2 (published on 2026-11-13, from
       `next` 2b0577c), and `bun run app`'s status bar says "Client 3.7.13.17"; on a later `next`, once format 3 is
       published, the same with format 3;
    3. with that data, start D3B, then a client, and open a character from the Characters view: its tab shows its
       breed's head, 40 px in a rounded square, in place of its initials, the head of its sex, and "Niveau <n> ·
       <breed> · <state>" with the breed's French name (in English after switching the language: "Level <n> ·
       <breed>", Crâ becoming Cra); a character of each sex if one is at hand;
    4. the Characters view shows the same head, 28 px, in place of the initials, on each character's row;
    5. at the next client patch, before its data: the tab and the character's row show the initials, and the tab
       "Niveau <n>" alone.
  - **the live view's life** (the owner's report of 2026-11-13, fixed): after a fight, stand still out of combat: the
    character's tab says "Vie — en régénération" while the bar fills, and no longer once it is full, before any sit
    or fight. Measured by the owner on 2026-11-13, v0.14.0 installed: passes.
- **step 3, the characters played** (PRs #226 and its view), on Windows x64 from `bun run app` on `next` or the next
  release; on macOS and Linux, Forget's trash by testers:
    1. play a character, close its client: its row stays under a line, greyed, "Vu à l'instant", then "Vu il y a 1 min"
       a minute later; its file is in `Documents/D3B/characters/<server>-<id>.json`;
    2. restart D3B: the row is still there; click it: its tab says "Hors ligne" and "Vu pour la dernière fois aujourd'hui
       à <heure>, à <sous-zone> (x, y)", without a map or gauges;
    3. with that tab open, start the character's client again: its row goes back above the line, and the tab becomes
       its live tab (map, gauges);
    4. play a character, change map, wait two minutes, quit D3B while playing: its tab, once D3B is back, says the
       time and place of within a minute before quitting;
    5. right-click an offline row, Forget: the row goes, the file is in the system's Recycle Bin and can be restored;
       Ctrl+Shift+P, "Oublier <nom>" does the same;
    6. make a character's file unreadable by hand (a stray letter): the row is gone and D3B's log names the file kept.
- **step 3, the Library**, on Windows x64 from `bun run app` on `next` or the next release:
    1. the activity bar has "Bibliothèque" under "Personnages"; it lists "Donnés par D3B": Récolte automatique, Récolte
       d'un métier, Chasse (in English after switching the language), and "Les miens", empty at first;
    2. click Récolte automatique: its tab says "Donné par D3B · v1 · verrouillé", its description, and its settings:
       Zone à choisir, Banque à 90 %, Banque la plus proche, Garder rien;
    3. Dupliquer (the tab's button or a right-click): "Récolte automatique (copie)" under "Les miens", its tab says
       "À moi · basé sur Récolte automatique v1", `Documents/D3B/blueprints/recolte-automatique-copie.json` exists,
       and it is still listed after a restart;
    4. a blueprint's tab shows "Mon blueprint" on the left (Graphe d'événements, the variables with an eye on Zone, Banque
       à, Banque, Garder) and the graph: Au lancement → Aller dans [Zone] (étape 4) → Récolter [Tout] dans [Zone]
       jusqu'à [Banque à] de pods (étape 5) → Aller à [Banque] → Déposer à la banque, garder [Garder], then back; Vie
       sous [40 %] → Attendre [90 %] de vie; no node over another; pan and zoom; nothing can be moved or wired;
    5. click Vie sous 40 %: Details on the right says its sentence, its inputs (Seuil 40 %, Réarmement 60 %), priority 1,
       and "Interrompre, puis reprendre" marked among the four modes, each drawn; click Récolter: "Le bot le fera à
       partir de l'étape 5" and what an unlinked Échec does; click Banque à in Mon blueprint: Pourcentage, 90 %, de 10
       à 100, set per character, 1 nœud; click the empty graph: "Choisis un nœud ou une variable.";
    5b. with the blueprint's tab active, "Graphe | Résumé" sits at the right of the tabs; Résumé shows "Au lancement"
       with steps 1 to 4 (Aller dans [Zone] étape 4 … Déposer à la banque, garder [Garder]) and "Revenir à 1.", then
       "Vie sous [40 %] · priorité 1 · Interrompre, puis reprendre" with Attendre [90 %] de vie, then the three
       defaults (Un combat commence, Le personnage est mort, Une action échoue · par défaut); click step 2: Details
       shows Récolter; Graphe brings the graph back; a character's tab shows no toggle;
    6. put a broken file in `Documents/D3B/blueprints` (a stray letter), then show the Library again (Personnages,
       then Bibliothèque): "Ignorés 1" appears folded at the bottom; open it: the file, and why in its hover.
- **Done in code, step 1**: `tools/extractor` reads the client's bundles, data tables, texts in five languages and flag
  enums, and writes the map positions, sub-areas, areas, zaaps, worlds and their tiles, maps, and the world graph,
  checked against one another, with the `client-<build>-format<n>.tar.gz` that `data:fetch` reads (ADR 0013); the app reads it all at start
  but the maps, which it reads one file at a time. The readers of maps, of the world graph and of the tiles have their
  checks tested in CI on objects built by hand (`TestMap`, `TestGraph`, `TestTiles`), the debt of step 1's review; only
  the checks of a bundle itself (one container, unique identifiers, its stream files) need the installed client.
- **Published**: `client-3.7.13.17.tar.gz`, from `next` f3104e9 (SHA-256 `74cfd3c3…`), added to the C# version's
  `client-3.7.13.17` release on the owner's go. v0.11.0 shipped without data, named before. Format 2 (the breeds):
  `client-3.7.13.17-format2.tar.gz`, from `next` 2b0577c (SHA-256 `2e3ac196…`, 84,146,484 bytes), added to the same
  release on the owner's go on 2026-11-13; `.github/game-data.json` named it until format 3 (step 3's (3b)), which
  a release needs published first. **v0.12.0**, published on the owner's go on 2026-11-11, packs that data
  and the relay, with installers for the six targets and its `latest.json` on `updater-next`; **v0.13.0**, on
  2026-11-11, adds the game connection split into frames; **v0.14.0**, on the owner's go on 2026-11-13, packs the
  data's format 2 and the new shell (status bar, inspector, Lucide icons, the breed's head), its six targets carrying
  `THIRD-PARTY-NOTICES.txt` (#205, #213, #214) and its `latest.json` on `updater-next`.
- **Done in code, step 2's relay**: the redirect, the connection phase and its one rewrite, the game connection, the
  replay of the owner's 225 captures (171 connection phases, 161 handed over, 54 game connections, no failure), the
  status-bar indicator and the restore before an update.
- **Next**: the owner measures the relay's item 10's status-bar offer with the release after v0.14.0, and item 11 when
  an account is at hand; the protocol's item 7 at the next client patch, with its pins (`GameAssembly.dll`,
  `global-metadata.dat` and `version` of 3.7.13.17 copied into `artifacts/` for Q17 on 2026-11-13). Step 2's next feature is designed below. The
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
  update offer (done in code, #195). The packages carry the font's, Lucide's and every bundled package's and
  crate's licences as `THIRD-PARTY-NOTICES.txt` (the owner, 2026-11-13; done in code,
  [updates.md](updates.md#third-party-notices)), so the font's licence no longer holds the next release back; the AppImage also carries the
  licences and sources of the 130 Ubuntu packages it packs (GTK, WebKitGTK and theirs) as
  `SYSTEM-LIBRARIES-NOTICES.txt`, read from a first build (the owner, 2026-11-13; done in code). Its own runtime
  (type2-runtime, with libfuse linked in, LGPL-2.1), the libraries that runtime links, and AppRun close that file,
  read from the same build (the owner, 2026-11-13; done in code), so nothing holds the next release back. A renderer of the actors' sprites
  was studied, then dropped by the owner on 2026-11-13: the client's sprites would take too much room. The relay's
  items 1, 2, 4, 7, 8 and 9 were measured again in it by the owner on 2026-11-13; the protocol's items that read the
  inspector, on the same day. Step 3's model is in [docs/bot.md](bot.md) (below).
- **Next, step 3** ([bot.md](bot.md)), started on 2026-11-13 (the owner's exception above): a stack of PRs against
  `next`, one behaviour each, measured as they show something. First two studies, done on 2026-11-13 from the owner's
  captures (`artifacts/studies/character-key.md`, `death.md`): a character's id carries its server in its low 16
  bits (verified on two servers), so a character's file is `<server>-<id>.json`, no new pin; a death is the fight
  end's result for the character lost, which needs one new pin (`GameFightEnd`'s results), life back at half and
  energy down by ten times the level (verified on 21 lost fights). Then (1) `bot`: the blueprint, its JSON
  and its validation (done in code: its file's shape in [bot.md](bot.md#storage), the layout under its own key, a
  variable both set per character and memory allowed, as Unreal, the owner's choices of 2026-11-13); (2) the catalogue of
  nodes, in two PRs: its 24 kinds and the problems of their pins and wires (done in code), then their sentences,
  descriptions and pins' labels in French and English, `node.kinds` and `node.pins` (done in code), then "sit until"
  replaced by "wait for life" (the owner, 2026-11-14: sitting no longer gives life back faster; done in code); (3)
  `storage`: `Documents/D3B`, atomic writes, a newer schema refused (done in code: the blueprints' folder, a file whose
  name is not its id kept and reported, the owner's choice of 2026-11-14); (3b) the extractor: the items, the
  harvesting professions and their resources, a new data format, so that D3B's blueprints name each profession's
  resources (the owner, 2026-11-14, from its study): format 3's `items.json`, `item-types.json`, `jobs.json` and
  `skills.json`, an item of a missing type and a skill gathering a missing item left out and counted (done in code:
  the installed 3.7.13.17 gives 21,621 items, 236 types, 23 jobs, 372 skills, one item and one skill left out), then the items'
  and jobs' icons at 32 px, still format 3 (done in code: 11,768 items' and 21 jobs' icons, 19.7 MB, four items naming
  an icon the client lacks counted, an archive of 105 MB in 30 s, the same bytes on two runs); then the owner's extraction and publishing of `client-3.7.13.17-format3.tar.gz`:
  until then `.github/game-data.json` names format 3, so `data:fetch` and a release find no data; (4) D3B's blueprints in the app, and
  Duplicate, in two PRs: automatic harvest, profession harvest (its profession a setting, through a new "resources of"
  node and a profession value: the owner, 2026-11-14) and hunt, each without problems, their texts in the
  translations (done in code), then the IPC lists them with mine and the files kept, and Duplicate makes
  "<name> (copie)" in `Documents/D3B` (the owner, 2026-11-14; done in code); (5) the Characters view keeps the characters played, greyed when gone, in two PRs (the owner's choices of
  2026-11-14 from mock-up 8: the offline ones under the connected ones, the most recently seen first, "Seen 2 h ago";
  Forget by right-click, the file to the system's trash through the `trash` crate; an offline character's tab says
  when and where it was last seen, without a map or gauges): its file `characters/<server>-<id>.json`, remembered as
  it is seen and when its connection closes, the IPC's list and Forget (done in code), then the view and the tab (done in code); (6) the Library (the owner's mock-up 9 of 2026-11-14; done in code); (7) the blueprint
  editor read-only: My Blueprint and the graph, in two PRs: a blueprint's graphs through the IPC, each node with its
  family, its step, its pins and its place (done in code), then the graph drawn: its nodes, their sentences and look (an "étape N" tag on what
  does not run yet; the types' colours, Profession, Route and Text added: the owner, 2026-11-14; done in code), then
  My Blueprint and the tab (done in code); (8) its Details and Summary, in three PRs: what Details needs through the IPC (the problems where they
  are, an event's settings, a function's signature, a variable's bounds; done in code), then Details in a column on the
  tab's right (the owner, 2026-11-14; done in code), then the Summary, toggled in the tab strip's editor actions (the
  owner, 2026-11-14; done in code); (9) the World view and tab from the tiles; (9b) the extractor: the client's hints
  (`World.HintsDataRoot`, 835 on 3.7.13.17 in 6 categories, measured by the C# version) and their pictures, a new data
  format (ADR 0013); (9c) the hints on the World's map, one switch per kind, which also give the nearest bank;
  (10) sub-areas as zones, a Zone chosen in a list or on the map; (11) a character's blueprint and settings in its tab;
  (12) links checked by type, and the picker; (13) delete, undo, autosave; (14) variables; (15) functions opened in
  place; (16) events: priority, re-arming, modes, the defaults; (17) comments and reroutes; (18) Align, copy and paste,
  search; (19) editing by keyboard; (20) my zones painted; (21) routes; (22) the `.d3b` file; (23) a given blueprint's
  update; (24) the "Advanced tools" setting; (25) the runtime: the flow, outcomes, variables, blocked, finished, on the
  captures; (26) events at run time: priorities, modes, thresholds, settings at safe points. Step 4 then brings Play,
  what a character does now, its History and the debugger, with the first actions.
- **Waiting on testers**: Dofus 3 has a native client on macOS and on Linux (the owner, 2026-11-11); where it is
  installed, whether its data is Windows', and the relay on each are measured by the first testers there.
