import i18next from "i18next";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

// The back end and the notifications, faked: what Duplicate asks, and what it says.
const backEnd = vi.hoisted(() => ({ duplicate: vi.fn(), toasts: [] as string[] }));
vi.mock("@/bindings.ts", () => ({
  commands: {
    duplicateBlueprint: backEnd.duplicate,
    blueprints: async () => ({ blueprints: [], kept: [] }),
    playedCharacters: async () => ({ characters: [], kept: [] }),
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
    backEnd.toasts.length = 0;
  });

  it("copies a given blueprint worded in the player's language, named as a copy", async () => {
    backEnd.duplicate.mockResolvedValue("recolte-automatique-copie");
    expect(await useLibrary.getState().duplicate(harvest)).toBe("recolte-automatique-copie");
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
    backEnd.duplicate.mockRejectedValueOnce({ kind: "notSaved", reason: "disque plein" });
    expect(await useLibrary.getState().duplicate(harvest)).toBeNull();
    expect(backEnd.toasts).toEqual(["Récolte automatique n'a pas été dupliqué : disque plein"]);
  });
});
