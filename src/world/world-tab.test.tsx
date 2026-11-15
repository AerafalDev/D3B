import { act, cleanup, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { Atlas_Serialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";

// The back end, faked: the atlas the tab reads.
const backEnd = vi.hoisted(() => ({ atlas: vi.fn() }));
vi.mock("@/bindings.ts", () => ({ commands: { worldAtlas: backEnd.atlas } }));
// The map, faked: happy-dom draws no SVG images, and the tab's text is what is checked here.
vi.mock("./world-map.tsx", () => ({
  WorldMap: ({ chosen }: { chosen: unknown[] | null }) => <p>carte {chosen?.length ?? "vide"}</p>,
}));

const { WorldTab } = await import("./world-tab.tsx");
const { WorldActions } = await import("./world-actions.tsx");
const { useWorld } = await import("./world-store.ts");

const names = (fr: string, en = fr) => ({ fr, en });
const world = (id: number, fr: string) => ({
  id,
  name: names(fr),
  origin: [0, 0] as [number, number],
  mapSize: [10, 10] as [number, number],
  size: [100, 100] as [number, number],
  minScale: 0.2,
  maxScale: 1,
  startScale: 0.5,
  zooms: [],
});

const ATLAS: Atlas_Serialize = {
  worlds: [world(1, "Monde des Douze"), world(2, "Monde d'Incarnam")],
  areas: [
    {
      id: 0,
      name: names("Astrub"),
      subAreas: [
        {
          id: 3,
          name: names("Forêt d'Astrub"),
          level: 20,
          maps: [
            {
              world: 1,
              at: [
                [0, 0],
                [1, 0],
              ],
            },
          ],
        },
        { id: 4, name: names("Antre d'Astrub"), level: 30, maps: [] },
      ],
    },
    {
      id: 1,
      name: names("Incarnam"),
      subAreas: [{ id: 5, name: names("Pâturages"), level: 1, maps: [{ world: 2, at: [[2, 2]] }] }],
    },
  ],
};

async function shown(view: React.ReactElement) {
  render(view);
  // The atlas is read when the tab mounts.
  await act(async () => {});
}

describe("the World tab", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    backEnd.atlas.mockReset().mockResolvedValue(ATLAS);
    useWorld.setState({ read: null, picked: null, world: 1 });
  });
  afterEach(cleanup);

  it("reads the atlas once, and shows the first world's map with nothing chosen", async () => {
    await shown(<WorldTab />);
    await useWorld.getState().load();
    expect(backEnd.atlas).toHaveBeenCalledTimes(1);
    expect(screen.getByText("carte vide")).toBeTruthy();
    expect(screen.queryByRole("heading")).toBeNull();
  });

  it("opens on the atlas's first world when the one asked for is not in it", async () => {
    useWorld.setState({ world: 9 });
    await shown(<WorldTab />);
    expect(useWorld.getState().world).toBe(1);
  });

  it("keeps the world shown when a sub-area chosen is on none", async () => {
    useWorld.setState({ read: { state: "read", atlas: ATLAS }, world: 2 });
    useWorld.getState().pick(4);
    expect(useWorld.getState()).toMatchObject({ picked: 4, world: 2 });
  });

  it("goes to the world that holds a sub-area chosen, and says its name, area, level and maps", async () => {
    await shown(<WorldTab />);
    act(() => useWorld.getState().pick(5));
    expect(useWorld.getState().world).toBe(2);
    expect(screen.getByRole("heading", { name: "Pâturages" })).toBeTruthy();
    expect(document.body.textContent).toContain("Incarnam · niv. 1 · 1 carte");
    expect(screen.getByText("carte 1")).toBeTruthy();
  });

  it("says a sub-area is not on the map, or on which world it is", async () => {
    useWorld.setState({ read: { state: "read", atlas: ATLAS }, picked: 4, world: 1 });
    await shown(<WorldTab />);
    expect(document.body.textContent).toContain("Astrub · niv. 30 · Pas sur la carte");
    expect(screen.getByText("carte vide")).toBeTruthy();
    act(() => useWorld.setState({ picked: 5 }));
    expect(document.body.textContent).toContain("Incarnam · niv. 1 · Sur Monde d'Incarnam");
  });

  it("says why there is no map without game data, and offers no worlds", async () => {
    backEnd.atlas.mockReset().mockRejectedValue({ kind: "noData", reason: "no folder" });
    await shown(
      <>
        <WorldTab />
        <WorldActions />
      </>,
    );
    expect(document.body.textContent).toContain("Pas de données du jeu : no folder");
    expect(screen.queryByRole("combobox")).toBeNull();
  });

  it("offers the worlds by name once the tab read them, the one shown chosen", async () => {
    useWorld.setState({ read: { state: "read", atlas: ATLAS }, world: 2 });
    await shown(<WorldActions />);
    expect(screen.getByRole("combobox", { name: "Monde affiché" }).textContent).toContain("Monde d'Incarnam");
  });
});
