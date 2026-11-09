# Step 9, the harvest's tour and the levels (D417)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D417: The harvest's tour weighs the character's level: a graphic that offers only what it cannot harvest makes no map probable

*2026-11-09, step 9, the debt D416 named, asked by the lead after the owner's words "il visite des maisons alors que no need". Refines D138, D416.*

- **Found.** D416 leaves an interior of a town to the tour only when something says it holds a resource. Graphic 682, drawn in 8 of
  Astrub's houses and learned by the owner's bots as offering the lumberjack's skill 141 (level 90), made those houses probable for a
  lumberjack of any level, and a probable map comes first in a tour. The tour never weighed the character's level, though the server's
  offer on a map does (`MapHarvest.Candidates`: the character's level in the profession against the skill's): such a house was entered
  at each tour, and left with nothing.
- **Measured** on the pack and the owner's 774 learned graphics, for a lumberjack in Astrub's city (sub-area 95, 83 interiors): unweighed,
  8 interiors entered; at level 1 to 89, none; at level 90, the 8 again. Outdoors, 43 maps probable unweighed, 30 at level 1, 43 from
  level 50 on: 13 maps whose known graphics offer only skills above level 1 are no longer sent first.
- **Made.** `HarvestTour.Know` takes the character's state, which the activity gives to the tour of each zone of its rotation at each
  step, so that a level gained counts at the next step, not at the next tour or the next draw of the rotation (D166). A graphic that offers a skill
  of the zone only above the character's level in its profession is no longer `Probable` (`HarvestZone.Foresee` takes the levels) and is
  not known to offer nothing either: the map is `Unknown`, so an outdoor map is gone to after the probable ones, as before. An interior
  needs a probable forecast or a sighting with an element the character can use (`HarvestTour`'s `HoldsAResource`): one seen with only
  what it cannot harvest is left out until the level is gained. A character whose professions the server has not described yet is not
  weighed.
- **Cost.** An outdoor map is never left out for the level: its place in the tour moves, nothing else, so a zone of resources above
  the character still idles through its tours as before. The interiors come back with the level, 8 of Astrub's at 90. The forecast
  is only as good as what the host learned of a graphic: a graphic seen offering only a high-level skill is read so, and a fishing
  graphic shared between seven fish may offer a lower one not seen yet (client.md): the map is then gone to later, not left out.
- **Not measured.** In game. The sightings of a map hold the skill of each element only for a map seen with its elements (`SeenElements`);
  one seen by its resources alone (`Seen`, the tests') is weighed by them, whatever the level.
- **Rejected.**
  - An outdoor map left out when its known graphics offer only what the level cannot: the shrunken tour could end the activity ("no map
    holds a resource") for a character a few levels short, where it idled before, and the graphics know a skill seen, not every one.
  - The time a map was held back as growing weighed by the level too: a change of its own, with nothing measured of it.
  - The levels given once to the tour when it is made: the rotation keeps a tour from draw to draw, through levels gained.
