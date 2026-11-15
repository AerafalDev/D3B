import { convertFileSrc } from "@tauri-apps/api/core";
import { Maximize, Minus, Plus } from "lucide-react";
import { type KeyboardEvent, type PointerEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import { named } from "@/characters/names.ts";
import {
  type AtlasWorld,
  fitView,
  levelFor,
  mapAt,
  mapsPaths,
  type PlacedTile,
  type View,
  visibleTiles,
  zoomAbout,
} from "./map-geometry.ts";

/** The sub-area chosen's colour, mock-up 10's teal (the owner, 2026-11-15). */
const TEAL = "#39c5bb";
/** A notch of the wheel, a press of + or −. */
const STEP = 1.2;
/** An arrow key's move, in screen pixels. */
const PAN = 80;

interface WorldMapProps {
  world: AtlasWorld;
  /** The maps to show in teal, outlined, and to fit the view to; `null` for none. */
  chosen: readonly (readonly [number, number])[] | null;
  /** The name said on hover of the map at `x, y`, if any. */
  nameAt: (x: number, y: number) => string | null;
}

/**
 * The world's map drawn by hand from its tiles (the owner, 2026-11-15): the level the zoom needs, only the tiles in
 * view, moved and scaled as one; the chosen maps in translucent teal with a sharp outline; a map's sub-area and
 * coordinates on hover. The wheel, the arrows and + / − move and zoom it within the client's bounds; 0 fits it again.
 * The view lives in a ref and reaches the page each frame, React drawing only when the tiles in view change.
 */
export function WorldMap({ world, chosen, nameAt }: WorldMapProps) {
  const { t, i18n } = useTranslation();
  const box = useRef<HTMLDivElement>(null);
  const moved = useRef<SVGGElement>(null);
  const tip = useRef<HTMLDivElement>(null);
  const zoom = useRef<HTMLSpanElement>(null);
  const view = useRef<View>({ x: 0, y: 0, scale: world.startScale });
  const frame = useRef(0);
  const drag = useRef<{ x: number; y: number } | null>(null);
  const unfitted = useRef(false);
  const [tiles, setTiles] = useState<PlacedTile[]>([]);
  const paths = useMemo(() => (chosen === null ? null : mapsPaths(world, chosen)), [world, chosen]);

  const draw = useCallback(() => {
    const element = box.current;
    if (element === null) {
      return;
    }
    const now = view.current;
    moved.current?.setAttribute("transform", `translate(${now.x} ${now.y}) scale(${now.scale})`);
    if (zoom.current !== null) {
      zoom.current.textContent = new Intl.NumberFormat(i18n.language, { style: "percent" }).format(now.scale);
    }
    // The screen's own pixels: a HiDPI screen needs a level as many times finer.
    const level = levelFor(world.zooms, now.scale * (globalThis.devicePixelRatio || 1));
    const next = level === undefined ? [] : visibleTiles(level, now, element.clientWidth, element.clientHeight);
    setTiles((shown) =>
      shown.length === next.length && shown.every((tile, index) => tile.file === next[index]?.file) ? shown : next,
    );
  }, [world, i18n.language]);

  const redraw = useCallback(() => {
    cancelAnimationFrame(frame.current);
    frame.current = requestAnimationFrame(draw);
  }, [draw]);

  const recentre = useCallback(() => {
    const width = box.current?.clientWidth ?? 0;
    const height = box.current?.clientHeight ?? 0;
    // A tab behind another has no size: the fit waits until it is shown.
    unfitted.current = width === 0 || height === 0;
    view.current = fitView(world, chosen ?? [], width, height);
    redraw();
  }, [world, chosen, redraw]);

  // A new world or a new choice fits the view to it; a resized box draws what it now shows, and fits it if it could
  // not before.
  useEffect(() => {
    recentre();
    const element = box.current;
    if (element === null || typeof ResizeObserver === "undefined") {
      return;
    }
    const watch = new ResizeObserver(() => (unfitted.current ? recentre() : redraw()));
    watch.observe(element);
    return () => {
      watch.disconnect();
      cancelAnimationFrame(frame.current);
    };
  }, [recentre, redraw]);

  const zoomBy = (factor: number, x?: number, y?: number) => {
    const element = box.current;
    const px = x ?? (element?.clientWidth ?? 0) / 2;
    const py = y ?? (element?.clientHeight ?? 0) / 2;
    view.current = zoomAbout(world, view.current, factor, px, py);
    redraw();
  };

  const point = (event: PointerEvent | { clientX: number; clientY: number }) => {
    const rect = box.current?.getBoundingClientRect();
    return { x: event.clientX - (rect?.left ?? 0), y: event.clientY - (rect?.top ?? 0) };
  };

  const hover = (x: number, y: number) => {
    const label = tip.current;
    if (label === null) {
      return;
    }
    const now = view.current;
    const [mapX, mapY] = mapAt(world, (x - now.x) / now.scale, (y - now.y) / now.scale);
    const name = drag.current === null ? nameAt(mapX, mapY) : null;
    label.hidden = name === null;
    if (name !== null) {
      label.textContent = `${name} · ${mapX}, ${mapY}`;
      label.style.transform = `translate(${x + 14}px, ${y + 14}px)`;
    }
  };

  const keys = (event: KeyboardEvent) => {
    const moves: Record<string, [number, number]> = {
      ArrowLeft: [PAN, 0],
      ArrowRight: [-PAN, 0],
      ArrowUp: [0, PAN],
      ArrowDown: [0, -PAN],
    };
    const move = moves[event.key];
    if (move !== undefined) {
      view.current = { ...view.current, x: view.current.x + move[0], y: view.current.y + move[1] };
      redraw();
    } else if (event.key === "+" || event.key === "=") {
      zoomBy(STEP);
    } else if (event.key === "-") {
      zoomBy(1 / STEP);
    } else if (event.key === "0" || event.code === "Digit0" || event.code === "Numpad0") {
      // By its place too: AZERTY's 0 key gives "à" unshifted.
      recentre();
    } else {
      return;
    }
    event.preventDefault();
  };

  return (
    <div className="relative h-full min-h-0 overflow-hidden bg-[#0d1416]">
      <div
        ref={box}
        role="application"
        aria-label={t("world.mapLabel", { world: named(world.name, i18n.language) ?? world.id })}
        aria-roledescription={t("world.map")}
        // biome-ignore lint/a11y/noNoninteractiveTabindex: the map takes the focus so its keys move it
        tabIndex={0}
        className="focus-visible:outline-focus absolute inset-0 cursor-grab touch-none select-none focus-visible:outline-1 focus-visible:-outline-offset-1 active:cursor-grabbing"
        onKeyDown={keys}
        onWheel={(event) => {
          if (event.deltaY === 0) {
            return;
          }
          // A notch is 100 pixels, or 3 lines: a step; a trackpad's small deltas make small steps.
          const pixels = event.deltaMode === 1 ? event.deltaY * 33 : event.deltaY;
          const at = point(event);
          zoomBy(STEP ** Math.max(-1, Math.min(1, -pixels / 100)), at.x, at.y);
        }}
        onPointerDown={(event) => {
          drag.current = { x: event.clientX - view.current.x, y: event.clientY - view.current.y };
          event.currentTarget.setPointerCapture?.(event.pointerId);
        }}
        onPointerMove={(event) => {
          const start = drag.current;
          if (start !== null) {
            view.current = { ...view.current, x: event.clientX - start.x, y: event.clientY - start.y };
            redraw();
          }
          const at = point(event);
          hover(at.x, at.y);
        }}
        onPointerUp={() => {
          drag.current = null;
        }}
        onPointerCancel={() => {
          drag.current = null;
        }}
        onLostPointerCapture={() => {
          drag.current = null;
        }}
        onPointerLeave={() => {
          if (tip.current !== null) {
            tip.current.hidden = true;
          }
        }}
      >
        <svg className="absolute inset-0 size-full" aria-hidden="true">
          <g ref={moved}>
            {tiles.map((tile) => (
              // A pixel more each way, so the tiles' edges never show a seam once scaled.
              <image
                key={tile.file}
                href={convertFileSrc(tile.file)}
                x={tile.left}
                y={tile.top}
                width={tile.width + 1}
                height={tile.height + 1}
                preserveAspectRatio="none"
              />
            ))}
            {paths !== null && (
              <>
                <path d={paths.cells} fill={TEAL} fillOpacity={0.28} />
                <path
                  d={paths.outline}
                  fill="none"
                  stroke={TEAL}
                  strokeWidth={2}
                  strokeLinecap="square"
                  vectorEffect="non-scaling-stroke"
                />
              </>
            )}
          </g>
        </svg>
      </div>
      <div
        ref={tip}
        hidden
        className="pointer-events-none absolute top-0 left-0 rounded-[5px] border border-[#3c3c3c] bg-[#252526] px-2 py-1 text-xs whitespace-nowrap shadow-[0_4px_14px_rgb(0_0_0/40%)]"
      />
      <span
        ref={zoom}
        className="text-text-muted absolute bottom-3 left-3 rounded bg-[#181818cc] px-1.5 py-0.5 text-[11px] tabular-nums"
      />
      <div className="absolute right-3 bottom-3 flex flex-col overflow-hidden rounded-md border border-[#3c3c3c] bg-[#252526]">
        {(
          [
            ["world.zoomIn", Plus, () => zoomBy(STEP)],
            ["world.zoomOut", Minus, () => zoomBy(1 / STEP)],
            ["world.fit", Maximize, recentre],
          ] as const
        ).map(([label, Glyph, act]) => (
          <button
            key={label}
            type="button"
            aria-label={t(label)}
            title={t(label)}
            onClick={act}
            className="hover:bg-hover focus-visible:outline-focus flex size-7 items-center justify-center border-[#3c3c3c] not-first:border-t focus-visible:outline-1 focus-visible:-outline-offset-1"
          >
            <Glyph aria-hidden="true" size={15} strokeWidth={1.5} />
          </button>
        ))}
      </div>
    </div>
  );
}
