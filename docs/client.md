# The official client

What the installed official client holds that D3B uses, measured for the steps that use it. It holds counts, formats
and layouts, never content. Measured on 2026-10-27 with `scripts/inspect.cs` (D26) and the extraction tool's checks, on
client 3.7.12.12, built on 2026-10-19, installed in `%LOCALAPPDATA%\Ankama\Dofus-dofus3`; how it connects was measured
on running clients, then on its bytes through D3B's relay ([Connection](#connection)), its protocol's code with
`scripts/schema.cs` ([Protocol code](#protocol-code)), its connection phase against D3B's readable protocol with
`scripts/mapping.cs` ([mapped](#the-connection-phase-mapped)), then decoded through it on 2026-10-28
([decoded](#the-connection-phase-decoded)), and its game the same day ([the game, mapped](client-game.md#the-game-mapped)), then
acted on through it ([the game, acted on](client-game.md#the-game-acted-on)), walked by a bot ([the game, walked](client-game.md#the-game-walked)),
travelled from map to map ([the game, between maps](client-game.md#the-game-between-maps)), its elements used
([the game, used](client-game.md#the-game-used)), its bank used ([the game, banked](client-game.md#the-game-banked)), its zaaps and haven bag
used ([the game, teleported](client-game.md#the-game-teleported)), and its floors, fights and disconnections met by an activity
([the game, interrupted](client-game.md#the-game-interrupted)). Its icons and its elements' graphics were measured on 2026-10-29 for
session 6b ([Icons](#icons), [Graphics](#graphics)), and a tour foretold by them ([the game, foretold](client-game.md#the-game-foretold)).
Its fights were measured on 2026-10-30 for session 9a, on the wire ([the game, fought](client-game.md#the-game-fought)) and in its tables
([Fights' tables](#fights-tables)), which session 9b measured whole, their flags read in its code, the same day; its
casts against D3B's rules for session 9c ([the game, cast](client-game.md#the-game-cast)), and what each did against D3B's effects for
session 9d ([the game, foreseen](client-game.md#the-game-foreseen)), and the states its spells ask for session 9f
([the game, masked](client-game.md#the-game-masked)). Its NPCs' dialogs and transports were measured on 2026-11-01 for session 9g3
([the game, through an NPC](client-game.md#the-game-through-an-npc)). What its items heal, the walks it does not follow and a hunt's
journeys were measured on 2026-11-02 for session 9g5 ([the game, kept on its way](client-game.md#the-game-kept-on-its-way)), its
summons for session 9f2 ([the game, summoned](client-game.md#the-game-summoned)), and its bombs and fights' placement cells for session
9f3 ([the game, bombed](client-game.md#the-game-bombed), [the game, placed](client-game.md#the-game-placed)). Client 3.7.13.17, built on
2026-11-03, was measured against it the same day, its pack, schema and mapping beside 3.7.12.12's
([Client 3.7.13.17](#client-371317)).
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
([the game, used](client-game.md#the-game-used)).

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
15; 97.6% of maps have one. The extractor checks that every vertex's zone is a walkable cell's zone of its map. On the 864 maps of the
pack of 3.7.13.17 that hold several, zones are almost always parts of the map one cannot walk between (10 pairs of zones joined by a path
among those tried), and 3,673 (element, zone) pairs on 607 maps have walkable neighbours in another zone only (D424).

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
interactive transition ([the game, used](client-game.md#the-game-used)).

**Coverage.** 10,981 of the 17,722 maps have a vertex, all `ClientMapData`. Every map with a vertex has a position in
`MapsInformationDataRoot`, and every zaap stands on one; in 3.7.13.17, all but one
([Client 3.7.13.17](#client-371317)).

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
  ([the game, teleported](client-game.md#the-game-teleported)); the other bits are not. BillBot reads it as Dofus 2's map
  capabilities, bit 2 letting one teleport to the map and bit 3 from it, as the client's code names them
  ([Fights' tables](#fights-tables)).
- **Sub-areas.** 34 of the 567 allow accounts without a subscription (`basicAccountAllowed`). `associatedZaapMapId`
  names a zaap's map on 329 and is 0 on the other 238. `level` from 0 to 200. `harvestables` lists items on 190
  sub-areas ([Harvestables](#harvestables)). Every map a sub-area lists names that sub-area back.
- **Zaaps.** 62, 47 `activated`; every one stands on a map of the world graph, in the sub-area of its map, but
  3.7.13.17's zaap 81 ([Client 3.7.13.17](#client-371317)). The graph has
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
The pack keeps the id, name, type, level, weight and icon, and from format 8 the life one use gives back when that is
all it does, 21 bytes an item ([the game, kept on its way](client-game.md#the-game-kept-on-its-way)); the other effects, recipes,
drops, criteria and `m_flags` are left out (D29, D139, D257).

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

### Hints

Measured on 3.7.13.17 on 2026-11-09 for the session that put the hints on the studio's map (D414), with `scripts/inspect.cs` and
measuring tools never committed that print counts and write previews outside the repository.

- **The table.** `World.HintsDataRoot` holds 835 hints (834 on 3.7.12.12) in 6 of its 12 categories (`HintCategoriesDataRoot`):
  Lieux de classes 19, Hôtels de vente 45, Ateliers 154, Divers 297, Donjons 130 and Transports 190; the six others (conquest
  territories, possessions, landmarks, anomalies, quests, repeatable quests) hold none. Every hint's map is in
  `MapsInformationDataRoot`, with the hint's own `x`, `y` and sub-area: 835 of 835. Its `worldMapId` is its map's `worldMap` for
  799; for 36 it is another, which is the world the client draws it on. `realMapId` is set on 144 hints, in the table for 8: not read.
  679 hints are on world 1, 156 on 31 others (24 on world 2, 15 on 34, 14 on 37); on world 1 they stand on 587 coordinates, at
  most 4 on one, and 83 maps hold two or more. The 835 names are in both languages.
- **Kinds, by `gfx`** (the picture, which tells apart what a category mixes): the 18 workshops are one graphic a craft, 301 to 319
  and 430, 478; 45 auction houses are 7 graphics; the 19 class places one a class, 10 to 180 and 470; the 130 dungeons are drawn
  with 422 but one (474, Gargandyas); the 190 transports are 45 zaaps (410), 46 boats (413), 32 quays (433), 24 carriers (412), 13
  drills (438) and about 30 others; Divers holds the 10 banks (401), 35 taverns (408), 36 emote spots (423, "Attitude …"), 14 spells
  (441), 11 libraries, 9 town halls and castles, 9 militias, and 102 passages (900 to 903: "Passage vers…", "Sortie…", "Vers…").
- **Zaaps.** The 45 hints of graphic 410 stand on the maps of 45 of the 63 waypoints of `WaypointsDataRoot`, every one activated.
  The other 18 waypoints have no hint, 3 of them activated (32, 33 and 81): the client's world map draws hints only.
- **Dungeons.** 130 hints on 127 maps in 121 sub-areas, 123 outdoor, each with its `level`, 10 to 200, the only hints that have
  one but 6 others. `SubAreasDataRoot.dungeonId` is set on 185 sub-areas and `entranceMapIds` on none: the hint is the only
  place the data says where a dungeon's door stands.
- **Pictures.** Not in `Picto`: in the addressables bundle `aa/StandaloneWindows64/uidarkstone_assets_all.bundle` (2.2 MiB),
  whose catalog lists them as `…/hints/<gfx>.png`, 143 addresses. The bundle holds 159 textures named by number, with a sprite of the
  same name; 91 of the 92 graphics the hints name have one, only 475 ("Portail vers la Source", one hint) has not. 116 are 64 by 64
  in DXT5, one level, streamed but for 470 (the Forgelance class place), held inline; the 18 other class places' are 62 by 62 in RGBA32,
  one level, inline in the object, which ends with 16 bytes of an empty stream data (its offset is 8 bytes). Full colour with a shadow, drawn to be seen at 24 to 32
  pixels: a zaap's stone arch with a swirl, a craft's tool or product, a skull for a dungeon, coins for a bank, a "K" tag on an
  auction house's. The 68 others are the house signs, prices and other numbers no hint uses. 91 pictures are 1,490,944 bytes as RGBA;
  the pack of format 12 is 111,872,773 bytes against format 11's 111,625,358, 247,415 more for the hints, their pictures and their texts.

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
of their own, which the server gives a fighter ([the game, fought](client-game.md#the-game-fought)).

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
- The extractor keeps the items' at 1x and the professions' at 2x, decoded, as raw RGBA (D135 to D137); since D265, every
  item's, halved to 32 pixels, each pixel the mean of four weighed by their alpha, the 3 not of 64 pixels in BC7 left out.

## The world's maps

Measured on 3.7.13.17 on 2026-11-05 for session 7d (D308), with `scripts/inspect.cs` and a measuring tool never committed
that prints counts and sizes, and writes a few previews outside the repository.

- **The bundle**: `Content/Picto/Worldmaps/worldmap_assets_.bundle`, 536.9 MiB on disk (520.4 MiB on 3.7.12.12), 925.9 MiB
  decompressed (891.0), in 7,408 blocks of 128 KiB. It holds 1,250 `Texture2D` and 1,250 `Sprite`, 969,875,776 bytes of
  texels in its `.resS`, and nothing else. A texture is BC7 (format 25), one level, its texels bottom row first as
  everywhere (Icons); its sprite and it share a name, a number.
- **Which tile is which** is in no object: the bundle's container is a map of 32 hexadecimal characters, one per
  texture-sprite pair, and the sprites are named `1`, `2`, `3`. The group's own Addressables catalog,
  `Content/Picto/Worldmaps/catalog_1.0.bin`, which `Content/catalogs.json` lists for the other groups the same way, holds
  the addresses, `worldmaps/<world>/<zoom>/<tile>.jpg`, the tiles having been JPEG files, each with its key. It is a
  binary catalog of Addressables 2: its strings are a length as `uint32` then ASCII; before a key's own string stand
  its address's suffix, `<zoom>/<tile>.jpg`, whose offset is the word 12 bytes before the key's length, and the word 8
  bytes before it, the offset of a record whose first word is the offset of the address's prefix, `worldmaps/<world>`.
  Read so, the 1,250 keys give 1,250 distinct addresses, none contradicting another, and the 1,250 sprites' names are
  their tiles' numbers.
- **The worlds**: `World.WorldMapsDataRoot`, `Content/Data/data_assets_worldmapsdataroot.asset.bundle`, 40 records with
  ids 1 to 41 but 23, of `id`, `nameId`, `origineX`, `origineY`, `mapWidth`, `mapHeight`, `minScale`, `maxScale`,
  `startScale`, `totalWidth`, `totalHeight`, `zoom` (floats) and `visibleOnMap`, which is 0 for 7 of them, ids 4, 5, 6, 7,
  11, 19 and 29. World 1, the main one, is 10,000 by 9,664 pixels at zoom 1, 69.5 by 49.7 pixels a map, its origin
  6,480 and 4,944, and has 5 levels, 1, 0.8, 0.6, 0.4 and 0.2; the other 39 have 4, 1, 0.75, 0.5 and 0.25, from 1,800 by
  1,400 to 7,226 by 5,023 pixels, and from 91 by 65 to 910 by 660 a map. `BreachWorldMapSectorsDataRoot` and
  `BreachWorldMapCoordinatesDataRoot` are the Breach's, not read.
- **Tiles.** An image at a zoom is cut into tiles of 1,024 pixels, row by row from the top left, numbered from 1, the last
  column and row cropped to what is left: 627 tiles are 1,024 by 1,024 and 623 smaller, such as world 24's 792 by 1,024
  and 1,024 by 124 at zoom 1. The 161 groups of a world and a zoom hold exactly `ceil(totalWidth · z / 1024) ·
  ceil(totalHeight · z / 1024)` tiles, numbered with no gap, 1,250 in all. The tiles stitched at zoom 0.4 draw world 1
  whole, upright, with no seam. A level's size is what its tiles add up to, a multiple of 4: it is the table's size at the
  zoom to within 4 pixels, 150 of the 161 levels differing from it (world 1 at 20 % is 2,000 by 1,932, the table's
  1,932.8; world 19 at 100 % is 2,496 by 1,900, its table's 2,500 by 1,904). BC7 decodes the opaque texels a quarter of
  the time as alpha 254, 225,405,668 of them, never less: the tiles are opaque.

| Zoom | Tiles | BC7 bytes | JPEG 90 | JPEG 85 | JPEG 75 | WebP 80 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0.2 | 4 | 3,864,000 | 618,798 | 570,644 | 493,243 | 588,130 |
| 0.25 | 55 | 25,287,488 | 4,885,717 | 4,276,490 | 2,854,749 | 3,561,060 |
| 0.4 | 16 | 15,472,000 | 1,988,344 | 1,836,556 | 1,583,458 | 1,879,016 |
| 0.5 | 164 | 101,007,440 | 16,374,777 | 14,301,640 | 9,524,793 | 12,218,554 |
| 0.6 | 36 | 34,800,000 | 3,857,313 | 3,566,325 | 3,071,615 | 3,673,812 |
| 0.75 | 318 | 227,178,624 | 30,893,805 | 26,873,569 | 18,295,767 | 22,727,062 |
| 0.8 | 64 | 61,856,000 | 6,042,373 | 5,585,909 | 4,812,031 | 5,755,202 |
| 1 | 593 | 500,410,224 | 59,865,819 | 52,518,277 | 39,107,726 | 47,119,562 |
| All | 1,250 | 969,875,776 | 124,526,946 | 109,529,410 | 79,743,382 | 97,522,398 |

- **The pack** (D369): the 1,250 tiles encoded at JPEG 75, the owner's choice, 79,743,382 bytes, each a pack entry of its own,
  `worldmaps/<world>/<zoom as a percentage>/<tile>`, and the 40 worlds and 161 zooms one more, `worldmaps/catalog`; with them
  the pack of 3.7.13.17 is 105,494,687 bytes, format 10, 18,890 entries, extracted in 41 s, the same SHA-256 on two
  extractions. Read back through `D3B.Data`, every tile decodes to the size its zoom gives it.
- **Cost.** The tiles decoded from BC7 and encoded again by SkiaSharp 3.119.4, no resampling: the sizes above, all 1,250
  tiles, in 112 s for the four encoders and the decoding. PNG, on 40 tiles drawn at random, weighs 525,584 bytes a tile,
  about 6 times JPEG 85. A tile of world 1 at zoom 1 weighs 166,824 bytes at JPEG 75 and 192,008 at 85. Raw RGBA is 4
  bytes a pixel, 3.9 GB. On a crop of that tile at full size, JPEG 75 and the PNG cannot be told apart by eye, the source
  being a JPEG itself.
- **Where a map stands.** At zoom 1, the map at coordinates `x`, `y` of world `w` begins at pixel `origineX + x · mapWidth`,
  `origineY + y · mapHeight` of its image, and at zoom `z` at `z` times that: the top left corner of the map's rectangle,
  `mapWidth` by `mapHeight`. Drawn over world 1 at 60 % for the maps of coordinates −14, −36 to 24, −6, around Astrub, every
  rectangle covers land and they stop at the coasts, the lone island's own among them. A map's world is its `worldMap` in
  `MapsInformationDataRoot`: −1 for 3,676 maps, 1 for 8,551, and 38 others, 3 to 640 each (world 11 holds none). Of the
  11,794 maps with a world, 11,446 lie wholly inside its image by that rule, all of world 1's 8,551 and the others' but
  348, measured in session 7d2 (D374): 249 stand at coordinates 0, 0 in 18 worlds whose origin lies outside the image, so
  that the map at 0, 0 begins off it (worlds 4 to 10, 12, 17, 18, 20, 22, 31 to 33 and 36 to 38; worlds 40 and 41 are the same,
  with no map there); 70 are of world 19, "World Maps", invisible on the client's map, whose image is a parchment of question
  marks and whose origin is 0, 0 as empty world 11's is (of its 640 maps, 554 stand at 0, 0 and 86 at the main world's
  coordinates, 16 of which fit, by chance); 29 are outliers, 12 in world 10, 9 stacked on 6, 13 in world 35 and 8 others. The maps
  at 0, 0 are the insides given no coordinates: 2,932 maps with a world stand there, 2,889 of them off the world graph against
  454 of the other 8,862. Taking away those at 0, 0 and world 19's, 8,776 maps are placed and 8,747 fit. By the measuring tool
  (never committed), a map's `worldMap` is the world its sub-area (`customWorldMapId`), else its area (`worldmapId` with
  `hasWorldMap`), else its super area names, for all 11,794 maps with a world and so for the 348: no other world holds them. The
  client's drawing code was not read, and bit 23 of `m_flags`, which its enum's order reads as `HasPriorityOnWorldmap`, is set on
  5,218 of those 8,862 maps and on 3 of the 348: what the client draws for the 348 is not measured. A
  sub-area names a world of its own in `customWorldMapId` (−1 for 425 of 569; 144 name one of 37 worlds, 3 to 41), an
  area in `worldmapId` (0 for 55 of 70; 15 name 12 worlds).
- **Insides and layers** (D373). Of 3,676 maps on no world, 2,608 have a sub-area whose other maps are all on one world (2,499
  stand on a square of it), 3,596 share coordinates with a map of some world, and 80 share them with none: `WorldMapIndex` puts an
  inside on the world of its sub-area, else the least world with a map at its coordinates.
- **Coordinates shared.** In world 1, 8,551 maps stand on 6,222 coordinates: 273 hold two maps or more, 27 five or more,
  and one holds 1,820. World −1, on no world's map, holds 3,676 maps on 990 coordinates, 480 of them with two or more
  and 159 with five or more, up to 482; world 19 holds 640 on 70, 554 on one; world 12, 345 on 155.

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

## Client 3.7.13.17

Measured on 2026-11-03 for session 3i (D360 to D362), against 3.7.12.12: `scripts/inspect.cs`, the extraction, the
schema and mapping tools, and measuring tools never committed that compare the two builds' packs and mappings table by
table and print counts only.

- **The installation.** Built on 2026-11-03 at 14:09 UTC, Unity 6000.3.16f1, serialized format 22, Addressables 2.9.1,
  as before. 20,671 bundles, 21.1 GiB, against 20,623: `Data` still 208 bundles, 99.1 MiB decompressed; `Map/Data` 596.
  The texts: 340,817 numbered, identifiers 1 to 1,202,050, and 7,501 keyed, 348,318 distinct; 24.1 MiB in French.
- **The pack**, format 9: 27,087,404 bytes, 17,639 entries, extracted in 32 s. Professions and skills, breeds, banks,
  weapons, characteristics and effects are the same, but 3 effects' descriptions; the 38 heads and 16 glyphs, drawn on a
  sheet, are upright and clean. The rest, table by table:

| Table | 3.7.12.12 | 3.7.13.17 | Changed |
| --- | ---: | ---: | --- |
| Maps | 17,722 | 17,623 | 129 gone, 30 new; of the 17,593 both hold, 101 with other cells, 40 with other elements |
| Interactive elements, graphics | 46,462, 3,499 | 46,490, 3,512 | 44 elements new, 4 gone, 12 moved, 8 drawn otherwise; 22 graphics new, 9 gone |
| World graph | 11,327 vertices, 30,909 edges | 11,331, 30,917 | 8 transitions more, the same 619 criteria |
| Positions, sub-areas, areas, zaaps | 15,409, 567, 69, 62 | 15,470, 569, 70, 63 | 62 positions new, 1 gone |
| Items | 21,554 | 21,622 | 68 new, none changed |
| Interactives | 446 | 457 | 11 new |
| Spell levels | 34,251 | 34,249 | 2 gone; about ten with another range, casts per turn, flags or effects |
| Spell states | 6,377 | 6,378 | 1 new; 24 named otherwise |
| Monsters, grades | 5,129, 26,821 | 5,135, 26,851 | 6 monsters and a race new |
| Icons | 11,748 items' | 11,764 | 619 items' and 7 heads re-encoded by a few texels; jobs, spells, monsters, glyphs the same |

- **Zaap 81** stands on a map the world graph does not hold, in sub-area 1137 of area 104, both new: 48 maps, level 10,
  not on the world map. 3.7.12.12 held the maps, with neither position nor vertex; all 48 now have a position, none a
  vertex. The extractor keeps the zaap and counts it (D360); no path teleports to it.
- **The graphics** (D362): 46,438 of the 46,446 elements on maps both builds hold keep their graphic. The 261 graphics
  the owner's bots learned are all still drawn, none among the 8 elements drawn otherwise: an update does not renumber
  them.
- **The protocol** (D361): the schema is 3.7.12.12's byte for byte, 170,287 bytes, SHA-256 `e383c947…`. With 3.7.12.12's
  pins and the ticket's request pinned as its captures anchored it, the mapping pairs every message, field, enum and
  value as 3.7.12.12's does.
- **The owner's captures** (D364): a connection phase and a game connection of 10 min 46 s through the app, 7,908
  frames, every one read by the schema. The mapping made with them anchors the ticket's request as pinned; of the 7,683
  game frames, 5,256 understood whole, 1,232 more read, none misread. The client confirmed all 63 of D3B's walks out of
  fights, asked for the information of each of the 49 maps it entered, and ended all 25 of D3B's uses (D60). In the AI's
  fight it ended 8 of the character's turns by itself, `automatic` set, over the fight's last 80 s, after 13 turns the
  AI played (D252), its inactive state (D366).

## Connection

How the client connects, which D34 and then D35 build on. Measured on 2026-10-27 from the installation, the client's
logs, the configuration its `ConfigUrl` names, the names in its IL2CPP metadata, and the connections of four clients of
this build in game for about 50 minutes, each logged in to its own account. Session 3b then changed `ConfigUrl` and
recorded the connection phase of two clients through D3B's relay ([on the wire](#the-connection-phase-on-the-wire)), and
session 3f took their game connection through it too ([the game connection](client-game.md#the-game-connection)).

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
([the game connection](client-game.md#the-game-connection)).

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

The game connection, from "The game connection" to "The game, listed and pictured", is in [client-game.md](client-game.md): one section for each session that measured it.

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
- **The schema of 3.7.12.12.** 170,287 bytes, written in about 6 s, the same bytes on two runs; 3.7.13.17's are the same
  bytes (D361). protoc compiles its
  `.proto` text to a descriptor equal to it on all 2,212 messages and 6,394 fields.
