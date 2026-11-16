import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { BlueprintSummary_Serialize, VariableView_Serialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";

// The back end, faked: what the settings send, and the atlas the Zone and profession fields read.
const backEnd = vi.hoisted(() => ({ blueprint: vi.fn(), setting: vi.fn(), atlas: vi.fn() }));
vi.mock("@/bindings.ts", () => ({
  commands: {
    setCharacterBlueprint: backEnd.blueprint,
    setCharacterSetting: backEnd.setting,
    worldAtlas: backEnd.atlas,
  },
}));
const editors = vi.hoisted(() => ({ opened: [] as string[] }));
vi.mock("@/shell/parts/editor-store.ts", () => ({
  openEditor: (editor: { id: string }) => editors.opened.push(editor.id),
  useEditors: { getState: () => ({ active: null, api: null }) },
}));
vi.mock("@tauri-apps/api/core", () => ({ convertFileSrc: (path: string) => `asset://${path}` }));
const toasts = vi.hoisted(() => ({ errors: [] as string[] }));
vi.mock("sonner", () => ({ toast: { error: (text: string) => toasts.errors.push(text) } }));

const { CharacterSettings } = await import("./character-settings.tsx");
const { usePlayed } = await import("@/characters/played/played-store.ts");
const { useLibrary } = await import("@/library/library-store.ts");
const { useWorld } = await import("@/world/world-store.ts");

const variable = (id: string, valueType: string, value: string | null, bounds: [number, number] | null = null) =>
  ({
    id,
    name: id,
    valueType,
    default: value,
    bounds,
    perCharacter: true,
    memory: false,
    help: "",
  }) satisfies VariableView_Serialize;

const HARVEST: BlueprintSummary_Serialize = {
  id: "automatic-harvest",
  name: "Automatic harvest",
  description: "",
  given: true,
  version: 1,
  basedOn: null,
  problems: 0,
  variables: [
    variable("zone", "zone", null),
    variable("pods", "percentage", "90", [10, 100]),
    variable("bank", "bank", '"nearest"'),
    variable("keep", "items", '"none"'),
    { ...variable("goal", "number", "0"), perCharacter: false },
  ],
};
const MINE: BlueprintSummary_Serialize = {
  ...HARVEST,
  id: "mine",
  name: "Mine",
  given: false,
  variables: [variable("night", "bool", "false"), variable("note", "text", '"hi"')],
};
const JOBS: BlueprintSummary_Serialize = {
  ...MINE,
  id: "jobs",
  name: "Jobs",
  variables: [variable("job", "job", null)],
};
const names = (fr: string) => ({ fr, en: fr });
const ATLAS = {
  worlds: [],
  jobs: [
    { id: 24, name: names("Mineur"), icon: "icons/jobs/24.png" },
    { id: 2, name: names("Bûcheron"), icon: null },
  ],
  resources: [],
  areas: [
    {
      id: 0,
      name: names("Astrub"),
      subAreas: [{ id: 97, name: names("Forêt d'Astrub"), level: 20, maps: [], resources: [], monsters: 0 }],
    },
  ],
};

function kalyx(blueprint: string | null, settings: Record<string, string> = {}) {
  return {
    key: "294-2",
    name: "Kalyx",
    level: 54,
    breedName: null,
    head: null,
    sex: null,
    lastSeen: 0,
    place: null,
    blueprint,
    settings,
  };
}

async function shown(blueprint: string | null, settings: Record<string, string> = {}) {
  usePlayed.setState({ played: [kalyx(blueprint, settings)] });
  render(<CharacterSettings characterKey="294-2" />);
  // The atlas is read when a Zone field mounts.
  await act(async () => {});
}

/** Opens a list as a click does. */
function open(name: string) {
  fireEvent.pointerDown(screen.getByRole("combobox", { name }), { button: 0, ctrlKey: false, pointerType: "mouse" });
}

describe("a character's blueprint and settings", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    backEnd.blueprint.mockReset().mockResolvedValue(null);
    backEnd.setting.mockReset().mockResolvedValue(null);
    backEnd.atlas.mockReset().mockResolvedValue(ATLAS);
    useWorld.setState({ read: null, picked: null, world: 1, choosing: null });
    editors.opened = [];
    toasts.errors = [];
    useLibrary.setState({ blueprints: [HARVEST, MINE, JOBS] });
  });
  afterEach(cleanup);

  it("runs no blueprint at first, without settings, and its arrow opens nothing", async () => {
    await shown(null);
    expect(screen.getByRole("combobox", { name: "Blueprint" }).textContent).toContain("Aucun blueprint");
    expect(screen.queryByText("Banque à")).toBeNull();
    expect(screen.getByRole("button", { name: "Ouvrir le blueprint" }).hasAttribute("disabled")).toBe(true);
  });

  it("lists D3B's blueprints and mine, and sends the one chosen, or none", async () => {
    await shown(null);
    open("Blueprint");
    const options = screen.getAllByRole("option").map((option) => option.textContent);
    expect(options).toEqual(["Aucun blueprint", "Récolte automatique", "Mine", "Jobs"]);
    fireEvent.click(screen.getByRole("option", { name: "Récolte automatique" }));
    expect(backEnd.blueprint).toHaveBeenCalledWith("294-2", "automatic-harvest");
    cleanup();
    await shown("automatic-harvest");
    open("Blueprint");
    fireEvent.click(screen.getByRole("option", { name: "Aucun blueprint" }));
    expect(backEnd.blueprint).toHaveBeenLastCalledWith("294-2", null);
  });

  it("shows the settings set per character by their names, a bank and items waiting for step 5", async () => {
    await shown("automatic-harvest");
    for (const name of ["Zone", "Banque à", "Banque", "Garder"]) {
      expect(screen.getByText(name, { selector: "span" })).toBeTruthy();
    }
    expect(screen.queryByText("goal")).toBeNull();
    expect(screen.getByRole("combobox", { name: "Zone" }).textContent).toContain("À choisir");
    expect((screen.getByRole("textbox", { name: "Banque à" }) as HTMLInputElement).value).toBe("90");
    expect(screen.getByText("La plus proche")).toBeTruthy();
    expect(screen.getAllByText("étape 5")).toHaveLength(2);
    fireEvent.click(screen.getByRole("button", { name: "Ouvrir le blueprint" }));
    expect(editors.opened).toEqual(["blueprint:automatic-harvest"]);
  });

  it("marks the character's own value, and its reset sends none", async () => {
    await shown("automatic-harvest", { pods: "80" });
    expect((screen.getByRole("textbox", { name: "Banque à" }) as HTMLInputElement).value).toBe("80");
    const reset = screen.getByRole("button", { name: "Revenir à la valeur du blueprint (90 %)" });
    expect(screen.getAllByRole("button", { name: /Revenir/ })).toHaveLength(1);
    fireEvent.click(reset);
    expect(backEnd.setting).toHaveBeenCalledWith("294-2", "pods", null);
  });

  it("sends a number within its bounds, and puts back one out of them without sending it", async () => {
    await shown("automatic-harvest");
    const pods = screen.getByRole("textbox", { name: "Banque à" }) as HTMLInputElement;
    fireEvent.change(pods, { target: { value: "5" } });
    expect(pods.getAttribute("aria-invalid")).toBe("true");
    fireEvent.blur(pods);
    expect(pods.value).toBe("90");
    fireEvent.change(pods, { target: { value: "12.5" } });
    fireEvent.keyDown(pods, { key: "Enter" });
    fireEvent.change(pods, { target: { value: "101" } });
    fireEvent.keyDown(pods, { key: "Enter" });
    expect(pods.value).toBe("90");
    fireEvent.change(pods, { target: { value: "70" } });
    fireEvent.keyDown(pods, { key: "Escape" });
    expect(pods.value).toBe("90");
    fireEvent.blur(pods);
    expect(backEnd.setting).not.toHaveBeenCalled();
    fireEvent.change(pods, { target: { value: "10" } });
    fireEvent.keyDown(pods, { key: "Enter" });
    expect(backEnd.setting.mock.calls).toEqual([["294-2", "pods", "10"]]);
  });

  it("puts a number back when the back end refuses it", async () => {
    backEnd.setting.mockRejectedValue({ kind: "notSaved", reason: "disk full" });
    await shown("automatic-harvest");
    const pods = screen.getByRole("textbox", { name: "Banque à" }) as HTMLInputElement;
    fireEvent.change(pods, { target: { value: "50" } });
    await act(async () => {
      fireEvent.keyDown(pods, { key: "Enter" });
    });
    expect(pods.value).toBe("90");
    expect(toasts.errors).toEqual(["Rien n'a changé pour Kalyx : disk full"]);
  });

  it("says the character's Zone, and sends the one chosen in its list", async () => {
    await shown("automatic-harvest", { zone: '{"sub_area":97}' });
    const zone = screen.getByRole("combobox", { name: "Zone" });
    expect(zone.textContent).toContain("Forêt d'Astrub");
    expect(zone.textContent).toContain("Astrub · niv. 20");
    cleanup();
    await shown("automatic-harvest");
    fireEvent.click(screen.getByRole("combobox", { name: "Zone" }));
    fireEvent.click(screen.getByRole("option", { name: /Forêt d'Astrub/ }));
    expect(backEnd.setting).toHaveBeenCalledWith("294-2", "zone", '{"sub_area":97}');
  });

  it("lists the harvesting professions by name with their icons, and sends the one chosen", async () => {
    await shown("jobs");
    expect(screen.getByRole("combobox", { name: "job" }).textContent).toContain("À choisir");
    open("job");
    const options = screen.getAllByRole("option");
    expect(options.map((option) => option.textContent)).toEqual(["Bûcheron", "Mineur"]);
    expect(options[1]?.querySelector("img")?.getAttribute("src")).toBe("asset://icons/jobs/24.png");
    fireEvent.click(screen.getByRole("option", { name: "Mineur" }));
    expect(backEnd.setting).toHaveBeenCalledWith("294-2", "job", "24");
  });

  it("names a profession the data lacks by its number, and one not a number as none", async () => {
    await shown("jobs", { job: "99" });
    expect(screen.getByRole("combobox", { name: "job" }).textContent).toContain("Métier 99");
    cleanup();
    await shown("jobs", { job: '"farmer"' });
    expect(screen.getByRole("combobox", { name: "job" }).textContent).toContain("À choisir");
  });

  it("switches a yes or no and sends a line of text as JSON", async () => {
    await shown("mine");
    fireEvent.click(screen.getByRole("switch", { name: "night" }));
    const note = screen.getByRole("textbox", { name: "note" }) as HTMLInputElement;
    expect(note.value).toBe("hi");
    fireEvent.change(note, { target: { value: "bye" } });
    fireEvent.blur(note);
    expect(backEnd.setting.mock.calls).toEqual([
      ["294-2", "night", "true"],
      ["294-2", "note", '"bye"'],
    ]);
  });

  it("names a blueprint gone, and says a change refused in a notification", async () => {
    backEnd.setting.mockRejectedValue({ kind: "refused" });
    await shown("deleted", { pods: "80" });
    expect(screen.getByRole("combobox", { name: "Blueprint" }).textContent).toContain("Blueprint deleted introuvable");
    expect(screen.queryByText("Banque à")).toBeNull();
    cleanup();
    await shown("automatic-harvest", { pods: "80" });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /Revenir/ }));
    });
    expect(toasts.errors).toEqual(["Rien n'a changé pour Kalyx : cette valeur n'est pas permise."]);
  });

  it("shows nothing for a character not remembered yet", async () => {
    usePlayed.setState({ played: [] });
    const { container } = render(<CharacterSettings characterKey="294-2" />);
    expect(container.textContent).toBe("");
  });
});
