import { ArrowUp, ChevronDown, ChevronRight } from "lucide-react";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import type { KeptFile } from "@/bindings.ts";
import { useGraphs } from "@/blueprint/graph-store.ts";
import { ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuTrigger } from "@/components/ui/context-menu.tsx";
import { Icon } from "@/icons/icon.tsx";
import { cn } from "@/lib/utils.ts";
import { useShare } from "@/share/share-store.ts";
import { closeEditor, useEditors } from "@/shell/parts/editor-store.ts";
import { givenText } from "./given-text.ts";
import { type BlueprintSummary, useLibrary } from "./library-store.ts";
import { BLUEPRINT_EDITOR, blueprintName, openBlueprint } from "./open-blueprint.ts";

/**
 * The Library view of the primary side bar (the owner's mock-up 9, variant A): "Given by D3B", then "Mine", one row
 * per blueprint, its name alone, its description or what a copy is based on in its hover, an amber mark with how
 * many problems it has; a copy whose original has a newer version a blue arrow, said in its hover too (the owner,
 * 2026-11-16); a right-click offers Duplicate, Export, and Delete on mine, to the system's trash without asking (the owner,
 * 2026-11-16), its tab closed. Last, folded, the player's files kept as they are, each
 * with why in its hover. The list is read again each time the view is shown, so a file the player put in the folder
 * appears without a restart.
 */
export function LibraryView() {
  const { t } = useTranslation();
  const blueprints = useLibrary((store) => store.blueprints);
  const kept = useLibrary((store) => store.kept);
  const duplicate = useLibrary((store) => store.duplicate);
  const exportBlueprint = useShare((store) => store.exportBlueprint);
  const remove = useLibrary((store) => store.remove);
  const rename = useLibrary((store) => store.rename);
  // The blueprint of mine whose name is edited in its row (the owner, 2026-11-17: F2 or the right-click, as my zones').
  const [renaming, setRenaming] = useState<string | null>(null);
  const renameAsked = useRef(false);
  // The row renamed, which takes the focus back once its name is no longer edited, as my zones' do.
  const [renamed, setRenamed] = useState<string | null>(null);
  const list = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (renamed !== null) {
      list.current?.querySelector<HTMLElement>(`[data-blueprint="${CSS.escape(renamed)}"]`)?.focus();
      setRenamed(null);
    }
  }, [renamed]);
  const forgetGraphs = useGraphs((store) => store.forget);
  const active = useEditors((store) => store.active);
  const refresh = useLibrary((store) => store.refresh);

  useEffect(() => {
    void refresh();
  }, [refresh]);
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
      basedOn?.newer == null ? null : t("library.originalNewer", { version: basedOn.newer }),
    ]
      .filter(Boolean)
      .join("\n");
    if (renaming === blueprint.id) {
      return (
        <li key={blueprint.id}>
          <RowName
            name={blueprint.name}
            label={t("library.name")}
            done={(named) => {
              setRenaming(null);
              setRenamed(blueprint.id);
              if (named !== null) {
                void rename(blueprint, { name: named });
              }
            }}
          />
        </li>
      );
    }
    return (
      <li key={blueprint.id}>
        <ContextMenu>
          <ContextMenuTrigger asChild>
            <button
              type="button"
              title={hover}
              data-blueprint={blueprint.id}
              onClick={() => openBlueprint(blueprint.id, name)}
              onKeyDown={(event) => {
                if (event.key === "F2" && !blueprint.given) {
                  event.preventDefault();
                  setRenaming(blueprint.id);
                }
              }}
              aria-current={active === `${BLUEPRINT_EDITOR}${blueprint.id}` ? "true" : undefined}
              className="hover:bg-hover focus-visible:outline-focus mx-1 flex w-[calc(100%-0.5rem)] items-center gap-2 rounded-[5px] px-2 py-1 text-left focus-visible:outline-1 aria-[current=true]:bg-[#2a2c30] aria-[current=true]:text-white"
            >
              <Icon name="blueprint" />
              <span className="min-w-0 flex-1 truncate">{name}</span>
              {basedOn?.newer != null && (
                <span className="text-focus flex items-center">
                  <ArrowUp aria-hidden="true" size={14} strokeWidth={1.75} />
                  <span className="sr-only">{t("library.originalNewer", { version: basedOn.newer })}</span>
                </span>
              )}
              {blueprint.problems > 0 && (
                <span className="flex items-center gap-0.5 text-[11px] text-[#d29922]">
                  <Icon name="warning" className="size-3.5" />
                  <span aria-hidden="true">{blueprint.problems}</span>
                  <span className="sr-only">{t("library.problems", { count: blueprint.problems })}</span>
                </span>
              )}
            </button>
          </ContextMenuTrigger>
          <ContextMenuContent
            // The menu gives the focus back to its row as it closes, which would end the name just opened there.
            onCloseAutoFocus={(event) => {
              if (renameAsked.current) {
                renameAsked.current = false;
                event.preventDefault();
              }
            }}
          >
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
            <ContextMenuItem onSelect={() => void exportBlueprint(blueprint)}>{t("share.export")}</ContextMenuItem>
            {!blueprint.given && (
              <ContextMenuItem
                onSelect={() => {
                  renameAsked.current = true;
                  setRenaming(blueprint.id);
                }}
              >
                {t("library.rename")}
              </ContextMenuItem>
            )}
            {!blueprint.given && (
              <ContextMenuItem
                onSelect={() =>
                  void remove(blueprint).then((deleted) => {
                    if (deleted) {
                      closeEditor(`${BLUEPRINT_EDITOR}${blueprint.id}`);
                      forgetGraphs(blueprint.id);
                    }
                  })
                }
              >
                {t("library.delete")}
              </ContextMenuItem>
            )}
          </ContextMenuContent>
        </ContextMenu>
      </li>
    );
  };

  return (
    <div ref={list} className="flex flex-col overflow-y-auto py-0.5">
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
                <span className="sr-only">{keptWhy(file, t)}</span>
              </span>
            </li>
          ))}
        </Group>
      )}
    </div>
  );
}

interface RowNameProps {
  name: string;
  label: string;
  /** The name typed, without the spaces around it, or `null` when Escape, a blank or the same name leaves it. */
  done: (name: string | null) => void;
}

/** A blueprint's name edited in its row: Enter or leaving saves it, Escape or a blank one leaves it as it was. */
function RowName({ name, label, done }: RowNameProps) {
  const [draft, setDraft] = useState(name);
  // Escape ends it without saving, even when the field's removal blurs it after.
  const cancelled = useRef(false);
  return (
    <input
      // biome-ignore lint/a11y/noAutofocus: F2 or the menu asked to type the name now
      autoFocus
      aria-label={label}
      value={draft}
      onChange={(event) => setDraft(event.target.value)}
      onFocus={(event) => event.currentTarget.select()}
      onBlur={() => {
        const trimmed = draft.trim();
        done(cancelled.current || trimmed === "" || trimmed === name ? null : trimmed);
      }}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          event.currentTarget.blur();
        } else if (event.key === "Escape") {
          event.preventDefault();
          cancelled.current = true;
          event.currentTarget.blur();
        }
      }}
      className="border-focus mx-1 h-6 w-[calc(100%-0.5rem)] rounded-[5px] border bg-[#181818] px-2 text-[13px] outline-0"
    />
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
    case "notAZone":
      return t("library.keptWhy.notOfItsShape", { detail: why.detail });
    case "nameIsNotId":
      return t("library.keptWhy.nameIsNotId", { id: why.id });
    case "givenId":
      return t("library.keptWhy.givenId", { id: why.id });
  }
}
