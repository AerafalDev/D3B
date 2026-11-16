import i18next from "i18next";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

// The back end and the notifications, faked.
const backEnd = vi.hoisted(() => ({
  zones: vi.fn(),
  create: vi.fn(),
  rename: vi.fn(),
  paint: vi.fn(),
  remove: vi.fn(),
  toasts: [] as string[],
}));
vi.mock("@/bindings.ts", () => ({
  commands: {
    zones: backEnd.zones,
    createZone: backEnd.create,
    renameZone: backEnd.rename,
    paintZone: backEnd.paint,
    deleteZone: backEnd.remove,
  },
}));
vi.mock("sonner", () => ({ toast: { error: (text: string) => backEnd.toasts.push(text) } }));

const { stroked, useZones } = await import("./zones-store.ts");

const zone = (id: string, maps: [number, number][] = []) => ({ id, name: id, world: 1, maps });

describe("my zones", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    for (const command of [backEnd.zones, backEnd.create, backEnd.rename, backEnd.paint, backEnd.remove]) {
      command.mockReset();
    }
    backEnd.toasts.length = 0;
    useZones.setState({ zones: [], kept: [], loaded: false });
  });

  it("reads my zones, dropping an older read that ends after a newer one, and says a read that fails", async () => {
    let first: (value: unknown) => void = () => {};
    backEnd.zones.mockReturnValueOnce(new Promise((resolve) => (first = resolve)));
    const older = useZones.getState().refresh();
    backEnd.zones.mockResolvedValueOnce({ zones: [zone("new")], kept: [] });
    await useZones.getState().refresh();
    first({ zones: [zone("old")], kept: [] });
    await older;
    expect(useZones.getState()).toMatchObject({ zones: [zone("new")], loaded: true });
    backEnd.zones.mockRejectedValueOnce({ kind: "notSaved", reason: "accès refusé" });
    await useZones.getState().refresh();
    expect(backEnd.toasts).toEqual(["Tes zones n'ont pas pu être lues : accès refusé"]);
    expect(useZones.getState().zones).toEqual([zone("new")]);
  });

  it("keeps every zone when a zone made answers before a read asked earlier, which it reads again", async () => {
    let read: (value: unknown) => void = () => {};
    backEnd.zones.mockReturnValueOnce(new Promise((resolve) => (read = resolve)));
    const reading = useZones.getState().refresh();
    backEnd.create.mockResolvedValue(zone("c"));
    backEnd.zones.mockResolvedValueOnce({ zones: [zone("a"), zone("c")], kept: [] });
    await useZones.getState().create(1);
    // The read asked before the zone was made, which lacks it, comes last: dropped.
    read({ zones: [zone("a")], kept: [] });
    await reading;
    await Promise.resolve();
    expect(useZones.getState().zones.map((one) => one.id)).toEqual(["a", "c"]);
  });

  it("says a zone not made, and opens none", async () => {
    backEnd.create.mockRejectedValue({ kind: "noFolder" });
    expect(await useZones.getState().create(1)).toBeNull();
    expect(backEnd.toasts).toEqual(["Rien n'a changé : le système ne donne aucun dossier pour tes fichiers."]);
    expect(useZones.getState().zones).toEqual([]);
  });

  it("says a name refused, and reads the zones again", async () => {
    useZones.setState({ zones: [zone("a")] });
    backEnd.rename.mockRejectedValue({ kind: "emptyName" });
    backEnd.zones.mockResolvedValue({ zones: [zone("a")], kept: [] });
    await useZones.getState().rename("a", "x");
    expect(backEnd.toasts).toEqual(["Une zone a besoin d'un nom."]);
    expect(backEnd.zones).toHaveBeenCalledTimes(1);
  });

  it("adds a stroke's maps once, in order, and takes the eraser's away", () => {
    const painted = stroked(
      zone("a", [[1, 0]]),
      [
        [0, 5],
        [1, 0],
        [0, -2],
      ],
      false,
    );
    expect(painted.maps).toEqual([
      [0, -2],
      [0, 5],
      [1, 0],
    ]);
    expect(
      stroked(
        painted,
        [
          [0, 5],
          [9, 9],
        ],
        true,
      ).maps,
    ).toEqual([
      [0, -2],
      [1, 0],
    ]);
  });

  it("creates a zone named in the player's language on the world given", async () => {
    backEnd.create.mockResolvedValue(zone("nouvelle-zone"));
    backEnd.zones.mockResolvedValue({ zones: [zone("nouvelle-zone")], kept: [] });
    expect(await useZones.getState().create(2)).toBe("nouvelle-zone");
    expect(backEnd.create.mock.calls).toEqual([["Nouvelle zone", 2]]);
    expect(useZones.getState().zones.map((one) => one.id)).toEqual(["nouvelle-zone"]);
  });

  it("shows a stroke at once, keeps the latest answer, and reads the zones again when it fails", async () => {
    useZones.setState({ zones: [zone("a")] });
    let answer: (value: unknown) => void = () => {};
    backEnd.paint.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    const first = useZones.getState().paint("a", [[1, 1]], false);
    expect(useZones.getState().zones[0]?.maps).toEqual([[1, 1]]);
    backEnd.paint.mockResolvedValueOnce(
      zone("a", [
        [1, 1],
        [2, 2],
      ]),
    );
    await useZones.getState().paint("a", [[2, 2]], false);
    // The first stroke's answer comes last: dropped.
    answer(zone("a", [[1, 1]]));
    await first;
    expect(useZones.getState().zones[0]?.maps).toEqual([
      [1, 1],
      [2, 2],
    ]);
    backEnd.paint.mockRejectedValueOnce({ kind: "noSuchZone" });
    backEnd.zones.mockResolvedValue({ zones: [], kept: [] });
    await useZones.getState().paint("a", [[3, 3]], false);
    expect(backEnd.toasts).toEqual(["Cette zone n'existe plus : rien n'a changé."]);
    expect(useZones.getState().zones).toEqual([]);
  });

  it("leaves a blank name, says a refusal, and drops a zone deleted", async () => {
    useZones.setState({ zones: [zone("a"), zone("b")] });
    await useZones.getState().rename("a", "   ");
    expect(backEnd.rename).not.toHaveBeenCalled();
    backEnd.rename.mockResolvedValue({ ...zone("a"), name: "Bois" });
    await useZones.getState().rename("a", "Bois");
    expect(useZones.getState().zones[0]?.name).toBe("Bois");
    backEnd.remove.mockResolvedValueOnce(null);
    expect(await useZones.getState().remove("a")).toBe(true);
    backEnd.remove.mockRejectedValueOnce({ kind: "notSaved", reason: "disque plein" });
    expect(await useZones.getState().remove("b")).toBe(false);
    expect(useZones.getState().zones.map((one) => one.id)).toEqual(["b"]);
    expect(backEnd.toasts).toEqual(["Rien n'a changé : disque plein"]);
  });
});
