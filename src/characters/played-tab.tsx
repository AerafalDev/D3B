import type { IDockviewPanelProps } from "dockview-react";
import { useTranslation } from "react-i18next";

import type { EditorParams } from "@/shell/parts/editor-store.ts";
import { Avatar } from "./avatar.tsx";
import { named } from "./names.ts";
import { usePlayed } from "./played-store.ts";

interface PlayedParams extends EditorParams {
  key: string;
}

/**
 * The tab of a character whose client is closed (the owner's mock-up 8, variant D): its header, "Offline", and when
 * and where it was last seen; no map and no gauges, so nothing out of date reads as true.
 */
export function PlayedTab({ params }: IDockviewPanelProps<PlayedParams>) {
  const { t, i18n } = useTranslation();
  const character = usePlayed((store) => store.played.find((one) => one.key === params.key));

  if (character === undefined) {
    return (
      <p className="text-text-muted flex h-full items-center justify-center p-6 text-center text-xs">
        {t("character.forgotten")}
      </p>
    );
  }
  const breed = named(character.breedName, i18n.language);
  const level = character.level;
  const when = new Intl.DateTimeFormat(i18n.language, { dateStyle: "medium", timeStyle: "short" }).format(
    character.lastSeen ?? undefined,
  );
  const place = character.place;
  const where = named(place?.subArea, i18n.language);

  return (
    <div className="flex max-w-[420px] flex-col gap-3 p-4">
      <header className="flex items-center gap-3">
        <Avatar name={character.name} head={character.head} size="tab" />
        <div className="min-w-0">
          <h2 className="truncate text-[15px] font-medium text-white">{character.name}</h2>
          <p className="text-text-muted text-xs">
            {breed === null ? t("character.level", { level }) : t("character.levelAndBreed", { level, breed })} ·{" "}
            {t("characters.offline")}
          </p>
        </div>
      </header>
      <p className="text-text-muted text-xs">
        {where === null || place?.at == null
          ? t("character.lastSeen", { when })
          : t("character.lastSeenAt", { when, where, x: place.at.x, y: place.at.y })}
      </p>
    </div>
  );
}
