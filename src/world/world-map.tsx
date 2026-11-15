import { convertFileSrc } from "@tauri-apps/api/core";
import { Maximize, Minus, Plus } from "lucide-react";
import { type KeyboardEvent, type PointerEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { flushSync } from "react-dom";
import { useTranslation } from "react-i18next";

import type { AtlasHint_Serialize } from "@/bindings.ts";
import { named } from "@/characters/names.ts";
import { HINT_SIZE, type PlacedHint, placeHints } from "./hint-layout.ts";
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
/** How far a press may move, in screen pixels, and still be a click rather than a drag. */
const CLICK = 4;

interface WorldMapProps {
  world: AtlasWorld;
  /** The maps to show in teal, outlined, and to fit the view to; `null` for none. */
  chosen: readonly (readonly [number, number])[] | null;
  /** The name said on hover of the map at `x, y`, if any. */
  nameAt: (x: number, y: number) => string | null;
  /** The hints to draw, by kind (the atlas's order). */
  hints: readonly AtlasHint_Serialize[];
  /** A click on the map at `x, y`; `true` when it chose something new, which leaves the view where it is. */
  onPick?: (x: number, y: number) => boolean;
  /** The maps to outline, dashed, while the one at `x, y` is hovered, the same array for all of them; none without. */
  outlineAt?: (x: number, y: number) => readonly (readonly [number, number])[] | null;
}

/**
 * The world's map drawn by hand from its tiles (the owner, 2026-11-15): the level the zoom needs, the tiles in view; the
 * chosen maps in teal, outlined; the hints' pictures, those that would overlap thinned out by kind; a hint's name, or a
 * map's sub-area, on hover; the wheel, the arrows, + / − and 0 move, zoom and fit it. The view lives in a ref, reaching
 * the page each frame; React draws only when the tiles in view or the zoom change.
 */
export function WorldMap({ world, chosen, nameAt, hints, onPick, outlineAt }: WorldMapProps) {
  const { t, i18n } = useTranslation();
  const box = useRef<HTMLDivElement>(null);
  const moved = useRef<SVGGElement>(null);
  const tip = useRef<HTMLDivElement>(null);
  const zoom = useRef<HTMLSpanElement>(null);
  const view = useRef<View>({ x: 0, y: 0, scale: world.startScale });
  const frame = useRef(0);
  const drag = useRef<{ x: number; y: number } | null>(null);
  // Where a press of the main button began on the screen, until it moves further than a click would.
  const press = useRef<{ x: number; y: number } | null>(null);
  // A choice made by a click here: the view stays where the player clicked rather than fitting it.
  const stay = useRef(false);
  const unfitted = useRef(false);
  const [tiles, setTiles] = useState<PlacedTile[]>([]);
  // The hints placed for a zoom, which a new zoom or new hints place again.
  const given = useRef(hints);
  const placed = useRef<{ scale: number; list: PlacedHint[] }>({ scale: Number.NaN, list: [] });
  const [pins, setPins] = useState(placed.current);
  const paths = useMemo(() => (chosen === null ? null : mapsPaths(world, chosen)), [world, chosen]);
  // The maps outlined under the pointer: React draws only when the pointer reaches another group of them.
  const [hovered, setHovered] = useState<readonly (readonly [number, number])[] | null>(null);
  const hoveredPaths = useMemo(() => (hovered === null ? null : mapsPaths(world, hovered)), [world, hovered]);

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
    if (placed.current.scale !== now.scale) {
      placed.current = { scale: now.scale, list: placeHints(world, given.current, now.scale) };
      // At once, so the pictures never show for a frame at the zoom before.
      flushSync(() => setPins(placed.current));
    }
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

  useEffect(() => {
    given.current = hints;
    placed.current = { scale: Number.NaN, list: [] };
    redraw();
  }, [hints, redraw]);

  // A new world or a new choice fits the view to it; a resized box draws what it now shows, and fits it if it could
  // not before.
  useEffect(() => {
    if (stay.current) {
      stay.current = false;
    } else {
      recentre();
    }
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
    const px = (x - now.x) / now.scale;
    const py = (y - now.y) / now.scale;
    const [mapX, mapY] = mapAt(world, px, py);
    const group = outlineAt?.(mapX, mapY) ?? null;
    setHovered((shown) => (shown === group ? shown : group));
    const half = HINT_SIZE / now.scale / 2;
    const pin = placed.current.list.find((one) => Math.abs(one.x - px) < half && Math.abs(one.y - py) < half)?.hint;
    const said =
      pin === undefined
        ? [nameAt(mapX, mapY)]
        : [named(pin.name, i18n.language), pin.level > 0 ? t("world.level", { level: pin.level }) : null];
    const name = drag.current === null && said[0] !== null ? said.filter((part) => part !== null).join(" · ") : null;
    label.hidden = name === null;
    if (name !== null) {
      const at = pin?.at ?? [mapX, mapY];
      label.textContent = `${name} · ${at[0]}, ${at[1]}`;
      label.style.transform = `translate(${x + 14}px, ${y + 14}px)`;
    }
  };

  // A drag ends with its button, or when the system takes the pointer.
  const release = () => {
    drag.current = null;
    press.current = null;
  };

  // A press of the main button released without having moved further than a click would is a click on the map.
  const click = (event: PointerEvent) => {
    const start = press.current;
    release();
    if (
      start === null ||
      onPick === undefined ||
      Math.hypot(event.clientX - start.x, event.clientY - start.y) > CLICK
    ) {
      return;
    }
    const at = point(event);
    const now = view.current;
    const [x, y] = mapAt(world, (at.x - now.x) / now.scale, (at.y - now.y) / now.scale);
    stay.current = onPick(x, y);
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
          press.current = event.button === 0 ? { x: event.clientX, y: event.clientY } : null;
          event.currentTarget.setPointerCapture?.(event.pointerId);
        }}
        onPointerMove={(event) => {
          const pressed = press.current;
          if (pressed !== null && Math.hypot(event.clientX - pressed.x, event.clientY - pressed.y) > CLICK) {
            press.current = null;
          }
          const start = drag.current;
          if (start !== null) {
            view.current = { ...view.current, x: event.clientX - start.x, y: event.clientY - start.y };
            redraw();
          }
          const at = point(event);
          hover(at.x, at.y);
        }}
        onPointerUp={click}
        onPointerCancel={release}
        onLostPointerCapture={release}
        onPointerLeave={() => {
          if (tip.current !== null) {
            tip.current.hidden = true;
          }
          setHovered(null);
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
            {hoveredPaths !== null && hovered !== chosen && (
              <>
                <path d={hoveredPaths.cells} fill="#ffffff" fillOpacity={0.08} />
                <path
                  d={hoveredPaths.outline}
                  fill="none"
                  stroke="#ffffff"
                  strokeOpacity={0.85}
                  strokeWidth={1.5}
                  strokeDasharray="4 3"
                  vectorEffect="non-scaling-stroke"
                />
              </>
            )}
            {pins.list.map(({ hint, x, y }) => {
              const side = HINT_SIZE / pins.scale;
              // The one picture the client lacks is a plain dot.
              return hint.picture === null ? (
                <circle
                  key={hint.id}
                  cx={x}
                  cy={y}
                  r={side / 5}
                  fill="#e6e6e6"
                  stroke="#0d1416"
                  strokeWidth={side / 14}
                />
              ) : (
                <image
                  key={hint.id}
                  href={convertFileSrc(hint.picture)}
                  x={x - side / 2}
                  y={y - side / 2}
                  width={side}
                  height={side}
                />
              );
            })}
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
