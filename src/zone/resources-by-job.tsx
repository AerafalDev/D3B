import { convertFileSrc } from "@tauri-apps/api/core";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import type { Atlas_Serialize, AtlasResource_Serialize as AtlasResource } from "@/bindings.ts";
import { named } from "@/characters/names.ts";

interface ResourcesByJobProps {
  atlas: Atlas_Serialize;
  /** The resources' ids, those the atlas lacks left out. */
  resources: readonly number[];
}

/**
 * A zone's resources by profession, as Details shows them (the owner's mock-up 12, 2026-11-15): each profession with its
 * icon, then its resources with their icons and levels, in the atlas's order, by profession then level.
 */
export function ResourcesByJob({ atlas, resources }: ResourcesByJobProps) {
  const { t, i18n } = useTranslation();
  // The resources in runs of one profession, as the atlas orders them.
  const byJob = useMemo(() => {
    const wanted = new Set(resources);
    const runs: { job: number; resources: AtlasResource[] }[] = [];
    for (const resource of atlas.resources) {
      if (!wanted.has(resource.id)) {
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
  }, [atlas, resources]);
  const jobs = useMemo(() => new Map(atlas.jobs.map((one) => [one.id, one])), [atlas]);

  if (byJob.length === 0) {
    return <p className="text-text-muted">{t("world.details.noResource")}</p>;
  }
  return byJob.map(({ job, resources: held }) => {
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
          {held.map((resource) => (
            <li key={resource.id} className="flex h-6 items-center gap-1 rounded bg-[#262626] pr-1.5 pl-0.5">
              {resource.icon !== null && <img src={convertFileSrc(resource.icon)} alt="" className="size-5" />}
              {named(resource.name, i18n.language)}
              <span className="text-text-faint text-[11px]">{resource.level}</span>
            </li>
          ))}
        </ul>
      </div>
    );
  });
}
