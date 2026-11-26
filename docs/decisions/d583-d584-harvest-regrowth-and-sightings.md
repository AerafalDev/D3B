# Harvest, a resource found still growing, and what each map holds kept (D583, D584)

## D583: An element a bot harvested, found still growing on a later visit, teaches a bound from below, never over twice the shortest time known, and holds its map back half the reckoning more

*2026-11-26, a session of its own, the lead's study of the owner's journals and captures of 26 November (app 1.1.2); the
ceiling the owner's choice, as the session recommended it. Refines D145 and D262.*

- **Measured** on the owner's journals of 26 November: of the Forgelance miner's 148 arrivals where nothing was harvested in the
  14:29 UTC session, 64 were on maps it had itself harvested 5 to 30 min before, 46 of them 18 to 30 min; 129 in the 08:39
  session, about 19 min of journeys. In `d3b.db`, six ore skills had no growing back seen whole and were reckoned from the least
  bound from above alone, 10.9 to 18.4 min; skill 25 was seen whole at 18.4 min beside a bound of 11.4 min, so one resource does
  not always take the same. The journal's time between two harvests of one element: at least 13 to 22 min, a median of 27 to
  48 min, for every ore skill.
- **Read in the code.** `Regrowth.Reckoned` was `Longest ?? AtMost`, and `AtMost` only shrinks, so the fastest growing back seen
  became the reckoning. A visit that found an element still growing taught nothing, and `HarvestTour.ReadyAt` kept the harvest
  plus that reckoning, already past: the map was ready again at every tour until it grew back.
- **What was made.** `RegrowthLearner` reads, in a map's information, each element a bot saw harvested still not offering its
  skill: `Regrowths.StillGrowing` keeps the longest such time as `Regrowth.AtLeast`, at most twice the shortest time known of the
  resource (`AtMost` or `Longest`), and nothing for a resource no time is known of. It is kept in `d3b.db` beside the other
  bounds (`Regrowths.AtLeastMilliseconds`). The reckoning is the longest seen whole, else the least bound from above, raised to
  the bound from below. A map whose element was seen still growing once the reckoning said it should have grown back waits half
  the reckoning from that sighting, the very one whose visit taught the bound too; a sighting before that time still changes
  nothing (D262). The ceiling holds in the reckoning as well, over a shortest time learned after the bound.
- **The ceiling.** Another player may have harvested the element once grown back, unseen, so that a bound from below is no fact;
  kept in the database, a bound without a ceiling would rise with each such harvest and hold the bots back for good. Twice the
  shortest leaves room for skill 25's 11.4 to 18.4 min, and an unseen harvest at most doubles a wait.
- **Not measured, tested on the fake game and the types only**: the miner's empty arrivals once the bound is learned. To see in
  the journals: fewer arrivals on maps it harvested 10 to 30 min before, and the ore skills' `AtLeastMilliseconds` in `d3b.db`.
- **Rejected.**
  - A bound from below without a ceiling, the study's: the unseen harvests of other players would raise it for good.
  - The map held back alone, nothing learned of the resource: one or two journeys for nothing at every growing back still.
  - The tour waiting a whole reckoning after an early visit: with a reckoning too short by half, as measured, half more
    finds the ore grown back sooner, and the bound from below lengthens the next.

## D584: What each map holds, its elements that gather a resource, is kept in D3B's database and taken back at the host's start, not whether they offered their skills

*2026-11-26, the same session, the lead's study. Supersedes D128 on keeping nothing of the sightings from one run to the next.
Builds D138, D145.*

- **Measured** on the owner's journals of 26 November: in the 14:29 UTC session, 227 arrivals where nothing was harvested were on
  maps the same bot had found empty in an earlier session of the day (the Forgelance 14, the Sacrieur 60, the Huppermage 101, the
  Pandawa 52), about 20 min of journeys over the four. Those a memory spares are the maps that showed no element of a wanted
  resource: 56 of the Huppermage's 224 empty arrivals, 47 of the Forgelance's 148, 30 of the Sacrieur's 129.
- **What was made.** `HarvestSightings` tells each map whose elements changed (`Learned`), not an element harvested or grown
  back, and a `SightingRecorder` writes it, as the graphics' and the growings back's recorders do: a table `Sightings`, one row a
  map, its elements as JSON (element, skill, resource), none for a map that holds none. Taken back before the host starts
  (`HarvestSightings.Remember`), each element as offering its skill, so that a tour goes through a map it holds a resource on, and
  leaves out one seen holding none of its resources, from the run's first tour.
- **Not kept.** Whether each element offered its skill: it changes at every harvest, and a run that starts hours later cannot
  trust it, so a restart costs at most one journey to a map still growing; nor the harvests a bot saw, in memory as before.
- **Risk.** A map kept as holding none of a tour's resources is left out of its tours for good, across the client's builds: only
  a journey that crosses it sees it again. A build that adds a resource to such a map would go unseen; none is measured. A
  sighting taken between a map change and the map's information, its elements not given yet, would be kept empty: the harvest
  does not step then, the activity getting ready until the information comes (seen on the fake game).
- **Rejected.**
  - Keeping each element's state and since when it is growing: a write at every harvest for one journey a map after a restart.
  - The sightings forgotten after a time: a map's elements are the pack's, and a bot that enters it writes what it finds.
