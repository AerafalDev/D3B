# The official client

What the installed official client holds that D3B uses, measured for the steps that extract it. It holds counts,
formats and layouts, never content. Measured on 2026-10-27 with `scripts/inspect.cs` (D26) and the extraction tool's
checks, on client 3.7.12.12, built on 2026-10-19, installed in `%LOCALAPPDATA%\Ankama\Dofus-dofus3`. Dofus Remastered's
`docs/client.md`, in `vendor/Dofus`, measures much more of the same client for drawing it: maps' graphics, effects,
shaders, characters.

## Measuring again

The client updates often. These commands measure again; `--help` describes the others.

```sh
dotnet run --file scripts/inspect.cs -- summary                       # versions, then bundles and other files per area
dotnet run --file scripts/inspect.cs -- texts                         # the text files, checked entry by entry
dotnet run --file scripts/inspect.cs -- types Map/Data --references   # the map layouts and their reference classes
dotnet run --file scripts/inspect.cs -- objects skillsdataroot        # the objects of the matching bundles
dotnet run --file scripts/inspect.cs -- dump skillsdataroot SkillsDataRoot --depth 5 --items 2
dotnet run --file scripts/inspect.cs -- layout worldassets_assets_all PathFindingData
dotnet run --file scripts/inspect.cs -- layout Map/Data MapMetadata --references
dotnet run --file scripts/extract.cs -- --output <dir> --force        # extracts, counting maps and graph, and checks them
```

`summary` reads headers only and takes seconds. The extraction tool fails when the client contradicts what this
document says D3B relies on (D27): its checks are the measurements to repeat at each client update.

## Installation

| Path | Content |
| --- | --- |
| `Dofus_Data/StreamingAssets/version` | `Version`, `BuildDate` and `ConfigUrl` lines: the build's identity (D24). |
| `manifest.json` | The launcher's manifest: the SHA1 of every file, which a pack's fingerprint uses (D24). |
| `Dofus_Data/StreamingAssets/Content/Data/` | 208 bundles, LZMA: one game data table each, 98.9 MiB decompressed. |
| `Dofus_Data/StreamingAssets/Content/Map/Data/` | 596 bundles, mostly LZ4HC: the maps, 4.0 GiB decompressed. |
| `Dofus_Data/StreamingAssets/Content/I18n/` | `de.bin`, `en.bin`, `es.bin`, `fr.bin`, `pt.bin`: the texts, 130.4 MiB. |
| `Dofus_Data/StreamingAssets/aa/StandaloneWindows64/` | 26 Addressables bundles, among them `worldassets_assets_all.bundle`, which holds the world graph. |

Unity 6000.3.16f1, serialized format 22, Addressables 2.9.1. In all, 20,623 bundles, 20.9 GiB on disk. Every
serialized file of a bundle carries its type trees, so every table and map reads without an IL2CPP dump.

AssetsTools.NET 3.0.5 reads every bundle. Its `IsAssetsFile` takes some `.resS` files for serialized ones, so
`UnityBundle` trusts the bundle's directory flags instead, and its generic reading builds one object per field, too
slow for maps: the extractor reads them through the type tree, with Dofus Remastered's typed readers (D26).

## Maps

`Map/Data` holds 17,722 maps in 586 of its 596 bundles, each map a MonoBehaviour named `map_<id>`, in one of two
layouts. `mapelements_assets_.bundle` holds the dictionary of element definitions, which D3B does not read, and nine
others hold no map. Every identifier is unique across both layouts; identifiers run from 0 to 247,333,634 and
fit an `int32`.

| Layout | Maps | Bundles | Type trees |
| --- | ---: | ---: | --- |
| `AleCore.Data.UnityClient.ClientMapData` | 15,983 | 560 | One. |
| `Core.World.Metadata.Maps.MapMetadata` | 1,739 | 26 | Three: 1,712, 23 and 4 maps. One lacks the sound playlists and the noise configuration; they differ in no field D3B reads. |

**`ClientMapData`** holds its `id`, the four neighbours `topNeighbourId`, `bottomNeighbourId`, `leftNeighbourId` and
`rightNeighbourId`, its graphics in three layers, its cells, and its elements as managed references.

**`MapMetadata`** nests a map of another layout in `mapData`, with the same neighbours and no id: the object's name
gives it. Its cells call the altitude `floor` and add `nonWalkableDuringFight` and `nonWalkableDuringRP`. None of its
maps has a position in `MapsInformationDataRoot` or a vertex in the world graph; their neighbours are mostly other
`MapMetadata` maps or maps the client does not hold, and no `ClientMapData` map names one as a neighbour. Their ids
start at 16,777,729. What they are is not known; D3B extracts them like the others.

### Cells

Every map has 560 cells, numbered in order, 14 columns of 40 staggered rows. Across the 9,924,320 cells:

| Field | Values | D3B |
| --- | --- | --- |
| `cellNumber` | The cell's index, always. | Checked, not stored. |
| `mov`, `los` | Booleans: walkable, lets lines of sight through. 4,968,756 cells are walkable. | Kept. |
| `nonWalkableDuringFight`, `nonWalkableDuringRP` | `MapMetadata` only: 7,233 and 101 cells. | Kept. |
| `farmCell`, `havenbagCell`, `roleplayMonstersMovementBlocked`, `red`, `blue` | Booleans, rare: 11,921, 9,427, 31, 35,740 and 33,918 cells. | Kept; their meaning is Dofus 2's, not measured. |
| `visible` | True on every cell. | Kept. |
| `speed` | 0 on every cell. | Kept, as a signed byte. |
| `mapChangeData` | 8 bits, bit `d` set where the cell leaves the map in direction `d`, clockwise from east. On cells of the east edge bits 0, 1 and 7; south 1, 2, 3; west 3, 4, 5; north 5, 6, 7; corners combine them. Set on walkable cells only. | Kept. |
| `linkedZone` | A byte. Its high four bits are the cell's roleplay zone, 0 on cells that cannot be walked on: the zone of the world graph's vertices (below). Its low four bits are not measured. | Kept; `MapCell.Zone` is the high bits. |
| `moveZone` | 0 or 1, 1 on 9,800 cells. Not measured. | Kept. |
| `altitude` or `floor` | From −1,000 to 820; 0 on 99.5% of cells. | Kept, as an `int16`. |
| `arrow` | From 0 to 10, non-zero on 295 cells, all `ClientMapData`. Not measured. | Kept. |

The four arrow cell lists of `ClientMapData` are set on 74 maps, 375 cells in all, of which 10 leave the map; D3B does
not keep them.

### Interactive elements

46,462 interactive elements stand on the maps, one per identifier and map. An identifier is unique within its map, not
across maps: 10,825 of the 31,579 distinct identifiers appear on several maps, mostly at other coordinates.

| Layout | Class | Elements | Cell |
| --- | --- | ---: | --- |
| `ClientMapData` | `ClientInteractiveMapAnimatedElement` | 31,416 | Its own `cellId`. Listed in `mapAnimatedElements`, in no group. |
| `ClientMapData` | `ClientInteractiveMapElement` | 13,860 | The `cellId` of the middleground group that lists it; 2,198 of them are bounding boxes (`isBoundingBox`). |
| `ClientMapData` | `ClientInteractiveMapElement` | 5 | None: drawn by background groups, which have no cell. |
| `MapMetadata` | `ClientInteractiveElementTransform`, `ClientInteractiveAnimatedElementTransform` | 1,100 | Their own `cellId`. |
| `MapMetadata` | Bounding boxes, a struct array of `mapData` | 81 | Their own `cellId`. |

In `ClientMapData` the identifier is `<interactiveId>k__BackingField`, a property's backing field, and in
`MapMetadata`, `m_interactionId`. The extractor treats any referenced object carrying one of the two as an interactive
element, whatever its class, and names fields by their property, so a layout serializing `interactiveId` as a plain
field reads the same. 65% of interactive elements stand on cells that cannot be walked on.

## World graph

`worldassets_assets_all.bundle` holds a `Core.PathFinding.WorldPathfinding.PathFindingData` named `world-graph`,
6.1 MB: the graph the client finds paths between maps with.

- `m_vertices`: for each of 10,981 maps, its vertices by zone, 11,327 in all. A vertex is a `m_mapId` (`SInt64`), a
  `m_zoneId` and a `m_uid` (`UInt64`). The uids run from 0 to 11,326, one per vertex; they are no function of the map
  and zone.
- `m_edges`: for each of 10,910 source vertices, by `m_uid`, its edges by destination `m_uid`: 30,909 edges, none
  between two zones of one map. An edge holds `m_from`, `m_to` and its `m_transitions`, one for 98.7% of edges and up
  to 4.
- A transition: `m_type`, `m_direction`, `m_skillId`, `m_criterion` (a string), `m_transitionMapId`, `m_cellId` and
  `m_id`. Every transition names a cell of its source map.
- `m_outgoingEdges` repeats `m_edges` as one list per source vertex: the same 30,909 edges and transitions.

**Zones.** The zones of a map's vertices are the high four bits of its walkable cells' `linkedZone`: equal sets on
10,795 maps, and on the other 186 the cells hold zones the graph leaves out, zones with no way out. Zones run from 1 to
15; 97.6% of maps have one. The extractor checks that every vertex's zone is a walkable cell's zone of its map.

**Transitions.** Four types occur, powers of two, one per transition. The client's code may know others, such as
zaaps; none is in the graph, and the extractor fails on one.

| `m_type` | Transitions | Meaning, measured |
| ---: | ---: | --- |
| 1 | 20,505 | Scroll: walk off the edge onto the neighbour of `m_direction`, 0, 2, 4 or 6. The destination is always that neighbour, and so is `m_transitionMapId`. |
| 2 | 3,554 | Scroll with an action: `m_transitionMapId` is still the neighbour, the map the client asks for, but the destination differs from it on 1,163 transitions: the server sends the character elsewhere. 1,101 of these match the client's `MapScrollActionsDataRoot`. |
| 8 | 1,764 | Map action: step on `m_cellId`, a walkable cell, which moves the character to the destination. `m_direction` is 255, none, on 77%. |
| 32 | 5,506 | Interactive: use the element `m_id` with skill `m_skillId`, 184 on 95% of them. The element is always one of the source map's; on 50 transitions it stands on another cell than `m_cellId`. |

Only interactive transitions name a skill and an element; the others have −1 in both. For every type but 2,
`m_transitionMapId` is the destination.

**Criteria.** 991 transitions carry one of 619 distinct criteria: 85 scrolls with an action, 67 map actions and 839
interactive transitions, no plain scroll. They are in the client's condition syntax: two-letter codes, 47 of them,
led by `Ad`, `Qo`, `Qa`, `PO` and `Qf`, each with an operator among `=`, `>`, `<` and `!`, and a value; 493 combine
several with `&` and `|`, 383 with parentheses. D3B keeps them as text; their meaning is for step 4.

**Coverage.** 10,981 of the 17,722 maps have a vertex, all `ClientMapData`. Every map with a vertex has a position in
`MapsInformationDataRoot`, and every zaap stands on one.

## Data tables

Each bundle `data_assets_<name>dataroot.asset.bundle` holds one MonoBehaviour,
`Core.DataCenter.Metadata.<domain>.<Name>DataRoot`. Its `objectsById` maps identifiers to records held as managed
references, laid out by their class's type tree. Records name their texts by number, such as `nameId`.

Tables the first milestone needs, with the fields of their records:

| Table | Records | Fields |
| --- | ---: | --- |
| `World.MapsInformationDataRoot` | 15,409 | `m_flags`, `id`, `posX`, `posY`, `nameId`, `subAreaId`, `worldMap`, `tacticalModeTemplateId` |
| `World.MapsCoordinatesDataRoot` | 6,674 | `compressedCoords`, `mapIds` |
| `World.MapReferencesDataRoot` | 574 | `id`, `mapId`, `cellId` |
| `World.MapScrollActionsDataRoot` | 2,222 | `id`, `rightExists`, `bottomExists`, `leftExists`, `topExists`, `rightMapId`, `bottomMapId`, `leftMapId`, `topMapId` |
| `World.WaypointsDataRoot` | 62 | `id`, `mapId`, `subAreaId`, `activated`: the zaaps |
| `World.SubAreasDataRoot` | 567 | `id`, `nameId`, `areaId`, `mapIds`, `bounds`, `shape`, `customWorldMapId`, `packId`, `level`, `isConquestVillage`, `basicAccountAllowed`, `displayOnWorldMap`, `mountAutoTripAllowed`, `psiAllowed`, `monsters`, `entranceMapIds`, `exitMapIds`, `capturable`, `achievements`, `exploreAchievementId`, `harvestables`, `associatedZaapMapId`, `neighbors`, `dungeonId` |
| `World.AreasDataRoot` | 69 | `id`, `nameId`, `superAreaId`, `containHouses`, `containPaddocks`, `bounds`, `worldmapId`, `subareaIds`, `hasWorldMap`, `hasSuggestion` |
| `World.SuperAreasDataRoot` | 10 | `id`, `nameId`, `worldmapId`, `hasWorldMap` |
| `Interactive.InteractivesDataRoot` | 446 | `id`, `nameId` |
| `Job.JobsDataRoot` | 23 | `id`, `nameId`, `iconId`, `hasLegendaryCraft` |
| `Job.SkillsDataRoot` | 373 | `id`, `nameId`, `parentJobId`, `isForgemagus`, `modifiableItemTypeIds`, `gatheredRessourceItem`, `craftableItemIds`, `range`, `useRangeInClient`, `useAnimation`, `cursor`, `elementActionId`, `availableInHouse`, `clientDisplay`, `levelMin`, `allowMarking` |

`SkillData` has no interactive identifier in this build: which skills an interactive offers comes from the network.
Items, item types, recipes, NPCs and their actions are measured in session 2c.

What the world tables hold, as the pack keeps them (D27):

- **Positions.** `MapsInformationDataRoot` covers 15,409 maps, every one a `ClientMapData` map; 2,313 maps have no
  position: the 1,739 `MapMetadata` maps and 574 `ClientMapData` maps with ids from 239,994,113 up, none in the world
  graph. `posX` from −88 to 66 and `posY` from −94 to 54, `int16`; `subAreaId` a `UInt16`, of which 9 maps name a
  sub-area the table lacks; `worldMap` an `SInt8`, 1 on 55% of maps and −1 on 24%; `tacticalModeTemplateId` 0
  everywhere, not kept. `m_flags` sets 27 of its 32 bits, bit 26 on every map; which bit means what is not measured,
  and the pack keeps the value as it is. BillBot reads it as Dofus 2's map capabilities, which no measurement here
  confirms.
- **Sub-areas.** 34 of the 567 allow accounts without a subscription (`basicAccountAllowed`). `associatedZaapMapId`
  names a zaap's map on 329 and is 0 on the other 238. `level` from 0 to 200. `harvestables` holds numbers from 289 to
  30,646 on 190 sub-areas, which session 2c measures. Every map a sub-area lists names that sub-area back.
- **Zaaps.** 62, 47 `activated`; every one stands on a map of the world graph, in the sub-area of its map. The graph has
  no zaap transitions, so a path through zaaps adds them from this table.
- **Areas and super-areas.** 69 areas and 10 super-areas; the pack keeps the areas' `superAreaId`, not the super-areas.
- `MapScrollActionsDataRoot` and `MapReferencesDataRoot` are not extracted: the graph's scroll actions already lead
  where the server sends, and no step needs map references yet.

## Texts

The five files share one Ankama format, little-endian throughout:

1. A language code as a string with a 7-bit encoded length: `fr` in every file, whatever its language.
2. An `int32` count, then that many pairs of an `int32` text identifier and an `int32` offset.
3. An `int32` count, then that many pairs of a 64-bit key hash and an `int32` offset.
4. The texts, UTF-8 strings with a 7-bit encoded length, which the offsets point to from the start of the file.

Every file has 339,912 numbered texts, identifiers 1 to 1,200,860, and 7,501 keyed ones, 347,413 distinct texts; every
offset starts a text. The texts take 24.0 MiB in French and 21.7 MiB in English. Packs keep the numbered texts in
French and English (D25).

## The pack of 3.7.12.12

Format 2 (D27, D28): 15,600,148 bytes, 17,726 entries, extracted in about 25 s on 16 threads, 10 s of which read the
client.

| Entries | Serialized | Compressed, zstd 19 |
| --- | ---: | ---: |
| `texts/fr`, `texts/en` | 54.2 MB | 11.4 MB |
| 17,722 `maps/<id>` | 90.5 MB, about 5.1 KB a map | 3.7 MB |
| `world/graph` | 1.4 MB | 0.25 MB |
| `world/atlas` | 0.4 MB | 0.06 MB |

Opening it reads the index in about 7 ms; a map reads in about 7 µs, the graph in 4 ms, the atlas in 1 ms.
