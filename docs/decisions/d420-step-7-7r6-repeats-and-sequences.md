# Step 7, 7r6, repeats and sequences (D420)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D420: 7r6, a step of a list repeats (once, a number of times, until a condition) and plays another list: the repeat is on the step

*2026-11-10, step 7, session 7r6, the lead's, within the owner's choices of D396 (the engine, the words kept). The owner answered three
questions one at a time, each as the lead recommended: where the repetition lives, what "until" reads and when, and how far blocks go.
Builds D295, D296, D297, D298, D307, D378, D379, D386, D395 to D397, D413, D419.*

- **What the code and the files held already.** A route's `Mode` (loop for ever, one way, there and back), `RouteCursor.Laps` read only,
  a list's one boolean `repeats` (for ever or once), `route.follow`, the conditions of the catalog (D298, used by a state's `When`, with no
  editor in the app) and a compiler that refuses a cycle on the way of execution (D87). A behavior as a node of another (D296) was not
  built. The mockup draws nothing for 7r6.
- **The repeat is on the step (the owner's first choice).** `StepRepeat` (`RepeatKind`: once, a number of times from 2 to 999, for ever,
  until a condition) is a property of a graph node (`GraphNode.Repeat`, `NodeCall.Repeat` for a list's step) and of a state
  (`BehaviorState.Repeat`, replacing the boolean). The file keeps the old flag as it was: `"repeats": true` reads and writes as for ever,
  so that no file changes; the others are `"repeat": {"times": 3}`, `{"until": {"node": ..., "values": ...}}`, `{"forever": true}`, refused
  when they say nothing useful. A pass is the step played to its end: a route once through its course, a sequence once. A route stays what it
  does in one pass: its `Mode` says what a pass is, and a loop followed by a step that repeats goes round once for each pass (a cursor of one
  lap, `RouteRuns.Of(..., pass)`, which says which lap it is, so that the card, the viewport and the journal read as before: said in the
  first pass only); a loop that no step repeats goes on for ever, as it did. The canvas of 7e draws the same property.
- **"Until" is one condition of the catalog, read before each pass (the owner's second choice).** Pods, life, level, a profession's level,
  kamas, items, a sub-area, an area, the last fight, the run's length (`BehaviorSequences.OfferedConditions`: those that read the
  character and need no variable), no and/or/not (7e's). It is read before each pass, the first included (Scratch's "repeat until"): already
  true, the step is passed or the list plays nothing; never in the middle of a pass, so a lap of forty maps is not cut. It is compiled by the
  behavior's own compiler (a wrong node or value is a problem where it is) and read by `StateVisit`, which counts the passes of each step
  and of the list.
- **A pass of a step that repeats ends the step**, instant or not, so that a loop of instant steps gives the bot's loop its turn. A step
  whose goal the world says reached is still passed. The memory is `StateVisit`'s, as before: what was played to its end, and now how
  many passes, so that a run cut by a fight resumes on the same pass.
- **Blocks are sequences played as steps of sequences (the owner's third choice).** The node `sequence.play` (`PlaySequence`, one setting
  `Sequence`, `SettingKind.Sequence`, a behavior's id picked in a dialog, never typed) plays a list of the project as one step: its
  steps in order, once for each pass of the step, so that "Bank × 2" and "Harvest until pods at least 90 %" are one step each. The list is
  compiled and walked by a `StateVisit` of its own (`SequenceRuns`, as `RouteRuns` is for a route), which outlives a cut: a fight or a
  bank goes on from the step the list was at, and a character that starts again begins over. The list's own repeat is left to the step that
  plays it. A list that plays itself, directly or through others, ends the run with a note; the editor does not offer a list that would
  play the one it edits (`BehaviorSequences.Reaches`), and `BehaviorAsset.Uses` lists the lists played as it lists the routes followed, so
  that "used by" and a deletion say so. A route's map steps do not play blocks: their actions depend on the map.
- **The editor.** Under each step of a list and under the list a token says how it repeats ("once", "3 times", "until pods at least 90 %",
  "for ever" for the list only) and opens its choices where it stands (`StepRepeatViewModel`, `StepRepeatView`); the condition of an
  "until" is a sentence with tokens edited as a step is (`ActionSentenceView`, taken out of the step's card), chosen in a menu of the
  catalog's sentences; one that lacks a value blocks Save. It is one change to undo, as every edit of the list.
- **Not measured, and not seen.** Everything is tried on the fake game and in view models and headless windows: a loop followed lap by lap,
  a step until a condition read at the bot's loop, a list played inside a list, a cut by a fight in the middle of a pass. Not on a client: the
  measure owed since D384 and D386 grows by these, to look at one by one: (1) a route followed three times, the journal saying the steps in
  the first pass only; (2) a loop followed until pods at least 90 %, and whether a lap long enough overshoots; (3) a list played twice with a
  fight in the middle of its second step, and the run resuming on that step; (4) a list played inside a list that follows a route, the
  playhead of the route still shown; (5) the flyout of the repeat token and of the condition, which the headless window cannot open, and
  the token's look under each step; (6) a long action (a harvest, a door) inside a block, interrupted by a bank.
- **Found by the review, and kept.** A route that comes back `Busy` after ten tries ends the node, so the pass counts as played, as a step with
  no repeat always did: `Follow R × 3` that meets it spends a pass without a lap (to decide after the measure). A repeat on a node that is
  no action of a state's list (an activity, a rule's node) is read and written but not played, and no problem says so: the list editor never
  offers one, the canvas of 7e will. A pause or a stop inside a played list cuts the step as it cuts any, so that a resume goes on in the list.
- **Left.** The route's own "followed by": no sign on a route of the lists that repeat it; the pass of a repeated one-way route on the
  card (only a loop says its lap); the condition read between two steps of a route; and, or and not in an "until" (7e); a list with
  parameters played as a block (`BehaviorAsset.Parameters` make it a canvas); a block of actions inside a route's step; `SettingKind.Sequence`
  in the profile's form; undo of a picked list is one change as any value. 7e, the canvas, is the next row.
- **Rejected.**
  - Laps and "until" on the route's asset: one route could not make three laps here and one there, and a second mechanism would repeat a list.
  - A container "Repeat" step wrapping other steps: the list becomes a tree, and the editor, `BehaviorSequences` and the canvas change; a sequence
    played as a step already is the container.
  - A fixed short list of "until" kinds: two lists of conditions to keep, and nothing for the nodes 7f adds.
  - Expanding a played list into the steps of the other at compile time: a repeat or an "until" cannot be said in a flat chain, and the compiler
    refuses a cycle on purpose.
  - The condition read between two steps of a route, in this session: it would go down into `RouteRunner` and ask what a cut in the middle of a lap
    keeps; to do after the measure.
  - A new kind of setting for the condition: the repeat is no value of a node, it is a property of the step, and its condition is a node call.
