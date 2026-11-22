# The harvest, a mine's floors and a resource grown back (D495)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The floors of a map
with stairs are [D86](d064-d090-step-4.md); a resource in another zone of its map, [D424 and D425](d424-step-9-the-harvests-zones.md);
the journey's stop on a crossed map, [D392](d390-d392-step-9.md) and [D439](d439-harvest-on-the-way.md); a map action's cell,
[D488](d488-harvest-action-cells.md).

## D495: A resource is used from its own floor, a ledge cut from the character's is come onto from the side it touches, and a journey stays where a resource grew back

*2026-11-22, the lead's task from two of the owner's reports: "the character tries to harvest resources that are not at its
height, and it fails; it does not go round through the maps", above all for the miner, "change a map left, then down,
then right"; and "a resource grows back on the map but the character does not harvest it". Bug fixes within the task's
scope. Builds D75, D86, D392, D424, D425, D439, D488.*

- **Measured, the heights**, on the 126 game captures D3B played of 18 to 22 November (client 3.7.14.18, mapping 24, pack
  13; scratch probes under `artifacts/measure/`, never committed, printing counts and ids): the server started 20,492 of D3B's uses and
  refused 179. 124 of them, on four maps of a mine (193200134, 193331713, 193331715, 193331717), came from a
  neighbour of the ore 100 to 190 above its floor, the ore on a ledge; on the maps with stairs the server took 427 uses
  from 40 or less. The bot's `ElementCells.UsedFrom` took every walkable neighbour of the zone, the nearest above the cliff. On the
  pack, over 456 maps with stairs, the gaps between an element and its walkable neighbours are 0 to 40 on 5,109 pairs, 41 to
  99 on 21, 100 or more on 289.
- **Made, the floor**: on a map with stairs, where floors count (D86), an element is used only from its walkable
  neighbours whose floor is within 40 of its own, when it has some; otherwise from all of them, as before. The zones it
  names (`NeighbourZones`) follow, and a door's cells (`WorldPathfinder.ExitCells`). An ore whose neighbours on its floor the character's floor cannot walk to is skipped
  for the visit (`BotLog.OutOfReach`), its use never sent.
- **Measured, the ledges**: on the pack, of the elements on a map with stairs whose neighbours lie on other floors, 74
  (element, zone) pairs have their floor's neighbours reached from the whole zone, the fix alone enough; 12 have them cut
  from part of the zone and reached only from an edge, as on 193331717, whose ore below the cliff is reached from the 21
  cells of its west edge alone, the character coming from the north and the east. The world graph has one vertex for the
  whole zone, both floors (D425), so no journey tells them apart. On the 52,658 scrolls of the captures, the
  character came onto the cell straight across the one it left from, 13 cells before it leaving east and 532 leaving
  south, the other ways round, 46,900 times; the cell straight across could not be walked on for 2,094 of the rest.
- **Made, the way round**: once a map is done, if wanted elements of the character's zone stood on a part it could not
  walk to, `MapHarvest.OtherLevel` finds a side whose edge cells lead there (`MapEntries.Reaching`) and whose neighbour on
  that side is in the world graph, with the cells straight across walkable and leaving towards the map
  (`MapEntries.LeftFrom`). The tour wants it once a tour (`HarvestTour.WantLevel`), if fewer than six map changes lead to the
  neighbour, and its next journey goes there; on the neighbour the character scrolls back from a cell straight across one
  of those edge cells, the visit's skipped elements are forgotten, and the step harvests the ledge. Arrived elsewhere, or
  not arrived, the part is left for the tour and counts nothing against the journeys that end the activity.
- **Made, leaving the ledge**: the graph offering every way of the map from the ledge, most out of its reach, a journey's change
  whose cells the character cannot walk to sends nothing and is left out of the journey without counting among its three
  failures (`WorldTravel`), so that the journey from the ledge leaves by the side it came from instead of failing.
- **Measured, the regrowths**: on the same captures, 757 wanted elements (of a skill D3B used in that capture) grew back on
  the character's map, `InteractiveElementUpdatedEvent` offering their skill again, which the bot's state takes (replayed
  through `CharacterEvents`); 178 were harvested on the visit. Of those left, apart from fights, banking and the group's
  followers, 136 grew back as the walk to the map's edge was going, the map change following it, and about 60 on a map
  the journey was crossing, after its information came. The journey asked whether to stop only on a map's arrival, and
  never of the map it started from.
- **Made, the stay**: a journey may stay on the map it is about to leave (`WorldTravel.TravelAsync(..., stayOn, ...)`), asked
  once the walk to a scroll's or a door's cell arrived, before the change is asked for, with the state the character came
  onto the map with and the state now (`MapChange.ChangeMapAsync(..., stay, ...)`, `MapChangeOutcome.Stayed`). The harvest
  stays where a wanted element it can reach is offered that was not as it came (`MapHarvest.Regrown`), harvests it, though
  the tour crossed that map out of the zone already, and goes on.
- **Not measured in game**: a miner coming onto a ledge, the server placing it on the cell straight across, then using the
  ore; a journey staying for a resource grown back. Tested on the fake game only.
- **Rejected.**
  - A floor gap above 40 still taken: never seen taken; the 21 pairs of 41 to 99 are left out with the cliffs.
  - Leaving out an element whose neighbours all lie on other floors: 27 elements on the pack, perhaps used from any of them;
    nothing measured says no.
  - Splitting the world graph's vertices by floor: a change of the extracted graph and of every set keyed by vertex for 12
    pairs; the tour's one detour does what the report asks.
  - Stopping the walk to the edge as the element grows back: the bot sends no walk's cancelling, a request of the
    client's own; asked at the walk's end, the stay loses at most its last steps.
  - Staying for any element offered on the map left, offered as the character came or not: one left for a refusal or
    another zone would hold every journey from that map.
