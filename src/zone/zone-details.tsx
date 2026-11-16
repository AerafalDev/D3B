import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import type { Atlas_Serialize } from "@/bindings.ts";
import { named } from "@/characters/names.ts";
import { usePlayed } from "@/characters/played/played-store.ts";
import { Button } from "@/components/ui/button.tsx";
import { useLibrary } from "@/library/library-store.ts";
import { zoneOf } from "@/settings/zone-of.ts";
import { useWorld } from "@/world/world-store.ts";
import { ResourcesByJob } from "./resources-by-job.tsx";
import { type MyZone, useZones } from "./zones-store.ts";

interface ZoneDetailsProps {
  atlas: Atlas_Serialize;
  zone: MyZone;
}

/**
 * The World tab's Details for a zone of mine (the owner's choices of 2026-11-16): its name, edited in place, "Zone of
 * mine" and its world, its maps, the sub-areas it touches with how many of their maps, whose Zone it is, the resources
 * of those sub-areas together by profession, and Delete, to the system's trash.
 */
export function ZoneDetails({ atlas, zone }: ZoneDetailsProps) {
  const { t, i18n } = useTranslation();
  const played = usePlayed((store) => store.played);
  const blueprints = useLibrary((store) => store.blueprints);
  const whose = useMemo(() => zoneOf({ zone: zone.id }, played, blueprints), [zone.id, played, blueprints]);
  // The sub-areas holding its maps on its world, by name, each with how many of its maps are the zone's.
  const touched = useMemo(() => {
    const held = new Set(zone.maps.map(([x, y]) => `${x},${y}`));
    const found: { id: number; name: string; inZone: number; all: number; resources: number[] }[] = [];
    for (const area of atlas.areas) {
      for (const subArea of area.subAreas) {
        const at = subArea.maps.find((one) => one.world === zone.world)?.at ?? [];
        const inZone = at.filter(([x, y]) => held.has(`${x},${y}`)).length;
        if (inZone > 0) {
          const name = named(subArea.name, i18n.language) ?? String(subArea.id);
          found.push({ id: subArea.id, name, inZone, all: at.length, resources: subArea.resources });
        }
      }
    }
    return found.sort((a, b) => b.inZone - a.inZone || a.name.localeCompare(b.name, i18n.language));
  }, [atlas, zone, i18n.language]);
  const resources = useMemo(() => [...new Set(touched.flatMap((one) => one.resources))], [touched]);
  const world = named(atlas.worlds.find((one) => one.id === zone.world)?.name, i18n.language) ?? String(zone.world);

  const remove = async () => {
    if ((await useZones.getState().remove(zone.id)) && useWorld.getState().zone === zone.id) {
      useWorld.setState({ zone: null });
    }
  };

  return (
    <aside
      aria-label={t("world.details.title")}
      className="border-line flex min-h-0 flex-col gap-3 overflow-y-auto border-l bg-[#181818] px-3 py-3 text-xs"
    >
      <h3 className="text-text-muted text-[11px] font-semibold tracking-[.04em] uppercase">
        {t("world.details.title")}
      </h3>
      <div className="flex flex-col gap-1">
        <ZoneName zone={zone} />
        <p className="text-text-muted">{t("zone.details.of", { world })}</p>
      </div>
      <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
        <dt className="text-text-muted">{t("world.details.maps")}</dt>
        <dd>{zone.maps.length}</dd>
        <dt className="text-text-muted">{t("zone.details.subAreas")}</dt>
        <dd>
          {touched.length === 0 ? (
            t("zone.details.empty")
          ) : (
            <ul className="flex flex-col gap-0.5">
              {touched.map((one) => (
                <li key={one.id} className="flex gap-2">
                  <span className="min-w-0 flex-1 truncate">{one.name}</span>
                  <span className="text-text-faint tabular-nums">
                    {t("zone.details.share", { inZone: one.inZone, all: one.all })}
                  </span>
                </li>
              ))}
            </ul>
          )}
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
      <ResourcesByJob atlas={atlas} resources={resources} />
      <Button variant="outline" size="xs" className="self-start" onClick={() => void remove()}>
        {t("zone.delete")}
      </Button>
    </aside>
  );
}

/** A zone's name, saved when Enter or leaving the field ends it, a blank one put back; Escape puts it back too. */
function ZoneName({ zone }: { zone: MyZone }) {
  const { t } = useTranslation();
  const [name, setName] = useState(zone.name);
  useEffect(() => setName(zone.name), [zone.name]);
  return (
    <input
      aria-label={t("zone.name")}
      value={name}
      onChange={(event) => setName(event.target.value)}
      onBlur={() => {
        if (name.trim() === "") {
          setName(zone.name);
        } else if (name !== zone.name) {
          // A name refused is put back.
          void useZones
            .getState()
            .rename(zone.id, name)
            .then((named) => named || setName(zone.name));
        }
      }}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          event.currentTarget.blur();
        } else if (event.key === "Escape") {
          // Not the World tab's, which would end a choice on the map.
          event.preventDefault();
          setName(zone.name);
        }
      }}
      className="focus:border-focus h-7 rounded border border-[#3c3c3c] bg-[#181818] px-2 text-sm text-white outline-0"
    />
  );
}
