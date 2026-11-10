# Step 7, 7r4, following a run (D419)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D419: 7r4, following a run: the run publishes where it is in `RouteRuns`, the app reads it, and a column lit is the playhead

*2026-11-10, step 7, session 7r4, the lead's, within the owner's choices of D396 (the engine, the Sequencer, the order of the sessions).
The owner answered five questions one at a time, each as the lead recommended: the seam, what the playhead is between two steps, several
characters on one route, the scope, and how the trial in game is handled. Builds D295, D379, D386, D393, D395 to D397, D413.*

- **The seam (the owner's first choice).** A run says where it is through `RouteRuns` in `D3B.Studio`, which the app reads every 250 ms as it
  reads the bots; `D3B.Bot` does not learn routes (D393's border). `FollowRoute` registers the run when the route begins to play
  (`RouteRuns.Begin`) and takes it off when the route ends, is cut or fails (`End`, in a `finally`); `Position` and `Positions` give a
  `RouteRunPosition` per character: the route, its step (from 1), whether the character is on its way or arrived, the actions played of
  the step's, the lap of a loop, and whether a route there and back is on its way back. The cursor outlives the run as before (a fight or a
  bank goes on from where the route was cut, D379), but is *followed* only while the route plays, so a cut route shows as the fight or the
  pause the card already says. `RouteCursor` learned to say it has arrived (`Arrive`, `Depart` at each journey, `Advance` clears it), and
  hands its place in one reading (`Snapshot`, under a lock, since the run writes it on the bot's thread and the app reads it on the UI's).
- **A list of steps says its step** through the existing `ActivityReport` (`BehaviorActivity.Report.Progress`, from `StateVisit.Place`):
  a state that is a plain chain of actions of at least two steps, with no branch, reports "step 2 of 5" and the step last begun, counted
  from 0 so that the card's "2/5" and the run block's bar read it unchanged; a state with a branch, or of a single step, reports none.
  A behavior that makes a harvest or a hunt by an activity node keeps that activity's report.
- **The playhead is a column lit, not a marker between columns (the owner's second choice).** The step gone to, outlined dashed while the
  character is on its way, filled with a bar and the character's initials once it has arrived; nothing is guessed from the time of a
  walk, which a zaap, a wait or a fight would make wrong. On the map the same step has a ring in the character's colour, hollow then filled,
  and, while the character walks there, the leg from the step before (a loop's first step comes from its last, a route going back from the
  step after), dashed. The character's own disc stays where it really is.
- **One marker for each character (the owner's third choice).** `FollowedRuns` (the app's, built from the bots' `Route`) reads every
  character that follows a route; the map marks each on its step (several on one step stand side by side), the Sequencer has a playhead
  each, the Outliner's route row lists who is on it and its step rows carry their initials. A route hidden by its eye, or a character
  hidden, leaves its markers off the map.
- **What each part says.** The card's step, the Outliner's character row and the Home page read `BotViewModel.Step`: "North · step 4/8 ·
  lap 3" (a loop, from 1), "North · to step 4/8" while walking there, "· back" on a route there and back; the run block's detail says it
  after the activity's name; the viewport's frame says "Ayuwoki plays · step 4/8 · lap 3"; the Sequencer's toolbar says "Ayuwoki · step
  4/8" in green beside the selection's status (a polite live region) and offers "Follow the run", on by default, so that the ruler scrolls
  to the playhead when it moves (`BringIntoView`: a column already in sight stays put) and the user can turn it off to read elsewhere.
- **Scope (the owner's fourth choice).** Two things 7r3 left came with it. Hovering a column of the Sequencer lights its step on the map
  (`SequencerViewModel.Hover`, `StepLit`, `WorldMapScene.LitStep`, drawn as a hovered node). And the run writes a line in the journal each
  time it reaches a step, "Route North: step 4 of 8, map 123.", **in the first lap of each run only** (`IRouteStage.Reached`,
  `RouteRunner`: `cursor.Laps` is 0), so that a loop that goes on for hours does not fill the journal; a route one way or there and back
  has every step said. The line does not name what the step does: the actions are in the route.
- **The trial in game (the owner's fifth choice).** The code and its tests are delivered and 7r4 is **done in code, to try in game**, as
  7d3c and 7d3d are; the measure already owed (D384, D386) stays the same one, and this decision adds to it the list below.
- **What the owner is to look at, on a client.** (1) A route of one lap: the column of step 1 is hollow while the character walks, filled
  when it is there, and the journal says "step 1 of N" once on arrival. (2) A route there and back: "· back" after the last step, and the
  leg drawn from the step after. (3) A route through a zaap: the leg dashed from the step before to the one the zaap leads to; whether
  the step stays hollow for the whole trip. (4) A loop cut by a fight and resumed: the card says the fight; the playhead leaves while the
  route is cut and comes back on the same step, not the first; the lap does not restart. (5) A long action (a harvest, a door, an NPC): the
  playhead stays filled on its step for its whole time. (6) Two accounts on one route: two markers, two playheads, each with its own
  initials and colour; the Outliner row lists both. (7) How a reading every 250 ms against a map every three seconds looks, and whether
  "Follow the run" scrolls a long ruler as expected. The journal's line is what lets the owner compare afterwards.
- **Not measured, and not seen.** Everything is tried on the fake game (a route of one map, the wait action, `RouteRunner` on a stage
  that logs) and on view models and headless windows (a window drawn with the character following a route, read as an image); not in the
  running app, not on a client. The ring and the dashed leg at several zooms, a screen reader on the ruler's status, the badge when many
  characters stand on one step, and the cost of reading every character's position at each tick were not looked at.
- **Left.** The steps already passed this lap dimmed on the map (the mockup draws them); an interpolated marker between steps (the owner
  chose the column); a character's run kept as "cut" while a fight plays (the card says the fight, the route's marker leaves); the
  actions a step plays named in the journal's line; the map's marker for a run on a route of another world than the one shown (it is
  left off while its world is not the layer shown); the Outliner's route rows unfolding by themselves to show the step.
- **Rejected.**
  - A route's position in `ActivityReport`: `D3B.Bot` would know routes, against D393's border (the owner's choice).
  - The journal's events as the way the app learns the position: the app would rebuild the position and lose it when it opens late (the
    owner's choice).
  - A marker moved between columns by an estimate of the walk's time: it would be wrong at each zaap, wait or fight (the owner's choice).
  - Only the character chosen in the run block followed: the others would run unseen on the map and the Sequencer (the owner's choice).
  - The journal's line at each lap: a loop of hours would fill the journal (the owner's choice).
  - The position kept in the cursor alone: it outlives its run, so that it could not tell a route playing from one remembered.
