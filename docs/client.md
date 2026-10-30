# The official client

What the installed official client holds that D3B uses, measured for the steps that use it. It holds counts, formats
and layouts, never content. Measured on 2026-10-27 with `scripts/inspect.cs` (D26) and the extraction tool's checks, on
client 3.7.12.12, built on 2026-10-19, installed in `%LOCALAPPDATA%\Ankama\Dofus-dofus3`; how it connects was measured
on running clients, then on its bytes through D3B's relay ([Connection](#connection)), its protocol's code with
`scripts/schema.cs` ([Protocol code](#protocol-code)), its connection phase against D3B's readable protocol with
`scripts/mapping.cs` ([mapped](#the-connection-phase-mapped)), then decoded through it on 2026-10-28
([decoded](#the-connection-phase-decoded)), and its game the same day ([the game, mapped](#the-game-mapped)), then
acted on through it ([the game, acted on](#the-game-acted-on)), walked by a bot ([the game, walked](#the-game-walked)),
travelled from map to map ([the game, between maps](#the-game-between-maps)), its elements used
([the game, used](#the-game-used)), its bank used ([the game, banked](#the-game-banked)), its zaaps and haven bag
used ([the game, teleported](#the-game-teleported)), and its floors, fights and disconnections met by an activity
([the game, interrupted](#the-game-interrupted)). Its icons and its elements' graphics were measured on 2026-10-29 for
session 6b ([Icons](#icons), [Graphics](#graphics)), and a tour foretold by them ([the game, foretold](#the-game-foretold)).
Its fights were measured on 2026-10-30 for session 9a, on the wire ([the game, fought](#the-game-fought)) and in its tables
([Fights' tables](#fights-tables)), which session 9b measured whole, their flags read in its code, the same day; its
casts against D3B's rules for session 9c ([the game, cast](#the-game-cast)), and what each did against D3B's effects for
session 9d ([the game, foreseen](#the-game-foreseen)), and the states its spells ask for session 9f
([the game, masked](#the-game-masked)).
Dofus Remastered's `docs/client.md`, in `vendor/Dofus`,
measures much more of the same client for drawing it: maps' graphics, effects, shaders, characters.

## Measuring again

The client updates often. These commands measure again; `--help` describes the others.

```sh
dotnet run --file scripts/inspect.cs -- summary                       # versions, then bundles and other files per area
dotnet run --file scripts/inspect.cs -- texts                         # the text files, checked entry by entry
dotnet run --file scripts/inspect.cs -- types Map/Data --references   # the map layouts and their reference classes
dotnet run --file scripts/inspect.cs -- objects skillsdataroot        # the objects of the matching bundles
dotnet run --file scripts/inspect.cs -- dump skillsdataroot SkillsDataRoot --depth 5 --items 2
dotnet run --file scripts/inspect.cs -- layout itemsdataroot ItemsDataRoot --references  # record classes
dotnet run --file scripts/inspect.cs -- layout worldassets_assets_all PathFindingData
dotnet run --file scripts/inspect.cs -- layout Map/Data MapMetadata --references
dotnet run --file scripts/inspect.cs -- objects job_assets_2x                 # the professions' icons, a texture and a sprite each
dotnet run --file scripts/inspect.cs -- layout mapelements_assets_ MapElementsMetadata --references  # the element definitions
dotnet run --file scripts/extract.cs -- --output <dir> --force        # extracts, counting maps, graph and tables, and checks them
dotnet run --project src/D3B.Host -- --Network:Captures:Enabled true  # relays clients started from the launcher, logs each frame of their connection phase, and records them
dotnet run --file scripts/client.cs                                   # which configuration the client reads, Ankama's or D3B's
dotnet run --file scripts/capture.cs -- --frames                      # the captures' reads, frames and protobuf shapes, D3B's own frames apart, no value
dotnet run --file scripts/schema.cs -- --proto <dir>                  # the protocol's obfuscated schema, counted, and as .proto text
dotnet run --file scripts/capture.cs -- --frames --schema <schema>    # which message of the schema reads the captures, field by field
dotnet run --file scripts/capture.cs -- <game captures> --messages --mapping <mapping> --frames --schema <schema>  # the game's messages in order, named, field by field
dotnet run --file scripts/mapping.cs                                  # pairs D3B's readable protocol with the schema, audits it, writes the mapping
dotnet run --file scripts/mapping.cs -- --without-captures            # the same from pins and structure alone, without anchors
dotnet run --file scripts/mapping.cs -- --pins <empty dir>            # without pins: lists, for what is left open, the messages that fit
dotnet run --file scripts/fights.cs -- --details                      # replays the captured fights, D3B's effects against the server's actions
dotnet run --file scripts/fights.cs -- --turns                        # replays every turn the characters played through the AI, its foresight against what came
dotnet run --file scripts/fights.cs -- --live                         # what D3B did in the fights: who played each, how it ended, its requests
```

`summary` reads headers only and takes seconds. The extraction tool fails when the client contradicts what this
document says D3B relies on (D27), and so do the schema tool (D40) and the mapping tool (D44): their checks are the
measurements to repeat at each client update.

## Installation

| Path | Content |
| --- | --- |
| `Dofus_Data/StreamingAssets/version` | `Version`, `BuildDate` and `ConfigUrl` lines: the build's identity (D24), and where the client reads its configuration ([Connection](#connection)). While D3B's host runs, `ConfigUrl` names D3B's copy (D35). |
| `manifest.json` | The launcher's manifest: the SHA1 of every file, which a pack's fingerprint uses (D24). |
| `zaap.yml`, `zaap-windows.yml`, `zaap-start.bat` | How the launcher starts the client: its arguments, settings and log file ([Connection](#connection)). |
| `GameAssembly.dll`, `Dofus_Data/il2cpp_data/Metadata/global-metadata.dat` | The IL2CPP code, 120.2 MiB, and its metadata, 41.8 MiB: version 39, not encrypted, 37,544 string literals and 348,188 identifier strings. |
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

An element names its graphic, `gfxId`, but not its kind: which interactive of `InteractivesDataRoot` it is, and which
skills it offers, the server tells for each element of the map a character enters ([Jobs and skills](#jobs-and-skills)).
The server also lists there the elements of the neighbouring maps, marked so, with their own map's cells
([the game, used](#the-game-used)).

### Graphics

Every interactive element of both layouts names its graphic, `gfxId`, a field of its own: 3,499 graphics draw the 46,462
elements. No element of client 3.7.12.12 is drawn twice on its cell with two graphics. `mapelements_assets_.bundle` holds
the element definitions, `MapElementsMetadata`, a dictionary of `GfxElement` (its id, type, `m_gfxId`, height, origin and
size) and `AnimatedElement` (an entity look and its animation): drawing only, nothing of which interactive an element is.

What an element is, the server says (above). Measured for session 6b on 31 captures of 6a, the server's descriptions of
550 elements on 102 maps, joined to the pack's elements by map and identifier, every one found:

- **One graphic, one resource**, mostly: 113 graphics seen, nettle's elements all drawn with one, ash's with another.
  Fishing spots share one graphic between seven fish, 7 interactives and their skills; a few graphics draw both an
  interactive the server names and an element it names nothing (interactive −1). So D3B learns, for each graphic, every
  skill seen (D138).
- **Foretelling**: learned on every capture but one, the graphics foretold the elements of the last right 102 times of
  103, each capture in turn.
- **Reach**: once seen, a graphic tells of the whole world. Nettle's draws 1,178 elements on 775 maps. The 53 sub-areas
  listing nettle hold 3,133 maps a character may travel to, 2,904 with elements and 750 drawing its graphic; over the 19
  resources learned, 5,569 of the 32,527 maps of the sub-areas listing them draw their graphic.
- **What the server leaves out**: of the 834 elements the pack places on the 102 maps it described, it listed 550; the 284
  others are drawn with 49 graphics, 26 of which it names a resource or another interactive elsewhere. An element it does
  not list says nothing of its graphic.
- Base's resources, such as drinking water from a well, are listed by no sub-area: only the graphics say where they are.

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
several with `&` and `|`, 383 with parentheses. The pack keeps them as text, and `D3B.Engine` reads them (D71): no group
mixes `&` and `|`; a condition is a code, an operator and a value whose arguments are separated by commas, 2,261 of them
`XX=n`, a few `XX=true` or naming an event such as `RV=5,monsterKilled,60`; 7 carry a context in brackets, `[413]MC=0`;
22 groups are led by `#`, all but one a list of cells, `#(PC=4|PC=5|PC=18|PC=19)`. Those lists name the cells of the
edge a scroll with an action is left from, its own cell among them on 13 of 23; two lists share an edge 8 times, each
leading to a map or zone of its own. `PZ` appears 9 times, always `PZ=1`, subscribed, all on doors.

**What the transitions' cells say.** Every scroll's cell lies on the edge of its direction. The client marks 18,746 of
these cells as leaving in that direction, 1,040 more only along a diagonal next to it, and 719 not at all; the cells of
the edges carry the marks of the edges they lie on, a corner those of both. By D3B's rule (D73), 74 scrolls are left
from their own cell alone, and a scroll from 18.4 cells on average. Of the map actions, 1,111 of the
1,356 without a direction have an interactive element on their cell; the other 408 have a direction, which only 16 of
their cells carry as a mark.

**Reached.** From the bank of Astrub, the scrolls, scrolls with an action and map actions whose criteria the bot can
answer reach 4,021 of the 11,327 vertices, 4,088 ignoring criteria; with doors 5,551 strictly and 8,738 counting what
is not known as met; with every transition, 9,024. Zaaps and the like are not in the graph: the path teleports apart
from it (D83). Measured again for doors,
for a subscribed character of level 200 holding nothing: 5,607 with the doors of skill 184 (D76), 5,649 with every
interactive transition ([the game, used](#the-game-used)).

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
| `World.HintsDataRoot` | 834 | `id`, `categoryId`, `gfx`, `nameId`, `mapId`, `realMapId`, `x`, `y`, `outdoor`, `subareaId`, `worldMapId`, `level`: the world map's hints |
| `World.HintCategoriesDataRoot` | 12 | `id`, `nameId` |
| `Item.ItemsDataRoot` | 21,554 | `m_flags`, `id`, `nameId`, `typeId`, `descriptionId`, `iconId`, `level`, `realWeight`, `price`, `itemSetId`, `criterions`, `criterionsTarget`, `appearanceId`, `isColorable`, `recipeSlots`, `recipeIds`, `dropMonsterIds`, `dropTemporisMonsterIds`, `possibleEffects`, `evolutiveEffectIds`, `favoriteSubAreas`, `favoriteSubAreasBonus`, `craftXpRatio`, `craftVisibleCriterion`, `craftConditionalCriterion`, `craftFeasibleCriterion`, `visibilityCriterion`, `recyclingNuggets`, `favoriteRecyclingSubareas`, `resourcesBySubarea`, `importantNoticeId`; weapons add `criticalHitBonus`, `minRange`, `criticalHitProbability`, `range`, `castInLine`, `apCost`, `castInDiagonal`, `castTestLos`, `maxCastPerTurn` |
| `Item.ItemTypesDataRoot` | 236 | `id`, `nameId`, `superTypeId`, `categoryId`, `isInEncyclopedia`, `rawZone`, `craftXpRatio`, `evolutiveTypeId` |
| `Item.ItemSuperTypesDataRoot` | 25 | `id`, `possiblePositions` |
| `Interactive.InteractivesDataRoot` | 446 | `id`, `nameId` |
| `Interactive.SkillNamesDataRoot` | 364 | `id`, `nameId` |
| `Job.JobsDataRoot` | 23 | `id`, `nameId`, `iconId`, `hasLegendaryCraft` |
| `Job.SkillsDataRoot` | 373 | `id`, `nameId`, `parentJobId`, `isForgemagus`, `modifiableItemTypeIds`, `gatheredRessourceItem`, `craftableItemIds`, `range`, `useRangeInClient`, `useAnimation`, `cursor`, `elementActionId`, `availableInHouse`, `clientDisplay`, `levelMin`, `allowMarking` |
| `Npc.NpcsDataRoot` | 6,472 | `id`, `nameId`, `dialogMessages`, `dialogReplies`, `actions`, `gender`, `look`, `animFunList`, `fastAnimsFun`, `tooltipVisible`, `dialogData`, `defaultSkinId` |
| `Npc.NpcActionsDataRoot` | 16 | `id`, `realId`, `nameId` |

Recipes, in `Job.RecipesDataRoot`, wait for crafting (step 11).

What the world tables hold, as the pack keeps them (D27, D29):

- **Positions.** `MapsInformationDataRoot` covers 15,409 maps, every one a `ClientMapData` map; 2,313 maps have no
  position: the 1,739 `MapMetadata` maps and 574 `ClientMapData` maps with ids from 239,994,113 up, none in the world
  graph. `posX` from −88 to 66 and `posY` from −94 to 54, `int16`; `subAreaId` a `UInt16`, of which 9 maps name a
  sub-area the table lacks; `worldMap` an `SInt8`, 1 on 55% of maps and −1 on 24%; `tacticalModeTemplateId` 0
  everywhere, not kept. `m_flags` sets 27 of its 32 bits, bit 26 on every map, and the pack keeps the value as it is.
  Bit 3 lets a character teleport from the map, and bit 2 opens the haven bag, measured
  ([the game, teleported](#the-game-teleported)); the other bits are not. BillBot reads it as Dofus 2's map
  capabilities, bit 2 letting one teleport to the map and bit 3 from it, as the client's code names them
  ([Fights' tables](#fights-tables)).
- **Sub-areas.** 34 of the 567 allow accounts without a subscription (`basicAccountAllowed`). `associatedZaapMapId`
  names a zaap's map on 329 and is 0 on the other 238. `level` from 0 to 200. `harvestables` lists items on 190
  sub-areas ([Harvestables](#harvestables)). Every map a sub-area lists names that sub-area back.
- **Zaaps.** 62, 47 `activated`; every one stands on a map of the world graph, in the sub-area of its map. The graph has
  no zaap transitions: the path teleports to the zaaps the server says the character knows (D83).
- **Areas and super-areas.** 69 areas and 10 super-areas; the pack keeps the areas' `superAreaId`, not the super-areas.
- `MapScrollActionsDataRoot` and `MapReferencesDataRoot` are not extracted: the graph's scroll actions already lead
  where the server sends, and no step needs map references yet.

### Items

21,554 items, 769 of them weapons (`WeaponData`, which adds the fight fields), ids from 44 to 34,840; every `nameId` has
a text in French and English. `typeId`, a `UInt16`, names 229 of the 236 types; one test item names type 264, which the
client lacks. `level`, a `UInt8`, from 0 to 200. `realWeight`, a `UInt16`, is the weight of one item in pods: 26
values from 0 to 20,000, 0 or 1 on 77% of items. `m_flags`, a `UInt16`, sets bits 0 to 11, bit 10 on 96% of items;
which bit means what is not measured, the client's code naming bit 10 saleable ([Fights' tables](#fights-tables)). `iconId` names the item's icon ([Icons](#icons)): 11,756 distinct over the items.
The pack keeps the id, name, type, level, weight and icon, 17 bytes an item; effects, recipes, drops, criteria and
`m_flags` are left out (D29, D139).

**Weapons**, measured for session 9e3 (D218, D219), kept from format 6 in their own entry: 769 of 13 types, each of super
type 2, the only types of it. A weapon names its effects by reference, in the table's registry. AP 4 on 386, 3 on 201, 5
on 110, 2 to 7 on 763, and 0, 1, 10, 20 and 42 on the others; the least range 1 on 603, 0 to 20; the most 1 on 483, 0 to
23; 270 cast in line, 77 in diagonal, every one testing the line of sight; a critical chance of 30 on 438, 0 to 100, its
bonus 0 to 30; once a turn on 611, twice on 148, 3 times on 7, no limit on 3. Their 6,637 effects, 0 to 21 a weapon,
are a blow's damage, steals, heals and AP or MP removed, 1,311 of effects used in fights, and the characteristics the
weapon gives; 2 name effect 205, which the client lacks. Every other field of a weapon's effect is the same on all: no
mask, no trigger, no duration, no draw, an element of -1, flags 15, a circle of 1 as zone; the zone a blow hits is its
type's `rawZone`: `P`, the cell struck, for 8 types and 479 weapons, the 126 swords among them; `T1,10,1` the 94 staffs,
`X1,0,10,1` the 99 hammers, `V1,10,2` the 61 shovels, `U1,10,1` the 16 scythes, `L3,10,3` the 20 lances. How the client
reads the numbers is not measured. Of the 126 swords, 122 do something in a fight, 76 one effect, mostly damage or a
steal of one element.

Item types: `superTypeId` takes 25 values, the ids of `ItemSuperTypesDataRoot`, which names none of them; `categoryId`
from 0 to 5, 2 on 78 types. The pack keeps both as they are.

### Jobs and skills

23 jobs, among them `Base`, id 1, the job of 248 of the 373 skills: using a zaap or a door, a workshop, a quest
object. Every skill's `parentJobId` is a job of the table.

`gatheredRessourceItem` makes a skill a harvest. 90 skills of five professions gather 85 items, the resources:
lumberjack (2) 21 skills, miner (24) 17, alchemist (26) 14, farmer (28) 14, fisherman (36) 24. Five of them, 296 and
298 to 301, repeat a level-1 skill with `clientDisplay` off. Every resource is an item with a known type. Of the skills
of `Base`, 34 gather 33 items, such as potatoes or event resources, 8 name 0, and skill 260 names item 3,656, which
the client lacks; the others name −1. The pack keeps 0 and the missing item as gathering nothing.

`levelMin` runs from 1 to 200 on harvests. `range` is 1 on 342 skills and 10 on 28, the fisherman's 24 among them,
and 2, 40 and 63 once each; `useRangeInClient` is set on 29 skills, none of the fisherman's. How the client uses either
is not measured.
`elementActionId` is 1 on every harvest. `craftableItemIds` is set on 99 skills, the crafts of step 11.

**What links them.** `SkillData` has no interactive identifier in this build, and a map's interactive element names only
its graphic (Interactive elements, above): which interactive an element is, and which skills it offers, the server
tells for each element of the map a character enters. The client's data links a profession's skill to its resource,
and the resource to the sub-areas that list it: a bot looking for ash wood finds the skill gathering item 303, the
sub-areas listing 303, then, on a map, the elements the server says offer that skill.

`SkillNamesDataRoot` names 364 things such as rooms ("Salle A II"); 270 of its ids are also skill ids, but none of its
texts is a skill's name. It is not extracted.

### Interactives

446 interactives, ids from 1 to 475, every one named: resources such as Frêne (1) or Blé (38), workshops, the zaap
(16), doors (70). The ids are those the server gives an element of the map; scripts of the Snowbot family name the
resources to harvest by them.

### Harvestables

A sub-area's `harvestables` lists items: 4,078 in 190 sub-areas, 254 distinct, every one an item of the client, none
twice in a sub-area, unsorted in 189. 1,318 of them are the 85 resources professions gather, every resource in at least
one sub-area. The rest is what harvesting may bring besides, mostly loot boxes (Boîte de Rékloot); no item a skill of
`Base` gathers is listed. The pack keeps the resources, sorted, as a slice of `WorldAtlas.Harvestables` per sub-area.

### Banks

The client marks no bank and no banker as such:

- **Banks.** 10 of the 834 hints are drawn with graphic 401, all named Banque, in category 4, Divers. Each stands on
  an outdoor map (`worldMap` 1) with a position and a vertex in the world graph, and the hint's coordinates and
  sub-area are its map's. From each, an interactive transition with skill 184 leads to 1 to 3 maps at the same
  coordinates on no world map (−1): the bank's inside, and for some other buildings. Which of them holds the banker,
  the server tells: the NPCs of a map come with it. Only Astrub's bank lies in a sub-area open to accounts without a
  subscription.
- **Bankers.** A reply of `dialogReplies` is a pair, its id and its text, on every one of the 64,346 replies; 60 reply
  ids are listed by several NPCs. 8 NPCs have a reply reading `Consulter son coffre personnel`, with or without a final
  full stop: 100 (Banquier), 520, 522, 3,516, 3,597, 5,653, 6,374 (Banquier brâkmarien) and 6,394 (Banquier
  bontarien), with 2 to 8 such replies each, 29 in all; 100, 520 and 522 share reply 259. Each offers only action 3.
- **Actions.** `NpcActionsDataRoot` holds 16 actions; 3, Parler, talks, and its `realId` is 3. 5,922 NPCs offer it;
  17, Examiner, also has `realId` 3.

The extractor finds banks by graphic 401 and bankers by the French text, and fails when it finds none, or a bank off
the graph, or a banker who cannot be talked to (D29).

**Insides**, measured on the world graph for session 4d (D79): from the map in front of each bank, doors of skill 184
lead to 1 to 5 maps, 16 in all at the bank's coordinates on no world map, and 2 to other maps of the world, both from
one bank. 7 banks have one inside a door without criterion leads into, 3 have two; quests close the doors of 3 more: at
Astrub, one door element leads to the bank and to such an inside, the criterion choosing. The bank of Astrub's inside
holds two bankers, NPCs 520 and 522; the server places them, with no NPC in the client's data for that map.

### Fights' tables

Laid out on 2026-10-30 for session 9a with `scripts/inspect.cs -- layout <bundle> <root> --references`, measured for
session 9b with a measuring tool never committed, the flags and names read in the client's code (D183). The pack holds
them from format 5 (D182).

| Root | Records | Holds | In the pack |
| --- | ---: | --- | --- |
| `SpellsDataRoot` | 17,006 | A spell's name, type, flags, levels, scripts of its animations, and its preview zone | All but its description, type and scripts |
| `SpellLevelsDataRoot` | 34,320 | A level's grade, AP, ranges, critical chance, stacks, casts per turn and target, global ones, interval, initial and global cooldowns, the least level, a criterion on states, flags in 16 bits, effects and critical effects, preview zones | Every field, but the 69 levels of no spell |
| `EffectsDataRoot` | 884 | An effect's characteristic and its operator, category, element, whether it boosts, is active and used in fights, its priority and power rate, its opposite | Those fields and its description |
| `SpellStatesDataRoot` | 6,377 | A state's flags, effects and spell level | All but its icon |
| `MonstersDataRoot` | 5,129 | A monster's flags, race, grades, spells and their grades, characteristic ratios, drops, sub-areas, aggression, souls | All but its drops, looks, sub-areas, achievements and aggression's criterion |
| `MonsterRacesDataRoot`, `MonsterSuperRacesDataRoot` | 265, 35 | Races and their super-races | Their names |
| `BreedsDataRoot`, `BreedRolesDataRoot` | 19, 8 | A breed's points per characteristic, spells and roles; the roles | All but looks and descriptions |
| `CharacteristicsDataRoot` | 123 | A characteristic's keyword, category and scaling formula | All but its asset |
| `SpellVariantsDataRoot` | 431 | A spell and its variant, of a breed | All |
| `SpellPairsDataRoot` | 644 | The custom mode's pairs: a name, a description, an icon | No |
| `SpellBombsDataRoot`, `SpellBombWallsDataRoot` | 10, 8 | The Rogue's bombs and the walls between them | All |
| `LuaFormulasDataRoot` | 38 | Formulas in Lua: a monster's characteristics and experience by level, experience tables, a group's bonus, and others of guilds, alliances and seasons | The 4 characteristics name |
| `SpellTypesDataRoot`, `SpellScriptsDataRoot` | 3,274, 14,934 | Spells' types, named by texts their records write as text; spells' animations | No |

**Flags**, as the client's code names each bit, with how many records set it:

- A spell's `m_flags`, `SpellFlags`: verbose cast 14,719, bypassing the summons' limit 20, always triggering spells 359,
  hiding the cast's conditions 1,674.
- A level's 16-bit `m_flags`, `SpellLevelFlags`, bits 0 to 13: cast in line 3,276, in diagonal 262, testing the line of
  sight 11,339, needing a free cell 2,117, a taken cell 1,879, a cell with no trap 219, range boosted 4,628, effects
  hidden 8, hidden 1, animation played 28,455, needing a fighter seen 18, a cell with no portal 42, no projection through
  portals 11, proportional diagonal 6. The getters of the level's booleans read the same bits.
- An effect's 8-bit `m_flags`, `EffectInstanceFlags`: visible in the tooltip 90,913, among the buffs 91,624, in the
  fight's log 90,491, on the ground 99,107, the client's alone 6,360, its ground mark visible 126,319, to allies only 2;
  `trigger`, bit 5, on none.
- A monster's `m_flags`, `MonsterFlags`, bits 0 to 18: a summon slot 4,655, a bomb slot 8, boss 204, mini-boss 306, quest
  monster 2,839, fast animations 617, plays 4,925, tackles 4,770, is pushed 4,840, switches position 4,851, is switched
  with 5,031, is carried 4,758, uses portals 4,839, idols disabled 0, race's values 102, soul not captured 3,395, hidden
  in the bestiary 48, moves 4,735, controlled by the fight's owner 52.
- Also named there, not in the pack: items' `ItemFlags`, bit 10, on 96% of items ([Items](#items)), being saleable;
  maps' `MapInformationFlags`, bits 0 to 20 the map's capabilities and 21 to 28 others, bit 2 allowing to teleport to the
  map and bit 3 from it ([Data tables](#data-tables)).

**Spells.** Ids 0 to 33,348; 17,005 named, one naming text 0. 10,521 have one level, at most 35; every spell's levels
are the spell's, grades 1 to their count in order. 69 levels name a spell the client lacks, and no spell lists them. 504
spells have a preview zone, which lists no cell. The animations' scripts are set on 11,213 spells, their parameters on
none; 3,982 have an administrator's name.

**Levels.** Ids 1,001 to 87,625. AP from 0 to 7,222, the least range 0 to 16, the most 0 to 63; a critical chance on
9,984 levels, -1 to 252; stacks -1 to 70, casts a turn -1 to 63, a target 0 to 63, global casts a turn 0 to 6 and a
target 0 or 1; the interval 0 to 63, 63 on 193 levels; initial and global cooldowns -1 to 63; the least level 0 to 1,001.
The criterion on states is set on 2,411 levels, 435 distinct, of `HS=` and `HS!` and a state joined by `&` and `|`;
`ignorePrisonGlyph` on 58. Effects 1 to 465 a level, critical effects on 10,006 levels, up to 73; preview zones on 317
levels, 469, each two zones and two masks.

**Effects of a level.** 126,767, 25,381 critical, 126,306 kept. Each names an effect of `EffectsDataRoot` by its
`actionId`, which the client's code types by its `ActionId` enum of 1,029 named actions; 408 are a spell's. The base
effect, target and modifier are 0 on every one, `displayZero` false, and the spell its level's. Target masks, 12,413
distinct, are letters and numbers joined by `,`, such as `a,A` or `*E` and a state, 71 kinds of token; triggers, 815
distinct, codes joined by `|`, `I` on 114,432; durations -1 to 59; a random weight on 9,296 effects, in groups up to 144;
dispel 1, 2 or 3; delays up to 63; elements -1 to 5. The dice name ids for some effects: a summon (181) a monster and its
grade, 4,821 of 4,825 a monster the client holds; an effect casting a spell (1160, 792 and others) the spell and its
grade; setting, removing and disabling a state (950 to 952) the state. Masks, criteria and these effects name a state
67,396 times, 94 lacking.

**Zones.** A shape, a character the client's code names (`SpellZoneShape`, 29 shapes), 26 on effects, its cells covered
as `SpellZones` reads them (D187); the client's code names a class for 14 of them, its methods obfuscated: `P` 86,233, `C`
13,386, `a` 6,975, `X` 5,936, `;` 3,955, the others fewer. `param1` is the size and `param2` the least size, as the
code's getters read them. Damage decreases 10% a step on 123,695 zones, over 4 steps at most on 124,027; the zone stops
at a target on 274, is forced in a direction on 236, takes in the carried on 86,548, keeps to the line of sight on 268.
12,717 effects' zones list cells, up to 161, 19,823 in all in the pack: a custom zone's, 3,955, cells of a map by their
ids, 1 to 559; a whole map's, 8,758, one number, 1. `LineFromCaster` sets `param1` to 0 to 5 and `param2` to 0 to 63,
1 and 63 on 160 of its 322: the first and the count of its cells from the caster, the other way round.

**Effects.** 884, ids 0 to 4,076: a characteristic on 213, changed by `+`, `-` or `/` on 383; categories 0 to 9; an
element on 118; boost on 251, active on 172, used in fights on 31; an opposite on 146, every one held; a description on
857, 27 none. The client's `effectId`, -1 on 486, is not measured and not kept.

**States.** 6,377, ids 0 to 7,362, every one named: silent 3,030, the turns shown 165, invulnerable 26, cannot be pushed
25 or moved 22, cannot tackle 19 or be tackled 17, cannot switch position 18, invulnerable in melee or at range 14 each,
prevents fights 13, incurable 9, prevents casts 5, deals no damage 5, main state 5. Effects on 3,142 states, 3,301,
number 16 on 3,031; a spell level on 206, 2 the client lacks.

**Monsters.** Ids 31 to 8,388, held in 16 bits; 1 to 20 grades, 5 on 4,031, 26,821 in all, numbered from 1 in order,
each naming its monster; levels 1 to 2,400; a starting spell, a spell level, on 10,163 grades, 6 the client lacks; the
bonus a summon takes on 277 grades of 105 monsters. 14,469 spells, up to 40 a monster, 37 the client lacks; each with a
text of pairs, the spell's grade and the monster grade's level, one per grade on 13,407, and 1,062 with more pairs, of no
level. Characteristic ratios on 4,970 monsters, ten pairs each, a characteristic and a ratio, for the ten
characteristics a formula scales; `scaleGradeRef` 1 to 11, 5 on 4,303.

**Lua formulas.** 38: a monster's experience, the scaling of its characteristics, a level's cap, experience tables, a
group's bonus, and guilds', alliances' and seasons'. Each declares its parameters, such as `monster_level`, `stat_ratio`,
`stat_base` and `monster_grade_level`, and returns `main()`. Characteristics name four: life and vitality one, strength,
wisdom, chance, agility, intelligence and damage in percent another, MP and range one each; AP none. Written again in
the measuring tool, at each grade's level with the monster's ratio, they give the grade's life on 20,639 of 26,339 grades,
its vitality on 26,170, its strength on 5,878, chance 5,775, wisdom 2,603, damage in percent 41: the grades hold values
of their own, which the server gives a fighter ([the game, fought](#the-game-fought)).

**Breeds.** 19, ids 1 to 18 and 20, each named by a text its `shortNameId` writes as text; 22 spells each, every one in
a variant of the breed's; points per characteristic as pairs of a threshold and a cost, 4 for strength, intelligence,
chance and agility, 1 for vitality and wisdom; 8 roles each.

**Variants and pairs.** 431 variants, two spells each, every spell held, none in two: 22 for each breed, and 13 of breed
19, which `BreedsDataRoot` lacks, the spells every character has, *Maîtrise des Invocations* and *Liberté des
Invocations* among them, variant 481. The 644 pairs name and illustrate the custom mode's: `CustomModeBreedSpellsDataRoot`
names 79 of them by breed, `ForgettableSpellsDataRoot` none, and no pair names a spell.

**Bombs and walls.** 10 bombs, keyed by monster: 7 are monsters, whose spells and walls the client holds; the other 3
name 9 spells and 2 walls it lacks. 8 walls, each a spell, 7 held.

**Characteristics.** 123, ids -1 to 199; ten scaled by a formula; 121 named, one naming a text the client lacks.

## Texts

The five files share one Ankama format, little-endian throughout:

1. A language code as a string with a 7-bit encoded length: `fr` in every file, whatever its language.
2. An `int32` count, then that many pairs of an `int32` text identifier and an `int32` offset.
3. An `int32` count, then that many pairs of a 64-bit key hash and an `int32` offset.
4. The texts, UTF-8 strings with a 7-bit encoded length, which the offsets point to from the start of the file.

Every file has 339,912 numbered texts, identifiers 1 to 1,200,860, and 7,501 keyed ones, 347,413 distinct texts; every
offset starts a text. The texts take 24.0 MiB in French and 21.7 MiB in English.

Packs keep, in French and English, the numbered texts the extracted tables name (D25, D29): 26,411 per language,
21,554 of them items' names. The 15,409 map positions name 3,135 distinct texts, and 12,274 positions name text 0,
which no file holds: those maps have no name.

## Icons

`Picto` holds BC7 textures, one level, their texels in each bundle's `.resS`, bottom row first, each with a sprite of the
same name at 100 pixels per unit, named by the icon's identifier, which items and jobs name as `iconId`.

| Bundle | Textures | Sizes |
| --- | ---: | --- |
| `Picto/Items/item_assets_1x.bundle` | 12,279 | 64x64 but for 3; one RGBA32 |
| `Picto/Items/item_assets_2x.bundle` | 12,279 | 128x128 but for 6 |
| `Picto/UI/job_assets_1x.bundle` | 21 | 32x32 |
| `Picto/UI/job_assets_2x.bundle` | 21 | 64x64 |

- An item's sprite covers its texture whole, but draws it through a mesh trimmed to its opaque texels, whose bounds are
  smaller; a profession's covers it whole.
- **What a harvest names**: the 254 items the sub-areas list as harvestable and the 118 items skills gather, 85 of them
  professions' resources and 33 `Base`'s, 287 in all, drawn by 121 icons, every one at 64 pixels in the 1x bundle. 22 of
  the 23 jobs name an icon, 21 in all, jobs 75 and 78 sharing one; `Base` names −1, which no bundle holds.
- **Sizes**, for the 121 items' icons at 1x: 1,982,464 bytes of RGBA, 474,157 compressed by zstd 19 icon by icon, 463,574
  all at once, 500,449 to 620,331 as PNG; at 2x, 1,251,326 compressed. The professions' at 2x: 344,064 bytes of RGBA,
  128,688 compressed.
- The extractor keeps the items' at 1x and the professions' at 2x, decoded, as raw RGBA (D135 to D137).

## The pack of 3.7.12.12

Format 6 (D219): 7,452,604 bytes, 17,738 entries, the weapons' 110 KB serialized and about 20 KB compressed, read in
under 10 ms; extracted in about 22 s. Format 5 (D182): 7,432,757 bytes, 17,737 entries, extracted in about 22 s on 16
threads, 14 s of which read the client. Two extractions give the same SHA-256. The pack records the extractor's version, so another build of the extractor writes
other bytes. Format 4 (D139) was 5,382,920 bytes; format 3 (D29, D30) 4,670,325, 4,670,336 from session 2d's extractor
(D33).

| Entries | Serialized | Compressed, zstd 19 |
| --- | ---: | ---: |
| `texts/fr`, `texts/en` | 3.37 MB, 1.73 in format 4 | 0.81 MB, 0.41 in format 4 |
| 17,722 `maps/<id>` | 90.7 MB, about 5.1 KB a map | 3.82 MB, 3.72 in format 3 |
| `spells/catalog` | 10.24 MB | 1.29 MB |
| `monsters/catalog` | 6.38 MB | 0.33 MB |
| `world/graph` | 1.42 MB | 0.25 MB |
| `world/atlas` | 0.41 MB | 0.06 MB |
| `items/catalog` | 0.37 MB | 0.13 MB |
| `items/weapons`, format 6 | 0.11 MB | about 0.02 MB |
| `icons/catalog` | 2.33 MB | 0.58 MB |
| `spells/states`, `effects/catalog`, `breeds/catalog`, `characteristics/catalog` | 0.18 MB | 0.04 MB |
| `jobs/catalog`, `interactives/catalog`, `banks/catalog` | 13 KB | 4 KB |

Opening it reads the index in about 8 ms. The icons read in 15 ms, the spells in 30 ms, the monsters in 12 ms. A map reads
in about 7 µs, the graph in 4 ms, the atlas and the items in about 1 ms each, a language's texts in 6 ms, the other tables
in under 1 ms.

**Dictionary, measured again.** Maps are now 80% of the pack. A zstd dictionary trained on them, deterministic and in
1.9 s, saves at best 0.45 MB with 16 KiB (3.27 MB of maps and dictionary instead of 3.72), 0.41 MB with 64 KiB and 0.35
MB with 256 KiB; decompressing every map then takes 41 ms instead of 63. Trained on every entry but the texts, it gains
nothing on the others. D30 keeps packs without one.

## Connection

How the client connects, which D34 and then D35 build on. Measured on 2026-10-27 from the installation, the client's
logs, the configuration its `ConfigUrl` names, the names in its IL2CPP metadata, and the connections of four clients of
this build in game for about 50 minutes, each logged in to its own account. Session 3b then changed `ConfigUrl` and
recorded the connection phase of two clients through D3B's relay ([on the wire](#the-connection-phase-on-the-wire)), and
session 3f took their game connection through it too ([the game connection](#the-game-connection)).

### Launch

The Ankama launcher starts the client. `zaap.yml` and `zaap-windows.yml` give its arguments and settings, and
`maxInstances: true` lets several clients run at once. `zaap-start.bat` runs `Dofus.exe -logFile <log>`, the log being
`dofus.<account id>.log` in the launcher's `gamesLogs\dofus-dofus3` folder, then the arguments:

| Argument | Value, measured in 14 logs |
| --- | --- |
| `--port` | The launcher's local port, which the client connects to on `127.0.0.1`; the same in every log. |
| `--gameName`, `--gameRelease` | `dofus`, `dofus3`. |
| `--instanceId` | A number counting the launches, from 1. |
| `--hash` | A UUID, new at each launch. |
| `--canLogin`, `--langCode` | `true`, the language setting. |
| `--autoConnectType` | The setting of how far the client logs in by itself: 0 to the server choice, 1 to the character choice, 2 into the game; 1 by default. |
| `--connectionPort` | The setting "Connection port": 5555, the default, or 443. |
| `--hdReady`, `--4kReady` | Present when the matching texture pack is set. |

The client logs its command line, hash included, and its connection to the launcher's port, then little else: the log
names no host it connects to. Through that connection (`Ankama.LauncherConnection`) it asks the launcher for its
login token, settings and language.

### Configuration

The `ConfigUrl` line of `version` names a JSON file on Ankama's CDN, over HTTPS: 768 bytes and 15 keys on 2026-10-27.
`connectionHosts` lists the connection hosts as `<name>:<host>:<ports>`: one entry, with ports `5555,443`. The others
name the chat's host and port (6337), Haapi, the shop, the website and content URLs. The client's configuration class
has a property for 14 of the keys; `avatarUrlFormat` has none. The settings the client keeps per player include
`autoConnectType`, `connectionPort` and `lastHostSelected`.

The connection host's name is an alias of an AWS network load balancer with three IPv4 addresses, the same through
four resolvers.

**How the client loads it**, measured by changing `ConfigUrl`:

- The client reads `ConfigUrl` from its version file at start and loads the URL with `UnityWebRequest`, in a method its
  log names `LoadExternalConfiguration`, before it shows anything.
- An `http:` URL fails before any request: "Non-secure network connections disabled in Player Settings", then
  `InvalidOperationException: Insecure connection not allowed`. The client stays on that error; a loopback address
  makes no difference.
- A `file:` URL to a local JSON file is read: the client connects to the connection host it names, an IPv4 address
  included. D35 builds on it.
- The launcher started the client with the changed file, without repairing it; it rewrites the file when it updates the
  client.
- Neither `ConfigUrl` nor `config.json` appears in clear in the IL2CPP metadata; the only `ConfigUrl` of the
  installation is Unity Analytics', in `UnityPlayer.dll`. The URL comes from the version file nonetheless, as the change
  shows.

### Connections

Every client in game held:

| Peer | Port | Connections | Use |
| --- | ---: | ---: | --- |
| A game server | 5555 | 1 | The game. Its address is none of the connection host's; the four clients, all on one game server, share it. |
| The configuration's chat host | 6337 | 1 | The chat. |
| The launcher, on `127.0.0.1` | `--port` | 1 | The launcher's local service. |
| `127.0.0.1`, its own process | | 2 | A pair of sockets within the process. |
| Ankama's CDN, over IPv6 | 443 | 4 or 5 | HTTPS, closing. |

No connection to the connection host remains in game. So the client connects twice: first to the connection host of
its configuration, on its connection port; then to a game server, whose address the first connection sends. The
references name the message carrying it, with a ticket, the host and the ports (D3); in this build its type is
obfuscated. Each client took 2.0 to 2.1 GiB of working set.

### Network code

From the names in the metadata, with no decompilation:

- `Ankama.Dofus.Protocol.Connection` and `Ankama.Dofus.Protocol.Game` hold the protocol. They are assemblies, not
  namespaces: their types have none ([Protocol code](#protocol-code)).
- Google.Protobuf is there with its varint and delimited-message helpers, and a constant `type.ankama.com`, the prefix
  of the type URLs the references read in `Any` envelopes.
- `Ankama.SpinConnection` is Ankama's own transport over TCP: a header with a message size and type, authentication
  by token (`SpinCredentials`), heartbeats, pings, LZ4 compression, and an option to use TLS. The references use it
  for the chat only; the connection phase does not use it ([on the wire](#the-connection-phase-on-the-wire)).

### The connection phase on the wire

Three connections of two clients of 3.7.12.12, on port 5555, recorded by D3B's relay and described by
`scripts/capture.cs` (D37). By structure only: a capture holds the account's login ticket.

- **No TLS.** No connection opens with a TLS record, and every byte falls in a frame.
- **Framing.** Every byte is in frames of a varint length, then that many bytes, as the references read it (D38); each
  read of the relay ended on a frame.
- **Protobuf.** Every frame parses whole as a protobuf message. A client frame is field 1 at its root, a server frame
  field 2; inside, a field 1 of one byte, then one field whose number says the exchange: 3 for the first, 4 for the
  second. No `Any` with a `type.ankama.com` URL appears in this phase.

| Exchange | Client | Server |
| --- | --- | --- |
| First, on connecting | 126 bytes: nested fields, among them texts of 64, 36 and 9 bytes | 455 bytes, 275 ms later: nested fields, among them two repeated fields of 14 and 6 entries |
| Second, on choosing a server and playing | 10 bytes | 83 bytes, 29 ms later |

- **Closing.** The server closed the connection as soon as it had answered the second exchange, then the client did: the
  game connection goes on alone. Between the two exchanges, the connection stayed open and silent for the two minutes
  the player spent choosing. Another connection was closed by its client 40 s after the first exchange, for a reason not
  measured.

Measured on 2026-10-28 by decoding that server selection's answer ([decoded](#the-connection-phase-decoded)), and on
four clients of this build in game:

- **The game server's host** is a DNS name of three labels, 32 characters, not an address. It resolves to three IPv4
  addresses, as the connection host does: a load balancer too.
- **The ports** sent are two, 5555 then 443, the connection host's two ports.
- **In game**, each of the four clients held one connection to a game server, all four on port 5555; one of them to an
  address of the host above.

Session 3f measured what this left open: the bytes of the game connection, which side speaks first on it, whether the
client takes an address for the game server's host, port 443 and how the client chooses among the ports it is sent
([the game connection](#the-game-connection)).

```powershell
Get-NetTCPConnection -OwningProcess (Get-Process Dofus).Id | Sort-Object OwningProcess, RemotePort  # connections
Resolve-DnsName <connection host> -DnsOnly                                                            # addresses
```

### The connection phase, read by the schema

The same three captures, read with the schema of 3.7.12.12 (`scripts/capture.cs --frames --schema`, D40), by structure
only. 96 of the schema's 2,213 messages, `google.protobuf.Any` included, read all 8 frames, since a field typed as
text or bytes reads any length-delimited value; one types all 356 of their fields, as many as their shapes show, and
the next 8. That one is the connection's root:

- **The root**, a message of the connection assembly: one oneof of three messages, field 1 the client's, field 2 the
  server's, field 3 one the captures do not hold.
- **Each side's message**: a string, field 1, the one-byte text of every frame, then a oneof of messages, fields 2 to 8
  for the client and 2 to 7 for the server. The exchange is the oneof's field, 3 then 4 on both sides, as on the wire.

| Exchange | Client | Server |
| --- | --- | --- |
| First, field 3 | Three strings, of 64, 36 and 9 bytes, one in a nested message beside an empty one | One message: an `int64`, strings of 8, 4 and 25 bytes, an empty message, and a message holding two lists of messages, 14 and 6 entries, of enums, `int32` and strings |
| Second, field 4 | One `int32` | One message: two strings of 32 bytes and a packed list of `int32`, 4 bytes |

The server's second answer has the shape the references give the game server's address, a ticket, a host and its
ports (D34); session 3e decodes it.

### The connection phase, mapped

The connection file of 3.7.12.12's schema against D3B's readable protocol (D42 to D45), with `scripts/mapping.cs`, by
structure only.

**Against the references.** The file holds the messages of `dofus-unity-proto`, an older build's, their fields at the
same numbers and of the same types, but for what this build changed or added; BillBot's copy, newer, already has the
success's field 8 and the server list's `bool`, and six rights flags:

| Where | `dofus-unity-proto` | 3.7.12.12 |
| --- | --- | --- |
| The connection's event | Two members, a server and a server update | One, at number 1, shaped as their server update |
| The identification's success | 7 fields | 9: at 8 the status forcing an account answers with, at 9 a message shaped as a friend's entry |
| Its rights | 5 flags | 4 |
| The identification's failure reason | 14 values | 15 |
| The server selection's error | 4 values | 5 |
| The server list | A slot limit at 2, an `int32` | At 2 a list of pairs of a server type and an `int32`, at 3 a `bool` |
| A server | A `bool` at 3 | At 3 an enum of 7 values, the server type the server list's pairs use too |

**Shapes.** 5 of the 37 messages are empty: the ping, the pong, the error forcing an account, and the requests releasing
an account and asking for the friend list. Three more share one shape, two strings and a list of `int32`: a friend's
entry, the field 9 above, and the server selection's success. 4 of the 9 enums have a count of values no other has: 19,
15, 7 and 5. The structure alone cannot tell these messages apart; where they are used does.

**Matching.**

- From the anchors of session 3b's three captures, 8 frames: the root types 356 fields, the next best message 8. They
  anchor 7 messages and 6 fields: the root, each side's message, and each side's message of the two exchanges. Fields
  then pair the other 30 messages and all 9 enums.
- From pins and structure alone, without captures: the same pairs. Scores pair 29 messages and the 4 enums of their own
  count of values, and fields the rest, the empty and alike messages among them.
- 3.7.12.12 needs no pin.

**Audit.** 37 messages, 92 fields, 9 enums and 60 values checked: each has its counterpart, and the build has no field
or value the readable protocol lacks.

**The mapping** is 3,305 bytes, the same bytes on two runs, written in 0.2 s once the tool is built.

### The connection phase, decoded

The connection phase through D3B's readable classes, decoded once (D47), by structure only:

- **Session 3b's captures.** Their 8 frames decode as connection messages, none holds a field the readable protocol
  lacks, and each encodes back to the very bytes it came as.
- **Through the host.** A client of 3.7.12.12 started from the launcher logged in and chose a server through the host,
  which had audited the build's mapping (D48). The host logged its connection phase (D49), below: every frame
  understood whole, in a connection of 4.7 s, 138 bytes from the client and 541 from the server. The game connection
  that follows does not go through D3B yet.

| Frame | Side | Bytes |
| --- | --- | ---: |
| `request.identification.token` | Client | 126 |
| `response.identification.success` | Server | 455 |
| `request.server_selection` | Client | 10 |
| `response.server_selection.success` | Server | 83 |

### The game connection

Measured on 2026-10-28 on clients of 3.7.12.12 started from the launcher, through the host, whose relay rewrote the
server selection's answer to its game listener (D51): four connection phases and their four game connections, the last
two from two accounts at once. By structure only: `scripts/capture.cs`, and where in a frame the ticket lies, never its
value.

- **An address for the host.** Sent D3B's address, 127.0.13.59, in place of the game server's name, the client connected
  there: it takes an address.
- **The ports.** The answer lists 5555 then 443, with the connection port set at 5555 as at 443. Sent the game
  listener's 5556 then 5557 in their place, the client connected to the first, with either setting; sent 5556 alone, to
  it. The setting chooses the connection phase's port only. Whether the client tries the next port when the first
  refuses it is not measured.
- **Port 443.** With the setting at 443, the connection phase goes to the connection host on 443, in the clear and framed
  as on 5555, its four frames of the same sizes. The game connection then goes to the first port the answer lists.
- **Who speaks first: the client.** Its first frame, 76 bytes, goes as soon as it is connected, unprompted; the server's
  first bytes come one round trip later, 35 ms, in each of the four connections.
- **The ticket.** That first frame holds the ticket the answer gave, 32 characters, as a string four fields down, field
  4.2.2.1 from the root, in each of the four; the client's next two frames do not hold it. D52 matches the connection
  with its server by it.
- **On the wire.** No TLS: every byte, both sides, in varint frames, each a protobuf message of the game assembly that
  the schema reads whole as its root. Each client frame is the root's field 4, an `int32` then a `google.protobuf.Any`,
  whose type URL is 19 bytes, `type.ankama.com/` and a type's three letters; the ticket is a field of the message in the
  first one's `Any`. Each server frame is the root's field 3, an `Any`, but one per connection, the root's field 1, shaped
  as a client frame.

| Game connection | Connection port | Game ports sent | Duration | Frames, client and server | Bytes, client and server |
| --- | ---: | --- | ---: | ---: | ---: |
| First | 5555 | 5556, 5557 | 32 s | 28, 157 | 1,368, 131,603 |
| Second | 443 | 5556, 5557 | 23 s | 28, 142 | 1,371, 130,238 |
| Third, with the fourth | 5555 | 5556 | 31 s | 28, 147 | 1,371, 132,100 |
| Fourth, another account | 5555 | 5556 | 25 s | 37, 127 | 1,754, 95,643 |

Client frames are 36 to 267 bytes, server frames 25 to 80,966. The third and fourth went to two game servers, each
matched by its ticket. Every connection phase ended 2.8 to 4.1 s after it began, the server closing once it had
answered, and its game connection opened about as it closed.

### The game, read by the schema

Session 3f's four game connections, 694 frames, read with the schema of 3.7.12.12 by `scripts/mapping.cs` (D57), by
structure and type URLs only:

- **The root**, a message of the game assembly, reads every frame and types 2,745 of their fields; the next best, 1,263.
  Its oneof holds a response at 1, an `int32` then an `Any`; an event at 3, an `Any` then a list of `int32`; a request at
  4, an `int32` then an `Any`. Its field 2, of the event's type and outside the oneof, is never set, nor is the event's
  list. The client sends requests only, the server events, and one response per connection.
- **The Any.** Every one's type URL is `type.ankama.com/` and a message of the game file, and its value reads whole as
  that message: 121 messages in all, 27 the client sends and 94 the server. None is a field's type in the file.
- **The structure.** The file holds 2,175 messages, 1,372 of which are no field's type: those frames carry. 758 of these
  have a shape, through all they hold, no other has; the largest sets of one shape are 117 empty messages, 73 holding one
  `int32`, 48 one `bool`, 37 one `int64` and 28 two `int32`. Of the 121 carried, 65 have a shape of their own.
- **The references' names.** Of the 63 messages the first milestone needs, as the references name them, 2 have their
  shape once in the build, 24 have a shape it no longer holds, and 37 share theirs with 2 to 175 of its messages.
- **Field numbers.** 980 of the file's 982 messages of three fields or more number them 1 to n, 920 in the order they
  declare them, and so do the connection file's. Against the references' older build, 15 of the 18 messages that pair
  alone by the kinds of their fields number them otherwise; the connection file's keep theirs (D42). 110 of the game's
  2,376 messages, map entries included, hold two fields of one message type, where the references hold few: in three
  measured, the envelope, the inventory and the character's selection, the captures set one of the two. The game's fields
  look shuffled and renumbered, with decoys, at each build; a second build will tell (D56, Q10).
- **Sizes.** The map's information holds, through its fields, 142 messages, 36 enums and 679 fields; the character list
  37 messages and 203 fields; the ten largest events on entering the game, 171 messages and 779 fields (D55).

What a game connection holds, in order, the same in all four: the ticket, then the server accepting it; the character
list asked for and given; the character chosen, then its characteristics, inventory, professions and quests; the game
context asked for, replaced, and the current map named; then the map's information asked for and given. Then other
actors walking. None of the four holds a movement of the character, a harvest or the bank; a fifth does
([the game, mapped](#the-game-mapped)).

### The game, mapped

The game file against D3B's readable protocol (D54 to D58), with `scripts/mapping.cs` and the pins of 3.7.12.12, on
session 3f's four game connections and a fifth recorded on 2026-10-28 for this: one account, 58 s, that walked on its
map, changed maps four times, harvested four resources, went into a bank through its door, talked to the banker, opened the
bank, put six stacks in it, took its kamas out and left.

- **What each message is** was measured by structure, by timing, and by values compared on the machine with the
  build's pack, never written down. The list's character ids include the one the selection names; the map the server
  names is the one the client asks about; the map's information gives its elements' ids as the map in the pack holds
  them, each element's interactive among the pack's and each skill among its skills; the professions, skills and items
  are the pack's; the NPC talked to is a banker of the pack, the action 3 its talking (D29), and the reply chosen one of
  its replies.
- **The actions, in order.** Walking: the client's path, the server showing it, then the client's confirmation once
  arrived, which the server answers with a response, not an event. Changing maps: a request naming the next map, then
  the current map and its information as on entering the game. Harvesting: a request naming a skill instance and an
  element, then the use starting for every actor, the element's state at 2 and its skill disabled; three seconds later,
  the use over, the objects obtained, the inventory's stack added or its quantity changed, the weight, the profession's
  experience, the state at 1. The bank: the action on the banker, the dialog and its question, the reply, then the
  dialog over, the exchange with the storage open, its objects and kamas; each stack moved, as a list of one id, then
  the inventory's stack deleted, the storage's updated and the weight; the kamas moved as a negative quantity, then the
  inventory's and the storage's kamas; leaving, then the exchange over.
- **What a stack is.** An object's quantity and position are the reverse of what the first captures suggested: every
  stack obtained or in the bank is at position 63, its quantity varying, and the inventory lists the worn objects
  first, one of each, at other positions.
- **What does not match.** The cell a stated element gives is the pack's for the two resources harvested, not for two
  elements of another map: elements of the neighbouring map, which the information lists too, measured in session 4c
  ([the game, used](#the-game-used)).

**Matching.** 106 messages paired, 43 by pin, 11 by the captures and 52 by fields, and 17 enums by fields. Without pins,
scores pair 9 messages, each as pinned, and the captures 11; fields follow from them.

**Audit.** 106 messages, 217 fields, 17 enums and 128 values checked: no failure and no note; 33 partial messages leave
fields out on purpose (D55). No message the client sends is partial.

**The mapping** is 7,531 bytes, format 2, the same bytes on two runs.

**Translation.** The five game connections decoded through the mapping (D58): of 1,069 frames, 178 understood whole,
69 more read, and 822 carrying one of 106 messages the readable protocol lacks, quests, chat and other players' among
them; none misread.

### The game, acted on

Measured on 2026-10-28 (D60 to D62): the request ids on the six game connections captured, then a client of 3.7.12.12
in game through the host for 26 minutes, one account, the player playing while D3B sent the server requests of its own
through a measuring plugin, never committed. By structure, timing, and values compared on the machine with what the
client sent before and with the build's pack, never written down.

**Request ids.** Every request of the first milestone carries the id -1, in every capture, and so does every response;
of this session's 141 requests, D3B's 18 among them, 9 carry another id, and 1 of session 3g's 90. The client counts
requests of four kinds, which the readable protocol lacks, numbered in turn from 2: one of 264 to 267 bytes right after
the character list, at 2 in every capture; one of 31 or 32 bytes about every four minutes, right after two events of 25
bytes and a request of its own at -1, six times in this session; and two of 265 and 267 bytes together, once, after 24
minutes, at 8 and 9. Nothing else sets the id.

**What D3B sent**, 18 requests, each whole between the client's frames, with the id -1:

| Request | Sent | The server's answer | What the client then sent itself |
| --- | ---: | --- | --- |
| Walk on the map | 6 | 5 walked: the character's movement, 28 to 29 ms later; 1 refused | The confirmation once arrived, 0.46 to 1.77 s after D3B's request, for each walk; the server's response |
| Change maps | 2 | The new map, 28 to 29 ms later | The map's information and its twin, 0.72 to 0.86 s later; then the server's answer |
| Use an element | 5 | 3 harvests: the use started, over 3 s later, the objects, weight and experience; 1 refused; 1 ignored | Nothing |
| Act on an NPC | 1 | The dialog and its question, with two replies | Nothing |
| Reply | 1 | The dialog over, the kamas, the exchange with the storage, its content | Nothing |
| Move a stack into the storage | 1 | The stack gone, the storage's stacks, the weight | Nothing |
| Move kamas | 1 | The storage's kamas and the character's | Nothing |
| Leave the dialog | 1 | The exchange over | Two requests to leave, 9 and 37 ms after the server's answer, which it did not answer |

- **On the screen**, the player saw each: the character walked, changed maps and harvested; the banker's dialog opened
  with no click, then the bank's window; the stack and the kama moved into the bank; the window closed. Between D3B's
  requests, the player walked and harvested by hand, the client answering as usual.
- **A walk.** The request names the path's turning points, each a cell and the direction leaving it, as the client's
  do; the server's movement event lists every cell of the path, without directions. D3B's paths, found in the pack's
  cells, walked as the client's.
- **A use.** The client walked to the walkable cell nearest the element it could reach, 3 cells away, and harvested
  from there. D3B did the same, at 3 and 4 cells, and once from 6 cells, all accepted.
- **The bank.** The NPC's question offered two replies, one of which the pack lists among its banker's: that one opened
  the bank.
- **Refused.** A path that does not start where the character stands: one event of the server, and no walk, so no
  confirmation; measured again in session 4a, the event is 40 bytes and names the character's cell
  ([the game, walked](#the-game-walked)), not 25. A use right after it: two events. A use of an element whose cell in
  the pack is not the one the server's state gives it, an element listed on two neighbouring maps: no answer at all, an
  element of the neighbouring map ([the game, used](#the-game-used)).

`scripts/capture.cs` describes D3B's frames apart from the client's, in captures of format 2 (D61): on this session's,
105 reads of the client, all ending on a frame, and 18 frames of D3B's.

### The game, walked

Measured on 2026-10-28 for session 4a (D66 to D69): the six captures of the game connection, then two more of a client
of 3.7.12.12 in game through the host, the bot walking through its walk behavior, one with the player playing and one
with the player's hands off. By structure, timing, and values compared on the machine with the build's pack, never
written down. The eight connections hold 67 walks the client asked for, 16 paths D3B sent, 3 of them refused, and 24
stops.

- **Cells.** Every one of the 797 cells of the character's paths the server showed is walkable in the pack, and the
  neighbour of the one before it, in a grid of 14 columns and 40 rows whose odd rows lie half a cell east of the even
  ones: from an even row, the diagonals are 13, 14, -14 and -15 cells away, from an odd one 14, 15, -13 and -14; east and
  west 1, south and north 28. Each of the 310 turning points of the requests, the client's and D3B's, leaves its cell in
  the direction this geometry gives, from 0, east, clockwise to 7. The floor never differs between two cells of a path,
  any actor's, 2,723 steps. The movements of NPCs and monsters skip cells, and six of other players' steps do.
- **Between two cells.** A step east, south, west or north passes between two cells. Over the character's 372 such
  steps, one or both of the cells beside it were walkable, but once, on a path D3B sent in session 3h, which the server
  walked: the client's paths never pass between two cells that cannot be walked on.
- **How long.** On every path of three steps or more the character runs: a step east or west takes about 170 ms, north
  or south 110 and along a diagonal 113, within 66 ms over a whole walk, at most 186 ms a step. A single step is walked,
  400 to 491 ms; no path of two steps was measured. The client
  confirms 0.38 to 2.9 s after its request, and the server answers the confirmation 28 to 30 ms later, once 100 ms.
- **The client's paths against D3B's.** Found again in the pack's cells with these weights, 3, 2 and 2, D3B's path from
  the start to the end of each of the client's 67 walks is as quick on 61 and quicker on 6, never slower; it is the same
  path on 20.
- **The refusal.** A path that does not start where the character stands: one event, 40 bytes, 29 ms after the request,
  and nothing else. It carries two numbers, the coordinates of the cell the character stands on, x the column plus half
  the row rounded up and y the column less half the row rounded down; all three refusals named that cell. One came
  while the player's own walk went on: the server named the walk's first cell, so it places a walking character there
  until the walk is over, and the client stopped its walk on that cell. The readable protocol names it
  `MapMovementRefusedEvent`, pinned among the 6 messages of its shape the captures carry.
- **The player's stop.** When the player gives a new path during a walk, the client sends a request naming the cell
  where the character stopped on its path, then the new path from that cell, in the same read, within 1 ms; the server
  shows the new path and answers the stop with nothing. All 24 stops were followed by the new path. The readable protocol
  names it `MapMovementCancelRequest`, pinned among the 9 messages of its shape the captures carry.
- **A map changed by a walk.** A walk ending on a cell that changes maps gets the new map before the response to the
  confirmation: the server names the map, then answers.
- **The bot walking.** With the player's hands off, the walk behavior sent 7 paths, 1 to 16 steps; the server walked each
  exactly as sent, 28 to 147 ms after the request, the client confirmed each 0.38 to 1.81 s after it, and the server
  answered every confirmation, the walk over 0.41 to 1.84 s after the request with the character on the path's last
  cell in the bot's state. A path sent from another cell was refused, naming the character's. With the player playing,
  the bot answered `Busy` to each walk asked while the character walked, and a walk it sent was interrupted when the
  player gave a new path 300 ms in: the stop and the new path came, and the bot paused.

### The game, between maps

Measured on 2026-10-28 for session 4b (D71 to D74): the eight captures of the game connection, then three more of a
client of 3.7.12.12 in game through the host, the player's hands off, a measuring plugin, never committed, asking the
bot to change maps, to ask for refused ones, and to travel. By structure, timing, and values compared on the machine with
the build's pack, never written down. The character rode a mount in this session's; the walks' durations are a
mounted character's, and D69's delays still bound a walking one.

- **A scroll, by the client.** 12 of the client's changes: it walks to a cell of the edge, and 4 to 6 ms after the
  server answered its confirmation asks for the neighbour; the server names the map 28 to 29 ms later, then the client
  asks what is on it, 0.6 to 1.3 s after, with its twin, and the server answers 29 to 101 ms later. Each cell it left from
  lies on the edge and carries a mark of that direction or of a diagonal next to it, one only the south-east for the
  east; the transition's own cell once. Scrolls with an action asked for the neighbour and landed on it, those of the
  captures.
- **A map action.** A walk ending on its cell gets, once confirmed, the new map, then the response to the confirmation,
  in the same millisecond, and no request from the client. The server's actions cover cells next to the graph's: the
  client walked onto 424 for an action on 409, three times, and the bot onto 154 for one on 140, twice; the graph's own
  cells moved it too, two of them, one with a direction.
- **D3B's changes.** 30 requests for a map: 23 answered with the new map 27 to 29 ms later, always with an event of 36
  bytes the readable protocol lacks, and on some maps another; 6 answered with nothing, from a cell off the edge, for the
  neighbour in another direction, for a map that is no neighbour, twice once a map action had already moved the
  character, and from a south-west corner marked for the south; 1 answered with the walk's refusal of D69, right after a
  walk the client never confirmed. A cell of the east edge marked nowhere was accepted; a south-west corner marked only
  south-east, on another map, was accepted for the west, and the server sent the character elsewhere than the neighbour.
- **What the client follows.** The bot's walk sent 1 ms after a new map's information, then one 50 ms after: the server
  walked each, the client never confirmed either. The server then had the character at the walk's end: the next path,
  from its start, was refused naming its last cell, which also placed the bot's state there. Walks sent 100 ms after,
  twice, 200, 500 and 1,000 were each confirmed. The client sends nothing when it has drawn a map. The player's own first
  walk on a new map came 207 ms after its information at the soonest.
- **Journeys.** One change: 0.7 s from a cell of the edge. Six changes: 27 s, one refused from a corner and the path found
  again through another map. Seven: 21.7 s, every change answered. 65 frames of D3B's, each whole between the client's.

The eleven game connections decoded through the mapping: of 5,040 frames, 737 understood whole, 988 more read, and 3,315
carrying one of 123 messages the readable protocol lacks; none misread.

### The game, used

Measured on 2026-10-28 for session 4c (D75 to D77): the captures of sessions 3g and 3h, then two game connections of a
client of 3.7.12.12 in game through the host, the player's hands off, 15 and 2 minutes, a measuring plugin, never
committed, sending uses from chosen cells, then the final behaviors. By structure, timing, and values compared on the
machine with the build's pack, never written down. The character rode a mount.

**The client's uses**, in the captures: four of skills of range 1, a tree on a walkable cell among them, each from a
walkable neighbour of the element's cell, the first its walk reached; two fishing uses, no neighbour walkable, from one
of the walkable cells nearest the water, 2 and 3 away; the bank's door from its west neighbour, 2 away, its own cell
walkable. It asks 1 to 5 ms after the server answered its confirmation; the server starts the use 28 ms later.

**What the server takes**, by the distance that sums the coordinates' differences:

| Skill | Range | Taken | Refused |
| --- | ---: | --- | --- |
| Fishing, 124 to 126 | 10 | 2, 3, 4, 6 and 10 away; 4 away with a cell that blocks sight on the straight line between | 11 away, three times, one only 6 along either coordinate |
| The door, 184 | 1 | From its west neighbour, 2 away, and from its own cell | 2 away off its neighbours, 3 away |

**Refusals.** An event the readable protocol names `InteractiveUseErrorEvent`, 28 ms after, then nothing: two for a use
from too far, the first carrying the skill's id where the second carries the instance's, five times; one carrying the
instance for a skill the element does not offer now, a resource harvested, twice, and for an instance it does not know.
Nothing at all for an element the map does not list, and for one of a neighbouring map.

**The neighbouring maps' elements.** The map's information lists, with the map's own, elements of the maps next to it,
a field set apart, which the references name `on_current_map`. In the 11 captures before this session, 104 of 343
interactive elements and 92 of 232 stated ones are marked another map's; each of those 92 stands on another cell than
the pack's, every other on the pack's. Every element listed has its id on the current map in the pack too, mostly on
another cell. The server answered nothing to a use of such an element, from next to either cell, three times. Their
updates come too: an element's skills with the field set as in the information, 109 of 110 times; an element's state
without it, 110 times.

**A harvest.** The element's skill disabled, the use started for every actor with its duration in tenths of a second,
15 or 30, the element's state at 2; the end that long later, to the millisecond, then, within 2 ms and in no set order,
the objects, the weight, the objects obtained, the profession's experience, the state at 1 and the skill still disabled.
The client sends nothing and walked on 5 to 6 ms after. A walk sent during a harvest: refused, naming the character's
cell, and the harvest went on to its end.

**A door.** The use, with no duration and its one boolean set, `can_move` in the references, the new map and an event of
36 bytes the readable protocol lacks, in the same millisecond; the client asks what is on the new map 545 to 627 ms
later. The bank of Astrub's door offers three skills, 184 among them. Out of the bank, a map action leads onto the
door's cell.

**The client draws the map first.** A door used as soon as the server named the new map, before the client asked what is
on it: the server took it, and the client asked for both maps' information in turn, the server answering the second
only. 55 s later the bot's next walk was shown and never confirmed, the player's next path refused, naming that walk's
end, and the player's path after that walked and confirmed from there (D77).

**The bot using.** Two harvests by the final behavior, the walk then the use, 3.5 and 3.6 s each; a journey of four map
changes into the bank by its door, 11 s, every walk 533 to 549 ms after a new map's information and confirmed; out by
the map action.

The 13 game connections decoded through mapping 4: of 6,483 frames, 1,012 understood whole, 1,284 more read, and 4,187
carrying one of 123 messages the readable protocol lacks; none misread.

### The game, banked

Measured on 2026-10-28 for session 4d (D79 to D82): the captures of sessions 3g and 3h, then two game connections of a
client of 3.7.12.12 in game through the host, the player's hands off but where told, 24 and 10 minutes, a measuring
plugin, never committed, sending the requests one by one, then the final behavior. By structure, timing, and values
compared on the machine with the build's pack, never written down. The character rode a mount.

**Opening.** The action on the banker, NPC action 3, naming the map and the NPC's actor id: the dialog, then its
question with two replies, in the same millisecond, 28 ms later, the character 10 cells from the banker, every time. One
reply is the banker's in the pack; the other asks a question offering none, the dialog going on. The banker's reply: the
dialog over, a text, the character's kamas less as many as the storage holds stacks, the exchange with the storage, of
the type 15 and as many stacks as an `int32` holds, and its content, all within 2 ms.

**Putting in.** A request listing stacks: the inventory's stacks deleted, those of the storage they joined, and the
weight, in one millisecond, 28 ms later. Of a list of two stacks, both; of a resource and a quest's ticket, the resource
alone; of the ticket alone, nothing. Kamas: the storage's kamas then the character's when put in, the reverse when taken
out. The stacks' own ids change from one game connection to the next.

**Refusals**, each 28 ms after its request when there is one:

| Request | When | The server's answer |
| --- | --- | --- |
| Action on an NPC | The map lacks it, or a dialog or the bank is open | One empty event, named `NpcActionErrorEvent` |
| Action on an NPC | Naming another map than the character's | Nothing |
| Action 1 on the banker | | An exchange's error, not named |
| Reply | Not offered | The question again, and a text of the error kind |
| Reply, stacks, kamas | Outside the dialog or the exchange | Nothing |
| Stacks | None the server takes, or none the inventory holds | Nothing |
| Kamas | None, more than the character or the storage holds | Nothing |
| Walk | During the dialog or the exchange | The refusal of a walk, naming the character's cell; the dialog or exchange goes on |
| Leaving | Outside a dialog | Nothing |

**Leaving.** D3B's request to leave: the exchange over, 28 ms later, then the client's two requests to leave by itself,
1 to 178 ms after, six times, the server answering neither; none once, after a bank D3B opened and left in 161 ms. The
player closing the banker's dialog or the bank: the client's request while it is open, then the end, 28 to 60 ms later,
and nothing more.

**The bot banking.** From inside the bank: two stacks and one kama put in and the bank left in 161 ms. From two maps
away: the journey through the door, the action on the banker 529 ms after the inside's information, one kama put in, and
the bank left, in 3.9 s. The player closing the bank the bot had opened and kept open interrupted the bot, which paused.

**A walk the client did not follow.** On the second connection, the bot's walk from the bank's inside onto its map
action's cell, 81 s after the inside's information, was walked by the server and never shown nor confirmed by the
client; the player's next path was refused, naming the walk's end, and the client then walked from there. The next such
walk, after the player's, was not followed either; a walk to another cell of the inside was, and three minutes later the
same walk out of the bank was. D3B's walks out of the bank were followed five times in session 4c, but once after a door
used too soon (D77). The cause is not found; it came back in session 4e, and the state now takes the walk's last cell,
where the server has the character ([the game, teleported](#the-game-teleported), D85).

The 15 game connections decoded through mapping 5: of 8,851 frames, 1,226 understood whole, 1,615 more read, and 6,010
carrying one of 175 messages the readable protocol lacks; none misread.

### The game, teleported

Measured on 2026-10-28 for session 4e (D83 to D85): a game connection of the player using zaaps, the haven bag and the
map, 9 minutes, then two of a client of 3.7.12.12 in game through the host, the player's hands off, 23 and 2 minutes, a
measuring plugin, never committed, sending the requests one by one, then the final behaviors. By structure, timing, and
values compared on the machine with the build's pack, never written down. The character rode a mount.

**Zaaps known.** On entering the game, among the events that describe the character, the server lists the maps of the
zaaps it knows, 30; it lists them again, whole, 31, as the character entered the map of a zaap it did not know, with a
text of the information kind. Using a zaap teaches nothing more.

**A zaap.** Its element, interactive 16, "Zaap", offers skill 114, "Utiliser". The server starts the use with no
duration and, in the same millisecond, lists the destinations: each zaap the character knows with its map, sub-area and
cost, the one it stands at with no cost, and four of another type, on zaaps' maps, carrying more. The list opens a
dialog: leaving it, the player or D3B, ends it with a dialog's end of type 10, 28 ms later, the client sending nothing
more; travelling ends it too. A walk during it is refused, naming the character's cell.

**Travelling.** One request naming the zaap's map and what the character travels by: 0 a map's zaap, 3 the haven bag's,
5 the map. The server answers 28 to 33 ms later, once 96, in one millisecond, with the new map, the kamas less what it
cost, a text of the information kind telling it, and the dialog over; the client asks what is on the map 0.66 to 1.36 s
later, after D3B's teleports as after the player's. With a list open, the type the request names does not count: the
haven bag's list took one of type 0. With none open, type 0 gets no answer, and type 5 travels: the client sends it when
the player chooses a zaap on its map, after asking what it costs, which the server answers; D3B's, sent without asking,
were taken.

**From where.** Type 5 was taken from 6 maps, a zaap's, the bank of Astrub's inside, and maps with no zaap, each of
whose flags carries bit 3; it was refused from an inside of Amakna's castle, without it, and from the haven bag, with
it. Of the graph's 11,327 vertices, 10,587 carry bit 3, every zaap's map among them; 136 carry bit 2 alone and 604
neither. One zaap's map, the Craqueleurs mountain's, has two zones.

**What it costs.** Ten kamas a whole coordinate of the straight line between the map the character stands on and the
zaap's: the 120 costs of four lists, from four maps, and the 13 teleports paid, but towards Incarnam's three zaaps, on
another world map, a thousand more. A bank's inside, on no world map, costs from its own coordinates. The haven bag's
list costs from the map the bag was entered from. Bubble's formula, ten kamas a tenth of a coordinate, gives costs that
are not tens, where every cost the server gave is.

**The haven bag.** The client enters it with a request naming the character, its owner, and the server answers 33 ms
later with the bag's map, which the pack holds at 0,0, in a sub-area of its own and off the world graph; the map's
information holds the bag's owner, in a oneof the other maps leave unset. The same request leaves it, back to the map it
was entered from. Its zaap lists the zaaps the character knows; teleporting from the map is refused there. The bag
opened on 3 maps whose flags carry bit 2, and was refused on 2 without it.

**Refusals**, 28 ms after the request, one text of the error kind, then nothing:

| Request | When | Text |
| --- | --- | --- |
| Teleport | During a dialog, such as a zaap's list, or a walk the client did not confirm | 474 |
| Teleport | To a map the character knows no zaap on | 1759 |
| Teleport from the map | From a map without bit 3, or from the haven bag | 416 |
| Haven bag | From a map without bit 2 | 471 |
| Teleport of type 0 | With no list open | Nothing at all |

**A walk the client did not follow.** A journey's walk shown 0.53 s after the information of a map the client had never
drawn, 14 steps along a row of plain cells, was walked by the server and never confirmed; the server then answered a
teleport and the haven bag as busy, 9 and 11 s later. A walk sent 5 minutes later from the walk's last cell was walked,
and the client first walked its character there from where it had kept it, 11 steps in 1.9 s, then confirmed. Of the 104
walks D3B sent in the 18 captures, the client confirmed 97, the player stopped one, and the client did not follow 6: two
shown 30 and 90 ms after a map's information (D73), one after a door used too soon (D77), two in the bank of Astrub 81
and 301 s after its information ([the game, banked](#the-game-banked)), and this one (D85). Of those sent 0.5 to 0.6 s
after a map's information, it confirmed 36 of 37.

**A path down a stair.** A walk D3B sent across Sufokia's zaap's map, 29 cells whose floors went 130, 100, 70, then 0,
was refused at once, naming the cell it started from, where the character stood. The paths of session 4a never changed
floor between two cells, 2,723 steps (D68), and D3B's cell paths did not weigh floors: which changes the server takes
was measured in session 4f, this step from 70 on a stair to 0 off it refused
([the game, interrupted](#the-game-interrupted), D86). The journey left that change out and went another way.

**The bot teleporting.** Through the host, the player's hands off: from an inside of Amakna's castle, out by its door, a
teleport to Astrub's zaap, then a scroll, 3 changes in 6.1 s; from the haven bag to Sufokia's zaap, the bag left then a
teleport, in 2.9 s; from there to the nearest banker, a teleport to the Cœur immaculé's zaap, a scroll and a door, in
6.6 s. Every walk was confirmed, and every cost was the one the rule gives.

The 18 game connections decoded through mapping 6: of 11,504 frames, 1,570 understood whole, 2,120 more read, and 7,814
carrying one of 181 messages the readable protocol lacks; none misread.

### The game, interrupted

Measured on 2026-10-28 for session 4f (D86 to D89): the pack, the 18 captures of the game connection before this
session, then four game connections of a client of 3.7.12.12 in game through the host, 16, 2, 7 and 1 minutes, the
player's hands off but where told, a measuring plugin, never committed, sending chosen paths, then running an activity.
By structure, timing, and values compared on the machine with the build's pack, never written down. The character rode
a mount.

**Floors.** A cell's moveZone is 0 or 1, 1 on 9,800 cells; 633 maps hold both, 469 of them in the world graph, and
their cells of moveZone 1 are stairs: on Sufokia's zaap's map, the floors 130, 100, 70, 40, 10 and 0 of two stairs
between a level of floor 130 and one of floor 0. Every walkable cell's floor is a multiple of 10. In the captures, the
player walked steps of 10 to 60 on maps of one moveZone; on maps of both, the player's client and others' walked steps
of 30 along a stair and of 10 between a stair and a cell off it. Paths D3B sent, each ending on the step tried, each
refused one sent again without it and walked:

| Map | Step | Walked | Refused |
| --- | --- | --- | --- |
| Both moveZones | Along a stair, both cells of moveZone 1 | 130 up and down, 30, 20 | Nothing |
| Both moveZones | Between moveZones 0 and 1 | 10 | 20, 30, 40 up and down, 70, 130 |
| Both moveZones | Between two cells of moveZone 0 | 10 | 20, 40, 130 |
| One moveZone | Any | 170 | Nothing |

Every refusal named the path's first cell, 28 to 136 ms after the request, and the client confirmed every path walked,
0.5 to 2.4 s after. On the pack, the difference of floors and that of their absolute values agree on every pair of
walkable neighbours of the 633 maps; the rule leaves out 5,655 of them. With D3B's paths not weighing floors, a journey
off the upper level of Sufokia's zaap's map got three refusals and failed; with the rule, it arrived in 5.3 s.

**A fight.** Two, the player attacking monsters: the client walked to the group, then sent its confirmation and a
request of 49 bytes the readable protocol lacks in one read. The server answered the confirmation, then, in 2 ms,
destroyed the context and created the one of value 1, named the fight's own map, of the pack, off the world graph and
at 0,0, as the haven bag is, and sent events of a fight. The client asked for that map's information and got the
fight's, no map information. In the fight, the client moved the character with the walk's request, on the fight's map.
The end, 24 and 45 s later: the loot, the weight, the context destroyed and created of value 0, the map the fight began
on, then its information, placing the character where the fight began.

**A disconnection.** The client closed 1.71 s into a walk of 15 steps, before confirming it: the server closed too. On
its new connection, 22 s later, the character stood on the walk's first cell, and the next path, sent from there, was
walked and confirmed. A request to walk naming no cell, D3B's, was refused by naming the character's cell, then the
server closed the game connection, 83 ms after the request.

**A walk the server cuts.** On the map below Sufokia's zaap's stairs, the path to the south edge passes a map action's
cell, 469, next to where the character arrives: the server showed the walk only to that cell. Of 43 such walks, the
client confirmed 24 at once, 0.43 to 0.44 s later, and the server answered with a refusal naming that cell, the new map
and the response, in one millisecond, as after a map action; the 18 others went unconfirmed until D3B sent another walk
3 s later, which the server refused by naming the first cell of the cut walk; the next walk from there was shown, the
client confirmed 0.5 s later, and the server answered with a refusal, the new map of the map action and the response.
Both kinds were sent 0.50 to 0.51 s after the map's information, and alternated, mostly, from one journey to the next.

**An activity.** A patrol between that map and Sufokia's zaap's, through the stairs, one journey a step: 85 journeys,
every one arrived, 1.5 to 6.7 s each. Full, it went to the nearest bank in two map changes, put a kama in and left,
6.6 s, then went on. The player's walk interrupted its journey; resumed 22 s later, it recovered in 2 ms and went on.
The client closed during a journey: the new connection 20 s later took the bot back, and the activity went on 2.2 s
after the server named the map, with no command.

The 22 game connections decoded through mapping 6: of 14,750 frames, 2,178 understood whole, 2,618 more read, and
9,954 carrying one of 185 messages the readable protocol lacks; none misread.

### The game, from the app

Measured on 2026-10-29 for session 5a (D91 to D94): two game connections of a client of 3.7.12.12 through the app, 43
and 47 s, the bots' host in the app's process, a measuring plugin, never committed, whose patrol goes between the
character's map and a neighbour by a scroll, started, stopped and resumed from the app's bots page.

- **The host in the app.** Built, its pack opened, its mapping loaded and its relay listening: 17 s on the first start
  after a build, 16 of them loading the mapping, while the window showed the host getting ready; 0.4 s on the next.
- **The bot** showed on the page once the server confirmed the character the client chose.
- **The patrol**, started from the page: 12 journeys arrived, 1.4 to 2.7 s each. The player's walk during a journey
  paused the bot twice, the banner shown; resumed from the page 14 and 5 s later, it recovered and went on, the next
  journey arriving 2.5 s after. Stopped from the page, it ended at once.
- **Closing the app** gave the client its configuration back, closed the game connection, and the app exited with 0;
  the client lost its connection to the game, as D91 says.

Measured on 2026-10-29 for session 5e (D110): two game connections of a client of 3.7.12.12 through the app, 32 s and
1 min 59 s, the scripts' folder holding the sample stroll and a script failing at its second step.

- **The scripts** were listed, neither failing to read.
- **The stroll**, started from the card with its 10 walks: 8 walks arrived, then the stop from the card ended it at
  once, during the 9th, its request sent: 9 frames from D3B. Run whole, 10 walks arrived. Started again,
  the player's click during its second walk sent `MapMovementCancelRequest` then `MapMovementRequest`, which paused the
  bot; resumed from the card 8 s later, it walked from the player's cell, 213, the script's counter at its second walk,
  and ended `Done`: 21 frames from D3B on that connection.
- **The failing script** failed at its second step, `erreur:10: attempt to index a nil value (local 'nothing')`: the
  chunk is named without `.lua`.
- **Closing the app** gave the client its configuration back, then the game connection closed.

Measured the same day for the logs and the inspector (D111 to D115): a game connection of 1 min 26 s, the bot's journal
opened from its card, the stroll run whole, then again, paused by the player's click during its third walk and resumed
from the card, 21 frames from D3B. No warning or error in the host's log. The owner went through the pages, journal,
filters, inspector live and a capture replayed, and reported no problem.

- **The capture replayed** through the inspector's reader, by structure and counts only: 298 frames, none cut. The
  client's 59, 32 understood and 27 not read; the server's 218, 36 understood, 33 read and 149 not read; D3B's 21, all
  understood. Every frame not read showed its shape. `AuthenticationRequest` came first, its ticket hidden, its language
  shown; the server confirmed the character chosen, which ties a connection's frames to its bot.
- **An earlier attempt** that morning: the client closed its connection phase 16 s after its identification was
  answered, without choosing a server, and never came back through D3B; the cause was not found. The launcher had
  updated itself, the client on disk still 3.7.12.12's, its files of 2026-10-20.

Measured on 2026-10-29 for the removal of the Lua scripts (D119): a game connection of a client of 3.7.12.12 through
the app, 55 s, the database a copy holding a profile that names `script:promenade.lua`, played by the character, which
starts it on connection. The owner went through the pages and reported no problem.

- **The host** built its five modules, the data, protocol, network, bots and storage modules, and no scripting module;
  the scripts' folder, `d3b.lua` included, was left as it was, its files' dates unchanged.
- **The profile** did not start on connection: one warning in the host's log, its activity missing, and the line in the
  bot's journal. The card offered it, its activity said gone, and starting it said so; the profiles page showed it gone,
  its editor offering no activity to save it with. No frame from D3B.
- **The inspector** showed the bot's frames live and replayed a capture.
- **Closing the app** gave the client its configuration back, then the game connection closed; the app exited with 0.
- **Not measured**: deleting the profile, still in the copy afterwards; the player's pause, which pauses a bot only while
  one of its behaviors runs, none running without an activity (D118).

Measured on 2026-10-29 for session 5d (D120 to D125): a game connection of a client of 3.7.12.12 through the app, 2 min
49 s, the map page open on the character, which the player moved by hand, no activity started. By structure, counts and
timing, and the maps' ids compared on the machine with the build's pack, never written down. The owner reported the map
following in every case.

- **The frames**: 567, none cut. The client's 101, 43 understood and
  58 not read; the server's 466, 34 understood, 41 read and 391 not read; none from D3B, and nothing in the host's log
  above information.
- **The player**: 13 walks, each confirmed; the server showed 13 more, of other actors, one of them in the fight.
- **8 maps named**, 5 of them distinct, all in the pack: the map of a zaap; the haven bag, entered there and left back
  to it, then entered and left again from a neighbour the client scrolled to, the bag's map off the world graph, at 0,0 on
  no world map; the fight's own map, off the graph at 0,0 on a world map, which got no map information, as before; and,
  once the fight was over, 10 s later, the map of another zaap, neither the map the fight began on nor a neighbour of it.
  How the fight ended is not decoded.
- **The fight's placement**: neither the fight's map nor the map it began on holds a cell marked `red` or `blue`. Of the
  2,877 maps of the pack at 0,0, off the graph and on a world map, as the fight's is, 1,158 hold some; of the graph's
  8,402 maps on a world map elsewhere than 0,0, 643. In the same millisecond as the fight's context and map, the server
  sent 16 frames the readable protocol lacks. The client sent no walk in the fight.

### The game, harvested

Measured on 2026-10-29 for session 6a (D127 to D134): game connections of a client of 3.7.12.12 through the app, over
four runs of the app, captures on, the owner's character harvesting nettles with its profile "Ortie", started from the
card, then on connection. By structure, counts and timing, the maps' ids compared on the machine with the build's pack,
never written down.

- **A tour.** Harvests of skill 68 on one map after another, each element walked to from the cell the client uses it from
  (D75), the tour going on to the nearest map still to go through, one scroll each in the measured part of the zone. A first
  tour went through the zone's houses first, through doors and floors and a journey of 12 changes by a zaap, before its
  first harvest; the tours since go through the maps of the world map first (D128). A journey of 12 changes led to maps of
  the zone's sub-area reachable only far round. Walks the client did not confirm (D85) came back on one map of the zone, the
  journey going another way.
- **Interruptions.** A fight began during the activity, which waited and went on once it was over. The player's click
  during a journey, `MapMovementCancelRequest`, paused the bot; resumed from the card, it went on. The client closed during
  the activity: its new connection took the bot back, and it went on with no command. Stopped from the card, it ended at
  once.
- **On connection.** Three times, the profile started once the client chose the character, its journal saying so (D99).
- **The bank.** Full, the character went to the bank of Bonta: a teleport to its zaap, a scroll, the door into the inside,
  then NPC action 3 on the banker 6394, the Bontarian banker. The player, earlier in a capture, replied 771 ms after the
  question. D3B replied 1 ms after it, the bank opened 29 ms later, the stacks went 6 ms after that, the leaving 39 ms after
  the opening, the whole in 130 ms, and the client sent its two requests to leave 1 and 142 ms after the server's end
  (D81): both windows stayed on the client's screen, the owner reported. Waiting half a second after the question and after the bank opened, the
  reply went at 547 ms, the stacks at 1,085 ms, the leaving at 1,118 ms, the client's requests at 1,152 and 1,179 ms, and the
  windows closed (D132). Then a teleport back to the zone's zaap.
- **Still full.** Once the harvest was put in, 3 stacks, the character still carried 4,958 of 7,590 pods, other things,
  above the share it banks at (D129).

### The game, foretold

Measured on 2026-10-29 for session 6b (D138, D142): a game connection of a client of 3.7.12.12 through the app, captures
on, nothing learned before, the owner's profile "Ortie", nettle and sage in a sub-area of Amakna, replayed against the
build's pack, each map's forecast reckoned from what the maps entered before it taught. By structure and counts, never the
maps' ids.

- **Learned**: 43 maps entered in 3 minutes, 35 graphics learned, which the database kept for the next run.
- **Foretold probable**: 18 maps, 13 of which held nettle or sage, 11 harvested. On each of the 5 others, every element
  drawn with a resource's graphic was one the server did not list (Graphics, above).
- **Foretold without a resource**: 5 maps, none holding one.
- **Unknown**: 20 maps, many gone through on the way, 1 holding one.

### The game, fought and grown back

Measured on 2026-10-29 for session 6c (D144, D145): the 34 game connections captured before it, read through mapping 6, by
structure, counts and timing, never the maps' or actors' ids.

- **Fights**: 4. Three the player's: the client walks to the group, then sends its walk's confirmation and a request of 49
  bytes the readable protocol lacks, the attack, in one read; the server destroys the context 30 ms later. One a resource's
  protector, during 6a's activity: the character's harvest ended, then 2 ms later, with no request from the client or D3B,
  the server destroyed the context and created a fight's; over 64.5 s later. The objects, the weight and the profession's
  experience came between the harvest's end and the fight. An event of 38 bytes, a single `int64`, comes right before each
  fight, but often on its own too: it does not give the fight's kind. No aggression was captured, and how a fight ended
  is not decoded.
- **Growing back**: the server never says when an element will offer its skill again; it shows it, the element updated, and
  on entering a map. No growing back was seen whole, the character never staying on a map that long. Bounds, the element
  harvested by any actor and found grown back on a later visit: nettle (skill 68) at most 445 s after, the least of 7 bounds;
  trout (125) at most 564 s after, the least of 7; gudgeon (124) at most 1,557 s after. Trout seen still harvested 297 s after,
  the character having stayed on the map since. A bound found on a visit holds from above only: another player may have
  harvested the element meanwhile, unseen.
- **Durations**: every harvest lasted 3 s, 30 tenths; drinking water from a well, 1.5 s.

Measured the same day on the pack of 3.7.12.12 (D147, D149, D150):

- **Zones that fall apart.** Of the 186 sub-areas listing resources, 67 fall apart through the transitions between their own
  maps, 588 of their 8,062 maps lying off their sub-area's main part, the part holding the most maps. From the main part,
  through the whole world graph and every transition, criteria aside: 19 maps are 2 changes away, 18 are 3, 17 are 4, 128
  are 5 to 35, and 416 are not reached at all, only by a teleport or a door whose criterion the graph keeps. Sub-area 601
  holds 222 maps, 141 in its main part, in 25 parts.
- **Base's resources.** 34 skills of `Base` gather 33 items no profession gathers, among them drinking water (skill 102,
  "Puiser"), potatoes, shells, crystals and events' items such as "Marron Glacé"; all of level 1. No sub-area lists them.
  The graphics of every map of the world graph, 10,981 maps, are 3,345 graphics drawn on 24,579 pairs of a map and a
  graphic, indexed in 564 ms.
- **The guild's chest.** Graphic 70671, which JondoEmu names the banks' guild chest, draws 21 elements on 21 maps of the
  world graph, 20 of them insides on no world map; measured through the server below.
- **Resource bags.** Item type 100, "Sac de ressources", holds 73 items, such as "Sac de blé" and "Sac de Houblon".

### The game, kept up

Measured on 2026-10-29 for session 6c (D147, D148, D153): two game connections of a client of 3.7.12.12 through the app,
captures on. On the first, the bot harvested for 8 minutes, a fight in between. On the second, 3 minutes by hand: a fight the
owner began and left, food eaten one at a time, then several at once, then at full life; a stack of two resource bags and a
barrel opened; then the guild's chest of Astrub's bank opened, stacks put in whole and in part, taken back, and objects
refused. By structure, timing, and values compared on the machine with the build's pack and with each other, never written
down. The owner's guild has one tab, and the character every right in it.

**The life.** The characteristics (`CharacterStatsEvent`) list about 120 characteristics by id. The server sends them
twice on entering the game, after the character is chosen without what its objects give, then once the context is
created; then as a fight starts and ends, and after food heals. A characteristic's value is the sum of its numbers. The
most life is the value of characteristic 0 plus that of 11, the vitality; the life, the most plus the value of 97, which is
negative, the life lacking, or 0 at full life. Out of fights the life regenerates a point each interval
`LifeRegenStartEvent` gives, in tenths of a second: 5 in all 48 of them, two points a second. The characteristics hold the
life as the regeneration started; `LifeRegenEndEvent`, as a fight starts and before food heals, gives the life and the most
then, and the points regenerated since, counted beyond the most. On the 12 ends of 6 captures, its most was the
characteristics', its life theirs plus the points regenerated, up to the most, and those points the time since at two a
second, every time. Sitting changed nothing, the owner saw; no capture holds it.

**Eating.** `ObjectUseRequest` names the stack. The server answers 28 ms later, once 183 ms: the regeneration's end and its
start again, the stack's new quantity, a text 1 of the information kind, the characteristics holding the life healed, and
the weight. Each of four breads healed the same. At full life: a text 225 of the information kind, nothing used, and
nothing else, four times. `ObjectUseMultipleRequest` uses several of a stack at once, naming the stack and a quantity, with
a message no capture sets, holding a map and a list: asked for the whole stack below full life, the server used as many as
the life lacked, the last in part, in one answer; at full life, the text 225 alone (D156).

**A container.** The same request on a stack of two resource bags, type 100: what the bag holds added to the inventory's
stack of that resource, a text 21 of the information kind, the bags' quantity and the weight; the second time, the bags'
stack deleted (`ObjectDeletedEvent`). A barrel of type 184, "Conteneur", opened the same, what it held a new stack. The
inventory gives each object's effects, which the readable protocol does not read: effect 209 names what the bags and the
barrel hold, item and quantity, as the server then gave them.

**The guild's chest.** Its element, interactive 388 "Coffre de guilde", the pack's graphic 70671, offers skill 184 in the
three bank insides captured. Used: the use, then in the same millisecond an event the readable protocol lacks holding a
list of one entry, the exchange of type 21 (`ExchangeStartedWithTabbedStorageEvent`, two numbers not measured), what the
chest holds as the bank's content gives it, with no kamas (`StorageContentEvent`), and two events the readable protocol
lacks holding strings. `ExchangeObjectMoveRequest` moves a stack, whole or in part: by the inventory's stack and a positive
quantity into the chest, by the chest's stack and a negative one out of it. The server answers 28 ms later with the chest's
stack as it now is (`StorageObjectUpdateEvent`), the inventory's stack deleted, changed or added, and the weight; a chest's
stack taken out whole is removed (`StorageObjectRemovedEvent`). A stack put in gets an id of its own in the chest, and one
taken out a new id in the inventory.

| Request | When | The server's answer |
| --- | --- | --- |
| A stack into the chest | An object bound to the account, a cape and a companion whose effects 981 and 983 no other object carries | A text 356 of the error kind, nothing moved, three times |
| Leaving | The chest open | The exchange over, of the storage's dialog type, 28 ms later, as the bank's |

Not measured: changing tabs and a tab's rights, the guild having one tab; a full chest; kamas; the bank's list of stacks
(`ExchangeTransferFromInventoryRequest`) on the chest, and `ExchangeObjectMoveRequest` in the bank.

**Matching.** 129 messages paired, 61 by pin, 11 by the captures and 57 by fields, and 19 enums by fields. **Audit.** 129
messages, 263 fields, 19 enums and 147 values checked: no failure and no note; 46 partial messages. **The mapping** is 8,926
bytes, format 7, the same bytes on two runs. **Translation.** The 36 game connections decoded through it: of 25,258 frames,
4,953 understood whole, 4,026 more read, and 16,279 carrying one of 206 messages the readable protocol lacks; none misread.

With `ObjectUseMultipleRequest` (D156): 132 messages paired, 62 by pin, 11 by the captures and 59 by fields, the map's entry
among them; 132 messages, 270 fields, 19 enums and 147 values audited, no failure and no note; the mapping 9,150 bytes,
format 8, the same bytes on two runs; the 37 game connections, of 26,090 frames, 5,219 understood whole, 4,164 more read,
and 16,707 carrying one of 206 messages the readable protocol lacks; none misread.

### The game, fought

Measured on 2026-10-30 for session 9a (D178 to D180): the 40 game connections of 3.7.12.12 captured, 11 fights among them,
4 the owner played with a Steamer for this session and 7 earlier, a Xelor's among them: 9 the player's attacks and 2
resources' protectors', which name the character the attacker too. By structure, timing, and values compared on the machine with the build's pack, never the actors' ids or names.
`scripts/capture.cs --messages --frames --schema` lists them, message by message.

- **The start.** The client walks to the group and sends, in one read, its walk's confirmation and the attack's request,
  the group's id. The server destroys the context and creates a fight's, 29 to 33 ms later (D88), then sends every fighter's
  disposition, the character's with each monster's in turn. The client asks for the fight's map, the server answers with
  the fight's start, who attacked whom, of type 4, the join, a placement of 440 to 444 tenths of a second at most, each
  fighter shown, both teams' placement cells and the turns' order.
- **Placement.** 8 or 12 cells a team; the attacking character, of team 0, stands on its list's first, the monsters, of team
  1, on theirs. The server's list for team 0 is the second, its other the first; the 15 cells the player moved to were all
  in it, each of the 14 requests answered with every fighter's disposition 28 to 33 ms later. The 11 fights' maps are in the pack, and
  none holds a cell flagged red or blue (D180). The client sends its readiness; the server answers whose, then starts the
  fight within the same millisecond.
- **The fighters.** A character with its breed and level, a monster with its id, grade and level, each with its team,
  whether it lives, and its characteristics: a character's AP and MP usable, its base in one number and what it spent this
  turn taken off in another, its life 0 plus 11 as out of fights; a monster's whole values, 0 alone of the life's. Monsters and
  summons have ids from -1 down.
- **Turns.** As the fight starts, the server gives the character's spells as the context the client plays in, and the
  first round. Each turn it names the fighter and the turn's length: 300 and 340 tenths for the two characters, 290 for a
  monster, 150 for a summon, 10 for a dead fighter, whose turn passes at once; to the character's client alone it then lets
  it play. It gives each fighter's AP and MP as its turn begins. It asks the client whether it is ready for the next turn,
  49 times: the client answered ready each time, 3 ms to 3.5 s later, 0.6 s halfway. The player ended turns early 16 times.
- **Sequences.** 466 in the captures, nested: a spell's (0), a weapon's (1, once), what an action triggers (3), a walk (4),
  a turn's start (6) and end (7), and the spells every fighter casts as the fight starts (8). The client acknowledged 120
  of the 124 sequences of the character's that nest in none, never a nested one, and 17 of other fighters' spells.
- **Actions.** Each by the effect's id: 344 casts, 1 weapon's blow, 80 AP spent and 32 MP spent, 64 losses of life from
  93 to 100 by element, 1 a push's and 1 of effect 2828, 19 heals, 9 teleports, 15 swaps, 23 deaths, 6 summons, 4 changes of
  look, and 17 actions of 4 other kinds, not named. A walk in a fight is a walk's request and event, framed in a sequence of its own with the MP it spent, and
  the client never confirms it. A loss names its element, 0 neutral to 4 air, -1 for a push's, the life lost, a monster's
  whole life when one blow killed it, and the part of it lost for the fight. A teleport's cell is where the fighter's next
  walk starts. A death is followed by the turns' order, the dead fighter kept in it, marked.
- **Summons.** A Steamer's turrets, each with its monster's id and grade, its characteristics, its summoner, and its id and
  cell in a position of its own. The server said each turret just summoned was not controlled; a turret was summoned again
  under another monster's id, its own id the same; in the last fight, the server gave the client a turret's context before
  the turret's turn, which the owner says a third evolution makes controlled (D177).
- **The end.** The last death, the client's readiness, then the objects and kamas gained, the characteristics, the
  experience, and the results: each fighter's outcome, 2 won and 0 lost, the character's level, its experience between
  its level's floor and the next's, and what the fight gave, and its loot, objects grouped by the monster they came from,
  and kamas. Then the roleplay's context and the map the fight began on. The character won 8 fights and lost 3, all three
  the player's attacks: after each loss, a text, 34, and another map than the fight began on, the same twice, from which
  the harvest travelled back to its zone.
- **No capture holds** an aggression, a push that moved a fighter, a mark, a state, an invisible fighter, or a fighter
  carried.
- **Against the pack**, measured for session 9b (D184): the 344 casts name a level of their spell by its id, 52 spells at
  grades 1 to 6; the 54 monster fighters, of 17 monsters, a grade of their monster at its level, with its life, AP and
  MP; the 6 summons a grade of theirs; 14 of the 16 groups of loot a monster of the pack, the other 2 monster 0, one
  object each that no monster dropped.

### The game, cast

Measured on 2026-10-30 for session 9c (D185 to D191): 49 fights, the 11 of the 40 earlier game connections and 38 of 12
the owner captured for 9c with 11 breeds at low levels, replayed through the bot's fight state and the pack of format 5,
each cast and walk checked against `D3B.Engine`'s rules, with a measuring tool never committed that prints counts.

- **Casts chosen and cast by effects.** Of 977 casts, 396 are the first cast of a sequence of a spell (0) the caster
  authored: the casts the fighters chose, 231 the characters' and 165 monsters' and summons'. The other 581 are cast by
  effects: at the fight's start, in its sequence (8); at a turn's start, in what it triggers (3); or after the first within
  a cast's sequence, as a passive answers it or a spell casts another (effect 1160). A weapon's blow is a sequence of its
  own (1), twice.
- **The rules** accept 392 of the 396: each within its level's range, 10 only once the caster's range bonus,
  characteristic 19, boosts it; the 78 of a level cast in line in line; 4 at a range of 1 on the diagonal neighbour, at a
  distance of 2, so that a diagonal counts its steps; the 286 testing the line of sight, 199 with cells between, unblocked
  by the cells' data or a fighter, 44 of them past a corner beside a cell that blocks; every cell cast on walkable in a
  fight; 40 needing a free cell on a free one, 44 a taken one on a fighter; the AP each costs at most what the caster had;
  275 limited a turn and 149 a target, 78 with an interval, 10 cast again after it, 3 with an initial cooldown, all within;
  42 with a criterion on states met, a turret's `HS=135` by the state the server gave it just before. The 4 others an
  Eliotrope cast through its portals, beyond the range from its cell.
- **A carrier.** A Pandawa carries (action 50: the carried fighter and its cell) and throws (51: the fighter and the cell
  it lands on): carrying, it is in state 3, *Porteur*, which prevents casting, and the carried in state 8, *Porté*; it
  cast 4 times so, by levels whose criterion asks `HS=3`. The throw ends both states, the server removing none.
- **Spell modifiers.** Buffs of effects 280 to 299, `#1 : +#3 Portée maximale` and the like, name the spell they change
  and the value: a Pandawa's give its carrying spell 3 more range, no taken cell needed and a free one. 3 casts carried a
  modifier of their spell.
- **Zones and masks.** Of 388 losses of life, 380 lie in the zone of a cast of their sequence, chosen or triggered, whose
  mask selects the fighter; 4 the Eliotrope's masks of portals (`R`, `r`) leave unknown, 1 hit a fighter carried, on no
  cell, and 3 came with an Osamodas's summons or a triggered spell. 15 shapes explain losses alone: point 161, cross 74,
  circle 41, perpendicular line 16, diagonal cross 13, line 11, square 10, cross without centre 7, fork 4, star 3,
  diagonal perpendicular line 3, line from the caster 2, half circle 2, cone 1; and the whole map. Of 343 fighters an
  effect damaging at once foresaw, 342 lost life. A heal may go to the caster, a steal's, and spell 24576 teleports its
  caster to the cell cast on: what an effect does to whom is 9d's.
- **Walks.** The 298 walks, each a list of every cell: every step to a cell at a distance of 1, walkable in a fight and
  free, the server taking one MP a step. Slides (action 5: the fighter, its cell and the cell it slides to), 36, move
  fighters too.
- **Tackle.** Leaving enemies, a fighter tackled hears the tackle (104), then each characteristic lowered and its loss,
  AP (101) and MP (127), then its step (129). 3 walks were tackled: 3 AP and 2 MP lost of 6 and 3, dodge 0 against lock 0;
  2 and 1 of 7 and 4, dodge 6 against 4; 1 and 1 of 2 and 4, a monster's dodge 1 against the character's lock 0. 6 lost
  nothing: monsters whose monster does not tackle, characteristic 78 the dodge and 79 the lock.
- **States and buffs.** `FightEffectsEvent`, as BillBot's mapping names it, carries a buff: its action, its source, and
  in the buff its value, its target, the spell it changes and the spell that gave it, its triggers and a uid; 950 sets a
  state. `RefreshCooldownEvent` carries a fighter's spells' cooldowns. Action 406 removes the buffs a spell gave a fighter,
  132 all of its buffs. Read raw by the measuring tool, never written down; 9d names them.
- **No refusal.** The client never sends a cast or a walk its own rules refuse: no capture holds the server refusing one,
  so what D3B refuses is measured only where the server accepted (Q16).

The 40 game connections decoded through mapping 9: of 29,428 frames, 8,134 understood whole, 5,219 more read, and 16,075
carrying one of 187 messages the readable protocol lacks; none misread. The mapping pairs 192 messages, 88 by pin, 11 by
the captures and 93 by fields, and 25 enums; 405 fields and 183 values audited, no failure; 12,783 bytes, format 9, the
same bytes on two runs.

### The game, foreseen

Measured on 2026-10-30 for session 9d (D193 to D196): the 52 game connections of 3.7.12.12, 49 fights and 50,755 frames,
decoded through mapping 10, replayed through the bot's fight state and the pack of format 5 by `scripts/fights.cs`,
which prints counts.

- **Actions named**, by the client's names of their ids: a slide (5), a carry (50) and a throw (51), a tackle (104), AP and
  MP dodged (308, 309), a spell's buffs removed (406), all (132), one by its uid (514), beside those of 9a. A loss's
  second field is what a shield took: with a shield up, the life lost was 0. A cast's field 8 says a critical hit, on 33
  of the 398 casts the fighters chose; its field 1, set on 15, is something else, not measured.
- **Buffs**, 843: each names its fighter, the spell's effect it comes from by its uid, every one in the pack, the spell
  and its grade, its kind (a boost, a state, a spell modifier, a triggered effect), a value and a parameter, its triggers,
  and the round and turn it ends at, the turn the place in the turns' order of the fighter playing as it was given. 113 of
  the 125 buffs for rounds a chosen cast's own effects gave end at the cast's round plus the effect's duration. No buff's
  end of time is announced; a removal before it is, by spell, all, or uid.
- **Cooldowns**: the character's and its summons' spells, each with the rounds it waits: 197 of 213 counts of spells cast
  are the interval less the caster's turns ended since, 16 one fewer, sent as the turn ended.
- **Positions**: the 36 slides and the 298 walks start on the cell the fight's state holds their fighter on, after every
  walk, slide, teleport, swap, carry and throw before.
- **Rules**: of the 398 casts chosen, 392 pass with the states and modifiers the state keeps; 4 an Eliotrope cast through
  its portals, 2 a bomb with AP the state holds spent.
- **Damage**, of each target by element, against what D3B foresaw of the cast: of the casts alone, 334 of 335 within the
  range, 99 of them the target's whole life, by element 84 air, 46 earth, 108 fire, 49 neutral and 47 water; the other one
  1 more under two buffs of final damage. With the spells the effects cast, which are not foreseen: 51 within, 7 more,
  1 less, 14 not foreseen.
- **Heals**: 27 within, 53 foreseen on a fighter whose life was whole, which the server does not announce, 2 not
  announced. 79 steals, each healing its caster half the life it took.
- **Buffs a cast's own effects gave**: 20 boosts and 77 deboosts within their roll, 19 states as foreseen, 4 the states
  of a carry, which the server sets itself; 20 went to a fighter the cast was not foreseen to reach, 12 are triggered
  effects kept, and 314 come from the effects the cast triggered.
- **Pushes and pulls**: 46 foreseen, 7 as far, 1 farther, 21 stopped short, 17 on a target that died first; their damage
  9 times as foreseen, twice less.
- **AP and MP removed**: 5 compared, 1 within with a dodge, 4 fewer.
- **Teleports**: 3 foreseen and not announced; summons and teleports onto a cell reach no fighter, and are not compared.

### The game, weighed

Measured on 2026-10-30 for session 9e2 (D205 to D214): the 61 game connections of 3.7.12.12 before 9e2 and 9 the owner
captured by hand for it, 77 fights, replayed with `scripts/fights.cs`, `--turns` and `--live`, which print counts.

- **Critical hits.** A cast's chance of one is its level's plus the caster's characteristic 18, in percent, within 0 and
  100: of 726 casts of levels with a chance, 64 foreseen and 57 drawn; none of the 217 casts of monsters whose
  characteristic 18 is below the level's chance, nor of a level without a chance. A monster's characteristic 18 is often
  negative.
- **The enemies' blows between two of the character's turns**: on the 61 captures, 4,710 of the character's life and 231
  of its summons'; on the owner's 9e2 fights, 1,866 and 1,102. The reply D3B imagines takes 4,745 and 262, then 1,623
  and 1,083; its foresight off by 1,377 on 84 turns, each turn's rolls and the monsters' choices. The monsters cast one
  or two spells a turn.
- **The client's previews of buffs.** Effects for the client only that boost or set a state on the caster: of 26 on the
  captures, 1 boost and 1 state given as previewed, 24 not, among them all of spell 14574's 3 rounds of damage and
  heals, whose server gives the caster nothing.
- **The owner's 9e2 fights**, by hand, characters of level 20 to 150 and their summons: of the casts alone, 91 losses
  within what D3B foresaw, 13 within capped, 4 below; pushes 9 as far as foreseen and 11 stopped short, pulls 5 and 3; a
  push's damage 8 times as foreseen and 3 none as foreseen; heals on a wounded ally 8 within and 2 capped; steals 7;
  swaps 4; AP and MP removed 1 and 4 below. Of 208 casts chosen, 197 pass D3B's rules; the server let through 8 cast for
  want of AP by a breed whose spells' cost falls with a state it gathers, states 2484 on, 2 out of range by a breed
  whose states 3737, 7021 and 7022 change its spells, and 1 beyond the summons.
- **Weapons.** None of the 180 turns of the characters captured holds a weapon's blow.

### The game, fought by D3B

Measured on 2026-10-30 for session 9e (D197 to D203): the game connections the owner captured with D3B's host and app while
the AI played, 13 fights, a Xelor at level 91 among the characters, and the 66 fights captured in all, replayed with
`scripts/fights.cs` and tools never committed that print counts.

- **The character's spells.** `SpellsEvent` lists them as the character enters the game, each with its grade; the server
  sends it again as an alteration transforms the character, its third field the form's spells, 2 for a bouftou. As each
  fight starts, `FightSpellsContextEvent` names a fighter, the character's own in 64 of 65, a turret once, and lists the
  spells the client plays it with: in the 55 fights it listed the character's, its 300 casts were of them, at their grade.
  Spell 0 is a weapon's blow.
- **The fight's options.** As each placement starts, the server says four options unset, `FightOptionUpdateEvent`, twice
  each time one changes; a toggle, `FightOptionToggleRequest`, answered 30 ms later. Measured again for 9e2 (D216), the
  owner watching the AI: option 0 refuses spectators, answered with text 40 of the message kind, and is kept from one
  fight to the next; option 2 closes the fight, answered with text 95. 9e's first reading had them the other way round.
- **D3B's requests.** 177 casts sent, 167 cast; 81 walks, 49 turns' ends, 12 placements, 14 readinesses, 2 options set.
  The client acknowledged D3B's sequences as its own and said it was ready for each turn by itself. A client that rejoins a
  fight under way, its connection back, gets the fight's start, its join and its fighters synchronized, then the turn: no
  `FightStartEvent`.
- **Refusals.** The server refuses a cast with a `TextInformationEvent` of the error kind, 29 ms after the request, text 175
  twice, and nothing else; a Zobal's spells asking a mask's state were refused so. A text of the message kind, 188, came with
  8 casts it made.
- **The client's previews.** Effects flagged `ForClientOnly` are what the client previews, never what the server does: a
  Xelor's Refraction shows a circle of damage the server dealt on none of 16 casts, dealing it through a spell its telefrag
  state triggers. Left out of what D3B foresees, the bench's damage of the casts alone is within on 331, below on 1, and
  foreseen but never dealt on 1, against 300, 32 and 104 before.
- **The player's pace**, on the 49 fights the owner played: 1.3 s halfway from a turn's start to the first action and from
  an action shown to the next, 0.6 s before ending a turn, 1.2 s from the placement's cells to the first move.
- **9e2's fights**, 17 the AI played for 9 breeds (D216): 16 won; 180 casts sent, 176 cast, 4 refused with text 175; 5
  of the character's 81 turns with nothing done; every character's 54 characteristics given at its first turn, one of
  level 150 with none of the four elements'.

### The game, struck

Measured on 2026-10-30 for session 9e3 (D217 to D222): the 80 game connections of 3.7.12.12, 100 fights, with
`scripts/fights.cs` and measuring tools never committed that print counts. They hold 2 weapon's blows, both a sword's.

- **The blow.** The client asks for spell 0 on a cell, `GameActionFightCastRequest` with its spell left out. The server
  answers 28 and 29 ms later with a sequence of type 1, a weapon's, its first action 303: a cast naming the cell and the
  fighter on it, no spell, no level, no critical field. Then the AP the sword costs, 3 and 4, spent, and its effects: a
  neutral steal of 5 to 6 took 5 and healed its caster 2, an earth blow of 16 to 25 took 8, the target's whole life. The
  client acknowledged both sequences. D3B's rules accept both, and its foresight holds them, the steal's heal too.
- **The worn weapon.** Position 1 of the inventory: of 75 inventories, the 63 with a weapon worn held it there, 45 a bow,
  18 a sword, and nothing else was there. Each other position, 0 and 2 to 15, held objects of one super type. A stack
  carries its own effects, its field 7, each an action and its dice; the 6 weapons worn rolled as their item does.
- **Equipping.** `ObjectMovementEvent` moves a stack to a position: 2 in the captures, a pet taken off to 63, and one
  worn at 8 after the client's `ito`, its uid, quantity and position. No weapon changed.
- **Spell 0.** Every list of the character's own spells names it at grade 1, 77 as it entered the game and 96 as a fight
  started; the 16 of a form or a turret do not. Its level is the punch: 3 AP, range 1, neutral 6 to 7, 9 to 10 critical.
- **The weapons' characteristics.** On the 358 turns of the characters captured, 122 and 142, the damage weapons deal and
  receive, were 100, and the server never gave 103, the weapons' power, nor 31, the weapon mastery.
- **The AI.** On the 109 turns replayed of a character wearing a sword D3B strikes, it planned 8 blows; the players
  struck none in those turns.

### The game, masked

Measured on 2026-10-30 for session 9f (D223 to D229): the pack of format 6 and the 80 game connections of 3.7.12.12, 100
fights, 6 of a Zobal and 38 of its turns, with `scripts/fights.cs`, `--turns`, and measuring tools never committed that
print counts.

- **Spells asking a state.** Of the 836 spells and variants of the 19 breeds, 132 have a criterion on states, asking 43
  states, 27 of which the breed's own spells set, directly or through a spell an effect casts. The Forgelance has 41, the
  Zobal 32, the Pandawa 16, the Féca 10, the Ouginak 8; the Enutrof, the Sram and the Sadida none. Most of the others
  forbid state 7, *Pesanteur*. In the captures, 106 casts the fighters chose asked a state, 64 a Zobal's, 21 a Pandawa's,
  7 a Forgelance's, and every one held the state its criterion asks.
- **The Zobal's masks.** States 98, 99 and 100, a mask each, two mask spells each, a spell and its variant. Spell 18633
  sets 98 as each fight starts. A mask, 1 AP every other round, casts the spell taking the three states off where worn,
  through two more of its grades, sets its own state for the whole fight, dispels the buffs of the masks whose state the
  Zobal is not in, changes the Zobal's look, then gives its boosts: AP, MP and dodge, or melee damage and lock. 14 masks
  were cast, 4 of them the mask worn, whose boosts the server ended before giving them anew. 32 of the Zobal's 44 spells
  ask a mask's state; no mask changes a spell's cost or range; two spells deal otherwise by the mask worn, cast in no
  capture.
- **A buff's end.** `juu`, a buff's uid and its fighter, with no action: 1,171 in the captures, 334 as a turn started on
  buffs already over, 184 on buffs already gone, and 653 ending a boost, a state, a spell modifier or a triggered effect
  the fight's state held, the state of a Zobal's old mask among them. Unread, the bot kept the old mask's state: the AI's
  6 casts the server refused with text 175 (D203) asked it. Read, through mapping format 13, they fail their criterion,
  and the 38 other casts the AI asked with a mask's criterion hold.
- **The caster outside the zone.** 32 casts of an effect whose mask names the caster with `c` and not `C`, the caster
  outside the effect's zone, a Zobal's Tortoruga on an enemy among them: none buffed the caster.
- **The states a cast changes**, against what the AI imagines: of the 244 the casts whose effects cast spells at once
  set or ended on living fighters, 111 foreseen, the 20 of the Zobal's 10 changes of mask among them, against 24 without
  the spells effects cast; the others a carry's, the Eliotrope's portals, the Xélor's telefrags and the Roublard's bombs.
- **The AI.** On the Zobal's 38 turns replayed, it puts on the mask whose spells deal more and casts them, Furia in the
  psychopath's mask where the turns played cast Tortoruga on enemies 32 times, and changes mask 29 times against 9.

## Protocol code

What the client's IL2CPP code holds of the protocol, measured with `scripts/schema.cs` (D39, D40), which reads it
with Cpp2IL.Core 2022.1.0-pre-release.21.

- **Opening.** Cpp2IL reads the metadata, version 39, and finds 151 assemblies: about 5 s, then 1.3 s to read the
  protocol, its machine code lifted.
- **Two assemblies** hold the protocol: `Ankama.Dofus.Protocol.Connection`, 115 types, and `Ankama.Dofus.Protocol.Game`,
  5,610. Their types have no namespace and are named by three lowercase letters; their members, enum values included,
  by four. Names the obfuscation keeps: the methods implementing an interface, such as `WriteTo`, `MergeFrom`,
  `CalculateSize`, `Clone` and `Equals`. No top-level message or enum of one assembly has the name of one of the other,
  so the schema's two files share one empty package.

| | Connection | Game |
| --- | ---: | ---: |
| Messages, nested ones among them | 37, 10 | 2,175, 580 |
| Enums | 9 | 423 |
| Fields | 92 | 6,302 |
| Lists | 8 | 770 |
| Maps | 0 | 201 |
| Fields in oneofs, oneofs | 29, 10 | 349, 109 |
| proto3 `optional` fields | 3 | 401 |
| Classes holding an original file's descriptor | 1 | 101 |

- **Types.** Counting a list's or a map's values and a map's keys: 1,958 `int32`, 1,402 messages, 922 `int64`, 883
  `string`, 754 `bool`, 644 enums, 22 `float`, 10 `uint32`. No `sint`, `fixed`, `double` or `bytes`, and every list of
  scalars packed. `google.protobuf.Any` is the only well-known type, in 3 fields of the game assembly. Every enum is an
  `int`, starts with zero, and has no alias nor negative value.
- **The generated code.** Every message implements `IMessage<T>`, `IBufferMessage`, `IEquatable<T>` and
  `IDeepCloneable<T>`. It declares its parser, its unknown fields, then for each field a constant holding its number
  and, outside a oneof, the field's storage, a list or a map's codec before it. Then come the parser and two descriptor
  properties, one property per field, a getter-only `bool` after the property of each of the 443 fields that say
  whether they are set (404 `optional` fields, 39 scalar members of oneofs), and one property per oneof, 119, typed by
  an enum nested in the message whose values but zero are its fields' numbers. A message's own messages and enums are
  in a static class nested in it, 632 in all; messages also nest the compiler's `<>c` class. In all 2,212 messages,
  the number constants and the field properties pair one to one.
- **The machine code agrees.** Each of the 5,415 fields of one value is written once by `InternalWriteTo`, after a
  `WriteRawTag` holding its number, with a method fitting its C# type; each list of scalars and map key or scalar value
  has its `FieldCodec` in the static constructor, with its number in its tag.
- **The original files.** 102 static classes each hold a `FileDescriptor`, one per `.proto` file the protocol was
  compiled from. A message's static `Descriptor` getter reads its file's class and its index there; enums have no such
  getter. The descriptors' bytes are not in clear: the game assembly's `<PrivateImplementationDetails>` holds an array
  of 242,769 bytes beside a class whose methods are named `a`, `a_` and `b`, which may be the obfuscator's encrypted
  strings; not studied.
- **Original names.** The metadata's string literals hold 1,222 names of the form `<original type>|<nested type>`, of
  510 game messages that nest types, and nested types named `decoy`; no type is tied to them without reading the code
  that uses them, which session 3g did not (D54). The client's classes handling game messages, in its `Core` assembly,
  are obfuscated too: 1,003 game types appear in their methods' signatures.
- **The schema of 3.7.12.12.** 170,287 bytes, written in about 6 s, the same bytes on two runs. protoc compiles its
  `.proto` text to a descriptor equal to it on all 2,212 messages and 6,394 fields.
