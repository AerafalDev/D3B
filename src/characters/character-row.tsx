import { useTranslation } from "react-i18next";

import type { GameConnectionStatus } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";
import { characterState } from "./character-state.ts";
import type { CharacterView } from "./character-store.ts";
import { initials, placeName } from "./names.ts";

interface CharacterRowProps {
  connection: GameConnectionStatus;
  view: CharacterView | undefined;
  selected: boolean;
  onOpen: () => void;
}

// The state's dot: green exploring, amber fighting, grey waiting or unread, red lost.
const DOT = {
  waiting: "bg-text-faint",
  exploring: "bg-[#3fb950]",
  fighting: "bg-[#d29922]",
  off: "bg-text-faint",
  lost: "bg-[#f85149]",
} as const;

/**
 * A character in the list: its avatar (its initials until the character renderer comes), its name and level, where it
 * is or what it is doing, and its state's dot. Before its name is known, its connection's number stands in.
 */
export function CharacterRow({ connection, view, selected, onOpen }: CharacterRowProps) {
  const { t, i18n } = useTranslation();
  const state = characterState(connection.protocol, view);
  const name = view?.name ?? t("characters.connection", { number: connection.number });
  const where = state === "exploring" ? placeName(view?.place, i18n.language) : null;

  return (
    <button
      type="button"
      onClick={onOpen}
      aria-current={selected ? "true" : undefined}
      className={cn(
        "hover:bg-hover focus-visible:outline-focus mx-1 flex w-[calc(100%-0.5rem)] items-center gap-2.5 rounded-[5px] px-2 py-1.5 text-left focus-visible:outline-1",
        selected && "bg-[#2a2c30] hover:bg-[#2a2c30]",
      )}
    >
      <span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-[#2a2c30] text-[11px] font-medium">
        {initials(name)}
      </span>
      <span className="flex min-w-0 flex-1 flex-col">
        <span className="truncate">
          <span className={cn(selected && "text-white")}>{name}</span>
          {view?.level != null && (
            <span className="text-text-muted text-xs"> {t("characters.level", { level: view.level })}</span>
          )}
        </span>
        <span className="text-text-muted truncate text-xs">{where ?? t(`characters.state.${state}`)}</span>
      </span>
      <span aria-hidden="true" className={cn("size-1.5 shrink-0 rounded-full", DOT[state])} />
    </button>
  );
}
