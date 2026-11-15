import { convertFileSrc } from "@tauri-apps/api/core";
import { ChevronDown } from "lucide-react";
import { useTranslation } from "react-i18next";

import type { AtlasHint_Serialize, HintKind } from "@/bindings.ts";
import { named } from "@/characters/names.ts";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu.tsx";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select.tsx";
import { Icon } from "@/icons/icon.tsx";
import { HINT_KINDS } from "./hint-layout.ts";
import { useWorld } from "./world-store.ts";

/**
 * The World tab's editor actions: "Hints", a switch for each kind of hint the world shown holds, its count and a
 * picture of it (the owner's mock-up 11, 2026-11-15), then the list of worlds, by name, the one shown chosen.
 */
export function WorldActions() {
  const { t, i18n } = useTranslation();
  const read = useWorld((store) => store.read);
  const world = useWorld((store) => store.world);
  const show = useWorld((store) => store.show);
  if (read?.state !== "read" || read.atlas.worlds.length === 0) {
    return null;
  }
  const worlds = read.atlas.worlds
    .map((one) => ({ id: one.id, name: named(one.name, i18n.language) ?? String(one.id) }))
    .sort((a, b) => a.name.localeCompare(b.name, i18n.language));
  const hints = read.atlas.worlds.find((one) => one.id === world)?.hints ?? [];
  return (
    <div className="flex items-center gap-1.5">
      {hints.length > 0 && <HintSwitches hints={hints} />}
      <Select value={String(world)} onValueChange={(value) => show(Number(value))}>
        <SelectTrigger size="sm" aria-label={t("world.worlds")} className="h-6 gap-1.5 rounded-[5px] px-2 text-xs">
          <Icon name="globe" className="size-3.5" />
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {worlds.map((one) => (
            <SelectItem key={one.id} value={String(one.id)} className="text-xs">
              {one.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

/** The switches of the kinds `hints` hold, in order of use; showing all or none. */
function HintSwitches({ hints }: { hints: readonly AtlasHint_Serialize[] }) {
  const { t } = useTranslation();
  const hidden = useWorld((store) => store.hidden);
  const hide = useWorld((store) => store.hide);
  const kinds = HINT_KINDS.flatMap((kind) => {
    const held = hints.filter((hint) => hint.kind === kind);
    const picture = held.find((hint) => hint.picture !== null)?.picture ?? null;
    return held.length === 0 ? [] : [{ kind, count: held.length, picture }];
  });
  const toggle = (kind: HintKind) => {
    const next = new Set(hidden);
    if (!next.delete(kind)) {
      next.add(kind);
    }
    hide(next);
  };
  // Kept open by the items, to switch several kinds.
  const keepOpen = (event: Event) => event.preventDefault();
  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="dark:bg-input/30 dark:hover:bg-input/50 focus-visible:border-ring focus-visible:ring-ring/50 border-input flex h-6 items-center gap-1.5 rounded-[5px] border px-2 text-xs outline-none focus-visible:ring-3">
        <Icon name="hints" className="size-3.5" />
        {t("world.hints")}
        <ChevronDown aria-hidden="true" className="text-muted-foreground size-4" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60">
        <DropdownMenuLabel className="text-xs">{t("world.hintsShown")}</DropdownMenuLabel>
        {kinds.map(({ kind, count, picture }) => (
          <DropdownMenuCheckboxItem
            key={kind}
            checked={!hidden.has(kind)}
            onSelect={keepOpen}
            onCheckedChange={() => toggle(kind)}
            className="text-xs"
          >
            {picture === null ? (
              <span className="size-5" />
            ) : (
              <img src={convertFileSrc(picture)} alt="" className="size-5" />
            )}
            {t(`world.kind.${kind}`)}
            <span className="text-text-muted ml-auto tabular-nums">{count}</span>
          </DropdownMenuCheckboxItem>
        ))}
        <DropdownMenuSeparator />
        <DropdownMenuItem
          className="text-xs"
          onSelect={(event) => {
            keepOpen(event);
            hide(new Set());
          }}
        >
          {t("world.hintsAll")}
        </DropdownMenuItem>
        <DropdownMenuItem
          className="text-xs"
          onSelect={(event) => {
            keepOpen(event);
            hide(new Set(HINT_KINDS));
          }}
        >
          {t("world.hintsNone")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
