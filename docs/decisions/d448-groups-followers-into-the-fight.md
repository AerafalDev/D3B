# Groups, the followers into their master's fight (D448)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The group's fights
are in [D440](d440-step-10-the-groups-fights.md), following in real time in [D447](d447-groups-following-in-real-time.md).

## D448: The master waits for its followers on the map its fight began from, and they walk beside it

*2026-11-19, the owner's retest in game of main 31984836 ("les suiveurs ne rentrent pas dans le combat du meneur", then
one of three); the cell beside the master the owner's choice between two, as the lead recommended it, the rest the
lead's fix. Refines D440, D447. Builds D85.*

- **Measured** on the owner's retest (four game connections of 3.7.14.18 through the app from 23:50, a master and three
  followers; the app's log; `scripts/capture.cs --messages --frames` and a throwaway reading on the machine, which printed
  option names and map comparisons only, never a value of the account):
  - The master's fight was locked to its party and refused spectators, as D440 wants. But the server names the fight's
    own map as the fight begins, and its id is not the map's the fight began on, in every fight. So the master, comparing
    its followers' maps with its fight's, found none on it and said it was ready at once (D440's flaw, which its tests,
    the fight on the same map, missed).
  - The master and its three followers walked to one cell at once, the followers copying their master's walk (D447):
    none of the four clients confirmed its walk, and the server held each character there (D85). The owner saw it: moved
    by hand, they came back to their cell. A held follower's request to join the fight went unanswered.
- **Made**:
  - A fight keeps the map it began from (`FightState.FromMapId`), and the master waits for its followers there.
  - Each follower walks to a free cell beside the one its master walks to, its own by its place in the party, by its own
    path. The owner's choice: they follow their master, not its exact cells.
  - A follower whose walk the server holds steps aside first (D85), then joins the fight.
- **Not measured**: whether the clients confirm walks to cells beside their master's; the owner looks at it in game.
- **Rejected.**
  - The same cell as the master, the step aside added alone: the walks would still go unconfirmed, the followers held.
