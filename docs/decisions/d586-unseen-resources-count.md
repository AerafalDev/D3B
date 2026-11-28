# The automatic place, the resources no bot saw (D586)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D586: The automatic place counts a resource no bot saw on a sixth of the maps of the sub-areas listing it, until one sees it

*2026-11-28, outside the roadmap's steps, the owner's report ("in the harvest's tours many resources are ignored, rice or
silicate"); option A chosen by the owner among three, as the lead recommended it. Refines D166, D391; builds D138, D264, D444.*

- **Measured** on the owner's 125 game captures of 20 to 28 November, replayed through the bot's own state with tools of the
  session, never committed: on the 49,006 visits a bot harvested on, it left a few percent of what it could harvest at its
  levels, wheat the most (1,128 left against 7,760 taken). Silicate and dolomite were offered on no map visited, enabled or
  not; rice was offered 28 times on 8 maps, each time to the fisherman. Nothing is ignored on a map: the bots never go where
  those grow.
- **Read in the code.** The ranking (D166) counted a resource only on the maps a learned graphic places it on, and a graphic is
  learned only from a map a bot entered (D138), so a resource no bot saw counted for nothing and no sub-area was chosen for it;
  the sub-areas' lists counted only while nothing the character can harvest was placed at all. D444 broke the same circle inside
  a zone, for the mines, not in choosing the zone. The owner's `d3b.db` knows 947 graphics, none of 21 harvest resources:
  silicate (miner of 162), the pine (lumberjack of 100), the frostiz and the quisnoa (farmer of 200) at the characters' levels.
- **Measured** on the pack of 3.7.14.18 with those graphics: a learned resource is placed in the median 100 % of the sub-areas
  listing it, on 17 % of their maps (median; tin in 16 of 18, iron, silver, manganese one short). D166 rejected the lists for
  saying nothing of how many grow: where they grow they say surely, and how many is reckoned. « Test conventionné », ten maps
  in the world graph, lists 85 resources, every other sub-area 23 at most.
- **Made.** A resource no learned graphic places anywhere counts, on each map of the sub-areas listing it, 0.17 of its worth
  (`AutomaticPlace.UnseenShare`), the isolated maps out as before (D264), a sub-area listing over 40 left out
  (`AutomaticPlace.MostListed`); it gives a profession's top too (D391). Once a bot sees it, its graphics are learned and only the
  maps they are drawn on count. The fallback for nothing placed is gone, this covering it.
- **What it gives**, reckoned on the same pack and graphics: the farmer of 200 goes first to the Champs de glace (27.4, against
  22.0 for the Dents de Pierre), now its top for the frostiz; the quisnoa's sub-areas rank 4th and 5th. The lumberjack's
  Forêt des pins perdus, for the pine, ranks 13th (26.6), out of the ten its rotation is drawn from. Silicate stays out for the
  miner of 162: an ore 62 levels below counts 0.12 of its worth, Terrdala about 0.7 against 7.1 for the first, as D166 wants.
- **Not measured.** In game: the farmer's tour of the Champs de glace, its graphics learned, and how often a sub-area's list
  names a resource the estimate sends a bot to for nothing.
- **Rejected.**
  - The profession's top alone aiming at its highest resource, seen or not: only the highest of each profession is found.
  - A rotation's place for every resource no bot saw at the character's level, until seen: journeys far off for resources the
    ranking would then weigh at little.
  - Weighing a low resource more, so that the miner of 162 goes for silicate: the halving every 20 levels is D166's choice, and
    a list profile harvests silicate now.
