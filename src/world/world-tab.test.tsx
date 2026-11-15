import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { Atlas_Serialize, AtlasWorld_Serialize as AtlasWorld } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";

// The back end, faked: the atlas the tab reads.
const backEnd = vi.hoisted(() => ({ atlas: vi.fn() }));
vi.mock("@/bindings.ts", () => ({ commands: { worldAtlas: backEnd.atlas } }));
// The map, faked: happy-dom draws no SVG images, and the tab's text is what is checked here.
vi.mock("./world-map.tsx", () => ({
  WorldMap: ({
    chosen,
    hints,
    onPick,
  }: {
    chosen: unknown[] | null;
    hints: { id: number }[];
    onPick: (x: number, y: number) => boolean;
  }) => (
    <>
      <p data-hints={hints.map((one) => one.id).join(",")}>carte {chosen?.length ?? "vide"}</p>
      <button type="button" onClick={(event) => (event.currentTarget.dataset["picked"] = String(onPick(1, 0)))}>
        clic 1, 0
      </button>
      <button type="button" onClick={(event) => (event.currentTarget.dataset["picked"] = String(onPick(5, 5)))}>
        clic 5, 5
      </button>
    </>
  ),
}));
// Details, faked: its own tests say what it shows.
vi.mock("./world-details.tsx", () => ({
  WorldDetails: ({ subArea }: { subArea: number | null }) => <aside>détails {subArea ?? "aucun"}</aside>,
}));

const { WorldTab } = await import("./world-tab.tsx");
const { WorldActions } = await import("./world-actions.tsx");
const { useWorld } = await import("./world-store.ts");

const names = (fr: string, en = fr) => ({ fr, en });
const BASE = { origin: [0, 0], mapSize: [10, 10], size: [100, 100], minScale: 0.2, maxScale: 1, startScale: 0.5 };
const world = (id: number, fr: string) =>
  ({ ...BASE, id, name: names(fr), zooms: [], hints: [] }) as unknown as AtlasWorld;

const ATLAS: Atlas_Serialize = {
  worlds: [world(1, "Monde des Douze"), world(2, "Monde d'Incarnam")],
  jobs: [],
  resources: [],
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
          resources: [],
          monsters: 0,
        },
        { id: 4, name: names("Antre d'Astrub"), level: 30, maps: [], resources: [], monsters: 0 },
      ],
    },
    {
      id: 1,
      name: names("Incarnam"),
      subAreas: [
        { id: 5, name: names("Pâturages"), level: 1, maps: [{ world: 2, at: [[2, 2]] }], resources: [], monsters: 0 },
      ],
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

  it("reads the atlas once, opens on its first world when the one asked for is not in it, nothing chosen", async () => {
    useWorld.setState({ world: 9 });
    await shown(<WorldTab />);
    await useWorld.getState().load();
    expect(backEnd.atlas).toHaveBeenCalledTimes(1);
    expect(useWorld.getState().world).toBe(1);
    expect(screen.getByText("carte vide")).toBeTruthy();
    expect(screen.queryByRole("heading")).toBeNull();
  });

  it("goes to the world that holds a sub-area chosen, and says its name, area, level and maps", async () => {
    await shown(<WorldTab />);
    act(() => useWorld.getState().pick(5));
    expect(useWorld.getState().world).toBe(2);
    expect(screen.getByRole("heading", { name: "Pâturages" })).toBeTruthy();
    expect(document.body.textContent).toContain("Incarnam · niv. 1 · 1 carte");
    expect(screen.getByText("carte 1")).toBeTruthy();
  });

  it("keeps the world for a sub-area on none, saying it is not on the map, or on which world it is", async () => {
    useWorld.setState({ read: { state: "read", atlas: ATLAS }, world: 2 });
    useWorld.getState().pick(4);
    expect(useWorld.getState()).toMatchObject({ picked: 4, world: 2 });
    useWorld.setState({ world: 1 });
    await shown(<WorldTab />);
    expect(document.body.textContent).toContain("Astrub · niv. 30 · Pas sur la carte");
    expect(screen.getByText("carte vide")).toBeTruthy();
    act(() => useWorld.setState({ picked: 5 }));
    expect(document.body.textContent).toContain("Incarnam · niv. 1 · Sur Monde d'Incarnam");
  });

  it("chooses the sub-area of a map clicked and shows it in Details, but not for a map of none or the one chosen", async () => {
    await shown(<WorldTab />);
    expect(document.body.textContent).toContain("détails aucun");
    const click = (name: string) => {
      const button = screen.getByRole("button", { name });
      fireEvent.click(button);
      return button.dataset["picked"];
    };
    // Map 1, 0 of the Twelve is Forêt d'Astrub's.
    expect(click("clic 1, 0")).toBe("true");
    expect(useWorld.getState()).toMatchObject({ picked: 3, world: 1 });
    expect(document.body.textContent).toContain("détails 3");
    expect(click("clic 1, 0")).toBe("false");
    expect(click("clic 5, 5")).toBe("false");
    expect(useWorld.getState().picked).toBe(3);
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

  it("gives the map the hints of the kinds shown", async () => {
    const hint = (id: number, kind: string) => ({
      id,
      kind,
      name: names("Indice"),
      at: [0, 0],
      level: 0,
      picture: null,
    });
    const hinted = {
      ...ATLAS,
      worlds: [{ ...world(1, "Monde des Douze"), hints: [hint(1, "zaap"), hint(2, "passage")] }],
    };
    useWorld.setState({ read: { state: "read", atlas: hinted as Atlas_Serialize }, hidden: new Set(["passage"]) });
    await shown(<WorldTab />);
    const map = () => screen.getByText(/carte/).getAttribute("data-hints");
    expect(map()).toBe("1");
    act(() => useWorld.getState().hide(new Set(["zaap"])));
    expect(map()).toBe("2");
  });
});
