import { useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";

import { named } from "@/characters/names.ts";
import { drawable } from "./map-geometry.ts";
import { WorldMap } from "./world-map.tsx";
import { findSubArea, useWorld } from "./world-store.ts";

/**
 * The World tab (the owner's mock-up 10 of 2026-11-15): the map of the world its editor action shows, the sub-area
 * chosen in the World view on it with its name, area, level and maps on top; one no world shows said "not on the
 * map", one of another world said where it is. The view comes with the World's next PR, the zones' tools with step
 * 3's PR 10, the hints after them.
 */
export function WorldTab() {
  const { t, i18n } = useTranslation();
  const read = useWorld((store) => store.read);
  const load = useWorld((store) => store.load);
  const worldId = useWorld((store) => store.world);
  const picked = useWorld((store) => store.picked);
  const atlas = read?.state === "read" ? read.atlas : null;
  const shown = atlas?.worlds.find((one) => one.id === worldId) ?? atlas?.worlds[0];
  // Memoised so the map refits only when the world changes.
  const world = useMemo(() => (shown === undefined ? undefined : drawable(shown)), [shown]);

  useEffect(() => {
    void load();
  }, [load]);

  // Each map of the world shown, by its coordinates, with its sub-area's name.
  const names = useMemo(() => {
    const byMap = new Map<string, string>();
    for (const area of atlas?.areas ?? []) {
      for (const subArea of area.subAreas) {
        const name = named(subArea.name, i18n.language) ?? "";
        for (const [x, y] of subArea.maps.find((one) => one.world === world?.id)?.at ?? []) {
          byMap.set(`${x},${y}`, name);
        }
      }
    }
    return byMap;
  }, [atlas, world, i18n.language]);

  if (read === null || read.state === "loading") {
    return <p className="text-text-muted flex h-full items-center justify-center text-xs">{t("world.loading")}</p>;
  }
  if (read.state === "failed" || world === undefined) {
    const reason = read.state === "failed" ? read.error.reason : "";
    return (
      <p className="text-text-muted flex h-full items-center justify-center p-6 text-xs">
        {t("world.noData", { reason })}
      </p>
    );
  }
  const subArea = picked === null || atlas === null ? undefined : findSubArea(atlas, picked);
  const here = subArea?.maps.find((one) => one.world === world.id)?.at ?? null;
  const elsewhere = subArea?.maps[0]?.world;
  const where =
    subArea === undefined || here !== null
      ? null
      : elsewhere === undefined
        ? t("world.notOnMap")
        : t("world.onWorld", {
            world: named(atlas?.worlds.find((one) => one.id === elsewhere)?.name, i18n.language) ?? elsewhere,
          });

  return (
    <div className="relative h-full min-h-0">
      <WorldMap world={world} chosen={here} nameAt={(x, y) => names.get(`${x},${y}`) ?? null} />
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
  );
}
