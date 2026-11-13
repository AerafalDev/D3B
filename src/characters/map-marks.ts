import type { MapCells } from "@/bindings.ts";
import type { CharacterView } from "./character-store.ts";

/** A cell's width and height: the client's diamonds are twice as wide as high. */
export const CELL_WIDTH = 40;
export const CELL_HEIGHT = 20;
const COLUMNS = 14;
const ROWS = 40;

/** The map's size in the units of `cellCentre`: 14 columns of 40 staggered rows. */
export const MAP_WIDTH = COLUMNS * CELL_WIDTH + CELL_WIDTH / 2;
export const MAP_HEIGHT = ROWS * (CELL_HEIGHT / 2) + CELL_HEIGHT / 2;

/** Where cell `cell`'s centre lies: 14 columns of 40 staggered rows, the odd rows half a cell to the right. */
export function cellCentre(cell: number): { x: number; y: number } {
  const row = Math.floor(cell / COLUMNS);
  const column = cell % COLUMNS;
  return {
    x: column * CELL_WIDTH + (row % 2) * (CELL_WIDTH / 2) + CELL_WIDTH / 2,
    y: row * (CELL_HEIGHT / 2) + CELL_HEIGHT / 2,
  };
}

/** The ground the owner's style "B" draws: only where one walks, and the exits. */
export type Floor = "walkable" | "exit";

/** What stands on a cell, drawn as a dot (`docs/game.md`, "In the app"). */
export type MarkKind = "me" | "player" | "monsters" | "npc" | "resource" | "harvested" | "other";

export interface Mark {
  cell: number;
  kind: MarkKind;
}

/** The cells drawn: walkable ones and exits; the others, obstacles and walls, stay undrawn. */
export function floors(cells: MapCells): { cell: number; floor: Floor }[] {
  const exits = new Set(cells.exits);
  const drawn: { cell: number; floor: Floor }[] = [];
  cells.flags.forEach((flags, cell) => {
    if (exits.has(cell)) {
      drawn.push({ cell, floor: "exit" });
    } else if ((flags & 1) !== 0) {
      drawn.push({ cell, floor: "walkable" });
    }
  });
  return drawn;
}

const ACTOR: Record<string, MarkKind> = { player: "player", monsterGroup: "monsters", npc: "npc" };

/**
 * What stands on the map, in drawing order: resources first (an element's cell from its update, else from the map's
 * data), then the other actors, then the character on top. Anything without a known cell is left out.
 */
export function marks(cells: MapCells, view: CharacterView): Mark[] {
  const elementCells = new Map(cells.elements.map((element) => [element.id, element.cell]));
  const placed: Mark[] = [];
  for (const element of view.stated) {
    const cell = element.cell ?? elementCells.get(element.elementId) ?? -1;
    if (cell >= 0) {
      placed.push({ cell, kind: element.state === 0 ? "resource" : "harvested" });
    }
  }
  for (const actor of view.actors) {
    if (actor.cell != null) {
      placed.push({ cell: actor.cell, kind: ACTOR[actor.kind.kind] ?? "other" });
    }
  }
  if (view.cell != null) {
    placed.push({ cell: view.cell, kind: "me" });
  }
  return placed;
}
