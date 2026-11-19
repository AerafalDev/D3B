# Fights, the placement's answer (D450)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The group's fights
are in [D440](d440-step-10-the-groups-fights.md).

## D450: The AI waits a second at most for its placement's answer

*2026-11-19, the owner's report after the retest of main c6cc5e25 ("les suiveurs mettent trop de temps à mettre prêt");
the lead's fix, local and reversible. Refines D202, D440.*

- **Measured** on the owner's retest (four game connections of 3.7.14.18 through the app from 00:22, a master and three
  followers; `scripts/capture.cs --messages --frames` and a throwaway reading on the machine printing message names and
  times only): from a follower's fight context to its readiness, about 3 s when it stayed on its cell, 6 s when it moved.
  The server answered a move to a free cell 29 ms after the request, with the dispositions the state reads. A move to a
  cell another follower chose at the same time it answered 31 ms after with another event, not mapped, whose fields are
  two cells and two fighters: a swap. The state did not see the character on its cell, and the AI waited its whole answer
  delay, 3 s, then the fight's pace, 0.8 s, before it said it was ready: 3.8 s, each time. The master said it was ready
  right after its last follower, as D440 wants.
- **Made** (`FightPlay`): the AI waits for its placement's answer 1 s at most, the server's being within 30 ms. A
  character whose answer goes unread says it is ready 1.8 s after its move instead of 3.8 s.
- **Not measured**: the owner looks at the followers' readiness again in game.
- **Rejected.**
  - Mapping the swap's event now: a mapping of format 22 and its publishing, for a wait the cap already shortens.
  - The AI avoiding the cells its allies hold: it already does; the swaps come from two followers choosing one free cell
    at the same time.
