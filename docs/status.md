# Status

Where the project stands, on one page. Read it first: it says what exists, what is not measured, what comes next and what is
owed. The reasons are in [the decisions](decisions/README.md), found through [decisions-index.md](decisions-index.md); the steps and
their sessions are in [roadmap.md](roadmap.md); how the code is built is in [architecture.md](architecture.md). Whoever closes
a session updates this page, the roadmap and the index.

As of 2026-11-10, after D425 (the harvest's tour goes into another zone of a map, out and back through the world graph), D424 (a resource in another zone of its map is out of reach, no longer used from afar), D421 (7r6, a step of a list repeats and plays another list), D420 (the Pandawa no longer swaps Sobre and Saoul instead of striking), D419 (7r4, following a run), D418 (7r5b, the profile as an asset editor), D415 (7r5 part 1, the zones read by the harvest and the hunt), D417 (the harvest's tour weighs the character's level) and D416 (it leaves the houses of a town alone), D414 (hints on the world map), D413 (7r3, the Sequencer) and D408 (the fight AI after the players' reports, D407, summons a last resort; the home page, D406; banking by general settings, D405; D394, session 7d4, merged into 7r2's branch). App 0.8.1, client build 3.7.13.17, pack format 11 published, 12 extracted and not published (D414), mapping format 17.

## What exists

- **A bot that plays through the official client** (D23): the relay between the client and Ankama's servers, the readable
  protocol and its per-build mapping, the data pack extracted from the client (steps 1 to 3).
- **The run time** (step 4): one bot per character, walking, changing maps, using elements, banking, zaaps and the haven bag,
  activities, recovery.
- **Harvesting and the hunt** (steps 6 and 9): both choose their place by the character's levels and go through several
  sub-areas in turn (D391, D392); fights are played by one AI for every breed (D244), with the rules and effects of a fight
  measured on the owner's captures.
- **The desktop app as a studio** (step 7, up to 7d4, 7r2 and 7r3): dark, docked, seen as a game engine (D397: the world's map is the
  centre once a route or a zone is selected, with the characters, routes and zones on it, an Outliner and a Details panel; the studio opens on a home page, D406), the project's assets in files,
  behaviors and profiles as variants, the world's map drawn from the client's images, routes with their actions, a profile
  playing a list of routes and steps made in the app (D386), zones painted on the same map (D394), Undo and Redo in the route, the
  list of steps and the profile editors, and rows dragged or moved by the keys (D394). A route's steps are set in the Sequencer
  under the world (D413): a ruler of steps, a track a kind of action, a clip drawn across the steps it covers and stretched, moved,
  copied, deleted by the pointer or the keys, several steps selected at once, the Details setting a clip's values once for all its steps.
  The world's map also draws the client's own hints, zaaps, dungeons, workshops, banks and more, with a tick for each category (D414).
  A profile opens as an asset editor (D418): four categories, the rare settings folded under "Advanced · N changed" with an amber dot, the world's
  map beside the form to pick the zone or the sub-areas of its place.
- **A versioned Claude Code setup** (D398): `.claude/` holds C# hooks that ask before a push or a `gh` write and refuse
  what the client produces, skills for C#, the studio's UI, tests, Git and the rest, and two agents. The hooks are tested in
  `tests/D3B.Hooks.Tests`; the write guard and the Git guard were seen refusing inside a session, the "ask" answers and the
  Stop hook's block not yet.
- **Published**: the installer and updates through Velopack (D171); the client build's pack, schema and mapping as the release
  `client-3.7.13.17` (D365).

## Not measured in game

- The harvest and the zones of a map (D424): a player's report ("it tries but cannot, unless it stands in front of the resource or comes back from
  another side"), read in the code and measured on the pack (864 maps with more than one zone, 3,673 element and zone pairs on 607 maps), fixed and
  tested on the fake game, not seen in game: that the server refuses a use from a cell two steps away, and that the tour no longer fails on those
  maps. To look at in the client: maps 0, 3333, 3845, 133890, 134403 or 134404, a character in one zone and a resource beside the other's cells.
  D425 (the tour going to the other zone through the world graph, out and back by another map, once per tour) is tested on fake graphs and a fake
  game with a map of two zones, not seen in game. To look at, on the same maps: that the character leaves the map and comes back into the zone of the
  resource (the landing cell's zone is the graph's `toZone`, measured only on the pack), that the vertex of the other zone is the one the character
  stands in once there, the number of map changes the way takes (the cap of six is the lead's reckoning), and the debug lines "the tour will go to
  zone" and "leaves zone ... alone" in the log. A map whose other zone no way leads into is left alone, so is one the graph does not hold.

- The sending (BR4, D410) was run for real from a throwaway tool with the production sink: posts of 0.1 to 20 MiB of random bytes were taken by the owner's
  webhook and 21.5 MiB and more refused (the limit is Discord's, depends on the server's level and may change: a refusal is a result, and the app says to hand
  the file over); a cleaned report of a real capture came back from Discord byte for byte and read by `ReportReader`, with no mention made of an `@everyone` in its
  message; every post was deleted. Not done: a send from the app's dialog, the asset published and read from GitHub, a rate limit (the webhook's own limits were
  not reached), a webhook revoked.
- The report's dialog (BR3, D410, D411) is tested on view models, with captures the tests make, and in a headless window drawn in both languages
  (form, preview, written), and, since the fix after release v0.9.0, through the real `MainWindow` (Help, Report a bug, form, Preview). v0.9.0's Help item opened
  nothing: the window's switch over its dialogs had no case for the report. Now one table (`DialogKind`) registers and shows each dialog, and a test
  fails when a dialog view model (`IDialogViewModel`) has no row. The fix reaches players with the next release. Not opened by the owner from the Help menu, not tried on the capture of a real session through a running host
  (which bot, which connection, which capture, the profile and the assets it reaches), and its look is not judged against a mockup.
- The cleaner (BR2, D423) was run on 48 of the owner's real captures of 3.7.13.17: 45 were cleaned with no ticket, secret or name left in any
  output and 3 were refused for want of a character list the translation reads (the tool fails closed); on one of the largest, whose fights `scripts/fights.cs` replays with the same result as the original. What it leaves is said in D421
  (other players' names, numbers, timestamps, the GUIDs of unknown meaning, a name under 6 bytes in a frame too deep or cut short to walk); a
  next build's own opaque strings are found by shape only if they keep it, and the copy is searched for every secret it found before it is returned.
  The report is not yet made by the app, so no report was sent anywhere. `scripts/report.cs --replay` ran on a report of a real capture.
- Recording by default and its retention (BR1, D409) are tested on temporary files, the relay of the tests and view models, and the settings
  and the home page's notice in headless windows, not seen by the owner in the app and not run on a client. The first start of the host after
  the change deletes the game's captures older than 7 days or beyond 1 GB: the owner's 153 captures, which `scripts/fights.cs` replays, would
  go, so he sets `Network:Captures:Retention:Days` to 0 (or copies `captures\game`) before it. The 7 days and the 1 GB are D409's, the lead's reckoning.
- Repeats and sequences as steps (D421, 7r6) are tried on the fake game, view models and headless windows, not on a client and not by the owner
  in the app: a route followed three times (the journal saying its steps in the first pass only), a loop followed until pods at least 90 % (a lap
  long enough overshoots, the condition being read between passes), a list played inside a list with a fight in the middle of its second step, a
  block that holds a route, the flyout of the repeat token and of the condition (the headless window opens no popup), and how the tokens read under
  each step. The list is in D421; it joins the measure owed below.
- Following a run (D419, 7r4) is tried on the fake game, view models and headless windows, not on a client: whether the position told is the
  position lived with real travel (zaaps, `Busy` waits, a map with no path), a fight or a bank cutting the route and the run resuming on the
  same step, a long action while the playhead stays on its step, two accounts on one route, 250 ms against a map every three seconds. The
  list to look at, one by one, is in D419; it joins the measure already owed below.
- The hints on the map (D414) were seen only as headless renders of the real pack and tiles at four zooms, not in the app by the owner: how
  the pictures read, whether the thresholds and priorities (the lead's reckoning) thin them as they wish, the name over the hovered one. The
  pack of format 12 is extracted on the development machine and not published: the app of this change needs it (`scripts/release.cs --publish`,
  the owner's go).
- The zones as a place (D415) are tested on the fake game and in the view models, not in game and not seen by the owner in the app: a harvest or a hunt through a zone's maps, the field that picks it and the words under it, and what the French "Région" (the game's area, once "Zone") reads like to them.
- The profile as an asset editor (D418) is tested on view models and headless windows, not seen by the owner in the app: how the four categories and the "Advanced" folds read (the marks of each rare setting are the lead's application of D415's list), the map beside the form (its tiles were not drawn in tests: the source was empty), a click on the map against a drag, and the narrow band. A sub-area clicked outside the region chosen only raises the field's "not within" problem.
- The Sequencer (D413) is tested on view models and in the headless window, not yet seen by the owner in the app: how a drag feels
  on a long ruler, how the tracks read at the bottom panel's height, whether it is what they mean by practical; no screen reader was
  run on its grid.

- The routes' actions that talk to the game and the profiles playing a list of steps (7d3c, 7d3d): tried on the test game
  only; a real client with the owner is to do.
- The home page (D406) is tested on view models and a headless window, not yet seen by the owner in the app: its look and whether the six recent assets and the characters are what they expect.
- 7d4's editors (undo, dragging, the zones' tools) are tested on view models and headless windows, not yet by the owner's hand
  in the app: how a drag feels, the focus after a move by the keys, the zones painted at several zooms.
- The owner's reports from game answered since 2026-11-06 (D387 to D392): the hunt attacking every group, the tour leaving
  interiors out, the AI fleeing less, the rotation through ten sub-areas and the professions in turn. The numbers 5, 10, a
  quarter and 50 of D391 and D392 are the lead's reckoning.
- Banking by the general settings (D405): the share of pods and the zaap rule are tested on the fake game and in the app's
  view models. A bank journey that outlives a refused zaap is the lead's reading of a player's report (Incarnam to Astrub, 1,500
  kamas the character lacked), not seen in game; the text the server answers to a zaap that cannot be paid, and the cost of a
  zaap leaving Incarnam's world map, are not measured.
- The harvest leaving the houses of a town alone (D416) and weighing the character's level (D417): a player's report ("I still go into
  all the houses in Astrub"), read in the code and measured on the pack and the owner's learned graphics, tested on the fake game, not
  seen in game. A mine whose graphics no bot has learned is no longer discovered by a tour; an outdoor map is never left out for the
  level, only gone to later.
- The players' reports answered by D407: the new curve of the character's life (above 60% a point is worth a quarter) and the
  plan with the fewest actions among equals, replayed on the captures but not played in game; what the server sends of an
  invisible fighter (no capture holds one: the AI keeps its last cell and `Combatant.IsVisible` is never unset); the damage of a
  long push against a wall (the captures hold pushes of 1 to 4 cells, the pack's *Panache DoPou* pushes 10); the players'
  *Chafer* and *Arakne* summons, which no breed of the pack casts.
- The Pandawa swapping Sobre and Saoul with Bombance or Picole instead of striking (D420): a player's report, reproduced on the imagined fight of the pack's spells and fixed in the judge (a state held as the search began is no gain), not seen in game: no capture holds a Pandawa casting either, so what the server sends back of the swap (the two states, the AP given back, state 1) is read in the pack. The player is to say whether it strikes now.
- Step 9's mechanisms: the Roublard against big enemies with `Fights:Alone`, the Féca's glyphs, an Osamodas's summons and the
  fight map's preview, an aggression, stairs and portals, the inactivity popup, what the client shows once its turn's end goes
  unanswered (D367).

## Next

0. **The harvest going to another zone of its map** (D424 (b), D425) is made in code and tests: to look at in the client with the maps listed above.
0. **The refactoring and tidy-up session** (D399 to D401) is mostly done: `decisions.md` is cut in files by group of the index,
   `client.md` has its game connection apart, `AssetsViewModel`, `BotActor`, `HarvestActivity`, `WorldMapView` and `Imagination`
   are under 900 lines, BenchmarkDotNet is gone. The two files that stayed over 900 lines, `FightJudge` and `RouteEditorViewModel`, are cut too.
0. **Bug reports with the player's capture** (D409 to D411, D422, D423): BR1 and BR2 are made. The game connection is recorded by default
   with its retention and the notice (`Network:Captures:Game`, `CaptureRetention`); what names the account in a capture was measured
   ([client-game.md](client-game.md#the-game-what-names-the-account)) and the cleaner hides it by value (`CaptureScrubber`, D423); a report is
   written and read as a zip with a manifest (`ReportWriter`, `ReportReader`) and `scripts/report.cs` unpacks one. BR3 is made: Help, Report a
   bug writes the file with its code, and BR4 is made: a click sends it to a private Discord webhook (`DiscordReportSink`), read from the asset
   `webhook.txt` of the release `report-channel`, which `scripts/release.cs --report-webhook` publishes (not yet published: the owner's go).
   What is left is to see it in the app and to publish the asset.
1. **7r4, following a run, and 7r6, repeats and sequences as steps, are made in code** (D419, D421), to try in game with the owner's lists; then 7e.
   7r6 left: the condition read between two steps of a route, and, or and not in an "until" (7e's), a pass of a repeated one-way route on the card,
   the lists that repeat a route shown on the route. What 7r3 and 7r4 leave: the map's frame, a panel taller for the Sequencer, the steps already
   passed dimmed on the map, and recording in the toolbar, which waits for the measure in game.
2. **A measure in game with the owner**, before anything else of step 7: the list above, and what 7d4b will record.
3. **Session 7d4b**: recording while playing and Simplify (D299, D394), once the events it reads are measured.
4. **7e and 7e2**: the canvas of behaviors, then 7f to 7i (nodes and planning, debugging, the AI's fight preferences, sharing).
   The breeds, set aside since 9f5, come back once these are done (D306).
5. **Then** step 9's remaining mechanisms, and steps 10 to 13 (several accounts, marketplace and crafting, treasure hunts and
   dungeons, the owner's profession scripts).

The measure in game is worth a session of its own before 7e grows the code further.

## Open questions

Q5 (the fight formulas left: shields, AP and MP removed, erosion), Q12 (what quests, breed and the server's codes gate),
Q16 (casts and walks the server refuses that the rules allow), Q17 (a build that changes its schema). The table is at the end of
[the open questions](decisions/open-questions.md#open-questions).

## Known debt

- `D3B.Bot` is about 17,900 lines in 224 files and `D3B.Studio` about 6,300 in 141 (D393). The border is the compiler's, one way:
  `D3B.Studio` references `D3B.Bot`, which knows nothing of it. Left over: `D3B.Studio.Routes` reaches the harvest and the fight
  through `MapHarvest` and `MapFight`, now public, so a module of `D3B.Bot` cannot declare nodes (the node API is the studio's):
  a future module in a project of its own can. Nine members and types of `D3B.Bot` became public so that the studio could use them
  (`BotContext.Actor`, `BotActor.Time`, `Log`, `PauseAfterStep`, `SettingDefinition.TryRead`, `ActivitySettingsBuilder`'s
  constructor and `Build`, `MapHarvest`, `MapFight`): a narrower seam would be a task of its own.
- Files over 900 lines (D399): none any more. `FightJudge` (890, from 1,365: its pure turn arithmetic is `JudgeTurns`, its damage tables and landing caches `JudgeTables`, its odds `JudgeOdds`; its score's terms, threat, approach, marks and buffs stay in it, cut only with the replay of the captured fights, `scripts/fights.cs --turns`, compared before and after) and `RouteEditorViewModel` (886, from 1,238: its texts, map scene, step rows, action forms, undo history, path planning, typed anchor, trace and world menu are cut). `AssetsViewModel` (885), `Imagination` (864, its pure fighter helpers are `ImaginedFighters`), `WorldMapView` (848, its hit test is `WorldMapHitTesting`), `BotActor` (723) and `HarvestActivity` (709) are cut. `D3B.Bot` stays one project: its core (`Runtime`, `Activities`, `Banks`, `Movement`, `Journal`, `Characters`) refers to itself both ways, `Harvesting` and `Fighting` are the leaves.
- Undo has two histories: the route editor keeps its own snapshots (D375), the list of steps, the profile and the zone editors use
  `UndoHistory` (D394). Moving the route onto it is a change of no use by itself; do it when the route editor is next opened for another reason.
- The zones are read by the harvest and the hunt as a place (D415) and by their editor; a zone is not yet a filter of the Automatic place, and the nodes and conditions of 7f do not read them.
- `GroupJudge.OddsOf`, `Beats`, `Winnable` and `Share` are called by nothing in `src/` since D387, only by
  `scripts/fights/HuntBench.cs` and their tests. D387 kept them on purpose, for that bench: if the bench is dropped, they go
  with their tests; `Allows` and `Character` are alive.
- `docs/decisions/` is cut in files by group of the index, about 100 KB at most each (D401): read the index, then the file of the area worked on.
