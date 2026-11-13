import { describe, expect, it } from "vitest";

import type { MapCells } from "@/bindings.ts";
import type { CharacterView } from "./character-store.ts";
import { cellCentre, floors, marks } from "./map-marks.ts";

// Three cells: 0 walkable, 1 an obstacle, 2 walkable and an exit; one element without a cell in its update, on cell 1.
const cells: MapCells = { flags: [1, 0, 1], exits: [2], elements: [{ id: 465_210, cell: 1 }] };

// Only what is drawn is set: the cells of the character, the actors and the stated elements.
const view = {
  cell: 0,
  actors: [
    { id: 7, kind: { kind: "player", name: "Mirabeau" }, cell: 2 },
    { id: -2, kind: { kind: "monsterGroup", monsters: [] }, cell: 1 },
    { id: 9, kind: { kind: "npc", npcId: 4 }, cell: null },
    { id: 10, kind: { kind: "other" }, cell: 0 },
  ],
  stated: [
    { elementId: 465_210, cell: null, state: 0 },
    { elementId: 1, cell: 2, state: 2 },
  ],
} as unknown as CharacterView;

describe("cellCentre", () => {
  it("lays 14 columns of 40 rows, the odd rows half a cell to the right", () => {
    expect(cellCentre(0)).toEqual({ x: 20, y: 10 });
    expect(cellCentre(13)).toEqual({ x: 540, y: 10 });
    expect(cellCentre(14)).toEqual({ x: 40, y: 20 });
    expect(cellCentre(559)).toEqual({ x: 560, y: 400 });
  });
});

describe("floors", () => {
  it("draws the walkable cells and the exits, and leaves the obstacles out", () => {
    expect(floors(cells)).toEqual([
      { cell: 0, floor: "walkable" },
      { cell: 2, floor: "exit" },
    ]);
  });
});

describe("marks", () => {
  it("places the resources, then the actors, then the character on top", () => {
    expect(marks(cells, view)).toEqual([
      { cell: 1, kind: "resource" },
      { cell: 2, kind: "harvested" },
      { cell: 2, kind: "player" },
      { cell: 1, kind: "monsters" },
      { cell: 0, kind: "other" },
      { cell: 0, kind: "me" },
    ]);
  });

  it("leaves out what has no known cell", () => {
    const lost = { ...view, cell: null, actors: [], stated: [{ elementId: 99, cell: null, state: 0 }] };
    expect(marks(cells, lost as CharacterView)).toEqual([]);
  });
});
