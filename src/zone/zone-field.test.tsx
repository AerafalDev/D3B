import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { Atlas_Serialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";

// The back end, faked: the atlas the field reads.
const backEnd = vi.hoisted(() => ({ atlas: vi.fn() }));
vi.mock("@/bindings.ts", () => ({ commands: { worldAtlas: backEnd.atlas } }));
// The editor area, faked: the editors opened, and the tab active before the map.
const editors = vi.hoisted(() => ({ opened: [] as string[] }));
vi.mock("@/shell/parts/editor-store.ts", () => ({
  openEditor: (editor: { id: string }) => editors.opened.push(editor.id),
  useEditors: { getState: () => ({ active: "character:1", api: null }) },
}));

const { ZoneField } = await import("./zone-field.tsx");
const { useWorld } = await import("@/world/world-store.ts");

const names = (fr: string, en = fr) => ({ fr, en });
const subArea = (id: number, fr: string, level: number, drawn = true) => ({
  id,
  name: names(fr),
  level,
  maps: drawn ? [{ world: 1, at: [[0, 0]] as [number, number][] }] : [],
  resources: [],
  monsters: 0,
});
const ATLAS: Atlas_Serialize = {
  worlds: [],
  jobs: [],
  resources: [],
  areas: [
    {
      id: 0,
      name: names("Astrub"),
      subAreas: [subArea(97, "Forêt d'Astrub", 20), subArea(98, "Champs d'Astrub", 15)],
    },
    { id: 1, name: names("Bonta"), subAreas: [subArea(10, "Cité de Bonta", 30), subArea(11, "Égouts", 40, false)] },
  ],
};

async function shown(view: React.ReactElement) {
  render(view);
  // The atlas is read when the field mounts.
  await act(async () => {});
}

describe("the Zone field", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    backEnd.atlas.mockReset().mockResolvedValue(ATLAS);
    editors.opened = [];
    useWorld.setState({ read: null, picked: null, world: 1, choosing: null });
  });
  afterEach(cleanup);

  it("says the zone chosen with its area and level, or that one is to choose, or a sub-area the data lacks", async () => {
    const { rerender } = render(<ZoneField value={{ sub_area: 97 }} onChange={() => {}} who="Aerinth" />);
    await act(async () => {});
    const field = () => screen.getByRole("combobox", { name: "Zone" });
    expect(field().textContent).toBe("Forêt d'AstrubAstrub · niv. 20");
    rerender(<ZoneField value={null} onChange={() => {}} who="Aerinth" />);
    expect(field().textContent).toBe("À choisir");
    rerender(<ZoneField value={{ sub_area: 445 }} onChange={() => {}} who="Aerinth" />);
    expect(field().textContent).toBe("Sous-zone 445 absente des données");
    rerender(<ZoneField value={{ zone: "my-wheat" }} onChange={() => {}} who="Aerinth" />);
    expect(field().textContent).toBe("my-wheat");
  });

  it("unfolds my zones, then the sub-areas by area and level, searched without accents, and a choice closes it", async () => {
    const onChange = vi.fn();
    await shown(<ZoneField value={{ sub_area: 97 }} onChange={onChange} who="Aerinth" />);
    fireEvent.click(screen.getByRole("combobox", { name: "Zone" }));
    const list = screen.getByRole("listbox");
    const headings = [...list.querySelectorAll("[cmdk-group-heading]")].map((one) => one.textContent);
    expect(headings).toEqual(["Mes zones", "Astrub", "Bonta"]);
    expect(list.textContent).toContain("Tes zones peintes viendront ici.");
    const items = () =>
      within(screen.getByRole("listbox"))
        .getAllByRole("option")
        .map((one) => one.textContent);
    expect(items()).toEqual([
      "Champs d'Astrubniv. 15",
      "Forêt d'Astrubniv. 20",
      "Cité de Bontaniv. 30",
      "Égoutsniv. 40",
      "Choisir sur la carte",
    ]);
    fireEvent.change(screen.getByRole("combobox", { name: "Chercher une zone" }), { target: { value: "egout" } });
    expect(items()).toEqual(["Égoutsniv. 40", "Choisir sur la carte"]);
    fireEvent.change(screen.getByRole("combobox", { name: "Chercher une zone" }), { target: { value: "zzz" } });
    expect(screen.getByRole("listbox").textContent).toContain("Aucune sous-zone ne correspond.");
    fireEvent.change(screen.getByRole("combobox", { name: "Chercher une zone" }), { target: { value: "champs" } });
    fireEvent.click(screen.getByRole("option", { name: /Champs d'Astrub/ }));
    expect(onChange.mock.calls).toEqual([[{ sub_area: 98 }]]);
    expect(screen.queryByRole("listbox")).toBeNull();
  });

  it("chooses on the map through its target, for whom it says, and takes the sub-area clicked there", async () => {
    const onChange = vi.fn();
    await shown(<ZoneField value={null} onChange={onChange} who="Aerinth" />);
    fireEvent.click(screen.getByRole("button", { name: "Choisir sur la carte" }));
    expect(editors.opened).toEqual(["world"]);
    expect(useWorld.getState().choosing?.who).toBe("Aerinth");
    act(() => useWorld.getState().endChoosing(10));
    expect(onChange.mock.calls).toEqual([[{ sub_area: 10 }]]);
  });

  it("hands a sub-area chosen on the map to the latest onChange given, and chooses from the keyboard", async () => {
    const [first, latest] = [vi.fn(), vi.fn()];
    const { rerender } = render(<ZoneField value={null} onChange={first} who="Aerinth" />);
    await act(async () => {});
    fireEvent.click(screen.getByRole("button", { name: "Choisir sur la carte" }));
    rerender(<ZoneField value={null} onChange={latest} who="Aerinth" />);
    act(() => useWorld.getState().endChoosing(10));
    expect([first.mock.calls, latest.mock.calls]).toEqual([[], [[{ sub_area: 10 }]]]);
    // The arrows move from the first sub-area listed, Enter chooses.
    fireEvent.click(screen.getByRole("combobox", { name: "Zone" }));
    const search = screen.getByRole("combobox", { name: "Chercher une zone" });
    fireEvent.keyDown(search, { key: "ArrowDown" });
    fireEvent.keyDown(search, { key: "Enter" });
    expect(latest.mock.calls[1]).toEqual([{ sub_area: 97 }]);
    // After a search, Enter takes what it found.
    fireEvent.click(screen.getByRole("combobox", { name: "Zone" }));
    fireEvent.change(screen.getByRole("combobox", { name: "Chercher une zone" }), { target: { value: "egout" } });
    fireEvent.keyDown(screen.getByRole("combobox", { name: "Chercher une zone" }), { key: "Enter" });
    expect(latest.mock.calls[2]).toEqual([{ sub_area: 11 }]);
  });

  it("says a sub-area by its number, and why the list is empty, without game data", async () => {
    backEnd.atlas.mockReset().mockRejectedValue({ kind: "noData", reason: "no folder" });
    await shown(<ZoneField value={{ sub_area: 97 }} onChange={() => {}} who="Aerinth" />);
    expect(screen.getByRole("combobox", { name: "Zone" }).textContent).toBe("Sous-zone 97");
    fireEvent.click(screen.getByRole("combobox", { name: "Zone" }));
    expect(screen.getByRole("listbox").textContent).toContain("Pas de données du jeu : no folder");
  });

  it("offers the map from its list too", async () => {
    await shown(<ZoneField value={null} onChange={() => {}} who="Aerinth" />);
    fireEvent.click(screen.getByRole("combobox", { name: "Zone" }));
    fireEvent.click(screen.getByRole("option", { name: "Choisir sur la carte" }));
    expect(editors.opened).toEqual(["world"]);
    expect(screen.queryByRole("listbox")).toBeNull();
  });
});
