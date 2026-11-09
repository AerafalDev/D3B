# Status

Where the project stands, on one page. Read it first: it says what exists, what is not measured, what comes next and what is
owed. The reasons are in [the decisions](decisions/README.md), found through [decisions-index.md](decisions-index.md); the steps and
their sessions are in [roadmap.md](roadmap.md); how the code is built is in [architecture.md](architecture.md). Whoever closes
a session updates this page, the roadmap and the index.

As of 2026-11-10, after D418 (7r5b, the profile as an asset editor), D415 (7r5 part 1, the zones read by the harvest and the hunt), D417 (the harvest's tour weighs the character's level) and D416 (it leaves the houses of a town alone), D414 (hints on the world map), D413 (7r3, the Sequencer) and D408 (the fight AI after the players' reports, D407, summons a last resort; the home page, D406; banking by general settings, D405; D394, session 7d4, merged into 7r2's branch). App 0.8.1, client build 3.7.13.17, pack format 11 published, 12 extracted and not published (D414), mapping format 17.

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
- Step 9's mechanisms: the Roublard against big enemies with `Fights:Alone`, the Féca's glyphs, an Osamodas's summons and the
  fight map's preview, an aggression, stairs and portals, the inactivity popup, what the client shows once its turn's end goes
  unanswered (D367).

## Next

0. **The refactoring and tidy-up session** (D399 to D401) is mostly done: `decisions.md` is cut in files by group of the index,
   `client.md` has its game connection apart, `AssetsViewModel`, `BotActor`, `HarvestActivity`, `WorldMapView` and `Imagination`
   are under 900 lines, BenchmarkDotNet is gone. Left: `FightJudge` and `RouteEditorViewModel`, over 900 lines (debt, below).
0. **Bug reports with the player's capture** (D409 to D411), designed, not begun: the game connection recorded by default, a
   `.d3breport` bundle with the capture cleaned at the export, sent by a private Discord webhook; BR1 to BR4 in the roadmap, BR1
   first. Nothing of it exists in code yet.
1. **Session 7r4, following a run** (D395): the cursor told to the app, the playhead on the Sequencer's ruler, the marker on the
   route, the progress on the Outliner. 7r3, the Sequencer, is made (D413); what it leaves is the map's frame, hovering a column
   lighting its step, a panel taller for the Sequencer, and recording in the toolbar, which waits for the measure in game. Then 7r5b
   (the profile's asset editor, made: D415 and D418), 7r6 sequences and laps; all before 7e.
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
- Files over 900 lines (D399): `FightJudge` 1,258 (from 1,365: its pure turn arithmetic is `JudgeTurns`; what is left is its score's terms, threat, approach, marks and buffs, which share its caches and are cut only with the replay of the captured fights, `scripts/fights.cs --turns`, compared before and after) and `RouteEditorViewModel` 1,044 (from 1,238: its texts, map scene, step rows and action forms are cut; its step actions, planning and the typed anchor are left). `AssetsViewModel` (885), `Imagination` (864, its pure fighter helpers are `ImaginedFighters`), `WorldMapView` (848, its hit test is `WorldMapHitTesting`), `BotActor` (723) and `HarvestActivity` (709) are cut. `D3B.Bot` stays one project: its core (`Runtime`, `Activities`, `Banks`, `Movement`, `Journal`, `Characters`) refers to itself both ways, `Harvesting` and `Fighting` are the leaves.
- Undo has two histories: the route editor keeps its own snapshots (D375), the list of steps, the profile and the zone editors use
  `UndoHistory` (D394). Moving the route onto it is a change of no use by itself; do it when the route editor is next opened for another reason.
- The zones are read by the harvest and the hunt as a place (D415) and by their editor; a zone is not yet a filter of the Automatic place, and the nodes and conditions of 7f do not read them.
- `GroupJudge.OddsOf`, `Beats`, `Winnable` and `Share` are called by nothing in `src/` since D387, only by
  `scripts/fights/HuntBench.cs` and their tests. D387 kept them on purpose, for that bench: if the bench is dropped, they go
  with their tests; `Allows` and `Character` are alive.
- `docs/decisions/` is cut in files by group of the index, about 100 KB at most each (D401): read the index, then the file of the area worked on.
