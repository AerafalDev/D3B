import { useTranslation } from "react-i18next";

import type { GameConnectionStatus } from "@/bindings.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { openEditor, useEditors } from "@/shell/parts/editor-store.ts";
import { CharacterRow } from "./character-row.tsx";
import { useCharacters } from "./character-store.ts";

// The same empty list each time, so the selector gives a stable snapshot before the relay speaks.
const NONE: readonly GameConnectionStatus[] = [];

/** A connection's character's editor, one per connection. */
function editorId(connection: number): string {
  return `character:${connection}`;
}

/** Chooses a connection's character and opens it in its editor tab, or brings that tab forward. */
export function openCharacter(connection: number, title: string): void {
  useCharacters.getState().select(connection);
  openEditor({ id: editorId(connection), component: "character", title, params: { icon: "user", connection } });
}

/**
 * The Characters view of the primary side bar: one row per game connection, from its open to its close
 * (`docs/game.md`, "In the app"); a row opens its character in its tab, and is marked while that tab is active.
 * Empty, it says how to start.
 */
export function CharactersView() {
  const { t } = useTranslation();
  const connections = useRelay((store) => store.status?.connections ?? NONE);
  const views = useCharacters((store) => store.views);
  const active = useEditors((store) => store.active);

  if (connections.length === 0) {
    return (
      <p className="text-text-muted flex flex-1 items-center justify-center px-6 pb-[35px] text-center text-xs">
        {t("characters.empty")}
      </p>
    );
  }
  return (
    <ul className="flex flex-col gap-0.5 overflow-y-auto py-1">
      {connections.map((connection) => {
        const view = views[connection.number];
        return (
          <li key={connection.number}>
            <CharacterRow
              connection={connection}
              view={view}
              selected={active === editorId(connection.number)}
              onOpen={() =>
                openCharacter(
                  connection.number,
                  view?.name ?? t("characters.connection", { number: connection.number }),
                )
              }
            />
          </li>
        );
      })}
    </ul>
  );
}
