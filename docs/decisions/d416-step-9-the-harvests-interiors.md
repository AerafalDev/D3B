# Step 9, the harvest's interiors (D416)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D416: The harvest's tour goes into an interior of a town or a field only when something says it holds a resource

*2026-11-09, step 9, a player's report relayed by the owner: "I still go into all the houses in Astrub". Refines D128, D149, D388.*

- **Reported and read in the code.** The harvest's tour goes through the maps of the world map first and its insides last (D128), and
  leaves out a map only when a bot saw it hold nothing wanted, or when every graphic its elements draw is known to offer nothing
  (D138). A house nobody has seen, whose graphics are not known, was neither: the tour went into it, one change in, one change out.
  A fresh install knows no graphic, so it entered all of them.
- **Measured** on the pack of 3.7.13.17 and on the owner's database of 774 learned graphics. Astrub's city, sub-area 95, holds 52
  maps on a world map and 83 interiors the world graph reaches, mostly houses. For a lumberjack with the
  owner's graphics: 8 interiors foretold (graphic 682, which the database says offers the lumberjack's skill 141, also drawn on 149
  maps outdoors), 59 known to hold nothing, 16 unknown and entered; with no graphic learned, all 83 were unknown. Of 2,652
  journeys between the city's outdoor maps, 51 crossed an interior, all from one map that only a building links to the others, so the
  journeys are not the cause.
- **Made.** `SubAreaInteriors.Of` names the interiors, the maps on no world map, of the sub-areas that also hold a map on a world
  map the world graph reaches: the rule of D388, which `HuntingGround` now shares. `HarvestZone.Interiors` holds them; a sub-area of
  interiors alone is a dungeon and keeps them all. `HarvestTour` leaves such an interior out unless a bot saw a wanted resource in it or a
  graphic it draws is known to offer one, and a sighting still wins over a graphic. The maps of the world map are unchanged: a map
  never seen is gone to, to learn what it holds. The count of a tour, the preview's and the map's states follow (an interior left out
  is `Empty`).
- **Not measured.** In game. A mine of such a sub-area whose graphics no bot has learned is no longer discovered by a tour; ore is drawn
  outdoors too, and a graphic learned anywhere in the world, kept by the database from one run to the next, brings the mine back. A
  zone's graphic that is drawn in houses and shared with a resource too high for the character (graphic 682 above) still brings those
  houses back each tour: the tour does not weigh the character's level, a debt of its own.
- **Rejected.**
  - The interiors left out always: the mines are interiors (D128).
  - A setting "Go into the buildings": a rule nobody has asked to change, and a setting more in a place 7r5 is changing.
  - The journeys avoiding interiors: one map of Astrub is reached only through one.
