import { convertFileSrc } from "@tauri-apps/api/core";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import type { Atlas_Serialize, AtlasResource_Serialize as AtlasResource } from "@/bindings.ts";
import { named } from "@/characters/names.ts";
import { usePlayed } from "@/characters/played/played-store.ts";
import { useLibrary } from "@/library/library-store.ts";
import { zoneOf } from "@/settings/zone-of.ts";
import { findSubArea } from "./world-store.ts";

interface WorldDetailsProps {
  atlas: Atlas_Serialize;
  /** The sub-area chosen, `null` for none. */
  subArea: number | null;
}

/**
 * The World tab's Details, a column on its right (the owner's mock-up 12, 2026-11-15): the sub-area chosen as a
 * ready-made zone, its area, level, maps and monsters, whose Zone it is, and its resources by profession with their
 * icons and levels.
 */
export function WorldDetails({ atlas, subArea }: WorldDetailsProps) {
  const { t, i18n } = useTranslation();
  const chosen = subArea === null ? undefined : findSubArea(atlas, subArea);
  const played = usePlayed((store) => store.played);
  const blueprints = useLibrary((store) => store.blueprints);
  const whose = useMemo(
    () => (subArea === null ? [] : zoneOf(subArea, played, blueprints)),
    [subArea, played, blueprints],
  );
  // The sub-area's resources in runs of one profession, as the atlas orders them.
  const byJob = useMemo(() => {
    const resources = new Map(atlas.resources.map((one) => [one.id, one]));
    const runs: { job: number; resources: AtlasResource[] }[] = [];
    for (const id of chosen?.resources ?? []) {
      const resource = resources.get(id);
      if (resource === undefined) {
        continue;
      }
      const last = runs.at(-1);
      if (last?.job === resource.job) {
        last.resources.push(resource);
      } else {
        runs.push({ job: resource.job, resources: [resource] });
      }
    }
    return runs;
  }, [atlas, chosen]);

  const jobs = useMemo(() => new Map(atlas.jobs.map((one) => [one.id, one])), [atlas]);
  const maps = chosen?.maps.reduce((count, one) => count + one.at.length, 0) ?? 0;

  return (
    <aside
      aria-label={t("world.details.title")}
      className="border-line flex min-h-0 flex-col gap-3 overflow-y-auto border-l bg-[#181818] px-3 py-3 text-xs"
    >
      <h3 className="text-text-muted text-[11px] font-semibold tracking-[.04em] uppercase">
        {t("world.details.title")}
      </h3>
      {chosen === undefined ? (
        <p className="text-text-muted">{t("world.details.none")}</p>
      ) : (
        <>
          <div>
            <p className="text-sm text-white">{named(chosen.name, i18n.language)}</p>
            <p className="text-text-muted">
              {t("world.details.subArea", { area: named(chosen.area.name, i18n.language) })}
            </p>
          </div>
          <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
            <dt className="text-text-muted">{t("world.details.level")}</dt>
            <dd>{chosen.level}</dd>
            <dt className="text-text-muted">{t("world.details.maps")}</dt>
            <dd>{maps === 0 ? t("world.notOnMap") : maps}</dd>
            <dt className="text-text-muted">{t("world.details.monsters")}</dt>
            <dd>
              {chosen.monsters === 0
                ? t("world.details.noMonster")
                : t("world.details.kinds", { count: chosen.monsters })}
            </dd>
            {whose.length > 0 && (
              <>
                <dt className="text-text-muted">{t("world.details.zoneOf")}</dt>
                <dd>{new Intl.ListFormat(i18n.language).format(whose)}</dd>
              </>
            )}
          </dl>
          <h4 className="text-text-muted text-[11px] font-semibold tracking-[.04em] uppercase">
            {t("world.details.resources")}
          </h4>
          {byJob.length === 0 && <p className="text-text-muted">{t("world.details.noResource")}</p>}
          {byJob.map(({ job, resources }) => {
            const profession = jobs.get(job);
            const name = named(profession?.name, i18n.language) ?? String(job);
            return (
              <div key={job} className="flex flex-col gap-1">
                <p className="text-text-muted flex items-center gap-1.5">
                  {profession !== undefined && profession.icon !== null && (
                    <img src={convertFileSrc(profession.icon)} alt="" className="size-[18px]" />
                  )}
                  {name}
                </p>
                <ul aria-label={name} className="flex flex-wrap gap-1">
                  {resources.map((resource) => (
                    <li key={resource.id} className="flex h-6 items-center gap-1 rounded bg-[#262626] pr-1.5 pl-0.5">
                      {resource.icon !== null && <img src={convertFileSrc(resource.icon)} alt="" className="size-5" />}
                      {named(resource.name, i18n.language)}
                      <span className="text-text-faint text-[11px]">{resource.level}</span>
                    </li>
                  ))}
                </ul>
              </div>
            );
          })}
        </>
      )}
    </aside>
  );
}
