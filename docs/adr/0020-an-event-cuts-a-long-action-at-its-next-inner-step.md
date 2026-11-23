# 0020. An event cuts a long action at its next inner step, and the action resumes where it was

- Status: accepted
- Date: 2026-11-23
- Decided by: the owner

## Context

ADR 0017's seam puts a safe point between two nodes, but Harvest until and Hunt until last minutes: "Life below 30 %"
would wait out a whole tour, and cutting at once would leave a walk, a use or the bank's window half done in the game.
The catalogue (ADR 0019) makes events the hooks of long actions, so where they cut decides what a copy can do.

## Decision

- **An inner step** is one gesture the game holds until it says it ended: a walk (its arrival), a map change, a use
  (about 3 s for a harvest), a teleport, a dialog or exchange the action opened (closed by it). Harvest until: the use
  or walk at work, or the tour's map change; Hunt until: the walk or map change, a fight never cut; travels: the map
  change or teleport; the bank: once open, the exchange to its end and the window closed; the waits are cut at once.
- **Interrupt then resume** (the default): the event's flow runs after the inner step, then the action runs again
  from where it was, its progress in its node's slot. **Wait for the current action** waits for the whole action.
  **Interrupt and restart** cuts at the inner step, then the main flow starts again. **Parallel** never acts. A safe
  point (settings, memory) is between two nodes or two inner steps.
- **Several events**: priorities 1 to 8, the same one in the graph's order; only a strictly higher one cuts, so at
  most nine flows; a lower one is kept pending once; the same event firing again during its flow is ignored.
- **In a fight** the events wait for its end, pending once (before step 6 the player fights by hand, notified; then
  On my turn is the fight's event); a timer missed while paused, away or fighting fires once on return.
- **A death** is a lost fight, held by "a fight ended" or "died" (D3B's blueprints: "after a lost fight"); a
  blueprint holding neither blocks. **Loops of events** meet the 10,000-node guard, counted across flows.

## Rejected

- Cut at once, cancelling the walk: half-done gestures in the game, which the server holds or refuses.
- Cut only between nodes: "Life below" would wait out a tour; "wait for the current action" already offers it.
- Timers skipped when missed: "At 23:00, the night pause" would be lost to a fight at 23:00.

## Consequences

Each long action of steps 4 to 6 names its inner steps and keeps its progress in its node's slot, tested by an event
cutting it at each step and the action resuming; review checks no action is cut between a request and the game's
answer. The editor limits priorities to 1 to 8. (26) builds the stack of flows on these rules.
