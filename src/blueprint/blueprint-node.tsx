import { Handle, type Node, type NodeProps, Position } from "@xyflow/react";
import { useTranslation } from "react-i18next";

import type { PinView_Serialize } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";
import { FAMILY_COLOUR, nodeIcon } from "./graph-look.ts";
import type { GraphNode, SentencePart } from "./node-sentence.ts";
import { pinLabel } from "./pin-label.ts";

/** What a node of the graph carries for React Flow: the IPC's node and its sentence, filled. */
export type BoxData = { node: GraphNode; sentence: SentencePart[]; faulty?: boolean };

/** A node as React Flow holds it. */
export type BoxNode = Node<BoxData, "box">;

function pinClass(pin: PinView_Serialize): string {
  return cn(
    "blueprint-pin",
    pin.carries === "exec" ? "blueprint-exec" : "blueprint-value",
    `blueprint-type-${pin.carries}`,
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
  const head = flowOuts[0];
  const ins = node.pins.filter((pin) => pin.side === "in" && pin.carries !== "exec");
  const outs = node.pins.filter((pin) => pin.side === "out" && pin !== head);
  return (
    <div
      className={cn("blueprint-node", selected && "is-selected", (node.family === null || data.faulty) && "is-unknown")}
    >
      <div className="blueprint-node-head">
        {flowIn !== undefined && (
          <Handle type="target" position={Position.Left} id="run" className={pinClass(flowIn)} />
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
        {node.fromStep !== null && (
          <span className="blueprint-step">{t("blueprint.fromStep", { step: node.fromStep })}</span>
        )}
        {head !== undefined && (
          <Handle type="source" position={Position.Right} id={head.id} className={pinClass(head)} />
        )}
      </div>
      {(ins.length > 0 || outs.length > 0) && (
        <div className="blueprint-node-body">
          <div className="flex flex-col">
            {ins.map((pin) => (
              <div key={pin.id} className="blueprint-row">
                <Handle type="target" position={Position.Left} id={pin.id} className={pinClass(pin)} />
                {pinLabel(pin.id)}
              </div>
            ))}
          </div>
          <div className="flex flex-col items-end">
            {outs.map((pin) => (
              <div key={pin.id} className={cn("blueprint-row", pin.id === "failed" && "is-failed")}>
                {pinLabel(pin.id)}
                <Handle type="source" position={Position.Right} id={pin.id} className={pinClass(pin)} />
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
