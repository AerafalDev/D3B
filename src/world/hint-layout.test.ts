import { describe, expect, it } from "vitest";

import type { AtlasHint_Serialize, HintKind } from "@/bindings.ts";
import { HINT_SIZE, placeHints } from "./hint-layout.ts";
import type { AtlasWorld } from "./map-geometry.ts";

// A map is 100 by 50 pixels at zoom 100, map 0, 0 beginning at 1000, 500.
const world: AtlasWorld = {
  id: 1,
  name: null,
  origin: [1000, 500],
  mapSize: [100, 50],
  size: [4000, 2000],
  minScale: 0.1,
  maxScale: 1,
  startScale: 0.5,
  zooms: [],
  hints: [],
};

function hint(id: number, kind: HintKind, x: number, y: number): AtlasHint_Serialize {
  return { id, kind, name: { fr: `Indice ${id}`, en: `Hint ${id}` }, at: [x, y], level: 0, picture: null };
}

const placed = (hints: AtlasHint_Serialize[], scale: number) =>
  placeHints(world, hints, scale).map(({ hint: { id }, x, y }) => [id, x, y]);

describe("the hints' places on the map", () => {
  it("puts a hint at its map's centre, those of one map side by side a picture apart", () => {
    expect(placed([hint(1, "zaap", 2, -1)], 1)).toEqual([[1, 1250, 475]]);
    // At 100 %, a picture is 28 pixels of the image: two fit a map's width.
    expect(placed([hint(1, "zaap", 0, 0), hint(2, "bank", 0, 0)], 1)).toEqual([
      [1, 1050 - HINT_SIZE / 2, 525],
      [2, 1050 + HINT_SIZE / 2, 525],
    ]);
  });

  it("leaves out a picture that would overlap one before it, more of them as the map zooms out", () => {
    // Two maps side by side: 100 pixels apart, which at 20 % are 20 on the screen, less than a picture.
    const hints = [hint(1, "zaap", 0, 0), hint(2, "bank", 1, 0), hint(3, "dungeon", 4, 0)];
    expect(placed(hints, 1).map(([id]) => id)).toEqual([1, 2, 3]);
    expect(placed(hints, 0.2).map(([id]) => id)).toEqual([1, 3]);
    // The order decides, not the place: the bank first keeps the bank.
    expect(placed([hints[1] as AtlasHint_Serialize, hints[0] as AtlasHint_Serialize], 0.2).map(([id]) => id)).toEqual([
      2,
    ]);
  });

  it("keeps pictures that only touch, and leaves out one nearer than 90 % of a picture", () => {
    // Side by side on one map, a picture apart: each kept. At 25 %, two maps 100 pixels apart are 25 on the screen,
    // less than 90 % of 28; at 27 %, 27 are more.
    expect(placed([hint(1, "zaap", 0, 0), hint(2, "bank", 0, 0), hint(3, "bank", 0, 0)], 1)).toHaveLength(3);
    expect(placed([hint(1, "zaap", 0, 0), hint(2, "bank", 1, 0)], 0.25)).toHaveLength(1);
    expect(placed([hint(1, "zaap", 0, 0), hint(2, "bank", 1, 0)], 0.27)).toHaveLength(2);
  });
});
