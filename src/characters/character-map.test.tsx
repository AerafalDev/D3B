import { cleanup, render, screen, waitFor } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { CharacterView } from "./character-store.ts";

const backEnd = vi.hoisted(() => ({ read: vi.fn() }));
vi.mock("@/lib/tauri.ts", () => ({ isTauri: () => true }));
vi.mock("@/bindings.ts", () => ({ commands: { mapCells: backEnd.read } }));

const { CharacterMap } = await import("./character-map.tsx");

// happy-dom lays out and paints nothing: a fake 2D context counts the diamonds and dots drawn, a fake observer gives the
// map's box a size, and a frame runs at once.
const painted = { diamonds: 0, dots: 0 };
const context = {
  setTransform: () => {},
  clearRect: () => {},
  beginPath: () => {},
  moveTo: () => {},
  lineTo: () => {},
  closePath: () => {},
  fill: () => {},
  stroke: () => {},
  arc: () => {
    painted.dots += 1;
  },
};
type Resized = (entries: { contentRect: { width: number; height: number } }[]) => void;
class FakeResizeObserver {
  private readonly callback: Resized;
  constructor(callback: Resized) {
    this.callback = callback;
  }
  observe() {
    this.callback([{ contentRect: { width: 580, height: 410 } }]);
  }
  disconnect() {}
}

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
  beforeEach(() => {
    painted.diamonds = 0;
    painted.dots = 0;
    context.closePath = () => {
      painted.diamonds += 1;
    };
    vi.stubGlobal("ResizeObserver", FakeResizeObserver);
    vi.stubGlobal("requestAnimationFrame", (run: () => void) => {
      run();
      return 0;
    });
    vi.stubGlobal("matchMedia", () => ({ addEventListener: () => {}, removeEventListener: () => {} }));
    // The fake context stands in for the canvas's own, which happy-dom does not have.
    vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(context as never);
  });
  afterEach(() => {
    cleanup();
    backEnd.read.mockReset();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("reads its map's cells, then draws the ground and what stands on it", async () => {
    backEnd.read.mockResolvedValue({ flags: [1, 0, 1], exits: [2], elements: [] });
    const withActor = { ...exploring, cell: 0, actors: [{ id: 7, kind: { kind: "player", name: "M" }, cell: 2 }] };
    render(<CharacterMap view={withActor as CharacterView} />);
    expect(backEnd.read).toHaveBeenCalledWith(88_213_271);
    // Two cells drawn (the walkable one and the exit), then the player's dot and the character's ringed one.
    await waitFor(() => expect(painted.diamonds).toBe(2));
    expect(painted.dots).toBe(3);
    expect(screen.getByText("Monsters")).toBeTruthy();
  });

  it("leaves a map's notice and drawing behind while the next map's cells are read", async () => {
    backEnd.read.mockRejectedValueOnce("no map 88213271 in the game data");
    const { rerender } = render(<CharacterMap view={exploring} />);
    await waitFor(() => expect(screen.getByText(/The map cannot be drawn/)).toBeTruthy());
    backEnd.read.mockReturnValueOnce(new Promise(() => {}));
    const next = { ...exploring, place: { ...exploring.place, mapId: 1 } } as CharacterView;
    rerender(<CharacterMap view={next} />);
    expect(screen.queryByText(/The map cannot be drawn/)).toBeNull();
    expect(screen.getByRole("img", { name: "The character's map" })).toBeTruthy();
    expect(painted.diamonds).toBe(0);
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
