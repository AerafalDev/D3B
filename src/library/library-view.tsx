import { ChevronDown, ChevronRight } from "lucide-react";
import { type ReactNode, useState } from "react";
import { useTranslation } from "react-i18next";

import type { KeptFile } from "@/bindings.ts";
import { ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuTrigger } from "@/components/ui/context-menu.tsx";
import { Icon } from "@/icons/icon.tsx";
import { cn } from "@/lib/utils.ts";
import { openEditor, useEditors } from "@/shell/parts/editor-store.ts";
import { givenText } from "./given-text.ts";
import { type BlueprintSummary, useLibrary } from "./library-store.ts";

const EDITOR = "blueprint:";

/** Opens a blueprint in its tab, or brings that tab forward. */
export function openBlueprint(id: string, title: string): void {
  openEditor({ id: `${EDITOR}${id}`, component: "blueprint", title, params: { icon: "blueprint", id } });
}

/** A blueprint's name as the player reads it: a given one's in their language. */
export function blueprintName(blueprint: BlueprintSummary): string {
  return blueprint.given ? givenText(blueprint.id, "name", blueprint.name) : blueprint.name;
}

/**
 * The Library view of the primary side bar (the owner's mock-up 9, variant A): "Given by D3B", then "Mine", one row
 * per blueprint, its name alone, its description or what a copy is based on in its hover, an amber mark with how
 * many problems it has; a right-click offers Duplicate. Last, folded, the player's files kept as they are, each
 * with why in its hover.
 */
export function LibraryView() {
  const { t } = useTranslation();
  const blueprints = useLibrary((store) => store.blueprints);
  const kept = useLibrary((store) => store.kept);
  const duplicate = useLibrary((store) => store.duplicate);
  const active = useEditors((store) => store.active);
  const byName = new Map(blueprints.map((blueprint) => [blueprint.id, blueprintName(blueprint)]));

  const row = (blueprint: BlueprintSummary) => {
    const name = blueprintName(blueprint);
    const description = blueprint.given
      ? givenText(blueprint.id, "description", blueprint.description)
      : blueprint.description;
    const basedOn = blueprint.basedOn;
    const hover = [
      description,
      basedOn === null
        ? null
        : t("library.basedOn", { name: byName.get(basedOn.id) ?? basedOn.id, version: basedOn.version }),
    ]
      .filter(Boolean)
      .join("\n");
    return (
      <li key={blueprint.id}>
        <ContextMenu>
          <ContextMenuTrigger asChild>
            <button
              type="button"
              title={hover}
              onClick={() => openBlueprint(blueprint.id, name)}
              aria-current={active === `${EDITOR}${blueprint.id}` ? "true" : undefined}
              className="hover:bg-hover focus-visible:outline-focus mx-1 flex w-[calc(100%-0.5rem)] items-center gap-2 rounded-[5px] px-2 py-1 text-left focus-visible:outline-1 aria-[current=true]:bg-[#2a2c30] aria-[current=true]:text-white"
            >
              <Icon name="blueprint" />
              <span className="min-w-0 flex-1 truncate">{name}</span>
              {blueprint.problems > 0 && (
                <span className="flex items-center gap-0.5 text-[11px] text-[#d29922]">
                  <Icon name="warning" className="size-3.5" />
                  <span aria-hidden="true">{blueprint.problems}</span>
                  <span className="sr-only">{t("library.problems", { count: blueprint.problems })}</span>
                </span>
              )}
            </button>
          </ContextMenuTrigger>
          <ContextMenuContent>
            <ContextMenuItem
              onSelect={() =>
                void duplicate(blueprint).then((id) => {
                  if (id !== null) {
                    openBlueprint(id, t("library.copyName", { name }));
                  }
                })
              }
            >
              {t("library.duplicate")}
            </ContextMenuItem>
          </ContextMenuContent>
        </ContextMenu>
      </li>
    );
  };

  return (
    <div className="flex flex-col overflow-y-auto py-0.5">
      <Group title={t("library.given")}>{blueprints.filter((one) => one.given).map(row)}</Group>
      <Group title={t("library.mine")}>{blueprints.filter((one) => !one.given).map(row)}</Group>
      {kept.length > 0 && (
        <Group title={t("library.kept")} count={kept.length} folded>
          {kept.map((file) => (
            <li key={file.path}>
              <span
                title={keptWhy(file, t)}
                className="mx-1 flex items-center gap-2 rounded-[5px] px-2 py-1 opacity-70"
              >
                <Icon name="fileWarning" className="text-[#d29922]" />
                <span className="min-w-0 flex-1 truncate">{fileName(file.path)}</span>
              </span>
            </li>
          ))}
        </Group>
      )}
    </div>
  );
}

interface GroupProps {
  title: string;
  count?: number;
  folded?: boolean;
  children: ReactNode;
}

/** A group's header, which folds and unfolds it, as VS Code's sections do. */
function Group({ title, count, folded = false, children }: GroupProps) {
  const [open, setOpen] = useState(!folded);
  const Chevron = open ? ChevronDown : ChevronRight;
  return (
    <section className="mt-1">
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen(!open)}
        className="text-text-muted hover:text-text focus-visible:outline-focus flex h-[22px] w-full items-center gap-1 px-2 text-[11px] font-semibold tracking-[.04em] uppercase focus-visible:outline-1"
      >
        <Chevron aria-hidden="true" className="size-3.5" />
        {title}
        {count !== undefined && <span className="text-text-faint font-normal normal-case">{count}</span>}
      </button>
      <ul className={cn("flex flex-col gap-px", !open && "hidden")}>{children}</ul>
    </section>
  );
}

/** A path's last part: the file's name. */
function fileName(path: string): string {
  return path.split(/[\\/]/).pop() ?? path;
}

/** Why a file is kept, in the player's language. */
function keptWhy(file: KeptFile, t: ReturnType<typeof useTranslation>["t"]): string {
  const why = file.why;
  switch (why.kind) {
    case "unreadable":
      return t("library.keptWhy.unreadable", { reason: why.reason });
    case "notText":
      return t("library.keptWhy.notText");
    case "newer":
      return t("library.keptWhy.newer", { found: why.found });
    case "notABlueprint":
    case "notACharacter":
      return t("library.keptWhy.notOfItsShape", { detail: why.detail });
    case "nameIsNotId":
      return t("library.keptWhy.nameIsNotId", { id: why.id });
    case "givenId":
      return t("library.keptWhy.givenId", { id: why.id });
  }
}
