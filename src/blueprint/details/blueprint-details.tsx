import { useTranslation } from "react-i18next";

import type { GraphView_Serialize, ProblemView } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";
import { givenText } from "@/library/given-text.ts";
import type { BlueprintSummary } from "@/library/library-store.ts";
import { valueText } from "@/library/value-text.ts";
import { type GraphNode, graphSentences } from "../node-sentence.ts";
import { pinLabel } from "../pin-label.ts";
import { DetailRow } from "./detail-row.tsx";
import { EventDetails } from "./event-details.tsx";
import { problemText } from "./problem-text.ts";

/** What Details shows: nothing chosen, a node of the graph shown, or a variable. */
export type Selection = { kind: "node"; id: string } | { kind: "variable"; id: string } | null;

interface BlueprintDetailsProps {
  blueprint: BlueprintSummary;
  /** All the blueprint's graphs, for the nodes that use a variable. */
  graphs: readonly GraphView_Serialize[];
  /** The graph shown, whose node may be chosen. */
  graph: GraphView_Serialize | undefined;
  problems: readonly ProblemView[];
  selection: Selection;
  names: ReadonlyMap<string, string>;
}

/**
 * The editor's Details, Unreal's, in a column on the tab's right (the owner, 2026-11-14): the node or the variable
 * chosen, else the blueprint's problems. Read-only until step 3's PR 13.
 */
export function BlueprintDetails({ blueprint, graphs, graph, problems, selection, names }: BlueprintDetailsProps) {
  const { t } = useTranslation();
  const node = selection?.kind === "node" ? graph?.nodes.find((one) => one.id === selection.id) : undefined;
  const variable =
    selection?.kind === "variable" ? blueprint.variables.find((one) => one.id === selection.id) : undefined;

  return (
    <aside
      aria-label={t("blueprint.details.title")}
      className="border-line flex min-h-0 flex-col gap-3 overflow-y-auto border-l px-3 py-3 text-xs"
    >
      <h3 className="text-text-muted text-[11px] font-semibold tracking-[.04em] uppercase">
        {t("blueprint.details.title")}
      </h3>
      {node !== undefined && graph !== undefined ? (
        <NodeDetails
          node={node}
          graph={graph}
          problems={problems.filter((one) => one.node === node.id && one.graph === graph.id)}
          names={names}
        />
      ) : variable !== undefined ? (
        <VariableDetails blueprint={blueprint} variable={variable} graphs={graphs} />
      ) : (
        <>
          <p className="text-text-muted">{t("blueprint.details.nothing")}</p>
          <Problems problems={problems} names={names} graphs={graphs} />
        </>
      )}
    </aside>
  );
}

interface ProblemsProps {
  problems: readonly ProblemView[];
  names: ReadonlyMap<string, string>;
  /** When given, a node's problem is said after its node's sentence, so the player finds it. */
  graphs?: readonly GraphView_Serialize[];
}

function Problems({ problems, names, graphs }: ProblemsProps) {
  const { t } = useTranslation();
  if (problems.length === 0) {
    return null;
  }
  return (
    <DetailRow label={t("blueprint.details.problems")}>
      <ul className="flex flex-col gap-1 text-[#d29922]">
        {problems.map((problem, index) => (
          // A problem has no id of its own; the list is never reordered.
          // biome-ignore lint/suspicious/noArrayIndexKey: the problems' order is the file's
          <li key={index}>
            {[graphs === undefined ? null : nodeTitle(graphs, problem, names), problemText(problem, names)]
              .filter(Boolean)
              .join(" : ")}
          </li>
        ))}
      </ul>
    </DetailRow>
  );
}

/** The sentence of the node a problem is on, among `graphs`, or `null`. */
function nodeTitle(graphs: readonly GraphView_Serialize[], problem: ProblemView, names: ReadonlyMap<string, string>) {
  const graph = graphs.find((one) => one.id === problem.graph);
  if (graph === undefined || problem.node == null) {
    return null;
  }
  const parts = graphSentences(graph, names).get(problem.node);
  return parts === undefined ? null : parts.map((part) => ("text" in part ? part.text : part.chip)).join("");
}

interface NodeDetailsProps {
  node: GraphNode;
  graph: GraphView_Serialize;
  problems: readonly ProblemView[];
  names: ReadonlyMap<string, string>;
}

function NodeDetails({ node, graph, problems, names }: NodeDetailsProps) {
  const { t, i18n } = useTranslation();
  const kind = node.does === "kind" ? node.kind : null;
  const description =
    kind === null
      ? null
      : i18n.exists(`node.kinds.${kind}.description`)
        ? String(t(`node.kinds.${kind}.description` as never))
        : null;
  // Its sentence as the graph says it, words and values in one line.
  const title = (graphSentences(graph, names).get(node.id) ?? [])
    .map((part) => ("text" in part ? part.text : part.chip))
    .join("");
  const wired = new Set(graph.wires.filter((wire) => wire.to[0] === node.id).map((wire) => wire.to[1]));
  const inputs = node.pins.filter((pin) => pin.side === "in" && pin.carries !== "exec");
  const failedFree =
    node.pins.some((pin) => pin.id === "failed") &&
    !graph.wires.some((wire) => wire.from[0] === node.id && wire.from[1] === "failed");

  return (
    <>
      <div className="text-sm text-white">{title}</div>
      {description !== null && <p className="text-text-muted">{description}</p>}
      {node.fromStep !== null && (
        <p className="text-text-faint">{t("blueprint.details.fromStep", { step: node.fromStep })}</p>
      )}
      {inputs.length > 0 && (
        <DetailRow label={t("blueprint.details.inputs")}>
          <dl className="grid grid-cols-[1fr_auto] gap-y-1">
            {inputs.map((pin) => (
              <div key={pin.id} className="contents">
                <dt className="text-text-muted">{pinLabel(pin.id)}</dt>
                <dd className={cn(wired.has(pin.id) && "text-text-faint")}>
                  {wired.has(pin.id)
                    ? t("blueprint.details.wired")
                    : valueText(pin.carries, node.inputs[pin.id] ?? pin.default ?? null, i18n.language)}
                </dd>
              </div>
            ))}
          </dl>
        </DetailRow>
      )}
      {failedFree && (
        <DetailRow label={t("blueprint.details.ifFailed")}>{t("blueprint.details.failedBlocks")}</DetailRow>
      )}
      {node.family === "event" && <EventDetails event={node.event} />}
      <Problems problems={problems} names={names} />
    </>
  );
}

interface VariableDetailsProps {
  blueprint: BlueprintSummary;
  variable: BlueprintSummary["variables"][number];
  graphs: readonly GraphView_Serialize[];
}

function VariableDetails({ blueprint, variable, graphs }: VariableDetailsProps) {
  const { t, i18n } = useTranslation();
  const read = (path: string, fallback: string) =>
    blueprint.given ? givenText(blueprint.id, path, fallback) : fallback;
  const usedBy = graphs
    .flatMap((one) => one.nodes)
    .filter((node) => (node.does === "get" || node.does === "set") && node.variable === variable.id).length;
  return (
    <>
      <div className="flex items-center gap-2 text-sm text-white">
        <span aria-hidden="true" className={cn("blueprint-chip-dot", `blueprint-type-${variable.valueType}`)} />
        {read(`variables.${variable.id}.name`, variable.name)}
      </div>
      <DetailRow label={t("blueprint.details.type")}>{t(`blueprint.type.${variable.valueType}` as never)}</DetailRow>
      <DetailRow label={t("blueprint.details.default")}>
        {valueText(variable.valueType, variable.default, i18n.language)}
      </DetailRow>
      {variable.bounds !== null && (
        <DetailRow label={t("blueprint.details.bounds")}>
          {t("blueprint.details.boundsValue", { min: variable.bounds[0], max: variable.bounds[1] })}
        </DetailRow>
      )}
      <DetailRow label={t("blueprint.details.perCharacter")}>
        {t(variable.perCharacter ? "blueprint.details.perCharacterYes" : "blueprint.details.perCharacterNo")}
      </DetailRow>
      {variable.perCharacter && variable.help !== "" && (
        <DetailRow label={t("blueprint.details.help")}>
          {read(`variables.${variable.id}.help`, variable.help)}
        </DetailRow>
      )}
      <DetailRow label={t("blueprint.details.duration")}>
        {t(variable.memory ? "blueprint.details.memory" : "blueprint.details.oneRun")}
      </DetailRow>
      <DetailRow label={t("blueprint.details.usedBy")}>{t("blueprint.details.nodes", { count: usedBy })}</DetailRow>
    </>
  );
}
