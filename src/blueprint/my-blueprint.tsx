import { useDraggable } from "@dnd-kit/core";
import { Eye, Plus } from "lucide-react";
import { useTranslation } from "react-i18next";

import type { GraphView_Serialize } from "@/bindings.ts";
import { Icon } from "@/icons/icon.tsx";
import { cn } from "@/lib/utils.ts";
import { givenText } from "@/library/given-text.ts";
import type { BlueprintSummary } from "@/library/library-store.ts";
import { valueText } from "@/library/value-text.ts";
import type { VariableDrag } from "./edit/variable-drop.ts";

interface MyBlueprintProps {
  blueprint: BlueprintSummary;
  graphs: readonly GraphView_Serialize[];
  /** The graph the canvas shows. */
  shown: string;
  onShow: (graph: string) => void;
  /** The variable Details shows, if one. */
  chosen?: string | null;
  onChoose?: (variable: string) => void;
  /** Creates a variable, in a blueprint of mine only, whose variables are then dragged onto the graph. */
  onAddVariable?: () => void;
}

/**
 * Unreal's "My Blueprint" (`docs/bot.md`, "In the app"): the blueprint's event graphs, its functions and its
 * variables, an eye on those set per character, each with its type's colour; a variable's help and default in its
 * hover and said to screen readers. A graph or a function shows on the canvas when chosen. In a blueprint of mine, "+"
 * creates a variable and a variable dragged onto the graph is read or written there (step 3's (14)).
 */
export function MyBlueprint({
  blueprint,
  graphs,
  shown,
  onShow,
  chosen = null,
  onChoose,
  onAddVariable,
}: MyBlueprintProps) {
  const { t, i18n } = useTranslation();
  const read = (path: string, fallback: string) =>
    blueprint.given ? givenText(blueprint.id, path, fallback) : fallback;
  // A blueprint's one event graph reads as Unreal's "Event Graph" in the player's language; several, and a function,
  // by their names (a given blueprint's translated).
  const eventGraphs = graphs.filter((graph) => !graph.function).length;
  const graphName = (graph: GraphView_Serialize) =>
    !graph.function && eventGraphs === 1 ? t("blueprint.eventGraph") : read(`graphs.${graph.id}`, graph.name);

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
      <Section
        title={t("blueprint.variables")}
        action={
          onAddVariable === undefined ? undefined : (
            <button
              type="button"
              aria-label={t("blueprint.addVariable")}
              title={t("blueprint.addVariable")}
              onClick={onAddVariable}
              className="text-text-muted hover:bg-hover focus-visible:outline-focus flex size-5 items-center justify-center rounded hover:text-white focus-visible:outline-1"
            >
              <Plus aria-hidden="true" size={14} strokeWidth={1.5} />
            </button>
          )
        }
      >
        {blueprint.variables.map((variable) => {
          const name = read(`variables.${variable.id}.name`, variable.name);
          const help = read(`variables.${variable.id}.help`, variable.help);
          const value = valueText(variable.valueType, variable.default, i18n.language);
          const hover = [help, t("blueprint.default", { value })].filter(Boolean).join("\n");
          return (
            <VariableRow
              key={variable.id}
              variable={variable}
              name={name}
              hover={hover}
              chosen={variable.id === chosen}
              draggable={onAddVariable !== undefined}
              onChoose={() => onChoose?.(variable.id)}
            />
          );
        })}
      </Section>
    </nav>
  );
}

interface VariableRowProps {
  variable: BlueprintSummary["variables"][number];
  /** Its name and hover, as the player reads them. */
  name: string;
  hover: string;
  chosen: boolean;
  /** Whether it drags onto the graph: in a blueprint of mine. */
  draggable: boolean;
  onChoose: () => void;
}

/** A variable's row, chosen by a click and, in a blueprint of mine, dragged onto the graph by `@dnd-kit`. */
function VariableRow({ variable, name, hover, chosen, draggable, onChoose }: VariableRowProps) {
  const { t } = useTranslation();
  const data: VariableDrag = { variable: variable.id };
  // The pointer moves a few pixels first (the tab's sensor), so a click still chooses it; the graph's picker reads and
  // writes it by keyboard.
  // Its listeners only: a pointer drags it, and the button keeps its own role and keyboard.
  const { listeners, setNodeRef } = useDraggable({ id: `variable:${variable.id}`, data, disabled: !draggable });
  return (
    <li title={hover}>
      <button
        ref={setNodeRef}
        {...(draggable ? listeners : {})}
        type="button"
        aria-current={chosen ? "true" : undefined}
        data-draggable={draggable ? "true" : undefined}
        onClick={onChoose}
        className="hover:bg-hover focus-visible:outline-focus mx-1 flex w-[calc(100%-0.5rem)] items-center gap-2 rounded-[5px] px-2 py-1 text-left focus-visible:outline-1 aria-[current=true]:bg-[#2a2c30] aria-[current=true]:text-white"
      >
        <span aria-hidden="true" className={cn("blueprint-chip-dot", `blueprint-type-${variable.valueType}`)} />
        <span className="min-w-0 flex-1 truncate">{name}</span>
        {variable.perCharacter && (
          <Eye aria-hidden="true" size={14} strokeWidth={1.5} className="text-text-muted shrink-0" />
        )}
        <span className="sr-only">
          {[variable.perCharacter ? t("blueprint.perCharacter") : null, hover].filter(Boolean).join(". ")}
        </span>
      </button>
    </li>
  );
}

interface SectionProps {
  title: string;
  /** A button on its header's right. */
  action?: React.ReactNode;
  children: React.ReactNode;
}

function Section({ title, action, children }: SectionProps) {
  return (
    <section>
      <div className="flex h-[22px] items-center justify-between pr-2 pl-3">
        <h3 className="text-text-muted text-[11px] font-semibold tracking-[.04em] uppercase">{title}</h3>
        {action}
      </div>
      <ul className="flex flex-col gap-px">{children}</ul>
    </section>
  );
}
