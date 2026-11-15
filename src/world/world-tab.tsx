import { Crosshair } from "lucide-react";
import { useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";

import { named } from "@/characters/names.ts";
import { Button } from "@/components/ui/button.tsx";
import { Kbd } from "@/components/ui/kbd.tsx";
import { drawable } from "./map-geometry.ts";
import { WorldDetails } from "./world-details.tsx";
import { WorldMap } from "./world-map.tsx";
import { findSubArea, useWorld } from "./world-store.ts";

/**
 * The World tab (the owner's mock-up 10 of 2026-11-15): the map of the world its editor action shows, the sub-area
 * chosen in the World view on it with its name, area, level and maps on top; one no world shows said "not on the
 * map", one of another world said where it is; the hints of the kinds its editor action shows. A click on the map
 * chooses its sub-area as the view does, and Details on the right says what it is as a zone (the owner's mock-up 12,
 * 2026-11-15). While a Zone is chosen on the map, a banner says for whom, a map hovered outlines its sub-area, and a
 * click chooses it and brings back the tab the choice began in; Escape cancels. The zones' tools come with later PRs.
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
  const atlas = read?.state === "read" ? read.atlas : null;
  const shown = atlas?.worlds.find((one) => one.id === worldId) ?? atlas?.worlds[0];
  // Memoised so the map refits only when the world changes.
  const world = useMemo(() => (shown === undefined ? undefined : drawable(shown)), [shown]);

  useEffect(() => {
    void load();
  }, [load]);

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

  // Each map of the world shown, by its coordinates, with its sub-area, that one's name and all its maps there.
  const names = useMemo(() => {
    const byMap = new Map<string, { id: number; name: string; maps: readonly (readonly [number, number])[] }>();
    for (const area of atlas?.areas ?? []) {
      for (const subArea of area.subAreas) {
        const name = named(subArea.name, i18n.language) ?? "";
        const maps = subArea.maps.find((one) => one.world === world?.id)?.at ?? [];
        for (const [x, y] of maps) {
          byMap.set(`${x},${y}`, { id: subArea.id, name, maps });
        }
      }
    }
    return byMap;
  }, [atlas, world, i18n.language]);

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
  // elsewhere, nothing.
  const pickAt = (x: number, y: number) => {
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
    <div className="grid h-full min-h-0 grid-cols-[minmax(0,1fr)_300px]">
      <div className="relative min-h-0">
        <WorldMap
          world={world}
          chosen={here}
          nameAt={(x, y) => names.get(`${x},${y}`)?.name ?? null}
          hints={hints}
          onPick={pickAt}
          outlineAt={choosing === null ? undefined : (x, y) => names.get(`${x},${y}`)?.maps ?? null}
        />
        {choosing !== null && (
          <div
            role="status"
            className="absolute top-3 left-1/2 flex -translate-x-1/2 items-center gap-2.5 rounded-lg border border-[#39c5bb88] bg-[#1b1c1f] py-1.5 pr-2 pl-3 text-xs whitespace-nowrap shadow-[0_6px_20px_rgb(0_0_0/45%)]"
          >
            <Crosshair aria-hidden="true" size={14} strokeWidth={1.5} />
            {t("world.choosing.banner", { who: choosing.who })}
            <Button variant="outline" size="xs" onClick={() => endChoosing(null)}>
              {t("world.choosing.cancel")}
              <Kbd>{t("world.choosing.escape")}</Kbd>
            </Button>
          </div>
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
      <WorldDetails atlas={atlas} subArea={picked} />
    </div>
  );
}
