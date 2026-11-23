# Now

_Updated 2026-11-23._

- **Step**: 2, Observe ([ROADMAP.md](../ROADMAP.md)); its first feature, the relay and the client's redirection, is
  designed with the owner ([docs/relay.md](relay.md), ADR 0011); its second, the protocol and its per-build mapping,
  is designed with the owner on 2026-11-11 ([docs/protocol.md](protocol.md), ADR 0012); its third, the live view of
  each character and the protocol inspector, on 2026-11-12 ([docs/game.md](game.md)). Steps 0 and 1 are done but
  for their measures below, which the owner takes alongside: the owner started step 2 before them, on 2026-11-11.
- **Step 3 designed ahead**: what is left of step 2 waits on outside events (the next release for the in-app Restart,
  client 3.7.14.18, out on 2026-11-16, for the protocol's item 7 before a release carries its pins, a Windows account whose name holds a space or an accent for the
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
- **Open checklists**, one file each in [checklists/](checklists/README.md) (`Status: open` or `waiting`),
  one line each for what is left; step 3's own measure is [bot.md](bot.md#measure), taken with these:
  - [v2.2, the editor reads it](checklists/v2-2-editor-reads-it.md): open, items 1 to 7.
  - [v2.3, the flow's kinds](checklists/v2-3-flow-kinds.md): open, items 1 to 5.
  - [the protocol](checklists/protocol.md): open, item 7 with v0.14.0 on client 3.7.14.18, before the next release.
  - [the breed](checklists/breed.md): open, items 1 and 2 (the owner's extraction and `data:fetch`), 5 with v0.14.0.
  - [the characters played](checklists/characters-played.md): open, items 1 to 6.
  - [the Library](checklists/library.md): open, items 1 to 6.
  - [the World](checklists/world.md): open, item 8's pictures, unticking, "Tout cacher", "Tout montrer", Incarnam.
  - [my zones painted](checklists/zones-painted.md): open, items 6 to 8 and the parts of 2 to 5 not tried.
  - [undo and redo of my zones](checklists/zones-undo.md): open, items 3 and 5, and the parts of 4 and 6 not tried.
  - [a character's blueprint and settings](checklists/character-blueprint.md): open, item 9, the parts of 2, 5, 7.
  - [deleting and undoing in a graph](checklists/graph-delete-undo.md): open, item 1, the parts of 2 to 5 not tried.
  - [values typed on a node's inputs](checklists/node-values.md): open, item 4, the parts of 2 and 3 not tried.
  - [my variables](checklists/my-variables.md): open, items 1 and 4, the parts of 2, 3 and 6 not tried.
  - [renaming a blueprint of mine](checklists/rename-blueprint.md): open, the parts of 1 to 3 not tried.
  - [Advanced tools](checklists/advanced-tools.md): open, items 4 and 6.
  - [the Protocol panel's opening](checklists/protocol-panel.md): open, item 5, the parts of 1 and 3 not tried.
  - [a given blueprint's update](checklists/blueprint-update.md): open, items 1 to 3; 4 waits on a given v2.
  - [the `.d3b` file](checklists/d3b-file.md): open, items 4 and 6, and the rest of 5.
  - [the relay](checklists/relay.md): waiting, item 10's offer on the release after v0.14.0, 11 on an accented account.
  - [macOS, Linux and ARM64](checklists/platforms.md): waiting on testers.
  Measured, kept as records: [step 1](checklists/step1-extractor.md), [the life](checklists/live-view-life.md),
  [the graph edited](checklists/graph-edited.md).
- **Done in code, step 1**: `tools/extractor` reads the client's bundles, data tables, texts in five languages and flag
  enums, and writes the map positions, sub-areas, areas, zaaps, worlds and their tiles, maps, and the world graph,
  checked against one another, with the `client-<build>-format<n>.tar.gz` that `data:fetch` reads (ADR 0013); the app reads it all at start
  but the maps, which it reads one file at a time. The readers of maps, of the world graph and of the tiles have their
  checks tested in CI on objects built by hand (`TestMap`, `TestGraph`, `TestTiles`), the debt of step 1's review; only
  the checks of a bundle itself (one container, unique identifiers, its stream files) need the installed client.
- **Published**: `client-3.7.14.18-format5.tar.gz`, from `next` 220473d (SHA-256 `331fe578…`, 105,692,906 bytes), in
  the C# version's `client-3.7.14.18` release on the owner's go on 2026-11-17, which `.github/game-data.json` names.
  `client-3.7.13.17.tar.gz`, from `next` f3104e9 (SHA-256 `74cfd3c3…`), added to the C# version's
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
  an account is at hand; the protocol's item 7 now, with v0.14.0 on client 3.7.14.18, before the release that carries
  its pins: 3.7.14.18's schema is 3.7.13.17's, so its pins are copied ([protocol.md](protocol.md#on-the-wire)), and its
  data packed (`.github/game-data.json`), without which a character's map and breed would not show, the data's build
  having to be the connection's ([game.md](game.md#in-the-app)); Q17,
  a build that changes the schema, waits for a later patch (`GameAssembly.dll`,
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
  "Not measured"), then my variables, (14)'s first PR, in three PRs: created, changed and deleted through
  `edit_blueprint`, the characters' settings following (done in code), then My Blueprint's "+" and Details (done in
  code; its checklist under "Not measured"), then a variable dragged onto the graph (done in code, item 6 of
  that checklist); then renaming a blueprint of mine (done in code; its checklist under "Not measured"); then
  **blueprints v2**
  ([bot.md](bot.md#the-language-v2), ADR 0016; the owner, 2026-11-16, after trying (12): "much more complete", "as
  practical as a Lua script"), from a mock-up of the catalogue and the Summary and an analysis of the reference Lua
  scripts: compact value nodes and a typed Expression, wildcards, lists and level ranges, flow for a bot, "When …
  becomes true" and events of mine, functions with exits, locals and pure ones, durations and times, randomness, a loop guard
  at run time, the unobserved data marked and studied at its step; before (14), so that the editors of variables,
  functions and events and the runtime are written once, on the final model, in sixteen PRs: (v2.1) the model in Rust (done in code), in four PRs (the owner, 2026-11-17: the second split in two): the new types (an NPC, ranges), lists and wildcard
  pins in the wiring's check (the owner's choice of 2026-11-17: a wildcard's type noted on its node; done in code); then a node's `options` and the pins they make (done in code); then the blueprint's own events, functions' exits, locals and pure (done in code);
  then the file's schema 2 and its migration from 1 (in `bot/schema/`, the folder holding ten files), D3B's
  blueprints in 2, with `≥` and N minutes brought before v2.4 for "at least" and Wait's minutes (done in code); each kind's problems come with the PR that brings the kind; (v2.2) the editor reads it (done in code, a node's options with v2.3's second PR; its checklist under "Not measured";
  [bot.md](bot.md#the-editor-reads-it-v22), the owner's
  choices of 2026-11-17: pins and colours from a mock-up, a map picked on the World or where a character stands, an
  item searched among all, ranges pasted as `1-49: value` replacing the table, my variables taking the new types and
  "List" and "By level" so each editor is used now), in seven PRs: the Rust and the IPC (an untyped `≥` open to its
  types, a type not taken refused when wired, `of` sent, `NodeView.options` and `types`; done in code); the drag's check, the
  picker's filter, the pins' shapes and colours (done in code); Details' type chooser on a wildcard, the time and the values' words (done in code);
  my variables' new types and the list editor (done in code); items (done in code); maps (done in code); ranges (done in code); then a node's options with v2.3; (v2.3) the flow's kinds and how the Summary reads them
  ([bot.md](bot.md#the-flows-kinds-v23), the owner's choices of 2026-11-17: started before (v2.2) is measured, its
  measures alongside; Sequence without Completed, Retry a node with a body, a node's memory for the run, Do once as Do
  N times, Pick of one mode, Trigger with (v2.6), the French names on a mock-up of the Summary), in six PRs: Sequence and
  Switch in Rust with the options' edits (done in code); a node's options in the editor (done in code); the Summary by pin roles (done in code); the loops (done in code); the
  kinds that remember (done in code); the waits and stops, in two PRs: Wait until, Block, and Pause and notify (done
  in code), then Retry and a run on the captures (done in code; (v2.3)'s checklist under "Not measured"); (v2.4)
  the values: comparisons, logic, arithmetic, randomness, text, durations, lists, drawn compact
  ([bot.md](bot.md#the-values-v24), the owner's choices of 2026-11-17: started before (v2.2) and (v2.3) are measured,
  their measures alongside; drawn compact as Unreal from a mock-up; AND and OR of N inputs; whole numbers; a fresh
  seed per run and no random duration; Text in one field; Choose reading the side it takes; the nodes' words in their
  own namespace), in eight PRs: the namespace `nodes` (done in code); comparisons, logic and Choose (done in code); arithmetic and N seconds (done in code);
  randomness (done in code); lists; Text and Join; drawn compact; sentences in place and the checklist; (v2.5) the queries
  over `game::Live`, and those marked with their study and step; (v2.6) the events: When, Every, At, the game's new
  ones, mine and Trigger with its timers; (v2.7) the actions of steps 4 to 7, marked (the haven bag, zaaps, doors,
  NPCs and their replies, equipment, resource bags, the experience received), and the variables' nodes (Add to, Reset,
  a list's); (v2.8) the Expression: its parser and checks in Rust, its node and completion in the editor.
  (v2.9) the picker by intent with its synonyms, and a node's inputs folded under "Advanced"; (v2.10) D3B's eight
  blueprints (Récolte, Combat, Mineur, Paysan, Alchimiste, Pêcheur, Bûcheron, Chasseur: the owner, 2026-11-16), with
  the ranges computed from the data and the C# version's measured behaviours, in four PRs: the extractor's format 5,
  the monsters' grades' levels, drops and flags, their races and super races (the owner's choices of 2026-11-17, race
  347 and its 6 monsters left out; done in code, `client-3.7.13.17-format5.tar.gz` published on the owner's go of
  2026-11-17, ADR 0013); the ranges (the owner's choices of 2026-11-17: a table by level of the richest sub-areas,
  resources counted once by kind, monsters as the C# version measured, a sub-area above the character or closed to
  its account left out as the bot plays, the rows below read when none is left, none without a map on a world map; done in code, `bot::places`, read by the
  next two PRs, [bot.md](bot.md#the-language-v2)); Récolte and the five
  harvest blueprints, which also harvest on the way (the owner, 2026-11-18); Combat and Chasseur, after the hunting weapon's and the meats' study. Each brings its sentences in French and English and its family in the picker; its checklist,
  [bot.md](bot.md#measure)'s items 6 and 7, is taken once (16) declares variables and events of mine. Then (14) variables, with locals, lists and the new types' editors; (15)
  functions opened in place, with their exits and the pure ones; (16) events: priority, re-arming, modes, the
  defaults, and the events of mine declared; (17) comments and reroutes; (18) Align, copy and paste,
  search; (19) editing by keyboard; (20) my zones painted (the owner's choices of 2026-11-16: coordinates on one world, a floating toolbar of four tools, "+" in the World view and the name in Details, amber), in PRs: `zones/<id>.json` and the IPC that lists, creates, paints, renames and deletes them (done in code), then my zones in the World view and the Zone field (done in code), then the map's tools and Details (done in code; its checklist under "Not measured"), then their undo and redo (the owner's request and choices of 2026-11-17: one history for all my zones, kept by Rust; done in code, its checklist under "Not measured"); (21) routes, with an action and a forced exit per anchor, Follow the route resuming from
  the map the character is on, the recorder and the text import and export; (22) the `.d3b` file (the owner's choices of 2026-11-16, ADR 0018: one readable JSON, its zones and routes joining it with (20) and (21), a preview dialog, Export in the Library's menu and the tab, Import in its header and by a drop), in PRs: the file, its export, preview and import through the IPC (done in code), then the Library's Export and Import, the preview and the drop (done in code; its checklist under "Not measured"), then the zones it names joining it (done in code, item 6 of its checklist); (23) a given blueprint's
  update (the owner's choices of 2026-11-16: a notice in the character's tab until closed, a value kept when the new
  version accepts it, a copy saying its original is newer with a blue arrow in the Library and nothing more yet; done
  in code, its checklist under "Not measured"); (24) the "Advanced tools" setting (the owner's choices of 2026-11-16: in the palette and View, no Settings
  view yet; `settings.json` in the player's folder; the panel gone while off; the frames followed even then; measured by
  the owner on 2026-11-17, who then asked for the panel closed at start and opened by a connected character: done in
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
  priorities, modes, thresholds, settings at safe points, with When, Every, At, the events of mine and their timers,
  now after step 4's first slice (below), which brings Play and what a character does now; its History and the
  debugger come after that slice.
- **Next, step 4's first slice, designed** ([play.md](play.md), ADR 0021; the owner's choices of 2026-11-23, after
  audits comparing `next` with `main`): D3B's Récolte automatique playing in game on Windows, on a game layer that reads
  every family the C# version reads before it acts, through typed acts with deadlines and a watchdog of 10 minutes;
  in about 19 PRs (F1 to F10, R1 to R9), its measure a 30-minute loop unblocked.
