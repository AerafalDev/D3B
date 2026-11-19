# Groups, the master's exit taken straight (D451)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). Following in real
time is in [D447](d447-groups-following-in-real-time.md), [D448](d448-groups-followers-into-the-fight.md) and
[D449](d449-groups-join-where-they-stand.md).

## D451: To its master's exit a follower walks to an exit of its own, and a character on an exit leaves from it

*2026-11-19, the owner's report after the retest of main 44d57510 ("les suiveurs font encore des détours alors que par
exemple faut juste descendre d'une map"); the lead's fix, local and reversible. Refines D448, D449. Builds D74, D85.*

- **Seen** in the app's log: the master went down one map by a scroll from its exit cell, in one change. A follower
  walked beside it, to a cell next to that exit (D448); to leave, it walked one step onto the exit, which its client did
  not confirm, so the server held it (D85); its step aside was refused, its map change from its cell went unanswered three
  times, and its journey went round, by two other maps (D74). The two other followers changed maps at once.
- **Made**:
  - `FollowActivity`: as its master walks to a cell of the map's edge, a follower walks to a free cell around it that
    leaves the map the same way, each its own, not just any cell beside it.
  - `MapChange`: a character standing on an exit of its map's edge leaves from it, with no walk at all, whatever cell
    its last walk ended on.
- **Not measured**: the owner looks at it again in game.
- **Rejected.**
  - Copying its master's walk to its master's very exit cell: the four walks to one cell D448 measured unconfirmed.
