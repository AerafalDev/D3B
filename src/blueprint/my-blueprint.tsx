import { Eye } from "lucide-react";
import { useTranslation } from "react-i18next";

import type { GraphView_Serialize } from "@/bindings.ts";
import { Icon } from "@/icons/icon.tsx";
import { cn } from "@/lib/utils.ts";
import { givenText } from "@/library/given-text.ts";
import type { BlueprintSummary } from "@/library/library-store.ts";
import { valueText } from "@/library/value-text.ts";

interface MyBlueprintProps {
  blueprint: BlueprintSummary;
  graphs: readonly GraphView_Serialize[];
  /** The graph the canvas shows. */
  shown: string;
  onShow: (graph: string) => void;
}

/**
 * Unreal's "My Blueprint" (`docs/bot.md`, "In the app"): the blueprint's event graphs, its functions and its
 * variables, an eye on those set per character, each with its type's colour; a variable's help and default in its
 * hover and said to screen readers. A graph or a function shows on the canvas when chosen.
 */
export function MyBlueprint({ blueprint, graphs, shown, onShow }: MyBlueprintProps) {
  const { t, i18n } = useTranslation();
  const read = (path: string, fallback: string) =>
    blueprint.given ? givenText(blueprint.id, path, fallback) : fallback;
  // An event graph reads as Unreal's "Event Graph" in the player's language; a function by its name.
  const graphName = (graph: GraphView_Serialize) => (graph.function ? graph.name : t("blueprint.eventGraph"));

  const graphRow = (graph: GraphView_Serialize) => (
    <li key={graph.id}>
      <button
        type="button"
        aria-current={graph.id === shown ? "true" : undefined}
        onClick={() => onShow(graph.id)}
        className="hover:bg-hover focus-visible:outline-focus mx-1 flex w-[calc(100%-0.5rem)] items-center gap-2 rounded-[5px] px-2 py-1 text-left focus-visible:outline-1 aria-[current=true]:bg-[#2a2c30] aria-[current=true]:text-white"
      >
        <Icon name={graph.function ? "blueprintFunction" : "blueprint"} />
        <span className="min-w-0 flex-1 truncate">{graphName(graph)}</span>
      </button>
    </li>
  );

  return (
    <nav
      aria-label={t("blueprint.myBlueprint")}
      className="border-line flex min-h-0 flex-col gap-2 overflow-y-auto border-r py-2 text-[13px]"
    >
      <Section title={t("blueprint.graphs")}>{graphs.filter((graph) => !graph.function).map(graphRow)}</Section>
      <Section title={t("blueprint.functions")}>{graphs.filter((graph) => graph.function).map(graphRow)}</Section>
      <Section title={t("blueprint.variables")}>
        {blueprint.variables.map((variable) => {
          const name = read(`variables.${variable.id}.name`, variable.name);
          const help = read(`variables.${variable.id}.help`, variable.help);
          const value = valueText(variable.valueType, variable.default, i18n.language);
          const hover = [help, t("blueprint.default", { value })].filter(Boolean).join("\n");
          return (
            <li key={variable.id} title={hover} className="mx-1 flex items-center gap-2 px-2 py-1">
              <span aria-hidden="true" className={cn("blueprint-chip-dot", `blueprint-type-${variable.valueType}`)} />
              <span className="min-w-0 flex-1 truncate">{name}</span>
              {variable.perCharacter && (
                <Eye aria-hidden="true" size={14} strokeWidth={1.5} className="text-text-muted shrink-0" />
              )}
              <span className="sr-only">
                {[variable.perCharacter ? t("blueprint.perCharacter") : null, hover].filter(Boolean).join(". ")}
              </span>
            </li>
          );
        })}
      </Section>
    </nav>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section>
      <h3 className="text-text-muted flex h-[22px] items-center px-3 text-[11px] font-semibold tracking-[.04em] uppercase">
        {title}
      </h3>
      <ul className="flex flex-col gap-px">{children}</ul>
    </section>
  );
}
