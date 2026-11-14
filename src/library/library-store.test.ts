import i18next from "i18next";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

// The back end and the notifications, faked: what Duplicate asks, and what it says.
const backEnd = vi.hoisted(() => ({
  duplicate: vi.fn(),
  blueprints: vi.fn(),
  played: vi.fn(),
  toasts: [] as string[],
}));
vi.mock("@/bindings.ts", () => ({
  commands: {
    duplicateBlueprint: backEnd.duplicate,
    blueprints: backEnd.blueprints,
    playedCharacters: backEnd.played,
  },
  events: {},
}));
vi.mock("sonner", () => ({ toast: { error: (text: string) => backEnd.toasts.push(text) } }));

const { useLibrary } = await import("./library-store.ts");

const harvest = {
  id: "automatic-harvest",
  name: "Automatic harvest",
  description: "Harvests what grows.",
  given: true,
  version: 1,
  basedOn: null,
  problems: 0,
  variables: [
    { id: "zone", name: "Zone", valueType: "zone", default: null, perCharacter: true, memory: false, help: "Where." },
  ],
};

describe("Duplicate", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    backEnd.duplicate.mockReset();
    backEnd.blueprints.mockReset().mockResolvedValue({ blueprints: [], kept: [] });
    backEnd.played.mockReset().mockResolvedValue({ characters: [], kept: [] });
    backEnd.toasts.length = 0;
  });

  it("copies a given blueprint worded in the player's language, named as a copy", async () => {
    backEnd.duplicate.mockResolvedValue("recolte-automatique-copie");
    backEnd.blueprints.mockResolvedValue({
      blueprints: [harvest, { ...harvest, id: "recolte-automatique-copie" }],
      kept: [],
    });
    expect(await useLibrary.getState().duplicate(harvest)).toBe("recolte-automatique-copie");
    // Read again, so the copy's tab finds it.
    expect(useLibrary.getState().blueprints.map((one) => one.id)).toContain("recolte-automatique-copie");
    expect(backEnd.duplicate).toHaveBeenCalledWith("automatic-harvest", {
      name: "Récolte automatique (copie)",
      description: "Récolte ce qui pousse dans une zone, vide ses pods à la banque, et recommence.",
      variables: { zone: { name: "Zone", help: "Où récolter." } },
    });
  });

  it("copies one of mine as it is written, named as a copy", async () => {
    backEnd.duplicate.mockResolvedValue("ma-chasse-copie");
    const mine = { ...harvest, id: "ma-chasse", name: "Ma chasse", given: false, description: "À moi." };
    await useLibrary.getState().duplicate(mine);
    expect(backEnd.duplicate).toHaveBeenCalledWith("ma-chasse", {
      name: "Ma chasse (copie)",
      description: "À moi.",
      variables: { zone: { name: "Zone", help: "Where." } },
    });
  });

  it("says why in a notification when no copy was made", async () => {
    for (const failure of [
      { kind: "notSaved", reason: "disque plein" },
      { kind: "noSuchBlueprint" },
      { kind: "noFolder" },
    ]) {
      backEnd.duplicate.mockRejectedValueOnce(failure);
      expect(await useLibrary.getState().duplicate(harvest)).toBeNull();
    }
    expect(backEnd.toasts).toEqual([
      "Récolte automatique n'a pas été dupliqué : disque plein",
      "Récolte automatique n'a pas été dupliqué : il n'est plus là.",
      "Récolte automatique n'a pas été dupliqué : le système ne donne pas de dossier pour vos fichiers.",
    ]);
  });
});

describe("the Library's list", () => {
  const kept = (path: string) => ({ path, why: { kind: "notText" as const } });

  beforeEach(() => {
    backEnd.toasts.length = 0;
  });

  it("reads the blueprints with the files kept, the characters' among them", async () => {
    backEnd.blueprints.mockResolvedValueOnce({ blueprints: [harvest], kept: [kept("a.json")] });
    backEnd.played.mockResolvedValueOnce({ characters: [], kept: [kept("294-1.json")] });
    await useLibrary.getState().refresh();
    expect(useLibrary.getState().blueprints).toEqual([harvest]);
    expect(useLibrary.getState().kept.map((file) => file.path)).toEqual(["a.json", "294-1.json"]);
  });

  it("drops an older read that ends after a newer one", async () => {
    let older: (list: unknown) => void = () => {};
    backEnd.blueprints.mockImplementationOnce(() => new Promise((resolve) => (older = resolve)));
    backEnd.blueprints.mockResolvedValueOnce({ blueprints: [harvest], kept: [] });
    const first = useLibrary.getState().refresh();
    await useLibrary.getState().refresh();
    older({ blueprints: [], kept: [] });
    await first;
    expect(useLibrary.getState().blueprints).toEqual([harvest]);
  });

  it("says in a notification when the list could not be read", async () => {
    backEnd.blueprints.mockRejectedValueOnce("the task panicked");
    await useLibrary.getState().refresh();
    expect(backEnd.toasts).toEqual(["La Bibliothèque n'a pas pu être lue : the task panicked"]);
  });
});
