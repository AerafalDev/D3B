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
  hints: [],
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
    render(<WorldMap world={world} chosen={null} nameAt={() => null} hints={[]} />);
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
    render(<WorldMap world={world} chosen={null} nameAt={() => null} hints={[]} />);
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

  it("fits the chosen maps, drawn in teal, once a box with no size, a tab behind another, is shown", async () => {
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
    render(<WorldMap world={world} chosen={[[0, 0]]} nameAt={() => null} hints={[]} />);
    expect(await zoomSaid()).toBe("20 %");
    const box = screen.getByRole("application");
    Object.defineProperty(box, "clientWidth", { value: 700 });
    Object.defineProperty(box, "clientHeight", { value: 350 });
    act(() => resized());
    expect(await zoomSaid()).toBe("100 %");
    // The chosen map in teal, outlined.
    const [cells, outline] = [...document.querySelectorAll("path")];
    expect([cells?.getAttribute("d"), outline?.getAttribute("stroke")]).toEqual(["M0,0H10V10H0Z", "#39c5bb"]);
    vi.unstubAllGlobals();
  });

  it("says the sub-area and coordinates of the map under the pointer, and nothing off the maps", () => {
    render(
      <WorldMap world={world} chosen={null} nameAt={(x, y) => (x === 7 && y === 7 ? "Forêt" : null)} hints={[]} />,
    );
    const map = screen.getByRole("application");
    const tip = document.querySelector("div[hidden]") as HTMLElement;
    // The view is the whole image centred in an empty box at 50 %: the box's 10, 10 is the image's 70, 70.
    fireEvent.pointerMove(map, { clientX: 10, clientY: 10 });
    expect([tip.hidden, tip.textContent]).toEqual([false, "Forêt · 7, 7"]);
    fireEvent.pointerMove(map, { clientX: 0, clientY: 0 });
    expect(tip.hidden).toBe(true);
  });

  it("picks the map clicked, not one dragged over, and leaves the view where it is for what it picked", async () => {
    const onPick = vi.fn((_x: number, _y: number) => true);
    const shown = (chosen: [number, number][] | null) => (
      <WorldMap world={world} chosen={chosen} nameAt={() => null} hints={[]} onPick={onPick} />
    );
    const { rerender } = render(shown(null));
    await zoomSaid();
    const map = screen.getByRole("application");
    const moved = () => document.querySelector("g")?.getAttribute("transform");
    const before = moved();
    // The box's 10, 10 is the image's 70, 70: map 7, 7.
    fireEvent.pointerDown(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerUp(map, { clientX: 11, clientY: 10 });
    expect(onPick.mock.calls).toEqual([[7, 7]]);
    rerender(shown([[7, 7]]));
    await zoomSaid();
    expect(moved()).toBe(before);
    // A drag moves the map and picks nothing.
    fireEvent.pointerDown(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerMove(map, { clientX: 40, clientY: 40 });
    fireEvent.pointerUp(map, { clientX: 40, clientY: 40 });
    expect(onPick).toHaveBeenCalledTimes(1);
    // A drag that comes back near its start, and a press of another button, pick nothing either.
    fireEvent.pointerDown(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerMove(map, { clientX: 60, clientY: 60 });
    fireEvent.pointerMove(map, { clientX: 11, clientY: 10 });
    fireEvent.pointerUp(map, { clientX: 11, clientY: 10 });
    fireEvent.pointerDown(map, { button: 2, clientX: 10, clientY: 10 });
    fireEvent.pointerUp(map, { button: 2, clientX: 10, clientY: 10 });
    expect(onPick).toHaveBeenCalledTimes(1);
    // A choice made elsewhere, as the view's, fits the view to it: map 1, 1 in an empty box, at the least zoom.
    rerender(shown([[1, 1]]));
    await zoomSaid();
    expect(moved()).toBe("translate(-3 -3) scale(0.2)");
  });

  it("paints a stroke over every map it crosses, shown in amber as it goes, those that do not paint left out", async () => {
    const onStroke = vi.fn();
    const paint = { erase: false, paints: (x: number) => x !== 8, onStroke };
    render(<WorldMap world={world} chosen={null} nameAt={() => null} hints={[]} mine={[[0, 0]]} paint={paint} />);
    await zoomSaid();
    const map = screen.getByRole("application");
    const amber = () => document.querySelector('path[fill="#e2a33a"]')?.getAttribute("d")?.split("M").length;
    expect(amber()).toBe(2);
    // The box's 10, 10 is map 7, 7, its 30, 10 map 11, 7: one move crosses 8, 9 and 10 between.
    fireEvent.pointerDown(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerMove(map, { clientX: 30, clientY: 10 });
    expect(amber()).toBe(6);
    expect(onStroke).not.toHaveBeenCalled();
    fireEvent.pointerUp(map, { clientX: 30, clientY: 10 });
    expect(onStroke.mock.calls).toEqual([
      [
        [
          [7, 7],
          [9, 7],
          [10, 7],
          [11, 7],
        ],
      ],
    ]);
    expect(amber()).toBe(2);
  });

  it("fits the zone opened when nothing is chosen, and not again as it is painted", async () => {
    const paint = { erase: false, paints: () => true, onStroke: () => {} };
    const fitTo: [number, number][] = [[1, 1]];
    const shown = (mine: [number, number][]) => (
      <WorldMap world={world} chosen={null} nameAt={() => null} hints={[]} fitTo={fitTo} mine={mine} paint={paint} />
    );
    const { rerender } = render(shown([[1, 1]]));
    await zoomSaid();
    const moved = () => document.querySelector("g")?.getAttribute("transform");
    // Map 1, 1 in an empty box, at the least zoom.
    expect(moved()).toBe("translate(-3 -3) scale(0.2)");
    fireEvent.keyDown(screen.getByRole("application"), { key: "+" });
    await zoomSaid();
    const zoomed = moved();
    rerender(
      shown([
        [1, 1],
        [5, 5],
      ]),
    );
    await zoomSaid();
    expect(moved()).toBe(zoomed);
  });

  it("paints a stroke with the tool it began with, and drops one the system cancels", async () => {
    const [brush, eraser] = [vi.fn(), vi.fn()];
    const shown = (erase: boolean) => (
      <WorldMap
        world={world}
        chosen={null}
        nameAt={() => null}
        hints={[]}
        mine={[]}
        paint={{ erase, paints: () => true, onStroke: erase ? eraser : brush }}
      />
    );
    const { rerender } = render(shown(false));
    await zoomSaid();
    const map = screen.getByRole("application");
    fireEvent.pointerDown(map, { clientX: 10, clientY: 10 });
    // E pressed during the drag: the stroke stays the brush's.
    rerender(shown(true));
    fireEvent.pointerMove(map, { clientX: 20, clientY: 10 });
    fireEvent.pointerUp(map, { clientX: 20, clientY: 10 });
    expect([brush.mock.calls.length, eraser.mock.calls.length]).toEqual([1, 0]);
    fireEvent.pointerDown(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerCancel(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerUp(map, { clientX: 10, clientY: 10 });
    expect(eraser).not.toHaveBeenCalled();
    expect(document.querySelector('path[fill="#e2a33a"]')?.getAttribute("d")).toBe("");
  });

  it("moves the map, painting nothing, by the middle button and Space held elsewhere", async () => {
    const onStroke = vi.fn();
    const paint = { erase: false, paints: () => true, onStroke };
    const shown = (panning: boolean) => (
      <WorldMap world={world} chosen={null} nameAt={() => null} hints={[]} mine={[]} paint={paint} panning={panning} />
    );
    const { rerender } = render(shown(false));
    await zoomSaid();
    const map = screen.getByRole("application");
    const moved = () => document.querySelector("g")?.getAttribute("transform");
    fireEvent.pointerDown(map, { button: 1, clientX: 10, clientY: 10 });
    fireEvent.pointerMove(map, { button: 1, clientX: 30, clientY: 10 });
    fireEvent.pointerUp(map, { button: 1, clientX: 30, clientY: 10 });
    await zoomSaid();
    expect(moved()).toBe("translate(-5 -25) scale(0.5)");
    rerender(shown(true));
    fireEvent.pointerDown(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerMove(map, { clientX: 10, clientY: 30 });
    fireEvent.pointerUp(map, { clientX: 10, clientY: 30 });
    await zoomSaid();
    expect(moved()).toBe("translate(-5 -5) scale(0.5)");
    expect(onStroke).not.toHaveBeenCalled();
  });

  it("moves the map, painting nothing, by the right button and by Space with the main one", async () => {
    const onStroke = vi.fn();
    const paint = { erase: true, paints: () => true, onStroke };
    render(<WorldMap world={world} chosen={null} nameAt={() => null} hints={[]} mine={[]} paint={paint} />);
    await zoomSaid();
    const map = screen.getByRole("application");
    const moved = () => document.querySelector("g")?.getAttribute("transform");
    fireEvent.pointerDown(map, { button: 2, clientX: 10, clientY: 10 });
    fireEvent.pointerMove(map, { button: 2, clientX: 30, clientY: 10 });
    fireEvent.pointerUp(map, { button: 2, clientX: 30, clientY: 10 });
    await zoomSaid();
    expect(moved()).toBe("translate(-5 -25) scale(0.5)");
    fireEvent.keyDown(map, { key: " " });
    fireEvent.pointerDown(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerMove(map, { clientX: 10, clientY: 30 });
    fireEvent.pointerUp(map, { clientX: 10, clientY: 30 });
    fireEvent.keyUp(map, { key: " " });
    await zoomSaid();
    expect(moved()).toBe("translate(-5 -5) scale(0.5)");
    expect(onStroke).not.toHaveBeenCalled();
    // Space released, the main button erases again.
    fireEvent.pointerDown(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerUp(map, { clientX: 10, clientY: 10 });
    expect(onStroke).toHaveBeenCalledTimes(1);
  });

  it("outlines, dashed, the maps the hovered one belongs with, and nothing off them or once the pointer leaves", () => {
    const forest: [number, number][] = [
      [7, 7],
      [8, 7],
    ];
    const outlineAt = (x: number, y: number) => (y === 7 && (x === 7 || x === 8) ? forest : null);
    const { rerender } = render(
      <WorldMap world={world} chosen={null} nameAt={() => null} hints={[]} outlineAt={outlineAt} />,
    );
    const map = screen.getByRole("application");
    const dashed = () => document.querySelector("path[stroke-dasharray]")?.getAttribute("d") ?? null;
    expect(dashed()).toBeNull();
    // The box's 10, 10 is map 7, 7.
    fireEvent.pointerMove(map, { clientX: 10, clientY: 10 });
    expect(dashed()).toBe("M70,70H80M70,80H80M70,70V80M80,70H90M80,80H90M90,70V80");
    fireEvent.pointerMove(map, { clientX: 0, clientY: 0 });
    expect(dashed()).toBeNull();
    fireEvent.pointerMove(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerLeave(map);
    expect(dashed()).toBeNull();
    // The chosen maps hovered are outlined too; once outlines are no longer asked for, nothing stays.
    rerender(<WorldMap world={world} chosen={forest} nameAt={() => null} hints={[]} outlineAt={outlineAt} />);
    // The view now fits the forest in the empty box at 20 %: the box's 1, -1 is the image's 85, 70, map 8, 7.
    fireEvent.pointerMove(map, { clientX: 1, clientY: -1 });
    expect(dashed()).not.toBeNull();
    rerender(<WorldMap world={world} chosen={forest} nameAt={() => null} hints={[]} />);
    expect(dashed()).toBeNull();
    rerender(<WorldMap world={world} chosen={forest} nameAt={() => null} hints={[]} outlineAt={outlineAt} />);
    expect(dashed()).toBeNull();
  });

  it("fits the view again after a click that picked nothing new", async () => {
    const onPick = vi.fn(() => false);
    const shown = (chosen: [number, number][] | null) => (
      <WorldMap world={world} chosen={chosen} nameAt={() => null} hints={[]} onPick={onPick} />
    );
    const { rerender } = render(shown(null));
    await zoomSaid();
    const map = screen.getByRole("application");
    fireEvent.pointerDown(map, { clientX: 10, clientY: 10 });
    fireEvent.pointerUp(map, { clientX: 10, clientY: 10 });
    expect(onPick).toHaveBeenCalledTimes(1);
    const before = document.querySelector("g")?.getAttribute("transform");
    rerender(shown([[1, 1]]));
    await zoomSaid();
    expect(document.querySelector("g")?.getAttribute("transform")).not.toBe(before);
  });

  it("draws the hints' pictures, a dot for one without, and says a hint's name over its map's", async () => {
    const hint = { name: { fr: "Zaap", en: "Zaap" }, level: 0 };
    render(
      <WorldMap
        world={world}
        chosen={null}
        nameAt={() => "Forêt"}
        hints={[
          { ...hint, id: 1, kind: "zaap", at: [7, 7], picture: "data/icons/hints/410.png" },
          {
            ...hint,
            id: 2,
            kind: "dungeon",
            name: { fr: "Donjon", en: "Dungeon" },
            at: [1, 1],
            level: 50,
            picture: null,
          },
        ]}
      />,
    );
    await zoomSaid();
    // At 50 %, a picture of 28 pixels on the screen is 56 of the image, centred on its map.
    const image = document.querySelector("image");
    expect(["href", "x", "width"].map((name) => image?.getAttribute(name))).toEqual([
      "asset://data/icons/hints/410.png",
      "47",
      "56",
    ]);
    expect(document.querySelector("circle")?.getAttribute("cx")).toBe("15");
    const map = screen.getByRole("application");
    const tip = document.querySelector("div[hidden]") as HTMLElement;
    // The image's 75, 75, the zaap's centre, is the box's 12.5, 12.5; its 15, 15 is -17.5, -17.5.
    fireEvent.pointerMove(map, { clientX: 12, clientY: 12 });
    expect(tip.textContent).toBe("Zaap · 7, 7");
    fireEvent.pointerMove(map, { clientX: -17, clientY: -17 });
    expect(tip.textContent).toBe("Donjon · niv. 50 · 1, 1");
    // Off the pictures, the map's own name.
    fireEvent.pointerMove(map, { clientX: 2, clientY: 40 });
    expect(tip.textContent).toBe("Forêt · 5, 13");
    // Zoomed out to 24 %, the pictures are placed again, 116 pixels of the image, and the dungeon, 60 from the zaap,
    // is crowded out.
    fireEvent.keyDown(map, { key: "-" });
    fireEvent.keyDown(map, { key: "-" });
    fireEvent.keyDown(map, { key: "-" });
    fireEvent.keyDown(map, { key: "-" });
    await zoomSaid();
    expect(document.querySelectorAll("image")).toHaveLength(1);
    expect(Number(document.querySelector("image")?.getAttribute("width"))).toBeCloseTo(28 / (0.5 / 1.2 ** 4));
    expect(document.querySelector("circle")).toBeNull();
  });
});
