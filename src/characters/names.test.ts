import { describe, expect, it } from "vitest";

import { initials, placeName } from "./names.ts";

describe("initials", () => {
  it("takes the name's first two letters, the second in lower case", () => {
    expect(initials("lyra")).toBe("Ly");
    expect(initials("Mi-Rabeau")).toBe("Mi");
  });

  it("stands a question mark in for a name without letters", () => {
    expect(initials("--")).toBe("?");
  });
});

describe("placeName", () => {
  const place = { mapId: 1, at: { x: 4, y: -18 }, subArea: { fr: "Forêt", en: "Forest" }, area: null };

  it("names the coordinates, then the sub-area in the interface's language", () => {
    expect(placeName(place, "fr")).toBe("4, -18 · Forêt");
    expect(placeName(place, "en")).toBe("4, -18 · Forest");
  });

  it("names the coordinates alone without a sub-area, and nothing without coordinates", () => {
    expect(placeName({ ...place, subArea: null }, "en")).toBe("4, -18");
    expect(placeName({ ...place, at: null }, "en")).toBeNull();
  });
});
