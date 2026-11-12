import { describe, expect, it } from "vitest";

import { cellCentre } from "./map-grid.tsx";

describe("cellCentre", () => {
  it("lays 14 columns of 40 rows, the odd rows half a cell to the right", () => {
    expect(cellCentre(0)).toEqual({ x: 20, y: 10 });
    expect(cellCentre(13)).toEqual({ x: 540, y: 10 });
    expect(cellCentre(14)).toEqual({ x: 40, y: 20 });
    expect(cellCentre(559)).toEqual({ x: 560, y: 400 });
  });
});
