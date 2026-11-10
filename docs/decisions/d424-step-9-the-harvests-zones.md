# Step 9, the harvest and the zones of a map (D424)

## D424: A resource is used only from a cell beside it in the character's zone; one in another zone of its map is out of reach, and the tour will go there through the world graph

*2026-11-10, step 9, a player's report relayed by the lead ("when a character harvests and is not at the same height, it tries and fails, though it sometimes can go in front of the resource, or change map and come back from another side"). (a) made in this session; (b) chosen by the owner, to make in a session of its own. Refines D75, D86, D138.*

- **Found.** `ElementCells.UsedFrom` kept the walkable neighbours of an element's cell in the character's zone (`MapCell.Zone`, the high bits of
  `linkedZone`), and with none fell back to the zone's walkable cells nearest the element, for the fishing on water. An element whose walkable
  neighbours all lie in another zone of the map fell back too: `MapHarvest.Candidates` counted it reachable, the character walked to a cell of its own
  zone two or more cells away and `ElementUse` sent the use from afar. That the server refuses it is the player's report and the client's rule
  (the use is made from a neighbour); it was not measured in game.
- **Measured** on the pack of 3.7.13.17 (17,623 maps; the maps are the same in formats 11 and 12), with `artifacts/measure/zones.cs` and `doors.cs`
  (not committed): 864 maps hold more than one zone, 2,657 of their 46,490 elements stand on them. Of the (element, zone) pairs of those maps, 3,673
  on 607 maps had a walkable neighbour in another zone only and took a far cell by the fallback: the report's case; 944 had no walkable neighbour at
  all (water), the fishing's case. Zones are almost always separate parts of a map: of the pairs of zones tried (first cell of each) 10 were
  joined by `CellPathfinder`, and of 2,228 zones 54 had cells (of the first 40 tried) the pathfinder could not join to the zone's first, which is
  the floors' side (D86): those elements have a neighbour in the zone but no path, and were already skipped cleanly (`NoPath`). None of the 5,506
  interactive transitions of the world graph used the fallback, so the doors and the journeys do not change.
- **Made (a).** `UsedFrom` returns the walkable neighbours in the zone; with none, the nearest cells of the zone only when the element has no walkable
  neighbour in any zone (water); an element with walkable neighbours in other zones only returns none. `MapHarvest.Candidates` skips it for the visit and
  writes a debug line saying why (`BotLog.OutOfReach`), `ElementUse` answers `NoPath` and sends nothing, `ForcedMoves`' door use and
  `WorldPathfinder.ExitCells` follow. Tested on `ElementCells` (a ring of another zone, water kept, nearest cells of the character's zone only), on
  `MapHarvest.Candidates`, `HarvestAsync` and `ElementUse` with a fake map of two zones.
- **Chosen (b), not made.** The bot goes to the element's zone: the world graph has a vertex per (map, zone) and no edge between two zones of one
  map (client.md), so it leaves the map and comes back by another side. This is a change of the tour, not of the element's use: the pathfinder's goal
  is a set of maps (`WorldPathfinder.TryFind`), the tour's visited and unreachable sets, sightings, regrowths and zones' maps are keyed by map. The
  session that makes it starts from this plan, to be settled with the owner:
  - `WorldPathfinder.TryFind` takes a goal of vertices (map and zone); for the start's own map and another zone it must leave the map, and a
    journey variant in `WorldTravel` takes them. The character's vertex is `World.TryGetVertex(map, cell)`.
  - Which zone holds a wanted element is read from `MapData` (`ElementCells.UsedFrom` per zone) with the graphics learned, or, on arrival, from
    what `Candidates` skipped for the zone.
  - The lightest tour change: once a map is done, if elements were left for their zone only, its other zone is one more goal for the tour, once
    per tour; the visited, regrowth and sighting sets stay keyed by map. Keying them all by vertex is the heavy alternative.
  - Care: a vertex that cannot be entered (criteria), a cap on the map changes a resource is worth, D392's rotation and D416's and D417's
    interiors unchanged, the previews counting maps.
- **Rejected.**
  - Keeping the far cell for an element beside another zone: the report, and 3,673 pairs on 607 maps.
  - Going to the other zone in the same change as the fix: a tour planned per vertex is a session of its own, and the fix holds without it.
  - Skipping silently: the player could not tell why a resource stays.

## D425: The harvest's tour goes into another zone of a map as one more place of its tour, once per tour, by a goal of vertices; its sets stay keyed by map

*2026-11-10, step 9, the owner's choice of the lighter of two designs. Builds D424 (b), D138, D392, D416, D417, D86.*

- **Found.** The world graph has a vertex per (map, zone) and no edge between two zones of one map, so a zone is reached by leaving the map and
  coming back by another side; `WorldPathfinder.TryFind` had a goal of maps and ended at once on the start's map. The tour's visited and unreachable
  sets, sightings, regrowths and the previews are keyed by map, because the server describes a map's elements and the host's bots remember a map, not
  a zone. The activity kept the elements it left for a visit by map: a second visit to the same map in another zone found them left, so the tree
  beside which it now stood was never tried (found by the test of this change, not in game).
- **Made.** `WorldPathfinder.TryFind(from, maps, zones, rules)` takes a goal of vertices beside the maps (the old overload is the same with none): a
  vertex is reached when the search comes to it, so another zone of the start's own map is a path out and back, and a vertex that cannot be entered
  (a closed sub-area, a criterion, a transition banned) has no path. `WorldTravel.TravelAsync(maps, zones, stopAt, ...)` follows it and has arrived
  when the character's vertex (`World.TryGetVertex(map, cell)`) is sought; the journal names the one map or the one zone's map. `ElementCells.NeighbourZones`
  lists the zones of an element's walkable neighbours and `MapHarvest.OtherZones` the zones, other than the character's, where wanted elements stand
  that none of its own zone's cells reach (an element with a neighbour in the character's zone, or none at all, as on water, names none; an element
  names its lowest zone unless a zone already chosen for another borders it, so a few zones may be walked for where one would do).
- **The tour.** `HarvestTour` keeps, beside its map sets, the vertices wanted and those entered in this tour (`OtherZones`, `WantZone`, `Enter`,
  `GiveUp`), both cleared by `Next`. On a map of the zone the activity enters the vertex it stands in; once the map is done (nothing left to harvest from
  there, `Visit`), the other zones `OtherZones` names become wanted, each once, if a path of at most six map changes leads to them (the lead's
  reckoning: through one neighbour it is two; the rest a resource is not worth more; a debug line says each way, `BotLog.ZoneWanted`, `ZoneLeftAlone`).
  The next journey seeks the maps still to go through and the wanted vertices together, the nearest first; the tour rotates only when neither is left;
  a journey that fails or has no path gives its wanted vertices up for the tour, and a failed detour to one counts for nothing against the three failed journeys that end the activity. What the activity left for a visit (`_skipped`) is kept by map and
  zone. The rotation (D392), the interiors (D416, D417) and the previews, which count maps, are unchanged.
- **Rejected.**
  - Keying the tour, the sightings, the regrowths, the zone's maps and the previews by vertex: it touches `HarvestSightings` (shared by the host's
    bots), `Regrowths`, `HarvestZone`, `HarvestPreviews` and the app, for 607 maps of 17,623, and is hard to undo; the one wanted vertex per tour does
    what the report asks.
  - Visiting the other zone before the map is done: the character would leave resources of its own zone behind and come back for them.
  - Wanting every zone an element has a neighbour in: one visit takes the element, the others would be walked for nothing.
  - Going to the other zone whatever the way costs: a cap of six map changes leaves a resource behind rather than walk across the world for it.
