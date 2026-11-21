# Movement and the harvest, a map action's cell crossed (D487)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The map's actions in
the world graph are [D71](d064-d090-step-4.md) and its steps; the harvest's tour is [D128](d126-d175-step-6.md); the
inactivity kick, [D481](d481-harvest-losses-go-on.md) and [D484](d484-busy-wait-bounded.md).

## D487: A walk goes round the map's action cells, a harvest the server cuts by moving the character leaves its map gone through, and a tour going round ends

*2026-11-21, the lead's task: another player reported a character stuck, then disconnected for inactivity, and gave the
owner two captures. The session measured the loop and settled the answer, a bug fix, within the task's scope. Builds D27,
D71, D128, D145, D425, D439, D444, D481, D484.*

- **Measured** on the player's two game captures (structure only: `scripts/capture.cs --messages` and scratch probes printing
  ids and counts, never committed), client 3.7.14.18, mapping 24, pack 13. The first, 43 min: from 25:08 to its end, 58
  identical rounds of 18.5 s through eight maps of a mine of sub-area 525, 168034304, 306, 308, 310, 312, 168167424,
  166912, 165888, by five doors, one map action and two scrolls, the world graph's one-way ring (306 leads to 308 only by
  the map action of cell 341); no harvest after 25:08, though copper grew back on 168034310 in 13 rounds.
- **The cause**, read from the captures, the pack and the code: on 168034306 the profile's ore 507630 (item 350, skill 29,
  level 120; the character's miner 130) is used from cells 284, 297 and 311. The harvest's walk from the door's cell 457
  to 297 crossed cell 341: the server moved the character to 168034308 at once, the walk refused there, every time. No use
  was asked, so the activity's step neither harvested nor went through the map, and what it left skipped was forgotten on
  the next map. The tour never went through 168034306, its last probable map, and never ended: each round a journey of 7
  changes back to it, passing 310 and 312 already gone through, the journey's stop for a resource offered (D392) asking
  only maps not gone through. Every journey arrived, so no counter of failures moved, and the dry tour's wait (D128,
  D145) comes only once a tour ends. A path to the ore round cell 341 exists. The second capture, 3 h 09, holds the same
  loop for 3 rounds, ended the round the ore was seen harvested by another.
- **Made, the walk**: `WorldPathfinder.ActionCells` gives a map's map action cells, read once per pack from the graph,
  whatever their criteria. `CellPathfinder` takes cells to avoid, never stepped on unless asked for or started from, and
  every walk out of fights (`RoleplayPaths`: walks, steps aside, the way to an element, a door or a map's exit) goes round
  them when it can, else crosses them as before.
- **Made, the harvest**: a use whose walk the server cut by moving the character to another map leaves the map it began on
  gone through for the tour, or crossed when it lies out of the zone (D439). A tour whose journeys arrive three times in a row with nothing done since the one before (no
  harvest, no map gone through, left out or crossed, no zone entered) goes round: its other maps and zones are given up for
  this tour, which ends as any other, the dry tour's wait or the next zone of the rotation then following. The journal
  says so (`TourGoneRound`), and the character's card warns until the next harvest.
- **Not measured**: whether a map action whose criterion the character fails moves it too; whether every map action's cell
  acts on a walk that only crosses it, measured on this one map (61 times in two runs).
- **Not found in the captures**: why the server closed the first connection. At 42:43.8 the client's confirmation of a walk
  on 168034310 got no answer, nor D3B's five walks over the next 20 s, then the server closed it first; nothing it sent
  before says why (its 29 texts after 25:08 are all text 65, a message), and 18 minutes of a door used every 3.7 s is not
  the 30 minutes of inactivity D481 measured.
- **Rejected**: going round action cells only, the harvest left as it was: another cause of a map never gone through would
  loop the same way. A watchdog alone: the ore would never be harvested, and a tour going round for three rounds each time.
  Stopping the activity when a tour goes round: the rest of the zone, here 186 maps outside the mine, still holds
  resources, and a stopped activity idles into the server's kick (D481). Counting dry tours to leave a zone: a contested
  zone has dry tours that end, waited for, which is not this loop; the owner's to decide if wanted. Never crossing an action
  cell, with no fall back: a map whose only way to a cell crosses one, through a criterion the character fails, would lose
  its walks.
