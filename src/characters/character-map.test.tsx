import { cleanup, render, screen, waitFor } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { CharacterView } from "./character-store.ts";

const backEnd = vi.hoisted(() => ({ read: vi.fn() }));
vi.mock("@/lib/tauri.ts", () => ({ isTauri: () => true }));
vi.mock("@/bindings.ts", () => ({ commands: { mapCells: backEnd.read } }));

const { CharacterMap } = await import("./character-map.tsx");

// Only where the character is and whether it fights decide what the map shows.
const exploring = {
  fighting: false,
  place: { mapId: 88_213_271, at: { x: 4, y: -18 }, subArea: null, area: null },
  cell: 288,
  actors: [],
  stated: [],
} as unknown as CharacterView;

describe("the character's map", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  afterEach(() => {
    cleanup();
    backEnd.read.mockReset();
  });

  it("reads its map's cells and draws them with a legend", async () => {
    backEnd.read.mockResolvedValue({ flags: [1], exits: [], elements: [] });
    render(<CharacterMap view={exploring} />);
    expect(backEnd.read).toHaveBeenCalledWith(88_213_271);
    expect(screen.getByRole("img", { name: "The character's map" })).toBeTruthy();
    expect(screen.getByText("Monsters")).toBeTruthy();
  });

  it("says why it cannot be drawn when the data lacks the map", async () => {
    backEnd.read.mockRejectedValue("no map 88213271 in the game data");
    render(<CharacterMap view={exploring} />);
    await waitFor(() =>
      expect(screen.getByText("The map cannot be drawn: no map 88213271 in the game data")).toBeTruthy(),
    );
  });

  it("says positions in fights are not read yet", () => {
    backEnd.read.mockResolvedValue({ flags: [1], exits: [], elements: [] });
    render(<CharacterMap view={{ ...exploring, fighting: true }} />);
    expect(screen.getByText(/In a fight: positions in fights are read in a later step/)).toBeTruthy();
  });

  it("waits for a map the data places", () => {
    render(<CharacterMap view={{ ...exploring, place: null }} />);
    expect(screen.getByText("No map yet")).toBeTruthy();
    expect(backEnd.read).not.toHaveBeenCalled();
  });
});
