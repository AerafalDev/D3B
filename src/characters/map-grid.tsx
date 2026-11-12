import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import { commands, type MapCells } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";
import type { CharacterView } from "./character-store.ts";

/** A cell's width and height on the canvas: the client's diamonds are twice as wide as high. */
const WIDTH = 40;
const HEIGHT = 20;
const COLUMNS = 14;
const ROWS = 40;

/** Where cell `cell`'s centre is drawn: 14 columns of 40 staggered rows, the odd rows half a cell to the right. */
export function cellCentre(cell: number): { x: number; y: number } {
  const row = Math.floor(cell / COLUMNS);
  const column = cell % COLUMNS;
  return { x: column * WIDTH + (row % 2) * (WIDTH / 2) + WIDTH / 2, y: row * (HEIGHT / 2) + HEIGHT / 2 };
}

const COLOUR = {
  walkable: "#3a4252",
  obstacle: "#6b7280",
  blind: "#7c5c3c",
  exit: "#2f5f94",
  me: "#7cb4ff",
  player: "#c4c4cc",
  monsters: "#e08a3c",
  npc: "#9b7be0",
  resource: "#3f8f5a",
  harvested: "#8fb59a",
  other: "#9ca3af",
} as const;

function diamond(context: CanvasRenderingContext2D, cell: number, fill: string) {
  const { x, y } = cellCentre(cell);
  context.beginPath();
  context.moveTo(x, y - HEIGHT / 2 + 1);
  context.lineTo(x + WIDTH / 2 - 1, y);
  context.lineTo(x, y + HEIGHT / 2 - 1);
  context.lineTo(x - WIDTH / 2 + 1, y);
  context.closePath();
  context.fillStyle = fill;
  context.fill();
}

function marker(context: CanvasRenderingContext2D, cell: number, fill: string, letter: string, ring = false) {
  const { x, y } = cellCentre(cell);
  context.beginPath();
  context.arc(x, y, 7, 0, 2 * Math.PI);
  context.fillStyle = fill;
  context.fill();
  if (ring) {
    context.lineWidth = 2;
    context.strokeStyle = "#e6e6ea";
    context.stroke();
  }
  context.fillStyle = "#0c0c0f";
  context.font = "bold 9px sans-serif";
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText(letter, x, y + 0.5);
}

/** Draws `cells` and, over them, what `view` holds: resources, actors by kind and the character. */
function draw(
  context: CanvasRenderingContext2D,
  cells: MapCells,
  view: CharacterView,
  letters: Record<string, string>,
) {
  context.clearRect(0, 0, context.canvas.width, context.canvas.height);
  const exits = new Set(cells.exits);
  cells.flags.forEach((flags, cell) => {
    const walkable = (flags & 1) !== 0;
    const sight = (flags & 2) !== 0;
    diamond(
      context,
      cell,
      exits.has(cell) ? COLOUR.exit : walkable ? COLOUR.walkable : sight ? COLOUR.obstacle : COLOUR.blind,
    );
  });
  const elements = new Map(cells.elements.map((element) => [element.id, element.cell]));
  for (const element of view.stated) {
    const cell = element.cell ?? elements.get(element.elementId) ?? -1;
    if (cell >= 0) {
      marker(context, cell, element.state === 0 ? COLOUR.resource : COLOUR.harvested, letters["resource"] ?? "R");
    }
  }
  for (const actor of view.actors) {
    if (actor.cell == null) {
      continue;
    }
    const [fill, letter] =
      actor.kind.kind === "player"
        ? [COLOUR.player, letters["player"]]
        : actor.kind.kind === "monsterGroup"
          ? [COLOUR.monsters, letters["monsters"]]
          : actor.kind.kind === "npc"
            ? [COLOUR.npc, letters["npc"]]
            : [COLOUR.other, letters["other"]];
    marker(context, actor.cell, fill, letter ?? "?");
  }
  if (view.cell != null) {
    marker(context, view.cell, COLOUR.me, letters["me"] ?? "V", true);
  }
}

/**
 * The character's map drawn as its 560 diamonds from the game data (`docs/game.md`, "In the app"), its cells read
 * when the map changes; only for a map the data places, of the connection's build.
 */
export function MapGrid({ view }: { view: CharacterView }) {
  const { t } = useTranslation();
  const canvas = useRef<HTMLCanvasElement>(null);
  const mapId = view.place?.at ? view.place.mapId : null;
  const [cells, setCells] = useState<{ mapId: number; cells: MapCells } | { mapId: number; error: string } | null>(
    null,
  );

  useEffect(() => {
    if (mapId === null || !isTauri()) {
      return;
    }
    let live = true;
    commands
      .mapCells(mapId)
      .then((read) => live && setCells({ mapId, cells: read }))
      .catch((error: unknown) => live && setCells({ mapId, error: String(error) }));
    return () => {
      live = false;
    };
  }, [mapId]);

  const shown = cells !== null && cells.mapId === mapId && "cells" in cells ? cells.cells : null;
  useEffect(() => {
    const context = canvas.current?.getContext("2d");
    if (context && !shown) {
      // Another map's cells are being read: the last map is not left drawn under the new one's actors.
      context.clearRect(0, 0, context.canvas.width, context.canvas.height);
    }
    if (context && shown) {
      draw(context, shown, view, {
        me: t("map.letterMe"),
        player: t("map.letterPlayer"),
        monsters: t("map.letterMonsters"),
        npc: t("map.letterNpc"),
        resource: t("map.letterResource"),
        other: t("map.letterOther"),
      });
    }
  }, [shown, view, t]);

  if (mapId === null || view.fighting) {
    return null;
  }
  return (
    <section className="flex flex-col gap-2 rounded-lg border p-3 text-sm">
      {cells !== null && cells.mapId === mapId && "error" in cells ? (
        <p className="text-muted-foreground">{t("map.unavailable", { error: cells.error })}</p>
      ) : (
        <canvas
          ref={canvas}
          width={COLUMNS * WIDTH + WIDTH / 2}
          height={ROWS * (HEIGHT / 2) + HEIGHT / 2}
          role="img"
          aria-label={t("map.label")}
        />
      )}
      <p className="text-muted-foreground text-xs">{t("map.legend")}</p>
    </section>
  );
}
