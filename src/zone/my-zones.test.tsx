import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

// The back end, faked: my zones, and what is asked of them.
const backEnd = vi.hoisted(() => ({ zones: vi.fn(), create: vi.fn(), rename: vi.fn(), remove: vi.fn() }));
vi.mock("@/bindings.ts", () => ({
  commands: {
    zones: backEnd.zones,
    createZone: backEnd.create,
    renameZone: backEnd.rename,
    deleteZone: backEnd.remove,
  },
}));
// The editor area, faked: the editors opened.
const opened = vi.hoisted(() => ({ editors: [] as string[] }));
vi.mock("@/shell/parts/editor-store.ts", () => ({
  openEditor: (editor: { id: string }) => opened.editors.push(editor.id),
}));

const { MyZones } = await import("./my-zones.tsx");
const { useWorld } = await import("@/world/world-store.ts");
const { useZones } = await import("./zones-store.ts");

const ZONES = [
  { id: "ble", name: "Mon blé", world: 1, maps: [[0, 0] as [number, number]] },
  { id: "bois", name: "Bois sombre", world: 2, maps: [] },
];

async function shown(query = "") {
  render(<MyZones query={query} />);
  // My zones are read when the section mounts.
  await act(async () => {});
}

const rows = () => screen.getAllByRole("listitem").map((row) => row.textContent);

describe("my zones in the World view", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    for (const command of [backEnd.zones, backEnd.create, backEnd.rename, backEnd.remove]) {
      command.mockReset();
    }
    backEnd.zones.mockResolvedValue({ zones: ZONES, kept: [] });
    useZones.setState({ zones: [], kept: [], loaded: false });
    useWorld.setState({ zone: null, picked: 3, world: 2, tool: "select" });
    opened.editors.length = 0;
  });
  afterEach(cleanup);

  it("lists my zones by name with their maps, searched without accents, or says there is none", async () => {
    await shown();
    expect(rows()).toEqual(["Bois sombre0 carte", "Mon blé1 carte"]);
    cleanup();
    await shown("ble");
    expect(rows()).toEqual(["Mon blé1 carte"]);
    cleanup();
    backEnd.zones.mockResolvedValue({ zones: [], kept: [] });
    await shown();
    expect(screen.getByText("Aucune zone : « + » en peint une.")).not.toBeNull();
  });

  it("makes a new zone on the world shown with +, and opens it there with the brush", async () => {
    backEnd.zones.mockResolvedValue({ zones: [], kept: [] });
    backEnd.create.mockResolvedValue({ id: "nouvelle-zone", name: "Nouvelle zone", world: 2, maps: [] });
    await shown();
    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Nouvelle zone" })));
    expect(backEnd.create.mock.calls).toEqual([["Nouvelle zone", 2]]);
    expect(useWorld.getState()).toMatchObject({ zone: "nouvelle-zone", world: 2, tool: "brush", picked: null });
    expect(opened.editors).toEqual(["world"]);
  });

  it("opens a zone on its world, and renames it with F2, Escape leaving it as it was", async () => {
    backEnd.rename.mockResolvedValue({ ...ZONES[0], name: "Blé d'Astrub" });
    await shown();
    fireEvent.click(screen.getByRole("button", { name: /Mon blé/ }));
    expect(useWorld.getState()).toMatchObject({ zone: "ble", world: 1 });
    fireEvent.keyDown(screen.getByRole("button", { name: /Mon blé/ }), { key: "F2" });
    const field = screen.getByRole("textbox", { name: "Nom de la zone" });
    fireEvent.change(field, { target: { value: "Rien" } });
    fireEvent.keyDown(field, { key: "Escape" });
    expect(backEnd.rename).not.toHaveBeenCalled();
    fireEvent.keyDown(screen.getByRole("button", { name: /Mon blé/ }), { key: "F2" });
    const again = screen.getByRole("textbox", { name: "Nom de la zone" });
    fireEvent.change(again, { target: { value: "Blé d'Astrub" } });
    await act(async () => fireEvent.keyDown(again, { key: "Enter" }));
    expect(backEnd.rename.mock.calls).toEqual([["ble", "Blé d'Astrub"]]);
    expect(rows()).toContain("Blé d'Astrub1 carte");
    // The row takes the focus back.
    expect(document.activeElement?.textContent).toBe("Blé d'Astrub1 carte");
  });

  it("renames by the menu too, and Escape saves nothing even when the field's removal blurs it", async () => {
    await shown();
    fireEvent.contextMenu(screen.getByRole("button", { name: /Mon blé/ }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Renommer" }));
    const field = screen.getByRole("textbox", { name: "Nom de la zone" });
    fireEvent.change(field, { target: { value: "Perdu" } });
    fireEvent.keyDown(field, { key: "Escape" });
    fireEvent.blur(field);
    expect(backEnd.rename).not.toHaveBeenCalled();
    expect(rows()).toContain("Mon blé1 carte");
  });

  it("says nothing of my zones before they are read", async () => {
    backEnd.zones.mockReturnValue(new Promise(() => {}));
    await shown();
    expect(screen.queryByText("Aucune zone : « + » en peint une.")).toBeNull();
    expect(screen.queryAllByRole("listitem")).toEqual([]);
  });

  it("deletes a zone by its menu, and closes it on the map when it was open", async () => {
    backEnd.remove.mockResolvedValue(null);
    await shown();
    useWorld.setState({ zone: "ble" });
    fireEvent.contextMenu(screen.getByRole("button", { name: /Mon blé/ }));
    await act(async () => fireEvent.click(screen.getByRole("menuitem", { name: "Supprimer" })));
    expect(backEnd.remove.mock.calls).toEqual([["ble"]]);
    expect(rows()).toEqual(["Bois sombre0 carte"]);
    expect(useWorld.getState().zone).toBeNull();
  });

  it("leaves another zone open when one is deleted, and a zone whose deletion is refused", async () => {
    backEnd.remove.mockResolvedValueOnce(null).mockRejectedValueOnce({ kind: "notSaved", reason: "occupé" });
    await shown();
    useWorld.setState({ zone: "bois" });
    fireEvent.contextMenu(screen.getByRole("button", { name: /Mon blé/ }));
    await act(async () => fireEvent.click(screen.getByRole("menuitem", { name: "Supprimer" })));
    expect(useWorld.getState().zone).toBe("bois");
    fireEvent.contextMenu(screen.getByRole("button", { name: /Bois sombre/ }));
    await act(async () => fireEvent.click(screen.getByRole("menuitem", { name: "Supprimer" })));
    expect(useWorld.getState().zone).toBe("bois");
    expect(rows()).toEqual(["Bois sombre0 carte"]);
  });
});
