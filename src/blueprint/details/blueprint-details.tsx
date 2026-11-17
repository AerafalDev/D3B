import { useTranslation } from "react-i18next";

import type { GraphView_Serialize, ProblemView, VariableChange } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";
import { givenText } from "@/library/given-text.ts";
import type { BlueprintSummary } from "@/library/library-store.ts";
import { shapedText, valueText } from "@/library/value-text.ts";
import { type GraphNode, graphSentences } from "../node-sentence.ts";
import { pinLabel } from "../pin-label.ts";
import { DetailRow } from "./detail-row.tsx";
import { EventDetails } from "./event-details.tsx";
import { InputRow } from "./input-row.tsx";
import { problemText } from "./problem-text.ts";
import { VariableEditor } from "./variable-editor.tsx";

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
  /**
   * Types a value on the input `pin` of the node `node` of the graph shown, as JSON, or none for its default; given for
   * a blueprint of mine only (the owner, 2026-11-17). Whether it was saved.
   */
  onValue?: (node: string, pin: string, value: string | null, of?: string) => Promise<boolean>;
  /** Changes a variable of mine, whole, saved at once (step 3's (14)); given for a blueprint of mine only. */
  onVariable?: (change: VariableChange) => Promise<boolean>;
  /** Deletes a variable of mine with its nodes. */
  onDeleteVariable?: (variable: string) => void;
}

/**
 * The editor's Details, Unreal's, in a column on the tab's right (the owner, 2026-11-14): the node or the variable
 * chosen, else the blueprint's problems; in a blueprint of mine, the values typed on a node's inputs are edited there.
 */
export function BlueprintDetails({
  blueprint,
  graphs,
  graph,
  problems,
  selection,
  names,
  onValue,
  onVariable,
  onDeleteVariable,
}: BlueprintDetailsProps) {
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
          variables={blueprint.variables}
          onValue={onValue === undefined ? undefined : (pin, value, of) => onValue(node.id, pin, value, of)}
        />
      ) : variable !== undefined ? (
        onVariable !== undefined ? (
          <VariableEditor
            // Another variable chosen keeps no draft of the one before.
            key={variable.id}
            variable={variable}
            usedBy={usedBy(graphs, variable.id)}
            onChange={onVariable}
            onDelete={() => onDeleteVariable?.(variable.id)}
          />
        ) : (
          <VariableDetails blueprint={blueprint} variable={variable} graphs={graphs} />
        )
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
  /** The blueprint's variables, whose bounds a Set's value keeps. */
  variables: BlueprintSummary["variables"];
  onValue: ((pin: string, value: string | null, of?: string) => Promise<boolean>) | undefined;
}

/** The types a wildcard of a kind that lists none may take: every type a value is typed as, the NPC at step 4. */
const ANY_TYPE = [
  "bool",
  "number",
  "percentage",
  "text",
  "zone",
  "route",
  "bank",
  "items",
  "job",
  "map",
  "item",
  "duration",
  "time",
];

/**
 * The types the input `pin` of `node` may be typed as, when it is a wildcard still open, as `bot::pins::open_pins`
 * decides: its node notes no type, changes no variable (typed by it), and no other wildcard of it is wired or holds a
 * value, which would read it as the kind's first type already; `undefined` otherwise.
 */
function openTo(node: GraphNode, pin: string, graph: GraphView_Serialize): readonly string[] | undefined {
  const at = node.pins.find((one) => one.id === pin && one.side === "in");
  const wildcard = at?.carries === "wildcard" || at?.wildcard === true;
  if (!wildcard || node.options?.valueType !== undefined || node.options?.variable !== undefined) {
    return undefined;
  }
  const used = node.pins.some(
    (one) =>
      one.wildcard === true &&
      !(one.id === pin && one.side === "in") &&
      (node.inputs[one.id] !== undefined ||
        graph.wires.some(({ from, to }) =>
          one.side === "in" ? to[0] === node.id && to[1] === one.id : from[0] === node.id && from[1] === one.id,
        )),
  );
  if (used) {
    return undefined;
  }
  return (node.types ?? []).length > 0 ? (node.types ?? []) : ANY_TYPE;
}
function NodeDetails({ node, graph, problems, names, variables, onValue }: NodeDetailsProps) {
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
      {inputs.length > 0 && onValue !== undefined && (
        <DetailRow label={t("blueprint.details.inputs")}>
          <div className="flex flex-col gap-2">
            {inputs.map((pin) =>
              wired.has(pin.id) ? (
                <div key={pin.id} className="flex justify-between">
                  <span className="text-text-muted">{pinLabel(pin.id)}</span>
                  <span className="text-text-faint">{t("blueprint.details.wired")}</span>
                </div>
              ) : (
                <InputRow
                  // A node chosen in its place keeps no draft of the one before.
                  key={`${node.id}.${pin.id}`}
                  pin={pin}
                  typed={node.inputs[pin.id]}
                  type={{
                    valueType: pin.carries,
                    bounds:
                      node.does === "set" ? (variables.find((one) => one.id === node.variable)?.bounds ?? null) : null,
                  }}
                  who={title}
                  // A wildcard its node notes no type for offers the types it may take (the owner, 2026-11-17).
                  // A wildcard its node notes no type for offers the types it may take (the owner, 2026-11-17).
                  open={openTo(node, pin.id, graph)}
                  onValue={(value, of) => onValue(pin.id, value, of)}
                />
              ),
            )}
          </div>
        </DetailRow>
      )}
      {inputs.length > 0 && onValue === undefined && (
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
  return (
    <>
      <div className="flex items-center gap-2 text-sm text-white">
        <span aria-hidden="true" className={cn("blueprint-chip-dot", `blueprint-type-${variable.valueType}`)} />
        {read(`variables.${variable.id}.name`, variable.name)}
      </div>
      <DetailRow label={t("blueprint.details.type")}>{t(`blueprint.type.${variable.valueType}` as never)}</DetailRow>
      <DetailRow label={t("blueprint.details.default")}>
        {shapedText(variable, variable.default, i18n.language)}
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
      <DetailRow label={t("blueprint.details.usedBy")}>
        {t("blueprint.details.nodes", { count: usedBy(graphs, variable.id) })}
      </DetailRow>
    </>
  );
}

/** How many nodes of `graphs` read or write the variable `id`. */
function usedBy(graphs: readonly GraphView_Serialize[], id: string) {
  return graphs
    .flatMap((one) => one.nodes)
    .filter((node) => (node.does === "get" || node.does === "set") && node.variable === id).length;
}
