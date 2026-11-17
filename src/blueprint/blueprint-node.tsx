import { Handle, type Node, type NodeProps, Position } from "@xyflow/react";
import { Plus } from "lucide-react";
import { useContext } from "react";
import { useTranslation } from "react-i18next";

import type { PinView_Serialize } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";
import { addedPin, NodeEdit } from "./edit/node-options.ts";
import { FAMILY_COLOUR, nodeIcon } from "./graph-look.ts";
import type { GraphNode, SentencePart } from "./node-sentence.ts";
import { pinLabel } from "./pin-label.ts";

/** What a node of the graph carries for React Flow: the IPC's node and its sentence, filled. */
export type BoxData = { node: GraphNode; sentence: SentencePart[]; faulty?: boolean };

/** A node as React Flow holds it. */
export type BoxNode = Node<BoxData, "box">;

/**
 * A pin's look (the owner, 2026-11-17, from a mock-up): a triangle for the flow, a circle for a value coloured by its
 * type, a filled square for a list and a hollow one for ranges; a wildcard its node notes no type for a hollow grey
 * circle (a square for a list), drawn as its type once typed.
 */
function pinClass(pin: PinView_Serialize, open: boolean): string {
  return cn(
    "blueprint-pin",
    pin.carries === "exec" ? "blueprint-exec" : "blueprint-value",
    `blueprint-type-${pin.carries}`,
    pin.list === true && "blueprint-list",
    pin.ranges === true && "blueprint-ranges",
    (pin.carries === "wildcard" || (open && pin.wildcard === true)) && "blueprint-open",
  );
}

/**
 * A node, the look the owner approved on the prototype (2026-11-13): a sober box, its family's icon coloured, its
 * sentence with the values in chips, its flow in and first flow out on its head, its other inputs on the left and
 * outputs on the right, a triangle for the flow and a circle for a value, coloured by type; an action the bot does not
 * do yet tagged with its step (the owner, 2026-11-14).
 */
export function BlueprintNode({ data, selected }: NodeProps<BoxNode>) {
  const { t } = useTranslation();
  const { node, sentence } = data;
  const Icon = nodeIcon(node.family, node.does === "kind" ? node.kind : null);
  const flowIn = node.pins.find((pin) => pin.side === "in" && pin.carries === "exec");
  const flowOuts = node.pins.filter((pin) => pin.side === "out" && pin.carries === "exec");
  // A node's numbered outputs and cases are rows, each with its label, never one alone on its head.
  const numbered = (node.adds ?? null) !== null || node.takesCases === true;
  const head = numbered ? undefined : flowOuts[0];
  const onEdit = useContext(NodeEdit);
  const added = onEdit === undefined ? null : addedPin(node);
  const adding = (side: "in" | "out") =>
    added !== null &&
    node.adds?.side === side && (
      <button
        type="button"
        // A click or a key adds the pin and neither drags nor chooses the node.
        className="nodrag nopan nokey text-text-muted hover:bg-hover focus-visible:outline-focus flex items-center gap-1 rounded px-1 text-[11px] hover:text-white focus-visible:outline-1"
        onClick={(event) => {
          event.stopPropagation();
          onEdit?.(added);
        }}
      >
        <Plus aria-hidden="true" size={12} strokeWidth={1.5} />
        {t(side === "in" ? "blueprint.addInput" : "blueprint.addOutput")}
      </button>
    );
  // A second flow input, Do N times' Reset, is a row with the values.
  const ins = node.pins.filter((pin) => pin.side === "in" && pin !== flowIn);
  const outs = node.pins.filter((pin) => pin.side === "out" && pin !== head);
  // A kind that changes a variable is typed by it, never open (`bot::pins`).
  const open = (node.options?.valueType ?? null) === null && node.options?.variable === undefined;
  return (
    <div
      className={cn(
        "blueprint-node",
        selected && "is-selected",
        node.family === null && "is-unknown",
        data.faulty && "is-faulty",
      )}
    >
      <div className="blueprint-node-head">
        {flowIn !== undefined && (
          <Handle type="target" position={Position.Left} id="run" className={pinClass(flowIn, open)} />
        )}
        <Icon
          aria-hidden="true"
          size={14}
          strokeWidth={1.5}
          color={FAMILY_COLOUR[node.family ?? "flow"]}
          className="shrink-0"
        />
        <span className="blueprint-node-title">
          {sentence.map((part, index) =>
            "text" in part ? (
              // The sentence's parts never move: their place is their identity.
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
        {data.faulty && <span className="sr-only">{t("blueprint.faulty")}</span>}
        {node.fromStep !== null && (
          <span className="blueprint-step">{t("blueprint.fromStep", { step: node.fromStep })}</span>
        )}
        {head !== undefined && (
          <Handle type="source" position={Position.Right} id={head.id} className={pinClass(head, open)} />
        )}
      </div>
      {(ins.length > 0 || outs.length > 0 || added !== null) && (
        <div className="blueprint-node-body">
          <div className="flex flex-col">
            {ins.map((pin) => (
              <div key={pin.id} className="blueprint-row">
                <Handle type="target" position={Position.Left} id={pin.id} className={pinClass(pin, open)} />
                {pinLabel(pin.id, node)}
              </div>
            ))}
            {adding("in")}
          </div>
          <div className="flex flex-col items-end">
            {outs.map((pin) => (
              <div key={pin.id} className={cn("blueprint-row", pin.id === "failed" && "is-failed")}>
                {pinLabel(pin.id, node)}
                <Handle type="source" position={Position.Right} id={pin.id} className={pinClass(pin, open)} />
              </div>
            ))}
            {adding("out")}
          </div>
        </div>
      )}
    </div>
  );
}
