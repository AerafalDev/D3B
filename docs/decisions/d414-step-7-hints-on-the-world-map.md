# Step 7, hints on the world map (D414)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D414: The world map draws the client's hints: its own table and pictures in the pack, format 12, a layer of its own with a tick for each category

*2026-11-09, step 7 (7r2's frame), a side session of the lead's, from the players' feedback the owner collected ("logos d'ateliers,
donjons, zaaps, etc."). Both choices below are the owner's, made when asked. Builds D308, D343, D369, D371, D385, D397.*

- **Found** ([client.md, Hints](../client.md#hints)). The client's `HintsDataRoot` holds 835 hints in 6 categories (class places,
  auction houses, workshops, misc, dungeons, transports), each with a picture number (`gfx`), a name, a map, the world it is drawn on and,
  for a dungeon, its level; the 45 zaaps are the hints of graphic 410, and the dungeons' doors are known nowhere else (`entranceMapIds`
  is empty). The pack kept only the 10 banks. The pictures are the client's own, in full colour, 64 pixels, in the addressables bundle
  `uidarkstone_assets_all` (91 of the 92 graphics the hints name).
- **Chosen by the owner: the hints and the client's pictures go in the pack, as format 12** (+247 KB on 111.6 MB). Rejected: only the
  table and D3B's own vector icons (the 18 workshops would look alike, and the owner asked for logos like the game's); doing nothing.
  `HintCatalog` (`D3B.Data.Hints`: `Hint`, `HintCategory`, the pictures as an `IconTable` by `gfx`) is its own entry, `hints/catalog`, read
  by `GameTables.Hints`; `HintsExtractor` reads it, the 18 class places' 62-pixel RGBA32 textures set in the middle of 64, one texture
  held inline in DXT5; the names are among the texts the pack keeps. The extraction fails when a hint names a category or a text the
  client lacks; a graphic without a picture (475, one hint) is drawn with a plain mark.
- **Chosen by the owner: everything on, one tick for each of the client's categories, filterable and switched off by one switch**, in
  the View menu of the viewport beside the grid and the shared marks. The categories are named by the game's own texts in the app's
  language. Rejected: six on and three off by default, with kinds of our own (zaaps, banks and passages split out of the client's
  categories); the four the owner named plus "other places".
- **Drawn so that it does not crowd** (`WorldMapHintLayer`, by the lead's reckoning, not yet seen in play): a picture of 18 to 30 pixels
  at the middle of its map's square; each hint has a priority (`HintPriority`: zaaps 100, banks 90, dungeons 80, workshops 70, auction
  houses 60, class places 50, other transports 40, other places 30, passages 10, read from the graphic before the category, which the
  client mixes) that decides from which width of map it shows (4, 8, 14 or 26 pixels) and which of two pictures that would overlap is
  kept, chosen over every hint of the world and not only those in view, so that panning never changes it; hints on one square lie side by
  side when it is wide enough. The name of the hint under the pointer is written over it, and its category, level and coordinates are in
  the HUD.
- **A layer of its own, and a hint is not selectable**: it adds no `WorldSelectionKind`, nothing to the scene, the Outliner or the Details
  (7r3's ground); a press on a hint is a press on its map with the active tool, so the Route tool traces to a zaap or a dungeon's door
  by clicking its picture, and the Select tool pans. Shared coordinates keep D373's choice.
- **Not measured**: the app on a real client (seen only as headless renders of the real pack and tiles at four zooms, which looked
  right); the sizes, thresholds and priorities, which are the lead's reckoning; the 18 waypoints with no hint, 3 of them activated, which
  the client's map does not draw and this one does not either.
- **Left for a later session**: a hint as a selection, shown in the Details with a button to add its map to the route; a hint's map
  chosen exactly when coordinates are shared (the hint knows its `MapId`); the hints in the Outliner; the pack published as the release asset
  `client-3.7.13.17` with the app of this format (`scripts/release.cs --publish`, the owner's go).
