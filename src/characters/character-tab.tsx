import type { IDockviewPanelProps } from "dockview-react";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";

import { useRelay } from "@/relay/relay-store.ts";
import type { EditorParams } from "@/shell/parts/editor-store.ts";
import { Avatar } from "./avatar.tsx";
import { CharacterMap } from "./character-map.tsx";
import { characterState } from "./character-state.ts";
import { type CharacterView, useCharacters } from "./character-store.ts";
import { Gauge } from "./gauge.tsx";
import { named } from "./names.ts";

interface CharacterParams extends EditorParams {
  connection: number;
}

/** What is around: players, monster groups, NPCs, resources, as many as the view knows. */
function around(view: CharacterView) {
  const count = (kind: string) => view.actors.filter((actor) => actor.kind.kind === kind).length;
  return [
    ["players", count("player")],
    ["monsterGroups", count("monsterGroup")],
    ["npcs", count("npc")],
    ["resources", view.stated.length],
  ] as const;
}

/**
 * A character's tab, the owner's layout "A" (2026-11-12): its state in a column on the left (who and where, its
 * gauges, its points and kamas, what is around), its map on the right. The tab takes the character's name once known.
 */
export function CharacterTab({ api, params }: IDockviewPanelProps<CharacterParams>) {
  const { t, i18n } = useTranslation();
  const view = useCharacters((store) => store.views[params.connection]);
  const protocol = useRelay(
    (store) => store.status?.connections.find((one) => one.number === params.connection)?.protocol,
  );
  const name = view?.name ?? null;

  useEffect(() => {
    if (name !== null) {
      api.setTitle(name);
    }
  }, [api, name]);

  if (protocol === undefined) {
    return <Notice text={t("character.closed")} />;
  }
  if (protocol === "unmapped" || protocol === "unknownBuild") {
    return <Notice text={t(`character.off.${protocol}`)} />;
  }
  if (view === undefined || name === null) {
    return <Notice text={t("character.waiting")} />;
  }
  // A lost protocol keeps the view as it was, marked lost.
  const state = characterState(protocol, view);

  const numbers = new Intl.NumberFormat(i18n.language);
  const place = view.place;
  const where = [named(place?.area, i18n.language), named(place?.subArea, i18n.language)].filter(Boolean).join(" › ");
  const breed = named(view.breedName, i18n.language);
  const level = view.level ?? "?";
  const stale = Object.values(view.stale).some(Boolean);

  return (
    <div className="grid h-full min-h-0 grid-cols-[260px_1fr] gap-4 overflow-hidden p-4">
      <section aria-label={t("character.state")} className="flex min-h-0 flex-col gap-4 overflow-y-auto pr-1">
        <header className="flex items-center gap-3">
          <Avatar name={name} head={view.head} />
          <div className="min-w-0">
            <h2 className="truncate text-[15px] font-medium text-white">{name}</h2>
            <p className="text-text-muted text-xs">
              {breed === null ? t("character.level", { level }) : t("character.levelAndBreed", { level, breed })} ·{" "}
              {t(`characters.state.${state}`)}
            </p>
          </div>
        </header>
        <p className="text-text-muted text-xs">
          {place?.at == null
            ? t("character.noPlace")
            : t("character.place", { where, x: place.at.x, y: place.at.y, cell: view.cell ?? "?" })}
        </p>
        <div className="flex flex-col gap-2.5">
          <Gauge
            label={t("character.life")}
            gauge={view.life}
            tone="life"
            note={view.lifeEstimated ? t("character.regenerating") : undefined}
          />
          <Gauge label={t("character.pods")} gauge={view.pods} tone="pods" />
          <Gauge label={t("character.experience")} gauge={view.experience} tone="experience" />
          <Gauge label={t("character.energy")} gauge={view.energy} tone="energy" />
        </div>
        <dl className="grid grid-cols-[1fr_auto] gap-y-1 text-xs">
          <dt className="text-text-muted">{t("character.points")}</dt>
          <dd className="tabular-nums">
            {t("character.pointsValue", { ap: view.actionPoints ?? "—", mp: view.movementPoints ?? "—" })}
          </dd>
          <dt className="text-text-muted">{t("character.kamas")}</dt>
          <dd className="tabular-nums">{view.kamas === null ? "—" : numbers.format(view.kamas)}</dd>
        </dl>
        <section aria-label={t("character.around")} className="flex flex-col gap-1 text-xs">
          <h3 className="text-text-muted">{t("character.around")}</h3>
          <dl className="grid grid-cols-[1fr_auto] gap-y-1">
            {around(view).map(([kind, count]) => (
              <div key={kind} className="contents">
                <dt className="text-text-muted">{t(`character.${kind}`)}</dt>
                <dd className="tabular-nums">{count}</dd>
              </div>
            ))}
          </dl>
        </section>
        {stale && <p className="text-text-faint text-xs">{t("character.stale")}</p>}
      </section>
      <CharacterMap view={view} />
    </div>
  );
}

function Notice({ text }: { text: string }) {
  return <p className="text-text-muted flex h-full items-center justify-center p-6 text-center text-xs">{text}</p>;
}
