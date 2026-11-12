import { Swords, Users } from "lucide-react";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import type { GameConnectionStatus } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { useCharacters } from "./character-store.ts";
import { GaugeBar } from "./gauge-bar.tsx";

/** The same empty list each time, so that the store's selector gives a stable snapshot before the relay speaks. */
const NONE: readonly GameConnectionStatus[] = [];

/**
 * The characters on the left, one per game connection from its open to its close (`docs/game.md`, "In the app"): its
 * number and protocol before a selection, then name, level, map, fight and life; the one chosen fills the workspace.
 */
export function CharacterList() {
  const { t } = useTranslation();
  const connections = useRelay((store) => store.status?.connections ?? NONE);
  const views = useCharacters((store) => store.views);
  const selected = useCharacters((store) => store.selected);
  const select = useCharacters((store) => store.select);
  const follow = useCharacters((store) => store.follow);

  useEffect(() => follow(), [follow]);

  if (connections.length === 0) {
    return (
      <div className="text-muted-foreground flex flex-1 flex-col items-center justify-center gap-2 p-4 text-center text-sm">
        <Users className="size-6" />
        <p>{t("characters.empty")}</p>
      </div>
    );
  }
  return (
    <ul className="flex flex-col">
      {connections.map((connection) => {
        const view = views[connection.number];
        const at = view?.place?.at;
        const off = connection.protocol !== "on";
        return (
          <li key={connection.number}>
            <button
              type="button"
              onClick={() => select(connection.number)}
              aria-pressed={selected === connection.number}
              className={cn(
                "hover:bg-accent flex w-full flex-col gap-1 border-b px-3 py-2 text-left text-sm",
                selected === connection.number && "bg-accent",
              )}
            >
              <span className="flex w-full justify-between gap-2">
                <span className="truncate font-medium">
                  {view?.name ?? t("characters.connection", { number: connection.number })}
                </span>
                {view?.level != null && (
                  <span className="text-muted-foreground">{t("characters.level", { level: view.level })}</span>
                )}
              </span>
              <span className="text-muted-foreground flex w-full justify-between gap-2 text-xs">
                <span>
                  {off ? t(`relay.protocol.${connection.protocol}`) : at ? `${at.x},${at.y}` : t("characters.noMap")}
                </span>
                {view?.fighting && (
                  <span className="flex items-center gap-1 text-amber-500">
                    <Swords className="size-3" />
                    {t("characters.fight")}
                  </span>
                )}
              </span>
              {view?.life && <GaugeBar gauge={view.life} tone="life" label={t("view.life")} thin />}
            </button>
          </li>
        );
      })}
    </ul>
  );
}
