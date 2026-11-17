import type { AtlasWorld_Serialize, AtlasZoom } from "@/bindings.ts";

/** A world as the map draws it, its numbers all there. */
export type AtlasWorld = Omit<AtlasWorld_Serialize, "mapSize" | "minScale" | "maxScale" | "startScale"> & {
  mapSize: [number, number];
  minScale: number;
  maxScale: number;
  startScale: number;
};

/** `world` with its numbers all there: the IPC types a float as nullable (a NaN), which the extractor never writes. */
export function drawable(world: AtlasWorld_Serialize): AtlasWorld {
  return {
    ...world,
    mapSize: [world.mapSize[0] ?? 1, world.mapSize[1] ?? 1],
    minScale: world.minScale ?? 1,
    maxScale: world.maxScale ?? 1,
    startScale: world.startScale ?? 1,
  };
}

/** How the world's image is shown: at zoom 100 scaled by `scale`, then moved by `x`, `y` pixels. */
export interface View {
  x: number;
  y: number;
  scale: number;
}

/** A tile to draw: its file, and where it covers the image at zoom 100. */
export interface PlacedTile {
  file: string;
  left: number;
  top: number;
  width: number;
  height: number;
}

/** A tile's side at every level, in its own pixels (`docs/data.md`). */
const TILE = 1024;

/** The level whose pixels are at least the screen's at `scale`, so the map stays sharp; the largest past them all. */
export function levelFor(zooms: readonly AtlasZoom[], scale: number): AtlasZoom | undefined {
  const levels = [...zooms].sort((a, b) => a.percent - b.percent);
  return levels.find((zoom) => zoom.percent / 100 >= scale) ?? levels.at(-1);
}

/** The tiles of `zoom` that a `width` by `height` box shows through `view`. */
export function visibleTiles(zoom: AtlasZoom, view: View, width: number, height: number): PlacedTile[] {
  const k = zoom.percent / 100;
  const tiles: PlacedTile[] = [];
  for (let row = 0; row < zoom.rows; row += 1) {
    for (let column = 0; column < zoom.columns; column += 1) {
      const file = zoom.tiles[row * zoom.columns + column];
      const left = (column * TILE) / k;
      const top = (row * TILE) / k;
      const tileWidth = Math.min(TILE, zoom.width - column * TILE) / k;
      const tileHeight = Math.min(TILE, zoom.height - row * TILE) / k;
      const x = left * view.scale + view.x;
      const y = top * view.scale + view.y;
      const shown = x < width && y < height && x + tileWidth * view.scale > 0 && y + tileHeight * view.scale > 0;
      if (file !== undefined && shown) {
        tiles.push({ file, left, top, width: tileWidth, height: tileHeight });
      }
    }
  }
  return tiles;
}

/** Whether two tiles cover some of the same image, not only an edge. */
function overlap(a: PlacedTile, b: PlacedTile): boolean {
  return a.left < b.left + b.width && b.left < a.left + a.width && a.top < b.top + b.height && b.top < a.top + a.height;
}

/**
 * The tiles to draw, bottom first: those `under` all, the coarsest level's whole, which once loaded leave no part of the
 * map bare where the view never was; then, of the tiles drawn `before`, those `loaded` that a tile `wanted` not loaded yet
 * overlaps, sharper than those under; then those wanted. So a level's tiles still loading never leave the map's
 * background showing (the owner's report of 2026-11-17: black patches for a few milliseconds while zooming).
 */
export function tilesDrawn(
  under: readonly PlacedTile[],
  wanted: readonly PlacedTile[],
  before: readonly PlacedTile[],
  loaded: ReadonlySet<string>,
): PlacedTile[] {
  const bare = wanted.filter((tile) => !loaded.has(tile.file));
  const files = new Set([...under, ...wanted].map((tile) => tile.file));
  const kept = before.filter(
    (tile) => !files.has(tile.file) && loaded.has(tile.file) && bare.some((over) => overlap(tile, over)),
  );
  return [...under, ...kept, ...wanted.filter((tile) => !under.some((one) => one.file === tile.file))];
}

/** Where map `x, y` begins on the image at zoom 100 (`ipc::atlas`). */
function corner(world: AtlasWorld, x: number, y: number): [number, number] {
  return [world.origin[0] + x * world.mapSize[0], world.origin[1] + y * world.mapSize[1]];
}

/** The map under the image's point `px, py` at zoom 100. */
export function mapAt(world: AtlasWorld, px: number, py: number): [number, number] {
  return [Math.floor((px - world.origin[0]) / world.mapSize[0]), Math.floor((py - world.origin[1]) / world.mapSize[1])];
}

/** SVG paths of `maps` on the image at zoom 100: each map's cell, and the outline of them all. */
export function mapsPaths(
  world: AtlasWorld,
  maps: readonly (readonly [number, number])[],
): { cells: string; outline: string } {
  const held = new Set(maps.map(([x, y]) => `${x},${y}`));
  let cells = "";
  let outline = "";
  for (const [x, y] of maps) {
    const [left, top] = corner(world, x, y);
    const [right, bottom] = corner(world, x + 1, y + 1);
    cells += `M${left},${top}H${right}V${bottom}H${left}Z`;
    // A side is on the outline where its neighbour is not held.
    if (!held.has(`${x},${y - 1}`)) outline += `M${left},${top}H${right}`;
    if (!held.has(`${x},${y + 1}`)) outline += `M${left},${bottom}H${right}`;
    if (!held.has(`${x - 1},${y}`)) outline += `M${left},${top}V${bottom}`;
    if (!held.has(`${x + 1},${y}`)) outline += `M${right},${top}V${bottom}`;
  }
  return { cells, outline };
}

/** `scale` kept within the world's zoom bounds. */
export function bounded(world: AtlasWorld, scale: number): number {
  return Math.min(world.maxScale, Math.max(world.minScale, scale));
}

/**
 * The view that shows `maps` whole in a `width` by `height` box, centred, with a margin, within the world's zoom
 * bounds; with no maps, the whole image centred at the world's starting zoom.
 */
export function fitView(
  world: AtlasWorld,
  maps: readonly (readonly [number, number])[],
  width: number,
  height: number,
): View {
  if (maps.length === 0) {
    const scale = bounded(world, world.startScale);
    return { scale, x: (width - world.size[0] * scale) / 2, y: (height - world.size[1] * scale) / 2 };
  }
  const xs = maps.map(([x]) => x);
  const ys = maps.map(([, y]) => y);
  const [left, top] = corner(world, Math.min(...xs), Math.min(...ys));
  const [right, bottom] = corner(world, Math.max(...xs) + 1, Math.max(...ys) + 1);
  const fits = Math.min((width * 0.7) / (right - left), (height * 0.7) / (bottom - top));
  const scale = bounded(world, Number.isFinite(fits) ? fits : world.startScale);
  return { scale, x: width / 2 - ((left + right) / 2) * scale, y: height / 2 - ((top + bottom) / 2) * scale };
}

/** `view` zoomed by `factor` about the box's point `px, py`, which stays put; within the world's bounds. */
export function zoomAbout(world: AtlasWorld, view: View, factor: number, px: number, py: number): View {
  const scale = bounded(world, view.scale * factor);
  return {
    scale,
    x: px - ((px - view.x) / view.scale) * scale,
    y: py - ((py - view.y) / view.scale) * scale,
  };
}
