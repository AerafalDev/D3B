import i18next from "i18next";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

// The back end, the notifications and the editor area, faked: what export and import ask, say and open.
const backEnd = vi.hoisted(() => ({
  exportBlueprint: vi.fn(),
  pickShared: vi.fn(),
  previewShared: vi.fn(),
  importShared: vi.fn(),
  blueprints: vi.fn(),
  played: vi.fn(),
  toasts: [] as string[],
  done: [] as string[],
  opened: [] as unknown[],
}));
vi.mock("@/bindings.ts", () => ({
  commands: {
    exportBlueprint: backEnd.exportBlueprint,
    pickShared: backEnd.pickShared,
    previewShared: backEnd.previewShared,
    importShared: backEnd.importShared,
    blueprints: backEnd.blueprints,
    playedCharacters: backEnd.played,
  },
  events: {},
}));
vi.mock("sonner", () => ({
  toast: {
    error: (text: string) => backEnd.toasts.push(text),
    success: (text: string) => backEnd.done.push(text),
  },
}));
vi.mock("@/shell/parts/editor-store.ts", () => ({
  openEditor: (editor: unknown) => backEnd.opened.push(editor),
}));

const { useShare } = await import("./share-store.ts");

const harvest = {
  id: "automatic-harvest",
  name: "Automatic harvest",
  description: "Harvests what grows.",
  given: true,
  version: 1,
  basedOn: null,
  problems: 0,
  variables: [],
};

const preview = {
  path: "C:\\Users\\me\\Downloads\\Ma récolte.d3b",
  blueprint: { ...harvest, id: "ma-recolte", name: "Ma récolte", given: false },
  problems: [],
  graphs: 1,
  nodes: 4,
  zones: 0,
  routes: 0,
};

describe("the .d3b file", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    for (const command of [backEnd.exportBlueprint, backEnd.pickShared, backEnd.previewShared, backEnd.importShared]) {
      command.mockReset();
    }
    backEnd.blueprints.mockReset().mockResolvedValue({ blueprints: [], kept: [] });
    backEnd.played.mockReset().mockResolvedValue({ characters: [], kept: [] });
    backEnd.toasts.length = 0;
    backEnd.done.length = 0;
    backEnd.opened.length = 0;
    useShare.setState({ preview: null, importing: false });
  });

  it("exports a given blueprint worded in the player's language and says where", async () => {
    backEnd.exportBlueprint.mockResolvedValue("C:\\Users\\me\\Récolte automatique.d3b");
    await useShare.getState().exportBlueprint(harvest);
    const [id, texts] = backEnd.exportBlueprint.mock.calls[0] ?? [];
    expect(id).toBe("automatic-harvest");
    expect(texts.name).toBe("Récolte automatique");
    expect(backEnd.done).toEqual(["Récolte automatique exporté dans Récolte automatique.d3b."]);
  });

  it("exports mine as it is, and says nothing when the dialog is cancelled", async () => {
    backEnd.exportBlueprint.mockResolvedValue(null);
    await useShare.getState().exportBlueprint({ ...harvest, id: "ma-recolte", name: "Ma récolte", given: false });
    expect(backEnd.exportBlueprint).toHaveBeenCalledWith("ma-recolte", null);
    expect([...backEnd.done, ...backEnd.toasts]).toEqual([]);
  });

  it("previews a file chosen or dropped, then imports it among mine and opens its tab", async () => {
    backEnd.pickShared.mockResolvedValue(null);
    await useShare.getState().pick();
    expect(useShare.getState().preview).toBeNull();
    backEnd.previewShared.mockResolvedValue(preview);
    await useShare.getState().open(preview.path);
    expect(useShare.getState().preview).toEqual(preview);
    backEnd.importShared.mockResolvedValue("ma-recolte-2");
    await useShare.getState().confirm();
    expect(backEnd.importShared).toHaveBeenCalledWith(preview.path);
    expect(useShare.getState().preview).toBeNull();
    expect(backEnd.blueprints).toHaveBeenCalled();
    expect(backEnd.opened).toEqual([
      expect.objectContaining({ id: "blueprint:ma-recolte-2", params: { icon: "blueprint", id: "ma-recolte-2" } }),
    ]);
  });

  it("says why a file is refused, and keeps the preview when the import fails", async () => {
    backEnd.previewShared.mockRejectedValue({ kind: "newer", found: 2 });
    await useShare.getState().open("x.d3b");
    backEnd.pickShared.mockRejectedValue({ kind: "notD3b" });
    await useShare.getState().pick();
    expect(backEnd.toasts).toEqual([
      "Le fichier n'a pas été importé : écrit par un D3B plus récent (format 2) : mettez D3B à jour.",
      "Le fichier n'a pas été importé : ce n'est pas un fichier .d3b.",
    ]);
    useShare.setState({ preview });
    backEnd.importShared.mockRejectedValue({ kind: "notSaved", reason: "disque plein" });
    await useShare.getState().confirm();
    expect(backEnd.toasts[2]).toBe("Le fichier n'a pas été importé : disque plein");
    expect(useShare.getState()).toMatchObject({ preview, importing: false });
    backEnd.exportBlueprint.mockRejectedValue({ kind: "noSuchBlueprint" });
    await useShare.getState().exportBlueprint(harvest);
    expect(backEnd.toasts[3]).toBe("Récolte automatique n'a pas été exporté : il n'est plus là.");
  });
});
