# Harvest, the maps no way leads to kept out of the next tours (D571)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The tours it refines are
[D128](d126-d175-step-6.md) and [D145](d126-d175-step-6.md); the rotation, [D391](d390-d392-step-9.md#d391-the-automatic-profiles-go-through-several-sub-areas-in-turn-and-the-harvest-varies-its-professions);
the recovery ladder, [D518](d518-d519-never-idle.md).

## D571: A map of the zone no way leads to, by the world graph and the character's rules, stays out of the zone's next tours until the character's level or subscription changes, and the zone is not dropped from the rotation for it

*2026-11-26, a session of its own, the owner's report (« Le panda a attendu sur une map je sais pas pourquoi et il se fait
agresser par les monstres de la map par ce qu'il attend, ça fait plusieurs fois que ça arrive »); the diagnosis and the fix the
lead's. Refines D128, D145 and D391. Builds D518, D519.*

- **Measured** on the owner's farmer's journals and captures of 25 and 26 November, on the pack of 3.7.14.18: of its 27 lost
  fights, most were attacks ending a stand of about 73 s on map 126093080, or of 6 to 34 s on map 126092053. Through each stand,
  a journey with no single map sought ended with no way, once a second, the character never moving. Its cell was in the graph
  (zone 2, vertex 4964, three ways out; zone 1, vertex 4922, four). Sub-area 809 holds 34 maps, and from there 33 are behind a
  door's criterion or in a zone of 126093080 that no way enters. Of sub-area 519's 109 maps, 8 are out of every way. The farmer
  plays "Paysan", the automatic place.
- **The loop, read in the code.** A tour left with only such maps: the journey found no way, they were unreached for the tour, and
  the zone was dropped from the rotation (D391). At the next step every other zone was done, the rotation was drawn again
  (`SubAreaReach` takes a sub-area one map of which is reached, here the map the character stood on), the zone came back with a
  new tour that knew nothing of them, and `HarvestRotation.Next` chose it. Two steps of 0.5 s, a journey a second. A zone kept
  would loop as well, a tour's `Next` forgetting its unreached maps.
- **What was made.** A journey to the zone's maps that ends with no way, from a zone of a map the graph leaves, is asked again of
  the graph with the character's rules and no way left out. When it still finds none, that is a fact: `HarvestTour.NoWay`
  marks each map, which this tour and the next ones leave out of what they go to, count and wait for. The marks go once the
  character's level or subscription changes (`Know`), either of which may open a criterion. The zone is not dropped then; it is
  left as any whose maps are done. With no map of it to go through, the activity ends unreachable, a failure the run restarts
  from (D519), not a final one.
- **Kept.** A journey whose every way was tried and failed ends with no way too, the ways it tried being left out for it
  (`WorldTravel`); and from a bank's inside or a mine's pocket, which the graph does not leave, no way says nothing of the maps
  beyond. Both keep the old way: unreached for the tour, the zone dropped from an automatic rotation, else the ladder (D518), whose
  last rung leaves the map. The fake game's never-idle tests caught both when the first draft marked every map with no way.
- **Not measured, tested on the fake game only**: a tour that keeps the marks in game. To see: the farmer in sub-area 809 or 519
  harvests the maps it reaches and leaves the others, with no stand of a journey a second.
- **Not changed, noted.** The automatic place still weighs a sub-area by all its maps (D166), so 809 ranks for 34 maps when one
  is reached. A sub-area whose maps are all left out, or out of every way, can still be drawn again with a new tour once per round
  of the rotation: one journey with no way each time, not one a second.
- **Rejected.**
  - Every journey with no way taken as the fact: from a pocket, or once its ways all failed, the next tours would leave out maps
    the character can reach. The never-idle tests saw the harvest wait two hours with nothing sent.
  - Filtering each drawn sub-area to the maps the character reaches, one search a map: about a thousand searches a draw, for what
    the tours learn on the way.
  - The marks forgotten at each draw of the rotation: the loop came back through the draw itself.
  - Ending the activity at the first map with no way: a sub-area of 109 maps with 8 shut has 101 to harvest.
