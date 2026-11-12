import { useTranslation } from "react-i18next";

import type { CharacterView } from "./character-store.ts";

/** What is around the character on its map, grouped by kind (`docs/game.md`, "What is around"). */
export function AroundList({ view }: { view: CharacterView }) {
  const { t } = useTranslation();
  if (view.fighting) {
    return <p className="text-muted-foreground w-80 rounded-lg border p-3 text-sm">{t("around.inFight")}</p>;
  }
  const players = view.actors.flatMap((actor) =>
    actor.kind.kind === "player" ? [{ actor, name: actor.kind.name }] : [],
  );
  const npcs = view.actors.flatMap((actor) => (actor.kind.kind === "npc" ? [{ actor, id: actor.kind.npcId }] : []));
  const groups = view.actors.flatMap((actor) =>
    actor.kind.kind === "monsterGroup" ? [{ actor, monsters: actor.kind.monsters }] : [],
  );
  const others = view.actors.filter((actor) => actor.kind.kind === "other").length;
  const cell = (value: number | null) => (value == null ? "" : t("view.cell", { cell: value }));
  const state = (value: number) =>
    t(
      value === 0
        ? "around.available"
        : value === 1
          ? "around.harvested"
          : value === 2
            ? "around.harvesting"
            : "around.state",
      { state: value },
    );

  return (
    <section className="flex w-80 flex-col gap-3 rounded-lg border p-3 text-sm">
      <h3 className="font-semibold">{t("around.title")}</h3>
      <div>
        <h4 className="text-muted-foreground text-xs">{t("around.players", { count: players.length })}</h4>
        {players.map(({ actor, name }) => (
          <p key={actor.id} className="flex justify-between pl-2">
            {name}
            <span className="text-muted-foreground text-xs">{cell(actor.cell)}</span>
          </p>
        ))}
      </div>
      <div>
        <h4 className="text-muted-foreground text-xs">{t("around.groups", { count: groups.length })}</h4>
        {groups.map(({ actor, monsters }) => {
          const levels = monsters.map((monster) => monster.level);
          const first = monsters[0];
          return (
            <p key={actor.id} className="flex justify-between pl-2">
              {first ? t("around.group", { id: first.id, others: monsters.length - 1 }) : t("around.emptyGroup")}
              <span className="text-muted-foreground text-xs">
                {levels.length > 0 && t("around.levels", { low: Math.min(...levels), high: Math.max(...levels) })}
              </span>
            </p>
          );
        })}
      </div>
      <div>
        <h4 className="text-muted-foreground text-xs">{t("around.npcs", { count: npcs.length })}</h4>
        {npcs.map(({ actor, id }) => (
          <p key={actor.id} className="flex justify-between pl-2">
            {t("around.npc", { id })}
            <span className="text-muted-foreground text-xs">{cell(actor.cell)}</span>
          </p>
        ))}
        {others > 0 && <p className="text-muted-foreground pl-2">{t("around.others", { count: others })}</p>}
      </div>
      <div>
        <h4 className="text-muted-foreground text-xs">{t("around.resources", { count: view.stated.length })}</h4>
        {view.stated.map((element) => (
          <p key={element.elementId} className="flex justify-between pl-2">
            {t("around.element", { id: element.elementId })}
            <span className="text-muted-foreground text-xs">{state(element.state)}</span>
          </p>
        ))}
      </div>
    </section>
  );
}
