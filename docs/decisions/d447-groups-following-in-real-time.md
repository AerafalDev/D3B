# Groups, following in real time (D447)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The chain is in
[D437](d437-step-10-the-chain.md).

## D447: The followers copy their master's walks as they come, and the master no longer waits after each map change

*2026-11-19, the owner's test in game of main 7350ece5; the master no longer waiting the owner's choice among three
(against the lead's recommendation of followers leaving together), corrected by the owner to following in real time
("faut que le suiveur suit en temps reel le meneur"); each walk copied, and a follower that misses one going by its own
paths in any order, the owner's choices, as the lead recommended them. Supersedes D432's waiting after each map change
and the chain's order, D437's master waiting on each map and its followers leaving one after the other. Builds D74, D87,
D437, D441.*

- **Reported** by the owner: the followers took far too long to follow their master.
- **Measured** on the owner's test (four game connections of 3.7.14.18 through the app from 22:32, a master and three
  followers; `scripts/capture.cs --messages`, type URLs and times only, the captures' starts known to the second): the
  master waited on its map 38 s, then 29 s, before its next map change, while its three followers came one after the
  other, each leaving once the one before it had left and once the master was placed; one follower's walk its client did
  not confirm cost it some 17 s.
- **The master** (`GroupGate`): it no longer holds its walks and map changes. It waits for its group, on its map, in its
  party and its life over the share (D441), only before an action: a use (a harvest, a door), an NPC talked to (the
  bank), a zaap, the haven bag, an attack.
- **The followers** (`FollowActivity`): with their master on its map, each copies its master's walk as soon as the server
  shows it, to the cell the master walks to, once a walk (`BotSeen.Moved` now sees a walk); the master's exit is such a
  walk. Off its master's map, a follower changes maps towards it by the world's paths, which from the cell the master left
  by is the master's way, at once when it copied that walk, else once the master is placed on its new map, as D437
  measured. A walk it cannot make is left: it joins its master's map by its own paths. No order between followers.
- **Not measured**: whether the clients follow walks sent while their master's own walk is shown, which D437 saw fail
  once, as the master changed maps; and how far behind a follower falls when its walk is not followed. The owner looks at
  it in game.
- **Rejected.**
  - The followers leaving together once their master changed maps, the master still waiting on each map: the lead's
    recommendation, which the owner did not want.
  - The followers walking towards the exit as their master does, then changing maps: the owner's correction made it every
    walk, not only the exits.
  - The followers copying only their master's exits: the group stretched across the map, the owner's choice.
  - A follower that missed a walk waiting for its master's next: it could stay behind for good.
