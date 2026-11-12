import { describe, expect, it } from "vitest";

import { characterState } from "./character-state.ts";
import type { CharacterView } from "./character-store.ts";

// Only the name and the fight decide the state: the rest of the view is left out.
const view = { name: "Lyra", fighting: false } as CharacterView;

describe("characterState", () => {
  it("says a lost protocol first, whatever the view kept", () => {
    expect(characterState("lost", view)).toBe("lost");
  });

  it("says off for a build without pins or without a name", () => {
    expect(characterState("unmapped", view)).toBe("off");
    expect(characterState("unknownBuild", undefined)).toBe("off");
  });

  it("waits until the character entered the game", () => {
    expect(characterState("on", undefined)).toBe("waiting");
    expect(characterState("on", { ...view, name: null })).toBe("waiting");
  });

  it("tells a fight from exploring", () => {
    expect(characterState("on", view)).toBe("exploring");
    expect(characterState("on", { ...view, fighting: true })).toBe("fighting");
  });
});
