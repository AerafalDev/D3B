import { describe, expect, it } from "vitest";

import type { Atlas_Serialize } from "@/bindings.ts";
import { worldTree } from "./world-tree.ts";

const names = (fr: string, en = fr) => ({ fr, en });
const subArea = (id: number, fr: string, en: string, drawn = true) => ({
  id,
  name: names(fr, en),
  level: id,
  maps: drawn ? [{ world: 1, at: [[0, 0]] as [number, number][] }] : [],
});

const atlas: Atlas_Serialize = {
  worlds: [],
  areas: [
    {
      id: 1,
      name: names("Bonta"),
      subAreas: [subArea(10, "Cité de Bonta", "Bonta City"), subArea(11, "Cité blanche", "White City")],
    },
    {
      id: 0,
      name: names("Astrub"),
      subAreas: [
        subArea(3, "Forêt d'Astrub", "Astrub Forest"),
        subArea(2, "Champs d'Astrub", "Astrub Fields"),
        subArea(4, "Antre d'Astrub", "Astrub Lair", false),
      ],
    },
  ],
};

describe("the World view's tree", () => {
  it("orders the areas by name in the player's language, their sub-areas by level then name, saying which are drawn", () => {
    expect(worldTree(atlas, "fr", "")).toEqual([
      {
        id: 0,
        name: "Astrub",
        subAreas: [
          { id: 2, name: "Champs d'Astrub", level: 2, drawn: true },
          { id: 3, name: "Forêt d'Astrub", level: 3, drawn: true },
          { id: 4, name: "Antre d'Astrub", level: 4, drawn: false },
        ],
      },
      {
        id: 1,
        name: "Bonta",
        subAreas: [
          { id: 10, name: "Cité de Bonta", level: 10, drawn: true },
          { id: 11, name: "Cité blanche", level: 11, drawn: true },
        ],
      },
    ]);
    // At one level, by name.
    const same = {
      ...atlas,
      areas: [{ ...atlas.areas[1], subAreas: atlas.areas[1]?.subAreas.map((one) => ({ ...one, level: 1 })) ?? [] }],
    };
    expect(worldTree(same as Atlas_Serialize, "en", "")[0]?.subAreas.map((one) => one.name)).toEqual([
      "Astrub Fields",
      "Astrub Forest",
      "Astrub Lair",
    ]);
  });

  it("keeps the sub-areas a search finds, accents and case aside, and drops the areas left empty", () => {
    expect(worldTree(atlas, "fr", " FORET ").map((area) => [area.name, area.subAreas.map((one) => one.id)])).toEqual([
      ["Astrub", [3]],
    ]);
  });

  it("keeps a whole area whose name a search finds", () => {
    // "Cité blanche" does not hold "bonta": its area's name keeps it.
    expect(worldTree(atlas, "fr", "bonta")[0]?.subAreas.map((one) => one.id)).toEqual([10, 11]);
    expect(worldTree(atlas, "fr", "astrub")[0]?.subAreas).toHaveLength(3);
    expect(worldTree(atlas, "fr", "nothing")).toEqual([]);
  });
});
