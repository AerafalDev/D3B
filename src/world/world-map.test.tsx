import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { AtlasWorld } from "./map-geometry.ts";

// The asset protocol's URL of a file, as Tauri makes it; happy-dom has no Tauri.
vi.mock("@tauri-apps/api/core", () => ({ convertFileSrc: (path: string) => `asset://${path}` }));

const { WorldMap } = await import("./world-map.tsx");

const world: AtlasWorld = {
  id: 1,
  name: { fr: "Monde des Douze", en: "The World of Twelve" },
  origin: [0, 0],
  mapSize: [10, 10],
  size: [100, 100],
  minScale: 0.2,
  maxScale: 1,
  startScale: 0.5,
  zooms: [],
};

/** The zoom the map says, once its next frame is drawn. */
async function zoomSaid(): Promise<string> {
  await act(() => new Promise((done) => requestAnimationFrame(() => done(undefined))));
  // French writes a narrow space before %.
  return (document.querySelector("span.tabular-nums")?.textContent ?? "").replace(/\s/g, " ");
}

describe("the World's map", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("opens at the world's starting zoom, and zooms by its keys and buttons within the client's bounds", async () => {
    render(<WorldMap world={world} chosen={null} nameAt={() => null} />);
    expect(await zoomSaid()).toBe("50 %");
    const map = screen.getByRole("application", { name: /Carte de Monde des Douze/ });
    fireEvent.keyDown(map, { key: "+" });
    expect(await zoomSaid()).toBe("60 %");
    for (let press = 0; press < 10; press += 1) {
      fireEvent.click(screen.getByRole("button", { name: "Zoom avant" }));
    }
    expect(await zoomSaid()).toBe("100 %");
    for (let press = 0; press < 20; press += 1) {
      fireEvent.keyDown(map, { key: "-" });
    }
    expect(await zoomSaid()).toBe("20 %");
    fireEvent.keyDown(map, { key: "0" });
    expect(await zoomSaid()).toBe("50 %");
  });

  it("moves by its arrows, zooms not on a sideways scroll, and fits again by the 0 key's place", async () => {
    render(<WorldMap world={world} chosen={null} nameAt={() => null} />);
    const map = screen.getByRole("application");
    await zoomSaid();
    const moved = () => document.querySelector("g")?.getAttribute("transform");
    // The whole image centred in an empty box at 50 %.
    expect(moved()).toBe("translate(-25 -25) scale(0.5)");
    fireEvent.keyDown(map, { key: "ArrowRight" });
    await zoomSaid();
    expect(moved()).toBe("translate(-105 -25) scale(0.5)");
    fireEvent.wheel(map, { deltaX: 120, deltaY: 0 });
    expect(await zoomSaid()).toBe("50 %");
    fireEvent.wheel(map, { deltaY: -100 });
    expect(await zoomSaid()).toBe("60 %");
    // AZERTY's 0 key, unshifted.
    fireEvent.keyDown(map, { key: "à", code: "Digit0" });
    await zoomSaid();
    expect(moved()).toBe("translate(-25 -25) scale(0.5)");
  });

  it("fits the chosen maps once a box with no size, a tab behind another, is shown", async () => {
    let resized = () => {};
    vi.stubGlobal(
      "ResizeObserver",
      class {
        constructor(callback: () => void) {
          resized = callback;
        }
        observe() {}
        disconnect() {}
      },
    );
    render(<WorldMap world={world} chosen={[[0, 0]]} nameAt={() => null} />);
    expect(await zoomSaid()).toBe("20 %");
    const box = screen.getByRole("application");
    Object.defineProperty(box, "clientWidth", { value: 700 });
    Object.defineProperty(box, "clientHeight", { value: 350 });
    act(() => resized());
    expect(await zoomSaid()).toBe("100 %");
    vi.unstubAllGlobals();
  });

  it("outlines the chosen maps in teal", () => {
    const { container } = render(<WorldMap world={world} chosen={[[0, 0]]} nameAt={() => null} />);
    const [cells, outline] = [...container.querySelectorAll("path")];
    expect(cells?.getAttribute("d")).toBe("M0,0H10V10H0Z");
    expect(outline?.getAttribute("stroke")).toBe("#39c5bb");
  });

  it("says the sub-area and coordinates of the map under the pointer, and nothing off the maps", () => {
    render(<WorldMap world={world} chosen={null} nameAt={(x, y) => (x === 7 && y === 7 ? "Forêt" : null)} />);
    const map = screen.getByRole("application");
    const tip = document.querySelector("div[hidden]") as HTMLElement;
    // The view is the whole image centred in an empty box at 50 %: the box's 10, 10 is the image's 70, 70.
    fireEvent.pointerMove(map, { clientX: 10, clientY: 10 });
    expect([tip.hidden, tip.textContent]).toEqual([false, "Forêt · 7, 7"]);
    fireEvent.pointerMove(map, { clientX: 0, clientY: 0 });
    expect(tip.hidden).toBe(true);
  });
});
