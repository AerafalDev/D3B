# The harvest, the mines (D444)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D444: The harvest's tour goes into an interior while its sub-area lists a resource no map of it showed, so that it finds the mines

*2026-11-18, outside the roadmap's steps, the owner's report ("the automatic harvest gathers about as much in every profession but the
miner's, much less"); the rule chosen by the owner among four, as the lead recommended it. Refines D416; builds D128, D138, D417.
D440 to D443 are taken by other sessions.*

- **Read in the code.** Since D416, the tour goes into an interior of a sub-area that also holds maps on a world map only once a bot saw a
  wanted resource in it or a graphic it draws is known to offer one; a graphic is learned only from a map a bot entered. Trees, plants,
  cereals and fish grow outdoors, where a map never seen is always gone to; ore grows in mines, which are such interiors. D416 foresaw it
  ("a mine whose graphics no bot has learned is no longer discovered") and left it unmeasured.
- **Measured** on the pack of 3.7.14.18 with tools of the session, never committed, that print counts and the client's ids: 42 maps of
  Amakna's mines are such interiors (Mine Hable in the Carrière d'Astrub, Hipouce, Astirite, Himum, du Volcan, Imale, Estrone, Maksage,
  Uskule), all in the world graph; the mines of a sub-area of their own (Incarnam's, Istairameur, Dopeuls) are not. The miner's sub-areas
  are no more interiors (16 % of their maps in the graph), isolated or out of the graph (12 %) than the other professions' (20 % and 12 %
  for the lumberjack's), and the farmer's, in 34 sub-areas against the miner's 61, was not reported. On 40 of the 42 mine maps, every
  graphic drawn three times or more is also drawn outdoors (iron's 1081 on 42 maps), so a database that learned enough outdoors brought
  them back, a fresh one never; some are rare outdoors (1072 on 2 maps, 2208 on 1). The owner's database on this machine knows 32
  graphics, none of ore, and its captures hold no automatic harvest: nothing of the report was measured in game.
- **Tried and rejected as rules** (same tools): a group of three interiors joined to each other keeps 34 mines and 841 other interiors; a
  graphic drawn three times on the map keeps 21 mines and 127 others. Neither tells a mine from a house.
- **Made.** `HarvestZone.Listed` holds, by sub-area, the wanted resources it lists among its harvestables. `HarvestTour` reckons, at each
  count, the sub-areas that list a resource, at the character's level (D417), that no map of theirs was seen holding nor is foretold to
  hold by a graphic learned anywhere; in those, an interior whose forecast is unknown is gone through, last, after the probable maps and
  the maps of the world map. One foretold to hold nothing, a map without elements included, stays out. Once a bot saw the resource, its
  graphics are learned and D416's rule holds again: the houses of Astrub stay out once every resource Astrub lists at the character's
  level was seen outdoors. A map the tour leaves out as isolated (D149) finds nothing, since the tour never goes there.
- **Cost.** In such a sub-area, its houses are entered too while the resource was seen nowhere, once each, a map seen empty being left
  out next; the sightings live as long as the app (`HarvestSightings`, one per host), so a sub-area whose list names a resource that
  does not grow there has its houses entered once each time the app starts. Measured on the pack: the sub-areas with such interiors
  that list a profession's resources are 71 for the lumberjack (881 interiors), 36 for the miner (388), 85 for the alchemist (1,111),
  22 for the farmer (330) and 56 for the fisherman (820); Astrub's city lists 17 resources, none an ore, for its 83 interiors. How many
  of these lists name a resource no outdoor map holds, only a measure in game says; the automatic profile, which wants every
  profession's, is the most exposed.
- **Not measured.** In game: an "Auto miner" and an "Auto lumberjack" profile, 15 minutes each on the same database, the ore harvested,
  the mines entered (Mine Hable from the Carrière d'Astrub), the houses entered, and the automatic profile's harvests by profession.
- **Rejected.**
  - The interiors gone into whenever the zone wants ore: the automatic profile wants it everywhere, and D416's report would come back.
  - The owner's learned graphics shipped with the app as a release asset: a new asset to keep for each client build, and a resource no
    bot saw stays unfound.
  - D416 kept as it is: the miner stays behind on a fresh database and for an ore rare outdoors.
