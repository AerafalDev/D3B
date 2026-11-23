import { Handle, type Node, type NodeProps, Position } from "@xyflow/react";
import { Plus } from "lucide-react";
import { useContext } from "react";
import { useTranslation } from "react-i18next";

import { nodeWord } from "@/i18n/node-words.ts";
import { cn } from "@/lib/utils.ts";
import { valueText } from "@/library/value-text.ts";
import { addedPin, NodeEdit } from "../edit/node-options.ts";
import { FAMILY_COLOUR, nodeIcon } from "../graph-look.ts";
import type { GraphNode, SentencePart } from "../node-sentence.ts";
import { pinLabel } from "../pin-label.ts";
import { pinClass } from "./pin-class.ts";

/** What a value node carries for React Flow: the IPC's node, its sentence, and its inputs a wire feeds. */
export type ValueData = { node: GraphNode; sentence: SentencePart[]; wired: string[]; faulty?: boolean };

/** A value node as React Flow holds it. */
export type ValueNodeType = Node<ValueData, "value">;

/** The operators drawn in the middle of their node, whose inputs need no labels: they play the same part. */
const SYMBOL: Record<string, string> = {
  less: "<",
  less_or_equal: "≤",
  greater: ">",
  greater_or_equal: "≥",
  equal: "=",
  not_equal: "≠",
  equal_lists: "=",
  add: "+",
  subtract: "−",
  multiply: "×",
  divide: "÷",
  min: "min",
  max: "max",
};

/** The kinds said by a word of their own in the middle, in the player's language: AND, OR. */
const WORDED = new Set(["and", "or", "make_list"]);

/**
 * A value drawn compact (the owner, 2026-11-17, from a mock-up, as Unreal's pure nodes): no head, its operator or its
 * word or its icon in the middle, its inputs on the left, labelled only where their parts differ, a value typed or
 * a default shown as a chip beside its pin, one output on the right; its whole sentence as its name and hover text.
 */
export function ValueNode({ data, selected }: NodeProps<ValueNodeType>) {
  const { t, i18n } = useTranslation();
  const { node, sentence, wired } = data;
  const kind = node.does === "kind" ? node.kind : "";
  const said = sentence.map((part) => ("text" in part ? part.text : part.chip)).join("");
  const ins = node.pins.filter((pin) => pin.side === "in" && pin.carries !== "exec");
  const out = node.pins.find((pin) => pin.side === "out");
  const symbol = SYMBOL[kind];
  const worded = WORDED.has(kind) ? (nodeWord(`kinds.${kind}.word`) ?? kind) : undefined;
  const labelled = symbol === undefined && !WORDED.has(kind);
  const Icon = nodeIcon(node.family, kind);
  const open = (node.options?.valueType ?? null) === null;
  const onEdit = useContext(NodeEdit);
  const added = onEdit === undefined ? null : addedPin(node);
  return (
    <div
      title={said}
      className={cn("blueprint-node blueprint-compact", selected && "is-selected", data.faulty && "is-faulty")}
    >
      {data.faulty && <span className="sr-only">{t("blueprint.faulty")}</span>}
      <div className="flex flex-col">
        {ins.map((pin) => {
          const json = wired.includes(pin.id) ? null : (node.inputs[pin.id] ?? pin.default ?? null);
          return (
            <div key={pin.id} className="blueprint-row">
              <Handle type="target" position={Position.Left} id={pin.id} className={pinClass(pin, open)} />
              {labelled && <span>{pinLabel(pin.id, node)}</span>}
              {json !== null && <span className="blueprint-chip">{valueText(pin.carries, json, i18n.language)}</span>}
            </div>
          );
        })}
        {added !== null && (
          <button
            type="button"
            // A click or a key adds the input and neither drags nor chooses the node.
            className="nodrag nopan nokey text-text-muted hover:bg-hover focus-visible:outline-focus flex items-center gap-1 rounded px-1 text-[11px] hover:text-white focus-visible:outline-1"
            onClick={(event) => {
              event.stopPropagation();
              onEdit?.(added);
            }}
          >
            <Plus aria-hidden="true" size={12} strokeWidth={1.5} />
            {t("blueprint.addInput")}
          </button>
        )}
      </div>
      <div className="blueprint-compact-op" aria-hidden="true">
        {symbol ?? worded ?? <Icon size={14} strokeWidth={1.5} color={FAMILY_COLOUR[node.family ?? "value"]} />}
      </div>
      {out !== undefined && (
        <div className="blueprint-row">
          <Handle type="source" position={Position.Right} id={out.id} className={pinClass(out, open)} />
        </div>
      )}
    </div>
  );
}
