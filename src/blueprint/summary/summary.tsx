import "../blueprint-graph.css";

import i18next from "i18next";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

import type { GraphView_Serialize } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";
import { FAMILY_COLOUR, nodeIcon } from "../graph-look.ts";
import { type GraphNode, graphSentences, type SentencePart } from "../node-sentence.ts";
import { pinLabel } from "../pin-label.ts";
import { type SummaryFlow, summaryFlow } from "./summary-flow.ts";
import { onPin } from "./summary-pins.ts";

/**
 * The events the game forces, said with their default when the blueprint does not handle them (`docs/bot.md`): the
 * catalogue's forced events (`bot::catalogue`, `EventRole`), which the IPC does not carry, and the step that brings the
 * default fight. A forced event added there is added here.
 */
const FORCED = [
  { kind: "fight_starts", fromStep: 6 },
  { kind: "died", fromStep: null },
  { kind: "action_failed", fromStep: null },
] as const;

interface SummaryProps {
  graphs: readonly GraphView_Serialize[];
  /** The blueprint's variables' and functions' names by id, as the player reads them. */
  names: ReadonlyMap<string, string>;
  /** The node Details shows, its step marked chosen. */
  chosen: { graph: string; node: string } | null;
  /** A step chosen, so Details shows its node, in its graph. */
  onChoose: (graph: string, node: string) => void;
}

/**
 * The blueprint read as sentences, the graph's other form (`docs/bot.md`, "In the app"; the prototype the owner
 * approved on 2026-11-13): a section per event, "On start" first and the others by priority, with their mode; its
 * steps numbered as the flow runs them, each output but the one a step goes on by under it by its role (a Failed, an
 * If's, a Sequence's outputs, a Switch's on one line, `summary-pins.ts`), a loop back to its number; the forced
 * events' defaults when the blueprint leaves them; then each function from its entry.
 */
export function Summary({ graphs, names, chosen, onChoose }: SummaryProps) {
  const { t } = useTranslation();
  const events = graphs.filter((graph) => !graph.function);
  const held = new Set(
    events.flatMap((graph) => graph.nodes.flatMap((node) => (node.does === "kind" ? [node.kind] : []))),
  );
  // Every event graph's events, ordered once, so "On start" comes first whichever graph holds it.
  const sections = events
    .flatMap((graph) => {
      const sentences = graphSentences(graph, names);
      return graph.nodes.filter((node) => node.family === "event").map((node) => ({ graph, node, sentences }));
    })
    .sort((a, b) => rank(a.node) - rank(b.node));
  return (
    <div className="flex min-h-0 flex-col gap-4 overflow-y-auto px-7 py-5 text-[13px]">
      {sections.map(({ graph, node, sentences }) => (
        <section key={`${graph.id}/${node.id}`}>
          <Heading node={node}>
            <Sentence parts={sentences.get(node.id) ?? []} />
            {node.event !== null && (
              <>
                {" · "}
                {t("blueprint.summary.priority", { priority: node.event.priority })}
                {" · "}
                {t(`blueprint.details.mode.${node.event.mode}`)}
              </>
            )}
          </Heading>
          <Flow
            flow={summaryFlow(graph, node.id, "then")}
            sentences={sentences}
            chosen={chosen?.graph === graph.id ? chosen.node : null}
            onChoose={(node) => onChoose(graph.id, node)}
          />
        </section>
      ))}
      {FORCED.filter((forced) => !held.has(forced.kind)).map((forced) => (
        <section key={forced.kind}>
          <Heading icon={forced.kind}>
            {kindSentence(forced.kind)}
            {" · "}
            {t("blueprint.summary.byDefault")}
          </Heading>
          <p className="m-0 flex items-center gap-2">
            {t(`blueprint.summary.default.${forced.kind}`)}
            {forced.fromStep !== null && <StepTag step={forced.fromStep} />}
          </p>
        </section>
      ))}
      {graphs
        .filter((graph) => graph.function)
        .map((graph) => {
          const sentences = graphSentences(graph, names);
          const entry = graph.nodes.find((node) => node.does === "kind" && node.kind === "entry");
          return (
            <section key={graph.id}>
              <Heading icon="entry">
                {t("blueprint.summary.function", { name: names.get(graph.id) ?? graph.name })}
              </Heading>
              <Flow
                flow={entry === undefined ? { steps: [], jump: null } : summaryFlow(graph, entry.id, "then")}
                sentences={sentences}
                chosen={chosen?.graph === graph.id ? chosen.node : null}
                onChoose={(node) => onChoose(graph.id, node)}
              />
            </section>
          );
        })}
    </div>
  );
}

/** "On start" first, then by priority, an event without one last. */
function rank(node: GraphNode): number {
  if (node.does === "kind" && node.kind === "on_start") {
    return 0;
  }
  return node.event?.priority ?? Number.MAX_SAFE_INTEGER;
}

/** A kind's sentence alone, for an event the blueprint does not hold. */
function kindSentence(kind: string): string {
  return i18next.exists(`node.kinds.${kind}.sentence`)
    ? String(i18next.t(`node.kinds.${kind}.sentence` as never))
    : kind;
}

function Heading({ node, icon, children }: { node?: GraphNode; icon?: string; children: ReactNode }) {
  const family = node?.family ?? (icon === "entry" ? "function" : "event");
  const kind = node === undefined ? (icon ?? null) : node.does === "kind" ? node.kind : null;
  const Icon = nodeIcon(family, kind);
  return (
    <h4 className="text-text-muted m-0 mb-1.5 flex items-center gap-1.5 text-xs font-medium">
      <Icon aria-hidden="true" size={14} strokeWidth={1.5} color={FAMILY_COLOUR[family]} />
      <span>{children}</span>
    </h4>
  );
}

function Sentence({ parts }: { parts: readonly SentencePart[] }) {
  return (
    <span>
      {parts.map((part, index) =>
        "text" in part ? (
          // biome-ignore lint/suspicious/noArrayIndexKey: a sentence's parts are in a fixed order
          <span key={index}>{part.text}</span>
        ) : (
          // biome-ignore lint/suspicious/noArrayIndexKey: a sentence's parts are in a fixed order
          <span key={index} className="blueprint-chip">
            <span className={cn("blueprint-chip-dot", `blueprint-type-${part.valueType}`)} />
            {part.chip}
          </span>
        ),
      )}
    </span>
  );
}

function StepTag({ step }: { step: number }) {
  const { t } = useTranslation();
  return <span className="text-text-faint text-[11px] whitespace-nowrap">{t("blueprint.fromStep", { step })}</span>;
}

/** The output a flow's last step goes on by, which its jump leaves through. */
function lastOn(flow: SummaryFlow): string | undefined {
  const last = flow.steps.at(-1);
  return last === undefined ? undefined : onPin(last.node);
}

/** Where a listed output leads: its first step, the step it jumps to, or nowhere. */
function target(flow: SummaryFlow): string {
  return flow.steps[0]?.label ?? flow.jump?.to ?? i18next.t("blueprint.summary.nowhere");
}

/** A branch's title, "Si ça échoue :", or for a function's named exit, its name: "Si Plein :". */
function branchTitle(pin: string, node: GraphNode): string {
  // A loop's body says when it runs, by its kind: "À chaque tour :", "Pour chaque élément :".
  const own = node.does === "kind" ? `blueprint.summary.branch.${node.kind}_${pin}` : null;
  if (own !== null && i18next.exists(own)) {
    return String(i18next.t(own as never));
  }
  const key = `blueprint.summary.branch.${pin}`;
  // The pins' ids are data, so their keys are checked at run time rather than typed; a named exit's id may be any,
  // `true` too, and is said by its name.
  const exit = node.exits?.some((one) => one.id === pin) === true;
  return !exit && i18next.exists(key)
    ? String(i18next.t(key as never))
    : i18next.t("blueprint.summary.branchOf", { pin: pinLabel(pin, node) });
}

interface FlowProps {
  flow: SummaryFlow;
  sentences: ReadonlyMap<string, SentencePart[]>;
  chosen: string | null;
  onChoose: (node: string) => void;
}

/** A flow's steps, each its number and its node's sentence, a button that shows the node in Details. */
function Flow({ flow, sentences, chosen, onChoose }: FlowProps) {
  const { t } = useTranslation();
  if (flow.steps.length === 0 && flow.jump === null) {
    return <p className="text-text-muted m-0">{t("blueprint.summary.nothing")}</p>;
  }
  return (
    <ol className="m-0 flex list-none flex-col gap-1 p-0">
      {flow.steps.map((step) => (
        <li key={step.label} className="flex flex-col gap-1">
          <button
            type="button"
            aria-current={step.node.id === chosen}
            onClick={() => onChoose(step.node.id)}
            className={cn(
              "hover:bg-hover focus-visible:outline-focus flex items-center gap-2 rounded px-1 text-left focus-visible:outline-1",
              step.node.id === chosen && "bg-[#2a2c30]",
            )}
          >
            {/* The spaces between the parts are said to screen readers, the gaps being only drawn. */}
            <span className="text-text-muted w-8 shrink-0 tabular-nums">{step.label}.</span>{" "}
            <Sentence parts={sentences.get(step.node.id) ?? []} />
            {step.node.fromStep !== null && (
              <>
                {" "}
                <StepTag step={step.node.fromStep} />
              </>
            )}
          </button>
          {step.branches.some((branch) => branch.role === "listed") && (
            // A Switch's outputs on one line, each to the step it leads to (the owner, 2026-11-17, on the mock-up).
            <p className="text-text-muted m-0 pl-10">
              {step.branches
                .filter((branch) => branch.role === "listed")
                .map((branch) =>
                  t("blueprint.summary.to", { pin: pinLabel(branch.pin, step.node), step: target(branch.flow) }),
                )
                .join(" · ")}
            </p>
          )}
          {step.branches
            // A listed output leading to no step of its own is all said on its line.
            .filter((branch) => branch.role !== "listed" || branch.flow.steps.length > 0)
            .map((branch) => (
              <div key={branch.pin} className="flex flex-col gap-1 pl-10">
                {branch.role === "titled" && (
                  <span className="text-text-muted">{branchTitle(branch.pin, step.node)}</span>
                )}
                {branch.role === "untitled" && branch.flow.steps.length === 0 ? (
                  // A Sequence's output wired straight to a step said already names its output.
                  <p className="text-text-muted m-0 pl-1">
                    {t("blueprint.summary.outTo", { pin: pinLabel(branch.pin, step.node), step: target(branch.flow) })}
                  </p>
                ) : (
                  <Flow flow={branch.flow} sentences={sentences} chosen={chosen} onChoose={onChoose} />
                )}
              </div>
            ))}
        </li>
      ))}
      {flow.jump !== null && (
        <li className="text-text-muted px-1 pl-11">
          {/* Out of a loop's Completed, the jump says what follows it (the owner, 2026-11-17, on the mock-up). */}
          {lastOn(flow) === "completed"
            ? t("blueprint.summary.afterLoop", { step: flow.jump.to })
            : t(flow.jump.back ? "blueprint.summary.back" : "blueprint.summary.on", { step: flow.jump.to })}
        </li>
      )}
    </ol>
  );
}
