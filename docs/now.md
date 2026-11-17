# Now

_Updated 2026-11-17._

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
  [docs/adr/](adr/README.md) 0001 to 0017.
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
    "Protocol" view under the character's tab (Ctrl+J shows the panel), its counts on its header's line. Since step 3's
    PR 24, items 1, 3, 7, 8 and 9 begin by turning on Advanced tools: Affichage > Outils avancés (or "Afficher ou
    masquer les outils avancés" in the palette), which the next starts remember.
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
       no game data and fails the Rust test that reads `data/`. On a later `next`, the same with format 4's archive,
       `client-3.7.13.17-format4.tar.gz`, whose run also prints the breeds' and the hints' lines;
    2. on the same commit, `bun run data:fetch 3.7.13.17` fills `data/` with format 2 (published on 2026-11-13, from
       `next` 2b0577c), and `bun run app`'s status bar says "Client 3.7.13.17"; on `next` at 55e4be8 or later, the
       same with format 4 (published on 2026-11-16);
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
- **step 3, the World**, on Windows x64 from `bun run app` on `next` or the next release. Measured by the owner on
  2026-11-17: items 1 to 11 passed; item 8 has changed since at the owner's request (every kind ticked at first) and
  is to be measured again:
    1. the activity bar has "Monde" under "Bibliothèque"; its view lists "Sous-zones du jeu" by area, folded, each
       area with its count; a click on Astrub unfolds it, its sub-areas by level, then by name;
    2. type "foret" in the search: the areas holding a matching sub-area unfold, the others go; a nonsense word says
       "Aucune sous-zone ne correspond.";
    3. click Forêt d'Astrub: the World tab opens on "Monde des Douze" (its editor action), the forest in translucent
       teal with a sharp outline, fitted in view, "Forêt d'Astrub" then "Astrub · niv. 20 · N cartes" on top left;
    4. the map is sharp at every zoom from 20 % to 100 % (the wheel, + and − on the focused map, the buttons); dragging
       moves it; hovering a map says its sub-area and coordinates; 0 or the fit button frames the forest again;
    5. choose Incarnam in the editor action: its map shows; click an Incarnam sub-area in the view: it shows there;
    6. a sub-area no world shows (a dungeon) is greyed with a crossed pin, its hover saying "Pas sur la carte"; clicking
       it says so on top of the map;
    7. (with format 4's data, `bun run data:fetch 3.7.13.17`) on the Twelve's map, the hints' pictures stand on their maps, the same size at every
       zoom, more of them as it zooms in, none overlapping, and dragging never changes which are shown; hovering a zaap
       says "Zaap" and its coordinates, a dungeon also its level;
    8. "Indices" beside the worlds lists the kinds the world holds, each with its picture and count, all ticked,
       "Autres lieux" and "Passages" too (the owner, 2026-11-17), their pictures on the map; unticking Zaaps takes them off the map with the menu still open; "Tout cacher"
       and "Tout montrer" do what they say; Incarnam's list has only its own kinds;
    9. with nothing chosen, Details on the right of the World tab says "Choisis une sous-zone dans la vue ou sur la
       carte."; choose Forêt d'Astrub in the view: Details says "Forêt d'Astrub", "Sous-zone du jeu · Astrub", Niveau
       20, Cartes 68, Monstres 8 sortes, then Ressources: Bûcheron, ten woods from Bois de Frêne 1 to Bois d'Orme 180,
       each with its icon, and Alchimiste, five plants from Ortie 1 to Orchidée Freyesque 80;
    10. click a map of another sub-area: it turns teal, the forest no longer, the card and Details follow it, and the map
       stays where it was; dragging the map chooses nothing; a click on the sea changes nothing; a dungeon chosen in the
       view says "Pas sur la carte" beside Cartes;
    11. (the owner's report of 2026-11-16, fixed) choose Route des âmes in Incarnam: only its row of 7 maps turns teal,
       and hovering 0, 0 no longer says Dofus Games, which is greyed "Pas sur la carte" with the Taverne and Beta; a
       dungeon of the Twelve (Donjon des Bworks) is greyed too, and no longer frames Village d'Amakna; "Indices" and the
       worlds' menu are the same height.
    12. (the owner's report of 2026-11-17, fixed) zoom in and out with the wheel from 20 % to 100 % and back, quickly,
       then drag the map far at 100 %: no part of it ever shows black, even for an instant; a part not loaded yet shows
       blurred, then sharp.
- **step 3, my zones painted** ((20)), on Windows x64 from `bun run app` on `next` or the next release, with format 4's
  data:
  Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: item 1 passes, and 2 to 5 but for
  what was not tried (Space with the main button, V then a click, F2, Renommer, a blank name or Escape, the file's
  form); a zone of mine is listed first in a character's Zone field. Not measured yet: choosing it there (item 6), its
  deletion (7) and a file kept (8).
    1. the World view says "Mes zones" above the sub-areas, "Aucune zone : « + » en peint une."; click "+": "Nouvelle
       zone" appears with 0 carte, the World tab opens on the world it showed, the brush pressed in the toolbar on top,
       "Nouvelle zone" and "Zone à moi · 0 carte" on top left, Details with its name and "Aucune : peins ses cartes.";
    2. press and drag over Forêt d'Astrub: each map crossed turns amber as the pointer goes, the sea stays as it was;
       release: the row says N cartes; the right button and Space with the main one move the map without painting;
    3. E, then drag over painted maps: they go; S: hovering outlines a whole sub-area; click Champs d'Astrub: all its
       maps turn amber; click it again: they go; V, then click a sub-area: it is chosen, the zone and toolbar go;
    4. type a name in Details' field, Enter: the row and the top left follow; a blank name or Escape puts it back;
       F2 on the row and Renommer in its right-click do the same in place; Details lists Forêt d'Astrub with its share
       ("12/68") and the Bûcheron's woods;
    5. restart D3B: the zone is there with its maps, in `Documents/D3B/zones/nouvelle-zone.json`, one map a line;
    6. in a character's tab, Zone lists it first under "Mes zones" with its maps; choose it: "<name> · Zone à moi · N
       cartes"; the World tab's Details of the zone says "Zone de <the character>";
    7. right-click the row, Supprimer: it goes to the system's trash, the map closes it, the character's Zone says
       "Zone nouvelle-zone introuvable"; put it back from the trash and restart D3B: everything is back;
    8. put a stray letter in a zone's file: the Library's "Ignorés" lists it, and it is never written over.
- **step 3, undo and redo of my zones** (the owner's request of 2026-11-17), on Windows x64 from `bun run app` on
  `next` or the next release, with format 4's data:
    1. "+" makes "Nouvelle zone"; paint two strokes with the brush, then a whole sub-area with S: Ctrl+Z three times
       undoes the sub-area, then each stroke, one stroke a step; Ctrl+Y twice and Ctrl+Shift+Z once bring them back;
    2. rename it in Details, then Ctrl+Z on the map: the old name is back in the row, the top left and Details; while
       typing in the name's field, Ctrl+Z undoes the typing only;
    3. click a row of "Mes zones" in the World view, then Ctrl+Z there: the change is undone the same way;
    4. delete the zone from its row: Ctrl+Z brings it back, open on the map with its maps, its file in
       `Documents/D3B/zones` again (its copy stays in the system's trash); Ctrl+Y deletes it again;
    5. make a second zone on Incarnam and paint a stroke, then open the first zone on the Twelve: Ctrl+Z undoes the
       stroke, the map going to Incarnam with the second zone open; Ctrl+Z again removes it, sent to the trash, and
       the map closes it;
    6. a stroke then Ctrl+Z at once, quickly: the stroke is undone, never the change before it; restart D3B: Ctrl+Z
       undoes nothing (the history lasts the session).
- **step 3, a character's blueprint and settings** ((10)'s Zone field and (11)), on Windows x64 from `bun run app` on
  `next` or the next release, with format 4's data:
  Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: items 1, 3, 4, 6 and 8 pass, and 2,
  5 and 7 but for what was not tried (the arrow opening the blueprint's tab, "Zone de A et B", the file's schema and an
  older file still reading). Not measured yet: item 9.
    1. play a character: its tab says "Blueprint", "Aucun blueprint" in the list, its arrow greyed, no setting; then
       its state and map as before;
    2. choose Récolte automatique: Zone "À choisir", Banque à 90 %, Banque "La plus proche" and Garder "Rien", both
       tagged "étape 5"; the arrow opens the blueprint's tab; the help of each setting shows on hover;
    3. Banque à: type 5, leave the field: it turns red, then 90 again; type 80, Enter: a blue bar on its left and a
       reset button "Revenir à la valeur du blueprint (90 %)"; click it: 90, the bar gone; type 90 again: no bar;
    4. click Zone: the list unfolds under it, "Mes zones" first, then the sub-areas by area and level; type "foret",
       arrows and Enter choose Forêt d'Astrub: the field says "Forêt d'Astrub · Astrub · niv. 20", marked; its target
       opens the World tab with a banner for the character, a hovered map outlines its whole sub-area, one click
       chooses it and the character's tab comes back with it; Escape on the map cancels and comes back too;
    5. in the World tab, choose Forêt d'Astrub in the view: Details says "Zone de <the character>"; a second character
       given the same Zone: "Zone de A et B"; another sub-area: no "Zone de";
    6. set Banque à to 80, then choose Récolte d'un métier: the Zone and Banque à 80 are kept, marked; Métier "À choisir" lists the harvesting
       professions with their icons, by name; choose Bûcheron: marked; choose "Aucun blueprint", then Récolte
       automatique again: back to its defaults;
    7. a second character keeps its own values; restart D3B: both kept, in `Documents/D3B/characters/<server>-<id>.json`
       (`"blueprint"` and `"settings"`, `"schema": 3` since (23)); a file written before keeps reading;
    8. close a character's client: its offline tab shows the same block, and a setting changed there is kept when it
       plays again;
    9. duplicate Récolte automatique, give it to a character, then delete its file by hand and restart D3B: the list says "Blueprint
       <id> introuvable" and no setting shows.
- **step 3, a blueprint's graph edited** ((12)), on Windows x64 from `bun run app` on `next` or the next release:
  Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: items 1 to 5 pass: (12) is measured
  on Windows x64.
    1. open Récolte automatique: its nodes do not move, no wire can be drawn, a right-click or Space opens nothing;
       the wire from "Déposer à la banque" back to "Aller dans" runs under the nodes, no line leaving the screen, and
       no mini-map in the bottom right corner;
    2. duplicate it and open the copy: drag "Récolter"'s Échec onto the empty graph: the picker opens there, "après
       « Échec »", "Souvent ici" Prévenir, Attendre, Terminer, then the rest by family, only what takes the flow;
       "Aller dans …" tagged "étape 4"; type "prev": Prévenir alone; Enter: a Prévenir node where the wire was
       dropped, wired from Échec;
    3. drag the Zone pill onto "Pods": the pin refuses it and says "Cette entrée ne prend pas ce que la sortie porte."
       beside the pointer; release: nothing changes; drag it onto "Zone": the old wire is replaced;
    4. drag "Au lancement"'s output onto Prévenir: its wire to Récolter goes; a right-click, then a click on the graph
       and Space with the pointer on it, open the picker with every node, "Lire" and "Mettre" the variables and the functions;
    5. move a node, close the tab and reopen it, then restart D3B: the nodes and wires are where they were left,
       in `Documents/D3B/blueprints/<id>.json`; the Library's count of problems follows the graph's (a Prévenir with
       no text: one).
- **step 3, deleting and undoing in a blueprint's graph** ((13)), on Windows x64 from `bun run app` on `next` or the
  next release:
  Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: item 6 passes, and 2 to 5 but for
  what was not tried (Ctrl+Shift+Z, Delete with the Library's list focused, a right-click inside a box, two nodes moved
  together undone in one step, the file following each change, after a restart). Not measured yet: item 1, and Ctrl+Z in
  the picker's search (item 4).
    1. open Récolte automatique: a click on a node then Delete or Backspace removes nothing, a right-click on a node
       offers no "Supprimer", Ctrl+Z changes nothing;
    2. duplicate it and open the copy: click "Récolter", press Delete: it goes with its four wires, at once, without
       a question; Ctrl+Z (the key Z of the keyboard's layout, AZERTY's too): it comes back where it was, with its
       wires; Ctrl+Y: it goes again; Ctrl+Shift+Z after a Ctrl+Z does the same; choose a node, click the Library's
       list, press Delete: nothing goes;
    3. Shift and drag a box around two nodes (or Ctrl and click the second), then Backspace: both go with their wires;
       one Ctrl+Z brings both back; right-click inside the box: "Supprimer" deletes both; move two nodes dragged
       together, then Ctrl+Z: both go back in one step;
    4. right-click a node: "Supprimer · Suppr" deletes it; right-click a wire: "Supprimer" deletes that wire alone;
       right-click the empty graph: the picker opens, no menu; type in the picker's search and press Ctrl+Z: the text
       is undone, not the graph;
    5. make three changes, close the tab and reopen it: Ctrl+Z still undoes them, one at a time, and the file in
       `Documents/D3B/blueprints/<id>.json` follows each; restart D3B: Ctrl+Z undoes nothing;
    6. give the copy to a character, then right-click it in the Library: "Supprimer" (absent on D3B's) sends it to
       the system's trash without asking and closes its tab; the character's tab says "Blueprint <id> introuvable";
       restore the file from the trash and reopen the Library: it is back, and the character runs it again with its
       values.
- **step 3, values typed on a node's inputs** (the owner's request of 2026-11-17), on Windows x64 from `bun run app`
  on `next` or the next release:
    1. open Récolte automatique and choose "Récolter": Details says its inputs' values, no field;
    2. in a copy of mine, add a Prévenir and choose it: Details shows "Texte" as a field; type "Objectif atteint",
       press Enter: the node's sentence says it, the Library's count of problems drops by one, and the file
       `Documents/D3B/blueprints/<id>.json` holds `"inputs": {"text": "Objectif atteint"}`; Ctrl+Z (outside the field)
       takes it back, Ctrl+Y puts it again;
    3. choose "Récolter": "Zone" and "Pods" say "relié", without a field; add "Attendre … de vie" from the picker and
       choose it: set "Vie" to 101: the field is marked and put back when left; set 80: a button "Revenir à la valeur
       par défaut (90 %)" shows; click it: 90 %, and the file holds no `life` on that node;
    4. add "Écrire Banque à" (Banque à is bounded from 10 to 100): 5 is refused, 50 kept; add "Aller dans …": its
       "Zone" is the Zone field, its target choosing on the World's map and coming back to the blueprint's tab; clear
       a Prévenir's text and press Enter: its value goes and its problem comes back.
- **step 3, Advanced tools** ((24)), on Windows x64 from `bun run app` on `next` or the next release:
  Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: items 1, 2, 3 and 5 pass, and 4 but
  for the palette's command, not tried. Not measured yet: item 6. The owner then asked for the panel closed by default
  and opened by a character online (2026-11-17), the next PR's work.
    1. with no `Documents/D3B/settings.json`, start D3B: no panel under the editor area, no panel toggle in the title
       bar, no Panneau in Affichage, Ctrl+J does nothing, and the palette has no "Afficher ou masquer le panneau";
    2. Affichage > Outils avancés: the panel slides open on the Protocol view, the toggle, Panneau and Ctrl+J come
       back, and `settings.json` says `"advancedTools": true`;
    3. restart D3B: the panel shows the Protocol view at once;
    4. "Afficher ou masquer les outils avancés" in the palette: the panel closes; restart: it stays hidden;
    5. Advanced tools off, play a character for a minute, then turn them on: its tab's frames of that minute are listed;
    6. write `{"schema": 9}` in `settings.json` and restart: a notification says the file is kept as it is and why,
       and Advanced tools are off; turn them on: a notification says the setting holds until D3B closes and why, and
       the file is unchanged.
- **step 3, a given blueprint's update** ((23)), on Windows x64 from `bun run app` on `next` or the next release:
    1. give a character Récolte automatique and set Banque à to 80; quit D3B; in its file
       `Documents/D3B/characters/<server>-<id>.json`, set `"blueprintVersion": 0` and add `"gone": 1` to `"settings"`;
       start D3B: its tab says above its settings "Récolte automatique v1 : ta valeur de gone ne s'applique plus.",
       Banque à still 80, and the file says `"schema": 3`, `"blueprintVersion": 1`, `"dropped": ["gone"]`, no `gone`;
    2. close a client: its offline tab shows the same notice; click its ×: the notice goes, and stays gone after a
       restart (`dropped` gone from the file); restart again: nothing changes;
    3. choose Chasse, then Récolte automatique: no notice; the file's `blueprintVersion` follows (1), and none for a
       copy of mine;
    4. when a release first ships a given blueprint at version 2: a copy made before shows a blue arrow on its
       Library row, its hover and its tab's line saying "son original a une version plus récente, v2"; a character
       running it keeps its values, a notice naming any lost.
- **step 3, the `.d3b` file** ((22)), on Windows x64 from `bun run app` on `next` or the next release:
  Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: items 1, 2 and 3 pass, and a
  `.json` refused (item 5). Not measured yet: item 4, the rest of item 5, and item 6.
    1. right-click Récolte automatique in the Library, Exporter…: the system's save dialog offers
       `Récolte automatique.d3b`; save it: a notification names the file; it is indented JSON beginning `"d3b": 1`;
    2. on another D3B (or after moving the file away from `Documents/D3B`), Importer… in the Library's header, choose
       the file: the preview shows its name, description, "Basé sur Récolte automatique v1", Zone, Pods and Banque under
       "Réglages par personnage" with their defaults, no problem, and what it holds; Annuler: nothing in "Les miens";
    3. drop the file on the window, Importer: it joins "Les miens" as `recolte-automatique` and its tab opens, its graph
       the same as the given one's; import it again: `recolte-automatique-2`, the first untouched;
    4. export one of mine with the Exporter button of its tab, import it: the same nodes, wires, values and layout;
    5. drop a `.json`, a `.d3b` cut in half, and one whose `"d3b"` is 2: each is refused with why (not a .d3b, its line
       and column, a newer D3B), and nothing is added;
    6. paint a zone `Blé` of three maps, set it as the Zone default of a blueprint of mine, export it, delete the zone,
       import the file: `Blé` comes back with its three maps and the blueprint names it; import it again: the same zone
       is reused, no `ble-2`; paint a fourth map on `Blé`, import once more: a zone `ble-2` holds the three maps and
       the new blueprint names it, `Blé` keeps its four.
- **Done in code, step 1**: `tools/extractor` reads the client's bundles, data tables, texts in five languages and flag
  enums, and writes the map positions, sub-areas, areas, zaaps, worlds and their tiles, maps, and the world graph,
  checked against one another, with the `client-<build>-format<n>.tar.gz` that `data:fetch` reads (ADR 0013); the app reads it all at start
  but the maps, which it reads one file at a time. The readers of maps, of the world graph and of the tiles have their
  checks tested in CI on objects built by hand (`TestMap`, `TestGraph`, `TestTiles`), the debt of step 1's review; only
  the checks of a bundle itself (one container, unique identifiers, its stream files) need the installed client.
- **Published**: `client-3.7.13.17.tar.gz`, from `next` f3104e9 (SHA-256 `74cfd3c3…`), added to the C# version's
  `client-3.7.13.17` release on the owner's go. v0.11.0 shipped without data, named before. Format 2 (the breeds):
  `client-3.7.13.17-format2.tar.gz`, from `next` 2b0577c (SHA-256 `2e3ac196…`, 84,146,484 bytes), added to the same
  release on the owner's go on 2026-11-13; `.github/game-data.json` named it until format 3 (step 3's (3b)), then
  format 4 (9b). Format 4 (the items, the jobs, the icons and the hints; format 3 never published):
  `client-3.7.13.17-format4.tar.gz`, from `next` 55e4be8 (SHA-256 `3c4f5060…`, 105,345,462 bytes), added to the same
  release on the owner's go on 2026-11-16, so `data:fetch` and the next release find it; that day, before the
  owner's measures, `bun run data:fetch 3.7.13.17` filled `data/` with it and the Rust test reading `data/` passed on
  it (the status bar not looked at). **v0.12.0**, published on the owner's go on 2026-11-11, packs format 1's data
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
  an icon the client lacks counted, an archive of 105 MB in 30 s, the same bytes on two runs); its publishing superseded by format 4's, (9b); (4) D3B's blueprints in the app, and
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
  owner, 2026-11-14; done in code); (9) the World view and tab from the tiles (the owner's mock-up 10 of 2026-11-15), in three PRs: the atlas
  through the IPC, the worlds with their tiles and the sub-areas by area (done in code), then the tab and its map (done
  in code), then the view that opens it (done in code); (9b) the extractor: the client's hints
  (`World.HintsDataRoot`, 835 on 3.7.13.17 in 6 categories, measured by the C# version) and their pictures at the
  client's 64 px (the owner, 2026-11-15), a new data format, 4 (ADR 0013; done in code: 835 hints, 12 categories, 91
  pictures of 395 KB, graphic 475's missing one counted, an archive of 105 MB in 28 s); format 3 is then never
  published: `client-3.7.13.17-format4.tar.gz` replaces it, published on 2026-11-16 (under "Published"); (9c) the hints on the World's map (the owner's mock-up 11, 2026-11-15): pictures 28 px on screen, those that would
  overlap thinned out by kind over the whole world, a switch per kind behind "Indices" in the tab's editor action, nine
  kinds told apart by picture before category, all shown at first (the owner, 2026-11-17, in place of the other
  places and passages off by default of 2026-11-15) (done in code; the nearest
  bank waits for step 4, measured by path with going to the bank);
  (10) sub-areas as zones, a Zone chosen in a list or on the map (the owner's mock-up 12, 2026-11-15: the field's list
  unfolding under it and its target, one click on the map choosing and going back, Details on the World tab's right),
  in three PRs: each sub-area's resources by profession and its monsters counted through the atlas (done in code), then
  the World tab's Details and a click on its map (done in code), then the Zone field and the map's choosing mode, which (11) puts in
  the character's tab and is measured with it (done in code: `ZoneField`, `src/zone/`, its checklist with (11)'s); (11) a character's blueprint and settings in its tab (the owner's choices of 2026-11-16: none at first; another blueprint keeping the values of the same id and type; editors for all but a bank and items, which wait for step 5; the offline tab editable too), in two PRs: the character's file, schema 2, and the IPC that chooses its blueprint and sets its settings (done in code), then the block in the character's tabs, live and offline, with the Zone field and "Zone de" in the World's Details (done in code; its checklist under "Not measured");
  (12) links checked by type, and the picker (the owner's choices of 2026-11-16: each change saved at once, a wire
  replacing the one that fed its input or left its flow output, the picker offering the catalogue and the blueprint's
  variables and functions, "Often here" from a fixed table by context), in two PRs: `edit_blueprint`, a wire, a node
  added or moved checked and saved, and each graph's choices (done in code), then the graph editable, a wire checked
  as it is dragged, and the picker (done in code; measured on Windows x64 on 2026-11-17); (13) delete and undo
  (autosave done by (12); the owner's choices of 2026-11-16: undo and redo kept in Rust, 100 changes for the session; a
  deletion not confirmed; a blueprint of mine deleted to the system's trash without asking, the characters that run it
  keeping its id), in two PRs: nodes and wires deleted, a move of many one change, undo, redo and `delete_blueprint`
  through the IPC (done in code), then Delete, Backspace and a context menu in the graph, Ctrl+Z and Ctrl+Y, and
  Delete in the Library (done in code; its checklist under "Not measured"); the owner's requests of 2026-11-17, before
  v2 (the owner's choices of that day): values typed on a node's inputs in Details (done in code; its checklist under
  "Not measured"), then my variables, (14)'s first PR, then renaming a blueprint of mine; then **blueprints v2**
  ([bot.md](bot.md#the-language-v2), ADR 0016; the owner, 2026-11-16, after trying (12): "much more complete", "as
  practical as a Lua script"), from a mock-up of the catalogue and the Summary and an analysis of the reference Lua
  scripts: compact value nodes and a typed Expression, wildcards, lists and level ranges, flow for a bot, "When …
  becomes true" and events of mine, functions with exits, locals and pure ones, durations and times, randomness, a loop guard
  at run time, the unobserved data marked and studied at its step; before (14), so that the editors of variables,
  functions and events and the runtime are written once, on the final model, in fifteen PRs: (v2.1) the model in Rust, in three PRs: the new types (an NPC, ranges), lists and wildcard
  pins in the wiring's check; then a node's `options`, the blueprint's own events, functions' exits, locals and pure;
  then the file's schema 2 and its migration from 1 (in a `bot/` subfolder, the folder holding ten files), D3B's
  blueprints in 2; each kind's problems come with the PR that brings the kind; (v2.2) the editor reads it: wildcards and
  lists (a square pin) in the drag's check, a node's options ("+" a case, an output), the new types' colours and
  values typed (a duration, a time, a map, an item, ranges as a table pasted from text); (v2.3) the flow's kinds and how the Summary reads them; (v2.4)
  the values: comparisons, logic, arithmetic, randomness, text, durations, lists, drawn compact; (v2.5) the queries
  over `game::Live`, and those marked with their study and step; (v2.6) the events: When, Every, At, the game's new
  ones, mine and Trigger with its timers; (v2.7) the actions of steps 4 to 7, marked (the haven bag, zaaps, doors,
  NPCs and their replies, equipment, resource bags, the experience received), and the variables' nodes (Add to, Reset,
  a list's); (v2.8) the Expression: its parser and checks in Rust, its node and completion in the editor.
  (v2.9) the picker by intent with its synonyms, and a node's inputs folded under "Advanced"; (v2.10) D3B's eight
  blueprints (Récolte, Combat, Mineur, Paysan, Alchimiste, Pêcheur, Bûcheron, Chasseur: the owner, 2026-11-16), with
  the ranges computed from the data and the C# version's measured behaviours, in four PRs: the extractor's format 5,
  the monsters' grades, levels and drops (published on the owner's go, ADR 0013); the ranges; Récolte and the five
  harvest blueprints; Combat and Chasseur, after the hunting weapon's and the meats' study. Each brings its sentences in French and English and its family in the picker; its checklist,
  [bot.md](bot.md#measure)'s items 6 and 7, is taken once (16) declares variables and events of mine. Then (14) variables, with locals, lists and the new types' editors; (15)
  functions opened in place, with their exits and the pure ones; (16) events: priority, re-arming, modes, the
  defaults, and the events of mine declared; (17) comments and reroutes; (18) Align, copy and paste,
  search; (19) editing by keyboard; (20) my zones painted (the owner's choices of 2026-11-16: coordinates on one world, a floating toolbar of four tools, "+" in the World view and the name in Details, amber), in PRs: `zones/<id>.json` and the IPC that lists, creates, paints, renames and deletes them (done in code), then my zones in the World view and the Zone field (done in code), then the map's tools and Details (done in code; its checklist under "Not measured"), then their undo and redo (the owner's request and choices of 2026-11-17: one history for all my zones, kept by Rust; done in code, its checklist under "Not measured"); (21) routes, with an action and a forced exit per anchor, Follow the route resuming from
  the map the character is on, the recorder and the text import and export; (22) the `.d3b` file (the owner's choices of 2026-11-16, ADR 0018: one readable JSON, its zones and routes joining it with (20) and (21), a preview dialog, Export in the Library's menu and the tab, Import in its header and by a drop), in PRs: the file, its export, preview and import through the IPC (done in code), then the Library's Export and Import, the preview and the drop (done in code; its checklist under "Not measured"), then the zones it names joining it (done in code, item 6 of its checklist); (23) a given blueprint's
  update (the owner's choices of 2026-11-16: a notice in the character's tab until closed, a value kept when the new
  version accepts it, a copy saying its original is newer with a blue arrow in the Library and nothing more yet; done
  in code, its checklist under "Not measured"); (24) the "Advanced tools" setting (the owner's choices of 2026-11-16: in the palette and View, no Settings
  view yet; `settings.json` in the player's folder; the panel gone while off; the frames followed even then; done in
  code, its checklist under "Not measured"); (25) the runtime: the flow, outcomes, variables, blocked, finished, on the
  captures, with v2's loops, Switch, lists, randomness, pure functions and the loop guard (designed with the owner on
  2026-11-16, [bot.md](bot.md#the-runtime), ADR 0017: the engine alone, wired by step 4's Play; an action before its
  step waiting for its check; memory in the character's file; the engine now on schema 1, v2's kinds each bringing
  their behaviours), in three PRs started before v2's: the fight's end pinned and the fights won and lost counted, the
  death's signal (done in code: 634 fight ends read, 613 won and 21 lost in 6 connections, as the study); the engine,
  its stack, its trait, Get, Set, Call, If, Finish and the returns (done in code: Sequence, a loop and Leave the loop
  proved by the tests' own kinds; a size exception, the owner's of 2026-11-16); the nodes that wait, the actions' checks, the
  queries, the death and the replay on the captures (done in code: 49 runs on the 54 game connections, 6,900 turns,
  blocked only by the 6 deaths, at their fights' end; D3B's three blueprints run and hold at their first action).
  (25) is measured with step 4's Play; (26) events at run time:
  priorities, modes, thresholds, settings at safe points, with When, Every, At, the events of mine and their timers. Step 4 then brings Play,
  what a character does now, its History and the debugger, with the first actions.
- **Waiting on testers**: Dofus 3 has a native client on macOS and on Linux (the owner, 2026-11-11); where it is
  installed, whether its data is Windows', and the relay on each are measured by the first testers there.
