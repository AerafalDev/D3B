import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { Atlas_Serialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";

// The asset protocol's URL of a file, as Tauri makes it; happy-dom has no Tauri.
vi.mock("@tauri-apps/api/core", () => ({ convertFileSrc: (path: string) => `asset://${path}` }));
// The back end, faked: a rename and a deletion.
const backEnd = vi.hoisted(() => ({ rename: vi.fn(), remove: vi.fn() }));
vi.mock("@/bindings.ts", () => ({
  commands: {
    renameZone: backEnd.rename,
    deleteZone: backEnd.remove,
    zones: async () => ({ zones: [], kept: [] }),
  },
}));

const { ZoneDetails } = await import("./zone-details.tsx");
const { useZones } = await import("./zones-store.ts");
const { useWorld } = await import("@/world/world-store.ts");
const { usePlayed } = await import("@/characters/played/played-store.ts");
const { useLibrary } = await import("@/library/library-store.ts");

const names = (fr: string) => ({ fr, en: fr });
const subArea = (id: number, fr: string, at: [number, number][], resources: number[]) => ({
  id,
  name: names(fr),
  level: 1,
  maps: [{ world: 1, at, priority: [] }],
  resources,
  monsters: 0,
});
const ATLAS = {
  worlds: [{ id: 1, name: names("Monde des Douze") }],
  jobs: [
    { id: 2, name: names("Bûcheron"), icon: null },
    { id: 28, name: names("Paysan"), icon: null },
  ],
  resources: [
    { id: 303, name: names("Frêne"), level: 1, job: 2, icon: null },
    { id: 289, name: names("Blé"), level: 1, job: 28, icon: null },
    { id: 400, name: names("Orge"), level: 10, job: 28, icon: null },
  ],
  areas: [
    {
      id: 0,
      name: names("Astrub"),
      subAreas: [
        subArea(
          97,
          "Forêt d'Astrub",
          [
            [0, 0],
            [1, 0],
            [2, 0],
          ],
          [303],
        ),
        subArea(98, "Champs d'Astrub", [[0, 1]], [289, 400]),
        subArea(99, "Ailleurs", [[9, 9]], [303]),
      ],
    },
  ],
} as unknown as Atlas_Serialize;
const ZONE = {
  id: "ble",
  name: "Mon blé",
  world: 1,
  maps: [
    [0, 0],
    [0, 1],
    [1, 0],
    [9, 9],
  ] as [number, number][],
};

describe("a zone of mine's Details", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    backEnd.rename.mockReset();
    backEnd.remove.mockReset();
    useZones.setState({ zones: [ZONE], loaded: true });
    useWorld.setState({ zone: "ble" });
    usePlayed.setState({ played: [] });
    useLibrary.setState({ blueprints: [] });
  });
  afterEach(cleanup);

  it("says its world and maps, the sub-areas it touches with their share, and their resources together", () => {
    render(<ZoneDetails atlas={ATLAS} zone={ZONE} />);
    const text = document.body.textContent ?? "";
    expect(text).toContain("Zone à moi · Monde des Douze");
    expect(text).toContain("Cartes4");
    // By their share of the zone, then by name; the resources of each once.
    expect(screen.getAllByRole("listitem").map((one) => one.textContent)).toEqual([
      "Forêt d'Astrub2/3",
      "Ailleurs1/1",
      "Champs d'Astrub1/1",
      "Frêne1",
      "Blé1",
      "Orge10",
    ]);
    expect(screen.getByRole("list", { name: "Paysan" })).toBeTruthy();
    cleanup();
    render(<ZoneDetails atlas={ATLAS} zone={{ ...ZONE, maps: [] }} />);
    expect(document.body.textContent).toContain("Aucune : peins ses cartes.");
    expect(document.body.textContent).toContain("Aucune ressource.");
  });

  it("renames it on Enter, puts back a blank name or one Escape left, and says whose Zone it is", async () => {
    usePlayed.setState({
      played: [{ name: "Kalyx", blueprint: "harvest", settings: { zone: '{"zone":"ble"}' } }],
    } as never);
    useLibrary.setState({
      blueprints: [
        { id: "harvest", variables: [{ id: "zone", valueType: "zone", perCharacter: true, default: null }] },
      ],
    } as never);
    backEnd.rename.mockResolvedValue({ ...ZONE, name: "Blé d'Astrub" });
    render(<ZoneDetails atlas={ATLAS} zone={ZONE} />);
    expect(document.body.textContent).toContain("Zone deKalyx");
    const field = screen.getByRole("textbox", { name: "Nom de la zone" }) as HTMLInputElement;
    fireEvent.change(field, { target: { value: "  " } });
    fireEvent.blur(field);
    expect(field.value).toBe("Mon blé");
    fireEvent.change(field, { target: { value: "Rien" } });
    fireEvent.keyDown(field, { key: "Escape" });
    fireEvent.blur(field);
    expect(backEnd.rename).not.toHaveBeenCalled();
    // The spaces around a name are no change.
    fireEvent.change(field, { target: { value: "Mon blé " } });
    fireEvent.blur(field);
    expect([backEnd.rename.mock.calls.length, field.value]).toEqual([0, "Mon blé"]);
    field.focus();
    fireEvent.change(field, { target: { value: " Blé d'Astrub " } });
    await act(async () => fireEvent.keyDown(field, { key: "Enter" }));
    expect(backEnd.rename.mock.calls).toEqual([["ble", "Blé d'Astrub"]]);
    expect(field.value).toBe("Blé d'Astrub");
  });

  it("puts back a name the back end refuses", async () => {
    backEnd.rename.mockRejectedValue({ kind: "notSaved", reason: "occupé" });
    render(<ZoneDetails atlas={ATLAS} zone={ZONE} />);
    const field = screen.getByRole("textbox", { name: "Nom de la zone" }) as HTMLInputElement;
    fireEvent.change(field, { target: { value: "Refusé" } });
    await act(async () => fireEvent.blur(field));
    expect(field.value).toBe("Mon blé");
  });

  it("deletes it, closing it on the map, and leaves it open when the deletion is refused", async () => {
    backEnd.remove.mockRejectedValueOnce({ kind: "notSaved", reason: "occupé" }).mockResolvedValueOnce(null);
    render(<ZoneDetails atlas={ATLAS} zone={ZONE} />);
    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Supprimer" })));
    expect(useWorld.getState().zone).toBe("ble");
    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Supprimer" })));
    expect(backEnd.remove.mock.calls).toEqual([["ble"], ["ble"]]);
    expect(useWorld.getState().zone).toBeNull();
    expect(useZones.getState().zones).toEqual([]);
  });
});
