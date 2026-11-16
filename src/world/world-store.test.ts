import { beforeEach, describe, expect, it, vi } from "vitest";

// The back end and the editor area, unused here.
vi.mock("@/bindings.ts", () => ({ commands: {} }));
vi.mock("@/shell/parts/editor-store.ts", () => ({ useEditors: { getState: () => ({ active: null, api: null }) } }));

const { useWorld } = await import("./world-store.ts");

describe("the World store's zone of mine", () => {
  beforeEach(() => useWorld.setState({ read: null, picked: 3, world: 1, zone: null, tool: "eraser" }));

  it("opens a zone on its world in place of a sub-area chosen, keeping the tool unless given one", () => {
    useWorld.getState().openZone("ble", 2);
    expect(useWorld.getState()).toMatchObject({ zone: "ble", world: 2, picked: null, tool: "eraser" });
    useWorld.getState().openZone("bois", 2, "brush");
    expect(useWorld.getState()).toMatchObject({ zone: "bois", tool: "brush" });
  });

  it("closes the zone for a sub-area chosen, and for another world but not the same one", () => {
    useWorld.getState().openZone("ble", 1);
    useWorld.getState().show(1);
    expect(useWorld.getState().zone).toBe("ble");
    useWorld.getState().show(2);
    expect(useWorld.getState()).toMatchObject({ zone: null, world: 2 });
    useWorld.getState().openZone("ble", 1);
    useWorld.getState().pick(5);
    expect(useWorld.getState()).toMatchObject({ zone: null, picked: 5 });
  });
});
