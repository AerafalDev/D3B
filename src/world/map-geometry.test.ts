import { describe, expect, it } from "vitest";

import {
  type AtlasWorld,
  fitView,
  levelFor,
  mapAt,
  mapsPaths,
  type PlacedTile,
  tilesDrawn,
  visibleTiles,
  zoomAbout,
} from "./map-geometry.ts";

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
  hints: [],
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

  it("places a smaller level's tiles in the image's pixels at zoom 100", () => {
    // At 50 %, 1,250 by 550 pixels in 2 columns: a tile covers twice its size, the last one cut.
    const half = visibleTiles(zoom(50, 1250, 550, 2, 1), { x: 0, y: 0, scale: 0.5 }, 5000, 5000);
    expect(half.map((tile) => [tile.file, tile.left, tile.top, tile.width, tile.height])).toEqual([
      ["t/50/1.jpg", 0, 0, 2048, 1100],
      ["t/50/2.jpg", 2048, 0, 452, 1100],
    ]);
  });

  describe("the tiles drawn", () => {
    const tile = (file: string, left: number, top: number, side: number): PlacedTile => ({
      file,
      left,
      top,
      width: side,
      height: side,
    });
    // One tile of the coarse level covers the four of the fine one, and the coarse level's second tile lies past them.
    const coarse = [tile("c/1", 0, 0, 2048), tile("c/2", 2048, 0, 2048)];
    const fine = [tile("f/1", 0, 0, 1024), tile("f/2", 1024, 0, 1024), tile("f/3", 0, 1024, 1024)];
    const coarsest = [tile("z/1", 0, 0, 4096)];
    const files = (tiles: readonly PlacedTile[]) => tiles.map((one) => one.file);

    it("keeps a tile drawn before under a new level's not loaded yet, not one past them", () => {
      expect(files(tilesDrawn([], fine, coarse, new Set(["c/1", "c/2"])))).toEqual(["c/1", "f/1", "f/2", "f/3"]);
    });

    it("keeps it while one tile over it is still loading", () => {
      expect(files(tilesDrawn([], fine, coarse, new Set(["c/1", "f/1", "f/2"])))).toEqual(["c/1", "f/1", "f/2", "f/3"]);
    });

    it("drops it once all the tiles over it are loaded", () => {
      expect(files(tilesDrawn([], fine, coarse, new Set(["c/1", "f/1", "f/2", "f/3"])))).toEqual(["f/1", "f/2", "f/3"]);
    });

    it("keeps no tile that never loaded, nor one only touching an edge", () => {
      const edge = [tile("e/1", 0, 2048, 1024), tile("e/2", 1024, 1024, 1024)];
      expect(files(tilesDrawn([], [tile("f/3", 0, 1024, 1024)], edge, new Set(["e/1"])))).toEqual(["f/3"]);
    });

    it("draws a tile wanted again once, on top", () => {
      expect(files(tilesDrawn([], coarse, [...coarse, ...fine], new Set(["f/1"])))).toEqual(["f/1", "c/1", "c/2"]);
    });

    it("lays the coarsest level under all, the tiles kept above it as sharper", () => {
      expect(files(tilesDrawn(coarsest, fine, [...coarsest, ...coarse], new Set(["z/1", "c/1"])))).toEqual([
        "z/1",
        "c/1",
        "f/1",
        "f/2",
        "f/3",
      ]);
    });

    it("draws the coarsest level once when it is the one wanted", () => {
      expect(files(tilesDrawn(coarsest, coarsest, fine, new Set(["f/1"])))).toEqual(["z/1", "f/1"]);
    });
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
    // Four maps in a row, 400 by 50, in a square box: the width limits, 70 % of it, inside the bounds.
    const row = fitView(
      world,
      [
        [0, 0],
        [3, 0],
      ],
      400,
      400,
    );
    expect(row.scale).toBeCloseTo(0.7);
    expect(row.x).toBeCloseTo(200 - 1200 * 0.7);
    expect(row.y).toBeCloseTo(200 - 525 * 0.7);
  });

  it("zooms about a point that stays put, within the bounds", () => {
    const view = zoomAbout(world, { x: 0, y: 0, scale: 0.5 }, 2, 100, 100);
    expect(view).toEqual({ scale: 1, x: -100, y: -100 });
    expect(zoomAbout(world, view, 10, 0, 0).scale).toBe(1);
    expect(zoomAbout(world, view, 0.01, 0, 0).scale).toBe(0.2);
  });
});
