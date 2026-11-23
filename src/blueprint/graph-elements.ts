import type { Edge, NodeChange } from "@xyflow/react";

import type { GraphView_Serialize } from "@/bindings.ts";
import type { BoxNode } from "./node/blueprint-node.tsx";
import type { ValueNodeType } from "./node/value-node.tsx";
import type { PillNode } from "./node/variable-pill.tsx";
import { graphSentences } from "./node-sentence.ts";

// Where a node the file does not place goes: a column on the left, one under the other, far enough apart for a box of
// several rows.
export const UNPLACED_X = -360;
export const UNPLACED_STEP = 200;

/**
 * A graph's nodes and wires as React Flow draws them: each node where the file puts it, else in the column; a get as
 * a pill, a value compact; each wire classed by what its source pin carries, for its colour.
 */
export function graphElements(
  graph: GraphView_Serialize,
  names: ReadonlyMap<string, string>,
  faulty: ReadonlySet<string> = new Set(),
  chosen: string | null = null,
): { nodes: (BoxNode | PillNode | ValueNodeType)[]; edges: Edge[] } {
  let unplaced = 0;
  const byId = new Map(graph.nodes.map((node) => [node.id, node]));
  const sentences = graphSentences(graph, names);
  const nodes = graph.nodes.map((node): BoxNode | PillNode | ValueNodeType => {
    const [x, y] = node.position ?? [UNPLACED_X, UNPLACED_STEP * unplaced++];
    const position = { x, y };
    if (node.does === "get") {
      const read = node.pins[0];
      const valueType = read?.carries ?? "text";
      const data = {
        name: names.get(node.variable) ?? node.variable,
        valueType,
        // A list's square and ranges' hollow square, as a box's pin (the owner, 2026-11-17).
        ...(read?.list === true && { list: true }),
        ...(read?.ranges === true && { ranges: true }),
        faulty: faulty.has(node.id),
      };
      return { id: node.id, type: "pill", position, data, selected: node.id === chosen };
    }
    const sentence = sentences.get(node.id) ?? [];
    if (node.family === "value") {
      // A value drawn compact, its inputs a wire feeds known so it shows their chips no more (the owner, 2026-11-17).
      const wired = graph.wires.filter((wire) => wire.to[0] === node.id).map((wire) => wire.to[1]);
      const data = { node, sentence, wired, faulty: faulty.has(node.id) };
      // Its whole sentence names the node the keyboard focuses: the symbols in its middle only stand for it.
      const ariaLabel = sentence.map((part) => ("text" in part ? part.text : part.chip)).join("");
      return { id: node.id, type: "value", position, data, selected: node.id === chosen, ariaLabel };
    }
    const data = { node, sentence, faulty: faulty.has(node.id) };
    return { id: node.id, type: "box", position, data, selected: node.id === chosen };
  });
  const edges: Edge[] = graph.wires.map(({ from, to }) => {
    const carries = byId.get(from[0])?.pins.find((pin) => pin.id === from[1] && pin.side === "out")?.carries ?? "exec";
    return {
      id: `${from.join(".")}>${to.join(".")}`,
      source: from[0],
      sourceHandle: from[1],
      target: to[0],
      targetHandle: to[1],
      type: "wire",
      className: `blueprint-type-${carries}`,
    };
  });
  return { nodes, edges };
}

/** The node React Flow's changes choose, by a click or by Enter on a focused node, if any. */
export function chosenNode(changes: readonly NodeChange[]): string | null {
  const chosen = changes.find((change) => change.type === "select" && change.selected);
  return chosen?.type === "select" ? chosen.id : null;
}
