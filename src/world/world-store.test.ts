import { beforeEach, describe, expect, it, vi } from "vitest";

// The back end and the editor area, unused here.
vi.mock("@/bindings.ts", () => ({ commands: {} }));
vi.mock("@/shell/parts/editor-store.ts", () => ({ useEditors: { getState: () => ({ active: null, api: null }) } }));

const { useWorld } = await import("./world-store.ts");

describe("the World store's hints", () => {
  it("shows every kind at first, the other places and the passages too (the owner, 2026-11-17)", () => {
    expect(useWorld.getInitialState().hidden.size).toBe(0);
  });
});

describe("the World store's choice on the map", () => {
  it("shows a sub-area chosen for a Zone, and takes a map without choosing a sub-area", () => {
    const zone = vi.fn();
    useWorld.setState({ picked: null });
    useWorld.getState().startChoosing("Lyra", zone);
    useWorld.getState().endChoosing(3);
    expect([zone.mock.calls, useWorld.getState().picked]).toEqual([[[3]], 3]);
    const map = vi.fn();
    useWorld.setState({ picked: null });
    useWorld.getState().startChoosing("Lyra", map, "map");
    useWorld.getState().endChoosing(88_080_385);
    expect([map.mock.calls, useWorld.getState().picked]).toEqual([[[88_080_385]], null]);
  });
});

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
