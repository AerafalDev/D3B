import { Handle, type Node, type NodeProps, Position } from "@xyflow/react";
import { useTranslation } from "react-i18next";

import type { PinView_Serialize } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";
import { FAMILY_COLOUR, nodeIcon } from "./graph-look.ts";
import type { GraphNode, SentencePart } from "./node-sentence.ts";

/** What a node of the graph carries for React Flow: the IPC's node and its sentence, filled. */
export type BoxData = { node: GraphNode; sentence: SentencePart[] };

/** A node as React Flow holds it. */
export type BoxNode = Node<BoxData, "box">;

/** A pin's label, `node.pins.<pin>`, or a function's parameter's id. */
function usePinLabel(): (pin: string) => string {
  const { t, i18n } = useTranslation();
  return (pin) => (i18n.exists(`node.pins.${pin}`) ? String(t(`node.pins.${pin}` as never)) : pin);
}

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
  const label = usePinLabel();
  const { node, sentence } = data;
  const Icon = nodeIcon(node.family, node.does === "kind" ? node.kind : null);
  const flowIn = node.pins.find((pin) => pin.side === "in" && pin.carries === "exec");
  const flowOuts = node.pins.filter((pin) => pin.side === "out" && pin.carries === "exec");
  const head = flowOuts[0];
  const ins = node.pins.filter((pin) => pin.side === "in" && pin.carries !== "exec");
  const outs = node.pins.filter((pin) => pin.side === "out" && pin !== head);
  return (
    <div className={cn("blueprint-node", selected && "is-selected", node.family === null && "is-unknown")}>
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
                {label(pin.id)}
              </div>
            ))}
          </div>
          <div className="flex flex-col items-end">
            {outs.map((pin) => (
              <div key={pin.id} className={cn("blueprint-row", pin.id === "failed" && "is-failed")}>
                {label(pin.id)}
                <Handle type="source" position={Position.Right} id={pin.id} className={pinClass(pin)} />
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

/** What a variable's pill carries: its name and type. */
export type PillData = { name: string; valueType: string };

/** A variable read in the graph, as Unreal's "Get": a pill of its type's colour. */
export function VariablePill({ data, selected }: NodeProps<Node<PillData, "pill">>) {
  return (
    <div
      className={cn("blueprint-pill", `blueprint-type-${data.valueType}`, selected && "is-selected")}
      title={data.name}
    >
      <span className={cn("blueprint-chip-dot", `blueprint-type-${data.valueType}`)} />
      {data.name}
      <Handle
        type="source"
        position={Position.Right}
        id="value"
        className={cn("blueprint-pin blueprint-value", `blueprint-type-${data.valueType}`)}
      />
    </div>
  );
}
