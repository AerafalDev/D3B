import { describe, expect, it } from "vitest";

import { type AtlasWorld, fitView, levelFor, mapAt, mapsPaths, visibleTiles, zoomAbout } from "./map-geometry.ts";

const zoom = (percent: number, width: number, height: number, columns: number, rows: number) => ({
  percent,
  width,
  height,
  columns,
  rows,
  tiles: Array.from({ length: columns * rows }, (_, index) => `t/${percent}/${index + 1}.jpg`),
});

// A world of 2,500 by 1,100 pixels at zoom 100, maps of 100 by 50 from 1,000, 500.
const world: AtlasWorld = {
  id: 1,
  name: null,
  origin: [1000, 500],
  mapSize: [100, 50],
  size: [2500, 1100],
  minScale: 0.2,
  maxScale: 1,
  startScale: 0.5,
  zooms: [zoom(100, 2500, 1100, 3, 2), zoom(40, 1000, 440, 1, 1), zoom(20, 500, 220, 1, 1)],
};

describe("the World's map geometry", () => {
  it("draws the smallest level as sharp as the screen, and the largest past them all", () => {
    expect(levelFor(world.zooms, 0.2)?.percent).toBe(20);
    expect(levelFor(world.zooms, 0.3)?.percent).toBe(40);
    expect(levelFor(world.zooms, 0.41)?.percent).toBe(100);
    expect(levelFor(world.zooms, 2)?.percent).toBe(100);
    expect(levelFor([], 1)).toBeUndefined();
  });

  it("draws only the tiles the box shows, the last column and row cut short", () => {
    const level = world.zooms[0];
    if (level === undefined) throw new Error("no level");
    const all = visibleTiles(level, { x: 0, y: 0, scale: 1 }, 5000, 5000);
    expect(all.map((tile) => [tile.file, tile.width, tile.height])).toEqual([
      ["t/100/1.jpg", 1024, 1024],
      ["t/100/2.jpg", 1024, 1024],
      ["t/100/3.jpg", 452, 1024],
      ["t/100/4.jpg", 1024, 76],
      ["t/100/5.jpg", 1024, 76],
      ["t/100/6.jpg", 452, 76],
    ]);
    // Moved so only the bottom right tile is in a 100 px box.
    const corner = visibleTiles(level, { x: -2100, y: -1050, scale: 1 }, 100, 100);
    expect(corner.map((tile) => [tile.file, tile.left, tile.top])).toEqual([["t/100/6.jpg", 2048, 1024]]);
  });

  it("finds the map under a point, west and north of the origin too", () => {
    expect(mapAt(world, 1000, 500)).toEqual([0, 0]);
    expect(mapAt(world, 1199, 549)).toEqual([1, 0]);
    expect(mapAt(world, 999, 499)).toEqual([-1, -1]);
  });

  it("outlines a set of maps by the sides no neighbour shares", () => {
    const { cells, outline } = mapsPaths(world, [
      [0, 0],
      [1, 0],
    ]);
    expect(cells).toBe("M1000,500H1100V550H1000ZM1100,500H1200V550H1100Z");
    // Top, bottom and left of the first; top, bottom and right of the second: the shared side is not drawn.
    expect(outline).toBe("M1000,500H1100M1000,550H1100M1000,500V550M1100,500H1200M1100,550H1200M1200,500V550");
  });

  it("fits maps in the box within the zoom bounds, and the whole image without maps", () => {
    const view = fitView(world, [[0, 0]], 700, 350);
    // 70 % of the box over one map: 4.9, held to the bound 1, the map's centre in the box's.
    expect(view).toEqual({ scale: 1, x: 350 - 1050, y: 175 - 525 });
    expect(fitView(world, [], 1250, 550)).toEqual({ scale: 0.5, x: 0, y: 0 });
  });

  it("zooms about a point that stays put, within the bounds", () => {
    const view = zoomAbout(world, { x: 0, y: 0, scale: 0.5 }, 2, 100, 100);
    expect(view).toEqual({ scale: 1, x: -100, y: -100 });
    expect(zoomAbout(world, view, 10, 0, 0).scale).toBe(1);
    expect(zoomAbout(world, view, 0.01, 0, 0).scale).toBe(0.2);
  });
});
