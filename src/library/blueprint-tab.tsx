import type { IDockviewPanelProps } from "dockview-react";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import type { EditorParams } from "@/shell/parts/editor-store.ts";
import { givenText } from "./given-text.ts";
import { useLibrary } from "./library-store.ts";
import { blueprintName, openBlueprint } from "./library-view.tsx";
import { valueText } from "./value-text.ts";

interface BlueprintParams extends EditorParams {
  id: string;
}

/**
 * A blueprint's tab, a preview until the editor comes (the owner's mock-up 9, variant C): its name, whose it is, its
 * description, its settings per character with their defaults, and Duplicate. The graph (PR 7) and the Summary
 * (PR 8) come into this same tab.
 */
export function BlueprintTab({ api, params }: IDockviewPanelProps<BlueprintParams>) {
  const { t, i18n } = useTranslation();
  const blueprint = useLibrary((store) => store.blueprints.find((one) => one.id === params.id));
  const duplicate = useLibrary((store) => store.duplicate);
  const name = blueprint === undefined ? null : blueprintName(blueprint);

  useEffect(() => {
    if (name !== null) {
      api.setTitle(name);
    }
  }, [api, name]);

  if (blueprint === undefined || name === null) {
    return <p className="text-text-muted flex h-full items-center justify-center p-6 text-xs">{t("library.gone")}</p>;
  }
  const read = (path: string, fallback: string) =>
    blueprint.given ? givenText(blueprint.id, path, fallback) : fallback;
  const settings = blueprint.variables.filter((variable) => variable.perCharacter);
  const whose = blueprint.given
    ? t("library.givenLine", { version: blueprint.version })
    : blueprint.basedOn === null
      ? t("library.mineLine")
      : t("library.mineBasedOn", {
          name: givenText(blueprint.basedOn.id, "name", blueprint.basedOn.id),
          version: blueprint.basedOn.version,
        });

  return (
    <div className="flex max-w-[520px] flex-col gap-3 px-5 py-4">
      <header className="flex items-center gap-2.5">
        <Icon name="blueprint" className="size-[22px]" />
        <div className="min-w-0 flex-1">
          <h2 className="truncate text-[15px] font-medium text-white">{name}</h2>
          <p className="text-text-muted text-xs">{whose}</p>
        </div>
        <button
          type="button"
          onClick={() =>
            void duplicate(blueprint).then((id) => {
              if (id !== null) {
                openBlueprint(id, t("library.copyName", { name }));
              }
            })
          }
          className="bg-focus hover:bg-focus/90 focus-visible:outline-focus flex items-center gap-1.5 rounded px-3 py-1 text-xs text-white focus-visible:outline-1 focus-visible:outline-offset-2"
        >
          <Icon name="copy" className="size-3.5 text-white" />
          {t("library.duplicate")}
        </button>
      </header>
      <p className="text-xs">{read("description", blueprint.description)}</p>
      {settings.length > 0 && (
        <section aria-label={t("library.settings")} className="flex flex-col gap-1.5">
          <h3 className="text-text-muted text-xs">{t("library.settings")}</h3>
          <dl className="grid grid-cols-[140px_1fr] gap-y-1.5 text-xs">
            {settings.map((variable) => (
              <div key={variable.id} className="contents" title={read(`variables.${variable.id}.help`, variable.help)}>
                <dt className="text-text-muted">{read(`variables.${variable.id}.name`, variable.name)}</dt>
                <dd className={variable.default === null ? "text-text-muted" : undefined}>
                  {valueText(variable.valueType, variable.default, i18n.language)}
                </dd>
              </div>
            ))}
          </dl>
        </section>
      )}
      {blueprint.problems > 0 && (
        <p className="text-xs text-[#d29922]">{t("library.problems", { count: blueprint.problems })}</p>
      )}
      <p className="text-text-muted rounded-md border border-dashed border-[#3c3c3c] px-3 py-2.5 text-xs">
        {t("library.soon")}
      </p>
    </div>
  );
}
