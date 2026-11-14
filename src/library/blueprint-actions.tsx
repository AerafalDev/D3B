import { useTranslation } from "react-i18next";

import { cn } from "@/lib/utils.ts";
import { type BlueprintView, useBlueprintView } from "./blueprint-view.ts";

const VIEWS: readonly BlueprintView[] = ["graph", "summary"];

/** A blueprint tab's editor actions: "Graph | Summary", the prototype's toggle, shown while its tab is active. */
export function BlueprintActions({ tab }: { tab: string }) {
  const { t } = useTranslation();
  const view = useBlueprintView((store) => store.view[tab] ?? "graph");
  const setView = useBlueprintView((store) => store.setView);
  return (
    <fieldset
      aria-label={t("blueprint.view.label")}
      className="m-0 flex overflow-hidden rounded-[5px] border border-[#3c3c3c] p-0 text-xs"
    >
      {VIEWS.map((one) => (
        <button
          key={one}
          type="button"
          aria-pressed={view === one}
          onClick={() => setView(tab, one)}
          className={cn(
            "focus-visible:outline-focus px-2.5 py-0.5 focus-visible:outline-1 focus-visible:-outline-offset-1",
            view === one ? "bg-[#313131] text-white" : "text-text-muted hover:text-text",
          )}
        >
          {t(`blueprint.view.${one}`)}
        </button>
      ))}
    </fieldset>
  );
}
