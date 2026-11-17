import { getCurrentWebview } from "@tauri-apps/api/webview";
import { type ReactNode, useEffect } from "react";
import { useTranslation } from "react-i18next";

import type { VariableView_Serialize as VariableView } from "@/bindings.ts";
import { problemText } from "@/blueprint/details/problem-text.ts";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog.tsx";
import { Icon } from "@/icons/icon.tsx";
import { isTauri } from "@/lib/tauri.ts";
import { givenText } from "@/library/given-text.ts";
import { shapedText } from "@/library/value-text.ts";
import { useShare } from "./share-store.ts";

/**
 * The import's preview (the owner, 2026-11-16): a `.d3b` chosen in the open dialog or dropped anywhere on the window
 * shows, before anything is saved, its blueprint's name and description, what it is based on, its settings then its
 * other variables, its problems in words and what it holds; Import puts it among mine under a free id and opens its
 * tab, Cancel leaves everything as it was.
 */
export function ImportPreview() {
  const { t, i18n } = useTranslation();
  const preview = useShare((store) => store.preview);
  const importing = useShare((store) => store.importing);
  const confirm = useShare((store) => store.confirm);
  const cancel = useShare((store) => store.cancel);
  const open = useShare((store) => store.open);

  // A file dropped on the window: the first one is previewed, and the back end refuses it with why when it is no .d3b.
  useEffect(() => {
    if (!isTauri()) {
      return;
    }
    const unlisten = getCurrentWebview().onDragDropEvent((event) => {
      const first = event.payload.type === "drop" ? event.payload.paths[0] : undefined;
      if (first !== undefined) {
        void open(first);
      }
    });
    return () => {
      void unlisten.then((stop) => stop());
    };
  }, [open]);

  if (preview === null) {
    return null;
  }
  const blueprint = preview.blueprint;
  const names = new Map(blueprint.variables.map((variable) => [variable.id, variable.name]));
  const settings = blueprint.variables.filter((variable) => variable.perCharacter);
  const others = blueprint.variables.filter((variable) => !variable.perCharacter);
  const holds = [
    t("share.graphs", { count: preview.graphs }),
    t("share.nodes", { count: preview.nodes }),
    t("share.zones", { count: preview.zones }),
    t("share.routes", { count: preview.routes }),
  ].join(" · ");
  const variable = (one: VariableView) => (
    <li key={one.id} className="flex gap-2" title={one.help || undefined}>
      <span className="min-w-0 flex-1 truncate">{one.name}</span>
      <span className="text-text-muted">{t(`blueprint.type.${one.valueType}` as never)}</span>
      <span className="text-text-muted">{shapedText(one, one.default, i18n.language)}</span>
    </li>
  );

  return (
    <Dialog open onOpenChange={(opened) => !opened && cancel()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Icon name="blueprint" />
            {blueprint.name}
          </DialogTitle>
          <DialogDescription>{blueprint.description || t("share.noDescription")}</DialogDescription>
        </DialogHeader>
        <div className="flex max-h-[50vh] flex-col gap-3 overflow-y-auto text-xs">
          {blueprint.basedOn !== null && (
            <p className="text-text-muted">
              {t("library.basedOn", {
                name: givenText(blueprint.basedOn.id, "name", blueprint.basedOn.id),
                version: blueprint.basedOn.version,
              })}
            </p>
          )}
          <Section title={t("share.settings")}>{settings.map(variable)}</Section>
          <Section title={t("share.otherVariables")}>{others.map(variable)}</Section>
          <Section title={t("library.problems", { count: preview.problems.length })}>
            {preview.problems.map((problem) => {
              const text = problemText(problem, names);
              return (
                <li key={`${problem.kind}-${problem.graph}-${problem.node}-${problem.pin}-${problem.subject}`}>
                  <Icon name="warning" className="mr-1 inline size-3.5" />
                  {text}
                </li>
              );
            })}
          </Section>
          <p className="text-text-muted">{t("share.holds", { what: holds })}</p>
        </div>
        <DialogFooter>
          <button
            type="button"
            onClick={cancel}
            className="hover:bg-hover focus-visible:outline-focus rounded px-3 py-1 text-xs focus-visible:outline-1"
          >
            {t("share.cancel")}
          </button>
          <button
            type="button"
            disabled={importing}
            onClick={() => void confirm()}
            className="bg-focus hover:bg-focus/90 focus-visible:outline-focus rounded px-3 py-1 text-xs text-white focus-visible:outline-1 focus-visible:outline-offset-2 disabled:opacity-60"
          >
            {t("share.confirm")}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** A titled list of the preview, left out when empty. */
function Section({ title, children }: { title: string; children: ReactNode[] }) {
  if (children.length === 0) {
    return null;
  }
  return (
    <section>
      <h3 className="text-text-muted mb-1 text-[11px] font-semibold tracking-[.04em] uppercase">{title}</h3>
      <ul className="flex flex-col gap-0.5">{children}</ul>
    </section>
  );
}
