import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import { commands, type MapCells } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";
import { cn } from "@/lib/utils.ts";
import type { CharacterView } from "./character-store.ts";
import {
  CELL_HEIGHT,
  CELL_WIDTH,
  cellCentre,
  type Floor,
  floors,
  MAP_HEIGHT,
  MAP_WIDTH,
  type MarkKind,
  marks,
} from "./map-marks.ts";

// The owner's style "B" (2026-11-12): discreet diamonds for the ground, a dot per actor until the renderer draws them.
const FLOOR: Record<Floor, string> = { walkable: "#272727", exit: "#1f3247" };
const DOT: Record<MarkKind, string> = {
  me: "#0078d4",
  player: "#a371f7",
  monsters: "#d29922",
  npc: "#39c5bb",
  resource: "#3fb950",
  harvested: "#2d5a36",
  other: "#8a8a8a",
};

function draw(context: CanvasRenderingContext2D, cells: MapCells, view: CharacterView) {
  context.clearRect(0, 0, MAP_WIDTH, MAP_HEIGHT);
  for (const { cell, floor } of floors(cells)) {
    const { x, y } = cellCentre(cell);
    context.beginPath();
    context.moveTo(x, y - CELL_HEIGHT / 2 + 1);
    context.lineTo(x + CELL_WIDTH / 2 - 1.5, y);
    context.lineTo(x, y + CELL_HEIGHT / 2 - 1);
    context.lineTo(x - CELL_WIDTH / 2 + 1.5, y);
    context.closePath();
    context.fillStyle = FLOOR[floor];
    context.fill();
  }
  for (const { cell, kind } of marks(cells, view)) {
    const { x, y } = cellCentre(cell);
    const me = kind === "me";
    context.beginPath();
    context.arc(x, y, me ? 6 : 4.5, 0, 2 * Math.PI);
    context.fillStyle = DOT[kind];
    context.fill();
    context.lineWidth = 2;
    context.strokeStyle = "#1f1f1f";
    context.stroke();
    if (me) {
      context.beginPath();
      context.arc(x, y, 10, 0, 2 * Math.PI);
      context.lineWidth = 1.5;
      context.strokeStyle = "rgb(0 120 212 / 50%)";
      context.stroke();
    }
  }
}

// The legend's dots, the same colours as `DOT`, written out so Tailwind finds them.
const LEGEND = {
  me: "bg-[#0078d4]",
  player: "bg-[#a371f7]",
  monsters: "bg-[#d29922]",
  npc: "bg-[#39c5bb]",
  resource: "bg-[#3fb950]",
} as const;

type Loaded = { mapId: number; cells: MapCells } | { mapId: number; error: string };

/**
 * The character's map in its tab, the owner's style "B": the ground where one walks and the exits as discreet
 * diamonds, from the game data's cells read when the map changes; what stands on it as dots, the character's ringed.
 * In a fight, positions are not read yet, so it says so.
 */
export function CharacterMap({ view }: { view: CharacterView }) {
  const { t } = useTranslation();
  const canvas = useRef<HTMLCanvasElement>(null);
  const box = useRef<HTMLDivElement>(null);
  const [size, setSize] = useState<{ width: number; height: number } | null>(null);
  const mapId = view.place?.at == null ? null : view.place.mapId;
  const [loaded, setLoaded] = useState<Loaded | null>(null);

  useEffect(() => {
    if (mapId === null || !isTauri()) {
      return;
    }
    let live = true;
    commands
      .mapCells(mapId)
      .then((cells) => live && setLoaded({ mapId, cells }))
      .catch((error: unknown) => live && setLoaded({ mapId, error: String(error) }));
    return () => {
      live = false;
    };
  }, [mapId]);

  const shown = loaded !== null && loaded.mapId === mapId ? loaded : null;
  const cells = shown !== null && "cells" in shown ? shown.cells : null;

  let notice: string | null = null;
  if (view.fighting === true) {
    notice = t("character.mapFighting");
  } else if (mapId === null) {
    notice = t("character.noPlace");
  } else if (shown !== null && "error" in shown) {
    notice = t("character.mapUnavailable", { error: shown.error });
  }
  const drawn = notice === null;

  // The map fills its column: its box's size is followed, and the map drawn again to fit whenever it changes.
  useEffect(() => {
    const element = box.current;
    if (!drawn || element === null || typeof ResizeObserver === "undefined") {
      return;
    }
    const observer = new ResizeObserver(([entry]) => {
      if (entry !== undefined) {
        setSize({ width: entry.contentRect.width, height: entry.contentRect.height });
      }
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [drawn]);

  useEffect(() => {
    const element = canvas.current;
    const context = element?.getContext("2d");
    if (element == null || context == null || size === null) {
      return;
    }
    // At the screen's pixel density so the diamonds stay sharp, at the largest scale that fits, centred.
    const ratio = window.devicePixelRatio || 1;
    element.width = Math.round(size.width * ratio);
    element.height = Math.round(size.height * ratio);
    const scale = Math.min(size.width / MAP_WIDTH, size.height / MAP_HEIGHT);
    const left = (size.width - MAP_WIDTH * scale) / 2;
    const top = (size.height - MAP_HEIGHT * scale) / 2;
    context.setTransform(1, 0, 0, 1, 0, 0);
    context.clearRect(0, 0, element.width, element.height);
    context.setTransform(ratio * scale, 0, 0, ratio * scale, left * ratio, top * ratio);
    // While another map's cells are read the canvas stays clear: the last map is not left under the new one's actors.
    if (cells !== null) {
      draw(context, cells, view);
    }
  }, [cells, view, size]);

  return (
    <section
      aria-label={t("character.map")}
      className="border-line flex min-h-0 flex-col justify-center gap-3 rounded-[6px] border bg-[#191919] p-3"
    >
      {drawn ? (
        <>
          <div ref={box} className="relative min-h-0 w-full flex-1">
            <canvas ref={canvas} role="img" aria-label={t("character.map")} className="absolute inset-0 size-full" />
          </div>
          <p className="text-text-muted flex flex-wrap justify-center gap-x-3 gap-y-1 text-xs">
            {(Object.keys(LEGEND) as (keyof typeof LEGEND)[]).map((kind) => (
              <span key={kind} className="flex items-center gap-1.5">
                <span aria-hidden="true" className={cn("size-2 rounded-full", LEGEND[kind])} />
                {t(`character.legend.${kind}`)}
              </span>
            ))}
          </p>
        </>
      ) : (
        <p className="text-text-faint text-center text-xs">{notice}</p>
      )}
    </section>
  );
}
