# The harvest hurries rather than wait, its bound from below a quarter over the shortest (D587)

## D587: When every zone of the rotation would wait, the harvest goes to the maps whose resources' shortest time known is past, and the bound from below stays within a quarter over that time

*2026-11-28, outside the roadmap's steps, the owner's report ("the Forgelance miner stops too often"); both corrections the lead
proposed, the owner's choice. Refines D583; builds D145, D391.*

- **Measured** on the owner's journals and `d3b.db` of 28 November: the Forgelance miner waited without a step 53 % of its last
  run (10:27 to 13:42 UTC, 43 silences over 20 s, one of 14 min), harvesting 72 times an hour against 279 on 26 November under
  1.1.2; its 28 November harvested on 26 maps against 103. The other three harvesters waited 0 to 1 % the same day. In `d3b.db`
  the bound from below of nearly every ore skill stood at D583's ceiling or over it: skill 29 reckoned 35 min against a shortest
  of 17.6, skill 31 27.5 against 13.7, skill 30 29 against 14.6.
- **Read in the code.** On a busy server another player harvests the ore once grown back, unseen, and the bot's next visit finds
  it still growing: the bound from below, the longest such time, only rises, to the ceiling, as D583's own risk said. Every map
  of the miner's few high-ore zones was then held back about twice its real time, and half that reckoning more once seen
  still growing, so `HarvestRotation.WakeAt` had the activity wait for ore already back.
- **What was made.** `Regrowth.Ceiling`, 1.25: the bound from below stays within a quarter over the shortest time known, learned
  or kept in the database, so the bounds `d3b.db` holds at the old ceiling read as that much and need no migration. A map seen
  still growing once it should have grown back waits a quarter of its resource's shortest time from that sighting, not half the
  reckoning. When no zone of the rotation has a map ready by the reckoning, `HarvestRotation.Hurry` has every tour hold its maps
  back only until the shortest time known (`HarvestTour.Hurried`) and picks the next zone so; nothing to see even then, the wait
  ends at the first of those times. The next rotation calms the tours again (`HarvestRotation.Calm`): the reckoning orders the
  tours while some map is ready by it.
- **Not measured, tested on the types only**: the miner's waits and harvests an hour once the change runs. To see in the
  journals: fewer silences over 20 s, more harvests an hour, and how many hurried arrivals find nothing.
- **Rejected.**
  - The ceiling lowered alone: the bound still reaches it on a busy server, and the miner still waits for ore a quarter longer
    than it may take.
  - Hurrying alone, D583's ceiling kept: the tours ordered by a reckoning twice too long while some map is ready, the half
    reckoning holding the maps found growing back long after their ore is.
  - The inflated bounds reset in `d3b.db` by a migration: the ceiling holds over a kept bound when read, and on a busy server the
    bound would reach it again within hours.
  - No hold-back after a visit too early: at the ceiling, the map would be ready again at every tour, D583's journeys for nothing.
