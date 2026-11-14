import { useTranslation } from "react-i18next";

import type { GameConnectionStatus } from "@/bindings.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { openEditor, useEditors } from "@/shell/parts/editor-store.ts";
import { CharacterRow } from "./character-row.tsx";
import { useCharacters } from "./character-store.ts";
import { PlayedRow } from "./played-row.tsx";
import { usePlayed } from "./played-store.ts";

// The same empty list each time, so the selector gives a stable snapshot before the relay speaks.
const NONE: readonly GameConnectionStatus[] = [];

const EDITOR = "character:";

/** A connection's character's editor, one per connection. */
function editorId(connection: number): string {
  return `${EDITOR}${connection}`;
}

/** The connection whose character an editor shows, or `null` for another editor or none. */
export function characterOf(editor: string | null): number | null {
  if (editor === null || !editor.startsWith(EDITOR)) {
    return null;
  }
  const connection = Number(editor.slice(EDITOR.length));
  return Number.isInteger(connection) ? connection : null;
}

const PLAYED = "played:";

/** Opens a character played whose client is closed in its tab, or brings that tab forward. */
export function openPlayed(key: string, title: string): void {
  openEditor({ id: `${PLAYED}${key}`, component: "played", title, params: { icon: "user", key } });
}

/** Chooses a connection's character and opens it in its editor tab, or brings that tab forward. */
export function openCharacter(connection: number, title: string): void {
  useCharacters.getState().select(connection);
  openEditor({ id: editorId(connection), component: "character", title, params: { icon: "user", connection } });
}

/**
 * The Characters view of the primary side bar: one row per game connection, from its open to its close
 * (`docs/game.md`, "In the app"), then, under a line, the characters played whose client is closed, greyed, the
 * most recently seen first (the owner's mock-up 8, variant A). A row opens its character in its tab, and is marked
 * while that tab is active. Empty, it says how to start.
 */
export function CharactersView() {
  const { t } = useTranslation();
  const connections = useRelay((store) => store.status?.connections ?? NONE);
  const views = useCharacters((store) => store.views);
  const active = useEditors((store) => store.active);
  const played = usePlayed((store) => store.played);
  const forget = usePlayed((store) => store.forget);
  const online = new Set(Object.values(views).map((view) => view.key));
  const offline = played.filter((character) => !online.has(character.key));
  const now = Date.now();

  if (connections.length === 0 && offline.length === 0) {
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
      {connections.length > 0 && offline.length > 0 && (
        <li role="separator" aria-hidden="true" className="bg-line mx-2.5 my-1.5 h-px" />
      )}
      {offline.map((character) => (
        <li key={character.key}>
          <PlayedRow
            character={character}
            selected={active === `${PLAYED}${character.key}`}
            now={now}
            onOpen={() => openPlayed(character.key, character.name)}
            onForget={() => void forget(character.key)}
          />
        </li>
      ))}
    </ul>
  );
}
