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
