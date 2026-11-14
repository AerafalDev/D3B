import type { IDockviewPanelProps } from "dockview-react";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";

import type { EditorParams } from "@/shell/parts/editor-store.ts";
import { Avatar } from "../avatar.tsx";
import { useCharacters } from "../character-store.ts";
import { CharacterTab } from "../character-tab.tsx";
import { named } from "../names.ts";
import { usePlayed } from "./played-store.ts";
import { seenOn, useNow } from "./played-time.ts";

interface PlayedParams extends EditorParams {
  key: string;
}

/**
 * The tab of a character whose client is closed (the owner's mock-up 8, variant D): its header, "Offline", and when
 * and where it was last seen; no map and no gauges, so nothing out of date reads as true. Once the character plays
 * again, the tab becomes its live one (the owner, 2026-11-14).
 */
export function PlayedTab(props: IDockviewPanelProps<PlayedParams>) {
  const { params } = props;
  const connection = useCharacters((store) => {
    const playing = Object.entries(store.views).find(([, view]) => view.key === params.key);
    return playing === undefined ? null : Number(playing[0]);
  });
  if (connection !== null) {
    return <CharacterTab {...props} params={{ icon: params.icon, connection }} />;
  }
  return <Offline {...props} />;
}

function Offline({ api, params }: IDockviewPanelProps<PlayedParams>) {
  const { t, i18n } = useTranslation();
  const character = usePlayed((store) => store.played.find((one) => one.key === params.key));
  const now = useNow();
  const name = character?.name ?? null;

  useEffect(() => {
    if (name !== null) {
      api.setTitle(name);
    }
  }, [api, name]);

  if (character === undefined) {
    return (
      <p className="text-text-muted flex h-full items-center justify-center p-6 text-center text-xs">
        {t("character.forgotten")}
      </p>
    );
  }
  const breed = named(character.breedName, i18n.language);
  const level = character.level;
  const place = character.place;
  const where = named(place?.subArea, i18n.language);
  const when = character.lastSeen === null ? null : seenOn(character.lastSeen, now, i18n.language);

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
      {when !== null && (
        <p className="text-text-muted text-xs">
          {where === null
            ? t("character.lastSeen", { when })
            : place?.at == null
              ? t("character.lastSeenIn", { when, where })
              : t("character.lastSeenAt", { when, where, x: place.at.x, y: place.at.y })}
        </p>
      )}
    </div>
  );
}
