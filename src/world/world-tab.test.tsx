import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { Atlas_Serialize, AtlasWorld_Serialize as AtlasWorld } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";

// The back end, faked: the atlas the tab reads.
const backEnd = vi.hoisted(() => ({ atlas: vi.fn(), zones: vi.fn(), paint: vi.fn(), undo: vi.fn() }));
vi.mock("@/bindings.ts", () => ({
  commands: { worldAtlas: backEnd.atlas, zones: backEnd.zones, paintZone: backEnd.paint, undoZone: backEnd.undo },
}));
// The map, faked: happy-dom draws no SVG images, and the tab's text is what is checked here.
vi.mock("./world-map.tsx", () => ({
  WorldMap: ({
    chosen,
    hints,
    nameAt,
    onPick,
    outlineAt,
    mine,
    paint,
    panning,
  }: {
    chosen: unknown[] | null;
    hints: { id: number }[];
    nameAt: (x: number, y: number) => string | null;
    onPick: (x: number, y: number) => boolean;
    outlineAt?: (x: number, y: number) => unknown[] | null;
    mine: unknown[] | null;
    paint?: { erase: boolean; paints: (x: number, y: number) => boolean; onStroke: (maps: [number, number][]) => void };
    panning: boolean;
  }) => (
    <>
      <p
        data-hints={hints.map((one) => one.id).join(",")}
        data-outline={String(outlineAt?.(1, 0)?.length)}
        data-name={nameAt(1, 0) ?? ""}
        data-mine={String(mine?.length)}
        data-panning={String(panning)}
        data-paint={
          paint === undefined ? "" : `${paint.erase ? "gomme" : "pinceau"} ${paint.paints(1, 0)} ${paint.paints(5, 5)}`
        }
      >
        carte {chosen?.length ?? "vide"}
      </p>
      {paint !== undefined && (
        <button
          type="button"
          onClick={() =>
            paint.onStroke([
              [0, 0],
              [1, 0],
            ])
          }
        >
          trait
        </button>
      )}
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

// The editor area, faked: the tab active before the map was opened, brought back.
// The editor area, faked: the tab active before the map was opened, brought back, and the panels' closing.
const editors = vi.hoisted(() => ({
  active: null as string | null,
  shown: [] as string[],
  closed: null as ((panel: { id: string }) => void) | null,
}));
vi.mock("@/shell/parts/editor-store.ts", () => ({
  useEditors: {
    getState: () => ({
      active: editors.active,
      api: {
        getPanel: (id: string) => ({ api: { setActive: () => editors.shown.push(id) } }),
        onDidRemovePanel: (listener: (panel: { id: string }) => void) => {
          editors.closed = listener;
          return { dispose: () => (editors.closed = null) };
        },
      },
    }),
  },
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
              priority: [],
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
        {
          id: 5,
          name: names("Pâturages"),
          level: 1,
          maps: [{ world: 2, at: [[2, 2]], priority: [] }],
          resources: [],
          monsters: 0,
        },
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
    backEnd.zones.mockReset().mockResolvedValue({ zones: [], kept: [] });
    backEnd.paint.mockReset();
    useWorld.setState({ read: null, picked: null, world: 1, choosing: null, zone: null, tool: "brush" });
  });
  afterEach(cleanup);

  describe("with a zone of mine open", () => {
    const zone = (maps: [number, number][]) => ({ id: "ble", name: "Mon blé", world: 1, maps });
    const map = () => screen.getByText(/^carte/);

    beforeEach(() => {
      backEnd.zones.mockResolvedValue({ zones: [zone([[0, 0]])], kept: [] });
      backEnd.paint.mockImplementation(async (_id: string, stroke: { maps: [number, number][]; erase: boolean }) =>
        zone(stroke.erase ? [] : [[0, 0], ...stroke.maps.filter(([x]) => x !== 0)]),
      );
    });

    it("shows it in amber with its name and maps, and the brush painting the world's maps only", async () => {
      useWorld.getState().openZone("ble", 1, "brush");
      await shown(<WorldTab />);
      expect(screen.getByRole("heading", { name: "Mon blé" })).toBeTruthy();
      expect(document.body.textContent).toContain("Zone à moi · 1 carte");
      expect(map().dataset["mine"]).toBe("1");
      expect(map().dataset["paint"]).toBe("pinceau true false");
      // The hovered map outlined alone.
      expect(map().dataset["outline"]).toBe("1");
      await act(async () => fireEvent.click(screen.getByRole("button", { name: "trait" })));
      expect(backEnd.paint.mock.calls).toEqual([
        [
          "ble",
          {
            maps: [
              [0, 0],
              [1, 0],
            ],
            erase: false,
          },
        ],
      ]);
      expect(map().dataset["mine"]).toBe("2");
    });

    it("takes its tools by their buttons and keys, but not while a field or another tab takes the keys", async () => {
      editors.active = "world";
      useWorld.getState().openZone("ble", 1, "brush");
      await shown(<WorldTab />);
      const pressed = () =>
        screen
          .getAllByRole("button", { pressed: true })
          .map((one) => one.getAttribute("aria-label"))
          .join();
      expect(pressed()).toBe("Pinceau (B)");
      fireEvent.keyDown(window, { key: "e" });
      expect(pressed()).toBe("Gomme (E)");
      expect(map().dataset["paint"]).toBe("gomme true false");
      fireEvent.click(screen.getByRole("button", { name: "Sélection (V)" }));
      expect(map().dataset["paint"]).toBe("");
      const field = document.body.appendChild(document.createElement("input"));
      fireEvent.keyDown(field, { key: "s" });
      field.remove();
      expect(pressed()).toBe("Sélection (V)");
      // Space held anywhere moves the map with the main button, until it is released.
      fireEvent.keyDown(screen.getByRole("button", { name: "Pinceau (B)" }), { key: " " });
      expect(map().dataset["panning"]).toBe("true");
      fireEvent.keyUp(window, { key: " " });
      expect(map().dataset["panning"]).toBe("false");
      editors.active = "blueprint:hunt";
      fireEvent.keyDown(window, { key: "b" });
      fireEvent.keyDown(window, { key: " " });
      expect(pressed()).toBe("Sélection (V)");
      expect(map().dataset["panning"]).toBe("false");
      editors.active = null;
    });

    it("adds a whole sub-area by one click, and takes it away when the zone holds it all", async () => {
      useWorld.getState().openZone("ble", 1, "subArea");
      await shown(<WorldTab />);
      // The forest's whole outline on hover.
      expect(map().dataset["outline"]).toBe("2");
      await act(async () => fireEvent.click(screen.getByRole("button", { name: "clic 1, 0" })));
      // Nothing chosen: the map fits again for the next choice.
      expect(screen.getByRole("button", { name: "clic 1, 0" }).dataset["picked"]).toBe("false");
      expect(backEnd.paint.mock.calls.at(-1)?.[1]).toEqual({
        maps: [
          [0, 0],
          [1, 0],
        ],
        erase: false,
      });
      await act(async () => fireEvent.click(screen.getByRole("button", { name: "clic 1, 0" })));
      expect(backEnd.paint.mock.calls.at(-1)?.[1]).toMatchObject({ erase: true });
      expect(useWorld.getState()).toMatchObject({ zone: "ble", picked: null });
      fireEvent.click(screen.getByRole("button", { name: "clic 5, 5" }));
      expect(screen.getByRole("button", { name: "clic 5, 5" }).dataset["picked"]).toBe("false");
      expect(backEnd.paint).toHaveBeenCalledTimes(2);
    });

    it("shows the zone's Details while it is open on the world shown, and the sub-area's otherwise", async () => {
      useWorld.getState().openZone("ble", 1, "brush");
      await shown(<WorldTab />);
      expect(document.body.textContent).toContain("Zone à moi · Monde des Douze");
      expect(document.body.textContent).not.toContain("détails");
      act(() => useWorld.setState({ zone: null }));
      expect(document.body.textContent).toContain("détails aucun");
      // A zone of another world than the one shown is not open here.
      act(() => useWorld.setState({ zone: "ble", world: 2 }));
      expect(document.body.textContent).toContain("détails aucun");
    });

    it("chooses a sub-area with Select as without a zone, which closes it, and another world closes it too", async () => {
      useWorld.getState().openZone("ble", 1, "select");
      await shown(<WorldTab />);
      fireEvent.click(screen.getByRole("button", { name: "clic 1, 0" }));
      expect(useWorld.getState()).toMatchObject({ zone: null, picked: 3 });
      expect(screen.queryByRole("toolbar")).toBeNull();
      act(() => useWorld.getState().openZone("ble", 1));
      expect(screen.getByRole("toolbar", { name: "Outils de la zone" })).toBeTruthy();
      act(() => useWorld.getState().show(2));
      expect(useWorld.getState().zone).toBeNull();
    });
  });

  it("undoes my zones' last change on Ctrl+Z anywhere in the tab", async () => {
    backEnd.undo.mockReset().mockResolvedValue(null);
    const { container } = render(<WorldTab />);
    await act(async () => {});
    const tab = container.firstElementChild as HTMLElement;
    tab.focus();
    await act(async () => {
      fireEvent.keyDown(tab, { key: "z", code: "KeyW", ctrlKey: true });
      fireEvent.keyUp(tab, { key: "z", code: "KeyW", ctrlKey: true });
    });
    expect(backEnd.undo).toHaveBeenCalledTimes(1);
  });

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

  it("names and chooses on a map two sub-areas share the one with the client's priority there, whichever comes last", async () => {
    const [astrub, incarnam] = ATLAS.areas;
    const [forest, ...others] = astrub?.subAreas ?? [];
    if (astrub === undefined || incarnam === undefined || forest === undefined) {
      throw new Error("the atlas fixture changed");
    }
    // A house listed after the forest, on its map 1, 0, without the priority the forest has there.
    const house = { ...forest, id: 6, name: names("Maison"), maps: [{ world: 1, at: [[1, 0]], priority: [] }] };
    const prioritised = { ...forest, maps: [{ world: 1, at: forest.maps[0]?.at ?? [], priority: [[1, 0]] }] };
    backEnd.atlas.mockResolvedValue({
      ...ATLAS,
      areas: [{ ...astrub, subAreas: [prioritised, ...others, house] }, incarnam],
    } as Atlas_Serialize);
    await shown(<WorldTab />);
    expect(document.querySelector("[data-name]")?.getAttribute("data-name")).toBe("Forêt d'Astrub");
    fireEvent.click(screen.getByRole("button", { name: "clic 1, 0" }));
    expect(useWorld.getState().picked).toBe(3);
  });

  it("while choosing a Zone, says for whom, outlines a sub-area hovered, and a click chooses it and goes back", async () => {
    const choose = vi.fn();
    editors.active = "character:1";
    editors.shown = [];
    // The forest is already shown on the map: choosing it still takes it.
    useWorld.setState({ picked: 3 });
    useWorld.getState().startChoosing("Aerinth", choose);
    await shown(<WorldTab />);
    expect(screen.getByRole("status").textContent).toContain("Aerinth : clique une sous-zone pour en faire sa Zone.");
    // Map 1, 0 is Forêt d'Astrub's: its two maps are outlined.
    expect(document.querySelector("[data-outline]")?.getAttribute("data-outline")).toBe("2");
    fireEvent.click(screen.getByRole("button", { name: "clic 1, 0" }));
    expect(choose.mock.calls).toEqual([[3]]);
    expect(useWorld.getState()).toMatchObject({ choosing: null, picked: 3 });
    expect(editors.shown).toEqual(["character:1"]);
    expect(screen.queryByRole("status")).toBeNull();
    expect(document.querySelector("[data-outline]")?.getAttribute("data-outline")).toBe("undefined");
  });

  it("keeps choosing after a click on the sea, and Escape or Cancel goes back without choosing", async () => {
    const choose = vi.fn();
    editors.active = "character:1";
    editors.shown = [];
    useWorld.getState().startChoosing("Aerinth", choose);
    await shown(<WorldTab />);
    fireEvent.click(screen.getByRole("button", { name: "clic 5, 5" }));
    expect(useWorld.getState().choosing?.who).toBe("Aerinth");
    // An Escape a menu already took to close itself leaves the choice.
    const taken = new KeyboardEvent("keydown", { key: "Escape", cancelable: true });
    taken.preventDefault();
    window.dispatchEvent(taken);
    expect(useWorld.getState().choosing?.who).toBe("Aerinth");
    fireEvent.keyDown(window, { key: "Escape" });
    expect(useWorld.getState().choosing).toBeNull();
    useWorld.getState().startChoosing("Aerinth", choose);
    await act(async () => {});
    fireEvent.click(screen.getByRole("button", { name: /Annuler/ }));
    expect(useWorld.getState()).toMatchObject({ choosing: null, picked: null });
    expect(choose).not.toHaveBeenCalled();
    expect(editors.shown).toEqual(["character:1", "character:1"]);
  });

  it("cancels a choice when the World tab closes, not another tab, and stops watching once it ends", () => {
    const choose = vi.fn();
    editors.active = "character:1";
    editors.shown = [];
    useWorld.getState().startChoosing("Aerinth", choose);
    editors.closed?.({ id: "character:2" });
    expect(useWorld.getState().choosing?.who).toBe("Aerinth");
    editors.closed?.({ id: "world" });
    expect(useWorld.getState().choosing).toBeNull();
    expect(editors.shown).toEqual(["character:1"]);
    expect(editors.closed).toBeNull();
    expect(choose).not.toHaveBeenCalled();
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
