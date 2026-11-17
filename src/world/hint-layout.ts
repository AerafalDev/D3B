import type { AtlasHint_Serialize, HintKind } from "@/bindings.ts";
import type { AtlasWorld } from "./map-geometry.ts";

/** A hint's picture's side on the screen, the same at every zoom (the owner's mock-up 11, 2026-11-15). */
export const HINT_SIZE = 28;

/** The kinds in order of use to a bot, the atlas's (`ipc::atlas`), as the switches list them. */
export const HINT_KINDS: readonly HintKind[] = [
  "zaap",
  "bank",
  "dungeon",
  "workshop",
  "auctionHouse",
  "classPlace",
  "transport",
  "place",
  "passage",
];

/** A hint to draw, its picture centred on `x, y` of the image at zoom 100. */
export interface PlacedHint {
  hint: AtlasHint_Serialize;
  x: number;
  y: number;
}

/** Two pictures overlap when their centres are nearer than this share of their side: a little overlap is allowed. */
const ROOM = 0.9;

/**
 * The hints to draw at `scale`, each at its map's centre, those kept on one map side by side about it; one whose
 * picture, or whose map's row once it joins, would overlap a picture of another map kept before is left out. `hints`
 * come by kind (the atlas's order), so the more useful stays. The choice is made over the whole world for a zoom, so
 * moving the map never changes it.
 */
export function placeHints(world: AtlasWorld, hints: readonly AtlasHint_Serialize[], scale: number): PlacedHint[] {
  // A picture's side on the image at zoom 100.
  const side = HINT_SIZE / scale;
  const room = side * ROOM;
  // The pictures kept, by map; a map's row is centred on it, a side apart, so never crowded within.
  const rows = new Map<string, PlacedHint[]>();
  const crowds = (x: number, y: number, map: string) =>
    [...rows].some(
      ([other, row]) =>
        other !== map && row.some((placed) => Math.abs(placed.x - x) < room && Math.abs(placed.y - y) < room),
    );
  for (const hint of hints) {
    const map = `${hint.at[0]},${hint.at[1]}`;
    const centreX = world.origin[0] + (hint.at[0] + 0.5) * world.mapSize[0];
    const y = world.origin[1] + (hint.at[1] + 0.5) * world.mapSize[1];
    const joined = [...(rows.get(map) ?? []).map((placed) => placed.hint), hint];
    const row = joined.map((one, index) => ({ hint: one, x: centreX + (index - (joined.length - 1) / 2) * side, y }));
    if (!row.some((placed) => crowds(placed.x, placed.y, map))) {
      rows.set(map, row);
    }
  }
  return [...rows.values()].flat();
}
