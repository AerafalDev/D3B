import { afterEach, describe, expect, it, vi } from "vitest";

// The back end, faked: a map at 5,-18 on the Twelve and its inside at the same coordinates; Blé among the items.
const backEnd = vi.hoisted(() => ({ mapAt: vi.fn(), findMaps: vi.fn(), findItems: vi.fn() }));
vi.mock("@/bindings.ts", () => ({
  commands: { mapAt: backEnd.mapAt, findMaps: backEnd.findMaps, findItems: backEnd.findItems },
  events: {},
}));

const { fromWords, toWords } = await import("./value-words.ts");
const { useZones } = await import("@/zone/zones-store.ts");

const street = { id: 10, at: [5, -18], world: 1, subArea: null };
const inside = { id: 11, at: [5, -18], world: 1, subArea: null };
const wheat = { id: 289, name: { fr: "Blé", en: "Wheat" }, level: 1, kind: { fr: "", en: "" }, icon: null };

describe("a value from the words typed for it, and back", () => {
  afterEach(() => vi.clearAllMocks());

  it("reads numbers, yes or no, durations and times, and refuses what holds none", async () => {
    expect(await fromWords("number", false, "-5")).toBe(-5);
    expect([await fromWords("percentage", false, "90"), await fromWords("percentage", false, "101")]).toEqual([
      90,
      null,
    ]);
    expect([await fromWords("bool", false, "Oui"), await fromWords("bool", false, "no")]).toEqual([true, false]);
    expect(await fromWords("bool", false, "peut-être")).toBeNull();
    expect([await fromWords("duration", false, "1 min 30"), await fromWords("time", false, "9:00")]).toEqual([
      90,
      null,
    ]);
  });

  it("reads a list by its values separated by ';', none when one names nothing", async () => {
    expect(await fromWords("duration", true, "1 min; 45 s")).toEqual([60, 45]);
    expect(await fromWords("duration", true, "1 min; bientôt")).toBeNull();
  });

  it("finds a zone of mine by its name before a sub-area", async () => {
    useZones.setState({ zones: [{ id: "ma-foret", name: "Ma forêt", world: 1, maps: [] }] as never });
    expect(await fromWords("zone", false, "ma foret")).toEqual({ zone: "ma-foret" });
    expect(await fromWords("zone", false, "Nulle part")).toBeNull();
    useZones.setState({ zones: [] });
  });

  it("takes an item by its whole name only, or its id", async () => {
    backEnd.findItems.mockResolvedValue([wheat]);
    expect(await fromWords("item", false, "blé")).toBe(289);
    expect(await fromWords("item", false, "bl")).toBeNull();
    expect(await fromWords("item", false, "400")).toBe(400);
  });

  it("writes a map by its coordinates when they read it back, else by its id", async () => {
    backEnd.mapAt.mockResolvedValue(street);
    backEnd.findMaps.mockImplementation(async (ids: number[]) => [ids[0] === 10 ? street : inside]);
    expect(await toWords("map", false, 10, "fr")).toBe("5,-18");
    expect(await toWords("map", false, 11, "fr")).toBe("11");
    expect(await fromWords("map", false, "5,-18")).toBe(10);
    expect(await fromWords("map", false, "11")).toBe(11);
  });

  it("writes yes or no in the player's language, which reads back the same", async () => {
    expect([await toWords("bool", false, true, "fr"), await toWords("bool", false, false, "en")]).toEqual([
      "oui",
      "no",
    ]);
  });
});
