# Groups, the master's door (D453)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). Following in real
time is in [D447](d447-groups-following-in-real-time.md) to [D451](d451-groups-direct-exit.md).

## D453: A follower does not copy its master's walk to a door or a cell that moves it, and takes that way itself

*2026-11-19, the owner's report after the retest of main 8291c6a7 ("une mule est restée longtemps devant la banque avant
d'entrer"); the lead's fix, local and reversible. Refines D447, D449. Builds D85.*

- **Seen** in the app's log: as their master walked to the bank's door, a follower walked beside it (D448), then needed
  a walk more onto the door's own cell, which its client did not confirm; the server held it (D85), its step aside was
  refused, and it took the door long after the others.
- **Made** (`FollowActivity`): a master's walk ending on the cell where a door is used, or a cell that moves the character
  to another map, by the world's graph, is its master's way out, as a walk to monsters is its attack (D449); the followers
  copy it not, and take that way themselves once their master took it, by the world's paths.
- **Not measured**: the owner looks at it again in game.
- **Rejected.**
  - Copying the walk to the door's cell itself: four walks to one cell D448 measured unconfirmed.
