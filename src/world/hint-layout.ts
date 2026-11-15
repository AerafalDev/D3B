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

/** The kinds hidden until shown: the other places and the passages, which crowd the map (the owner, 2026-11-15). */
export const HIDDEN_KINDS: ReadonlySet<HintKind> = new Set(["place", "passage"]);

/** A hint to draw, its picture centred on `x, y` of the image at zoom 100. */
export interface PlacedHint {
  hint: AtlasHint_Serialize;
  x: number;
  y: number;
}

/** Two pictures overlap when their centres are nearer than this share of their side: a little overlap is allowed. */
const ROOM = 0.9;

/**
 * The hints to draw at `scale`, each at its map's centre, those sharing a map side by side; one whose picture would
 * overlap a picture kept before it is left out. `hints` come by kind (the atlas's order), so the more useful stays.
 * The choice is made over the whole world for a zoom, so moving the map never changes it.
 */
export function placeHints(world: AtlasWorld, hints: readonly AtlasHint_Serialize[], scale: number): PlacedHint[] {
  // A picture's side on the image at zoom 100.
  const side = HINT_SIZE / scale;
  const room = side * ROOM;
  const onMap = new Map<string, AtlasHint_Serialize[]>();
  for (const hint of hints) {
    const key = `${hint.at[0]},${hint.at[1]}`;
    onMap.set(key, [...(onMap.get(key) ?? []), hint]);
  }
  // The kept pictures by cell of a grid as wide as the room, so that a picture is checked against its neighbours only.
  const grid = new Map<string, PlacedHint[]>();
  const kept: PlacedHint[] = [];
  for (const hint of hints) {
    const row = onMap.get(`${hint.at[0]},${hint.at[1]}`) ?? [hint];
    const x =
      world.origin[0] + (hint.at[0] + 0.5) * world.mapSize[0] + (row.indexOf(hint) - (row.length - 1) / 2) * side;
    const y = world.origin[1] + (hint.at[1] + 0.5) * world.mapSize[1];
    const column = Math.floor(x / room);
    const line = Math.floor(y / room);
    let crowded = false;
    for (let dx = -1; dx <= 1 && !crowded; dx += 1) {
      for (let dy = -1; dy <= 1 && !crowded; dy += 1) {
        crowded = (grid.get(`${column + dx},${line + dy}`) ?? []).some(
          (other) => Math.abs(other.x - x) < room && Math.abs(other.y - y) < room,
        );
      }
    }
    if (!crowded) {
      const placed = { hint, x, y };
      kept.push(placed);
      const cell = `${column},${line}`;
      grid.set(cell, [...(grid.get(cell) ?? []), placed]);
    }
  }
  return kept;
}
