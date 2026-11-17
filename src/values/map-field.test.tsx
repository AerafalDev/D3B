import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

// The back end, faked: the map at coordinates of the Twelve, and the maps by id.
const backEnd = vi.hoisted(() => ({ mapAt: vi.fn(), findMaps: vi.fn(), opened: vi.fn() }));
vi.mock("@/bindings.ts", () => ({
  commands: { mapAt: backEnd.mapAt, findMaps: backEnd.findMaps },
  events: {},
}));
vi.mock("@/world/open-world.ts", () => ({ openWorld: backEnd.opened }));

const { MapField } = await import("./map-field.tsx");
const { useWorld } = await import("@/world/world-store.ts");
const { useCharacters } = await import("@/characters/character-store.ts");

const fields = { id: 88_080_385, at: [5, -18], world: 1, subArea: { fr: "Champs d'Astrub", en: "Astrub Fields" } };

describe("a map's field", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });

  beforeEach(() => {
    backEnd.findMaps.mockImplementation(async (ids: number[]) => (ids.includes(fields.id) ? [fields] : []));
    backEnd.mapAt.mockImplementation(async (_world: number, x: number, y: number) =>
      x === 5 && y === -18 ? fields : null,
    );
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    useCharacters.setState({ views: {} });
  });

  const shown = (value: string | null) => {
    const onChange = vi.fn(async () => true);
    render(<MapField value={value} onChange={onChange} who="Lyra" label="Carte" />);
    return onChange;
  };

  it("shows the map chosen by its coordinates and sub-area", async () => {
    shown(String(fields.id));
    expect(await screen.findByText("5,-18 · Champs d'Astrub")).toBeTruthy();
  });

  it("takes coordinates typed on the Twelve, and marks those where no map is or that are no coordinates", async () => {
    const onChange = shown(null);
    const typed = screen.getByRole("textbox", { name: "Carte : coordonnées" });
    for (const [text, kept] of [
      ["5;-18", false],
      ["9,9", false],
      ["5, -18", true],
    ] as const) {
      fireEvent.change(typed, { target: { value: text } });
      await act(async () => {
        fireEvent.keyDown(typed, { key: "Enter" });
      });
      expect(typed.getAttribute("aria-invalid")).toBe(String(!kept));
    }
    expect(backEnd.mapAt).toHaveBeenLastCalledWith(1, 5, -18);
    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenLastCalledWith(String(fields.id));
  });

  it("takes coordinates on blur too, once, and says no map without the game data", async () => {
    const onChange = shown(null);
    const typed = screen.getByRole("textbox", { name: "Carte : coordonnées" });
    fireEvent.change(typed, { target: { value: "5,-18" } });
    await act(async () => {
      fireEvent.keyDown(typed, { key: "Enter" });
      fireEvent.blur(typed);
    });
    expect(onChange).toHaveBeenCalledTimes(1);
    backEnd.mapAt.mockRejectedValue({ kind: "noData", reason: "no data" });
    fireEvent.change(typed, { target: { value: "6,-18" } });
    await act(async () => {
      fireEvent.blur(typed);
    });
    expect(typed.getAttribute("aria-invalid")).toBe("true");
    cleanup();
    backEnd.findMaps.mockRejectedValue({ kind: "noData", reason: "no data" });
    shown(String(fields.id));
    expect(await screen.findByText(`Carte ${fields.id}`)).toBeTruthy();
  });

  it("takes nothing from a choice on the World cancelled", () => {
    const onChange = shown(null);
    fireEvent.click(screen.getByRole("button", { name: "Choisir Carte sur le Monde" }));
    useWorld.getState().endChoosing(null);
    expect(onChange).not.toHaveBeenCalled();
  });

  it("chooses on the World a map, which the field takes", () => {
    const onChange = shown(null);
    fireEvent.click(screen.getByRole("button", { name: "Choisir Carte sur le Monde" }));
    expect(backEnd.opened).toHaveBeenCalled();
    expect(useWorld.getState().choosing?.kind).toBe("map");
    useWorld.getState().choosing?.choose(fields.id);
    expect(onChange).toHaveBeenLastCalledWith(String(fields.id));
  });

  it("takes the map a character online stands on", () => {
    useCharacters.setState({
      views: { 1: { name: "Lyra", place: { mapId: 99, at: null, subArea: null, area: null } } } as never,
    });
    const onChange = shown(null);
    fireEvent.click(screen.getByRole("button", { name: "Où est Lyra" }));
    expect(onChange).toHaveBeenLastCalledWith("99");
  });
});
