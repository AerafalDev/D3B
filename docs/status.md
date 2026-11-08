# Status

Where the project stands, on one page. Read it first: it says what exists, what is not measured, what comes next and what is
owed. The reasons are in [decisions.md](decisions.md), found through [decisions-index.md](decisions-index.md); the steps and
their sessions are in [roadmap.md](roadmap.md); how the code is built is in [architecture.md](architecture.md). Whoever closes
a session updates this page, the roadmap and the index.

As of 2026-11-09, after D395 (D394, session 7d4, is on its own branch, not merged when D395 was written). App 0.8.1, client build 3.7.13.17, pack format 11, mapping format 17, all published.

## What exists

- **A bot that plays through the official client** (D23): the relay between the client and Ankama's servers, the readable
  protocol and its per-build mapping, the data pack extracted from the client (steps 1 to 3).
- **The run time** (step 4): one bot per character, walking, changing maps, using elements, banking, zaaps and the haven bag,
  activities, recovery.
- **Harvesting and the hunt** (steps 6 and 9): both choose their place by the character's levels and go through several
  sub-areas in turn (D391, D392); fights are played by one AI for every breed (D244), with the rules and effects of a fight
  measured on the owner's captures.
- **The desktop app as a studio** (step 7, up to 7d3d): dark, docked, the characters as cards, the project's assets in files,
  behaviors and profiles as variants, the world's map drawn from the client's images, routes with their actions, and a profile
  playing a list of routes and steps made in the app (D386).
- **Published**: the installer and updates through Velopack (D171); the client build's pack, schema and mapping as the release
  `client-3.7.13.17` (D365).

## Not measured in game

- The routes' actions that talk to the game and the profiles playing a list of steps (7d3c, 7d3d): tried on the test game
  only; a real client with the owner is to do.
- The owner's reports from game answered since 2026-11-06 (D387 to D392): the hunt attacking every group, the tour leaving
  interiors out, the AI fleeing less, the rotation through ten sub-areas and the professions in turn. The numbers 5, 10, a
  quarter and 50 of D391 and D392 are the lead's reckoning.
- Step 9's mechanisms: the Roublard against big enemies with `Fights:Alone`, the Féca's glyphs, an Osamodas's summons and the
  fight map's preview, an aggression, stairs and portals, the inactivity popup, what the client shows once its turn's end goes
  unanswered (D367).

## Next

1. **Session 7r2, the engine's frame** (D395, D396): the studio redesigned as a game engine (`docs/mockups/studio-7r.html`), the owner
   chose the engine and the Sequencer for a route's steps; the world viewport as the centre with its tools, the Outliner, the Details
   panel, Play in Editor. Nothing of 7r is coded yet. The words stay (profile, route, sequence); still open: the order of 7r2 to 7r6.
   It comes before 7e, so that the canvas does not grow on a design the owner finds impractical (a route needs each step set by
   hand; running it takes about fourteen gestures in four editors).
2. **Session 7d4** (D394, its own branch, commit `f0c0cd2`): undo and the keys of the other editors, steps dragged, zones and their
   editor; recording while playing and Simplify (7d4b) wait for the measure in game and then take their place in 7r3's workbench.
3. **7e and 7e2**: the canvas of behaviors, then 7f to 7i (nodes and planning, debugging, the AI's fight preferences, sharing).
   The breeds, set aside since 9f5, come back once these are done (D306).
4. **Then** step 9's remaining mechanisms, and steps 10 to 13 (several accounts, marketplace and crafting, treasure hunts and
   dungeons, the owner's profession scripts).

A measure in game with the owner, of the list above, is worth a session of its own before 7e grows the code further.

## Open questions

Q5 (the fight formulas left: shields, AP and MP removed, erosion), Q12 (what quests, breed and the server's codes gate),
Q16 (casts and walks the server refuses that the rules allow), Q17 (a build that changes its schema). The table is at the end of
[decisions.md](decisions.md#open-questions).

## Known debt

- `D3B.Bot` is about 17,900 lines in 224 files and `D3B.Studio` about 6,300 in 141 (D393). The border is the compiler's, one way:
  `D3B.Studio` references `D3B.Bot`, which knows nothing of it. Left over: `D3B.Studio.Routes` reaches the harvest and the fight
  through `MapHarvest` and `MapFight`, now public, so a module of `D3B.Bot` cannot declare nodes (the node API is the studio's):
  a future module in a project of its own can. Nine members and types of `D3B.Bot` became public so that the studio could use them
  (`BotContext.Actor`, `BotActor.Time`, `Log`, `PauseAfterStep`, `SettingDefinition.TryRead`, `ActivitySettingsBuilder`'s
  constructor and `Build`, `MapHarvest`, `MapFight`): a narrower seam would be a task of its own.
- Files over 900 lines: `FightJudge`, `RouteEditorViewModel`, `AssetsViewModel`, `HarvestActivity`.
- `GroupJudge.OddsOf`, `Beats`, `Winnable` and `Share` are called by nothing in `src/` since D387, only by
  `scripts/fights/HuntBench.cs` and their tests. D387 kept them on purpose, for that bench: if the bench is dropped, they go
  with their tests; `Allows` and `Character` are alive.
- `docs/decisions.md` is about 700 KB: read the index and the entries about the area worked on, never the whole file.
