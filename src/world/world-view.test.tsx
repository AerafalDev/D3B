import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { Atlas_Serialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";

// The back end, faked: the atlas the view reads.
const backEnd = vi.hoisted(() => ({ atlas: vi.fn() }));
vi.mock("@/bindings.ts", () => ({ commands: { worldAtlas: backEnd.atlas } }));
// The editor area, faked: the editors opened.
const opened = vi.hoisted(() => ({ editors: [] as { id: string }[] }));
vi.mock("@/shell/parts/editor-store.ts", () => ({
  openEditor: (editor: { id: string }) => opened.editors.push(editor),
}));

const { WorldView } = await import("./world-view.tsx");
const { useWorld } = await import("./world-store.ts");

const names = (fr: string) => ({ fr, en: fr });
const ATLAS = {
  worlds: [],
  areas: [
    {
      id: 0,
      name: names("Astrub"),
      subAreas: [
        { id: 3, name: names("Forêt d'Astrub"), level: 20, maps: [{ world: 1, at: [[0, 0]] }] },
        { id: 4, name: names("Antre d'Astrub"), level: 30, maps: [] },
        { id: 6, name: names("Cité d'Astrub"), level: 10, maps: [{ world: 1, at: [[1, 1]] }] },
      ],
    },
    {
      id: 1,
      name: names("Incarnam"),
      subAreas: [{ id: 5, name: names("Pâturages"), level: 1, maps: [{ world: 2, at: [[2, 2]] }] }],
    },
  ],
} as unknown as Atlas_Serialize;

async function shown() {
  render(<WorldView />);
  // The atlas is read when the view mounts.
  await act(async () => {});
}

describe("the World view", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    backEnd.atlas.mockReset().mockResolvedValue(ATLAS);
    useWorld.setState({ read: null, picked: null, world: 1 });
    opened.editors.length = 0;
  });
  afterEach(cleanup);

  it("lists the areas folded, unfolds one by level, and greys a sub-area not on the map", async () => {
    await shown();
    const astrub = screen.getByRole("button", { name: /Astrub/ });
    expect(astrub.getAttribute("aria-expanded")).toBe("false");
    expect(screen.queryByText("Forêt d'Astrub")).toBeNull();
    fireEvent.click(astrub);
    expect(astrub.getAttribute("aria-expanded")).toBe("true");
    const rows = screen.getAllByRole("button").slice(1, 4);
    expect(rows.map((row) => row.textContent)).toEqual([
      "Cité d'Astrubniv. 10",
      "Forêt d'Astrubniv. 20",
      "Antre d'AstrubPas sur la carteniv. 30",
    ]);
    expect(rows[2]?.getAttribute("title")).toBe("Pas sur la carte");
    expect(rows.map((row) => row.querySelector("[data-icon]")?.getAttribute("data-icon"))).toEqual([
      "mapPin",
      "mapPin",
      "mapPinOff",
    ]);
    fireEvent.click(astrub);
    expect(screen.queryByText("Forêt d'Astrub")).toBeNull();
  });

  it("says it reads the atlas until it has it", () => {
    backEnd.atlas.mockReset().mockReturnValue(new Promise(() => {}));
    render(<WorldView />);
    expect(document.body.textContent).toContain("Lecture des données du jeu…");
  });

  it("unfolds what a search finds, folds an area clicked meanwhile, and says when nothing matches", async () => {
    await shown();
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "patur" } });
    expect(screen.getByRole("button", { name: /Pâturages/ })).toBeTruthy();
    expect(screen.queryByText("Forêt d'Astrub")).toBeNull();
    const incarnam = screen.getByRole("button", { name: /^Incarnam/ });
    fireEvent.click(incarnam);
    expect(incarnam.getAttribute("aria-expanded")).toBe("false");
    // The search over, the tree is as before it: folded.
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "" } });
    expect(screen.getByRole("button", { name: /^Incarnam/ }).getAttribute("aria-expanded")).toBe("false");
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "patur" } });
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "zzz" } });
    expect(screen.getByText("Aucune sous-zone ne correspond.")).toBeTruthy();
  });

  it("opens the World tab on a sub-area chosen, on its world, and marks it chosen", async () => {
    await shown();
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "patur" } });
    const pasture = screen.getByRole("button", { name: /Pâturages/ });
    fireEvent.click(pasture);
    expect(opened.editors.map((editor) => editor.id)).toEqual(["world"]);
    expect(useWorld.getState()).toMatchObject({ picked: 5, world: 2 });
    expect(pasture.getAttribute("aria-current")).toBe("true");
  });

  it("says why there is nothing without game data", async () => {
    backEnd.atlas.mockReset().mockRejectedValue({ kind: "noData", reason: "no folder" });
    await shown();
    expect(document.body.textContent).toContain("Pas de données du jeu : no folder");
  });
});
