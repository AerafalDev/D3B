import { Crosshair } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { commands } from "@/bindings.ts";
import { named } from "@/characters/names.ts";
import { Button } from "@/components/ui/button.tsx";
import { Kbd } from "@/components/ui/kbd.tsx";
import { ZoneDetails } from "@/zone/zone-details.tsx";
import { ZoneTools } from "@/zone/zone-tools.tsx";
import { useZoneUndo } from "@/zone/zone-undo.ts";
import { type MyZone, useZones } from "@/zone/zones-store.ts";
import { drawable } from "./map-geometry.ts";
import { WorldDetails } from "./world-details.tsx";
import { type Painting, WorldMap } from "./world-map.tsx";
import { findSubArea, useWorld } from "./world-store.ts";

/**
 * The World tab (the owner's mock-up 10 of 2026-11-15): the map of the world its editor action shows, the sub-area
 * chosen in the World view on it with its name, area, level and maps on top; one no world shows said "not on the
 * map", one of another world said where it is; the hints of the kinds its editor action shows. A click on the map
 * chooses its sub-area as the view does, and Details on the right says what it is as a zone (the owner's mock-up 12,
 * 2026-11-15). While a Zone is chosen on the map, a banner says for whom, a map hovered outlines its sub-area, and a
 * click chooses it and brings back the tab the choice began in; Escape cancels. A zone of mine open shows in amber
 * with its tools (the owner's choices of 2026-11-16): the brush and the eraser paint it map by map, the whole sub-area
 * tool adds a sub-area, or takes it away when the zone holds it all, and Select chooses a sub-area as without a zone.
 * Ctrl+Z and Ctrl+Y undo and redo my zones' changes (the owner, 2026-11-17).
 */
export function WorldTab() {
  const { t, i18n } = useTranslation();
  const read = useWorld((store) => store.read);
  const load = useWorld((store) => store.load);
  const worldId = useWorld((store) => store.world);
  const picked = useWorld((store) => store.picked);
  const pick = useWorld((store) => store.pick);
  const choosing = useWorld((store) => store.choosing);
  const endChoosing = useWorld((store) => store.endChoosing);
  const hidden = useWorld((store) => store.hidden);
  const zoneId = useWorld((store) => store.zone);
  const tool = useWorld((store) => store.tool);
  const panning = useWorld((store) => store.panning);
  const zones = useZones((store) => store.zones);
  const refreshZones = useZones((store) => store.refresh);
  const paintZone = useZones((store) => store.paint);
  const undoBox = useZoneUndo<HTMLDivElement>();
  const atlas = read?.state === "read" ? read.atlas : null;
  const shown = atlas?.worlds.find((one) => one.id === worldId) ?? atlas?.worlds[0];
  // Memoised so the map refits only when the world changes.
  const world = useMemo(() => (shown === undefined ? undefined : drawable(shown)), [shown]);

  useEffect(() => {
    void load();
    void refreshZones();
  }, [load, refreshZones]);

  // Escape cancels a choice on the map, wherever the focus is.
  useEffect(() => {
    if (choosing === null) {
      return;
    }
    const cancel = (event: KeyboardEvent) => {
      // Not an Escape a menu or a dialog already took to close itself.
      if (event.key === "Escape" && !event.defaultPrevented) {
        endChoosing(null);
      }
    };
    window.addEventListener("keydown", cancel);
    return () => window.removeEventListener("keydown", cancel);
  }, [choosing, endChoosing]);

  // Each map of the world shown, by its coordinates, with its sub-area, that one's name and all its maps there; on a
  // map two share, the one with the client's priority there.
  const names = useMemo(() => {
    const byMap = new Map<string, { id: number; name: string; maps: readonly (readonly [number, number])[] }>();
    const first = new Set<string>();
    for (const area of atlas?.areas ?? []) {
      for (const subArea of area.subAreas) {
        const name = named(subArea.name, i18n.language) ?? "";
        const here = subArea.maps.find((one) => one.world === world?.id);
        const maps = here?.at ?? [];
        const priority = new Set(here?.priority.map(([x, y]) => `${x},${y}`));
        for (const [x, y] of maps) {
          const key = `${x},${y}`;
          if (priority.has(key) || !first.has(key)) {
            byMap.set(key, { id: subArea.id, name, maps });
          }
          if (priority.has(key)) {
            first.add(key);
          }
        }
      }
    }
    return byMap;
  }, [atlas, world, i18n.language]);

  // The zone of mine open, when it is on the world shown.
  const open = zones.find((one) => one.id === zoneId && one.world === world?.id);
  // The maps a zone of mine had when it was opened, which the view fits; painting it never moves the view.
  const [frame, setFrame] = useState<MyZone["maps"] | null>(null);
  const shownId = world?.id;
  useEffect(() => {
    const opened = useZones.getState().zones.find((one) => one.id === zoneId && one.world === shownId);
    setFrame(opened?.maps ?? null);
  }, [zoneId, shownId]);
  const painting = useMemo<Painting | undefined>(
    () =>
      open === undefined || (tool !== "brush" && tool !== "eraser")
        ? undefined
        : {
            erase: tool === "eraser",
            paints: (x, y) => names.has(`${x},${y}`),
            onStroke: (maps) => void paintZone(open.id, maps, tool === "eraser"),
          },
    [open, tool, names, paintZone],
  );
  // One array per map hovered, so the map draws its outline again only when the pointer reaches another.
  const cells = useMemo(() => new Map<string, readonly (readonly [number, number])[]>(), []);
  const cellAt = (x: number, y: number) => {
    const key = `${x},${y}`;
    if (!names.has(key)) {
      return null;
    }
    const held = cells.get(key) ?? [[x, y] as const];
    cells.set(key, held);
    return held;
  };

  const hints = useMemo(() => world?.hints.filter((hint) => !hidden.has(hint.kind)) ?? [], [world, hidden]);

  if (read === null || read.state === "loading") {
    return <p className="text-text-muted flex h-full items-center justify-center text-xs">{t("world.loading")}</p>;
  }
  if (read.state === "failed" || world === undefined || atlas === null) {
    return (
      <p className="text-text-muted flex h-full items-center justify-center p-6 text-xs">
        {read.state === "failed" ? t("world.noData", { reason: read.error.reason }) : t("world.noWorld")}
      </p>
    );
  }
  const subArea = picked === null ? undefined : findSubArea(atlas, picked);
  const here = subArea?.maps.find((one) => one.world === world.id)?.at ?? null;
  const elsewhere = subArea?.maps[0]?.world;
  const where =
    subArea === undefined || here !== null
      ? null
      : elsewhere === undefined
        ? t("world.notOnMap")
        : t("world.onWorld", {
            world: named(atlas.worlds.find((one) => one.id === elsewhere)?.name, i18n.language) ?? elsewhere,
          });

  // A click on a map of a sub-area not chosen yet chooses it; while choosing a Zone, it takes it, chosen or not;
  // with the whole sub-area tool, it is added to the zone open, or taken away when the zone holds it all; elsewhere,
  // nothing.
  const pickAt = (x: number, y: number) => {
    if (open !== undefined && tool === "subArea" && choosing === null) {
      const maps = names.get(`${x},${y}`)?.maps ?? [];
      const held = new Set(open.maps.map(([mapX, mapY]) => `${mapX},${mapY}`));
      void paintZone(
        open.id,
        maps,
        maps.every(([mapX, mapY]) => held.has(`${mapX},${mapY}`)),
      );
      // Nothing chosen: the view stays, and fits again for the next choice.
      return false;
    }
    if (choosing?.kind === "map" && world !== undefined) {
      // The map the world map draws there: the one with the priority, else the first (Rust's `map_at`).
      void commands.mapAt(world.id, x, y).then(
        (map) => map !== null && endChoosing(map.id),
        () => endChoosing(null),
      );
      return true;
    }
    const under = names.get(`${x},${y}`)?.id;
    if (under === undefined || (choosing === null && under === picked)) {
      return false;
    }
    if (choosing === null) {
      pick(under);
    } else {
      endChoosing(under);
    }
    return true;
  };

  return (
    // Focusable, so that a click anywhere in the tab takes its shortcuts there.
    <div ref={undoBox} tabIndex={-1} className="grid h-full min-h-0 grid-cols-[minmax(0,1fr)_300px] outline-none">
      <div className="relative min-h-0">
        <WorldMap
          world={world}
          chosen={here}
          nameAt={(x, y) => names.get(`${x},${y}`)?.name ?? null}
          hints={hints}
          onPick={pickAt}
          outlineAt={
            choosing?.kind === "map"
              ? (x, y) => [[x, y]]
              : choosing !== null || (open !== undefined && tool === "subArea")
                ? (x, y) => names.get(`${x},${y}`)?.maps ?? null
                : painting === undefined
                  ? undefined
                  : cellAt
          }
          fitTo={frame}
          mine={open?.maps ?? null}
          paint={choosing === null ? painting : undefined}
          panning={panning}
        />
        {open !== undefined && choosing === null && <ZoneTools />}
        {choosing !== null && (
          <div
            role="status"
            className="absolute top-3 left-1/2 flex -translate-x-1/2 items-center gap-2.5 rounded-lg border border-[#39c5bb88] bg-[#1b1c1f] py-1.5 pr-2 pl-3 text-xs whitespace-nowrap shadow-[0_6px_20px_rgb(0_0_0/45%)]"
          >
            <Crosshair aria-hidden="true" size={14} strokeWidth={1.5} />
            {t(choosing.kind === "map" ? "world.choosing.bannerMap" : "world.choosing.banner", { who: choosing.who })}
            <Button variant="outline" size="xs" onClick={() => endChoosing(null)}>
              {t("world.choosing.cancel")}
              <Kbd>{t("world.choosing.escape")}</Kbd>
            </Button>
          </div>
        )}
        {open !== undefined && (
          <section
            aria-live="polite"
            className="pointer-events-none absolute top-3 left-3 flex max-w-[min(280px,calc(50%-4.5rem))] flex-col gap-0.5 rounded-md border border-[#2b2b2b] bg-[#181818d9] px-2.5 py-1.5 text-xs"
          >
            <h2 className="truncate font-medium text-white">{open.name}</h2>
            <p className="text-text-muted text-[11px]">
              {[t("zone.mineOne"), t("world.maps", { count: open.maps.length })].join(" · ")}
            </p>
          </section>
        )}
        {subArea !== undefined && (
          <section
            aria-live="polite"
            className="pointer-events-none absolute top-3 left-3 flex max-w-[280px] flex-col gap-0.5 rounded-md border border-[#2b2b2b] bg-[#181818d9] px-2.5 py-1.5 text-xs"
          >
            <h2 className="font-medium text-white">{named(subArea.name, i18n.language)}</h2>
            <p className="text-text-muted text-[11px]">
              {[
                named(subArea.area.name, i18n.language),
                t("world.level", { level: subArea.level }),
                here === null ? where : t("world.maps", { count: here.length }),
              ].join(" · ")}
            </p>
          </section>
        )}
      </div>
      {open === undefined ? <WorldDetails atlas={atlas} subArea={picked} /> : <ZoneDetails atlas={atlas} zone={open} />}
    </div>
  );
}
