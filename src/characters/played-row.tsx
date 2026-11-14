import { useTranslation } from "react-i18next";

import { ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuTrigger } from "@/components/ui/context-menu.tsx";
import { cn } from "@/lib/utils.ts";
import { Avatar } from "./avatar.tsx";
import { type PlayedCharacter, seenAgo } from "./played-store.ts";

interface PlayedRowProps {
  character: PlayedCharacter;
  selected: boolean;
  /** Now, in milliseconds: the list says how long ago each was seen. */
  now: number;
  onOpen: () => void;
  onForget: () => void;
}

/**
 * A character played whose client is closed, greyed under the connected ones (the owner's mock-up 8, variant A): its
 * avatar, name and level, and how long ago it was seen. A right-click offers Open and Forget.
 */
export function PlayedRow({ character, selected, now, onOpen, onForget }: PlayedRowProps) {
  const { t, i18n } = useTranslation();
  const seen = seenAgo(character.lastSeen ?? now, now, i18n.language);
  return (
    <ContextMenu>
      <ContextMenuTrigger asChild>
        <button
          type="button"
          onClick={onOpen}
          aria-current={selected ? "true" : undefined}
          className={cn(
            "hover:bg-hover focus-visible:outline-focus mx-1 flex w-[calc(100%-0.5rem)] items-center gap-2.5 rounded-[5px] px-2 py-1.5 text-left opacity-50 focus-visible:outline-1",
            selected && "bg-[#2a2c30] opacity-70 hover:bg-[#2a2c30]",
          )}
        >
          <Avatar name={character.name} head={character.head} size="row" />
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="truncate">
              <span className={cn(selected && "text-white")}>{character.name}</span>
              <span className="text-text-muted text-xs"> {t("characters.level", { level: character.level })}</span>
            </span>
            <span className="text-text-muted truncate text-xs">
              {seen === null ? t("characters.seenNow") : t("characters.seen", { when: seen })}
            </span>
          </span>
          <span aria-hidden="true" className="bg-text-faint size-1.5 shrink-0 rounded-full" />
        </button>
      </ContextMenuTrigger>
      <ContextMenuContent>
        <ContextMenuItem onSelect={onOpen}>{t("characters.open")}</ContextMenuItem>
        <ContextMenuItem onSelect={onForget}>{t("characters.forget", { name: character.name })}</ContextMenuItem>
      </ContextMenuContent>
    </ContextMenu>
  );
}
