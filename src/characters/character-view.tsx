import { useTranslation } from "react-i18next";

import type { Names } from "@/bindings.ts";
import { Badge } from "@/components/ui/badge.tsx";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs.tsx";
import { useRelay } from "@/relay/relay-store.ts";
import { AroundList } from "./around-list.tsx";
import { useCharacters } from "./character-store.ts";
import { GaugeBar } from "./gauge-bar.tsx";

/** A name in the interface's language. */
function named(names: Names | null | undefined, language: string): string | null {
  if (!names) {
    return null;
  }
  return language.startsWith("fr") ? names.fr : names.en;
}

/** The character chosen on the left, its View and Protocol tabs (`docs/game.md`, "In the app", the owner's choice). */
export function CharacterView() {
  const { t, i18n } = useTranslation();
  const selected = useCharacters((store) => store.selected);
  const view = useCharacters((store) => (store.selected === null ? undefined : store.views[store.selected]));
  const connection = useRelay((store) => store.status?.connections.find((one) => one.number === selected));

  if (selected === null || connection === undefined) {
    return <p className="text-muted-foreground m-auto text-sm">{t("workspace.empty")}</p>;
  }
  const numbers = new Intl.NumberFormat(i18n.language);
  const format = (value: number | null | undefined) => (value == null ? "—" : numbers.format(value));
  const place = view?.place;
  const lost = connection.protocol === "lost";
  const off = connection.protocol !== "on" && !lost;
  return (
    <Tabs defaultValue="view" className="flex min-h-0 flex-1 flex-col gap-0">
      <div className="flex items-center gap-3 border-b px-3 py-1.5">
        <TabsList>
          <TabsTrigger value="view">{t("view.tab")}</TabsTrigger>
          <TabsTrigger value="protocol">{t("view.protocolTab")}</TabsTrigger>
        </TabsList>
        <span className="text-muted-foreground ml-auto text-xs">
          {t("relay.connectionBuild", {
            number: connection.number,
            build: connection.build ?? "?",
            protocol: t(`relay.protocol.${connection.protocol}`),
          })}
        </span>
      </div>
      <TabsContent value="view" className="min-h-0 flex-1 overflow-auto p-4">
        {off || !view ? (
          <p className="text-muted-foreground text-sm">
            {off ? t(`relay.protocol.${connection.protocol}`) : t("view.waiting")}
          </p>
        ) : (
          <div className="flex flex-wrap items-start gap-4">
            <section className="flex w-80 flex-col gap-3">
              <div className="flex flex-col gap-1 rounded-lg border p-3 text-sm">
                <div className="flex items-baseline justify-between">
                  <span className="text-base font-semibold">
                    {view.name ?? t("characters.connection", { number: connection.number })}
                  </span>
                  {view.level != null && (
                    <span className="text-muted-foreground">{t("characters.level", { level: view.level })}</span>
                  )}
                </div>
                {view.breed != null && (
                  <span className="text-muted-foreground">{t("view.breed", { breed: view.breed })}</span>
                )}
                <span>
                  {place?.at ? t("view.map", { x: place.at.x, y: place.at.y }) : t("characters.noMap")}
                  {view.cell != null && ` · ${t("view.cell", { cell: view.cell })}`}
                </span>
                {(place?.subArea || place?.area) && (
                  <span className="text-muted-foreground">
                    {[named(place?.subArea, i18n.language), named(place?.area, i18n.language)]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                )}
                <div className="flex gap-2 pt-1">
                  {view.fighting != null && (
                    <Badge variant={view.fighting ? "destructive" : "secondary"}>
                      {t(view.fighting ? "characters.fight" : "view.roleplay")}
                    </Badge>
                  )}
                  {lost && <Badge variant="outline">{t("view.lost")}</Badge>}
                </div>
              </div>
              <div className="flex flex-col gap-2 rounded-lg border p-3 text-sm">
                {view.life && (
                  <div className="flex flex-col gap-1">
                    <span className="flex justify-between">
                      {t("view.life")}
                      <span>
                        {format(view.life.value)} / {format(view.life.max)}
                        {view.lifeEstimated && <span className="text-muted-foreground"> {t("view.estimated")}</span>}
                      </span>
                    </span>
                    <GaugeBar gauge={view.life} tone="life" label={t("view.life")} />
                  </div>
                )}
                {view.pods && (
                  <div className="flex flex-col gap-1">
                    <span className="flex justify-between">
                      {t("view.pods")}
                      <span>
                        {format(view.pods.value)} / {format(view.pods.max)}
                      </span>
                    </span>
                    <GaugeBar gauge={view.pods} tone="pods" label={t("view.pods")} />
                  </div>
                )}
                {view.experience && (
                  <div className="flex flex-col gap-1">
                    <span className="flex justify-between">
                      {t("view.experience")}
                      <span className="text-muted-foreground">
                        {format(view.experience.value)} / {format(view.experience.max)}
                      </span>
                    </span>
                    <GaugeBar gauge={view.experience} tone="experience" label={t("view.experience")} thin />
                  </div>
                )}
                <dl className="grid grid-cols-2 gap-x-3 gap-y-1">
                  <dt className="text-muted-foreground">{t("view.kamas")}</dt>
                  <dd>{format(view.kamas)}</dd>
                  <dt className="text-muted-foreground">{t("view.energy")}</dt>
                  <dd>{view.energy ? `${format(view.energy.value)} / ${format(view.energy.max)}` : "—"}</dd>
                  <dt className="text-muted-foreground">{t("view.actionPoints")}</dt>
                  <dd>{format(view.actionPoints)}</dd>
                  <dt className="text-muted-foreground">{t("view.movementPoints")}</dt>
                  <dd>{format(view.movementPoints)}</dd>
                </dl>
                {(view.stale.map || view.stale.life || view.stale.pods || view.stale.kamas) && (
                  <p className="text-xs text-amber-500">{t("view.stale", { count: connection.dropped, dropped: numbers.format(connection.dropped) })}</p>
                )}
              </div>
            </section>
            <AroundList view={view} />
          </div>
        )}
      </TabsContent>
      <TabsContent value="protocol" className="text-muted-foreground p-4 text-sm">
        {t("view.protocolSoon")}
      </TabsContent>
    </Tabs>
  );
}
