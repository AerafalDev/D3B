# Status

Where the project stands, on one page. Read it first: it says what exists, what is not measured, what comes next and what is
owed. The reasons are in [decisions.md](decisions.md), found through [decisions-index.md](decisions-index.md); the steps and
their sessions are in [roadmap.md](roadmap.md); how the code is built is in [architecture.md](architecture.md). Whoever closes
a session updates this page, the roadmap and the index.

As of 2026-11-08, after D392. App 0.8.1, client build 3.7.13.17, pack format 11, mapping format 17, all published.

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

1. **Session 7d4**: recording while playing and Simplify, the zones and their editor, undo and the keys of the other editors,
   steps reordered by dragging (D376).
2. **7e and 7e2**: the canvas of behaviors, then 7f to 7i (nodes and planning, debugging, the AI's fight preferences, sharing).
   The breeds, set aside since 9f5, come back once these are done (D306).
3. **Then** step 9's remaining mechanisms, and steps 10 to 13 (several accounts, marketplace and crafting, treasure hunts and
   dungeons, the owner's profession scripts).

A measure in game with the owner, of the list above, is worth a session of its own before 7e grows the code further.

## Open questions

Q5 (the fight formulas left: shields, AP and MP removed, erosion), Q12 (what quests, breed and the server's codes gate),
Q16 (casts and walks the server refuses that the rules allow), Q17 (a build that changes its schema). The table is at the end of
[decisions.md](decisions.md#open-questions).

## Known debt

- `D3B.Bot` holds the bots' run time, harvesting, fighting and the studio's model (behaviors, routes, assets, settings): about
  24,000 lines in 365 files. Splitting the studio's model from it is worth deciding before 7e to 7i add the canvas, nodes and
  debugging.
- `D3B.Storage` references `D3B.Bot`, and `D3B.App` reaches `D3B.Bot` only through it.
- Files over 900 lines: `FightJudge`, `RouteEditorViewModel`, `AssetsViewModel`, `HarvestActivity`.
- `GroupJudge.OddsOf`, `Beats`, `Winnable` and `Share` are called by nothing in `src/` since D387, only by
  `scripts/fights/HuntBench.cs`.
- `docs/decisions.md` is about 700 KB: read the index and the entries about the area worked on, never the whole file.
